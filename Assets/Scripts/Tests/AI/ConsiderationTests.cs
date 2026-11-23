#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DND35.AI.Knowledge;
using DND35.AI.Personality;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 3 (docs/designs/personalities_batch14.md "Considerations"): the 16 target considerations of the
/// personality term, on fixed views (the StubView pattern of KnowledgeFactsTests).
/// <list type="bullet">
/// <item>A fact the view does not know gives 0 for every consideration; every value lies in [0, 1].</item>
/// <item>WoundSeverity: Unhurt 0, Hurt .33, BadlyHurt .66, a standing creature at 0 hp 1, a downed one 0.</item>
/// <item>Softness: Unarmored 1, Light .67, Medium .33, Heavy 0, another category .5.</item>
/// <item>KillChance: clamp01(damage / hp) times 1 for a caster or healer, .6 otherwise, 0 when Down.</item>
/// <item>Proximity (12 - d) / 11; Clustered min(FoesNear, 3) / 3; LooksDangerous by size, armor and a melee weapon.</item>
/// <item>The flag considerations read their facts (ThreatensObserver, EngagedByAlly, FlankingNow, Fleeing,
/// ThreatensAllyInNeed, DamagedObserver, Caster, Healer, Down, Helpless).</item>
/// </list>
/// Either mode (no creature).
/// </summary>
public static class ConsiderationTests
{
    private static int _passed;
    private static int _failed;

    /// <summary>A view with fixed facts and no creatures (shared with PersonalityBlendTests).</summary>
    public sealed class StubView : ITargetView
    {
        public CharacterController Observer => null;
        public CharacterController Subject => null;
        public readonly Dictionary<FactKey, FactValue> Facts = new Dictionary<FactKey, FactValue>();

        public FactValue Get(FactKey key) => Facts.TryGetValue(key, out FactValue v) ? v : FactValue.Unknown;

        public bool Has(FactKey key, string value)
        {
            FactValue f = Get(key);
            return f.IsKnown && f.Text != null && string.Equals(f.Text, value, StringComparison.OrdinalIgnoreCase);
        }

        public StubView Flag(FactKey key, bool value = true)
        {
            Facts[key] = FactValue.Of(KnowledgeLevel.Confirmed, value);
            return this;
        }

        public StubView Number(FactKey key, int value)
        {
            Facts[key] = FactValue.OfNumber(KnowledgeLevel.Confirmed, value);
            return this;
        }

        public StubView Band(WoundBand band)
        {
            Facts[FactKey.WoundBand] = FactValue.Of(KnowledgeLevel.Confirmed, (int)band, band.ToString());
            return this;
        }

        public StubView Armor(WornArmor armor)
        {
            Facts[FactKey.ArmorCategory] = FactValue.Of(KnowledgeLevel.Confirmed, (int)armor, armor.ToString());
            return this;
        }

        public StubView Size(SizeCategory size)
        {
            Facts[FactKey.Size] = FactValue.Of(KnowledgeLevel.Confirmed, (int)size, size.ToString());
            return this;
        }
    }

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== CONSIDERATION TESTS (batch 14 step 3) ======");

        TestUnknownIsZero();
        TestWoundSeverity();
        TestSoftness();
        TestKillChance();
        TestProximityAndClustered();
        TestLooksDangerous();
        TestFlags();
        TestRange();

