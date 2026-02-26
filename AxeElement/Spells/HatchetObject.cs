using System;
using System.Collections.Generic;
using DG.Tweening;
using FMOD.Studio;
using UnityEngine;

namespace AxeElement
{
    public class HatchetObject : SpellObject
    {
        // Inspector-assigned fields matching the Glaive prefab
        public UnityEngine.Object impact;

        private PhysicsBody phys;
        private float accel = 1f;
        public float homingCurve = 5f;
        private float homingAccel = 1.001f;
        public bool homing;
        private List<GameObject> alreadyHit = new List<GameObject>();
        public Transform caster;
        public Transform target;
        public GameObject initialTarget;
        private float noHitTimer;
        private float noSelfHitTimer;
        private Transform child;
        private EventInstance aSource;
        private Collision cachedCollision;

        public HatchetObject()
        {
            DAMAGE = 7f;
            RADIUS = 4f;
            POWER = 30f;
            Y_POWER = 0f;
            START_TIME = 1.2f;
            collisionRadius = 1f;
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
            this.noSelfHitTimer = Time.time + 0.17f;
            if (sp != null)
            {
                this.aSource = sp.PlaySound("event:/sfx/metal/glaive-cast");
            }
            SpellObjectStart();
        }

        public override void SpellObjectStart()
        {
            base.SpellObjectStart();
            WizardController wizard = GameUtility.GetWizard(id.owner);
            this.caster = (wizard != null) ? wizard.transform : null;
            UpdateColor();
        }

        public void Init(Identity identity, float curve, float velocity)
        {
            id.owner = identity.owner;
            this.caster = identity.transform;
            this.curve = curve;
            this.velocity = velocity;
            ChangeToSpellLayerDelayed(velocity);
            if (!base.photonView.IsConnectedAndNotLocal())
            {
                base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.All, new object[]
                {
                    identity.owner,
                    base.transform.position,
                    base.transform.rotation,
                    curve,
                    velocity
                });
            }
        }

        private void FixedUpdate()
        {
            Vector3 localEulerAngles = this.child.localEulerAngles;
            localEulerAngles.y += 20f;
            this.child.localEulerAngles = localEulerAngles;

            if (!this.homing || this.target == null)
            {
                this.velocity *= this.accel;
                this.curve *= this.accel;
                Vector3 eulerAngles = base.transform.eulerAngles;
                eulerAngles.y += this.curve;
                base.transform.eulerAngles = eulerAngles;
            }
            else
            {
                this.homingCurve *= this.homingAccel;
                float num = base.transform.eulerAngles.y;
                float num2 = 90f - Mathf.Atan2(this.target.position.z - base.transform.position.z,
                    this.target.position.x - base.transform.position.x) * 57.29578f;
                if (num2 < 0f) num2 += 360f;

                bool flag;
                if (num2 >= 180f)
                    flag = num > num2 || num < num2 - 180f;
                else
                    flag = num > num2 && num < num2 + 180f;

                if (flag)
                {
                    if (this.homingCurve > num)
                    {
                        if (num + 360f < num2 + this.homingCurve || num >= num2)
                            num = num2;
                        else
                            num += 360f - this.homingCurve;
                    }
                    else if (num - this.homingCurve < num2 && num >= num2)
                        num = num2;
                    else
                        num -= this.homingCurve;
                }
                else if (num + this.homingCurve >= 360f)
                {
                    if (num + this.homingCurve > num2 + 360f || num <= num2)
                        num = num2;
                    else
                        num += this.homingCurve - 360f;
                }
                else if (num + this.homingCurve > num2 && num <= num2)
                    num = num2;
                else
                    num += this.homingCurve;

                Vector3 euler2 = base.transform.eulerAngles;
                euler2.y = num;
                base.transform.eulerAngles = euler2;
            }

            this.phys.movementVelocity = base.transform.forward * this.velocity;

            if (base.photonView.IsConnectedAndNotLocal())
            {
                BaseClientCorrection();
                return;
            }

            if (this.noHitTimer != 0f && this.noHitTimer < Time.time && this.cachedCollision != null)
            {
                this.Collide(this.cachedCollision);
                this.cachedCollision = null;
                this.noHitTimer = 0f;
            }

            if (deathTimer < Time.time)
            {
                SpellObjectDeath();
            }
        }

