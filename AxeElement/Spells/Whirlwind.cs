using System;
using PigeonCoopToolkit.Effects.Trails;
using UnityEngine;

namespace AxeElement
{
    public class Whirlwind : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[Whirlwind] Initialize: owner={identity?.owner}, pos={position}, curve={curve}, spellIndex={spellIndex}");
            try
            {
                var go = GameUtility.Instantiate("Objects/Double Strike", position, rotation, 0);
                var original = go.GetComponent<DoubleStrikeObject>();
                UnityEngine.Object _impact = null;
                ParticleSystem _distortionTrail = null;
                ParticleSystem _distortion = null;
                UnityEngine.Object _effect = null;
                ParticleSystem _effectStart = null;
                SmokeTrail _trail = null;
                if (original != null)
                {
                    _impact = original.impact;
                    _distortionTrail = original.distortionTrail;
                    _distortion = original.distortion;
                    _effect = original.effect;
                    _effectStart = original.effectStart;
                    _trail = original.trail;
                }
                Plugin.Log.LogInfo($"[Whirlwind] Prefab fields: impact={_impact != null}, trail={_trail != null}, distortion={_distortion != null}");
                UnityEngine.Object.DestroyImmediate(original);
                var comp = go.AddComponent<WhirlwindObject>();
                comp.impact = _impact;
                comp.distortionTrail = _distortionTrail;
                comp.distortion = _distortion;
                comp.effect = _effect;
                comp.effectStart = _effectStart;
                comp.trail = _trail;
                comp.Init(identity, curve);
                Plugin.Log.LogInfo("[Whirlwind] Spawned successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Whirlwind] Initialize FAILED: {ex}");
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
            return use != SpellUses.Custom || ai.spellComponent.WillStillBeDealingDamageOverTime(this.windUp);
        }
    }
}
