using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Manages all combat UI elements with D&D 3.5 ability score display.
/// Shows 6 ability scores with modifiers and derived stats for each character.
/// Supports action economy buttons (Move, Attack, Full Attack, Dual Wield, End Turn).
/// Includes AoO warning confirmation dialog.
/// </summary>
public class CombatUI : MonoBehaviour
{
    [Header("Turn Indicator")]
    public Text TurnIndicatorText;

    [Header("PC1 Stats")]
    public Text PC1NameText;
    public Text PC1HPText;
    public Text PC1ACText;
    public Text PC1AtkText;
    public Text PC1SpeedText;
    public Text PC1AbilityText; // Shows all 6 ability scores
    public Image PC1HPBar;
    public GameObject PC1Panel;

    [Header("PC2 Stats")]
    public Text PC2NameText;
    public Text PC2HPText;
    public Text PC2ACText;
    public Text PC2AtkText;
    public Text PC2SpeedText;
    public Text PC2AbilityText;
    public Image PC2HPBar;
    public GameObject PC2Panel;

    [Header("NPC Stats")]
    public Text NPCNameText;
    public Text NPCHPText;
    public Text NPCACText;
    public Text NPCAtkText;
    public Text NPCSpeedText;
    public Text NPCAbilityText;
    public Image NPCHPBar;

    [Header("Combat Log")]
    public Text CombatLogText;

    [Header("Action Buttons - Action Economy")]
    public GameObject ActionPanel;
    public Button MoveButton;
    public Button AttackButton;         // Single attack (Standard Action)
    public Button FullAttackButton;     // Full Attack (Full-Round Action)
    public Button DualWieldButton;      // Dual Wield (Full-Round Action)
    public Button EndTurnButton;
    public Text ActionStatusText;       // Shows current action economy status

    [Header("Feat Controls")]
    public GameObject PowerAttackPanel;     // Panel containing Power Attack slider
    public Slider PowerAttackSlider;        // Slider for Power Attack value (0 to BAB)
    public Text PowerAttackLabel;           // Shows "Power Attack: -X attack / +Y damage"
    public GameObject RapidShotPanel;       // Panel containing Rapid Shot toggle
    public Button RapidShotToggle;          // Toggle button for Rapid Shot
    public Text RapidShotLabel;             // Shows "Rapid Shot: ON/OFF"

    // Active-PC indicator images on the panels
    public Image PC1ActiveIndicator;
    public Image PC2ActiveIndicator;

    // Legacy aliases for compatibility
    public Text PCNameText { get => PC1NameText; set => PC1NameText = value; }
    public Text PCHPText { get => PC1HPText; set => PC1HPText = value; }
    public Text PCACText { get => PC1ACText; set => PC1ACText = value; }
    public Image PCHPBar { get => PC1HPBar; set => PC1HPBar = value; }

    /// <summary>
    /// Update stat displays for both PCs and the NPC.
    /// </summary>
    public void UpdateAllStats(CharacterController pc1, CharacterController pc2, CharacterController npc)
    {
        UpdateCharacterStats(pc1, PC1NameText, PC1HPText, PC1ACText, PC1AtkText, PC1SpeedText, PC1AbilityText, PC1HPBar);
        UpdateCharacterStats(pc2, PC2NameText, PC2HPText, PC2ACText, PC2AtkText, PC2SpeedText, PC2AbilityText, PC2HPBar);
        UpdateCharacterStats(npc, NPCNameText, NPCHPText, NPCACText, NPCAtkText, NPCSpeedText, NPCAbilityText, NPCHPBar);
    }

