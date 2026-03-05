using System;
using UnityEngine;

namespace AxeElement
{
    public class AxeUtility : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
            float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[AxeUtility] Initialize: owner={identity?.owner}");
            try
            {
                SpawnGlaive(identity, 0f);
                SpawnGlaive(identity, 180f);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AxeUtility] Initialize FAILED: {ex}");
            }
        }

        // ── Called on the CASTER's client ────────────────────────────────────
        private static void SpawnGlaive(Identity identity, float startAngle)
        {
            // Create locally (not via PhotonNetwork.Instantiate) so we fully
            // control what component runs on this client.
            SpawnGlaiveLocal(identity.owner, startAngle, isOwner: true);

            // Tell all other clients to create their own local copy.
            if (Globals.online)
            {
                var wc  = GameUtility.GetWizard(identity.owner);
                var pv  = wc?.GetComponent<PhotonView>();
                if (pv != null)
                    pv.RPC("rpcAxeGlaiveStart", PhotonTargets.Others,
                        new object[] { identity.owner, startAngle });
            }
        }

        // ── Called on EVERY client (caster directly, remote via wizard RPC) ──
        public static void SpawnGlaiveLocal(int owner, float startAngle, bool isOwner = false)
        {
            try
            {
                var wizGo = GameUtility.GetWizard(owner)?.gameObject;
                var pos   = wizGo?.transform.position ?? Vector3.zero;

                var go = (GameObject)UnityEngine.Object.Instantiate(
                    Resources.Load("Objects/Glaive", typeof(GameObject)), pos, Quaternion.identity);
                if (go == null) return;

                var original = go.GetComponent<GlaiveObject>();
                UnityEngine.Object impact = original?.impact;
                if (original != null) UnityEngine.Object.DestroyImmediate(original);

                var comp         = go.AddComponent<AxeUtilityObject>();
                comp.impact      = impact;
                comp.isOwnerClient = isOwner;
                comp.InitRemote(owner, wizGo, startAngle);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AxeUtility] SpawnGlaiveLocal FAILED: {ex}");
            }
        }

        public override Vector3? GetAiAim(TargetComponent targetComponent, Vector3 position,
            Vector3 target, SpellUses use, ref float curve, int owner)
        {
            return base.GetAiAim(targetComponent, position, target, use, ref curve, owner);
        }

        public override float GetAiRefresh(int owner)
        {
            return base.GetAiRefresh(owner);
        }

        public override bool AvailableOverride(AiController ai, int owner, SpellUses use, int reactivate)
        {
            return base.AvailableOverride(ai, owner, use, reactivate);
        }
    }
}
