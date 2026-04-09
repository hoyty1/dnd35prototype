using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all combat UI elements with D&D 3.5 ability score display.
/// Shows 6 ability scores with modifiers and derived stats for each character.
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

    [Header("Action Buttons")]
    public GameObject ActionPanel;
    public Button AttackButton;
    public Button EndTurnButton;

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
            nameText.text = $"{s.CharacterName} (Lv {s.Level})";
            if (s.IsDead) nameText.text += " (DEAD)";
        }

        if (hpText != null)
            hpText.text = $"HP: {s.CurrentHP}/{s.MaxHP}";

        if (acText != null)
            acText.text = $"AC: {s.ArmorClass} (10{FormatBonusDetail(s.DEXMod, "DEX")}{FormatBonusDetail(s.ArmorBonus, "Armor")}{FormatBonusDetail(s.ShieldBonus, "Shield")})";

        if (atkText != null)
            atkText.text = $"Atk: {CharacterStats.FormatMod(s.AttackBonus)} (BAB {CharacterStats.FormatMod(s.BaseAttackBonus)} {CharacterStats.FormatMod(s.STRMod)} STR)";

        if (speedText != null)
            speedText.text = $"Speed: {s.MoveRange} hexes";

        if (abilityText != null)
        {
            abilityText.text =
                $"STR {s.STR}({CharacterStats.FormatMod(s.STRMod)}) " +
                $"DEX {s.DEX}({CharacterStats.FormatMod(s.DEXMod)}) " +
                $"CON {s.CON}({CharacterStats.FormatMod(s.CONMod)})\n" +
                $"WIS {s.WIS}({CharacterStats.FormatMod(s.WISMod)}) " +
                $"INT {s.INT}({CharacterStats.FormatMod(s.INTMod)}) " +
                $"CHA {s.CHA}({CharacterStats.FormatMod(s.CHAMod)})";
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
            CombatLogText.text = message;
    }

    public void SetActionButtonsVisible(bool visible)
    {
        if (ActionPanel != null)
            ActionPanel.SetActive(visible);
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
}
