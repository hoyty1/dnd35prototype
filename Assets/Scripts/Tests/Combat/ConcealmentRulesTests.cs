using UnityEngine;
using DND35e.Identifiers;

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
        TestDarknessSpellDefinitionAndConcealment();
        TestDarknessDoesNotBlockVisionAndGrantsConcealmentThroughArea();
        TestAttackMissesWhenDefenderHasGuaranteedConcealment();
        TestTotalConcealmentPreventsAttackOfOpportunity();
        TestObscuringMistAppliesConcealmentToTargetsInsideWhenAttackerOutside();

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
        SpellData obscuringMist = SpellDatabase.GetSpell(SpellNames.OBSCURING_MIST);
        SpellData fogCloud = SpellDatabase.GetSpell(SpellNames.FOG_CLOUD);

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

    private static void TestDarknessSpellDefinitionAndConcealment()
    {
        SpellData darkness = SpellDatabase.GetSpell(SpellNames.DARKNESS);
        Assert(darkness != null, "Darkness definition exists");

        if (darkness != null)
        {
            Assert(!darkness.IsPlaceholder, "Darkness is not placeholder");
            Assert(darkness.TargetType == SpellTargetType.Area, "Darkness targets area");
            Assert(darkness.RangeCategory == SpellRangeCategory.Touch, "Darkness uses touch range");
            Assert(darkness.AoEShapeType == AoEShape.Burst && darkness.AoESizeSquares == 4,
                "Darkness uses 20-ft radius burst",
                $"shape={darkness.AoEShapeType}, size={darkness.AoESizeSquares}");
            Assert(darkness.DurationType == DurationType.Minutes && darkness.DurationValue == 10 && darkness.DurationScalesWithLevel,
                "Darkness duration is 10 min/level");
            Assert(darkness.IsDismissible, "Darkness is dismissible");
        }

        CharacterController attacker = null;
        CharacterController target = null;
        DarknessAreaEffect darknessArea = null;

        try
        {
            attacker = CreateController(BuildStats("DarknessAttacker", "Fighter", 6, Alignment.TrueNeutral, 16, 14, 14, 10, 10, 10, 6));
            target = CreateController(BuildStats("DarknessTarget", "Wizard", 6, Alignment.TrueNeutral, 10, 12, 10, 10, 16, 10, 3));

            attacker.GridPosition = new Vector2Int(0, 0);
            target.GridPosition = new Vector2Int(2, 0);

            GameObject darknessObject = new GameObject("ConcealmentTest_Darkness");
            darknessArea = darknessObject.AddComponent<DarknessAreaEffect>();
            darknessArea.AffectedCells.Add(target.GridPosition);

            int missChance = darknessArea.GetConcealmentMissChance(attacker, target);
            Assert(missChance == 20,
                "Darkness grants 20% miss chance to targets inside area",
                $"missChance={missChance}");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(target);

            if (darknessArea != null)
                Object.DestroyImmediate(darknessArea.gameObject);
        }
    }

    private static void TestDarknessDoesNotBlockVisionAndGrantsConcealmentThroughArea()
    {
        CharacterController observer = null;
        CharacterController target = null;
        DarknessAreaEffect darknessArea = null;

        try
        {
            observer = CreateController(BuildStats("VisionObserver", "Fighter", 5, Alignment.TrueNeutral, 16, 12, 14, 10, 10, 10, 5));
            target = CreateController(BuildStats("VisionTarget", "Rogue", 5, Alignment.TrueNeutral, 12, 16, 12, 10, 10, 10, 3));

            observer.GridPosition = new Vector2Int(0, 0);
            target.GridPosition = new Vector2Int(6, 0);

            GameObject darknessObject = new GameObject("ConcealmentTest_DarknessVision");
            darknessArea = darknessObject.AddComponent<DarknessAreaEffect>();

            // Create a darkness strip between observer and target to verify "through" concealment.
            darknessArea.AffectedCells.Add(new Vector2Int(3, 0));
            darknessArea.AffectedCells.Add(new Vector2Int(4, 0));
            AreaEffectManager.Instance.RegisterAreaEffect(darknessArea);

            Assert(!DarknessAreaEffect.BlocksVision(observer, target),
                "Darkness does not block vision through an intervening dark area");
            Assert(observer.CanSee(target),
                "CharacterController.CanSee is not blocked by darkness");

            int throughMissChance = DarknessAreaEffect.GetAttackConcealmentMissChance(observer, target);
            Assert(throughMissChance == 20,
                "Attacks through darkness get 20% miss chance",
                $"missChance={throughMissChance}");

            // Move target into darkness to verify target-in-darkness concealment.
            target.GridPosition = new Vector2Int(3, 0);
            int targetInDarknessMissChance = DarknessAreaEffect.GetAttackConcealmentMissChance(observer, target);
            Assert(targetInDarknessMissChance == 20,
                "Targets in darkness get 20% miss chance",
                $"missChance={targetInDarknessMissChance}");

            // Move observer into darkness and target outside to verify attacker-in-darkness concealment.
            observer.GridPosition = new Vector2Int(4, 0);
            target.GridPosition = new Vector2Int(7, 0);
            int attackerInDarknessMissChance = DarknessAreaEffect.GetAttackConcealmentMissChance(observer, target);
            Assert(attackerInDarknessMissChance == 20,
                "Attackers in darkness get 20% miss chance",
                $"missChance={attackerInDarknessMissChance}");
        }
        finally
        {
            DestroyController(observer);
            DestroyController(target);

            if (darknessArea != null)
            {
                AreaEffectManager.Instance.UnregisterAreaEffect(darknessArea);
                Object.DestroyImmediate(darknessArea.gameObject);
            }
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

            threatener.SetTeam(CharacterTeam.Player);
            target.SetTeam(CharacterTeam.Enemy);

            StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
            var invisEffect = new ActiveSpellEffect
            {
                Spell = new SpellData { SpellId = SpellNames.INVISIBILITY, Name = "Invisibility" },
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

    private static void TestObscuringMistAppliesConcealmentToTargetsInsideWhenAttackerOutside()
    {
        CharacterController attacker = null;
        CharacterController target = null;
        ObscuringMistAreaEffect mist = null;

        try
        {
            attacker = CreateController(BuildStats("OutsideArcher", "Fighter", 6, Alignment.TrueNeutral, 14, 16, 12, 10, 10, 10, 6));
            target = CreateController(BuildStats("InsideTarget", "Wizard", 6, Alignment.TrueNeutral, 10, 12, 10, 10, 16, 10, 3));

            attacker.GridPosition = new Vector2Int(0, 0);
            target.GridPosition = new Vector2Int(3, 0);

            GameObject mistObject = new GameObject("ConcealmentTest_ObscuringMist");
            mist = mistObject.AddComponent<ObscuringMistAreaEffect>();

            // Simulate only the target being inside the mist area.
            mist.AffectedCells.Add(target.GridPosition);

            int farMissChance = mist.GetConcealmentMissChance(attacker, target);
            Assert(farMissChance == 50,
                "Obscuring Mist grants total concealment to target inside mist when attacker is outside beyond 5 ft",
                $"missChance={farMissChance}");

            // Move attacker adjacent to the target while still outside the mist area.
            attacker.GridPosition = new Vector2Int(2, 0);
            int nearMissChance = mist.GetConcealmentMissChance(attacker, target);
            Assert(nearMissChance == 20,
                "Obscuring Mist grants 20% concealment to target inside mist when attacker is outside within 5 ft",
                $"missChance={nearMissChance}");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(target);

            if (mist != null)
                Object.DestroyImmediate(mist.gameObject);
        }
    }
}
}
