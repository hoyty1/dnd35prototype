using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all combat UI elements: stats panels, combat log, action buttons, turn indicator.
/// </summary>
public class CombatUI : MonoBehaviour
{
    [Header("Turn Indicator")]
    public Text TurnIndicatorText;

    [Header("PC Stats")]
    public Text PCNameText;
    public Text PCHPText;
    public Text PCACText;
    public Image PCHPBar;

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

    public void UpdateStats(CharacterController pc, CharacterController npc)
    {
        if (pc != null && pc.Stats != null)
        {
            if (PCNameText != null) PCNameText.text = pc.Stats.CharacterName;
            if (PCHPText != null) PCHPText.text = $"HP: {pc.Stats.CurrentHP}/{pc.Stats.MaxHP}";
            if (PCACText != null) PCACText.text = $"AC: {pc.Stats.ArmorClass}";
            if (PCHPBar != null) PCHPBar.fillAmount = (float)pc.Stats.CurrentHP / pc.Stats.MaxHP;
        }

        if (npc != null && npc.Stats != null)
        {
            if (NPCNameText != null) NPCNameText.text = npc.Stats.CharacterName;
            if (NPCHPText != null) NPCHPText.text = $"HP: {npc.Stats.CurrentHP}/{npc.Stats.MaxHP}";
            if (NPCACText != null) NPCACText.text = $"AC: {npc.Stats.ArmorClass}";
            if (NPCHPBar != null) NPCHPBar.fillAmount = (float)npc.Stats.CurrentHP / npc.Stats.MaxHP;
        }
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
}
