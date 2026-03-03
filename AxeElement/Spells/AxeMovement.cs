using System;
using UnityEngine;

namespace AxeElement
{
    public class AxeMovement : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
            float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[AxeMovement] Initialize: owner={identity?.owner}, spellIndex={spellIndex}");
            try
            {
                var go = GameUtility.Instantiate("Objects/Double Strike", position, rotation, 0);
                var original = go.GetComponent<DoubleStrikeObject>();
                UnityEngine.Object impact = null;
                if (original != null)
                    impact = original.impact;
                UnityEngine.Object.DestroyImmediate(original);
                var comp = go.AddComponent<AxeMovementObject>();
                comp.impact = impact;
                comp.Init(identity);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AxeMovement] Initialize FAILED: {ex}");
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
