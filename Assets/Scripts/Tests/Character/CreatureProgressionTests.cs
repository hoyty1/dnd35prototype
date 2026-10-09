using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// CRE-004: a creature's base attack bonus and base saves are the sum of its racial Hit Dice, by its creature type's
/// progressions (MM Table 4-1, p.290, and the type entries p.305-317), and its class levels, by the class tables
/// (PHB Table 3-1 p.22, the class chapters; DMG p.107-110 for NPC classes), added as multiclass values are
/// (PHB p.59, MM p.290). A humanoid with 1 HD or less exchanges its Hit Die for its class levels (MM p.290, p.310).
/// The same CharacterStats formulas serve PCs and NPCs (docs/systems/PC_NPC_PARITY.md).
///
/// MM figures checked (base values; the totals add the ability modifiers of the game's data where it matches the MM):
/// orc p.203 (BAB +1, warrior 1), goblin p.133 (BAB +1, Fort +3, Ref +1, Will -1), hobgoblin p.153 (BAB +1, Fort +4,
/// Ref +1, Will -1), kobold p.161 (BAB +1, Fort +2, Ref +1, Will -1), gnoll p.130 (2 HD, BAB +1, Fort +4, Ref +0,
/// Will +0), bugbear p.29 (3 HD, BAB +2, Fort +2, Ref +4, Will +1), lizardfolk p.169 (2 HD, BAB +1, Fort +1, Ref +3,
/// Will +0), troglodyte p.246 (2 HD, BAB +1, base Fort +3), ogre p.199 (4 HD, BAB +3, Fort +6, Ref +0, Will +1) and
/// the ogre barbarian 4 of the same page (8 HD, BAB +7, Fort +12, Ref +2, Will +2 at Str 26, Dex 11, Con 18, Wis 10),
/// human warrior skeleton p.226 (BAB +0, Fort +0, Ref +1, Will +2), the ghost p.117 and vampire p.250 samples (human
/// fighter 5 under the template: 5 HD, BAB +5, base saves +4/+1/+1), dire lion p.63 (8 HD, BAB +6, Fort +9, Ref +8,
/// Will +7) and dire wolf p.65 (6 HD, BAB +4, Fort +8, Ref +7, Will +6); dire animals have a good Will save.
/// DMG p.97 rows "ghost/vampire, 5th-level human fighter" (CR 7) name those samples and add no levels.
/// The pure formula tests need no scene; the spawn tests go through GameManager.InitializeNPCFromDefinition and need
/// Play mode (a scene GameManager). Spawned controllers sit off the grid and are destroyed afterwards.
/// </summary>
public static class CreatureProgressionTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int SpawnPos = new Vector2Int(-55, -55);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        NPCDatabase.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();
        ClassRegistry.Init();

        Debug.Log("====== CREATURE PROGRESSION TESTS (CRE-004) ======");

        TestFormulas();
        TestRacialHitDiceResolution();
        TestApplyClassReplacesHumanoidHitDie();
        TestApplyClassStacksOnRacialHitDice();
        TestApplyToStatsOutsideTheSpawn();

        if (GameManager.Instance == null)
        {
            Assert(false, "Creature progression spawn tests need Play mode with a scene GameManager");
        }
        else
        {
            TestOneHitDieHumanoids();
            TestHumanoidsWithRacialHitDice();
            TestOgre();
            TestOgreBarbarian4();
            TestGoblinAdept();
            TestHobgoblinWarrior3();
            TestGnollFighter2();
            TestClassedNpcsFromData();
            TestTemplates();
            TestNpcMatchesPc();
        }

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

    // ── Helpers ─────────────────────────────────────────────────────────

    private static NPCDefinition Template(string id)
    {
        NPCDefinition def = NPCDatabase.Get(id);
        Assert(def != null, id + " is in NPCDatabase");
        return def;
    }

    private static CharacterController Spawn(NPCDefinition def, string label)
    {
        var go = new GameObject("CreatureProgressionTest_" + label);
        CharacterController cc = go.AddComponent<CharacterController>();
        GameManager.Instance.InitializeNPCFromDefinition(cc, def, SpawnPos, null, null);
        return cc;
    }

    private static void Cleanup(CharacterController cc)
    {
        if (cc == null)
            return;
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.Grid != null)
            gm.Grid.ClearCreatureOccupancy(cc);
        Object.DestroyImmediate(cc.gameObject);
    }

    private static string Fmt(CharacterStats s)
    {
        return $"HD {s.GetHitDice()} (racial {s.RacialHitDice}), BAB {CharacterStats.FormatMod(s.BaseAttackBonus)}, "
            + $"base F/R/W {CharacterStats.FormatMod(s.ClassFortSave)}/{CharacterStats.FormatMod(s.ClassRefSave)}/{CharacterStats.FormatMod(s.ClassWillSave)}, "
            + $"saves {CharacterStats.FormatMod(s.FortitudeSave)}/{CharacterStats.FormatMod(s.ReflexSave)}/{CharacterStats.FormatMod(s.WillSave)}";
    }

    /// <summary>Checks HD, BAB and base saves of <paramref name="s"/>.</summary>
    private static void CheckBase(CharacterStats s, string what, int hd, int bab, int fort, int reflex, int will)
    {
        bool ok = s.GetHitDice() == hd && s.BaseAttackBonus == bab
            && s.ClassFortSave == fort && s.ClassRefSave == reflex && s.ClassWillSave == will;
        Assert(ok, $"{what}: HD {hd}, BAB {CharacterStats.FormatMod(bab)}, base F/R/W {CharacterStats.FormatMod(fort)}/{CharacterStats.FormatMod(reflex)}/{CharacterStats.FormatMod(will)}",
            "got " + Fmt(s));
    }

    /// <summary>Checks the total saves (base + ability + everything else) of <paramref name="s"/>.</summary>
    private static void CheckTotals(CharacterStats s, string what, int fort, int reflex, int will)
    {
        bool ok = s.FortitudeSave == fort && s.ReflexSave == reflex && s.WillSave == will;
        Assert(ok, $"{what}: saves Fort {CharacterStats.FormatMod(fort)}, Ref {CharacterStats.FormatMod(reflex)}, Will {CharacterStats.FormatMod(will)}",
            "got " + Fmt(s));
    }

    private static void SpawnAndCheck(NPCDefinition def, string what, int hd, int bab, int fort, int reflex, int will,
        int? totalFort = null, int? totalRef = null, int? totalWill = null)
    {
        if (def == null)
            return;
        CharacterController cc = Spawn(def, def.Id ?? what);
        try
        {
            CheckBase(cc.Stats, what, hd, bab, fort, reflex, will);
            if (totalFort.HasValue)
                CheckTotals(cc.Stats, what, totalFort.Value, totalRef.Value, totalWill.Value);
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void SpawnTemplateAndCheck(string id, string what, int hd, int bab, int fort, int reflex, int will,
        int? totalFort = null, int? totalRef = null, int? totalWill = null)
    {
        NPCDefinition template = Template(id);
        if (template == null)
            return;
        SpawnAndCheck(template.Clone(), what, hd, bab, fort, reflex, will, totalFort, totalRef, totalWill);
    }

    // ── Pure formulas ───────────────────────────────────────────────────

    private static void TestFormulas()
    {
        Debug.Log("--- Formulas: racial progressions give 0 without racial HD; class saves by the class tables ---");
        Assert(ProgressionCalculator.CalculateRacialBAB(BABProgression.Good, 0) == 0
               && ProgressionCalculator.CalculateRacialSave(SaveProgression.Good, 0) == 0,
            "No racial HD: racial BAB and a good racial save are 0, not the 1st-HD values");
        Assert(ProgressionCalculator.CalculateRacialBAB(BABProgression.Medium, 4) == 3
               && ProgressionCalculator.CalculateRacialSave(SaveProgression.Good, 4) == 4
               && ProgressionCalculator.CalculateRacialSave(SaveProgression.Poor, 4) == 1,
            "4 giant HD: BAB +3, good Fort +4, poor +1 (MM p.199, Table 4-1)");
        Assert(ClassProgression.BaseSaveForProgression(true, 1) == 2 && ClassProgression.BaseSaveForProgression(false, 1) == 0
               && ClassProgression.BaseSaveForProgression(true, 0) == 0 && ClassProgression.BaseSaveForProgression(false, 9) == 3,
            "Class base saves: good 2 + L/2, poor L/3, 0 without levels (PHB Table 3-1)");
        Assert(ClassProgression.GetClassBaseSave("Warrior", 1, SavingThrowType.Fortitude) == 2
               && ClassProgression.GetClassBaseSave("Warrior", 1, SavingThrowType.Will) == 0
               && ClassProgression.GetClassBaseSave("Adept", 2, SavingThrowType.Will) == 3
               && ClassProgression.GetClassBaseSave("Barbarian", 4, SavingThrowType.Fortitude) == 4,
            "Warrior 1 Fort +2 Will +0, adept 2 Will +3, barbarian 4 Fort +4 (DMG p.108-109, PHB p.25)");
        Assert(CreatureClassEngine.CalculateTotalSave(4, true, 4) == 8,
            "Racial and class good saves stack: ogre 4 + barbarian 4 Fort base +8 (MM p.199 ogre barbarian)");

        CreatureTypeProgression humanoid = CreatureTypeProgressionDatabase.Get(CreatureTypeId.Humanoid);
        Assert(humanoid.Reflex == SaveProgression.Good && humanoid.Fortitude == SaveProgression.Poor && humanoid.Will == SaveProgression.Poor,
            "Humanoid racial HD: good Reflex by default (MM p.310, 'usually')");
    }

    private static void TestRacialHitDiceResolution()
    {
        Debug.Log("--- Racial Hit Dice of a definition ---");
        NPCDefinition orc = Template("orc_warrior");
        NPCDefinition gnoll = Template("gnoll");
        NPCDefinition ogre = Template("ogre");
        NPCDefinition paladin = Template("human_paladin");
        NPCDefinition djinni = Template("noble_djinni");
        NPCDefinition skeleton = Template("skeleton_human_warrior");
        if (orc != null)
            Assert(orc.ResolveRacialHitDice() == 0, "orc warrior: its Hit Die is a warrior level, no racial HD (MM p.310)", $"got {orc.ResolveRacialHitDice()}");
        if (gnoll != null)
            Assert(gnoll.ResolveRacialHitDice() == 2, "gnoll: 2 racial HD (MM p.130)", $"got {gnoll.ResolveRacialHitDice()}");
        if (ogre != null)
            Assert(ogre.ResolveRacialHitDice() == 4, "ogre: 4 racial HD behind its Warrior stand-in (CRE-024)", $"got {ogre.ResolveRacialHitDice()}");
        if (paladin != null)
            Assert(paladin.ResolveRacialHitDice() == 0, "human paladin: all class levels", $"got {paladin.ResolveRacialHitDice()}");
        if (djinni != null)
            Assert(djinni.ResolveClassLevelsAreRacialHitDice() && djinni.ResolveRacialHitDice() == 7,
                "noble djinni: the unregistered class name 'Outsider' stands in for 7 racial HD", $"got {djinni.ResolveRacialHitDice()}");
        if (skeleton != null)
            Assert(skeleton.ResolveRacialHitDice() == 1, "human warrior skeleton: the template drops the class, 1 undead HD (MM p.226)", $"got {skeleton.ResolveRacialHitDice()}");
    }

    private static void TestApplyClassReplacesHumanoidHitDie()
    {
        Debug.Log("--- A 1-HD humanoid exchanges its Hit Die for class levels (MM p.290) ---");
        NPCDefinition goblin = Template("goblin");
        if (goblin == null)
            return;
        int templateHp = goblin.BaseHitDieHP;
        int? templateOverride = goblin.BaseAttackBonusOverride;
        NPCDefinition def = goblin.Clone();
        Assert(CreatureClassEngine.ClassLevelsReplaceHitDice(def), "goblin (1 HD humanoid) has its Hit Die replaced");
        CreatureClassEngine.ApplyClassToDefinition(def, ClassRegistry.GetClass("Adept"), 2);
        int conMod = ClassProgression.HitPointConstitutionModifier(def.CON);
        Assert(def.HitDice == 2 && def.Level == 2 && def.CharacterClass == "Adept",
            "goblin adept 2: 2 HD, all adept", $"HD {def.HitDice}, {def.CharacterClass} {def.Level}");
        Assert(def.BaseHitDieHP == CreatureClassEngine.CalculateClassHP(6, conMod, 2),
            "goblin adept 2: hit points from the adept dice alone (the goblin's 1d8 is gone)", $"got {def.BaseHitDieHP}");
        Assert(!def.BaseAttackBonusOverride.HasValue && def.ResolveRacialHitDice() == 0,
            "goblin adept 2: the goblin's BAB override and racial HD are gone");
        Assert(goblin.BaseHitDieHP == templateHp && goblin.BaseAttackBonusOverride == templateOverride && goblin.HitDice == 1,
            "goblin template unchanged");

        NPCDefinition gnoll = Template("gnoll");
        if (gnoll != null)
            Assert(!CreatureClassEngine.ClassLevelsReplaceHitDice(gnoll.Clone()), "gnoll (2 HD) keeps its racial HD beside a class (MM p.290)");
        NPCDefinition ogre = Template("ogre");
        if (ogre != null)
            Assert(!CreatureClassEngine.ClassLevelsReplaceHitDice(ogre.Clone()), "ogre (giant) keeps its racial HD beside a class (MM p.290)");
    }

    private static void TestApplyClassStacksOnRacialHitDice()
    {
        Debug.Log("--- Class levels add to racial HD (MM p.290) ---");
        NPCDefinition ogre = Template("ogre");
        if (ogre == null)
            return;
        NPCDefinition def = ogre.Clone();
        CreatureClassEngine.ApplyClassToDefinition(def, ClassRegistry.GetClass("Barbarian"), 4);
        Assert(def.HitDice == 8 && def.ResolveRacialHitDice() == 4 && def.BAB == 7,
            "ogre barbarian 4: 8 HD, 4 of them racial, reference BAB +7 (MM p.199)", $"HD {def.HitDice}, racial {def.ResolveRacialHitDice()}, BAB {def.BAB}");
        Assert(ogre.HitDice == 4 && ogre.Level == 4 && ogre.CharacterClass == "Warrior", "ogre template unchanged");

        // ApplyClassToDefinition adds levels: a base with real levels in the same class keeps one class entry and
        // adds to it (QuickSpawnSystem on a classed NPC).
        NPCDefinition vampire = Template("vampire");
        if (vampire != null)
        {
            NPCDefinition v = vampire.Clone();
            CreatureClassEngine.ApplyClassToDefinition(v, ClassRegistry.GetClass("Fighter"), 2);
            Assert(v.CharacterClass == "Fighter" && v.Level == 7 && v.HitDice == 7 && v.ResolveRacialHitDice() == 0,
                "vampire (fighter 5) + 2 fighter levels: fighter 7, 7 HD, no racial HD",
                $"{v.CharacterClass} {v.Level}, HD {v.HitDice}, racial {v.ResolveRacialHitDice()}");
        }

        // A DMG row names the creature's class level: "vampire/ghost, 5th-level human fighter" (DMG p.97, CR 7) is the
        // MM sample (MM p.117, p.250), which the entries already are, so the encounter path adds no levels.
        foreach (string id in new[] { "vampire", "ghost" })
        {
            NPCDefinition sample = Template(id);
            if (sample == null)
                continue;
            NPCDefinition row = sample.Clone();
            int added = CreatureClassEngine.ApplyEncounterClassLevel(row, ClassRegistry.GetClass("Fighter"), 5);
            Assert(added == 0 && row.CharacterClass == "Fighter" && row.Level == 5 && row.HitDice == 5
                   && row.ChallengeRating == "7" && row.BaseHitDieHP == 32,
                $"DMG row \"{id}, 5th-level human fighter\": the fighter 5 sample unchanged (5 HD, 32 hp, CR 7)",
                $"added {added}, {row.CharacterClass} {row.Level}, HD {row.HitDice}, hp {row.BaseHitDieHP}, CR {row.ChallengeRating}");
            NPCDefinition seventh = sample.Clone();
            int addedTwo = CreatureClassEngine.ApplyEncounterClassLevel(seventh, ClassRegistry.GetClass("Fighter"), 7);
            Assert(addedTwo == 2 && seventh.Level == 7 && seventh.HitDice == 7,
                $"a row naming {id} fighter 7 adds the 2 missing levels", $"added {addedTwo}, level {seventh.Level}, HD {seventh.HitDice}");
            Assert(sample.Level == 5 && sample.HitDice == 5, id + " template unchanged");
        }
        NPCDefinition ogreRow = ogre.Clone();
        Assert(CreatureClassEngine.ApplyEncounterClassLevel(ogreRow, ClassRegistry.GetClass("Barbarian"), 4) == 4 && ogreRow.HitDice == 8,
            "a row on a monster base (ogre, Warrior stand-in) adds all its class levels");

        // A definition without a hit point total keeps its racial hit points when a class is added.
        NPCDefinition noTotal = ogre.Clone();
        noTotal.BaseHitDieHP = 0;
        int racial = ClassProgression.CreatureMaxHitPoints(0, 8, 4, noTotal.CON);
        CreatureClassEngine.ApplyClassToDefinition(noTotal, ClassRegistry.GetClass("Fighter"), 1);
        int conMod = ClassProgression.HitPointConstitutionModifier(noTotal.CON);
        Assert(noTotal.BaseHitDieHP == racial + CreatureClassEngine.CalculateClassHP(10, conMod, 1),
            "no hit point total: the racial average is kept and the class hit points are added", $"got {noTotal.BaseHitDieHP}, racial {racial}");
    }

    private static void TestApplyToStatsOutsideTheSpawn()
    {
        Debug.Log("--- Other stat paths: the Lion's Shield summon builds its dire lion with the same helper ---");
        NPCDefinition lion = Template("dire_lion");
        if (lion != null)
        {
            // The summon's own stats builder (LionsShieldBehavior.ActivateSummon calls it).
            CharacterStats s = LionsShieldBehavior.BuildSummonStats(lion.Clone());
            CheckBase(s, "Lion's Shield dire lion (8 animal HD behind a Warrior stand-in; MM p.63 BAB +6, good Will)", 8, 6, 6, 6, 6);
            CheckTotals(s, "Lion's Shield dire lion (MM p.63)", 9, 8, 7);
        }

        // ApplyToStats alone marks the stand-in class, so the stand-in's levels are not counted again as class levels.
        NPCDefinition ogre = Template("ogre");
        if (ogre != null)
        {
            var s = new CharacterStats(
                name: ogre.Name, level: ogre.Level, characterClass: ogre.CharacterClass,
                str: ogre.STR, dex: ogre.DEX, con: ogre.CON, wis: ogre.WIS, intelligence: ogre.INT, cha: ogre.CHA,
                bab: 0, armorBonus: 0, shieldBonus: 0, damageDice: 0, damageCount: 1, bonusDamage: 0,
                baseSpeed: ogre.BaseSpeed, atkRange: 1, baseHitDieHP: System.Math.Max(1, ogre.BaseHitDieHP));
            s.HitDice = ogre.ResolveTotalHitDice();
            CreatureTypeProgressionDatabase.ApplyToStats(s, ogre);
            Assert(s.RacialHitDiceStandInClass == "Warrior", "ApplyToStats marks the ogre's Warrior as a racial-HD stand-in",
                "got " + (s.RacialHitDiceStandInClass ?? "null"));
            CheckBase(s, "ogre through ApplyToStats alone (no proficiency call): 4 giant HD only", 4, 3, 4, 1, 1);
        }
    }

    // ── Spawned creatures against the MM ────────────────────────────────

    private static void TestOneHitDieHumanoids()
    {
        Debug.Log("--- 1-HD humanoids are 1st-level warriors (MM p.310; DMG p.109 warrior: BAB +1, good Fort) ---");
        // orc_warrior data has WIS 8 (MM orc Wis 7, p.203), so only the base values are compared for it.
        SpawnTemplateAndCheck("orc_warrior", "orc warrior (MM p.203)", 1, 1, 2, 0, 0);
        SpawnTemplateAndCheck("goblin", "goblin (MM p.133)", 1, 1, 2, 0, 0, 3, 1, -1);
        SpawnTemplateAndCheck("hobgoblin", "hobgoblin (MM p.153)", 1, 1, 2, 0, 0, 4, 1, -1);
        SpawnTemplateAndCheck("hobgoblin_warrior", "hobgoblin warrior (MM p.153)", 1, 1, 2, 0, 0);
        SpawnTemplateAndCheck("kobold_warrior", "kobold warrior (MM p.161)", 1, 1, 2, 0, 0, 2, 1, -1);
        foreach (string id in new[] { "goblin", "goblin_warrior" })
        {
            NPCDefinition g = Template(id);
            if (g != null)
                Assert(!g.BaseAttackBonusOverride.HasValue, id + ": BAB comes from its Warrior 1 level, not a fixed override");
        }
    }

    private static void TestHumanoidsWithRacialHitDice()
    {
        Debug.Log("--- Humanoids with more than 1 HD use the humanoid type: BAB 3/4, one good save (MM p.310) ---");
        SpawnTemplateAndCheck("gnoll", "gnoll (MM p.130)", 2, 1, 3, 0, 0, 4, 0, 0);
        SpawnTemplateAndCheck("bugbear", "bugbear (MM p.29)", 3, 2, 1, 3, 1, 2, 4, 1);
        SpawnTemplateAndCheck("lizardfolk", "lizardfolk (MM p.169)", 2, 1, 0, 3, 0, 1, 3, 0);
        // The troglodyte data has DEX 10 (MM Dex 9, p.246), so only the base values are compared.
        SpawnTemplateAndCheck("troglodyte", "troglodyte (MM p.246)", 2, 1, 3, 0, 0);
    }

    private static void TestOgre()
    {
        Debug.Log("--- Ogre: 4 giant HD behind the Warrior stand-in (MM p.199) ---");
        SpawnTemplateAndCheck("ogre", "ogre (MM p.199)", 4, 3, 4, 1, 1, 6, 0, 1);
    }

    private static void TestOgreBarbarian4()
    {
        Debug.Log("--- Ogre barbarian 4: racial and class values stack (MM p.199, p.290) ---");
        NPCDefinition ogre = Template("ogre");
        if (ogre == null)
            return;
        NPCDefinition def = ogre.Clone();
        CreatureClassEngine.ApplyClassToDefinition(def, ClassRegistry.GetClass("Barbarian"), 4);
        // The MM ogre barbarian's ability scores, so the totals can be compared with its stat block.
        def.STR = 26; def.DEX = 11; def.CON = 18; def.INT = 8; def.WIS = 10; def.CHA = 4;
        SpawnAndCheck(def, "ogre barbarian 4 (MM p.199)", 8, 7, 8, 2, 2, 12, 2, 2);
    }

    private static void TestGoblinAdept()
    {
        Debug.Log("--- Goblin adept: the class replaces the goblin's Hit Die (MM p.290; DMG p.108 adept) ---");
        NPCDefinition goblin = Template("goblin");
        if (goblin == null)
            return;
        NPCDefinition one = goblin.Clone();
        CreatureClassEngine.ApplyClassToDefinition(one, ClassRegistry.GetClass("Adept"), 1);
        SpawnAndCheck(one, "goblin adept 1", 1, 0, 0, 0, 2, 1, 1, 1);

        NPCDefinition two = goblin.Clone();
        CreatureClassEngine.ApplyClassToDefinition(two, ClassRegistry.GetClass("Adept"), 2);
        SpawnAndCheck(two, "goblin adept 2", 2, 1, 0, 0, 3);
    }

    private static void TestHobgoblinWarrior3()
    {
        Debug.Log("--- Hobgoblin warrior 3 (DMG p.109 warrior; MM p.153, p.290) ---");
        NPCDefinition hob = Template("hobgoblin_warrior");
        if (hob == null)
            return;
        NPCDefinition def = hob.Clone();
        CreatureClassEngine.ApplyClassToDefinition(def, ClassRegistry.GetClass("Warrior"), 3);
        SpawnAndCheck(def, "hobgoblin warrior 3", 3, 3, 3, 1, 1);
    }

    private static void TestGnollFighter2()
    {
        Debug.Log("--- Gnoll fighter 2: 2 humanoid HD plus fighter 2 (MM p.130, p.290; PHB p.38) ---");
        NPCDefinition gnoll = Template("gnoll");
        if (gnoll == null)
            return;
        NPCDefinition def = gnoll.Clone();
        CreatureClassEngine.ApplyClassToDefinition(def, ClassRegistry.GetClass("Fighter"), 2);
        // Racial: BAB +1, good Fort +3; fighter 2: BAB +2, Fort +3, Ref +0, Will +0.
        SpawnAndCheck(def, "gnoll fighter 2", 4, 3, 6, 0, 0);
    }

    private static void TestClassedNpcsFromData()
    {
        Debug.Log("--- NPCs with PHB class levels use the class tables (PHB ch.3) ---");
        SpawnTemplateAndCheck("human_paladin", "human paladin 5 (PHB p.43)", 5, 5, 4, 1, 1);
        SpawnTemplateAndCheck("human_monk_5", "human monk 5 (PHB p.40)", 5, 3, 4, 4, 4);
        SpawnTemplateAndCheck("human_cleric", "human cleric 5 (PHB p.31)", 5, 3, 4, 1, 4);
        SpawnTemplateAndCheck("lich", "lich wizard 11 (MM p.166: class BAB and saves under the template)", 11, 5, 3, 3, 7);
        // Undead have no Constitution; these entries store CON 0 (CRE-044), so only the base values are compared.
        SpawnTemplateAndCheck("vampire", "vampire, human fighter 5 (MM p.250)", 5, 5, 4, 1, 1);
        SpawnTemplateAndCheck("ghost", "ghost, human fighter 5 (MM p.117)", 5, 5, 4, 1, 1);
        NPCDefinition vampire = Template("vampire");
        if (vampire != null)
        {
            NPCDefinition row = vampire.Clone();
            CreatureClassEngine.ApplyEncounterClassLevel(row, ClassRegistry.GetClass("Fighter"), 5);
            SpawnAndCheck(row, "DMG row vampire, 5th-level human fighter (DMG p.97)", 5, 5, 4, 1, 1);
        }
        SpawnTemplateAndCheck("noble_djinni", "noble djinni (outsider 7 HD, all good)", 7, 7, 5, 5, 5);
        TestDragonCasterLevelIsNotAClass();
    }

    private static void TestDragonCasterLevelIsNotAClass()
    {
        // A dragon casts as a sorcerer of a level set by its age (MM p.68-70); the spawn injects a Sorcerer entry for
        // the caster level, which must add no BAB or saves: dragon HD give BAB = HD and good saves (MM Table 4-1).
        NPCDefinition dragon = null;
        foreach (NPCDefinition d in NPCDatabase.AllNPCs)
        {
            if (d != null && d.CreatureType == "Dragon" && d.KnownSpellIds != null && d.KnownSpellIds.Count > 0
                && !d.BaseAttackBonusOverride.HasValue && d.BABOverride == BABProgression.Good
                && d.FortitudeSaveOverride == SaveProgression.Good && d.ReflexSaveOverride == SaveProgression.Good
                && d.WillSaveOverride == SaveProgression.Good)
            {
                dragon = d;
                break;
            }
        }
        if (dragon == null)
        {
            Assert(false, "a spellcasting dragon with good progressions is in NPCDatabase");
            return;
        }
        CharacterController cc = Spawn(dragon.Clone(), dragon.Id);
        try
        {
            CharacterStats s = cc.Stats;
            int hd = s.GetHitDice();
            Assert(s.GetClassLevel("Sorcerer") > 0 && s.IsSpellcaster, dragon.Id + " casts as a sorcerer (injected caster level)",
                "sorcerer level " + s.GetClassLevel("Sorcerer"));
            CheckBase(s, dragon.Id + ": the sorcerer caster level adds no BAB or saves", hd, hd, 2 + hd / 2, 2 + hd / 2, 2 + hd / 2);
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestTemplates()
    {
        Debug.Log("--- Dire animals: animal HD with a good Will save (MM p.62-65) ---");
        SpawnTemplateAndCheck("dire_wolf", "dire wolf (MM p.65)", 6, 4, 5, 5, 5, 8, 7, 6);
        SpawnTemplateAndCheck("dire_lion", "dire lion (MM p.63)", 8, 6, 6, 6, 6, 9, 8, 7);

        Debug.Log("--- Templates: skeletons drop class levels (MM p.226) ---");
        SpawnTemplateAndCheck("skeleton_human_warrior", "human warrior skeleton (MM p.226)", 1, 0, 0, 0, 2);
    }

    private static void TestNpcMatchesPc()
    {
        Debug.Log("--- Parity: a classed NPC and a PC of the same class and level have the same BAB and base saves ---");
        NPCDefinition human = Template("human_warrior");
        if (human == null)
            return;
        NPCDefinition def = human.Clone();
        CreatureClassEngine.ApplyClassToDefinition(def, ClassRegistry.GetClass("Rogue"), 4);
        CharacterController cc = Spawn(def, "human_rogue_4");
        try
        {
            var pc = new CharacterStats(
                name: "PC Rogue", level: 4, characterClass: "Rogue",
                str: 10, dex: 10, con: 10, wis: 10, intelligence: 10, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 6, raceName: "Human");
            CharacterStats npc = cc.Stats;
            Assert(npc.BaseAttackBonus == pc.BaseAttackBonus && npc.ClassFortSave == pc.ClassFortSave
                   && npc.ClassRefSave == pc.ClassRefSave && npc.ClassWillSave == pc.ClassWillSave
                   && pc.BaseAttackBonus == 3 && pc.ClassRefSave == 4,
                "human rogue 4: NPC and PC both BAB +3, base F/R/W +1/+4/+1 (PHB p.50)",
                "NPC " + Fmt(npc) + "; PC BAB " + pc.BaseAttackBonus + ", F/R/W " + pc.ClassFortSave + "/" + pc.ClassRefSave + "/" + pc.ClassWillSave);
        }
        finally
        {
            Cleanup(cc);
        }
    }
}
}
