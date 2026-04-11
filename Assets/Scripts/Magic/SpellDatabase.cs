using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static database of all spells in the game.
/// D&D 3.5e PHB spell definitions for Wizard and Cleric (levels 0-2).
///
/// Spell Status Key:
///   FUNCTIONAL = Full mechanics implemented (damage, healing, buffs, saves)
///   PLACEHOLDER = Description only, mechanics not yet implemented
///
/// Total spells: ~140 (Wizard 0/1/2 + Cleric 0/1/2)
/// Functional: ~60-70% (combat-relevant spells)
/// Placeholder: ~30-40% (summoning, illusions, complex utility)
/// </summary>
public static class SpellDatabase
{
    private static Dictionary<string, SpellData> _spells;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _spells = new Dictionary<string, SpellData>();

        // ================================================================
        //  WIZARD CANTRIPS (Level 0)  — PHB p.192-196
        // ================================================================
        RegisterWizardCantrips();

        // ================================================================
        //  WIZARD 1ST LEVEL SPELLS  — PHB p.196-230 (approx)
        // ================================================================
        RegisterWizard1stLevel();

        // ================================================================
        //  WIZARD 2ND LEVEL SPELLS  — PHB p.230-260 (approx)
        // ================================================================
        RegisterWizard2ndLevel();

        // ================================================================
        //  CLERIC CANTRIPS (Level 0 Orisons)  — PHB p.183-184
        // ================================================================
        RegisterClericCantrips();

        // ================================================================
        //  CLERIC 1ST LEVEL SPELLS  — PHB p.184-210 (approx)
        // ================================================================
        RegisterCleric1stLevel();

        // ================================================================
        //  CLERIC 2ND LEVEL SPELLS  — PHB p.210-230 (approx)
        // ================================================================
        RegisterCleric2ndLevel();

