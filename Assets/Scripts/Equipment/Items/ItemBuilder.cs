using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// ItemBuilder — Unified fluent API for creating D&D 3.5e items.
//
// Replaces verbose 15-25 line ItemData object initializer blocks with concise,
// validated, chainable method calls.
//
// === WEAPONS ===
//   var longsword = ItemBuilder.Weapon("longsword")
//       .Named("Longsword")
//       .Simple().Melee().OneHanded()
//       .Damage(1, 8, "slashing")
//       .Crit(19, 2)
//       .Weight(4f)
//       .Price(15)
//       .Icon("\u2694", new Color(0.7f, 0.7f, 0.8f))
//       .Build();
//
//   var flaming = ItemBuilder.Weapon("longsword_flaming_1")
//       .FromBase("longsword")              // Clone base stats from ItemDatabase
//       .Named("+1 Flaming Longsword")
//       .Enhancement(1)
//       .Masterwork()
//       .Magic()
//       .Build();
//
// === ARMOR ===
//   var chainmail = ItemBuilder.Armor("chainmail")
//       .Named("Chainmail")
//       .AC(5).MaxDex(2).CheckPenalty(5).SpellFailure(30)
//       .Heavy().Metal()
//       .Weight(40f)
//       .Price(150)
//       .Build();
//
// === SHIELDS ===
//   var heavyShield = ItemBuilder.Shield("heavy_wooden_shield")
//       .Named("Heavy Wooden Shield")
//       .ShieldAC(2).CheckPenalty(2).SpellFailure(15)
//       .Weight(10f)
//       .Build();
//
// === SCROLLS ===
//   var scroll = ItemBuilder.Scroll("scroll_fireball")
//       .ForSpell("Fireball", 3)
//       .Arcane()
//       .CasterLevel(5)
//       .Price(375)
//       .Build();
//
// === WANDS ===
//   var wand = ItemBuilder.Wand("wand_magic_missile")
//       .ForSpell("Magic Missile", "magic_missile")
//       .CasterLevel(1)
//       .Charges(50)
//       .Price(750)
//       .Build();
//
// === POTIONS ===
//   var potion = ItemBuilder.Potion("potion_cure_light")
//       .ForSpell("Cure Light Wounds")
//       .CasterLevel(1)
//       .Healing(1, 8, 1)
//       .Price(50)
//       .Build();
//
// === RINGS ===
//   var ring = ItemBuilder.Ring("ring_of_protection_1")
//       .Named("Ring of Protection +1")
//       .DeflectionBonus(1)
//       .CasterLevel(3)
//       .Price(2000)
//       .Build();
//
// === WONDROUS ITEMS ===
//   var cloak = ItemBuilder.Wondrous("cloak_of_resistance_1")
//       .Named("Cloak of Resistance +1")
//       .Slot(EquipSlot.Back)
//       .CasterLevel(3)
//       .Price(1000)
//       .Build();
//
// === AMMUNITION ===
//   var arrows = ItemBuilder.Ammo("arrows_20")
//       .Named("Arrows (20)")
//       .AmmoType(AmmunitionType.Arrow)
//       .Quantity(20, 20)
//       .Price(1)
//       .Build();
//
// All Build() calls validate required fields and throw ArgumentException
// with clear messages if something is missing or invalid.
// ============================================================================

/// <summary>
/// Fluent builder for creating ItemData instances with validation.
/// Entry points: Weapon(), Armor(), Shield(), Scroll(), Wand(), Potion(),
/// Ring(), Wondrous(), Ammo(), Misc().
/// </summary>
public class ItemBuilder
{
    private readonly ItemData _item;
    private bool _built;
    private string _baseItemId; // For cloning from an existing base

    // ════════════════════════════════════════════════════════════
    //  Private Constructor — use static factory methods
    // ════════════════════════════════════════════════════════════

