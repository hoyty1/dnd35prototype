using UnityEngine;

public enum HPCalculationMode
{
    Roll,
    Average,
    Maximum
}

/// <summary>
/// Global game settings singleton.
///
/// HP mode note:
/// - This is a stub for future difficulty/settings UI (HP mode, AI difficulty).
/// - No player-facing settings menu is wired yet.
/// - Default remains Roll (D&D 3.5e standard).
/// </summary>
public class GameSettings : MonoBehaviour
{
    private static GameSettings _instance;

    public static GameSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameSettings>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameSettings");
                    _instance = go.AddComponent<GameSettings>();
                }
            }

            return _instance;
        }
    }

    [Header("Difficulty Stub")]
    public HPCalculationMode hpCalculationMode = HPCalculationMode.Roll;

    /// <summary>
    /// The AI difficulty (batch 14 step 4, C11, Q6 default; AI-012): Easy, Normal (default) or Hard. AIService.Difficulty
    /// reads it live unless the scenario harness has set its override. No menu sets it yet; a future menu writes this field. Easy skips only the personality opinion rows
    /// marked MinDifficulty Normal (today only Sadistic's coup de grace Insist); Hard plays as Normal in batch 14.
    /// </summary>
    public AIService.AIDifficultyLevel aiDifficulty = AIService.AIDifficultyLevel.Normal;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
