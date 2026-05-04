using UnityEngine;
using DND35e.Identifiers;
using Tests.Utilities;

namespace Tests.Combat
{
/// <summary>
/// Regression tests for reusable creature immunity/mindless rules (D&D 3.5e).
///
/// Sources:
/// - Monster Manual (3.5): Lemure (devil traits: poison + fire immunity)
/// - SRD/Monster Manual glossary: Mindless creatures are immune to mind-affecting effects.
/// </summary>
public static class CreatureImmunityRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CREATURE IMMUNITY RULES TESTS ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        NPCDatabase.Init();

        TestLemureDefinitionCarriesMindlessAndImmunityFlags();
        TestLemureUsesNoScoreIntelligence();
        TestLemureNotHelplessFromNoIntelligenceScore();
        TestLemurePoisonImmunityPreventsPoisonApplication();
        TestLemureFireImmunityBlocksFireDamage();
        TestLemureMindAffectingImmunityBlocksCharmPerson();
        TestMindlessCreatureIsImmuneToIntelligenceDamage();
        TestFighterReducedToIntZeroBecomesComatoseAndHelpless();
        TestNoScoreDisplaysAsEmDash();
        TestNonImmuneTargetCanStillReceivePoison();

        // Monster Manual swarm regression coverage (Bat/Rat/Spider swarms).
        TestBatSwarmDefinitionMatchesMonsterManual();
        TestRatSwarmDefinitionMatchesMonsterManual();
        TestSpiderSwarmDefinitionMatchesMonsterManual();
        TestSwarmsTakeZeroWeaponDamageFromNonFireWeapons();
        TestSwarmsTakeWeaponFireDamageFromTorchLikeAttacks();
        TestSwarmsTakeFullFireSpellDamage();

