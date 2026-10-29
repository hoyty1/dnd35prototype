using System;
using System.Collections.Generic;
using UnityEngine;

// The maneuver core (batch 12 step 19, docs/designs/pc_npc_parity_batch12.md; CMB-102, CMB-098, CMB-103, D10, D15): one
// request, one cost table (ManeuverActionCost.For, Combat/Core/AttackPool.cs), one legality (GameManager.CanStartManeuver),
// one executor (GameManager.ExecuteManeuver, GameManager.ManeuverCore.cs) and one decider for the choices the rules leave
// to the creature. The PC wrapper (GameManager.ExecuteSpecialAttack) and the NPC executor (TryNPCSpecialAttackIfBeneficial)
// only build the request and run their own continuation.

/// <summary>
/// One maneuver attempt (batch 12 step 19): who, against whom, which maneuver, the item a disarm or sunder aims at (the
/// attacker's choice, PHB p.155, p.158, D15), the hand (a PC's off-hand disarm or sunder), the attack intent (the two-weapon
/// state whose main-hand penalty a maneuver step takes, PHB p.160) and the decider for the choices that follow.
/// </summary>
public sealed class ManeuverRequest
{
    public CharacterController Attacker;
    public CharacterController Target;
    public SpecialAttackType Type;

    /// <summary>The disarm or sunder target slot the attacker chose (D15); null for the other maneuvers.</summary>
    public EquipSlot? ItemSlot;

    /// <summary>A PC's off-hand disarm or sunder (the Off-Hand buttons).</summary>
    public bool UseOffHand;

    /// <summary>The attack intent: the maneuver, the weapon of the hand chosen, and the two-weapon state (PHB p.160).</summary>
    public AttackIntent Intent;

    /// <summary>The attacker's decider (UI for a PC on its own turn, AI on the NPC turn path).</summary>
    public IManeuverDecider Decider;

    /// <summary>The row of the cost table (ManeuverActionCost.For).</summary>
    public ManeuverCost Cost;

    /// <summary>Who builds the request: the combat log line differs (a PC's "SPECIAL [type] (cost)", an NPC's "uses SPECIAL").</summary>
    public ActorSource Source;

    /// <summary>Optional: run after the cost is paid, before the ward and the AoOs (the PC wrapper's attempt counters and label).</summary>
    public Action<ManeuverPayment> OnPaid;

    /// <summary>Optional: run right after the maneuver's own log line (the PC wrapper's remaining-attempt lines).</summary>
    public Action<SpecialAttackResult, ManeuverPayment> OnResolvedReport;

    /// <summary>
    /// The PC UI's request: <paramref name="intent"/> is the UI's (the dual-wield prompt's state and the hand chosen, read
    /// once by the wrapper); the decider is the attacker's own (<see cref="ManeuverDeciders.For"/>).
    /// </summary>
    public static ManeuverRequest ForUi(CharacterController attacker, CharacterController target, SpecialAttackType type,
        EquipSlot? itemSlot, bool useOffHand, AttackIntent intent)
    {
        return new ManeuverRequest
        {
            Attacker = attacker,
            Target = target,
            Type = type,
            ItemSlot = itemSlot,
            UseOffHand = useOffHand,
            Intent = intent ?? AttackIntent.ForManeuver(type),
            Decider = ManeuverDeciders.For(attacker),
            Cost = ManeuverActionCost.For(type, attacker),
            Source = ActorSource.Ui,
        };
    }

    /// <summary>
    /// The NPC executor's request: the AI decider (the NPC turn path is AI-run for every creature on it), the item chosen
    /// by it (D15: the attacker picks the item, the AI by its profile, AIProfile.ChooseManeuverItem), and the step's intent
    /// when the maneuver replaces a step of an attack sequence (its two-weapon state; null: none).
    /// </summary>
    public static ManeuverRequest ForAI(GameManager gm, CharacterController npc, CharacterController target, SpecialAttackType type,
        AttackIntent stepIntent)
    {
        AttackIntent intent = AttackIntent.ForManeuver(type);
        if (stepIntent != null)
            intent.TwoWeapon = stepIntent.TwoWeapon;
        var req = new ManeuverRequest
        {
            Attacker = npc,
            Target = target,
            Type = type,
            Intent = intent,
            Decider = ManeuverDeciders.For(npc, ActorSource.AI),
            Cost = ManeuverActionCost.For(type, npc),
            Source = ActorSource.AI,
        };
        if (type == SpecialAttackType.Disarm || type == SpecialAttackType.Sunder)
            req.Decider.ChooseItem(gm, npc, target, type, slot => req.ItemSlot = slot);
        return req;
    }
}

