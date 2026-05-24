using System;
using System.Collections.Generic;

// ============================================================================
// D&D 3.5e Specific Magic Items - Named items from DMG p.220-232
// These are pre-constructed magic items with unique properties and fixed stats.
// ============================================================================

/// <summary>
/// Enum of all specific named magic items from the D&D 3.5 DMG.
/// Each corresponds to a unique item with fixed properties, unlike generic
/// enchanted items which are built from components.
/// </summary>
public enum SpecificItemType
{
    None = 0,

    // ================================================================
    // SPECIFIC WEAPONS (DMG p.224-232)
    // ================================================================

    // --- Minor Weapons ---
    SleepArrow,                 // +1 arrow, sleep on hit (Will DC 11)
    ScreamingBolt,              // +2 bolt, shaken 20 ft AoE (Will DC 14)
    SilverDaggerMW,             // Masterwork silver dagger
    ColdIronLongswordMW,        // Masterwork cold iron longsword
    JavelinOfLightning,         // +1 javelin, 5d6 lightning bolt
    SlayingArrow,               // +1 arrow, death effect vs creature type (Fort DC 20)
    AdamantineDagger,           // Nonmagical adamantine dagger
    AdamantineBattleaxe,        // Nonmagical adamantine battleaxe
    GreaterSlayingArrow,        // +1 arrow, death effect vs creature type (Fort DC 23)
    Shatterspike,               // +1 longsword, +4 sunder
    DaggerOfVenom,              // +1 dagger, 1/day poison (Fort DC 14)

    // --- Medium Weapons ---
    TridentOfWarning,           // +2 trident, detect aquatic predators
    AssassinsDagger,            // +2 dagger, +1 death attack DC
    ShiftersSorrow,             // +1/+1 silver two-bladed sword, anti-shapechanger
    TridentOfFishCommand,       // +1 trident, charm aquatic animals 3/day
    FlameTongue,                // +1 flaming burst longsword, 1/day 4d6 fire ray
    LuckBlade0,                 // +2 short sword, +1 luck saves, 1/day reroll, 0 wishes
    SwordOfSubtlety,            // +1 short sword, +4 sneak attack bonus
    SwordOfThePlanes,           // +1/+2/+3 longsword (varies by plane)
    NineLivesStealer,           // +2 longsword, crit = death (9 charges), evil
    Oathbow,                    // +2 comp longbow, sworn enemy +5/+2d6
    SwordOfLifeStealing,        // +2 longsword, crit = negative level + temp HP
    MaceOfTerror,               // +2 heavy mace, 3/day fear cone (Will DC 16)
    LifeDrinker,                // +1 greataxe, 2 negative levels on hit (self takes 1)
    SylvanScimitar,             // +3 scimitar, Cleave, +1d6 outdoors
    RapierOfPuncturing,         // +2 wounding rapier, 3/day 1d6 Con damage
    SunBlade,                   // +2 bastard sword, +4 vs evil, x2 vs undead, sunlight
    FrostBrand,                 // +3 frost greatsword, fire resist 10, extinguish fires
    DwarvenThrower,             // +2/+3 returning warhammer, dwarf only

    // --- Major Weapons ---
    LuckBlade1,                 // +2 short sword, luck/reroll, 1 wish
    MaceOfSmiting,              // +3 adamantine heavy mace, +5 vs constructs
    LuckBlade2,                 // +2 short sword, luck/reroll, 2 wishes
    HolyAvenger,                // +2/+5 cold iron holy longsword, paladin SR, dispel
    LuckBlade3,                 // +2 short sword, luck/reroll, 3 wishes

    // ================================================================
    // SPECIFIC ARMORS (DMG p.220-222)
    // ================================================================

    // --- Nonmagical ---
    MithralShirt,               // Mithral chain shirt (light armor)
    DragonhidePlate,            // Dragonhide full plate (druid-compatible)
    ElvenChain,                 // Mithral chainmail (light armor)
    AdamantineBreastplate,      // Adamantine breastplate (DR 2/—)
    DwarvenPlate,               // Adamantine full plate (DR 3/—, nonmagical)

