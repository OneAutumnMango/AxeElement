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
            PatchGroup(harmony, typeof(AxeSpellManagerPatch));
            PatchGroup(harmony, typeof(AxeWizardStatusPatch));
            PatchGroup(harmony, typeof(AxeGameSettingsPatch));
            PatchGroup(harmony, typeof(AxeElementColorMappingPatch));
            PatchGroup(harmony, typeof(AxeVideoSpellPlayerPatch));
            PatchGroup(harmony, typeof(AxeSelectionMenuPatch));
        }

        protected override void OnUnload(Harmony harmony)
        {
            harmony.UnpatchSelf();
        }
    }
}