    private ItemBuilder(string id, ItemType type)
    {
        _item = new ItemData
        {
            Id = id,
            Type = type,
            DamageCount = 1,          // Sensible default
            CritThreatMin = 20,       // Default: crit on 20 only
            CritMultiplier = 2,       // Default: ×2
            MaxDexBonus = -1,         // -1 = no limit (default for non-armor)
            StackCount = 1,
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Static Factory Methods — Entry Points
    // ════════════════════════════════════════════════════════════

    /// <summary>Start building a weapon.</summary>
    public static ItemBuilder Weapon(string id)
    {
        var b = new ItemBuilder(id, ItemType.Weapon);
        b._item.Slot = EquipSlot.EitherHand;
        b._item.DmgModType = DamageModifierType.Strength;
        return b;
    }

    /// <summary>Start building body armor.</summary>
    public static ItemBuilder Armor(string id)
    {
        var b = new ItemBuilder(id, ItemType.Armor);
        b._item.Slot = EquipSlot.Armor;
        return b;
    }

    /// <summary>Start building a shield.</summary>
    public static ItemBuilder Shield(string id)
    {
        var b = new ItemBuilder(id, ItemType.Shield);
        b._item.Slot = EquipSlot.LeftHand;
        return b;
    }

    /// <summary>Start building a scroll.</summary>
    public static ItemBuilder Scroll(string id)
    {
        var b = new ItemBuilder(id, ItemType.Consumable);
        b._item.Slot = EquipSlot.None;
        b._item.IsScroll = true;
        b._item.ConsumableEffect = ConsumableEffectType.SpellEffect;
        b._item.WeightLbs = 0.1f;
        b._item.IsStackable = true;
        b._item.MaxStackSize = 20;
        b._item.IconChar = "\u2709";
        b._item.IconColor = new Color(0.4f, 0.7f, 1f);
        return b;
    }

    /// <summary>Start building a wand.</summary>
    public static ItemBuilder Wand(string id)
    {
        var b = new ItemBuilder(id, ItemType.Consumable);
        b._item.Slot = EquipSlot.None;
        b._item.IsWand = true;
        b._item.ConsumableEffect = ConsumableEffectType.SpellEffect;
        b._item.WeightLbs = 0.25f;
        b._item.CurrentCharges = 50;
        b._item.MaxCharges = 50;
        b._item.IconChar = "\u2742";
        b._item.IconColor = new Color(0.6f, 0.4f, 0.9f);
        return b;
    }

    /// <summary>Start building a potion.</summary>
    public static ItemBuilder Potion(string id)
    {
        var b = new ItemBuilder(id, ItemType.Consumable);
        b._item.Slot = EquipSlot.None;
        b._item.IsPotion = true;
        b._item.ConsumableEffect = ConsumableEffectType.SpellEffect;
        b._item.WeightLbs = 0.1f;
        b._item.IsStackable = true;
        b._item.MaxStackSize = 20;
        b._item.IconChar = "\u2697";
        b._item.IconColor = new Color(0.4f, 0.9f, 0.5f);
        return b;
    }

    /// <summary>Start building a ring.</summary>
    public static ItemBuilder Ring(string id)
    {
        var b = new ItemBuilder(id, ItemType.Ring);
        b._item.Slot = EquipSlot.EitherRing;
        b._item.IsRing = true;
        b._item.RingId = id;
        b._item.CountsAsMagicForBypass = true;
        b._item.IconChar = "\uD83D\uDC8D"; // 💍
        b._item.IconColor = new Color(0.7f, 0.85f, 1.0f);
        return b;
    }

    /// <summary>Start building a wondrous item.</summary>
    public static ItemBuilder Wondrous(string id)
    {
        var b = new ItemBuilder(id, ItemType.Wondrous);
        b._item.IsWondrous = true;
        b._item.WondrousId = id;
        b._item.CountsAsMagicForBypass = true;
        b._item.IconChar = "\u2728"; // ✨
        b._item.IconColor = new Color(0.9f, 0.9f, 1f);
        return b;
    }

    /// <summary>Start building ammunition.</summary>
    public static ItemBuilder Ammo(string id)
    {
        var b = new ItemBuilder(id, ItemType.Ammunition);
        b._item.Slot = EquipSlot.None;
        b._item.IsStackable = true;
        return b;
    }

    /// <summary>Start building a miscellaneous/gear item.</summary>
    public static ItemBuilder Misc(string id)
    {
        return new ItemBuilder(id, ItemType.Misc);
    }

    // ════════════════════════════════════════════════════════════
    //  Core Identity
    // ════════════════════════════════════════════════════════════

    /// <summary>Set the item's display name.</summary>
    public ItemBuilder Named(string name)
    {
        _item.Name = name;
        return this;
    }

    /// <summary>Set the item's description/tooltip text.</summary>
    public ItemBuilder Desc(string description)
    {
        _item.Description = description;
        return this;
    }

    /// <summary>Clone base stats from an existing item in ItemDatabase, then allow overrides.</summary>
    public ItemBuilder FromBase(string baseItemId)
    {
        _baseItemId = baseItemId;
        return this;
    }

    // ════════════════════════════════════════════════════════════
    //  Weapon Properties
    // ════════════════════════════════════════════════════════════

    /// <summary>Set proficiency to Simple.</summary>
    public ItemBuilder Simple() { _item.Proficiency = WeaponProficiency.Simple; return this; }

    /// <summary>Set proficiency to Martial.</summary>
    public ItemBuilder Martial() { _item.Proficiency = WeaponProficiency.Martial; return this; }

    /// <summary>Set proficiency to Exotic.</summary>
    public ItemBuilder Exotic() { _item.Proficiency = WeaponProficiency.Exotic; return this; }

    /// <summary>Set weapon category to Melee.</summary>
    public ItemBuilder Melee() { _item.WeaponCat = WeaponCategory.Melee; return this; }

    /// <summary>Set weapon category to Ranged.</summary>
    public ItemBuilder Ranged() { _item.WeaponCat = WeaponCategory.Ranged; return this; }

    /// <summary>Mark as Light weapon (reduces TWF penalties).</summary>
    public ItemBuilder Light()
    {
        _item.WeaponSize = WeaponSizeCategory.Light;
        _item.IsLightWeapon = true;
        return this;
    }

    /// <summary>Mark as One-Handed weapon.</summary>
    public ItemBuilder OneHanded()
    {
        _item.WeaponSize = WeaponSizeCategory.OneHanded;
        return this;
    }

    /// <summary>Mark as Two-Handed weapon (1.5× STR to damage).</summary>
    public ItemBuilder TwoHanded()
    {
        _item.WeaponSize = WeaponSizeCategory.TwoHanded;
        _item.IsTwoHanded = true;
        return this;
    }

    /// <summary>Set damage dice: count d sides (e.g., 2d6 = Damage(2, 6)).</summary>
    public ItemBuilder Damage(int count, int sides, string damageType = null)
    {
        _item.DamageCount = count;
        _item.DamageDice = sides;
        if (damageType != null) _item.DamageType = damageType;
        return this;
    }

    /// <summary>Set critical threat range and multiplier (e.g., Crit(19, 2) = 19-20/×2).</summary>
    public ItemBuilder Crit(int threatMin, int multiplier)
    {
        _item.CritThreatMin = threatMin;
        _item.CritMultiplier = multiplier;
        return this;
    }

    /// <summary>Set melee attack range in squares (default 1 = adjacent).</summary>
    public ItemBuilder Range(int squares)
    {
        _item.AttackRange = squares;
        return this;
    }

    /// <summary>Mark as a reach weapon with specified reach in squares.</summary>
    public ItemBuilder Reach(int squares, bool canAttackAdjacent = false)
    {
        _item.HasReach = true;
        _item.IsReachWeapon = true;
        _item.ReachSquares = squares;
        _item.CanAttackAdjacent = canAttackAdjacent;
        return this;
    }

    /// <summary>Mark as throwable with range increment in feet.</summary>
    public ItemBuilder Thrown(int rangeIncrementFeet)
    {
        _item.IsThrown = true;
        _item.RangeIncrement = rangeIncrementFeet;
        return this;
    }

    /// <summary>Set range increment for ranged weapons in feet.</summary>
    public ItemBuilder RangeIncrement(int feet)
    {
        _item.RangeIncrement = feet;
        return this;
    }

    /// <summary>Set damage modifier type (Strength, StrengthAndAHalf, None).</summary>
    public ItemBuilder DamageModifier(DamageModifierType type)
    {
        _item.DmgModType = type;
        return this;
    }

    /// <summary>Set composite bow strength rating.</summary>
    public ItemBuilder Composite(int maxStrBonus)
    {
        _item.CompositeRating = maxStrBonus;
        return this;
    }

    /// <summary>Mark weapon as requiring reload (crossbows). Sets IsLoaded = true by default.</summary>
    public ItemBuilder RequiresReload(ReloadActionType action = ReloadActionType.MoveAction)
    {
        _item.RequiresReload = true;
        _item.ReloadAction = action;
        _item.IsLoaded = true;
        return this;
    }

    /// <summary>Set required ammo type for ranged weapons.</summary>
    public ItemBuilder RequiresAmmo(AmmunitionType type)
    {
        _item.RequiresAmmoType = type;
        return this;
    }

    /// <summary>Mark as dealing nonlethal damage (whip, sap).</summary>
    public ItemBuilder Nonlethal()
    {
        _item.DealsNonlethalDamage = true;
        return this;
    }

    /// <summary>Set bonus flat damage.</summary>
    public ItemBuilder BonusDamage(int bonus)
    {
        _item.BonusDamage = bonus;
        return this;
    }

    // ════════════════════════════════════════════════════════════
    //  Armor / Shield Properties
    // ════════════════════════════════════════════════════════════

    /// <summary>Set armor AC bonus.</summary>
    public ItemBuilder AC(int bonus)
    {
        _item.ArmorBonus = bonus;
        return this;
    }

    /// <summary>Set shield AC bonus.</summary>
    public ItemBuilder ShieldAC(int bonus)
    {
        _item.ShieldBonus = bonus;
        return this;
    }

    /// <summary>Set maximum Dexterity bonus to AC while wearing.</summary>
    public ItemBuilder MaxDex(int maxDex)
    {
        _item.MaxDexBonus = maxDex;
        return this;
    }

    /// <summary>Set armor check penalty (stored as positive value).</summary>
    public ItemBuilder CheckPenalty(int penalty)
    {
        _item.ArmorCheckPenalty = penalty;
        return this;
    }

    /// <summary>Set arcane spell failure percentage (0-100).</summary>
    public ItemBuilder SpellFailure(int percent)
    {
        _item.ArcaneSpellFailure = percent;
        return this;
    }

    /// <summary>Set armor category to Light.</summary>
    public ItemBuilder LightArmor() { _item.ArmorCat = ArmorCategory.Light; return this; }

    /// <summary>Set armor category to Medium.</summary>
    public ItemBuilder MediumArmor() { _item.ArmorCat = ArmorCategory.Medium; return this; }

    /// <summary>Set armor category to Heavy.</summary>
    public ItemBuilder Heavy() { _item.ArmorCat = ArmorCategory.Heavy; return this; }

    /// <summary>Set armor material to Metal.</summary>
    public ItemBuilder Metal() { _item.ArmorMaterial = ArmorMaterialType.Metal; return this; }

    /// <summary>Set armor material to Non-Metal.</summary>
    public ItemBuilder NonMetal() { _item.ArmorMaterial = ArmorMaterialType.NonMetal; return this; }

    /// <summary>Set armor material to Mixed.</summary>
    public ItemBuilder MixedMaterial() { _item.ArmorMaterial = ArmorMaterialType.Mixed; return this; }

    // ════════════════════════════════════════════════════════════
    //  Enhancement / Material Properties (Weapons + Armor)
    // ════════════════════════════════════════════════════════════

    /// <summary>Set magic enhancement bonus (+1 to +5).</summary>
    public ItemBuilder Enhancement(int bonus)
    {
        _item.EnhancementBonus = bonus;
        _item.enhancementBonus = bonus;
        return this;
    }

    /// <summary>Mark item as masterwork quality.</summary>
    public ItemBuilder Masterwork()
    {
        _item.IsMasterwork = true;
        return this;
    }

    /// <summary>Mark item as counting as magical for DR bypass purposes.</summary>
    public ItemBuilder Magic()
    {
        _item.CountsAsMagicForBypass = true;
        return this;
    }

    /// <summary>Mark item as silvered (bypasses DR/silver).</summary>
    public ItemBuilder Silvered()
    {
        _item.IsSilvered = true;
        return this;
    }

    /// <summary>Mark item as cold iron (bypasses DR/cold iron).</summary>
    public ItemBuilder ColdIron()
    {
        _item.IsColdIron = true;
        return this;
    }

    /// <summary>Mark item as adamantine (bypasses DR/adamantine).</summary>
    public ItemBuilder Adamantine()
    {
        _item.IsAdamantine = true;
        return this;
    }

    /// <summary>Set alignment for DR bypass (good, evil, lawful, chaotic).</summary>
    public ItemBuilder Aligned(bool good = false, bool evil = false, bool lawful = false, bool chaotic = false)
    {
        if (good) _item.IsAlignedGood = true;
        if (evil) _item.IsAlignedEvil = true;
        if (lawful) _item.IsAlignedLawful = true;
        if (chaotic) _item.IsAlignedChaotic = true;
        return this;
    }

    // ════════════════════════════════════════════════════════════
    //  Scroll / Wand / Potion — Consumable Properties
    // ════════════════════════════════════════════════════════════

    /// <summary>Set the spell for scrolls (spell name/ID + level).</summary>
    public ItemBuilder ForSpell(string spellName, int spellLevel)
    {
        _item.ConsumableSpellName = spellName;
        if (_item.IsScroll)
        {
            _item.ScrollSpellLevel = spellLevel;
            _item.ScrollEffectiveSpellLevel = spellLevel;
        }
        if (_item.IsPotion) _item.PotionSpellLevel = spellLevel;
        return this;
    }

    /// <summary>Set the spell for wands (spell name + spell ID).</summary>
    public ItemBuilder ForSpell(string spellName, string spellId)
    {
        _item.ConsumableSpellName = spellName;
        if (_item.IsWand) _item.WandSpellId = spellId;
        return this;
    }

    /// <summary>Set caster level for consumables and magic items.</summary>
    public ItemBuilder CasterLevel(int cl)
    {
        _item.ConsumableMinimumCasterLevel = cl;
        if (_item.IsWand) _item.WandCasterLevel = cl;
        if (_item.IsRing) _item.RingCasterLevel = cl;
        if (_item.IsWondrous) _item.WondrousCasterLevel = cl;
        return this;
    }

    /// <summary>Mark scroll as Arcane.</summary>
    public ItemBuilder Arcane()
    {
        _item.ScrollType = "Arcane";
        _item.IconColor = new Color(0.4f, 0.7f, 1f);
        return this;
    }

    /// <summary>Mark scroll as Divine.</summary>
    public ItemBuilder Divine()
    {
        _item.ScrollType = "Divine";
        _item.IconColor = new Color(1f, 0.85f, 0.4f);
        return this;
    }

    /// <summary>Set wand charges.</summary>
    public ItemBuilder Charges(int current, int max = -1)
    {
        _item.CurrentCharges = current;
        _item.MaxCharges = max > 0 ? max : current;
        return this;
    }

    /// <summary>Set wand spell level.</summary>
    public ItemBuilder SpellLevel(int level)
    {
        if (_item.IsWand) _item.WandSpellLevel = level;
        if (_item.IsScroll) _item.ScrollSpellLevel = level;
        if (_item.IsPotion) _item.PotionSpellLevel = level;
        return this;
    }

    /// <summary>Set healing dice for potions/wands (e.g., 1d8+1 = Healing(1, 8, 1)).</summary>
    public ItemBuilder Healing(int diceCount, int diceSides, int bonus = 0)
    {
        _item.ConsumableEffect = ConsumableEffectType.HealHP;
        _item.HealDiceCount = diceCount;
        _item.HealDiceSides = diceSides;
        _item.HealBonus = bonus;
        return this;
    }

    // ════════════════════════════════════════════════════════════
    //  Ring Properties
    // ════════════════════════════════════════════════════════════

    /// <summary>Set deflection bonus to AC (Ring of Protection).</summary>
    public ItemBuilder DeflectionBonus(int bonus)
    {
        _item.RingDeflectionBonus = bonus;
        return this;
    }

    /// <summary>Set resistance bonus to all saves (Ring of Resistance).</summary>
    public ItemBuilder ResistanceSaveBonus(int bonus)
    {
        _item.RingResistanceSaveBonus = bonus;
        return this;
    }

    /// <summary>Set shield bonus from ring (Ring of Force Shield).</summary>
    public ItemBuilder RingShield(int bonus)
    {
        _item.RingShieldBonus = bonus;
        return this;
    }

    /// <summary>Set energy resistance (Ring of Energy Resistance).</summary>
    public ItemBuilder EnergyResistance(string energyType, int amount)
    {
        _item.RingEnergyType = energyType;
        _item.RingEnergyResistanceAmount = amount;
        return this;
    }

    /// <summary>Grant evasion (Ring of Evasion).</summary>
    public ItemBuilder GrantsEvasion()
    {
        _item.RingGrantsEvasion = true;
        return this;
    }

    /// <summary>Grant freedom of movement (Ring of Freedom of Movement).</summary>
    public ItemBuilder GrantsFreedomOfMovement()
    {
        _item.RingGrantsFreedomOfMovement = true;
        return this;
    }

    // ════════════════════════════════════════════════════════════
    //  Wondrous Item Properties
    // ════════════════════════════════════════════════════════════

    /// <summary>Set the equipment slot for wondrous items.</summary>
    public ItemBuilder Slot(EquipSlot slot)
    {
        _item.Slot = slot;
        if (_item.IsWondrous)
        {
            _item.WondrousRequiredSlot = slot;
            _item.IsSlotless = (slot == EquipSlot.Slotless);
        }
        return this;
    }

    // ════════════════════════════════════════════════════════════
    //  Ammunition Properties
    // ════════════════════════════════════════════════════════════

    /// <summary>Set ammunition type.</summary>
    public ItemBuilder AmmoType(AmmunitionType type)
    {
        _item.AmmoType = type;
        return this;
    }

    /// <summary>Set stack quantity and max.</summary>
    public ItemBuilder Quantity(int count, int max)
    {
        _item.Quantity = count;
        _item.MaxQuantity = max;
        _item.StackCount = count;
        _item.MaxStackSize = max;
        _item.IsStackable = true;
        return this;
    }

    // ════════════════════════════════════════════════════════════
    //  Common Properties (all item types)
    // ════════════════════════════════════════════════════════════

    /// <summary>Set weight in pounds.</summary>
    public ItemBuilder Weight(float lbs)
    {
        _item.WeightLbs = lbs;
        return this;
    }

    /// <summary>Set base price in gold pieces.</summary>
    public ItemBuilder Price(int gp)
    {
        _item.BasePriceGp = gp;
        return this;
    }

    /// <summary>Set icon character and color for UI display.</summary>
    public ItemBuilder Icon(string iconChar, Color color)
    {
        _item.IconChar = iconChar;
        _item.IconColor = color;
        return this;
    }

    /// <summary>Set icon character only (keeps current color).</summary>
    public ItemBuilder Icon(string iconChar)
    {
        _item.IconChar = iconChar;
        return this;
    }

    /// <summary>Add visual tags for display/filtering.</summary>
    public ItemBuilder Tags(params string[] tags)
    {
        if (_item.VisualTags == null) _item.VisualTags = new HashSet<string>();
        foreach (string tag in tags)
            _item.VisualTags.Add(tag);
        return this;
    }

    /// <summary>Mark item as stackable.</summary>
    public ItemBuilder Stackable(int maxStack = 20)
    {
        _item.IsStackable = true;
        _item.MaxStackSize = maxStack;
        return this;
    }

    /// <summary>Set a custom property on the item directly via an action.</summary>
    public ItemBuilder With(Action<ItemData> configure)
    {
        configure?.Invoke(_item);
        return this;
    }

    // ════════════════════════════════════════════════════════════
    //  Build — Finalize and Validate
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Finalize the item, validate required fields, and return the ItemData.
    /// Throws ArgumentException if validation fails.
    /// </summary>
    public ItemData Build()
    {
        if (_built)
            throw new InvalidOperationException("ItemBuilder.Build() has already been called. Create a new builder for each item.");
        _built = true;

        // Apply base item cloning if requested
        if (!string.IsNullOrEmpty(_baseItemId))
        {
            ItemData baseItem = ItemDatabase.GetItem(_baseItemId);
            if (baseItem == null)
                throw new ArgumentException($"ItemBuilder: Base item '{_baseItemId}' not found in ItemDatabase.");

            // Copy base stats but preserve the builder's overrides
            string savedId = _item.Id;
            string savedName = _item.Name;
            string savedDesc = _item.Description;
            ItemType savedType = _item.Type;
            int savedEnhancement = _item.EnhancementBonus;
            bool savedMasterwork = _item.IsMasterwork;
            bool savedMagic = _item.CountsAsMagicForBypass;

            // Copy all base properties
            CopyBaseProperties(baseItem);

            // Restore overrides
            _item.Id = savedId;
            _item.Type = savedType;
            if (!string.IsNullOrEmpty(savedName)) _item.Name = savedName;
            if (!string.IsNullOrEmpty(savedDesc)) _item.Description = savedDesc;
            if (savedEnhancement > 0) { _item.EnhancementBonus = savedEnhancement; _item.enhancementBonus = savedEnhancement; }
            if (savedMasterwork) _item.IsMasterwork = true;
            if (savedMagic) _item.CountsAsMagicForBypass = true;
        }

        // Auto-populate ScrollData for scroll items if not already set
        if (_item.IsScroll && _item.Scroll == null && !string.IsNullOrWhiteSpace(_item.ConsumableSpellName))
        {
            SpellDatabase.Init();
            SpellData spell = SpellDatabase.GetSpell(_item.ConsumableSpellName)
                              ?? SpellDatabase.GetSpellByName(_item.ConsumableSpellName);
            if (spell != null)
            {
                bool isArcane = _item.ScrollType == "Arcane";
                int cl = Mathf.Max(1, _item.ConsumableMinimumCasterLevel);
                int price = _item.BasePriceGp > 0 ? _item.BasePriceGp : (_item.ScrollSpellLevel * cl * 25);
                _item.Scroll = ScrollData.Create(spell, cl, isArcane, price,
                    _item.ScrollMetamagicFeats,
                    _item.ScrollEffectiveSpellLevel > 0 ? _item.ScrollEffectiveSpellLevel : -1,
                    _item.ScrollSavedDC);
                // Ensure ConsumableSpellName uses canonical spell ID
                _item.ConsumableSpellName = spell.SpellId;
            }
        }

        // Validation
        Validate();

        return _item;
    }

    /// <summary>
    /// Build and register the item in ItemDatabase. Returns the built item.
    /// </summary>
    public ItemData BuildAndRegister()
    {
        ItemData item = Build();
        ItemDatabase.Register(item);
        return item;
    }

    // ════════════════════════════════════════════════════════════
    //  Validation
    // ════════════════════════════════════════════════════════════

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(_item.Id))
            throw new ArgumentException("ItemBuilder: Item must have a non-empty Id.");

        if (string.IsNullOrWhiteSpace(_item.Name))
            throw new ArgumentException($"ItemBuilder [{_item.Id}]: Item must have a Name.");

        switch (_item.Type)
        {
            case ItemType.Weapon:
                ValidateWeapon();
                break;
            case ItemType.Armor:
                ValidateArmor();
                break;
            case ItemType.Shield:
                ValidateShield();
                break;
            case ItemType.Consumable:
                ValidateConsumable();
                break;
        }

        // Enhancement bonus validation (weapons + armor + shields)
        if (_item.EnhancementBonus > 0)
        {
            if (_item.EnhancementBonus < 1 || _item.EnhancementBonus > 5)
                throw new ArgumentException($"ItemBuilder [{_item.Id}]: Enhancement bonus must be 1-5, got {_item.EnhancementBonus}.");
        }
    }

