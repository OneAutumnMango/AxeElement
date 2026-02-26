using System;
using DG.Tweening;
using FMOD.Studio;
using UnityEngine;

namespace AxeElement
{
    public class ShatterObject : global::Photon.MonoBehaviour
    {
        public float DAMAGE = 7f;
        protected float RADIUS = 0f;
        protected float POWER = 0f;
        protected float START_TIME = 1.156f;

        public float deathTimer;
        protected Identity id = new Identity();
        protected SoundPlayer sp;

        // Inspector-assigned from Reflex prefab
        public UnityEngine.Object impact;
        public UnityEngine.Object effect;

        private PhysicsBody phys;
        private float accel = 1f;
        private float decel = 0.8f;
        private bool dying;
        private Transform child;
        private Transform hammer;
        private Transform target;
        private EventInstance aSource;
        private float curve;
        private float velocity;

        private Vector3 correctObjectPos;

        private void Awake()
        {
            this.sp = base.GetComponent<SoundPlayer>();
        }

        private void Start()
        {
            this.child = base.transform.GetChild(0);
            this.phys = base.GetComponent<PhysicsBody>();
            if (this.sp != null)
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-cast", 5f);
            this.deathTimer = Time.time + this.START_TIME;
        }

        public void Init(int owner, float curve, float velocity)
        {
            this.id.owner = owner;
            this.curve = curve;
            this.velocity = velocity;
            base.gameObject.layer = 0;
            GameUtility.SetWizardColor(owner, base.gameObject, false);
            if (!base.photonView.IsConnectedAndNotLocal())
            {
                base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.All,
                    new object[] { owner, base.transform.position, base.transform.rotation, curve, velocity });
            }
        }

        private void Update()
        {
            if (this.hammer != null && this.target != null)
                this.hammer.position = this.target.position;
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

        public void SpellObjectDeath()
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (base.photonView.IsConnectedAndNotLocal()) return;
            GameObject go = collision.transform.root.gameObject;
            if (!this.dying && GameUtility.IdentityCompare(go, UnitType.Unit) &&
                !GameUtility.IdentityCompare(go, this.id.owner))
            {
                if (Globals.online)
                {
                    PhotonView pv = go.GetPhotonView();
                    base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All, new object[]
                    {
                        base.transform.position,
                        (pv != null) ? new int?(pv.viewID) : null,
                        SpellButton.Utility
                    });
                }
                else
                {
                    this.localCollision(base.transform.position, go);
                }
                go.GetComponent<UnitStatus>().ApplyDamage(this.DAMAGE, this.id.owner, 61);
                this.SpellObjectDeath();
            }
        }

        private void OnDestroy()
        {
            if (this.hammer != null)
                UnityEngine.Object.Destroy(this.hammer.gameObject);
            if (this.aSource.isValid())
                this.aSource.FadeSoundOut(0f, 0.2f, 0f);
        }

        public void PlayImpact()
        {
            if (this.hammer == null) return;
            if (this.impact != null)
                UnityEngine.Object.Instantiate(this.impact, this.hammer.position, Globals.sideways);
            if (this.sp != null)
            {
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/reflex-impact", 5f);
                if (this.aSource.isValid())
                    this.aSource.FadeSoundOut(0f, 0.2f, 0f);
            }
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
                    var _em = ps.emission; _em.enabled = false;
                }
                foreach (Transform sub in child)
                {
                    ParticleSystem ps2 = sub.GetComponent<ParticleSystem>();
                    if (ps2 != null) { var _em2 = ps2.emission; _em2.enabled = false; }
                }
            }
            UnityEngine.Object.Destroy(base.gameObject, 8f);
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
            GameUtility.SetWizardColor(owner, base.gameObject, false);
        }

        [PunRPC]
        public void rpcCollision(Vector3 pos, int viewId, SpellButton button)
        {
            PhotonView pv = PhotonView.Find(viewId);
            GameObject go = (pv != null) ? pv.gameObject : null;
            if (go != null) this.localCollision(pos, go);
        }

        public void localCollision(Vector3 pos, GameObject wizard)
        {
            if (this.effect != null)
            {
                GameObject effectGo = UnityEngine.Object.Instantiate(this.effect, wizard.transform.position, Quaternion.identity) as GameObject;
                if (effectGo != null)
                {
                    this.hammer = effectGo.transform;
                    GameUtility.SetWizardColor(this.id.owner, this.hammer.gameObject, false);
                }
            }
            if (this.sp != null)
            {
                this.sp.PlaySoundComponentInstantiate("event:/sfx/metal/reflex-hit", 5f);
                this.aSource = this.sp.PlaySound("event:/sfx/metal/reflex-effect").FadeSoundIn(0f, 0.2f, 0f);
            }
            this.target = (wizard != null) ? wizard.transform : null;
        }

        [PunRPC]
        public void rpcSwingHammer()
        {
            if (this.hammer != null)
            {
                Animator anim = this.hammer.GetComponent<Animator>();
                if (anim != null) anim.SetBool("Hit", true);
                if (this.hammer.childCount > 1 && this.hammer.GetChild(1).GetComponent<ParticleSystem>() != null)
                {
                    var _ps = this.hammer.GetChild(1).GetComponent<ParticleSystem>();
                    var _em = _ps.emission; _em.enabled = false;
                }
                base.Invoke("PlayImpact", 0.4f);
            }
        }

        private void BaseClientCorrection()
        {
            base.transform.position = Vector3.Lerp(base.transform.position, this.correctObjectPos, 0.5f);
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.isWriting)
            {
                stream.SendNext(base.transform.position);
                stream.SendNext(base.transform.rotation);
            }
            else
            {
                this.correctObjectPos = (Vector3)stream.ReceiveNext();
                stream.ReceiveNext();
            }
        }
    }
}
