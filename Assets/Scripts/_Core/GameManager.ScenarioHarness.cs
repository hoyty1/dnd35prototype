#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// GameManager partial class: scenario harness entry points (editor only).
///
/// Thin <c>Harness_*</c> wrappers that let the scenario harness (Assets/Scripts/Tests/Scenarios)
/// boot a party, spawn enemies at exact squares, start and halt combat, reset the world and drive
/// the PC menu callbacks. Being part of GameManager they reach its private state directly, with no
/// reflection (TST-006). Nothing in game code calls this partial, and the whole file is compiled
/// only in the editor, so player builds are unaffected. Pair with <see cref="ScenarioHooks"/>.
/// </summary>
public partial class GameManager
{
    /// <summary>True when the grid, the combat UI and the turn service exist (the scene finished bootstrapping).</summary>
    internal bool Harness_IsReady()
    {
        return Grid != null && CombatUI != null && _turnService != null;
    }

    /// <summary>
    /// Builds the party from creation data through the real <see cref="SetupCreatedCharacters"/> path
    /// without opening the level-up or encounter selection UI. Re-runnable in one Play session:
    /// SetupCreatedCharacters resets each slot that held a character through
    /// <see cref="ResetPCSlotForNewCharacter"/> (CRE-046), so nothing of an earlier run's character (spell
    /// effects, spellcasting component, feat toggles, tags, trackers, links) stays. Slots beyond the data
    /// are deactivated.
    /// Limitation: level-ups queued by creation (custom-rolled characters, or TargetLevel above the
    /// base level) are not applied, because the level-up UI is skipped. Returns null on success, or a
    /// reason (also logged) when there was no data or a slot was left with pending level-ups; the
    /// runner should mark such a run SetupFailed.
    /// </summary>
    internal string Harness_SetupPartyFromCreation(CharacterCreationData[] data)
    {
        if (data == null || data.Length == 0)
        {
            const string noData = "Harness_SetupPartyFromCreation called with no data.";
            Debug.LogWarning("[ScenarioHarness] " + noData);
            return noData;
        }

        CharacterController[] slots = { PC1, PC2, PC3, PC4 };

        WaitingForCharacterCreation = false;
        SetupCreatedCharacters(data);

        for (int i = 0; i < slots.Length; i++)
            SetPCActiveState(slots[i], i < data.Length, Harness_GetPCPanel(i));

        UpdateAllStatsUI();

        var pending = new List<string>();
        for (int i = 0; i < slots.Length && i < data.Length; i++)
        {
            CharacterStats st = slots[i] != null ? slots[i].Stats : null;
            if (st != null && st.PendingLevelUps > 0)
                pending.Add($"{st.CharacterName} ({st.PendingLevelUps})");
        }

        if (pending.Count == 0)
            return null;

        string reason = "pending level-ups not applied (the level-up UI is skipped): " + string.Join(", ", pending);
        Debug.LogWarning("[ScenarioHarness] " + reason);
        return reason;
    }

    /// <summary>
    /// Puts an exactly built character into PC slot 0-3 at <paramref name="pos"/>, the way the
    /// <c>Configure*TestParty</c> presets do (GameManager.TestConfigs.cs): Init, inventory with the
    /// class starting kit, RecalculateStats, status/concentration/spellcasting components, then the
    /// slot is activated. Set BAB through <c>CharacterStats.BaseAttackBonusOverride</c>; writes to
    /// <c>BaseAttackBonus</c> are ignored (CHR-068). A slot that held a character is reset first through
    /// <see cref="ResetPCSlotForNewCharacter"/> (CRE-046), so casters always get a fresh SpellcastingComponent
    /// and nothing of the earlier character stays.
    /// </summary>
    internal CharacterController Harness_SetupPartySlot(int slot, CharacterStats stats, Vector2Int pos)
    {
        CharacterController pc = Harness_GetPCSlot(slot);
        if (pc == null || stats == null)
        {
            Debug.LogWarning($"[ScenarioHarness] Harness_SetupPartySlot: no PC slot {slot} or no stats.");
            return null;
        }

        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");
        Sprite pcAlive = IconLoader.GetToken(stats.CharacterClass) ?? pcAliveFallback;

        // A previous run left another character on this slot: clear every trace of it (CRE-046).
        ResetPCSlotForNewCharacter(pc);

        pc.Init(stats, pos, pcAlive, pcDead);

        InventoryComponent inv = pc.gameObject.GetComponent<InventoryComponent>();
        if (inv == null)
            inv = pc.gameObject.AddComponent<InventoryComponent>();
        inv.Init(stats);
        SetupStartingEquipment(inv, stats.CharacterClass);
        inv.CharacterInventory.RecalculateStats();

        if (stats.IsSpellcaster)
        {
            SpellDatabase.Init();
            SpellcastingComponent spellComp = pc.gameObject.AddComponent<SpellcastingComponent>();
            spellComp.Init(stats);
        }

        StatusEffectManager statusMgr = pc.StatusEffectManager;
        if (statusMgr == null)
            statusMgr = pc.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        ConcentrationManager concMgr = pc.Concentration;
        if (concMgr == null)
            concMgr = pc.gameObject.AddComponent<ConcentrationManager>();
        concMgr.Init(stats, pc);

        SetPCActiveState(pc, true, Harness_GetPCPanel(slot));
        UpdateAllStatsUI();
        return pc;
    }

