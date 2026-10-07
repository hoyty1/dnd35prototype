#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Scenarios
{
    /// <summary>
    /// Invariants, combat-end detection and the watchdog for one job. Each invariant has an id; a broken one
    /// becomes a 'violation' trace event, matched against the def's and the global waivers
    /// (<see cref="KnownIssueWaivers"/>). Checks run inside game callbacks, so they never throw into rules code
    /// (the trace wraps them) and never change game state, except the harness halts listed below.
    /// </summary>
    internal sealed class ScenarioChecks
    {
        public const string HpBounds = "hp-bounds";
        public const string GridInv = "grid";
        public const string TurnStructure = "turn-structure";
        public const string NoActWhenDown = "no-act-when-down";
        public const string AttackMath = "attack-math";
        public const string Econ = "econ";
        public const string AoOBudget = "aoo-budget";
        public const string ProneMove = "prone-move";
        public const string TeamStable = "team-stable";
        public const string NoErrors = "no-errors";
        public const string CombatEndDetected = "combat-end-detected";
        public const string CombatEndEarly = "combat-end-early";
        public const string PhaseAfterCombatOver = "phase-after-combatover";
        public const string HookError = "hook-error";

        /// <summary>Frames and game-seconds without progress before a soft lock is declared.</summary>
        public int SoftLockFrames = 300;
        public float SoftLockGameSeconds = 60f;
        public float WallClockCapSeconds = 120f;

        private readonly ScenarioJob _job;
        private readonly GameManager _gm;
        private readonly Dictionary<CharacterController, int> _lastTurnRound = new Dictionary<CharacterController, int>();
        private readonly Dictionary<CharacterController, int> _aooSinceTurn = new Dictionary<CharacterController, int>();
        private readonly Dictionary<CharacterController, CharacterTeam> _team = new Dictionary<CharacterController, CharacterTeam>();
        private readonly Dictionary<CharacterController, int> _speedSquaresAtTurnStart = new Dictionary<CharacterController, int>();
        private bool _singleActionAtTurnStart;
        private CharacterController _openTurn;
        private int _ownMoveSquares;
        private int _movesSinceAction;
        private int _lastRound;
        private int _turnStartsFrame = -1;
        private int _turnStartsInFrame;
        private int _hookErrorsSeen;
        private float _startRealtime;

        public ScenarioChecks(ScenarioJob job, GameManager gm)
        {
            _job = job;
            _gm = gm;
            _startRealtime = Time.realtimeSinceStartup;
        }

        public void RegisterActor(CharacterController c)
        {
            if (c != null && !_team.ContainsKey(c))
                _team[c] = c.Team;
        }

        // ── Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Dead, dying, stable or unconscious (CharacterController.IsUnconscious covers the HP states and the
        /// Unconscious condition). A disabled creature can still act: at 0 HP (PHB p.145), and at negative HP with
        /// Diehard (PHB p.93), so negative HP alone does not make a creature down.
        /// </summary>
        public static bool IsDown(CharacterController c)
        {
            if (c == null || c.Stats == null)
                return true;
            return c.IsDead || c.Stats.IsDead || c.IsUnconscious;
        }

        /// <summary>
        /// Harness policy for combat end (owner decision 2026-10-07): a creature is out of the fight when it is dead,
        /// dying or unconscious. A creature at negative HP with regeneration or fast healing still counts until dead.
        /// A disabled creature still counts: at 0 HP (PHB p.145) or at negative HP with Diehard.
        /// </summary>
        public static bool IsOutOfFight(CharacterController c)
        {
            if (c == null || c.Stats == null || c.IsDead || c.Stats.IsDead)
                return true;
            if (!IsDown(c))
                return false;
            return !CanRecover(c);
        }

        private static bool CanRecover(CharacterController c)
        {
            if (c.HasRegeneration)
                return true;
            List<string> abilities = c.Stats.SpecialAbilities;
            if (abilities == null)
                return false;
            foreach (string a in abilities)
            {
                if (string.IsNullOrWhiteSpace(a))
                    continue;
                string n = a.ToLowerInvariant();
                if (n.Contains("regeneration") || n.Contains("fast healing"))
                    return true;
            }
            return false;
        }

        private static bool IsActive(CharacterController c)
            => c != null && c.gameObject != null && c.gameObject.activeInHierarchy && c.Stats != null;

        /// <summary>For each side, whether it has any active member and whether every active member is out.</summary>
        public void SideState(out bool playersOut, out bool enemiesOut, out string detail)
            => SideState(out playersOut, out enemiesOut, out detail, out _, out _);

        /// <summary>
        /// As above, plus for each side whether every member that is out has HP at or below 0 (the game's own
        /// end predicates, AreAllNPCsDead and AreAllPCsDead, count only HP &lt;= 0; a side put out at positive HP,
        /// by sleep or nonlethal damage, is a predicate mismatch, CORE-037, not a missed check, CORE-011).
        /// </summary>
        public void SideState(out bool playersOut, out bool enemiesOut, out string detail, out bool playersOutByHp, out bool enemiesOutByHp)
        {
            int pIn = 0, pAll = 0, eIn = 0, eAll = 0, pOutPositive = 0, eOutPositive = 0;
            var parts = new List<string>();
            foreach (CharacterController c in _gm.GetAllCharactersForAI())
            {
                if (!IsActive(c) || c.Team == CharacterTeam.Neutral)
                    continue;
                bool outOf = IsOutOfFight(c);
                bool positive = outOf && c.Stats.CurrentHP > 0 && !c.IsDead && !c.Stats.IsDead;
                if (c.Team == CharacterTeam.Player) { pAll++; if (!outOf) pIn++; if (positive) pOutPositive++; }
                else { eAll++; if (!outOf) eIn++; if (positive) eOutPositive++; }
                parts.Add(_job.Trace.KeyOf(c) + "=" + c.Stats.CurrentHP + "/" + c.CurrentHPState);
            }
            playersOut = pAll > 0 && pIn == 0;
            enemiesOut = eAll > 0 && eIn == 0;
            playersOutByHp = playersOut && pOutPositive == 0;
            enemiesOutByHp = enemiesOut && eOutPositive == 0;
            detail = "players " + pIn + "/" + pAll + " in, enemies " + eIn + "/" + eAll + " in: " + string.Join(", ", parts);
        }

        private void Violation(string inv, string detail, CharacterController actor = null)
        {
            _job.AddViolation(inv, detail, actor != null ? _job.Trace.KeyOf(actor) : null);
        }

        // ── Turn structure, snapshots ───────────────────────────────────

        public void OnNewRound(int round)
        {
            if (round < _lastRound)
                Violation(TurnStructure, "round went from " + _lastRound + " to " + round);
            _lastRound = round;

            if (round > _job.MaxRounds && !_job.Decided)
                _job.Decide(Outcome.Stalemate, "round " + round + " exceeds MaxRounds " + _job.MaxRounds, haltNow: false);
        }

        public void OnTurnStart(CharacterController a, List<JsonObj> snaps)
        {
            // Skip-chain guard (CORE-012): turns start synchronously inside each other.
            if (_turnStartsFrame != Time.frameCount)
            {
                _turnStartsFrame = Time.frameCount;
                _turnStartsInFrame = 0;
            }
            _turnStartsInFrame++;
            int limit = 3 * Math.Max(1, _job.InitiativeCount);
            if (_turnStartsInFrame > limit && !_job.Decided)
            {
                _job.Decide(Outcome.SkipChainRunaway, _turnStartsInFrame + " turn starts in one frame (limit " + limit + ")", haltNow: true);
                return;
            }

            if (_openTurn != null)
                Violation(TurnStructure, "turn_start for " + _job.Trace.KeyOf(a) + " while " + _job.Trace.KeyOf(_openTurn) + "'s turn is still open", a);
            _openTurn = a;
            _ownMoveSquares = 0;
            _movesSinceAction = 0;
            _aooSinceTurn[a] = 0;
            _speedSquaresAtTurnStart[a] = a.Stats != null ? Math.Max(0, a.Stats.EffectiveSpeedFeet / 5) : 0;
            // Disabled or staggered when the turn starts (BeginNPCTurnForAI may still set it; the flag is read again
            // at turn end only if it was already set here, so a creature dropped to 0 HP mid-charge is not flagged).
            _singleActionAtTurnStart = a.Actions != null && a.Actions.SingleActionOnly;

            int round = _gm.CurrentRound;
            if (_lastTurnRound.TryGetValue(a, out int last) && last == round)
                Violation(TurnStructure, _job.Trace.KeyOf(a) + " has a second turn in round " + round, a);
            _lastTurnRound[a] = round;

            if (a.IsDead || (a.Stats != null && a.Stats.IsDead))
                Violation(TurnStructure, "turn_start for dead " + _job.Trace.KeyOf(a), a);

            CheckState("turn_start");
        }

        public void OnTurnEnd(CharacterController a, TraceEvent ev)
        {
            if (_openTurn != a)
                Violation(TurnStructure, "turn_end for " + _job.Trace.KeyOf(a) + " but the open turn is " + (_openTurn != null ? _job.Trace.KeyOf(_openTurn) : "none"), a);
            _openTurn = null;

            ActionEconomy act = a.Actions;
            if (act != null)
            {
                // A disabled or staggered creature may take only a move or a standard action (PHB p.145-146).
                if (_singleActionAtTurnStart && act.SingleActionOnly && act.FullRoundActionUsed)
                    Violation(Econ, _job.Trace.KeyOf(a) + " took a full-round action while limited to a single action", a);
                if (act.HasMoved5Ft && _ownMoveSquares > 1)
                    Violation(Econ, _job.Trace.KeyOf(a) + " took a 5-ft step and moved " + _ownMoveSquares + " squares", a);
            }
            _speedSquaresAtTurnStart.TryGetValue(a, out int speed);
            if (speed > 0 && _ownMoveSquares > 4 * speed)
                Violation(Econ, _job.Trace.KeyOf(a) + " moved " + _ownMoveSquares + " squares with speed " + speed, a);

            CheckState("turn_end");
            CheckHookErrors();

            // Combat end: a side out with no CombatOver by the end of the turn in which it went out.
            if (!_job.Decided && _gm.CurrentPhase != GameManager.TurnPhase.CombatOver)
            {
                SideState(out bool playersOut, out bool enemiesOut, out string detail, out bool playersByHp, out bool enemiesByHp);
                if (playersOut || enemiesOut)
                {
                    bool victory = enemiesOut && !playersOut;
                    // cause=hp<=0: the game's own predicate says the side is out too, so a check was missed (CORE-011);
                    // cause=unconscious>0: someone is out at positive HP, which the game's predicate ignores (CORE-037).
                    string cause = (victory ? enemiesByHp : playersByHp) ? "hp<=0" : "unconscious>0";
                    Violation(CombatEndDetected, (victory ? "victory" : "defeat") + " side out (cause=" + cause + ") but the game did not end combat: " + detail);
                    _job.Decide(victory ? Outcome.VictoryUndetected : Outcome.DefeatUndetected, detail, haltNow: false);
                }
            }
        }

        private void CheckState(string when)
        {
            SquareGrid grid = _gm.Grid;
            var byPos = new Dictionary<Vector2Int, CharacterController>();
            foreach (CharacterController c in _job.Trace.KnownActors)
            {
                if (!IsActive(c))
                    continue;
                string key = _job.Trace.KeyOf(c);
                CharacterStats s = c.Stats;

                // hp-bounds
                if (s.CurrentHP > s.TotalMaxHP + Math.Max(0, s.TempHP))
                    Violation(HpBounds, when + ": " + key + " HP " + s.CurrentHP + " above max " + s.TotalMaxHP + " + temp " + s.TempHP, c);
                string stateProblem = HpStateProblem(c);
                if (stateProblem != null)
                    Violation(HpBounds, when + ": " + key + " " + stateProblem, c);

                // team-stable
                if (_team.TryGetValue(c, out CharacterTeam team) && team != c.Team
                    && !c.HasCondition(CombatConditionType.Charmed) && !c.HasCondition(CombatConditionType.Commanded))
                    Violation(TeamStable, when + ": " + key + " changed team " + team + " -> " + c.Team + " with no charm", c);

                // grid
                if (c.IsDead || s.IsDead)
                    continue;
                if (grid != null)
                {
                    if (!grid.IsValidPosition(c.GridPosition))
                    {
                        Violation(GridInv, when + ": " + key + " is off the grid at " + c.GridPosition, c);
                        continue;
                    }
                    SquareCell cell = grid.GetCell(c.GridPosition);
                    if (cell != null && !cell.ContainsOccupant(c))
                        Violation(GridInv, when + ": " + key + " at " + c.GridPosition + " is not in that square's occupancy", c);
                }
                if (byPos.TryGetValue(c.GridPosition, out CharacterController other))
                {
                    // Grapplers share a square (PHB p.156), and a creature may end its move in a helpless
                    // creature's square (PHB p.148, Ending Your Movement).
                    bool allowed = SafeGrappling(c) || SafeGrappling(other) || IsHelpless(c) || IsHelpless(other);
                    if (!allowed)
                        Violation(GridInv, when + ": " + key + " and " + _job.Trace.KeyOf(other) + " share " + c.GridPosition, c);
                }
                else
                {
                    byPos[c.GridPosition] = c;
                }
            }
        }

        private static bool IsHelpless(CharacterController c)
            => IsDown(c) || c.HasCondition(CombatConditionType.Helpless) || c.HasCondition(CombatConditionType.Paralyzed);

        private static bool SafeGrappling(CharacterController c)
        {
            try { return c.IsGrappling(); } catch (Exception) { return false; }
        }

        /// <summary>Null when the HP state agrees with HP (CharacterController.DetermineStateFromHPTransition), else the problem.</summary>
        private static string HpStateProblem(CharacterController c)
        {
            int hp = c.Stats.CurrentHP;
            HPState st = c.CurrentHPState;
            if (st == HPState.Dead)
                return null; // death effects may kill above -10
            if (hp <= -10)
                return "HP " + hp + " but state " + st;
            if (hp < 0 && st != HPState.Dying && st != HPState.Stable && st != HPState.Disabled)
                return "HP " + hp + " but state " + st;
            if (hp == 0 && st != HPState.Disabled && st != HPState.Unconscious)
                return "HP 0 but state " + st;
            if (hp > 0 && st != HPState.Healthy && st != HPState.Staggered && st != HPState.Unconscious)
                return "HP " + hp + " but state " + st;
            return null;
        }

        // ── Actions ─────────────────────────────────────────────────────

        public void OnAttack(CharacterController attacker, TraceEvent ev)
        {
            _movesSinceAction = 0;
            string key = ev.Str("attacker");
            if (ev.Bool("attackerDown"))
                Violation(NoActWhenDown, key + " attacked while down", attacker);

            int die = ev.Int("die");
            if (die <= 0)
                return; // no attack roll (automatic hit or a non-roll path)
            int total = ev.Int("total");
            int ac = ev.Int("ac");
            bool hit = ev.Bool("hit");
            int flankBonus = ev.Int("flankBonus");
            if (flankBonus != 0 && flankBonus != 2)
                Violation(AttackMath, key + " flanking bonus " + flankBonus + " (PHB p.153: +2)", attacker);
            if (ev.Bool("nat1") && hit)
                Violation(AttackMath, key + " hit on a natural 1 (PHB p.134)", attacker);
            // Concealment, a protection barrier and Deflect Arrows (PHB p.93) all turn a hit into a miss.
            bool excused = ev.Bool("conceal") || ev.Bool("barrier") || ev.Bool("deflected");
            if (hit && total < ac && !ev.Bool("nat20"))
                Violation(AttackMath, key + " hit with " + total + " against AC " + ac + " (die " + die + ")", attacker);
            if (!hit && total >= ac && !ev.Bool("nat1") && !excused)
                Violation(AttackMath, key + " missed with " + total + " against AC " + ac + " (die " + die + ")", attacker);
        }

        public void OnAoO(CharacterController threatener, TraceEvent ev)
        {
            _movesSinceAction = 0;
            string key = ev.Str("by");
            if (threatener == null || threatener.Stats == null)
                return;
            _aooSinceTurn.TryGetValue(threatener, out int n);
            n++;
            _aooSinceTurn[threatener] = n;
            // One AoO per round, more with Combat Reflexes (PHB p.92, p.137); the count resets on the threatener's turn.
            int budget = 1 + (threatener.Stats.HasFeat("Combat Reflexes") ? Math.Max(0, threatener.Stats.DEXMod) : 0);
            if (n > budget)
                Violation(AoOBudget, key + " made " + n + " AoOs since its last turn (budget " + budget + ")", threatener);
        }

        public void OnActed(CharacterController actor, TraceEvent ev, string what)
        {
            if (ev.Bool("attackerDown"))
                Violation(NoActWhenDown, _job.Trace.KeyOf(actor) + " resolved a " + what + " while down", actor);

            // The bull rush follow and the overrun moves go through MoveToCell with markAsMoved, so they arrive as
            // ordinary 'move' events before the maneuver event. They are part of the maneuver, not the actor's own
            // movement (PHB p.154 limits the follow only by normal movement; whether it ends a 5-ft step is not
            // settled RAW), so take back the squares moved since the actor's last action or AoO. This can also
            // take back a move made just before an Improved Bull Rush with no AoO, so the econ squares stay a lower bound.
            object type = ev.Get("type");
            if (actor == _openTurn && type is SpecialAttackType t
                && (t == SpecialAttackType.BullRushAttack || t == SpecialAttackType.BullRushCharge || t == SpecialAttackType.Overrun))
                _ownMoveSquares = Math.Max(0, _ownMoveSquares - _movesSinceAction);
            _movesSinceAction = 0;
        }

        public void OnMove(CharacterController mover, TraceEvent ev)
        {
            string type = ev.Str("type");
            bool own = type == "move" || type == "path-move";
            if (!own)
                return;
            int sq = ev.Int("sq");
            if (mover == _openTurn)
            {
                _ownMoveSquares += sq;
                _movesSinceAction += sq;
            }
            // A prone creature can only crawl 5 ft as a move action (PHB p.143, CMB-074).
            if (ev.Bool("prone") && sq > 1)
                Violation(ProneMove, _job.Trace.KeyOf(mover) + " moved " + sq + " squares while prone", mover);
        }

        // ── Combat end, phase ───────────────────────────────────────────

        public void OnPhaseChanged(GameManager.TurnPhase from, GameManager.TurnPhase to, List<string> by)
        {
            if (from == GameManager.TurnPhase.CombatOver && to != GameManager.TurnPhase.CombatOver && _job.CombatOverSeen)
                Violation(PhaseAfterCombatOver, "phase " + from + " -> " + to + " after the game ended combat");

            if (to != GameManager.TurnPhase.CombatOver || !_job.Started)
                return;
            _job.CombatOverSeen = true;
            if (_job.Decided)
                return;

            SideState(out bool playersOut, out bool enemiesOut, out string detail);
            string byText = by != null ? string.Join(" < ", by) : "";
            Outcome outcome;
            if (enemiesOut && !playersOut)
                outcome = Outcome.Victory;
            else if (playersOut)
                outcome = Outcome.Defeat;
            else
            {
                outcome = byText.IndexOf("Victory", StringComparison.Ordinal) >= 0 ? Outcome.Victory : Outcome.Defeat;
                Violation(CombatEndEarly, (outcome == Outcome.Victory ? "victory" : "defeat") + " declared by " + byText + " while both sides have creatures in the fight: " + detail);
            }
            _job.Decide(outcome, "CombatOver by " + byText, haltNow: false);
        }

        // ── Logs, hooks, watchdog ───────────────────────────────────────

        /// <summary>
        /// Where a logged error or exception came from, by its stack trace: "harness" (a frame in the scenario
        /// harness or ScenarioHooks; a throwing hook handler is already a hook-error), "game" (a frame elsewhere
        /// under Assets/Scripts), or "foreign" (no project frame: editor windows, the agent's MCP CommandScripts).
        /// An empty stack trace cannot be placed and counts as "game".
        /// </summary>
        public static string LogOrigin(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return "game";
            string st = stackTrace.Replace('\\', '/');
            if (st.IndexOf("Assets/Scripts/Tests/Scenarios/", StringComparison.Ordinal) >= 0
                || st.IndexOf("Assets/Scripts/_Core/ScenarioHooks.cs", StringComparison.Ordinal) >= 0)
                return "harness";
            if (st.IndexOf("Assets/Scripts/", StringComparison.Ordinal) >= 0)
                return "game";
            return "foreign";
        }

        public void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            string origin = LogOrigin(stackTrace);
            if (origin != "game")
            {
                // Not game code: never a violation or an Exception ending (a typo in a Poll script must not cost the session).
                if (_job.Notes.Count < 50)
                    _job.Notes.Add(origin + " " + type.ToString().ToLowerInvariant() + " (not counted): " + FirstLine(condition));
                return;
            }
            if (type == LogType.Exception)
            {
                string first = FirstLine(condition);
                Violation(NoErrors, "exception: " + first + " | " + FirstLine(stackTrace));
                if (!_job.Decided)
                    _job.Decide(Outcome.Exception, first, haltNow: false); // halted by the runner next frame: halting inside a log callback is unsafe
            }
            else if (type == LogType.Error || type == LogType.Assert)
            {
                Violation(NoErrors, "error: " + FirstLine(condition));
            }
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            int nl = s.IndexOf('\n');
            string line = nl >= 0 ? s.Substring(0, nl) : s;
            return line.Length > 300 ? line.Substring(0, 300) : line;
        }

        public void CheckHookErrors()
        {
            while (_hookErrorsSeen < ScenarioHooks.HookErrors.Count)
            {
                Violation(HookError, ScenarioHooks.HookErrors[_hookErrorsSeen]);
                _hookErrorsSeen++;
            }
        }

        /// <summary>Per-frame checks from the runner's monitor loop.</summary>
        public void Frame()
        {
            CheckHookErrors();
            if (_job.Decided)
                return;

            ScenarioTrace trace = _job.Trace;
            int idleFrames = Time.frameCount - trace.LastProgressFrame;
            float idleSeconds = Time.time - trace.LastProgressTime;
            if (idleFrames > SoftLockFrames && idleSeconds > SoftLockGameSeconds)
            {
                // The counts and the dump vary with timing, so they go in 'diag', which the trace hash leaves out.
                _job.Decide(Outcome.SoftLock, "no progress", haltNow: false,
                    diag: "no progress for " + idleFrames + " frames and " + idleSeconds.ToString("0") + " game-seconds; " + Diagnose());
                return;
            }

            if (Time.realtimeSinceStartup - _startRealtime > WallClockCapSeconds)
                _job.Decide(Outcome.Timeout, "wall-clock cap " + WallClockCapSeconds + " s", haltNow: false, diag: Diagnose());
        }

        /// <summary>What the game is waiting for: Harness_DumpState, the open prompt-like UI, and the last trace lines (also added to the job notes).</summary>
        public string Diagnose()
        {
            string state;
            try { state = _gm.Harness_DumpState(); } catch (Exception ex) { state = "DumpState threw " + ex.GetType().Name + ": " + ex.Message; }

            var open = new List<string>();
            try
            {
                foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                {
                    foreach (Transform t in canvas.GetComponentsInChildren<Transform>(false))
                    {
                        string n = t.name;
                        if ((n.IndexOf("Prompt", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Dialog", StringComparison.OrdinalIgnoreCase) >= 0
                             || n.IndexOf("Confirm", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0)
                            && !open.Contains(n) && open.Count < 30)
                            open.Add(n);
                    }
                }
            }
            catch (Exception ex)
            {
                open.Add("canvas scan threw " + ex.GetType().Name);
            }

            _job.Notes.Add("diagnosis: " + state);
            _job.Notes.Add("open UI: " + (open.Count > 0 ? string.Join(", ", open) : "none"));
            foreach (string line in _job.Trace.LastLines(20))
                _job.Notes.Add("trace: " + line);
            return state + " | open UI: " + (open.Count > 0 ? string.Join(", ", open) : "none");
        }
    }
}
#endif
