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
                SpawnFieldLocal(identity.owner, identity.gameObject, isOwner: true);

                if (Globals.online)
                {
                    var pv = identity.gameObject?.GetComponent<PhotonView>();
                    if (pv != null)
                        pv.RPC("rpcAxeFieldStart", PhotonTargets.Others,
                            new object[] { identity.owner });
                }

                Plugin.Log.LogInfo("[BloodField] Spawned successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[BloodField] Initialize FAILED: {ex}");
            }
        }

        // ── Called on EVERY client (caster directly, remote via wizard RPC) ──
        public static void SpawnFieldLocal(int owner, GameObject wizGo = null, bool isOwner = false)
        {
            try
            {
                // Fallback to GameUtility.GetWizard if wizGo is null (for RPC calls without GameObject)
                if (wizGo == null)
                    wizGo = GameUtility.GetWizard(owner)?.gameObject;
                var pos   = wizGo?.transform.position ?? Vector3.zero;

                var go = (GameObject)UnityEngine.Object.Instantiate(
                    Resources.Load("Objects/Push", typeof(GameObject)), pos, Quaternion.identity);
                if (go == null) return;

                var original = go.GetComponent<PushObject>();
                if (original != null) UnityEngine.Object.DestroyImmediate(original);

                var comp = go.AddComponent<AxeUltimateObject>();
                comp.isOwnerClient = isOwner;
                comp.InitLocal(owner, wizGo);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[BloodField] SpawnFieldLocal FAILED: {ex}");
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
            // Always available when off cooldown
            return true;
        }
    }
}
