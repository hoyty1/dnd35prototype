using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight screen-space tooltip for battlefield character hover details.
/// Displays name, race, equipment tags, HP condition state, and active statuses.
/// </summary>
public class CharacterHoverTooltipUI : MonoBehaviour
{
    public static CharacterHoverTooltipUI Instance { get; private set; }

    private RectTransform _panel;
    private Text _text;
    private Canvas _canvas;
    private int _lastShowFrame = -1;

    // Tooltip text caching to avoid expensive/redundant rebuilds and UI relayout flicker.
    private CharacterController _lastTooltipCharacter;
    private int _lastTooltipStateHash;
    private string _lastTooltipText;
    private bool _hasTooltipCache;

    public static void EnsureInstance()
    {
        if (Instance != null)
            return;

        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("CharacterTooltipCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        GameObject go = new GameObject("CharacterHoverTooltipUI");
        go.transform.SetParent(canvas.transform, false);
        CharacterHoverTooltipUI tooltip = go.AddComponent<CharacterHoverTooltipUI>();
        tooltip.Initialize(canvas);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Initialize(Canvas canvas)
    {
        _canvas = canvas;

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.86f);

        _panel.pivot = new Vector2(0f, 1f);
        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(0f, 1f);
        _panel.sizeDelta = new Vector2(280f, 120f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        _text = textGO.AddComponent<Text>();
        _text.font = LoadBuiltinTooltipFont();
        _text.fontSize = 14;
        _text.color = new Color(1f, 0.95f, 0.8f, 1f);
        _text.alignment = TextAnchor.UpperLeft;
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;

        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8f, 8f);
        textRT.offsetMax = new Vector2(-8f, -8f);

        HideTooltip();
    }

    private void LateUpdate()
    {
        if (_panel != null && _panel.gameObject.activeSelf && _lastShowFrame != Time.frameCount)
        {
            _panel.gameObject.SetActive(false);
            ResetTooltipCache();
        }
    }

    public void ShowTooltip(CharacterController character, Vector2 screenPosition)
    {
        if (_panel == null || _text == null || _canvas == null || character == null || character.Stats == null)
            return;

        int currentStateHash = CalculateCharacterStateHash(character);
        bool requiresRebuild = !_hasTooltipCache
            || character != _lastTooltipCharacter
            || currentStateHash != _lastTooltipStateHash;

        if (requiresRebuild)
        {
            string rebuiltTooltipText = BuildTooltipText(character);
            if (!string.Equals(_lastTooltipText, rebuiltTooltipText, StringComparison.Ordinal))
            {
                _lastTooltipText = rebuiltTooltipText;
                _text.text = rebuiltTooltipText;

                float width = Mathf.Clamp(_text.preferredWidth + 20f, 180f, 420f);
                float height = Mathf.Clamp(_text.preferredHeight + 16f, 64f, 320f);
                _panel.sizeDelta = new Vector2(width, height);
            }

            _lastTooltipCharacter = character;
            _lastTooltipStateHash = currentStateHash;
            _hasTooltipCache = true;
        }

        RectTransform canvasRT = _canvas.transform as RectTransform;
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPosition, uiCamera, out Vector2 localPos))
            _panel.anchoredPosition = new Vector2(localPos.x + 16f, localPos.y - 16f);

        _lastShowFrame = Time.frameCount;
        _panel.gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        if (_panel != null)
            _panel.gameObject.SetActive(false);

