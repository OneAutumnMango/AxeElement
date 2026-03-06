using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace AxeElement
{
    public class AxeMovementObject : SpellObject
    {
        private enum Phase { BackStep, Dash, Done }

        private const float BACKSTEP_SPEED = 5f;
        private const float BACKSTEP_TIME  = 0.3f;
        private const float DASH_SPEED     = 40f;
        private const float DASH_TIME      = 0.2f;

        public UnityEngine.Object impact;

        private Phase phase;
        private float phaseTimer;
        private UnitStatus wizardUs;
        private PhysicsBody phys;
        private HashSet<int> hitOwners = new HashSet<int>();
        private bool dying;

        public AxeMovementObject()
        {
            this.DAMAGE     = 4f;
            this.POWER      = 30f;
            this.START_TIME = BACKSTEP_TIME + DASH_TIME;
        }

        protected override void Awake()
        {
            base.Awake();
            if (this.id == null)
                this.id = new Identity();
        }

        private void Start()
        {
            // Set deathTimer so base-class logic doesn't trigger early death.
            this.deathTimer = Time.time + this.START_TIME;
        }

        public void Init(Identity identity)
        {
            this.localSpellObjectStart(identity.owner, identity.gameObject);
        }

        private void localSpellObjectStart(int owner, GameObject wizardGo)
        {
            this.id.owner = owner;
            this.hitOwners.Clear();
            this.hitOwners.Add(owner);

            this.wizardUs = wizardGo?.GetComponent<UnitStatus>();
            this.phys     = wizardGo?.GetComponent<PhysicsBody>();

            if (this.wizardUs != null)
                this.wizardUs.RegisterCallback(this);

            this.phase      = Phase.BackStep;
            this.phaseTimer = Time.time + BACKSTEP_TIME;

            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-cast", 5f);

            if (wizardGo != null)
                base.transform.position = wizardGo.transform.position;

            base.ChangeToSpellLayerDelayed(DASH_SPEED);
        }

        private void FixedUpdate()
        {
            if (this.wizardUs != null)
                base.transform.position = this.wizardUs.transform.position;

            if (Globals.online && base.photonView != null && base.photonView.IsConnectedAndNotLocal())
            {
                this.BaseClientCorrection();
                return;
            }

            if (this.dying) return;

            if (this.phase == Phase.BackStep)
            {
                if (this.phys != null)
                    this.phys.abilityVelocity = -base.transform.forward * BACKSTEP_SPEED;
                if (Time.time >= this.phaseTimer)
                {
                    this.phase      = Phase.Dash;
                    this.phaseTimer = Time.time + DASH_TIME;
                }
            }
            else if (this.phase == Phase.Dash)
            {
                if (this.phys != null)
                    this.phys.abilityVelocity = base.transform.forward * DASH_SPEED;
                if (Time.time >= this.phaseTimer)
                    this.SpellObjectDeath();
            }
        }

        public override void SpellObjectCallback(Identity identity, Vector3 position,
            Quaternion rotation, GameObject go, Collision collision)
        {
            if (Globals.online && (base.photonView == null || base.photonView.IsConnectedAndNotLocal())) return;
            if (this.phase != Phase.Dash) return;

            Identity ident = go.GetComponent<Identity>();
            if (ident == null) return;
            if (!GameUtility.IdentityCompare(go, UnitType.Unit)) return;
            if (this.hitOwners.Contains(ident.owner)) return;

            this.hitOwners.Add(ident.owner);

            this.rpcCollision(base.transform.position);

            UnitStatus us = go.GetComponent<UnitStatus>();
            if (us != null)
                us.ApplyDamage(this.DAMAGE, this.id.owner, 0);

            // Push enemy in dash direction.
            PhysicsBody pb = go.GetComponent<PhysicsBody>();
            if (pb != null)
                pb.AddForceOwner(base.transform.forward * this.POWER);
        }

        public override void SpellObjectDeath()
        {
            if (Globals.online && base.photonView != null)
                base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All,
                    Array.Empty<object>());
            else
                this.rpcSpellObjectDeath();
        }

        private void OnDestroy()
        {
            if (this.wizardUs != null)
                this.wizardUs.UnRegisterCallback(this);
            if (this.phys != null)
                this.phys.abilityVelocity = Vector3.zero;
        }

        [PunRPC]
        public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, int viewId)
        {
            base.transform.position = pos;
            base.transform.rotation = rot;
            GameObject wizardGo = null;
            if (viewId != -1)
            {
                PhotonView pv = PhotonView.Find(viewId);
                wizardGo = pv?.gameObject;
            }
            this.localSpellObjectStart(owner, wizardGo);
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.dying = true;
            this.phase = Phase.Done;

            if (this.phys != null)
                this.phys.abilityVelocity = Vector3.zero;

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
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/double-strike-impact", 5f);
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            this.BaseSerialize(stream, info);
        }
    }
}
