using System;

namespace DND35.AI.Knowledge
{
    /// <summary>
    /// The ground-truth fact computations the omniscient provider reads (batch 13 step 7). Each reads the subject's live
    /// state, the same state the display tags of StatusTagManager are refreshed from.
    /// </summary>
    public static class KnowledgeFacts
    {
        /// <summary>
        /// The wound band of <paramref name="currentHp"/> out of <paramref name="maxHp"/>: Down at 0 or less, Unhurt at full
        /// hit points (or more, with temporary hit points), Hurt at half or more, else BadlyHurt.
        /// </summary>
        public static WoundBand WoundBandOf(int currentHp, int maxHp)
        {
            if (currentHp <= 0)
                return WoundBand.Down;
            if (maxHp <= 0 || currentHp >= maxHp)
                return WoundBand.Unhurt;
            return currentHp * 2 >= maxHp ? WoundBand.Hurt : WoundBand.BadlyHurt;
        }

        /// <summary>
        /// The creature type of <paramref name="stats"/>, its CreatureType string normalized through the one creature-type
        /// parser (CreatureTypeProgressionDatabase.TryParseCreatureType: "Magical Beast" and "MagicalBeast" are one type;
        /// an empty string is the CharacterStats default, Humanoid). Read live, because wild shape changes the type.
        /// False for a string that names no MM type.
        /// </summary>
        public static bool TryCreatureTypeOf(CharacterStats stats, out CreatureTypeId type)
        {
            type = CreatureTypeId.Humanoid;
            return stats != null && CreatureTypeProgressionDatabase.TryParseCreatureType(stats.CreatureType, out type);
        }

        /// <summary>The MM spelling of a type ("Magical Beast", "Monstrous Humanoid").</summary>
        public static string CreatureTypeName(CreatureTypeId type)
        {
            switch (type)
            {
                case CreatureTypeId.MagicalBeast: return "Magical Beast";
                case CreatureTypeId.MonstrousHumanoid: return "Monstrous Humanoid";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// The worn armor's category, read from the armor slot item's typed data (ItemData.ArmorCat, PHB p.123 Table 7-6),
        /// never from its display tags: no item, or an item that is not armor (a robe), is Unarmored; body armor gives its
        /// Light, Medium or Heavy category; armor of any other category is Other. The base category is the armor's look:
        /// mithral full plate looks heavy though it counts as medium for its wearer (DMG p.284,
        /// MaterialProperties.GetEffectiveArmorCategory), so the apparent fact keeps the base category.
        /// </summary>
        public static WornArmor WornArmorOf(CharacterController c)
        {
            Inventory inv = InventoryOf(c);
            ItemData armor = inv != null ? inv.ArmorRobeSlot : null;
            if (armor == null || !armor.IsArmor)
                return WornArmor.Unarmored;
            switch (armor.ArmorCat)
            {
                case ArmorCategory.Light: return WornArmor.Light;
                case ArmorCategory.Medium: return WornArmor.Medium;
                case ArmorCategory.Heavy: return WornArmor.Heavy;
                default: return WornArmor.Other;
            }
        }

        /// <summary>True when an item in either hand of <paramref name="c"/> passes <paramref name="test"/>.</summary>
        public static bool HandsHold(CharacterController c, Func<ItemData, bool> test)
        {
            Inventory inv = InventoryOf(c);
            if (inv == null || test == null)
                return false;
            return (inv.RightHandSlot != null && test(inv.RightHandSlot)) || (inv.LeftHandSlot != null && test(inv.LeftHandSlot));
        }

        public static bool IsShield(ItemData item) => item != null && item.IsShield;

        /// <summary>A melee weapon; the virtual unarmed strike is not one (an empty hand is unarmed).</summary>
        public static bool IsMeleeWeapon(ItemData item)
            => item != null && item.IsWeapon && !item.IsRangedWeapon && !string.Equals(item.Id, DND35e.Identifiers.ItemIDs.UNARMED_STRIKE, StringComparison.Ordinal);

        public static bool IsRangedWeapon(ItemData item) => item != null && item.IsRangedWeapon;

        public static bool IsReachWeapon(ItemData item) => item != null && item.IsWeapon && (item.IsReachWeapon || item.HasReach);

        private static Inventory InventoryOf(CharacterController c)
            => c != null && c.Inventory != null ? c.Inventory.GetInventory() : null;
    }
}
