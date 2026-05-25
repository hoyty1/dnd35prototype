using System.Collections.Generic;
using UnityEngine;

namespace Tests.Classes
{
/// <summary>
/// Phase 3 class implementation tests:
/// 1. Bard class — bardic music (9 abilities), bardic knowledge, spontaneous arcane casting (CHA)
/// 2. Druid class — wild shape (30+ forms), animal companion (full level), prepared divine casting (WIS)
///
/// D&D 3.5e PHB accuracy verified against tables on p.26-29 (Bard), p.33-37 (Druid).
/// </summary>
public static class Phase3ClassTests
{
    private static int _passed;
    private static int _failed;

    public static void phase3_class_tests() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== PHASE 3 CLASS TESTS ======");
        RaceDatabase.Init();
        ClassRegistry.Init();
        WildShapeFormDatabase.Init();

        // ── Bard Class Tests ──
        TestBardClassRegistered();
        TestBardClassProperties();
        TestBardClassSkills();
        TestBardQuickStart();

        // ── Bardic Music Tests ──
        TestBardicMusicUsesPerDay();
        TestInspireCourageScaling();
        TestFascinateTargets();
        TestBardicMusicDC();
        TestBardicAbilityMinLevels();
        TestBardicMusicPerformance();
        TestBardicMusicLinger();

        // ── Bardic Knowledge Tests ──
        TestBardicKnowledgeModifier();
        TestBardicKnowledgeCategories();

        // ── Bard CharacterStats Integration ──
        TestBardCharacterStatsProperties();

        // ── Druid Class Tests ──
        TestDruidClassRegistered();
        TestDruidClassProperties();
        TestDruidClassSkills();
        TestDruidQuickStart();

        // ── Druid Feature Levels ──
        TestDruidNatureSense();
        TestDruidTracklessStep();
        TestDruidResistNaturesLure();
        TestDruidVenomImmunity();
        TestDruidAThousandFaces();
        TestDruidTimelessBody();

        // ── Wild Shape Data Tests ──
        TestWildShapeUsesPerDay();
        TestWildShapeDuration();
        TestWildShapeTransformation();
        TestWildShapeRevert();
        TestWildShapeStatSwap();
        TestWildShapeNaturalSpell();
        TestWildShapeDurationTick();

        // ── Wild Shape Form Database Tests ──
        TestFormDatabaseInit();
        TestFormDatabaseCounts();
        TestFormsByType();
        TestFormAvailabilityByLevel();
        TestFormSizeRestrictions();
        TestElementalForms();
        TestPlantForms();
        TestFormLookupByName();

        // ── Druid CharacterStats Integration ──
        TestDruidCharacterStatsProperties();
        TestDruidAnimalCompanionLevel();

        // ── Druid Armor Restriction ──
        TestDruidMetalArmorCheck();

        // ── ClassRegistry Total ──
        TestAllElevenClassesRegistered();

        Debug.Log($"====== Phase 3 Class Results: {_passed} passed, {_failed} failed ======");
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

    // ==================== BARD CLASS TESTS ====================

    private static void TestBardClassRegistered()
    {
        var bardClass = ClassRegistry.GetClass("Bard");
        Assert(bardClass != null, "Bard class registered in ClassRegistry");
        Assert(bardClass != null && bardClass.ClassName == "Bard", "Bard class name is 'Bard'");
    }

    private static void TestBardClassProperties()
    {
        var bc = ClassRegistry.GetClass("Bard");
        if (bc == null) { Assert(false, "BardClass lookup"); return; }

        Assert(bc.HitDie == 6, "Bard d6 hit die");
        Assert(bc.BABAtLevel3 == 2, "Bard medium BAB (2 at level 3)");
        Assert(bc.GoodFortitude == false, "Bard poor Fort save");
        Assert(bc.GoodReflex == true, "Bard good Ref save");
        Assert(bc.GoodWill == true, "Bard good Will save");
        Assert(bc.SkillPointsPerLevel == 6, "Bard 6 skill points/level");
        Assert(bc.IsSpellcaster == true, "Bard is a spellcaster");
        Assert(bc.DefaultDamageDice == 6, "Bard default damage d6 (rapier)");
    }

