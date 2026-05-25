using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════
//  Metamagic Rod Activation System (DMG pp. 224–228)
//
//  Handles the application of metamagic rods to spells during casting.
//  KEY RULE: Metamagic rods apply metamagic WITHOUT increasing spell slot
//  level — the rod absorbs the slot increase entirely.
//
//  Integration points:
//  - MetamagicData.ApplyFromRod() — marks metamagic as rod-sourced
//  - MetamagicSystem.PrepareMetamagicSpell() — handles rod modifiers
//  - SpellCaster.ApplyMetamagicToSpellData() — applies effects
//
//  Validation:
//  - Spell level ≤ rod's MaxSpellLevel (3/6/9 for Lesser/Normal/Greater)
//  - Rod has daily uses remaining (3/day standard)
//  - Rod is held by the caster (EitherHand slot)
// ════════════════════════════════════════════════════════════════════════

public static class MetamagicRodActivation
{
    // ── Validation ────────────────────────────────────────────

    /// <summary>
    /// Validate whether a metamagic rod can be applied to a given spell.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    public static string ValidateRodApplication(ItemData rod, SpellData spell)
    {
        if (rod == null)
            return "No rod provided.";

        if (!rod.IsRod || !rod.RodIsMetamagic)
            return $"{rod.Name} is not a metamagic rod.";

        if (spell == null)
            return "No spell provided.";

        // Check spell level vs rod maximum
        int baseSpellLevel = spell.SpellLevel;
        if (baseSpellLevel > rod.RodMaxSpellLevel)
        {
            string powerLabel = rod.RodPower == RodPowerLevel.Lesser ? "Lesser" :
                                rod.RodPower == RodPowerLevel.Normal ? "Normal" : "Greater";
            return $"Spell level {baseSpellLevel} exceeds {powerLabel} rod maximum of {rod.RodMaxSpellLevel}. " +
                   $"This rod can only affect spells up to level {rod.RodMaxSpellLevel}.";
        }

        // Check daily uses
        if (rod.RodUsesToday >= rod.RodUsesPerDay)
        {
            return $"{rod.Name} has been used {rod.RodUsesToday}/{rod.RodUsesPerDay} times today. " +
                   "Daily uses are reset at dawn.";
        }

        // Check rod of cancellation expended
        if (rod.RodIsExpended)
        {
            return $"{rod.Name} has been expended and is now nonmagical.";
        }

        // Check metamagic applicability to spell
        if (!MetamagicData.IsApplicable(rod.RodMetamagicType, spell))
        {
            string metamagicName = MetamagicData.GetDisplayName(rod.RodMetamagicType);
            return $"{metamagicName} cannot be applied to {spell.Name}. " +
                   "Check spell requirements for this metamagic type.";
        }

        return null; // Valid
    }

    /// <summary>
    /// Check if a rod can be used (has uses remaining and is not expended).
    /// Quick boolean check without detailed error message.
    /// </summary>
    public static bool CanUseRod(ItemData rod)
    {
        if (rod == null || !rod.IsRod) return false;
        if (rod.RodIsExpended) return false;

        if (rod.RodIsMetamagic)
            return rod.RodUsesToday < rod.RodUsesPerDay;

        return true;
    }

    // ── Application ───────────────────────────────────────────

    /// <summary>
    /// Apply a metamagic rod to a spell, creating a MetamagicModifier with AppliedByRod = true.
    /// Consumes one daily use of the rod.
    /// Returns the modifier on success, null on failure (check ValidateRodApplication for reason).
    /// </summary>
    public static MetamagicModifier ApplyRodToSpell(ItemData rod, SpellData spell)
    {
        string error = ValidateRodApplication(rod, spell);
        if (error != null)
        {
            Debug.LogWarning($"[MetamagicRodActivation] {error}");
            return null;
        }

        // Create modifier with rod flag — rod absorbs the slot increase!
        var modifier = new MetamagicModifier(rod.RodMetamagicType, spell.SpellLevel, appliedByRod: true);
        modifier.SlotIncrease = 0; // KEY: Rods don't increase spell slot!

        // Consume daily use
        rod.RodUsesToday++;

        string metamagicName = MetamagicData.GetDisplayName(rod.RodMetamagicType);
        Debug.Log($"[MetamagicRodActivation] Applied {rod.Name} to {spell.Name}. " +
                  $"Metamagic: {metamagicName} (no slot increase). " +
                  $"Uses: {rod.RodUsesToday}/{rod.RodUsesPerDay} today.");

        return modifier;
    }

