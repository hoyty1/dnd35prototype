/// <summary>
/// GameManager partial class: the start of a combatant's turn (batch 12 steps 5-6, docs/designs/pc_npc_parity_batch12.md;
/// docs/systems/PC_NPC_PARITY.md section 5).
///
/// Two routines hold the rules work of a turn start, the same for a PC and an NPC:
/// - <see cref="BeginCombatantTurn"/> runs once per turn from <c>OnTurnStarted</c>, before the turn routine's turn-skip
///   gate (<c>ShouldSkipTurnDueToHPState</c>), whether or not the creature can act: the ongoing effects, the cooldowns,
///   the per-turn resets and the turn-start combat-end check (decision D3).
/// - <see cref="BeginActingTurn"/> runs after the gate, only for a creature that acts: the Turn Undead proximity check.
///
/// Barbarian rage passes at the barbarian's initiative count (<c>OnInitiativeCountReached</c>, <see cref="TickRageAtCount"/>),
/// like bardic music. Only the NPC path checks for unseen creatures at turn start (CMB-094, batch 12 step 8).
/// <see cref="BeginCombatantTurn"/> never yields: the skip gate advances the turn synchronously (CORE-012).
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// The turn-start work of every creature, before the turn-skip gate (delegated RAW reading 2026-10-23, decision D3,
    /// docs/systems/RULES_COVERAGE.md 4.1: MM p.314, regeneration works every
    /// round; PHB p.138, durations run on the creature's own initiative count; PHB p.142, fighting defensively lasts until
    /// the creature's next turn). Returns true when the combat is over, in which case <c>OnTurnStarted</c> starts no turn
    /// routine. Concentration on a skipped turn keeps its own rule (<c>MaintainConcentrationOnSkippedTurn</c>).
    /// </summary>
    private bool BeginCombatantTurn(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return false;

        // Ongoing damage and healing at the start of the turn.
        HandleFlamingSphereTurnStart(actor);
        ApplyMelfsAcidArrowTurnStartDamage(actor);
        actor.ApplyRegenerationAtTurnStart();

        // Breath weapon, ranged special and terrain manipulation cooldowns (AI-032), and the once-per-round flags.
        actor.TickTurnCooldowns();
        actor.ClearOncePerRoundFlags();

        // Aid Another expiry counters tick before actions; this keeps bonuses available for one full beneficiary turn.
        ExpireAidBonusesAtTurnStart(actor);

        // The per-turn resets: actions, attacks of opportunity, fighting defensively, a readied counterspell, movement.
        actor.StartNewTurn();

        // Ongoing damage at the start of the turn can drop the last creature of a side: one check for both sides (CORE-003).
        return EvaluateCombatEnd("TurnStart");
    }

    /// <summary>
    /// The turn-start rules work that runs after the turn-skip gate, only for a creature that acts this turn, PC or NPC:
    /// the Turn Undead tracker prune and proximity check. The PC path's UI resets (off-hand and dual-wield fields, the
    /// attack sequence, the logs) stay in <c>StartPCTurn</c>.
    /// </summary>
    private void BeginActingTurn(CharacterController actor)
    {
        if (actor == null)
            return;

        PruneTurnUndeadTrackers();
        CheckTurnUndeadProximityBreakingForCleric(actor);
    }

    /// <summary>
    /// A round of a raging barbarian's rage passes at its initiative count, PC or NPC, whether or not it can act (PHB p.25,
    /// rage lasts a number of rounds; decision D3; CHR-069). Called from <c>OnInitiativeCountReached</c>.
    /// </summary>
    private void TickRageAtCount(CharacterController count)
    {
        if (count == null || count.Stats == null || !count.Stats.IsBarbarian || !count.Stats.IsRaging)
            return;

        count.Stats.TickRage();
        LogRageTick(count);
    }

    /// <summary>The combat log line of a rage tick: the rounds left, or the end of the rage and the fatigue.</summary>
    private void LogRageTick(CharacterController barbarian)
    {
        CharacterStats s = barbarian.Stats;
        if (!s.IsRaging)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"😫 {s.CharacterName}'s rage has ended! Now fatigued."));
            UpdateAllStatsUI();
        }
        else
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚡", $"{s.CharacterName}: Rage - {s.RageRoundsRemaining} rounds remaining"));
        }
    }
}
