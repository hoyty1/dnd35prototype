using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Command Undead spell mechanics (D&D 3.5e PHB p.211).
/// Tests:
///   • Spell definition correctness
///   • Unintelligent undead (no save required)
///   • Intelligent undead (Will save required)
///   • Non-undead target rejection
///   • Effect data factory methods
///   • Control breaking on threatening acts
///   • Multiple undead control via multiple castings
///   • Duration tracking (1 day/level)
///   • SR mechanics
/// </summary>
public static class CommandUndeadRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void command_undead_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== COMMAND UNDEAD RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinition();
        TestEffectDataFactoryNonintelligent();
        TestEffectDataFactoryIntelligent();
        TestNonintelligentUndeadNoSave();
        TestIntelligentUndeadSaveRequired();
        TestNonUndeadTargetRejected();
        TestControlBreakingOnThreateningAct();
        TestMultipleUndeadControl();
        TestDurationTracking();
        TestSuicidalOrderLogic();
        TestCharismaCheckRequirement();
        TestEffectRemoval();

        Debug.Log($"====== Command Undead Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(
        string name,
        string className = "Wizard",
        int level = 5,
        string creatureType = "Humanoid",
        bool isMindless = false,
        int intelligence = 16)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: 12,
            con: 12,
            wis: 10,
            intelligence: intelligence,
            cha: 10,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        stats.CreatureType = creatureType;
        if (isMindless)
        {
            stats.ApplyMindlessTrait(true);
        }
        stats.InitializeSkills(className, level);

        GameObject go = new GameObject($"CmdUndead_{name}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Init(stats, Vector2Int.zero, null, null);

        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        inv.Init(stats);

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static CharacterController CreateUndeadController(string name, bool intelligent, int intelligence = 14)
    {
        if (intelligent)
        {
            return CreateController(name, creatureType: "Undead", isMindless: false, intelligence: intelligence);
        }
        else
        {
            return CreateController(name, creatureType: "Undead", isMindless: true, intelligence: 0);
        }
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null && controller.gameObject != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    // ======================== TESTS ========================

    private static void TestSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.COMMAND_UNDEAD);
        Assert(spell != null, "Spell exists in database");
        if (spell == null) return;

        Assert(spell.Name == "Command Undead", "Spell name is 'Command Undead'");
        Assert(spell.SpellLevel == 2, "Spell level is 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Necromancy", "School is Necromancy", $"got {spell.School}");
        Assert(!spell.IsPlaceholder, "Spell is not a placeholder");
        Assert(spell.EffectType == SpellEffectType.Debuff, "Effect type is Debuff");
        Assert(spell.AllowsSavingThrow, "Allows saving throw (for intelligent undead)");
        Assert(spell.SavingThrowType == "Will", "Save type is Will");
        Assert(spell.SpellResistanceApplies, "SR applies");
        Assert(spell.RangeCategory == SpellRangeCategory.Close, "Range is Close");
        Assert(spell.DurationType == DurationType.Days, "Duration type is Days");
        Assert(spell.DurationValue == 1, "Duration value is 1 (day/level)");
        Assert(spell.DurationScalesWithLevel, "Duration scales with level");
        Assert(spell.TargetType == SpellTargetType.SingleEnemy, "Target type is SingleEnemy");

        // Check class availability
        bool hasWizard = false;
        bool hasSorcerer = false;
        if (spell.ClassList != null)
        {
            foreach (string cls in spell.ClassList)
            {
                if (cls == "Wizard") hasWizard = true;
                if (cls == "Sorcerer") hasSorcerer = true;
            }
        }
        Assert(hasWizard, "Available to Wizard");
        Assert(hasSorcerer, "Available to Sorcerer");
    }

    private static void TestEffectDataFactoryNonintelligent()
    {
        var caster = CreateController("Caster");
        var undead = CreateUndeadController("Skeleton", intelligent: false);

        var effect = CommandUndeadEffectData.CreateForNonintelligent(caster, undead, casterLevel: 5);

        Assert(effect != null, "Factory creates non-null effect for nonintelligent");
        Assert(effect.IsActive, "Effect is active");
        Assert(!effect.IsIntelligent, "IsIntelligent is false for nonintelligent");
        Assert(effect.DurationRemainingRounds == 5 * CommandUndeadEffectData.ROUNDS_PER_DAY,
            "Duration = 5 × ROUNDS_PER_DAY",
            $"got {effect.DurationRemainingRounds}");
        Assert(effect.CasterLevel == 5, "Caster level stored correctly");
        Assert(effect.Caster == caster, "Caster reference stored");
        Assert(effect.ControlledUndead == undead, "Target reference stored");
        Assert(effect.CasterName == caster.Stats.CharacterName, "Caster name stored");
        Assert(effect.ControlledUndeadName == undead.Stats.CharacterName, "Target name stored");

        DestroyController(caster);
        DestroyController(undead);
    }

    private static void TestEffectDataFactoryIntelligent()
    {
        var caster = CreateController("Caster");
        var undead = CreateUndeadController("Wight", intelligent: true);

        var effect = CommandUndeadEffectData.CreateForIntelligent(caster, undead, casterLevel: 7);

        Assert(effect != null, "Factory creates non-null effect for intelligent");
        Assert(effect.IsActive, "Effect is active");
        Assert(effect.IsIntelligent, "IsIntelligent is true for intelligent undead");
        Assert(effect.DurationRemainingRounds == 7 * CommandUndeadEffectData.ROUNDS_PER_DAY,
            "Duration = 7 × ROUNDS_PER_DAY",
            $"got {effect.DurationRemainingRounds}");

        DestroyController(caster);
        DestroyController(undead);
    }

    private static void TestNonintelligentUndeadNoSave()
    {
        var caster = CreateController("Caster");
        var skeleton = CreateUndeadController("Skeleton", intelligent: false);

        Assert(skeleton.CanBeCommandedAsUndead(), "Skeleton can be commanded (is undead)");
        Assert(!skeleton.IsIntelligentUndead(), "Skeleton is not intelligent undead");

        // Apply the effect directly (simulating no save path)
        var effect = CommandUndeadEffectData.CreateForNonintelligent(caster, skeleton, casterLevel: 3);
        skeleton.ApplyCommandUndeadEffect(effect);

        Assert(skeleton.IsCommandedUndead, "Skeleton is commanded after effect applied");
        Assert(skeleton.CommandUndeadController == caster, "Controller is the caster");
        Assert(skeleton.ActiveCommandUndeadEffect == effect, "Active effect matches");

        DestroyController(caster);
        DestroyController(skeleton);
    }

    private static void TestIntelligentUndeadSaveRequired()
    {
        var caster = CreateController("Caster");
        var wight = CreateUndeadController("Wight", intelligent: true, intelligence: 14);

        Assert(wight.CanBeCommandedAsUndead(), "Wight can be commanded (is undead)");
        Assert(wight.IsIntelligentUndead(), "Wight is intelligent undead (Int >= 1)");

        // If save fails, effect is applied
        var effect = CommandUndeadEffectData.CreateForIntelligent(caster, wight, casterLevel: 5);
        wight.ApplyCommandUndeadEffect(effect);

        Assert(wight.IsCommandedUndead, "Wight is commanded after failed save (simulated)");
        Assert(effect.IsIntelligent, "Effect correctly marks as intelligent");
        Assert(effect.RequiresCharismaCheckForOrder(), "Intelligent undead requires CHA check for unusual orders");
        Assert(!effect.WouldObeySuicidalOrder(), "Intelligent undead refuses suicidal orders");

        DestroyController(caster);
        DestroyController(wight);
    }

    private static void TestNonUndeadTargetRejected()
    {
        var humanoid = CreateController("Goblin", creatureType: "Humanoid");
        var construct = CreateController("Golem", creatureType: "Construct");

        Assert(!humanoid.CanBeCommandedAsUndead(), "Humanoid cannot be commanded as undead");
        Assert(!construct.CanBeCommandedAsUndead(), "Construct cannot be commanded as undead");

        DestroyController(humanoid);
        DestroyController(construct);
    }

    private static void TestControlBreakingOnThreateningAct()
    {
        var caster = CreateController("Caster");
        var skeleton = CreateUndeadController("Skeleton", intelligent: false);

        var effect = CommandUndeadEffectData.CreateForNonintelligent(caster, skeleton, casterLevel: 5);
        skeleton.ApplyCommandUndeadEffect(effect);
        Assert(skeleton.IsCommandedUndead, "Skeleton is commanded initially");

        // Break control via threatening act
        skeleton.BreakCommandUndeadControl("Caster attacked skeleton");

        Assert(!skeleton.IsCommandedUndead, "Skeleton is no longer commanded after threatening act");
        Assert(skeleton.ActiveCommandUndeadEffect == null, "Active effect is null after break");
        Assert(!effect.IsActive, "Effect data marked as inactive");

        DestroyController(caster);
        DestroyController(skeleton);
    }

    private static void TestMultipleUndeadControl()
    {
        var caster = CreateController("Caster");
        var skeleton1 = CreateUndeadController("Skeleton1", intelligent: false);
        var skeleton2 = CreateUndeadController("Skeleton2", intelligent: false);

        var effect1 = CommandUndeadEffectData.CreateForNonintelligent(caster, skeleton1, casterLevel: 5);
        skeleton1.ApplyCommandUndeadEffect(effect1);

        var effect2 = CommandUndeadEffectData.CreateForNonintelligent(caster, skeleton2, casterLevel: 5);
        skeleton2.ApplyCommandUndeadEffect(effect2);

        Assert(caster.CommandedUndeadList.Count == 2,
            "Caster controls 2 undead",
            $"got {caster.CommandedUndeadList.Count}");
        Assert(skeleton1.IsCommandedUndead, "Skeleton1 is commanded");
        Assert(skeleton2.IsCommandedUndead, "Skeleton2 is commanded");

        // Remove one
        skeleton1.RemoveCommandUndeadEffect();
        Assert(caster.CommandedUndeadList.Count == 1,
            "Caster controls 1 undead after removal",
            $"got {caster.CommandedUndeadList.Count}");
        Assert(!skeleton1.IsCommandedUndead, "Skeleton1 is no longer commanded");
        Assert(skeleton2.IsCommandedUndead, "Skeleton2 is still commanded");

        DestroyController(caster);
        DestroyController(skeleton1);
        DestroyController(skeleton2);
    }

    private static void TestDurationTracking()
    {
        var caster = CreateController("Caster");
        var skeleton = CreateUndeadController("Skeleton", intelligent: false);

        // CL 1 = 1 day = 14400 rounds
        var effect = CommandUndeadEffectData.CreateForNonintelligent(caster, skeleton, casterLevel: 1);
        skeleton.ApplyCommandUndeadEffect(effect);

        Assert(effect.DurationRemainingRounds == CommandUndeadEffectData.ROUNDS_PER_DAY,
            "Initial duration = 1 day (14400 rounds)",
            $"got {effect.DurationRemainingRounds}");

        // Tick a few rounds
        bool stillActive = effect.TickRound();
        Assert(stillActive, "Still active after 1 tick");
        Assert(effect.DurationRemainingRounds == CommandUndeadEffectData.ROUNDS_PER_DAY - 1,
            "Duration decreased by 1 round");

        // Tick down to 1 round remaining
        effect.DurationRemainingRounds = 1;
        stillActive = effect.TickRound();
        Assert(!stillActive, "Not active after final tick");
        Assert(!effect.IsActive, "Effect is inactive after duration expires");

        // GetRemainingDays for zero duration
        Assert(effect.GetRemainingDays() == 0f, "Remaining days is 0 after expiry");

        DestroyController(caster);
        DestroyController(skeleton);
    }

    private static void TestSuicidalOrderLogic()
    {
        var caster = CreateController("Caster");
        var skeleton = CreateUndeadController("Skeleton", intelligent: false);
        var wight = CreateUndeadController("Wight", intelligent: true);

        var effectSkeleton = CommandUndeadEffectData.CreateForNonintelligent(caster, skeleton, casterLevel: 5);
        var effectWight = CommandUndeadEffectData.CreateForIntelligent(caster, wight, casterLevel: 5);

        Assert(effectSkeleton.WouldObeySuicidalOrder(),
            "Nonintelligent undead obeys suicidal orders");
        Assert(!effectWight.WouldObeySuicidalOrder(),
            "Intelligent undead refuses suicidal orders");

        DestroyController(caster);
        DestroyController(skeleton);
        DestroyController(wight);
    }

    private static void TestCharismaCheckRequirement()
    {
        var caster = CreateController("Caster");
        var skeleton = CreateUndeadController("Skeleton", intelligent: false);
        var wight = CreateUndeadController("Wight", intelligent: true);

        var effectSkeleton = CommandUndeadEffectData.CreateForNonintelligent(caster, skeleton, casterLevel: 5);
        var effectWight = CommandUndeadEffectData.CreateForIntelligent(caster, wight, casterLevel: 5);

        Assert(!effectSkeleton.RequiresCharismaCheckForOrder(),
            "Nonintelligent undead does not require CHA check");
        Assert(effectWight.RequiresCharismaCheckForOrder(),
            "Intelligent undead requires CHA check for unusual orders");

        DestroyController(caster);
        DestroyController(skeleton);
        DestroyController(wight);
    }

    private static void TestEffectRemoval()
    {
        var caster = CreateController("Caster");
        var skeleton = CreateUndeadController("Skeleton", intelligent: false);

        var effect = CommandUndeadEffectData.CreateForNonintelligent(caster, skeleton, casterLevel: 5);
        skeleton.ApplyCommandUndeadEffect(effect);

        Assert(skeleton.IsCommandedUndead, "Commanded before removal");
        Assert(caster.CommandedUndeadList.Count == 1, "Caster has 1 commanded undead");

        skeleton.RemoveCommandUndeadEffect();

        Assert(!skeleton.IsCommandedUndead, "Not commanded after removal");
        Assert(skeleton.ActiveCommandUndeadEffect == null, "Active effect cleared");
        Assert(caster.CommandedUndeadList.Count == 0, "Caster's list cleared after removal");

        DestroyController(caster);
        DestroyController(skeleton);
    }
}
}