        Debug.Log($"====== Consideration Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-4f;

    private static void Expect(ConsiderationId id, StubView view, float expected, string what)
    {
        float v = Considerations.Evaluate(id, view);
        Assert(Near(v, expected), id + ": " + what + " is " + expected, "got " + v);
    }

    private static IEnumerable<ConsiderationId> AllIds()
    {
        foreach (ConsiderationId id in Enum.GetValues(typeof(ConsiderationId)))
            yield return id;
    }

    private static void TestUnknownIsZero()
    {
        Assert(Enum.GetValues(typeof(ConsiderationId)).Length == 16, "16 considerations (the plan's table)");
        var empty = new StubView();
        var nonZero = new List<string>();
        foreach (ConsiderationId id in AllIds())
            if (Considerations.Evaluate(id, empty) != 0f)
                nonZero.Add(id.ToString());
        Assert(nonZero.Count == 0, "a view that knows nothing gives 0 for every consideration", string.Join(", ", nonZero));
        Assert(Considerations.Evaluate(ConsiderationId.Proximity, null) == 0f, "no view gives 0");
    }

    private static void TestWoundSeverity()
    {
        Expect(ConsiderationId.WoundSeverity, new StubView().Band(WoundBand.Unhurt), 0f, "Unhurt");
        Expect(ConsiderationId.WoundSeverity, new StubView().Band(WoundBand.Hurt), 0.33f, "Hurt");
        Expect(ConsiderationId.WoundSeverity, new StubView().Band(WoundBand.BadlyHurt), 0.66f, "BadlyHurt");
        Expect(ConsiderationId.WoundSeverity, new StubView().Band(WoundBand.Down).Flag(FactKey.Down, false), 1f,
            "a standing creature at 0 hp (disabled)");
        Expect(ConsiderationId.WoundSeverity, new StubView().Band(WoundBand.BadlyHurt).Flag(FactKey.Down), 0f,
            "a downed creature (standing targets only)");
        Expect(ConsiderationId.IsDown, new StubView().Flag(FactKey.Down), 1f, "Down");
        Expect(ConsiderationId.IsDown, new StubView().Flag(FactKey.Down, false), 0f, "not Down");
        Expect(ConsiderationId.Helpless, new StubView().Flag(FactKey.Helpless), 1f, "Helpless");
    }

    private static void TestSoftness()
    {
        Expect(ConsiderationId.Softness, new StubView().Armor(WornArmor.Unarmored), 1f, "Unarmored");
        Expect(ConsiderationId.Softness, new StubView().Armor(WornArmor.Light), 0.67f, "Light");
        Expect(ConsiderationId.Softness, new StubView().Armor(WornArmor.Medium), 0.33f, "Medium");
        Expect(ConsiderationId.Softness, new StubView().Armor(WornArmor.Heavy), 0f, "Heavy");
        Expect(ConsiderationId.Softness, new StubView().Armor(WornArmor.Other), 0.5f, "another category");
    }

    private static void TestKillChance()
    {
        StubView plain = new StubView().Number(FactKey.HpCurrent, 10);
        Assert(Near(Considerations.KillChance(plain, 5f), 0.3f), "KillChance: half the hit points of a frontliner is .5 x .6 = .3",
            "got " + Considerations.KillChance(plain, 5f));
        Assert(Near(Considerations.KillChance(plain, 20f), 0.6f), "KillChance: the share clamps at 1 (x .6)");
        Assert(Considerations.KillChance(plain, 0f) == 0f, "KillChance: no expected damage is 0");
        StubView caster = new StubView().Number(FactKey.HpCurrent, 10).Flag(FactKey.Caster);
        Assert(Near(Considerations.KillChance(caster, 5f), 0.5f), "KillChance: a known caster's threat value is 1");
        StubView healer = new StubView().Number(FactKey.HpCurrent, 10).Flag(FactKey.Healer);
        Assert(Near(Considerations.KillChance(healer, 5f), 0.5f), "KillChance: a known healer's threat value is 1");
        StubView down = new StubView().Number(FactKey.HpCurrent, -2).Flag(FactKey.Down).Flag(FactKey.Caster);
        Assert(Considerations.KillChance(down, 5f) == 0f, "KillChance: a Down target's threat value is 0");
        StubView disabled = new StubView().Number(FactKey.HpCurrent, 0).Flag(FactKey.Down, false);
        Assert(Near(Considerations.KillChance(disabled, 1f), 0.6f), "KillChance: a standing target at 0 hp drops to any damage");
        Assert(Considerations.KillChance(new StubView(), 5f) == 0f, "KillChance: unknown hit points give 0");
        Expect(ConsiderationId.KillChance, plain.Number(FactKey.AcEstimate, 15), 0f, "no observer (no expected damage)");
    }

    private static void TestProximityAndClustered()
    {
        Expect(ConsiderationId.Proximity, new StubView().Number(FactKey.Distance, 1), 1f, "adjacent");
        Expect(ConsiderationId.Proximity, new StubView().Number(FactKey.Distance, 4), 8f / 11f, "4 squares");
        Expect(ConsiderationId.Proximity, new StubView().Number(FactKey.Distance, 12), 0f, "12 squares");
        Expect(ConsiderationId.Proximity, new StubView().Number(FactKey.Distance, 30), 0f, "30 squares");
        Expect(ConsiderationId.Proximity, new StubView().Number(FactKey.Distance, 0), 1f, "0 squares (clamped)");
        Expect(ConsiderationId.Clustered, new StubView().Number(FactKey.FoesNear, 0), 0f, "no foe near");
        Expect(ConsiderationId.Clustered, new StubView().Number(FactKey.FoesNear, 1), 1f / 3f, "one foe near");
        Expect(ConsiderationId.Clustered, new StubView().Number(FactKey.FoesNear, 3), 1f, "three foes near");
        Expect(ConsiderationId.Clustered, new StubView().Number(FactKey.FoesNear, 6), 1f, "six foes near (saturated)");
    }

    private static void TestLooksDangerous()
    {
        Expect(ConsiderationId.LooksDangerous, new StubView().Size(SizeCategory.Large), 1f, "larger than a Medium observer");
        Expect(ConsiderationId.LooksDangerous, new StubView().Size(SizeCategory.Medium).Armor(WornArmor.Heavy).Flag(FactKey.WieldsMelee), 1f,
            "heavy armor and a melee weapon");
        Expect(ConsiderationId.LooksDangerous, new StubView().Size(SizeCategory.Medium).Armor(WornArmor.Heavy), 0.5f, "heavy armor alone");
        Expect(ConsiderationId.LooksDangerous, new StubView().Size(SizeCategory.Medium).Armor(WornArmor.Medium).Flag(FactKey.WieldsMelee), 0.5f,
            "medium armor and a melee weapon");
        Expect(ConsiderationId.LooksDangerous, new StubView().Size(SizeCategory.Medium).Armor(WornArmor.Light).Flag(FactKey.WieldsMelee), 0f,
            "light armor and a melee weapon");
        Expect(ConsiderationId.LooksDangerous, new StubView().Size(SizeCategory.Small).Armor(WornArmor.Unarmored), 0f, "a small unarmored creature");
    }

    private static void TestFlags()
    {
        var map = new (ConsiderationId id, FactKey key)[]
        {
            (ConsiderationId.KnownCaster, FactKey.Caster),
            (ConsiderationId.KnownHealer, FactKey.Healer),
            (ConsiderationId.ThreatensMe, FactKey.ThreatensObserver),
            (ConsiderationId.EngagedByAlly, FactKey.EngagedByAlly),
            (ConsiderationId.FlankOpportunity, FactKey.FlankingNow),
            (ConsiderationId.Fleeing, FactKey.Fleeing),
            (ConsiderationId.ThreatToWard, FactKey.ThreatensAllyInNeed),
            (ConsiderationId.HurtMe, FactKey.DamagedObserver)
        };
        foreach (var (id, key) in map)
        {
            Expect(id, new StubView().Flag(key), 1f, key + " known true");
            Expect(id, new StubView().Flag(key, false), 0f, key + " known false");
        }
        var hurt = new StubView();
        hurt.Facts[FactKey.DamagedObserver] = new FactValue(KnowledgeLevel.Confirmed, true, 7, "true");
        Expect(ConsiderationId.HurtMe, hurt, 1f, "7 damage taken from it (a flag, not the amount)");
    }

    private static void TestRange()
    {
        var views = new List<StubView>
        {
            new StubView().Band(WoundBand.BadlyHurt).Armor(WornArmor.Unarmored).Number(FactKey.Distance, 2).Number(FactKey.FoesNear, 9)
                .Number(FactKey.HpCurrent, 3).Flag(FactKey.Caster).Flag(FactKey.ThreatensObserver).Size(SizeCategory.Huge),
            new StubView().Band(WoundBand.Down).Flag(FactKey.Down).Flag(FactKey.Helpless).Number(FactKey.Distance, -3).Number(FactKey.FoesNear, -1),
            new StubView().Armor(WornArmor.Other).Number(FactKey.HpCurrent, 1000).Number(FactKey.Distance, 99)
        };
        var outOfRange = new List<string>();
        for (int v = 0; v < views.Count; v++)
            foreach (ConsiderationId id in AllIds())
            {
                float x = Considerations.Evaluate(id, views[v]);
                if (x < 0f || x > 1f || float.IsNaN(x))
                    outOfRange.Add("view " + v + " " + id + "=" + x);
            }
        Assert(outOfRange.Count == 0, "every consideration lies in [0, 1] on odd views", string.Join("; ", outOfRange));
    }
}
}
#endif
