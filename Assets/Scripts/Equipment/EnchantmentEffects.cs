using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// D&D 3.5e Enchantment Effects - Combat integration for weapon/armor enchantments
// Phase 3-4: Combat effects - elemental damage, alignment damage, keen, speed,
//            fortification, energy resistance
// ============================================================================

/// <summary>
/// Static utility class providing all combat effect calculations for enchanted items.
/// Called from CharacterController during attack resolution, damage calculation,
/// and defensive checks. All data is read from EnchantmentProperties — no hardcoding.
/// </summary>
public static class EnchantmentEffects
{
    // ========================================================================
    // WEAPON ATTACK MODIFIERS
    // ========================================================================

    /// <summary>
    /// Calculate extra attack bonus from enchantments (beyond base enhancement bonus).
    /// Currently only Bane provides this (+2 vs matching creature type).
    /// </summary>
    /// <param name="weapon">The attacking weapon.</param>
    /// <param name="targetCreatureType">The target's creature type string (e.g., "Undead").</param>
    /// <returns>Additional attack bonus from enchantments.</returns>
    public static int GetEnchantmentAttackBonus(ItemData weapon, string targetCreatureType)
    {
        if (weapon == null || !weapon.IsEnchanted) return 0;

        int bonus = 0;
        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats == null) continue;

            // Bane: +2 attack vs matching creature type
            if (stats.IsBane && IsBaneMatch(weapon.Enchantment.BaneCreatureType, targetCreatureType))
            {
                bonus += stats.BaneEnhancementBonus;
            }

