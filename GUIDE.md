# Axe Element Mod — Comprehensive Development Guide

This guide covers everything needed to update, maintain, and extend the AxeElement BepInEx mod for MageQuit.

---

## Table of Contents

1. [Project Architecture](#1-project-architecture)
2. [How the Mod Works](#2-how-the-mod-works)
3. [Building and Deploying](#3-building-and-deploying)
4. [Changing Spell Behavior](#4-changing-spell-behavior)
5. [Adding Custom Icons](#5-adding-custom-icons)
6. [Adding or Modifying Harmony Patches](#6-adding-or-modifying-harmony-patches)
7. [Common Fixes and Pitfalls](#7-common-fixes-and-pitfalls)
8. [Debugging](#8-debugging)
9. [Reference Tables](#9-reference-tables)

---

## 1. Project Architecture

### File Structure

```
AxeElement/
  Plugin.cs                          # BepInEx plugin entry point
  AxeElement/
    AxeElementModule.cs              # Module registration, wires up all Harmony patches
    AxeElementPatches.cs             # All Harmony patches + Axe constants
    AxeRegistration.cs               # Spell definitions, registration, icon/color setup
    AxePhotonExtensions.cs           # Photon networking extension methods
    Spells/
      Hatchet.cs                     # Spell initializer (spawns HatchetObject)
      HatchetObject.cs               # Runtime behavior (extends SpellObject)
      Lunge.cs / LungeObject.cs
      Cleave.cs / CleaveObject.cs
      CleaveShackle.cs               # Sub-object spawned by Cleave
      Tomahawk.cs / TomahawkObject.cs
      IronWard.cs / IronWardObject.cs
      Shatter.cs / ShatterObject.cs
      Whirlwind.cs / WhirlwindObject.cs
```

### Class Relationships

```
Plugin (BepInEx entry)
  -> ModuleManager
    -> AxeElementModule (registers Harmony patches)
      -> AxeElementPatches (patches game classes)
      -> AxeRegistration (spell definitions at runtime)

Each spell has two classes:
  Spell subclass (e.g., Hatchet : Spell)
    - Override Initialize() to spawn the spell object
    - Override AI methods for bot behavior
  SpellObject subclass (e.g., HatchetObject : SpellObject)
    - Stats: DAMAGE, RADIUS, POWER, Y_POWER, START_TIME
    - Lifecycle: Awake, Start, FixedUpdate, OnCollisionEnter, SpellObjectDeath
    - Networking: Init -> RPC -> rpcSpellObjectStart, OnPhotonSerializeView
```

### Key Concepts

- **Element.Tutorial == (Element)11** is used as the Axe slot. Ice stays fully vanilla.
- **SpellNames 146-152** are appended after the last vanilla spell (ColdFusion = 145).
- The mod dynamically expands `AvailableElements.unlockOrder` from 10 to 11 entries to include the Axe element.
- UI arrays (SelectionMenu elements, AvailableElements tablet, GameSettings.elements, color arrays) are dynamically expanded to accommodate the 11th element.
- Icons and videos are borrowed from **Metal** element spells via `SpellButton` mapping.
- Spell objects are instantiated by hijacking vanilla prefabs (e.g., `"Objects/Glaive"` for Hatchet), stripping the original component, and attaching the custom one.

---

## 2. How the Mod Works

### Boot Sequence

1. **Plugin.Awake()** — BepInEx loads the plugin, registers with ModFramework
2. **AxeElementModule.OnLoad()** — Called by ModFramework via Harmony
3. **AxeElementPatches.Initialize()** — Expands `AvailableElements.unlockOrder` to 11 entries (adds Axe/Tutorial at index 10)
4. **PatchGroup()** — Registers each Harmony patch class (listed in AxeElementModule.cs)
5. **AvailableElements.Awake (prefix)** — Clones a tablet child to create the 11th icon slot before Awake's body runs
6. **SelectionMenu.Start (prefix)** — Creates the 11th element Image in the toggle grid before Start's body runs
7. **SpellManager.Awake (postfix)** — When the game creates SpellManager, our postfix calls `AxeRegistration.RegisterSpells()`
8. **RegisterSpells()** — Creates 7 Axe `Spell` components on the SpellManager GameObject, populates spell_table, sets up AI draft priority, expands color arrays

### Spell Cast Flow

1. Player presses cast button
2. `SpellHandler` looks up the spell from `spell_table[spellName]`
3. Calls `spell.Initialize(identity, position, rotation, curve, spellIndex, selfCast, spellName)`
4. `Initialize()` in our Spell subclass:
   - Calls `GameUtility.Instantiate("Objects/<PrefabName>", ...)` to create the prefab
   - Gets the original component (e.g., `GlaiveObject`), saves its inspector fields
   - Destroys the original component with `DestroyImmediate()`
   - Adds our custom component (e.g., `HatchetObject`) and copies saved fields
   - Calls `comp.Init(identity, curve, velocity)` to start the spell

### Prefab Hijacking Pattern

Since we can't create new Unity prefabs at runtime, every spell uses an existing vanilla prefab as a base:

| Axe Spell | Prefab Used | Original Component | Why This Prefab |
|-----------|-------------|-------------------|-----------------|
| Hatchet | `Objects/Glaive` | GlaiveObject | Projectile with homing, similar shape |
| Lunge | `Objects/Steal Trap` | StealTrapObject | Dash + grab mechanics, vine visuals |
| Cleave | `Objects/Shackle` | ShackleObject | Melee AoE with ball-and-chain |
| CleaveShackle | `Objects/Shackle Object` | TetherballObjectObject | Tether sub-object for Cleave |
| Tomahawk | `Objects/Urchain` | UrchainObject | Thrown projectile with vine trail |
| IronWard | `Objects/Chainmail` | ChainmailObject | Defensive shield with reactive sparks |
| Shatter | `Objects/Reflex` | ReflexObject | Fast projectile with impact VFX |
| Whirlwind | `Objects/Double Strike` | DoubleStrikeObject | Multi-hit melee with trail VFX |

**When changing which prefab a spell uses**, update the `GameUtility.Instantiate()` string and the component type in the corresponding `Spell.Initialize()` method.

---

## 3. Building and Deploying

### Build Command

```bash
cd c:\msys64\home\david\magequit-workspace\AxeElement
dotnet build
```

Output: `bin/Debug/net472/AxeElement.dll`

### Deploying

Copy the built DLL to BepInEx plugins folder:
```
C:\Program Files (x86)\Steam\steamapps\common\MageQuit\BepInEx\plugins\AxeElement.dll
```

The `bepinex-guide.md` file in the repo root has full BepInEx setup instructions.

### Dependencies

The project references:
- `MageQuitModFramework.dll` (mod framework)
- `Assembly-CSharp.dll` (game code — from decompilation)
- `0Harmony.dll` (Harmony patching library)
- Various Unity DLLs (UnityEngine, UI, PostProcessing, Video)
- `PhotonUnityNetworking.dll` (Photon networking)
- `FMODUnity.dll` + `fmodstudioL.dll` (audio)
- `DOTween.dll` (tweening animations)

All references are in the `lib/` folder.

---

## 4. Changing Spell Behavior

### 4a. Tuning Spell Stats

Stats are set in the **SpellObject constructor**. Edit the specific `*Object.cs` file:

```csharp
// In HatchetObject.cs constructor:
public HatchetObject()
{
    DAMAGE = 7f;       // Damage dealt on hit
    RADIUS = 4f;       // AoE detection radius
    POWER = 30f;       // Knockback force
    Y_POWER = 0f;      // Upward knockback bias
    START_TIME = 1.2f; // Lifetime in seconds
    collisionRadius = 1f;
}
```

| Stat | Effect | Typical Range |
|------|--------|---------------|
| `DAMAGE` | HP removed on hit | 5–25 |
| `RADIUS` | Radius for `GetAllInSphere()` AoE checks | 2–12 |
| `POWER` | Knockback magnitude via `AddForceOwner()` | 15–95 |
| `Y_POWER` | Upward component of knockback | 0–5 |
| `START_TIME` | Seconds before auto-death | 0.2–20 |
| `collisionRadius` | Physics collision size | 0.5–2 |

### 4b. Tuning Spell Metadata (spelltable values)

Metadata is set in `AxeRegistration.cs` during `RegisterSpells()`:

```csharp
hatchet.cooldown         = 1.5f;   // Seconds between casts
hatchet.windUp           = 0.35f;  // Cast animation lead-in time
hatchet.windDown         = 0.3f;   // Recovery time after cast
hatchet.animationName    = "Attack";
hatchet.curveMultiplier  = 1.5f;   // How much joystick curve steers the spell
hatchet.initialVelocity  = 28f;    // Projectile speed at spawn
hatchet.minRange         = 0f;     // AI minimum targeting range
hatchet.maxRange         = 30f;    // AI maximum targeting range
hatchet.uses             = SpellUses.Attack;
```

| Field | What It Does | Notes |
|-------|-------------|-------|
| `cooldown` | Time between casts | Primary: 1–2s, Ultimate: 10–20s |
| `windUp` | Time from button press to spell spawn | 0.1–1.3s |
| `windDown` | Recovery after spell spawns | 0.2–0.5s |
| `animationName` | Wizard animation to play (see table below) | Must match animation_table |
| `curveMultiplier` | Steering sensitivity | 0 = no steering, 1.5–2 = normal |
| `initialVelocity` | Speed of projectile/dash | Melee/defensive: 0, Projectile: 20–45 |
| `minRange` / `maxRange` | AI targeting range | 0 = melee, 30–40 = ranged |
| `uses` | Tells AI when to use | `Attack`, `Move`, `Defend`, `Custom` flags |
| `reactivate` | Number of re-casts | 0 = single cast, 1+ = multi-cast |
| `additionalCasts` | SubSpell array for multi-cast | See Lunge in AxeRegistration.cs |
| `description` | Tooltip text shown during draft | Human-readable string |

### Available Animation Names

| Name | Used For |
|------|----------|
| `"Attack"` | Standard projectile cast |
| `"Walk"` | Walk-and-cast |
| `"Melee"` | Close-range punch/slam |
| `"FlameLeap"` | Dash/lunge forward |
| `"Somer Assault Flip"` | Spinning flip |
| `"Spell Channel"` | Channeled ability (hold) |
| `"Backhand Spell"` | Backhand throw |
| `"Spell 360"` | Spin attack |
| `"Secondary Spell"` | Alternate cast animation |
| `"Defensive"` | Shield/guard animation |
| `"Flying Forward"` | Flying charge |
| `"Katana Summon"` | Weapon summon |
| `"Sled Push"` | Push animation |
| `"Cold Fusion"` | Special fusion cast |

### 4c. Changing Collision / Hit Logic

All collision logic is in the SpellObject class. The pattern is:

```csharp
private void OnCollisionEnter(Collision collision)
{
    // 1. Only the owner processes collisions
    if (base.photonView.IsConnectedAndNotLocal()) return;

    // 2. Get the root object
    GameObject root = collision.transform.root.gameObject;

    // 3. Check if it's a valid target (Unit type, not self)
    if (!GameUtility.IdentityCompare(root, UnitType.Unit)) return;
    if (GameUtility.IdentityCompare(root, id.owner)) return;

    // 4. AoE damage
    Collider[] hits = GameUtility.GetAllInSphere(
        base.transform.position, RADIUS, id.owner, new UnitType[] { UnitType.Unit });
    foreach (Collider hit in hits)
    {
        GameObject target = hit.transform.root.gameObject;
        target.GetComponent<PhysicsBody>().AddForceOwner(
            GameUtility.GetForceVector(base.transform.position, target.transform.position, POWER));
        target.GetComponent<UnitStatus>().ApplyDamage(DAMAGE, id.owner, /* source ID */);
    }

    // 5. Sync effects to all clients
    base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All,
        new object[] { base.transform.position });

    // 6. Destroy the spell
    SpellObjectDeath();
}
```

**To change what happens on hit**, modify the body of `OnCollisionEnter()` or `Collide()` in the relevant SpellObject class.

### 4d. Changing Movement Patterns

Movement is typically in `FixedUpdate()`:

```csharp
private void FixedUpdate()
{
    // Steering
    Vector3 euler = base.transform.eulerAngles;
    euler.y += this.curve;
    base.transform.eulerAngles = euler;

    // Forward movement
    this.phys.movementVelocity = base.transform.forward * this.velocity;

    // Remote client: lerp to synced position
    if (base.photonView.IsConnectedAndNotLocal())
    {
        BaseClientCorrection();
        return;
    }

    // Death timer
    if (deathTimer < Time.time)
        SpellObjectDeath();
}
```

**For homing:** See `HatchetObject.FixedUpdate()` — rotates toward target each frame.
**For dashes:** See `LungeObject.FixedUpdate()` — drives wizard's `abilityVelocity`.
**For stationary:** See `IronWardObject` / `CleaveObject` — no movement, works on timers.

### 4e. Adding a New Spell

1. Create `NewSpell.cs` in `Spells/` — extend `Spell`, override `Initialize()` (see existing spells)
2. Create `NewSpellObject.cs` in `Spells/` — extend `SpellObject`, implement stats + behavior
3. Add a constant in `AxeElementPatches.cs` (`Axe` class): `public static readonly SpellName NewSpell = (SpellName)153;`
4. Register in `AxeRegistration.cs` — add component, set metadata, add to spellTable and axeSpellNames
5. If replacing an existing Axe spell, remove the old registration
6. Add to AI draft priority in `AxeRegistration.cs`
7. Build and test

### 4f. Changing the Prefab a Spell Uses

In the Spell class (e.g., `Hatchet.cs`), change the `GameUtility.Instantiate()` string and the component type to strip:

```csharp
// Before: borrowing Glaive
var go = GameUtility.Instantiate("Objects/Glaive", position + Spell.skillshotOffset, rotation, 0);
var original = go.GetComponent<GlaiveObject>();

// After: borrowing Fireball instead
var go = GameUtility.Instantiate("Objects/Fireball", position + Spell.skillshotOffset, rotation, 0);
var original = go.GetComponent<FireballObject>();
```

Then update the field-copying section to save/restore the new prefab's inspector fields. See the pattern in each spell's `Initialize()`.

### 4g. Modifying AI Behavior

Each Spell subclass has three AI overrides:

```csharp
// How the AI aims this spell
public override Vector3? GetAiAim(TargetComponent targetComponent, Vector3 position,
    Vector3 target, SpellUses use, ref float curve, int owner)
{
    // Return direction vector, or null if AI shouldn't cast
    return base.GetAiAim(targetComponent, position, target, use, ref curve, owner);
}

// How long the AI waits after casting (default: windUp + windDown + 0.1)
public override float GetAiRefresh(int owner)
{
    return base.GetAiRefresh(owner);
}

// Whether the AI should use this spell right now
public override bool AvailableOverride(AiController ai, int owner, SpellUses use, int reactivate)
{
    // Example: only use defensive spell when taking damage
    return use != SpellUses.Custom || ai.spellComponent.WillStillBeTakingDamageOverTime(this.windUp, 2f);
    // Example: always available
    return base.AvailableOverride(ai, owner, use, reactivate);
}
```

---

## 5. Adding Custom Icons

### Current System: Borrowing Metal Icons

Currently, the mod borrows icons from Metal element spells. In `AxeRegistration.cs`:

```csharp
// Collect Metal icons/videos by SpellButton slot
var metalIcons  = new Dictionary<SpellButton, Sprite>();
var metalVideos = new Dictionary<SpellButton, UnityEngine.Video.VideoClip>();
foreach (var kv in spellTable)
{
    if (kv.Value != null && kv.Value.element == Element.Metal)
    {
        if (kv.Value.icon != null && !metalIcons.ContainsKey(kv.Value.spellButton))
            metalIcons[kv.Value.spellButton] = kv.Value.icon;
        // ...
    }
}
// Assign to each Axe spell
AssignAssets(hatchet, SpellButton.Primary, metalIcons, metalVideos);
```

**To borrow from a different element**, change `Element.Metal` to another element (e.g., `Element.Fire`).

### Loading Custom Icons from PNG Files

To use your own sprites, you need to load PNG files at runtime using Unity's `Texture2D.LoadImage()`:

```csharp
using System.IO;
using System.Reflection;
using UnityEngine;

public static class IconLoader
{
    private static string ModFolder => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    public static Sprite LoadIcon(string filename)
    {
        string path = Path.Combine(ModFolder, "Icons", filename);
        if (!File.Exists(path))
        {
            Plugin.Log.LogWarning($"[Icons] Missing: {path}");
            return null;
        }

        byte[] data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(data);
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
    }
}
```

Then in `AxeRegistration.cs`, after creating each spell:
```csharp
hatchet.icon = IconLoader.LoadIcon("hatchet.png") ?? metalIcons.GetValueOrDefault(SpellButton.Primary);
```

**Icon file requirements:**
- Place PNG files in a subfolder next to the DLL: `BepInEx/plugins/Icons/hatchet.png`
- Recommended size: 128x128 or 256x256 pixels
- Transparent background
- One icon per spell (7 total for all slots)

### Overriding Element Icons in Scene UI

The mod dynamically creates the 11th element slot in two scene-level displays:

1. **SelectionMenu** (element toggle screen) — `AxeSelectionMenuIconPatch` (prefix on `SelectionMenu.Start`) clones the Metal icon (index 8) to create the 11th Image at index 10, positions it using grid spacing, expands the `elements` array from 10 to 11, and wires up `ClickElement(10)` on the button.

2. **AvailableElements** (round display tablet) — `AxeAvailableElementsPatch` prefix on `AvailableElements.Awake` clones the Metal tablet child (index 8) as the 11th child, then the postfix confirms the Metal sprite is applied to `elementIcons[10]`.

**To use a custom element icon instead of Metal's:**
```csharp
// In the AvailableElements postfix, instead of:
axeIcon.sprite = metalIcon.sprite;
// Load your own:
var customSprite = IconLoader.LoadIcon("axe-element.png");
if (customSprite != null)
    axeIcon.sprite = customSprite;
else
    axeIcon.sprite = metalIcon.sprite; // fallback
```

### Overriding Element Colors

Element colors are patched in several places:

| What | Where | Current Value |
|------|-------|---------------|
| Spell cooldown ring color | `AxeRegistration.cs` → `spellColors[11]` | `(0.6f, 0.6f, 0.65f)` light steel |
| Icon emission glow | `AxeRegistration.cs` → `iconEmissionColors[11]` | `(0.35f, 0.35f, 0.38f)` steel grey |
| Draft dark color | `AxeVideoSpellPlayerPatch` → `darkColors[11]` | `(0.3f, 0.3f, 0.35f)` |
| Draft light color | `AxeVideoSpellPlayerPatch` → `lightColors[11]` | `(0.7f, 0.7f, 0.75f)` |
| Stage vignette color | `AxeElementColorMappingPatch` → vignette | `(0.45f, 0.45f, 0.50f)` |
| Stage vignette intensity | `AxeElementColorMappingPatch` → vignette | `0.35f` |
| Stage bloom intensity | `AxeElementColorMappingPatch` → bloom | `2.0f` |

### Overriding Element Name

The `AxeSelectionMenuPatch` postfix replaces "Tutorial" with "Axe" in the tooltip text. If you rename the element, update the replacement string in `AxeElementPatches.cs`:

```csharp
uiText.text = uiText.text.Replace("Tutorial", "YourName");
```

---

## 6. Adding or Modifying Harmony Patches

### How Patches Are Registered

All patch classes live in `AxeElementPatches.cs`. Each is registered in `AxeElementModule.cs`:

```csharp
protected override void OnLoad(Harmony harmony)
{
    AxeElementPatches.Initialize();
    PatchGroup(harmony, typeof(AxeElementPatches));
    PatchGroup(harmony, typeof(AxeSpellManagerPatch));
    // ... etc
}
```

### Adding a New Patch

1. Add the patch class in `AxeElementPatches.cs`:

```csharp
[HarmonyPatch(typeof(TargetClass), "MethodName")]
public static class MyNewPatch
{
    [HarmonyPostfix]  // or [HarmonyPrefix]
    public static void Postfix(TargetClass __instance)
    {
        // Your code here
    }
}
```

2. Register it in `AxeElementModule.cs`:

```csharp
PatchGroup(harmony, typeof(MyNewPatch));
```

### Accessing Private Fields

Use `System.Reflection` or `HarmonyLib.Traverse`:

```csharp
// Reflection (cached, preferred for repeated access):
var field = typeof(SomeClass).GetField("privateField",
    BindingFlags.NonPublic | BindingFlags.Instance);
var value = field.GetValue(__instance);

// Traverse (convenience, fine for one-time access):
var value = Traverse.Create(__instance).Field("privateField").GetValue<SomeType>();
```

### Patch Types

| Attribute | Runs | Use Case |
|-----------|------|----------|
| `[HarmonyPrefix]` | Before original method | Block execution (return false), modify input |
| `[HarmonyPostfix]` | After original method | Modify result, run additional logic |
| `[HarmonyTranspiler]` | Rewrites IL | Surgical code changes (advanced) |

### Existing Patches

| Class | Target | Purpose |
|-------|--------|---------|
| `AxeSpellManagerPatch` | `SpellManager.Awake` | Register Axe spells |
| `AxeWizardStatusPatch` | `WizardStatus.rpcApplyDamage` | IronWard/Whirlwind damage hooks |
| `AxeGameSettingsPatch` | `GameSettings` constructor | Ensure elements array is large enough |
| `AxeElementsArrayGuardPatch` | `SelectionMenu.ShowElements` | Re-expand elements array after preset reset |
| `AxeGetAvailableGuardPatch` | `AvailableElements.GetAvailableAndIncludedElements` | Re-expand elements array before loop |
| `AxeAvailableElementsPatch` | `AvailableElements.Awake` | Expand unlockOrder, clone 11th tablet child, assign Metal icon |
| `AxeSelectionMenuIconPatch` | `SelectionMenu.Start` | Create 11th element Image, expand elements array |
| `AxeElementColorMappingPatch` | `ElementColorMapping.Start` | Override stage visuals (skips Practice Range) |
| `AxeVideoSpellPlayerPatch` | `VideoSpellPlayer.SlideIn` | Override draft UI colors |
| `AxeSelectionMenuPatch` | `SelectionMenu.ShowElementTooltip` | Replace "Tutorial" with "Axe" text |
| `AxeGetSpellDebugPatch` | `GameUtility.GetSpellByRoundAndElement` | Debug logging |
| `AxePracticeRangeGuardPatch` | `PracticeRangeManager.Awake` | Ensure Practice Range FMOD state stays correct |

---

## 7. Common Fixes and Pitfalls

### 7a. NullReferenceException in vineTransforms

**Problem:** Prefab-sourced `Transform[]` arrays can have null individual elements after the `DestroyImmediate` + `AddComponent` cycle.

**Fix:** Always null-check individual array elements:
```csharp
// BAD:
for (int i = 0; i < vineTransforms.Length; i++)
    vineTransforms[i].localScale = Vector3.zero;

// GOOD:
for (int i = 0; i < vineTransforms.Length; i++)
    if (vineTransforms[i] != null)
        vineTransforms[i].localScale = Vector3.zero;
```

This affects: `TomahawkObject`, `IronWardObject`, `LungeObject`, `CleaveShackle` — all files that use vine/chain Transform arrays.

### 7b. SpellManager Duplicate Instances

**Problem:** SpellManager uses a DontDestroyOnLoad singleton pattern. Duplicate instances are created and immediately destroyed, but our Awake postfix fires on all of them.

**Fix:** Check for the canonical instance:
```csharp
if (Globals.spell_manager != null && Globals.spell_manager != __instance)
{
    Plugin.Log.LogInfo("[AxePatch] Skipping duplicate SpellManager instance.");
    return;
}
```

### 7c. Identity Initialization

**Problem:** `SpellObject.Awake()` calls `GetComponent<Identity>()` which may return null on hijacked prefabs that don't have an Identity component.

**Fix:** Override Awake and create a new Identity if needed:
```csharp
protected override void Awake()
{
    base.Awake();
    if (id == null) id = new Identity();
}
```

All 8 spell object classes already have this fix.

### 7d. Field Copying from Prefabs

**Problem:** When replacing a prefab's component, inspector-assigned fields (transforms, particle systems, materials) must be manually saved before `DestroyImmediate()` and restored after `AddComponent()`.

**Pattern:**
```csharp
var original = go.GetComponent<OriginalType>();
// Save fields BEFORE destroying
UnityEngine.Object _impact = original.impact;
Transform[] _vines = original.vineTransforms;
// Destroy original
UnityEngine.Object.DestroyImmediate(original);
// Add custom component
var comp = go.AddComponent<CustomType>();
// Restore fields AFTER adding
comp.impact = _impact;
comp.vineTransforms = _vines;
```

**If a spell has visual glitches or missing effects**, the most likely cause is a missed field in this copy step. Check the original component's public fields in the decompiled source.

### 7e. Spelltable Values Too Low

**Problem:** `initialVelocity` and `curveMultiplier` values that are too small (e.g., 0.5) result in projectiles that barely move.

**Reference ranges:**
- `initialVelocity`: 20–45 for projectiles, 0 for melee/defensive
- `curveMultiplier`: 1.5–2.0 for projectiles, 0 for melee/defensive

Check the `spelltable-guide` file in the repo root for vanilla spell reference values.

### 7f. Wrong Animation Name

**Problem:** If `animationName` doesn't match the `animation_table` dictionary, the wizard won't play any animation on cast.

**Fix:** Use only the 14 valid names listed in [Section 4b](#4b-tuning-spell-metadata-spelltable-values).

### 7g. RPC Method Not Found

**Problem:** Photon RPC methods must be `[PunRPC]` public methods on the same component that calls `photonView.RPC()`. If the method name string doesn't match, you get silent failures in multiplayer.

**Fix:** Ensure every RPC call's method name string exactly matches a `[PunRPC]` method on the same class:
```csharp
// The string "rpcSpellObjectDeath" must match:
base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, ...);

[PunRPC]
public void rpcSpellObjectDeath() { ... }  // Must be public, must have [PunRPC]
```

### 7h. Spell Layer Timing

**Problem:** Spells that immediately collide with the caster. The spell starts on layer 0 (default) and needs to transition to layer 11 (spell layer) after a delay.

**Fix:** Call `ChangeToSpellLayerDelayed(velocity)` in `Init()`. The base `SpellObject` handles the delay. For melee spells, use `SetLayerImmediate()` or set `gameObject.layer = 0` to stay on default layer.

---

## 8. Debugging

### Logging

The mod uses BepInEx logging via `Plugin.Log`:

```csharp
Plugin.Log.LogInfo("Normal message");
Plugin.Log.LogWarning("Warning message");
Plugin.Log.LogError("Error message");
```

Logs appear in `BepInEx/LogOutput.log` and the BepInEx console (if enabled).

All 7 spell Initialize methods log:
- Entry parameters (owner, position, curve, spellIndex, curveMultiplier, velocity)
- Prefab field status (which inspector references were copied)
- Success/failure with exception details

### Enabling BepInEx Console

In `BepInEx/config/BepInEx.cfg`:
```ini
[Logging.Console]
Enabled = true
```

### Debug Patch

`AxeGetSpellDebugPatch` traces `GameUtility.GetSpellByRoundAndElement()` for the Axe element. Check logs for entries like:
```
[AxeDbg] GetSpellByRoundAndElement(Ice/Axe, round=0) => Hatchet el=Ice btn=Primary
```

### Common Log Messages

| Message | Meaning |
|---------|---------|
| `[AxePatch] Skipping duplicate SpellManager instance.` | Normal. The game creates temporary SpellManager copies. |
| `[AxeReg] RegisterSpells called. registered=False` | First-time registration (expected once). |
| `[AxeReg] RegisterSpells called. registered=True` | Re-entry on round transition. Ensures spells stay assigned. |
| `[AxeReg] spellTable is null!` | SpellManager's spell_table wasn't populated. Should not happen after the duplicate-skip fix. |
| `[AxeUI] SelectionMenu: Replaced Ice icon with Metal icon` | Icon swap succeeded. |
| `[Hatchet] Initialize FAILED: ...` | Spell spawn crashed. Check the exception for details. |

### Adding More Logging

Add `Plugin.Log.LogInfo()` calls at interesting code paths. Wrap risky code in try-catch:

```csharp
try
{
    // risky operation
}
catch (System.Exception ex)
{
    Plugin.Log.LogError($"[MySpell] Operation failed: {ex}");
}
```

---

## 9. Reference Tables

### Element Enum Values

| Element | Int Value | UnlockOrder Index |
|---------|-----------|-------------------|
| None | 0 | — |
| Fire | 1 | 0 |
| Water | 2 | 1 |
| Air | 3 | 2 |
| Earth | 4 | 3 |
| Sand | 5 | 6 |
| Nature | 6 | 4 |
| Electric | 7 | 5 |
| Steam | 8 | 7 |
| Metal | 9 | 8 |
| Ice | 10 | 9 |
| Tutorial/Axe | 11 | 10 (added by mod) |

### Axe Spell Constants

| Spell | SpellName Value | SpellButton | Prefab Source |
|-------|----------------|-------------|---------------|
| Hatchet | 146 | Primary | Objects/Glaive |
| Lunge | 147 | Movement | Objects/Steal Trap |
| Cleave | 148 | Melee | Objects/Shackle |
| Tomahawk | 149 | Secondary | Objects/Urchain |
| IronWard | 150 | Defensive | Objects/Chainmail |
| Shatter | 151 | Utility | Objects/Reflex |
| Whirlwind | 152 | Ultimate | Objects/Double Strike |

### SpellObject Lifecycle Methods

| Method | When Called | Override? |
|--------|-----------|-----------|
| `Awake()` | Component creation | `protected override void Awake()` |
| `Start()` | First frame | Regular Unity Start (not virtual) |
| `SpellObjectStart()` | Called from Start(), sets deathTimer | `public override void SpellObjectStart()` |
| `FixedUpdate()` | Every physics tick | Regular Unity FixedUpdate |
| `Update()` | Every frame | Regular Unity Update |
| `OnCollisionEnter()` | Physics collision | Regular Unity callback |
| `SpellObjectDeath()` | When spell should die | `public override void SpellObjectDeath()` |
| `BaseClientCorrection()` | Non-owner lerp each tick | `public override void BaseClientCorrection()` |
| `BaseSerialize()` | Photon sync each tick | `public override void BaseSerialize()` |
| `UpdateColor()` | Set wizard color on visuals | Inherited, call in Start()/Init() |
| `ChangeToSpellLayerDelayed()` | Set spell layer after delay | Inherited, call in Init() |

### Key Utility Methods

```csharp
// Instantiate a prefab
GameUtility.Instantiate("Objects/PrefabName", position, rotation, 0);

// Find all units in radius (excludes owner)
Collider[] hits = GameUtility.GetAllInSphere(position, radius, ownerId, new UnitType[] { UnitType.Unit });

// Get force vector for knockback
Vector3 force = GameUtility.GetForceVector(from, to, power);

// Set wizard color on a game object
GameUtility.SetWizardColor(ownerId, gameObject, false);

// Get wizard controller by owner ID
WizardController wiz = GameUtility.GetWizard(ownerId);

// Check if a game object belongs to a specific unit type
bool isUnit = GameUtility.IdentityCompare(gameObject, UnitType.Unit);

// Check if a game object belongs to a specific owner
bool isMine = GameUtility.IdentityCompare(gameObject, ownerId);
```

### Photon Extension Methods (AxePhotonExtensions.cs)

```csharp
// Check if this is a remote client's object (not ours, and we're online)
photonView.IsConnectedAndNotLocal()

// Check if we own this object (or we're master for scene objects)
photonView.IsMine()

// Send RPC with local fallback for offline play
photonView.RPCLocal(callingClass, "methodName", PhotonTargets.All, args);

// Get AI event handler (internal Globals field)
AxePhotonExtensions.AiEventHandler
```

---

## Quick Checklist: Making a Change

1. **Edit the relevant `.cs` file(s)** in `AxeElement/AxeElement/` or `AxeElement/AxeElement/Spells/`
2. **If adding a new Harmony patch**, register it in `AxeElementModule.cs`
3. **Build**: `dotnet build` from the repo root
4. **Check for 0 errors** (warnings about unused fields are fine)
5. **Copy DLL** to `BepInEx/plugins/`
6. **Test in-game**: launch MageQuit, check BepInEx console/logs
7. **Check logs** in `BepInEx/LogOutput.log` for `[Axe*]` prefixed messages