    /// <summary>
    /// Apply multiple metamagic rods to a spell simultaneously.
    /// Each rod consumes one daily use. Returns list of valid modifiers.
    /// Rods that fail validation are skipped with warnings.
    /// </summary>
    public static List<MetamagicModifier> ApplyMultipleRods(List<ItemData> rods, SpellData spell)
    {
        var modifiers = new List<MetamagicModifier>();
        if (rods == null || spell == null) return modifiers;

        var usedMetamagicTypes = new HashSet<MetamagicFeatId>();

        foreach (var rod in rods)
        {
            if (rod == null) continue;

            // Prevent duplicate metamagic types (D&D 3.5e: can't stack same metamagic)
            if (usedMetamagicTypes.Contains(rod.RodMetamagicType))
            {
                Debug.LogWarning($"[MetamagicRodActivation] Duplicate metamagic type " +
                                 $"{rod.RodMetamagicType} — skipping {rod.Name}");
                continue;
            }

            var modifier = ApplyRodToSpell(rod, spell);
            if (modifier != null)
            {
                modifiers.Add(modifier);
                usedMetamagicTypes.Add(rod.RodMetamagicType);
            }
        }

        return modifiers;
    }

    // ── Rod of Absorption ─────────────────────────────────────

    /// <summary>
    /// Attempt to absorb an incoming spell with the Rod of Absorption.
    /// Returns true if the spell was absorbed successfully.
    /// </summary>
    public static bool TryAbsorbSpell(ItemData rod, SpellData incomingSpell)
    {
        if (rod == null || !rod.RodCanAbsorbSpells)
        {
            Debug.LogWarning("[MetamagicRodActivation] Item is not a Rod of Absorption.");
            return false;
        }

        if (rod.RodAbsorbedLevels >= rod.RodMaxAbsorbedLevels)
        {
            Debug.Log($"[MetamagicRodActivation] Rod of Absorption at max capacity " +
                      $"({rod.RodMaxAbsorbedLevels} levels). Cannot absorb more spells.");
            return false;
        }

        int spellLevel = incomingSpell != null ? incomingSpell.SpellLevel : 1;
        int newTotal = rod.RodAbsorbedLevels + spellLevel;

        // Cap at max
        if (newTotal > rod.RodMaxAbsorbedLevels)
        {
            Debug.Log($"[MetamagicRodActivation] Rod would exceed capacity. " +
                      $"Current: {rod.RodAbsorbedLevels}, Incoming: {spellLevel}, " +
                      $"Max: {rod.RodMaxAbsorbedLevels}. Absorbing partial.");
            rod.RodAbsorbedLevels = rod.RodMaxAbsorbedLevels;
        }
        else
        {
            rod.RodAbsorbedLevels = newTotal;
        }

        string spellName = incomingSpell?.Name ?? "Unknown Spell";
        Debug.Log($"[MetamagicRodActivation] Absorbed {spellName} (level {spellLevel}). " +
                  $"Rod now holds {rod.RodAbsorbedLevels}/{rod.RodMaxAbsorbedLevels} spell levels.");

        return true;
    }

    /// <summary>
    /// Spend absorbed spell levels from Rod of Absorption to cast a spell.
    /// Returns true if enough levels were available and consumed.
    /// </summary>
    public static bool SpendAbsorbedLevels(ItemData rod, int levelsNeeded)
    {
        if (rod == null || !rod.RodCanAbsorbSpells) return false;

        if (rod.RodAbsorbedLevels < levelsNeeded)
        {
            Debug.Log($"[MetamagicRodActivation] Not enough absorbed levels. " +
                      $"Have: {rod.RodAbsorbedLevels}, Need: {levelsNeeded}.");
            return false;
        }

        rod.RodAbsorbedLevels -= levelsNeeded;
        Debug.Log($"[MetamagicRodActivation] Spent {levelsNeeded} absorbed levels. " +
                  $"Remaining: {rod.RodAbsorbedLevels}/{rod.RodMaxAbsorbedLevels}.");
        return true;
    }

