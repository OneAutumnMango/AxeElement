using System;
using UnityEngine;

namespace AxeElement
{
    /// <summary>
    /// Blood Field ultimate.
    /// Slams an axe into the ground to soak the area in a persistent crimson field:
    ///   - enemies inside are immediately bled and slowed
    ///   - field persists as long as enemies remain inside (up to 5 s)
    ///   - all damage you deal to bleeding targets heals you for 10 %
    /// </summary>
    public class AxeUltimate : Spell
    {
        public override void Initialize(
            Identity identity, Vector3 position, Quaternion rotation,
            float curve, int spellIndex, bool selfCast,
            SpellName spellNameForCooldown)
        {
            Plugin.Log.LogInfo($"[BloodField] Initialize: owner={identity?.owner}, pos={position}");
            try
            {
                // Objects/Push provides a clean networked SpellObject container
                // with PhotonView and SoundPlayer already wired up.
                var go       = GameUtility.Instantiate("Objects/Push", position, rotation, 0);
                var original = go.GetComponent<PushObject>();
                if (original != null)
                    UnityEngine.Object.DestroyImmediate(original);

                var comp = go.AddComponent<AxeUltimateObject>();
                comp.Init(identity);
                Plugin.Log.LogInfo("[BloodField] Spawned successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[BloodField] Initialize FAILED: {ex}");
            }
        }

        public override Vector3? GetAiAim(
            TargetComponent targetComponent, Vector3 position,
            Vector3 target, SpellUses use, ref float curve, int owner)
        {
            return base.GetAiAim(targetComponent, position, target, use, ref curve, owner);
        }

        public override float GetAiRefresh(int owner)
        {
            return base.GetAiRefresh(owner);
        }

        public override bool AvailableOverride(
            AiController ai, int owner, SpellUses use, int reactivate)
        {
            // Always available when off cooldown (use == Custom means "is the AI
            // still going to deal damage over time?"; for a field we always say yes)
            return true;
        }
    }
}