    /// <summary>Deactivates PC slot 0-3 and its panel (an unused party slot).</summary>
    internal void Harness_DeactivatePartySlot(int slot)
    {
        CharacterController pc = Harness_GetPCSlot(slot);
        if (pc == null)
            return;

        Grid?.ClearCreatureOccupancy(pc);
        SetPCActiveState(pc, false, Harness_GetPCPanel(slot));
    }

    /// <summary>
    /// Spawns <paramref name="ids"/> into the NPC pool at <paramref name="positions"/> (index-parallel), as a custom
    /// encounter with every <c>_is*TestEncounter</c> preset flag cleared. The pool grows to the number of ids
    /// (ENC-001). Each creature goes through the game's spawn placement (EncounterSpawnPlacement): a position whose
    /// footprint fits is kept, one that does not gives way to the nearest square that does, and a missing or negative
    /// position gets the game's default layout (the harness's game-placed actors).
    /// </summary>
    internal void Harness_SpawnEnemies(List<string> ids, Vector2Int[] positions)
    {
        // Same flag list as ApplyRandomEncounter: no test preset may steer placement or setup.
        _isGrappleTestEncounter = false;
        _isGreaseTestEncounter = false;
        _isFeintSneakTestEncounter = false;
        _isTurnUndeadTestEncounter = false;
        _isArmorTargetingTestEncounter = false;
        _isTigerHuntTestEncounter = false;
        _isOgreBattleTestEncounter = false;
        _isShieldBashTestEncounter = false;
        _isCelestialTemplateTestEncounter = false;
        _isFiendishTemplateTestEncounter = false;
        _isSummonMonsterTestEncounter = false;
        _isNpcMagicMissileTestEncounter = false;
        _isProtectionFromEvilTestEncounter = false;
        _isWindDispersionTestEncounter = false;
        _isObscuringMistRangedOnlyTestEncounter = false;
        _isDisruptUndeadTestEncounter = false;
        _isTrueStrikeTestEncounter = false;
        _isWizardSpellTestEncounter = false;
        _isClericSpellTestEncounter = false;
        _isCharmPersonTestEncounter = false;
        _isSleepSpellTestEncounter = false;
        _isMirrorImageTestEncounter = false;

        _selectedEncounterPresetId = "scenario_harness";
        _activeEncounterEnemyIds.Clear();
        if (ids != null)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(ids[i]))
                    _activeEncounterEnemyIds.Add(ids[i]);
            }
        }

        _isCustomEncounter = true;
        _customEncounterSpawnPositions = positions != null ? (Vector2Int[])positions.Clone() : null;

        SetupEnemyEncounter(_activeEncounterEnemyIds);
        SetupNPCIcons();
        UpdateAllStatsUI();
    }

    /// <summary>
    /// Starts combat the way the pre-combat hub's Start button does (ForceStartEncounterFromPreCombat),
    /// without the unprepared-caster dialog and with the encounter selection UI closed.
    /// </summary>
    internal void Harness_StartCombat()
    {
        EncounterSelectionUI?.Close();
        WaitingForEncounterSelection = false;
        WaitingForCharacterCreation = false;
        ForceStartEncounterFromPreCombat("ScenarioHarness");
    }

    /// <summary>
    /// Stops combat at once without the end-of-combat callbacks (no XP, loot or rest). Every coroutine
    /// on GameManager and TurnService is stopped first, so an NPC turn in progress cannot reach
    /// NextInitiativeTurn after the halt; the harness must therefore run on its own MonoBehaviour.
    /// EndTurn is a no-op afterwards, so a running skip chain unwinds (CORE-012). StartPCTurn can still
    /// set the phase again in the same frame, so callers re-check the phase on the next frame.
    /// Follow a halt with <see cref="Harness_ResetWorld"/> before reading results, and ignore events
    /// that arrive after the halt.
    /// </summary>
    internal void Harness_HaltCombat(string reason)
    {
        StopAllCoroutines();
        _turnService?.StopAllCoroutines();
        _turnService?.ForceResetWithoutCallbacks($"ScenarioHarness.Halt:{reason}");
        ClearCounterTripPrompt(hideDialog: true);
        ClearGrappleEscapeOppositionPrompt();
        ClearOverrunResponsePrompt();
        ClearOverrunChoiceState();
        CurrentPhase = TurnPhase.CombatOver;
    }

    /// <summary>
    /// Returns the world to a clean pre-encounter state: combat reset, loot window closed, static
    /// per-combat state cleared, summons despawned (removed from NPCs and the parallel AI behaviour
    /// list, AI-015), the defeated-enemy tracker cleared, every party and pool grapple released (the
    /// static grapple links outlive a fight, CMB-038) and every NPC pool slot deactivated.
    /// Each step runs on its own, so one that throws does not skip the rest. Returns null when every
    /// step succeeded, else the failed steps; the harness should then record a dirty reset and start a
    /// fresh Play session. Summon GameObjects are removed with a deferred Destroy, so check that the
    /// world is clean on the next frame. Party HP, spells and conditions are not restored; call
    /// <see cref="Harness_RestorePartyAfterCombat"/>.
    /// </summary>
    internal string Harness_ResetWorld(string ctx)
    {
        string context = $"ScenarioHarness.Reset:{ctx}";
        var errors = new List<string>();

        void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                errors.Add(name + ": " + ex.GetType().Name + ": " + ex.Message);
                Debug.LogException(ex);
            }
        }

        Step("ResetCombatStateForNextEncounter", () => ResetCombatStateForNextEncounter(context));
        Step("LootCollectionUI.Close", () => LootCollectionUI?.Close(invokeClosedCallback: false));
        Step("ResetPostCombatLootCollectionState", () => ResetPostCombatLootCollectionState(context));
        Step("AIService.ClearAuraSaveImmunities", AIService.ClearAuraSaveImmunities);
        Step("AISpellcastingStrategist.ResetCombatState", AISpellcastingStrategist.ResetCombatState);
        Step("ClearAllMirrorImageEffects", () => ClearAllMirrorImageEffects(context));
        Step("ClearAllActiveGreaseEffects", ClearAllActiveGreaseEffects);

        var summons = new List<CharacterController>();
        Step("CollectSummons", () =>
        {
            for (int i = 0; i < _activeSummons.Count; i++)
            {
                CharacterController summon = _activeSummons[i]?.Controller;
                if (summon != null && !summons.Contains(summon))
                    summons.Add(summon);
            }
            foreach (CharacterController summon in _summonedAllies)
                if (summon != null && !summons.Contains(summon))
                    summons.Add(summon);
            foreach (CharacterController summon in _summonedEnemies)
                if (summon != null && !summons.Contains(summon))
                    summons.Add(summon);
        });
        // Grapple links are static and keyed by controller, and a halted fight never ends them (CMB-038):
        // without this the reused party and pool controllers would start the next job still grappling.
        // It runs before the summons are destroyed, so a link that involves a summon is ended too.
        Step("ReleaseGrapples", () =>
        {
            var everyone = new List<CharacterController>(summons);
            if (PCs != null) everyone.AddRange(PCs);
            if (NPCs != null) everyone.AddRange(NPCs);
            foreach (CharacterController cc in everyone)
                if (cc != null)
                    cc.ReleaseGrappleState("ScenarioHarness reset");
        });
        Step("ClearSummonLists", () =>
        {
            _activeSummons.Clear();
            _summonedAllies.Clear();
            _summonedEnemies.Clear();
        });
        foreach (CharacterController summon in summons)
            Step("RemoveSummon", () => Harness_RemoveSpawnedController(summon));

        Step("ClearDefeatedEnemies", () => _defeatedEnemiesThisCombat.Clear());

        if (NPCs != null)
        {
            for (int i = 0; i < NPCs.Count; i++)
            {
                CharacterController npc = NPCs[i];
                if (npc == null)
                    continue;

                Step("DeactivateNPC " + i, () =>
                {
                    Grid?.ClearCreatureOccupancy(npc);
                    if (npc.gameObject != null)
                        npc.gameObject.SetActive(false);
                });
            }
        }

        if (errors.Count == 0)
            return null;

        string result = string.Join("; ", errors);
        Debug.LogWarning("[ScenarioHarness] Dirty reset (" + ctx + "): " + result);
        return result;
    }

    /// <summary>The end-of-combat party rest (GameManager.RestorePartyAfterCombat).</summary>
    internal void Harness_RestorePartyAfterCombat()
    {
        RestorePartyAfterCombat();
    }

    /// <summary>One line per piece of turn and prompt state, for soft-lock diagnosis.</summary>
    internal string Harness_DumpState()
    {
        CharacterController current = CurrentCharacter;
        var sb = new StringBuilder();
        sb.Append("phase=").Append(CurrentPhase);
        sb.Append(" subPhase=").Append(CurrentSubPhase);
        sb.Append(" current=").Append(current != null && current.Stats != null ? current.Stats.CharacterName : "none");
        sb.Append(" combatActive=").Append(_turnService != null && _turnService.IsCombatActive);
        sb.Append(" round=").Append(_turnService != null ? _turnService.CurrentRound : 0);
        sb.Append(" waitingCreation=").Append(WaitingForCharacterCreation);
        sb.Append(" waitingEncounter=").Append(WaitingForEncounterSelection);
        sb.Append(" waitingPreCombat=").Append(WaitingForPreCombatInventory);
        sb.Append(" waitingLoot=").Append(WaitingForLootCollection);
        sb.Append(" waitingAoO=").Append(_waitingForAoOConfirmation);
        sb.Append(" pendingAoO=").Append(_pendingAoOAction != null ? _pendingAoOAction.ActionName : "none");
        sb.Append(" awaitingRangedRetarget=").Append(_isAwaitingRangedRetargetSelection);
        sb.Append(" awaitingFullAttack5ft=").Append(_isAwaitingFullAttackFiveFootStepSelection);
        sb.Append(" selectingSpecial=").Append(_isSelectingSpecialAttack);
        sb.Append(" pendingSpell=").Append(_pendingSpell != null ? _pendingSpell.SpellId : "none");
        return sb.ToString();
    }

    /// <summary>
    /// The PC prompt or selection the game is waiting on, or null when none is open: "aoo" (the AoO
    /// confirmation), "ranged-retarget", "full-attack-5ft", "submenu" (a special-style selection
    /// menu) or "touch-spell" (Cast Now / Discharge Later; the Ui Cast step answers it with
    /// <see cref="Harness_AnswerTouchSpellPromptCastNow"/>). Other modal prompts (bull rush push and
    /// follow, Improved Grab, the disarm item choice) are not listed; a scripted UI step that never
    /// settles reports them through <see cref="Harness_DumpState"/> and the open UI scan.
    /// </summary>
    internal string Harness_PendingPrompt()
    {
        if (_counterTripPromptOpen)
            return "counter-trip";
        if (_overrunResponsePromptAnswer != null)
            return "overrun-response";
        if (_overrunChoicePending)
            return "overrun-choice";
        if (CombatUI != null && CombatUI.Harness_IsTouchSpellPromptOpen())
            return "touch-spell";
        if (_waitingForAoOConfirmation)
            return "aoo";
        if (_isAwaitingRangedRetargetSelection)
            return "ranged-retarget";
        if (_isAwaitingFullAttackFiveFootStepSelection)
            return "full-attack-5ft";
        if (CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen())
            return "submenu";
        return null;
    }

    /// <summary>
    /// Leaves a target or destination selection and returns to the action menu, as cancelling a
    /// selection does (ShowActionChoices). Used when a scripted UI click picked no valid square.
    /// </summary>
    internal void Harness_CancelToActionChoices()
    {
        CombatUI?.HideSpecialAttackMenu();
        ShowActionChoices();
    }

    /// <summary>
    /// Presses the Special Attack button for the active PC and reports whether the menu is open
    /// afterwards (the selecting flag set and the panel shown). Writes no game state of its own: a
    /// selecting flag already set before the press (left by an earlier selection) is reported in
    /// <paramref name="staleFlag"/>, not cleared, so the game's own guards still see it.
    /// </summary>
    internal bool Harness_OpenSpecialAttackMenu(out string staleFlag)
    {
        staleFlag = _isSelectingSpecialAttack ? "the special-attack selecting flag was already set before the press" : null;
        OnSpecialAttackButtonPressed();
        return _isSelectingSpecialAttack && CombatUI != null && CombatUI.Harness_IsSpecialAttackMenuOpen();
    }

    /// <summary>
    /// Clicks the open Special Attack menu's button for <paramref name="type"/> (its onClick, as a
    /// player's click does). False, with <paramref name="why"/>, when the menu has no such button or
    /// the button is hidden or not interactable, so a scripted step cannot choose what a player could not.
    /// </summary>
    internal bool Harness_PressSpecialAttackButton(SpecialAttackType type, bool offHand, out string why)
    {
        string name = Harness_SpecialAttackButtonName(type, offHand);
        if (name == null)
        {
            why = "the Special Attack menu has no button for " + type + (offHand ? " (off-hand)" : "");
            return false;
        }

        UnityEngine.UI.Button button = CombatUI != null ? CombatUI.Harness_FindSpecialAttackButton(name) : null;
        if (button == null)
        {
            why = "no '" + name + "' button in the open Special Attack menu";
            return false;
        }
        if (!button.gameObject.activeInHierarchy || !button.interactable)
        {
            why = "the '" + name + "' button is " + (button.gameObject.activeInHierarchy ? "disabled" : "hidden");
            return false;
        }

        why = null;
        button.onClick.Invoke();
        return true;
    }

    /// <summary>The Special Attack menu button name for a type (the names CombatUI.WireSpecialAttackMenu wires), or null.</summary>
    private static string Harness_SpecialAttackButtonName(SpecialAttackType type, bool offHand)
    {
        switch (type)
        {
            case SpecialAttackType.Trip: return offHand ? null : "Trip";
            case SpecialAttackType.Disarm: return offHand ? "Disarm (Off-Hand)" : "Disarm";
            case SpecialAttackType.Grapple: return offHand ? null : "Grapple";
            case SpecialAttackType.Sunder: return offHand ? "Sunder (Off-Hand)" : "Sunder";
            case SpecialAttackType.BullRushAttack: return offHand ? null : "Bull Rush (Standard)";
            case SpecialAttackType.BullRushCharge: return offHand ? null : "Bull Rush (Charge)";
            case SpecialAttackType.Overrun: return offHand ? null : "Overrun";
            case SpecialAttackType.Feint: return offHand ? null : "Feint";
            case SpecialAttackType.CoupDeGrace: return offHand ? null : "Coup de Grace";
            case SpecialAttackType.AidAnother: return offHand ? null : "Aid Another";
            default: return null;
        }
    }

    /// <summary>The Special Attack menu callback, as if the player picked <paramref name="type"/>.</summary>
    internal void Harness_SelectSpecialAttack(SpecialAttackType type, bool offHand)
    {
        OnSpecialAttackSelected(type, offHand);
    }

    /// <summary>
    /// The cursor position, in grid units (a square's centre is its coordinates), that the area targeting reads while it is
    /// set (GetMouseWorldPosition), so a scripted click aims where the scenario says rather than wherever the editor's
    /// cursor is; null in play. The scenario steps set it to the clicked square's centre around each click (batch 11 step
    /// 24: a burst then originates at that square's north-east corner, a cone or line starts at the caster's corner nearest
    /// it). Writes no game state.
    /// </summary>
    internal Vector2? Harness_AimPoint;

    /// <summary>
    /// The PC area aim rule (TryBuildUiAreaAim, the rule the cursor's preview and the click share; batch 11 step 24) for
    /// <paramref name="caster"/> casting <paramref name="spell"/> at <paramref name="gridPoint"/> (grid units), for the
    /// static tests. Writes no game state.
    /// </summary>
    internal bool Harness_BuildUiAreaAim(CharacterController caster, SpellData spell, Vector2 gridPoint,
        out HashSet<Vector2Int> cells, out GridIntersection? origin)
        => TryBuildUiAreaAim(caster, spell, gridPoint, out cells, out origin);

    /// <summary>
    /// The Legality stage's area line of effect filter (FilterAreaAimByLineOfEffect: the aim's squares and creatures that
    /// its recorded origin cannot reach are dropped, in place) on <paramref name="req"/>, for the static tests (batch 11
    /// step 24, SPL-006). Changes only the request.
    /// </summary>
    internal static void Harness_FilterAreaAimByLineOfEffect(SpellCastRequest req) => FilterAreaAimByLineOfEffect(req);

    /// <summary>The spell menu callback, as if the player picked <paramref name="spell"/> with <paramref name="metamagic"/>.</summary>
    internal void Harness_SelectSpell(SpellData spell, MetamagicData metamagic)
    {
        OnSpellSelectedWithMetamagic(spell, metamagic);
    }

    /// <summary>
    /// The spell menu's Convert button for a cleric's prepared <paramref name="sacrificedSpellId"/> of
    /// <paramref name="level"/>, then the metamagic panel's choice (<paramref name="metamagic"/>, null for none), as if the
    /// player picked them: the cure or inflict spell of that level goes to the spell callback as a spontaneous conversion
    /// (CombatUI.BeginSpontaneousConversion; batch 11 step 21). False when the caster has no such spell to convert.
    /// </summary>
    internal bool Harness_SelectSpontaneousConversion(CharacterController caster, int level, string sacrificedSpellId, MetamagicData metamagic)
    {
        SpellcastingComponent sc = caster != null ? caster.Spellcasting : null;
        SpellData sacrificed = SpellDatabase.GetSpell(sacrificedSpellId);
        SpellData converted = sc != null ? sc.GetSpontaneousSpell(level) : null;
        if (CombatUI == null || sacrificed == null || converted == null || !sc.CanConvertSpellToSpontaneous(sacrificed))
            return false;
        CombatUI.BeginSpontaneousConversion(level, sacrificedSpellId);
        OnSpellSelectedWithMetamagic(converted, metamagic);
        return true;
    }

    /// <summary>
    /// Sets the current cast's spell and metamagic directly, with no menu, targeting or other side effect (TST-006: replaces
    /// reflection on <c>_pendingSpell</c> and <c>_pendingMetamagic</c>, which are read-only views of the current cast's
    /// request since batch 11 step 29). Pass a clone, never a database template. With no cast open, a spell opens a scratch
    /// Ui cast (CastForWrite); a null spell ends the open Ui cast (EndPendingCast). Read the old values first with
    /// <see cref="Harness_GetPendingSpell"/> and <see cref="Harness_GetPendingMetamagic"/> and put them back afterwards.
    /// </summary>
    internal void Harness_SetPendingCast(SpellData spell, MetamagicData metamagic)
    {
        if (spell == null)
            EndPendingCast();
        else
            CastForWrite(false).Request.Spell = spell;
        CastContext open = CastForWrite(metamagic == null);
        if (open != null)
            open.Request.Metamagic = metamagic;
    }

    /// <summary>The pending cast's spell, or null.</summary>
    internal SpellData Harness_GetPendingSpell() => _pendingSpell;

    /// <summary>The pending cast's metamagic, or null.</summary>
    internal MetamagicData Harness_GetPendingMetamagic() => _pendingMetamagic;

    /// <summary>The number of open casts on the per-cast context stack (batch 11 step 4).</summary>
    internal int Harness_OpenCastCount => _castStack.Count;

    /// <summary>The request of the cast on top of the per-cast context stack, or null (batch 11 step 4).</summary>
    internal SpellCastRequest Harness_CurrentCastRequest => CurrentCast?.Request;

    /// <summary>The last combat log a cast or attack kept (_lastCombatLog; the UI decider writes it after a cast, the cast core never).</summary>
    internal string Harness_LastCombatLog => _lastCombatLog;

    /// <summary>
    /// The resource snapshot a cast keeps for a cancel at the AoO prompt (_pendingSpellcastSnapshot); a test sets one to
    /// check that a nested AI cast (RunCastSync) leaves it as it was (batch 11 step 8).
    /// </summary>
    internal CastResourceSnapshot Harness_PendingSpellcastSnapshot
    {
        get => _pendingSpellcastSnapshot;
        set => _pendingSpellcastSnapshot = value;
    }

    /// <summary>Ends every open cast, as the combat reset does (a test's cleanup).</summary>
    internal void Harness_EndAllCasts(string reason) => EndAllCasts(reason);

    /// <summary>
    /// Right-click or Escape, as the player backs out of the open selection (HandleInputCancelRequested: a spell's target
    /// selection, an area preview, a move and the like). False when nothing was open to cancel.
    /// </summary>
    internal bool Harness_CancelSelection() => HandleInputCancelRequested(null);

    /// <summary>
    /// Confirms an open self-centered area preview (Bless, Prayer: "Left-click to confirm"), as the player's left click
    /// does (HandleInputModeLeftClick); false when no such preview is open. Writes no game state of its own.
    /// </summary>
    internal bool Harness_ConfirmSelfAoE()
    {
        if (CurrentSubPhase != PlayerSubPhase.ConfirmingSelfAoE)
            return false;
        // The corner is the scenario's, not wherever the editor's cursor left it (UpdateSelfAoEPreview reads the real mouse
        // every frame): the caster's creature-centred origin nearest Harness_AimPoint when set, else the default (north-east)
        // one, so a scripted Bless, Bane or Prayer always originates at the same corner (batch 11 step 24 review).
        CharacterController caster = ActivePC;
        if (caster != null && _pendingSpell != null && Grid != null)
        {
            List<GridIntersection> origins = AoESystem.GetCreatureCentredOrigins(caster);
            if (origins.Count > 0)
            {
                GridIntersection origin = Harness_AimPoint.HasValue ? AoESystem.NearestOf(origins, Harness_AimPoint.Value) : origins[0];
                ShowSelfAoEPreview(caster, _pendingSpell, origin);
            }
        }
        OnSelfAoEConfirmed();
        return true;
    }

    /// <summary>
    /// Clicks the open Wild Shape form list's option for the bestiary entry <paramref name="formId"/> (its label begins
    /// with the entry's name, GameManager.DescribeWildShapeForm). False when the list is not open or lacks that form.
    /// </summary>
    internal bool Harness_ChooseWildShapeForm(string formId)
    {
        NPCDefinition form = NPCDatabase.Get(formId);
        if (form == null || CombatUI == null)
            return false;
        UnityEngine.UI.Button option = CombatUI.Harness_FindSelectionOption(form.Name + " (");
        if (option == null || !option.interactable)
            return false;
        option.onClick.Invoke();
        return true;
    }

    /// <summary>
    /// Clicks the open selection list's option whose label starts with <paramref name="labelPrefix"/> (the Bardic Music
    /// lists, CHR-026). False when the list is not open or has no such option.
    /// </summary>
    internal bool Harness_ChooseSelectionOption(string labelPrefix)
    {
        UnityEngine.UI.Button option = CombatUI != null ? CombatUI.Harness_FindSelectionOption(labelPrefix) : null;
        if (option == null || !option.interactable)
            return false;
        option.onClick.Invoke();
        return true;
    }

    /// <summary>
    /// Clicks the open selection list's first option (the Ui CastChoosing step's "*" answer, for example the only weapon
    /// Magic Weapon offers). False when no list is open or its first option is not interactable.
    /// </summary>
    internal bool Harness_ChooseFirstSelectionOption()
    {
        UnityEngine.UI.Button option = CombatUI != null ? CombatUI.Harness_FindFirstSelectionOption() : null;
        if (option == null || !option.interactable)
            return false;
        option.onClick.Invoke();
        return true;
    }

    /// <summary>
    /// Presses Discharge Later on the open touch-spell prompt, as a player clicks it (the caster then holds the charge
    /// through the cast core, BeginHoldTouchCharge); false when no prompt or button is open. Writes no game state of its own.
    /// </summary>
    internal bool Harness_AnswerTouchSpellPromptDischargeLater()
    {
        UnityEngine.UI.Button later = CombatUI != null ? CombatUI.Harness_FindTouchSpellPromptButton("DischargeLater") : null;
        if (later == null && CombatUI != null)
            later = CombatUI.Harness_FindTouchSpellPromptButton("Discharge Later");
        if (later == null || !later.interactable)
            return false;
        later.onClick.Invoke();
        return true;
    }

    /// <summary>
    /// Presses Cast Now on the open touch-spell prompt (a melee touch spell's Cast Now / Discharge Later choice), as a
    /// player clicks it; false when no prompt or button is open. Writes no game state of its own.
    /// </summary>
    internal bool Harness_AnswerTouchSpellPromptCastNow()
    {
        UnityEngine.UI.Button castNow = CombatUI != null ? CombatUI.Harness_FindTouchSpellPromptButton("Cast Now") : null;
        if (castNow == null && CombatUI != null)
            castNow = CombatUI.Harness_FindTouchSpellPromptButton("CastNow");
        if (castNow == null || !castNow.interactable)
            return false;
        castNow.onClick.Invoke();
        return true;
    }

    /// <summary>
    /// Applies a buff spell's effect exactly as a landed cast does (<c>ApplySpellBuff</c>, the shared PC and NPC
    /// effect step), without the slot, action and targeting steps around it. Pass a clone, never a database
    /// template (CLAUDE.md). Used by rules scenarios that need a spell effect on an actor that has no slot for it.
    /// <paramref name="castResult"/> stands in for the SpellCaster.Cast result of a landed single-target cast (its
    /// spell resistance and save already rolled); null makes the handlers that need one roll their own (SPL-037).
    /// </summary>
    internal ActiveSpellEffect Harness_ApplySpellBuff(CharacterController caster, CharacterController target, SpellData spell, SpellResult castResult = null)
    {
        if (target == null || spell == null)
            return null;
        return ApplySpellBuff(caster ?? target, target, spell, (caster ?? target).Spellcasting, castResult);
    }

    /// <summary>
    /// Runs the single-target handler registered for <paramref name="spell"/> (the registry's Handlers step,
    /// RunSingleTargetHandlers) as for a landed cast, without the slot, action and targeting steps around it. Pass a
    /// clone, never a database template. Used by rules scenarios that need a handler's effect (Lesser Globe of
    /// Invulnerability's emanation) from an actor that has no slot the cast path can spend for it (SPL-041).
    /// </summary>
    internal bool Harness_RunSingleTargetHandler(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (caster == null || spell == null)
            return false;
        var landed = new SpellResult { Spell = spell, Success = true, AttackHit = true };
        return RunSingleTargetHandlers(caster, target ?? caster, spell, landed, false, out _);
    }

    /// <summary>The AoO confirmation the player is being asked to answer, or null.</summary>
    internal AoOProvokingActionInfo Harness_PendingAoO
        => _waitingForAoOConfirmation ? _pendingAoOAction : null;

    /// <summary>The defender whose counter-trip prompt is open (PHB p.158, CMB-079), else null.</summary>
    internal CharacterController Harness_PendingCounterTripDefender
        => _counterTripPromptOpen ? _counterTripPromptDefender : null;

    /// <summary>
    /// Answers the open counter-trip prompt exactly as its buttons do: true = Trip Back, false = Decline.
    /// Returns false when no prompt is open.
    /// </summary>
    internal bool Harness_AnswerCounterTrip(bool tripBack)
    {
        if (!_counterTripPromptOpen)
            return false;

        CombatUI?.HideConfirmationDialog();
        AnswerCounterTripPrompt(tripBack);
        return true;
    }

    /// <summary>The grappler whose escape-opposition prompt is open (PHB p.156, D10), else null.</summary>
    internal CharacterController Harness_PendingEscapeOppositionGrappler
        => _escapeOppositionPromptAnswer != null ? _escapeOppositionPromptGrappler : null;

    /// <summary>
    /// Answers the open escape-opposition prompt exactly as its buttons do: true = Oppose Escape, false = Allow Escape.
    /// Returns false when no prompt is open.
    /// </summary>
    internal bool Harness_AnswerEscapeOpposition(bool oppose)
    {
        if (_escapeOppositionPromptAnswer == null)
            return false;

        CombatUI?.HideConfirmationDialog();
        AnswerGrappleEscapeOppositionPrompt(oppose);
        return true;
    }

    /// <summary>The defender whose overrun avoid-or-block prompt is open (PHB p.157, D11), else null.</summary>
    internal CharacterController Harness_PendingOverrunResponseDefender
        => _overrunResponsePromptAnswer != null ? _overrunResponsePromptDefender : null;

    /// <summary>
    /// Answers the open overrun prompt exactly as its buttons do: true = Block, false = Avoid. Returns false when no
    /// prompt is open.
    /// </summary>
    internal bool Harness_AnswerOverrunResponse(bool block)
    {
        if (_overrunResponsePromptAnswer == null)
            return false;

        CombatUI?.HideConfirmationDialog();
        AnswerOverrunResponsePrompt(block);
        return true;
    }

    /// <summary>
    /// Answers the PC's open "Overrun or Normal Move" dialog exactly as its buttons do: true = Overrun, false = Normal Move.
    /// Returns false when it is not open.
    /// </summary>
    internal bool Harness_AnswerOverrunChoice(bool overrun)
    {
        if (!_overrunChoicePending)
            return false;

        CombatUI?.HideConfirmationDialog();
        AnswerOverrunChoice(overrun);
        return true;
    }

    /// <summary>
    /// Answers the pending AoO confirmation exactly as the panel buttons do: 0 = proceed (cast
    /// normally), 1 = cast defensively (spells only), 2 = cancel. Returns false when nothing is pending
    /// or the choice is not offered.
    /// </summary>
    internal bool Harness_AnswerAoO(int choice)
    {
        AoOProvokingActionInfo info = Harness_PendingAoO;
        if (info == null)
            return false;

        System.Action answer;
        switch (choice)
        {
            case 0:
                answer = info.OnProceed;
                break;
            case 1:
                if (info.ActionType != AoOProvokingAction.CastSpell)
                    return false;
                answer = info.OnCastDefensively;
                break;
            case 2:
                answer = info.OnCancel;
                break;
            default:
                return false;
        }

        CombatUI?.HideAoOConfirmationPrompt();
        answer?.Invoke();
        return true;
    }

    private CharacterController Harness_GetPCSlot(int slot)
    {
        switch (slot)
        {
            case 0: return PC1;
            case 1: return PC2;
            case 2: return PC3;
            case 3: return PC4;
            default: return null;
        }
    }

    private GameObject Harness_GetPCPanel(int slot)
    {
        if (CombatUI == null)
            return null;

        switch (slot)
        {
            case 0: return CombatUI.PC1Panel;
            case 1: return CombatUI.PC2Panel;
            case 2: return CombatUI.PC3Panel;
            case 3: return CombatUI.PC4Panel;
            default: return null;
        }
    }

    /// <summary>
    /// The game's summon spawn, <see cref="SpawnSummonedCreature"/>, the one path a PC's and an NPC's Summon Monster or
    /// Summon Nature's Ally takes: template, runtime id and name, tags, team, alignment and Augment Summoning. The creature
    /// joins <see cref="NPCs"/> and the summon trackers but not the initiative order or the active summon list (the cast
    /// pipeline adds those). Pair with <see cref="Harness_DespawnSummon"/>.
    /// </summary>
    internal CharacterController Harness_SpawnSummonedCreature(CharacterController caster, Vector2Int cell, SummonMonsterOption option)
    {
        return SpawnSummonedCreature(caster, cell, option);
    }

    /// <summary>
    /// Takes a summon made by <see cref="Harness_SpawnSummonedCreature"/> out of play at once: off the grid, out of the
    /// summon trackers, the initiative order, <see cref="NPCs"/> and the parallel AI behaviour list (AI-015), then a
    /// deferred Destroy.
    /// </summary>
    internal void Harness_DespawnSummon(CharacterController summon)
    {
        if (summon == null)
            return;

        _activeSummons.RemoveAll(s => s != null && s.Controller == summon);
        _summonedAllies.Remove(summon);
        _summonedEnemies.Remove(summon);
        _turnService?.RemoveFromInitiative(summon);
        Harness_RemoveSpawnedController(summon);
    }

    private void Harness_RemoveSpawnedController(CharacterController cc)
    {
        if (cc == null)
            return;

        Grid?.ClearCreatureOccupancy(cc);
        int npcIdx = NPCs.IndexOf(cc);
        if (npcIdx >= 0)
        {
            NPCs.RemoveAt(npcIdx);
            if (npcIdx < _npcAIBehaviors.Count)
                _npcAIBehaviors.RemoveAt(npcIdx);
        }

        if (cc.gameObject != null)
            Destroy(cc.gameObject);
    }

}
#endif