    // ── Rod of Cancellation ───────────────────────────────────

    /// <summary>
    /// Use the Rod of Cancellation to destroy a magic item's enchantment.
    /// The rod becomes nonmagical after use (single-use item).
    /// Returns true if the item was successfully cancelled.
    /// </summary>
    public static bool UseRodOfCancellation(ItemData rod, ItemData targetItem)
    {
        if (rod == null || !rod.RodCanCancelMagic)
        {
            Debug.LogWarning("[MetamagicRodActivation] Item is not a Rod of Cancellation.");
            return false;
        }

        if (rod.RodIsExpended)
        {
            Debug.Log("[MetamagicRodActivation] Rod of Cancellation already expended.");
            return false;
        }

        if (targetItem == null)
        {
            Debug.LogWarning("[MetamagicRodActivation] No target item to cancel.");
            return false;
        }

        // Cancel the item's magic
        Debug.Log($"[MetamagicRodActivation] Rod of Cancellation touches {targetItem.Name}! " +
                  "All magical properties are permanently destroyed.");

        // Strip magic from target (implementation depends on item type)
        targetItem.CountsAsMagicForBypass = false;
        targetItem.EnhancementBonus = 0;
        targetItem.enhancementBonus = 0;
        if (targetItem.Enchantment != null)
        {
            targetItem.Enchantment = null;
        }

        // Rod becomes nonmagical
        rod.RodIsExpended = true;
        rod.RodCanCancelMagic = false;
        rod.CountsAsMagicForBypass = false;
        rod.Name = "Rod of Cancellation (Expended)";
        rod.Description = "This rod has been used and is now a nonmagical piece of kindling.";
        rod.BasePriceGp = 0;

        Debug.Log("[MetamagicRodActivation] Rod of Cancellation is now expended and nonmagical.");
        return true;
    }

    // ── Immovable Rod ─────────────────────────────────────────

    /// <summary>
    /// Toggle the Immovable Rod's activated state.
    /// When activated, the rod holds position and supports up to 8,000 lbs.
    /// </summary>
    public static void ToggleImmovableRod(ItemData rod)
    {
        if (rod == null || !rod.RodIsImmovable)
        {
            Debug.LogWarning("[MetamagicRodActivation] Item is not an Immovable Rod.");
            return;
        }

        rod.RodIsActivated = !rod.RodIsActivated;

        if (rod.RodIsActivated)
        {
            Debug.Log("[MetamagicRodActivation] Immovable Rod ACTIVATED. " +
                      $"Rod is locked in place. Supports up to {rod.RodHoldWeightLbs} lbs. " +
                      $"DC {rod.RodMoveDC} Strength to move.");
        }
        else
        {
            Debug.Log("[MetamagicRodActivation] Immovable Rod DEACTIVATED. Rod can be moved normally.");
        }
    }

    // ── Rod of Lordly Might ───────────────────────────────────

    /// <summary>
    /// Switch the Rod of Lordly Might to a different weapon mode.
    /// Updates weapon stats (enhancement, damage, weapon type) accordingly.
    /// </summary>
    public static void SwitchLordlyMightMode(ItemData rod, LordlyMightWeaponMode mode)
    {
        if (rod == null || !rod.RodIsLordlyMight)
        {
            Debug.LogWarning("[MetamagicRodActivation] Item is not a Rod of Lordly Might.");
            return;
        }

        rod.RodLordlyMightMode = (int)mode;

        switch (mode)
        {
            case LordlyMightWeaponMode.HeavyMace:
                rod.RodWeaponEnhancement = 3;
                rod.RodWeaponDamageDice = "1d8";
                rod.RodWeaponMode = "Heavy Mace";
                break;
            case LordlyMightWeaponMode.FlamingSword:
                rod.RodWeaponEnhancement = 1;
                rod.RodWeaponDamageDice = "1d8"; // +1d6 fire handled separately
                rod.RodWeaponMode = "Flaming Longsword";
                break;
            case LordlyMightWeaponMode.Battleaxe:
                rod.RodWeaponEnhancement = 4;
                rod.RodWeaponDamageDice = "1d8";
                rod.RodWeaponMode = "Battleaxe";
                break;
            case LordlyMightWeaponMode.Shortspear:
                rod.RodWeaponEnhancement = 3;
                rod.RodWeaponDamageDice = "1d6";
                rod.RodWeaponMode = "Shortspear";
                break;
            case LordlyMightWeaponMode.Longsword:
                rod.RodWeaponEnhancement = 2;
                rod.RodWeaponDamageDice = "1d8";
                rod.RodWeaponMode = "Longsword";
                break;
            case LordlyMightWeaponMode.ClimbingPole:
                rod.RodWeaponEnhancement = 0;
                rod.RodWeaponDamageDice = "";
                rod.RodWeaponMode = "Climbing Pole (50 ft)";
                break;
        }

        Debug.Log($"[MetamagicRodActivation] Rod of Lordly Might transformed to {rod.RodWeaponMode} " +
                  $"(+{rod.RodWeaponEnhancement}, {rod.RodWeaponDamageDice})");
    }

