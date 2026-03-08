using System;
using System.Collections.Generic;
using System.Reflection;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using MageQuitModFramework.Data;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BloodElement
{
    // ─────────────────────────────────────────────────────────────────────────
    // Constant aliases for the Blood element and its seven SpellName values.
    // Element.Tutorial == 11 is used as the Blood slot. Ice stays vanilla.
    // SpellNames 146-152 are appended after ColdFusion (145).
    // ─────────────────────────────────────────────────────────────────────────
    public static class Blood
    {
        public static readonly Element Element = (Element)11; // Tutorial slot

        public static readonly SpellName BloodPrimary  = (SpellName)146;
        public static readonly SpellName BloodMovement = (SpellName)147;
        public static readonly SpellName BloodMelee   = (SpellName)148;
        public static readonly SpellName BloodSecondary = (SpellName)149;
        public static readonly SpellName BloodDefensive = (SpellName)150;
        public static readonly SpellName BloodUtility = (SpellName)151;
        public static readonly SpellName BloodUltimate = (SpellName)152;
    }

    [HarmonyPatch]
    public static class BloodElementPatches
    {
        public static void Initialize()
        {
            // Expand unlockOrder to include Blood (Tutorial element) as the 11th entry.
            // This runs at mod load, before any Unity lifecycle methods.
            if (AvailableElements.unlockOrder != null && AvailableElements.unlockOrder.Length <= 10)
            {
                var expanded = new Element[11];
                AvailableElements.unlockOrder.CopyTo(expanded, 0);
                expanded[10] = Blood.Element;
                AvailableElements.unlockOrder = expanded;
                Plugin.Log.LogInfo("[BloodInit] Expanded unlockOrder to 11 elements (added Blood at index 10)");
            }

            // Inject Blood element into SpellHandler's static sound dictionaries.
            // Both map Element → FMOD event path; all vanilla elements use the same path.
            try
            {
                var castField = typeof(SpellHandler).GetField("castSounds",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (castField != null)
                {
                    var dict = castField.GetValue(null) as Dictionary<Element, string>;
                    if (dict != null && !dict.ContainsKey(Blood.Element))
                    {
                        dict[Blood.Element] = "event:/sfx/wizard/spell-attack";
                        Plugin.Log.LogInfo("[BloodInit] Injected Blood into SpellHandler.castSounds");
                    }
                }

                var ultField = typeof(SpellHandler).GetField("ultimateCastSounds",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (ultField != null)
                {
                    var dict = ultField.GetValue(null) as Dictionary<Element, string>;
                    if (dict != null && !dict.ContainsKey(Blood.Element))
                    {
                        dict[Blood.Element] = "event:/sfx/wizard/spell-attack";
                        Plugin.Log.LogInfo("[BloodInit] Injected Blood into SpellHandler.ultimateCastSounds");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodInit] Failed to inject SpellHandler sound dictionaries: {ex}");
            }
        }

        /// <summary>
        /// Ensures GameSettings.elements array is large enough for all unlockOrder entries,
        /// and bumps LastUnlockedIndex so vanilla .Take(LastUnlockedIndex+5) thresholds
        /// include Blood at index 10.
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
    // WizardController.Awake — attach BloodNetworkBridge to every wizard GO so
    // that wizard-level RPCs can route Blood spell events to remote clients.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(WizardController), "Awake")]
    public static class BloodWizardBridgePatch
    {
        [HarmonyPostfix]
        public static void Postfix(WizardController __instance)
        {
            if (__instance == null) return;
            if (__instance.GetComponent<BloodNetworkBridge>() == null)
                __instance.gameObject.AddComponent<BloodNetworkBridge>();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WizardStatus.rpcApplyDamage — notify BloodDefensive objects
    // whenever the wizard takes damage.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(WizardStatus), "rpcApplyDamage")]
    public static class BloodWizardStatusPatch
    {
        [HarmonyPostfix]
        public static void Postfix(WizardStatus __instance, float damage, int owner, int source)
        {
            try
            {
                BloodDefensiveObject.NotifyDamage(owner, damage, __instance);
                BloodUltimateObject.NotifyDamage(owner, damage, __instance as UnitStatus);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodDmg] rpcApplyDamage hook failed: damage={damage}, owner={owner}, source={source}: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GameSettings constructor — ensure elements array is large enough for
    // 11 elements (Blood occupies the Tutorial slot at index 10).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(GameSettings), MethodType.Constructor)]
    public static class BloodGameSettingsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameSettings __instance)
        {
            BloodElementPatches.EnsureElementsArraySize();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Guard: SelectionMenu.ShowElements — ensure arrays are correct size.
    // Prefix: expand GameSettings.elements; skip if Image array not ready.
    // Postfix: force-unlock Blood (index 10) past LastUnlockedIndex threshold.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "ShowElements")]
    public static class BloodElementsArrayGuardPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(SelectionMenu __instance)
        {
            BloodElementPatches.EnsureElementsArraySize();

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
            // Force unlock Blood (index 10) — the vanilla condition
            // i < LastUnlockedIndex + 5 excludes our modded element
            var elementsField = typeof(SelectionMenu).GetField("elements",
                BindingFlags.Public | BindingFlags.Instance);
            if (elementsField == null) return;
            var elements = elementsField.GetValue(__instance) as Image[];
            if (elements == null || elements.Length < 11) return;

            var bloodImage = elements[10];
            if (bloodImage != null)
            {
                if (bloodImage.transform.childCount > 2)
                    bloodImage.transform.GetChild(2).gameObject.SetActive(false);
                var btn = bloodImage.GetComponent<Button>();
                if (btn != null)
                    btn.interactable = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Guard: AvailableElements.GetAvailableAndIncludedElements — ensure
    // GameSettings.elements is large enough before the loop, and include
    // Blood (index 10) in the available/included lists afterward.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(AvailableElements), "GetAvailableAndIncludedElements")]
    public static class BloodGetAvailableGuardPatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            BloodElementPatches.EnsureElementsArraySize();
        }

        [HarmonyPostfix]
        public static void Postfix(ref List<Element> available, ref List<Element> included)
        {
            // The vanilla loop only iterates i <= lastUnlockedIndex + 4 (up to index 9).
            // Manually check index 10 (Blood) and add to the correct list.
            if (PlayerManager.gameSettings.elements != null &&
                PlayerManager.gameSettings.elements.Length > 10)
            {
                var mode = PlayerManager.gameSettings.elements[10];
                if (mode == ElementInclusionMode.Possible && !available.Contains(Blood.Element))
                    available.Add(Blood.Element);
                else if (mode == ElementInclusionMode.Included && !included.Contains(Blood.Element))
                    included.Add(Blood.Element);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AvailableElements.ShowAvailableElements — Force-unlock Blood (index 10)
    // on the round display tablet. The vanilla threshold LastUnlockedIndex + 5
    // excludes our modded element.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(AvailableElements), "ShowAvailableElements")]
    public static class BloodAvailableElementsUnlockPatch
    {
        [HarmonyPostfix]
        public static void Postfix(AvailableElements __instance)
        {
            try
            {
                var locksField = typeof(AvailableElements).GetField("locks",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                var locks = locksField?.GetValue(__instance) as Image[];

                if (locks != null && locks.Length > 10)
                    locks[10].enabled = false;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodUI] AvailableElements unlock postfix failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SelectionMenu.ChangeElement — Extend the cycle range to include Blood.
    // Vanilla uses LastUnlockedIndex + 5 as modulo cap, which excludes
    // our 11th element (index 10).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "ChangeElement")]
    public static class BloodChangeElementPatch
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
                Plugin.Log.LogError($"[BloodUI] ChangeElement patch failed: {ex}");
                return true; // Fallback to vanilla
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AvailableElements.Awake — Prefix clones a tablet child to create the
    // 11th icon slot; Postfix verifies the Metal sprite is applied.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(AvailableElements), "Awake")]
    public static class BloodAvailableElementsPatch
    {
        [HarmonyPrefix]
        public static void Prefix(AvailableElements __instance)
        {
            try
            {
                // Ensure unlockOrder is expanded (safety — Initialize should have done this)
                if (AvailableElements.unlockOrder.Length <= 10)
                    BloodElementPatches.Initialize();

                // Clone a tablet child to create the 11th icon slot.
                // Awake's body iterates tablet.GetChild(i) for i=0..unlockOrder.Length-1,
                // so we must add the child BEFORE Awake runs.
                var tablet = __instance.panel.transform.GetChild(0);
                if (tablet.childCount == 10)
                {
                    // Clone Metal's child (index 8) — it already has the Metal sprite
                    var metalChild = tablet.GetChild(8);
                    var newChild = UnityEngine.Object.Instantiate(metalChild, tablet);
                    newChild.SetAsLastSibling();

                    // Place Blood as the 5th element of the center diagonal (children 0-3).
                    // Step = (child3 - child0) / 3; new pos = child3 + step.
                    var e0RT = tablet.GetChild(0).GetComponent<RectTransform>();
                    var e3RT = tablet.GetChild(3).GetComponent<RectTransform>();
                    var step = (e3RT.anchoredPosition - e0RT.anchoredPosition) / 3f;
                    var newRT = newChild.GetComponent<RectTransform>();
                    newRT.anchoredPosition = e3RT.anchoredPosition + step;

                    // Match the prefab rotation baked into the other icons (clockwise tilt).
                    // Copy from child 0 to guarantee parity regardless of clone behaviour.
                    newRT.localEulerAngles = e0RT.localEulerAngles;

                    // Replace Metal's symbol with the Blood icon PNG
                    var bloodSprite = BloodRegistration.LoadPngIcon("primary.png");
                    if (bloodSprite != null)
                        newChild.GetComponent<Image>().sprite = bloodSprite;

                    Plugin.Log.LogInfo("[BloodUI] AvailableElements: Cloned Metal icon as 11th tablet child for Blood");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodUI] AvailableElements tablet clone failed: {ex}");
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

                // Ensure index 10 (Blood) has the Blood icon, falling back to Metal sprite
                var metalIcon = elementIcons[8];
                var bloodIcon = elementIcons[10];
                if (bloodIcon != null)
                {
                    var bloodSprite = BloodRegistration.LoadPngIcon("primary.png");
                    if (bloodSprite != null)
                    {
                        bloodIcon.sprite = bloodSprite;
                        Plugin.Log.LogInfo("[BloodUI] AvailableElements: Set Blood PNG icon on slot (index 10)");
                    }
                    else if (metalIcon != null && metalIcon.sprite != null)
                    {
                        bloodIcon.sprite = metalIcon.sprite;
                        Plugin.Log.LogInfo("[BloodUI] AvailableElements: Fallback — Metal icon on Blood slot (index 10)");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodUI] AvailableElements icon assignment failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SelectionMenu.Start — Prefix creates the 11th element Image in the
    // element toggle grid before Start's body calls Refresh → ShowElements.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "Start")]
    public static class BloodSelectionMenuIconPatch
    {
        [HarmonyPrefix]
        public static void Prefix(SelectionMenu __instance)
        {
            try
            {
                // Ensure GameSettings.elements array can hold 11 entries
                BloodElementPatches.EnsureElementsArraySize();

                var elementsField = typeof(SelectionMenu).GetField("elements",
                    BindingFlags.Public | BindingFlags.Instance);
                if (elementsField == null) return;

                var elements = elementsField.GetValue(__instance) as Image[];
                if (elements == null || elements.Length != 10) return;

                // Clone Metal icon (index 8) as template for Blood — already has Metal sprite
                var template = elements[8];
                var newObj = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
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

                // Replace Metal's element symbol with the Blood icon PNG
                var bloodSprite = BloodRegistration.LoadPngIcon("primary.png");
                if (bloodSprite != null)
                    newImage.sprite = bloodSprite;

                // Initialize child indicators (Included/Banned/Lock) as hidden
                newObj.transform.GetChild(0).gameObject.SetActive(false);
                newObj.transform.GetChild(1).gameObject.SetActive(false);
                if (newObj.transform.childCount > 2)
                    newObj.transform.GetChild(2).gameObject.SetActive(false);

                // Fix button onClick to call ClickElement(10) for the Blood slot
                var btn = newObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener(() => __instance.ClickElement(10));
                    btn.interactable = true;
                }

                Plugin.Log.LogInfo("[BloodUI] SelectionMenu: Created 11th element Image for Blood");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodUI] SelectionMenu element creation failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SpellHandler.Start — Expand elementEffects (EmissionModule[]) and
    // ultimateEffects (ParticleSystem[]) to include index 11 for Blood.
    // Reuse Metal's effects (index 9).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SpellHandler), "Start")]
    public static class BloodSpellHandlerPatch
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
                        // Reuse Metal's emission (index 9) for Blood (index 11)
                        expanded[11] = emArr[9];
                        elemField.SetValue(__instance, expanded);
                        Plugin.Log.LogInfo($"[BloodUI] SpellHandler: Expanded elementEffects from {emArr.Length} to 12");
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
                        Plugin.Log.LogInfo($"[BloodUI] SpellHandler: Expanded ultimateEffects from {arr.Length} to 12");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodUI] SpellHandler FX expansion failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ElementColorMapping.Start — Override stage visuals with steel/grey
    // for the Blood element. Skips Practice Range to avoid FMOD conflicts.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ElementColorMapping), "Start")]
    public static class BloodElementColorMappingPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ElementColorMapping __instance)
        {
            if (__instance.element != Blood.Element)
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
    // VideoSpellPlayer — Override draft UI colors for element index 11 (Blood).
    // Expands the color arrays if needed since vanilla only has indices 0-10.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(VideoSpellPlayer), "SlideIn")]
    public static class BloodVideoSpellPlayerPatch
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
                    darkColors[11] = new Color(0.35f, 0.04f, 0.04f);
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
                    lightColors[11] = new Color(0.75f, 0.25f, 0.20f);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SelectionMenu.ShowElementTooltip — Replace "Tutorial" with "Blood" in
    // the element name shown in the description text.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(SelectionMenu), "ShowElementTooltip")]
    public static class BloodSelectionMenuPatch
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
                    uiText.text = uiText.text.Replace("Tutorial", "Blood");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEBUG: Trace GetSpellByRoundAndElement to diagnose element display.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(GameUtility), "GetSpellByRoundAndElement", typeof(Element), typeof(int))]
    public static class BloodGetSpellDebugPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Element el, int round, Spell __result)
        {
            if (el == Blood.Element)
            {
                if (__result != null)
                    Plugin.Log.LogInfo($"[BloodDbg] GetSpellByRoundAndElement(Tutorial/Blood, round={round}) => {__result.spellName} el={__result.element} btn={__result.spellButton}");
                else
                    Plugin.Log.LogWarning($"[BloodDbg] GetSpellByRoundAndElement(Tutorial/Blood, round={round}) => NULL");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PracticeRangeManager.Awake — Preemptive guard to ensure Practice Range
    // FMOD state stays on Tutorial music. Since Tutorial == Blood.Element,
    // we confirm the element assignment after the vanilla Awake runs.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PracticeRangeManager), "Awake")]
    public static class BloodPracticeRangeGuardPatch
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
                Plugin.Log.LogInfo("[BloodUI] Practice Range guard: confirmed currentElement = Tutorial/11");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodUI] Practice Range guard failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PlayerSelection.Update — Temporarily bump LastUnlockedIndex to 6 so
    // ErrorCheck's .Take(LastUnlockedIndex+5) includes Blood at index 10.
    // Restored in the postfix so other systems (GetRandomAvailable, etc.)
    // see the vanilla value and select Blood spells correctly.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PlayerSelection), "Update")]
    public static class BloodPlayerSelectionUpdatePatch
    {
        private static int savedLastUnlockedIndex = -1;
        private static bool logged = false;

        [HarmonyPrefix]
        public static void Prefix()
        {
            BloodElementPatches.EnsureElementsArraySize();

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
                    Plugin.Log.LogInfo("[BloodUI] PlayerSelection.Update prefix: " +
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

    // ─────────────────────────────────────────────────────────────────────────
    // OnlineLobbyCursor.UpdateActions — Temporarily bump LastUnlockedIndex to 6
    // so the element ban validation's .Take(LastUnlockedIndex+5) includes Blood
    // at index 10. Mirrors BloodPlayerSelectionUpdatePatch for online mode.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(OnlineLobbyCursor), "UpdateActions")]
    public static class BloodOnlineLobbyUpdateActionsPatch
    {
        private static int savedLastUnlockedIndex = -1;

        [HarmonyPrefix]
        public static void Prefix()
        {
            BloodElementPatches.EnsureElementsArraySize();

            if (GamePreferences.current != null &&
                GamePreferences.current.prefs != null)
            {
                savedLastUnlockedIndex = GamePreferences.current.prefs.LastUnlockedIndex;
                if (GamePreferences.current.prefs.LastUnlockedIndex < 6)
                {
                    GamePreferences.current.prefs.LastUnlockedIndex = 6;
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

    // ─────────────────────────────────────────────────────────────────────────
    // WizardStatus.ApplyDamage — Prefix:
    //   • If target has BloodDefensive active  → counter and zero the damage.
    //   • If target is bleeding              → refresh bleed timer.
    // Lifesteal is handled in the rpcApplyDamage postfix below, which fires
    // exactly once per actual damage application on the authoritative client.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(WizardStatus), "ApplyDamage")]
    public static class BloodBleedApplyDamagePatch
    {
        [HarmonyPrefix]
        public static void Prefix(WizardStatus __instance, ref float damage, int owner, int source)
        {
            try
            {
                if (__instance == null) return;
                var targetId = __instance.GetComponent<Identity>();
                if (targetId == null) return;

                // If target has BloodDefensive active, trigger the counter and block the damage.
                if (BloodDefensiveObject.activeDefensives.ContainsKey(targetId.owner) &&
                    BloodDefensiveObject.activeDefensives[targetId.owner] != null)
                {
                    BloodDefensiveObject.NotifyDamage(owner, damage, __instance);
                    damage = 0f;
                    return;
                }

                if (BleedManager.IsBleedActive(targetId.owner))
                {
                    Plugin.Log.LogInfo($"[BloodField] owner={owner} target={targetId.owner} is bleeding");
                    BleedManager.RefreshBleed(targetId.owner);

                    // Blood player bonus: +10% damage and lifesteal vs bleeding enemies.
                    bool isBloodPlayer =
                        PlayerManager.players.ContainsKey(owner) &&
                        (
                            (PlayerManager.players[owner].spell_library.TryGetValue(
                                SpellButton.Ultimate, out SpellName ultName) && ultName == Blood.BloodUltimate) ||
                            (PlayerManager.players[owner].spell_library.TryGetValue(
                                SpellButton.Melee, out SpellName meleeName) && meleeName == Blood.BloodMelee)
                        );

                    if (isBloodPlayer)
                    {
                        float heal = damage * BloodUltimateObject.LIFESTEAL_MULT;
                        damage *= BleedManager.BLEED_DAMAGE_MULT;
                        Plugin.Log.LogInfo($"[BloodField] owner={owner} damage={damage:F2} heal={heal:F2}");
                        GameUtility.GetWizard(owner)?.GetComponent<WizardStatus>()?.ApplyHealing(heal, owner);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodBleed] Damage prefix failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PhysicsBody.AddForceOwner — Block knockback from the parried hit.
    // activeDefensives is already cleared when AddForceOwner is called (the
    // parry consumes during ApplyDamage), so we use recentlyParriedUntil which
    // stays set for 0.5 s, covering the same-frame AddForceOwner call.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PhysicsBody), "AddForceOwner")]
    public static class BloodDefensiveKnockbackPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PhysicsBody __instance)
        {
            var identity = __instance.GetComponent<Identity>();
            if (identity == null) return true;

            // Block while actively parrying.
            if (BloodDefensiveObject.activeDefensives.TryGetValue(identity.owner, out var def) && def != null)
                return false;

            // Block briefly after the parry fires (covers same-frame AddForceOwner calls).
            if (BloodDefensiveObject.recentlyParriedUntil.TryGetValue(identity.owner, out float until) &&
                Time.time < until)
                return false;

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PracticeRangePauseMenu.Awake — Append 7 spell-cell buttons for the Blood
    // column (index 10) and bump maxX so keyboard/gamepad navigation reaches it.
    //
    // Layout: this.spells children are laid out as:
    //   [0..14]  = header row / misc UI (15 items, untouched)
    //   [15 + i*7 + j] = spell cell for element column i, spell row j
    // We append children 85..91 (= 15 + 10*7 + 0..6) for Blood.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PracticeRangePauseMenu), "Awake")]
    public static class BloodPracticeMenuPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PracticeRangePauseMenu __instance)
        {
            try
            {
                var spells = __instance.spells;
                if (spells == null) return;

                // Guard: only expand once
                int expected = 15 + 11 * 7; // 92
                if (spells.childCount >= expected) return;

                // Clone Metal's cells (column i=8, children 71..77) as templates.
                // Compute per-row column step from Metal→Ice (i=8→9) to place Blood at i=10.
                for (int j = 0; j < 7; j++)
                {
                    var metalTemplate = spells.GetChild(15 + 8 * 7 + j);
                    var iceTemplate   = spells.GetChild(15 + 9 * 7 + j);
                    var metalRT = metalTemplate.GetComponent<RectTransform>();
                    var iceRT   = iceTemplate.GetComponent<RectTransform>();
                    Vector2 colStep = iceRT.anchoredPosition - metalRT.anchoredPosition;

                    var newCell = UnityEngine.Object.Instantiate(metalTemplate, spells);
                    newCell.SetAsLastSibling();

                    // Position one column-step beyond Ice
                    var newRT = newCell.GetComponent<RectTransform>();
                    newRT.anchoredPosition = iceRT.anchoredPosition + colStep;

                    // Replace Metal icon with the Blood spell icon loaded from disk.
                    string[] iconFiles = { "primary.png", "movement.png", "melee.png",
                                          "secondary.png", "defensive.png", "utility.png", "ultimate.png" };
                    var bloodSprite = BloodRegistration.LoadPngIcon(iconFiles[j]);

                    var img = newCell.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = Color.white;
                        if (bloodSprite != null)
                            img.sprite = bloodSprite;
                        // else: keep Metal's cloned sprite so the cell remains visible
                    }

                    // Wire button click to navigate to this cell
                    int captJ = j;
                    var btn = newCell.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick = new Button.ButtonClickedEvent();
                        btn.onClick.AddListener(() => __instance.UpdateCursor(10, captJ));
                        btn.interactable = true;
                    }
                }

                // Bump maxX to 11 so gamepad/keyboard navigation reaches the Blood column
                var maxXField = typeof(PracticeRangePauseMenu).GetField("maxX",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (maxXField != null)
                {
                    int cur = (int)maxXField.GetValue(__instance);
                    if (cur < 11)
                        maxXField.SetValue(__instance, 11);
                }

                Plugin.Log.LogInfo("[BloodUI] PracticeRangePauseMenu: Added Blood column, maxX >= 11");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BloodUI] PracticeRangePauseMenu patch failed: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VideoSpellPlayer.HighlightSpell — Fix spell name display.
    // SpellName values 146-152 are not defined in the vanilla enum, so
    // .ToString() returns the integer ("146") and AddSpaces turns it into
    // " 146". Both overloads set spellNameText.text the same way, so we
    // patch both with the same postfix: if the trimmed text parses as an int,
    // look it up in our name table and replace it.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class BloodSpellDisplayNames
    {
        internal static readonly Dictionary<SpellName, string> Names =
            new Dictionary<SpellName, string>
            {
                { Blood.BloodPrimary,   "Rend" },
                { Blood.BloodMovement,  "Lunge" },
                { Blood.BloodMelee,     "Bleed" },
                { Blood.BloodSecondary, "Wild Blades" },
                { Blood.BloodDefensive, "Riposte" },
                { Blood.BloodUtility,   "Blade Storm" },
                { Blood.BloodUltimate,  "Sanguine Aura" },
            };

        internal static void FixNameText(VideoSpellPlayer vsp)
        {
            if (vsp == null || vsp.spellNameText == null) return;
            string raw = vsp.spellNameText.text.Trim();
            if (!int.TryParse(raw, out int id)) return;
            if (Names.TryGetValue((SpellName)id, out string name))
                vsp.spellNameText.text = name;
        }
    }

    [HarmonyPatch(typeof(VideoSpellPlayer), "HighlightSpell", new Type[] { typeof(Element), typeof(int) })]
    public static class BloodHighlightSpellElementPatch
    {
        [HarmonyPostfix]
        public static void Postfix(VideoSpellPlayer __instance)
        {
            BloodSpellDisplayNames.FixNameText(__instance);
        }
    }

    [HarmonyPatch(typeof(VideoSpellPlayer), "HighlightSpell", new Type[] { typeof(int) })]
    public static class BloodHighlightSpellIndexPatch
    {
        [HarmonyPostfix]
        public static void Postfix(VideoSpellPlayer __instance)
        {
            BloodSpellDisplayNames.FixNameText(__instance);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PlayVideo null-guard patches.
    // Blood spells have no VideoClip assigned, so spell.video is null.
    // Without these guards, Dictionary lookups with a null key throw
    // ArgumentNullException in InitClips and ChangeVideo.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PlayVideo), "ChangeVideo")]
    public static class BloodPlayVideoChangeVideoPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(VideoClip clip) => clip != null;
    }

    [HarmonyPatch(typeof(PlayVideo), "InitClips")]
    public static class BloodPlayVideoInitClipsPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref VideoClip[] clips)
        {
            clips = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Where(clips, c => c != null));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WizardController.Update — allow 40% movement during Blood counter parry.
    // rewindCount still blocks casting; this prefix injects movement input
    // so the wizard can move at reduced speed during the parry window.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(WizardController), "Update")]
    public static class BloodWizardCounterMovePatch
    {
        [HarmonyPrefix]
        public static void Prefix(WizardController __instance)
        {
            if (__instance.rewindCount <= 0) return;
            if (__instance.petrifyCount > 0) return;
            if (!__instance.active) return;
            if (__instance.input == null) return;

            Identity id = __instance.GetComponent<Identity>();
            if (id == null || !BloodDefensiveObject.activeDefensives.ContainsKey(id.owner)) return;

            Vector3 inputMove = __instance.input.GetAxis();
            if (inputMove.magnitude > 1f) inputMove.Normalize();
            if (Mathf.Pow(inputMove.x, 2f) + Mathf.Pow(inputMove.z, 2f) < 0.03f)
            {
                inputMove.x = 0f;
                inputMove.z = 0f;
            }
            Traverse.Create(__instance).Field("move").SetValue(inputMove);
        }
    }
}
