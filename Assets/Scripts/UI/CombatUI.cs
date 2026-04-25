using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Manages all combat UI elements with D&D 3.5 ability score display.
/// Shows 6 ability scores with modifiers and derived stats for each character.
/// Supports 4 PC panels, multiple NPC panels, initiative order display, and character icons.
/// Includes action economy buttons (Move, 5-Foot Step, Drop Prone, Stand Up, Crawl, Attack, Full Attack, Dual Wield, End Turn).
/// Includes AoO warning confirmation dialog.
/// </summary>
public class CombatUI : MonoBehaviour
{
    [Header("Turn Indicator")]
    public Text TurnIndicatorText;

    [Header("Initiative Display")]
    public Text InitiativeOrderText;
    public GameObject InitiativePanel;

    [Header("PC1 Stats")]
    public Text PC1NameText;
    public Text PC1HPText;
    public Text PC1ACText;
    public Text PC1AtkText;
    public Text PC1SpeedText;
    public Text PC1AbilityText;
    public Image PC1HPBar;
    public GameObject PC1Panel;
    public Image PC1Icon;

    [Header("PC2 Stats")]
    public Text PC2NameText;
    public Text PC2HPText;
    public Text PC2ACText;
    public Text PC2AtkText;
    public Text PC2SpeedText;
    public Text PC2AbilityText;
    public Image PC2HPBar;
    public GameObject PC2Panel;
    public Image PC2Icon;

    [Header("PC3 Stats")]
    public Text PC3NameText;
    public Text PC3HPText;
    public Text PC3ACText;
    public Text PC3AtkText;
    public Text PC3SpeedText;
    public Text PC3AbilityText;
    public Image PC3HPBar;
    public GameObject PC3Panel;
    public Image PC3Icon;

    [Header("PC4 Stats")]
    public Text PC4NameText;
    public Text PC4HPText;
    public Text PC4ACText;
    public Text PC4AtkText;
    public Text PC4SpeedText;
    public Text PC4AbilityText;
    public Image PC4HPBar;
    public GameObject PC4Panel;
    public Image PC4Icon;

    [Header("Buff Display")]
    public Text PC1BuffText;
    public Text PC2BuffText;
    public Text PC3BuffText;
    public Text PC4BuffText;

    [Header("NPC Stats")]
    public Text NPCNameText;
    public Text NPCHPText;
    public Text NPCACText;
    public Text NPCAtkText;
    public Text NPCSpeedText;
    public Text NPCAbilityText;
    public Image NPCHPBar;

    /// <summary>UI data for each NPC stat panel (supports multiple enemies).</summary>
    public List<NPCPanelUI> NPCPanels = new List<NPCPanelUI>();

    [Header("Combat Log")]
    public Text CombatLogText;  // Legacy single-text reference (kept for backward compat)
    public GameObject CombatLogContent;     // Content container for scrollable log messages
    public ScrollRect CombatLogScrollRect;  // ScrollRect for auto-scrolling

    [Header("Action Buttons - Action Economy")]
    public GameObject ActionPanel;
    public Button MoveButton;
    public Button WithdrawButton;       // Withdraw (Full-Round Action, first square no AoO)
    public Button FiveFootStepButton;   // 5-foot step (Free Action, no AoO)
    public Button DropProneButton;      // Drop prone (Free Action)
    public Button StandUpButton;        // Stand up (Move Action, provokes AoO)
    public Button CrawlButton;          // Crawl 5 ft (Move Action, provokes AoO)
    public Button AttackButton;         // Single attack (Standard Action)
    public Button AttackThrownButton;          // Thrown attack (Standard, or sequence continuation for throwable melee)
    public Button AttackOffHandButton;         // Off-hand attack (once/turn; can be used in iterative sequence)
    public Button AttackOffHandThrownButton;   // Off-hand thrown attack (once/turn; can be used in iterative sequence)
    public Button FullAttackButton;     // Full Attack (Full-Round Action)
    public Button SpecialAttackButton;  // Combat maneuvers (Standard Action)
    public Button TurnUndeadButton;     // Turn Undead (Standard Action, Cleric/Paladin only)
    public Button SmiteButton;          // Template Smite (Standard Action, 1/day)
    public Button GrappleActionsButton; // Legacy grapple menu button (deprecated)
    public Button GrappleDamageButton;          // Grapple: deal damage (Standard Action)
    public Button GrappleLightWeaponAttackButton; // Grapple: attack with main-hand light weapon (Standard Action)
    public Button GrappleUnarmedAttackButton;     // Grapple: attack unarmed strike (Standard Action)
    public Button GrapplePinButton;             // Grapple: pin/maintain pin (Standard Action)
    public Button GrappleBreakPinButton;        // Grapple: break pin (Standard Action)
    public Button GrappleEscapeArtistButton;    // Grapple: escape via Escape Artist (Standard Action)
    public Button GrappleEscapeCheckButton;     // Grapple: escape via opposed grapple (iterative grapple attack)
    public Button GrappleMoveButton;            // Grapple: move while grappling (Standard Action)
    public Button GrappleUseOpponentWeaponButton; // Grapple: use opponent weapon (Standard Action)
    public Button GrappleDisarmSmallObjectButton; // Grapple: disarm small object (Stub)
    public Button GrappleReleasePinnedButton;   // Grapple: release pinned opponent (Free Action)
    public Button AidAnotherButton;     // Aid Another (Standard Action)
    public Button OverrunButton;        // Overrun (Standard Action)
    public Button ChargeButton;         // Charge (Full-Round Action)
    public Button DualWieldButton;      // Dual Wield (Full-Round Action)
    public Button EndTurnButton;
    public Button ReloadButton;            // Reload equipped crossbow (action varies by weapon/feat)
    public Button DropEquippedItemButton;  // Drop currently held equipped item (Free Action)
    public Button PickUpItemButton;        // Pick up item from current or adjacent square (Move Action, provokes AoO)
    public Button DamageModeToggleButton;  // Toggle attack damage mode (Lethal/Nonlethal)
    public Text ActionStatusText;          // Shows current action economy status

    [Header("Monk/Barbarian Buttons")]
    public Button FlurryOfBlowsButton;     // Flurry of Blows (Full-Round Action, Monk only)
    public Button RageButton;              // Barbarian Rage (Free Action)
    public Text RageStatusText;            // Shows rage/fatigue status

    [Header("Spellcasting")]
    public Button CastSpellButton;         // Cast Spell (Standard Action)
    public Button DischargeTouchButton;    // Deliver currently held touch charge (Free Action)
    public Text SpellSlotsText;            // Shows remaining spell slots
    [Header("Feat Controls")]
    public GameObject PowerAttackPanel;     // Panel containing Power Attack slider
    public Slider PowerAttackSlider;        // Slider for Power Attack value (0 to BAB)
    public Text PowerAttackLabel;           // Shows "Power Attack: -X attack / +Y damage"
    public GameObject RapidShotPanel;       // Panel containing Rapid Shot toggle
    public Button RapidShotToggle;          // Toggle button for Rapid Shot
    public Text RapidShotLabel;             // Shows "Rapid Shot: ON/OFF"
    public Button AttackDefensivelyButton;    // Single attack (standard) while fighting defensively
    public Button FullAttackDefensivelyButton; // Full attack while fighting defensively

    [Header("Layout Panels")]
    public GameObject PartyPanelGO;          // Left-side party panel container
    public GameObject CombatDataPanelGO;     // Bottom combat data panel container

    [Header("Floating Windows")]
    public RectTransform CombatLogWindowRect;
    public RectTransform ActionWindowRect;
    public ResizableWindow CombatLogResizable;
    public ResizableWindow ActionWindowResizable;
    public Button ResetUILayoutButton;
    public Vector2 DefaultCombatLogWindowPosition;
    public Vector2 DefaultCombatLogWindowSize;
    public Vector2 DefaultActionWindowPosition;
    public Vector2 DefaultActionWindowSize;

    // Active-PC indicator images on the panels
    public Image PC1ActiveIndicator;
    public Image PC2ActiveIndicator;
    public Image PC3ActiveIndicator;
    public Image PC4ActiveIndicator;

    // Legacy aliases for compatibility
    public Text PCNameText { get => PC1NameText; set => PC1NameText = value; }
    public Text PCHPText { get => PC1HPText; set => PC1HPText = value; }
    public Text PCACText { get => PC1ACText; set => PC1ACText = value; }
    public Image PCHPBar { get => PC1HPBar; set => PC1HPBar = value; }

    /// <summary>
    /// Update stat displays for both PCs and all NPCs.
    /// Supports the new multi-NPC system while maintaining backward compatibility.
    /// </summary>
    public void UpdateAllStats(CharacterController pc1, CharacterController pc2, CharacterController npc)
    {
        EnsureCharacterInfoPanel();
        _characterInfoPanel?.UpdateAllStats(pc1, pc2, npc);
    }

    /// <summary>
    /// Update stat displays for all 4 PCs and multiple NPCs.
    /// </summary>
    public void UpdateAllStats4PC(List<CharacterController> pcs, List<CharacterController> npcs)
    {
        EnsureCharacterInfoPanel();
        _characterInfoPanel?.UpdateAllStats4PC(pcs, npcs);
    }

    /// <summary>
    /// Update stat displays for both PCs and multiple NPCs (legacy 2-PC version).
    /// </summary>
    public void UpdateAllStatsMultiNPC(CharacterController pc1, CharacterController pc2, List<CharacterController> npcs)
    {
        EnsureCharacterInfoPanel();
        _characterInfoPanel?.UpdateAllStatsMultiNPC(pc1, pc2, npcs);
    }

    /// <summary>
    /// Legacy 2-param overload for backward compatibility.
    /// </summary>
    public void UpdateStats(CharacterController pc, CharacterController npc)
    {
        UpdateAllStats(pc, null, npc);
    }

    public void SetTurnIndicator(string message)
    {
        if (TurnIndicatorText != null)
            TurnIndicatorText.text = message;
    }

    /// <summary>
    /// Build a standardized short flanking indicator for turn/targeting text.
    /// </summary>
    public string BuildFlankingIndicator(bool isFlanking, CharacterController flankingAlly)
    {
        EnsureTargetSelectionPanel();
        return _targetSelectionPanel != null
            ? _targetSelectionPanel.BuildFlankingIndicator(isFlanking, flankingAlly)
            : string.Empty;
    }

    public void SetCurrentTarget(CharacterController target)
    {
        EnsureTargetSelectionPanel();
        _targetSelectionPanel?.SetTarget(target);
    }

    public CharacterController GetCurrentTarget()
    {
        EnsureTargetSelectionPanel();
        return _targetSelectionPanel != null ? _targetSelectionPanel.GetCurrentTarget() : null;
    }

    public void ClearCurrentTarget()
    {
        EnsureTargetSelectionPanel();
        _targetSelectionPanel?.ClearTarget();
    }

    /// <summary>
    /// Update the initiative order display text.
    /// </summary>
    public void UpdateInitiativeDisplay(string initiativeText)
    {
        EnsureInitiativePanel();
        _initiativePanel?.UpdateInitiativeDisplay(initiativeText);
    }

    /// <summary>
    /// Adds a message to the persistent scrollable combat log.
    /// Messages accumulate throughout combat and can be scrolled.
    /// </summary>
    public void ShowCombatLog(string message)
    {
        EnsureCombatLogPanel();
        _combatLogPanel?.AddMessage(message);
    }

    /// <summary>
    /// Adds a visual turn separator line to the combat log.
    /// </summary>
    public void AddTurnSeparator(int turnNumber)
    {
        EnsureCombatLogPanel();
        _combatLogPanel?.AddTurnSeparator(turnNumber);
    }

    /// <summary>
    /// Clears all messages from the combat log. Call when combat ends.
    /// </summary>
    public void ClearCombatLog()
    {
        EnsureCombatLogPanel();
        _combatLogPanel?.ClearLog();
    }

    public void AddCombatMessage(string message, MessageType type = MessageType.Normal)
    {
        EnsureCombatLogPanel();
        _combatLogPanel?.AddMessage(message, type);
    }

    public void LogAttack(string attacker, string target, int roll, int mod, int total, int ac, bool hit)
    {
        EnsureCombatLogPanel();
        _combatLogPanel?.LogAttack(attacker, target, roll, mod, total, ac, hit);
    }

    public void LogDamage(string target, int damage, string damageType = "")
    {
        EnsureCombatLogPanel();
        _combatLogPanel?.LogDamage(target, damage, damageType);
    }

    public string ExportCombatLog()
    {
        EnsureCombatLogPanel();
        return _combatLogPanel != null ? _combatLogPanel.ExportLog() : string.Empty;
    }

    public void SetActionButtonsVisible(bool visible)
    {
        if (ActionPanel != null)
            ActionPanel.SetActive(visible);
        if (!visible)
        {
            if (PowerAttackPanel != null) PowerAttackPanel.SetActive(false);
            if (RapidShotPanel != null) RapidShotPanel.SetActive(false);
            if (RageStatusText != null) RageStatusText.gameObject.SetActive(false);
            if (DischargeTouchButton != null) DischargeTouchButton.gameObject.SetActive(false);
            HideSpecialAttackMenu();
            HideSpecialStyleSelectionMenu();
            HideBullRushExtraPushChoice();
            HidePickUpItemSelection();
            HideDropEquippedItemSelection();
            ResetDamageModeToggleVisual();
        }
    }

    public void ResetFloatingWindowLayout()
    {
        DeleteWindowPrefs("ui_window_combat_log");
        DeleteWindowPrefs("ui_window_action_panel");

        if (CombatLogResizable != null)
            CombatLogResizable.ApplyWindowState(DefaultCombatLogWindowPosition, DefaultCombatLogWindowSize, saveToPlayerPrefs: true);
        else if (CombatLogWindowRect != null)
        {
            CombatLogWindowRect.sizeDelta = DefaultCombatLogWindowSize;
            CombatLogWindowRect.anchoredPosition = DefaultCombatLogWindowPosition;
        }

        if (ActionWindowResizable != null)
            ActionWindowResizable.ApplyWindowState(DefaultActionWindowPosition, DefaultActionWindowSize, saveToPlayerPrefs: true);
        else if (ActionWindowRect != null)
        {
            ActionWindowRect.sizeDelta = DefaultActionWindowSize;
            ActionWindowRect.anchoredPosition = DefaultActionWindowPosition;
        }

        PlayerPrefs.Save();
        ShowCombatLog("UI layout reset to default window positions and sizes.");
    }

    private static void DeleteWindowPrefs(string persistenceKey)
    {
        if (string.IsNullOrEmpty(persistenceKey))
            return;

        PlayerPrefs.DeleteKey(persistenceKey + "_x");
        PlayerPrefs.DeleteKey(persistenceKey + "_y");
        PlayerPrefs.DeleteKey(persistenceKey + "_w");
        PlayerPrefs.DeleteKey(persistenceKey + "_h");
    }

    private GameObject _touchSpellPromptPanel;
    private GameObject _specialAttackPanel;
    private GameObject _specialStyleSelectionPanel;
    private GameObject _summonSelectionPanel;
    private GameObject _disarmWeaponSelectionPanel;
    private GameObject _pickUpItemSelectionPanel;
    private GameObject _dropEquippedItemSelectionPanel;
    private bool _dischargeTouchHooked;
    private GameObject _summonContextMenuRoot;
    private bool _specialStyleMenuWasActiveLastFrame;

    private GameObject _confirmationPanel;
    private ActionButtonPanel _actionButtonPanel;
    private CombatLogPanel _combatLogPanel;
    private CharacterInfoPanel _characterInfoPanel;
    private TargetSelectionPanel _targetSelectionPanel;
    private InitiativePanel _initiativePanel;

    private void LogSpecialStyleMenuLifecycle(string eventName, string details = null)
    {
        string suffix = string.IsNullOrEmpty(details) ? string.Empty : $" | {details}";
        Debug.Log($"[CombatUI][SpecialStyleMenu] {eventName}{suffix}\nStackTrace:\n{System.Environment.StackTrace}");
    }

    private void Update()
    {
        bool isActive = _specialStyleSelectionPanel != null && _specialStyleSelectionPanel.activeSelf;
        if (_specialStyleMenuWasActiveLastFrame && !isActive)
        {
            LogSpecialStyleMenuLifecycle(
                "MENU_DISAPPEARED_BETWEEN_FRAMES",
                $"frame={Time.frameCount}");
        }

        _specialStyleMenuWasActiveLastFrame = isActive;
        HandleBullRushExtraPushKeyboardInput();
    }

    private void HandleBullRushExtraPushKeyboardInput()
    {
        if (_bullRushExtraPushPanel == null || !_bullRushExtraPushPanel.activeSelf)
            return;

        // Escape defaults to 0 extra squares.
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBullRushExtraPushSelected(0);
            return;
        }

        for (int i = 0; i <= 9; i++)
        {
            KeyCode alphaKey = (KeyCode)((int)KeyCode.Alpha0 + i);
            KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad0 + i);
            if ((Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey)) && i <= _bullRushExtraPushMaxExtraSquares)
            {
                OnBullRushExtraPushSelected(i);
                return;
            }
        }
#endif
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                OnBullRushExtraPushSelected(0);
                return;
            }

            for (int i = 0; i <= 9; i++)
            {
                if (i > _bullRushExtraPushMaxExtraSquares)
                    continue;

                bool pressed = false;
                switch (i)
                {
                    case 0: pressed = keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame; break;
                    case 1: pressed = keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame; break;
                    case 2: pressed = keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame; break;
                    case 3: pressed = keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame; break;
                    case 4: pressed = keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame; break;
                    case 5: pressed = keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame; break;
                    case 6: pressed = keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame; break;
                    case 7: pressed = keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame; break;
                    case 8: pressed = keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame; break;
                    case 9: pressed = keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame; break;
                }

                if (pressed)
                {
                    OnBullRushExtraPushSelected(i);
                    return;
                }
            }
        }
