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
        TestSorcererCantripsSpent();
        TestBardCantripsSpent();
        TestWizardCantripsSpent();
        TestClericOrisonConversionSpent();
        TestDruidOrisonsPerDay();

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

        // The caster's component kit, as character creation and NPC spawn give it (batch 11 step 17): the castable list
        // also leaves out a spell whose components the caster lacks (Daze's M, Alarm's F).
        var inventory = go.AddComponent<InventoryComponent>();
        inventory.Init(stats);
        SpellComponentKit.Give(stats, inventory.CharacterInventory);

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

    // ========== SPL-138: 0-LEVEL SPELLS ARE SPELLS PER DAY ==========

    /// <summary>
    /// SPL-138: a sorcerer 1 has 5 0-level spells per day (PHB p.52 Table 3-16; no ability bonus at 0 level, PHB p.8
    /// Table 1-1). Each Daze spends one; the sixth is refused on every path (the AI candidate list is the PC menu list,
    /// GetCastablePreparedSpells); a rest restores them.
    /// </summary>
    private static void TestSorcererCantripsSpent()
    {
        CharacterController sorc = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 1, 10, 16, new List<string> { SpellNames.DAZE, SpellNames.MAGIC_MISSILE });
            SpellcastingComponent sc = sorc.Spellcasting;
            if (sc == null || !sc.IsSpontaneousCaster)
            {
                Assert(false, "Sorcerer 1 is a spontaneous caster (0-level test)");
                return;
            }

            int max = sc.SpontaneousData.GetMaxSlots(0);
            Assert(max == 5, "SPL-138: Sorcerer 1 with CHA 16 has 5 0-level spells per day (PHB p.52)", $"got {max}");
            SpellData daze = Spell(SpellNames.DAZE);
            bool allSpent = true;
            for (int i = 0; i < max; i++)
            {
                allSpent &= sc.CastSpellFromSlot(daze.Clone());
                allSpent &= sc.SpontaneousData.GetSlotsRemaining(0) == max - 1 - i;
            }
            Assert(allSpent, $"SPL-138: each of the {max} Daze casts spends one 0-level spell");
            AssertAvailability(sorc, SpellNames.DAZE, false, "SPL-138: Sorcerer 1, 0-level spells spent");
            Assert(!sc.CastSpellFromSlot(daze.Clone()), "SPL-138: a sixth Daze cannot be cast");
            Assert(sc.SpontaneousData.GetSlotsRemaining(1) == sc.SpontaneousData.GetMaxSlots(1),
                "SPL-138: spending 0-level spells leaves the 1st-level slots alone");
            AssertAvailability(sorc, SpellNames.MAGIC_MISSILE, true, "SPL-138: Sorcerer 1, 1st-level spell after the 0-level spells are spent");
            Assert(sc.GetSlotSummary().Contains("L0:0/" + max), "SPL-138: the slot summary shows the spent 0-level spells",
                $"got '{sc.GetSlotSummary()}'");

            sc.RestoreAllSlots();
            Assert(sc.SpontaneousData.GetSlotsRemaining(0) == max, "SPL-138: a rest restores the 0-level spells",
                $"got {sc.SpontaneousData.GetSlotsRemaining(0)}");
            AssertAvailability(sorc, SpellNames.DAZE, true, "SPL-138: Sorcerer 1 after a rest");
        }
        finally
        {
            Cleanup(sorc);
        }
    }

    /// <summary>SPL-138: a bard 3 has 3 0-level spells per day (PHB p.27 Table 3-4); a fourth Daze is refused.</summary>
    private static void TestBardCantripsSpent()
    {
        CharacterController bard = null;
        try
        {
            bard = CreateCaster("Bard", 3, 10, 16, new List<string> { SpellNames.DAZE, SpellNames.CHARM_PERSON, SpellNames.CURE_LIGHT_WOUNDS });
            SpellcastingComponent sc = bard.Spellcasting;
            if (sc == null || !sc.IsSpontaneousCaster)
            {
                Assert(false, "Bard 3 is a spontaneous caster (0-level test)");
                return;
            }

            int max = sc.SpontaneousData.GetMaxSlots(0);
            Assert(max == 3, "SPL-138: Bard 3 has 3 0-level spells per day (PHB p.27)", $"got {max}");
            SpellData daze = Spell(SpellNames.DAZE);
            bool allSpent = true;
            for (int i = 0; i < max; i++)
                allSpent &= sc.CastSpellFromSlot(daze.Clone());
            Assert(allSpent && sc.SpontaneousData.GetSlotsRemaining(0) == 0, $"SPL-138: {max} Daze casts spend every bard 0-level spell",
                $"left {sc.SpontaneousData.GetSlotsRemaining(0)}");
            AssertAvailability(bard, SpellNames.DAZE, false, "SPL-138: Bard 3, 0-level spells spent");
            AssertAvailability(bard, SpellNames.CHARM_PERSON, true, "SPL-138: Bard 3, 1st-level spell after the 0-level spells are spent");
        }
        finally
        {
            Cleanup(bard);
        }
    }

    /// <summary>
    /// SPL-138: a wizard's prepared cantrip is spent like any prepared spell (PHB p.177: Mialee prepares detect magic
    /// twice and casts it once; PHB p.55 Table 3-18 counts 0-level spells per day). The cantrip's unused-slot count,
    /// the 0-level remaining count, every availability path and the any-castable check all follow the used slots;
    /// a rest restores them.
    /// </summary>
    private static void TestWizardCantripsSpent()
    {
        CharacterController wizard = null;
        try
        {
            wizard = CreateCaster("Wizard", 3, 18, 10, null);
            SpellcastingComponent sc = wizard.Spellcasting;
            if (sc == null || sc.IsSpontaneousCaster)
            {
                Assert(false, "Wizard 3 is a prepared caster (0-level test)");
                return;
            }

            List<SpellSlot> cantripSlots = sc.SpellSlots.Where(s => s != null && s.Level == 0 && s.PreparedSpell != null).ToList();
            Assert(cantripSlots.Count == 4, "SPL-138: Wizard 3 has 4 prepared 0-level slots (PHB p.55)", $"got {cantripSlots.Count}");
            if (cantripSlots.Count == 0)
                return;

            SpellData cantrip = cantripSlots[0].PreparedSpell;
            int n = cantripSlots.Count(s => s.PreparedSpell.SpellId == cantrip.SpellId);
            int before = sc.GetSlotsRemaining(0);
            Assert(before == cantripSlots.Count, "SPL-138: every prepared 0-level slot counts as remaining before any cast",
                $"got {before}/{cantripSlots.Count}");
            Assert(sc.CountAvailablePreparedSpell(cantrip) == n, $"SPL-138: {cantrip.Name} counts its {n} unused slot(s), not an unlimited marker",
                $"got {sc.CountAvailablePreparedSpell(cantrip)}");

            bool allCast = true;
            for (int i = 0; i < n; i++)
                allCast &= sc.CastSpellFromSlot(cantrip.Clone());
            Assert(allCast, $"SPL-138: {cantrip.Name} is cast from each of its {n} slot(s)");
            Assert(sc.GetSlotsRemaining(0) == before - n, $"SPL-138: the 0-level remaining count drops by {n}",
                $"got {sc.GetSlotsRemaining(0)}, expected {before - n}");
            Assert(sc.CountAvailablePreparedSpell(cantrip) == 0, $"SPL-138: no unused {cantrip.Name} slot is left");
            Assert(!sc.GetUniqueAvailableSpells(0).Any(s => s != null && s.SpellId == cantrip.SpellId),
                $"SPL-138: the combat menu's 0-level list no longer offers {cantrip.Name}");
            AssertAvailability(wizard, cantrip.SpellId, false, $"SPL-138: Wizard 3, {cantrip.Name} spent");
            Assert(!sc.CastSpellFromSlot(cantrip.Clone()), $"SPL-138: {cantrip.Name} cannot be cast again before a rest");

            // Spend every remaining slot: with no unused slot left the caster has nothing to cast (before SPL-138 a
            // prepared cantrip kept HasAnyCastablePreparedSpell true forever).
            foreach (SpellSlot slot in sc.SpellSlots.Where(s => s != null && s.CanCast).ToList())
                sc.CastSpellFromSlot(slot.PreparedSpell.Clone());
            Assert(!sc.HasAnyCastablePreparedSpell(), "SPL-138: with every slot spent, 0-level ones included, nothing is castable",
                $"left: {sc.GetSlotDetails()}");
            Assert(sc.GetCastablePreparedSpells().Count == 0, "SPL-138: the PC menu and AI candidate list are empty with every slot spent");

            sc.RestoreAllSlots();
            Assert(sc.GetSlotsRemaining(0) == before, "SPL-138: a rest restores the 0-level slots", $"got {sc.GetSlotsRemaining(0)}");
            AssertAvailability(wizard, cantrip.SpellId, true, $"SPL-138: Wizard 3, {cantrip.Name} after a rest");
        }
        finally
        {
            Cleanup(wizard);
        }
    }

    /// <summary>
    /// SPL-138 with PHB p.32: a good cleric converts a prepared orison into Cure Minor Wounds, which spends that
    /// orison slot, on the combat menu's path (CanConvertSpellToSpontaneous, SpontaneousCastFromSpecificSpell) and the
    /// level-based one (CanSpontaneousCast, SpontaneousCastFromSlot); once every orison slot is spent no 0-level
    /// conversion is left.
    /// </summary>
    private static void TestClericOrisonConversionSpent()
    {
        CharacterController cleric = null;
        try
        {
            cleric = CreateCaster("Cleric", 3, 10, 10, null);
            SpellcastingComponent sc = cleric.Spellcasting;
            if (sc == null)
            {
                Assert(false, "Cleric 3 has a SpellcastingComponent");
                return;
            }
            cleric.Stats.SpontaneousCasting = SpontaneousCastingType.Cure;

            List<SpellSlot> orisonSlots = sc.SpellSlots.Where(s => s != null && s.Level == 0 && s.PreparedSpell != null).ToList();
            Assert(orisonSlots.Count == 4, "SPL-138: Cleric 3 has 4 prepared orison slots (PHB p.31)", $"got {orisonSlots.Count}");
            if (orisonSlots.Count == 0)
                return;

            Assert(sc.CanSpontaneousCast(0), "SPL-138: an unused orison can become Cure Minor Wounds (PHB p.32)");

            // The PC combat menu's path: the Convert button (CanConvertSpellToSpontaneous) and the spend of the
            // chosen orison (ConsumePendingSpellSlot -> SpontaneousCastFromSpecificSpell).
            SpellData chosen = orisonSlots[0].PreparedSpell;
            List<SpellSlot> chosenSlots = orisonSlots.Where(s => s.PreparedSpell.SpellId == chosen.SpellId).ToList();
            Assert(sc.CanConvertSpellToSpontaneous(chosen), $"SPL-138: the combat menu offers converting the prepared {chosen.Name}");
            List<bool> usedBefore = sc.SpellSlots.Select(s => s != null && s.IsUsed).ToList();
            Assert(sc.SpontaneousCastFromSpecificSpell(chosen.SpellId), $"SPL-138: converting the chosen {chosen.Name} succeeds");
            List<int> newlyUsed = Enumerable.Range(0, sc.SpellSlots.Count)
                .Where(i => sc.SpellSlots[i] != null && sc.SpellSlots[i].IsUsed && !usedBefore[i]).ToList();
            Assert(newlyUsed.Count == 1 && chosenSlots.Contains(sc.SpellSlots[newlyUsed[0]]),
                $"SPL-138: converting {chosen.Name} spends exactly one slot holding it", $"spent {newlyUsed.Count} slot(s)");
            bool restConverted = true;
            for (int i = 1; i < chosenSlots.Count; i++)
                restConverted &= sc.SpontaneousCastFromSpecificSpell(chosen.SpellId);
            Assert(restConverted && chosenSlots.All(s => s.IsUsed), $"SPL-138: each of the {chosenSlots.Count} slots holding {chosen.Name} converts once");
            Assert(!sc.CanConvertSpellToSpontaneous(chosen), $"SPL-138: with every {chosen.Name} spent the combat menu no longer offers converting it");
            Assert(!sc.SpontaneousCastFromSpecificSpell(chosen.SpellId), $"SPL-138: a further conversion of {chosen.Name} is refused");

            // The level-based path (CanSpontaneousCast / SpontaneousCastFromSlot) spends the remaining orisons.
            int remaining = orisonSlots.Count(s => !s.IsUsed);
            bool allConverted = true;
            for (int i = 0; i < remaining; i++)
                allConverted &= sc.SpontaneousCastFromSlot(0);
            Assert(allConverted, $"SPL-138: each of the other {remaining} orison slots converts once");
            Assert(orisonSlots.All(s => s.IsUsed), "SPL-138: every converted orison slot is spent");
            Assert(!sc.CanSpontaneousCast(0), "SPL-138: with every orison spent no 0-level conversion is left");
            Assert(!sc.SpontaneousCastFromSlot(0), "SPL-138: a further 0-level conversion is refused");
        }
        finally
        {
            Cleanup(cleric);
        }
    }

    /// <summary>
    /// SPL-138 with PHB p.35 Table 3-8: a druid's orisons per day are 3 at level 1, 4 at levels 2-3, 5 at 4-6 and 6
    /// at 7-20 (WIS gives no bonus 0-level spells, PHB p.8 Table 1-1). Since SPL-138 they cap the casts, not only the
    /// preparation; before it the count stayed 4 from level 3 up.
    /// </summary>
    private static void TestDruidOrisonsPerDay()
    {
        int[][] expected = { new[] { 1, 3 }, new[] { 2, 4 }, new[] { 3, 4 }, new[] { 4, 5 }, new[] { 5, 5 }, new[] { 6, 5 }, new[] { 7, 6 }, new[] { 8, 6 } };
        foreach (int[] row in expected)
        {
            CharacterController druid = null;
            try
            {
                druid = CreateCaster("Druid", row[0], 10, 10, null);
                SpellcastingComponent sc = druid.Spellcasting;
                if (sc == null)
                {
                    Assert(false, $"Druid {row[0]} has a SpellcastingComponent");
                    continue;
                }
                int max = sc.GetMaxSlots(0);
                int slots = sc.SpellSlots.Count(s => s != null && s.Level == 0);
                Assert(max == row[1] && slots == row[1], $"SPL-138: Druid {row[0]} has {row[1]} orisons per day (PHB p.35)", $"got max {max}, {slots} slots");
            }
            finally
            {
                Cleanup(druid);
            }
        }
    }
}
}
