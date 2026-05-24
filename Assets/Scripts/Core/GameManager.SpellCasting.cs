using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DND35.Magic;
using DND35.AI.Profiles;
using DND35e.Identifiers;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// GameManager partial class: Spell Casting Orchestration
/// 
/// Contains all spell casting flow logic:
/// - Spell slot consumption and validation
/// - Arcane spell failure checks
/// - Spell targeting (single, AoE, summon placement)
/// - Spell cast execution pipeline
/// - Individual spell resolution (Sleep, Color Spray, Cause Fear, etc.)
/// - Spell buff application and duration tracking
/// - Concentration mechanics
/// - Summoned creature spawn/despawn lifecycle
/// - Spellcasting provocation (AoO) handling
/// 
/// Extracted from main GameManager.cs to reduce file size and improve
/// maintainability. All methods are internal to the GameManager partial class.
/// </summary>
public partial class GameManager
{
    // ═══════════════════════════════════════════════════════════════════
    //  SPELL CASTING ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════

    private bool TryConsumePendingSpellCast(CharacterController caster)
    {
        if (caster == null || _pendingSpell == null) return false;

        bool isQuickened = _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            Debug.LogError("[GameManager] TryConsumePendingSpellCast: No SpellcastingComponent!");
            return false;
        }

        if (!isQuickened)
        {
            caster.CommitStandardAction();
        }
        else
        {
            spellComp.MarkQuickenedSpellCast();
        }

