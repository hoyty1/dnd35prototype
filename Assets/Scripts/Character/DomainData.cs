using System.Collections.Generic;

/// <summary>
/// Represents a cleric domain from D&D 3.5e PHB.
/// Each domain provides a granted power and domain spells at each spell level.
/// </summary>
[System.Serializable]
public class DomainData
{
    /// <summary>Domain name (e.g., "Good", "Healing").</summary>
    public string Name;

    /// <summary>Description of the domain's granted power.</summary>
    public string GrantedPower;

    /// <summary>Domain spells by spell level. Key = spell level (1-9), Value = spell ID.</summary>
    public Dictionary<int, string> DomainSpells;

    public DomainData(string name, string grantedPower, Dictionary<int, string> domainSpells)
    {
        Name = name;
        GrantedPower = grantedPower;
        DomainSpells = domainSpells ?? new Dictionary<int, string>();
    }

    /// <summary>Get the domain spell ID for a given spell level, or null if none.</summary>
    public string GetDomainSpellId(int spellLevel)
    {
        return DomainSpells.TryGetValue(spellLevel, out string id) ? id : null;
    }

    /// <summary>Check if this domain has a spell at the given level.</summary>
    public bool HasSpellAtLevel(int spellLevel)
    {
        return DomainSpells.ContainsKey(spellLevel);
    }
}
