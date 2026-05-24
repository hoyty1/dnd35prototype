/// <summary>
/// Abstract ICreatureTemplate base for lycanthrope templates.
/// Each concrete subclass represents a specific lycanthrope type (werewolf, wererat, etc.)
/// and carries the correct animal form, alignment, and template ID.
///
/// Delegates to <see cref="LycanthropeTemplate.Apply"/> for the actual stat-block mutation,
/// then copies result fields back in-place (same pattern as Skeleton/Zombie wrappers).
///
/// NOTE: This is the Phase A-B partial integration only.
/// Multi-form, curse progression, disease expansion, and form shifting are NOT implemented.
/// </summary>
public abstract class LycanthropeTemplateBase : ICreatureTemplate
{
    public abstract string TemplateId { get; }

    /// <summary>Lycanthrope templates merge two creatures — applied after undead, before outsider.</summary>
    public int ApplicationOrder => 15;

    /// <summary>The animal type for this lycanthrope variant.</summary>
    protected abstract LycanthropeAnimalType AnimalType { get; }

    /// <summary>The canonical D&amp;D 3.5e alignment for this lycanthrope type.</summary>
    protected abstract Alignment LycanthropeAlignment { get; }

    /// <summary>Whether this is a natural (true) or afflicted lycanthrope. Defaults to natural.</summary>
    protected virtual bool IsNatural => true;

    public void ApplyToDefinition(NPCDefinition definition)
    {
        if (definition == null)
            return;

        NPCDefinition result = LycanthropeTemplate.Apply(
            definition,
            AnimalType,
            IsNatural,
            definition.Id,
            definition.Name);

        if (result == null)
            return;

        // Enforce canonical alignment for this lycanthrope type
        result.CharacterAlignment = LycanthropeAlignment;

        CopyDefinitionFields(result, definition);
    }

    /// <summary>
    /// Copies gameplay-relevant fields from the LycanthropeTemplate.Apply result back in-place.
    /// </summary>
    private static void CopyDefinitionFields(NPCDefinition source, NPCDefinition target)
    {
        target.Name = source.Name;

        // Core stats (lycanthrope hybrid form modifies STR, DEX, CON, WIS)
        target.STR = source.STR;
        target.DEX = source.DEX;
        target.CON = source.CON;
        target.INT = source.INT;
        target.WIS = source.WIS;
        target.CHA = source.CHA;

        // Combat
        target.HitDice = source.HitDice;
        target.BAB = source.BAB;
        target.BABOverride = source.BABOverride;
        target.BaseAttackBonusOverride = source.BaseAttackBonusOverride;
        target.FortitudeSaveOverride = source.FortitudeSaveOverride;
        target.ReflexSaveOverride = source.ReflexSaveOverride;
        target.WillSaveOverride = source.WillSaveOverride;
        target.BaseHitDieHP = source.BaseHitDieHP;

        // Type and properties
        target.CreatureType = source.CreatureType;
        target.NaturalArmorBonus = source.NaturalArmorBonus;
        target.SizeCategory = source.SizeCategory;
        target.BaseSpeed = source.BaseSpeed;

        // Alignment
        target.CharacterAlignment = source.CharacterAlignment;

        // Defenses
        target.DamageReductionAmount = source.DamageReductionAmount;
        target.DamageReductionBypass = source.DamageReductionBypass;
        target.Immunities = source.Immunities;

        // Attacks
        target.NaturalAttacks = source.NaturalAttacks;

        // Special abilities
        target.HasImprovedGrab = source.HasImprovedGrab;
        target.ImprovedGrabTriggerAttackName = source.ImprovedGrabTriggerAttackName;
        target.HasPounce = source.HasPounce;
        target.HasRake = source.HasRake;
        target.RakeAttack = source.RakeAttack;
        target.HasScent = source.HasScent;
        target.HasTripAttack = source.HasTripAttack;
        target.TripAttackCheckBonus = source.TripAttackCheckBonus;

        // Display
        target.SpecialAbilities = source.SpecialAbilities;
        target.CreatureTags = source.CreatureTags;
        target.AppliedTemplateIds = source.AppliedTemplateIds;
        target.ChallengeRating = source.ChallengeRating;

        // Feats
        target.Feats = source.Feats;
    }
}

// ─────────────────────────────────────────────────────────────
//  Per-animal lycanthrope template classes
//  Each enforces the correct MM 3.5e alignment for that type.
// ─────────────────────────────────────────────────────────────

/// <summary>Werewolf template — Wolf animal form, Chaotic Evil alignment.</summary>
public sealed class WerewolfCreatureTemplate : LycanthropeTemplateBase
{
    public override string TemplateId => "werewolf";
    protected override LycanthropeAnimalType AnimalType => LycanthropeAnimalType.Wolf;
    protected override Alignment LycanthropeAlignment => Alignment.ChaoticEvil;
}

/// <summary>Wererat template — Rat (Dire Rat) animal form, Lawful Evil alignment.</summary>
public sealed class WereratCreatureTemplate : LycanthropeTemplateBase
{
    public override string TemplateId => "wererat";
    protected override LycanthropeAnimalType AnimalType => LycanthropeAnimalType.Rat;
    protected override Alignment LycanthropeAlignment => Alignment.LawfulEvil;
}

/// <summary>Wereboar template — Boar animal form, Chaotic Neutral alignment.</summary>
public sealed class WereboarCreatureTemplate : LycanthropeTemplateBase
{
    public override string TemplateId => "wereboar";
    protected override LycanthropeAnimalType AnimalType => LycanthropeAnimalType.Boar;
    protected override Alignment LycanthropeAlignment => Alignment.ChaoticNeutral;
}

/// <summary>Weretiger template — Tiger animal form, True Neutral alignment.</summary>
public sealed class WeretigerCreatureTemplate : LycanthropeTemplateBase
{
    public override string TemplateId => "weretiger";
    protected override LycanthropeAnimalType AnimalType => LycanthropeAnimalType.Tiger;
    protected override Alignment LycanthropeAlignment => Alignment.TrueNeutral;
}

/// <summary>Werebear template — Brown Bear animal form, Lawful Good alignment.</summary>
public sealed class WerebearCreatureTemplate : LycanthropeTemplateBase
{
    public override string TemplateId => "werebear";
    protected override LycanthropeAnimalType AnimalType => LycanthropeAnimalType.BrownBear;
    protected override Alignment LycanthropeAlignment => Alignment.LawfulGood;
}
