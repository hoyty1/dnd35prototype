using UnityEngine;

/// <summary>
/// Debug-only keyboard helpers.
/// Ctrl+H cycles HP calculation mode: Roll -> Average -> Maximum.
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
#endif
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
