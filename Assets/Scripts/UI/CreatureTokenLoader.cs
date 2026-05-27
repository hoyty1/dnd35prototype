using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads creature token images from StreamingAssets/CreatureTokens/ at runtime.
/// Tokens are 256x256 circular PNG images with transparency, extracted from the
/// D&D 3.5e Monster Manual. Provides caching and fallback handling.
///
/// Usage:
///   Sprite tokenSprite = CreatureTokenLoader.GetToken("goblin");
///   if (tokenSprite != null) myImage.sprite = tokenSprite;
///
/// The loader checks StreamingAssets/CreatureTokens/{name}.png, where {name}
/// is the sanitized creature name (lowercase, underscores for spaces).
/// It also supports explicit paths via NPCDefinition.TokenPath.
/// </summary>
public static class CreatureTokenLoader
{
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
    private static CreatureManifest _manifest;
    private static bool _manifestLoaded;

    /// <summary>Base directory for creature token images.</summary>
    public static string TokenDirectory =>
        Path.Combine(Application.streamingAssetsPath, "CreatureTokens");

    /// <summary>
    /// Get a creature token sprite by creature name or NPCDefinition.TokenPath.
    /// Returns null if no token image exists for the creature.
    /// </summary>
    /// <param name="creatureName">Creature name (e.g., "Goblin", "Mind Flayer", "Red Dragon")</param>
    /// <returns>Sprite with the circular creature token, or null if not found</returns>
    public static Sprite GetToken(string creatureName)
    {
        if (string.IsNullOrEmpty(creatureName)) return null;

        // Check cache first
        string cacheKey = creatureName.ToLowerInvariant();
        if (_cache.TryGetValue(cacheKey, out Sprite cached))
            return cached;

        // Try loading from file
        Sprite sprite = TryLoadToken(creatureName);
        _cache[cacheKey] = sprite; // Cache even null to avoid repeated file checks
        return sprite;
    }

    /// <summary>
    /// Get a creature token sprite using an NPCDefinition.
    /// Checks TokenPath first, then falls back to creature name lookup.
    /// </summary>
    public static Sprite GetToken(NPCDefinition npcDef)
    {
        if (npcDef == null) return null;

        // Try explicit TokenPath first
        if (!string.IsNullOrEmpty(npcDef.TokenPath))
        {
            string cacheKey = npcDef.TokenPath.ToLowerInvariant();
            if (_cache.TryGetValue(cacheKey, out Sprite cached))
                return cached;

            string fullPath = Path.Combine(TokenDirectory, npcDef.TokenPath);
            Sprite sprite = LoadSpriteFromFile(fullPath);
            _cache[cacheKey] = sprite;
            if (sprite != null) return sprite;
        }

        // Fall back to name-based lookup
        return GetToken(npcDef.Name ?? npcDef.Id);
    }

    /// <summary>
    /// Check if a token exists for a given creature name without loading it.
    /// </summary>
    public static bool HasToken(string creatureName)
    {
        if (string.IsNullOrEmpty(creatureName)) return false;
        string filename = SanitizeFilename(creatureName) + ".png";
        string fullPath = Path.Combine(TokenDirectory, filename);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Get the manifest data (creature list, counts, metadata).
    /// </summary>
    public static CreatureManifest GetManifest()
    {
        if (!_manifestLoaded)
        {
            _manifestLoaded = true;
            string manifestPath = Path.Combine(TokenDirectory, "creature_manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    _manifest = JsonUtility.FromJson<CreatureManifest>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CreatureTokenLoader] Failed to load manifest: {e.Message}");
                }
            }
        }
        return _manifest;
    }

    /// <summary>Clear all cached sprites (useful for memory management).</summary>
    public static void ClearCache()
    {
        foreach (var kvp in _cache)
        {
            if (kvp.Value != null && kvp.Value.texture != null)
                UnityEngine.Object.Destroy(kvp.Value.texture);
        }
        _cache.Clear();
    }

    /// <summary>Get count of loaded/cached tokens.</summary>
    public static int CachedTokenCount => _cache.Count;

    // --- Private helpers ---

    private static Sprite TryLoadToken(string creatureName)
    {
        // Build filename from creature name
        string filename = SanitizeFilename(creatureName) + ".png";
        string fullPath = Path.Combine(TokenDirectory, filename);

        if (File.Exists(fullPath))
            return LoadSpriteFromFile(fullPath);

        // Try alternate naming patterns
        // Remove parenthetical: "Babau (demon)" -> "babau"
        string simplified = System.Text.RegularExpressions.Regex.Replace(
            creatureName, @"\s*\([^)]*\)\s*", " ").Trim();
        if (simplified != creatureName)
        {
            filename = SanitizeFilename(simplified) + ".png";
            fullPath = Path.Combine(TokenDirectory, filename);
            if (File.Exists(fullPath))
                return LoadSpriteFromFile(fullPath);
        }

        return null;
    }

    private static Sprite LoadSpriteFromFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            if (texture.LoadImage(fileData))
            {
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                return sprite;
            }
            else
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CreatureTokenLoader] Error loading token '{path}': {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sanitize a creature name into a valid filename.
    /// "Mind Flayer" -> "mind_flayer", "Will-o'-wisp" -> "will-o-wisp"
    /// </summary>
    private static string SanitizeFilename(string name)
    {
        // Remove parenthetical notes
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s*\([^)]*\)\s*", " ");
        name = name.Trim().ToLowerInvariant();
        // Replace special chars
        name = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-z0-9\s\-]", "");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", "_");
        name = name.Trim('_');
        if (name.Length > 60) name = name.Substring(0, 60);
        return name;
    }
}

/// <summary>
/// Serializable manifest data for the creature token collection.
/// Loaded from StreamingAssets/CreatureTokens/creature_manifest.json.
/// </summary>
[Serializable]
public class CreatureManifest
{
    public string version;
    public string description;
    public int token_size;
    public int total_tokens;
    public int unique_creatures;
}
