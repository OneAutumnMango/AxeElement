using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MageQuitModFramework.Data;
using MageQuitModFramework.Spells;
using MageQuitModFramework.Utilities;
using UnityEngine;

namespace BloodElement
{
    /// <summary>
    /// Spell definition and registration. Called from BloodElementPatches during SpellManager.Awake.
    /// Contains spell metadata (cooldowns, windups, descriptions, etc.) and AI draft priority setup.
    /// </summary>
    public static class BloodRegistration
    {
        private static bool registered;
        private static readonly HashSet<SpellName> bloodSpellNames = new HashSet<SpellName>();

        public static void RegisterSpells(SpellManager manager, Dictionary<SpellName, Spell> spellTable)
        {
            if (spellTable == null)
            {
                Plugin.Log.LogWarning("[BloodReg] spellTable is null!");
                return;
            }

            Plugin.Log.LogInfo($"[BloodReg] RegisterSpells called. registered={registered}, table count={spellTable.Count}");

            if (registered)
            {
                // Re-entering (round transition): ensure Blood spells stay in the table.
                var existing = manager.gameObject.GetComponents<Spell>();
                foreach (var spell in existing)
                {
                    if (spell != null && bloodSpellNames.Contains(spell.spellName))
                    {
                        spell.element = Blood.Element;
                        if (!spellTable.ContainsKey(spell.spellName))
                            spellTable[spell.spellName] = spell;
                    }
                }
                Plugin.Log.LogInfo("[BloodReg] Re-entry complete.");
                return;
            }
            registered = true;

            // ── Collect Metal spell icons/videos to use for Blood spells ──────────
            // Map by SpellButton so we can assign to matching Blood spell slots
            var metalIcons  = new Dictionary<SpellButton, Sprite>();
            var metalVideos = new Dictionary<SpellButton, UnityEngine.Video.VideoClip>();

            foreach (var kv in spellTable)
            {
                if (kv.Value != null && kv.Value.element == Element.Metal)
                {
                    if (kv.Value.icon != null && !metalIcons.ContainsKey(kv.Value.spellButton))
                        metalIcons[kv.Value.spellButton] = kv.Value.icon;
                    if (kv.Value.video != null && !metalVideos.ContainsKey(kv.Value.spellButton))
                        metalVideos[kv.Value.spellButton] = kv.Value.video;
                }
            }

            Plugin.Log.LogInfo($"[BloodReg] Collected Metal icons: {metalIcons.Count}, videos: {metalVideos.Count}");
            foreach (var kv in metalIcons)
                Plugin.Log.LogInfo($"[BloodReg]   Metal icon: btn={kv.Key} sprite={kv.Value.name}");
            foreach (var kv in metalVideos)
                Plugin.Log.LogInfo($"[BloodReg]   Metal video: btn={kv.Key} clip={kv.Value.name}");

            // ── Collect Hinder icon for BloodMelee ─────────────────────────────
            Sprite hinderIcon = null;
            if (spellTable.TryGetValue((SpellName)8, out var hinderSpell) && hinderSpell?.icon != null)
                hinderIcon = hinderSpell.icon;
            Plugin.Log.LogInfo($"[BloodReg] Hinder icon found: {hinderIcon != null}");

            // ── Collect Sand Ult icon for BloodUtility ─────────────────────
            Sprite sandUltIcon = null;
            foreach (var kv in spellTable)
            {
                if (kv.Value != null && kv.Value.element == (Element)5 &&
                    kv.Value.spellButton == SpellButton.Ultimate && kv.Value.icon != null)
                {
                    sandUltIcon = kv.Value.icon;
                    break;
                }
            }
            Plugin.Log.LogInfo($"[BloodReg] Sand Ult icon found: {sandUltIcon != null}");

            // ── Collect Sand Primary icon for BloodMovement ────────────────────
            Sprite sandPrimaryIcon = null;
            foreach (var kv in spellTable)
            {
                if (kv.Value != null && kv.Value.element == (Element)5 &&
                    kv.Value.spellButton == SpellButton.Primary && kv.Value.icon != null)
                {
                    sandPrimaryIcon = kv.Value.icon;
                    break;
                }
            }
            Plugin.Log.LogInfo($"[BloodReg] Sand Primary icon found: {sandPrimaryIcon != null}");

            // ── BloodPrimary (Primary) ─────────────────────────────────────────
            var bloodPrimary = manager.gameObject.AddComponent<BloodPrimary>();
            bloodPrimary.spellName        = Blood.BloodPrimary;
            bloodPrimary.element          = Blood.Element;
            bloodPrimary.spellButton      = SpellButton.Primary;
            bloodPrimary.description      = "Hurl a spinning blade forward.";
            bloodPrimary.cooldown         = 3.5f;
            bloodPrimary.windUp           = 0.35f;
            bloodPrimary.windDown         = 0.3f;
            bloodPrimary.animationName    = "Attack";
            bloodPrimary.curveMultiplier  = 1.5f;
            bloodPrimary.initialVelocity  = 28f;
            bloodPrimary.minRange         = 0f;
            bloodPrimary.maxRange         = 30f;
            bloodPrimary.uses             = SpellUses.Attack;
            bloodPrimary.additionalCasts  = new SubSpell[0];
            AssignAssets(bloodPrimary, SpellButton.Utility, metalIcons, metalVideos);
            var primaryPng = LoadPngIcon("primary.png");
            if (primaryPng != null)
                bloodPrimary.icon = primaryPng;
            spellTable[Blood.BloodPrimary]     = bloodPrimary;
            bloodSpellNames.Add(Blood.BloodPrimary);

            // ── BloodMovement (Movement) ──────────────────────────────────────────────
            var bloodMovement = manager.gameObject.AddComponent<BloodMovement>();
            bloodMovement.spellName         = Blood.BloodMovement;
            bloodMovement.element           = Blood.Element;
            bloodMovement.spellButton       = SpellButton.Movement;
            bloodMovement.description       = "Step back and surge forward, striking all enemies in your path. Press again to chain up to 3 lunges.";
            bloodMovement.cooldown          = 12f;
            bloodMovement.windUp            = 0.15f;
            bloodMovement.windDown          = 0.2f;
            bloodMovement.animationName     = "FlameLeap";
            bloodMovement.curveMultiplier   = 0f;
            bloodMovement.initialVelocity   = 0f;
            bloodMovement.minRange          = 0f;
            bloodMovement.maxRange          = 30f;
            bloodMovement.uses              = SpellUses.Move | SpellUses.Attack;
            bloodMovement.reactivate        = 2;
            bloodMovement.additionalCasts   = new SubSpell[]
            {
                new SubSpell
                {
                    animationName    = "FlameLeap",
                    cooldown         = 12f,
                    windUp           = 0.15f,
                    windDown         = 0.2f,
                    activationWindow = 3f,
                    startsDisabled   = false,
                    curveMultiplier  = 0f,
                    initialVelocity  = 0f,
                    minRange         = 0f,
                    maxRange         = 30f,
                    uses             = SpellUses.Move | SpellUses.Attack
                },
                new SubSpell
                {
                    animationName    = "FlameLeap",
                    cooldown         = 12f,
                    windUp           = 0.15f,
                    windDown         = 0.3f,
                    activationWindow = 3f,
                    startsDisabled   = false,
                    curveMultiplier  = 0f,
                    initialVelocity  = 0f,
                    minRange         = 0f,
                    maxRange         = 30f,
                    uses             = SpellUses.Move | SpellUses.Attack
                }
            };
            AssignAssets(bloodMovement, SpellButton.Movement, metalIcons, metalVideos);
            if (sandPrimaryIcon != null)
                bloodMovement.icon = sandPrimaryIcon;
            var movementPng = LoadPngIcon("movement.png");
            if (movementPng != null)
                bloodMovement.icon = movementPng;
            spellTable[Blood.BloodMovement] = bloodMovement;
            bloodSpellNames.Add(Blood.BloodMovement);

            // ── BloodMelee (Melee) ───────────────────────────────────────────
            var bloodMelee = manager.gameObject.AddComponent<BloodMelee>();
            bloodMelee.spellName        = Blood.BloodMelee;
            bloodMelee.element          = Blood.Element;
            bloodMelee.spellButton      = SpellButton.Melee;
            bloodMelee.description      = "Slash enemies to open deep wounds. Bleeding targets take increased spell damage. Hitting spells refreshes the duration.";
            bloodMelee.cooldown         = 5f;
            bloodMelee.windUp           = 0.65f;
            bloodMelee.windDown         = 1.5f;
            bloodMelee.animationName    = "Melee";
            bloodMelee.curveMultiplier  = 0f;
            bloodMelee.initialVelocity  = 0f;
            bloodMelee.minRange         = 0f;
            bloodMelee.maxRange         = 4f;
            bloodMelee.uses             = SpellUses.Attack;
            bloodMelee.additionalCasts  = new SubSpell[0];
            AssignAssets(bloodMelee, SpellButton.Melee, metalIcons, metalVideos);
            if (hinderIcon != null)
                bloodMelee.icon = hinderIcon;
            var meleePng = LoadPngIcon("melee.png");
            if (meleePng != null)
                bloodMelee.icon = meleePng;
            spellTable[Blood.BloodMelee]    = bloodMelee;
            bloodSpellNames.Add(Blood.BloodMelee);

            // ── BloodSecondary (Secondary) ────────────────────────────────────
            var bloodSecondary = manager.gameObject.AddComponent<BloodSecondary>();
            bloodSecondary.spellName       = Blood.BloodSecondary;
            bloodSecondary.element         = Blood.Element;
            bloodSecondary.spellButton     = SpellButton.Secondary;
            bloodSecondary.description     = "Unleash two blades that arc outward and converge back, piercing through all enemies in their path.";
            bloodSecondary.cooldown        = 10f;
            bloodSecondary.windUp          = 0.35f;
            bloodSecondary.windDown        = 0.35f;
            bloodSecondary.animationName   = "Secondary Spell";
            bloodSecondary.curveMultiplier = 0f;
            bloodSecondary.initialVelocity = 30f;
            bloodSecondary.minRange        = 0f;
            bloodSecondary.maxRange        = 28f;
            bloodSecondary.uses            = SpellUses.Attack;
            bloodSecondary.additionalCasts = new SubSpell[0];
            AssignAssets(bloodSecondary, SpellButton.Primary, metalIcons, metalVideos); // fallback
            var secondaryPng = LoadPngIcon("secondary.png");
            if (secondaryPng != null)
                bloodSecondary.icon = secondaryPng;
            spellTable[Blood.BloodSecondary] = bloodSecondary;
            bloodSpellNames.Add(Blood.BloodSecondary);

            // ── BloodDefensive (Defensive) ───────────────────────────────────
            var bloodDefensive = manager.gameObject.AddComponent<BloodDefensive>();
            bloodDefensive.spellName       = Blood.BloodDefensive;
            bloodDefensive.element         = Blood.Element;
            bloodDefensive.spellButton     = SpellButton.Defensive;
            bloodDefensive.description     = "Brace for 1 second; if struck, vanish and reappear at your attacker, dealing 5 damage.";
            bloodDefensive.cooldown        = 6f;
            bloodDefensive.windUp          = 0.2f;
            bloodDefensive.windDown        = 0.2f;
            bloodDefensive.animationName   = "Defensive";
            bloodDefensive.curveMultiplier = 0f;
            bloodDefensive.initialVelocity = 0f;
            bloodDefensive.minRange        = 0f;
            bloodDefensive.maxRange        = 0f;
            bloodDefensive.uses            = SpellUses.Defend | SpellUses.Custom;
            bloodDefensive.additionalCasts = new SubSpell[0];
            AssignAssets(bloodDefensive, SpellButton.Ultimate, metalIcons, metalVideos); // use metal ult icon
            var defensivePng = LoadPngIcon("defensive.png");
            if (defensivePng != null)
                bloodDefensive.icon = defensivePng;
            spellTable[Blood.BloodDefensive] = bloodDefensive;
            bloodSpellNames.Add(Blood.BloodDefensive);

            // ── BloodUtility (Utility) ───────────────────────────────────
            var bloodUtility = manager.gameObject.AddComponent<BloodUtility>();
            bloodUtility.spellName        = Blood.BloodUtility;
            bloodUtility.element          = Blood.Element;
            bloodUtility.spellButton      = SpellButton.Utility;
            bloodUtility.description      = "Summon two spinning glaives to orbit you for 5 seconds, shredding nearby enemies.";
            bloodUtility.cooldown         = 12f;
            bloodUtility.windUp           = 0.25f;
            bloodUtility.windDown         = 0.8f;
            bloodUtility.animationName    = "Spell 360";
            bloodUtility.curveMultiplier  = 0f;
            bloodUtility.initialVelocity  = 0f;
            bloodUtility.minRange         = 0f;
            bloodUtility.maxRange         = 0f;
            bloodUtility.uses             = SpellUses.Attack | SpellUses.Custom;
            bloodUtility.additionalCasts  = new SubSpell[0];
            if (sandUltIcon != null)
                bloodUtility.icon = sandUltIcon;
            var utilityPng = LoadPngIcon("utility.png");
            if (utilityPng != null)
                bloodUtility.icon = utilityPng;
            spellTable[Blood.BloodUtility] = bloodUtility;
            bloodSpellNames.Add(Blood.BloodUtility);

            // ── Blood Field (Ultimate) ─────────────────────────────────────────
            var bloodUltimate = manager.gameObject.AddComponent<BloodUltimate>();
            bloodUltimate.spellName       = Blood.BloodUltimate;
            bloodUltimate.element         = Blood.Element;
            bloodUltimate.spellButton     = SpellButton.Ultimate;
            bloodUltimate.description     = "Drive your weapon into the ground, saturating the area with a blood field. Enemies inside bleed and slow; every wound you deal to bleeding foes restores your health. Spell persists if enemies remain inside.";
            bloodUltimate.cooldown        = 25f;
            bloodUltimate.windUp          = 0.75f;
            bloodUltimate.windDown        = 0.5f;
            bloodUltimate.animationName   = "Melee";
            bloodUltimate.curveMultiplier = 0f;
            bloodUltimate.initialVelocity = 0f;
            bloodUltimate.minRange        = 0f;
            bloodUltimate.maxRange        = 0f;
            bloodUltimate.uses            = SpellUses.Attack | SpellUses.Custom;
            bloodUltimate.additionalCasts = new SubSpell[0];
            AssignAssets(bloodUltimate, SpellButton.Movement, metalIcons, metalVideos);
            var ultimatePng = LoadPngIcon("ultimate.png");
            if (ultimatePng != null)
                bloodUltimate.icon = ultimatePng;
            spellTable[Blood.BloodUltimate] = bloodUltimate;
            bloodSpellNames.Add(Blood.BloodUltimate);

            // ── AI draft priority ──────────────────────────────────────────
            var aiDraft = Traverse.Create(manager)
                .Field("ai_draft_priority")
                .GetValue<Dictionary<SpellButton, List<SpellName>>>();

            if (aiDraft != null)
            {
                void TryAddDraft(SpellButton btn, SpellName name)
                {
                    if (aiDraft.ContainsKey(btn) && !aiDraft[btn].Contains(name))
                        aiDraft[btn].Add(name);
                }
                TryAddDraft(SpellButton.Primary,   Blood.BloodPrimary);
                TryAddDraft(SpellButton.Movement,  Blood.BloodMovement);
                TryAddDraft(SpellButton.Melee,     Blood.BloodMelee);
                TryAddDraft(SpellButton.Secondary, Blood.BloodSecondary);
                TryAddDraft(SpellButton.Defensive, Blood.BloodDefensive);
                TryAddDraft(SpellButton.Utility,   Blood.BloodUtility);
                TryAddDraft(SpellButton.Ultimate,  Blood.BloodUltimate);
            }

            // ── UI colors ──────────────────────────────────────────────────
            // Expand spellColors to include index 11 (Blood/Tutorial slot)
            if (manager.spellColors != null && manager.spellColors.Length <= 11)
            {
                var expanded = new Color[12];
                manager.spellColors.CopyTo(expanded, 0);
                manager.spellColors = expanded;
            }
            if (manager.spellColors != null && manager.spellColors.Length > 11)
                manager.spellColors[11] = new Color(0.55f, 0.06f, 0.06f);

            // Expand iconEmissionColors to include index 11
            if (Globals.iconEmissionColors != null && Globals.iconEmissionColors.Length <= 11)
            {
                var expanded = new Color[12];
                Globals.iconEmissionColors.CopyTo(expanded, 0);
                Globals.iconEmissionColors = expanded;
            }
            if (Globals.iconEmissionColors != null && Globals.iconEmissionColors.Length > 11)
                Globals.iconEmissionColors[11] = new Color(0.30f, 0.03f, 0.03f);

            // ── Load bleed effect prefab for BloodMelee ─────────────────────
            try
            {
                var ignitePrefab = Resources.Load<GameObject>("Objects/Ignite");
                var igniteComp = ignitePrefab?.GetComponent<IgniteObject>();
                BloodMeleeObject.BleedEffectPrefab = igniteComp?.effect;
                Plugin.Log.LogInfo($"[BloodReg] BleedEffectPrefab loaded: {BloodMeleeObject.BleedEffectPrefab != null}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[BloodReg] BleedEffectPrefab load failed: {ex.Message}");
            }

            // ── Diagnostic: verify spell table state ──────────────────────────
            int bloodCount = 0;
            foreach (var kv in spellTable)
            {
                if (kv.Value != null && kv.Value.element == Blood.Element)
                {
                    bloodCount++;
                    Plugin.Log.LogInfo($"[BloodReg]   Blood spell in table: {kv.Key} btn={kv.Value.spellButton} el={kv.Value.element}");
                }
            }
            Plugin.Log.LogInfo($"[BloodReg] Registration complete. Blood spells in table: {bloodCount}, bloodSpellNames count: {bloodSpellNames.Count}");
            Plugin.Log.LogInfo($"[BloodReg] Globals.spell_manager == manager: {Globals.spell_manager == manager}");
            // Verify the table reference is the same one Globals uses
            var globalsTable = Traverse.Create(Globals.spell_manager)
                .Field("spell_table")
                .GetValue<Dictionary<SpellName, Spell>>();
            Plugin.Log.LogInfo($"[BloodReg] spellTable ref == globals ref: {object.ReferenceEquals(spellTable, globalsTable)}");
        }