        public override void SpellObjectDeath()
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (base.photonView.IsConnectedAndNotLocal()) return;
            if (this.noHitTimer >= Time.time)
            {
                this.cachedCollision = collision;
                return;
            }
            GameObject gameObject = collision.gameObject;
            if (this.noSelfHitTimer > Time.time && GameUtility.IdentityCompare(gameObject, id.owner)) return;
            this.Collide(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (base.photonView.IsConnectedAndNotLocal()) return;
            UnityEngine.Object obj = (collision != null) ? collision.gameObject : null;
            Collision col2 = this.cachedCollision;
            if (obj == ((col2 != null) ? col2.gameObject : null))
                this.cachedCollision = null;
        }

        private void Collide(Collision collision)
        {
            GameObject gameObject = null;
            if (base.gameObject == null) return;
            if (collision != null && collision.collider != null && collision.transform != null && collision.transform.root != null)
                gameObject = collision.transform.root.gameObject;
            if (gameObject == null) return;

            if (GameUtility.IdentityCompare(gameObject, UnitType.Unit) &&
                gameObject.transform != this.caster &&
                !this.alreadyHit.Contains(gameObject))
            {
                this.alreadyHit.Add(gameObject);
                this.cachedCollision = collision;

                if (!this.homing)
                {
                    deathTimer += 4f;
                    this.initialTarget = gameObject;
                    this.homing = true;

                    Collider[] allInSphere = GameUtility.GetAllInSphere(base.transform.position, 15f, -1, new UnitType[1]);
                    float num = float.PositiveInfinity;
                    Collider nearest = null;
                    foreach (Collider col in allInSphere)
                    {
                        Transform root = col.transform.root;
                        if (root == this.caster) continue;
                        if (root.gameObject == this.initialTarget) continue;
                        if (root.GetComponent<CrystalObject>() != null) continue;
                        float sqrMag = (col.transform.position - base.transform.position).sqrMagnitude;
                        if (sqrMag < num && sqrMag != 0f)
                        {
                            num = sqrMag;
                            nearest = col;
                        }
                    }
                    this.target = (nearest != null) ? nearest.transform : null;
                }

                base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All, new object[] { gameObject.transform.position });

                Collider[] aoeSphere = GameUtility.GetAllInSphere(base.transform.position, RADIUS, id.owner, new UnitType[1]);
                for (int i = 0; i < aoeSphere.Length; i++)
                {
                    GameObject go2 = aoeSphere[i].transform.root.gameObject;
                    go2.GetComponent<PhysicsBody>().AddForceOwner(GameUtility.GetForceVector(base.transform.position, go2.transform.position, POWER));
                    go2.GetComponent<UnitStatus>().ApplyDamage(
                        (go2 == gameObject) ? DAMAGE : (DAMAGE * 0.5f),
                        id.owner, 56);
                }

                if (this.target == null || this.target.gameObject == gameObject)
                    SpellObjectDeath();
            }
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            if (this.aSource.isValid())
                this.aSource.FadeSoundOut(0f, 0.1f, 0f);
            base.transform.DOScale(0f, 0.2f);
            base.GetComponent<CapsuleCollider>().enabled = false;
            UnityEngine.Object.Destroy(base.gameObject, 1f);
        }

        [PunRPC]
        public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, float curve, float velocity)
        {
            id.owner = owner;
            base.transform.position = pos;
            base.transform.rotation = rot;
            this.curve = curve;
            this.velocity = velocity;
            SpellObjectStart();
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            BaseSerialize(stream, info);
        }

        [PunRPC]
        public void rpcCollision(Vector3 pos)
        {
            if (base.transform == null) return;
            if (sp != null)
                sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-hit", 5f);
            if (this.impact != null)
                UnityEngine.Object.Instantiate(this.impact, base.transform.position, Globals.sideways);
            if (this.target != null)
            {
                Quaternion q = Quaternion.LookRotation(GameUtility.GetForceVector(pos, this.target.position, 1f));
                base.transform.rotation = q;
            }
            this.noHitTimer = Time.time + 0.17f;
        }
    }
}
