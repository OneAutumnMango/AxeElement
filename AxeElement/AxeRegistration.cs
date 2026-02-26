using System.Collections.Generic;
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
        public static void RegisterSpells(SpellManager manager, Dictionary<SpellName, Spell> spellTable)
        {
            // Reassign existing Ice spells to Tutorial element slot (11)
            // so the Axe element slot (10) is exclusively Axe.
            foreach (var kv in spellTable)
            {
                if (kv.Value != null && kv.Value.element == Element.Ice)
                    kv.Value.element = Element.Tutorial;
            }

            // ── Hatchet (Primary) ──────────────────────────────────────────
            var hatchet = manager.gameObject.AddComponent<Hatchet>();
            hatchet.spellName        = Axe.Hatchet;
            hatchet.element          = Axe.Element;
            hatchet.spellButton      = SpellButton.Primary;
            hatchet.description      = "Hurl a spinning hatchet that homes in on a second target after the first hit.";
            hatchet.cooldown         = 1.5f;
            hatchet.windUp           = 0.35f;
            hatchet.windDown         = 0.3f;
            hatchet.animationName    = "Attack";
            hatchet.curveMultiplier  = 0.5f;
            hatchet.initialVelocity  = 0.5f;
            hatchet.minRange         = 5f;
            hatchet.maxRange         = 30f;
            hatchet.uses             = SpellUses.Attack;
            spellTable[Axe.Hatchet]  = hatchet;

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
            lunge.curveMultiplier   = 0.5f;
            lunge.initialVelocity   = 0.5f;
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
            spellTable[Axe.Lunge] = lunge;

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
            spellTable[Axe.Cleave]  = cleave;

            // ── Tomahawk (Secondary) ───────────────────────────────────────
            var tomahawk = manager.gameObject.AddComponent<Tomahawk>();
            tomahawk.spellName       = Axe.Tomahawk;
            tomahawk.element         = Axe.Element;
            tomahawk.spellButton     = SpellButton.Secondary;
            tomahawk.description     = "Throw a tomahawk that sticks to the first target, then leaps to a second and pulls them together.";
            tomahawk.cooldown        = 7f;
            tomahawk.windUp          = 0.5f;
            tomahawk.windDown        = 0.4f;
            tomahawk.animationName   = "Attack";
            tomahawk.curveMultiplier = 1.0f;
            tomahawk.initialVelocity = 0.35f;
            tomahawk.minRange        = 5f;
            tomahawk.maxRange        = 30f;
            tomahawk.uses            = SpellUses.Attack;
            spellTable[Axe.Tomahawk] = tomahawk;

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
            spellTable[Axe.IronWard] = ironWard;

            // ── Shatter (Utility) ──────────────────────────────────────────
            var shatter = manager.gameObject.AddComponent<Shatter>();
            shatter.spellName        = Axe.Shatter;
            shatter.element          = Axe.Element;
            shatter.spellButton      = SpellButton.Utility;
            shatter.description      = "Launch a shatter-blade that, on hit, calls down a crushing hammer on the target.";
            shatter.cooldown         = 8f;
            shatter.windUp           = 0.35f;
            shatter.windDown         = 0.35f;
            shatter.animationName    = "Attack";
            shatter.curveMultiplier  = 0.5f;
            shatter.initialVelocity  = 0.4f;
            shatter.minRange         = 5f;
            shatter.maxRange         = 30f;
            shatter.uses             = SpellUses.Attack;
            spellTable[Axe.Shatter]  = shatter;

            // ── Whirlwind (Ultimate) ───────────────────────────────────────
            var whirlwind = manager.gameObject.AddComponent<Whirlwind>();
            whirlwind.spellName       = Axe.Whirlwind;
            whirlwind.element         = Axe.Element;
            whirlwind.spellButton     = SpellButton.Ultimate;
            whirlwind.description     = "Enter a berserker state; teleport to and devastate every enemy that has hurt you.";
            whirlwind.cooldown        = 20f;
            whirlwind.windUp          = 1.3f;
            whirlwind.windDown        = 0.5f;
            whirlwind.animationName   = "SelfCast";
            whirlwind.curveMultiplier = 1.0f;
            whirlwind.initialVelocity = 0f;
            whirlwind.minRange        = 0f;
            whirlwind.maxRange        = 0f;
            whirlwind.uses            = SpellUses.Attack | SpellUses.Custom;
            spellTable[Axe.Whirlwind] = whirlwind;

            // ── AI draft priority ──────────────────────────────────────────
            var aiDraft = Traverse.Create(manager)
                .Field("ai_draft_priority")
                .GetValue<Dictionary<SpellButton, List<SpellName>>>();

            if (aiDraft != null)
            {
                if (aiDraft.ContainsKey(SpellButton.Primary))
                    aiDraft[SpellButton.Primary].Add(Axe.Hatchet);
                if (aiDraft.ContainsKey(SpellButton.Movement))
                    aiDraft[SpellButton.Movement].Add(Axe.Lunge);
                if (aiDraft.ContainsKey(SpellButton.Melee))
                    aiDraft[SpellButton.Melee].Add(Axe.Cleave);
                if (aiDraft.ContainsKey(SpellButton.Secondary))
                    aiDraft[SpellButton.Secondary].Add(Axe.Tomahawk);
                if (aiDraft.ContainsKey(SpellButton.Defensive))
                    aiDraft[SpellButton.Defensive].Add(Axe.IronWard);
                if (aiDraft.ContainsKey(SpellButton.Utility))
                    aiDraft[SpellButton.Utility].Add(Axe.Shatter);
                if (aiDraft.ContainsKey(SpellButton.Ultimate))
                    aiDraft[SpellButton.Ultimate].Add(Axe.Whirlwind);
            }

            // ── UI colors ──────────────────────────────────────────────────
            // Steel/grey spell color for the Axe element in cooldown UI
            if (manager.spellColors != null && manager.spellColors.Length > 10)
                manager.spellColors[10] = new Color(0.6f, 0.6f, 0.65f);

            // Steel/grey icon emission color
            if (Globals.iconEmissionColors != null && Globals.iconEmissionColors.Length > 10)
                Globals.iconEmissionColors[10] = new Color(0.35f, 0.35f, 0.38f);
        }
    }
}
