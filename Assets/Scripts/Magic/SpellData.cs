using UnityEngine;

/// <summary>
/// Defines a spell's properties according to D&D 3.5e rules.
/// Analogous to ItemData for weapons/armor.
/// </summary>
[System.Serializable]
public class SpellData
{
    // ========== IDENTITY ==========
    public string SpellId;          // Unique key (e.g., "magic_missile")
    public string Name;             // Display name (e.g., "Magic Missile")
    public string Description;      // Short description
    public int SpellLevel;          // 0 = cantrip, 1 = 1st level, etc.
    public string School;           // Evocation, Conjuration, Necromancy, Abjuration, etc.

    // ========== CLASSES ==========
    /// <summary>Which classes can cast this spell ("Wizard", "Cleric", or both).</summary>
    public string[] ClassList;

    // ========== TARGETING ==========
    public SpellTargetType TargetType;  // SingleEnemy, SingleAlly, Self, Area
    public int RangeSquares;            // Range in squares (0 = touch, -1 = self)
    public int AreaRadius;              // For area spells, radius in squares (0 = single target)

    // ========== EFFECTS ==========
    public SpellEffectType EffectType;  // Damage, Healing, Buff, Debuff
    public int DamageDice;              // Sides of damage die (e.g., 6 for d6)
    public int DamageCount;             // Number of dice
    public int BonusDamage;             // Flat bonus (e.g., per missile for Magic Missile)
    public string DamageType;           // "fire", "cold", "acid", "force", "negative", "positive"
    public bool AutoHit;                // True for Magic Missile (no attack roll)
    public bool AllowsSavingThrow;      // Whether targets get a save
    public string SavingThrowType;      // "Reflex", "Will", "Fortitude"
    public int SaveDC;                  // 0 = computed (10 + spell level + casting mod)
    public bool SaveHalves;             // True if save halves damage (e.g., Acid Splash)

    // ========== BUFF/DEBUFF ==========
    public int BuffACBonus;             // AC bonus (Mage Armor = +4)
    public int BuffDurationRounds;      // Duration in rounds (0 = instantaneous, -1 = hours/level)
    public string BuffType;             // "armor", "shield", etc. (for stacking rules)

    // ========== HEALING ==========
    public int HealDice;                // Sides of healing die
    public int HealCount;               // Number of dice
    public int BonusHealing;            // Flat bonus healing (e.g., caster level for Cure spells)

    // ========== CASTING ==========
    public SpellActionType ActionType;  // Standard, FullRound, Swift, Free
    public bool ProvokesAoO;            // Most spells provoke AoO (true by default)

    // ========== SPECIAL ==========
    /// <summary>Number of missiles for Magic Missile (1 at CL1, +1 per 2 CL above 1).</summary>
    public int MissileCount;

    /// <summary>Clone this spell data for independent modification.</summary>
    public SpellData Clone()
    {
        return (SpellData)this.MemberwiseClone();
    }

    /// <summary>Get a formatted description for the spell list UI.</summary>
    public string GetShortDescription()
    {
        string levelStr = SpellLevel == 0 ? "Cantrip" : $"Level {SpellLevel}";
        string rangeStr = RangeSquares < 0 ? "Self" : RangeSquares == 0 ? "Touch" : $"{RangeSquares} sq ({RangeSquares * 5} ft)";

        string effectStr = "";
        if (EffectType == SpellEffectType.Damage)
        {
            if (AutoHit && MissileCount > 0)
                effectStr = $"{MissileCount}×(1d{DamageDice}+{BonusDamage}) {DamageType}";
            else
                effectStr = $"{DamageCount}d{DamageDice} {DamageType}";
        }
        else if (EffectType == SpellEffectType.Healing)
        {
            effectStr = $"Heals {HealCount}d{HealDice}+{BonusHealing}";
        }
        else if (EffectType == SpellEffectType.Buff)
        {
            if (BuffACBonus > 0)
                effectStr = $"+{BuffACBonus} AC ({BuffType})";
        }

        return $"[{levelStr}] {Name} ({School})\n{effectStr} | Range: {rangeStr}";
    }
}

/// <summary>Who or what the spell targets.</summary>
public enum SpellTargetType
{
    Self,           // Caster only (Mage Armor)
    SingleEnemy,    // One hostile (Ray of Frost, Magic Missile)
    SingleAlly,     // One friendly (Cure Light Wounds)
    Touch,          // Touch range, friendly or hostile
    Area            // Area of effect (future: Fireball)
}

/// <summary>What the spell does.</summary>
public enum SpellEffectType
{
    Damage,
    Healing,
    Buff,
    Debuff
}

/// <summary>Action type required to cast.</summary>
public enum SpellActionType
{
    Standard,       // Most spells
    FullRound,      // Some longer spells
    Swift,          // Quickened spells
    Free            // Very rare
}
