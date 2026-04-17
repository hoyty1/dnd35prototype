using System.Reflection;
using UnityEngine;

/// <summary>
/// Tests for attack damage mode defaults and D&D 3.5 mismatch penalties.
/// Run via UnarmedDamageModeTests.RunAll() from a runtime test hook.
/// </summary>
public static class UnarmedDamageModeTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();

        Debug.Log("========== UNARMED DAMAGE MODE TESTS ==========");

        TestNormalUnarmedDefaultsToNonlethal();
        TestGauntletMakesUnarmedDefaultLethal();
        TestImprovedUnarmedStrikeTakesPriorityAndDefaultsLethal();
        TestToggleStillOverridesDefault();
        TestLethalWeaponToNonlethalAppliesPenaltyAndLogsBreakdown();
        TestLethalWeaponToLethalHasNoPenalty();
        TestMonkUnarmedLethalWithoutIusAppliesPenalty();
        TestMonkUnarmedLethalWithIusHasNoPenalty();
        TestGetUnarmedDamageDefaultsTo1d3();
        TestMonkUnarmedDamageUsesMonkDie();
        TestAttackResultUsesUnarmedStrikeLabel();

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}");
        }
    }

    private static CharacterController CreateTestCharacter(string name, string className = "Fighter")
    {
        var go = new GameObject($"{name}_GO");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        var stats = new CharacterStats(name, 2, className, 14, 12, 12, 10, 10, 10, 2, 0, 0, 8, 1, 0, 4, 1, 18);

        controller.Init(stats, Vector2Int.zero, null, null);
        inventory.Init(stats);

        return controller;
    }

    private static void Cleanup(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static object ResolveUnarmedDamageProfile(CharacterController controller)
    {
        MethodInfo method = typeof(CharacterController).GetMethod("ResolveDamageModeAttackProfile", BindingFlags.Instance | BindingFlags.NonPublic);
        return method.Invoke(controller, new object[] { null });
    }

    private static object ResolveDamageProfile(CharacterController controller, ItemData weapon)
    {
        MethodInfo method = typeof(CharacterController).GetMethod("ResolveDamageModeAttackProfile", BindingFlags.Instance | BindingFlags.NonPublic);
        return method.Invoke(controller, new object[] { weapon });
    }

    private static bool GetProfileBool(object profile, string fieldName)
    {
        FieldInfo field = profile.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        return field != null && (bool)field.GetValue(profile);
    }

    private static int GetProfileInt(object profile, string fieldName)
    {
        FieldInfo field = profile.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        return field != null ? (int)field.GetValue(profile) : 0;
    }

    private static string GetProfileString(object profile, string fieldName)
    {
        FieldInfo field = profile.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        return field != null ? (string)field.GetValue(profile) : string.Empty;
    }

    private static void TestNormalUnarmedDefaultsToNonlethal()
    {
        var c = CreateTestCharacter("UnarmedDefault");
        c.ResetAttackDamageMode();

        object profile = ResolveUnarmedDamageProfile(c);
        Assert(GetProfileBool(profile, "DealNonlethalDamage"), "Unarmed without feat/gauntlet defaults to nonlethal");
        Assert(GetProfileInt(profile, "AttackPenalty") == 0, "Default nonlethal unarmed has no penalty");

        c.SetAttackDamageMode(AttackDamageMode.Lethal);
        profile = ResolveUnarmedDamageProfile(c);
        Assert(!GetProfileBool(profile, "DealNonlethalDamage"), "Manual lethal toggle forces lethal unarmed");
        Assert(GetProfileInt(profile, "AttackPenalty") == -4, "Lethal unarmed without feat/gauntlet applies -4 penalty");

        Cleanup(c);
    }

    private static void TestGauntletMakesUnarmedDefaultLethal()
    {
        var c = CreateTestCharacter("GauntletDefault");
        var inv = c.GetComponent<InventoryComponent>();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("gauntlet"), EquipSlot.Hands);

        c.ResetAttackDamageMode();
        object profile = ResolveUnarmedDamageProfile(c);

        Assert(c.HasGauntletEquipped(), "Gauntlet is detected in hands slot");
        Assert(!GetProfileBool(profile, "DealNonlethalDamage"), "Gauntlet makes unarmed default lethal");
        Assert(GetProfileInt(profile, "AttackPenalty") == 0, "Gauntlet lethal default has no penalty");

        Cleanup(c);
    }

    private static void TestImprovedUnarmedStrikeTakesPriorityAndDefaultsLethal()
    {
        var c = CreateTestCharacter("IUSPriority");
        c.Stats.Feats.Add("Improved Unarmed Strike");

        c.ResetAttackDamageMode();
        object profile = ResolveUnarmedDamageProfile(c);

        Assert(FeatManager.HasImprovedUnarmedStrike(c.Stats), "Improved Unarmed Strike helper detects feat");
        Assert(!GetProfileBool(profile, "DealNonlethalDamage"), "Improved Unarmed Strike makes unarmed default lethal");
        Assert(GetProfileInt(profile, "AttackPenalty") == 0, "Improved Unarmed Strike lethal default has no penalty");

        Cleanup(c);
    }

    private static void TestToggleStillOverridesDefault()
    {
        var c = CreateTestCharacter("ToggleOverride");
        c.Stats.Feats.Add("Improved Unarmed Strike");
        c.ResetAttackDamageMode();

        c.ToggleAttackDamageMode();
        object profile = ResolveUnarmedDamageProfile(c);

        Assert(GetProfileBool(profile, "DealNonlethalDamage"), "Toggle can switch lethal-default unarmed to nonlethal");

        Cleanup(c);
    }

    private static void TestLethalWeaponToNonlethalAppliesPenaltyAndLogsBreakdown()
    {
        var attacker = CreateTestCharacter("LongswordNonlethalAttacker");
        var defender = CreateTestCharacter("LongswordNonlethalDefender");

        attacker.GridPosition = new Vector2Int(0, 0);
        defender.GridPosition = new Vector2Int(1, 0);

        var inv = attacker.GetComponent<InventoryComponent>();
        ItemData longsword = ItemDatabase.CloneItem("longsword");
        inv.CharacterInventory.DirectEquip(longsword, EquipSlot.RightHand);

        attacker.SetAttackDamageMode(AttackDamageMode.Nonlethal);

        object profile = ResolveDamageProfile(attacker, longsword);
        Assert(GetProfileBool(profile, "DealNonlethalDamage"), "Longsword toggled to nonlethal resolves as nonlethal");
        Assert(GetProfileInt(profile, "AttackPenalty") == -4, "Longsword toggled to nonlethal applies -4 attack penalty");
        Assert(GetProfileString(profile, "PenaltySource").Contains("Longsword"), "Penalty source includes weapon name for longsword nonlethal mismatch");

        CombatResult result = attacker.Attack(defender, isFlanking: false, flankingBonus: 0, flankingPartnerName: null);
        Assert(result != null, "Longsword nonlethal attack returns combat result");
        Assert(result != null && result.AttackDamageMode == AttackDamageMode.Nonlethal, "Combat result tracks nonlethal damage mode");
        Assert(result != null && result.DamageModeAttackPenalty == -4, "Combat result attack penalty includes -4 damage mode mismatch");

        string detailedLog = result != null ? result.GetDetailedSummary() : string.Empty;
        Assert(detailedLog.Contains("Using nonlethal damage with Longsword"), "Detailed combat log shows nonlethal-with-longsword penalty source");

        string breakdown = result != null ? result.GetAttackBreakdown("Attack 1") : string.Empty;
        Assert(breakdown.Contains("Using nonlethal damage with Longsword"), "Attack breakdown shows nonlethal-with-longsword penalty source");

        Cleanup(attacker);
        Cleanup(defender);
    }

    private static void TestLethalWeaponToLethalHasNoPenalty()
    {
        var c = CreateTestCharacter("LongswordLethal");
        var inv = c.GetComponent<InventoryComponent>();
        ItemData longsword = ItemDatabase.CloneItem("longsword");
        inv.CharacterInventory.DirectEquip(longsword, EquipSlot.RightHand);

        c.SetAttackDamageMode(AttackDamageMode.Lethal);
        object profile = ResolveDamageProfile(c, longsword);

        Assert(!GetProfileBool(profile, "DealNonlethalDamage"), "Longsword in lethal mode resolves as lethal");
        Assert(GetProfileInt(profile, "AttackPenalty") == 0, "Longsword in lethal mode has no attack penalty");

        Cleanup(c);
    }

    private static void TestMonkUnarmedLethalWithoutIusAppliesPenalty()
    {
        var c = CreateTestCharacter("MonkNoIus", "Monk");
        c.Stats.Feats.Remove("Improved Unarmed Strike");

        c.SetAttackDamageMode(AttackDamageMode.Lethal);
        object profile = ResolveUnarmedDamageProfile(c);

        Assert(!GetProfileBool(profile, "DealNonlethalDamage"), "Monk toggled to lethal resolves as lethal");
        Assert(GetProfileInt(profile, "AttackPenalty") == -4, "Monk unarmed lethal without IUS applies -4 attack penalty");

        Cleanup(c);
    }

    private static void TestMonkUnarmedLethalWithIusHasNoPenalty()
    {
        var c = CreateTestCharacter("MonkWithIus", "Monk");
        if (!c.Stats.HasFeat("Improved Unarmed Strike"))
            c.Stats.Feats.Add("Improved Unarmed Strike");

        c.SetAttackDamageMode(AttackDamageMode.Lethal);
        object profile = ResolveUnarmedDamageProfile(c);

        Assert(!GetProfileBool(profile, "DealNonlethalDamage"), "Monk with IUS toggled to lethal resolves as lethal");
        Assert(GetProfileInt(profile, "AttackPenalty") == 0, "Monk unarmed lethal with IUS has no attack penalty");

        Cleanup(c);
    }

    private static void TestGetUnarmedDamageDefaultsTo1d3()
    {
        var c = CreateTestCharacter("UnarmedDamageDefault");
        var unarmed = c.GetUnarmedDamage();

        Assert(unarmed.damageCount == 1, "Default unarmed strike uses one damage die");
        Assert(unarmed.damageDice == 3, "Default unarmed strike die is 1d3");

        Cleanup(c);
    }

    private static void TestMonkUnarmedDamageUsesMonkDie()
    {
        var go = new GameObject("MonkUnarmed_GO");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        var stats = new CharacterStats("MonkUnarmed", 3, "Monk", 12, 14, 12, 10, 14, 10, 2, 0, 0, 8, 1, 0, 6, 1, 18);

        controller.Init(stats, Vector2Int.zero, null, null);
        inventory.Init(stats);

        var unarmed = controller.GetUnarmedDamage();
        Assert(stats.IsMonk, "Monk test character recognized as monk");
        Assert(unarmed.damageDice == stats.MonkUnarmedDamageDie, "Monk unarmed strike uses monk progression damage die");

        Cleanup(controller);
    }

    private static void TestAttackResultUsesUnarmedStrikeLabel()
    {
        var attacker = CreateTestCharacter("UnarmedLabelAttacker");
        var defender = CreateTestCharacter("UnarmedLabelDefender");

        attacker.GridPosition = new Vector2Int(0, 0);
        defender.GridPosition = new Vector2Int(1, 0);

        CombatResult result = attacker.Attack(defender, isFlanking: false, flankingBonus: 0, flankingPartnerName: null);
        Assert(result != null, "Unarmed attack produces a combat result");
        Assert(result != null && result.WeaponName == "Unarmed strike", "Combat result weapon label is 'Unarmed strike' for no-weapon attacks");

        Cleanup(attacker);
        Cleanup(defender);
    }
}
