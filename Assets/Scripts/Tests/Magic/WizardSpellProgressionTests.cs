using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Magic
{
    /// <summary>
    /// Regression checks for wizard spell progression and multiclass prepared-caster separation.
    /// </summary>
    public static class WizardSpellProgressionTests
    {
        private static int _passed;
        private static int _failed;

        public static void wizard_spell_progression_test() => RunAll();

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== WIZARD SPELL PROGRESSION TESTS ======");

            RaceDatabase.Init();
            ClassRegistry.Init();
            SpellDatabase.Init();

            TestWizardLevel2HasNoSecondLevelSlotsEvenWithHighInt();
            TestWizardLevel3UnlocksSecondLevelSlots();
            TestMulticlassPreparedSlotsAreSeparatedPerClass();
            TestMulticlassCastingConsumesCorrectClassSlotPool();
            TestWizardPrimaryMulticlassDoesNotAutoFillWizardSpellbook();
            TestWizardPrimaryFallbackDoesNotApplyToClericPrimaryMulticlass();
            TestLearnSpellForClassStaysWithinSelectedCasterClass();
            TestWizardInitialSpellbookSelectionCountUsesIntModifier();
            TestWizardInitialSpellbookSelectionLevelDetection();

            Debug.Log($"====== Wizard Spell Progression Results: {_passed} passed, {_failed} failed ======");
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

        private static CharacterStats CreateWizardStats(string name, int wizardLevel, int intelligence)
        {
            return new CharacterStats(
                name: name,
                level: wizardLevel,
                characterClass: "Wizard",
                str: 10,
                dex: 12,
                con: 12,
                wis: 10,
                intelligence: intelligence,
                cha: 10,
                bab: Mathf.Max(0, wizardLevel / 2),
                armorBonus: 0,
                shieldBonus: 0,
                damageDice: 4,
                damageCount: 1,
                bonusDamage: 0,
                baseSpeed: 6,
                atkRange: 1,
                baseHitDieHP: Mathf.Max(4, wizardLevel * 4),
                raceName: "Human");
        }

        private static CharacterStats CreateWizardClericStats(string name)
        {
            CharacterStats stats = new CharacterStats(
                name: name,
                level: 4,
                characterClass: "Wizard",
                str: 10,
                dex: 12,
                con: 12,
                wis: 16,
                intelligence: 16,
                cha: 10,
                bab: 2,
                armorBonus: 0,
                shieldBonus: 0,
                damageDice: 4,
                damageCount: 1,
                bonusDamage: 0,
                baseSpeed: 6,
                atkRange: 1,
                baseHitDieHP: 18,
                raceName: "Human");

            stats.ClassLevels = new List<ClassLevelEntry>
            {
                new ClassLevelEntry("Wizard", 2),
                new ClassLevelEntry("Cleric", 2)
            };
            stats.Level = 4;
            stats.CharacterClass = "Wizard";
            stats.ChosenDomains = new List<string> { "Healing" };
            return stats;
        }

        private static SpellcastingComponent CreateSpellcasting(CharacterStats stats, string goName)
        {
            GameObject go = new GameObject(goName);
            SpellcastingComponent spellcasting = go.AddComponent<SpellcastingComponent>();
            spellcasting.Init(stats);
            return spellcasting;
        }

        private static void DestroySpellcasting(SpellcastingComponent spellcasting)
        {
            if (spellcasting != null)
                UnityEngine.Object.DestroyImmediate(spellcasting.gameObject);
        }

        private static void TestWizardLevel2HasNoSecondLevelSlotsEvenWithHighInt()
        {
            SpellcastingComponent sc = null;
            try
            {
                CharacterStats stats = CreateWizardStats("WizardLv2", 2, intelligence: 18);
                sc = CreateSpellcasting(stats, "WizardLv2_Test");

                int cantripSlots = sc.GetSpellSlotsPerDay(0);
                int firstLevelSlots = sc.GetSpellSlotsPerDay(1);
                int secondLevelSlots = sc.GetSpellSlotsPerDay(2);
                int highestSlotLevel = sc.GetHighestSlotLevel();

                Assert(cantripSlots == 4, "Wizard 2 has 4 cantrip slots", $"expected 4, got {cantripSlots}");
                Assert(firstLevelSlots == 3, "Wizard 2 has 2 base + 1 bonus 1st-level slot", $"expected 3, got {firstLevelSlots}");
                Assert(secondLevelSlots == 0, "Wizard 2 has 0 second-level slots", $"expected 0, got {secondLevelSlots}");
                Assert(highestSlotLevel == 1, "Wizard 2 highest slot level is 1st", $"expected 1, got {highestSlotLevel}");
            }
            finally
            {
                DestroySpellcasting(sc);
            }
        }

        private static void TestWizardLevel3UnlocksSecondLevelSlots()
        {
            SpellcastingComponent sc = null;
            try
            {
                CharacterStats stats = CreateWizardStats("WizardLv3", 3, intelligence: 18);
                sc = CreateSpellcasting(stats, "WizardLv3_Test");

                int secondLevelSlots = sc.GetSpellSlotsPerDay(2);
                int highestSlotLevel = sc.GetHighestSlotLevel();

                Assert(secondLevelSlots == 2, "Wizard 3 has 1 base + 1 bonus 2nd-level slots", $"expected 2, got {secondLevelSlots}");
                Assert(highestSlotLevel == 2, "Wizard 3 highest slot level is 2nd", $"expected 2, got {highestSlotLevel}");

                string knownSecondLevelSpell = sc.GetAllKnownSpells()
                    .Select(SpellDatabase.GetSpell)
                    .Where(spell => spell != null && spell.SpellLevel == 2)
                    .Select(spell => spell.Name)
                    .FirstOrDefault();

                Assert(!string.IsNullOrWhiteSpace(knownSecondLevelSpell), "Wizard 3 has access to known 2nd-level spells");
            }
            finally
            {
                DestroySpellcasting(sc);
            }
        }

        private static void TestMulticlassPreparedSlotsAreSeparatedPerClass()
        {
            SpellcastingComponent sc = null;
            try
            {
                sc = CreateSpellcasting(CreateWizardClericStats("MultiPrep"), "MulticlassSlots_Test");

                List<string> classes = sc.GetPreparedCasterClassNames();
                Assert(classes.Contains("Wizard"), "Wizard/Cleric includes Wizard caster class");
                Assert(classes.Contains("Cleric"), "Wizard/Cleric includes Cleric caster class");

                List<SpellSlot> wizardSlots = sc.GetSlotsForClass("Wizard");
                List<SpellSlot> clericSlots = sc.GetSlotsForClass("Cleric");
                Assert(wizardSlots.Count > 0, "Wizard/Cleric has wizard slots");
                Assert(clericSlots.Count > 0, "Wizard/Cleric has cleric slots");
                Assert(wizardSlots.All(s => string.Equals(s.CasterClassName, "Wizard", StringComparison.OrdinalIgnoreCase)), "Wizard slots tagged Wizard");
                Assert(clericSlots.All(s => string.Equals(s.CasterClassName, "Cleric", StringComparison.OrdinalIgnoreCase)), "Cleric slots tagged Cleric");

                SpellData bless = SpellDatabase.GetSpell("bless");
                SpellData magicMissile = SpellDatabase.GetSpell("magic_missile");
                Assert(bless != null && magicMissile != null, "Required spells exist for multiclass prep test");

                SpellSlot wizardLv1 = wizardSlots.FirstOrDefault(s => s.Level == 1 && !s.IsDomainSlot);
                SpellSlot clericLv1 = clericSlots.FirstOrDefault(s => s.Level == 1 && !s.IsDomainSlot);
                Assert(wizardLv1 != null && clericLv1 != null, "Wizard/Cleric has regular level-1 slots in both classes");

                int wizardGlobalIndex = sc.SpellSlots.IndexOf(wizardLv1);
                int clericGlobalIndex = sc.SpellSlots.IndexOf(clericLv1);
                bool wizardAcceptsWizardSpell = sc.PrepareSpellInSlot(wizardGlobalIndex, magicMissile);
                bool clericAcceptsClericSpell = sc.PrepareSpellInSlot(clericGlobalIndex, bless);
                bool wizardRejectsClericSpell = !sc.PrepareSpellInSlot(wizardGlobalIndex, bless);
                bool clericRejectsWizardSpell = !sc.PrepareSpellInSlot(clericGlobalIndex, magicMissile);

                Assert(wizardAcceptsWizardSpell, "Wizard slot accepts wizard spellbook spell");
                Assert(clericAcceptsClericSpell, "Cleric slot accepts cleric list spell");
                Assert(wizardRejectsClericSpell, "Wizard slot rejects cleric-only spell");
                Assert(clericRejectsWizardSpell, "Cleric slot rejects wizard-only spell");
            }
            finally
            {
                DestroySpellcasting(sc);
            }
        }

        private static void TestMulticlassCastingConsumesCorrectClassSlotPool()
        {
            SpellcastingComponent sc = null;
            try
            {
                sc = CreateSpellcasting(CreateWizardClericStats("MultiCast"), "MulticlassCast_Test");

                SpellData magicMissile = SpellDatabase.GetSpell("magic_missile");
                SpellData bless = SpellDatabase.GetSpell("bless");
                Assert(magicMissile != null && bless != null, "Required spells exist for multiclass casting test");

                int wizardBefore = sc.GetSlotsRemainingForClass("Wizard")[1];
                int clericBefore = sc.GetSlotsRemainingForClass("Cleric")[1];

                bool castWizardSpell = sc.CastSpellFromSlot(magicMissile);
                int wizardAfterWizardCast = sc.GetSlotsRemainingForClass("Wizard")[1];
                int clericAfterWizardCast = sc.GetSlotsRemainingForClass("Cleric")[1];

                bool castClericSpell = sc.CastSpellFromSlot(bless);
                int wizardAfterClericCast = sc.GetSlotsRemainingForClass("Wizard")[1];
                int clericAfterClericCast = sc.GetSlotsRemainingForClass("Cleric")[1];

                Assert(castWizardSpell, "Wizard spell cast succeeds for multiclass caster");
                Assert(castClericSpell, "Cleric spell cast succeeds for multiclass caster");
                Assert(wizardAfterWizardCast == wizardBefore - 1, "Casting wizard spell consumes wizard level-1 slot", $"before={wizardBefore}, after={wizardAfterWizardCast}");
                Assert(clericAfterWizardCast == clericBefore, "Casting wizard spell does not consume cleric level-1 slot", $"before={clericBefore}, after={clericAfterWizardCast}");
                Assert(wizardAfterClericCast == wizardAfterWizardCast, "Casting cleric spell does not consume wizard level-1 slot", $"before={wizardAfterWizardCast}, after={wizardAfterClericCast}");
                Assert(clericAfterClericCast == clericBefore - 1, "Casting cleric spell consumes cleric level-1 slot", $"before={clericBefore}, after={clericAfterClericCast}");
            }
            finally
            {
                DestroySpellcasting(sc);
            }
        }

        private static void TestWizardPrimaryMulticlassDoesNotAutoFillWizardSpellbook()
        {
            SpellcastingComponent sc = null;
            try
            {
                CharacterStats stats = CreateWizardClericStats("WizardPrimary");
                stats.CharacterClass = "Wizard";
                sc = CreateSpellcasting(stats, "WizardPrimary_Test");

                List<SpellData> wizardKnown = sc.GetKnownSpellsForClass("Wizard");
                int wizardCantrips = wizardKnown.Count(s => s != null && s.SpellLevel == 0);
                int wizardNonCantrips = wizardKnown.Count(s => s != null && s.SpellLevel > 0);

                Assert(wizardCantrips > 0, "Wizard-primary multiclass keeps wizard cantrips in spellbook");
                Assert(wizardNonCantrips == 0,
                    "Wizard-primary multiclass does not auto-fill wizard non-cantrip spellbook",
                    $"expected 0 non-cantrips, got {wizardNonCantrips}");
            }
            finally
            {
                DestroySpellcasting(sc);
            }
        }

        private static void TestWizardPrimaryFallbackDoesNotApplyToClericPrimaryMulticlass()
        {
            SpellcastingComponent sc = null;
            try
            {
                CharacterStats stats = CreateWizardClericStats("ClericPrimary");
                stats.CharacterClass = "Cleric";
                sc = CreateSpellcasting(stats, "ClericPrimary_Test");

                List<SpellData> wizardKnown = sc.GetKnownSpellsForClass("Wizard");
                int wizardNonCantrips = wizardKnown.Count(s => s != null && s.SpellLevel > 0);

                Assert(sc.ClassKnowsAllSpells("Cleric"), "Cleric class reports full spell access");
                Assert(!sc.ClassKnowsAllSpells("Wizard"), "Wizard class does not report full spell access");
                Assert(wizardNonCantrips == 0,
                    "Cleric-primary multiclass does not auto-fill wizard non-cantrip spellbook",
                    $"expected 0 non-cantrips, got {wizardNonCantrips}");
            }
            finally
            {
                DestroySpellcasting(sc);
            }
        }

        private static void TestLearnSpellForClassStaysWithinSelectedCasterClass()
        {
            SpellcastingComponent sc = null;
            try
            {
                CharacterStats stats = CreateWizardClericStats("ClassScopedLearn");
                stats.CharacterClass = "Cleric";
                sc = CreateSpellcasting(stats, "ClassScopedLearn_Test");

                sc.LearnSpellForClass("Wizard", "magic_missile");

                bool wizardHasMagicMissile = sc.GetKnownSpellsForClass("Wizard")
                    .Any(s => s != null && s.SpellId == "magic_missile");
                bool clericHasMagicMissile = sc.GetKnownSpellsForClass("Cleric")
                    .Any(s => s != null && s.SpellId == "magic_missile");

                Assert(wizardHasMagicMissile, "Wizard learns chosen wizard spell via class-scoped learning");
                Assert(!clericHasMagicMissile, "Wizard spell learning does not leak into Cleric known list");
            }
            finally
            {
                DestroySpellcasting(sc);
            }
        }

        private static void TestWizardInitialSpellbookSelectionCountUsesIntModifier()
        {
            int intModThree = SpellSelectionUI.GetInitialWizardSpellbookSpellCount(3);
            int intModZero = SpellSelectionUI.GetInitialWizardSpellbookSpellCount(0);
            int intModNegative = SpellSelectionUI.GetInitialWizardSpellbookSpellCount(-2);

            CharacterStats wizardOne = CreateWizardStats("WizardOneCount", wizardLevel: 1, intelligence: 16);
            CharacterStats wizardTwo = CreateWizardStats("WizardTwoCount", wizardLevel: 2, intelligence: 16);
            int wizardOneSelectionCount = SpellSelectionUI.GetWizardLevelUpSpellSelectionCount(wizardOne);
            int wizardTwoSelectionCount = SpellSelectionUI.GetWizardLevelUpSpellSelectionCount(wizardTwo);

            Assert(intModThree == 6, "Wizard initial spellbook count is 3 + INT mod (INT mod +3 => 6)", $"expected 6, got {intModThree}");
            Assert(intModZero == 3, "Wizard initial spellbook count supports non-bonus INT (INT mod +0 => 3)", $"expected 3, got {intModZero}");
            Assert(intModNegative == 1, "Wizard initial spellbook count has floor of 1 for low INT", $"expected 1, got {intModNegative}");
            Assert(wizardOneSelectionCount == 6, "Wizard 1 level-up selection count uses initial spellbook formula", $"expected 6, got {wizardOneSelectionCount}");
            Assert(wizardTwoSelectionCount == 2, "Wizard 2+ level-up selection count remains 2 spells", $"expected 2, got {wizardTwoSelectionCount}");
        }

        private static void TestWizardInitialSpellbookSelectionLevelDetection()
        {
            CharacterStats firstWizardLevel = null;
            CharacterStats higherWizardLevel = null;
            try
            {
                firstWizardLevel = new CharacterStats(
                    name: "FighterToWizard",
                    level: 3,
                    characterClass: "Wizard",
                    str: 14,
                    dex: 12,
                    con: 12,
                    wis: 10,
                    intelligence: 16,
                    cha: 8,
                    bab: 2,
                    armorBonus: 0,
                    shieldBonus: 0,
                    damageDice: 8,
                    damageCount: 1,
                    bonusDamage: 0,
                    baseSpeed: 6,
                    atkRange: 1,
                    baseHitDieHP: 20,
                    raceName: "Human");
                firstWizardLevel.ClassLevels = new List<ClassLevelEntry>
                {
                    new ClassLevelEntry("Fighter", 2),
                    new ClassLevelEntry("Wizard", 1)
                };

                higherWizardLevel = CreateWizardStats("WizardLv2Detect", wizardLevel: 2, intelligence: 16);

                bool firstLevelDetected = SpellSelectionUI.IsInitialWizardSpellbookSelectionLevel(firstWizardLevel);
                bool higherLevelDetected = SpellSelectionUI.IsInitialWizardSpellbookSelectionLevel(higherWizardLevel);

                Assert(firstLevelDetected, "Wizard initial spellbook detection is true at Wizard class level 1");
                Assert(!higherLevelDetected, "Wizard initial spellbook detection is false above Wizard class level 1");
            }
            catch (Exception ex)
            {
                Assert(false, "Wizard initial spellbook selection level detection did not throw", ex.Message);
            }
        }
    }
}
