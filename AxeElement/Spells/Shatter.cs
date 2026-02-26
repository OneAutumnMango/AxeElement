using System;
using UnityEngine;

namespace AxeElement
{
    public class Shatter : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            var go = GameUtility.Instantiate("Objects/Reflex", position + Spell.skillshotOffset, rotation, 0);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<ReflexObject>());
            go.AddComponent<ShatterObject>().Init(identity.owner, curve * this.curveMultiplier, this.initialVelocity);
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
