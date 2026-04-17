using UnityEngine;

/// <summary>
/// MonoBehaviour wrapper that holds a character's Inventory.
/// Attach to character GameObjects alongside CharacterController.
/// </summary>
public class InventoryComponent : MonoBehaviour
{
    public Inventory CharacterInventory;

    /// <summary>
    /// Initialize inventory with starting equipment and items.
    /// </summary>
    public void Init(CharacterStats stats)
    {
        CharacterInventory = new Inventory();
        CharacterInventory.OwnerStats = stats;
    }

    /// <summary>
    /// Set up starting equipment and extra inventory items for Aldric (Fighter).
    /// </summary>
    public void SetupAldric()
    {
        ItemDatabase.Init();

        // Equipped items
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chain_shirt"), EquipSlot.Armor);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem("spiked_gauntlet"), EquipSlot.Hands);

        // Extra items in inventory - showcase variety of PHB weapons and armor
        CharacterInventory.AddItem(ItemDatabase.CloneItem("mace_heavy"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("dagger"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("greatsword"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("battleaxe"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("gauntlet"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("scale_mail"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("breastplate"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_shield_of_faith"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("torch"));

        CharacterInventory.RecalculateStats();
    }

    /// <summary>
    /// Set up starting equipment and extra inventory items for Lyra (Rogue).
    /// </summary>
    public void SetupLyra()
    {
        ItemDatabase.Init();

        // Equipped items - Lyra dual wields short sword + dagger
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem("leather_armor"), EquipSlot.Armor);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem("short_sword"), EquipSlot.RightHand);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem("dagger"), EquipSlot.LeftHand);

        // Extra items in inventory - rogue-appropriate gear
        CharacterInventory.AddItem(ItemDatabase.CloneItem("rapier"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("dagger"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("handaxe"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("shortbow"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("buckler"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("studded_leather"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_greater_healing"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_shield_of_faith"));
        CharacterInventory.AddItem(ItemDatabase.CloneItem("rope"));

        CharacterInventory.RecalculateStats();
    }
}