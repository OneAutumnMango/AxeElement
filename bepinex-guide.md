# Guide: Modding MageQuit with BepInEx + Harmony

This guide covers how to add new elements, spells, and modify game behavior using BepInEx and Harmony — without modifying the original game DLLs.

---

## Prerequisites

- **BepInEx 5.x** — Already installed in the MageQuit directory (`winhttp.dll` + `BepInEx/` folder)
- **Visual Studio** or **VS Code** with C# support
- **.NET Framework 4.x** targeting pack
- The decompiled source code (this workspace) as reference

### Game Install Structure

```
MageQuit/
├── MageQuit.exe
├── MageQuit_Data/
│   └── Managed/
│       └── Assembly-CSharp.dll    ← The game code (reference this)
├── MonoBleedingEdge/              ← Mono runtime (good — no IL2CPP)
├── BepInEx/
│   ├── core/                      ← BepInEx runtime
│   ├── plugins/                   ← YOUR MODS GO HERE
│   ├── patchers/                  ← Preloader patchers
│   └── config/
├── winhttp.dll                    ← BepInEx proxy loader
├── doorstop_config.ini            ← BepInEx doorstop config
└── magequitdecompilation/         ← Decompiled reference source
```

---

## Part 1: Setting Up a BepInEx Plugin Project

### 1a. Create a Class Library Project

```bash
dotnet new classlib -n MageQuitMod -f net46
cd MageQuitMod
```

Or in Visual Studio: File → New Project → Class Library (.NET Framework 4.6+).

### 1b. Add References

Add these DLL references from the game directory:

| DLL | Location |
|-----|----------|
| `Assembly-CSharp.dll` | `MageQuit_Data/Managed/` |
| `UnityEngine.dll` | `MageQuit_Data/Managed/` |
| `UnityEngine.CoreModule.dll` | `MageQuit_Data/Managed/` |
| `UnityEngine.PhysicsModule.dll` | `MageQuit_Data/Managed/` |
| `Photon3Unity3D.dll` | `MageQuit_Data/Managed/` |
| `PhotonUnityNetworking.dll` | `MageQuit_Data/Managed/` |
| `BepInEx.dll` | `BepInEx/core/` |
| `0Harmony.dll` | `BepInEx/core/` |

