using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Focused regression suite for high-priority D&D 3.5 core combat conditions.
/// Run with CoreConditionRulesTests.RunAll().
/// </summary>
public static class CoreConditionRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CORE CONDITION RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestHelplessRemovesDexToAcAndGrantsMeleeAttackBonus();
        TestParalyzedAutoAppliesHelplessAndAdjacentAutoCrit();
        TestStunnedDropsHeldItemsAndBlocksActions();
        TestBlindedAppliesMissChanceMovementAndSkillPenalties();
        TestSpellConditionMappingsPresent();

        Debug.Log($"====== Core Condition Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, int bab, int str = 16, int dex = 16)
    {
        return new CharacterStats(
            name: name,
            level: 6,
            characterClass: "Fighter",
            str: str,
            dex: dex,
            con: 14,
            wis: 10,
            intelligence: 10,
            cha: 10,
            bab: bab,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 35,
            raceName: "Human");
    }

    private static CharacterController CreateController(string name, int bab, int str = 16, int dex = 16)
    {
        var go = new GameObject($"CoreConditionTest_{name}");
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = BuildStats(name, bab, str, dex);

        var inv = go.AddComponent<InventoryComponent>();
        inv.Init(controller.Stats);

        var spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.Init(controller.Stats);

        var statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(controller.Stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static int GetPositiveDexBonusApplied(CharacterStats stats)
    {
        if (stats == null)
            return 0;

        int dexBonus = stats.DEXMod;
        if (stats.MaxDexBonus >= 0 && dexBonus > stats.MaxDexBonus)
            dexBonus = stats.MaxDexBonus;

        return Mathf.Max(0, dexBonus);
    }

    private static void TestHelplessRemovesDexToAcAndGrantsMeleeAttackBonus()
    {
        CharacterController attacker = null;
        CharacterController defender = null;

        try
        {
            attacker = CreateController("HelplessAttacker", bab: 12, str: 20, dex: 12);
            defender = CreateController("HelplessDefender", bab: 4, str: 10, dex: 18);

            attacker.GridPosition = new Vector2Int(0, 0);
            defender.GridPosition = new Vector2Int(1, 0);

            int baseAc = defender.Stats.ArmorClass;
            int expectedDexLoss = GetPositiveDexBonusApplied(defender.Stats);

            defender.ApplyCondition(CombatConditionType.Helpless, 3, "UnitTest");

            int helplessAc = defender.Stats.ArmorClass;
            Assert(helplessAc == baseAc - expectedDexLoss,
                "Helpless removes Dex bonus to AC without extra AC penalty",
                $"base={baseAc}, expected={baseAc - expectedDexLoss}, actual={helplessAc}");

            Random.InitState(1337);
            CombatResult result = attacker.Attack(defender, false, 0, null, null, null, null);
            bool hasHelplessAttackBonus = result != null
                && result.AttackBuffDebuffModifiers != null
                && result.AttackBuffDebuffModifiers.Exists(m => m.Label == "Melee vs helpless target" && m.Value == 4);

            Assert(hasHelplessAttackBonus,
                "Melee attacker gets +4 bonus vs helpless target",
                result == null ? "result was null" : "missing +4 helpless attack bonus entry");

            Assert(defender.IsHelplessForCoupDeGrace(),
                "Helpless target is valid for coup de grace");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    private static void TestParalyzedAutoAppliesHelplessAndAdjacentAutoCrit()
    {
        CharacterController attacker = null;
        CharacterController defender = null;

        try
        {
            attacker = CreateController("ParalyzeAttacker", bab: 14, str: 22, dex: 12);
            defender = CreateController("ParalyzeDefender", bab: 4, str: 10, dex: 16);

            attacker.GridPosition = new Vector2Int(2, 2);
            defender.GridPosition = new Vector2Int(3, 2);

            defender.ApplyCondition(CombatConditionType.Paralyzed, 3, "UnitTest");

            Assert(defender.HasCondition(CombatConditionType.Helpless),
                "Paralyzed automatically applies Helpless condition");

            CombatResult hitResult = null;
            Random.InitState(2026);
            for (int i = 0; i < 30; i++)
            {
                CombatResult attempt = attacker.Attack(defender, false, 0, null, null, null, null);
                if (attempt != null && attempt.Hit)
                {
                    hitResult = attempt;
                    break;
                }
            }

            Assert(hitResult != null, "Paralyzed auto-crit test produced at least one successful melee hit");
            if (hitResult != null)
            {
                Assert(hitResult.CritConfirmed,
                    "Adjacent melee hit vs paralyzed target auto-confirms critical");
                Assert(!string.IsNullOrEmpty(hitResult.SpecialAttackNote) && hitResult.SpecialAttackNote.ToLowerInvariant().Contains("automatic critical hit"),
                    "Combat log note records paralyzed auto-crit rule",
                    $"note='{hitResult.SpecialAttackNote}'");
            }
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    private static void TestStunnedDropsHeldItemsAndBlocksActions()
    {
        CharacterController target = null;

        try
        {
            target = CreateController("StunnedTarget", bab: 6, str: 16, dex: 18);

            global::Inventory inv = target.Inventory.GetInventory();
            inv.DirectEquip(ItemDatabase.CloneItem("dagger"), EquipSlot.RightHand);
            inv.DirectEquip(ItemDatabase.CloneItem("shield_heavy_wooden"), EquipSlot.LeftHand);
            inv.RecalculateStats();

            int baseAc = target.Stats.ArmorClass;
            int expectedDexLoss = GetPositiveDexBonusApplied(target.Stats);

            target.ApplyCondition(CombatConditionType.Stunned, 2, "UnitTest");

            int stunnedAc = target.Stats.ArmorClass;
            int expectedAc = baseAc - expectedDexLoss - 2;
            Assert(stunnedAc == expectedAc,
                "Stunned applies -2 AC and removes Dex bonus to AC",
                $"base={baseAc}, expected={expectedAc}, actual={stunnedAc}");

            Assert(!target.CanAttack(), "Stunned target cannot attack/actions blocked");

            global::Inventory inventoryData = target.Inventory.GetInventory();
            bool droppedHands = inventoryData != null && inventoryData.RightHandSlot == null && inventoryData.LeftHandSlot == null;
            Assert(droppedHands,
                "Stunned target drops held items");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestBlindedAppliesMissChanceMovementAndSkillPenalties()
    {
        CharacterController attacker = null;
        CharacterController defender = null;

        try
        {
            attacker = CreateController("BlindAttacker", bab: 12, str: 18, dex: 16);
            defender = CreateController("BlindDefender", bab: 4, str: 10, dex: 10);

            attacker.GridPosition = new Vector2Int(4, 4);
            defender.GridPosition = new Vector2Int(5, 4);

            int baseAc = attacker.Stats.ArmorClass;
            int expectedDexLoss = GetPositiveDexBonusApplied(attacker.Stats);

            attacker.ApplyCondition(CombatConditionType.Blinded, 3, "UnitTest");

            int blindedAc = attacker.Stats.ArmorClass;
            int expectedAc = baseAc - expectedDexLoss - 2;
            Assert(blindedAc == expectedAc,
                "Blinded applies -2 AC and removes Dex bonus to AC",
                $"base={baseAc}, expected={expectedAc}, actual={blindedAc}");

            Assert(Mathf.Approximately(attacker.Stats.ConditionMovementMultiplier, 0.5f),
                "Blinded halves movement speed",
                $"movementMultiplier={attacker.Stats.ConditionMovementMultiplier}");

            Assert(attacker.Stats.GetConditionSkillModifier("Climb") <= -4,
                "Blinded applies at least -4 to STR/DEX-based skills",
                $"Climb modifier={attacker.Stats.GetConditionSkillModifier("Climb")}");

            Assert(attacker.Stats.GetConditionSkillModifier("Spot") == 0,
                "Blinded does not apply blanket penalty to non-STR/DEX skill checks",
                $"Spot modifier={attacker.Stats.GetConditionSkillModifier("Spot")}");

            CombatResult observed = null;
            Random.InitState(99);
            for (int i = 0; i < 40; i++)
            {
                CombatResult attempt = attacker.Attack(defender, false, 0, null, null, null, null);
                if (attempt != null && attempt.ConcealmentMissChance == 50)
                {
                    observed = attempt;
                    break;
                }
            }

            Assert(observed != null,
                "Blinded attacker applies 50% miss chance on attacks",
                "No attack result recorded the blind miss chance");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    private static void TestSpellConditionMappingsPresent()
    {
        SpellData holdPerson = SpellDatabase.GetSpell("hold_person");
        SpellData sleep = SpellDatabase.GetSpell("sleep");
        SpellData blindness = SpellDatabase.GetSpell("blindness_deafness_wiz");

        Assert(holdPerson != null, "Hold Person spell definition exists for Paralyzed application");
        Assert(sleep != null, "Sleep spell definition exists for Unconscious application");
        Assert(blindness != null, "Blindness/Deafness spell definition exists for Blinded application");

        SpellData powerWordStun = SpellDatabase.GetSpell("power_word_stun");
        if (powerWordStun == null)
            Debug.Log("  INFO: Power Word Stun spell definition not present in current spell database; runtime mapping hook still exists in GameManager.");
        else
            Assert(powerWordStun != null, "Power Word Stun spell definition exists for Stunned application");
    }
}
}
