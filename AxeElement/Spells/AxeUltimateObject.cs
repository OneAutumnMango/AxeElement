using System;
using System.Collections.Generic;
using UnityEngine;

namespace AxeElement
{
    /// <summary>
    /// Blood Field ultimate: creates a persistent dark field that follows the caster.
    /// All enemies inside are immediately bled and slowed 12.5 %.
    /// Minimum duration 5 s; field persists while any enemy is inside, then collapses
    /// the moment the last enemy leaves.
    /// After leaving the field, the slow lingers for 0.5 s.
    /// Deals no direct damage — the surrounding Bleed / lifesteal patches supply the
    /// offensive payoff.
    /// </summary>
    public class AxeUltimateObject : SpellObject
    {
        // ── Tuning ──────────────────────────────────────────────────────────────
        private const float FIELD_RADIUS   = 10f;
        private const float FIELD_DURATION = 5f;
        private const float SLOW_MULT      = 0.875f;  // 12.5 % speed reduction
        private const float LINGER         = 0.5f;    // seconds slow persists after leaving
        private const float TICK           = 0.25f;   // AOE check interval

        // ── Instance state ───────────────────────────────────────────────────────
        // Maps enemy owner id → (WizardController, when-the-linger-expires).
        // float.MaxValue = target is still inside the field (linger not yet started).
        private readonly Dictionary<int, (WizardController wc, float lingerExpiry)> _slowed
            = new Dictionary<int, (WizardController wc, float lingerExpiry)>();

        private float            _fieldExpiry;    // abs time the minimum duration expires
        private bool             _someoneInside;  // true if any enemy was in range last tick
        private float            _nextTick;
        private bool             _dying;
        private GameObject       _disc;           // dark ground-plane visual
        private WizardController _casterWc;       // caster reference for position tracking

        // ── Compatibility stub — AxeWizardStatusPatch still calls this ─────────
        public static void NotifyDamage(int owner, float damage, UnitStatus unit) { }

        // ── SpellObject base plumbing ────────────────────────────────────────────
        protected override void Awake()
        {
            base.Awake();
            if (id == null) id = new Identity();
        }

        // ── Init: called by AxeUltimate.cs immediately after AddComponent ─────────
        public void Init(Identity identity)
        {
            this.id.owner = identity.owner;

            if (Globals.online)
            {
                if (base.photonView != null && base.photonView.isMine)
                {
                    base.photonView.RPCLocal(this, "rpcFieldStart", PhotonTargets.All,
                        new object[] { identity.owner, identity.gameObject.GetPhotonView().viewID });
                }
                // Non-owner clients receive the RPC; nothing else required.
            }
            else
            {
                this.localFieldStart(identity.owner, identity.gameObject);
            }
        }

        [PunRPC]
        public void rpcFieldStart(int owner, int wizardViewId)
        {
            var go = PhotonView.Find(wizardViewId)?.gameObject;
            this.localFieldStart(owner, go);
        }

        private void localFieldStart(int owner, GameObject wizardGo)
        {
            this.id.owner     = owner;
            this._fieldExpiry = Time.time + FIELD_DURATION;
            this._nextTick    = Time.time;            // run first tick immediately

            // Cache the caster's controller for position tracking
            this._casterWc = wizardGo?.GetComponent<WizardController>();

            // Snap to caster
            if (wizardGo != null)
                base.transform.position = wizardGo.transform.position;

            // Apply bleed to every enemy currently in range
            this.SweepBleed();

            // Spawn the dark ground disc — parented to this object so it follows
            this._disc = CreateFieldDisc();
            if (this._disc != null)
            {
                this._disc.transform.SetParent(base.transform, worldPositionStays: false);
                this._disc.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                this._disc.transform.localRotation = Quaternion.identity;
            }

            // Cast sound: reuse the melee impact sound for now
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-hit", 5f);
        }

        // ── Per-frame logic ──────────────────────────────────────────────────────
        private void Update()
        {
            if (this._dying) return;

            // Track caster — field follows the caster
            if (this._casterWc != null)
                base.transform.position = this._casterWc.transform.position;

            // AOE tick
            if (Time.time >= this._nextTick)
            {
                this._nextTick = Time.time + TICK;
                this.TickField();
            }

            // Drain linger timers
            foreach (int key in new List<int>(this._slowed.Keys))
                if (Time.time >= this._slowed[key].lingerExpiry)
                    this.RemoveSlow(key);

            // Field expiry: minimum 5 s, then dies immediately when no one is inside
            if (Time.time >= this._fieldExpiry && !this._someoneInside)
                this.BeginDie();
        }

