using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Types of items in the game.
/// </summary>
public enum ItemType
{
    Weapon,
    Armor,
    Shield,
    Consumable,
    Misc,
    Ammunition,
    Ring,
    Wondrous
}

/// <summary>
/// Built-in consumable effect categories.
/// Keep this extensible so future item effects can be added cleanly.
/// </summary>
public enum ConsumableEffectType
{
    None,
    HealHP,
    SpellEffect
}

/// <summary>
/// Which equipment slot(s) an item can go into.
/// </summary>
public enum EquipSlot
{
    None = 0,         // Cannot be equipped (consumable, misc)

    // Legacy/core combat slots (kept stable for serialized data compatibility)
    Armor = 1,        // Legacy name for armor/robe slot
    LeftHand = 2,     // Shield or weapon
    RightHand = 3,    // Weapon only
    EitherHand = 4,   // Can go in left or right hand (weapons)

    // D&D 3.5e body equipment slots
    Head = 5,
    FaceEyes = 6,
    Neck = 7,
    Torso = 8,
    ArmorRobe = 9,
    Waist = 10,
    Back = 11,
    Wrists = 12,
    Hands = 13,
    LeftRing = 14,
    RightRing = 15,
    EitherRing = 16,
    Feet = 17,
    Slotless = 18,  // Wondrous items that don't occupy a body slot (Ioun Stones, bags, etc.)

    // Convenience aliases for creature equipment definitions
    MainHand = 19,    // Primary weapon hand (equivalent to RightHand for creatures)
    OffHand = 20,     // Off-hand weapon or shield (equivalent to LeftHand for creatures)
    Ranged = 21       // Ranged weapon slot (bow, crossbow, etc.)
}

/// <summary>
/// Weapon proficiency category from D&D 3.5 PHB.
/// </summary>
public enum WeaponProficiency
{
    None,
    Simple,
    Martial,
    Exotic
}

/// <summary>
/// Weapon category: melee or ranged.
/// </summary>
public enum WeaponCategory
{
    None,
    Melee,
    Ranged
}

/// <summary>
/// Handedness/size category for weapon use in D&D 3.5 combat maneuvers.
/// </summary>
public enum WeaponSizeCategory
{
    None,
    Light,
    OneHanded,
    TwoHanded
}

/// <summary>
/// How a weapon applies ability modifiers to damage (D&D 3.5 rules).
/// </summary>
public enum DamageModifierType
{
    None,               // No ability modifier to damage (bows, crossbows, slings)
    Strength,           // Add full STR modifier (one-handed melee, thrown weapons)
    StrengthOneAndHalf, // Add 1.5× STR bonus, rounded down; STR penalties are not multiplied (two-handed melee)
    StrengthHalf,       // Add 0.5× STR bonus, rounded down; STR penalties are not multiplied (off-hand; handled separately)
    Composite           // Add STR up to composite rating (composite bows)
}

/// <summary>
/// Armor weight category from D&D 3.5 PHB.
/// </summary>
public enum ArmorCategory
{
    None,
    Light,
    Medium,
    Heavy,
    Shield
}

/// <summary>
/// Material profile used for armor interactions (for example Shocking Grasp metal bonus).
/// </summary>
public enum ArmorMaterialType
{
    Unknown,
    NonMetal,
    Metal,
    Mixed
}

/// <summary>
/// Broad body composition used for creature-material interactions.
/// </summary>
public enum MaterialComposition
{
    Unknown,
    Organic,
    Metal,
    Stone,
    Wood,
    Energy,
    Mixed,
    Bone
}

/// <summary>
/// Ammunition compatibility type linking projectile weapons to their ammo.
/// </summary>
public enum AmmunitionType
{
    None,
    Arrow,       // Used by longbow, shortbow, composite bows
    Bolt,        // Used by crossbows
    SlingBullet  // Used by slings
}

/// <summary>
/// D&D 3.5 reload action required for crossbows.
/// </summary>
public enum ReloadActionType
{
    None,
    FreeAction,
    MoveAction,
    FullRound
}

/// <summary>
/// Represents a single item with its properties and stats.
/// Items are value types copied around; use ItemDatabase IDs for identity.
/// </summary>
[System.Serializable]
public class ItemData
{
    public string Id;           // Unique identifier (legacy storage string, e.g., ItemIDs.LONGSWORD)
    public ItemID IdEnum        // Type-safe view over Id for Phase 2 migration
    {
        get => Id.ToItemID();
        set => Id = value.ToStorageString();
    }
    public string Name;         // Display name
    public string Description;  // Tooltip description
    public ItemType Type;
    public EquipSlot Slot;

    // --- Weapon Properties ---
    public WeaponProficiency Proficiency;   // Simple, Martial, Exotic
    public WeaponCategory WeaponCat;        // Melee or Ranged
    public WeaponSizeCategory WeaponSize;   // Light, One-Handed, or Two-Handed
    public int DamageDice;      // Sides on damage die (e.g., 8 for d8)
    public int DamageCount;     // Number of damage dice (usually 1)
    public int BonusDamage;     // Flat bonus damage
    public SizeCategory DesignedForSize = SizeCategory.Medium; // Creature size this weapon profile is built for
    public int AttackRange;     // Legacy max range field: melee in squares, ranged in feet. Use ReachSquares for melee semantics.
    public bool IsLightWeapon;  // Light weapon (dagger, short sword) - reduces TWF penalties
    public bool IsTwoHanded;    // Two-handed weapon - can't be dual-wielded, 1.5x STR to damage
    public bool HasReach;       // Legacy reach flag (kept for backward compatibility)

    // --- D&D 3.5 Reach Mechanics ---
    public int ReachSquares;          // Melee reach in squares (1 = 5 ft, 2 = 10 ft, 3 = 15 ft)
    public bool CanAttackAdjacent;    // Whether this melee weapon can attack adjacent (distance 1)
    public bool IsReachWeapon;        // True for reach weapons (typically ReachSquares > 1)
    public bool DealsNonlethalDamage; // Whip and similar weapons can deal nonlethal damage
    public bool WhipLikeArmorRestriction; // Cannot harm targets with armor/natural armor bonus +1 or higher

    public string DamageType;   // Legacy display/source string ("slashing", "piercing", etc.)
    public bool NoStrengthToDamage; // Special-case weapons (e.g., torch) that never add STR to damage.
    public string SpecialProperties; // Extensible comma-separated flags for special weapon behavior/UI.

    // --- Masterwork & Special Material (D&D 3.5e PHB p.126-128, DMG p.283-284) ---
    public bool IsMasterwork;              // Masterwork quality: weapons +1 attack, armor -1 ACP
    public ItemMaterial Material;          // Special material (adamantine, mithral, cold iron, silver, darkwood)

    // --- Damage bypass/material/alignment properties (for DR interactions) ---
    public bool CountsAsMagicForBypass;    // Bypasses DR/magic
    public bool IsSilvered;                // Bypasses DR/silver
    public bool IsColdIron;                // Bypasses DR/cold iron
    public bool IsAdamantine;              // Bypasses DR/adamantine
    public bool IsAlignedGood;             // Bypasses DR/good
    public bool IsAlignedEvil;             // Bypasses DR/evil
    public bool IsAlignedLawful;           // Bypasses DR/lawful

    // --- Reloading (D&D 3.5 crossbows) ---
    public bool RequiresReload;              // True for crossbows that must be reloaded after firing
    public bool IsLoaded = true;             // Runtime state: starts loaded
    public ReloadActionType ReloadAction;    // Base reload action without Rapid Reload
    public bool IsAlignedChaotic;          // Bypasses DR/chaotic

    // --- Ammunition Properties (D&D 3.5) ---
    /// <summary>For projectile weapons: type of ammunition consumed per shot.</summary>
    public AmmunitionType RequiresAmmoType = AmmunitionType.None;
    /// <summary>For ammunition items: what type this ammo is (Arrow, Bolt, SlingBullet).</summary>
    public AmmunitionType AmmoType = AmmunitionType.None;
    /// <summary>For ammunition items: how many individual rounds remain in this stack.</summary>
    public int Quantity;
    /// <summary>For ammunition items: maximum stack size (e.g. 20 for a bundle of 20 arrows).</summary>
    public int MaxQuantity;

    // --- Damage Modifier Properties (D&D 3.5) ---
    public DamageModifierType DmgModType;  // How STR (or other) applies to damage
    public int CompositeRating;            // For composite bows: max STR bonus allowed (0 = no bonus)
    public bool IsThrown;                  // Whether this weapon can be thrown (gets STR on throw)

    // --- Range Increment (D&D 3.5) ---
    // Max range = 5 × RangeIncrement for thrown weapons (IsThrown), 10 × RangeIncrement for projectile weapons.
    public int RangeIncrement;             // Range increment in feet (0 = melee only).

    // Compatibility aliases for gameplay/UI code that still uses explicit throwable naming.
    public bool IsThrowable { get => IsThrown; set => IsThrown = value; }
    public int ThrowRangeIncrement { get => RangeIncrement; set => RangeIncrement = value; }

    // --- Critical Hit (D&D 3.5) ---
    public int CritThreatMin;   // Minimum natural d20 roll to threaten a crit (e.g., 19 for 19-20, 20 for 20 only)
    public int CritMultiplier;  // Damage multiplier on confirmed crit (e.g., 2 for ×2, 3 for ×3)

    // --- Armor/Shield Properties (D&D 3.5 PHB) ---
    public int ArmorBonus;          // AC bonus when equipped as armor
    public int ShieldBonus;         // AC bonus when equipped as shield
    public ArmorCategory ArmorCat;  // Light, Medium, Heavy, Shield
    public ArmorMaterialType ArmorMaterial = ArmorMaterialType.Unknown;
    public int MaxDexBonus;         // Maximum DEX bonus to AC while wearing (-1 = no limit)
    public int ArmorCheckPenalty;   // Penalty to STR/DEX skills (stored as positive, applied as negative)
    public int ArcaneSpellFailure;  // Percentage chance of arcane spell failure (0-100)
    public float WeightLbs;         // Weight in pounds

    // --- Extensible tag metadata ---
    // Tags inherited by characters while this item is equipped.
    // Examples: "Light Armor", "Chain Shirt".
    public HashSet<string> VisualTags = new HashSet<string>();

    // --- Item pricing + enhancement ---
    public int BasePriceGp;         // Mundane base price in gp (before magical enhancement)
    public int enhancementBonus;    // Serialized enhancement bonus field requested by enhancement item pipeline (0-5)

    // --- Appraise / Treasure valuation (D&D 3.5e PHB p.67) ---
    /// <summary>True value of the item in gp (set for appraised treasure items like gems/art).</summary>
    public int TrueValueGp;
    /// <summary>Appraised value after Appraise skill check. May differ from TrueValueGp on failed checks.</summary>
    public int AppraisedValueGp;
    /// <summary>Whether this item has been appraised (gems/art from treasure generation).</summary>
    public bool IsAppraised;
    /// <summary>Whether this item originated from treasure generation (gem, art object, mundane, or magic treasure).</summary>
    public bool IsTreasureItem;

    // Legacy/compatibility field still used by existing systems/tests.
    public int EnhancementBonus;    // Magic enhancement bonus to durability (+2 hardness, +10 HP per +1)

    // --- Item durability (used by Sunder) ---
    public int Hardness;            // Effective hardness after enhancement
    public int MaxHitPoints;        // Maximum object HP after enhancement
    public int CurrentHitPoints;    // Runtime durability HP
    public bool IsBroken;           // Broken at <= half max HP (until repaired)
    public bool IsDestroyed;        // Destroyed at <= 0 HP

    // --- Active item spell effects (duration tracked on item instance) ---
    public List<ItemSpellEffect> ActiveSpellEffects = new List<ItemSpellEffect>();

    // --- Magic Item Enchantments (D&D 3.5 DMG special abilities) ---
    /// <summary>
    /// Enchantment data for this item (special abilities like Flaming, Holy, Keen, Fortification, etc.).
    /// Null if the item has no special abilities (plain magic or mundane).
    /// </summary>
    public ItemEnchantmentData Enchantment;

    // --- Specific Magic Item (DMG named items like Flame Tongue, Holy Avenger) ---

    /// <summary>True if this is a named specific magic item from the DMG.</summary>
    public bool IsSpecificItem;

    /// <summary>The specific item type enum (e.g., FlameTongue, HolyAvenger). Only valid if IsSpecificItem is true.</summary>
    public SpecificItemType SpecificItemType;

    /// <summary>Reference to the specific item's definition data. Null for non-specific items.</summary>
    [System.NonSerialized]
    public SpecificItemDefinition SpecificItemData;

    /// <summary>Custom behavior script for specific magic items (combat hooks, activated abilities, etc.). Null for non-specific items.</summary>
    [System.NonSerialized]
    public SpecificItemBehavior SpecificItemBehavior;

    /// <summary>Check if this specific item has a named unique property.</summary>
    public bool HasSpecificProperty(string key)
    {
        return IsSpecificItem && SpecificItemData != null && SpecificItemData.HasProperty(key);
    }

    /// <summary>Get a typed unique property from the specific item definition.</summary>
    public T GetSpecificProperty<T>(string key, T defaultValue = default)
    {
        if (!IsSpecificItem || SpecificItemData == null) return defaultValue;
        return SpecificItemData.GetProperty(key, defaultValue);
    }

    // --- Consumable ---
    public ConsumableEffectType ConsumableEffect; // Generic effect type for extensibility
    public string ConsumableSpellName;            // Legacy spell identifier this consumable emulates
    public SpellID ConsumableSpellIDEnum          // Type-safe view over ConsumableSpellName for Phase 2 migration
    {
        get => ConsumableSpellName.ToSpellID();
        set => ConsumableSpellName = value.ToStorageString();
    }
    public int ConsumableMinimumCasterLevel = 1;  // Potions use minimum caster level by default (D&D 3.5e)
    public int ConsumableModifier;                // Generic +X modifier for spell-derived consumables
    public int HealAmount;      // Legacy flat HP restore fallback
    public int HealDiceCount;   // Number of healing dice (e.g., 1 for 1d8)
    public int HealDiceSides;   // Sides per healing die (e.g., 8 for 1d8)
    public int HealBonus;       // Flat healing bonus (e.g., +1)

    // --- Scroll-specific (D&D 3.5e DMG) ---
    /// <summary>True if this consumable is a spell scroll. Enables scroll validation rules.</summary>
    public bool IsScroll;
    /// <summary>Arcane or Divine scroll type. Determines which caster types can use it natively.</summary>
    public string ScrollType;   // "Arcane" or "Divine"
    /// <summary>The spell level on the scroll (0-9). Used for validation and pricing.</summary>
    public int ScrollSpellLevel;

    // --- Metamagic Scroll Data (crafted scrolls with metamagic) ---
    /// <summary>Metamagic feats applied to this scroll at creation time. Null/empty if no metamagic.</summary>
    public List<MetamagicFeatId> ScrollMetamagicFeats;
    /// <summary>Effective spell level after metamagic adjustments (base + metamagic). Equals ScrollSpellLevel if no metamagic.</summary>
    public int ScrollEffectiveSpellLevel;
    /// <summary>Save DC baked into this scroll at creation: 10 + effective spell level + caster's ability modifier.</summary>
    public int ScrollSavedDC;
    /// <summary>True if this scroll has metamagic applied (convenience check).</summary>
    public bool HasScrollMetamagic => ScrollMetamagicFeats != null && ScrollMetamagicFeats.Count > 0;

    // --- Unified Scroll Data (canonical source of truth) ---
    /// <summary>
    /// Unified scroll data container. Non-null for scroll items.
    /// All scroll creation paths populate this. Usage code should prefer Scroll.GetSpell()
    /// over ConsumableSpellName for spell resolution on scrolls.
    /// Legacy fields (IsScroll, ScrollType, etc.) are still populated for backward compatibility.
    /// </summary>
    public ScrollData Scroll;

    // --- Potion-specific (D&D 3.5e DMG) ---
    /// <summary>True if this consumable is a potion. Anyone can drink a potion (no class/ability restrictions).</summary>
    public bool IsPotion;
    /// <summary>The spell level of the potion's spell (0-3). Used for pricing and identification.</summary>
    public int PotionSpellLevel;

    // --- Wand-specific (D&D 3.5e DMG) ---
    /// <summary>True if this item is a wand. Wands are spell-trigger items requiring class spell list or UMD.</summary>
    public bool IsWand;
    /// <summary>Current number of charges remaining (starts at 50 for new wands).</summary>
    public int CurrentCharges;
    /// <summary>Maximum charges this wand can hold (always 50 for standard wands).</summary>
    public int MaxCharges = 50;
    /// <summary>The spell ID stored in this wand (used for spell resolution).</summary>
    public string WandSpellId;
    /// <summary>The caster level at which the wand casts its spell.</summary>
    public int WandCasterLevel;
    /// <summary>The spell level of the wand's spell (0-4).</summary>
    public int WandSpellLevel;

    // --- Staff-specific (D&D 3.5e DMG p.243 — Core rules only) ---
    // Staves are non-rechargeable under core DMG 3.5e rules.
    // Once all charges are expended, the staff becomes a non-magical quarterstaff (worthless).
    /// <summary>True if this item is a magic staff. Staves hold multiple spells with variable charge costs.</summary>
    public bool IsStaff;
    /// <summary>Key into StaffDatabase, e.g. "staff_of_fire". Null/empty if not a staff.</summary>
    public string StaffId;
    /// <summary>Current charges remaining. Starts at 50 (or 10 for some staves). Cannot be recharged under core rules.</summary>
    public int StaffCharges;
    /// <summary>The caster level at which the staff casts its spells (uses staff's CL, not wielder's).</summary>
    public int StaffCasterLevel;

    /// <summary>Look up this staff's definition from StaffDatabase. Returns null if not a staff.</summary>
    public StaffDefinition GetStaffDefinition()
    {
        if (!IsStaff || string.IsNullOrEmpty(StaffId)) return null;
        return StaffDatabase.GetStaff(StaffId);
    }

    /// <summary>True if this is a staff that has been fully expended (0 charges). Now non-magical.</summary>
    public bool IsStaffExpended()
    {
        return IsStaff && StaffCharges <= 0;
    }

