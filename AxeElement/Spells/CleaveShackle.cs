using System;
using DG.Tweening;
using UnityEngine;

namespace AxeElement
{
    public class CleaveShackle : SpellObject
    {
        // Inspector-assigned from Shackle Object prefab
        public Transform[] vineTransforms;
        public Transform attach;

        private const float INNER_LENGTH_SQ = 56.25f;
        private const float OUTER_LENGTH_SQ = 306.25f;
        private const float CHAIN_POWER_MAX = 5f;
        private const float CHAIN_POWER_M = 0.5f;

        private Transform ball;
        private Transform target;
        private PhysicsBody phys;
        private Transform ankle;
        private bool isAnkle;
        private bool dying;
        private bool started;

        public CleaveShackle()
        {
            DAMAGE = 3f;
            RADIUS = 3.5f;
            POWER = 2.5f;
            Y_POWER = 1.4f;
            START_TIME = 7f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (id == null) id = new Identity();
        }

        private void Start()
        {
            deathTimer = Time.time + START_TIME;
            if (this.vineTransforms != null)
                for (int i = 0; i < this.vineTransforms.Length; i++)
                    this.vineTransforms[i].parent = null;
        }

        public void Init(int owner, int enemyViewId, int ballViewId)
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.All,
                new object[] { owner, enemyViewId, ballViewId });
        }

        public void LocalInit(int owner, GameObject enemy, GameObject ballObj)
        {
            this.localSpellObjectStart(owner, enemy, ballObj);
        }

        private void Update()
        {
            if (this.dying) return;
            if (!this.started) return;
            if (this.ball == null || this.target == null || this.ankle == null)
            {
                SpellObjectDeath();
                return;
            }
            if (this.isAnkle)
                base.transform.position = this.ankle.position;
            else
                base.transform.position = this.ankle.position + Vector3.up;
            base.transform.rotation.SetLookRotation((this.ball.position - base.transform.position).WithY(0f), Vector3.up);
            this.PositionLine();
            if (deathTimer < Time.time)
                SpellObjectDeath();
        }

        private void FixedUpdate()
        {
            if (this.dying) return;
            if (base.photonView.IsMine() && this.ball != null && this.target != null && this.phys != null)
            {
                float force = this.GetForce((this.ball.position - this.target.position).sqrMagnitude);
                if (force > 0f)
                    this.phys.AddForce(GameUtility.GetForceVector(this.target.position, this.ball.position, force));
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
            this.vineTransforms[0].position = this.attach.position;
            this.vineTransforms[0].rotation = this.attach.rotation;
            this.vineTransforms[this.vineTransforms.Length - 1].position = this.ball.position;
        }

        public override void SpellObjectDeath()
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        [PunRPC]
        public void rpcSpellObjectStart(int owner, int enemyViewId, int ballViewId)
        {
            PhotonView enemyPv = PhotonView.Find(enemyViewId);
            GameObject enemy = (enemyPv != null) ? enemyPv.gameObject : null;
            PhotonView ballPv = PhotonView.Find(ballViewId);
            GameObject ballObj = (ballPv != null) ? ballPv.gameObject : null;
            this.localSpellObjectStart(owner, enemy, ballObj);
        }

        public void localSpellObjectStart(int owner, GameObject enemy, GameObject ballObj)
        {
            id.owner = owner;
            GameUtility.SetWizardColor(owner, base.gameObject, false);
            if (enemy == null) return;
            this.target = enemy.transform;
            UnitStatus us = this.target.GetComponent<UnitStatus>();
            if (us != null && us.ankle != null)
            {
                this.ankle = us.ankle.transform;
                this.isAnkle = true;
            }
            else
            {
                this.ankle = this.target;
            }
            this.phys = this.target.GetComponent<PhysicsBody>();
            if (ballObj != null)
                this.ball = ballObj.transform.GetChild(1);
            this.started = true;
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.dying = true;
            base.transform.DOScale(0f, 0.3f);
            if (this.vineTransforms != null)
                for (int i = 0; i < this.vineTransforms.Length; i++)
                    this.vineTransforms[i].DOScale(0f, 0.3f);
            UnityEngine.Object.Destroy(base.gameObject, 0.5f);
        }

        private void OnDestroy()
        {
            if (this.vineTransforms == null) return;
            for (int i = 0; i < this.vineTransforms.Length; i++)
            {
                Transform t = this.vineTransforms[i];
                if (t != null && t.gameObject != null)
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
        }
    }
}
