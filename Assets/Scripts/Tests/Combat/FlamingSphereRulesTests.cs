using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Runtime regression checks for Flaming Sphere tactical behavior with two stationary enemies.
/// Run with FlamingSphereRulesTests.RunAll().
/// </summary>
public static class FlamingSphereRulesTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int WizardStart = new Vector2Int(5, 5);
    private static readonly Vector2Int Enemy1Start = new Vector2Int(10, 5);
    private static readonly Vector2Int Enemy2Start = new Vector2Int(15, 5);

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void flaming_sphere_rules_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== FLAMING SPHERE RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Test_FlamingSphere_Creation();
        Test_FlamingSphere_UsesTargetSelectionOnCast();
        Test_FlamingSphere_MoveBetweenTwoEnemies();
        Test_FlamingSphere_DamageAndReflex();
        Test_FlamingSphere_TurnEndWarning();
        Test_FlamingSphere_Duration();

        Debug.Log($"====== Flaming Sphere Rules Results: {_passed} passed, {_failed} failed ======");
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

    private sealed class ScenarioContext
    {
        public GameObject GameManagerObject;
        public GameObject GridObject;
        public GameManager GameManager;
        public SquareGrid Grid;
        public CharacterController Wizard;
        public CharacterController Enemy1;
        public CharacterController Enemy2;
        public SpellData FlamingSphereSpell;
    }

    private static ScenarioContext CreateScenario()
    {
        var ctx = new ScenarioContext();

        ctx.GameManagerObject = new GameObject("FlamingSphereRulesTest_GM");
        ctx.GameManager = ctx.GameManagerObject.AddComponent<GameManager>();
        ctx.GameManager.CombatUI = ctx.GameManagerObject.AddComponent<CombatUI>();

        ctx.GridObject = new GameObject("FlamingSphereRulesTest_Grid");
        ctx.Grid = ctx.GridObject.AddComponent<SquareGrid>();
        ctx.Grid.Width = 25;
        ctx.Grid.Height = 25;
        ctx.Grid.GenerateGrid();

        ctx.GameManager.Grid = ctx.Grid;

        ctx.Wizard = CreateCharacter(
            name: "FlamingWizard",
            team: CharacterTeam.Player,
            start: WizardStart,
            characterClass: "Wizard",
            level: 10,
            str: 8,
            dex: 14,
            con: 12,
            wis: 12,
            intelligence: 34,
            cha: 10,
            bab: 5);

        // High Reflex enemy to guarantee successful save.
        ctx.Enemy1 = CreateCharacter(
            name: "StationaryGoblin_A",
            team: CharacterTeam.Enemy,
            start: Enemy1Start,
            characterClass: "Rogue",
            level: 20,
            str: 10,
            dex: 34,
            con: 12,
            wis: 12,
            intelligence: 10,
            cha: 8,
            bab: 15);

        // Low Reflex enemy to guarantee failed save vs very high caster DC.
        ctx.Enemy2 = CreateCharacter(
            name: "StationaryGoblin_B",
            team: CharacterTeam.Enemy,
            start: Enemy2Start,
            characterClass: "Fighter",
            level: 1,
            str: 10,
            dex: 1,
            con: 10,
            wis: 8,
            intelligence: 8,
            cha: 8,
            bab: 1);

        ctx.GameManager.PCs.Clear();
        ctx.GameManager.NPCs.Clear();
        ctx.GameManager.PCs.Add(ctx.Wizard);
        ctx.GameManager.NPCs.Add(ctx.Enemy1);
        ctx.GameManager.NPCs.Add(ctx.Enemy2);

        // Explicit occupancy with no obstacles.
        ctx.Grid.SetCreatureOccupancy(ctx.Wizard, WizardStart, 1);
        ctx.Grid.SetCreatureOccupancy(ctx.Enemy1, Enemy1Start, 1);
        ctx.Grid.SetCreatureOccupancy(ctx.Enemy2, Enemy2Start, 1);

        ctx.FlamingSphereSpell = SpellDatabase.GetSpell(SpellNames.FLAMING_SPHERE);
        return ctx;
    }

    private static CharacterController CreateCharacter(
        string name,
        CharacterTeam team,
        Vector2Int start,
        string characterClass,
        int level,
        int str,
        int dex,
        int con,
        int wis,
        int intelligence,
        int cha,
        int bab)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: characterClass,
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
            baseHitDieHP: 80,
            raceName: "Human");

        stats.InitializeSkills(characterClass, level);

        GameObject go = new GameObject($"FlamingSphereRules_{name}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Init(stats, start, null, null);
        controller.ConfigureTeamControl(team, controllable: team == CharacterTeam.Player);

        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        inv.Init(stats);

        return controller;
    }

    private static void CleanupScenario(ScenarioContext ctx)
    {
        if (ctx == null)
            return;

        DestroyController(ctx.Wizard);
        DestroyController(ctx.Enemy1);
        DestroyController(ctx.Enemy2);

        if (ctx.GridObject != null)
            UnityEngine.Object.DestroyImmediate(ctx.GridObject);

        if (ctx.GameManagerObject != null)
            UnityEngine.Object.DestroyImmediate(ctx.GameManagerObject);
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
    }

    private static bool TryCreateFlamingSphere(ScenarioContext ctx, Vector2Int targetCell, out string castLog)
    {
        castLog = string.Empty;
        MethodInfo createMethod = typeof(GameManager).GetMethod("TryResolveFlamingSphereAoECast", PrivateInstance);
        if (createMethod == null)
            return false;

        var aoeCells = new HashSet<Vector2Int> { targetCell };
        object[] args = { ctx.Wizard, ctx.FlamingSphereSpell, aoeCells, string.Empty };
        bool created = (bool)createMethod.Invoke(ctx.GameManager, args);
        castLog = args[3] as string;
        return created;
    }

    private static FlamingSphereEntity GetSphere(GameManager gm, CharacterController caster)
    {
        MethodInfo getter = typeof(GameManager).GetMethod("GetPrimaryFlamingSphereForCaster", PrivateInstance);
        if (getter == null)
            return null;

        return getter.Invoke(gm, new object[] { caster }) as FlamingSphereEntity;
    }

    private static bool TryMoveSphere(GameManager gm, CharacterController caster, FlamingSphereEntity sphere, Vector2Int destination)
    {
        MethodInfo moveMethod = typeof(GameManager).GetMethod("TryMoveFlamingSphere", PrivateInstance);
        if (moveMethod == null)
            return false;

        object[] args = { caster, sphere, destination, true, false, false };
        return (bool)moveMethod.Invoke(gm, args);
    }

    private static SpellResult ResolveSphereImpact(GameManager gm, CharacterController caster, FlamingSphereEntity sphere, CharacterController target, string context)
    {
        MethodInfo impactMethod = typeof(GameManager).GetMethod("ResolveFlamingSphereImpactDamage", PrivateInstance);
        if (impactMethod == null)
            return null;

        return impactMethod.Invoke(gm, new object[] { caster, sphere, target, sphere.SourceSpell, context }) as SpellResult;
    }

    private static void Test_FlamingSphere_Creation()
    {
        ScenarioContext ctx = null;
        try
        {
            ctx = CreateScenario();
            bool created = TryCreateFlamingSphere(ctx, Enemy1Start, out string castLog);
            FlamingSphereEntity sphere = GetSphere(ctx.GameManager, ctx.Wizard);

            int expectedDuration = ActiveSpellEffect.CalculateDurationRounds(ctx.FlamingSphereSpell, ctx.Wizard.Stats.GetCasterLevel());

            Assert(created, "Flaming Sphere is created successfully");
            Assert(sphere != null, "Sphere entity exists after cast");
            Assert(sphere != null && sphere.GridPosition == Enemy1Start,
                "Sphere spawns on selected target cell",
                $"expected={Enemy1Start}, actual={(sphere != null ? sphere.GridPosition.ToString() : "<null>")}");
            Assert(sphere != null && sphere.RemainingRounds == expectedDuration,
                "Sphere duration initialized from caster level",
                $"expected={expectedDuration}, actual={(sphere != null ? sphere.RemainingRounds : -1)}");
            Assert(ctx.GameManager.HasActiveFlamingSphere(ctx.Wizard), "Wizard has active Flaming Sphere after casting");
            Assert(ctx.GameManager.CanControlFlamingSphere(ctx.Wizard), "Control Flaming Sphere action is available immediately after cast");
            Assert(!string.IsNullOrEmpty(castLog) && castLog.Contains("2d6") && castLog.Contains("Control Flaming Sphere"),
                "Cast log documents 2d6 damage and control hint");
        }
        finally
        {
            CleanupScenario(ctx);
        }
    }

    private static void Test_FlamingSphere_UsesTargetSelectionOnCast()
    {
        ScenarioContext ctx = null;
        try
        {
            ctx = CreateScenario();

            MethodInfo enterAoEMethod = typeof(GameManager).GetMethod("EnterAoETargetingMode", PrivateInstance);
            Assert(enterAoEMethod != null, "Flaming Sphere AoE targeting entry point exists");
            if (enterAoEMethod == null)
                return;

            enterAoEMethod.Invoke(ctx.GameManager, new object[] { ctx.Wizard, ctx.FlamingSphereSpell });

            FieldInfo isAoETargetingField = typeof(GameManager).GetField("_isAoETargeting", PrivateInstance);
            FieldInfo isConfirmingSelfAoEField = typeof(GameManager).GetField("_isConfirmingSelfAoE", PrivateInstance);

            bool isAoETargeting = isAoETargetingField != null && (bool)isAoETargetingField.GetValue(ctx.GameManager);
            bool isConfirmingSelfAoE = isConfirmingSelfAoEField != null && (bool)isConfirmingSelfAoEField.GetValue(ctx.GameManager);

            Assert(ctx.GameManager.CurrentSubPhase == GameManager.PlayerSubPhase.SelectingAoETarget,
                "Flaming Sphere cast enters AoE cell selection phase");
            Assert(isAoETargeting,
                "Flaming Sphere cast enables AoE targeting mode");
            Assert(!isConfirmingSelfAoE,
                "Flaming Sphere cast is not treated as self-centered burst");
        }
        finally
        {
            CleanupScenario(ctx);
        }
    }

    private static void Test_FlamingSphere_MoveBetweenTwoEnemies()
    {
        ScenarioContext ctx = null;
        try
        {
            ctx = CreateScenario();
            bool created = TryCreateFlamingSphere(ctx, Enemy1Start, out _);
            FlamingSphereEntity sphere = GetSphere(ctx.GameManager, ctx.Wizard);

            int enemy2HpBefore = ctx.Enemy2.Stats.CurrentHP;
            bool moved = created && sphere != null && TryMoveSphere(ctx.GameManager, ctx.Wizard, sphere, Enemy2Start);
            int enemy2HpAfter = ctx.Enemy2.Stats.CurrentHP;

            Assert(created && sphere != null, "Movement test setup created Flaming Sphere");
            Vector2Int expectedStopCell = new Vector2Int(Enemy2Start.x - 1, Enemy2Start.y);

            Assert(moved, "Sphere can be moved between two stationary enemies");
            Assert(sphere != null && sphere.GridPosition == expectedStopCell,
                "Sphere stops one square before second enemy on impact",
                $"expected={expectedStopCell}, actual={(sphere != null ? sphere.GridPosition.ToString() : "<null>")}");
            Assert(sphere != null && sphere.MovedThisTurn, "Sphere marks moved state after reposition");
            Assert(enemy2HpAfter <= enemy2HpBefore,
                "Second enemy is impacted or successfully negates damage on contact",
                $"hpBefore={enemy2HpBefore}, hpAfter={enemy2HpAfter}");
        }
        finally
        {
            CleanupScenario(ctx);
        }
    }

    private static void Test_FlamingSphere_DamageAndReflex()
    {
        ScenarioContext ctx = null;
        try
        {
            ctx = CreateScenario();
            bool created = TryCreateFlamingSphere(ctx, WizardStart, out _);
            FlamingSphereEntity sphere = GetSphere(ctx.GameManager, ctx.Wizard);

            SpellResult successSaveResult = ResolveSphereImpact(ctx.GameManager, ctx.Wizard, sphere, ctx.Enemy1, "high reflex test");
            SpellResult failedSaveResult = ResolveSphereImpact(ctx.GameManager, ctx.Wizard, sphere, ctx.Enemy2, "low reflex test");

            Assert(created && sphere != null, "Damage/reflex test setup created Flaming Sphere");

            Assert(successSaveResult != null && successSaveResult.RequiredSave && successSaveResult.SaveSucceeded,
                "High-Reflex enemy succeeds Reflex save against Flaming Sphere",
                successSaveResult == null ? "result was null" : $"saveTotal={successSaveResult.SaveTotal}, dc={successSaveResult.SaveDC}");
            Assert(successSaveResult != null && successSaveResult.DamageDealt == 0,
                "Successful Reflex save negates Flaming Sphere damage",
                $"damage={ (successSaveResult != null ? successSaveResult.DamageDealt : -1) }");

            Assert(failedSaveResult != null && failedSaveResult.RequiredSave && !failedSaveResult.SaveSucceeded,
                "Low-Reflex enemy fails Reflex save against Flaming Sphere",
                failedSaveResult == null ? "result was null" : $"saveTotal={failedSaveResult.SaveTotal}, dc={failedSaveResult.SaveDC}");
            Assert(failedSaveResult != null && failedSaveResult.DamageRolled >= 2 && failedSaveResult.DamageRolled <= 12,
                "Flaming Sphere rolls damage in 2d6 range",
                failedSaveResult == null ? "result was null" : $"rolled={failedSaveResult.DamageRolled}");
            Assert(failedSaveResult != null && failedSaveResult.DamageDealt == failedSaveResult.DamageRolled && failedSaveResult.DamageDealt > 0,
                "Failed Reflex save applies full 2d6 damage",
                failedSaveResult == null ? "result was null" : $"rolled={failedSaveResult.DamageRolled}, dealt={failedSaveResult.DamageDealt}");
        }
        finally
        {
            CleanupScenario(ctx);
        }
    }

    private static void Test_FlamingSphere_TurnEndWarning()
    {
        ScenarioContext ctx = null;
        try
        {
            ctx = CreateScenario();
            bool created = TryCreateFlamingSphere(ctx, Enemy1Start, out _);
            FlamingSphereEntity sphere = GetSphere(ctx.GameManager, ctx.Wizard);

            ctx.GameManager.WarnFlamingSphereNotMovedAtTurnEnd(ctx.Wizard);
            string exportedLog = ctx.GameManager.CombatUI != null ? ctx.GameManager.CombatUI.ExportCombatLog() : string.Empty;

            Assert(created && sphere != null, "Turn-end warning test setup created Flaming Sphere");
            Assert(sphere != null && sphere.WarnedNotMovedThisTurn,
                "Sphere state tracks end-of-turn warning when not moved");
            Assert(!string.IsNullOrEmpty(exportedLog) && exportedLog.Contains("ends turn without moving Flaming Sphere"),
                "Combat log includes turn-end warning when sphere is not moved");
        }
        finally
        {
            CleanupScenario(ctx);
        }
    }

    private static void Test_FlamingSphere_Duration()
    {
        ScenarioContext ctx = null;
        try
        {
            ctx = CreateScenario();
            bool created = TryCreateFlamingSphere(ctx, Enemy1Start, out _);
            FlamingSphereEntity sphere = GetSphere(ctx.GameManager, ctx.Wizard);

            int initialDuration = sphere != null ? sphere.RemainingRounds : 0;

            for (int i = 0; i < Mathf.Max(0, initialDuration - 1); i++)
                ctx.GameManager.HandleFlamingSphereTurnStart(ctx.Wizard);

            bool activeBeforeFinalTick = ctx.GameManager.HasActiveFlamingSphere(ctx.Wizard);
            ctx.GameManager.HandleFlamingSphereTurnStart(ctx.Wizard);
            bool activeAfterFinalTick = ctx.GameManager.HasActiveFlamingSphere(ctx.Wizard);

            Assert(created && sphere != null, "Duration test setup created Flaming Sphere");
            Assert(initialDuration > 0, "Sphere starts with positive duration", $"duration={initialDuration}");
            Assert(activeBeforeFinalTick, "Sphere remains active until final duration tick");
            Assert(!activeAfterFinalTick, "Sphere dissipates when duration reaches zero");
        }
        finally
        {
            CleanupScenario(ctx);
        }
    }
}
}
