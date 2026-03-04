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
                this._disc.transform.localPosition = Vector3.down * 0.5f;
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

        // ── Visual: semi-transparent dark cylinder (parented, position set by caller) ─
        private static GameObject CreateFieldDisc()
        {
            try
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.transform.localScale = new Vector3(FIELD_RADIUS * 2f, 0.05f, FIELD_RADIUS * 2f);

                // Strip the collider — we don't want physics interaction
                var col = disc.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);

                // Dark crimson semi-transparent material
                var rend = disc.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    if (mat != null)
                    {
                        mat.color = new Color(0.15f, 0.02f, 0.01f, 0.5f);
                        // Switch to Transparent rendering mode
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

                return disc;
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