    private void UpdateCharacterStats(CharacterController ch,
        Text nameText, Text hpText, Text acText, Text atkText, Text speedText, Text abilityText, Image hpBar)
    {
        if (ch == null || ch.Stats == null) return;

        var s = ch.Stats;

        if (nameText != null)
        {
            string raceStr = !string.IsNullOrEmpty(s.RaceName) ? $"{s.RaceName} " : "";
            string sizeStr = (s.SizeCategory != "Medium") ? $" [{s.SizeCategory}]" : "";
            nameText.text = $"{s.CharacterName} (Lv {s.Level} {raceStr}{s.CharacterClass}){sizeStr}";
            if (s.IsDead) nameText.text += " (DEAD)";
        }

        if (hpText != null)
            hpText.text = $"HP: {s.CurrentHP}/{s.MaxHP}";

        if (acText != null)
        {
            // Show effective DEX mod (capped by Max Dex Bonus from armor)
            int effectiveDex = s.DEXMod;
            if (s.MaxDexBonus >= 0 && effectiveDex > s.MaxDexBonus)
                effectiveDex = s.MaxDexBonus;
            string dexLabel = (s.MaxDexBonus >= 0 && s.DEXMod > s.MaxDexBonus)
                ? $"DEX*"  // Asterisk indicates capped
                : "DEX";
            string sizeDetail = FormatBonusDetail(s.SizeModifier, "Size");
            string acDetails = $"AC: {s.ArmorClass} (10{FormatBonusDetail(effectiveDex, dexLabel)}{FormatBonusDetail(s.ArmorBonus, "Armor")}{FormatBonusDetail(s.ShieldBonus, "Shield")}{sizeDetail})";
            if (s.ArmorCheckPenalty > 0)
                acDetails += $" ACP:-{s.ArmorCheckPenalty}";
            acText.text = acDetails;
        }

        if (atkText != null)
        {
            string sizeAtkStr = s.SizeModifier != 0 ? $" {CharacterStats.FormatMod(s.SizeModifier)} Size" : "";
            atkText.text = $"Atk: {CharacterStats.FormatMod(s.AttackBonus)} (BAB {CharacterStats.FormatMod(s.BaseAttackBonus)} {CharacterStats.FormatMod(s.STRMod)} STR{sizeAtkStr})";
        }

        if (speedText != null)
        {
            string speedExtra = "";
            if (s.SpeedNotReducedByArmor)
                speedExtra = " (no armor penalty)";
            speedText.text = $"Speed: {s.MoveRange} sq ({s.SpeedInFeet} ft){speedExtra}";
        }

        if (abilityText != null)
        {
            abilityText.supportRichText = true;
            // Show racial modifiers as annotations (e.g., CON 16(+3)[+2] for Dwarf)
            abilityText.text =
                $"{s.GetAbilityStringWithRacial("STR", s.STR, s.GetRacialModifier("STR"))} " +
                $"{s.GetAbilityStringWithRacial("DEX", s.DEX, s.GetRacialModifier("DEX"))} " +
                $"{s.GetAbilityStringWithRacial("CON", s.CON, s.GetRacialModifier("CON"))}\n" +
                $"{s.GetAbilityStringWithRacial("WIS", s.WIS, s.GetRacialModifier("WIS"))} " +
                $"{s.GetAbilityStringWithRacial("INT", s.INT, s.GetRacialModifier("INT"))} " +
                $"{s.GetAbilityStringWithRacial("CHA", s.CHA, s.GetRacialModifier("CHA"))}";
        }

        if (hpBar != null)
            hpBar.fillAmount = (float)s.CurrentHP / s.MaxHP;
    }

