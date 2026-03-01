using System;
using UnityEngine;

namespace AxeElement
{
    public class AxePrimary : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[AxePrimary] Initialize: owner={identity?.owner}, pos={position}, curve={curve}, spellIndex={spellIndex}, curveM={this.curveMultiplier}, vel={this.initialVelocity}");
            try
            {
                var go = GameUtility.Instantiate("Objects/Reflex", position + Spell.skillshotOffset, rotation, 0);
                var original = go.GetComponent<ReflexObject>();
                UnityEngine.Object _impact = null;
                if (original != null)
                {
                    _impact = original.impact;
                }
                Plugin.Log.LogInfo($"[AxePrimary] Prefab fields: impact={_impact != null}");
                UnityEngine.Object.DestroyImmediate(original);
                var comp = go.AddComponent<AxePrimaryObject>();
                comp.impact = _impact;
                comp.Init(identity.owner, curve * this.curveMultiplier, this.initialVelocity);
                Plugin.Log.LogInfo($"[AxePrimary] Spawned successfully, effective curve={curve * this.curveMultiplier}, vel={this.initialVelocity}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxePrimary] Initialize FAILED: {ex}");
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
