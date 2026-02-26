using System;
using System.Collections.Generic;
using DG.Tweening;
using FMOD.Studio;
using UnityEngine;

namespace AxeElement
{
    public class CleaveObject : SpellObject
    {
        // Inspector-assigned from Shackle prefab
        public UnityEngine.Object impact;
        public Transform ball;
        public float rollSpeed = 1f;

        private bool dying;
        private Vector3 lastPos = Vector3.zero;
        private EventInstance aSource;
        private float volume;

        public CleaveObject()
        {
            DAMAGE = 7f;
            RADIUS = 3f;
            POWER = 20f;
            START_TIME = 7f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (id == null) id = new Identity();
        }

        private void Start()
        {
            if (this.ball != null)
                this.ball.localScale = Vector3.zero;
            deathTimer = Time.time + START_TIME;
            this.lastPos = base.transform.position;
            if (sp != null)
                this.aSource = sp.PlaySound("event:/sfx/metal/tetherball-roll").SetVolume(0f).SetPitch(1f);
        }

        public void Init(Identity identity)
        {
            id.owner = 0;
            Collider[] allInSphere = GameUtility.GetAllInSphere(base.transform.position, RADIUS, identity.owner, new UnitType[1]);
            bool hit = allInSphere.Length > 0;
            List<int> viewIds = new List<int>();
            List<GameObject> enemies = new List<GameObject>();
            foreach (Collider col in allInSphere)
            {
                GameObject go = col.transform.root.gameObject;
                enemies.Add(go);
                viewIds.Add(go.GetPhotonView().viewID);
            }
            if (Globals.online)
            {
                int? casterViewId = null;
                if (identity != null && identity.gameObject != null)
                {
                    PhotonView pv = identity.gameObject.GetPhotonView();
                    casterViewId = (pv != null) ? new int?(pv.viewID) : null;
                }
                base.photonView.RPCLocal(this, "rpcImpact", PhotonTargets.All,
                    new object[] { identity.owner, hit, viewIds.ToArray(), casterViewId ?? (-1) });
            }
            else
            {
                this.localImpact(identity.owner, hit, enemies.ToArray(), null,
                    (identity != null) ? identity.gameObject : null, -1);
            }
        }

        private void Update()
        {
            Vector3 delta = base.transform.position - this.lastPos;
            if (delta.sqrMagnitude > 0.1f * Time.deltaTime)
            {
                base.transform.LookAt(base.transform.position + delta, Vector3.up);
                if (this.ball != null)
                    this.ball.rotation *= Quaternion.Euler(new Vector3(delta.magnitude * this.rollSpeed, 0f, 0f));
                this.volume += Time.deltaTime * 4f;
                if (this.volume > 1f) this.volume = 1f;
            }
            else
            {
                this.volume -= Time.deltaTime * 8f;
                if (this.volume < 0f) this.volume = 0f;
            }
            if (this.aSource.isValid())
                this.aSource.SetVolume(this.volume);
            this.lastPos = base.transform.position;
            if (deathTimer < Time.time && !this.dying)
                SpellObjectDeath();
        }

        public override void SpellObjectDeath()
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, Array.Empty<object>());
        }

        [PunRPC]
        public void rpcImpact(int owner, bool hit, int[] viewIds, int casterViewId)
        {
            GameObject[] enemies = new GameObject[viewIds.Length];
            for (int i = 0; i < viewIds.Length; i++)
            {
                PhotonView pv = PhotonView.Find(viewIds[i]);
                enemies[i] = (pv != null) ? pv.gameObject : null;
            }
            PhotonView casterPv = PhotonView.Find(casterViewId);
            this.localImpact(owner, hit, enemies, viewIds,
                (casterPv != null) ? casterPv.gameObject : null, casterViewId);
        }

        public void localImpact(int owner, bool hit, GameObject[] enemies, int[] viewIds, GameObject caster, int casterViewId)
        {
            id.owner = 0;
            GameUtility.SetWizardColor(owner, base.gameObject, false);
            int ballViewId = -1;
            if (Globals.online && base.photonView != null)
                ballViewId = base.photonView.viewID;

            for (int i = 0; i < enemies.Length; i++)
            {
                GameObject enemy = enemies[i];
                if (enemy == null) continue;
                if (Globals.online && enemy.GetPhotonView().IsConnectedAndNotLocal()) continue;

                enemy.GetComponent<PhysicsBody>().AddForce(
                    GameUtility.GetForceVector(base.transform.position, enemy.transform.position, POWER));
                enemy.GetComponent<UnitStatus>().ApplyDamage(DAMAGE, owner, 58);

                GameObject shackleGo = GameUtility.Instantiate("Objects/Shackle Object",
                    enemy.transform.position,
                    Quaternion.LookRotation((enemy.transform.position - base.transform.position).WithY(0f), Vector3.up), 0);
                UnityEngine.Object.DestroyImmediate(shackleGo.GetComponent<TetherballObjectObject>());
                CleaveShackle shackle = shackleGo.AddComponent<CleaveShackle>();
                if (Globals.online)
                    shackle.Init(owner, viewIds[i], ballViewId);
                else
                    shackle.LocalInit(owner, enemy, base.gameObject);
            }

            if (hit)
            {
                if (this.ball != null)
                    this.ball.DOScale(14.36213f, 0.3f).SetEase(Ease.InOutCubic);
                SphereCollider sc = base.GetComponent<SphereCollider>();
                if (sc != null) sc.enabled = true;
                if (sp != null)
                    sp.PlaySoundComponentInstantiate("event:/sfx/metal/tetherball-cast", 5f);
                if (this.impact != null)
                    UnityEngine.Object.Instantiate(this.impact, base.transform.position, Globals.sideways);
                if (caster != null && caster.GetComponent<SpellHandler>() != null)
                    caster.GetComponent<SpellHandler>().EndSpell();
            }
            else
            {
                this.rpcSpellObjectDeath();
            }
        }

        [PunRPC]
        public void rpcSpellObjectDeath()
        {
            this.dying = true;
            if (this.ball != null)
                this.ball.DOScale(0f, 0.3f);
            SphereCollider sc = base.GetComponent<SphereCollider>();
            if (sc != null) sc.enabled = false;
            UnityEngine.Object.Destroy(base.gameObject, 0.5f);
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
        }
    }
}
