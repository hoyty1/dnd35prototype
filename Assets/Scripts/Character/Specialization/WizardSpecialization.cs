using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class WizardSpecialization
{
    public bool isSpecialist;
    public string specializationSchool = string.Empty;
    public List<string> prohibitedSchools = new List<string>();

    private static readonly string[] SupportedSchools =
    {
        "Abjuration", "Conjuration", "Divination", "Enchantment",
        "Evocation", "Illusion", "Necromancy", "Transmutation"
    };

    public static IReadOnlyList<string> SelectableSchools => SupportedSchools;

    public bool IsGeneralist => !isSpecialist || string.IsNullOrWhiteSpace(specializationSchool);

    public int RequiredProhibitedSchoolCount
    {
        get
        {
            if (IsGeneralist)
                return 0;

            return string.Equals(specializationSchool, "Divination", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        }
    }

    public void Normalize()
    {
        if (!isSpecialist)
        {
            specializationSchool = string.Empty;
            prohibitedSchools = new List<string>();
            return;
        }

        specializationSchool = NormalizeSchoolName(specializationSchool);
        prohibitedSchools = (prohibitedSchools ?? new List<string>())
            .Select(NormalizeSchoolName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool IsValid(out string error)
    {
        Normalize();

        if (IsGeneralist)
        {
            error = string.Empty;
            return true;
        }

        if (!SelectableSchools.Any(s => string.Equals(s, specializationSchool, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Select a valid specialist school.";
            return false;
        }

        if (prohibitedSchools.Any(s => string.Equals(s, "Universal", StringComparison.OrdinalIgnoreCase)))
        {
            error = "Universal school cannot be prohibited.";
            return false;
        }

        if (prohibitedSchools.Any(s => string.Equals(s, specializationSchool, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Specialization school cannot also be prohibited.";
            return false;
        }

        if (prohibitedSchools.Count != RequiredProhibitedSchoolCount)
        {
            error = $"Select exactly {RequiredProhibitedSchoolCount} prohibited school(s).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool IsProhibitedSchool(string school)
    {
        if (string.IsNullOrWhiteSpace(school) || prohibitedSchools == null)
            return false;

        return prohibitedSchools.Any(s => string.Equals(s, school.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeSchoolName(string school)
    {
        if (string.IsNullOrWhiteSpace(school))
            return string.Empty;

        string trimmed = school.Trim();
        string canonical = SupportedSchools.FirstOrDefault(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(canonical))
            return canonical;

        if (string.Equals(trimmed, "Universal", StringComparison.OrdinalIgnoreCase))
            return "Universal";

        return trimmed;
    }

    public string GetSpecialistTitle()
    {
        if (IsGeneralist)
            return "Generalist";

        switch (specializationSchool)
        {
            case "Abjuration": return "Abjurer";
            case "Conjuration": return "Conjurer";
            case "Divination": return "Diviner";
            case "Enchantment": return "Enchanter";
            case "Evocation": return "Evoker";
            case "Illusion": return "Illusionist";
            case "Necromancy": return "Necromancer";
            case "Transmutation": return "Transmuter";
            default: return specializationSchool;
        }
    }

    public static WizardSpecialization CreateGeneralist()
    {
        return new WizardSpecialization
        {
            isSpecialist = false,
            specializationSchool = string.Empty,
            prohibitedSchools = new List<string>()
        };
    }
}