        // ── AOE tick: maintain slows and refresh bleed for targets inside ────────
        private void TickField()
        {
            Collider[] hits = GameUtility.GetAllInSphere(
                base.transform.position, FIELD_RADIUS, this.id.owner, new UnitType[1]);

            var inField = new HashSet<int>();

            foreach (Collider col in hits)
            {
                GameObject go = col.transform.root.gameObject;
                var eid = go.GetComponent<Identity>();
                var wc  = go.GetComponent<WizardController>();
                if (eid == null || wc == null) continue;
                if (!inField.Add(eid.owner)) continue;   // deduplicate per wizard

                // Keep bleed refreshed
                BleedManager.ApplyBleed(eid.owner, go, AxeMeleeObject.BleedEffectPrefab);

                // 1 DPS — apply on the local authority for each target
                if (!Globals.online || !go.GetPhotonView().IsConnectedAndNotLocal())
                {
                    var us = go.GetComponent<UnitStatus>();
                    if (us != null)
                        us.ApplyDamage(1f * TICK, this.id.owner, (int)Axe.AxeUltimate);
                }

                // Apply or re-anchor slow
                if (this._slowed.TryGetValue(eid.owner, out var existing))
                    // Cancel any linger countdown — they're back inside
                    this._slowed[eid.owner] = (existing.wc, float.MaxValue);
                else
                    this.ApplySlow(eid.owner, wc);
            }

            // For every slowed target that is NOT in the field this tick, start linger
            foreach (int key in new List<int>(this._slowed.Keys))
            {
                if (!inField.Contains(key))
                {
                    var entry = this._slowed[key];
                    if (entry.lingerExpiry == float.MaxValue)
                        this._slowed[key] = (entry.wc, Time.time + LINGER);
                }
            }

            // Track occupancy for field expiry logic
            this._someoneInside = inField.Count > 0;
        }

        // ── Apply bleed to all in radius at cast time ────────────────────────────
        private void SweepBleed()
        {
            Collider[] hits = GameUtility.GetAllInSphere(
                base.transform.position, FIELD_RADIUS, this.id.owner, new UnitType[1]);
            var seen = new HashSet<int>();
            foreach (Collider col in hits)
            {
                GameObject go = col.transform.root.gameObject;
                var eid = go.GetComponent<Identity>();
                if (eid == null || !seen.Add(eid.owner)) continue;
                BleedManager.ApplyBleed(eid.owner, go, AxeMeleeObject.BleedEffectPrefab);
            }
        }

        // ── Slow helpers (instance-scoped so multi-field stacking is safe) ───────
        private void ApplySlow(int targetOwner, WizardController wc)
        {
            if (this._slowed.ContainsKey(targetOwner)) return;
            wc.MOVEMENT_SPEED *= SLOW_MULT;
            this._slowed[targetOwner] = (wc, float.MaxValue);
        }

        private void RemoveSlow(int targetOwner)
        {
            if (!this._slowed.TryGetValue(targetOwner, out var entry)) return;
            if (entry.wc != null)
                entry.wc.MOVEMENT_SPEED /= SLOW_MULT;
            this._slowed.Remove(targetOwner);
        }

        private void CleanupAll()
        {
            foreach (int key in new List<int>(this._slowed.Keys))
                this.RemoveSlow(key);

            if (this._disc != null)
            {
                UnityEngine.Object.Destroy(this._disc);
                this._disc = null;
            }
        }

        // ── Death ────────────────────────────────────────────────────────────────
        private void BeginDie()
        {
            if (this._dying) return;
            this._dying = true;
            this.CleanupAll();
            this.rpcSpellObjectDeath();
        }

        private void OnDestroy()
        {
            this.CleanupAll();
        }

        public override void SpellObjectDeath()
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.CleanupAll();
            UnityEngine.Object.Destroy(base.gameObject, 0.5f);
        }

        // ── Visual: blood pool with rising tendril particles ─────────────────────
        private static GameObject CreateFieldDisc()
        {
            try
            {
                // Parent container — position/parent set by caller
                var root = new GameObject("BloodField");

                // ── Ground pool disc ─────────────────────────────────────────────
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.transform.SetParent(root.transform, false);
                disc.transform.localScale    = new Vector3(FIELD_RADIUS * 2f, 0.05f, FIELD_RADIUS * 2f);
                disc.transform.localPosition = Vector3.zero;

                var discCol = disc.GetComponent<Collider>();
                if (discCol != null) UnityEngine.Object.Destroy(discCol);

                var rend = disc.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    if (mat != null)
                    {
                        mat.color = new Color(0.12f, 0.01f, 0.005f, 0.65f);
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = 3000;
                        rend.material = mat;
                    }
                }

                // ── Rising blood tendril particles ───────────────────────────────
                var psGo = new GameObject("BloodTendrils");
                psGo.transform.SetParent(root.transform, false);
                psGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                // Rotate 90° on X so the Circle shape lies flat on the ground (XZ plane)
                psGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var ps    = psGo.AddComponent<ParticleSystem>();
                var psRend = psGo.GetComponent<ParticleSystemRenderer>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                main.loop            = true;
                main.prewarm         = true;   // start with full particle count immediately
                main.startLifetime   = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                main.startSpeed      = new ParticleSystem.MinMaxCurve(0.5f, 2.0f);
                main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);  // finer
                main.startColor      = new ParticleSystem.MinMaxGradient(
                    new Color(1.0f, 0.15f, 0.05f, 1.0f),
                    new Color(0.5f, 0.03f, 0.01f, 0.9f));
                main.startRotation   = 0f;
                main.maxParticles    = 500;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0f;