    // --- Ring-specific (D&D 3.5e DMG pp. 229–233 — Core rules only) ---
    /// <summary>True if this item is a magic ring. Rings occupy the LeftRing or RightRing equipment slots.</summary>
    public bool IsRing;
    /// <summary>Key into RingDatabase, e.g. "ring_of_protection_3". Null/empty if not a ring.</summary>
    public string RingId;
    /// <summary>Deflection bonus to AC (+1 to +5 for Protection rings). 0 if not applicable.</summary>
    public int RingDeflectionBonus;
    /// <summary>Resistance bonus to all saving throws (+1 to +5 for Resistance rings). 0 if not applicable.</summary>
    public int RingResistanceSaveBonus;
    /// <summary>Shield bonus to AC (for Ring of Force Shield: +2). 0 if not applicable.</summary>
    public int RingShieldBonus;
    /// <summary>Energy type for Energy Resistance rings (Acid, Cold, Electricity, Fire, Sonic). Null if not applicable.</summary>
    public string RingEnergyType;
    /// <summary>Energy resistance amount (10=Minor, 20=Major, 30=Greater). 0 if not applicable.</summary>
    public int RingEnergyResistanceAmount;
    /// <summary>Competence bonus to a specific skill (+5 for Climbing/Swimming/Jumping, +10 for Chameleon Power). 0 if not applicable.</summary>
    public int RingSkillBonus;
    /// <summary>Skill name for competence bonus ("Climb", "Swim", "Jump", "Hide"). Null if not applicable.</summary>
    public string RingSkillName;
    /// <summary>Grants Evasion ability (Ring of Evasion). Default false.</summary>
    public bool RingGrantsEvasion;
    /// <summary>Grants continuous Freedom of Movement (Ring of Freedom of Movement). Default false.</summary>
    public bool RingGrantsFreedomOfMovement;
    /// <summary>Grants continuous Feather Fall (Ring of Feather Falling). Default false.</summary>
    public bool RingGrantsFeatherFall;
    /// <summary>Grants ability to walk on water (Ring of Water Walking). Default false.</summary>
    public bool RingGrantsWaterWalking;
    /// <summary>Grants no need for food, water, or sleep (Ring of Sustenance). Default false.</summary>
    public bool RingGrantsSustenance;
    /// <summary>Grants immunity to detect thoughts, discern lies, alignment detection (Ring of Mind Shielding). Default false.</summary>
    public bool RingGrantsMindShielding;
    /// <summary>Grants resistance to cold environments (Ring of Warmth). Default false.</summary>
    public bool RingGrantsColdEndurance;
    /// <summary>Caster level of the ring's magical effect. Used for dispel checks.</summary>
    public int RingCasterLevel;

    // --- Sprint 2: Active Ring Abilities (D&D 3.5e DMG pp. 229–233) ---
    /// <summary>List of activatable abilities on this ring. Null/empty for passive-only rings.</summary>
    public System.Collections.Generic.List<RingAbility> RingAbilities;
    /// <summary>Unique instance ID for this specific ring item (for use tracking). Auto-generated if empty.</summary>
    public string RingInstanceId;
    /// <summary>Current charge count for charge-based rings (Ring of the Ram). 0 if not charge-based.</summary>
    public int RingCurrentCharges;
    /// <summary>Maximum charge capacity (Ring of the Ram: 50). 0 if not charge-based.</summary>
    public int RingMaxCharges;
    /// <summary>Charges regenerated per day on rest (Ring of the Ram: 10 → rolls 1d10). 0 if no regen.</summary>
    public int RingChargesPerDay;
    /// <summary>Remaining Spell Turning reflection pool (Ring of Spell Turning: 1d4+6 = 7–10).</summary>
    public int RingSpellTurningPool;
    /// <summary>Whether the bound Djinni has been slain (Ring of Djinni Calling becomes permanently inert).</summary>
    public bool RingDjinniSlain;
    /// <summary>Whether the Djinni is currently summoned (Ring of Djinni Calling).</summary>
    public bool RingDjinniSummoned;

    // ── Sprint 3: Complex Mechanics Ring Fields (Tier 3) ──

    /// <summary>Ring of Counterspells: SpellId of the stored counterspell (empty = no spell stored).</summary>
    public string RingCounterspellStored = "";
    /// <summary>Ring of Counterspells: Display name of the stored counterspell.</summary>
    public string RingCounterspellStoredName = "";
    /// <summary>Ring of Counterspells: Level of the stored counterspell.</summary>
    public int RingCounterspellStoredLevel;

    /// <summary>Ring of Spell Storing: List of spells currently stored in the ring.</summary>
    public List<StoredSpell> StoredSpells;
    /// <summary>Ring of Spell Storing: Maximum total spell levels that can be stored (3 = Minor, 5 = Major).</summary>
    public int MaxStoredSpellLevels;

    /// <summary>Ring of Wizardry: Which arcane spell level this ring doubles (1–4, 0 = none).</summary>
    public int RingWizardryLevel;

    /// <summary>Ring of Regeneration: Whether regeneration is active on this ring.</summary>
    public bool RingHasRegeneration;

    // ════════════════════════════════════════════════════════════
    //  ROD FIELDS (D&D 3.5e DMG pp. 224–228)
    //  All 36 DMG rods: 21 metamagic + 7 combat + 5 utility + 3 legendary
    // ════════════════════════════════════════════════════════════

    /// <summary>True if this item is a rod. Gates rod-specific UI/mechanics.</summary>
    public bool IsRod;
    /// <summary>Key into RodDatabase, e.g. "rod_empower_lesser". Null/empty if not a rod.</summary>
    public string RodId;
    /// <summary>Rod category: Metamagic, Combat, Utility, Legendary.</summary>
    public RodCategory RodCategory;
    /// <summary>Caster level for the rod's abilities. Most rods: CL 17th.</summary>
    public int RodCasterLevel;
    /// <summary>True for legendary rods (Alertness, Lordly Might, Security).</summary>
    public bool IsLegendary;

    // ── Metamagic Rod Fields ──────────────────────────────────
    /// <summary>True for all 21 metamagic rods.</summary>
    public bool RodIsMetamagic;
    /// <summary>Which metamagic feat this rod applies (e.g., EmpowerSpell).</summary>
    public MetamagicFeatId RodMetamagicType;
    /// <summary>Power level: Lesser (≤3rd), Normal (≤6th), Greater (≤9th).</summary>
    public RodPowerLevel RodPower;
    /// <summary>Maximum base spell level this rod can affect (3, 6, or 9).</summary>
    public int RodMaxSpellLevel;
    /// <summary>The slot level increase this metamagic normally costs (for display).</summary>
    public int RodSlotLevelIncrease;
    /// <summary>Daily uses (standard: 3/day for metamagic rods).</summary>
    public int RodUsesPerDay;
    /// <summary>Uses consumed today. Reset at dawn.</summary>
    public int RodUsesToday;

    // ── Rod of Absorption Fields ──────────────────────────────
    /// <summary>True for Rod of Absorption.</summary>
    public bool RodCanAbsorbSpells;
    /// <summary>Current absorbed spell levels (0–50).</summary>
    public int RodAbsorbedLevels;
    /// <summary>Maximum absorbed spell levels (default 50).</summary>
    public int RodMaxAbsorbedLevels = 50;

    // ── Rod of Cancellation Fields ────────────────────────────
    /// <summary>True for Rod of Cancellation.</summary>
    public bool RodCanCancelMagic;
    /// <summary>True after Rod of Cancellation has been used (becomes nonmagical).</summary>
    public bool RodIsExpended;

    // ── Rod of Flailing Fields ────────────────────────────────
    /// <summary>True for Rod of Flailing.</summary>
    public bool RodIsFlail;
    /// <summary>Weapon enhancement bonus for combat rods.</summary>
    public int RodWeaponEnhancement;
    /// <summary>Weapon damage dice string (e.g., "1d8").</summary>
    public string RodWeaponDamageDice;
    /// <summary>Current weapon mode string for display.</summary>
    public string RodWeaponMode;
    /// <summary>Deflection bonus when Rod of Flailing is in dire flail mode.</summary>
    public int RodFlailDeflectionBonus;

    // ── Immovable Rod Fields ──────────────────────────────────
    /// <summary>True for Immovable Rod.</summary>
    public bool RodIsImmovable;
    /// <summary>Whether the Immovable Rod button is pressed (locked in place).</summary>
    public bool RodIsActivated;
    /// <summary>Maximum weight the rod can hold (default 8,000 lbs).</summary>
    public int RodHoldWeightLbs = 8000;
    /// <summary>DC of Strength check to move the activated rod.</summary>
    public int RodMoveDC = 30;

    // ── Rod of Lordly Might Fields ────────────────────────────
    /// <summary>True for Rod of Lordly Might.</summary>
    public bool RodIsLordlyMight;
    /// <summary>Current weapon mode (cast to LordlyMightWeaponMode).</summary>
    public int RodLordlyMightMode;
    /// <summary>Fear cone save DC (default 16).</summary>
    public int RodFearConeDC = 16;
    /// <summary>Fear cone range in feet (default 30).</summary>
    public int RodFearConeRangeFt = 30;
    /// <summary>Fear cone uses per day (default 2).</summary>
    public int RodFearUsesPerDay;
    /// <summary>Fear cone uses consumed today.</summary>
    public int RodFearUsesToday;

    // ── Rod of Metal/Mineral Detection and Enemy Detection ────
    /// <summary>True for Rod of Metal and Mineral Detection.</summary>
    public bool RodCanDetectMetals;
    /// <summary>True for Rod of Enemy Detection.</summary>
    public bool RodCanDetectEnemies;
    /// <summary>Detection radius in feet.</summary>
    public float RodDetectionRadiusFt;
    /// <summary>How far detection penetrates stone (feet).</summary>
    public float RodPenetratesStoneFt;

    // ── Rod of Splendor Fields ────────────────────────────────
    /// <summary>True for Rod of Splendor.</summary>
    public bool RodIsSplendor;
    /// <summary>Tent uses per week (default 1).</summary>
    public int RodSplendorTentUsesPerWeek;
    /// <summary>Tent uses consumed this week.</summary>
    public int RodSplendorTentUsesThisWeek;
    /// <summary>Clothes per week (default 7).</summary>
    public int RodSplendorClothesPerWeek;
    /// <summary>Clothes consumed this week.</summary>
    public int RodSplendorClothesThisWeek;
    /// <summary>Feast uses per day (default 1).</summary>
    public int RodSplendorFeastUsesPerDay;
    /// <summary>Feast uses consumed today.</summary>
    public int RodSplendorFeastUsesToday;
    /// <summary>Enhancement bonus to Charisma while holding rod (default +4).</summary>
    public int RodSplendorCharismaBonus;

    // ── Rod of Alertness Fields ───────────────────────────────
    /// <summary>True for Rod of Alertness.</summary>
    public bool RodIsAlertness;
    /// <summary>Insight bonus to Initiative.</summary>
    public int RodInsightBonusInit;
    /// <summary>Insight bonus to Listen checks.</summary>
    public int RodInsightBonusListen;
    /// <summary>Grants See Invisible at will.</summary>
    public bool RodGrantsSeeInvisible;
    /// <summary>Grants Detect Evil at will.</summary>
    public bool RodGrantsDetectEvil;
    /// <summary>Grants Detect Magic at will.</summary>
    public bool RodGrantsDetectMagic;
    /// <summary>Grants Light at will.</summary>
    public bool RodGrantsLight;
    /// <summary>Animate Objects uses per day (default 1).</summary>
    public int RodAnimateUsesPerDay;
    /// <summary>Animate Objects uses consumed today.</summary>
    public int RodAnimateUsesToday;
    /// <summary>Prayer uses per day (default 1).</summary>
    public int RodPrayerUsesPerDay;
    /// <summary>Prayer uses consumed today.</summary>
    public int RodPrayerUsesToday;

    // ── Rod of Negation Fields ────────────────────────────────
    /// <summary>True for Rod of Negation.</summary>
    public bool RodIsNegation;
    /// <summary>Caster level for dispel checks (default 15).</summary>
    public int RodDispelCL;
    /// <summary>Greater Dispel Magic uses per day (default 2).</summary>
    public int RodGreaterDispelUsesPerDay;
    /// <summary>Greater Dispel Magic uses consumed today.</summary>
    public int RodGreaterDispelUsesToday;

    // ── Rod of Python Fields ──────────────────────────────────
    /// <summary>True for Rod of Python.</summary>
    public bool RodCanTransformToSnake;
    /// <summary>Whether the rod is currently in snake form.</summary>
    public bool RodIsInSnakeForm;
    /// <summary>Snake form current HP.</summary>
    public int RodSnakeHP;
    /// <summary>Snake form maximum HP.</summary>
    public int RodSnakeMaxHP;
    /// <summary>Snake form AC.</summary>
    public int RodSnakeAC;
    /// <summary>Snake form attack bonus.</summary>
    public int RodSnakeAttackBonus;
    /// <summary>Snake form damage dice string.</summary>
    public string RodSnakeDamage;
    /// <summary>Snake has constrict ability.</summary>
    public bool RodSnakeHasConstrict;
    /// <summary>Snake constrict damage dice string.</summary>
    public string RodSnakeConstrictDamage;

    // ── Rod of Security Fields ────────────────────────────────
    /// <summary>True for Rod of Security.</summary>
    public bool RodCanCreateDemiplane;
    /// <summary>Maximum capacity of demiplane (people).</summary>
    public int RodDemiplaneCapacity;
    /// <summary>Total person-days available in demiplane.</summary>
    public int RodDemiplanePersonDays;
    /// <summary>Whether the demiplane provides healing.</summary>
    public bool RodDemiplaneHeals;
    /// <summary>Demiplane uses per week (default 1).</summary>
    public int RodDemiplaneUsesPerWeek;
    /// <summary>Demiplane uses consumed this week.</summary>
    public int RodDemiplaneUsesThisWeek;

    /// <summary>Helper: True if this is a rod item.</summary>
    public bool IsRodItem => IsRod;

    // ── Wondrous Item Fields (D&D 3.5e DMG pp. 248–271) ──

    /// <summary>True if this item is a wondrous item. Gates wondrous-specific UI/mechanics.</summary>
    public bool IsWondrous;
    /// <summary>Key into WondrousItemDatabase, e.g. "headband_of_intellect_2". Null/empty if not wondrous.</summary>
    public string WondrousId;
    /// <summary>Unique instance ID for this specific wondrous item (for use tracking). Auto-generated if empty.</summary>
    public string WondrousInstanceId;
    /// <summary>Category of wondrous item (ability, armor, movement, storage, skill, save, ac, utility).</summary>
    public string WondrousItemType;
    /// <summary>Which slot this wondrous item occupies (matching EquipSlot). Slotless for no-slot items.</summary>
    public EquipSlot WondrousRequiredSlot;
    /// <summary>True if this item does not occupy a body slot (Ioun Stones, bags, consumables).</summary>
    public bool IsSlotless;

    // --- Ability Score Enhancement ---
    /// <summary>Enhancement bonus to an ability score (+2/+4/+6 for stat items). 0 if not applicable.</summary>
    public int WondrousAbilityBonus;
    /// <summary>Which ability score is enhanced ("Str", "Dex", "Con", "Int", "Wis", "Cha"). Null if N/A.</summary>
    public string WondrousAbilityType;

    // --- AC Bonuses ---
    /// <summary>Armor class bonus from wondrous item (Bracers of Armor, Amulet of Natural Armor).</summary>
    public int WondrousACBonus;
    /// <summary>Type of AC bonus: "armor" (Bracers), "natural" (Amulet), "deflection", "shield".</summary>
    public string WondrousACBonusType;

    // --- Saving Throw Bonuses ---
    /// <summary>Bonus to saving throws (Cloak of Resistance). 0 if not applicable.</summary>
    public int WondrousSaveBonus;
    /// <summary>Which saves: "all", "fort", "ref", "will". Null if N/A.</summary>
    public string WondrousSaveType;

    // --- Skill Bonuses ---
    /// <summary>Competence/enhancement bonus to a skill. 0 if not applicable.</summary>
    public int WondrousSkillBonus;
    /// <summary>Name of the skill boosted ("Hide", "Move Silently", "Spot", etc.). Null if N/A.</summary>
    public string WondrousSkillName;
    /// <summary>Secondary skill bonus (for items boosting two skills like Gloves of Swimming and Climbing).</summary>
    public int WondrousSkillBonus2;
    /// <summary>Secondary skill name. Null if N/A.</summary>
    public string WondrousSkillName2;
    /// <summary>Type of skill bonus: "competence", "enhancement", "circumstance", "insight". Default: "competence".</summary>
    public string WondrousSkillBonusType;

    // --- Movement ---
    /// <summary>Whether this item grants a special movement mode (fly, spider climb, etc.).</summary>
    public bool WondrousGrantsMovement;
    /// <summary>Movement mode granted: "fly", "spider_climb", "water_walk", "teleport", "levitate".</summary>
    public string WondrousMovementMode;
    /// <summary>Movement speed in feet (30 for spider climb, 60 for fly, etc.). 0 if no speed.</summary>
    public int WondrousMovementSpeed;
    /// <summary>Flight maneuverability: "clumsy", "poor", "average", "good", "perfect". Only for fly mode.</summary>
    public string WondrousFlightManeuverability;
    /// <summary>Whether this item grants haste effect (Boots of Speed). Tracks rounds per day.</summary>
    public bool WondrousGrantsHaste;
    /// <summary>Max rounds of haste per day (Boots of Speed = 10). 0 if N/A.</summary>
    public int WondrousHasteMaxRounds;
    /// <summary>Haste rounds used today (runtime tracking, resets on rest).</summary>
    public int WondrousHasteRoundsUsedToday;
    /// <summary>Whether haste is currently active on this item.</summary>
    public bool WondrousHasteCurrentlyActive;
    /// <summary>Duration of each flight use in rounds (Winged Boots = 50). 0 = unlimited.</summary>
    public int WondrousFlightDurationRounds;
    /// <summary>Flight rounds remaining for current activation. 0 if not active or unlimited.</summary>
    public int WondrousFlightRoundsRemaining;
    /// <summary>Whether this item grants cold endurance (Boots of the Winterlands).</summary>
    public bool WondrousGrantsColdEndurance;
    /// <summary>Survival bonus in cold environments (Boots of the Winterlands: +10).</summary>
    public int WondrousColdSurvivalBonus;
    /// <summary>Teleport weight limit per use in lbs (Boots of Teleportation: 300). 0 if N/A.</summary>
    public int WondrousTeleportWeightLimit;

    // --- Activation ---
    /// <summary>Whether this item requires activation (command word, use-activated).</summary>
    public bool WondrousHasActivation;
    /// <summary>Activation type: "passive", "command_word", "use_activated", "continuous".</summary>
    public string WondrousActivationType;
    /// <summary>Number of uses per day (0 = unlimited/continuous). -1 = single use (consumable).</summary>
    public int WondrousUsesPerDay;
    /// <summary>Uses expended today (runtime tracking, resets on rest).</summary>
    public int WondrousUsesToday;

