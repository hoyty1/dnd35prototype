using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class WizardFamiliar
{
    public bool hasFamiliar;
    public string familiarType = string.Empty;
    public List<FamiliarBonusEntry> serializedBonuses = new List<FamiliarBonusEntry>();

    [NonSerialized] public Dictionary<string, int> bonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Dictionary<string, int>> FamiliarBonusTable =
        new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bat"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Listen"] = 3 },
            ["Cat"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Move Silently"] = 3 },
            ["Hawk"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Spot"] = 3 },
            ["Owl"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Spot"] = 3 },
            ["Rat"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fortitude"] = 2 },
            ["Raven"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Appraise"] = 3 },
            ["Snake (Viper)"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Bluff"] = 3 },
            ["Toad"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["HitPoints"] = 3 },
            ["Weasel"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Reflex"] = 2 }
        };

    public static IReadOnlyList<string> FamiliarTypes => FamiliarBonusTable.Keys.ToList();

    public bool HasAnyBonus(string key)
    {
        EnsureBonusesInitialized();
        return bonuses.ContainsKey(key) && bonuses[key] != 0;
    }

    public int GetBonus(string key)
    {
        EnsureBonusesInitialized();
        return bonuses.TryGetValue(key, out int value) ? value : 0;
    }

    public int HitPointBonus => GetBonus("HitPoints");
    public int FortitudeBonus => GetBonus("Fortitude");
    public int ReflexBonus => GetBonus("Reflex");

    public void EnsureBonusesInitialized()
    {
        if (bonuses == null)
            bonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (bonuses.Count == 0 && serializedBonuses != null && serializedBonuses.Count > 0)
        {
            for (int i = 0; i < serializedBonuses.Count; i++)
            {
                FamiliarBonusEntry entry = serializedBonuses[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                bonuses[entry.key] = entry.value;
            }
        }
    }

    public void RebuildBonuses()
    {
        bonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (!hasFamiliar || string.IsNullOrWhiteSpace(familiarType))
        {
            serializedBonuses = new List<FamiliarBonusEntry>();
            return;
        }

        if (!FamiliarBonusTable.TryGetValue(familiarType, out Dictionary<string, int> familiarBonuses))
        {
            serializedBonuses = new List<FamiliarBonusEntry>();
            return;
        }

        foreach (var kvp in familiarBonuses)
            bonuses[kvp.Key] = kvp.Value;

        serializedBonuses = bonuses
            .Select(kvp => new FamiliarBonusEntry { key = kvp.Key, value = kvp.Value })
            .ToList();
    }

    public static WizardFamiliar CreateNone()
    {
        return new WizardFamiliar
        {
            hasFamiliar = false,
            familiarType = string.Empty,
            serializedBonuses = new List<FamiliarBonusEntry>(),
            bonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        };
    }

    public static WizardFamiliar Create(string familiarType)
    {
        var familiar = new WizardFamiliar
        {
            hasFamiliar = !string.IsNullOrWhiteSpace(familiarType),
            familiarType = string.IsNullOrWhiteSpace(familiarType) ? string.Empty : familiarType.Trim()
        };
        familiar.RebuildBonuses();
        return familiar;
    }

    [Serializable]
    public class FamiliarBonusEntry
    {
        public string key;
        public int value;
    }
}
