using System.Collections.Generic;

/// <summary>
/// The caster's actions and spells per day captured before a cast spends them, so that cancelling the cast at the AoO
/// prompt gives them back (GameManager.CaptureSpellcastResourceSnapshot and RestoreSpellcastResourceSnapshot). It was
/// GameManager's private nested SpellcastResourceSnapshot until batch 11 step 4; it is a top-level internal type so a
/// <see cref="CastContext"/> can hold it.
/// </summary>
internal sealed class CastResourceSnapshot
{
    public CharacterController Caster;
    public bool MoveActionUsed;
    public bool StandardActionUsed;
    public bool FullRoundActionUsed;
    public bool SwiftActionUsed;
    public bool StandardConvertedToMove;
    public bool QuickenedSpellUsed;
    public List<bool> SlotUsedStates;
    // Spells-per-day counters spent before the AoO prompt (spontaneous casters,
    // the legacy per-level array and paladin/ranger partial casters), so a
    // cancelled cast refunds every kind of caster alike (SPL-138 review).
    public int[] SpontaneousSlotsRemaining;
    public int[] LegacySlotsRemaining;
    public int[] PartialSlotsRemaining;
}