/// <summary>What the maneuver core paid: the BAB a step-substitute rolls at and the hand and weapon it used.</summary>
public sealed class ManeuverPayment
{
    /// <summary>The step's BAB for a maneuver that replaces a melee attack (the two-weapon main-hand penalty included); null otherwise.</summary>
    public int? StepBab;
    public bool UsedOffHand;
    public ItemData Weapon;
    /// <summary>The two-weapon penalty included in <see cref="StepBab"/>, for the log.</summary>
    public int DualWieldPenalty;
    /// <summary>The cost for the PC log ("standard action", "attack BAB +6 (1 remaining)").</summary>
    public string ActionLabel = "standard action";
    public int AttacksRemaining;
}

public enum ManeuverOutcomeKind
{
    /// <summary>Refused before any cost (legality or cost): nothing was spent.</summary>
    Refused,
    /// <summary>The cost was paid but a ward save failed (Sanctuary, Hide from Undead; D6): the maneuver was never made.</summary>
    WardFailed,
    /// <summary>The cost was paid but an initiation AoO foiled the attempt (PHB p.155-158, CMB-076).</summary>
    Foiled,
    /// <summary>The maneuver was resolved (<see cref="ManeuverOutcome.Result"/>).</summary>
    Resolved
}

/// <summary>The maneuver core's outcome, reported once through the executor's callback.</summary>
public sealed class ManeuverOutcome
{
    public ManeuverRequest Request;
    public ManeuverOutcomeKind Kind;
    public string Reason;
    public SpecialAttackResult Result;
    public ManeuverPayment Payment;

    /// <summary>The attacker ended the maneuver dead, dying or unconscious (an AoO in a bull rush push, a reaction).</summary>
    public bool AttackerDown;

    /// <summary>True once everything that follows the maneuver is settled (a counter-trip prompt may still be open before).</summary>
    public bool Settled;

    public bool Spent => Kind != ManeuverOutcomeKind.Refused;
    public bool Succeeded => Kind == ManeuverOutcomeKind.Resolved && Result != null && Result.Success;
}

/// <summary>
/// The choices a maneuver leaves to a creature (batch 12 step 19; CMB-098, D10, D15), PC and NPC alike: the UI decider asks
/// the player, the AI decider answers from the creature's AIProfile (AI stopgaps until the batch 13 catalog). Each method
/// answers through <c>answer</c> exactly once: at once (the AI, or a UI choice with one option) or when the player answers.
/// Only <see cref="ManeuverDeciders.For"/> reads IsControllable.
/// </summary>
public interface IManeuverDecider
{
    bool IsSynchronous { get; }

    /// <summary>The held item a disarm, or the item a sunder, aims at (PHB p.155, p.158; D15); null cancels (the UI menu's Cancel).</summary>
    void ChooseItem(GameManager gm, CharacterController attacker, CharacterController target, SpecialAttackType type, Action<EquipSlot?> answer);

    /// <summary>After a won bull rush (PHB p.154): stay and push 5 ft, or move with the defender (follow) up to <paramref name="maxIfFollowing"/> squares.</summary>
    void ChooseBullRushPush(GameManager gm, CharacterController attacker, CharacterController target, int maxIfFollowing, Action<bool, int> answer);

    /// <summary>Whether a defender whose trip attacker lost tries to trip it back (PHB p.158, CMB-079); asked of the defender's decider.</summary>
    void ChooseCounterTrip(GameManager gm, CharacterController defender, CharacterController tripper, Action<bool> answer);

    /// <summary>
    /// Where a grappler that won its move check takes the grapple (PHB p.156: up to half speed, bringing the others along;
    /// CMB-103): one of <paramref name="paths"/>' destinations, or null to stay.
    /// </summary>
    void ChooseGrappleMoveDestination(GameManager gm, CharacterController actor, IReadOnlyDictionary<Vector2Int, List<Vector2Int>> paths,
        IList<CharacterController> opponents, Action<Vector2Int?> answer);

    /// <summary>
    /// The space an escaper moves into (PHB p.156-157: it finishes the escape by moving into any space adjacent to its former
    /// opponents; D10): one of <paramref name="squares"/>. The move is not optional: a null answer takes the default stopgap
    /// square (GameManager.OfferFreeAdjacentMovement), and the UI selection cannot be cancelled while a square is free.
    /// </summary>
    void ChooseEscapeSquare(GameManager gm, CharacterController escaper, IList<Vector2Int> squares, CharacterController formerOpponent,
        Action<Vector2Int?> answer);

