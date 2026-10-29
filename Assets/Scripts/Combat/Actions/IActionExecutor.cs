using System.Collections;

/// <summary>
/// The one executor of a catalog action, PC and NPC alike (batch 13 step 3). Rules for every executor
/// (ActionCatalogTests scans the files under Combat/Actions/Executors):
/// - <see cref="CanStart"/> and <see cref="CanExecute"/> are pure: no dice, no log, no state change;
/// - <see cref="Execute"/> pays through ActionCosts.TryPay or through the core it wraps, and fills the outcome;
/// - it never continues the UI (ShowActionChoices, AfterAttackDelay, EndActivePCTurn, DelayedEndActivePCTurn): the PC
///   runner does, from the entry's UiAfter;
/// - it never reads IsControllable, ActivePC or IsPlayerTurn: whose choices they are is the context's Source.
/// </summary>
public interface IActionExecutor
{
    /// <summary>May <paramref name="actor"/> take this action at all now (its action, its conditions, its resources), with no target yet?</summary>
    Legality CanStart(CharacterController actor);

    /// <summary>May the action go ahead as <paramref name="ctx"/> asks? A Delegating executor's core may still refuse in Execute.</summary>
    Legality CanExecute(ActionContext ctx);

    /// <summary>Takes the action; a coroutine the runner yields as a nested enumerator.</summary>
    IEnumerator Execute(ActionContext ctx, ActionOutcome outcome);
}
