using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace AxeElement
{
    public class AxeUtilityObject : SpellObject
    {
        private static readonly Color GlaiveCrimsonColor = new Color(0.55f, 0.05f, 0.05f);

        private const float ORBIT_RADIUS   = 2.5f;
        private const float ANGULAR_SPEED  = 180f;   // degrees/s — full orbit in ~2.0 s
        private const float LIFETIME       = 5f;
        private const float HIT_RADIUS     = 1.5f;
        private const float HIT_DAMAGE     = 5f;
        private const float HIT_COOLDOWN   = 1.0f;   // min seconds between hits on same target
        private const float HIT_PUSH       = 1f;  // basically no knockback
        private const float SPIN_SPEED     = 360f*1.5f;   // degrees/s self-spin on local Y axis

        public UnityEngine.Object impact;

        /// <summary>
        /// True on the casting client; false on all remote clients.
        /// Controls whether this instance runs hit-detection and applies damage.
        /// </summary>
        public bool isOwnerClient = false;

        private float angle;
        private float spinAngle;
        private UnitStatus wizardUs;
        private bool dying;
        private Dictionary<int, float> hitCooldowns = new Dictionary<int, float>();

        public AxeUtilityObject()
        {
            this.DAMAGE     = HIT_DAMAGE;
            this.START_TIME = LIFETIME;
        }

        protected override void Awake()
        {
            base.Awake();
            if (this.id == null)
                this.id = new Identity();
        }

        private void Start()
        {
            this.deathTimer = Time.time + LIFETIME;
        }

        // ── Called by AxeUtility.SpawnGlaive on the caster ─────────────────
        public void Init(Identity identity, float startAngle)
        {
            this.isOwnerClient = true;
            this.InitRemote(identity.owner, identity.gameObject, startAngle);
        }

        // ── Called on EVERY client (via AxeUtility.SpawnGlaiveLocal) ────────
        public void InitRemote(int owner, GameObject wizardGo, float startAngle)
        {
            this.localSpellObjectStart(owner, wizardGo, startAngle);
        }

        private void localSpellObjectStart(int owner, GameObject wizardGo, float startAngle)
        {
            this.id.owner = owner;
            this.angle    = startAngle;
            this.wizardUs = wizardGo?.GetComponent<UnitStatus>();

            if (wizardGo != null)
            {
                float rad = startAngle * Mathf.Deg2Rad;
                base.transform.position = wizardGo.transform.position +
                    new Vector3(Mathf.Sin(rad), 0.2f, Mathf.Cos(rad)) * ORBIT_RADIUS;
            }

            ApplyGreyColor();

            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-cast", 5f);

            this.deathTimer = Time.time + LIFETIME;
        }

        private void ApplyGreyColor()
        {
            foreach (Renderer renderer in base.gameObject.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material mat in renderer.materials)
                {
                    if (mat != null)
                    {
                        if (mat.HasProperty("_Color"))
                            mat.color = GlaiveCrimsonColor;
                        if (mat.HasProperty("_EmissionColor"))
                            mat.SetColor("_EmissionColor", GlaiveCrimsonColor * 2f);
                    }
                }
            }
            foreach (Light light in base.gameObject.GetComponentsInChildren<Light>(true))
                light.color = GlaiveCrimsonColor;
            foreach (ParticleSystem ps in base.gameObject.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = GlaiveCrimsonColor;
            }
        }

        private void FixedUpdate()
        {
            // Advance orbit on all clients for smooth visuals.
            this.angle     += ANGULAR_SPEED * Time.fixedDeltaTime;
            this.spinAngle += SPIN_SPEED    * Time.fixedDeltaTime;
            if (this.wizardUs != null)
            {
                float rad = this.angle * Mathf.Deg2Rad;
                base.transform.position = this.wizardUs.transform.position +
                    new Vector3(Mathf.Sin(rad), 0.2f, Mathf.Cos(rad)) * ORBIT_RADIUS;

                // Face the tangent (direction of travel).
                float trad = (this.angle + 90f) * Mathf.Deg2Rad;
                Vector3 tangent = new Vector3(Mathf.Sin(trad), 0f, Mathf.Cos(trad));
                if (tangent != Vector3.zero)
                    base.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up)
                        * Quaternion.Euler(0, this.spinAngle, 0);
            }

            // Remote clients only need visuals — no hit detection or damage.
            // Still check death timer so the GO is cleaned up when the lifetime expires.
            if (Globals.online && !this.isOwnerClient)
            {
                if (!this.dying && Time.time >= this.deathTimer)
                    this.SpellObjectDeath();
                return;
            }
            if (this.dying) return;

            // Lifetime check.
            if (Time.time >= this.deathTimer)
            {
                this.SpellObjectDeath();
                return;
            }

            // Hit detection.
            var cols = GameUtility.GetAllInSphere(base.transform.position, HIT_RADIUS,
                this.id.owner, new UnitType[] { UnitType.Unit });
            foreach (var col in cols)
            {
                if (col == null) break;

                Identity ident = col.GetComponent<Identity>();
                if (ident == null) ident = col.GetComponentInParent<Identity>();
                if (ident == null || ident.owner == this.id.owner) continue;

                if (this.hitCooldowns.TryGetValue(ident.owner, out float nextHit) && Time.time < nextHit)
                    continue;

                this.hitCooldowns[ident.owner] = Time.time + HIT_COOLDOWN;

                // Impact effect + sound (local only — GO has no PhotonView)
                this.rpcCollision(base.transform.position);

                UnitStatus us = col.GetComponent<UnitStatus>();
                if (us != null)
                    us.ApplyDamage(HIT_DAMAGE, this.id.owner, 0);

                PhysicsBody pb = col.GetComponent<PhysicsBody>();
                if (pb != null && this.wizardUs != null)
                {
                    Vector3 pushDir = (col.transform.position - this.wizardUs.transform.position).WithY(0f);
                    if (pushDir == Vector3.zero) pushDir = base.transform.forward;
                    pb.AddForceOwner(pushDir.normalized * HIT_PUSH);
                }
            }
        }

        public override void SpellObjectDeath()
        {
            // GO has no PhotonView — just handle locally.
            this.rpcSpellObjectDeath();
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.dying = true;

            foreach (Transform t in base.transform)
            {
                Light lt = t.GetComponent<Light>();
                if (lt != null)
                    lt.enabled = false;
                else
                    t.DOScale(0f, 0.25f);
                ParticleSystem ps = t.GetComponent<ParticleSystem>();
                if (ps != null) { var em = ps.emission; em.enabled = false; }
                foreach (Transform sub in t)
                {
                    ParticleSystem ps2 = sub.GetComponent<ParticleSystem>();
                    if (ps2 != null) { var em2 = ps2.emission; em2.enabled = false; }
                }
            }
            UnityEngine.Object.Destroy(base.gameObject, 1f);
        }

        [PunRPC]
        public void rpcCollision(Vector3 pos)
        {
            if (this.impact != null)
                UnityEngine.Object.Instantiate(this.impact, pos, Globals.sideways);
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/reflex-impact", 5f);
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            // GO is local-only; no Photon serialization needed.
        }
    }
}
