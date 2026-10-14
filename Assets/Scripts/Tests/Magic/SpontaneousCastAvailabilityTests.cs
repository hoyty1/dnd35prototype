using System.Collections.Generic;
using System.Linq;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// SPL-123: a spontaneous caster's spell availability is one rule for the PC menu, the NPC cast executor and the
/// counterspell checks. Rules (checked 2026-10-14): PHB p.54 (a sorcerer casts any spell she knows without
/// preparing it, by spending one of her spells per day of that level), PHB p.28 (the bard likewise), MM p.69 (a
/// dragon of the right age casts arcane spells as a sorcerer), PHB p.170-171 (counterspelling with the same spell
/// needs that spell available to cast; a spontaneous caster spends a slot of its level).
/// Before SPL-123, <see cref="SpellcastingComponent.CanCast"/> (the NPC executor's and the counterspell check) read the
/// legacy SlotsRemaining view, which is empty for a spontaneous caster, so it refused every spell while the PC
/// menu's list (<see cref="SpellcastingComponent.GetCastablePreparedSpells"/>) offered them.
/// Builds controllers without CharacterController.Init (as CounterspellRulesTests does), so it runs in edit or Play mode.
/// </summary>
public static class SpontaneousCastAvailabilityTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Debug.Log("====== SPONTANEOUS CAST AVAILABILITY TESTS (SPL-123) ======");

        TestSorcererKnownSpells();
        TestSorcererSlotsSpent();
        TestBardKnownSpells();
        TestPreparedCasterUnchanged();
        TestDragonCastFirstFilter();

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }

    // ========== HELPERS ==========

    private static CharacterController CreateCaster(string cls, int level, int intelligence, int cha, List<string> known)
    {
        var go = new GameObject($"SpontaneousCastTest_{cls}{level}");
        var controller = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(
            name: cls + " " + level, level: level, characterClass: cls,
            str: 10, dex: 14, con: 12, wis: 10, intelligence: intelligence, cha: cha,
            bab: level / 2, armorBonus: 0, shieldBonus: 0,
            damageDice: 4, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 4 * level, raceName: "Human");
        controller.Stats = stats;

        var spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.SelectedSpellIds = known;
        spellComp.Init(stats);
        return controller;
    }

    private static void Cleanup(CharacterController controller)
    {
        if (controller != null && controller.gameObject != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static SpellData Spell(string id) => SpellDatabase.GetSpell(id);

    /// <summary>
    /// The NPC executor's check (CanCast), the counterspell check (HasSpellAvailableForCounter), the PC menu list
    /// (GetCastablePreparedSpells) and the character sheet check (CanCastSpell) agree with the expectation.
    /// </summary>
    private static void AssertAvailability(CharacterController c, string id, bool expected, string label)
    {
        SpellcastingComponent sc = c.Spellcasting;
        SpellData spell = Spell(id);
        if (spell == null)
        {
            Assert(false, $"{label}: {id} exists in SpellDatabase");
            return;
        }
        bool npc = sc.CanCast(spell);
        bool sheet = sc.CanCastSpell(spell);
        bool menu = sc.GetCastablePreparedSpells().Any(s => s != null && s.SpellId == id);
        bool counter = c.HasSpellAvailableForCounter(id);
        Assert(npc == expected, $"{label}: NPC executor check (CanCast) for {spell.Name} is {expected}", $"got {npc}");
        Assert(sheet == expected, $"{label}: CanCastSpell for {spell.Name} is {expected}", $"got {sheet}");
        Assert(menu == expected, $"{label}: PC menu list offers {spell.Name}: {expected}", $"got {menu}");
        Assert(counter == expected, $"{label}: counterspell availability of {spell.Name} is {expected}", $"got {counter}");
    }

    // ========== TESTS ==========

    /// <summary>A sorcerer 1 (CHA 16) knowing Daze and Magic Missile: both available, an unknown spell not.</summary>
    private static void TestSorcererKnownSpells()
    {
        CharacterController sorc = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 1, 10, 16, new List<string> { SpellNames.DAZE, SpellNames.MAGIC_MISSILE });
            SpellcastingComponent sc = sorc.Spellcasting;
            Assert(sc != null && sc.IsSpontaneousCaster, "Sorcerer 1 is a spontaneous caster");
            if (sc == null || !sc.IsSpontaneousCaster)
                return;

            AssertAvailability(sorc, SpellNames.MAGIC_MISSILE, true, "Sorcerer 1, known 1st-level spell with slots");
            AssertAvailability(sorc, SpellNames.DAZE, true, "Sorcerer 1, known 0-level spell");
            AssertAvailability(sorc, SpellNames.CHARM_PERSON, false, "Sorcerer 1, unknown 1st-level spell");
            AssertAvailability(sorc, SpellNames.FIREBALL, false, "Sorcerer 1, unknown 3rd-level spell");
        }
        finally
        {
            Cleanup(sorc);
        }
    }

    /// <summary>Spending every 1st-level slot makes Magic Missile unavailable on every path (PHB p.54).</summary>
    private static void TestSorcererSlotsSpent()
    {
        CharacterController sorc = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 1, 10, 16, new List<string> { SpellNames.DAZE, SpellNames.MAGIC_MISSILE });
            SpellcastingComponent sc = sorc.Spellcasting;
            if (sc == null || !sc.IsSpontaneousCaster)
            {
                Assert(false, "Sorcerer 1 is a spontaneous caster (slots test)");
                return;
            }

            // PHB p.52 Table 3-16: 3 1st-level spells per day at level 1, +1 bonus for CHA 16 (PHB p.8 Table 1-1).
            int max = sc.SpontaneousData.GetMaxSlots(1);
            Assert(max == 4, "Sorcerer 1 with CHA 16 has 4 1st-level slots", $"got {max}");

            SpellData missile = Spell(SpellNames.MAGIC_MISSILE);
            bool allSpent = true;
            for (int i = 0; i < max; i++)
                allSpent &= sc.CastSpellFromSlot(missile.Clone());
            Assert(allSpent, $"Each of the {max} casts spends a 1st-level slot");
            Assert(sc.SpontaneousData.GetSlotsRemaining(1) == 0, "No 1st-level slot is left",
                $"got {sc.SpontaneousData.GetSlotsRemaining(1)}");

            AssertAvailability(sorc, SpellNames.MAGIC_MISSILE, false, "Sorcerer 1, 1st-level slots spent");
            Assert(!sc.CastSpellFromSlot(missile.Clone()), "A fifth Magic Missile cannot be cast");
        }
        finally
        {
            Cleanup(sorc);
        }
    }

    /// <summary>A bard 3 (CHA 16) knowing Daze, Charm Person and Cure Light Wounds (Brd 0, 1, 1; PHB p.209, p.215, p.217).</summary>
    private static void TestBardKnownSpells()
    {
        CharacterController bard = null;
        try
        {
            bard = CreateCaster("Bard", 3, 10, 16, new List<string> { SpellNames.DAZE, SpellNames.CHARM_PERSON, SpellNames.CURE_LIGHT_WOUNDS });
            SpellcastingComponent sc = bard.Spellcasting;
            Assert(sc != null && sc.IsSpontaneousCaster, "Bard 3 is a spontaneous caster");
            if (sc == null || !sc.IsSpontaneousCaster)
                return;

            AssertAvailability(bard, SpellNames.CHARM_PERSON, true, "Bard 3, known 1st-level spell with slots");
            AssertAvailability(bard, SpellNames.CURE_LIGHT_WOUNDS, true, "Bard 3, known 1st-level healing spell");
            AssertAvailability(bard, SpellNames.DAZE, true, "Bard 3, known 0-level spell");
            AssertAvailability(bard, SpellNames.SLEEP, false, "Bard 3, unknown 1st-level spell");
        }
        finally
        {
            Cleanup(bard);
        }
    }

    /// <summary>Control: a wizard's prepared-slot availability is unchanged by the spontaneous branch.</summary>
    private static void TestPreparedCasterUnchanged()
    {
        CharacterController wizard = null;
        try
        {
            wizard = CreateCaster("Wizard", 3, 18, 10, null);
            SpellcastingComponent sc = wizard.Spellcasting;
            Assert(sc != null && !sc.IsSpontaneousCaster, "Wizard 3 is not a spontaneous caster");
            if (sc == null)
                return;

            SpellData prepared = sc.SpellSlots.Where(s => s != null && s.Level >= 1 && s.PreparedSpell != null)
                .Select(s => s.PreparedSpell).FirstOrDefault();
            Assert(prepared != null, "Wizard 3 has a prepared 1st- or 2nd-level spell");
            if (prepared == null)
                return;
            AssertAvailability(wizard, prepared.SpellId, true, $"Wizard 3, prepared {prepared.Name}");

            // A 1st-level spell in the spellbook but in no slot: refused by the preparation check itself (a 3rd-level
            // spell would be refused earlier, for want of a 3rd-level slot).
            var preparedIds = new HashSet<string>(sc.SpellSlots.Where(s => s != null && s.PreparedSpell != null).Select(s => s.PreparedSpell.SpellId));
            SpellData unprepared = sc.KnownSpells.FirstOrDefault(s => s != null && s.SpellLevel == 1 && !preparedIds.Contains(s.SpellId));
            Assert(unprepared != null, "Wizard 3 knows a 1st-level spell it did not prepare");
            if (unprepared != null)
                AssertAvailability(wizard, unprepared.SpellId, false, $"Wizard 3, known but unprepared {unprepared.Name}");
        }
        finally
        {
            Cleanup(wizard);
        }
    }

    /// <summary>
    /// AI-037 (since SPL-123): the spells an AI-run dragon's cast-first step may choose. Never a cantrip; with the
    /// breath ready or an enemy adjacent, only a buff (AIService.IsDragonCastFirstCandidate).
    /// </summary>
    private static void TestDragonCastFirstFilter()
    {
        SpellData flare = Spell(SpellNames.FLARE), shield = Spell(SpellNames.SHIELD), missile = Spell(SpellNames.MAGIC_MISSILE);
        Assert(flare != null && shield != null && missile != null, "Flare, Shield and Magic Missile exist in SpellDatabase");
        if (flare == null || shield == null || missile == null)
            return;
        Assert(!AIService.IsDragonCastFirstCandidate(flare, false), "Dragon cast-first: a cantrip (Flare) is never chosen, breath recharging");
        Assert(!AIService.IsDragonCastFirstCandidate(flare, true), "Dragon cast-first: a cantrip (Flare) is never chosen, breath ready");
        Assert(AIService.IsDragonCastFirstCandidate(shield, true), "Dragon cast-first: a 1st-level buff (Shield) is allowed with the breath ready");
        Assert(!AIService.IsDragonCastFirstCandidate(missile, true), "Dragon cast-first: an attack spell (Magic Missile) waits while the breath is ready");
        Assert(AIService.IsDragonCastFirstCandidate(missile, false), "Dragon cast-first: an attack spell (Magic Missile) is allowed while the breath recharges");
    }
}
}
