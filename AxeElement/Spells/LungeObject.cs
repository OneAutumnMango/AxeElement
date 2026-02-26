using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace AxeElement
{
    public class LungeObject : SpellObject
    {
        public int spellIndex;

        private PhysicsBody wizardPhys;
        private PhysicsBody phys;
        private float accel = 1.1f;
        private float decel = 0.85f;
        private bool hit;
        private bool dying;
        private WizardController wizard;
        private SpellName spellName;
        private float landTimer;
        private float unitLandTimer;
        private int gravityResets;
        private Transform target;
        private PhysicsBody enemyPb;
        private Transform ankle;
        private bool isAnkle;
        private bool tossed;
        private float tossMagnitude;
        private PhotonView targetPv;
        private Dictionary<int, AiEventHandler.Avoid> avoids = new Dictionary<int, AiEventHandler.Avoid>();
        private Vector3 lastPos;
        public LungeObject.LungeState state;

        // Inspector-assigned fields from the steal trap prefab
        public Transform attach;
        public Transform[] vineTransforms;
        public Transform wizardParticles;
        public Transform targetEffects;
        public Animator anim;
        public float multiplier = 2f;
        public ParticleSystem dustTrail;
        public ParticleSystem trapClose;
        public ParticleSystem loveSparkles;
        public ParticleSystem gettinSlammed;
        public ParticleSystem slam;

        public static Dictionary<GameObject, Dictionary<SpellName, LungeObject>> lungeTraps =
            new Dictionary<GameObject, Dictionary<SpellName, LungeObject>>();

        private const float INNER_LENGTH_SQ = 56.25f;
        private const float OUTER_LENGTH_SQ = 156.25f;
        private const float CHAIN_POWER_MAX = 2.5f;
        private const float CHAIN_POWER_M = 0.5f;
        private const float SLAM_DAMAGE = 8f;

        public LungeObject()
        {
            DAMAGE = 5f;
            RADIUS = 4f;
            POWER = 20f;
            Y_POWER = 0f;
            START_TIME = 4f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (id == null) id = new Identity();
        }

        private void Start()
        {
            this.deathTimer = Time.time + this.START_TIME;
            this.lastPos = base.transform.position;
            Transform[] array = this.vineTransforms;
            if (array != null)
                for (int i = 0; i < array.Length; i++)
                    array[i].parent = null;
            if (this.wizardParticles != null)
                this.wizardParticles.parent = null;
            if (this.targetEffects != null)
                this.targetEffects.parent = null;
            Globals.camera_contain.AddCameraTransform(base.transform);
        }

        public void Init(Identity identity, float curve, float velocity, int spellIndex, SpellName spellNameForCooldown)
        {
            this.spellName = spellNameForCooldown;
            GameObject gameObject = (identity != null) ? identity.gameObject : null;

            if (spellIndex != -1)
            {
                if (LungeObject.lungeTraps.ContainsKey(gameObject) &&
                    LungeObject.lungeTraps[gameObject] != null &&
                    LungeObject.lungeTraps[gameObject].ContainsKey(this.spellName) &&
                    LungeObject.lungeTraps[gameObject][this.spellName] != null)
                {
                    if (curve > 0.2f || curve < -0.2f)
                        LungeObject.lungeTraps[gameObject][this.spellName].Detach();
                    else
                        LungeObject.lungeTraps[gameObject][this.spellName].Pull();
                }
                Renderer[] renderers = base.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].enabled = false;
                if (base.photonView != null && base.photonView.isMine)
                {
                    base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.Others, new object[] { true });
                }
                return;
            }

            if (!LungeObject.lungeTraps.ContainsKey(gameObject) || LungeObject.lungeTraps[gameObject] == null)
                LungeObject.lungeTraps[gameObject] = new Dictionary<SpellName, LungeObject>();
            LungeObject.lungeTraps[gameObject][this.spellName] = this;

            this.wizard = identity.GetComponent<WizardController>();
            if (this.wizard != null)
                this.wizardPhys = this.wizard.GetComponent<PhysicsBody>();
            this.phys = base.GetComponent<PhysicsBody>();
            this.landTimer = Time.time + 0.2f;

            if (this.wizardPhys != null)
            {
                this.wizardPhys.abilityVelocity = base.transform.forward * velocity;
                this.wizardPhys.velocity += Vector3.up * 1.2f;
                this.wizard.ModifyGravity(this.phys.gravity);
                this.gravityResets++;
            }

            this.id.owner = identity.owner;
            CrystalObject.GetOutOfPreserveCrystals(this.id.owner, this.wizard.transform);
            GameUtility.SetWizardColor(this.id.owner, base.gameObject, false);
            this.curve = curve;
            this.velocity = velocity;
            this.spellIndex = spellIndex;
            this.SetLayerImmediate(velocity);
            this.RegisterAvoids();

            if (this.sp != null)
                this.sp.PlaySoundInstantiate("event:/sfx/metal/steal-trap-set", 5f);

            if (base.photonView != null && base.photonView.isMine)
            {
                object[] array = new object[7];
                array[0] = this.id.owner;
                array[1] = base.transform.position;
                array[2] = base.transform.rotation;
                array[3] = curve;
                array[4] = velocity;
                array[5] = spellIndex;
                int? num2 = null;
                if (identity != null && identity.gameObject != null)
                {
                    PhotonView pv = identity.gameObject.GetPhotonView();
                    num2 = (pv != null) ? new int?(pv.viewID) : null;
                }
                array[6] = num2 ?? (-1);
                base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.Others, array);
            }
        }

        private void SetLayerImmediate(float vel)
        {
            base.gameObject.layer = 0;
        }

        public void Pull()
        {
            if (base.photonView.IsMine())
                base.photonView.RPCLocal(this, "rpcPull", PhotonTargets.All, Array.Empty<object>());
        }

        public void Detach()
        {
            if (base.photonView.IsMine())
                base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, new object[] { false });
        }

        private void OnDestroy()
        {
            if (this.wizardPhys != null && this.spellIndex == -1)
            {
                this.ResetGravityAndAbilityVelocity();
                this.ResetGravityAndAbilityVelocity();
            }
            if (this.wizardParticles != null)
                UnityEngine.Object.Destroy(this.wizardParticles.gameObject);
            if (this.targetEffects != null)
                UnityEngine.Object.Destroy(this.targetEffects.gameObject);
            Globals.camera_contain.RemoveCameraTransform(base.transform);
            if (this.vineTransforms != null)
                for (int i = 0; i < this.vineTransforms.Length; i++)
                {
                    Transform t = this.vineTransforms[i];
                    if (t != null && t.gameObject != null)
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
        }

        private void ResetGravityAndAbilityVelocity()
        {
            if (this.gravityResets > 0)
            {
                this.gravityResets--;
                if (this.wizard != null) this.wizard.UnModifyGravity();
                if (this.wizardPhys != null) this.wizardPhys.abilityVelocity = Vector3.zero;
            }
        }

        private void Update()
        {
            if (this.state == LungeState.AttachedToUnit && (base.transform.position - this.lastPos).sqrMagnitude > 0.001f)
            {
                base.transform.rotation = Quaternion.RotateTowards(base.transform.rotation,
                    Quaternion.LookRotation((base.transform.position - this.lastPos).WithY(0f), Vector3.up), 5f);
                this.lastPos = base.transform.position;
            }

            if (this.wizard != null)
            {
                if (this.state != LungeState.AttachedToUnit && this.state != LungeState.End && this.ankle != null)
                {
                    base.transform.position = this.isAnkle ? this.ankle.position : (this.ankle.position + Vector3.up);
                    base.transform.rotation = this.ankle.rotation;
                }
                this.PositionLine();
            }

            if (this.state == LungeState.ThrowingUnit && this.target != null && this.targetPv.IsMine())
            {
                if (!this.tossed && !this.enemyPb.onGround)
                {
                    this.tossed = true;
                    this.unitLandTimer = Time.time + 0.2f;
                }
                if (this.unitLandTimer < Time.time && this.tossed && this.enemyPb.onGround)
                {
                    Collider[] aoe = GameUtility.GetAllInSphere(this.target.position, this.RADIUS, this.id.owner, new UnitType[1]);
                    for (int i = 0; i < aoe.Length; i++)
                    {
                        GameObject go = aoe[i].transform.root.gameObject;
                        if (go != this.target.gameObject && this.wizard != null)
                            go.GetComponent<PhysicsBody>().AddForceOwner(GameUtility.GetForceVector(this.wizard.transform.position, go.transform.position, this.tossMagnitude));
                        go.GetComponent<UnitStatus>().ApplyDamage(SLAM_DAMAGE, this.id.owner, 57);
                    }
                    base.photonView.RPCLocal(this, "rpcLand", PhotonTargets.All, new object[] { base.transform.position, false });
                }
            }

            if (this.target != null && this.targetEffects != null)
                this.targetEffects.position = this.target.position;
        }

        private void FixedUpdate()
        {
            switch (this.state)
            {
                case LungeState.AttachedToUnit:
                    if (base.photonView.IsMine() && this.wizard != null)
                    {
                        float force = this.GetForce((base.transform.position - this.wizard.transform.position).sqrMagnitude);
                        if (force > 0f)
                            this.phys.AddForce(GameUtility.GetForceVector(base.transform.position, this.wizard.transform.position, force));
                    }
                    break;
                case LungeState.EnemyHooked:
                    if (this.targetPv.IsMine() && this.enemyPb != null && this.wizard != null)
                    {
                        float force2 = this.GetForce((this.target.position - this.wizard.transform.position).sqrMagnitude);
                        if (force2 > 0f)
                            this.enemyPb.AddForce(GameUtility.GetForceVector(this.target.position, this.wizard.transform.position, force2));
                    }
                    else if (base.photonView.IsMine() && this.enemyPb == null)
                        this.deathTimer = 0f;
                    break;
            }

            if (!this.hit)
            {
                if (this.wizardParticles != null)
                {
                    if (this.wizardPhys != null)
                        this.wizardPhys.abilityVelocity = this.wizardParticles.forward * this.velocity;
                    Vector3 euler = this.wizardParticles.eulerAngles;
                    euler.y += this.curve;
                    this.wizardParticles.eulerAngles = euler;
                    if (this.wizard != null)
                    {
                        this.wizardParticles.position = this.wizard.transform.position;
                        this.wizard.transform.eulerAngles = euler;
                    }
                }
                else if (this.wizardPhys != null && this.wizard != null)
                {
                    // Fallback when wizardParticles is not available on the prefab:
                    // drive forward movement using the wizard's own transform direction
                    this.wizardPhys.abilityVelocity = this.wizard.transform.forward * this.velocity;
                    Vector3 euler = this.wizard.transform.eulerAngles;
                    euler.y += this.curve;
                    this.wizard.transform.eulerAngles = euler;
                }
                if (this.landTimer < Time.time && this.wizardPhys != null && this.wizardPhys.onGround)
                {
                    this.landTimer = Time.time + 100f;
                    if (base.photonView.IsMine())
                        base.photonView.RPCLocal(this, "rpcLand", PhotonTargets.All, new object[] { base.transform.position, true });
                }
            }

            if (base.photonView.IsConnectedAndNotLocal())
            {
                this.BaseClientCorrection();
                return;
            }
            if (!this.dying && this.deathTimer < Time.time)
                this.SpellObjectDeath();
        }

        public override void BaseClientCorrection()
        {
            base.transform.position = Vector3.Lerp(base.transform.position, this.correctObjectPos, 0.5f);
        }

        private float GetForce(float distanceSQ)
        {
            if (distanceSQ <= INNER_LENGTH_SQ) return 0f;
            if (distanceSQ > OUTER_LENGTH_SQ) return CHAIN_POWER_MAX;
            return CHAIN_POWER_M * (Mathf.Sqrt(distanceSQ) - 7.5f);
        }

        public override void ChangeToSpellLayer()
        {
            base.gameObject.layer = 11;
        }

        private void PositionLine()
        {
            if (this.vineTransforms == null || this.vineTransforms.Length == 0 || this.attach == null) return;
            if (this.vineTransforms[0] != null)
            {
                this.vineTransforms[0].position = this.attach.position;
                this.vineTransforms[0].rotation = this.attach.rotation;
            }
            var last = this.vineTransforms[this.vineTransforms.Length - 1];
            if (last != null && this.wizard != null)
                last.position = this.wizard.transform.position;
        }

        public override void SpellObjectDeath()
        {
            this.dying = true;
            if (!base.photonView.IsConnectedAndNotLocal() && this.wizard != null && this.state == LungeState.EnemyHooked)
                this.wizard.GetComponent<SpellHandler>().DisableRecast(this.spellName);
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, new object[] { false });
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!base.photonView.IsMine()) return;
            if (this.state == LungeState.AttachedToUnit)
            {
                GameObject gameObject = collision.transform.root.gameObject;
                if (this.wizard != null && gameObject != this.wizard.gameObject)
                {
                    Identity identity = GameUtility.GetIdentity(gameObject);
                    UnitType? unitType = (identity != null) ? new UnitType?(identity.type) : null;
                    if ((unitType.GetValueOrDefault() == UnitType.Unit) & (unitType != null))
                    {
                        int? ownerNum = (identity != null) ? new int?(identity.owner) : null;
                        if (!((ownerNum.GetValueOrDefault() == this.id.owner) & (ownerNum != null)))
                        {
                            if (Globals.online)
                            {
                                object[] arr = new object[2];
                                PhotonView pv = collision.gameObject.GetPhotonView();
                                arr[0] = (pv != null) ? pv.viewID : (-1);
                                arr[1] = unitType.Value;
                                base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All, arr);
                            }
                            else
                                this.localCollision(collision.gameObject, unitType.Value);
                            collision.gameObject.GetComponent<UnitStatus>().ApplyDamage(this.DAMAGE, this.id.owner, 57);
                        }
                    }
                }
            }
        }

        private void RegisterAvoids()
        {
            this.avoids = AxePhotonExtensions.AiEventHandler.RegisterAvoidsForAll(base.transform, 10f, 100f, false, this.id.owner, 0, TeamColor.None);
        }

        private void UnRegisterAvoids()
        {
            AxePhotonExtensions.AiEventHandler.UnRegisterAvoids(this.avoids);
        }

        [PunRPC]
        public void rpcSpellObjectDeath(bool remove)
        {
            this.dying = true;
            if (remove)
                UnityEngine.Object.Destroy(base.gameObject);
            else
            {
                if (this.anim != null) this.anim.SetBool("Closed", false);
                base.transform.DOScale(0f, 0.3f).SetEase(Ease.InOutCubic);
                if (this.vineTransforms != null)
                    for (int i = 0; i < this.vineTransforms.Length; i++)
                        this.vineTransforms[i].DOScale(0f, 0.3f).SetEase(Ease.InOutCubic);
                if (this.dustTrail != null) { var _em = this.dustTrail.emission; _em.enabled = false; }
                UnityEngine.Object.Destroy(base.gameObject, 1f);
            }
            this.UnRegisterAvoids();
        }

        [PunRPC]
        public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, float curve, float velocity, int spellIndex, int viewId)
        {
            PhotonView photonView = PhotonView.Find(viewId);
            this.wizard = (photonView != null) ? photonView.GetComponent<WizardController>() : null;
            if (this.wizard != null)
            {
                this.wizardPhys = this.wizard.GetComponent<PhysicsBody>();
                CrystalObject.GetOutOfPreserveCrystals(owner, this.wizard.transform);
            }
            this.phys = base.GetComponent<PhysicsBody>();
            this.id.owner = owner;
            GameUtility.SetWizardColor(this.id.owner, base.gameObject, false);
            base.transform.position = pos;
            base.transform.rotation = rot;
            this.curve = curve;
            this.velocity = velocity;
            this.spellIndex = spellIndex;
            this.landTimer = Time.time + 0.2f;
            if (this.wizardPhys != null)
            {
                this.wizardPhys.abilityVelocity = base.transform.forward * velocity;
                this.wizardPhys.velocity += Vector3.up * 1.2f;
                this.wizard.ModifyGravity(this.phys.gravity);
                this.gravityResets++;
            }
            this.RegisterAvoids();
            if (this.sp != null)
                this.sp.PlaySoundInstantiate("event:/sfx/metal/steal-trap-set", 5f);
        }

        [PunRPC]
        public void rpcPull()
        {
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/steal-trap-whip", 5f).SetPitch(1f);
            if (this.wizardPhys != null)
            {
                CrystalObject.GetOutOfPreserveCrystals(this.id.owner, this.wizard.transform);
                this.wizardPhys.velocity += Vector3.up * 1.2f;
                this.wizard.ModifyGravity(this.phys.gravity);
                this.landTimer = Time.time + 0.2f;
                this.hit = false;
                this.gravityResets++;
                this.velocity = 0f;
                this.unitLandTimer = Time.time + 1f;
            }
            if (this.target != null && this.vineTransforms != null)
            {
                for (int i = 1; i < this.vineTransforms.Length - 1; i++)
                {
                    this.vineTransforms[i].DOMoveY(base.transform.position.y + 5f, 0.2f, false).SetEase(Ease.OutCubic).SetDelay((float)(4 - i) * 0.1f);
                    this.vineTransforms[i].DOScale(10f, 0.2f).SetEase(Ease.OutCubic).SetDelay((float)(4 - i) * 0.1f);
                    this.vineTransforms[i].DOScale(5f, 0.2f).SetEase(Ease.OutCubic).SetDelay((float)(4 - i) * 0.1f + 0.2f);
                }
                if (this.state == LungeState.EnemyHooked)
                {
                    this.state = LungeState.ThrowingUnit;
                    if (this.gettinSlammed != null) { var _em = this.gettinSlammed.emission; _em.enabled = true; }
                    if (this.targetPv.IsMine()) base.Invoke("Throw", 0.4f);
                    GameUtility.RegisterDamageOverTime(1f, this.wizard != null ? this.wizard.transform : null, this.target, 1f);
                }
            }
        }

        public void Throw()
        {
            if (this.enemyPb != null && this.wizard != null)
            {
                Vector3 dir = (this.wizard.transform.position - this.target.position).WithY(0f);
                this.tossMagnitude = dir.magnitude * this.multiplier;
                this.enemyPb.rpcSetVelocityOwner(dir * this.multiplier + Vector3.up * 0.9f);
                this.target.GetComponent<UnitStatus>().ApplyDamage(1f, this.id.owner, 57);
            }
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.isWriting)
            {
                stream.SendNext(base.transform.position);
                stream.SendNext(base.transform.rotation);
            }
            else
                this.correctObjectPos = (Vector3)stream.ReceiveNext();
        }

        [PunRPC]
        public void rpcLand(Vector3 pos, bool me)
        {
            if (me)
            {
                this.hit = true;
                if (this.wizardParticles != null && this.wizardParticles.childCount > 1)
                {
                    var _ps = this.wizardParticles.GetChild(1).GetComponent<ParticleSystem>();
                    var _em = _ps.emission; _em.enabled = false;
                }
                this.ResetGravityAndAbilityVelocity();
                return;
            }
            this.state = LungeState.End;
            if (this.sp != null) this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/steal-trap-slam", 5f);
            if (this.gettinSlammed != null) { var _em = this.gettinSlammed.emission; _em.enabled = false; }
            if (this.slam != null) this.slam.Play();
            this.rpcSpellObjectDeath(false);
        }

        [PunRPC]
        public void rpcCollision(int viewId, UnitType unitType)
        {
            if (viewId == -1)
            {
                this.localCollision(null, unitType);
                return;
            }
            PhotonView photonView = PhotonView.Find(viewId);
            GameObject gameObject = (photonView != null) ? photonView.gameObject : null;
            if (gameObject != null) this.localCollision(gameObject, unitType);
        }

        public void localCollision(GameObject go, UnitType unitType)
        {
            if (go == null) return;
            if (this.sp != null) this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/steal-trap-catch", 5f);
            if (this.wizardPhys == null) this.wizardPhys = base.GetComponent<PhysicsBody>();
            this.wizardPhys.velocity = Vector3.zero;
            if (this.anim != null) this.anim.SetBool("Closed", true);
            base.GetComponent<Collider>().enabled = false;
            this.target = go.transform;
            UnitStatus us = this.target.GetComponent<UnitStatus>();
            if (us != null && us.ankle != null)
            {
                this.ankle = us.ankle.transform;
                this.isAnkle = true;
            }
            else
                this.ankle = this.target;
            if (Globals.online) this.targetPv = go.GetPhotonView();
            this.enemyPb = this.target.GetComponent<PhysicsBody>();
            if (unitType == UnitType.Unit)
            {
                this.UnRegisterAvoids();
                WizardController wc = go.GetComponent<WizardController>();
                if ((wc == null || wc.isClone) && go.GetComponent<SharkController>() == null && go.GetComponent<Puck>() == null)
                {
                    this.target.GetComponent<Identity>().owner = this.id.owner;
                    SpellObject so = this.target.GetComponent<SpellObject>();
                    if (so == null)
                    {
                        GameUtility.SetWizardColor(this.id.owner, go, false);
                        WizardStatus ws = go.GetComponent<WizardStatus>();
                        if (ws != null) ws.SaveMaterials();
                    }
                    else
                        so.UpdateColor();
                    if (this.loveSparkles != null) this.loveSparkles.Play(true);
                    if (this.sp != null) this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/steal-trap-love", 5f);
                }
                this.state = LungeState.EnemyHooked;
                if (this.dustTrail != null) { var _em = this.dustTrail.emission; _em.enabled = false; }
                if (this.trapClose != null) this.trapClose.Play();
            }
            this.deathTimer = Time.time + 4f;
            if (!base.photonView.IsConnectedAndNotLocal())
                this.wizard.GetComponent<SpellHandler>().EnableRecast(this.spellName);
        }

        public enum LungeState
        {
            AttachedToUnit,
            EnemyHooked,
            ThrowingUnit,
            End
        }
    }
}
