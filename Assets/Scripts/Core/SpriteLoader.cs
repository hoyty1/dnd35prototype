using UnityEngine;

/// <summary>
/// Utility to load sprites from the Resources folder.
/// Sprites should be placed in Assets/Resources/Sprites/
/// </summary>
public static class SpriteLoader
{
    public static Sprite Load(string spriteName)
    {
        // Try as sprite first
        Sprite s = Resources.Load<Sprite>("Sprites/" + spriteName);
        if (s != null) return s;

        // Try loading as texture and creating sprite
        Texture2D tex = Resources.Load<Texture2D>("Sprites/" + spriteName);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 64f);
        }

        Debug.LogWarning($"SpriteLoader: Could not load sprite '{spriteName}'");
        return null;
    }
}
