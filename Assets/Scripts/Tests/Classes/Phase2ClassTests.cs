using System.Collections.Generic;
using UnityEngine;

namespace Tests.Classes
{
/// <summary>
/// Phase 2 class implementation tests:
/// 1. Ranger class — favored enemy, combat styles, animal companion, partial spellcasting
/// 2. Paladin class — smite evil, lay on hands, divine grace, turn undead, partial spellcasting
/// 3. Shared partial caster framework (Ranger/Paladin spell progression)
///
/// D&D 3.5e PHB accuracy verified against tables on p.46-48 (Ranger), p.42-45 (Paladin).
/// </summary>
public static class Phase2ClassTests
{
    private static int _passed;
    private static int _failed;

    public static void phase2_class_tests() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== PHASE 2 CLASS TESTS ======");
        RaceDatabase.Init();
        ClassRegistry.Init();
        SpellDatabase.Init();

        // Ranger Class Tests
        TestRangerClassRegistered();
        TestRangerClassProperties();
        TestRangerQuickStart();

        // Favored Enemy Tests
        TestFavoredEnemyAdd();
        TestFavoredEnemyIncrease();
        TestFavoredEnemyBonusVs();
        TestFavoredEnemyLevelProgression();
        TestFavoredEnemySubtypeMatching();

        // Combat Style Tests
        TestCombatStyleArchery();
        TestCombatStyleTWF();
        TestCombatStyleFeatProgression();

        // Animal Companion Tests
        TestAnimalCompanionTemplates();
        TestAnimalCompanionProgression();
        TestAnimalCompanionSpecialAbilities();
        TestRangerEffectiveDruidLevel();

        // Ranger Feature Level Tests
        TestRangerEvasion();
        TestRangerWoodlandStride();
        TestRangerCamouflage();
        TestRangerHideInPlainSight();

        // Paladin Class Tests
        TestPaladinClassRegistered();
        TestPaladinClassProperties();
        TestPaladinQuickStart();

        // Smite Evil Tests
        TestSmiteEvilUsesPerDay();
        TestSmiteEvilAttackBonus();
        TestSmiteEvilDamageBonus();
        TestSmiteEvilExpend();

        // Lay on Hands Tests
        TestLayOnHandsPool();
        TestLayOnHandsHeal();
        TestLayOnHandsHarmUndead();

        // Paladin Feature Level Tests
        TestPaladinDivineGrace();
        TestPaladinAuraOfCourage();
        TestPaladinDivineHealth();
        TestPaladinRemoveDisease();
        TestPaladinTurnUndead();

        // Partial Caster Framework Tests
        TestPartialCasterInit();
        TestPartialCasterSpellProgression();
        TestPartialCasterBonusSlots();
        TestPartialCasterCanCastAndSpend();
        TestPartialCasterPrepareSpells();
        TestPartialCasterClassDetection();

        Debug.Log($"====== Phase 2 Class Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, string className, int level,
        int str = 14, int dex = 12, int con = 14, int wis = 12, int intel = 10, int cha = 10)
    {
        ICharacterClass classDef = ClassRegistry.GetClass(className);
        int bab = classDef != null ? (level * classDef.BABAtLevel3 / 3) : 0;
        var stats = new CharacterStats(
            name: name, level: level, characterClass: className,
            str: str, dex: dex, con: con, wis: wis, intelligence: intel, cha: cha,
            bab: bab, armorBonus: classDef != null ? classDef.DefaultArmorBonus : 0,
            shieldBonus: classDef != null ? classDef.DefaultShieldBonus : 0,
            damageDice: classDef != null ? classDef.DefaultDamageDice : 8,
            damageCount: 1, bonusDamage: 0, baseSpeed: 6, atkRange: 1,
            baseHitDieHP: classDef != null ? classDef.HitDie : 8, raceName: "Human");
        return stats;
    }

    // ==================== RANGER CLASS TESTS ====================

