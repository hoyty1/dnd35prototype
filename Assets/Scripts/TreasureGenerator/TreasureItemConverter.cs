// ============================================================================
// TreasureItemConverter.cs — Converts D&D 3.5e treasure results into ItemData
// instances for the loot grid, and implements the Appraise skill check system.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Treasure;

/// <summary>
/// Converts treasure generation results (gems, art objects, mundane items, magic items)
/// into ItemData instances suitable for the loot collection grid. Also implements
/// the D&D 3.5e Appraise skill check system (PHB p.67) for gems and art objects.
/// </summary>
public static class TreasureItemConverter
{
    // ========================================================================
    // D&D 3.5e Appraise DC Table (PHB p.67)
    // ========================================================================
    // "You can appraise common or well-known objects with a DC 12 Appraise check."
    // The DC scales with item value:
    //   DC 12: items worth 100 gp or less
    //   DC 15: items worth 101–1,000 gp
    //   DC 20: items worth 1,001–10,000 gp
    //   DC 25: items worth 10,001+ gp

    private static int GetAppraiseDC(int trueValueGp)
    {
        if (trueValueGp <= 100) return 12;
        if (trueValueGp <= 1000) return 15;
        if (trueValueGp <= 10000) return 20;
        return 25;
    }

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>
    /// Result container for treasure item conversion, including all converted items
    /// and the Appraise check log.
    /// </summary>
    public class ConversionResult
    {
        public readonly List<ItemData> Items = new List<ItemData>();
        public readonly List<string> AppraiseLog = new List<string>();
        public int BestAppraiseModifier;
        public string BestAppraiserName;
    }

    /// <summary>
    /// Convert all items from a TreasureResult into ItemData instances.
    /// Performs Appraise skill checks on gems and art objects using the best
    /// Appraise modifier from the party.
    /// </summary>
    /// <param name="treasure">The treasure result to convert.</param>
    /// <param name="partyMembers">List of PCs for Appraise skill checks.</param>
    /// <returns>ConversionResult containing all items and appraise log.</returns>
    public static ConversionResult ConvertAll(TreasureResult treasure, List<CharacterController> partyMembers)
    {
        var result = new ConversionResult();

        if (treasure == null || treasure.IsEmpty)
            return result;

        // Find the best Appraise modifier in the party
        FindBestAppraiser(partyMembers, out int bestMod, out string bestName);
        result.BestAppraiseModifier = bestMod;
        result.BestAppraiserName = bestName;
        result.AppraiseLog.Add($"Best appraiser: {bestName} (Appraise +{bestMod})");

        // Convert gems with Appraise checks
        for (int i = 0; i < treasure.Gems.Count; i++)
        {
            GemResult gem = treasure.Gems[i];
            ItemData item = ConvertGem(gem, bestMod, bestName, result.AppraiseLog);
            if (item != null)
                result.Items.Add(item);
        }

        // Convert art objects with Appraise checks
        for (int i = 0; i < treasure.ArtObjects.Count; i++)
        {
            ArtResult art = treasure.ArtObjects[i];
            ItemData item = ConvertArtObject(art, bestMod, bestName, result.AppraiseLog);
            if (item != null)
                result.Items.Add(item);
        }

        // Convert mundane items (no Appraise needed — value is deterministic)
        for (int i = 0; i < treasure.MundaneItems.Count; i++)
        {
            MundaneItemResult mundane = treasure.MundaneItems[i];
            ItemData item = ConvertMundaneItem(mundane);
            if (item != null)
                result.Items.Add(item);
        }

        // Convert magic items (no Appraise needed — value is deterministic for known items)
        for (int i = 0; i < treasure.MagicItems.Count; i++)
        {
            MagicItemResult magic = treasure.MagicItems[i];
            ItemData item = ConvertMagicItem(magic);
            if (item != null)
                result.Items.Add(item);
        }

        Debug.Log($"[TreasureConvert] Converted {result.Items.Count} treasure items " +
                  $"(gems={treasure.Gems.Count} art={treasure.ArtObjects.Count} " +
                  $"mundane={treasure.MundaneItems.Count} magic={treasure.MagicItems.Count})");

        return result;
    }

