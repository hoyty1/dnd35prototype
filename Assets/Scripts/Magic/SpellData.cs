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


    // ========== TOUCH ATTACK METADATA ==========
    /// <summary>Whether this spell is a touch spell (used for held charge and targeting UX).</summary>
    public bool IsTouch;
    /// <summary>Whether this spell uses a melee touch attack (vs touch AC) when applicable.</summary>
    public bool IsMeleeTouch;
    /// <summary>Whether this spell uses a ranged touch attack (vs touch AC) when applicable.</summary>
    public bool IsRangedTouch;

    /// <summary>
    /// Returns true if this spell should be treated as a touch spell.
    /// Falls back to range/target heuristics for backward compatibility.
    /// </summary>
    public bool IsTouchSpell()
    {
        if (IsTouch || IsMeleeTouch || IsRangedTouch) return true;
        return TargetType == SpellTargetType.Touch || RangeSquares == 1;
    }

    /// <summary>
    /// Returns true if this spell should use a melee touch attack roll.
    /// Falls back to simple range heuristic for backward compatibility.
    /// </summary>
    public bool IsMeleeTouchSpell()
    {
        if (IsMeleeTouch) return true;
        if (IsRangedTouch) return false;
        return IsTouchSpell() && RangeSquares <= 1;
    }

    /// <summary>
    /// Returns true if this spell should use a ranged touch attack roll.
    /// Falls back to simple range heuristic for backward compatibility.
    /// </summary>
    public bool IsRangedTouchSpell()
    {
        if (IsRangedTouch) return true;
        if (IsMeleeTouch) return false;
        return IsTouchSpell() && RangeSquares > 1;
    }
    // ========== AREA OF EFFECT ==========
    /// <summary>Shape of the AoE (None = single target, Burst = radius, Cone = emanation from caster).</summary>
    public AoEShape AoEShapeType;
    /// <summary>Size of the AoE in grid squares. For Burst: radius. For Cone: length.</summary>
    public int AoESizeSquares;
    /// <summary>How far the AoE origin can be placed from the caster (in squares). For cones, this is 0 (originates from caster).</summary>
    public int AoERangeSquares;
    /// <summary>Who is affected by the AoE (All, AlliesOnly, EnemiesOnly).</summary>
    public AoETargetFilter AoEFilter;

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
    public int BuffDurationRounds;      // Duration in rounds (0 = instantaneous, -1 = hours/level) [LEGACY - prefer DurationType system]
    public string BuffType;             // LEGACY: "armor", "shield", "morale", etc. — use BuffBonusType enum instead

    /// <summary>
    /// D&D 3.5e bonus type enum for proper stacking rule enforcement.
    /// Preferred over the legacy string BuffType field.
    /// If not explicitly set (Untyped), falls back to parsing BuffType string.
    /// </summary>
    public BonusType BuffBonusType;

    /// <summary>Whether BuffBonusType was explicitly set (vs. defaulting to Untyped).</summary>
    public bool BonusTypeExplicitlySet;

    /// <summary>
    /// Get the effective BonusType for this spell. If BuffBonusType was explicitly set, use it.
    /// Otherwise, parse the legacy BuffType string for backward compatibility.
    /// </summary>
    public BonusType GetEffectiveBonusType()
    {
        if (BonusTypeExplicitlySet) return BuffBonusType;
        return BonusTypeHelper.FromString(BuffType);
    }

    // ========== DURATION SYSTEM (D&D 3.5e) ==========
    /// <summary>How the spell's duration is measured (Instantaneous, Rounds, Minutes, Hours, Permanent, Concentration).</summary>
    public DurationType DurationType;
    /// <summary>Base duration value (e.g., 1 for "1 min/level", 3 for "3 rounds").</summary>
    public int DurationValue;
    /// <summary>Whether duration scales with caster level (e.g., "1 min/level" = true, "3 rounds" = false).</summary>
    public bool DurationScalesWithLevel;

    // ========== HEALING ==========
    public int HealDice;                // Sides of healing die
    public int HealCount;               // Number of dice
    public int BonusHealing;            // Flat bonus healing (e.g., caster level for Cure spells)

    // ========== CASTING ==========
    public SpellActionType ActionType;  // Standard, FullRound, Swift, Free
    public bool ProvokesAoO;            // Most spells provoke AoO (true by default)
    public bool HasVerbalComponent = true; // Most spells include verbal components unless explicitly overridden.
    public bool HasSomaticComponent = true; // Most spells include somatic components unless explicitly overridden.
    // ========== SPECIAL ==========
    /// <summary>Number of missiles for Magic Missile (1 at CL1, +1 per 2 CL above 1).</summary>
    public int MissileCount;

    // ========== PLACEHOLDER & BUFF DETAIL ==========
    /// <summary>Whether this spell's mechanics are not yet fully implemented.</summary>
    public bool IsPlaceholder;
    /// <summary>Reason/description for placeholder status (e.g., "[PLACEHOLDER - Summoning not implemented]").</summary>
    public string PlaceholderReason;

    // ========== STAT BUFF DETAILS ==========
    /// <summary>Stat to buff (e.g., "STR", "DEX", "CON", "attack", "saves").</summary>
    public string BuffStatName;
    /// <summary>Stat bonus amount (e.g., +4 for Bull's Strength enhancement bonus to STR).</summary>
    public int BuffStatBonus;
    /// <summary>Shield bonus to AC (separate from armor for stacking).</summary>
    public int BuffShieldBonus;
    /// <summary>Deflection bonus to AC.</summary>
    public int BuffDeflectionBonus;
    /// <summary>Temporary HP granted.</summary>
    public int BuffTempHP;
    /// <summary>Attack bonus (morale, luck, etc.).</summary>
    public int BuffAttackBonus;
    /// <summary>Damage bonus (morale, luck, etc.).</summary>
    public int BuffDamageBonus;
    /// <summary>Save bonus (morale, luck, etc.).</summary>
    public int BuffSaveBonus;


    // ========== ADVANCED MITIGATION BUFFS ==========
    /// <summary>Typed damage resistance amount granted by this spell (e.g., Resist Fire 10).</summary>
    public int BuffDamageResistanceAmount;
    /// <summary>Damage type for BuffDamageResistanceAmount.</summary>
    public global::DamageType BuffDamageResistanceType = global::DamageType.Untyped;

    /// <summary>Typed immunity granted by this spell.</summary>
    public global::DamageType BuffDamageImmunityType = global::DamageType.Untyped;

    /// <summary>Damage reduction amount granted by this spell (e.g., DR 10/magic).</summary>
    public int BuffDamageReductionAmount;
    /// <summary>Bypass tags for the granted DR.</summary>
    public DamageBypassTag BuffDamageReductionBypass = DamageBypassTag.None;
    /// <summary>If true, granted DR applies only against ranged weapon attacks.</summary>
    public bool BuffDamageReductionRangedOnly;
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

        // AoE info
        string aoeStr = "";
        if (AoEShapeType == AoEShape.Burst)
            aoeStr = $" | AoE: {AoESizeSquares * 5}-ft burst";
        else if (AoEShapeType == AoEShape.Cone)
            aoeStr = $" | AoE: {AoESizeSquares * 5}-ft cone";
        else if (AoEShapeType == AoEShape.Line)
            aoeStr = $" | AoE: {AoESizeSquares * 5}-ft line";

        // Duration info
        string durStr = "";
        if (DurationType != DurationType.Instantaneous && DurationValue > 0)
        {
            string unit = DurationType == DurationType.Rounds ? "rd" :
                          DurationType == DurationType.Minutes ? "min" :
                          DurationType == DurationType.Hours ? "hr" : "";
            durStr = $" | Dur: {DurationValue}{unit}";
            if (DurationScalesWithLevel) durStr += "/lvl";
        }
        else if (DurationType == DurationType.Permanent)
        {
            durStr = " | Dur: Permanent";
        }
        else if (DurationType == DurationType.Concentration)
        {
            durStr = " | Dur: Concentration";
        }

        string placeholderStr = IsPlaceholder ? " <color=#FF8800>[PLACEHOLDER]</color>" : "";
        return $"[{levelStr}] {Name} ({School}){placeholderStr}\n{effectStr} | Range: {rangeStr}{aoeStr}{durStr}";
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