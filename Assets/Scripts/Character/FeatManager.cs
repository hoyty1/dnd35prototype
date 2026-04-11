using System.Collections.Generic;
using UnityEngine;

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
    /// </summary>
    public static int GetWeaponFocusBonus(CharacterStats stats, string weaponName)
    {
        int bonus = 0;
        // Check if the character has Weapon Focus (stored as "Weapon Focus" with weapon choice tracked separately)
        if (stats.HasFeat("Weapon Focus")) bonus += 1;
        if (stats.HasFeat("Greater Weapon Focus")) bonus += 1;
        return bonus;
    }

    /// <summary>
    /// Get damage bonus from Weapon Specialization and Greater Weapon Specialization.
    /// </summary>
    public static int GetWeaponSpecializationBonus(CharacterStats stats, string weaponName)
    {
        int bonus = 0;
        if (stats.HasFeat("Weapon Specialization")) bonus += 2;
        if (stats.HasFeat("Greater Weapon Specialization")) bonus += 2;
        return bonus;
    }

    /// <summary>
    /// Check if Weapon Finesse should apply (use DEX instead of STR for attack).
    /// Applies to light weapons, rapier, whip, spiked chain.
    /// </summary>
    public static bool ShouldUseWeaponFinesse(CharacterStats stats, ItemData weapon)
    {
        if (!stats.HasFeat("Weapon Finesse")) return false;
        if (weapon == null) return true; // Unarmed counts as light
        return weapon.IsLightWeapon || weapon.Name.ToLower().Contains("rapier");
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

    // ========================================================================
    // FEAT SUMMARY FOR DISPLAY
    // ========================================================================

    /// <summary>
    /// Get a summary of all feat bonuses for display in the character sheet.
    /// </summary>
    public static string GetFeatSummary(CharacterStats stats)
    {
        var lines = new List<string>();

        // Attack bonuses
        int wfBonus = GetWeaponFocusBonus(stats, "");
        if (wfBonus > 0) lines.Add($"Weapon Focus: +{wfBonus} attack");

        int wsBonus = GetWeaponSpecializationBonus(stats, "");
        if (wsBonus > 0) lines.Add($"Weapon Spec: +{wsBonus} damage");

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
