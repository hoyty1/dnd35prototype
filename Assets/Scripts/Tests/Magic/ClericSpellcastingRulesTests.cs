using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Magic
{
    /// <summary>
    /// CRE-062 (and the cleric part of SPL-041): cleric spellcasting through the shared SpellcastingComponent, for PCs
    /// and NPCs alike. A cleric prepares any spell of the cleric list of a level it can cast, plus one domain spell per
    /// spell level in a domain slot, which may hold a domain spell that is not on the cleric list (PHB p.32); Wisdom
    /// 16-17 gives one bonus spell of 1st, 2nd and 3rd level (PHB p.8, Table 1-1); a 5th-level cleric has 5/3/2/1 base
    /// spells (PHB p.31, Table 3-6). Domain spells of levels 3 and 4 as PHB p.185-189 lists them. The NPC human_cleric
    /// is the DMG's sample 5th-level cleric (DMG p.114: neutral good, Healing and Protection, prepared 5/5/4/3).
    /// The component tests need no scene; the spawn test goes through GameManager.InitializeNPCFromDefinition and needs
    /// Play mode (a scene GameManager). Spawned controllers sit off the grid and are destroyed afterwards.
    /// </summary>
    public static class ClericSpellcastingRulesTests
    {
        private static int _passed;
        private static int _failed;

        private static readonly Vector2Int SpawnPos = new Vector2Int(-57, -57);

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== CLERIC SPELLCASTING RULES TESTS (CRE-062) ======");

            RaceDatabase.Init();
            ClassRegistry.Init();
            SpellDatabase.Init();
            DomainDatabase.Init();
            NPCDatabase.Init();
            ItemDatabase.Init();
            FeatDefinitions.Init();

            TestDomainSpellsOfLevelsThreeAndFour();
            TestClericFiveSlotsAndKnownSpells();
            TestClericFiveAutoPreparesThirdLevel();
            TestDomainSlotHoldsDomainOnlySpell();
            TestDomainOnlySpellsAreNotClericSpells();
            TestDomainSlotUsesDomainLevel();
            TestSpellGrantingClassLevels();

            if (GameManager.Instance == null)
                Assert(false, "The human_cleric spawn test needs Play mode with a scene GameManager");
            else
                TestHumanClericSpawnsWithDmgSpells();

            Debug.Log($"====== Cleric Spellcasting Results: {_passed} passed, {_failed} failed ======");
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

        private static CharacterStats ClericStats(string name, int level, int wis, params string[] domains)
        {
            var stats = new CharacterStats(
                name: name, level: level, characterClass: "Cleric",
                str: 12, dex: 10, con: 14, wis: wis, intelligence: 10, cha: 12,
                bab: level * 3 / 4, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 8 * level, raceName: "Human");
            stats.ChosenDomains = new List<string>(domains);
            return stats;
        }

        private static SpellcastingComponent Create(CharacterStats stats, List<string> prepared = null)
        {
            var go = new GameObject("ClericSpellcastingTest_" + stats.CharacterName);
            SpellcastingComponent sc = go.AddComponent<SpellcastingComponent>();
            sc.PreparedSpellSlotIds = prepared;
            sc.Init(stats);
            return sc;
        }

        private static void Destroy(SpellcastingComponent sc)
        {
            if (sc != null)
                Object.DestroyImmediate(sc.gameObject);
        }

        private static List<SpellSlot> Slots(SpellcastingComponent sc, int level, bool domain)
        {
            return sc.SpellSlots.Where(s => s != null && s.Level == level && s.IsDomainSlot == domain).ToList();
        }

        private static void TestDomainSpellsOfLevelsThreeAndFour()
        {
            Debug.Log("--- Domain spells of levels 3 and 4 (PHB p.185-189) ---");
            var expected = new (string domain, int level, string spellId)[]
            {
                ("Healing", 3, SpellNames.CURE_SERIOUS_WOUNDS), ("Healing", 4, SpellNames.CURE_CRITICAL_WOUNDS),
                ("Protection", 3, SpellNames.PROTECTION_FROM_ENERGY), ("Protection", 4, SpellNames.SPELL_IMMUNITY),
                ("Good", 3, SpellNames.MAGIC_CIRCLE_AGAINST_EVIL), ("Good", 4, SpellNames.HOLY_SMITE),
                ("Evil", 3, SpellNames.MAGIC_CIRCLE_AGAINST_GOOD), ("Evil", 4, SpellNames.UNHOLY_BLIGHT),
                ("Law", 3, SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS), ("Law", 4, SpellNames.ORDERS_WRATH),
                ("Chaos", 3, SpellNames.MAGIC_CIRCLE_AGAINST_LAW), ("Chaos", 4, SpellNames.CHAOS_HAMMER),
                ("War", 3, SpellNames.MAGIC_VESTMENT), ("War", 4, SpellNames.DIVINE_POWER),
                ("Strength", 3, SpellNames.MAGIC_VESTMENT), ("Strength", 4, SpellNames.SPELL_IMMUNITY),
                ("Magic", 3, SpellNames.DISPEL_MAGIC), ("Magic", 4, SpellNames.IMBUE_WITH_SPELL_ABILITY),
                ("Luck", 3, SpellNames.PROTECTION_FROM_ENERGY), ("Luck", 4, SpellNames.FREEDOM_OF_MOVEMENT),
                ("Destruction", 3, SpellNames.CONTAGION), ("Destruction", 4, SpellNames.INFLICT_CRITICAL_WOUNDS),
                ("Sun", 3, SpellNames.SEARING_LIGHT), ("Sun", 4, SpellNames.FIRE_SHIELD),
                ("Fire", 3, SpellNames.RESIST_ENERGY), ("Fire", 4, SpellNames.WALL_OF_FIRE),
                ("Plant", 3, SpellNames.PLANT_GROWTH), ("Plant", 4, SpellNames.COMMAND_PLANTS),
                ("Animal", 3, SpellNames.DOMINATE_ANIMAL), ("Animal", 4, SpellNames.SUMMON_NATURES_ALLY_4),
                ("Death", 4, SpellNames.DEATH_WARD), ("Earth", 4, SpellNames.SPIKE_STONES),
                ("Travel", 4, SpellNames.DIMENSION_DOOR), ("Trickery", 4, SpellNames.CONFUSION),
            };
            foreach (var row in expected)
            {
                List<SpellData> spells = SpellDatabase.GetDomainSpells(row.domain, row.level);
                Assert(spells.Any(s => s.SpellId == row.spellId),
                    $"{row.domain} domain {row.level}: {row.spellId}",
                    "got [" + string.Join(", ", spells.Select(s => s.SpellId)) + "]");
            }
        }

        private static void TestClericFiveSlotsAndKnownSpells()
        {
            Debug.Log("--- Cleric 5, Wis 17, Healing and Protection: 5/3+1+1/2+1+1/1+1+1 slots; the whole cleric list known ---");
            SpellcastingComponent sc = null;
            try
            {
                sc = Create(ClericStats("Cleric5", 5, 17, "Healing", "Protection"));
                int[] regular = { 5, 4, 3, 2 };
                for (int level = 0; level <= 3; level++)
                {
                    int r = Slots(sc, level, false).Count;
                    int d = Slots(sc, level, true).Count;
                    Assert(r == regular[level] && d == (level == 0 ? 0 : 1),
                        $"Cleric 5 level-{level} slots: {regular[level]} regular, {(level == 0 ? 0 : 1)} domain (PHB p.31, p.8)",
                        $"got {r} regular, {d} domain");
                }
                Assert(Slots(sc, 4, false).Count + Slots(sc, 4, true).Count == 0, "Cleric 5 has no 4th-level slot");

                List<SpellData> known = sc.GetKnownSpellsForClass("Cleric");
                foreach (string id in new[] { SpellNames.PRAYER, SpellNames.DISPEL_MAGIC, SpellNames.CURE_SERIOUS_WOUNDS, SpellNames.MAGIC_CIRCLE_AGAINST_EVIL, SpellNames.CURE_CRITICAL_WOUNDS })
                    Assert(known.Any(s => s.SpellId == id), $"Cleric list includes {id} (any cleric spell can be prepared, PHB p.32)");
                Assert(known.All(s => s.SpellLevel == s.GetSpellLevelFor("Cleric")),
                    "Every known cleric spell has its cleric level as its SpellLevel (per-class levels wait for SPL-008)",
                    string.Join(", ", known.Where(s => s.SpellLevel != s.GetSpellLevelFor("Cleric")).Select(s => s.SpellId)));
                Assert(!known.Any(s => s.SpellId == SpellNames.WALL_OF_FIRE), "Wall of Fire (Fire domain only) is not on the cleric list");
            }
            finally
            {
                Destroy(sc);
            }
        }

        private static void TestClericFiveAutoPreparesThirdLevel()
        {
            Debug.Log("--- Cleric 5 without a preparation list: every slot of levels 0-3 prepared, the domain slots with domain spells ---");
            SpellcastingComponent sc = null;
            try
            {
                sc = Create(ClericStats("Cleric5Auto", 5, 17, "Healing", "Protection"));
                List<SpellSlot> third = Slots(sc, 3, false);
                Assert(third.Count == 2 && third.All(s => s.HasSpell && s.PreparedSpell.SpellLevel == 3),
                    "Both regular 3rd-level slots hold a 3rd-level cleric spell",
                    string.Join(", ", third.Select(s => s.HasSpell ? s.PreparedSpell.SpellId : "empty")));
                List<SpellSlot> domain3 = Slots(sc, 3, true);
                Assert(domain3.Count == 1 && domain3[0].HasSpell
                       && (domain3[0].PreparedSpell.SpellId == SpellNames.CURE_SERIOUS_WOUNDS || domain3[0].PreparedSpell.SpellId == SpellNames.PROTECTION_FROM_ENERGY),
                    "The 3rd-level domain slot holds Cure Serious Wounds or Protection from Energy",
                    domain3.Count == 1 && domain3[0].HasSpell ? domain3[0].PreparedSpell.SpellId : "empty");
            }
            finally
            {
                Destroy(sc);
            }
        }

        private static void TestDomainSlotHoldsDomainOnlySpell()
        {
            Debug.Log("--- Cleric 7 of Fire and Sun: Wall of Fire (Fire 4, not a cleric spell) only in the domain slot ---");
            SpellcastingComponent sc = null;
            try
            {
                sc = Create(ClericStats("Cleric7Fire", 7, 14, "Fire", "Sun"));
                SpellData wall = SpellDatabase.GetSpell(SpellNames.WALL_OF_FIRE);
                int domainIndex = sc.SpellSlots.FindIndex(s => s != null && s.Level == 4 && s.IsDomainSlot);
                int regularIndex = sc.SpellSlots.FindIndex(s => s != null && s.Level == 4 && !s.IsDomainSlot);
                Assert(domainIndex >= 0 && regularIndex >= 0, "Cleric 7 has a regular and a domain 4th-level slot",
                    $"domain {domainIndex}, regular {regularIndex}");
                if (domainIndex >= 0)
                    Assert(sc.PrepareSpellInSlot(domainIndex, wall), "Wall of Fire can be prepared in the 4th-level domain slot (PHB p.32)");
                if (regularIndex >= 0)
                    Assert(!sc.PrepareSpellInSlot(regularIndex, wall), "Wall of Fire cannot be prepared in a regular cleric slot");
                SpellData holySmite = SpellDatabase.GetSpell(SpellNames.HOLY_SMITE);
                if (domainIndex >= 0)
                    Assert(!sc.PrepareSpellInSlot(domainIndex, holySmite), "Holy Smite (Good 4) is refused in the domain slot of a Fire and Sun cleric");
            }
            finally
            {
                Destroy(sc);
            }
        }

        // Spells the PHB lists only for domains and druids or rangers (PHB p.183-188), which the data once put on the
        // general cleric list (SPL-102).
        private static readonly string[] DomainOrNatureOnlySpells =
        {
            SpellNames.DOMINATE_ANIMAL, SpellNames.PLANT_GROWTH, SpellNames.COMMAND_PLANTS, SpellNames.SPIKE_STONES,
            SpellNames.CHAOS_HAMMER, SpellNames.HOLY_SMITE, SpellNames.ORDERS_WRATH, SpellNames.UNHOLY_BLIGHT,
        };

        private static void TestDomainOnlySpellsAreNotClericSpells()
        {
            Debug.Log("--- Cleric 7 of Healing and Protection: domain-only spells of levels 3-4 are not on the cleric list (PHB p.32, SPL-102) ---");
            SpellcastingComponent sc = null;
            try
            {
                sc = Create(ClericStats("Cleric7Plain", 7, 14, "Healing", "Protection"));
                List<SpellData> known = sc.GetKnownSpellsForClass("Cleric");
                foreach (string id in DomainOrNatureOnlySpells)
                    Assert(!known.Any(s => s.SpellId == id), $"{id} is not a cleric spell (domain or druid/ranger only)");

                List<SpellSlot> regular4 = Slots(sc, 4, false);
                Assert(regular4.Count > 0 && regular4.All(s => s.HasSpell && !DomainOrNatureOnlySpells.Contains(s.PreparedSpell.SpellId)),
                    "Auto-prepared regular 4th-level slots hold only cleric-list spells",
                    string.Join(", ", regular4.Select(s => s.HasSpell ? s.PreparedSpell.SpellId : "empty")));
                List<SpellSlot> regular3 = Slots(sc, 3, false);
                Assert(regular3.Count > 0 && regular3.All(s => s.HasSpell && !DomainOrNatureOnlySpells.Contains(s.PreparedSpell.SpellId)),
                    "Auto-prepared regular 3rd-level slots hold only cleric-list spells",
                    string.Join(", ", regular3.Select(s => s.HasSpell ? s.PreparedSpell.SpellId : "empty")));

                int regularIndex = sc.SpellSlots.FindIndex(s => s != null && s.Level == 4 && !s.IsDomainSlot);
                if (regularIndex >= 0)
                    Assert(!sc.PrepareSpellInSlot(regularIndex, SpellDatabase.GetSpell(SpellNames.HOLY_SMITE)),
                        "Holy Smite (Good 4 only) is refused in a regular 4th-level cleric slot");
            }
            finally
            {
                Destroy(sc);
            }

            SpellData dominate = SpellDatabase.GetSpell(SpellNames.DOMINATE_ANIMAL);
            SpellData plantGrowth = SpellDatabase.GetSpell(SpellNames.PLANT_GROWTH);
            SpellData commandPlants = SpellDatabase.GetSpell(SpellNames.COMMAND_PLANTS);
            SpellData spikeStones = SpellDatabase.GetSpell(SpellNames.SPIKE_STONES);
            Assert(dominate.IsAvailableFor("Druid", 3), "Dominate Animal is Drd 3 (PHB p.224)");
            Assert(plantGrowth.IsAvailableFor("Druid", 3) && plantGrowth.IsAvailableFor("Ranger", 3), "Plant Growth is Drd 3, Rgr 3 (PHB p.262)");
            Assert(commandPlants.IsAvailableFor("Druid", 4) && commandPlants.IsAvailableFor("Ranger", 3), "Command Plants is Drd 4, Rgr 3 (PHB p.211)");
            Assert(spikeStones.IsAvailableFor("Druid", 4), "Spike Stones is Drd 4 (PHB p.283)");
        }

        private static void TestDomainSlotUsesDomainLevel()
        {
            Debug.Log("--- Cleric 5 of Magic and Fire: a domain slot holds the domain spell of its own level (PHB p.32) ---");
            SpellcastingComponent sc = null;
            try
            {
                sc = Create(ClericStats("Cleric5MagicFire", 5, 14, "Magic", "Fire"));
                int domain1 = sc.SpellSlots.FindIndex(s => s != null && s.Level == 1 && s.IsDomainSlot);
                int domain2 = sc.SpellSlots.FindIndex(s => s != null && s.Level == 2 && s.IsDomainSlot);
                int domain3 = sc.SpellSlots.FindIndex(s => s != null && s.Level == 3 && s.IsDomainSlot);
                Assert(domain1 >= 0 && domain2 >= 0 && domain3 >= 0, "Cleric 5 has domain slots of levels 1-3",
                    $"{domain1}, {domain2}, {domain3}");
                if (domain1 < 0 || domain2 < 0 || domain3 < 0)
                    return;

                SpellData identify = SpellDatabase.GetSpell(SpellNames.IDENTIFY);
                SpellData resistEnergy = SpellDatabase.GetSpell(SpellNames.RESIST_ENERGY);
                SpellData produceFlame = SpellDatabase.GetSpell(SpellNames.PRODUCE_FLAME);
                Assert(!sc.PrepareSpellInSlot(domain1, identify), "Identify (Sor/Wiz 1, Magic 2) is refused in the 1st-level domain slot");
                Assert(sc.PrepareSpellInSlot(domain2, identify), "Identify is accepted in the 2nd-level domain slot (Magic 2)");
                Assert(!sc.PrepareSpellInSlot(domain2, resistEnergy), "Resist Energy (Clr 2, Fire 3) is refused in the 2nd-level domain slot");
                Assert(sc.PrepareSpellInSlot(domain2, produceFlame), "Produce Flame (Fire 2) is accepted in the 2nd-level domain slot");
                Assert(sc.PrepareSpellInSlot(domain3, resistEnergy), "Resist Energy is accepted in the 3rd-level domain slot (Fire 3)");
                Assert(sc.SpellSlots[domain3].HasSpell && sc.SpellSlots[domain3].PreparedSpell.SpellId == SpellNames.RESIST_ENERGY,
                    "The 3rd-level domain slot now holds Resist Energy");
            }
            finally
            {
                Destroy(sc);
            }
        }

        private static CharacterStats ClassStats(string name, string cls, int level, int wis)
        {
            return new CharacterStats(name: name, level: level, characterClass: cls,
                str: 14, dex: 10, con: 12, wis: wis, intelligence: 10, cha: 12, bab: level, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0, baseSpeed: 6, atkRange: 1, baseHitDieHP: 8 * level, raceName: "Human");
        }

        private static void TestSpellGrantingClassLevels()
        {
            Debug.Log("--- Class levels that grant spells (the spawn warning of CRE-062) ---");
            Assert(GameManager.NpcClassGrantsSpells(ClericStats("Cleric1", 1, 10)), "Cleric 1 grants spells");
            var fighter = new CharacterStats(name: "Fighter3", level: 3, characterClass: "Fighter",
                str: 16, dex: 12, con: 14, wis: 14, intelligence: 10, cha: 10, bab: 3, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0, baseSpeed: 6, atkRange: 1, baseHitDieHP: 24, raceName: "Human");
            Assert(!GameManager.NpcClassGrantsSpells(fighter), "Fighter 3 grants no spells");
            var paladin3 = new CharacterStats(name: "Paladin3", level: 3, characterClass: "Paladin",
                str: 16, dex: 10, con: 14, wis: 14, intelligence: 10, cha: 14, bab: 3, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0, baseSpeed: 6, atkRange: 1, baseHitDieHP: 24, raceName: "Human");
            Assert(!GameManager.NpcClassGrantsSpells(paladin3), "Paladin 3 grants no spells (PHB p.44: from 4th level)");
            var paladin4 = new CharacterStats(name: "Paladin4", level: 4, characterClass: "Paladin",
                str: 16, dex: 10, con: 14, wis: 14, intelligence: 10, cha: 14, bab: 4, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0, baseSpeed: 6, atkRange: 1, baseHitDieHP: 32, raceName: "Human");
            Assert(GameManager.NpcClassGrantsSpells(paladin4), "Paladin 4 with Wis 14 has a bonus 1st-level spell (PHB p.44, p.8)");
            Assert(!GameManager.NpcClassGrantsSpells(ClassStats("Paladin5Wis11", "Paladin", 5, 11)),
                "Paladin 5 with Wis 11 has no spell (no base spell before 6th level, no Wisdom bonus)");
            Assert(GameManager.NpcClassGrantsSpells(ClassStats("Paladin6Wis11", "Paladin", 6, 11)),
                "Paladin 6 with Wis 11 has a base 1st-level spell (PHB p.44)");
            Assert(GameManager.NpcClassGrantsSpells(ClassStats("Ranger6Wis11", "Ranger", 6, 11)),
                "Ranger 6 with Wis 11 has a base 1st-level spell (PHB p.48)");
            Assert(GameManager.NpcClassGrantsSpells(ClassStats("Adept1", "Adept", 1, 12)),
                "Adept 1 grants spells (DMG p.107)");

            Assert(GameManager.ClericDomainMatchesAlignment("Good", Alignment.NeutralGood)
                   && !GameManager.ClericDomainMatchesAlignment("Good", Alignment.TrueNeutral)
                   && !GameManager.ClericDomainMatchesAlignment("Law", Alignment.ChaoticGood)
                   && GameManager.ClericDomainMatchesAlignment("Healing", Alignment.ChaoticEvil),
                "An alignment domain needs a matching alignment, other domains do not (PHB p.32, CHR-078)");
        }

        private static void TestHumanClericSpawnsWithDmgSpells()
        {
            Debug.Log("--- human_cleric spawns with the DMG p.114 sample cleric's spells ---");
            NPCDefinition template = NPCDatabase.Get("human_cleric");
            Assert(template != null, "human_cleric is registered");
            if (template == null)
                return;

            var go = new GameObject("ClericSpellcastingTest_human_cleric");
            CharacterController cc = go.AddComponent<CharacterController>();
            try
            {
                GameManager.Instance.InitializeNPCFromDefinition(cc, template.Clone(), SpawnPos, null, null);
                SpellcastingComponent sc = cc.Spellcasting;
                Assert(sc != null, "human_cleric has a SpellcastingComponent");
                if (sc == null)
                    return;

                CharacterStats s = cc.Stats;
                Assert(s.CharacterAlignment == Alignment.NeutralGood, "human_cleric is neutral good (DMG p.114)", s.CharacterAlignment.ToString());
                Assert(s.ChosenDomains.Contains("Healing") && s.ChosenDomains.Contains("Protection") && s.ChosenDomains.Count == 2,
                    "human_cleric has the Healing and Protection domains", s.DomainsDisplay);
                Assert(s.SpontaneousCasting == SpontaneousCastingType.Cure, "A good cleric converts prepared spells into cure spells (PHB p.32)",
                    s.SpontaneousCasting.ToString());

                int[] totals = { 5, 5, 4, 3 };
                for (int level = 0; level <= 3; level++)
                {
                    int count = sc.SpellSlots.Count(x => x != null && x.Level == level);
                    Assert(count == totals[level], $"human_cleric level-{level} slots: {totals[level]} (DMG p.114: 5/5/4/3)", "got " + count);
                }

                var expected = new List<string>(template.PreparedSpellSlotIds);
                var got = sc.SpellSlots.Where(x => x != null).Select(x => x.HasSpell ? x.PreparedSpell.SpellId : "empty").ToList();
                Assert(got.SequenceEqual(expected), "Every slot holds the definition's spell in order",
                    "got [" + string.Join(", ", got) + "]");

                SpellSlot domain3 = sc.SpellSlots.FirstOrDefault(x => x != null && x.Level == 3 && x.IsDomainSlot);
                Assert(domain3 != null && domain3.HasSpell && domain3.PreparedSpell.SpellId == SpellNames.PROTECTION_FROM_ENERGY,
                    "The 3rd-level domain slot holds Protection from Energy (Protection 3)");
                Assert(sc.CanCastSpell(SpellDatabase.GetSpell(SpellNames.DISPEL_MAGIC)), "human_cleric can cast its prepared Dispel Magic (3rd level)");
                Assert(!sc.CanCastSpell(SpellDatabase.GetSpell(SpellNames.PRAYER)), "human_cleric cannot cast Prayer, which it has not prepared");
            }
            finally
            {
                GameManager gm = GameManager.Instance;
                if (gm != null && gm.Grid != null)
                    gm.Grid.ClearCreatureOccupancy(cc);
                Object.DestroyImmediate(go);
            }
        }
    }
}
