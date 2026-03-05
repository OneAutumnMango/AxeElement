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
                var go = GameUtility.Instantiate("Objects/Push", position + rotation * Vector3.forward * 4f, rotation, 0);
                var original = go.GetComponent<PushObject>();
                UnityEngine.Object _impact = null;
                if (original != null)
                    _impact = original.impact;
                UnityEngine.Object.DestroyImmediate(original);
                var comp = go.AddComponent<AxeMeleeObject>();
                comp.impact = _impact;
                comp.Init(identity);
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