        ResetTooltipCache();
    }

    private void ResetTooltipCache()
    {
        _lastTooltipCharacter = null;
        _lastTooltipStateHash = 0;
        _lastTooltipText = null;
        _hasTooltipCache = false;
    }

    private static int CalculateCharacterStateHash(CharacterController character)
    {
        if (character == null)
            return 0;

        unchecked
        {
            int hash = 17;

            hash = (hash * 31) + character.GetInstanceID();

            if (character.Stats != null)
            {
                hash = (hash * 31) + character.Stats.CurrentHP;
                hash = (hash * 31) + character.Stats.MaxHP;
                if (!string.IsNullOrWhiteSpace(character.Stats.ChallengeRating))
                    hash = (hash * 31) + character.Stats.ChallengeRating.GetHashCode();
            }

            InventoryComponent inventoryComponent = character.GetComponent<InventoryComponent>();
            Inventory inventory = inventoryComponent != null ? inventoryComponent.CharacterInventory : null;
            if (inventory != null)
            {
                hash = (hash * 31) + (inventory.RightHandSlot != null ? inventory.RightHandSlot.GetHashCode() : 0);
                hash = (hash * 31) + (inventory.LeftHandSlot != null ? inventory.LeftHandSlot.GetHashCode() : 0);
                hash = (hash * 31) + (inventory.ArmorRobeSlot != null ? inventory.ArmorRobeSlot.GetHashCode() : 0);
                hash = (hash * 31) + (inventory.HandsSlot != null ? inventory.HandsSlot.GetHashCode() : 0);
            }

            if (character.Tags != null)
            {
                List<string> sortedTags = character.Tags.GetAllTags()
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (int i = 0; i < sortedTags.Count; i++)
                    hash = (hash * 31) + sortedTags[i].GetHashCode();
            }

            if (character.Conditions != null)
                hash = (hash * 31) + character.Conditions.GetActiveConditionsCount();

            hash = (hash * 31) + character.ActiveDiseases.Count;
            hash = (hash * 31) + character.ActivePoisons.Count;

            string diseaseSummary = character.GetActiveDiseaseSummary();
            string poisonSummary = character.GetActivePoisonSummary();
            if (!string.IsNullOrEmpty(diseaseSummary))
                hash = (hash * 31) + diseaseSummary.GetHashCode();
            if (!string.IsNullOrEmpty(poisonSummary))
                hash = (hash * 31) + poisonSummary.GetHashCode();

            return hash;
        }
    }

    private static string BuildTooltipText(CharacterController character)
    {
        var tags = character.Tags.GetAllTags()
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
        StringBuilder sb = new StringBuilder();

        string displayName = !string.IsNullOrWhiteSpace(character.Stats.CharacterName)
            ? character.Stats.CharacterName
            : character.name;
        sb.Append(displayName);

        string teamLabel = character.Team.ToString();
        string controlLabel = character.IsControllable ? "Player" : "AI";
        sb.Append("\nTeam: ").Append(teamLabel).Append(" • Control: ").Append(controlLabel);

        string race = ExtractTagValue(character, "Race: ");
        if (!string.IsNullOrWhiteSpace(race) && race != "Unknown")
            sb.Append("\nRace: ").Append(race);

        if (!string.IsNullOrWhiteSpace(character.Stats.ChallengeRating))
            sb.Append("\nCR: ").Append(character.Stats.ChallengeRatingDisplay);

        List<string> wielding = GetWieldingTags(tags);
        if (wielding.Count > 0)
            sb.Append("\nWielding: ").Append(string.Join(", ", wielding));
        else
            Debug.LogWarning($"[Tooltip] No wielding tags found for {displayName}. Tags: {character.Tags.GetTagsDebugString()}");

        List<string> armor = GetArmorTags(tags);
        if (armor.Count > 0)
            sb.Append("\nArmor: ").Append(string.Join(", ", armor));
        else
            Debug.LogWarning($"[Tooltip] No armor tags found for {displayName}. Tags: {character.Tags.GetTagsDebugString()}");

        string hpState = ExtractTagValue(character, "HP State: ");
        if (!string.IsNullOrWhiteSpace(hpState) && hpState != "Unknown")
            sb.Append("\nCondition: ").Append(hpState);

        List<string> statuses = character.Tags.GetTagsByPrefix("Status: ")
            .Where(tag => tag.Length > "Status: ".Length)
            .Select(tag => tag.Substring("Status: ".Length))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (statuses.Count > 0)
            sb.Append("\nStatus: ").Append(string.Join(", ", statuses));

        string specialAbilities = character.Stats.GetSpecialAbilitiesSummary();
        if (!string.IsNullOrEmpty(specialAbilities))
            sb.Append("\nTraits: ").Append(specialAbilities);

        string diseaseSummary = character.GetActiveDiseaseSummary();
        if (!string.IsNullOrEmpty(diseaseSummary))
            sb.Append("\nDisease: ").Append(diseaseSummary);

        string poisonSummary = character.GetActivePoisonSummary();
        if (!string.IsNullOrEmpty(poisonSummary))
            sb.Append("\nPoison: ").Append(poisonSummary);

        return sb.ToString();
    }

    private static string FormatEquipmentNameForTooltip(string equipmentName)
    {
        if (string.IsNullOrWhiteSpace(equipmentName))
            return equipmentName;

        return FormatShieldNameForTooltip(equipmentName.Trim());
    }

    private static string FormatShieldNameForTooltip(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return itemName;

        string trimmed = itemName.Trim();
        string lower = trimmed.ToLowerInvariant();

        // Convert database-style names like "Shield, Heavy Wooden" to natural form "Heavy Wooden Shield".
        if (lower.StartsWith("shield,"))
        {
            string descriptor = trimmed.Substring("shield,".Length).Trim();
            if (string.IsNullOrEmpty(descriptor))
                return "Shield";

            string titleDescriptor = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(descriptor.ToLowerInvariant());
            return $"{titleDescriptor} Shield";
        }

        if (string.Equals(lower, "tower shield", StringComparison.Ordinal))
            return "Tower Shield";

        if (string.Equals(lower, "buckler", StringComparison.Ordinal))
            return "Buckler";

        return trimmed;
    }

    private static List<string> GetWieldingTags(List<string> tags)
    {
        List<string> wielding = tags
            .Where(tag => tag.StartsWith("Wielding: "))
            .Select(tag => tag.Substring("Wielding: ".Length))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(FormatEquipmentNameForTooltip)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wielding.Count > 0)
            return wielding;

        // Backward-compatible fallback for older tag format.
        return tags
            .Where(tag => tag.Equals("Dual-Wielding")
                          || tag.Equals("Single Weapon")
                          || tag.Equals("Unarmed")
                          || tag.Equals("Shield Equipped")
                          || tag.Equals("Two-Handed Weapon"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetArmorTags(List<string> tags)
    {
        List<string> armor = tags
            .Where(tag => tag.StartsWith("Armor: "))
            .Select(tag => tag.Substring("Armor: ".Length))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(FormatEquipmentNameForTooltip)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (armor.Count > 0)
            return armor;

        // Backward-compatible fallback for older armor tags.
        return tags
            .Where(tag => tag.Equals("Unarmored")
                          || tag.Equals("Light Armor")
                          || tag.Equals("Medium Armor")
                          || tag.Equals("Heavy Armor")
                          || tag.EndsWith(" Armor"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ExtractTagValue(CharacterController character, string prefix)
    {
        foreach (string tag in character.Tags.GetTagsByPrefix(prefix))
        {
            if (tag.Length > prefix.Length)
                return tag.Substring(prefix.Length);
        }

        return "Unknown";
    }

    private static Font LoadBuiltinTooltipFont()
    {
        try
        {
            Font legacyRuntime = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyRuntime != null)
                return legacyRuntime;
        }
        catch
        {
        }

        try
        {
            Font arial = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (arial != null)
                return arial;
        }
        catch
        {
        }

        return Font.CreateDynamicFontFromOSFont("Arial", 14);
    }
}