        Debug.Log($"====== Creature Immunity Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildLemureLikeStats()
    {
        CharacterStats stats = TestHelpers.CreateStats(
            name: "Lemure",
            level: 2,
            characterClass: "Warrior",
            str: 11,
            dex: 10,
            con: 12,
            wis: 11,
            intelligence: CharacterStats.NO_SCORE,
            cha: 5,
            bab: 2,
            raceName: "Human");

        stats.CreatureType = "Outsider";
        stats.Immunities = ImmunityPresets.Combine(
            ImmunityPresets.DevilImmunities(),
            ImmunityPresets.MindlessImmunities());
        stats.AddDamageImmunity(DamageType.Fire);
        stats.ApplyMindlessTrait(true);
        return stats;
    }

    private static void TestLemureDefinitionCarriesMindlessAndImmunityFlags()
    {
        NPCDefinition lemure = NPCDatabase.Get("lemure");
        bool ok = lemure != null
                  && lemure.IsMindless
                  && lemure.Immunities != null
                  && lemure.Immunities.immuneToPoison
                  && lemure.Immunities.immuneToFire
                  && lemure.Immunities.immuneToMindAffecting;

        Assert(ok,
            "Lemure definition has mindless + poison/fire/mind-affecting immunities",
            lemure == null
                ? "(lemure missing)"
                : $"(mindless={lemure.IsMindless}, poison={lemure.Immunities?.immuneToPoison}, fire={lemure.Immunities?.immuneToFire}, mind={lemure.Immunities?.immuneToMindAffecting})");
    }

    private static void TestLemureUsesNoScoreIntelligence()
    {
        CharacterStats lemure = BuildLemureLikeStats();
        bool ok = lemure.INT == CharacterStats.NO_SCORE
                  && !lemure.HasIntelligence()
                  && lemure.IntelligenceDisplay == "—";

        Assert(ok,
            "Lemure uses NO_SCORE for Intelligence",
            $"(int={lemure.INT}, hasInt={lemure.HasIntelligence()}, display={lemure.IntelligenceDisplay})");
    }

    private static void TestLemureNotHelplessFromNoIntelligenceScore()
    {
        CharacterStats lemure = BuildLemureLikeStats();
        Assert(!lemure.IsHelplessFromAbilityScore(),
            "Lemure is not helpless from natural no-score Intelligence",
            $"(int={lemure.INT}, effInt={lemure.EffectiveINTScore}, helpless={lemure.IsHelplessFromAbilityScore()})");
    }

    private static void TestLemurePoisonImmunityPreventsPoisonApplication()
    {
        CharacterController lemureController = null;
        try
        {
            GameObject go = new GameObject("Lemure_Poison_Test");
            lemureController = go.AddComponent<CharacterController>();
            CharacterStats lemureStats = BuildLemureLikeStats();
            lemureController.Init(lemureStats, Vector2Int.zero, null, null);

            lemureController.ApplyPoison("black_adder_venom");

            Assert(lemureController.ActivePoisons.Count == 0,
                "Poison immunity prevents poison from being applied to Lemure",
                $"(activePoisons={lemureController.ActivePoisons.Count})");
        }
        finally
        {
            TestHelpers.Cleanup(lemureController != null ? lemureController.gameObject : null);
        }
    }

    private static void TestLemureFireImmunityBlocksFireDamage()
    {
        CharacterStats lemure = BuildLemureLikeStats();
        int hpBefore = lemure.CurrentHP;

        DamagePacket packet = new DamagePacket
        {
            RawDamage = 12,
            Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Test Fire"
        };

        DamageResolutionResult result = lemure.ApplyIncomingDamage(12, packet);

        bool ok = result.ImmunityTriggered && result.FinalDamage == 0 && lemure.CurrentHP == hpBefore;
        Assert(ok,
            "Fire immunity blocks fire damage on Lemure",
            $"(immunityTriggered={result.ImmunityTriggered}, final={result.FinalDamage}, hpBefore={hpBefore}, hpAfter={lemure.CurrentHP})");
    }

    private static void TestLemureMindAffectingImmunityBlocksCharmPerson()
    {
        CharacterStats caster = TestHelpers.CreateStats(name: "Wizard", characterClass: "Wizard", level: 5, intelligence: 18, bab: 2);
        CharacterStats lemure = BuildLemureLikeStats();
        SpellData charm = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.CHARM_PERSON);

        SpellResult result = SpellCaster.Cast(charm, caster, lemure);

        Assert(result != null && result.MindAffectingImmunityBlocked,
            "Mindless Lemure blocks mind-affecting Charm Person",
            result == null ? "(no result)" : $"(blocked={result.MindAffectingImmunityBlocked}, reason={result.NoEffectReason})");
    }

    private static void TestMindlessCreatureIsImmuneToIntelligenceDamage()
    {
        CharacterController lemureController = null;
        try
        {
            GameObject go = new GameObject("Lemure_Int_Damage_Test");
            lemureController = go.AddComponent<CharacterController>();
            lemureController.Init(BuildLemureLikeStats(), Vector2Int.zero, null, null);

            int intDamageBefore = lemureController.Stats.AbilityScoreDamage.IntelligenceDamage;
            lemureController.ApplyAbilityDamage(AbilityType.INT, 3, "Unit Test");
            int intDamageAfter = lemureController.Stats.AbilityScoreDamage.IntelligenceDamage;

            Assert(intDamageAfter == intDamageBefore,
                "Mindless creatures are immune to Intelligence damage",
                $"(before={intDamageBefore}, after={intDamageAfter})");
        }
        finally
        {
            TestHelpers.Cleanup(lemureController != null ? lemureController.gameObject : null);
        }
    }

    private static void TestFighterReducedToIntZeroBecomesComatoseAndHelpless()
    {
        CharacterController fighter = null;
        try
        {
            fighter = TestHelpers.CreateCharacter(name: "Fighter_Int_Zero", characterClass: "Fighter", level: 3, intelligence: 10);
            fighter.ApplyAbilityDrain(AbilityType.INT, 10, "Unit Test");

            bool helpless = fighter.Stats.IsHelplessFromAbilityScore() && fighter.HasCondition(CombatConditionType.Helpless);
            bool comatose = fighter.Stats.IsComatoseFromAbilityScore() && fighter.HasCondition(CombatConditionType.Unconscious);

            Assert(helpless && comatose,
                "Fighter reduced to Int 0 becomes comatose and helpless",
                $"(effInt={fighter.Stats.EffectiveINTScore}, helpless={helpless}, comatose={comatose})");
        }
        finally
        {
            TestHelpers.Cleanup(fighter != null ? fighter.gameObject : null);
        }
    }