    private static void TestBardClassSkills()
    {
        var bc = ClassRegistry.GetClass("Bard");
        if (bc == null) { Assert(false, "BardClass lookup"); return; }

        Assert(bc.ClassSkills.Contains("Bluff"), "Bard class skill: Bluff");
        Assert(bc.ClassSkills.Contains("Diplomacy"), "Bard class skill: Diplomacy");
        Assert(bc.ClassSkills.Contains("Tumble"), "Bard class skill: Tumble");
        Assert(bc.ClassSkills.Contains("Knowledge (Arcana)"), "Bard class skill: Knowledge (Arcana)");
        Assert(bc.ClassSkills.Contains("Knowledge (History)"), "Bard class skill: Knowledge (History)");
        Assert(bc.ClassSkills.Contains("Listen"), "Bard class skill: Listen");
    }

    private static void TestBardQuickStart()
    {
        var data = BardClass.GetQuickStartCharacter();
        Assert(data != null, "Bard QuickStart data created");
        Assert(data.ClassName == "Bard", "Bard QuickStart class is Bard");
        Assert(data.CharacterName == "Lyric", "Bard QuickStart name is Lyric");
        Assert(data.CHA >= 16, "Bard QuickStart CHA >= 16 (primary stat)");
    }

    // ==================== BARDIC MUSIC TESTS ====================

    private static void TestBardicMusicUsesPerDay()
    {
        Assert(BardClass.BardicMusicUsesPerDay(1) == 1, "Bardic Music 1/day at L1");
        Assert(BardClass.BardicMusicUsesPerDay(5) == 5, "Bardic Music 5/day at L5");
        Assert(BardClass.BardicMusicUsesPerDay(10) == 10, "Bardic Music 10/day at L10");
        Assert(BardClass.BardicMusicUsesPerDay(20) == 20, "Bardic Music 20/day at L20");
    }

    private static void TestInspireCourageScaling()
    {
        Assert(BardClass.InspireCourageBonus(1) == 1, "Inspire Courage +1 at L1");
        Assert(BardClass.InspireCourageBonus(7) == 1, "Inspire Courage +1 at L7");
        Assert(BardClass.InspireCourageBonus(8) == 2, "Inspire Courage +2 at L8");
        Assert(BardClass.InspireCourageBonus(13) == 2, "Inspire Courage +2 at L13");
        Assert(BardClass.InspireCourageBonus(14) == 3, "Inspire Courage +3 at L14");
        Assert(BardClass.InspireCourageBonus(19) == 3, "Inspire Courage +3 at L19");
        Assert(BardClass.InspireCourageBonus(20) == 4, "Inspire Courage +4 at L20");
    }

    private static void TestFascinateTargets()
    {
        Assert(BardClass.FascinateTargets(1) == 1, "Fascinate 1 target at L1");
        Assert(BardClass.FascinateTargets(4) == 2, "Fascinate 2 targets at L4");
        Assert(BardClass.FascinateTargets(7) == 3, "Fascinate 3 targets at L7");
        Assert(BardClass.FascinateTargets(10) == 4, "Fascinate 4 targets at L10");
    }

    private static void TestBardicMusicDC()
    {
        // DC = 10 + level/2 + CHA mod
        int dc = BardClass.BardicMusicDC(10, 3); // 10 + 5 + 3 = 18
        Assert(dc == 18, "Bardic Music DC 18 at L10 CHA+3", $"(got {dc})");

        int dc2 = BardClass.BardicMusicDC(1, 2); // 10 + 0 + 2 = 12
        Assert(dc2 == 12, "Bardic Music DC 12 at L1 CHA+2", $"(got {dc2})");
    }

