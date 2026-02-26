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
    // Element.Tutorial == 11 is used as the Axe slot. Ice stays vanilla.
    // SpellNames 146-152 are appended after ColdFusion (145).
    // ─────────────────────────────────────────────────────────────────────────
    public static class Axe
    {
        public static readonly Element Element = (Element)11; // Tutorial slot

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
            // Expand unlockOrder to include Axe (Tutorial element) as the 11th entry.
            // This runs at mod load, before any Unity lifecycle methods.
            if (AvailableElements.unlockOrder != null && AvailableElements.unlockOrder.Length <= 10)
            {
                var expanded = new Element[11];
                AvailableElements.unlockOrder.CopyTo(expanded, 0);
                expanded[10] = Axe.Element;
                AvailableElements.unlockOrder = expanded;
                Plugin.Log.LogInfo("[AxeInit] Expanded unlockOrder to 11 elements (added Axe at index 10)");
            }

            // Inject Axe element into SpellHandler's static sound dictionaries.
            // Both map Element → FMOD event path; all vanilla elements use the same path.
            try
            {
                var castField = typeof(SpellHandler).GetField("castSounds",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (castField != null)
                {
                    var dict = castField.GetValue(null) as Dictionary<Element, string>;
                    if (dict != null && !dict.ContainsKey(Axe.Element))
                    {
                        dict[Axe.Element] = "event:/sfx/wizard/spell-attack";
                        Plugin.Log.LogInfo("[AxeInit] Injected Axe into SpellHandler.castSounds");
                    }
                }

                var ultField = typeof(SpellHandler).GetField("ultimateCastSounds",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (ultField != null)
                {
                    var dict = ultField.GetValue(null) as Dictionary<Element, string>;
                    if (dict != null && !dict.ContainsKey(Axe.Element))
                    {
                        dict[Axe.Element] = "event:/sfx/wizard/spell-attack";
                        Plugin.Log.LogInfo("[AxeInit] Injected Axe into SpellHandler.ultimateCastSounds");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeInit] Failed to inject SpellHandler sound dictionaries: {ex}");
            }
        }

        /// <summary>
        /// Ensures GameSettings.elements array is large enough for all unlockOrder entries,
        /// and bumps LastUnlockedIndex so vanilla .Take(LastUnlockedIndex+5) thresholds
        /// include Axe at index 10.
        /// Called from multiple guard patches to prevent IndexOutOfRange when presets
        /// reset the array to size 10.
        /// </summary>
        public static void EnsureElementsArraySize()
        {
            if (PlayerManager.gameSettings != null &&
                PlayerManager.gameSettings.elements != null &&
                PlayerManager.gameSettings.elements.Length < AvailableElements.unlockOrder.Length)
            {
                var expanded = new ElementInclusionMode[AvailableElements.unlockOrder.Length];
                PlayerManager.gameSettings.elements.CopyTo(expanded, 0);
                PlayerManager.gameSettings.elements = expanded;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SpellManager.Awake — register all 7 Axe spells after the vanilla
    // spells have been populated.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpellManager), "Awake")]
    public static class AxeSpellManagerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SpellManager __instance)
        {
            var spellTable = Traverse.Create(__instance)
                .Field("spell_table")
                .GetValue<Dictionary<SpellName, Spell>>();

            AxeRegistration.RegisterSpells(__instance, spellTable);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WizardStatus.rpcApplyDamage — notify IronWard and Whirlwind objects
    // whenever the wizard takes damage.
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
    // GameSettings constructor — ensure elements array is large enough for
    // 11 elements (Axe occupies the Tutorial slot at index 10).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(GameSettings), MethodType.Constructor)]
    public static class AxeGameSettingsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameSettings __instance)
        {
            AxeElementPatches.EnsureElementsArraySize();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Guard: SelectionMenu.ShowElements — ensure arrays are correct size.
    // Prefix: expand GameSettings.elements; skip if Image array not ready.
    // Postfix: force-unlock Axe (index 10) past LastUnlockedIndex threshold.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "ShowElements")]
    public static class AxeElementsArrayGuardPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(SelectionMenu __instance)
        {
            AxeElementPatches.EnsureElementsArraySize();

            // If SelectionMenu's Image array hasn't been expanded to 11 yet
            // (Start prefix hasn't run), skip ShowElements to avoid crash
            var elementsField = typeof(SelectionMenu).GetField("elements",
                BindingFlags.Public | BindingFlags.Instance);
            if (elementsField != null)
            {
                var elements = elementsField.GetValue(__instance) as Image[];
                if (elements != null && elements.Length < AvailableElements.unlockOrder.Length)
                    return false; // Skip — will run correctly once Start expands the array
            }

            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(SelectionMenu __instance)
        {
            // Force unlock Axe (index 10) — the vanilla condition
            // i < LastUnlockedIndex + 5 excludes our modded element
            var elementsField = typeof(SelectionMenu).GetField("elements",
                BindingFlags.Public | BindingFlags.Instance);
            if (elementsField == null) return;
            var elements = elementsField.GetValue(__instance) as Image[];
            if (elements == null || elements.Length < 11) return;

            var axeImage = elements[10];
            if (axeImage != null)
            {
                if (axeImage.transform.childCount > 2)
                    axeImage.transform.GetChild(2).gameObject.SetActive(false);
                var btn = axeImage.GetComponent<Button>();
                if (btn != null)
                    btn.interactable = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Guard: AvailableElements.GetAvailableAndIncludedElements — ensure
    // GameSettings.elements is large enough before the loop, and include
    // Axe (index 10) in the available/included lists afterward.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(AvailableElements), "GetAvailableAndIncludedElements")]
    public static class AxeGetAvailableGuardPatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            AxeElementPatches.EnsureElementsArraySize();
        }

        [HarmonyPostfix]
        public static void Postfix(ref List<Element> available, ref List<Element> included)
        {
            // The vanilla loop only iterates i <= lastUnlockedIndex + 4 (up to index 9).
            // Manually check index 10 (Axe) and add to the correct list.
            if (PlayerManager.gameSettings.elements != null &&
                PlayerManager.gameSettings.elements.Length > 10)
            {
                var mode = PlayerManager.gameSettings.elements[10];
                if (mode == ElementInclusionMode.Possible && !available.Contains(Axe.Element))
                    available.Add(Axe.Element);
                else if (mode == ElementInclusionMode.Included && !included.Contains(Axe.Element))
                    included.Add(Axe.Element);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AvailableElements.ShowAvailableElements — Force-unlock Axe (index 10)
    // on the round display tablet. The vanilla threshold LastUnlockedIndex + 5
    // excludes our modded element.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(AvailableElements), "ShowAvailableElements")]
    public static class AxeAvailableElementsUnlockPatch
    {
        [HarmonyPostfix]
        public static void Postfix(AvailableElements __instance)
        {
            try
            {
                var locksField = typeof(AvailableElements).GetField("locks",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var iconsField = typeof(AvailableElements).GetField("elementIcons",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                var locks = locksField?.GetValue(__instance) as Image[];
                var icons = iconsField?.GetValue(__instance) as Image[];

                if (locks != null && locks.Length > 10)
                    locks[10].enabled = false;

                if (icons != null && icons.Length > 10)
                    icons[10].color = new Color(0.4f, 0.4f, 0.4f);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeUI] AvailableElements unlock postfix failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SelectionMenu.ChangeElement — Extend the cycle range to include Axe.
    // Vanilla uses LastUnlockedIndex + 5 as modulo cap, which excludes
    // our 11th element (index 10).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "ChangeElement")]
    public static class AxeChangeElementPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(SelectionMenu __instance, bool up)
        {
            try
            {
                int num = AvailableElements.unlockOrder.Length; // 11
                var indexField = typeof(SelectionMenu).GetField("elementIndex",
                    BindingFlags.Public | BindingFlags.Instance);
                if (indexField == null) return true;

                int current = (int)indexField.GetValue(__instance);
                int next = (current + (up ? 1 : (num - 1))) % num;
                indexField.SetValue(__instance, next);

                var updateMethod = typeof(SelectionMenu).GetMethod("UpdateElementSelector",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                updateMethod?.Invoke(__instance, null);

                return false; // Skip original
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeUI] ChangeElement patch failed: {ex}");
                return true; // Fallback to vanilla
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AvailableElements.Awake — Prefix clones a tablet child to create the
    // 11th icon slot; Postfix verifies the Metal sprite is applied.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(AvailableElements), "Awake")]
    public static class AxeAvailableElementsPatch
    {
        [HarmonyPrefix]
        public static void Prefix(AvailableElements __instance)
        {
            try
            {
                // Ensure unlockOrder is expanded (safety — Initialize should have done this)
                if (AvailableElements.unlockOrder.Length <= 10)
                    AxeElementPatches.Initialize();

                // Clone a tablet child to create the 11th icon slot.
                // Awake's body iterates tablet.GetChild(i) for i=0..unlockOrder.Length-1,
                // so we must add the child BEFORE Awake runs.
                var tablet = __instance.panel.transform.GetChild(0);
                if (tablet.childCount == 10)
                {
                    // Clone Metal's child (index 8) — it already has the Metal sprite
                    var metalChild = tablet.GetChild(8);
                    var newChild = Object.Instantiate(metalChild, tablet);
                    newChild.SetAsLastSibling();

                    // Position next to Ice (index 9) using same grid offset
                    var metalRT = metalChild.GetComponent<RectTransform>();
                    var iceRT = tablet.GetChild(9).GetComponent<RectTransform>();
                    var newRT = newChild.GetComponent<RectTransform>();
                    float xStep = iceRT.anchoredPosition.x - metalRT.anchoredPosition.x;
                    newRT.anchoredPosition = new Vector2(
                        iceRT.anchoredPosition.x + xStep,
                        metalRT.anchoredPosition.y);

                    Plugin.Log.LogInfo("[AxeUI] AvailableElements: Cloned Metal icon as 11th tablet child for Axe");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeUI] AvailableElements tablet clone failed: {ex}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix(AvailableElements __instance)
        {
            try
            {
                var iconsField = typeof(AvailableElements).GetField("elementIcons",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (iconsField == null) return;

                var elementIcons = iconsField.GetValue(__instance) as Image[];
                if (elementIcons == null || elementIcons.Length < 11) return;

                // Ensure index 10 (Axe) has the Metal sprite
                var metalIcon = elementIcons[8];
                var axeIcon = elementIcons[10];
                if (metalIcon != null && axeIcon != null && metalIcon.sprite != null)
                {
                    axeIcon.sprite = metalIcon.sprite;
                    Plugin.Log.LogInfo("[AxeUI] AvailableElements: Confirmed Metal icon on Axe slot (index 10)");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeUI] AvailableElements icon assignment failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SelectionMenu.Start — Prefix creates the 11th element Image in the
    // element toggle grid before Start's body calls Refresh → ShowElements.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "Start")]
    public static class AxeSelectionMenuIconPatch
    {
        [HarmonyPrefix]
        public static void Prefix(SelectionMenu __instance)
        {
            try
            {
                // Ensure GameSettings.elements array can hold 11 entries
                AxeElementPatches.EnsureElementsArraySize();

                var elementsField = typeof(SelectionMenu).GetField("elements",
                    BindingFlags.Public | BindingFlags.Instance);
                if (elementsField == null) return;

                var elements = elementsField.GetValue(__instance) as Image[];
                if (elements == null || elements.Length != 10) return;

                // Clone Metal icon (index 8) as template for Axe — already has Metal sprite
                var template = elements[8];
                var newObj = Object.Instantiate(template.gameObject, template.transform.parent);
                var newImage = newObj.GetComponent<Image>();

                // Position next to Ice (index 9) using same grid offset
                var metalRT = elements[8].GetComponent<RectTransform>();
                var iceRT = elements[9].GetComponent<RectTransform>();
                var newRT = newObj.GetComponent<RectTransform>();
                float xStep = iceRT.anchoredPosition.x - metalRT.anchoredPosition.x;
                newRT.anchoredPosition = new Vector2(
                    iceRT.anchoredPosition.x + xStep,
                    metalRT.anchoredPosition.y);

                // Expand the elements array to 11
                var expanded = new Image[11];
                elements.CopyTo(expanded, 0);
                expanded[10] = newImage;
                elementsField.SetValue(__instance, expanded);

                // Initialize child indicators (Included/Banned/Lock) as hidden
                newObj.transform.GetChild(0).gameObject.SetActive(false);
                newObj.transform.GetChild(1).gameObject.SetActive(false);
                if (newObj.transform.childCount > 2)
                    newObj.transform.GetChild(2).gameObject.SetActive(false);

                // Fix button onClick to call ClickElement(10) for the Axe slot
                var btn = newObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener(() => __instance.ClickElement(10));
                    btn.interactable = true;
                }

                Plugin.Log.LogInfo("[AxeUI] SelectionMenu: Created 11th element Image for Axe");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeUI] SelectionMenu element creation failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SpellHandler.Start — Expand elementEffects (EmissionModule[]) and
    // ultimateEffects (ParticleSystem[]) to include index 11 for Axe.
    // Reuse Metal's effects (index 9).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpellHandler), "Start")]
    public static class AxeSpellHandlerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SpellHandler __instance)
        {
            try
            {
                // elementEffects is ParticleSystem.EmissionModule[] (struct array), size 11
                var elemField = typeof(SpellHandler).GetField("elementEffects",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (elemField != null)
                {
                    var raw = elemField.GetValue(__instance);
                    if (raw is ParticleSystem.EmissionModule[] emArr && emArr.Length <= 11)
                    {
                        var expanded = new ParticleSystem.EmissionModule[12];
                        emArr.CopyTo(expanded, 0);
                        // Reuse Metal's emission (index 9) for Axe (index 11)
                        expanded[11] = emArr[9];
                        elemField.SetValue(__instance, expanded);
                        Plugin.Log.LogInfo($"[AxeUI] SpellHandler: Expanded elementEffects from {emArr.Length} to 12");
                    }
                }

                // ultimateEffects is ParticleSystem[], size 11
                var ultFxField = typeof(SpellHandler).GetField("ultimateEffects",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (ultFxField != null)
                {
                    var arr = ultFxField.GetValue(__instance) as ParticleSystem[];
                    if (arr != null && arr.Length <= 11)
                    {
                        var expanded = new ParticleSystem[12];
                        arr.CopyTo(expanded, 0);
                        expanded[11] = arr[9];
                        ultFxField.SetValue(__instance, expanded);
                        Plugin.Log.LogInfo($"[AxeUI] SpellHandler: Expanded ultimateEffects from {arr.Length} to 12");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeUI] SpellHandler FX expansion failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ElementColorMapping.Start — Override stage visuals with steel/grey
    // for the Axe element. Skips Practice Range to avoid FMOD conflicts.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ElementColorMapping), "Start")]
    public static class AxeElementColorMappingPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ElementColorMapping __instance)
        {
            if (__instance.element != Axe.Element)
                return;

            // Skip in Practice Range to avoid FMOD/visual conflicts
            if (Globals.practice_range_manager != null)
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

            // Replace ambient sound
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
    // VideoSpellPlayer — Override draft UI colors for element index 11 (Axe).
    // Expands the color arrays if needed since vanilla only has indices 0-10.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(VideoSpellPlayer), "SlideIn")]
    public static class AxeVideoSpellPlayerPatch
    {
        [HarmonyPrefix]
        public static void Prefix(VideoSpellPlayer __instance)
        {
            var darkField = typeof(VideoSpellPlayer).GetField("darkColors",
                BindingFlags.Public | BindingFlags.Instance);
            var lightField = typeof(VideoSpellPlayer).GetField("lightColors",
                BindingFlags.Public | BindingFlags.Instance);

            if (darkField != null)
            {
                var darkColors = darkField.GetValue(__instance) as Color[];
                if (darkColors != null && darkColors.Length <= 11)
                {
                    var expanded = new Color[12];
                    darkColors.CopyTo(expanded, 0);
                    darkField.SetValue(__instance, expanded);
                    darkColors = expanded;
                }
                if (darkColors != null && darkColors.Length > 11)
                    darkColors[11] = new Color(0.3f, 0.3f, 0.35f);
            }

            if (lightField != null)
            {
                var lightColors = lightField.GetValue(__instance) as Color[];
                if (lightColors != null && lightColors.Length <= 11)
                {
                    var expanded = new Color[12];
                    lightColors.CopyTo(expanded, 0);
                    lightField.SetValue(__instance, expanded);
                    lightColors = expanded;
                }
                if (lightColors != null && lightColors.Length > 11)
                    lightColors[11] = new Color(0.7f, 0.7f, 0.75f);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SelectionMenu.ShowElementTooltip — Replace "Tutorial" with "Axe" in
    // the element name shown in the description text.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "ShowElementTooltip")]
    public static class AxeSelectionMenuPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SelectionMenu __instance)
        {
            var descField = typeof(SelectionMenu).GetField("descriptionText",
                BindingFlags.Public | BindingFlags.Instance);
            if (descField != null)
            {
                var textObj = descField.GetValue(__instance);
                if (textObj is Text uiText && uiText.text != null)
                    uiText.text = uiText.text.Replace("Tutorial", "Axe");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEBUG: Trace GetSpellByRoundAndElement to diagnose element display.
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
                    Plugin.Log.LogInfo($"[AxeDbg] GetSpellByRoundAndElement(Tutorial/Axe, round={round}) => {__result.spellName} el={__result.element} btn={__result.spellButton}");
                else
                    Plugin.Log.LogWarning($"[AxeDbg] GetSpellByRoundAndElement(Tutorial/Axe, round={round}) => NULL");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PracticeRangeManager.Awake — Preemptive guard to ensure Practice Range
    // FMOD state stays on Tutorial music. Since Tutorial == Axe.Element,
    // we confirm the element assignment after the vanilla Awake runs.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PracticeRangeManager), "Awake")]
    public static class AxePracticeRangeGuardPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                var currentField = typeof(FmodController).GetField("current",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (currentField == null) return;

                var fmod = currentField.GetValue(null);
                if (fmod == null) return;

                var elementField = typeof(FmodController).GetField("currentElement",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (elementField == null) return;

                elementField.SetValue(fmod, (Element)11);
                Plugin.Log.LogInfo("[AxeUI] Practice Range guard: confirmed currentElement = Tutorial/11");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[AxeUI] Practice Range guard failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PlayerSelection.Update — Temporarily bump LastUnlockedIndex to 6 so
    // ErrorCheck's .Take(LastUnlockedIndex+5) includes Axe at index 10.
    // Restored in the postfix so other systems (GetRandomAvailable, etc.)
    // see the vanilla value and select Axe spells correctly.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PlayerSelection), "Update")]
    public static class AxePlayerSelectionUpdatePatch
    {
        private static int savedLastUnlockedIndex = -1;
        private static bool logged = false;

        [HarmonyPrefix]
        public static void Prefix()
        {
            AxeElementPatches.EnsureElementsArraySize();

            if (GamePreferences.current != null &&
                GamePreferences.current.prefs != null)
            {
                savedLastUnlockedIndex = GamePreferences.current.prefs.LastUnlockedIndex;
                if (GamePreferences.current.prefs.LastUnlockedIndex < 6)
                {
                    GamePreferences.current.prefs.LastUnlockedIndex = 6;
                }

                if (!logged)
                {
                    logged = true;
                    Plugin.Log.LogInfo("[AxeUI] PlayerSelection.Update prefix: " +
                        $"bumped LastUnlockedIndex from {savedLastUnlockedIndex} to 6");
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (savedLastUnlockedIndex >= 0 &&
                GamePreferences.current != null &&
                GamePreferences.current.prefs != null)
            {
                GamePreferences.current.prefs.LastUnlockedIndex = savedLastUnlockedIndex;
                savedLastUnlockedIndex = -1;
            }
        }
    }
}
