using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Persistent Grease area (PHB p.237) on the reusable area-effect framework, by decision D12 of
/// docs/designs/pc_npc_parity_batch12.md (delegated RAW reading 2026-10-23, docs/systems/RULES_COVERAGE.md 4.1):
/// <list type="bullet">
/// <item>A creature in the area when the spell is cast makes a Reflex save or falls (the initial effect), and repeats
/// the save on each of the caster's later turns while it stays in the area (<see cref="OnCasterTurnStart"/>, run from
/// GameManager.BeginCombatantTurn). "Your turn" is read as the caster's, because spell text addresses the caster. The
/// repeat is only for a creature that was in the area at casting and has not left it: PHB p.237's "each round that the
/// creature remains within the area" refers back to "any creature in the area when the spell is cast"; a later entrant
/// takes the Balance check of its movement instead.</item>
/// <item>A creature moving within or into the area makes one DC 10 Balance check a round, on its first step of greased
/// movement (<see cref="CheckMovement"/>, from the Grease movement step hook, GreaseStepHook, for every mover). Success
/// lets it move there at half speed that round (the greased squares are difficult terrain: each costs double,
/// SquareGridUtils.StepCost). Failure means it cannot move that round and makes the Reflex save or falls; failure by 5 or
/// more means it falls (PHB p.237, p.67).</item>
/// <item>There is no Reflex save merely for entering the area, and no check for standing in it. No 5-foot step
/// leaves or enters it (difficult terrain, PHB p.144, p.148; MovementService.IsValidAdjacentStepDestination).</item>
/// </list>
/// Not applied yet (GRID-026): a creature balancing in grease is flat-footed without 5 ranks of Balance, and damage
/// while balancing calls for another check (PHB p.67).
/// </summary>
public class GreaseAreaEffect : PersistentAreaEffect
{
    private const int BalanceDC = 10;

    /// <summary>The d20 context of the Balance check to move in grease (DiceService; a scenario can force it).</summary>
    public const string BalanceDiceContext = "Grease Balance check";

    /// <summary>The dice context of Grease's Reflex save against falling (SaveRollRules; a scenario can force it).</summary>
    public const string ReflexDiceContext = "Grease Reflex";

    private sealed class BalanceState
    {
        public int Round;
        public bool Failed;
        public int StopLoggedRound = -1;
    }

    /// <summary>Each mover's Balance check this round (once per round, D12). The area is destroyed with the encounter.</summary>
    private readonly Dictionary<CharacterController, BalanceState> _balanceByCreature = new Dictionary<CharacterController, BalanceState>();

    /// <summary>The creatures in the area when the spell was cast that have not left it: the repeated save's (D12).</summary>
    private readonly HashSet<CharacterController> _inAreaSinceCasting = new HashSet<CharacterController>();