    private string FormatBonusDetail(int value, string label)
    {
        if (value == 0) return "";
        return value > 0 ? $"+{value} {label}" : $"{value} {label}";
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

    public void ShowCombatLog(string message)
    {
        if (CombatLogText != null)
        {
            // Apply rich-text highlighting for critical hits
            CombatLogText.supportRichText = true;
            string formatted = HighlightCriticalHits(message);
            CombatLogText.text = formatted;
        }
    }

    /// <summary>
    /// Apply rich-text color highlights for combat log text.
    /// Colors: Green=hits, Red=misses, Gold=crits/Power Attack, Blue=Rapid Shot, Green=PBS
    /// </summary>
    private string HighlightCriticalHits(string text)
    {
        // Highlight hit/miss results
        text = text.Replace("- HIT!", "- <color=#66FF66><b>HIT!</b></color>");
        text = text.Replace("- MISS!", "- <color=#FF4444><b>MISS!</b></color>");

        // Highlight critical hit confirmations and damage in gold/yellow
        text = text.Replace("CRITICAL HIT!", "<color=#FFD700><b>CRITICAL HIT!</b></color>");
        text = text.Replace("CRIT!", "<color=#FFD700><b>CRIT!</b></color>");
        text = text.Replace("*** Critical Threat", "<color=#FFA500><b>*** Critical Threat</b></color>");
        text = text.Replace("CONFIRMED!", "<color=#FFD700><b>CONFIRMED!</b></color>");
        text = text.Replace("Not confirmed", "<color=#AAAAAA>Not confirmed</color>");

        // Highlight natural 20s in bright gold
        text = text.Replace("(NATURAL 20!)", "<color=#FFD700><b>(NATURAL 20!)</b></color>");
        text = text.Replace("(NAT 20!)", "<color=#FFD700><b>(NAT 20!)</b></color>");

        // Highlight natural 1s in red
        text = text.Replace("(NATURAL 1!)", "<color=#FF4444><b>(NATURAL 1!)</b></color>");
        text = text.Replace("(NAT 1!)", "<color=#FF4444><b>(NAT 1!)</b></color>");

        // Highlight slain messages
        text = text.Replace("has been slain!", "<color=#FF6666><b>has been slain!</b></color>");

        // Highlight range info
        text = text.Replace("no penalty", "<color=#66FF66>no penalty</color>");
        text = text.Replace("beyond maximum range!", "<color=#FF4444><b>beyond maximum range!</b></color>");

        // Highlight feat text - Gold for Power Attack, Blue for Rapid Shot, Green for PBS
        text = text.Replace("Power Attack", "<color=#FF9933>Power Attack</color>");
        text = text.Replace("Rapid Shot", "<color=#66CCFF>Rapid Shot</color>");
        text = text.Replace("Point Blank Shot", "<color=#66FF66>Point Blank Shot</color>");

        // Highlight separator lines
        text = text.Replace("═══════════════════════════════════", "<color=#888888>═══════════════════════════════════</color>");

        // Highlight damage numbers
        text = text.Replace("total damage", "<color=#FFAA44><b>total damage</b></color>");
        text = text.Replace(" damage", " <color=#FFAA44>damage</color>");

        // Highlight HP change arrows
        text = text.Replace(" → ", " <color=#FFFF66>→</color> ");

        return text;
    }

    public void SetActionButtonsVisible(bool visible)
    {
        if (ActionPanel != null)
            ActionPanel.SetActive(visible);
        // Hide feat controls when action panel is hidden
        if (!visible)
        {
            if (PowerAttackPanel != null) PowerAttackPanel.SetActive(false);
            if (RapidShotPanel != null) RapidShotPanel.SetActive(false);
        }
    }

    // ========== ACTION ECONOMY UI ==========

    /// <summary>
    /// Update the action panel buttons based on the current action economy state.
    /// Enables/disables buttons based on available actions.
    /// </summary>
    public void UpdateActionButtons(CharacterController pc)
    {
        if (pc == null || ActionPanel == null) return;

        var actions = pc.Actions;
        bool canDualWield = pc.CanDualWield();
        bool hasIterativeAttacks = pc.Stats.IterativeAttackCount > 1;

        // Rapid Shot grants an extra attack during full attack with ranged weapon,
        // so we need to show the Full Attack button even at low BAB
        bool hasRapidShot = pc.Stats.HasFeat("Rapid Shot");
        bool fullAttackRelevant = hasIterativeAttacks || hasRapidShot;

        // Move button: available if Move Action is available, or can convert Standard to Move
        if (MoveButton != null)
        {
            bool canMove = actions.HasMoveAction || actions.CanConvertStandardToMove;
            MoveButton.gameObject.SetActive(true);
            MoveButton.interactable = canMove;

            // Update label
            Text moveLabel = MoveButton.GetComponentInChildren<Text>();
            if (moveLabel != null)
            {
                if (actions.HasMoveAction)
                    moveLabel.text = "Move (Move Action)";
                else if (actions.CanConvertStandardToMove)
                    moveLabel.text = "Move (Std\u2192Move)";
                else
                    moveLabel.text = "Move (Used)";
            }
        }

        // Single Attack button: Standard Action
        if (AttackButton != null)
        {
            AttackButton.gameObject.SetActive(true);
            AttackButton.interactable = actions.HasStandardAction;

            Text atkLabel = AttackButton.GetComponentInChildren<Text>();
            if (atkLabel != null)
                atkLabel.text = actions.HasStandardAction ? "Attack (Standard)" : "Attack (Used)";
        }

        // Full Attack button: Full-Round Action
        // Show if BAB grants iterative attacks OR if character has Rapid Shot feat
        // (Rapid Shot adds an extra attack during full attack with ranged weapon)
        if (FullAttackButton != null)
        {
            bool showFullAtk = fullAttackRelevant && actions.HasFullRoundAction;
            FullAttackButton.gameObject.SetActive(fullAttackRelevant);
            FullAttackButton.interactable = showFullAtk;

            Text faLabel = FullAttackButton.GetComponentInChildren<Text>();
            if (faLabel != null)
            {
                int atkCount = pc.Stats.IterativeAttackCount;
                // Rapid Shot only adds an extra attack with a ranged weapon equipped
                bool weaponIsRanged = pc.IsEquippedWeaponRanged();
                bool rapidShotWillApply = hasRapidShot && pc.RapidShotEnabled && weaponIsRanged;

                string label;
                if (!showFullAtk)
                {
                    label = "Full Attack (N/A)";
                }
                else if (rapidShotWillApply)
                {
                    label = $"Full Attack x{atkCount + 1} (Rapid Shot)";
                }
                else if (hasRapidShot && pc.RapidShotEnabled && !weaponIsRanged)
                {
                    // Rapid Shot is ON but weapon is melee — warn the user
                    label = $"Full Attack x{atkCount} (RS: need ranged wpn)";
                }
                else if (hasIterativeAttacks)
                {
                    label = $"Full Attack x{atkCount} (Full-Round)";
                }
                else
                {
                    label = "Full Attack (Full-Round)";
                }
                faLabel.text = label;
            }
        }

        // Dual Wield button: Full-Round Action, only show if both hands have weapons
        if (DualWieldButton != null)
        {
            bool showDW = canDualWield;
            bool canDW = canDualWield && actions.HasFullRoundAction;
            DualWieldButton.gameObject.SetActive(showDW);
            DualWieldButton.interactable = canDW;

            Text dwLabel = DualWieldButton.GetComponentInChildren<Text>();
            if (dwLabel != null)
                dwLabel.text = canDW ? "Dual Wield (Full-Round)" : "Dual Wield (N/A)";
        }

        // End Turn always available
        if (EndTurnButton != null)
        {
            EndTurnButton.gameObject.SetActive(true);
            EndTurnButton.interactable = true;
        }

        // Action status text
        if (ActionStatusText != null)
        {
            ActionStatusText.text = actions.GetStatusString();
        }
    }

    // ========== FEAT UI ==========

    /// <summary>
    /// Update feat controls (Power Attack slider, Rapid Shot toggle) for the active PC.
    /// Only shows controls for feats the character actually has.
    /// </summary>
    public void UpdateFeatControls(CharacterController pc)
    {
        if (pc == null || pc.Stats == null) return;

        // === Power Attack ===
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

        // === Rapid Shot ===
        bool hasRapidShot = pc.Stats.HasFeat("Rapid Shot");
        if (RapidShotPanel != null)
        {
            RapidShotPanel.SetActive(hasRapidShot);
            if (hasRapidShot)
            {
                UpdateRapidShotLabel(pc);
            }
        }
    }

    /// <summary>Update Power Attack label to show current penalty/bonus.</summary>
    public void UpdatePowerAttackLabel(CharacterController pc)
    {
        if (PowerAttackLabel == null || pc == null) return;
        int val = pc.PowerAttackValue;
        if (val == 0)
        {
            PowerAttackLabel.text = "Power Attack: OFF";
        }
        else
        {
            ItemData weapon = pc.GetEquippedMainWeapon();
            bool twoHanded = CharacterController.IsWeaponTwoHanded(weapon);
            int dmgBonus = twoHanded ? val * 2 : val;
            string thStr = twoHanded ? " (2H)" : "";
            PowerAttackLabel.text = $"Power Attack: -{val} atk / +{dmgBonus} dmg{thStr}";
        }
    }

    /// <summary>Update Rapid Shot label to show ON/OFF state.</summary>
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

    /// <summary>
    /// Highlight which PC is currently active (1 or 2). Pass 0 to clear.
    /// </summary>
    public void SetActivePC(int pcNumber)
    {
        if (PC1ActiveIndicator != null)
            PC1ActiveIndicator.enabled = (pcNumber == 1);
        if (PC2ActiveIndicator != null)
            PC2ActiveIndicator.enabled = (pcNumber == 2);

        // Dim dead / inactive panel, brighten active
        if (PC1Panel != null)
        {
            Image panelImg = PC1Panel.GetComponent<Image>();
            if (panelImg != null)
                panelImg.color = (pcNumber == 1)
                    ? new Color(0.1f, 0.3f, 0.5f, 0.95f)   // bright active
                    : new Color(0.1f, 0.2f, 0.4f, 0.65f);   // dimmed
        }
        if (PC2Panel != null)
        {
            Image panelImg = PC2Panel.GetComponent<Image>();
            if (panelImg != null)
                panelImg.color = (pcNumber == 2)
                    ? new Color(0.1f, 0.2f, 0.5f, 0.95f)
                    : new Color(0.1f, 0.15f, 0.35f, 0.65f);
        }
    }

    // ========================================================================
    // AoO WARNING DIALOG
    // ========================================================================

    private GameObject _aooWarningPanel;
    private Text _aooWarningText;
    private Button _aooConfirmButton;
    private Button _aooCancelButton;
    private System.Action _onAoOConfirm;
    private System.Action _onAoOCancel;

    /// <summary>
    /// Show the AoO warning confirmation dialog.
    /// Blocks all other actions until the player responds.
    /// </summary>
    /// <param name="enemyNames">List of enemy names that would get AoOs.</param>
    /// <param name="onConfirm">Callback when player confirms movement.</param>
    /// <param name="onCancel">Callback when player cancels movement.</param>
    public void ShowAoOWarning(List<string> enemyNames, System.Action onConfirm, System.Action onCancel)
    {
        _onAoOConfirm = onConfirm;
        _onAoOCancel = onCancel;

        // Build the warning panel if it doesn't exist
        if (_aooWarningPanel == null)
            BuildAoOWarningPanel();

        // Set the warning text
        string enemyList = string.Join(", ", enemyNames);
        _aooWarningText.text = $"⚠ ATTACK OF OPPORTUNITY WARNING ⚠\n\n" +
                               $"This movement will provoke Attacks of Opportunity from:\n" +
                               $"<color=#FF6666><b>{enemyList}</b></color>\n\n" +
                               $"Each enemy will get a free melee attack against you.\n" +
                               $"Continue moving?";

        _aooWarningPanel.SetActive(true);
        Debug.Log($"[CombatUI] AoO warning shown: {enemyList}");
    }

    /// <summary>
    /// Hide the AoO warning dialog.
    /// </summary>
    public void HideAoOWarning()
    {
        if (_aooWarningPanel != null)
            _aooWarningPanel.SetActive(false);
    }

    /// <summary>
    /// Build the AoO warning panel UI programmatically.
    /// </summary>
    private void BuildAoOWarningPanel()
    {
        // Find or create a canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Create overlay panel (blocks raycasts)
        _aooWarningPanel = new GameObject("AoOWarningPanel");
        _aooWarningPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRT = _aooWarningPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Semi-transparent background
        Image bgImage = _aooWarningPanel.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.7f);

        // Add CanvasGroup to block raycasts
        CanvasGroup cg = _aooWarningPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        // Dialog box
        GameObject dialogBox = new GameObject("DialogBox");
        dialogBox.transform.SetParent(_aooWarningPanel.transform, false);
        RectTransform dialogRT = dialogBox.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.25f, 0.25f);
        dialogRT.anchorMax = new Vector2(0.75f, 0.75f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialogBox.AddComponent<Image>();
        dialogBg.color = new Color(0.15f, 0.1f, 0.1f, 0.95f);

        // Add outline
        Outline dialogOutline = dialogBox.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        dialogOutline.effectDistance = new Vector2(2, 2);

        // Warning text
        GameObject textObj = new GameObject("WarningText");
        textObj.transform.SetParent(dialogBox.transform, false);
        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.05f, 0.3f);
        textRT.anchorMax = new Vector2(0.95f, 0.95f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        _aooWarningText = textObj.AddComponent<Text>();
        _aooWarningText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_aooWarningText.font == null)
            _aooWarningText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        _aooWarningText.fontSize = 16;
        _aooWarningText.color = Color.white;
        _aooWarningText.alignment = TextAnchor.MiddleCenter;
        _aooWarningText.supportRichText = true;
        _aooWarningText.text = "";

