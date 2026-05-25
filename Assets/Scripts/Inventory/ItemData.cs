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
    Ring
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
    Feet = 17
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

        return false;
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