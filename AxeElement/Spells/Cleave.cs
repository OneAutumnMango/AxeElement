using System;
using UnityEngine;

namespace AxeElement
{
    public class Cleave : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[Cleave] Initialize: owner={identity?.owner}, pos={position}, curve={curve}, spellIndex={spellIndex}");
            try
            {
                var go = GameUtility.Instantiate("Objects/Shackle", position + rotation * Vector3.forward * 4f, rotation, 0);
                var original = go.GetComponent<TetherballObject>();
                UnityEngine.Object _impact = null;
                Transform _ball = null;
                float _rollSpeed = 1f;
                if (original != null)
                {
                    _impact = original.impact;
                    _ball = original.ball;
                    _rollSpeed = original.rollSpeed;
                }
                Plugin.Log.LogInfo($"[Cleave] Prefab fields: impact={_impact != null}, ball={_ball != null}, rollSpeed={_rollSpeed}");
                UnityEngine.Object.DestroyImmediate(original);
                var comp = go.AddComponent<CleaveObject>();
                comp.impact = _impact;
                comp.ball = _ball;
                comp.rollSpeed = _rollSpeed;
                comp.Init(identity);
                Plugin.Log.LogInfo("[Cleave] Spawned successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Cleave] Initialize FAILED: {ex}");
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