    /// <summary>
    /// Use the Rod of Lordly Might's Fear Cone ability (2/day, 30 ft cone, DC 16 Will).
    /// Returns true if the ability was used, false if no uses remain.
    /// </summary>
    public static bool UseLordlyMightFear(ItemData rod)
    {
        if (rod == null || !rod.RodIsLordlyMight) return false;

        if (rod.RodFearUsesToday >= rod.RodFearUsesPerDay)
        {
            Debug.Log($"[MetamagicRodActivation] Fear cone already used " +
                      $"{rod.RodFearUsesToday}/{rod.RodFearUsesPerDay} today.");
            return false;
        }

        rod.RodFearUsesToday++;
        Debug.Log($"[MetamagicRodActivation] Rod of Lordly Might: Fear cone! " +
                  $"30 ft cone, DC {rod.RodFearConeDC} Will save or frightened. " +
                  $"Uses: {rod.RodFearUsesToday}/{rod.RodFearUsesPerDay} today.");
        return true;
    }

    // ── Rod of Python ─────────────────────────────────────────

    /// <summary>
    /// Toggle the Rod of Python between rod form and snake form.
    /// Snake: 60 HP, AC 15, +13 attack, 1d3+10 damage, constrict.
    /// </summary>
    public static void ToggleRodOfPython(ItemData rod)
    {
        if (rod == null || !rod.RodCanTransformToSnake)
        {
            Debug.LogWarning("[MetamagicRodActivation] Item is not a Rod of Python.");
            return;
        }

        rod.RodIsInSnakeForm = !rod.RodIsInSnakeForm;

        if (rod.RodIsInSnakeForm)
        {
            // Transform to snake — reset HP to max
            rod.RodSnakeHP = rod.RodSnakeMaxHP;
            Debug.Log("[MetamagicRodActivation] Rod of Python transforms into Giant Constrictor Snake! " +
                      $"HP: {rod.RodSnakeHP}, AC: {rod.RodSnakeAC}, " +
                      $"Attack: +{rod.RodSnakeAttackBonus}, Damage: {rod.RodSnakeDamage}");
        }
        else
        {
            Debug.Log("[MetamagicRodActivation] Snake transforms back into Rod of Python.");
        }
    }

    // ── Rod of Security ───────────────────────────────────────

    /// <summary>
    /// Activate the Rod of Security to create a paradise demiplane.
    /// 200 person-days capacity, complete rest and healing. 1/week.
    /// Returns true if the demiplane was created successfully.
    /// </summary>
    public static bool ActivateRodOfSecurity(ItemData rod)
    {
        if (rod == null || !rod.RodCanCreateDemiplane) return false;

        if (rod.RodDemiplaneUsesThisWeek >= rod.RodDemiplaneUsesPerWeek)
        {
            Debug.Log("[MetamagicRodActivation] Rod of Security already used this week.");
            return false;
        }

        rod.RodDemiplaneUsesThisWeek++;
        Debug.Log("[MetamagicRodActivation] Rod of Security activated! " +
                  $"Paradise demiplane created. Capacity: {rod.RodDemiplaneCapacity} people. " +
                  $"Total: {rod.RodDemiplanePersonDays} person-days. " +
                  "Complete rest and healing inside.");
        return true;
    }

