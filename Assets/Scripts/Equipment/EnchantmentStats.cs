using System.Collections.Generic;

// ============================================================================
// D&D 3.5e Magic Item Enchantment Stats - Data class for enchantment properties
// Phase 1: Foundation - Mirrors ItemMaterial pattern
// ============================================================================

/// <summary>
/// Specifies what equipment slot an enchantment can be applied to.
/// </summary>
public enum EnchantmentSlot
{
    Weapon,
    Armor,
    Shield,
    ArmorOrShield,  // Can go on either armor or shield
}

/// <summary>
/// Data class holding all properties for a single enchantment type.
/// Immutable after construction — created by EnchantmentProperties and looked up at runtime.
/// Mirrors the ItemMaterial pattern: centralized data, no hardcoding in gameplay code.
/// </summary>
public class EnchantmentStats
{
    // ========================================================================
    // IDENTITY
    // ========================================================================

    /// <summary>The enum identifier for this enchantment.</summary>
    public EnchantmentType Type;

    /// <summary>Display name (e.g., "Flaming", "Holy", "Fortification, Light").</summary>
    public string DisplayName;

    /// <summary>Short description for tooltips.</summary>
    public string Description;

    // ========================================================================
    // PRICING (D&D 3.5 DMG)
    // ========================================================================

    /// <summary>
    /// Bonus equivalent cost for pricing (e.g., +1 for Flaming, +2 for Holy).
    /// Used in the formula: total bonus² × multiplier.
    /// If 0, use FlatCostGp instead.
    /// </summary>
    public int BonusEquivalent;

    /// <summary>
    /// Flat gold cost (used when BonusEquivalent is 0).
    /// E.g., some special abilities have fixed prices instead of bonus equivalents.
    /// </summary>
    public int FlatCostGp;

    // ========================================================================
    // SLOT & RESTRICTIONS
    // ========================================================================

    /// <summary>What equipment slot this enchantment applies to.</summary>
    public EnchantmentSlot Slot;

    /// <summary>If true, only melee weapons can have this enchantment.</summary>
    public bool MeleeOnly;

    /// <summary>If true, only ranged weapons can have this enchantment.</summary>
    public bool RangedOnly;

    /// <summary>If true, only slashing or piercing weapons can have this (e.g., Keen, Vorpal).</summary>
    public bool RequiresSlashingOrPiercing;

    /// <summary>Minimum base enhancement bonus required before this can be added.</summary>
    public int MinimumEnhancementBonus;

    /// <summary>Other enchantments that must already be present (e.g., Vorpal requires Keen).</summary>
    public List<EnchantmentType> RequiredEnchantments;

    /// <summary>Enchantments that conflict / cannot coexist (e.g., Flaming + Frost on same weapon is fine, but FortLight + FortModerate is not).</summary>
    public List<EnchantmentType> IncompatibleWith;

    // ========================================================================
    // COMBAT EFFECTS - Elemental Damage
    // ========================================================================

    /// <summary>Extra damage type dealt on hit (Fire, Cold, Electricity, Acid, Sonic).</summary>
    public DamageType ExtraDamageType;

    /// <summary>Number of dice for extra damage on every hit (e.g., 1 for 1d6 fire).</summary>
    public int ExtraDamageDice;

    /// <summary>Sides per die for extra damage (e.g., 6 for d6).</summary>
    public int ExtraDamageDieSides;

    /// <summary>Number of extra dice on critical hit (e.g., 1 for Burst abilities).</summary>
    public int CritBonusDice;

    /// <summary>Sides per die for crit bonus damage (e.g., 10 for d10 on Burst).</summary>
    public int CritBonusDieSides;

    /// <summary>If true, crit bonus dice scale with crit multiplier (×2 = +1d10, ×3 = +2d10, ×4 = +3d10).</summary>
    public bool CritDiceScaleWithMultiplier;

    // ========================================================================
    // COMBAT EFFECTS - Alignment Damage
    // ========================================================================

    /// <summary>If true, deals alignment-based bonus damage (Holy, Unholy, Axiomatic, Anarchic).</summary>
    public bool IsAlignmentDamage;

    /// <summary>
    /// The alignment axis this targets:
    /// Holy targets Evil, Unholy targets Good, Axiomatic targets Chaotic, Anarchic targets Lawful.
    /// Stored as the "opposing" alignment descriptor for checking.
    /// </summary>
    public DamageBypassTag AlignmentDamageTargets;

    /// <summary>
    /// The alignment bypass tag this weapon gains (e.g., Holy adds Good tag for DR bypass).
    /// </summary>
    public DamageBypassTag AlignmentBypassTag;

    /// <summary>Number of alignment damage dice (e.g., 2 for 2d6).</summary>
    public int AlignmentDamageDice;

    /// <summary>Sides per alignment damage die (e.g., 6 for d6).</summary>
    public int AlignmentDamageDieSides;

    // ========================================================================
    // COMBAT EFFECTS - Bane
    // ========================================================================

    /// <summary>If true, this is a Bane enchantment against a specific creature type.</summary>
    public bool IsBane;

    /// <summary>The creature type targeted by Bane (e.g., "Undead", "Dragon").</summary>
    public string BaneCreatureType;

    /// <summary>Enhancement bonus increase vs bane target (typically +2).</summary>
    public int BaneEnhancementBonus;

    /// <summary>Number of bonus damage dice vs bane target (typically 2).</summary>
    public int BaneDamageDice;

