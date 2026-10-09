/// <summary>
/// D&D 3.5e bonus types (PHB p.171 "Bonus Types"; DMG p.21 "Bonus Types"; PHB glossary).
/// Used to enforce stacking rules: bonuses of the same type to the same statistic do NOT stack (only the highest
/// applies), except dodge bonuses, circumstance bonuses from different circumstances and untyped bonuses from
/// different sources (<see cref="BonusTypeHelper.DoesStack"/>, <see cref="BonusStacking"/>).
/// </summary>
using DND35e.Identifiers;
public enum BonusType
{
    /// <summary>No typed bonus: stacks with every bonus, except another from the same source (PHB p.172, glossary "stack" p.313).</summary>
    Untyped = 0,

    /// <summary>Alchemical bonus (e.g., alchemical items).</summary>
    Alchemical,

    /// <summary>Armor bonus to AC (e.g., Mage Armor, actual armor). Does not stack with other armor bonuses.</summary>
    Armor,

    /// <summary>Circumstance bonus: stacks with other circumstance bonuses unless they arise from essentially the same circumstance (PHB glossary p.306; DMG p.21).</summary>
    Circumstance,

    /// <summary>Competence bonus (e.g., Guidance cantrip).</summary>
    Competence,

    /// <summary>Deflection bonus to AC (e.g., Shield of Faith, Ring of Protection).</summary>
    Deflection,

    /// <summary>Dodge bonus to AC (and sometimes Reflex): stacks with other dodge bonuses (PHB glossary p.307).</summary>
    Dodge,

    /// <summary>Enhancement bonus (e.g., Bull's Strength, magic weapon enhancement).</summary>
    Enhancement,

    /// <summary>Insight bonus (e.g., True Strike).</summary>
    Insight,

    /// <summary>Luck bonus (e.g., Divine Favor, Prayer, a stone of good luck). Multiple luck bonuses do not stack; only the highest applies (PHB glossary p.310).</summary>
    Luck,

    /// <summary>Morale bonus (e.g., Bless, Good Hope).</summary>
    Morale,

    /// <summary>Natural armor bonus (e.g., Barkskin, Amulet of Natural Armor).</summary>
    NaturalArmor,

    /// <summary>Profane bonus (e.g., some evil spells/effects).</summary>
    Profane,

    /// <summary>Racial bonus (e.g., racial skill bonuses).</summary>
    Racial,

    /// <summary>Resistance bonus (e.g., Resistance cantrip, Cloak of Resistance).</summary>
    Resistance,

    /// <summary>Sacred bonus (e.g., some good-aligned spells).</summary>
    Sacred,

    /// <summary>Shield bonus to AC (e.g., Shield spell, actual shield).</summary>
    Shield,

    /// <summary>Size bonus/penalty (e.g., Enlarge Person, Reduce Person).</summary>
    Size,

    // ========== NON-STANDARD / SPELL-SPECIFIC TYPES ==========
    // These are used for spells whose effects don't fit standard bonus types
    // but still need stacking tracking (same spell doesn't stack with itself).

    /// <summary>Protection from alignment effects (e.g., Protection from Evil).</summary>
    Protection,

    /// <summary>Entropic effect (e.g., Entropic Shield — miss chance).</summary>
    Entropic,

    /// <summary>Concealment effect (e.g., Blur, Invisibility — miss chance).</summary>
    Concealment,

    /// <summary>Sanctuary effect (Will save to attack).</summary>
    Sanctuary,

    /// <summary>Enlarge/Reduce size-change effect.</summary>
    Enlarge,

    /// <summary>Reduce size-change effect.</summary>
    Reduce,

    /// <summary>Mirror Image effect (illusory duplicates).</summary>
    MirrorImage,

    /// <summary>Damage reduction vs arrows (e.g., Protection from Arrows).</summary>
    DRArrows,

    /// <summary>Energy resistance effect.</summary>
    EnergyResistance,

    /// <summary>Temporary HP (e.g., False Life, Aid).</summary>
    TempHP,

    /// <summary>Spectral Hand effect.</summary>
    SpectralHand,

    /// <summary>Levitate effect.</summary>
    Levitate,

    /// <summary>See Invisible effect.</summary>
    SeeInvisibility,

    /// <summary>Spider Climb effect.</summary>
    SpiderClimb,

    /// <summary>Speed enhancement (e.g., Expeditious Retreat, Longstrider).</summary>
    Speed,

    /// <summary>Shield Other effect (damage sharing).</summary>
    ShieldOther,

    /// <summary>Protection from alignment (domain version).</summary>
    ProtectionAlignment,

    /// <summary>Invisibility effect.</summary>
    Invisibility
}

