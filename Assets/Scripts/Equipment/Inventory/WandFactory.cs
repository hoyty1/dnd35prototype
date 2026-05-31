using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates wand ItemData entries for eligible spells in the SpellDatabase.
/// Follows D&D 3.5e DMG wand creation rules:
///   - Only spells of level 0-4 can be made into wands
///   - No target/range restrictions (unlike potions — wands can hold any qualifying spell)
///   - Price = SpellLevel × CasterLevel × 750 gp (0-level uses 0.5 → 375 gp at CL 1)
///   - Minimum CL = max(1, 2 × SpellLevel - 1) — same formula as scrolls/potions
///   - Each wand starts with 50 charges, 1 charge per use, non-rechargeable
///   - Wands are NOT stackable (each has unique charge state)
///   - Spell trigger activation: need spell on class list OR Use Magic Device (DC 20)
///   - Standard action to activate, does NOT provoke attacks of opportunity
///   - Depleted wand (0 charges) = useless nonmagical stick
/// </summary>
public static class WandFactory
{
    /// <summary>Maximum spell level that can be made into a wand (D&D 3.5e DMG).</summary>
    public const int MaxWandSpellLevel = 4;

    /// <summary>Standard charge count for a new wand (D&D 3.5e DMG).</summary>
    public const int DefaultWandCharges = 50;

    /// <summary>All wand IDs registered by this factory (populated after RegisterAllWands).</summary>
    private static readonly List<string> _registeredWandIds = new List<string>();
    public static IReadOnlyList<string> RegisteredWandIds => _registeredWandIds;

    private static bool _registered = false;

    // Classes used for determining wand availability and pricing
    private static readonly string[] AllCasterClasses = { "Wizard", "Sorcerer", "Bard", "Cleric", "Druid", "Paladin", "Ranger" };

    // ── Public API ──

    /// <summary>
    /// Registers wand items in ItemDatabase for every eligible non-placeholder spell.
    /// Call this AFTER SpellDatabase.Init() and during ItemDatabase.Init().
    /// </summary>
    public static void RegisterAllWands()
    {
        if (_registered) return;
        _registered = true;

        SpellDatabase.Init();
        List<SpellData> allSpells = SpellDatabase.GetAllSpells();

        int wandCount = 0;
        int skippedCount = 0;

        foreach (SpellData spell in allSpells)
        {
            if (spell == null || string.IsNullOrWhiteSpace(spell.SpellId))
                continue;

            if (spell.IsPlaceholder)
            {
                skippedCount++;
                continue;
            }

            int spellLevel = GetBestWandSpellLevel(spell);
            if (spellLevel < 0 || spellLevel > MaxWandSpellLevel)
            {
                skippedCount++;
                continue;
            }

            int minCasterLevel = GetMinimumCasterLevel(spellLevel);
            int priceGp = CalculateWandPrice(spellLevel, minCasterLevel);

            RegisterWand(spell, spellLevel, minCasterLevel, priceGp);
            wandCount++;
        }

        Debug.Log($"[WandFactory] Registered {wandCount} wands ({skippedCount} spells skipped).");
    }

    /// <summary>
    /// Adds all registered wands to the store, organized by category "Wand (Lvl X)".
    /// </summary>
    public static void AddWandsToStore(StoreInventory store)
    {
        if (store == null) return;

        int added = 0;
        foreach (string wandId in _registeredWandIds)
        {
            ItemData template = ItemDatabase.Get(wandId);
            if (template == null) continue;

            string category = $"Wand (Lvl {template.WandSpellLevel})";
            store.AddWandItem(wandId, category, template.BasePriceGp);
            added++;
        }

        Debug.Log($"[WandFactory] Added {added} wands to store.");
    }

    // ── Pricing ──

