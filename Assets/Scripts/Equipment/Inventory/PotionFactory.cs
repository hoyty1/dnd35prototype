using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Generates potion ItemData entries for eligible spells in the SpellDatabase.
/// Follows D&D 3.5e potion creation rules (PHB p.89 Brew Potion, DMG p.229 and p.286):
///   - Only spells of level 0-3 can be made into potions
///   - The spell must target one or more creatures (no personal range, no Area or Effect entry, not Shield Other);
///     see IsEligibleForPotion. Oils (object targets, DMG p.229) are not modelled (ITM-080).
///   - The store and the brew list offer only potions the drink path can resolve (IsOfferedAsPotion)
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

            if (!IsOfferedAsPotion(spell))
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
    /// Spells of 3rd level or lower whose PHB entry has no creature Target line although the spell data gives them a
    /// single target, so they cannot be brewed (PHB p.89: a potion holds a spell that targets one or more creatures).
    /// Object targets (their DMG p.229 form is an oil, which is not modelled; ITM-080): Align Weapon (PHB p.197),
    /// Continual Flame (p.213), Grease (p.237), Heat Metal (p.239), Keen Edge (p.246), Magic Weapon, Greater Magic
    /// Weapon and Magic Vestment (p.251), Shrink Item (p.279). Effect entries: Flare (p.232), Produce Flame (p.265),
    /// Spiritual Weapon (p.283). Area entries: Bane (p.203), Invisibility Sphere (p.245), Lullaby and the Magic Circles
    /// (p.249), Silence (p.279), Sound Burst (p.281).
    /// </summary>
    private static readonly HashSet<string> NoCreatureTargetSpellIds = new HashSet<string>(StringComparer.Ordinal)
    {
        SpellNames.ALIGN_WEAPON, SpellNames.CONTINUAL_FLAME, SpellNames.GREASE, SpellNames.HEAT_METAL,
        SpellNames.KEEN_EDGE, SpellNames.MAGIC_WEAPON, SpellNames.GREATER_MAGIC_WEAPON, SpellNames.MAGIC_VESTMENT,
        SpellNames.SHRINK_ITEM,
        SpellNames.FLARE, SpellNames.PRODUCE_FLAME, SpellNames.SPIRITUAL_WEAPON,
        SpellNames.BANE, SpellNames.INVISIBILITY_SPHERE, SpellNames.LULLABY, SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS,
        SpellNames.MAGIC_CIRCLE_AGAINST_EVIL, SpellNames.MAGIC_CIRCLE_AGAINST_GOOD, SpellNames.MAGIC_CIRCLE_AGAINST_LAW,
        SpellNames.SILENCE, SpellNames.SOUND_BURST,
    };

    /// <summary>
    /// Whether the rules let <paramref name="spell"/> be a potion (its level is checked separately).
    /// PHB p.89 (Brew Potion): a spell that targets one or more creatures. DMG p.286: never a personal-range spell
    /// (Shield), and never a spell whose caster and target must differ (Shield Other), because the drinker is both.
    /// The spell data has no Target/Effect/Area entry, so this rejects Area spells, ranged-touch spells (rays and
    /// missiles have an Effect entry) and the listed NoCreatureTargetSpellIds. Every spell here casts in 1 full-round
    /// action or less, so the DMG p.229 limit (casting time under 1 minute) never bites.
    /// Remaining gap (ITM-080): Calm Animals and Halt Undead have a Targets line but are modelled as areas.
    /// </summary>
    public static bool IsEligibleForPotion(SpellData spell)
    {
        if (spell == null) return false;

        // DMG p.286: personal range never exists as a potion.
        SpellRangeCategory range = spell.GetEffectiveRangeCategory();
        if (range == SpellRangeCategory.Personal)
            return false;

        // Self-only spells can't be potions (they have no target other than caster)
        if (spell.TargetType == SpellTargetType.Self && range != SpellRangeCategory.Touch)
            return false;

        // PHB p.89: an Area spell has no creature Target line (Bless, Sleep, Darkness, Daylight).
        if (spell.TargetType == SpellTargetType.Area)
            return false;

        // Rays and other ranged-touch missiles are Effect entries (Ray of Enfeeblement, Searing Light, Acid Arrow).
        if (spell.IsRangedTouch)
            return false;

        // DMG p.286: the imbiber is both caster and target, so Shield Other cannot be a potion.
        if (string.Equals(spell.SpellId, SpellNames.SHIELD_OTHER, StringComparison.Ordinal))
            return false;

        if (NoCreatureTargetSpellIds.Contains(spell.SpellId))
            return false;

        return true;
    }

    /// <summary>
    /// Whether the drink path (GameManager.TryApplySpellConsumableEffect, or HealHP for a healing potion) can resolve
    /// a potion of <paramref name="spell"/>: a spell that restores hit points, or a Buff, Debuff, Control or Illusion
    /// spell (applied to the drinker through StatusEffectManager). Potions of Damage, Dispel, Utility, Escape, Summon,
    /// Wall or Divination spells, and of healing spells that restore no hit points (Remove Disease), would fail or do
    /// nothing when drunk, so they are not offered yet (ITM-080).
    /// </summary>
    public static bool CanResolveAsPotion(SpellData spell)
    {
        if (spell == null) return false;
        switch (spell.EffectType)
        {
            case SpellEffectType.Healing:
                return RestoresHitPoints(spell);
            case SpellEffectType.Buff:
            case SpellEffectType.Debuff:
            case SpellEffectType.Control:
            case SpellEffectType.Illusion:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether <paramref name="spell"/> is a healing spell that restores hit points (the cures, Cure Minor Wounds):
    /// it has healing dice or a flat amount, is no placeholder, and has no handler that does something else instead.
    /// Lesser Restoration (a placeholder whose 1d4 is ability points, PHB p.272) and Remove Disease or Remove
    /// Paralysis (no hit points) do not, so a potion of one is never a HealHP potion.
    /// </summary>
    public static bool RestoresHitPoints(SpellData spell)
    {
        if (spell == null || spell.EffectType != SpellEffectType.Healing || spell.IsPlaceholder || spell.HealingResolvedByHandler)
            return false;
        SpellDice dice = SpellDiceRules.Healing(spell, 1);
        return dice.HasDice || dice.Bonus > 0;
    }

    /// <summary>
    /// Whether the store sells, and the brew list offers, a potion of <paramref name="spell"/>: not a placeholder,
    /// allowed by the rules (IsEligibleForPotion) and resolvable when drunk (CanResolveAsPotion). Each caller checks
    /// the spell level limit (3rd).
    /// </summary>
    public static bool IsOfferedAsPotion(SpellData spell)
    {
        return spell != null && !spell.IsPlaceholder && IsEligibleForPotion(spell) && CanResolveAsPotion(spell);
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

        ItemDatabase.RegisterPotionItem(BuildPotion(spell, id, spellLevel, casterLevel, priceGp));
        _registeredPotionIds.Add(id);
    }

    /// <summary>
    /// Builds a potion of <paramref name="spell"/> at <paramref name="casterLevel"/>: the one item shape shared by the
    /// store potions this factory registers and the potions a Brew Potion crafter makes (CraftingExecutor, ITM-002), so
    /// both are drunk through the same consumable path for any creature. DMG p.229: whoever drinks the potion is the
    /// spell's target and the potion works at its own caster level, so a healing spell becomes HealHP dice at that level
    /// (SpellDiceRules, SPL-005) and any other spell a SpellEffect resolved at that level. Potions stack (up to 20 per
    /// slot) with others of the same <paramref name="id"/>. Never registers anything itself.
    /// </summary>
    public static ItemData BuildPotion(SpellData spell, string id, int spellLevel, int casterLevel, int priceGp)
    {
        if (spell == null)
            return null;

        casterLevel = Mathf.Max(1, casterLevel);
        ItemData potionItem = new ItemData
        {
            Id = id,
            Name = $"Potion of {spell.Name}",
            Description = BuildPotionDescription(spell, spellLevel, casterLevel, priceGp),
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            ConsumableEffect = ConsumableEffectType.SpellEffect,
            ConsumableSpellName = spell.SpellId,   // Canonical spell ID (as scrolls and wands), not the display name
            ConsumableMinimumCasterLevel = casterLevel,
            ConsumableModifier = 0,
            BasePriceGp = priceGp,
            WeightLbs = 0.1f,
            IconChar = "\u2697", // alembic icon for potions
            IconColor = GetPotionColor(spell),
            IsPotion = true,
            PotionSpellLevel = spellLevel,
            IsStackable = true,
            MaxStackSize = 20,
            StackCount = 1
        };

        // A spell that restores hit points becomes HealHP; any other spell (a Healing-typed one that restores none,
        // such as Remove Disease, included) stays a SpellEffect resolved on the drinker.
        if (RestoresHitPoints(spell))
        {
            // The potion's dice at its own caster level (DMG p.229; SpellDiceRules, SPL-005): CLW at CL 1 is 1d8+1.
            SpellDice dice = SpellDiceRules.Healing(spell, casterLevel);
            potionItem.ConsumableEffect = ConsumableEffectType.HealHP;
            if (dice.HasDice)
            {
                potionItem.HealDiceCount = dice.Count;
                potionItem.HealDiceSides = dice.Sides;
                potionItem.HealBonus = dice.Bonus;
            }
            else
            {
                // No dice (Cure Minor Wounds): a flat amount, 1 point (PHB p.216), not 1d1 + 1.
                potionItem.HealAmount = Mathf.Max(0, dice.Bonus);
            }
        }

        return potionItem;
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