    // --- Container Properties (Bags of Holding, Handy Haversack, Portable Hole) ---
    /// <summary>Maximum weight capacity in lbs for container items (250/500/1000/1500 for Bags of Holding).</summary>
    public float WondrousWeightCapacity;
    /// <summary>Maximum volume capacity in cubic feet (30/70/150/250 for Bags of Holding).</summary>
    public float WondrousVolumeCapacity;
    /// <summary>Whether this is an extradimensional space (Bag of Holding, Portable Hole). Nesting = disaster.</summary>
    public bool WondrousIsExtradimensional;
    /// <summary>Apparent weight (5 lbs for Bag of Holding, 5 lbs for Handy Haversack).</summary>
    public float WondrousApparentWeight;
    /// <summary>Whether this container allows quick retrieval (move action instead of standard).
    /// Applies to Handy Haversack and Efficient Quiver.</summary>
    public bool WondrousQuickRetrievalEnabled;

    // --- Ioun Stone Properties ---
    /// <summary>Whether this is an Ioun Stone (orbits head, targetable AC 24, 10 HP).</summary>
    public bool IsIounStone;
    /// <summary>Ioun Stone sub-type for specific effects (e.g., "dusty_rose_prism").</summary>
    public string IounStoneType;

    // --- Caster Level & Charges ---
    /// <summary>Caster level of the wondrous item's magical effect. Used for dispel checks.</summary>
    public int WondrousCasterLevel;
    /// <summary>Current charges for charge-based items (Scarab of Protection: 12). 0 if not charge-based.</summary>
    public int WondrousCurrentCharges;
    /// <summary>Maximum charge capacity. 0 if not charge-based.</summary>
    public int WondrousMaxCharges;

    // --- Misc Flags ---
    /// <summary>Bonus to attack rolls for unarmed/natural attacks (Amulet of Mighty Fists).</summary>
    public int WondrousMightyFistsBonus;
    /// <summary>Speed bonus in feet (Boots of Striding: +10 base land speed). 0 if N/A.</summary>
    public int WondrousSpeedBonus;
    /// <summary>Displacement miss chance percentage (Cloak of Displacement: 20 minor, 50 major). 0 if N/A.</summary>
    public int WondrousDisplacementMissChance;
    /// <summary>Grants darkvision (Goggles of Night). Range in feet (60). 0 if N/A.</summary>
    public int WondrousDarkvisionRange;
    /// <summary>Bonus to caster level checks to overcome spell resistance (Robe of the Archmagi).</summary>
    public int WondrousSpellResistanceBonus;
    /// <summary>Spell Resistance granted by this item (0 = none).</summary>
    public int WondrousGrantsSR;

    // --- Bow Attack Bonuses (Bracers of Archery) ---
    /// <summary>Competence bonus to attack rolls with bows only (Bracers of Archery). 0 if N/A.</summary>
    public int WondrousBowAttackBonus;
    /// <summary>Competence bonus to damage rolls with bows only (Greater Bracers of Archery). 0 if N/A.</summary>
    public int WondrousBowDamageBonus;

    // --- Consumable Bead/Ball Tracking (Necklace of Fireballs, Beads of Force) ---
    /// <summary>List of remaining bead damage dice (e.g., [5,5,5] for Necklace Type I with three 5d6 beads).
    /// Each entry is the number of d6 dice for that bead. Remove entry when used. Null if not bead-tracked.</summary>
    public List<int> WondrousBeadDamageDice;
    /// <summary>Reflex save DC for bead effects (Necklace of Fireballs: 14-17, Beads of Force: 16).</summary>
    public int WondrousBeadSaveDC;
    /// <summary>Damage type for bead attacks ("fire" for necklace, "force" for beads of force).</summary>
    public string WondrousBeadDamageType;
    /// <summary>Radius of bead effect in feet (20 for necklace fireballs, 10 for beads of force).</summary>
    public int WondrousBeadRadius;

    // --- Weekly/Monthly Use Tracking ---
    /// <summary>Maximum uses per week (Bag of Tricks: 3, Figurines: 1-3). 0 = not weekly-tracked.</summary>
    public int WondrousUsesPerWeek;
    /// <summary>Uses expended this week (runtime tracking). Resets on weekly rest cycle.</summary>
    public int WondrousUsesThisWeek;
    /// <summary>Maximum uses per month (Marble Elephant: 4). 0 = not monthly-tracked.</summary>
    public int WondrousUsesPerMonth;
    /// <summary>Uses expended this month (runtime tracking).</summary>
    public int WondrousUsesThisMonth;

    // --- Summoning Properties (Bag of Tricks, Figurines, Elemental Gems) ---
    /// <summary>Whether this item can summon a creature.</summary>
    public bool WondrousCanSummon;
    /// <summary>List of possible creature IDs to summon (random selection for Bag of Tricks).
    /// For figurines, single-entry list. For Bag of Tricks, 5 possible creatures.</summary>
    public List<string> WondrousSummonCreatureIds;
    /// <summary>Duration of summoned creature in rounds (10 min = 100, 1 hr = 600, etc.). 0 = permanent until dismissed.</summary>
    public int WondrousSummonDurationRounds;
    /// <summary>Whether summoned creature is a mount (can be ridden).</summary>
    public bool WondrousSummonIsMountable;
    /// <summary>Descriptive label for the summoned creature (for tooltips).</summary>
    public string WondrousSummonDescription;

    // --- Entrapment (Beads of Force, Iron Bands of Binding) ---
    /// <summary>Whether this item creates an entrapment effect on use.</summary>
    public bool WondrousCreatesEntrapment;
    /// <summary>Save DC for entrapment (Fort for Beads of Force, Reflex for Iron Bands).</summary>
    public int WondrousEntrapmentSaveDC;
    /// <summary>Save type for entrapment: "fort", "reflex", "will".</summary>
    public string WondrousEntrapmentSaveType;
    /// <summary>Strength DC to break free of entrapment (Iron Bands: 30, Beads of Force: N/A uses Disintegrate).</summary>
    public int WondrousEntrapmentBreakDC;
    /// <summary>Duration of entrapment in rounds (Beads of Force: 100 = 10 min). 0 = until broken.</summary>
    public int WondrousEntrapmentDurationRounds;

    // --- Ioun Stone / Phase 9 Properties ---
    /// <summary>Insight bonus to AC from wondrous item (Dusty Rose Prism: +1). 0 if N/A.</summary>
    public int WondrousInsightACBonus;
    /// <summary>Competence bonus to all saving throws (Pale Green Prism: +1). 0 if N/A.</summary>
    public int WondrousCompetenceSaveBonus;
    /// <summary>Caster level bonus from wondrous item (Orange Prism: +1, Robe of Archmagi: +2). 0 if N/A.</summary>
    public int WondrousCasterLevelBonus;
    /// <summary>HP regeneration per hour (Pearly White Spindle: 1). 0 if N/A.</summary>
    public int WondrousRegenPerHour;
    /// <summary>Feat name granted by this item (Dark Blue Rhomboid: "Alertness"). Null if N/A.</summary>
    public string WondrousGrantsFeatName;
    /// <summary>Whether this item sustains without food/water (Clear Spindle).</summary>
    public bool WondrousSustainsWithoutFood;
    /// <summary>Whether this item sustains without air (Iridescent Spindle).</summary>
    public bool WondrousSustainsWithoutAir;
    /// <summary>Max spell levels this item can store (Vibrant Purple Prism: 3). 0 if N/A.</summary>
    public int WondrousSpellStorageLevels;
    /// <summary>Max spell level this item can absorb (Pale Lavender: 4, Lavender & Green: 8). 0 if N/A.</summary>
    public int WondrousSpellAbsorptionMaxLevel;
    /// <summary>Charges remaining for spell absorption. 0 = not applicable or exhausted.</summary>
    public int WondrousSpellAbsorptionCharges;
    /// <summary>Max charges for spell absorption (default: starts at 20 or 50).</summary>
    public int WondrousSpellAbsorptionMaxCharges;

    // --- Phase 10: Complex Multi-Ability Properties ---
    /// <summary>Whether this item grants poison immunity (Periapt of Proof Against Poison, Cloak of Arachnida).</summary>
    public bool WondrousGrantsPoisonImmunity;
    /// <summary>Whether this item grants disease immunity (Periapt of Health).</summary>
    public bool WondrousGrantsDiseaseImmunity;
    /// <summary>Whether this item grants web immunity (Cloak of Arachnida).</summary>
    public bool WondrousGrantsWebImmunity;
    /// <summary>Luck bonus to Fort saves vs poison (Cloak of Arachnida: +2). 0 if N/A.</summary>
    public int WondrousLuckFortSaveBonus;
    /// <summary>Resistance bonus to all saves from wondrous item (Robe of Archmagi: +4). 0 if N/A.</summary>
    public int WondrousResistanceSaveBonus;
    /// <summary>Required alignment for this item ("good", "neutral", "evil"). Null = no restriction.</summary>
    public string WondrousRequiredAlignment;
    /// <summary>Penalty to AC if worn by wrong alignment (Robe of Archmagi: -4). 0 if N/A.</summary>
    public int WondrousWrongAlignmentACPenalty;
    /// <summary>Penalty to saves if worn by wrong alignment (Robe of Archmagi: -2). 0 if N/A.</summary>
    public int WondrousWrongAlignmentSavePenalty;
    /// <summary>Number of consumable patches remaining (Robe of Bones, Robe of Useful Items, Robe of Stars).</summary>
    public int WondrousPatchesRemaining;
    /// <summary>Maximum patches this item had when new.</summary>
    public int WondrousPatchesMax;
    /// <summary>Description of patch contents for tooltips.</summary>
    public string WondrousPatchDescription;
    /// <summary>Whether this item grants spider climb (Cloak of Arachnida).</summary>
    public bool WondrousGrantsSpiderClimb;
    /// <summary>Whether this item grants See Invisible (Robe of Eyes).</summary>
    public bool WondrousGrantsSeeInvisible;
    /// <summary>Whether wearer cannot be flanked (Robe of Eyes).</summary>
    public bool WondrousPreventsFlanking;
    /// <summary>Bonus to Search checks (Robe of Eyes: +10). 0 if N/A.</summary>
    public int WondrousSearchBonus;
    /// <summary>Bonus to Spot checks (Robe of Eyes: +10). 0 if N/A.</summary>
    public int WondrousSpotBonus;
    /// <summary>Bonus to Disguise checks (Robe of Blending: +10). 0 if N/A.</summary>
    public int WondrousDisguiseBonus;
    /// <summary>Detect Thoughts save DC (Helm of Telepathy: 13). 0 if N/A.</summary>
    public int WondrousDetectThoughtsDC;
    /// <summary>Suggestion save DC (Helm of Telepathy: 14). 0 if N/A.</summary>
    public int WondrousSuggestionDC;
    /// <summary>Ability bonus granted to Wisdom for monks (Monk's Belt: +2 to AC). Uses WondrousAbilityBonus for WIS.</summary>
    public int WondrousMonkLevelBonus;
    /// <summary>Rolled AC bonus (Robe of Stars: 1d6 rolled on equip). 0 until equipped.</summary>
    public int WondrousRolledACBonus;
    /// <summary>Whether underwater vision is granted (Helm of Underwater Action). Range in ft.</summary>
    public int WondrousUnderwaterVisionRange;
    /// <summary>Whether freedom of movement in water is granted.</summary>
    public bool WondrousWaterFreedomOfMovement;

    // --- Phase 2/3: Spell-like abilities and Scarab charge tracking ---

    /// <summary>Names of spell-like abilities this item grants (e.g., "Bless,Detect Evil,Remove Fear,Aid").</summary>
    public string WondrousSpellLikeAbilities;
    /// <summary>Caster level for spell-like abilities.</summary>
    public int WondrousSpellLikeCasterLevel;
    /// <summary>Uses per day for each spell-like ability (typically 1).</summary>
    public int WondrousSpellLikeUsesPerDay;
    /// <summary>Comma-separated list of today's remaining uses per spell-like ability.</summary>
    public string WondrousSpellLikeUsesToday;

    /// <summary>True if this Scarab absorbs death effects.</summary>
    public bool WondrousScarabAbsorbsDeath;
    /// <summary>True if this Scarab absorbs energy drain.</summary>
    public bool WondrousScarabAbsorbsDrain;
    /// <summary>True if this Scarab absorbs negative energy.</summary>
    public bool WondrousScarabAbsorbsNegativeEnergy;

    // --- Phase 4/5: Planar Travel Items ---

    /// <summary>Plane destinations for Cubic Gate sides (6 elements). Stored as int[] (Plane enum cast).</summary>
    public int[] WondrousCubicGateSides;
    /// <summary>Uses this week per Cubic Gate side (6 elements). Each side gets 3/week.</summary>
    public int[] WondrousCubicGateUsesThisWeek;
    /// <summary>Max uses per week per side (typically 3).</summary>
    public int WondrousCubicGateMaxUsesPerSide = 3;

    /// <summary>True if this is a Well of Many Worlds.</summary>
    public bool WondrousIsWellOfManyWorlds;
    /// <summary>True if the Well is currently open (portal active).</summary>
    public bool WondrousWellIsOpen;
    /// <summary>Current destination plane for the open Well (Plane enum cast to int). -1 = none.</summary>
    public int WondrousWellCurrentDestination = -1;

    /// <summary>True if this is a Carpet of Flying.</summary>
    public bool WondrousIsCarpetOfFlying;
    /// <summary>Carpet size in feet (e.g., 5, 10).</summary>
    public int WondrousCarpetSizeFeet;
    /// <summary>Max weight capacity in lbs.</summary>
    public int WondrousCarpetCapacityLbs;
    /// <summary>Carpet fly speed in feet.</summary>
    public int WondrousCarpetFlySpeed;
    /// <summary>Carpet flight maneuverability (e.g., "average").</summary>
    public string WondrousCarpetManeuverability;
    /// <summary>True if carpet is currently flying.</summary>
    public bool WondrousCarpetIsFlying;

    /// <summary>Plane shift mishap chance in percent (e.g., 5 for Amulet of the Planes).</summary>
    public int WondrousPlaneShiftMishapPercent;
    /// <summary>Max travelers for plane shift (e.g., 8 for Amulet of the Planes).</summary>
    public int WondrousPlaneShiftMaxTravelers;
    /// <summary>True if this item grants at-will plane shifting.</summary>
    public bool WondrousGrantsPlaneShift;

    // --- Phase 6–8: Creature Trapping Items ---

    /// <summary>List of creatures currently trapped in this item. Null if not a trapping item.</summary>
    public System.Collections.Generic.List<TrappedCreature> WondrousTrappedCreatures;
    /// <summary>Maximum number of creatures this item can hold (1 for Flask/Bottle, 15 for Mirror).</summary>
    public int WondrousMaxTrappedCreatures;
    /// <summary>Save DC to resist trapping (19 for Flask/Bottle, 23 for Mirror).</summary>
    public int WondrousTrapSaveDC;
    /// <summary>Save type for trapping ("Will" or "Reflex").</summary>
    public string WondrousTrapSaveType;
    /// <summary>Range of trapping effect in feet (60 for Flask, 50 for Mirror).</summary>
    public int WondrousTrapRangeFeet;
    /// <summary>True if this item can trap any creature type (Iron Flask).</summary>
    public bool WondrousTrapAnyType;
    /// <summary>Comma-separated allowed creature types for trapping (e.g., "Outsider").</summary>
    public string WondrousTrapAllowedTypes;
    /// <summary>Service duration in minutes when creature is released friendly (default 60).</summary>
    public float WondrousTrapServiceMinutes = 60f;
    /// <summary>True if this item starts with a pre-loaded creature (Efreeti Bottle).</summary>
    public bool WondrousTrapHasDefaultCreature;
    /// <summary>Type of default creature ("Efreeti", "ElderEarthElemental").</summary>
    public string WondrousTrapDefaultCreatureType;
    /// <summary>Control range for area control abilities (Stone of Controlling Earth Elementals).</summary>
    public int WondrousTrapControlRangeFeet;
    /// <summary>Control save DC for area control.</summary>
    public int WondrousTrapControlSaveDC;
    /// <summary>True if this item can summon/control earth elementals (Stone).</summary>
    public bool WondrousControlsEarthElementals;

    // ── Phase 9: Mirror of Opposition ──
    /// <summary>True if this is a Mirror of Opposition.</summary>
    public bool WondrousIsMirrorOfOpposition;
    /// <summary>True if an evil duplicate is currently active.</summary>
    public bool WondrousMirrorDuplicateActive;
    /// <summary>Unique ID of the active duplicate character (if any).</summary>
    public string WondrousMirrorDuplicateID;
    /// <summary>Rounds of delay before duplicate appears (rolled 1d4 on activation).</summary>
    public int WondrousMirrorDelayRounds;

    // ── Phase 10: Mirror of Mental Prowess ──
    /// <summary>True if this is a Mirror of Mental Prowess.</summary>
    public bool WondrousIsMirrorOfMentalProwess;
    /// <summary>Enhancement bonus to Int/Wis/Cha for creatures within aura range.</summary>
    public int WondrousMirrorMentalBonus;
    /// <summary>Range (ft) of the mental stat aura.</summary>
    public int WondrousMirrorMentalBonusRange;
    /// <summary>Scrying uses remaining today.</summary>
    public int WondrousMirrorScryingUsesToday;
    /// <summary>Detect Thoughts uses remaining today.</summary>
    public int WondrousMirrorDetectThoughtsUsesToday;
    /// <summary>Suggestion uses remaining today.</summary>
    public int WondrousMirrorSuggestionUsesToday;
    /// <summary>Telepathy uses remaining today.</summary>
    public int WondrousMirrorTelepathyUsesToday;
    /// <summary>Will DC for scrying ability.</summary>
    public int WondrousMirrorScryingDC;
    /// <summary>Will DC for detect thoughts ability.</summary>
    public int WondrousMirrorDetectThoughtsDC;
    /// <summary>Will DC for suggestion ability.</summary>
    public int WondrousMirrorSuggestionDC;
    /// <summary>Range (ft) for detect thoughts and telepathy.</summary>
    public int WondrousMirrorTelepathyRange;

