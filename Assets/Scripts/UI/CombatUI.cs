using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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
    private List<string> _combatLogMessages = new List<string>();
    private const int MaxLogMessages = 200; // Prevent unbounded growth

    [Header("Action Buttons - Action Economy")]
    public GameObject ActionPanel;
    public Button MoveButton;
    public Button FiveFootStepButton;   // 5-foot step (Free Action, no AoO)
    public Button DropProneButton;      // Drop prone (Free Action)
    public Button StandUpButton;        // Stand up (Move Action, provokes AoO)
    public Button CrawlButton;          // Crawl 5 ft (Move Action, provokes AoO)
    public Button AttackButton;         // Single attack (Standard Action)
    public Button FullAttackButton;     // Full Attack (Full-Round Action)
    public Button SpecialAttackButton;  // Combat maneuvers (Standard Action)
    public Button ChargeButton;         // Charge (Full-Round Action)
    public Button DualWieldButton;      // Dual Wield (Full-Round Action)
    public Button EndTurnButton;
    public Button ReloadButton;         // Reload equipped crossbow (action varies by weapon/feat)
    public Text ActionStatusText;       // Shows current action economy status

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

        // Update buff displays for PCs
        if (pcs.Count > 0) UpdateBuffDisplay(pcs[0], PC1BuffText);
        if (pcs.Count > 1) UpdateBuffDisplay(pcs[1], PC2BuffText);
        if (pcs.Count > 2) UpdateBuffDisplay(pcs[2], PC3BuffText);
        if (pcs.Count > 3) UpdateBuffDisplay(pcs[3], PC4BuffText);

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
            string displayName = s.CharacterName;
            if (GameManager.Instance != null)
                displayName = GameManager.Instance.GetSummonDisplayName(ch);

            nameText.text = $"{displayName} (Lv {s.Level} {raceStr}{s.CharacterClass}){sizeStr}";
            if (s.IsDead) nameText.text += " (DEAD)";
        }

        // Use TotalMaxHP (includes feat bonuses like Toughness and spell-based BonusMaxHP)
        int totalMax = s.TotalMaxHP;
        if (totalMax <= 0) totalMax = 1; // Guard against division by zero

        if (hpText != null)
            hpText.text = $"HP: {s.CurrentHP}/{totalMax}";

        if (acText != null)
        {
            int effectiveDex = s.DEXMod;
            if (s.MaxDexBonus >= 0 && effectiveDex > s.MaxDexBonus)
                effectiveDex = s.MaxDexBonus;
            string dexLabel = (s.MaxDexBonus >= 0 && s.DEXMod > s.MaxDexBonus)
                ? $"DEX*" : "DEX";
            string sizeDetail = FormatBonusDetail(s.SizeModifier, "Size");
            string defensiveDetail = ch.IsFightingDefensively ? " +2 Fighting Defensively" : "";
            string acDetails = $"AC: {s.ArmorClass + (ch.IsFightingDefensively ? 2 : 0)} (10{FormatBonusDetail(effectiveDex, dexLabel)}{FormatBonusDetail(s.ArmorBonus, "Armor")}{FormatBonusDetail(s.ShieldBonus, "Shield")}{sizeDetail}{defensiveDetail})";
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
        {
            // Update fill amount based on HP percentage using TotalMaxHP
            float hpPercent = (float)s.CurrentHP / totalMax;
            hpBar.fillAmount = hpPercent;

            // Update HP bar color based on HP percentage:
            //   > 50%  → Green
            //   > 25%  → Yellow
            //   ≤ 25%  → Red
            Color hpColor;
            if (hpPercent > 0.5f)
                hpColor = new Color(0.2f, 0.8f, 0.2f, 1f);  // Green
            else if (hpPercent > 0.25f)
                hpColor = new Color(0.8f, 0.8f, 0.2f, 1f);  // Yellow
            else
                hpColor = new Color(0.8f, 0.2f, 0.2f, 1f);  // Red

            hpBar.color = hpColor;
        }
    }

    /// <summary>
    /// Update the buff display text for a character, showing active spell effects.
    /// </summary>
    private void UpdateBuffDisplay(CharacterController ch, Text buffText)
    {
        if (buffText == null) return;
        if (ch == null || ch.Stats == null)
        {
            buffText.text = "";
            return;
        }

        var statusMgr = ch.GetComponent<StatusEffectManager>();
        var concMgr = ch.GetComponent<ConcentrationManager>();
        var spellComp = ch.GetComponent<SpellcastingComponent>();

        string buffStr = (statusMgr != null && statusMgr.ActiveEffectCount > 0)
            ? statusMgr.GetBuffSummaryString() : "";
        string concStr = (concMgr != null && concMgr.IsConcentrating)
            ? concMgr.GetConcentrationDisplayString() : "";
        string heldStr = (spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null)
            ? $"✋ Holding: {spellComp.HeldTouchSpell.Name}" : "";
        string condStr = ch.Stats.GetConditionSummary();

        if (string.IsNullOrEmpty(buffStr) && string.IsNullOrEmpty(concStr) && string.IsNullOrEmpty(heldStr) && string.IsNullOrEmpty(condStr))
        {
            buffText.text = "";
            buffText.gameObject.SetActive(false);
            return;
        }

        buffText.gameObject.SetActive(true);
        buffText.supportRichText = true;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(concStr)) parts.Add(concStr);
        if (!string.IsNullOrEmpty(heldStr)) parts.Add(heldStr);
        if (!string.IsNullOrEmpty(condStr)) parts.Add(condStr);
        if (!string.IsNullOrEmpty(buffStr)) parts.Add(buffStr);
        buffText.text = string.Join(" ", parts);
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
    /// Build a standardized short flanking indicator for turn/targeting text.
    /// </summary>
    public string BuildFlankingIndicator(bool isFlanking, CharacterController flankingAlly)
    {
        if (!isFlanking) return string.Empty;

        string allyName = flankingAlly != null && flankingAlly.Stats != null
            ? flankingAlly.Stats.CharacterName
            : string.Empty;

        return string.IsNullOrEmpty(allyName)
            ? " (FLANKING +2)"
            : $" (FLANKING +2 with {allyName})";
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

    /// <summary>
    /// Adds a message to the persistent scrollable combat log.
    /// Messages accumulate throughout combat and can be scrolled.
    /// </summary>
    public void ShowCombatLog(string message)
    {
        // --- New scrollable log path ---
        if (CombatLogContent != null)
        {
            string formatted = HighlightCriticalHits(message);
            _combatLogMessages.Add(formatted);

            // Create a new Text element as a child of the content container
            GameObject msgObj = new GameObject($"LogMsg_{_combatLogMessages.Count}");
            msgObj.transform.SetParent(CombatLogContent.transform, false);

            Text text = msgObj.AddComponent<Text>();
            text.supportRichText = true;
            text.text = formatted;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (text.font == null) text.font = Font.CreateDynamicFontFromOSFont("Arial", 11);
            text.fontSize = 11;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // Let VerticalLayoutGroup + ContentSizeFitter handle sizing
            LayoutElement le = msgObj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;

            // Trim oldest messages if over limit
            if (_combatLogMessages.Count > MaxLogMessages && CombatLogContent.transform.childCount > MaxLogMessages)
            {
                Transform oldest = CombatLogContent.transform.GetChild(0);
                Destroy(oldest.gameObject);
                _combatLogMessages.RemoveAt(0);
            }

            // Auto-scroll to bottom next frame (let layout recalculate)
            StartCoroutine(ScrollToBottomNextFrame());
            return;
        }

        // --- Fallback: legacy single-text field ---
        if (CombatLogText != null)
        {
            CombatLogText.supportRichText = true;
            string formatted = HighlightCriticalHits(message);
            CombatLogText.text = formatted;
        }
    }

    /// <summary>
    /// Adds a visual turn separator line to the combat log.
    /// </summary>
    public void AddTurnSeparator(int turnNumber)
    {
        ShowCombatLog($"<color=#888888>─────── Turn {turnNumber} ───────</color>");
    }

    /// <summary>
    /// Clears all messages from the combat log. Call when combat ends.
    /// </summary>
    public void ClearCombatLog()
    {
        _combatLogMessages.Clear();

        if (CombatLogContent != null)
        {
            foreach (Transform child in CombatLogContent.transform)
            {
                Destroy(child.gameObject);
            }
        }

        // Also clear legacy text
        if (CombatLogText != null)
            CombatLogText.text = "";
    }

    /// <summary>
    /// Coroutine that scrolls the combat log to the bottom after one frame,
    /// giving layout groups time to recalculate content size.
    /// </summary>
    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null; // Wait one frame for layout update
        if (CombatLogScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            CombatLogScrollRect.verticalNormalizedPosition = 0f; // 0 = bottom
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
        text = text.Replace("Fighting Defensively", "<color=#99CCFF>Fighting Defensively</color>");
        text = text.Replace("Shooting into melee", "<color=#FFCC66>Shooting into melee</color>");
        text = text.Replace("Precise Shot", "<color=#99FF99>Precise Shot</color>");

        // Highlight spellcasting and metamagic
        text = text.Replace("SPELL CAST!", "<color=#BB88FF><b>SPELL CAST!</b></color>");
        text = text.Replace("Metamagic:", "<color=#FFB833><b>Metamagic:</b></color>");
        text = text.Replace("Empower:", "<color=#FFB833>Empower:</color>");
        text = text.Replace("QUICKENED", "<color=#FFD700><b>QUICKENED</b></color>");
        text = text.Replace("⚡", "<color=#FFB833>⚡</color>");
        text = text.Replace("healed!", "<color=#66FF66><b>healed!</b></color>");
        text = text.Replace("BUFF APPLIED!", "<color=#6699FF><b>BUFF APPLIED!</b></color>");
        text = text.Replace("RESISTED!", "<color=#AAAAAA><b>RESISTED!</b></color>");
        text = text.Replace("Touch Attack", "<color=#BB88FF>Touch Attack</color>");
        text = text.Replace("Fortitude save", "<color=#FFAA44>Fortitude save</color>");
        text = text.Replace("Reflex save", "<color=#FFAA44>Reflex save</color>");
        text = text.Replace("Will save", "<color=#FFAA44>Will save</color>");

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
            if (DischargeTouchButton != null) DischargeTouchButton.gameObject.SetActive(false);
            HideSpecialAttackMenu();
        }
    }

    private GameObject _touchSpellPromptPanel;
    private GameObject _specialAttackPanel;
    private GameObject _summonSelectionPanel;
    private bool _dischargeTouchHooked;
    private GameObject _summonContextMenuRoot;

    private GameObject _confirmationPanel;

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
        bool isProne = pc.HasCondition(CombatConditionType.Prone);
        bool canAttackWithWeapon = pc.CanAttackWithEquippedWeapon(out _);

        if (MoveButton != null)
        {
            bool canMoveByActions = actions.HasMoveAction || actions.CanConvertStandardToMove;
            bool blockedByFiveFootStep = pc.HasTakenFiveFootStep;
            bool canMove = canMoveByActions && !blockedByFiveFootStep && !isProne;

            MoveButton.gameObject.SetActive(true);
            MoveButton.interactable = canMove;
            Text moveLabel = MoveButton.GetComponentInChildren<Text>();
            if (moveLabel != null)
            {
                if (isProne) moveLabel.text = "Move (Stand up first)";
                else if (blockedByFiveFootStep) moveLabel.text = "Move (After 5-ft step: no)";
                else if (actions.HasMoveAction) moveLabel.text = "Move (Move Action)";
                else if (actions.CanConvertStandardToMove) moveLabel.text = "Move (Std→Move)";
                else moveLabel.text = "Move (Used)";
            }
        }

        if (FiveFootStepButton != null)
        {
            string disabledReason = GameManager.Instance != null ? GameManager.Instance.GetFiveFootStepDisabledReason(pc) : "Unavailable";
            bool canFiveFootStep = string.IsNullOrEmpty(disabledReason);
            FiveFootStepButton.gameObject.SetActive(true);
            FiveFootStepButton.interactable = canFiveFootStep;
            Text fiveFootLabel = FiveFootStepButton.GetComponentInChildren<Text>();
            if (fiveFootLabel != null)
                fiveFootLabel.text = canFiveFootStep ? "5-Foot Step (Free)" : $"5-Foot Step ({disabledReason})";
        }

        if (DropProneButton != null)
        {
            string disabledReason = GameManager.Instance != null ? GameManager.Instance.GetDropProneDisabledReason(pc) : "Unavailable";
            bool canDropProne = string.IsNullOrEmpty(disabledReason);
            DropProneButton.gameObject.SetActive(!isProne);
            DropProneButton.interactable = canDropProne;
            Text dropProneLabel = DropProneButton.GetComponentInChildren<Text>();
            if (dropProneLabel != null)
                dropProneLabel.text = canDropProne ? "Drop Prone (Free)" : $"Drop Prone ({disabledReason})";
        }

        if (StandUpButton != null)
        {
            string disabledReason = GameManager.Instance != null ? GameManager.Instance.GetStandUpDisabledReason(pc) : "Unavailable";
            bool canStandUp = string.IsNullOrEmpty(disabledReason);
            StandUpButton.gameObject.SetActive(isProne);
            StandUpButton.interactable = canStandUp;
            Text standUpLabel = StandUpButton.GetComponentInChildren<Text>();
            if (standUpLabel != null)
                standUpLabel.text = canStandUp ? "Stand Up (Move, AoO)" : $"Stand Up ({disabledReason})";
        }

        if (CrawlButton != null)
        {
            string disabledReason = GameManager.Instance != null ? GameManager.Instance.GetCrawlDisabledReason(pc) : "Unavailable";
            bool canCrawl = string.IsNullOrEmpty(disabledReason);
            CrawlButton.gameObject.SetActive(isProne);
            CrawlButton.interactable = canCrawl;
            Text crawlLabel = CrawlButton.GetComponentInChildren<Text>();
            if (crawlLabel != null)
                crawlLabel.text = canCrawl ? "Crawl 5 ft (Move, AoO)" : $"Crawl ({disabledReason})";
        }

        bool canFightDefensively = pc.Stats.BaseAttackBonus >= 1;

        if (AttackButton != null)
        {
            bool canSingleAttack = actions.HasStandardAction && canAttackWithWeapon;
            AttackButton.gameObject.SetActive(true);
            AttackButton.interactable = canSingleAttack;
            Text atkLabel = AttackButton.GetComponentInChildren<Text>();
            if (atkLabel != null)
            {
                if (!actions.HasStandardAction) atkLabel.text = "Attack (Used)";
                else if (!canAttackWithWeapon) atkLabel.text = "Attack (Reload first)";
                else atkLabel.text = "Attack (Standard)";
            }
        }

        if (AttackDefensivelyButton != null)
        {
            bool canAttackDefensively = actions.HasStandardAction && canFightDefensively && canAttackWithWeapon;
            AttackDefensivelyButton.gameObject.SetActive(true);
            AttackDefensivelyButton.interactable = canAttackDefensively;
            Text atkDefLabel = AttackDefensivelyButton.GetComponentInChildren<Text>();
            if (atkDefLabel != null)
            {
                if (!canFightDefensively) atkDefLabel.text = "Fighting Defensively (Std) [BAB +1]";
                else if (!actions.HasStandardAction) atkDefLabel.text = "Fighting Defensively (Std) [Used]";
                else if (!canAttackWithWeapon) atkDefLabel.text = "Fighting Defensively (Std) [Reload first]";
                else atkDefLabel.text = "Fighting Defensively (Std)";
            }
        }

        if (SpecialAttackButton != null)
        {
            SpecialAttackButton.gameObject.SetActive(true);
            SpecialAttackButton.interactable = actions.HasStandardAction;
            Text spLabel = SpecialAttackButton.GetComponentInChildren<Text>();
            if (spLabel != null)
                spLabel.text = actions.HasStandardAction ? "Special Attack (Standard)" : "Special Attack (Used)";
        }

        if (ChargeButton != null)
        {
            bool hasFullRound = actions.HasFullRoundAction;
            bool hasMeleeThreat = pc.HasMeleeWeaponEquipped();
            bool fatigued = pc.Stats != null && pc.Stats.IsFatigued;
            bool blockedByFiveFootStep = pc.HasTakenFiveFootStep;
            bool blockedByProne = isProne;
            bool hasAnyChargeTarget = GameManager.Instance != null && GameManager.Instance.HasAnyValidChargeTarget(pc);
            bool canChargeTarget = hasFullRound && hasMeleeThreat && !fatigued && !blockedByFiveFootStep && !blockedByProne && hasAnyChargeTarget;

            ChargeButton.gameObject.SetActive(hasMeleeThreat || fatigued || blockedByFiveFootStep || blockedByProne);
            ChargeButton.interactable = canChargeTarget;
            Text chargeLabel = ChargeButton.GetComponentInChildren<Text>();
            if (chargeLabel != null)
            {
                if (!hasMeleeThreat) chargeLabel.text = "Charge (Need melee)";
                else if (fatigued) chargeLabel.text = "Charge (Fatigued)";
                else if (blockedByProne) chargeLabel.text = "Charge (Prone)";
                else if (blockedByFiveFootStep) chargeLabel.text = "Charge (After 5-ft step: no)";
                else if (!hasFullRound) chargeLabel.text = "Charge (Used)";
                else if (!hasAnyChargeTarget) chargeLabel.text = "Charge (No lane)";
                else chargeLabel.text = "Charge (Full-Round)";
            }
        }

        bool canTakeFullRoundAttack = fullAttackRelevant && actions.HasFullRoundAction && canAttackWithWeapon;
        if (FullAttackButton != null)
        {
            FullAttackButton.gameObject.SetActive(fullAttackRelevant);
            FullAttackButton.interactable = canTakeFullRoundAttack;
            Text faLabel = FullAttackButton.GetComponentInChildren<Text>();
            if (faLabel != null)
            {
                int atkCount = pc.Stats.IterativeAttackCount;
                bool weaponIsRanged = pc.IsEquippedWeaponRanged();
                bool rapidShotWillApply = hasRapidShot && pc.RapidShotEnabled && weaponIsRanged;

                if (!canAttackWithWeapon) faLabel.text = "Full Attack (Reload first)";
                else if (!canTakeFullRoundAttack) faLabel.text = "Full Attack (N/A)";
                else if (rapidShotWillApply) faLabel.text = $"Full Attack x{atkCount + 1} (Rapid Shot)";
                else if (hasRapidShot && pc.RapidShotEnabled && !weaponIsRanged) faLabel.text = $"Full Attack x{atkCount} (RS: need ranged wpn)";
                else if (hasIterativeAttacks) faLabel.text = $"Full Attack x{atkCount} (Full-Round)";
                else faLabel.text = "Full Attack (Full-Round)";
            }
        }

        if (FullAttackDefensivelyButton != null)
        {
            FullAttackDefensivelyButton.gameObject.SetActive(fullAttackRelevant);
            bool canFullAttackDefensively = canTakeFullRoundAttack && canFightDefensively;
            FullAttackDefensivelyButton.interactable = canFullAttackDefensively;
            Text fullDefLabel = FullAttackDefensivelyButton.GetComponentInChildren<Text>();
            if (fullDefLabel != null)
            {
                if (!canFightDefensively) fullDefLabel.text = "Full Attack (Def) [BAB +1]";
                else if (!canAttackWithWeapon) fullDefLabel.text = "Full Attack (Def) [Reload first]";
                else if (!canTakeFullRoundAttack) fullDefLabel.text = "Full Attack (Def) [N/A]";
                else fullDefLabel.text = "Full Attack (Def)";
            }
        }

        if (DualWieldButton != null)
        {
            bool canDW = canDualWield && actions.HasFullRoundAction;
            bool canDualWieldAttack = true;
            if (canDualWield)
            {
                var inv = pc.GetComponent<InventoryComponent>();
                ItemData mainWeapon = inv != null ? inv.CharacterInventory.RightHandSlot : null;
                ItemData offWeapon = inv != null ? inv.CharacterInventory.LeftHandSlot : null;
                bool canMainAttack = pc.CanAttackWithWeapon(mainWeapon, out _);
                bool canOffAttack = pc.CanAttackWithWeapon(offWeapon, out _);
                canDualWieldAttack = canMainAttack || canOffAttack;
            }

            DualWieldButton.gameObject.SetActive(canDualWield);
            DualWieldButton.interactable = canDW && canDualWieldAttack;
            Text dwLabel = DualWieldButton.GetComponentInChildren<Text>();
            if (dwLabel != null)
                dwLabel.text = !canDualWieldAttack ? "Dual Wield (Reload first)" : (canDW ? "Dual Wield (Full-Round)" : "Dual Wield (N/A)");
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
            bool canRage = isBarbarian && !pc.Stats.IsRaging && !pc.Stats.IsFatigued && pc.Stats.RagesUsedToday < pc.Stats.MaxRagesPerDay;
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

        if (CastSpellButton != null)
        {
            bool isSpellcaster = pc.Stats.IsSpellcaster;
            var spellComp = pc.GetComponent<SpellcastingComponent>();
            bool hasCastableSpells = isSpellcaster && spellComp != null && spellComp.HasAnyCastablePreparedSpell();
            bool canCast = actions.HasStandardAction && hasCastableSpells;

            CastSpellButton.gameObject.SetActive(hasCastableSpells);
            CastSpellButton.interactable = canCast;
            Text castLabel = CastSpellButton.GetComponentInChildren<Text>();
            if (castLabel != null)
            {
                string baseLabel = canCast ? "Cast Spell (Standard)" : "Cast Spell (N/A)";
                int asfChance = (pc.Stats != null && pc.Stats.IsAffectedByArcaneSpellFailure)
                    ? Mathf.Clamp(pc.Stats.ArcaneSpellFailure, 0, 100)
                    : 0;

                castLabel.text = asfChance > 0
                    ? $"{baseLabel}\n⚠ ASF {asfChance}%"
                    : baseLabel;
            }
        }

        EnsureDischargeTouchButtonExists();
        if (DischargeTouchButton != null)
        {
            var spellComp = pc.GetComponent<SpellcastingComponent>();
            bool hasHeldTouchCharge = pc.Stats.IsSpellcaster && spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null;
            DischargeTouchButton.gameObject.SetActive(hasHeldTouchCharge);
            DischargeTouchButton.interactable = hasHeldTouchCharge;

            Text dischargeLabel = DischargeTouchButton.GetComponentInChildren<Text>();
            if (dischargeLabel != null)
            {
                string heldName = hasHeldTouchCharge ? spellComp.HeldTouchSpell.Name : "Touch";
                dischargeLabel.text = $"Discharge {heldName}";
            }
        }

        if (ReloadButton != null)
        {
            ItemData weapon = pc.GetEquippedMainWeapon();
            bool hasReloadableWeaponEquipped = weapon != null && weapon.RequiresReload;
            bool isWeaponLoaded = !hasReloadableWeaponEquipped || weapon.IsLoaded;
            string reloadDisabledReason = "Unavailable";
            ReloadActionType reloadAction = ReloadActionType.None;
            bool canReload = hasReloadableWeaponEquipped && GameManager.Instance != null
                             && GameManager.Instance.CanReloadEquippedWeapon(pc, out reloadDisabledReason, out reloadAction);

            ReloadButton.gameObject.SetActive(hasReloadableWeaponEquipped);
            ReloadButton.interactable = canReload;

            Text reloadLabel = ReloadButton.GetComponentInChildren<Text>();
            if (reloadLabel != null)
            {
                string actionLabel = hasReloadableWeaponEquipped ? CharacterController.GetReloadActionLabel(pc.GetEffectiveReloadAction(weapon)) : "Move";
                if (isWeaponLoaded) reloadLabel.text = $"Reload ({actionLabel}) [Loaded]";
                else if (canReload) reloadLabel.text = $"Reload ({actionLabel})";
                else reloadLabel.text = $"Reload ({actionLabel}) [{reloadDisabledReason}]";
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


    public void ShowSpecialAttackMenu(CharacterController pc, System.Action<SpecialAttackType> onSelect, System.Action onCancel)
    {
        if (_specialAttackPanel == null)
            BuildSpecialAttackPanel();

        if (_specialAttackPanel == null) return;

        _specialAttackPanel.SetActive(true);

        var buttons = _specialAttackPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn.name == "Cancel") continue;
            bool enabled = pc != null && pc.Actions.HasStandardAction;

            if (pc != null)
            {
                if (btn.name == "Disarm" || btn.name == "Sunder")
                    enabled &= pc.HasMeleeWeaponEquipped();
                if (btn.name == "Grapple" || btn.name == "Bull Rush" || btn.name == "Overrun" || btn.name == "Trip")
                    enabled &= pc.HasMeleeWeaponEquipped();
            }

            btn.interactable = enabled;
        }

        WireSpecialAttackMenu(onSelect, onCancel);
    }

    public void HideSpecialAttackMenu()
    {
        if (_specialAttackPanel != null)
            _specialAttackPanel.SetActive(false);
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
        CreateSpecialButton("Grapple", "Grapple");
        CreateSpecialButton("Sunder", "Sunder");
        CreateSpecialButton("Bull Rush", "Bull Rush");
        CreateSpecialButton("Overrun", "Overrun");
        CreateSpecialButton("Feint", "Feint");
        CreateSpecialCancelButton();

        _specialAttackPanel.SetActive(false);
    }

    private void WireSpecialAttackMenu(System.Action<SpecialAttackType> onSelect, System.Action onCancel)
    {
        if (_specialAttackPanel == null) return;

        foreach (var btn in _specialAttackPanel.GetComponentsInChildren<Button>(true))
        {
            btn.onClick.RemoveAllListeners();
            switch (btn.name)
            {
                case "Trip": btn.onClick.AddListener(() => onSelect?.Invoke(SpecialAttackType.Trip)); break;
                case "Disarm": btn.onClick.AddListener(() => onSelect?.Invoke(SpecialAttackType.Disarm)); break;
                case "Grapple": btn.onClick.AddListener(() => onSelect?.Invoke(SpecialAttackType.Grapple)); break;
                case "Sunder": btn.onClick.AddListener(() => onSelect?.Invoke(SpecialAttackType.Sunder)); break;
                case "Bull Rush": btn.onClick.AddListener(() => onSelect?.Invoke(SpecialAttackType.BullRush)); break;
                case "Overrun": btn.onClick.AddListener(() => onSelect?.Invoke(SpecialAttackType.Overrun)); break;
                case "Feint": btn.onClick.AddListener(() => onSelect?.Invoke(SpecialAttackType.Feint)); break;
                case "Cancel": btn.onClick.AddListener(() => { HideSpecialAttackMenu(); onCancel?.Invoke(); }); break;
            }
        }
    }

    private void CreateSpecialButton(string name, string label)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_specialAttackPanel.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.28f, 1f);
        go.AddComponent<Button>();

        GameObject txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        var txt = txtGo.AddComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 12;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 12);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 24;
    }

    private void CreateSpecialCancelButton()
    {
        GameObject go = new GameObject("Cancel");
        go.transform.SetParent(_specialAttackPanel.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.35f, 0.16f, 0.16f, 1f);
        go.AddComponent<Button>();

        GameObject txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        var txt = txtGo.AddComponent<Text>();
        txt.text = "Cancel";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 12;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 12);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 24;
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

    /// <summary>
    /// Highlight which NPC is currently active (0-based index). Pass -1 to clear all.
    /// </summary>
    public void SetActiveNPC(int npcIndex)
    {
        for (int i = 0; i < NPCPanels.Count; i++)
        {
            var p = NPCPanels[i];
            bool isActive = (i == npcIndex);

            if (p.ActiveIndicator != null)
                p.ActiveIndicator.enabled = isActive;

            // Dim inactive NPC panels, brighten active
            SetPanelActiveState(p.Panel, isActive,
                new Color(0.4f, 0.15f, 0.15f, 0.95f),  // Brighter red when active
                new Color(0.3f, 0.1f, 0.1f, 0.65f));    // Dimmed when inactive
        }
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
        var cancelColors = cancelBtn.colors;
        cancelColors.highlightedColor = new Color(0.65f, 0.35f, 0.35f, 1f);
        cancelColors.pressedColor = new Color(0.4f, 0.1f, 0.1f, 1f);
        cancelBtn.colors = cancelColors;

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
        var colors = btn.colors;
        colors.highlightedColor = new Color(btnColor.r + 0.15f, btnColor.g + 0.15f, btnColor.b + 0.15f, 1f);
        colors.pressedColor = new Color(btnColor.r - 0.1f, btnColor.g - 0.1f, btnColor.b - 0.1f, 1f);
        btn.colors = colors;

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
        var spellColors = spellBtn.colors;
        spellColors.highlightedColor = new Color(btnColor.r + 0.15f, btnColor.g + 0.15f, btnColor.b + 0.15f, 1f);
        spellColors.pressedColor = new Color(btnColor.r - 0.1f, btnColor.g - 0.1f, btnColor.b - 0.1f, 1f);
        spellBtn.colors = spellColors;

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
        var convertColors = convertBtn.colors;
        convertColors.highlightedColor = new Color(convertColor.r + 0.15f, convertColor.g + 0.15f, convertColor.b + 0.15f, 1f);
        convertColors.pressedColor = new Color(convertColor.r - 0.1f, convertColor.g - 0.1f, convertColor.b - 0.1f, 1f);
        convertBtn.colors = convertColors;

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
            var colors = btn.colors;
            colors.highlightedColor = new Color(btnColor.r + 0.1f, btnColor.g + 0.1f, btnColor.b + 0.15f, 1f);
            colors.pressedColor = new Color(btnColor.r - 0.05f, btnColor.g - 0.05f, btnColor.b - 0.05f, 1f);
            btn.colors = colors;

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
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);

        Text txt = textObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.color = color;
        txt.alignment = align;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.text = textValue;

        return txt;
    }

    private Button CreateContextMenuButton(GameObject parent, string label, Color bgColor)
    {
        GameObject btnObj = new GameObject($"CtxBtn_{label}");
        btnObj.transform.SetParent(parent.transform, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredHeight = 30f;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(8f, 0f);
        txtRT.offsetMax = new Vector2(-8f, 0f);

        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        txt.fontSize = 13;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = new Color(0.94f, 0.97f, 1f, 1f);
        txt.text = label;

        return btn;
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


    private Button CreateTouchPromptButton(GameObject parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
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
        colors.highlightedColor = new Color(Mathf.Min(bgColor.r + 0.15f, 1f), Mathf.Min(bgColor.g + 0.15f, 1f), Mathf.Min(bgColor.b + 0.15f, 1f), 1f);
        colors.pressedColor = new Color(Mathf.Max(bgColor.r - 0.1f, 0f), Mathf.Max(bgColor.g - 0.1f, 0f), Mathf.Max(bgColor.b - 0.1f, 0f), 1f);
        btn.colors = colors;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(4, 2);
        txtRT.offsetMax = new Vector2(-4, -2);

        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        txt.fontSize = 13;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.text = label;

        return btn;
    }

    private Button CreateSpellDialogButton(GameObject parent, string name, string label,
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
    public Image ActiveIndicator;
}