# Axe Element Refactor Plan

## Overview
Refactor the AxeElement mod to: move Harmony patches to `AxeElementPatches.cs`, make spell objects inherit from `SpellObject`, and replace all Ice UI elements with Axe (steel/grey theme). Reuse existing game assets throughout.

---

## Step 0: Copy Plan to Repo
Copy this plan file to `c:\msys64\home\david\magequit-workspace\AxeElement\PLAN.md` so it persists as a reference.

---

## Step 1: Restructure AxeRegistration.cs and AxeElementPatches.cs

### 1a. AxeElementPatches.cs — Receives all Harmony patches + Axe constants

Move the `Axe` constants class and all three `[HarmonyPatch]` classes into `AxeElementPatches.cs`:

- `Axe` static class (Element/SpellName constants)
- `AxeSpellManagerPatch` (patches `SpellManager.Awake`)
- `AxeWizardStatusPatch` (patches `WizardStatus.rpcApplyDamage`)
- `AxeGameSettingsPatch` (patches `GameSettings` constructor)

Add an `Initialize()` method (called by `AxeElementModule.OnLoad`) for any one-time setup.

The `AxeSpellManagerPatch.Postfix` should call into `AxeRegistration` for spell table population (details, cooldowns, etc.) rather than doing it inline.

### 1b. AxeRegistration.cs — Spell definition only

Strip patches out. Keep only a static method like:
```csharp
public static class AxeRegistration
{
    public static void RegisterSpells(SpellManager manager, Dictionary<SpellName, Spell> spellTable)
    {
        // Ice reassignment to Tutorial
        // AddComponent<Hatchet>() + set cooldown/windup/etc.
        // AddComponent<Lunge>() + ...
        // ... all 7 spells
        // AI draft priority registration
    }
}
```

Called from `AxeSpellManagerPatch.Postfix` inside `AxeElementPatches.cs`.

### 1c. AxeElementModule.cs — Wire up

Update `OnLoad` to call `AxeElementPatches.Initialize()` and patch `typeof(AxeElementPatches)` (already does this). Also patch the new UI patch classes added in Step 3.

### Files modified:
- `AxeElement/AxeRegistration.cs`
- `AxeElement/AxeElementPatches.cs`
- `AxeElement/AxeElementModule.cs`

---

## Step 2: Spell Objects Inherit from SpellObject (Full Refactor)

Change all 8 spell object classes from `global::Photon.MonoBehaviour` to `SpellObject`.

### SpellObject base class provides (from decompilation):
```
Fields: DAMAGE, RADIUS, POWER, Y_POWER, START_TIME, deathTimer, collisionRadius,
        velocity, curve, id (Identity), sp (SoundPlayer),
        freshPos, correctObjectPos, correctObjectRot, lerpAmount, posMargin, rotMargin
Methods (virtual):
  - Awake()           -> Gets id and sp components
  - SpellObjectStart() -> Sets deathTimer = Time.time + START_TIME
  - BaseClientCorrection() -> Lerps to correctObjectPos/Rot
  - UpdateColor()     -> GameUtility.SetWizardColor(id.owner, gameObject, false)
  - SpellObjectCallback() -> Empty, override for collision handling
  - SpellObjectDeath() -> Empty, override for cleanup
  - BaseSerialize()   -> Sends/receives position+rotation via PhotonStream
  - ChangeToSpellLayer() -> Sets layer to 11
  - SetCorrectPosition/Rotation() -> Direct position setters
  - ChangeToSpellLayerDelayed() -> Invokes delayed layer change
```

### Per-class changes:

#### HatchetObject.cs
- Change: `global::Photon.MonoBehaviour` -> `SpellObject`
- Remove duplicate fields: `DAMAGE, RADIUS, POWER, Y_POWER, START_TIME, deathTimer, collisionRadius, velocity, curve, id, sp, correctObjectPos, correctObjectRot`
- Override `Awake()`: call `base.Awake()` then get `phys` component
- Override `SpellObjectStart()`: call `base.SpellObjectStart()`, set caster, call `UpdateColor()`
- Override `SpellObjectDeath()`: send RPC for death
- Override `BaseClientCorrection()`: use base lerp behavior
- Override `BaseSerialize()`: call `base.BaseSerialize()` (already sends pos+rot)
- Remove private `BaseClientCorrection()` and `ChangeToSpellLayerDelayed()` methods (use inherited)
- Remove `OnPhotonSerializeView` body, call `base.BaseSerialize(stream, info)` instead