    private static void TestRangerClassRegistered()
    {
        var rangerClass = ClassRegistry.GetClass("Ranger");
        Assert(rangerClass != null, "Ranger class registered in ClassRegistry");
        Assert(rangerClass != null && rangerClass.ClassName == "Ranger", "Ranger class name is 'Ranger'");
    }

    private static void TestRangerClassProperties()
    {
        var rc = ClassRegistry.GetClass("Ranger");
        if (rc == null) { Assert(false, "RangerClass lookup"); return; }

        Assert(rc.HitDie == 8, "Ranger d8 hit die");
        Assert(rc.BABAtLevel3 == 3, "Ranger full BAB (3 at level 3)");
        Assert(rc.GoodFortitude == true, "Ranger good Fort save");
        Assert(rc.GoodReflex == true, "Ranger good Ref save");
        Assert(rc.GoodWill == false, "Ranger poor Will save");
        Assert(rc.SkillPointsPerLevel == 6, "Ranger 6 skill points/level");
        Assert(rc.IsSpellcaster == true, "Ranger is spellcaster (partial)");
        Assert(rc.ClassSkills.Contains("Survival"), "Ranger has Survival as class skill");
        Assert(rc.ClassSkills.Contains("Hide"), "Ranger has Hide as class skill");
    }

    private static void TestRangerQuickStart()
    {
        var data = RangerClass.GetQuickStartCharacter();
        Assert(data != null, "Ranger QuickStart character created");
        Assert(data.ClassName == "Ranger", "Ranger QuickStart is Ranger class");
        Assert(data.CharacterName == "Kael", "Ranger QuickStart name is Kael");
        Assert(data.DEX >= 14, "Ranger QuickStart DEX >= 14 (primary for archery)");
        Assert(data.WIS >= 12, "Ranger QuickStart WIS >= 12 (for future spells)");
    }

    // ==================== FAVORED ENEMY TESTS ====================

    private static void TestFavoredEnemyAdd()
    {
        var fed = new FavoredEnemyData();
        fed.AddOrIncreaseFavoredEnemy("Undead");
        Assert(fed.GetBonusVs("Undead") == 2, "Favored Enemy: Initial bonus vs Undead is +2");
        Assert(fed.Enemies.Count == 1, "Favored Enemy: One enemy added");
    }

    private static void TestFavoredEnemyIncrease()
    {
        var fed = new FavoredEnemyData();
        fed.AddOrIncreaseFavoredEnemy("Undead");
        fed.AddOrIncreaseFavoredEnemy("Undead"); // Increase existing
        Assert(fed.GetBonusVs("Undead") == 4, "Favored Enemy: Increased bonus vs Undead is +4");
        Assert(fed.Enemies.Count == 1, "Favored Enemy: Still one enemy after increase");
    }

    private static void TestFavoredEnemyBonusVs()
    {
        var fed = new FavoredEnemyData();
        fed.AddOrIncreaseFavoredEnemy("Undead");
        fed.AddOrIncreaseFavoredEnemy("Aberration");
        Assert(fed.GetBonusVs("Undead") == 2, "Favored Enemy: +2 vs Undead");
        Assert(fed.GetBonusVs("Aberration") == 2, "Favored Enemy: +2 vs Aberration");
        Assert(fed.GetBonusVs("Dragon") == 0, "Favored Enemy: +0 vs non-favored Dragon");
        Assert(fed.GetDamageBonusVs("Undead") == 2, "Favored Enemy: +2 damage vs Undead");
        Assert(fed.GetSkillBonusVs("Undead") == 2, "Favored Enemy: +2 skill bonus vs Undead");
    }

