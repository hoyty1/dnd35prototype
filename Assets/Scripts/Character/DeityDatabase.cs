using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static database of D&D 3.5e Greyhawk pantheon deities.
/// Used during character creation for deity selection.
/// </summary>
public static class DeityDatabase
{
    private static Dictionary<string, DeityData> _deities = new Dictionary<string, DeityData>();
    private static bool _initialized = false;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        Register(new DeityData("pelor", "Pelor", Alignment.NeutralGood,
            new List<string> { "Good", "Healing", "Strength", "Sun" },
            "Mace", "Sun, light, strength, healing", "God of the Sun"));

        Register(new DeityData("st_cuthbert", "St. Cuthbert", Alignment.LawfulNeutral,
            new List<string> { "Destruction", "Law", "Protection", "Strength" },
            "Mace", "Retribution, honesty, truth, discipline", "God of Retribution"));

        Register(new DeityData("heironeous", "Heironeous", Alignment.LawfulGood,
            new List<string> { "Good", "Law", "War" },
            "Longsword", "Valor, chivalry, justice, honor, war, daring", "God of Valor"));

        Register(new DeityData("wee_jas", "Wee Jas", Alignment.LawfulNeutral,
            new List<string> { "Death", "Law", "Magic" },
            "Dagger", "Death, magic, vanity, law", "Goddess of Death and Magic"));

        Register(new DeityData("boccob", "Boccob", Alignment.TrueNeutral,
            new List<string> { "Knowledge", "Magic", "Trickery" },
            "Quarterstaff", "Magic, arcane knowledge, balance", "God of Magic"));

        Register(new DeityData("nerull", "Nerull", Alignment.NeutralEvil,
            new List<string> { "Death", "Evil", "Trickery" },
            "Scythe", "Death, darkness, murder, the underworld", "God of Death"));

        Register(new DeityData("hextor", "Hextor", Alignment.LawfulEvil,
            new List<string> { "Destruction", "Evil", "Law", "War" },
            "Flail", "Tyranny, war, discord, massacres, conflict", "God of Tyranny"));

        Register(new DeityData("erythnul", "Erythnul", Alignment.ChaoticEvil,
            new List<string> { "Chaos", "Evil", "Trickery", "War" },
            "Morningstar", "Hate, envy, malice, panic, ugliness, slaughter", "God of Slaughter"));

        Register(new DeityData("obad_hai", "Obad-Hai", Alignment.TrueNeutral,
            new List<string> { "Air", "Animal", "Earth", "Fire", "Plant", "Water" },
            "Quarterstaff", "Nature, woodlands, freedom, hunting, beasts", "God of Nature"));

        Register(new DeityData("ehlonna", "Ehlonna", Alignment.NeutralGood,
            new List<string> { "Animal", "Good", "Plant", "Sun" },
            "Longbow", "Forests, woodlands, flora, fauna, fertility", "Goddess of the Forests"));

        Register(new DeityData("kord", "Kord", Alignment.ChaoticGood,
            new List<string> { "Chaos", "Good", "Luck", "Strength" },
            "Greatsword", "Athletics, sports, brawling, strength, courage", "God of Strength"));

        Register(new DeityData("fharlanghn", "Fharlanghn", Alignment.TrueNeutral,
            new List<string> { "Luck", "Protection", "Travel" },
            "Quarterstaff", "Horizons, distance, travel, roads", "God of Horizons"));

        Register(new DeityData("olidammara", "Olidammara", Alignment.ChaoticNeutral,
            new List<string> { "Chaos", "Luck", "Trickery" },
            "Rapier", "Rogues, music, revelry, wine, humor, tricks", "God of Rogues"));

        Register(new DeityData("moradin", "Moradin", Alignment.LawfulGood,
            new List<string> { "Earth", "Good", "Law", "Protection" },
            "Warhammer", "Dwarves, creation, smithing, protection, metalcraft", "Dwarf God"));

        Register(new DeityData("corellon", "Corellon Larethian", Alignment.ChaoticGood,
            new List<string> { "Chaos", "Good", "Protection", "War" },
            "Longsword", "Elves, magic, music, arts, crafts, poetry, warfare", "Elf God"));

        Register(new DeityData("yondalla", "Yondalla", Alignment.LawfulGood,
            new List<string> { "Good", "Law", "Protection" },
            "Short Sword", "Halflings, protection, bounty, children, security, leadership", "Halfling Goddess"));

        Debug.Log($"[DeityDatabase] Initialized {_deities.Count} deities.");
    }

    private static void Register(DeityData deity)
    {
        _deities[deity.DeityId] = deity;
    }

    /// <summary>Get a deity by its ID.</summary>
    public static DeityData GetDeity(string deityId)
    {
        if (!_initialized) Init();
        return _deities.TryGetValue(deityId, out DeityData deity) ? deity : null;
    }

    /// <summary>Get all deities.</summary>
    public static List<DeityData> GetAllDeities()
    {
        if (!_initialized) Init();
        return new List<DeityData>(_deities.Values);
    }

    /// <summary>Get deities compatible with a given character alignment (within one step).</summary>
    public static List<DeityData> GetCompatibleDeities(Alignment characterAlignment)
    {
        if (!_initialized) Init();
        var result = new List<DeityData>();
        foreach (var deity in _deities.Values)
        {
            if (deity.IsAlignmentCompatible(characterAlignment))
                result.Add(deity);
        }
        return result;
    }

    /// <summary>Total number of registered deities.</summary>
    public static int Count => _deities.Count;
}