#### LungeObject.cs
- Change: `global::Photon.MonoBehaviour` -> `SpellObject`
- Remove duplicate fields: `DAMAGE, RADIUS, POWER, Y_POWER, START_TIME, deathTimer, id, sp, velocity, curve, correctObjectPos`
- Override `Awake()`: call `base.Awake()`
- Make `SpellObjectStart()` override of `base.SpellObjectStart()` (already called internally)
- Override `SpellObjectDeath()`: existing logic (set dying, disable recast, send RPC)
- Override `BaseClientCorrection()`: existing lerp logic
- Override `ChangeToSpellLayer()`: `base.ChangeToSpellLayer()` (already sets layer 11)
- Replace `OnPhotonSerializeView` with `BaseSerialize` call

#### CleaveObject.cs
- Change: `global::Photon.MonoBehaviour` -> `SpellObject`
- Remove duplicate fields: `DAMAGE, RADIUS, POWER, START_TIME, deathTimer, id, sp`
- Override `Awake()`: call `base.Awake()`
- Move `Start()` initialization into an override of `SpellObjectStart()`
- Make `SpellObjectDeath()` an override
- `OnPhotonSerializeView` is empty - can call `base.BaseSerialize()` or leave empty

#### CleaveShackle.cs
- Change: `global::Photon.MonoBehaviour` -> `SpellObject`
- Remove duplicate fields: `DAMAGE, RADIUS, POWER, Y_POWER, START_TIME, deathTimer, id`
- Note: Does NOT have `sp` field, base Awake sets it to null (fine)
- Override `SpellObjectStart()` via `Start()`: call `base.SpellObjectStart()` for deathTimer
- Make `SpellObjectDeath()` an override
- `OnPhotonSerializeView` is empty - leave empty or call base

#### TomahawkObject.cs
- Change: `global::Photon.MonoBehaviour` -> `SpellObject`
- Remove duplicate fields: `DAMAGE, RADIUS, POWER, START_TIME, deathTimer, id, sp, correctObjectPos`
- Override `Awake()`: call `base.Awake()`
- In `Start()`: call `base.SpellObjectStart()` for deathTimer + coloring
- Make `SpellObjectDeath()` an override
- Override `BaseClientCorrection()`: existing lerp
- Replace `OnPhotonSerializeView` with `BaseSerialize` call

#### IronWardObject.cs
- Change: `global::Photon.MonoBehaviour` -> `SpellObject`
- Remove duplicate fields: `DAMAGE, RADIUS, POWER, START_TIME, deathTimer, id, sp`
- Override `Awake()`: call `base.Awake()`, then get sparksMain
- Make `SpellObjectDeath()` an override
- `OnPhotonSerializeView` is empty - leave empty

#### ShatterObject.cs
- Change: `global::Photon.MonoBehaviour` -> `SpellObject`
- Remove duplicate fields: `DAMAGE, RADIUS, POWER, START_TIME, deathTimer, id, sp, correctObjectPos`
- Override `Awake()`: call `base.Awake()`
- Make `SpellObjectDeath()` an override: sends RPC
- Override `BaseClientCorrection()`: existing lerp
- Replace `OnPhotonSerializeView` with `BaseSerialize` call

#### WhirlwindObject.cs
- Change: `global::Photon.MonoBehaviour` -> `SpellObject`
- Remove duplicate fields: `DAMAGE, RADIUS, POWER, START_TIME, deathTimer, id, sp`
- Override `Awake()`: call `base.Awake()`
- Make `SpellObjectDeath()` an override
- `OnPhotonSerializeView` is empty - leave empty

### Key considerations:
- `SpellObject.Awake()` does `id = GetComponent<Identity>()` and `sp = GetComponent<SoundPlayer>()`. Most spell objects currently do `new Identity()` for `id` instead of getting the component. Need to ensure `id` is properly initialized. If the prefab lacks an `Identity` component, we may need to keep `id = new Identity()` and set `id.owner` manually (assign after `base.Awake()` if base gives null).
- `SpellObject` fields are `protected` for `RADIUS, POWER, Y_POWER, START_TIME` and `public` for `DAMAGE, deathTimer, collisionRadius, velocity, curve`. The spell objects should set these in constructor or in `Awake()`.
- Naming: the existing code uses `public void SpellObjectDeath()` as a non-virtual method that sends RPCs. The base class has `public virtual void SpellObjectDeath()` which is empty. We need to make these `override` methods. Same for `SpellObjectStart()`.

