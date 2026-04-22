using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated initiative display presenter for CombatUI.
/// Owns initiative text rendering and panel visibility.
/// </summary>
public class InitiativePanel : MonoBehaviour
{
    private CombatUI _combatUI;

    public void Initialize(CombatUI combatUI)
    {
        _combatUI = combatUI;
    }

    public void UpdateInitiativeDisplay(string initiativeText)
    {
        if (_combatUI == null)
            return;

        if (_combatUI.InitiativeOrderText != null)
        {
            _combatUI.InitiativeOrderText.supportRichText = true;
            _combatUI.InitiativeOrderText.text = initiativeText;
        }

        if (_combatUI.InitiativePanel != null)
            _combatUI.InitiativePanel.SetActive(!string.IsNullOrEmpty(initiativeText));
    }
}
