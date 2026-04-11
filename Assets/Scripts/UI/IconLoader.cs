using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Utility class for loading character portrait icons and map token sprites.
/// Portraits are loaded from Resources/Icons/Portraits/ (64x64 character portraits for UI panels).
/// Tokens are loaded from Resources/Icons/Tokens/ (48x48 map tokens for grid display).
/// Provides caching, fallback disk loading, and helper methods for both icon types.
/// </summary>
public static class IconLoader
{
    private static Dictionary<string, Sprite> _portraitCache = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _tokenCache = new Dictionary<string, Sprite>();

    /// <summary>
    /// Get the portrait sprite for a character class (e.g., "Fighter", "Wizard").
    /// Loads from Resources/Icons/Portraits/{className}_portrait.
    /// </summary>
    public static Sprite GetPortrait(string className)
    {
        string key = className.ToLower();

        // Check cache first
        if (_portraitCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        // Try loading from Resources
        string resourcePath = $"Icons/Portraits/{key}_portrait";
        Sprite sprite = LoadSpriteFromResources(resourcePath);

        if (sprite != null)
        {
            _portraitCache[key] = sprite;
            return sprite;
        }

        // Fallback: try Sprites/Icons path (where IconManager icons live)
        string fallbackPath = $"Sprites/Icons/{key}_portrait";
        sprite = LoadSpriteFromResources(fallbackPath);

        if (sprite != null)
        {
            _portraitCache[key] = sprite;
            return sprite;
        }

        Debug.LogWarning($"[IconLoader] Portrait not found for class: {className}");
        return null;
    }

    /// <summary>
    /// Get the map token sprite for a character class or monster type.
    /// Loads from Resources/Icons/Tokens/{entityType}_token.
    /// </summary>
    public static Sprite GetToken(string entityType)
    {
        string key = entityType.ToLower();

        // Check cache first
        if (_tokenCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        // Try loading from Resources
        string resourcePath = $"Icons/Tokens/{key}_token";
        Sprite sprite = LoadSpriteFromResources(resourcePath);

        if (sprite != null)
        {
            _tokenCache[key] = sprite;
            return sprite;
        }

        // Fallback: try Sprites/Icons path
        string fallbackPath = $"Sprites/Icons/{key}_token";
        sprite = LoadSpriteFromResources(fallbackPath);

        if (sprite != null)
        {
            _tokenCache[key] = sprite;
            return sprite;
        }

        Debug.LogWarning($"[IconLoader] Token not found for entity: {entityType}");
        return null;
    }

    /// <summary>
    /// Get a token sprite for a character on the grid.
    /// Tries class name first, then character name for enemies.
    /// </summary>
    public static Sprite GetTokenForCharacter(CharacterController character)
    {
        if (character == null || character.Stats == null) return null;

        // Try by class name
        Sprite token = GetToken(character.Stats.CharacterClass);
        if (token != null) return token;

        // Try by character name (for enemies like "Goblin", "Orc Berserker")
        string name = character.Stats.CharacterName;
        token = GetToken(name);
        if (token != null) return token;

        // Try extracting monster type from name
        string monsterType = DetermineMonsterType(name);
        if (!string.IsNullOrEmpty(monsterType))
        {
            token = GetToken(monsterType);
            if (token != null) return token;
        }

        return null;
    }

    /// <summary>
    /// Extract a monster base type from a monster name.
    /// e.g., "Orc Berserker" → "orc", "Skeleton Archer" → "skeleton"
    /// </summary>
    public static string DetermineMonsterType(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        string lower = name.ToLower();

        if (lower.Contains("goblin")) return "goblin";
        if (lower.Contains("orc")) return "orc";
        if (lower.Contains("skeleton")) return "skeleton";
        if (lower.Contains("zombie")) return "zombie";
        if (lower.Contains("wolf")) return "wolf";
        if (lower.Contains("hobgoblin")) return "goblin"; // Use goblin token as fallback

        return null;
    }

    /// <summary>
    /// Get a class-specific color for fallback when icons aren't available.
    /// </summary>
    public static Color GetClassColor(string className)
    {
        switch (className)
        {
            case "Fighter":   return new Color(0.8f, 0.2f, 0.2f);
            case "Rogue":     return new Color(0.2f, 0.8f, 0.2f);
            case "Cleric":    return new Color(0.9f, 0.8f, 0.3f);
            case "Wizard":    return new Color(0.3f, 0.5f, 0.9f);
            case "Monk":      return new Color(0.9f, 0.6f, 0.2f);
            case "Barbarian": return new Color(0.6f, 0.1f, 0.1f);
            default:          return Color.gray;
        }
    }

    /// <summary>
    /// Load a sprite from the Resources folder. Handles both Sprite and Texture2D assets.
    /// </summary>
    private static Sprite LoadSpriteFromResources(string path)
    {
        // Try loading as Sprite directly
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;

        // Try loading as Texture2D and converting
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture != null)
        {
            sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            return sprite;
        }

        return null;
    }

    /// <summary>
    /// Clear all cached sprites. Call when changing scenes or to free memory.
    /// </summary>
    public static void ClearCache()
    {
        _portraitCache.Clear();
        _tokenCache.Clear();
    }

    /// <summary>
    /// Test that all expected icons load correctly. Logs results to console.
    /// </summary>
    public static void TestIconLoading()
    {
        Debug.Log("[IconLoader] Testing icon loading...");

        // Test portraits
        string[] classes = { "Fighter", "Rogue", "Cleric", "Wizard", "Monk", "Barbarian" };
        int portraitsLoaded = 0;
        foreach (var className in classes)
        {
            Sprite portrait = GetPortrait(className);
            if (portrait != null)
            {
                Debug.Log($"[IconLoader] ✓ Portrait loaded: {className} ({portrait.texture.width}x{portrait.texture.height})");
                portraitsLoaded++;
            }
            else
            {
                Debug.LogWarning($"[IconLoader] ✗ Portrait missing: {className}");
            }
        }

        // Test tokens
        string[] tokens = { "Fighter", "Rogue", "Cleric", "Wizard", "Monk", "Barbarian",
                           "Goblin", "Orc", "Skeleton", "Zombie", "Wolf" };
        int tokensLoaded = 0;
        foreach (var tokenName in tokens)
        {
            Sprite token = GetToken(tokenName);
            if (token != null)
            {
                Debug.Log($"[IconLoader] ✓ Token loaded: {tokenName} ({token.texture.width}x{token.texture.height})");
                tokensLoaded++;
            }
            else
            {
                Debug.LogWarning($"[IconLoader] ✗ Token missing: {tokenName}");
            }
        }

        Debug.Log($"[IconLoader] Test complete: {portraitsLoaded}/{classes.Length} portraits, {tokensLoaded}/{tokens.Length} tokens loaded.");
    }
}