    /// <summary>
    /// D&D 3.5e DMG: Minimum CL for wands = max(1, 2 × spellLevel - 1).
    /// Same formula as scrolls and potions.
    /// </summary>
    public static int GetMinimumCasterLevel(int spellLevel)
    {
        if (spellLevel <= 1) return 1;
        return 2 * spellLevel - 1;
    }

    /// <summary>
    /// D&D 3.5e DMG: Wand Price = SL × CL × 750 gp. For 0-level, SL is treated as 0.5 → 375 gp at CL 1.
    /// </summary>
    public static int CalculateWandPrice(int spellLevel, int casterLevel)
    {
        if (spellLevel == 0)
            return Mathf.CeilToInt(0.5f * casterLevel * 750f); // 375 gp for CL 1

        return spellLevel * casterLevel * 750;
    }

    /// <summary>
    /// D&D 3.5e DMG: Wand save DC = 10 + spell level + minimum ability modifier for that spell level.
    /// The minimum ability score to cast a spell of level N is 10 + N, giving a modifier of N/2 (rounded down).
    /// So DC = 10 + SL + (10+SL - 10)/2 = 10 + SL + SL/2. But by convention: DC = 10 + spell level.
    /// Actually the DMG states: "The DC for a save is 10 + spell level + the minimum ability modifier
    /// needed to cast that spell." Min ability = 10+SL, modifier = floor((10+SL-10)/2) = floor(SL/2).
    /// So DC = 10 + SL + floor(SL/2).
    /// </summary>
    public static int CalculateWandSaveDC(int spellLevel)
    {
        int minAbilityMod = spellLevel / 2; // floor(SL/2) — min ability is 10+SL, mod = (10+SL-10)/2
        return 10 + spellLevel + minAbilityMod;
    }

    /// <summary>
    /// Gets the lowest spell level across all caster classes (capped at MaxWandSpellLevel).
    /// Returns -1 if no class has this spell at level 0-4.
    /// </summary>
    public static int GetBestWandSpellLevel(SpellData spell)
    {
        if (spell == null) return -1;

        int best = int.MaxValue;

        foreach (string cls in AllCasterClasses)
        {
            int level = spell.GetSpellLevelFor(cls);
            if (level >= 0 && level <= MaxWandSpellLevel && level < best)
                best = level;
        }

        // Also check domain availability
        if (best == int.MaxValue && spell.AvailableFor != null)
        {
            foreach (var a in spell.AvailableFor)
            {
                if (a != null && a.Level >= 0 && a.Level <= MaxWandSpellLevel && a.Level < best)
                    best = a.Level;
            }
        }

        if (best == int.MaxValue && spell.SpellLevel >= 0 && spell.SpellLevel <= MaxWandSpellLevel)
            best = spell.SpellLevel;

        return best <= MaxWandSpellLevel ? best : -1;
    }

    // ── Internal registration ──

    private static void RegisterWand(SpellData spell, int spellLevel, int casterLevel, int priceGp)
    {
        string id = GenerateWandId(spell.SpellId);

        // Skip if this wand ID is already registered
        if (ItemDatabase.HasItem(id))
            return;

        string name = $"Wand of {spell.Name}";
        string description = BuildWandDescription(spell, spellLevel, casterLevel, priceGp);

        // Pick icon color based on spell effect type
        Color iconColor = GetWandColor(spell);

        // Determine if the spell is arcane
        bool isArcane = IsArcaneSpell(spell);

        // Create unified WandData (single source of truth)
        WandData wandData = WandData.Create(
            spell, casterLevel, isArcane, priceGp);

        ItemData wandItem = new ItemData
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            ConsumableEffect = ConsumableEffectType.SpellEffect,
            ConsumableSpellName = spell.SpellId,
            ConsumableMinimumCasterLevel = casterLevel,
            ConsumableModifier = 0,
            BasePriceGp = priceGp,
            WeightLbs = 0.25f, // Wands weigh negligible amount, ~1/4 lb typical
            IconChar = "\u2742", // ✿ — using a distinctive icon for wands (floral/star)
            IconColor = iconColor,
            IsWand = true,
            CurrentCharges = DefaultWandCharges,
            MaxCharges = DefaultWandCharges,
            WandSpellId = spell.SpellId,
            WandCasterLevel = casterLevel,
            WandSpellLevel = spellLevel,
            Wand = wandData,
            IsStackable = false, // Wands are NOT stackable — each has unique charge count
            MaxStackSize = 1,
            StackCount = 1
        };

