// ============================================================================
// D&D 3.5e Wild Shape Form Database
// Contains 30+ animal forms, elemental forms, and plant forms for Druid.
// Stats from Monster Manual, used during Wild Shape transformation.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Creature size categories for Wild Shape restrictions.</summary>
public enum WildShapeSize
{
    Tiny,
    Small,
    Medium,
    Large,
    Huge
}

/// <summary>Creature type for Wild Shape form categories.</summary>
public enum WildShapeFormType
{
    Animal,
    Plant,
    Elemental
}

/// <summary>
/// A natural attack available in a Wild Shape form.
/// </summary>
[Serializable]
public class NaturalAttackData
{
    public string Name;         // e.g., "Bite", "Claw", "Slam"
    public int DamageDice;      // e.g., 6 for d6
    public int DamageCount;     // e.g., 1 for 1d6
    public int AttackCount;     // e.g., 2 for 2 claw attacks
    public bool IsPrimary;      // Primary = full STR bonus, Secondary = 1/2 STR

    public NaturalAttackData(string name, int count, int dice, int diceCount = 1, bool primary = true)
    {
        Name = name;
        AttackCount = count;
        DamageDice = dice;
        DamageCount = diceCount;
        IsPrimary = primary;
    }

    public override string ToString() => $"{(AttackCount > 1 ? AttackCount + "×" : "")}{Name} {DamageCount}d{DamageDice}";
}

/// <summary>
/// Defines a single Wild Shape form with all relevant stats.
/// </summary>
[Serializable]
public class WildShapeForm
{
    public string Name;
    public WildShapeSize Size;
    public WildShapeFormType FormType;

    // Physical stats (replace character's)
    public int STR;
    public int DEX;
    public int CON;

    // Defenses
    public int NaturalArmor;
    public int Speed;           // In 5-ft squares

    // Natural attacks
    public List<NaturalAttackData> Attacks = new List<NaturalAttackData>();

    // Special abilities
    public List<string> SpecialAbilities = new List<string>();

    /// <summary>Get a display summary of this form.</summary>
    public string GetSummary()
    {
        string atkStr = Attacks.Count > 0
            ? string.Join(", ", Attacks.ConvertAll(a => a.ToString()))
            : "none";
        string specials = SpecialAbilities.Count > 0
            ? string.Join(", ", SpecialAbilities)
            : "none";
        return $"{Name} ({Size} {FormType}): STR {STR}, DEX {DEX}, CON {CON}, " +
               $"NA +{NaturalArmor}, Spd {Speed * 5}ft, Atk: {atkStr}, Special: {specials}";
    }
}

/// <summary>
/// Static database of all Wild Shape forms available to Druids.
/// Organized by type (Animal, Plant, Elemental) and size.
/// Stats sourced from D&D 3.5e Monster Manual.
/// </summary>
public static class WildShapeFormDatabase
{
    private static List<WildShapeForm> _allForms;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _allForms = new List<WildShapeForm>();
        RegisterAnimalForms();
        RegisterPlantForms();
        RegisterElementalForms();

