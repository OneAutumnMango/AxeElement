using System;
using UnityEngine;

namespace AxeElement
{
    public class Lunge : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            var go = GameUtility.Instantiate("Objects/Steal Trap", position, rotation, 0);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<StealTrapObject>());
            LungeObject component = go.AddComponent<LungeObject>();
            if (spellIndex < 0)
                component.Init(identity, curve * this.curveMultiplier, this.initialVelocity, spellIndex, spellNameForCooldown);
            else
                component.Init(identity, curve * this.additionalCasts[0].curveMultiplier, this.additionalCasts[0].initialVelocity, spellIndex, spellNameForCooldown);
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
            if (use == SpellUses.Move && !ai.GetComponent<WizardStatus>().onLava)
            {
                Vector3? aim = ai.spellComponent.aim;
                if (aim == null) return false;
                RaycastHit hit;
                if (!Physics.Raycast(ai.transform.position + aim.Value.normalized * 14f + Vector3.up * 3f, Vector3.down, out hit, 30f, 256))
                    return false;
                if (hit.collider.transform.root.CompareTag("Lava"))
                    return false;
            }
            return true;
        }
    }
}
