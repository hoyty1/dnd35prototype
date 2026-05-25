using System.Collections.Generic;

/// <summary>
/// A pre-built NPC stat block template based on D&D 3.5e DMG Chapter 4 (p.110-127).
/// Contains all stats, equipment, feats, and spells for a given class at a specific level.
/// Used by QuickSpawnSystem to quickly generate NPCs for encounters.
/// </summary>
[System.Serializable]
public class NPCTemplate
{
    // ── Identity ──
    public string ClassName;
    public int Level;
    public int ChallengeRating;
    public string Race = "Human";
    public string Alignment = "True Neutral";

    // ── Ability Scores ──
    public int Strength;
    public int Dexterity;
    public int Constitution;
    public int Intelligence;
    public int Wisdom;
    public int Charisma;

    // ── Combat Stats ──
    public int HitPoints;
    public int ArmorClass;
    public int TouchAC;
    public int FlatFootedAC;
    public int Initiative;
    public int BaseAttackBonus;
    public int BaseSpeed = 30;

    // ── Saving Throws ──
    public int Fortitude;
    public int Reflex;
    public int Will;

    // ── Attacks ──
    public List<AttackTemplate> MeleeAttacks = new List<AttackTemplate>();
    public List<AttackTemplate> RangedAttacks = new List<AttackTemplate>();

    // ── Skills ──
    public Dictionary<string, int> Skills = new Dictionary<string, int>();

    // ── Feats ──
    public List<string> Feats = new List<string>();

    // ── Equipment ──
    public List<EquipmentItem> Equipment = new List<EquipmentItem>();
    public int TotalWealthGP;

    // ── Spellcasting (for caster classes) ──
    public SpellcastingTemplate Spellcasting;

    // ── Class Features ──
    public List<string> ClassFeatures = new List<string>();

    /// <summary>Unique key for template lookup: "ClassName_Level".</summary>
    public string Key => $"{ClassName}_{Level}";

    /// <summary>
    /// Get a display summary for logging/debugging.
    /// </summary>
    public string GetSummary()
    {
        return $"{Race} {ClassName} L{Level} (CR {ChallengeRating}) — " +
               $"HP {HitPoints}, AC {ArmorClass}, BAB +{BaseAttackBonus} | " +
               $"STR {Strength} DEX {Dexterity} CON {Constitution} INT {Intelligence} WIS {Wisdom} CHA {Charisma} | " +
               $"Feats: {Feats.Count}, Equipment: {Equipment.Count}, Wealth: {TotalWealthGP}gp";
    }
}

/// <summary>
/// A weapon attack entry in an NPC template.
/// </summary>
[System.Serializable]
public class AttackTemplate
{
    public string WeaponName;
    public int AttackBonus;
    public string Damage; // e.g., "1d8+5"
    public int CriticalRange = 20; // Threat range (e.g., 19 for 19-20)
    public int CriticalMultiplier = 2;

    public string GetDisplayString()
    {
        string critStr = CriticalRange < 20
            ? $"{CriticalRange}-20/x{CriticalMultiplier}"
            : (CriticalMultiplier > 2 ? $"x{CriticalMultiplier}" : "");
        return $"{WeaponName} +{AttackBonus} ({Damage}{(critStr.Length > 0 ? ", " + critStr : "")})";
    }
}

/// <summary>
/// An equipment item in an NPC template.
/// </summary>
[System.Serializable]
public class EquipmentItem
{
    public string ItemName;
    public int ValueGP;
    public bool IsMagical;
    public string MagicProperties = "";

    public EquipmentItem() { }
    public EquipmentItem(string name, int value, bool magical = false, string props = "")
    {
        ItemName = name;
        ValueGP = value;
        IsMagical = magical;
        MagicProperties = props;
    }
}

/// <summary>
/// Spellcasting data for a caster NPC template.
/// </summary>
[System.Serializable]
public class SpellcastingTemplate
{
    public string SpellcastingType = "Prepared"; // "Prepared" or "Spontaneous"
    public string AbilityScore = "Intelligence"; // "Intelligence", "Wisdom", or "Charisma"
    public int CasterLevel;
    public Dictionary<int, List<string>> SpellsPrepared = new Dictionary<int, List<string>>();
    public Dictionary<int, int> SpellsPerDay = new Dictionary<int, int>();

    /// <summary>Get total prepared spells across all levels.</summary>
    public int TotalPreparedSpells
    {
        get
        {
            int total = 0;
            foreach (var kvp in SpellsPrepared)
                total += kvp.Value.Count;
            return total;
        }
    }
}
