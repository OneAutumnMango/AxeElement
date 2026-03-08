using BepInEx;
using BepInEx.Logging;
using MageQuitModFramework.Modding;
using MageQuitModFramework.UI;

namespace BloodElement
{
    [BepInPlugin("com.magequit.bloodelement", "Blood Element", "1.3.0")]
    [BepInDependency("com.magequit.modframework", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log;

        private ModuleManager _moduleManager;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo("Blood Element loading...");

            _moduleManager = ModManager.RegisterMod("Blood Element", "com.magequit.bloodelement");
            _moduleManager.RegisterModule(new BloodElementModule());

            ModUIRegistry.RegisterMod(
                "Blood Element",
                "Adds a new Blood element featuring 7 unique spells",
                BuildModUI,
                priority: 10
            );

            Log.LogInfo("Blood Element loaded!");
        }

        private void BuildModUI() { }
    }
}
