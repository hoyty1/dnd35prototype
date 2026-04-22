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
    /// Uses portrait icons for all 6 classes; falls back to legacy _icon files.
    /// </summary>
    private static readonly Dictionary<string, string> ClassIconMap = new Dictionary<string, string>
    {
        { "Fighter", "fighter_portrait" },
        { "Rogue", "rogue_portrait" },
        { "Cleric", "cleric_portrait" },
        { "Wizard", "wizard_portrait" },
        { "Monk", "monk_portrait" },
        { "Barbarian", "barbarian_portrait" },
    };

    /// <summary>
    /// Legacy class icon names for fallback.
    /// </summary>
    private static readonly Dictionary<string, string> ClassIconFallback = new Dictionary<string, string>
    {
        { "Fighter", "fighter_icon" },
        { "Rogue", "rogue_icon" },
        { "Monk", "monk_icon" },
        { "Barbarian", "barbarian_icon" },
    };

    /// <summary>
    /// Class/entity name → token file name mapping for grid display.
    /// </summary>
    private static readonly Dictionary<string, string> TokenIconMap = new Dictionary<string, string>
    {
        { "Fighter", "fighter_token" },
        { "Rogue", "rogue_token" },
        { "Cleric", "cleric_token" },
        { "Wizard", "wizard_token" },
        { "Monk", "monk_token" },
        { "Barbarian", "barbarian_token" },
        { "goblin", "goblin_token" },
        { "orc", "orc_token" },
        { "skeleton", "skeleton_token" },
        { "zombie", "zombie_token" },
        { "wolf", "wolf_token" },
        { "ogre", "ogre_token" },
    };

    /// <summary>
    /// Enemy ID/name → icon file name mapping (without extension).
    /// Includes both portrait-based icons and legacy icons for enemies.
    /// </summary>
    private static readonly Dictionary<string, string> EnemyIconMap = new Dictionary<string, string>
    {
        { "skeleton_archer", "skeleton_archer_icon" },
        { "Skeleton Archer", "skeleton_archer_icon" },
        { "wight_dreadwalker", "skeleton_token" },
        { "Wight Dreadwalker", "skeleton_token" },
        { "orc_berserker", "orc_berserker_icon" },
        { "Orc Berserker", "orc_berserker_icon" },
        { "hobgoblin_sergeant", "hobgoblin_sergeant_icon" },
        { "Hobgoblin Sergeant", "hobgoblin_sergeant_icon" },
        { "goblin", "goblin_token" },
        { "Goblin", "goblin_token" },
        { "goblin_warchief", "goblin_token" },
        { "Goblin Warchief", "goblin_token" },
        { "orc", "orc_token" },
        { "Orc", "orc_token" },
        { "skeleton", "skeleton_token" },
        { "Skeleton", "skeleton_token" },
        { "zombie", "zombie_token" },
        { "Zombie", "zombie_token" },
        { "wolf", "wolf_token" },
        { "Wolf", "wolf_token" },
        { "wolf_pack_hunter", "wolf_token" },
        { "Wolf Pack Hunter", "wolf_token" },
        { "dire_wolf", "wolf_token" },
        { "Dire Wolf", "wolf_token" },
        { "ogre_brute", "ogre_token" },
        { "Ogre Brute", "ogre_token" },
    };

    /// <summary>
    /// Initialize the icon manager and preload all icons (portraits, tokens, enemy icons).
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // Preload all class portrait icons
        foreach (var kvp in ClassIconMap)
            LoadIcon(kvp.Value);

        // Preload legacy class icons as fallback
        foreach (var kvp in ClassIconFallback)
            LoadIcon(kvp.Value);

        // Preload all token icons
        var loaded = new HashSet<string>();
        foreach (var kvp in TokenIconMap)
        {
            if (!loaded.Contains(kvp.Value))
            {
                LoadIcon(kvp.Value);
                loaded.Add(kvp.Value);
            }
        }

        // Preload all enemy icons
        foreach (var kvp in EnemyIconMap)
        {
            if (!loaded.Contains(kvp.Value))
            {
                LoadIcon(kvp.Value);
                loaded.Add(kvp.Value);
            }
        }

        Debug.Log($"[IconManager] Initialized with {_iconCache.Count} icons loaded (portraits + tokens + enemy icons).");
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
    /// Get the portrait icon sprite for a PC class (Fighter, Rogue, Cleric, Wizard, Monk, Barbarian).
    /// Falls back to legacy _icon files if portrait not found.
    /// </summary>
    public static Sprite GetClassIcon(string className)
    {
        Init();
        // Try portrait icon first
        if (ClassIconMap.TryGetValue(className, out string iconName))
        {
            if (_iconCache.TryGetValue(iconName, out Sprite sprite) && sprite != null)
                return sprite;
            Sprite loaded = LoadIcon(iconName);
            if (loaded != null) return loaded;
        }
        // Fallback to legacy icon
        if (ClassIconFallback.TryGetValue(className, out string fallbackName))
        {
            if (_iconCache.TryGetValue(fallbackName, out Sprite sprite) && sprite != null)
                return sprite;
            return LoadIcon(fallbackName);
        }
        Debug.LogWarning($"[IconManager] No icon mapping for class: {className}");
        return null;
    }

    /// <summary>
    /// Get the map token sprite for a PC class or monster type for grid display.
    /// </summary>
    public static Sprite GetTokenIcon(string entityType)
    {
        Init();
        if (TokenIconMap.TryGetValue(entityType, out string iconName))
        {
            if (_iconCache.TryGetValue(iconName, out Sprite sprite) && sprite != null)
                return sprite;
            return LoadIcon(iconName);
        }
        // Try lowercase version for monster types
        string lower = entityType.ToLower();
        if (TokenIconMap.TryGetValue(lower, out string lowerIconName))
        {
            if (_iconCache.TryGetValue(lowerIconName, out Sprite sprite) && sprite != null)
                return sprite;
            return LoadIcon(lowerIconName);
        }
        Debug.LogWarning($"[IconManager] No token mapping for entity: {entityType}");
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