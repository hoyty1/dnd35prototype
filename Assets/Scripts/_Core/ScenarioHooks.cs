using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test instrumentation. Every member is null or false in normal play; rules code must never read these values.
/// The scenario harness (editor only) sets them to observe or steer a fight: force dice through
/// <see cref="RollFilter"/>, record attacks, maneuvers, NPC casts, moves, conditions and phase changes, script
/// turns, and force the initiative head. Raise sites are single null-conditional calls, so the game
/// behaves identically while the hooks are null.
///
/// Handlers run inline inside rules code. Install every observer through <c>Safe</c> (any arity)
/// and every scripted turn through <see cref="SafeTurn"/>: an exception in a harness handler is then
/// recorded in <see cref="HookErrors"/> and logged, and the game path goes on as if no hook were set.
/// A run must fail when <see cref="HookErrors"/> is not empty. Call <see cref="ClearAll"/> when a run
/// ends; the editor-only ScenarioSessionGuard (on leaving Play mode) and the static suite runner
/// (before a pass) also clear leftovers.
/// </summary>
public static class ScenarioHooks
{
    /// <summary>
    /// Replaces a die result after it was drawn: (sides, context, natural) returns the value to use.
    /// The natural roll is always drawn first, so forcing a die never shifts later rolls.
    /// Context is the DiceService context string, or null for DiceRoller and context-free rolls.
    /// A filter that throws or returns a value outside 1..sides is recorded in <see cref="HookErrors"/>
    /// and the natural roll is used.
    /// </summary>
    public static Func<int, string, int, int> RollFilter;

    /// <summary>Every resolved weapon attack roll (CharacterController.PerformSingleAttackWithCrit): attacker, result.</summary>
    public static Action<CharacterController, CombatResult> AttackResolved;

    /// <summary>Every attack of opportunity that was made: threatener, target, trigger, result.</summary>
    public static Action<CharacterController, CharacterController, string, CombatResult> AoOResolved;

    /// <summary>Every CharacterController.ExecuteSpecialAttack call: attacker, target, type, result.</summary>
    public static Action<CharacterController, CharacterController, SpecialAttackType, SpecialAttackResult> ManeuverResolved;

    /// <summary>A spell cast while threatened: caster, spell, cast defensively, spell proceeds, threatener count.</summary>
    public static Action<CharacterController, SpellData, bool, bool, int> ThreatenedCast;

    /// <summary>
    /// An NPC-path cast begins (GameManager.TryNPCPerformSpellCast, right after the standard action is committed,
    /// before Concentration, spell failure, AoOs or counterspells decide whether it goes off): caster, target, spell.
    /// The PC cast pipelines do not raise it.
    /// </summary>
    public static Action<CharacterController, CharacterController, SpellData> SpellCast;

    /// <summary>A position change: mover, from, to, movement type ("move", "path-move", "teleport", ...).</summary>
    public static Action<CharacterController, Vector2Int, Vector2Int, string> Moved;

    /// <summary>A condition added to or removed from a creature: target, type, added, rounds, source.</summary>
    public static Action<CharacterController, CombatConditionType, bool, int, string> ConditionChanged;

    /// <summary>Every combat log line, before it reaches the panel.</summary>
    public static Action<string> CombatLog;

    /// <summary>GameManager.CurrentPhase changed: old phase, new phase.</summary>
    public static Action<GameManager.TurnPhase, GameManager.TurnPhase> PhaseChanged;

    /// <summary>
    /// Scripted turn for an AI-run actor. Called by AIService.ExecuteNPCTurn after the turn-start rules,
    /// the HP gate and the confused, charmed and fascinated gates. A non-null coroutine replaces the
    /// whole rest of the AI turn, so it also bypasses the automatic stand-up from prone, the
    /// frightened/panicked gate, the Animate Rope escape, the no-target search turn, the turned-undead
    /// gate, the grapple turn, free auras and spittle, and the Resilient Sphere restriction. The provider
    /// must return null for Frightened, Panicked or Turned actors (and for grappling ones unless the
    /// script runs the grapple rules), so the AI applies those compulsions. Install the provider through
    /// <see cref="SafeTurn"/> so a throwing script ends the turn normally instead of stalling it.
    /// </summary>
    public static Func<CharacterController, IEnumerator> ScriptedTurn;

    /// <summary>When non-empty, these combatants go first in initiative, in this order.</summary>
    public static List<CharacterController> ForcedFirstInitiative;

    /// <summary>When true, GameManager.Update ignores mouse and keyboard world input.</summary>
    public static bool SuppressPlayerInput;

    /// <summary>Exceptions thrown by hook handlers and invalid filtered rolls since the last <see cref="ClearAll"/>.</summary>
    public static readonly List<string> HookErrors = new List<string>();

    /// <summary>Applies <see cref="RollFilter"/> to a drawn die; returns <paramref name="natural"/> when no filter is set.</summary>
    public static int FilterRoll(int sides, string ctx, int natural)
        => RollFilter == null ? natural : FilterRollChecked(sides, ctx, natural);

    private static int FilterRollChecked(int sides, string ctx, int natural)
    {
        int value;
        try
        {
            value = RollFilter(sides, ctx, natural);
        }
        catch (Exception ex)
        {
            RecordError("RollFilter", ex);
            return natural;
        }

        if (value < 1 || value > sides)
        {
            RecordError("RollFilter", "returned " + value + " for a d" + sides + " (context " + (ctx ?? "none") + "); natural " + natural + " used");
            return natural;
        }
        return value;
    }