    private static void TestFavoredEnemyLevelProgression()
    {
        // Ranger gains favored enemies at L1, 5, 10, 15, 20
        Assert(FavoredEnemyData.IsFavoredEnemyLevel(1), "Favored enemy at level 1");
        Assert(!FavoredEnemyData.IsFavoredEnemyLevel(3), "No favored enemy at level 3");
        Assert(FavoredEnemyData.IsFavoredEnemyLevel(5), "Favored enemy at level 5");
        Assert(FavoredEnemyData.IsFavoredEnemyLevel(10), "Favored enemy at level 10");
        Assert(FavoredEnemyData.IsFavoredEnemyLevel(15), "Favored enemy at level 15");
        Assert(FavoredEnemyData.IsFavoredEnemyLevel(20), "Favored enemy at level 20");

        Assert(FavoredEnemyData.GetTotalSelectionsAtLevel(1) == 1, "1 selection at level 1");
        Assert(FavoredEnemyData.GetTotalSelectionsAtLevel(5) == 2, "2 selections at level 5");
        Assert(FavoredEnemyData.GetTotalSelectionsAtLevel(10) == 3, "3 selections at level 10");
        Assert(FavoredEnemyData.GetTotalSelectionsAtLevel(20) == 5, "5 selections at level 20");
    }

    private static void TestFavoredEnemySubtypeMatching()
    {
        var fed = new FavoredEnemyData();
        fed.AddOrIncreaseFavoredEnemy("Humanoid (Orc)");
        Assert(fed.GetBonusVs("Humanoid (Orc)") == 2, "Favored Enemy: Exact Humanoid subtype match");
        Assert(fed.GetBonusVs("Humanoid (Elf)") == 0, "Favored Enemy: Different Humanoid subtype = 0");
    }

    // ==================== COMBAT STYLE TESTS ====================

    private static void TestCombatStyleArchery()
    {
        var csd = new CombatStyleData();
        csd.SelectStyle(RangerCombatStyle.Archery);
        Assert(csd.Style == RangerCombatStyle.Archery, "Combat Style: Archery selected");
        csd.GrantStyleFeat(2);
        Assert(csd.GrantedFeats.Contains("Rapid Shot"), "Combat Style L2: Rapid Shot granted");
    }

    private static void TestCombatStyleTWF()
    {
        var csd = new CombatStyleData();
        csd.SelectStyle(RangerCombatStyle.TwoWeaponFighting);
        csd.GrantStyleFeat(2);
        Assert(csd.GrantedFeats.Contains("Two-Weapon Fighting"), "Combat Style TWF L2: Two-Weapon Fighting granted");
    }

    private static void TestCombatStyleFeatProgression()
    {
        var csd = new CombatStyleData();
        csd.SelectStyle(RangerCombatStyle.Archery);

        // Test all three archery levels
        csd.GrantStyleFeat(2);
        csd.GrantStyleFeat(6);
        csd.GrantStyleFeat(11);
        Assert(csd.GrantedFeats.Contains("Rapid Shot"), "Archery L2: Rapid Shot");
        Assert(csd.GrantedFeats.Contains("Manyshot"), "Archery L6: Manyshot");
        Assert(csd.GrantedFeats.Contains("Improved Precise Shot"), "Archery L11: Improved Precise Shot");
        Assert(csd.GrantedFeats.Count == 3, "Archery: Exactly 3 style feats at L11");

        // TWF full progression
        var twf = new CombatStyleData();
        twf.SelectStyle(RangerCombatStyle.TwoWeaponFighting);
        twf.GrantStyleFeat(2);
        twf.GrantStyleFeat(6);
        twf.GrantStyleFeat(11);
        Assert(twf.GrantedFeats.Contains("Two-Weapon Fighting"), "TWF L2");
        Assert(twf.GrantedFeats.Contains("Improved Two-Weapon Fighting"), "TWF L6");
        Assert(twf.GrantedFeats.Contains("Greater Two-Weapon Fighting"), "TWF L11");

        // Non-style-feat levels should not grant
        Assert(!CombatStyleData.IsStyleFeatLevel(3), "Level 3 is not a style feat level");
        Assert(CombatStyleData.IsStyleFeatLevel(2), "Level 2 is a style feat level");
        Assert(CombatStyleData.IsStyleFeatLevel(6), "Level 6 is a style feat level");
        Assert(CombatStyleData.IsStyleFeatLevel(11), "Level 11 is a style feat level");
    }

