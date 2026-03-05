using System;
using UnityEngine;

namespace AxeElement
{
    public class AxeMelee : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[AxeMelee] Initialize: owner={identity?.owner}, pos={position}");
            try
            {
                var spawnPos = position + rotation * Vector3.forward * 4f;
                var go = (GameObject)UnityEngine.Object.Instantiate(
                    Resources.Load("Objects/Push", typeof(GameObject)), spawnPos, rotation);
                if (go == null) return;

                var original = go.GetComponent<PushObject>();
                UnityEngine.Object _impact = original?.impact;
                if (original != null) UnityEngine.Object.DestroyImmediate(original);

                var comp = go.AddComponent<AxeMeleeObject>();
                comp.impact = _impact;
                int[] enemyOwnerIds = comp.InitLocal(identity);

                if (Globals.online && enemyOwnerIds != null)
                {
                    var pv = GameUtility.GetWizard(identity.owner)?.GetComponent<PhotonView>();
                    if (pv != null)
                        pv.RPC("rpcAxeMeleeImpact", PhotonTargets.Others,
                            new object[] { identity.owner, enemyOwnerIds.Length > 0, enemyOwnerIds });
                }

                Plugin.Log.LogInfo("[AxeMelee] Spawned successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeMelee] Initialize FAILED: {ex}");
            }
        }

        public override Vector3? GetAiAim(TargetComponent targetComponent, Vector3 position, Vector3 target, SpellUses use, ref float curve, int owner)
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
