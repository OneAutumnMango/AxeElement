using System;
using System.Collections.Generic;
using PigeonCoopToolkit.Effects.Trails;
using UnityEngine;

namespace AxeElement
{
    public class AxeDefensiveObject : SpellObject
    {
        // Configurable: how long the player stands still waiting for a hit.
        public float parryWindow = 1f;

        private const float COUNTER_DAMAGE = 5f;

        // From Double Strike prefab.
        public UnityEngine.Object impact;
        public ParticleSystem distortionTrail;
        public ParticleSystem distortion;
        public UnityEngine.Object effect;
        public ParticleSystem effectStart;
        public SmokeTrail trail;

        private WizardController wc;
        private NetworkedWizard nw;
        private AxeDefensiveState state = AxeDefensiveState.Dead;
        private float stateTimer;
        private bool _speedReduced;

        // Static registry: defender owner ID → active AxeDefensiveObject
        public static Dictionary<int, AxeDefensiveObject> activeDefensives =
            new Dictionary<int, AxeDefensiveObject>();

        // Brief knockback immunity window after a parry fires (outlasts the ApplyDamage→AddForceOwner sequence).
        public static Dictionary<int, float> recentlyParriedUntil =
            new Dictionary<int, float>();

        public static void NotifyDamage(int attackerOwner, float damage, WizardStatus targetWizard)
        {
            if (targetWizard == null || damage <= 0f) return;
            Identity ident = targetWizard.GetComponent<Identity>();
            if (ident == null) return;
            int wizardOwner = ident.owner;
            if (!activeDefensives.TryGetValue(wizardOwner, out var def)) return;
            if (def != null)
                def.RegisterDamage(attackerOwner, damage);
        }

        protected override void Awake()
        {
            base.Awake();
            if (this.id == null)
                this.id = new Identity();
        }

        public void Init(Identity identity)
        {
            this.id.owner = identity.owner;
            if (Globals.online)
            {
                if (base.photonView != null && base.photonView.isMine)
                {
                    base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.All, new object[]
                    {
                        identity.owner,
                        identity.gameObject.GetPhotonView().viewID
                    });
                }
            }
            else
            {
                this.localSpellObjectStart(identity.owner, identity.gameObject);
            }
        }

        private void Update()
        {
            if (this.wc != null)
                base.transform.position = this.wc.transform.position;

            if (this.state == AxeDefensiveState.Active && this.stateTimer < Time.time)
                this.EndWithoutTrigger();
        }

        private void RegisterDamage(int attackerOwner, float damage)
        {
            if (this.state != AxeDefensiveState.Active) return;
            this.state = AxeDefensiveState.Dead;

            // Unfreeze the player and restore movement speed.
            if (this.wc != null)
            {
                this.wc.rewindCount--;
                if (this._speedReduced) { this.wc.MOVEMENT_SPEED /= 0.4f; this._speedReduced = false; }
            }

            // Remove from registry and mark knockback immunity window.
            if (activeDefensives.ContainsKey(this.id.owner) && activeDefensives[this.id.owner] == this)
                activeDefensives.Remove(this.id.owner);
            recentlyParriedUntil[this.id.owner] = Time.time + 0.5f;

            if (Globals.online && base.photonView != null)
            {
                int viewId = -1;
                WizardController atk = GameUtility.GetWizard(attackerOwner);
                if (atk != null)
                {
                    PhotonView pv = atk.gameObject.GetPhotonView();
                    viewId = (pv != null) ? pv.viewID : -1;
                }
                base.photonView.RPCLocal(this, "rpcCounterAttack", PhotonTargets.All,
                    new object[] { attackerOwner, viewId });
            }
            else
            {
                WizardController atk = GameUtility.GetWizard(attackerOwner);
                this.localCounterAttack(attackerOwner, atk != null ? atk.gameObject : null);
            }
        }

        private void EndWithoutTrigger()
        {
            if (this.state != AxeDefensiveState.Active) return;
            this.state = AxeDefensiveState.Dead;

            if (this.wc != null)
            {
                this.wc.rewindCount--;
                if (this._speedReduced) { this.wc.MOVEMENT_SPEED /= 0.4f; this._speedReduced = false; }
            }

            if (activeDefensives.ContainsKey(this.id.owner) && activeDefensives[this.id.owner] == this)
                activeDefensives.Remove(this.id.owner);

            if (this.trail != null)
                this.trail.Emit = false;

            UnityEngine.Object.Destroy(base.gameObject, 0.5f);
        }

