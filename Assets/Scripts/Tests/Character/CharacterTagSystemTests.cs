using UnityEngine;
using Tests.Utilities;

namespace Tests.Character
{
/// <summary>
/// Runtime tests for the character tag system (armor classification + unarmored flow).
/// Run by calling CharacterTagSystemTests.RunAll().
/// </summary>
public static class CharacterTagSystemTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CHARACTER TAG SYSTEM TESTS ======");
        TestHelpers.EnsureCoreDatabasesInitialized();

        TestStartsUnarmored();
        TestEquipLightArmorAppliesTags();
        TestSwitchArmorReplacesTags();
        TestUnequipArmorRestoresUnarmored();
        TestIndependentCharacterTagSets();

        Debug.Log($"====== Character Tag Results: {_passed} passed, {_failed} failed ======");
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

    private static void TestStartsUnarmored()
    {
        CharacterController c = TestHelpers.CreateCharacter(name: "Tag_Unarmored");
        try
        {
            Assert(c.Tags.HasTag("Unarmored"), "Character starts with Unarmored tag");
            Assert(!c.Tags.HasTag("Light Armor") && !c.Tags.HasTag("Medium Armor") && !c.Tags.HasTag("Heavy Armor"),
                "Character starts without armor classification tags");
        }
        finally
        {
            TestHelpers.Cleanup(c != null ? c.gameObject : null);
        }
    }

    private static void TestEquipLightArmorAppliesTags()
    {
        CharacterController c = TestHelpers.CreateCharacter(name: "Tag_LightArmor");
        try
        {
            InventoryComponent invComp = c.GetComponent<InventoryComponent>();
            invComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chain_shirt"), EquipSlot.Armor);

            Assert(!c.Tags.HasTag("Unarmored"), "Equipping armor removes Unarmored tag");
            Assert(c.Tags.HasTag("Light Armor"), "Chain Shirt applies Light Armor classification tag");
            Assert(c.Tags.HasTag("Chain Shirt"), "Chain Shirt applies item visual tag");
        }
        finally
        {
            TestHelpers.Cleanup(c != null ? c.gameObject : null);
        }
    }

    private static void TestSwitchArmorReplacesTags()
    {
        CharacterController c = TestHelpers.CreateCharacter(name: "Tag_SwitchArmor");
        try
        {
            InventoryComponent invComp = c.GetComponent<InventoryComponent>();
            invComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chain_shirt"), EquipSlot.Armor);
            invComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("full_plate"), EquipSlot.Armor);

            Assert(!c.Tags.HasTag("Light Armor") && !c.Tags.HasTag("Chain Shirt"),
                "Switching armor removes previous armor tags");
            Assert(c.Tags.HasTag("Heavy Armor") && c.Tags.HasTag("Full Plate"),
                "Switching armor applies new heavy armor tags");
        }
        finally
        {
            TestHelpers.Cleanup(c != null ? c.gameObject : null);
        }
    }

    private static void TestUnequipArmorRestoresUnarmored()
    {
        CharacterController c = TestHelpers.CreateCharacter(name: "Tag_UnequipArmor");
        try
        {
            InventoryComponent invComp = c.GetComponent<InventoryComponent>();
            invComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("full_plate"), EquipSlot.Armor);
            bool unequipped = invComp.CharacterInventory.Unequip(EquipSlot.Armor);

            Assert(unequipped, "Unequip armor returns success");
            Assert(c.Tags.HasTag("Unarmored"), "Unequipping armor restores Unarmored tag");
            Assert(!c.Tags.HasTag("Heavy Armor") && !c.Tags.HasTag("Full Plate"),
                "Unequipping armor removes inherited armor tags");
        }
        finally
        {
            TestHelpers.Cleanup(c != null ? c.gameObject : null);
        }
    }

    private static void TestIndependentCharacterTagSets()
    {
        CharacterController fighter = TestHelpers.CreateCharacter(name: "Tag_Fighter");
        CharacterController rogue = TestHelpers.CreateCharacter(name: "Tag_Rogue");
        CharacterController wizard = TestHelpers.CreateCharacter(name: "Tag_Wizard");

        try
        {
            fighter.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem("full_plate"), EquipSlot.Armor);
            rogue.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem("leather_armor"), EquipSlot.Armor);

            Assert(fighter.Tags.HasTag("Heavy Armor") && fighter.Tags.HasTag("Full Plate"),
                "Fighter has heavy armor tags");
            Assert(rogue.Tags.HasTag("Light Armor") && rogue.Tags.HasTag("Leather Armor"),
                "Rogue has light armor tags");
            Assert(wizard.Tags.HasTag("Unarmored"),
                "Wizard remains unarmored");

            Assert(!fighter.Tags.HasTag("Light Armor") && !fighter.Tags.HasTag("Leather Armor"),
                "Fighter tags are independent of Rogue tags");
            Assert(!rogue.Tags.HasTag("Heavy Armor") && !rogue.Tags.HasTag("Full Plate"),
                "Rogue tags are independent of Fighter tags");
        }
        finally
        {
            TestHelpers.Cleanup(
                fighter != null ? fighter.gameObject : null,
                rogue != null ? rogue.gameObject : null,
                wizard != null ? wizard.gameObject : null);
        }
    }
}
}