#endif
    }
    private void EnsureDischargeTouchButtonExists()
    {
        if (DischargeTouchButton == null)
        {
            if (ActionPanel == null || CastSpellButton == null) return;

            GameObject cloned = Instantiate(CastSpellButton.gameObject, CastSpellButton.transform.parent);
            cloned.name = "DischargeTouchButton";
            DischargeTouchButton = cloned.GetComponent<Button>();

            if (DischargeTouchButton == null) return;

            Text txt = DischargeTouchButton.GetComponentInChildren<Text>();
            if (txt != null) txt.text = "Discharge Touch";

            var img = DischargeTouchButton.GetComponent<Image>();
            if (img != null)
                img.color = new Color(0.35f, 0.2f, 0.55f, 1f);
        }

        if (!_dischargeTouchHooked && DischargeTouchButton != null)
        {
            DischargeTouchButton.onClick.RemoveAllListeners();
            DischargeTouchButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.OnDischargeHeldTouchButtonPressed();
            });
            _dischargeTouchHooked = true;
        }
    }
    // ========== ACTION ECONOMY UI ==========

    private bool HideStandaloneDisarmButtonsInMainActions()
    {
        if (ActionPanel == null)
            return false;

        bool standaloneDisarmWasVisible = false;
        Button[] allButtons = ActionPanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in allButtons)
        {
            if (button == null)
                continue;

            bool isLikelyDisarmButton = button.name == "Disarm"
                || button.name == "DisarmBtn"
                || button.name == "DisarmButton"
                || button.name.Contains("StandaloneDisarm");
            if (!isLikelyDisarmButton)
                continue;

            bool isSpecialAttackMenuDisarm = _specialAttackPanel != null && button.transform.IsChildOf(_specialAttackPanel.transform);
            if (isSpecialAttackMenuDisarm)
                continue;

            if (button.gameObject.activeSelf || button.interactable)
            {
                standaloneDisarmWasVisible = true;
                Debug.LogWarning($"[CombatUI][Actions] Suppressing standalone Disarm button '{button.name}' from main actions. Disarm must be chosen via Special Attack menu.");
            }

            button.gameObject.SetActive(false);
            button.interactable = false;
        }

        return standaloneDisarmWasVisible;
    }

    private void EnsureCombatLogPanel()
    {
        if (_combatLogPanel == null)
            _combatLogPanel = GetComponent<CombatLogPanel>();

        if (_combatLogPanel == null)
            _combatLogPanel = gameObject.AddComponent<CombatLogPanel>();

        _combatLogPanel.Initialize(this, CombatLogText, CombatLogContent, CombatLogScrollRect);
    }

    private void EnsureActionButtonPanel()
    {
        if (_actionButtonPanel == null)
            _actionButtonPanel = GetComponent<ActionButtonPanel>();

        if (_actionButtonPanel == null)
            _actionButtonPanel = gameObject.AddComponent<ActionButtonPanel>();

        _actionButtonPanel.Initialize(this);
    }

    private void EnsureCharacterInfoPanel()
    {
        if (_characterInfoPanel == null)
            _characterInfoPanel = GetComponent<CharacterInfoPanel>();

        if (_characterInfoPanel == null)
            _characterInfoPanel = gameObject.AddComponent<CharacterInfoPanel>();

        _characterInfoPanel.Initialize(this);
    }

    private void EnsureTargetSelectionPanel()
    {
        if (_targetSelectionPanel == null)
            _targetSelectionPanel = GetComponent<TargetSelectionPanel>();

        if (_targetSelectionPanel == null)
            _targetSelectionPanel = gameObject.AddComponent<TargetSelectionPanel>();

        _targetSelectionPanel.Initialize(this);
    }

    private void EnsureInitiativePanel()
    {
        if (_initiativePanel == null)
            _initiativePanel = GetComponent<InitiativePanel>();

        if (_initiativePanel == null)
            _initiativePanel = gameObject.AddComponent<InitiativePanel>();

        _initiativePanel.Initialize(this);
    }

    internal bool HideStandaloneDisarmButtonsInMainActionsForActionPanel()
    {
        return HideStandaloneDisarmButtonsInMainActions();
    }

    internal void EnsureDischargeTouchButtonExistsForActionPanel()
    {
        EnsureDischargeTouchButtonExists();
    }

    /// <summary>
    /// Update the action panel buttons based on the current action economy state.
    /// </summary>
    public void UpdateActionButtons(CharacterController pc)
    {
        EnsureActionButtonPanel();
        _actionButtonPanel.UpdateActionButtons(pc);
    }

    public void RefreshActionButtons()
    {
        EnsureActionButtonPanel();
        _actionButtonPanel.RefreshActionButtons();
    }

    public void OnGrappleActionsClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowGrappleActionMenu();
    }

    public void ResetDamageModeToggleVisual()
    {
        if (DamageModeToggleButton == null)
            return;

        DamageModeToggleButton.gameObject.SetActive(false);
        DamageModeToggleButton.interactable = false;

        Text label = DamageModeToggleButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = "Damage: Lethal";

        Image buttonImage = DamageModeToggleButton.GetComponent<Image>();
        if (buttonImage != null)
            buttonImage.color = new Color(0.65f, 0.2f, 0.2f, 1f);
    }

    public void UpdateDamageModeToggle(CharacterController pc)
    {
        if (DamageModeToggleButton == null)
            return;

        if (pc == null)
        {
            ResetDamageModeToggleVisual();
            return;
        }

        bool isNonlethal = pc.CurrentAttackDamageMode == AttackDamageMode.Nonlethal;
        DamageModeToggleButton.gameObject.SetActive(true);
        DamageModeToggleButton.interactable = true;

        Text label = DamageModeToggleButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = isNonlethal ? "Damage: Nonlethal" : "Damage: Lethal";

        Image buttonImage = DamageModeToggleButton.GetComponent<Image>();
        if (buttonImage != null)
            buttonImage.color = isNonlethal
                ? new Color(0.25f, 0.45f, 0.75f, 1f)
                : new Color(0.65f, 0.2f, 0.2f, 1f);
    }

    // ========== FEAT UI ==========

    public void UpdateFeatControls(CharacterController pc)
    {
        if (pc == null || pc.Stats == null) return;

        bool hasPowerAttack = pc.Stats.HasFeat("Power Attack");
        if (PowerAttackPanel != null)
        {
            PowerAttackPanel.SetActive(hasPowerAttack);
            if (hasPowerAttack && PowerAttackSlider != null)
            {
                int maxPA = Mathf.Max(1, pc.Stats.BaseAttackBonus);
                PowerAttackSlider.minValue = 0;
                PowerAttackSlider.maxValue = maxPA;
                PowerAttackSlider.wholeNumbers = true;
                PowerAttackSlider.value = pc.PowerAttackValue;
                UpdatePowerAttackLabel(pc);
            }
        }

        bool hasRapidShot = pc.Stats.HasFeat("Rapid Shot");
        if (RapidShotPanel != null)
        {
            RapidShotPanel.SetActive(hasRapidShot);
            if (hasRapidShot) UpdateRapidShotLabel(pc);
        }
    }

    public void UpdatePowerAttackLabel(CharacterController pc)
    {
        if (PowerAttackLabel == null || pc == null) return;
        int val = pc.PowerAttackValue;
        if (val == 0) { PowerAttackLabel.text = "Power Attack: OFF"; return; }
        ItemData weapon = pc.GetEquippedMainWeapon();
        bool twoHanded = CharacterController.IsWeaponTwoHanded(weapon);
        int dmgBonus = twoHanded ? val * 2 : val;
        string thStr = twoHanded ? " (2H)" : "";
        PowerAttackLabel.text = $"Power Attack: -{val} atk / +{dmgBonus} dmg{thStr}";
    }

    public void UpdateRapidShotLabel(CharacterController pc)
    {
        if (RapidShotLabel == null || pc == null) return;
        if (pc.RapidShotEnabled)
        {
            bool weaponIsRanged = pc.IsEquippedWeaponRanged();
            RapidShotLabel.text = weaponIsRanged
                ? "Rapid Shot: ON (Extra atk, -2 all)"
                : "Rapid Shot: ON (Equip ranged weapon!)";
            if (RapidShotToggle != null)
            {
                var colors = RapidShotToggle.colors;
                colors.normalColor = new Color(0.2f, 0.6f, 0.2f, 1f);
                RapidShotToggle.colors = colors;
            }
        }
        else
        {
            RapidShotLabel.text = "Rapid Shot: OFF";
            if (RapidShotToggle != null)
            {
                var colors = RapidShotToggle.colors;
                colors.normalColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                RapidShotToggle.colors = colors;
            }
        }
    }


    public void ShowSpecialAttackMenu(CharacterController pc, System.Action<SpecialAttackType, bool> onSelect, System.Action onCancel)
    {
        HideSpecialStyleSelectionMenu();
        HideBullRushExtraPushChoice();

        if (_specialAttackPanel == null)
            BuildSpecialAttackPanel();

        if (_specialAttackPanel == null) return;

        bool hasStandardAction = pc != null && pc.Actions.HasStandardAction;
        bool hasFullRoundAction = pc != null && pc.Actions.HasFullRoundAction;
        bool hasGrappleAttackAvailable = pc != null && GameManager.Instance != null && GameManager.Instance.CanUseGrappleAttackOption(pc);
        bool hasBullRushAttackAvailable = pc != null && GameManager.Instance != null && GameManager.Instance.CanUseBullRushAttackOption(pc);
        bool hasTripAttackAvailable = pc != null && GameManager.Instance != null && GameManager.Instance.CanUseTripAttackOption(pc);
        bool hasDisarmAttackAvailable = pc != null && GameManager.Instance != null && GameManager.Instance.CanUseDisarmAttackOption(pc);
        bool hasSunderAttackAvailable = pc != null && GameManager.Instance != null && GameManager.Instance.CanUseSunderAttackOption(pc);
        bool canImprovedFeintMove = pc != null
            && pc.Stats != null
            && pc.Stats.HasFeat("Improved Feint")
            && (pc.Actions.HasMoveAction || pc.Actions.CanConvertStandardToMove);

        GameManager gm = GameManager.Instance;
        string actorName = pc != null && pc.Stats != null ? pc.Stats.CharacterName : "<null>";
        Debug.Log($"[CombatUI][SpecialAttackMenu] SHOW actor={actorName} phase={(gm != null ? gm.CurrentPhase.ToString() : "<no-gm>")} subPhase={(gm != null ? gm.CurrentSubPhase.ToString() : "<no-gm>")} std={hasStandardAction} full={hasFullRoundAction} grappleAvailable={hasGrappleAttackAvailable} bullRushAvailable={hasBullRushAttackAvailable} tripAvailable={hasTripAttackAvailable} disarmAvailable={hasDisarmAttackAvailable} sunderAvailable={hasSunderAttackAvailable} improvedFeintMove={canImprovedFeintMove}");

        _specialAttackPanel.SetActive(true);

        var buttons = _specialAttackPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn == null) continue;

            if (btn.name == "Disarm (Off-Hand)")
            {
                bool showOffHandDisarm = pc != null
                    && GameManager.Instance != null
                    && GameManager.Instance.ShouldShowOffHandDisarmButton(pc);
                btn.gameObject.SetActive(showOffHandDisarm);
                if (!showOffHandDisarm)
                {
                    Debug.Log($"[CombatUI][SpecialAttackMenu] button={btn.name} active=false (dual wield disarm not enabled)");
                    continue;
                }
            }
            else if (btn.name == "Sunder (Off-Hand)")
            {
                bool showOffHandSunder = pc != null
                    && GameManager.Instance != null
                    && GameManager.Instance.ShouldShowOffHandSunderButton(pc);
                btn.gameObject.SetActive(showOffHandSunder);
                if (!showOffHandSunder)
                {
                    Debug.Log($"[CombatUI][SpecialAttackMenu] button={btn.name} active=false (dual wield sunder not enabled)");
                    continue;
                }
            }
            else
            {
                bool hideStandardGrappleForImprovedGrab = btn.name == "Grapple"
                    && pc != null
                    && pc.Stats != null
                    && pc.Stats.HasImprovedGrab;
                btn.gameObject.SetActive(!hideStandardGrappleForImprovedGrab);
                if (hideStandardGrappleForImprovedGrab)
                {
                    Debug.Log($"[CombatUI][SpecialAttackMenu] button={btn.name} hidden for {pc.Stats.CharacterName}: Improved Grab creatures cannot use standard Grapple action.");
                    continue;
                }
            }

            bool enabled = IsSpecialAttackButtonEnabled(btn.name, pc, hasStandardAction, hasFullRoundAction, hasGrappleAttackAvailable, hasBullRushAttackAvailable, hasTripAttackAvailable, hasDisarmAttackAvailable, hasSunderAttackAvailable, canImprovedFeintMove);
            btn.interactable = enabled;
            UpdateSpecialAttackButtonLabel(btn, pc, enabled);
            Debug.Log($"[CombatUI][SpecialAttackMenu] button={btn.name} active={btn.gameObject.activeSelf} interactable={btn.interactable}");
        }

        WireSpecialAttackMenu(onSelect, onCancel);
    }

    private bool IsSpecialAttackButtonEnabled(string buttonName, CharacterController pc, bool hasStandardAction, bool hasFullRoundAction, bool hasGrappleAttackAvailable, bool hasBullRushAttackAvailable, bool hasTripAttackAvailable, bool hasDisarmAttackAvailable, bool hasSunderAttackAvailable, bool canImprovedFeintMove)
    {
        if (buttonName == "Cancel")
            return true;

        if (pc != null && pc.HasCondition(CombatConditionType.Turned))
            return false;

        bool enabled;
        switch (buttonName)
        {
            case "Grapple":
                enabled = hasGrappleAttackAvailable;
                break;
            case "Bull Rush (Attack)":
                enabled = hasBullRushAttackAvailable;
                break;
            case "Bull Rush (Charge)":
                enabled = hasFullRoundAction;
                break;
            case "Disarm":
                enabled = GameManager.Instance != null && GameManager.Instance.CanUseMainHandDisarmAttackOption(pc);
                break;
            case "Disarm (Off-Hand)":
                enabled = GameManager.Instance != null && GameManager.Instance.CanUseOffHandDisarmAttackOption(pc);
                break;
            case "Sunder":
                enabled = GameManager.Instance != null && GameManager.Instance.CanUseMainHandSunderAttackOption(pc);
                break;
            case "Sunder (Off-Hand)":
                enabled = GameManager.Instance != null && GameManager.Instance.CanUseOffHandSunderAttackOption(pc);
                break;
            case "Feint":
                enabled = hasStandardAction || canImprovedFeintMove;
                break;
            case "Coup de Grace":
                enabled = GameManager.Instance != null && GameManager.Instance.CanUseCoupDeGraceAttackOption(pc);
                break;
            case "Aid Another":
                enabled = GameManager.Instance != null && GameManager.Instance.CanUseAidAnother(pc, out _);
                break;
            case "Overrun":
                enabled = GameManager.Instance != null && GameManager.Instance.CanUseOverrun(pc, out _);
                break;
            case "Trip":
                enabled = hasTripAttackAvailable;
                break;
            default:
                enabled = hasStandardAction;
                break;
        }

        if (pc != null)
        {
            if (buttonName == "Disarm")
                enabled &= pc.HasMeleeWeaponEquipped();
            else if (buttonName == "Disarm (Off-Hand)")
                enabled &= pc.HasOffHandWeaponEquipped();
            else if (buttonName == "Sunder")
                enabled &= pc.HasMeleeWeaponEquipped();
            else if (buttonName == "Sunder (Off-Hand)")
                enabled &= pc.HasOffHandWeaponEquipped();
            if (buttonName == "Bull Rush (Attack)" || buttonName == "Bull Rush (Charge)" || buttonName == "Trip")
                enabled &= pc.HasMeleeWeaponEquipped();
        }

        return enabled;
    }

    private void UpdateSpecialAttackButtonLabel(Button button, CharacterController pc, bool isEnabled)
    {
        if (button == null)
            return;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label == null)
            return;

        switch (button.name)
        {
            case "Grapple":
                if (pc != null && pc.Stats != null && pc.Stats.HasImprovedGrab)
                {
                    label.text = "Grapple (Use Improved Grab on hit)";
                }
                else if (pc != null && isEnabled && GameManager.Instance != null)
                {
                    int remaining = GameManager.Instance.GetRemainingGrappleAttackActions(pc);
                    int currentBab = GameManager.Instance.GetCurrentGrappleAttackBonus(pc);
                    label.text = $"Grapple (BAB {CharacterStats.FormatMod(currentBab)}, {remaining} left)";
                }
                else
                {
                    label.text = "Grapple (No attacks)";
                }
                break;

            case "Bull Rush (Attack)":
                if (pc != null && isEnabled && GameManager.Instance != null)
                {
                    int remaining = GameManager.Instance.GetRemainingBullRushAttackActions(pc);
                    int currentBab = GameManager.Instance.GetCurrentBullRushAttackBonus(pc);
                    label.text = $"Bull Rush (Attack) (BAB {CharacterStats.FormatMod(currentBab)}, {remaining} left)";
                }
                else
                {
                    label.text = "Bull Rush (Attack) (No attacks)";
                }
                break;

            case "Trip":
                if (pc != null && isEnabled && GameManager.Instance != null)
                {
                    int remaining = GameManager.Instance.GetRemainingTripAttackActions(pc);
                    int currentBab = GameManager.Instance.GetCurrentTripAttackBonus(pc);
                    label.text = $"Trip (BAB {CharacterStats.FormatMod(currentBab)}, {remaining} left)";
                }
                else
                {
                    label.text = "Trip (No attacks)";
                }
                break;

            case "Bull Rush (Charge)":
                label.text = isEnabled
                    ? "Bull Rush (Charge) (+2 bonus)"
                    : "Bull Rush (Charge) (Not available)";
                break;

            case "Disarm":
                if (pc != null && isEnabled && GameManager.Instance != null)
                {
                    int remaining = GameManager.Instance.GetRemainingMainHandDisarmAttackActions(pc);
                    int currentBab = GameManager.Instance.GetCurrentMainHandDisarmAttackBonus(pc);
                    label.text = $"Disarm (BAB {CharacterStats.FormatMod(currentBab)}, {remaining} left)";
                }
                else
                {
                    label.text = "Disarm (No main-hand attacks)";
                }
                break;

            case "Disarm (Off-Hand)":
                if (pc != null && isEnabled && GameManager.Instance != null)
                {
                    int remaining = GameManager.Instance.GetRemainingOffHandDisarmAttackActions(pc);
                    int currentBab = GameManager.Instance.GetCurrentOffHandDisarmAttackBonus(pc);
                    label.text = $"Disarm (Off-Hand) (BAB {CharacterStats.FormatMod(currentBab)}, {remaining} left)";
                }
                else
                {
                    label.text = "Disarm (Off-Hand) (No attacks)";
                }
                break;

            case "Sunder":
                if (pc != null && isEnabled && GameManager.Instance != null)
                {
                    int remaining = GameManager.Instance.GetRemainingMainHandSunderAttackActions(pc);
                    int currentBab = GameManager.Instance.GetCurrentMainHandSunderAttackBonus(pc);
                    label.text = $"Sunder (BAB {CharacterStats.FormatMod(currentBab)}, {remaining} left)";
                }
                else
                {
                    label.text = "Sunder (No main-hand attacks)";
                }
                break;

            case "Sunder (Off-Hand)":
                if (pc != null && isEnabled && GameManager.Instance != null)
                {
                    int remaining = GameManager.Instance.GetRemainingOffHandSunderAttackActions(pc);
                    int currentBab = GameManager.Instance.GetCurrentOffHandSunderAttackBonus(pc);
                    label.text = $"Sunder (Off-Hand) (BAB {CharacterStats.FormatMod(currentBab)}, {remaining} left)";
                }
                else
                {
                    label.text = "Sunder (Off-Hand) (No attacks)";
                }
                break;

            case "Coup de Grace":
                if (pc != null && GameManager.Instance != null)
                {
                    int helplessCount = GameManager.Instance.GetAdjacentHelplessEnemiesForCoupDeGrace(pc).Count;
                    bool canUseCdg = GameManager.Instance.CanUseCoupDeGraceAttackOption(pc);
                    label.text = canUseCdg
                        ? $"Coup de Grace ({helplessCount} target{(helplessCount == 1 ? string.Empty : "s")})"
                        : "Coup de Grace (Needs full-round + helpless adjacent target)";
                }
                else
                {
                    label.text = "Coup de Grace";
                }
                break;

            case "Aid Another":
                string aidReason = "Unavailable";
                bool canAidAnother = pc != null && GameManager.Instance != null && GameManager.Instance.CanUseAidAnother(pc, out aidReason);
                label.text = canAidAnother ? "Aid Another (Standard)" : $"Aid Another ({aidReason})";
                break;

            case "Overrun":
                string overrunReason = "Unavailable";
                bool canOverrun = pc != null && GameManager.Instance != null && GameManager.Instance.CanUseOverrun(pc, out overrunReason);
                label.text = canOverrun ? "Overrun (Move+Standard)" : $"Overrun ({overrunReason})";
                break;
        }
    }

    public void HideSpecialAttackMenu()
    {
        if (_specialAttackPanel != null)
            _specialAttackPanel.SetActive(false);
    }

    public void ShowSpecialStyleSelectionMenu(
        string menuName,
        List<string> optionLabels,
        List<bool> optionEnabledStates,
        System.Action<int> onSelect,
        System.Action onCancel)
    {
        string optionSummary = optionLabels == null ? "<null>" : string.Join(", ", optionLabels);
        LogSpecialStyleMenuLifecycle(
            "SHOW_MENU_CALLED",
            $"name={menuName}, options=[{optionSummary}], frame={Time.frameCount}");

        HideSpecialStyleSelectionMenu();

        if (ActionPanel == null)
        {
            onCancel?.Invoke();
            return;
        }

        if (optionLabels == null)
            optionLabels = new List<string>();

        if (optionEnabledStates == null || optionEnabledStates.Count != optionLabels.Count)
        {
            optionEnabledStates = new List<bool>(optionLabels.Count);
            for (int i = 0; i < optionLabels.Count; i++)
                optionEnabledStates.Add(true);
        }

        _specialStyleSelectionPanel = new GameObject(string.IsNullOrEmpty(menuName) ? "SpecialStyleSelectionPanel" : menuName);
        _specialStyleSelectionPanel.transform.SetParent(ActionPanel.transform, false);

        RectTransform rt = _specialStyleSelectionPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.02f, 0.35f);
        rt.anchorMax = new Vector2(0.52f, 0.98f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = _specialStyleSelectionPanel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

        VerticalLayoutGroup layout = _specialStyleSelectionPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        _specialStyleSelectionPanel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < optionLabels.Count; i++)
        {
            int optionIndex = i;
            bool isEnabled = optionEnabledStates[i];
            Button optionButton = CreateSpecialStyleSelectionButton(
                parent: _specialStyleSelectionPanel.transform,
                name: $"Option_{i}",
                label: optionLabels[i],
                backgroundColor: new Color(0.22f, 0.22f, 0.28f, 1f),
                isInteractable: isEnabled);

            if (!isEnabled)
            {
                Text labelText = optionButton != null ? optionButton.GetComponentInChildren<Text>() : null;
                if (labelText != null)
                    labelText.color = new Color(0.74f, 0.74f, 0.74f, 1f);
            }

            if (optionButton != null)
            {
                optionButton.onClick.AddListener(() =>
                {
                    LogSpecialStyleMenuLifecycle("OPTION_CLICKED", $"index={optionIndex}, label={optionLabels[optionIndex]}, frame={Time.frameCount}");
                    HideSpecialStyleSelectionMenu();
                    onSelect?.Invoke(optionIndex);
                });
            }
        }

        Button cancelButton = CreateSpecialStyleSelectionButton(
            parent: _specialStyleSelectionPanel.transform,
            name: "Cancel",
            label: "Cancel",
            backgroundColor: new Color(0.35f, 0.16f, 0.16f, 1f),
            isInteractable: true);

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(() =>
            {
                LogSpecialStyleMenuLifecycle("CANCEL_CLICKED", $"frame={Time.frameCount}");
                HideSpecialStyleSelectionMenu();
                onCancel?.Invoke();
            });
        }

        _specialStyleSelectionPanel.SetActive(true);
        _specialStyleMenuWasActiveLastFrame = true;
        LogSpecialStyleMenuLifecycle("MENU_PANEL_ACTIVATED", $"name={_specialStyleSelectionPanel.name}, frame={Time.frameCount}");
    }

    public void HideSpecialStyleSelectionMenu()
    {
        if (_specialStyleSelectionPanel != null)
        {
            LogSpecialStyleMenuLifecycle("HIDE_MENU_CALLED", $"name={_specialStyleSelectionPanel.name}, active={_specialStyleSelectionPanel.activeSelf}, frame={Time.frameCount}");
            Destroy(_specialStyleSelectionPanel);
            _specialStyleSelectionPanel = null;
            _specialStyleMenuWasActiveLastFrame = false;
            LogSpecialStyleMenuLifecycle("MENU_PANEL_DEACTIVATED", $"frame={Time.frameCount}");
        }
        else
        {
            LogSpecialStyleMenuLifecycle("HIDE_MENU_CALLED_WITH_NULL_PANEL", $"frame={Time.frameCount}");
        }
    }

    public void ShowBullRushPushChoice(
        CharacterController attacker,
        CharacterController target,
        int minSquares,
        int maxSquares,
        System.Action<int> onSelect,
        System.Action onCancel = null)
    {
        // Backward-compatible wrapper: old API chose total squares, new UI chooses extra squares.
        int min = Mathf.Max(1, minSquares);
        int max = Mathf.Max(min, maxSquares);
        int maxExtra = Mathf.Max(0, max - 1);

        ShowBullRushExtraPushChoice(attacker, target, maxExtra,
            onSelect: extraSquares =>
            {
                int totalSquares = 1 + Mathf.Max(0, extraSquares);
                totalSquares = Mathf.Clamp(totalSquares, min, max);
                onSelect?.Invoke(totalSquares);
            },
            onCancel: onCancel);
    }

    private GameObject _bullRushExtraPushPanel;
    private int _bullRushExtraPushMaxExtraSquares;
    private System.Action<int> _bullRushExtraPushOnSelect;
    private System.Action _bullRushExtraPushOnCancel;

    private void LogBullRushExtraPushLifecycle(string eventName, string details = null)
    {
        string suffix = string.IsNullOrEmpty(details) ? string.Empty : $" | {details}";
        Debug.Log($"[CombatUI][BullRushExtraPush] {eventName}{suffix}");
    }

    public void ShowBullRushExtraPushChoice(
        CharacterController attacker,
        CharacterController target,
        int maxExtraSquares,
        System.Action<int> onSelect,
        System.Action onCancel = null)
    {
        string attackerName = attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "<null-attacker>";
        string targetName = target != null && target.Stats != null ? target.Stats.CharacterName : "<null-target>";

        int maxExtra = Mathf.Max(0, maxExtraSquares);
        LogBullRushExtraPushLifecycle("SHOW_CALLED", $"attacker={attackerName}, target={targetName}, requestedMaxExtra={maxExtraSquares}, clampedMaxExtra={maxExtra}, frame={Time.frameCount}");

        if (maxExtra <= 0)
        {
            LogBullRushExtraPushLifecycle("AUTO_SELECT_ZERO", $"reason=maxExtra<=0, frame={Time.frameCount}");
            onSelect?.Invoke(0);
            return;
        }

        HideSpecialStyleSelectionMenu();
        HideBullRushExtraPushChoice();

        _bullRushExtraPushMaxExtraSquares = maxExtra;
        _bullRushExtraPushOnSelect = onSelect;
        _bullRushExtraPushOnCancel = onCancel;

        if (ActionPanel == null)
        {
            LogBullRushExtraPushLifecycle("ACTION_PANEL_NULL", $"fallbackSelect=0, frame={Time.frameCount}");
            _bullRushExtraPushOnSelect?.Invoke(0);
            HideBullRushExtraPushChoice();
            return;
        }

        if (!ActionPanel.activeSelf)
        {
            LogBullRushExtraPushLifecycle("ACTION_PANEL_INACTIVE", $"Reactivating ActionPanel before creating prompt, frame={Time.frameCount}");
            ActionPanel.SetActive(true);
        }

        LogBullRushExtraPushLifecycle("PANEL_CREATE_BEGIN", $"parent={ActionPanel.name}, parentActiveSelf={ActionPanel.activeSelf}, parentActiveInHierarchy={ActionPanel.activeInHierarchy}, frame={Time.frameCount}");

        _bullRushExtraPushPanel = new GameObject("BullRushExtraPushChoicePanel");
        _bullRushExtraPushPanel.transform.SetParent(ActionPanel.transform, false);
        _bullRushExtraPushPanel.transform.SetAsLastSibling();

        RectTransform rt = _bullRushExtraPushPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.02f, 0.35f);
        rt.anchorMax = new Vector2(0.58f, 0.98f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = _bullRushExtraPushPanel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

        VerticalLayoutGroup rootLayout = _bullRushExtraPushPanel.AddComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 6f;
        rootLayout.padding = new RectOffset(10, 10, 10, 10);
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;

        _bullRushExtraPushPanel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateBullRushExtraPushText(
            _bullRushExtraPushPanel.transform,
            "Header",
            "How many extra squares would you like to push the target?",
            14,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            Color.white);

        CreateBullRushExtraPushText(
            _bullRushExtraPushPanel.transform,
            "Info",
            $"Base push: 1 square (5 feet)\nAdditional available: 0 to {maxExtra} squares",
            12,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            new Color(0.85f, 0.85f, 0.9f, 1f));

        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(_bullRushExtraPushPanel.transform, false);
        var grid = buttonContainer.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(52f, 42f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Clamp(maxExtra + 1, 3, 8);
        var buttonContainerLE = buttonContainer.AddComponent<LayoutElement>();
        buttonContainerLE.minHeight = maxExtra > 5 ? 96f : 48f;

        int createdOptionButtonCount = 0;
        for (int extra = 0; extra <= maxExtra; extra++)
        {
            int extraCopy = extra;
            int totalSquares = 1 + extraCopy;
            int totalFeet = totalSquares * 5;

            Button optionButton = CreateSpecialStyleSelectionButton(
                parent: buttonContainer.transform,
                name: $"Extra_{extraCopy}",
                label: extraCopy.ToString(),
                backgroundColor: extraCopy == 0 ? new Color(0.3f, 0.3f, 0.34f, 1f) : new Color(0.45f, 0.33f, 0.12f, 1f),
                isInteractable: true);

            if (optionButton != null)
            {
                createdOptionButtonCount++;
                Text labelText = optionButton.GetComponentInChildren<Text>();
                if (labelText != null)
                {
                    labelText.fontSize = 24;
                    labelText.fontStyle = FontStyle.Bold;
                    labelText.color = Color.white;
                }

                optionButton.onClick.AddListener(() => OnBullRushExtraPushSelected(extraCopy));
                optionButton.gameObject.name = $"Extra_{extraCopy}_{totalSquares}sq_{totalFeet}ft";
            }
        }

        CreateBullRushExtraPushText(
            _bullRushExtraPushPanel.transform,
            "Footer",
            "(Total push = 1 + extra squares)",
            11,
            FontStyle.Italic,
            TextAnchor.MiddleLeft,
            new Color(0.72f, 0.72f, 0.78f, 1f));

        Button cancelButton = CreateSpecialStyleSelectionButton(
            parent: _bullRushExtraPushPanel.transform,
            name: "Cancel",
            label: "Cancel (default 0)",
            backgroundColor: new Color(0.35f, 0.16f, 0.16f, 1f),
            isInteractable: true);

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(() =>
            {
                LogBullRushExtraPushLifecycle("CANCEL_CLICKED", $"hasCustomCancel={_bullRushExtraPushOnCancel != null}, frame={Time.frameCount}");
                if (_bullRushExtraPushOnCancel != null)
                {
                    System.Action cancelCallback = _bullRushExtraPushOnCancel;
                    HideBullRushExtraPushChoice();
                    cancelCallback.Invoke();
                }
                else
                {
                    OnBullRushExtraPushSelected(0);
                }
            });
        }

        _bullRushExtraPushPanel.SetActive(true);
        LogBullRushExtraPushLifecycle(
            "PANEL_ACTIVATED",
            $"buttons={createdOptionButtonCount}, panelActiveSelf={_bullRushExtraPushPanel.activeSelf}, panelActiveInHierarchy={_bullRushExtraPushPanel.activeInHierarchy}, anchoredMin={rt.anchorMin}, anchoredMax={rt.anchorMax}, frame={Time.frameCount}");
    }

    public void HideBullRushExtraPushChoice()
    {
        if (_bullRushExtraPushPanel != null)
        {
            LogBullRushExtraPushLifecycle("HIDE_PANEL", $"panelName={_bullRushExtraPushPanel.name}, activeSelf={_bullRushExtraPushPanel.activeSelf}, frame={Time.frameCount}");
            Destroy(_bullRushExtraPushPanel);
        }
        else
        {
            LogBullRushExtraPushLifecycle("HIDE_PANEL_NOOP", $"panelAlreadyNull=true, frame={Time.frameCount}");
        }

        _bullRushExtraPushPanel = null;
        _bullRushExtraPushMaxExtraSquares = 0;
        _bullRushExtraPushOnSelect = null;
        _bullRushExtraPushOnCancel = null;
    }

    private void OnBullRushExtraPushSelected(int extraSquares)
    {
        int extra = Mathf.Clamp(extraSquares, 0, _bullRushExtraPushMaxExtraSquares);
        LogBullRushExtraPushLifecycle("OPTION_SELECTED", $"requested={extraSquares}, clamped={extra}, maxExtra={_bullRushExtraPushMaxExtraSquares}, frame={Time.frameCount}");
        System.Action<int> callback = _bullRushExtraPushOnSelect;
        HideBullRushExtraPushChoice();
        callback?.Invoke(extra);
    }

    private Text CreateBullRushExtraPushText(
        Transform parent,
        string name,
        string text,
        int fontSize,
        FontStyle style,
        TextAnchor alignment,
        Color color)
    {
        GameObject textGo = new GameObject(name);
        textGo.transform.SetParent(parent, false);

        Text txt = textGo.AddComponent<Text>();
        txt.text = text;
        txt.alignment = alignment;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);

        LayoutElement le = textGo.AddComponent<LayoutElement>();
        le.minHeight = Mathf.Max(24f, fontSize + 8f);

        return txt;
    }

    public void ShowBullRushFollowChoice(
        CharacterController attacker,
        CharacterController target,
        int pushedSquares,
        System.Action<bool> onDecision)
    {
        string attackerName = attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "Attacker";
        string targetName = target != null && target.Stats != null ? target.Stats.CharacterName : "target";
        int squares = Mathf.Max(1, pushedSquares);
        int feet = squares * 5;

        ShowConfirmationDialog(
            title: "Bull Rush Follow",
            message: $"{attackerName} pushed {targetName} {squares} square{(squares == 1 ? string.Empty : "s")} ({feet} feet).\nFollow into the vacated squares?",
            confirmLabel: "Follow",
            cancelLabel: "Stay",
            onConfirm: () => onDecision?.Invoke(true),
            onCancel: () => onDecision?.Invoke(false));
    }

    public bool IsSpecialStyleSelectionMenuOpen()
    {
        return _specialStyleSelectionPanel != null && _specialStyleSelectionPanel.activeSelf;
    }
    private Button CreateSpecialStyleSelectionButton(Transform parent, string name, string label, Color backgroundColor, bool isInteractable)
    {
        if (parent == null)
            return null;

        Button button = UIFactory.CreateButton(
            parent,
            label,
            null,
            new Vector2(0f, 24f),
            backgroundColor,
            name,
            UIFactory.GetDefaultFont(),
            12,
            Color.white);

        UIFactory.ApplyEnhancedCombatButtonStyle(button, backgroundColor);
        button.interactable = isInteractable;

        LayoutElement le = button.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 24;

        return button;
    }

    private void BuildSpecialAttackPanel()
    {
        if (ActionPanel == null) return;

        _specialAttackPanel = new GameObject("SpecialAttackPanel");
        _specialAttackPanel.transform.SetParent(ActionPanel.transform, false);

        var rt = _specialAttackPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.02f, 0.35f);
        rt.anchorMax = new Vector2(0.52f, 0.98f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = _specialAttackPanel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

        var layout = _specialAttackPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        _specialAttackPanel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateSpecialButton("Trip", "Trip");
        CreateSpecialButton("Disarm", "Disarm");
        CreateSpecialButton("Disarm (Off-Hand)", "Disarm (Off-Hand)");
        CreateSpecialButton("Grapple", "Grapple");
        CreateSpecialButton("Sunder", "Sunder");
        CreateSpecialButton("Sunder (Off-Hand)", "Sunder (Off-Hand)");
        CreateSpecialButton("Bull Rush (Attack)", "Bull Rush (Attack)");
        CreateSpecialButton("Bull Rush (Charge)", "Bull Rush (Charge)");
        CreateSpecialButton("Overrun", "Overrun");
        CreateSpecialButton("Feint", "Feint");
        CreateSpecialButton("Coup de Grace", "Coup de Grace");
        CreateSpecialButton("Aid Another", "Aid Another");
        CreateSpecialCancelButton();

        _specialAttackPanel.SetActive(false);
    }

    private void WireSpecialAttackMenu(System.Action<SpecialAttackType, bool> onSelect, System.Action onCancel)
    {
        if (_specialAttackPanel == null) return;

        foreach (var btn in _specialAttackPanel.GetComponentsInChildren<Button>(true))
        {
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            switch (btn.name)
            {
                case "Trip": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Trip"); onSelect?.Invoke(SpecialAttackType.Trip, false); }); break;
                case "Disarm": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Disarm"); onSelect?.Invoke(SpecialAttackType.Disarm, false); }); break;
                case "Disarm (Off-Hand)": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Disarm (Off-Hand)"); onSelect?.Invoke(SpecialAttackType.Disarm, true); }); break;
                case "Grapple": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Grapple"); onSelect?.Invoke(SpecialAttackType.Grapple, false); }); break;
                case "Sunder": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Sunder"); onSelect?.Invoke(SpecialAttackType.Sunder, false); }); break;
                case "Sunder (Off-Hand)": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Sunder (Off-Hand)"); onSelect?.Invoke(SpecialAttackType.Sunder, true); }); break;
                case "Bull Rush (Attack)": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Bull Rush (Attack)"); onSelect?.Invoke(SpecialAttackType.BullRushAttack, false); }); break;
                case "Bull Rush (Charge)": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Bull Rush (Charge)"); onSelect?.Invoke(SpecialAttackType.BullRushCharge, false); }); break;
                case "Overrun": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Overrun"); onSelect?.Invoke(SpecialAttackType.Overrun, false); }); break;
                case "Feint": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Feint"); onSelect?.Invoke(SpecialAttackType.Feint, false); }); break;
                case "Coup de Grace": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Coup de Grace"); onSelect?.Invoke(SpecialAttackType.CoupDeGrace, false); }); break;
                case "Aid Another": btn.onClick.AddListener(() => { Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Aid Another"); onSelect?.Invoke(SpecialAttackType.AidAnother, false); }); break;
                case "Cancel":
                    btn.onClick.AddListener(() =>
                    {
                        Debug.Log("[CombatUI][SpecialAttackMenu] CLICK Cancel");
                        HideSpecialAttackMenu();
                        onCancel?.Invoke();
                    });
                    break;
            }
        }
    }

    private void CreateSpecialButton(string name, string label)
    {
        Color baseColor = new Color(0.22f, 0.22f, 0.28f, 1f);
        Button button = UIFactory.CreateButton(
            _specialAttackPanel.transform,
            label,
            null,
            new Vector2(0f, 24f),
            baseColor,
            name,
            UIFactory.GetDefaultFont(),
            12,
            Color.white);

        UIFactory.ApplyEnhancedCombatButtonStyle(button, baseColor);

        LayoutElement le = button.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 24;
    }

    private void CreateSpecialCancelButton()
    {
        Color baseColor = new Color(0.35f, 0.16f, 0.16f, 1f);
        Button button = UIFactory.CreateButton(
            _specialAttackPanel.transform,
            "Cancel",
            null,
            new Vector2(0f, 24f),
            baseColor,
            "Cancel",
            UIFactory.GetDefaultFont(),
            12,
            Color.white);

        UIFactory.ApplyEnhancedCombatButtonStyle(button, baseColor);

        LayoutElement le = button.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 24;
    }

    /// <summary>
    /// Highlight which PC is currently active (1-4). Pass 0 to clear all.
    /// </summary>
    public void SetActivePC(int pcNumber)
    {
        EnsureCharacterInfoPanel();
        _characterInfoPanel?.SetActivePC(pcNumber);
    }

    /// <summary>
    /// Highlight which NPC is currently active (0-based index). Pass -1 to clear all.
    /// </summary>
    public void SetActiveNPC(int npcIndex)
    {
        EnsureCharacterInfoPanel();
        _characterInfoPanel?.SetActiveNPC(npcIndex);
    }

    /// <summary>
    /// Set a PC panel's icon sprite.
    /// </summary>
    public void SetPCIcon(int pcNumber, Sprite icon)
    {
        EnsureCharacterInfoPanel();
        _characterInfoPanel?.SetPCIcon(pcNumber, icon);
    }

    /// <summary>
    /// Set an NPC panel's icon sprite.
    /// </summary>
    public void SetNPCIcon(int npcIndex, Sprite icon)
    {
        EnsureCharacterInfoPanel();
        _characterInfoPanel?.SetNPCIcon(npcIndex, icon);
    }

    // ========================================================================
    // UNIFIED AOO CONFIRMATION DIALOG
    // ========================================================================

    private GameObject _aooConfirmationPanel;
    private Text _aooConfirmationTitleText;
    private Text _aooConfirmationActionText;
    private Text _aooConfirmationThreatsText;

    private Button _aooProceedButton;
    private Text _aooProceedButtonText;

    private GameObject _aooCastDefensivelyButtonObject;
    private Button _aooCastDefensivelyButton;
    private Text _aooCastDefensivelyButtonText;

    private Button _aooCancelButton;
    private Text _aooCancelButtonText;

    public void ShowAoOConfirmationPrompt(AoOProvokingActionInfo actionInfo)
    {
        if (actionInfo == null)
        {
            Debug.LogWarning("[CombatUI] ShowAoOConfirmationPrompt called with null actionInfo");
            return;
        }

        if (_aooConfirmationPanel == null)
            BuildAoOConfirmationPanel();

        if (_aooConfirmationPanel == null)
        {
            actionInfo.OnCancel?.Invoke();
            return;
        }

        _aooConfirmationTitleText.text = "ACTION PROVOKES ATTACK";
        _aooConfirmationActionText.text = $"Action: {actionInfo.ActionName}";

        var threatsText = new StringBuilder();
        int threatCount = actionInfo.ThreateningEnemies != null ? actionInfo.ThreateningEnemies.Count : 0;
        string enemyWord = threatCount == 1 ? "enemy" : "enemies";
        threatsText.AppendLine();
        threatsText.AppendLine($"This will provoke attacks of opportunity from {threatCount} {enemyWord}:");

        if (actionInfo.ThreateningEnemies != null)
        {
            for (int i = 0; i < actionInfo.ThreateningEnemies.Count; i++)
            {
                CharacterController enemy = actionInfo.ThreateningEnemies[i];
                if (enemy == null || enemy.Stats == null) continue;

                Vector2Int actorPos = actionInfo.Actor != null ? actionInfo.Actor.GridPosition : Vector2Int.zero;
                Vector2Int enemyPos = enemy.GridPosition;
                string direction = GetDirectionString(actorPos, enemyPos);
                threatsText.AppendLine($"• {enemy.Stats.CharacterName} ({direction})");
            }
        }

        _aooConfirmationThreatsText.text = threatsText.ToString();

        bool isSpellcast = actionInfo.ActionType == AoOProvokingAction.CastSpell;
        _aooCastDefensivelyButtonObject.SetActive(isSpellcast);

        if (isSpellcast)
        {
            var defensiveText = new StringBuilder();
            defensiveText.AppendLine("CAST DEFENSIVELY");
            defensiveText.AppendLine();
            defensiveText.AppendLine($"Concentration DC: {actionInfo.CastDefensivelyDC}");
            defensiveText.AppendLine($"Your Bonus: +{actionInfo.ConcentrationBonus}");
            defensiveText.AppendLine($"Success Chance: {Mathf.RoundToInt(actionInfo.SuccessChance)}%");
            defensiveText.AppendLine();
            defensiveText.AppendLine("✓ No AoO if successful");
            defensiveText.AppendLine("⚠ Spell lost if failed");
            _aooCastDefensivelyButtonText.text = defensiveText.ToString();

            _aooCastDefensivelyButton.onClick.RemoveAllListeners();
            _aooCastDefensivelyButton.onClick.AddListener(() =>
            {
                HideAoOConfirmationPrompt();
                actionInfo.OnCastDefensively?.Invoke();
            });

            var proceedText = new StringBuilder();
            proceedText.AppendLine("CAST NORMALLY");
            proceedText.AppendLine();
            proceedText.AppendLine($"Provokes {threatCount} {(threatCount == 1 ? "attack" : "attacks")}");
            proceedText.AppendLine();
            proceedText.AppendLine("⚠ May be interrupted if hit");
            _aooProceedButtonText.text = proceedText.ToString();
        }
        else
        {
            var proceedText = new StringBuilder();
            proceedText.AppendLine("PROCEED");
            proceedText.AppendLine();
            proceedText.AppendLine($"Provokes {threatCount} {(threatCount == 1 ? "attack" : "attacks")}");

            if (actionInfo.ActionType == AoOProvokingAction.StandFromProne)
            {
                proceedText.AppendLine();
                proceedText.AppendLine("Stand up (move action)");
            }
            else if (actionInfo.ActionType == AoOProvokingAction.DrinkPotion)
            {
                proceedText.AppendLine();
                proceedText.AppendLine("Manipulate item (move action, or standard)");
            }

            _aooProceedButtonText.text = proceedText.ToString();
        }

        _aooProceedButton.onClick.RemoveAllListeners();
        _aooProceedButton.onClick.AddListener(() =>
        {
            HideAoOConfirmationPrompt();
            actionInfo.OnProceed?.Invoke();
        });

        _aooCancelButtonText.text = "CANCEL\n\nReturn to action selection";
        _aooCancelButton.onClick.RemoveAllListeners();
        _aooCancelButton.onClick.AddListener(() =>
        {
            HideAoOConfirmationPrompt();
            actionInfo.OnCancel?.Invoke();
        });

        _aooConfirmationPanel.SetActive(true);
    }

    public void HideAoOConfirmationPrompt()
    {
        if (_aooConfirmationPanel != null)
            _aooConfirmationPanel.SetActive(false);
    }

    private void BuildAoOConfirmationPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _aooConfirmationPanel = new GameObject("AoOConfirmationPanel");
        _aooConfirmationPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRT = _aooConfirmationPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bgImage = _aooConfirmationPanel.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.76f);

        GameObject dialogBox = new GameObject("DialogBox");
        dialogBox.transform.SetParent(_aooConfirmationPanel.transform, false);
        RectTransform dialogRT = dialogBox.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.28f, 0.17f);
        dialogRT.anchorMax = new Vector2(0.72f, 0.83f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialogBox.AddComponent<Image>();
        dialogBg.color = new Color(0.12f, 0.1f, 0.16f, 0.98f);

        Outline dialogOutline = dialogBox.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.75f, 0.7f, 0.95f, 1f);
        dialogOutline.effectDistance = new Vector2(2f, 2f);

        _aooConfirmationTitleText = CreatePanelText(dialogBox, "Title", new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), 16, TextAnchor.MiddleCenter, FontStyle.Bold);
        _aooConfirmationActionText = CreatePanelText(dialogBox, "Action", new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.89f), 13, TextAnchor.MiddleCenter, FontStyle.Bold);
        _aooConfirmationThreatsText = CreatePanelText(dialogBox, "Threats", new Vector2(0.07f, 0.54f), new Vector2(0.93f, 0.81f), 12, TextAnchor.UpperLeft, FontStyle.Normal);

        _aooCastDefensivelyButtonObject = CreatePromptButtonObject(dialogBox, "CastDefensivelyBtn", new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.53f), new Color(0.2f, 0.35f, 0.2f, 1f), out _aooCastDefensivelyButton, out _aooCastDefensivelyButtonText);
        _aooProceedButton = CreatePromptButton(dialogBox, "ProceedBtn", new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.3f), new Color(0.45f, 0.26f, 0.18f, 1f), out _aooProceedButtonText);
        _aooCancelButton = CreatePromptButton(dialogBox, "CancelBtn", new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.12f), new Color(0.45f, 0.2f, 0.2f, 1f), out _aooCancelButtonText);

        _aooConfirmationPanel.SetActive(false);
    }

    private Text CreatePanelText(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, TextAnchor alignment, FontStyle style)
    {
        Text txt = UIFactory.CreateLabel(
            parent.transform,
            string.Empty,
            fontSize,
            alignment,
            new Color(0.95f, 0.95f, 1f, 1f),
            name,
            UIFactory.GetDefaultFont());

        RectTransform textRT = txt.GetComponent<RectTransform>();
        textRT.anchorMin = anchorMin;
        textRT.anchorMax = anchorMax;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        txt.fontStyle = style;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    private GameObject CreatePromptButtonObject(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, out Button button, out Text text)
    {
        button = UIFactory.CreateButton(
            parent.transform,
            string.Empty,
            null,
            null,
            bgColor,
            name,
            UIFactory.GetDefaultFont(),
            12,
            Color.white);

        UIFactory.ApplyEnhancedCombatButtonStyle(button, bgColor);

        GameObject btnObj = button.gameObject;
        RectTransform btnRT = btnObj.GetComponent<RectTransform>();
        btnRT.anchorMin = anchorMin;
        btnRT.anchorMax = anchorMax;
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;

        text = btnObj.transform.Find("Text")?.GetComponent<Text>();
        if (text == null)
        {
            text = UIFactory.CreateLabel(btnObj.transform, string.Empty, 12, TextAnchor.UpperLeft, Color.white, "Text", UIFactory.GetDefaultFont());
        }

        RectTransform txtRT = text.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0.04f, 0.08f);
        txtRT.anchorMax = new Vector2(0.96f, 0.92f);
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        text.alignment = TextAnchor.UpperLeft;
        text.supportRichText = true;

        return btnObj;
    }

    private Button CreatePromptButton(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, out Text text)
    {
        CreatePromptButtonObject(parent, name, anchorMin, anchorMax, bgColor, out Button btn, out text);
        text.alignment = TextAnchor.UpperLeft;
        return btn;
    }

    private string GetDirectionString(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;

        if (delta.x == 0 && delta.y > 0) return "north";
        if (delta.x == 0 && delta.y < 0) return "south";
        if (delta.x > 0 && delta.y == 0) return "east";
        if (delta.x < 0 && delta.y == 0) return "west";
        if (delta.x > 0 && delta.y > 0) return "northeast";
        if (delta.x > 0 && delta.y < 0) return "southeast";
        if (delta.x < 0 && delta.y > 0) return "northwest";
        if (delta.x < 0 && delta.y < 0) return "southwest";

        return "adjacent";
    }

    // ========================================================================
    // ========================================================================
    // SPELL SELECTION PANEL (with Metamagic Support)
    // ========================================================================

    private GameObject _spellSelectionPanel;
    private System.Action<SpellData> _onSpellSelected;
    private System.Action _onSpellCancelled;
    private SpellcastingComponent _currentSpellComp;

    /// <summary>Current metamagic data being configured for the next spell cast.</summary>
    public MetamagicData PendingMetamagic { get; private set; }

    /// <summary>Callback with metamagic: (spell, metamagic)</summary>
    private System.Action<SpellData, MetamagicData> _onSpellSelectedWithMetamagic;

    /// <summary>
    /// Show the spell selection panel with all castable spells for the current character.
    /// Overload that supports metamagic callback.
    /// </summary>
    public void ShowSpellSelection(SpellcastingComponent spellComp,
        System.Action<SpellData, MetamagicData> onSelect, System.Action onCancel)
    {
        _onSpellSelectedWithMetamagic = onSelect;
        _onSpellSelected = null;
        _onSpellCancelled = onCancel;
        _currentSpellComp = spellComp;
        PendingMetamagic = new MetamagicData();
        ClearSpontaneousCastState();

        if (_spellSelectionPanel != null)
            Destroy(_spellSelectionPanel);

        BuildSpellSelectionPanel(spellComp);
        _spellSelectionPanel.SetActive(true);
    }

    /// <summary>
    /// Show the spell selection panel (legacy overload without metamagic).
    /// </summary>
    public void ShowSpellSelection(SpellcastingComponent spellComp,
        System.Action<SpellData> onSelect, System.Action onCancel)
    {
        _onSpellSelected = onSelect;
        _onSpellSelectedWithMetamagic = null;
        _onSpellCancelled = onCancel;
        _currentSpellComp = spellComp;
        PendingMetamagic = new MetamagicData();
        ClearSpontaneousCastState();

        if (_spellSelectionPanel != null)
            Destroy(_spellSelectionPanel);

        BuildSpellSelectionPanel(spellComp);
        _spellSelectionPanel.SetActive(true);
    }

    public void HideSpellSelection()
    {
        if (_spellSelectionPanel != null)
        {
            Destroy(_spellSelectionPanel);
            _spellSelectionPanel = null;
        }
    }

    private void BuildSpellSelectionPanel(SpellcastingComponent spellComp)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Overlay panel (full-screen darkened background)
        _spellSelectionPanel = new GameObject("SpellSelectionPanel");
        _spellSelectionPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRT = _spellSelectionPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bgImage = _spellSelectionPanel.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.7f);

        CanvasGroup cg = _spellSelectionPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        // Dialog box
        GameObject dialogBox = new GameObject("DialogBox");
        dialogBox.transform.SetParent(_spellSelectionPanel.transform, false);
        RectTransform dialogRT = dialogBox.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.1f, 0.05f);
        dialogRT.anchorMax = new Vector2(0.9f, 0.95f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialogBox.AddComponent<Image>();
        dialogBg.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

        Outline dialogOutline = dialogBox.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.4f, 0.3f, 0.8f, 1f);
        dialogOutline.effectDistance = new Vector2(2, 2);

        // Title — anchored to top of dialog
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialogBox.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = new Vector2(0, -4);
        titleRT.sizeDelta = new Vector2(-20, 30);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        titleText.fontSize = 16;
        titleText.color = new Color(0.8f, 0.7f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;

        bool hasMetamagic = spellComp.HasAnyMetamagicFeat();
        string mmNote = hasMetamagic ? " | ⚡ Metamagic Available" : "";
        int asfChance = (spellComp.Stats != null && spellComp.Stats.IsAffectedByArcaneSpellFailure)
            ? Mathf.Clamp(spellComp.Stats.ArcaneSpellFailure, 0, 100)
            : 0;
        string asfNote = asfChance > 0 ? $" | ⚠ ASF {asfChance}%" : "";
        titleText.text = $"✦ CAST SPELL ✦ — {spellComp.GetSlotSummary()}{mmNote}{asfNote}";

        // Cancel button — anchored to bottom of dialog
        GameObject cancelObj = new GameObject("CancelBtn");
        cancelObj.transform.SetParent(dialogBox.transform, false);
        RectTransform cancelRT = cancelObj.AddComponent<RectTransform>();
        cancelRT.anchorMin = new Vector2(0.3f, 0);
        cancelRT.anchorMax = new Vector2(0.7f, 0);
        cancelRT.pivot = new Vector2(0.5f, 0);
        cancelRT.anchoredPosition = new Vector2(0, 6);
        cancelRT.sizeDelta = new Vector2(0, 36);

        Image cancelImg = cancelObj.AddComponent<Image>();
        cancelImg.color = new Color(0.5f, 0.2f, 0.2f, 1f);

        Button cancelBtn = cancelObj.AddComponent<Button>();
        UIFactory.ApplyEnhancedCombatButtonStyle(cancelBtn, cancelImg.color);

        GameObject cancelTxtObj = new GameObject("Text");
        cancelTxtObj.transform.SetParent(cancelObj.transform, false);
        RectTransform cancelTxtRT = cancelTxtObj.AddComponent<RectTransform>();
        cancelTxtRT.anchorMin = Vector2.zero;
        cancelTxtRT.anchorMax = Vector2.one;
        cancelTxtRT.offsetMin = new Vector2(5, 2);
        cancelTxtRT.offsetMax = new Vector2(-5, -2);

        Text cancelTxt = cancelTxtObj.AddComponent<Text>();
        cancelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cancelTxt.font == null) cancelTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        cancelTxt.fontSize = 14;
        cancelTxt.color = Color.white;
        cancelTxt.alignment = TextAnchor.MiddleCenter;
        cancelTxt.text = "✋ CANCEL";
        cancelTxt.fontStyle = FontStyle.Bold;

        cancelBtn.onClick.AddListener(() =>
        {
            HideSpellSelection();
            _onSpellCancelled?.Invoke();
        });

        // Scroll area — fills space between title and cancel button
        float titleHeight = 34f;
        float cancelHeight = 44f;
        GameObject scrollArea = new GameObject("ScrollArea");
        scrollArea.transform.SetParent(dialogBox.transform, false);
        RectTransform scrollAreaRT = scrollArea.AddComponent<RectTransform>();
        scrollAreaRT.anchorMin = Vector2.zero;
        scrollAreaRT.anchorMax = Vector2.one;
        scrollAreaRT.offsetMin = new Vector2(4, cancelHeight);
        scrollAreaRT.offsetMax = new Vector2(-4, -titleHeight);

        // Viewport with Mask (Color.white + showMaskGraphic=false to avoid invisible content bug)
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        RectTransform vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(4, 4);
        vpRT.offsetMax = new Vector2(-4, -4);
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content container — grows vertically based on spell count
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;

        // ScrollRect component
        ScrollRect scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.content = contentRT;
        scrollRect.viewport = vpRT;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 30f;

        // Vertical scrollbar via helper
        ScrollbarHelper.CreateVerticalScrollbar(scrollRect, scrollArea.transform);

        // Build spell list from PREPARED spells only, grouped by level
        float headerHeight = 24f;
        float buttonHeight = 32f;
        float spacing = 4f;
        float yOffset = 0f; // grows downward from top of content

        int highestLevel = spellComp.GetHighestSlotLevel();
        bool usesSlotSystem = spellComp.Stats != null &&
            (spellComp.Stats.IsWizard || spellComp.Stats.IsCleric) &&
            spellComp.SpellSlots.Count > 0;

        for (int level = 0; level <= highestLevel; level++)
        {
            var preparedAtLevel = spellComp.GetPreparedSpellsByLevel(level);
            int slotsRemaining = spellComp.GetSlotsRemaining(level);
            int slotsMax = spellComp.GetMaxSlots(level);
            bool hasSlotsAvailable = slotsRemaining > 0;

            if (preparedAtLevel.Count == 0) continue;

            // Build section header with slot counts
            string levelName;
            string slotInfo;
            if (level == 0)
            {
                // Cantrips are unlimited
                levelName = "Cantrips";
                slotInfo = "\u221e unlimited";
            }
            else
            {
                string ordinal = level == 1 ? "1st" : level == 2 ? "2nd" : level == 3 ? "3rd" : $"{level}th";
                levelName = $"{ordinal} Level";
                slotInfo = $"{slotsRemaining}/{slotsMax} slots";
            }

            // Cantrips never depleted (unlimited); other levels check slots
            bool isDepleted = (level > 0) && !hasSlotsAvailable;
            string depletedTag = isDepleted ? " [DEPLETED]" : "";
            Color headerColor = !isDepleted ? new Color(0.7f, 0.7f, 0.9f) : new Color(0.6f, 0.3f, 0.3f);

            CreateSpellSectionLabel(content, $"\u2500\u2500 {levelName} ({slotInfo}){depletedTag} \u2500\u2500", yOffset, headerHeight, headerColor);
            yOffset += headerHeight + spacing;

            if (isDepleted)
            {
                CreateSpellSectionLabel(content, "  (No slots available)", yOffset, headerHeight, new Color(0.5f, 0.3f, 0.3f));
                yOffset += headerHeight + spacing;
            }
            else
            {
                foreach (var spell in preparedAtLevel)
                {
                    // Show count of available prepared slots for this spell (both Wizard and Cleric)
                    int preparedCount = usesSlotSystem ? spellComp.CountAvailablePreparedSpell(spell) : 0;

                    // Check if this spell can be spontaneously converted (clerics only, non-domain)
                    bool canConvert = spellComp.CanConvertSpellToSpontaneous(spell);

                    if (canConvert)
                    {
                        // Create a row with the spell button and a Convert button side by side
                        CreateSpellButtonWithConvert(content, spell, yOffset, buttonHeight, spellComp, hasMetamagic, usesSlotSystem, preparedCount);
                    }
                    else
                    {
                        // Standard spell button (full width) — domain spells, non-cleric, etc.
                        CreateSpellButton(content, spell, yOffset, buttonHeight, spellComp, hasMetamagic, usesSlotSystem, preparedCount);
                    }
                    yOffset += buttonHeight + spacing;
                }
            }
        }

        // Set content height based on total spell list size
        contentRT.sizeDelta = new Vector2(0, yOffset + spacing);

        _spellSelectionPanel.SetActive(false);
    }

    /// <summary>
    /// Creates a section label inside the scrollable content area at a given pixel offset from top.
    /// </summary>
    private void CreateSpellSectionLabel(GameObject parent, string label, float yOffset,
        float height, Color? color = null)
    {
        GameObject obj = new GameObject("SectionLabel");
        obj.transform.SetParent(parent.transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -yOffset);
        rt.sizeDelta = new Vector2(-16, height);

        Text txt = obj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        txt.fontSize = 12;
        txt.color = color ?? new Color(0.7f, 0.7f, 0.9f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Italic;
        txt.text = label;
    }

    /// <summary>
    /// Creates a spell button inside the scrollable content area at a given pixel offset from top.
    /// Shows prepared count for slot-based casters (e.g., "Magic Missile ×2" or "∞" for cantrips).
    /// </summary>
    private void CreateSpellButton(GameObject parent, SpellData spell, float yOffset,
        float height, SpellcastingComponent spellComp, bool hasMetamagic,
        bool usesSlotSystem = false, int preparedCount = 0)
    {
        // Determine color based on effect type
        Color btnColor;
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage: btnColor = new Color(0.5f, 0.2f, 0.2f, 1f); break;
            case SpellEffectType.Healing: btnColor = new Color(0.2f, 0.5f, 0.2f, 1f); break;
            case SpellEffectType.Buff: btnColor = new Color(0.2f, 0.3f, 0.6f, 1f); break;
            default: btnColor = new Color(0.3f, 0.3f, 0.4f, 1f); break;
        }

        // Build label
        string rangeStr = spell.RangeSquares == 0 ? "Touch" :
                          spell.RangeSquares < 0 ? "Self" :
                          $"{spell.RangeSquares} sq";
        string effectStr = "";
        if (spell.EffectType == SpellEffectType.Damage)
            effectStr = $" | {spell.DamageCount}d{spell.DamageDice}{(spell.BonusDamage > 0 ? $"+{spell.BonusDamage}" : "")} {spell.DamageType}";
        else if (spell.EffectType == SpellEffectType.Healing)
            effectStr = $" | {spell.HealCount}d{spell.HealDice}+{spell.BonusHealing} HP";
        else if (spell.EffectType == SpellEffectType.Buff)
            effectStr = $" | +{spell.BuffACBonus} AC";

        // Show count: ∞ for cantrips, ×N for level 1+ spells with multiple prepared
        string countStr = "";
        if (usesSlotSystem)
        {
            if (spell.SpellLevel == 0)
                countStr = " \u221e"; // ∞ for cantrips
            else if (preparedCount > 1)
                countStr = $" \u00d7{preparedCount}"; // ×N for multiple
        }
        int asfChance = (spellComp != null && spellComp.Stats != null && spellComp.Stats.IsAffectedByArcaneSpellFailure)
            ? Mathf.Clamp(spellComp.Stats.ArcaneSpellFailure, 0, 100)
            : 0;
        string asfWarn = asfChance > 0 ? $" ⚠ASF {asfChance}%" : "";

        string label = $"{spell.Name}{countStr}{asfWarn} ({rangeStr}){effectStr}";

        // Create button using pixel-based positioning within scroll content
        string prefix = hasMetamagic ? "⚡ " : "";

        GameObject btnObj = new GameObject(spell.SpellId);
        btnObj.transform.SetParent(parent.transform, false);
        RectTransform btnRT = btnObj.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0, 1);
        btnRT.anchorMax = new Vector2(1, 1);
        btnRT.pivot = new Vector2(0.5f, 1);
        btnRT.anchoredPosition = new Vector2(0, -yOffset);
        btnRT.sizeDelta = new Vector2(-16, height);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = btnColor;

        Button btn = btnObj.AddComponent<Button>();
        UIFactory.ApplyEnhancedCombatButtonStyle(btn, btnColor);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(8, 2);
        txtRT.offsetMax = new Vector2(-8, -2);

        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = $"{prefix}{label}";
        txt.fontStyle = FontStyle.Bold;

        // Wire up click handler
        if (hasMetamagic)
        {
            SpellData capturedSpell = spell;
            btn.onClick.AddListener(() =>
            {
                ShowMetamagicPanel(capturedSpell, spellComp);
            });
        }
        else
        {
            SpellData capturedSpell = spell;
            btn.onClick.AddListener(() =>
            {
                HideSpellSelection();
                if (_onSpellSelectedWithMetamagic != null)
                    _onSpellSelectedWithMetamagic.Invoke(capturedSpell, null);
                else
                    _onSpellSelected?.Invoke(capturedSpell);
            });
        }
    }

    /// <summary>
    /// Creates a spell button with an adjacent "Convert to Cure/Inflict" button for clerics.
    /// D&D 3.5e: Clerics can convert a specific prepared spell (except domain spells)
    /// into a cure/inflict spell of the same level.
    /// The spell button takes ~72% width; the convert button takes ~28% width.
    /// </summary>
    private void CreateSpellButtonWithConvert(GameObject parent, SpellData spell, float yOffset,
        float height, SpellcastingComponent spellComp, bool hasMetamagic,
        bool usesSlotSystem = false, int preparedCount = 0)
    {
        // Create a container row for both buttons
        GameObject rowObj = new GameObject($"SpellRow_{spell.SpellId}");
        rowObj.transform.SetParent(parent.transform, false);
        RectTransform rowRT = rowObj.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot = new Vector2(0.5f, 1);
        rowRT.anchoredPosition = new Vector2(0, -yOffset);
        rowRT.sizeDelta = new Vector2(-16, height);

        // === SPELL BUTTON (left side, ~72% width) ===
        Color btnColor;
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage: btnColor = new Color(0.5f, 0.2f, 0.2f, 1f); break;
            case SpellEffectType.Healing: btnColor = new Color(0.2f, 0.5f, 0.2f, 1f); break;
            case SpellEffectType.Buff: btnColor = new Color(0.2f, 0.3f, 0.6f, 1f); break;
            default: btnColor = new Color(0.3f, 0.3f, 0.4f, 1f); break;
        }

        string rangeStr = spell.RangeSquares == 0 ? "Touch" :
                          spell.RangeSquares < 0 ? "Self" :
                          $"{spell.RangeSquares} sq";
        string effectStr = "";
        if (spell.EffectType == SpellEffectType.Damage)
            effectStr = $" | {spell.DamageCount}d{spell.DamageDice}{(spell.BonusDamage > 0 ? $"+{spell.BonusDamage}" : "")} {spell.DamageType}";
        else if (spell.EffectType == SpellEffectType.Healing)
            effectStr = $" | {spell.HealCount}d{spell.HealDice}+{spell.BonusHealing} HP";
        else if (spell.EffectType == SpellEffectType.Buff)
            effectStr = $" | +{spell.BuffACBonus} AC";

        string countStr = "";
        if (usesSlotSystem)
        {
            if (spell.SpellLevel == 0)
                countStr = " \u221e";
            else if (preparedCount > 1)
                countStr = $" \u00d7{preparedCount}";
        }
        int asfChance = (spellComp != null && spellComp.Stats != null && spellComp.Stats.IsAffectedByArcaneSpellFailure)
            ? Mathf.Clamp(spellComp.Stats.ArcaneSpellFailure, 0, 100)
            : 0;
        string asfWarn = asfChance > 0 ? $" ⚠ASF {asfChance}%" : "";

        string spellLabel = $"{spell.Name}{countStr}{asfWarn} ({rangeStr}){effectStr}";
        string prefix = hasMetamagic ? "⚡ " : "";

        GameObject spellBtnObj = new GameObject($"SpellBtn_{spell.SpellId}");
        spellBtnObj.transform.SetParent(rowObj.transform, false);
        RectTransform spellBtnRT = spellBtnObj.AddComponent<RectTransform>();
        spellBtnRT.anchorMin = new Vector2(0, 0);
        spellBtnRT.anchorMax = new Vector2(0.72f, 1);
        spellBtnRT.offsetMin = Vector2.zero;
        spellBtnRT.offsetMax = new Vector2(-1, 0);

        Image spellBtnImg = spellBtnObj.AddComponent<Image>();
        spellBtnImg.color = btnColor;

        Button spellBtn = spellBtnObj.AddComponent<Button>();
        UIFactory.ApplyEnhancedCombatButtonStyle(spellBtn, btnColor);

        GameObject spellTxtObj = new GameObject("Text");
        spellTxtObj.transform.SetParent(spellBtnObj.transform, false);
        RectTransform spellTxtRT = spellTxtObj.AddComponent<RectTransform>();
        spellTxtRT.anchorMin = Vector2.zero;
        spellTxtRT.anchorMax = Vector2.one;
        spellTxtRT.offsetMin = new Vector2(6, 2);
        spellTxtRT.offsetMax = new Vector2(-4, -2);

        Text spellTxt = spellTxtObj.AddComponent<Text>();
        spellTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (spellTxt.font == null) spellTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        spellTxt.fontSize = 13;
        spellTxt.color = Color.white;
        spellTxt.alignment = TextAnchor.MiddleLeft;
        spellTxt.text = $"{prefix}{spellLabel}";
        spellTxt.fontStyle = FontStyle.Bold;

        // Wire spell button click handler (same as normal spell button)
        if (hasMetamagic)
        {
            SpellData capturedSpell = spell;
            spellBtn.onClick.AddListener(() =>
            {
                ShowMetamagicPanel(capturedSpell, spellComp);
            });
        }
        else
        {
            SpellData capturedSpell = spell;
            spellBtn.onClick.AddListener(() =>
            {
                HideSpellSelection();
                if (_onSpellSelectedWithMetamagic != null)
                    _onSpellSelectedWithMetamagic.Invoke(capturedSpell, null);
                else
                    _onSpellSelected?.Invoke(capturedSpell);
            });
        }

        // === CONVERT BUTTON (right side, ~28% width) ===
        bool isCure = spellComp.Stats.SpontaneousCasting == SpontaneousCastingType.Cure;
        Color convertColor = isCure
            ? new Color(0.2f, 0.45f, 0.25f, 1f)    // green for cure
            : new Color(0.45f, 0.15f, 0.35f, 1f);   // dark purple for inflict

        SpellData spontSpell = spellComp.GetSpontaneousSpell(spell.SpellLevel);
        string convertLabel = isCure ? "⟳ Convert\nto Cure" : "⟳ Convert\nto Inflict";

        GameObject convertBtnObj = new GameObject($"ConvertBtn_{spell.SpellId}");
        convertBtnObj.transform.SetParent(rowObj.transform, false);
        RectTransform convertBtnRT = convertBtnObj.AddComponent<RectTransform>();
        convertBtnRT.anchorMin = new Vector2(0.72f, 0);
        convertBtnRT.anchorMax = new Vector2(1, 1);
        convertBtnRT.offsetMin = new Vector2(1, 0);
        convertBtnRT.offsetMax = Vector2.zero;

        Image convertBtnImg = convertBtnObj.AddComponent<Image>();
        convertBtnImg.color = convertColor;

        Button convertBtn = convertBtnObj.AddComponent<Button>();
        UIFactory.ApplyEnhancedCombatButtonStyle(convertBtn, convertColor);

        GameObject convertTxtObj = new GameObject("Text");
        convertTxtObj.transform.SetParent(convertBtnObj.transform, false);
        RectTransform convertTxtRT = convertTxtObj.AddComponent<RectTransform>();
        convertTxtRT.anchorMin = Vector2.zero;
        convertTxtRT.anchorMax = Vector2.one;
        convertTxtRT.offsetMin = new Vector2(2, 1);
        convertTxtRT.offsetMax = new Vector2(-2, -1);

        Text convertTxt = convertTxtObj.AddComponent<Text>();
        convertTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (convertTxt.font == null) convertTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        convertTxt.fontSize = 10;
        convertTxt.color = Color.white;
        convertTxt.alignment = TextAnchor.MiddleCenter;
        convertTxt.text = convertLabel;
        convertTxt.fontStyle = FontStyle.BoldAndItalic;

        // Wire convert button click handler — sacrifices this specific spell
        SpellData capturedSpontSpell = spontSpell;
        string capturedSacrificeId = spell.SpellId;
        int capturedLevel = spell.SpellLevel;
        convertBtn.onClick.AddListener(() =>
        {
            HideSpellSelection();
            // Mark as spontaneous cast with the specific spell being sacrificed
            _isSpontaneousCast = true;
            _spontaneousCastLevel = capturedLevel;
            _spontaneousSacrificedSpellId = capturedSacrificeId;
            if (_onSpellSelectedWithMetamagic != null)
                _onSpellSelectedWithMetamagic.Invoke(capturedSpontSpell, null);
            else
                _onSpellSelected?.Invoke(capturedSpontSpell);
        });
    }

    /// <summary>Whether the last spell selection was a spontaneous cast.</summary>
    public bool IsSpontaneousCast => _isSpontaneousCast;
    private bool _isSpontaneousCast;

    /// <summary>The spell level being consumed for spontaneous casting.</summary>
    public int SpontaneousCastLevel => _spontaneousCastLevel;
    private int _spontaneousCastLevel;

    /// <summary>The SpellId of the specific prepared spell being sacrificed for spontaneous conversion.</summary>
    public string SpontaneousSacrificedSpellId => _spontaneousSacrificedSpellId;
    private string _spontaneousSacrificedSpellId;

    /// <summary>Reset spontaneous casting state (called after spell is cast).</summary>
    public void ClearSpontaneousCastState()
    {
        _isSpontaneousCast = false;
        _spontaneousCastLevel = -1;
        _spontaneousSacrificedSpellId = null;
    }

    // ========================================================================
    // METAMAGIC SELECTION SUB-PANEL
    // ========================================================================

    private GameObject _metamagicPanel;
    private MetamagicData _tempMetamagic;
    private Text _metamagicInfoText;
    private List<Image> _metamagicToggleImages = new List<Image>();
    private List<Text> _metamagicToggleTexts = new List<Text>();
    private List<MetamagicFeatId> _metamagicToggleIds = new List<MetamagicFeatId>();

    /// <summary>
    /// Show the metamagic options sub-panel for a selected spell.
    /// </summary>
    private void ShowMetamagicPanel(SpellData spell, SpellcastingComponent spellComp)
    {
        // Clean up any existing metamagic panel
        if (_metamagicPanel != null)
            Destroy(_metamagicPanel);

        _tempMetamagic = new MetamagicData();
        _metamagicToggleImages.Clear();
        _metamagicToggleTexts.Clear();
        _metamagicToggleIds.Clear();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Overlay on top of spell selection
        _metamagicPanel = new GameObject("MetamagicPanel");
        _metamagicPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRT = _metamagicPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bgImage = _metamagicPanel.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.8f);

        CanvasGroup cg = _metamagicPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        // Dialog box
        GameObject dialogBox = new GameObject("MetamagicDialog");
        dialogBox.transform.SetParent(_metamagicPanel.transform, false);
        RectTransform dialogRT = dialogBox.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.08f, 0.05f);
        dialogRT.anchorMax = new Vector2(0.92f, 0.95f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialogBox.AddComponent<Image>();
        dialogBg.color = new Color(0.08f, 0.06f, 0.18f, 0.98f);

        Outline dialogOutline = dialogBox.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.6f, 0.4f, 1f, 1f);
        dialogOutline.effectDistance = new Vector2(2, 2);

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialogBox.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.91f);
        titleRT.anchorMax = new Vector2(0.95f, 0.99f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        titleText.fontSize = 15;
        titleText.color = new Color(1f, 0.85f, 0.4f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        titleText.text = $"⚡ METAMAGIC — {spell.Name} (Lv {spell.SpellLevel}) ⚡";

        // Info text: shows current effective level and slot cost
        GameObject infoObj = new GameObject("InfoText");
        infoObj.transform.SetParent(dialogBox.transform, false);
        RectTransform infoRT = infoObj.AddComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.05f, 0.84f);
        infoRT.anchorMax = new Vector2(0.95f, 0.91f);
        infoRT.offsetMin = Vector2.zero;
        infoRT.offsetMax = Vector2.zero;

        _metamagicInfoText = infoObj.AddComponent<Text>();
        _metamagicInfoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_metamagicInfoText.font == null) _metamagicInfoText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        _metamagicInfoText.fontSize = 12;
        _metamagicInfoText.color = new Color(0.9f, 0.9f, 0.7f);
        _metamagicInfoText.alignment = TextAnchor.MiddleCenter;

        // Build metamagic toggle buttons
        var knownMM = spellComp.GetKnownMetamagicFeats();
        float yPos = 0.80f;
        float mmStep = 0.065f;

        foreach (var mmId in knownMM)
        {
            bool applicable = MetamagicData.IsApplicable(mmId, spell);

            // D&D 3.5e: Only one quickened spell per round
            bool quickenBlocked = false;
            if (mmId == MetamagicFeatId.QuickenSpell && !spellComp.CanUseQuickenSpell())
            {
                applicable = false;
                quickenBlocked = true;
            }

            CreateMetamagicToggleButton(dialogBox, mmId, spell, spellComp, yPos, applicable, quickenBlocked);
            yPos -= mmStep;
        }

        // Update info text initially
        UpdateMetamagicInfoText(spell, spellComp);

        // "Cast with no metamagic" button
        float btnY = Mathf.Max(yPos - 0.02f, 0.12f);
        Button castNormalBtn = CreateSpellDialogButton(dialogBox, "CastNormal",
            $"✨ Cast {spell.Name} (No Metamagic)",
            new Vector2(0.05f, btnY - 0.04f), new Vector2(0.95f, btnY + 0.02f),
            new Color(0.2f, 0.4f, 0.3f, 1f));
        SpellData capturedSpell1 = spell;
        castNormalBtn.onClick.AddListener(() =>
        {
            if (_metamagicPanel != null) Destroy(_metamagicPanel);
            HideSpellSelection();
            if (_onSpellSelectedWithMetamagic != null)
                _onSpellSelectedWithMetamagic.Invoke(capturedSpell1, null);
            else
                _onSpellSelected?.Invoke(capturedSpell1);
        });

        // "Cast with metamagic" button
        float castMMY = btnY - 0.08f;
        Button castMMBtn = CreateSpellDialogButton(dialogBox, "CastMM",
            $"⚡ Cast with Metamagic",
            new Vector2(0.05f, castMMY - 0.04f), new Vector2(0.95f, castMMY + 0.02f),
            new Color(0.4f, 0.2f, 0.5f, 1f));
        SpellData capturedSpell2 = spell;
        SpellcastingComponent capturedComp = spellComp;
        castMMBtn.onClick.AddListener(() =>
        {
            if (!_tempMetamagic.HasAnyMetamagic)
            {
                // No metamagic selected, cast normally
                if (_metamagicPanel != null) Destroy(_metamagicPanel);
                HideSpellSelection();
                if (_onSpellSelectedWithMetamagic != null)
                    _onSpellSelectedWithMetamagic.Invoke(capturedSpell2, null);
                else
                    _onSpellSelected?.Invoke(capturedSpell2);
                return;
            }

            // Check if caster has a slot at the effective level
            int effectiveLevel = _tempMetamagic.GetEffectiveSpellLevel(capturedSpell2.SpellLevel);
            if (!capturedComp.HasSlotAtLevel(effectiveLevel))
            {
                Debug.Log($"[Metamagic] No slot available at level {effectiveLevel}!");
                // Flash info text red
                if (_metamagicInfoText != null)
                    _metamagicInfoText.color = Color.red;
                return;
            }

            MetamagicData finalMM = _tempMetamagic;
            if (_metamagicPanel != null) Destroy(_metamagicPanel);
            HideSpellSelection();
            if (_onSpellSelectedWithMetamagic != null)
                _onSpellSelectedWithMetamagic.Invoke(capturedSpell2, finalMM);
            else
                _onSpellSelected?.Invoke(capturedSpell2);
        });

        // Back button
        float backY = castMMY - 0.08f;
        Button backBtn = CreateSpellDialogButton(dialogBox, "BackBtn", "← Back to Spells",
            new Vector2(0.25f, backY - 0.04f), new Vector2(0.75f, backY + 0.02f),
            new Color(0.4f, 0.3f, 0.2f, 1f));
        backBtn.onClick.AddListener(() =>
        {
            if (_metamagicPanel != null) Destroy(_metamagicPanel);
        });
    }

    private void CreateMetamagicToggleButton(GameObject parent, MetamagicFeatId mmId,
        SpellData spell, SpellcastingComponent spellComp, float yPos, bool applicable,
        bool quickenBlocked = false)
    {
        string displayName = MetamagicData.GetDisplayName(mmId);
        string effect = MetamagicData.GetShortEffect(mmId);
        string label;

        if (quickenBlocked)
        {
            // Show clear message that quickened spell limit is reached
            label = $"[✗] {displayName}: Already cast a quickened spell this round";
        }
        else
        {
            label = $"[ ] {displayName}: {effect}";
        }

        Color btnColor = applicable
            ? new Color(0.25f, 0.2f, 0.4f, 1f)
            : quickenBlocked
                ? new Color(0.3f, 0.15f, 0.15f, 0.6f) // Red-tinted for quicken blocked
                : new Color(0.2f, 0.2f, 0.2f, 0.6f);

        GameObject btnObj = new GameObject($"MM_{mmId}");
        btnObj.transform.SetParent(parent.transform, false);
        RectTransform btnRT = btnObj.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.03f, yPos - 0.028f);
        btnRT.anchorMax = new Vector2(0.97f, yPos + 0.028f);
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = btnColor;

        int toggleIdx = _metamagicToggleImages.Count;
        _metamagicToggleImages.Add(btnImg);
        _metamagicToggleIds.Add(mmId);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(8, 1);
        txtRT.offsetMax = new Vector2(-8, -1);

        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        txt.fontSize = 12;
        txt.color = applicable ? Color.white :
                   quickenBlocked ? new Color(0.8f, 0.3f, 0.3f) : // Red text for quicken blocked
                   new Color(0.5f, 0.5f, 0.5f);
        txt.alignment = TextAnchor.MiddleLeft;
        txt.text = label;
        txt.fontStyle = FontStyle.Bold;

        _metamagicToggleTexts.Add(txt);

        if (applicable)
        {
            Button btn = btnObj.AddComponent<Button>();
            UIFactory.ApplyEnhancedCombatButtonStyle(btn, btnColor);

            MetamagicFeatId capturedId = mmId;
            SpellData capturedSpell = spell;
            SpellcastingComponent capturedComp = spellComp;
            int capturedIdx = toggleIdx;
            btn.onClick.AddListener(() =>
            {
                _tempMetamagic.Toggle(capturedId);
                RefreshMetamagicToggle(capturedIdx, _tempMetamagic.Has(capturedId));
                UpdateMetamagicInfoText(capturedSpell, capturedComp);
            });
        }
    }

    private void RefreshMetamagicToggle(int idx, bool active)
    {
        if (idx < 0 || idx >= _metamagicToggleImages.Count) return;

        if (active)
        {
            _metamagicToggleImages[idx].color = new Color(0.3f, 0.15f, 0.5f, 1f);
            string currentText = _metamagicToggleTexts[idx].text;
            if (currentText.StartsWith("[ ]"))
                _metamagicToggleTexts[idx].text = "[✓]" + currentText.Substring(3);
            _metamagicToggleTexts[idx].color = new Color(1f, 0.9f, 0.4f);
        }
        else
        {
            _metamagicToggleImages[idx].color = new Color(0.25f, 0.2f, 0.4f, 1f);
            string currentText = _metamagicToggleTexts[idx].text;
            if (currentText.StartsWith("[✓]"))
                _metamagicToggleTexts[idx].text = "[ ]" + currentText.Substring(3);
            _metamagicToggleTexts[idx].color = Color.white;
        }
    }

    private void UpdateMetamagicInfoText(SpellData spell, SpellcastingComponent spellComp)
    {
        if (_metamagicInfoText == null) return;

        int baseLvl = spell.SpellLevel;
        int effectiveLvl = _tempMetamagic.GetEffectiveSpellLevel(baseLvl);
        bool hasSlot = spellComp.HasSlotAtLevel(effectiveLvl);
        int maxSlot = spellComp.GetHighestSlotLevel();

        string slotStatus = hasSlot
            ? $"<color=#80ff80>Slot Available (Lv{effectiveLvl})</color>"
            : $"<color=#ff6060>No Lv{effectiveLvl} Slot! (Max: Lv{maxSlot})</color>";

        if (!_tempMetamagic.HasAnyMetamagic)
        {
            _metamagicInfoText.text = $"Base Spell Level: {baseLvl} | Select metamagic feats to apply";
            _metamagicInfoText.color = new Color(0.9f, 0.9f, 0.7f);
        }
        else
        {
            _metamagicInfoText.text = $"Lv{baseLvl} → Lv{effectiveLvl} slot needed | {slotStatus}";
            _metamagicInfoText.supportRichText = true;
            _metamagicInfoText.color = hasSlot ? new Color(0.9f, 0.9f, 0.7f) : new Color(1f, 0.5f, 0.5f);
        }
    }

    // ========================================================================
    // TOUCH SPELL PROMPT (Cast Now vs Discharge Later)
    // ========================================================================

    public void ShowTouchSpellPrompt(SpellData spell, System.Action onCastNow, System.Action onDischargeLater, System.Action onCancel)
    {
        HideTouchSpellPrompt();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            onCastNow?.Invoke();
            return;
        }

        _touchSpellPromptPanel = new GameObject("TouchSpellPromptPanel");
        _touchSpellPromptPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = _touchSpellPromptPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image overlay = _touchSpellPromptPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);

        CanvasGroup cg = _touchSpellPromptPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(_touchSpellPromptPanel.transform, false);
        RectTransform dialogRT = dialog.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.28f, 0.36f);
        dialogRT.anchorMax = new Vector2(0.72f, 0.68f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.1f, 0.1f, 0.18f, 0.97f);
        Outline outline = dialog.AddComponent<Outline>();
        outline.effectColor = new Color(0.65f, 0.55f, 0.95f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.8f);
        titleRT.anchorMax = new Vector2(0.95f, 0.96f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.9f, 0.85f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = "TOUCH SPELL OPTIONS";

        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(dialog.transform, false);
        RectTransform bodyRT = bodyObj.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0.08f, 0.36f);
        bodyRT.anchorMax = new Vector2(0.92f, 0.78f);
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = Vector2.zero;

        Text bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bodyText.font == null) bodyText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        bodyText.fontSize = 15;
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.color = Color.white;
        bodyText.text = $"You are casting: {spell.Name}\n\nHow do you want to cast this spell?";

        Button castNowBtn = CreateTouchPromptButton(dialog, "CastNow", "Cast Now", new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.28f), new Color(0.2f, 0.45f, 0.25f, 1f));
        castNowBtn.onClick.AddListener(() =>
        {
            HideTouchSpellPrompt();
            onCastNow?.Invoke();
        });

        Button dischargeBtn = CreateTouchPromptButton(dialog, "DischargeLater", "Discharge Later", new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.28f), new Color(0.35f, 0.2f, 0.55f, 1f));
        dischargeBtn.onClick.AddListener(() =>
        {
            HideTouchSpellPrompt();
            onDischargeLater?.Invoke();
        });

        Button cancelBtn = CreateTouchPromptButton(dialog, "Cancel", "Cancel", new Vector2(0.38f, 0.01f), new Vector2(0.62f, 0.08f), new Color(0.45f, 0.2f, 0.2f, 1f));
        cancelBtn.onClick.AddListener(() =>
        {
            HideTouchSpellPrompt();
            onCancel?.Invoke();
        });
    }

    public void HideTouchSpellPrompt()
    {
        if (_touchSpellPromptPanel != null)
        {
            Destroy(_touchSpellPromptPanel);
            _touchSpellPromptPanel = null;
        }
    }
    // ========================================================================
    // SUMMON CREATURE SELECTION PROMPT
    // ========================================================================

    public void ShowSummonCreatureSelection(string spellName, List<string> creatureOptions,
        System.Action<int> onSelect, System.Action onCancel)
    {
        HideSummonCreatureSelection();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            onCancel?.Invoke();
            return;
        }

        _summonSelectionPanel = new GameObject("SummonSelectionPanel");
        _summonSelectionPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = _summonSelectionPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image overlay = _summonSelectionPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.75f);

        CanvasGroup cg = _summonSelectionPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(_summonSelectionPanel.transform, false);
        RectTransform dialogRT = dialog.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.24f, 0.22f);
        dialogRT.anchorMax = new Vector2(0.76f, 0.78f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.12f, 0.12f, 0.2f, 0.97f);
        Outline outline = dialog.AddComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.55f, 1f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.86f);
        titleRT.anchorMax = new Vector2(0.95f, 0.98f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.92f, 0.88f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = $"SUMMON CREATURE — {spellName}";

        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(dialog.transform, false);
        RectTransform bodyRT = bodyObj.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0.08f, 0.76f);
        bodyRT.anchorMax = new Vector2(0.92f, 0.86f);
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = Vector2.zero;

        Text bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bodyText.font == null) bodyText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        bodyText.fontSize = 14;
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.color = new Color(0.9f, 0.95f, 1f);
        bodyText.text = "Choose one creature to summon:";

        if (creatureOptions == null)
            creatureOptions = new List<string>();

        float optionsTop = 0.72f;
        float optionsBottom = 0.2f;
        int optionCount = Mathf.Max(1, creatureOptions.Count);
        float totalHeight = optionsTop - optionsBottom;
        float step = totalHeight / optionCount;

        for (int i = 0; i < creatureOptions.Count; i++)
        {
            int optionIndex = i;
            float yMax = optionsTop - (step * i);
            float yMin = yMax - (step * 0.82f);

            Button optionBtn = CreateTouchPromptButton(dialog,
                $"Option_{i}",
                creatureOptions[i],
                new Vector2(0.1f, yMin),
                new Vector2(0.9f, yMax),
                new Color(0.28f, 0.26f, 0.58f, 1f));

            optionBtn.onClick.AddListener(() =>
            {
                HideSummonCreatureSelection();
                onSelect?.Invoke(optionIndex);
            });
        }

        Button cancelBtn = CreateTouchPromptButton(dialog, "Cancel", "Cancel",
            new Vector2(0.35f, 0.06f), new Vector2(0.65f, 0.16f),
            new Color(0.45f, 0.2f, 0.2f, 1f));
        cancelBtn.onClick.AddListener(() =>
        {
            HideSummonCreatureSelection();
            onCancel?.Invoke();
        });
    }


    // ========================================================================
    // ========================================================================
    // SUMMON CONTEXT MENU
    // ========================================================================

    public void ShowSummonContextMenu(
        CharacterController summon,
        int remainingRounds,
        int totalRounds,
        SummonCommand currentCommand,
        System.Action onAttackNearest,
        System.Action onProtectCaster,
        System.Action onDismiss)
    {
        HideSummonContextMenu();

        if (summon == null || summon.Stats == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        _summonContextMenuRoot = new GameObject("SummonContextMenuRoot");
        _summonContextMenuRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRT = _summonContextMenuRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image rootBg = _summonContextMenuRoot.AddComponent<Image>();
        rootBg.color = new Color(0f, 0f, 0f, 0.02f);

        Button clickOutsideBtn = _summonContextMenuRoot.AddComponent<Button>();
        clickOutsideBtn.transition = Selectable.Transition.None;
        clickOutsideBtn.onClick.AddListener(HideSummonContextMenu);

        GameObject panel = new GameObject("SummonContextMenuPanel");
        panel.transform.SetParent(_summonContextMenuRoot.transform, false);

        RectTransform panelRT = panel.AddComponent<RectTransform>();
        panelRT.pivot = new Vector2(0f, 1f);
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);

        const float menuWidth = 300f;
        const float menuHeight = 260f;
        const float edgeMargin = 10f;
        panelRT.sizeDelta = new Vector2(menuWidth, menuHeight);

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Camera worldCamera = uiCamera != null ? uiCamera : Camera.main;

        Vector3 summonWorldPos = summon.transform.position;
        Vector3 summonScreenPos = worldCamera != null
            ? worldCamera.WorldToScreenPoint(summonWorldPos)
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        if (summonScreenPos.z < 0f)
        {
            summonScreenPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        Vector2 offset = new Vector2(100f, 50f);

        if (summonScreenPos.x + menuWidth + offset.x > Screen.width - edgeMargin)
            offset.x = -menuWidth - 20f;

        if (summonScreenPos.y + menuHeight + offset.y > Screen.height - edgeMargin)
            offset.y = -menuHeight - 20f;

        if (summonScreenPos.x + offset.x < edgeMargin)
            offset.x = 20f;

        if (summonScreenPos.y + offset.y < edgeMargin)
            offset.y = 20f;

        Vector2 menuScreenPos = new Vector2(summonScreenPos.x + offset.x, summonScreenPos.y + offset.y);
        menuScreenPos.x = Mathf.Clamp(menuScreenPos.x, edgeMargin, Screen.width - menuWidth - edgeMargin);
        menuScreenPos.y = Mathf.Clamp(menuScreenPos.y, edgeMargin, Screen.height - edgeMargin);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, menuScreenPos, uiCamera, out Vector2 localPoint);

        float minX = canvasRect.rect.xMin + edgeMargin;
        float maxX = canvasRect.rect.xMax - menuWidth - edgeMargin;
        float minY = canvasRect.rect.yMin + menuHeight + edgeMargin;
        float maxY = canvasRect.rect.yMax - edgeMargin;

        panelRT.anchoredPosition = new Vector2(
            Mathf.Clamp(localPoint.x, minX, maxX),
            Mathf.Clamp(localPoint.y, minY, maxY));

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.1f, 0.16f, 0.96f);
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.38f, 0.72f, 1f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 6f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        string roundsWord = remainingRounds == 1 ? "round" : "rounds";
        string cmdDesc = currentCommand != null ? currentCommand.Description : SummonCommand.AttackNearest().Description;

        Text header = CreateMenuText(panel, "Header",
            $"{summon.Stats.CharacterName} [S]\nHP: {summon.Stats.CurrentHP}/{Mathf.Max(1, summon.Stats.TotalMaxHP)}  AC: {summon.Stats.ArmorClass}\nDuration: {remainingRounds}/{Mathf.Max(1, totalRounds)} {roundsWord}\nCurrent: {cmdDesc}",
            12,
            FontStyle.Bold,
            new Color(0.92f, 0.97f, 1f, 1f),
            TextAnchor.MiddleLeft);
        var headerLE = header.gameObject.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 78f;

        Text cmdLabel = CreateMenuText(panel, "CommandLabel", "COMMANDS", 11, FontStyle.Bold,
            new Color(0.7f, 0.88f, 1f, 1f), TextAnchor.MiddleLeft);
        var cmdLE = cmdLabel.gameObject.AddComponent<LayoutElement>();
        cmdLE.preferredHeight = 16f;

        string attackLabel = currentCommand != null && currentCommand.Type == SummonCommandType.AttackNearest
            ? "Attack Nearest Enemy ✓"
            : "Attack Nearest Enemy";
        Button attackBtn = CreateContextMenuButton(panel, attackLabel, new Color(0.18f, 0.33f, 0.5f, 1f));
        attackBtn.onClick.AddListener(() =>
        {
            HideSummonContextMenu();
            onAttackNearest?.Invoke();
        });

        string protectLabel = currentCommand != null && currentCommand.Type == SummonCommandType.ProtectCaster
            ? "Protect Me ✓"
            : "Protect Me";
        Button protectBtn = CreateContextMenuButton(panel, protectLabel, new Color(0.18f, 0.4f, 0.32f, 1f));
        protectBtn.onClick.AddListener(() =>
        {
            HideSummonContextMenu();
            onProtectCaster?.Invoke();
        });

        Button dismissBtn = CreateContextMenuButton(panel, "Dismiss Summon", new Color(0.45f, 0.2f, 0.2f, 1f));
        dismissBtn.onClick.AddListener(() =>
        {
            HideSummonContextMenu();
            onDismiss?.Invoke();
        });

        Button cancelBtn = CreateContextMenuButton(panel, "Cancel", new Color(0.22f, 0.22f, 0.28f, 1f));
        cancelBtn.onClick.AddListener(HideSummonContextMenu);
    }

    public void HideSummonContextMenu()
    {
        if (_summonContextMenuRoot != null)
        {
            Destroy(_summonContextMenuRoot);
            _summonContextMenuRoot = null;
        }
    }

    private Text CreateMenuText(GameObject parent, string name, string textValue, int fontSize, FontStyle fontStyle, Color color, TextAnchor align)
    {
        Text txt = UIFactory.CreateLabel(parent.transform, textValue, fontSize, align, color, name, UIFactory.GetDefaultFont());
        txt.fontStyle = fontStyle;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    private Button CreateContextMenuButton(GameObject parent, string label, Color bgColor)
    {
        Button btn = UIFactory.CreateButton(
            parent.transform,
            label,
            null,
            new Vector2(0f, 30f),
            bgColor,
            $"CtxBtn_{label}",
            UIFactory.GetDefaultFont(),
            13,
            new Color(0.94f, 0.97f, 1f, 1f));

        UIFactory.ApplyEnhancedCombatButtonStyle(btn, bgColor);

        LayoutElement le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 30f;

        Text txt = btn.transform.Find("Text")?.GetComponent<Text>();
        if (txt != null)
        {
            txt.alignment = TextAnchor.MiddleLeft;
            RectTransform txtRT = txt.GetComponent<RectTransform>();
            txtRT.offsetMin = new Vector2(8f, 0f);
            txtRT.offsetMax = new Vector2(-8f, 0f);
        }

        return btn;
    }

    /// <summary>
    /// Backward-compatible helper that now routes through the unified AoO prompt.
    /// callback(true) = cast defensively, callback(false) = cast normally.
    /// </summary>
    public void ShowCastDefensivelyPrompt(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> threateningEnemies,
        System.Action<bool> callback)
    {
        int defensiveDC = 15 + (spell != null ? spell.SpellLevel : 0);
        int concentrationBonus = (caster != null && caster.Stats != null)
            ? caster.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: true)
            : 0;
        int requiredRoll = defensiveDC - concentrationBonus;
        float successChance = Mathf.Clamp((21 - requiredRoll) / 20f * 100f, 5f, 95f);

        var info = new AoOProvokingActionInfo
        {
            ActionType = AoOProvokingAction.CastSpell,
            ActionName = spell != null ? $"CAST {spell.Name.ToUpper()}" : "CAST SPELL",
            ActionDescription = spell != null ? $"Casting {spell.Name}" : "Casting spell",
            Actor = caster,
            ThreateningEnemies = threateningEnemies ?? new List<CharacterController>(),
            Spell = spell,
            CastDefensivelyDC = defensiveDC,
            ConcentrationBonus = concentrationBonus,
            SuccessChance = successChance,
            OnCastDefensively = () => callback?.Invoke(true),
            OnProceed = () => callback?.Invoke(false),
            OnCancel = () => callback?.Invoke(false)
        };

        ShowAoOConfirmationPrompt(info);
    }
    public void ShowConfirmationDialog(string title, string message,
        string confirmLabel, string cancelLabel,
        System.Action onConfirm, System.Action onCancel)
    {
        if (_confirmationPanel != null)
            Destroy(_confirmationPanel);

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            onCancel?.Invoke();
            return;
        }

        _confirmationPanel = new GameObject("ConfirmationPanel");
        _confirmationPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRT = _confirmationPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image overlay = _confirmationPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(_confirmationPanel.transform, false);
        RectTransform dialogRT = dialog.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.33f, 0.37f);
        dialogRT.anchorMax = new Vector2(0.67f, 0.63f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.15f, 0.14f, 0.2f, 0.96f);
        Outline dialogOutline = dialog.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.7f, 0.7f, 0.95f, 1f);
        dialogOutline.effectDistance = new Vector2(2f, 2f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.06f, 0.72f);
        titleRT.anchorMax = new Vector2(0.94f, 0.95f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        titleText.fontSize = 16;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.94f, 0.92f, 1f, 1f);
        titleText.text = title;

        GameObject bodyObj = new GameObject("Body");
        bodyObj.transform.SetParent(dialog.transform, false);
        RectTransform bodyRT = bodyObj.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0.08f, 0.4f);
        bodyRT.anchorMax = new Vector2(0.92f, 0.72f);
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = Vector2.zero;

        Text bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = titleText.font;
        bodyText.fontSize = 13;
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.color = new Color(0.9f, 0.95f, 1f, 1f);
        bodyText.text = message;

        Button confirmBtn = CreateTouchPromptButton(dialog, "Confirm", string.IsNullOrEmpty(confirmLabel) ? "Confirm" : confirmLabel,
            new Vector2(0.1f, 0.1f), new Vector2(0.45f, 0.28f), new Color(0.2f, 0.45f, 0.25f, 1f));
        confirmBtn.onClick.AddListener(() =>
        {
            if (_confirmationPanel != null) Destroy(_confirmationPanel);
            _confirmationPanel = null;
            onConfirm?.Invoke();
        });

        Button cancelBtn = CreateTouchPromptButton(dialog, "Cancel", string.IsNullOrEmpty(cancelLabel) ? "Cancel" : cancelLabel,
            new Vector2(0.55f, 0.1f), new Vector2(0.9f, 0.28f), new Color(0.45f, 0.2f, 0.2f, 1f));
        cancelBtn.onClick.AddListener(() =>
        {
            if (_confirmationPanel != null) Destroy(_confirmationPanel);
            _confirmationPanel = null;
            onCancel?.Invoke();
        });
    }

    public void HideSummonCreatureSelection()
    {
        if (_summonSelectionPanel != null)
        {
            Destroy(_summonSelectionPanel);
            _summonSelectionPanel = null;
        }
    }

    private Sprite _defaultWhiteSprite;

    private Sprite GetOrCreateDefaultSprite()
    {
        if (_defaultWhiteSprite != null)
            return _defaultWhiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "AidUI_DefaultWhiteTexture"
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        _defaultWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f);
        _defaultWhiteSprite.name = "AidUI_DefaultWhiteSprite";

        Debug.Log("[AidUI] Created default white sprite programmatically");

        return _defaultWhiteSprite;
    }

    private Sprite GetDefaultUISprite()
    {
        return GetOrCreateDefaultSprite();
    }

    private void EnsureVisibleImage(Image image, Color fallbackColor, string contextLabel)
    {
        if (image == null)
        {
            Debug.LogWarning($"[AidUI] Missing Image component for {contextLabel}");
            return;
        }

        image.sprite = GetDefaultUISprite();
        image.type = Image.Type.Simple;
        image.color = fallbackColor;
        image.raycastTarget = true;

        Debug.Log($"[AidUI] {contextLabel} image -> sprite={(image.sprite != null)}, color={image.color}, raycastTarget={image.raycastTarget}");
    }

    private void LogAidSelectionButtonDiagnostics(GameObject buttonObject, string characterLabel)
    {
        if (buttonObject == null)
        {
            Debug.LogError($"[AidUI] Button object is null for '{characterLabel}'");
            return;
        }

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        Image image = buttonObject.GetComponent<Image>();
        Text text = buttonObject.GetComponentInChildren<Text>();

        Debug.Log(
            $"[AidUI] Button diagnostics for '{characterLabel}' | " +
            $"activeInHierarchy={buttonObject.activeInHierarchy} | " +
            $"size={(rectTransform != null ? rectTransform.rect.size.ToString() : "<none>")} | " +
            $"anchoredPos={(rectTransform != null ? rectTransform.anchoredPosition.ToString() : "<none>")} | " +
            $"hasImage={(image != null)} | imageColor={(image != null ? image.color.ToString() : "<none>")} | hasSprite={(image != null && image.sprite != null)} | " +
            $"hasText={(text != null)} | text='{(text != null ? text.text : "<none>")}' | textColor={(text != null ? text.color.ToString() : "<none>")} | fontSize={(text != null ? text.fontSize : 0)}");
    }

    private void LogFullUIHierarchy(GameObject root, string context)
    {
        if (root == null)
        {
            Debug.LogWarning($"[AidUI][Hierarchy] {context} root is null");
            return;
        }

        Debug.Log($"[AidUI][Hierarchy] {context} - Full UI tree:");
        LogUIHierarchyRecursive(root.transform, 0);
    }

    private void LogUIHierarchyRecursive(Transform transformNode, int depth)
    {
        if (transformNode == null)
            return;

        string indent = new string(' ', depth * 2);

        RectTransform rectTransform = transformNode.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = transformNode.GetComponent<CanvasGroup>();
        Image image = transformNode.GetComponent<Image>();
        Canvas canvas = transformNode.GetComponent<Canvas>();

        string info = $"{indent}├─ {transformNode.name}";
        info += $" | active={transformNode.gameObject.activeInHierarchy}";

        if (rectTransform != null)
        {
            info += $" | size={rectTransform.rect.width:F1}x{rectTransform.rect.height:F1}";
            info += $" | pos={rectTransform.anchoredPosition}";
        }

        if (canvasGroup != null)
        {
            info += $" | CGalpha={canvasGroup.alpha:F2}";
            info += $" | CGinteractable={canvasGroup.interactable}";
            info += $" | CGblockRay={canvasGroup.blocksRaycasts}";
        }

        if (image != null)
        {
            info += $" | Image(sprite={image.sprite != null}, color={image.color}, enabled={image.enabled})";
        }

        if (canvas != null)
        {
            info += $" | Canvas(renderMode={canvas.renderMode}, sortOrder={canvas.sortingOrder})";
        }

        Debug.Log($"[AidUI][Hierarchy] {info}");

        for (int i = 0; i < transformNode.childCount; i++)
        {
            LogUIHierarchyRecursive(transformNode.GetChild(i), depth + 1);
        }
    }

    private void ForceUIVisibility(GameObject root)
    {
        if (root == null)
        {
            Debug.LogWarning("[AidUI] ForceUIVisibility called with null root");
            return;
        }

        Debug.Log($"[AidUI] ForceUIVisibility on {root.name}");

        CanvasGroup[] canvasGroups = root.GetComponentsInChildren<CanvasGroup>(true);
        foreach (CanvasGroup group in canvasGroups)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            Debug.Log($"[AidUI] Forced CanvasGroup on {group.gameObject.name} to alpha=1");
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            img.enabled = true;
            if (img.color.a < 1f)
            {
                Color c = img.color;
                c.a = 1f;
                img.color = c;
                Debug.Log($"[AidUI] Forced Image on {img.gameObject.name} to alpha=1");
            }
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
                Debug.Log($"[AidUI] Forced {t.gameObject.name} to active");
            }
        }
    }

    private void VerifyCanvasSettings()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[AidUI] Canvas not found!");
            return;
        }

        Debug.Log("[AidUI] Canvas settings:");
        Debug.Log($"[AidUI]   renderMode: {canvas.renderMode}");
        Debug.Log($"[AidUI]   sortingOrder: {canvas.sortingOrder}");
        Debug.Log($"[AidUI]   worldCamera: {canvas.worldCamera}");
        Debug.Log($"[AidUI]   pixelPerfect: {canvas.pixelPerfect}");

        CanvasScaler canvasScaler = canvas.GetComponent<CanvasScaler>();
        if (canvasScaler != null)
        {
            Debug.Log($"[AidUI]   scaleMode: {canvasScaler.uiScaleMode}");
            Debug.Log($"[AidUI]   referenceResolution: {canvasScaler.referenceResolution}");
            Debug.Log($"[AidUI]   scaleFactor: {canvasScaler.scaleFactor}");
        }

        GraphicRaycaster graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
        if (graphicRaycaster != null)
            Debug.Log($"[AidUI]   raycaster enabled: {graphicRaycaster.enabled}");
    }

    public void ShowPickUpItemSelection(
        string actorName,
        List<string> itemOptions,
        System.Action<int> onSelect,
        System.Action onCancel,
        string titleOverride = null,
        string bodyOverride = null,
        Color? optionButtonColorOverride = null)
    {
        Debug.Log("[AidUI] ========== ShowPickUpItemSelection START ==========");
        Debug.Log($"[AidUI] ShowPickUpItemSelection called | actor='{actorName}' | options={(itemOptions != null ? itemOptions.Count : 0)} | title='{titleOverride}'");
        VerifyCanvasSettings();

        HidePickUpItemSelection();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[AidUI] No Canvas found for selection UI.");
            onCancel?.Invoke();
            return;
        }

        Debug.Log($"[AidUI] Canvas located | name='{canvas.name}' | renderMode={canvas.renderMode} | sortingOrder={canvas.sortingOrder}");

        if (itemOptions == null)
            itemOptions = new List<string>();

        _pickUpItemSelectionPanel = new GameObject("PickUpItemSelectionPanel");
        _pickUpItemSelectionPanel.transform.SetParent(canvas.transform, false);
        _pickUpItemSelectionPanel.transform.SetAsLastSibling();

        RectTransform panelRT = _pickUpItemSelectionPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image overlay = _pickUpItemSelectionPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.75f);
        EnsureVisibleImage(overlay, new Color(0f, 0f, 0f, 0.75f), "SelectionOverlay");

        CanvasGroup cg = _pickUpItemSelectionPanel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        GameObject dialog = new GameObject("Dialog");
        LogFullUIHierarchy(_pickUpItemSelectionPanel, "After overlay creation");
        dialog.transform.SetParent(_pickUpItemSelectionPanel.transform, false);

        RectTransform dialogRT = dialog.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.18f, 0.16f);
        dialogRT.anchorMax = new Vector2(0.82f, 0.84f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.1f, 0.14f, 0.2f, 0.98f);
        EnsureVisibleImage(dialogBg, new Color(0.1f, 0.14f, 0.2f, 0.98f), "SelectionDialog");
        Outline outline = dialog.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.8f, 0.95f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.9f);
        titleRT.anchorMax = new Vector2(0.95f, 0.98f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.88f, 0.97f, 1f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = string.IsNullOrEmpty(titleOverride) ? "PICK UP WHICH ITEM?" : titleOverride;

        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(dialog.transform, false);
        RectTransform bodyRT = bodyObj.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0.08f, 0.81f);
        bodyRT.anchorMax = new Vector2(0.92f, 0.89f);
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = Vector2.zero;

        Text bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = titleText.font;
        bodyText.fontSize = 13;
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.color = new Color(0.9f, 0.95f, 1f, 1f);
        bodyText.text = string.IsNullOrEmpty(bodyOverride)
            ? $"{actorName}, choose one item within reach (this uses a move action and can provoke AoO):"
            : bodyOverride;

        GameObject listRoot = new GameObject("ItemList");
        listRoot.transform.SetParent(dialog.transform, false);
        RectTransform listRT = listRoot.AddComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0.08f, 0.2f);
        listRT.anchorMax = new Vector2(0.92f, 0.78f);
        listRT.offsetMin = Vector2.zero;
        listRT.offsetMax = Vector2.zero;

        Image listBg = listRoot.AddComponent<Image>();
        listBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        EnsureVisibleImage(listBg, new Color(0.06f, 0.08f, 0.12f, 0.95f), "SelectionListBackground");

        ScrollRect scrollRect = listRoot.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(listRoot.transform, false);
        RectTransform viewportRT = viewport.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(4f, 4f);
        viewportRT.offsetMax = new Vector2(-4f, -4f);

        Image viewportImage = viewport.AddComponent<Image>();
        // IMPORTANT: fully transparent viewport masks can clip all children on some Unity versions.
        // Use opaque white mask graphic with showMaskGraphic=false so content remains visible.
        EnsureVisibleImage(viewportImage, Color.white, "SelectionViewportMask");
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        for (int i = 0; i < itemOptions.Count; i++)
        {
            int optionIndex = i;
            string optionLabel = itemOptions[i];

            GameObject btnObj = new GameObject($"PickUpOption_{i}");
            btnObj.transform.SetParent(content.transform, false);
            LayoutElement layoutElement = btnObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 92f;

            Image btnImg = btnObj.AddComponent<Image>();
            Color buttonColor = optionButtonColorOverride ?? new Color(0.2f, 0.36f, 0.5f, 1f);
            btnImg.color = buttonColor;
            EnsureVisibleImage(btnImg, buttonColor, $"OptionButton[{optionIndex}] '{optionLabel}'");

            Button optionBtn = btnObj.AddComponent<Button>();
            optionBtn.targetGraphic = btnImg;
            UIFactory.ApplyEnhancedCombatButtonStyle(optionBtn, buttonColor);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRT = txtObj.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(8f, 6f);
            txtRT.offsetMax = new Vector2(-8f, -6f);

            Text txt = txtObj.AddComponent<Text>();
            txt.font = titleText.font;
            if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 12);
            txt.fontSize = 12;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.color = new Color(1f, 1f, 1f, 1f);
            txt.text = optionLabel;

            LogAidSelectionButtonDiagnostics(btnObj, optionLabel);

            optionBtn.onClick.AddListener(() =>
            {
                HidePickUpItemSelection();
                onSelect?.Invoke(optionIndex);
            });
        }

        Debug.Log($"[AidUI] Created {itemOptions.Count} option buttons for selection UI.");

        Button cancelBtn = CreateTouchPromptButton(dialog, "Cancel", "Cancel",
            new Vector2(0.34f, 0.06f), new Vector2(0.66f, 0.15f),
            new Color(0.45f, 0.2f, 0.2f, 1f));
        if (cancelBtn != null)
        {
            LogAidSelectionButtonDiagnostics(cancelBtn.gameObject, "Cancel");
            cancelBtn.onClick.AddListener(() =>
            {
                HidePickUpItemSelection();
                onCancel?.Invoke();
            });
        }
        else
        {
            Debug.LogError("[AidUI] Failed to create Cancel button for selection UI.");
        }

        // Force a layout rebuild so VerticalLayoutGroup positions option buttons immediately.
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)dialog.transform);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_pickUpItemSelectionPanel.transform);
        Canvas.ForceUpdateCanvases();

        // Keep panel in front and force visibility for debugging and resilience.
        _pickUpItemSelectionPanel.transform.SetAsLastSibling();
        Debug.Log("[AidUI] About to show UI, forcing visibility:");
        ForceUIVisibility(_pickUpItemSelectionPanel);

        Debug.Log("[AidUI] UI shown, final hierarchy:");
        LogFullUIHierarchy(_pickUpItemSelectionPanel, "Final state");
        Debug.Log("[AidUI] ========== ShowPickUpItemSelection END ==========");
    }

    public void HidePickUpItemSelection()
    {
        if (_pickUpItemSelectionPanel != null)
        {
            Destroy(_pickUpItemSelectionPanel);
            _pickUpItemSelectionPanel = null;
        }
    }

    /// <summary>
    /// Generic character-selection dialog built on top of the existing selectable-list modal.
    /// Useful for actions like Aid Another ally/enemy selection.
    /// </summary>
    public void ShowCharacterSelectionUI(
        string title,
        string body,
        List<CharacterController> characters,
        System.Action<CharacterController> onSelect,
        System.Action onCancel,
        Color? optionButtonColorOverride = null)
    {
        Debug.Log($"[AidUI] ShowCharacterSelectionUI called | title='{title}' | characterCount={(characters != null ? characters.Count : 0)}");

        if (characters == null)
            characters = new List<CharacterController>();

        var labels = new List<string>(characters.Count);
        for (int i = 0; i < characters.Count; i++)
        {
            CharacterController c = characters[i];
            string label = c != null && c.Stats != null ? c.Stats.CharacterName : "Unknown";
            labels.Add(label);
            Debug.Log($"[AidUI] Character option [{i}] -> '{label}'");
        }

        ShowPickUpItemSelection(
            actorName: "Selection",
            itemOptions: labels,
            onSelect: selectedIndex =>
            {
                Debug.Log($"[AidUI] Character selection clicked index={selectedIndex}");
                if (selectedIndex < 0 || selectedIndex >= characters.Count)
                {
                    Debug.LogWarning($"[AidUI] Invalid character selection index={selectedIndex}; cancelling.");
                    onCancel?.Invoke();
                    return;
                }

                CharacterController selectedCharacter = characters[selectedIndex];
                string selectedName = selectedCharacter != null && selectedCharacter.Stats != null
                    ? selectedCharacter.Stats.CharacterName
                    : "Unknown";
                Debug.Log($"[AidUI] Character selected -> '{selectedName}'");
                onSelect?.Invoke(selectedCharacter);
            },
            onCancel: () =>
            {
                Debug.Log("[AidUI] Character selection cancelled.");
                onCancel?.Invoke();
            },
            titleOverride: string.IsNullOrEmpty(title) ? "Select Character" : title,
            bodyOverride: string.IsNullOrEmpty(body) ? "Choose a character:" : body,
            optionButtonColorOverride: optionButtonColorOverride ?? new Color(0.26f, 0.34f, 0.55f, 1f));
    }

    public void ShowDropEquippedItemSelection(string actorName, List<string> itemOptions, System.Action<int> onSelect, System.Action onCancel)
    {
        HideDropEquippedItemSelection();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            onCancel?.Invoke();
            return;
        }

        if (itemOptions == null)
            itemOptions = new List<string>();

        _dropEquippedItemSelectionPanel = new GameObject("DropEquippedItemSelectionPanel");
        _dropEquippedItemSelectionPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = _dropEquippedItemSelectionPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image overlay = _dropEquippedItemSelectionPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.75f);

        CanvasGroup cg = _dropEquippedItemSelectionPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(_dropEquippedItemSelectionPanel.transform, false);

        RectTransform dialogRT = dialog.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.24f, 0.2f);
        dialogRT.anchorMax = new Vector2(0.76f, 0.8f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.12f, 0.12f, 0.2f, 0.97f);
        Outline outline = dialog.AddComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.55f, 1f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.86f);
        titleRT.anchorMax = new Vector2(0.95f, 0.98f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.92f, 0.88f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = "DROP WHICH HELD ITEM?";

        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(dialog.transform, false);
        RectTransform bodyRT = bodyObj.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0.08f, 0.75f);
        bodyRT.anchorMax = new Vector2(0.92f, 0.86f);
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = Vector2.zero;

        Text bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bodyText.font == null) bodyText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        bodyText.fontSize = 14;
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.color = new Color(0.9f, 0.95f, 1f);
        bodyText.text = $"{actorName}, choose which held item to drop (free action, no AoO):";

        float optionsTop = 0.7f;
        float optionsBottom = 0.2f;
        int optionCount = Mathf.Max(1, itemOptions.Count);
        float totalHeight = optionsTop - optionsBottom;
        float step = totalHeight / optionCount;

        for (int i = 0; i < itemOptions.Count; i++)
        {
            int optionIndex = i;
            float yMax = optionsTop - (step * i);
            float yMin = yMax - (step * 0.8f);

            Button optionBtn = CreateTouchPromptButton(dialog,
                $"DropEquippedOption_{i}",
                itemOptions[i],
                new Vector2(0.08f, yMin),
                new Vector2(0.92f, yMax),
                new Color(0.3f, 0.26f, 0.58f, 1f));

            optionBtn.onClick.AddListener(() =>
            {
                HideDropEquippedItemSelection();
                onSelect?.Invoke(optionIndex);
            });
        }

        Button cancelBtn = CreateTouchPromptButton(dialog, "Cancel", "Cancel",
            new Vector2(0.36f, 0.06f), new Vector2(0.64f, 0.16f),
            new Color(0.45f, 0.2f, 0.2f, 1f));
        cancelBtn.onClick.AddListener(() =>
        {
            HideDropEquippedItemSelection();
            onCancel?.Invoke();
        });
    }

    public void HideDropEquippedItemSelection()
    {
        if (_dropEquippedItemSelectionPanel != null)
        {
            Destroy(_dropEquippedItemSelectionPanel);
            _dropEquippedItemSelectionPanel = null;
        }
    }

    public void ShowDisarmWeaponSelection(string targetName, List<string> itemOptions, System.Action<int> onSelect, System.Action onCancel)
    {
        ShowItemSelectionDialog(
            panelName: "DisarmWeaponSelectionPanel",
            title: "DISARM TARGET HELD ITEM",
            bodyTextTemplate: "Choose which held item to disarm from {0}:",
            targetName: targetName,
            itemOptions: itemOptions,
            onSelect: onSelect,
            onCancel: onCancel);
    }

    public void ShowSunderItemSelection(string targetName, List<string> itemOptions, System.Action<int> onSelect, System.Action onCancel)
    {
        ShowItemSelectionDialog(
            panelName: "SunderItemSelectionPanel",
            title: "SUNDER TARGET ITEM",
            bodyTextTemplate: "Choose which equipped item to sunder on {0}:",
            targetName: targetName,
            itemOptions: itemOptions,
            onSelect: onSelect,
            onCancel: onCancel);
    }

    private void ShowItemSelectionDialog(string panelName, string title, string bodyTextTemplate, string targetName, List<string> itemOptions, System.Action<int> onSelect, System.Action onCancel)
    {
        HideDisarmWeaponSelection();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            onCancel?.Invoke();
            return;
        }

        if (itemOptions == null)
            itemOptions = new List<string>();

        _disarmWeaponSelectionPanel = new GameObject(string.IsNullOrEmpty(panelName) ? "ItemSelectionPanel" : panelName);
        _disarmWeaponSelectionPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = _disarmWeaponSelectionPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image overlay = _disarmWeaponSelectionPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.75f);

        CanvasGroup cg = _disarmWeaponSelectionPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(_disarmWeaponSelectionPanel.transform, false);

        RectTransform dialogRT = dialog.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.26f, 0.24f);
        dialogRT.anchorMax = new Vector2(0.74f, 0.76f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.12f, 0.12f, 0.2f, 0.97f);
        Outline outline = dialog.AddComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.55f, 1f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(dialog.transform, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.86f);
        titleRT.anchorMax = new Vector2(0.95f, 0.98f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.92f, 0.88f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = title;

        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(dialog.transform, false);
        RectTransform bodyRT = bodyObj.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0.08f, 0.75f);
        bodyRT.anchorMax = new Vector2(0.92f, 0.86f);
        bodyRT.offsetMin = Vector2.zero;
        bodyRT.offsetMax = Vector2.zero;

        Text bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bodyText.font == null) bodyText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        bodyText.fontSize = 14;
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.color = new Color(0.9f, 0.95f, 1f);
        bodyText.text = string.Format(bodyTextTemplate, targetName);

        float optionsTop = 0.7f;
        float optionsBottom = 0.2f;
        int optionCount = Mathf.Max(1, itemOptions.Count);
        float totalHeight = optionsTop - optionsBottom;
        float step = totalHeight / optionCount;

        for (int i = 0; i < itemOptions.Count; i++)
        {
            int optionIndex = i;
            float yMax = optionsTop - (step * i);
            float yMin = yMax - (step * 0.8f);

            Button optionBtn = CreateTouchPromptButton(dialog,
                $"ItemOption_{i}",
                itemOptions[i],
                new Vector2(0.08f, yMin),
                new Vector2(0.92f, yMax),
                new Color(0.3f, 0.26f, 0.58f, 1f));

            optionBtn.onClick.AddListener(() =>
            {
                HideDisarmWeaponSelection();
                onSelect?.Invoke(optionIndex);
            });
        }

        Button cancelBtn = CreateTouchPromptButton(dialog, "Cancel", "Cancel",
            new Vector2(0.36f, 0.06f), new Vector2(0.64f, 0.16f),
            new Color(0.45f, 0.2f, 0.2f, 1f));
        cancelBtn.onClick.AddListener(() =>
        {
            HideDisarmWeaponSelection();
            onCancel?.Invoke();
        });
    }

    public void HideDisarmWeaponSelection()
    {
        if (_disarmWeaponSelectionPanel != null)
        {
            Destroy(_disarmWeaponSelectionPanel);
            _disarmWeaponSelectionPanel = null;
        }
    }


    private Button CreateTouchPromptButton(GameObject parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
    {
        Button btn = UIFactory.CreateButton(
            parent.transform,
            label,
            null,
            null,
            bgColor,
            name,
            UIFactory.GetDefaultFont(),
            13,
            Color.white);

        RectTransform btnRT = btn.GetComponent<RectTransform>();
        btnRT.anchorMin = anchorMin;
        btnRT.anchorMax = anchorMax;
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;

        Image btnImg = btn.GetComponent<Image>();
        EnsureVisibleImage(btnImg, bgColor, $"PromptButton '{name}'");

        btn.targetGraphic = btnImg;
        UIFactory.ApplyEnhancedCombatButtonStyle(btn, bgColor);

        Text txt = btn.transform.Find("Text")?.GetComponent<Text>();
        if (txt != null)
        {
            RectTransform txtRT = txt.GetComponent<RectTransform>();
            txtRT.offsetMin = new Vector2(4, 2);
            txtRT.offsetMax = new Vector2(-4, -2);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;
        }

        return btn;
    }

    private Button CreateSpellDialogButton(GameObject parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
    {
        Button btn = UIFactory.CreateButton(
            parent.transform,
            label,
            null,
            null,
            bgColor,
            name,
            UIFactory.GetDefaultFont(),
            14,
            Color.white);

        RectTransform btnRT = btn.GetComponent<RectTransform>();
        btnRT.anchorMin = anchorMin;
        btnRT.anchorMax = anchorMax;
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;

        UIFactory.ApplyEnhancedCombatButtonStyle(btn, bgColor);

        Text txt = btn.transform.Find("Text")?.GetComponent<Text>();
        if (txt != null)
        {
            RectTransform txtRT = txt.GetComponent<RectTransform>();
            txtRT.offsetMin = new Vector2(5, 2);
            txtRT.offsetMax = new Vector2(-5, -2);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;
        }

        return btn;
    }


}



/// <summary>
/// UI element references for a single NPC stats panel.
/// Created dynamically by SceneBootstrap for each enemy in the encounter.
/// </summary>
[System.Serializable]
public class NPCPanelUI
{
    public GameObject Panel;
    public Text NameText;
    public Text HPText;
    public Text ACText;
    public Text AtkText;
    public Text SpeedText;
    public Text AbilityText;
    public Image HPBar;
    public Image IconImage;
    public Image ActiveIndicator;
}