using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// CHR-001, CHR-002, CHR-071: class BAB and hit die come from the class definitions, hit points add the CON modifier
/// once per Hit Die with a minimum of 1 per die, and Hit Dice follow every level-up.
/// Rules (checked 2026-10-08): PHB p.9 (CON modifier on each Hit Die, at least 1 hit point per die), p.22 Table 3-1
/// (BAB good = level, average = 3/4, poor = 1/2), p.23 (maximum hit points at 1st level), p.59 (a multiclass character
/// adds its classes' BAB and gains the new class's Hit Die), p.309 (HD = character level for a character). Class
/// tables: barbarian d12 good (p.24-25), bard d6 average (p.27-28), cleric d8 average (p.31), druid d8 average (p.34-35),
/// fighter d10 good (p.38-39), monk d8 average (p.40), paladin d10 good (p.43-44), ranger d8 good (p.46-47), rogue d6
/// average (p.49-50), sorcerer d4 poor (p.51-54), wizard d4 poor (p.55-56); DMG p.108-110: adept d6 poor, aristocrat d8
/// average, commoner d4 poor, expert d6 average, warrior d8 good.
/// Builds CharacterStats and a bare CharacterController only, so it runs in edit or Play mode. Level-up hit points use
/// GameSettings' Average mode for the duration of the suite and restore the previous mode; in edit mode a GameSettings
/// object the suite had to create is destroyed again so the open scene is left as it was.
/// </summary>
public static class ClassProgressionTests
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

        Debug.Log("====== CLASS PROGRESSION TESTS (CHR-001, CHR-002, CHR-071) ======");

        // GameSettings.Instance creates a GameSettings object when the scene has none; in edit mode that object would
        // stay in the open scene and dirty it, so remove it again afterwards.
        bool hadSettings = Object.FindAnyObjectByType<GameSettings>() != null;
        GameSettings settings = GameSettings.Instance;
        HPCalculationMode previousMode = settings.hpCalculationMode;
        settings.hpCalculationMode = HPCalculationMode.Average;
        try
        {
            TestClassTables();
            TestMulticlassBab();
            TestConstructorConstitution();
            TestCreatureMaxHitPoints();
            TestCreationHitPoints();
            TestQuickStartPresets();
            TestLevelUpHitDieAndHitDice();
            TestLevelUpMinimumOneHitPoint();
            TestLevelUpPreview();
            TestCreatureHitDiceWin();
        }
        finally
        {
            settings.hpCalculationMode = previousMode;
            if (!Application.isPlaying && !hadSettings && settings != null)
                Object.DestroyImmediate(settings.gameObject);
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

    private static CharacterStats Build(string cls, int level, int con, int baseHitDieHp, string race = "Human")
    {
        return new CharacterStats(
            name: cls + " " + level, level: level, characterClass: cls,
            str: 10, dex: 10, con: con, wis: 10, intelligence: 10, cha: 10,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: baseHitDieHp, raceName: race);
    }

    // ── PHB and DMG class tables ───────────────────────────────────────

    private static void TestClassTables()
    {
        Debug.Log("--- Class hit die and BAB (PHB ch.3, DMG p.108-110; CHR-002) ---");
        // name, hit die, BAB at 4th, BAB at 20th
        var rows = new (string cls, int hd, int bab4, int bab20)[]
        {
            ("Barbarian", 12, 4, 20), ("Bard", 6, 3, 15), ("Cleric", 8, 3, 15), ("Druid", 8, 3, 15),
            ("Fighter", 10, 4, 20), ("Monk", 8, 3, 15), ("Paladin", 10, 4, 20), ("Ranger", 8, 4, 20),
            ("Rogue", 6, 3, 15), ("Sorcerer", 4, 2, 10), ("Wizard", 4, 2, 10),
            ("Adept", 6, 2, 10), ("Aristocrat", 8, 3, 15), ("Commoner", 4, 2, 10), ("Expert", 6, 3, 15), ("Warrior", 8, 4, 20),
        };
        foreach (var r in rows)
        {
            Assert(ClassProgression.GetHitDie(r.cls) == r.hd, $"{r.cls} hit die d{r.hd}", $"got d{ClassProgression.GetHitDie(r.cls)}");
            Assert(ClassProgression.GetClassBaseAttackBonus(r.cls, 4) == r.bab4, $"{r.cls} 4 BAB +{r.bab4}", $"got {ClassProgression.GetClassBaseAttackBonus(r.cls, 4)}");
            Assert(ClassProgression.GetClassBaseAttackBonus(r.cls, 20) == r.bab20, $"{r.cls} 20 BAB +{r.bab20}", $"got {ClassProgression.GetClassBaseAttackBonus(r.cls, 20)}");
            CharacterStats s = Build(r.cls, 4, 10, r.hd * 4);
            Assert(s.BaseAttackBonus == r.bab4, $"{r.cls} 4 CharacterStats.BaseAttackBonus +{r.bab4}", $"got {s.BaseAttackBonus}");
        }
        Assert(ClassProgression.GetClassBaseAttackBonus("bard", 4) == 3, "Class lookups ignore case");
    }

    private static void TestMulticlassBab()
    {
        Debug.Log("--- Multiclass BAB adds the classes (PHB p.59) ---");
        CharacterStats s = Build("Rogue", 3, 10, 18);
        s.ClassLevels.Add(new ClassLevelEntry("Bard", 3));
        s.InvalidateClassLevelCache();
        Assert(s.BaseAttackBonus == 4, "Rogue 3 / Bard 3 BAB +4 (2 + 2)", $"got {s.BaseAttackBonus}");

        CharacterStats f = Build("Fighter", 1, 10, 10);
        f.ClassLevels.Add(new ClassLevelEntry("Wizard", 2));
        f.InvalidateClassLevelCache();
        Assert(f.BaseAttackBonus == 2, "Fighter 1 / Wizard 2 BAB +2 (1 + 1)", $"got {f.BaseAttackBonus}");
    }

    // ── Hit points ──────────────────────────────────────────────────────

    private static void TestConstructorConstitution()
    {
        Debug.Log("--- The constructor adds CON once per Hit Die, minimum 1 per die (PHB p.9; CHR-001) ---");
        Assert(Build("Fighter", 3, 14, 22).MaxHP == 28, "CON 14: 22 + 2 x 3 = 28", $"got {Build("Fighter", 3, 14, 22).MaxHP}");
        Assert(Build("Fighter", 3, 10, 22).MaxHP == 22, "CON 10 adds nothing (was +1 per level)", $"got {Build("Fighter", 3, 10, 22).MaxHP}");
        Assert(Build("Wizard", 2, 8, 7).MaxHP == 5, "CON 8: 7 - 1 x 2 = 5", $"got {Build("Wizard", 2, 8, 7).MaxHP}");
        Assert(Build("Wizard", 2, 3, 4).MaxHP == 2, "CON 3: at least 1 per Hit Die", $"got {Build("Wizard", 2, 3, 4).MaxHP}");
        CharacterStats noCon = Build("Fighter", 2, CharacterStats.NO_SCORE, 13, race: null);
        Assert(noCon.MaxHP == 13, "No CON score adds nothing (was +1 per level)", $"got {noCon.MaxHP}");
        // 15 MM undead and construct entries store the missing CON score as 0 (CRE-044); it must not count as -5.
        CharacterStats conZero = Build("Warrior", 2, 0, 13, race: null);
        Assert(conZero.MaxHP == 13, "CON 0 (undead data) adds nothing, not -5 per Hit Die", $"got {conZero.MaxHP}");
        Assert(ClassProgression.HitPointConstitutionModifier(0) == 0 && ClassProgression.HitPointConstitutionModifier(CharacterStats.NO_SCORE) == 0
            && ClassProgression.HitPointConstitutionModifier(15) == 2 && ClassProgression.HitPointConstitutionModifier(7) == -2,
            "Hit point CON modifier: 0 for no score or 0, the ability modifier otherwise");
    }

    private static void TestCreatureMaxHitPoints()
    {
        Debug.Log("--- Creature max HP: the definition total is final, CON not added again (PHB p.9; CHR-001, CRE-041) ---");
        Assert(ClassProgression.CreatureMaxHitPoints(29, 8, 4, 15) == 29, "Ogre total 29 (4d8+11, MM p.199) stays 29", $"got {ClassProgression.CreatureMaxHitPoints(29, 8, 4, 15)}");
        Assert(ClassProgression.CreatureMaxHitPoints(13, 12, 2, 0) == 13, "Ghoul total 13 with CON 0 stays 13", $"got {ClassProgression.CreatureMaxHitPoints(13, 12, 2, 0)}");
        Assert(ClassProgression.CreatureMaxHitPoints(0, 8, 3, 14) == 18, "No total: 3 x (4 + 2) = 18", $"got {ClassProgression.CreatureMaxHitPoints(0, 8, 3, 14)}");
        Assert(ClassProgression.CreatureMaxHitPoints(0, 8, 3, 1) == 3, "No total, CON 1: at least 1 per Hit Die", $"got {ClassProgression.CreatureMaxHitPoints(0, 8, 3, 1)}");
        Assert(ClassProgression.CreatureMaxHitPoints(0, 12, 2, CharacterStats.NO_SCORE) == 12, "No total, no CON: 2 x 6 = 12", $"got {ClassProgression.CreatureMaxHitPoints(0, 12, 2, CharacterStats.NO_SCORE)}");

        // A 1-HD humanoid given class levels exchanges its Hit Die for them (MM p.290; CRE-004): max(1, die + CON) per
        // class die, CON counted once, and the orc's own 1d8 gone.
        NPCDefinition orc = NPCDatabase.Get("orc_warrior");
        if (orc == null)
        {
            Assert(false, "orc_warrior definition exists");
            return;
        }
        NPCDefinition barbarianOrc = orc.Clone();
        int templateTotal = barbarianOrc.BaseHitDieHP;
        int conMod = ClassProgression.HitPointConstitutionModifier(barbarianOrc.CON);
        CreatureClassEngine.ApplyClassToDefinition(barbarianOrc, ClassRegistry.GetClass("Barbarian"), 2);
        int expected = ClassProgression.HitPointsForHitDie(12, conMod) + ClassProgression.HitPointsForHitDie(6, conMod);
        int got = ClassProgression.CreatureMaxHitPoints(barbarianOrc.BaseHitDieHP, 8, barbarianOrc.HitDice, barbarianOrc.CON);
        Assert(got == expected && barbarianOrc.HitDice == 2, $"Orc barbarian 2: 2 HD, (12 + {conMod}) + (6 + {conMod}) = {expected}", $"got {got}, HD {barbarianOrc.HitDice}");
        Assert(orc.BaseHitDieHP == templateTotal && orc.HitDice == 1, "The orc template is not changed by the clone", $"template {orc.BaseHitDieHP}");

        // A monster with racial HD keeps them and adds the class (MM p.290): ogre 29 + barbarian 1 (12 + CON 2).
        // The hit points are current behaviour, not RAW (CRE-041): the barbarian die is maximized although the ogre's
        // racial HD came first; RAW gives an average die. Update this check when CRE-041 is fixed.
        NPCDefinition ogre = NPCDatabase.Get("ogre");
        if (ogre != null)
        {
            NPCDefinition barbarianOgre = ogre.Clone();
            CreatureClassEngine.ApplyClassToDefinition(barbarianOgre, ClassRegistry.GetClass("Barbarian"), 1);
            Assert(barbarianOgre.HitDice == 5 && barbarianOgre.BaseHitDieHP == 29 + ClassProgression.HitPointsForHitDie(12, 2),
                "Ogre barbarian 1: 5 HD (4d8 + 1d12, MM p.290), 29 + 14 hit points (current behaviour, CRE-041: max first class die; RAW is an average die)", $"HD {barbarianOgre.HitDice}, HP {barbarianOgre.BaseHitDieHP}");
        }
    }

    private static CharacterCreationData CreationData(string cls, string race, int con, int level)
    {
        var d = new CharacterCreationData
        {
            ClassName = cls,
            RaceName = race,
            Race = RaceDatabase.GetRace(race),
            STR = 10, DEX = 10, CON = con, INT = 10, WIS = 10, CHA = 10,
            CharacterLevel = level,
            TargetLevel = level,
        };
        d.ComputeFinalStats();
        return d;
    }

    private static CharacterStats BuildFromCreation(CharacterCreationData d)
    {
        return new CharacterStats(
            name: "Created", level: d.CharacterLevel, characterClass: d.ClassName,
            str: d.STR, dex: d.DEX, con: d.CON, wis: d.WIS, intelligence: d.INT, cha: d.CHA,
            bab: d.BAB, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: d.BaseSpeed, atkRange: 1, baseHitDieHP: d.BaseHitDieHP, raceName: d.RaceName);
    }

    private static void TestCreationHitPoints()
    {
        Debug.Log("--- Creation hit points: maximum die at 1st level, CON once (PHB p.23; CHR-001) ---");
        var cases = new (string cls, string race, int con, int level, int hp, string what)[]
        {
            ("Rogue", "Human", 10, 1, 6, "Rogue 1, CON 10: d6 = 6"),
            ("Fighter", "Human", 14, 1, 12, "Fighter 1, CON 14: 10 + 2 = 12 (was 14)"),
            ("Wizard", "Elf", 10, 1, 3, "Elf wizard 1, CON 10 - 2: 4 - 1 = 3"),
            ("Wizard", "Human", 3, 1, 1, "Wizard 1, CON 3: at least 1"),
            ("Bard", "Human", 12, 3, 17, "Bard 3, CON 12: 7 + 5 + 5 = 17 (was 20 with CON twice)"),
        };
        foreach (var c in cases)
        {
            CharacterCreationData d = CreationData(c.cls, c.race, c.con, c.level);
            Assert(d.HP == c.hp, c.what + " (creation data)", $"got {d.HP}");
            CharacterStats s = BuildFromCreation(d);
            Assert(s.MaxHP == c.hp, c.what + " (built character)", $"got {s.MaxHP}");
        }
    }

    private static void TestQuickStartPresets()
    {
        Debug.Log("--- Quick Start presets at 3rd level: HP and BAB (CHR-001, CHR-002) ---");
        var presets = new (string cls, CharacterCreationData data, int hp, int bab)[]
        {
            ("Fighter", FighterClass.GetQuickStartCharacter(), 31, 3),     // Dwarf, CON 16: 13 + 9 + 9
            ("Rogue", RogueClass.GetQuickStartCharacter(), 14, 2),         // Elf, CON 10: 6 + 4 + 4
            ("Cleric", ClericClass.GetQuickStartCharacter(), 24, 2),       // Human, CON 14: 10 + 7 + 7
            ("Wizard", WizardClass.GetQuickStartCharacter(), 10, 1),       // Elf, CON 10: 4 + 3 + 3
            ("Monk", MonkClass.GetQuickStartCharacter(), 21, 2),           // Human, CON 12: 9 + 6 + 6
            ("Barbarian", BarbarianClass.GetQuickStartCharacter(), 35, 3), // Half-Orc, CON 16: 15 + 10 + 10
            ("Sorcerer", SorcererClass.GetQuickStartCharacter(), 13, 1),   // Human, CON 12: 5 + 4 + 4
            ("Ranger", RangerClass.GetQuickStartCharacter(), 21, 3),       // Human, CON 12: 9 + 6 + 6
            ("Paladin", PaladinClass.GetQuickStartCharacter(), 28, 3),     // Human, CON 14: 12 + 8 + 8
            ("Bard", BardClass.GetQuickStartCharacter(), 17, 2),           // Half-Elf, CON 12: 7 + 5 + 5
            ("Druid", DruidClass.GetQuickStartCharacter(), 24, 2),         // Human, CON 14: 10 + 7 + 7
        };
        foreach (var p in presets)
        {
            CharacterCreationData d = p.data;
            d.ComputeFinalStats();
            Assert(d.CharacterLevel == 3, $"Quick Start {p.cls} is built at 3rd level", $"got {d.CharacterLevel}");
            Assert(d.HP == p.hp, $"Quick Start {p.cls} HP {p.hp}", $"got {d.HP}");
            Assert(d.BAB == p.bab, $"Quick Start {p.cls} BAB +{p.bab}", $"got {d.BAB}");
            CharacterStats s = BuildFromCreation(d);
            Assert(s.MaxHP == p.hp, $"Quick Start {p.cls} built with {p.hp} HP (CON once)", $"got {s.MaxHP}");
            Assert(s.BaseAttackBonus == p.bab, $"Quick Start {p.cls} built with BAB +{p.bab}", $"got {s.BaseAttackBonus}");
            Assert(s.GetHitDice() == 3, $"Quick Start {p.cls} has 3 HD", $"got {s.GetHitDice()}");
        }
    }

    // ── Level-ups ───────────────────────────────────────────────────────

    private static void TestLevelUpHitDieAndHitDice()
    {
        Debug.Log("--- Level-ups use the class hit die and add a Hit Die (PHB p.23, p.59, p.309; CHR-002, CHR-071) ---");
        // Human, CON 12 (+1); Average mode: d6 4, d8 5, d4 3.
        CharacterStats s = Build("Rogue", 1, 12, 6);
        Assert(s.MaxHP == 7 && s.GetHitDice() == 1, "Rogue 1: 7 HP, 1 HD", $"got {s.MaxHP} HP, {s.GetHitDice()} HD");
        s.PendingLevelUps = 3;
        s.EnsureMulticlassDataInitialized();
        Assert(s.GetHitDice() == 1, "Pending level-ups add no Hit Die", $"got {s.GetHitDice()}");

        s.ApplyPendingLevelUp("Rogue");
        Assert(s.LastLevelUpHPGain == 5, "Rogue level-up: d6 average 4 + 1 (was a d8)", $"got {s.LastLevelUpHPGain}");
        Assert(s.HitDice == 2 && s.GetHitDice() == 2, "Rogue 2: 2 HD", $"got field {s.HitDice}, GetHitDice {s.GetHitDice()}");
        Assert(s.BaseAttackBonus == 1, "Rogue 2 BAB +1", $"got {s.BaseAttackBonus}");

        s.ApplyPendingLevelUp("Ranger");
        Assert(s.LastLevelUpHPGain == 6, "Ranger level-up: d8 average 5 + 1 (was a d10)", $"got {s.LastLevelUpHPGain}");
        Assert(s.GetHitDice() == 3, "Rogue 2 / Ranger 1: 3 HD", $"got {s.GetHitDice()}");
        Assert(s.BaseAttackBonus == 2, "Rogue 2 / Ranger 1 BAB +2 (1 + 1)", $"got {s.BaseAttackBonus}");

        s.ApplyPendingLevelUp("Bard");
        Assert(s.LastLevelUpHPGain == 5, "Bard level-up: d6 average 4 + 1 (was a d8)", $"got {s.LastLevelUpHPGain}");
        Assert(s.GetHitDice() == 4 && s.Level == 4, "Rogue 2 / Ranger 1 / Bard 1: 4 HD, level 4", $"got {s.GetHitDice()} HD, level {s.Level}");
        Assert(s.MaxHP == 7 + 5 + 6 + 5, "Max HP 23 after three level-ups", $"got {s.MaxHP}");

        var go = new GameObject("ClassProgressionTests_Target");
        try
        {
            CharacterController c = go.AddComponent<CharacterController>();
            c.Stats = s;
            Assert(TeamUtility.GetHitDice(c) == 4, "TeamUtility.GetHitDice sees the levelled HD (Sleep, Daze, Color Spray)", $"got {TeamUtility.GetHitDice(c)}");
            Assert(SpellTargetingService.IsWithinHDLimit(c, 4) && !SpellTargetingService.IsWithinHDLimit(c, 3), "HD limits use the levelled HD");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestLevelUpMinimumOneHitPoint()
    {
        Debug.Log("--- A level-up gives at least 1 hit point (PHB p.9) ---");
        CharacterStats s = Build("Wizard", 1, 3, 4); // CON 3 (-4): 4 - 4 -> 1
        Assert(s.MaxHP == 1, "Wizard 1, CON 3: 1 HP", $"got {s.MaxHP}");
        s.PendingLevelUps = 1;
        s.EnsureMulticlassDataInitialized();
        s.ApplyPendingLevelUp("Wizard");
        Assert(s.LastLevelUpHPGain == 1, "d4 average 3 - 4 still gives 1", $"got {s.LastLevelUpHPGain}");
        ClassHitPointEntry e = s.HitPointGainsByClassLevel.Count > 0 ? s.HitPointGainsByClassLevel[s.HitPointGainsByClassLevel.Count - 1] : null;
        Assert(e != null && e.HitDie == 4 && e.Roll == 3 && e.ConstitutionBonus == -4 && e.TotalGain == 1,
            "The level-up record keeps the die result, the CON modifier and the clamped gain",
            e == null ? "no record" : $"d{e.HitDie} roll {e.Roll} CON {e.ConstitutionBonus} total {e.TotalGain}");
    }

    private static void TestLevelUpPreview()
    {
        Debug.Log("--- The level-up preview uses the same class data (CHR-002) ---");
        var go = new GameObject("ClassProgressionTests_Preview");
        try
        {
            CharacterController c = go.AddComponent<CharacterController>();
            CharacterStats s = Build("Bard", 1, 12, 6);
            s.PendingLevelUps = 1;
            s.EnsureMulticlassDataInitialized();
            c.Stats = s;
            LevelUpData data = LevelUpCalculator.CalculateLevelUp(c, 1, 2);
            Assert(data.HPGained == 5, "Bard 1 -> 2 preview: d6 average 4 + 1", $"got {data.HPGained}");
            Assert(data.NewBAB == 1, "Bard 1 -> 2 preview: BAB +1 (3/4)", $"got {data.NewBAB}");
            LevelUpCalculator.RecalculateForSelectedClass(data, "Ranger");
            Assert(data.HPGained == 6 && data.NewBAB == 1, "Bard 1 + Ranger 1 preview: d8 average 5 + 1, BAB 0 + 1",
                $"got {data.HPGained} HP, BAB {data.NewBAB}");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestCreatureHitDiceWin()
    {
        Debug.Log("--- A creature's definition HD win over its class entries (PHB p.309; CHR-071) ---");
        CharacterStats s = Build("Warrior", 1, 10, 6, race: null);
        s.HitDice = 6;
        Assert(s.GetHitDice() == 6, "HitDice 6 with one stand-in class level: 6 HD", $"got {s.GetHitDice()}");
        var dragon = Build("Fighter", 7, 10, 40, race: null);
        dragon.HitDice = 7;
        dragon.ClassLevels.Add(new ClassLevelEntry("Sorcerer", 3));
        dragon.EnsureMulticlassDataInitialized();
        Assert(dragon.GetHitDice() == 7 && dragon.Level == 10, "7 HD with an injected sorcerer caster level: 7 HD, Level 10",
            $"got {dragon.GetHitDice()} HD, level {dragon.Level}");
    }
}
}
