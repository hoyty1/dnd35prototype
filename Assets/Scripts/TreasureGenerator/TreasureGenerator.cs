// ============================================================================
// TreasureGenerator.cs — D&D 3.5e Treasure Generation Engine
// Port of js/treasure.js. Implements Table 3-5 treasure generation by EL.
// Reference: DMG 3.5e Chapter 3 (pp. 52-58)
// ============================================================================
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DND35e.Treasure
{
    /// <summary>
    /// Main treasure generation engine. Generates complete treasure hoards
    /// based on Encounter Level using DMG 3.5e Table 3-5 and sub-tables.
    /// </summary>
    public static class TreasureGenerator
    {
        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Generate complete treasure for an encounter.
        /// </summary>
        /// <param name="el">Encounter Level (1-20)</param>
        /// <param name="monsterGearGP">Total GP value of monster magical gear to subtract (optional)</param>
        /// <returns>Complete treasure breakdown.</returns>
        public static TreasureResult Generate(int el, int monsterGearGP = 0)
        {
            el = Mathf.Clamp(el, 1, 20);
            monsterGearGP = Mathf.Max(0, monsterGearGP);

            if (!TreasureData.Table3_5.TryGetValue(el, out EncounterLevelTreasure levelData))
            {
                Debug.LogWarning($"[TreasureGenerator] Invalid EL {el}, defaulting to EL 1.");
                levelData = TreasureData.Table3_5[1];
            }

            var result = new TreasureResult
            {
                EncounterLevel = el,
                MonsterGearSubtracted = monsterGearGP
            };

            result.Log.Add($"=== Treasure for EL {el} ===");

            // Step 1: Roll Coins (Table 3-5 Coins column)
            RollCoins(levelData.Coins, result);

            // Step 2: Roll Goods (Table 3-5 Goods column → Table 3-6 or 3-7)
            RollGoods(levelData.Goods, result);

            // Step 3: Roll Items (Table 3-5 Items column → mundane/minor/medium/major)
            RollItems(levelData.Items, result);

            // Calculate coin GP value
            result.CoinsGPValue = Mathf.RoundToInt(
                (result.CopperPieces / 100f) + (result.SilverPieces / 10f) +
                result.GoldPieces + (result.PlatinumPieces * 10f)
            );

            // Subtract monster magical gear from magic items value (per DMG rules)
            int magicAfterSubtraction = Mathf.Max(0, result.MagicItemsGPValue - monsterGearGP);
            int actualSubtracted = result.MagicItemsGPValue - magicAfterSubtraction;

            if (monsterGearGP > 0)
            {
                result.Log.Add($"--- Monster Gear Subtraction ---");
                result.Log.Add($"Monster magical gear value: {monsterGearGP:N0} gp");
                result.Log.Add($"Subtracted from magic items: {actualSubtracted:N0} gp");
                if (monsterGearGP > result.MagicItemsGPValue)
                    result.Log.Add($"Note: Monster gear exceeds magic item value; excess NOT subtracted from other treasure.");
            }

            result.TotalGPValue = result.CoinsGPValue + result.GemsGPValue +
                result.ArtGPValue + result.MundaneGPValue + magicAfterSubtraction;

            result.Log.Add($"=== TOTAL VALUE: {result.TotalGPValue:N0} gp ===");

            return result;
        }

        // ====================================================================
        // COINS (Table 3-5 Coins column)
        // ====================================================================

        private static void RollCoins(PercentEntry<CoinResult>[] coinTable, TreasureResult result)
        {
            int idx = TreasureDice.LookupPercentIndex(coinTable);
            if (idx < 0 || !coinTable[idx].HasResult)
            {
                result.Log.Add("Coins: None");
                return;
            }

            var r = coinTable[idx].Result;
            int amount = TreasureDice.Evaluate(r.Dice);

            switch (r.Type)
            {
                case "cp": result.CopperPieces = amount; break;
                case "sp": result.SilverPieces = amount; break;
                case "gp": result.GoldPieces = amount; break;
                case "pp": result.PlatinumPieces = amount; break;
            }

            result.Log.Add($"Coins: {amount:N0} {r.Type} [{r.Dice}]");
        }

        // ====================================================================
        // GOODS (Table 3-5 Goods column → gems 3-6 or art 3-7)
        // ====================================================================

        private static void RollGoods(PercentEntry<GoodsResult>[] goodsTable, TreasureResult result)
        {
            int idx = TreasureDice.LookupPercentIndex(goodsTable);
            if (idx < 0 || !goodsTable[idx].HasResult)
            {
                result.Log.Add("Goods: None");
                return;
            }

            var r = goodsTable[idx].Result;
            int count = TreasureDice.Evaluate(r.Count);
            result.Log.Add($"Goods: {count} {r.Type}(s)");

            for (int i = 0; i < count; i++)
            {
                if (r.Type == "gem")
                    RollGem(result);
                else
                    RollArt(result);
            }
        }

        /// <summary>Roll a single gem on Table 3-6.</summary>
        private static void RollGem(TreasureResult result)
        {
            int idx = TreasureDice.LookupPercentIndex(TreasureData.Table3_6);
            if (idx < 0) return;

            var entry = TreasureData.Table3_6[idx];
            int value = RollDiceValue(entry.Dice, entry.Mult);

            var gem = new GemResult
            {
                Name = PickRandom(entry.Desc, ','),
                Value = value,
                Tier = entry.Avg
            };

            result.Gems.Add(gem);
            result.GemsGPValue += value;
            result.Log.Add($"  Gem: {gem.Name} ({value:N0} gp)");
        }

        /// <summary>Roll a single art object on Table 3-7.</summary>
        private static void RollArt(TreasureResult result)
        {
            int idx = TreasureDice.LookupPercentIndex(TreasureData.Table3_7);
            if (idx < 0) return;

            var entry = TreasureData.Table3_7[idx];
            int value = RollDiceValue(entry.Dice, entry.Mult);

            var art = new ArtResult
            {
                Name = PickRandom(entry.Desc, ';'),
                Value = value,
                Tier = entry.Avg
            };

            result.ArtObjects.Add(art);
            result.ArtGPValue += value;
            result.Log.Add($"  Art: {art.Name} ({value:N0} gp)");
        }

        // ====================================================================
        // ITEMS (Table 3-5 Items column → mundane/minor/medium/major)
        // ====================================================================

        private static void RollItems(PercentEntry<ItemResult>[] itemsTable, TreasureResult result)
        {
            int idx = TreasureDice.LookupPercentIndex(itemsTable);
            if (idx < 0 || !itemsTable[idx].HasResult)
            {
                result.Log.Add("Items: None");
                return;
            }

            var r = itemsTable[idx].Result;
            int count = TreasureDice.Evaluate(r.Count);
            result.Log.Add($"Items: {count} {r.Type} item(s)");

            for (int i = 0; i < count; i++)
            {
                if (r.Type == "mundane")
                {
                    RollMundane(result);
                }
                else
                {
                    // minor, medium, or major magic item
                    var item = MagicItemGenerator.Generate(r.Type);
                    result.MagicItems.Add(item);
                    result.MagicItemsGPValue += item.Price;
                    result.Log.Add($"  Magic Item ({r.Type}): {item.Name} ({item.Price:N0} gp)");
                }
            }
        }

        /// <summary>Roll a mundane item on Table 3-8.</summary>
        private static void RollMundane(TreasureResult result)
        {
            int catIdx = TreasureDice.LookupPercentIndex(TreasureData.Table3_8_Categories);
            if (catIdx < 0) return;

            string category = TreasureData.Table3_8_Categories[catIdx].Type;
            string name = null;
            int value = 0;

            if (category == "weapon")
            {
                // Weapon sub-table → common melee / uncommon / ranged
                int wci = TreasureDice.LookupPercentIndex(TreasureData.Table3_8_Weapon);
                if (wci >= 0)
                {
                    string weapCat = TreasureData.Table3_8_Weapon[wci].Type;
                    WeaponEntry[] weapTable;
                    if (weapCat == "common_melee")
                        weapTable = TreasureData.Table7_11;
                    else if (weapCat == "uncommon")
                        weapTable = TreasureData.Table7_12;
                    else
                        weapTable = TreasureData.Table7_13;

                    int wi = TreasureDice.LookupPercentIndex(weapTable);
                    if (wi >= 0)
                    {
                        name = $"Masterwork {weapTable[wi].Name}";
                        value = weapTable[wi].Cost;
                    }
                }
            }
            else
            {
                // Armor, Tools, Alchemical sub-tables
                MundaneItemEntry[] subTable;
                if (category == "armor")
                    subTable = TreasureData.Table3_8_Armor;
                else if (category == "tools")
                    subTable = TreasureData.Table3_8_Tools;
                else
                    subTable = TreasureData.Table3_8_Alchemical;

                int si = TreasureDice.LookupPercentIndex(subTable);
                if (si >= 0)
                {
                    var subEntry = subTable[si];
                    if (!string.IsNullOrEmpty(subEntry.QtyDice))
                    {
                        int qty = TreasureDice.Evaluate(subEntry.QtyDice);
                        name = Regex.Replace(subEntry.Item, @"\(.*?\)", $"(×{qty})");
                        value = subEntry.Price * qty;
                    }
                    else
                    {
                        name = subEntry.Item;
                        value = subEntry.Price;
                    }
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                result.MundaneItems.Add(new MundaneItemResult { Name = name, Value = value });
                result.MundaneGPValue += value;
                result.Log.Add($"  Mundane: {name} ({value:N0} gp)");
            }
        }

        // ====================================================================
        // UTILITY HELPERS
        // ====================================================================

        /// <summary>Parse a dice notation like "2d6" and multiply by a multiplier.</summary>
        private static int RollDiceValue(string dice, int mult)
        {
            var m = Regex.Match(dice, @"(\d+)d(\d+)");
            if (!m.Success) return 0;
            int n = int.Parse(m.Groups[1].Value);
            int s = int.Parse(m.Groups[2].Value);
            return TreasureDice.RollNdS(n, s) * (mult > 0 ? mult : 1);
        }

        /// <summary>Pick a random entry from a delimited string.</summary>
        private static string PickRandom(string desc, char separator)
        {
            if (string.IsNullOrEmpty(desc)) return "Unknown";
            string[] parts = desc.Split(separator);
            var trimmed = new List<string>();
            foreach (var p in parts)
            {
                string t = p.Trim();
                if (t.Length > 0) trimmed.Add(t);
            }
            if (trimmed.Count == 0) return "Unknown";
            return trimmed[UnityEngine.Random.Range(0, trimmed.Count)];
        }
    }
}
