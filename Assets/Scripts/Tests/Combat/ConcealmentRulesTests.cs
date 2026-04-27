using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for concealment and fog-area mechanics.
/// Run with ConcealmentRulesTests.RunAll().
/// </summary>
public static class ConcealmentRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CONCEALMENT RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestFogSpellsAreImplementedAsAoEConcealment();
        TestAttackMissesWhenDefenderHasGuaranteedConcealment();
        TestTotalConcealmentPreventsAttackOfOpportunity();

        Debug.Log($"====== Concealment Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(
        string name,
        string className,
        int level,
        Alignment alignment,
        int str,
        int dex,
        int con,
        int wis,
        int intelligence,
        int cha,
        int bab)
    {
        var stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: str,
            dex: dex,
            con: con,
            wis: wis,
            intelligence: intelligence,
            cha: cha,
            bab: bab,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        stats.CharacterAlignment = alignment;
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats)
    {
        var go = new GameObject($"ConcealmentTest_{stats.CharacterName}");
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;

        var spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.Init(stats);

        var statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestFogSpellsAreImplementedAsAoEConcealment()
    {
        SpellData obscuringMist = SpellDatabase.GetSpell("obscuring_mist");
        SpellData fogCloud = SpellDatabase.GetSpell("fog_cloud");

        Assert(obscuringMist != null, "Obscuring Mist definition exists");
        Assert(fogCloud != null, "Fog Cloud definition exists");

        if (obscuringMist != null)
        {
            Assert(!obscuringMist.IsPlaceholder, "Obscuring Mist is not placeholder");
            Assert(obscuringMist.TargetType == SpellTargetType.Area, "Obscuring Mist targets area");
            Assert(obscuringMist.AoEShapeType == AoEShape.Burst && obscuringMist.AoESizeSquares == 4,
                "Obscuring Mist uses 20-ft burst",
                $"shape={obscuringMist.AoEShapeType}, size={obscuringMist.AoESizeSquares}");
            Assert(obscuringMist.DurationType == DurationType.Minutes && obscuringMist.DurationValue == 1 && obscuringMist.DurationScalesWithLevel,
                "Obscuring Mist duration is 1 min/level");
        }

        if (fogCloud != null)
        {
            Assert(!fogCloud.IsPlaceholder, "Fog Cloud is not placeholder");
            Assert(fogCloud.TargetType == SpellTargetType.Area, "Fog Cloud targets area");
            Assert(fogCloud.AoEShapeType == AoEShape.Burst && fogCloud.AoESizeSquares == 4,
                "Fog Cloud uses 20-ft burst",
                $"shape={fogCloud.AoEShapeType}, size={fogCloud.AoESizeSquares}");
            Assert(fogCloud.DurationType == DurationType.Minutes && fogCloud.DurationValue == 10 && fogCloud.DurationScalesWithLevel,
                "Fog Cloud duration is 10 min/level");
        }
    }

    private static void TestAttackMissesWhenDefenderHasGuaranteedConcealment()
    {
        CharacterController attacker = null;
        CharacterController defender = null;

        try
        {
            attacker = CreateController(BuildStats("Attacker", "Fighter", 6, Alignment.TrueNeutral, 20, 14, 14, 10, 10, 10, 6));
            defender = CreateController(BuildStats("Defender", "Wizard", 6, Alignment.TrueNeutral, 10, 10, 10, 10, 16, 10, 3));

            StatusEffectManager statusMgr = defender.GetComponent<StatusEffectManager>();
            var concealmentEffect = new ActiveSpellEffect
            {
                Spell = new SpellData { SpellId = "test_total_concealment", Name = "Test Total Concealment" },
                CasterName = "UnitTest",
                CasterLevel = 1,
                RemainingRounds = 5,
                DurationType = DurationType.Rounds,
                AffectedCharacterName = defender.Stats.CharacterName,
                BonusTypeLegacy = "Concealment",
                BonusTypeEnum = BonusType.Concealment,
                IsApplied = true,
                MissChance = 100,
                IsTotalConcealment = true,
                ConcealmentSource = "Unit Test"
            };
            statusMgr.ActiveEffects.Add(concealmentEffect);

            Random.InitState(123456);
            CombatResult result = attacker.Attack(defender, false, 0, null, null, null, null);

            Assert(result != null, "Attack result returned for concealment test");
            if (result != null)
            {
                Assert(result.MissedDueToConcealment, "Attack misses due to concealment");
                Assert(!result.Hit, "Hit flag cleared after concealment miss");
                Assert(result.ConcealmentMissChance == 100, "Concealment miss chance recorded in result", $"value={result.ConcealmentMissChance}");
            }
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    private static void TestTotalConcealmentPreventsAttackOfOpportunity()
    {
        CharacterController threatener = null;
        CharacterController target = null;

        try
        {
            threatener = CreateController(BuildStats("Threatener", "Fighter", 5, Alignment.TrueNeutral, 18, 12, 14, 10, 10, 10, 5));
            target = CreateController(BuildStats("ConcealedTarget", "Rogue", 5, Alignment.TrueNeutral, 12, 16, 12, 10, 10, 10, 3));

            threatener.Team = CharacterTeam.Player;
            target.Team = CharacterTeam.Enemy;

            StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
            var invisEffect = new ActiveSpellEffect
            {
                Spell = new SpellData { SpellId = "invisibility", Name = "Invisibility" },
                CasterName = "UnitTest",
                CasterLevel = 5,
                RemainingRounds = 5,
                DurationType = DurationType.Rounds,
                AffectedCharacterName = target.Stats.CharacterName,
                BonusTypeLegacy = "Concealment",
                BonusTypeEnum = BonusType.Concealment,
                IsApplied = true,
                MissChance = 50,
                IsTotalConcealment = true,
                ConcealmentSource = "Invisibility"
            };
            statusMgr.ActiveEffects.Add(invisEffect);

            ThreatSystem.ResetAoOForTurn(threatener);
            int usedBefore = threatener.Stats.AttacksOfOpportunityUsed;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(threatener, target);

            Assert(aooResult == null, "AoO prevented by total concealment");
            Assert(threatener.Stats.AttacksOfOpportunityUsed == usedBefore,
                "AoO pool not consumed when total concealment blocks AoO",
                $"before={usedBefore}, after={threatener.Stats.AttacksOfOpportunityUsed}");
        }
        finally
        {
            DestroyController(threatener);
            DestroyController(target);
        }
    }
}
}
