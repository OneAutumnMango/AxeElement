using System;
using System.Collections.Generic;
using UnityEngine;

namespace AxeElement
{
    public class AxeMeleeObject : SpellObject
    {
        public UnityEngine.Object impact;

        // Set by AxeRegistration after RegisterSpells
        public static UnityEngine.Object BleedEffectPrefab;

        private const int SOURCE_ID = 148; // (int)Axe.Cleave — registered in spell_table for kill feed

        public AxeMeleeObject()
        {
            DAMAGE = 7f;
            RADIUS = 3f;
            POWER = 20f;
            Y_POWER = 0f;
            START_TIME = 0f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (id == null) id = new Identity();
        }

        public void Init(Identity identity)
        {
            this.id.owner = identity.owner;

            Collider[] hits = GameUtility.GetAllInSphere(base.transform.position, RADIUS, identity.owner, new UnitType[1]);

            var viewIds = new List<int>();
            var enemies = new List<GameObject>();
            var seen = new System.Collections.Generic.HashSet<GameObject>();

            foreach (Collider col in hits)
            {
                GameObject go = col.transform.root.gameObject;
                if (!seen.Add(go)) continue;
                enemies.Add(go);
                viewIds.Add(go.GetPhotonView().viewID);
            }

            bool anyHit = enemies.Count > 0;

            int casterViewId = -1;
            if (identity != null && identity.gameObject != null)
            {
                PhotonView cpv = identity.gameObject.GetPhotonView();
                if (cpv != null) casterViewId = cpv.viewID;
            }

            if (Globals.online)
            {
                base.photonView.RPCLocal(this, "rpcImpact", PhotonTargets.All,
                    new object[] { identity.owner, anyHit, viewIds.ToArray(), casterViewId });
            }
            else
            {
                localImpact(identity.owner, anyHit, enemies.ToArray(), identity.gameObject);
            }

            UnityEngine.Object.Destroy(base.gameObject, 1.6f);
        }

        [PunRPC]
        public void rpcImpact(int owner, bool hit, int[] viewIds, int casterViewId)
        {
            var enemies = new GameObject[viewIds.Length];
            for (int i = 0; i < viewIds.Length; i++)
            {
                PhotonView pv = PhotonView.Find(viewIds[i]);
                enemies[i] = (pv != null) ? pv.gameObject : null;
            }
            PhotonView casterPv = PhotonView.Find(casterViewId);
            localImpact(owner, hit, enemies, (casterPv != null) ? casterPv.gameObject : null);
        }

        public void localImpact(int owner, bool hit, GameObject[] enemies, GameObject caster)
        {
            this.id.owner = owner;

            foreach (GameObject enemy in enemies)
            {
                if (enemy == null) continue;

                // Apply force + damage first (authoritative client only)
                // — bleed is not yet active, so the initial hit does NOT get the 10% bonus
                if (!Globals.online || !enemy.GetPhotonView().IsConnectedAndNotLocal())
                {
                    enemy.GetComponent<PhysicsBody>().AddForceOwner(
                        GameUtility.GetForceVector(base.transform.position, enemy.transform.position, POWER));
                    enemy.GetComponent<UnitStatus>().ApplyDamage(DAMAGE, owner, SOURCE_ID);
                }

                // Apply bleed AFTER damage, on ALL clients (visual parity)
                var enemyId = enemy.GetComponent<Identity>();
                if (enemyId != null)
                    BleedManager.ApplyBleed(enemyId.owner, enemy, BleedEffectPrefab);
            }

            if (hit)
            {
                if (sp != null)
                    sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-hit", 5f);
                if (impact != null)
                    UnityEngine.Object.Instantiate(impact, base.transform.position, Globals.sideways);
                if (caster != null && caster.GetComponent<SpellHandler>() != null)
                    caster.GetComponent<SpellHandler>().EndSpell();
            }
        }

        private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
        }
    }
}
