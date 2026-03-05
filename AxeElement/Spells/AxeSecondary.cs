using System;
using UnityEngine;

namespace AxeElement
{
    public class AxeSecondary : Spell
    {
        // Degrees each axe is offset left/right from the aim direction.
        private const float SPREAD_ANGLE = 35f;

        // Degrees per FixedUpdate (50 Hz default) that each axe curves inward.
        // At 50 Hz, 1.5°/frame × 50 frames (1.0 s) ≈ 75° total arc, which
        // sweeps each axe back from the spread angle to center over the flight.
        private const float ARC_RATE = 1.5f;

        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation, float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[AxeSecondary] Initialize: owner={identity?.owner}, pos={position}, vel={this.initialVelocity}");
            try
            {
                float vel = this.initialVelocity;
                int owner = identity.owner;

                // ── Left axe: offset +SPREAD_ANGLE, arcs right (inward) ──
                Quaternion leftRot = rotation * Quaternion.Euler(0f, SPREAD_ANGLE, 0f);
                SpawnAxe(owner, position, leftRot, -ARC_RATE, vel);

                // ── Right axe: offset -SPREAD_ANGLE, arcs left (inward) ──
                Quaternion rightRot = rotation * Quaternion.Euler(0f, -SPREAD_ANGLE, 0f);
                SpawnAxe(owner, position, rightRot, +ARC_RATE, vel);

                Plugin.Log.LogInfo("[AxeSecondary] Both axes spawned successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AxeSecondary] Initialize FAILED: {ex}");
            }
        }

        private static void SpawnAxe(int owner, Vector3 position, Quaternion rotation, float arcRate, float velocity)
        {
            var go = GameUtility.Instantiate("Objects/Reflex", position + Spell.skillshotOffset, rotation, 0);
            var original = go.GetComponent<ReflexObject>();
            UnityEngine.Object _impact = null;
            if (original != null)
                _impact = original.impact;
            UnityEngine.Object.DestroyImmediate(original);
            var comp = go.AddComponent<AxeSecondaryObject>();
            comp.impact = _impact;
            comp.Init(owner, arcRate, velocity);
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
