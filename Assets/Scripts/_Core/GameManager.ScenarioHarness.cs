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
    /// without opening the level-up or encounter selection UI. Re-runnable in one Play session: the
    /// slot's spell effects and concentration from an earlier run are removed while still bound to the
    /// old stats, and SetupCreatedCharacters adds InventoryComponent and SpellcastingComponent
    /// unconditionally, so existing ones are destroyed first. Slots beyond the data are deactivated.
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
        for (int i = 0; i < slots.Length && i < data.Length; i++)
        {
            CharacterController pc = slots[i];
            if (pc == null)
                continue;

            Harness_ClearSlotSpellState(pc);
            DestroyAllComponents<InventoryComponent>(pc.gameObject);
        }

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
    /// <c>BaseAttackBonus</c> are ignored (CHR-068). Spell effects and concentration left on the slot by
    /// an earlier run are removed first, and casters always get a fresh SpellcastingComponent.
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

        // A previous run may have left spell effects, concentration and a spellcasting component
        // (with its buffs, spellbook and preparation lists) on this slot.
        Harness_ClearSlotSpellState(pc);

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
    /// Spawns <paramref name="ids"/> into the NPC pool at exactly <paramref name="positions"/>
    /// (index-parallel; a missing position falls back to the default spawn square), as a custom
    /// encounter with every <c>_is*TestEncounter</c> preset flag cleared.
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
    /// set the phase again in the same frame (CORE-003), so callers re-check the phase on the next frame.
    /// Follow a halt with <see cref="Harness_ResetWorld"/> before reading results, and ignore events
    /// that arrive after the halt.
    /// </summary>
    internal void Harness_HaltCombat(string reason)
    {
        StopAllCoroutines();
        _turnService?.StopAllCoroutines();
        _turnService?.ForceResetWithoutCallbacks($"ScenarioHarness.Halt:{reason}");
        CurrentPhase = TurnPhase.CombatOver;
    }

    /// <summary>
    /// Returns the world to a clean pre-encounter state: combat reset, loot window closed, static
    /// per-combat state cleared, summons despawned (removed from NPCs and the parallel AI behaviour
    /// list, AI-015), the defeated-enemy tracker cleared and every NPC pool slot deactivated.
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

    /// <summary>The Special Attack menu callback, as if the player picked <paramref name="type"/>.</summary>
    internal void Harness_SelectSpecialAttack(SpecialAttackType type, bool offHand)
    {
        OnSpecialAttackSelected(type, offHand);
    }

    /// <summary>The spell menu callback, as if the player picked <paramref name="spell"/> with <paramref name="metamagic"/>.</summary>
    internal void Harness_SelectSpell(SpellData spell, MetamagicData metamagic)
    {
        OnSpellSelectedWithMetamagic(spell, metamagic);
    }

    /// <summary>The AoO confirmation the player is being asked to answer, or null.</summary>
    internal AoOProvokingActionInfo Harness_PendingAoO
        => _waitingForAoOConfirmation ? _pendingAoOAction : null;

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

    /// <summary>
    /// Removes what an earlier run left on a party slot: concentration and every spell effect (while
    /// the managers are still bound to the old stats, so the reversals hit those stats), then the
    /// spellcasting component with its buffs, spellbook and preparation lists. The controller's lazy
    /// component caches and StatusEffectManager.Init fetch the replacement.
    /// </summary>
    private static void Harness_ClearSlotSpellState(CharacterController pc)
    {
        if (pc == null)
            return;

        ConcentrationManager conc = pc.GetComponent<ConcentrationManager>();
        if (conc != null && conc.IsConcentrating)
            conc.EndConcentration(silent: true);

        StatusEffectManager statusMgr = pc.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
            statusMgr.RemoveAllEffects();

        DestroyAllComponents<SpellcastingComponent>(pc.gameObject);
    }

    private static void DestroyAllComponents<T>(GameObject go) where T : Component
    {
        if (go == null)
            return;

        T[] components = go.GetComponents<T>();
        for (int i = 0; i < components.Length; i++)
            UnityEngine.Object.DestroyImmediate(components[i]);
    }
}
#endif
