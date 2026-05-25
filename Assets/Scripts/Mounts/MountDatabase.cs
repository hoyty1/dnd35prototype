using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static database of all mount definitions from D&D 3.5e PHB p.273 and Monster Manual.
/// All stats are PHB-accurate with natural attacks, carrying capacity, and training status.
/// </summary>
public static class MountDatabase
{
    private static Dictionary<MountType, MountData> _mounts;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _mounts = new Dictionary<MountType, MountData>();
        InitializeMounts();
        Debug.Log($"[MountDatabase] Initialized {_mounts.Count} mount types.");
    }

    private static void InitializeMounts()
    {
        // ── Light Horse (MM p.273) ──
        // HD 3d8+6 (avg 19), AC 13 (+1 Dex, +2 natural), Speed 60 ft.
        // Attacks: 2 hooves +2 melee (1d4+1)
        _mounts[MountType.LightHorse] = new MountData
        {
            Name = "Light Horse",
            Type = MountType.LightHorse,
            Size = SizeCategory.Large,
            MovementSpeed = 60,
            ArmorClass = 13,
            HitPoints = 19,
            HitDice = 3,
            Strength = 14,
            Dexterity = 13,
            Constitution = 15,
            Intelligence = 2,
            Wisdom = 12,
            Charisma = 6,
            IsWarTrained = false,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 2, DamageDieCount = 1, DamageDieSides = 4, DamageBonus = 1, IsPrimary = true },
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 2, DamageDieCount = 1, DamageDieSides = 4, DamageBonus = 1, IsPrimary = true }
            },
            LightLoad = 150,
            MediumLoad = 300,
            HeavyLoad = 450,
            CanFly = false,
            FlySpeed = 0
        };

        // ── Heavy Horse (MM p.273) ──
        // HD 3d8+6 (avg 19), AC 13 (+1 Dex, +2 natural), Speed 50 ft.
        // Attacks: 2 hooves +3 melee (1d6+2)
        _mounts[MountType.HeavyHorse] = new MountData
        {
            Name = "Heavy Horse",
            Type = MountType.HeavyHorse,
            Size = SizeCategory.Large,
            MovementSpeed = 50,
            ArmorClass = 13,
            HitPoints = 19,
            HitDice = 3,
            Strength = 16,
            Dexterity = 13,
            Constitution = 15,
            Intelligence = 2,
            Wisdom = 12,
            Charisma = 6,
            IsWarTrained = false,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 3, DamageDieCount = 1, DamageDieSides = 6, DamageBonus = 3, IsPrimary = true },
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 3, DamageDieCount = 1, DamageDieSides = 6, DamageBonus = 3, IsPrimary = true }
            },
            LightLoad = 200,
            MediumLoad = 400,
            HeavyLoad = 600,
            CanFly = false,
            FlySpeed = 0
        };

        // ── Light Warhorse (MM p.274) ──
        // HD 3d8+9 (avg 22), AC 14 (+1 Dex, +3 natural), Speed 60 ft.
        // Attacks: 2 hooves +4 melee (1d4+3), bite +(-1) melee (1d3+1)
        _mounts[MountType.LightWarhorse] = new MountData
        {
            Name = "Light Warhorse",
            Type = MountType.LightWarhorse,
            Size = SizeCategory.Large,
            MovementSpeed = 60,
            ArmorClass = 14,
            HitPoints = 22,
            HitDice = 3,
            Strength = 16,
            Dexterity = 13,
            Constitution = 17,
            Intelligence = 2,
            Wisdom = 13,
            Charisma = 6,
            IsWarTrained = true,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 4, DamageDieCount = 1, DamageDieSides = 4, DamageBonus = 3, IsPrimary = true },
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 4, DamageDieCount = 1, DamageDieSides = 4, DamageBonus = 3, IsPrimary = true },
                new MountNaturalAttack { Name = "Bite", AttackBonus = -1, DamageDieCount = 1, DamageDieSides = 3, DamageBonus = 1, IsPrimary = false }
            },
            LightLoad = 230,
            MediumLoad = 460,
            HeavyLoad = 690,
            CanFly = false,
            FlySpeed = 0
        };

        // ── Heavy Warhorse (MM p.274) ──
        // HD 4d8+12 (avg 30), AC 14 (+1 Dex, +3 natural), Speed 50 ft.
        // Attacks: 2 hooves +6 melee (1d6+4), bite +1 melee (1d4+2)
        _mounts[MountType.HeavyWarhorse] = new MountData
        {
            Name = "Heavy Warhorse",
            Type = MountType.HeavyWarhorse,
            Size = SizeCategory.Large,
            MovementSpeed = 50,
            ArmorClass = 14,
            HitPoints = 30,
            HitDice = 4,
            Strength = 18,
            Dexterity = 13,
            Constitution = 17,
            Intelligence = 2,
            Wisdom = 13,
            Charisma = 6,
            IsWarTrained = true,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 6, DamageDieCount = 1, DamageDieSides = 6, DamageBonus = 4, IsPrimary = true },
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 6, DamageDieCount = 1, DamageDieSides = 6, DamageBonus = 4, IsPrimary = true },
                new MountNaturalAttack { Name = "Bite", AttackBonus = 1, DamageDieCount = 1, DamageDieSides = 4, DamageBonus = 2, IsPrimary = false }
            },
            LightLoad = 300,
            MediumLoad = 600,
            HeavyLoad = 900,
            CanFly = false,
            FlySpeed = 0
        };

        // ── Pony (MM p.274) ──
        // HD 2d8+2 (avg 11), AC 13 (+1 Dex, +2 natural), Speed 40 ft.
        // Attacks: 2 hooves +1 melee (1d3)
        _mounts[MountType.Pony] = new MountData
        {
            Name = "Pony",
            Type = MountType.Pony,
            Size = SizeCategory.Medium,
            MovementSpeed = 40,
            ArmorClass = 13,
            HitPoints = 11,
            HitDice = 2,
            Strength = 13,
            Dexterity = 13,
            Constitution = 12,
            Intelligence = 2,
            Wisdom = 11,
            Charisma = 4,
            IsWarTrained = false,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 1, DamageDieCount = 1, DamageDieSides = 3, DamageBonus = 1, IsPrimary = true },
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 1, DamageDieCount = 1, DamageDieSides = 3, DamageBonus = 1, IsPrimary = true }
            },
            LightLoad = 75,
            MediumLoad = 150,
            HeavyLoad = 225,
            CanFly = false,
            FlySpeed = 0
        };

        // ── War Pony (MM p.274) ──
        // HD 2d8+4 (avg 13), AC 13 (+1 Dex, +2 natural), Speed 40 ft.
        // Attacks: 2 hooves +2 melee (1d3+1), bite -3 melee (1d3)
        _mounts[MountType.WarPony] = new MountData
        {
            Name = "War Pony",
            Type = MountType.WarPony,
            Size = SizeCategory.Medium,
            MovementSpeed = 40,
            ArmorClass = 13,
            HitPoints = 13,
            HitDice = 2,
            Strength = 13,
            Dexterity = 13,
            Constitution = 14,
            Intelligence = 2,
            Wisdom = 11,
            Charisma = 4,
            IsWarTrained = true,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 2, DamageDieCount = 1, DamageDieSides = 3, DamageBonus = 1, IsPrimary = true },
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 2, DamageDieCount = 1, DamageDieSides = 3, DamageBonus = 1, IsPrimary = true },
                new MountNaturalAttack { Name = "Bite", AttackBonus = -3, DamageDieCount = 1, DamageDieSides = 3, DamageBonus = 0, IsPrimary = false }
            },
            LightLoad = 75,
            MediumLoad = 150,
            HeavyLoad = 225,
            CanFly = false,
            FlySpeed = 0
        };

        // ── Donkey (MM p.272) ──
        // HD 2d8+2 (avg 11), AC 13 (+1 Dex, +2 natural), Speed 30 ft.
        // Attacks: bite +1 melee (1d2)
        _mounts[MountType.Donkey] = new MountData
        {
            Name = "Donkey",
            Type = MountType.Donkey,
            Size = SizeCategory.Medium,
            MovementSpeed = 30,
            ArmorClass = 13,
            HitPoints = 11,
            HitDice = 2,
            Strength = 10,
            Dexterity = 13,
            Constitution = 12,
            Intelligence = 1,
            Wisdom = 11,
            Charisma = 4,
            IsWarTrained = false,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Bite", AttackBonus = 1, DamageDieCount = 1, DamageDieSides = 2, DamageBonus = 0, IsPrimary = true }
            },
            LightLoad = 50,
            MediumLoad = 100,
            HeavyLoad = 150,
            CanFly = false,
            FlySpeed = 0
        };

        // ── Mule (MM p.276) ──
        // HD 3d8+9 (avg 22), AC 13 (+1 Dex, +2 natural), Speed 30 ft.
        // Attacks: 2 hooves +4 melee (1d4+3)
        _mounts[MountType.Mule] = new MountData
        {
            Name = "Mule",
            Type = MountType.Mule,
            Size = SizeCategory.Large,
            MovementSpeed = 30,
            ArmorClass = 13,
            HitPoints = 22,
            HitDice = 3,
            Strength = 16,
            Dexterity = 13,
            Constitution = 17,
            Intelligence = 2,
            Wisdom = 11,
            Charisma = 6,
            IsWarTrained = false,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 4, DamageDieCount = 1, DamageDieSides = 4, DamageBonus = 3, IsPrimary = true },
                new MountNaturalAttack { Name = "Hoof", AttackBonus = 4, DamageDieCount = 1, DamageDieSides = 4, DamageBonus = 3, IsPrimary = true }
            },
            LightLoad = 230,
            MediumLoad = 460,
            HeavyLoad = 690,
            CanFly = false,
            FlySpeed = 0
        };

        // ── Camel (MM p.270) ──
        // HD 3d8+6 (avg 19), AC 13 (-1 size, +1 Dex, +3 natural), Speed 50 ft.
        // Attacks: bite +4 melee (1d4+2)
        _mounts[MountType.Camel] = new MountData
        {
            Name = "Camel",
            Type = MountType.Camel,
            Size = SizeCategory.Large,
            MovementSpeed = 50,
            ArmorClass = 13,
            HitPoints = 19,
            HitDice = 3,
            Strength = 18,
            Dexterity = 16,
            Constitution = 14,
            Intelligence = 2,
            Wisdom = 11,
            Charisma = 4,
            IsWarTrained = false,
            NaturalAttacks = new[]
            {
                new MountNaturalAttack { Name = "Bite", AttackBonus = 4, DamageDieCount = 1, DamageDieSides = 4, DamageBonus = 2, IsPrimary = true }
            },
            LightLoad = 300,
            MediumLoad = 600,
            HeavyLoad = 900,
            CanFly = false,
            FlySpeed = 0
        };
    }

    /// <summary>Get mount data template by type. Returns null if not found.</summary>
    public static MountData GetMount(MountType type)
    {
        Init();
        if (_mounts.TryGetValue(type, out MountData data))
            return data;
        Debug.LogWarning($"[MountDatabase] Mount type {type} not found.");
        return null;
    }

    /// <summary>Get mount data by name (case-insensitive). Returns null if not found.</summary>
    public static MountData GetMountByName(string name)
    {
        Init();
        foreach (var kvp in _mounts)
        {
            if (string.Equals(kvp.Value.Name, name, System.StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        Debug.LogWarning($"[MountDatabase] Mount '{name}' not found.");
        return null;
    }

    /// <summary>Get all mount types in the database.</summary>
    public static IEnumerable<MountData> GetAllMounts()
    {
        Init();
        return _mounts.Values;
    }

    /// <summary>Get the number of registered mount types.</summary>
    public static int Count
    {
        get { Init(); return _mounts.Count; }
    }
}
