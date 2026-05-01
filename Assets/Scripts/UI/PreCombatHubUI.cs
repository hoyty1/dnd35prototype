using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// High-level pre-combat hub menu that lets players choose between
/// stash management, store, spell preparation, or immediately starting combat.
///
/// FULLSCREEN UI TEST CHECKLIST:
/// ✓ Character Creation - Fullscreen
/// ✓ Store - Fullscreen
/// ✓ Stash - Fullscreen
/// ✓ Pre-combat Hub - Fullscreen
/// ✓ Encounter Selection - Fullscreen
/// ✓ Character Stats - Fullscreen
/// ✓ Spell Prep - Fullscreen
/// ✓ Level Up - Fullscreen
/// ✓ Loot - Fullscreen
/// ✓ XP Display - Fullscreen
/// ✗ Combat Actions/Log/Grid - unchanged (excluded)
/// </summary>
public class PreCombatHubUI : MonoBehaviour
{
    private GameObject _root;
    private Text _subtitleText;

    private Action _onOpenStore;
    private Action _onOpenInventory;
    private Action _onOpenSpellPreparation;
    private Action _onStartEncounter;
    private Action _onBackToEncounterSelection;

    public bool IsOpen => _root != null && _root.activeSelf;

    public void Open(
        Action onOpenStore,
        Action onOpenInventory,
        Action onOpenSpellPreparation,
        Action onStartEncounter,
        Action onBackToEncounterSelection)
    {
        EnsureBuilt();
        if (_root == null)
            return;

        _onOpenStore = onOpenStore;
        _onOpenInventory = onOpenInventory;
        _onOpenSpellPreparation = onOpenSpellPreparation;
        _onStartEncounter = onStartEncounter;
        _onBackToEncounterSelection = onBackToEncounterSelection;

        _subtitleText.text = "Choose your final preparations before battle.";
        _root.transform.SetAsLastSibling();
        _root.SetActive(true);

        Debug.Log("[PreCombatHub] Hub opened.");
    }

    public void ShowMenu()
    {
        if (_root == null)
            return;

        _root.transform.SetAsLastSibling();
        _root.SetActive(true);
    }

    public void HideMenu()
    {
        if (_root == null)
            return;

        _root.SetActive(false);
    }

    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);

        _onOpenStore = null;
        _onOpenInventory = null;
        _onOpenSpellPreparation = null;
        _onStartEncounter = null;
        _onBackToEncounterSelection = null;
    }

    private void EnsureBuilt()
    {
        if (_root != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[PreCombatHub] Cannot build UI because no Canvas was found.");
            return;
        }

        _root = CreatePanel(canvas.transform, "PreCombatHubRoot",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.08f, 0.09f, 0.14f, 0.97f));

        CreateText(_root.transform, "Title", "PRE-COMBAT PREPARATION",
            new Vector2(0.1f, 0.93f), new Vector2(0.9f, 0.99f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 40, FontStyle.Bold,
            new Color(0.97f, 0.87f, 0.45f, 1f), TextAnchor.MiddleCenter);

        _subtitleText = CreateText(_root.transform, "Subtitle", string.Empty,
            new Vector2(0.12f, 0.88f), new Vector2(0.88f, 0.93f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 20, FontStyle.Italic,
            new Color(0.8f, 0.87f, 1f, 1f), TextAnchor.MiddleCenter);

        float firstButtonY = 180f;
        float step = 94f;

        CreateButton(_root.transform, "InventoryButton", "📦 Manage Inventory (Stash)", new Vector2(0f, firstButtonY), new Color(0.23f, 0.38f, 0.63f, 1f), () => _onOpenInventory?.Invoke());
        CreateButton(_root.transform, "StoreButton", "🏪 Open Store", new Vector2(0f, firstButtonY - step), new Color(0.2f, 0.48f, 0.3f, 1f), () => _onOpenStore?.Invoke());
        CreateButton(_root.transform, "SpellPrepButton", "🔮 Prepare Spells", new Vector2(0f, firstButtonY - (step * 2f)), new Color(0.37f, 0.3f, 0.62f, 1f), () => _onOpenSpellPreparation?.Invoke());

        CreateButton(_root.transform, "StartButton", "⚔ Start Encounter", new Vector2(0f, firstButtonY - (step * 3f)), new Color(0.19f, 0.58f, 0.27f, 1f), () => _onStartEncounter?.Invoke());
        CreateButton(_root.transform, "BackButton", "← Back to Encounter Selection", new Vector2(0f, firstButtonY - (step * 4f)), new Color(0.55f, 0.23f, 0.23f, 1f), () => _onBackToEncounterSelection?.Invoke());

        Debug.Log("[PreCombatHub] Panel: (0,0) to (1,1) - FULLSCREEN");
        _root.SetActive(false);
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        image.color = color;

        return panel;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Text text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.text = value;

        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Color color, Action onClick)
    {
        GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(620f, 74f);

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(buttonObj.transform, "Label", label,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        return button;
    }
}
