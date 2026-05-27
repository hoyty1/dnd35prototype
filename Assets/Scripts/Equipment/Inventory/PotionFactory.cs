using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates potion ItemData entries for eligible spells in the SpellDatabase.
/// Follows D&D 3.5e DMG potion creation rules:
///   - Only spells of level 0-3 can be made into potions
///   - Spell must target one or more creatures or objects (no personal range)
///   - Spell cannot be an area-only effect
///   - Price = SpellLevel × CasterLevel × 50 gp (0-level uses 0.5 → 25 gp at CL 1)
///   - Minimum CL = max(1, 2 × SpellLevel - 1)
///   - Anyone can drink a potion (no class restrictions, no ability score requirements)
///   - Drinking a potion is a standard action that provokes AoO
///   - Administering a potion to another is a full-round action
/// </summary>
public static class PotionFactory
{
    /// <summary>Maximum spell level that can be made into a potion (D&D 3.5e DMG).</summary>
    public const int MaxPotionSpellLevel = 3;

    /// <summary>All potion IDs registered by this factory (populated after RegisterAllPotions).</summary>
    private static readonly List<string> _registeredPotionIds = new List<string>();
    public static IReadOnlyList<string> RegisteredPotionIds => _registeredPotionIds;

    private static bool _registered = false;

    // Classes used for determining potion availability and pricing
    private static readonly string[] AllCasterClasses = { "Wizard", "Sorcerer", "Bard", "Cleric", "Druid", "Paladin", "Ranger" };

    // ── Public API ──

    /// <summary>
    /// Registers potion items in ItemDatabase for every eligible non-placeholder spell.
    /// Call this AFTER SpellDatabase.Init() and during ItemDatabase.Init().
    /// </summary>
    public static void RegisterAllPotions()
    {
        if (_registered) return;
        _registered = true;

        SpellDatabase.Init();
        List<SpellData> allSpells = SpellDatabase.GetAllSpells();

        int potionCount = 0;
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

            if (!IsEligibleForPotion(spell))
            {
                skippedCount++;
                continue;
            }

            int spellLevel = GetBestPotionSpellLevel(spell);
            if (spellLevel < 0 || spellLevel > MaxPotionSpellLevel)
            {
                skippedCount++;
                continue;
            }

            int minCasterLevel = GetMinimumCasterLevel(spellLevel);
            int priceGp = CalculatePotionPrice(spellLevel, minCasterLevel);

            RegisterPotion(spell, spellLevel, minCasterLevel, priceGp);
            potionCount++;
        }

