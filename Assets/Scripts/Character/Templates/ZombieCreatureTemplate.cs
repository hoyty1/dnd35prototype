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
        target.RaceName = source.RaceName;
        target.CharacterAlignment = source.CharacterAlignment; // always neutral evil (CRE-002)
        target.NaturalArmorBonus = source.NaturalArmorBonus;
        target.IsExceptionallyStable = source.IsExceptionallyStable;
        target.Beheading = source.Beheading;

        // Proficiency data (CHR-072; CreatureProficiency.ApplyFromDefinition)
        target.EntryWeaponIds = source.EntryWeaponIds;
        target.EntryArmorCategory = source.EntryArmorCategory;
        target.EntryShieldProficiency = source.EntryShieldProficiency;
        target.IsHumanoidForm = source.IsHumanoidForm;
        target.ClassLevelsAreRacialHitDice = source.ClassLevelsAreRacialHitDice;
        target.HasRacialHumanoidHitDice = source.HasRacialHumanoidHitDice;
        target.FirstHitDieMaximized = source.FirstHitDieMaximized;

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
        target.IsQuadruped = source.IsQuadruped;
        target.ImprovedGrabTriggerAttackName = source.ImprovedGrabTriggerAttackName;
        target.ImprovedGrabMaxTargetSize = source.ImprovedGrabMaxTargetSize;
        target.TripTriggerAttackName = source.TripTriggerAttackName;
        target.KnownSpellIds = source.KnownSpellIds;
        target.PreparedSpellSlotIds = source.PreparedSpellSlotIds;
        target.ClericDomains = source.ClericDomains;
        target.ClericSpontaneousChoice = source.ClericSpontaneousChoice;
        target.InnateSpellcastingClass = source.InnateSpellcastingClass;
        target.InnateCasterLevel = source.InnateCasterLevel;
        target.SpellResistance = source.SpellResistance;

        // Display
        target.SpecialAbilities = source.SpecialAbilities;
        target.CreatureTags = source.CreatureTags;
        target.AppliedTemplateIds = source.AppliedTemplateIds;
        target.BakedTemplateIds = source.BakedTemplateIds;
        target.ChallengeRating = source.ChallengeRating;

        // Feats (zombie keeps Toughness)
        target.Feats = source.Feats;
        target.BonusFeats = source.BonusFeats;

        // Ranger choices (CHR-020; CRE-023): the zombie has no class levels, so the template result clears them.
        target.RangerStyle = source.RangerStyle;
        target.RangerFavoredEnemies = NPCDefinition.CloneFavoredEnemies(source.RangerFavoredEnemies);

        // Breath weapons, frightful presence and engulf (every BreathWeaponDefinition field included: damage bonus, save type, recharge dice, once per round;
        // CRE-023, CRE-040): the template result's own, which the factory clears (a zombie loses the base creature's special attacks, MM p.266).
        target.BreathWeapon = source.BreathWeapon?.Clone();
        target.SecondaryBreathWeapon = source.SecondaryBreathWeapon?.Clone();
        target.FrightfulPresence = source.FrightfulPresence?.Clone();
        target.Engulf = source.Engulf?.Clone();
    }
}
