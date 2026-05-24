/// <summary>
/// ICreatureTemplate wrapper around <see cref="ZombieTemplate"/>.
/// Allows zombie creation to be driven by the CreatureTemplateRegistry via
/// AppliedTemplateIds (e.g., a "Celestial Zombie" creature definition).
///
/// The underlying ZombieTemplate.Apply() returns a new clone, so this wrapper
/// applies in-place by delegating to Apply() then copying the result's fields
/// back onto the passed definition.
/// </summary>
public sealed class ZombieCreatureTemplate : ICreatureTemplate
{
    public string TemplateId => "zombie";

    /// <summary>Undead templates radically change the creature type — apply first.</summary>
    public int ApplicationOrder => 10;

    public void ApplyToDefinition(NPCDefinition definition)
    {
        if (definition == null)
            return;

        NPCDefinition result = ZombieTemplate.Apply(
            definition,
            definition.Id,
            definition.Name != null ? definition.Name + " Zombie" : "Zombie");

        if (result == null)
            return;

        CopyDefinitionFields(result, definition);
    }

    /// <summary>
    /// Copies all gameplay-relevant fields from source to target in-place.
    /// Bridges ZombieTemplate.Apply (returns new object) with ICreatureTemplate (mutates in place).
    /// </summary>
    private static void CopyDefinitionFields(NPCDefinition source, NPCDefinition target)
    {
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

        // Defenses
        target.DamageReductionAmount = source.DamageReductionAmount;
        target.DamageReductionBypass = source.DamageReductionBypass;
        target.Immunities = source.Immunities;
        target.IsMindless = source.IsMindless;

        // Attacks
        target.NaturalAttacks = source.NaturalAttacks;

        // Zombie-specific
        target.IsSingleActionsOnly = source.IsSingleActionsOnly;

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

        // Feats (zombie keeps Toughness)
        target.Feats = source.Feats;
    }
}
