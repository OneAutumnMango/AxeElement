using System;
using System.Collections.Generic;
using DG.Tweening;
using FMOD.Studio;
using UnityEngine;

namespace AxeElement
{
    public class TomahawkObject : SpellObject
    {
        // Inspector-assigned from Urchain prefab
        public Transform[] vineTransforms1;
        public Transform[] vineTransforms2;
        public Animator anim;
        public ParticleSystem[] ps;
        public UnityEngine.Object impact;

        private const float CHAIN_POWER = 6f;

        private PhysicsBody phys;
        private float accel = 1f;
        private Transform caster;
        private Transform target;
        private Transform target2;
        private PhysicsBody targetPb;
        private PhysicsBody target2Pb;
        private PhotonView targetView;
        private PhotonView target2View;
        private UnitStatus targetStatus;
        private new float curve;
        private new float velocity;
        private float stateTimer;
        private Collider col;
        private Transform child;
        private Vector3 offset;
        private Quaternion offsetRotation;
        private List<GameObject> alreadyHit = new List<GameObject>();
        private bool alreadyExploded;
        private float skinnyArmTimer;
        private Transform target2Temp;
        private Transform lastTarget2Temp;
        private EventInstance aSource;
        private Vector3 urchainOffset = new Vector3(0f, 3f, 0f);
        public TomahawkState state;

        public TomahawkObject()
        {
            DAMAGE = 10f;
            RADIUS = 3f;
            POWER = 25f;
            START_TIME = 2f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (id == null) id = new Identity();
        }

        private void Start()
        {
            this.child = base.transform.GetChild(0);
            this.phys = base.GetComponent<PhysicsBody>();
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/urchain-cast", 5f);
            this.deathTimer = Time.time + this.START_TIME;
            if (this.vineTransforms1 != null)
                for (int i = 0; i < this.vineTransforms1.Length; i++)
                    if (this.vineTransforms1[i] != null)
                        this.vineTransforms1[i].localScale = Vector3.zero;
            if (this.vineTransforms2 != null)
                for (int i = 0; i < this.vineTransforms2.Length; i++)
                    if (this.vineTransforms2[i] != null)
                        this.vineTransforms2[i].localScale = Vector3.zero;
            WizardController wizard = GameUtility.GetWizard(this.id.owner);
            this.caster = (wizard != null) ? wizard.transform : null;
            GameUtility.SetWizardColor(this.id.owner, base.gameObject, false);
        }

        public void Init(int owner, float curve, float velocity)
        {
            this.col = base.GetComponent<Collider>();
            this.stateTimer = Time.time + 10f;
            this.id.owner = owner;
            this.curve = curve;
            this.velocity = velocity;
            WizardController wizard = GameUtility.GetWizard(owner);
            this.caster = (wizard != null) ? wizard.transform : null;
            base.gameObject.layer = 0;
            if (!base.photonView.IsConnectedAndNotLocal())
            {
                base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.All,
                    new object[] { owner, base.transform.position, base.transform.rotation, curve, velocity });
            }
        }

        private void FixedUpdate()
        {
            switch (this.state)
            {
                case TomahawkState.Projectile:
                    this.velocity *= this.accel;
                    this.curve *= this.accel;
                    Vector3 euler = base.transform.eulerAngles;
                    euler.y += this.curve;
                    base.transform.eulerAngles = euler;
                    if (this.phys != null)
                        this.phys.movementVelocity = base.transform.forward * this.velocity;
                    if (base.photonView.IsConnectedAndNotLocal())
                    {
                        this.BaseClientCorrection();
                        return;
                    }
                    if (this.deathTimer < Time.time)
                    {
                        this.SpellObjectDeath();
                        return;
                    }
                    break;
                case TomahawkState.Hit:
                    this.state = TomahawkState.Stick;
                    return;
                case TomahawkState.Stick:
                    if (this.skinnyArmTimer < Time.time)
                    {
                        this.skinnyArmTimer = Time.time + 0.25f;
                        GameObject nearestTasty = this.GetNearestTasty();
                        this.target2Temp = (nearestTasty != null) ? nearestTasty.transform : null;
                        if (this.lastTarget2Temp != this.target2Temp && this.sp != null)
                            this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/urchain-chain-jump", 5f);
                        this.lastTarget2Temp = this.target2Temp;
                    }
                    if (!base.photonView.IsConnectedAndNotLocal())
                    {
                        if (this.target == null)
                        {
                            this.SpellObjectDeath();
                            return;
                        }
                        if (this.stateTimer < Time.time)
                        {
                            GameObject nearest = this.GetNearestTasty();
                            if (nearest == null)
                            {
                                this.SpellObjectDeath();
                                return;
                            }
                            if (Globals.online)
                            {
                                PhotonView pv = nearest.GetPhotonView();
                                int? viewId = (pv != null) ? new int?(pv.viewID) : null;
                                base.photonView.RPCLocal(this, "rpcDetachLeech", PhotonTargets.All,
                                    new object[] { viewId ?? (-1) });
                            }
                            else
                            {
                                this.localDetachLeech(nearest);
                            }
                            return;
                        }
                    }
                    break;
                case TomahawkState.Jump:
                    if (this.target == null || this.target2 == null)
                    {
                        if (!base.photonView.IsConnectedAndNotLocal())
                            this.SpellObjectDeath();
                        return;
                    }
                    if (this.stateTimer < Time.time)
                    {
                        this.stateTimer = Time.time + 4f;
                        this.state = TomahawkState.Pull;
                        return;
                    }
                    break;
                case TomahawkState.Pull:
                    if (this.target == null || this.target2 == null)
                    {
                        if (!base.photonView.IsConnectedAndNotLocal())
                            this.SpellObjectDeath();
                        return;
                    }
                    this.ApplyForceTowardUnit(this.targetPb, this.target, this.target2);
                    this.ApplyForceTowardUnit(this.target2Pb, this.target2, this.target);
                    if (base.photonView.IsConnectedAndNotLocal()) return;
                    if ((this.target.position - this.target2.position).sqrMagnitude < 4f)
                    {
                        this.TriggerCollision();
                        return;
                    }
                    if (this.stateTimer < Time.time)
                        this.SpellObjectDeath();
                    break;
                case TomahawkState.End:
                    break;
            }
        }

