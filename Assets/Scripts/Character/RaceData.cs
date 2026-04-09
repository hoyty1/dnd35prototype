using System.Collections.Generic;

/// <summary>
/// D&D 3.5 racial data structure.
/// Defines ability score modifiers, speed, size, special abilities, and racial bonuses.
/// </summary>
[System.Serializable]
public class RaceData
{
    // ========== ENUMS ==========

    public enum Size { Fine, Diminutive, Tiny, Small, Medium, Large, Huge, Gargantuan, Colossal }
    public enum CreatureType { Humanoid }
    public enum VisionType { Normal, LowLight, Darkvision }

    // ========== IDENTITY ==========
    public string RaceName;
    public Size RaceSize;
    public CreatureType Type;

    // ========== ABILITY SCORE MODIFIERS ==========
    public int STRModifier;
    public int DEXModifier;
    public int CONModifier;
    public int WISModifier;
    public int INTModifier;
    public int CHAModifier;

    // ========== PHYSICAL TRAITS ==========
    /// <summary>Base land speed in feet (e.g., 30 for most Medium races, 20 for dwarves).</summary>
    public int BaseSpeedFeet;

    /// <summary>Base land speed in hexes (derived from feet: 5 ft per hex).</summary>
    public int BaseSpeedHexes => BaseSpeedFeet / 5;

    /// <summary>If true, speed is NOT reduced by armor or encumbrance (Dwarf trait).</summary>
    public bool SpeedNotReducedByArmor;

    // ========== VISION ==========
    public VisionType Vision;
    /// <summary>Darkvision range in feet (0 if none).</summary>
    public int DarkvisionRange;

    // ========== RACIAL WEAPON PROFICIENCIES ==========
    /// <summary>
    /// Weapons this race is automatically proficient with (treated as martial).
    /// Uses item IDs from ItemDatabase.
    /// </summary>
    public List<string> RacialWeaponProficiencies = new List<string>();

    /// <summary>
    /// Weapon familiarity: these exotic weapons are treated as martial weapons.
    /// Uses item IDs from ItemDatabase.
    /// </summary>
    public List<string> WeaponFamiliarity = new List<string>();

    // ========== RACIAL COMBAT BONUSES ==========

    /// <summary>
    /// Racial attack bonus against specific creature types (e.g., Dwarf +1 vs orcs/goblinoids).
    /// Key = creature type tag (e.g., "Orc", "Goblinoid"), Value = bonus.
    /// </summary>
    public Dictionary<string, int> RacialAttackBonuses = new Dictionary<string, int>();

    /// <summary>
    /// Racial dodge bonus to AC against specific creature types (e.g., Dwarf +4 vs giants).
    /// Key = creature type tag, Value = bonus.
    /// </summary>
    public Dictionary<string, int> RacialACBonuses = new Dictionary<string, int>();

    /// <summary>
    /// Stability bonus on checks to resist bull rush/trip (Dwarf = +4).
    /// </summary>
    public int StabilityBonus;

    // ========== RACIAL SAVING THROW BONUSES (for future) ==========
    /// <summary>Bonus on saves vs poison (Dwarf = +2).</summary>
    public int SaveVsPoison;

    /// <summary>Bonus on saves vs spells/spell-like effects (Dwarf = +2).</summary>
    public int SaveVsSpells;

    /// <summary>Bonus on saves vs enchantment (Elf = +2).</summary>
    public int SaveVsEnchantment;

    // ========== RACIAL SKILL BONUSES (for future) ==========
    /// <summary>
    /// Racial skill bonuses. Key = skill name, Value = bonus.
    /// E.g., Elf: Listen +2, Search +2, Spot +2
    /// </summary>
    public Dictionary<string, int> RacialSkillBonuses = new Dictionary<string, int>();

    // ========== SPECIAL TRAITS ==========
    /// <summary>Immune to sleep effects (Elf).</summary>
    public bool ImmunityToSleep;

    /// <summary>Stonecunning: +2 Search checks related to stone (Dwarf).</summary>
    public bool Stonecunning;

    /// <summary>Automatic Search check within 5 ft of secret door (Elf).</summary>
    public bool AutoSearchSecretDoors;

    // ========== DISPLAY HELPERS ==========

    /// <summary>Get a formatted string of ability score modifiers.</summary>
    public string GetAbilityModifierString()
    {
        var parts = new List<string>();
        if (STRModifier != 0) parts.Add($"STR {CharacterStats.FormatMod(STRModifier)}");
        if (DEXModifier != 0) parts.Add($"DEX {CharacterStats.FormatMod(DEXModifier)}");
        if (CONModifier != 0) parts.Add($"CON {CharacterStats.FormatMod(CONModifier)}");
        if (WISModifier != 0) parts.Add($"WIS {CharacterStats.FormatMod(WISModifier)}");
        if (INTModifier != 0) parts.Add($"INT {CharacterStats.FormatMod(INTModifier)}");
        if (CHAModifier != 0) parts.Add($"CHA {CharacterStats.FormatMod(CHAModifier)}");
        return parts.Count > 0 ? string.Join(", ", parts) : "None";
    }

    /// <summary>Get a summary of racial features for display.</summary>
    public string GetFeatureSummary()
    {
        var lines = new List<string>();
        lines.Add($"{RaceName} ({RaceSize})");
        lines.Add($"Ability Mods: {GetAbilityModifierString()}");
        lines.Add($"Speed: {BaseSpeedFeet} ft ({BaseSpeedHexes} hexes)");

        if (SpeedNotReducedByArmor)
            lines.Add("Speed not reduced by armor/encumbrance");

        if (Vision == VisionType.Darkvision)
            lines.Add($"Darkvision {DarkvisionRange} ft");
        else if (Vision == VisionType.LowLight)
            lines.Add("Low-light vision");

        if (RacialWeaponProficiencies.Count > 0)
            lines.Add($"Weapon Prof: {string.Join(", ", RacialWeaponProficiencies)}");

        if (RacialAttackBonuses.Count > 0)
        {
            foreach (var kvp in RacialAttackBonuses)
                lines.Add($"+{kvp.Value} attack vs {kvp.Key}");
        }

        if (ImmunityToSleep) lines.Add("Immunity to sleep");
        if (Stonecunning) lines.Add("Stonecunning");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Check if this race grants proficiency with a specific weapon (by item ID).
    /// </summary>
    public bool HasRacialProficiency(string weaponId)
    {
        if (weaponId == null) return false;
        return RacialWeaponProficiencies.Contains(weaponId.ToLower()) ||
               WeaponFamiliarity.Contains(weaponId.ToLower());
    }

    /// <summary>
    /// Get racial attack bonus against a target with specified creature tags.
    /// </summary>
    public int GetRacialAttackBonus(List<string> targetCreatureTags)
    {
        if (targetCreatureTags == null) return 0;
        int bonus = 0;
        foreach (var tag in targetCreatureTags)
        {
            if (RacialAttackBonuses.ContainsKey(tag))
                bonus += RacialAttackBonuses[tag];
        }
        return bonus;
    }

    /// <summary>
    /// Get racial AC dodge bonus against a target with specified creature tags.
    /// </summary>
    public int GetRacialACBonus(List<string> targetCreatureTags)
    {
        if (targetCreatureTags == null) return 0;
        int bonus = 0;
        foreach (var tag in targetCreatureTags)
        {
            if (RacialACBonuses.ContainsKey(tag))
                bonus += RacialACBonuses[tag];
        }
        return bonus;
    }
}
