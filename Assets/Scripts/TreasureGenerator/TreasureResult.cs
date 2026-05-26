// ============================================================================
// TreasureResult.cs — Data classes for generated treasure results
// D&D 3.5e DMG Treasure Generation System
// ============================================================================
using System.Collections.Generic;
using System.Text;

namespace DND35e.Treasure
{
    /// <summary>
    /// Represents a single generated gem with name, value, and quality tier.
    /// </summary>
    public class GemResult
    {
        public string Name;
        public int Value;
        public string Tier; // e.g. "10 gp", "50 gp", etc.

        public override string ToString() => $"{Name} ({Value:N0} gp)";
    }

    /// <summary>
    /// Represents a single generated art object with description and value.
    /// </summary>
    public class ArtResult
    {
        public string Name;
        public int Value;
        public string Tier;

        public override string ToString() => $"{Name} ({Value:N0} gp)";
    }

    /// <summary>
    /// Represents a generated mundane item (masterwork weapon, alchemical item, etc.).
    /// </summary>
    public class MundaneItemResult
    {
        public string Name;
        public int Value;

        public override string ToString() => $"{Name} ({Value:N0} gp)";
    }

    /// <summary>
    /// Represents a generated magic item (armor, weapon, potion, scroll, etc.).
    /// </summary>
    public class MagicItemResult
    {
        public string Name;
        public int Price;
        public string Type;        // armor, weapon, potion, ring, rod, scroll, staff, wand, wondrous
        public string Subtype;     // specific, null
        public int Enhancement;    // +1 through +5 for enhanced items
        public string WeaponType;  // melee, ranged (for weapons)
        public string ScrollType;  // arcane, divine (for scrolls)
        public List<string> Abilities;   // special abilities on armor/weapons
        public List<ScrollSpellResult> Spells; // spells on scrolls

        public override string ToString()
        {
            string priceStr = Price > 0 ? $" ({Price:N0} gp)" : "";
            return $"{Name}{priceStr}";
        }
    }

    /// <summary>
    /// Represents a single spell on a scroll.
    /// </summary>
    public class ScrollSpellResult
    {
        public string Name;
        public int Level;
        public int CasterLevel;
        public int Price;
    }

    /// <summary>
    /// Complete treasure result for an encounter, containing all coins, gems, art, and items.
    /// </summary>
    public class TreasureResult
    {
        public int EncounterLevel;
        public int NumCreatures;
        public int MonsterGearSubtracted;

        // Coins
        public int CopperPieces;
        public int SilverPieces;
        public int GoldPieces;
        public int PlatinumPieces;
        public int CoinsGPValue;

        // Goods
        public List<GemResult> Gems = new List<GemResult>();
        public int GemsGPValue;
        public List<ArtResult> ArtObjects = new List<ArtResult>();
        public int ArtGPValue;

        // Items
        public List<MundaneItemResult> MundaneItems = new List<MundaneItemResult>();
        public int MundaneGPValue;
        public List<MagicItemResult> MagicItems = new List<MagicItemResult>();
        public int MagicItemsGPValue;

        // Total
        public int TotalGPValue;

        // Generation log for debugging
        public List<string> Log = new List<string>();

        /// <summary>Returns true if no treasure was generated at all.</summary>
        public bool IsEmpty =>
            CopperPieces == 0 && SilverPieces == 0 && GoldPieces == 0 && PlatinumPieces == 0 &&
            Gems.Count == 0 && ArtObjects.Count == 0 &&
            MundaneItems.Count == 0 && MagicItems.Count == 0;

        /// <summary>Returns a formatted summary of all treasure.</summary>
        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Treasure for EL {EncounterLevel} (Total: {TotalGPValue:N0} gp) ===");

            // Coins
            if (CoinsGPValue > 0)
            {
                sb.Append("Coins: ");
                var parts = new List<string>();
                if (CopperPieces > 0) parts.Add($"{CopperPieces:N0} cp");
                if (SilverPieces > 0) parts.Add($"{SilverPieces:N0} sp");
                if (GoldPieces > 0) parts.Add($"{GoldPieces:N0} gp");
                if (PlatinumPieces > 0) parts.Add($"{PlatinumPieces:N0} pp");
                sb.AppendLine(string.Join(", ", parts) + $" (≈{CoinsGPValue:N0} gp)");
            }

            // Gems
            if (Gems.Count > 0)
            {
                sb.AppendLine($"Gems ({Gems.Count}, total {GemsGPValue:N0} gp):");
                foreach (var g in Gems) sb.AppendLine($"  • {g}");
            }

            // Art
            if (ArtObjects.Count > 0)
            {
                sb.AppendLine($"Art Objects ({ArtObjects.Count}, total {ArtGPValue:N0} gp):");
                foreach (var a in ArtObjects) sb.AppendLine($"  • {a}");
            }

            // Mundane
            if (MundaneItems.Count > 0)
            {
                sb.AppendLine($"Mundane Items ({MundaneItems.Count}, total {MundaneGPValue:N0} gp):");
                foreach (var m in MundaneItems) sb.AppendLine($"  • {m}");
            }

            // Magic
            if (MagicItems.Count > 0)
            {
                sb.AppendLine($"Magic Items ({MagicItems.Count}, total {MagicItemsGPValue:N0} gp):");
                foreach (var i in MagicItems) sb.AppendLine($"  • {i}");
            }

            if (MonsterGearSubtracted > 0)
                sb.AppendLine($"(Monster gear subtracted: {MonsterGearSubtracted:N0} gp)");

            return sb.ToString();
        }
    }
}