    /// <summary>Sides per bane damage die (typically 6).</summary>
    public int BaneDamageDieSides;

    // ========================================================================
    // COMBAT EFFECTS - Critical Modifications
    // ========================================================================

    /// <summary>If true, doubles the weapon's threat range (Keen).</summary>
    public bool DoublesThreadRange;

    /// <summary>If true, special effect on natural 20 confirmed crit (Vorpal).</summary>
    public bool VorpalEffect;

    // ========================================================================
    // COMBAT EFFECTS - Attack/Damage Modifiers
    // ========================================================================

    /// <summary>Flat bonus to attack rolls (beyond enhancement bonus).</summary>
    public int AttackBonus;

    /// <summary>Flat bonus to damage rolls (beyond enhancement bonus).</summary>
    public int DamageBonus;

    /// <summary>If true, deals extra damage to target but also damages wielder (Vicious).</summary>
    public bool ViciousEffect;

    /// <summary>Vicious damage dice to target.</summary>
    public int ViciousDamageDice;

    /// <summary>Vicious damage die sides to target.</summary>
    public int ViciousDamageDieSides;

    /// <summary>Vicious backlash damage dice to wielder.</summary>
    public int ViciousBacklashDice;

    /// <summary>Vicious backlash die sides.</summary>
    public int ViciousBacklashDieSides;

    /// <summary>If true, deals 1 CON damage per hit (Wounding).</summary>
    public bool WoundingEffect;

    // ========================================================================
    // COMBAT EFFECTS - Speed
    // ========================================================================

    /// <summary>If true, grants one extra attack per round at full BAB (Speed/Haste).</summary>
    public bool GrantsExtraAttack;

    // ========================================================================
    // COMBAT EFFECTS - Thrown/Ranged
    // ========================================================================

    /// <summary>If true, allows melee weapon to be thrown with 10 ft range increment (Throwing).</summary>
    public bool AllowsThrow;

    /// <summary>Range increment when thrown (typically 10 ft).</summary>
    public int ThrowRangeIncrement;

    /// <summary>If true, thrown weapon returns to thrower immediately (Returning).</summary>
    public bool ReturnsWhenThrown;

    /// <summary>If true, doubles range increment (Distance).</summary>
    public bool DoublesRange;

    /// <summary>If true, negates concealment for ranged attacks (Seeking).</summary>
    public bool NegatesConcealment;

    // ========================================================================
    // COMBAT EFFECTS - Defensive (Weapon)
    // ========================================================================

    /// <summary>If true, wielder can transfer enhancement bonus to AC (Defending).</summary>
    public bool DefendingEffect;

    // ========================================================================
    // ARMOR/SHIELD EFFECTS - Fortification
    // ========================================================================

    /// <summary>Percentage chance to negate crits/sneak attacks (0-100).</summary>
    public int FortificationPercent;

    // ========================================================================
    // ARMOR/SHIELD EFFECTS - Energy Resistance
    // ========================================================================

    /// <summary>Damage type for energy resistance (Fire, Cold, etc.).</summary>
    public DamageType ResistanceDamageType;

    /// <summary>Amount of energy resistance granted (10, 20, or 30).</summary>
    public int ResistanceAmount;

    // ========================================================================
    // ARMOR/SHIELD EFFECTS - Skill Bonuses
    // ========================================================================

    /// <summary>Competence bonus to a specific skill (Shadow→Hide, SilentMoves→MoveSilently, Slick→EscapeArtist).</summary>
    public int SkillBonus;

    /// <summary>Name of the skill affected (for display/tooltip).</summary>
    public string SkillBonusTarget;

    // ========================================================================
    // ARMOR/SHIELD EFFECTS - DR / Spell Resistance
    // ========================================================================

    /// <summary>Damage Reduction amount (e.g., 5 for Invulnerability's DR 5/magic).</summary>
    public int DamageReductionAmount;

    /// <summary>DR bypass type string (e.g., "magic" for Invulnerability).</summary>
    public string DamageReductionBypass;

    /// <summary>Spell Resistance value granted (13, 15, 17, 19).</summary>
    public int SpellResistance;

    // ========================================================================
    // ARMOR/SHIELD EFFECTS - Misc
    // ========================================================================

    /// <summary>If true, armor works with druid wild shape (Wild).</summary>
    public bool WildShapeCompatible;

    /// <summary>If true, provides full AC bonus vs incorporeal (Ghost Touch).</summary>
    public bool GhostTouchEffect;

    /// <summary>If true, shield can deflect ranged attacks (Arrow Deflection).</summary>
    public bool ArrowDeflectionEffect;

    /// <summary>If true, shield bash deals damage as 2 sizes larger (Bashing).</summary>
    public bool BashingEffect;

    // ========================================================================
    // CONSTRUCTOR
    // ========================================================================

    public EnchantmentStats()
    {
        Type = EnchantmentType.None;
        DisplayName = "";
        Description = "";
        RequiredEnchantments = new List<EnchantmentType>();
        IncompatibleWith = new List<EnchantmentType>();
        ExtraDamageType = DamageType.Untyped;
        ResistanceDamageType = DamageType.Untyped;
        AlignmentDamageTargets = DamageBypassTag.None;
        AlignmentBypassTag = DamageBypassTag.None;
        BaneCreatureType = "";
        SkillBonusTarget = "";
        DamageReductionBypass = "";
    }
}
