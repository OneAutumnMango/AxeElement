# Plan: Switch Axe from Ice (Element 10) to Tutorial (Element 11)

## Goal
Stop replacing Ice. Instead, make Axe occupy the Tutorial element slot (value 11). Ice stays fully vanilla. Axe appears as an 11th playable element in all UI screens. Keep Metal icons/sprites and steel/grey theme.

---

## Why This Is Non-Trivial

Tutorial (Element 11) is intentionally hidden from all gameplay UI:
- **Not in `AvailableElements.unlockOrder`** (10-element array, Fire through Ice)
- **No UI Image** in SelectionMenu grid (only 10 Images in scene)
- **No tablet child** in AvailableElements (only 10 children in scene)
- **`GameSettings.elements`** is sized to 10 (no slot for an 11th element)
- **`Globals.iconEmissionColors`** has 11 entries (0-10) — no index 11
- **`SpellManager.spellColors`** has 11 entries (0-10) — no index 11
- **`VideoSpellPlayer.darkColors/lightColors`** — indexed by `(int)Element`, likely no index 11
- **SelectionMenu presets** hardcode `new ElementInclusionMode[10]` in 6+ places

We must patch all of these to make element 11 visible and functional.

---

## Step 1: Change Axe.Element Constant

**File:** `AxeElement/AxeElementPatches.cs` (line 20)

```
BEFORE: public static readonly Element Element = (Element)10;
AFTER:  public static readonly Element Element = (Element)11;
```

Update the comment on line 15 from "Element.Ice == 10" to "Element.Tutorial == 11".

---

## Step 2: Remove Ice Spell Reassignment

**File:** `AxeElement/AxeRegistration.cs`

**Remove** the code that reassigns Ice spells to Tutorial. Ice stays untouched.

- **Lines 31-35** (re-entry block): Remove the loop that sets `element = Element.Tutorial` for Ice spells. Replace with just re-ensuring Axe spells (element 11) are in the table.
- **Lines 76-80** (first-time block): Remove the loop `foreach (kv in spellTable) if element == Element.Ice → element = Tutorial`. There are no Tutorial spells to displace either, so no displacement needed at all.
- **Lines 33**: Change the filter from `element == Element.Ice` to `element == Axe.Element` for the re-entry check.

The re-entry block should only ensure existing Axe spells (element 11) stay registered. Remove all Ice references.

---

## Step 3: Expand unlockOrder to Include Axe/Tutorial

**New patch in:** `AxeElement/AxeElementPatches.cs`

Patch a very early lifecycle method to expand the static `AvailableElements.unlockOrder` array before anything reads it.

**Target:** `AvailableElements.Awake()` Prefix (runs BEFORE Awake body)

```csharp
[HarmonyPatch(typeof(AvailableElements), "Awake")]
public static class AxeUnlockOrderPatch
{
    [HarmonyPrefix]
    public static void Prefix(AvailableElements __instance)
    {
        // Only expand once
        if (AvailableElements.unlockOrder.Length > 10) return;

        var expanded = new Element[11];
        AvailableElements.unlockOrder.CopyTo(expanded, 0);
        expanded[10] = (Element)11; // Tutorial/Axe
        AvailableElements.unlockOrder = expanded;

        // Clone the last tablet child to create the 11th icon slot
        var tablet = __instance.panel.transform.GetChild(0);
        if (tablet.childCount == 10)
        {
            var lastChild = tablet.GetChild(9); // Ice icon
            var newChild = UnityEngine.Object.Instantiate(lastChild, tablet);
            newChild.SetAsLastSibling();
            // Position it: offset from last child's position
            var lastRT = lastChild.GetComponent<RectTransform>();
            var newRT = newChild.GetComponent<RectTransform>();
            // Copy position with horizontal offset (same spacing as between other icons)
            var pos = lastRT.anchoredPosition;
            // Use same offset pattern as the grid (elements alternate rows)
            var prevPos = tablet.GetChild(8).GetComponent<RectTransform>().anchoredPosition;
            float xStep = lastRT.anchoredPosition.x - prevPos.x;
            newRT.anchoredPosition = new Vector2(pos.x + xStep, prevPos.y); // alternate row
        }
    }
}
```