    // ==================== ANIMAL COMPANION TESTS ====================

    private static void TestAnimalCompanionTemplates()
    {
        AnimalCompanionTemplates.Init();
        var wolf = AnimalCompanionTemplates.GetByName("Wolf");
        Assert(wolf != null, "Animal Companion: Wolf template exists");
        Assert(wolf.Name == "Wolf", "Animal Companion: Wolf name correct");
        Assert(wolf.BaseHD > 0, "Animal Companion: Wolf has base HD");
        Assert(wolf.BaseSpeed > 0, "Animal Companion: Wolf has base speed");

        var badger = AnimalCompanionTemplates.GetByName("Badger");
        Assert(badger != null, "Animal Companion: Badger template exists");

        var allTemplates = AnimalCompanionTemplates.GetAll();
        Assert(allTemplates.Count >= 13, $"Animal Companion: At least 13 templates ({allTemplates.Count} found)");
    }

    private static void TestAnimalCompanionProgression()
    {
        Assert(AnimalCompanionProgression.GetBonusHD(1) == 0, "Companion EDL1: +0 bonus HD");
        Assert(AnimalCompanionProgression.GetBonusNaturalArmor(1) == 0, "Companion EDL1: +0 natural armor");

        Assert(AnimalCompanionProgression.GetBonusHD(3) == 2, "Companion EDL3: +2 bonus HD");
        Assert(AnimalCompanionProgression.GetBonusNaturalArmor(3) == 2, "Companion EDL3: +2 natural armor");

        Assert(AnimalCompanionProgression.GetBonusHD(20) > 0, "Companion EDL20: Has bonus HD");
    }

    private static void TestAnimalCompanionSpecialAbilities()
    {
        // Link and Share Spells at EDL 1
        Assert(AnimalCompanionProgression.HasLink(1), "Companion EDL1: Has Link");
        Assert(AnimalCompanionProgression.HasShareSpells(1), "Companion EDL1: Has Share Spells");

        // Evasion at EDL 3
        Assert(AnimalCompanionProgression.HasEvasion(3), "Companion EDL3: Has Evasion");
        Assert(!AnimalCompanionProgression.HasEvasion(2), "Companion EDL2: No Evasion yet");

        // Devotion at EDL 5
        Assert(AnimalCompanionProgression.HasDevotion(5), "Companion EDL5: Has Devotion");

        // Multiattack at EDL 9
        Assert(AnimalCompanionProgression.HasMultiattack(9), "Companion EDL9: Has Multiattack");

        // Improved Evasion at EDL 15
        Assert(AnimalCompanionProgression.HasImprovedEvasion(15), "Companion EDL15: Has Improved Evasion");
        Assert(!AnimalCompanionProgression.HasImprovedEvasion(14), "Companion EDL14: No Improved Evasion yet");
    }

    private static void TestRangerEffectiveDruidLevel()
    {
        Assert(RangerClass.GetEffectiveDruidLevel(3) == 0, "Ranger L3: No companion (EDL 0)");
        Assert(RangerClass.GetEffectiveDruidLevel(4) == 1, "Ranger L4: EDL 1 (companion gained)");
        Assert(RangerClass.GetEffectiveDruidLevel(10) == 7, "Ranger L10: EDL 7");
        Assert(RangerClass.GetEffectiveDruidLevel(20) == 17, "Ranger L20: EDL 17");
    }

    // ==================== RANGER FEATURE LEVEL TESTS ====================

    private static void TestRangerEvasion()
    {
        var r8 = BuildStats("RangerL8", "Ranger", 8);
        var r9 = BuildStats("RangerL9", "Ranger", 9);
        Assert(!r8.HasEvasion, "Ranger L8: No evasion yet");
        Assert(r9.HasEvasion, "Ranger L9: Has evasion");
    }

    private static void TestRangerWoodlandStride()
    {
        var r6 = BuildStats("RangerL6", "Ranger", 6);
        var r7 = BuildStats("RangerL7", "Ranger", 7);
        Assert(!r6.HasWoodlandStride, "Ranger L6: No Woodland Stride");
        Assert(r7.HasWoodlandStride, "Ranger L7: Has Woodland Stride");
    }

