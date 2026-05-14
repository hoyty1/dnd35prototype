using UnityEngine;

// ============================================================================
// D&D 3.5 Attack Calculator - Centralized feat-based attack modifier logic
// ============================================================================

/// <summary>
/// Centralized calculator for feat-based attack and damage modifiers.
/// Extracts duplicated feat calculation logic from CharacterController's
/// attack methods (single attack, full attack, dual-wield) into a
/// single authoritative source.
/// </summary>
public static class AttackCalculator
{
    // ========================================================================
    // RESULT STRUCTURES
    // ========================================================================

    /// <summary>
    /// Complete set of feat-derived attack and damage modifiers for a single attack.
    /// </summary>
    public struct FeatModifiers
    {
        /// <summary>Power Attack penalty to attack rolls (negative value).</summary>
        public int PowerAttackPenalty;
        /// <summary>Power Attack bonus to damage rolls (positive value).</summary>
        public int PowerAttackDamageBonus;
        /// <summary>Whether Point Blank Shot is active.</summary>
        public bool PointBlankShotActive;
        /// <summary>Point Blank Shot attack bonus (+1).</summary>
        public int PointBlankShotAttackBonus;
        /// <summary>Point Blank Shot damage bonus (+1).</summary>
        public int PointBlankShotDamageBonus;
        /// <summary>Weapon Focus / Greater Weapon Focus attack bonus.</summary>
        public int WeaponFocusBonus;
        /// <summary>Weapon Specialization / Greater Weapon Spec damage bonus.</summary>
        public int WeaponSpecDamageBonus;
        /// <summary>Ability modifier for attack rolls (STR or DEX with Finesse).</summary>
        public int AbilityMod;
        /// <summary>Name of the ability used for attack rolls (STR, DEX, DEX(Finesse)).</summary>
        public string AbilityName;
        /// <summary>Combat Expertise penalty to attack rolls (negative value).</summary>
        public int CombatExpertisePenalty;
        /// <summary>Critical threat minimum after Improved Critical adjustment.</summary>
        public int CritThreatMin;
        /// <summary>Whether Rapid Shot is active (ranged full attack only).</summary>
        public bool RapidShotActive;
        /// <summary>Rapid Shot penalty to all attack rolls (-2).</summary>
        public int RapidShotPenalty;

        /// <summary>Total feat attack modifier (sum of all feat-related attack bonuses/penalties).</summary>
        public int TotalFeatAttackModifier =>
            PowerAttackPenalty + PointBlankShotAttackBonus + WeaponFocusBonus
            + CombatExpertisePenalty + RapidShotPenalty;

        /// <summary>Total feat damage modifier (sum of all feat-related damage bonuses).</summary>
        public int TotalFeatDamageBonus =>
            PowerAttackDamageBonus + PointBlankShotDamageBonus + WeaponSpecDamageBonus;
    }

    // ========================================================================
    // POWER ATTACK
    // ========================================================================

    /// <summary>
    /// Calculate Power Attack modifiers for a melee attack.
    /// D&amp;D 3.5e PHB p.98: Subtract from melee attack, add to melee damage.
    /// Two-handed weapons get 2x damage bonus.
    /// </summary>
    /// <param name="stats">Character's stats (must have Power Attack feat).</param>
    /// <param name="powerAttackValue">Current Power Attack setting (1 to BAB).</param>
    /// <param name="isMelee">Whether this is a melee attack.</param>
    /// <param name="isTwoHanded">Whether the weapon is wielded two-handed.</param>
    /// <param name="weaponDisablesStrDmg">Whether the weapon prevents STR damage (e.g. ray).</param>
    /// <param name="penalty">Output: attack penalty (negative).</param>
    /// <param name="damageBonus">Output: damage bonus (positive).</param>
    public static void CalculatePowerAttack(
        CharacterStats stats, int powerAttackValue,
        bool isMelee, bool isTwoHanded, bool weaponDisablesStrDmg,
        out int penalty, out int damageBonus)
    {
        penalty = 0;
        damageBonus = 0;

        if (!isMelee || !stats.HasFeat("Power Attack") || powerAttackValue <= 0 || weaponDisablesStrDmg)
            return;

        penalty = -powerAttackValue;
        damageBonus = isTwoHanded ? powerAttackValue * 2 : powerAttackValue;
    }

    // ========================================================================
    // POINT BLANK SHOT
    // ========================================================================