    private void ValidateWeapon()
    {
        if (_item.DamageDice <= 0)
            throw new ArgumentException($"ItemBuilder [{_item.Id}]: Weapon must have DamageDice > 0.");
        if (_item.CritMultiplier < 2)
            throw new ArgumentException($"ItemBuilder [{_item.Id}]: Weapon CritMultiplier must be >= 2, got {_item.CritMultiplier}.");
        if (_item.CritThreatMin < 1 || _item.CritThreatMin > 20)
            throw new ArgumentException($"ItemBuilder [{_item.Id}]: CritThreatMin must be 1-20, got {_item.CritThreatMin}.");
    }

    private void ValidateArmor()
    {
        if (_item.ArmorBonus < 0)
            throw new ArgumentException($"ItemBuilder [{_item.Id}]: Armor bonus cannot be negative.");
    }

    private void ValidateShield()
    {
        if (_item.ShieldBonus < 0)
            throw new ArgumentException($"ItemBuilder [{_item.Id}]: Shield bonus cannot be negative.");
    }

    private void ValidateConsumable()
    {
        if (_item.IsScroll)
        {
            if (string.IsNullOrWhiteSpace(_item.ConsumableSpellName))
                throw new ArgumentException($"ItemBuilder [{_item.Id}]: Scroll must have a spell set via ForSpell().");
            if (_item.ScrollSpellLevel < 0)
                throw new ArgumentException($"ItemBuilder [{_item.Id}]: Scroll spell level must be >= 0.");
        }

        if (_item.IsWand)
        {
            if (string.IsNullOrWhiteSpace(_item.ConsumableSpellName) && string.IsNullOrWhiteSpace(_item.WandSpellId))
                throw new ArgumentException($"ItemBuilder [{_item.Id}]: Wand must have a spell set via ForSpell().");
            if (_item.MaxCharges <= 0)
                throw new ArgumentException($"ItemBuilder [{_item.Id}]: Wand must have MaxCharges > 0.");
        }

        if (_item.IsPotion)
        {
            if (string.IsNullOrWhiteSpace(_item.ConsumableSpellName))
                throw new ArgumentException($"ItemBuilder [{_item.Id}]: Potion must have a spell set via ForSpell().");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Internal: Copy base properties for FromBase()
    // ════════════════════════════════════════════════════════════

    private void CopyBaseProperties(ItemData src)
    {
        // Weapon stats
        _item.Proficiency = src.Proficiency;
        _item.WeaponCat = src.WeaponCat;
        _item.WeaponSize = src.WeaponSize;
        _item.DamageDice = src.DamageDice;
        _item.DamageCount = src.DamageCount;
        _item.BonusDamage = src.BonusDamage;
        _item.AttackRange = src.AttackRange;
        _item.IsLightWeapon = src.IsLightWeapon;
        _item.IsTwoHanded = src.IsTwoHanded;
        _item.HasReach = src.HasReach;
        _item.ReachSquares = src.ReachSquares;
        _item.CanAttackAdjacent = src.CanAttackAdjacent;
        _item.IsReachWeapon = src.IsReachWeapon;
        _item.DamageType = src.DamageType;
        _item.DmgModType = src.DmgModType;
        _item.CritThreatMin = src.CritThreatMin;
        _item.CritMultiplier = src.CritMultiplier;
        _item.IsThrown = src.IsThrown;
        _item.RangeIncrement = src.RangeIncrement;
        _item.RequiresReload = src.RequiresReload;
        _item.ReloadAction = src.ReloadAction;
        _item.RequiresAmmoType = src.RequiresAmmoType;
        _item.CompositeRating = src.CompositeRating;
        _item.DealsNonlethalDamage = src.DealsNonlethalDamage;
        _item.DesignedForSize = src.DesignedForSize;

        // Armor stats
        _item.ArmorBonus = src.ArmorBonus;
        _item.ShieldBonus = src.ShieldBonus;
        _item.ArmorCat = src.ArmorCat;
        _item.ArmorMaterial = src.ArmorMaterial;
        _item.MaxDexBonus = src.MaxDexBonus;
        _item.ArmorCheckPenalty = src.ArmorCheckPenalty;
        _item.ArcaneSpellFailure = src.ArcaneSpellFailure;

        // Common stats
        _item.Slot = src.Slot;
        _item.WeightLbs = src.WeightLbs;
        _item.BasePriceGp = src.BasePriceGp;
        _item.IconChar = src.IconChar;
        _item.IconColor = src.IconColor;
        _item.SpecialProperties = src.SpecialProperties;
        _item.NoStrengthToDamage = src.NoStrengthToDamage;
        _item.WhipLikeArmorRestriction = src.WhipLikeArmorRestriction;
        if (src.VisualTags != null && src.VisualTags.Count > 0)
            _item.VisualTags = new HashSet<string>(src.VisualTags);

        // Preserve base name/description as fallback if builder hasn't set them
        if (string.IsNullOrEmpty(_item.Name)) _item.Name = src.Name;
        if (string.IsNullOrEmpty(_item.Description)) _item.Description = src.Description;
    }
}
