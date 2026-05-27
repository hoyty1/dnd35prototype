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
/// - This is a stub for future difficulty/settings UI.
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