    /// <summary>
    /// Calculate Point Blank Shot modifiers for a ranged attack.
    /// D&amp;D 3.5e PHB p.98: +1 attack and damage for ranged attacks within 30 feet.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="isRanged">Whether this is a ranged attack.</param>
    /// <param name="distanceFeet">Distance to target in feet.</param>
    /// <param name="isActive">Output: whether PBS is active.</param>
    /// <param name="attackBonus">Output: attack bonus (+1 or 0).</param>
    /// <param name="damageBonus">Output: damage bonus (+1 or 0).</param>
    public static void CalculatePointBlankShot(
        CharacterStats stats, bool isRanged, int distanceFeet,
        out bool isActive, out int attackBonus, out int damageBonus)
    {
        isActive = false;
        attackBonus = 0;
        damageBonus = 0;

        if (!isRanged || !stats.HasFeat("Point Blank Shot") || distanceFeet > 30)
            return;

        isActive = true;
        attackBonus = 1;
        damageBonus = 1;
    }

    // ========================================================================
    // WEAPON FOCUS / GREATER WEAPON FOCUS
    // ========================================================================

    /// <summary>
    /// Get the Weapon Focus attack bonus from CharacterStats.
    /// Delegates to the existing Stats property which already accounts for
    /// Weapon Focus (+1) and Greater Weapon Focus (+1).
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <returns>Total Weapon Focus attack bonus.</returns>
    public static int GetWeaponFocusBonus(CharacterStats stats)
    {
        return stats.WeaponFocusAttackBonus;
    }

    // ========================================================================
    // WEAPON SPECIALIZATION / GREATER WEAPON SPEC
    // ========================================================================

    /// <summary>
    /// Get the Weapon Specialization damage bonus from CharacterStats.
    /// Delegates to the existing Stats property which already accounts for
    /// Weapon Spec (+2) and Greater Weapon Spec (+2).
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <returns>Total Weapon Specialization damage bonus.</returns>
    public static int GetWeaponSpecBonus(CharacterStats stats)
    {
        return stats.WeaponSpecDamageBonus;
    }

    // ========================================================================
    // WEAPON FINESSE
    // ========================================================================

    /// <summary>
    /// Determine the ability modifier and name for attack rolls,
    /// accounting for Weapon Finesse (DEX for light melee weapons).
    /// D&amp;D 3.5e PHB p.102: DEX instead of STR for attack with light/finesse weapons.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="weapon">The weapon being used (null = unarmed).</param>
    /// <param name="isRanged">Whether this is a ranged attack.</param>
    /// <param name="abilityMod">Output: the ability modifier to use.</param>
    /// <param name="abilityName">Output: label for the ability (STR, DEX, DEX(Finesse)).</param>
    public static void GetAttackAbilityModifier(
        CharacterStats stats, ItemData weapon, bool isRanged,
        out int abilityMod, out string abilityName)
    {
        if (isRanged)
        {
            abilityMod = stats.DEXMod;
            abilityName = "DEX";
        }
        else if (FeatManager.ShouldUseWeaponFinesse(stats, weapon))
        {
            abilityMod = stats.DEXMod;
            abilityName = "DEX(Finesse)";
        }
        else
        {
            abilityMod = stats.STRMod;
            abilityName = "STR";
        }
    }

    // ========================================================================
    // COMBAT EXPERTISE
    // ========================================================================

    /// <summary>
    /// Calculate Combat Expertise attack penalty.
    /// D&amp;D 3.5e PHB p.92: Trade melee attack bonus for AC (up to 5 or BAB).
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="isMelee">Whether this is a melee attack.</param>
    /// <returns>Attack penalty (negative value, or 0 if not active).</returns>
    public static int CalculateCombatExpertisePenalty(CharacterStats stats, bool isMelee)
    {
        if (!isMelee || !stats.HasFeat("Combat Expertise") || stats.CombatExpertiseValue <= 0)
            return 0;

        int maxCE = FeatManager.GetMaxCombatExpertise(stats);
        return -Mathf.Min(stats.CombatExpertiseValue, maxCE);
    }

    // ========================================================================
    // IMPROVED CRITICAL
    // ========================================================================

    /// <summary>
    /// Get the adjusted critical threat range minimum after Improved Critical.
    /// D&amp;D 3.5e PHB p.95: Doubles the weapon's threat range.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="baseThreatMin">Base critical threat minimum from weapon.</param>
    /// <returns>Adjusted threat minimum (may be lower = wider range).</returns>
    public static int GetAdjustedCritThreatMin(CharacterStats stats, int baseThreatMin)
    {
        return FeatManager.GetAdjustedCritThreatMin(stats, baseThreatMin);
    }

    // ========================================================================
    // RAPID SHOT
    // ========================================================================

