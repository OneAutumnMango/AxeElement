using System;
using DG.Tweening;
using FMOD.Studio;
using UnityEngine;

namespace AxeElement
{
    public class AxePrimaryObject : SpellObject
    {
        public UnityEngine.Object impact;

        private PhysicsBody phys;
        private float accel = 1f;
        private float decel = 0.8f;
        private bool dying;
        private Transform child;
        private EventInstance aSource;
        private new float curve;
        private new float velocity;

        public AxePrimaryObject()
        {
            DAMAGE = 7f;
            RADIUS = 3f;
            POWER = 15f;
            Y_POWER = 0f;
            START_TIME = 1.3f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (this.id == null)
                this.id = new Identity();
        }

        private void Start()
        {
            this.child = base.transform.GetChild(0);
            this.phys = base.GetComponent<PhysicsBody>();
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-cast", 5f);
            this.deathTimer = Time.time + this.START_TIME;
            AxeColorUtility.ApplyCrimsonColor(base.gameObject);
        }

        public void Init(int owner, float curve, float velocity)
        {
            this.id.owner = owner;
            this.curve = curve;
            this.velocity = velocity;
            base.ChangeToSpellLayerDelayed(velocity);
            AxeColorUtility.ApplyCrimsonColor(base.gameObject);
            if (!base.photonView.IsConnectedAndNotLocal())
            {
                base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.All,
                    new object[] { owner, base.transform.position, base.transform.rotation, curve, velocity });
            }
        }

        private void FixedUpdate()
        {
            if (this.child != null)
            {
                Vector3 le = this.child.localEulerAngles;
                le.z -= 30f;
                this.child.localEulerAngles = le;
            }

            if (!this.dying)
            {
                this.velocity *= this.accel;
                this.curve *= this.accel;
            }
            else
            {
                this.velocity *= this.decel;
                this.curve *= this.decel;
            }

            if (this.phys != null)
                this.phys.velocity = base.transform.forward * this.velocity + Vector3.up * this.phys.velocity.y;

            Vector3 euler = base.transform.eulerAngles;
            euler.y += this.curve;
            base.transform.eulerAngles = euler;

            if (base.photonView.IsConnectedAndNotLocal())
            {
                this.BaseClientCorrection();
                return;
            }

            if (this.deathTimer < Time.time && !this.dying)
                this.SpellObjectDeath();
        }

        public override void SpellObjectDeath()
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (base.photonView.IsConnectedAndNotLocal()) return;

            GameObject go = collision.transform.root.gameObject;
            if (!this.dying &&
                GameUtility.IdentityCompare(go, UnitType.Unit) &&
                !GameUtility.IdentityCompare(go, this.id.owner))
            {
                base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All,
                    new object[] { base.transform.position });

                Collider[] allInSphere = GameUtility.GetAllInSphere(
                    base.transform.position, this.RADIUS, this.id.owner, new UnitType[1]);
                var hitTargets = new System.Collections.Generic.HashSet<GameObject>();
                for (int i = 0; i < allInSphere.Length; i++)
                {
                    GameObject target = allInSphere[i].transform.root.gameObject;
                    if (!hitTargets.Add(target)) continue;
                    target.GetComponent<PhysicsBody>().AddForceOwner(
                        GameUtility.GetForceVector(base.transform.position, target.transform.position, this.POWER));
                    target.GetComponent<UnitStatus>().ApplyDamage(this.DAMAGE, this.id.owner, 0);
                }

                this.SpellObjectDeath();
            }
        }

        private void OnDestroy()
        {
            if (this.aSource.isValid())
                this.aSource.FadeSoundOut(0f, 0.2f, 0f);
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.dying = true;
            CapsuleCollider cc = base.GetComponent<CapsuleCollider>();
            if (cc != null) cc.enabled = false;
            foreach (Transform child in base.transform)
            {
                Light lt = child.GetComponent<Light>();
                if (lt != null)
                    lt.enabled = false;
                else
                    child.DOScale(0f, 0.25f);
                ParticleSystem ps = child.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    UnityEngine.Object.Destroy(ps);
                    var em = ps.emission; em.enabled = false;
                }
                foreach (Transform sub in child)
                {
                    ParticleSystem ps2 = sub.GetComponent<ParticleSystem>();
                    if (ps2 != null) { var em2 = ps2.emission; em2.enabled = false; }
                }
            }
            UnityEngine.Object.Destroy(base.gameObject, 1f);
        }

        [PunRPC]
        public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, float curve, float velocity)
        {
            this.id.owner = owner;
            base.transform.position = pos;
            base.transform.rotation = rot;
            this.curve = curve;
            this.velocity = velocity;
            this.deathTimer = Time.time + this.START_TIME;
            AxeColorUtility.ApplyCrimsonColor(base.gameObject);
            // Delayed recolor to ensure materials are loaded on remote clients
            Invoke("ApplyCrimsonColorDelayed", 0.05f);
        }

        private void ApplyCrimsonColorDelayed()
        {
            AxeColorUtility.ApplyCrimsonColor(base.gameObject);
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
