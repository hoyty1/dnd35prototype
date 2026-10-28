/// <summary>
/// GameManager partial class: the start of a combatant's turn (batch 12 step 5, docs/designs/pc_npc_parity_batch12.md;
/// docs/systems/PC_NPC_PARITY.md section 5).
///
/// Two routines hold the rules work of a turn start:
/// - <see cref="BeginCombatantTurn"/> runs before the turn-skip gate (<c>ShouldSkipTurnDueToHPState</c>): the ongoing
///   effects of the turn start and the turn-start combat-end check.
/// - <see cref="BeginActingTurn"/> runs after the gate, only for a creature that acts: the rage tick (PC path),
///   the per-turn resets (<see cref="CharacterController.StartNewTurn"/>), perception (NPC path) and the Turn Undead
///   proximity check.
///
/// Step 5 is a pure move: each routine still has one branch per turn path, holding exactly what that path ran
/// before, so the PC and NPC turn starts still differ in order and content (CMB-075, CHR-069, CMB-094, CORE-003,
/// AI-032). <see cref="TurnStartPath"/> names the caller's path; it never reads <c>IsControllable</c>.
/// Step 6 (<c>b12-turn-start-raw</c>) merges the branches into one RAW order (decision D3) and removes the parameter.
/// <see cref="BeginCombatantTurn"/> never yields: the skip gate advances the turn synchronously (CORE-012).
/// </summary>
public partial class GameManager
{
    /// <summary>Which turn routine started this turn: <c>StartPCTurn</c> (UI input) or <c>SingleNPCTurnFromInitiative</c>
    /// (AI choice). Temporary: batch 12 step 6 deletes it with the per-path branches.</summary>
    public enum TurnStartPath
    {
        Pc,
        Npc
    }

    /// <summary>
    /// The turn-start work that runs before the turn-skip gate. Returns true when the combat is over, in which case the
    /// caller returns at once. The scenario trace treats this method as transparent when it names the callers of a
    /// combat end (ScenarioTrace.CallerFrames), so its check reports the turn routine that called it.
    /// </summary>
    private bool BeginCombatantTurn(CharacterController actor, TurnStartPath path)
    {
        if (path == TurnStartPath.Pc)
        {
            // PC path (CMB-075: these effects run before the gate here but after it on the NPC path).
            HandleFlamingSphereTurnStart(actor);
            ApplyMelfsAcidArrowTurnStartDamage(actor);
            actor.TickBombardierAcidSprayCooldown();
            actor.ApplyRegenerationAtTurnStart();
            CloseInventoryIfOpen(); // UI: only the PC path has an inventory window open

            // Ongoing damage at the start of the turn can drop the last creature of a side.
            if (EvaluateCombatEnd("StartPCTurn.TurnStartEffects"))
                return true;

            // Tick Aid Another expiry counters before actions; this keeps bonuses available for one full beneficiary turn.
            ExpireAidBonusesAtTurnStart(actor);
            return false;
        }

        // NPC path: Melf's Acid Arrow, regeneration and the bombardier cooldown run after the gate, in
        // BeginActingTurn (CMB-075), so this check runs before the Acid Arrow tick (CORE-003).
        ExpireAidBonusesAtTurnStart(actor);
        HandleFlamingSphereTurnStart(actor);

        // Ongoing damage at the start of the turn can drop the last creature of a side.
        return EvaluateCombatEnd("SingleNPCTurn.TurnStartEffects");
    }

    /// <summary>
    /// The turn-start rules work that runs after the turn-skip gate, only for a creature that acts this turn.
    /// The PC path's UI resets (off-hand and dual-wield fields, the attack sequence, the logs) stay in
    /// <c>StartPCTurn</c>. AIService.ExecuteNPCTurnBody calls the NPC path.
    /// </summary>
    public void BeginActingTurn(CharacterController actor, TurnStartPath path)
    {
        if (actor == null)
            return;

        if (path == TurnStartPath.Pc)
        {
            // Barbarian rage passes on the PC path only, and only on a turn the PC acts (CHR-069).
            if (actor.Stats.IsBarbarian && actor.Stats.IsRaging)
            {
                actor.Stats.TickRage();
                if (!actor.Stats.IsRaging)
                {
                    CombatUI.ShowCombatLog(CombatLogHelper.Info("", $"😫 {actor.Stats.CharacterName}'s rage has ended! Now fatigued."));
                    UpdateAllStatsUI();
                }
                else
                {
                    CombatUI.ShowCombatLog(CombatLogHelper.Damage("⚡", $"{actor.Stats.CharacterName}: Rage - {actor.Stats.RageRoundsRemaining} rounds remaining"));
                }
            }

            // Bardic music rounds pass at the bard's initiative count for PCs and NPCs alike (OnInitiativeCountReached, CHR-069).

            actor.StartNewTurn();

            PruneTurnUndeadTrackers();
            CheckTurnUndeadProximityBreakingForCleric(actor);
            return;
        }

        // NPC path. ConditionService.OnTurnStart already ran in OnTurnStarted, before the turn-skip check (CMB-006).
        // These three ticks run before the gate on the PC path (CMB-075).
        ApplyMelfsAcidArrowTurnStartDamage(actor);
        actor.TickBombardierAcidSprayCooldown();
        actor.ApplyRegenerationAtTurnStart();
        actor.StartNewTurn();
        // Only the NPC path checks for unseen creatures at turn start (CMB-094).
        ProcessNPCRoundStartPerception(actor);
        PruneTurnUndeadTrackers();
        CheckTurnUndeadProximityBreakingForCleric(actor);
    }
}
