using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Per-attack modifier breakdown shared by every weapon attack path (CMB-043)
// ============================================================================

/// <summary>
/// Every term of one weapon attack roll's modifier, built by
/// CharacterController.BuildAttackBonus and used by Attack, FullAttack (iterative and
/// natural), DualWieldAttack and FlurryOfBlows so all of them add the same terms.
/// Terms resolved inside PerformSingleAttackWithCrit (weapon enhancement or masterwork,
/// True Strike, invisible attacker, helpless or blinded target, bane, Destruction smite)
/// are not part of this struct. The damage roll's terms are <see cref="WeaponDamageBreakdown"/> (CMB-003).
/// </summary>
public struct AttackBonusBreakdown
{
    /// <summary>Base attack bonus step for this attack (BAB, an iterative step or a progressive-pool override).</summary>
    public int BaseAttackBonus;
    /// <summary>Penalty tied to the attack's place in a sequence: two-weapon fighting, flurry, secondary natural attack, Manyshot, Mobility AoO.</summary>
    public int SequenceModifier;
    /// <summary>Combat-log label for SequenceModifier (empty when the caller shows it another way).</summary>
    public string SequenceLabel;
    /// <summary>Ability modifier on the attack roll: DEX for ranged attacks and Weapon Finesse, otherwise STR (PHB p.134, p.102).</summary>
    public int AbilityMod;
    public string AbilityName;
    public int SizeModifier;
    public int FlankingBonus;
    public int RacialBonus;
    public int RangePenalty;
    public int MountedRangedPenalty;
    /// <summary>Feat terms (Power Attack, Point Blank Shot, Weapon Focus, Combat Expertise, Rapid Shot) plus Improved Critical.</summary>
    public AttackCalculator.FeatModifiers Feats;
    public int PronePenalty;
    public int FightingDefensivelyPenalty;
    public int ShootingIntoMeleePenalty;
    public bool PreciseShotNegated;
    public int WeaponNonProficiencyPenalty;
    public int ArmorNonProficiencyPenalty;
    /// <summary>CharacterStats.MoraleAttackBonus (Bless, Inspire Courage, charge or Pounce +2, other spell buffs; SPL-026).</summary>
    public int MoraleBonus;
    /// <summary>CharacterStats.ConditionAttackPenalty (Shaken, Sickened, Haste, Slow and other conditions).</summary>
    public int ConditionModifier;
    public int AidAnotherBonus;
    public int DamageModePenalty;
    public int SolidFogPenalty;
    public int WondrousBowAttackBonus;
    public int MagicStoneBonus;
    /// <summary>A bonus for this one attack only, such as a template smite's attack bonus; listed under <see cref="SituationalLabel"/>.</summary>
    public int SituationalBonus;
    public string SituationalLabel;
    /// <summary>Weapon threat range after Improved Critical.</summary>
    public int CritThreatMin;
    public int CritMultiplier;

    /// <summary>The modifier passed to the attack roll.</summary>
    public int Total =>
        BaseAttackBonus + SequenceModifier + AbilityMod + SizeModifier + FlankingBonus + RacialBonus
        + RangePenalty + MountedRangedPenalty + Feats.TotalFeatAttackModifier
        + PronePenalty + FightingDefensivelyPenalty + ShootingIntoMeleePenalty
        + WeaponNonProficiencyPenalty + ArmorNonProficiencyPenalty + MoraleBonus + ConditionModifier
        + AidAnotherBonus + DamageModePenalty + SolidFogPenalty + WondrousBowAttackBonus + MagicStoneBonus
        + SituationalBonus;

    /// <summary>BAB step + ability + size + sequence penalty: the figure shown in attack labels such as "Attack 2 (+6)".</summary>
    public int LabelBonus => BaseAttackBonus + SequenceModifier + AbilityMod + SizeModifier;

