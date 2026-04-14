using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Canonical damage/mitigation model used by weapon and spell damage resolution.
/// Supports D&D-style DR X/bypass, flat resistance by type, and immunity by type.
/// </summary>
[Flags]
public enum DamageBypassTag
{
    None = 0,
    Bludgeoning = 1 << 0,
    Piercing = 1 << 1,
    Slashing = 1 << 2,
    Magic = 1 << 3,
    Silver = 1 << 4,
    ColdIron = 1 << 5,
    Adamantine = 1 << 6,
    Good = 1 << 7,
    Evil = 1 << 8,
    Lawful = 1 << 9,
    Chaotic = 1 << 10,
    Ranged = 1 << 11,
}

public enum DamageType
{
    Untyped,
    Bludgeoning,
    Piercing,
    Slashing,
    Fire,
    Cold,
    Acid,
    Electricity,
    Sonic,
    Force,
    Positive,
    Negative,
}

[Serializable]
public class DamageReductionEntry
{
    public int Amount;
    public DamageBypassTag BypassAnyTag = DamageBypassTag.None;
    public bool AppliesToRangedOnly;

    public string GetDisplayString()
    {
        string ranged = AppliesToRangedOnly ? " (ranged only)" : "";
        string bypass = BypassAnyTag == DamageBypassTag.None ? "—" : DamageTextUtils.FormatBypassTags(BypassAnyTag);
        return $"DR {Amount}/{bypass}{ranged}";
    }
}

[Serializable]
public class DamageResistanceEntry
{
    public DamageType Type = DamageType.Untyped;
    public int Amount;

    public string GetDisplayString() => $"Resist {Amount} {DamageTextUtils.GetDamageTypeDisplay(Type)}";
}

public class DamagePacket
{
    public int RawDamage;
    public HashSet<DamageType> Types = new HashSet<DamageType>();
    public DamageBypassTag AttackTags = DamageBypassTag.None;
    public bool IsWeaponDamage;
    public bool IsRangedWeaponDamage;
    public string SourceName;
}

public class DamageResolutionResult
{
    public int RawDamage;
    public int DamageAfterImmunity;
    public int ResistanceApplied;
    public int DamageAfterResistance;
    public int DamageReductionApplied;
    public int FinalDamage;
    public bool ImmunityTriggered;
    public DamageType ImmunityType = DamageType.Untyped;

    public int TotalPrevented => Math.Max(0, RawDamage - FinalDamage);

    public string GetMitigationSummary()
    {
        if (RawDamage <= 0) return "";
        if (ImmunityTriggered)
            return $"Immune to {DamageTextUtils.GetDamageTypeDisplay(ImmunityType)} (blocked {RawDamage})";

        var parts = new List<string>();
        if (ResistanceApplied > 0) parts.Add($"Resist {ResistanceApplied}");
        if (DamageReductionApplied > 0) parts.Add($"DR {DamageReductionApplied}");
        if (parts.Count == 0) return "";
        return string.Join(" + ", parts);
    }
}

public static class DamageTextUtils
{
    public static string GetDamageTypeDisplay(DamageType type)
    {
        switch (type)
        {
            case DamageType.Bludgeoning: return "bludgeoning";
            case DamageType.Piercing: return "piercing";
            case DamageType.Slashing: return "slashing";
            case DamageType.Fire: return "fire";
            case DamageType.Cold: return "cold";
            case DamageType.Acid: return "acid";
            case DamageType.Electricity: return "electricity";
            case DamageType.Sonic: return "sonic";
            case DamageType.Force: return "force";
            case DamageType.Positive: return "positive";
            case DamageType.Negative: return "negative";
            default: return "untyped";
        }
    }

    public static string FormatDamageTypes(IEnumerable<DamageType> types)
    {
        if (types == null) return "untyped";
        var ordered = types.Where(t => t != DamageType.Untyped).Distinct().Select(GetDamageTypeDisplay).ToList();
        return ordered.Count == 0 ? "untyped" : string.Join("/", ordered);
    }