    /// <summary>
    /// Whether a grappler opposes an escape attempt (PHB p.156: opponents need not try to hold an escaping creature; D10).
    /// Not opposing counts as losing against that grappler. Asked of the grappler's decider; the AI always opposes.
    /// </summary>
    void ChooseOpposeEscape(GameManager gm, CharacterController grappler, CharacterController escaper, Action<bool> answer);

    /// <summary>
    /// Whether a defender blocks an overrun or avoids it (PHB p.157 step 2; batch 12 step 20, D11): true blocks (the opposed
    /// check), false lets the attacker by. Asked of the defender's decider by GameManager.RunOverrunMove, never when the
    /// attacker has Improved Overrun (PHB p.96: the target may not avoid). The AI always blocks (a stopgap).
    /// </summary>
    void ChooseOverrunResponse(GameManager gm, CharacterController defender, CharacterController attacker, Action<bool> answer);
}

/// <summary>The player's maneuver choices, through the prompts (GameManager.PromptManeuverItem, PromptBullRushPush, PromptCounterTrip, BeginGrappleMoveSelection, BeginFreeAdjacentGrappleMoveSelection, PromptGrappleEscapeOpposition, PromptOverrunResponse).</summary>
public sealed class UiManeuverDecider : IManeuverDecider
{
    public static readonly UiManeuverDecider Instance = new UiManeuverDecider();

    private UiManeuverDecider() { }

    public bool IsSynchronous => false;

    public void ChooseItem(GameManager gm, CharacterController attacker, CharacterController target, SpecialAttackType type, Action<EquipSlot?> answer)
    {
        if (gm == null || !gm.PromptManeuverItem(attacker, target, type, answer))
            AiManeuverDecider.Instance.ChooseItem(gm, attacker, target, type, answer);
    }

    public void ChooseBullRushPush(GameManager gm, CharacterController attacker, CharacterController target, int maxIfFollowing, Action<bool, int> answer)
    {
        if (gm == null || !gm.PromptBullRushPush(attacker, target, maxIfFollowing, answer))
            AiManeuverDecider.Instance.ChooseBullRushPush(gm, attacker, target, maxIfFollowing, answer);
    }

    public void ChooseCounterTrip(GameManager gm, CharacterController defender, CharacterController tripper, Action<bool> answer)
    {
        if (gm == null || !gm.PromptCounterTrip(defender, tripper, answer))
            AiManeuverDecider.Instance.ChooseCounterTrip(gm, defender, tripper, answer);
    }

    public void ChooseGrappleMoveDestination(GameManager gm, CharacterController actor, IReadOnlyDictionary<Vector2Int, List<Vector2Int>> paths,
        IList<CharacterController> opponents, Action<Vector2Int?> answer)
    {
        // The selection opens only on the creature's own PC turn; otherwise it cannot click now and the AI answers.
        if (gm == null || !gm.PromptGrappleMoveDestination(actor, paths, opponents, answer))
            AiManeuverDecider.Instance.ChooseGrappleMoveDestination(gm, actor, paths, opponents, answer);
    }

    public void ChooseEscapeSquare(GameManager gm, CharacterController escaper, IList<Vector2Int> squares, CharacterController formerOpponent,
        Action<Vector2Int?> answer)
    {
        if (gm == null || !gm.PromptGrappleEscapeSquare(escaper, squares, formerOpponent, answer))
            AiManeuverDecider.Instance.ChooseEscapeSquare(gm, escaper, squares, formerOpponent, answer);
    }

    public void ChooseOpposeEscape(GameManager gm, CharacterController grappler, CharacterController escaper, Action<bool> answer)
    {
        if (gm == null || !gm.PromptGrappleEscapeOpposition(grappler, escaper, answer))
            AiManeuverDecider.Instance.ChooseOpposeEscape(gm, grappler, escaper, answer);
    }

    public void ChooseOverrunResponse(GameManager gm, CharacterController defender, CharacterController attacker, Action<bool> answer)
    {
        if (gm == null || !gm.PromptOverrunResponse(defender, attacker, answer))
            AiManeuverDecider.Instance.ChooseOverrunResponse(gm, defender, attacker, answer);
    }
}

/// <summary>
/// The AI's maneuver choices, answered at once from the creature's AIProfile (or the profile defaults without one). AI
/// stopgaps (batch 13 catalog): the item by AIProfile.ChooseManeuverItem, the push by ChooseBullRushPush, the counter-trip
/// by ShouldCounterTrip, the grapple move and the escape square by ChooseGrappleMoveDestination and ChooseGrappleEscapeSquare,
/// opposing an escape by ShouldOpposeGrappleEscape (always, D10), and blocking an overrun by ShouldBlockOverrun (always, D11).
/// </summary>
public sealed class AiManeuverDecider : IManeuverDecider
{
    public static readonly AiManeuverDecider Instance = new AiManeuverDecider();

