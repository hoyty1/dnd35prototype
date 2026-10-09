using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// SPL-002 and SPL-003: one duration computation (<see cref="SpellDurationRules"/>) and spell flags that end with their spell.
/// Rules (checked 2026-10-09 against the PHB): p.176 (timed durations, per level where the spell says so; instantaneous,
/// permanent and concentration durations), p.138 (1 round = 6 seconds, so 1 minute = 10 rounds), p.91 (Extend Spell doubles
/// a timed duration). Each spell's duration line is cited in <see cref="PhbDurations"/>.
/// Silence keeps the code's long-standing 1 round/level (believed to be the SRD's; not verified); PHB p.279 prints 1 min./level;
/// owner question SPL-003.
/// Builds CharacterStats and bare controllers (no GameManager), so it runs in edit or Play mode.
/// </summary>
public static class SpellDurationRulesTests
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
        SpellDatabase.Init();

        Debug.Log("====== SPELL DURATION RULES TESTS (SPL-002, SPL-003) ======");

        TestRoundsPerUnit();
        TestLegacyNormalization();
        TestNoSpellNeedsNormalization();
        TestPhbDurations();
        TestExtendDoublesFixedSpells();
        TestTrackedEffectsLast();
        TestSilenceExpires();
        TestUntrackedFlagsExpire();
        TestDeathKnellStrengthReversedOnce();
        TestSilenceRecastKeepsFlag();
        TestAlignWeaponRecastKeepsFlag();
        TestDeathKnellRecastAddsNoStrength();
        TestDescribeWritesThePhbLine();
        TestDispelEndsFlags();
        TestShieldOtherLinkEnds();

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

    private static void AssertEq(int actual, int expected, string testName)
    {
        Assert(actual == expected, testName, $"expected {expected}, got {actual}");
    }

    private static SpellData Timed(DurationType type, int value, bool perLevel)
        => new SpellData { SpellId = "duration_probe", Name = "Duration Probe", DurationType = type, DurationValue = value, DurationScalesWithLevel = perLevel };

    // ── The computation ──

    private static void TestRoundsPerUnit()
    {
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Rounds, 3, false), 5), 3, "3 rounds stays 3 at CL 5");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Rounds, 1, true), 5), 5, "1 round/level at CL 5 = 5");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Minutes, 1, true), 5), 50, "1 min./level at CL 5 = 50 rounds");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Minutes, 10, false), 5), 100, "10 minutes = 100 rounds");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Hours, 1, true), 3), 1800, "1 hour/level at CL 3 = 1,800 rounds");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Hours, 24, false), 3), 14400, "24 hours = 14,400 rounds");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Days, 1, true), 2), 28800, "1 day/level at CL 2 = 28,800 rounds");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.MinutesPerLevel, 0, false), 4), 40, "MinutesPerLevel with no value = 1 min./level (was a fixed fallback)");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Minutes, 1, true), 0), 10, "caster level below 1 counts as 1");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Instantaneous, 0, false), 5), SpellDurationRules.InstantaneousRounds, "instantaneous = 0");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Permanent, 0, false), 5), SpellDurationRules.PermanentRounds, "permanent = -1");
        AssertEq(SpellDurationRules.Rounds(Timed(DurationType.Concentration, 1, true), 5), SpellDurationRules.ConcentrationRounds, "concentration = -2");
        AssertEq(ActiveSpellEffect.CalculateDurationRounds(Timed(DurationType.Minutes, 1, true), 7), 70, "ActiveSpellEffect.CalculateDurationRounds delegates to the rule");
        AssertEq(SpellCastingHelper.CalculateDuration(Timed(DurationType.Rounds, 1, true), 6), 6, "SpellCastingHelper.CalculateDuration delegates to the rule");
        Assert(SpellDurationRules.Describe(Timed(DurationType.Minutes, 1, true)) == "1 min./level", "Describe writes 1 min./level", SpellDurationRules.Describe(Timed(DurationType.Minutes, 1, true)));
        Assert(SpellDurationRules.DescribeRounds(50) == "5 min.", "DescribeRounds(50) = 5 min.", SpellDurationRules.DescribeRounds(50));
    }

    private static void TestLegacyNormalization()
    {
        var fixedRounds = new SpellData { SpellId = "legacy_rounds", BuffDurationRounds = 30 };
        Assert(SpellDurationRules.HasLegacyOnlyDuration(fixedRounds), "A spell with only BuffDurationRounds is legacy-only");
        Assert(SpellDurationRules.NormalizeLegacyDuration(fixedRounds), "Normalizing a legacy-only spell changes it");
        AssertEq(SpellDurationRules.Rounds(fixedRounds, 5), 30, "Legacy 30 becomes 30 rounds (not 0)");
        Assert(!SpellDurationRules.NormalizeLegacyDuration(fixedRounds), "A normalized spell is not changed again");

        var hoursPerLevel = new SpellData { SpellId = "legacy_hours", BuffDurationRounds = -1 };
        SpellDurationRules.NormalizeLegacyDuration(hoursPerLevel);
        AssertEq(SpellDurationRules.Rounds(hoursPerLevel, 2), 1200, "Legacy -1 becomes 1 hour/level");

        var modern = Timed(DurationType.Minutes, 1, true);
        modern.BuffDurationRounds = 30;
        Assert(!SpellDurationRules.NormalizeLegacyDuration(modern), "A spell with its own duration keeps it");
    }

    private static void TestNoSpellNeedsNormalization()
    {
        var ids = SpellDatabase.LegacyDurationSpellIds;
        Assert(ids.Count == 0, "Every registered spell sets its own duration (none relies on BuffDurationRounds)",
            ids.Count > 0 ? string.Join(", ", ids) : "");
    }

    // ── The data: each spell's PHB duration line ──

    private static readonly (string id, DurationType type, int value, bool perLevel, string phb)[] PhbDurations =
    {
        (SpellNames.AID, DurationType.Minutes, 1, true, "p.196 1 min./level"),
        (SpellNames.ALARM, DurationType.Hours, 2, true, "p.197 2 hours/level"),
        (SpellNames.ALIGN_WEAPON, DurationType.Minutes, 1, true, "p.197 1 min./level"),
        (SpellNames.ARCANE_LOCK, DurationType.Permanent, 0, false, "p.200 permanent"),
        (SpellNames.BANE, DurationType.Minutes, 1, true, "p.203 1 min./level"),
        (SpellNames.BLINDNESS_DEAFNESS, DurationType.Permanent, 0, false, "p.206 permanent"),
        (SpellNames.CAUSE_FEAR, DurationType.Rounds, 1, false, "p.208 1d4 rounds or 1 round (the handler rolls)"),
        (SpellNames.COMMAND, DurationType.Rounds, 1, false, "p.211 1 round"),
        (SpellNames.COMPREHEND_LANGUAGES, DurationType.Minutes, 10, true, "p.211 10 min./level"),
        (SpellNames.CONFUSION, DurationType.Rounds, 1, true, "p.212 1 round/level"),
        (SpellNames.CONSECRATE, DurationType.Hours, 2, true, "p.212 2 hours/level"),
        (SpellNames.DARKVISION, DurationType.Hours, 1, true, "p.216 1 hour/level"),
        (SpellNames.DEATHWATCH, DurationType.Minutes, 10, true, "p.217 10 min./level"),
        (SpellNames.DELAY_POISON, DurationType.Hours, 1, true, "p.217 1 hour/level"),
        (SpellNames.DESECRATE, DurationType.Hours, 2, true, "p.218 2 hours/level"),
        (SpellNames.DETECT_SECRET_DOORS, DurationType.Concentration, 1, true, "p.220 concentration, up to 1 min./level"),
        (SpellNames.DOOM, DurationType.Minutes, 1, true, "p.225 1 min./level"),
        (SpellNames.ENDURE_ELEMENTS, DurationType.Hours, 24, false, "p.226 24 hours"),
        (SpellNames.ENTHRALL, DurationType.Hours, 1, false, "p.227 1 hour or less"),
        (SpellNames.FEATHER_FALL, DurationType.Rounds, 1, true, "p.229 until landing or 1 round/level"),
        (SpellNames.FIND_TRAPS, DurationType.Minutes, 1, true, "p.229 1 min./level"),
        (SpellNames.FLARE, DurationType.Instantaneous, 0, false, "p.232 instantaneous (dazzled 1 minute)"),
        (SpellNames.FLESH_TO_STONE, DurationType.Instantaneous, 0, false, "p.232 instantaneous"),
        (SpellNames.FLOATING_DISK, DurationType.Hours, 1, true, "p.294 1 hour/level"),
        (SpellNames.GENTLE_REPOSE, DurationType.Days, 1, true, "p.235 1 day/level"),
        (SpellNames.GHOST_SOUND, DurationType.Rounds, 1, true, "p.235 1 round/level"),
        (SpellNames.GHOUL_TOUCH, DurationType.Rounds, 2, false, "p.235 1d6+2 rounds (the handler rolls the 1d6)"),
        (SpellNames.GUIDANCE, DurationType.Minutes, 1, false, "p.238 1 minute"),
        (SpellNames.GUST_OF_WIND, DurationType.Rounds, 1, false, "p.238 1 round"),
        (SpellNames.HOLD_PORTAL, DurationType.Minutes, 1, true, "p.241 1 min./level"),
        (SpellNames.LIGHT, DurationType.Minutes, 10, true, "p.247 10 min./level"),
        (SpellNames.LOCATE_OBJECT, DurationType.Minutes, 1, true, "p.249 1 min./level"),
        (SpellNames.LULLABY, DurationType.Concentration, 1, true, "p.249 concentration + 1 round/level"),
        (SpellNames.MAGE_HAND, DurationType.Concentration, 0, false, "p.249 concentration"),
        (SpellNames.MESSAGE, DurationType.Minutes, 10, true, "p.253 10 min./level"),
        (SpellNames.MOUNT, DurationType.Hours, 2, true, "p.256 2 hours/level"),
        (SpellNames.NYSTULS_MAGIC_AURA, DurationType.Days, 1, true, "p.257 1 day/level"),
        (SpellNames.OBSCURE_OBJECT, DurationType.Hours, 8, false, "p.258 8 hours"),
        (SpellNames.PRESTIDIGITATION, DurationType.Hours, 1, false, "p.264 1 hour"),
        (SpellNames.READ_MAGIC, DurationType.Minutes, 10, true, "p.268 10 min./level"),
        (SpellNames.REMOVE_FEAR, DurationType.Minutes, 10, false, "p.271 10 minutes"),
        (SpellNames.RESISTANCE, DurationType.Minutes, 1, false, "p.272 1 minute"),
        (SpellNames.ROPE_TRICK, DurationType.Hours, 1, true, "p.273 1 hour/level"),
        (SpellNames.SHIELD_OTHER, DurationType.Hours, 1, true, "p.278 1 hour/level"),
        (SpellNames.SILENCE, DurationType.Rounds, 1, true, "the code's 1 round/level (believed SRD, not verified); PHB p.279 prints 1 min./level; owner question SPL-003"),
        (SpellNames.SPIDER_CLIMB, DurationType.Minutes, 10, true, "p.283 10 min./level"),
        (SpellNames.SPIRITUAL_WEAPON, DurationType.Rounds, 1, true, "p.283 1 round/level"),
        (SpellNames.STATUS, DurationType.Hours, 1, true, "p.284 1 hour/level"),
        (SpellNames.SUMMON_MONSTER_1, DurationType.Rounds, 1, true, "p.285 1 round/level"),
        (SpellNames.SUMMON_MONSTER_2, DurationType.Rounds, 1, true, "p.286 as summon monster I"),
        (SpellNames.UNSEEN_SERVANT, DurationType.Hours, 1, true, "p.297 1 hour/level"),
        (SpellNames.VENTRILOQUISM, DurationType.Minutes, 1, true, "p.298 1 min./level"),
        (SpellNames.VIRTUE, DurationType.Minutes, 1, false, "p.298 1 minute"),
        (SpellNames.ZONE_OF_TRUTH, DurationType.Minutes, 1, true, "p.303 1 min./level"),
        (SpellNames.INVISIBILITY_PURGE, DurationType.Minutes, 1, true, "p.245 1 min./level"),
        (SpellNames.DEATH_WARD, DurationType.Minutes, 1, true, "p.217 1 min./level"),
        (SpellNames.DIVINE_POWER, DurationType.Rounds, 1, true, "p.224 1 round/level"),
        (SpellNames.FREEDOM_OF_MOVEMENT, DurationType.Minutes, 10, true, "p.233 10 min./level"),
        (SpellNames.GIANT_VERMIN, DurationType.Minutes, 1, true, "p.235 1 min./level"),
        (SpellNames.NEUTRALIZE_POISON, DurationType.Minutes, 10, true, "p.257 10 min./level"),
        (SpellNames.REPEL_VERMIN, DurationType.Minutes, 10, true, "p.271 10 min./level"),
        (SpellNames.SPELL_IMMUNITY, DurationType.Minutes, 10, true, "p.282 10 min./level"),
    };

    private static void TestPhbDurations()
    {
        foreach (var row in PhbDurations)
        {
            SpellData spell = SpellDatabase.GetSpell(row.id);
            if (spell == null)
            {
                Assert(false, $"{row.id} is registered");
                continue;
            }

            bool same = spell.DurationType == row.type && spell.DurationValue == row.value && spell.DurationScalesWithLevel == row.perLevel;
            Assert(same, $"{spell.Name}: {SpellDurationRules.Describe(spell)} (PHB {row.phb})",
                $"got {spell.DurationType} {spell.DurationValue} perLevel={spell.DurationScalesWithLevel}");
        }

        // Rounds at caster level 5 for a few, against the PHB arithmetic.
        AssertEq(SpellDurationRules.Rounds(SpellDatabase.GetSpell(SpellNames.AID), 5), 50, "Aid at CL 5 lasts 50 rounds (was 0)");
        AssertEq(SpellDurationRules.Rounds(SpellDatabase.GetSpell(SpellNames.SHIELD_OTHER), 5), 3000, "Shield Other at CL 5 lasts 5 hours");
        AssertEq(SpellDurationRules.Rounds(SpellDatabase.GetSpell(SpellNames.CONFUSION), 7), 7, "Confusion at CL 7 lasts 7 rounds (the legacy field fixed 4)");
        AssertEq(SpellDurationRules.Rounds(SpellDatabase.GetSpell(SpellNames.INVISIBILITY_PURGE), 7), 70, "Invisibility Purge at CL 7 lasts 70 rounds (the fallback fixed 50)");
        AssertEq(SpellDurationRules.Rounds(SpellDatabase.GetSpell(SpellNames.DEATH_WARD), 7), 70, "Death Ward at CL 7 lasts 70 rounds (the data said 10)");
        AssertEq(SpellDurationRules.Rounds(SpellDatabase.GetSpell(SpellNames.REMOVE_FEAR), 7), 100, "Remove Fear lasts 10 minutes at any CL");
    }

    private static void TestExtendDoublesFixedSpells()
    {
        var metamagic = new MetamagicData();
        metamagic.AppliedMetamagic.Add(MetamagicFeatId.ExtendSpell);

        SpellData aid = SpellDatabase.GetSpell(SpellNames.AID).Clone();
        SpellCaster.ApplyMetamagicToSpellData(aid, metamagic);
        AssertEq(SpellDurationRules.Rounds(aid, 5), 100, "Extended Aid at CL 5 lasts 100 rounds (PHB p.91)");

        SpellData blind = SpellDatabase.GetSpell(SpellNames.BLINDNESS_DEAFNESS).Clone();
        SpellCaster.ApplyMetamagicToSpellData(blind, metamagic);
        AssertEq(SpellDurationRules.Rounds(blind, 5), SpellDurationRules.PermanentRounds, "Extend leaves a permanent spell permanent");
        Assert(SpellDatabase.GetSpell(SpellNames.AID).DurationValue == 1, "Extending a clone leaves the template unchanged");
    }

    // ── Effects through the StatusEffectManager ──

    private static CharacterStats BuildStats(string name, string cls, int level, int str = 12)
    {
        return new CharacterStats(
            name: name, level: level, characterClass: cls,
            str: str, dex: 10, con: 12, wis: 16, intelligence: 10, cha: 10,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 8 * level, raceName: "Human");
    }

    private static CharacterController CreateController(CharacterStats stats)
    {
        var go = new GameObject("DurationTest_" + stats.CharacterName);
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        var statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);
        return controller;
    }

    private static void Destroy(params CharacterController[] controllers)
    {
        foreach (CharacterController c in controllers)
            if (c != null)
                Object.DestroyImmediate(c.gameObject);
    }

    /// <summary>One round as the game ticks it: the tracked effects, then the level-2 cleric spell flags.</summary>
    private static void TickRound(CharacterController c)
    {
        c.StatusEffectManager.TickAllEffects();
        EffectService.TickClericSpell2Durations(c);
    }

    private static void TestTrackedEffectsLast()
    {
        CharacterController target = CreateController(BuildStats("Duration Target", "Fighter", 3));
        CharacterController doomed = CreateController(BuildStats("Doomed", "Fighter", 3));
        try
        {
            StatusEffectManager mgr = target.StatusEffectManager;
            ActiveSpellEffect aid = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.AID).Clone(), "Cleric", 5);
            ActiveSpellEffect resistance = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.RESISTANCE).Clone(), "Adept", 1);
            ActiveSpellEffect doom = doomed.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.DOOM).Clone(), "Acolyte", 3);
            AssertEq(aid != null ? aid.RemainingRounds : -99, 50, "Aid from a CL 5 caster starts at 50 rounds");
            AssertEq(resistance != null ? resistance.RemainingRounds : -99, 10, "Resistance starts at 10 rounds");
            AssertEq(doom != null ? doom.RemainingRounds : -99, 30, "Doom from a CL 3 caster starts at 30 rounds");

            TickRound(target);
            TickRound(doomed);
            Assert(mgr.HasEffect(SpellNames.AID) && mgr.HasEffect(SpellNames.RESISTANCE) && doomed.StatusEffectManager.HasEffect(SpellNames.DOOM),
                "Aid, Resistance and Doom survive the first round tick (they expired on it before SPL-002)");
            AssertEq(mgr.GetRemainingRounds(SpellNames.RESISTANCE), 9, "Resistance has 9 rounds left after one tick");

            for (int i = 0; i < 9; i++)
                TickRound(target);
            Assert(!mgr.HasEffect(SpellNames.RESISTANCE), "Resistance ends after 10 rounds");
            Assert(mgr.HasEffect(SpellNames.AID), "Aid is still active after 10 rounds");
        }
        finally
        {
            Destroy(target, doomed);
        }
    }

    /// <summary>What the Silence handler does on a failed save: <see cref="EffectService.ApplySilence"/> with the spell's duration.</summary>
    private static int ApplySilence(CharacterController target, int casterLevel)
    {
        SpellData silence = SpellDatabase.GetSpell(SpellNames.SILENCE).Clone();
        int rounds = SpellCastingHelper.CalculateDuration(silence, casterLevel);
        return EffectService.ApplySilence(target, silence, "Cleric", casterLevel, rounds);
    }

    /// <summary>What the Align Weapon handler does: <see cref="EffectService.ApplyAlignWeapon"/> with the spell's duration.</summary>
    private static int ApplyAlignWeapon(CharacterController target, int casterLevel, string alignment)
    {
        SpellData align = SpellDatabase.GetSpell(SpellNames.ALIGN_WEAPON).Clone();
        int rounds = SpellCastingHelper.CalculateDuration(align, casterLevel);
        return EffectService.ApplyAlignWeapon(target, align, "Cleric", casterLevel, rounds, alignment);
    }

    private static void TestSilenceExpires()
    {
        CharacterController target = CreateController(BuildStats("Silenced", "Wizard", 3));
        try
        {
            ApplySilence(target, 3);
            Assert(target.Stats.SilenceActive, "Silence sets the flag");
            TickRound(target);
            TickRound(target);
            Assert(target.Stats.SilenceActive, "Silence from a CL 3 caster is still active after 2 rounds");
            AssertEq(target.Stats.SilenceRoundsRemaining, 1, "The flag's counter follows the tracked effect");
            TickRound(target);
            Assert(!target.Stats.SilenceActive, "Silence ends after 3 rounds (SPL-003: it never ended)");
            Assert(!target.StatusEffectManager.HasEffect(SpellNames.SILENCE), "The tracked Silence effect is gone");
        }
        finally
        {
            Destroy(target);
        }
    }

    private static void TestUntrackedFlagsExpire()
    {
        CharacterController target = CreateController(BuildStats("Untracked", "Fighter", 3));
        try
        {
            target.Stats.SilenceActive = true;
            target.Stats.SilenceRoundsRemaining = 2;
            target.Stats.AlignWeaponActive = true;
            target.Stats.AlignWeaponAlignment = "good";
            target.Stats.AlignWeaponRoundsRemaining = 1;

            TickRound(target);
            Assert(target.Stats.SilenceActive && !target.Stats.AlignWeaponActive, "A flag with no tracked effect counts down on its own (Align Weapon ends after 1 round)");
            Assert(target.Stats.AlignWeaponAlignment == null, "Align Weapon's alignment is cleared");
            TickRound(target);
            Assert(!target.Stats.SilenceActive, "Silence with no tracked effect ends after 2 rounds");
        }
        finally
        {
            Destroy(target);
        }
    }

    private static void TestDeathKnellStrengthReversedOnce()
    {
        CharacterController caster = CreateController(BuildStats("Knell Caster", "Cleric", 3, str: 10));
        try
        {
            // The Death Knell handler's state for a slain 1 HD creature, shortened to 2 rounds.
            caster.Stats.STR += 2;
            caster.Stats.DeathKnellActive = true;
            caster.Stats.DeathKnellStrBonus = 2;
            caster.Stats.DeathKnellCLBonus = 1;
            caster.Stats.DeathKnellRoundsRemaining = 2;
            ActiveSpellEffect effect = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.DEATH_KNELL).Clone(), "Knell Caster", 3);
            Assert(effect != null, "Death Knell adds a tracked effect");
            if (effect != null)
            {
                effect.RemainingRounds = 2;
                effect.AppliedStatName = "STR";
                effect.AppliedStatBonus = 2;
            }

            TickRound(caster);
            AssertEq(caster.Stats.STR, 12, "STR 12 while Death Knell lasts");
            TickRound(caster);
            Assert(!caster.Stats.DeathKnellActive, "Death Knell's flag ends with the spell (SPL-003)");
            AssertEq(caster.Stats.STR, 10, "STR back to 10, taken back exactly once");
            AssertEq(caster.Stats.DeathKnellCLBonus, 0, "The +1 caster level ends too");

            // A flag left with no tracked effect is cleared at the rest after combat, and its STR taken back.
            caster.Stats.STR += 2;
            caster.Stats.DeathKnellActive = true;
            caster.Stats.DeathKnellStrBonus = 2;
            caster.Stats.DeathKnellRoundsRemaining = 50;
            EffectService.ClearAllClericSpell2Flags(caster.Stats);
            Assert(!caster.Stats.DeathKnellActive, "The rest after combat clears an untracked Death Knell flag");
            AssertEq(caster.Stats.STR, 10, "and takes its STR back");
        }
        finally
        {
            Destroy(caster);
        }
    }

    private static void TestSilenceRecastKeepsFlag()
    {
        CharacterController target = CreateController(BuildStats("Resilenced", "Wizard", 3));
        try
        {
            ApplySilence(target, 3);
            TickRound(target);
            AssertEq(target.StatusEffectManager.GetRemainingRounds(SpellNames.SILENCE), 2, "Silence has 2 rounds left after a tick");

            // The recast lasts longer than what is left, so it replaces the old effect; that removal must not end the new flag.
            AssertEq(ApplySilence(target, 3), 3, "A recast Silence lasts its full 3 rounds");
            Assert(target.Stats.SilenceActive && target.StatusEffectManager.HasEffect(SpellNames.SILENCE),
                "After a recast the flag and the tracked effect are both on (SPL-003 recast)");
            AssertEq(target.Stats.SilenceRoundsRemaining, 3, "The flag's counter has the recast's 3 rounds");

            // A recast that is not longer keeps the active spell.
            AssertEq(ApplySilence(target, 1), 3, "A shorter recast (CL 1) keeps the 3 rounds left");
            Assert(target.Stats.SilenceActive, "and the flag stays on");

            TickRound(target);
            TickRound(target);
            Assert(target.Stats.SilenceActive, "The recast Silence is on 2 rounds later");
            TickRound(target);
            Assert(!target.Stats.SilenceActive && !target.StatusEffectManager.HasEffect(SpellNames.SILENCE), "The recast Silence ends after its 3 rounds");
        }
        finally
        {
            Destroy(target);
        }
    }

    private static void TestAlignWeaponRecastKeepsFlag()
    {
        CharacterController target = CreateController(BuildStats("Realigned", "Fighter", 3));
        try
        {
            AssertEq(ApplyAlignWeapon(target, 3, "good"), 30, "Align Weapon from a CL 3 caster lasts 30 rounds");
            TickRound(target);
            TickRound(target);
            AssertEq(ApplyAlignWeapon(target, 3, "good"), 30, "A recast 2 rounds later lasts its full 30 rounds");
            Assert(target.Stats.AlignWeaponActive && target.Stats.AlignWeaponAlignment == "good" && target.StatusEffectManager.HasEffect(SpellNames.ALIGN_WEAPON),
                "After a recast the weapon is still good-aligned, flag and tracked effect (SPL-003 recast)");

            AssertEq(ApplyAlignWeapon(target, 1, "lawful"), 30, "A shorter recast (CL 1) keeps the 30 rounds left");
            Assert(target.Stats.AlignWeaponAlignment == "good", "and keeps the active spell's alignment");
        }
        finally
        {
            Destroy(target);
        }
    }

    private static void TestDeathKnellRecastAddsNoStrength()
    {
        CharacterController caster = CreateController(BuildStats("Double Knell", "Cleric", 3, str: 10));
        try
        {
            SpellData knell = SpellDatabase.GetSpell(SpellNames.DEATH_KNELL).Clone();
            Assert(EffectService.ApplyDeathKnellBonus(caster, knell, 3, 3, 4), "The first Death Knell applies its bonus");
            AssertEq(caster.Stats.STR, 12, "STR 12 after one Death Knell");
            TickRound(caster);

            Assert(!EffectService.ApplyDeathKnellBonus(caster, knell, 3, 5, 6), "A second Death Knell while the first lasts only refreshes it");
            AssertEq(caster.Stats.STR, 12, "STR still 12: the same spell's enhancement bonus does not stack (PHB p.217)");
            AssertEq(caster.StatusEffectManager.GetRemainingRounds(SpellNames.DEATH_KNELL), 5, "The refresh keeps the longer duration (5 rounds)");
            AssertEq(caster.Stats.TempHP, 6, "and the higher temporary hit points");

            for (int i = 0; i < 5; i++)
                TickRound(caster);
            Assert(!caster.Stats.DeathKnellActive && !caster.StatusEffectManager.HasEffect(SpellNames.DEATH_KNELL), "Death Knell ends after the refreshed duration");
            AssertEq(caster.Stats.STR, 10, "STR back to 10 after a double cast (it kept +2 for good before)");
        }
        finally
        {
            Destroy(caster);
        }
    }

    private static void TestDescribeWritesThePhbLine()
    {
        (string id, string text)[] rows =
        {
            (SpellNames.GHOUL_TOUCH, "1d6+2 rounds"),
            (SpellNames.CAUSE_FEAR, "1d4 rounds or 1 round"),
            (SpellNames.HYPNOTISM, "2d4 rounds (D)"),
            (SpellNames.LULLABY, "Concentration + 1 round/level (D)"),
            (SpellNames.DETECT_SECRET_DOORS, "Concentration, up to 1 min./level (D)"),
            (SpellNames.AID, "1 min./level"),
            (SpellNames.GENTLE_REPOSE, "1 day/level"),
        };
        foreach (var row in rows)
        {
            SpellData spell = SpellDatabase.GetSpell(row.id);
            string got = SpellDurationRules.Describe(spell);
            Assert(got == row.text, (spell != null ? spell.Name : row.id) + " reads '" + row.text + "'", got);
        }

        string summary = SpellDatabase.GetSpell(SpellNames.GENTLE_REPOSE).GetShortDescription();
        Assert(summary.Contains("Dur: 1 day/level"), "The spell summary uses the same text (Gentle Repose: 1 day/level)", summary);
    }

    private static void TestDispelEndsFlags()
    {
        CharacterController target = CreateController(BuildStats("Dispelled", "Fighter", 3));
        try
        {
            target.Stats.AlignWeaponActive = true;
            target.Stats.AlignWeaponAlignment = "good";
            target.Stats.AlignWeaponRoundsRemaining = 30;
            ActiveSpellEffect effect = target.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.ALIGN_WEAPON).Clone(), "Cleric", 3);
            AssertEq(effect != null ? effect.RemainingRounds : -99, 30, "Align Weapon from a CL 3 caster lasts 30 rounds");
            ApplySilence(target, 3);

            target.StatusEffectManager.RemoveAllEffects();
            Assert(!target.Stats.AlignWeaponActive && !target.Stats.SilenceActive, "Removing the tracked effects (dispel, rest) ends Align Weapon and Silence");
        }
        finally
        {
            Destroy(target);
        }
    }

    private static void TestShieldOtherLinkEnds()
    {
        CharacterController protector = CreateController(BuildStats("Protector", "Cleric", 3));
        CharacterController ward = CreateController(BuildStats("Ward", "Fighter", 3));
        try
        {
            ward.Stats.ShieldOtherProtectedActive = true;
            ward.Stats.ShieldOtherProtector = protector;
            protector.Stats.ShieldOtherProtectorActive = true;
            protector.Stats.ShieldOtherProtected = ward;
            ActiveSpellEffect effect = ward.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.SHIELD_OTHER).Clone(), "Protector", 3);
            AssertEq(effect != null ? effect.RemainingRounds : -99, 1800, "Shield Other from a CL 3 caster lasts 3 hours");

            TickRound(ward);
            Assert(ward.Stats.ShieldOtherProtectedActive, "The link holds while the spell lasts");

            ward.StatusEffectManager.RemoveEffectsBySpellId(SpellNames.SHIELD_OTHER);
            Assert(!ward.Stats.ShieldOtherProtectedActive && ward.Stats.ShieldOtherProtector == null, "The ward's side of the link ends with the spell");
            Assert(!protector.Stats.ShieldOtherProtectorActive && protector.Stats.ShieldOtherProtected == null, "The protector's side ends too");
        }
        finally
        {
            Destroy(protector, ward);
        }
    }
}
}