    // ── Rod of Alertness ──────────────────────────────────────

    /// <summary>
    /// Use the Rod of Alertness Animate Objects ability (1/day).
    /// The rod becomes a +1 defending longsword temporarily.
    /// </summary>
    public static bool UseAlertnessAnimate(ItemData rod)
    {
        if (rod == null || !rod.RodIsAlertness) return false;

        if (rod.RodAnimateUsesToday >= rod.RodAnimateUsesPerDay)
        {
            Debug.Log("[MetamagicRodActivation] Animate Objects already used today.");
            return false;
        }

        rod.RodAnimateUsesToday++;
        Debug.Log("[MetamagicRodActivation] Rod of Alertness: Animate Objects! " +
                  "Rod becomes a +1 defending longsword.");
        return true;
    }

    /// <summary>
    /// Use the Rod of Alertness Prayer ability (1/day, 30 ft radius).
    /// </summary>
    public static bool UseAlertnessPrayer(ItemData rod)
    {
        if (rod == null || !rod.RodIsAlertness) return false;

        if (rod.RodPrayerUsesToday >= rod.RodPrayerUsesPerDay)
        {
            Debug.Log("[MetamagicRodActivation] Prayer already used today.");
            return false;
        }

        rod.RodPrayerUsesToday++;
        Debug.Log("[MetamagicRodActivation] Rod of Alertness: Prayer! " +
                  "+1 luck to attack, damage, saves, skills for allies; " +
                  "-1 luck penalty for enemies within 30 ft.");
        return true;
    }

    // ── Rod of Negation ───────────────────────────────────────

    /// <summary>
    /// Use the Rod of Negation's Greater Dispel Magic ability (2/day, CL 15).
    /// Returns true if the ability was used successfully.
    /// </summary>
    public static bool UseNegationGreaterDispel(ItemData rod)
    {
        if (rod == null || !rod.RodIsNegation) return false;

        if (rod.RodGreaterDispelUsesToday >= rod.RodGreaterDispelUsesPerDay)
        {
            Debug.Log("[MetamagicRodActivation] Greater Dispel Magic already used " +
                      $"{rod.RodGreaterDispelUsesToday}/{rod.RodGreaterDispelUsesPerDay} today.");
            return false;
        }

        rod.RodGreaterDispelUsesToday++;
        Debug.Log("[MetamagicRodActivation] Rod of Negation: Greater Dispel Magic! " +
                  $"CL {rod.RodDispelCL}. Uses: {rod.RodGreaterDispelUsesToday}/{rod.RodGreaterDispelUsesPerDay}.");
        return true;
    }

    // ── Rod of Splendor ───────────────────────────────────────

    /// <summary>Use Rod of Splendor to create a feast (1/day, feeds 12 people).</summary>
    public static bool UseSplendorFeast(ItemData rod)
    {
        if (rod == null || !rod.RodIsSplendor) return false;

        if (rod.RodSplendorFeastUsesToday >= rod.RodSplendorFeastUsesPerDay)
        {
            Debug.Log("[MetamagicRodActivation] Feast already created today.");
            return false;
        }

        rod.RodSplendorFeastUsesToday++;
        Debug.Log("[MetamagicRodActivation] Rod of Splendor: Magnificent feast created! Feeds 12 people.");
        return true;
    }

    /// <summary>Use Rod of Splendor to create fine clothes (7/week).</summary>
    public static bool UseSplendorClothes(ItemData rod)
    {
        if (rod == null || !rod.RodIsSplendor) return false;

        if (rod.RodSplendorClothesThisWeek >= rod.RodSplendorClothesPerWeek)
        {
            Debug.Log("[MetamagicRodActivation] Clothes limit reached this week.");
            return false;
        }

        rod.RodSplendorClothesThisWeek++;
        Debug.Log("[MetamagicRodActivation] Rod of Splendor: Fine clothes created! " +
                  $"{rod.RodSplendorClothesThisWeek}/{rod.RodSplendorClothesPerWeek} this week.");
        return true;
    }

