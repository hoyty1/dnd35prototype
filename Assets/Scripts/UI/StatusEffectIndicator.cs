using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Draws compact status effect icons above battlefield tokens.
/// Icons update in real-time based on active combat conditions.
/// </summary>
public class StatusEffectIndicator : MonoBehaviour
{
    [Header("Icon Layout")]
    public float iconSize = 0.28f;
    public float iconSpacing = 0.04f;
    public Vector3 iconOffset = new Vector3(0f, 0.72f, 0f);
    public int iconSortingOrder = 50;

    private CharacterController _character;
    private Transform _iconContainer;
    private readonly List<GameObject> _activeIcons = new List<GameObject>();
    private string _lastSignature = string.Empty;

    private static Sprite _badgeSprite;

    private struct IconData
    {
        public string Key;
        public string ShortLabel;
        public string Tooltip;
        public Color Color;
        public int Duration;
    }

    private void Start()
    {
        _character = GetComponent<CharacterController>();
        if (_character == null)
        {
            enabled = false;
            return;
        }

        if (_badgeSprite == null)
            _badgeSprite = CreateBadgeSprite();

        if (_iconContainer == null)
        {
            GameObject container = new GameObject("StatusIconContainer");
            _iconContainer = container.transform;
            _iconContainer.SetParent(transform, false);
            _iconContainer.localPosition = iconOffset;
        }

        StatusEffectTooltipUI.EnsureInstance();
    }

    private void LateUpdate()
    {
        if (_character == null || _character.Stats == null)
            return;

        _iconContainer.localPosition = iconOffset;

        List<IconData> iconData = BuildIconData();
        string signature = BuildSignature(iconData);

        if (signature != _lastSignature)
        {
            RebuildIcons(iconData);
            _lastSignature = signature;
        }

        UpdateTooltip(iconData);
    }

    private void OnDisable()
    {
        if (StatusEffectTooltipUI.Instance != null)
            StatusEffectTooltipUI.Instance.HideTooltip();
    }

    private List<IconData> BuildIconData()
    {
        var list = new List<IconData>();

        if (_character.Stats.ActiveConditions != null)
        {
            foreach (var condition in _character.Stats.ActiveConditions)
            {
                CombatConditionType normalized = ConditionRules.Normalize(condition.Type);
                list.Add(new IconData
                {
                    Key = normalized.ToString(),
                    ShortLabel = GetConditionLabel(normalized),
                    Tooltip = BuildConditionTooltip(condition),
                    Color = GetConditionColor(normalized),
                    Duration = condition.RemainingRounds
                });
            }
        }

        if (_character.Stats.IsFatigued)
        {
            list.Add(new IconData
            {
                Key = "Fatigued",
                ShortLabel = "FT",
                Tooltip = "Fatigued\n-2 STR, -2 DEX\nCannot charge or run",
                Color = new Color(0.92f, 0.45f, 0.45f, 0.9f),
                Duration = -1
            });
        }

        return list;
    }

