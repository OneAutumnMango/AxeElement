using HarmonyLib;
using MageQuitModFramework.Modding;

namespace AxeElement
{
    public class AxeElementModule : BaseModule
    {
        public override string ModuleName => "Axe Element";

        protected override void OnLoad(Harmony harmony)
        {
            AxeElementPatches.Initialize();
            PatchGroup(harmony, typeof(AxeElementPatches));
        }

        protected override void OnUnload(Harmony harmony)
        {
            harmony.UnpatchSelf();
        }
    }
}
