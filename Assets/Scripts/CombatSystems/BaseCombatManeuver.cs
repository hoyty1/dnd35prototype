using UnityEngine;

public abstract class BaseCombatManeuver : MonoBehaviour, ICombatSystem
{
    protected GameManager gm;
    protected CombatUI combatUI;

    public virtual void Initialize(GameManager gameManager)
    {
        gm = gameManager;
        combatUI = gameManager != null ? gameManager.CombatUI : null;
    }

    public virtual void Cleanup() { }
}
