using System;
using System.Reflection;

namespace AxeElement
{
    // ─────────────────────────────────────────────────────────────────────────
    // Replicates the extension methods from GameExtensions (internal in
    // Assembly-CSharp and therefore inaccessible from external assemblies).
    // Also provides reflection-based accessors for internal Globals fields.
    // ─────────────────────────────────────────────────────────────────────────
    public static class AxePhotonExtensions
    {
        // ── Globals.ai_event_handler (internal field) ─────────────────────
        private static readonly FieldInfo _aiField =
            typeof(Globals).GetField("ai_event_handler",
                BindingFlags.NonPublic | BindingFlags.Static);

        public static AiEventHandler AiEventHandler =>
            _aiField?.GetValue(null) as AiEventHandler;

        // ── PhotonView extension methods ───────────────────────────────────
        public static bool IsConnectedAndNotLocal(this PhotonView photonView)
        {
            return photonView != null && !photonView.isMine && PhotonNetwork.connected;
        }

        public static bool IsMine(this PhotonView photonView)
        {
            return !Globals.online || photonView == null || photonView.isMine
                || (photonView.isSceneView && PhotonNetwork.isMasterClient);
        }

        public static PhotonPlayer GetOwnerOrMaster(this PhotonView photonView)
        {
            if (photonView == null || photonView.isSceneView)
                return PhotonNetwork.masterClient;
            return photonView.owner;
        }

        public static void RPCLocal(this PhotonView photonView, object callingClass, string methodName, PhotonTargets target, params object[] parameters)
        {
            if (photonView != null && PhotonNetwork.connected)
            {
                photonView.RPC(methodName, target, parameters);
                return;
            }
            callingClass.GetType().GetMethod(methodName).Invoke(callingClass, parameters);
        }

        public static void RPCLocal(this PhotonView photonView, object callingClass, string methodName, PhotonPlayer player, params object[] parameters)
        {
            if (photonView != null && PhotonNetwork.connected)
            {
                photonView.RPC(methodName, player, parameters);
                return;
            }
            callingClass.GetType().GetMethod(methodName).Invoke(callingClass, parameters);
        }
    }
}