        public override void SpellObjectDeath()
        {
            this.EndWithoutTrigger();
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            UnityEngine.Object.Destroy(base.gameObject, 0.5f);
        }

        [PunRPC]
        public void rpcSpellObjectStart(int owner, int viewId)
        {
            PhotonView pv = PhotonView.Find(viewId);
            GameObject go = (pv != null) ? pv.gameObject : null;
            this.localSpellObjectStart(owner, go);
        }

        public void localSpellObjectStart(int owner, GameObject go)
        {
            if (go == null) return;
            this.id.owner = owner;
            this.wc = go.GetComponent<WizardController>();
            this.nw = go.GetComponent<NetworkedWizard>();

            this.state = AxeDefensiveState.Active;
            this.stateTimer = Time.time + this.parryWindow;

            // Register so damage notifications reach us.
            activeDefensives[owner] = this;

            // Freeze the player (blocks casting); also reduce movement speed to 40%.
            if (this.wc != null)
            {
                this.wc.rewindCount++;
                this._speedReduced = true;
                this.wc.MOVEMENT_SPEED *= 0.4f;
                this.wc.ResetMove();
            }

            // Visual effects.
            if (this.effectStart != null)
                this.effectStart.Play(true);
            if (this.trail != null)
                this.trail.Emit = true;
            if (this.distortionTrail != null)
                this.distortionTrail.Play(true);

            // Sound.
            if (this.sp != null)
                this.sp.PlaySound("event:/sfx/metal/double-strike-cast").SetPitch(1f);

            // Hint the AI not to attack the caster during the window.
            AxePhotonExtensions.AiEventHandler.DoNotAttackWizardFor(go, this.parryWindow + 0.1f, 0);
        }

        [PunRPC]
        public void rpcCounterAttack(int attackerOwner, int viewId)
        {
            GameObject go = null;
            if (viewId != -1)
            {
                PhotonView pv = PhotonView.Find(viewId);
                go = (pv != null) ? pv.gameObject : null;
            }
            this.localCounterAttack(attackerOwner, go);
        }

        private void localCounterAttack(int attackerOwner, GameObject attackerGo)
        {
            if (attackerGo == null)
            {
                if (this.trail != null) this.trail.Emit = false;
                UnityEngine.Object.Destroy(base.gameObject, 0.5f);
                return;
            }

            Vector3 dest = attackerGo.transform.position;

            // Teleport the caster to the attacker.
            if (this.nw != null)
            {
                Vector3 dir = (dest - base.transform.position).WithY(0f);
                if (dir != Vector3.zero)
                    this.nw.SetCorrectRotation(Quaternion.LookRotation(dir, Vector3.up));
                this.nw.SetCorrectPosition(dest);
            }
            if (this.wc != null)
                this.wc.transform.position = dest;
            base.transform.position = dest;

            AxePhotonExtensions.AiEventHandler.OnTeleport(this.id.owner);

            // Impact visual.
            if (this.impact != null)
                UnityEngine.Object.Instantiate(this.impact, dest, Quaternion.Euler(270f, 0f, 0f));

            // Impact sound.
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/double-strike-impact", 5f);

            // Deal damage (authority only).
            if (base.photonView.IsMine())
            {
                UnitStatus us = attackerGo.GetComponent<UnitStatus>();
                if (us != null)
                    us.ApplyDamage(COUNTER_DAMAGE, this.id.owner, 62);
            }

            if (this.trail != null)
                this.trail.Emit = false;

            UnityEngine.Object.Destroy(base.gameObject, 0.5f);
        }

        private void OnDestroy()
        {
            // Safety: ensure player is never left frozen or slowed.
            if (this.state == AxeDefensiveState.Active && this.wc != null)
                this.wc.rewindCount--;
            if (this._speedReduced && this.wc != null)
            {
                this.wc.MOVEMENT_SPEED /= 0.4f;
                this._speedReduced = false;
            }

            if (activeDefensives.ContainsKey(this.id.owner) && activeDefensives[this.id.owner] == this)
                activeDefensives.Remove(this.id.owner);
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
        }

        private enum AxeDefensiveState
        {
            Active,
            Dead
        }
    }
}
