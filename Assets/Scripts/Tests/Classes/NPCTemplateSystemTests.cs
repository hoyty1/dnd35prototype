using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Classes
{
/// <summary>
/// NPC Template System tests — Phase 4:
/// 1. NPC classes (Adept, Aristocrat, Commoner, Expert, Warrior)
/// 2. Adept spell list (60 spells, levels 0-5)
/// 3. Template database (70 templates, queries by class/level/CR)
/// 4. Creature class engine (HD, BAB, saves, HP, skill points)
/// 5. CR calculator (associated, nonassociated, NPC CR, fractional)
/// 6. ECL tracker
/// 7. Stat array applier (elite/nonelite, class priorities)
/// 8. Quick spawn system
/// 9. Equipment assigner (DMG wealth table)
/// 10. Integration (ClassRegistry 16 classes, CharacterStats NPC properties)
///
/// D&D 3.5e DMG Chapter 4 accuracy verified.
/// </summary>
public static class NPCTemplateSystemTests
{
    private static int _passed;
    private static int _failed;

    public static void npc_template_system_tests() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== NPC TEMPLATE SYSTEM TESTS ======");
        RaceDatabase.Init();
        ClassRegistry.Init();
        SpellDatabase.Init();
        NPCTemplateDatabase.Init();
        AdeptSpellList.Init();

        // === NPC CLASS TESTS ===
        TestAdeptClassRegistered();
        TestAdeptClassProperties();
        TestAristocratClassRegistered();
        TestAristocratClassProperties();
        TestCommonerClassRegistered();
        TestCommonerClassProperties();
        TestExpertClassRegistered();
        TestExpertClassProperties();
        TestWarriorClassRegistered();
        TestWarriorClassProperties();
        TestNPCClassCount();

        // === ADEPT SPELL LIST TESTS ===
        TestAdeptSpellListLoaded();
        TestAdeptCantrips();
        TestAdeptHighLevelSpells();
        TestAdeptSpellLookup();
        TestAdeptSpellLevelLookup();

        // === TEMPLATE DATABASE TESTS ===
        TestTemplateDatabaseLoaded();
        TestTemplateCount();
        TestTemplateLookupByClassLevel();
        TestAllPHBTemplatesExist();
        TestAllNPCTemplatesExist();
        TestTemplateQueryByCR();
        TestNearestTemplateLookup();
        TestAllTemplatesForClass();
        TestAllClassNamesInDatabase();
        TestTemplateCRValues();
        TestTemplateStatBlocks();

        // === CREATURE CLASS ENGINE TESTS ===
        TestTotalHDCalculation();
        TestClassBABCalculation();
        TestTotalBABCalculation();
        TestClassSaveCalculation();
        TestClassSkillPointsCalculation();
        TestFeatsFromTotalHD();
        TestAbilityIncreasesFromTotalHD();
        TestClassHPCalculation();

        // === CR CALCULATOR TESTS ===
        TestCRToFloatConversion();
        TestFloatToCRConversion();
        TestAssociatedClassCR();
        TestNonassociatedClassCR();
        TestNPCCR();
        TestStandardCRLookup();

        // === ECL TRACKER TESTS ===
        TestECLBasicCalculation();
        TestECLWithLevelAdjustment();
        TestECLFeatsAndAbilityIncreases();
        TestECLXPCalculation();

        // === STAT ARRAY TESTS ===
        TestEliteArray();
        TestNoneliteArray();
        TestStatPriorityFighter();
        TestStatPriorityWizard();
        TestStatPriorityAdept();
        TestStatPriorityWarrior();
        TestApplyEliteArrayFighter();
        TestApplyNoneliteArrayCommoner();
        TestRacialModifiers();
        TestAbilityIncreasesFromHD();

        // === EQUIPMENT ASSIGNER TESTS ===
        TestEquipmentValueCalculation();
        TestExpectedWealthByLevel();
        TestMagicItemCount();
        TestEquipmentSummary();

        // === QUICK SPAWN TESTS ===
        TestSpawnWarrior();
        TestSpawnAdept();
        TestSpawnNPCByCR();
        TestCreateFromTemplate();

        // === INTEGRATION TESTS ===
        TestClassRegistryTotal();
        TestCharacterStatsNPCProperties();
        TestCharacterStatsECL();

        // === CLASS ASSOCIATION RULES TESTS ===
        TestHumanoidAssociations();
        TestGiantAssociations();
        TestDragonAssociations();
        TestUndeadAssociations();
        TestIsNPCClass();

        Debug.Log($"====== NPC TEMPLATE SYSTEM: {_passed} passed, {_failed} failed, {_passed + _failed} total ======");
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

    // ==================== NPC CLASS TESTS ====================

    private static void TestAdeptClassRegistered()
    {
        var c = ClassRegistry.GetClass("Adept");
        Assert(c != null, "Adept class registered in ClassRegistry");
        Assert(c != null && c.ClassName == "Adept", "Adept class name is 'Adept'");
    }

    private static void TestAdeptClassProperties()
    {
        var c = ClassRegistry.GetClass("Adept");
        if (c == null) { Assert(false, "Adept lookup"); return; }

        Assert(c.HitDie == 6, "Adept d6 hit die");
        Assert(c.BABAtLevel3 == 1, "Adept poor BAB (1 at level 3)");
        Assert(c.FortSaveIsGood == false, "Adept Fort save poor");
        Assert(c.RefSaveIsGood == false, "Adept Ref save poor");
        Assert(c.WillSaveIsGood == true, "Adept Will save good");
        Assert(c.SkillPointsPerLevel == 2, "Adept 2 skill points per level");
        Assert(c.DefaultArmorBonus == 0, "Adept no default armor");
        Assert(c.DefaultShieldBonus == 0, "Adept no default shield");
    }

    private static void TestAristocratClassRegistered()
    {
        var c = ClassRegistry.GetClass("Aristocrat");
        Assert(c != null, "Aristocrat class registered in ClassRegistry");
        Assert(c != null && c.ClassName == "Aristocrat", "Aristocrat class name is 'Aristocrat'");
    }

    private static void TestAristocratClassProperties()
    {
        var c = ClassRegistry.GetClass("Aristocrat");
        if (c == null) { Assert(false, "Aristocrat lookup"); return; }

        Assert(c.HitDie == 8, "Aristocrat d8 hit die");
        Assert(c.BABAtLevel3 == 2, "Aristocrat medium BAB (2 at level 3)");
        Assert(c.FortSaveIsGood == false, "Aristocrat Fort save poor");
        Assert(c.RefSaveIsGood == false, "Aristocrat Ref save poor");
        Assert(c.WillSaveIsGood == true, "Aristocrat Will save good");
        Assert(c.SkillPointsPerLevel == 4, "Aristocrat 4 skill points per level");
    }

    private static void TestCommonerClassRegistered()
    {
        var c = ClassRegistry.GetClass("Commoner");
        Assert(c != null, "Commoner class registered in ClassRegistry");
        Assert(c != null && c.ClassName == "Commoner", "Commoner class name is 'Commoner'");
    }

    private static void TestCommonerClassProperties()
    {
        var c = ClassRegistry.GetClass("Commoner");
        if (c == null) { Assert(false, "Commoner lookup"); return; }

        Assert(c.HitDie == 4, "Commoner d4 hit die (weakest)");
        Assert(c.BABAtLevel3 == 1, "Commoner poor BAB (1 at level 3)");
        Assert(c.FortSaveIsGood == false, "Commoner Fort save poor");
        Assert(c.RefSaveIsGood == false, "Commoner Ref save poor");
        Assert(c.WillSaveIsGood == false, "Commoner Will save poor (all saves poor)");
        Assert(c.SkillPointsPerLevel == 2, "Commoner 2 skill points per level");
        Assert(c.DefaultArmorBonus == 0, "Commoner no armor proficiency");
    }

    private static void TestExpertClassRegistered()
    {
        var c = ClassRegistry.GetClass("Expert");
        Assert(c != null, "Expert class registered in ClassRegistry");
        Assert(c != null && c.ClassName == "Expert", "Expert class name is 'Expert'");
    }

    private static void TestExpertClassProperties()
    {
        var c = ClassRegistry.GetClass("Expert");
        if (c == null) { Assert(false, "Expert lookup"); return; }

        Assert(c.HitDie == 6, "Expert d6 hit die");
        Assert(c.BABAtLevel3 == 2, "Expert medium BAB (2 at level 3)");
        Assert(c.FortSaveIsGood == false, "Expert Fort save poor");
        Assert(c.RefSaveIsGood == true, "Expert Ref save good");
        Assert(c.WillSaveIsGood == true, "Expert Will save good");
        Assert(c.SkillPointsPerLevel == 6, "Expert 6 skill points per level (highest NPC class)");
    }

    private static void TestWarriorClassRegistered()
    {
        var c = ClassRegistry.GetClass("Warrior");
        Assert(c != null, "Warrior class registered in ClassRegistry");
        Assert(c != null && c.ClassName == "Warrior", "Warrior class name is 'Warrior'");
    }

    private static void TestWarriorClassProperties()
    {
        var c = ClassRegistry.GetClass("Warrior");
        if (c == null) { Assert(false, "Warrior lookup"); return; }

        Assert(c.HitDie == 8, "Warrior d8 hit die");
        Assert(c.BABAtLevel3 == 3, "Warrior good BAB (3 at level 3, full progression)");
        Assert(c.FortSaveIsGood == true, "Warrior Fort save good");
        Assert(c.RefSaveIsGood == false, "Warrior Ref save poor");
        Assert(c.WillSaveIsGood == false, "Warrior Will save poor");
        Assert(c.SkillPointsPerLevel == 2, "Warrior 2 skill points per level");
    }

    private static void TestNPCClassCount()
    {
        string[] npcClasses = { "Adept", "Aristocrat", "Commoner", "Expert", "Warrior" };
        int found = 0;
        foreach (var name in npcClasses)
        {
            if (ClassRegistry.GetClass(name) != null) found++;
        }
        Assert(found == 5, "All 5 NPC classes registered", $"found={found}");
    }

    // ==================== ADEPT SPELL LIST TESTS ====================

    private static void TestAdeptSpellListLoaded()
    {
        Assert(AdeptSpellList.TotalSpellCount > 0, "Adept spell list loaded with spells",
            $"count={AdeptSpellList.TotalSpellCount}");
        Assert(AdeptSpellList.TotalSpellCount >= 50, "Adept has 50+ spells total",
            $"count={AdeptSpellList.TotalSpellCount}");
    }

    private static void TestAdeptCantrips()
    {
        var cantrips = AdeptSpellList.GetSpellsForLevel(0);
        Assert(cantrips != null && cantrips.Count > 0, "Adept has cantrips (0th level spells)");
        Assert(cantrips != null && cantrips.Count >= 8, "Adept has 8+ cantrips",
            $"count={cantrips?.Count}");
    }

    private static void TestAdeptHighLevelSpells()
    {
        Assert(AdeptSpellList.MaxSpellLevel == 5, "Adept max spell level is 5");
        var level5 = AdeptSpellList.GetSpellsForLevel(5);
        Assert(level5 != null && level5.Count > 0, "Adept has 5th level spells");
        var level6 = AdeptSpellList.GetSpellsForLevel(6);
        Assert(level6 == null || level6.Count == 0, "Adept has no 6th level spells");
    }

    private static void TestAdeptSpellLookup()
    {
        // Cure Light Wounds is a classic adept spell
        bool isCLW = AdeptSpellList.IsAdeptSpell("CURE_LIGHT_WOUNDS");
        Assert(isCLW, "Cure Light Wounds is an adept spell");
        bool isBless = AdeptSpellList.IsAdeptSpell("BLESS");
        Assert(isBless, "Bless is an adept spell");
        // Fireball should not be an adept spell
        bool isFireball = AdeptSpellList.IsAdeptSpell("FIREBALL");
        Assert(!isFireball, "Fireball is NOT an adept spell");
    }

    private static void TestAdeptSpellLevelLookup()
    {
        int lvl = AdeptSpellList.GetSpellLevel("CURE_LIGHT_WOUNDS");
        Assert(lvl == 1, "Cure Light Wounds is 1st level for adept", $"got={lvl}");
        int lvl2 = AdeptSpellList.GetSpellLevel("CURE_MODERATE_WOUNDS");
        Assert(lvl2 == 2, "Cure Moderate Wounds is 2nd level for adept", $"got={lvl2}");
    }

    // ==================== TEMPLATE DATABASE TESTS ====================

    private static void TestTemplateDatabaseLoaded()
    {
        Assert(NPCTemplateDatabase.Count > 0, "Template database loaded with templates");
    }

    private static void TestTemplateCount()
    {
        // 11 PHB classes × 5 levels + 5 NPC classes × 3 levels = 55 + 15 = 70
        Assert(NPCTemplateDatabase.Count == 70, "Template database has exactly 70 templates",
            $"count={NPCTemplateDatabase.Count}");
    }

    private static void TestTemplateLookupByClassLevel()
    {
        var t = NPCTemplateDatabase.GetTemplate("Fighter", 1);
        Assert(t != null, "Fighter level 1 template exists");
        Assert(t != null && t.ClassName == "Fighter", "Fighter template has correct class name");
        Assert(t != null && t.Level == 1, "Fighter template has correct level");

        var t2 = NPCTemplateDatabase.GetTemplate("Wizard", 20);
        Assert(t2 != null, "Wizard level 20 template exists");
        Assert(t2 != null && t2.Level == 20, "Wizard level 20 template correct level");
    }

    private static void TestAllPHBTemplatesExist()
    {
        string[] phbClasses = { "Fighter", "Rogue", "Monk", "Barbarian", "Wizard",
            "Cleric", "Sorcerer", "Ranger", "Paladin", "Bard", "Druid" };
        int[] levels = { 1, 5, 10, 15, 20 };
        int found = 0;
        foreach (var cls in phbClasses)
            foreach (var lvl in levels)
                if (NPCTemplateDatabase.GetTemplate(cls, lvl) != null) found++;
        Assert(found == 55, "All 55 PHB class templates exist (11×5)", $"found={found}");
    }

    private static void TestAllNPCTemplatesExist()
    {
        string[] npcClasses = { "Adept", "Aristocrat", "Commoner", "Expert", "Warrior" };
        int[] levels = { 1, 5, 10 };
        int found = 0;
        foreach (var cls in npcClasses)
            foreach (var lvl in levels)
                if (NPCTemplateDatabase.GetTemplate(cls, lvl) != null) found++;
        Assert(found == 15, "All 15 NPC class templates exist (5×3)", $"found={found}");
    }

    private static void TestTemplateQueryByCR()
    {
        var cr1 = NPCTemplateDatabase.GetTemplatesForCR(1);
        Assert(cr1 != null && cr1.Count > 0, "Templates exist for CR 1");

        var cr20 = NPCTemplateDatabase.GetTemplatesForCR(20);
        Assert(cr20 != null && cr20.Count > 0, "Templates exist for CR 20");
    }

    private static void TestNearestTemplateLookup()
    {
        // Request level 3, should return nearest (1 or 5)
        var t = NPCTemplateDatabase.GetNearestTemplate("Fighter", 3);
        Assert(t != null, "Nearest template found for Fighter L3");
        Assert(t != null && (t.Level == 1 || t.Level == 5), "Nearest Fighter L3 returns L1 or L5",
            $"got level={t?.Level}");
    }

    private static void TestAllTemplatesForClass()
    {
        var fighters = NPCTemplateDatabase.GetAllTemplatesForClass("Fighter");
        Assert(fighters != null && fighters.Count == 5, "Fighter has 5 templates (L1,5,10,15,20)",
            $"count={fighters?.Count}");

        var warriors = NPCTemplateDatabase.GetAllTemplatesForClass("Warrior");
        Assert(warriors != null && warriors.Count == 3, "Warrior has 3 templates (L1,5,10)",
            $"count={warriors?.Count}");
    }

    private static void TestAllClassNamesInDatabase()
    {
        var names = NPCTemplateDatabase.GetAllClassNames();
        Assert(names != null && names.Count == 16, "Database has all 16 class names",
            $"count={names?.Count}");
    }

    private static void TestTemplateCRValues()
    {
        // Fighter L1 should be CR 1
        var f1 = NPCTemplateDatabase.GetTemplate("Fighter", 1);
        Assert(f1 != null && f1.CR == 1, "Fighter L1 is CR 1", $"cr={f1?.CR}");

        // Fighter L20 should be CR 20
        var f20 = NPCTemplateDatabase.GetTemplate("Fighter", 20);
        Assert(f20 != null && f20.CR == 20, "Fighter L20 is CR 20", $"cr={f20?.CR}");

        // Warrior L1 should have lower CR than Fighter L1 (typically CR 1/2)
        var w1 = NPCTemplateDatabase.GetTemplate("Warrior", 1);
        Assert(w1 != null && w1.CR <= 1, "Warrior L1 is CR 1 or lower", $"cr={w1?.CR}");
    }

    private static void TestTemplateStatBlocks()
    {
        // Fighter L1: should have reasonable stats
        var f1 = NPCTemplateDatabase.GetTemplate("Fighter", 1);
        if (f1 == null) { Assert(false, "Fighter L1 stat block test"); return; }

        Assert(f1.HP > 0, "Fighter L1 has positive HP", $"hp={f1.HP}");
        Assert(f1.AC >= 10, "Fighter L1 AC >= 10", $"ac={f1.AC}");
        Assert(f1.BAB >= 1, "Fighter L1 BAB >= 1", $"bab={f1.BAB}");
        Assert(f1.Strength > 0, "Fighter L1 has Strength", $"str={f1.Strength}");
        Assert(f1.Equipment != null && f1.Equipment.Count > 0, "Fighter L1 has equipment");
        Assert(f1.Feats != null && f1.Feats.Count > 0, "Fighter L1 has feats");

        // Wizard L10: should have spellcasting data
        var w10 = NPCTemplateDatabase.GetTemplate("Wizard", 10);
        if (w10 == null) { Assert(false, "Wizard L10 stat block test"); return; }
        Assert(w10.Spellcasting != null, "Wizard L10 has spellcasting data");
        Assert(w10.Intelligence >= 14, "Wizard L10 Intelligence >= 14", $"int={w10.Intelligence}");
    }

    // ==================== CREATURE CLASS ENGINE TESTS ====================

    private static void TestTotalHDCalculation()
    {
        Assert(CreatureClassEngine.CalculateTotalHD(0, 5) == 5, "0 racial HD + 5 class = 5 total HD");
        Assert(CreatureClassEngine.CalculateTotalHD(4, 3) == 7, "4 racial HD + 3 class = 7 total HD");
        Assert(CreatureClassEngine.CalculateTotalHD(10, 0) == 10, "10 racial HD + 0 class = 10 total HD");
    }

    private static void TestClassBABCalculation()
    {
        // Full BAB (3 at level 3) = 1/level
        Assert(CreatureClassEngine.CalculateClassBAB(3, 1) == 1, "Full BAB at 1 level = 1");
        Assert(CreatureClassEngine.CalculateClassBAB(3, 10) == 10, "Full BAB at 10 levels = 10");
        Assert(CreatureClassEngine.CalculateClassBAB(3, 20) == 20, "Full BAB at 20 levels = 20");

        // Medium BAB (2 at level 3) = 3/4 level
        Assert(CreatureClassEngine.CalculateClassBAB(2, 4) == 3, "Medium BAB at 4 levels = 3");

        // Poor BAB (1 at level 3) = 1/2 level
        Assert(CreatureClassEngine.CalculateClassBAB(1, 6) == 3, "Poor BAB at 6 levels = 3");
        Assert(CreatureClassEngine.CalculateClassBAB(1, 1) == 0, "Poor BAB at 1 level = 0");
    }

    private static void TestTotalBABCalculation()
    {
        Assert(CreatureClassEngine.CalculateTotalBAB(5, 3, 3) == 8, "Racial BAB 5 + full BAB 3 levels = 8");
    }

    private static void TestClassSaveCalculation()
    {
        // Good save: 2 + level/2
        Assert(CreatureClassEngine.CalculateClassSave(true, 1) == 2, "Good save at L1 = 2");
        Assert(CreatureClassEngine.CalculateClassSave(true, 10) == 7, "Good save at L10 = 7");
        Assert(CreatureClassEngine.CalculateClassSave(true, 20) == 12, "Good save at L20 = 12");

        // Poor save: level/3
        Assert(CreatureClassEngine.CalculateClassSave(false, 1) == 0, "Poor save at L1 = 0");
        Assert(CreatureClassEngine.CalculateClassSave(false, 3) == 1, "Poor save at L3 = 1");
        Assert(CreatureClassEngine.CalculateClassSave(false, 10) == 3, "Poor save at L10 = 3");
        Assert(CreatureClassEngine.CalculateClassSave(false, 20) == 6, "Poor save at L20 = 6");
    }

    private static void TestClassSkillPointsCalculation()
    {
        // Expert: 6 skill points + INT modifier per level
        // With INT 14 (mod +2), 5 levels: (6+2)*5 = 40
        Assert(CreatureClassEngine.CalculateClassSkillPoints(6, 2, 5) == 40,
            "Expert 5 levels INT +2 = 40 skill points");

        // First level gets ×4
        Assert(CreatureClassEngine.CalculateClassSkillPoints(2, 0, 1) == 8,
            "First level gets ×4 skill points (2×4=8)");
    }

    private static void TestFeatsFromTotalHD()
    {
        Assert(CreatureClassEngine.FeatsFromTotalHD(1) == 1, "1 HD = 1 feat");
        Assert(CreatureClassEngine.FeatsFromTotalHD(3) == 2, "3 HD = 2 feats");
        Assert(CreatureClassEngine.FeatsFromTotalHD(6) == 3, "6 HD = 3 feats");
        Assert(CreatureClassEngine.FeatsFromTotalHD(20) == 7, "20 HD = 7 feats");
    }

    private static void TestAbilityIncreasesFromTotalHD()
    {
        Assert(CreatureClassEngine.AbilityIncreasesFromTotalHD(1) == 0, "1 HD = 0 ability increases");
        Assert(CreatureClassEngine.AbilityIncreasesFromTotalHD(4) == 1, "4 HD = 1 ability increase");
        Assert(CreatureClassEngine.AbilityIncreasesFromTotalHD(8) == 2, "8 HD = 2 ability increases");
        Assert(CreatureClassEngine.AbilityIncreasesFromTotalHD(20) == 5, "20 HD = 5 ability increases");
    }

    private static void TestClassHPCalculation()
    {
        // d8 hit die, CON mod +2, 5 levels: avg = (4.5+2)*5 = 32.5, but uses floor/avg
        int hp = CreatureClassEngine.CalculateClassHP(8, 2, 5);
        Assert(hp > 0, "Class HP is positive", $"hp={hp}");
        Assert(hp >= 15, "d8 + CON 2 for 5 levels >= 15", $"hp={hp}");
    }

    // ==================== CR CALCULATOR TESTS ====================

    private static void TestCRToFloatConversion()
    {
        Assert(CRCalculator.CRToFloat("1/2") == 0.5f, "CR 1/2 = 0.5");
        Assert(CRCalculator.CRToFloat("1/4") == 0.25f, "CR 1/4 = 0.25");
        Assert(CRCalculator.CRToFloat("1/8") == 0.125f, "CR 1/8 = 0.125");
        Assert(CRCalculator.CRToFloat("1") == 1f, "CR 1 = 1.0");
        Assert(CRCalculator.CRToFloat("10") == 10f, "CR 10 = 10.0");
    }

    private static void TestFloatToCRConversion()
    {
        Assert(CRCalculator.FloatToCR(0.5f) == "1/2", "0.5 = CR 1/2");
        Assert(CRCalculator.FloatToCR(0.25f) == "1/4", "0.25 = CR 1/4");
        Assert(CRCalculator.FloatToCR(1f) == "1", "1.0 = CR 1");
        Assert(CRCalculator.FloatToCR(10f) == "10", "10.0 = CR 10");
    }

    private static void TestAssociatedClassCR()
    {
        // Associated class: +1 CR per level
        int adj = CRCalculator.CalculateCRAdjustment("Humanoid", "Fighter", 3, 1);
        Assert(adj == 3, "Humanoid + Fighter (associated) 3 levels = +3 CR", $"adj={adj}");
    }

    private static void TestNonassociatedClassCR()
    {
        // Nonassociated class: +1 CR per 2 levels (until exceeding racial HD)
        // Dragon adding Rogue levels (nonassociated for Dragon)
        int adj = CRCalculator.CalculateCRAdjustment("Dragon", "Rogue", 4, 10);
        Assert(adj == 2, "Dragon + Rogue (nonassociated) 4 levels, 10 racial HD = +2 CR", $"adj={adj}");
    }

    private static void TestNPCCR()
    {
        // NPC classes: Warrior L1 = CR 1/2, Warrior L5 ≈ CR 3
        int cr1 = CRCalculator.CalculateNPCCR("Warrior", 1);
        Assert(cr1 <= 1, "Warrior L1 CR <= 1", $"cr={cr1}");

        int cr5 = CRCalculator.CalculateNPCCR("Warrior", 5);
        Assert(cr5 >= 2 && cr5 <= 4, "Warrior L5 CR between 2-4", $"cr={cr5}");
    }

    private static void TestStandardCRLookup()
    {
        Assert(CRCalculator.GetStandardCR("Fighter", 1) == 1, "Standard CR: Fighter 1 = CR 1");
        Assert(CRCalculator.GetStandardCR("Fighter", 10) == 10, "Standard CR: Fighter 10 = CR 10");
    }

    // ==================== ECL TRACKER TESTS ====================

    private static void TestECLBasicCalculation()
    {
        var ecl = new ECLTracker(0, 5, 0);
        Assert(ecl.ECL == 5, "ECL: 0 racial + 5 class + 0 LA = 5");
        Assert(ecl.TotalHD == 5, "TotalHD: 0 racial + 5 class = 5");
    }

    private static void TestECLWithLevelAdjustment()
    {
        var ecl = new ECLTracker(2, 3, 2);
        Assert(ecl.ECL == 7, "ECL: 2 racial + 3 class + 2 LA = 7");
        Assert(ecl.TotalHD == 5, "TotalHD: 2 racial + 3 class = 5");
        Assert(ecl.HasLevelAdjustmentPenalty, "Has level adjustment penalty when LA > 0");
    }

    private static void TestECLFeatsAndAbilityIncreases()
    {
        var ecl = new ECLTracker(4, 8, 0);
        Assert(ecl.TotalHD == 12, "12 total HD for feat/ability calc");
        Assert(ecl.FeatsFromHD() == 5, "12 HD = 5 feats (1+3+6+9+12)", $"feats={ecl.FeatsFromHD()}");
        Assert(ecl.AbilityIncreasesFromHD() == 3, "12 HD = 3 ability increases (4+8+12)",
            $"increases={ecl.AbilityIncreasesFromHD()}");
    }

    private static void TestECLXPCalculation()
    {
        var ecl = new ECLTracker(0, 5, 0);
        int xp = ecl.XPAtCurrentLevel();
        Assert(xp >= 0, "XP at current level is non-negative", $"xp={xp}");
        int nextXp = ecl.XPForNextLevel();
        Assert(nextXp > xp, "XP for next level > current level XP",
            $"current={xp}, next={nextXp}");
    }

    // ==================== STAT ARRAY TESTS ====================

    private static void TestEliteArray()
    {
        var arr = StatArrayApplier.GetArray(StatArrayType.Elite);
        Assert(arr != null && arr.Length == 6, "Elite array has 6 values");
        Assert(arr[0] == 15, "Elite array highest = 15");
        Assert(arr[5] == 8, "Elite array lowest = 8");
        // Sum: 15+14+13+12+10+8 = 72
        Assert(arr.Sum() == 72, "Elite array sum = 72", $"sum={arr.Sum()}");
    }

    private static void TestNoneliteArray()
    {
        var arr = StatArrayApplier.GetArray(StatArrayType.Nonelite);
        Assert(arr != null && arr.Length == 6, "Nonelite array has 6 values");
        Assert(arr[0] == 13, "Nonelite array highest = 13");
        Assert(arr[5] == 8, "Nonelite array lowest = 8");
        // Sum: 13+12+11+10+9+8 = 63
        Assert(arr.Sum() == 63, "Nonelite array sum = 63", $"sum={arr.Sum()}");
    }

    private static void TestStatPriorityFighter()
    {
        var priority = StatArrayApplier.GetPriorityStats("Fighter");
        Assert(priority != null && priority.Length == 6, "Fighter has 6 priority stats");
        Assert(priority[0] == "Strength", "Fighter primary stat is Strength");
        Assert(priority[1] == "Constitution", "Fighter secondary stat is Constitution");
    }

    private static void TestStatPriorityWizard()
    {
        var priority = StatArrayApplier.GetPriorityStats("Wizard");
        Assert(priority != null && priority.Length == 6, "Wizard has 6 priority stats");
        Assert(priority[0] == "Intelligence", "Wizard primary stat is Intelligence");
    }

    private static void TestStatPriorityAdept()
    {
        var priority = StatArrayApplier.GetPriorityStats("Adept");
        Assert(priority != null && priority.Length == 6, "Adept has 6 priority stats");
        Assert(priority[0] == "Wisdom", "Adept primary stat is Wisdom");
    }

    private static void TestStatPriorityWarrior()
    {
        var priority = StatArrayApplier.GetPriorityStats("Warrior");
        Assert(priority != null && priority.Length == 6, "Warrior has 6 priority stats");
        Assert(priority[0] == "Strength", "Warrior primary stat is Strength");
    }

    private static void TestApplyEliteArrayFighter()
    {
        var stats = StatArrayApplier.ApplyArray(StatArrayType.Elite, "Fighter");
        Assert(stats != null && stats.Count == 6, "Applied elite array has 6 stats");
        Assert(stats["Strength"] == 15, "Fighter elite STR = 15 (highest to primary)",
            $"str={stats["Strength"]}");
        Assert(stats["Constitution"] == 14, "Fighter elite CON = 14 (2nd to secondary)",
            $"con={stats["Constitution"]}");
    }

    private static void TestApplyNoneliteArrayCommoner()
    {
        var stats = StatArrayApplier.ApplyArray(StatArrayType.Nonelite, "Commoner");
        Assert(stats != null && stats.Count == 6, "Applied nonelite array has 6 stats");
        // All stats should be between 8-13
        foreach (var kv in stats)
        {
            Assert(kv.Value >= 8 && kv.Value <= 13,
                $"Commoner nonelite {kv.Key} in range 8-13", $"val={kv.Value}");
        }
    }

    private static void TestRacialModifiers()
    {
        var baseStats = new Dictionary<string, int>
        {
            {"Strength", 14}, {"Dexterity", 12}, {"Constitution", 14},
            {"Intelligence", 10}, {"Wisdom", 12}, {"Charisma", 10}
        };
        var racialMods = new Dictionary<string, int>
        {
            {"Strength", 2}, {"Dexterity", 0}, {"Constitution", -2},
            {"Intelligence", 0}, {"Wisdom", 0}, {"Charisma", 0}
        };
        var result = StatArrayApplier.ApplyRacialModifiers(baseStats, racialMods);
        Assert(result["Strength"] == 16, "Racial +2 STR: 14 → 16", $"str={result["Strength"]}");
        Assert(result["Constitution"] == 12, "Racial -2 CON: 14 → 12", $"con={result["Constitution"]}");
        Assert(result["Dexterity"] == 12, "No racial mod DEX stays 12", $"dex={result["Dexterity"]}");
    }

    private static void TestAbilityIncreasesFromHD()
    {
        var stats = new Dictionary<string, int>
        {
            {"Strength", 15}, {"Dexterity", 14}, {"Constitution", 13},
            {"Intelligence", 12}, {"Wisdom", 10}, {"Charisma", 8}
        };
        StatArrayApplier.ApplyAbilityIncreases(stats, "Fighter", 8);
        // 8 HD = 2 ability increases, both should go to primary stat (Strength for Fighter)
        Assert(stats["Strength"] == 17, "Fighter 8 HD: 2 ability increases to STR (15→17)",
            $"str={stats["Strength"]}");
    }

    // ==================== EQUIPMENT ASSIGNER TESTS ====================

    private static void TestEquipmentValueCalculation()
    {
        var items = new List<EquipmentItem>
        {
            new EquipmentItem { Name = "Longsword", GoldValue = 15 },
            new EquipmentItem { Name = "Chain Shirt", GoldValue = 100 },
            new EquipmentItem { Name = "Shield", GoldValue = 9 }
        };
        int total = EquipmentAssigner.CalculateEquipmentValue(items);
        Assert(total == 124, "Equipment value: 15+100+9 = 124 gp", $"total={total}");
    }

    private static void TestExpectedWealthByLevel()
    {
        // DMG Table 5-1: Level 1 = 900 gp, Level 5 = 9,000 gp, Level 10 = 49,000 gp
        Assert(EquipmentAssigner.GetExpectedWealthByLevel(1) == 900,
            "Level 1 wealth = 900 gp", $"got={EquipmentAssigner.GetExpectedWealthByLevel(1)}");
        Assert(EquipmentAssigner.GetExpectedWealthByLevel(5) == 9000,
            "Level 5 wealth = 9,000 gp", $"got={EquipmentAssigner.GetExpectedWealthByLevel(5)}");
        Assert(EquipmentAssigner.GetExpectedWealthByLevel(10) == 49000,
            "Level 10 wealth = 49,000 gp", $"got={EquipmentAssigner.GetExpectedWealthByLevel(10)}");
        Assert(EquipmentAssigner.GetExpectedWealthByLevel(20) == 760000,
            "Level 20 wealth = 760,000 gp", $"got={EquipmentAssigner.GetExpectedWealthByLevel(20)}");
    }

    private static void TestMagicItemCount()
    {
        var items = new List<EquipmentItem>
        {
            new EquipmentItem { Name = "+1 Longsword", IsMagic = true, GoldValue = 2315 },
            new EquipmentItem { Name = "Chain Shirt", IsMagic = false, GoldValue = 100 },
            new EquipmentItem { Name = "Cloak of Resistance +1", IsMagic = true, GoldValue = 1000 }
        };
        int magic = EquipmentAssigner.CountMagicItems(items);
        Assert(magic == 2, "2 magic items in equipment list", $"count={magic}");
    }

    private static void TestEquipmentSummary()
    {
        var template = NPCTemplateDatabase.GetTemplate("Fighter", 1);
        if (template == null) { Assert(false, "Equipment summary test"); return; }
        string summary = EquipmentAssigner.GetEquipmentSummary(template);
        Assert(!string.IsNullOrEmpty(summary), "Fighter L1 equipment summary is non-empty");
    }

    // ==================== QUICK SPAWN TESTS ====================

    private static void TestSpawnWarrior()
    {
        var def = QuickSpawnSystem.SpawnNPC("Warrior", 5);
        Assert(def != null, "SpawnNPC Warrior L5 returns non-null");
        if (def == null) return;
        Assert(def.DisplayName != null && def.DisplayName.Contains("Warrior"),
            "Spawned Warrior has correct name", $"name={def.DisplayName}");
        Assert(def.MaxHP > 0, "Spawned Warrior has positive HP", $"hp={def.MaxHP}");
    }

    private static void TestSpawnAdept()
    {
        var def = QuickSpawnSystem.SpawnNPC("Adept", 5);
        Assert(def != null, "SpawnNPC Adept L5 returns non-null");
        if (def == null) return;
        Assert(def.MaxHP > 0, "Spawned Adept has positive HP", $"hp={def.MaxHP}");
    }

    private static void TestSpawnNPCByCR()
    {
        var def = QuickSpawnSystem.SpawnNPCByCR(5);
        Assert(def != null, "SpawnNPCByCR(5) returns non-null");
    }

    private static void TestCreateFromTemplate()
    {
        var template = NPCTemplateDatabase.GetTemplate("Fighter", 10);
        if (template == null) { Assert(false, "CreateFromTemplate test"); return; }
        var def = QuickSpawnSystem.CreateFromTemplate(template);
        Assert(def != null, "CreateFromTemplate returns non-null");
        if (def == null) return;
        Assert(def.MaxHP == template.HP, "Created NPC HP matches template",
            $"def={def.MaxHP}, template={template.HP}");
        Assert(def.ChallengeRating == template.CR, "Created NPC CR matches template",
            $"def={def.ChallengeRating}, template={template.CR}");
    }

    // ==================== INTEGRATION TESTS ====================

    private static void TestClassRegistryTotal()
    {
        string[] allClasses = { "Fighter", "Rogue", "Monk", "Barbarian", "Wizard",
            "Cleric", "Sorcerer", "Ranger", "Paladin", "Bard", "Druid",
            "Adept", "Aristocrat", "Commoner", "Expert", "Warrior" };
        int found = 0;
        foreach (var cls in allClasses)
            if (ClassRegistry.GetClass(cls) != null) found++;
        Assert(found == 16, "ClassRegistry has all 16 classes (11 PHB + 5 NPC)", $"found={found}");
    }

    private static void TestCharacterStatsNPCProperties()
    {
        var warrior = BuildStats("TestWarrior", "Warrior", 5);
        Assert(warrior.IsWarrior, "Warrior CharacterStats.IsWarrior = true");
        Assert(warrior.HasNPCClass, "Warrior CharacterStats.HasNPCClass = true");

        var fighter = BuildStats("TestFighter", "Fighter", 5);
        Assert(!fighter.IsWarrior, "Fighter CharacterStats.IsWarrior = false");
        Assert(!fighter.HasNPCClass, "Fighter CharacterStats.HasNPCClass = false");

        var adept = BuildStats("TestAdept", "Adept", 3);
        Assert(adept.IsAdept, "Adept CharacterStats.IsAdept = true");
        Assert(adept.HasNPCClass, "Adept CharacterStats.HasNPCClass = true");

        var commoner = BuildStats("TestCommoner", "Commoner", 1);
        Assert(commoner.IsCommoner, "Commoner CharacterStats.IsCommoner = true");

        var aristocrat = BuildStats("TestAristocrat", "Aristocrat", 2);
        Assert(aristocrat.IsAristocrat, "Aristocrat CharacterStats.IsAristocrat = true");

        var expert = BuildStats("TestExpert", "Expert", 4);
        Assert(expert.IsExpert, "Expert CharacterStats.IsExpert = true");
    }

    private static void TestCharacterStatsECL()
    {
        var warrior = BuildStats("TestWarrior", "Warrior", 5);
        Assert(warrior.ECL != null, "Warrior has ECL tracker");
        if (warrior.ECL != null)
        {
            Assert(warrior.ECL.ClassLevels == 5, "Warrior ECL ClassLevels = 5",
                $"got={warrior.ECL.ClassLevels}");
            Assert(warrior.EffectiveCharacterLevel == 5, "Warrior ECL = 5",
                $"ecl={warrior.EffectiveCharacterLevel}");
        }
    }

    // ==================== CLASS ASSOCIATION RULES TESTS ====================

    private static void TestHumanoidAssociations()
    {
        // Humanoids can take any class as associated
        Assert(ClassAssociationRules.IsAssociatedClass("Humanoid", "Fighter"),
            "Humanoid + Fighter is associated");
        Assert(ClassAssociationRules.IsAssociatedClass("Humanoid", "Wizard"),
            "Humanoid + Wizard is associated");
        Assert(ClassAssociationRules.IsAssociatedClass("Humanoid", "Adept"),
            "Humanoid + Adept is associated");
        Assert(ClassAssociationRules.IsAssociatedClass("Humanoid", "Commoner"),
            "Humanoid + Commoner is associated");
    }

    private static void TestGiantAssociations()
    {
        // Giants: martial classes are associated
        Assert(ClassAssociationRules.IsAssociatedClass("Giant", "Fighter"),
            "Giant + Fighter is associated");
        Assert(ClassAssociationRules.IsAssociatedClass("Giant", "Barbarian"),
            "Giant + Barbarian is associated");
    }

    private static void TestDragonAssociations()
    {
        Assert(ClassAssociationRules.IsAssociatedClass("Dragon", "Sorcerer"),
            "Dragon + Sorcerer is associated");
        Assert(ClassAssociationRules.IsAssociatedClass("Dragon", "Wizard"),
            "Dragon + Wizard is associated");
    }

    private static void TestUndeadAssociations()
    {
        Assert(ClassAssociationRules.IsAssociatedClass("Undead", "Cleric"),
            "Undead + Cleric is associated");
        Assert(ClassAssociationRules.IsAssociatedClass("Undead", "Wizard"),
            "Undead + Wizard is associated");
    }

    private static void TestIsNPCClass()
    {
        Assert(ClassAssociationRules.IsNPCClass("Adept"), "Adept is NPC class");
        Assert(ClassAssociationRules.IsNPCClass("Aristocrat"), "Aristocrat is NPC class");
        Assert(ClassAssociationRules.IsNPCClass("Commoner"), "Commoner is NPC class");
        Assert(ClassAssociationRules.IsNPCClass("Expert"), "Expert is NPC class");
        Assert(ClassAssociationRules.IsNPCClass("Warrior"), "Warrior is NPC class");
        Assert(!ClassAssociationRules.IsNPCClass("Fighter"), "Fighter is NOT NPC class");
        Assert(!ClassAssociationRules.IsNPCClass("Wizard"), "Wizard is NOT NPC class");
    }
}
}
