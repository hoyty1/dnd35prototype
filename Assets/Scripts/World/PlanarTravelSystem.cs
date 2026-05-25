using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

// ══════════════════════════════════════════════════════════════════════
//  D&D 3.5e PLANAR TRAVEL SYSTEM — DMG Chapter 5 (pp. 147–168)
//  Supports Plane Shift, Gate creation, environmental hazards,
//  and all wondrous items that interact with the planes.
// ══════════════════════════════════════════════════════════════════════

/// <summary>All D&D 3.5e planes of existence.</summary>
public enum Plane
{
    // Material
    Material,

    // Transitive Planes
    Astral,
    Ethereal,
    Shadow,

    // Inner Planes — Elemental
    Elemental_Air,
    Elemental_Earth,
    Elemental_Fire,
    Elemental_Water,

    // Inner Planes — Energy
    Positive_Energy,
    Negative_Energy,

    // Outer Planes — Lawful Good axis
    Mount_Celestia,
    Bytopia,
    Arcadia,

    // Outer Planes — Neutral Good
    Elysium,
    Beastlands,

    // Outer Planes — Chaotic Good
    Arborea,

    // Outer Planes — Lawful Neutral
    Mechanus,
    Acheron,

    // Outer Planes — True Neutral
    Outlands,

    // Outer Planes — Chaotic Neutral
    Limbo,

    // Outer Planes — Lawful Evil
    Nine_Hells,

    // Outer Planes — Neutral Evil
    Gehenna,
    Gray_Waste,

    // Outer Planes — Chaotic Evil
    Abyss,
    Carceri,
    Pandemonium
}

/// <summary>Gravity type for a plane.</summary>
public enum PlanarGravityType
{
    Normal,
    Heavy,
    Light,
    None,
    ObjectiveDirectional,
    SubjectiveDirectional
}

/// <summary>Time flow for a plane.</summary>
public enum PlanarTimeFlow
{
    Normal,
    Erratic,
    Flowing,
    Timeless
}

/// <summary>Magic trait for a plane.</summary>
public enum PlanarMagicType
{
    Normal,
    Enhanced,
    Impeded,
    Limited,
    Wild,
    Dead
}

/// <summary>Alignment trait intensity for a plane.</summary>
public enum PlanarAlignmentTrait
{
    None,
    MildlyAligned,
    StronglyAligned
}

/// <summary>Environmental hazard type.</summary>
public enum PlanarHazardType
{
    None,
    ExtremeHeat,
    ExtremeCold,
    NegativeEnergy,
    PositiveEnergy,
    Vacuum,
    Acid,
    Drowning,
    Crushing
}

/// <summary>
/// Data class describing a single environmental hazard on a plane.
/// </summary>
[Serializable]
public class PlanarHazard
{
    public PlanarHazardType Type;
    public string DamageExpression;  // e.g., "3d10", "1d6"
    public string DamageType;        // e.g., "fire", "negative"
    public int FortSaveDC;           // 0 = no save
    public int FrequencyRounds;      // Damage every N rounds (1 = every round)
    public string Description;
}

/// <summary>
/// Complete data for a single plane of existence.
/// DMG Chapter 5, pp. 147–168.
/// </summary>
[Serializable]
public class PlaneData
{
    public Plane PlaneType;
    public string Name;
    public string Description;

    // Physical traits
    public PlanarGravityType Gravity = PlanarGravityType.Normal;
    public PlanarTimeFlow TimeFlow = PlanarTimeFlow.Normal;

    // Magic traits
    public PlanarMagicType Magic = PlanarMagicType.Normal;
    public string[] EnhancedSchools;   // e.g., {"Evocation"} for fire plane
    public string[] ImpededSchools;    // e.g., {"Evocation(water)"} for fire plane

    // Alignment traits
    public PlanarAlignmentTrait AlignmentTrait = PlanarAlignmentTrait.None;
    public string AlignmentType;       // "Good", "Evil", "Lawful", "Chaotic", "LawfulGood", etc.

