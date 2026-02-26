using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using FMOD.Studio;
using PigeonCoopToolkit.Effects.Trails;
using UnityEngine;

namespace AxeElement
{
    public class WhirlwindObject : global::Photon.MonoBehaviour
    {
        public float DAMAGE = 5f;
        protected float RADIUS = 5f;
        protected float POWER = 30f;
        protected float START_TIME = 3f;

        public float deathTimer;
        protected Identity id = new Identity();
        protected SoundPlayer sp;

        // Inspector-assigned from Double Strike prefab
        public UnityEngine.Object impact;
        public ParticleSystem distortionTrail;
        public ParticleSystem distortion;
        public UnityEngine.Object effect;
        public ParticleSystem effectStart;
        public SmokeTrail trail;

        private WizardController wc;
        private Animator anim;
        private bool curved;
        private Spell meleeSpell;
        private EventInstance aSource;
        private float stateTimer;
        private WhirlwindState state;
        private Vector3 startPos;
        private List<UnitStatus> targets = new List<UnitStatus>();
        private List<float> damages = new List<float>();
        private List<Transform> effects = new List<Transform>();
        private int currentTarget;
        private ParticleSystem.EmissionModule emit;
        private bool isLocal = true;

        private static Dictionary<int, List<WhirlwindObject>> activeWhirlwinds =
            new Dictionary<int, List<WhirlwindObject>>();

        public static void NotifyDamage(int owner, float damage, UnitStatus unit)
        {
            if (!activeWhirlwinds.ContainsKey(owner)) return;
            foreach (WhirlwindObject wo in activeWhirlwinds[owner].ToList())
            {
                if (wo == null)
                    activeWhirlwinds[owner].Remove(wo);
                else
                    wo.RegisterWWDamage(damage, unit);
            }
        }

        private static void RegisterWhirlwind(int owner, WhirlwindObject wo)
        {
            if (!activeWhirlwinds.ContainsKey(owner))
                activeWhirlwinds[owner] = new List<WhirlwindObject>();
            if (!activeWhirlwinds[owner].Contains(wo))
                activeWhirlwinds[owner].Add(wo);
        }

        private static void UnRegisterWhirlwind(int owner, WhirlwindObject wo)
        {
            if (!activeWhirlwinds.ContainsKey(owner)) return;
            if (activeWhirlwinds[owner].Contains(wo))
                activeWhirlwinds[owner].Remove(wo);
        }

        private void Awake()
        {
            this.sp = base.GetComponent<SoundPlayer>();
        }

        private void Start()
        {
            this.deathTimer = Time.time + this.START_TIME;
        }

        public void Init(Identity identity, float curve)
        {
            this.id.owner = identity.owner;
            if (curve < -0.3f || curve > 0.3f)
                this.curved = true;
            if (Globals.online)
            {
                if (base.photonView != null && base.photonView.isMine)
                {
                    base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.All, new object[]
                    {
                        identity.owner,
                        identity.gameObject.GetPhotonView().viewID,
                        this.curved
                    });
                }
                else
                {
                    this.isLocal = false;
                }
            }
            else
            {
                this.localSpellObjectStart(identity.owner, identity.gameObject, this.curved);
            }
            base.Invoke("SpellObjectDeath", 5.5f);
        }

