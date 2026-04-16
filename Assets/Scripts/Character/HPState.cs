/// <summary>
/// D&D 3.5e HP state model.
/// </summary>
public enum HPState
{
    Healthy,   // 1+ HP
    Disabled,  // Exactly 0 HP
    Dying,     // -1 to -9 HP (loses 1 HP each turn until stabilized)
    Stable,    // -1 to -9 HP (no longer losing HP)
    Dead       // -10 HP or lower
}
