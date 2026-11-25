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
/// - The AI turn speed (aiTurnSpeed) has a combat HUD button (grouped AI turns step 6).
/// - Group AI turns (groupAiTurns, step 9) is off by default and has no HUD toggle yet (step 11).
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

    /// <summary>
    /// The AI turn speed (grouped AI turns step 6, owner request 2026-10-30, Q4 default; design choice C2): every pacing
    /// wait and movement step of an AI-decided turn (NPCs, AI-run allies and summons, forced PC turns) plays this many
    /// times faster; player-run turns stay at 1x. One of <see cref="AiTurnSpeedChoices"/>, default 2x, session-only (the
    /// game has no saves). Set by the combat HUD's "AI speed" button and Ctrl+J (DebugCommands); the scenario harness
    /// sets it from its aiSpeed= option (default 1) and restores it. Read through <see cref="CurrentAiTurnSpeed"/> by
    /// CombatPacing.Speed. Presentation only: no rule reads it.
    /// </summary>
    public float aiTurnSpeed = DefaultAiTurnSpeed;

    /// <summary>The AI turn speed in play (Q4 default): 2x.</summary>
    public const float DefaultAiTurnSpeed = 2f;

    /// <summary>The AI turn speeds the HUD button cycles through (Q4 default): 1, 1.5, 2, 3 and 4x.</summary>
    public static readonly float[] AiTurnSpeedChoices = { 1f, 1.5f, 2f, 3f, 4f };

    /// <summary>True when <paramref name="speed"/> is one of <see cref="AiTurnSpeedChoices"/>.</summary>
    public static bool IsAiTurnSpeedChoice(float speed)
    {
        foreach (float c in AiTurnSpeedChoices)
            if (c == speed)
                return true;
        return false;
    }

    /// <summary>The choice after <paramref name="speed"/>, wrapping from 4x to 1x; a value off the list goes to the default.</summary>
    public static float NextAiTurnSpeed(float speed)
    {
        for (int i = 0; i < AiTurnSpeedChoices.Length; i++)
            if (AiTurnSpeedChoices[i] == speed)
                return AiTurnSpeedChoices[(i + 1) % AiTurnSpeedChoices.Length];
        return DefaultAiTurnSpeed;
    }

    /// <summary>
    /// Group AI turns (grouped AI turns step 9, owner request 2026-10-30; docs/designs/grouped_ai_turns.md "The group"):
    /// the AI-decided turns before the next player-run turn resolve at once and play back together, the rules unchanged
    /// (CombatPresentation's Holding mode). Read once per group, when TurnControl.AiTurnStarting opens one. Off by default
    /// until step 11 (Q1's default then turns it on in play with a HUD toggle); the scenario harness sets it from its
    /// group= option (default off) and restores it. Session-only. Presentation only: no rule reads it.
    /// </summary>
    public bool groupAiTurns = DefaultGroupAiTurns;

    /// <summary>The default of <see cref="groupAiTurns"/>: off (step 9; Q1 moves it to on in step 11).</summary>
    public const bool DefaultGroupAiTurns = false;

    /// <summary>
    /// Whether grouping is on: the live settings object's <see cref="groupAiTurns"/>, or the default when no settings
    /// object exists yet (this never creates one).
    /// </summary>
    public static bool CurrentGroupAiTurns
    {
        get
        {
            GameSettings s = _instance;
            return s != null ? s.groupAiTurns : DefaultGroupAiTurns;
        }
    }

    /// <summary>
    /// The AI turn speed in force: the live settings object's <see cref="aiTurnSpeed"/>, or the default when no settings
    /// object exists yet (edit mode included; this never creates one), and the default for a value off the choice list.
    /// </summary>
    public static float CurrentAiTurnSpeed
    {
        get
        {
            GameSettings s = _instance;
            if (s == null)
                return DefaultAiTurnSpeed;
            return IsAiTurnSpeedChoice(s.aiTurnSpeed) ? s.aiTurnSpeed : DefaultAiTurnSpeed;
        }
    }

    /// <summary>Steps <see cref="aiTurnSpeed"/> to the next choice and returns it (the HUD button and Ctrl+J).</summary>
    public float CycleAiTurnSpeed()
    {
        aiTurnSpeed = NextAiTurnSpeed(aiTurnSpeed);
        return aiTurnSpeed;
    }

    /// <summary>The HUD label of a speed: "AI Normal" at 1x (the player turns' own pace; owner, 2026-11-24), else "AI 1.5x", "AI 2x".</summary>
    public static string AiTurnSpeedLabel(float speed)
        => speed == 1f ? "AI Normal" : "AI " + speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";

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
