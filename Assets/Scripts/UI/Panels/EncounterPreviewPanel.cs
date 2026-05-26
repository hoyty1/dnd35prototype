using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable panel component that displays a formatted preview of a dungeon encounter.
/// Can be embedded in any UI that needs to show encounter details.
///
/// Shows:
///   - Encounter name and description
///   - Dungeon level and Encounter Level (EL)
///   - Creature list with counts and class levels
///   - Total creature count
///   - Difficulty estimate relative to party level
///
/// Phase 4: UI Integration for DMG Encounter Tables.
/// </summary>
public class EncounterPreviewPanel : MonoBehaviour
{
    // =========================================================================
    //  State
    // =========================================================================

    private GameObject _panel;
    private Text _titleText;
    private Text _detailsText;
    private Text _creaturesText;
    private Text _difficultyText;

    private static readonly Color PanelBg = new Color(0.06f, 0.08f, 0.14f, 0.95f);
    private static readonly Color TitleColor = new Color(0.95f, 0.86f, 0.45f, 1f);
    private static readonly Color DetailsColor = new Color(0.8f, 0.85f, 0.95f, 1f);
    private static readonly Color CreatureColor = new Color(0.9f, 0.94f, 1f, 1f);

    /// <summary>Whether the panel has been built.</summary>
    public bool IsBuilt => _panel != null;

    // =========================================================================
    //  Public API
    // =========================================================================

    /// <summary>
    /// Build the panel under the given parent transform.
    /// Call once, then use ShowEncounter/Clear to update content.
    /// </summary>
    public void Build(Transform parent, float width = 500f, float height = 300f)
    {
        if (_panel != null) return;

        _panel = new GameObject("EncounterPreviewPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(parent, false);

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(width, height);
        _panel.GetComponent<Image>().color = PanelBg;

        Outline outline = _panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.4f, 0.6f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        float yOffset = -10f;

        // Title
        _titleText = CreateText(_panel.transform, "", 22, FontStyle.Bold, TitleColor, TextAnchor.MiddleCenter);
        PositionText(_titleText, yOffset, 30f, 20f);
        yOffset -= 36f;

        // Details line (level, EL, environment)
        _detailsText = CreateText(_panel.transform, "", 16, FontStyle.Normal, DetailsColor, TextAnchor.UpperLeft);
        _detailsText.supportRichText = true;
        PositionText(_detailsText, yOffset, 60f, 20f);
        yOffset -= 66f;

        // Creatures list
        _creaturesText = CreateText(_panel.transform, "", 17, FontStyle.Normal, CreatureColor, TextAnchor.UpperLeft);
        _creaturesText.supportRichText = true;
        _creaturesText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _creaturesText.verticalOverflow = VerticalWrapMode.Overflow;
        PositionText(_creaturesText, yOffset, 120f, 20f);
        yOffset -= 126f;

        // Difficulty estimate
        _difficultyText = CreateText(_panel.transform, "", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        _difficultyText.supportRichText = true;
        PositionText(_difficultyText, yOffset, 28f, 20f);

        _panel.SetActive(false);
    }

    /// <summary>
    /// Display the given encounter in the preview panel.
    /// </summary>
    /// <param name="encounter">The encounter to display.</param>
    /// <param name="dungeonLevel">Dungeon level it was rolled on.</param>
    /// <param name="partyLevel">Party level for difficulty comparison.</param>
    public void ShowEncounter(EncounterDefinition encounter, int dungeonLevel = 0, int partyLevel = 0)
    {
        if (!IsBuilt) return;

        if (encounter == null)
        {
            Clear();
            return;
        }

        _panel.SetActive(true);

        // Title
        if (_titleText != null)
            _titleText.text = encounter.Name ?? "Dungeon Encounter";

        // Details
        if (_detailsText != null)
        {
            StringBuilder sb = new StringBuilder();
            if (dungeonLevel > 0)
                sb.AppendLine($"<b>Dungeon Level:</b> {dungeonLevel}");
            if (encounter.TargetEL > 0)
                sb.AppendLine($"<b>Encounter Level:</b> {encounter.TargetEL}");
            if (!string.IsNullOrEmpty(encounter.Environment))
                sb.AppendLine($"<b>Environment:</b> {encounter.Environment}");
            _detailsText.text = sb.ToString().TrimEnd();
        }

        // Creatures
        if (_creaturesText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Creatures:</b>");
            int totalCount = 0;
            for (int i = 0; i < encounter.Entries.Count; i++)
            {
                EncounterCreatureEntry entry = encounter.Entries[i];
                if (entry == null) continue;
                int count = Mathf.Max(1, entry.Count);
                totalCount += count;
                sb.AppendLine($"  • {entry.DisplayName}");
            }
            sb.AppendLine($"\n<b>Total:</b> {totalCount} creature{(totalCount != 1 ? "s" : "")}");
            _creaturesText.text = sb.ToString().TrimEnd();
        }

        // Difficulty
        if (_difficultyText != null && partyLevel > 0 && encounter.TargetEL > 0)
        {
            int diff = encounter.TargetEL - partyLevel;
            string label;
            Color color;
            if (diff <= -3)      { label = "💤 Trivial"; color = new Color(0.5f, 0.5f, 0.5f); }
            else if (diff <= -1) { label = "🟢 Easy"; color = new Color(0.3f, 0.8f, 0.3f); }
            else if (diff == 0)  { label = "🟡 Average"; color = new Color(0.9f, 0.8f, 0.2f); }
            else if (diff <= 2)  { label = "🟠 Challenging"; color = new Color(0.9f, 0.55f, 0.15f); }
            else if (diff <= 4)  { label = "🔴 Hard"; color = new Color(0.9f, 0.25f, 0.2f); }
            else                 { label = "💀 Deadly"; color = new Color(0.7f, 0.1f, 0.1f); }

            _difficultyText.text = $"Difficulty: {label}";
            _difficultyText.color = color;
        }
        else if (_difficultyText != null)
        {
            _difficultyText.text = "";
        }
    }

    /// <summary>Clear the preview and hide the panel.</summary>
    public void Clear()
    {
        if (_titleText != null) _titleText.text = "";
        if (_detailsText != null) _detailsText.text = "";
        if (_creaturesText != null) _creaturesText.text = "";
        if (_difficultyText != null) _difficultyText.text = "";
        if (_panel != null) _panel.SetActive(false);
    }

    /// <summary>Show or hide the panel.</summary>
    public void SetVisible(bool visible)
    {
        if (_panel != null) _panel.SetActive(visible);
    }

    // =========================================================================
    //  Helpers
    // =========================================================================

    private Text CreateText(Transform parent, string value, int fontSize, FontStyle style,
        Color color, TextAnchor alignment)
    {
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(parent, false);

        Text text = textObj.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontStyle = style;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;

        return text;
    }

    private void PositionText(Text text, float yOffset, float height, float xPadding)
    {
        RectTransform rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(-(xPadding * 2f), height);
    }
}