/// <summary>
/// Helper class for D&D 3.5e bonus type stacking rules (PHB p.171-172, glossary p.305-313; DMG p.21):
///   - Bonuses of the same type do NOT stack (only the highest applies); penalties of the same type, only the worst.
///   - Dodge bonuses stack with each other.
///   - Circumstance bonuses stack unless they arise from the same circumstance (same source).
///   - Untyped bonuses stack unless they come from the same source.
///   - The same spell or effect never stacks with itself.
/// The totals are computed by <see cref="BonusStacking.Combine(System.Collections.Generic.IList{TypedBonus})"/>.
/// No house rule changes these (owner directive 2026-10-09: strict RAW).
/// </summary>
public static class BonusTypeHelper
{
    /// <summary>
    /// Whether bonuses of this type from DIFFERENT sources stack with each other (all are applied rather than only the
    /// highest). Bonuses from the same source never stack, whatever their type (<see cref="BonusStacking"/>).
    ///
    /// Returns true for Dodge, Untyped and Circumstance (PHB p.171-172; glossary "stack" p.313, "circumstance bonus"
    /// p.306, "dodge bonus" p.307). Returns false for every named type, luck included (glossary "luck bonus" p.310).
    /// Racial returns false (DMG p.21); PHB p.171 lists racial bonuses among those that stack, an open owner question
    /// (CHR-019).
    /// </summary>
    public static bool DoesStack(BonusType type)
    {
        switch (type)
        {
            case BonusType.Dodge:
            case BonusType.Untyped:
            case BonusType.Circumstance:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Determine if this is a "core" D&D bonus type that participates in standard
    /// stacking rules, vs. a spell-specific effect type.
    /// Core types enforce "only highest applies" (unless stackable).
    /// Spell-specific types (MirrorImage, Invisibility, etc.) only check same-spell stacking.
    /// </summary>
    public static bool IsCoreType(BonusType type)
    {
        switch (type)
        {
            case BonusType.Untyped:
            case BonusType.Alchemical:
            case BonusType.Armor:
            case BonusType.Circumstance:
            case BonusType.Competence:
            case BonusType.Deflection:
            case BonusType.Dodge:
            case BonusType.Enhancement:
            case BonusType.Insight:
            case BonusType.Luck:
            case BonusType.Morale:
            case BonusType.NaturalArmor:
            case BonusType.Profane:
            case BonusType.Racial:
            case BonusType.Resistance:
            case BonusType.Sacred:
            case BonusType.Shield:
            case BonusType.Size:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Convert a legacy string BuffType to the new BonusType enum.
    /// Used for backward compatibility during migration.
    /// </summary>
    public static BonusType FromString(string buffType)
    {
        if (string.IsNullOrEmpty(buffType)) return BonusType.Untyped;

        switch (buffType.ToLower().Trim())
        {
            case "untyped": return BonusType.Untyped;
            case "alchemical": return BonusType.Alchemical;
            case "armor": return BonusType.Armor;
            case "circumstance": return BonusType.Circumstance;
            case "competence": return BonusType.Competence;
            case "deflection": return BonusType.Deflection;
            case "dodge": return BonusType.Dodge;
            case "enhancement": return BonusType.Enhancement;
            case "insight": return BonusType.Insight;
            case "luck": return BonusType.Luck;
            case "morale": return BonusType.Morale;
            case "natural_armor": return BonusType.NaturalArmor;
            case "profane": return BonusType.Profane;
            case "racial": return BonusType.Racial;
            case "resistance": return BonusType.Resistance;
            case "sacred": return BonusType.Sacred;
            case SpellNames.SHIELD: return BonusType.Shield;
            case "size": return BonusType.Size;
            case "protection": return BonusType.Protection;
            case "entropic": return BonusType.Entropic;
            case "concealment": return BonusType.Concealment;
            case SpellNames.SANCTUARY: return BonusType.Sanctuary;
            case "enlarge": return BonusType.Enlarge;
            case "reduce": return BonusType.Reduce;
            case SpellNames.MIRROR_IMAGE: return BonusType.MirrorImage;
            case "dr_arrows": return BonusType.DRArrows;
            case "energy_resistance": return BonusType.EnergyResistance;
            case "temp_hp": return BonusType.TempHP;
            case SpellNames.SPECTRAL_HAND: return BonusType.SpectralHand;
            case SpellNames.LEVITATE: return BonusType.Levitate;
            case "see_invis": return BonusType.SeeInvisibility;
            case SpellNames.SPIDER_CLIMB: return BonusType.SpiderClimb;
            case "speed": return BonusType.Speed;
            case SpellNames.SHIELD_OTHER: return BonusType.ShieldOther;
            case "protection_alignment": return BonusType.ProtectionAlignment;
            case SpellNames.INVISIBILITY: return BonusType.Invisibility;
            default:
                UnityEngine.Debug.LogWarning($"[BonusType] Unknown BuffType string: '{buffType}', defaulting to Untyped");
                return BonusType.Untyped;
        }
    }

    /// <summary>
    /// Get a display-friendly name for a bonus type (e.g., "Enhancement", "Natural Armor").
    /// </summary>
    public static string GetDisplayName(BonusType type)
    {
        switch (type)
        {
            case BonusType.NaturalArmor: return "Natural Armor";
            case BonusType.DRArrows: return "DR/Arrows";
            case BonusType.EnergyResistance: return "Energy Resistance";
            case BonusType.TempHP: return "Temp HP";
            case BonusType.SpectralHand: return "Spectral Hand";
            case BonusType.SeeInvisibility: return "See Invisible";
            case BonusType.SpiderClimb: return "Spider Climb";
            case BonusType.MirrorImage: return "Mirror Image";
            case BonusType.ShieldOther: return "Shield Other";
            case BonusType.ProtectionAlignment: return "Protection (Alignment)";
            default: return type.ToString();
        }
    }
}