    /// <summary>
    /// Copies the terms onto a CombatResult so its attack breakdown lists exactly what was added.
    /// Morale and condition terms are listed by PerformSingleAttackWithCrit (AttachAttackBuffDebuffBreakdown).
    /// Call after PerformSingleAttackWithCrit. Callers that show SequenceModifier as the
    /// dual-wield/off-hand entry set SequenceLabel empty and fill BreakdownDualWieldPenalty themselves.
    /// </summary>
    public void ApplyToResult(CombatResult result, RangeInfo rangeInfo)
    {
        if (result == null)
            return;

        result.BreakdownBAB = BaseAttackBonus;
        result.BreakdownAbilityMod = AbilityMod;
        result.BreakdownAbilityName = AbilityName;
        result.SizeAttackBonus = SizeModifier;
        result.RacialAttackBonus = RacialBonus;
        result.PowerAttackValue = Feats.PowerAttackPenalty != 0 ? -Feats.PowerAttackPenalty : 0;
        result.RapidShotActive = Feats.RapidShotActive;
        result.PointBlankShotActive = Feats.PointBlankShotActive;
        result.WeaponFocusBonus = Feats.WeaponFocusBonus;
        result.CombatExpertisePenalty = Feats.CombatExpertisePenalty;
        result.FightingDefensivelyAttackPenalty = FightingDefensivelyPenalty;
        result.ShootingIntoMeleePenalty = ShootingIntoMeleePenalty;
        result.PreciseShotNegated = PreciseShotNegated;
        result.AidAnotherAttackBonus = AidAnotherBonus;
        result.WeaponNonProficiencyPenalty = WeaponNonProficiencyPenalty;
        result.ArmorNonProficiencyPenalty = ArmorNonProficiencyPenalty;
        // The damage terms (Power Attack, Weapon Specialization, ...) are set by PerformSingleAttackWithCrit from
        // the attack's WeaponDamageBreakdown (CMB-003).

        if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
        {
            result.IsRangedAttack = true;
            result.RangeDistanceFeet = rangeInfo.DistanceFeet;
            result.RangeDistanceSquares = rangeInfo.SquareDistance;
            result.RangeIncrementNumber = rangeInfo.IncrementNumber;
            result.RangePenalty = rangeInfo.Penalty;
        }

        // Terms CombatResult has no dedicated field for.
        if (SequenceModifier != 0 && !string.IsNullOrEmpty(SequenceLabel))
            result.AddAttackBuffDebuffModifier(SequenceLabel, SequenceModifier);
        result.AddAttackBuffDebuffModifier("prone", PronePenalty);
        result.AddAttackBuffDebuffModifier("mounted ranged", MountedRangedPenalty);
        result.AddAttackBuffDebuffModifier("Solid Fog", SolidFogPenalty);
        result.AddAttackBuffDebuffModifier("Bracers of Archery", WondrousBowAttackBonus);
        result.AddAttackBuffDebuffModifier("Magic Stone", MagicStoneBonus);
        if (SituationalBonus != 0)
            result.AddAttackBuffDebuffModifier(string.IsNullOrWhiteSpace(SituationalLabel) ? "situational" : SituationalLabel, SituationalBonus);
    }
}

// ============================================================================
// Per-hit weapon damage modifier shared by every weapon damage roll (CMB-003)
// ============================================================================

