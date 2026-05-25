using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// D&D 3.5 Feat Manager - Applies feat effects to characters
// ============================================================================

/// <summary>
/// Manages feat application and effect calculation for characters.
/// Handles passive bonuses, combat modifiers, and feat queries.
/// </summary>
public static class FeatManager
{
    /// <summary>
    /// Apply all passive feat effects to a character's stats.
    /// Call this after feats are assigned and whenever stats need recalculation.
    /// </summary>
    public static void ApplyPassiveFeats(CharacterStats stats)
    {
        FeatDefinitions.Init();
        Debug.Log($"[FeatManager] Applying passive feats for {stats.CharacterName} ({stats.Feats.Count} feats)");

        // Calculate and apply HP bonuses (Toughness)
        int hpBonus = GetTotalHPBonus(stats);
        if (hpBonus > 0)
        {
            // If character is at full health (fresh init), set CurrentHP to include feat HP bonus
            if (stats.CurrentHP == stats.MaxHP)
            {
                stats.CurrentHP = stats.TotalMaxHP;
            }
            Debug.Log($"[FeatManager] {stats.CharacterName}: +{hpBonus} HP from feats (Toughness) → TotalMaxHP={stats.TotalMaxHP}, CurrentHP={stats.CurrentHP}");
        }

        // Log save bonuses
        int fortBonus = GetFortitudeSaveBonus(stats);
        int refBonus = GetReflexSaveBonus(stats);
        int willBonus = GetWillSaveBonus(stats);
        if (fortBonus > 0) Debug.Log($"[FeatManager] {stats.CharacterName}: +{fortBonus} Fortitude saves");
        if (refBonus > 0) Debug.Log($"[FeatManager] {stats.CharacterName}: +{refBonus} Reflex saves");
        if (willBonus > 0) Debug.Log($"[FeatManager] {stats.CharacterName}: +{willBonus} Will saves");

        // Log initiative bonus
        int initBonus = GetInitiativeBonus(stats);
        if (initBonus > 0) Debug.Log($"[FeatManager] {stats.CharacterName}: +{initBonus} Initiative");

        // Apply skill bonuses
        ApplySkillFeatBonuses(stats);
    }

    // ========================================================================
    // HP BONUSES
    // ========================================================================

    /// <summary>Get total HP bonus from feats (Toughness: +3 each).</summary>
    public static int GetTotalHPBonus(CharacterStats stats)
    {
        int bonus = 0;
        // Toughness can be taken multiple times. Count how many times.
        // In our system, feats are stored as names, so Toughness counts as 1.
        // For multiple Toughness, we'd need a count. For now, +3 per instance.
        if (stats.HasFeat("Toughness"))
            bonus += 3;
        return bonus;
    }

    // ========================================================================
    // SAVE BONUSES
    // ========================================================================

    /// <summary>Get total Fortitude save bonus from feats.</summary>
    public static int GetFortitudeSaveBonus(CharacterStats stats)
    {
        int bonus = 0;
        if (stats.HasFeat("Great Fortitude")) bonus += 2;
        return bonus;
    }

    /// <summary>Get total Reflex save bonus from feats.</summary>
    public static int GetReflexSaveBonus(CharacterStats stats)
    {
        int bonus = 0;
        if (stats.HasFeat("Lightning Reflexes")) bonus += 2;
        return bonus;
    }

    /// <summary>Get total Will save bonus from feats.</summary>
    public static int GetWillSaveBonus(CharacterStats stats)
    {
        int bonus = 0;
        if (stats.HasFeat("Iron Will")) bonus += 2;
        return bonus;
    }

    // ========================================================================
    // INITIATIVE BONUS
    // ========================================================================

    /// <summary>Get total initiative bonus from feats.</summary>
    public static int GetInitiativeBonus(CharacterStats stats)
    {
        int bonus = 0;
        if (stats.HasFeat("Improved Initiative")) bonus += 4;
        return bonus;
    }

    // ========================================================================
    // AC BONUSES
    // ========================================================================

    /// <summary>Get total feat AC bonus (Dodge, etc.).</summary>
    public static int GetACBonus(CharacterStats stats)
    {
        int bonus = 0;
        if (stats.HasFeat("Dodge")) bonus += 1;
        return bonus;
    }

    /// <summary>Get Two-Weapon Defense AC bonus (if dual wielding).</summary>
    public static int GetTWFDefenseACBonus(CharacterStats stats, bool isDualWielding)
    {
        if (isDualWielding && stats.HasFeat("Two-Weapon Defense"))
            return 1;
        return 0;
    }

    // ========================================================================
    // ATTACK BONUSES
    // ========================================================================

    /// <summary>
    /// Get attack bonus from Weapon Focus and Greater Weapon Focus for a specific weapon.
    /// D&D 3.5e PHB: Weapon Focus applies only to the specific weapon chosen when the feat was taken.
    /// </summary>
    public static int GetWeaponFocusBonus(CharacterStats stats, string weaponName)
    {
        if (!IsWeaponFocusMatch(stats, weaponName))
            return 0;

        int bonus = 0;
        if (stats.HasFeat("Weapon Focus")) bonus += 1;
        if (stats.HasFeat("Greater Weapon Focus")) bonus += 1;
        return bonus;
    }

