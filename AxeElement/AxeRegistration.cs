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

            // ── AxePrimary (Primary) ─────────────────────────────────────────
            var axePrimary = manager.gameObject.AddComponent<AxePrimary>();
            axePrimary.spellName        = Axe.Hatchet;
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
            AssignAssets(axePrimary, SpellButton.Primary, metalIcons, metalVideos);
            TintIconLighter(axePrimary);
            spellTable[Axe.Hatchet]     = axePrimary;
            axeSpellNames.Add(Axe.Hatchet);

            // ── Lunge (Movement) ───────────────────────────────────────────
            var lunge = manager.gameObject.AddComponent<Lunge>();
            lunge.spellName         = Axe.Lunge;
            lunge.element           = Axe.Element;
            lunge.spellButton       = SpellButton.Movement;
            lunge.description       = "Lunge forward and latch onto an enemy; recast to reel them in.";
            lunge.cooldown          = 6f;
            lunge.windUp            = 0.45f;
            lunge.windDown          = 0.4f;
            lunge.animationName     = "FlameLeap";
            lunge.curveMultiplier   = 2f;
            lunge.initialVelocity   = 20f;
            lunge.minRange          = 8f;
            lunge.maxRange          = 20f;
            lunge.uses              = SpellUses.Move;
            lunge.reactivate        = 1;
            lunge.additionalCasts   = new SubSpell[]
            {
                new SubSpell
                {
                    animationName    = "Attack",
                    cooldown         = 0f,
                    windUp           = 0.1f,
                    windDown         = 0.3f,
                    activationWindow = 4f,
                    startsDisabled   = true,
                    curveMultiplier  = 0f,
                    initialVelocity  = 0f,
                    minRange         = 0f,
                    maxRange         = 25f,
                    uses             = SpellUses.Move | SpellUses.Attack
                }
            };
            AssignAssets(lunge, SpellButton.Movement, metalIcons, metalVideos);
            spellTable[Axe.Lunge] = lunge;
            axeSpellNames.Add(Axe.Lunge);

            // ── Cleave (Melee) ─────────────────────────────────────────────
            var cleave = manager.gameObject.AddComponent<Cleave>();
            cleave.spellName        = Axe.Cleave;
            cleave.element          = Axe.Element;
            cleave.spellButton      = SpellButton.Melee;
            cleave.description      = "Slam the ground around you, chaining nearby enemies to a heavy ball.";
            cleave.cooldown         = 5f;
            cleave.windUp           = 0.35f;
            cleave.windDown         = 0.35f;
            cleave.animationName    = "Melee";
            cleave.curveMultiplier  = 0f;
            cleave.initialVelocity  = 0f;
            cleave.minRange         = 0f;
            cleave.maxRange         = 4f;
            cleave.uses             = SpellUses.Attack;
            cleave.additionalCasts  = new SubSpell[0];
            AssignAssets(cleave, SpellButton.Melee, metalIcons, metalVideos);
            spellTable[Axe.Cleave]  = cleave;
            axeSpellNames.Add(Axe.Cleave);

            // ── Tomahawk (Secondary) ───────────────────────────────────────
            var tomahawk = manager.gameObject.AddComponent<Tomahawk>();
            tomahawk.spellName       = Axe.Tomahawk;
            tomahawk.element         = Axe.Element;
            tomahawk.spellButton     = SpellButton.Secondary;
            tomahawk.description     = "Throw a tomahawk that sticks to the first target, then leaps to a second and pulls them together.";
            tomahawk.cooldown        = 7f;
            tomahawk.windUp          = 0.5f;
            tomahawk.windDown        = 0.4f;
            tomahawk.animationName   = "Secondary Spell";
            tomahawk.curveMultiplier = 1.5f;
            tomahawk.initialVelocity = 25f;
            tomahawk.minRange        = 0f;
            tomahawk.maxRange        = 40f;
            tomahawk.uses            = SpellUses.Attack;
            tomahawk.additionalCasts = new SubSpell[0];
            AssignAssets(tomahawk, SpellButton.Secondary, metalIcons, metalVideos);
            spellTable[Axe.Tomahawk] = tomahawk;
            axeSpellNames.Add(Axe.Tomahawk);

            // ── IronWard (Defensive) ───────────────────────────────────────
            var ironWard = manager.gameObject.AddComponent<IronWard>();
            ironWard.spellName       = Axe.IronWard;
            ironWard.element         = Axe.Element;
            ironWard.spellButton     = SpellButton.Defensive;
            ironWard.description     = "Summon a ward that intercepts incoming damage and hurls it back at the attacker.";
            ironWard.cooldown        = 10f;
            ironWard.windUp          = 0.35f;
            ironWard.windDown        = 0.35f;
            ironWard.animationName   = "Defensive";
            ironWard.curveMultiplier = 0f;
            ironWard.initialVelocity = 0f;
            ironWard.minRange        = 0f;
            ironWard.maxRange        = 0f;
            ironWard.uses            = SpellUses.Defend | SpellUses.Custom;
            ironWard.additionalCasts = new SubSpell[0];
            AssignAssets(ironWard, SpellButton.Defensive, metalIcons, metalVideos);
            spellTable[Axe.IronWard] = ironWard;
            axeSpellNames.Add(Axe.IronWard);

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
                TryAddDraft(SpellButton.Primary,   Axe.Hatchet);
                TryAddDraft(SpellButton.Movement,  Axe.Lunge);
                TryAddDraft(SpellButton.Melee,     Axe.Cleave);
                TryAddDraft(SpellButton.Secondary, Axe.Tomahawk);
                TryAddDraft(SpellButton.Defensive, Axe.IronWard);
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
