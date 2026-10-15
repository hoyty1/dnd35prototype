/// <summary>
/// The only difference between a PC cast and an NPC cast (batch 11, cast_core_batch11.md; PC_NPC_PARITY): who makes the
/// caster's choices. The UI decider asks the player through the dialogs and the AoO prompt and can suspend the cast;
/// the AI decider answers inline. For now the interface carries only <see cref="IsSynchronous"/>; the choice methods
/// (FillChoices, ChooseProvocation, ChooseTouchDelivery, ForgoSave, ChooseMirrorImageSwap) arrive with the cast core.
/// </summary>
public interface ICastDecider
{
    /// <summary>True when every choice is answered inline (the AI), false when the cast may wait for the player.</summary>
    bool IsSynchronous { get; }
}