    /// <summary>
    /// Get damage bonus from Weapon Specialization and Greater Weapon Specialization.
    /// D&D 3.5e PHB: Weapon Specialization applies only to the Weapon Focus weapon.
    /// </summary>
    public static int GetWeaponSpecializationBonus(CharacterStats stats, string weaponName)
    {
        if (!IsWeaponFocusMatch(stats, weaponName))
            return 0;

        int bonus = 0;
        if (stats.HasFeat("Weapon Specialization")) bonus += 2;
        if (stats.HasFeat("Greater Weapon Specialization")) bonus += 2;
        return bonus;
    }

    /// <summary>
    /// Check if a weapon name matches ANY of the character's Weapon Focus choices.
    /// D&D 3.5e: Weapon Focus can be taken multiple times for different weapons.
    /// Handles various naming conventions (e.g., "Mace, Heavy" matches "Mace, Heavy",
    /// "Bite" for natural weapons, composite weapon variants like "Composite Longbow (+2)").
    /// </summary>
    public static bool IsWeaponFocusMatch(CharacterStats stats, string weaponName)
    {
        if (stats == null || stats.WeaponFocusWeapons == null || stats.WeaponFocusWeapons.Count == 0)
            return false;
        if (string.IsNullOrWhiteSpace(weaponName))
            return false;

        string weapon = weaponName.Trim();
        string normalizedWeapon = NormalizeWeaponName(weapon);

        for (int i = 0; i < stats.WeaponFocusWeapons.Count; i++)
        {
            string choice = stats.WeaponFocusWeapons[i];
            if (string.IsNullOrWhiteSpace(choice)) continue;

            choice = choice.Trim();

            // Exact match (case-insensitive)
            if (string.Equals(choice, weapon, System.StringComparison.OrdinalIgnoreCase))
                return true;

            // Normalized match: handles composite variants, enhancement notation
            string normalizedChoice = NormalizeWeaponName(choice);
            if (string.Equals(normalizedChoice, normalizedWeapon, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Normalize a weapon name for matching purposes.
    /// Strips enhancement notation like "+1 Longsword" → "Longsword",
    /// composite rating like "Composite Longbow (+2)" → "Composite Longbow",
    /// and "Unarmed Strike" for natural weapon matching.
    /// </summary>
    private static string NormalizeWeaponName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        // Strip leading enhancement bonus: "+1 Longsword" → "Longsword"
        string result = ItemData.StripEnhancementNotation(name).Trim();

        // Strip trailing composite rating: "Composite Longbow (+2)" → "Composite Longbow"
        int parenIdx = result.IndexOf(" (+");
        if (parenIdx > 0)
            result = result.Substring(0, parenIdx).Trim();

        return result;
    }

    /// <summary>
    /// Get Weapon Focus attack bonus for a specific weapon item.
    /// This is the preferred overload when you have the actual weapon ItemData.
    /// </summary>
    public static int GetWeaponFocusBonus(CharacterStats stats, ItemData weapon)
    {
        if (weapon == null)
        {
            // Unarmed: check if WeaponFocusChoice is "Unarmed Strike"
            return GetWeaponFocusBonus(stats, "Unarmed Strike");
        }
        return GetWeaponFocusBonus(stats, weapon.Name);
    }

    /// <summary>
    /// Get Weapon Specialization damage bonus for a specific weapon item.
    /// This is the preferred overload when you have the actual weapon ItemData.
    /// </summary>
    public static int GetWeaponSpecializationBonus(CharacterStats stats, ItemData weapon)
    {
        if (weapon == null)
        {
            return GetWeaponSpecializationBonus(stats, "Unarmed Strike");
        }
        return GetWeaponSpecializationBonus(stats, weapon.Name);
    }

    /// <summary>
    /// Check if Weapon Finesse should apply (use DEX instead of STR for attack).
    /// Applies to light weapons, rapier, whip, spiked chain.
    /// </summary>
    public static bool ShouldUseWeaponFinesse(CharacterStats stats, ItemData weapon)
    {
        if (!stats.HasFeat("Weapon Finesse")) return false;
        if (weapon == null) return true; // Unarmed counts as light
        return weapon.IsLightWeapon || weapon.Name.ToLower().Contains(ItemIDs.RAPIER);
    }

    /// <summary>
    /// Get the attack modifier accounting for Weapon Finesse.
    /// Returns DEX mod if Weapon Finesse applies, otherwise STR mod.
    /// </summary>
    public static int GetMeleeAttackAbilityMod(CharacterStats stats, ItemData weapon)
    {
        if (ShouldUseWeaponFinesse(stats, weapon))
            return stats.DEXMod;
        return stats.STRMod;
    }

    // ========================================================================
    // COMBAT EXPERTISE (Active feat - like Power Attack but for AC)
    // ========================================================================

    /// <summary>
    /// Get max Combat Expertise value (up to 5 or BAB, whichever is lower).
    /// </summary>
    public static int GetMaxCombatExpertise(CharacterStats stats)
    {
        if (!stats.HasFeat("Combat Expertise")) return 0;
        return Mathf.Min(5, stats.BaseAttackBonus);
    }

    // ========================================================================
    // TWO-WEAPON FIGHTING PENALTIES
    // ========================================================================

    /// <summary>
    /// Get TWF penalties adjusted for TWF feats.
    /// Without feat: -6/-10 (normal) or -4/-8 (light off-hand)
    /// With TWF feat: -4/-4 (normal) or -2/-2 (light off-hand)
    /// </summary>
    public static (int mainPenalty, int offPenalty) GetTWFPenalties(CharacterStats stats, bool lightOffHand)
    {
        if (stats.HasFeat("Two-Weapon Fighting"))
        {
            // TWF feat: -2/-2 with light off-hand, -4/-4 without
            return lightOffHand ? (-2, -2) : (-4, -4);
        }
        else
        {
            // No TWF feat: -4/-8 with light off-hand, -6/-10 without
            return lightOffHand ? (-4, -8) : (-6, -10);
        }
    }

    /// <summary>
    /// Get the number of off-hand attacks for TWF.
    /// Base: 1. Improved TWF: 2. Greater TWF: 3.
    /// </summary>
    public static int GetOffHandAttackCount(CharacterStats stats)
    {
        int count = 1;
        if (stats.HasFeat("Improved Two-Weapon Fighting")) count++;
        if (stats.HasFeat("Greater Two-Weapon Fighting")) count++;
        return count;
    }

    // ========================================================================
    // THREAT RANGE (Improved Critical)
    // ========================================================================

    /// <summary>
    /// Get the adjusted critical threat range minimum for a weapon.
    /// Improved Critical doubles the threat range.
    /// E.g., 19-20 becomes 17-20; 20 becomes 19-20.
    /// </summary>
    public static int GetAdjustedCritThreatMin(CharacterStats stats, int baseThreatMin)
    {
        if (!stats.HasFeat("Improved Critical")) return baseThreatMin;

        // Double the threat range
        int threatRange = 21 - baseThreatMin; // e.g., 19-20 = 2, 20 = 1
        int doubledRange = threatRange * 2;
        int newMin = 21 - doubledRange;
        return Mathf.Max(2, newMin); // Can't go below 2
    }

    // ========================================================================
    // SKILL BONUSES
    // ========================================================================

    /// <summary>
    /// Apply all feat-based skill bonuses to a character's skills.
    /// Called during passive feat application.
    /// </summary>
    public static void ApplySkillFeatBonuses(CharacterStats stats)
    {
        if (stats.Skills == null || stats.Skills.Count == 0) return;

        var bonuses = GetAllSkillBonuses(stats);
        foreach (var kvp in bonuses)
        {
            if (stats.Skills.ContainsKey(kvp.Key))
            {
                Debug.Log($"[FeatManager] {stats.CharacterName}: +{kvp.Value} feat bonus to {kvp.Key}");
            }
        }
    }

    /// <summary>
    /// Get all skill bonuses from feats, keyed by skill name.
    /// </summary>
    public static Dictionary<string, int> GetAllSkillBonuses(CharacterStats stats)
    {
        var bonuses = new Dictionary<string, int>();
        FeatDefinitions.Init();

        foreach (string featName in stats.Feats)
        {
            var featDef = FeatDefinitions.GetFeat(featName);
            if (featDef == null) continue;

            foreach (var kvp in featDef.Benefit.SkillBonuses)
            {
                if (!bonuses.ContainsKey(kvp.Key))
                    bonuses[kvp.Key] = 0;
                bonuses[kvp.Key] += kvp.Value;
            }
        }

        // Skill Focus: +3 to chosen skill (stored as "Skill Focus" in feats, choice tracked separately)
        if (stats.HasFeat("Skill Focus") && stats.SkillFocusChoice != null)
        {
            if (!bonuses.ContainsKey(stats.SkillFocusChoice))
                bonuses[stats.SkillFocusChoice] = 0;
            bonuses[stats.SkillFocusChoice] += 3;
        }

        return bonuses;
    }

    /// <summary>
    /// Get total feat bonus for a specific skill.
    /// </summary>
    public static int GetSkillFeatBonus(CharacterStats stats, string skillName)
    {
        var bonuses = GetAllSkillBonuses(stats);
        return bonuses.ContainsKey(skillName) ? bonuses[skillName] : 0;
    }

    // ========================================================================
    // COMBAT QUERIES
    // ========================================================================

    /// <summary>Does this character have Cleave?</summary>
    public static bool HasCleave(CharacterStats stats) => stats.HasFeat("Cleave");

    /// <summary>Does this character have Great Cleave (unlimited)?</summary>
    public static bool HasGreatCleave(CharacterStats stats) => stats.HasFeat("Great Cleave");

    /// <summary>Does this character have Precise Shot (no penalty shooting into melee)?</summary>
    public static bool HasPreciseShot(CharacterStats stats) => stats.HasFeat("Precise Shot");

    /// <summary>Does this character have Blind-Fight (reroll concealment miss)?</summary>
    public static bool HasBlindFight(CharacterStats stats) => stats.HasFeat("Blind-Fight");

    /// <summary>Does this character have Combat Reflexes (extra AoOs)?</summary>
    public static bool HasCombatReflexes(CharacterStats stats) => stats.HasFeat("Combat Reflexes");

    /// <summary>Get the max AoOs per round (1 normally, 1+DEX mod with Combat Reflexes).</summary>
    public static int GetMaxAoOPerRound(CharacterStats stats)
    {
        if (stats.HasFeat("Combat Reflexes"))
            return 1 + Mathf.Max(0, stats.DEXMod);
        return 1;
    }

    /// <summary>Does this character have Spring Attack?</summary>
    public static bool HasSpringAttack(CharacterStats stats) => stats.HasFeat("Spring Attack");

    /// <summary>Does this character have Shot on the Run?</summary>
    public static bool HasShotOnTheRun(CharacterStats stats) => stats.HasFeat("Shot on the Run");

    /// <summary>Does this character have Quick Draw?</summary>
    public static bool HasQuickDraw(CharacterStats stats) => stats.HasFeat("Quick Draw");

    /// <summary>Does this character keep shield AC while performing a shield bash?</summary>
    public static bool HasImprovedShieldBash(CharacterStats stats) => stats != null && stats.HasFeat("Improved Shield Bash");

    /// <summary>Does this character have Improved Unarmed Strike (lethal unarmed by default)?</summary>
    public static bool HasImprovedUnarmedStrike(CharacterStats stats) => stats != null && stats.HasFeat("Improved Unarmed Strike");

    // ========================================================================
    // SPELL FOCUS / SPELL PENETRATION
    // ========================================================================

    /// <summary>
    /// Returns the total Spell Focus DC bonus for a given spell school.
    /// Spell Focus = +1, Greater Spell Focus = +1 (stacking to +2 total).
    /// </summary>
    public static int GetSpellFocusDCBonus(CharacterStats stats, string spellSchool)
    {
        if (stats == null || string.IsNullOrEmpty(spellSchool))
            return 0;

        int bonus = 0;
        if (stats.HasFeat("Spell Focus") &&
            string.Equals(stats.SpellFocusSchool, spellSchool, System.StringComparison.OrdinalIgnoreCase))
            bonus += 1;

        if (stats.HasFeat("Greater Spell Focus") &&
            string.Equals(stats.GreaterSpellFocusSchool, spellSchool, System.StringComparison.OrdinalIgnoreCase))
            bonus += 1;

        return bonus;
    }

    /// <summary>
    /// Returns the total Spell Penetration bonus to caster level checks vs SR.
    /// Spell Penetration = +2, Greater Spell Penetration = +2 (stacking to +4 total).
    /// </summary>
    public static int GetSpellPenetrationBonus(CharacterStats stats)
    {
        if (stats == null)
            return 0;

        int bonus = 0;
        if (stats.HasFeat("Spell Penetration"))
            bonus += 2;
        if (stats.HasFeat("Greater Spell Penetration"))
            bonus += 2;

        return bonus;
    }

    /// <summary>Does this character have Mobility (+4 AC vs movement AoO)?</summary>
    public static bool HasMobility(CharacterStats stats) => stats != null && stats.HasFeat("Mobility");

    /// <summary>Does this character have Diehard (conscious at negative HP)?</summary>
    public static bool HasDiehard(CharacterStats stats) => stats != null && stats.HasFeat("Diehard");

    /// <summary>Does this character have the Run feat (5× speed)?</summary>
    public static bool HasRun(CharacterStats stats) => stats != null && stats.HasFeat("Run");

    // ========================================================================
    // STUNNING FIST (D&D 3.5 PHB p.101)
    // ========================================================================

    /// <summary>Does this character have Stunning Fist?</summary>
    public static bool HasStunningFist(CharacterStats stats) => stats != null && stats.HasFeat("Stunning Fist");

    /// <summary>Can this character use Stunning Fist right now?</summary>
    public static bool CanUseStunningFist(CharacterStats stats)
    {
        if (!HasStunningFist(stats)) return false;
        // Lazy-initialize remaining uses if not yet set
        if (stats.StunningFistUsesRemaining < 0)
            stats.ResetStunningFistUses();
        return stats.StunningFistUsesRemaining > 0;
    }

    /// <summary>
    /// Calculate Stunning Fist DC = 10 + 1/2 character level + WIS modifier.
    /// </summary>
    public static int GetStunningFistDC(CharacterStats stats)
    {
        if (stats == null) return 10;
        int wisMod = stats.WISMod;
        int level = stats.Level;
        return 10 + (level / 2) + wisMod;
    }

    /// <summary>
    /// Attempt to apply Stunning Fist after a successful unarmed hit.
    /// Returns true if the target is stunned.
    /// Must be called AFTER the attack hits and damage is applied.
    /// </summary>
    public static bool TryApplyStunningFist(CharacterStats attackerStats, CharacterController target)
    {
        if (attackerStats == null || target == null || target.Stats == null) return false;
        if (!CanUseStunningFist(attackerStats)) return false;

        // Consume a use
        attackerStats.StunningFistUsesRemaining--;
        attackerStats.StunningFistActive = false; // Reset toggle

        int dc = GetStunningFistDC(attackerStats);
        int fortSave = DiceService.D20("Stunning Fist Fort save") + target.Stats.FortitudeSave;

        string attackerName = attackerStats.CharacterName ?? "Attacker";
        string targetName = target.Stats.CharacterName ?? "Target";

        if (fortSave < dc)
        {
            // Target is stunned for 1 round
            target.ApplyCondition(CombatConditionType.Stunned, 1, "Stunning Fist");
            Debug.Log($"[Stunning Fist] {targetName} STUNNED by {attackerName}! (Fort {fortSave} < DC {dc}) [{attackerStats.StunningFistUsesRemaining} uses remaining]");
            return true;
        }
        else
        {
            Debug.Log($"[Stunning Fist] {targetName} resists {attackerName}'s Stunning Fist (Fort {fortSave} >= DC {dc}) [{attackerStats.StunningFistUsesRemaining} uses remaining]");
            return false;
        }
    }

    // ========================================================================
    // DEFLECT ARROWS (D&D 3.5 PHB p.93)
    // ========================================================================

    /// <summary>Does this character have Deflect Arrows?</summary>
    public static bool HasDeflectArrows(CharacterStats stats) => stats != null && stats.HasFeat("Deflect Arrows");

    /// <summary>Does this character have Snatch Arrows?</summary>
    public static bool HasSnatchArrows(CharacterStats stats) => stats != null && stats.HasFeat("Snatch Arrows");

    /// <summary>
    /// Check if a character can deflect an incoming ranged attack this round.
    /// Requirements: Deflect Arrows feat, at least one free hand, not flat-footed,
    /// not already used this round, must be aware of the attack.
    /// </summary>
    public static bool CanDeflectArrow(CharacterController defender)
    {
        if (defender == null || defender.Stats == null) return false;
        if (!HasDeflectArrows(defender.Stats)) return false;
        if (defender.Stats.DeflectArrowsUsedThisRound) return false;

        // Must not be flat-footed, stunned, or otherwise unable to act
        if (defender.HasCondition(CombatConditionType.Stunned)) return false;
        if (defender.HasCondition(CombatConditionType.Paralyzed)) return false;
        if (defender.HasCondition(CombatConditionType.Helpless)) return false;
        if (defender.HasCondition(CombatConditionType.FlatFooted)) return false;

        // Must have at least one free hand (check if wielding two-handed or dual-wielding)
        if (!HasFreeHandForDeflection(defender)) return false;

        return true;
    }

    /// <summary>
    /// Attempt to deflect a ranged attack. Returns true if deflected (attack negated).
    /// </summary>
    public static bool TryDeflectArrow(CharacterController defender, CharacterController attacker)
    {
        if (!CanDeflectArrow(defender)) return false;

        // Mark as used this round
        defender.Stats.DeflectArrowsUsedThisRound = true;

        string defName = defender.Stats.CharacterName ?? "Defender";
        string atkName = attacker?.Stats?.CharacterName ?? "Attacker";

        bool snatched = HasSnatchArrows(defender.Stats);
        if (snatched)
        {
            Debug.Log($"[Snatch Arrows] {defName} CATCHES {atkName}'s ranged attack!");
        }
        else
        {
            Debug.Log($"[Deflect Arrows] {defName} DEFLECTS {atkName}'s ranged attack!");
        }

        return true;
    }

    /// <summary>Check if the character has at least one hand free for deflection.</summary>
    private static bool HasFreeHandForDeflection(CharacterController character)
    {
        if (character == null) return false;
        Inventory inv = character.GetInventoryData();
        if (inv == null) return true; // No inventory → assume free hands

        ItemData rightHand = inv.RightHandSlot;
        ItemData leftHand = inv.LeftHandSlot;
        ItemData twoHand = inv.TwoHandSlot;

        // Two-handed weapon → no free hand
        if (twoHand != null) return false;

        // Both hands occupied → no free hand
        if (rightHand != null && leftHand != null) return false;

        return true;
    }

    // ========================================================================
    // MANYSHOT (D&D 3.5 PHB p.97)
    // ========================================================================

    /// <summary>Does this character have Manyshot?</summary>
    public static bool HasManyshot(CharacterStats stats) => stats != null && stats.HasFeat("Manyshot");

    /// <summary>
    /// Quick prerequisite check: does this character meet all Manyshot feat prerequisites?
    /// Does not check weapon or distance — use the overload for combat-time validation.
    /// Prereqs: Manyshot feat, Point Blank Shot, Rapid Shot, DEX 17+, BAB +6.
    /// </summary>
    public static bool CanUseManyshot(CharacterStats stats)
    {
        if (!HasManyshot(stats)) return false;
        if (!stats.HasFeat("Point Blank Shot")) return false;
        if (!stats.HasFeat("Rapid Shot")) return false;
        if (stats.DEXMod < 3) return false; // DEX 17 means modifier >= +3
        if (stats.BaseAttackBonus < 6) return false;
        return true;
    }

    /// <summary>
    /// Full combat-time check: can use Manyshot with this specific weapon at this distance?
    /// </summary>
    public static bool CanUseManyshot(CharacterStats stats, ItemData weapon, float distanceFeet)
    {
        if (!CanUseManyshot(stats)) return false;
        if (weapon == null) return false;

        // Must be using a bow (not crossbow, not thrown)
        string wName = (weapon.Name ?? "").ToLowerInvariant();
        bool isBow = wName.Contains("bow") && !wName.Contains("crossbow");
        if (!isBow) return false;

        // Must be within 30 feet
        if (distanceFeet > 30f) return false;

        return true;
    }

    /// <summary>
    /// Get the Manyshot attack penalty (-4 per PHB).
    /// </summary>
    public static int GetManyshotAttackPenalty() => -4;

    /// <summary>
    /// Get how many arrows Manyshot fires (2 for standard, could be more at higher BAB in variants).
    /// D&D 3.5 PHB: always 2 arrows as a standard action.
    /// </summary>
    public static int GetManyshotArrowCount() => 2;

    // ========================================================================
    // IMPROVED PRECISE SHOT (D&D 3.5 PHB p.96)
    // ========================================================================

    /// <summary>Does this character have Improved Precise Shot?</summary>
    public static bool HasImprovedPreciseShot(CharacterStats stats) => stats != null && stats.HasFeat("Improved Precise Shot");

    /// <summary>
    /// Should concealment miss chance be ignored for this ranged attack?
    /// Improved Precise Shot ignores anything less than total concealment (50%).
    /// Total concealment still applies its full miss chance.
    /// </summary>
    public static bool ShouldIgnoreConcealment(CharacterStats attackerStats, int missChancePercent)
    {
        if (!HasImprovedPreciseShot(attackerStats)) return false;
        // Ignore partial concealment (20%) but not total concealment (50%)
        return missChancePercent < 50;
    }

    /// <summary>
    /// Should cover AC bonus be ignored for this ranged attack?
    /// Improved Precise Shot ignores anything less than total cover.
    /// </summary>
    public static bool ShouldIgnoreCover(CharacterStats attackerStats)
    {
        return HasImprovedPreciseShot(attackerStats);
    }

    // ========================================================================
    // SPRING ATTACK (D&D 3.5 PHB p.100)
    // ========================================================================

    // NOTE: HasSpringAttack() is defined above at the feat-query section (line ~428)

    /// <summary>
    /// Can this character use Spring Attack? Requires Dodge, Mobility, BAB +4.
    /// Spring Attack: move, make single melee attack, continue moving.
    /// The attack target does not get an AoO against the character.
    /// </summary>
    public static bool CanUseSpringAttack(CharacterStats stats)
    {
        if (!HasSpringAttack(stats)) return false;
        if (!stats.HasFeat("Dodge")) return false;
        if (!stats.HasFeat("Mobility")) return false;
        if (stats.BaseAttackBonus < 4) return false;
        return true;
    }

    // ========================================================================
    // SHOT ON THE RUN (D&D 3.5 PHB p.100)
    // ========================================================================

    // NOTE: HasShotOnTheRun() is defined above at the feat-query section (line ~431)

    /// <summary>
    /// Can this character use Shot on the Run? Ranged version of Spring Attack.
    /// </summary>
    public static bool CanUseShotOnTheRun(CharacterStats stats)
    {
        if (!HasShotOnTheRun(stats)) return false;
        if (!stats.HasFeat("Dodge")) return false;
        if (!stats.HasFeat("Mobility")) return false;
        if (!stats.HasFeat("Point Blank Shot")) return false;
        if (stats.BaseAttackBonus < 4) return false;
        return true;
    }

    // ========================================================================
    // WHIRLWIND ATTACK (D&D 3.5 PHB p.102)
    // ========================================================================

    /// <summary>Does this character have Whirlwind Attack?</summary>
    public static bool HasWhirlwindAttack(CharacterStats stats) => stats != null && stats.HasFeat("Whirlwind Attack");

    /// <summary>
    /// Can this character use Whirlwind Attack?
    /// Full-round action: make one melee attack at full BAB against each adjacent enemy.
    /// </summary>
    public static bool CanUseWhirlwindAttack(CharacterStats stats)
    {
        if (!HasWhirlwindAttack(stats)) return false;
        if (!stats.HasFeat("Combat Expertise")) return false;
        if (!stats.HasFeat("Dodge")) return false;
        if (!stats.HasFeat("Mobility")) return false;
        if (!stats.HasFeat("Spring Attack")) return false;
        if (stats.BaseAttackBonus < 4) return false;
        return true;
    }

    // ========================================================================
    // PHASE 2: SPECIALIZED TACTICS FEATS
    // ========================================================================

    // ── FAR SHOT (D&D 3.5 PHB p.94) ──

    /// <summary>Does this character have Far Shot?</summary>
    public static bool HasFarShot(CharacterStats stats) => stats != null && stats.HasFeat("Far Shot");

    /// <summary>
    /// Get the range increment multiplier for Far Shot.
    /// Projectile weapons: ×1.5. Thrown weapons: ×2.0. No Far Shot: ×1.0.
    /// </summary>
    public static float GetFarShotRangeMultiplier(CharacterStats stats, bool isThrownWeapon)
    {
        if (!HasFarShot(stats)) return 1.0f;
        return isThrownWeapon ? 2.0f : 1.5f;
    }

    // ── RAPID RELOAD (D&D 3.5 PHB p.99) ──
    // NOTE: Rapid Reload runtime mechanics are already fully implemented in
    // CharacterEquipment.HasRapidReloadForWeapon() and ItemData.GetEffectiveReloadAction().

    /// <summary>Does this character have any Rapid Reload variant?</summary>
    public static bool HasAnyRapidReload(CharacterStats stats)
    {
        if (stats == null) return false;
        return stats.HasFeat("Rapid Reload (Light Crossbow)")
            || stats.HasFeat("Rapid Reload (Heavy Crossbow)")
            || stats.HasFeat("Rapid Reload (Hand Crossbow)")
            || stats.HasFeat("Rapid Reload (Repeating Crossbow)");
    }

    // ── SNATCH ARROWS (D&D 3.5 PHB p.100) ──
    // Extends Deflect Arrows: catch the projectile and throw it back.

    // NOTE: HasSnatchArrows() already exists in the feat-query section above.

    /// <summary>
    /// Can this character use Snatch Arrows? Requires Deflect Arrows, Improved Unarmed Strike, DEX 15+.
    /// </summary>
    public static bool CanUseSnatchArrows(CharacterStats stats)
    {
        if (!HasSnatchArrows(stats)) return false;
        if (!stats.HasFeat("Deflect Arrows")) return false;
        if (!stats.HasFeat("Improved Unarmed Strike")) return false;
        if (stats.DEXMod < 2) return false; // DEX 15 → mod +2
        return true;
    }

    /// <summary>
    /// After deflecting a ranged attack, attempt to snatch the arrow and throw it back.
    /// Returns true if the character has Snatch Arrows (the throw-back is handled by the caller).
    /// </summary>
    public static bool ShouldSnatchAfterDeflect(CharacterController target)
    {
        if (target == null || target.Stats == null) return false;
        return CanUseSnatchArrows(target.Stats) && HasFreeHandForDeflection(target);
    }

    // ── IMPROVED COMBAT MANEUVERS ──
    // NOTE: Improved Bull Rush, Improved Overrun, Improved Sunder, and Improved Grapple
    // are already fully implemented with +4 bonuses and AoO suppression in:
    //   - CharacterController.RollBullRushAttackerCheck() (+4 from Improved Bull Rush)
    //   - CharacterCombatStats.GetGrappleModifier() (+4 from Improved Grapple)
    //   - CharacterController.ResolveSunder() (+4 from Improved Sunder)
    //   - OverrunSystem.ResolveOverrunOpposedCheck() (+4 from Improved Overrun)
    //   - GameManager.CombatActions (AoO suppression via attackerIgnoresAoO flags)

    /// <summary>Does this character have Improved Bull Rush?</summary>
    public static bool HasImprovedBullRush(CharacterStats stats) => stats != null && stats.HasFeat("Improved Bull Rush");

    /// <summary>Does this character have Improved Overrun?</summary>
    public static bool HasImprovedOverrun(CharacterStats stats) => stats != null && stats.HasFeat("Improved Overrun");

    /// <summary>Does this character have Improved Sunder?</summary>
    public static bool HasImprovedSunder(CharacterStats stats) => stats != null && stats.HasFeat("Improved Sunder");

    /// <summary>Does this character have Improved Grapple?</summary>
    public static bool HasImprovedGrapple(CharacterStats stats) => stats != null && stats.HasFeat("Improved Grapple");

    // ── AUGMENT SUMMONING (D&D 3.5 PHB p.89) ──

    /// <summary>Does this character have Augment Summoning?</summary>
    public static bool HasAugmentSummoning(CharacterStats stats) => stats != null && stats.HasFeat("Augment Summoning");

    /// <summary>
    /// Apply Augment Summoning bonuses to a summoned creature's stats.
    /// +4 enhancement bonus to STR and CON.
    /// </summary>
    public static void ApplyAugmentSummoningBonuses(CharacterStats summonStats)
    {
        if (summonStats == null) return;
        summonStats.BaseSTR += 4;
        summonStats.BaseCON += 4;
        // Recalculate HP for CON increase: +2 HP per hit die (from +4 CON = +2 mod)
        int conHPBonus = 2 * Mathf.Max(1, summonStats.HitDice);
        summonStats.MaxHP += conHPBonus;
        summonStats.CurrentHP += conHPBonus;
        Debug.Log($"[AugmentSummoning] {summonStats.CharacterName} gains +4 STR (now {summonStats.BaseSTR}), +4 CON (now {summonStats.BaseCON}), +{conHPBonus} HP");
    }

    // ── NATURAL SPELL (D&D 3.5 PHB p.97) ──

    /// <summary>Does this character have Natural Spell?</summary>
    public static bool HasNaturalSpell(CharacterStats stats) => stats != null && stats.HasFeat("Natural Spell");

    /// <summary>
    /// Can this character cast spells while in wild shape?
    /// Requires Natural Spell feat + Wild Shape class feature.
    /// </summary>
    public static bool CanCastInWildShape(CharacterStats stats)
    {
        if (!HasNaturalSpell(stats)) return false;
        return stats.HasWildShape;
    }

    // ── EXTRA TURNING (D&D 3.5 PHB p.94) ──

    /// <summary>Does this character have Extra Turning?</summary>
    public static bool HasExtraTurning(CharacterStats stats) => stats != null && stats.HasFeat("Extra Turning");

    /// <summary>
    /// Get the bonus turning attempts from Extra Turning feat.
    /// Each instance of the feat grants +4 attempts per day.
    /// </summary>
    public static int GetExtraTurningUses(CharacterStats stats)
    {
        if (!HasExtraTurning(stats)) return 0;
        return 4; // +4 per feat instance
    }

    // ── IMPROVED TURNING (D&D 3.5 PHB p.96) ──

    /// <summary>Does this character have Improved Turning?</summary>
    public static bool HasImprovedTurning(CharacterStats stats) => stats != null && stats.HasFeat("Improved Turning");

    /// <summary>
    /// Get the effective turning level bonus from Improved Turning.
    /// +1 to effective cleric level for turning checks and turning damage.
    /// </summary>
    public static int GetImprovedTurningLevelBonus(CharacterStats stats)
    {
        if (!HasImprovedTurning(stats)) return 0;
        return 1;
    }

    // ========================================================================
    // FEAT SUMMARY FOR DISPLAY
    // ========================================================================

    /// <summary>
    /// Get a summary of all feat bonuses for display in the character sheet.
    /// </summary>
    public static string GetFeatSummary(CharacterStats stats)
    {
        var lines = new List<string>();

        // Attack bonuses — show each Weapon Focus weapon separately
        if (stats.WeaponFocusWeapons != null && stats.WeaponFocusWeapons.Count > 0)
        {
            foreach (string wfWeapon in stats.WeaponFocusWeapons)
            {
                if (string.IsNullOrWhiteSpace(wfWeapon)) continue;
                int wfBonus = GetWeaponFocusBonus(stats, wfWeapon);
                if (wfBonus > 0) lines.Add($"Weapon Focus ({wfWeapon}): +{wfBonus} attack");

                int wsBonus = GetWeaponSpecializationBonus(stats, wfWeapon);
                if (wsBonus > 0) lines.Add($"Weapon Spec ({wfWeapon}): +{wsBonus} damage");
            }
        }

        if (stats.HasFeat("Weapon Finesse")) lines.Add("Weapon Finesse: DEX to attack");

        // Defense
        int acBonus = GetACBonus(stats);
        if (acBonus > 0) lines.Add($"Dodge: +{acBonus} AC");

        // Saves
        int fort = GetFortitudeSaveBonus(stats);
        int refSave = GetReflexSaveBonus(stats);
        int will = GetWillSaveBonus(stats);
        if (fort > 0) lines.Add($"Great Fortitude: +{fort} Fort");
        if (refSave > 0) lines.Add($"Lightning Reflexes: +{refSave} Ref");
        if (will > 0) lines.Add($"Iron Will: +{will} Will");

        // Initiative
        int init = GetInitiativeBonus(stats);
        if (init > 0) lines.Add($"Improved Initiative: +{init} Init");

        // HP
        int hp = GetTotalHPBonus(stats);
        if (hp > 0) lines.Add($"Toughness: +{hp} HP");

        // Special abilities
        if (stats.HasFeat("Cleave")) lines.Add("Cleave: Extra attack when foe drops");
        if (stats.HasFeat("Great Cleave")) lines.Add("Great Cleave: Unlimited cleave");
        if (stats.HasFeat("Power Attack")) lines.Add("Power Attack: Trade attack for damage");
        if (stats.HasFeat("Combat Expertise")) lines.Add("Combat Expertise: Trade attack for AC");
        if (stats.HasFeat("Combat Reflexes")) lines.Add($"Combat Reflexes: {GetMaxAoOPerRound(stats)} AoO/round");
        if (stats.HasFeat("Blind-Fight")) lines.Add("Blind-Fight: Reroll concealment miss");
        if (stats.HasFeat("Improved Critical")) lines.Add("Improved Critical: Double threat range");
        if (stats.HasFeat("Spell Focus")) lines.Add($"Spell Focus ({stats.SpellFocusSchool}): +1 DC");
        if (stats.HasFeat("Greater Spell Focus")) lines.Add($"Greater Spell Focus ({stats.GreaterSpellFocusSchool}): +1 DC");
        if (stats.HasFeat("Spell Penetration")) lines.Add("Spell Penetration: +2 vs SR");
        if (stats.HasFeat("Greater Spell Penetration")) lines.Add("Greater Spell Penetration: +2 vs SR");
        if (stats.HasFeat("Mobility")) lines.Add("Mobility: +4 dodge AC vs movement AoO");
        if (stats.HasFeat("Diehard")) lines.Add("Diehard: Conscious at negative HP");
        if (stats.HasFeat("Run")) lines.Add("Run: 5× speed");
        if (stats.HasFeat("Quick Draw")) lines.Add("Quick Draw: Draw weapon as free action");
        if (stats.HasFeat("Spring Attack")) lines.Add("Spring Attack: Move before/after melee, no AoO from target");
        if (stats.HasFeat("Shot on the Run")) lines.Add("Shot on the Run: Move before/after ranged attack");
        if (stats.HasFeat("Whirlwind Attack")) lines.Add("Whirlwind Attack: Attack all adjacent enemies (full-round)");
        if (HasStunningFist(stats)) lines.Add($"Stunning Fist: DC {GetStunningFistDC(stats)}, {stats.StunningFistUsesPerDay}/day");
        if (stats.HasFeat("Deflect Arrows")) lines.Add("Deflect Arrows: Deflect 1 ranged/round (free hand required)");
        if (stats.HasFeat("Snatch Arrows")) lines.Add("Snatch Arrows: Catch deflected arrows");
        if (stats.HasFeat("Manyshot")) lines.Add("Manyshot: Fire 2 arrows at -4 (standard action, ≤30ft)");
        if (stats.HasFeat("Improved Precise Shot")) lines.Add("Improved Precise Shot: Ignore cover/concealment (except total)");

        // Phase 2 Specialized Tactics feats
        if (HasFarShot(stats)) lines.Add("Far Shot: Range increment ×1.5 (projectile) or ×2 (thrown)");
        if (HasAnyRapidReload(stats)) lines.Add("Rapid Reload: Reduce crossbow reload time by one step");
        if (HasImprovedBullRush(stats)) lines.Add("Improved Bull Rush: +4 bonus, no AoO on bull rush");
        if (HasImprovedOverrun(stats)) lines.Add("Improved Overrun: +4 bonus, no AoO on overrun");
        if (HasImprovedSunder(stats)) lines.Add("Improved Sunder: +4 bonus, no AoO on sunder");
        if (HasImprovedGrapple(stats)) lines.Add("Improved Grapple: +4 bonus, no AoO on grapple");
        if (HasAugmentSummoning(stats)) lines.Add("Augment Summoning: Summoned creatures gain +4 STR/CON");
        if (HasNaturalSpell(stats)) lines.Add("Natural Spell: Cast spells while in wild shape");
        if (HasExtraTurning(stats)) lines.Add($"Extra Turning: +{GetExtraTurningUses(stats)} turn undead uses/day");
        if (HasImprovedTurning(stats)) lines.Add($"Improved Turning: +{GetImprovedTurningLevelBonus(stats)} effective cleric level for turning");

        // Metamagic feats
        foreach (var mmId in MetamagicData.AllMetamagicFeats)
        {
            string featName = MetamagicData.GetFeatName(mmId);
            if (stats.HasFeat(featName))
                lines.Add($"⚡ {featName}: {MetamagicData.GetShortEffect(mmId)}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Check if a character has any metamagic feats.
    /// </summary>
    public static bool HasAnyMetamagicFeat(CharacterStats stats)
    {
        foreach (var mmId in MetamagicData.AllMetamagicFeats)
        {
            if (stats.HasFeat(MetamagicData.GetFeatName(mmId)))
                return true;
        }
        return false;
    }
}