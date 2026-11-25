using UnityEngine;

/// <summary>
/// Debug-only keyboard helpers.
/// Ctrl+H cycles HP calculation mode: Roll -> Average -> Maximum.
/// Ctrl+J cycles the AI turn speed (GameSettings.aiTurnSpeed: 1, 1.5, 2, 3, 4x), as the combat HUD's "AI speed" button
/// does (grouped AI turns step 6).
/// Ctrl+G turns Group AI turns on or off (GameSettings.groupAiTurns), as the combat HUD's "Group AI turns" toggle does
/// (grouped AI turns step 11); it applies from the next group.
/// </summary>
public class DebugCommands : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureDebugCommandsExists()
    {
        DebugCommands existing = FindObjectOfType<DebugCommands>();
        if (existing != null)
            return;

        GameObject go = new GameObject("DebugCommands");
        go.AddComponent<DebugCommands>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.H))
        {
            CycleHPMode();
        }
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.J))
        {
            CycleAiTurnSpeed();
        }
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.G))
        {
            ToggleGroupAiTurns();
        }
#endif
    }

    /// <summary>Steps the AI turn speed to its next choice and refreshes the HUD button's label.</summary>
    public static void CycleAiTurnSpeed()
    {
        GameSettings settings = GameSettings.Instance;
        if (settings == null)
            return;

        float speed = settings.CycleAiTurnSpeed();
        CombatUI ui = GameManager.Instance != null ? GameManager.Instance.CombatUI : null;
        if (ui != null)
            ui.RefreshAiSpeedButton();
        Debug.Log("[Debug] AI turn speed: " + GameSettings.AiTurnSpeedLabel(speed));
    }

    /// <summary>
    /// Turns Group AI turns on or off and refreshes the HUD toggle's label. The setting is read when a group opens
    /// (TurnControl.AiTurnStarting), so a group being recorded or played back is not cut short; the next one follows it.
    /// </summary>
    public static void ToggleGroupAiTurns()
    {
        GameSettings settings = GameSettings.Instance;
        if (settings == null)
            return;

        bool on = settings.ToggleGroupAiTurns();
        CombatUI ui = GameManager.Instance != null ? GameManager.Instance.CombatUI : null;
        if (ui != null)
            ui.RefreshGroupAiTurnsButton();
        Debug.Log("[Debug] Group AI turns: " + (on ? "on" : "off"));
    }

    private static void CycleHPMode()
    {
        GameSettings settings = GameSettings.Instance;
        if (settings == null)
            return;

        switch (settings.hpCalculationMode)
        {
            case HPCalculationMode.Roll:
                settings.hpCalculationMode = HPCalculationMode.Average;
                Debug.Log("[Debug] HP Mode: AVERAGE");
                break;

            case HPCalculationMode.Average:
                settings.hpCalculationMode = HPCalculationMode.Maximum;
                Debug.Log("[Debug] HP Mode: MAXIMUM");
                break;

            case HPCalculationMode.Maximum:
            default:
                settings.hpCalculationMode = HPCalculationMode.Roll;
                Debug.Log("[Debug] HP Mode: ROLL");
                break;
        }
    }
}