    // Environmental hazards
    public List<PlanarHazard> Hazards = new List<PlanarHazard>();

    // Informational
    public bool IsTransitive;
    public bool IsInnerPlane;
    public bool IsOuterPlane;
}

/// <summary>
/// Result of a plane shift attempt.
/// </summary>
public class PlaneShiftResult
{
    public bool Success;
    public Plane OriginalPlane;
    public Plane DestinationPlane;
    public bool Mishap;
    public string MishapDescription;
    public int TravelersCount;
    public string LogMessage;
}

/// <summary>
/// Result of a gate/portal creation.
/// </summary>
public class GateResult
{
    public bool Success;
    public Plane DestinationPlane;
    public float DurationMinutes;
    public bool IsTwoWay;
    public string LogMessage;
}

/// <summary>
/// Core planar travel system. Manages plane database, travel, gates, and environmental effects.
/// Singleton pattern via static Instance; initialized lazily.
/// </summary>
public class PlanarTravelSystem : MonoBehaviour
{
    public static PlanarTravelSystem Instance { get; private set; }

    private static Dictionary<Plane, PlaneData> _planeDatabase;
    private static bool _databaseInitialized = false;

    /// <summary>Current plane the party is on. Defaults to Material.</summary>
    public Plane CurrentPlane = Plane.Material;

    /// <summary>Active gates/portals (plane → expiry time).</summary>
    private List<ActiveGate> _activeGates = new List<ActiveGate>();

    private class ActiveGate
    {
        public Plane Destination;
        public float ExpiryTime; // Time.time when gate closes
        public bool IsTwoWay;
    }