    // ========================================================================
    // APPRAISE SKILL CHECK (D&D 3.5e PHB p.67)
    // ========================================================================

    /// <summary>
    /// Find the party member with the highest Appraise skill modifier.
    /// D&D 3.5e: "You can also use the check to determine the most valuable
    /// item visible in a treasure hoard." — the best appraiser examines all items.
    /// </summary>
    private static void FindBestAppraiser(List<CharacterController> partyMembers, out int bestMod, out string bestName)
    {
        bestMod = 0; // Untrained Appraise defaults to INT mod (0 for average)
        bestName = "Party (untrained)";

        if (partyMembers == null || partyMembers.Count == 0)
            return;

        for (int i = 0; i < partyMembers.Count; i++)
        {
            CharacterController pc = partyMembers[i];
            if (pc == null || pc.Stats == null)
                continue;

            int mod = pc.Stats.GetSkillBonus("Appraise");
            string charName = !string.IsNullOrWhiteSpace(pc.Stats.CharacterName)
                ? pc.Stats.CharacterName : $"PC_{i}";

            Debug.Log($"[Appraise] {charName}: Appraise modifier = {mod}");

            if (mod > bestMod || i == 0)
            {
                bestMod = mod;
                bestName = charName;
            }
        }
    }

    /// <summary>
    /// Perform an Appraise check for a gem or art object.
    /// D&D 3.5e rules (PHB p.67):
    ///   - Roll d20 + Appraise modifier vs DC
    ///   - Success: know exact value
    ///   - Fail by 4 or less: estimate is off by 10-40%
    ///   - Fail by 5 or more: estimate is off by 20-80%
    /// </summary>
    /// <returns>The appraised value in gp.</returns>
    private static int PerformAppraiseCheck(
        string itemName,
        int trueValueGp,
        int appraiseModifier,
        string appraiserName,
        List<string> log)
    {
        int dc = GetAppraiseDC(trueValueGp);
        int roll = Random.Range(1, 21); // d20
        int total = roll + appraiseModifier;
        int margin = total - dc;

        if (margin >= 0)
        {
            // Success: exact value known
            string logEntry = $"[Appraise] {appraiserName} appraised '{itemName}': " +
                              $"d20({roll}) + {appraiseModifier} = {total} vs DC {dc} — SUCCESS. " +
                              $"True value: {trueValueGp} gp";
            log.Add(logEntry);
            Debug.Log(logEntry);
            return trueValueGp;
        }
        else if (margin >= -4)
        {
            // Failed by 4 or less: estimate off by ±10-40%
            float errorPct = Random.Range(0.10f, 0.40f);
            bool overEstimate = Random.value > 0.5f;
            float multiplier = overEstimate ? (1f + errorPct) : (1f - errorPct);
            int appraisedValue = Mathf.Max(1, Mathf.RoundToInt(trueValueGp * multiplier));

            string logEntry = $"[Appraise] {appraiserName} appraised '{itemName}': " +
                              $"d20({roll}) + {appraiseModifier} = {total} vs DC {dc} — FAILED by {-margin}. " +
                              $"True: {trueValueGp} gp, Appraised: {appraisedValue} gp " +
                              $"({(overEstimate ? "+" : "-")}{errorPct * 100:F0}%)";
            log.Add(logEntry);
            Debug.Log(logEntry);
            return appraisedValue;
        }
        else
        {
            // Failed by 5+: estimate off by ±20-80%
            float errorPct = Random.Range(0.20f, 0.80f);
            bool overEstimate = Random.value > 0.5f;
            float multiplier = overEstimate ? (1f + errorPct) : (1f - errorPct);
            int appraisedValue = Mathf.Max(1, Mathf.RoundToInt(trueValueGp * multiplier));

            string logEntry = $"[Appraise] {appraiserName} appraised '{itemName}': " +
                              $"d20({roll}) + {appraiseModifier} = {total} vs DC {dc} — BADLY FAILED by {-margin}. " +
                              $"True: {trueValueGp} gp, Appraised: {appraisedValue} gp " +
                              $"({(overEstimate ? "+" : "-")}{errorPct * 100:F0}%)";
            log.Add(logEntry);
            Debug.Log(logEntry);
            return appraisedValue;
        }
    }

