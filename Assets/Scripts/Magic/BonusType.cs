/// <summary>
/// D&D 3.5e bonus types from the Player's Handbook (PHB p.21, p.177).
/// Used to enforce stacking rules: bonuses of the same type to the same
/// statistic do NOT stack (only the highest applies), with exceptions
/// for Dodge, Untyped, and Luck (per house rule).
/// </summary>
public enum BonusType
{
    /// <summary>No typed bonus — always stacks with everything.</summary>
    Untyped = 0,

    /// <summary>Alchemical bonus (e.g., alchemical items).</summary>
    Alchemical,

    /// <summary>Armor bonus to AC (e.g., Mage Armor, actual armor). Does not stack with other armor bonuses.</summary>
    Armor,

    /// <summary>Circumstance bonus — always stacks (even with itself).</summary>
    Circumstance,

    /// <summary>Competence bonus (e.g., Guidance cantrip).</summary>
    Competence,

    /// <summary>Deflection bonus to AC (e.g., Shield of Faith, Ring of Protection).</summary>
    Deflection,

    /// <summary>Dodge bonus to AC — always stacks (even with itself).</summary>
    Dodge,

    /// <summary>Enhancement bonus (e.g., Bull's Strength, magic weapon enhancement).</summary>
    Enhancement,

    /// <summary>Insight bonus (e.g., True Strike).</summary>
    Insight,

    /// <summary>Luck bonus (e.g., Divine Favor). House rule: stacks like dodge.</summary>
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

    /// <summary>See Invisibility effect.</summary>
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
/// Helper class for D&D 3.5e bonus type stacking rules.
/// 
/// Standard Rules (PHB p.177):
///   - Most bonuses of the same type do NOT stack (only highest applies).
///   - Dodge bonuses always stack.
///   - Circumstance bonuses always stack.
///   - Untyped bonuses always stack.
///   
/// House Rules applied:
///   - Luck bonuses stack (per user request — non-standard but requested).
/// </summary>
public static class BonusTypeHelper
{
    /// <summary>
    /// Determine whether a bonus type stacks with itself (i.e., multiple bonuses of the
    /// same type to the same stat are all applied rather than using only the highest).
    /// 
    /// Returns true for: Dodge, Untyped, Circumstance, Luck (house rule).
    /// Returns false for all other types.
    /// </summary>
    public static bool DoesStack(BonusType type)
    {
        switch (type)
        {
            case BonusType.Dodge:
            case BonusType.Untyped:
            case BonusType.Circumstance:
            case BonusType.Luck:  // House rule: luck bonuses stack
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
            case "shield": return BonusType.Shield;
            case "size": return BonusType.Size;
            case "protection": return BonusType.Protection;
            case "entropic": return BonusType.Entropic;
            case "concealment": return BonusType.Concealment;
            case "sanctuary": return BonusType.Sanctuary;
            case "enlarge": return BonusType.Enlarge;
            case "reduce": return BonusType.Reduce;
            case "mirror_image": return BonusType.MirrorImage;
            case "dr_arrows": return BonusType.DRArrows;
            case "energy_resistance": return BonusType.EnergyResistance;
            case "temp_hp": return BonusType.TempHP;
            case "spectral_hand": return BonusType.SpectralHand;
            case "levitate": return BonusType.Levitate;
            case "see_invis": return BonusType.SeeInvisibility;
            case "spider_climb": return BonusType.SpiderClimb;
            case "speed": return BonusType.Speed;
            case "shield_other": return BonusType.ShieldOther;
            case "protection_alignment": return BonusType.ProtectionAlignment;
            case "invisibility": return BonusType.Invisibility;
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
            case BonusType.SeeInvisibility: return "See Invisibility";
            case BonusType.SpiderClimb: return "Spider Climb";
            case BonusType.MirrorImage: return "Mirror Image";
            case BonusType.ShieldOther: return "Shield Other";
            case BonusType.ProtectionAlignment: return "Protection (Alignment)";
            default: return type.ToString();
        }
    }
}
