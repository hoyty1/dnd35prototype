using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper class to create styled vertical scrollbars for ScrollRect components.
/// Provides a consistent look across all scrollable UIs in the project.
/// </summary>
public static class ScrollbarHelper
{
    /// <summary>
    /// Creates a vertical scrollbar and attaches it to the given ScrollRect.
    /// The scrollbar is positioned on the right edge of the scroll area.
    ///
    /// Structure created:
    ///   Scrollbar (Scrollbar component + Image background)
    ///     └─ Sliding Area (RectTransform)
    ///         └─ Handle (Image)
    /// </summary>
    /// <param name="scrollRect">The ScrollRect to attach the scrollbar to.</param>
    /// <param name="scrollAreaTransform">The transform of the scroll area (parent of viewport).</param>
    /// <param name="width">Width of the scrollbar in pixels. Default: 16.</param>
    /// <returns>The created Scrollbar component.</returns>
    public static Scrollbar CreateVerticalScrollbar(
        ScrollRect scrollRect,
        Transform scrollAreaTransform,
        float width = 16f)
    {
        // --- Scrollbar root ---
        GameObject scrollbarGO = new GameObject("Scrollbar Vertical");
        scrollbarGO.transform.SetParent(scrollAreaTransform, false);

        RectTransform sbRT = scrollbarGO.AddComponent<RectTransform>();
        // Anchor to the right edge, full height
        sbRT.anchorMin = new Vector2(1, 0);
        sbRT.anchorMax = new Vector2(1, 1);
        sbRT.pivot = new Vector2(1, 0.5f);
        sbRT.offsetMin = new Vector2(-width, 0);
        sbRT.offsetMax = Vector2.zero;

        Image sbBg = scrollbarGO.AddComponent<Image>();
        sbBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        // --- Sliding Area ---
        GameObject slidingArea = new GameObject("Sliding Area");
        slidingArea.transform.SetParent(scrollbarGO.transform, false);

        RectTransform saRT = slidingArea.AddComponent<RectTransform>();
        saRT.anchorMin = Vector2.zero;
        saRT.anchorMax = Vector2.one;
        // Small padding so handle doesn't touch edges
        saRT.offsetMin = new Vector2(0, 2);
        saRT.offsetMax = new Vector2(0, -2);

        // --- Handle ---
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(slidingArea.transform, false);

        RectTransform hRT = handle.AddComponent<RectTransform>();
        hRT.anchorMin = Vector2.zero;
        hRT.anchorMax = new Vector2(1, 0.2f); // Initial size; Scrollbar component manages this
        hRT.offsetMin = Vector2.zero;
        hRT.offsetMax = Vector2.zero;

        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.4f, 0.4f, 0.5f, 1f);

        // --- Configure Scrollbar component ---
        Scrollbar scrollbar = scrollbarGO.AddComponent<Scrollbar>();
        scrollbar.handleRect = hRT;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImg;

        // --- Link to ScrollRect ---
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -2f;

        return scrollbar;
    }
}