    private static void TestNoScoreDisplaysAsEmDash()
    {
        CharacterStats lemure = BuildLemureLikeStats();
        Assert(lemure.GetAbilityScoreDisplay(AbilityType.INT) == "—"
               && CharacterStats.GetAbilityScoreDisplay(CharacterStats.NO_SCORE) == "—",
            "NO_SCORE displays as em dash",
            $"(display={lemure.GetAbilityScoreDisplay(AbilityType.INT)})");
    }

    private static void TestNonImmuneTargetCanStillReceivePoison()
    {
        CharacterController target = null;
        try
        {
            target = TestHelpers.CreateCharacter(name: "Non-Immune Target", characterClass: "Fighter", level: 3, intelligence: 10);
            target.ApplyPoison("black_adder_venom");

            Assert(target.ActivePoisons.Count >= 1,
                "Non-immune target can still receive poison",
                $"(activePoisons={target.ActivePoisons.Count})");
        }
        finally
        {
            TestHelpers.Cleanup(target != null ? target.gameObject : null);
        }
    }

    private static void TestBatSwarmDefinitionMatchesMonsterManual()
    {
        NPCDefinition batSwarm = NPCDatabase.Get("bat_swarm");
        bool ok = batSwarm != null
                  && batSwarm.CreatureType == "Animal"
                  && batSwarm.HitDice == 3
                  && batSwarm.BaseHitDieHP == 13
                  && batSwarm.BaseAttackBonusOverride == 2
                  && batSwarm.STR == 3
                  && batSwarm.DEX == 15
                  && batSwarm.CON == 10
                  && batSwarm.INT == 2
                  && batSwarm.WIS == 12
                  && batSwarm.CHA == 4
                  && batSwarm.IsSwarm
                  && !batSwarm.CanMakeAttacksOfOpportunity
                  && batSwarm.SizeCategory == SizeCategory.Large
                  && batSwarm.SwarmTraits != null
                  && batSwarm.SwarmTraits.SwarmDamageDice == "1d6"
                  && batSwarm.SwarmTraits.DistractionDC == 11
                  && batSwarm.SwarmTraits.HasWounding
                  && batSwarm.Immunities != null
                  && batSwarm.Immunities.immuneToWeaponDamage;

        Assert(ok,
            "Bat Swarm definition matches Monster Manual core swarm stats",
            batSwarm == null
                ? "(definition missing)"
                : $"(HD={batSwarm.HitDice}, HP={batSwarm.BaseHitDieHP}, BAB={batSwarm.BaseAttackBonusOverride}, swarm={batSwarm.IsSwarm}, aoo={batSwarm.CanMakeAttacksOfOpportunity})");
    }