            // Generic attack bonus (future-proofing)
            bonus += stats.AttackBonus;
        }
        return bonus;
    }

    // ========================================================================
    // WEAPON DAMAGE - ELEMENTAL
    // ========================================================================

    /// <summary>
    /// Roll all extra elemental damage from weapon enchantments on a normal hit.
    /// Returns a list of (DamageType, amount) pairs for combat log display.
    /// </summary>
    public static List<EnchantmentDamageResult> RollElementalDamage(ItemData weapon)
    {
        var results = new List<EnchantmentDamageResult>();
        if (weapon == null || !weapon.IsEnchanted) return results;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats == null) continue;

            // Skip abilities with no per-hit damage
            if (stats.ExtraDamageDice <= 0 || stats.ExtraDamageDieSides <= 0) continue;

            // Skip Merciful if suppressed (deals lethal normally instead)
            if (weapon.Enchantment.Abilities[i] == EnchantmentType.MercifulWeapon && weapon.Enchantment.MercifulSuppressed)
                continue;

            int damage = DiceService.RollMultiple(stats.ExtraDamageDice, stats.ExtraDamageDieSides,
                $"{stats.DisplayName} damage");

            results.Add(new EnchantmentDamageResult
            {
                Source = weapon.Enchantment.Abilities[i],
                DamageType = stats.ExtraDamageType,
                Amount = damage,
                DisplayName = stats.DisplayName,
            });
        }
        return results;
    }

    /// <summary>
    /// Roll extra elemental damage on a critical hit (Burst abilities).
    /// Burst dice scale with crit multiplier: ×2 = 1d10, ×3 = 2d10, ×4 = 3d10.
    /// </summary>
    public static List<EnchantmentDamageResult> RollCritBonusDamage(ItemData weapon, int critMultiplier)
    {
        var results = new List<EnchantmentDamageResult>();
        if (weapon == null || !weapon.IsEnchanted) return results;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats == null) continue;

            if (stats.CritBonusDice <= 0 || stats.CritBonusDieSides <= 0) continue;

            // Scale dice with crit multiplier if applicable
            int numDice = stats.CritBonusDice;
            if (stats.CritDiceScaleWithMultiplier && critMultiplier > 2)
            {
                // ×2 crit = base dice, ×3 = base+1, ×4 = base+2
                numDice = stats.CritBonusDice + (critMultiplier - 2);
            }

            int damage = DiceService.RollMultiple(numDice, stats.CritBonusDieSides,
                $"{stats.DisplayName} crit bonus damage");

            results.Add(new EnchantmentDamageResult
            {
                Source = weapon.Enchantment.Abilities[i],
                DamageType = stats.ExtraDamageType,
                Amount = damage,
                DisplayName = $"{stats.DisplayName} (crit)",
            });
        }
        return results;
    }

    // ========================================================================
    // WEAPON DAMAGE - ALIGNMENT
    // ========================================================================

    /// <summary>
    /// Roll alignment-based bonus damage (Holy, Unholy, Axiomatic, Anarchic).
    /// Only applies if the target's alignment matches.
    /// </summary>
    /// <param name="weapon">The attacking weapon.</param>
    /// <param name="targetAlignment">The target creature's alignment.</param>
    /// <returns>List of alignment damage results.</returns>
    public static List<EnchantmentDamageResult> RollAlignmentDamage(ItemData weapon, Alignment targetAlignment)
    {
        var results = new List<EnchantmentDamageResult>();
        if (weapon == null || !weapon.IsEnchanted) return results;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats == null || !stats.IsAlignmentDamage) continue;

            // Check if target alignment matches
            if (!IsAlignmentVulnerable(targetAlignment, stats.AlignmentDamageTargets))
                continue;

            int damage = DiceService.RollMultiple(stats.AlignmentDamageDice, stats.AlignmentDamageDieSides,
                $"{stats.DisplayName} alignment damage");

            results.Add(new EnchantmentDamageResult
            {
                Source = weapon.Enchantment.Abilities[i],
                DamageType = DamageType.Untyped, // Alignment damage is untyped per RAW
                Amount = damage,
                DisplayName = stats.DisplayName,
            });
        }
        return results;
    }

    // ========================================================================
    // WEAPON DAMAGE - BANE
    // ========================================================================

    /// <summary>
    /// Roll Bane bonus damage against matching creature type (+2d6).
    /// </summary>
    public static List<EnchantmentDamageResult> RollBaneDamage(ItemData weapon, string targetCreatureType)
    {
        var results = new List<EnchantmentDamageResult>();
        if (weapon == null || !weapon.IsEnchanted) return results;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats == null || !stats.IsBane) continue;

            if (!IsBaneMatch(weapon.Enchantment.BaneCreatureType, targetCreatureType))
                continue;

            int damage = DiceService.RollMultiple(stats.BaneDamageDice, stats.BaneDamageDieSides,
                $"Bane ({weapon.Enchantment.BaneCreatureType}) damage");

            results.Add(new EnchantmentDamageResult
            {
                Source = weapon.Enchantment.Abilities[i],
                DamageType = DamageType.Untyped,
                Amount = damage,
                DisplayName = $"Bane ({weapon.Enchantment.BaneCreatureType})",
            });
        }
        return results;
    }

    // ========================================================================
    // WEAPON DAMAGE - VICIOUS
    // ========================================================================

    /// <summary>
    /// Roll Vicious weapon damage — returns both target damage and wielder backlash.
    /// </summary>
    /// <param name="weapon">The attacking weapon.</param>
    /// <param name="targetDamage">Output: extra damage to the target.</param>
    /// <param name="wielderBacklash">Output: damage to the wielder.</param>
    /// <returns>True if Vicious effect was applied.</returns>
    public static bool RollViciousDamage(ItemData weapon, out int targetDamage, out int wielderBacklash)
    {
        targetDamage = 0;
        wielderBacklash = 0;
        if (weapon == null || !weapon.IsEnchanted) return false;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats == null || !stats.ViciousEffect) continue;

            targetDamage = DiceService.RollMultiple(stats.ViciousDamageDice, stats.ViciousDamageDieSides, "Vicious target damage");
            wielderBacklash = DiceService.RollMultiple(stats.ViciousBacklashDice, stats.ViciousBacklashDieSides, "Vicious backlash");
            return true;
        }
        return false;
    }

    // ========================================================================
    // WEAPON - VORPAL
    // ========================================================================

    /// <summary>
    /// Check if Vorpal effect triggers. Vorpal activates on a natural 20 confirmed crit.
    /// </summary>
    /// <param name="weapon">The attacking weapon.</param>
    /// <param name="naturalRoll">The natural d20 roll.</param>
    /// <param name="critConfirmed">Whether the critical was confirmed.</param>
    /// <returns>True if Vorpal decapitation should occur.</returns>
    public static bool CheckVorpalEffect(ItemData weapon, int naturalRoll, bool critConfirmed)
    {
        if (weapon == null || !weapon.IsEnchanted) return false;
        if (naturalRoll != 20 || !critConfirmed) return false;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats != null && stats.VorpalEffect) return true;
        }
        return false;
    }

    // ========================================================================
    // WEAPON - SPEED (Extra Attack)
    // ========================================================================

    /// <summary>
    /// Check if weapon grants an extra attack (Speed ability, as haste).
    /// </summary>
    public static bool GrantsExtraAttack(ItemData weapon)
    {
        if (weapon == null || !weapon.IsEnchanted) return false;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats != null && stats.GrantsExtraAttack) return true;
        }
        return false;
    }

    // ========================================================================
    // WEAPON - WOUNDING
    // ========================================================================

    /// <summary>
    /// Check if weapon has the Wounding ability (1 CON damage per hit).
    /// </summary>
    public static bool HasWoundingEffect(ItemData weapon)
    {
        if (weapon == null || !weapon.IsEnchanted) return false;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats != null && stats.WoundingEffect) return true;
        }
        return false;
    }

    // ========================================================================
    // WEAPON - SEEKING (Concealment Negation)
    // ========================================================================

    /// <summary>
    /// Check if weapon negates concealment (Seeking ability).
    /// </summary>
    public static bool NegatesConcealment(ItemData weapon)
    {
        if (weapon == null || !weapon.IsEnchanted) return false;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats != null && stats.NegatesConcealment) return true;
        }
        return false;
    }

    // ========================================================================
    // WEAPON - RETURNING
    // ========================================================================

    /// <summary>
    /// Check if thrown weapon returns to thrower.
    /// </summary>
    public static bool ReturnsWhenThrown(ItemData weapon)
    {
        if (weapon == null || !weapon.IsEnchanted) return false;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats != null && stats.ReturnsWhenThrown) return true;
        }
        return false;
    }

    // ========================================================================
    // WEAPON - DEFENDING
    // ========================================================================

    /// <summary>
    /// Get the AC bonus from a Defending weapon (transferred enhancement points).
    /// </summary>
    public static int GetDefendingACBonus(ItemData weapon)
    {
        if (weapon == null || !weapon.IsEnchanted) return 0;
        if (weapon.Enchantment.DefendingACTransfer <= 0) return 0;

        // Can't transfer more than the weapon's enhancement bonus
        int maxTransfer = Mathf.Max(0, weapon.ResolveEnhancementBonus());
        return Mathf.Min(weapon.Enchantment.DefendingACTransfer, maxTransfer);
    }

    /// <summary>
    /// Get the remaining attack/damage enhancement bonus after Defending transfer.
    /// </summary>
    public static int GetDefendingReducedEnhancement(ItemData weapon)
    {
        if (weapon == null) return 0;
        int baseEnh = Mathf.Max(0, weapon.ResolveEnhancementBonus());
        return baseEnh - GetDefendingACBonus(weapon);
    }

    // ========================================================================
    // WEAPON - BYPASS TAGS
    // ========================================================================

    /// <summary>
    /// Get all DR bypass tags granted by weapon enchantments.
    /// </summary>
    public static DamageBypassTag GetEnchantmentBypassTags(ItemData weapon)
    {
        DamageBypassTag tags = DamageBypassTag.None;
        if (weapon == null || !weapon.IsEnchanted) return tags;

        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats == null) continue;
            tags |= stats.AlignmentBypassTag;
        }
        return tags;
    }

    // ========================================================================
    // ARMOR/SHIELD - FORTIFICATION
    // ========================================================================

    /// <summary>
    /// Check if a critical hit or sneak attack is negated by Fortification.
    /// Rolls a percentile check against the highest fortification percentage
    /// from the defender's armor and shield combined.
    /// </summary>
    /// <param name="armor">Equipped armor (can be null).</param>
    /// <param name="shield">Equipped shield (can be null).</param>
    /// <param name="rollResult">Output: the actual d100 roll for logging.</param>
    /// <returns>True if the crit/sneak attack is negated.</returns>
    public static bool CheckFortification(ItemData armor, ItemData shield, out int rollResult)
    {
        int totalFort = GetFortificationPercent(armor) + GetFortificationPercent(shield);
        // Per RAW, fortification from different sources doesn't stack beyond design intent,
        // but for simplicity we take the max of armor + shield (capped at 100)
        totalFort = Mathf.Min(totalFort, 100);

        if (totalFort <= 0)
        {
            rollResult = 0;
            return false;
        }

        return DiceService.PercentileCheck(totalFort, "Fortification check", out rollResult);
    }

    /// <summary>Get the fortification percentage from a single piece of equipment.</summary>
    public static int GetFortificationPercent(ItemData item)
    {
        if (item == null || !item.IsEnchanted) return 0;

        int maxFort = 0;
        for (int i = 0; i < item.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(item.Enchantment.Abilities[i]);
            if (stats != null && stats.FortificationPercent > maxFort)
                maxFort = stats.FortificationPercent;
        }
        return maxFort;
    }

    // ========================================================================
    // ARMOR/SHIELD - ENERGY RESISTANCE
    // ========================================================================

    /// <summary>
    /// Get the total energy resistance for a specific damage type from armor and shield.
    /// Multiple sources of the same element don't stack — take the highest.
    /// </summary>
    public static int GetEnergyResistance(ItemData armor, ItemData shield, DamageType damageType)
    {
        int armorResist = GetItemEnergyResistance(armor, damageType);
        int shieldResist = GetItemEnergyResistance(shield, damageType);
        return Mathf.Max(armorResist, shieldResist); // Don't stack, take highest
    }

    /// <summary>Get energy resistance from a single item for a specific damage type.</summary>
    private static int GetItemEnergyResistance(ItemData item, DamageType damageType)
    {
        if (item == null || !item.IsEnchanted) return 0;

        int maxResist = 0;
        for (int i = 0; i < item.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(item.Enchantment.Abilities[i]);
            if (stats == null) continue;
            if (stats.ResistanceDamageType == damageType && stats.ResistanceAmount > maxResist)
                maxResist = stats.ResistanceAmount;
        }
        return maxResist;
    }

    /// <summary>
    /// Apply energy resistance to incoming elemental damage.
    /// Returns the reduced damage amount.
    /// </summary>
    public static int ApplyEnergyResistance(int incomingDamage, DamageType damageType, ItemData armor, ItemData shield)
    {
        int resistance = GetEnergyResistance(armor, shield, damageType);
        if (resistance <= 0) return incomingDamage;

        int reducedDamage = Mathf.Max(0, incomingDamage - resistance);
        if (reducedDamage < incomingDamage)
        {
            Debug.Log($"[EnchantmentEffects] Energy resistance {resistance} reduced {DamageTextUtils.GetDamageTypeDisplay(damageType)} " +
                      $"damage from {incomingDamage} to {reducedDamage}.");
        }
        return reducedDamage;
    }

    // ========================================================================
    // ARMOR/SHIELD - SPELL RESISTANCE
    // ========================================================================

    /// <summary>
    /// Get the highest spell resistance granted by armor enchantments.
    /// </summary>
    public static int GetSpellResistance(ItemData armor, ItemData shield)
    {
        return Mathf.Max(GetItemSpellResistance(armor), GetItemSpellResistance(shield));
    }

    private static int GetItemSpellResistance(ItemData item)
    {
        if (item == null || !item.IsEnchanted) return 0;

        int maxSR = 0;
        for (int i = 0; i < item.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(item.Enchantment.Abilities[i]);
            if (stats != null && stats.SpellResistance > maxSR)
                maxSR = stats.SpellResistance;
        }
        return maxSR;
    }

    // ========================================================================
    // ARMOR/SHIELD - DAMAGE REDUCTION
    // ========================================================================

    /// <summary>
    /// Get DR amount from Invulnerability or similar enchantments.
    /// </summary>
    public static int GetEnchantmentDR(ItemData armor, ItemData shield)
    {
        return Mathf.Max(GetItemEnchantmentDR(armor), GetItemEnchantmentDR(shield));
    }

    private static int GetItemEnchantmentDR(ItemData item)
    {
        if (item == null || !item.IsEnchanted) return 0;

        int maxDR = 0;
        for (int i = 0; i < item.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(item.Enchantment.Abilities[i]);
            if (stats != null && stats.DamageReductionAmount > maxDR)
                maxDR = stats.DamageReductionAmount;
        }
        return maxDR;
    }

    // ========================================================================
    // ARMOR/SHIELD - SKILL BONUSES
    // ========================================================================

    /// <summary>
    /// Get competence bonus to a specific skill from armor/shield enchantments.
    /// Takes the highest single source (competence bonuses don't stack).
    /// </summary>
    public static int GetSkillBonus(ItemData armor, ItemData shield, string skillName)
    {
        int armorBonus = GetItemSkillBonus(armor, skillName);
        int shieldBonus = GetItemSkillBonus(shield, skillName);
        return Mathf.Max(armorBonus, shieldBonus);
    }

    private static int GetItemSkillBonus(ItemData item, string skillName)
    {
        if (item == null || !item.IsEnchanted) return 0;

        int maxBonus = 0;
        for (int i = 0; i < item.Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(item.Enchantment.Abilities[i]);
            if (stats == null) continue;
            if (stats.SkillBonusTarget == skillName && stats.SkillBonus > maxBonus)
                maxBonus = stats.SkillBonus;
        }
        return maxBonus;
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    /// <summary>
    /// Check if a target alignment is vulnerable to an alignment-based weapon.
    /// Holy targets Evil, Unholy targets Good, Axiomatic targets Chaotic, Anarchic targets Lawful.
    /// </summary>
    private static bool IsAlignmentVulnerable(Alignment targetAlignment, DamageBypassTag damageTargets)
    {
        if (targetAlignment == Alignment.None) return false;

        // Holy targets Evil creatures
        if (damageTargets.HasFlag(DamageBypassTag.Evil))
            return AlignmentHelper.IsEvil(targetAlignment);

        // Unholy targets Good creatures
        if (damageTargets.HasFlag(DamageBypassTag.Good))
            return AlignmentHelper.IsGood(targetAlignment);

        // Axiomatic targets Chaotic creatures
        if (damageTargets.HasFlag(DamageBypassTag.Chaotic))
            return AlignmentHelper.IsChaotic(targetAlignment);

        // Anarchic targets Lawful creatures
        if (damageTargets.HasFlag(DamageBypassTag.Lawful))
            return AlignmentHelper.IsLawful(targetAlignment);

        return false;
    }

    /// <summary>
    /// Check if a creature type matches a Bane weapon's target type.
    /// Case-insensitive comparison.
    /// </summary>
    private static bool IsBaneMatch(string baneType, string creatureType)
    {
        if (string.IsNullOrEmpty(baneType) || string.IsNullOrEmpty(creatureType))
            return false;
        return string.Equals(baneType.Trim(), creatureType.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}

// ============================================================================
// ENCHANTMENT DAMAGE RESULT - Data structure for combat log integration
// ============================================================================

/// <summary>
/// Represents a single instance of bonus damage from an enchantment effect.
/// Used to report elemental, alignment, and bane damage to the combat log.
/// </summary>
public struct EnchantmentDamageResult
{
    /// <summary>The enchantment that produced this damage.</summary>
    public EnchantmentType Source;

    /// <summary>The damage type (Fire, Cold, etc. or Untyped for alignment damage).</summary>
    public DamageType DamageType;

    /// <summary>The rolled damage amount.</summary>
    public int Amount;

    /// <summary>Display name for combat log (e.g., "Flaming", "Holy", "Bane (Undead)").</summary>
    public string DisplayName;

    /// <summary>Format for combat log display.</summary>
    public override string ToString()
    {
        string typeStr = DamageType != DamageType.Untyped
            ? $" {DamageTextUtils.GetDamageTypeDisplay(DamageType)}"
            : "";
        return $"+{Amount}{typeStr} ({DisplayName})";
    }
}
