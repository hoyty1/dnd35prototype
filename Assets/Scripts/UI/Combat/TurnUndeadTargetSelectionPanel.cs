using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built modal panel for Turn Undead HD-pool target selection.
/// </summary>
public class TurnUndeadTargetSelectionPanel : MonoBehaviour
{
    public sealed class TargetToggleData
    {
        public CharacterController Target;
        public Toggle Toggle;
        public Text Label;
        public Image RowImage;
        public int HdCost;
        public bool IsSelected;
        public bool IsAlreadyTurned;
        public int TurnedRoundsRemaining;
    }

    private static Sprite _cachedDefaultSprite;

    private readonly Dictionary<CharacterController, TargetToggleData> _targetData = new Dictionary<CharacterController, TargetToggleData>();

    private Text _titleText;
    private Text _hdPoolText;
    private Transform _targetListContainer;
    private Button _confirmButton;
    private Button _cancelButton;

    private int _totalHdPool;
    private int _remainingHd;
    private bool _suppressToggleEvents;

    private Action<CharacterController, bool> _onTargetToggled;
    private Action<List<CharacterController>, int, int> _onConfirm;
    private Action _onCancel;
    private Action<string> _onLogMessage;

    public static TurnUndeadTargetSelectionPanel Create(Canvas canvas)
    {
        if (canvas == null)
            return null;

        GameObject root = new GameObject("TurnUndeadSelectionPanel");
        root.transform.SetParent(canvas.transform, false);

        TurnUndeadTargetSelectionPanel panel = root.AddComponent<TurnUndeadTargetSelectionPanel>();
        panel.BuildRuntimeUI();
        return panel;
    }

    public void Initialize(
        int hdPool,
        Action<CharacterController, bool> onTargetToggled,
        Action<List<CharacterController>, int, int> onConfirm,
        Action onCancel,
        Action<string> onLogMessage)
    {
        _totalHdPool = Mathf.Max(0, hdPool);
        _remainingHd = _totalHdPool;
        _onTargetToggled = onTargetToggled;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _onLogMessage = onLogMessage;

        if (_titleText != null)
            _titleText.text = "Turn Undead - Select Targets";

        UpdateHdPoolDisplay();

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    public void AddTarget(CharacterController target, int hdCost, string effectText, bool isAlreadyTurned = false, int roundsRemaining = 0)
    {
        if (target == null || target.Stats == null || _targetListContainer == null)
            return;

        GameObject row = BuildTargetRow(_targetListContainer, out Toggle toggle, out Text label, out Image rowImage);

        string name = target.Stats.CharacterName;
        string effect = string.IsNullOrEmpty(effectText) ? "Turned" : effectText;
        int clampedHdCost = Mathf.Max(1, hdCost);

        string baseText = $"{name} ({clampedHdCost} HD) - {effect}";
        if (isAlreadyTurned)
        {
            int clampedRounds = Mathf.Max(1, roundsRemaining);
            Color statusColor = GetTurnStatusColor(clampedRounds);
            string colorHex = ColorUtility.ToHtmlStringRGB(statusColor);
            label.alignment = TextAnchor.UpperLeft;
            label.text = $"{baseText}\n<size=11><color=#{colorHex}>[ALREADY TURNED - {clampedRounds} rounds left]</color></size>";

            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout != null)
                rowLayout.preferredHeight = 52f;

            RectTransform rowRT = row.GetComponent<RectTransform>();
            if (rowRT != null)
                rowRT.sizeDelta = new Vector2(0f, 52f);

            RectTransform labelRT = label.rectTransform;
            if (labelRT != null)
            {
                labelRT.offsetMin = new Vector2(36f, 4f);
                labelRT.offsetMax = new Vector2(-8f, -4f);
            }
        }
        else
        {
            label.alignment = TextAnchor.MiddleLeft;
            label.text = baseText;
        }

        ColorBlock colors = toggle.colors;
        colors.normalColor = new Color(0.14f, 0.18f, 0.24f, 0.96f);
        colors.highlightedColor = new Color(0.2f, 0.25f, 0.34f, 1f);
        colors.pressedColor = new Color(0.23f, 0.28f, 0.4f, 1f);
        colors.selectedColor = new Color(0.17f, 0.22f, 0.3f, 1f);
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.65f);
        toggle.colors = colors;