    private AiManeuverDecider() { }

    public bool IsSynchronous => true;

    public void ChooseItem(GameManager gm, CharacterController attacker, CharacterController target, SpecialAttackType type, Action<EquipSlot?> answer)
    {
        DND35.AI.AIProfile profile = attacker != null ? attacker.aiProfile : null;
        EquipSlot? slot = profile != null
            ? profile.ChooseManeuverItem(attacker, target, type)
            : DND35.AI.AIProfile.DefaultChooseManeuverItem(attacker, target, type);
        answer?.Invoke(slot);
    }

    public void ChooseBullRushPush(GameManager gm, CharacterController attacker, CharacterController target, int maxIfFollowing, Action<bool, int> answer)
    {
        bool follow = true;
        int squares = gm != null ? gm.ChooseBullRushPushForAIDecider(attacker, target, maxIfFollowing, out follow) : Mathf.Max(1, maxIfFollowing);
        answer?.Invoke(follow, squares);
    }

    public void ChooseCounterTrip(GameManager gm, CharacterController defender, CharacterController tripper, Action<bool> answer)
    {
        DND35.AI.AIProfile profile = defender != null ? defender.aiProfile : null;
        answer?.Invoke(gm != null
            ? gm.ShouldCounterTripForAIDecider(defender, tripper)
            : profile != null ? profile.ShouldCounterTrip(defender, tripper) : DND35.AI.AIProfile.DefaultShouldCounterTrip(defender, tripper));
    }

    public void ChooseGrappleMoveDestination(GameManager gm, CharacterController actor, IReadOnlyDictionary<Vector2Int, List<Vector2Int>> paths,
        IList<CharacterController> opponents, Action<Vector2Int?> answer)
    {
        DND35.AI.AIProfile profile = actor != null ? actor.aiProfile : null;
        IList<CharacterController> all = gm != null ? (IList<CharacterController>)gm.GetAllCharactersForAI() : new List<CharacterController>();
        Vector2Int? pick = profile != null
            ? profile.ChooseGrappleMoveDestination(actor, paths, opponents, all)
            : DND35.AI.AIProfile.DefaultChooseGrappleMoveDestination(actor, paths, opponents, all);
        answer?.Invoke(pick);
    }

    public void ChooseEscapeSquare(GameManager gm, CharacterController escaper, IList<Vector2Int> squares, CharacterController formerOpponent,
        Action<Vector2Int?> answer)
    {
        DND35.AI.AIProfile profile = escaper != null ? escaper.aiProfile : null;
        IList<CharacterController> all = gm != null ? (IList<CharacterController>)gm.GetAllCharactersForAI() : new List<CharacterController>();
        Vector2Int? pick = profile != null
            ? profile.ChooseGrappleEscapeSquare(escaper, squares, formerOpponent, all)
            : DND35.AI.AIProfile.DefaultChooseGrappleEscapeSquare(escaper, squares, formerOpponent, all);
        answer?.Invoke(pick);
    }

    public void ChooseOpposeEscape(GameManager gm, CharacterController grappler, CharacterController escaper, Action<bool> answer)
    {
        DND35.AI.AIProfile profile = grappler != null ? grappler.aiProfile : null;
        answer?.Invoke(profile == null || profile.ShouldOpposeGrappleEscape(grappler, escaper));
    }

    public void ChooseOverrunResponse(GameManager gm, CharacterController defender, CharacterController attacker, Action<bool> answer)
    {
        DND35.AI.AIProfile profile = defender != null ? defender.aiProfile : null;
        answer?.Invoke(profile == null || profile.ShouldBlockOverrun(defender, attacker));
    }
}

/// <summary>The decider of a creature's maneuver choices (the one IsControllable read of the maneuver paths).</summary>
public static class ManeuverDeciders
{
    /// <summary>
    /// The UI decider for a controllable creature, else the AI's. <paramref name="overrideSource"/> fixes the source: the NPC
    /// executor passes <see cref="ActorSource.AI"/> (the NPC turn path is AI-run for every creature on it), and batch 13
    /// passes a forced source.
    /// </summary>
    public static IManeuverDecider For(CharacterController actor, ActorSource? overrideSource = null)
    {
        ActorSource source = overrideSource ?? (actor != null && actor.IsControllable ? ActorSource.Ui : ActorSource.AI);
        return source == ActorSource.Ui ? (IManeuverDecider)UiManeuverDecider.Instance : AiManeuverDecider.Instance;
    }
}