In your `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net46</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- Adjust paths to your MageQuit install -->
    <Reference Include="Assembly-CSharp">
      <HintPath>..\MageQuit_Data\Managed\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>..\MageQuit_Data\Managed\UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>..\MageQuit_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.PhysicsModule">
      <HintPath>..\MageQuit_Data\Managed\UnityEngine.PhysicsModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Photon3Unity3D">
      <HintPath>..\MageQuit_Data\Managed\Photon3Unity3D.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="PhotonUnityNetworking">
      <HintPath>..\MageQuit_Data\Managed\PhotonUnityNetworking.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="BepInEx">
      <HintPath>..\BepInEx\core\BepInEx.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>..\BepInEx\core\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### 1c. Create the Plugin Entry Point

```csharp
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace MageQuitMod
{
    [BepInPlugin("com.yourname.magequitmod", "MageQuit Mod", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("MageQuit Mod loaded!");

            harmony = new Harmony("com.yourname.magequitmod");
            harmony.PatchAll(); // Auto-patches all [HarmonyPatch] classes in this assembly
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
```

### 1d. Build and Install

```bash
dotnet build -c Release
```

Copy the output DLL to `MageQuit/BepInEx/plugins/`:

```
MageQuit/BepInEx/plugins/MageQuitMod.dll
```

Launch the game. Check `BepInEx/LogOutput.log` to verify your plugin loaded.

---

## Part 2: Adding a New Element via Harmony

### The Problem

The `Element` enum is compiled into the DLL. You can't add new enum values at runtime in C#. But you can work around this.

### Strategy: Repurpose or Extend

**Option A — Repurpose `Element.Tutorial` (easiest):**

`Element.Tutorial` (value `11`) exists but isn't used as a playable element. Hijack it:

```csharp
public static class ModdedElement
{
    // Tutorial = 11 in the original enum
    public static readonly Element Shadow = Element.Tutorial;
}
```

**Option B — Use integer casting (for truly new elements):**

Since `Element` is just an int enum, you can cast arbitrary integers:

```csharp
public static class ModdedElement
{
    public static readonly Element Shadow = (Element)12;
    public static readonly Element Arcane = (Element)13;
}
```

This works because C# enums are just integers. The game code uses `(int)element` for array indexing, so you must patch all arrays to be large enough.

### 2a. Patch `AvailableElements.unlockOrder`

The unlock order is a static array. Patch it to include your new element:

```csharp
[HarmonyPatch(typeof(AvailableElements))]
public static class AvailableElementsPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(MethodType.StaticConstructor)]
    public static void Postfix_StaticCtor()
    {
        // Original has 10 elements. Add our new one.
        var original = AvailableElements.unlockOrder;
        var extended = new Element[original.Length + 1];
        original.CopyTo(extended, 0);
        extended[extended.Length - 1] = ModdedElement.Shadow;
        AvailableElements.unlockOrder = extended;

        Plugin.Log.LogInfo($"Extended unlockOrder to {extended.Length} elements");
    }
}
```

### 2b. Patch `GameSettings.elements` Array Size

```csharp
[HarmonyPatch(typeof(GameSettings))]
public static class GameSettingsPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(MethodType.Constructor)]
    public static void Postfix_Ctor(GameSettings __instance)
    {
        // Expand from 10 to 11 entries
        if (__instance.elements.Length < 11)
        {
            var expanded = new ElementInclusionMode[11];
            __instance.elements.CopyTo(expanded, 0);
            __instance.elements = expanded;
        }
    }
}
```

### 2c. Patch `Globals.iconEmissionColors`

```csharp
[HarmonyPatch(typeof(Globals))]
public static class GlobalsPatch
{
    // Patch wherever iconEmissionColors is first assigned
    // Since it's a static field, patch the class's static constructor or first use
    public static void EnsureExtended()
    {
        if (Globals.iconEmissionColors != null && Globals.iconEmissionColors.Length < 13)
        {
            var extended = new UnityEngine.Color[13];
            Globals.iconEmissionColors.CopyTo(extended, 0);
            // Add colors for your new elements
            extended[12] = new UnityEngine.Color(0.3f, 0f, 0.5f); // Shadow purple
            Globals.iconEmissionColors = extended;
        }
    }
}
```

### 2d. Patch `DefaultPlayerSelection` Random Range

```csharp
[HarmonyPatch(typeof(DefaultPlayerSelection), "Start")]
public static class DefaultPlayerSelectionPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // Change Random.Range(1, 11) to Random.Range(1, 12)
        var codes = new List<CodeInstruction>(instructions);
        for (int i = 0; i < codes.Count - 1; i++)
        {
            // Look for ldc.i4.s 11 (the upper bound) preceded by ldc.i4.1
            if (codes[i].opcode == OpCodes.Ldc_I4_1 &&
                codes[i + 1].opcode == OpCodes.Ldc_I4_S &&
                (sbyte)codes[i + 1].operand == 11)
            {
                codes[i + 1].operand = (sbyte)12;
                Plugin.Log.LogInfo("Patched DefaultPlayerSelection random range");
            }
        }
        return codes;
    }
}
```

Alternatively, use a simpler prefix patch:

```csharp
[HarmonyPatch(typeof(DefaultPlayerSelection), "Start")]
public static class DefaultPlayerSelectionPatch
{
    [HarmonyPrefix]
    public static bool Prefix(DefaultPlayerSelection __instance)
    {
        // Reimplement Start() with updated range
        // ... (copy decompiled logic, change 11 to 12) ...
        return false; // Skip original
    }
}
```

### 2e. Patch ElementColorMapping

```csharp
[HarmonyPatch(typeof(ElementColorMapping), "Awake")]
public static class ElementColorMappingPatch
{
    [HarmonyPostfix]
    public static void Postfix(ElementColorMapping __instance)
    {
        // ElementColorMapping stores arrays of colors indexed by (int)Element.
        // Use reflection to extend each color array on the instance.
        // Check the decompiled source for exact field names.
    }
}
```

---

## Part 3: Adding a New Spell via Harmony

### Strategy

Since `SpellManager.Awake()` discovers spells via `GetComponents<Spell>()` on its own GameObject, we need to:

1. Dynamically add our `Spell` MonoBehaviour component to the SpellManager GameObject
2. Register it in `spell_table`
3. Add it to `ai_draft_priority`

### 3a. The SpellName Problem

Like `Element`, `SpellName` is a compiled enum. Use the same integer casting trick:

```csharp
public static class ModdedSpells
{
    // Next value after ColdFusion (69)
    public static readonly SpellName ShadowBolt = (SpellName)70;
    public static readonly SpellName ShadowStep = (SpellName)71;
    public static readonly SpellName ShadowStrike = (SpellName)72;
    public static readonly SpellName ShadowOrb = (SpellName)73;
    public static readonly SpellName ShadowShield = (SpellName)74;
    public static readonly SpellName ShadowBind = (SpellName)75;
    public static readonly SpellName ShadowStorm = (SpellName)76;
}
```

### 3b. Define Your Spell Classes in the Mod

```csharp
// Your Spell subclass — lives in your mod DLL
public class ShadowBolt : Spell
{
    public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
        float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
    {
        // We'll need a custom instantiation path since we can't add
        // prefabs to Resources at runtime. See "Asset Loading" below.
        var obj = ModAssetLoader.Instantiate("ShadowBolt", position + Spell.skillshotOffset, rotation);
        var spellObj = obj.GetComponent<ShadowBoltObject>();
        spellObj.Init(identity.owner, curve * this.curveMultiplier, this.initialVelocity);
    }
}
```

### 3c. Inject Spell into SpellManager

Patch `SpellManager.Awake()` to add your spells after initialization:

```csharp
[HarmonyPatch(typeof(SpellManager), "Awake")]
public static class SpellManagerAwakePatch
{
    [HarmonyPostfix]
    public static void Postfix(SpellManager __instance)
    {
        // Access the private spell_table via reflection or Traverse
        var spellTable = Traverse.Create(__instance).Field("spell_table")
            .GetValue<Dictionary<SpellName, Spell>>();

        // Add our Spell component to the SpellManager GameObject
        var shadowBolt = __instance.gameObject.AddComponent<ShadowBolt>();

        // Configure it (normally done via Inspector)
        shadowBolt.spellName = ModdedSpells.ShadowBolt;
        shadowBolt.element = ModdedElement.Shadow;
        shadowBolt.spellButton = SpellButton.Primary;
        shadowBolt.cooldown = 1.5f;
        shadowBolt.windUp = 0.35f;
        shadowBolt.windDown = 0.3f;
        shadowBolt.curveMultiplier = 1f;
        shadowBolt.initialVelocity = 0.5f;
        shadowBolt.minRange = 3f;
        shadowBolt.maxRange = 30f;
        shadowBolt.uses = SpellUses.Attack;
        shadowBolt.description = "A bolt of shadow energy";
        // icon and video must be loaded separately (see Asset Loading)

        // Register in spell_table
        spellTable[ModdedSpells.ShadowBolt] = shadowBolt;

        // Repeat for all 7 spells...

        Plugin.Log.LogInfo($"Registered {7} shadow spells");
    }
}
```

### 3d. Add to AI Draft Priority

```csharp
[HarmonyPatch(typeof(SpellManager), "Awake")]
public static class SpellManagerAIDraftPatch
{
    // Run after the spell registration patch
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    public static void Postfix(SpellManager __instance)
    {
        var aiDraftPriority = Traverse.Create(__instance).Field("ai_draft_priority")
            .GetValue<Dictionary<SpellButton, List<SpellName>>>();

        // Add to each slot's priority list (at the end = lowest AI priority)
        if (aiDraftPriority.ContainsKey(SpellButton.Primary))
            aiDraftPriority[SpellButton.Primary].Add(ModdedSpells.ShadowBolt);
        if (aiDraftPriority.ContainsKey(SpellButton.Movement))
            aiDraftPriority[SpellButton.Movement].Add(ModdedSpells.ShadowStep);
        if (aiDraftPriority.ContainsKey(SpellButton.Melee))
            aiDraftPriority[SpellButton.Melee].Add(ModdedSpells.ShadowStrike);
        if (aiDraftPriority.ContainsKey(SpellButton.Secondary))
            aiDraftPriority[SpellButton.Secondary].Add(ModdedSpells.ShadowOrb);
        if (aiDraftPriority.ContainsKey(SpellButton.Defensive))
            aiDraftPriority[SpellButton.Defensive].Add(ModdedSpells.ShadowShield);
        if (aiDraftPriority.ContainsKey(SpellButton.Utility))
            aiDraftPriority[SpellButton.Utility].Add(ModdedSpells.ShadowBind);
        if (aiDraftPriority.ContainsKey(SpellButton.Ultimate))
            aiDraftPriority[SpellButton.Ultimate].Add(ModdedSpells.ShadowStorm);
    }
}
```

---

## Part 4: Asset Loading (Prefabs, Sprites, Audio)

The biggest challenge with BepInEx modding is that you can't add files to `Resources/` at runtime. Here are the approaches:

### Option A: AssetBundle (Recommended)

Create an AssetBundle in the Unity Editor containing your prefabs, sprites, and materials.

**In Unity Editor:**
1. Create a new Unity project matching MageQuit's Unity version
2. Create your prefabs with all required components
3. Mark them for an AssetBundle (Inspector → bottom → AssetBundle dropdown → "shadowspells")
4. Build AssetBundles: `BuildPipeline.BuildAssetBundles("Assets/Bundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64)`
5. Place the bundle file in `BepInEx/plugins/MageQuitMod/assets/`

**Loading in your mod:**

```csharp
public static class ModAssetLoader
{
    private static AssetBundle bundle;
    private static Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

    public static void LoadBundle()
    {
        string path = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "assets", "shadowspells");
        bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null)
        {
            Plugin.Log.LogError("Failed to load asset bundle!");
            return;
        }
        Plugin.Log.LogInfo("Asset bundle loaded successfully");
    }

    public static GameObject Instantiate(string prefabName, Vector3 position, Quaternion rotation)
    {
        if (!prefabCache.TryGetValue(prefabName, out var prefab))
        {
            prefab = bundle.LoadAsset<GameObject>(prefabName);
            prefabCache[prefabName] = prefab;
        }

        if (Globals.online)
        {
            // For networked games, you'll need a custom instantiation approach
            // since PhotonNetwork.Instantiate requires Resources/ prefabs
            // See "Networking Workaround" below
            return UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
        else
        {
            return UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
    }

    public static Sprite LoadSprite(string name)
    {
        return bundle.LoadAsset<Sprite>(name);
    }
}
```

### Option B: Runtime Prefab Construction (No Unity Editor Needed)

Build prefabs from code using primitive meshes and existing game particles:

```csharp
public static class PrefabFactory
{
    private static Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();

    public static void CreateShadowBoltTemplate()
    {
        // Create a root object (inactive, used as template)
        var template = new GameObject("ShadowBolt_Template");
        template.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(template);

        // Add required components
        var identity = template.AddComponent<Identity>();
        identity.type = UnitType.Projectile;

        template.AddComponent<SoundPlayer>();
        template.AddComponent<ShadowBoltObject>();

        var physBody = template.AddComponent<PhysicsBody>();
        physBody.gravity = 0f;

        var collider = template.AddComponent<SphereCollider>();
        collider.radius = 0.5f;

        var rb = template.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // Visual: use a simple sphere mesh
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.SetParent(template.transform);
        visual.transform.localScale = Vector3.one * 0.5f;
        visual.transform.localPosition = Vector3.zero;
        UnityEngine.Object.Destroy(visual.GetComponent<Collider>());

        // Color it dark purple
        var renderer = visual.GetComponent<Renderer>();
        renderer.material.color = new Color(0.3f, 0f, 0.5f);

        // Optional: add a trail
        var trail = template.AddComponent<TrailRenderer>();
        trail.startWidth = 0.3f;
        trail.endWidth = 0f;
        trail.time = 0.5f;
        trail.material = new Material(Shader.Find("Particles/Standard Unlit"));
        trail.startColor = new Color(0.5f, 0f, 0.8f, 1f);
        trail.endColor = new Color(0.5f, 0f, 0.8f, 0f);

        templates["ShadowBolt"] = template;
    }

    public static GameObject Instantiate(string name, Vector3 position, Quaternion rotation)
    {
        if (!templates.TryGetValue(name, out var template))
        {
            Plugin.Log.LogError($"No template found for {name}");
            return null;
        }

        var instance = UnityEngine.Object.Instantiate(template, position, rotation);
        instance.SetActive(true);
        return instance;
    }
}
```

### Networking Workaround

`GameUtility.Instantiate()` uses `PhotonNetwork.Instantiate()` in online mode, which **requires** prefabs in a `Resources/` folder. Workarounds:

**Option 1 — Patch `GameUtility.Instantiate`:**

```csharp
[HarmonyPatch(typeof(GameUtility), "Instantiate",
    new Type[] { typeof(string), typeof(Vector3), typeof(Quaternion), typeof(byte) })]
public static class GameUtilityInstantiatePatch
{
    [HarmonyPrefix]
    public static bool Prefix(string name, Vector3 position, Quaternion rotation, byte group,
        ref GameObject __result)
    {
        // Intercept our custom prefab names
        if (name.StartsWith("Objects/Shadow"))
        {
            if (Globals.online)
            {
                // In online mode, use manual RPC-based sync instead of PhotonNetwork.Instantiate
                __result = PrefabFactory.Instantiate(name.Replace("Objects/", ""), position, rotation);
                // You'll need to manually add a PhotonView and handle ownership
                // This is the hardest part of online modding
            }
            else
            {
                __result = PrefabFactory.Instantiate(name.Replace("Objects/", ""), position, rotation);
            }
            return false; // Skip original
        }
        return true; // Original method handles vanilla spells
    }
}
```

**Option 2 — Copy prefab to Resources at startup (hacky but effective):**

```csharp
// Register custom prefabs with Photon's resource cache
// This only works if PUN has a prefab pool system
PhotonNetwork.PrefabPool.RegisterPrefab("Objects/ShadowBolt", templateObject);
```

**Option 3 — Offline-only mod (simplest):**

Skip networking entirely. Only support local/bot games:

```csharp
public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
    float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
{
    // Always use local instantiation
    var obj = PrefabFactory.Instantiate("ShadowBolt", position + Spell.skillshotOffset, rotation);
    obj.GetComponent<ShadowBoltObject>().Init(identity.owner, curve * this.curveMultiplier, this.initialVelocity);
}
```

---

## Part 5: Modifying Existing Spells (Stat Tweaking)

The easiest type of mod — change existing spell stats:

### 5a. Patch a SpellObject Constructor

```csharp
[HarmonyPatch(typeof(FireballObject), MethodType.Constructor)]
public static class FireballBuffPatch
{
    [HarmonyPostfix]
    public static void Postfix(FireballObject __instance)
    {
        __instance.DAMAGE = 15f;  // Was 10
        // RADIUS, POWER, etc. are protected — use Traverse:
        Traverse.Create(__instance).Field("RADIUS").SetValue(6f);   // Was 4
        Traverse.Create(__instance).Field("POWER").SetValue(60f);   // Was 35
    }
}
```

### 5b. Change Cooldowns via SpellManager

```csharp
[HarmonyPatch(typeof(SpellManager), "Awake")]
public static class CooldownModPatch
{
    [HarmonyPostfix]
    public static void Postfix(SpellManager __instance)
    {
        var spellTable = Traverse.Create(__instance).Field("spell_table")
            .GetValue<Dictionary<SpellName, Spell>>();

        if (spellTable.TryGetValue(SpellName.Fireball, out var fireball))
        {
            fireball.cooldown = 0.5f;  // Rapid fire!
        }
    }
}
```

### 5c. Modify Spell Behavior

```csharp
// Make Fireball explode with double radius
[HarmonyPatch(typeof(FireballObject), "OnCollisionEnter")]
public static class FireballExplosionPatch
{
    [HarmonyPrefix]
    public static void Prefix(FireballObject __instance)
    {
        // Double the radius right before collision is processed
        Traverse.Create(__instance).Field("RADIUS").SetValue(8f);
    }
}
```

---

## Part 6: Modifying Game Settings

### 6a. Change Default Round Count

```csharp
[HarmonyPatch(typeof(GameSettings), MethodType.Constructor)]
public static class GameSettingsPatch
{
    [HarmonyPostfix]
    public static void Postfix(GameSettings __instance)
    {
        __instance.numberOfRounds = 15;
    }
}
```

### 6b. Force All Elements Available

```csharp
[HarmonyPatch(typeof(AvailableElements), "GetRandomAvailable")]
public static class AllElementsPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ref Element[] __result)
    {
        // Return all elements instead of random 4
        __result = (Element[])AvailableElements.unlockOrder.Clone();
        return false;
    }
}
```

### 6c. Custom Health Values

```csharp
[HarmonyPatch(typeof(UnitStatus))]
public static class HealthPatch
{
    // Find the method that initializes health and patch it
    [HarmonyPostfix]
    [HarmonyPatch("SetupHealth")] // Check decompiled source for exact method name
    public static void Postfix(UnitStatus __instance)
    {
        // Double everyone's health
        Traverse.Create(__instance).Field("maxHealth").SetValue(200f);
        Traverse.Create(__instance).Field("health").SetValue(200f);
    }
}
```

---

## Part 7: Complete Plugin Example

A full plugin that adds a Shadow element with one spell (ShadowBolt):

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MageQuitShadowMod
{
    // ═══════════════════════════════════════
    // Constants
    // ═══════════════════════════════════════

    public static class Shadow
    {
        public static readonly Element Element = Element.Tutorial; // Repurpose Tutorial slot
        public static readonly SpellName Bolt = (SpellName)70;
        public static readonly SpellName Step = (SpellName)71;
        public static readonly SpellName Strike = (SpellName)72;
        public static readonly SpellName Orb = (SpellName)73;
        public static readonly SpellName Shield = (SpellName)74;
        public static readonly SpellName Bind = (SpellName)75;
        public static readonly SpellName Storm = (SpellName)76;
    }

    // ═══════════════════════════════════════
    // Plugin Entry Point
    // ═══════════════════════════════════════

    [BepInPlugin("com.modder.magequit.shadow", "Shadow Element Mod", "1.0.0")]
    public class ShadowPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("Shadow Element Mod loading...");

            // Build runtime prefab templates
            PrefabFactory.Initialize();

            harmony = new Harmony("com.modder.magequit.shadow");
            harmony.PatchAll();

            Logger.LogInfo("Shadow Element Mod loaded!");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    // ═══════════════════════════════════════
    // Prefab Factory (builds spell objects at runtime)
    // ═══════════════════════════════════════

    public static class PrefabFactory
    {
        private static Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();

        public static void Initialize()
        {
            CreateTemplate("ShadowBolt", typeof(ShadowBoltObject), new Color(0.3f, 0f, 0.5f));
            // Repeat for other spells...
        }

        private static void CreateTemplate(string name, Type spellObjectType, Color color)
        {
            var go = new GameObject($"{name}_Template");
            go.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(go);

            go.AddComponent(spellObjectType);
            var id = go.AddComponent<Identity>();
            id.type = UnitType.Projectile;
            go.AddComponent<SoundPlayer>();

            var pb = go.AddComponent<PhysicsBody>();
            pb.gravity = 0f;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.4f;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // Simple visual
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.SetParent(go.transform);
            visual.transform.localScale = Vector3.one * 0.4f;
            visual.transform.localPosition = Vector3.zero;
            UnityEngine.Object.Destroy(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().material.color = color;

            templates[name] = go;
        }

        public static GameObject Instantiate(string name, Vector3 pos, Quaternion rot)
        {
            if (!templates.TryGetValue(name, out var template))
            {
                ShadowPlugin.Log.LogError($"Template not found: {name}");
                return new GameObject(name);
            }
            var obj = UnityEngine.Object.Instantiate(template, pos, rot);
            obj.SetActive(true);
            return obj;
        }
    }

    // ═══════════════════════════════════════
    // Spell Classes
    // ═══════════════════════════════════════

    public class ShadowBoltSpell : Spell
    {
        public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
            float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
        {
            var obj = PrefabFactory.Instantiate("ShadowBolt",
                position + Spell.skillshotOffset, rotation);
            obj.GetComponent<ShadowBoltObject>()
                .Init(identity.owner, curve * this.curveMultiplier, this.initialVelocity);
        }
    }

    public class ShadowBoltObject : SpellObject
    {
        public ShadowBoltObject()
        {
            this.DAMAGE = 11f;
            this.RADIUS = 4f;
            this.POWER = 42f;
            this.Y_POWER = 0f;
            this.START_TIME = 1f;
        }

        private PhysicsBody phys;

        private void Start()
        {
            this.phys = GetComponent<PhysicsBody>();
            this.SpellObjectStart();
            this.UpdateColor();
        }

        public void Init(int owner, float curve, float velocity)
        {
            this.id.owner = owner;
            this.curve = curve;
            this.velocity = velocity;
        }

        private void FixedUpdate()
        {
            if (this.phys == null) return;

            this.phys.velocity = transform.forward * this.velocity;
            transform.Rotate(Vector3.up, this.curve);

            if (Time.time > this.deathTimer)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            var root = collision.gameObject.transform.root.gameObject;
            if (!GameUtility.IdentityCompare(root, UnitType.Unit)) return;
            if (GameUtility.IdentityCompare(root, this.id.owner)) return;

            var hits = GameUtility.GetAllInSphere(
                transform.position, this.RADIUS, this.id.owner, new UnitType[] { UnitType.Unit });

            foreach (var hit in hits)
            {
                var target = hit.transform.root.gameObject;
                var us = target.GetComponent<UnitStatus>();
                if (us != null)
                {
                    us.TakeDamage(this.DAMAGE, this.id.owner, 0);
                    var pb = target.GetComponent<PhysicsBody>();
                    if (pb != null)
                    {
                        pb.AddForce(GameUtility.GetForceVector(
                            transform.position, target.transform.position, this.POWER, this.Y_POWER));
                    }
                }
            }

            UnityEngine.Object.Destroy(gameObject);
        }
    }

    // ═══════════════════════════════════════
    // Harmony Patches
    // ═══════════════════════════════════════

    // --- Inject spells into SpellManager ---
    [HarmonyPatch(typeof(SpellManager), "Awake")]
    public static class SpellManagerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SpellManager __instance)
        {
            var spellTable = Traverse.Create(__instance).Field("spell_table")
                .GetValue<Dictionary<SpellName, Spell>>();

            // Add ShadowBolt
            var bolt = __instance.gameObject.AddComponent<ShadowBoltSpell>();
            bolt.spellName = Shadow.Bolt;
            bolt.element = Shadow.Element;
            bolt.spellButton = SpellButton.Primary;
            bolt.cooldown = 1.5f;
            bolt.windUp = 0.35f;
            bolt.windDown = 0.3f;
            bolt.curveMultiplier = 1f;
            bolt.initialVelocity = 0.5f;
            bolt.minRange = 3f;
            bolt.maxRange = 30f;
            bolt.uses = SpellUses.Attack;
            bolt.description = "Shadow bolt";
            bolt.animationName = "Attack";

            spellTable[Shadow.Bolt] = bolt;

            // Add to AI draft
            var aiDraft = Traverse.Create(__instance).Field("ai_draft_priority")
                .GetValue<Dictionary<SpellButton, List<SpellName>>>();
            if (aiDraft.ContainsKey(SpellButton.Primary))
                aiDraft[SpellButton.Primary].Add(Shadow.Bolt);

            // ... repeat for other 6 spells ...

            ShadowPlugin.Log.LogInfo("Shadow spells registered");
        }
    }

    // --- Add Shadow to available elements ---
    [HarmonyPatch(typeof(AvailableElements))]
    public static class AvailableElementsPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(MethodType.StaticConstructor)]
        public static void Postfix()
        {
            var orig = AvailableElements.unlockOrder;
            if (Array.IndexOf(orig, Shadow.Element) >= 0) return; // Already added

            var extended = new Element[orig.Length + 1];
            orig.CopyTo(extended, 0);
            extended[extended.Length - 1] = Shadow.Element;
            AvailableElements.unlockOrder = extended;

            ShadowPlugin.Log.LogInfo($"Element unlock order extended to {extended.Length}");
        }
    }

    // --- Expand GameSettings elements array ---
    [HarmonyPatch(typeof(GameSettings), MethodType.Constructor)]
    public static class GameSettingsCtorPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameSettings __instance)
        {
            int needed = AvailableElements.unlockOrder.Length;
            if (__instance.elements.Length < needed)
            {
                var expanded = new ElementInclusionMode[needed];
                __instance.elements.CopyTo(expanded, 0);
                __instance.elements = expanded;
            }
        }
    }
}
```

---

## Part 8: Debugging Your Mod

### Enable BepInEx Console

Edit `BepInEx/config/BepInEx.cfg`:

```ini
[Logging.Console]
Enabled = true
```

### Logging

```csharp
Plugin.Log.LogInfo("Normal message");
Plugin.Log.LogWarning("Something odd");
Plugin.Log.LogError("Something broke!");
Plugin.Log.LogDebug("Verbose detail");
```

### Check Logs

- Console window (if enabled)
- `BepInEx/LogOutput.log` — full session log

### Common Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| Plugin not loading | Wrong .NET target or missing dependencies | Check `LogOutput.log` for load errors |
| `NullReferenceException` in patches | Patching too early (object not yet created) | Use `[HarmonyPostfix]` instead of `[HarmonyPrefix]`, or delay with coroutine |
| `MissingMethodException` | Game updated, method signature changed | Re-decompile and update patches |
| Spell doesn't appear in draft | Not added to `AvailableElements` or wrong element | Check both `unlockOrder` AND `spell_table` |
| Crash on online game | Missing PhotonView / RPC | Use offline-only mode or implement full networking |
| `TypeLoadException` | Referencing wrong DLL version | Ensure all `<Reference>` paths point to MageQuit's DLLs |
| Enum.ToString() shows number | Expected — cast enums are just ints | Use your own lookup table for display names |

### Verifying Patches Applied

```csharp
private void Awake()
{
    harmony = new Harmony("com.yourname.mod");
    harmony.PatchAll();

    // Log all successfully applied patches
    foreach (var method in harmony.GetPatchedMethods())
    {
        Logger.LogInfo($"Patched: {method.DeclaringType?.Name}.{method.Name}");
    }
}
```

---

## Part 9: Useful Harmony Patterns

### Access Private Fields

```csharp
// Using Traverse (safe, slower)
var value = Traverse.Create(instance).Field("privateField").GetValue<float>();
Traverse.Create(instance).Field("privateField").SetValue(42f);