    // ── Phase 11: Construct Guardians ──
    /// <summary>True if this is an Iron Cobra construct guardian.</summary>
    public bool WondrousIsIronCobra;
    /// <summary>Iron Cobra current HP.</summary>
    public int WondrousIronCobraCurrentHP;
    /// <summary>Iron Cobra max HP.</summary>
    public int WondrousIronCobraMaxHP;
    /// <summary>Iron Cobra AC.</summary>
    public int WondrousIronCobraAC;
    /// <summary>Iron Cobra attack bonus.</summary>
    public int WondrousIronCobraAttackBonus;
    /// <summary>Iron Cobra damage dice (e.g. "1d3").</summary>
    public string WondrousIronCobraDamageDice;
    /// <summary>Iron Cobra fast healing per round.</summary>
    public int WondrousIronCobraFastHealing;
    /// <summary>Iron Cobra poison Fort save DC.</summary>
    public int WondrousIronCobraPoisonDC;
    /// <summary>Iron Cobra poison damage dice (e.g. "1d6 Con").</summary>
    public string WondrousIronCobraPoisonDamage;
    /// <summary>Iron Cobra guard radius in feet.</summary>
    public int WondrousIronCobraGuardRadius;
    /// <summary>True if Iron Cobra is currently active/deployed.</summary>
    public bool WondrousIronCobraIsActive;

    /// <summary>True if this is a Stone Horse.</summary>
    public bool WondrousIsStoneHorse;
    /// <summary>Stone Horse variant: "Courser", "Destrier", or "Griffon".</summary>
    public string WondrousStoneHorseType;
    /// <summary>Stone Horse land speed (ft).</summary>
    public int WondrousStoneHorseSpeed;
    /// <summary>Stone Horse fly speed (0 if cannot fly).</summary>
    public int WondrousStoneHorseFlySpeed;
    /// <summary>Stone Horse flight maneuverability (if applicable).</summary>
    public string WondrousStoneHorseManeuverability;
    /// <summary>Stone Horse AC.</summary>
    public int WondrousStoneHorseAC;
    /// <summary>Stone Horse max HP.</summary>
    public int WondrousStoneHorseMaxHP;
    /// <summary>Stone Horse current HP.</summary>
    public int WondrousStoneHorseCurrentHP;
    /// <summary>Stone Horse Strength score.</summary>
    public int WondrousStoneHorseSTR;
    /// <summary>Stone Horse Dexterity score.</summary>
    public int WondrousStoneHorseDEX;
    /// <summary>Stone Horse Constitution score.</summary>
    public int WondrousStoneHorseCON;
    /// <summary>True if the Stone Horse is currently active (not stone form).</summary>
    public bool WondrousStoneHorseIsActive;

    // ── Phase 12: Apparatus of Kwalish ──
    /// <summary>True if this is the Apparatus of Kwalish.</summary>
    public bool WondrousIsApparatusOfKwalish;
    /// <summary>Apparatus AC.</summary>
    public int WondrousApparatusAC;
    /// <summary>Apparatus max HP.</summary>
    public int WondrousApparatusMaxHP;
    /// <summary>Apparatus current HP.</summary>
    public int WondrousApparatusCurrentHP;
    /// <summary>Apparatus hardness (damage reduction).</summary>
    public int WondrousApparatusHardness;
    /// <summary>Max occupants (2 Medium creatures).</summary>
    public int WondrousApparatusMaxOccupants;
    /// <summary>Air supply remaining in hours (10 max).</summary>
    public float WondrousApparatusAirHours = 10f;
    /// <summary>Current movement speed (0, 30, or 200 ft/round).</summary>
    public int WondrousApparatusCurrentSpeed;
    /// <summary>Current facing angle in degrees (0-359).</summary>
    public int WondrousApparatusFacing;
    /// <summary>States of the 10 levers (true = activated/extended/open).</summary>
    public bool[] WondrousApparatusLevers;
    /// <summary>Pincer attack bonus.</summary>
    public int WondrousApparatusPincerAttack;
    /// <summary>Pincer damage dice (e.g. "2d6").</summary>
    public string WondrousApparatusPincerDamage;

    // ── Phase 13: Legendary Tools ──

    // --- Titan Weapons (Mattock & Maul) ---
    /// <summary>True if this is a Mattock of the Titans.</summary>
    public bool WondrousIsMattockOfTitans;
    /// <summary>True if this is a Maul of the Titans.</summary>
    public bool WondrousIsMaulOfTitans;
    /// <summary>Enhancement bonus for titan weapons.</summary>
    public int WondrousTitanEnhancement;
    /// <summary>Damage dice string (e.g. "4d6").</summary>
    public string WondrousTitanDamageDice;
    /// <summary>Weapon weight in lbs.</summary>
    public int WondrousTitanWeightLbs;
    /// <summary>Weapon size category (Huge).</summary>
    public string WondrousTitanSize;
    /// <summary>Material (Adamantine).</summary>
    public string WondrousTitanMaterial;
    /// <summary>Maximum hardness ignored (adamantine = 20).</summary>
    public int WondrousTitanIgnoreHardness;
    /// <summary>Highest Break DC auto-destroyed by the weapon.</summary>
    public int WondrousTitanAutoBreakDC;
    /// <summary>Sunder bonus (Maul only, +4).</summary>
    public int WondrousTitanSunderBonus;
    /// <summary>True if sunder does not provoke AoO (Maul).</summary>
    public bool WondrousTitanSunderNoAoO;
    /// <summary>Penalty for Medium creature wielding (typically -4).</summary>
    public int WondrousTitanOversizePenalty;

    // --- Lyre of Building ---
    /// <summary>True if this is a Lyre of Building.</summary>
    public bool WondrousIsLyreOfBuilding;
    /// <summary>Uses per week.</summary>
    public int WondrousLyreUsesPerWeek;
    /// <summary>Uses this week.</summary>
    public int WondrousLyreUsesThisWeek;
    /// <summary>Perform DC required.</summary>
    public int WondrousLyrePerformDC;
    /// <summary>Worker-hours per use (800).</summary>
    public int WondrousLyreWorkerHoursPerUse;

    // --- Horn of Valhalla ---
    /// <summary>True if this is a Horn of Valhalla.</summary>
    public bool WondrousIsHornOfValhalla;
    /// <summary>Horn type (Iron, Bronze, etc.).</summary>
    public string WondrousHornType;
    /// <summary>Number of barbarians summoned.</summary>
    public int WondrousHornBarbarianCount;
    /// <summary>Level of summoned barbarians.</summary>
    public int WondrousHornBarbarianLevel;
    /// <summary>Average HP per barbarian.</summary>
    public int WondrousHornBarbarianHP;
    /// <summary>AC of summoned barbarians.</summary>
    public int WondrousHornBarbarianAC;
    /// <summary>Attack bonus of barbarians.</summary>
    public int WondrousHornBarbarianAttack;
    /// <summary>Damage dice of barbarians (e.g. "1d12+3").</summary>
    public string WondrousHornBarbarianDamage;
    /// <summary>Service duration in minutes (60).</summary>
    public float WondrousHornServiceMinutes;
    /// <summary>Uses per week.</summary>
    public int WondrousHornUsesPerWeek;
    /// <summary>Uses this week.</summary>
    public int WondrousHornUsesThisWeek;

    // --- Robe of Stars (enhanced) ---
    /// <summary>True if this is a Robe of Stars.</summary>
    public bool WondrousIsRobeOfStars;
    /// <summary>Luck bonus to all saves.</summary>
    public int WondrousRobeStarsLuckSaveBonus;
    /// <summary>Armor bonus to AC.</summary>
    public int WondrousRobeStarsArmorBonus;
    /// <summary>Fireball stars remaining (max 6).</summary>
    public int WondrousRobeFireballStars;
    /// <summary>Fireball stars max.</summary>
    public int WondrousRobeFireballStarsMax;
    /// <summary>Magic Missile stars remaining (max 20).</summary>
    public int WondrousRobeMagicMissileStars;
    /// <summary>Magic Missile stars max.</summary>
    public int WondrousRobeMagicMissileStarsMax;
    /// <summary>Light stars remaining (max 30).</summary>
    public int WondrousRobeLightStars;
    /// <summary>Light stars max.</summary>
    public int WondrousRobeLightStarsMax;
    /// <summary>Stars regenerated per month (1).</summary>
    public int WondrousRobeStarsRegenPerMonth;
    /// <summary>True if Robe grants Plane Shift to Astral.</summary>
    public bool WondrousRobeGrantsAstralShift;

    /// <summary>True if this ring has any activatable abilities (Sprint 2+).</summary>
    public bool HasActiveRingAbility => IsRing && (
        (RingAbilities != null && RingAbilities.Count > 0) ||
        MaxStoredSpellLevels > 0 ||
        RingId == RingNames.RING_OF_COUNTERSPELLS
    );

    // --- Stackability ---
    /// <summary>Whether this item can stack with identical items in inventory (e.g., scrolls, potions).</summary>
    public bool IsStackable;
    /// <summary>Maximum number of items in a single stack. Default 1 (non-stackable). Scrolls/potions use 20.</summary>
    public int MaxStackSize = 1;
    /// <summary>Current stack count for stackable consumables. 1 = single item. For ammunition, use Quantity instead.</summary>
    public int StackCount = 1;

    // --- Visual ---
    public string IconChar;     // Unicode/emoji character for display (fallback icon)
    public Color IconColor;     // Color tint for the icon

    /// <summary>
    /// Returns a display color based on the item's quality tier:
    ///   Standard = White, Masterwork = Light Blue, Special Material = Purple, Magic = Gold.
    /// </summary>
    public Color GetQualityColor()
    {
        int enhBonus = ResolveEnhancementBonus();
        if (enhBonus > 0)
        {
            // Tiered enchantment colors based on effective bonus (enhancement + ability equivalents)
            int effectiveBonus = GetEffectiveBonusForPricing();
            if (effectiveBonus >= 8)
                return new Color(1f, 0.5f, 0f);    // Orange - Legendary (+8 or higher)
            if (effectiveBonus >= 5)
                return new Color(0.7f, 0.5f, 1f);  // Purple - Epic (+5 to +7)
            if (effectiveBonus >= 3)
                return new Color(0.3f, 0.5f, 1f);  // Blue - Rare (+3 to +4)
            return new Color(0.2f, 0.8f, 0.2f);    // Green - Uncommon (+1 to +2)
        }

        if (Material != null && Material.MaterialType != ItemMaterialType.Standard)
            return new Color(0.7f, 0.5f, 1f); // Purple for special material

        if (IsMasterwork)
            return new Color(0.6f, 0.85f, 1f); // Light blue for masterwork

        // Magic rings always show as magical quality
        if (IsRing && Type == ItemType.Ring)
            return new Color(0.3f, 0.5f, 1f); // Blue - Rare (magic ring)

        // Wondrous items always show as magical quality
        if (IsWondrous && Type == ItemType.Wondrous)
        {
            if (BasePriceGp >= 100000) return new Color(1f, 0.5f, 0f);    // Orange - Legendary
            if (BasePriceGp >= 25000) return new Color(0.7f, 0.5f, 1f);   // Purple - Epic
            if (BasePriceGp >= 4000) return new Color(0.3f, 0.5f, 1f);    // Blue - Rare
            return new Color(0.2f, 0.8f, 0.2f);                           // Green - Uncommon
        }

        return Color.white; // Standard
    }

    /// <summary>Create an empty/null item.</summary>
    public static ItemData Empty => null;


    /// <summary>True if this is an ammunition item with remaining quantity.</summary>
    public bool IsAmmunition => Type == ItemType.Ammunition;

    /// <summary>True if this ammunition stack has at least one round remaining.</summary>
    public bool HasAmmoRemaining => IsAmmunition && Quantity > 0;

    /// <summary>True if this projectile weapon requires ammunition to fire.</summary>
    public bool IsProjectileWeapon => IsWeapon && WeaponCat == WeaponCategory.Ranged && RequiresAmmoType != AmmunitionType.None;

    /// <summary>Consume one round of ammunition. Returns true if successful.</summary>
    public bool ConsumeOneAmmo()
    {
        if (!IsAmmunition || Quantity <= 0)
            return false;
        Quantity--;
        return true;
    }

    /// <summary>True if this item is one of the supported crossbow weapon types.</summary>
    public bool IsCrossbowWeapon => IsWeapon && RequiresReload;

    /// <summary>
    /// Returns true if this weapon has a Rapid Reload feat variant keyed by this weapon type.
    /// </summary>
    public bool IsRapidReloadSupportedCrossbow
    {
        get
        {
            if (!IsCrossbowWeapon) return false;
            string id = (Id ?? string.Empty).ToLowerInvariant();
            return id.Contains(ItemIDs.CROSSBOW_LIGHT)
                || id.Contains(ItemIDs.CROSSBOW_HEAVY)
                || id.Contains("crossbow_hand")
                || id.Contains("crossbow_repeating");
        }
    }

    /// <summary>
    /// Get the feat name that applies Rapid Reload to this crossbow.
    /// Returns empty for non-crossbows or unsupported crossbow variants.
    /// </summary>
    public string GetRapidReloadFeatName()
    {
        if (!IsCrossbowWeapon) return string.Empty;

        string id = (Id ?? string.Empty).ToLowerInvariant();
        if (id.Contains(ItemIDs.CROSSBOW_LIGHT)) return "Rapid Reload (Light Crossbow)";
        if (id.Contains(ItemIDs.CROSSBOW_HEAVY)) return "Rapid Reload (Heavy Crossbow)";
        if (id.Contains("crossbow_hand")) return "Rapid Reload (Hand Crossbow)";
        if (id.Contains("crossbow_repeating")) return "Rapid Reload (Repeating Crossbow)";
        return string.Empty;
    }

    /// <summary>
    /// Get the effective reload action after applying Rapid Reload if the character has it for this weapon.
    /// </summary>
    public ReloadActionType GetEffectiveReloadAction(bool hasRapidReload)
    {
        ReloadActionType action = ReloadAction;
        if (!hasRapidReload) return action;

        if (action == ReloadActionType.FullRound) return ReloadActionType.MoveAction;
        if (action == ReloadActionType.MoveAction) return ReloadActionType.FreeAction;
        return action;
    }
    public bool IsWeapon => Type == ItemType.Weapon;
    public bool IsArmor => Type == ItemType.Armor;
    public bool IsShield => Type == ItemType.Shield;
    public bool IsConsumable => Type == ItemType.Consumable;
    public bool IsRingItem => Type == ItemType.Ring;
    public bool IsWondrousItem => Type == ItemType.Wondrous;

    /// <summary>True if this wondrous item has activatable abilities (command word, use-activated).</summary>
    public bool HasActiveWondrousAbility => IsWondrous && WondrousHasActivation &&
        !string.IsNullOrEmpty(WondrousActivationType) && WondrousActivationType != "passive" && WondrousActivationType != "continuous";

    public bool IsSunderable => IsWeapon || IsArmor || IsShield;

    /// <summary>True if this item has any special abilities (enchantments) applied.</summary>
    public bool IsEnchanted => Enchantment != null && Enchantment.Abilities.Count > 0;

    /// <summary>True if this is a ranged weapon (WeaponCat == Ranged).</summary>
    public bool IsRangedWeapon => IsWeapon && WeaponCat == WeaponCategory.Ranged;

    /// <summary>True if this is a melee weapon (WeaponCat == Melee).</summary>
    public bool IsMeleeWeapon => IsWeapon && WeaponCat == WeaponCategory.Melee;

    /// <summary>
    /// True if this melee weapon can be thrown (either naturally via IsThrown or via Throwing enchantment).
    /// </summary>
    public bool CanBeThrown => IsWeapon && (IsThrown || HasEnchantment(EnchantmentType.Throwing));

    /// <summary>Check if this item has a specific enchantment.</summary>
    public bool HasEnchantment(EnchantmentType type)
    {
        if (Enchantment == null) return false;
        for (int i = 0; i < Enchantment.Abilities.Count; i++)
        {
            if (Enchantment.Abilities[i] == type) return true;
        }
        return false;
    }

    /// <summary>
    /// Get the total effective enhancement bonus for pricing, including special ability bonus equivalents.
    /// E.g., a +1 Flaming weapon has effective bonus of +2 (1 base + 1 flaming) for pricing.
    /// </summary>
    public int GetEffectiveBonusForPricing()
    {
        int baseBonus = Mathf.Max(0, ResolveEnhancementBonus());
        if (Enchantment == null) return baseBonus;
        int abilityBonus = 0;
        for (int i = 0; i < Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(Enchantment.Abilities[i]);
            if (stats != null) abilityBonus += stats.BonusEquivalent;
        }
        return baseBonus + abilityBonus;
    }

    /// <summary>
    /// Get the total flat cost of enchantments that use flat pricing instead of bonus equivalents.
    /// </summary>
    public int GetEnchantmentFlatCostGp()
    {
        if (Enchantment == null) return 0;
        int total = 0;
        for (int i = 0; i < Enchantment.Abilities.Count; i++)
        {
            var stats = EnchantmentProperties.Get(Enchantment.Abilities[i]);
            if (stats != null && stats.BonusEquivalent == 0)
                total += stats.FlatCostGp;
        }
        return total;
    }

    /// <summary>
    /// Build a tooltip section listing all enchantment abilities with descriptions.
    /// Returns empty string if no enchantments are applied.
    /// </summary>
    public string GetEnchantmentTooltipSection()
    {
        if (!IsEnchanted) return "";

        var sb = new System.Text.StringBuilder();
        sb.Append("\n── Enchantments ──");

        for (int i = 0; i < Enchantment.Abilities.Count; i++)
        {
            var ench = EnchantmentProperties.Get(Enchantment.Abilities[i]);
            if (ench == null) continue;

            string bonusLabel = ench.BonusEquivalent > 0
                ? $" [+{ench.BonusEquivalent} equiv]"
                : (ench.FlatCostGp > 0 ? $" [{ench.FlatCostGp:N0} gp]" : "");

            string name = ench.DisplayName;
            if (Enchantment.Abilities[i] == EnchantmentType.Bane && !string.IsNullOrEmpty(Enchantment.BaneCreatureType))
                name = $"Bane ({Enchantment.BaneCreatureType})";

            sb.Append($"\n✧ {name}{bonusLabel}");

            // Short description line
            if (!string.IsNullOrEmpty(ench.Description))
            {
                string desc = ench.Description.Length > 80
                    ? ench.Description.Substring(0, 77) + "..."
                    : ench.Description;
                sb.Append($"\n   {desc}");
            }
        }

        // Total effective bonus and price
        int effectiveBonus = GetEffectiveBonusForPricing();
        int flatCost = GetEnchantmentFlatCostGp();
        sb.Append($"\nTotal Effective Bonus: +{effectiveBonus}");
        if (flatCost > 0)
            sb.Append($" + {flatCost:N0} gp flat");
        sb.Append($"\nEnchanted Value: {EnhancedPriceGp:N0} gp");

        return sb.ToString();
    }

    /// <summary>Enhancement bonus clamped to D&D 3.5e item range (0-5).</summary>
    public int ClampedEnhancementBonus => Mathf.Clamp(ResolveEnhancementBonus(), 0, 5);

