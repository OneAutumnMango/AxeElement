using System;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace AxeElement
{
    public class IronWardObject : SpellObject
    {
        public IronWardObject()
        {
            DAMAGE = 3f;
            RADIUS = 3f;
            POWER = 2.5f;
            START_TIME = 2f;
        }

        // Inspector-assigned from Chainmail prefab
        public Transform child;
        public Transform[] vineTransforms;
        public Transform attach;
        public AnimationCurve shieldCurve;
        public ParticleSystem sparks;

        private const float INNER_LENGTH_SQ = 56.25f;
        private const float OUTER_LENGTH_SQ = 156.25f;
        private const float CHAIN_POWER_MAX = 2.5f;
        private const float CHAIN_POWER_M = 0.5f;

        public int blockedOwner = -1;
        public Transform blockedTransform;
        public PhysicsBody blockedUnit;
        private PhotonView blockedView;
        private Transform shield;
        private WizardStatus wizard;
        private IronWardState state;
        private float stateTimer;
        private bool swap;
        private ParticleSystem.MainModule sparksMain;
        private float sparkTimer;

        // Static registry: player owner -> IronWard objects protecting them
        public static Dictionary<int, List<IronWardObject>> activeIronWards =
            new Dictionary<int, List<IronWardObject>>();

        public static void NotifyDamage(int attackerOwner, float damage, WizardStatus targetWizard)
        {
            if (targetWizard == null) return;
            Identity ident = targetWizard.GetComponent<Identity>();
            if (ident == null) return;
            int wizardOwner = ident.owner;
            if (!activeIronWards.ContainsKey(wizardOwner)) return;
            foreach (IronWardObject ward in activeIronWards[wizardOwner])
            {
                if (ward != null)
                    ward.RegisterDamage(damage, attackerOwner);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (this.id == null)
                this.id = new Identity();
            if (this.sparks != null)
                this.sparksMain = this.sparks.main;
        }

        private void Start()
        {
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/chainmail-cast", 5f);
            if (this.vineTransforms != null)
                for (int i = 0; i < this.vineTransforms.Length; i++)
                    if (this.vineTransforms[i] != null)
                        this.vineTransforms[i].localScale = Vector3.zero;
            this.deathTimer = Time.time + this.START_TIME;
        }

        public void Init(Identity identity)
        {
            this.wizard = identity.GetComponent<WizardStatus>();
            this.id.owner = identity.owner;
            // Register this ward for the caster
            if (!activeIronWards.ContainsKey(this.id.owner))
                activeIronWards[this.id.owner] = new List<IronWardObject>();
            activeIronWards[this.id.owner].Add(this);

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
            if (this.wizard != null)
                base.transform.position = this.wizard.transform.position;
            else if (this.state != IronWardState.End)
            {
                this.rpcSpellObjectDeath();
                return;
            }

            if (this.child != null && this.state == IronWardState.Catch)
                this.child.rotation *= Quaternion.Euler(0f, 300f * Time.deltaTime, 0f);

            if (this.blockedUnit != null)
                this.PositionLine();

            switch (this.state)
            {
                case IronWardState.Catch:
                    if (this.stateTimer != 0f && this.stateTimer < Time.time)
                        this.rpcSpellObjectDeath();
                    break;
                case IronWardState.Flying:
                    if (this.blockedTransform != null && this.shield != null)
                    {
                        float t = (this.stateTimer - Time.time) / 0.3f;
                        float curveVal = (this.shieldCurve != null) ? this.shieldCurve.Evaluate(t) : t;
                        this.shield.position = Vector3.Lerp(
                            this.blockedTransform.position + Vector3.up,
                            base.transform.position,
                            curveVal);
                        this.shield.localRotation *= Quaternion.Euler(0f, 0f, 2700f * Time.deltaTime);
                        if (this.child != null)
                            this.child.rotation = Quaternion.RotateTowards(
                                this.child.rotation,
                                Quaternion.LookRotation(this.blockedTransform.position - base.transform.position, Vector3.up),
                                1200f * Time.deltaTime);
                    }
                    if (this.stateTimer < Time.time)
                    {
                        this.state = IronWardState.Block;
                        this.stateTimer = Time.time + 4.7f;
                        if (this.shield != null) this.shield.DOScale(0f, 0.3f);
                        if (this.blockedUnit != null)
                        {
                            if (base.photonView.IsMine())
                            {
                                UnitStatus us = this.blockedUnit.GetComponent<UnitStatus>();
                                if (us != null) us.ApplyDamage(this.DAMAGE, this.id.owner, 60);
                            }
                            if (this.blockedView != null && this.blockedView.IsMine())
                                this.blockedUnit.AddForce(GameUtility.GetForceVector(base.transform.position, this.blockedTransform.position, 20f));
                        }
                    }
                    break;
                case IronWardState.Block:
                    if (this.blockedTransform != null && this.child != null)
                        this.child.rotation = Quaternion.RotateTowards(
                            this.child.rotation,
                            Quaternion.LookRotation(this.blockedTransform.position - base.transform.position, Vector3.up),
                            600f * Time.deltaTime);
                    if (this.stateTimer < Time.time)
                        this.rpcSpellObjectDeath();
                    break;
                case IronWardState.End:
                    return;
            }
        }

        private float GetForce(float distanceSQ)
        {
            if (distanceSQ <= INNER_LENGTH_SQ) return 0f;
            if (distanceSQ > OUTER_LENGTH_SQ) return CHAIN_POWER_MAX;
            return CHAIN_POWER_M * (Mathf.Sqrt(distanceSQ) - 7.5f);
        }

        private void PositionLine()
        {
            if (this.vineTransforms == null || this.vineTransforms.Length == 0 || this.attach == null) return;
            if (this.vineTransforms[0] != null)
            {
                this.vineTransforms[0].position = this.attach.position;
                this.vineTransforms[0].rotation = this.attach.rotation;
            }
            Transform last = this.vineTransforms[this.vineTransforms.Length - 1];
            if (last != null)
            {
                if (this.state == IronWardState.Flying && this.shield != null)
                    last.position = this.shield.position;
                else if (this.blockedTransform != null)
                    last.position = this.blockedTransform.position + Vector3.up;
            }
        }

        private void FixedUpdate()
        {
            if (this.blockedUnit != null && this.blockedView != null && this.blockedView.IsMine() &&
                this.state == IronWardState.Block)
            {
                float force = this.GetForce((base.transform.position - this.blockedTransform.position).sqrMagnitude);
                if (force > 0f)
                    this.blockedUnit.AddForce(GameUtility.GetForceVector(this.blockedTransform.position, base.transform.position, force));
            }
        }

        public override void SpellObjectDeath()
        {
            this.state = IronWardState.End;
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        public void RegisterDamage(float damage, int attackerOwner)
        {
            GameObject attackerGo = null;
            bool triggerShield = false;

            if (this.blockedOwner == -1)
            {
                if (attackerOwner == 0 &&
                    (Globals.current_stage == StageName.Islands || Globals.current_stage == StageName.Bridges))
                {
                    SharkController shark = Globals.shark;
                    attackerGo = (shark != null) ? shark.gameObject : null;
                    this.blockedOwner = 0;
                    triggerShield = true;
                }
                else if (attackerOwner > 0)
                {
                    WizardController wc = GameUtility.GetWizard(attackerOwner);
                    attackerGo = (wc != null) ? wc.gameObject : null;
                    this.blockedOwner = attackerOwner;
                    triggerShield = true;
                }
            }

            if (this.blockedOwner >= 0)
            {
                if (Globals.online)
                {
                    int viewId = -1;
                    if (attackerGo != null)
                    {
                        PhotonView pv = attackerGo.GetPhotonView();
                        viewId = (pv != null) ? pv.viewID : -1;
                    }
                    base.photonView.RPCLocal(this, "rpcRegisterDamage", PhotonTargets.All,
                        new object[] { damage, this.blockedOwner, viewId, triggerShield });
                }
                else
                {
                    this.localRegisterDamage(damage, this.blockedOwner, attackerGo, triggerShield);
                }
            }
        }

        private void OnDestroy()
        {
            if (base.photonView.IsMine() && this.wizard != null)
            {
                int owner = this.id.owner;
                if (activeIronWards.ContainsKey(owner))
                    activeIronWards[owner].Remove(this);
            }
            if (this.vineTransforms != null)
                for (int i = 0; i < this.vineTransforms.Length; i++)
                {
                    Transform t = this.vineTransforms[i];
                    if (t != null && t.gameObject != null)
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            if (this.shield != null)
                UnityEngine.Object.Destroy(this.shield.gameObject);
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.blockedOwner = -2;
            if (this.child != null)
                this.child.DOScale(0f, 0.3f).SetEase(Ease.InOutCubic);
            this.state = IronWardState.End;
            this.blockedUnit = null;
            this.blockedTransform = null;
            this.blockedView = null;
            if (this.wizard != null)
            {
                int owner = this.id.owner;
                if (activeIronWards.ContainsKey(owner))
                    activeIronWards[owner].Remove(this);
            }
            if (this.vineTransforms != null)
                for (int i = 0; i < this.vineTransforms.Length; i++)
                    if (this.vineTransforms[i] != null)
                        this.vineTransforms[i].DOScale(0f, 0.3f);
            UnityEngine.Object.Destroy(base.gameObject, 1f);
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
            WizardStatus ws = go.GetComponent<WizardStatus>();
            this.wizard = ws;
            this.stateTimer = Time.time + 2f;
            if (this.child != null)
                this.child.DOScale(0f, 0.2f).From<TweenerCore<Vector3, Vector3, DG.Tweening.Plugins.Options.VectorOptions>>().SetEase(Ease.InOutCubic);
            if (this.child != null)
                GameUtility.SetWizardColor(owner, this.child.gameObject, false);
            AxePhotonExtensions.AiEventHandler.DoNotAttackWizardFor(go, 2f, 0);
        }

        [PunRPC]
        public void rpcRegisterDamage(float damage, int attackerOwner, int viewId, bool triggerShield)
        {
            GameObject go = null;
            if (viewId != -1)
            {
                PhotonView pv = PhotonView.Find(viewId);
                go = (pv != null) ? pv.gameObject : null;
            }
            this.localRegisterDamage(damage, attackerOwner, go, triggerShield);
        }

        private void localRegisterDamage(float damage, int attackerOwner, GameObject target, bool triggerShield)
        {
            if (this.wizard == null) return;
            if (triggerShield)
            {
                this.state = IronWardState.Flying;
                this.stateTimer = Time.time + 0.3f;
                if (this.child != null)
                    GameUtility.SetWizardColor(attackerOwner, this.child.gameObject, false);
                if (target != null)
                {
                    this.blockedTransform = target.transform;
                    this.blockedUnit = target.GetComponent<PhysicsBody>();
                    if (Globals.online)
                        this.blockedView = target.GetPhotonView();
                    if (this.vineTransforms != null)
                        foreach (Transform t in this.vineTransforms)
                        {
                            if (t == null) continue;
                            t.parent = null;
                            t.DOScale(5f, 0.15f);
                        }
                    if (this.child != null && this.child.childCount > 0)
                    {
                        this.shield = this.child.GetChild(0);
                        this.shield.parent = null;
                        this.shield.DOScale(4.719017f, 0.1f);
                    }
                }
                else
                {
                    if (this.child != null && this.child.childCount > 0)
                    {
                        this.shield = this.child.GetChild(0);
                        this.shield.parent = null;
                        this.shield.DOScale(0f, 0.1f);
                    }
                }
                this.blockedOwner = attackerOwner;
                if (this.sp != null)
                    this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/chainmail-block", 5f).SetPitch(1f);
                AiEventHandler aeh = AxePhotonExtensions.AiEventHandler;
                WizardStatus ws = this.wizard;
                aeh.DoNotAttackWizardFor((ws != null) ? ws.gameObject : null, 5f, this.blockedOwner);
            }
            if (damage > 0f && this.sparkTimer < Time.time)
            {
                this.sparkTimer = Time.time + 0.1f;
                this.sparksMain.startSpeed = 2.5f * damage + 10f;
                if (this.sparks != null) this.sparks.Play();
                if (this.sp != null && target == null)
                {
                    float vol = damage / 45f + 0.4f;
                    this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/chainmail-block-again", 5f).SetVolume(vol);
                }
            }
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
        }

        private enum IronWardState
        {
            Catch,
            Flying,
            Block,
            End
        }
    }
}