        TargetToggleData data = new TargetToggleData
        {
            Target = target,
            Toggle = toggle,
            Label = label,
            RowImage = rowImage,
            HdCost = clampedHdCost,
            IsSelected = false,
            IsAlreadyTurned = isAlreadyTurned,
            TurnedRoundsRemaining = Mathf.Max(0, roundsRemaining),
        };

        _targetData[target] = data;

        toggle.onValueChanged.AddListener(selected =>
        {
            if (_suppressToggleEvents)
                return;

            OnTargetToggledInternal(data, rowImage, selected);
        });
    }

    public List<CharacterController> GetSelectedTargets()
    {
        List<CharacterController> selected = new List<CharacterController>();
        foreach (var kvp in _targetData)
        {
            if (kvp.Value != null && kvp.Value.IsSelected && kvp.Key != null && kvp.Key.Stats != null && !kvp.Key.Stats.IsDead)
                selected.Add(kvp.Key);
        }

        return selected;
    }

    public int GetRemainingHd()
    {
        return _remainingHd;
    }

    public int GetSpentHd()
    {
        return Mathf.Max(0, _totalHdPool - _remainingHd);
    }

    public void Close()
    {
        _onTargetToggled = null;
        _onConfirm = null;
        _onCancel = null;
        _onLogMessage = null;

        if (gameObject != null)
            Destroy(gameObject);
    }

    private static Color GetTurnStatusColor(int roundsRemaining)
    {
        if (roundsRemaining <= 2)
            return new Color(1f, 0.35f, 0.35f, 1f);

        if (roundsRemaining <= 5)
            return new Color(1f, 0.74f, 0.35f, 1f);

        return new Color(0.65f, 1f, 0.65f, 1f);
    }

    private void OnTargetToggledInternal(TargetToggleData data, Image rowImage, bool selected)
    {
        if (data == null || data.Target == null)
            return;

        if (selected)
        {
            if (_remainingHd < data.HdCost)
            {
                _suppressToggleEvents = true;
                data.Toggle.isOn = false;
                _suppressToggleEvents = false;

                _onLogMessage?.Invoke($"[Turn Undead] Not enough HD! Need {data.HdCost}, have {_remainingHd}.");
                return;
            }

            _remainingHd -= data.HdCost;
            data.IsSelected = true;
            if (rowImage != null)
                rowImage.color = new Color(0.18f, 0.42f, 0.26f, 0.96f);

            _onTargetToggled?.Invoke(data.Target, true);
        }
        else
        {
            _remainingHd += data.HdCost;
            data.IsSelected = false;
            if (rowImage != null)
                rowImage.color = new Color(0.14f, 0.18f, 0.24f, 0.96f);

            _onTargetToggled?.Invoke(data.Target, false);
        }

        UpdateHdPoolDisplay();
    }

    private void OnConfirmClicked()
    {
        List<CharacterController> selectedTargets = GetSelectedTargets();
        _onConfirm?.Invoke(selectedTargets, GetSpentHd(), GetRemainingHd());
    }

    private void OnCancelClicked()
    {
        _onCancel?.Invoke();
    }

    private void UpdateHdPoolDisplay()
    {
        if (_hdPoolText == null)
            return;

        _hdPoolText.text = $"HD Pool: {_remainingHd}/{_totalHdPool}";

        float ratio = _totalHdPool <= 0 ? 0f : (float)_remainingHd / _totalHdPool;
        if (ratio > 0.5f)
            _hdPoolText.color = new Color(0.35f, 0.95f, 0.45f, 1f);
        else if (ratio > 0.25f)
            _hdPoolText.color = new Color(0.95f, 0.9f, 0.3f, 1f);
        else
            _hdPoolText.color = new Color(1f, 0.55f, 0.18f, 1f);
    }

    private void BuildRuntimeUI()
    {
        RectTransform rootRT = gameObject.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image overlay = gameObject.AddComponent<Image>();
        overlay.sprite = GetOrCreateDefaultSprite();
        overlay.type = Image.Type.Sliced;
        overlay.color = new Color(0f, 0f, 0f, 0.72f);

        CanvasGroup canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        GameObject dialog = new GameObject("Dialog", typeof(RectTransform), typeof(Image), typeof(Outline));
        dialog.transform.SetParent(transform, false);

        RectTransform dialogRT = dialog.GetComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.22f, 0.15f);
        dialogRT.anchorMax = new Vector2(0.78f, 0.85f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogImage = dialog.GetComponent<Image>();
        dialogImage.sprite = GetOrCreateDefaultSprite();
        dialogImage.type = Image.Type.Sliced;
        dialogImage.color = new Color(0.1f, 0.14f, 0.2f, 0.98f);

        Outline outline = dialog.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.58f, 0.95f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        _titleText = CreateText(dialog.transform, "Title", 18, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 1f, 1f));
        RectTransform titleRT = _titleText.rectTransform;
        titleRT.anchorMin = new Vector2(0.05f, 0.88f);
        titleRT.anchorMax = new Vector2(0.95f, 0.98f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        _hdPoolText = CreateText(dialog.transform, "HdPool", 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        RectTransform hdRT = _hdPoolText.rectTransform;
        hdRT.anchorMin = new Vector2(0.05f, 0.79f);
        hdRT.anchorMax = new Vector2(0.95f, 0.87f);
        hdRT.offsetMin = Vector2.zero;
        hdRT.offsetMax = Vector2.zero;

        GameObject scrollObj = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObj.transform.SetParent(dialog.transform, false);
        RectTransform scrollRT = scrollObj.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.08f, 0.22f);
        scrollRT.anchorMax = new Vector2(0.92f, 0.77f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;

        Image scrollBg = scrollObj.GetComponent<Image>();
        scrollBg.sprite = GetOrCreateDefaultSprite();
        scrollBg.type = Image.Type.Sliced;
        scrollBg.color = new Color(0.05f, 0.08f, 0.12f, 0.96f);

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObj.transform.SetParent(scrollObj.transform, false);
        RectTransform viewportRT = viewportObj.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(8f, 8f);
        viewportRT.offsetMax = new Vector2(-8f, -8f);

        Image viewportImage = viewportObj.GetComponent<Image>();
        viewportImage.sprite = GetOrCreateDefaultSprite();
        viewportImage.type = Image.Type.Sliced;
        viewportImage.color = new Color(0.07f, 0.1f, 0.15f, 0.95f);

        Mask viewportMask = viewportObj.GetComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(viewportObj.transform, false);

        RectTransform contentRT = contentObj.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObj.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(6, 6, 6, 6);
        contentLayout.spacing = 6f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = contentObj.GetComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = scrollObj.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 26f;

        _targetListContainer = contentObj.transform;

        _confirmButton = BuildActionButton(dialog.transform, "ConfirmButton", "Confirm", new Vector2(0.1f, 0.08f), new Vector2(0.45f, 0.18f), new Color(0.2f, 0.45f, 0.25f, 1f));
        _cancelButton = BuildActionButton(dialog.transform, "CancelButton", "Cancel", new Vector2(0.55f, 0.08f), new Vector2(0.9f, 0.18f), new Color(0.45f, 0.2f, 0.2f, 1f));
    }

    private GameObject BuildTargetRow(Transform parent, out Toggle toggle, out Text label, out Image rowImage)
    {
        GameObject row = new GameObject("TargetRow", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        RectTransform rowRT = row.GetComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0f, 34f);

        rowImage = row.GetComponent<Image>();
        rowImage.sprite = GetOrCreateDefaultSprite();
        rowImage.type = Image.Type.Sliced;
        rowImage.color = new Color(0.14f, 0.18f, 0.24f, 0.96f);

        LayoutElement rowLE = row.GetComponent<LayoutElement>();
        rowLE.preferredHeight = 34f;

        toggle = row.GetComponent<Toggle>();
        toggle.targetGraphic = rowImage;

        GameObject checkBgObj = new GameObject("CheckBackground", typeof(RectTransform), typeof(Image));
        checkBgObj.transform.SetParent(row.transform, false);
        RectTransform checkBgRT = checkBgObj.GetComponent<RectTransform>();
        checkBgRT.anchorMin = new Vector2(0f, 0.5f);
        checkBgRT.anchorMax = new Vector2(0f, 0.5f);
        checkBgRT.pivot = new Vector2(0f, 0.5f);
        checkBgRT.anchoredPosition = new Vector2(10f, 0f);
        checkBgRT.sizeDelta = new Vector2(18f, 18f);

        Image checkBgImage = checkBgObj.GetComponent<Image>();
        checkBgImage.sprite = GetOrCreateDefaultSprite();
        checkBgImage.type = Image.Type.Sliced;
        checkBgImage.color = new Color(0.05f, 0.07f, 0.1f, 1f);

        GameObject checkmarkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkmarkObj.transform.SetParent(checkBgObj.transform, false);
        RectTransform checkmarkRT = checkmarkObj.GetComponent<RectTransform>();
        checkmarkRT.anchorMin = new Vector2(0.15f, 0.15f);
        checkmarkRT.anchorMax = new Vector2(0.85f, 0.85f);
        checkmarkRT.offsetMin = Vector2.zero;
        checkmarkRT.offsetMax = Vector2.zero;

        Image checkmarkImage = checkmarkObj.GetComponent<Image>();
        checkmarkImage.sprite = GetOrCreateDefaultSprite();
        checkmarkImage.type = Image.Type.Sliced;
        checkmarkImage.color = new Color(0.3f, 0.95f, 0.4f, 1f);

        toggle.graphic = checkmarkImage;
        toggle.isOn = false;

        label = CreateText(row.transform, "Label", 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.93f, 0.95f, 1f, 1f));
        RectTransform labelRT = label.rectTransform;
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.offsetMin = new Vector2(36f, 2f);
        labelRT.offsetMax = new Vector2(-8f, -2f);

        return row;
    }

    private Button BuildActionButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        RectTransform buttonRT = buttonObj.GetComponent<RectTransform>();
        buttonRT.anchorMin = anchorMin;
        buttonRT.anchorMax = anchorMax;
        buttonRT.offsetMin = Vector2.zero;
        buttonRT.offsetMax = Vector2.zero;

        Image buttonImage = buttonObj.GetComponent<Image>();
        buttonImage.sprite = GetOrCreateDefaultSprite();
        buttonImage.type = Image.Type.Sliced;
        buttonImage.color = color;

        Button button = buttonObj.GetComponent<Button>();

        Text label = CreateText(buttonObj.transform, "Text", 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        RectTransform labelRT = label.rectTransform;
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        label.text = text;

        return button;
    }

    private static Text CreateText(Transform parent, string name, int fontSize, FontStyle style, TextAnchor anchor, Color color)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(parent, false);

        Text text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Sprite GetOrCreateDefaultSprite()
    {
        if (_cachedDefaultSprite != null)
            return _cachedDefaultSprite;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "TurnUndeadSelection_DefaultSprite";
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply(false, true);

        _cachedDefaultSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        _cachedDefaultSprite.name = "TurnUndeadSelection_DefaultSprite";
        return _cachedDefaultSprite;
    }
}
