using System;
using System.Linq;
using UnityEngine;

namespace Tests.Magic
{
    /// <summary>
    /// Regression checks for wizard spell progression during level-up.
    /// Ensures Wizard 2 cannot access 2nd-level slots/spells, and Wizard 3 can.
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
                CharacterStats stats = CreateWizardStats("WizardLv2", 2, intelligence: 18); // INT mod +4
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
                CharacterStats stats = CreateWizardStats("WizardLv3", 3, intelligence: 18); // INT mod +4
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
    }
}