        private void Update()
        {
            if (this.wc != null)
                base.transform.position = this.wc.transform.position;
            else if (this.state != WhirlwindState.Init && this.state != WhirlwindState.Death)
                this.rpcSpellObjectDeath();

            for (int i = 0; i < this.effects.Count; i++)
            {
                UnitStatus us = this.targets[i];
                Transform t = this.effects[i];
                if (us != null && t != null)
                    t.position = us.transform.position;
            }

            switch (this.state)
            {
                case WhirlwindState.Init:
                case WhirlwindState.Death:
                    break;
                case WhirlwindState.Cast:
                    if (this.stateTimer < Time.time)
                    {
                        this.state = WhirlwindState.Wait;
                        this.stateTimer = Time.time + 5f;
                    }
                    break;
                case WhirlwindState.Wait:
                    if (this.stateTimer < Time.time && this.wc.petrifyCount <= 0 && this.wc.rewindCount <= 0)
                    {
                        UnRegisterWhirlwind(this.id.owner, this);
                        if (this.anim != null) this.anim.Play("Katana Starting");
                        if (this.effectStart != null) this.effectStart.Play(true);
                        if (this.trail != null)
                        {
                            this.trail.Emit = true;
                            this.trail.transform.DOShakePosition(0.1f, 1f, 10, 90f, false, true).SetDelay(0.9f);
                        }
                        if (this.wc != null)
                        {
                            this.wc.rewindCount++;
                            this.wc.ResetMove();
                        }
                        this.state = WhirlwindState.WindUp;
                        this.stateTimer = Time.time + 1f;
                    }
                    break;
                case WhirlwindState.WindUp:
                    if (this.stateTimer < Time.time)
                    {
                        this.state = WhirlwindState.Attack;
                        if (this.wc != null)
                            CrystalObject.GetOutOfPreserveCrystals(this.id.owner, this.wc.transform);
                        if (this.curved)
                            this.startPos = this.wc.transform.position;
                        if (this.targets.Count > 0)
                            Globals.camera_contain.Shake(3f);
                    }
                    break;
                case WhirlwindState.Attack:
                    if (this.stateTimer < Time.time)
                    {
                        if (this.currentTarget >= this.targets.Count)
                        {
                            this.WindDown();
                            return;
                        }
                        UnitStatus unitStatus = null;
                        float dmg = 0f;
                        while (unitStatus == null)
                        {
                            unitStatus = this.targets[this.currentTarget];
                            dmg = this.damages[this.currentTarget];
                            Transform fx = this.effects[this.currentTarget];
                            if (fx != null)
                            {
                                fx.DOScale(0f, 0.3f);
                                UnityEngine.Object.Destroy(fx.gameObject, 0.4f);
                            }
                            this.currentTarget++;
                            if (this.currentTarget >= this.targets.Count) break;
                        }
                        if (unitStatus != null)
                        {
                            Vector3 dest = unitStatus.transform.position;
                            if (this.anim != null) this.anim.Play("Katana Drawn");
                            NetworkedWizard nw = this.wc.GetComponent<NetworkedWizard>();
                            nw.SetCorrectRotation(Quaternion.LookRotation((dest - base.transform.position).WithY(0f), Vector3.up));
                            nw.SetCorrectPosition(dest);
                            base.transform.position = dest;
                            AxePhotonExtensions.AiEventHandler.OnTeleport(this.id.owner);
                            if (this.impact != null)
                                UnityEngine.Object.Instantiate(this.impact, dest, Quaternion.Euler(new Vector3(270f, 0f, 0f)));
                            if (this.sp != null)
                                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/double-strike-impact", 5f);
                            if (base.photonView.IsMine())
                            {
                                unitStatus.ApplyDamage(dmg, this.id.owner, 62);
                                if (this.meleeSpell != null && this.meleeSpell.spellName != SpellName.Geyser)
                                    dest -= base.transform.forward * 4f;
                                if (this.meleeSpell != null)
                                    this.meleeSpell.Initialize(this.id, dest, base.transform.rotation, 0f, -1, false, (SpellName)152);
                            }
                            this.wc.GetComponent<PhysicsBody>().rpcSetVelocity(Vector3.zero);
                            this.stateTimer = Time.time + 0.1f;
                        }
                        if (this.currentTarget >= this.targets.Count)
                        {
                            this.WindDown();
                            return;
                        }
                    }
                    break;
                case WhirlwindState.Return:
                    if (this.stateTimer < Time.time)
                        this.WindDown();
                    break;
                case WhirlwindState.WindDown:
                    if (this.stateTimer < Time.time)
                    {
                        if (this.wc != null && this.wc.katana != null)
                            this.wc.katana.SetActive(false);
                        if (this.wc != null)
                            this.wc.rewindCount--;
                        this.state = WhirlwindState.Death;
                        this.rpcSpellObjectDeath();
                    }
                    break;
            }
        }

        private void WindDown()
        {
            if (this.curved)
            {
                this.state = WhirlwindState.Return;
                this.stateTimer = Time.time + 0.1f;
                this.curved = false;
                if (this.wc != null)
                {
                    NetworkedWizard nw = this.wc.GetComponent<NetworkedWizard>();
                    nw.SetCorrectRotation(Quaternion.LookRotation((this.startPos - base.transform.position).WithY(0f), Vector3.up));
                    nw.SetCorrectPosition(this.startPos);
                }
                AxePhotonExtensions.AiEventHandler.OnTeleport(this.id.owner);
                return;
            }
            if (this.trail != null)
            {
                this.trail.transform.DOShakePosition(0.01f, 1f, 10, 90f, false, true).OnComplete(delegate
                {
                    this.trail.Emit = false;
                    this.emit.enabled = false;
                });
            }
            this.state = WhirlwindState.WindDown;
            this.stateTimer = Time.time + 1.1f;
            if (this.anim != null)
            {
                if (this.targets == null || this.targets.Count == 0)
                {
                    this.anim.Play("Katana Fail");
                    if (this.sp != null)
                        this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/double-strike-fail", 5f);
                }
                else
                {
                    this.anim.Play("Katana End");
                    if (this.sp != null)
                        this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/double-strike-winddown", 5f);
                }
            }
        }

        private void OnDestroy()
        {
            UnRegisterWhirlwind(this.id.owner, this);
        }