// Using AccessTools + reflection (faster in loops)
var fieldInfo = AccessTools.Field(typeof(SpellObject), "RADIUS");
float radius = (float)fieldInfo.GetValue(instance);
fieldInfo.SetValue(instance, 6f);
```

### Call Private Methods

```csharp
var method = AccessTools.Method(typeof(SpellManager), "CastSpell");
method.Invoke(spellManager, new object[] { spellName, identity, pos, rot, curve, -1, false, spellName });
```

### Prefix (Replace or Block Original)

```csharp
[HarmonyPrefix]
public static bool Prefix(OriginalClass __instance, int someParam, ref int __result)
{
    // __instance = the object the method is called on
    // someParam = original method parameter (same name!)
    // __result = set this to override the return value

    __result = 42;
    return false;  // false = skip original method
    // return true = run original after this
}
```

### Postfix (Run After Original)

```csharp
[HarmonyPostfix]
public static void Postfix(OriginalClass __instance, ref int __result)
{
    // Modify the result after the original ran
    __result *= 2;
}
```

### Transpiler (IL Patching — Advanced)

```csharp
[HarmonyTranspiler]
public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    var codes = new List<CodeInstruction>(instructions);
    for (int i = 0; i < codes.Count; i++)
    {
        // Find and replace specific IL instructions
        if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 10f)
        {
            codes[i].operand = 20f; // Change hardcoded 10f to 20f
        }
    }
    return codes;
}
```

---

## Summary Checklist

### New Element
- [ ] Choose an `Element` value (repurpose `Tutorial` or cast new int)
- [ ] Patch `AvailableElements.unlockOrder` to include it
- [ ] Patch `GameSettings` constructor to expand `elements[]` array
- [ ] Patch `Globals.iconEmissionColors` if needed
- [ ] Patch `ElementColorMapping` for stage visuals
- [ ] Patch `DefaultPlayerSelection` / `PlayerDropIn` random ranges

### New Spell
- [ ] Choose `SpellName` values (cast new ints after 69)
- [ ] Create `Spell` subclass with `Initialize()` override
- [ ] Create `SpellObject` subclass with stats, movement, collision
- [ ] Build prefab templates in `PrefabFactory` (or use AssetBundle)
- [ ] Patch `SpellManager.Awake()` to register in `spell_table`
- [ ] Configure all Inspector fields in code (element, slot, cooldown, etc.)
- [ ] Add to `ai_draft_priority`

### Testing
- [ ] Check `BepInEx/LogOutput.log` for load errors
- [ ] Verify patches with `harmony.GetPatchedMethods()`
- [ ] Test in practice range (offline first)
- [ ] Test with bots (AI draft + usage)
- [ ] Test online only if networking is implemented
