using UnityEngine;

namespace AxeElement
{
    /// <summary>
    /// Added at runtime to every wizard GameObject (via a Harmony patch on
    /// WizardController.Awake).  Because this component lives on the wizard's
    /// networked PhotonView, all [PunRPC] methods here are callable from any
    /// client using that wizard's PhotonView — regardless of which spell objects
    /// are (or aren't) present on the remote client.
    ///
    /// Each Axe spell that needs a remote visual calls:
    ///     wizardPv.RPC("rpc...", PhotonTargets.Others, args);
    /// and this component receives it on the other side.
    /// </summary>
    public class AxeNetworkBridge : MonoBehaviour
    {
        // ── Utility: orbiting glaives ─────────────────────────────────────────

        [PunRPC]
        public void rpcAxeGlaiveStart(int owner, float startAngle)
        {
            AxeUtility.SpawnGlaiveLocal(owner, startAngle);
        }

        // ── Ultimate: blood field ─────────────────────────────────────────────

        [PunRPC]
        public void rpcAxeFieldStart(int owner)
        {
            AxeUltimate.SpawnFieldLocal(owner);
        }

        [PunRPC]
        public void rpcAxeFieldDeath(int owner)
        {
            AxeUltimateObject.RemoteKill(owner);
        }

        // ── Melee: bleed / impact visuals ────────────────────────────────────

        [PunRPC]
        public void rpcAxeMeleeImpact(int owner, bool hit, int[] enemyOwnerIds)
        {
            AxeMeleeObject.RemoteImpact(owner, hit, enemyOwnerIds);
        }

        // ── Movement: cast sound ─────────────────────────────────────────────

        [PunRPC]
        public void rpcAxeMovementSound()
        {
            var sp = GetComponent<SoundPlayer>();
            if (sp != null)
                sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-cast", 5f);
        }
    }
}
