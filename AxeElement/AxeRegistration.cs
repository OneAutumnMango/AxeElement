using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace AxeElement
{
    /// <summary>
    /// Spell definition and registration. Called from AxeElementPatches during SpellManager.Awake.
    /// Contains spell metadata (cooldowns, windups, descriptions, etc.) and AI draft priority setup.
    /// </summary>
    public static class AxeRegistration
    {
        private static bool registered;
        private static readonly HashSet<SpellName> axeSpellNames = new HashSet<SpellName>();

        public static void RegisterSpells(SpellManager manager, Dictionary<SpellName, Spell> spellTable)
        {
            if (spellTable == null)
            {
                Plugin.Log.LogWarning("[AxeReg] spellTable is null!");
                return;
            }

            Plugin.Log.LogInfo($"[AxeReg] RegisterSpells called. registered={registered}, table count={spellTable.Count}");

            if (registered)
            {
                // Re-entering (round transition): ensure Axe spells stay in the table.
                var existing = manager.gameObject.GetComponents<Spell>();
                foreach (var spell in existing)
                {
                    if (spell != null && axeSpellNames.Contains(spell.spellName))
                    {
                        spell.element = Axe.Element;
                        if (!spellTable.ContainsKey(spell.spellName))
                            spellTable[spell.spellName] = spell;
                    }
                }
                Plugin.Log.LogInfo("[AxeReg] Re-entry complete.");
                return;
            }
            registered = true;

            // ── Collect Metal spell icons/videos to use for Axe spells ──────────
            // Map by SpellButton so we can assign to matching Axe spell slots
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

            Plugin.Log.LogInfo($"[AxeReg] Collected Metal icons: {metalIcons.Count}, videos: {metalVideos.Count}");
            foreach (var kv in metalIcons)
                Plugin.Log.LogInfo($"[AxeReg]   Metal icon: btn={kv.Key} sprite={kv.Value.name}");
            foreach (var kv in metalVideos)
                Plugin.Log.LogInfo($"[AxeReg]   Metal video: btn={kv.Key} clip={kv.Value.name}");

            // ── Collect Hinder icon for AxeMelee ─────────────────────────────
            Sprite hinderIcon = null;
            if (spellTable.TryGetValue((SpellName)8, out var hinderSpell) && hinderSpell?.icon != null)
                hinderIcon = hinderSpell.icon;
            Plugin.Log.LogInfo($"[AxeReg] Hinder icon found: {hinderIcon != null}");

            // ── Collect Sand Ult icon for AxeUtility ─────────────────────
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
            Plugin.Log.LogInfo($"[AxeReg] Sand Ult icon found: {sandUltIcon != null}");

            // ── Collect Sand Primary icon for AxeMovement ────────────────────
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
            Plugin.Log.LogInfo($"[AxeReg] Sand Primary icon found: {sandPrimaryIcon != null}");

            // ── AxePrimary (Primary) ─────────────────────────────────────────
            var axePrimary = manager.gameObject.AddComponent<AxePrimary>();
            axePrimary.spellName        = Axe.AxePrimary;
            axePrimary.element          = Axe.Element;
            axePrimary.spellButton      = SpellButton.Primary;
            axePrimary.description      = "Hurl a spinning blade forward.";
            axePrimary.cooldown         = 3.5f;
            axePrimary.windUp           = 0.35f;
            axePrimary.windDown         = 0.3f;
            axePrimary.animationName    = "Attack";
            axePrimary.curveMultiplier  = 1.5f;
            axePrimary.initialVelocity  = 28f;
            axePrimary.minRange         = 0f;
            axePrimary.maxRange         = 30f;
            axePrimary.uses             = SpellUses.Attack;
            axePrimary.additionalCasts  = new SubSpell[0];
            AssignAssets(axePrimary, SpellButton.Utility, metalIcons, metalVideos);
            var primaryPng = LoadPngIcon("primary.png");
            if (primaryPng != null)
                axePrimary.icon = primaryPng;
            spellTable[Axe.AxePrimary]     = axePrimary;
            axeSpellNames.Add(Axe.AxePrimary);

            // ── AxeMovement (Movement) ──────────────────────────────────────────────
            var axeMovement = manager.gameObject.AddComponent<AxeMovement>();
            axeMovement.spellName         = Axe.AxeMovement;
            axeMovement.element           = Axe.Element;
            axeMovement.spellButton       = SpellButton.Movement;
            axeMovement.description       = "Step back and surge forward, striking all enemies in your path. Press again to chain up to 3 lunges.";
            axeMovement.cooldown          = 12f;
            axeMovement.windUp            = 0.15f;
            axeMovement.windDown          = 0.2f;
            axeMovement.animationName     = "FlameLeap";
            axeMovement.curveMultiplier   = 0f;
            axeMovement.initialVelocity   = 0f;
            axeMovement.minRange          = 0f;
            axeMovement.maxRange          = 30f;
            axeMovement.uses              = SpellUses.Move | SpellUses.Attack;
            axeMovement.reactivate        = 2;
            axeMovement.additionalCasts   = new SubSpell[]
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
            AssignAssets(axeMovement, SpellButton.Movement, metalIcons, metalVideos);
            if (sandPrimaryIcon != null)
                axeMovement.icon = sandPrimaryIcon;
            var movementPng = LoadPngIcon("movement.png");
            if (movementPng != null)
                axeMovement.icon = movementPng;
            spellTable[Axe.AxeMovement] = axeMovement;
            axeSpellNames.Add(Axe.AxeMovement);

            // ── AxeMelee (Melee) ───────────────────────────────────────────
            var axeMelee = manager.gameObject.AddComponent<AxeMelee>();
            axeMelee.spellName        = Axe.AxeMelee;
            axeMelee.element          = Axe.Element;
            axeMelee.spellButton      = SpellButton.Melee;
            axeMelee.description      = "Slash enemies to open deep wounds. Bleeding targets take increased spell damage. Hitting spells refreshes the duration.";
            axeMelee.cooldown         = 5f;
            axeMelee.windUp           = 0.65f;
            axeMelee.windDown         = 1.5f;
            axeMelee.animationName    = "Melee";
            axeMelee.curveMultiplier  = 0f;
            axeMelee.initialVelocity  = 0f;
            axeMelee.minRange         = 0f;
            axeMelee.maxRange         = 4f;
            axeMelee.uses             = SpellUses.Attack;
            axeMelee.additionalCasts  = new SubSpell[0];
            AssignAssets(axeMelee, SpellButton.Melee, metalIcons, metalVideos);
            if (hinderIcon != null)
                axeMelee.icon = hinderIcon;
            var meleePng = LoadPngIcon("melee.png");
            if (meleePng != null)
                axeMelee.icon = meleePng;
            spellTable[Axe.AxeMelee]    = axeMelee;
            axeSpellNames.Add(Axe.AxeMelee);

            // ── AxeSecondary (Secondary) ────────────────────────────────────
            var axeSecondary = manager.gameObject.AddComponent<AxeSecondary>();
            axeSecondary.spellName       = Axe.AxeSecondary;
            axeSecondary.element         = Axe.Element;
            axeSecondary.spellButton     = SpellButton.Secondary;
            axeSecondary.description     = "Unleash two axes that arc outward and converge back, piercing through all enemies in their path.";
            axeSecondary.cooldown        = 10f;
            axeSecondary.windUp          = 0.35f;
            axeSecondary.windDown        = 0.35f;
            axeSecondary.animationName   = "Secondary Spell";
            axeSecondary.curveMultiplier = 0f;
            axeSecondary.initialVelocity = 30f;
            axeSecondary.minRange        = 0f;
            axeSecondary.maxRange        = 28f;
            axeSecondary.uses            = SpellUses.Attack;
            axeSecondary.additionalCasts = new SubSpell[0];
            AssignAssets(axeSecondary, SpellButton.Primary, metalIcons, metalVideos); // fallback
            var secondaryPng = LoadPngIcon("secondary.png");
            if (secondaryPng != null)
                axeSecondary.icon = secondaryPng;
            spellTable[Axe.AxeSecondary] = axeSecondary;
            axeSpellNames.Add(Axe.AxeSecondary);

            // ── AxeDefensive (Defensive) ───────────────────────────────────
            var axeDefensive = manager.gameObject.AddComponent<AxeDefensive>();
            axeDefensive.spellName       = Axe.AxeDefensive;
            axeDefensive.element         = Axe.Element;
            axeDefensive.spellButton     = SpellButton.Defensive;
            axeDefensive.description     = "Brace for 1 second; if struck, vanish and reappear at your attacker, dealing 5 damage.";
            axeDefensive.cooldown        = 6f;
            axeDefensive.windUp          = 0.2f;
            axeDefensive.windDown        = 0.2f;
            axeDefensive.animationName   = "Defensive";
            axeDefensive.curveMultiplier = 0f;
            axeDefensive.initialVelocity = 0f;
            axeDefensive.minRange        = 0f;
            axeDefensive.maxRange        = 0f;
            axeDefensive.uses            = SpellUses.Defend | SpellUses.Custom;
            axeDefensive.additionalCasts = new SubSpell[0];
            AssignAssets(axeDefensive, SpellButton.Ultimate, metalIcons, metalVideos); // use metal ult icon
            var defensivePng = LoadPngIcon("defensive.png");
            if (defensivePng != null)
                axeDefensive.icon = defensivePng;
            spellTable[Axe.AxeDefensive] = axeDefensive;
            axeSpellNames.Add(Axe.AxeDefensive);

            // ── AxeUtility (Utility) ───────────────────────────────────
            var axeUtility = manager.gameObject.AddComponent<AxeUtility>();
            axeUtility.spellName        = Axe.AxeUtility;
            axeUtility.element          = Axe.Element;
            axeUtility.spellButton      = SpellButton.Utility;
            axeUtility.description      = "Summon two spinning glaives to orbit you for 5 seconds, shredding nearby enemies.";
            axeUtility.cooldown         = 12f;
            axeUtility.windUp           = 0.25f;
            axeUtility.windDown         = 0.8f;
            axeUtility.animationName    = "Spell 360";
            axeUtility.curveMultiplier  = 0f;
            axeUtility.initialVelocity  = 0f;
            axeUtility.minRange         = 0f;
            axeUtility.maxRange         = 0f;
            axeUtility.uses             = SpellUses.Attack | SpellUses.Custom;
            axeUtility.additionalCasts  = new SubSpell[0];
            if (sandUltIcon != null)
                axeUtility.icon = sandUltIcon;
            var utilityPng = LoadPngIcon("utility.png");
            if (utilityPng != null)
                axeUtility.icon = utilityPng;
            spellTable[Axe.AxeUtility] = axeUtility;
            axeSpellNames.Add(Axe.AxeUtility);

            // ── Blood Field (Ultimate) ─────────────────────────────────────────
            var axeUltimate = manager.gameObject.AddComponent<AxeUltimate>();
            axeUltimate.spellName       = Axe.AxeUltimate;
            axeUltimate.element         = Axe.Element;
            axeUltimate.spellButton     = SpellButton.Ultimate;
            axeUltimate.description     = "Drive your weapon into the ground, saturating the area with a blood field. Enemies inside bleed and slow; every wound you deal to bleeding foes restores your health. Spell persists if enemies remain inside.";
            axeUltimate.cooldown        = 25f;
            axeUltimate.windUp          = 0.75f;
            axeUltimate.windDown        = 0.5f;
            axeUltimate.animationName   = "Melee";
            axeUltimate.curveMultiplier = 0f;
            axeUltimate.initialVelocity = 0f;
            axeUltimate.minRange        = 0f;
            axeUltimate.maxRange        = 0f;
            axeUltimate.uses            = SpellUses.Attack | SpellUses.Custom;
            axeUltimate.additionalCasts = new SubSpell[0];
            AssignAssets(axeUltimate, SpellButton.Movement, metalIcons, metalVideos);
            var ultimatePng = LoadPngIcon("ultimate.png");
            if (ultimatePng != null)
                axeUltimate.icon = ultimatePng;
            spellTable[Axe.AxeUltimate] = axeUltimate;
            axeSpellNames.Add(Axe.AxeUltimate);

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
                TryAddDraft(SpellButton.Primary,   Axe.AxePrimary);
                TryAddDraft(SpellButton.Movement,  Axe.AxeMovement);
                TryAddDraft(SpellButton.Melee,     Axe.AxeMelee);
                TryAddDraft(SpellButton.Secondary, Axe.AxeSecondary);
                TryAddDraft(SpellButton.Defensive, Axe.AxeDefensive);
                TryAddDraft(SpellButton.Utility,   Axe.AxeUtility);
                TryAddDraft(SpellButton.Ultimate,  Axe.AxeUltimate);
            }

            // ── UI colors ──────────────────────────────────────────────────
            // Expand spellColors to include index 11 (Axe/Tutorial slot)
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

            // ── Load bleed effect prefab for AxeMelee ─────────────────────
            try
            {
                var ignitePrefab = Resources.Load<GameObject>("Objects/Ignite");
                var igniteComp = ignitePrefab?.GetComponent<IgniteObject>();
                AxeMeleeObject.BleedEffectPrefab = igniteComp?.effect;
                Plugin.Log.LogInfo($"[AxeReg] BleedEffectPrefab loaded: {AxeMeleeObject.BleedEffectPrefab != null}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[AxeReg] BleedEffectPrefab load failed: {ex.Message}");
            }

            // ── Diagnostic: verify spell table state ──────────────────────────
            int axeCount = 0;
            foreach (var kv in spellTable)
            {
                if (kv.Value != null && kv.Value.element == Axe.Element)
                {
                    axeCount++;
                    Plugin.Log.LogInfo($"[AxeReg]   Axe spell in table: {kv.Key} btn={kv.Value.spellButton} el={kv.Value.element}");
                }
            }
            Plugin.Log.LogInfo($"[AxeReg] Registration complete. Axe spells in table: {axeCount}, axeSpellNames count: {axeSpellNames.Count}");
            Plugin.Log.LogInfo($"[AxeReg] Globals.spell_manager == manager: {Globals.spell_manager == manager}");
            // Verify the table reference is the same one Globals uses
            var globalsTable = Traverse.Create(Globals.spell_manager)
                .Field("spell_table")
                .GetValue<Dictionary<SpellName, Spell>>();
            Plugin.Log.LogInfo($"[AxeReg] spellTable ref == globals ref: {object.ReferenceEquals(spellTable, globalsTable)}");
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
                    Plugin.Log.LogWarning($"[AxeReg] Icon not found: {path}");
                    return null;
                }
                byte[] data = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, data))
                {
                    Plugin.Log.LogWarning($"[AxeReg] Failed to decode: {filename}");
                    return null;
                }
                var sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                Plugin.Log.LogInfo($"[AxeReg] Loaded icon from disk: {filename} ({tex.width}x{tex.height})");
                return sprite;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[AxeReg] LoadPngIcon failed for {filename}: {ex.Message}");
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
    }
}