        // Copy healing data from spell if applicable
        if (spell.EffectType == SpellEffectType.Healing)
        {
            wandItem.ConsumableEffect = ConsumableEffectType.HealHP;
            wandItem.HealDiceCount = Mathf.Max(1, spell.HealCount);
            wandItem.HealDiceSides = Mathf.Max(1, spell.HealDice);
            wandItem.HealBonus = spell.BonusHealing;
        }

        ItemDatabase.RegisterWandItem(wandItem);
        _registeredWandIds.Add(id);
    }

    /// <summary>Generate a unique wand item ID from spell ID.</summary>
    public static string GenerateWandId(string spellId)
    {
        string normalizedSpell = (spellId ?? "unknown").Trim().ToLowerInvariant().Replace(" ", "_");
        return $"wand_{normalizedSpell}";
    }

    private static string BuildWandDescription(SpellData spell, int spellLevel, int casterLevel, int priceGp)
    {
        string schoolText = !string.IsNullOrWhiteSpace(spell.School) ? $" [{spell.School}]" : "";
        string classList = BuildClassListString(spell);
        int saveDC = CalculateWandSaveDC(spellLevel);

        return $"A slender wooden wand containing {spell.Name}{schoolText}.\n" +
               $"Spell Level: {spellLevel} | Caster Level: {casterLevel} | Price: {priceGp:N0} gp\n" +
               $"Charges: {DefaultWandCharges}/{DefaultWandCharges} | Save DC: {saveDC}\n" +
               $"Classes: {classList}\n" +
               $"Spell trigger activation — standard action, does NOT provoke AoO.\n" +
               $"Requires spell on class list or Use Magic Device (DC 20).\n" +
               $"Non-rechargeable. Depleted wand becomes a useless stick.";
    }

    private static string BuildClassListString(SpellData spell)
    {
        List<string> classes = new List<string>();

        foreach (string cls in AllCasterClasses)
        {
            int level = spell.GetSpellLevelFor(cls);
            if (level >= 0 && level <= MaxWandSpellLevel)
                classes.Add($"{cls} {level}");
        }

        return classes.Count > 0 ? string.Join(", ", classes) : "Various";
    }

    /// <summary>Determines if a spell is arcane (Wizard/Sorcerer/Bard) or divine.</summary>
    private static bool IsArcaneSpell(SpellData spell)
    {
        if (spell == null || spell.AvailableFor == null) return true; // Default to arcane
        string[] arcaneCasters = { "Wizard", "Sorcerer", "Bard" };
        foreach (var avail in spell.AvailableFor)
        {
            if (avail == null) continue;
            foreach (string cls in arcaneCasters)
            {
                if (avail.MatchesClass(cls)) return true;
            }
        }
        return false;
    }

    private static Color GetWandColor(SpellData spell)
    {
        switch (spell.EffectType)
        {
            case SpellEffectType.Healing:
                return new Color(1f, 0.4f, 0.4f);    // Red-pink for healing
            case SpellEffectType.Buff:
                return new Color(0.5f, 0.85f, 1f);   // Light blue for buffs
            case SpellEffectType.Damage:
                return new Color(1f, 0.5f, 0.15f);   // Bright orange for damage
            case SpellEffectType.Debuff:
                return new Color(0.7f, 0.35f, 0.9f);  // Purple for debuffs
            default:
                return new Color(0.6f, 0.9f, 0.6f);   // Soft green for misc
        }
    }
}