        private void Update()
        {
            if (this.state == TomahawkState.Stick && this.target != null)
            {
                base.transform.rotation = this.target.rotation * this.offsetRotation;
                base.transform.position = this.target.position - base.transform.forward * 1.5f;
                this.PositionLine2();
            }
            if ((this.state == TomahawkState.Jump || this.state == TomahawkState.Pull) &&
                this.target != null && this.target2 != null)
            {
                this.UpdatePosition();
            }
        }

        private GameObject GetNearestTasty()
        {
            Collider[] nearby = GameUtility.GetAllInSphere(base.transform.position, 40f, -1, new UnitType[1]);
            float nearestDist = float.PositiveInfinity;
            Collider nearest = null;
            foreach (Collider c in nearby)
            {
                if (c.transform.root == this.target) continue;
                float dist = (c.transform.position - base.transform.position).sqrMagnitude;
                if (dist < nearestDist && dist != 0f)
                {
                    nearestDist = dist;
                    nearest = c;
                }
            }
            return (nearest != null) ? nearest.gameObject : null;
        }

        private void ApplyForceTowardUnit(PhysicsBody pb, Transform from, Transform to)
        {
            if (pb == null) return;
            pb.velocity *= 0.975f;
            pb.rpcAddForce(GameUtility.GetForceVector(from.position, to.position, CHAIN_POWER));
        }

        private void UpdatePosition()
        {
            Vector3 mid = (this.target.position + this.target2.position) / 2f + this.urchainOffset;
            base.transform.position = Vector3.Lerp(base.transform.position, mid, 0.1f);
            if (this.phys != null)
                this.phys.velocity = Vector3.zero;
            this.PositionLine();
            this.PositionLine2();
        }

        public override void SpellObjectDeath()
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        private void TriggerCollision()
        {
            if (this.target == null || this.target2 == null) return;
            Vector3 mid = (this.target2.position + this.target.position) / 2f;
            base.photonView.RPCLocal(this, "rpcTargetCollision", PhotonTargets.All, new object[] { mid });
            Collider[] aoe = GameUtility.GetAllInSphere(mid, this.RADIUS, -1, new UnitType[1]);
            for (int i = 0; i < aoe.Length; i++)
            {
                GameObject unit = aoe[i].transform.root.gameObject;
                if (Globals.online)
                {
                    PhotonView pv = unit.GetPhotonView();
                    if (pv != null)
                    {
                        base.photonView.RPCLocal(this, "rpcExplode", pv.GetOwnerOrMaster(),
                            new object[] { pv.viewID, unit.transform.position - mid });
                    }
                }
                else
                {
                    this.localExplode(unit, unit.transform.position - mid);
                }
            }
            this.SpellObjectDeath();
        }

        private void PositionLine()
        {
            if (this.vineTransforms1 == null || this.vineTransforms1.Length == 0) return;
            if (this.vineTransforms1[0] != null)
            {
                this.vineTransforms1[0].position = base.transform.position + Vector3.up;
                this.vineTransforms1[0].rotation = base.transform.rotation;
            }
            if (this.target != null)
            {
                var last = this.vineTransforms1[this.vineTransforms1.Length - 1];
                if (last != null)
                    last.position = this.target.position + Vector3.up;
            }
        }

