using UnityEngine;

namespace BloodElement
{
    /// <summary>
    /// Added at runtime to every wizard GameObject (via a Harmony patch on
    /// WizardController.Awake).  Because this component lives on the wizard's
    /// networked PhotonView, all [PunRPC] methods here are callable from any
    /// client using that wizard's PhotonView — regardless of which spell objects
    /// are (or aren't) present on the remote client.
    ///
    /// Each Blood spell that needs a remote visual calls:
    ///     wizardPv.RPC("rpc...", PhotonTargets.Others, args);
    /// and this component receives it on the other side.
    /// </summary>
    public class BloodNetworkBridge : MonoBehaviour
    {
        // ── Utility: orbiting glaives ─────────────────────────────────────────

        [PunRPC]
        public void rpcBloodGlaiveStart(int owner, float startAngle)
        {
            BloodUtility.SpawnGlaiveLocal(owner, this.gameObject, startAngle);
        }

        // ── Ultimate: blood field ─────────────────────────────────────────────

        [PunRPC]
        public void rpcBloodFieldStart(int owner)
        {
            BloodUltimate.SpawnFieldLocal(owner, this.gameObject);
        }

        [PunRPC]
        public void rpcBloodFieldDeath(int owner)
        {
            BloodUltimateObject.RemoteKill(owner);
        }

        // ── Melee: bleed / impact visuals ────────────────────────────────────

        [PunRPC]
        public void rpcBloodMeleeImpact(int owner, bool hit, int[] enemyOwnerIds)
        {
            BloodMeleeObject.RemoteImpact(owner, hit, enemyOwnerIds);
        }

        // ── Movement: cast sound ─────────────────────────────────────────────

        [PunRPC]
        public void rpcBloodMovementSound()
        {
            var sp = GetComponent<SoundPlayer>();
            if (sp != null)
                sp.PlaySoundComponentInstantiate("event:/sfx/metal/glaive-cast", 5f);
        }
    }
}