    private static void TestRatSwarmDefinitionMatchesMonsterManual()
    {
        NPCDefinition ratSwarm = NPCDatabase.Get("rat_swarm");
        bool ok = ratSwarm != null
                  && ratSwarm.CreatureType == "Animal"
                  && ratSwarm.HitDice == 4
                  && ratSwarm.BaseHitDieHP == 18
                  && ratSwarm.BaseAttackBonusOverride == 3
                  && ratSwarm.STR == 2
                  && ratSwarm.DEX == 15
                  && ratSwarm.CON == 10
                  && ratSwarm.INT == 2
                  && ratSwarm.WIS == 12
                  && ratSwarm.CHA == 2
                  && ratSwarm.HasScent
                  && ratSwarm.IsSwarm
                  && !ratSwarm.CanMakeAttacksOfOpportunity
                  && ratSwarm.SizeCategory == SizeCategory.Large
                  && ratSwarm.SwarmTraits != null
                  && ratSwarm.SwarmTraits.SwarmDamageDice == "1d6"
                  && ratSwarm.SwarmTraits.DistractionDC == 12
                  && ratSwarm.SwarmTraits.HasDisease
                  && ratSwarm.SwarmTraits.DiseaseType == DiseaseType.FilthFever
                  && ratSwarm.Immunities != null
                  && ratSwarm.Immunities.immuneToWeaponDamage;

        Assert(ok,
            "Rat Swarm definition matches Monster Manual core swarm stats",
            ratSwarm == null
                ? "(definition missing)"
                : $"(HD={ratSwarm.HitDice}, HP={ratSwarm.BaseHitDieHP}, BAB={ratSwarm.BaseAttackBonusOverride}, disease={ratSwarm.SwarmTraits?.DiseaseType})");
    }

    private static void TestSpiderSwarmDefinitionMatchesMonsterManual()
    {
        NPCDefinition spiderSwarm = NPCDatabase.Get("spider_swarm");
        bool ok = spiderSwarm != null
                  && spiderSwarm.CreatureType == "Vermin"
                  && spiderSwarm.HitDice == 2
                  && spiderSwarm.BaseHitDieHP == 9
                  && spiderSwarm.BaseAttackBonusOverride == 1
                  && spiderSwarm.STR == 1
                  && spiderSwarm.DEX == 17
                  && spiderSwarm.CON == 10
                  && spiderSwarm.INT == CharacterStats.NO_SCORE
                  && spiderSwarm.WIS == 10
                  && spiderSwarm.CHA == 2
                  && spiderSwarm.IsMindless
                  && spiderSwarm.IsSwarm
                  && !spiderSwarm.CanMakeAttacksOfOpportunity
                  && spiderSwarm.SizeCategory == SizeCategory.Large
                  && spiderSwarm.SwarmTraits != null
                  && spiderSwarm.SwarmTraits.SwarmDamageDice == "1d6"
                  && spiderSwarm.SwarmTraits.DistractionDC == 11
                  && spiderSwarm.SwarmTraits.HasPoison
                  && spiderSwarm.SwarmTraits.PoisonId == "medium_spider_poison"
                  && spiderSwarm.SwarmTraits.PoisonDcModifier == -1
                  && spiderSwarm.Immunities != null
                  && spiderSwarm.Immunities.immuneToWeaponDamage
                  && spiderSwarm.Immunities.immuneToMindAffecting;

        Assert(ok,
            "Spider Swarm definition matches Monster Manual core swarm stats",
            spiderSwarm == null
                ? "(definition missing)"
                : $"(HD={spiderSwarm.HitDice}, HP={spiderSwarm.BaseHitDieHP}, BAB={spiderSwarm.BaseAttackBonusOverride}, int={spiderSwarm.INT}, mindless={spiderSwarm.IsMindless})");
    }

    private static void TestSwarmsTakeZeroWeaponDamageFromNonFireWeapons()
    {
        CharacterStats bat = BuildStatsFromNpcDefinition("bat_swarm");
        CharacterStats rat = BuildStatsFromNpcDefinition("rat_swarm");
        CharacterStats spider = BuildStatsFromNpcDefinition("spider_swarm");

        DamagePacket swordPacket = new DamagePacket
        {
            RawDamage = 9,
            Source = AttackSource.Weapon,
            SourceName = "Longsword",
            Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Slashing }
        };

        DamageResolutionResult batResult = bat.ApplyIncomingDamage(9, swordPacket);
        DamageResolutionResult ratResult = rat.ApplyIncomingDamage(9, swordPacket);
        DamageResolutionResult spiderResult = spider.ApplyIncomingDamage(9, swordPacket);

        bool ok = batResult.FinalDamage == 0 && batResult.ImmunityTriggered
                  && ratResult.FinalDamage == 0 && ratResult.ImmunityTriggered
                  && spiderResult.FinalDamage == 0 && spiderResult.ImmunityTriggered;