/// <summary>
/// Every static term of one weapon, natural or unarmed damage roll, built by
/// CharacterController.BuildWeaponDamageBonus and added by PerformSingleAttackWithCrit (every weapon
/// attack path: Attack, FullAttack, DualWieldAttack, FlurryOfBlows, rake, the grapple weapon attacks),
/// coup de grace, sunder and grapple damage, so they all add the same terms (CMB-003; the attack
/// roll's counterpart is <see cref="AttackBonusBreakdown"/>, CMB-043). The weapon dice and the extra
/// dice that are never multiplied (sneak attack, energy, alignment and bane riders, specific-item
/// effects) stay with the caller. On a confirmed critical hit these terms are added once, not
/// multiplied (CMB-004, an open owner question; RAW PHB p.134 multiplies them). Mount attacks and the
/// Confused self-attack still roll their own damage without it (CMB-162).
/// Rules applied: PHB p.134 (Strength to melee, thrown and sling damage; 1/2 in the off hand,
/// 1-1/2 two-handed, a penalty never multiplied, CMB-161; a bow takes only a penalty, a composite bow its
/// rating), PHB p.41 (full Strength in a flurry), MM p.312 (a natural attack's Strength share),
/// PHB p.98 (Power Attack, Point Blank Shot), p.102 (Weapon Specialization), p.29 (Inspire Courage),
/// p.264 (Prayer), p.224 (Divine Favor), p.250 (Magic Fang), p.186 (Destruction domain smite, melee
/// only), DMG p.224 (bane: +2 enhancement against its foe), DMG p.301 (Sickened: -2 weapon damage),
/// DMG p.250 (Bracers of Archery), PHB p.281 (Solid Fog), DMG p.283 (alchemical silver -1).
/// Spell bonuses of every type land in MoraleBonus (SPL-026).
/// </summary>
public struct WeaponDamageBreakdown
{
    /// <summary>Strength share (or none) by the weapon's hand rule or the natural attack's (MM p.312).</summary>
    public int StrengthBonus;
    /// <summary>"STR", "1.5× STR", "0.5× STR", "composite +2", "no STR modifier", ... (CharacterStats.GetDamageModifierDescription).</summary>
    public string StrengthLabel;
    /// <summary>The weapon adds no Strength or Power Attack to damage (torch, ItemData.NoStrengthToDamage).</summary>
    public bool StrengthSuppressed;
    /// <summary>The attack profile's own flat damage (ItemData.BonusDamage, Magic Stone's +1, a sunder's natural bonus).</summary>
    public int WeaponBonus;
    public string WeaponBonusLabel;
    /// <summary>The weapon's enhancement bonus to damage (ItemData.GetEnhancementDamageBonus).</summary>
    public int EnhancementBonus;
    /// <summary>A bane weapon's +2 effective enhancement against its designated foe (DMG p.224); set by PerformSingleAttackWithCrit, which knows the target.</summary>
    public int BaneBonus;
    /// <summary>Special material modifier (alchemical silver -1; ItemData.MaterialDamageModifier).</summary>
    public int MaterialModifier;
    public int PowerAttackBonus;
    public int PointBlankShotBonus;
    public int WeaponSpecializationBonus;
    /// <summary>
    /// CharacterStats.MoraleDamageBonus: Inspire Courage, Prayer, Divine Favor, Magic Fang and other spell buffs. Every
    /// spell bonus type is pooled there (SPL-026), so the term is labelled by its sources ("Divine Favor"), not as morale.
    /// </summary>
    public int MoraleBonus;
    public string MoraleLabel;
    /// <summary>Conditions that change weapon damage rolls (Sickened -2; CharacterStats.ConditionWeaponDamageModifier).</summary>
    public int ConditionModifier;
    public string ConditionLabel;
    /// <summary>Solid Fog's melee damage penalty.</summary>
    public int SolidFogPenalty;
    /// <summary>Bracers of Archery competence bonus, bows only.</summary>
    public int BracersOfArcheryBonus;
    /// <summary>Destruction domain smite (cleric level); set by PerformSingleAttackWithCrit for the smiting attack only.</summary>
    public int DestructionSmiteBonus;
    /// <summary>A bonus for this one attack only, such as a template smite's damage; listed under <see cref="SituationalLabel"/>.</summary>
    public int SituationalBonus;
    public string SituationalLabel;
    /// <summary>The attack is a ranged attack (the path's own reading, not the weapon's range increment): no Solid Fog penalty, no Destruction smite (PHB p.186: a melee attack).</summary>
    public bool IsRangedAttack;

    /// <summary>Power Attack, Point Blank Shot and Weapon Specialization.</summary>
    public int FeatBonus => PowerAttackBonus + PointBlankShotBonus + WeaponSpecializationBonus;

    /// <summary>The whole static modifier added to the damage roll.</summary>
    public int Total =>
        StrengthBonus + WeaponBonus + EnhancementBonus + BaneBonus + MaterialModifier + FeatBonus + MoraleBonus
        + ConditionModifier + SolidFogPenalty + BracersOfArcheryBonus + DestructionSmiteBonus + SituationalBonus;

