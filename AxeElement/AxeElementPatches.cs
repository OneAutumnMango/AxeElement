using System.Collections.Generic;
using System.Reflection;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using MageQuitModFramework.Data;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

namespace AxeElement
{
    // ─────────────────────────────────────────────────────────────────────────
    // Constant aliases for the Axe element and its seven SpellName values.
    // Element.Ice == 10 is reused as the Axe slot (replaces Ice entirely).
    // SpellNames 146-152 are appended after ColdFusion (145).
    // ─────────────────────────────────────────────────────────────────────────
    public static class Axe
    {
        public static readonly Element Element = (Element)10; // same integer as Element.Ice

        public static readonly SpellName Hatchet   = (SpellName)146;
        public static readonly SpellName Lunge     = (SpellName)147;
        public static readonly SpellName Cleave    = (SpellName)148;
        public static readonly SpellName Tomahawk  = (SpellName)149;
        public static readonly SpellName IronWard  = (SpellName)150;
        public static readonly SpellName Shatter   = (SpellName)151;
        public static readonly SpellName Whirlwind = (SpellName)152;
    }

    [HarmonyPatch]
    public static class AxeElementPatches
    {
        public static void Initialize()
        {
            // One-time setup (called from AxeElementModule.OnLoad)
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SpellManager.Awake — register all 7 Axe spells after the vanilla
    // spells have been populated, and replace Ice spells in the element slot.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpellManager), "Awake")]
    public static class AxeSpellManagerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SpellManager __instance)
        {
            // Only register on the canonical SpellManager (DontDestroyOnLoad).
            // Duplicate instances self-destruct before populating spell_table.
            if (Globals.spell_manager != null && Globals.spell_manager != __instance)
            {
                Plugin.Log.LogInfo("[AxePatch] Skipping duplicate SpellManager instance.");
                return;
            }

            var spellTable = Traverse.Create(__instance)
                .Field("spell_table")
                .GetValue<Dictionary<SpellName, Spell>>();

            AxeRegistration.RegisterSpells(__instance, spellTable);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WizardStatus.rpcApplyDamage — notify IronWard and Whirlwind objects
    // whenever the wizard takes damage (mirrors chainmail + DoubleStrike hooks).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(WizardStatus), "rpcApplyDamage")]
    public static class AxeWizardStatusPatch
    {
        [HarmonyPostfix]
        public static void Postfix(WizardStatus __instance, float damage, int owner, int source)
        {
            try
            {
                IronWardObject.NotifyDamage(owner, damage, __instance);
                WhirlwindObject.NotifyDamage(owner, damage, __instance as UnitStatus);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeDmg] rpcApplyDamage hook failed: damage={damage}, owner={owner}, source={source}: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GameSettings constructor — ensure elements array is large enough.
    // Since Axe reuses the Ice slot (10) no expansion is needed, but we
    // keep this patch for safety in case the array is shorter than expected.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(GameSettings), MethodType.Constructor)]
    public static class AxeGameSettingsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameSettings __instance)
        {
            int needed = AvailableElements.unlockOrder != null
                ? AvailableElements.unlockOrder.Length
                : 11;
            if (__instance.elements != null && __instance.elements.Length < needed)
            {
                var expanded = new ElementInclusionMode[needed];
                __instance.elements.CopyTo(expanded, 0);
                __instance.elements = expanded;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ElementColorMapping.Start — Override Ice stage visuals with steel/grey.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ElementColorMapping), "Start")]
    public static class AxeElementColorMappingPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ElementColorMapping __instance)
        {
            if (__instance.element != Axe.Element)
                return;

            // Override post-processing with steel/grey theme
            Bloom bloom = null;
            Vignette vignette = null;

            var profileField = typeof(ElementColorMapping).GetField("profile",
                BindingFlags.Public | BindingFlags.Instance);
            if (profileField == null) return;

            var profile = profileField.GetValue(__instance) as PostProcessProfile;
            if (profile == null) return;

            profile.TryGetSettings(out bloom);
            profile.TryGetSettings(out vignette);

            if (bloom != null)
                bloom.intensity.value = 2.0f;
            if (vignette != null)
            {
                vignette.color.value = new Color(0.45f, 0.45f, 0.50f);
                vignette.intensity.value = 0.35f;
            }

            // Replace ambient sound: fade out ice, fade in metal/fire as placeholder
            var ambientField = typeof(ElementColorMapping).GetField("ambientInstance",
                BindingFlags.Public | BindingFlags.Static);
            if (ambientField != null)
            {
                var existing = (EventInstance)ambientField.GetValue(null);
                if (existing.isValid())
                    existing.FadeSoundOut(0f, 0.5f, 0f);

                var newInstance = RuntimeManager.CreateInstance("event:/sfx/ambience/fire");
                newInstance.FadeSoundIn(0f, 2f, 0f);
                newInstance.start();
                ambientField.SetValue(null, newInstance);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VideoSpellPlayer — Override draft UI colors for element index 10.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(VideoSpellPlayer), "SlideIn")]
    public static class AxeVideoSpellPlayerPatch
    {
        [HarmonyPrefix]
        public static void Prefix(VideoSpellPlayer __instance)
        {
            // Override darkColors[10] and lightColors[10] with steel/grey
            var darkField = typeof(VideoSpellPlayer).GetField("darkColors",
                BindingFlags.Public | BindingFlags.Instance);
            var lightField = typeof(VideoSpellPlayer).GetField("lightColors",
                BindingFlags.Public | BindingFlags.Instance);

            if (darkField != null)
            {
                var darkColors = darkField.GetValue(__instance) as Color[];
                if (darkColors != null && darkColors.Length > 10)
                    darkColors[10] = new Color(0.3f, 0.3f, 0.35f);
            }

            if (lightField != null)
            {
                var lightColors = lightField.GetValue(__instance) as Color[];
                if (lightColors != null && lightColors.Length > 10)
                    lightColors[10] = new Color(0.7f, 0.7f, 0.75f);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SelectionMenu.ShowElementTooltip — Replace "Ice" name with "Axe".
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "ShowElementTooltip")]
    public static class AxeSelectionMenuPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SelectionMenu __instance)
        {
            // The tooltip text field displays the element name from ToString().
            // Replace "Ice" with "Axe" in whatever text was set.
            var tooltipField = typeof(SelectionMenu).GetField("tooltipText",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (tooltipField != null)
            {
                var textObj = tooltipField.GetValue(__instance);
                if (textObj is Text uiText && uiText.text != null)
                    uiText.text = uiText.text.Replace("Ice", "Axe");
            }

            // Also try alternative field name
            var altField = typeof(SelectionMenu).GetField("tooltip",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (altField != null)
            {
                var textObj = altField.GetValue(__instance);
                if (textObj is Text uiText && uiText.text != null)
                    uiText.text = uiText.text.Replace("Ice", "Axe");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEBUG: Trace GetSpellByRoundAndElement to diagnose wrong element display.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(GameUtility), "GetSpellByRoundAndElement", typeof(Element), typeof(int))]
    public static class AxeGetSpellDebugPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Element el, int round, Spell __result)
        {
            if (el == Axe.Element)
            {
                if (__result != null)
                    Plugin.Log.LogInfo($"[AxeDbg] GetSpellByRoundAndElement(Ice/Axe, round={round}) => {__result.spellName} el={__result.element} btn={__result.spellButton}");
                else
                    Plugin.Log.LogWarning($"[AxeDbg] GetSpellByRoundAndElement(Ice/Axe, round={round}) => NULL");
            }
        }
    }
}
