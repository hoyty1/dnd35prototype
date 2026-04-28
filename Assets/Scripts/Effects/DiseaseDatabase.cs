using System;
using System.Collections.Generic;

/// <summary>
/// D&D 3.5e DMG disease catalog (core/common examples).
/// </summary>
public static class DiseaseDatabase
{
    private static Dictionary<DiseaseType, DiseaseData> _diseases;

    public static void Initialize()
    {
        _diseases = new Dictionary<DiseaseType, DiseaseData>
        {
            [DiseaseType.BlindingSickness] = new DiseaseData
            {
                Name = "Blinding Sickness",
                Type = DiseaseType.BlindingSickness,
                FortitudeDC = 16,
                IncubationPeriod = "1d3",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.STR, DamageAmount = "1d4", IsDrain = false }
                },
                Description = "Contact disease. Victim takes daily Strength damage and may eventually go blind."
            },
            [DiseaseType.CackleFever] = new DiseaseData
            {
                Name = "Cackle Fever",
                Type = DiseaseType.CackleFever,
                FortitudeDC = 16,
                IncubationPeriod = "1",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.WIS, DamageAmount = "1d6", IsDrain = false }
                },
                Description = "Inhaled disease causing uncontrollable laughter and Wisdom damage."
            },
            [DiseaseType.FilthFever] = new DiseaseData
            {
                Name = "Filth Fever",
                Type = DiseaseType.FilthFever,
                FortitudeDC = 12,
                IncubationPeriod = "1d3",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.DEX, DamageAmount = "1d3", IsDrain = false },
                    new AbilityDamageEffect { Ability = AbilityType.CON, DamageAmount = "1d3", IsDrain = false }
                },
                Description = "Common disease from filth/infected wounds."
            },
            [DiseaseType.Mindfire] = new DiseaseData
            {
                Name = "Mindfire",
                Type = DiseaseType.Mindfire,
                FortitudeDC = 12,
                IncubationPeriod = "1",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.INT, DamageAmount = "1d4", IsDrain = false }
                },
                Description = "Inhaled disease attacking the mind."
            },
            [DiseaseType.RedAche] = new DiseaseData
            {
                Name = "Red Ache",
                Type = DiseaseType.RedAche,
                FortitudeDC = 15,
                IncubationPeriod = "1d3",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.STR, DamageAmount = "1d6", IsDrain = false }
                },
                Description = "Injury-spread disease causing painful, weakening inflammation."
            },
            [DiseaseType.Shakes] = new DiseaseData
            {
                Name = "The Shakes",
                Type = DiseaseType.Shakes,
                FortitudeDC = 13,
                IncubationPeriod = "1",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.DEX, DamageAmount = "1d8", IsDrain = false }
                },
                Description = "Contact disease causing violent tremors."
            },
            [DiseaseType.SlimyDoom] = new DiseaseData
            {
                Name = "Slimy Doom",
                Type = DiseaseType.SlimyDoom,
                FortitudeDC = 14,
                IncubationPeriod = "1",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.CON, DamageAmount = "1d4", IsDrain = false }
                },
                Description = "Contact disease with severe Constitution damage."
            },
            [DiseaseType.MummyRot] = new DiseaseData
            {
                Name = "Mummy Rot",
                Type = DiseaseType.MummyRot,
                FortitudeDC = 20,
                IncubationPeriod = "1",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.CON, DamageAmount = "1d6", IsDrain = false }
                },
                Description = "Supernatural curse-disease from mummies."
            },
            [DiseaseType.DevilChills] = new DiseaseData
            {
                Name = "Devil Chills",
                Type = DiseaseType.DevilChills,
                FortitudeDC = 14,
                IncubationPeriod = "1d4",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.STR, DamageAmount = "1d4", IsDrain = false }
                },
                Description = "Infernal disease causing persistent shivering and weakness."
            },
            [DiseaseType.DemonFever] = new DiseaseData
            {
                Name = "Demon Fever",
                Type = DiseaseType.DemonFever,
                FortitudeDC = 18,
                IncubationPeriod = "1",
                DamageEffects = new List<AbilityDamageEffect>
                {
                    new AbilityDamageEffect { Ability = AbilityType.CON, DamageAmount = "1d6", IsDrain = false }
                },
                Description = "Abyssal disease causing burning nightmares and Constitution damage."
            }
        };
    }

    public static DiseaseData GetDisease(DiseaseType type)
    {
        EnsureInitialized();
        return _diseases.TryGetValue(type, out DiseaseData disease) ? disease : null;
    }

    public static DiseaseData GetDiseaseByName(string name)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        foreach (DiseaseData disease in _diseases.Values)
        {
            if (disease != null && string.Equals(disease.Name, name, StringComparison.OrdinalIgnoreCase))
                return disease;
        }

        return null;
    }

    public static IReadOnlyDictionary<DiseaseType, DiseaseData> GetAllDiseases()
    {
        EnsureInitialized();
        return _diseases;
    }

    private static void EnsureInitialized()
    {
        if (_diseases == null)
            Initialize();
    }
}