    private static void TestRangerCamouflage()
    {
        var r12 = BuildStats("RangerL12", "Ranger", 12);
        var r13 = BuildStats("RangerL13", "Ranger", 13);
        Assert(!r12.HasCamouflage, "Ranger L12: No Camouflage");
        Assert(r13.HasCamouflage, "Ranger L13: Has Camouflage");
    }

    private static void TestRangerHideInPlainSight()
    {
        var r16 = BuildStats("RangerL16", "Ranger", 16);
        var r17 = BuildStats("RangerL17", "Ranger", 17);
        Assert(!r16.HasHideInPlainSight, "Ranger L16: No Hide in Plain Sight");
        Assert(r17.HasHideInPlainSight, "Ranger L17: Has Hide in Plain Sight");
    }

    // ==================== PALADIN CLASS TESTS ====================

    private static void TestPaladinClassRegistered()
    {
        var paladinClass = ClassRegistry.GetClass("Paladin");
        Assert(paladinClass != null, "Paladin class registered in ClassRegistry");
        Assert(paladinClass != null && paladinClass.ClassName == "Paladin", "Paladin class name is 'Paladin'");
    }

    private static void TestPaladinClassProperties()
    {
        var pc = ClassRegistry.GetClass("Paladin");
        if (pc == null) { Assert(false, "PaladinClass lookup"); return; }

        Assert(pc.HitDie == 10, "Paladin d10 hit die");
        Assert(pc.BABAtLevel3 == 3, "Paladin full BAB (3 at level 3)");
        Assert(pc.GoodFortitude == true, "Paladin good Fort save");
        Assert(pc.GoodReflex == false, "Paladin poor Ref save");
        Assert(pc.GoodWill == false, "Paladin poor Will save");
        Assert(pc.SkillPointsPerLevel == 2, "Paladin 2 skill points/level");
        Assert(pc.IsSpellcaster == true, "Paladin is spellcaster (partial)");
        Assert(pc.ClassSkills.Contains("Diplomacy"), "Paladin has Diplomacy as class skill");
    }

    private static void TestPaladinQuickStart()
    {
        var data = PaladinClass.GetQuickStartCharacter();
        Assert(data != null, "Paladin QuickStart character created");
        Assert(data.ClassName == "Paladin", "Paladin QuickStart is Paladin class");
        Assert(data.CharacterName == "Corrin", "Paladin QuickStart name is Corrin");
        Assert(data.ChosenAlignment == Alignment.LawfulGood, "Paladin QuickStart is Lawful Good");
        Assert(data.CHA >= 14, "Paladin QuickStart CHA >= 14 (for smite/lay on hands)");
    }

    // ==================== SMITE EVIL TESTS ====================

    private static void TestSmiteEvilUsesPerDay()
    {
        Assert(PaladinClass.SmitesPerDay(1) == 1, "Smite Evil: 1/day at L1");
        Assert(PaladinClass.SmitesPerDay(4) == 1, "Smite Evil: 1/day at L4");
        Assert(PaladinClass.SmitesPerDay(5) == 2, "Smite Evil: 2/day at L5");
        Assert(PaladinClass.SmitesPerDay(10) == 3, "Smite Evil: 3/day at L10");
        Assert(PaladinClass.SmitesPerDay(15) == 4, "Smite Evil: 4/day at L15");
        Assert(PaladinClass.SmitesPerDay(20) == 5, "Smite Evil: 5/day at L20");
    }

    private static void TestSmiteEvilAttackBonus()
    {
        var se = new SmiteEvilData();
        se.Initialize(5, 3); // Level 5, CHA mod +3
        Assert(se.GetSmiteAttackBonus() == 3, "Smite attack bonus = CHA mod (+3)");

        var seNeg = new SmiteEvilData();
        seNeg.Initialize(5, -1); // Negative CHA mod
        Assert(seNeg.GetSmiteAttackBonus() == 0, "Smite attack bonus minimum +0 with negative CHA");
    }

