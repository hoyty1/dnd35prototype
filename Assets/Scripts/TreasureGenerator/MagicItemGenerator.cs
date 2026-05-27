// ============================================================================
// MagicItemGenerator.cs — D&D 3.5e Magic Item Generation Engine
// Port of js/magic_items.js. Implements all Table 7-x chains.
// Reference: DMG 3.5e Chapter 7 (pp. 216-271)
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace DND35e.Treasure
{
    /// <summary>
    /// Generates random magic items by tier (minor/medium/major) using DMG 3.5e tables.
    /// Handles full table chains: type → bonus/specific/ability → sub-tables.
    /// </summary>
    public static class MagicItemGenerator
    {
        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Generate a random magic item of the given tier.
        /// </summary>
        /// <param name="tier">"minor", "medium", or "major"</param>
        /// <returns>Fully resolved magic item result.</returns>
        public static MagicItemResult Generate(string tier)
        {
            if (string.IsNullOrEmpty(tier)) tier = "minor";
            tier = tier.ToLower();

            // Table 7-1: Determine item type
            var typeTable = GetTable7_1(tier);
            var typeEntry = TreasureDice.LookupPercentIndex(typeTable);
            if (typeEntry < 0)
                return new MagicItemResult { Name = "Unknown magic item", Price = 0, Type = "unknown" };

            string itemType = typeTable[typeEntry].Type;

            switch (itemType)
            {
                case "armor":    return GenerateArmor(tier);
                case "weapon":   return GenerateWeapon(tier);
                case "potion":   return GeneratePotion(tier);
                case "ring":     return GenerateRing(tier);
                case "rod":      return GenerateRod(tier);
                case "scroll":   return GenerateScroll(tier);
                case "staff":    return GenerateStaff(tier);
                case "wand":     return GenerateWand(tier);
                case "wondrous": return GenerateWondrous(tier);
                default:
                    return new MagicItemResult { Name = $"Unknown {itemType}", Price = 0, Type = itemType };
            }
        }

        // ====================================================================
        // ARMOR & SHIELDS (Tables 7-2 through 7-8)
        // ====================================================================

        public static MagicItemResult GenerateArmor(string tier)
        {
            var table = GetTable7_2(tier);
            int idx = TreasureDice.LookupPercentIndex(table);
            if (idx < 0)
                return new MagicItemResult { Name = "Unknown armor", Price = 0, Type = "armor" };

            var entry = table[idx];

            // Specific armor
            if (entry.Type == "specific_armor")
            {
                var specTable = GetTable7_7(tier);
                int si = TreasureDice.LookupPercentIndex(specTable);
                if (si >= 0)
                    return new MagicItemResult { Name = specTable[si].Name, Price = specTable[si].Price, Type = "armor", Subtype = "specific" };
            }

            // Specific shield
            if (entry.Type == "specific_shield")
            {
                var specTable = GetTable7_8(tier);
                int si = TreasureDice.LookupPercentIndex(specTable);
                if (si >= 0)
                    return new MagicItemResult { Name = specTable[si].Name, Price = specTable[si].Price, Type = "shield", Subtype = "specific" };
            }

            // Special ability: roll base bonus first, then add abilities
            if (entry.Type == "special_ability")
            {
                ArmorBonusEntry baseEntry = default;
                bool found = false;
                for (int attempts = 0; attempts < 10; attempts++)
                {
                    int bi = TreasureDice.LookupPercentIndex(table);
                    if (bi >= 0 && table[bi].Bonus > 0)
                    {
                        baseEntry = table[bi];
                        found = true;
                        break;
                    }
                }
                if (!found)
                    baseEntry = new ArmorBonusEntry { Bonus = 1, Type = TreasureDice.Roll(2) == 1 ? "armor" : "shield", Price = 1000 };

                return AddArmorAbility(baseEntry, tier);
            }

            // Standard enhanced armor/shield
            bool isShield = entry.Type == "shield";
            string baseItem;
            int baseCost;

            if (isShield)
            {
                int si = TreasureDice.LookupPercentIndex(TreasureData.Table7_4);
                baseItem = si >= 0 ? TreasureData.Table7_4[si].Name : "Heavy steel shield";
                baseCost = si >= 0 ? TreasureData.Table7_4[si].Cost : 20;
            }
            else
            {
                int ai = TreasureDice.LookupPercentIndex(TreasureData.Table7_3);
                baseItem = ai >= 0 ? TreasureData.Table7_3[ai].Name : "Chain shirt";
                baseCost = ai >= 0 ? TreasureData.Table7_3[ai].Cost : 100;
            }

            int enhPrice = entry.Bonus * entry.Bonus * 1000;
            return new MagicItemResult
            {
                Name = $"+{entry.Bonus} {baseItem}",
                Price = enhPrice + baseCost,
                Type = isShield ? "shield" : "armor",
                Enhancement = entry.Bonus
            };
        }

        private static MagicItemResult AddArmorAbility(ArmorBonusEntry baseEntry, string tier)
        {
            bool isShield = baseEntry.Type == "shield";

            string baseItem;
            int baseCost;
            if (isShield)
            {
                int si = TreasureDice.LookupPercentIndex(TreasureData.Table7_4);
                baseItem = si >= 0 ? TreasureData.Table7_4[si].Name : "Heavy steel shield";
                baseCost = si >= 0 ? TreasureData.Table7_4[si].Cost : 20;
            }
            else
            {
                int ai = TreasureDice.LookupPercentIndex(TreasureData.Table7_3);
                baseItem = ai >= 0 ? TreasureData.Table7_3[ai].Name : "Chain shirt";
                baseCost = ai >= 0 ? TreasureData.Table7_3[ai].Cost : 100;
            }

            var abilTable = GetTable7_5(tier);
            int abi = TreasureDice.LookupPercentIndex(abilTable);

            // On reroll or miss, just bump enhancement
            if (abi < 0 || abilTable[abi].ModType == "reroll")
            {
                int newBonus = Mathf.Min(baseEntry.Bonus + 1, 5);
                int enhP = newBonus * newBonus * 1000;
                return new MagicItemResult
                {
                    Name = $"+{newBonus} {baseItem}",
                    Price = enhP + baseCost,
                    Type = isShield ? "shield" : "armor",
                    Enhancement = newBonus
                };
            }

            var ability = abilTable[abi];
            int totalPrice = baseCost;
            int effectiveBonus = baseEntry.Bonus;

            if (ability.ModType == "bonus")
            {
                effectiveBonus += ability.Mod;
                totalPrice += effectiveBonus * effectiveBonus * 1000;
            }
            else
            {
                totalPrice += (baseEntry.Bonus * baseEntry.Bonus * 1000) + ability.Mod;
            }

            return new MagicItemResult
            {
                Name = $"+{baseEntry.Bonus} {ability.Name} {baseItem}",
                Price = totalPrice,
                Type = isShield ? "shield" : "armor",
                Enhancement = baseEntry.Bonus,
                Abilities = new List<string> { ability.Name }
            };
        }

        // ====================================================================
        // WEAPONS (Tables 7-9 through 7-16)
        // ====================================================================

        public static MagicItemResult GenerateWeapon(string tier)
        {
            var table = GetTable7_9(tier);
            int idx = TreasureDice.LookupPercentIndex(table);
            if (idx < 0)
                return new MagicItemResult { Name = "Unknown weapon", Price = 0, Type = "weapon" };

            var entry = table[idx];

            // Specific weapon
            if (entry.Type == "specific")
            {
                var specTable = GetTable7_16(tier);
                int si = TreasureDice.LookupPercentIndex(specTable);
                if (si >= 0)
                    return new MagicItemResult { Name = specTable[si].Name, Price = specTable[si].Price, Type = "weapon", Subtype = "specific" };
            }

            // Special ability
            if (entry.Type == "special_ability")
            {
                WeaponBonusEntry baseEntry = default;
                bool found = false;
                for (int attempts = 0; attempts < 10; attempts++)
                {
                    int bi = TreasureDice.LookupPercentIndex(table);
                    if (bi >= 0 && table[bi].Bonus > 0)
                    {
                        baseEntry = table[bi];
                        found = true;
                        break;
                    }
                }
                if (!found)
                    baseEntry = new WeaponBonusEntry { Bonus = 1, Price = 2000 };

                return AddWeaponAbility(baseEntry, tier);
            }

            // Standard enhanced weapon
            RollWeaponType(out string weapName, out int weapCost, out string category);
            int enhPrice = entry.Bonus * entry.Bonus * 2000;

            return new MagicItemResult
            {
                Name = $"+{entry.Bonus} {weapName}",
                Price = enhPrice + weapCost,
                Type = "weapon",
                Enhancement = entry.Bonus,
                WeaponType = category
            };
        }

        private static void RollWeaponType(out string name, out int cost, out string category)
        {
            int ci = TreasureDice.LookupPercentIndex(TreasureData.Table7_10);
            string catType = ci >= 0 ? TreasureData.Table7_10[ci].Type : "common_melee";

            WeaponEntry[] weapTable;
            if (catType == "common_melee") { weapTable = TreasureData.Table7_11; category = "melee"; }
            else if (catType == "uncommon") { weapTable = TreasureData.Table7_12; category = "melee"; }
            else { weapTable = TreasureData.Table7_13; category = "ranged"; }

            int wi = TreasureDice.LookupPercentIndex(weapTable);
            name = wi >= 0 ? weapTable[wi].Name : "Longsword";
            cost = wi >= 0 ? weapTable[wi].Cost : 15;
        }

        private static MagicItemResult AddWeaponAbility(WeaponBonusEntry baseEntry, string tier)
        {
            RollWeaponType(out string weapName, out int weapCost, out string category);
            bool isRanged = category == "ranged";

            var abilTable = isRanged ? GetTable7_15(tier) : GetTable7_14(tier);
            int abi = TreasureDice.LookupPercentIndex(abilTable);

            // On reroll or miss, bump enhancement
            if (abi < 0 || abilTable[abi].ModType == "reroll")
            {
                int newBonus = Mathf.Min(baseEntry.Bonus + 1, 5);
                int totalP = newBonus * newBonus * 2000 + weapCost;
                return new MagicItemResult
                {
                    Name = $"+{newBonus} {weapName}",
                    Price = totalP,
                    Type = "weapon",
                    Enhancement = newBonus
                };
            }

            var ability = abilTable[abi];
            string abilityName = ability.Name;

            // Handle Bane: determine designated foe
            if (abilityName == "Bane")
            {
                int fi = TreasureDice.LookupPercentIndex(TreasureData.BaneFoes);
                if (fi >= 0)
                    abilityName = $"Bane ({TreasureData.BaneFoes[fi].Name})";
            }

            int totalPrice = weapCost;
            int effectiveBonus = baseEntry.Bonus;

            if (ability.ModType == "bonus")
            {
                effectiveBonus += ability.Mod;
                totalPrice += effectiveBonus * effectiveBonus * 2000;
            }
            else
            {
                totalPrice += (baseEntry.Bonus * baseEntry.Bonus * 2000) + ability.Mod;
            }

            return new MagicItemResult
            {
                Name = $"+{baseEntry.Bonus} {abilityName} {weapName}",
                Price = totalPrice,
                Type = "weapon",
                Enhancement = baseEntry.Bonus,
                Abilities = new List<string> { abilityName }
            };
        }

        // ====================================================================
        // POTIONS (Table 7-17)
        // ====================================================================

        public static MagicItemResult GeneratePotion(string tier)
        {
            var table = GetTable7_17(tier);
            int idx = TreasureDice.LookupPercentIndex(table);
            if (idx < 0)
                return new MagicItemResult { Name = "Unknown potion", Price = 50, Type = "potion" };

            var entry = table[idx];

            // Handle reroll directives
            if (!string.IsNullOrEmpty(entry.Reroll))
            {
                var rerollTable = GetTable7_17(entry.Reroll);
                int ri = TreasureDice.LookupPercentIndex(rerollTable);
                if (ri >= 0)
                    entry = rerollTable[ri];
            }

            return new MagicItemResult { Name = entry.Name, Price = entry.Price, Type = "potion" };
        }

        // ====================================================================
        // RINGS (Table 7-18)
        // ====================================================================

        public static MagicItemResult GenerateRing(string tier)
        {
            var table = GetTable7_18(tier);
            int idx = TreasureDice.LookupPercentIndex(table);
            if (idx < 0)
                return new MagicItemResult { Name = "Unknown ring", Price = 2000, Type = "ring" };

            return new MagicItemResult { Name = table[idx].Name, Price = table[idx].Price, Type = "ring" };
        }

        // ====================================================================
        // RODS (Table 7-19) — Medium/Major only
        // ====================================================================

        public static MagicItemResult GenerateRod(string tier)
        {
            string effectiveTier = (tier == "minor") ? "medium" : tier;
            var table = GetTable7_19(effectiveTier);
            int idx = TreasureDice.LookupPercentIndex(table);
            if (idx < 0)
                return new MagicItemResult { Name = "Unknown rod", Price = 5000, Type = "rod" };

            return new MagicItemResult { Name = table[idx].Name, Price = table[idx].Price, Type = "rod" };
        }

        // ====================================================================
        // SCROLLS (Tables 7-20 through 7-24)
        // ====================================================================

        public static MagicItemResult GenerateScroll(string tier)
        {
            // Step 1: Arcane or Divine? (Table 7-20)
            int ti = TreasureDice.LookupPercentIndex(TreasureData.Table7_20);
            string scrollType = ti >= 0 ? TreasureData.Table7_20[ti].Type : "arcane";

            // Step 2: Number of spells (Table 7-21)
            string numDice;
            if (!TreasureData.Table7_21.TryGetValue(tier, out numDice))
                numDice = "1d3";
            int numSpells = TreasureDice.Evaluate(numDice);

            // Step 3: For each spell, determine level and pick spell
            var spells = new List<ScrollSpellResult>();
            int totalPrice = 0;

            var levelTable = GetTable7_22(tier);

            for (int i = 0; i < numSpells; i++)
            {
                int li = TreasureDice.LookupPercentIndex(levelTable);
                if (li < 0) continue;

                int spellLevel = levelTable[li].Level;
                int casterLevel = levelTable[li].CasterLevel;

                // Price: spell level * caster level * 25 (0-level = 0.5 for pricing)
                int pricePerSpell = spellLevel == 0
                    ? Mathf.RoundToInt(0.5f * casterLevel * 25)
                    : spellLevel * casterLevel * 25;

                string spellName = PickScrollSpell(scrollType, spellLevel);
                spells.Add(new ScrollSpellResult
                {
                    Name = spellName,
                    Level = spellLevel,
                    CasterLevel = casterLevel,
                    Price = pricePerSpell
                });
                totalPrice += pricePerSpell;
            }

            // Build name
            var sb = new System.Text.StringBuilder();
            sb.Append($"Scroll ({scrollType}): ");
            for (int i = 0; i < spells.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{spells[i].Name} (lvl {spells[i].Level}, CL {spells[i].CasterLevel})");
            }

            return new MagicItemResult
            {
                Name = sb.ToString(),
                Price = totalPrice,
                Type = "scroll",
                ScrollType = scrollType,
                Spells = spells
            };
        }

        private static string PickScrollSpell(string type, int level)
        {
            // Try the requested level first, then fall back to lower levels
            // if no implemented spells exist at the rolled level.
            for (int lv = level; lv >= 0; lv--)
            {
                string key = $"{type}_{lv}";
                if (TreasureData.ScrollSpells.TryGetValue(key, out string[] spells) && spells.Length > 0)
                    return spells[UnityEngine.Random.Range(0, spells.Length)];
            }
            return $"{type} spell (level {level})";
        }

        // ====================================================================
        // STAFFS (Table 7-25) — Medium/Major only
        // ====================================================================

        public static MagicItemResult GenerateStaff(string tier)
        {
            string effectiveTier = (tier == "minor") ? "medium" : tier;
            var table = GetTable7_25(effectiveTier);
            int idx = TreasureDice.LookupPercentIndex(table);
            if (idx < 0)
                return new MagicItemResult { Name = "Unknown staff", Price = 16500, Type = "staff" };

            return new MagicItemResult { Name = table[idx].Name, Price = table[idx].Price, Type = "staff" };
        }

        // ====================================================================
        // WANDS (Table 7-26)
        // ====================================================================

        public static MagicItemResult GenerateWand(string tier)
        {
            var table = GetTable7_26(tier);
            int idx = TreasureDice.LookupPercentIndex(table);
            if (idx < 0)
                return new MagicItemResult { Name = "Unknown wand", Price = 375, Type = "wand" };

            return new MagicItemResult { Name = table[idx].Name, Price = table[idx].Price, Type = "wand" };
        }

        // ====================================================================
        // WONDROUS ITEMS (Tables 7-27, 7-28, 7-29)
        // ====================================================================

        public static MagicItemResult GenerateWondrous(string tier)
        {
            WondrousEntry[] table;
            switch (tier)
            {
                case "medium": table = TreasureData.Table7_28; break;
                case "major":  table = TreasureData.Table7_29; break;
                default:       table = TreasureData.Table7_27; break;
            }

            int idx = TreasureDice.LookupPercentIndex(table);
            if (idx < 0)
                return new MagicItemResult { Name = "Unknown wondrous item", Price = 0, Type = "wondrous" };

            return new MagicItemResult { Name = table[idx].Name, Price = table[idx].Price, Type = "wondrous" };
        }

        // ====================================================================
        // TABLE LOOKUP HELPERS — map tier string to typed arrays
        // ====================================================================

        private static MagicTypeEntry[] GetTable7_1(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_1_Medium;
                case "major":  return TreasureData.Table7_1_Major;
                default:       return TreasureData.Table7_1_Minor;
            }
        }

        private static ArmorBonusEntry[] GetTable7_2(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_2_Medium;
                case "major":  return TreasureData.Table7_2_Major;
                default:       return TreasureData.Table7_2_Minor;
            }
        }

        private static ArmorAbilityEntry[] GetTable7_5(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_5_Medium;
                case "major":  return TreasureData.Table7_5_Major;
                default:       return TreasureData.Table7_5_Minor;
            }
        }

        private static SpecificItemEntry[] GetTable7_7(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_7_Medium;
                case "major":  return TreasureData.Table7_7_Major;
                default:       return TreasureData.Table7_7_Minor;
            }
        }

        private static SpecificItemEntry[] GetTable7_8(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_8_Medium;
                case "major":  return TreasureData.Table7_8_Major;
                default:       return TreasureData.Table7_8_Minor;
            }
        }

        private static WeaponBonusEntry[] GetTable7_9(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_9_Medium;
                case "major":  return TreasureData.Table7_9_Major;
                default:       return TreasureData.Table7_9_Minor;
            }
        }

        private static WeaponAbilityEntry[] GetTable7_14(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_14_Medium;
                case "major":  return TreasureData.Table7_14_Major;
                default:       return TreasureData.Table7_14_Minor;
            }
        }

        private static WeaponAbilityEntry[] GetTable7_15(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_15_Medium;
                case "major":  return TreasureData.Table7_15_Major;
                default:       return TreasureData.Table7_15_Minor;
            }
        }

        private static SpecificItemEntry[] GetTable7_16(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_16_Medium;
                case "major":  return TreasureData.Table7_16_Major;
                default:       return TreasureData.Table7_16_Minor;
            }
        }

        private static PotionEntry[] GetTable7_17(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_17_Medium;
                case "major":  return TreasureData.Table7_17_Major;
                default:       return TreasureData.Table7_17_Minor;
            }
        }

        private static RingEntry[] GetTable7_18(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_18_Medium;
                case "major":  return TreasureData.Table7_18_Major;
                default:       return TreasureData.Table7_18_Minor;
            }
        }

        private static RodEntry[] GetTable7_19(string tier)
        {
            switch (tier)
            {
                case "major": return TreasureData.Table7_19_Major;
                default:      return TreasureData.Table7_19_Medium;
            }
        }

        private static ScrollLevelEntry[] GetTable7_22(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_22_Medium;
                case "major":  return TreasureData.Table7_22_Major;
                default:       return TreasureData.Table7_22_Minor;
            }
        }

        private static StaffEntry[] GetTable7_25(string tier)
        {
            switch (tier)
            {
                case "major": return TreasureData.Table7_25_Major;
                default:      return TreasureData.Table7_25_Medium;
            }
        }

        private static WandEntry[] GetTable7_26(string tier)
        {
            switch (tier)
            {
                case "medium": return TreasureData.Table7_26_Medium;
                case "major":  return TreasureData.Table7_26_Major;
                default:       return TreasureData.Table7_26_Minor;
            }
        }
    }
}
