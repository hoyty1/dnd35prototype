using UnityEngine;

/// <summary>
/// Standardized color palette for persistent area effects.
/// Use these values for transparent grid-highlight overlays.
/// </summary>
public static class AreaEffectColors
{
    // Weather / environment
    public static readonly Color ObscuringMist = new Color(0.85f, 0.95f, 1.0f, 0.5f);
    public static readonly Color FogCloud = new Color(0.7f, 0.7f, 0.75f, 0.65f);
    public static readonly Color SolidFog = new Color(0.5f, 0.5f, 0.55f, 0.8f);
    public static readonly Color SleetStorm = new Color(0.8f, 0.9f, 1.0f, 0.7f);

    // Slippery effects
    public static readonly Color Grease = new Color(0.6f, 0.4f, 0.2f, 0.6f);

    // Fire / heat
    public static readonly Color WallOfFire = new Color(1.0f, 0.5f, 0.2f, 0.7f);
    public static readonly Color IncendiaryCloud = new Color(1.0f, 0.4f, 0.1f, 0.6f);
    public static readonly Color FlamingSphere = new Color(1.0f, 0.6f, 0.0f, 0.8f);

    // Cold / ice
    public static readonly Color WallOfIce = new Color(0.7f, 0.9f, 1.0f, 0.7f);
    public static readonly Color WallOfIceBreached = new Color(0.5f, 0.7f, 0.85f, 0.35f);
    public static readonly Color IceStorm = new Color(0.75f, 0.85f, 0.95f, 0.65f);

    // Poison / acid
    public static readonly Color Cloudkill = new Color(0.7f, 0.9f, 0.3f, 0.7f);
    public static readonly Color StinkingCloud = new Color(0.8f, 0.75f, 0.4f, 0.6f);
    public static readonly Color AcidFog = new Color(0.6f, 0.85f, 0.4f, 0.7f);

    // Darkness / shadow
    public static readonly Color Darkness = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    public static readonly Color DeeperDarkness = new Color(0.05f, 0.05f, 0.05f, 0.95f);

    // Magical effects
    public static readonly Color Web = new Color(0.9f, 0.9f, 0.9f, 0.5f);
    public static readonly Color Entangle = new Color(0.4f, 0.6f, 0.3f, 0.6f);
    public static readonly Color AntimagicField = new Color(0.7f, 0.5f, 0.9f, 0.5f);
    public static readonly Color Silence = new Color(0.6f, 0.7f, 0.9f, 0.4f);
    public static readonly Color Glitterdust = new Color(1.0f, 0.84f, 0.2f, 0.45f);

    // Wind / air
    public static readonly Color WindWall = new Color(0.7f, 0.85f, 1.0f, 0.45f);

    // Force effects
    public static readonly Color ResilientSphere = new Color(0.6f, 0.3f, 0.9f, 0.50f); // Purple/violet for force

    // Illusion (concealment / invisibility emanations)
    public static readonly Color InvisibilitySphere = new Color(0.6f, 0.7f, 1.0f, 0.35f);
}