    /// <summary>Human-readable item name that includes enhancement prefix when present (for example "+1 Longsword").</summary>
    public string FullNameWithEnhancement => FullDisplayName;

    /// <summary>Price in gp after enhancement formula is applied to BasePriceGp.</summary>
    public int EnhancedPriceGp => GetEnhancedPriceGp(BasePriceGp);

    /// <summary>
    /// Calculate final price from a mundane base price using D&D 3.5e enhancement formulas:
    /// weapon = base + effectiveBonus²×2000, armor/shield = base + effectiveBonus²×1000.
    /// Effective bonus = enhancement bonus + sum of special ability bonus equivalents.
    /// Flat-cost abilities are added separately.
    /// </summary>
    public int GetEnhancedPriceGp(int basePriceGp)
    {
        int clampedBase = Mathf.Max(0, basePriceGp);
        int effectiveBonus = GetEffectiveBonusForPricing();
        if (effectiveBonus <= 0)
            return clampedBase + GetEnchantmentFlatCostGp();

        int multiplier = IsWeapon ? 2000 : (IsArmor || IsShield ? 1000 : 0);
        if (multiplier <= 0)
            return clampedBase;

        return clampedBase + (effectiveBonus * effectiveBonus * multiplier) + GetEnchantmentFlatCostGp();
    }

    /// <summary>
    /// Ensure durability stats are initialized for sunderable items.
    /// Durability persists on the item once initialized.
    /// </summary>
    public void EnsureDurabilityInitialized()
    {
        if (!IsSunderable)
            return;

        if (MaxHitPoints > 0 && Hardness > 0)
        {
            if (CurrentHitPoints <= 0 && !IsDestroyed)
                CurrentHitPoints = MaxHitPoints;
            return;
        }

        int baseHardness = GetBaseHardness();
        int baseHp = GetBaseHitPoints();
        int enhancement = Mathf.Max(0, ResolveEnhancementBonus());

        Hardness = baseHardness + (enhancement * 2);
        MaxHitPoints = baseHp + (enhancement * 10);
        CurrentHitPoints = Mathf.Clamp(CurrentHitPoints <= 0 ? MaxHitPoints : CurrentHitPoints, 0, MaxHitPoints);
        IsBroken = CurrentHitPoints > 0 && CurrentHitPoints <= Mathf.Max(1, MaxHitPoints / 2);
        IsDestroyed = CurrentHitPoints <= 0;
    }