    private static void TestSmiteEvilDamageBonus()
    {
        var se = new SmiteEvilData();
        se.Initialize(10, 2); // Level 10, CHA mod +2
        Assert(se.GetSmiteDamageBonus() == 10, "Smite damage bonus = paladin level (10)");
    }

    private static void TestSmiteEvilExpend()
    {
        var se = new SmiteEvilData();
        se.Initialize(5, 2); // Level 5 = 2/day
        Assert(se.MaxUsesPerDay == 2, "Smite: 2 max uses at L5");
        Assert(se.CanSmite, "Smite: Can smite initially");

        bool used1 = se.ExpendSmite();
        Assert(used1, "Smite: First use succeeds");
        Assert(se.RemainingUses == 1, "Smite: 1 remaining after first use");

        bool used2 = se.ExpendSmite();
        Assert(used2, "Smite: Second use succeeds");
        Assert(!se.CanSmite, "Smite: Cannot smite after expending all");

        bool used3 = se.ExpendSmite();
        Assert(!used3, "Smite: Third use fails (none remaining)");

        se.RefreshUses();
        Assert(se.CanSmite, "Smite: Can smite again after refresh");
        Assert(se.RemainingUses == 2, "Smite: Full uses restored after refresh");
    }

    // ==================== LAY ON HANDS TESTS ====================

    private static void TestLayOnHandsPool()
    {
        Assert(PaladinClass.LayOnHandsPool(5, 3) == 15, "Lay on Hands: L5 CHA+3 = 15 pool");
        Assert(PaladinClass.LayOnHandsPool(10, 2) == 20, "Lay on Hands: L10 CHA+2 = 20 pool");
        Assert(PaladinClass.LayOnHandsPool(5, 0) == 0, "Lay on Hands: CHA+0 = 0 pool");
        Assert(PaladinClass.LayOnHandsPool(5, -1) == 0, "Lay on Hands: CHA-1 = 0 pool");
    }

    private static void TestLayOnHandsHeal()
    {
        var loh = new LayOnHandsData();
        loh.Initialize(5, 3); // Pool = 15
        Assert(loh.MaxPool == 15, "Lay on Hands: Max pool is 15");
        Assert(loh.CanLayOnHands, "Lay on Hands: Can use initially");

        int healed = loh.HealLiving(10);
        Assert(healed == 10, "Lay on Hands: Healed 10 HP");
        Assert(loh.RemainingPool == 5, "Lay on Hands: 5 remaining after healing 10");

        int healed2 = loh.HealLiving(8);
        Assert(healed2 == 5, "Lay on Hands: Only 5 healed (capped by remaining pool)");
        Assert(!loh.CanLayOnHands, "Lay on Hands: Cannot use after pool depleted");

        loh.RefreshPool();
        Assert(loh.RemainingPool == 15, "Lay on Hands: Pool restored after refresh");
    }

    private static void TestLayOnHandsHarmUndead()
    {
        var loh = new LayOnHandsData();
        loh.Initialize(5, 3); // Pool = 15
        int damage = loh.HarmUndead(7);
        Assert(damage == 7, "Lay on Hands: 7 damage to undead");
        Assert(loh.RemainingPool == 8, "Lay on Hands: 8 remaining after harming undead for 7");
    }

    // ==================== PALADIN FEATURE LEVEL TESTS ====================

    private static void TestPaladinDivineGrace()
    {
        var p1 = BuildStats("PaladinL1", "Paladin", 1, cha: 16); // CHA mod +3
        var p2 = BuildStats("PaladinL2", "Paladin", 2, cha: 16);
        Assert(p1.DivineGraceBonus == 0, "Paladin L1: No Divine Grace yet");
        Assert(p2.DivineGraceBonus == 3, "Paladin L2: Divine Grace +3 (CHA mod)");

        var p2Low = BuildStats("PaladinL2Low", "Paladin", 2, cha: 8); // CHA mod -1
        Assert(p2Low.DivineGraceBonus == 0, "Paladin L2 CHA 8: Divine Grace min +0");
    }