**Why Prefix**: Awake's body reads `unlockOrder.Length` to size its arrays and iterates `tablet.GetChild(i)`. If we expand unlockOrder and add the child BEFORE Awake runs, Awake will naturally create `elementIcons[11]` and `locks[11]` that include our new element.

**Register in:** `AxeElementModule.cs` — add `PatchGroup(harmony, typeof(AxeUnlockOrderPatch));` BEFORE `AxeAvailableElementsIconPatch`.

---

## Step 4: Create 11th SelectionMenu Image

**Modify patch:** `AxeSelectionMenuIconPatch` in `AxeElementPatches.cs`

Currently patches `SelectionMenu.Start()` postfix to swap sprites. Extend it to also create the 11th element Image:

```csharp
[HarmonyPostfix]
public static void Postfix(SelectionMenu __instance)
{
    var elementsField = typeof(SelectionMenu).GetField("elements",
        BindingFlags.Public | BindingFlags.Instance);
    var elements = elementsField.GetValue(__instance) as Image[];
    if (elements == null || elements.Length < 10) return;

    // Create 11th element if not already present
    if (elements.Length == 10)
    {
        // Clone the Metal icon (index 8) as template for Axe
        var template = elements[8]; // Metal
        var newObj = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
        var newImage = newObj.GetComponent<Image>();

        // Position: offset from last element
        var lastRT = elements[9].GetComponent<RectTransform>();
        var prevRT = elements[8].GetComponent<RectTransform>();
        var newRT = newObj.GetComponent<RectTransform>();
        float xStep = lastRT.anchoredPosition.x - prevRT.anchoredPosition.x;
        newRT.anchoredPosition = new Vector2(lastRT.anchoredPosition.x + xStep, prevRT.anchoredPosition.y);

        // Set the Metal sprite on it
        newImage.sprite = elements[8].sprite; // Metal icon

        // Expand the elements array
        var expanded = new Image[11];
        elements.CopyTo(expanded, 0);
        expanded[10] = newImage;
        elementsField.SetValue(__instance, expanded);

        // Set up child indicators (Included/Banned/Lock)
        newObj.transform.GetChild(0).gameObject.SetActive(false);
        newObj.transform.GetChild(1).gameObject.SetActive(false);
        if (newObj.transform.childCount > 2)
            newObj.transform.GetChild(2).gameObject.SetActive(false);

        // Make it interactable
        var btn = newObj.GetComponent<Button>();
        if (btn != null) btn.interactable = true;
    }
}
```

**Remove** the old Ice->Metal sprite swap logic (indices [8] and [9]) since Ice is no longer ours.

---

## Step 5: Expand GameSettings.elements Array

**Modify:** `AxeGameSettingsPatch` in `AxeElementPatches.cs`

Change the `needed` value from `unlockOrder.Length` to explicitly 11:

```csharp
int needed = AvailableElements.unlockOrder != null
    ? AvailableElements.unlockOrder.Length  // Now 11 after our patch
    : 12;
```

Also need to handle SelectionMenu presets that hardcode `new ElementInclusionMode[10]`:

**New patch:** `SelectionMenu.ShowPreset()` Postfix

```csharp
[HarmonyPatch(typeof(SelectionMenu), "ShowPreset")]
public static class AxePresetSizePatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (PlayerManager.gameSettings.elements != null &&
            PlayerManager.gameSettings.elements.Length < 11)
        {
            var expanded = new ElementInclusionMode[11];
            PlayerManager.gameSettings.elements.CopyTo(expanded, 0);
            PlayerManager.gameSettings.elements = expanded;
        }
    }
}
```

---

## Step 6: Expand Color Arrays

**Modify:** `AxeRegistration.cs` — UI colors section