    // --- Magical ---
    RhinoHide,                  // +2 hide, +2d6 charge damage
    BandedMailOfLuck,           // +3 banded mail, 1/week reroll attack
    CelestialArmor,             // +3 chainmail (light), fly 1/day
    PlateArmorOfTheDeep,        // +1 full plate, underwater breathing/swim
    BreastplateOfCommand,       // +2 breastplate, +2 Cha/Leadership
    MithralFullPlateOfSpeed,    // +1 mithral full plate, haste 10 rnd/day
    DemonArmor,                 // +4 full plate, claw attacks, contagion, evil
    ArmorOfRage,                // +1 breastplate, barbarian rage +6 STR/CON (instead of +4)
    PlateArmorOfEtherealness,   // +1 full plate + Etherealness enhancement (NOT in SRD specific items)

    // ================================================================
    // SPECIFIC SHIELDS (DMG p.222-223)
    // ================================================================

    // --- Nonmagical ---
    DarkwoodBuckler,            // Darkwood buckler (light weight)
    DarkwoodShield,             // Darkwood heavy wooden shield
    MithralHeavyShield,         // Mithral heavy steel shield

    // --- Magical ---
    CastersShield,              // +1 light wood shield, scroll slot (3rd level)
    SpinedShield,               // +1 heavy steel shield, 3/day spine ranged attack
    LionsShield,                // +2 heavy steel shield, 3/day lion bite (2d6)
    LionsShieldGreater,         // +2 heavy steel shield, 3/day lion bite (2d8+2), 1/day summon dire lion
    WingedShield,               // +3 heavy wood shield, fly 1/day
    AbsorbingShield,            // +1 heavy steel shield, disintegrate object 1/2 days, spell absorption (50 levels)
    AbsorbingShieldGreater,     // +1 heavy steel shield (greater), disintegrate 1/day, spell absorption (100 levels)
    AnimatedShield,             // +2 heavy steel shield, animates for hands-free defense
    AnimatedShieldGreater,      // +5 heavy steel shield, animates for hands-free defense
}

/// <summary>
/// Definition for a specific named magic item from the DMG.
/// Contains all fixed properties that distinguish it from a generic enchanted item.
/// </summary>
[Serializable]
public class SpecificItemDefinition
{
    /// <summary>The unique identifier for this specific item.</summary>
    public SpecificItemType Type;

    /// <summary>Display name (e.g., "Flame Tongue", "Holy Avenger").</summary>
    public string Name;

    /// <summary>Full description of the item's lore and abilities.</summary>
    public string Description;

    /// <summary>ID of the base item in ItemDatabase (e.g., "Longsword", "Full Plate").</summary>
    public string BaseItemId;

    /// <summary>Item category: Weapon, Armor, or Shield.</summary>
    public ItemType ItemCategory;

    /// <summary>Enhancement bonus (0 for nonmagical items).</summary>
    public int EnhancementBonus;

    /// <summary>
    /// Standard enchantments that this item inherently possesses.
    /// These use the existing EnchantmentType system.
    /// </summary>
    public List<EnchantmentType> StandardEnchantments = new List<EnchantmentType>();

    /// <summary>Market price in gold pieces (DMG fixed price).</summary>
    public int MarketPrice;

    /// <summary>Caster level for the item (0 if nonmagical).</summary>
    public int CasterLevel;

    /// <summary>
    /// Material type override (e.g., Mithral, Adamantine, ColdIron).
    /// Set to Standard if no special material.
    /// </summary>
    public ItemMaterialType MaterialOverride = ItemMaterialType.Standard;

    /// <summary>True if this item has unique behavior beyond standard enchantments.</summary>
    public bool HasCustomBehavior;

    /// <summary>
    /// Key-value store for unique properties not covered by standard enchantments.
    /// Examples: "FireResistance" → 10, "ChargesRemaining" → 9, "DwarfOnly" → true
    /// </summary>
    public Dictionary<string, object> UniqueProperties = new Dictionary<string, object>();

    /// <summary>Implementation priority tier (1 = iconic, 2 = popular, 3 = complete).</summary>
    public int PriorityTier = 3;

    /// <summary>Brief implementation notes for developers.</summary>
    public string ImplementationNotes = "";

    /// <summary>Check if this item has a specific unique property.</summary>
    public bool HasProperty(string key) => UniqueProperties != null && UniqueProperties.ContainsKey(key);

    /// <summary>Get a unique property value with a default fallback.</summary>
    public T GetProperty<T>(string key, T defaultValue = default)
    {
        if (UniqueProperties != null && UniqueProperties.TryGetValue(key, out object val) && val is T typed)
            return typed;
        return defaultValue;
    }
}
