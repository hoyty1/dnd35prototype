using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression coverage for Charm Person core rules:
/// spell definition, charmed condition metadata, and threatened/attacked save bonus behavior.
/// Run with CharmPersonRulesTests.RunAll().
/// </summary>
public static class CharmPersonRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CHARM PERSON RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestCharmPersonDefinitionMatchesCoreRules();
        TestCharmedConditionDefinitionExists();
        TestCharmPersonThreatenedSaveBonusApplied();

        Debug.Log($"====== Charm Person Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, string className, int level, int bab, string creatureType = "Humanoid")
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: 12,
            con: 12,
            wis: 14,
            intelligence: 14,
            cha: 12,
            bab: bab,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human");

        stats.CreatureType = creatureType;
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats, CharacterTeam team, Vector2Int gridPos)
    {
        GameObject go = new GameObject($"CharmTest_{stats.CharacterName}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        controller.SetTeam(team);
        controller.GridPosition = gridPos;

        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        inv.Init(stats);

        SpellcastingComponent spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.Init(stats);

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestCharmPersonDefinitionMatchesCoreRules()
    {
        SpellData charm = SpellDatabase.GetSpell("charm_person");
        Assert(charm != null, "Charm Person spell definition exists");
        if (charm == null)
            return;

        Assert(!charm.IsPlaceholder, "Charm Person is implemented (not placeholder)");
        Assert(charm.TargetType == SpellTargetType.SingleEnemy, "Charm Person targets one creature");
        Assert(charm.AllowsSavingThrow && charm.SavingThrowType == "Will", "Charm Person uses Will save negates");
        Assert(charm.DurationType == DurationType.Hours && charm.DurationValue == 1 && charm.DurationScalesWithLevel,
            "Charm Person duration is 1 hour/level",
            $"duration={charm.DurationType}, value={charm.DurationValue}, scales={charm.DurationScalesWithLevel}");
    }

    private static void TestCharmedConditionDefinitionExists()
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Charmed);
        Assert(def != null, "Charmed condition definition exists");
        if (def == null)
            return;

        Assert(def.StackingRule == ConditionStackingRule.Refresh, "Charmed uses refresh stacking");
        Assert(def.MovementMultiplier == 1f, "Charmed does not alter base movement");
    }

    private static void TestCharmPersonThreatenedSaveBonusApplied()
    {
        GameObject gmObject = new GameObject("CharmRulesTest_GameManager");
        GameManager gm = gmObject.AddComponent<GameManager>();

        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 5, 2), CharacterTeam.Player, new Vector2Int(2, 2));
            target = CreateController(BuildStats("Target", "Fighter", 4, 4), CharacterTeam.Enemy, new Vector2Int(3, 2));

            gm.PCs.Add(caster);
            gm.NPCs.Add(target);

            // Simulate already-being-attacked state for the +5 save bonus branch.
            target.Stats.TakeDamage(3);

            SpellData charm = SpellDatabase.GetSpell("charm_person");
            int protectionBonus;
            int saveMod = InvokeGetSaveModifier(target.Stats, charm, caster, target, out protectionBonus);

            Assert(SpellCaster.IsBeingThreatenedBy(target, caster),
                "Threat detection returns true when target is already under hostile pressure from caster side");
            Assert(saveMod >= target.Stats.WillSave + 5,
                "Charm Person applies +5 save bonus when threatened/attacked by caster side",
                $"baseWill={target.Stats.WillSave}, saveMod={saveMod}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gmObject);
        }
    }

    private static int InvokeGetSaveModifier(
        CharacterStats targetStats,
        SpellData spell,
        CharacterController caster,
        CharacterController target,
        out int protectionSaveBonus)
    {
        MethodInfo method = typeof(SpellCaster).GetMethod(
            "GetSaveModifier",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
        {
            protectionSaveBonus = 0;
            return 0;
        }

        object[] args = new object[]
        {
            targetStats,
            spell,
            default(AlignmentProtectionBenefits),
            caster,
            target,
            0
        };

        object result = method.Invoke(null, args);
        protectionSaveBonus = args[5] is int bonus ? bonus : 0;
        return result is int value ? value : 0;
    }
}
}