    private static void TestBardicAbilityMinLevels()
    {
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.Countersong) == 1, "Countersong at L1");
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.Fascinate) == 1, "Fascinate at L1");
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.InspireCourage) == 1, "Inspire Courage at L1");
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.InspireCompetence) == 3, "Inspire Competence at L3");
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.Suggestion) == 6, "Suggestion at L6");
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.InspireGreatness) == 9, "Inspire Greatness at L9");
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.SongOfFreedom) == 12, "Song of Freedom at L12");
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.InspireHeroics) == 15, "Inspire Heroics at L15");
        Assert(BardicAbilityInfo.GetMinLevel(BardicAbility.MassSuggestion) == 18, "Mass Suggestion at L18");
    }

    private static void TestBardicMusicPerformance()
    {
        var music = new BardicMusicData();
        music.Initialize(5, 3); // L5 bard, CHA mod +3
        Assert(music.UsesRemaining == 5, "BardicMusic initialized with 5 uses at L5");
        Assert(!music.IsPerforming, "Not performing initially");

        bool started = music.StartPerformance(BardicAbility.InspireCourage, 5);
        Assert(started, "Started Inspire Courage performance");
        Assert(music.IsPerforming, "Is performing after start");
        Assert(music.ActiveAbility == BardicAbility.InspireCourage, "Active ability is Inspire Courage");
        Assert(music.UsesRemaining == 4, "Uses decreased to 4 after starting");

        music.StopPerformance();
        Assert(!music.IsPerforming, "Not performing after stop");
        Assert(music.LingerRoundsRemaining == 5, "5 linger rounds after stopping");
    }

    private static void TestBardicMusicLinger()
    {
        var music = new BardicMusicData();
        music.Initialize(5, 3);
        music.StartPerformance(BardicAbility.InspireCourage, 5);
        music.StopPerformance();

        Assert(music.LingerRoundsRemaining == 5, "Linger starts at 5");
        music.TickLingerRound();
        Assert(music.LingerRoundsRemaining == 4, "Linger decreases to 4");
        music.TickLingerRound();
        music.TickLingerRound();
        music.TickLingerRound();
        music.TickLingerRound();
        Assert(music.LingerRoundsRemaining == 0, "Linger reaches 0");
    }

    // ==================== BARDIC KNOWLEDGE TESTS ====================

    private static void TestBardicKnowledgeModifier()
    {
        // Modifier = bard level + INT mod
        int mod = BardClass.BardicKnowledgeModifier(5, 2); // 5 + 2 = 7
        Assert(mod == 7, "Bardic Knowledge modifier 7 at L5 INT+2", $"(got {mod})");
    }

    private static void TestBardicKnowledgeCategories()
    {
        Assert(BardicKnowledgeData.GetResultCategory(9) == "No useful information", "DC <10 no info");
        Assert(BardicKnowledgeData.GetResultCategory(10) == "Common knowledge", "DC 10 common knowledge");
        Assert(BardicKnowledgeData.GetResultCategory(20) == "Uncommon knowledge", "DC 20 uncommon");
        Assert(BardicKnowledgeData.GetResultCategory(25) == "Rare knowledge", "DC 25 rare");
        Assert(BardicKnowledgeData.GetResultCategory(30) == "Legendary knowledge", "DC 30 legendary");
    }

    // ==================== BARD CHARACTERSTATS INTEGRATION ====================

    private static void TestBardCharacterStatsProperties()
    {
        var stats = BuildStats("TestBard", "Bard", 10, cha: 16); // CHA 16 = +3 mod
        Assert(stats.IsBard, "CharacterStats.IsBard true for Bard");
        Assert(!stats.IsDruid, "CharacterStats.IsDruid false for Bard");
        Assert(stats.BardicMusicUsesPerDay == 10, "Stats.BardicMusicUsesPerDay == 10 at L10");
        Assert(stats.InspireCourageBonus == 2, "Stats.InspireCourageBonus == 2 at L10");

        int expectedDC = 10 + 10 / 2 + 3; // 10 + 5 + 3 = 18
        Assert(stats.BardicMusicDC == expectedDC, "Stats.BardicMusicDC == 18 at L10 CHA+3", $"(got {stats.BardicMusicDC})");
    }

    // ==================== DRUID CLASS TESTS ====================

    private static void TestDruidClassRegistered()
    {
        var druidClass = ClassRegistry.GetClass("Druid");
        Assert(druidClass != null, "Druid class registered in ClassRegistry");
        Assert(druidClass != null && druidClass.ClassName == "Druid", "Druid class name is 'Druid'");
    }

    private static void TestDruidClassProperties()
    {
        var dc = ClassRegistry.GetClass("Druid");
        if (dc == null) { Assert(false, "DruidClass lookup"); return; }

        Assert(dc.HitDie == 8, "Druid d8 hit die");
        Assert(dc.BABAtLevel3 == 2, "Druid medium BAB (2 at level 3)");
        Assert(dc.GoodFortitude == true, "Druid good Fort save");
        Assert(dc.GoodReflex == false, "Druid poor Ref save");
        Assert(dc.GoodWill == true, "Druid good Will save");
        Assert(dc.SkillPointsPerLevel == 4, "Druid 4 skill points/level");
        Assert(dc.IsSpellcaster == true, "Druid is a spellcaster");
    }

    private static void TestDruidClassSkills()
    {
        var dc = ClassRegistry.GetClass("Druid");
        if (dc == null) { Assert(false, "DruidClass lookup"); return; }

        Assert(dc.ClassSkills.Contains("Knowledge (Nature)"), "Druid class skill: Knowledge (Nature)");
        Assert(dc.ClassSkills.Contains("Survival"), "Druid class skill: Survival");
        Assert(dc.ClassSkills.Contains("Listen"), "Druid class skill: Listen");
        Assert(dc.ClassSkills.Contains("Spot"), "Druid class skill: Spot");
        Assert(dc.ClassSkills.Contains("Swim"), "Druid class skill: Swim");
    }

    private static void TestDruidQuickStart()
    {
        var data = DruidClass.GetQuickStartCharacter();
        Assert(data != null, "Druid QuickStart data created");
        Assert(data.ClassName == "Druid", "Druid QuickStart class is Druid");
        Assert(data.CharacterName == "Ashara", "Druid QuickStart name is Ashara");
        Assert(data.WIS >= 16, "Druid QuickStart WIS >= 16 (primary stat)");
    }

    // ==================== DRUID FEATURE LEVEL TESTS ====================

    private static void TestDruidNatureSense()
    {
        Assert(DruidClass.NatureSenseBonus == 2, "Nature Sense bonus is +2");
        var stats = BuildStats("TestDruid", "Druid", 1);
        Assert(stats.DruidNatureSenseBonus == 2, "Stats Nature Sense +2 at L1");
    }

    private static void TestDruidTracklessStep()
    {
        Assert(!DruidClass.HasTracklessStep(2), "No Trackless Step at L2");
        Assert(DruidClass.HasTracklessStep(3), "Trackless Step at L3");
        var stats = BuildStats("TestDruid", "Druid", 3);
        Assert(stats.HasTracklessStep, "Stats HasTracklessStep at L3");
    }

    private static void TestDruidResistNaturesLure()
    {
        Assert(!DruidClass.HasResistNaturesLure(3), "No Resist Nature's Lure at L3");
        Assert(DruidClass.HasResistNaturesLure(4), "Resist Nature's Lure at L4");
        Assert(DruidClass.ResistNaturesLureBonus == 4, "Resist Nature's Lure bonus is +4");
    }

    private static void TestDruidVenomImmunity()
    {
        Assert(!DruidClass.HasVenomImmunity(8), "No Venom Immunity at L8");
        Assert(DruidClass.HasVenomImmunity(9), "Venom Immunity at L9");
    }

    private static void TestDruidAThousandFaces()
    {
        Assert(!DruidClass.HasAThousandFaces(12), "No Thousand Faces at L12");
        Assert(DruidClass.HasAThousandFaces(13), "A Thousand Faces at L13");
    }

    private static void TestDruidTimelessBody()
    {
        Assert(!DruidClass.HasTimelessBody(14), "No Timeless Body at L14");
        Assert(DruidClass.HasTimelessBody(15), "Timeless Body at L15");
    }

    // ==================== WILD SHAPE DATA TESTS ====================

    private static void TestWildShapeUsesPerDay()
    {
        Assert(WildShapeData.GetUsesPerDay(4) == 0, "Wild Shape 0/day at L4");
        Assert(WildShapeData.GetUsesPerDay(5) == 1, "Wild Shape 1/day at L5");
        Assert(WildShapeData.GetUsesPerDay(6) == 2, "Wild Shape 2/day at L6");
        Assert(WildShapeData.GetUsesPerDay(7) == 3, "Wild Shape 3/day at L7");
        Assert(WildShapeData.GetUsesPerDay(10) == 4, "Wild Shape 4/day at L10");
        Assert(WildShapeData.GetUsesPerDay(14) == 5, "Wild Shape 5/day at L14");
        Assert(WildShapeData.GetUsesPerDay(18) == 6, "Wild Shape 6/day at L18");
    }

    private static void TestWildShapeDuration()
    {
        Assert(WildShapeData.GetDurationHours(4) == 0, "Wild Shape 0 hours at L4");
        Assert(WildShapeData.GetDurationHours(5) == 5, "Wild Shape 5 hours at L5");
        Assert(WildShapeData.GetDurationHours(10) == 10, "Wild Shape 10 hours at L10");
        Assert(WildShapeData.GetDurationHours(20) == 20, "Wild Shape 20 hours at L20");
    }

    private static void TestWildShapeTransformation()
    {
        var ws = new WildShapeData();
        ws.Initialize(8); // L8 druid
        Assert(ws.UsesRemaining == 2, "WildShape initialized 2 uses at L8", $"(got {ws.UsesRemaining})");
        Assert(!ws.IsWildShaped, "Not wild shaped initially");

        var wolf = WildShapeFormDatabase.GetFormByName("Dire Wolf");
        Assert(wolf != null, "Dire Wolf form exists in database");

        bool success = ws.TransformInto(wolf, 14, 12, 14, 0, 6);
        Assert(success, "Transform into Dire Wolf succeeded");
        Assert(ws.IsWildShaped, "Is wild shaped after transform");
        Assert(ws.CurrentForm.Name == "Dire Wolf", "Current form is Dire Wolf");
        Assert(ws.UsesRemaining == 1, "Uses decreased to 1 after transform");
    }

    private static void TestWildShapeRevert()
    {
        var ws = new WildShapeData();
        ws.Initialize(8);
        var wolf = WildShapeFormDatabase.GetFormByName("Dire Wolf");
        ws.TransformInto(wolf, 14, 12, 14, 0, 6);

        bool reverted = ws.RevertToNormal();
        Assert(reverted, "Revert to normal succeeded");
        Assert(!ws.IsWildShaped, "Not wild shaped after revert");
        Assert(ws.CurrentForm == null, "No current form after revert");
        Assert(ws.OriginalSTR == 14, "Original STR preserved as 14");
    }

    private static void TestWildShapeStatSwap()
    {
        var ws = new WildShapeData();
        ws.Initialize(8);
        var bear = WildShapeFormDatabase.GetFormByName("Brown Bear");
        Assert(bear != null, "Brown Bear form exists");

        ws.TransformInto(bear, 10, 10, 10, 0, 6);
        Assert(ws.GetWildShapeSTR() == bear.STR, $"Wild Shape STR = Bear STR ({bear.STR})");
        Assert(ws.GetWildShapeDEX() == bear.DEX, $"Wild Shape DEX = Bear DEX ({bear.DEX})");
        Assert(ws.GetWildShapeCON() == bear.CON, $"Wild Shape CON = Bear CON ({bear.CON})");
        Assert(ws.GetWildShapeNaturalArmor() == bear.NaturalArmor, "Wild Shape natural armor from form");
    }

    private static void TestWildShapeNaturalSpell()
    {
        var ws = new WildShapeData();
        ws.Initialize(8, false);
        Assert(!ws.CanCastInWildShape, "Cannot cast without Natural Spell");

        ws.SetNaturalSpellFeat(true);
        Assert(ws.CanCastInWildShape, "Can cast with Natural Spell feat");
    }

    private static void TestWildShapeDurationTick()
    {
        var ws = new WildShapeData();
        ws.Initialize(5); // 5 hours = 3000 rounds
        var bat = WildShapeFormDatabase.GetFormByName("Bat");
        ws.TransformInto(bat, 10, 10, 10, 0, 6);
        Assert(ws.RoundsRemaining == 3000, "Duration 3000 rounds at L5", $"(got {ws.RoundsRemaining})");

        ws.TickRound();
        Assert(ws.RoundsRemaining == 2999, "Duration decreased to 2999 after tick");
        Assert(ws.IsWildShaped, "Still wild shaped after 1 tick");
    }

    // ==================== WILD SHAPE FORM DATABASE TESTS ====================

    private static void TestFormDatabaseInit()
    {
        var allForms = WildShapeFormDatabase.GetAllForms();
        Assert(allForms != null, "Form database initialized");
        Assert(allForms.Count >= 30, $"At least 30 forms in database (got {allForms.Count})");
    }

    private static void TestFormDatabaseCounts()
    {
        var animals = WildShapeFormDatabase.GetFormsByType(WildShapeFormType.Animal);
        var plants = WildShapeFormDatabase.GetFormsByType(WildShapeFormType.Plant);
        var elementals = WildShapeFormDatabase.GetFormsByType(WildShapeFormType.Elemental);

        Assert(animals.Count >= 20, $"At least 20 animal forms (got {animals.Count})");
        Assert(plants.Count >= 3, $"At least 3 plant forms (got {plants.Count})");
        Assert(elementals.Count >= 16, $"At least 16 elemental forms (got {elementals.Count})");
    }

    private static void TestFormsByType()
    {
        var animals = WildShapeFormDatabase.GetFormsByType(WildShapeFormType.Animal);
        bool hasTiny = false, hasSmall = false, hasMedium = false, hasLarge = false, hasHuge = false;
        foreach (var a in animals)
        {
            if (a.Size == WildShapeSize.Tiny) hasTiny = true;
            if (a.Size == WildShapeSize.Small) hasSmall = true;
            if (a.Size == WildShapeSize.Medium) hasMedium = true;
            if (a.Size == WildShapeSize.Large) hasLarge = true;
            if (a.Size == WildShapeSize.Huge) hasHuge = true;
        }
        Assert(hasTiny, "Animals include Tiny size");
        Assert(hasSmall, "Animals include Small size");
        Assert(hasMedium, "Animals include Medium size");
        Assert(hasLarge, "Animals include Large size");
        Assert(hasHuge, "Animals include Huge size");
    }

    private static void TestFormAvailabilityByLevel()
    {
        // L5: Small and Medium animals only
        var l5Forms = WildShapeFormDatabase.GetAvailableForms(5);
        bool hasLargeAtL5 = false;
        foreach (var f in l5Forms)
        {
            if (f.Size == WildShapeSize.Large || f.Size == WildShapeSize.Huge) hasLargeAtL5 = true;
        }
        Assert(!hasLargeAtL5, "No Large/Huge forms at L5");
        Assert(l5Forms.Count > 0, "Some forms available at L5");

        // L8: Large animals available
        var l8Forms = WildShapeFormDatabase.GetAvailableForms(8);
        bool hasLargeAtL8 = false;
        foreach (var f in l8Forms)
        {
            if (f.Size == WildShapeSize.Large && f.FormType == WildShapeFormType.Animal) hasLargeAtL8 = true;
        }
        Assert(hasLargeAtL8, "Large animal forms available at L8");
    }

    private static void TestFormSizeRestrictions()
    {
        var bat = WildShapeFormDatabase.GetFormByName("Bat");
        Assert(bat != null && bat.Size == WildShapeSize.Tiny, "Bat is Tiny");
        Assert(!WildShapeFormDatabase.IsFormAvailable(bat, 5), "Bat (Tiny) not available at L5");
        Assert(WildShapeFormDatabase.IsFormAvailable(bat, 11), "Bat (Tiny) available at L11");

        var elephant = WildShapeFormDatabase.GetFormByName("Elephant");
        Assert(elephant != null && elephant.Size == WildShapeSize.Huge, "Elephant is Huge");
        Assert(!WildShapeFormDatabase.IsFormAvailable(elephant, 14), "Elephant (Huge) not available at L14");
        Assert(WildShapeFormDatabase.IsFormAvailable(elephant, 15), "Elephant (Huge) available at L15");
    }

    private static void TestElementalForms()
    {
        var fireSmall = WildShapeFormDatabase.GetFormByName("Small Fire Elemental");
        Assert(fireSmall != null, "Small Fire Elemental exists");
        Assert(fireSmall.FormType == WildShapeFormType.Elemental, "Fire Elemental is Elemental type");
        Assert(!WildShapeFormDatabase.IsFormAvailable(fireSmall, 15), "Small Fire Elemental not available at L15");
        Assert(WildShapeFormDatabase.IsFormAvailable(fireSmall, 16), "Small Fire Elemental available at L16");
    }

    private static void TestPlantForms()
    {
        var treant = WildShapeFormDatabase.GetFormByName("Treant");
        Assert(treant != null, "Treant form exists");
        Assert(treant.FormType == WildShapeFormType.Plant, "Treant is Plant type");
        Assert(!WildShapeFormDatabase.IsFormAvailable(treant, 11), "Treant not available at L11");
        Assert(WildShapeFormDatabase.IsFormAvailable(treant, 12), "Treant available at L12");
    }

    private static void TestFormLookupByName()
    {
        Assert(WildShapeFormDatabase.GetFormByName("Tiger") != null, "Tiger form exists");
        Assert(WildShapeFormDatabase.GetFormByName("Leopard") != null, "Leopard form exists");
        Assert(WildShapeFormDatabase.GetFormByName("Wolf (Small)") != null, "Wolf (Small) form exists");
        Assert(WildShapeFormDatabase.GetFormByName("Nonexistent") == null, "Nonexistent form returns null");
    }

    // ==================== DRUID CHARACTERSTATS INTEGRATION ====================

    private static void TestDruidCharacterStatsProperties()
    {
        var stats = BuildStats("TestDruid", "Druid", 10);
        Assert(stats.IsDruid, "CharacterStats.IsDruid true for Druid");
        Assert(!stats.IsBard, "CharacterStats.IsBard false for Druid");
        Assert(stats.HasWildShape, "Stats HasWildShape at L10");
        Assert(stats.WildShapeUsesPerDay == 4, "Stats WildShapeUsesPerDay == 4 at L10", $"(got {stats.WildShapeUsesPerDay})");
        Assert(stats.HasTracklessStep, "Stats HasTracklessStep at L10");
        Assert(stats.HasResistNaturesLure, "Stats HasResistNaturesLure at L10");
        Assert(stats.ResistNaturesLureBonus == 4, "Stats ResistNaturesLureBonus == 4");
        Assert(stats.HasVenomImmunity, "Stats HasVenomImmunity at L10");
        Assert(!stats.HasAThousandFaces, "Stats no Thousand Faces at L10");
        Assert(stats.DruidNatureSenseBonus == 2, "Stats DruidNatureSenseBonus == 2");
    }

    private static void TestDruidAnimalCompanionLevel()
    {
        // Druid companion uses FULL druid level
        Assert(DruidClass.GetEffectiveDruidLevel(5) == 5, "Druid effective level 5 at L5");
        Assert(DruidClass.GetEffectiveDruidLevel(10) == 10, "Druid effective level 10 at L10");
        Assert(DruidClass.GetEffectiveDruidLevel(20) == 20, "Druid effective level 20 at L20");

        // Compare with Ranger's level - 3
        Assert(RangerClass.GetEffectiveDruidLevel(10) == 7, "Ranger effective druid level 7 at L10");
        Assert(DruidClass.GetEffectiveDruidLevel(10) > RangerClass.GetEffectiveDruidLevel(10),
            "Druid companion level > Ranger companion level at same character level");
    }

    // ==================== DRUID ARMOR RESTRICTION ====================

    private static void TestDruidMetalArmorCheck()
    {
        Assert(!DruidClass.IsMetalArmor("Leather Armor"), "Leather is not metal");
        Assert(!DruidClass.IsMetalArmor("Hide Armor"), "Hide is not metal");
        Assert(!DruidClass.IsMetalArmor("Padded Armor"), "Padded is not metal");
        Assert(DruidClass.IsMetalArmor("Chain Shirt"), "Chain shirt is metal");
        Assert(DruidClass.IsMetalArmor("Scale Mail"), "Scale mail is metal");
        Assert(DruidClass.IsMetalArmor("Breastplate"), "Breastplate is metal");
        Assert(DruidClass.IsMetalArmor("Full Plate"), "Full plate is metal");
    }

    // ==================== REGISTRY TOTAL ====================

    private static void TestAllElevenClassesRegistered()
    {
        var all = ClassRegistry.GetAllClasses();
        Assert(all.Count == 11, $"All 11 PHB classes registered (got {all.Count})");

        string[] expected = { "Fighter", "Rogue", "Monk", "Barbarian", "Wizard", "Cleric", "Sorcerer", "Ranger", "Paladin", "Bard", "Druid" };
        foreach (string name in expected)
        {
            Assert(ClassRegistry.GetClass(name) != null, $"ClassRegistry contains {name}");
        }
    }
}
}
