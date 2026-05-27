using System;
using UnityEngine;

/// <summary>
/// D&D 3.5e mount types based on PHB p.273 and Monster Manual.
/// </summary>
public enum MountType
{
    LightHorse,
    HeavyHorse,
    LightWarhorse,
    HeavyWarhorse,
    Pony,
    WarPony,
    Donkey,
    Mule,
    Camel,
    Griffon,
    Hippogriff,
    Pegasus,
    Wyvern
}

/// <summary>
/// Control state for a ridden mount (PHB p.157).
/// </summary>
public enum MountControlState
{
    /// <summary>DC 5 Ride check passed; mount takes only move actions, rider directs.</summary>
    Controlled,
    /// <summary>Mount acts independently using its own AI.</summary>
    Uncontrolled,
    /// <summary>Mount flees from threats at double speed.</summary>
    Panicked
}

/// <summary>
/// Represents a natural attack from a mount (hoof, bite, etc.).
/// </summary>
[Serializable]
public class MountNaturalAttack
{
    public string Name;           // e.g., "Hoof", "Bite"
    public int AttackBonus;       // Total attack bonus
    public int DamageDieCount;    // e.g., 1
    public int DamageDieSides;    // e.g., 6 for d6
    public int DamageBonus;       // Flat bonus to damage
    public bool IsPrimary;        // Primary (full STR) or secondary (-5, half STR)

    /// <summary>Roll damage for this attack.</summary>
    public int RollDamage()
    {
        int total = 0;
        for (int i = 0; i < DamageDieCount; i++)
            total += UnityEngine.Random.Range(1, DamageDieSides + 1);
        return Mathf.Max(1, total + DamageBonus);
    }

    public override string ToString()
    {
        return $"{Name} +{AttackBonus} ({DamageDieCount}d{DamageDieSides}+{DamageBonus})";
    }
}

/// <summary>
/// D&D 3.5e mount data structure. Stats from PHB p.273, Monster Manual.
/// Immutable template data — instances are created by MountDatabase.
/// </summary>
[Serializable]
public class MountData
{
    // ── Identity ──
    public string Name;
    public MountType Type;
    public SizeCategory Size;

    // ── Core Stats ──
    public int MovementSpeed;    // feet per round (base land speed)
    public int ArmorClass;       // natural AC
    public int HitPoints;        // average HP from hit dice
    public int HitDice;          // number of hit dice
    public int Strength;
    public int Dexterity;
    public int Constitution;
    public int Intelligence;
    public int Wisdom;
    public int Charisma;

    // ── Combat ──
    public bool IsWarTrained;    // Can fight in battle without Ride DC 10 check
    public MountNaturalAttack[] NaturalAttacks;

    // ── Carrying Capacity (PHB p.162, based on STR and size) ──
    public int LightLoad;        // lbs
    public int MediumLoad;       // lbs
    public int HeavyLoad;        // lbs

    // ── Special Movement ──
    public bool CanFly;
    public int FlySpeed;         // 0 if cannot fly

    // ── Derived ──
    public int STRMod => (Strength - 10) / 2;
    public int DEXMod => (Dexterity - 10) / 2;
    public int CONMod => (Constitution - 10) / 2;

    /// <summary>Create a deep copy of this mount data for a specific mount instance.</summary>
    public MountData Clone()
    {
        var clone = (MountData)this.MemberwiseClone();
        if (NaturalAttacks != null)
        {
            clone.NaturalAttacks = new MountNaturalAttack[NaturalAttacks.Length];
            for (int i = 0; i < NaturalAttacks.Length; i++)
            {
                clone.NaturalAttacks[i] = new MountNaturalAttack
                {
                    Name = NaturalAttacks[i].Name,
                    AttackBonus = NaturalAttacks[i].AttackBonus,
                    DamageDieCount = NaturalAttacks[i].DamageDieCount,
                    DamageDieSides = NaturalAttacks[i].DamageDieSides,
                    DamageBonus = NaturalAttacks[i].DamageBonus,
                    IsPrimary = NaturalAttacks[i].IsPrimary
                };
            }
        }
        return clone;
    }

    public override string ToString()
    {
        string attacks = NaturalAttacks != null ? string.Join(", ", System.Array.ConvertAll(NaturalAttacks, a => a.ToString())) : "none";
        return $"{Name} ({Size}): Speed {MovementSpeed}ft, AC {ArmorClass}, HP {HitPoints}, STR {Strength}, DEX {Dexterity}, CON {Constitution}" +
               $", War-trained: {IsWarTrained}, Attacks: [{attacks}], Load: {LightLoad}/{MediumLoad}/{HeavyLoad}";
    }
}