        public void SpellObjectDeath()
        {
            UnRegisterWhirlwind(this.id.owner, this);
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        private void RegisterWWDamage(float damage, UnitStatus unit)
        {
            if (Globals.online)
            {
                PhotonPlayer ownerOrMaster = base.photonView.GetOwnerOrMaster();
                int playerId = (ownerOrMaster != null) ? ownerOrMaster.ID : 0;
                int viewId = -1;
                if (unit != null && unit.gameObject != null)
                {
                    PhotonView pv = unit.gameObject.GetPhotonView();
                    viewId = (pv != null) ? pv.viewID : -1;
                }
                base.photonView.RPCLocal(this, "rpcRegisterDamage", PhotonPlayer.Find(playerId),
                    new object[] { damage, viewId });
                return;
            }
            this.localRegisterDamage(damage, unit, -1);
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.state = WhirlwindState.Death;
            if (this.effects != null)
                foreach (Transform t in this.effects)
                    if (t != null)
                    {
                        t.DOScale(0f, 0.3f);
                        UnityEngine.Object.Destroy(t.gameObject, 0.33f);
                    }
            UnityEngine.Object.Destroy(base.gameObject, 2f);
        }

        [PunRPC]
        public void rpcSpellObjectStart(int owner, int viewId, bool curved)
        {
            GameObject go = PhotonView.Find(viewId)?.gameObject;
            this.localSpellObjectStart(owner, go, curved);
        }

        public void localSpellObjectStart(int owner, GameObject go, bool curved)
        {
            this.curved = curved;
            this.id.owner = owner;
            this.wc = go.GetComponent<WizardController>();
            this.anim = go.GetComponent<Animator>();
            this.stateTimer = Time.time + 1.3f;
            this.state = WhirlwindState.Cast;
            if (this.sp != null)
                this.aSource = this.sp.PlaySound("event:/sfx/metal/double-strike-cast").SetPitch(1f);
            RegisterWhirlwind(owner, this);
            SpellName meleeName = PlayerManager.players[owner].spell_library[SpellButton.Melee];
            this.meleeSpell = Globals.spell_manager.spell_table[meleeName];
            if (this.wc != null && this.wc.katana != null)
            {
                this.wc.katana.SetActive(true);
                ParticleSystem.EmissionModule em = this.wc.katana.transform
                    .GetChild(0).GetChild(0).GetComponent<ParticleSystem>().emission;
                em.enabled = true;
                this.emit = em;
            }
        }

        [PunRPC]
        public void rpcRegisterDamage(float damage, int viewId)
        {
            if (viewId == -1) return;
            PhotonView pv = PhotonView.Find(viewId);
            if (pv != null)
            {
                GameObject go = pv.gameObject;
                UnitStatus us = (go != null) ? go.GetComponent<UnitStatus>() : null;
                this.localRegisterDamage(damage, us, viewId);
            }
        }

        private void localRegisterDamage(float damage, UnitStatus unit, int viewId)
        {
            if (Globals.online && base.photonView.IsMine())
            {
                base.photonView.RPCLocal(this, "rpcRegisterDamage", PhotonTargets.Others,
                    new object[] { damage, viewId });
            }
            if (!this.targets.Contains(unit))
            {
                this.targets.Add(unit);
                this.damages.Add(damage);
                if (unit != null && this.wc != null)
                    GameUtility.RegisterDamageOverTime(
                        this.stateTimer - Time.time,
                        this.wc.transform,
                        unit.transform,
                        this.stateTimer - Time.time - 0.01f);
                if (this.effect != null)
                {
                    Transform fxT = (UnityEngine.Object.Instantiate(this.effect, base.transform.position, Quaternion.identity) as GameObject).transform;
                    fxT.DORotate(new Vector3(0f, 360f, 0f), 1f, RotateMode.Fast)
                        .SetRelative<TweenerCore<Quaternion, Vector3, QuaternionOptions>>()
                        .SetLoops(-1).SetEase(Ease.Linear);
                    GameUtility.SetWizardColor(this.id.owner, fxT.gameObject, false);
                    this.effects.Add(fxT);
                    fxT.localScale = Vector3.zero;
                    this.UpdateEffectScale(fxT, damage);
                }
                else
                {
                    this.effects.Add(null);
                }
                if (this.sp != null)
                    this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/double-strike-ignite", 5f);
            }
            else
            {
                int idx = this.targets.IndexOf(unit);
                this.damages[idx] += damage;
                if (this.effects[idx] != null)
                    this.UpdateEffectScale(this.effects[idx], this.damages[idx]);
            }
        }

        private void UpdateEffectScale(Transform e, float damage)
        {
            e.DOScale(1f + damage * 0.03f, 0.2f);
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
        }

        private enum WhirlwindState
        {
            Init,
            Cast,
            Wait,
            WindUp,
            Attack,
            Return,
            WindDown,
            Death
        }
    }
}