        int total = _spells.Count;
        int functional = 0;
        int placeholder = 0;
        foreach (var s in _spells.Values)
        {
            if (s.IsPlaceholder) placeholder++;
            else functional++;
        }
        Debug.Log($"[SpellDatabase] Initialized with {total} spells ({functional} functional, {placeholder} placeholders).");
    }

    // ====================================================================
    //  WIZARD CANTRIPS (Level 0)
    // ====================================================================
    private static void RegisterWizardCantrips()
    {
        // --- FUNCTIONAL: Damage Cantrips ---

        Register(new SpellData
        {
            SpellId = "ray_of_frost",
            Name = "Ray of Frost",
            Description = "A ray of freezing air and ice deals 1d3 cold damage. Ranged touch attack.",
            SpellLevel = 0, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5, // Close range ~25ft
            EffectType = SpellEffectType.Damage,
            DamageDice = 3, DamageCount = 1,
            DamageType = "cold",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "acid_splash",
            Name = "Acid Splash",
            Description = "An orb of acid deals 1d3 acid damage on a ranged touch attack.",
            SpellLevel = 0, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 3, DamageCount = 1,
            DamageType = "acid",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "disrupt_undead",
            Name = "Disrupt Undead",
            Description = "Deals 1d6 positive energy damage to one undead creature. Ranged touch attack.",
            SpellLevel = 0, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 1,
            DamageType = "positive",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "electric_jolt",
            Name = "Electric Jolt",
            Description = "Deals 1d3 electricity damage. Ranged touch attack. (Spell Compendium)",
            SpellLevel = 0, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 3, DamageCount = 1,
            DamageType = "electricity",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- PLACEHOLDER: Utility Cantrips ---

        Register(new SpellData
        {
            SpellId = "detect_magic_wiz",
            Name = "Detect Magic",
            Description = "Detects spells and magic items within 60 ft cone for up to 1 min/level.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 10, // 1 min/level, ~10 rounds at CL3
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Detection mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "read_magic",
            Name = "Read Magic",
            Description = "Read scrolls and spellbooks. Duration 10 min/level.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Scroll/spellbook reading not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "prestidigitation",
            Name = "Prestidigitation",
            Description = "Performs minor tricks: clean, soil, color, flavor, chill, warm, create small trinket. Lasts 1 hour.",
            SpellLevel = 0, School = "Universal",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 2,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Utility effects not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "detect_poison_wiz",
            Name = "Detect Poison",
            Description = "Detects poison in one creature or small object.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff, // detection
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Poison detection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "dancing_lights",
            Name = "Dancing Lights",
            Description = "Creates up to four lights that move as you direct. Lasts 1 minute.",
            SpellLevel = 0, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 20,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 10,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Light/illumination not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "flare",
            Name = "Flare",
            Description = "Dazzles one creature (–1 on attack rolls). Fortitude save negates.",
            SpellLevel = 0, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            BuffDurationRounds = -1, // Until end of encounter
            BuffAttackBonus = -1, // -1 to attack rolls (dazzled)
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "ghost_sound",
            Name = "Ghost Sound",
            Description = "Figment sounds. Will disbelief (if interacted with).",
            SpellLevel = 0, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Illusion mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "light",
            Name = "Light",
            Description = "Object shines like a torch for 10 min/level.",
            SpellLevel = 0, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Light/illumination not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "mage_hand",
            Name = "Mage Hand",
            Description = "5-pound telekinesis. Move one nonmagical, unattended object up to 5 lb.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Telekinesis not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "mending",
            Name = "Mending",
            Description = "Makes minor repairs on an object (1d4 damage repaired).",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 2,
            EffectType = SpellEffectType.Healing,
            HealDice = 4, HealCount = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object repair not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "message",
            Name = "Message",
            Description = "Whispered conversation at distance. Range: 100 ft + 10 ft/level.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 10,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Communication not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "open_close",
            Name = "Open/Close",
            Description = "Opens or closes small or light things (door, chest, bottle, etc.).",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object interaction not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "arcane_mark",
            Name = "Arcane Mark",
            Description = "Inscribes a personal rune (visible or invisible) on an object or creature.",
            SpellLevel = 0, School = "Universal",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Marking not implemented]"
        });

        // Resistance cantrip (functional - minor save buff)
        Register(new SpellData
        {
            SpellId = "resistance_wiz",
            Name = "Resistance",
            Description = "Subject gains +1 on saving throws for 1 minute.",
            SpellLevel = 0, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1, // Touch
            EffectType = SpellEffectType.Buff,
            BuffSaveBonus = 1,
            BuffDurationRounds = 10, // 1 minute
            BuffType = "resistance",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "daze",
            Name = "Daze",
            Description = "Humanoid creature of 4 HD or less loses next action. Will save negates.",
            SpellLevel = 0, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "touch_of_fatigue",
            Name = "Touch of Fatigue",
            Description = "Touch attack fatigues target (–2 STR and DEX, can't run or charge). Fort save negates.",
            SpellLevel = 0, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1, // Touch
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            BuffStatBonus = -2, // -2 to STR and DEX
            BuffDurationRounds = 10, // 1 round/level at CL3 = 3, simplified to 10
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });
    }

    // ====================================================================
    //  WIZARD 1ST LEVEL SPELLS
    // ====================================================================
    private static void RegisterWizard1stLevel()
    {
        // --- FUNCTIONAL: Damage Spells ---

        Register(new SpellData
        {
            SpellId = "magic_missile",
            Name = "Magic Missile",
            Description = "1d4+1 force damage per missile, auto-hit. 2 missiles at CL3. No save, no SR.",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22, // Medium range
            EffectType = SpellEffectType.Damage,
            DamageDice = 4, DamageCount = 1, BonusDamage = 1,
            DamageType = "force",
            AutoHit = true,
            MissileCount = 2, // CL3
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "burning_hands",
            Name = "Burning Hands",
            Description = "1d4/level fire damage (max 5d4) in 15-ft cone. Reflex half. PHB p.207",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified from cone
            RangeSquares = 3, // 15-ft cone ~3 squares
            AreaRadius = 3,
            EffectType = SpellEffectType.Damage,
            DamageDice = 4, DamageCount = 3, // 3d4 at CL3
            DamageType = "fire",
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "shocking_grasp",
            Name = "Shocking Grasp",
            Description = "Touch delivers 1d6/level electricity damage (max 5d6). +3 attack vs metal armor. PHB p.279",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1, // Touch
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 3, // 3d6 at CL3
            DamageType = "electricity",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "chromatic_orb",
            Name = "Chromatic Orb",
            Description = "Ranged touch attack deals 1d8 damage (type varies by caster level). At CL3: fire, 1d8.",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 8, DamageCount = 1,
            DamageType = "fire",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "chill_touch",
            Name = "Chill Touch",
            Description = "1 touch/level, each dealing 1d6 negative energy damage and 1 STR damage. Fort save negates STR damage.",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1, // Touch
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 1,
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            SaveHalves = false, // Save negates STR damage only
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Buff Spells ---

        Register(new SpellData
        {
            SpellId = "mage_armor",
            Name = "Mage Armor",
            Description = "+4 armor bonus to AC for 1 hour/level. Doesn't stack with actual armor. PHB p.249",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 4,
            BuffDurationRounds = -1, // 1 hour/level
            BuffType = "armor",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "shield",
            Name = "Shield",
            Description = "+4 shield bonus to AC, blocks Magic Missile. Duration 1 min/level. PHB p.278",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffShieldBonus = 4,
            BuffDurationRounds = 30, // 1 min/level = 30 rounds at CL3
            BuffType = "shield",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "enlarge_person",
            Name = "Enlarge Person",
            Description = "Humanoid creature doubles in size. +2 STR, -2 DEX, -1 AC/attack (size). Duration 1 min/level. PHB p.226",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "STR",
            BuffStatBonus = 2, // +2 size bonus to STR
            BuffDurationRounds = 30,
            BuffType = "enlarge",
            AllowsSavingThrow = true, // Fortitude negates (unwilling)
            SavingThrowType = "Fortitude",
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "protection_from_evil",
            Name = "Protection from Evil",
            Description = "+2 deflection bonus to AC and +2 resistance bonus on saves vs evil creatures. 1 min/level. PHB p.266",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDeflectionBonus = 2,
            BuffSaveBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "protection",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "true_strike",
            Name = "True Strike",
            Description = "+20 insight bonus on your next single attack roll. PHB p.296",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 20,
            BuffDurationRounds = 1, // Until next attack or end of next round
            BuffType = "insight",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "expeditious_retreat",
            Name = "Expeditious Retreat",
            Description = "Your base speed increases by 30 ft (+6 squares). Duration 1 min/level. PHB p.228",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            BuffType = "speed",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Debuff Spells ---

        Register(new SpellData
        {
            SpellId = "sleep",
            Name = "Sleep",
            Description = "Puts 4 HD of creatures into magical slumber. Will save negates. Range: Medium. PHB p.280",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 10, // 1 min/level
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "charm_person",
            Name = "Charm Person",
            Description = "Makes one humanoid creature friendly to you. Will save negates. Duration 1 hr/level. PHB p.209",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Charm/mind control not implemented in combat]"
        });

        Register(new SpellData
        {
            SpellId = "grease",
            Name = "Grease",
            Description = "Covers ground in grease, creatures must save or fall. Reflex save. 1 round/level. PHB p.237",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified
            RangeSquares = 5,
            AreaRadius = 2,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "color_spray",
            Name = "Color Spray",
            Description = "Knocks unconscious, blinds, and/or stuns weak creatures in 15-ft cone. Will save negates. PHB p.210",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified from cone
            RangeSquares = 3,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3, // Varies by HD
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "cause_fear",
            Name = "Cause Fear",
            Description = "One creature of 5 HD or less flees for 1d4 rounds. Will save: shaken for 1 round instead. PHB p.208",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3, // 1d4 rounds avg
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "ray_of_enfeeblement",
            Name = "Ray of Enfeeblement",
            Description = "Ranged touch attack. Target takes 1d6+1 per 2 CL (max +5) STR penalty. No save. 1 min/level. PHB p.269",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            DamageDice = 6, DamageCount = 1, BonusDamage = 1, // 1d6+1 STR penalty at CL3
            DamageType = "str_penalty",
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "reduce_person",
            Name = "Reduce Person",
            Description = "Humanoid creature halves in size. -2 STR, +2 DEX, +1 AC/attack (size). 1 min/level. PHB p.269",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "DEX",
            BuffStatBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "reduce",
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true
        });

        // --- PLACEHOLDER: Utility/Complex ---

        Register(new SpellData
        {
            SpellId = "identify",
            Name = "Identify",
            Description = "Determines all magic properties of a single magic item. Requires 100gp pearl. PHB p.243",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Item identification not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "feather_fall",
            Name = "Feather Fall",
            Description = "Objects or creatures fall slowly (60 ft/round). Immediate action. PHB p.229",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Free, // Immediate action
            ProvokesAoO = false,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Falling mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "comprehend_languages",
            Name = "Comprehend Languages",
            Description = "You understand all spoken and written languages. Duration 10 min/level. PHB p.212",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Language mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "summon_monster_1",
            Name = "Summon Monster I",
            Description = "Calls a creature to fight for you. Duration 1 round/level. PHB p.285",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 3,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "unseen_servant",
            Name = "Unseen Servant",
            Description = "Invisible, mindless force that performs simple tasks. Duration 1 hr/level. PHB p.297",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Servant/minion not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "silent_image",
            Name = "Silent Image",
            Description = "Creates minor illusion of your design. Concentration + 2 rounds. Will disbelief. PHB p.279",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 8,
            EffectType = SpellEffectType.Buff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 5,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Illusion mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "disguise_self",
            Name = "Disguise Self",
            Description = "Changes your appearance. Duration 10 min/level. Will disbelief (if interacted). PHB p.222",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Disguise not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "obscuring_mist",
            Name = "Obscuring Mist",
            Description = "Fog surrounds you, granting concealment (20% miss chance). 20-ft radius. 1 min/level. PHB p.258",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 4,
            AreaRadius = 4,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            BuffType = "concealment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Concealment/fog not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "magic_weapon",
            Name = "Magic Weapon",
            Description = "Weapon gains +1 enhancement bonus on attack and damage. 1 min/level. PHB p.251",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffDamageBonus = 1,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "mount",
            Name = "Mount",
            Description = "Summons a riding horse for 2 hr/level. PHB p.256",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Mount/summoning not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "alarm",
            Name = "Alarm",
            Description = "Wards an area for 2 hours/level. Mental or audible alarm when creature enters. PHB p.197",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Ward/alarm not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "endure_elements",
            Name = "Endure Elements",
            Description = "Exist comfortably in hot or cold environments. Duration 24 hours. PHB p.226",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Environmental protection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "hold_portal",
            Name = "Hold Portal",
            Description = "Holds door shut as if locked. Duration 1 min/level. PHB p.241",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Door mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "floating_disk",
            Name = "Floating Disk",
            Description = "Creates 3-ft diameter horizontal disk that holds 100 lb/level. Follows you. 1 hr/level. PHB p.232",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Carrying/utility not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "hypnotism",
            Name = "Hypnotism",
            Description = "Fascinates 2d4 HD of creatures. Will save negates. PHB p.242",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Fascination mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "nystuls_magic_aura",
            Name = "Nystul's Magic Aura",
            Description = "Alters an object's magic aura. Duration 1 day/level. PHB p.257",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Aura manipulation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "ventriloquism",
            Name = "Ventriloquism",
            Description = "Throws voice for 1 min/level. Will disbelief. PHB p.298",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Sound manipulation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "animate_rope",
            Name = "Animate Rope",
            Description = "Makes a rope move at your command. Duration 1 round/level. PHB p.199",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Rope animation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "erase",
            Name = "Erase",
            Description = "Mundane or magical writing vanishes. PHB p.227",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Writing mechanics not implemented]"
        });
    }

    // ====================================================================
    //  WIZARD 2ND LEVEL SPELLS
    // ====================================================================
    private static void RegisterWizard2ndLevel()
    {
        // --- FUNCTIONAL: Damage Spells ---

        Register(new SpellData
        {
            SpellId = "scorching_ray",
            Name = "Scorching Ray",
            Description = "Ranged touch attack, 4d6 fire damage per ray. 1 ray at CL3 (2 at CL7, 3 at CL11). PHB p.274",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 4, // 4d6 per ray
            DamageType = "fire",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "acid_arrow",
            Name = "Acid Arrow",
            Description = "Ranged touch attack, 2d4 acid damage + 2d4/round for 1 round per 3 CL. PHB p.196",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 8, // Long range
            EffectType = SpellEffectType.Damage,
            DamageDice = 4, DamageCount = 2, // Initial 2d4
            DamageType = "acid",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "flaming_sphere",
            Name = "Flaming Sphere",
            Description = "Rolling ball of fire, 2d6 fire damage. Reflex negates. Lasts 1 round/level, movable. PHB p.232",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 2, // 2d6
            DamageType = "fire",
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            SaveHalves = false, // Negates
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "shatter",
            Name = "Shatter",
            Description = "Sonic vibration damages objects or crystalline creatures. 1d6/level (max 10d6) sonic. PHB p.278",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 3, // 3d6 at CL3
            DamageType = "sonic",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = false,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "gust_of_wind",
            Name = "Gust of Wind",
            Description = "Blasts of wind knock down/push creatures. Fortitude save or be blown away (size-dependent). PHB p.238",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 12, // 60 ft
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Buff Spells ---

        Register(new SpellData
        {
            SpellId = "bulls_strength",
            Name = "Bull's Strength",
            Description = "Subject gains +4 enhancement bonus to STR for 1 min/level. PHB p.207",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1, // Touch
            EffectType = SpellEffectType.Buff,
            BuffStatName = "STR",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "cats_grace",
            Name = "Cat's Grace",
            Description = "Subject gains +4 enhancement bonus to DEX for 1 min/level. PHB p.208",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "DEX",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "bears_endurance",
            Name = "Bear's Endurance",
            Description = "Subject gains +4 enhancement bonus to CON for 1 min/level. PHB p.203",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "CON",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "foxs_cunning",
            Name = "Fox's Cunning",
            Description = "Subject gains +4 enhancement bonus to INT for 1 min/level. PHB p.233",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "INT",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "owls_wisdom",
            Name = "Owl's Wisdom",
            Description = "Subject gains +4 enhancement bonus to WIS for 1 min/level. PHB p.259",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "WIS",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "eagles_splendor",
            Name = "Eagle's Splendor",
            Description = "Subject gains +4 enhancement bonus to CHA for 1 min/level. PHB p.225",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "CHA",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "invisibility",
            Name = "Invisibility",
            Description = "Subject is invisible for 1 min/level or until it attacks. +2 attack on first strike. PHB p.245",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 2, // Simplified: +2 from invisibility on first attack
            BuffDurationRounds = 30,
            BuffType = "invisibility",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "mirror_image",
            Name = "Mirror Image",
            Description = "Creates 1d4+1 decoy duplicates of you (CL3). Attacks may hit images instead. 1 min/level. PHB p.254",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 4, // Simplified: effective +4 AC from images
            BuffDurationRounds = 30,
            BuffType = "mirror_image",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "protection_from_arrows",
            Name = "Protection from Arrows",
            Description = "Subject gains DR 10/magic vs ranged weapons. Absorbs 10/CL damage (30 at CL3). 1 hr/level. PHB p.266",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "DR_arrows",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Damage reduction not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "resist_energy",
            Name = "Resist Energy",
            Description = "Grants resistance 10 to specified energy type (fire, cold, acid, electricity, sonic). 10 min/level. PHB p.272",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "energy_resistance",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Energy resistance not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "false_life",
            Name = "False Life",
            Description = "Gain 1d10+CL temporary hit points (max +10). Duration 1 hour/level. PHB p.229",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffTempHP = 8, // ~average of 1d10+3 at CL3
            BuffDurationRounds = -1,
            BuffType = "temp_hp",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Debuff Spells ---

        Register(new SpellData
        {
            SpellId = "web",
            Name = "Web",
            Description = "Fills 20-ft-radius spread with sticky webs. Reflex save or stuck. STR/Escape check to escape. 10 min/level. PHB p.301",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified from area
            RangeSquares = 22,
            AreaRadius = 4,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "glitterdust",
            Name = "Glitterdust",
            Description = "Blinds creatures in area and outlines invisible creatures. Will save negates blindness. 1 round/level. PHB p.236",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified
            RangeSquares = 22,
            AreaRadius = 2,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "touch_of_idiocy",
            Name = "Touch of Idiocy",
            Description = "Touch attack reduces target's INT, WIS, and CHA by 1d6 each. No save. 10 min/level. PHB p.294",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1, // Touch
            EffectType = SpellEffectType.Debuff,
            DamageDice = 6, DamageCount = 1, // 1d6 to each mental stat
            DamageType = "mental_drain",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "blindness_deafness_wiz",
            Name = "Blindness/Deafness",
            Description = "Makes subject blind or deaf. Fortitude negates. Permanent. PHB p.206",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "daze_monster",
            Name = "Daze Monster",
            Description = "Living creature of 6 HD or less loses next action. Will save negates. PHB p.217",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "hideous_laughter",
            Name = "Hideous Laughter",
            Description = "Subject laughs uncontrollably, falls prone. Will save negates. 1 round/level. PHB p.240",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "scare",
            Name = "Scare",
            Description = "Frightens creatures of less than 6 HD. Will save negates. 1 round/level. PHB p.274",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "spectral_hand",
            Name = "Spectral Hand",
            Description = "Creates ghostly hand to deliver touch spells at range. +2 on touch attacks via hand. 1 min/level. PHB p.282",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "spectral_hand",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- PLACEHOLDER: Utility/Complex ---

        Register(new SpellData
        {
            SpellId = "knock",
            Name = "Knock",
            Description = "Opens locked or magically sealed door. PHB p.246",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Lock/door mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "levitate",
            Name = "Levitate",
            Description = "Subject moves up or down at your direction. 1 min/level. Will negates (object). PHB p.248",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            BuffType = "levitate",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Vertical movement not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "see_invisibility",
            Name = "See Invisibility",
            Description = "You can see invisible creatures and objects. Duration 10 min/level. PHB p.275",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "see_invis",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Invisibility detection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "detect_thoughts",
            Name = "Detect Thoughts",
            Description = "Allows listening to surface thoughts. Concentration, up to 1 min/level. Will negates. PHB p.220",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 12,
            EffectType = SpellEffectType.Buff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Mind reading not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "locate_object",
            Name = "Locate Object",
            Description = "Senses direction toward object. Duration 1 min/level. PHB p.249",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object location not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "summon_monster_2",
            Name = "Summon Monster II",
            Description = "Calls creature to fight for you. Duration 1 round/level. PHB p.286",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 3,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "minor_image",
            Name = "Minor Image",
            Description = "As silent image, plus some sound. Concentration + 2 rounds. Will disbelief. PHB p.254",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 8,
            EffectType = SpellEffectType.Buff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 5,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Illusion mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "blur",
            Name = "Blur",
            Description = "Attacks against subject have 20% miss chance. Duration 1 min/level. PHB p.206",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 2, // Simplified: 20% miss ~= +2 AC effective
            BuffDurationRounds = 30,
            BuffType = "concealment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "alter_self",
            Name = "Alter Self",
            Description = "Assume form of a similar creature. +10 Disguise, may gain abilities. 10 min/level. PHB p.197",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Transformation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "darkvision",
            Name = "Darkvision",
            Description = "See 60 ft in total darkness. Duration 1 hr/level. PHB p.216",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Vision/darkness not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "rope_trick",
            Name = "Rope Trick",
            Description = "As many as 8 creatures hide in extradimensional space. Duration 1 hr/level. PHB p.273",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Extradimensional space not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "spider_climb",
            Name = "Spider Climb",
            Description = "Grants ability to walk on walls and ceilings. Speed 20 ft. Duration 10 min/level. PHB p.283",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "spider_climb",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Wall climbing not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "whispering_wind",
            Name = "Whispering Wind",
            Description = "Sends a short message or sound to a distant location. PHB p.301",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Long-range communication not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "arcane_lock",
            Name = "Arcane Lock",
            Description = "Magically locks a portal or chest. Permanent. PHB p.200",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Lock mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "obscure_object",
            Name = "Obscure Object",
            Description = "Masks object against scrying. Duration 8 hours. PHB p.258",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Anti-scrying not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "continual_flame",
            Name = "Continual Flame",
            Description = "Makes a permanent, heatless flame. Requires 50 gp ruby dust. PHB p.213",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Light/illumination not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "command_undead",
            Name = "Command Undead",
            Description = "Undead creature obeys your commands. Will save for intelligent undead. 1 day/level. PHB p.211",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Undead control not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "ghoul_touch",
            Name = "Ghoul Touch",
            Description = "Touch attack paralyzes one living subject for 1d6+2 rounds. Fort negates. Sickens nearby. PHB p.235",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            BuffDurationRounds = 5, // 1d6+2 avg
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "hypnotic_pattern",
            Name = "Hypnotic Pattern",
            Description = "Fascinates 2d4+CL HD of creatures. Will negates. Concentration + 2 rounds. PHB p.242",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 5,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Fascination mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "pyrotechnics",
            Name = "Pyrotechnics",
            Description = "Turns fire into blinding light or choking smoke. PHB p.267",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 8,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            AreaRadius = 4,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Fire interaction not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "fog_cloud",
            Name = "Fog Cloud",
            Description = "Fog obscures vision, 20-ft radius. Duration 10 min/level. PHB p.232",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            AreaRadius = 4,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Fog/concealment area not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "summon_swarm",
            Name = "Summon Swarm",
            Description = "Summons swarm of bats, rats, or spiders. 2d6 damage/round. Concentration + 2 rounds. PHB p.289",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 2,
            DamageType = "swarm",
            BuffDurationRounds = 5,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Swarm summoning not implemented]"
        });
    }

    // ====================================================================
    //  CLERIC CANTRIPS (Level 0 - Orisons)
    // ====================================================================
    private static void RegisterClericCantrips()
    {
        // --- FUNCTIONAL: Healing ---

        Register(new SpellData
        {
            SpellId = "cure_minor_wounds",
            Name = "Cure Minor Wounds",
            Description = "Cures 1 point of damage. Touch range.",
            SpellLevel = 0, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Healing,
            HealDice = 0, HealCount = 0, BonusHealing = 1, // Fixed 1 HP
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Damage ---

        Register(new SpellData
        {
            SpellId = "inflict_minor_wounds",
            Name = "Inflict Minor Wounds",
            Description = "Touch attack deals 1 point of negative energy damage. Will save halves.",
            SpellLevel = 0, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1,
            EffectType = SpellEffectType.Damage,
            DamageDice = 0, DamageCount = 0, BonusDamage = 1,
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Utility/Buff Orisons ---

        Register(new SpellData
        {
            SpellId = "guidance",
            Name = "Guidance",
            Description = "+1 on one attack roll, saving throw, or skill check. Duration 1 minute or until discharged.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffSaveBonus = 1,
            BuffDurationRounds = 10,
            BuffType = "competence",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "virtue",
            Name = "Virtue",
            Description = "Subject gains 1 temporary hit point for 1 minute.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffTempHP = 1,
            BuffDurationRounds = 10,
            BuffType = "temp_hp",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "resistance_clr",
            Name = "Resistance",
            Description = "Subject gains +1 on saving throws for 1 minute.",
            SpellLevel = 0, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffSaveBonus = 1,
            BuffDurationRounds = 10,
            BuffType = "resistance",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "detect_magic_clr",
            Name = "Detect Magic",
            Description = "Detects spells and magic items within 60 ft cone for up to 1 min/level.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 10,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Detection mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "detect_poison_clr",
            Name = "Detect Poison",
            Description = "Detects poison in one creature or small object.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Poison detection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "light_clr",
            Name = "Light",
            Description = "Object shines like a torch for 10 min/level.",
            SpellLevel = 0, School = "Evocation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Light/illumination not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "mending_clr",
            Name = "Mending",
            Description = "Makes minor repairs on an object.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 2,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object repair not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "purify_food_drink",
            Name = "Purify Food and Drink",
            Description = "Purifies 1 cu.ft./level of food and water.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 2,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Food/water mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "create_water",
            Name = "Create Water",
            Description = "Creates 2 gallons/level of pure water.",
            SpellLevel = 0, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Water creation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "read_magic_clr",
            Name = "Read Magic",
            Description = "Read scrolls and spellbooks. Duration 10 min/level.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Scroll reading not implemented]"
        });
    }

    // ====================================================================
    //  CLERIC 1ST LEVEL SPELLS
    // ====================================================================
    private static void RegisterCleric1stLevel()
    {
        // --- FUNCTIONAL: Healing ---

        Register(new SpellData
        {
            SpellId = "cure_light_wounds",
            Name = "Cure Light Wounds",
            Description = "Heals 1d8 + caster level (max +5) HP. PHB p.215",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Healing,
            HealDice = 8, HealCount = 1, BonusHealing = 3, // +CL (3 at CL3, max +5)
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Damage ---

        Register(new SpellData
        {
            SpellId = "inflict_light_wounds",
            Name = "Inflict Light Wounds",
            Description = "Touch attack deals 1d8 + CL (max +5) negative energy damage. Will save half. PHB p.244",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1,
            EffectType = SpellEffectType.Damage,
            DamageDice = 8, DamageCount = 1, BonusDamage = 3, // +CL
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Buff Spells ---

        Register(new SpellData
        {
            SpellId = "bless",
            Name = "Bless",
            Description = "Allies gain +1 morale bonus on attack rolls and saves vs fear. 1 min/level. PHB p.205",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self, // Area: 50-ft burst centered on caster (simplified to self/party)
            RangeSquares = -1,
            AreaRadius = 10,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffSaveBonus = 1, // vs fear, simplified to all saves
            BuffDurationRounds = 30,
            BuffType = "morale",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "shield_of_faith",
            Name = "Shield of Faith",
            Description = "+2 deflection bonus to AC. Duration 1 min/level. PHB p.278",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDeflectionBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "deflection",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "divine_favor",
            Name = "Divine Favor",
            Description = "+1 luck bonus on attack and damage rolls (per 3 CL, max +3). Duration 1 minute. PHB p.224",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffDamageBonus = 1,
            BuffDurationRounds = 10,
            BuffType = "luck",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "protection_from_evil_clr",
            Name = "Protection from Evil",
            Description = "+2 deflection bonus to AC and +2 resistance bonus on saves vs evil. 1 min/level. PHB p.266",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDeflectionBonus = 2,
            BuffSaveBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "protection",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "magic_weapon_clr",
            Name = "Magic Weapon",
            Description = "Weapon gains +1 enhancement bonus on attack and damage. 1 min/level. PHB p.251",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffDamageBonus = 1,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "endure_elements_clr",
            Name = "Endure Elements",
            Description = "Exist comfortably in hot or cold environments. Duration 24 hours. PHB p.226",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Environmental protection not implemented]"
        });

        // --- FUNCTIONAL: Debuff Spells ---

        Register(new SpellData
        {
            SpellId = "bane",
            Name = "Bane",
            Description = "Enemies take –1 on attack rolls and saves vs fear. 1 min/level. Will save negates. PHB p.203",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified from area
            RangeSquares = 10,
            AreaRadius = 10,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffAttackBonus = -1,
            BuffSaveBonus = -1,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "doom",
            Name = "Doom",
            Description = "One subject is shaken (–2 on attack, saves, skills, ability checks). Will save negates. 1 min/level. PHB p.225",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffAttackBonus = -2,
            BuffSaveBonus = -2,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "command",
            Name = "Command",
            Description = "One subject obeys selected command for 1 round: approach, drop, fall, flee, halt. Will negates. PHB p.211",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "cause_fear_clr",
            Name = "Cause Fear",
            Description = "One creature of 5 HD or less flees for 1d4 rounds. Will save: shaken 1 round. PHB p.208",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "entropic_shield",
            Name = "Entropic Shield",
            Description = "Ranged attacks against you have 20% miss chance. Duration 1 min/level. PHB p.227",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 2, // Simplified: 20% miss ~= +2 AC vs ranged
            BuffDurationRounds = 30,
            BuffType = "entropic",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- PLACEHOLDER: Utility ---

        Register(new SpellData
        {
            SpellId = "detect_evil",
            Name = "Detect Evil",
            Description = "Reveals evil creatures, spells, or objects. Duration: Concentration up to 10 min/level. PHB p.218",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Alignment detection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "detect_undead",
            Name = "Detect Undead",
            Description = "Reveals undead within 60 ft. Concentration up to 1 min/level. PHB p.220",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Undead detection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "sanctuary",
            Name = "Sanctuary",
            Description = "Opponents can't attack you unless they make a Will save. 1 round/level. PHB p.274",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            BuffType = "sanctuary",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Attack prevention not fully implemented]"
        });

        Register(new SpellData
        {
            SpellId = "comprehend_languages_clr",
            Name = "Comprehend Languages",
            Description = "Understand all spoken and written languages. Duration 10 min/level. PHB p.212",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Language mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "remove_fear",
            Name = "Remove Fear",
            Description = "Suppresses fear or gives +4 morale bonus vs fear for 10 min. One ally +1 per 4 CL. PHB p.271",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffSaveBonus = 4, // +4 morale vs fear, simplified
            BuffDurationRounds = -1,
            BuffType = "morale",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "hide_from_undead",
            Name = "Hide from Undead",
            Description = "Undead can't perceive one subject/level. Duration 10 min/level. Will negates (intelligent undead). PHB p.241",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Undead perception not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "obscuring_mist_clr",
            Name = "Obscuring Mist",
            Description = "Fog provides concealment (20% miss chance). 20-ft radius. 1 min/level. PHB p.258",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 4,
            AreaRadius = 4,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            BuffType = "concealment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Concealment/fog not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "deathwatch",
            Name = "Deathwatch",
            Description = "Reveals how near death subjects within 30 ft are. Duration 10 min/level. PHB p.217",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - HP reveal not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "summon_monster_1_clr",
            Name = "Summon Monster I",
            Description = "Calls a creature to fight for you. Duration 1 round/level. PHB p.285",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 3,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
        });
    }

    // ====================================================================
    //  CLERIC 2ND LEVEL SPELLS
    // ====================================================================
    private static void RegisterCleric2ndLevel()
    {
        // --- FUNCTIONAL: Healing ---

        Register(new SpellData
        {
            SpellId = "cure_moderate_wounds",
            Name = "Cure Moderate Wounds",
            Description = "Heals 2d8 + CL (max +10) HP. Touch range. PHB p.216",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Healing,
            HealDice = 8, HealCount = 2, BonusHealing = 3, // +CL
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "lesser_restoration",
            Name = "Lesser Restoration",
            Description = "Dispels magical ability penalty or repairs 1d4 ability damage. PHB p.272",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Healing,
            HealDice = 4, HealCount = 1, // 1d4 ability restored
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Ability damage restoration not fully implemented]"
        });

        // --- FUNCTIONAL: Damage ---

        Register(new SpellData
        {
            SpellId = "inflict_moderate_wounds",
            Name = "Inflict Moderate Wounds",
            Description = "Touch attack deals 2d8 + CL (max +10) negative energy damage. Will half. PHB p.244",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1,
            EffectType = SpellEffectType.Damage,
            DamageDice = 8, DamageCount = 2, BonusDamage = 3,
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "spiritual_weapon",
            Name = "Spiritual Weapon",
            Description = "Magic weapon attacks on its own. 1d8 + 1/3CL force damage. Lasts 1 round/level. No AoO. PHB p.283",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Damage,
            DamageDice = 8, DamageCount = 1, BonusDamage = 1,
            DamageType = "force",
            AutoHit = false, // Uses caster's BAB + WIS mod for attack
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = false // Does not provoke
        });

        Register(new SpellData
        {
            SpellId = "sound_burst",
            Name = "Sound Burst",
            Description = "Deals 1d8 sonic damage in 10-ft radius. Fortitude save or stunned for 1 round. PHB p.281",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified from area
            RangeSquares = 5,
            AreaRadius = 2,
            EffectType = SpellEffectType.Damage,
            DamageDice = 8, DamageCount = 1,
            DamageType = "sonic",
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            SaveHalves = false, // Stunned if failed, not half damage
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Buff Spells ---

        Register(new SpellData
        {
            SpellId = "aid",
            Name = "Aid",
            Description = "+1 morale bonus on attack and saves vs fear, plus 1d8 temporary HP. Duration 1 min/level. PHB p.196",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffSaveBonus = 1,
            BuffTempHP = 5, // ~average of 1d8
            BuffDurationRounds = 30,
            BuffType = "morale",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "bulls_strength_clr",
            Name = "Bull's Strength",
            Description = "Subject gains +4 enhancement bonus to STR for 1 min/level. PHB p.207",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "STR",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "bears_endurance_clr",
            Name = "Bear's Endurance",
            Description = "Subject gains +4 enhancement bonus to CON for 1 min/level. PHB p.203",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "CON",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "owls_wisdom_clr",
            Name = "Owl's Wisdom",
            Description = "Subject gains +4 enhancement bonus to WIS for 1 min/level. PHB p.259",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "WIS",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "eagles_splendor_clr",
            Name = "Eagle's Splendor",
            Description = "Subject gains +4 enhancement bonus to CHA for 1 min/level. PHB p.225",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "CHA",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "shield_other",
            Name = "Shield Other",
            Description = "+1 deflection AC and +1 resistance on saves. Caster takes half of subject's damage. 1 hr/level. PHB p.278",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDeflectionBonus = 1,
            BuffSaveBonus = 1,
            BuffDurationRounds = -1,
            BuffType = "shield_other",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "resist_energy_clr",
            Name = "Resist Energy",
            Description = "Grants resistance 10 to specified energy type. 10 min/level. PHB p.272",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "energy_resistance",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Energy resistance not implemented]"
        });

        // --- FUNCTIONAL: Debuff Spells ---

        Register(new SpellData
        {
            SpellId = "hold_person",
            Name = "Hold Person",
            Description = "Paralyzes one humanoid for 1 round/level. Will negates. PHB p.241",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3, // 1 round/level
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "silence",
            Name = "Silence",
            Description = "Negates sound in 20-ft radius. Prevents spellcasting with verbal components. 1 round/level. PHB p.279",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy, // Can target creature or area
            RangeSquares = 8,
            AreaRadius = 4,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will", // If targeted on a creature
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "death_knell",
            Name = "Death Knell",
            Description = "Kills dying creature, caster gains 1d8 temp HP, +2 STR, +1 CL. Touch range. Will negates. PHB p.217",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1,
            EffectType = SpellEffectType.Damage,
            DamageDice = 0, DamageCount = 0, BonusDamage = 10, // kills dying creature
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = false,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- PLACEHOLDER: Utility ---

        Register(new SpellData
        {
            SpellId = "augury",
            Name = "Augury",
            Description = "Learns whether an action will be good, bad, mixed, or nothing. 70% + 1%/CL success. PHB p.202",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Divination/prediction not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "zone_of_truth",
            Name = "Zone of Truth",
            Description = "Subjects in area can't lie. Will negates. 20-ft radius. 1 min/level. PHB p.303",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            AreaRadius = 4,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Truth compulsion not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "delay_poison",
            Name = "Delay Poison",
            Description = "Stops poison from harming subject for 1 hr/level. PHB p.217",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Poison mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "remove_paralysis",
            Name = "Remove Paralysis",
            Description = "Frees 1-4 creatures from paralysis or slow effect. PHB p.271",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Healing,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Status effect removal not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "summon_monster_2_clr",
            Name = "Summon Monster II",
            Description = "Calls creature to fight for you. Duration 1 round/level. PHB p.286",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 3,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "find_traps",
            Name = "Find Traps",
            Description = "+10 insight bonus on Search checks to find traps. Duration 1 min/level. PHB p.230",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Trap detection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "status",
            Name = "Status",
            Description = "Monitors condition and position of allies. Duration 1 hr/level. PHB p.284",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Ally monitoring not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "consecrate",
            Name = "Consecrate",
            Description = "Fills area with positive energy. Undead suffer penalties. 20-ft radius. 2 hr/level. PHB p.212",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            AreaRadius = 4,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Area consecration not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "gentle_repose",
            Name = "Gentle Repose",
            Description = "Preserves a corpse. Duration 1 day/level. PHB p.235",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Corpse preservation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "make_whole",
            Name = "Make Whole",
            Description = "Repairs an object of up to 10 cu.ft./level. PHB p.252",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Healing,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object repair not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "calm_emotions",
            Name = "Calm Emotions",
            Description = "Calms creatures in 20-ft radius, suppressing morale bonuses and emotion effects. Will negates. Concentration + 1 round/level. PHB p.207",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            AreaRadius = 4,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Emotion suppression not fully implemented]"
        });

        Register(new SpellData
        {
            SpellId = "enthrall",
            Name = "Enthrall",
            Description = "Captivates all within 100 ft + 10 ft/level. Will negates. Duration 1 hour or until distracted. PHB p.227",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Captivation not implemented]"
        });
    }

    // ====================================================================
    //  REGISTRATION & LOOKUP
    // ====================================================================

    private static void Register(SpellData spell)
    {
        _spells[spell.SpellId] = spell;
    }

    /// <summary>Get a spell by ID. Returns null if not found.</summary>
    public static SpellData GetSpell(string spellId)
    {
        Init();
        if (_spells.TryGetValue(spellId, out SpellData spell))
            return spell;
        Debug.LogWarning($"[SpellDatabase] Spell not found: {spellId}");
        return null;
    }

    /// <summary>Get all spells available to a specific class.</summary>
    public static List<SpellData> GetSpellsForClass(string className)
    {
        Init();
        var result = new List<SpellData>();
        foreach (var spell in _spells.Values)
        {
            foreach (string cls in spell.ClassList)
            {
                if (cls == className)
                {
                    result.Add(spell);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>Get all spells of a specific level for a class.</summary>
    public static List<SpellData> GetSpellsForClassAtLevel(string className, int spellLevel)
    {
        Init();
        var result = new List<SpellData>();
        foreach (var spell in _spells.Values)
        {
            if (spell.SpellLevel != spellLevel) continue;
            foreach (string cls in spell.ClassList)
            {
                if (cls == className)
                {
                    result.Add(spell);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>Get all registered spells.</summary>
    public static List<SpellData> GetAllSpells()
    {
        Init();
        return new List<SpellData>(_spells.Values);
    }

    /// <summary>Get count of all registered spells.</summary>
    public static int Count
    {
        get
        {
            Init();
            return _spells.Count;
        }
    }
}