        // Confirm button
        _aooConfirmButton = CreateAoOButton(dialogBox, "ConfirmBtn",
            "⚔ CONFIRM (Provoke AoO)", new Vector2(0.1f, 0.05f), new Vector2(0.48f, 0.22f),
            new Color(0.6f, 0.2f, 0.2f, 1f));
        _aooConfirmButton.onClick.AddListener(() =>
        {
            HideAoOWarning();
            _onAoOConfirm?.Invoke();
        });

        // Cancel button
        _aooCancelButton = CreateAoOButton(dialogBox, "CancelBtn",
            "✋ CANCEL (Stay Safe)", new Vector2(0.52f, 0.05f), new Vector2(0.9f, 0.22f),
            new Color(0.2f, 0.4f, 0.2f, 1f));
        _aooCancelButton.onClick.AddListener(() =>
        {
            HideAoOWarning();
            _onAoOCancel?.Invoke();
        });

        _aooWarningPanel.SetActive(false);
    }

    /// <summary>Helper to create a styled button for the AoO dialog.</summary>
    private Button CreateAoOButton(GameObject parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent.transform, false);
        RectTransform btnRT = btnObj.AddComponent<RectTransform>();
        btnRT.anchorMin = anchorMin;
        btnRT.anchorMax = anchorMax;
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r + 0.15f, bgColor.g + 0.15f, bgColor.b + 0.15f, 1f);
        colors.pressedColor = new Color(bgColor.r - 0.1f, bgColor.g - 0.1f, bgColor.b - 0.1f, 1f);
        btn.colors = colors;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(5, 2);
        txtRT.offsetMax = new Vector2(-5, -2);

        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null)
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = label;
        txt.fontStyle = FontStyle.Bold;

        return btn;
    }
}
