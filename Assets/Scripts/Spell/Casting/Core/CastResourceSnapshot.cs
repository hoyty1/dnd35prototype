using System.Collections.Generic;

/// <summary>
/// The caster's actions, spells per day and the cast's scroll or wand captured before a cast spends them, so that
/// cancelling the cast at the AoO prompt, or a spend that fails, gives them back (GameManager.CaptureSpellcastResourceSnapshot
/// and RestoreSpellcastResourceSnapshot). It was GameManager's private nested SpellcastResourceSnapshot until batch 11
/// step 4; it is a top-level internal type so a <see cref="CastContext"/> can hold it. Since batch 11 step 14 it records
/// the scroll's stack and slot and both of a wand's charge stores (ITM-084; ITM-034: ItemData.CurrentCharges and
/// WandData.CurrentCharges are both restored).
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

    // The cast's scroll or wand (a Scroll or Wand source), so a cancelled or unspent item cast keeps its item (ITM-084).
    /// <summary>The cast's source, whose item and pack slot the spend clears (GameManager.CleanupScrollCastState).</summary>
    public CastSource ItemSource;
    /// <summary>The scroll or wand; null when the cast uses none.</summary>
    public ItemData Item;
    /// <summary>The item's general pack slot (Inventory.GeneralSlots), where a used-up scroll is put back.</summary>
    public int ItemIndex = -1;
    /// <summary>The scroll's stack count before the spend.</summary>
    public int ItemStackCount;
    /// <summary>The wand's ItemData.CurrentCharges before the spend.</summary>
    public int ItemCharges;
    /// <summary>The wand's WandData.CurrentCharges before the spend; -1 without WandData.</summary>
    public int WandDataCharges = -1;
}