    protected override Color GridHighlightColor => AreaEffectColors.Grease;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Grease";
        SpellId = SpellNames.GREASE;
        Shape = AreaShape.Square;
        SizeX = 2; // 10-ft square on 5-ft grid
        SizeY = 2;
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, true);
        LogEffect($"10-ft square becomes slippery: moving in it takes a DC {BalanceDC} Balance check each round (half speed); Reflex DC {SaveDC} or fall for creatures in it now and on each of the caster's turns.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // Entering is movement: the Balance check of the movement step hook covers it (D12). Only a creature in the area
        // when the spell is cast saves now.
        if (!isInitial)
            return;

        _inAreaSinceCasting.Add(character);
        RollFallSave(character, "is in");
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        // Leaving ends the repeated save: a creature that comes back is a mover (the Balance check), not one in the area
        // at casting (D12).
        if (character != null)
            _inAreaSinceCasting.Remove(character);
    }

    /// <summary>
    /// The repeated Reflex save on the caster's turn (PHB p.237, D12) for every creature that was in the area at casting
    /// and is still in it. A creature already prone has nothing left to fall from and rolls nothing.
    /// </summary>
    public void OnCasterTurnStart()
    {
        if (AffectedCells == null || AffectedCells.Count == 0 || RoundsRemaining <= 0)
            return;

        var since = new List<CharacterController>(_inAreaSinceCasting);
        for (int i = 0; i < since.Count; i++)
        {
            CharacterController character = since[i];
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;
            if (!IsCharacterInArea(character))
            {
                _inAreaSinceCasting.Remove(character);
                continue;
            }
            if (character.HasCondition(CombatConditionType.Prone))
                continue;

            RollFallSave(character, "is still in");
        }
    }

    /// <summary>Whether <paramref name="character"/> failed its Balance check in this area in <paramref name="round"/>.</summary>
    public bool FailedBalanceThisRound(CharacterController character, int round)
        => character != null && _balanceByCreature.TryGetValue(character, out BalanceState st) && st.Round == round && st.Failed;

    /// <summary>Whether <paramref name="character"/> failed its Balance check this round in any grease (it cannot move).</summary>
    public static bool FailedBalanceInAnyGreaseThisRound(CharacterController character)
    {
        if (character == null || !AreaEffectManager.HasInstance)
            return false;
        int round = GameManager.Instance != null ? GameManager.Instance.CurrentRound : 0;
        List<GreaseAreaEffect> greases = AreaEffectManager.Instance.GetEffectsOfType<GreaseAreaEffect>();
        for (int i = 0; i < greases.Count; i++)
            if (greases[i] != null && greases[i].FailedBalanceThisRound(character, round))
                return true;
        return false;
    }

    /// <summary>Logs once per round that a creature that failed its Balance check cannot move this round.</summary>
    public void LogCannotMove(CharacterController character, int round)
    {
        if (character == null || character.Stats == null || !_balanceByCreature.TryGetValue(character, out BalanceState st) || st.StopLoggedRound == round)
            return;
        st.StopLoggedRound = round;
        LogEffect($"{character.Stats.CharacterName} cannot move this round (failed Balance check in the grease).");
    }

    /// <summary>
    /// The Balance check of a creature about to move within or into the area (D12): once per round, on its first step of
    /// greased movement. A failure refuses the step (<paramref name="stop"/>), then the Reflex save or a fall.
    /// </summary>
    public void CheckMovement(CharacterController character, MoveKind kind, int round, ref bool stop)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        if (_balanceByCreature.TryGetValue(character, out BalanceState state) && state.Round == round)
        {
            if (state.Failed)
            {
                LogCannotMove(character, round);
                stop = true;
            }
            return;
        }

        int total = character.Stats.RollSkillCheck("Balance", BalanceDiceContext);
        bool success = total >= BalanceDC;
        state = new BalanceState { Round = round, Failed = !success };
        _balanceByCreature[character] = state;

        if (success)
        {
            LogEffect($"{character.Stats.CharacterName} Balance {total} vs DC {BalanceDC} SUCCESS: moves through the grease at half speed this round.");
            return;
        }

        stop = true;
        state.StopLoggedRound = round;
        int failedBy = BalanceDC - total;
        LogEffect($"{character.Stats.CharacterName} Balance {total} vs DC {BalanceDC} FAIL: cannot move this round.");

        if (character.HasCondition(CombatConditionType.Prone))
            return;

        if (failedBy >= 5)
        {
            character.ApplyCondition(CombatConditionType.Prone, -1, "Grease");
            LogEffect($"💥 {character.Stats.CharacterName} fails by {failedBy} (5 or more) and falls prone.");
            return;
        }

        RollFallSave(character, "slips in");
    }

    /// <summary>The Reflex save or fall (SaveRollRules through RollSpellSave, D13).</summary>
    private void RollFallSave(CharacterController character, string timing)
    {
        // A creature the grease does not hinder rolls nothing: an incorporeal creature does not stand on the surface and
        // cannot fall (MM p.311), and Freedom of Movement moves normally despite it (PHB p.233; delegated RAW reading
        // 2026-10-29, docs/systems/RULES_COVERAGE.md 4.1; MovementTerrain.ZoneHindersMovement, the one exemption).
        if (!MovementTerrain.ZoneHindersMovement(character, this))
            return;

        SaveRoll save = RollSpellSave(character, SavingThrowType.Reflex, SaveDC, ReflexDiceContext); // SaveRollRules, D13
        LogEffect($"{character.Stats.CharacterName} {timing} grease: Reflex d20({save.Roll}) + {save.Modifier} = {save.Total} vs DC {SaveDC} {SaveRollRules.Outcome(save)}");

        if (!save.Succeeded)
        {
            character.ApplyCondition(CombatConditionType.Prone, -1, "Grease");
            LogEffect($"💥 {character.Stats.CharacterName} falls prone.");
        }
    }

    protected override void OnAreaExpires()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, false);
        LogEffect("Grease dissipates.");
    }
}
