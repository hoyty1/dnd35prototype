using System.Collections.Generic;

/// <summary>
/// Represents a deity from the D&D 3.5e Greyhawk pantheon.
/// Clerics must choose a deity, which determines their available domains.
/// </summary>
[System.Serializable]
public class DeityData
{
    /// <summary>Unique identifier (e.g., "pelor").</summary>
    public string DeityId;

    /// <summary>Display name (e.g., "Pelor").</summary>
    public string Name;

    /// <summary>Alignment of the deity.</summary>
    public Alignment DeityAlignment;

    /// <summary>List of domain names this deity offers (e.g., "Good", "Healing").</summary>
    public List<string> Domains;

    /// <summary>Favored weapon name.</summary>
    public string FavoredWeapon;

    /// <summary>Portfolio description.</summary>
    public string Portfolio;

    /// <summary>Short title (e.g., "God of the Sun").</summary>
    public string Title;

    public DeityData(string id, string name, Alignment alignment, List<string> domains,
                     string favoredWeapon, string portfolio, string title)
    {
        DeityId = id;
        Name = name;
        DeityAlignment = alignment;
        Domains = domains;
        FavoredWeapon = favoredWeapon;
        Portfolio = portfolio;
        Title = title;
    }

    /// <summary>
    /// Check if a character alignment is compatible with this deity.
    /// D&D 3.5e rule: cleric must be within one step of deity's alignment.
    /// </summary>
    public bool IsAlignmentCompatible(Alignment characterAlignment)
    {
        return AlignmentHelper.IsWithinOneStep(characterAlignment, DeityAlignment);
    }

    /// <summary>Get alignment abbreviation for display.</summary>
    public string AlignmentAbbr => AlignmentHelper.GetAbbreviation(DeityAlignment);

    /// <summary>Get domains as comma-separated string.</summary>
    public string DomainsString => string.Join(", ", Domains);
}
