using BepInEx;
using BepInEx.Logging;
using MageQuitModFramework.Modding;
using MageQuitModFramework.UI;

namespace AxeElement
{
    [BepInPlugin("com.magequit.axeelement", "Axe Element", "1.0.0")]
    [BepInDependency("com.magequit.modframework", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log;

        private ModuleManager _moduleManager;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo("Axe Element loading...");

            _moduleManager = ModManager.RegisterMod("Axe Element", "com.magequit.axeelement");
            _moduleManager.RegisterModule(new AxeElementModule());

            ModUIRegistry.RegisterMod(
                "Axe Element",
                "Adds a new Axe element featuring 7 unique spells",
                BuildModUI,
                priority: 10
            );

            Log.LogInfo("Axe Element loaded!");
        }

        private void BuildModUI() { }
    }
}
