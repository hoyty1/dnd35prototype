using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Jump and Magic Weapon spell behavior.
/// </summary>
public static class JumpAndMagicWeaponRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void jump_magic_weapon_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== JUMP + MAGIC WEAPON RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestJumpDefinition();
        TestJumpCasterLevelScaling();
        TestJumpDismissRemovesBonus();

        TestMagicWeaponDefinition();
        TestMagicWeaponEnhancementAndBypass();
        TestMagicWeaponHighestEnhancementOnly();
        TestMagicWeaponAttackDamageIntegration();
        TestMagicWeaponDurationPerWeapon();
        TestMagicWeaponInventorySelectionIncludesBackpack();

        Debug.Log($"====== Jump + Magic Weapon Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(string name, int level = 3)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: "Wizard",
            str: 12,
            dex: 12,
            con: 12,
            wis: 10,
            intelligence: 16,
            cha: 10,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 18,
            raceName: "Human");

        stats.InitializeSkills("Wizard", level);

        GameObject go = new GameObject($"JumpMw_{name}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Init(stats, Vector2Int.zero, null, null);

        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        inv.Init(stats);

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestJumpDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell("jump");
        Assert(spell != null, "Jump spell definition exists");
        if (spell == null)
            return;

        Assert(spell.RangeCategory == SpellRangeCategory.Touch, "Jump range is Touch");
        Assert(spell.TargetType == SpellTargetType.SingleAlly, "Jump targets ally/self");
        Assert(string.Equals(spell.BuffSkillName, "Jump", StringComparison.OrdinalIgnoreCase), "Jump buffs Jump skill");
        Assert(spell.GetEffectiveBonusType() == BonusType.Enhancement, "Jump bonus type is enhancement");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
            "Jump duration is 1 minute/level");
        Assert(spell.IsDismissible, "Jump is dismissible");
    }

    private static void TestJumpCasterLevelScaling()
    {
        CharacterController controller = null;
        try
        {
            controller = CreateController("JumpScale", level: 7);
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();
            SpellData spell = SpellDatabase.GetSpell("jump");

            int baselineJumpBonus = controller.Stats.GetSkillBonus("Jump");

            ActiveSpellEffect cl1 = statusMgr.AddEffect(spell, "Tester", 1);
            Assert(cl1 != null && cl1.AppliedSkillBonus == 10, "Jump CL1 grants +10", $"actual={(cl1 != null ? cl1.AppliedSkillBonus : -1)}");
            statusMgr.RemoveEffectsBySpellId("jump");

            ActiveSpellEffect cl3 = statusMgr.AddEffect(spell, "Tester", 3);
            Assert(cl3 != null && cl3.AppliedSkillBonus == 20, "Jump CL3 grants +20", $"actual={(cl3 != null ? cl3.AppliedSkillBonus : -1)}");
            statusMgr.RemoveEffectsBySpellId("jump");

            ActiveSpellEffect cl7 = statusMgr.AddEffect(spell, "Tester", 7);
            Assert(cl7 != null && cl7.AppliedSkillBonus == 30, "Jump CL7 grants +30", $"actual={(cl7 != null ? cl7.AppliedSkillBonus : -1)}");
            Assert(controller.Stats.GetSkillBonus("Jump") == baselineJumpBonus + 30,
                "Jump skill check includes applied +30 enhancement",
                $"baseline={baselineJumpBonus}, actual={controller.Stats.GetSkillBonus("Jump")}");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    private static void TestJumpDismissRemovesBonus()
    {
        CharacterController controller = null;
        try
        {
            controller = CreateController("JumpDismiss", level: 3);
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();
            SpellData spell = SpellDatabase.GetSpell("jump");

            int baselineJumpBonus = controller.Stats.GetSkillBonus("Jump");
            statusMgr.AddEffect(spell, "Tester", 3);

            Assert(controller.Stats.JumpEnhancementBonus == 20, "Jump enhancement applied at CL3 (+20)");

            statusMgr.RemoveEffectsBySpellId("jump");
            Assert(controller.Stats.JumpEnhancementBonus == 0, "Dismiss/removal clears Jump enhancement bonus");
            Assert(controller.Stats.GetSkillBonus("Jump") == baselineJumpBonus, "Jump skill returns to baseline after dismissal");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    private static void TestMagicWeaponDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell("magic_weapon");
        Assert(spell != null, "Magic Weapon spell definition exists");
        if (spell == null)
            return;

        Assert(spell.RangeCategory == SpellRangeCategory.Touch, "Magic Weapon range is Touch");
        Assert(spell.TargetType == SpellTargetType.SingleAlly, "Magic Weapon targets ally/self");
        Assert(spell.GetEffectiveBonusType() == BonusType.Enhancement, "Magic Weapon bonus type is enhancement");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
            "Magic Weapon duration is 1 minute/level");
    }

    private static void TestMagicWeaponEnhancementAndBypass()
    {
        ItemData weapon = ItemDatabase.GetItem("longsword");
        weapon.AddOrReplaceItemSpellEffect(new ItemSpellEffect("magic_weapon", "Magic Weapon", "Tester", 3, 30)
        {
            BonusType = BonusType.Enhancement,
            EnhancementBonusAttack = 1,
            EnhancementBonusDamage = 1,
            CountsAsMagicForBypass = true
        });

        Assert(weapon.GetEnhancementAttackBonus() == 1, "Magic Weapon gives +1 enhancement attack bonus");
        Assert(weapon.GetEnhancementDamageBonus() == 1, "Magic Weapon gives +1 enhancement damage bonus");
        Assert(weapon.IsMagicForBypass, "Magic Weapon makes weapon count as magic for bypass");
    }

    private static void TestMagicWeaponHighestEnhancementOnly()
    {
        ItemData plusTwoWeapon = ItemDatabase.GetItem("longsword");
        plusTwoWeapon.EnhancementBonus = 2;
        plusTwoWeapon.AddOrReplaceItemSpellEffect(new ItemSpellEffect("magic_weapon", "Magic Weapon", "Tester", 3, 30)
        {
            BonusType = BonusType.Enhancement,
            EnhancementBonusAttack = 1,
            EnhancementBonusDamage = 1,
            CountsAsMagicForBypass = true
        });

        Assert(plusTwoWeapon.GetEnhancementAttackBonus() == 2,
            "Enhancement stacking uses highest attack bonus (existing +2 over Magic Weapon +1)");
        Assert(plusTwoWeapon.GetEnhancementDamageBonus() == 2,
            "Enhancement stacking uses highest damage bonus (existing +2 over Magic Weapon +1)");
    }

    private static void TestMagicWeaponAttackDamageIntegration()
    {
        CharacterController attacker = null;
        CharacterController defender = null;
        try
        {
            attacker = CreateController("MwAttacker", level: 3);
            defender = CreateController("MwDefender", level: 3);

            Inventory attackerInventory = attacker.GetComponent<InventoryComponent>().CharacterInventory;
            ItemData weapon = ItemDatabase.GetItem("longsword");
            weapon.AddOrReplaceItemSpellEffect(new ItemSpellEffect("magic_weapon", "Magic Weapon", "Tester", 3, 30)
            {
                EnhancementBonusAttack = 1,
                EnhancementBonusDamage = 1,
                CountsAsMagicForBypass = true
            });
            attackerInventory.DirectEquip(weapon, EquipSlot.RightHand);
            attackerInventory.RecalculateStats();

            CombatResult result = attacker.Attack(defender);
            Assert(result != null && result.WeaponEnhancementAttackBonus == 1,
                "Attack flow reads +1 enhancement attack bonus from weapon effect",
                $"actual={(result != null ? result.WeaponEnhancementAttackBonus : -1)}");
            Assert(result != null && result.WeaponEnhancementDamageBonus == 1,
                "Attack flow reads +1 enhancement damage bonus from weapon effect",
                $"actual={(result != null ? result.WeaponEnhancementDamageBonus : -1)}");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    private static void TestMagicWeaponDurationPerWeapon()
    {
        ItemData weaponA = ItemDatabase.GetItem("longsword");
        ItemData weaponB = ItemDatabase.GetItem("dagger");

        weaponA.AddOrReplaceItemSpellEffect(new ItemSpellEffect("magic_weapon", "Magic Weapon", "Tester", 3, 2)
        {
            EnhancementBonusAttack = 1,
            EnhancementBonusDamage = 1,
            CountsAsMagicForBypass = true
        });

        weaponB.AddOrReplaceItemSpellEffect(new ItemSpellEffect("magic_weapon", "Magic Weapon", "Tester", 3, 4)
        {
            EnhancementBonusAttack = 1,
            EnhancementBonusDamage = 1,
            CountsAsMagicForBypass = true
        });

        weaponA.TickItemSpellEffects();
        weaponB.TickItemSpellEffects();
        Assert(weaponA.ActiveSpellEffects.Count == 1 && weaponA.ActiveSpellEffects[0].RemainingRounds == 1,
            "Weapon A duration ticks independently");
        Assert(weaponB.ActiveSpellEffects.Count == 1 && weaponB.ActiveSpellEffects[0].RemainingRounds == 3,
            "Weapon B duration ticks independently");

        weaponA.TickItemSpellEffects();
        Assert(weaponA.ActiveSpellEffects.Count == 0, "Weapon A effect expires without affecting weapon B");
        Assert(weaponB.ActiveSpellEffects.Count == 1, "Weapon B effect remains active after weapon A expires");
    }

    private static void TestMagicWeaponInventorySelectionIncludesBackpack()
    {
        CharacterController controller = null;
        try
        {
            controller = CreateController("InventorySelect", level: 3);
            InventoryComponent invComp = controller.GetComponent<InventoryComponent>();
            Inventory inv = invComp.CharacterInventory;

            ItemData equipped = ItemDatabase.GetItem("longsword");
            ItemData backpackWeapon = ItemDatabase.GetItem("dagger");

            inv.DirectEquip(equipped, EquipSlot.RightHand);
            bool added = inv.AddItem(backpackWeapon);
            Assert(added, "Backpack weapon added for Magic Weapon selection test");

            MethodInfo method = typeof(GameManager).GetMethod("TryGetMagicWeaponInventoryOptions", BindingFlags.NonPublic | BindingFlags.Static);
            object[] args = { controller, null, null };
            bool success = method != null && (bool)method.Invoke(null, args);

            var options = args[1] as List<ItemData>;
            Assert(success && options != null, "Magic Weapon inventory option query succeeds");
            Assert(options != null && options.Exists(w => ReferenceEquals(w, equipped)), "Magic Weapon selector includes equipped weapon");
            Assert(options != null && options.Exists(w => ReferenceEquals(w, backpackWeapon)), "Magic Weapon selector includes backpack weapon");
        }
        finally
        {
            DestroyController(controller);
        }
    }
}
}