```csharp
// Expand spellColors if needed (vanilla is 11 entries, indices 0-10, need index 11)
if (manager.spellColors != null && manager.spellColors.Length <= 11)
{
    var expanded = new Color[12];
    manager.spellColors.CopyTo(expanded, 0);
    manager.spellColors = expanded;
}
manager.spellColors[11] = new Color(0.6f, 0.6f, 0.65f); // steel

// Expand iconEmissionColors (vanilla is 11 entries, need index 11)
if (Globals.iconEmissionColors != null && Globals.iconEmissionColors.Length <= 11)
{
    var expanded = new Color[12];
    Globals.iconEmissionColors.CopyTo(expanded, 0);
    Globals.iconEmissionColors = expanded;
}
Globals.iconEmissionColors[11] = new Color(0.35f, 0.35f, 0.38f); // steel grey
```

Change all `[10]` index accesses to `[11]` and `> 10` checks to `> 11`.

**Modify:** `AxeVideoSpellPlayerPatch` in `AxeElementPatches.cs`

Expand darkColors/lightColors arrays and set index 11:

```csharp
// Expand and set darkColors[11]
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

// Same for lightColors[11]
```

---

## Step 7: Update Element Name Display

**Modify:** `AxeSelectionMenuPatch` (ShowElementTooltip postfix)

Change string replacement from `"Ice"` to `"Tutorial"`:

```csharp
uiText.text = uiText.text.Replace("Tutorial", "Axe");
```

---

## Step 8: Update AvailableElements Icon Patch

**Modify:** `AxeAvailableElementsIconPatch` in `AxeElementPatches.cs`

Since unlockOrder now has 11 entries with Axe at index 10, update the icon swap:

```csharp
// Index 8 = Metal, Index 10 = Tutorial/Axe in unlockOrder
if (elementIcons.Length > 10)
{
    var metalIcon = elementIcons[8];
    var axeIcon = elementIcons[10];
    if (metalIcon != null && axeIcon != null && metalIcon.sprite != null)
        axeIcon.sprite = metalIcon.sprite;
}
```

---

## Step 9: Update ElementColorMapping Patch

**File:** `AxeElementPatches.cs` — `AxeElementColorMappingPatch`

The check `if (__instance.element != Axe.Element)` already uses `Axe.Element` which will now be `(Element)11`. The patch handles stage visuals when the Axe element stage loads. However, since Tutorial has no stage in the game, this patch may never fire. **Keep it for safety** — if a stage is assigned element 11, it will get steel/grey visuals.

Add a guard to skip when in Practice Range:
```csharp
if (Globals.practice_range_manager != null) return;
```

---

## Step 10: Update Debug Patch

**File:** `AxeElementPatches.cs` — `AxeGetSpellDebugPatch`

Update log message strings from "Ice/Axe" to "Tutorial/Axe":
```csharp
Plugin.Log.LogInfo($"[AxeDbg] GetSpellByRoundAndElement(Tutorial/Axe, round={round}) => ...");
```

---

## Step 11: Update Plugin Description

**File:** `Plugin.cs` (line 28)

```
BEFORE: "Replaces the Ice element with a new Axe element featuring 7 unique spells"
AFTER:  "Adds a new Axe element featuring 7 unique spells"
```

---

## Step 12: Update Comments Throughout

Update all comments that reference Ice/slot 10 to reference Tutorial/slot 11. This is cosmetic but important for maintainability.

---

## Step 13: Update GUIDE.md

Reflect the new architecture in the guide:
- Axe.Element = (Element)11 (Tutorial slot)
- Ice is untouched
- unlockOrder has 11 entries
- Dynamically created 11th UI elements

---

## Step 14: Preemptive Practice Range FMOD Patch

**New patch in:** `AxeElement/AxeElementPatches.cs`

The Practice Range calls `FmodController.FadeToBattle(Element.Tutorial)` in its Awake. Since Tutorial == (Element)11 == Axe.Element, add a guard to ensure Practice Range music stays clean:

