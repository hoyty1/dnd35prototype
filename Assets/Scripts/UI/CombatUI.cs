using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Manages all combat UI elements with D&D 3.5 ability score display.
/// Shows 6 ability scores with modifiers and derived stats for each character.
/// Supports 4 PC panels, multiple NPC panels, initiative order display, and character icons.
/// Includes action economy buttons (Move, Attack, Full Attack, Dual Wield, End Turn).
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
    public Text CombatLogText;

    [Header("Action Buttons - Action Economy")]
    public GameObject ActionPanel;
    public Button MoveButton;
    public Button AttackButton;         // Single attack (Standard Action)
    public Button FullAttackButton;     // Full Attack (Full-Round Action)
    public Button DualWieldButton;      // Dual Wield (Full-Round Action)
    public Button EndTurnButton;
    public Text ActionStatusText;       // Shows current action economy status

    [Header("Monk/Barbarian Buttons")]
    public Button FlurryOfBlowsButton;     // Flurry of Blows (Full-Round Action, Monk only)
    public Button RageButton;              // Barbarian Rage (Free Action)
    public Text RageStatusText;            // Shows rage/fatigue status

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
        UpdateCharacterStats(pc1, PC1NameText, PC1HPText, PC1ACText, PC1AtkText, PC1SpeedText, PC1AbilityText, PC1HPBar);
        UpdateCharacterStats(pc2, PC2NameText, PC2HPText, PC2ACText, PC2AtkText, PC2SpeedText, PC2AbilityText, PC2HPBar);
        UpdateCharacterStats(npc, NPCNameText, NPCHPText, NPCACText, NPCAtkText, NPCSpeedText, NPCAbilityText, NPCHPBar);
    }

    /// <summary>
    /// Update stat displays for all 4 PCs and multiple NPCs.
    /// </summary>
    public void UpdateAllStats4PC(List<CharacterController> pcs, List<CharacterController> npcs)
    {
        // Update PC panels
        if (pcs.Count > 0) UpdateCharacterStats(pcs[0], PC1NameText, PC1HPText, PC1ACText, PC1AtkText, PC1SpeedText, PC1AbilityText, PC1HPBar);
        if (pcs.Count > 1) UpdateCharacterStats(pcs[1], PC2NameText, PC2HPText, PC2ACText, PC2AtkText, PC2SpeedText, PC2AbilityText, PC2HPBar);
        if (pcs.Count > 2) UpdateCharacterStats(pcs[2], PC3NameText, PC3HPText, PC3ACText, PC3AtkText, PC3SpeedText, PC3AbilityText, PC3HPBar);
        if (pcs.Count > 3) UpdateCharacterStats(pcs[3], PC4NameText, PC4HPText, PC4ACText, PC4AtkText, PC4SpeedText, PC4AbilityText, PC4HPBar);

        // Update each NPC panel
        for (int i = 0; i < NPCPanels.Count && i < npcs.Count; i++)
        {
            var p = NPCPanels[i];
            UpdateCharacterStats(npcs[i], p.NameText, p.HPText, p.ACText, p.AtkText, p.SpeedText, p.AbilityText, p.HPBar);
        }

        // Also update legacy single-NPC fields if they exist (backward compat)
        if (npcs.Count > 0)
            UpdateCharacterStats(npcs[0], NPCNameText, NPCHPText, NPCACText, NPCAtkText, NPCSpeedText, NPCAbilityText, NPCHPBar);
    }

    /// <summary>
    /// Update stat displays for both PCs and multiple NPCs (legacy 2-PC version).
    /// </summary>
    public void UpdateAllStatsMultiNPC(CharacterController pc1, CharacterController pc2, List<CharacterController> npcs)
    {
        UpdateCharacterStats(pc1, PC1NameText, PC1HPText, PC1ACText, PC1AtkText, PC1SpeedText, PC1AbilityText, PC1HPBar);
        UpdateCharacterStats(pc2, PC2NameText, PC2HPText, PC2ACText, PC2AtkText, PC2SpeedText, PC2AbilityText, PC2HPBar);

        // Update each NPC panel
        for (int i = 0; i < NPCPanels.Count && i < npcs.Count; i++)
        {
            var p = NPCPanels[i];
            UpdateCharacterStats(npcs[i], p.NameText, p.HPText, p.ACText, p.AtkText, p.SpeedText, p.AbilityText, p.HPBar);
        }

        // Also update legacy single-NPC fields if they exist (backward compat)
        if (npcs.Count > 0)
            UpdateCharacterStats(npcs[0], NPCNameText, NPCHPText, NPCACText, NPCAtkText, NPCSpeedText, NPCAbilityText, NPCHPBar);
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
            int effectiveDex = s.DEXMod;
            if (s.MaxDexBonus >= 0 && effectiveDex > s.MaxDexBonus)
                effectiveDex = s.MaxDexBonus;
            string dexLabel = (s.MaxDexBonus >= 0 && s.DEXMod > s.MaxDexBonus)
                ? $"DEX*" : "DEX";
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

    /// <summary>
    /// Update the initiative order display text.
    /// </summary>
    public void UpdateInitiativeDisplay(string initiativeText)
    {
        if (InitiativeOrderText != null)
        {
            InitiativeOrderText.supportRichText = true;
            InitiativeOrderText.text = initiativeText;
        }
        if (InitiativePanel != null)
            InitiativePanel.SetActive(!string.IsNullOrEmpty(initiativeText));
    }

    public void ShowCombatLog(string message)
    {
        if (CombatLogText != null)
        {
            CombatLogText.supportRichText = true;
            string formatted = HighlightCriticalHits(message);
            CombatLogText.text = formatted;
        }
    }

    /// <summary>
    /// Apply rich-text color highlights for combat log text.
    /// </summary>
    private string HighlightCriticalHits(string text)
    {
        text = text.Replace("- HIT!", "- <color=#66FF66><b>HIT!</b></color>");
        text = text.Replace("- MISS!", "- <color=#FF4444><b>MISS!</b></color>");
        text = text.Replace("CRITICAL HIT!", "<color=#FFD700><b>CRITICAL HIT!</b></color>");
        text = text.Replace("CRIT!", "<color=#FFD700><b>CRIT!</b></color>");
        text = text.Replace("*** Critical Threat", "<color=#FFA500><b>*** Critical Threat</b></color>");
        text = text.Replace("CONFIRMED!", "<color=#FFD700><b>CONFIRMED!</b></color>");
        text = text.Replace("Not confirmed", "<color=#AAAAAA>Not confirmed</color>");
        text = text.Replace("(NATURAL 20!)", "<color=#FFD700><b>(NATURAL 20!)</b></color>");
        text = text.Replace("(NAT 20!)", "<color=#FFD700><b>(NAT 20!)</b></color>");
        text = text.Replace("(NATURAL 1!)", "<color=#FF4444><b>(NATURAL 1!)</b></color>");
        text = text.Replace("(NAT 1!)", "<color=#FF4444><b>(NAT 1!)</b></color>");
        text = text.Replace("has been slain!", "<color=#FF6666><b>has been slain!</b></color>");
        text = text.Replace("no penalty", "<color=#66FF66>no penalty</color>");
        text = text.Replace("beyond maximum range!", "<color=#FF4444><b>beyond maximum range!</b></color>");
        text = text.Replace("Power Attack", "<color=#FF9933>Power Attack</color>");
        text = text.Replace("Rapid Shot", "<color=#66CCFF>Rapid Shot</color>");
        text = text.Replace("Point Blank Shot", "<color=#66FF66>Point Blank Shot</color>");
        text = text.Replace("═══════════════════════════════════", "<color=#888888>═══════════════════════════════════</color>");
        text = text.Replace("total damage", "<color=#FFAA44><b>total damage</b></color>");
        text = text.Replace(" damage", " <color=#FFAA44>damage</color>");
        text = text.Replace(" → ", " <color=#FFFF66>→</color> ");
        return text;
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
        }
    }

    // ========== ACTION ECONOMY UI ==========

    /// <summary>
    /// Update the action panel buttons based on the current action economy state.
    /// </summary>
    public void UpdateActionButtons(CharacterController pc)
    {
        if (pc == null || ActionPanel == null) return;

        var actions = pc.Actions;
        bool canDualWield = pc.CanDualWield();
        bool hasIterativeAttacks = pc.Stats.IterativeAttackCount > 1;
        bool hasRapidShot = pc.Stats.HasFeat("Rapid Shot");
        bool fullAttackRelevant = hasIterativeAttacks || hasRapidShot;

        if (MoveButton != null)
        {
            bool canMove = actions.HasMoveAction || actions.CanConvertStandardToMove;
            MoveButton.gameObject.SetActive(true);
            MoveButton.interactable = canMove;
            Text moveLabel = MoveButton.GetComponentInChildren<Text>();
            if (moveLabel != null)
            {
                if (actions.HasMoveAction) moveLabel.text = "Move (Move Action)";
                else if (actions.CanConvertStandardToMove) moveLabel.text = "Move (Std→Move)";
                else moveLabel.text = "Move (Used)";
            }
        }

        if (AttackButton != null)
        {
            AttackButton.gameObject.SetActive(true);
            AttackButton.interactable = actions.HasStandardAction;
            Text atkLabel = AttackButton.GetComponentInChildren<Text>();
            if (atkLabel != null)
                atkLabel.text = actions.HasStandardAction ? "Attack (Standard)" : "Attack (Used)";
        }

        if (FullAttackButton != null)
        {
            bool showFullAtk = fullAttackRelevant && actions.HasFullRoundAction;
            FullAttackButton.gameObject.SetActive(fullAttackRelevant);
            FullAttackButton.interactable = showFullAtk;
            Text faLabel = FullAttackButton.GetComponentInChildren<Text>();
            if (faLabel != null)
            {
                int atkCount = pc.Stats.IterativeAttackCount;
                bool weaponIsRanged = pc.IsEquippedWeaponRanged();
                bool rapidShotWillApply = hasRapidShot && pc.RapidShotEnabled && weaponIsRanged;
                string label;
                if (!showFullAtk) label = "Full Attack (N/A)";
                else if (rapidShotWillApply) label = $"Full Attack x{atkCount + 1} (Rapid Shot)";
                else if (hasRapidShot && pc.RapidShotEnabled && !weaponIsRanged) label = $"Full Attack x{atkCount} (RS: need ranged wpn)";
                else if (hasIterativeAttacks) label = $"Full Attack x{atkCount} (Full-Round)";
                else label = "Full Attack (Full-Round)";
                faLabel.text = label;
            }
        }

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

        if (FlurryOfBlowsButton != null)
        {
            bool isMonk = pc.Stats.IsMonk;
            bool canFlurry = isMonk && actions.HasFullRoundAction;
            FlurryOfBlowsButton.gameObject.SetActive(isMonk);
            FlurryOfBlowsButton.interactable = canFlurry;
            Text flurryLabel = FlurryOfBlowsButton.GetComponentInChildren<Text>();
            if (flurryLabel != null)
            {
                if (canFlurry)
                {
                    int[] bonuses = pc.Stats.GetFlurryOfBlowsBonuses();
                    string bonusStr = string.Join("/", System.Array.ConvertAll(bonuses, b => CharacterStats.FormatMod(b)));
                    flurryLabel.text = $"Flurry of Blows x{bonuses.Length} ({bonusStr})";
                }
                else flurryLabel.text = "Flurry of Blows (N/A)";
            }
        }

        if (RageButton != null)
        {
            bool isBarbarian = pc.Stats.IsBarbarian;
            bool canRage = isBarbarian && !pc.Stats.IsRaging && !pc.Stats.IsFatigued
                           && pc.Stats.RagesUsedToday < pc.Stats.MaxRagesPerDay;
            RageButton.gameObject.SetActive(isBarbarian);
            RageButton.interactable = canRage;
            Text rageLabel = RageButton.GetComponentInChildren<Text>();
            if (rageLabel != null)
            {
                if (pc.Stats.IsRaging) rageLabel.text = $"RAGING ({pc.Stats.RageRoundsRemaining} rds)";
                else if (pc.Stats.IsFatigued) rageLabel.text = "Rage (Fatigued)";
                else if (canRage) rageLabel.text = $"Rage ({pc.Stats.MaxRagesPerDay - pc.Stats.RagesUsedToday}/day)";
                else rageLabel.text = "Rage (Used)";
            }
        }

        if (RageStatusText != null)
        {
            bool isBarbarian = pc.Stats.IsBarbarian;
            RageStatusText.gameObject.SetActive(isBarbarian && (pc.Stats.IsRaging || pc.Stats.IsFatigued));
            if (pc.Stats.IsRaging)
                RageStatusText.text = $"⚡ RAGING! {pc.Stats.RageRoundsRemaining} rounds left | -2 AC | +4 STR/CON | +2 Will";
            else if (pc.Stats.IsFatigued)
                RageStatusText.text = "😫 FATIGUED: -2 STR, -2 DEX";
        }

        if (EndTurnButton != null)
        {
            EndTurnButton.gameObject.SetActive(true);
            EndTurnButton.interactable = true;
        }

        if (ActionStatusText != null)
            ActionStatusText.text = actions.GetStatusString();
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

    /// <summary>
    /// Highlight which PC is currently active (1-4). Pass 0 to clear all.
    /// </summary>
    public void SetActivePC(int pcNumber)
    {
        if (PC1ActiveIndicator != null) PC1ActiveIndicator.enabled = (pcNumber == 1);
        if (PC2ActiveIndicator != null) PC2ActiveIndicator.enabled = (pcNumber == 2);
        if (PC3ActiveIndicator != null) PC3ActiveIndicator.enabled = (pcNumber == 3);
        if (PC4ActiveIndicator != null) PC4ActiveIndicator.enabled = (pcNumber == 4);

        // Dim inactive panels, brighten active
        SetPanelActiveState(PC1Panel, pcNumber == 1, new Color(0.1f, 0.3f, 0.5f, 0.95f), new Color(0.1f, 0.2f, 0.4f, 0.65f));
        SetPanelActiveState(PC2Panel, pcNumber == 2, new Color(0.1f, 0.2f, 0.5f, 0.95f), new Color(0.1f, 0.15f, 0.35f, 0.65f));
        SetPanelActiveState(PC3Panel, pcNumber == 3, new Color(0.15f, 0.3f, 0.4f, 0.95f), new Color(0.1f, 0.2f, 0.3f, 0.65f));
        SetPanelActiveState(PC4Panel, pcNumber == 4, new Color(0.3f, 0.15f, 0.15f, 0.95f), new Color(0.25f, 0.1f, 0.1f, 0.65f));
    }

    private void SetPanelActiveState(GameObject panel, bool active, Color activeColor, Color inactiveColor)
    {
        if (panel == null) return;
        Image panelImg = panel.GetComponent<Image>();
        if (panelImg != null)
            panelImg.color = active ? activeColor : inactiveColor;
    }

    /// <summary>
    /// Set a PC panel's icon sprite.
    /// </summary>
    public void SetPCIcon(int pcNumber, Sprite icon)
    {
        Image target = null;
        switch (pcNumber)
        {
            case 1: target = PC1Icon; break;
            case 2: target = PC2Icon; break;
            case 3: target = PC3Icon; break;
            case 4: target = PC4Icon; break;
        }
        if (target != null && icon != null)
        {
            target.sprite = icon;
            target.enabled = true;
        }
    }

    /// <summary>
    /// Set an NPC panel's icon sprite.
    /// </summary>
    public void SetNPCIcon(int npcIndex, Sprite icon)
    {
        if (npcIndex < NPCPanels.Count && NPCPanels[npcIndex].IconImage != null && icon != null)
        {
            NPCPanels[npcIndex].IconImage.sprite = icon;
            NPCPanels[npcIndex].IconImage.enabled = true;
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

    public void ShowAoOWarning(List<string> enemyNames, System.Action onConfirm, System.Action onCancel)
    {
        _onAoOConfirm = onConfirm;
        _onAoOCancel = onCancel;

        if (_aooWarningPanel == null)
            BuildAoOWarningPanel();

        string enemyList = string.Join(", ", enemyNames);
        _aooWarningText.text = $"⚠ ATTACK OF OPPORTUNITY WARNING ⚠\n\n" +
                               $"This movement will provoke Attacks of Opportunity from:\n" +
                               $"<color=#FF6666><b>{enemyList}</b></color>\n\n" +
                               $"Each enemy will get a free melee attack against you.\n" +
                               $"Continue moving?";

        _aooWarningPanel.SetActive(true);
        Debug.Log($"[CombatUI] AoO warning shown: {enemyList}");
    }

    public void HideAoOWarning()
    {
        if (_aooWarningPanel != null)
            _aooWarningPanel.SetActive(false);
    }

    private void BuildAoOWarningPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _aooWarningPanel = new GameObject("AoOWarningPanel");
        _aooWarningPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRT = _aooWarningPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bgImage = _aooWarningPanel.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.7f);

        CanvasGroup cg = _aooWarningPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        GameObject dialogBox = new GameObject("DialogBox");
        dialogBox.transform.SetParent(_aooWarningPanel.transform, false);
        RectTransform dialogRT = dialogBox.AddComponent<RectTransform>();
        dialogRT.anchorMin = new Vector2(0.25f, 0.25f);
        dialogRT.anchorMax = new Vector2(0.75f, 0.75f);
        dialogRT.offsetMin = Vector2.zero;
        dialogRT.offsetMax = Vector2.zero;

        Image dialogBg = dialogBox.AddComponent<Image>();
        dialogBg.color = new Color(0.15f, 0.1f, 0.1f, 0.95f);

        Outline dialogOutline = dialogBox.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        dialogOutline.effectDistance = new Vector2(2, 2);

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

        _aooConfirmButton = CreateAoOButton(dialogBox, "ConfirmBtn",
            "⚔ CONFIRM (Provoke AoO)", new Vector2(0.1f, 0.05f), new Vector2(0.48f, 0.22f),
            new Color(0.6f, 0.2f, 0.2f, 1f));
        _aooConfirmButton.onClick.AddListener(() => { HideAoOWarning(); _onAoOConfirm?.Invoke(); });

        _aooCancelButton = CreateAoOButton(dialogBox, "CancelBtn",
            "✋ CANCEL (Stay Safe)", new Vector2(0.52f, 0.05f), new Vector2(0.9f, 0.22f),
            new Color(0.2f, 0.4f, 0.2f, 1f));
        _aooCancelButton.onClick.AddListener(() => { HideAoOWarning(); _onAoOCancel?.Invoke(); });

        _aooWarningPanel.SetActive(false);
    }

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
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = label;
        txt.fontStyle = FontStyle.Bold;

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
}
