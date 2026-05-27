// ============================================================================
// D&D 3.5e Magic Item Enchantment Types
// Phase 1: Foundation - Enchantment type enumeration
// ============================================================================

/// <summary>
/// All weapon and armor/shield special abilities from the D&D 3.5 DMG.
/// Organized by category for clarity.
/// </summary>
public enum EnchantmentType
{
    None = 0,

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Elemental Damage
    // ========================================================================
    Flaming,            // +1d6 fire damage
    FlamingBurst,       // +1d6 fire + extra on crit
    Frost,              // +1d6 cold damage
    IcyBurst,           // +1d6 cold + extra on crit
    Shock,              // +1d6 electricity damage
    ShockingBurst,      // +1d6 electricity + extra on crit
    Corrosive,          // +1d6 acid damage (non-core but common)
    Thundering,         // +1d8 sonic on crit

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Alignment Damage
    // ========================================================================
    Holy,               // +2d6 vs evil
    Unholy,             // +2d6 vs good
    Axiomatic,          // +2d6 vs chaotic
    Anarchic,           // +2d6 vs lawful

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Bane
    // ========================================================================
    Bane,               // +2 enh, +2d6 vs specific creature type

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Critical Enhancement
    // ========================================================================
    Keen,               // Double threat range
    Vorpal,             // Decapitate on natural 20

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Attack/Damage Modifiers
    // ========================================================================
    Vicious,            // +2d6 damage to target, 1d6 to wielder
    Wounding,           // 1 CON damage per hit

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Speed / Extra Attacks
    // ========================================================================
    Speed,              // Extra attack at full BAB (as haste)

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Thrown/Ranged
    // ========================================================================
    Throwing,           // Allows melee weapon to be thrown (10 ft increment)
    Returning,          // Thrown weapon returns immediately
    Distance,           // Double range increment
    Seeking,            // Negates concealment for ranged attacks

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Defensive
    // ========================================================================
    Defending,          // Transfer enhancement bonus to AC

    // ========================================================================
    // WEAPON SPECIAL ABILITIES - Spell-like / Special
    // ========================================================================
    SpellStoring,       // Store and release spell on hit (up to 3rd level)
    MercifulWeapon,     // +1d6 nonlethal, suppress to deal lethal
    BrilliantEnergy,    // Ignores armor, shield, and natural armor (not force/deflection)
    Dancing,            // Weapon fights on its own for 4 rounds
    Disruption,         // Undead must Fort DC 14 or be destroyed (bludgeoning only)
    KiFocus,            // Allows monk ki abilities through weapon
    GhostTouchWeapon,   // Full damage vs incorporeal creatures

    // ========================================================================
    // ARMOR/SHIELD SPECIAL ABILITIES - Fortification
    // ========================================================================
    FortificationLight,     // 25% chance to negate crits/sneak attack
    FortificationModerate,  // 50% chance to negate crits/sneak attack
    FortificationHeavy,     // 75% chance to negate crits/sneak attack

    // ========================================================================
    // ARMOR/SHIELD SPECIAL ABILITIES - Energy Resistance
    // ========================================================================
    EnergyResistanceFire,           // Resist fire 10
    EnergyResistanceCold,           // Resist cold 10
    EnergyResistanceElectricity,    // Resist electricity 10
    EnergyResistanceAcid,           // Resist acid 10
    EnergyResistanceSonic,          // Resist sonic 10
    ImprovedEnergyResistanceFire,       // Resist fire 20
    ImprovedEnergyResistanceCold,       // Resist cold 20
    ImprovedEnergyResistanceElectricity,// Resist electricity 20
    ImprovedEnergyResistanceAcid,       // Resist acid 20
    ImprovedEnergyResistanceSonic,      // Resist sonic 20
    GreaterEnergyResistanceFire,        // Resist fire 30
    GreaterEnergyResistanceCold,        // Resist cold 30
    GreaterEnergyResistanceElectricity, // Resist electricity 30
    GreaterEnergyResistanceAcid,        // Resist acid 30
    GreaterEnergyResistanceSonic,       // Resist sonic 30

    // ========================================================================
    // ARMOR/SHIELD SPECIAL ABILITIES - Defensive Enhancements
    // ========================================================================
    Shadow,             // +5 Hide
    ImprovedShadow,     // +10 Hide
    GreaterShadow,      // +15 Hide
    SilentMoves,        // +5 Move Silently
    ImprovedSilentMoves,// +10 Move Silently
    GreaterSilentMoves, // +15 Move Silently
    SlickArmor,         // +5 Escape Artist
    ImprovedSlick,      // +10 Escape Artist
    GreaterSlick,       // +15 Escape Artist
    GhostTouch,         // Full AC vs incorporeal
    Invulnerability,    // DR 5/magic
    WildArmor,          // Armor melds with wild shape
    Glamered,           // Disguise Self at will on armor appearance
    Etherealness,       // Ethereal Jaunt 1/day for 10 min
    UndeadControlling,  // Command undead as evil cleric

    // ========================================================================
    // SHIELD SPECIFIC
    // ========================================================================
    ArrowDeflection,    // Deflect one ranged attack per round
    Bashing,            // Shield deals damage as if 2 sizes larger
    Blinding,           // Flash 2/day, Fort DC 14 or blind 1d4 rounds
    Animated,           // Shield floats, defends without being held
    Reflecting,         // Reflect targeted spells back at caster 1/day
    GhostTouchShield,   // Shield blocks incorporeal touch attacks

    // ========================================================================
    // ARMOR SPECIAL ABILITIES - Spell Resistance
    // ========================================================================
    SpellResistance13,  // SR 13
    SpellResistance15,  // SR 15
    SpellResistance17,  // SR 17
    SpellResistance19,  // SR 19
}
