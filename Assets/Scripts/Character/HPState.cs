/// <summary>
/// D&D 3.5e HP state model.
/// </summary>
public enum HPState
{
    Healthy,      // 1+ HP and nonlethal below current HP
    Disabled,     // Exactly 0 HP
    Staggered,    // Nonlethal damage equals current HP (can take one move OR one standard action)
    Unconscious,  // Nonlethal damage exceeds current HP (but not dying from lethal damage)
    Dying,        // -1 to -9 HP (loses 1 HP each turn until stabilized)
    Stable,       // -1 to -9 HP (no longer losing HP)
    Dead          // -10 HP or lower
}
