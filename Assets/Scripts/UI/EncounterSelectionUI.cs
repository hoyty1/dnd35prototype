using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight modal used before combat to choose an enemy encounter preset.
/// </summary>
public class EncounterSelectionUI : MonoBehaviour
{
    private GameObject _panel;
    private Text _descriptionText;
    private RectTransform _buttonContainer;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public void Open(List<EncounterPreset> presets, Action<string> onSelect, Action onCancel = null)
    {
        EnsureBuilt();
        if (_panel == null) return;

        _panel.SetActive(true);
        _descriptionText.text = "Choose an encounter preset, or launch a dedicated mechanics test (Grapple / Feint).";

        if (_buttonContainer != null)
        {
            for (int i = _buttonContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_buttonContainer.GetChild(i).gameObject);
            }

            // Dedicated quick launcher for the grappling test encounter.
            CreateActionButton("🧪 Grapple Test", new Color(0.38f, 0.16f, 0.5f, 0.98f), () =>
            {
                Close();
                onSelect?.Invoke("grapple_test");
            });

            // Dedicated quick launcher for feint/sneak-attack mechanics validation.
            CreateActionButton("🗡️ Feint & Sneak Test", new Color(0.2f, 0.24f, 0.44f, 0.98f), () =>
            {
                Close();
                onSelect?.Invoke("feint_sneak_test");
            });

            if (presets != null)
            {
                foreach (var preset in presets)
                {
                    // Avoid duplicate cards when test encounters are already exposed as quick-launch actions.
                    if (preset != null && (preset.Id == "grapple_test" || preset.Id == "feint_sneak_test"))
                        continue;

                    CreatePresetButton(preset, onSelect);
                }
            }
            CreateActionButton("Use Default", new Color(0.2f, 0.35f, 0.6f, 0.95f), () =>
            {
                string fallbackId = "goblin_raiders";
                if (presets != null)
                {
                    for (int i = 0; i < presets.Count; i++)
                    {
                        if (presets[i] != null && presets[i].Id != "grapple_test" && presets[i].Id != "feint_sneak_test")
                        {
                            fallbackId = presets[i].Id;
                            break;
                        }
                    }
                }

                Close();
                onSelect?.Invoke(fallbackId);
            });

            if (onCancel != null)
            {
                CreateActionButton("Cancel", new Color(0.35f, 0.2f, 0.2f, 0.95f), () =>
                {
                    Close();
                    onCancel.Invoke();
                });
            }
        }
    }

    public void Close()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (_panel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[EncounterSelectionUI] No Canvas found.");
            return;
        }

        _panel = new GameObject("EncounterSelectionPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)_panel.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(640f, 520f);

        var bg = _panel.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

        CreateText(_panel.transform, "Encounter Selection", 28, FontStyle.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -24f), new Vector2(580f, 40f), TextAnchor.MiddleCenter, out _);

        CreateText(_panel.transform, "", 18, FontStyle.Normal,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -64f), new Vector2(580f, 32f), TextAnchor.MiddleCenter, out _descriptionText);

        GameObject content = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(_panel.transform, false);
        _buttonContainer = content.GetComponent<RectTransform>();
        _buttonContainer.anchorMin = new Vector2(0.5f, 0.5f);
        _buttonContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _buttonContainer.pivot = new Vector2(0.5f, 0.5f);
        _buttonContainer.sizeDelta = new Vector2(580f, 380f);
        _buttonContainer.anchoredPosition = new Vector2(0f, -30f);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;

        var csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _panel.SetActive(false);
    }

    private void CreatePresetButton(EncounterPreset preset, Action<string> onSelect)
    {
        if (preset == null) return;

        GameObject btnGo = new GameObject($"Preset_{preset.Id}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnGo.transform.SetParent(_buttonContainer, false);

        var le = btnGo.GetComponent<LayoutElement>();
        le.preferredHeight = 72f;

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.18f, 0.2f, 0.28f, 0.95f);

        var btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            Close();
            onSelect?.Invoke(preset.Id);
        });

        CreateText(btnGo.transform,
            $"{preset.DisplayName}\n<size=16>{preset.Description}</size>",
            20,
            FontStyle.Bold,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(540f, 64f),
            TextAnchor.MiddleCenter,
            out _);
    }

    private void CreateActionButton(string label, Color color, Action onClick)
    {
        GameObject btnGo = new GameObject($"Action_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnGo.transform.SetParent(_buttonContainer, false);

        var le = btnGo.GetComponent<LayoutElement>();
        le.preferredHeight = 46f;

        var img = btnGo.GetComponent<Image>();
        img.color = color;

        var btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        CreateText(btnGo.transform, label, 18, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(500f, 36f), TextAnchor.MiddleCenter, out _);
    }

    private void CreateText(Transform parent, string value, int fontSize, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        TextAnchor alignment, out Text text)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        text = go.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.supportRichText = true;
    }
}
