using UnityEngine;
using DND35e.Identifiers;

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
        CharacterInventory.OwnerCharacter = GetComponent<CharacterController>();
    }

    /// <summary>
    /// Set up starting equipment and extra inventory items for Aldric (Fighter).
    /// </summary>
    public void SetupAldric()
    {
        ItemDatabase.Init();

        // Equipped items
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAIN_SHIRT), EquipSlot.Armor);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SPIKED_GAUNTLET), EquipSlot.Hands);

        // Extra items in inventory - showcase variety of PHB weapons and armor
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.MACE_HEAVY));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.GREATSWORD));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.BATTLEAXE));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.GAUNTLET));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.SCALE_MAIL));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.BREASTPLATE));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_SHIELD_OF_FAITH));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.TORCH));

        CharacterInventory.RecalculateStats();
    }

    /// <summary>
    /// Set up starting equipment and extra inventory items for Lyra (Rogue).
    /// </summary>
    public void SetupLyra()
    {
        ItemDatabase.Init();

        // Equipped items - Lyra dual wields short sword + dagger
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LEATHER_ARMOR), EquipSlot.Armor);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHORT_SWORD), EquipSlot.RightHand);
        CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.DAGGER), EquipSlot.LeftHand);

        // Extra items in inventory - rogue-appropriate gear
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.RAPIER));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.HANDAXE));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.SHORTBOW));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.BUCKLER));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.STUDDED_LEATHER));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_GREATER_HEALING));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_SHIELD_OF_FAITH));
        CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.ROPE));

        CharacterInventory.RecalculateStats();
    }
}