    /// <summary>
    /// The non-zero terms in a fixed order, labelled for the combat log; their values add up to <see cref="Total"/>
    /// (minus the Strength term when <paramref name="includeStrength"/> is false). A suppressed Strength term is
    /// listed as "no STR modifier" with value 0.
    /// </summary>
    public List<AttackModifierBreakdownEntry> GetTerms(bool includeStrength = true)
    {
        var terms = new List<AttackModifierBreakdownEntry>();
        if (includeStrength)
        {
            if (StrengthBonus != 0)
                terms.Add(new AttackModifierBreakdownEntry(string.IsNullOrWhiteSpace(StrengthLabel) ? "STR" : StrengthLabel, StrengthBonus));
            else if (StrengthSuppressed)
                terms.Add(new AttackModifierBreakdownEntry("no STR modifier", 0));
        }
        AddTerm(terms, string.IsNullOrWhiteSpace(WeaponBonusLabel) ? "weapon" : WeaponBonusLabel, WeaponBonus);
        AddTerm(terms, "enhancement", EnhancementBonus);
        AddTerm(terms, "bane", BaneBonus);
        AddTerm(terms, "material", MaterialModifier);
        AddTerm(terms, "Power Attack", PowerAttackBonus);
        AddTerm(terms, "Point Blank Shot", PointBlankShotBonus);
        AddTerm(terms, "Weapon Specialization", WeaponSpecializationBonus);
        AddTerm(terms, string.IsNullOrWhiteSpace(MoraleLabel) ? "spell and morale bonuses" : MoraleLabel, MoraleBonus);
        AddTerm(terms, string.IsNullOrWhiteSpace(ConditionLabel) ? "conditions" : ConditionLabel, ConditionModifier);
        AddTerm(terms, "Solid Fog", SolidFogPenalty);
        AddTerm(terms, "Bracers of Archery", BracersOfArcheryBonus);
        AddTerm(terms, "Destruction smite", DestructionSmiteBonus);
        AddTerm(terms, string.IsNullOrWhiteSpace(SituationalLabel) ? "situational" : SituationalLabel, SituationalBonus);
        return terms;
    }

    /// <summary>One line for logs, e.g. "STR +3, enhancement +1, Inspire Courage +1 = +5".</summary>
    public string Describe()
    {
        List<AttackModifierBreakdownEntry> terms = GetTerms();
        var parts = new List<string>(terms.Count);
        for (int i = 0; i < terms.Count; i++)
            parts.Add(terms[i].Label + " " + CharacterStats.FormatMod(terms[i].Value));
        string sum = CharacterStats.FormatMod(Total);
        return parts.Count == 0 ? sum : string.Join(", ", parts) + " = " + sum;
    }

    private static void AddTerm(List<AttackModifierBreakdownEntry> terms, string label, int value)
    {
        if (value != 0)
            terms.Add(new AttackModifierBreakdownEntry(label, value));
    }
}

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
    /// Get the Weapon Focus attack bonus for a specific weapon.
    /// D&D 3.5e: Weapon Focus only applies when attacking with the chosen weapon.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="weapon">The weapon being used for the attack (null for unarmed).</param>
    /// <returns>Total Weapon Focus attack bonus (0 if wrong weapon).</returns>
    public static int GetWeaponFocusBonus(CharacterStats stats, ItemData weapon = null)
    {
        return FeatManager.GetWeaponFocusBonus(stats, weapon);
    }

    // ========================================================================
    // WEAPON SPECIALIZATION / GREATER WEAPON SPEC
    // ========================================================================

    /// <summary>
    /// Get the Weapon Specialization damage bonus for a specific weapon.
    /// D&D 3.5e: Weapon Specialization only applies when attacking with the chosen weapon.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="weapon">The weapon being used for the attack (null for unarmed).</param>
    /// <returns>Total Weapon Specialization damage bonus (0 if wrong weapon).</returns>
    public static int GetWeaponSpecBonus(CharacterStats stats, ItemData weapon = null)
    {
        return FeatManager.GetWeaponSpecializationBonus(stats, weapon);
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

        // Weapon Focus / Greater (weapon-specific check)
        result.WeaponFocusBonus = GetWeaponFocusBonus(stats, weapon);

        // Weapon Specialization / Greater (weapon-specific check)
        result.WeaponSpecDamageBonus = GetWeaponSpecBonus(stats, weapon);

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