```csharp
[HarmonyPatch(typeof(PracticeRangeManager), "Awake")]
public static class AxePracticeRangeGuardPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        // Ensure Practice Range FMOD state stays on Tutorial's music event
        if (FmodController.current != null)
        {
            FmodController.current.currentElement = (Element)11;
            Plugin.Log.LogInfo("[AxeUI] Practice Range guard: confirmed currentElement = Tutorial/11");
        }
    }
}
```

**Register in:** `AxeElementModule.cs` — add `PatchGroup(harmony, typeof(AxePracticeRangeGuardPatch));`

---

## Files Modified

| File | Changes |
|------|---------|
| `AxeElement/AxeElementPatches.cs` | Change Axe.Element to 11, update all patches for index 11, add AxeUnlockOrderPatch, add AxePresetSizePatch, add AxePracticeRangeGuardPatch, modify SelectionMenu/AvailableElements icon patches, update tooltip replacement, add Practice Range guard to ElementColorMapping |
| `AxeElement/AxeRegistration.cs` | Remove Ice spell reassignment, change color array indices from [10] to [11], expand array sizes |
| `AxeElement/AxeElementModule.cs` | Register new patches (AxeUnlockOrderPatch, AxePresetSizePatch, AxePracticeRangeGuardPatch) |
| `Plugin.cs` | Update description string |
| `GUIDE.md` | Update documentation |

---

## Execution Order (Critical)

Patches must register in this order to work correctly:

1. **AxeUnlockOrderPatch** (AvailableElements.Awake Prefix) — Expands unlockOrder and clones tablet child BEFORE Awake runs
2. **AxeAvailableElementsIconPatch** (AvailableElements.Awake Postfix) — Sets Metal sprite on index 10 AFTER Awake populates arrays
3. **AxeGameSettingsPatch** (GameSettings constructor Postfix) — Expands elements array to 11
4. **AxePresetSizePatch** (SelectionMenu.ShowPreset Postfix) — Fixes preset array sizes
5. **AxeSelectionMenuIconPatch** (SelectionMenu.Start Postfix) — Creates 11th Image, sets sprite
6. **AxeSpellManagerPatch** (SpellManager.Awake Postfix) — Registers Axe spells, expands color arrays
7. **AxePracticeRangeGuardPatch** (PracticeRangeManager.Awake Postfix) — Practice Range FMOD guard
8. Everything else (order doesn't matter)

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Cloned UI element has wrong position | Axe icon overlaps or is off-screen | Hardcode position next to Metal/Ice; fall back to grid-spacing calculation |
| SelectionMenu.ChangeElement() overflow | Cycling past 10 elements crashes | It uses `LastUnlockedIndex + 5` which caps at unlockOrder.Length; should work if unlockOrder is 11 |
| Preset reset shrinks array to 10 | GameSettings.elements[10] out of bounds | AxePresetSizePatch postfix re-expands after every preset change |
| Practice Range music affected | FMOD Tutorial music changes | Preemptive guard patch resets currentElement; ElementColorMapping skips in Practice Range |
| darkColors/lightColors Inspector arrays too small | IndexOutOfRange on draft screen | Expand arrays in VideoSpellPlayer prefix before SlideIn accesses them |
| Game serialization of GameSettings | Online lobby transmits elements[10] | Elements array is serialized; expanded slot should sync naturally since both players have the mod |

---

## Verification

1. `dotnet build` — 0 errors
2. Launch game, check SelectionMenu: 11 element icons visible, Axe shows Metal icon at index 10
3. Toggle Axe inclusion mode (Possible/Included/Banned) — no crash
4. Select preset (Standard, Competitive, etc.) — no crash, Axe slot preserved
5. Start game with Axe included: AvailableElements tablet shows 11 icons
6. Draft round: Axe spells appear with Metal icons/videos and steel/grey colors
7. Cast all 7 Axe spells — same behavior as before
8. Ice element works normally (all vanilla Ice spells functional)
9. Check BepInEx logs for `[AxeUI]` and `[AxeReg]` success messages