        private void PositionLine2()
        {
            if (this.vineTransforms2 == null || this.vineTransforms2.Length == 0) return;
            if (this.vineTransforms2[0] != null)
            {
                this.vineTransforms2[0].position = base.transform.position + Vector3.up;
                this.vineTransforms2[0].rotation = base.transform.rotation;
            }
            var last2 = this.vineTransforms2[this.vineTransforms2.Length - 1];
            if (last2 != null)
            {
                if (this.target2 != null)
                    last2.position = this.target2.position + Vector3.up;
                else if (this.target2Temp != null)
                    last2.position = this.target2Temp.position + Vector3.up;
                else
                    last2.position = base.transform.position + Vector3.up;
            }
        }

        private void OnDestroy()
        {
            if (this.vineTransforms1 != null)
                for (int i = 0; i < this.vineTransforms1.Length; i++)
                {
                    Transform t = this.vineTransforms1[i];
                    if (t != null && t.gameObject != null)
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                    if (i < this.vineTransforms2.Length)
                    {
                        Transform t2 = this.vineTransforms2[i];
                        if (t2 != null && t2.gameObject != null)
                            UnityEngine.Object.DestroyImmediate(t2.gameObject);
                    }
                }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (base.photonView.IsConnectedAndNotLocal()) return;
            if (this.state == TomahawkState.Projectile)
            {
                GameObject go = collision.transform.root.gameObject;
                if (GameUtility.IdentityCompare(go, UnitType.Unit) &&
                    (this.caster == null || go != this.caster.gameObject))
                {
                    if (Globals.online)
                    {
                        base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All, new object[]
                        {
                            (base.transform.position - go.transform.position).WithY(0f).normalized,
                            go.GetPhotonView().viewID
                        });
                    }
                    else
                    {
                        this.localCollision((base.transform.position - go.transform.position).WithY(0f).normalized, go);
                    }
                }
            }
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            if (this.col != null) this.col.enabled = false;
            if (this.ps != null && this.ps.Length > 0)
            {
                { var _em = this.ps[0].emission; _em.enabled = false; }
                if (this.ps.Length > 3) { var _em = this.ps[3].emission; _em.enabled = false; }
            }
            if (this.sp != null && this.aSource.isValid())
                this.aSource.FadeSoundOut(0f, 0.1f, 0f);
            if (this.state != TomahawkState.Pull && this.phys != null)
            {
                this.phys.movementVelocity = Vector3.zero;
                this.phys.velocity = Vector3.zero;
                this.velocity = 0f;
            }
            base.transform.DOScale(0f, 0.3f).SetEase(Ease.InOutCubic);
            this.state = TomahawkState.End;
            if (this.vineTransforms1 != null)
                for (int i = 0; i < this.vineTransforms1.Length; i++)
                    if (this.vineTransforms1[i] != null)
                        this.vineTransforms1[i].DOScale(0f, 0.3f);
            if (this.vineTransforms2 != null)
                for (int i = 0; i < this.vineTransforms2.Length; i++)
                    if (this.vineTransforms2[i] != null)
                        this.vineTransforms2[i].DOScale(0f, 0.3f);
            UnityEngine.Object.Destroy(base.gameObject, 2.5f);
        }

        [PunRPC]
        public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, float curve, float velocity)
        {
            this.col = base.GetComponent<Collider>();
            this.stateTimer = Time.time + 10f;
            this.id.owner = owner;
            base.transform.position = pos;
            base.transform.rotation = rot;
            this.curve = curve;
            this.velocity = velocity;
            this.deathTimer = Time.time + this.START_TIME;
            WizardController wizard = GameUtility.GetWizard(owner);
            this.caster = (wizard != null) ? wizard.transform : null;
            GameUtility.SetWizardColor(owner, base.gameObject, false);
        }

        [PunRPC]
        public void rpcExplode(int viewId, Vector3 dir)
        {
            PhotonView pv = PhotonView.Find(viewId);
            GameObject go = (pv != null) ? pv.gameObject : null;
            this.localExplode(go, dir);
        }

        public void localExplode(GameObject go, Vector3 dir)
        {
            if (go == null) return;
            if (this.alreadyHit.Contains(go)) return;
            this.alreadyHit.Add(go);
            go.GetComponent<PhysicsBody>().SetVelocityOwner(Vector3.zero);
            go.GetComponent<UnitStatus>().ApplyDamage(this.DAMAGE, this.id.owner, 59);
        }

        [PunRPC]
        public void rpcDetachLeech(int viewId)
        {
            PhotonView pv = PhotonView.Find(viewId);
            GameObject go = (pv != null) ? pv.gameObject : null;
            if (go != null) this.localDetachLeech(go);
        }

        public void localDetachLeech(GameObject target2)
        {
            this.stateTimer = Time.time + 0.3f;
            this.state = TomahawkState.Jump;
            if (this.anim != null) this.anim.SetBool("Stuck", false);
            if (this.ps != null && this.ps.Length > 2) { var _em = this.ps[2].emission; _em.enabled = false; }
            base.Invoke("ShowSweat", 0.3f);
            if (this.sp != null)
                this.aSource = this.sp.PlaySound("event:/sfx/metal/urchain-jump-and-pull").SetPitch(1f);
            this.target2 = target2.transform;
            this.target2Pb = target2.GetComponent<PhysicsBody>();
            this.target2Temp = null;
            if (Globals.online)
                this.target2View = target2.GetPhotonView();
            if (this.targetStatus != null && this.targetView != null && this.targetView.IsMine())
                this.targetStatus.ApplyDamage(1f, this.id.owner, 59);
            if (target2 != null && this.target2View != null && this.target2View.IsMine())
                target2.GetComponent<UnitStatus>().ApplyDamage(1f, this.id.owner, 59);
            UnityEngine.Object.Destroy(this.phys);
            Rigidbody rb = base.GetComponent<Rigidbody>();
            if (rb != null) UnityEngine.Object.Destroy(rb);
            if (this.vineTransforms1 != null)
                foreach (Transform t in this.vineTransforms1)
                {
                    if (t == null) continue;
                    t.parent = null;
                    t.DOScale(5f, 0.3f);
                }
            if (this.vineTransforms2 != null)
                for (int i = 0; i < this.vineTransforms2.Length; i++)
                    if (this.vineTransforms2[i] != null)
                        this.vineTransforms2[i].DOScale(5f, 0.3f);
        }

        public void ShowSweat()
        {
            if (this.ps != null && this.ps.Length > 3)
            {
                var _em = this.ps[3].emission; _em.enabled = true;
            }
        }

        [PunRPC]
        public void rpcCollision(Vector3 pos, int viewId)
        {
            PhotonView pv = PhotonView.Find(viewId);
            if (pv != null)
                this.localCollision(pos, pv.gameObject);
        }

        public void localCollision(Vector3 pos, GameObject enemy)
        {
            if (this.state == TomahawkState.Projectile)
            {
                this.state = TomahawkState.Hit;
                this.target = enemy.transform;
                this.targetPb = enemy.GetComponent<PhysicsBody>();
                this.targetStatus = enemy.GetComponent<UnitStatus>();
                PhotonView pv = enemy.GetPhotonView();
                if (Globals.online)
                {
                    PhotonPlayer ownerOrMaster = pv.GetOwnerOrMaster();
                    this.targetView = pv;
                }
                if (pv != null && pv.IsMine() && this.targetStatus != null)
                    this.targetStatus.ApplyDamage(1f, this.id.owner, 59);
                this.velocity = 8f;
                this.stateTimer = Time.time + 4f;
                WizardController wizard = GameUtility.GetWizard(this.id.owner);
                Transform casterT = (wizard != null) ? wizard.transform : null;
                GameUtility.RegisterDamageOverTime(4.3f, casterT, this.target, 4f);
                if (this.col != null) this.col.enabled = false;
                base.gameObject.layer = 0;
                if (this.anim != null) this.anim.SetBool("Stuck", true);
                this.offset = pos;
                if (this.ps != null)
                {
                    if (this.ps.Length > 0) { var _em = this.ps[0].emission; _em.enabled = false; }
                    if (this.ps.Length > 1) this.ps[1].Play();
                    if (this.ps.Length > 2) { var _em = this.ps[2].emission; _em.enabled = true; }
                }
                if (this.sp != null)
                    this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/urchain-hit", 5f);
                base.transform.position = enemy.transform.position + this.offset;
                base.transform.LookAt(enemy.transform.position, Vector3.up);
                this.offsetRotation = base.transform.rotation * Quaternion.Inverse(enemy.transform.rotation);
                if (this.vineTransforms2 != null)
                    foreach (Transform t in this.vineTransforms2)
                    {
                        if (t == null) continue;
                        t.parent = null;
                        t.DOScale(2f, 0.3f);
                    }
            }
        }

        [PunRPC]
        public void rpcTargetCollision(Vector3 pos)
        {
            if (this.alreadyExploded) return;
            this.alreadyExploded = true;
            if (this.sp != null)
            {
                if (this.aSource.isValid())
                    this.aSource.FadeSoundOut(0f, 0.1f, 0f);
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/urchain-impact", 5f);
            }
            if (this.impact != null)
                UnityEngine.Object.Instantiate(this.impact, pos, Globals.sideways);
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            BaseSerialize(stream, info);
        }

        public enum TomahawkState
        {
            Projectile,
            Hit,
            Stick,
            Jump,
            Pull,
            End
        }
    }
}
