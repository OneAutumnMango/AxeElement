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

        private static void SpawnGlaive(Identity identity, float startAngle)
        {
            var go = GameUtility.Instantiate("Objects/Glaive", identity.transform.position, Quaternion.identity, 0);
            var original = go.GetComponent<GlaiveObject>();
            UnityEngine.Object impact = null;
            if (original != null)
                impact = original.impact;
            UnityEngine.Object.DestroyImmediate(original);
            var comp = go.AddComponent<AxeUtilityObject>();
            comp.impact = impact;
            comp.Init(identity, startAngle);
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