    // ========================================================================
    // ITEM CONVERSION METHODS
    // ========================================================================

    private static int _treasureItemCounter;

    /// <summary>Generate a unique ID for a treasure item to prevent collisions.</summary>
    private static string GenerateTreasureItemId(string prefix)
    {
        _treasureItemCounter++;
        return $"treasure_{prefix}_{_treasureItemCounter}_{Random.Range(1000, 9999)}";
    }

    private static ItemData ConvertGem(GemResult gem, int appraiseModifier, string appraiserName, List<string> log)
    {
        if (gem == null) return null;

        int appraisedValue = PerformAppraiseCheck(gem.Name, gem.Value, appraiseModifier, appraiserName, log);

        var item = new ItemData
        {
            Id = GenerateTreasureItemId("gem"),
            Name = gem.Name,
            Description = $"A precious gemstone{(gem.Tier != null ? $" ({gem.Tier} tier)" : "")}.\nAppraised value: {appraisedValue:N0} gp",
            Type = ItemType.Misc,
            Slot = EquipSlot.None,
            BasePriceGp = appraisedValue,
            IconChar = "💎",
            IsTreasureItem = true,
            IsAppraised = true,
            TrueValueGp = gem.Value,
            AppraisedValueGp = appraisedValue
        };

        return item;
    }

    private static ItemData ConvertArtObject(ArtResult art, int appraiseModifier, string appraiserName, List<string> log)
    {
        if (art == null) return null;

        int appraisedValue = PerformAppraiseCheck(art.Name, art.Value, appraiseModifier, appraiserName, log);

        var item = new ItemData
        {
            Id = GenerateTreasureItemId("art"),
            Name = art.Name,
            Description = $"An art object{(art.Tier != null ? $" ({art.Tier} tier)" : "")}.\nAppraised value: {appraisedValue:N0} gp",
            Type = ItemType.Misc,
            Slot = EquipSlot.None,
            BasePriceGp = appraisedValue,
            IconChar = "🖼",
            IsTreasureItem = true,
            IsAppraised = true,
            TrueValueGp = art.Value,
            AppraisedValueGp = appraisedValue
        };

        return item;
    }

    private static ItemData ConvertMundaneItem(MundaneItemResult mundane)
    {
        if (mundane == null) return null;

        // Determine appropriate type from name heuristics
        ItemType type = ItemType.Misc;
        string iconChar = "🔧";
        EquipSlot slot = EquipSlot.None;

        string nameLower = mundane.Name != null ? mundane.Name.ToLowerInvariant() : "";
        if (nameLower.Contains("sword") || nameLower.Contains("axe") || nameLower.Contains("mace") ||
            nameLower.Contains("hammer") || nameLower.Contains("dagger") || nameLower.Contains("spear") ||
            nameLower.Contains("bow") || nameLower.Contains("crossbow") || nameLower.Contains("flail") ||
            nameLower.Contains("halberd") || nameLower.Contains("lance") || nameLower.Contains("rapier") ||
            nameLower.Contains("scimitar") || nameLower.Contains("trident") || nameLower.Contains("weapon"))
        {
            type = ItemType.Weapon;
            iconChar = "⚔";
            slot = EquipSlot.EitherHand;
        }
        else if (nameLower.Contains("armor") || nameLower.Contains("mail") || nameLower.Contains("plate") ||
                 nameLower.Contains("breastplate") || nameLower.Contains("barding"))
        {
            type = ItemType.Armor;
            iconChar = "🛡";
            slot = EquipSlot.ArmorRobe;
        }
        else if (nameLower.Contains("shield") || nameLower.Contains("buckler"))
        {
            type = ItemType.Shield;
            iconChar = "🛡";
            slot = EquipSlot.LeftHand;
        }
        else if (nameLower.Contains("potion") || nameLower.Contains("elixir") || nameLower.Contains("oil") ||
                 nameLower.Contains("antitoxin") || nameLower.Contains("acid") || nameLower.Contains("alchemist"))
        {
            type = ItemType.Consumable;
            iconChar = "🧪";
        }

        bool isMasterwork = nameLower.Contains("masterwork") || nameLower.Contains("mw ");

        var item = new ItemData
        {
            Id = GenerateTreasureItemId("mundane"),
            Name = mundane.Name,
            Description = $"A mundane item found in the treasure hoard.\nValue: {mundane.Value:N0} gp",
            Type = type,
            Slot = slot,
            BasePriceGp = mundane.Value,
            IconChar = iconChar,
            IsMasterwork = isMasterwork,
            IsTreasureItem = true,
            IsAppraised = false,
            TrueValueGp = mundane.Value,
            AppraisedValueGp = mundane.Value // Mundane items have known market prices
        };

        return item;
    }

