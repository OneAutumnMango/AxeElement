using System;
using UnityEngine;

namespace AxeElement
{
    public class IronWard : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[IronWard] Initialize: owner={identity?.owner}, pos={position}, curve={curve}, spellIndex={spellIndex}");
            try
            {
                var go = GameUtility.Instantiate("Objects/Chainmail", position, rotation, 0);
                var original = go.GetComponent<ChainmailObject>();
                Transform _child = null;
                Transform[] _vineTransforms = null;
                Transform _attach = null;
                AnimationCurve _shieldCurve = null;
                ParticleSystem _sparks = null;
                if (original != null)
                {
                    _child = original.child;
                    _vineTransforms = original.vineTransforms;
                    _attach = original.attach;
                    _shieldCurve = original.shieldCurve;
                    _sparks = original.sparks;
                }
                Plugin.Log.LogInfo($"[IronWard] Prefab fields: child={_child != null}, vines={_vineTransforms?.Length ?? -1}, attach={_attach != null}, sparks={_sparks != null}");
                UnityEngine.Object.DestroyImmediate(original);
                var comp = go.AddComponent<IronWardObject>();
                comp.child = _child;
                comp.vineTransforms = _vineTransforms;
                comp.attach = _attach;
                comp.shieldCurve = _shieldCurve;
                comp.sparks = _sparks;
                comp.Init(identity);
                Plugin.Log.LogInfo("[IronWard] Spawned successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[IronWard] Initialize FAILED: {ex}");
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
            return use != SpellUses.Custom || ai.spellComponent.WillStillBeTakingDamageOverTime(this.windUp, 2f);
        }
    }
}