        Debug.Log($"[PotionFactory] Registered {potionCount} potions ({skippedCount} spells skipped).");
    }

    /// <summary>
    /// Adds all registered potions to the store, organized by category "Potion (Lvl X)".
    /// </summary>
    public static void AddPotionsToStore(StoreInventory store)
    {
        if (store == null) return;

        int added = 0;
        foreach (string potionId in _registeredPotionIds)
        {
            ItemData template = ItemDatabase.Get(potionId);
            if (template == null) continue;

            string category = $"Potion (Lvl {template.PotionSpellLevel})";
            store.AddPotionItem(potionId, category, template.BasePriceGp);
            added++;
        }

        Debug.Log($"[PotionFactory] Added {added} potions to store.");
    }

    // ── Eligibility ──

    /// <summary>
    /// Determines if a spell is eligible for potion creation per D&D 3.5e DMG rules:
    /// - Spell level 0-3 (checked separately via GetBestPotionSpellLevel)
    /// - Must NOT be Personal range only
    /// - Must target one or more creatures or objects (not area-only)
    /// - Standard action or less to cast (casting time < 1 minute)
    /// </summary>
    public static bool IsEligibleForPotion(SpellData spell)
    {
        if (spell == null) return false;

        // Must NOT be personal-range-only
        // Personal range = Self target type with no other targeting options
        SpellRangeCategory range = spell.GetEffectiveRangeCategory();
        if (range == SpellRangeCategory.Personal)
            return false;

        // Self-only spells can't be potions (they have no target other than caster)
        if (spell.TargetType == SpellTargetType.Self && range != SpellRangeCategory.Touch)
            return false;

        // Area-only spells cannot be potions (potions target the drinker only)
        // However, spells that have BOTH single-target and area modes are allowed
        // (e.g., Bless has AoE but targets allies — allowed as potion)
        if (spell.TargetType == SpellTargetType.Area && spell.AoEShapeType != AoEShape.None)
        {
            // Pure area spells (like Fireball) can't be potions
            // But buff-type AoE spells that target creatures (Bless, Prayer) can be
            if (spell.EffectType == SpellEffectType.Damage)
                return false;
        }

        // Full-round action spells can still be potions (the potion itself is standard action to drink)
        // But spells with casting time > 1 minute cannot (we don't track minute-long casting times yet)

        return true;
    }

    // ── Pricing ──

    /// <summary>
    /// D&D 3.5e DMG: Minimum CL for potions = max(1, 2 × spellLevel - 1).
    /// Same formula as scrolls.
    /// </summary>
    public static int GetMinimumCasterLevel(int spellLevel)
    {
        if (spellLevel <= 1) return 1;
        return 2 * spellLevel - 1;
    }

    /// <summary>
    /// D&D 3.5e DMG: Potion Price = SL × CL × 50 gp. For 0-level, SL is treated as 0.5 → 25 gp at CL 1.
    /// </summary>
    public static int CalculatePotionPrice(int spellLevel, int casterLevel)
    {
        if (spellLevel == 0)
            return Mathf.CeilToInt(0.5f * casterLevel * 50f); // 25 gp for CL 1

        return spellLevel * casterLevel * 50;
    }

    /// <summary>
    /// Gets the lowest spell level across all caster classes (capped at MaxPotionSpellLevel).
    /// Returns -1 if no class has this spell at level 0-3.
    /// </summary>
    public static int GetBestPotionSpellLevel(SpellData spell)
    {
        if (spell == null) return -1;

        int best = int.MaxValue;

        foreach (string cls in AllCasterClasses)
        {
            int level = spell.GetSpellLevelFor(cls);
            if (level >= 0 && level <= MaxPotionSpellLevel && level < best)
                best = level;
        }

        // Also check domain availability
        if (best == int.MaxValue && spell.AvailableFor != null)
        {
            foreach (var a in spell.AvailableFor)
            {
                if (a != null && a.Level >= 0 && a.Level <= MaxPotionSpellLevel && a.Level < best)
                    best = a.Level;
            }
        }

        if (best == int.MaxValue && spell.SpellLevel >= 0 && spell.SpellLevel <= MaxPotionSpellLevel)
            best = spell.SpellLevel;

        return best <= MaxPotionSpellLevel ? best : -1;
    }

    // ── Internal registration ──

    private static void RegisterPotion(SpellData spell, int spellLevel, int casterLevel, int priceGp)
    {
        string id = GeneratePotionId(spell.SpellId);

        // Skip if this potion ID is already registered (e.g., manually registered potions)
        if (ItemDatabase.HasItem(id))
            return;

        string name = $"Potion of {spell.Name}";
        string description = BuildPotionDescription(spell, spellLevel, casterLevel, priceGp);

        // Pick icon color based on spell effect type
        Color iconColor = GetPotionColor(spell);

        ItemData potionItem = new ItemData
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            ConsumableEffect = ConsumableEffectType.SpellEffect,
            ConsumableSpellName = spell.Name,
            ConsumableMinimumCasterLevel = casterLevel,
            ConsumableModifier = 0,
            BasePriceGp = priceGp,
            WeightLbs = 0.1f,
            IconChar = "\u2697", // ⚗ alembic icon for potions
            IconColor = iconColor,
            IsPotion = true,
            PotionSpellLevel = spellLevel,
            IsStackable = true,
            MaxStackSize = 20,
            StackCount = 1
        };

        // Copy healing data from spell if applicable
        if (spell.EffectType == SpellEffectType.Healing)
        {
            potionItem.ConsumableEffect = ConsumableEffectType.HealHP;
            potionItem.HealDiceCount = Mathf.Max(1, spell.HealCount);
            potionItem.HealDiceSides = Mathf.Max(1, spell.HealDice);
            potionItem.HealBonus = spell.BonusHealing;
        }

        ItemDatabase.RegisterPotionItem(potionItem);
        _registeredPotionIds.Add(id);
    }

    /// <summary>Generate a unique potion item ID from spell ID.</summary>
    public static string GeneratePotionId(string spellId)
    {
        string normalizedSpell = (spellId ?? "unknown").Trim().ToLowerInvariant().Replace(" ", "_");
        return $"potion_{normalizedSpell}";
    }

    private static string BuildPotionDescription(SpellData spell, int spellLevel, int casterLevel, int priceGp)
    {
        string schoolText = !string.IsNullOrWhiteSpace(spell.School) ? $" [{spell.School}]" : "";
        string classList = BuildClassListString(spell);

        return $"Potion containing {spell.Name}{schoolText}.\n" +
               $"Spell Level: {spellLevel} | Caster Level: {casterLevel} | Price: {priceGp} gp\n" +
               $"Classes: {classList}\n" +
               $"Drinking a potion is a standard action that provokes AoO.\n" +
               $"No class restrictions — anyone can use a potion.\n" +
               $"Administering a potion to another creature is a full-round action.";
    }

    private static string BuildClassListString(SpellData spell)
    {
        List<string> classes = new List<string>();

        foreach (string cls in AllCasterClasses)
        {
            int level = spell.GetSpellLevelFor(cls);
            if (level >= 0 && level <= MaxPotionSpellLevel)
                classes.Add($"{cls} {level}");
        }

        return classes.Count > 0 ? string.Join(", ", classes) : "Various";
    }

    private static Color GetPotionColor(SpellData spell)
    {
        switch (spell.EffectType)
        {
            case SpellEffectType.Healing:
                return new Color(1f, 0.3f, 0.3f);    // Red for healing
            case SpellEffectType.Buff:
                return new Color(0.4f, 0.8f, 1f);    // Light blue for buffs
            case SpellEffectType.Damage:
                return new Color(1f, 0.6f, 0.2f);    // Orange for damage
            case SpellEffectType.Debuff:
                return new Color(0.6f, 0.3f, 0.8f);  // Purple for debuffs
            default:
                return new Color(0.5f, 1f, 0.5f);    // Green for misc
        }
    }
}
