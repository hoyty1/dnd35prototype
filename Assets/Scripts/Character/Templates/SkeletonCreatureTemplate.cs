/// <summary>
/// ICreatureTemplate wrapper around <see cref="SkeletonTemplate"/>.
/// Allows skeleton creation to be driven by the CreatureTemplateRegistry via
/// AppliedTemplateIds (e.g., a "Fiendish Skeleton" creature definition).
///
/// The underlying SkeletonTemplate.Apply() returns a new clone, so this wrapper
/// applies in-place by delegating to Apply() then copying the result's fields
/// back onto the passed definition.  Because ApplyTemplatesClone already cloned
/// the source, this is safe.
/// </summary>
public sealed class SkeletonCreatureTemplate : ICreatureTemplate
{
    public string TemplateId => "skeleton";

    /// <summary>Undead templates radically change the creature type — apply first.</summary>
    public int ApplicationOrder => 10;

    public void ApplyToDefinition(NPCDefinition definition)
    {
        if (definition == null)
            return;

        // SkeletonTemplate.Apply clones internally, so we apply and copy back
        NPCDefinition result = SkeletonTemplate.Apply(
            definition,
            definition.Id,
            definition.Name != null ? definition.Name + " Skeleton" : "Skeleton",
            hasHands: true);

        if (result == null)
            return;

        CopyDefinitionFields(result, definition);
    }

    /// <summary>
    /// Copies all gameplay-relevant fields from source to target in-place.
    /// This bridges the gap between SkeletonTemplate.Apply (which returns a new object)
    /// and ICreatureTemplate.ApplyToDefinition (which mutates in place).
    /// </summary>
    private static void CopyDefinitionFields(NPCDefinition source, NPCDefinition target)
    {
        // Identity — preserve original Id but update Name
        target.Name = source.Name;

        // Core stats
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
        target.MaterialComposition = source.MaterialComposition;

        // Defenses
        target.DamageReductionAmount = source.DamageReductionAmount;
        target.DamageReductionBypass = source.DamageReductionBypass;
        target.Immunities = source.Immunities;
        target.IsMindless = source.IsMindless;

        // Attacks
        target.NaturalAttacks = source.NaturalAttacks;

        // Cleared capabilities
        target.HasImprovedGrab = source.HasImprovedGrab;
        target.ImprovedGrabTriggerAttackName = source.ImprovedGrabTriggerAttackName;
        target.KnownSpellIds = source.KnownSpellIds;
        target.PreparedSpellSlotIds = source.PreparedSpellSlotIds;
        target.SpellResistance = source.SpellResistance;

        // Display
        target.SpecialAbilities = source.SpecialAbilities;
        target.CreatureTags = source.CreatureTags;
        target.AppliedTemplateIds = source.AppliedTemplateIds;
        target.ChallengeRating = source.ChallengeRating;

        // Feats (skeleton clears these)
        target.Feats = source.Feats;
    }
}