        int slotLevelToConsume = _pendingSpell.SpellLevel;
        bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;
        if (hasMetamagicApplied)
            slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);


        if (!ResolveEntangledSomaticCastingConcentration(
                caster,
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                false,
                -1,
                null))
        {
            HandleConcentrationOnCasting(caster, _pendingSpell);
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingAnimateRopeItem = null;
            _pendingResistEnergyType = null;
            _pendingFireShieldIsWarm = null;
            _pendingProtectionFromEnergyType = null;
            ResetPendingWallOfFireMode();
            ResetPendingWallOfIceMode();
            return false;
        }

        if (!ResolveGrappledOrPinnedCastingConcentration(
                caster,
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                false,
                -1,
                null))
        {
            HandleConcentrationOnCasting(caster, _pendingSpell);
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingAnimateRopeItem = null;
            _pendingResistEnergyType = null;
            _pendingFireShieldIsWarm = null;
            _pendingProtectionFromEnergyType = null;
            ResetPendingWallOfFireMode();
            ResetPendingWallOfIceMode();
            return false;
        }
        if (TryRollArcaneSpellFailure(caster, _pendingSpell, false, out int asfRoll, out int asfChance))
        {
            bool consumedOnFailure = ConsumePendingSpellSlot(
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                false,
                -1,
                null);

            if (!consumedOnFailure)
            {
                Debug.LogError($"[GameManager] ASF failure path: could not consume level {slotLevelToConsume} slot for {_pendingSpell.Name}");
                return false;
            }

            HandleConcentrationOnCasting(caster, _pendingSpell);
            LogArcaneSpellFailure(caster, _pendingSpell, asfRoll, asfChance);

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;

            return false;
        }

        bool consumed = ConsumePendingSpellSlot(
            spellComp,
            _pendingSpell,
            _pendingMetamagic,
            hasMetamagicApplied,
            slotLevelToConsume,
            false,
            -1,
            null);

        if (!consumed)
        {
            Debug.LogError($"[GameManager] Failed to consume level {slotLevelToConsume} slot for summon spell {_pendingSpell.Name}");
            return false;
        }

        HandleConcentrationOnCasting(caster, _pendingSpell);
        return true;
    }

    private bool ConsumePendingSpellSlot(
        SpellcastingComponent spellComp,
        SpellData spell,
        MetamagicData metamagic,
        bool hasMetamagicApplied,
        int slotLevelToConsume,
        bool isSpontaneous,
        int spontaneousLevel,
        string spontaneousSacrificedSpellId)
    {
        if (spellComp == null || spell == null) return false;

        if (isSpontaneous)
        {
            if (!string.IsNullOrEmpty(spontaneousSacrificedSpellId))
                return spellComp.SpontaneousCastFromSpecificSpell(spontaneousSacrificedSpellId);

            return spellComp.SpontaneousCastFromSlot(spontaneousLevel);
        }

        if (hasMetamagicApplied && slotLevelToConsume > 0)
            return spellComp.CastWizardSpellWithMetamagic(spell, metamagic);

        return spellComp.CastSpellFromSlot(spell);
    }

    private bool TryRollArcaneSpellFailure(CharacterController caster, SpellData spell, bool isDeliveringHeldCharge, out int roll, out int asfChance)
    {
        roll = 0;
        asfChance = 0;

        if (isDeliveringHeldCharge || caster == null || caster.Stats == null || spell == null)
            return false;

        // Bypass arcane spell failure for F12 test panel casts
        if (_testPanelCastActive)
            return false;

        if (!caster.Stats.IsAffectedByArcaneSpellFailure)
            return false;

        asfChance = Mathf.Clamp(caster.Stats.ArcaneSpellFailure, 0, 100);
        if (asfChance <= 0)
            return false;

        roll = DiceService.Percentile("Arcane spell failure");

        CombatUI?.ShowCombatLog($"ASF Check ({caster.Stats.CharacterName}, {spell.Name}): roll {roll}% vs {asfChance}%");
        return roll <= asfChance;
    }

    private void LogArcaneSpellFailure(CharacterController caster, SpellData spell, int roll, int asfChance)
    {
        if (caster == null || caster.Stats == null || spell == null) return;

        CombatUI?.ShowCombatLog("═══════════════════════════════");
        CombatUI?.ShowCombatLog($"{caster.Stats.CharacterName} attempts to cast {spell.Name}");
        CombatUI?.ShowCombatLog($"Arcane Spell Failure: {roll}% ≤ {asfChance}%");
        CombatUI?.ShowCombatLog("⚠️ SPELL FAILS! Spell slot consumed, no effect.");
        CombatUI?.ShowCombatLog("═══════════════════════════════");
    }

    private void InsertIntoInitiative(CharacterController combatant, CharacterController summoner)
    {
        if (combatant == null || combatant.Stats == null)
            return;

        bool isPCTeam = IsPC(combatant);
        _turnService?.AddToInitiative(combatant, isPCTeam, summoner);
        UpdateInitiativeUI();
    }

    private CharacterController SpawnSummonedCreature(CharacterController caster, Vector2Int cell, SummonMonsterOption option)
    {
        if (caster == null || option == null)
            return null;

        NPCDefinition baseDef = NPCDatabase.Get(option.NpcDefinitionId);
        if (baseDef == null)
        {
            Debug.LogError($"[Summon] Missing NPC definition '{option.NpcDefinitionId}' for summon option '{option.DisplayName}'.");
            return null;
        }

        NPCDefinition summonDef = baseDef.Clone();

        if (summonDef.AppliedTemplateIds == null)
            summonDef.AppliedTemplateIds = new List<string>();
        else
            summonDef.AppliedTemplateIds.Clear();

        if (!string.IsNullOrWhiteSpace(option.TemplateId))
            summonDef.AppliedTemplateIds.Add(option.TemplateId);

        // Apply template mutations (DR/resistances/special abilities/etc.) through the centralized registry.
        summonDef = CreatureTemplateRegistry.ApplyTemplatesClone(summonDef) ?? summonDef;
        summonDef.Id = $"summon_runtime_{option.NpcDefinitionId}";
        summonDef.Name = option.BuildUiLabel();

        bool isCelestial = string.Equals(option.TemplateId, "celestial", StringComparison.OrdinalIgnoreCase);
        bool isFiendish = string.Equals(option.TemplateId, "fiendish", StringComparison.OrdinalIgnoreCase);

        if (summonDef.CreatureTags == null)
            summonDef.CreatureTags = new List<string>();

        if (!summonDef.CreatureTags.Contains("Summoned"))
            summonDef.CreatureTags.Add("Summoned");

        if (isCelestial && !summonDef.CreatureTags.Contains("Good"))
            summonDef.CreatureTags.Add("Good");
        if (isFiendish && !summonDef.CreatureTags.Contains("Evil"))
            summonDef.CreatureTags.Add("Evil");

        GameObject summonGO = new GameObject($"Summon_{option.NpcDefinitionId}_{UnityEngine.Random.Range(1000, 9999)}");
        if (summonGO.GetComponent<SpriteRenderer>() == null)
            summonGO.AddComponent<SpriteRenderer>();

        CharacterController summon = summonGO.AddComponent<CharacterController>();

        string iconKey = IconLoader.DetermineMonsterType(summonDef.Name);
        Sprite alive = !string.IsNullOrEmpty(iconKey) ? IconLoader.GetToken(iconKey) : null;
        if (alive == null)
            alive = LoadSprite("Sprites/npc_enemy_alive");
        Sprite dead = LoadSprite("Sprites/npc_enemy_dead");

        InitializeNPCFromDefinition(summon, summonDef, cell, alive, dead);

        bool alliedToPlayer = caster.Team == CharacterTeam.Player;
        summon.ConfigureTeamControl(alliedToPlayer ? CharacterTeam.Player : CharacterTeam.Enemy, controllable: alliedToPlayer);

        if (summon.Stats != null)
        {
            if (option.SummonedCreatureAlignment != Alignment.None)
            {
                summon.Stats.CharacterAlignment = option.SummonedCreatureAlignment;
            }
            else if (isCelestial)
            {
                summon.Stats.CharacterAlignment = Alignment.NeutralGood;
            }
            else if (isFiendish)
            {
                summon.Stats.CharacterAlignment = Alignment.NeutralEvil;
            }
            else
            {
                summon.Stats.CharacterAlignment = Alignment.TrueNeutral;
            }
        }

        NPCs.Add(summon);
        _npcAIBehaviors.Add(summonDef.AIBehavior);

        if (summon.Team == CharacterTeam.Player)
            _summonedAllies.Add(summon);
        else
            _summonedEnemies.Add(summon);

        var summonVisual = summon.gameObject.GetComponent<SummonedCreatureVisual>();
        if (summonVisual == null)
            summonVisual = summon.gameObject.AddComponent<SummonedCreatureVisual>();
        summonVisual.Init(summon, isCelestial, isFiendish);

        return summon;
    }

    private IEnumerator DespawnSummonWithEffect(ActiveSummonInstance summon, string reason)
    {
        if (summon == null || summon.Controller == null)
            yield break;

        CharacterController cc = summon.Controller;

        var summonVisual = cc.GetComponent<SummonedCreatureVisual>();
        if (summonVisual != null)
            yield return StartCoroutine(summonVisual.PlayDespawnEffect());

        Grid.ClearCreatureOccupancy(cc);

        int npcIdx = NPCs.IndexOf(cc);
        if (npcIdx >= 0)
        {
            NPCs.RemoveAt(npcIdx);
            if (npcIdx < _npcAIBehaviors.Count)
                _npcAIBehaviors.RemoveAt(npcIdx);
        }

        _summonedAllies.Remove(cc);
        _summonedEnemies.Remove(cc);

        _turnService?.RemoveFromInitiative(cc);

        string despawnMessage;
        if (reason == "duration expired")
            despawnMessage = $"<color=#66E8FF>{cc.Stats.CharacterName} disappears as the summoning ends.</color>";
        else if (reason == "dismissed")
            despawnMessage = $"<color=#66E8FF>{cc.Stats.CharacterName} returns to its home plane.</color>";
        else
            despawnMessage = $"<color=#FF8F8F>{cc.Stats.CharacterName} is slain! (Summoning ended early)</color>";

        CombatUI?.ShowCombatLog(despawnMessage);
        Debug.Log($"[Summon] Despawned {cc.Stats.CharacterName}: {reason}");

        Destroy(cc.gameObject);
        UpdateInitiativeUI();
        UpdateAllStatsUI();
    }

    private void HandleSummonDeathCleanup(CharacterController maybeSummon)
    {
        if (maybeSummon == null) return;

        ClearMirrorImageForCaster(maybeSummon, "caster slain", removeStatusEffect: true, log: true);

        ActiveSummonInstance summon = GetActiveSummon(maybeSummon);
        if (summon == null) return;

        _activeSummons.Remove(summon);
        StartCoroutine(DespawnSummonWithEffect(summon, "destroyed"));
    }

    private void TickSummonDurations()
    {
        if (_activeSummons.Count == 0) return;

        var expired = new List<ActiveSummonInstance>();
        foreach (var summon in _activeSummons)
        {
            if (summon == null || summon.Controller == null || summon.Controller.Stats == null)
            {
                expired.Add(summon);
                continue;
            }

            if (summon.Controller.Stats.IsDead)
            {
                expired.Add(summon);
                continue;
            }

            bool casterIncapacitated = summon.Caster == null
                || summon.Caster.Stats == null
                || summon.Caster.Stats.IsDead
                || summon.Caster.HasCondition(CombatConditionType.Unconscious)
                || summon.Caster.HasCondition(CombatConditionType.Paralyzed);

            if (summon.IsConcentrationSummon
                && !summon.HasEnteredPostConcentrationDuration
                && casterIncapacitated
                && IsCasterMaintainingSummonSwarmConcentration(summon.Caster))
            {
                ConcentrationManager casterConc = summon.Caster.GetComponent<ConcentrationManager>();
                if (casterConc != null)
                {
                    string breakLog = casterConc.ForceBreakConcentration("incapacitated");
                    if (!string.IsNullOrEmpty(breakLog))
                        CombatUI?.ShowCombatLog($"<color=#FF6644>{breakLog}</color>");
                }
            }

            bool holdByConcentration = summon.IsConcentrationSummon
                && !summon.HasEnteredPostConcentrationDuration
                && IsCasterMaintainingSummonSwarmConcentration(summon.Caster);

            if (!holdByConcentration)
            {
                if (summon.IsConcentrationSummon && !summon.HasEnteredPostConcentrationDuration)
                {
                    summon.HasEnteredPostConcentrationDuration = true;
                    summon.RemainingRounds = Mathf.Max(1, summon.TotalDurationRounds);
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>{summon.Controller.Stats.CharacterName}: concentration ended, {summon.RemainingRounds} rounds until dismissal.</color>");
                }
                else
                {
                    summon.RemainingRounds--;
                }
            }

            var visual = summon.Controller.GetComponent<SummonedCreatureVisual>();
            if (visual != null)
                visual.SetDuration(summon.RemainingRounds, summon.TotalDurationRounds);

            if (!holdByConcentration)
            {
                if (summon.RemainingRounds == 2)
                    CombatUI?.ShowCombatLog($"<color=#66E8FF>{summon.Controller.Stats.CharacterName}: 2 rounds remaining.</color>");
                else if (summon.RemainingRounds == 1)
                    CombatUI?.ShowCombatLog($"<color=#FFCC66>{summon.Controller.Stats.CharacterName}: 1 round remaining!</color>");
            }

            if (!holdByConcentration && summon.RemainingRounds <= 0)
                expired.Add(summon);
        }

        foreach (var ex in expired)
        {
            _activeSummons.Remove(ex);
            StartCoroutine(DespawnSummonWithEffect(ex, ex != null && ex.RemainingRounds <= 0 ? "duration expired" : "destroyed"));
        }
    }

    private int RollSummonCreatureCount(int spellLevel, int selectedListLevel, out string rollLog)
    {
        SummonCreatureCountInfo info = SummonMonsterLists.GetCreatureCountInfo(spellLevel, selectedListLevel);

        if (info == null || !info.RequiresRoll)
        {
            rollLog = "Creature count: 1 (same-level list).";
            return 1;
        }

        if (info.LevelDifference == 1)
        {
            int d3Roll = DiceService.Roll(1, 3, "Summon creature count 1d3");
            rollLog = $"Rolling for creature count: 1d3 = {d3Roll}";
            return d3Roll;
        }

        int d4Roll = DiceService.D4("Summon creature count 1d4");
        int total = d4Roll + 1;
        rollLog = $"Rolling for creature count: 1d4+1 = [{d4Roll}] + 1 = {total}";
        return total;
    }

    private SquareCell FindBestAdditionalSummonCell(CharacterController caster, Vector2Int preferredOrigin, int summonSizeSquares, int maxRangeFromCaster)
    {
        if (Grid == null || Grid.Cells == null || Grid.Cells.Count == 0)
            return null;

        SquareCell bestCell = null;
        int bestScore = int.MaxValue;

        foreach (KeyValuePair<Vector2Int, SquareCell> kvp in Grid.Cells)
        {
            SquareCell cell = kvp.Value;
            if (cell == null)
                continue;

            if (!Grid.CanPlaceCreature(cell.Coords, summonSizeSquares))
                continue;

            if (caster != null && maxRangeFromCaster > 0)
            {
                int casterDistance = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
                if (casterDistance > maxRangeFromCaster)
                    continue;
            }

            int distanceFromPrimary = SquareGridUtils.GetDistance(preferredOrigin, cell.Coords);
            int distanceFromCaster = caster != null ? SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords) : 0;

            // Primary key: nearest to the initially selected summon point.
            // Secondary key: weighted toward the caster when multiple cells are similar.
            int score = (distanceFromPrimary * 100) + (distanceFromCaster * 10);
            if (score < bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    private void RegisterActiveSummon(
        CharacterController summon,
        CharacterController caster,
        string sourceSpellId,
        int? durationRoundsOverride = null,
        bool concentrationSummon = false,
        bool startsInPostConcentrationMode = false)
    {
        if (summon == null)
            return;

        int defaultDuration = Mathf.Max(1, caster != null && caster.Stats != null ? caster.Stats.Level : 1);
        int durationRounds = Mathf.Max(1, durationRoundsOverride ?? defaultDuration);

        var activeSummon = new ActiveSummonInstance
        {
            Controller = summon,
            Caster = caster,
            RemainingRounds = durationRounds,
            TotalDurationRounds = durationRounds,
            SourceSpellId = sourceSpellId,
            IsAlliedToPCs = summon.Team == CharacterTeam.Player,
            SmiteUsed = false,
            CurrentCommand = SummonCommand.AttackNearest(),
            IsConcentrationSummon = concentrationSummon,
            HasEnteredPostConcentrationDuration = startsInPostConcentrationMode
        };
        _activeSummons.Add(activeSummon);

        var visual = summon.GetComponent<SummonedCreatureVisual>();
        if (visual != null)
            visual.SetDuration(durationRounds, durationRounds);
    }

    private void PerformSummonSwarmCast(CharacterController caster, SquareCell targetCell, string swarmNpcId)
    {
        if (caster == null || targetCell == null || string.IsNullOrWhiteSpace(swarmNpcId) || _pendingSpell == null)
        {
            ShowActionChoices();
            return;
        }

        NPCDefinition baseDef = NPCDatabase.Get(swarmNpcId);
        if (baseDef == null)
        {
            CombatUI?.ShowCombatLog($"Cannot summon swarm: missing creature definition '{swarmNpcId}'.");
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;
        CaptureSpellcastResourceSnapshot(caster);

        if (!TryConsumePendingSpellCast(caster))
        {
            ClearSpellcastResourceSnapshot();
            ShowActionChoices();
            return;
        }

        ResolveSpellcastProvocation(caster, _pendingSpell, false, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();

            SummonMonsterOption option = new SummonMonsterOption
            {
                DisplayName = baseDef.Name,
                NpcDefinitionId = swarmNpcId,
                TemplateId = null
            };

            CharacterController summonCC = SpawnSummonedCreature(caster, targetCell.Coords, option);
            if (summonCC == null)
            {
                CombatUI?.ShowCombatLog("⚠ Summon Swarm failed: creature could not be spawned.");
                ShowActionChoices();
                return;
            }

            summonCC.aiProfile = ScriptableObject.CreateInstance<IndiscriminateSwarmAI>();
            summonCC.EnemyUseCoupDeGraceOverride = false;

            // Swarm is completely uncontrollable - it attacks nearest creature regardless of team
            summonCC.SetControllable(false);

            InsertIntoInitiative(summonCC, caster);

            int persistedRounds = 2;
            RegisterActiveSummon(
                summonCC,
                caster,
                _pendingSpell.SpellId,
                durationRoundsOverride: persistedRounds,
                concentrationSummon: true,
                startsInPostConcentrationMode: false);

            BeginSummonSwarmConcentration(caster, _pendingSpell, summonCC);

            int rangeFeet = CalculateSummonSwarmRangeFeet(caster != null && caster.Stats != null ? caster.Stats.Level : 1);
            CombatUI?.ShowCombatLog($"<color=#66E8FF>{caster.Stats.CharacterName} casts {_pendingSpell.Name}!</color>");
            CombatUI?.ShowCombatLog($"  Range: {rangeFeet} ft (level {Mathf.Max(1, caster.Stats.Level)} caster)");
            CombatUI?.ShowCombatLog($"  Target location: ({targetCell.Coords.x}, {targetCell.Coords.y})");
            CombatUI?.ShowCombatLog($"  Swarm type: {baseDef.Name}");
            CombatUI?.ShowCombatLog($"<color=#FF8866>⚠ WARNING: The swarm is uncontrolled and will attack the nearest creature (NO friend/foe distinction - caster and allies ARE valid targets)!</color>");
            CombatUI?.ShowCombatLog($"<color=#44AAFF>{caster.Stats.CharacterName} is concentrating on {_pendingSpell.Name}.</color>");

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingSummonSwarmNpcId = null;
            _pendingSummonSelection = null;
            _pendingSummonListLevel = 0;
            _pendingSummonCountInfo = null;

            Grid.ClearAllHighlights();
            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(caster, 1.0f));
        });
    }

    private void PerformSummonMonsterCast(CharacterController caster, SquareCell targetCell, SummonMonsterOption option)
    {
        if (caster == null || targetCell == null || option == null || _pendingSpell == null)
        {
            ShowActionChoices();
            return;
        }

        NPCDefinition baseDef = NPCDatabase.Get(option.NpcDefinitionId);
        if (baseDef == null)
        {
            CombatUI?.ShowCombatLog($"Cannot summon {option.DisplayName}: missing creature definition.");
            ShowActionChoices();
            return;
        }

        bool isSwarmSummonOption = SummonMonsterLists.IsSwarmOption(option);

        int summonSizeSquares = baseDef.SizeCategory.GetSpaceWidthSquares();
        if (!Grid.CanPlaceCreature(targetCell.Coords, summonSizeSquares))
        {
            CombatUI.ShowCombatLog("Cannot summon there: not enough open space for that creature size.");
            ShowSummonPlacementTargets(caster, _pendingSpell);
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;
        CaptureSpellcastResourceSnapshot(caster);

        if (!TryConsumePendingSpellCast(caster))
        {
            ClearSpellcastResourceSnapshot();
            ShowActionChoices();
            return;
        }

        ResolveSpellcastProvocation(caster, _pendingSpell, false, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();

            int spellLevel = SummonMonsterLists.GetSummonMonsterSpellLevel(_pendingSpell.SpellId);
            int selectedListLevel = _pendingSummonListLevel > 0 ? _pendingSummonListLevel : spellLevel;
            SummonCreatureCountInfo countInfo = _pendingSummonCountInfo ?? SummonMonsterLists.GetCreatureCountInfo(spellLevel, selectedListLevel);
            int creatureCount = RollSummonCreatureCount(spellLevel, selectedListLevel, out string rollLog);
            creatureCount = Mathf.Max(1, creatureCount);

            string selectedListRoman = SummonMonsterLists.ToRomanLevel(Mathf.Max(1, selectedListLevel));
            CombatUI?.ShowCombatLog($"<color=#66E8FF>{caster.Stats.CharacterName} casts {_pendingSpell.Name} (using Level {selectedListRoman} list).</color>");
            if (countInfo != null && countInfo.RequiresRoll)
                CombatUI?.ShowCombatLog($"<color=#CCEEFF>{rollLog}</color>");

            string summonLabel = option.BuildUiLabel();
            CombatUI?.ShowCombatLog($"<color=#66E8FF>Summoning {creatureCount} {summonLabel}{(creatureCount == 1 ? string.Empty : "s")}...</color>");
            if (isSwarmSummonOption)
                CombatUI?.ShowCombatLog("<color=#77EE99>Swarm summons are AI-controlled allies and cannot be directly commanded.</color>");

            List<CharacterController> spawnedCreatures = new List<CharacterController>(creatureCount);
            Vector2Int primaryCell = targetCell.Coords;
            int summonRangeSquares = Mathf.Max(1, _pendingSpell.GetRangeSquaresForCasterLevel(caster != null && caster.Stats != null ? caster.Stats.Level : 0));

            for (int i = 0; i < creatureCount; i++)
            {
                SquareCell spawnCell = i == 0
                    ? targetCell
                    : FindBestAdditionalSummonCell(caster, primaryCell, summonSizeSquares, summonRangeSquares);

                bool stackedDueToNoSpace = false;
                if (spawnCell == null)
                {
                    spawnCell = targetCell;
                    stackedDueToNoSpace = true;
                }

                CharacterController summonCC = SpawnSummonedCreature(caster, spawnCell.Coords, option);
                if (summonCC == null)
                    continue;

                if (isSwarmSummonOption)
                {
                    // Summoned swarms stay allied, but act under AI control.
                    summonCC.SetControllable(false);

                    if (!(summonCC.aiProfile is SwarmAI) && !(summonCC.aiProfile is IndiscriminateSwarmAI))
                        summonCC.aiProfile = ScriptableObject.CreateInstance<SwarmAI>();
                }

                if (stackedDueToNoSpace)
                    CombatUI?.ShowCombatLog("<color=#FFCC66>Not enough open slots; stacking extra summons on the primary tile.</color>");

                InsertIntoInitiative(summonCC, caster);
                RegisterActiveSummon(summonCC, caster, _pendingSpell.SpellId);
                spawnedCreatures.Add(summonCC);

                string summonIndexLabel = creatureCount > 1 ? $" {i + 1}" : string.Empty;
                CombatUI?.ShowCombatLog($"<color=#CCEEFF>{summonLabel}{summonIndexLabel} appears!</color>");
            }

            if (spawnedCreatures.Count == 0)
            {
                CombatUI?.ShowCombatLog("⚠ Summoning failed: no valid creatures could be spawned.");
                ShowActionChoices();
                return;
            }

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingSummonSelection = null;
            _pendingSummonListLevel = 0;
            _pendingSummonCountInfo = null;
            _pendingSummonSwarmNpcId = null;

            Grid.ClearAllHighlights();
            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(caster, 1.0f));
        });
    }

    private void BeginPendingSpellTargeting(CharacterController caster)
    {
        Debug.Log($"[SpellCasting] BeginPendingSpellTargeting  caster={caster?.Stats?.CharacterName}  spell={_pendingSpell?.Name}  target={_pendingSpell?.TargetType}  AoE={_pendingSpell?.AoEShapeType}");

        if (caster == null || _pendingSpell == null)
        {
            Debug.LogWarning($"[SpellCasting] BeginPendingSpellTargeting ABORTED: caster={caster != null}  spell={_pendingSpell != null}");
            ShowActionChoices();
            return;
        }

        // ── Resilient Sphere outgoing spell block (PHB p.263) ──
        // Creature can attempt to cast, but non-self spells cannot pass through the sphere boundary.
        // Self-targeting spells still work normally inside the sphere.
        // Now uses area effect check instead of character stats.
        if (ResilientSphereAreaEffect.IsCharacterInAnySphere(caster)
            && _pendingSpell.TargetType != SpellTargetType.Self)
        {
            CombatUI?.ShowCombatLog(
                $"<color=#44CCFF>🔮 {caster.Stats.CharacterName}'s spell cannot pass through the Resilient Sphere!</color>");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            ShowActionChoices();
            return;
        }

        if (!CanBeginSpellcastWhileGrappledOrPinned(caster, _pendingSpell))
        {
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            ShowActionChoices();
            return;
        }

        if (IsPendingGreaseAreaCast())
        {
            EnterGreaseAreaTargetingMode(caster, _pendingSpell);
            return;
        }

        if (IsPendingGreaseArmorCast())
        {
            EnterGreaseArmorTargetingMode(caster, _pendingSpell);
            return;
        }

        if (string.Equals(_pendingSpell.SpellId, SpellNames.DISGUISE_SELF, StringComparison.Ordinal))
        {
            ShowDisguiseSelfRaceSelection(caster);
            return;
        }

        if (string.Equals(_pendingSpell.SpellId, SpellNames.RESIST_ENERGY, StringComparison.Ordinal) && !_pendingResistEnergyType.HasValue)
        {
            ShowResistEnergyTypeSelection(caster);
            return;
        }

        if (string.Equals(_pendingSpell.SpellId, SpellNames.PROTECTION_FROM_ENERGY, StringComparison.Ordinal) && !_pendingProtectionFromEnergyType.HasValue)
        {
            ShowProtectionFromEnergyTypeSelection(caster);
            return;
        }

        if (string.Equals(_pendingSpell.SpellId, SpellNames.FIRE_SHIELD, StringComparison.Ordinal) && !_pendingFireShieldIsWarm.HasValue)
        {
            ShowFireShieldTypeSelection(caster);
            return;
        }

        // Wall of Fire: choose Line vs Ring mode before AoE targeting (PHB p.298)
        if (string.Equals(_pendingSpell.SpellId, SpellNames.WALL_OF_FIRE, StringComparison.Ordinal) && !_pendingWallOfFireMode.HasValue)
        {
            ShowWallOfFireModeSelection(caster);
            return;
        }

        // Wall of Ice: choose Line vs Circle mode before AoE targeting (PHB p.299)
        if (string.Equals(_pendingSpell.SpellId, SpellNames.WALL_OF_ICE, StringComparison.Ordinal) && !_pendingWallOfIceMode.HasValue)
        {
            ShowWallOfIceModeSelection(caster);
            return;
        }

        // ===== AoE SPELLS: Enter AoE targeting mode =====
        if (_pendingSpell.AoEShapeType != AoEShape.None)
        {
            EnterAoETargetingMode(caster, _pendingSpell);
            return;
        }

        // Summon Monster spells: choose creature first, then select destination tile.
        if (IsSummonMonsterSpell(_pendingSpell))
        {
            ShowSummonCreatureSelectionMenu(caster, _pendingSpell);
            return;
        }

        // Summon Swarm: choose swarm type first, then place in range (occupied squares allowed).
        if (IsSummonSwarmSpell(_pendingSpell))
        {
            ShowSummonSwarmSelectionMenu(caster, _pendingSpell);
            return;
        }

        // Wish: open selection UI for players, auto-decide for AI (PHB p.302)
        if (string.Equals(_pendingSpell.SpellId, SpellNames.WISH, StringComparison.Ordinal))
        {
            HandleWishSpellCast(caster);
            return;
        }

        // Determine targeting based on spell type
        if (_pendingSpell.TargetType == SpellTargetType.Self)
        {
            // Self-targeting spells cast immediately
            Debug.Log($"[SpellCasting] Self-targeting spell → PerformSpellCast immediately");
            PerformSpellCast(caster, caster);
        }
        else
        {
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            Debug.Log($"[SpellCasting] Non-self spell → SelectingAttackTarget mode, calling ShowSpellTargets  range={_pendingSpell.GetRangeSquaresForCasterLevel(caster?.Stats?.Level ?? 0)}");
            ShowSpellTargets(caster, _pendingSpell);
        }
    }

    private void ShowDisguiseSelfRaceSelection(CharacterController caster)
    {
        if (caster == null || caster.Stats == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        SizeCategory casterSize = caster.Stats.CurrentSizeCategory;
        List<string> raceOptions = RaceDatabase.GetRaceNamesBySizeCategory(casterSize);
        if (raceOptions == null || raceOptions.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ No races available for size {casterSize}. Disguise Self cancelled.");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingDisguiseSelfRace = null;
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.ShowDisguiseSelfRaceSelector(
            caster.Stats.CharacterName,
            casterSize,
            raceOptions,
            onSelect: selectedRace =>
            {
                _pendingDisguiseSelfRace = selectedRace;
                PerformSpellCast(caster, caster);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingDisguiseSelfRace = null;
                ShowActionChoices();
            });
    }

    private void ShowResistEnergyTypeSelection(CharacterController caster)
    {
        if (caster == null || caster.Stats == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        CombatUI.SetActionButtonsVisible(false);

        List<string> options = new List<string>
        {
            "Acid",
            "Cold",
            "Electricity",
            "Fire",
            "Sonic"
        };

        CombatUI.ShowPickUpItemSelection(
            actorName: caster.Stats.CharacterName,
            itemOptions: options,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingResistEnergyType = null;
                    _pendingFireShieldIsWarm = null;
                    _pendingProtectionFromEnergyType = null;
                    CombatUI?.ShowCombatLog("⚠ Resist Energy cancelled: no energy type selected.");
                    ShowActionChoices();
                    return;
                }

                _pendingResistEnergyType = (ResistEnergyType)selectedIndex;
                CombatUI?.ShowCombatLog($"✨ Resist Energy prepared for {options[selectedIndex].ToLowerInvariant()}.");
                BeginPendingSpellTargeting(caster);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingResistEnergyType = null;
                _pendingFireShieldIsWarm = null;
                _pendingProtectionFromEnergyType = null;
                CombatUI?.ShowCombatLog("↩ Resist Energy cancelled (energy type not selected).");
                ShowActionChoices();
            },
            titleOverride: "Resist Energy - Choose Energy Type",
            bodyOverride: "Select one energy type to resist: acid, cold, electricity, fire, or sonic.",
            optionButtonColorOverride: new Color(0.24f, 0.4f, 0.62f, 1f));
    }

    private void ShowProtectionFromEnergyTypeSelection(CharacterController caster)
    {
        if (caster == null || caster.Stats == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        CombatUI.SetActionButtonsVisible(false);

        List<string> options = new List<string>
        {
            "Acid",
            "Cold",
            "Electricity",
            "Fire",
            "Sonic"
        };

        CombatUI.ShowPickUpItemSelection(
            actorName: caster.Stats.CharacterName,
            itemOptions: options,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingProtectionFromEnergyType = null;
                    CombatUI?.ShowCombatLog("⚠ Protection from Energy cancelled: no energy type selected.");
                    ShowActionChoices();
                    return;
                }

                _pendingProtectionFromEnergyType = (ResistEnergyType)selectedIndex;
                CombatUI?.ShowCombatLog($"✨ Protection from Energy prepared for {options[selectedIndex].ToLowerInvariant()}.");
                BeginPendingSpellTargeting(caster);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingProtectionFromEnergyType = null;
                CombatUI?.ShowCombatLog("↩ Protection from Energy cancelled (energy type not selected).");
                ShowActionChoices();
            },
            titleOverride: "Protection from Energy - Choose Energy Type",
            bodyOverride: "Select one energy type to protect against: acid, cold, electricity, fire, or sonic.\nAbsorbs 12 points per caster level (max 120).",
            optionButtonColorOverride: new Color(0.24f, 0.5f, 0.40f, 1f));
    }

    private void ShowFireShieldTypeSelection(CharacterController caster)
    {
        if (caster == null || caster.Stats == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        CombatUI.SetActionButtonsVisible(false);

        List<string> options = new List<string>
        {
            "Chill Shield — cold retribution, resist fire 50%",
            "Warm Shield — fire retribution, resist cold 50%"
        };

        CombatUI.ShowPickUpItemSelection(
            actorName: caster.Stats.CharacterName,
            itemOptions: options,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingFireShieldIsWarm = null;
                    CombatUI?.ShowCombatLog("⚠ Fire Shield cancelled: no shield type selected.");
                    ShowActionChoices();
                    return;
                }

                // Index 0 = Chill Shield (not warm), Index 1 = Warm Shield
                _pendingFireShieldIsWarm = (selectedIndex == 1);
                string chosen = selectedIndex == 1 ? "Warm Shield" : "Chill Shield";
                CombatUI?.ShowCombatLog($"✨ Fire Shield prepared: {chosen}.");
                BeginPendingSpellTargeting(caster);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingFireShieldIsWarm = null;
                CombatUI?.ShowCombatLog("↩ Fire Shield cancelled (shield type not selected).");
                ShowActionChoices();
            },
            titleOverride: "Fire Shield — Choose Shield Type",
            bodyOverride: "Choose a shield type:\n• Chill Shield: deals cold retribution to melee attackers; reduces fire damage by half.\n• Warm Shield: deals fire retribution to melee attackers; reduces cold damage by half.",
            optionButtonColorOverride: new Color(0.8f, 0.3f, 0.1f, 1f));
    }

    private bool ShouldShowTouchSpellPrompt(SpellData spell)
    {
        if (spell == null) return false;
        if (spell.AoEShapeType != AoEShape.None) return false;
        if (!spell.IsMeleeTouchSpell()) return false;
        return spell.TargetType != SpellTargetType.Self;
    }

    /// <summary>Legacy callback for backward compat (no metamagic).</summary>
    private void OnSpellSelected(SpellData spell)
    {
        OnSpellSelectedWithMetamagic(spell, null);
    }

    /// <summary>Called when spell selection is cancelled.</summary>
    private void OnSpellSelectionCancelled()
    {
        _pendingResistEnergyType = null;
        _pendingFireShieldIsWarm = null;
        _pendingProtectionFromEnergyType = null;
        _pendingDisguiseSelfRace = null;
        ResetPendingWallOfFireMode();
        ResetPendingWallOfIceMode();
        ShowActionChoices();
    }

    private bool IsHoldableMeleeTouchSpell(SpellData spell)
    {
        if (spell == null) return false;
        if (spell.AoEShapeType != AoEShape.None) return false;
        if (!spell.IsMeleeTouchSpell()) return false;
        if (spell.TargetType == SpellTargetType.Self) return false;
        return true;
    }

    private bool IsFriendlyTarget(CharacterController caster, CharacterController target)
    {
        return IsAllyTeam(caster, target);
    }

    private bool IsHumanoid(CharacterController target)
    {
        if (target?.Stats == null) return false;
        return string.Equals(target.Stats.CreatureType, "Humanoid", StringComparison.OrdinalIgnoreCase);
    }

    private int GetTargetHitDice(CharacterController target)
    {
        if (target?.Stats == null) return 0;
        return Mathf.Max(1, target.Stats.HitDice > 0 ? target.Stats.HitDice : target.Stats.Level);
    }

    private bool IsImmuneToMindAffecting(CharacterController target)
    {
        if (target?.Stats == null) return false;

        string creatureType = string.IsNullOrWhiteSpace(target.Stats.CreatureType)
            ? string.Empty
            : target.Stats.CreatureType.Trim().ToLowerInvariant();

        if (creatureType == "undead" || creatureType == "construct" || creatureType == "ooze" || creatureType == "plant" || creatureType == "vermin")
            return true;

        if (target.Stats.SpecialAbilities != null)
        {
            for (int i = 0; i < target.Stats.SpecialAbilities.Count; i++)
            {
                string trait = target.Stats.SpecialAbilities[i];
                if (string.IsNullOrWhiteSpace(trait))
                    continue;

                string normalized = trait.ToLowerInvariant();
                if (normalized.Contains("mind-affect") || normalized.Contains("mind affecting") || normalized.Contains("mindless"))
                    return true;
            }
        }

        return false;
    }

    private static bool IsCauseFearSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.CAUSE_FEAR, StringComparison.Ordinal);
    }

    private bool IsLivingCreatureForFearSpell(CharacterController target)
    {
        if (target?.Stats == null)
            return false;

        string creatureType = string.IsNullOrWhiteSpace(target.Stats.CreatureType)
            ? string.Empty
            : target.Stats.CreatureType.Trim().ToLowerInvariant();

        if (creatureType == "undead" || creatureType == "construct")
            return false;

        return true;
    }

    private bool IsValidTargetForSpell(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (caster == null || target == null || spell == null || target.Stats == null)
            return false;

        if (target.Stats.IsDead)
        {
            Debug.Log($"[Targeting][Spell] Reject target '{target.Stats.CharacterName}' for {spell.Name}: HP={target.Stats.CurrentHP}, IsDead={target.Stats.IsDead}, IsUnconscious={target.Stats.IsUnconscious}");
            return false;
        }

        bool isPersonTransmutation = spell.SpellId == SpellNames.ENLARGE_PERSON || spell.SpellId == SpellNames.REDUCE_PERSON
            || spell.SpellId == SpellNames.MASS_ENLARGE_PERSON || spell.SpellId == SpellNames.MASS_REDUCE_PERSON;
        if (isPersonTransmutation)
        {
            // Person transmutations can target any humanoid creature (ally or enemy).
            // TARGETING OVERRIDE: Allow any character to be targeted regardless of faction.
            return IsHumanoid(target);
        }

        if (spell.SpellId == SpellNames.DAZE)
        {
            // D&D 3.5e Daze: one humanoid creature of 4 HD or less.
            // TARGETING OVERRIDE: Removed enemy-only restriction. Still requires humanoid and HD check.
            if (!IsHumanoid(target)) return false;
            if (!_isProtectionFromEvilTestEncounter && GetTargetHitDice(target) > 4) return false;
            if (IsImmuneToMindAffecting(target)) return false;
            return true;
        }

        if (spell.SpellId == SpellNames.CHARM_PERSON)
        {
            // D&D 3.5e Charm Person: one humanoid creature.
            // TARGETING OVERRIDE: Removed enemy-only restriction. Still requires humanoid and HD check.
            if (!IsHumanoid(target)) return false;
            if (GetTargetHitDice(target) > 4) return false;
            if (IsImmuneToMindAffecting(target)) return false;
            return true;
        }

        if (spell.SpellId == SpellNames.TOUCH_OF_IDIOCY)
        {
            // TARGETING OVERRIDE: Removed enemy-only restriction. Still requires living creature.
            if (!IsLivingCreatureForFearSpell(target)) return false;
            return true;
        }

        // Direct targeting requires line of sight for enemies.
        // See Invisible allows direct targeting of invisible enemies, but does not bypass
        // true visibility blockers or total concealment sources.
        // TARGETING OVERRIDE: Line-of-sight check still applies for enemies only.
        if (IsEnemyTeam(caster, target))
        {
            bool isRangedTouch = spell.IsRangedTouchSpell();
            bool casterCanSeeInvisibleTarget = target.HasActiveInvisibilityEffect && caster.CanSeeInvisible(target);
            if (!casterCanSeeInvisibleTarget && !caster.CanSee(target, isRangedTouch))
                return false;
        }

        // TARGETING OVERRIDE: All spells can now target any character regardless of faction.
        // SingleEnemy and SingleAlly are no longer restricted to their respective teams.
        // Self spells still require self-targeting.
        switch (spell.TargetType)
        {
            case SpellTargetType.SingleEnemy:
                // Allow targeting any living character (ally or enemy).
                return true;
            case SpellTargetType.SingleAlly:
                // Allow targeting any living character (ally or enemy).
                return true;
            case SpellTargetType.Touch:
                return true;
            case SpellTargetType.Area:
                return true;
            case SpellTargetType.Self:
                return target == caster;
            default:
                return false;
        }
    }

    private bool ShouldForceTargetToAcceptSave(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (caster == null || target == null || spell == null) return false;

        bool isAlly = target == caster || IsAllyTeam(caster, target);

        // --- VOLUNTARY SAVE FAILURE (D&D 3.5e PHB p.177) ---
        // A willing creature can voluntarily forgo a saving throw and accept a spell's result.
        // This applies to ALL spells cast on allies, not just specific "harmless" ones.
        //
        // EXCEPTION: Compelled or controlled creatures (e.g. under Charm or Confusion)
        // cannot voluntarily choose to fail a save, because their will is compromised.
        // They must roll saves normally even against allied casters.
        if (isAlly)
        {
            // Check if the target is under a compulsion/control effect that prevents voluntary save failure.
            bool isCompelled = target.HasCondition(CombatConditionType.Charmed)
                            || target.HasCondition(CombatConditionType.Confused);

            if (isCompelled)
            {
                // Compelled allies cannot voluntarily fail saves — they roll normally.
                // BUFF AUTO-SUCCESS: Even compelled allies auto-fail saves for buff spells,
                // since buff spells are beneficial and the compulsion doesn't make them resist help.
                // (Task 2: EffectType.Buff on ally = always skip save)
                if (spell.EffectType == SpellEffectType.Buff)
                {
                    return true; // Buff on ally always auto-accepts, even if compelled
                }
                return false; // Non-buff on compelled ally: roll save normally
            }

            // Willing (non-compelled) ally: voluntarily fails the save for ANY spell.
            // This covers both buff spells (Task 2) and the general voluntary failure rule (Task 3).
            return true;
        }

        // Enemies always roll saves normally — no change to existing enemy save mechanics.
        return false;
    }

    private bool HasActiveShieldSpell(CharacterController target)
    {
        if (target == null)
            return false;

        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            foreach (ActiveSpellEffect effect in statusMgr.ActiveEffects)
            {
                if (effect?.Spell == null)
                    continue;

                if (string.Equals(effect.Spell.SpellId, SpellNames.SHIELD, StringComparison.OrdinalIgnoreCase)
                    && effect.RemainingRounds > 0)
                {
                    return true;
                }
            }
        }

        SpellcastingComponent spellComp = target.GetComponent<SpellcastingComponent>();
        if (spellComp != null
            && spellComp.ActiveBuffs != null
            && spellComp.ActiveBuffs.TryGetValue(SpellNames.SHIELD, out int rounds)
            && rounds > 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Highlight valid targets for a spell based on its range and target type.
    /// Shows the full spell range area (purple) with valid targets highlighted (magenta).
    /// </summary>
    private void ShowSpellTargets(CharacterController caster, SpellData spell)
    {
        Debug.Log($"[SpellCasting] ShowSpellTargets  caster={caster?.Stats?.CharacterName}@{caster?.GridPosition}  spell={spell?.Name}  targetType={spell?.TargetType}");

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int range = spell.GetRangeSquaresForCasterLevel(caster?.Stats?.Level ?? 0);
        if (range <= 0) range = 1; // Touch/self spells = adjacent (1 square for targeting)
        Debug.Log($"[SpellCasting]   Computed range={range} squares ({range * 5} ft)");


        List<SquareCell> allCells = Grid.GetCellsInRange(caster.GridPosition, range);
        bool hasTarget = false;

        // First pass: highlight all cells in range with spell range color (purple)
        foreach (var cell in allCells)
        {
            int sqDist = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
            if (sqDist > range) continue;
            if (cell.Coords == caster.GridPosition) continue;
            cell.SetHighlight(HighlightType.SpellRange);
        }

        // Highlight caster's full occupied footprint.
        HighlightCharacterFootprint(caster, HighlightType.Selected);

        // Second pass: highlight valid targets (magenta) on top of range area
        foreach (var cell in allCells)
        {
            if (!cell.IsOccupied || cell.Occupant.Stats.IsDead) continue;
            if (cell.Occupant == caster) continue;

            int sqDist = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
            if (sqDist > range) continue;

            bool validTarget = IsValidTargetForSpell(caster, cell.Occupant, spell);

            if (validTarget)
            {
                cell.SetHighlight(HighlightType.SpellTarget);

                _highlightedCells.Add(cell);
                hasTarget = true;
            }
        }

        // For friendly/support touch spells, also allow self-targeting by clicking own tile.
        if ((spell.TargetType == SpellTargetType.SingleAlly || spell.TargetType == SpellTargetType.Touch)
            && IsValidTargetForSpell(caster, caster, spell))
        {
            HighlightCharacterFootprint(caster, HighlightType.SpellTarget, addToSelectableCells: true);
            hasTarget = true;
        }

        Debug.Log($"[SpellCasting] ShowSpellTargets: {_highlightedCells.Count} valid target cell(s) found, hasTarget={hasTarget}");

        if (hasTarget)
        {
            string rangeStr = spell.RangeSquares <= 0 ? "Touch" : $"{range} sq ({range * 5} ft)";
            string targetMsg;
            if (_pendingSpellFromHeldCharge)
            {
                targetMsg = "Click a target to discharge held touch spell";
            }
            else
            {
                targetMsg = spell.TargetType == SpellTargetType.SingleAlly
                    ? "Click an ally (or self) to cast"
                    : spell.TargetType == SpellTargetType.Touch
                        ? "Click a creature (ally, self, or enemy) to cast"
                        : spell.TargetType == SpellTargetType.Area
                            ? "Click a target area to cast"
                            : "Click an enemy to cast";
            }
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: {targetMsg} | Range: {rangeStr} | Right-click to cancel");
            Debug.Log($"[SpellCasting]   Awaiting target click. SubPhase={CurrentSubPhase}  AttackMode={_pendingAttackMode}");
        }
        else
        {
            Debug.LogWarning($"[SpellCasting] No valid targets found for {spell.Name}!");
            CombatUI.SetTurnIndicator($"No valid targets for {spell.Name}! | Right-click to cancel");
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    /// <summary>
    /// Convert the currently selected melee touch spell into a held charge.
    /// The slot is consumed now; delivery can happen on a later action.
    /// </summary>
    private void HoldPendingMeleeTouchCharge(CharacterController caster)
    {
        if (caster == null || _pendingSpell == null) { ShowActionChoices(); return; }

        CurrentSubPhase = PlayerSubPhase.Animating;

        CaptureSpellcastResourceSnapshot(caster);

        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            ClearSpellcastResourceSnapshot();
            Debug.LogError("[GameManager] HoldPendingMeleeTouchCharge: No SpellcastingComponent!");
            ShowActionChoices();
            return;
        }

        bool isQuickened = _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        if (!isQuickened)
        {
            caster.CommitStandardAction();
        }
        else
        {
            spellComp.MarkQuickenedSpellCast();
        }

        bool consumed = true;
        if (!_pendingSpellFromHeldCharge)
        {
            bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;
            int slotLevelToConsume = _pendingSpell.SpellLevel;
            if (hasMetamagicApplied)
                slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);

            if (!ResolveGrappledOrPinnedCastingConcentration(
                    caster,
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    false,
                    -1,
                    null))
            {
                HandleConcentrationOnCasting(caster, _pendingSpell);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }
            if (TryRollArcaneSpellFailure(caster, _pendingSpell, false, out int asfRoll, out int asfChance))
            {
                consumed = ConsumePendingSpellSlot(
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    false,
                    -1,
                    null);

                if (!consumed)
                {
                    ClearSpellcastResourceSnapshot();
                    Debug.LogError($"[GameManager] ASF failure path: could not consume level {slotLevelToConsume} spell slot for held charge!");
                    ShowActionChoices();
                    return;
                }

                HandleConcentrationOnCasting(caster, _pendingSpell);
                LogArcaneSpellFailure(caster, _pendingSpell, asfRoll, asfChance);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }

            consumed = ConsumePendingSpellSlot(
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                false,
                -1,
                null);

            if (!consumed)
            {
                ClearSpellcastResourceSnapshot();
                Debug.LogError($"[GameManager] Failed to consume level {slotLevelToConsume} spell slot for held charge!");
                ShowActionChoices();
                return;
            }
        }

        HandleConcentrationOnCasting(caster, _pendingSpell);

        ResolveSpellcastProvocation(caster, _pendingSpell, false, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();
            spellComp.SetHeldTouchCharge(_pendingSpell, _pendingMetamagic);

            CombatUI.ShowCombatLog($"✋ {caster.Stats.CharacterName} chooses Discharge Later and holds the charge of {_pendingSpell.Name}.");
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;

            StartCoroutine(AfterAttackDelay(caster, 1.0f));
        });
    }
    /// <summary>
    /// Execute a spell cast from caster to target.
    /// </summary>
    private void PerformSpellCast(CharacterController caster, CharacterController target)
    {
        Debug.Log($"[SpellCasting] PerformSpellCast  caster={caster?.Stats?.CharacterName}  target={target?.Stats?.CharacterName}  spell={_pendingSpell?.Name}  testPanel={_testPanelCastActive}");

        // Clean up test-panel override now that the spell is resolving
        CleanupTestPanelCast();

        CombatUI?.HideDisguiseSelfRaceSelector();
        CurrentSubPhase = PlayerSubPhase.Animating;

        if (!IsValidTargetForSpell(caster, target, _pendingSpell))
        {
            CombatUI?.ShowCombatLog($"{_pendingSpell.Name} has invalid target (requires humanoid ally/enemy constraints).");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingMagicWeaponItem = null;
            _pendingKeenEdgeItem = null;
            _pendingKeenEdgeIsAmmo = false;
            _pendingGreaterMagicWeaponItem = null;
            _pendingResistEnergyType = null;
            _pendingFireShieldIsWarm = null;
            _pendingProtectionFromEnergyType = null;
            ShowActionChoices();
            return;
        }

        if (string.Equals(_pendingSpell.SpellId, SpellNames.RESIST_ENERGY, StringComparison.Ordinal) && !_pendingResistEnergyType.HasValue)
        {
            CombatUI?.ShowCombatLog("⚠ Resist Energy requires selecting an energy type before casting.");
            ShowResistEnergyTypeSelection(caster);
            return;
        }

        if (string.Equals(_pendingSpell.SpellId, SpellNames.PROTECTION_FROM_ENERGY, StringComparison.Ordinal) && !_pendingProtectionFromEnergyType.HasValue)
        {
            CombatUI?.ShowCombatLog("⚠ Protection from Energy requires selecting an energy type before casting.");
            ShowProtectionFromEnergyTypeSelection(caster);
            return;
        }

        if (string.Equals(_pendingSpell.SpellId, SpellNames.FIRE_SHIELD, StringComparison.Ordinal) && !_pendingFireShieldIsWarm.HasValue)
        {
            CombatUI?.ShowCombatLog("⚠ Fire Shield requires selecting a shield type before casting.");
            ShowFireShieldTypeSelection(caster);
            return;
        }

        if (TryHandleMagicWeaponWeaponSelection(caster, target))
            return;

        if (TryHandleKeenEdgeWeaponSelection(caster, target))
            return;

        if (TryHandleGreaterMagicWeaponSelection(caster, target))
            return;

        // ── Spell Component Pouch check (D&D 3.5e PHB p.130) ──
        // Spells with common material components (M with no GP cost) require a spell component pouch.
        // F12 test panel casts bypass this requirement for testing convenience.
        if (!_testPanelCastActive && _pendingSpell.HasMaterialComponent)
        {
            if (!SpellComponentRegistry.ValidatePouchRequirement(_pendingSpell.SpellId, _pendingSpell, caster, out string pouchFailure))
            {
                CombatUI?.ShowCombatLog($"<color=#FF6666>❌ {caster.Stats?.CharacterName} cannot cast {_pendingSpell.Name} — requires a {pouchFailure} for material components! Purchase one from the store (5 gp).</color>");
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingMagicWeaponItem = null;
                _pendingKeenEdgeItem = null;
                _pendingKeenEdgeIsAmmo = false;
                _pendingGreaterMagicWeaponItem = null;
                _pendingResistEnergyType = null;
                _pendingFireShieldIsWarm = null;
                _pendingProtectionFromEnergyType = null;
                ShowActionChoices();
                return;
            }
        }

        CaptureSpellcastResourceSnapshot(caster);

        bool isDeliveringHeldCharge = _pendingSpellFromHeldCharge;

        // Quickened applies when CASTING the spell, not when delivering a previously held charge.
        bool isQuickened = !isDeliveringHeldCharge && _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        if (isDeliveringHeldCharge)
        {
            // D&D 3.5e: discharging a held touch spell is a free action.
            // Do not consume standard/move actions here.
            Debug.Log($"[GameManager] {caster.Stats.CharacterName} discharging held touch spell as a free action.");
        }
        else if (!isQuickened)
        {
            caster.CommitStandardAction();
        }
        else
        {
            // Mark that this character has used their one quickened spell for this round
            var casterSpellComp = caster.GetComponent<SpellcastingComponent>();
            if (casterSpellComp != null)
            {
                casterSpellComp.MarkQuickenedSpellCast();
            }
        }

        // Get spellcasting component
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            ClearSpellcastResourceSnapshot();
            Debug.LogError("[GameManager] PerformSpellCast: No SpellcastingComponent!");
            ShowActionChoices();
            return;
        }

        // Check if this is a spontaneous cast (cleric converting a specific prepared spell)
        bool isSpontaneous = !isDeliveringHeldCharge && CombatUI != null && CombatUI.IsSpontaneousCast;
        int spontaneousLevel = isSpontaneous ? CombatUI.SpontaneousCastLevel : -1;
        string spontaneousSacrificedSpellId = isSpontaneous ? CombatUI.SpontaneousSacrificedSpellId : null;

        // Clear spontaneous casting state
        if (CombatUI != null)
            CombatUI.ClearSpontaneousCastState();

        // Consume spell slot using D&D 3.5e slot-based system
        // Cantrips (level 0) are UNLIMITED — no slot consumed
        int slotLevelToConsume = _pendingSpell.SpellLevel;
        bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;

        if (hasMetamagicApplied)
        {
            slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);
            Debug.Log($"[GameManager] Metamagic: consuming level {slotLevelToConsume} slot " +
                      $"(base {_pendingSpell.SpellLevel} + {slotLevelToConsume - _pendingSpell.SpellLevel} metamagic)");
        }

        if (!isDeliveringHeldCharge)
        {
            if (!ResolveEntangledSomaticCastingConcentration(
                    caster,
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    isSpontaneous,
                    spontaneousLevel,
                    spontaneousSacrificedSpellId))
            {
                HandleConcentrationOnCasting(caster, _pendingSpell);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingSpellFromHeldCharge = false;
                _pendingMetamagic = null;
                _pendingAnimateRopeItem = null;
                _pendingResistEnergyType = null;
                _pendingFireShieldIsWarm = null;
                _pendingProtectionFromEnergyType = null;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }

            if (!ResolveGrappledOrPinnedCastingConcentration(
                    caster,
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    isSpontaneous,
                    spontaneousLevel,
                    spontaneousSacrificedSpellId))
            {
                HandleConcentrationOnCasting(caster, _pendingSpell);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingSpellFromHeldCharge = false;
                _pendingMetamagic = null;
                _pendingAnimateRopeItem = null;
                _pendingResistEnergyType = null;
                _pendingFireShieldIsWarm = null;
                _pendingProtectionFromEnergyType = null;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }
            if (TryRollArcaneSpellFailure(caster, _pendingSpell, false, out int asfRoll, out int asfChance))
            {
                bool consumedOnFailure = ConsumePendingSpellSlot(
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    isSpontaneous,
                    spontaneousLevel,
                    spontaneousSacrificedSpellId);

                if (!consumedOnFailure)
                {
                    ClearSpellcastResourceSnapshot();
                    Debug.LogError($"[GameManager] ASF failure path: failed to consume level {slotLevelToConsume} spell slot!");
                    ShowActionChoices();
                    return;
                }

                HandleConcentrationOnCasting(caster, _pendingSpell);
                LogArcaneSpellFailure(caster, _pendingSpell, asfRoll, asfChance);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingSpellFromHeldCharge = false;
                _pendingMetamagic = null;
                _pendingAnimateRopeItem = null;
                _pendingResistEnergyType = null;
                _pendingFireShieldIsWarm = null;
                _pendingProtectionFromEnergyType = null;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }

            // D&D 3.5e PHB p.206: Blinking caster has a 20% spell failure chance.
            // The caster may be on the Ethereal Plane when the spell is cast, causing it to fizzle.
            // This is checked separately from (and in addition to) arcane spell failure from armor.
            if (caster.HasActiveBlinkEffect)
            {
                int blinkSpellRoll = DiceService.Percentile("Blink caster spell failure");
                if (blinkSpellRoll <= 20)
                {
                    bool consumedOnBlink = ConsumePendingSpellSlot(
                        spellComp,
                        _pendingSpell,
                        _pendingMetamagic,
                        hasMetamagicApplied,
                        slotLevelToConsume,
                        isSpontaneous,
                        spontaneousLevel,
                        spontaneousSacrificedSpellId);

                    if (!consumedOnBlink)
                    {
                        ClearSpellcastResourceSnapshot();
                        Debug.LogError($"[GameManager] Blink spell failure path: failed to consume level {slotLevelToConsume} spell slot!");
                        ShowActionChoices();
                        return;
                    }

                    HandleConcentrationOnCasting(caster, _pendingSpell);
                    CombatUI?.ShowCombatLog($"⚡ {caster.Stats.CharacterName}'s {_pendingSpell.Name} fizzles! (Blink spell failure: rolled {blinkSpellRoll} ≤ 20%)");
                    UpdateAllStatsUI();
                    Grid.ClearAllHighlights();

                    _pendingSpell = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingMetamagic = null;
                    _pendingAnimateRopeItem = null;
                    _pendingResistEnergyType = null;
                    _pendingFireShieldIsWarm = null;
                    _pendingProtectionFromEnergyType = null;

                    ClearSpellcastResourceSnapshot();
                    StartCoroutine(AfterAttackDelay(caster, 1.0f));
                    return;
                }
                else
                {
                    CombatUI?.ShowCombatLog($"⚡ Blink spell check: {caster.Stats.CharacterName} rolled {blinkSpellRoll} > 20% (spell proceeds).");
                }
            }

            // Consume spell slot
            // Cantrips are unlimited — CastSpellFromSlot handles this (no slot consumed)
            // Both Wizards and Clerics use slot-based system
            bool consumed = ConsumePendingSpellSlot(
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                isSpontaneous,
                spontaneousLevel,
                spontaneousSacrificedSpellId);

            if (!consumed)
            {
                ClearSpellcastResourceSnapshot();
                Debug.LogError($"[GameManager] Failed to consume level {slotLevelToConsume} spell slot!");
                ShowActionChoices();
                return;
            }

            if (isSpontaneous && !string.IsNullOrEmpty(spontaneousSacrificedSpellId))
                Debug.Log($"[GameManager] Spontaneous cast: {caster.Stats.CharacterName} sacrificed '{spontaneousSacrificedSpellId}' → {_pendingSpell.Name}");
            else if (isSpontaneous)
                Debug.Log($"[GameManager] Spontaneous cast (level-based): {caster.Stats.CharacterName} converted a level {spontaneousLevel} slot → {_pendingSpell.Name}");
        }
        // Check if caster is concentrating on another spell — casting requires a concentration check
        HandleConcentrationOnCasting(caster, _pendingSpell);

        ResolveSpellcastProvocation(caster, _pendingSpell, isDeliveringHeldCharge, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();

            // ── COUNTERSPELL CHECK ──
            // D&D 3.5e PHB: Before spell effects resolve, check for readied counterspells.
            // Only actual spells can be counterspelled (not SLAs, Su, or Ex abilities).
            if (!isDeliveringHeldCharge)
            {
                CounterspellResult counterspellResult = TryResolveCounterspell(caster, _pendingSpell);
                if (counterspellResult != null && counterspellResult.Success)
                {
                    // Spell was successfully countered — it has no effect
                    Debug.Log($"[Counterspell] {_pendingSpell.Name} was countered! No spell effect.");
                    UpdateAllStatsUI();
                    Grid.ClearAllHighlights();

                    _pendingSpell = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingMetamagic = null;
                    _pendingAnimateRopeItem = null;
                    _pendingResistEnergyType = null;
                    _pendingFireShieldIsWarm = null;
                    _pendingProtectionFromEnergyType = null;
                    _pendingDisguiseSelfRace = null;
                    ResetPendingGreaseCastMode();

                    StartCoroutine(AfterAttackDelay(caster, 1.0f));
                    return;
                }
                // If counterspell attempted but failed (Dispel Magic miss), spell proceeds normally
            }

            // Resolve the spell with metamagic.
            // D&D 3.5e: willing friendly targets for melee touch delivery should auto-succeed.
            BreakInvisibilityOnHostileSpellCast(caster, _pendingSpell, target, null);
            bool skipFriendlyTouchAttackRoll = _pendingSpell.IsMeleeTouchSpell() && IsFriendlyTarget(caster, target);
            bool forceTargetToFailSave = ShouldForceTargetToAcceptSave(caster, target, _pendingSpell);

            if (TryHandleMirrorImageSpellTargetAttack(caster, target, _pendingSpell, out string mirrorSpellLog))
            {
                _lastCombatLog = mirrorSpellLog;
                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingSpellFromHeldCharge = false;
                _pendingMetamagic = null;
                _pendingAnimateRopeItem = null;
                _pendingResistEnergyType = null;
                _pendingFireShieldIsWarm = null;
                _pendingProtectionFromEnergyType = null;
                _pendingDisguiseSelfRace = null;
                ResetPendingGreaseCastMode();

                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }

            // ── BLINK TARGET SPELL FAILURE ──
            // D&D 3.5e PHB p.206: Individually targeted spells have a 50% chance
            // to fail against a blinking creature (the target may be on the Ethereal Plane).
            // This does not apply to area spells or self-targeted spells.
            if (target != null && target != caster && target.HasActiveBlinkEffect
                && _pendingSpell.TargetType != SpellTargetType.Self
                && _pendingSpell.TargetType != SpellTargetType.Area)
            {
                int blinkTargetRoll = DiceService.Percentile("Blink target spell failure");
                if (blinkTargetRoll <= 50)
                {
                    string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
                    CombatUI?.ShowCombatLog($"🌀 {_pendingSpell.Name} fails to reach {targetName}! Target is on the Ethereal Plane. (Blink: rolled {blinkTargetRoll} ≤ 50%)");
                    UpdateAllStatsUI();
                    Grid.ClearAllHighlights();

                    _pendingSpell = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingMetamagic = null;
                    _pendingAnimateRopeItem = null;
                    _pendingResistEnergyType = null;
                    _pendingFireShieldIsWarm = null;
                    _pendingProtectionFromEnergyType = null;
                    _pendingDisguiseSelfRace = null;
                    ResetPendingGreaseCastMode();

                    StartCoroutine(AfterAttackDelay(caster, 1.0f));
                    return;
                }
                else
                {
                    string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
                    CombatUI?.ShowCombatLog($"🌀 Blink target check: {targetName} rolled {blinkTargetRoll} > 50% (spell connects).");
                }
            }

            SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic, skipFriendlyTouchAttackRoll, forceTargetToFailSave, caster, target);

            // Apply tracked buff/debuff effects based on spell type
            // Includes expanded categories that also apply status effects
            bool appliesTrackedEffect = _pendingSpell.EffectType == SpellEffectType.Buff ||
                                        _pendingSpell.EffectType == SpellEffectType.Debuff ||
                                        _pendingSpell.EffectType == SpellEffectType.Control ||
                                        _pendingSpell.EffectType == SpellEffectType.Illusion ||
                                        _pendingSpell.EffectType == SpellEffectType.Wall;

            bool causeFearSaveReduced = IsCauseFearSpell(_pendingSpell) && result.RequiredSave && result.SaveSucceeded;
            bool scareSaveReduced = IsScareSpell(_pendingSpell) && result.RequiredSave && result.SaveSucceeded;
            bool blurSaveNegated = _pendingSpell != null
                                   && string.Equals(_pendingSpell.SpellId, SpellNames.BLUR, StringComparison.Ordinal)
                                   && result.RequiredSave
                                   && result.SaveSucceeded;

            // D&D 3.5e PHB p.211: Command Undead — nonintelligent undead get no saving throw.
            bool commandUndeadNoSaveOverride = _pendingSpell != null
                && _pendingSpell.SpellId == SpellNames.COMMAND_UNDEAD
                && target != null && !target.IsIntelligentUndead();

            bool effectNegatedBySave = ((_pendingSpell.EffectType == SpellEffectType.Debuff ||
                                        _pendingSpell.EffectType == SpellEffectType.Control)
                                       || blurSaveNegated)
                                       && result.RequiredSave
                                       && result.SaveSucceeded
                                       && !causeFearSaveReduced
                                       && !scareSaveReduced
                                       && !commandUndeadNoSaveOverride;
            if (effectNegatedBySave)
            {
                CombatUI?.ShowCombatLog($"🛡 {target.Stats.CharacterName} resists {_pendingSpell.Name} with a successful {result.SaveType} save.");
            }

            if (result.MindAffectingImmunityBlocked)
            {
                CombatUI?.ShowCombatLog($"🧠 {target.Stats.CharacterName} is immune to mind-affecting effects. {_pendingSpell.Name} has no effect.");
            }

            // ── Lesser Globe of Invulnerability check ──
            // PHB p.246: Spell effects of 3rd level or lower are excluded from the globe area.
            // Check if target is inside a Lesser Globe and the incoming spell is ≤ 3rd level.
            // Note: the caster's own spells are also blocked if target is in a globe.
            bool blockedByGlobe = false;
            if (target != null && _pendingSpell != null && result.Success && !effectNegatedBySave)
            {
                if (LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(_pendingSpell, target))
                {
                    blockedByGlobe = true;
                    result.Success = false;
                    CombatUI?.ShowCombatLog($"🛡 {_pendingSpell.Name} (level {_pendingSpell.SpellLevel}) is blocked by Lesser Globe of Invulnerability! Spell effects of 3rd level or lower cannot affect {target.Stats.CharacterName}.");
                }
            }

            bool handledCauseFear = TryResolveCauseFearSpellEffect(caster, target, _pendingSpell, result);

            bool handledGhoulTouch = false;
            if (!handledCauseFear)
                handledGhoulTouch = TryResolveGhoulTouchSpellEffect(caster, target, _pendingSpell, result);

            bool handledScare = false;
            if (!handledCauseFear && !handledGhoulTouch)
                handledScare = TryResolveScareSpellEffect(caster, target, _pendingSpell, result);

            bool handledRayOfEnfeeblement = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && result.Success && !effectNegatedBySave)
                handledRayOfEnfeeblement = TryResolveRayOfEnfeeblementSpellEffect(caster, target, _pendingSpell, result);

            bool handledTouchOfIdiocy = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && result.Success && !effectNegatedBySave)
                handledTouchOfIdiocy = TryResolveTouchOfIdiocySpellEffect(caster, target, _pendingSpell, result);

            bool handledMelfsAcidArrow = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && result.Success && !effectNegatedBySave)
                handledMelfsAcidArrow = TryResolveMelfsAcidArrowSpellEffect(caster, target, _pendingSpell, result);

            bool handledRayOfExhaustion = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && result.Success)
                handledRayOfExhaustion = TryResolveRayOfExhaustionSpellEffect(caster, target, _pendingSpell, result);

            bool handledVampiricTouch = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && result.Success)
                handledVampiricTouch = TryResolveVampiricTouchSpellEffect(caster, target, _pendingSpell, result);

            bool handledEnervation = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && result.Success)
                handledEnervation = TryResolveEnervationSpellEffect(caster, target, _pendingSpell, result);

            bool handledContagion = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && result.Success)
                handledContagion = TryResolveContagionSpellEffect(caster, target, _pendingSpell, result);

            bool handledBestowCurse = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && result.Success)
                handledBestowCurse = TryResolveBestowCurseSpellEffect(caster, target, _pendingSpell, result);

            bool handledGreaterInvisibility = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && result.Success)
                handledGreaterInvisibility = TryResolveGreaterInvisibilitySpellEffect(caster, target, _pendingSpell, result);

            bool handledPhantasmalKiller = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && result.Success)
                handledPhantasmalKiller = TryResolvePhantasmalKillerSpellEffect(caster, target, _pendingSpell, result);

            bool handledFireShield = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && result.Success)
                handledFireShield = TryResolveFireShieldSpellEffect(caster, target, _pendingSpell, result);

            bool handledResilientSphere = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && result.Success && !effectNegatedBySave)
                handledResilientSphere = TryResolveResilientSphereSpellEffect(caster, target, _pendingSpell, result);

            bool handledAnimateRope = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere)
                handledAnimateRope = TryResolveAnimateRopeSpellEffect(caster, target, _pendingSpell, result);

            bool handledMirrorImage = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && result.Success && !effectNegatedBySave)
                handledMirrorImage = TryResolveMirrorImageSpellEffect(caster, target, _pendingSpell, result);

            bool handledDimensionalAnchor = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && !handledMirrorImage && result.Success && !effectNegatedBySave)
                handledDimensionalAnchor = TryResolveDimensionalAnchorSpellEffect(caster, target, _pendingSpell, result);

            bool handledRemoveCurse = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && !handledMirrorImage && !handledDimensionalAnchor && result.Success)
                handledRemoveCurse = TryResolveRemoveCurseSpellEffect(caster, target, _pendingSpell, result);

            bool handledDimensionDoor = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && !handledMirrorImage && !handledDimensionalAnchor && !handledRemoveCurse && result.Success)
                handledDimensionDoor = TryResolveDimensionDoorSpellEffect(caster, target, _pendingSpell, result);

            bool handledLesserGlobe = false;
            if (!handledCauseFear && !handledGhoulTouch && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && !handledMirrorImage && !handledDimensionalAnchor && !handledRemoveCurse && !handledDimensionDoor && result.Success)
                handledLesserGlobe = TryResolveLesserGlobeSpellEffect(caster, target, _pendingSpell, result);

            // ── Cleric 2nd-level spell handlers (GameManager_NewSpells.cs) ──
            bool anyPriorHandled = handledCauseFear || handledGhoulTouch || handledScare || handledRayOfEnfeeblement || handledTouchOfIdiocy || handledMelfsAcidArrow || handledRayOfExhaustion || handledVampiricTouch || handledEnervation || handledContagion || handledBestowCurse || handledGreaterInvisibility || handledPhantasmalKiller || handledFireShield || handledResilientSphere || handledAnimateRope || handledMirrorImage || handledDimensionalAnchor || handledRemoveCurse || handledDimensionDoor || handledLesserGlobe;

            bool handledDeathKnell = false;
            if (!anyPriorHandled && result.Success && !effectNegatedBySave)
                handledDeathKnell = TryResolveDeathKnellSpellEffect(caster, target, _pendingSpell, result);

            bool handledShieldOther = false;
            if (!anyPriorHandled && !handledDeathKnell && result.Success && !effectNegatedBySave)
                handledShieldOther = TryResolveShieldOtherSpellEffect(caster, target, _pendingSpell, result);

            bool handledSilence = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && result.Success && !effectNegatedBySave)
                handledSilence = TryResolveSilenceSpellEffect(caster, target, _pendingSpell, result);

            bool handledSoundBurst = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && !handledSilence)
                handledSoundBurst = TryResolveSoundBurstStunEffect(caster, target, _pendingSpell, result);

            bool handledSpiritualWeapon = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && !handledSilence && !handledSoundBurst && result.Success)
                handledSpiritualWeapon = TryResolveSpiritualWeaponSpellEffect(caster, target, _pendingSpell, result);

            bool handledAlignWeapon = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && !handledSilence && !handledSoundBurst && !handledSpiritualWeapon && result.Success && !effectNegatedBySave)
                handledAlignWeapon = TryResolveAlignWeaponSpellEffect(caster, target, _pendingSpell, result);

            // ── 3rd-level Cleric spell resolution ──
            bool handledSearingLight = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && !handledSilence && !handledSoundBurst && !handledSpiritualWeapon && !handledAlignWeapon && result.Success)
                handledSearingLight = TryResolveSearingLightSpellEffect(caster, target, _pendingSpell, result);

            bool handledInvisibilityPurge = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && !handledSilence && !handledSoundBurst && !handledSpiritualWeapon && !handledAlignWeapon && !handledSearingLight && result.Success)
                handledInvisibilityPurge = TryResolveInvisibilityPurgeSpellEffect(caster, target, _pendingSpell, result);

            bool handledRemoveDisease = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && !handledSilence && !handledSoundBurst && !handledSpiritualWeapon && !handledAlignWeapon && !handledSearingLight && !handledInvisibilityPurge && result.Success)
                handledRemoveDisease = TryResolveRemoveDiseaseSpellEffect(caster, target, _pendingSpell, result);

            bool handledPrayer = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && !handledSilence && !handledSoundBurst && !handledSpiritualWeapon && !handledAlignWeapon && !handledSearingLight && !handledInvisibilityPurge && !handledRemoveDisease && result.Success)
                handledPrayer = TryResolvePrayerSpellEffect(caster, target, _pendingSpell, result);

            bool handledRemoveBlindnessDeafness = false;
            if (!anyPriorHandled && !handledDeathKnell && !handledShieldOther && !handledSilence && !handledSoundBurst && !handledSpiritualWeapon && !handledAlignWeapon && !handledSearingLight && !handledInvisibilityPurge && !handledRemoveDisease && !handledPrayer && result.Success)
                handledRemoveBlindnessDeafness = TryResolveRemoveBlindnessDeafnessSpellEffect(caster, target, _pendingSpell, result);

            bool anyClericHandled = handledDeathKnell || handledSilence || handledSoundBurst || handledSpiritualWeapon || handledAlignWeapon
                || handledSearingLight || handledInvisibilityPurge || handledRemoveDisease || handledPrayer || handledRemoveBlindnessDeafness;
            // Note: handledShieldOther returns false to allow normal buff application (deflection/resistance)

            // ── 4th-level Cleric spells ──
            bool handledChaosHammer = false;
            if (!anyPriorHandled && !anyClericHandled && result.Success)
                handledChaosHammer = TryResolveChaosHammerSpellEffect(caster, target, _pendingSpell, result);

            bool handledHolySmite = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && result.Success)
                handledHolySmite = TryResolveHolySmiteSpellEffect(caster, target, _pendingSpell, result);

            bool handledOrdersWrath = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && result.Success)
                handledOrdersWrath = TryResolveOrdersWrathSpellEffect(caster, target, _pendingSpell, result);

            bool handledUnholyBlight = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && result.Success)
                handledUnholyBlight = TryResolveUnholyBlightSpellEffect(caster, target, _pendingSpell, result);

            bool handledDeathWard = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && result.Success)
                handledDeathWard = TryResolveDeathWardSpellEffect(caster, target, _pendingSpell, result);

            bool handledDivinePower = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && !handledDeathWard && result.Success)
                handledDivinePower = TryResolveDivinePowerSpellEffect(caster, target, _pendingSpell, result);

            bool handledFreedomOfMovement = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && !handledDeathWard && !handledDivinePower && result.Success)
                handledFreedomOfMovement = TryResolveFreedomOfMovementSpellEffect(caster, target, _pendingSpell, result);

            bool handledSpellImmunity = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && !handledDeathWard && !handledDivinePower && !handledFreedomOfMovement && result.Success)
                handledSpellImmunity = TryResolveSpellImmunitySpellEffect(caster, target, _pendingSpell, result);

            bool handledNeutralizePoison = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && !handledDeathWard && !handledDivinePower && !handledFreedomOfMovement && !handledSpellImmunity && result.Success)
                handledNeutralizePoison = TryResolveNeutralizePoisonSpellEffect(caster, target, _pendingSpell, result);

            bool handledPoison = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && !handledDeathWard && !handledDivinePower && !handledFreedomOfMovement && !handledSpellImmunity && !handledNeutralizePoison && result.Success)
                handledPoison = TryResolvePoisonSpellEffect(caster, target, _pendingSpell, result);

            bool handledDismissal = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && !handledDeathWard && !handledDivinePower && !handledFreedomOfMovement && !handledSpellImmunity && !handledNeutralizePoison && !handledPoison && result.Success)
                handledDismissal = TryResolveDismissalSpellEffect(caster, target, _pendingSpell, result);

            bool handledRepelVermin = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && !handledDeathWard && !handledDivinePower && !handledFreedomOfMovement && !handledSpellImmunity && !handledNeutralizePoison && !handledPoison && !handledDismissal && result.Success)
                handledRepelVermin = TryResolveRepelVerminSpellEffect(caster, target, _pendingSpell, result);

            bool handledImbueWithSpellAbility = false;
            if (!anyPriorHandled && !anyClericHandled && !handledChaosHammer && !handledHolySmite && !handledOrdersWrath && !handledUnholyBlight && !handledDeathWard && !handledDivinePower && !handledFreedomOfMovement && !handledSpellImmunity && !handledNeutralizePoison && !handledPoison && !handledDismissal && !handledRepelVermin && result.Success)
                handledImbueWithSpellAbility = TryResolveImbueWithSpellAbilitySpellEffect(caster, target, _pendingSpell, result);

            // ── Domain spell handlers (single-target) ──
            bool handledHoldAnimal = false;
            if (!anyPriorHandled && !anyClericHandled && result.Success && !effectNegatedBySave)
                handledHoldAnimal = TryResolveHoldAnimalSpellEffect(caster, target, _pendingSpell, result);

            bool handledProduceFlame = false;
            if (!anyPriorHandled && !anyClericHandled && !handledHoldAnimal && result.Success)
                handledProduceFlame = TryResolveProduceFlameSpellEffect(caster, target, _pendingSpell, result);

            bool handledHeatMetal = false;
            if (!anyPriorHandled && !anyClericHandled && !handledHoldAnimal && !handledProduceFlame && result.Success && !effectNegatedBySave)
                handledHeatMetal = TryResolveHeatMetalSpellEffect(caster, target, _pendingSpell, result);

            bool handledMagicVestment = false;
            if (!anyPriorHandled && !anyClericHandled && !handledHoldAnimal && !handledProduceFlame && !handledHeatMetal && result.Success && !effectNegatedBySave)
                handledMagicVestment = TryResolveMagicVestmentSpellEffect(caster, target, _pendingSpell, result);

            bool handledDominateAnimal = false;
            if (!anyPriorHandled && !anyClericHandled && !handledHoldAnimal && !handledProduceFlame && !handledHeatMetal && !handledMagicVestment && result.Success && !effectNegatedBySave)
                handledDominateAnimal = TryResolveDominateAnimalSpellEffect(caster, target, _pendingSpell, result);

            bool handledCommandPlants = false;
            if (!anyPriorHandled && !anyClericHandled && !handledHoldAnimal && !handledProduceFlame && !handledHeatMetal && !handledMagicVestment && !handledDominateAnimal && result.Success && !effectNegatedBySave)
                handledCommandPlants = TryResolveCommandPlantsSpellEffect(caster, target, _pendingSpell, result);

            bool anyCleric4Handled = handledChaosHammer || handledHolySmite || handledOrdersWrath || handledUnholyBlight
                || handledDeathWard || handledDivinePower || handledFreedomOfMovement || handledSpellImmunity
                || handledNeutralizePoison || handledPoison || handledDismissal || handledRepelVermin
                || handledImbueWithSpellAbility
                || handledHoldAnimal || handledProduceFlame || handledHeatMetal || handledMagicVestment
                || handledDominateAnimal || handledCommandPlants;

            if (!anyPriorHandled && !anyClericHandled && !anyCleric4Handled && result.Success && appliesTrackedEffect && !effectNegatedBySave)
            {
                var appliedEffect = ApplySpellBuff(caster, target, _pendingSpell, spellComp);

                // If this is a concentration spell, begin tracking concentration on the caster
                if (appliedEffect != null && _pendingSpell.DurationType == DurationType.Concentration)
                {
                    BeginConcentrationTracking(caster, appliedEffect, _pendingSpell);
                }
            }

            // Check concentration for spell damage on the target
            if (result.DamageDealt > 0 && target != null)
            {
                CheckConcentrationOnDamage(target, result.DamageDealt);
            }

            bool retainedHeldChargeOnMiss = false;

            // Delivering a held charge clears it only if the touch actually lands.
            // If the touch attack misses, keep the held charge (PHB 3.5e p.141).
            if (isDeliveringHeldCharge)
            {
                bool touchDeliverySucceeded = !result.RequiredAttackRoll || result.AttackHit;
                if (touchDeliverySucceeded)
                {
                    spellComp.ClearHeldTouchCharge("touch delivered");
                }
                else
                {
                    retainedHeldChargeOnMiss = true;
                }
            }

            // Handle death if target was killed
            if (result.TargetKilled && target != null)
            {
                target.OnDeath();
                HandleSummonDeathCleanup(target);
            }

            // Build combat log with quickened spell / spontaneous cast indicators
            _lastCombatLog = result.GetFormattedLog();

            if (isSpontaneous)
            {
                string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                    ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                    : "Converted prepared spell";
                string spontPrefix = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n";
                _lastCombatLog = spontPrefix + _lastCombatLog;
            }

            if (isQuickened)
            {
                string quickenedPrefix = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n";
                _lastCombatLog = quickenedPrefix + _lastCombatLog;
            }

            if (retainedHeldChargeOnMiss)
            {
                _lastCombatLog += $"\n✋ Touch attack missed — {caster.Stats.CharacterName} retains {_pendingSpell.Name} charge.";
            }

            if (GameManager.LogAttacksToConsole)
                Debug.Log("[Spell] " + _lastCombatLog);

            CombatUI.ShowCombatLog(_lastCombatLog);
            UpdateAllStatsUI();

            Grid.ClearAllHighlights();

            // Check for victory (all NPCs dead) or defeat (all PCs dead)
            if (result.TargetKilled)
            {
                if (AreAllNPCsDead())
                {
                    Debug.Log("[CombatEnd] Victory condition met after spell target kill.");
                    HandleCombatVictoryDetected("ResolveSingleTargetSpell");
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingAnimateRopeItem = null;
                    _pendingResistEnergyType = null;
                    _pendingFireShieldIsWarm = null;
                    _pendingProtectionFromEnergyType = null;
                    ResetPendingGreaseCastMode();
                    return;
                }
                else if (AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingAnimateRopeItem = null;
                    _pendingResistEnergyType = null;
                    _pendingFireShieldIsWarm = null;
                    _pendingProtectionFromEnergyType = null;
                    ResetPendingGreaseCastMode();
                    return;
                }
            }

            _pendingSpell = null;
            _pendingSpellFromHeldCharge = false;
            _pendingMetamagic = null;
            _pendingAnimateRopeItem = null;
            _pendingResistEnergyType = null;
            _pendingFireShieldIsWarm = null;
            _pendingProtectionFromEnergyType = null;
            _pendingDisguiseSelfRace = null;
            ResetPendingGreaseCastMode();

            // After standard action, check for remaining actions
            StartCoroutine(AfterAttackDelay(caster, 1.5f));
        });
    }

    // ========================================================================
    // AREA OF EFFECT (AoE) TARGETING AND RESOLUTION
    // ========================================================================

    /// <summary>
    /// Enter AoE targeting mode for the given spell.
    /// Shows the spell's placement range and lets the player aim the AoE.
    /// </summary>
    private void EnterAoETargetingMode(CharacterController caster, SpellData spell)
    {
        int casterLevel = caster?.Stats?.GetCasterLevel() ?? 0;
        int placementRange = spell.AoERangeSquares > 0
            ? spell.AoERangeSquares
            : spell.GetRangeSquaresForCasterLevel(casterLevel);

        // ===== SELF-CENTERED BURST: Show preview with confirmation =====
        // Some burst spells (e.g., Bless) are centered on the caster. Others (e.g.,
        // Flaming Sphere, Sleep, Web) use AoERangeSquares=0 as "use range profile"
        // and must still prompt for target selection.
        if (spell.AoEShapeType == AoEShape.Burst && placementRange <= 0)
        {
            Debug.Log($"[AoE] Self-centered burst: {spell.Name} — showing preview at ({caster.GridPosition.x},{caster.GridPosition.y})");

            // Calculate AoE cells centered on caster
            HashSet<Vector2Int> aoeCells = AoESystem.GetBurstCells(caster.GridPosition, spell.AoESizeSquares, Grid);

            // Filter out cells blocked by line-of-effect blockers (walls, spheres, etc.)
            AoESystem.FilterCellsByLineOfEffect(aoeCells, caster.GridPosition);

            // Visual preview — highlight affected area
            Grid.ClearAllHighlights();
            foreach (Vector2Int cellPos in aoeCells)
            {
                SquareCell cell = Grid.GetCell(cellPos);
                if (cell == null) continue;

                if (cell.IsOccupied && cell.Occupant != null && !cell.Occupant.Stats.IsDead)
                {
                    bool isAlly = IsAllyTeam(caster, cell.Occupant);
                    cell.SetHighlight(isAlly ? HighlightType.AoEAlly : HighlightType.AoETarget);
                }
                else
                {
                    cell.SetHighlight(HighlightType.AoEPreview);
                }
            }

            // Get all valid targets
            bool casterIsPC = caster.Team == CharacterTeam.Player;
            CharacterTeam enemyTeamType = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
            List<CharacterController> allyTeam = GetTeamMembers(caster.Team);
            List<CharacterController> enemyTeam = GetTeamMembers(enemyTeamType);
            List<CharacterController> targets = AoESystem.GetTargetsInArea(
                aoeCells, caster, allyTeam, enemyTeam,
                spell.AoEFilter, casterIsPC, Grid);

            Debug.Log($"[AoE] Self-centered {spell.Name}: {aoeCells.Count} cells, {targets.Count} targets — awaiting confirmation");

            // Store state for confirmation
            _isConfirmingSelfAoE = true;
            _pendingSelfAoECells = aoeCells;
            _pendingSelfAoETargets = targets;
            CurrentSubPhase = PlayerSubPhase.ConfirmingSelfAoE;
            CombatUI.SetActionButtonsVisible(false);

            // Show turn indicator with confirm/cancel instructions
            CombatUI.SetTurnIndicator($"Casting {spell.Name} — Left-click to confirm, Right-click to cancel");
            return;
        }

        _isAoETargeting = true;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _pendingAttackMode = PendingAttackMode.CastSpell;
        CurrentSubPhase = PlayerSubPhase.SelectingAoETarget;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        // For burst spells with range > 0, show the valid placement range
        if (spell.AoEShapeType == AoEShape.Burst)
        {
            int range = placementRange;
            if (range <= 0) range = 1;

            List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, range);
            foreach (var cell in rangeCells)
            {
                cell.SetHighlight(HighlightType.SpellRange);
            }
            // Also highlight the caster's full occupied footprint as valid.
            HighlightCharacterFootprint(caster, HighlightType.SpellRange);

            string rangeStr = $"{range * 5} ft";
            if (string.Equals(spell.SpellId, SpellNames.FLAMING_SPHERE, StringComparison.Ordinal))
            {
                CombatUI.SetTurnIndicator($"✦ Select target location for Flaming Sphere | Range: {rangeStr} (Medium) | Click destination cell | Right-click to cancel");
            }
            else
            {
                string sizeStr = $"{spell.AoESizeSquares * 5}-ft radius burst";
                CombatUI.SetTurnIndicator($"✦ {spell.Name}: Aim {sizeStr} | Range: {rangeStr} | Move mouse to preview, click to cast | Right-click to cancel");
            }
        }
        // For cone spells, highlight the caster footprint; direction is determined by mouse.
        else if (spell.AoEShapeType == AoEShape.Cone)
        {
            HighlightCharacterFootprint(caster, HighlightType.Selected);

            string sizeStr = $"{spell.AoESizeSquares * 5}-ft cone";
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: Aim {sizeStr} from caster | Move mouse to aim, click to cast | Right-click to cancel");
        }
        else if (spell.AoEShapeType == AoEShape.Line)
        {
            // ── Wall of Fire Line Mode: show placement range, two-click targeting ──
            bool isWallOfFireLine = IsPendingWallOfFire() && _pendingWallOfFireMode == WallOfFireMode.Line;
            if (isWallOfFireLine)
            {
                int spellRange = GetWallOfFireRangeSquares(caster);
                List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, spellRange);
                foreach (SquareCell cell in rangeCells)
                {
                    if (cell == null) continue;
                    cell.SetHighlight(HighlightType.SpellRange);
                }
                HighlightCharacterFootprint(caster, HighlightType.Selected);

                int maxLen = GetWallOfFireMaxLengthSquares(caster);
                if (_pendingWallLineStart.HasValue)
                {
                    // Second click mode
                    CombatUI.SetTurnIndicator(
                        $"✦ Wall of Fire (Line): Click END point for the wall (max {maxLen * 5} ft from start) | Right-click to cancel");
                }
                else
                {
                    // First click mode
                    CombatUI.SetTurnIndicator(
                        $"✦ Wall of Fire (Line): Click START point within range | Wall up to {maxLen * 5} ft long | Right-click to cancel");
                }
            }
            // ── Wall of Fire Ring Mode: show placement range, click center ──
            else if (IsPendingWallOfFire() && _pendingWallOfFireMode == WallOfFireMode.Ring)
            {
                int spellRange = GetWallOfFireRangeSquares(caster);
                List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, spellRange);
                foreach (SquareCell cell in rangeCells)
                {
                    if (cell == null) continue;
                    cell.SetHighlight(HighlightType.SpellRange);
                }
                HighlightCharacterFootprint(caster, HighlightType.Selected);

                int maxRad = GetWallOfFireMaxRingRadius(caster);
                CombatUI.SetTurnIndicator(
                    $"✦ Wall of Fire (Ring): Click CENTER point within range | Max radius: {maxRad * 5} ft | Right-click to cancel");
            }
            // ── Wall of Ice Line Mode: show placement range, two-click targeting ──
            else if (IsPendingWallOfIce() && _pendingWallOfIceMode == WallOfIceMode.Line)
            {
                int spellRange = GetWallOfIceRangeSquares(caster);
                List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, spellRange);
                foreach (SquareCell cell in rangeCells)
                {
                    if (cell == null) continue;
                    cell.SetHighlight(HighlightType.SpellRange);
                }
                HighlightCharacterFootprint(caster, HighlightType.Selected);

                int maxLen = GetWallOfIceMaxLengthSquares(caster);
                if (_pendingWallOfIceLineStart.HasValue)
                {
                    CombatUI.SetTurnIndicator(
                        $"✦ Wall of Ice (Line): Click END point for the wall (max {maxLen * 5} ft from start) | Right-click to cancel");
                }
                else
                {
                    CombatUI.SetTurnIndicator(
                        $"✦ Wall of Ice (Line): Click START point within range | Wall up to {maxLen * 5} ft long | Right-click to cancel");
                }
            }
            // ── Wall of Ice Circle Mode: show placement range, click center ──
            else if (IsPendingWallOfIce() && _pendingWallOfIceMode == WallOfIceMode.Circle)
            {
                int spellRange = GetWallOfIceRangeSquares(caster);
                List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, spellRange);
                foreach (SquareCell cell in rangeCells)
                {
                    if (cell == null) continue;
                    cell.SetHighlight(HighlightType.SpellRange);
                }
                HighlightCharacterFootprint(caster, HighlightType.Selected);

                int maxRad = GetWallOfIceMaxCircleRadius(caster);
                CombatUI.SetTurnIndicator(
                    $"✦ Wall of Ice (Hemisphere): Click CENTER point within range | Max radius: {maxRad * 5} ft | Right-click to cancel");
            }
            else
            {
                // Normal line spell (e.g., Lightning Bolt)
                int range = Mathf.Max(1, spell.AoESizeSquares);
                List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, range);
                foreach (SquareCell cell in rangeCells)
                {
                    if (cell == null || cell.Coords == caster.GridPosition)
                        continue;
                    cell.SetHighlight(HighlightType.SpellRange);
                }

                HighlightCharacterFootprint(caster, HighlightType.Selected);

                string sizeStr = $"{spell.AoESizeSquares * 5}-ft line";
                CombatUI.SetTurnIndicator($"✦ {spell.Name}: Select target for line ({sizeStr}) | Click endpoint cell within range | Right-click to cancel");
            }
        }

        Debug.Log($"[AoE] Entered AoE targeting mode: {spell.Name} ({spell.AoEShapeType}, {spell.AoESizeSquares} sq)");
    }

    /// <summary>
    /// Get the current mouse position in world coordinates.
    /// Used by AoE targeting previews (including line spell endpoint selection).
    /// </summary>
    private Vector2 GetMouseWorldPosition()
    {
        if (_inputService != null)
            return _inputService.GetMouseWorldPosition();

        if (_mainCam == null)
            return Vector2.zero;

        return _mainCam.ScreenToWorldPoint(Input.mousePosition);
    }

    /// <summary>
    /// Called every frame during AoE targeting to update the preview overlay
    /// based on the current mouse position.
    /// </summary>
    private void UpdateAoEPreview()
    {
        if (!_isAoETargeting || _pendingSpell == null) return;

        CharacterController pc = ActivePC;
        if (pc == null) return;

        // Get mouse position in world coordinates
        Vector2 worldPoint = GetMouseWorldPosition();
        if (worldPoint == Vector2.zero && _mainCam == null) return;

        if (TryUpdateGreaseAreaPreview(pc, worldPoint))
            return;

        HashSet<Vector2Int> aoeCells = null;

        // ===== LINE SPELLS: click endpoint targeting =====
        // Player picks an endpoint cell within range; preview draws the line
        // from caster to that hovered endpoint cell.
        if (_pendingSpell.AoEShapeType == AoEShape.Line)
        {
            Vector2Int gridPos = SquareGridUtils.WorldToGrid(worldPoint);

            // ── Wall of Fire Line Mode: DIRECTION SELECTION PHASE ──
            // After wall placement, mouse position determines which side radiates heat.
            // We show wall cells in red and heat wave cells (preview) on the hovered side.
            if (IsPendingWallOfFireLineDirectionPhase())
            {
                if (gridPos == _lastLineHoverKey) return;
                _lastLineHoverKey = gridPos;

                // Clear previous preview
                if (_currentAoECells != null)
                {
                    foreach (Vector2Int c in _currentAoECells)
                    {
                        SquareCell sc = Grid.GetCell(c);
                        if (sc != null) sc.SetHighlight(HighlightType.None);
                    }
                }

                // Re-highlight the wall cells
                foreach (Vector2Int wc in _pendingWallLineCellsForDirection)
                {
                    SquareCell sc = Grid.GetCell(wc);
                    if (sc != null) sc.SetHighlight(HighlightType.AoETarget);
                }

                // Determine which side of the line the mouse is on
                Vector2Int lineStart = _pendingWallLineStart ?? Vector2Int.zero;
                Vector2Int lineEnd = lineStart;
                if (_pendingWallLineCellsForDirection.Count > 0)
                {
                    int maxDist = 0;
                    foreach (var cell in _pendingWallLineCellsForDirection)
                    {
                        int d = SquareGridUtils.GetDistance(lineStart, cell);
                        if (d > maxDist) { maxDist = d; lineEnd = cell; }
                    }
                }

                int side = GetSideOfLine(lineStart, lineEnd, gridPos);
                if (side != 0)
                {
                    // Show heat wave preview cells on the hovered side (10 ft = 2 squares)
                    HashSet<Vector2Int> heatCells = GetHeatWaveCellsForLineSide(
                        lineStart, lineEnd, _pendingWallLineCellsForDirection, side, 2);

                    foreach (Vector2Int hc in heatCells)
                    {
                        SquareCell sc = Grid.GetCell(hc);
                        if (sc != null) sc.SetHighlight(HighlightType.AoEPreview);
                    }

                    // Track all highlighted cells for clearing
                    var allCells = new HashSet<Vector2Int>(_pendingWallLineCellsForDirection);
                    allCells.UnionWith(heatCells);
                    _currentAoECells = allCells;
                }
                else
                {
                    // Mouse is on the line itself — just show wall cells
                    _currentAoECells = new HashSet<Vector2Int>(_pendingWallLineCellsForDirection);
                }
                return; // Line direction phase handles its own highlighting
            }

            // ── Wall of Fire Ring Mode: DIRECTION SELECTION PHASE ──
            // After ring placement, mouse position (inside/outside ring) determines heat direction.
            // We show ring cells in red and heat wave cells (preview) on the hovered side.
            if (IsPendingWallOfFireRingDirectionPhase())
            {
                if (gridPos == _lastLineHoverKey) return;
                _lastLineHoverKey = gridPos;

                // Clear previous preview
                if (_currentAoECells != null)
                {
                    foreach (Vector2Int c in _currentAoECells)
                    {
                        SquareCell sc = Grid.GetCell(c);
                        if (sc != null) sc.SetHighlight(HighlightType.None);
                    }
                }

                // Re-highlight the ring cells
                foreach (Vector2Int rc in _pendingWallRingCellsForDirection)
                {
                    SquareCell sc = Grid.GetCell(rc);
                    if (sc != null) sc.SetHighlight(HighlightType.AoETarget);
                }

                // Determine if mouse is inside or outside the ring
                bool mouseInside = IsInsideRing(gridPos, _pendingWallRingCenterForDirection, _pendingWallRingRadiusForDirection);

                // Don't show preview if mouse is on the ring itself
                bool mouseOnRing = _pendingWallRingCellsForDirection.Contains(gridPos);

                Debug.Log($"[WallOfFire][RingDir] Preview update: gridPos=({gridPos.x},{gridPos.y}), mouseInside={mouseInside}, mouseOnRing={mouseOnRing}");

                if (!mouseOnRing)
                {
                    // Show heat wave preview cells on the hovered side (10 ft = 2 squares)
                    HashSet<Vector2Int> heatCells = GetHeatWaveCellsForRingSide(
                        _pendingWallRingCenterForDirection, _pendingWallRingRadiusForDirection,
                        _pendingWallRingCellsForDirection, mouseInside, 2);

                    Debug.Log($"[WallOfFire][RingDir] Heat wave preview: side={(mouseInside ? "Inside" : "Outside")}, heatCells={heatCells.Count}");

                    foreach (Vector2Int hc in heatCells)
                    {
                        SquareCell sc = Grid.GetCell(hc);
                        if (sc != null) sc.SetHighlight(HighlightType.AoEPreview);
                    }

                    // Track all highlighted cells for clearing
                    var allCells = new HashSet<Vector2Int>(_pendingWallRingCellsForDirection);
                    allCells.UnionWith(heatCells);
                    _currentAoECells = allCells;
                }
                else
                {
                    // Mouse is on the ring itself — just show ring cells
                    _currentAoECells = new HashSet<Vector2Int>(_pendingWallRingCellsForDirection);
                }
                return; // Ring direction phase handles its own highlighting
            }

            if (gridPos == _lastLineHoverKey) return;
            _lastLineHoverKey = gridPos;

            ClearAoEPreviewHighlights();

            // ── Wall of Fire Line Mode: preview from first-click start to mouse ──
            if (IsPendingWallOfFireLineSecondClick())
            {
                Vector2Int start = _pendingWallLineStart.Value;
                int maxLen = GetWallOfFireMaxLengthSquares(pc);
                int distFromStart = SquareGridUtils.GetDistance(start, gridPos);
                if (distFromStart > maxLen) return; // Out of wall length range

                aoeCells = AoESystem.GetLineCellsBetweenPoints(start, gridPos, maxLen, Grid);
            }
            // ── Wall of Fire Ring Mode: preview ring at hovered center ──
            else if (IsPendingWallOfFire() && _pendingWallOfFireMode == WallOfFireMode.Ring)
            {
                int spellRange = GetWallOfFireRangeSquares(pc);
                if (!AoESystem.IsWithinCastingRange(pc.GridPosition, gridPos, spellRange))
                    return;
                // Show a small ring preview at radius 1 while hovering center
                int previewRadius = Mathf.Min(2, GetWallOfFireMaxRingRadius(pc));
                aoeCells = AoESystem.GetRingCells(gridPos, previewRadius, Grid);
            }
            // ── Wall of Fire Line Mode first click: just show hovered cell ──
            else if (IsPendingWallOfFire() && _pendingWallOfFireMode == WallOfFireMode.Line && !_pendingWallLineStart.HasValue)
            {
                int spellRange = GetWallOfFireRangeSquares(pc);
                if (!AoESystem.IsWithinCastingRange(pc.GridPosition, gridPos, spellRange))
                    return;
                aoeCells = new HashSet<Vector2Int> { gridPos };
            }
            // ── Wall of Ice Line Mode: preview from first-click start to mouse ──
            else if (IsPendingWallOfIceLineSecondClick())
            {
                Vector2Int start = _pendingWallOfIceLineStart.Value;
                int maxLen = GetWallOfIceMaxLengthSquares(pc);
                int distFromStart = SquareGridUtils.GetDistance(start, gridPos);
                if (distFromStart > maxLen) return;

                aoeCells = AoESystem.GetLineCellsBetweenPoints(start, gridPos, maxLen, Grid);
            }
            // ── Wall of Ice Circle Mode: preview ring at hovered center ──
            else if (IsPendingWallOfIce() && _pendingWallOfIceMode == WallOfIceMode.Circle)
            {
                int spellRange = GetWallOfIceRangeSquares(pc);
                if (!AoESystem.IsWithinCastingRange(pc.GridPosition, gridPos, spellRange))
                    return;
                int previewRadius = Mathf.Min(2, GetWallOfIceMaxCircleRadius(pc));
                aoeCells = AoESystem.GetRingCells(gridPos, previewRadius, Grid);
            }
            // ── Wall of Ice Line Mode first click: just show hovered cell ──
            else if (IsPendingWallOfIce() && _pendingWallOfIceMode == WallOfIceMode.Line && !_pendingWallOfIceLineStart.HasValue)
            {
                int spellRange = GetWallOfIceRangeSquares(pc);
                if (!AoESystem.IsWithinCastingRange(pc.GridPosition, gridPos, spellRange))
                    return;
                aoeCells = new HashSet<Vector2Int> { gridPos };
            }
            // ── Normal line spell (Lightning Bolt, etc.) ──
            else
            {
                if (!AoESystem.IsWithinCastingRange(pc.GridPosition, gridPos, _pendingSpell.AoESizeSquares))
                    return;

                aoeCells = AoESystem.GetLineCellsToTarget(
                    pc.GridPosition, gridPos, _pendingSpell.AoESizeSquares, Grid);
            }
        }
        else
        {
            Vector2Int gridPos = SquareGridUtils.WorldToGrid(worldPoint);

            if (_pendingSpell.AoEShapeType == AoEShape.Burst)
            {
                // Burst preview only depends on hovered grid cell center.
                if (gridPos == _lastAoEHoverPos) return;
                _lastAoEHoverPos = gridPos;

                ClearAoEPreviewHighlights();

                int range = _pendingSpell.AoERangeSquares > 0
                    ? _pendingSpell.AoERangeSquares
                    : _pendingSpell.GetRangeSquaresForCasterLevel(pc?.Stats?.GetCasterLevel() ?? 0);
                if (!AoESystem.IsWithinCastingRange(pc.GridPosition, gridPos, range))
                    return;
                aoeCells = AoESystem.GetBurstCells(gridPos, _pendingSpell.AoESizeSquares, Grid);
            }
            else if (_pendingSpell.AoEShapeType == AoEShape.Cone)
            {
                // Cone preview depends on both target cell (direction snap) and
                // precise mouse position (cardinal first-row tilt), so track both.
                Vector2Int coneHoverKey = new Vector2Int(
                    Mathf.RoundToInt(worldPoint.x * 4f),
                    Mathf.RoundToInt(worldPoint.y * 4f));

                if (gridPos == _lastAoEHoverPos && coneHoverKey == _lastConeHoverKey) return;

                _lastAoEHoverPos = gridPos;
                _lastConeHoverKey = coneHoverKey;

                ClearAoEPreviewHighlights();
                aoeCells = AoESystem.GetConeCells(
                    pc.GridPosition,
                    gridPos,
                    _pendingSpell.AoESizeSquares,
                    Grid,
                    worldPoint);
            }
        }

        if (aoeCells == null || aoeCells.Count == 0) return;

        // ── Wall of Ice Line-of-Effect filtering for AoE PREVIEW ──
        // Remove cells that are blocked by an intact Wall of Ice so the
        // player sees only the squares that will actually be affected.
        {
            Vector2Int loeOrigin;
            if (_pendingSpell.AoEShapeType == AoEShape.Burst)
            {
                // For bursts the origin is the burst center (hovered grid cell)
                Vector2Int gridPosForOrigin = SquareGridUtils.WorldToGrid(GetMouseWorldPosition());
                loeOrigin = gridPosForOrigin;
            }
            else
            {
                // Cone / Line: origin is caster position
                loeOrigin = pc.GridPosition;
            }
            AoESystem.FilterCellsByLineOfEffect(aoeCells, loeOrigin);
        }

        if (aoeCells.Count == 0) return;

        _currentAoECells = aoeCells;


        // Highlight the AoE cells with color-coded feedback
        foreach (Vector2Int cellPos in aoeCells)
        {
            SquareCell cell = Grid.GetCell(cellPos);
            if (cell == null) continue;

            if (cell.IsOccupied && cell.Occupant != null && !cell.Occupant.Stats.IsDead)
            {
                CharacterController occupant = cell.Occupant;
                bool isAlly = IsAllyTeam(pc, occupant);
                bool isEnemy = IsEnemyTeam(pc, occupant);

                if (_pendingSpell.AoEFilter == AoETargetFilter.AlliesOnly && isAlly)
                    cell.SetHighlight(HighlightType.AoEAlly);
                else if (_pendingSpell.AoEFilter == AoETargetFilter.EnemiesOnly && isEnemy)
                    cell.SetHighlight(HighlightType.AoETarget);
                else if (_pendingSpell.AoEFilter == AoETargetFilter.All)
                {
                    cell.SetHighlight(isEnemy ? HighlightType.AoETarget : HighlightType.AoEAlly);
                }
                else
                    cell.SetHighlight(HighlightType.AoEPreview);
            }
            else
            {
                cell.SetHighlight(HighlightType.AoEPreview);
            }
        }
    }

    /// <summary>
    /// Clear only the AoE preview highlights, keeping the spell range highlights intact.
    /// </summary>
    private void ClearAoEPreviewHighlights()
    {
        if (_currentAoECells == null) return;

        CharacterController pc = ActivePC;
        Vector2Int casterPos = pc != null ? pc.GridPosition : Vector2Int.zero;

        foreach (Vector2Int cellPos in _currentAoECells)
        {
            SquareCell cell = Grid.GetCell(cellPos);
            if (cell == null) continue;

            // Restore base range highlights (burst and line), otherwise clear.
            if (_pendingSpell != null && _pendingSpell.AoEShapeType == AoEShape.Burst)
            {
                int range = _pendingSpell.AoERangeSquares > 0
                    ? _pendingSpell.AoERangeSquares
                    : _pendingSpell.GetRangeSquaresForCasterLevel(pc?.Stats?.GetCasterLevel() ?? 0);
                int dist = SquareGridUtils.GetDistance(casterPos, cellPos);
                if (dist <= range)
                    cell.SetHighlight(HighlightType.SpellRange);
                else
                    cell.SetHighlight(HighlightType.None);
            }
            else if (_pendingSpell != null && _pendingSpell.AoEShapeType == AoEShape.Line)
            {
                // Wall of Fire/Ice uses spell range (Medium) for placement; normal lines use AoESizeSquares
                int lineRange = IsPendingWallOfFire()
                    ? GetWallOfFireRangeSquares(pc)
                    : IsPendingWallOfIce()
                        ? GetWallOfIceRangeSquares(pc)
                        : Mathf.Max(1, _pendingSpell.AoESizeSquares);
                int dist = SquareGridUtils.GetDistance(casterPos, cellPos);
                if (dist <= lineRange)
                    cell.SetHighlight(HighlightType.SpellRange);
                else
                    cell.SetHighlight(HighlightType.None);
            }
            else
            {
                cell.SetHighlight(HighlightType.None);
            }
        }

        _currentAoECells = null;
    }

    /// <summary>
    /// Handle a click during AoE targeting mode.
    /// Confirms the AoE placement and resolves the spell.
    /// </summary>
    private void HandleAoETargetClick(CharacterController caster, SquareCell clickedCell)
    {
        if (_pendingSpell == null || !_isAoETargeting) return;

        if (TryHandleGreaseAreaTargetClick(caster, clickedCell))
            return;

        Vector2Int targetPos = clickedCell.Coords;

        // Validate range for burst and line spells
        if (_pendingSpell.AoEShapeType == AoEShape.Burst)
        {
            int range = _pendingSpell.AoERangeSquares > 0
                ? _pendingSpell.AoERangeSquares
                : _pendingSpell.GetRangeSquaresForCasterLevel(caster?.Stats?.GetCasterLevel() ?? 0);
            if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, range))
            {
                Debug.Log($"[AoE] Target position ({targetPos.x},{targetPos.y}) is out of range for burst");
                return; // Don't cancel, just ignore out-of-range clicks
            }
        }
        else if (_pendingSpell.AoEShapeType == AoEShape.Line)
        {
            // ── Wall of Fire: use spell range (Medium) instead of AoESizeSquares ──
            if (IsPendingWallOfFire())
            {
                int spellRange = GetWallOfFireRangeSquares(caster);

                // ── LINE MODE: two-click targeting ──
                if (_pendingWallOfFireMode == WallOfFireMode.Line)
                {
                    // ── DIRECTION PHASE: click to confirm heat wave side ──
                    if (IsPendingWallOfFireLineDirectionPhase())
                    {
                        Vector2Int lineStart = _pendingWallLineStart ?? Vector2Int.zero;
                        Vector2Int lineEnd = lineStart;
                        if (_pendingWallLineCellsForDirection != null && _pendingWallLineCellsForDirection.Count > 0)
                        {
                            int maxDist = 0;
                            foreach (var cell in _pendingWallLineCellsForDirection)
                            {
                                int d = SquareGridUtils.GetDistance(lineStart, cell);
                                if (d > maxDist) { maxDist = d; lineEnd = cell; }
                            }
                        }

                        int clickSide = GetSideOfLine(lineStart, lineEnd, targetPos);
                        if (clickSide == 0)
                        {
                            CombatUI?.ShowCombatLog("⚠ Click on one side of the wall, not on the wall itself.");
                            return;
                        }

                        ConfirmWallOfFireLineDirection(caster, clickSide);
                        return;
                    }

                    if (!_pendingWallLineStart.HasValue)
                    {
                        // FIRST CLICK: set start point (must be within spell range of caster)
                        if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, spellRange))
                        {
                            Debug.Log($"[WallOfFire] Start point ({targetPos.x},{targetPos.y}) out of spell range");
                            return;
                        }

                        _pendingWallLineStart = targetPos;
                        CombatUI?.ShowCombatLog($"🔥 Wall start set at ({targetPos.x}, {targetPos.y}). Click END point.");

                        // Highlight start cell and update instructions
                        SquareCell startCell = Grid.GetCell(targetPos);
                        if (startCell != null) startCell.SetHighlight(HighlightType.AoETarget);

                        int maxLen = GetWallOfFireMaxLengthSquares(caster);
                        CombatUI.SetTurnIndicator(
                            $"✦ Wall of Fire (Line): Click END point (max {maxLen * 5} ft from start) | Right-click to cancel");
                        return; // Wait for second click
                    }
                    else
                    {
                        // SECOND CLICK: set end point
                        // End point must be within wall max length of start AND within spell range of caster
                        int maxLen = GetWallOfFireMaxLengthSquares(caster);
                        Vector2Int start = _pendingWallLineStart.Value;
                        int distFromStart = SquareGridUtils.GetDistance(start, targetPos);

                        if (distFromStart > maxLen)
                        {
                            Debug.Log($"[WallOfFire] End point ({targetPos.x},{targetPos.y}) too far from start ({distFromStart} > {maxLen})");
                            CombatUI?.ShowCombatLog($"⚠ End point too far from start (max {maxLen * 5} ft). Click closer.");
                            return;
                        }

                        if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, spellRange))
                        {
                            Debug.Log($"[WallOfFire] End point ({targetPos.x},{targetPos.y}) out of spell range");
                            return;
                        }

                        // Compute final wall cells
                        HashSet<Vector2Int> wallCells = AoESystem.GetLineCellsBetweenPoints(start, targetPos, maxLen, Grid);
                        Debug.Log($"[WallOfFire] Line mode: start=({start.x},{start.y}), end=({targetPos.x},{targetPos.y}), cells={wallCells.Count}");

                        if (wallCells.Count == 0)
                        {
                            Debug.Log("[WallOfFire] No valid cells for wall line");
                            return;
                        }

                        // Get targets
                        bool casterIsPC2 = caster.Team == CharacterTeam.Player;
                        CharacterTeam enemyTeamType2 = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
                        List<CharacterController> allyTeam2 = GetTeamMembers(caster.Team);
                        List<CharacterController> enemyTeam2 = GetTeamMembers(enemyTeamType2);
                        List<CharacterController> targets2 = AoESystem.GetTargetsInArea(
                            wallCells, caster, allyTeam2, enemyTeam2,
                            _pendingSpell.AoEFilter, casterIsPC2, Grid);

                        // Enter heat wave direction selection phase (PHB p.298)
                        // Player must choose which side of the wall radiates heat
                        Grid.ClearAllHighlights();
                        EnterWallOfFireLineDirectionPhase(caster, wallCells, targets2);
                        return;
                    }
                }
                // ── RING MODE ──
                else if (_pendingWallOfFireMode == WallOfFireMode.Ring)
                {
                    // ── DIRECTION PHASE: click to confirm heat wave direction ──
                    if (IsPendingWallOfFireRingDirectionPhase())
                    {
                        Debug.Log($"[WallOfFire][RingDir] Click detected at ({targetPos.x},{targetPos.y}) during ring direction phase");

                        // Check if click is on the ring itself
                        if (_pendingWallRingCellsForDirection.Contains(targetPos))
                        {
                            Debug.Log($"[WallOfFire][RingDir] Click was on ring itself — ignored");
                            CombatUI?.ShowCombatLog("⚠ Click inside or outside the ring, not on the ring itself.");
                            return;
                        }

                        bool clickInside = IsInsideRing(targetPos, _pendingWallRingCenterForDirection, _pendingWallRingRadiusForDirection);
                        Debug.Log($"[WallOfFire][RingDir] Click confirmed: {(clickInside ? "Inside (Inwards)" : "Outside (Outwards)")}");
                        ConfirmWallOfFireRingDirection(caster, clickInside);
                        return;
                    }

                    // ── CENTER SELECTION: click center, then radius prompt ──
                    if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, spellRange))
                    {
                        Debug.Log($"[WallOfFire] Ring center ({targetPos.x},{targetPos.y}) out of spell range");
                        return;
                    }

                    // Show radius selection prompt
                    ShowWallOfFireRadiusSelection(caster, targetPos);
                    return;
                }
            }

            // ── Wall of Ice: use spell range (Medium) instead of AoESizeSquares ──
            if (IsPendingWallOfIce())
            {
                int spellRange = GetWallOfIceRangeSquares(caster);

                // ── LINE MODE: two-click targeting ──
                if (_pendingWallOfIceMode == WallOfIceMode.Line)
                {
                    if (!_pendingWallOfIceLineStart.HasValue)
                    {
                        // FIRST CLICK: set start point
                        if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, spellRange))
                        {
                            Debug.Log($"[WallOfIce] Start point ({targetPos.x},{targetPos.y}) out of spell range");
                            return;
                        }

                        _pendingWallOfIceLineStart = targetPos;
                        CombatUI?.ShowCombatLog($"❄ Wall start set at ({targetPos.x}, {targetPos.y}). Click END point.");

                        SquareCell startCell = Grid.GetCell(targetPos);
                        if (startCell != null) startCell.SetHighlight(HighlightType.AoETarget);

                        int maxLen = GetWallOfIceMaxLengthSquares(caster);
                        CombatUI.SetTurnIndicator(
                            $"✦ Wall of Ice (Line): Click END point (max {maxLen * 5} ft from start) | Right-click to cancel");
                        return; // Wait for second click
                    }
                    else
                    {
                        // SECOND CLICK: set end point
                        int maxLen = GetWallOfIceMaxLengthSquares(caster);
                        Vector2Int start = _pendingWallOfIceLineStart.Value;
                        int distFromStart = SquareGridUtils.GetDistance(start, targetPos);

                        if (distFromStart > maxLen)
                        {
                            Debug.Log($"[WallOfIce] End point ({targetPos.x},{targetPos.y}) too far from start ({distFromStart} > {maxLen})");
                            CombatUI?.ShowCombatLog($"⚠ End point too far from start (max {maxLen * 5} ft). Click closer.");
                            return;
                        }

                        if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, spellRange))
                        {
                            Debug.Log($"[WallOfIce] End point ({targetPos.x},{targetPos.y}) out of spell range");
                            return;
                        }

                        // Compute final wall cells
                        HashSet<Vector2Int> wallCells = AoESystem.GetLineCellsBetweenPoints(start, targetPos, maxLen, Grid);
                        Debug.Log($"[WallOfIce] Line mode: start=({start.x},{start.y}), end=({targetPos.x},{targetPos.y}), cells={wallCells.Count}");

                        if (wallCells.Count == 0)
                        {
                            Debug.Log("[WallOfIce] No valid cells for wall line");
                            return;
                        }

                        // Get targets
                        bool casterIsPC3 = caster.Team == CharacterTeam.Player;
                        CharacterTeam enemyTeamType3 = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
                        List<CharacterController> allyTeam3 = GetTeamMembers(caster.Team);
                        List<CharacterController> enemyTeam3 = GetTeamMembers(enemyTeamType3);
                        List<CharacterController> targets3 = AoESystem.GetTargetsInArea(
                            wallCells, caster, allyTeam3, enemyTeam3,
                            _pendingSpell.AoEFilter, casterIsPC3, Grid);

                        // No direction selection for Wall of Ice — proceed directly to cast
                        _isAoETargeting = false;
                        _currentAoECells = null;
                        _lastAoEHoverPos = new Vector2Int(-1, -1);
                        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
                        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);

                        PerformAoESpellCast(caster, targets3, wallCells);
                        return;
                    }
                }
                // ── CIRCLE MODE ──
                else if (_pendingWallOfIceMode == WallOfIceMode.Circle)
                {
                    // CENTER SELECTION: click center, then radius prompt
                    if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, spellRange))
                    {
                        Debug.Log($"[WallOfIce] Circle center ({targetPos.x},{targetPos.y}) out of spell range");
                        return;
                    }

                    // Show radius selection prompt
                    ShowWallOfIceRadiusSelection(caster, targetPos);
                    return;
                }
            }

            // Normal line spell range check
            int lineRange = Mathf.Max(1, _pendingSpell.AoESizeSquares);
            if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, lineRange))
            {
                Debug.Log($"[AoE] Target position ({targetPos.x},{targetPos.y}) is out of range for line");
                return;
            }
        }

        // Calculate the final AoE cells
        HashSet<Vector2Int> aoeCells = null;
        Vector2 worldPoint = GetMouseWorldPosition();

        if (_pendingSpell.AoEShapeType == AoEShape.Burst)
        {
            aoeCells = AoESystem.GetBurstCells(targetPos, _pendingSpell.AoESizeSquares, Grid);
        }
        else if (_pendingSpell.AoEShapeType == AoEShape.Cone)
        {
            aoeCells = AoESystem.GetConeCells(
                caster.GridPosition,
                targetPos,
                _pendingSpell.AoESizeSquares,
                Grid,
                worldPoint);
        }
        else if (_pendingSpell.AoEShapeType == AoEShape.Line)
        {
            // Line spells: click endpoint targeting from caster to selected cell.
            // (Wall of Fire is handled above and returns early)
            aoeCells = AoESystem.GetLineCellsToTarget(
                caster.GridPosition, targetPos, _pendingSpell.AoESizeSquares, Grid);
            Debug.Log($"[AoE] Line endpoint ({targetPos.x},{targetPos.y}) → {aoeCells.Count} cells");
        }

        if (aoeCells == null || aoeCells.Count == 0)
        {
            Debug.Log("[AoE] No cells in AoE area");
            return;
        }

        // Get all valid targets in the AoE
        bool casterIsPC = caster.Team == CharacterTeam.Player;
        CharacterTeam enemyTeamType = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        List<CharacterController> allyTeam = GetTeamMembers(caster.Team);
        List<CharacterController> enemyTeam = GetTeamMembers(enemyTeamType);
        List<CharacterController> targets = AoESystem.GetTargetsInArea(
            aoeCells, caster, allyTeam, enemyTeam,
            _pendingSpell.AoEFilter, casterIsPC, Grid);

        Debug.Log($"[AoE] {_pendingSpell.Name}: {aoeCells.Count} cells, {targets.Count} targets");

        // Exit AoE targeting mode
        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        // Execute the AoE spell
        PerformAoESpellCast(caster, targets, aoeCells);
    }

    /// <summary>
    /// Cancel AoE targeting and return to action choices.
    /// </summary>
    private void CancelAoETargeting()
    {
        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _pendingSpell = null;
        _pendingMetamagic = null;
        _pendingSpellFromHeldCharge = false;
        _pendingAnimateRopeItem = null;
        _pendingResistEnergyType = null;
        _pendingFireShieldIsWarm = null;
        _pendingProtectionFromEnergyType = null;
        ResetPendingGreaseCastMode();
        ResetPendingWallOfFireMode();
        ResetPendingWallOfIceMode();

        Grid.ClearAllHighlights();
        ShowActionChoices();
        Debug.Log("[AoE] AoE targeting cancelled");
    }

    /// <summary>
    /// Cancel single-target spell targeting and return to action choices.
    /// Called when right-click or Escape is pressed during SelectingAttackTarget
    /// while a spell is pending.
    /// </summary>
    private void CancelSpellTargeting()
    {
        _pendingSpell = null;
        _pendingSpellFromHeldCharge = false;
        _pendingMetamagic = null;
        _pendingAnimateRopeItem = null;
        _pendingResistEnergyType = null;
        _pendingFireShieldIsWarm = null;
        _pendingProtectionFromEnergyType = null;
        _pendingSummonSelection = null;
        _pendingSummonListLevel = 0;
        _pendingSummonCountInfo = null;
        _pendingSummonSwarmNpcId = null;
        ResetPendingGreaseCastMode();
        ResetPendingWallOfFireMode();
        ResetPendingWallOfIceMode();
        _pendingAttackMode = PendingAttackMode.Single;

        Grid.ClearAllHighlights();
        ShowActionChoices();
        Debug.Log("[Spell] Spell targeting cancelled via right-click/Escape");
    }

    /// <summary>
    /// Cancel weapon attack targeting and clear any pending defensive declaration.
    /// </summary>
    private void CancelPendingAttackTargeting()
    {
        if (_isAwaitingRangedRetargetSelection)
        {
            _rangedRetargetSelectionCancelled = true;
            _selectedRangedRetarget = null;
            _isAwaitingRangedRetargetSelection = false;
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            CurrentSubPhase = PlayerSubPhase.Animating;
            CombatUI?.ShowCombatLog("↩ Remaining full-attack swings/shots cancelled.");
            return;
        }

        CharacterController pc = ActivePC;
        if (pc != null && _pendingDefensiveAttackSelection)
        {
            pc.SetFightingDefensively(false);
            CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} cancels defensive attack declaration.");
            UpdateAllStatsUI();
        }

        _pendingDefensiveAttackSelection = false;
        _pendingAttackMode = PendingAttackMode.Single;
        _skipNextSingleAttackStandardActionCommit = false;
        ClearPendingNaturalAttackSelection();
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;
        _currentOffHandBAB = 0;
        _currentOffHandWeapon = null;

        Grid.ClearAllHighlights();
        ShowActionChoices();
    }

    // ========== SELF-CENTERED AOE CONFIRMATION CALLBACKS ==========

    /// <summary>
    /// Called when the player confirms a self-centered AoE spell via left-click.
    /// Proceeds with the actual spell cast.
    /// </summary>
    private void OnSelfAoEConfirmed()
    {
        if (!_isConfirmingSelfAoE || _pendingSpell == null)
        {
            Debug.LogWarning("[AoE] OnSelfAoEConfirmed called but no pending self-AoE!");
            ShowActionChoices();
            return;
        }

        CharacterController caster = ActivePC;
        if (caster == null)
        {
            ClearSelfAoEState();
            ShowActionChoices();
            return;
        }

        Debug.Log($"[AoE] Self-centered {_pendingSpell.Name} CONFIRMED — casting on {_pendingSelfAoETargets.Count} targets");

        // Cache before clearing state
        var targets = _pendingSelfAoETargets;
        var cells = _pendingSelfAoECells;

        ClearSelfAoEState();

        // Now execute the spell (Wall of Ice LoE filtering handled inside PerformAoESpellCast)
        PerformAoESpellCast(caster, targets, cells);
    }

    /// <summary>
    /// Called when the player cancels a self-centered AoE spell via right-click/Escape.
    /// Returns to action choices without consuming the spell slot.
    /// </summary>
    private void OnSelfAoECancelled()
    {
        Debug.Log($"[AoE] Self-centered AoE spell CANCELLED — no spell slot consumed");

        ClearSelfAoEState();

        _pendingSpell = null;
        _pendingMetamagic = null;
        _pendingSpellFromHeldCharge = false;

        Grid.ClearAllHighlights();
        ShowActionChoices();
    }

    /// <summary>Clear the self-centered AoE confirmation state.</summary>
    private void ClearSelfAoEState()
    {
        _isConfirmingSelfAoE = false;
        _pendingSelfAoECells = null;
        _pendingSelfAoETargets = null;
    }

    /// <summary>
    /// Execute an AoE spell against all valid targets in the area.
    /// Handles spell slot consumption, then resolves the spell for each target.
    /// </summary>
    private void PerformAoESpellCast(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        // ── WALL OF ICE LINE-OF-EFFECT FILTERING (centralized) ──
        // Determine AoE origin for LoE checks based on spell shape:
        //   Burst spells: use centroid of AoE cells as origin (burst center)
        //   Cone/Line/other: use caster position as origin
        if (_pendingSpell != null && caster != null && aoeCells != null && aoeCells.Count > 0)
        {
            Vector2Int loeOrigin;
            if (_pendingSpell.AoEShapeType == AoEShape.Burst)
            {
                // For bursts, compute the center of the AoE cells
                int sumX = 0, sumY = 0, count = 0;
                foreach (Vector2Int c in aoeCells)
                {
                    sumX += c.x; sumY += c.y; count++;
                }
                loeOrigin = count > 0 ? new Vector2Int(sumX / count, sumY / count) : caster.GridPosition;
            }
            else
            {
                loeOrigin = caster.GridPosition;
            }

            AoESystem.FilterCellsByLineOfEffect(aoeCells, loeOrigin);
            AoESystem.FilterTargetsByLineOfEffect(targets, loeOrigin);
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        CaptureSpellcastResourceSnapshot(caster);

        // Quickened spells don't consume standard action
        bool isQuickened = _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        if (!isQuickened)
        {
            caster.CommitStandardAction();
        }
        else
        {
            var casterSpellComp = caster.GetComponent<SpellcastingComponent>();
            if (casterSpellComp != null)
                casterSpellComp.MarkQuickenedSpellCast();
        }

        // Get spellcasting component
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            ClearSpellcastResourceSnapshot();
            Debug.LogError("[GameManager] PerformAoESpellCast: No SpellcastingComponent!");
            ShowActionChoices();
            return;
        }

        // Check spontaneous casting state
        bool isSpontaneous = CombatUI != null && CombatUI.IsSpontaneousCast;
        int spontaneousLevel = isSpontaneous ? CombatUI.SpontaneousCastLevel : -1;
        string spontaneousSacrificedSpellId = isSpontaneous ? CombatUI.SpontaneousSacrificedSpellId : null;

        if (CombatUI != null)
            CombatUI.ClearSpontaneousCastState();

        // Consume spell slot (same logic as PerformSpellCast)
        int slotLevelToConsume = _pendingSpell.SpellLevel;
        bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;

        if (hasMetamagicApplied)
        {
            slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);
        }

        if (!ResolveGrappledOrPinnedCastingConcentration(
                caster,
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                isSpontaneous,
                spontaneousLevel,
                spontaneousSacrificedSpellId))
        {
            HandleConcentrationOnCasting(caster, _pendingSpell);
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();

            _pendingSpell = null;
            _pendingMetamagic = null;

            ClearSpellcastResourceSnapshot();
            StartCoroutine(AfterAttackDelay(caster, 1.0f));
            return;
        }
        if (TryRollArcaneSpellFailure(caster, _pendingSpell, false, out int asfRoll, out int asfChance))
        {
            bool consumedOnFailure = ConsumePendingSpellSlot(
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                isSpontaneous,
                spontaneousLevel,
                spontaneousSacrificedSpellId);

            if (!consumedOnFailure)
            {
                ClearSpellcastResourceSnapshot();
                Debug.LogError($"[GameManager] AoE ASF failure path: failed to consume level {slotLevelToConsume} spell slot!");
                ShowActionChoices();
                return;
            }

            HandleConcentrationOnCasting(caster, _pendingSpell);
            LogArcaneSpellFailure(caster, _pendingSpell, asfRoll, asfChance);
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();

            _pendingSpell = null;
            _pendingMetamagic = null;

            ClearSpellcastResourceSnapshot();
            StartCoroutine(AfterAttackDelay(caster, 1.0f));
            return;
        }

        bool consumed = ConsumePendingSpellSlot(
            spellComp,
            _pendingSpell,
            _pendingMetamagic,
            hasMetamagicApplied,
            slotLevelToConsume,
            isSpontaneous,
            spontaneousLevel,
            spontaneousSacrificedSpellId);

        if (!consumed)
        {
            ClearSpellcastResourceSnapshot();
            Debug.LogError($"[GameManager] AoE: Failed to consume level {slotLevelToConsume} spell slot!");
            ShowActionChoices();
            return;
        }

        // Check if caster is concentrating on another spell — casting requires a concentration check
        HandleConcentrationOnCasting(caster, _pendingSpell);

        ResolveSpellcastProvocation(caster, _pendingSpell, false, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();

            // ── COUNTERSPELL CHECK (AoE spell cast path) ──
            {
                CounterspellResult aoeCounterspellResult = TryResolveCounterspell(caster, _pendingSpell);
                if (aoeCounterspellResult != null && aoeCounterspellResult.Success)
                {
                    Debug.Log($"[Counterspell] AoE spell {_pendingSpell.Name} was countered! No effect.");
                    UpdateAllStatsUI();
                    Grid.ClearAllHighlights();

                    _pendingSpell = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingMetamagic = null;
                    _pendingAnimateRopeItem = null;
                    _pendingResistEnergyType = null;
                    _pendingFireShieldIsWarm = null;
                    _pendingProtectionFromEnergyType = null;
                    _pendingDisguiseSelfRace = null;
                    ResetPendingGreaseCastMode();

                    StartCoroutine(AfterAttackDelay(caster, 1.0f));
                    return;
                }
            }

            BreakInvisibilityOnHostileSpellCast(caster, _pendingSpell, null, targets);

            if (TryHandleConcealmentAreaSpellCast(caster, _pendingSpell, aoeCells, targets, out string concealmentAreaLog))
            {
                _lastCombatLog = concealmentAreaLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Consecrate / Desecrate area effects (GameManager_HolyAreas.cs) ──
            if (TryResolveConsecrateAreaEffect(caster, _pendingSpell, aoeCells, targets, out string consecrateLog))
            {
                _lastCombatLog = consecrateLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            if (TryResolveDesecrateAreaEffect(caster, _pendingSpell, aoeCells, targets, out string desecrateLog))
            {
                _lastCombatLog = desecrateLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // Lightning Bolt & Fireball — scalable AoE damage (1d6/CL, max 10d6)
            if (TryResolveScaledAoEDamageSpell(caster, _pendingSpell, targets, aoeCells, out string scaledDamagLog))
            {
                _lastCombatLog = scaledDamagLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                // Check for victory/defeat
                if (AreAllNPCsDead())
                {
                    HandleCombatVictoryDetected("ResolveScaledAoEDamageSpell");
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    return;
                }
                else if (AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    return;
                }

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // Daylight — persistent light area effect that counters/dispels darkness
            if (TryResolveDaylightSpell(caster, _pendingSpell, targets, aoeCells, out string daylightLog))
            {
                _lastCombatLog = daylightLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // Wind Wall — persistent line area effect that deflects ranged attacks and disperses fog/smoke
            if (TryResolveWindWallSpell(caster, _pendingSpell, targets, aoeCells, out string windWallLog))
            {
                _lastCombatLog = windWallLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // Halt Undead — paralyzes up to 3 undead within 30 ft of each other (Will save for intelligent only)
            if (TryResolveHaltUndeadSpell(caster, _pendingSpell, targets, aoeCells, out string haltUndeadLog))
            {
                _lastCombatLog = haltUndeadLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                // Check for victory/defeat (paralyzed enemies don't typically end combat, but check anyway)
                if (AreAllNPCsDead())
                {
                    HandleCombatVictoryDetected("ResolveHaltUndeadSpell");
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    return;
                }

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            if (string.Equals(_pendingSpell.SpellId, SpellNames.HYPNOTISM, StringComparison.Ordinal))
            {
                ResolveHypnotismSpell(caster, targets, aoeCells);
                return;
            }

            if (string.Equals(_pendingSpell.SpellId, SpellNames.SLEEP, StringComparison.Ordinal))
            {
                ResolveSleepSpell(caster, targets, aoeCells);
                return;
            }

            if (string.Equals(_pendingSpell.SpellId, SpellNames.DEEP_SLUMBER, StringComparison.Ordinal))
            {
                ResolveDeepSlumberSpell(caster, targets, aoeCells);
                return;
            }

            if (string.Equals(_pendingSpell.SpellId, SpellNames.COLOR_SPRAY, StringComparison.Ordinal))
            {
                ResolveColorSpraySpell(caster, targets, aoeCells);
                return;
            }

            if (string.Equals(_pendingSpell.SpellId, SpellNames.FEAR, StringComparison.Ordinal))
            {
                ResolveFearSpell(caster, targets, aoeCells);
                return;
            }

            if (TryResolveGlitterdustSpell(caster, _pendingSpell, targets, aoeCells, out string glitterdustLog))
            {
                _lastCombatLog = glitterdustLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            if (TryResolveWebSpell(caster, _pendingSpell, targets, aoeCells, out string webLog))
            {
                _lastCombatLog = webLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // Rainbow Pattern — AoE fascination (mind-affecting, up to 24 HD)
            if (TryResolveRainbowPatternAoE(caster, _pendingSpell, targets, aoeCells, out string rainbowLog))
            {
                _lastCombatLog = rainbowLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                // Begin concentration tracking for Rainbow Pattern
                if (_pendingSpell.DurationType == DurationType.Concentration)
                {
                    BeginConcentrationTracking(caster, null, _pendingSpell);
                }

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Ice Storm (AoE, no save, 3d6 bludgeoning + 2d6 cold) ──
            if (TryResolveIceStormAoE(caster, _pendingSpell, targets, aoeCells, out string iceStormLog))
            {
                _lastCombatLog = iceStormLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Shout (Cone AoE, 5d6 sonic, Fort half, deafen on fail) ──
            if (TryResolveShoutAoE(caster, _pendingSpell, targets, aoeCells, out string shoutLog))
            {
                _lastCombatLog = shoutLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Wall of Fire (line wall, persistent area effect) ──
            if (TryResolveWallOfFireSpell(caster, _pendingSpell, targets, aoeCells, out string wallOfFireLog))
            {
                _lastCombatLog = wallOfFireLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Wall of Ice (line wall, persistent area effect) ──
            // Check for adjacent creatures that can attempt Reflex save to disrupt
            if (IsWallOfIceSpell(_pendingSpell))
            {
                // Capture local copies for the callback closure
                SpellData wallSpell = _pendingSpell;
                bool wallIsSpontaneous = isSpontaneous;
                string wallSpontaneousSacrificedSpellId = spontaneousSacrificedSpellId;
                bool wallIsQuickened = isQuickened;
                List<CharacterController> wallTargets = targets;
                HashSet<Vector2Int> wallAoeCells = aoeCells;

                ResolveWallOfIceWithReflexSaves(caster, wallSpell, wallTargets, wallAoeCells, wallLog =>
                {
                    _lastCombatLog = wallLog;

                    if (wallIsSpontaneous)
                    {
                        string sacrificeInfo = !string.IsNullOrEmpty(wallSpontaneousSacrificedSpellId)
                            ? $"Sacrificed: {wallSpontaneousSacrificedSpellId}"
                            : "Converted prepared spell";
                        _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {wallSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                    }

                    if (wallIsQuickened)
                        _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {wallSpell.Name}! (Free Action)\n" + _lastCombatLog;

                    CombatUI.ShowCombatLog(_lastCombatLog);
                    UpdateAllStatsUI();
                    Grid.ClearAllHighlights();

                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    StartCoroutine(AfterAttackDelay(caster, 1.5f));
                });
                return;
            }

            if (TryResolveFlamingSphereAoECast(caster, _pendingSpell, aoeCells, out string flamingSphereLog))
            {
                _lastCombatLog = flamingSphereLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Entangle (persistent entangling area effect) ──
            if (TryResolveEntangleSpell(caster, _pendingSpell, targets, aoeCells, out string entangleLog))
            {
                _lastCombatLog = entangleLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Soften Earth and Stone (instantaneous difficult terrain) ──
            if (TryResolveSoftenEarthSpell(caster, _pendingSpell, targets, aoeCells, out string softenEarthLog))
            {
                _lastCombatLog = softenEarthLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Spike Stones (persistent movement-damage area) ──
            if (TryResolveSpikeStoneSpell(caster, _pendingSpell, targets, aoeCells, out string spikeStonesLog))
            {
                _lastCombatLog = spikeStonesLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Plant Growth (instantaneous overgrowth) ──
            if (TryResolvePlantGrowthSpell(caster, _pendingSpell, targets, aoeCells, out string plantGrowthLog))
            {
                _lastCombatLog = plantGrowthLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Calm Animals (AoE HD-budget animal calming) ──
            if (TryResolveCalmAnimalsSpell(caster, _pendingSpell, targets, aoeCells, out string calmAnimalsLog))
            {
                _lastCombatLog = calmAnimalsLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Calm Emotions (AoE morale/emotion suppression) ──
            if (TryResolveCalmEmotionsSpell(caster, _pendingSpell, targets, aoeCells, out string calmEmotionsLog))
            {
                _lastCombatLog = calmEmotionsLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            // ── Evard's Black Tentacles (persistent grappling area effect) ──
            if (TryResolveBlackTentaclesAoECast(caster, _pendingSpell, aoeCells, out string blackTentaclesLog))
            {
                _lastCombatLog = blackTentaclesLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

            if (aoeCells != null
                && string.Equals(_pendingSpell.DamageType, "fire", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Vector2Int cell in aoeCells)
                    NotifyFireDamageAtPosition(cell, _pendingSpell.Name);
            }

            // Build the combat log header
            var logBuilder = new System.Text.StringBuilder();
            string shapeStr = _pendingSpell.AoEShapeType == AoEShape.Cone ? "cone" :
                              _pendingSpell.AoEShapeType == AoEShape.Burst ? "burst" : "line";
            logBuilder.AppendLine($"═══════════════════════════════════");
            logBuilder.AppendLine($"✨ {caster.Stats.CharacterName} casts {_pendingSpell.Name}! ({_pendingSpell.AoESizeSquares * 5}-ft {shapeStr})");
            logBuilder.AppendLine($"  [{(_pendingSpell.SpellLevel == 0 ? "Cantrip" : $"Level {_pendingSpell.SpellLevel}")}] {_pendingSpell.School}");
            logBuilder.AppendLine($"  Targets: {targets.Count} creature(s) in {aoeCells.Count} squares");
            logBuilder.AppendLine();

            if (targets.Count == 0)
            {
                logBuilder.AppendLine($"  No valid targets in area!");
                logBuilder.Append($"═══════════════════════════════════");
            }
            else
            {
                // Resolve spell for each target
                int targetIndex = 0;
                foreach (CharacterController target in targets)
                {
                    targetIndex++;
                    logBuilder.AppendLine($"  --- Target {targetIndex}: {target.Stats.CharacterName} ---");

                    // ── Lesser Globe of Invulnerability — block AoE spell effects ≤ 3rd level ──
                    if (LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(_pendingSpell, target))
                    {
                        logBuilder.AppendLine($"  🛡 Blocked by Lesser Globe of Invulnerability (spell level {_pendingSpell.SpellLevel} ≤ 3)!");
                        continue;
                    }

                    // For buff/debuff/control/illusion/wall spells, apply tracked effects
                    if (_pendingSpell.EffectType == SpellEffectType.Buff || _pendingSpell.EffectType == SpellEffectType.Debuff ||
                        _pendingSpell.EffectType == SpellEffectType.Control || _pendingSpell.EffectType == SpellEffectType.Illusion ||
                        _pendingSpell.EffectType == SpellEffectType.Wall)
                    {
                        var appliedEffect = ApplySpellBuff(caster, target, _pendingSpell, spellComp);

                        // Track concentration for the first target of a concentration AoE effect
                        if (appliedEffect != null && _pendingSpell.DurationType == DurationType.Concentration && targetIndex == 1)
                        {
                            BeginConcentrationTracking(caster, appliedEffect, _pendingSpell);
                        }

                        string effectLabel = (_pendingSpell.EffectType == SpellEffectType.Debuff || _pendingSpell.EffectType == SpellEffectType.Control)
                            ? "DEBUFF APPLIED" : "BUFF APPLIED";
                        logBuilder.AppendLine($"  {effectLabel}! {_pendingSpell.Description}");
                        Debug.Log($"[AoE] {_pendingSpell.EffectType} applied to {target.Stats.CharacterName}");
                    }
                    // For damage spells, resolve with save and damage
                    else if (_pendingSpell.EffectType == SpellEffectType.Damage)
                    {
                        // VOLUNTARY SAVE FAILURE: AoE damage spells also respect voluntary save failure for allies.
                        bool aoeForceFailSave = ShouldForceTargetToAcceptSave(caster, target, _pendingSpell);
                        SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic, false, aoeForceFailSave, caster, target);

                        if (result.RequiredSave)
                        {
                            string saveResult = result.SaveSucceeded ? "SAVED" : "FAILED";
                            logBuilder.AppendLine($"  {result.SaveType} save DC {result.SaveDC}: d20={result.SaveRoll}+{result.SaveMod}={result.SaveTotal} - {saveResult}!");
                        }

                        if (result.DamageDealt > 0)
                        {
                            logBuilder.AppendLine($"  Damage: {result.DamageDealt} {result.DamageType}");
                            logBuilder.AppendLine($"  {target.Stats.CharacterName}: {result.TargetHPBefore} → {result.TargetHPAfter} HP");

                            // Check concentration for AoE spell damage
                            CheckConcentrationOnDamage(target, result.DamageDealt);
                        }

                        if (result.TargetKilled)
                        {
                            target.OnDeath();
                            HandleSummonDeathCleanup(target);
                            logBuilder.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                        }
                    }
                    // For healing spells
                    else if (_pendingSpell.EffectType == SpellEffectType.Healing)
                    {
                        // VOLUNTARY SAVE FAILURE: AoE healing spells also respect voluntary save failure for allies.
                        bool aoeHealForceFailSave = ShouldForceTargetToAcceptSave(caster, target, _pendingSpell);
                        SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic, false, aoeHealForceFailSave, caster, target);

                        logBuilder.AppendLine($"  Healed: {result.HealingDone} HP");
                        logBuilder.AppendLine($"  {target.Stats.CharacterName}: {result.TargetHPBefore} → {result.TargetHPAfter} HP");
                    }

                    logBuilder.AppendLine();
                }

                logBuilder.Append($"═══════════════════════════════════");
            }

            _lastCombatLog = logBuilder.ToString();

            if (isSpontaneous)
            {
                string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                    ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                    : "Converted prepared spell";
                _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
            }

            if (isQuickened)
            {
                _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;
            }

            if (GameManager.LogAttacksToConsole)
                Debug.Log("[AoE Spell] " + _lastCombatLog);

            CombatUI.ShowCombatLog(_lastCombatLog);
            UpdateAllStatsUI();

            Grid.ClearAllHighlights();

            // Check for victory/defeat
            if (AreAllNPCsDead())
            {
                Debug.Log("[CombatEnd] Victory condition met after AoE spell resolution.");
                HandleCombatVictoryDetected("ResolveAOESpell");
                _pendingSpell = null;
                _pendingMetamagic = null;
                return;
            }
            else if (AreAllPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
                CombatUI.SetActionButtonsVisible(false);
                _pendingSpell = null;
                _pendingMetamagic = null;
                return;
            }

            _pendingSpell = null;
            _pendingMetamagic = null;

            StartCoroutine(AfterAttackDelay(caster, 1.5f));
        });
    }

    private void ResolveHypnotismSpell(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (caster == null || caster.Stats == null || _pendingSpell == null)
            return;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int castingAbilityMod = GetSpellSaveAbilityModifier(caster, _pendingSpell);
        int saveDc = 10 + _pendingSpell.SpellLevel + castingAbilityMod;
        int hdPool = DiceService.RollMultiple(2, 4, "Hypnotism HD pool 2d4"); // 2d4
        int fascinatedRounds = DiceService.RollMultiple(2, 4, "Fascinated rounds 2d4"); // 2d4

        List<CharacterController> candidates = new List<CharacterController>();
        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(caster, target))
                continue;
            if (IsImmuneToMindAffecting(target))
                continue;

            bool canSeeCaster = target.CanSee(caster);
            bool canHearCaster = !target.HasCondition(CombatConditionType.Deafened);
            if (!canSeeCaster && !canHearCaster)
                continue;

            candidates.Add(target);
        }

        candidates.Sort((a, b) =>
        {
            int aHd = GetTargetHitDice(a);
            int bHd = GetTargetHitDice(b);
            int hdCompare = aHd.CompareTo(bHd);
            if (hdCompare != 0)
                return hdCompare;

            int distA = SquareGridUtils.GetDistance(caster.GridPosition, a.GridPosition);
            int distB = SquareGridUtils.GetDistance(caster.GridPosition, b.GridPosition);
            return distA.CompareTo(distB);
        });

        int threatenedCount = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (SpellCaster.IsBeingThreatenedBy(candidates[i], caster))
                threatenedCount++;
        }

        int saveContextModifier = 0;
        string saveContextLabel = "normal";
        if (threatenedCount >= 2)
        {
            saveContextModifier = 2;
            saveContextLabel = "+2 combat";
        }
        else if (candidates.Count == 1 && threatenedCount == 0)
        {
            saveContextModifier = -2;
            saveContextLabel = "-2 single target out of combat";
        }

        var logBuilder = new System.Text.StringBuilder();
        logBuilder.AppendLine("═══════════════════════════════════");
        logBuilder.AppendLine($"✨ {caster.Stats.CharacterName} casts Hypnotism! (15-ft burst)");
        logBuilder.AppendLine($"  HD Pool: {hdPool} | Duration: {fascinatedRounds} rounds | Will DC {saveDc} ({saveContextLabel})");
        logBuilder.AppendLine($"  Candidates: {candidates.Count} in area ({aoeCells.Count} squares)");
        logBuilder.AppendLine();

        int remainingPool = hdPool;
        int affectedCount = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController target = candidates[i];
            int targetHd = Mathf.Max(1, GetTargetHitDice(target));
            if (targetHd > remainingPool)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName} ({targetHd} HD) exceeds remaining pool ({remainingPool}) — skipped.");
                continue;
            }

            remainingPool -= targetHd;

            int srTotal = 0;
            int srRoll = 0;
            if (_pendingSpell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                srRoll = DiceService.D20("Hypnotism SR check");
                srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                if (srTotal < target.Stats.SpellResistance)
                {
                    logBuilder.AppendLine($"  • {target.Stats.CharacterName}: SR blocks effect (d20 {srRoll} + CL {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance}).");
                    continue;
                }
            }

            int saveRoll = DiceService.D20("Hypnotism Will save");
            int saveTotal = saveRoll + target.Stats.WillSave + saveContextModifier;
            bool saved = saveTotal >= saveDc;
            if (saved)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName}: Will save succeeds ({saveTotal} vs DC {saveDc}).");
                continue;
            }

            var fascinatedData = new FascinatedConditionData
            {
                Caster = caster,
                CasterName = caster.Stats.CharacterName,
                RemainingRounds = fascinatedRounds,
                DisturbanceSaveDC = saveDc,
                SourceSpellId = _pendingSpell.SpellId,
                SourceEffectName = _pendingSpell.Name
            };

            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Fascinated,
                    fascinatedRounds,
                    source: caster,
                    data: fascinatedData,
                    sourceNameOverride: _pendingSpell.Name,
                    sourceCategory: "Spell",
                    sourceId: _pendingSpell.SpellId);
            }
            else
            {
                target.ApplyCondition(CombatConditionType.Fascinated, fascinatedRounds, caster.Stats.CharacterName);
            }

            affectedCount++;
            logBuilder.AppendLine($"  • {target.Stats.CharacterName}: Fascinated for {fascinatedRounds} rounds (Will {saveTotal} vs DC {saveDc}).");
        }

        logBuilder.AppendLine();
        logBuilder.AppendLine($"  Result: {affectedCount} target(s) fascinated. Remaining HD pool: {remainingPool}.");
        logBuilder.Append("═══════════════════════════════════");

        _lastCombatLog = logBuilder.ToString();
        CombatUI?.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        if (AreAllNPCsDead())
        {
            Debug.Log("[CombatEnd] Victory condition met after Hypnotism spell resolution.");
            HandleCombatVictoryDetected("ResolveHypnotismSpell");
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        if (AreAllPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        _pendingSpell = null;
        _pendingMetamagic = null;
        StartCoroutine(AfterAttackDelay(caster, 1.5f));
    }

    private int GetSpellSaveAbilityModifier(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null)
            return 0;

        // Default by class; this keeps custom/legacy classes stable for now.
        if (caster.Stats.IsWizard)
            return caster.Stats.INTMod;
        if (caster.Stats.IsCleric)
            return caster.Stats.WISMod;

        string className = caster.Stats.CharacterClass ?? string.Empty;
        if (string.Equals(className, "Druid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Ranger", StringComparison.OrdinalIgnoreCase))
            return caster.Stats.WISMod;

        if (string.Equals(className, "Sorcerer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Bard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Paladin", StringComparison.OrdinalIgnoreCase))
            return caster.Stats.CHAMod;

        return caster.Stats.WISMod;
    }

    private void ResolveSleepSpell(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (caster == null || caster.Stats == null || _pendingSpell == null)
            return;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int castingAbilityMod = GetSpellSaveAbilityModifier(caster, _pendingSpell);
        int saveDc = 10 + _pendingSpell.SpellLevel + castingAbilityMod;
        int hdPool = DiceService.RollMultiple(4, 4, "Sleep HD pool 4d4"); // 4d4
        int sleepRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(_pendingSpell, casterLevel));

        List<CharacterController> candidates = new List<CharacterController>();
        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;
            if (IsImmuneToSleepEffects(target))
                continue;

            int targetHd = Mathf.Max(1, GetTargetHitDice(target));
            if (targetHd > 4)
                continue;

            candidates.Add(target);
        }

        candidates.Sort((a, b) =>
        {
            int aHd = GetTargetHitDice(a);
            int bHd = GetTargetHitDice(b);
            int hdCompare = aHd.CompareTo(bHd);
            if (hdCompare != 0)
                return hdCompare;

            int distA = SquareGridUtils.GetDistance(caster.GridPosition, a.GridPosition);
            int distB = SquareGridUtils.GetDistance(caster.GridPosition, b.GridPosition);
            return distA.CompareTo(distB);
        });

        var logBuilder = new System.Text.StringBuilder();
        logBuilder.AppendLine("═══════════════════════════════════");
        logBuilder.AppendLine($"✨ {caster.Stats.CharacterName} casts Sleep! (10-ft burst)");
        logBuilder.AppendLine($"  HD Pool: {hdPool} (4d4) | Duration: {sleepRounds} rounds | Will DC {saveDc}");
        logBuilder.AppendLine($"  Candidates: {candidates.Count} in area ({aoeCells.Count} squares)");
        logBuilder.AppendLine();

        int remainingPool = hdPool;
        int affectedCount = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController target = candidates[i];
            int targetHd = Mathf.Max(1, GetTargetHitDice(target));
            if (targetHd > remainingPool)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName} ({targetHd} HD) exceeds remaining pool ({remainingPool}) — skipped.");
                continue;
            }

            remainingPool -= targetHd;

            if (_pendingSpell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                int srRoll = DiceService.D20("Sleep SR check");
                int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                if (srTotal < target.Stats.SpellResistance)
                {
                    logBuilder.AppendLine($"  • {target.Stats.CharacterName}: SR blocks effect (d20 {srRoll} + CL {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance}).");
                    continue;
                }
            }

            int saveRoll = DiceService.D20("Sleep Will save");
            int saveTotal = saveRoll + target.Stats.WillSave;
            if (saveTotal >= saveDc)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName}: Will save succeeds ({saveTotal} vs DC {saveDc}).");
                continue;
            }

            ApplySleepState(caster, target, sleepRounds, saveDc, _pendingSpell);
            affectedCount++;
            logBuilder.AppendLine($"  • {target.Stats.CharacterName}: falls asleep for {sleepRounds} rounds (Will {saveTotal} vs DC {saveDc}).");
        }

        logBuilder.AppendLine();
        logBuilder.AppendLine($"  Result: {affectedCount} target(s) asleep. Remaining HD pool: {remainingPool}.");
        logBuilder.Append("═══════════════════════════════════");

        _lastCombatLog = logBuilder.ToString();
        CombatUI?.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        if (AreAllNPCsDead())
        {
            Debug.Log("[CombatEnd] Victory condition met after Sleep spell resolution.");
            HandleCombatVictoryDetected("ResolveSleepSpell");
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        if (AreAllPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        _pendingSpell = null;
        _pendingMetamagic = null;
        StartCoroutine(AfterAttackDelay(caster, 1.5f));
    }

    /// <summary>
    /// Resolve Deep Slumber spell (PHB p.217).
    /// As Sleep, but affects up to 10 HD of creatures (no per-creature HD cap).
    /// Lowest HD creatures affected first; Will negates; SR applies.
    /// </summary>
    private void ResolveDeepSlumberSpell(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (caster == null || caster.Stats == null || _pendingSpell == null)
            return;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int castingAbilityMod = GetSpellSaveAbilityModifier(caster, _pendingSpell);
        int saveDc = 10 + _pendingSpell.SpellLevel + castingAbilityMod;
        int hdPool = 10; // Deep Slumber: flat 10 HD (no dice roll, unlike Sleep's 4d4)
        int sleepRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(_pendingSpell, casterLevel));

        // Gather eligible candidates — Deep Slumber has no per-creature HD cap (unlike Sleep's 4 HD limit)
        List<CharacterController> candidates = new List<CharacterController>();
        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;
            if (IsImmuneToSleepEffects(target))
                continue;
            // No per-creature HD cap for Deep Slumber (unlike Sleep which skips > 4 HD)
            candidates.Add(target);
        }

        // Sort by HD ascending (lowest first), then by distance from caster
        candidates.Sort((a, b) =>
        {
            int aHd = GetTargetHitDice(a);
            int bHd = GetTargetHitDice(b);
            int hdCompare = aHd.CompareTo(bHd);
            if (hdCompare != 0)
                return hdCompare;

            int distA = SquareGridUtils.GetDistance(caster.GridPosition, a.GridPosition);
            int distB = SquareGridUtils.GetDistance(caster.GridPosition, b.GridPosition);
            return distA.CompareTo(distB);
        });

        var logBuilder = new System.Text.StringBuilder();
        logBuilder.AppendLine("═══════════════════════════════════");
        logBuilder.AppendLine($"✨ {caster.Stats.CharacterName} casts Deep Slumber! (10-ft burst)");
        logBuilder.AppendLine($"  HD Pool: {hdPool} (flat) | Duration: {sleepRounds} rounds | Will DC {saveDc}");
        logBuilder.AppendLine($"  Candidates: {candidates.Count} in area ({aoeCells.Count} squares)");
        logBuilder.AppendLine();

        int remainingPool = hdPool;
        int affectedCount = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController target = candidates[i];
            int targetHd = Mathf.Max(1, GetTargetHitDice(target));
            if (targetHd > remainingPool)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName} ({targetHd} HD) exceeds remaining pool ({remainingPool}) — skipped.");
                continue;
            }

            remainingPool -= targetHd;

            // Spell Resistance check
            if (_pendingSpell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                int srRoll = DiceService.D20("Color Spray SR check");
                int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                if (srTotal < target.Stats.SpellResistance)
                {
                    logBuilder.AppendLine($"  • {target.Stats.CharacterName}: SR blocks effect (d20 {srRoll} + CL {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance}).");
                    continue;
                }
            }

            // Will save
            int saveRoll = DiceService.D20("Color Spray Will save");
            int saveTotal = saveRoll + target.Stats.WillSave;
            if (saveTotal >= saveDc)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName}: Will save succeeds ({saveTotal} vs DC {saveDc}).");
                continue;
            }

            ApplySleepState(caster, target, sleepRounds, saveDc, _pendingSpell);
            affectedCount++;
            logBuilder.AppendLine($"  • {target.Stats.CharacterName}: falls into deep slumber for {sleepRounds} rounds (Will {saveTotal} vs DC {saveDc}).");
        }

        logBuilder.AppendLine();
        logBuilder.AppendLine($"  Result: {affectedCount} target(s) in deep slumber. Remaining HD pool: {remainingPool}.");
        logBuilder.Append("═══════════════════════════════════");

        _lastCombatLog = logBuilder.ToString();
        CombatUI?.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        if (AreAllNPCsDead())
        {
            Debug.Log("[CombatEnd] Victory condition met after Deep Slumber spell resolution.");
            HandleCombatVictoryDetected("ResolveDeepSlumberSpell");
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        if (AreAllPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        _pendingSpell = null;
        _pendingMetamagic = null;
        StartCoroutine(AfterAttackDelay(caster, 1.5f));
    }

    private void ResolveColorSpraySpell(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (caster == null || caster.Stats == null || _pendingSpell == null)
            return;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int castingAbilityMod = GetSpellSaveAbilityModifier(caster, _pendingSpell);
        int saveDc = 10 + _pendingSpell.SpellLevel + castingAbilityMod;

        var logBuilder = new System.Text.StringBuilder();
        logBuilder.AppendLine("═══════════════════════════════════");
        logBuilder.AppendLine($"✨ {caster.Stats.CharacterName} casts Color Spray! (15-ft cone)");
        logBuilder.AppendLine($"  Will DC {saveDc} | Targets in cone: {targets.Count} ({aoeCells.Count} squares)");
        logBuilder.AppendLine();

        int affectedCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;
            if (target == caster)
                continue;

            if (IsImmuneToMindAffecting(target))
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName}: immune to mind-affecting effects.");
                continue;
            }

            int targetHd = Mathf.Max(1, GetTargetHitDice(target));

            if (_pendingSpell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                int srRoll = DiceService.D20("Dazing Touch SR check");
                int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                if (srTotal < target.Stats.SpellResistance)
                {
                    logBuilder.AppendLine($"  • {target.Stats.CharacterName}: SR blocks Color Spray (d20 {srRoll} + CL {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance}).");
                    continue;
                }
            }

            int saveRoll = DiceService.D20("Dazing Touch Will save");
            int saveTotal = saveRoll + target.Stats.WillSave;
            if (saveTotal >= saveDc)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName}: Will save succeeds ({saveTotal} vs DC {saveDc}).");
                continue;
            }

            ColorSprayEffectData effectData = BuildColorSprayEffectData(caster, targetHd);
            ApplyColorSprayStageConditions(target, effectData, _pendingSpell);
            affectedCount++;

            string stageSummary = effectData.HdTier switch
            {
                1 => "unconscious, blinded, and stunned",
                2 => "blinded and stunned",
                _ => "stunned"
            };

            logBuilder.AppendLine($"  • {target.Stats.CharacterName} {stageSummary} by Color Spray ({effectData.RemainingDuration} rounds) [HD {targetHd}].");
        }

        logBuilder.AppendLine();
        logBuilder.AppendLine($"  Result: {affectedCount} target(s) affected.");
        logBuilder.Append("═══════════════════════════════════");

        _lastCombatLog = logBuilder.ToString();
        CombatUI?.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        if (AreAllNPCsDead())
        {
            Debug.Log("[CombatEnd] Victory condition met after Color Spray resolution.");
            HandleCombatVictoryDetected("ResolveColorSpraySpell");
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        if (AreAllPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        _pendingSpell = null;
        _pendingMetamagic = null;
        StartCoroutine(AfterAttackDelay(caster, 1.5f));
    }

    private ColorSprayEffectData BuildColorSprayEffectData(CharacterController caster, int targetHd)
    {
        int hd = Mathf.Max(1, targetHd);

        int stage1Duration;
        int stage2Duration;
        int stage3Duration;
        int hdTier;

        if (hd <= 2)
        {
            hdTier = 1;
            stage1Duration = DiceService.RollMultiple(2, 4, "Disease stage 1 duration 2d4"); // 2d4
            stage2Duration = DiceService.D4("Disease stage 2 duration 1d4"); // 1d4
            stage3Duration = 1;
        }
        else if (hd <= 4)
        {
            hdTier = 2;
            stage1Duration = DiceService.D4("Disease stage 1 duration 1d4"); // 1d4
            stage2Duration = 1;
            stage3Duration = 0;
        }
        else
        {
            hdTier = 3;
            stage1Duration = 1;
            stage2Duration = 0;
            stage3Duration = 0;
        }

        return new ColorSprayEffectData
        {
            Caster = caster,
            CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Color Spray",
            SourceSpellId = SpellNames.COLOR_SPRAY,
            SourceEffectName = "Color Spray",
            HitDice = hd,
            HdTier = hdTier,
            CurrentStage = 1,
            RemainingDuration = stage1Duration,
            NextStage = GetColorSprayNextStage(hdTier, 1),
            Stage1Duration = stage1Duration,
            Stage2Duration = stage2Duration,
            Stage3Duration = stage3Duration
        };
    }

    private static int GetColorSprayNextStage(int hdTier, int currentStage)
    {
        if (hdTier <= 1)
        {
            if (currentStage == 1) return 2;
            if (currentStage == 2) return 3;
            return 0;
        }

        if (hdTier == 2)
            return currentStage == 1 ? 2 : 0;

        return 0;
    }

    private static int GetColorSprayStageDuration(ColorSprayEffectData data, int stage)
    {
        if (data == null)
            return 0;

        return stage switch
        {
            1 => data.Stage1Duration,
            2 => data.Stage2Duration,
            3 => data.Stage3Duration,
            _ => 0
        };
    }

    private void ApplyColorSprayStageConditions(CharacterController target, ColorSprayEffectData data, SpellData sourceSpell)
    {
        if (target == null || target.Stats == null || data == null || sourceSpell == null)
            return;

        int duration = Mathf.Max(1, GetColorSprayStageDuration(data, data.CurrentStage));
        data.RemainingDuration = duration;
        data.NextStage = GetColorSprayNextStage(data.HdTier, data.CurrentStage);

        bool applyUnconscious = data.CurrentStage == 1 && data.HdTier == 1;
        bool applyBlinded = (data.CurrentStage == 1 && data.HdTier <= 2) || (data.CurrentStage == 2 && data.HdTier == 1);
        bool applyStunned = true;

        string sourceName = sourceSpell.Name;
        if (_conditionService != null)
        {
            if (applyUnconscious)
            {
                _conditionService.ApplyCondition(target, CombatConditionType.Unconscious, duration,
                    source: data.Caster, data: data,
                    sourceNameOverride: sourceName, sourceCategory: "Spell", sourceId: sourceSpell.SpellId);
            }

            if (applyBlinded)
            {
                _conditionService.ApplyCondition(target, CombatConditionType.Blinded, duration,
                    source: data.Caster, data: data,
                    sourceNameOverride: sourceName, sourceCategory: "Spell", sourceId: sourceSpell.SpellId);
            }

            if (applyStunned)
            {
                _conditionService.ApplyCondition(target, CombatConditionType.Stunned, duration,
                    source: data.Caster, data: data,
                    sourceNameOverride: sourceName, sourceCategory: "Spell", sourceId: sourceSpell.SpellId);
            }
        }
        else
        {
            string fallbackSource = data.Caster != null && data.Caster.Stats != null ? data.Caster.Stats.CharacterName : sourceName;
            if (applyUnconscious)
                target.ApplyCondition(CombatConditionType.Unconscious, duration, fallbackSource);
            if (applyBlinded)
                target.ApplyCondition(CombatConditionType.Blinded, duration, fallbackSource);
            if (applyStunned)
                target.ApplyCondition(CombatConditionType.Stunned, duration, fallbackSource);
        }
    }

    private bool TryHandleColorSprayConditionExpiry(CharacterController character, ConditionService.ActiveCondition condition)
    {
        if (character == null || character.Stats == null || condition == null)
            return false;

        CombatConditionType normalizedType = ConditionRules.Normalize(condition.Type);
        if (normalizedType != CombatConditionType.Stunned)
            return false;

        if (condition.Data is not ColorSprayEffectData data)
            return false;

        int nextStage = GetColorSprayNextStage(data.HdTier, data.CurrentStage);
        if (nextStage <= 0)
        {
            CombatUI?.ShowCombatLog($"⏱ {character.Stats.CharacterName} is no longer stunned.");
            return true;
        }

        if (data.CurrentStage == 1 && data.HdTier == 1)
            CombatUI?.ShowCombatLog($"⏱ {character.Stats.CharacterName} no longer unconscious, still blinded and stunned ({Mathf.Max(1, GetColorSprayStageDuration(data, nextStage))} rounds).");
        else if (((data.CurrentStage == 2 && data.HdTier == 1) || (data.CurrentStage == 1 && data.HdTier == 2)))
            CombatUI?.ShowCombatLog($"⏱ {character.Stats.CharacterName} no longer blinded, still stunned ({Mathf.Max(1, GetColorSprayStageDuration(data, nextStage))} round{(Mathf.Max(1, GetColorSprayStageDuration(data, nextStage)) == 1 ? string.Empty : "s")}).");

        RemoveCondition(character, CombatConditionType.Unconscious);
        RemoveCondition(character, CombatConditionType.Blinded);
        RemoveCondition(character, CombatConditionType.Stunned);

        data.CurrentStage = nextStage;
        data.RemainingDuration = Mathf.Max(1, GetColorSprayStageDuration(data, nextStage));
        data.NextStage = GetColorSprayNextStage(data.HdTier, nextStage);

        SpellData sourceSpell = SpellDatabase.GetSpell(data.SourceSpellId);
        if (sourceSpell == null)
            sourceSpell = new SpellData { SpellId = SpellNames.COLOR_SPRAY, Name = "Color Spray" };

        ApplyColorSprayStageConditions(character, data, sourceSpell);
        return true;
    }

    private bool IsImmuneToSleepEffects(CharacterController target)
    {
        if (target == null || target.Stats == null)
            return true;

        if (IsImmuneToMindAffecting(target))
            return true;

        if (target.Stats.Race != null && target.Stats.Race.ImmunityToSleep)
            return true;

        return false;
    }

    private void ApplySleepState(CharacterController caster, CharacterController target, int sleepRounds, int wakeDc, SpellData sourceSpell)
    {
        if (target == null || target.Stats == null || sourceSpell == null)
            return;

        var asleepData = new AsleepConditionData
        {
            Caster = caster,
            CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceSpell.Name,
            RemainingRounds = sleepRounds,
            WakeDC = Mathf.Max(1, wakeDc),
            SourceSpellId = sourceSpell.SpellId,
            SourceEffectName = sourceSpell.Name
        };

        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target,
                CombatConditionType.Asleep,
                sleepRounds,
                source: caster,
                data: asleepData,
                sourceNameOverride: sourceSpell.Name,
                sourceCategory: "Spell",
                sourceId: sourceSpell.SpellId);

            _conditionService.ApplyCondition(
                target,
                CombatConditionType.Unconscious,
                sleepRounds,
                source: caster,
                sourceNameOverride: sourceSpell.Name,
                sourceCategory: "Spell",
                sourceId: sourceSpell.SpellId);
        }
        else
        {
            string fallbackSource = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceSpell.Name;
            target.ApplyCondition(CombatConditionType.Asleep, sleepRounds, fallbackSource);
            target.ApplyCondition(CombatConditionType.Unconscious, sleepRounds, fallbackSource);
            target.ApplyCondition(CombatConditionType.Helpless, sleepRounds, fallbackSource);
        }
    }

    public bool IsCharacterAsleep(CharacterController character)
    {
        return character != null && HasCondition(character, CombatConditionType.Asleep);
    }

    public bool TryWakeSleepingCharacter(CharacterController target, string reason, CharacterController waker = null, bool suppressLog = false)
    {
        if (target == null || target.Stats == null)
            return false;

        if (!HasCondition(target, CombatConditionType.Asleep))
            return false;

        RemoveCondition(target, CombatConditionType.Asleep);
        RemoveCondition(target, CombatConditionType.Unconscious);
        target.SyncHPStateFromCurrentHP(emitLog: false);

        if (!suppressLog)
        {
            string wakerText = waker != null && waker.Stats != null
                ? $" by {waker.Stats.CharacterName}"
                : string.Empty;
            string reasonText = string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason})";
            CombatUI?.ShowCombatLog($"💤 {target.Stats.CharacterName} wakes{wakerText}{reasonText}.");
        }

        return true;
    }

    private bool TryResolveCauseFearSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsCauseFearSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result != null && result.MindAffectingImmunityBlocked)
            return true;

        string targetName = target.Stats.CharacterName;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        CombatUI?.ShowCombatLog($"✨ {casterName} casts Cause Fear on {targetName}.");

        if (!IsLivingCreatureForFearSpell(target))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is immune to fear (not a living creature).";
            }

            CombatUI?.ShowCombatLog($"🧟 {targetName} is immune to fear effects.");
            return true;
        }

        int targetHd = Mathf.Max(1, GetTargetHitDice(target));
        if (targetHd > 5)
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is too powerful to be frightened.";
            }

            CombatUI?.ShowCombatLog($"⚠ {targetName} is too powerful to be frightened.");
            return true;
        }

        if (result != null && result.RequiredSave && result.SaveSucceeded)
        {
            const int shakenRounds = 1;
            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Shaken,
                    shakenRounds,
                    source: caster,
                    sourceNameOverride: spell.Name,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                target.ApplyCondition(CombatConditionType.Shaken, shakenRounds, casterName);
            }

            result.BuffApplied = true;
            result.BuffDescription = "Debuff: Shaken for 1 round (successful Will save reduces Cause Fear).";
            CombatUI?.ShowCombatLog($"😰 {targetName} resists the worst of Cause Fear and is shaken for 1 round.");
            return true;
        }

        int frightenedRounds = DiceService.D4("Frightened duration 1d4");
        var fearData = new FrightenedConditionData
        {
            Caster = caster,
            CasterName = casterName,
            RemainingRounds = frightenedRounds,
            SourceSpellId = spell.SpellId,
            SourceEffectName = spell.Name
        };

        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target,
                CombatConditionType.Frightened,
                frightenedRounds,
                source: caster,
                data: fearData,
                sourceNameOverride: spell.Name,
                sourceCategory: "Spell",
                sourceId: spell.SpellId);
        }
        else
        {
            target.ApplyCondition(CombatConditionType.Frightened, frightenedRounds, casterName);
        }

        if (result != null)
        {
            result.BuffApplied = true;
            result.BuffDescription = $"Debuff: Frightened for {frightenedRounds} rounds.";
        }

        CombatUI?.ShowCombatLog($"😱 {targetName} fails Will save - Frightened for {frightenedRounds} rounds! ({casterName} is the source of fear)");
        return true;
    }

    // ======================== GHOUL TOUCH SPELL HANDLER ========================

    private static bool IsGhoulTouchSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.GHOUL_TOUCH, StringComparison.Ordinal);
    }

    private bool TryResolveGhoulTouchSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsGhoulTouchSpell(spell) || target == null || target.Stats == null)
            return false;

        string targetName = target.Stats.CharacterName;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        CombatUI?.ShowCombatLog($"✨ {casterName} casts Ghoul Touch on {targetName}.");

        // Ghoul Touch only works on living humanoids
        if (!IsLivingCreatureForFearSpell(target))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is not a living creature.";
            }
            CombatUI?.ShowCombatLog($"🧟 {targetName} is immune to Ghoul Touch (not a living creature).");
            return true;
        }

        if (!IsHumanoid(target))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is not a humanoid.";
            }
            CombatUI?.ShowCombatLog($"⚠ {targetName} is immune to Ghoul Touch (not a humanoid).");
            return true;
        }

        // Touch attack must hit
        if (result != null && result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ {casterName}'s Ghoul Touch misses {targetName}.");
            return true;
        }

        // Fort save negates
        if (result != null && result.RequiredSave && result.SaveSucceeded)
        {
            CombatUI?.ShowCombatLog($"🛡 {targetName} resists Ghoul Touch with a successful Fortitude save.");
            return true;
        }

        // Failed save: apply paralysis + stench
        GhoulTouchEffectData ghoulEffect = GhoulTouchEffectData.CreateGhoulTouch(caster, target);

        // Apply paralysis via CharacterController
        target.ApplyGhoulTouchEffect(ghoulEffect);

        // Track via StatusEffectManager for dispel/duration
        StatusEffectManager targetStatusMgr = target.GetComponent<StatusEffectManager>();
        if (targetStatusMgr == null)
        {
            targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);
        }

        targetStatusMgr.AddEffect(spell, casterName, caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1);

        if (result != null)
        {
            result.BuffApplied = true;
            result.BuffDescription = $"Debuff: Paralyzed for {ghoulEffect.ParalysisDurationRounds} rounds (stench aura active).";
        }

        CombatUI?.ShowCombatLog($"<color=#FF6666>⛓ {targetName} is paralyzed by Ghoul Touch for {ghoulEffect.ParalysisDurationRounds} rounds!</color>");
        CombatUI?.ShowCombatLog($"<color=#99CC66>☠ Carrion stench emanates from {targetName} (10-ft radius, sickens living creatures).</color>");

        // Apply stench to nearby creatures immediately
        ApplyGhoulTouchStench(caster, target, ghoulEffect);

        Debug.Log($"[GameManager] Ghoul Touch: {targetName} paralyzed for {ghoulEffect.ParalysisDurationRounds} rounds, stench active");
        return true;
    }

    /// <summary>
    /// Apply Ghoul Touch stench aura to living creatures within 10 ft of the paralyzed target.
    /// Each creature makes a Fort save or becomes sickened. Caster is exempt. Poison effect.
    /// </summary>
    private void ApplyGhoulTouchStench(CharacterController caster, CharacterController paralyzedTarget, GhoulTouchEffectData ghoulEffect)
    {
        if (paralyzedTarget == null || !ghoulEffect.IsStenchActive)
            return;

        var allCharacters = GetAllCharacters();
        if (allCharacters == null) return;

        int spellDC = 10 + 2; // Base DC for a level 2 spell; caster ability mod added below
        if (caster != null && caster.Stats != null)
        {
            int casterAbilityMod = Mathf.Max(caster.Stats.INTMod, caster.Stats.CHAMod);
            spellDC = 10 + 2 + casterAbilityMod; // 10 + spell level + ability mod
        }

        foreach (CharacterController creature in allCharacters)
        {
            if (creature == null || creature == paralyzedTarget) continue;

            if (!ghoulEffect.IsValidStenchTarget(creature)) continue;
            if (ghoulEffect.IsCreaturePoisonImmune(creature))
            {
                CombatUI?.ShowCombatLog($"🛡 {creature.Stats.CharacterName} is immune to poison (stench has no effect).");
                continue;
            }

            // Check distance (10 ft = 2 squares)
            int distance = Mathf.Max(
                Mathf.Abs(creature.GridPosition.x - paralyzedTarget.GridPosition.x),
                Mathf.Abs(creature.GridPosition.y - paralyzedTarget.GridPosition.y));

            if (distance > ghoulEffect.StenchRadiusSquares) continue;

            // Fort save vs sickened
            int fortSave = DiceService.D20("Ghoul Touch Fort save") + (creature.Stats != null ? creature.Stats.FortitudeSave : 0);
            bool saved = fortSave >= spellDC;

            if (saved)
            {
                CombatUI?.ShowCombatLog($"🛡 {creature.Stats.CharacterName} resists the carrion stench (Fort {fortSave} vs DC {spellDC}).");
            }
            else
            {
                // Apply sickened condition
                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        creature,
                        CombatConditionType.Sickened,
                        ghoulEffect.ParalysisRemainingRounds,
                        source: caster,
                        sourceNameOverride: "Ghoul Touch (stench)",
                        sourceCategory: "Spell",
                        sourceId: SpellNames.GHOUL_TOUCH);
                }
                else
                {
                    creature.ApplyCondition(CombatConditionType.Sickened, ghoulEffect.ParalysisRemainingRounds,
                        "Ghoul Touch stench");
                }

                CombatUI?.ShowCombatLog($"<color=#CCCC66>🤢 {creature.Stats.CharacterName} is sickened by the carrion stench! (-2 to attacks, damage, saves, checks) Fort {fortSave} vs DC {spellDC}</color>");
            }
        }
    }

    // ======================== SCARE SPELL HANDLER ========================

    private static bool IsScareSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.SCARE, StringComparison.Ordinal);
    }

    private bool TryResolveScareSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsScareSpell(spell) || target == null || target.Stats == null)
            return false;

        string targetName = target.Stats.CharacterName;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;

        CombatUI?.ShowCombatLog($"✨ {casterName} casts Scare on {targetName}.");

        // Check mind-affecting immunity
        if (result != null && result.MindAffectingImmunityBlocked)
            return true;

        // Must be a living creature
        if (!IsLivingCreatureForFearSpell(target))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is immune to fear (not a living creature).";
            }
            CombatUI?.ShowCombatLog($"🧟 {targetName} is immune to Scare (not a living creature).");
            return true;
        }

        // HD limit: 6+ HD are completely immune
        int targetHd = Mathf.Max(1, GetTargetHitDice(target));
        if (ScareEffectData.IsImmuneByHD(targetHd))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} has {targetHd} HD (6+ HD immune to Scare).";
            }
            CombatUI?.ShowCombatLog($"⚠ {targetName} ({targetHd} HD) is too powerful to be affected by Scare.");
            return true;
        }

        // Will save partial
        if (result != null && result.RequiredSave && result.SaveSucceeded)
        {
            // Successful save: shaken for 1 round
            ScareEffectData shakenEffect = ScareEffectData.CreateShaken(caster);
            target.ApplyScareEffect(shakenEffect);

            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Shaken,
                    1,
                    source: caster,
                    sourceNameOverride: spell.Name,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }

            if (result != null)
            {
                result.BuffApplied = true;
                result.BuffDescription = "Debuff: Shaken for 1 round (successful Will save reduces Scare).";
            }

            CombatUI?.ShowCombatLog($"😰 {targetName} resists the worst of Scare and is shaken for 1 round (-2 to attacks, saves, checks).");
            return true;
        }

        // Failed save: frightened for 1 round/level
        int frightenedRounds = Mathf.Max(1, casterLevel);
        ScareEffectData frightenedEffect = ScareEffectData.CreateFrightened(casterLevel, caster);
        target.ApplyScareEffect(frightenedEffect);

        var fearData = new FrightenedConditionData
        {
            Caster = caster,
            CasterName = casterName,
            RemainingRounds = frightenedRounds,
            SourceSpellId = spell.SpellId,
            SourceEffectName = spell.Name
        };

        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target,
                CombatConditionType.Frightened,
                frightenedRounds,
                source: caster,
                data: fearData,
                sourceNameOverride: spell.Name,
                sourceCategory: "Spell",
                sourceId: spell.SpellId);
        }
        else
        {
            target.ApplyCondition(CombatConditionType.Frightened, frightenedRounds, casterName);
        }

        if (result != null)
        {
            result.BuffApplied = true;
            result.BuffDescription = $"Debuff: Frightened for {frightenedRounds} rounds.";
        }

        CombatUI?.ShowCombatLog($"😱 {targetName} fails Will save - Frightened for {frightenedRounds} rounds! Must flee from {casterName}. (-2 to attacks, saves, checks)");
        Debug.Log($"[GameManager] Scare: {targetName} frightened for {frightenedRounds} rounds by {casterName}");
        return true;
    }

    // ======================== FEAR SPELL HANDLER (Cone AoE) ========================

    private static bool IsFearSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.FEAR, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolve the Fear spell (PHB p.229) as a cone AoE.
    /// All living creatures in the 30-ft cone: Will partial.
    /// Failed save → Panicked for 1 round/level.
    /// Successful save → Shaken for 1 round.
    /// </summary>
    private void ResolveFearSpell(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (caster == null || caster.Stats == null || _pendingSpell == null)
            return;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int saveDc = 10 + _pendingSpell.SpellLevel + caster.Stats.GetPrimaryCastingModifier();
        string casterName = caster.Stats.CharacterName;

        var logBuilder = new System.Text.StringBuilder();
        logBuilder.AppendLine("═══════════════════════════════════");
        logBuilder.AppendLine($"✨ {casterName} casts Fear! (30-ft cone)");
        logBuilder.AppendLine($"  Will DC {saveDc} | Targets in cone: {targets.Count} ({aoeCells.Count} squares)");
        logBuilder.AppendLine();

        int panickedCount = 0;
        int shakenCount = 0;
        int immuneCount = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;

            string targetName = target.Stats.CharacterName;

            // Check living creature (undead, constructs immune)
            if (!IsLivingCreatureForFearSpell(target))
            {
                logBuilder.AppendLine($"  • {targetName}: Immune (not a living creature).");
                immuneCount++;
                continue;
            }

            // Check mind-affecting immunity
            if (IsImmuneToMindAffecting(target))
            {
                logBuilder.AppendLine($"  • {targetName}: Immune to mind-affecting effects.");
                immuneCount++;
                continue;
            }

            // Spell Resistance check
            if (_pendingSpell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                int srRoll = DiceService.D20("Fear SR check");
                int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                if (srTotal < target.Stats.SpellResistance)
                {
                    logBuilder.AppendLine($"  • {targetName}: SR blocks Fear (d20 {srRoll} + CL {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance}).");
                    immuneCount++;
                    continue;
                }
            }

            // Will save
            int saveRoll = DiceService.D20("Fear Will save");
            int saveTotal = saveRoll + target.Stats.WillSave;
            bool saveSuccess = saveTotal >= saveDc;

            if (saveSuccess)
            {
                // Successful save: Shaken for 1 round
                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        target,
                        CombatConditionType.Shaken,
                        1,
                        source: caster,
                        sourceNameOverride: _pendingSpell.Name,
                        sourceCategory: "Spell",
                        sourceId: _pendingSpell.SpellId);
                }
                else
                {
                    target.ApplyCondition(CombatConditionType.Shaken, 1, casterName);
                }

                shakenCount++;
                logBuilder.AppendLine($"  • {targetName}: Will d20({saveRoll}) + {target.Stats.WillSave} = {saveTotal} ≥ DC {saveDc} — Shaken for 1 round.");
            }
            else
            {
                // Failed save: Panicked for 1 round/level
                int panickedRounds = Mathf.Max(1, casterLevel);

                var fearData = new FrightenedConditionData
                {
                    Caster = caster,
                    CasterName = casterName,
                    RemainingRounds = panickedRounds,
                    SourceSpellId = _pendingSpell.SpellId,
                    SourceEffectName = _pendingSpell.Name
                };

                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        target,
                        CombatConditionType.Panicked,
                        panickedRounds,
                        source: caster,
                        data: fearData,
                        sourceNameOverride: _pendingSpell.Name,
                        sourceCategory: "Spell",
                        sourceId: _pendingSpell.SpellId);
                }
                else
                {
                    target.ApplyCondition(CombatConditionType.Panicked, panickedRounds, casterName);
                }

                // Also apply ScareEffectData for the flee behavior integration
                var scareData = new ScareEffectData
                {
                    CurrentFearLevel = FearLevel.Panicked,
                    DurationRemainingRounds = panickedRounds,
                    IsActive = true,
                    AttackPenalty = -2,
                    SavePenalty = -2,
                    SkillPenalty = -2,
                    AbilityCheckPenalty = -2,
                    SourceSpellId = SpellNames.FEAR,
                    SourceName = "Fear"
                };
                scareData.SetCaster(caster);
                target.ApplyScareEffect(scareData);

                panickedCount++;
                logBuilder.AppendLine($"  • {targetName}: Will d20({saveRoll}) + {target.Stats.WillSave} = {saveTotal} < DC {saveDc} — PANICKED for {panickedRounds} rounds!");
            }
        }

        logBuilder.AppendLine();
        logBuilder.AppendLine($"Result: {panickedCount} panicked, {shakenCount} shaken, {immuneCount} immune/resisted");
        logBuilder.AppendLine("═══════════════════════════════════");

        CombatUI?.ShowCombatLog(logBuilder.ToString());
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        if (AreAllNPCsDead())
        {
            Debug.Log("[CombatEnd] Victory condition met after Fear resolution.");
            HandleCombatVictoryDetected("ResolveFearSpell");
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        if (AreAllPCsDead())
        {
            Debug.Log("[CombatEnd] Defeat condition met after Fear resolution.");
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI?.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            CombatUI?.SetActionButtonsVisible(false);
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        _pendingSpell = null;
        _pendingMetamagic = null;
        StartCoroutine(AfterAttackDelay(caster, 1.5f));
    }

    private static bool IsRayOfEnfeeblementSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.RAY_OF_ENFEEBLEMENT, StringComparison.Ordinal);
    }

    private static int CalculateRayOfEnfeeblementPenalty(CharacterController caster)
    {
        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int levelBonus = Mathf.Min(5, 1 + ((casterLevel - 1) / 2));
        int d6 = DiceService.D6("Enervation damage");
        return d6 + levelBonus;
    }

    private bool TryResolveRayOfEnfeeblementSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsRayOfEnfeeblementSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null || !result.Success || (result.RequiredAttackRoll && !result.AttackHit))
            return false;

        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, caster != null && caster.Stats != null ? caster.Stats.GetDomainBoostedCasterLevel(spell) : 1));
        int penalty = CalculateRayOfEnfeeblementPenalty(caster);

        EnfeebledConditionData previousEffect = target.ActiveEnfeeblementEffect;
        int previousPenalty = previousEffect != null ? Mathf.Max(0, previousEffect.StrengthPenaltyAmount) : 0;
        if (previousEffect != null)
            target.RemoveEnfeeblementEffect();

        EnfeebledConditionData effect = target.ApplyEnfeeblementEffect(penalty, durationRounds, caster);
        if (effect == null)
            return true;

        int resultingStrength = target.Stats.EffectiveStrengthScore;

        result.BuffApplied = true;
        result.BuffDescription = $"Debuff: STR -{penalty} for {durationRounds} rounds (effective STR {resultingStrength}).";

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
        if (previousEffect != null)
        {
            string replacementDescriptor = penalty > previousPenalty
                ? "stronger"
                : (penalty < previousPenalty ? "weaker" : "equally strong");
            string article = replacementDescriptor == "equally strong" ? "an" : "a";
            CombatUI?.ShowCombatLog($"♻ Previous enfeeblement replaced on {target.Stats.CharacterName}.");
            CombatUI?.ShowCombatLog($"💀 {target.Stats.CharacterName}'s enfeeblement is replaced by {article} {replacementDescriptor} effect (old STR -{previousPenalty} → new STR -{penalty}).");
        }

        CombatUI?.ShowCombatLog($"💀 {target.Stats.CharacterName} is enfeebled by {casterName}: STR -{penalty} for {durationRounds} rounds (STR now {resultingStrength}).");
        return true;
    }

    private static bool IsTouchOfIdiocySpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.TOUCH_OF_IDIOCY, StringComparison.Ordinal);
    }

    private bool TryResolveTouchOfIdiocySpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsTouchOfIdiocySpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null || !result.Success || (result.RequiredAttackRoll && !result.AttackHit))
            return false;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        int intDamage = DiceService.D6("Touch of Idiocy INT damage");
        int wisDamage = DiceService.D6("Touch of Idiocy WIS damage");
        int chaDamage = DiceService.D6("Touch of Idiocy CHA damage");

        TouchOfIdiocyConditionData previous = target.ActiveTouchOfIdiocyEffect;
        if (previous != null)
            target.RemoveTouchOfIdiocyEffect();

        int intBefore = target.Stats.GetEffectiveAbilityScore(AbilityType.INT);
        int wisBefore = target.Stats.GetEffectiveAbilityScore(AbilityType.WIS);
        int chaBefore = target.Stats.GetEffectiveAbilityScore(AbilityType.CHA);

        TouchOfIdiocyConditionData effect = target.ApplyTouchOfIdiocyEffect(
            intDamage,
            wisDamage,
            chaDamage,
            durationRounds,
            caster);

        if (effect == null)
            return true;

        int intAfter = target.Stats.GetEffectiveAbilityScore(AbilityType.INT);
        int wisAfter = target.Stats.GetEffectiveAbilityScore(AbilityType.WIS);
        int chaAfter = target.Stats.GetEffectiveAbilityScore(AbilityType.CHA);

        result.BuffApplied = true;
        result.BuffDescription = $"Debuff: Int -{intDamage}, Wis -{wisDamage}, Cha -{chaDamage} for {durationRounds} rounds.";

        CombatUI?.ShowCombatLog($"🧠 {target.Stats.CharacterName} is touched by idiocy for {durationRounds} rounds.");
        CombatUI?.ShowCombatLog($"   Ability damage: INT 1d6={intDamage}, WIS 1d6={wisDamage}, CHA 1d6={chaDamage}");
        CombatUI?.ShowCombatLog($"   INT {intBefore}→{intAfter}, WIS {wisBefore}→{wisAfter}, CHA {chaBefore}→{chaAfter}");

        return true;
    }

    private static bool IsMelfsAcidArrowSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.MELFS_ACID_ARROW, StringComparison.Ordinal);
    }

    private static int CalculateMelfsAcidArrowAdditionalRounds(CharacterController caster)
    {
        int casterLevel = 1;
        if (caster != null && caster.Stats != null)
        {
            casterLevel = caster.Stats.GetCasterLevel();
            if (casterLevel <= 0)
                casterLevel = Mathf.Max(1, caster.Stats.EffectiveCharacterLevel);
        }

        // D&D 3.5e: total rounds = 1 + floor(CL / 3), max total 7 rounds at CL 18.
        return Mathf.Min(6, Mathf.Max(0, casterLevel / 3));
    }

    private bool TryResolveMelfsAcidArrowSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsMelfsAcidArrowSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            target.ClearMelfsAcidArrowEffect();
            return true;
        }

        int initialDamage = Mathf.Max(0, result.DamageDealt);
        CombatUI?.ShowCombatLog($"🧪 Melf's Acid Arrow hits for {initialDamage} damage");

        int lingeringRounds = CalculateMelfsAcidArrowAdditionalRounds(caster);
        if (lingeringRounds > 0)
            target.ApplyMelfsAcidArrowEffect(lingeringRounds, caster);
        else
            target.ClearMelfsAcidArrowEffect();

        return true;
    }

    private void ApplyMelfsAcidArrowTurnStartDamage(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        MelfsAcidArrowEffectData effectData = character.ActiveMelfsAcidArrowEffect;
        if (effectData == null || !effectData.IsActive || effectData.RemainingDamageRounds <= 0)
            return;

        int diceCount = Mathf.Max(1, effectData.DamageDiceCount);
        int diceSides = Mathf.Max(2, effectData.DamageDiceSides);
        int rolledDamage = 0;
        for (int i = 0; i < diceCount; i++)
            rolledDamage += DiceService.RollDie(diceSides, "Spectral Hand damage die");

        var packet = new DamagePacket
        {
            RawDamage = rolledDamage,
            Types = new HashSet<DamageType> { DamageType.Acid },
            AttackTags = DamageBypassTag.None,
            IsRanged = true,
            IsNonlethal = false,
            Source = AttackSource.Spell,
            SourceName = "Melf's Acid Arrow (Lingering)"
        };

        DamageResolutionResult mitigation = character.Stats.ApplyIncomingDamage(rolledDamage, packet);
        int damageDealt = mitigation.FinalDamage;

        int roundsLeft = Mathf.Max(0, effectData.RemainingDamageRounds - 1);
        character.UpdateMelfsAcidArrowDuration(roundsLeft);

        if (damageDealt > 0)
            CheckConcentrationOnDamage(character, damageDealt);

        CombatUI?.ShowCombatLog($"🧪 Acid continues to burn for {damageDealt} damage ({roundsLeft} rounds left)");

        if (character.Stats.IsDead)
        {
            character.OnDeath();
            HandleSummonDeathCleanup(character);
        }

        if (roundsLeft <= 0)
        {
            character.ClearMelfsAcidArrowEffect();
            CombatUI?.ShowCombatLog("⏱ Acid effect expires");
        }
    }

    /// <summary>
    /// Apply buff effects from a spell to the target character.
    /// Uses StatusEffectManager for proper duration tracking and stat modification reversal.
    /// Falls back to legacy system if StatusEffectManager is not available.
    /// </summary>
    private ActiveSpellEffect ApplySpellBuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        if (spell != null && (spell.SpellId == SpellNames.DAZE || spell.SpellId == SpellNames.DAZE_MONSTER))
        {
            int dazeRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, caster != null && caster.Stats != null ? caster.Stats.GetDomainBoostedCasterLevel(spell) : 1));
            string sourceName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;

            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Dazed,
                    dazeRounds,
                    source: caster,
                    sourceNameOverride: spell.Name,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                target.ApplyCondition(CombatConditionType.Dazed, dazeRounds, sourceName);
            }

            CombatUI?.ShowCombatLog($"<color=#FFCC66>💫 {target.Stats.CharacterName} is dazed for {dazeRounds} round(s)!</color>");
            Debug.Log($"[GameManager] {spell.Name} applied Dazed to {target.Stats.CharacterName} for {dazeRounds} round(s)");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.HIDEOUS_LAUGHTER)
        {
            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            int laughterRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
            string sourceName = spell.Name;

            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.HideousLaughter,
                    laughterRounds,
                    source: caster,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);

                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Prone,
                    laughterRounds,
                    source: caster,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                string fallbackSource = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
                target.ApplyCondition(CombatConditionType.HideousLaughter, laughterRounds, fallbackSource);
                target.ApplyCondition(CombatConditionType.Prone, laughterRounds, fallbackSource);
            }

            CombatUI?.ShowCombatLog($"<color=#FF99FF>🤣 HAHAHA! {target.Stats.CharacterName} collapses in hideous laughter and falls prone for {laughterRounds} round(s)!</color>");
            Debug.Log($"[GameManager] Tasha's Hideous Laughter applied to {target.Stats.CharacterName} for {laughterRounds} rounds");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.FLARE)
        {
            int dazzledRounds = Mathf.Max(1, spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : 10);
            string sourceName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
            target.ApplyCondition(CombatConditionType.Dazzled, dazzledRounds, sourceName);
            CombatUI?.ShowCombatLog($"<color=#FFCC66>✨ {target.Stats.CharacterName} is dazzled (-1 attack, Spot, and Search) for {dazzledRounds} round(s)!</color>");
            Debug.Log($"[GameManager] Flare applied Dazzled to {target.Stats.CharacterName} for {dazzledRounds} round(s)");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.HOLD_PERSON)
        {
            return ApplyHoldPersonBuff(caster, target, spell, spellComp);
        }

        if (spell != null && spell.SpellId == SpellNames.RAGE)
        {
            return ApplyRageSpellBuff(caster, target, spell, spellComp);
        }

        if (spell != null && (spell.SpellId == SpellNames.BLINDNESS_DEAFNESS_WIZ
            || spell.SpellId == SpellNames.BLINDNESS_DEAFNESS_CLR
            || spell.SpellId == SpellNames.BLINDNESS_DEAFNESS_BRD))
        {
            int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;

            // D&D 3.5e PHB p.206: Caster chooses blindness or deafness at cast time.
            // For AI casters, randomly choose. For player casters, default to blindness
            // (the more tactically impactful choice). Future: add UI prompt for player choice.
            bool chooseBlindness = true;
            if (caster != null && !caster.IsPlayerControlled)
            {
                chooseBlindness = DiceService.CoinFlip("Blindness/Deafness AI choice"); // 50/50 for AI
            }

            if (chooseBlindness)
            {
                var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(spell.SpellId, caster, casterLevel);
                target.ApplyBlindnessEffect(blindEffect);
                CombatUI?.ShowCombatLog($"<color=#FF9966>🔲 {target.Stats.CharacterName} is blinded by {spell.Name}!</color>");
                Debug.Log($"[GameManager] {spell.Name} applied Blindness to {target.Stats.CharacterName} (permanent)");
            }
            else
            {
                var deafEffect = BlindnessDeafnessEffectData.CreateSpellDeafness(spell.SpellId, caster, casterLevel);
                target.ApplyDeafnessEffect(deafEffect);
                CombatUI?.ShowCombatLog($"<color=#FF9966>🔔 {target.Stats.CharacterName} is deafened by {spell.Name}!</color>");
                Debug.Log($"[GameManager] {spell.Name} applied Deafness to {target.Stats.CharacterName} (permanent)");
            }
            return null;
        }

        // ================================================================
        // Command Undead — D&D 3.5e PHB p.211
        // Grants control over one undead creature. No HD limit.
        // Nonintelligent: no save, obey all commands including suicidal.
        // Intelligent: Will save (already resolved), Friendly attitude,
        //   CHA check for unusual orders, never obey suicidal orders.
        // Duration: 1 day/level. Threatening acts break control.
        // ================================================================
        if (spell != null && spell.SpellId == SpellNames.COMMAND_UNDEAD)
        {
            if (target == null || target.Stats == null)
                return null;

            // Validate target is undead
            if (!target.CanBeCommandedAsUndead())
            {
                CombatUI?.ShowCombatLog($"⚠ {spell.Name} has no effect — {target.Stats.CharacterName} is not undead.");
                Debug.Log($"[GameManager] {spell.Name} failed: {target.Stats.CharacterName} is not undead (CreatureType={target.Stats.CreatureType})");
                return null;
            }

            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            bool isIntelligent = target.IsIntelligentUndead();

            CommandUndeadEffectData effectData;
            if (isIntelligent)
            {
                effectData = CommandUndeadEffectData.CreateForIntelligent(caster, target, casterLevel);
                CombatUI?.ShowCombatLog($"<color=#9966FF>💀 {target.Stats.CharacterName} is now commanded by {caster.Stats.CharacterName}! (intelligent undead — Friendly attitude, {casterLevel} day(s))</color>");
            }
            else
            {
                effectData = CommandUndeadEffectData.CreateForNonintelligent(caster, target, casterLevel);
                CombatUI?.ShowCombatLog($"<color=#9966FF>💀 {target.Stats.CharacterName} is now commanded by {caster.Stats.CharacterName}! (mindless undead — full obedience, {casterLevel} day(s))</color>");
            }

            target.ApplyCommandUndeadEffect(effectData);

            Debug.Log($"[GameManager] Command Undead applied: {target.Stats.CharacterName} commanded by {caster.Stats.CharacterName} " +
                      $"(intelligent={isIntelligent}, duration={effectData.DurationRemainingRounds} rounds, casterLevel={casterLevel})");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.CONFUSION)
        {
            int confusionRounds = Mathf.Max(1, spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : 1);
            string sourceName = spell.Name;

            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Confused,
                    confusionRounds,
                    source: caster,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                string fallbackSource = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
                target.ApplyCondition(CombatConditionType.Confused, confusionRounds, fallbackSource);
            }

            CombatUI?.ShowCombatLog($"<color=#FFCC99>🌀 {target.Stats.CharacterName} is confused for {confusionRounds} round(s)!</color>");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.CHARM_PERSON)
        {
            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            int charmRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
            string sourceName = spell.Name;

            var charmData = new CharmedConditionData
            {
                Caster = caster,
                CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName,
                RemainingRounds = charmRounds,
                SourceSpellId = spell.SpellId,
                SourceEffectName = spell.Name
            };

            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Charmed,
                    charmRounds,
                    source: caster,
                    data: charmData,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                string fallbackSource = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
                target.ApplyCondition(CombatConditionType.Charmed, charmRounds, fallbackSource);
            }

            CombatUI?.ShowCombatLog($"<color=#FFD699>💞 {target.Stats.CharacterName} is charmed by {charmData.CasterName} for {charmRounds} round(s)!</color>");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.ENERVATION)
        {
            string sourceName = spell.Name;
            int negativeLevels = DiceService.D4("Enervation negative levels 1d4");
            int total = NegativeLevelSystem.ApplyNegativeLevels(target, negativeLevels, sourceName);
            CombatUI?.ShowCombatLog($"<color=#9966CC>☠ {target.Stats.CharacterName} gains {negativeLevels} negative level(s) from Enervation (total {total}).</color>");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.FLESH_TO_STONE)
        {
            string sourceName = spell.Name;
            int rounds = spell.BuffDurationRounds;
            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Petrified,
                    rounds,
                    source: caster,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                string fallbackSource = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
                target.ApplyCondition(CombatConditionType.Petrified, rounds, fallbackSource);
            }

            CombatUI?.ShowCombatLog($"<color=#AAAAAA>🪨 {target.Stats.CharacterName} is petrified!</color>");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.STONE_TO_FLESH)
        {
            bool removed = target.RemoveCondition(CombatConditionType.Petrified);
            if (removed)
                CombatUI?.ShowCombatLog($"<color=#99FF99>✨ {target.Stats.CharacterName} returns to flesh.</color>");
            return null;
        }

        if (spell != null && (spell.SpellId == SpellNames.RESTORATION || spell.SpellId == SpellNames.GREATER_RESTORATION))
        {
            int removed = NegativeLevelSystem.RemoveNegativeLevels(target, int.MaxValue, spell.Name);
            CombatUI?.ShowCombatLog($"<color=#99FFCC>✨ {target.Stats.CharacterName} recovers {removed} negative level(s).</color>");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.SLEEP)
        {
            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            int sleepRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
            int wakeDc = 10 + spell.SpellLevel + GetSpellSaveAbilityModifier(caster, spell);

            ApplySleepState(caster, target, sleepRounds, wakeDc, spell);

            CombatUI?.ShowCombatLog($"<color=#99CCFF>💤 {target.Stats.CharacterName} falls asleep for {sleepRounds} round(s)!</color>");
            Debug.Log($"[GameManager] Sleep applied Asleep/Unconscious to {target.Stats.CharacterName} for {sleepRounds} rounds");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.DEEP_SLUMBER)
        {
            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            int sleepRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
            int wakeDc = 10 + spell.SpellLevel + GetSpellSaveAbilityModifier(caster, spell);

            ApplySleepState(caster, target, sleepRounds, wakeDc, spell);

            CombatUI?.ShowCombatLog($"<color=#9999FF>💤 {target.Stats.CharacterName} falls into deep slumber for {sleepRounds} round(s)!</color>");
            Debug.Log($"[GameManager] Deep Slumber applied Asleep/Unconscious to {target.Stats.CharacterName} for {sleepRounds} rounds");
            return null;
        }

        if (spell != null && spell.SpellId == "power_word_stun")
        {
            int stunRounds = Mathf.Max(1, spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : 1);
            string sourceName = spell.Name;

            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Stunned,
                    stunRounds,
                    source: caster,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                string fallbackSource = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
                target.ApplyCondition(CombatConditionType.Stunned, stunRounds, fallbackSource);
            }

            CombatUI?.ShowCombatLog($"<color=#FFCC66>💫 {target.Stats.CharacterName} is stunned by {spell.Name} for {stunRounds} round(s)!</color>");
            Debug.Log($"[GameManager] {spell.Name} applied Stunned to {target.Stats.CharacterName} for {stunRounds} rounds");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.TOUCH_OF_FATIGUE)
        {
            string sourceName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
            int casterLevel = caster != null && caster.Stats != null
                ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell))
                : 1;

            bool wasExhausted = target.HasCondition(CombatConditionType.Exhausted);
            if (wasExhausted)
            {
                CombatUI?.ShowCombatLog($"<color=#FFCC66>💤 {target.Stats.CharacterName} is already exhausted. Touch of Fatigue has no effect.</color>");
                Debug.Log($"[GameManager] Touch of Fatigue had no effect on {target.Stats.CharacterName} (already exhausted)");
                return null;
            }

            bool wasFatigued = target.HasCondition(CombatConditionType.Fatigued);
            target.ApplyCondition(CombatConditionType.Fatigued, casterLevel, sourceName);

            bool isNowExhausted = target.HasCondition(CombatConditionType.Exhausted);
            if (wasFatigued && isNowExhausted)
            {
                CombatUI?.ShowCombatLog($"<color=#FF9966>🥵 {target.Stats.CharacterName} becomes exhausted for {casterLevel} round(s)!</color>");
                Debug.Log($"[GameManager] Touch of Fatigue escalated {target.Stats.CharacterName} to Exhausted for {casterLevel} rounds");
            }
            else
            {
                CombatUI?.ShowCombatLog($"<color=#FFCC66>😫 {target.Stats.CharacterName} is fatigued for {casterLevel} round(s)!</color>");
                Debug.Log($"[GameManager] Touch of Fatigue applied Fatigued to {target.Stats.CharacterName} for {casterLevel} rounds");
            }

            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.TRUE_STRIKE)
        {
            CharacterController recipient = caster;
            if (recipient == null)
                return null;

            if (target != null && target != caster)
            {
                CombatUI?.ShowCombatLog($"<color=#FFAA66>⚠ True Strike is personal and can only target the caster.</color>");
                Debug.Log("[GameManager] True Strike attempted on non-caster target; ignoring target and applying to caster only.");
            }

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            recipientStatusMgr?.RemoveEffectsBySpellId(SpellNames.TRUE_STRIKE);

            TrueStrikeEffect existing = recipient.GetComponent<TrueStrikeEffect>();
            if (existing != null)
                Destroy(existing);

            TrueStrikeEffect effect = recipient.gameObject.AddComponent<TrueStrikeEffect>();
            effect.Initialize(recipient, this, CurrentRound);

            string casterName = recipient.Stats != null ? recipient.Stats.CharacterName : "Unknown";
            CombatUI?.ShowCombatLog($"<color=#88CCFF>✨ {casterName} casts True Strike (+20 insight on next attack, ignores concealment).</color>");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.DISGUISE_SELF)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            string selectedRace = string.IsNullOrWhiteSpace(_pendingDisguiseSelfRace)
                ? recipient.ActualRace
                : _pendingDisguiseSelfRace;

            if (RaceDatabase.TryGetRaceSizeCategory(selectedRace, out SizeCategory selectedRaceSize)
                && selectedRaceSize != recipient.Stats.CurrentSizeCategory)
            {
                selectedRace = recipient.ActualRace;
            }

            int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                recipient.ApplyDisguiseSelfEffect(selectedRace, effect.RemainingRounds, caster);

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88FFEE>🎭 {recipient.Stats.CharacterName} now appears as {recipient.DisplayedRace} ({effect.GetDurationDisplayString()}).</color>");
            }

            _pendingDisguiseSelfRace = null;
            UpdateAllStatsUI();
            return effect;
        }

        if (spell != null && spell.SpellId == SpellNames.EXPEDITIOUS_RETREAT)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                recipient.ApplyExpeditiousRetreatEffect(effect.AppliedSpeedBonusFeet, effect.RemainingRounds, caster);

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88FFEE>💨 {recipient.Stats.CharacterName}'s land speed increases by +{effect.AppliedSpeedBonusFeet} ft ({effect.GetDurationDisplayString()}).</color>");
            }

            UpdateAllStatsUI();
            return effect;
        }

        if (spell != null && spell.SpellId == SpellNames.SEE_INVISIBLE)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                recipient.ApplySeeInvisibilityEffect(effect.RemainingRounds, recipient);

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88FFEE>👁 {recipient.Stats.CharacterName} can now see invisible creatures ({effect.GetDurationDisplayString()}).</color>");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ── Detect Alignment / Undead Suite ──
        if (spell != null && AlignmentDetectionEffectData.IsDetectionSpell(spell.SpellId))
        {
            return ApplyAlignmentDetectionSpell(caster, spell, spellComp);
        }

        if (spell != null && spell.SpellId == SpellNames.INVISIBILITY_SPHERE)
        {
            return ApplyInvisibilitySphere(caster, target, spell, spellComp);
        }

        if (spell != null && spell.SpellId == SpellNames.INVISIBILITY)
        {
            CharacterController recipient = target ?? caster;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                recipient.ApplyInvisibilityEffect(effect.RemainingRounds, caster, isMoving: false);

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                UpdateEnemyLastKnownPositionForInvisibility(recipient);
                CombatUI?.ShowCombatLog($"<color=#88FFEE>👁 {recipient.Stats.CharacterName} becomes invisible ({effect.GetDurationDisplayString()}).</color>");
            }

            UpdateAllStatsUI();
            return effect;
        }

        if (spell != null && spell.SpellId == SpellNames.BLUR)
        {
            CharacterController recipient = target ?? caster;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
                bool selfCast = recipient == caster;
                string castLine = selfCast
                    ? $"<color=#88FFEE>🌫 {casterName} casts Blur on self!</color>"
                    : $"<color=#88FFEE>🌫 {casterName} casts Blur on {recipient.Stats.CharacterName}!</color>";

                CombatUI?.ShowCombatLog(castLine);
                CombatUI?.ShowCombatLog($"<color=#A6F3FF>   {recipient.Stats.CharacterName}'s outline becomes blurred and indistinct.</color>");
                CombatUI?.ShowCombatLog($"<color=#A6F3FF>   Attacks against {recipient.Stats.CharacterName} have 20% miss chance ({effect.GetDurationDisplayString()}, {Mathf.Max(0, effect.RemainingRounds)} rounds).</color>");
            }

            UpdateAllStatsUI();
            return effect;
        }

        if (spell != null && spell.SpellId == SpellNames.DISPLACEMENT)
        {
            return ApplyDisplacementBuff(caster, target, spell, spellComp);
        }

        if (spell != null && spell.SpellId == SpellNames.BLINK)
        {
            return ApplyBlinkBuff(caster, spell, spellComp);
        }

        if (spell != null && spell.SpellId == SpellNames.HASTE)
        {
            return ApplyHasteBuff(caster, target, spell, spellComp);
        }

        if (spell != null && spell.SpellId == SpellNames.SLOW)
        {
            return ApplySlowDebuff(caster, target, spell, spellComp);
        }

        if (spell != null && (spell.SpellId == SpellNames.MASS_ENLARGE_PERSON || spell.SpellId == SpellNames.MASS_REDUCE_PERSON))
        {
            return ApplyMassSizeChangeBuff(caster, target, spell, spellComp);
        }

        if (spell != null && spell.SpellId == SpellNames.RESIST_ENERGY)
        {
            CharacterController recipient = target ?? caster;
            if (recipient == null || recipient.Stats == null)
                return null;

            if (!_pendingResistEnergyType.HasValue)
            {
                if (caster != null && !caster.IsControllable)
                    _pendingResistEnergyType = ResistEnergyType.Fire;
                else
                {
                    CombatUI?.ShowCombatLog("⚠ Resist Energy failed: no energy type selected.");
                    return null;
                }
            }

            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            int resistance = casterLevel >= 11 ? 30 : (casterLevel >= 7 ? 20 : 10);
            int durationRounds = ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel);

            DamageType chosenDamageType = DamageType.Fire;
            switch (_pendingResistEnergyType.Value)
            {
                case ResistEnergyType.Acid: chosenDamageType = DamageType.Acid; break;
                case ResistEnergyType.Cold: chosenDamageType = DamageType.Cold; break;
                case ResistEnergyType.Electricity: chosenDamageType = DamageType.Electricity; break;
                case ResistEnergyType.Fire: chosenDamageType = DamageType.Fire; break;
                case ResistEnergyType.Sonic: chosenDamageType = DamageType.Sonic; break;
            }

            recipient.Stats.SetResistEnergyEffect(new ResistEnergyEffectData
            {
                EnergyType = _pendingResistEnergyType.Value,
                ResistanceAmount = resistance,
                DurationRemainingRounds = durationRounds,
                Caster = caster
            });

            string energyLabel = DamageTextUtils.GetDamageTypeDisplay(chosenDamageType);
            CombatUI?.ShowCombatLog($"<color=#88FFEE>🛡 {recipient.Stats.CharacterName} gains Resist Energy ({energyLabel} {resistance}) for {Mathf.Max(0, durationRounds)} rounds.</color>");
            _pendingResistEnergyType = null;
            _pendingFireShieldIsWarm = null;
            _pendingProtectionFromEnergyType = null;
            UpdateAllStatsUI();
            return null;
        }

        // ================================================================
        //  PROTECTION FROM ENERGY (PHB p.266)
        //  Absorbs 12 pts/CL (max 120) of chosen energy type.
        //  Duration: 10 min/level or until discharged.
        // ================================================================
        if (spell != null && spell.SpellId == SpellNames.PROTECTION_FROM_ENERGY)
        {
            CharacterController recipient = target ?? caster;
            if (recipient == null || recipient.Stats == null)
                return null;

            if (!_pendingProtectionFromEnergyType.HasValue)
            {
                if (caster != null && !caster.IsControllable)
                    _pendingProtectionFromEnergyType = ResistEnergyType.Fire;
                else
                {
                    CombatUI?.ShowCombatLog("⚠ Protection from Energy failed: no energy type selected.");
                    return null;
                }
            }

            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            int absorptionPool = ProtectionFromEnergyEffectData.CalculateAbsorptionPool(casterLevel);
            int durationRoundsProtEnergy = ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel);

            recipient.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
            {
                EnergyType = _pendingProtectionFromEnergyType.Value,
                MaxAbsorptionPoints = absorptionPool,
                RemainingAbsorptionPoints = absorptionPool,
                DurationRemainingRounds = durationRoundsProtEnergy,
                Caster = caster,
                CasterLevel = casterLevel
            });

            DamageType chosenDamageTypeProtEnergy = DamageType.Fire;
            switch (_pendingProtectionFromEnergyType.Value)
            {
                case ResistEnergyType.Acid: chosenDamageTypeProtEnergy = DamageType.Acid; break;
                case ResistEnergyType.Cold: chosenDamageTypeProtEnergy = DamageType.Cold; break;
                case ResistEnergyType.Electricity: chosenDamageTypeProtEnergy = DamageType.Electricity; break;
                case ResistEnergyType.Fire: chosenDamageTypeProtEnergy = DamageType.Fire; break;
                case ResistEnergyType.Sonic: chosenDamageTypeProtEnergy = DamageType.Sonic; break;
            }

            string protEnergyLabel = DamageTextUtils.GetDamageTypeDisplay(chosenDamageTypeProtEnergy);
            CombatUI?.ShowCombatLog($"<color=#88FFEE>🛡 {recipient.Stats.CharacterName} gains Protection from Energy ({protEnergyLabel}, {absorptionPool} pts) for {Mathf.Max(0, durationRoundsProtEnergy)} rounds (CL {casterLevel}).</color>");
            _pendingProtectionFromEnergyType = null;
            UpdateAllStatsUI();
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.PROTECTION_FROM_ARROWS)
        {
            CharacterController recipient = target ?? caster;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = caster != null && caster.Stats != null ? caster.Stats.Level : 1;
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                int maxPool = Mathf.Min(100, Mathf.Max(1, casterLevel) * 10);
                int drAmount = effect.AppliedDamageReductionAmount > 0 ? effect.AppliedDamageReductionAmount : 10;
                recipient.Stats.ActiveProtectionFromArrowsEffect = new ProtectionFromArrowsEffectData
                {
                    DamageReductionAmount = drAmount,
                    TotalAbsorptionPool = maxPool,
                    CurrentAbsorbedDamage = 0,
                    DurationRemainingRounds = effect.RemainingRounds,
                    AttacksBlocked = 0
                };

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88FFEE>🛡 {recipient.Stats.CharacterName} gains Protection from Arrows (DR {drAmount}/magic vs ranged weapons, {maxPool} absorption pool).</color>");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== STONESKIN — PHB p.285 =====
        // Abjuration. Touch. DR 10/adamantine with absorption pool.
        // Material component: granite + 250 gp diamond dust (consumed).
        if (spell != null && spell.SpellId == SpellNames.STONESKIN)
        {
            CharacterController recipient = target ?? caster;
            if (recipient == null || recipient.Stats == null)
                return null;

            // Check and consume material components (250 gp diamond dust from inventory)
            // F12 test panel casts bypass inventory requirements for testing convenience.
            CharacterStats casterStats = caster != null ? caster.Stats : null;
            bool isTestPanelCast = _testPanelCastActive;
            if (casterStats != null && SpellComponentRegistry.HasCostlyComponents(spell.SpellId) && !isTestPanelCast)
            {
                var req = SpellComponentRegistry.GetRequirements(spell.SpellId);
                if (req != null && req.HasInventoryComponents)
                {
                    // Inventory-based component check
                    if (!SpellComponentRegistry.HasRequiredInventoryComponents(spell.SpellId, caster, out string missingComponent))
                    {
                        CombatUI?.ShowCombatLog($"<color=#FF6666>❌ {casterStats.CharacterName} cannot cast Stoneskin — missing required component: {missingComponent}! Purchase it from the store and carry it in your inventory.</color>");
                        return null;
                    }
                    SpellComponentRegistry.ConsumeInventoryComponents(spell.SpellId, caster);
                    CombatUI?.ShowCombatLog($"<color=#CCAA44>💎 {casterStats.CharacterName} consumes diamond dust (250 gp) from inventory for Stoneskin.</color>");
                }
                else
                {
                    // Fallback: gold-based component check for non-inventory components
                    if (!SpellComponentRegistry.CanAffordComponents(spell.SpellId, casterStats))
                    {
                        string summary = SpellComponentRegistry.GetConsumptionSummary(spell.SpellId);
                        CombatUI?.ShowCombatLog($"<color=#FF6666>❌ {casterStats.CharacterName} cannot cast Stoneskin — insufficient material components! {summary ?? ""} (has {casterStats.ComponentGold} gp)</color>");
                        return null;
                    }
                    SpellComponentRegistry.ConsumeComponents(spell.SpellId, casterStats);
                    CombatUI?.ShowCombatLog($"<color=#CCAA44>💎 {casterStats.CharacterName} consumes 250 gp diamond dust for Stoneskin ({casterStats.ComponentGold} gp remaining).</color>");
                }
            }
            else if (isTestPanelCast && casterStats != null)
            {
                Debug.Log($"[SpellCasting] F12 test panel cast — bypassing inventory component check for {spell.SpellId}.");
                CombatUI?.ShowCombatLog($"<color=#888888>🔧 [Test] {casterStats.CharacterName} casts Stoneskin (component check bypassed for testing).</color>");
            }

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = caster != null && caster.Stats != null ? caster.Stats.GetDomainBoostedCasterLevel(spell) : 1;
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                // Absorption pool: 10 per caster level, max 150
                int maxPool = Mathf.Min(150, Mathf.Max(1, casterLevel) * 10);
                recipient.Stats.ActiveStoneskinEffect = new StoneskinEffectData
                {
                    DamageReductionAmount = 10,
                    BypassTag = DamageBypassTag.Adamantine,
                    TotalAbsorptionPool = maxPool,
                    CurrentAbsorbedDamage = 0,
                    DurationRemainingRounds = effect.RemainingRounds,
                    HitsBlocked = 0
                };

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88FFEE>🪨 {recipient.Stats.CharacterName} gains Stoneskin (DR 10/adamantine, {maxPool} absorption pool, CL {casterLevel}).</color>");
            }

            UpdateAllStatsUI();
            return effect;
        }

        if (spell != null && spell.SpellId == SpellNames.MAGIC_WEAPON)
        {
            TryApplyMagicWeaponToPendingItem(caster, target, spell);
            UpdateAllStatsUI();
            return null;
        }

        // ===== FLAME ARROW — PHB p.231 =====
        // Self-targeting spell that enchants ammunition in caster's inventory.
        if (spell != null && spell.SpellId == SpellNames.FLAME_ARROW)
        {
            TryResolveFlameArrowSpell(caster, spell);
            return null;
        }

        // ===== KEEN EDGE — PHB p.246 =====
        // Doubles threat range of one slashing/piercing weapon.
        if (spell != null && spell.SpellId == SpellNames.KEEN_EDGE)
        {
            TryApplyKeenEdgeToPendingItem(caster, target, spell);
            UpdateAllStatsUI();
            return null;
        }

        // ===== GREATER MAGIC WEAPON — PHB p.251 =====
        // +1 enhancement per 4 CL (max +5) to one weapon.
        if (spell != null && spell.SpellId == SpellNames.GREATER_MAGIC_WEAPON)
        {
            TryApplyGreaterMagicWeaponToPendingItem(caster, target, spell);
            UpdateAllStatsUI();
            return null;
        }

        // ===== SPECTRAL HAND — D&D 3.5e PHB p.282 =====
        // Necromancy. Sor/Wiz 2. V, S. 1 standard action.
        // Range: Medium (100 ft + 10 ft/level). Duration: 1 min/level (D).
        // No save, no SR. Caster loses 1d4 HP (regained on spell end, NOT if hand destroyed).
        // Hand HP = HP lost. Hand AC = 22 + Int mod. +2 melee touch attack.
        // Can deliver touch spells of 4th level or lower.
        if (spell != null && spell.SpellId == SpellNames.SPECTRAL_HAND)
        {
            CharacterController recipient = caster; // Personal range — always self
            if (recipient == null || recipient.Stats == null)
                return null;

            int casterLevel = Mathf.Max(1, recipient.Stats.GetCasterLevel());
            int intMod = recipient.Stats.INTMod;

            // Roll 1d4 for HP loss / hand HP
            int handHP = SpectralHandEffectData.RollHandHP();

            // Check if caster has enough HP (must have at least 1 HP after loss)
            if (recipient.Stats.CurrentHP <= handHP)
            {
                CombatUI?.ShowCombatLog($"<color=#FF8888>👻 {recipient.Stats.CharacterName} does not have enough HP to create Spectral Hand (needs >{handHP} HP).</color>");
                return null;
            }

            // Create the effect data
            SpectralHandEffectData spectralHandEffect = SpectralHandEffectData.CreateWithHP(handHP, casterLevel, intMod, recipient);

            // Track via StatusEffectManager for duration and dispel
            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                // Apply the effect (handles HP loss)
                recipient.ApplySpectralHandEffect(spectralHandEffect);

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88FF88>👻 {recipient.Stats.CharacterName} casts Spectral Hand — loses {handHP} HP. " +
                                        $"Hand HP: {handHP}, AC: {spectralHandEffect.HandAC} " +
                                        $"(22 + Int {intMod:+#;-#;+0}) [{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] Spectral Hand applied to {recipient.Stats.CharacterName}: " +
                          $"Hand HP {handHP}, AC {spectralHandEffect.HandAC}, CL {casterLevel}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== ATTRIBUTE ENHANCEMENT SPELLS — D&D 3.5e PHB =====
        // Bear's Endurance (CON), Bull's Strength (STR), Cat's Grace (DEX),
        // Eagle's Splendor (CHA), Fox's Cunning (INT), Owl's Wisdom (WIS)
        // All grant +4 enhancement bonus to one ability score, 1 min/level, Touch.
        // Enhancement bonuses don't stack (same type to same stat). Different stats coexist.
        // Bear's Endurance: +2 HP per HD (real HP, not temp). Loss can kill.
        if (spell != null && AttributeEnhancementEffectData.IsAttributeEnhancementSpell(spell.SpellId))
        {
            if (target == null || target.Stats == null)
                return null;

            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            int targetHD = target.Stats.HitDice > 0 ? target.Stats.HitDice : target.Stats.Level;

            // Create the attribute enhancement effect data
            AttributeEnhancementEffectData enhancementEffect = AttributeEnhancementEffectData.Create(
                spell.SpellId, casterLevel, targetHD, caster);

            // Track via StatusEffectManager for duration and dispel (handles stat bonus apply/reverse)
            StatusEffectManager targetStatusMgr = target.GetComponent<StatusEffectManager>();
            if (targetStatusMgr == null)
                targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);

            ActiveSpellEffect effect = targetStatusMgr.AddEffect(spell,
                caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown", casterLevel);

            if (effect != null)
            {
                // Apply the attribute enhancement effect (handles HP for Bear's Endurance, non-stacking)
                target.ApplyAttributeEnhancement(enhancementEffect);

                // Track in SpellcastingComponent for backward compat
                SpellcastingComponent targetSpellComp = target.GetComponent<SpellcastingComponent>();
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                // Build log message
                string abilityName = enhancementEffect.AbilityName;
                string spellName = enhancementEffect.SourceName;
                string hpNote = enhancementEffect.IsBearsEndurance && enhancementEffect.GrantedBonusHP > 0
                    ? $", +{enhancementEffect.GrantedBonusHP} HP ({targetHD} HD × 2)"
                    : "";
                string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

                CombatUI?.ShowCombatLog($"<color=#88FF88>💪 {casterName} casts {spellName} on {target.Stats.CharacterName} — " +
                    $"+{enhancementEffect.BonusAmount} enhancement bonus to {abilityName}{hpNote} " +
                    $"[{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] {spellName} applied to {target.Stats.CharacterName}: " +
                    $"+{enhancementEffect.BonusAmount} {abilityName}, CL {casterLevel}{hpNote}");
            }
            else
            {
                Debug.Log($"[GameManager] {spell.Name} NOT applied — StatusEffectManager stacking rules prevented it");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== FALSE LIFE — D&D 3.5e PHB p.229 =====
        // Personal spell: 1d10 + min(CL, 10) temp HP, 1 hour/level, no save, no SR.
        // Temp HP are lost before regular HP and cannot be healed.
        // Multiple False Life castings do NOT stack — use the higher value.
        if (spell != null && spell.SpellId == SpellNames.FALSE_LIFE)
        {
            CharacterController recipient = caster; // Personal range — always self
            if (recipient == null || recipient.Stats == null)
                return null;

            int casterLevel = Mathf.Max(1, recipient.Stats.GetCasterLevel());

            // Roll temp HP: 1d10 + min(CL, 10)
            FalseLifeEffectData falseLifeEffect = FalseLifeEffectData.CreateFalseLife(casterLevel, recipient);

            // Track via StatusEffectManager for duration and dispel
            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                // Apply the rolled temp HP via CharacterController (handles non-stacking)
                recipient.ApplyFalseLifeEffect(falseLifeEffect);

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88FF88>💀 {recipient.Stats.CharacterName} casts False Life — gains {falseLifeEffect.CurrentTempHP} temporary HP " +
                                        $"(1d10+{Mathf.Min(casterLevel, 10)}) [{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] False Life applied to {recipient.Stats.CharacterName}: {falseLifeEffect.CurrentTempHP} temp HP, CL {casterLevel}");
            }
            else
            {
                Debug.Log($"[GameManager] False Life NOT applied — StatusEffectManager stacking rules prevented it");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== SANCTUARY — D&D 3.5e PHB p.274 =====
        if (spell != null && spell.SpellId == SpellNames.SANCTUARY)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            int casterLevel = Mathf.Max(1, recipient.Stats.GetCasterLevel());
            int saveDC = 10 + spell.SpellLevel + (recipient.Stats != null ? recipient.Stats.WISMod : 0);

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                recipient.Stats.SanctuaryActive = true;
                recipient.Stats.SanctuaryDC = saveDC;
                recipient.Stats.SanctuaryCasterLevel = casterLevel;

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88DDFF>🛡️ {recipient.Stats.CharacterName} casts Sanctuary — enemies must make Will save DC {saveDC} to attack [{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] Sanctuary applied to {recipient.Stats.CharacterName}: DC {saveDC}, CL {casterLevel}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== HIDE FROM UNDEAD — D&D 3.5e PHB p.241 =====
        if (spell != null && spell.SpellId == SpellNames.HIDE_FROM_UNDEAD)
        {
            if (target == null || target.Stats == null)
                return null;

            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
            int saveDC = 10 + spell.SpellLevel + (caster != null && caster.Stats != null ? caster.Stats.WISMod : 0);

            StatusEffectManager targetStatusMgr = target.GetComponent<StatusEffectManager>();
            if (targetStatusMgr == null)
                targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);

            ActiveSpellEffect effect = targetStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown", casterLevel);
            if (effect != null)
            {
                target.Stats.HideFromUndeadActive = true;
                target.Stats.HideFromUndeadDC = saveDC;
                target.Stats.HideFromUndeadCasterLevel = casterLevel;

                SpellcastingComponent targetSpellComp = target.GetComponent<SpellcastingComponent>();
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88DDFF>👻 {target.Stats.CharacterName} is hidden from undead — mindless undead auto-fail, intelligent undead Will DC {saveDC} [{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] Hide from Undead applied to {target.Stats.CharacterName}: DC {saveDC}, CL {casterLevel}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== REMOVE FEAR — D&D 3.5e PHB p.271 =====
        if (spell != null && spell.SpellId == SpellNames.REMOVE_FEAR)
        {
            if (target == null || target.Stats == null)
                return null;

            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;

            // Remove existing fear conditions (Frightened, Shaken, Panicked)
            bool removedFear = false;
            if (target.HasCondition(CombatConditionType.Frightened))
            {
                target.RemoveCondition(CombatConditionType.Frightened);
                removedFear = true;
                Debug.Log($"[RemoveFear] Removed Frightened from {target.Stats.CharacterName}");
            }
            if (target.HasCondition(CombatConditionType.Shaken))
            {
                target.RemoveCondition(CombatConditionType.Shaken);
                removedFear = true;
                Debug.Log($"[RemoveFear] Removed Shaken from {target.Stats.CharacterName}");
            }
            if (target.HasCondition(CombatConditionType.Panicked))
            {
                target.RemoveCondition(CombatConditionType.Panicked);
                removedFear = true;
                Debug.Log($"[RemoveFear] Removed Panicked from {target.Stats.CharacterName}");
            }

            // Apply +4 morale bonus vs fear for 10 minutes
            StatusEffectManager targetStatusMgr = target.GetComponent<StatusEffectManager>();
            if (targetStatusMgr == null)
                targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);

            ActiveSpellEffect effect = targetStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown", casterLevel);
            if (effect != null)
            {
                target.Stats.RemoveFearMoraleBonus = 4;

                SpellcastingComponent targetSpellComp = target.GetComponent<SpellcastingComponent>();
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                string fearStatus = removedFear ? "Fear removed! " : "";
                CombatUI?.ShowCombatLog($"<color=#AAFF88>✨ {fearStatus}{target.Stats.CharacterName} gains +4 morale bonus vs fear [{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] Remove Fear applied to {target.Stats.CharacterName}: +4 morale vs fear, CL {casterLevel}, fear removed={removedFear}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== ENTROPIC SHIELD — D&D 3.5e PHB p.227 =====
        // Self-only buff: ranged attacks against you have 20% miss chance. Duration 1 min/level.
        if (spell != null && spell.SpellId == SpellNames.ENTROPIC_SHIELD)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            int casterLevel = Mathf.Max(1, recipient.Stats.GetCasterLevel());

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                recipient.Stats.EntropicShieldActive = true;
                recipient.Stats.EntropicShieldCasterLevel = casterLevel;

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88DDFF>🛡️ {recipient.Stats.CharacterName} casts Entropic Shield — ranged attacks have 20% miss chance (CL {casterLevel}) [{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] Entropic Shield applied to {recipient.Stats.CharacterName}: CL {casterLevel}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== MAGIC STONE — D&D 3.5e PHB p.251 =====
        // Self buff: enchants up to 3 pebbles. +1 enhancement to attack, 1d6+1 damage, counts as magic.
        // Duration 30 minutes or until discharged.
        if (spell != null && spell.SpellId == SpellNames.DOMAIN_MAGIC_STONE)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            int casterLevel = Mathf.Max(1, recipient.Stats.GetCasterLevel());

            StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                recipient.Stats.MagicStoneActive = true;
                recipient.Stats.MagicStoneCharges = 3;
                recipient.Stats.MagicStoneCasterLevel = casterLevel;

                SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88DDFF>🪨 {recipient.Stats.CharacterName} casts Magic Stone — 3 pebbles enchanted (+1 atk, 1d6+1 dmg, magic) (CL {casterLevel}) [{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] Magic Stone applied to {recipient.Stats.CharacterName}: 3 charges, CL {casterLevel}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== SHIELD OF FAITH — D&D 3.5e PHB p.278 =====
        // Deflection bonus scales: +2 base, +1 per 6 CL above 1st (max +5 at CL 18)
        if (spell != null && spell.SpellId == SpellNames.SHIELD_OF_FAITH)
        {
            if (target == null || target.Stats == null)
                return null;

            int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;

            // D&D 3.5e: +2 at CL 1-5, +3 at CL 6-11, +4 at CL 12-17, +5 at CL 18+
            int deflectionBonus = 2;
            if (casterLevel >= 18) deflectionBonus = 5;
            else if (casterLevel >= 12) deflectionBonus = 4;
            else if (casterLevel >= 6) deflectionBonus = 3;

            StatusEffectManager targetStatusMgr = target.GetComponent<StatusEffectManager>();
            if (targetStatusMgr == null)
                targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);

            // Override the spell's deflection bonus with scaled value
            SpellData scaledSpell = spell;
            scaledSpell.BuffDeflectionBonus = deflectionBonus;

            ActiveSpellEffect effect = targetStatusMgr.AddEffect(scaledSpell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown", casterLevel);
            if (effect != null)
            {
                target.Stats.DeflectionBonus = Mathf.Max(target.Stats.DeflectionBonus, deflectionBonus);
                target.Stats.ShieldOfFaithDeflectionBonus = deflectionBonus;

                SpellcastingComponent targetSpellComp = target.GetComponent<SpellcastingComponent>();
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog($"<color=#88DDFF>🛡️ {target.Stats.CharacterName} gains +{deflectionBonus} deflection bonus to AC from Shield of Faith (CL {casterLevel}) [{effect.GetDurationDisplayString()}]</color>");
                Debug.Log($"[GameManager] Shield of Faith applied to {target.Stats.CharacterName}: +{deflectionBonus} deflection, CL {casterLevel}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== DISPEL MAGIC — D&D 3.5e PHB p.223 =====
        // Targeted dispel: make one dispel check (1d20 + CL, max +10) vs DC 11 + spell's CL.
        // Check spells in descending CL order. Remove at most ONE spell per casting.
        // Auto-succeeds against own spells.
        if (spell != null && spell.SpellId == SpellNames.DISPEL_MAGIC)
        {
            PerformTargetedDispel(caster, target);
            return null; // Dispel Magic is instantaneous — no ongoing effect to track
        }

        // Use StatusEffectManager for tracked buff application
        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            // Defensive rebind: some encounter presets reinitialize CharacterStats objects on existing
            // character GameObjects, so the manager must always point at the current stats instance.
            statusMgr.Init(target.Stats);

            int casterLevel = caster.Stats != null ? caster.Stats.Level : 1;
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);

            if (effect != null)
            {
                // Also track in SpellcastingComponent's ActiveBuffs for backward compat
                var targetSpellComp = target.GetComponent<SpellcastingComponent>();
                if (targetSpellComp != null)
                {
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;
                }

                // ── Magic Circle area emanation registration ──
                if (AlignmentProtectionRules.IsMagicCircleSpell(spell.SpellId))
                {
                    AlignmentProtectionRules.TryGetProtectionTypeForSpell(spell.SpellId, out AlignmentProtectionType mcWardType);
                    var mcData = new MagicCircleEffectData
                    {
                        WardedAlignment = mcWardType,
                        CenterCreature = target,
                        CasterLevel = casterLevel,
                        RemainingRounds = effect.RemainingRounds,
                        SourceSpellId = spell.SpellId,
                        CasterName = caster.Stats.CharacterName
                    };
                    RegisterMagicCircle(mcData);
                    CombatUI?.ShowCombatLog($"<color=#88CCFF>🔵 {spell.Name} emanation (10-ft radius) centered on {target.Stats.CharacterName}.</color>");
                }

                string durStr = effect.GetDurationDisplayString();
                bool isDebuff = spell.EffectType == SpellEffectType.Debuff || spell.EffectType == SpellEffectType.Control;
                string color = isDebuff ? "#FF8888" : "#88FF88";
                string effectLabel = isDebuff ? "debuff" : "buff";
                CombatUI?.ShowCombatLog($"<color={color}>✨ {spell.Name} {effectLabel} applied to {target.Stats.CharacterName} [{durStr}]</color>");
                Debug.Log($"[GameManager] {spell.Name} {effectLabel} applied to {target.Stats.CharacterName} via StatusEffectManager: {effect.GetDetailedString()}");
            }
            else
            {
                Debug.Log($"[GameManager] {spell.Name} effect NOT applied to {target.Stats.CharacterName} (stacking rules prevented it)");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== LEGACY FALLBACK (no StatusEffectManager) =====
        var legacySpellComp = target.GetComponent<SpellcastingComponent>();

        if (spell.SpellId == SpellNames.MAGE_ARMOR)
        {
            target.Stats.SpellACBonus = spell.BuffACBonus;
            if (legacySpellComp != null)
            {
                legacySpellComp.MageArmorActive = true;
                legacySpellComp.MageArmorACBonus = spell.BuffACBonus;
            }
            else
            {
                SpellcastingComponent.ApplyMageArmor(target, spell);
            }
        }
        else if (spell.BuffAttackBonus != 0 || spell.BuffDamageBonus != 0 || spell.BuffSaveBonus != 0)
        {
            if (spell.BuffAttackBonus != 0) target.Stats.MoraleAttackBonus += spell.BuffAttackBonus;
            if (spell.BuffDamageBonus != 0) target.Stats.MoraleDamageBonus += spell.BuffDamageBonus;
            if (spell.BuffSaveBonus != 0) target.Stats.MoraleSaveBonus += spell.BuffSaveBonus;
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else if (spell.BuffDeflectionBonus > 0)
        {
            target.Stats.DeflectionBonus += spell.BuffDeflectionBonus;
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else if (spell.BuffShieldBonus > 0)
        {
            target.Stats.ShieldBonus += spell.BuffShieldBonus;
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else if (!string.IsNullOrEmpty(spell.BuffStatName) && spell.BuffStatBonus != 0)
        {
            ApplyStatBuff(target, spell.BuffStatName, spell.BuffStatBonus);
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else if (spell.BuffTempHP > 0)
        {
            target.Stats.TempHP += spell.BuffTempHP;
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else
        {
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
            else if (spellComp != null) spellComp.ApplyBuff(spell);
        }

        Debug.Log($"[GameManager] {spell.Name} buff applied to {target.Stats.CharacterName} (legacy path)");
        return null; // Legacy path doesn't return tracked effects
    }

    /// <summary>
    /// Apply a stat buff to a target character (e.g., +4 STR from Bull's Strength).
    /// </summary>
    private void ApplyStatBuff(CharacterController target, string statName, int bonus)
    {
        switch (statName.ToUpper())
        {
            case "STR":
                target.Stats.STR += bonus;
                break;
            case "DEX":
                target.Stats.DEX += bonus;
                break;
            case "CON":
                target.Stats.CON += bonus;
                int hpBonus = (bonus / 2) * target.Stats.Level;
                target.Stats.CurrentHP += hpBonus;
                target.Stats.BonusMaxHP += hpBonus;
                break;
            case "INT":
                target.Stats.INT += bonus;
                break;
            case "WIS":
                target.Stats.WIS += bonus;
                break;
            case "CHA":
                target.Stats.CHA += bonus;
                break;
            default:
                Debug.Log($"[GameManager] Unknown stat buff target: {statName}");
                break;
        }
    }

    /// <summary>
    /// Tick all spell effect durations for all characters (PCs and NPCs).
    /// Called at the start of each new combat round.
    /// Removes expired effects and reverses their stat modifications.
    /// </summary>
    private void TickAllSpellDurations()
    {
        Debug.Log($"[SpellDuration] Ticking spell durations for round {CurrentRound}...");

        // Tick active, living PCs
        foreach (var pc in PCs)
        {
            if (!IsActiveCombatant(pc) || pc.Stats.IsDead) continue;
            TickCharacterSpellDurations(pc);
        }

        // Tick active, living NPCs
        foreach (var npc in NPCs)
        {
            if (!IsActiveCombatant(npc) || npc.Stats.IsDead) continue;
            TickCharacterSpellDurations(npc);
        }

        // Tick alignment/undead detection effects (concentration-based, separate from StatusEffectManager)
        TickAllAlignmentDetectionDurations();

        UpdateAllStatsUI();
    }

    /// <summary>
    /// Tick alignment/undead detection effects for all characters each round.
    /// Updates scans (progressive detail) and decrements duration.
    /// </summary>
    private void TickAllAlignmentDetectionDurations()
    {
        var allCharacters = GetAllLivingCharacters();

        foreach (var character in allCharacters)
        {
            if (character == null || !character.HasActiveAlignmentDetection) continue;

            // Update scan with progressive detail (round 1/2/3+)
            UpdateAlignmentDetectionForRound(character);

            // Tick duration down; remove if expired
            string detSpellName = character.ActiveAlignmentDetectionEffect?.SpellName ?? "Detection";
            bool expired = character.TickAlignmentDetectionDuration();
            if (expired)
            {
                CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ {detSpellName} expires on {character.Stats.CharacterName}.</color>");
                ClearDetectionHighlights();
            }
        }
    }

    /// <summary>
    /// Tick spell durations for a single character.
    /// </summary>
    private void TickCharacterSpellDurations(CharacterController character)
    {
        if (!IsActiveCombatant(character) || character.Stats.IsDead)
            return;
        var statusMgr = character.GetComponent<StatusEffectManager>();
        if (statusMgr != null && statusMgr.ActiveEffectCount > 0)
        {
            var expired = statusMgr.TickAllEffects();

            foreach (var effect in expired)
            {
                string msg = $"⏱ {effect.Spell?.Name ?? "Unknown"} has expired on {character.Stats.CharacterName}!";
                Debug.Log($"[SpellDuration] {msg}");
                CombatUI?.ShowCombatLog($"<color=#FFAA44>{msg}</color>");

                if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.DISGUISE_SELF, StringComparison.Ordinal))
                {
                    CombatUI?.ShowCombatLog($"<color=#88CCFF>🎭 {character.Stats.CharacterName}'s disguise fades; visible race returns to {character.DisplayedRace}.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.EXPEDITIOUS_RETREAT, StringComparison.Ordinal))
                {
                    ExpeditiousRetreatEffectData expiredData = character.RemoveExpeditiousRetreatEffect();
                    int speedDelta = expiredData != null ? Mathf.Max(0, expiredData.SpeedBonusFeet) : Mathf.Max(0, effect.AppliedSpeedBonusFeet);
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Expeditious Retreat expires on {character.Stats.CharacterName}: speed -{speedDelta} ft.</color>");
                }
                else if (effect.Spell != null && (string.Equals(effect.Spell.SpellId, SpellNames.INVISIBILITY, StringComparison.Ordinal)
                    || string.Equals(effect.Spell.SpellId, "greater_invisibility", StringComparison.Ordinal)
                    || string.Equals(effect.Spell.SpellId, "improved_invisibility", StringComparison.Ordinal)))
                {
                    character.ClearInvisibilityEffect();
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ {effect.Spell.Name} expires on {character.Stats.CharacterName}.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.SEE_INVISIBLE, StringComparison.Ordinal))
                {
                    character.ClearSeeInvisibilityEffect();
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ {character.Stats.CharacterName}'s See Invisible expires.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.GLITTERDUST, StringComparison.Ordinal))
                {
                    character.ClearGlitterdustEffect();
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Glitterdust fades from {character.Stats.CharacterName}.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.PROTECTION_FROM_ARROWS, StringComparison.Ordinal))
                {
                    character.Stats.ActiveProtectionFromArrowsEffect = null;
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Protection from Arrows expires on {character.Stats.CharacterName}.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.STONESKIN, StringComparison.Ordinal))
                {
                    character.Stats.ActiveStoneskinEffect = null;
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Stoneskin expires on {character.Stats.CharacterName}.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.DIMENSIONAL_ANCHOR, StringComparison.Ordinal))
                {
                    character.Stats.ActiveDimensionalAnchorEffect = null;
                    CombatUI?.ShowCombatLog($"<color=#00FF88>⏱ Dimensional Anchor fades from {character.Stats.CharacterName}. Extradimensional travel is no longer blocked.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.MIRROR_IMAGE, StringComparison.Ordinal))
                {
                    OnMirrorImageEffectExpired(character);
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.HASTE, StringComparison.Ordinal))
                {
                    character.ClearHasteEffect();
                    if (character.Stats != null)
                    {
                        character.Stats.HasteAttackBonus = 0;
                        character.Stats.HasteACBonus = 0;
                        character.Stats.HasteReflexBonus = 0;
                    }
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Haste expires on {character.Stats.CharacterName}: attack, AC, Reflex, and speed bonuses removed.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.SLOW, StringComparison.Ordinal))
                {
                    character.ClearSlowEffect();
                    if (character.Stats != null)
                    {
                        character.Stats.SlowAttackPenalty = 0;
                        character.Stats.SlowACPenalty = 0;
                        character.Stats.SlowReflexPenalty = 0;
                        character.Stats.SlowSpeedMultiplier = 1f;
                    }
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Slow expires on {character.Stats.CharacterName}: penalties removed, full actions restored.</color>");
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.FIRE_SHIELD, StringComparison.Ordinal))
                {
                    if (character.Stats != null)
                    {
                        character.Stats.FireShieldActive = false;
                        character.Stats.FireShieldIsWarm = false;
                        character.Stats.FireShieldCasterLevel = 0;
                        character.Stats.FireShieldDurationRounds = 0;
                    }
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Fire Shield expires on {character.Stats.CharacterName}: retribution and elemental damage reduction removed.</color>");
                }
                else if (effect.Spell != null && AlignmentDetectionEffectData.IsDetectionSpell(effect.Spell.SpellId))
                {
                    character.RemoveAlignmentDetectionEffect();
                    ClearDetectionHighlights();
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ {effect.Spell.Name} expires on {character.Stats.CharacterName}.</color>");
                }
                // NOTE: Resilient Sphere expiry is now handled by the area effect system
                // (ResilientSphereAreaEffect.OnRoundStart → RoundsRemaining countdown → ExpireEffect).
                // No character-based cleanup needed here.
            }

            if (statusMgr.ActiveEffectCount > 0)
            {
                foreach (var effect in statusMgr.ActiveEffects)
                {
                    if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.DISGUISE_SELF, StringComparison.Ordinal))
                        character.UpdateDisguiseSelfDuration(effect.RemainingRounds);
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.EXPEDITIOUS_RETREAT, StringComparison.Ordinal))
                        character.UpdateExpeditiousRetreatDuration(effect.RemainingRounds);
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.INVISIBILITY, StringComparison.Ordinal))
                        character.UpdateInvisibilityDuration(effect.RemainingRounds);
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.SEE_INVISIBLE, StringComparison.Ordinal))
                        character.UpdateSeeInvisibilityDuration(effect.RemainingRounds);
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.GLITTERDUST, StringComparison.Ordinal))
                    {
                        character.UpdateGlitterdustDuration(effect.RemainingRounds);
                        character.SetGlitterdustBlindedState(HasCondition(character, CombatConditionType.Blinded));
                    }
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.PROTECTION_FROM_ARROWS, StringComparison.Ordinal))
                    {
                        ProtectionFromArrowsEffectData protection = character.Stats.ActiveProtectionFromArrowsEffect;
                        if (protection != null)
                            protection.DurationRemainingRounds = effect.RemainingRounds;
                    }
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.STONESKIN, StringComparison.Ordinal))
                    {
                        StoneskinEffectData stoneskin = character.Stats.ActiveStoneskinEffect;
                        if (stoneskin != null)
                            stoneskin.DurationRemainingRounds = effect.RemainingRounds;
                    }
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.DIMENSIONAL_ANCHOR, StringComparison.Ordinal))
                    {
                        DimensionalAnchorEffectData anchor = character.Stats.ActiveDimensionalAnchorEffect;
                        if (anchor != null)
                            anchor.DurationRemainingRounds = effect.RemainingRounds;
                    }
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.MIRROR_IMAGE, StringComparison.Ordinal))
                    {
                        SyncMirrorImageDurationForCaster(character, statusMgr);
                    }
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.HASTE, StringComparison.Ordinal))
                        character.UpdateHasteDuration(effect.RemainingRounds);
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.SLOW, StringComparison.Ordinal))
                        character.UpdateSlowDuration(effect.RemainingRounds);
                    else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.FIRE_SHIELD, StringComparison.Ordinal))
                        character.Stats.FireShieldDurationRounds = effect.RemainingRounds;
                    // NOTE: Resilient Sphere duration is now tracked by the area effect itself,
                    // not on CharacterStats.

                    Debug.Log($"[SpellDuration] {character.Stats.CharacterName}: {effect.GetDisplayString()}");
                }
            }
        }

        if (character.Stats != null && character.Stats.ActiveResistEnergyEffects != null && character.Stats.ActiveResistEnergyEffects.Count > 0)
        {
            for (int i = character.Stats.ActiveResistEnergyEffects.Count - 1; i >= 0; i--)
            {
                ResistEnergyEffectData effect = character.Stats.ActiveResistEnergyEffects[i];
                if (effect == null)
                {
                    character.Stats.ActiveResistEnergyEffects.RemoveAt(i);
                    continue;
                }

                if (effect.DurationRemainingRounds >= 0)
                    effect.DurationRemainingRounds--;

                if (effect.DurationRemainingRounds <= 0)
                {
                    string energyLabel = DamageTextUtils.GetDamageTypeDisplay(effect.ToDamageType());
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Resist Energy ({energyLabel}) expires on {character.Stats.CharacterName}.</color>");
                    character.Stats.ActiveResistEnergyEffects.RemoveAt(i);
                }
            }
        }

        // Tick Protection from Energy durations
        if (character.Stats != null && character.Stats.ActiveProtectionFromEnergyEffects != null && character.Stats.ActiveProtectionFromEnergyEffects.Count > 0)
        {
            for (int i = character.Stats.ActiveProtectionFromEnergyEffects.Count - 1; i >= 0; i--)
            {
                ProtectionFromEnergyEffectData protEffect = character.Stats.ActiveProtectionFromEnergyEffects[i];
                if (protEffect == null)
                {
                    character.Stats.ActiveProtectionFromEnergyEffects.RemoveAt(i);
                    continue;
                }

                if (protEffect.DurationRemainingRounds >= 0)
                    protEffect.DurationRemainingRounds--;

                if (protEffect.DurationRemainingRounds <= 0)
                {
                    string protEnergyLabel = protEffect.GetDisplayLabel();
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Protection from Energy ({protEnergyLabel}) expires on {character.Stats.CharacterName}.</color>");
                    character.Stats.ActiveProtectionFromEnergyEffects.RemoveAt(i);
                }
            }
        }

        EnfeebledConditionData expiredEnfeeblement = character.TickEnfeeblementEffect();
        if (expiredEnfeeblement != null)
        {
            int amount = Mathf.Max(0, expiredEnfeeblement.StrengthPenaltyAmount);
            string sourceName = !string.IsNullOrWhiteSpace(expiredEnfeeblement.CasterName)
                ? expiredEnfeeblement.CasterName
                : "an unknown caster";
            CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Ray of Enfeeblement expires on {character.Stats.CharacterName}: STR +{amount} restored (source: {sourceName}).</color>");
        }

        TouchOfIdiocyConditionData expiredIdiocy = character.TickTouchOfIdiocyEffect();
        if (expiredIdiocy != null)
        {
            CombatUI?.ShowCombatLog(
                $"<color=#FFAA44>⏱ Touch of Idiocy expires on {character.Stats.CharacterName}: " +
                $"INT +{Mathf.Max(0, expiredIdiocy.IntelligenceDamage)}, " +
                $"WIS +{Mathf.Max(0, expiredIdiocy.WisdomDamage)}, " +
                $"CHA +{Mathf.Max(0, expiredIdiocy.CharismaDamage)} restored.</color>");
        }

        TickCharacterItemSpellDurations(character);

        // Tick custom cleric spell duration counters (level 3 + level 4)
        TickClericSpell3Durations(character);
        TickClericSpell4Durations(character);
    }

    private void TickCharacterItemSpellDurations(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        InventoryComponent inventoryComponent = character.GetComponent<InventoryComponent>();
        Inventory inventory = inventoryComponent != null ? inventoryComponent.CharacterInventory : null;
        if (inventory == null)
            return;

        var processed = new HashSet<ItemData>();

        // Equipped slots.
        TickItemSlotSpellDurations(character, inventory.RightHandSlot, processed);
        TickItemSlotSpellDurations(character, inventory.LeftHandSlot, processed);
        TickItemSlotSpellDurations(character, inventory.HandsSlot, processed);
        TickItemSlotSpellDurations(character, inventory.HeadSlot, processed);
        TickItemSlotSpellDurations(character, inventory.FaceEyesSlot, processed);
        TickItemSlotSpellDurations(character, inventory.NeckSlot, processed);
        TickItemSlotSpellDurations(character, inventory.TorsoSlot, processed);
        TickItemSlotSpellDurations(character, inventory.ArmorRobeSlot, processed);
        TickItemSlotSpellDurations(character, inventory.WaistSlot, processed);
        TickItemSlotSpellDurations(character, inventory.BackSlot, processed);
        TickItemSlotSpellDurations(character, inventory.WristsSlot, processed);
        TickItemSlotSpellDurations(character, inventory.LeftRingSlot, processed);
        TickItemSlotSpellDurations(character, inventory.RightRingSlot, processed);
        TickItemSlotSpellDurations(character, inventory.FeetSlot, processed);

        // Full backpack inventory.
        if (inventory.GeneralSlots != null)
        {
            for (int i = 0; i < inventory.GeneralSlots.Length; i++)
                TickItemSlotSpellDurations(character, inventory.GeneralSlots[i], processed);
        }
    }

    private void TickItemSlotSpellDurations(CharacterController owner, ItemData item, HashSet<ItemData> processed)
    {
        if (owner == null || item == null || processed == null)
            return;

        if (processed.Contains(item))
            return;

        processed.Add(item);
        List<ItemSpellEffect> expired = item.TickItemSpellEffects();
        if (expired == null || expired.Count == 0)
            return;

        for (int i = 0; i < expired.Count; i++)
        {
            ItemSpellEffect effect = expired[i];
            if (effect == null)
                continue;

            string spellName = string.IsNullOrWhiteSpace(effect.SpellName) ? "Item Spell" : effect.SpellName;
            CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ {spellName} expires on {owner.Stats.CharacterName}'s {item.Name}.</color>");
        }
    }


    private List<CharacterController> GetThreateningEnemiesForSpellcasting(CharacterController caster)
    {
        var threatening = ThreatSystem.GetThreateningEnemies(caster.GridPosition, caster, GetAllCharacters());
        threatening.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));
        return threatening;
    }

    private bool ResolveEntangledSomaticCastingConcentration(
        CharacterController caster,
        SpellcastingComponent spellComp,
        SpellData spell,
        MetamagicData metamagic,
        bool hasMetamagicApplied,
        int slotLevelToConsume,
        bool isSpontaneous,
        int spontaneousLevel,
        string spontaneousSacrificedSpellId)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!caster.HasCondition(CombatConditionType.Entangled) || !spell.HasSomaticComponent)
            return true;

        int dc = 15 + Mathf.Max(0, spell.SpellLevel);
        int roll = DiceService.D20("Dispel Magic check");
        int bonus = Mathf.Max(0, caster.Stats.GetDomainBoostedCasterLevel(spell)) + GetSpellSaveAbilityModifier(caster, spell);
        int total = roll + bonus;
        bool success = total >= dc;

        CombatUI?.ShowCombatLog($"🪢 Entangled somatic concentration ({caster.Stats.CharacterName}, {spell.Name}): d20 {roll} + {bonus} = {total} vs DC {dc}.");

        if (success)
            return true;

        bool consumed = ConsumePendingSpellSlot(
            spellComp,
            spell,
            metamagic,
            hasMetamagicApplied,
            slotLevelToConsume,
            isSpontaneous,
            spontaneousLevel,
            spontaneousSacrificedSpellId);

        if (!consumed)
        {
            Debug.LogError($"[GameManager] Entangled concentration failure path: could not consume level {slotLevelToConsume} slot for {spell.Name}");
            return false;
        }

        CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} fails the DC {dc} concentration check while entangled. {spell.Name} is lost and the spell slot is spent.");
        return false;
    }

    private bool AttemptCastDefensively(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null) return false;

        int dc = 15 + spell.SpellLevel;
        var check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
            caster,
            dc,
            ConcentrationCheckType.CastingDefensively,
            spell);

        LogSpellcastingConcentrationCheck(caster, spell, check);

        if (!check.Success)
        {
            CombatUI?.ShowCombatLog($"<color=#FF6644>💥 {caster.Stats.CharacterName} fails to cast defensively. {spell.Name} is lost!</color>");
            return false;
        }

        CombatUI?.ShowCombatLog($"<color=#88CCFF>🛡 {caster.Stats.CharacterName} casts defensively and avoids attacks of opportunity.</color>");
        return true;
    }

    private void ResolveSpellcastProvocation(CharacterController caster, SpellData spell, bool isDeliveringHeldCharge, System.Action<bool> onResolved)
    {
        _spellcastProvocationCancelled = false;

        if (caster == null || spell == null || isDeliveringHeldCharge)
        {
            onResolved?.Invoke(true);
            return;
        }

        var threateningEnemies = GetThreateningEnemiesForSpellcasting(caster);
        if (threateningEnemies == null || threateningEnemies.Count == 0)
        {
            onResolved?.Invoke(true);
            return;
        }

        CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} is casting {spell.Name} while threatened ({threateningEnemies.Count} adjacent).");

        int defensiveDC = 15 + spell.SpellLevel;
        int concentrationBonus = caster.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: true);
        float successChance = CalculateDefensiveCastSuccessChancePercent(concentrationBonus, defensiveDC);

        ShowAoOActionConfirmation(new AoOProvokingActionInfo
        {
            ActionType = AoOProvokingAction.CastSpell,
            ActionName = $"CAST {spell.Name.ToUpper()}",
            ActionDescription = $"Cast {spell.Name}",
            Actor = caster,
            ThreateningEnemies = threateningEnemies,
            Spell = spell,
            CastDefensivelyDC = defensiveDC,
            ConcentrationBonus = concentrationBonus,
            SuccessChance = successChance,
            OnCastDefensively = () => onResolved?.Invoke(AttemptCastDefensively(caster, spell)),
            OnProceed = () => ResolveSpellcastAoOs(caster, spell, threateningEnemies, onResolved),
            OnCancel = () =>
            {
                _spellcastProvocationCancelled = true;
                onResolved?.Invoke(false);
            }
        });
    }

    private void ResolveSpellcastAoOs(CharacterController caster, SpellData spell, List<CharacterController> threateningEnemies, System.Action<bool> onResolved)
    {
        if (caster == null || spell == null)
        {
            onResolved?.Invoke(false);
            return;
        }

        if (threateningEnemies == null || threateningEnemies.Count == 0)
        {
            onResolved?.Invoke(true);
            return;
        }

        CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} casts normally and provokes {threateningEnemies.Count} attack(s) of opportunity.");

        foreach (var enemy in threateningEnemies)
        {
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy))
                continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, caster);
            if (aooResult == null)
                continue;

            CombatUI?.ShowCombatLog($"⚔ AoO vs spellcasting: {aooResult.GetDetailedSummary()}");

            if (aooResult.Hit && aooResult.TotalDamage > 0)
            {
                // Existing concentration effects / held charges can also be disrupted by this damage.
                CheckConcentrationOnDamage(caster, aooResult.TotalDamage);

                int dc = 10 + aooResult.TotalDamage + spell.SpellLevel;
                var check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
                    caster,
                    dc,
                    ConcentrationCheckType.DamagedWhileCasting,
                    spell,
                    aooResult.TotalDamage);

                LogSpellcastingConcentrationCheck(caster, spell, check);

                if (!check.Success)
                {
                    CombatUI?.ShowCombatLog($"<color=#FF6644>💥 {caster.Stats.CharacterName}'s casting is interrupted by damage. {spell.Name} is lost!</color>");
                    onResolved?.Invoke(false);
                    return;
                }
            }

            if (caster.Stats.IsDead)
            {
                CombatUI?.ShowCombatLog($"<color=#FF6644>💀 {caster.Stats.CharacterName} is slain while casting {spell.Name}!</color>");
                onResolved?.Invoke(false);
                return;
            }
        }

        onResolved?.Invoke(true);
    }

    private void LogSpellcastingConcentrationCheck(CharacterController caster, SpellData spell, ConcentrationCheckResult check)
    {
        if (caster == null || caster.Stats == null || spell == null || check == null) return;

        string reason = check.CheckType == ConcentrationCheckType.CastingDefensively
            ? "Cast Defensively"
            : check.CheckType == ConcentrationCheckType.DamagedWhileCasting
                ? $"Damaged While Casting ({check.DamageDealt} dmg)"
                : check.CheckType.ToString();

        string status = check.Success ? "SUCCESS" : "FAIL";
        string color = check.Success ? "#88CCFF" : "#FF6644";

        CombatUI?.ShowCombatLog($"<color={color}>Concentration [{reason}] {caster.Stats.CharacterName}: d20 {check.Roll} + {check.Bonus} = {check.Total} vs DC {check.DC} — {status}</color>");
    }

    private void CaptureSpellcastResourceSnapshot(CharacterController caster)
    {
        _pendingSpellcastSnapshot = null;

        if (caster == null)
            return;

        var snapshot = new SpellcastResourceSnapshot
        {
            Caster = caster,
            MoveActionUsed = caster.Actions.MoveActionUsed,
            StandardActionUsed = caster.Actions.StandardActionUsed,
            FullRoundActionUsed = caster.Actions.FullRoundActionUsed,
            SwiftActionUsed = caster.Actions.SwiftActionUsed,
            StandardConvertedToMove = caster.Actions.StandardConvertedToMove,
            SlotUsedStates = null,
            QuickenedSpellUsed = false
        };

        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp != null)
        {
            snapshot.QuickenedSpellUsed = spellComp.HasCastQuickenedSpellThisRound;

            if (spellComp.SpellSlots != null && spellComp.SpellSlots.Count > 0)
            {
                snapshot.SlotUsedStates = new List<bool>(spellComp.SpellSlots.Count);
                foreach (var slot in spellComp.SpellSlots)
                    snapshot.SlotUsedStates.Add(slot != null && slot.IsUsed);
            }
        }

        _pendingSpellcastSnapshot = snapshot;
    }

    private void ClearSpellcastResourceSnapshot()
    {
        _pendingSpellcastSnapshot = null;
    }

    private void RestoreSpellcastResourceSnapshot(CharacterController caster)
    {
        if (_pendingSpellcastSnapshot == null || caster == null || _pendingSpellcastSnapshot.Caster != caster)
            return;

        caster.Actions.MoveActionUsed = _pendingSpellcastSnapshot.MoveActionUsed;
        caster.Actions.StandardActionUsed = _pendingSpellcastSnapshot.StandardActionUsed;
        caster.Actions.FullRoundActionUsed = _pendingSpellcastSnapshot.FullRoundActionUsed;
        caster.Actions.SwiftActionUsed = _pendingSpellcastSnapshot.SwiftActionUsed;
        caster.Actions.StandardConvertedToMove = _pendingSpellcastSnapshot.StandardConvertedToMove;

        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp != null)
        {
            spellComp.HasCastQuickenedSpellThisRound = _pendingSpellcastSnapshot.QuickenedSpellUsed;

            if (_pendingSpellcastSnapshot.SlotUsedStates != null && spellComp.SpellSlots != null)
            {
                int count = Mathf.Min(_pendingSpellcastSnapshot.SlotUsedStates.Count, spellComp.SpellSlots.Count);
                for (int i = 0; i < count; i++)
                {
                    if (spellComp.SpellSlots[i] != null)
                        spellComp.SpellSlots[i].IsUsed = _pendingSpellcastSnapshot.SlotUsedStates[i];
                }

                spellComp.SyncPreparedSpellsFromSlots();
            }
        }

        _pendingSpellcastSnapshot = null;
    }

    private void HandleSpellcastCancelledFromAoOPrompt(CharacterController caster)
    {
        RestoreSpellcastResourceSnapshot(caster);
        _spellcastProvocationCancelled = false;

        _pendingSpell = null;
        _pendingMetamagic = null;
        _pendingSpellFromHeldCharge = false;
        ResetPendingGreaseCastMode();

        Grid.ClearAllHighlights();
        UpdateAllStatsUI();

        if (caster != null && caster.Stats != null)
            CombatUI?.ShowCombatLog($"↩ {caster.Stats.CharacterName} cancels spell cast.");

        ShowActionChoices();
    }

    private void HandleInterruptedSpellCast(CharacterController caster, float delaySeconds = 1.0f)
    {
        _pendingSpell = null;
        _pendingMetamagic = null;
        _pendingSpellFromHeldCharge = false;
        ResetPendingGreaseCastMode();

        Grid.ClearAllHighlights();
        UpdateAllStatsUI();

        if (caster != null)
            StartCoroutine(AfterAttackDelay(caster, delaySeconds));
        else
            ShowActionChoices();
    }
    // ========== CONCENTRATION MECHANICS (D&D 3.5e PHB) ==========

    private bool IsCasterMaintainingSummonSwarmConcentration(CharacterController caster)
    {
        if (caster == null)
            return false;

        ConcentrationManager concMgr = caster.GetComponent<ConcentrationManager>();
        if (concMgr == null || !concMgr.IsConcentrating || concMgr.ConcentratingOn == null || concMgr.ConcentratingOn.Spell == null)
            return false;

        return string.Equals(concMgr.ConcentratingOn.Spell.SpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal);
    }

    private void BeginSummonSwarmConcentration(CharacterController caster, SpellData spell, CharacterController summonedSwarm)
    {
        if (caster == null || spell == null)
            return;

        ConcentrationManager concMgr = caster.GetComponent<ConcentrationManager>();
        if (concMgr == null)
            return;

        if (concMgr.IsConcentrating
            && concMgr.ConcentratingOn != null
            && concMgr.ConcentratingOn.Spell != null
            && string.Equals(concMgr.ConcentratingOn.Spell.SpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal))
        {
            TransitionSummonSwarmToPostConcentration(caster, "new concentration spell");
        }

        ActiveSpellEffect concentrationMarker = new ActiveSpellEffect(spell, caster.Stats != null ? caster.Stats.CharacterName : "Caster", caster.Stats != null ? caster.Stats.GetDomainBoostedCasterLevel(spell) : 1, caster.Stats != null ? caster.Stats.CharacterName : "Caster");
        string log = concMgr.BeginConcentration(concentrationMarker);
        if (!string.IsNullOrEmpty(log))
            CombatUI?.ShowCombatLog($"<color=#44AAFF>{log}</color>");

        if (summonedSwarm != null)
        {
            ActiveSummonInstance active = GetActiveSummon(summonedSwarm);
            if (active != null)
            {
                active.IsConcentrationSummon = true;
                active.HasEnteredPostConcentrationDuration = false;
                active.RemainingRounds = 2;
                active.TotalDurationRounds = 2;
            }
        }
    }

    private void TransitionSummonSwarmToPostConcentration(CharacterController caster, string reason)
    {
        if (caster == null)
            return;

        bool transitionedAny = false;
        for (int i = 0; i < _activeSummons.Count; i++)
        {
            ActiveSummonInstance summon = _activeSummons[i];
            if (summon == null || summon.Caster != caster || !summon.IsConcentrationSummon)
                continue;
            if (!string.Equals(summon.SourceSpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal))
                continue;

            if (!summon.HasEnteredPostConcentrationDuration)
            {
                summon.HasEnteredPostConcentrationDuration = true;
                summon.RemainingRounds = 2;
                summon.TotalDurationRounds = 2;
                transitionedAny = true;
            }
        }

        if (transitionedAny)
        {
            CombatUI?.ShowCombatLog($"<color=#FFAA44>Concentration on Summon Swarm ended ({reason}). Swarm will last 2 more rounds.</color>");
        }
    }

    /// <summary>
    /// Check if a character needs to make concentration checks after taking damage.
    /// Applies to both ongoing concentration spells and held touch charges.
    /// </summary>
    private void CheckConcentrationOnDamage(CharacterController character, int damageTaken)
    {
        if (character == null || damageTaken <= 0) return;

        var concMgr = character.GetComponent<ConcentrationManager>();
        var spellComp = character.GetComponent<SpellcastingComponent>();

        bool hasConcentrationSpell = concMgr != null && concMgr.IsConcentrating;
        bool hasHeldTouchCharge = spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null;

        if (!hasConcentrationSpell && !hasHeldTouchCharge) return;

        // If the character is dead, concentration and held charge break automatically.
        if (character.Stats.IsDead)
        {
            if (hasConcentrationSpell)
            {
                bool wasSummonSwarm = IsCasterMaintainingSummonSwarmConcentration(character);
                string breakLog = concMgr.ForceBreakConcentration("killed");
                if (!string.IsNullOrEmpty(breakLog))
                    CombatUI?.ShowCombatLog($"<color=#FF6644>{breakLog}</color>");
                if (wasSummonSwarm)
                    TransitionSummonSwarmToPostConcentration(character, "caster incapacitated");
            }

            if (hasHeldTouchCharge)
            {
                string lostSpellName = spellComp.HeldTouchSpell.Name;
                spellComp.ClearHeldTouchCharge("killed");
                CombatUI?.ShowCombatLog($"<color=#FF6644>💥 {character.Stats.CharacterName}'s held {lostSpellName} charge is lost (killed)!</color>");
            }

            UpdateAllStatsUI();
            return;
        }

        // 1) Held touch charge concentration check (injury formula)
        if (hasHeldTouchCharge)
        {
            var heldResult = concMgr != null
                ? concMgr.CheckHeldChargeOnDamage(spellComp.HeldTouchSpell, damageTaken)
                : new ConcentrationCheckResult { Success = true, LogMessage = "" };

            if (!string.IsNullOrEmpty(heldResult.LogMessage))
            {
                string color = heldResult.Success ? "#88CCFF" : "#FF6644";
                CombatUI?.ShowCombatLog($"<color={color}>{heldResult.LogMessage}</color>");
            }

            if (!heldResult.Success)
            {
                string lostSpellName = spellComp.HeldTouchSpell.Name;
                spellComp.ClearHeldTouchCharge("failed concentration after damage");
                CombatUI?.ShowCombatLog($"<color=#FF6644>💥 {character.Stats.CharacterName} loses concentration and the held {lostSpellName} charge dissipates!</color>");
            }
        }

        // 2) Ongoing concentration spell check
        if (hasConcentrationSpell)
        {
            bool wasSummonSwarm = IsCasterMaintainingSummonSwarmConcentration(character);
            var result = concMgr.CheckConcentrationOnDamage(damageTaken);
            if (!string.IsNullOrEmpty(result.LogMessage))
            {
                string color = result.Success ? "#88CCFF" : "#FF6644";
                CombatUI?.ShowCombatLog($"<color={color}>{result.LogMessage}</color>");
            }

            if (!result.Success && wasSummonSwarm)
                TransitionSummonSwarmToPostConcentration(character, "failed concentration check");
        }

        UpdateAllStatsUI();
    }

    /// <summary>
    /// Check concentration when a character casts a spell while already concentrating.
    /// If the new spell is also a concentration spell, the old one ends automatically.
    /// If the new spell is NOT a concentration spell, requires a check (DC 15 + new spell level).
    /// </summary>
    /// <param name="caster">The caster.</param>
    /// <param name="newSpell">The spell being cast.</param>
    /// <returns>True if casting can proceed, false if concentration check failed and casting should be aborted.</returns>
    private bool HandleConcentrationOnCasting(CharacterController caster, SpellData newSpell)
    {
        if (caster == null || newSpell == null) return true;

        var concMgr = caster.GetComponent<ConcentrationManager>();
        if (concMgr == null || !concMgr.IsConcentrating) return true;

        if (IsCasterMaintainingSummonSwarmConcentration(caster)
            && !string.Equals(newSpell.SpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal))
        {
            string endLog = concMgr.EndConcentration();
            if (!string.IsNullOrEmpty(endLog))
                CombatUI?.ShowCombatLog($"<color=#FFAA44>{endLog}</color>");

            TransitionSummonSwarmToPostConcentration(caster, "caster cast another spell");
            return true;
        }

        // If the new spell is a concentration spell, the old one ends automatically
        // (handled in BeginConcentration). No check needed, casting proceeds.
        if (newSpell.DurationType == DurationType.Concentration)
        {
            return true;
        }

        // Casting a non-concentration spell while concentrating requires a check
        // DC = 15 + spell level of the NEW spell
        bool wasSummonSwarm = IsCasterMaintainingSummonSwarmConcentration(caster);
        var result = concMgr.CheckConcentrationOnCasting(newSpell.SpellLevel);
        if (!string.IsNullOrEmpty(result.LogMessage))
        {
            string color = result.Success ? "#88CCFF" : "#FF6644";
            CombatUI?.ShowCombatLog($"<color={color}>{result.LogMessage}</color>");
        }

        if (!result.Success)
        {
            if (wasSummonSwarm)
                TransitionSummonSwarmToPostConcentration(caster, "failed concentration while casting");

            UpdateAllStatsUI();
        }

        // Casting always proceeds — the check only affects the existing concentration spell
        return true;
    }

    /// <summary>
    /// After a concentration spell is successfully cast and its effect applied,
    /// begin tracking concentration for the caster.
    /// </summary>
    /// <param name="caster">The caster of the concentration spell.</param>
    /// <param name="effect">The ActiveSpellEffect that was created.</param>
    /// <param name="spell">The concentration spell that was cast.</param>
    private void BeginConcentrationTracking(CharacterController caster, ActiveSpellEffect effect, SpellData spell)
    {
        if (caster == null || effect == null || spell == null) return;
        if (spell.DurationType != DurationType.Concentration) return;

        var concMgr = caster.GetComponent<ConcentrationManager>();
        if (concMgr == null) return;

        if (IsCasterMaintainingSummonSwarmConcentration(caster)
            && !string.Equals(spell.SpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal))
        {
            TransitionSummonSwarmToPostConcentration(caster, "started another concentration spell");
        }

        string log = concMgr.BeginConcentration(effect);
        if (!string.IsNullOrEmpty(log))
        {
            CombatUI?.ShowCombatLog($"<color=#44AAFF>{log}</color>");
        }
    }

    /// <summary>
    /// Voluntarily end concentration for a character (free action).
    /// Called from UI "End Concentration" button.
    /// </summary>
    public void EndConcentrationVoluntarily(CharacterController character)
    {
        if (character == null) return;

        var concMgr = character.GetComponent<ConcentrationManager>();
        if (concMgr == null || !concMgr.IsConcentrating) return;

        bool wasSummonSwarm = IsCasterMaintainingSummonSwarmConcentration(character);
        string log = concMgr.EndConcentration();
        if (!string.IsNullOrEmpty(log))
        {
            CombatUI?.ShowCombatLog($"<color=#FFAA44>{log}</color>");
        }

        if (wasSummonSwarm)
            TransitionSummonSwarmToPostConcentration(character, "voluntary end");

        UpdateAllStatsUI();
    }

    // ========================================================================
    //  DETECT ALIGNMENT / UNDEAD — PHB p.218-220
    //  Concentration spells that reveal aligned or undead creatures in 60 ft.
    // ========================================================================

    /// <summary>
    /// Apply a Detect Alignment/Undead spell to the caster (self-targeting buff).
    /// Creates the detection effect, performs initial scan, shows combat log.
    /// </summary>
    private ActiveSpellEffect ApplyAlignmentDetectionSpell(CharacterController caster, SpellData spell, SpellcastingComponent spellComp)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return null;

        // Remove any existing detection effect (only one at a time)
        if (caster.HasActiveAlignmentDetection)
        {
            var old = caster.RemoveAlignmentDetectionEffect();
            if (old != null)
            {
                CombatUI?.ShowCombatLog($"<color=#AAAAAA>Previous {old.SpellDisplayName} ends as {caster.Stats.CharacterName} begins a new detection.</color>");
            }
        }

        // Ensure StatusEffectManager exists
        StatusEffectManager statusMgr = caster.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = caster.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(caster.Stats);

        // Add spell effect for duration tracking
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        ActiveSpellEffect effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);

        // Create detection data
        AlignmentDetectionEffectData detectionData = AlignmentDetectionEffectData.CreateFromSpell(spell.SpellId, caster);
        detectionData.DurationRemainingRounds = effect != null ? effect.RemainingRounds : casterLevel * 100; // 10 min/level = lots of rounds
        detectionData.ConcentrationRounds = 0;

        // Apply to caster
        caster.ApplyAlignmentDetectionEffect(detectionData);

        // Track in spellcasting component
        if (spellComp != null)
            spellComp.ActiveBuffs[spell.SpellId] = detectionData.DurationRemainingRounds;

        // Perform initial scan (round 1)
        List<CharacterController> allChars = GetAllLivingCharacters();
        caster.UpdateAlignmentDetectionScan(allChars);

        // Show combat log with initial results
        string typeName = detectionData.Type == DetectionType.Undead ? "undead" : detectionData.Type.ToString().ToLower();
        string emoji = GetDetectionEmoji(detectionData.Type);
        CombatUI?.ShowCombatLog($"<color=#88CCFF>{emoji} {caster.Stats.CharacterName} begins concentrating on {detectionData.SpellDisplayName}.</color>");

        string summary = detectionData.GetDetectionSummary();
        CombatUI?.ShowCombatLog($"<color=#BBDDFF>  → {summary}</color>");

        // Apply visual highlights to detected creatures
        RefreshDetectionHighlights(caster);

        UpdateAllStatsUI();
        return effect;
    }

    /// <summary>
    /// Called each round to update detection scans for all characters with active detection.
    /// Should be called from the round-tick logic.
    /// </summary>
    public void UpdateAlignmentDetectionForRound(CharacterController character)
    {
        if (character == null || !character.HasActiveAlignmentDetection)
            return;

        var detection = character.ActiveAlignmentDetectionEffect;

        // Tick duration
        detection.DurationRemainingRounds = Mathf.Max(0, detection.DurationRemainingRounds - 1);
        if (detection.DurationRemainingRounds <= 0)
        {
            string emoji = GetDetectionEmoji(detection.Type);
            CombatUI?.ShowCombatLog($"<color=#FFAA44>{emoji} {character.Stats.CharacterName}'s {detection.SpellDisplayName} expires.</color>");
            character.RemoveAlignmentDetectionEffect();
            ClearDetectionHighlights(character);
            return;
        }

        // Re-scan
        List<CharacterController> allChars = GetAllLivingCharacters();
        character.UpdateAlignmentDetectionScan(allChars);

        // Log updated info
        string summary = detection.GetDetectionSummary();
        string emoji2 = GetDetectionEmoji(detection.Type);
        CombatUI?.ShowCombatLog($"<color=#BBDDFF>{emoji2} {detection.SpellDisplayName} (Round {detection.ConcentrationRounds}): {summary}</color>");

        // Update visual highlights
        RefreshDetectionHighlights(character);
    }

    /// <summary>
    /// Get all living characters on the battlefield.
    /// </summary>
    private List<CharacterController> GetAllLivingCharacters()
    {
        var result = new List<CharacterController>();
        if (_playerCharacters != null)
        {
            foreach (var pc in _playerCharacters)
            {
                if (pc != null && pc.Stats != null && pc.Stats.CurrentHP > 0)
                    result.Add(pc);
            }
        }
        if (_activeNPCs != null)
        {
            foreach (var npc in _activeNPCs)
            {
                if (npc != null && npc.Stats != null && npc.Stats.CurrentHP > 0)
                    result.Add(npc);
            }
        }
        return result;
    }

    /// <summary>
    /// Apply visual tint/highlight to detected creatures on the battlefield.
    /// Creates a colored overlay on detected characters' sprites.
    /// </summary>
    private void RefreshDetectionHighlights(CharacterController detector)
    {
        if (detector == null || !detector.HasActiveAlignmentDetection)
            return;

        var detection = detector.ActiveAlignmentDetectionEffect;

        // Only show locations at round 3+
        if (detection.ConcentrationRounds < 3)
            return;

        foreach (var detected in detection.DetectedCreatures)
        {
            if (detected.Creature == null || detected.Creature.OccupiedCell == null)
                continue;

            // Apply a subtle colored highlight on the creature's cell
            var cell = detected.Creature.OccupiedCell;
            cell.SetHighlight(detection.HighlightColor);
        }
    }

    /// <summary>
    /// Clear visual highlights from all cells when detection ends.
    /// </summary>
    private void ClearDetectionHighlights(CharacterController detector)
    {
        // Clear all cell highlights (they'll be restored by normal game logic)
        if (_grid != null)
        {
            foreach (var cell in _grid.AllCells())
            {
                cell.ClearHighlight();
            }
        }
    }

    /// <summary>Get the emoji for a detection type for combat log display.</summary>
    private static string GetDetectionEmoji(DetectionType type)
    {
        switch (type)
        {
            case DetectionType.Chaos: return "🌀";
            case DetectionType.Evil:  return "👿";
            case DetectionType.Good:  return "😇";
            case DetectionType.Law:   return "⚖";
            case DetectionType.Undead: return "💀";
            default: return "🔮";
        }
    }

    // ========== MOVEMENT ==========
}