### Files modified (8 files):
- `AxeElement/Spells/HatchetObject.cs`
- `AxeElement/Spells/LungeObject.cs`
- `AxeElement/Spells/CleaveObject.cs`
- `AxeElement/Spells/CleaveShackle.cs`
- `AxeElement/Spells/TomahawkObject.cs`
- `AxeElement/Spells/IronWardObject.cs`
- `AxeElement/Spells/ShatterObject.cs`
- `AxeElement/Spells/WhirlwindObject.cs`

---

## Step 3: UI Patches — Replace Ice with Axe (Steel/Grey Theme)

All UI patches go into `AxeElementPatches.cs` (or a dedicated `AxeUIPatches.cs` within the same namespace, patched from `AxeElementModule`).

### 3a. ElementColorMapping — Stage Visuals
**Patch:** `ElementColorMapping.Start()` (Postfix)

When `this.element == (Element)10`, override the post-processing values:
- Vignette color: `new Color(0.45f, 0.45f, 0.50f)` (steel grey with slight blue)
- Vignette intensity: `0.35f`
- Bloom intensity: `2.0f`
- Ambient sound: `"event:/sfx/ambience/fire"` (reuse fire ambience as placeholder)
- Color LUT: skip or reuse Metal's `"color/Metal"` as placeholder

### 3b. Globals.iconEmissionColors — Icon Glow
**Patch:** `Globals` static constructor or `SpellManager.Awake` postfix (after Globals is available)

Set `Globals.iconEmissionColors[10] = new Color(0.35f, 0.35f, 0.38f)` (steel grey emission)

### 3c. SpellManager.spellColors — Spell UI Color
**Patch:** In `AxeSpellManagerPatch.Postfix` (already patching SpellManager.Awake)

After spell registration: `__instance.spellColors[10] = new Color(0.6f, 0.6f, 0.65f)` (light steel)

### 3d. VideoSpellPlayer — Draft UI Colors
**Patch:** `VideoSpellPlayer.SlideIn()` (Prefix or Transpiler)

Override `darkColors[10]` and `lightColors[10]` with steel/grey values:
- Dark: `new Color(0.3f, 0.3f, 0.35f)`
- Light: `new Color(0.7f, 0.7f, 0.75f)`

Alternative: Patch `VideoSpellPlayer.Awake()` or `Start()` to set the array values.

### 3e. SelectionMenu — Element Name Display
**Patch:** `SelectionMenu.ShowElementTooltip()` (Prefix or Postfix)

