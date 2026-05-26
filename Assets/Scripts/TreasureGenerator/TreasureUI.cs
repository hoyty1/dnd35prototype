// ============================================================================
// TreasureUI.cs — Unity UI component for displaying generated treasure
// D&D 3.5e DMG Treasure Generation System
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DND35e.Treasure;

/// <summary>
/// Displays generated treasure results in a scrollable UI panel.
/// Attach to a GameObject with a Canvas parent. Creates its own UI hierarchy.
/// Can be shown after combat or triggered manually for testing.
/// </summary>
public class TreasureUI : MonoBehaviour
{
    [Header("References (auto-created if null)")]
    public GameObject PanelRoot;
    public Text TitleText;
    public Text ContentText;
    public Button CloseButton;
    public ScrollRect ScrollView;

    [Header("Settings")]
    public Color CoinColor = new Color(1f, 0.84f, 0f);       // Gold
    public Color GemColor = new Color(0.5f, 0.8f, 1f);        // Light blue
    public Color ArtColor = new Color(0.9f, 0.7f, 1f);        // Light purple
    public Color MundaneColor = new Color(0.8f, 0.8f, 0.8f);  // Light gray
    public Color MagicColor = new Color(0.3f, 1f, 0.3f);      // Green

    private TreasureResult _currentResult;
    private System.Action _onClosed;

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>
    /// Generate and display treasure for the given encounter level.
    /// </summary>
    /// <param name="el">Encounter Level (1-20)</param>
    /// <param name="monsterGearGP">Monster gear GP to subtract</param>
    /// <param name="onClosed">Optional callback when UI is closed</param>
    public void ShowTreasure(int el, int monsterGearGP = 0, System.Action onClosed = null)
    {
        _onClosed = onClosed;
        _currentResult = DND35e.Treasure.TreasureGenerator.Generate(el, monsterGearGP);
        Debug.Log($"[TreasureUI] Generated treasure for EL {el}: {_currentResult.TotalGPValue:N0} gp total");
        DisplayResult(_currentResult);
        Show();
    }

    /// <summary>
    /// Display a pre-generated treasure result.
    /// </summary>
    public void ShowResult(TreasureResult result, System.Action onClosed = null)
    {
        _onClosed = onClosed;
        _currentResult = result;
        DisplayResult(result);
        Show();
    }

    /// <summary>Get the last generated result.</summary>
    public TreasureResult GetLastResult() => _currentResult;

    public void Show()
    {
        EnsureUICreated();
        PanelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (PanelRoot != null)
            PanelRoot.SetActive(false);
        _onClosed?.Invoke();
        _onClosed = null;
    }

    public bool IsVisible => PanelRoot != null && PanelRoot.activeSelf;

    // ========================================================================
    // DISPLAY LOGIC
    // ========================================================================

    private void DisplayResult(TreasureResult result)
    {
        EnsureUICreated();

        if (result == null)
        {
            TitleText.text = "No Treasure";
            ContentText.text = "The encounter yielded no treasure.";
            return;
        }

        TitleText.text = $"Treasure — EL {result.EncounterLevel} ({result.TotalGPValue:N0} gp)";

        var sb = new System.Text.StringBuilder();

        // Coins
        if (result.CoinsGPValue > 0)
        {
            sb.AppendLine("<b><color=#FFD700>═══ Coins ═══</color></b>");
            var parts = new List<string>();
            if (result.CopperPieces > 0) parts.Add($"{result.CopperPieces:N0} cp");
            if (result.SilverPieces > 0) parts.Add($"{result.SilverPieces:N0} sp");
            if (result.GoldPieces > 0) parts.Add($"{result.GoldPieces:N0} gp");
            if (result.PlatinumPieces > 0) parts.Add($"{result.PlatinumPieces:N0} pp");
            sb.AppendLine(string.Join(", ", parts));
            sb.AppendLine($"<i>(≈{result.CoinsGPValue:N0} gp value)</i>");
            sb.AppendLine();
        }

        // Gems
        if (result.Gems.Count > 0)
        {
            sb.AppendLine($"<b><color=#80CCFF>═══ Gems ({result.Gems.Count}) — {result.GemsGPValue:N0} gp ═══</color></b>");
            foreach (var g in result.Gems)
                sb.AppendLine($"  • {g.Name} <color=#80CCFF>({g.Value:N0} gp)</color>");
            sb.AppendLine();
        }

        // Art Objects
        if (result.ArtObjects.Count > 0)
        {
            sb.AppendLine($"<b><color=#E6B3FF>═══ Art Objects ({result.ArtObjects.Count}) — {result.ArtGPValue:N0} gp ═══</color></b>");
            foreach (var a in result.ArtObjects)
                sb.AppendLine($"  • {a.Name} <color=#E6B3FF>({a.Value:N0} gp)</color>");
            sb.AppendLine();
        }

        // Mundane Items
        if (result.MundaneItems.Count > 0)
        {
            sb.AppendLine($"<b><color=#CCCCCC>═══ Mundane Items ({result.MundaneItems.Count}) — {result.MundaneGPValue:N0} gp ═══</color></b>");
            foreach (var m in result.MundaneItems)
                sb.AppendLine($"  • {m.Name} <color=#CCCCCC>({m.Value:N0} gp)</color>");
            sb.AppendLine();
        }

        // Magic Items
        if (result.MagicItems.Count > 0)
        {
            sb.AppendLine($"<b><color=#4DFF4D>═══ Magic Items ({result.MagicItems.Count}) — {result.MagicItemsGPValue:N0} gp ═══</color></b>");
            foreach (var item in result.MagicItems)
            {
                string typeTag = item.Type != null ? $"[{item.Type}]" : "";
                sb.AppendLine($"  • {item.Name} <color=#4DFF4D>({item.Price:N0} gp)</color> {typeTag}");
            }
            sb.AppendLine();
        }

        // Empty treasure
        if (result.IsEmpty)
        {
            sb.AppendLine("<i>The encounter yielded no treasure.</i>");
        }

        // Monster gear subtraction note
        if (result.MonsterGearSubtracted > 0)
        {
            sb.AppendLine($"<color=#FF8888><i>(Monster gear subtracted: {result.MonsterGearSubtracted:N0} gp)</i></color>");
        }

        ContentText.text = sb.ToString();

        // Reset scroll position to top
        if (ScrollView != null)
            ScrollView.verticalNormalizedPosition = 1f;
    }