    /// <summary>Wraps a hook handler so an exception is recorded instead of escaping into rules code.</summary>
    public static Action<T> Safe<T>(string name, Action<T> handler)
    {
        if (handler == null) return null;
        return a => { try { handler(a); } catch (Exception ex) { RecordError(name, ex); } };
    }

    /// <summary>Wraps a hook handler so an exception is recorded instead of escaping into rules code.</summary>
    public static Action<T1, T2> Safe<T1, T2>(string name, Action<T1, T2> handler)
    {
        if (handler == null) return null;
        return (a, b) => { try { handler(a, b); } catch (Exception ex) { RecordError(name, ex); } };
    }

    /// <summary>Wraps a hook handler so an exception is recorded instead of escaping into rules code.</summary>
    public static Action<T1, T2, T3> Safe<T1, T2, T3>(string name, Action<T1, T2, T3> handler)
    {
        if (handler == null) return null;
        return (a, b, c) => { try { handler(a, b, c); } catch (Exception ex) { RecordError(name, ex); } };
    }

    /// <summary>Wraps a hook handler so an exception is recorded instead of escaping into rules code.</summary>
    public static Action<T1, T2, T3, T4> Safe<T1, T2, T3, T4>(string name, Action<T1, T2, T3, T4> handler)
    {
        if (handler == null) return null;
        return (a, b, c, d) => { try { handler(a, b, c, d); } catch (Exception ex) { RecordError(name, ex); } };
    }

    /// <summary>Wraps a hook handler so an exception is recorded instead of escaping into rules code.</summary>
    public static Action<T1, T2, T3, T4, T5> Safe<T1, T2, T3, T4, T5>(string name, Action<T1, T2, T3, T4, T5> handler)
    {
        if (handler == null) return null;
        return (a, b, c, d, e) => { try { handler(a, b, c, d, e); } catch (Exception ex) { RecordError(name, ex); } };
    }

    /// <summary>
    /// Wraps a scripted-turn provider: a provider or script step (including nested IEnumerator steps)
    /// that throws is recorded and the script stops, so ExecuteNPCTurn returns and the turn ends normally.
    /// </summary>
    public static Func<CharacterController, IEnumerator> SafeTurn(Func<CharacterController, IEnumerator> provider)
    {
        if (provider == null) return null;
        return actor =>
        {
            IEnumerator script;
            try
            {
                script = provider(actor);
            }
            catch (Exception ex)
            {
                RecordError("ScriptedTurn", ex);
                return null;
            }
            return script == null ? null : SafeEnumerate(script);
        };
    }

    private static IEnumerator SafeEnumerate(IEnumerator script)
    {
        while (true)
        {
            object current;
            try
            {
                if (!script.MoveNext())
                    yield break;
                current = script.Current;
            }
            catch (Exception ex)
            {
                RecordError("ScriptedTurn", ex);
                yield break;
            }

            // A nested coroutine is wrapped too, so its exceptions are caught here as well.
            if (current is IEnumerator nested)
                yield return SafeEnumerate(nested);
            else
                yield return current;
        }
    }

    private static void RecordError(string name, Exception ex)
    {
        HookErrors.Add(name + ": " + ex.GetType().Name + ": " + ex.Message);
        Debug.LogException(ex);
    }

    private static void RecordError(string name, string message)
    {
        HookErrors.Add(name + ": " + message);
        Debug.LogWarning("[ScenarioHooks] " + name + ": " + message);
    }

    /// <summary>The names of the hooks that are not inert (and the error count), comma separated, or null when none is set.</summary>
    public static string DescribeSet()
    {
        var set = new List<string>();
        if (RollFilter != null) set.Add(nameof(RollFilter));
        if (AttackResolved != null) set.Add(nameof(AttackResolved));
        if (AoOResolved != null) set.Add(nameof(AoOResolved));
        if (ManeuverResolved != null) set.Add(nameof(ManeuverResolved));
        if (ThreatenedCast != null) set.Add(nameof(ThreatenedCast));
        if (SpellCast != null) set.Add(nameof(SpellCast));
        if (Moved != null) set.Add(nameof(Moved));
        if (ConditionChanged != null) set.Add(nameof(ConditionChanged));
        if (CombatLog != null) set.Add(nameof(CombatLog));
        if (PhaseChanged != null) set.Add(nameof(PhaseChanged));
        if (ScriptedTurn != null) set.Add(nameof(ScriptedTurn));
        if (ForcedFirstInitiative != null) set.Add(nameof(ForcedFirstInitiative));
        if (SuppressPlayerInput) set.Add(nameof(SuppressPlayerInput));
        if (HookErrors.Count > 0) set.Add(nameof(HookErrors) + "(" + HookErrors.Count + ")");
        return set.Count == 0 ? null : string.Join(", ", set);
    }

    /// <summary>Resets every hook to its inert value and clears <see cref="HookErrors"/>; read the errors first.</summary>
    public static void ClearAll()
    {
        RollFilter = null;
        AttackResolved = null;
        AoOResolved = null;
        ManeuverResolved = null;
        ThreatenedCast = null;
        SpellCast = null;
        Moved = null;
        ConditionChanged = null;
        CombatLog = null;
        PhaseChanged = null;
        ScriptedTurn = null;
        ForcedFirstInitiative = null;
        SuppressPlayerInput = false;
        HookErrors.Clear();
    }
}