    /// <summary>
    /// Calculate Rapid Shot status for a full attack with a ranged weapon.
    /// D&amp;D 3.5e PHB p.99: Extra attack at highest BAB, -2 to all ranged attacks.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="isRanged">Whether this is a ranged attack.</param>
    /// <param name="rapidShotEnabled">Whether the player has toggled Rapid Shot on.</param>
    /// <param name="isActive">Output: whether Rapid Shot is active for this attack sequence.</param>
    /// <param name="penalty">Output: -2 penalty to all attacks if active, 0 otherwise.</param>
    public static void CalculateRapidShot(
        CharacterStats stats, bool isRanged, bool rapidShotEnabled,
        out bool isActive, out int penalty)
    {
        bool hasFeat = stats.HasFeat("Rapid Shot");
        isActive = isRanged && hasFeat && rapidShotEnabled;
        penalty = isActive ? -2 : 0;
    }

    // ========================================================================
    // DEXTERITY DENIAL
    // ========================================================================

    /// <summary>
    /// Determine if a target should be denied their Dexterity bonus to AC.
    /// Checks for conditions like Blink, invisibility, flat-footed, etc.
    /// </summary>
    /// <param name="attacker">The attacking character.</param>
    /// <param name="target">The target character.</param>
    /// <param name="isFlanking">Whether the attacker is flanking.</param>
    /// <returns>True if the target is denied DEX to AC.</returns>
    public static bool ShouldDenyDexToAC(CharacterController attacker, CharacterController target, bool isFlanking)
    {
        if (target == null || target.Stats == null)
            return false;

        // Flanking denies DEX
        if (isFlanking)
            return true;

        // Flat-footed denies DEX
        if (target.IsFlatFootedCondition)
            return true;

        // Blink can deny DEX if target can't see invisible
        if (attacker != null && attacker.HasActiveBlinkEffect && attacker.BlinkDeniesDexToAC(target))
            return true;

        // Invisible attacker denies DEX (unless target can see invisible)
        if (attacker != null && attacker.HasActiveInvisibilityEffect && !target.CanSeeInvisible())
            return true;

        return false;
    }

    // ========================================================================
    // COMPLETE FEAT MODIFIER CALCULATION
    // ========================================================================

    /// <summary>
    /// Calculate all feat-based modifiers for a standard (non-dual-wield) attack.
    /// This is the primary entry point that replaces the duplicated feat blocks
    /// in CharacterController's attack methods.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="weapon">The weapon being used.</param>
    /// <param name="isRanged">Whether this is a ranged attack.</param>
    /// <param name="isMelee">Whether this is a melee attack.</param>
    /// <param name="isTwoHanded">Whether the weapon is wielded two-handed.</param>
    /// <param name="powerAttackValue">Current Power Attack setting.</param>
    /// <param name="distanceFeet">Distance to target in feet (for Point Blank Shot).</param>
    /// <param name="hasValidRange">Whether range info is available.</param>
    /// <param name="weaponDisablesStrDmg">Whether the weapon prevents STR damage bonuses.</param>
    /// <param name="baseCritThreatMin">Base critical threat minimum from weapon stats.</param>
    /// <param name="rapidShotEnabled">Whether Rapid Shot is toggled on (for full attacks).</param>
    /// <returns>Complete set of feat modifiers.</returns>
    public static FeatModifiers CalculateAllFeatModifiers(
        CharacterStats stats,
        ItemData weapon,
        bool isRanged,
        bool isMelee,
        bool isTwoHanded,
        int powerAttackValue,
        int distanceFeet,
        bool hasValidRange,
        bool weaponDisablesStrDmg,
        int baseCritThreatMin,
        bool rapidShotEnabled = false)
    {
        var result = new FeatModifiers();

        // Power Attack
        CalculatePowerAttack(stats, powerAttackValue, isMelee, isTwoHanded, weaponDisablesStrDmg,
            out result.PowerAttackPenalty, out result.PowerAttackDamageBonus);

        // Point Blank Shot
        CalculatePointBlankShot(stats, isRanged, distanceFeet,
            out result.PointBlankShotActive, out result.PointBlankShotAttackBonus, out result.PointBlankShotDamageBonus);

        // Weapon Focus / Greater
        result.WeaponFocusBonus = GetWeaponFocusBonus(stats);

        // Weapon Specialization / Greater
        result.WeaponSpecDamageBonus = GetWeaponSpecBonus(stats);

        // Weapon Finesse
        GetAttackAbilityModifier(stats, weapon, isRanged, out result.AbilityMod, out result.AbilityName);

        // Combat Expertise
        result.CombatExpertisePenalty = CalculateCombatExpertisePenalty(stats, isMelee);

        // Improved Critical
        result.CritThreatMin = GetAdjustedCritThreatMin(stats, baseCritThreatMin);

        // Rapid Shot
        CalculateRapidShot(stats, isRanged, rapidShotEnabled, out result.RapidShotActive, out result.RapidShotPenalty);

        return result;
    }
}