        public static Sprite LoadPngIcon(string filename)
        {
            try
            {
                string dllDir = System.IO.Path.GetDirectoryName(
                    typeof(Plugin).Assembly.Location);
                string path = System.IO.Path.Combine(dllDir, "icons", filename);
                if (!System.IO.File.Exists(path))
                {
                    Plugin.Log.LogWarning($"[BloodReg] Icon not found: {path}");
                    return null;
                }
                byte[] data = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, data))
                {
                    Plugin.Log.LogWarning($"[BloodReg] Failed to decode: {filename}");
                    return null;
                }
                var sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                Plugin.Log.LogInfo($"[BloodReg] Loaded icon from disk: {filename} ({tex.width}x{tex.height})");
                return sprite;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[BloodReg] LoadPngIcon failed for {filename}: {ex.Message}");
                return null;
            }
        }

        private static void AssignAssets(
            Spell spell,
            SpellButton button,
            Dictionary<SpellButton, Sprite> metalIcons,
            Dictionary<SpellButton, UnityEngine.Video.VideoClip> metalVideos)
        {
            if (metalIcons.TryGetValue(button, out var icon))
                spell.icon = icon;
            if (metalVideos.TryGetValue(button, out var video))
                spell.video = video;
        }

        // ── SpellModificationSystem integration ─────────────────────────────────
        // Maps each Blood SpellName to the concrete SpellObject type whose constructor
        // sets the class-level attribute defaults (DAMAGE, RADIUS, POWER, Y_POWER).
        private static readonly (SpellName name, Type objectType)[] BloodSpellObjectMap =
        [
            (Blood.BloodPrimary,   typeof(BloodPrimaryObject)),
            (Blood.BloodMovement,  typeof(BloodMovementObject)),
            (Blood.BloodMelee,     typeof(BloodMeleeObject)),
            (Blood.BloodSecondary, typeof(BloodSecondaryObject)),
            (Blood.BloodDefensive, typeof(BloodDefensiveObject)),
            (Blood.BloodUtility,   typeof(BloodUtilityObject)),
            (Blood.BloodUltimate,  typeof(BloodUltimateObject)),
        ];

        private static readonly string[] ClassAttrFields = ["DAMAGE", "RADIUS", "POWER", "Y_POWER"];

        private static readonly (SpellName spell, string displayName)[] BloodDisplayNames =
        [
            (Blood.BloodPrimary,   "Rend"),
            (Blood.BloodMovement,  "Lunge"),
            (Blood.BloodMelee,     "Bleed"),
            (Blood.BloodSecondary, "Wild Blades"),
            (Blood.BloodDefensive, "Riposte"),
            (Blood.BloodUtility,   "Blade Storm"),
            (Blood.BloodUltimate,  "Sanguine Aura"),
        ];

        /// <summary>
        /// Registers human-readable display names for all Blood spell names.
        /// Called at mod load so names are available before game data is ready.
        /// </summary>
        public static void RegisterSpellDisplayNames()
        {
            foreach (var (spell, displayName) in BloodDisplayNames)
                SpellNameRegistry.Register(spell, displayName);
        }

        /// <summary>
        /// Registers all Blood SpellObject types with SpellModificationSystem so that
        /// PatchAllSpellObjects and GetSpellNameFromTypeName resolve them correctly.
        /// Call this from OnLoad (before any PatchAllSpellObjects call) and also
        /// on each game data load for safety.
        /// </summary>
        public static void RegisterSpellObjectTypes()
        {
            foreach (var (spellName, objectType) in BloodSpellObjectMap)
                SpellModificationSystem.RegisterSpellObjectType(spellName, objectType);
        }

        /// <summary>
        /// Registers all Blood spells with the MageQuitModFramework SpellModificationSystem.
        /// Must be called after <see cref="RegisterSpells"/> so the entries exist in spellTable.
        /// Safe to call every round — the framework re-creates its default table each round,
        /// so we re-inject the Blood entries every time.
        /// </summary>
        public static void RegisterBloodSpellsWithModSystem(Dictionary<SpellName, Spell> spellTable)
        {
            var defaultTable = SpellModificationSystem.Default();
            if (defaultTable == null)
            {
                Plugin.Log.LogWarning("[BloodReg] SpellModificationSystem default table is null; " +
                                      "Blood spells won't have modifier support this round");
                return;
            }

            foreach (var (spellName, objectType) in BloodSpellObjectMap)
            {
                if (!spellTable.TryGetValue(spellName, out var spell))
                {
                    Plugin.Log.LogWarning($"[BloodReg] {spellName} missing from spell table; skipping modifier registration");
                    continue;
                }

                // Read class-level attributes from a fresh constructor call on the SpellObject type.
                // The constructor sets DAMAGE/RADIUS/POWER/Y_POWER to their design defaults.
                // This mirrors what GameDataInitializer.PopulateDefaultClassAttributes does for
                // vanilla spells — but those iterate Enum.GetValues(SpellName) which excludes
                // our out-of-range SpellName values, so we do it manually here.
                var classAttrs = new Dictionary<string, float>();
                try
                {
                    var obj = Activator.CreateInstance(objectType) as SpellObject;
                    foreach (var field in ClassAttrFields)
                    {
                        try
                        {
                            classAttrs[field] = GameModificationHelpers.GetPrivateField<float>(obj, field);
                        }
                        catch
                        {
                            classAttrs[field] = 0f;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[BloodReg] Could not instantiate {objectType.Name} for attribute defaults: {ex.Message}");
                    foreach (var field in ClassAttrFields)
                        classAttrs[field] = 0f;
                }

                float GetAttr(string key) => classAttrs.TryGetValue(key, out var v) ? v : 0f;

                var mods = new SpellModifiers
                {
                    DAMAGE          = new AttributeModifier(GetAttr("DAMAGE")),
                    RADIUS          = new AttributeModifier(GetAttr("RADIUS")),
                    POWER           = new AttributeModifier(GetAttr("POWER")),
                    Y_POWER         = new AttributeModifier(GetAttr("Y_POWER")),
                    cooldown        = new AttributeModifier(spell.cooldown),
                    windUp          = new AttributeModifier(spell.windUp),
                    windDown        = new AttributeModifier(spell.windDown),
                    initialVelocity = new AttributeModifier(spell.initialVelocity),
                    HEAL            = new AttributeModifier(0f),
                };

                // Inject into the default table AND any already-registered named tables
                // (e.g. "boosted") in case they were created before we ran.
                SpellModificationSystem.InjectIntoAllTables(spellName, mods);

                // Mirror into the framework's canonical snapshots so any code that reads
                // GameDataInitializer.DefaultSpellTable / DefaultClassAttributes also sees Blood.
                GameDataInitializer.DefaultSpellTable[spellName]        = spell;
                GameDataInitializer.DefaultClassAttributes[spellName]   = classAttrs;

                Plugin.Log.LogInfo($"[BloodReg] Registered {spellName} with SpellModificationSystem " +
                                   $"(DAMAGE={GetAttr("DAMAGE")}, cooldown={spell.cooldown})");
            }

            Plugin.Log.LogInfo("[BloodReg] Blood spells registered with SpellModificationSystem");

            // Re-register spell object types in case BoostedPatch.PatchAll runs after us.
            RegisterSpellObjectTypes();
        }
    }
}
