using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Focused tests for all four Magic Circle against [Alignment] spells.
/// D&D 3.5e PHB compliance — standard action version only.
/// Run with MagicCircleRulesTests.RunAll().
/// </summary>
public static class MagicCircleRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== MAGIC CIRCLE RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        // Spell definition tests
        TestAllFourSpellDefinitionsExist();
        TestDurationScales10MinPerLevel();
        TestSpellSchoolAndDescriptors();
        TestClassAvailability();

        // Alignment protection rules integration
        TestAlignmentProtectionRulesRecognizeMagicCircle();
        TestIsMagicCircleSpellHelper();

        // Deflection AC bonus tests
        TestDeflectionBonusVsWardedAlignment();
        TestDeflectionBonusNotAppliedVsNonWardedAlignment();

        // Resistance save bonus tests
        TestResistanceSaveBonusVsWardedAlignment();

        // Area emanation tests
        TestAreaEmanation10FtRadius();
        TestEmanationMovesWithCenter();
        TestMultipleAlliesInArea();

        // Mental control immunity
        TestMentalControlBlocking();

        // Summoned creature barrier
        TestSummonedCreatureBarrier();

        // Non-stacking with Protection from Alignment
        TestNonStackingWithProtectionFromAlignment();

        // MagicCircleEffectData unit tests
        TestEffectDataAlignmentChecking();
        TestEffectDataDurationTick();

        Debug.Log($"====== Magic Circle Rules Results: {_passed} passed, {_failed} failed ======");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

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

    private static CharacterStats BuildStats(
        string name,
        string className,
        int level,
        Alignment alignment,
        int str = 10,
        int dex = 12,
        int con = 12,
        int wis = 14,
        int intelligence = 16,
        int cha = 10,
        int bab = 2)
    {
        var stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: str, dex: dex, con: con, wis: wis, intelligence: intelligence, cha: cha,
            bab: bab,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        stats.CharacterAlignment = alignment;
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats, Vector2Int? gridPos = null)
    {
        var go = new GameObject($"MCTest_{stats.CharacterName}");
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        controller.GridPosition = gridPos ?? Vector2Int.zero;

        var spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.Init(stats);

        var statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SPELL DEFINITION TESTS
    // ═══════════════════════════════════════════════════════════════════

    private static void TestAllFourSpellDefinitionsExist()
    {
        string[] ids = {
            SpellNames.MAGIC_CIRCLE_AGAINST_EVIL,
            SpellNames.MAGIC_CIRCLE_AGAINST_GOOD,
            SpellNames.MAGIC_CIRCLE_AGAINST_LAW,
            SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS
        };

        for (int i = 0; i < ids.Length; i++)
        {
            SpellData spell = SpellDatabase.GetSpell(ids[i]);
            Assert(spell != null, $"{ids[i]} definition exists");
            if (spell == null) continue;
            Assert(!spell.IsPlaceholder, $"{ids[i]} is not placeholder");
            Assert(spell.SpellLevel == 3, $"{ids[i]} is level 3", $"got level {spell.SpellLevel}");
            Assert(spell.RangeCategory == SpellRangeCategory.Touch, $"{ids[i]} range is Touch");
            Assert(spell.EffectType == SpellEffectType.Buff, $"{ids[i]} effect type is Buff");
            Assert(spell.BuffDeflectionBonus == 2, $"{ids[i]} grants +2 deflection", $"got {spell.BuffDeflectionBonus}");
            Assert(spell.BuffSaveBonus == 2, $"{ids[i]} grants +2 resistance saves", $"got {spell.BuffSaveBonus}");
        }
    }

    private static void TestDurationScales10MinPerLevel()
    {
        string[] ids = {
            SpellNames.MAGIC_CIRCLE_AGAINST_EVIL,
            SpellNames.MAGIC_CIRCLE_AGAINST_GOOD,
            SpellNames.MAGIC_CIRCLE_AGAINST_LAW,
            SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS
        };

        for (int i = 0; i < ids.Length; i++)
        {
            SpellData spell = SpellDatabase.GetSpell(ids[i]);
            if (spell == null) continue;

            Assert(spell.DurationType == DurationType.Minutes, $"{ids[i]} duration type is Minutes", $"got {spell.DurationType}");
            Assert(spell.DurationValue == 10, $"{ids[i]} duration value is 10 (10 min/level)", $"got {spell.DurationValue}");
            Assert(spell.DurationScalesWithLevel, $"{ids[i]} scales with level");

            // 10 min/level at CL 5 = 50 min = 500 rounds
            int roundsAt5 = ActiveSpellEffect.CalculateDurationRounds(spell, 5);
            Assert(roundsAt5 == 500, $"{ids[i]} = 500 rounds at CL 5 (10 min/level × 5)", $"got {roundsAt5}");

            // At CL 1 = 10 min = 100 rounds
            int roundsAt1 = ActiveSpellEffect.CalculateDurationRounds(spell, 1);
            Assert(roundsAt1 == 100, $"{ids[i]} = 100 rounds at CL 1 (10 min/level × 1)", $"got {roundsAt1}");
        }
    }

    private static void TestSpellSchoolAndDescriptors()
    {
        SpellData evil = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL);
        SpellData good = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_GOOD);
        SpellData law = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_LAW);
        SpellData chaos = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS);

        if (evil != null) Assert(evil.School.Contains("Abjuration") && evil.School.Contains("Good"),
            "MC vs Evil: Abjuration [Good]", $"got {evil.School}");
        if (good != null) Assert(good.School.Contains("Abjuration") && good.School.Contains("Evil"),
            "MC vs Good: Abjuration [Evil]", $"got {good.School}");
        if (law != null) Assert(law.School.Contains("Abjuration") && law.School.Contains("Chaotic"),
            "MC vs Law: Abjuration [Chaotic]", $"got {law.School}");
        if (chaos != null) Assert(chaos.School.Contains("Abjuration") && chaos.School.Contains("Lawful"),
            "MC vs Chaos: Abjuration [Lawful]", $"got {chaos.School}");
    }

    private static void TestClassAvailability()
    {
        SpellData evil = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL);
        SpellData good = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_GOOD);
        SpellData law = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_LAW);
        SpellData chaos = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS);

        // Evil and Chaos: available to Paladin
        if (evil != null)
        {
            Assert(evil.ClassList != null && System.Array.IndexOf(evil.ClassList, "Paladin") >= 0,
                "MC vs Evil available to Paladin");
            Assert(evil.ClassList != null && System.Array.IndexOf(evil.ClassList, "Cleric") >= 0,
                "MC vs Evil available to Cleric");
        }
        if (chaos != null)
        {
            Assert(chaos.ClassList != null && System.Array.IndexOf(chaos.ClassList, "Paladin") >= 0,
                "MC vs Chaos available to Paladin");
        }

        // Good and Law: NOT available to Paladin
        if (good != null)
        {
            Assert(good.ClassList == null || System.Array.IndexOf(good.ClassList, "Paladin") < 0,
                "MC vs Good NOT available to Paladin");
        }
        if (law != null)
        {
            Assert(law.ClassList == null || System.Array.IndexOf(law.ClassList, "Paladin") < 0,
                "MC vs Law NOT available to Paladin");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ALIGNMENT PROTECTION RULES INTEGRATION
    // ═══════════════════════════════════════════════════════════════════

    private static void TestAlignmentProtectionRulesRecognizeMagicCircle()
    {
        Assert(AlignmentProtectionRules.TryGetProtectionTypeForSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL, out var typeEvil) && typeEvil == AlignmentProtectionType.Evil,
            "AlignmentProtectionRules recognizes MC vs Evil as Evil protection");
        Assert(AlignmentProtectionRules.TryGetProtectionTypeForSpell(SpellNames.MAGIC_CIRCLE_AGAINST_GOOD, out var typeGood) && typeGood == AlignmentProtectionType.Good,
            "AlignmentProtectionRules recognizes MC vs Good as Good protection");
        Assert(AlignmentProtectionRules.TryGetProtectionTypeForSpell(SpellNames.MAGIC_CIRCLE_AGAINST_LAW, out var typeLaw) && typeLaw == AlignmentProtectionType.Law,
            "AlignmentProtectionRules recognizes MC vs Law as Law protection");
        Assert(AlignmentProtectionRules.TryGetProtectionTypeForSpell(SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS, out var typeChaos) && typeChaos == AlignmentProtectionType.Chaos,
            "AlignmentProtectionRules recognizes MC vs Chaos as Chaos protection");
    }

    private static void TestIsMagicCircleSpellHelper()
    {
        Assert(AlignmentProtectionRules.IsMagicCircleSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL),
            "IsMagicCircleSpell: MC vs Evil = true");
        Assert(AlignmentProtectionRules.IsMagicCircleSpell(SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS),
            "IsMagicCircleSpell: MC vs Chaos = true");
        Assert(!AlignmentProtectionRules.IsMagicCircleSpell(SpellNames.PROTECTION_FROM_EVIL),
            "IsMagicCircleSpell: Protection from Evil = false");
        Assert(!AlignmentProtectionRules.IsMagicCircleSpell(SpellNames.MAGE_ARMOR),
            "IsMagicCircleSpell: Mage Armor = false");

        Assert(AlignmentProtectionRules.IsProtectionFromAlignmentSpell(SpellNames.PROTECTION_FROM_EVIL),
            "IsProtectionFromAlignmentSpell: Protection from Evil = true");
        Assert(!AlignmentProtectionRules.IsProtectionFromAlignmentSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL),
            "IsProtectionFromAlignmentSpell: MC vs Evil = false");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DEFLECTION AC BONUS TESTS
    // ═══════════════════════════════════════════════════════════════════

    private static void TestDeflectionBonusVsWardedAlignment()
    {
        CharacterController target = null;
        CharacterController evilAttacker = null;

        try
        {
            target = CreateController(BuildStats("WardedTarget", "Wizard", 5, Alignment.TrueNeutral), new Vector2Int(5, 5));
            evilAttacker = CreateController(BuildStats("EvilAttacker", "Fighter", 5, Alignment.NeutralEvil, str: 16, bab: 5), new Vector2Int(5, 6));

            // Apply Magic Circle against Evil as a direct effect on target
            SpellData mcEvil = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL);
            target.GetComponent<StatusEffectManager>().AddEffect(mcEvil, "AllyCaster", 5);

            // Check benefits against evil attacker
            AlignmentProtectionBenefits vsEvil = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.NeutralEvil);
            Assert(vsEvil.HasMatch, "MC vs Evil: benefits match against evil source");
            Assert(vsEvil.DeflectionAcBonus == 2, "MC vs Evil: +2 deflection AC vs evil", $"got {vsEvil.DeflectionAcBonus}");

            // Verify attack shows the bonus
            CombatResult evilAttack = evilAttacker.Attack(target);
            Assert(evilAttack.ProtectionDeflectionBonusToAc == 2,
                "MC vs Evil: attack result shows +2 deflection AC", $"got {evilAttack.ProtectionDeflectionBonusToAc}");
        }
        finally
        {
            DestroyController(target);
            DestroyController(evilAttacker);
        }
    }

    private static void TestDeflectionBonusNotAppliedVsNonWardedAlignment()
    {
        CharacterController target = null;
        CharacterController neutralAttacker = null;
        CharacterController goodAttacker = null;

        try
        {
            target = CreateController(BuildStats("WardedTarget", "Wizard", 5, Alignment.TrueNeutral), new Vector2Int(5, 5));
            neutralAttacker = CreateController(BuildStats("NeutralAttacker", "Fighter", 5, Alignment.TrueNeutral, str: 16, bab: 5), new Vector2Int(5, 6));
            goodAttacker = CreateController(BuildStats("GoodAttacker", "Fighter", 5, Alignment.NeutralGood, str: 16, bab: 5), new Vector2Int(5, 4));

            SpellData mcEvil = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL);
            target.GetComponent<StatusEffectManager>().AddEffect(mcEvil, "AllyCaster", 5);

            // Neutral attacker: no bonus
            AlignmentProtectionBenefits vsNeutral = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.TrueNeutral);
            Assert(!vsNeutral.HasMatch, "MC vs Evil: no match against neutral source");
            Assert(vsNeutral.DeflectionAcBonus == 0, "MC vs Evil: no deflection AC vs neutral", $"got {vsNeutral.DeflectionAcBonus}");

            // Good attacker: no bonus (warding is vs Evil only)
            AlignmentProtectionBenefits vsGood = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.NeutralGood);
            Assert(!vsGood.HasMatch, "MC vs Evil: no match against good source");
        }
        finally
        {
            DestroyController(target);
            DestroyController(neutralAttacker);
            DestroyController(goodAttacker);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RESISTANCE SAVE BONUS TESTS
    // ═══════════════════════════════════════════════════════════════════

    private static void TestResistanceSaveBonusVsWardedAlignment()
    {
        CharacterController target = null;

        try
        {
            target = CreateController(BuildStats("WardedTarget", "Wizard", 5, Alignment.TrueNeutral));

            SpellData mcEvil = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL);
            target.GetComponent<StatusEffectManager>().AddEffect(mcEvil, "AllyCaster", 5);

            AlignmentProtectionBenefits vsEvil = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.NeutralEvil);
            Assert(vsEvil.ResistanceSaveBonus == 2, "MC vs Evil: +2 resistance saves vs evil", $"got {vsEvil.ResistanceSaveBonus}");

            AlignmentProtectionBenefits vsNeutral = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.TrueNeutral);
            Assert(vsNeutral.ResistanceSaveBonus == 0, "MC vs Evil: no save bonus vs neutral", $"got {vsNeutral.ResistanceSaveBonus}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AREA EMANATION TESTS
    // ═══════════════════════════════════════════════════════════════════

    private static void TestAreaEmanation10FtRadius()
    {
        var mcData = new MagicCircleEffectData
        {
            WardedAlignment = AlignmentProtectionType.Evil,
            CasterLevel = 5,
            RemainingRounds = 500,
            SourceSpellId = SpellNames.MAGIC_CIRCLE_AGAINST_EVIL
        };

        CharacterController center = null;
        CharacterController nearAlly = null;
        CharacterController farAlly = null;

        try
        {
            center = CreateController(BuildStats("Center", "Cleric", 5, Alignment.LawfulGood), new Vector2Int(5, 5));
            nearAlly = CreateController(BuildStats("NearAlly", "Fighter", 5, Alignment.NeutralGood), new Vector2Int(5, 7)); // 2 squares away
            farAlly = CreateController(BuildStats("FarAlly", "Rogue", 5, Alignment.TrueNeutral), new Vector2Int(5, 9)); // 4 squares away

            mcData.CenterCreature = center;

            Assert(mcData.IsCreatureInArea(center), "Center creature is in area");
            Assert(mcData.IsCreatureInArea(nearAlly), "Ally 2 squares away is in area (10 ft)");
            Assert(!mcData.IsCreatureInArea(farAlly), "Ally 4 squares away is NOT in area (>10 ft)");
        }
        finally
        {
            DestroyController(center);
            DestroyController(nearAlly);
            DestroyController(farAlly);
        }
    }

    private static void TestEmanationMovesWithCenter()
    {
        var mcData = new MagicCircleEffectData
        {
            WardedAlignment = AlignmentProtectionType.Evil,
            CasterLevel = 5,
            RemainingRounds = 500,
            SourceSpellId = SpellNames.MAGIC_CIRCLE_AGAINST_EVIL
        };

        CharacterController center = null;
        CharacterController ally = null;

        try
        {
            center = CreateController(BuildStats("Center", "Cleric", 5, Alignment.LawfulGood), new Vector2Int(5, 5));
            ally = CreateController(BuildStats("Ally", "Fighter", 5, Alignment.NeutralGood), new Vector2Int(5, 7));

            mcData.CenterCreature = center;

            Assert(mcData.IsCreatureInArea(ally), "Ally in area at start");

            // Move center away
            center.GridPosition = new Vector2Int(10, 10);
            Assert(!mcData.IsCreatureInArea(ally), "Ally NOT in area after center moves away");

            // Move center back near ally
            center.GridPosition = new Vector2Int(5, 6);
            Assert(mcData.IsCreatureInArea(ally), "Ally back in area after center moves near");
        }
        finally
        {
            DestroyController(center);
            DestroyController(ally);
        }
    }

    private static void TestMultipleAlliesInArea()
    {
        var mcData = new MagicCircleEffectData
        {
            WardedAlignment = AlignmentProtectionType.Evil,
            CasterLevel = 5,
            RemainingRounds = 500,
            SourceSpellId = SpellNames.MAGIC_CIRCLE_AGAINST_EVIL
        };

        CharacterController center = null;
        CharacterController ally1 = null;
        CharacterController ally2 = null;
        CharacterController ally3 = null;

        try
        {
            center = CreateController(BuildStats("Center", "Cleric", 5, Alignment.LawfulGood), new Vector2Int(5, 5));
            ally1 = CreateController(BuildStats("Ally1", "Fighter", 5, Alignment.NeutralGood), new Vector2Int(5, 6));
            ally2 = CreateController(BuildStats("Ally2", "Rogue", 5, Alignment.ChaoticGood), new Vector2Int(6, 5));
            ally3 = CreateController(BuildStats("Ally3", "Wizard", 5, Alignment.TrueNeutral), new Vector2Int(4, 4));

            mcData.CenterCreature = center;

            var allChars = new List<CharacterController> { center, ally1, ally2, ally3 };
            var inArea = mcData.GetCreaturesInArea(allChars);

            Assert(inArea.Count == 4, "All 4 allies within radius are in area", $"got {inArea.Count}");
        }
        finally
        {
            DestroyController(center);
            DestroyController(ally1);
            DestroyController(ally2);
            DestroyController(ally3);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MENTAL CONTROL BLOCKING
    // ═══════════════════════════════════════════════════════════════════

    private static void TestMentalControlBlocking()
    {
        CharacterController target = null;

        try
        {
            target = CreateController(BuildStats("WardedTarget", "Wizard", 5, Alignment.TrueNeutral));

            SpellData mcEvil = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL);
            target.GetComponent<StatusEffectManager>().AddEffect(mcEvil, "AllyCaster", 5);

            AlignmentProtectionBenefits vsEvil = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.NeutralEvil);
            Assert(vsEvil.BlocksMentalControl, "MC vs Evil: blocks mental control from evil");
            Assert(vsEvil.BlocksSummonedContact, "MC vs Evil: blocks summoned contact from evil");
        }
        finally
        {
            DestroyController(target);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SUMMONED CREATURE BARRIER
    // ═══════════════════════════════════════════════════════════════════

    private static void TestSummonedCreatureBarrier()
    {
        GameManager gm = null;
        CharacterController target = null;
        CharacterController summonAttacker = null;
        CharacterController summonCaster = null;

        try
        {
            var gmGo = new GameObject("MCTest_GameManager");
            gm = gmGo.AddComponent<GameManager>();

            target = CreateController(BuildStats("ProtectedDefender", "Wizard", 5, Alignment.TrueNeutral), new Vector2Int(5, 5));
            summonAttacker = CreateController(BuildStats("SummonedFiend", "Fighter", 5, Alignment.NeutralEvil, str: 16, bab: 5), new Vector2Int(5, 6));
            summonCaster = CreateController(BuildStats("Summoner", "Wizard", 5, Alignment.NeutralEvil), new Vector2Int(5, 8));

            // Apply MC against Evil directly on target
            SpellData mcEvil = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL);
            target.GetComponent<StatusEffectManager>().AddEffect(mcEvil, "AllyCaster", 5);

            // Register summoned creature using reflection (same pattern as ProtectionFromAlignmentTests)
            RegisterAsActiveSummon(gm, summonAttacker, summonCaster);

            CombatResult attack = summonAttacker.Attack(target);
            Assert(attack.ProtectionSummonedBarrierBlocked,
                "MC vs Evil: barrier blocks summoned evil creature melee");
            Assert(!attack.Hit, "MC vs Evil: summoned blocked attack is treated as miss");
        }
        finally
        {
            DestroyController(target);
            DestroyController(summonAttacker);
            DestroyController(summonCaster);
            if (gm != null)
                UnityEngine.Object.DestroyImmediate(gm.gameObject);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NON-STACKING WITH PROTECTION FROM ALIGNMENT
    // ═══════════════════════════════════════════════════════════════════

    private static void TestNonStackingWithProtectionFromAlignment()
    {
        CharacterController target = null;

        try
        {
            target = CreateController(BuildStats("DoubleWarded", "Wizard", 5, Alignment.TrueNeutral));
            var statusMgr = target.GetComponent<StatusEffectManager>();

            // Apply both Protection from Evil AND Magic Circle against Evil
            SpellData protEvil = SpellDatabase.GetSpell(SpellNames.PROTECTION_FROM_EVIL);
            SpellData mcEvil = SpellDatabase.GetSpell(SpellNames.MAGIC_CIRCLE_AGAINST_EVIL);

            statusMgr.AddEffect(protEvil, "AllyCaster", 5);
            statusMgr.AddEffect(mcEvil, "AllyCaster2", 5);

            // Benefits should NOT stack — both give +2, result should be +2 (not +4)
            AlignmentProtectionBenefits vsEvil = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.NeutralEvil);
            Assert(vsEvil.HasMatch, "Both spells active: match found");
            Assert(vsEvil.DeflectionAcBonus == 2,
                "Non-stacking: deflection bonus remains +2 (not +4)",
                $"got {vsEvil.DeflectionAcBonus}");
            Assert(vsEvil.ResistanceSaveBonus == 2,
                "Non-stacking: resistance bonus remains +2 (not +4)",
                $"got {vsEvil.ResistanceSaveBonus}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EFFECT DATA UNIT TESTS
    // ═══════════════════════════════════════════════════════════════════

    private static void TestEffectDataAlignmentChecking()
    {
        var mcData = new MagicCircleEffectData
        {
            WardedAlignment = AlignmentProtectionType.Evil,
            CasterLevel = 5,
            RemainingRounds = 500,
            SourceSpellId = SpellNames.MAGIC_CIRCLE_AGAINST_EVIL
        };

        // Evil alignments match
        Assert(mcData.IsAttackerOfWardedAlignment(Alignment.LawfulEvil), "LE matches Evil ward");
        Assert(mcData.IsAttackerOfWardedAlignment(Alignment.NeutralEvil), "NE matches Evil ward");
        Assert(mcData.IsAttackerOfWardedAlignment(Alignment.ChaoticEvil), "CE matches Evil ward");

        // Non-evil don't match
        Assert(!mcData.IsAttackerOfWardedAlignment(Alignment.LawfulGood), "LG does not match Evil ward");
        Assert(!mcData.IsAttackerOfWardedAlignment(Alignment.TrueNeutral), "TN does not match Evil ward");
        Assert(!mcData.IsAttackerOfWardedAlignment(Alignment.ChaoticGood), "CG does not match Evil ward");

        // Test other variants
        var mcGood = new MagicCircleEffectData { WardedAlignment = AlignmentProtectionType.Good };
        Assert(mcGood.IsAttackerOfWardedAlignment(Alignment.NeutralGood), "NG matches Good ward");
        Assert(!mcGood.IsAttackerOfWardedAlignment(Alignment.NeutralEvil), "NE does not match Good ward");

        var mcLaw = new MagicCircleEffectData { WardedAlignment = AlignmentProtectionType.Law };
        Assert(mcLaw.IsAttackerOfWardedAlignment(Alignment.LawfulNeutral), "LN matches Law ward");
        Assert(!mcLaw.IsAttackerOfWardedAlignment(Alignment.ChaoticNeutral), "CN does not match Law ward");

        var mcChaos = new MagicCircleEffectData { WardedAlignment = AlignmentProtectionType.Chaos };
        Assert(mcChaos.IsAttackerOfWardedAlignment(Alignment.ChaoticGood), "CG matches Chaos ward");
        Assert(!mcChaos.IsAttackerOfWardedAlignment(Alignment.LawfulGood), "LG does not match Chaos ward");
    }

    private static void TestEffectDataDurationTick()
    {
        var mcData = new MagicCircleEffectData
        {
            WardedAlignment = AlignmentProtectionType.Evil,
            CasterLevel = 5,
            RemainingRounds = 3,
            SourceSpellId = SpellNames.MAGIC_CIRCLE_AGAINST_EVIL
        };

        Assert(mcData.Tick(), "Tick 1: still active (2 remaining)");
        Assert(mcData.RemainingRounds == 2, "After tick 1: 2 rounds left", $"got {mcData.RemainingRounds}");
        Assert(mcData.Tick(), "Tick 2: still active (1 remaining)");
        Assert(!mcData.Tick(), "Tick 3: expired (0 remaining)");
        Assert(mcData.RemainingRounds == 0, "After tick 3: 0 rounds left", $"got {mcData.RemainingRounds}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  REFLECTION HELPER
    // ═══════════════════════════════════════════════════════════════════

    private static void RegisterAsActiveSummon(GameManager gameManager, CharacterController summon, CharacterController caster)
    {
        Type gmType = typeof(GameManager);
        Type summonType = gmType.GetNestedType("ActiveSummonInstance", BindingFlags.NonPublic);
        if (summonType == null) { Assert(false, "Reflection: found ActiveSummonInstance type"); return; }

        object summonEntry = Activator.CreateInstance(summonType);

        summonType.GetField("Controller", BindingFlags.Instance | BindingFlags.Public)?.SetValue(summonEntry, summon);
        summonType.GetField("Caster", BindingFlags.Instance | BindingFlags.Public)?.SetValue(summonEntry, caster);
        summonType.GetField("RemainingRounds", BindingFlags.Instance | BindingFlags.Public)?.SetValue(summonEntry, 5);
        summonType.GetField("TotalDurationRounds", BindingFlags.Instance | BindingFlags.Public)?.SetValue(summonEntry, 5);
        summonType.GetField("IsAlliedToPCs", BindingFlags.Instance | BindingFlags.Public)?.SetValue(summonEntry, true);

        FieldInfo activeListField = gmType.GetField("_activeSummons", BindingFlags.Instance | BindingFlags.NonPublic);
        object activeList = activeListField?.GetValue(gameManager);
        activeList?.GetType().GetMethod("Add")?.Invoke(activeList, new[] { summonEntry });

        bool registered = gameManager.IsSummonedCreature(summon);
        Assert(registered, "Reflection: summoned creature registered in GameManager");
    }
}
}
