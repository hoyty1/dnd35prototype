using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Animate Rope implementation details.
/// Run with AnimateRopeRulesTests.RunAll().
/// </summary>
public static class AnimateRopeRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== ANIMATE ROPE RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestAnimateRopeSpellDefinition();
        TestRopeItemsDefinition();
        TestEntangledConditionDefinitionMatchesPhb();
        TestEntangledHalvesSpeedAndBlocksRunCharge();
        TestAnimateRopeEntangledTargetCanStillMoveAtHalfSpeed();
        TestEntangledConcentrationDcScalesWithSpellLevel();
        TestAnimateRopeConditionDataUsesStandardEntangledMetadata();

        Debug.Log($"====== Animate Rope Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name)
    {
        return new CharacterStats(
            name: name,
            level: 5,
            characterClass: "Wizard",
            str: 10,
            dex: 14,
            con: 14,
            wis: 12,
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
            baseHitDieHP: 20,
            raceName: "Human");
    }

    private static void TestAnimateRopeSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell("animate_rope");
        Assert(spell != null, "Animate Rope spell exists");
        if (spell == null)
            return;

        Assert(spell.IsRangedTouchSpell(), "Animate Rope is a ranged touch spell");
        Assert(spell.AllowsSavingThrow && spell.SavingThrowType == "Reflex", "Animate Rope uses Reflex save");
        Assert(spell.GetRangeSquaresForCasterLevel(20) == 10, "Animate Rope max range is 10 squares (50 ft)",
            $"observed={spell.GetRangeSquaresForCasterLevel(20)}");
    }

    private static void TestRopeItemsDefinition()
    {
        ItemData hemp = ItemDatabase.Get("rope_hemp");
        ItemData silk = ItemDatabase.Get("rope_silk");

        Assert(hemp is RopeItemData, "Hemp rope uses RopeItemData");
        Assert(silk is RopeItemData, "Silk rope uses RopeItemData");

        RopeItemData hempRope = hemp as RopeItemData;
        RopeItemData silkRope = silk as RopeItemData;

        Assert(hempRope != null && hempRope.BreakDC == 24, "Hemp rope break DC is 24");
        Assert(silkRope != null && silkRope.BreakDC == 23, "Silk rope break DC is 23");
    }

    private static void TestEntangledConditionDefinitionMatchesPhb()
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Entangled);
        Assert(def != null, "Entangled condition definition exists");
        if (def == null)
            return;

        Assert(def.AttackModifier == -2, "Entangled applies -2 attack penalty", $"observed={def.AttackModifier}");
        Assert(!def.PreventsMovement, "General Entangled does not hard-prevent movement");
        Assert(Mathf.Approximately(def.MovementMultiplier, 0.5f),
            "General Entangled applies half-speed movement",
            $"observed={def.MovementMultiplier}");
    }

    private static void TestEntangledHalvesSpeedAndBlocksRunCharge()
    {
        CharacterStats stats = BuildStats("EntangledTarget");
        int baseSpeedFeet = stats.EffectiveSpeedFeet;

        stats.ApplyCondition(CombatConditionType.Entangled, 3, "UnitTest");

        int expectedSpeedFeet = Mathf.FloorToInt((baseSpeedFeet * 0.5f) / 5f) * 5;
        Assert(stats.EffectiveSpeedFeet == expectedSpeedFeet,
            "Entangled halves movement speed",
            $"expected={expectedSpeedFeet}, observed={stats.EffectiveSpeedFeet}");
        Assert(!stats.CanRun && !stats.CanCharge,
            "Entangled prevents run and charge actions");
    }

    private static void TestAnimateRopeEntangledTargetCanStillMoveAtHalfSpeed()
    {
        CharacterStats stats = BuildStats("AnimateRopeTarget");
        int baseSpeedFeet = stats.EffectiveSpeedFeet;

        stats.ApplyCondition(CombatConditionType.Entangled, 3, "Animate Rope");

        int expectedSpeedFeet = Mathf.FloorToInt((baseSpeedFeet * 0.5f) / 5f) * 5;
        Assert(stats.EffectiveSpeedFeet == expectedSpeedFeet,
            "Animate Rope uses standard Entangled half-speed movement",
            $"expected={expectedSpeedFeet}, observed={stats.EffectiveSpeedFeet}");
        Assert(stats.EffectiveSpeedFeet > 0,
            "Animate Rope entangled targets are still able to move");
    }

    private static void TestEntangledConcentrationDcScalesWithSpellLevel()
    {
        SpellData fireball = SpellDatabase.GetSpell("fireball");
        Assert(fireball != null && fireball.SpellLevel == 3, "Fireball level data available for concentration test");
        if (fireball == null)
            return;

        GameObject go = new GameObject("EntangledConcentrationTest");
        try
        {
            CharacterController caster = go.AddComponent<CharacterController>();
            caster.Stats = BuildStats("ConcentrationCaster");

            ConcentrationManager concentration = go.AddComponent<ConcentrationManager>();
            concentration.Init(caster.Stats, caster);
            concentration.BeginConcentration(new ActiveSpellEffect
            {
                Spell = fireball,
                CasterName = caster.Stats.CharacterName,
                CasterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel()),
                RemainingRounds = 1
            });

            ConcentrationCheckResult result = concentration.CheckConcentrationEntangled();
            Assert(result.DC == 18,
                "Entangled concentration check uses DC 15 + spell level",
                $"expected=18, observed={result.DC}");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestAnimateRopeConditionDataUsesStandardEntangledMetadata()
    {
        var data = new AnimateRopeEntangledConditionData
        {
            RopeBreakDC = 24,
            SourceSpellId = "animate_rope",
            SourceSpellName = "Animate Rope"
        };

        Assert(data.RopeBreakDC == 24, "Animate Rope condition data keeps rope break DC metadata");
        Assert(data.SourceSpellId == "animate_rope", "Animate Rope condition data tracks spell source id");
    }
}
}