    public static string FormatBypassTags(DamageBypassTag tags)
    {
        if (tags == DamageBypassTag.None) return "none";
        var parts = new List<string>();
        if (tags.HasFlag(DamageBypassTag.Bludgeoning)) parts.Add("bludgeoning");
        if (tags.HasFlag(DamageBypassTag.Piercing)) parts.Add("piercing");
        if (tags.HasFlag(DamageBypassTag.Slashing)) parts.Add("slashing");
        if (tags.HasFlag(DamageBypassTag.Magic)) parts.Add("magic");
        if (tags.HasFlag(DamageBypassTag.Silver)) parts.Add("silver");
        if (tags.HasFlag(DamageBypassTag.ColdIron)) parts.Add("cold iron");
        if (tags.HasFlag(DamageBypassTag.Adamantine)) parts.Add("adamantine");
        if (tags.HasFlag(DamageBypassTag.Good)) parts.Add("good");
        if (tags.HasFlag(DamageBypassTag.Evil)) parts.Add("evil");
        if (tags.HasFlag(DamageBypassTag.Lawful)) parts.Add("lawful");
        if (tags.HasFlag(DamageBypassTag.Chaotic)) parts.Add("chaotic");
        if (tags.HasFlag(DamageBypassTag.Ranged)) parts.Add("ranged");
        return string.Join("/", parts);
    }

    public static HashSet<DamageType> ParseDamageTypes(string source)
    {
        var set = new HashSet<DamageType>();
        if (string.IsNullOrWhiteSpace(source))
        {
            set.Add(DamageType.Untyped);
            return set;
        }

        string normalized = source.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        var tokens = normalized.Split(new[] { '/', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            switch (token)
            {
                case "bludgeoning": set.Add(DamageType.Bludgeoning); break;
                case "piercing": set.Add(DamageType.Piercing); break;
                case "slashing": set.Add(DamageType.Slashing); break;
                case "fire": set.Add(DamageType.Fire); break;
                case "cold": set.Add(DamageType.Cold); break;
                case "acid": set.Add(DamageType.Acid); break;
                case "electricity":
                case "lightning": set.Add(DamageType.Electricity); break;
                case "sonic": set.Add(DamageType.Sonic); break;
                case "force": set.Add(DamageType.Force); break;
                case "positive":
                case "positiveenergy": set.Add(DamageType.Positive); break;
                case "negative":
                case "negativeenergy": set.Add(DamageType.Negative); break;
            }
        }

        if (set.Count == 0) set.Add(DamageType.Untyped);
        return set;
    }

    public static DamageType ParseSingleDamageType(string source)
    {
        var parsed = ParseDamageTypes(source);
        return parsed.FirstOrDefault();
    }

    public static DamageBypassTag ParseBypassTags(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return DamageBypassTag.None;
        string normalized = source.ToLowerInvariant().Replace("_", " ").Replace("-", " ");
        var tokens = normalized.Split(new[] { '/', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        DamageBypassTag tags = DamageBypassTag.None;

        foreach (var token in tokens)
        {
            string t = token.Trim();
            switch (t)
            {
                case "bludgeoning": tags |= DamageBypassTag.Bludgeoning; break;
                case "piercing": tags |= DamageBypassTag.Piercing; break;
                case "slashing": tags |= DamageBypassTag.Slashing; break;
                case "magic": tags |= DamageBypassTag.Magic; break;
                case "silver":
                case "silvered": tags |= DamageBypassTag.Silver; break;
                case "cold iron":
                case "coldiron": tags |= DamageBypassTag.ColdIron; break;
                case "adamantine": tags |= DamageBypassTag.Adamantine; break;
                case "good": tags |= DamageBypassTag.Good; break;
                case "evil": tags |= DamageBypassTag.Evil; break;
                case "lawful": tags |= DamageBypassTag.Lawful; break;
                case "chaotic": tags |= DamageBypassTag.Chaotic; break;
                case "ranged":
                case "arrow":
                case "arrows": tags |= DamageBypassTag.Ranged; break;
            }
        }

        return tags;
    }
}