    /// <summary>Use Rod of Splendor to create a pavilion tent (1/week, 100 people).</summary>
    public static bool UseSplendorTent(ItemData rod)
    {
        if (rod == null || !rod.RodIsSplendor) return false;

        if (rod.RodSplendorTentUsesThisWeek >= rod.RodSplendorTentUsesPerWeek)
        {
            Debug.Log("[MetamagicRodActivation] Tent already created this week.");
            return false;
        }

        rod.RodSplendorTentUsesThisWeek++;
        Debug.Log("[MetamagicRodActivation] Rod of Splendor: Pavilion tent created! Holds 100 people.");
        return true;
    }

    // ── Utility: Find Rods in Inventory ───────────────────────

    /// <summary>
    /// Find all metamagic rods that can be applied to a given spell from a character's inventory.
    /// Returns rods that have uses remaining and can affect the spell's level.
    /// </summary>
    public static List<ItemData> FindApplicableMetamagicRods(List<ItemData> inventory, SpellData spell)
    {
        var applicable = new List<ItemData>();
        if (inventory == null || spell == null) return applicable;

        foreach (var item in inventory)
        {
            if (item == null || !item.IsRod || !item.RodIsMetamagic) continue;
            if (ValidateRodApplication(item, spell) == null)
            {
                applicable.Add(item);
            }
        }

        return applicable;
    }

    /// <summary>
    /// Get a summary of all rod daily use states for logging/UI.
    /// </summary>
    public static string GetRodUseSummary(ItemData rod)
    {
        if (rod == null || !rod.IsRod) return "";

        var parts = new List<string>();

        if (rod.RodIsMetamagic)
        {
            parts.Add($"Metamagic uses: {rod.RodUsesToday}/{rod.RodUsesPerDay}");
        }

        if (rod.RodCanAbsorbSpells)
        {
            parts.Add($"Absorbed levels: {rod.RodAbsorbedLevels}/{rod.RodMaxAbsorbedLevels}");
        }

        if (rod.RodIsLordlyMight)
        {
            parts.Add($"Fear uses: {rod.RodFearUsesToday}/{rod.RodFearUsesPerDay}");
            parts.Add($"Mode: {rod.RodWeaponMode}");
        }

        if (rod.RodIsAlertness)
        {
            parts.Add($"Animate: {rod.RodAnimateUsesToday}/{rod.RodAnimateUsesPerDay}");
            parts.Add($"Prayer: {rod.RodPrayerUsesToday}/{rod.RodPrayerUsesPerDay}");
        }

        if (rod.RodIsNegation)
        {
            parts.Add($"Greater Dispel: {rod.RodGreaterDispelUsesToday}/{rod.RodGreaterDispelUsesPerDay}");
        }

        if (rod.RodCanDetectEnemies)
        {
            parts.Add($"Detection: {rod.RodUsesToday}/{rod.RodUsesPerDay}");
        }

        if (rod.RodIsSplendor)
        {
            parts.Add($"Feast: {rod.RodSplendorFeastUsesToday}/{rod.RodSplendorFeastUsesPerDay}/day");
            parts.Add($"Clothes: {rod.RodSplendorClothesThisWeek}/{rod.RodSplendorClothesPerWeek}/wk");
            parts.Add($"Tent: {rod.RodSplendorTentUsesThisWeek}/{rod.RodSplendorTentUsesPerWeek}/wk");
        }

        if (rod.RodCanCreateDemiplane)
        {
            parts.Add($"Demiplane: {rod.RodDemiplaneUsesThisWeek}/{rod.RodDemiplaneUsesPerWeek}/wk");
        }

        if (rod.RodCanTransformToSnake)
        {
            parts.Add(rod.RodIsInSnakeForm ?
                $"Snake Form (HP: {rod.RodSnakeHP}/{rod.RodSnakeMaxHP})" : "Rod Form");
        }

        if (rod.RodIsImmovable)
        {
            parts.Add(rod.RodIsActivated ? "LOCKED in place" : "Normal (movable)");
        }

        if (rod.RodIsExpended)
        {
            parts.Add("EXPENDED (nonmagical)");
        }

        return string.Join(" | ", parts);
    }
}
