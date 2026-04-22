using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generic, extensible tag container for a character.
/// Supports visual/combat/status/role tags and future categories.
/// </summary>
public class CharacterTags
{
    private readonly HashSet<string> _tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public CharacterTags(CharacterController character)
    {
    }

    public int Count => _tags.Count;

    /// <summary>Add a tag to this character.</summary>
    public bool AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        return _tags.Add(tag.Trim());
    }

    /// <summary>Remove a tag from this character.</summary>
    public bool RemoveTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        return _tags.Remove(tag.Trim());
    }

    /// <summary>Check if the character has a specific tag.</summary>
    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        return _tags.Contains(tag.Trim());
    }

    /// <summary>Returns a copy of all tags.</summary>
    public IEnumerable<string> GetAllTags()
    {
        return _tags.ToArray();
    }

    /// <summary>Remove all tags.</summary>
    public void ClearAllTags()
    {
        _tags.Clear();
    }

    /// <summary>Remove all tags with a prefix (case-insensitive).</summary>
    public int RemoveTagsByPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return 0;

        return _tags.RemoveWhere(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Get tags with a prefix (case-insensitive).</summary>
    public IEnumerable<string> GetTagsByPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return Enumerable.Empty<string>();

        return _tags.Where(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public string GetTagsDebugString()
    {
        if (_tags.Count == 0)
            return "(none)";

        return string.Join(", ", _tags.OrderBy(tag => tag));
    }
}
