using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// The Blink spell-failure rolls of the cast core (<see cref="BlinkCastRules"/>, PHB p.206) and the one spell resistance
/// check (<see cref="SpellSaveResolver.RollSpellResistance(int, int, int, string)"/>, PHB p.177). Ported from the old
/// SpellResolutionServiceTests when batch 11 step 29 folded SpellResolutionService into the cast core (SPL-056), with
/// the thresholds now pinned by forced dice (ScenarioHooks.RollFilter) instead of statistics:
/// <list type="bullet">
/// <item>A blinking caster's spell fails on a percentile roll of 20 or less and goes off on 21 or more, one roll under
/// <see cref="BlinkCastRules.CasterDiceContext"/>; a caster that is not blinking (or none) rolls nothing.</item>
/// <item>An individually targeted spell fails against a blinking creature on 50 or less and reaches it on 51 or more, one
/// roll under <see cref="BlinkCastRules.TargetDiceContext"/>; no roll for a target that is not blinking, for the caster
/// itself, for a Self or an Area spell, or for no target.</item>
/// <item>Spell resistance: d20 + caster level against SR, one d20 under the caller's label.</item>
/// </list>
/// Edit or Play mode (the creatures are bare controllers with a StatusEffectManager).
/// </summary>
public static class BlinkCastRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== BLINK CAST RULES TESTS ======");

        TestHelpers.EnsureCoreDatabasesInitialized();

        Func<int, string, int, int> oldFilter = ScenarioHooks.RollFilter;
        try
        {
            TestCasterFailure();
            TestTargetFailure();
            TestSpellResistance();
        }
        finally
        {
            ScenarioHooks.RollFilter = oldFilter;
        }

        Debug.Log($"====== Blink Cast Rules Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✅ PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  ❌ FAIL: {testName} {detail}");
        }
    }

    private static CharacterController MakeCharacter(string name)
    {
        var go = new GameObject($"BlinkRules_Test_{name}");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = TestHelpers.CreateStats(name: name, level: 5, characterClass: "Wizard");
        return cc;
    }

    private static SpellData MakeSpell(string name, SpellTargetType targetType = SpellTargetType.SingleEnemy)
    {
        return new SpellData
        {
            Name = name,
            SpellId = name.ToLowerInvariant().Replace(" ", "_"),
            TargetType = targetType
        };
    }

    private static void AddBlink(CharacterController c)
    {
        StatusEffectManager statusMgr = c.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
        {
            statusMgr = c.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(c.Stats);
        }

        statusMgr.ActiveEffects.Add(new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = SpellNames.BLINK, Name = "Blink" },
            CasterName = "UnitTest",
            CasterLevel = 5,
            RemainingRounds = 5,
            DurationType = DurationType.Rounds,
            AffectedCharacterName = c.Stats.CharacterName,
            IsApplied = true
        });
    }

    private static void Destroy(params CharacterController[] characters)
    {
        foreach (CharacterController cc in characters)
            if (cc != null && cc.gameObject != null)
                Object.DestroyImmediate(cc.gameObject);
    }

    /// <summary>Forces every die rolled under a Blink label to <paramref name="value"/> and records the labels.</summary>
    private static void ForceBlink(int value, List<string> labels)
    {
        ScenarioHooks.RollFilter = (sides, label, natural) =>
        {
            if (label == null || !label.StartsWith("Blink", StringComparison.Ordinal))
                return natural;
            labels.Add(label);
            return value;
        };
    }

    private static void TestCasterFailure()
    {
        var labels = new List<string>();
        CharacterController plain = MakeCharacter("Plain_Caster");
        CharacterController blinking = MakeCharacter("Blinking_Caster");
        try
        {
            ForceBlink(1, labels);
            bool failed = BlinkCastRules.TryCasterFailure(plain, "Test Spell", out int roll);
            Assert(!failed && roll == 0 && labels.Count == 0, "Caster not blinking: no failure and no roll", $"roll={roll} labels={labels.Count}");

            failed = BlinkCastRules.TryCasterFailure(null, "Test Spell", out roll);
            Assert(!failed && roll == 0 && labels.Count == 0, "No caster: no failure and no roll", $"roll={roll}");

            AddBlink(blinking);
            Assert(blinking.HasActiveBlinkEffect, "The test caster is blinking");

            ForceBlink(20, labels);
            failed = BlinkCastRules.TryCasterFailure(blinking, "Test Spell", out roll);
            Assert(failed && roll == 20, "Blinking caster: a roll of 20 fails (20%, PHB p.206)", $"failed={failed} roll={roll}");
            Assert(labels.Count == 1 && labels[0] == BlinkCastRules.CasterDiceContext,
                "Blinking caster: one percentile roll under the caster label", string.Join(",", labels));

            labels.Clear();
            ForceBlink(21, labels);
            failed = BlinkCastRules.TryCasterFailure(blinking, "Test Spell", out roll);
            Assert(!failed && roll == 21 && labels.Count == 1, "Blinking caster: a roll of 21 casts", $"failed={failed} roll={roll}");
        }
        finally
        {
            Destroy(plain, blinking);
        }
    }

    private static void TestTargetFailure()
    {
        var labels = new List<string>();
        CharacterController caster = MakeCharacter("Caster");
        CharacterController plainTarget = MakeCharacter("Target_NoBlink");
        CharacterController blinkingTarget = MakeCharacter("Target_Blink");
        SpellData ray = MakeSpell("Ray of Frost");
        try
        {
            ForceBlink(1, labels);
            bool failed = BlinkCastRules.TryTargetFailure(caster, plainTarget, ray, out int roll);
            Assert(!failed && roll == 0 && labels.Count == 0, "Target not blinking: no failure and no roll", $"roll={roll}");

            failed = BlinkCastRules.TryTargetFailure(caster, null, ray, out roll);
            Assert(!failed && roll == 0 && labels.Count == 0, "No target: no failure and no roll", $"roll={roll}");

            AddBlink(caster);
            failed = BlinkCastRules.TryTargetFailure(caster, caster, ray, out roll);
            Assert(!failed && roll == 0 && labels.Count == 0, "The blinking caster's spell on itself: no target roll", $"roll={roll}");

            AddBlink(blinkingTarget);
            failed = BlinkCastRules.TryTargetFailure(caster, blinkingTarget, MakeSpell("Shield", SpellTargetType.Self), out roll);
            Assert(!failed && roll == 0 && labels.Count == 0, "A Self spell: no target roll", $"roll={roll}");

            failed = BlinkCastRules.TryTargetFailure(caster, blinkingTarget, MakeSpell("Fireball", SpellTargetType.Area), out roll);
            Assert(!failed && roll == 0 && labels.Count == 0, "An Area spell is not individually targeted: no target roll", $"roll={roll}");

            ForceBlink(50, labels);
            failed = BlinkCastRules.TryTargetFailure(caster, blinkingTarget, ray, out roll);
            Assert(failed && roll == 50, "Blinking target: a roll of 50 fails (50%, PHB p.206)", $"failed={failed} roll={roll}");
            Assert(labels.Count == 1 && labels[0] == BlinkCastRules.TargetDiceContext,
                "Blinking target: one percentile roll under the target label", string.Join(",", labels));

            labels.Clear();
            ForceBlink(51, labels);
            failed = BlinkCastRules.TryTargetFailure(caster, blinkingTarget, ray, out roll);
            Assert(!failed && roll == 51 && labels.Count == 1, "Blinking target: a roll of 51 reaches it", $"failed={failed} roll={roll}");
        }
        finally
        {
            Destroy(caster, plainTarget, blinkingTarget);
        }
    }

    private static void TestSpellResistance()
    {
        var labels = new List<string>();
        ScenarioHooks.RollFilter = (sides, label, natural) =>
        {
            if (sides != 20)
                return natural;
            labels.Add(label ?? "(none)");
            return 1;
        };
        SRResult low = SpellSaveResolver.RollSpellResistance(10, 0, 5, "test SR");
        Assert(low.Overcame && low.Roll == 1 && low.Total == 11, "SR: d20 1 + CL 10 = 11 overcomes SR 5", $"total={low.Total}");
        Assert(labels.Count == 1 && labels[0] == "test SR", "SR: one d20 under the caller's label", string.Join(",", labels));

        labels.Clear();
        ScenarioHooks.RollFilter = (sides, label, natural) =>
        {
            if (sides != 20)
                return natural;
            labels.Add(label ?? "(none)");
            return 20;
        };
        SRResult high = SpellSaveResolver.RollSpellResistance(1, 0, 30, "test SR high");
        Assert(!high.Overcame && high.Roll == 20 && high.Total == 21,
            "SR: d20 20 + CL 1 = 21 does not overcome SR 30", $"total={high.Total}");
    }
}
}
