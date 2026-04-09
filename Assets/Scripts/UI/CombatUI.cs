using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all combat UI elements: stats panels for PC1, PC2, NPC,
/// combat log, action buttons, turn indicator, and active-PC highlighting.
/// </summary>
public class CombatUI : MonoBehaviour
{
    [Header("Turn Indicator")]
    public Text TurnIndicatorText;

    [Header("PC1 Stats")]
    public Text PC1NameText;
    public Text PC1HPText;
    public Text PC1ACText;
    public Image PC1HPBar;
    public GameObject PC1Panel;

    [Header("PC2 Stats")]
    public Text PC2NameText;
    public Text PC2HPText;
    public Text PC2ACText;
    public Image PC2HPBar;
    public GameObject PC2Panel;

    [Header("NPC Stats")]
    public Text NPCNameText;
    public Text NPCHPText;
    public Text NPCACText;
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
        UpdatePCStats(pc1, PC1NameText, PC1HPText, PC1ACText, PC1HPBar);
        UpdatePCStats(pc2, PC2NameText, PC2HPText, PC2ACText, PC2HPBar);

        if (npc != null && npc.Stats != null)
        {
            if (NPCNameText != null) NPCNameText.text = npc.Stats.CharacterName;
            if (NPCHPText != null) NPCHPText.text = $"HP: {npc.Stats.CurrentHP}/{npc.Stats.MaxHP}";
            if (NPCACText != null) NPCACText.text = $"AC: {npc.Stats.ArmorClass}";
            if (NPCHPBar != null) NPCHPBar.fillAmount = (float)npc.Stats.CurrentHP / npc.Stats.MaxHP;
        }
    }

    private void UpdatePCStats(CharacterController pc, Text nameText, Text hpText, Text acText, Image hpBar)
    {
        if (pc != null && pc.Stats != null)
        {
            if (nameText != null)
            {
                nameText.text = pc.Stats.CharacterName;
                if (pc.Stats.IsDead) nameText.text += " (DEAD)";
            }
            if (hpText != null) hpText.text = $"HP: {pc.Stats.CurrentHP}/{pc.Stats.MaxHP}";
            if (acText != null) acText.text = $"AC: {pc.Stats.ArmorClass}";
            if (hpBar != null) hpBar.fillAmount = (float)pc.Stats.CurrentHP / pc.Stats.MaxHP;
        }
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
