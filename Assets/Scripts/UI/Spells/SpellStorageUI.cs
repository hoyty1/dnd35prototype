using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ════════════════════════════════════════════════════════════════════════════
//  Spell Storage UI — D&D 3.5e Sprint 3 Ring of Spell Storing Interface
//
//  Lightweight UI panel for managing spells stored in Rings of Spell Storing.
//  Displays stored spells, capacity, and provides store/cast/remove actions.
//
//  In the prototype, this is a simple list-based panel. It creates itself
//  dynamically when needed and can be shown via SpellStorageUI.Show().
// ════════════════════════════════════════════════════════════════════════════

public class SpellStorageUI : MonoBehaviour
{
    // Singleton-style quick access
    private static SpellStorageUI _instance;

    [Header("References")]
    public Text TitleLabel;
    public Text CapacityLabel;
    public Transform SpellListContainer;
    public Button CloseButton;

    private ItemData _currentRing;
    private CharacterController _currentWearer;

    void Awake()
    {
        _instance = this;
        if (CloseButton != null)
            CloseButton.onClick.AddListener(Hide);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── Public static API ──

    /// <summary>
    /// Show the spell storage panel for a given ring and wearer.
    /// If the panel doesn't exist, logs the storage info to combat log instead.
    /// </summary>
    public static void ShowForRing(ItemData ring, CharacterController wearer)
    {
        if (ring == null || ring.MaxStoredSpellLevels <= 0) return;

        if (_instance != null)
        {
            _instance._currentRing = ring;
            _instance._currentWearer = wearer;
            _instance.gameObject.SetActive(true);
            _instance.RefreshDisplay();
        }
        else
        {
            // Fallback: log to combat log if UI isn't instantiated
            string display = SpellStorageManager.GetStorageDisplayString(ring);
            string wearerName = wearer?.Stats?.CharacterName ?? "Unknown";
            string msg = $"💍 {wearerName}'s Ring of Spell Storing:\n{display}";
            Debug.Log($"[SpellStorageUI] {msg}");
            if (GameManager.Instance != null)
                GameManager.Instance.CombatUI?.ShowCombatLog(msg);
        }
    }

    /// <summary>
    /// Hide the spell storage panel.
    /// </summary>
    public static void HidePanel()
    {
        if (_instance != null)
            _instance.Hide();
    }

    // ── Instance methods ──

    public void Hide()
    {
        gameObject.SetActive(false);
        _currentRing = null;
        _currentWearer = null;
    }

    public void RefreshDisplay()
    {
        if (_currentRing == null) return;

        // Update title
        string ringName = _currentRing.MaxStoredSpellLevels <= 3 ? "Ring of Spell Storing, Minor" : "Ring of Spell Storing, Major";
        if (TitleLabel != null)
            TitleLabel.text = ringName;

        // Update capacity
        int used = SpellStorageManager.GetUsedSpellLevels(_currentRing);
        if (CapacityLabel != null)
            CapacityLabel.text = $"Capacity: {used} / {_currentRing.MaxStoredSpellLevels} spell levels";

        // Rebuild spell list
        if (SpellListContainer != null)
        {
            // Clear existing entries
            foreach (Transform child in SpellListContainer)
                Destroy(child.gameObject);

            // Add stored spell entries
            if (_currentRing.StoredSpells != null)
            {
                for (int i = 0; i < _currentRing.StoredSpells.Count; i++)
                {
                    int index = i; // Capture for closure
                    StoredSpell spell = _currentRing.StoredSpells[i];
                    CreateSpellEntry(spell, index);
                }
            }

            // Add "Store New Spell" button if there's capacity
            if (SpellStorageManager.GetAvailableSpellLevels(_currentRing) > 0)
            {
                CreateStoreButton();
            }
        }
    }

    private void CreateSpellEntry(StoredSpell spell, int index)
    {
        if (SpellListContainer == null) return;

        var entryObj = new GameObject($"StoredSpell_{index}");
        entryObj.transform.SetParent(SpellListContainer, false);

        var layout = entryObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        // Spell info label
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(entryObj.transform, false);
        var label = labelObj.AddComponent<Text>();
        label.text = $"[{index + 1}] {spell}";
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 14;
        label.color = Color.white;

        // Cast button
        CreateActionButton(entryObj.transform, "Cast", () => OnCastSpell(index));

        // Remove button
        CreateActionButton(entryObj.transform, "Remove", () => OnRemoveSpell(index));
    }

    private void CreateStoreButton()
    {
        if (SpellListContainer == null) return;

        var btnObj = new GameObject("StoreNewSpell");
        btnObj.transform.SetParent(SpellListContainer, false);

        var label = btnObj.AddComponent<Text>();
        int available = SpellStorageManager.GetAvailableSpellLevels(_currentRing);
        label.text = $"[Store New Spell] ({available} levels available)";
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 14;
        label.color = new Color(0.5f, 1f, 0.5f);

        // Note: Full spell selection UI would open a spell picker here.
        // For prototype, we log what's needed.
    }

    private void CreateActionButton(Transform parent, string text, UnityEngine.Events.UnityAction action)
    {
        var btnObj = new GameObject($"Btn_{text}");
        btnObj.transform.SetParent(parent, false);

        var btn = btnObj.AddComponent<Button>();
        var label = btnObj.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 12;
        label.color = Color.yellow;

        btn.onClick.AddListener(action);
    }

    private void OnCastSpell(int index)
    {
        if (_currentRing == null || _currentWearer == null) return;

        SpellStorageManager.CastStoredSpell(_currentRing, index, _currentWearer, null, out string msg);
        RefreshDisplay();
    }

    private void OnRemoveSpell(int index)
    {
        if (_currentRing == null) return;

        SpellStorageManager.RemoveStoredSpell(_currentRing, index);
        RefreshDisplay();
    }
}
