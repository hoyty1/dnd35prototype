using System.Collections.Generic;
using System.Linq;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Magic
{
    /// <summary>
    /// Tests for all 19 Summon Monster III creatures from the D&D 3.5e Monster Manual.
    /// Validates that each creature is registered in NPCDatabase with correct stats,
    /// and that SummonMonsterLists level 3 contains the expected options.
    /// Run manually via SummonMonster3CreaturesTests.RunAll().
    /// </summary>
    public static class SummonMonster3CreaturesTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== SUMMON MONSTER III CREATURE TESTS ======");

            NPCDatabase.Init();

            // Creature definition tests
            TestBlackBearDefinition();
            TestBisonDefinition();
            TestDireBadgerDefinition();
            TestApeDefinition();
            TestBoarDefinition();
            TestDireBatDefinition();
            TestDireWeaselDefinition();
            TestWolverineDefinition();
            TestCrocodileDefinition();
            TestConstrictorSnakeDefinition();
            TestLargeViperDefinition();
            TestHugeMonstruousCentipedeDefinition();
            TestSmallAirElementalDefinition();
            TestSmallFireElementalDefinition();
            TestSmallEarthElementalDefinition();
            TestSmallWaterElementalDefinition();
            TestHippogriffDefinition();
            TestHellHoundDefinition();
            TestDretchDefinition();

            // Summon list tests
            TestSummonMonsterIIIListCount();
            TestSummonMonsterIIIContainsAllCreatures();
            TestSummonMonsterIIIAlignmentDistribution();

            Debug.Log($"====== Summon Monster III Creature Results: {_passed} passed, {_failed} failed ======");
        }

        // ───────────────────────────────────────────
        // Helper methods
        // ───────────────────────────────────────────

        private static void AssertTrue(bool condition, string testName)
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"  PASS: {testName}");
            }
            else
            {
                _failed++;
                Debug.LogError($"  FAIL: {testName}");
            }
        }

        private static void AssertEqual(int expected, int actual, string testName)
        {
            if (expected == actual)
            {
                _passed++;
                Debug.Log($"  PASS: {testName} ({actual})");
            }
            else
            {
                _failed++;
                Debug.LogError($"  FAIL: {testName} (expected {expected}, got {actual})");
            }
        }

        private static bool HasSpecialAbility(NPCDefinition def, string abilitySubstring)
        {
            if (def == null || def.SpecialAbilities == null) return false;
            return def.SpecialAbilities.Any(a => a != null && a.Contains(abilitySubstring));
        }

        // ───────────────────────────────────────────
        // Individual creature definition tests
        // ───────────────────────────────────────────

        private static void TestBlackBearDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("black_bear");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Medium
                      && def.STR == 19
                      && def.DEX == 13
                      && def.CON == 15
                      && def.BaseHitDieHP == 19
                      && def.ChallengeRating == "2"
                      && def.NaturalAttacks != null
                      && def.NaturalAttacks.Count >= 3; // 2 claws + bite
            AssertTrue(ok, "Black Bear definition matches MM stats");
        }

        private static void TestBisonDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("bison");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Large
                      && def.STR == 22
                      && def.CON == 16
                      && def.BaseHitDieHP == 37
                      && def.HitDice == 8
                      && def.Level == 5
                      && def.ChallengeRating == "2";
            AssertTrue(ok, "Bison definition matches MM stats");
        }

        private static void TestDireBadgerDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("dire_badger");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Medium
                      && def.STR == 14
                      && def.CON == 19
                      && def.BaseHitDieHP == 28
                      && def.ChallengeRating == "2"
                      && HasSpecialAbility(def, "Rage");
            AssertTrue(ok, "Dire Badger definition matches MM stats with Rage");
        }

        private static void TestApeDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("ape");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Large
                      && def.STR == 21
                      && def.DEX == 15
                      && def.BaseHitDieHP == 29
                      && def.ChallengeRating == "2"
                      && def.HasScent;
            AssertTrue(ok, "Ape definition matches MM stats");
        }

        private static void TestBoarDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("boar");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Medium
                      && def.STR == 15
                      && def.CON == 17
                      && def.BaseHitDieHP == 25
                      && def.ChallengeRating == "2"
                      && HasSpecialAbility(def, "Ferocity");
            AssertTrue(ok, "Boar definition matches MM stats with Ferocity");
        }

        private static void TestDireBatDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("dire_bat");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Large
                      && def.DEX == 22
                      && def.NaturalArmorBonus == 5
                      && def.ChallengeRating == "2";
            AssertTrue(ok, "Dire Bat definition matches MM stats");
        }

        private static void TestDireWeaselDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("dire_weasel");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Medium
                      && def.STR == 14
                      && def.DEX == 19
                      && def.BaseHitDieHP == 13
                      && def.ChallengeRating == "2"
                      && HasSpecialAbility(def, "Attach")
                      && HasSpecialAbility(def, "Blood Drain");
            AssertTrue(ok, "Dire Weasel definition matches MM stats with Attach/Blood Drain");
        }

        private static void TestWolverineDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("wolverine");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Medium
                      && def.STR == 14
                      && def.CON == 19
                      && def.BaseHitDieHP == 28
                      && def.ChallengeRating == "2"
                      && HasSpecialAbility(def, "Rage");
            AssertTrue(ok, "Wolverine definition matches MM stats with Rage");
        }

        private static void TestCrocodileDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("crocodile");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Medium
                      && def.STR == 19
                      && def.NaturalArmorBonus == 4
                      && def.BaseHitDieHP == 22
                      && def.ChallengeRating == "2"
                      && def.HasImprovedGrab
                      && def.HasScent;
            AssertTrue(ok, "Crocodile definition matches MM stats with Improved Grab");
        }

        private static void TestConstrictorSnakeDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("constrictor_snake");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Medium
                      && def.STR == 17
                      && def.DEX == 17
                      && def.Level == 3
                      && def.HitDice == 6
                      && def.BaseHitDieHP == 19
                      && def.ChallengeRating == "2"
                      && def.HasImprovedGrab
                      && def.HasScent;
            AssertTrue(ok, "Constrictor Snake definition matches MM stats (Medium, 3HD)");
        }

        private static void TestLargeViperDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("large_viper");
            bool ok = def != null
                      && def.CreatureType == "Animal"
                      && def.SizeCategory == SizeCategory.Large
                      && def.STR == 8
                      && def.DEX == 17
                      && def.BaseHitDieHP == 13
                      && def.ChallengeRating == "2"
                      && def.HasScent;
            AssertTrue(ok, "Large Viper definition matches MM stats");
        }

        private static void TestHugeMonstruousCentipedeDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("huge_monstrous_centipede");
            bool ok = def != null
                      && def.CreatureType == "Vermin"
                      && def.SizeCategory == SizeCategory.Huge
                      && def.STR == 17
                      && def.BaseHitDieHP == 33
                      && def.ChallengeRating == "2"
                      && def.IsMindless
                      && def.INT == CharacterStats.NO_SCORE;
            AssertTrue(ok, "Huge Monstrous Centipede definition matches MM stats (Mindless)");
        }

        private static void TestSmallAirElementalDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("small_air_elemental");
            bool ok = def != null
                      && def.CreatureType == "Elemental"
                      && def.SizeCategory == SizeCategory.Small
                      && def.DEX == 17
                      && def.CON == 10
                      && def.NaturalArmorBonus == 3
                      && def.BaseHitDieHP == 9
                      && def.ChallengeRating == "1";
            AssertTrue(ok, "Small Air Elemental definition matches MM stats");

            bool immunities = def != null
                              && def.Immunities != null
                              && def.Immunities.immuneToPoison
                              && def.Immunities.immuneToCriticalHits;
            AssertTrue(immunities, "Small Air Elemental has elemental immunities");
        }

        private static void TestSmallFireElementalDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("small_fire_elemental");
            bool ok = def != null
                      && def.CreatureType == "Elemental"
                      && def.SizeCategory == SizeCategory.Small
                      && def.STR == 10
                      && def.CON == 10
                      && def.BaseHitDieHP == 9
                      && def.ChallengeRating == "1";
            AssertTrue(ok, "Small Fire Elemental definition matches MM stats");

            bool fireImmune = def != null
                              && def.Immunities != null
                              && def.Immunities.immuneToFire;
            AssertTrue(fireImmune, "Small Fire Elemental is immune to fire");
        }

        private static void TestSmallEarthElementalDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("small_earth_elemental");
            bool ok = def != null
                      && def.CreatureType == "Elemental"
                      && def.SizeCategory == SizeCategory.Small
                      && def.STR == 17
                      && def.CON == 13
                      && def.NaturalArmorBonus == 4
                      && def.BaseHitDieHP == 11
                      && def.ChallengeRating == "1";
            AssertTrue(ok, "Small Earth Elemental definition matches MM stats");

            bool immunities = def != null
                              && def.Immunities != null
                              && def.Immunities.immuneToPoison
                              && def.Immunities.immuneToCriticalHits;
            AssertTrue(immunities, "Small Earth Elemental has elemental immunities");
        }

        private static void TestSmallWaterElementalDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("small_water_elemental");
            bool ok = def != null
                      && def.CreatureType == "Elemental"
                      && def.SizeCategory == SizeCategory.Small
                      && def.STR == 14
                      && def.CON == 13
                      && def.NaturalArmorBonus == 4
                      && def.BaseHitDieHP == 11
                      && def.ChallengeRating == "1";
            AssertTrue(ok, "Small Water Elemental definition matches MM stats");

            bool immunities = def != null
                              && def.Immunities != null
                              && def.Immunities.immuneToPoison
                              && def.Immunities.immuneToCriticalHits;
            AssertTrue(immunities, "Small Water Elemental has elemental immunities");
        }

        private static void TestHippogriffDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("hippogriff");
            bool ok = def != null
                      && def.CreatureType == "Magical Beast"
                      && def.SizeCategory == SizeCategory.Large
                      && def.STR == 18
                      && def.DEX == 15
                      && def.BaseHitDieHP == 25
                      && def.ChallengeRating == "2"
                      && def.HasScent;
            AssertTrue(ok, "Hippogriff definition matches MM stats");
        }

        private static void TestHellHoundDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("hell_hound");
            bool ok = def != null
                      && def.CreatureType == "Outsider"
                      && def.SizeCategory == SizeCategory.Medium
                      && def.STR == 13
                      && def.BaseHitDieHP == 22
                      && def.ChallengeRating == "3"
                      && def.HasScent;
            AssertTrue(ok, "Hell Hound definition matches MM stats");

            bool fireImmune = def != null
                              && def.Immunities != null
                              && def.Immunities.immuneToFire;
            AssertTrue(fireImmune, "Hell Hound is immune to fire");

            bool hasBreath = HasSpecialAbility(def, "Breath Weapon");
            AssertTrue(hasBreath, "Hell Hound has Breath Weapon special ability");
        }

        private static void TestDretchDefinition()
        {
            NPCDefinition def = NPCDatabase.Get("dretch");
            bool ok = def != null
                      && def.CreatureType == "Outsider"
                      && def.SizeCategory == SizeCategory.Small
                      && def.STR == 12
                      && def.BaseHitDieHP == 13
                      && def.ChallengeRating == "2"
                      && def.NaturalAttacks != null
                      && def.NaturalAttacks.Count >= 3; // 2 claws + bite
            AssertTrue(ok, "Dretch definition matches MM stats");

            bool poisonImmune = def != null
                                && def.Immunities != null
                                && def.Immunities.immuneToPoison;
            AssertTrue(poisonImmune, "Dretch is immune to poison");

            bool hasDR = def != null
                         && def.DamageReductionAmount > 0;
            AssertTrue(hasDR, "Dretch has damage reduction");
        }

        // ───────────────────────────────────────────
        // Summon Monster III list tests
        // ───────────────────────────────────────────

        /// <summary>
        /// Creates a True-Neutral Wizard caster for testing — wizards see all options
        /// regardless of alignment, and True Neutral has no axis restrictions.
        /// </summary>
        private static CharacterStats CreateUnrestrictedCaster()
        {
            CharacterStats caster = new CharacterStats(
                "Test Wizard", 1, "Wizard",
                10, 10, 10, 10, 10, 10,
                0, 0, 0, 6, 1, 0, 6, 1, 8
            );
            caster.CharacterAlignment = Alignment.TrueNeutral;
            return caster;
        }

        private static List<SummonMonsterOption> GetLevel3Options()
        {
            return SummonMonsterLists.GetFilteredOptionsForListLevel(3, CreateUnrestrictedCaster());
        }

        private static void TestSummonMonsterIIIListCount()
        {
            List<SummonMonsterOption> options = GetLevel3Options();
            // 19 creatures, but some appear as both celestial and fiendish variants
            // Expected: 19 total entries (see SummonMonsterLists.GetSummonMonsterIIIOptions)
            AssertTrue(options != null && options.Count == 19,
                $"Summon Monster III list has exactly 19 entries (got {(options != null ? options.Count : 0)})");
        }

        private static void TestSummonMonsterIIIContainsAllCreatures()
        {
            List<SummonMonsterOption> options = GetLevel3Options();
            if (options == null)
            {
                AssertTrue(false, "Summon Monster III list is not null");
                return;
            }

            // Verify key creatures are present by NPC definition ID
            string[] expectedIds = new string[]
            {
                "black_bear", "bison", "dire_badger", "ape", "boar",
                "dire_bat", "dire_weasel", "wolverine", "crocodile",
                "constrictor_snake", "large_viper", "huge_monstrous_centipede",
                "small_air_elemental", "small_fire_elemental",
                "small_earth_elemental", "small_water_elemental",
                "hippogriff", "hell_hound", "dretch"
            };

            foreach (string id in expectedIds)
            {
                bool found = options.Any(o => o.NpcDefinitionId == id);
                AssertTrue(found, $"Summon Monster III list contains creature: {id}");
            }
        }

        private static void TestSummonMonsterIIIAlignmentDistribution()
        {
            List<SummonMonsterOption> options = GetLevel3Options();
            if (options == null)
            {
                AssertTrue(false, "Summon Monster III list for alignment distribution test is not null");
                return;
            }

            // Should have a mix of good (celestial), evil (fiendish), and neutral options
            bool hasGood = options.Any(o => o.SummonedCreatureAlignment == Alignment.NeutralGood
                                         || o.SummonedCreatureAlignment == Alignment.LawfulGood
                                         || o.SummonedCreatureAlignment == Alignment.ChaoticGood);
            bool hasEvil = options.Any(o => o.SummonedCreatureAlignment == Alignment.NeutralEvil
                                         || o.SummonedCreatureAlignment == Alignment.LawfulEvil
                                         || o.SummonedCreatureAlignment == Alignment.ChaoticEvil);
            bool hasNeutral = options.Any(o => o.SummonedCreatureAlignment == Alignment.TrueNeutral
                                            || o.SummonedCreatureAlignment == Alignment.None);

            AssertTrue(hasGood, "Summon Monster III list includes good-aligned (celestial) options");
            AssertTrue(hasEvil, "Summon Monster III list includes evil-aligned (fiendish) options");
            AssertTrue(hasNeutral, "Summon Monster III list includes neutral options (elementals)");
        }
    }
}