        Assert(ok,
            "Swarms are immune to non-fire weapon damage (sword test)",
            $"(bat={batResult.FinalDamage}, rat={ratResult.FinalDamage}, spider={spiderResult.FinalDamage})");
    }

    private static void TestSwarmsTakeWeaponFireDamageFromTorchLikeAttacks()
    {
        CharacterStats rat = BuildStatsFromNpcDefinition("rat_swarm");

        DamagePacket torchPacket = new DamagePacket
        {
            RawDamage = 2,
            Source = AttackSource.Weapon,
            SourceName = "Torch",
            Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Fire }
        };

        DamageResolutionResult result = rat.ApplyIncomingDamage(2, torchPacket);
        bool ok = !result.ImmunityTriggered && result.FinalDamage == 2;

        Assert(ok,
            "Swarms take torch-like weapon fire damage (1d3 range preserved by caller)",
            $"(final={result.FinalDamage}, immunity={result.ImmunityTriggered})");
    }

    private static void TestSwarmsTakeFullFireSpellDamage()
    {
        CharacterStats spider = BuildStatsFromNpcDefinition("spider_swarm");

        DamagePacket fireSpellPacket = new DamagePacket
        {
            RawDamage = 11,
            Source = AttackSource.Spell,
            SourceName = "Burning Hands",
            Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Fire }
        };

        DamageResolutionResult result = spider.ApplyIncomingDamage(11, fireSpellPacket);
        bool ok = !result.ImmunityTriggered && result.FinalDamage == 11;

        Assert(ok,
            "Swarms take full fire spell damage",
            $"(final={result.FinalDamage}, immunity={result.ImmunityTriggered})");
    }

    private static CharacterStats BuildStatsFromNpcDefinition(string npcId)
    {
        NPCDefinition def = NPCDatabase.Get(npcId);
        Assert(def != null, $"NPC definition exists: {npcId}");
        if (def == null)
            return TestHelpers.CreateStats(name: $"Missing_{npcId}");

        CharacterStats stats = new CharacterStats(
            name: def.Name,
            level: Mathf.Max(1, def.Level),
            characterClass: def.CharacterClass,
            str: def.STR,
            dex: def.DEX,
            con: def.CON,
            wis: def.WIS,
            intelligence: def.INT,
            cha: def.CHA,
            bab: def.BaseAttackBonusOverride ?? Mathf.Max(0, def.BAB),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 0,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: def.BaseSpeed,
            atkRange: 1,
            baseHitDieHP: Mathf.Max(1, def.BaseHitDieHP));

        stats.SetBaseSizeCategory(def.SizeCategory);
        stats.CreatureType = def.CreatureType;
        stats.Immunities = def.Immunities != null ? def.Immunities.Clone() : new CreatureImmunities();
        stats.IsSwarm = def.IsSwarm;
        stats.SwarmTraits = def.SwarmTraits != null
            ? new SwarmTraits
            {
                IsSwarm = def.SwarmTraits.IsSwarm,
                SwarmDamage = def.SwarmTraits.SwarmDamage,
                SwarmDamageDice = def.SwarmTraits.SwarmDamageDice,
                DistractionDC = def.SwarmTraits.DistractionDC,
                HasPoison = def.SwarmTraits.HasPoison,
                HasDisease = def.SwarmTraits.HasDisease,
                HasWounding = def.SwarmTraits.HasWounding,
                SwarmDamageType = def.SwarmTraits.SwarmDamageType,
                PoisonId = def.SwarmTraits.PoisonId,
                PoisonDcModifier = def.SwarmTraits.PoisonDcModifier,
                DiseaseType = def.SwarmTraits.DiseaseType,
                DiseaseDcModifier = def.SwarmTraits.DiseaseDcModifier
            }
            : new SwarmTraits();
        stats.CanMakeAttacksOfOpportunity = def.CanMakeAttacksOfOpportunity;
        stats.ApplyMindlessTrait(def.IsMindless);

        return stats;
    }
}
}