                var emission = ps.emission;
                emission.rateOverTime = 70f;

                var shape = ps.shape;
                shape.enabled         = true;
                shape.shapeType       = ParticleSystemShapeType.Circle;
                shape.radius          = FIELD_RADIUS * 0.85f;  // cover full disc area
                shape.radiusThickness = 1.0f;

                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space   = ParticleSystemSimulationSpace.World;
                vel.y       = new ParticleSystem.MinMaxCurve(1.0f, 3.0f);   // upward rise
                vel.radial  = new ParticleSystem.MinMaxCurve(-0.5f, 0.0f);  // inward pull

                // Low frequency noise = smooth large-scale curl (not scatter)
                var noise = ps.noise;
                noise.enabled          = true;
                noise.strength         = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
                noise.frequency        = 0.12f;
                noise.scrollSpeed      = 0.20f;
                noise.damping          = true;
                noise.octaveCount      = 2;
                noise.octaveMultiplier = 0.5f;
                noise.octaveScale      = 2.0f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0.0f, 1.0f),
                    new Keyframe(0.5f, 0.8f),
                    new Keyframe(1.0f, 0.0f)));

                var colorLife = ps.colorOverLifetime;
                colorLife.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1.0f, 0.20f, 0.05f), 0.00f),
                        new GradientColorKey(new Color(0.6f, 0.05f, 0.02f), 0.40f),
                        new GradientColorKey(new Color(0.2f, 0.01f, 0.00f), 0.80f),
                        new GradientColorKey(new Color(0.02f, 0f,   0.00f), 1.00f),
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(0.00f, 0.00f),
                        new GradientAlphaKey(1.00f, 0.05f),
                        new GradientAlphaKey(0.80f, 0.50f),
                        new GradientAlphaKey(0.00f, 1.00f),
                    });
                colorLife.color = new ParticleSystem.MinMaxGradient(grad);

                // Trail module: each particle drags a ribbon behind it as it follows
                // the curved noise path — this is what creates the tendril appearance
                var trails = ps.trails;
                trails.enabled           = true;
                trails.lifetime          = new ParticleSystem.MinMaxCurve(0.4f);
                trails.minVertexDistance = 0.05f;
                trails.textureMode       = ParticleSystemTrailTextureMode.Stretch;
                trails.dieWithParticles  = true;
                trails.worldSpace        = true;
                var trailWidthCurve = new AnimationCurve(
                    new Keyframe(0.0f, 1.0f),
                    new Keyframe(0.4f, 0.6f),
                    new Keyframe(1.0f, 0.0f));
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, trailWidthCurve);

                // Billboard for particle head; trail does the bulk of the visual work
                psRend.renderMode = ParticleSystemRenderMode.Billboard;

                Material psMat = null;
                foreach (var sn in new[] {
                    "Particles/Additive",
                    "Particles/Standard Unlit",
                    "Particles/Alpha Blended",
                    "Legacy Shaders/Particles/Alpha Blended" })
                {
                    var s = Shader.Find(sn);
                    if (s != null) { psMat = new Material(s); break; }
                }
                if (psMat == null) psMat = new Material(Shader.Find("Standard"));
                if (psMat != null) { psMat.color = Color.white; psRend.material = psMat; }

                // Trail material (separate renderer slot — same additive shader)
                Material trailMat = null;
                foreach (var sn in new[] {
                    "Particles/Additive",
                    "Legacy Shaders/Particles/Additive",
                    "Sprites/Default" })
                {
                    var s = Shader.Find(sn);
                    if (s != null) { trailMat = new Material(s); break; }
                }
                if (trailMat != null) psRend.trailMaterial = trailMat;

                ps.Play();

                Plugin.Log.LogInfo("[BloodField] Field visual created (disc + tendrils)");
                return root;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[BloodField] Disc creation failed: {ex.Message}");
                return null;
            }
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) { }
    }
}
