using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight modal selector used by Disguise Self to pick a race appearance.
/// </summary>
public class DisguiseSelfRaceSelector : MonoBehaviour
{
    private GameObject _panelRoot;

    public bool IsOpen => _panelRoot != null;

    public void Show(string casterName, SizeCategory sizeCategory, List<string> raceOptions, Action<string> onSelect, Action onCancel)
    {
        Hide();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            onCancel?.Invoke();
            return;
        }

        if (raceOptions == null || raceOptions.Count == 0)
        {
            onCancel?.Invoke();
            return;
        }

        _panelRoot = new GameObject("DisguiseSelfRaceSelector");
        _panelRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRT = _panelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image rootBg = _panelRoot.AddComponent<Image>();
        rootBg.color = new Color(0f, 0f, 0f, 0.78f);

        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(_panelRoot.transform, false);
        RectTransform dialogRT = dialog.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRT.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRT.pivot = new Vector2(0.5f, 0.5f);
        dialogRT.sizeDelta = new Vector2(460f, 520f);

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.1f, 0.12f, 0.2f, 0.98f);

        VerticalLayoutGroup layout = dialog.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        Text title = CreateText(dialog.transform, "Choose Disguise Appearance", 18, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.95f, 0.95f, 1f));
        title.GetComponent<LayoutElement>().preferredHeight = 28f;

        string bodyText = $"{casterName} can mimic any humanoid of size {sizeCategory}.";
        Text body = CreateText(dialog.transform, bodyText, 13, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.8f, 0.86f, 1f));
        body.GetComponent<LayoutElement>().preferredHeight = 24f;

        GameObject listContainer = new GameObject("RaceList");
        listContainer.transform.SetParent(dialog.transform, false);
        RectTransform listRT = listContainer.AddComponent<RectTransform>();
        listRT.sizeDelta = new Vector2(0f, 390f);
        LayoutElement listLE = listContainer.AddComponent<LayoutElement>();
        listLE.preferredHeight = 390f;

        VerticalLayoutGroup listLayout = listContainer.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 5;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandHeight = false;

        for (int i = 0; i < raceOptions.Count; i++)
        {
            string race = raceOptions[i];
            Button raceButton = CreateButton(listContainer.transform, race, new Color(0.2f, 0.36f, 0.62f, 1f));
            raceButton.onClick.AddListener(() =>
            {
                Hide();
                onSelect?.Invoke(race);
            });
        }

        Button cancelButton = CreateButton(dialog.transform, "Cancel", new Color(0.5f, 0.2f, 0.2f, 1f));
        cancelButton.onClick.AddListener(() =>
        {
            Hide();
            onCancel?.Invoke();
        });
    }

    public void Hide()
    {
        if (_panelRoot != null)
        {
            Destroy(_panelRoot);
            _panelRoot = null;
        }
    }

    private static Text CreateText(Transform parent, string content, int fontSize, TextAnchor anchor, FontStyle style, Color color)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 24f);

        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.fontStyle = style;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        go.AddComponent<LayoutElement>();
        return text;
    }

    private static Button CreateButton(Transform parent, string label, Color color)
    {
        GameObject go = new GameObject(label.Replace(" ", string.Empty) + "Button");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 32f);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 32f;

        Image image = go.AddComponent<Image>();
        image.color = color;

        Button button = go.AddComponent<Button>();
        UIFactory.ApplyEnhancedCombatButtonStyle(button, color);

        Text buttonText = CreateText(go.transform, label, 12, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
        RectTransform textRT = buttonText.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return button;
    }
}