    public int ResolveEnhancementBonus()
    {
        int explicitField = Mathf.Max(EnhancementBonus, enhancementBonus);
        if (explicitField > 0)
            return Mathf.Clamp(explicitField, 0, 5);

        string rawName = Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawName))
            return 0;

        string trimmed = rawName.Trim();

        // Prefix format: "+1 Longsword"
        if (trimmed.StartsWith("+", StringComparison.Ordinal))
        {
            int idx = 1;
            int parsed = 0;
            bool hasDigits = false;
            while (idx < trimmed.Length && char.IsDigit(trimmed[idx]))
            {
                hasDigits = true;
                parsed = (parsed * 10) + (trimmed[idx] - '0');
                idx++;
            }

            if (hasDigits && (idx == trimmed.Length || char.IsWhiteSpace(trimmed[idx])))
                return Mathf.Clamp(parsed, 0, 5);
        }

        // Suffix format: "Longsword +1" (avoid parsing parenthetical names like "Composite Longbow (+1)").
        int lastPlus = trimmed.LastIndexOf('+');
        if (lastPlus > 0 && lastPlus < trimmed.Length - 1)
        {
            bool hasWhitespaceBefore = char.IsWhiteSpace(trimmed[lastPlus - 1]);
            if (hasWhitespaceBefore)
            {
                int idx = lastPlus + 1;
                int parsed = 0;
                bool hasDigits = false;
                while (idx < trimmed.Length && char.IsDigit(trimmed[idx]))
                {
                    hasDigits = true;
                    parsed = (parsed * 10) + (trimmed[idx] - '0');
                    idx++;
                }

                if (hasDigits && idx == trimmed.Length)
                    return Mathf.Clamp(parsed, 0, 5);
            }
        }

        return 0;
    }

    public int GetTotalArmorBonus()
    {
        return Mathf.Max(0, ArmorBonus) + (IsArmor ? ClampedEnhancementBonus : 0);
    }

    public int GetTotalShieldBonus()
    {
        return Mathf.Max(0, ShieldBonus) + (IsShield ? ClampedEnhancementBonus : 0);
    }

    public static string FormatEnhancedName(string originalName, int bonus)
    {
        string cleanName = StripEnhancementNotation(originalName);
        int clamped = Mathf.Clamp(bonus, 0, 5);
        return clamped > 0 ? $"+{clamped} {cleanName}" : cleanName;
    }

    public static string StripEnhancementNotation(string originalName)
    {
        string name = (originalName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        if (name.StartsWith("+", StringComparison.Ordinal))
        {
            int idx = 1;
            while (idx < name.Length && char.IsDigit(name[idx]))
                idx++;

            if (idx > 1 && idx < name.Length && char.IsWhiteSpace(name[idx]))
                return name.Substring(idx).TrimStart();
        }

        int lastPlus = name.LastIndexOf('+');
        if (lastPlus > 0 && lastPlus < name.Length - 1 && char.IsWhiteSpace(name[lastPlus - 1]))
        {
            bool digitsOnlyToEnd = true;
            for (int i = lastPlus + 1; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i]))
                {
                    digitsOnlyToEnd = false;
                    break;
                }
            }

            if (digitsOnlyToEnd)
                return name.Substring(0, lastPlus).TrimEnd();
        }

        return name;
    }

    public int GetHighestWeaponEnhancementBonus()
    {
        int best = Mathf.Max(0, ResolveEnhancementBonus());
        if (ActiveSpellEffects == null)
            return best;

        for (int i = 0; i < ActiveSpellEffects.Count; i++)
        {
            ItemSpellEffect effect = ActiveSpellEffects[i];
            if (effect == null)
                continue;

            int effectBest = Mathf.Max(effect.EnhancementBonusAttack, effect.EnhancementBonusDamage);
            if (effectBest > best)
                best = effectBest;
        }

        return best;
    }

    public bool HasMagicBypassFromActiveEffects()
    {
        if (ActiveSpellEffects == null)
            return false;

        for (int i = 0; i < ActiveSpellEffects.Count; i++)
        {
            ItemSpellEffect effect = ActiveSpellEffects[i];
            if (effect != null && effect.CountsAsMagicForBypass)
                return true;
        }

        return false;
    }

    public bool IsMagicForBypass => CountsAsMagicForBypass || HasMagicBypassFromActiveEffects() || GetHighestWeaponEnhancementBonus() > 0;

    // ── Masterwork & Material Bonus Properties ──

    /// <summary>
    /// D&D 3.5e PHB p.126: Masterwork weapons grant +1 enhancement bonus to attack rolls.
    /// This does NOT stack with magic enhancement bonuses (magic weapons are already masterwork).
    /// Returns +1 only if masterwork AND not already magic-enhanced.
    /// </summary>
    public int MasterworkAttackBonus
    {
        get
        {
            if (!IsMasterwork || (!IsWeapon && !IsAmmunition)) return 0;
            // Magic enhancement bonus already includes masterwork quality
            if (GetEnhancementAttackBonus() > 0) return 0;
            return 1;
        }
    }

    /// <summary>
    /// D&D 3.5e PHB p.284: Alchemical silver weapons take -1 penalty to damage rolls.
    /// </summary>
    public int MaterialDamageModifier => Material != null ? Material.DamageModifier : 0;

    /// <summary>
    /// D&D 3.5e PHB p.126: Masterwork armor/shields reduce armor check penalty by 1.
    /// Returns the ACP reduction (positive value) from masterwork quality.
    /// Does NOT stack with magic enhancement (magic armor is already masterwork).
    /// </summary>
    public int MasterworkACPReduction
    {
        get
        {
            if (!IsMasterwork || (!IsArmor && !IsShield)) return 0;
            if (ResolveEnhancementBonus() > 0) return 0; // Magic armor already masterwork
            return 1;
        }
    }

    /// <summary>
    /// Total armor check penalty reduction from masterwork + material.
    /// Masterwork: -1 ACP. Mithral: additional -3 ACP.
    /// </summary>
    public int TotalACPReduction
    {
        get
        {
            int reduction = MasterworkACPReduction;
            if (Material != null)
                reduction += Material.ArmorCheckPenaltyReduction;
            return reduction;
        }
    }

    /// <summary>
    /// Effective armor check penalty after masterwork and material reductions.
    /// Minimum 0 (ACP cannot become a bonus).
    /// </summary>
    public int EffectiveArmorCheckPenalty => Mathf.Max(0, ArmorCheckPenalty - TotalACPReduction);

    /// <summary>
    /// Effective Max Dex Bonus after material increases (mithral +2).
    /// -1 means no limit.
    /// </summary>
    public int EffectiveMaxDexBonus
    {
        get
        {
            if (MaxDexBonus < 0) return -1; // No limit
            int increase = Material != null ? Material.MaxDexBonusIncrease : 0;
            return MaxDexBonus + increase;
        }
    }

    /// <summary>
    /// Effective arcane spell failure after material reductions (mithral -10%).
    /// Minimum 0%.
    /// </summary>
    public int EffectiveArcaneSpellFailure
    {
        get
        {
            int reduction = Material != null ? Material.ArcaneSpellFailureReduction : 0;
            return Mathf.Max(0, ArcaneSpellFailure - reduction);
        }
    }

    /// <summary>
    /// Effective weight after material multiplier (mithral/darkwood = half).
    /// </summary>
    public float EffectiveWeightLbs
    {
        get
        {
            float mult = Material != null ? Material.WeightMultiplier : 1f;
            return WeightLbs * mult;
        }
    }

    /// <summary>
    /// Full display name including masterwork/material prefix and enhancement.
    /// E.g. "Masterwork Longsword", "Adamantine Full Plate", "+1 Mithral Chain Shirt".
    /// </summary>
    public string FullDisplayName
    {
        get
        {
            string baseName = Name;
            int enhBonus = ResolveEnhancementBonus();

            // Some stored names already bake in a "+N " enhancement prefix
            // (e.g. RegisterEnhancedVariant sets Name = "+1 Banded Mail").
            // Strip it here so we don't render a duplicate "+1 +1 Banded Mail".
            if (enhBonus > 0)
                baseName = StripEnhancementNotation(baseName);

            // Material prefix
            string matPrefix = "";
            if (Material != null && Material.MaterialType != ItemMaterialType.Standard)
                matPrefix = MaterialProperties.GetMaterialPrefix(Material.MaterialType);

            // Enhancement prefix (magic items)
            string enhPrefix = "";
            if (enhBonus > 0)
                enhPrefix = $"+{enhBonus}";

            // Masterwork (only shown if no magic enhancement and no special material name)
            string mwPrefix = "";
            if (IsMasterwork && enhBonus <= 0 && string.IsNullOrEmpty(matPrefix))
                mwPrefix = "Masterwork";

            // Enchantment suffix (special abilities)
            string enchSuffix = "";
            if (IsEnchanted)
            {
                var names = new System.Text.StringBuilder();
                for (int i = 0; i < Enchantment.Abilities.Count; i++)
                {
                    string abilityName = EnchantmentProperties.GetDisplayName(Enchantment.Abilities[i]);

                    // For Bane, append creature type
                    if (Enchantment.Abilities[i] == EnchantmentType.Bane && !string.IsNullOrEmpty(Enchantment.BaneCreatureType))
                        abilityName = $"Bane ({Enchantment.BaneCreatureType})";

                    if (names.Length > 0) names.Append("/");
                    names.Append(abilityName);
                }
                if (names.Length > 0) enchSuffix = names.ToString();
            }

            // Build name: "+1 Flaming Frost Longsword" or "+1 Adamantine Keen Longsword"
            string prefix = "";
            if (!string.IsNullOrEmpty(enhPrefix))
                prefix = enhPrefix;
            if (!string.IsNullOrEmpty(matPrefix))
                prefix = string.IsNullOrEmpty(prefix) ? matPrefix : $"{prefix} {matPrefix}";
            if (!string.IsNullOrEmpty(enchSuffix))
                prefix = string.IsNullOrEmpty(prefix) ? enchSuffix : $"{prefix} {enchSuffix}";
            if (!string.IsNullOrEmpty(mwPrefix) && string.IsNullOrEmpty(prefix))
                prefix = mwPrefix;

            return string.IsNullOrEmpty(prefix) ? baseName : $"{prefix} {baseName}";
        }
    }

    public int GetEnhancementAttackBonus()
    {
        int best = Mathf.Max(0, ResolveEnhancementBonus());
        if (ActiveSpellEffects != null)
        {
            for (int i = 0; i < ActiveSpellEffects.Count; i++)
            {
                ItemSpellEffect effect = ActiveSpellEffects[i];
                if (effect == null)
                    continue;

                if (effect.EnhancementBonusAttack > best)
                    best = effect.EnhancementBonusAttack;
            }
        }

        return best;
    }

    public int GetEnhancementDamageBonus()
    {
        int best = Mathf.Max(0, ResolveEnhancementBonus());
        if (ActiveSpellEffects != null)
        {
            for (int i = 0; i < ActiveSpellEffects.Count; i++)
            {
                ItemSpellEffect effect = ActiveSpellEffects[i];
                if (effect == null)
                    continue;

                if (effect.EnhancementBonusDamage > best)
                    best = effect.EnhancementBonusDamage;
            }
        }

        return best;
    }

    public void AddOrReplaceItemSpellEffect(ItemSpellEffect effect)
    {
        if (effect == null)
            return;

        if (ActiveSpellEffects == null)
            ActiveSpellEffects = new List<ItemSpellEffect>();

        for (int i = ActiveSpellEffects.Count - 1; i >= 0; i--)
        {
            ItemSpellEffect existing = ActiveSpellEffects[i];
            if (existing == null)
            {
                ActiveSpellEffects.RemoveAt(i);
                continue;
            }

            if (!string.IsNullOrEmpty(effect.SpellId)
                && string.Equals(existing.SpellId, effect.SpellId, StringComparison.OrdinalIgnoreCase))
            {
                ActiveSpellEffects.RemoveAt(i);
            }
        }

        ActiveSpellEffects.Add(effect);
    }

    public List<ItemSpellEffect> TickItemSpellEffects()
    {
        var expired = new List<ItemSpellEffect>();
        if (ActiveSpellEffects == null || ActiveSpellEffects.Count == 0)
            return expired;

        for (int i = ActiveSpellEffects.Count - 1; i >= 0; i--)
        {
            ItemSpellEffect effect = ActiveSpellEffects[i];
            if (effect == null)
            {
                ActiveSpellEffects.RemoveAt(i);
                continue;
            }

            if (effect.Tick())
            {
                expired.Add(effect);
                ActiveSpellEffects.RemoveAt(i);
            }
        }

        return expired;
    }

    public int ApplySunderDamage(int incomingDamage, out int effectiveDamage, out int hpBefore, out int hpAfter)
    {
        EnsureDurabilityInitialized();

        hpBefore = CurrentHitPoints;
        effectiveDamage = Mathf.Max(0, incomingDamage - Mathf.Max(0, Hardness));

        if (effectiveDamage > 0)
            CurrentHitPoints = Mathf.Max(0, CurrentHitPoints - effectiveDamage);

        hpAfter = CurrentHitPoints;
        IsDestroyed = CurrentHitPoints <= 0;
        IsBroken = !IsDestroyed && CurrentHitPoints <= Mathf.Max(1, MaxHitPoints / 2);

        return effectiveDamage;
    }

    private int GetBaseHardness()
    {
        if (IsWeapon || IsShield)
            return 10;

        if (IsArmor)
            return ArmorCat == ArmorCategory.Heavy ? 10 : 5;

        return 0;
    }

    private int GetBaseHitPoints()
    {
        if (IsWeapon)
        {
            if (WeaponSize == WeaponSizeCategory.Light || IsLightWeapon)
                return 2;
            if (WeaponSize == WeaponSizeCategory.TwoHanded || IsTwoHanded)
                return 10;
            return 5;
        }

        if (IsShield)
        {
            string id = (Id ?? string.Empty).ToLowerInvariant();
            string n = (Name ?? string.Empty).ToLowerInvariant();

            if (id.Contains(ItemIDs.BUCKLER) || n.Contains(ItemIDs.BUCKLER))
                return 5;
            if (id.Contains("tower") || n.Contains("tower"))
                return 20;
            if (id.Contains("heavy") || n.Contains("heavy") || ShieldBonus >= 2)
                return 10;
            return 5;
        }

        if (IsArmor)
        {
            switch (ArmorCat)
            {
                case ArmorCategory.Light: return 10;
                case ArmorCategory.Medium: return 20;
                case ArmorCategory.Heavy: return 30;
                default: return 10;
            }
        }

        return 0;
    }

    /// <summary>Can this item be equipped in the given slot?</summary>
    public bool CanEquipIn(EquipSlot targetSlot)
    {
        if (Slot == EquipSlot.None) return false;
        if (Slot == targetSlot) return true;

        // Backward-compatible aliasing between legacy Armor and new ArmorRobe slot name.
        if ((Slot == EquipSlot.Armor && targetSlot == EquipSlot.ArmorRobe) ||
            (Slot == EquipSlot.ArmorRobe && targetSlot == EquipSlot.Armor))
            return true;

        // EitherHand items can go in LeftHand or RightHand.
        if (Slot == EquipSlot.EitherHand && (targetSlot == EquipSlot.LeftHand || targetSlot == EquipSlot.RightHand))
            return true;

        // Ring items can support either finger ring slot.
        if (Slot == EquipSlot.EitherRing && (targetSlot == EquipSlot.LeftRing || targetSlot == EquipSlot.RightRing))
            return true;

        // Slotless items can be equipped in the Slotless pseudo-slot.
        if (Slot == EquipSlot.Slotless && targetSlot == EquipSlot.Slotless)
            return true;

        return false;
    }

    /// <summary>Get a human-readable display name for an equipment slot.</summary>
    public static string GetSlotDisplayName(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Head: return "Head";
            case EquipSlot.FaceEyes: return "Face/Eyes";
            case EquipSlot.Neck: return "Throat";
            case EquipSlot.Torso: return "Body";
            case EquipSlot.ArmorRobe: return "Armor/Robe";
            case EquipSlot.Armor: return "Armor/Robe";
            case EquipSlot.Waist: return "Waist";
            case EquipSlot.Back: return "Shoulders";
            case EquipSlot.Wrists: return "Arms/Wrists";
            case EquipSlot.Hands: return "Hands";
            case EquipSlot.LeftRing: return "Left Ring";
            case EquipSlot.RightRing: return "Right Ring";
            case EquipSlot.EitherRing: return "Ring";
            case EquipSlot.Feet: return "Feet";
            case EquipSlot.LeftHand: return "Left Hand";
            case EquipSlot.RightHand: return "Right Hand";
            case EquipSlot.Slotless: return "Slotless";
            default: return slot.ToString();
        }
    }

    /// <summary>
    /// Returns this weapon's damage dice scaled from its designed size to a target wielder size.
    /// Falls back to the item's base dice if no progression entry exists.
    /// </summary>
    public void GetScaledDamageDice(SizeCategory wielderSize, out int damageCount, out int damageDice)
    {
        int baseCount = Mathf.Max(1, DamageCount);
        int baseDice = Mathf.Max(1, DamageDice);

        if (!WeaponDamageScaler.TryScaleDamageDice(baseCount, baseDice, DesignedForSize, wielderSize, out damageCount, out damageDice))
        {
            damageCount = baseCount;
            damageDice = baseDice;
        }
    }

    /// <summary>Get parsed canonical damage types for this weapon.</summary>
    public HashSet<DamageType> GetDamageTypes()
    {
        return DamageTextUtils.ParseDamageTypes(DamageType);
    }

    /// <summary>Check whether this item declares a special property flag (case-insensitive).</summary>
    public bool HasSpecialProperty(string property)
    {
        if (string.IsNullOrWhiteSpace(property) || string.IsNullOrWhiteSpace(SpecialProperties))
            return false;

        return SpecialProperties.IndexOf(property, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Build bypass tags granted by this weapon (material/alignment and physical forms).
    /// Physical damage forms are included so DR/slashing, DR/piercing, etc. can be bypassed.
    /// </summary>
    public DamageBypassTag GetBypassTags()
    {
        DamageBypassTag tags = DamageBypassTag.None;
        var dmgTypes = GetDamageTypes();

        if (dmgTypes.Contains(global::DamageType.Bludgeoning)) tags |= DamageBypassTag.Bludgeoning;
        if (dmgTypes.Contains(global::DamageType.Piercing)) tags |= DamageBypassTag.Piercing;
        if (dmgTypes.Contains(global::DamageType.Slashing)) tags |= DamageBypassTag.Slashing;

        if (IsMagicForBypass) tags |= DamageBypassTag.Magic;
        if (IsSilvered) tags |= DamageBypassTag.Silver;
        if (IsColdIron) tags |= DamageBypassTag.ColdIron;
        if (IsAdamantine) tags |= DamageBypassTag.Adamantine;
        if (IsAlignedGood) tags |= DamageBypassTag.Good;
        if (IsAlignedEvil) tags |= DamageBypassTag.Evil;
        if (IsAlignedLawful) tags |= DamageBypassTag.Lawful;
        if (IsAlignedChaotic) tags |= DamageBypassTag.Chaotic;

        // D&D 3.5e: Special material bypass tags (supplements legacy bool flags)
        if (Material != null)
            tags |= Material.WeaponBypassTags;

        if (WeaponCat == WeaponCategory.Ranged || RangeIncrement > 0)
            tags |= DamageBypassTag.Ranged;

        return tags;
    }
    /// <summary>Get a formatted critical hit range string (e.g., "19-20/×2").</summary>
    public string GetCritRangeString()
    {
        int threatMin = CritThreatMin > 0 ? CritThreatMin : 20;
        int mult = CritMultiplier > 0 ? CritMultiplier : 2;
        string range = threatMin < 20 ? $"{threatMin}-20" : "20";
        return $"{range}/×{mult}";
    }

    /// <summary>Get formatted melee reach description (e.g., "5 ft", "10 ft").</summary>
    public string GetReachDescription()
    {
        int reach = ReachSquares > 0 ? ReachSquares : Mathf.Max(1, AttackRange);
        return $"{reach * 5} ft";
    }

    /// <summary>Get a short stat summary for tooltips.</summary>
    public string GetStatSummary()
    {
        string stats = "";
        if (Type == ItemType.Weapon)
        {
            string dmg = $"{DamageCount}d{DamageDice}";
            if (BonusDamage > 0) dmg += $"+{BonusDamage}";
            stats = $"Damage: {dmg} | Crit: {GetCritRangeString()}";

            int enhancementAttack = GetEnhancementAttackBonus();
            int enhancementDamage = GetEnhancementDamageBonus();
            if (enhancementAttack > 0 || enhancementDamage > 0)
            {
                stats += $"\nEnhancement: +{Mathf.Max(enhancementAttack, enhancementDamage)} Enhancement";
            }

            // Show detailed enchantment info from active spell effects on weapon
            if (ActiveSpellEffects != null)
            {
                foreach (var eff in ActiveSpellEffects)
                {
                    if (eff == null) continue;

                    // Keen Edge: show doubled threat range
                    if (eff.CritThreatRangeModifier != 0)
                    {
                        int baseThreat = CritThreatMin > 0 ? CritThreatMin : 20;
                        int keenThreat = Mathf.Max(2, baseThreat + eff.CritThreatRangeModifier);
                        stats += $"\nKeen Edge: Critical {keenThreat}-20 (Doubled Threat Range) [{eff.GetDurationDisplayString()}]";
                    }

                    // Flame Arrow / bonus damage dice
                    if (!string.IsNullOrEmpty(eff.BonusDamageDice))
                    {
                        string dmgType = string.IsNullOrEmpty(eff.BonusDamageType) ? "" : $" {eff.BonusDamageType}";
                        string chargeInfo = eff.EnchantedAmmoRemaining > 0 ? $" ({eff.EnchantedAmmoRemaining} charges)" : "";
                        stats += $"\n{eff.BonusDamageDice}{dmgType} Damage{chargeInfo} [{eff.GetDurationDisplayString()}]";
                    }
                }
            }
            if (!string.IsNullOrEmpty(DamageType)) stats += $"\nType: {DamageType}";
            if (RangeIncrement > 0)
            {
                int maxIncrements = IsThrown ? 5 : 10;
                int maxRange = RangeIncrement * maxIncrements;
                int incSquares = RangeIncrement / 5;
                int maxSquares = maxRange / 5;

                string weaponType = IsThrown ? "thrown" : "projectile";
                stats += $"\nRange: {RangeIncrement} ft increment ({incSquares} sq), max {maxRange} ft ({maxSquares} sq) [{weaponType}]";
            }
            else if (WeaponCat == WeaponCategory.Melee)
            {
                int minReach = CanAttackAdjacent ? 1 : Mathf.Min(2, Mathf.Max(1, ReachSquares));
                int maxReach = ReachSquares > 0 ? ReachSquares : Mathf.Max(1, AttackRange);
                stats += $"\nReach: {GetReachDescription()} ({minReach}-{maxReach} sq)";
                if (!CanAttackAdjacent)
                    stats += " | Cannot attack adjacent";
            }

            if (RequiresReload)
            {
                string reloadLabel = ReloadAction == ReloadActionType.FullRound ? "Full-round"
                    : ReloadAction == ReloadActionType.MoveAction ? "Move"
                    : ReloadAction == ReloadActionType.FreeAction ? "Free"
                    : "None";
                string loadedLabel = IsLoaded ? "Loaded" : "Unloaded";
                stats += $"\nReload: {reloadLabel} | {loadedLabel}";
            }
            string props = "";
            if (WeaponSize == WeaponSizeCategory.Light) props += "Light, ";
            else if (WeaponSize == WeaponSizeCategory.OneHanded) props += "One-handed, ";
            else if (WeaponSize == WeaponSizeCategory.TwoHanded) props += "Two-handed, ";
            if (IsReachWeapon) props += "Reach, ";
            if (DealsNonlethalDamage) props += "Nonlethal, ";
            if (WhipLikeArmorRestriction) props += "Cannot harm armor/natural armor +1+, ";
            if (IsThrown) props += "Thrown, ";
            if (DmgModType == DamageModifierType.Composite) props += $"Composite (+{CompositeRating} STR), ";
            else if (DmgModType == DamageModifierType.StrengthOneAndHalf) props += "1.5× STR dmg, ";
            else if (DmgModType == DamageModifierType.None && Type == ItemType.Weapon && WeaponCat == WeaponCategory.Ranged) props += "No STR to dmg, ";
            if (NoStrengthToDamage) props += "No STR to dmg, ";
            if (props.Length > 0)
            {
                props = props.TrimEnd(',', ' ');
                stats += $"\n{props}";
            }
            stats += $"\n{Proficiency} {WeaponCat}";

            // Masterwork & material info for weapons
            if (IsMasterwork && MasterworkAttackBonus > 0)
                stats += "\n✦ Masterwork (+1 attack)";
            if (Material != null && Material.MaterialType != ItemMaterialType.Standard)
            {
                string matName = MaterialProperties.GetMaterialPrefix(Material.MaterialType);
                if (Material.DamageModifier != 0)
                    stats += $"\n✦ {matName} ({Material.DamageModifier} damage)";
                if (Material.WeaponBypassTags != DamageBypassTag.None)
                    stats += $"\n✦ Bypasses DR/{matName.ToLowerInvariant()}";
                if (Material.WeightMultiplier < 1f)
                    stats += $"\n✦ Weight: {EffectiveWeightLbs:F1} lbs (half)";
            }
            stats += GetEnchantmentTooltipSection();
        }
        else if (Type == ItemType.Armor)
        {
            ArmorCategory effectiveCat = MaterialProperties.GetEffectiveArmorCategory(this);
            string catLabel = effectiveCat != ArmorCat
                ? $"{ArmorCat} → {effectiveCat}"
                : $"{ArmorCat}";
            stats = $"AC Bonus: +{GetTotalArmorBonus()} ({catLabel})";
            if (ClampedEnhancementBonus > 0)
                stats += $"\nEnhancement: +{ClampedEnhancementBonus} armor";
            int effMaxDex = EffectiveMaxDexBonus;
            int effACP = EffectiveArmorCheckPenalty;
            int effASF = EffectiveArcaneSpellFailure;
            if (effMaxDex >= 0) stats += $"\nMax Dex: +{effMaxDex}";
            if (effACP > 0) stats += $" | Check: -{effACP}";
            if (effASF > 0) stats += $"\nSpell Fail: {effASF}%";
            // Show material/masterwork notes
            if (IsMasterwork && MasterworkACPReduction > 0)
                stats += "\n✦ Masterwork (-1 ACP)";
            if (Material != null && Material.MaterialType != ItemMaterialType.Standard)
            {
                string matName = MaterialProperties.GetMaterialPrefix(Material.MaterialType);
                if (Material.ArmorDRAmount > 0)
                    stats += $"\n✦ {matName}: DR {Material.ArmorDRAmount}/—";
                if (Material.ArmorCategoryShift < 0)
                    stats += $"\n✦ {matName}: Counts as {effectiveCat} for movement/proficiency";
                if (Material.WeightMultiplier < 1f)
                    stats += $"\n✦ Weight: {EffectiveWeightLbs:F1} lbs (half)";
            }
            stats += GetEnchantmentTooltipSection();
        }
        else if (Type == ItemType.Shield)
        {
            stats = $"Shield Bonus: +{GetTotalShieldBonus()}";
            if (ClampedEnhancementBonus > 0)
                stats += $"\nEnhancement: +{ClampedEnhancementBonus} shield";
            int effMaxDex = EffectiveMaxDexBonus;
            int effACP = EffectiveArmorCheckPenalty;
            int effASF = EffectiveArcaneSpellFailure;
            if (effMaxDex >= 0) stats += $"\nMax Dex: +{effMaxDex}";
            if (effACP > 0) stats += $" | Check: -{effACP}";
            if (effASF > 0) stats += $"\nSpell Fail: {effASF}%";
            if (IsMasterwork && MasterworkACPReduction > 0)
                stats += "\n✦ Masterwork (-1 ACP)";
            if (Material != null && Material.MaterialType != ItemMaterialType.Standard)
            {
                string matName = MaterialProperties.GetMaterialPrefix(Material.MaterialType);
                if (Material.WeightMultiplier < 1f)
                    stats += $"\n✦ {matName}: Weight {EffectiveWeightLbs:F1} lbs (half)";
            }

            // D&D 3.5 shield bash profile (when present on this shield definition).
            if (DamageDice > 0 && DamageCount > 0)
            {
                string bashDmg = $"{DamageCount}d{DamageDice}";
                if (BonusDamage > 0) bashDmg += $"+{BonusDamage}";
                string dmgType = string.IsNullOrEmpty(DamageType) ? "bludgeoning" : DamageType;
                string prof = Proficiency == WeaponProficiency.None ? "Martial" : Proficiency.ToString();
                stats += $"\nShield Bash: {bashDmg} {dmgType} ({prof})";
            }
            stats += GetEnchantmentTooltipSection();
        }
        else if (Type == ItemType.Consumable)
        {
            if (ConsumableEffect == ConsumableEffectType.HealHP)
            {
                if (HealDiceCount > 0 && HealDiceSides > 0)
                {
                    string healExpr = $"{HealDiceCount}d{HealDiceSides}";
                    if (HealBonus > 0)
                        healExpr += $"+{HealBonus}";
                    stats = $"Heals: {healExpr} HP";
                }
                else if (HealAmount > 0)
                {
                    stats = $"Heals: {HealAmount} HP";
                }
            }
            else if (ConsumableEffect == ConsumableEffectType.SpellEffect)
            {
                string spellLabel = string.IsNullOrEmpty(ConsumableSpellName) ? "Unknown Spell" : ConsumableSpellName;
                string itemTypeLabel = IsStaff ? "Staff" : IsWand ? "Wand" : IsPotion ? "Potion" : IsScroll ? "Scroll" : "Spell Effect";
                stats = $"{itemTypeLabel}: {spellLabel}";

                if (ConsumableModifier != 0)
                    stats += $"\nModifier: {ConsumableModifier:+#;-#;0}";

                if (ConsumableMinimumCasterLevel > 0)
                    stats += $"\nCaster Level: {ConsumableMinimumCasterLevel}";

                if (IsStaff)
                {
                    string chargeStatus = StaffCharges > 0
                        ? $"{StaffCharges} charges remaining"
                        : "EXPENDED (non-magical)";
                    stats += $"\nCharges: {chargeStatus}";
                    stats += $"\nCaster Level: {StaffCasterLevel} | Spell trigger (class list or UMD DC 20)";
                    if (StaffCharges <= 0)
                        stats += "\n(Cannot be recharged — core DMG 3.5e rules)";
                }
                else if (IsWand)
                {
                    string chargeStatus = CurrentCharges > 0
                        ? $"{CurrentCharges}/{MaxCharges} charges"
                        : "DEPLETED (0 charges)";
                    stats += $"\nCharges: {chargeStatus}";
                    stats += $"\nSpell Level: {WandSpellLevel} | Spell trigger (class list or UMD DC 20)";
                }
                else if (IsPotion)
                    stats += $"\nSpell Level: {PotionSpellLevel} | No class restrictions";
            }
            else if (HealAmount > 0)
            {
                // Backward-compatible fallback for legacy consumables.
                stats = $"Heals: {HealAmount} HP";
            }
        }

        if (Type == ItemType.Ammunition)
        {
            stats = $"Ammunition ({AmmoType})";
            stats += $"\nQuantity: {Quantity}/{MaxQuantity}";

            // Show enchantment info from spell effects
            if (ActiveSpellEffects != null && ActiveSpellEffects.Count > 0)
            {
                foreach (var eff in ActiveSpellEffects)
                {
                    if (eff == null) continue;
                    string label = string.IsNullOrWhiteSpace(eff.SpellName) ? "Enchanted" : eff.SpellName;
                    string details = "";
                    if (!string.IsNullOrEmpty(eff.BonusDamageDice))
                        details += $" +{eff.BonusDamageDice} {eff.BonusDamageType}";
                    if (eff.CritThreatRangeModifier != 0)
                    {
                        int ammoBaseThreat = CritThreatMin > 0 ? CritThreatMin : 20;
                        int keenThreat = Mathf.Max(2, ammoBaseThreat + eff.CritThreatRangeModifier);
                        details += $" Keen (threat {keenThreat}-20)";
                    }
                    if (eff.EnhancementBonusAttack > 0 || eff.EnhancementBonusDamage > 0)
                        details += $" (+{eff.EnhancementBonusAttack} atk/+{eff.EnhancementBonusDamage} dmg)";
                    stats += $"\n{label}{details} ({eff.GetDurationDisplayString()}, {eff.EnchantedAmmoRemaining} enchanted)";
                }
            }
        }

        // --- Rod Tooltip ---
        if (IsRod)
        {
            string catLabel = RodCategory.ToString();
            if (IsLegendary) catLabel = "Legendary";
            stats = $"Rod ({catLabel})";

            if (RodCasterLevel > 0)
                stats += $"\nCaster Level: {RodCasterLevel}th";

            if (RodIsMetamagic)
            {
                string metamagicName = MetamagicData.GetDisplayName(RodMetamagicType);
                string powerLabel = RodPower == RodPowerLevel.Lesser ? "Lesser" :
                                    RodPower == RodPowerLevel.Normal ? "Normal" : "Greater";
                stats += $"\nMetamagic: {metamagicName} (+{RodSlotLevelIncrease} slot, FREE with rod)";
                stats += $"\nPower: {powerLabel} (spells up to level {RodMaxSpellLevel})";
                stats += $"\nUses: {RodUsesToday}/{RodUsesPerDay} per day";
            }

            if (RodCanAbsorbSpells)
            {
                stats += $"\n🛡 Absorbs targeted spells";
                stats += $"\nStored: {RodAbsorbedLevels}/{RodMaxAbsorbedLevels} spell levels";
            }

            if (RodCanCancelMagic)
            {
                if (RodIsExpended)
                    stats += "\n⚠ EXPENDED — nonmagical";
                else
                    stats += "\n⚡ Touch to destroy 1 magic item (single use)";
            }

            if (RodIsFlail)
            {
                stats += $"\n⚔ Weapon: +{RodWeaponEnhancement} {RodWeaponMode} ({RodWeaponDamageDice}+{RodWeaponEnhancement})";
                if (RodFlailDeflectionBonus > 0)
                    stats += $"\nDire Flail mode: +{RodFlailDeflectionBonus} deflection to AC";
            }

            if (RodIsImmovable)
            {
                stats += RodIsActivated
                    ? $"\n🔒 LOCKED — holds {RodHoldWeightLbs:N0} lbs, DC {RodMoveDC} to move"
                    : "\n🔓 Normal (press button to lock)";
            }

            if (RodIsLordlyMight)
            {
                var mode = (LordlyMightWeaponMode)RodLordlyMightMode;
                stats += $"\n⚔ Current Form: {RodWeaponMode}";
                if (!string.IsNullOrEmpty(RodWeaponDamageDice))
                    stats += $" (+{RodWeaponEnhancement}, {RodWeaponDamageDice}+{RodWeaponEnhancement})";
                stats += $"\nFear Cone: {RodFearUsesToday}/{RodFearUsesPerDay}/day (DC {RodFearConeDC}, {RodFearConeRangeFt} ft)";
                stats += "\nModes: Mace, Flaming Sword, Battleaxe, Spear, Longsword, Climbing Pole";
            }

            if (RodCanDetectMetals)
            {
                stats += $"\n🔍 Detect metals/minerals: {RodDetectionRadiusFt} ft (through {RodPenetratesStoneFt} ft stone)";
            }

            if (RodCanDetectEnemies)
            {
                stats += $"\n🔍 Detect enemies: {RodDetectionRadiusFt} ft (through {RodPenetratesStoneFt} ft stone)";
                stats += $"\nUses: {RodUsesToday}/{RodUsesPerDay} per day";
            }

            if (RodIsSplendor)
            {
                stats += $"\n✨ Charisma: +{RodSplendorCharismaBonus} enhancement while held";
                stats += $"\nFeast: {RodSplendorFeastUsesToday}/{RodSplendorFeastUsesPerDay}/day (12 people)";
                stats += $"\nClothes: {RodSplendorClothesThisWeek}/{RodSplendorClothesPerWeek}/week";
                stats += $"\nTent: {RodSplendorTentUsesThisWeek}/{RodSplendorTentUsesPerWeek}/week (100 people)";
            }

            if (RodIsAlertness)
            {
                stats += $"\n+{RodInsightBonusInit} insight to Initiative, +{RodInsightBonusListen} insight to Listen";
                stats += "\nAt will: Light, Detect Evil, Detect Magic, See Invisible";
                stats += $"\nAnimate Objects: {RodAnimateUsesToday}/{RodAnimateUsesPerDay}/day";
                stats += $"\nPrayer: {RodPrayerUsesToday}/{RodPrayerUsesPerDay}/day (30 ft)";
            }

            if (RodIsNegation)
            {
                stats += $"\n✨ Dispel Magic at will (CL {RodDispelCL})";
                stats += $"\nGreater Dispel: {RodGreaterDispelUsesToday}/{RodGreaterDispelUsesPerDay}/day";
            }

            if (RodCanTransformToSnake)
            {
                if (RodIsInSnakeForm)
                {
                    stats += $"\n🐍 SNAKE FORM — HP: {RodSnakeHP}/{RodSnakeMaxHP}, AC: {RodSnakeAC}";
                    stats += $"\nAttack: +{RodSnakeAttackBonus}, Damage: {RodSnakeDamage}";
                    if (RodSnakeHasConstrict)
                        stats += $"\nConstrict: {RodSnakeConstrictDamage}";
                }
                else
                {
                    stats += "\n🐍 Command: Transform to giant constrictor snake";
                }
            }

            if (RodCanCreateDemiplane)
            {
                stats += $"\n🌟 Paradise Demiplane: {RodDemiplaneCapacity} people, {RodDemiplanePersonDays} person-days";
                if (RodDemiplaneHeals) stats += " (full healing)";
                stats += $"\nUses: {RodDemiplaneUsesThisWeek}/{RodDemiplaneUsesPerWeek}/week";
            }

            if (BasePriceGp > 0)
                stats += $"\nValue: {BasePriceGp:N0} gp";
        }

        // --- Wondrous Item Tooltip (skip rods — they have their own tooltip above) ---
        if (Type == ItemType.Wondrous && IsWondrous && !IsRod)
        {
            string slotLabel = IsSlotless ? "Slotless" : GetSlotDisplayName(WondrousRequiredSlot);
            stats = $"Wondrous Item ({slotLabel})";

            if (WondrousAbilityBonus > 0 && !string.IsNullOrEmpty(WondrousAbilityType))
                stats += $"\n+{WondrousAbilityBonus} enhancement bonus to {WondrousAbilityType}";
            if (WondrousACBonus > 0 && !string.IsNullOrEmpty(WondrousACBonusType))
                stats += $"\n+{WondrousACBonus} {WondrousACBonusType} bonus to AC";
            if (WondrousSaveBonus > 0)
            {
                string saveLabel = WondrousSaveType == "all" ? "all saving throws" :
                    WondrousSaveType == "fort" ? "Fortitude saves" :
                    WondrousSaveType == "ref" ? "Reflex saves" :
                    WondrousSaveType == "will" ? "Will saves" : "saves";
                stats += $"\n+{WondrousSaveBonus} resistance bonus to {saveLabel}";
            }
            if (WondrousSkillBonus > 0 && !string.IsNullOrEmpty(WondrousSkillName))
            {
                string bonusType = string.IsNullOrEmpty(WondrousSkillBonusType) ? "competence" : WondrousSkillBonusType;
                stats += $"\n+{WondrousSkillBonus} {bonusType} bonus to {WondrousSkillName}";
            }
            if (WondrousSkillBonus2 > 0 && !string.IsNullOrEmpty(WondrousSkillName2))
                stats += $"\n+{WondrousSkillBonus2} bonus to {WondrousSkillName2}";
            if (WondrousMightyFistsBonus > 0)
                stats += $"\n+{WondrousMightyFistsBonus} enhancement bonus to unarmed/natural attacks";
            if (WondrousSpeedBonus > 0)
                stats += $"\n+{WondrousSpeedBonus} ft enhancement bonus to land speed";
            if (WondrousDisplacementMissChance > 0)
                stats += $"\n{WondrousDisplacementMissChance}% miss chance (displacement)";
            if (WondrousDarkvisionRange > 0)
                stats += $"\nDarkvision {WondrousDarkvisionRange} ft";
            if (WondrousGrantsMovement && !string.IsNullOrEmpty(WondrousMovementMode))
            {
                string moveLabel = WondrousMovementMode.Replace("_", " ");
                if (WondrousMovementMode == "fly" && !string.IsNullOrEmpty(WondrousFlightManeuverability))
                    stats += $"\nGrants fly {WondrousMovementSpeed} ft ({WondrousFlightManeuverability} maneuverability)";
                else if (WondrousMovementSpeed > 0)
                    stats += $"\nGrants {moveLabel} {WondrousMovementSpeed} ft";
                else
                    stats += $"\nGrants {moveLabel}";
                if (WondrousFlightDurationRounds > 0)
                    stats += $" ({WondrousFlightDurationRounds} rounds per use)";
            }
            if (WondrousGrantsHaste)
            {
                int remaining = WondrousHasteMaxRounds - WondrousHasteRoundsUsedToday;
                stats += $"\nHaste effect ({remaining}/{WondrousHasteMaxRounds} rounds/day)";
                if (WondrousHasteCurrentlyActive)
                    stats += " [ACTIVE]";
            }
            if (WondrousGrantsColdEndurance)
                stats += "\nEndure Elements (cold)";
            if (WondrousColdSurvivalBonus > 0)
                stats += $"\n+{WondrousColdSurvivalBonus} Survival in cold environments";
            if (WondrousTeleportWeightLimit > 0)
                stats += $"\nTeleport (up to {WondrousTeleportWeightLimit} lbs)";
            if (WondrousWeightCapacity > 0)
            {
                stats += $"\nCapacity: {WondrousWeightCapacity:0} lbs / {WondrousVolumeCapacity:0} cu ft";
                if (WondrousIsExtradimensional) stats += " (extradimensional)";
                if (WondrousApparentWeight >= 0 && WondrousIsExtradimensional)
                    stats += $"\nApparent weight: {WondrousApparentWeight:0.#} lbs";
                if (WondrousQuickRetrievalEnabled)
                    stats += "\nQuick retrieval (move action)";
            }
            if (WondrousHasActivation && !string.IsNullOrEmpty(WondrousActivationType))
            {
                string actLabel = WondrousActivationType.Replace("_", " ");
                stats += $"\nActivation: {actLabel}";
                if (WondrousUsesPerDay > 0)
                {
                    int remaining = WondrousUsesPerDay - WondrousUsesToday;
                    stats += $" ({remaining}/{WondrousUsesPerDay} uses/day)";
                }
                else if (WondrousUsesPerDay == -1)
                    stats += " (single use)";
            }
            if (WondrousMaxCharges > 0)
                stats += $"\nCharges: {WondrousCurrentCharges}/{WondrousMaxCharges}";
            // Bow attack/damage bonuses (Bracers of Archery)
            if (WondrousBowAttackBonus > 0)
                stats += $"\n+{WondrousBowAttackBonus} competence bonus to bow attack rolls";
            if (WondrousBowDamageBonus > 0)
                stats += $"\n+{WondrousBowDamageBonus} competence bonus to bow damage rolls";
            // Bead tracking (Necklace of Fireballs, Beads of Force)
            if (WondrousBeadDamageDice != null && WondrousBeadDamageDice.Count > 0)
            {
                string beadList = string.Join(", ", WondrousBeadDamageDice.ConvertAll(d => $"{d}d6"));
                stats += $"\nBeads remaining: {WondrousBeadDamageDice.Count} ({beadList} {WondrousBeadDamageType ?? "fire"})";
                if (WondrousBeadRadius > 0)
                    stats += $"\n{WondrousBeadRadius} ft radius, Reflex DC {WondrousBeadSaveDC} for half";
            }
            // Weekly/monthly use tracking
            if (WondrousUsesPerWeek > 0)
            {
                int weekRemaining = WondrousUsesPerWeek - WondrousUsesThisWeek;
                stats += $"\n{weekRemaining}/{WondrousUsesPerWeek} uses/week";
            }
            if (WondrousUsesPerMonth > 0)
            {
                int monthRemaining = WondrousUsesPerMonth - WondrousUsesThisMonth;
                stats += $"\n{monthRemaining}/{WondrousUsesPerMonth} uses/month";
            }
            // Summoning properties
            if (WondrousCanSummon && !string.IsNullOrEmpty(WondrousSummonDescription))
            {
                stats += $"\nSummons: {WondrousSummonDescription}";
                if (WondrousSummonDurationRounds > 0)
                {
                    if (WondrousSummonDurationRounds >= 600)
                        stats += $" ({WondrousSummonDurationRounds / 600} hr)";
                    else if (WondrousSummonDurationRounds >= 10)
                        stats += $" ({WondrousSummonDurationRounds / 10} min)";
                    else
                        stats += $" ({WondrousSummonDurationRounds} rounds)";
                }
                if (WondrousSummonIsMountable) stats += " [mountable]";
            }
            // Entrapment
            if (WondrousCreatesEntrapment)
            {
                stats += $"\nEntrapment: {WondrousEntrapmentSaveType ?? "Reflex"} DC {WondrousEntrapmentSaveDC}";
                if (WondrousEntrapmentBreakDC > 0)
                    stats += $", Str/Escape Artist DC {WondrousEntrapmentBreakDC} to break free";
            }
            // Insight AC bonus (Dusty Rose Prism)
            if (WondrousInsightACBonus > 0)
                stats += $"\n+{WondrousInsightACBonus} insight bonus to AC";
            // Competence save bonus (Pale Green Prism)
            if (WondrousCompetenceSaveBonus > 0)
                stats += $"\n+{WondrousCompetenceSaveBonus} competence bonus to all saves";
            // Resistance save bonus (Robe of Archmagi)
            if (WondrousResistanceSaveBonus > 0)
                stats += $"\n+{WondrousResistanceSaveBonus} resistance bonus to all saves";
            // Caster level bonus (Orange Prism, Robe of Archmagi)
            if (WondrousCasterLevelBonus > 0)
                stats += $"\n+{WondrousCasterLevelBonus} caster level";
            // Regen (Pearly White Spindle)
            if (WondrousRegenPerHour > 0)
                stats += $"\nRegenerates {WondrousRegenPerHour} HP/hour";
            // Feat grants (Dark Blue Rhomboid)
            if (!string.IsNullOrEmpty(WondrousGrantsFeatName))
                stats += $"\nGrants feat: {WondrousGrantsFeatName}";
            // Sustenance (Clear Spindle, Iridescent Spindle)
            if (WondrousSustainsWithoutFood)
                stats += "\nSustains without food or water";
            if (WondrousSustainsWithoutAir)
                stats += "\nSustains without air";
            // Spell storage (Vibrant Purple Prism)
            if (WondrousSpellStorageLevels > 0)
                stats += $"\nStores up to {WondrousSpellStorageLevels} spell levels";
            // Spell absorption (Pale Lavender, Lavender & Green)
            if (WondrousSpellAbsorptionMaxLevel > 0)
            {
                stats += $"\nAbsorbs spells ≤{WondrousSpellAbsorptionMaxLevel}th level";
                if (WondrousSpellAbsorptionMaxCharges > 0)
                    stats += $" ({WondrousSpellAbsorptionCharges}/{WondrousSpellAbsorptionMaxCharges} charges)";
            }
            // Immunities
            if (WondrousGrantsPoisonImmunity)
                stats += "\n🛡 Immune to poison";
            if (WondrousGrantsDiseaseImmunity)
                stats += "\n🛡 Immune to disease";
            if (WondrousGrantsWebImmunity)
                stats += "\n🛡 Immune to webs";
            if (WondrousLuckFortSaveBonus > 0)
                stats += $"\n+{WondrousLuckFortSaveBonus} luck bonus to Fort saves vs poison";
            // Spider Climb (Cloak of Arachnida)
            if (WondrousGrantsSpiderClimb)
                stats += "\nSpider Climb at will";
            // See Invisible / Anti-flanking (Robe of Eyes)
            if (WondrousGrantsSeeInvisible)
                stats += "\nSee Invisible continuously";
            if (WondrousPreventsFlanking)
                stats += "\nCannot be flanked or surprised";
            if (WondrousSearchBonus > 0)
                stats += $"\n+{WondrousSearchBonus} to Search";
            if (WondrousSpotBonus > 0)
                stats += $"\n+{WondrousSpotBonus} to Spot";
            if (WondrousDisguiseBonus > 0)
                stats += $"\n+{WondrousDisguiseBonus} to Disguise";
            // Spell-like abilities (Helm of Telepathy)
            if (WondrousDetectThoughtsDC > 0)
                stats += $"\nDetect Thoughts at will (Will DC {WondrousDetectThoughtsDC})";
            if (WondrousSuggestionDC > 0)
                stats += $"\nSuggestion 1/day (Will DC {WondrousSuggestionDC})";
            // Patches (Robe of Bones, Robe of Useful Items, Robe of Stars)
            if (WondrousPatchesMax > 0)
            {
                stats += $"\nPatches: {WondrousPatchesRemaining}/{WondrousPatchesMax}";
                if (!string.IsNullOrEmpty(WondrousPatchDescription))
                    stats += $" ({WondrousPatchDescription})";
            }
            // Monk bonus (Monk's Belt)
            if (WondrousMonkLevelBonus > 0)
                stats += $"\nMonk abilities function as +{WondrousMonkLevelBonus} levels";
            // Rolled AC (Robe of Stars)
            if (WondrousRolledACBonus > 0)
                stats += $"\n+{WondrousRolledACBonus} armor bonus to AC (rolled)";
            // Underwater (Helm of Underwater Action)
            if (WondrousUnderwaterVisionRange > 0)
                stats += $"\nUnderwater vision {WondrousUnderwaterVisionRange} ft";
            if (WondrousWaterFreedomOfMovement)
                stats += "\nFreedom of movement in water";
            // SR (Robe of Archmagi, Mantle of Spell Resistance)
            if (WondrousGrantsSR > 0)
                stats += $"\nSpell Resistance {WondrousGrantsSR}";
            // Spell-like abilities (Mantle of Faith, etc.)
            if (!string.IsNullOrEmpty(WondrousSpellLikeAbilities))
            {
                string[] spells = WondrousSpellLikeAbilities.Split(',');
                int usesPerDay = WondrousSpellLikeUsesPerDay > 0 ? WondrousSpellLikeUsesPerDay : 1;
                stats += $"\nSpell-like abilities ({usesPerDay}/day each, CL {WondrousSpellLikeCasterLevel}):";
                // Show remaining uses if tracking
                string[] usesToday = !string.IsNullOrEmpty(WondrousSpellLikeUsesToday)
                    ? WondrousSpellLikeUsesToday.Split(',') : null;
                for (int i = 0; i < spells.Length; i++)
                {
                    int remaining = usesPerDay;
                    if (usesToday != null && i < usesToday.Length)
                    {
                        int.TryParse(usesToday[i].Trim(), out int used);
                        remaining = Mathf.Max(0, usesPerDay - used);
                    }
                    stats += $"\n  • {spells[i].Trim()} ({remaining}/{usesPerDay})";
                }
            }
            // Scarab absorption (death/drain/negative energy)
            if (WondrousScarabAbsorbsDeath || WondrousScarabAbsorbsDrain || WondrousScarabAbsorbsNegativeEnergy)
            {
                stats += "\nAbsorbs:";
                if (WondrousScarabAbsorbsDeath) stats += " death effects (1 charge),";
                if (WondrousScarabAbsorbsDrain) stats += " energy drain (2 charges/level),";
                if (WondrousScarabAbsorbsNegativeEnergy) stats += " negative energy (1 charge/die),";
                stats = stats.TrimEnd(',');
                if (WondrousCurrentCharges >= 0 && WondrousMaxCharges > 0)
                    stats += $"\n  Charges: {WondrousCurrentCharges}/{WondrousMaxCharges}";
                if (WondrousCurrentCharges <= 0 && WondrousMaxCharges > 0)
                    stats += "\n  ⚠ Depleted — non-magical";
            }
            // Planar items (Phase 4/5)
            if (WondrousGrantsPlaneShift)
            {
                stats += "\nPlane Shift at will";
                if (WondrousPlaneShiftMaxTravelers > 1)
                    stats += $" (up to {WondrousPlaneShiftMaxTravelers} creatures)";
                if (WondrousPlaneShiftMishapPercent > 0)
                    stats += $"\n  ⚠ {WondrousPlaneShiftMishapPercent}% mishap chance (random plane)";
            }
            if (WondrousCubicGateSides != null && WondrousCubicGateSides.Length == 6)
            {
                stats += "\nCubic Gate (6 sides):";
                for (int i = 0; i < 6; i++)
                {
                    string planeName = PlanarTravelSystem.GetPlaneName((Plane)WondrousCubicGateSides[i]);
                    int usesLeft = WondrousCubicGateMaxUsesPerSide;
                    if (WondrousCubicGateUsesThisWeek != null && i < WondrousCubicGateUsesThisWeek.Length)
                        usesLeft = WondrousCubicGateMaxUsesPerSide - WondrousCubicGateUsesThisWeek[i];
                    stats += $"\n  Side {i + 1}: {planeName} ({usesLeft}/{WondrousCubicGateMaxUsesPerSide}/week)";
                }
            }
            if (WondrousIsWellOfManyWorlds)
            {
                stats += "\nOpens portal to RANDOM plane";
                stats += "\nOpen while held, closes on release";
                stats += "\n⚠ Extradimensional contact → catastrophic!";
                if (WondrousWellIsOpen && WondrousWellCurrentDestination >= 0)
                    stats += $"\n  ★ Currently open: {PlanarTravelSystem.GetPlaneName((Plane)WondrousWellCurrentDestination)}";
            }
            if (WondrousIsCarpetOfFlying)
            {
                stats += $"\nFly speed {WondrousCarpetFlySpeed} ft ({WondrousCarpetManeuverability ?? "average"})";
                stats += $"\nCarries up to {WondrousCarpetCapacityLbs} lbs";
                stats += $"\nSize: {WondrousCarpetSizeFeet}×{WondrousCarpetSizeFeet} ft";
                if (WondrousCarpetIsFlying) stats += "\n  ★ Currently flying";
            }
            // Creature Trapping (Phase 6–8)
            if (WondrousMaxTrappedCreatures > 0)
            {
                int trapped = WondrousTrappedCreatures != null ? WondrousTrappedCreatures.Count : 0;
                stats += $"\n⚔ Creature Trapping ({trapped}/{WondrousMaxTrappedCreatures})";
                if (WondrousTrapSaveDC > 0)
                    stats += $"\n  {WondrousTrapSaveType ?? "Will"} save DC {WondrousTrapSaveDC} to resist";
                if (WondrousTrapRangeFeet > 0)
                    stats += $"\n  Range: {WondrousTrapRangeFeet} ft";
                if (WondrousTrapAnyType)
                    stats += "\n  Can trap ANY creature type";
                else if (!string.IsNullOrEmpty(WondrousTrapAllowedTypes))
                    stats += $"\n  Allowed types: {WondrousTrapAllowedTypes}";
                if (WondrousTrapServiceMinutes > 0)
                    stats += $"\n  Service: {WondrousTrapServiceMinutes:0} minutes when released friendly";
                // List trapped creatures
                if (trapped > 0)
                {
                    stats += "\n  ── Trapped Creatures ──";
                    for (int i = 0; i < WondrousTrappedCreatures.Count; i++)
                    {
                        var tc = WondrousTrappedCreatures[i];
                        stats += $"\n  {i + 1}. {tc.GetSummary()}";
                    }
                }
                else
                {
                    stats += "\n  (empty)";
                }
            }
            if (WondrousControlsEarthElementals)
            {
                stats += "\n⚔ Controls earth elementals";
                if (WondrousTrapControlRangeFeet > 0)
                    stats += $" within {WondrousTrapControlRangeFeet} ft";
                if (WondrousTrapControlSaveDC > 0)
                    stats += $" (Will DC {WondrousTrapControlSaveDC})";
            }
            if (WondrousTrapHasDefaultCreature && !string.IsNullOrEmpty(WondrousTrapDefaultCreatureType))
            {
                string defName = WondrousTrapDefaultCreatureType == "Efreeti" ? "an Efreeti" :
                    WondrousTrapDefaultCreatureType == "ElderEarthElemental" ? "an Elder Earth Elemental" :
                    WondrousTrapDefaultCreatureType;
                stats += $"\nSummons {defName} (1 hour service)";
            }

            // ── Mirror of Opposition ──
            if (WondrousIsMirrorOfOpposition)
            {
                stats += "\n\n🪞 Mirror of Opposition";
                if (WondrousMirrorDuplicateActive)
                    stats += $"\n  ⚠ DUPLICATE ACTIVE (ID: {WondrousMirrorDuplicateID ?? "unknown"})";
                else
                    stats += "\n  Ready — gazing creates evil duplicate";
                stats += "\n  Duplicate: opposite alignment, full HP, exact stats";
                stats += "\n  Delay: 1d4 rounds after viewing";
                stats += "\n  Mirror unusable until duplicate defeated";
            }

            // ── Mirror of Mental Prowess ──
            if (WondrousIsMirrorOfMentalProwess)
            {
                stats += $"\n\n🧠 Mirror of Mental Prowess";
                if (WondrousMirrorMentalBonus > 0)
                    stats += $"\n  +{WondrousMirrorMentalBonus} Int/Wis/Cha (within {WondrousMirrorMentalBonusRange} ft)";
                stats += "\n  Daily Abilities:";
                stats += $"\n    Scrying (Will DC {WondrousMirrorScryingDC}) — {(WondrousMirrorScryingUsesToday > 0 ? "USED" : "available")}";
                stats += $"\n    Detect Thoughts (Will DC {WondrousMirrorDetectThoughtsDC}, {WondrousMirrorTelepathyRange} ft) — {(WondrousMirrorDetectThoughtsUsesToday > 0 ? "USED" : "available")}";
                stats += $"\n    Suggestion (Will DC {WondrousMirrorSuggestionDC}) — {(WondrousMirrorSuggestionUsesToday > 0 ? "USED" : "available")}";
                stats += $"\n    Telepathy ({WondrousMirrorTelepathyRange} ft) — {(WondrousMirrorTelepathyUsesToday > 0 ? "USED" : "available")}";
            }

            // ── Iron Cobra ──
            if (WondrousIsIronCobra)
            {
                stats += "\n\n🐍 Iron Cobra (Tiny Construct)";
                stats += $"\n  HP: {WondrousIronCobraCurrentHP}/{WondrousIronCobraMaxHP}  AC: {WondrousIronCobraAC}";
                stats += $"\n  Attack: +{WondrousIronCobraAttackBonus}, {WondrousIronCobraDamageDice} + poison";
                stats += $"\n  Poison: Fort DC {WondrousIronCobraPoisonDC}, {WondrousIronCobraPoisonDamage} (initial & secondary)";
                stats += $"\n  Fast Healing {WondrousIronCobraFastHealing}";
                stats += $"\n  Guard radius: {WondrousIronCobraGuardRadius} ft";
                stats += $"\n  Status: {(WondrousIronCobraIsActive ? "ACTIVE (patrolling)" : "Inactive")}";
            }

            // ── Stone Horse ──
            if (WondrousIsStoneHorse)
            {
                stats += $"\n\n🐴 Stone Horse ({WondrousStoneHorseType ?? "Unknown"})";
                stats += $"\n  HP: {WondrousStoneHorseCurrentHP}/{WondrousStoneHorseMaxHP}  AC: {WondrousStoneHorseAC}";
                stats += $"\n  Str {WondrousStoneHorseSTR} Dex {WondrousStoneHorseDEX} Con {WondrousStoneHorseCON}";
                if (WondrousStoneHorseSpeed > 0)
                    stats += $"\n  Land speed: {WondrousStoneHorseSpeed} ft";
                if (WondrousStoneHorseFlySpeed > 0)
                    stats += $"\n  Fly speed: {WondrousStoneHorseFlySpeed} ft ({WondrousStoneHorseManeuverability ?? "average"})";
                stats += "\n  Construct: does not eat, sleep, or tire";
                stats += $"\n  Status: {(WondrousStoneHorseIsActive ? "ACTIVE (animate)" : "Stone form (inactive)")}";
            }

            // ── Apparatus of Kwalish ──
            if (WondrousIsApparatusOfKwalish)
            {
                stats += "\n\n🦞 Apparatus of Kwalish (Large Vehicle)";
                stats += $"\n  HP: {WondrousApparatusCurrentHP}/{WondrousApparatusMaxHP}  AC: {WondrousApparatusAC}  Hardness: {WondrousApparatusHardness}";
                stats += $"\n  Occupants: max {WondrousApparatusMaxOccupants} Medium creatures";
                stats += $"\n  Air supply: {WondrousApparatusAirHours:0.#} hours (sealed)";
                stats += $"\n  Speed: {WondrousApparatusCurrentSpeed} ft/round  Facing: {WondrousApparatusFacing}°";
                if (WondrousApparatusLevers != null && WondrousApparatusLevers.Length >= 10)
                {
                    string[] leverNames = {"Fast swim", "Slow swim", "Turn left", "Turn right",
                        "Hatch", "Fwd window", "Side windows", "Legs", "Pincers", "Antenna/light"};
                    stats += "\n  Levers:";
                    for (int lv = 0; lv < 10; lv++)
                        stats += $"\n    {lv + 1}. {leverNames[lv]}: {(WondrousApparatusLevers[lv] ? "ON" : "off")}";
                }
                if (WondrousApparatusPincerAttack > 0)
                    stats += $"\n  Pincer: +{WondrousApparatusPincerAttack}, {WondrousApparatusPincerDamage} damage";
            }

            // ── Titan Weapons ──
            if (WondrousIsMattockOfTitans || WondrousIsMaulOfTitans)
            {
                string name = WondrousIsMattockOfTitans ? "Mattock of the Titans" : "Maul of the Titans";
                stats += $"\n\n⚒ {name} ({WondrousTitanSize} {WondrousTitanMaterial})";
                stats += $"\n  +{WondrousTitanEnhancement} enhancement, {WondrousTitanDamageDice}+{WondrousTitanEnhancement} damage";
                stats += $"\n  Weight: {WondrousTitanWeightLbs} lbs";
                if (WondrousTitanOversizePenalty != 0)
                    stats += $"\n  ⚠ Oversize penalty: {WondrousTitanOversizePenalty} (Medium wielder)";
                if (WondrousTitanIgnoreHardness > 0)
                    stats += $"\n  Ignores hardness up to {WondrousTitanIgnoreHardness} (adamantine)";
                if (WondrousTitanAutoBreakDC > 0)
                    stats += $"\n  Auto-breaks objects DC ≤ {WondrousTitanAutoBreakDC}";
                if (WondrousTitanSunderBonus > 0)
                    stats += $"\n  +{WondrousTitanSunderBonus} bonus on sunder attempts";
                if (WondrousTitanSunderNoAoO)
                    stats += "\n  Sunder does NOT provoke AoO";
            }

            // ── Lyre of Building ──
            if (WondrousIsLyreOfBuilding)
            {
                int remaining = WondrousLyreUsesPerWeek - WondrousLyreUsesThisWeek;
                stats += "\n\n🎵 Lyre of Building";
                stats += $"\n  Uses this week: {WondrousLyreUsesThisWeek}/{WondrousLyreUsesPerWeek} ({remaining} remaining)";
                stats += $"\n  Perform DC {WondrousLyrePerformDC} (string instruments)";
                stats += $"\n  1 hour playing = {WondrousLyreWorkerHoursPerUse} worker-hours";
                stats += "\n  Can construct buildings, fortifications, ships";
            }

            // ── Horn of Valhalla ──
            if (WondrousIsHornOfValhalla)
            {
                int hRemaining = WondrousHornUsesPerWeek - WondrousHornUsesThisWeek;
                stats += $"\n\n📯 Horn of Valhalla ({WondrousHornType})";
                stats += $"\n  Uses this week: {WondrousHornUsesThisWeek}/{WondrousHornUsesPerWeek} ({hRemaining} remaining)";
                stats += $"\n  Summons {WondrousHornBarbarianCount}× Lv{WondrousHornBarbarianLevel} barbarians";
                stats += $"\n  Each: {WondrousHornBarbarianHP} HP, AC {WondrousHornBarbarianAC}, +{WondrousHornBarbarianAttack} atk, {WondrousHornBarbarianDamage}";
                stats += "\n  Feats: Rage, Power Attack, Cleave";
                stats += $"\n  Serve for {WondrousHornServiceMinutes:0} minutes, fight to the death";
            }

            // ── Robe of Stars (enhanced) ──
            if (WondrousIsRobeOfStars)
            {
                stats += "\n\n⭐ Robe of Stars";
                if (WondrousRobeStarsLuckSaveBonus > 0)
                    stats += $"\n  +{WondrousRobeStarsLuckSaveBonus} luck bonus to ALL saves";
                if (WondrousRobeStarsArmorBonus > 0)
                    stats += $"\n  +{WondrousRobeStarsArmorBonus} armor bonus to AC";
                stats += $"\n  Fireball stars: {WondrousRobeFireballStars}/{WondrousRobeFireballStarsMax} (5d6 fire, DC 15)";
                stats += $"\n  Magic Missile stars: {WondrousRobeMagicMissileStars}/{WondrousRobeMagicMissileStarsMax} (1d4+1 force)";
                stats += $"\n  Light stars: {WondrousRobeLightStars}/{WondrousRobeLightStarsMax} (1 hr, 20 ft radius)";
                stats += $"\n  Stars regenerate: {WondrousRobeStarsRegenPerMonth}/month";
                if (WondrousRobeGrantsAstralShift)
                    stats += "\n  Command word: Plane Shift to Astral Plane";
            }

            // Alignment restriction
            if (!string.IsNullOrEmpty(WondrousRequiredAlignment))
                stats += $"\n⚠ Alignment: {WondrousRequiredAlignment}";
            if (IsIounStone)
                stats += "\n✦ Ioun Stone (orbits head, AC 24, HP 10)";
            if (WondrousCasterLevel > 0)
                stats += $"\nCaster Level: {WondrousCasterLevel}";
        }

        // --- Ring Tooltip ---
        if (Type == ItemType.Ring && IsRing)
        {
            stats = "Ring (magic)";
            if (RingDeflectionBonus > 0) stats += $"\n+{RingDeflectionBonus} deflection bonus to AC";
            if (RingResistanceSaveBonus > 0) stats += $"\n+{RingResistanceSaveBonus} resistance bonus to all saves";
            if (RingShieldBonus > 0) stats += $"\n+{RingShieldBonus} shield bonus to AC (force)";
            if (RingEnergyResistanceAmount > 0 && !string.IsNullOrEmpty(RingEnergyType))
                stats += $"\nEnergy Resistance {RingEnergyResistanceAmount} ({RingEnergyType})";
            if (RingGrantsEvasion) stats += "\nGrants Evasion";
            if (RingGrantsFreedomOfMovement) stats += "\nContinuous Freedom of Movement";
            if (RingGrantsFeatherFall) stats += "\nContinuous Feather Fall";
            if (RingSkillBonus > 0 && !string.IsNullOrEmpty(RingSkillName))
                stats += $"\n+{RingSkillBonus} competence bonus to {RingSkillName}";
            if (RingGrantsWaterWalking) stats += "\nWalk on water";
            if (RingGrantsSustenance) stats += "\nNo need for food, water, or sleep";
            if (RingGrantsMindShielding) stats += "\nImmune to detect thoughts, discern lies, alignment detection";
            if (RingGrantsColdEndurance) stats += "\nResist cold environments (endure elements vs cold)";
            if (RingCasterLevel > 0) stats += $"\nCaster Level: {RingCasterLevel}";

            // Sprint 2: Active ability info
            if (RingAbilities != null && RingAbilities.Count > 0)
            {
                stats += "\n── Active Abilities ──";
                string ringInstId = RingActivationManager.GetRingInstanceId(this);
                var tracker = RingUseTracker.Instance;
                foreach (var ability in RingAbilities)
                {
                    string usesStr = ability.GetUsesDisplayString(tracker, ringInstId);
                    stats += $"\n  {ability.DisplayName} ({usesStr})";
                    if (!string.IsNullOrEmpty(ability.Description))
                        stats += $"\n    {ability.Description}";
                }
            }
            if (RingMaxCharges > 0)
                stats += $"\nCharges: {RingCurrentCharges}/{RingMaxCharges}";
            if (RingSpellTurningPool > 0)
                stats += $"\nSpell Turning Pool: {RingSpellTurningPool} levels";
            if (RingDjinniSlain)
                stats += "\n⚠ INERT — Bound Djinni was slain";

            // Sprint 3: Complex ring info
            if (RingId == RingNames.RING_OF_COUNTERSPELLS)
            {
                stats += "\n── Counterspell ──";
                stats += $"\n  {CounterspellManager.GetStoredCounterspellDisplay(this)}";
            }
            if (MaxStoredSpellLevels > 0)
            {
                stats += $"\n── Spell Storage ──";
                stats += $"\n  {SpellStorageManager.GetStorageDisplayString(this)}";
            }
            if (RingWizardryLevel > 0)
            {
                string ordinal = RingWizardryLevel == 1 ? "1st" : RingWizardryLevel == 2 ? "2nd" : RingWizardryLevel == 3 ? "3rd" : "4th";
                stats += $"\n  Doubles {ordinal}-level arcane spell slots";
            }
            if (RingHasRegeneration)
            {
                stats += "\n  ♻ Regeneration: heal 1 HP/level per hour, prevents HP death";
            }
        }

        if (IsSunderable)
        {
            EnsureDurabilityInitialized();
            string durabilityLine = $"Hardness: {Hardness} | HP: {CurrentHitPoints}/{MaxHitPoints}";
            if (IsDestroyed)
                durabilityLine += " (Destroyed)";
            else if (IsBroken)
                durabilityLine += " (Broken)";

            stats = string.IsNullOrEmpty(stats) ? durabilityLine : $"{stats}\n{durabilityLine}";
        }

        if (ActiveSpellEffects != null && ActiveSpellEffects.Count > 0)
        {
            var lines = new List<string>();
            for (int i = 0; i < ActiveSpellEffects.Count; i++)
            {
                ItemSpellEffect effect = ActiveSpellEffects[i];
                if (effect == null)
                    continue;

                string spellLabel = string.IsNullOrWhiteSpace(effect.SpellName) ? "Spell Effect" : effect.SpellName;
                lines.Add($"{spellLabel}: {effect.GetDurationDisplayString()}");
            }

            if (lines.Count > 0)
            {
                string effectSummary = "Item Effects: " + string.Join(", ", lines);
                stats = string.IsNullOrEmpty(stats) ? effectSummary : $"{stats}\n{effectSummary}";
            }
        }

        if (WeightLbs > 0f)
        {
            string weightLabel = WeightLbs == Mathf.Floor(WeightLbs)
                ? $"{WeightLbs:0} lbs"
                : $"{WeightLbs:0.##} lbs";
            stats = string.IsNullOrEmpty(stats) ? $"Weight: {weightLabel}" : $"{stats}\nWeight: {weightLabel}";
        }

        // Stack info for stackable consumables
        if (IsStackable && MaxStackSize > 1)
        {
            string stackLabel = $"Stack: {StackCount}/{MaxStackSize}";
            stats = string.IsNullOrEmpty(stats) ? stackLabel : $"{stats}\n{stackLabel}";
        }

        return stats;
    }
}