        Debug.Log($"[WildShapeFormDB] Registered {_allForms.Count} forms " +
                  $"({CountByType(WildShapeFormType.Animal)} animal, " +
                  $"{CountByType(WildShapeFormType.Plant)} plant, " +
                  $"{CountByType(WildShapeFormType.Elemental)} elemental)");
    }

    private static int CountByType(WildShapeFormType type)
    {
        int count = 0;
        for (int i = 0; i < _allForms.Count; i++)
            if (_allForms[i].FormType == type) count++;
        return count;
    }

    // ─────────────────────────────────────────────
    // Queries
    // ─────────────────────────────────────────────

    /// <summary>Get all registered forms.</summary>
    public static List<WildShapeForm> GetAllForms()
    {
        Init();
        return new List<WildShapeForm>(_allForms);
    }

    /// <summary>Get a form by name.</summary>
    public static WildShapeForm GetFormByName(string name)
    {
        Init();
        for (int i = 0; i < _allForms.Count; i++)
            if (string.Equals(_allForms[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return _allForms[i];
        return null;
    }

    /// <summary>Get all forms of a specific type.</summary>
    public static List<WildShapeForm> GetFormsByType(WildShapeFormType type)
    {
        Init();
        var result = new List<WildShapeForm>();
        for (int i = 0; i < _allForms.Count; i++)
            if (_allForms[i].FormType == type) result.Add(_allForms[i]);
        return result;
    }

    /// <summary>Get all forms available at a given druid level.</summary>
    public static List<WildShapeForm> GetAvailableForms(int druidLevel)
    {
        Init();
        var result = new List<WildShapeForm>();
        for (int i = 0; i < _allForms.Count; i++)
        {
            if (IsFormAvailable(_allForms[i], druidLevel))
                result.Add(_allForms[i]);
        }
        return result;
    }

    /// <summary>Check if a specific form is available at a given druid level.</summary>
    public static bool IsFormAvailable(WildShapeForm form, int druidLevel)
    {
        if (form == null || druidLevel < 5) return false;

        // Animal forms
        if (form.FormType == WildShapeFormType.Animal)
        {
            switch (form.Size)
            {
                case WildShapeSize.Small:
                case WildShapeSize.Medium:
                    return druidLevel >= 5;
                case WildShapeSize.Large:
                    return druidLevel >= 8;
                case WildShapeSize.Tiny:
                    return druidLevel >= 11;
                case WildShapeSize.Huge:
                    return druidLevel >= 15;
            }
        }

        // Plant forms (L12+)
        if (form.FormType == WildShapeFormType.Plant)
        {
            if (druidLevel < 12) return false;
            switch (form.Size)
            {
                case WildShapeSize.Small:
                case WildShapeSize.Medium:
                    return druidLevel >= 12;
                case WildShapeSize.Large:
                    return druidLevel >= 12;
                case WildShapeSize.Huge:
                    return druidLevel >= 15;
                default:
                    return druidLevel >= 12;
            }
        }

        // Elemental forms (L16+)
        if (form.FormType == WildShapeFormType.Elemental)
        {
            if (druidLevel < 16) return false;
            switch (form.Size)
            {
                case WildShapeSize.Small:
                case WildShapeSize.Medium:
                case WildShapeSize.Large:
                    return druidLevel >= 16;
                case WildShapeSize.Tiny:
                case WildShapeSize.Huge:
                    return druidLevel >= 18;
                default:
                    return druidLevel >= 16;
            }
        }

        return false;
    }

    // ─────────────────────────────────────────────
    // Animal Forms (MM stats)
    // ─────────────────────────────────────────────
    private static void RegisterAnimalForms()
    {
        // ── TINY ANIMALS ──
        _allForms.Add(new WildShapeForm
        {
            Name = "Bat", Size = WildShapeSize.Tiny, FormType = WildShapeFormType.Animal,
            STR = 1, DEX = 15, CON = 10, NaturalArmor = 0, Speed = 1, // 5ft ground, 40ft fly
            Attacks = { new NaturalAttackData("Bite", 1, 1) },
            SpecialAbilities = { "Blindsense 20ft", "Fly 40ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Hawk", Size = WildShapeSize.Tiny, FormType = WildShapeFormType.Animal,
            STR = 6, DEX = 17, CON = 10, NaturalArmor = 1, Speed = 2,
            Attacks = { new NaturalAttackData("Talons", 1, 4) },
            SpecialAbilities = { "Fly 60ft", "+8 Spot in daylight" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Owl", Size = WildShapeSize.Tiny, FormType = WildShapeFormType.Animal,
            STR = 4, DEX = 17, CON = 10, NaturalArmor = 1, Speed = 2,
            Attacks = { new NaturalAttackData("Talons", 1, 4) },
            SpecialAbilities = { "Fly 40ft", "+8 Listen", "+14 Move Silently" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Rat", Size = WildShapeSize.Tiny, FormType = WildShapeFormType.Animal,
            STR = 2, DEX = 15, CON = 10, NaturalArmor = 0, Speed = 3,
            Attacks = { new NaturalAttackData("Bite", 1, 3) },
            SpecialAbilities = { "Swim 15ft", "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Raven", Size = WildShapeSize.Tiny, FormType = WildShapeFormType.Animal,
            STR = 1, DEX = 15, CON = 10, NaturalArmor = 0, Speed = 2,
            Attacks = { new NaturalAttackData("Claws", 1, 2) },
            SpecialAbilities = { "Fly 40ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Snake (Tiny Viper)", Size = WildShapeSize.Tiny, FormType = WildShapeFormType.Animal,
            STR = 4, DEX = 17, CON = 11, NaturalArmor = 3, Speed = 3,
            Attacks = { new NaturalAttackData("Bite", 1, 2) },
            SpecialAbilities = { "Poison (Fort DC 10, 1d6 Con)", "Swim 15ft", "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Toad", Size = WildShapeSize.Tiny, FormType = WildShapeFormType.Animal,
            STR = 1, DEX = 12, CON = 11, NaturalArmor = 0, Speed = 1,
            Attacks = { },
            SpecialAbilities = { "+4 Hide" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Weasel", Size = WildShapeSize.Tiny, FormType = WildShapeFormType.Animal,
            STR = 3, DEX = 15, CON = 10, NaturalArmor = 0, Speed = 4,
            Attacks = { new NaturalAttackData("Bite", 1, 3) },
            SpecialAbilities = { "Attach", "Scent" }
        });

        // ── SMALL ANIMALS ──
        _allForms.Add(new WildShapeForm
        {
            Name = "Badger", Size = WildShapeSize.Small, FormType = WildShapeFormType.Animal,
            STR = 8, DEX = 17, CON = 15, NaturalArmor = 1, Speed = 6,
            Attacks = { new NaturalAttackData("Claw", 2, 2), new NaturalAttackData("Bite", 1, 3, 1, false) },
            SpecialAbilities = { "Rage", "Scent", "Burrow 10ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Dog", Size = WildShapeSize.Small, FormType = WildShapeFormType.Animal,
            STR = 13, DEX = 17, CON = 15, NaturalArmor = 1, Speed = 8,
            Attacks = { new NaturalAttackData("Bite", 1, 4) },
            SpecialAbilities = { "Scent", "Track" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Eagle", Size = WildShapeSize.Small, FormType = WildShapeFormType.Animal,
            STR = 10, DEX = 15, CON = 12, NaturalArmor = 1, Speed = 2,
            Attacks = { new NaturalAttackData("Talons", 2, 4), new NaturalAttackData("Bite", 1, 4, 1, false) },
            SpecialAbilities = { "Fly 80ft", "+8 Spot in daylight" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Monkey", Size = WildShapeSize.Small, FormType = WildShapeFormType.Animal,
            STR = 7, DEX = 15, CON = 10, NaturalArmor = 0, Speed = 6,
            Attacks = { new NaturalAttackData("Bite", 1, 3) },
            SpecialAbilities = { "Climb 30ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Wolf (Small)", Size = WildShapeSize.Small, FormType = WildShapeFormType.Animal,
            STR = 13, DEX = 15, CON = 15, NaturalArmor = 2, Speed = 10,
            Attacks = { new NaturalAttackData("Bite", 1, 6) },
            SpecialAbilities = { "Trip", "Scent" }
        });

        // ── MEDIUM ANIMALS ──
        _allForms.Add(new WildShapeForm
        {
            Name = "Ape", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 21, DEX = 15, CON = 14, NaturalArmor = 3, Speed = 6,
            Attacks = { new NaturalAttackData("Claw", 2, 6), new NaturalAttackData("Bite", 1, 6, 1, false) },
            SpecialAbilities = { "Climb 30ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Black Bear", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 19, DEX = 13, CON = 15, NaturalArmor = 2, Speed = 8,
            Attacks = { new NaturalAttackData("Claw", 2, 4), new NaturalAttackData("Bite", 1, 6, 1, false) },
            SpecialAbilities = { "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Boar", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 15, DEX = 10, CON = 17, NaturalArmor = 6, Speed = 8,
            Attacks = { new NaturalAttackData("Gore", 1, 8) },
            SpecialAbilities = { "Ferocity" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Cheetah", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 16, DEX = 19, CON = 15, NaturalArmor = 1, Speed = 10,
            Attacks = { new NaturalAttackData("Bite", 1, 6), new NaturalAttackData("Claw", 2, 2, 1, false) },
            SpecialAbilities = { "Sprint" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Crocodile", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 19, DEX = 12, CON = 17, NaturalArmor = 4, Speed = 4,
            Attacks = { new NaturalAttackData("Bite", 1, 8), new NaturalAttackData("Tail Slap", 1, 12, 1, false) },
            SpecialAbilities = { "Improved Grab", "Swim 30ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Leopard", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 16, DEX = 19, CON = 15, NaturalArmor = 1, Speed = 8,
            Attacks = { new NaturalAttackData("Bite", 1, 6), new NaturalAttackData("Claw", 2, 3) },
            SpecialAbilities = { "Improved Grab", "Pounce", "Rake", "Climb 20ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Wolverine", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 14, DEX = 15, CON = 19, NaturalArmor = 2, Speed = 6,
            Attacks = { new NaturalAttackData("Claw", 2, 4), new NaturalAttackData("Bite", 1, 6, 1, false) },
            SpecialAbilities = { "Rage", "Climb 10ft", "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Dire Badger", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 14, DEX = 17, CON = 19, NaturalArmor = 3, Speed = 6,
            Attacks = { new NaturalAttackData("Claw", 2, 4), new NaturalAttackData("Bite", 1, 6, 1, false) },
            SpecialAbilities = { "Rage", "Scent", "Burrow 10ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Snake (Constrictor)", Size = WildShapeSize.Medium, FormType = WildShapeFormType.Animal,
            STR = 17, DEX = 17, CON = 13, NaturalArmor = 2, Speed = 4,
            Attacks = { new NaturalAttackData("Bite", 1, 6) },
            SpecialAbilities = { "Constrict 1d6+4", "Improved Grab", "Swim 20ft", "Scent" }
        });

        // ── LARGE ANIMALS ──
        _allForms.Add(new WildShapeForm
        {
            Name = "Brown Bear", Size = WildShapeSize.Large, FormType = WildShapeFormType.Animal,
            STR = 27, DEX = 13, CON = 19, NaturalArmor = 5, Speed = 8,
            Attacks = { new NaturalAttackData("Claw", 2, 8), new NaturalAttackData("Bite", 1, 6, 2, false) },
            SpecialAbilities = { "Improved Grab", "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Dire Wolf", Size = WildShapeSize.Large, FormType = WildShapeFormType.Animal,
            STR = 25, DEX = 15, CON = 17, NaturalArmor = 3, Speed = 10,
            Attacks = { new NaturalAttackData("Bite", 1, 8) },
            SpecialAbilities = { "Trip", "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Lion", Size = WildShapeSize.Large, FormType = WildShapeFormType.Animal,
            STR = 21, DEX = 17, CON = 15, NaturalArmor = 3, Speed = 8,
            Attacks = { new NaturalAttackData("Claw", 2, 6), new NaturalAttackData("Bite", 1, 8, 1, false) },
            SpecialAbilities = { "Pounce", "Rake 1d4+2", "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Rhinoceros", Size = WildShapeSize.Large, FormType = WildShapeFormType.Animal,
            STR = 26, DEX = 10, CON = 21, NaturalArmor = 7, Speed = 6,
            Attacks = { new NaturalAttackData("Gore", 1, 8, 2) },
            SpecialAbilities = { "Powerful Charge 2d8+12" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Tiger", Size = WildShapeSize.Large, FormType = WildShapeFormType.Animal,
            STR = 23, DEX = 15, CON = 17, NaturalArmor = 3, Speed = 8,
            Attacks = { new NaturalAttackData("Claw", 2, 8), new NaturalAttackData("Bite", 1, 6, 2, false) },
            SpecialAbilities = { "Improved Grab", "Pounce", "Rake 1d8+3", "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Snake (Giant Constrictor)", Size = WildShapeSize.Large, FormType = WildShapeFormType.Animal,
            STR = 25, DEX = 17, CON = 13, NaturalArmor = 3, Speed = 4,
            Attacks = { new NaturalAttackData("Bite", 1, 8) },
            SpecialAbilities = { "Constrict 1d8+10", "Improved Grab", "Scent" }
        });

        // ── HUGE ANIMALS ──
        _allForms.Add(new WildShapeForm
        {
            Name = "Dire Bear", Size = WildShapeSize.Huge, FormType = WildShapeFormType.Animal,
            STR = 31, DEX = 13, CON = 19, NaturalArmor = 7, Speed = 8,
            Attacks = { new NaturalAttackData("Claw", 2, 6, 2), new NaturalAttackData("Bite", 1, 8, 2, false) },
            SpecialAbilities = { "Improved Grab", "Scent" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Elephant", Size = WildShapeSize.Huge, FormType = WildShapeFormType.Animal,
            STR = 30, DEX = 10, CON = 21, NaturalArmor = 7, Speed = 8,
            Attacks = { new NaturalAttackData("Gore", 1, 8, 2), new NaturalAttackData("Slam", 2, 6, 1, false), new NaturalAttackData("Stamp", 2, 6, 1, false) },
            SpecialAbilities = { "Trample 2d8+15", "Scent" }
        });
    }

    // ─────────────────────────────────────────────
    // Plant Forms (L12+)
    // ─────────────────────────────────────────────
    private static void RegisterPlantForms()
    {
        _allForms.Add(new WildShapeForm
        {
            Name = "Assassin Vine", Size = WildShapeSize.Large, FormType = WildShapeFormType.Plant,
            STR = 20, DEX = 10, CON = 16, NaturalArmor = 6, Speed = 1,
            Attacks = { new NaturalAttackData("Slam", 1, 6) },
            SpecialAbilities = { "Constrict 1d6+7", "Entangle", "Improved Grab", "Blindsight 30ft" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Shambling Mound", Size = WildShapeSize.Large, FormType = WildShapeFormType.Plant,
            STR = 21, DEX = 10, CON = 17, NaturalArmor = 11, Speed = 4,
            Attacks = { new NaturalAttackData("Slam", 2, 6) },
            SpecialAbilities = { "Constrict 2d6+7", "Improved Grab", "Immunity: Electricity", "Resistance: Fire 10" }
        });
        _allForms.Add(new WildShapeForm
        {
            Name = "Treant", Size = WildShapeSize.Huge, FormType = WildShapeFormType.Plant,
            STR = 29, DEX = 8, CON = 21, NaturalArmor = 13, Speed = 6,
            Attacks = { new NaturalAttackData("Slam", 2, 6, 2) },
            SpecialAbilities = { "Trample 2d6+13", "Double damage vs objects", "Vulnerability: Fire" }
        });
    }

    // ─────────────────────────────────────────────
    // Elemental Forms (L16+)
    // ─────────────────────────────────────────────
    private static void RegisterElementalForms()
    {
        // AIR ELEMENTALS
        _allForms.Add(MakeElemental("Air Elemental (Small)", WildShapeSize.Small, 10, 21, 10, 2, 20,
            new NaturalAttackData("Slam", 1, 4), "Fly 100ft", "Whirlwind"));
        _allForms.Add(MakeElemental("Air Elemental (Medium)", WildShapeSize.Medium, 12, 25, 12, 6, 20,
            new NaturalAttackData("Slam", 1, 6), "Fly 100ft", "Whirlwind"));
        _allForms.Add(MakeElemental("Air Elemental (Large)", WildShapeSize.Large, 14, 29, 14, 8, 20,
            new NaturalAttackData("Slam", 2, 8), "Fly 100ft", "Whirlwind"));
        _allForms.Add(MakeElemental("Air Elemental (Huge)", WildShapeSize.Huge, 18, 33, 16, 10, 20,
            new NaturalAttackData("Slam", 2, 10), "Fly 100ft", "Whirlwind"));

        // EARTH ELEMENTALS
        _allForms.Add(MakeElemental("Earth Elemental (Small)", WildShapeSize.Small, 17, 8, 13, 4, 4,
            new NaturalAttackData("Slam", 1, 6), "Earth Glide", "Push"));
        _allForms.Add(MakeElemental("Earth Elemental (Medium)", WildShapeSize.Medium, 21, 8, 15, 7, 4,
            new NaturalAttackData("Slam", 1, 8), "Earth Glide", "Push"));
        _allForms.Add(MakeElemental("Earth Elemental (Large)", WildShapeSize.Large, 25, 8, 17, 9, 4,
            new NaturalAttackData("Slam", 2, 8), "Earth Glide", "Push"));
        _allForms.Add(MakeElemental("Earth Elemental (Huge)", WildShapeSize.Huge, 29, 8, 19, 11, 4,
            new NaturalAttackData("Slam", 2, 10), "Earth Glide", "Push"));

        // FIRE ELEMENTALS
        _allForms.Add(MakeElemental("Fire Elemental (Small)", WildShapeSize.Small, 10, 13, 10, 0, 10,
            new NaturalAttackData("Slam", 1, 4), "Burn 1d4", "Immunity: Fire", "Vulnerability: Cold"));
        _allForms.Add(MakeElemental("Fire Elemental (Medium)", WildShapeSize.Medium, 12, 17, 12, 2, 10,
            new NaturalAttackData("Slam", 1, 6), "Burn 1d6", "Immunity: Fire", "Vulnerability: Cold"));
        _allForms.Add(MakeElemental("Fire Elemental (Large)", WildShapeSize.Large, 14, 21, 14, 4, 10,
            new NaturalAttackData("Slam", 2, 6), "Burn 1d8", "Immunity: Fire", "Vulnerability: Cold"));
        _allForms.Add(MakeElemental("Fire Elemental (Huge)", WildShapeSize.Huge, 18, 25, 16, 6, 10,
            new NaturalAttackData("Slam", 2, 8), "Burn 2d6", "Immunity: Fire", "Vulnerability: Cold"));

        // WATER ELEMENTALS
        _allForms.Add(MakeElemental("Water Elemental (Small)", WildShapeSize.Small, 14, 10, 13, 4, 4,
            new NaturalAttackData("Slam", 1, 6), "Swim 90ft", "Vortex", "Drench"));
        _allForms.Add(MakeElemental("Water Elemental (Medium)", WildShapeSize.Medium, 18, 10, 15, 7, 4,
            new NaturalAttackData("Slam", 1, 8), "Swim 90ft", "Vortex", "Drench"));
        _allForms.Add(MakeElemental("Water Elemental (Large)", WildShapeSize.Large, 22, 10, 17, 9, 4,
            new NaturalAttackData("Slam", 2, 8), "Swim 90ft", "Vortex", "Drench"));
        _allForms.Add(MakeElemental("Water Elemental (Huge)", WildShapeSize.Huge, 26, 10, 19, 11, 4,
            new NaturalAttackData("Slam", 2, 10), "Swim 90ft", "Vortex", "Drench"));
    }

    private static WildShapeForm MakeElemental(string name, WildShapeSize size,
        int str, int dex, int con, int naturalArmor, int speed,
        NaturalAttackData attack, params string[] specials)
    {
        var form = new WildShapeForm
        {
            Name = name,
            Size = size,
            FormType = WildShapeFormType.Elemental,
            STR = str, DEX = dex, CON = con,
            NaturalArmor = naturalArmor,
            Speed = speed,
            Attacks = { attack },
            SpecialAbilities = new List<string>(specials)
        };
        return form;
    }
}
