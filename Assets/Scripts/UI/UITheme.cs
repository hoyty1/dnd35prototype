using UnityEngine;

/// <summary>
/// Centralized UI styling and theming constants.
/// Keeps color/size/font choices consistent across dynamically built UI.
/// </summary>
public static class UITheme
{
    // === COLORS ===
    public static readonly Color ButtonNormal = new Color(0.7f, 0.7f, 0.7f, 1f);
    public static readonly Color ButtonHighlight = new Color(0.8f, 0.8f, 0.8f, 1f);
    public static readonly Color ButtonPressed = new Color(0.6f, 0.6f, 0.6f, 1f);
    public static readonly Color ButtonDisabled = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    public static readonly Color ButtonSpecial = new Color(0.4f, 0.6f, 0.9f, 1f);
    public static readonly Color ButtonDanger = new Color(0.9f, 0.4f, 0.4f, 1f);
    public static readonly Color ButtonSuccess = new Color(0.4f, 0.9f, 0.4f, 1f);
    public static readonly Color ButtonWarning = new Color(0.9f, 0.7f, 0.3f, 1f);

    public static readonly Color PanelBackground = new Color(0.2f, 0.2f, 0.2f, 0.95f);
    public static readonly Color PanelBorder = new Color(0.5f, 0.5f, 0.5f, 1f);

    public static readonly Color TextNormal = Color.white;
    public static readonly Color TextDisabled = new Color(0.6f, 0.6f, 0.6f, 1f);
    public static readonly Color TextHighlight = new Color(1f, 0.9f, 0.5f, 1f);
    public static readonly Color TextError = new Color(1f, 0.4f, 0.4f, 1f);
    public static readonly Color TextSuccess = new Color(0.4f, 1f, 0.4f, 1f);

    // === FONT SIZES ===
    public const int FontSizeSmall = 12;
    public const int FontSizeNormal = 14;
    public const int FontSizeLarge = 16;
    public const int FontSizeHeader = 18;
    public const int FontSizeTitle = 20;

    // === SPACING ===
    public const float SpacingSmall = 5f;
    public const float SpacingNormal = 10f;
    public const float SpacingLarge = 15f;

    // === SIZES ===
    public static readonly Vector2 ButtonSizeSmall = new Vector2(80f, 30f);
    public static readonly Vector2 ButtonSizeNormal = new Vector2(120f, 35f);
    public static readonly Vector2 ButtonSizeLarge = new Vector2(160f, 40f);
    public static readonly Vector2 ButtonSizeWide = new Vector2(200f, 35f);
}
