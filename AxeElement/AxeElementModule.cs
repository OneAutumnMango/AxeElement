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
            PatchGroup(harmony, typeof(AxeElementsArrayGuardPatch));
            PatchGroup(harmony, typeof(AxeGetAvailableGuardPatch));
            PatchGroup(harmony, typeof(AxeAvailableElementsUnlockPatch));
            PatchGroup(harmony, typeof(AxeAvailableElementsPatch));
            PatchGroup(harmony, typeof(AxeSelectionMenuIconPatch));
            PatchGroup(harmony, typeof(AxeChangeElementPatch));
            PatchGroup(harmony, typeof(AxeSpellHandlerPatch));
            PatchGroup(harmony, typeof(AxeElementColorMappingPatch));
            PatchGroup(harmony, typeof(AxeVideoSpellPlayerPatch));
            PatchGroup(harmony, typeof(AxeSelectionMenuPatch));
            PatchGroup(harmony, typeof(AxeGetSpellDebugPatch));
            PatchGroup(harmony, typeof(AxePracticeRangeGuardPatch));
            PatchGroup(harmony, typeof(AxePlayerSelectionUpdatePatch));
        }

        protected override void OnUnload(Harmony harmony)
        {
            harmony.UnpatchSelf();
        }
    }
}
