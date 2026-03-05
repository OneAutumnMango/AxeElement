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

        private const int SOURCE_ID = 148; // (int)Axe.AxeMelee — registered in spell_table for kill feed

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

        // ── Called on the CASTER's client; returns enemy owner IDs for wizard RPC ──
        public int[] InitLocal(Identity identity)
        {
            this.id.owner = identity.owner;

            Collider[] hits = GameUtility.GetAllInSphere(base.transform.position, RADIUS, identity.owner, new UnitType[1]);

            var ownerIds = new List<int>();
            var enemies  = new List<GameObject>();
            var seen     = new HashSet<GameObject>();

            foreach (Collider col in hits)
            {
                GameObject go = col.transform.root.gameObject;
                if (!seen.Add(go)) continue;
                var eid = go.GetComponent<Identity>() ?? go.GetComponentInParent<Identity>();
                if (eid == null) continue;
                enemies.Add(go);
                ownerIds.Add(eid.owner);
            }

            localImpact(identity.owner, enemies.Count > 0, enemies.ToArray(), identity.gameObject);
            UnityEngine.Object.Destroy(base.gameObject, 1.6f);
            return ownerIds.ToArray();
        }

        // ── Called on remote clients by AxeNetworkBridge.rpcAxeMeleeImpact ────────
        public static void RemoteImpact(int owner, bool hit, int[] enemyOwnerIds)
        {
            var casterPos = GameUtility.GetWizard(owner)?.transform.position ?? Vector3.zero;
            foreach (int eid in enemyOwnerIds)
            {
                var enemyGo = GameUtility.GetWizard(eid)?.gameObject;
                if (enemyGo == null) continue;

                // Apply bleed visual on ALL clients
                BleedManager.ApplyBleed(eid, enemyGo, BleedEffectPrefab);

                // Force + damage only on the enemy's own client
                if (Globals.online && enemyGo.GetPhotonView().IsConnectedAndNotLocal()) continue;
                enemyGo.GetComponent<PhysicsBody>()?.AddForceOwner(
                    GameUtility.GetForceVector(casterPos, enemyGo.transform.position, 20f));
                enemyGo.GetComponent<UnitStatus>()?.ApplyDamage(7f, owner, SOURCE_ID);
            }
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
            // GO is local-only; no Photon serialization needed.
        }
    }
}
