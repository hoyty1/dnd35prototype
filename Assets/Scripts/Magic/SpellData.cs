using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class SpellAvailability
{
    public string ClassName;
    public int Level;
    public string Domain;

    public SpellAvailability(string className, int level, string domain = null)
    {
        ClassName = className;
        Level = level;
        Domain = domain;
    }

    public bool MatchesClass(string className)
    {
        return !string.IsNullOrWhiteSpace(className) &&
               string.Equals(ClassName, className, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesDomain(string domain)
    {
        return !string.IsNullOrWhiteSpace(domain) &&
               !string.IsNullOrWhiteSpace(Domain) &&
               string.Equals(Domain, domain, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Defines a spell's properties according to D&D 3.5e rules.
/// Analogous to ItemData for weapons/armor.
/// </summary>
[Serializable]
public class SpellData
{
    // ========== IDENTITY ==========
    public string SpellId;          // Unique key (e.g., "magic_missile")
    public string Name;             // Display name (e.g., "Magic Missile")
    public string Description;      // Short description
    public int SpellLevel;          // 0 = cantrip, 1 = 1st level, etc.
    public string School;           // Evocation, Conjuration, Necromancy, Abjuration, etc.

    // ========== CLASSES ==========
    /// <summary>Legacy class list field (kept for backward compatibility with existing spell declarations).</summary>
    public string[] ClassList;

    /// <summary>
    /// Availability metadata for class/domain spell list membership.
    /// A spell may have multiple entries (e.g., Cleric 1 + Healing domain 1).
    /// </summary>
    public List<SpellAvailability> AvailableFor = new List<SpellAvailability>();

    public void AddAvailability(string className, int level, string domain = null)
    {
        if (string.IsNullOrWhiteSpace(className))
            return;

        bool exists = AvailableFor.Any(a =>
            a != null &&
            a.MatchesClass(className) &&
            a.Level == level &&
            (string.IsNullOrWhiteSpace(domain)
                ? string.IsNullOrWhiteSpace(a.Domain)
                : a.MatchesDomain(domain)));

        if (!exists)
            AvailableFor.Add(new SpellAvailability(className, level, domain));
    }

    public void EnsureAvailabilityFromLegacyClassList()
    {
        if (ClassList == null || ClassList.Length == 0)
            return;

        for (int i = 0; i < ClassList.Length; i++)
        {
            AddAvailability(ClassList[i], SpellLevel);
        }
    }

    public bool IsAvailableFor(string className, int spellLevel)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;

        if (AvailableFor != null && AvailableFor.Count > 0)
        {
            return AvailableFor.Any(a =>
                a != null &&
                a.MatchesClass(className) &&
                string.IsNullOrWhiteSpace(a.Domain) &&
                a.Level == spellLevel);
        }

        return SpellLevel == spellLevel &&
               ClassList != null &&
               ClassList.Any(cls => string.Equals(cls, className, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAvailableForDomain(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName) || AvailableFor == null)
            return false;

        return AvailableFor.Any(a => a != null && a.MatchesDomain(domainName));
    }

    public int GetSpellLevelFor(string className, string domain = null)
    {
        if (string.IsNullOrWhiteSpace(className))
            return -1;

        if (AvailableFor != null && AvailableFor.Count > 0)
        {
            SpellAvailability availability;
            if (string.IsNullOrWhiteSpace(domain))
            {
                availability = AvailableFor
                    .Where(a => a != null && a.MatchesClass(className) && string.IsNullOrWhiteSpace(a.Domain))
                    .OrderBy(a => a.Level)
                    .FirstOrDefault();
            }
            else
            {
                availability = AvailableFor
                    .Where(a => a != null && a.MatchesClass(className) && a.MatchesDomain(domain))
                    .OrderBy(a => a.Level)
                    .FirstOrDefault();
            }

            if (availability != null)
                return availability.Level;
        }

        if (ClassList != null && ClassList.Any(cls => string.Equals(cls, className, StringComparison.OrdinalIgnoreCase)))
            return SpellLevel;

        return -1;
    }

    // ========== TARGETING ==========
    public SpellTargetType TargetType;  // SingleEnemy, SingleAlly, Self, Area

    /// <summary>
    /// Primary spell range definition. Use standard D&D 3.5e categories where possible.
    /// If set to a non-custom category, legacy range fields are auto-synchronized.
    /// </summary>
    private SpellRangeCategory _rangeCategory = SpellRangeCategory.Custom;
    public SpellRangeCategory RangeCategory
    {
        get => _rangeCategory;
        set
        {
            _rangeCategory = value;
            if (_rangeCategory != SpellRangeCategory.Custom)
            {
                SpellRanges.Configure(this, _rangeCategory);
            }
        }
    }

    // Legacy/manual range fields retained for backward compatibility and custom formulas.
    public int RangeSquares;            // Base range in squares (-1 = self, 1 = touch)
    public int RangeIncreasePerLevels;  // Optional scaling: +RangeIncreaseSquares per N caster levels (round down)
    public int RangeIncreaseSquares;    // Optional scaling amount in squares
    public int AreaRadius;              // For area spells, radius in squares (0 = single target)

    /// <summary>
    /// Configure this spell with one of the standard D&D 3.5e range categories.
    /// </summary>
    public void SetRange(SpellRangeCategory range)
    {
        RangeCategory = range;
    }

    public void SetRangeClose() => SetRange(SpellRangeCategory.Close);
    public void SetRangeMedium() => SetRange(SpellRangeCategory.Medium);
    public void SetRangeLong() => SetRange(SpellRangeCategory.Long);

    /// <summary>
    /// Convenience factory for creating a SpellData instance preconfigured
    /// with a standard range category.
    /// </summary>
    public static SpellData CreateWithRange(SpellRangeCategory range)
    {
        return new SpellData { RangeCategory = range };
    }

    /// <summary>
    /// Returns the effective range category (explicit category if set; otherwise auto-detected from legacy values).
    /// </summary>
    public SpellRangeCategory GetEffectiveRangeCategory()
    {
        if (RangeCategory != SpellRangeCategory.Custom)
            return RangeCategory;

        return SpellRanges.TryDetectCategory(RangeSquares, RangeIncreasePerLevels, RangeIncreaseSquares, out var detected)
            ? detected
            : SpellRangeCategory.Custom;
    }

    private void GetEffectiveRangeProfile(out int baseSquares, out int increasePerLevels, out int increaseSquares)
    {
        var category = GetEffectiveRangeCategory();
        if (SpellRanges.TryGetStandardRangeProfile(category, out baseSquares, out increasePerLevels, out increaseSquares))
            return;

        baseSquares = RangeSquares;
        increasePerLevels = RangeIncreasePerLevels;
        increaseSquares = RangeIncreaseSquares;
    }

    private int GetEffectiveBaseRangeSquares()
    {
        GetEffectiveRangeProfile(out int baseSquares, out _, out _);
        return baseSquares;
    }

    private int GetEffectiveRangeIncreasePerLevels()
    {
        GetEffectiveRangeProfile(out _, out int increasePerLevels, out _);
        return increasePerLevels;
    }

    private int GetEffectiveRangeIncreaseSquares()
    {
        GetEffectiveRangeProfile(out _, out _, out int increaseSquares);
        return increaseSquares;
    }

    /// <summary>
    /// Returns effective range in squares for a given caster level.
    /// Scaling uses integer division (round down), e.g. 5 + floor(level/2).
    /// </summary>
    public int GetRangeSquaresForCasterLevel(int casterLevel)
    {
        int baseRange = GetEffectiveBaseRangeSquares();

        // Preserve sentinel values and touch/self behavior.
        if (baseRange <= 0) return baseRange;

        int increasePerLevels = GetEffectiveRangeIncreasePerLevels();
        int increaseSquares = GetEffectiveRangeIncreaseSquares();

        if (increasePerLevels <= 0 || increaseSquares <= 0 || casterLevel <= 0)
            return baseRange;

        int increments = casterLevel / increasePerLevels;
        return baseRange + (increments * increaseSquares);
    }


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
        return TargetType == SpellTargetType.Touch || GetEffectiveBaseRangeSquares() == 1;
    }

    /// <summary>
    /// Returns true if this spell should use a melee touch attack roll.
    /// Falls back to simple range heuristic for backward compatibility.
    /// </summary>
    public bool IsMeleeTouchSpell()
    {
        if (IsMeleeTouch) return true;
        if (IsRangedTouch) return false;
        return IsTouchSpell() && GetEffectiveBaseRangeSquares() <= 1;
    }

    /// <summary>
    /// Returns true if this spell should use a ranged touch attack roll.
    /// Falls back to simple range heuristic for backward compatibility.
    /// </summary>
    public bool IsRangedTouchSpell()
    {
        if (IsRangedTouch) return true;
        if (IsMeleeTouch) return false;
        return IsTouchSpell() && GetEffectiveBaseRangeSquares() > 1;
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
    public bool SpellResistanceApplies; // True if target SR can negate the spell on a caster-level check

    /// <summary>
    /// True for [Mind-Affecting] spells/effects.
    /// Use this for broad mind-affecting mechanics (for example: undead/construct immunity).
    /// </summary>
    public bool IsMindAffecting;

    /// <summary>
    /// Is this spell blocked by Protection from [Alignment] spells?
    /// This is a subset of mind-affecting spells - specifically those that
    /// involve mental control/compulsion that Protection spells ward against.
    ///
    /// Per D&D 3.5e: Protection from Alignment blocks mental control, which includes
    /// charm, compulsion, and domination effects, but not all mind-affecting spells.
    ///
    /// Examples:
    ///   YES (Blocked): Charm Person, Command, Dominate Person, Suggestion
    ///   NO (Not Blocked): Fear effects, morale bonuses, some compulsions
    ///
    /// This flag should be curated per spell based on D&D 3.5e rules and playtesting.
    /// </summary>
    public bool BlockedByProtectionFromAlignment;

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
    /// <summary>Whether the caster can dismiss the spell early as a standard action.</summary>
    public bool IsDismissible;

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

    /// <summary>Land speed bonus in feet (e.g., Expeditious Retreat +30 ft enhancement).</summary>
    public int BuffSpeedBonusFeet;


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
        SpellData clone = (SpellData)this.MemberwiseClone();
        clone.AvailableFor = AvailableFor != null
            ? AvailableFor
                .Where(a => a != null)
                .Select(a => new SpellAvailability(a.ClassName, a.Level, a.Domain))
                .ToList()
            : new List<SpellAvailability>();
        return clone;
    }

    /// <summary>Get a formatted description for the spell list UI.</summary>
    public string GetShortDescription()
    {
        string levelStr = SpellLevel == 0 ? "Cantrip" : $"Level {SpellLevel}";

        int baseRangeSquares = GetEffectiveBaseRangeSquares();
        int rangeIncreasePerLevels = GetEffectiveRangeIncreasePerLevels();
        int rangeIncreaseSquares = GetEffectiveRangeIncreaseSquares();
        SpellRangeCategory effectiveRangeCategory = GetEffectiveRangeCategory();

        string rangeStr;
        if (effectiveRangeCategory == SpellRangeCategory.Unlimited)
        {
            rangeStr = "Unlimited";
        }
        else if (baseRangeSquares < 0)
        {
            rangeStr = "Self";
        }
        else if (effectiveRangeCategory == SpellRangeCategory.Touch || baseRangeSquares == 0)
        {
            rangeStr = "Touch";
        }
        else if (rangeIncreasePerLevels > 0 && rangeIncreaseSquares > 0)
        {
            rangeStr = $"{baseRangeSquares} sq + {rangeIncreaseSquares} sq/{rangeIncreasePerLevels} lv";
        }
        else
        {
            rangeStr = $"{baseRangeSquares} sq ({baseRangeSquares * 5} ft)";
        }

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
            else if (BuffSpeedBonusFeet > 0)
                effectStr = $"+{BuffSpeedBonusFeet} ft speed";
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