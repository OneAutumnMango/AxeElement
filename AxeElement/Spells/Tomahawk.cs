using System;
using UnityEngine;

namespace AxeElement
{
    public class Tomahawk : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            var go = GameUtility.Instantiate("Objects/Urchain", position + Spell.skillshotOffset, rotation, 0);
            var original = go.GetComponent<UrchainObject>();
            Transform[] _vt1 = null;
            Transform[] _vt2 = null;
            Animator _anim = null;
            ParticleSystem[] _ps = null;
            UnityEngine.Object _impact = null;
            if (original != null)
            {
                _vt1 = original.vineTransforms1;
                _vt2 = original.vineTransforms2;
                _anim = original.anim;
                _ps = original.ps;
                _impact = original.impact;
            }
            UnityEngine.Object.DestroyImmediate(original);
            var comp = go.AddComponent<TomahawkObject>();
            comp.vineTransforms1 = _vt1;
            comp.vineTransforms2 = _vt2;
            comp.anim = _anim;
            comp.ps = _ps;
            comp.impact = _impact;
            comp.Init(identity.owner, curve * this.curveMultiplier, this.initialVelocity);
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
