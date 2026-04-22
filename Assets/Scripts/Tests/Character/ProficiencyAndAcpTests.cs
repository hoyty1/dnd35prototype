using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Tests for D&D 3.5 proficiency penalties and armor check penalty (ACP) skill handling.
/// Run by calling ProficiencyAndAcpTests.RunAll() from any MonoBehaviour.
/// </summary>
public static class ProficiencyAndAcpTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== PROFICIENCY & ACP TESTS ======");

        RaceDatabase.Init();
        ItemDatabase.Init();

        TestWeaponProficiencyPenalty();
        TestArmorProficiencyPenalty();
        TestShieldProficiencyPenalty();
        TestSkillAcpWithNonProficientShield();
        TestSwimDoublesFinalAcp();
        TestEscapeArtistReceivesAcp();
        TestNonAcpSkillUnaffected();
        TestMaxDexLimitedByArmorOnly();
        TestShieldDoesNotCapDexWhenUnarmored();
        TestWeaponProficiencyLookupByName();

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeChar(string className)
    {
        // Use simple baseline level-3 stats for test construction.
        return new CharacterStats("Test", 3, className,
            14, 14, 14, 10, 10, 10,
            2, 0, 0,
            8, 1, 0,
            6, 1, 20,
            "Human");
    }

    private static void TestWeaponProficiencyPenalty()
    {
        var fighter = MakeChar("Fighter");
        var wizard = MakeChar("Wizard");
        var longsword = ItemDatabase.Get("longsword");

        int fighterPenalty = fighter.GetWeaponNonProficiencyPenalty(longsword);
        int wizardPenalty = wizard.GetWeaponNonProficiencyPenalty(longsword);

        Assert(fighterPenalty == 0, "Fighter proficient with longsword has no penalty", $"got {fighterPenalty}");
        Assert(wizardPenalty == -4, "Wizard non-proficient with longsword gets -4", $"got {wizardPenalty}");
    }

    private static void TestArmorProficiencyPenalty()
    {
        var fighter = MakeChar("Fighter");
        var rogue = MakeChar("Rogue");
        var fullPlate = ItemDatabase.Get("full_plate");

        fighter.EquippedArmorItem = fullPlate;
        rogue.EquippedArmorItem = fullPlate;

        Assert(fighter.GetArmorNonProficiencyAttackPenalty() == 0,
            "Fighter proficient with heavy armor has no armor attack penalty");
        Assert(rogue.GetArmorNonProficiencyAttackPenalty() == -6,
            "Rogue non-proficient heavy armor applies ACP as attack penalty (-6)",
            $"got {rogue.GetArmorNonProficiencyAttackPenalty()}");
    }

    private static void TestShieldProficiencyPenalty()
    {
        var fighter = MakeChar("Fighter");
        var rogue = MakeChar("Rogue");
        var towerShield = ItemDatabase.Get("tower_shield");

        fighter.EquippedShieldItem = towerShield;
        rogue.EquippedShieldItem = towerShield;

        Assert(fighter.GetArmorNonProficiencyAttackPenalty() == 0,
            "Fighter has tower shield proficiency and gets no tower shield attack penalty",
            $"got {fighter.GetArmorNonProficiencyAttackPenalty()}");
        Assert(rogue.GetArmorNonProficiencyAttackPenalty() == -10,
            "Rogue lacks shield/tower shield proficiency and gets -10 penalty",
            $"got {rogue.GetArmorNonProficiencyAttackPenalty()}");
    }

    private static void TestSkillAcpWithNonProficientShield()
    {
        var rogue = MakeChar("Rogue");
        var heavySteelShield = ItemDatabase.Get("shield_heavy_steel"); // ACP 2

        rogue.EquippedShieldItem = heavySteelShield;

        int climbPenalty = rogue.GetArmorCheckPenaltyForSkill("Climb");
        Assert(climbPenalty == -4,
            "Non-proficient shield doubles ACP for ACP skills (-2 -> -4)",
            $"got {climbPenalty}");
    }

    private static void TestSwimDoublesFinalAcp()
    {
        var rogue = MakeChar("Rogue");
        var heavySteelShield = ItemDatabase.Get("shield_heavy_steel"); // ACP 2, doubled to 4 if non-proficient

        rogue.EquippedShieldItem = heavySteelShield;

        int swimPenalty = rogue.GetArmorCheckPenaltyForSkill("Swim");
        Assert(swimPenalty == -8,
            "Swim doubles the final ACP (non-proficient shield: -4 -> -8)",
            $"got {swimPenalty}");
    }

    private static void TestEscapeArtistReceivesAcp()
    {
        var fighter = MakeChar("Fighter");
        var chainShirt = ItemDatabase.Get("chain_shirt"); // ACP 2

        fighter.EquippedArmorItem = chainShirt;

        int escapeArtistPenalty = fighter.GetArmorCheckPenaltyForSkill("Escape Artist");
        Assert(escapeArtistPenalty == -2,
            "Escape Artist is treated as an ACP-affected skill",
            $"got {escapeArtistPenalty}");
    }

    private static void TestMaxDexLimitedByArmorOnly()
    {
        var fighter = MakeChar("Fighter");
        fighter.DEX = 20; // +5

        var fullPlate = ItemDatabase.Get("full_plate"); // Max Dex +1
        var heavyShield = ItemDatabase.Get("shield_heavy_steel");

        var inv = new Inventory { OwnerStats = fighter, ArmorSlot = fullPlate, LeftHandSlot = heavyShield };
        inv.RecalculateStats();

        Assert(fighter.MaxDexBonus == 1,
            "Max Dex cap is sourced from armor (full plate +1)",
            $"got {fighter.MaxDexBonus}");

        Assert(fighter.ArmorClass == 21,
            "AC applies capped DEX with armor and shield (10 + 8 armor + 1 dex cap + 2 shield)",
            $"got {fighter.ArmorClass}");
    }

    private static void TestShieldDoesNotCapDexWhenUnarmored()
    {
        var rogue = MakeChar("Rogue");
        rogue.DEX = 20; // +5

        var towerShield = ItemDatabase.Get("tower_shield");

        var inv = new Inventory { OwnerStats = rogue, ArmorSlot = null, LeftHandSlot = towerShield };
        inv.RecalculateStats();

        Assert(rogue.MaxDexBonus == -1,
            "Shield alone does not impose a Max Dex cap",
            $"got {rogue.MaxDexBonus}");

        Assert(rogue.ArmorClass == 19,
            "Unarmored AC keeps full DEX while using shield (10 + 5 DEX + 4 shield)",
            $"got {rogue.ArmorClass}");
    }

    private static void TestNonAcpSkillUnaffected()
    {
        var rogue = MakeChar("Rogue");
        var heavySteelShield = ItemDatabase.Get("shield_heavy_steel");

        rogue.EquippedShieldItem = heavySteelShield;

        int spotPenalty = rogue.GetArmorCheckPenaltyForSkill("Spot");
        Assert(spotPenalty == 0, "Non-ACP skill (Spot) gets no armor check penalty", $"got {spotPenalty}");
    }

    private static void TestWeaponProficiencyLookupByName()
    {
        var wizard = MakeChar("Wizard");

        Assert(wizard.IsProficientWithWeaponByName("Crossbow, Light"),
            "Wizard proficiency lookup by display name works for light crossbow");
        Assert(!wizard.IsProficientWithWeaponByName("Longsword"),
            "Wizard proficiency lookup by display name returns false for longsword");
    }
}

}
