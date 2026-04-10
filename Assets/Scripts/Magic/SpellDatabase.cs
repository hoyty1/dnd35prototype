using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static database of all spells in the game.
/// Analogous to ItemDatabase. Initializes once and provides lookup by ID.
/// D&D 3.5e spell definitions for Wizard and Cleric at level 3.
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

        // ========== WIZARD CANTRIPS (Level 0) ==========

        Register(new SpellData
        {
            SpellId = "ray_of_frost",
            Name = "Ray of Frost",
            Description = "A ray of freezing air and ice deals 1d3 cold damage.",
            SpellLevel = 0,
            School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5, // Close range: 25 ft + 5 ft/2 levels = 30 ft at CL3 = 6 sq, using 5
            AreaRadius = 0,
            EffectType = SpellEffectType.Damage,
            DamageDice = 3,   // 1d3
            DamageCount = 1,
            BonusDamage = 0,
            DamageType = "cold",
            AutoHit = false,  // Ranged touch attack required
            AllowsSavingThrow = false,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "acid_splash",
            Name = "Acid Splash",
            Description = "An orb of acid deals 1d3 acid damage on a ranged touch attack.",
            SpellLevel = 0,
            School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5, // Close range
            AreaRadius = 0,
            EffectType = SpellEffectType.Damage,
            DamageDice = 3,   // 1d3
            DamageCount = 1,
            BonusDamage = 0,
            DamageType = "acid",
            AutoHit = false,  // Ranged touch attack
            AllowsSavingThrow = false,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // ========== CLERIC CANTRIPS (Level 0) ==========

        Register(new SpellData
        {
            SpellId = "inflict_minor_wounds",
            Name = "Inflict Minor Wounds",
            Description = "Touch attack deals 1 point of negative energy damage.",
            SpellLevel = 0,
            School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1, // Touch (adjacent)
            AreaRadius = 0,
            EffectType = SpellEffectType.Damage,
            DamageDice = 0,   // Fixed 1 damage, no dice
            DamageCount = 0,
            BonusDamage = 1,
            DamageType = "negative",
            AutoHit = false,  // Melee touch attack
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // ========== WIZARD LEVEL 1 SPELLS ==========

        Register(new SpellData
        {
            SpellId = "magic_missile",
            Name = "Magic Missile",
            Description = "1d4+1 force damage per missile, auto-hit. 2 missiles at CL3.",
            SpellLevel = 1,
            School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22, // Medium range: 100 ft + 10 ft/level = 130 ft at CL3 ≈ 26 sq, cap at 22
            AreaRadius = 0,
            EffectType = SpellEffectType.Damage,
            DamageDice = 4,    // 1d4+1 per missile
            DamageCount = 1,
            BonusDamage = 1,
            DamageType = "force",
            AutoHit = true,    // No attack roll, no save
            AllowsSavingThrow = false,
            MissileCount = 2,  // At CL3: 1 + (3-1)/2 = 2 missiles
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "mage_armor",
            Name = "Mage Armor",
            Description = "+4 armor bonus to AC for 1 hour/level. Doesn't stack with actual armor.",
            SpellLevel = 1,
            School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1, // Self
            AreaRadius = 0,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 4,
            BuffDurationRounds = -1, // 1 hour/level (effectively whole combat)
            BuffType = "armor",
            AllowsSavingThrow = false,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // ========== CLERIC LEVEL 1 SPELLS ==========

        Register(new SpellData
        {
            SpellId = "cure_light_wounds",
            Name = "Cure Light Wounds",
            Description = "Heals 1d8 + caster level (max +5) HP.",
            SpellLevel = 1,
            School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1, // Touch
            AreaRadius = 0,
            EffectType = SpellEffectType.Healing,
            HealDice = 8,     // 1d8
            HealCount = 1,
            BonusHealing = 3, // + caster level (3 at level 3, max +5)
            AllowsSavingThrow = false,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Debug.Log($"[SpellDatabase] Initialized with {_spells.Count} spells.");
    }

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
}
