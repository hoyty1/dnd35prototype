using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression coverage for Shocking Grasp +3 attack bonus against metal armor/bodies.
/// Run with ShockingGraspMetalBonusTests.RunAll().
/// </summary>
public static class ShockingGraspMetalBonusTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SHOCKING GRASP METAL BONUS TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestBonusAppliesAgainstMetalArmor();
        TestBonusDoesNotApplyAgainstLeatherArmor();
        TestBonusAppliesAgainstMetalBody();
        TestBonusDoesNotApplyAgainstFleshBody();

        Debug.Log($"====== Shocking Grasp Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateWizardController(string name)
    {
        var go = new GameObject($"ShockingGraspTest_{name}");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();

        CharacterStats stats = new CharacterStats(
            name: name,
            level: 3,
            characterClass: "Wizard",
            str: 12, dex: 14, con: 12, wis: 12, intelligence: 16, cha: 10,
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

        controller.Init(stats, Vector2Int.zero, null, null);
        inventory.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static SpellResult CastShockingGrasp(CharacterController caster, CharacterController target)
    {
        SpellData spell = SpellDatabase.GetSpell("shocking_grasp");
        return SpellCaster.Cast(
            spell,
            caster.Stats,
            target.Stats,
            metamagic: null,
            forceFriendlyTouchNoRoll: false,
            forceTargetToFailSave: false,
            casterController: caster,
            targetController: target);
    }

    private static void TestBonusAppliesAgainstMetalArmor()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateWizardController("MetalArmorCaster");
            target = CreateWizardController("MetalArmorTarget");
            target.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chainmail"), EquipSlot.Armor);

            SpellResult result = CastShockingGrasp(caster, target);

            Assert(result.SituationalAttackBonus == 3,
                "Shocking Grasp grants +3 against metal armor",
                $"expected +3, got {result.SituationalAttackBonus}");
            Assert(!string.IsNullOrWhiteSpace(result.SituationalAttackBonusSource) && result.SituationalAttackBonusSource.Contains("metal armor"),
                "Shocking Grasp logs metal armor bonus source",
                $"source='{result.SituationalAttackBonusSource}'");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestBonusDoesNotApplyAgainstLeatherArmor()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateWizardController("LeatherArmorCaster");
            target = CreateWizardController("LeatherArmorTarget");
            target.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem("leather_armor"), EquipSlot.Armor);

            SpellResult result = CastShockingGrasp(caster, target);

            Assert(result.SituationalAttackBonus == 0,
                "Shocking Grasp does not grant +3 against leather armor",
                $"expected +0, got {result.SituationalAttackBonus}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestBonusAppliesAgainstMetalBody()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateWizardController("MetalBodyCaster");
            target = CreateWizardController("IronGolemTarget");
            target.Stats.CreatureType = "Construct";
            target.Stats.MaterialComposition = MaterialComposition.Metal;

            SpellResult result = CastShockingGrasp(caster, target);

            Assert(result.SituationalAttackBonus == 3,
                "Shocking Grasp grants +3 against metal-bodied target",
                $"expected +3, got {result.SituationalAttackBonus}");
            Assert(!string.IsNullOrWhiteSpace(result.SituationalAttackBonusSource) && result.SituationalAttackBonusSource.Contains("metal body"),
                "Shocking Grasp logs metal body bonus source",
                $"source='{result.SituationalAttackBonusSource}'");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestBonusDoesNotApplyAgainstFleshBody()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateWizardController("FleshBodyCaster");
            target = CreateWizardController("FleshGolemTarget");
            target.Stats.CreatureType = "Construct";
            target.Stats.MaterialComposition = MaterialComposition.Organic;

            SpellResult result = CastShockingGrasp(caster, target);

            Assert(result.SituationalAttackBonus == 0,
                "Shocking Grasp does not grant +3 against non-metal body",
                $"expected +0, got {result.SituationalAttackBonus}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }
}
}
