using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace AxeElement
{
    public class AxeSecondaryObject : SpellObject
    {
        private static readonly Color AxeGreyColor = new Color(0.85f, 0.85f, 0.9f);

        public UnityEngine.Object impact;

        private PhysicsBody phys;
        private Collider col;
        private float arcRate;
        private float velSpeed;
        private bool dying;
        private Transform child;
        private HashSet<int> hitOwners = new HashSet<int>();

        public AxeSecondaryObject()
        {
            DAMAGE = 8f;
            RADIUS = 2.5f;
            POWER = 25f;
            Y_POWER = 0f;
            START_TIME = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (this.id == null)
                this.id = new Identity();
        }

        private void Start()
        {
            if (base.transform.childCount > 0)
            {
                this.child = base.transform.GetChild(0);
                // Rotate the visual child 90° on the forward axis so the
                // projectile disc/glaive spins in the horizontal plane.
                this.child.localRotation = Quaternion.Euler(0f, 0f, 90f) * this.child.localRotation;
            }
            this.phys = base.GetComponent<PhysicsBody>();
            this.col = base.GetComponent<Collider>();
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-cast", 5f);
            this.deathTimer = Time.time + this.START_TIME;
            ApplyGreyColor();
        }

        public void Init(int owner, float arcRate, float velocity)
        {
            this.id.owner = owner;
            this.arcRate = arcRate;
            this.velSpeed = velocity;
            this.hitOwners.Add(owner); // never hit caster
            base.ChangeToSpellLayerDelayed(velocity);
            ApplyGreyColor();
            if (!base.photonView.IsConnectedAndNotLocal())
            {
                base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.All,
                    new object[] { owner, base.transform.position, base.transform.rotation, arcRate, velocity });
            }
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
                            mat.color = AxeGreyColor;
                        if (mat.HasProperty("_EmissionColor"))
                            mat.SetColor("_EmissionColor", AxeGreyColor * 2f);
                    }
                }
            }
            foreach (Light light in base.gameObject.GetComponentsInChildren<Light>(true))
                light.color = AxeGreyColor;
            foreach (ParticleSystem ps in base.gameObject.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = AxeGreyColor;
            }
        }

        private void FixedUpdate()
        {
            // Spin the visual child
            if (this.child != null)
            {
                Vector3 le = this.child.localEulerAngles;
                le.z -= 30f;
                this.child.localEulerAngles = le;
            }

            if (!this.dying)
            {
                // Arc the projectile's heading each fixed frame
                Vector3 euler = base.transform.eulerAngles;
                euler.y += this.arcRate;
                base.transform.eulerAngles = euler;
            }

            if (this.phys != null)
                this.phys.velocity = base.transform.forward * this.velSpeed + Vector3.up * this.phys.velocity.y;

            if (base.photonView.IsConnectedAndNotLocal())
            {
                this.BaseClientCorrection();
                return;
            }

            if (!this.dying && this.deathTimer < Time.time)
                this.SpellObjectDeath();
        }

        public override void SpellObjectDeath()
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (base.photonView.IsConnectedAndNotLocal()) return;
            if (this.dying) return;

            GameObject go = collision.transform.root.gameObject;
            Identity ident = go.GetComponent<Identity>();
            if (ident == null) return;

            if (GameUtility.IdentityCompare(go, UnitType.Unit) && !hitOwners.Contains(ident.owner))
            {
                hitOwners.Add(ident.owner);

                // Pierce: ignore future collision with this exact collider
                Collider enemyCol = collision.collider;
                if (enemyCol != null && this.col != null)
                    Physics.IgnoreCollision(this.col, enemyCol, true);

                base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All,
                    new object[] { base.transform.position });

                UnitStatus us = go.GetComponent<UnitStatus>();
                if (us != null)
                    us.ApplyDamage(this.DAMAGE, this.id.owner, 0);

                PhysicsBody pb = go.GetComponent<PhysicsBody>();
                if (pb != null)
                    pb.AddForceOwner(GameUtility.GetForceVector(base.transform.position, go.transform.position, this.POWER));
            }
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.dying = true;
            if (this.col != null) this.col.enabled = false;
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
        public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, float arcRate, float velocity)
        {
            this.id.owner = owner;
            this.hitOwners.Clear();
            this.hitOwners.Add(owner);
            base.transform.position = pos;
            base.transform.rotation = rot;
            this.arcRate = arcRate;
            this.velSpeed = velocity;
            this.deathTimer = Time.time + this.START_TIME;
            ApplyGreyColor();
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
            this.BaseSerialize(stream, info);
        }
    }
}
