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

            // ── AxePrimary (Primary) ─────────────────────────────────────────
            var axePrimary = manager.gameObject.AddComponent<AxePrimary>();
            axePrimary.spellName        = Axe.AxePrimary;
            axePrimary.element          = Axe.Element;
            axePrimary.spellButton      = SpellButton.Primary;
            axePrimary.description      = "Hurl a spinning axe-blade that explodes on impact, dealing area damage.";
            axePrimary.cooldown         = 1.5f;
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
            TintIconLighter(axePrimary);
            spellTable[Axe.AxePrimary]     = axePrimary;
            axeSpellNames.Add(Axe.AxePrimary);

            // ── AxeMovement (Movement) ──────────────────────────────────────────────
            var axeMovement = manager.gameObject.AddComponent<AxeMovement>();
            axeMovement.spellName         = Axe.AxeMovement;
            axeMovement.element           = Axe.Element;
            axeMovement.spellButton       = SpellButton.Movement;
            axeMovement.description       = "Step back and dash forward, striking all enemies in your path. Press again to dash up to 3 times.";
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
                    windDown         = 0.2f,
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
            spellTable[Axe.AxeMovement] = axeMovement;
            axeSpellNames.Add(Axe.AxeMovement);

            // ── AxeMelee (Melee) ───────────────────────────────────────────
            var axeMelee = manager.gameObject.AddComponent<AxeMelee>();
            axeMelee.spellName        = Axe.AxeMelee;
            axeMelee.element          = Axe.Element;
            axeMelee.spellButton      = SpellButton.Melee;
            axeMelee.description      = "Slam with your axe to wound nearby enemies; spells deal more damage to bleeding targets.";
            axeMelee.cooldown         = 5f;
            axeMelee.windUp           = 0.35f;
            axeMelee.windDown         = 0.35f;
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
            TintIconGreyscale(axeMelee);
            spellTable[Axe.AxeMelee]    = axeMelee;
            axeSpellNames.Add(Axe.AxeMelee);

            // ── AxeSecondary (Secondary) ────────────────────────────────────
            var axeSecondary = manager.gameObject.AddComponent<AxeSecondary>();
            axeSecondary.spellName       = Axe.AxeSecondary;
            axeSecondary.element         = Axe.Element;
            axeSecondary.spellButton     = SpellButton.Secondary;
            axeSecondary.description     = "Throw two axes that arc outward and converge, piercing through all enemies they pass through.";
            axeSecondary.cooldown        = 10f;
            axeSecondary.windUp          = 0.4f;
            axeSecondary.windDown        = 0.35f;
            axeSecondary.animationName   = "Secondary Spell";
            axeSecondary.curveMultiplier = 0f;
            axeSecondary.initialVelocity = 30f;
            axeSecondary.minRange        = 0f;
            axeSecondary.maxRange        = 28f;
            axeSecondary.uses            = SpellUses.Attack;
            axeSecondary.additionalCasts = new SubSpell[0];
            AssignAssets(axeSecondary, SpellButton.Primary, metalIcons, metalVideos); // start from primary icon
            TintIconLighter(axeSecondary);
            spellTable[Axe.AxeSecondary] = axeSecondary;
            axeSpellNames.Add(Axe.AxeSecondary);

            // ── AxeDefensive (Defensive) ───────────────────────────────────
            var axeDefensive = manager.gameObject.AddComponent<AxeDefensive>();
            axeDefensive.spellName       = Axe.AxeDefensive;
            axeDefensive.element         = Axe.Element;
            axeDefensive.spellButton     = SpellButton.Defensive;
            axeDefensive.description     = "Stand your ground for 1 second; if struck, teleport to the attacker and deal 5 damage.";
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
            TintIconLighter(axeDefensive);                                              // lighten like Primary
            spellTable[Axe.AxeDefensive] = axeDefensive;
            axeSpellNames.Add(Axe.AxeDefensive);

            // ── Shatter (Utility) ──────────────────────────────────────────
            var shatter = manager.gameObject.AddComponent<Shatter>();
            shatter.spellName        = Axe.Shatter;
            shatter.element          = Axe.Element;
            shatter.spellButton      = SpellButton.Utility;
            shatter.description      = "Launch a shatter-blade that, on hit, calls down a crushing hammer on the target.";
            shatter.cooldown         = 8f;
            shatter.windUp           = 0.35f;
            shatter.windDown         = 0.35f;
            shatter.animationName    = "Spell 360";
            shatter.curveMultiplier  = 1.5f;
            shatter.initialVelocity  = 35f;
            shatter.minRange         = 0f;
            shatter.maxRange         = 40f;
            shatter.uses             = SpellUses.Attack;
            shatter.additionalCasts  = new SubSpell[0];
            AssignAssets(shatter, SpellButton.Utility, metalIcons, metalVideos);
            spellTable[Axe.Shatter]  = shatter;
            axeSpellNames.Add(Axe.Shatter);

            // ── Whirlwind (Ultimate) ───────────────────────────────────────
            var whirlwind = manager.gameObject.AddComponent<Whirlwind>();
            whirlwind.spellName       = Axe.Whirlwind;
            whirlwind.element         = Axe.Element;
            whirlwind.spellButton     = SpellButton.Ultimate;
            whirlwind.description     = "Enter a berserker state; teleport to and devastate every enemy that has hurt you.";
            whirlwind.cooldown        = 20f;
            whirlwind.windUp          = 1.3f;
            whirlwind.windDown        = 0.5f;
            whirlwind.animationName   = "Spell Channel";
            whirlwind.curveMultiplier = 0f;
            whirlwind.initialVelocity = 0f;
            whirlwind.minRange        = 0f;
            whirlwind.maxRange        = 0f;
            whirlwind.uses            = SpellUses.Attack | SpellUses.Custom;
            whirlwind.additionalCasts = new SubSpell[0];
            AssignAssets(whirlwind, SpellButton.Ultimate, metalIcons, metalVideos);
            spellTable[Axe.Whirlwind] = whirlwind;
            axeSpellNames.Add(Axe.Whirlwind);

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
                TryAddDraft(SpellButton.Utility,   Axe.Shatter);
                TryAddDraft(SpellButton.Ultimate,  Axe.Whirlwind);
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
                manager.spellColors[11] = new Color(0.6f, 0.6f, 0.65f);

            // Expand iconEmissionColors to include index 11
            if (Globals.iconEmissionColors != null && Globals.iconEmissionColors.Length <= 11)
            {
                var expanded = new Color[12];
                Globals.iconEmissionColors.CopyTo(expanded, 0);
                Globals.iconEmissionColors = expanded;
            }
            if (Globals.iconEmissionColors != null && Globals.iconEmissionColors.Length > 11)
                Globals.iconEmissionColors[11] = new Color(0.35f, 0.35f, 0.38f);

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

        private static void TintIconLighter(Spell spell)
        {
            if (spell.icon == null) return;
            try
            {
                Sprite original = spell.icon;
                int w = (int)original.rect.width;
                int h = (int)original.rect.height;
                Texture2D readableTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

                // RenderTexture blit to read non-readable source textures
                RenderTexture rt = RenderTexture.GetTemporary(
                    original.texture.width, original.texture.height, 0, RenderTextureFormat.Default);
                Graphics.Blit(original.texture, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                readableTex.ReadPixels(new Rect(
                    original.rect.x,
                    original.texture.height - original.rect.y - original.rect.height,
                    w, h), 0, 0);
                readableTex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                // Blend RGB 40% toward white, preserve original alpha
                Color[] pixels = readableTex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(
                        Mathf.Lerp(pixels[i].r, 1f, 0.4f),
                        Mathf.Lerp(pixels[i].g, 1f, 0.4f),
                        Mathf.Lerp(pixels[i].b, 1f, 0.4f),
                        pixels[i].a);
                }
                readableTex.SetPixels(pixels);
                readableTex.Apply();

                spell.icon = Sprite.Create(
                    readableTex,
                    new Rect(0, 0, w, h),
                    new Vector2(0.5f, 0.5f),
                    original.pixelsPerUnit);
                Plugin.Log.LogInfo("[AxeReg] Tinted Primary icon lighter successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[AxeReg] Icon tinting failed (using original): {ex.Message}");
            }
        }

        private static void TintIconGreyscale(Spell spell)
        {
            if (spell.icon == null) return;
            try
            {
                Sprite original = spell.icon;
                int w = (int)original.rect.width;
                int h = (int)original.rect.height;
                Texture2D readableTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

                RenderTexture rt = RenderTexture.GetTemporary(
                    original.texture.width, original.texture.height, 0, RenderTextureFormat.Default);
                Graphics.Blit(original.texture, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                readableTex.ReadPixels(new Rect(
                    original.rect.x,
                    original.texture.height - original.rect.y - original.rect.height,
                    w, h), 0, 0);
                readableTex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                // Convert to greyscale, then lighten 40% toward white; preserve alpha
                Color[] pixels = readableTex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    float lum = pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f;
                    float light = Mathf.Lerp(lum, 1f, 0.4f) * 0.9f;
                    pixels[i] = new Color(light, light, light, pixels[i].a);
                }
                readableTex.SetPixels(pixels);
                readableTex.Apply();

                spell.icon = Sprite.Create(
                    readableTex,
                    new Rect(0, 0, w, h),
                    new Vector2(0.5f, 0.5f),
                    original.pixelsPerUnit);
                Plugin.Log.LogInfo("[AxeReg] Converted Melee icon to greyscale successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[AxeReg] Greyscale icon conversion failed (using original): {ex.Message}");
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
