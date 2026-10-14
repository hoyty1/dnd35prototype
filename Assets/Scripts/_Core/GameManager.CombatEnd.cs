using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager partial class: the end of combat, for both sides (CORE-011, CORE-034, CORE-037, CORE-001;
/// docs/systems/PC_NPC_PARITY.md, plan step 12).
///
/// <see cref="EvaluateCombatEnd"/> is the one check. It applies <see cref="CombatEndRules"/> per team over every
/// active combatant and ends the combat in victory or defeat. Rules code calls it after anything that can drop a
/// creature: each attack, spell, maneuver and AoO resolution that already checked, the PC action menu
/// (<c>ShowActionChoices</c>, reached after every PC action), the start of a turn after ongoing damage, the end of
/// every NPC turn and every turn boundary (<c>NextInitiativeTurn</c>). So whoever drops the last creature, a PC, an
/// AI-run party member, a summon or an enemy, the combat ends no later than the end of that turn.
/// Defeat opens the defeat screen; its New Party button returns to character creation.
/// Owner interpretations 2026-10-11 (CORE-038): a hostile creature charmed by or under the control of the party side is
/// out of the hostile side, and when both sides are out at once the dying PCs roll their stabilization checks first
/// (PHB p.145) and the party wins if any PC survives.
/// </summary>
public partial class GameManager
{
    /// <summary>True while the defeat screen waits for the player's choice; blocks world input.</summary>
    public bool WaitingForDefeatChoice { get; private set; }

    /// <summary>
    /// The shared combat-end check. Returns true when no combat is running any more: either the phase was already
    /// <see cref="TurnPhase.CombatOver"/>, or one side is now out of the fight and this call ended the combat (only the
    /// party side out is a defeat, only the hostile side out a victory). When both sides are out at once, the dying PCs
    /// first roll their stabilization checks until each is stable or dead (PHB p.145), and the party wins if any PC
    /// survives, else it is a defeat (owner interpretation 2026-10-11, CORE-038).
    /// Callers stop their resolution when it returns true.
    /// </summary>
    public bool EvaluateCombatEnd(string sourceContext)
    {
        if (CurrentPhase == TurnPhase.CombatOver)
            return true;

        CombatEndRules.SideCounts sides = GetCombatEndSides();
        if (sides.PlayersOut && sides.EnemiesOut)
        {
            if (ResolvePartyDyingAfterBothSidesOut(sourceContext))
                HandleCombatVictoryDetected(sourceContext);
            else
                HandleCombatDefeatDetected(sourceContext, GetCombatEndSides());
            return true;
        }

        if (sides.PlayersOut)
        {
            HandleCombatDefeatDetected(sourceContext, sides);
            return true;
        }

        if (sides.EnemiesOut)
        {
            HandleCombatVictoryDetected(sourceContext);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Both sides are out at once (owner interpretation 2026-10-11, CORE-038): every dying PC rolls its stabilization
    /// check round after round (PHB p.145: 10% on d% to become stable, else it loses 1 hit point; dead at -10) until it
    /// is stable or dead, as the fight would play out with nobody left standing. Returns whether any PC survives (is not
    /// dead): a stable, unconscious, asleep or petrified PC survives. Only the party's PCs count; allies and summons do
    /// not decide the fight.
    /// </summary>
    private bool ResolvePartyDyingAfterBothSidesOut(string sourceContext)
    {
        Debug.Log($"[CombatEnd] Both sides out at once | source={sourceContext}: resolving the PCs' dying rolls before deciding (CORE-038).");
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("⚖", "Both sides are down at once: the dying heroes roll to stabilize before the fight is decided."));

        bool anySurvivor = false;
        if (PCs == null)
            return false;

        for (int i = 0; i < PCs.Count; i++)
        {
            CharacterController pc = PCs[i];
            if (!CombatEndRules.IsActiveCombatant(pc) || pc.Team != CharacterTeam.Player)
                continue;

            // A dying character loses at most 9 more hit points before -10, so 10 checks always settle it.
            for (int roll = 0; roll < 10 && pc.CurrentHPState == HPState.Dying && !pc.IsDead && !pc.Stats.IsDead; roll++)
                ProcessEndOfTurnHPState(pc);

            if (!pc.IsDead && !pc.Stats.IsDead)
                anySurvivor = true;
        }

        Debug.Log($"[CombatEnd] Dying rolls resolved | source={sourceContext} | survivor={anySurvivor}");
        return anySurvivor;
    }

    /// <summary>
    /// A hostile creature charmed by or under the control of a party-side creature counts as out of the hostile side
    /// (owner interpretation 2026-10-11, CORE-038; <see cref="CombatEndRules.IsControlledByTeam"/>).
    /// </summary>
    private bool IsOutOfHostileSideByPartyControl(CharacterController c)
        => CombatEndRules.IsControlledByTeam(c, CharacterTeam.Player, GetActiveConditions(c));

    // Whether each side had an active member at some point in this combat: a side whose last creature was trapped,
    // destroyed or removed has no active member left but is still out (CombatEndRules.SideCounts).
    private bool _combatPartySideSeen;
    private bool _combatEnemySideSeen;

    /// <summary>Records which sides are present when a combat starts (called from StartCombat).</summary>
    private void RecordCombatSidesAtStart()
    {
        CombatEndRules.SideCounts sides = CombatEndRules.Count(GetAllCharacters());
        _combatPartySideSeen = sides.PlayersAll > 0;
        _combatEnemySideSeen = sides.EnemiesAll > 0;
    }

    /// <summary>Forgets the sides of the last combat (combat reset, new party).</summary>
    private void ClearCombatSidesSeen()
    {
        _combatPartySideSeen = false;
        _combatEnemySideSeen = false;
    }

    /// <summary>
    /// The per-side counts that <see cref="EvaluateCombatEnd"/> applies: <see cref="CombatEndRules.Count"/> over every
    /// combatant, plus whether each side had members earlier in this combat. Also read by the scenario harness.
    /// </summary>
    public CombatEndRules.SideCounts GetCombatEndSides()
    {
        CombatEndRules.SideCounts sides = CombatEndRules.Count(GetAllCharacters(), IsOutOfHostileSideByPartyControl);
        if (CurrentPhase != TurnPhase.CombatOver)
        {
            _combatPartySideSeen |= sides.PlayersAll > 0;
            _combatEnemySideSeen |= sides.EnemiesAll > 0;
        }
        sides.PlayersHadMembers = _combatPartySideSeen;
        sides.EnemiesHadMembers = _combatEnemySideSeen;
        return sides;
    }

    /// <summary>
    /// Registers <paramref name="defeatedTarget"/> for XP when it is out of the fight, then runs
    /// <see cref="EvaluateCombatEnd"/>. Kept for the callers that name the creature they dropped.
    /// </summary>
    private bool CheckCombatVictory(string sourceContext, CharacterController defeatedTarget = null)
    {
        RegisterDefeatedEnemyForXP(defeatedTarget, sourceContext);
        return EvaluateCombatEnd(sourceContext);
    }

    /// <summary>Hostile-side combatants still in the fight (for the "N enemies remain" log lines).</summary>
    private int GetAliveNPCCount()
        => GetCombatEndSides().EnemiesIn;

    /// <summary>
    /// Tracks <paramref name="character"/> for the encounter's XP when it is a hostile creature out of the fight. With
    /// <paramref name="countPartyControlled"/> (the victory snapshot only), a hostile creature charmed by or under the
    /// control of the party side counts too: the victory treats it as out of the hostile side (owner interpretation
    /// 2026-10-11, CORE-038), so the party overcame it in that battle (DMG p.36-37: XP for a challenge overcome, and a
    /// monster defeated in battle is one). It is not tracked during the fight, since the charm can still end.
    /// </summary>
    private void RegisterDefeatedEnemyForXP(CharacterController character, string sourceContext, bool countPartyControlled = false)
    {
        if (character == null || character.Stats == null)
            return;

        if (character.Team != CharacterTeam.Enemy)
            return;

        bool outOfFight = CombatEndRules.IsOutOfFight(character);
        if (!outOfFight && !(countPartyControlled && CombatEndRules.IsActiveCombatant(character) && IsOutOfHostileSideByPartyControl(character)))
            return;

        if (_defeatedEnemiesThisCombat.Contains(character))
            return;

        _defeatedEnemiesThisCombat.Add(character);
        string enemyName = string.IsNullOrWhiteSpace(character.Stats.CharacterName) ? "Unknown Enemy" : character.Stats.CharacterName;
        string cr = string.IsNullOrWhiteSpace(character.Stats.ChallengeRating) ? "—" : character.Stats.ChallengeRatingDisplay;
        Debug.Log($"[Combat] Enemy defeated tracked: {enemyName} (CR {cr}) | source={sourceContext}");
    }

    private void CaptureDefeatedEnemiesSnapshotForXP(string sourceContext, bool countPartyControlled = false)
    {
        if (NPCs == null)
            return;

        // Only creatures of this combat: a pool slot this encounter left unused is inactive but keeps the stats of the
        // creature it held before, which may be dead or dying, and would otherwise count for XP again.
        for (int i = 0; i < NPCs.Count; i++)
        {
            if (!CombatEndRules.IsActiveCombatant(NPCs[i]))
                continue;
            RegisterDefeatedEnemyForXP(NPCs[i], sourceContext, countPartyControlled);
        }

        Debug.Log($"[XP] Defeated enemy snapshot captured | source={sourceContext} | tracked={_defeatedEnemiesThisCombat.Count}");
    }

    public List<CharacterController> GetDefeatedEnemiesForXP()
    {
        return new List<CharacterController>(_defeatedEnemiesThisCombat);
    }

    private void HandleCombatVictoryDetected(string sourceContext)
    {
        Debug.Log($"[CombatEnd] Victory detected | source={sourceContext} | frame={Time.frameCount} | phaseBefore={CurrentPhase} | waitingLootBefore={WaitingForLootCollection}");

        CurrentPhase = TurnPhase.CombatOver;
        TurnDurations.ClearCurrentAnchor(); // effects created after the fight have no initiative count (CMB-006)
        CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
        CombatUI?.SetActionButtonsVisible(false);

        if (LootCollectionUI == null)
            Debug.LogWarning("[LootUI] LootCollectionUI reference is null before BeginPostCombatLootCollection. Initialization will be attempted.");
        if (PartyStash == null)
            Debug.LogWarning("[LootFlow] PartyStash is null before BeginPostCombatLootCollection. Initialization will be attempted.");

        CaptureDefeatedEnemiesSnapshotForXP($"{sourceContext}.Victory", countPartyControlled: true);
        BeginPostCombatLootCollection();
        Debug.Log($"[CombatEnd] Post-combat loot collection invoked | source={sourceContext} | waitingAfter={WaitingForLootCollection} | phaseAfter={CurrentPhase}");
    }

    /// <summary>
    /// Every party-side combatant is dead, dying or unconscious (and, when both sides went out at once, no PC survived
    /// its dying rolls, CORE-038): end the combat and open the defeat screen (CORE-001).
    /// </summary>
    private void HandleCombatDefeatDetected(string sourceContext, CombatEndRules.SideCounts sides)
    {
        Debug.Log($"[CombatEnd] Defeat detected | source={sourceContext} | frame={Time.frameCount} | phaseBefore={CurrentPhase} | party={sides.PlayersIn}/{sides.PlayersAll} in | enemies={sides.EnemiesIn}/{sides.EnemiesAll} in");

        CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("☠", "DEFEAT! Every hero is dead, dying or unconscious."));
        CurrentPhase = TurnPhase.CombatOver;
        TurnDurations.ClearCurrentAnchor(); // effects created after the fight have no initiative count (CMB-006)
        CombatUI?.SetTurnIndicator("DEFEAT! All heroes have fallen!");
        CombatUI?.SetActionButtonsVisible(false);

        ShowDefeatScreen();
    }

    private void ShowDefeatScreen()
    {
        if (CombatUI == null)
        {
            Debug.LogWarning("[CombatEnd] CombatUI is null; the defeat screen cannot be shown.");
            return;
        }

        WaitingForDefeatChoice = true;
        CombatUI.ShowDefeatPanel(
            "DEFEAT",
            "Every hero is dead, dying or unconscious.\nThe adventure is over for this party.",
            StartNewPartyAfterDefeat,
            QuitAfterDefeat);
    }

    /// <summary>Closes the defeat screen without acting on it (a world reset, a new combat).</summary>
    private void CloseDefeatScreen()
    {
        WaitingForDefeatChoice = false;
        CombatUI?.HideDefeatPanel();
    }

    /// <summary>
    /// The defeat screen's New Party button: clears the lost fight (combat state, enemies, summons, grapples, loot
    /// state), gives the new party fresh gold and a fresh stash, reactivates all four party slots as at game start
    /// and reopens character creation, whose completion runs the normal path (level-ups, then encounter selection).
    /// </summary>
    public void StartNewPartyAfterDefeat()
    {
        const string context = "Defeat.NewParty";
        Debug.Log("[CombatEnd] New party requested after defeat.");

        CloseDefeatScreen();
        ResetCombatStateForNextEncounter(context);
        LootCollectionUI?.Close(invokeClosedCallback: false);
        ResetPostCombatLootCollectionState(context);
        AIService.ClearAuraSaveImmunities();
        AISpellcastingStrategist.ResetCombatState();
        ClearAllMirrorImageEffects(context);
        MeleeReactionService.ClearAll();
        CurseTracker.ClearAll();
        _defeatedEnemiesThisCombat.Clear();

        // Grapple links are static and keyed by controller (CMB-038); end them before the summons go.
        var everyone = new List<CharacterController>();
        if (PCs != null) everyone.AddRange(PCs);
        if (NPCs != null) everyone.AddRange(NPCs);
        for (int i = 0; i < _activeSummons.Count; i++)
            if (_activeSummons[i]?.Controller != null)
                everyone.Add(_activeSummons[i].Controller);
        foreach (CharacterController cc in everyone)
            if (cc != null)
                cc.ReleaseGrappleState("new party after defeat");

        // Despawn summons as the post-combat rest does.
        for (int i = _activeSummons.Count - 1; i >= 0; i--)
        {
            CharacterController summon = _activeSummons[i]?.Controller;
            if (summon == null)
                continue;
            Grid?.ClearCreatureOccupancy(summon);
            Destroy(summon.gameObject);
        }
        _activeSummons.Clear();
        _summonedAllies.Clear();
        _summonedEnemies.Clear();

        // A new party starts with the starting gold, the default stash and empty loop statistics.
        _economyService?.ResetForNewParty(partyGold);
        EnsurePartyStashInitialized();
        CompletedCombatCount = 0;
        TotalLootItemsCollected = 0;
        TotalEncounterXPDefeated = 0;

        // All four slots are active at game start; a test preset may have deactivated some.
        CharacterController[] slots = { PC1, PC2, PC3, PC4 };
        GameObject[] panels = CombatUI != null
            ? new[] { CombatUI.PC1Panel, CombatUI.PC2Panel, CombatUI.PC3Panel, CombatUI.PC4Panel }
            : new GameObject[4];
        for (int i = 0; i < slots.Length; i++)
            SetPCActiveState(slots[i], true, panels[i]);

        if (CharacterCreationUI == null)
        {
            Debug.LogWarning("[CombatEnd] No character creation UI; keeping the current characters and opening encounter selection.");
            PromptEncounterSelection();
            return;
        }

        WaitingForCharacterCreation = true;
        CharacterCreationUI.OnCreationComplete = OnCharacterCreationComplete;
        CharacterCreationUI.OnCreationComplete4 = OnCharacterCreationComplete4;
        CharacterCreationUI.ReopenForNewParty();
    }

    /// <summary>The defeat screen's Quit button: the same exit as the loot screen's (stops Play mode or quits).</summary>
    private void QuitAfterDefeat()
    {
        CloseDefeatScreen();
        ExitCombatLoopToMenu();
    }
}