When `elementIndex == 9` (Ice's position in unlockOrder), replace the displayed name from "Ice" to "Axe" in the tooltip text.

### 3f. SpellManager AI Draft — Remove Ice Special-Casing
**Patch:** `SpellManager.GetDraftTargetSpellIndex()` (Transpiler or Prefix)

Replace the `Element.Ice` check with `(Element)10` (Axe) so AI still gets the draft bonus for Axe spells. The existing `+60` weight logic works fine, just needs to reference the correct element.

**Patch:** `SpellManager.GetMirrorSelectIndex()` (same approach)

Replace `Element.Ice` check with `(Element)10`.

Note: Since `Axe.Element == (Element)10 == Element.Ice`, the enum value is the same. The AI logic that checks `== Element.Ice` will automatically match Axe since the integer value is identical. **No patch needed for AI draft logic** — it already works because the enum values are the same integer.

### 3g. SpellHandler — Cast Sounds
**Patch:** Post `SpellHandler.Awake()` or constructor

The cast sounds dictionary maps `Element.Ice` -> `"event:/sfx/wizard/spell-attack"`. Since `Element.Ice == (Element)10 == Axe.Element`, this already works. **No patch needed.**

### 3h. DefaultPlayerSelection — AI Quick Match
**Patch:** `DefaultPlayerSelection.Start()` (Transpiler or Prefix)

Line 41 assigns `Element.Ice` to AI player slot 3. Since the enum value is the same, **no patch needed** — AI players will get Axe spells.

### 3i. FmodController — Audio System
**Patch:** `FmodController` array containing `Element.Ice`

Same enum value, **no patch needed.**

### 3j. Element Name in ToString()
The `Element` enum's `ToString()` will still return `"Ice"` for value 10 since we can't modify the enum. Where this is displayed to users, we need string replacement patches:
- `SelectionMenu.ShowElementTooltip()` — replace "Ice" with "Axe"
- `ElementColorMapping.Start()` line 22 — `Resources.Load("color/" + this.element.ToString())` loads `"color/Ice"`. Patch to load `"color/Metal"` as fallback for the Axe stage.

### Summary of patches needed:
| Patch Target | Method | Priority |
|---|---|---|
| `ElementColorMapping.Start` | Postfix | High - stage visuals |
| `SpellManager.Awake` (extend existing) | Postfix | High - spellColors[10], iconEmissionColors[10] |
| `VideoSpellPlayer.SlideIn` or `Start` | Postfix | Medium - draft UI colors |
| `SelectionMenu.ShowElementTooltip` | Postfix | Medium - element name |

### Files modified:
- `AxeElement/AxeElementPatches.cs` (add UI patches)
- `AxeElement/AxeElementModule.cs` (register new patch classes if separate)

---

## Step 4: Fix Plugin.cs Description

Current description is wrong: `"Replaces kill scoring with placement-based 'last alive' scoring"`
Change to: `"Replaces the Ice element with a new Axe element featuring 7 unique spells"`

### Files modified:
- `Plugin.cs`

---

## Full File Change Summary

| File | Action |
|---|---|
| `Plugin.cs` | Fix description string |
| `AxeElement/AxeRegistration.cs` | Strip patches, keep spell definitions only |
| `AxeElement/AxeElementPatches.cs` | Add all patches (moved + new UI patches) |
| `AxeElement/AxeElementModule.cs` | Update OnLoad wiring |
| `AxeElement/Spells/HatchetObject.cs` | Inherit SpellObject, refactor lifecycle |
| `AxeElement/Spells/LungeObject.cs` | Inherit SpellObject, refactor lifecycle |
| `AxeElement/Spells/CleaveObject.cs` | Inherit SpellObject, refactor lifecycle |
| `AxeElement/Spells/CleaveShackle.cs` | Inherit SpellObject, refactor lifecycle |
| `AxeElement/Spells/TomahawkObject.cs` | Inherit SpellObject, refactor lifecycle |
| `AxeElement/Spells/IronWardObject.cs` | Inherit SpellObject, refactor lifecycle |
| `AxeElement/Spells/ShatterObject.cs` | Inherit SpellObject, refactor lifecycle |
| `AxeElement/Spells/WhirlwindObject.cs` | Inherit SpellObject, refactor lifecycle |

**Total: 12 files modified, 1 new file (PLAN.md)**

---

## Step 5: Build and Verify

1. Build the project: `dotnet build` from the AxeElement directory
2. Fix any compilation errors (likely field visibility or missing overrides)
3. Verify the DLL is output to the BepInEx plugins folder
4. Test in-game:
   - Element selection shows Axe where Ice was
   - Spell HUD displays all 7 Axe spells correctly
   - Stage visuals use steel/grey theme
   - All spells function (hatchet, lunge, cleave, tomahawk, ironward, shatter, whirlwind)
   - AI draft properly selects Axe spells
   - Multiplayer sync works (if testable)

---

## Key Risks and Mitigations

1. **`Identity` initialization**: `SpellObject.Awake()` calls `GetComponent<Identity>()` which may return null on prefabs that don't have one. Mitigation: Override `Awake()`, call `base.Awake()`, then check if `id` is null and create `new Identity()` if so.

2. **Field shadowing**: If we declare `new float DAMAGE = 7f` it shadows the base field. Mitigation: Don't redeclare — set the base field values in constructor or `Awake()`.

3. **Method hiding vs overriding**: Current code has `public void SpellObjectDeath()` which hides rather than overrides the base virtual. Mitigation: Add `override` keyword to all lifecycle methods.

4. **Color LUT loading**: `Resources.Load("color/Ice")` won't find an "Axe" texture. Mitigation: Patch to load `"color/Metal"` as a placeholder until a custom LUT is created.

5. **Element name display**: `Element.Ice.ToString()` still returns `"Ice"`. Mitigation: String replacement patches at display points.
