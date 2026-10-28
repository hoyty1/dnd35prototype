using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// D13 of the batch 12 design (SPL-018, SPL-090): the one saving throw roll, SaveRollRules.Roll, and the resolvers built
/// on it (SavingThrowResolver.ResolveSave, SpellSaveResolver.RollSave). PHB p.177: a natural 1 on a save always fails
/// and a natural 20 always succeeds; the Luck domain reroll (PHB p.187) is armed once and spent, and keeps the better of
/// two d20s (the current deviation CHR-063: PHB p.187 makes the reroll stand even when it is worse); PHB p.29: a bard's
/// countersong result replaces a lower save against a sonic or language-dependent effect (and, once it does, the save's
/// natural 1 no longer decides it; on a natural 1 any Perform result that meets the DC is used: delegated RAW reading
/// 2026-10-28, RULES_COVERAGE 4.1); PHB p.50: Evasion takes no
/// damage on a successful Reflex save for half, here the Javelin of Lightning's bolt (DMG p.226). Dice are forced through
/// ScenarioHooks.RollFilter. Play mode: the countersong reads the scene GameManager's combatants.
/// </summary>
public static class SaveRollRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SAVE ROLL RULES TESTS (D13, SPL-018, SPL-090) ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();
        ItemDatabase.Init();

        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            TestNaturalOne();
            TestNaturalTwenty();
            TestNaturalNoteOnlyWhenItDecides();
            TestLuckReroll();
            TestCountersong();
            TestJavelinEvasion();
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
        }

        Debug.Log($"====== Save Roll Rules Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✅ PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  ❌ FAIL: {testName} {detail}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static CharacterStats Build(string name, string cls = "Fighter", int level = 1, int perform = -1)
    {
        var stats = new CharacterStats(
            name: name, level: level, characterClass: cls,
            str: 12, dex: 12, con: 12, wis: 10, intelligence: 10, cha: 16,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: level * 8, raceName: "Human");
        stats.EnsureMulticlassDataInitialized();
        if (perform >= 0)
        {
            stats.Skills["Perform"] = new Skill("Perform", AbilityType.CHA, isClassSkill: true) { Ranks = perform };
            stats.EnsureBardicMusicInitialized();
        }
        return stats;
    }

    private static CharacterStats WithSaveBonus(CharacterStats stats, int bonus)
    {
        stats.Bonuses.Set("test:save-roll", BonusTarget.AllSaves, BonusType.Untyped, bonus, "save roll test");
        return stats;
    }

    private static CharacterController Create(CharacterStats stats, Vector2Int pos)
    {
        var go = new GameObject(stats.CharacterName + "_SaveRollRulesTest");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        controller.Init(stats, pos, null, null);
        inventory.Init(stats);
        controller.SetTeam(CharacterTeam.Player);
        controller.GridPosition = pos;
        stats.AdjustMaxHP(200);
        stats.CurrentHP += 200;
        return controller;
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        GameManager gm = GameManager.Instance;
        foreach (CharacterController c in controllers)
        {
            if (c == null)
                continue;
            gm?.RemoveAllConditions(c);
            if (gm != null && gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(c);
            Object.DestroyImmediate(c.gameObject);
        }
    }

    /// <summary>Every d20 comes up <paramref name="d20"/>; other dice are left as rolled.</summary>
    private static System.Func<int, string, int, int> ForceD20(int d20)
        => (sides, ctx, natural) => sides == 20 ? d20 : natural;

    /// <summary>The d20s come up <paramref name="sequence"/> in order (then as rolled).</summary>
    private static System.Func<int, string, int, int> D20Sequence(params int[] sequence)
    {
        int next = 0;
        return (sides, ctx, natural) => sides == 20 && next < sequence.Length ? sequence[next++] : natural;
    }

    // ── Natural 1 and 20 (PHB p.177) ──────────────────────────────

    private static void TestNaturalOne()
    {
        CharacterStats stats = WithSaveBonus(Build("NatOne"), 30);
        ScenarioHooks.RollFilter = ForceD20(1);

        SaveRoll roll = SaveRollRules.Roll(stats, SavingThrowType.Will, 15, SaveContext.None, "test save");
        Assert(roll.Roll == 1 && roll.NaturalOne && roll.Total >= 15 && !roll.Succeeded,
            "A natural 1 fails even at +30 against DC 15 (PHB p.177)", $"roll {roll.Roll} total {roll.Total} succeeded {roll.Succeeded}");
        Assert(roll.DecidedByNatural && roll.NaturalSuffix == " (natural 1)", "The log note names the natural 1", roll.NaturalSuffix);

        SavingThrowResolver.SaveResult resolver = SavingThrowResolver.ResolveSave(stats, SavingThrowResolver.SaveType.Fortitude, 15, "test");
        Assert(!resolver.Succeeded && resolver.NaturalOne && resolver.LogMessage.EndsWith("(natural 1)"),
            "SavingThrowResolver.ResolveSave: a natural 1 fails at +30, and its log says so", resolver.LogMessage);

        SaveResult spell = SpellSaveResolver.RollSave((CharacterController)null, SaveType.Reflex, 15, null);
        Assert(!spell.Saved && spell.NaturalOne, "SpellSaveResolver.RollSave: a natural 1 fails", $"saved {spell.Saved}");
        SaveResult spellStats = SpellSaveResolver.RollSave(stats, SaveType.Reflex, 15);
        Assert(!spellStats.Saved && spellStats.Total >= 15 && spellStats.NaturalSuffix == " (natural 1)",
            "SpellSaveResolver.RollSave (stats): a natural 1 fails at +30", $"total {spellStats.Total} saved {spellStats.Saved}");

        Assert(!SavingThrowResolver.QuickSave(stats, SavingThrowResolver.SaveType.Will, 2), "QuickSave: a natural 1 fails at +30 against DC 2");
    }

    private static void TestNaturalTwenty()
    {
        CharacterStats stats = WithSaveBonus(Build("NatTwenty"), -30);
        ScenarioHooks.RollFilter = ForceD20(20);

        SaveRoll roll = SaveRollRules.Roll(stats, SavingThrowType.Fortitude, 25, SaveContext.None, "test save");
        Assert(roll.Roll == 20 && roll.NaturalTwenty && roll.Total < 25 && roll.Succeeded,
            "A natural 20 succeeds even at -10 or worse against DC 25 (PHB p.177)", $"total {roll.Total} succeeded {roll.Succeeded}");
        Assert(roll.NaturalSuffix == " (natural 20)", "The log note names the natural 20", roll.NaturalSuffix);

        SavingThrowResolver.SaveResult resolver = SavingThrowResolver.ResolveSave(stats, SavingThrowResolver.SaveType.Will, 25, "test");
        Assert(resolver.Succeeded && resolver.NaturalTwenty, "SavingThrowResolver.ResolveSave: a natural 20 succeeds", resolver.LogMessage);

        SaveResult spell = SpellSaveResolver.RollSave(stats, SaveType.Will, 40);
        Assert(spell.Saved && spell.NaturalTwenty, "SpellSaveResolver.RollSave: a natural 20 succeeds against DC 40", $"total {spell.Total}");
    }

    private static void TestNaturalNoteOnlyWhenItDecides()
    {
        CharacterStats stats = Build("NatQuiet");
        ScenarioHooks.RollFilter = ForceD20(1);
        SaveRoll lowOne = SaveRollRules.Roll(stats, SavingThrowType.Will, 15, SaveContext.None, null);
        Assert(!lowOne.Succeeded && !lowOne.DecidedByNatural && lowOne.NaturalSuffix.Length == 0,
            "A natural 1 that misses the DC anyway adds no note", lowOne.NaturalSuffix);

        ScenarioHooks.RollFilter = ForceD20(10);
        SaveRoll plain = SaveRollRules.Roll(stats, SavingThrowType.Will, 10, SaveContext.None, null, extraModifier: 2);
        Assert(plain.Succeeded && plain.Modifier == SaveRules.Modifier(stats, SavingThrowType.Will) + 2 && plain.Total == 10 + plain.Modifier,
            "Otherwise total = d20 + SaveRules.Modifier + the extra modifier, compared with the DC", $"total {plain.Total}");
    }

    // ── Luck domain reroll (PHB p.187; keeps the better roll, CHR-063) ──

    private static void TestLuckReroll()
    {
        CharacterStats stats = Build("Lucky", "Cleric");
        stats.LuckRerollPending = true;
        ScenarioHooks.RollFilter = D20Sequence(3, 17);
        SaveRoll roll = SaveRollRules.Roll(stats, SavingThrowType.Will, 15, SaveContext.None, "luck save");
        Assert(roll.Roll == 17 && !stats.LuckRerollPending,
            "An armed Luck reroll replaces a 3 with the reroll's 17, and is spent", $"roll {roll.Roll} pending {stats.LuckRerollPending}");

        stats.LuckRerollPending = true;
        ScenarioHooks.RollFilter = D20Sequence(18, 4);
        SaveResult keep = SpellSaveResolver.RollSave(stats, SaveType.Fortitude, 15);
        Assert(keep.Roll == 18 && !stats.LuckRerollPending, "The better roll stands (18 over a reroll of 4), through SpellSaveResolver too (CHR-063, not RAW: PHB p.187 forces the reroll)",
            $"roll {keep.Roll}");

        ScenarioHooks.RollFilter = D20Sequence(3, 17);
        SaveRoll unarmed = SaveRollRules.Roll(stats, SavingThrowType.Will, 15, SaveContext.None, null);
        Assert(unarmed.Roll == 3, "With no reroll armed, the first d20 counts", $"roll {unarmed.Roll}");
    }

    // ── Countersong (PHB p.29) ────────────────────────────────────

    private static void TestCountersong()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("  ⚠ SaveRollRulesTests: no scene GameManager; countersong checks skipped (Play mode needed).");
            return;
        }

        CharacterController bard = null, near = null;
        try
        {
            bard = Create(Build("SrBard", "Bard", 1, perform: 4), new Vector2Int(-120, -30));
            near = Create(Build("SrNear"), new Vector2Int(-117, -30));
            var all = new List<CharacterController> { bard, near };
            ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == BardicMusicRules.PerformCheckContext ? 19 : sides == 20 ? 5 : natural;
            BardicMusicResult started = BardicMusicRules.Start(bard, BardicAbility.Countersong, null, null, all);
            Assert(started.Performed, "Bard 1 begins a countersong", started.Refusal);
            int perform = bard.Stats.BardBardicMusic.LastPerformCheck;
            SaveContext sonic = SaveContext.ForSpellId(SpellNames.SOUND_BURST);

            gm.PCs.Add(bard); gm.PCs.Add(near);
            try
            {
                SaveRoll raised = SaveRollRules.Roll(near.Stats, SavingThrowType.Fortitude, perform, sonic, null);
                Assert(raised.CountersongUsed && raised.Total == perform && raised.Succeeded,
                    "The countersong's Perform result raises a save of d20 5 to " + perform + " against Sound Burst", $"total {raised.Total}");

                SaveRoll plain = SaveRollRules.Roll(near.Stats, SavingThrowType.Fortitude, perform, SaveContext.ForSpellId(SpellNames.HOLD_PERSON), null);
                Assert(!plain.CountersongUsed && plain.Total == 5 + plain.Modifier, "... but not against a spell that is neither sonic nor language-dependent");

                ScenarioHooks.RollFilter = ForceD20(1);
                SaveRoll one = SaveRollRules.Roll(near.Stats, SavingThrowType.Fortitude, perform, sonic, null);
                Assert(one.CountersongUsed && one.Succeeded && !one.DecidedByNatural,
                    "A natural 1 replaced by the countersong result is decided by that result (PHB p.29, delegated RAW reading)", $"total {one.Total}");

                // A natural 1 at +30: the save's total (31) is above the Perform result, but the save fails
                // automatically, so a Perform result that meets the DC is the higher result and is used.
                WithSaveBonus(near.Stats, 30);
                SaveRoll highOne = SaveRollRules.Roll(near.Stats, SavingThrowType.Fortitude, perform, sonic, null);
                Assert(highOne.CountersongUsed && highOne.Succeeded && highOne.Total == perform,
                    "On a natural 1 at +30, a Perform result that meets the DC replaces the automatic failure", $"total {highOne.Total} used {highOne.CountersongUsed}");
                SaveRoll shortOne = SaveRollRules.Roll(near.Stats, SavingThrowType.Fortitude, perform + 1, sonic, null);
                Assert(!shortOne.CountersongUsed && !shortOne.Succeeded,
                    "... and a Perform result below the DC is not used: the natural 1 fails", $"total {shortOne.Total} used {shortOne.CountersongUsed}");
                near.Stats.Bonuses.Set("test:save-roll", BonusTarget.AllSaves, BonusType.Untyped, 0);

                ScenarioHooks.RollFilter = ForceD20(20);
                SaveRoll twenty = SaveRollRules.Roll(near.Stats, SavingThrowType.Fortitude, 40, sonic, null);
                Assert(!twenty.CountersongUsed && twenty.Succeeded, "A natural 20 is never replaced and succeeds");
            }
            finally
            {
                gm.PCs.Remove(bard); gm.PCs.Remove(near);
            }
            BardicMusicRules.Stop(bard, all);
        }
        finally
        {
            Cleanup(bard, near);
        }
    }

    // ── Evasion on the Javelin of Lightning (PHB p.50, SPL-090) ───

    private static void TestJavelinEvasion()
    {
        CharacterController rogue = null, fighter = null;
        try
        {
            CharacterStats rogueStats = Build("SrEvader");
            rogueStats.RingGrantsEvasion = true;
            rogue = Create(rogueStats, new Vector2Int(-120, -40));
            fighter = Create(Build("SrPlain"), new Vector2Int(-120, -44));
            var item = new ItemData { Name = "Javelin of Lightning" };

            // The bolt's 5d6 all come up 6 (30); the Reflex save (DC 14) comes up 20.
            ScenarioHooks.RollFilter = (sides, ctx, natural) => sides == 20 ? 20 : sides == 6 ? 6 : natural;
            var evading = new JavelinOfLightningBehavior();
            evading.Initialize(item);
            var notes = new List<string>();
            int before = rogue.Stats.CurrentHP;
            evading.Activate(rogue, notes);
            Assert(rogue.Stats.CurrentHP == before, "Evasion: a creature with Evasion that saves takes no damage from the bolt",
                $"HP {before} -> {rogue.Stats.CurrentHP}; {string.Join(" | ", notes)}");
            Assert(notes.Count > 0 && notes[0].Contains("Evasion"), "The javelin's log names Evasion", string.Join(" | ", notes));

            var plain = new JavelinOfLightningBehavior();
            plain.Initialize(item);
            notes.Clear();
            before = fighter.Stats.CurrentHP;
            plain.Activate(fighter, notes);
            Assert(before - fighter.Stats.CurrentHP == 15, "Without Evasion a successful save halves the bolt (30 to 15)",
                $"HP {before} -> {fighter.Stats.CurrentHP}");
        }
        finally
        {
            Cleanup(rogue, fighter);
        }
    }
}
}
