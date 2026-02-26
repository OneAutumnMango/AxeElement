using System;
using UnityEngine;

namespace AxeElement
{
    public class Lunge : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[Lunge] Initialize: owner={identity?.owner}, pos={position}, curve={curve}, spellIndex={spellIndex}, curveM={this.curveMultiplier}, vel={this.initialVelocity}");
            try
            {
                var go = GameUtility.Instantiate("Objects/Steal Trap", position, rotation, 0);

                // Copy inspector-assigned references from the prefab's StealTrapObject
                // before destroying it — these don't transfer to AddComponent automatically.
                var original = go.GetComponent<StealTrapObject>();
                Transform _attach = null, _wizardParticles = null, _targetEffects = null;
                Transform[] _vineTransforms = null;
                Animator _anim = null;
                float _multiplier = 2f;
                ParticleSystem _dustTrail = null, _trapClose = null, _loveSparkles = null, _gettinSlammed = null, _slam = null;
                if (original != null)
                {
                    _attach = original.attach;
                    _wizardParticles = original.wizardParticles;
                    _targetEffects = original.targetEffects;
                    _vineTransforms = original.vineTransforms;
                    _anim = original.anim;
                    _multiplier = original.multiplier;
                    _dustTrail = original.dustTrail;
                    _trapClose = original.trapClose;
                    _loveSparkles = original.loveSparkles;
                    _gettinSlammed = original.gettinSlammed;
                    _slam = original.slam;
                }
                Plugin.Log.LogInfo($"[Lunge] Prefab fields: attach={_attach != null}, wizardParticles={_wizardParticles != null}, vines={_vineTransforms?.Length ?? -1}, anim={_anim != null}");
                UnityEngine.Object.DestroyImmediate(original);

                LungeObject component = go.AddComponent<LungeObject>();
                component.attach = _attach;
                component.wizardParticles = _wizardParticles;
                component.targetEffects = _targetEffects;
                component.vineTransforms = _vineTransforms;
                component.anim = _anim;
                component.multiplier = _multiplier;
                component.dustTrail = _dustTrail;
                component.trapClose = _trapClose;
                component.loveSparkles = _loveSparkles;
                component.gettinSlammed = _gettinSlammed;
                component.slam = _slam;
                if (spellIndex < 0)
                    component.Init(identity, curve * this.curveMultiplier, this.initialVelocity, spellIndex, spellNameForCooldown);
                else
                    component.Init(identity, curve * this.additionalCasts[0].curveMultiplier, this.additionalCasts[0].initialVelocity, spellIndex, spellNameForCooldown);
                Plugin.Log.LogInfo($"[Lunge] Spawned successfully, spellIndex={spellIndex}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Lunge] Initialize FAILED: {ex}");
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