    private static void TestPaladinAuraOfCourage()
    {
        var p2 = BuildStats("PaladinL2", "Paladin", 2);
        var p3 = BuildStats("PaladinL3", "Paladin", 3);
        Assert(!p2.HasAuraOfCourage, "Paladin L2: No Aura of Courage");
        Assert(p3.HasAuraOfCourage, "Paladin L3: Has Aura of Courage");
    }

    private static void TestPaladinDivineHealth()
    {
        var p2 = BuildStats("PaladinL2", "Paladin", 2);
        var p3 = BuildStats("PaladinL3", "Paladin", 3);
        Assert(!p2.HasDivineHealth, "Paladin L2: No Divine Health");
        Assert(p3.HasDivineHealth, "Paladin L3: Has Divine Health");
    }

    private static void TestPaladinRemoveDisease()
    {
        Assert(PaladinClass.RemoveDiseasePerWeek(5) == 0, "Remove Disease: 0/week at L5");
        Assert(PaladinClass.RemoveDiseasePerWeek(6) == 1, "Remove Disease: 1/week at L6");
        Assert(PaladinClass.RemoveDiseasePerWeek(9) == 2, "Remove Disease: 2/week at L9");
        Assert(PaladinClass.RemoveDiseasePerWeek(12) == 3, "Remove Disease: 3/week at L12");
        Assert(PaladinClass.RemoveDiseasePerWeek(15) == 4, "Remove Disease: 4/week at L15");
        Assert(PaladinClass.RemoveDiseasePerWeek(18) == 5, "Remove Disease: 5/week at L18");
    }

    private static void TestPaladinTurnUndead()
    {
        Assert(PaladinClass.HasTurnUndead(3) == false, "Paladin L3: No Turn Undead");
        Assert(PaladinClass.HasTurnUndead(4) == true, "Paladin L4: Has Turn Undead");
        Assert(PaladinClass.TurnUndeadEffectiveLevel(4) == 1, "Paladin L4: Turn as Cleric 1");
        Assert(PaladinClass.TurnUndeadEffectiveLevel(7) == 4, "Paladin L7: Turn as Cleric 4");
        Assert(PaladinClass.TurnUndeadEffectiveLevel(20) == 17, "Paladin L20: Turn as Cleric 17");
    }

    // ==================== PARTIAL CASTER FRAMEWORK TESTS ====================

    private static void TestPartialCasterInit()
    {
        var pcd = new PartialCasterData();
        pcd.Initialize("Ranger", 4, 1); // WIS mod +1
        Assert(pcd.IsInitialized, "PartialCaster: Initialized successfully");
        Assert(pcd.ClassName == "Ranger", "PartialCaster: Class name is Ranger");
        Assert(pcd.HasSpellcasting, "PartialCaster: L4 Ranger has spellcasting");

        var pcdLow = new PartialCasterData();
        pcdLow.Initialize("Ranger", 3, 1);
        Assert(!pcdLow.HasSpellcasting, "PartialCaster: L3 Ranger does NOT have spellcasting");
    }

