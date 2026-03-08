using HarmonyLib;
using MageQuitModFramework.Data;
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

            // Register display names immediately so UI code (e.g. Boosted upgrade picker)
            // can show friendly names even before the first round loads.
            AxeRegistration.RegisterSpellDisplayNames();

            // Register spell object types early — before OnGameDataLoaded fires — so that
            // SpellModificationSystem.PatchAllSpellObjects (called by BoostedModule) finds
            // AxeXxxObject types regardless of which OnGameDataLoaded subscriber runs first.
            AxeRegistration.RegisterSpellObjectTypes();

            // Subscribe to the framework's game-data-loaded event so spell registration
            // always happens after GameDataInitializer has finished snapshotting the table
            // and initialising SpellModificationSystem. The event fires every round.
            GameEventsObserver.SubscribeToGameDataLoaded(OnGameDataLoaded);

            // If the game data is already loaded (e.g. hot-reload mid-session), run immediately.
            if (GameEventsObserver.IsGameDataLoaded)
                OnGameDataLoaded();
        }

        protected override void OnUnload(Harmony harmony)
        {
            GameEventsObserver.UnsubscribeFromGameDataLoaded(OnGameDataLoaded);
            harmony.UnpatchSelf();
        }

        private static void OnGameDataLoaded()
        {
            var manager = Globals.spell_manager;
            if (manager == null)
            {
                Plugin.Log.LogWarning("[AxeModule] OnGameDataLoaded: spell_manager is null, skipping registration");
                return;
            }

            var spellTable = manager.spell_table;
            if (spellTable == null)
            {
                Plugin.Log.LogWarning("[AxeModule] OnGameDataLoaded: spell_table is null, skipping registration");
                return;
            }

            AxeRegistration.RegisterSpells(manager, spellTable);
            AxeRegistration.RegisterAxeSpellsWithModSystem(spellTable);
        }
    }
}