    private string BuildSignature(List<IconData> data)
    {
        if (data == null || data.Count == 0)
            return "none";

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.Count; i++)
        {
            sb.Append(data[i].Key);
            sb.Append(':');
            sb.Append(data[i].Duration);
            sb.Append('|');
        }
        return sb.ToString();
    }

    private void RebuildIcons(List<IconData> data)
    {
        for (int i = 0; i < _activeIcons.Count; i++)
        {
            if (_activeIcons[i] != null)
                Destroy(_activeIcons[i]);
        }
        _activeIcons.Clear();

        if (data == null || data.Count == 0)
            return;

        float totalWidth = data.Count * iconSize + (data.Count - 1) * iconSpacing;
        float startX = -totalWidth * 0.5f + iconSize * 0.5f;

        for (int i = 0; i < data.Count; i++)
        {
            IconData iconData = data[i];
            GameObject icon = CreateIconObject(iconData);
            icon.transform.localPosition = new Vector3(startX + i * (iconSize + iconSpacing), 0f, 0f);
            _activeIcons.Add(icon);
        }
    }

    private GameObject CreateIconObject(IconData data)
    {
        GameObject go = new GameObject($"Status_{data.Key}");
        go.transform.SetParent(_iconContainer, false);
        go.transform.localScale = new Vector3(iconSize, iconSize, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _badgeSprite;
        sr.color = data.Color;
        sr.sortingOrder = iconSortingOrder;

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        textGO.transform.localPosition = new Vector3(0f, 0f, 0f);

        TextMesh text = textGO.AddComponent<TextMesh>();
        text.text = data.ShortLabel;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.16f;
        text.fontSize = 48;
        text.color = new Color(1f, 1f, 1f, 0.96f);

        MeshRenderer textRenderer = textGO.GetComponent<MeshRenderer>();
        if (textRenderer != null)
            textRenderer.sortingOrder = iconSortingOrder + 1;

        return go;
    }

    private void UpdateTooltip(List<IconData> iconData)
    {
        if (StatusEffectTooltipUI.Instance == null)
            return;

        if (iconData == null || iconData.Count == 0 || !IsPointerOverThisCharacter())
            return;

        StringBuilder sb = new StringBuilder();
        sb.Append(_character.Stats.CharacterName);
        for (int i = 0; i < iconData.Count; i++)
        {
            sb.Append("\n• ");
            sb.Append(iconData[i].Tooltip);
        }

        StatusEffectTooltipUI.Instance.ShowTooltip(sb.ToString(), GetMouseScreenPosition());
    }

    private bool IsPointerOverThisCharacter()
    {
        Camera cam = Camera.main;
        if (cam == null || GameManager.Instance == null || GameManager.Instance.Grid == null)
            return false;

        Vector3 mouseScreen = GetMouseScreenPosition();
        Vector3 world = cam.ScreenToWorldPoint(mouseScreen);
        Vector2Int coord = SquareGridUtils.WorldToGrid(world);

        if (coord != _character.GridPosition)
            return false;

        SquareCell cell = GameManager.Instance.Grid.GetCell(coord);
        return cell != null && cell.ContainsOccupant(_character);
    }

    private static Vector3 GetMouseScreenPosition()
    {
        Vector3 mouseScreenPos = Vector3.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        mouseScreenPos = Input.mousePosition;
#endif

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
            mouseScreenPos = mouse.position.ReadValue();
#endif

        return mouseScreenPos;
    }

    private static string GetConditionLabel(CombatConditionType type)
    {
        var def = ConditionRules.GetDefinition(type);
        if (!string.IsNullOrEmpty(def.ShortLabel))
            return def.ShortLabel;

        string raw = def.DisplayName;
        if (string.IsNullOrEmpty(raw))
            raw = type.ToString();
        return raw.Length <= 2 ? raw.ToUpperInvariant() : raw.Substring(0, 2).ToUpperInvariant();
    }

    private static Color GetConditionColor(CombatConditionType type)
    {
        switch (ConditionRules.Normalize(type))
        {
            case CombatConditionType.Invisible:
                return new Color(0.38f, 0.72f, 0.95f, 0.88f);
            case CombatConditionType.Flanked:
                return new Color(1f, 0.55f, 0.2f, 0.9f);
            case CombatConditionType.Turned:
                return new Color(1f, 0.95f, 0.66f, 0.92f);
            case CombatConditionType.Prone:
            case CombatConditionType.Grappled:
            case CombatConditionType.Pinned:
            case CombatConditionType.Feinted:
            case CombatConditionType.ChargePenalty:
            case CombatConditionType.Disarmed:
            case CombatConditionType.Blinded:
            case CombatConditionType.Entangled:
            case CombatConditionType.Stunned:
            case CombatConditionType.Paralyzed:
            case CombatConditionType.Helpless:
            case CombatConditionType.Nauseated:
            case CombatConditionType.Panicked:
                return new Color(0.9f, 0.35f, 0.35f, 0.88f);
            default:
                return new Color(0.95f, 0.82f, 0.35f, 0.88f);
        }
    }

    private static string BuildConditionTooltip(StatusEffect condition)
    {
        string duration = condition.RemainingRounds < 0 ? "∞" : $"{Mathf.Max(0, condition.RemainingRounds)} rounds";
        var def = ConditionRules.GetDefinition(condition.Type);
        string source = string.IsNullOrEmpty(condition.SourceName) ? "Unknown" : condition.SourceName;

        if (!string.IsNullOrEmpty(def.Description))
            return $"{def.DisplayName}\n{def.Description}\nSource: {source}\nDuration: {duration}";

        return $"{def.DisplayName}\nSource: {source}\nDuration: {duration}";
    }

    private static Sprite CreateBadgeSprite()
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                pixels[y * size + x] = border
                    ? new Color(0f, 0f, 0f, 0.65f)
                    : Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}