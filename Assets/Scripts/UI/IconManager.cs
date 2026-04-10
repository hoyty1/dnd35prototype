using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages loading and mapping of character/enemy icons.
/// Icons are loaded from Resources/Sprites/Icons/ folder.
/// Maps class names and enemy IDs to their respective icons.
/// </summary>
public static class IconManager
{
    private static Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();
    private static bool _initialized = false;

    /// <summary>
    /// Class name → icon file name mapping (without extension).
    /// </summary>
    private static readonly Dictionary<string, string> ClassIconMap = new Dictionary<string, string>
    {
        { "Fighter", "fighter_icon" },
        { "Rogue", "rogue_icon" },
        { "Monk", "monk_icon" },
        { "Barbarian", "barbarian_icon" },
    };

    /// <summary>
    /// Enemy ID/name → icon file name mapping (without extension).
    /// </summary>
    private static readonly Dictionary<string, string> EnemyIconMap = new Dictionary<string, string>
    {
        { "skeleton_archer", "skeleton_archer_icon" },
        { "Skeleton Archer", "skeleton_archer_icon" },
        { "orc_berserker", "orc_berserker_icon" },
        { "Orc Berserker", "orc_berserker_icon" },
        { "hobgoblin_sergeant", "hobgoblin_sergeant_icon" },
        { "Hobgoblin Sergeant", "hobgoblin_sergeant_icon" },
    };

    /// <summary>
    /// Initialize the icon manager and preload all icons.
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // Preload all class icons
        foreach (var kvp in ClassIconMap)
            LoadIcon(kvp.Value);

        // Preload all enemy icons
        var loaded = new HashSet<string>();
        foreach (var kvp in EnemyIconMap)
        {
            if (!loaded.Contains(kvp.Value))
            {
                LoadIcon(kvp.Value);
                loaded.Add(kvp.Value);
            }
        }

        Debug.Log($"[IconManager] Initialized with {_iconCache.Count} icons loaded.");
    }

    /// <summary>
    /// Load an icon from Resources/Sprites/Icons/ and cache it.
    /// </summary>
    private static Sprite LoadIcon(string iconName)
    {
        if (_iconCache.ContainsKey(iconName) && _iconCache[iconName] != null)
            return _iconCache[iconName];

        string path = "Sprites/Icons/" + iconName;
        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite == null)
        {
            // Try loading as texture and creating sprite
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
        }

        if (sprite != null)
        {
            _iconCache[iconName] = sprite;
            Debug.Log($"[IconManager] Loaded icon: {iconName}");
        }
        else
        {
            Debug.LogWarning($"[IconManager] Failed to load icon: {path}");
        }

        return sprite;
    }

    /// <summary>
    /// Get the icon sprite for a PC class (Fighter, Rogue, Monk, Barbarian).
    /// </summary>
    public static Sprite GetClassIcon(string className)
    {
        Init();
        if (ClassIconMap.TryGetValue(className, out string iconName))
        {
            if (_iconCache.TryGetValue(iconName, out Sprite sprite))
                return sprite;
            return LoadIcon(iconName);
        }
        Debug.LogWarning($"[IconManager] No icon mapping for class: {className}");
        return null;
    }

    /// <summary>
    /// Get the icon sprite for an enemy (by ID or display name).
    /// </summary>
    public static Sprite GetEnemyIcon(string enemyIdOrName)
    {
        Init();
        if (EnemyIconMap.TryGetValue(enemyIdOrName, out string iconName))
        {
            if (_iconCache.TryGetValue(iconName, out Sprite sprite))
                return sprite;
            return LoadIcon(iconName);
        }
        Debug.LogWarning($"[IconManager] No icon mapping for enemy: {enemyIdOrName}");
        return null;
    }

    /// <summary>
    /// Get icon for a character (auto-detects PC vs NPC by class or name).
    /// </summary>
    public static Sprite GetIconForCharacter(CharacterController character)
    {
        if (character == null || character.Stats == null) return null;

        Init();

        // Try as PC class icon first
        string className = character.Stats.CharacterClass;
        if (ClassIconMap.ContainsKey(className))
            return GetClassIcon(className);

        // Try as enemy by name
        string charName = character.Stats.CharacterName;
        if (EnemyIconMap.ContainsKey(charName))
            return GetEnemyIcon(charName);

        return null;
    }
}