    // ========================================================================
    // UI CONSTRUCTION (auto-creates if references not assigned)
    // ========================================================================

    private void EnsureUICreated()
    {
        if (PanelRoot != null) return;

        // Create panel root
        PanelRoot = new GameObject("TreasurePanel", typeof(RectTransform), typeof(Image));
        PanelRoot.transform.SetParent(transform, false);
        var panelRT = PanelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.15f, 0.1f);
        panelRT.anchorMax = new Vector2(0.85f, 0.9f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        var panelImg = PanelRoot.GetComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

        // Title bar
        var titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(PanelRoot.transform, false);
        var titleRT = titleObj.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.9f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.offsetMin = new Vector2(10, 0);
        titleRT.offsetMax = new Vector2(-60, -5);
        TitleText = titleObj.GetComponent<Text>();
        TitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (TitleText.font == null) TitleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        TitleText.fontSize = 20;
        TitleText.fontStyle = FontStyle.Bold;
        TitleText.color = CoinColor;
        TitleText.alignment = TextAnchor.MiddleLeft;
        TitleText.text = "Treasure";

        // Close button
        var closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(PanelRoot.transform, false);
        var closeRT = closeObj.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0.9f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.offsetMin = new Vector2(-50, 5);
        closeRT.offsetMax = new Vector2(-5, -5);
        closeObj.GetComponent<Image>().color = new Color(0.6f, 0.15f, 0.15f, 1f);
        CloseButton = closeObj.GetComponent<Button>();
        CloseButton.onClick.AddListener(Hide);

        // Close button text
        var closeTxtObj = new GameObject("CloseTxt", typeof(RectTransform), typeof(Text));
        closeTxtObj.transform.SetParent(closeObj.transform, false);
        var closeTxtRT = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRT.anchorMin = Vector2.zero;
        closeTxtRT.anchorMax = Vector2.one;
        closeTxtRT.offsetMin = Vector2.zero;
        closeTxtRT.offsetMax = Vector2.zero;
        var closeTxt = closeTxtObj.GetComponent<Text>();
        closeTxt.font = TitleText.font;
        closeTxt.fontSize = 18;
        closeTxt.fontStyle = FontStyle.Bold;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.text = "✕";

        // Scroll view
        var scrollObj = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollObj.transform.SetParent(PanelRoot.transform, false);
        var scrollRT = scrollObj.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 0.9f);
        scrollRT.offsetMin = new Vector2(5, 5);
        scrollRT.offsetMax = new Vector2(-5, -5);
        scrollObj.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.8f);
        ScrollView = scrollObj.GetComponent<ScrollRect>();
        ScrollView.horizontal = false;
        ScrollView.movementType = ScrollRect.MovementType.Clamped;

        // Content container
        var contentObj = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(scrollObj.transform, false);
        var contentRT = contentObj.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = new Vector2(0, 0);
        contentRT.offsetMax = new Vector2(0, 0);
        var fitter = contentObj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollView.content = contentRT;

        // Content text
        var textObj = new GameObject("ContentText", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(contentObj.transform, false);
        var textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.offsetMin = new Vector2(10, 5);
        textRT.offsetMax = new Vector2(-10, -5);
        ContentText = textObj.GetComponent<Text>();
        ContentText.font = TitleText.font;
        ContentText.fontSize = 14;
        ContentText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        ContentText.alignment = TextAnchor.UpperLeft;
        ContentText.supportRichText = true;
        ContentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        ContentText.verticalOverflow = VerticalWrapMode.Overflow;

        // Also add ContentSizeFitter to text so it drives the content height
        var textFitter = textObj.AddComponent<ContentSizeFitter>();
        textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Add VerticalLayoutGroup to content so text expands it
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        PanelRoot.SetActive(false);
    }

    // ========================================================================
    // TESTING: Quick test from Unity Editor or debug console
    // ========================================================================

#if UNITY_EDITOR
    [ContextMenu("Test Generate EL 5")]
    private void TestGenerateEL5() { ShowTreasure(5); }

    [ContextMenu("Test Generate EL 10")]
    private void TestGenerateEL10() { ShowTreasure(10); }

    [ContextMenu("Test Generate EL 15")]
    private void TestGenerateEL15() { ShowTreasure(15); }

    [ContextMenu("Test Generate EL 20")]
    private void TestGenerateEL20() { ShowTreasure(20); }
#endif
}