    // ════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializePlaneDatabase();
    }

    /// <summary>Ensure the static plane database is built. Idempotent.</summary>
    public static void InitializePlaneDatabase()
    {
        if (_databaseInitialized) return;
        _databaseInitialized = true;
        _planeDatabase = new Dictionary<Plane, PlaneData>();

        // ── Material Plane ──
        Register(new PlaneData
        {
            PlaneType = Plane.Material,
            Name = "Material Plane",
            Description = "The normal world of mortals, where most campaigns take place.",
            Gravity = PlanarGravityType.Normal,
            TimeFlow = PlanarTimeFlow.Normal,
            Magic = PlanarMagicType.Normal,
        });

        // ── Transitive Planes ──
        Register(new PlaneData
        {
            PlaneType = Plane.Astral,
            Name = "Astral Plane",
            Description = "A silvery void connecting the Material Plane to the Outer Planes. Timeless and weightless.",
            Gravity = PlanarGravityType.SubjectiveDirectional,
            TimeFlow = PlanarTimeFlow.Timeless,
            Magic = PlanarMagicType.Enhanced,
            EnhancedSchools = new[] { "All" },
            IsTransitive = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Ethereal,
            Name = "Ethereal Plane",
            Description = "A misty realm overlapping the Material Plane. Connects to Inner Planes.",
            Gravity = PlanarGravityType.None,
            TimeFlow = PlanarTimeFlow.Normal,
            Magic = PlanarMagicType.Normal,
            IsTransitive = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Shadow,
            Name = "Plane of Shadow",
            Description = "A dark reflection of the Material Plane. Shadows twist and writhe.",
            Gravity = PlanarGravityType.Normal,
            TimeFlow = PlanarTimeFlow.Normal,
            Magic = PlanarMagicType.Enhanced,
            EnhancedSchools = new[] { "Illusion(shadow)" },
            IsTransitive = true,
        });

        // ── Inner Planes — Elemental ──
        Register(new PlaneData
        {
            PlaneType = Plane.Elemental_Air,
            Name = "Elemental Plane of Air",
            Description = "An endless expanse of sky and clouds. Empty and breathable.",
            Gravity = PlanarGravityType.SubjectiveDirectional,
            Magic = PlanarMagicType.Enhanced,
            EnhancedSchools = new[] { "Evocation(air)" },
            ImpededSchools = new[] { "Evocation(earth)" },
            IsInnerPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Elemental_Earth,
            Name = "Elemental Plane of Earth",
            Description = "Solid rock and stone in every direction. Rare pockets of air exist.",
            Gravity = PlanarGravityType.Normal,
            Magic = PlanarMagicType.Enhanced,
            EnhancedSchools = new[] { "Evocation(earth)" },
            ImpededSchools = new[] { "Evocation(air)" },
            IsInnerPlane = true,
            Hazards = new List<PlanarHazard>
            {
                new PlanarHazard
                {
                    Type = PlanarHazardType.Crushing,
                    Description = "Solid rock everywhere; suffocation without burrowing/ethereal travel.",
                    FrequencyRounds = 1
                }
            }
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Elemental_Fire,
            Name = "Elemental Plane of Fire",
            Description = "An endless inferno of flame and magma. Lethal without fire protection.",
            Gravity = PlanarGravityType.Normal,
            Magic = PlanarMagicType.Enhanced,
            EnhancedSchools = new[] { "Evocation(fire)" },
            ImpededSchools = new[] { "Evocation(water)", "Evocation(cold)" },
            IsInnerPlane = true,
            Hazards = new List<PlanarHazard>
            {
                new PlanarHazard
                {
                    Type = PlanarHazardType.ExtremeHeat,
                    DamageExpression = "3d10",
                    DamageType = "fire",
                    FortSaveDC = 15,
                    FrequencyRounds = 1,
                    Description = "Overwhelming heat deals 3d10 fire damage each round (Fort DC 15 half)."
                }
            }
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Elemental_Water,
            Name = "Elemental Plane of Water",
            Description = "An infinite ocean without surface or bottom. Drowning without water breathing.",
            Gravity = PlanarGravityType.SubjectiveDirectional,
            Magic = PlanarMagicType.Enhanced,
            EnhancedSchools = new[] { "Evocation(water)" },
            ImpededSchools = new[] { "Evocation(fire)" },
            IsInnerPlane = true,
            Hazards = new List<PlanarHazard>
            {
                new PlanarHazard
                {
                    Type = PlanarHazardType.Drowning,
                    Description = "Infinite water; drowning without water breathing.",
                    FrequencyRounds = 1
                }
            }
        });

        // ── Inner Planes — Energy ──
        Register(new PlaneData
        {
            PlaneType = Plane.Positive_Energy,
            Name = "Positive Energy Plane",
            Description = "Blinding white light and overwhelming life energy. Creatures gain temp HP, then explode.",
            Gravity = PlanarGravityType.SubjectiveDirectional,
            Magic = PlanarMagicType.Enhanced,
            EnhancedSchools = new[] { "Conjuration(healing)" },
            IsInnerPlane = true,
            Hazards = new List<PlanarHazard>
            {
                new PlanarHazard
                {
                    Type = PlanarHazardType.PositiveEnergy,
                    Description = "Gain 2d6 temp HP/round. At 150% max HP, must make DC 20 Fort or explode.",
                    FrequencyRounds = 1
                }
            }
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Negative_Energy,
            Name = "Negative Energy Plane",
            Description = "Absolute darkness. Life energy is drained away each moment.",
            Gravity = PlanarGravityType.SubjectiveDirectional,
            Magic = PlanarMagicType.Enhanced,
            EnhancedSchools = new[] { "Necromancy" },
            IsInnerPlane = true,
            Hazards = new List<PlanarHazard>
            {
                new PlanarHazard
                {
                    Type = PlanarHazardType.NegativeEnergy,
                    DamageExpression = "1d6",
                    DamageType = "negative",
                    FortSaveDC = 25,
                    FrequencyRounds = 1,
                    Description = "Major negative-dominant: 1 negative level/round (Fort DC 25 negates)."
                }
            }
        });

        // ── Outer Planes — Good ──
        Register(new PlaneData
        {
            PlaneType = Plane.Mount_Celestia,
            Name = "Mount Celestia (Seven Heavens)",
            Description = "The lawful good plane of perfect order and goodness. Seven ascending mountain layers.",
            AlignmentTrait = PlanarAlignmentTrait.StronglyAligned,
            AlignmentType = "LawfulGood",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Bytopia,
            Name = "Bytopia (Twin Paradises)",
            Description = "Two layers of idyllic land facing each other. Industrious good.",
            AlignmentTrait = PlanarAlignmentTrait.MildlyAligned,
            AlignmentType = "NeutralGood",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Elysium,
            Name = "Elysium (Blessed Fields)",
            Description = "The plane of pure good. Rest, contentment, and joy.",
            AlignmentTrait = PlanarAlignmentTrait.StronglyAligned,
            AlignmentType = "NeutralGood",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Beastlands,
            Name = "Beastlands (Happy Hunting Grounds)",
            Description = "An untamed wilderness of primal nature. Sentient animals and eternal hunts.",
            AlignmentTrait = PlanarAlignmentTrait.MildlyAligned,
            AlignmentType = "NeutralGood",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Arborea,
            Name = "Arborea (Olympian Glades)",
            Description = "Wild, passionate, and free. Home of the Greek pantheon. Extremes of emotion.",
            AlignmentTrait = PlanarAlignmentTrait.StronglyAligned,
            AlignmentType = "ChaoticGood",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Arcadia,
            Name = "Arcadia (Peaceable Kingdoms)",
            Description = "Ordered and good. Perfect orchards, geometric fields, harmonious communities.",
            AlignmentTrait = PlanarAlignmentTrait.MildlyAligned,
            AlignmentType = "LawfulGood",
            IsOuterPlane = true,
        });

        // ── Outer Planes — Neutral ──
        Register(new PlaneData
        {
            PlaneType = Plane.Mechanus,
            Name = "Mechanus (Clockwork Nirvana)",
            Description = "Massive interlocking gears and absolute law. Logic and order supreme.",
            AlignmentTrait = PlanarAlignmentTrait.StronglyAligned,
            AlignmentType = "LawfulNeutral",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Acheron,
            Name = "Acheron (Infernal Battlefield)",
            Description = "Iron cubes floating in void. Endless war between armies that never win.",
            AlignmentTrait = PlanarAlignmentTrait.MildlyAligned,
            AlignmentType = "LawfulNeutral",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Outlands,
            Name = "Outlands (Concordant Opposition)",
            Description = "The true neutral crossroads of the Outer Planes. The Spire rises infinitely at the center.",
            AlignmentTrait = PlanarAlignmentTrait.MildlyAligned,
            AlignmentType = "TrueNeutral",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Limbo,
            Name = "Limbo (Ever-Changing Chaos)",
            Description = "Raw chaos incarnate. Reality shifts constantly — fire, stone, water, air randomly morph.",
            Gravity = PlanarGravityType.SubjectiveDirectional,
            Magic = PlanarMagicType.Wild,
            AlignmentTrait = PlanarAlignmentTrait.StronglyAligned,
            AlignmentType = "ChaoticNeutral",
            IsOuterPlane = true,
        });

        // ── Outer Planes — Evil ──
        Register(new PlaneData
        {
            PlaneType = Plane.Nine_Hells,
            Name = "Nine Hells of Baator",
            Description = "The ordered realm of devils. Nine layers of torment, each ruled by an archdevil.",
            AlignmentTrait = PlanarAlignmentTrait.StronglyAligned,
            AlignmentType = "LawfulEvil",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Gehenna,
            Name = "Gehenna (Bleak Eternity)",
            Description = "Volcanic slopes of cruelty. Four layers of fire and treachery.",
            AlignmentTrait = PlanarAlignmentTrait.MildlyAligned,
            AlignmentType = "NeutralEvil",
            IsOuterPlane = true,
            Hazards = new List<PlanarHazard>
            {
                new PlanarHazard
                {
                    Type = PlanarHazardType.ExtremeHeat,
                    DamageExpression = "1d6",
                    DamageType = "fire",
                    FortSaveDC = 12,
                    FrequencyRounds = 10,
                    Description = "Volcanic heat: 1d6 fire damage every 10 rounds (Fort DC 12 negates)."
                }
            }
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Gray_Waste,
            Name = "Gray Waste (Hades)",
            Description = "Gray, bleak, hopeless. Color and emotion drain away. Triple evil alignment.",
            AlignmentTrait = PlanarAlignmentTrait.StronglyAligned,
            AlignmentType = "NeutralEvil",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Abyss,
            Name = "The Abyss (Infinite Layers)",
            Description = "Infinite layers of chaos and evil. Home of demons. Each layer uniquely horrible.",
            AlignmentTrait = PlanarAlignmentTrait.StronglyAligned,
            AlignmentType = "ChaoticEvil",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Carceri,
            Name = "Carceri (Tarterian Depths)",
            Description = "Prison plane. Six layers of nested orbs. Once you enter, you cannot leave without magic.",
            AlignmentTrait = PlanarAlignmentTrait.MildlyAligned,
            AlignmentType = "ChaoticEvil",
            IsOuterPlane = true,
        });

        Register(new PlaneData
        {
            PlaneType = Plane.Pandemonium,
            Name = "Pandemonium (Windswept Depths)",
            Description = "Tunnels of howling wind and madness. The noise drives mortals insane.",
            AlignmentTrait = PlanarAlignmentTrait.MildlyAligned,
            AlignmentType = "ChaoticEvil",
            IsOuterPlane = true,
        });

        Debug.Log($"[PlanarTravelSystem] Plane database initialized: {_planeDatabase.Count} planes registered.");
    }

    private static void Register(PlaneData data)
    {
        _planeDatabase[data.PlaneType] = data;
    }

    // ════════════════════════════════════════════════════════════
    //  PLANE DATABASE QUERIES
    // ════════════════════════════════════════════════════════════

    /// <summary>Get data for a specific plane.</summary>
    public static PlaneData GetPlaneData(Plane plane)
    {
        if (!_databaseInitialized) InitializePlaneDatabase();
        return _planeDatabase.ContainsKey(plane) ? _planeDatabase[plane] : null;
    }

    /// <summary>Get all registered planes.</summary>
    public static IReadOnlyDictionary<Plane, PlaneData> GetAllPlanes()
    {
        if (!_databaseInitialized) InitializePlaneDatabase();
        return _planeDatabase;
    }

    /// <summary>Get the display name for a plane.</summary>
    public static string GetPlaneName(Plane plane)
    {
        var data = GetPlaneData(plane);
        return data != null ? data.Name : plane.ToString().Replace('_', ' ');
    }

    /// <summary>Get the count of registered planes.</summary>
    public static int PlaneCount
    {
        get
        {
            if (!_databaseInitialized) InitializePlaneDatabase();
            return _planeDatabase.Count;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  PLANE SHIFT — Core travel method
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Perform a Plane Shift to a destination plane.
    /// Used by Amulet of the Planes, spells, etc.
    /// </summary>
    /// <param name="travelerName">Name of the primary traveler.</param>
    /// <param name="destination">Target plane.</param>
    /// <param name="companionCount">Additional willing creatures (0–7 for Amulet).</param>
    /// <param name="mishapChancePercent">Chance of arriving at wrong plane (5 for Amulet).</param>
    /// <returns>Result describing the shift outcome.</returns>
    public PlaneShiftResult PlaneShift(string travelerName, Plane destination, int companionCount = 0, int mishapChancePercent = 0)
    {
        var result = new PlaneShiftResult
        {
            OriginalPlane = CurrentPlane,
            DestinationPlane = destination,
            TravelersCount = 1 + companionCount,
            Success = true,
            Mishap = false,
        };

        // Check for mishap
        if (mishapChancePercent > 0 && Random.Range(0, 100) < mishapChancePercent)
        {
            Plane randomPlane = GetRandomPlane();
            result.Mishap = true;
            result.DestinationPlane = randomPlane;
            result.MishapDescription = $"Plane Shift mishap! Intended {GetPlaneName(destination)}, arrived at {GetPlaneName(randomPlane)} instead!";
            Debug.Log($"[PlanarTravel] MISHAP: {travelerName} shifted to {GetPlaneName(randomPlane)} instead of {GetPlaneName(destination)}");
        }

        Plane oldPlane = CurrentPlane;
        CurrentPlane = result.DestinationPlane;

        string travelers = companionCount > 0 ? $" and {companionCount} companions" : "";
        result.LogMessage = $"{travelerName}{travelers} plane shifted from {GetPlaneName(oldPlane)} to {GetPlaneName(result.DestinationPlane)}.";
        if (result.Mishap)
            result.LogMessage += $" (MISHAP: intended {GetPlaneName(destination)})";

        Debug.Log($"[PlanarTravel] {result.LogMessage}");
        return result;
    }

    // ════════════════════════════════════════════════════════════
    //  GATE — Two-way portal creation
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Create a gate (two-way portal) to a destination plane.
    /// Used by Cubic Gate, Gate spell, Well of Many Worlds.
    /// </summary>
    /// <param name="destination">Target plane.</param>
    /// <param name="durationMinutes">How long the gate stays open. 0 = instantaneous.</param>
    /// <param name="isTwoWay">Whether creatures can pass in both directions.</param>
    /// <returns>Result describing the gate creation.</returns>
    public GateResult CreateGate(Plane destination, float durationMinutes, bool isTwoWay = true)
    {
        var result = new GateResult
        {
            Success = true,
            DestinationPlane = destination,
            DurationMinutes = durationMinutes,
            IsTwoWay = isTwoWay,
        };

        if (durationMinutes > 0)
        {
            _activeGates.Add(new ActiveGate
            {
                Destination = destination,
                ExpiryTime = Time.time + (durationMinutes * 60f),
                IsTwoWay = isTwoWay,
            });
        }

        string direction = isTwoWay ? "two-way" : "one-way";
        string duration = durationMinutes > 0 ? $" for {durationMinutes} minutes" : " (instantaneous)";
        result.LogMessage = $"Gate ({direction}) opened to {GetPlaneName(destination)}{duration}.";

        Debug.Log($"[PlanarTravel] {result.LogMessage}");
        return result;
    }

    /// <summary>Close all expired gates. Call from Update if needed.</summary>
    public void CleanupExpiredGates()
    {
        float now = Time.time;
        for (int i = _activeGates.Count - 1; i >= 0; i--)
        {
            if (_activeGates[i].ExpiryTime <= now)
            {
                Debug.Log($"[PlanarTravel] Gate to {GetPlaneName(_activeGates[i].Destination)} has closed.");
                _activeGates.RemoveAt(i);
            }
        }
    }

    /// <summary>Get count of currently active gates.</summary>
    public int ActiveGateCount => _activeGates.Count;

    // ════════════════════════════════════════════════════════════
    //  RANDOM PLANE — For Well of Many Worlds
    // ════════════════════════════════════════════════════════════

    /// <summary>Select a random plane (excluding Material, for Well of Many Worlds).</summary>
    public Plane GetRandomPlane()
    {
        var allPlanes = Enum.GetValues(typeof(Plane)).Cast<Plane>().Where(p => p != Plane.Material).ToArray();
        return allPlanes[Random.Range(0, allPlanes.Length)];
    }

    /// <summary>Select a truly random plane including Material.</summary>
    public Plane GetTrulyRandomPlane()
    {
        var allPlanes = Enum.GetValues(typeof(Plane)).Cast<Plane>().ToArray();
        return allPlanes[Random.Range(0, allPlanes.Length)];
    }

    // ════════════════════════════════════════════════════════════
    //  ENVIRONMENTAL EFFECTS
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply environmental effects of the current plane to a character.
    /// Called each round while on a hazardous plane.
    /// Returns a description of effects applied.
    /// </summary>
    public string ApplyPlanarEffects(CharacterStats stats, Plane plane)
    {
        var data = GetPlaneData(plane);
        if (data == null || data.Hazards == null || data.Hazards.Count == 0)
            return null;

        var effects = new List<string>();

        foreach (var hazard in data.Hazards)
        {
            switch (hazard.Type)
            {
                case PlanarHazardType.ExtremeHeat:
                    effects.Add($"[{data.Name}] {hazard.Description}");
                    break;
                case PlanarHazardType.ExtremeCold:
                    effects.Add($"[{data.Name}] {hazard.Description}");
                    break;
                case PlanarHazardType.NegativeEnergy:
                    effects.Add($"[{data.Name}] {hazard.Description}");
                    break;
                case PlanarHazardType.PositiveEnergy:
                    effects.Add($"[{data.Name}] {hazard.Description}");
                    break;
                case PlanarHazardType.Drowning:
                    effects.Add($"[{data.Name}] {hazard.Description}");
                    break;
                case PlanarHazardType.Crushing:
                    effects.Add($"[{data.Name}] {hazard.Description}");
                    break;
            }
        }

        if (effects.Count == 0) return null;
        string combined = string.Join("\n", effects);
        Debug.Log($"[PlanarTravel] Environmental effects on {plane}: {combined}");
        return combined;
    }

    /// <summary>
    /// Check if a plane has alignment restrictions that would penalize a character.
    /// Returns penalty magnitude: 0 = none, -2 = mild, -4 = strong.
    /// </summary>
    public int GetAlignmentPenalty(Plane plane, string characterAlignment)
    {
        var data = GetPlaneData(plane);
        if (data == null || data.AlignmentTrait == PlanarAlignmentTrait.None)
            return 0;

        // Check if character's alignment opposes the plane
        bool opposed = IsAlignmentOpposed(characterAlignment, data.AlignmentType);
        if (!opposed) return 0;

        return data.AlignmentTrait == PlanarAlignmentTrait.StronglyAligned ? -4 : -2;
    }

    private static bool IsAlignmentOpposed(string characterAlignment, string planeAlignment)
    {
        if (string.IsNullOrEmpty(characterAlignment) || string.IsNullOrEmpty(planeAlignment))
            return false;

        string ca = characterAlignment.ToLower();
        string pa = planeAlignment.ToLower();

        // Good vs Evil
        if ((ca.Contains("good") && pa.Contains("evil")) || (ca.Contains("evil") && pa.Contains("good")))
            return true;
        // Lawful vs Chaotic
        if ((ca.Contains("lawful") && pa.Contains("chaotic")) || (ca.Contains("chaotic") && pa.Contains("lawful")))
            return true;

        return false;
    }

    // ════════════════════════════════════════════════════════════
    //  EXTRADIMENSIONAL ITEM INTERACTION
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Check for catastrophic interaction between extradimensional items.
    /// DMG p.248: Bag of Holding + Portable Hole = both destroyed, rift to Astral.
    /// Returns true if catastrophic interaction occurs.
    /// </summary>
    public static bool CheckExtradimensionalInteraction(ItemData item1, ItemData item2)
    {
        if (item1 == null || item2 == null) return false;
        if (!item1.WondrousIsExtradimensional || !item2.WondrousIsExtradimensional) return false;

        // Both are extradimensional — catastrophic interaction!
        Debug.Log($"[PlanarTravel] CATASTROPHIC INTERACTION: {item1.Name} + {item2.Name}!");
        Debug.Log("[PlanarTravel] Both items destroyed! 10×10 ft rift to Astral Plane for 1 round!");
        return true;
    }

    // ════════════════════════════════════════════════════════════
    //  CUBIC GATE HELPERS
    // ════════════════════════════════════════════════════════════

    /// <summary>Default plane assignment for a Cubic Gate's 6 sides.</summary>
    public static Plane[] GetDefaultCubicGateSides()
    {
        return new Plane[]
        {
            Plane.Elemental_Fire,
            Plane.Elemental_Earth,
            Plane.Elemental_Air,
            Plane.Elemental_Water,
            Plane.Astral,
            Plane.Nine_Hells
        };
    }
}