    private static void TestPartialCasterSpellProgression()
    {
        // Ranger L4: 0 1st-level spells base (PHB p.48)
        var r4 = new PartialCasterData();
        r4.Initialize("Ranger", 4, 0); // WIS 10, mod 0
        Assert(PartialCasterData.GetBaseSlots(4, 1) == 0, "Ranger L4: 0 base 1st-level slots");

        // Ranger L5 with WIS 12 (mod +1): 0 base + 1 bonus = 1 first-level slot
        var r5 = new PartialCasterData();
        r5.Initialize("Ranger", 5, 1);
        Assert(r5.HasAccessToSpellLevel(1), "Ranger L5: Has access to 1st-level spells");

        // Ranger L11 should have 1st and 2nd level spells
        var r11 = new PartialCasterData();
        r11.Initialize("Ranger", 11, 2); // WIS 14, mod +2
        Assert(r11.HasAccessToSpellLevel(1), "Ranger L11: Has 1st-level access");
        Assert(r11.HasAccessToSpellLevel(2), "Ranger L11: Has 2nd-level access");

        // Ranger L14 should have 1st, 2nd, 3rd level
        var r14 = new PartialCasterData();
        r14.Initialize("Ranger", 14, 2);
        Assert(r14.HasAccessToSpellLevel(3), "Ranger L14: Has 3rd-level access");

        // Paladin L14 should have 1st, 2nd, 3rd level
        var p14 = new PartialCasterData();
        p14.Initialize("Paladin", 14, 2);
        Assert(p14.HasAccessToSpellLevel(3), "Paladin L14: Has 3rd-level access");

        // Max 4th-level spells at high levels
        var r20 = new PartialCasterData();
        r20.Initialize("Ranger", 20, 3); // WIS 16
        Assert(r20.HasAccessToSpellLevel(4), "Ranger L20: Has 4th-level access");
        Assert(r20.GetHighestSpellLevel() == 4, "Ranger L20: Highest spell level is 4");
    }

    private static void TestPartialCasterBonusSlots()
    {
        // WIS 14 (mod +2): bonus slots at 1st and 2nd level
        var pcd = new PartialCasterData();
        pcd.Initialize("Ranger", 11, 2); // WIS 14

        // PHB bonus slot formula: bonus at spell level L if modifier >= L
        // WIS mod +2: +1 bonus 1st, +1 bonus 2nd, +0 bonus 3rd+
        int base1st = PartialCasterData.GetBaseSlots(11, 1);
        int total1st = pcd.SlotsMax[1];
        Assert(total1st > base1st, $"Ranger L11 WIS14: 1st-level has bonus slots ({total1st} > {base1st})");
    }

    private static void TestPartialCasterCanCastAndSpend()
    {
        var pcd = new PartialCasterData();
        pcd.Initialize("Ranger", 8, 2); // WIS 14, mod +2, has 1st-level spells

        // Prepare a spell first
        pcd.PrepareSpell("cure_light_wounds", 1);

        bool canCast = pcd.CanCast(1);
        Assert(canCast, "PartialCaster: Can cast at 1st level (has slots)");

        bool spent = pcd.SpendSlot(1);
        Assert(spent, "PartialCaster: Slot spent successfully");

        // After refresh, should be able to cast again
        pcd.RefreshAllSlots();
        Assert(pcd.CanCast(1), "PartialCaster: Can cast again after refresh");
    }

    private static void TestPartialCasterPrepareSpells()
    {
        var pcd = new PartialCasterData();
        pcd.Initialize("Ranger", 8, 2);

        pcd.PrepareSpell("cure_light_wounds", 1);
        pcd.PrepareSpell("entangle", 1);
        var prepared = pcd.GetAllPreparedSpellIds();
        Assert(prepared.Contains("cure_light_wounds"), "PartialCaster: cure_light_wounds is prepared");
        Assert(prepared.Contains("entangle"), "PartialCaster: entangle is prepared");

        pcd.ClearPreparedSpells();
        Assert(pcd.GetAllPreparedSpellIds().Count == 0, "PartialCaster: All prepared spells cleared");
    }

    private static void TestPartialCasterClassDetection()
    {
        Assert(PartialCasterData.IsPartialCasterClass("Ranger"), "IsPartialCasterClass: Ranger = true");
        Assert(PartialCasterData.IsPartialCasterClass("Paladin"), "IsPartialCasterClass: Paladin = true");
        Assert(!PartialCasterData.IsPartialCasterClass("Wizard"), "IsPartialCasterClass: Wizard = false");
        Assert(!PartialCasterData.IsPartialCasterClass("Sorcerer"), "IsPartialCasterClass: Sorcerer = false");
        Assert(!PartialCasterData.IsPartialCasterClass("Fighter"), "IsPartialCasterClass: Fighter = false");
    }
}
}