    private static ItemData ConvertMagicItem(MagicItemResult magic)
    {
        if (magic == null) return null;

        // Determine type and icon from magic item type field
        ItemType type = ItemType.Misc;
        string iconChar = "✨";
        EquipSlot slot = EquipSlot.None;

        string magicType = magic.Type != null ? magic.Type.ToLowerInvariant() : "";
        switch (magicType)
        {
            case "weapon":
                type = ItemType.Weapon;
                iconChar = "⚔";
                slot = EquipSlot.EitherHand;
                break;
            case "armor":
                type = ItemType.Armor;
                iconChar = "🛡";
                slot = EquipSlot.ArmorRobe;
                break;
            case "shield":
                type = ItemType.Shield;
                iconChar = "🛡";
                slot = EquipSlot.LeftHand;
                break;
            case "potion":
                type = ItemType.Consumable;
                iconChar = "🧪";
                break;
            case "scroll":
                type = ItemType.Consumable;
                iconChar = "📜";
                break;
            case "wand":
                type = ItemType.Wondrous;
                iconChar = "🪄";
                slot = EquipSlot.Slotless;
                break;
            case "rod":
                type = ItemType.Wondrous;
                iconChar = "🔮";
                slot = EquipSlot.Slotless;
                break;
            case "staff":
                type = ItemType.Wondrous;
                iconChar = "🪄";
                slot = EquipSlot.Slotless;
                break;
            case "ring":
                type = ItemType.Ring;
                iconChar = "💍";
                slot = EquipSlot.EitherRing;
                break;
            case "wondrous":
                type = ItemType.Wondrous;
                iconChar = "✨";
                slot = EquipSlot.Slotless;
                break;
        }

        string enhancementStr = magic.Enhancement > 0 ? $"+{magic.Enhancement} " : "";
        string abilitiesStr = "";
        if (magic.Abilities != null && magic.Abilities.Count > 0)
            abilitiesStr = "\nAbilities: " + string.Join(", ", magic.Abilities);

        var item = new ItemData
        {
            Id = GenerateTreasureItemId("magic"),
            Name = magic.Name,
            Description = $"{enhancementStr}Magic item{(magic.Type != null ? $" ({magic.Type})" : "")}.\n" +
                          $"Value: {magic.Price:N0} gp{abilitiesStr}",
            Type = type,
            Slot = slot,
            BasePriceGp = magic.Price,
            IconChar = iconChar,
            EnhancementBonus = magic.Enhancement,
            enhancementBonus = magic.Enhancement,
            CountsAsMagicForBypass = true,
            IsTreasureItem = true,
            IsAppraised = false,
            TrueValueGp = magic.Price,
            AppraisedValueGp = magic.Price // Magic items have known market prices via Identify/Detect Magic
        };

        return item;
    }
}
