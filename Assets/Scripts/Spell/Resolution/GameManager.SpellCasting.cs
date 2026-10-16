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

        // What the cast spends is its own source (batch 11 step 4): the cast on top of the cast stack, which is this
        // cast (every cast core run, PC or NPC, and every other pipeline opens its own context before it reaches here),
        // so a scroll or wand cast of another caster, or a cancelled one, can never be spent here.
        CastContext cast = CurrentCast;
        CastSourceKind sourceKind = cast != null ? cast.Request.Source.Kind : CastSourceKind.PreparedSlot;

        // Scroll casts do not consume spell slots — the scroll IS the resource
        if (sourceKind == CastSourceKind.Scroll)
        {
            Debug.Log($"[ScrollCast] Skipping spell slot consumption — casting from scroll.");
            ConsumeScrollAfterCast(cast);
            MarkCastSpend(spellComp, spell, "scroll");
            return true;
        }

        // Wand casts do not consume spell slots — the wand charge IS the resource
        if (sourceKind == CastSourceKind.Wand)
        {
            Debug.Log($"[WandCast] Skipping spell slot consumption — casting from wand.");
            ConsumeWandChargeAfterCast(cast);
            MarkCastSpend(spellComp, spell, "wand");
            return true;
        }

        if (isSpontaneous)
        {
            // A cleric's conversion gives up a prepared spell of the cast's level, the adjusted level with metamagic (PHB
            // p.32, p.88; batch 11 step 21, SPL-148): the named sacrifice of that level, else any convertible one.
            bool spentSpontaneous = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                ? spellComp.SpontaneousCastFromSpecificSpell(spontaneousSacrificedSpellId, hasMetamagicApplied ? slotLevelToConsume : spontaneousLevel)
                : hasMetamagicApplied
                    ? spellComp.SpontaneousCastFromSlot(spell.SpellLevel, metamagic)
                    : spellComp.SpontaneousCastFromSlot(spontaneousLevel);
            MarkCastSpend(spellComp, spell, spentSpontaneous ? "spontaneous-slot" : "not-spent");
            return spentSpontaneous;
        }

        bool withMetamagic = hasMetamagicApplied && slotLevelToConsume > 0;
        bool spent = withMetamagic
            ? spellComp.CastWizardSpellWithMetamagic(spell, metamagic)
            : spellComp.CastSpellFromSlot(spell);
        MarkCastSpend(spellComp, spell, !spent ? "not-spent" : withMetamagic ? "metamagic-slot" : "slot");
        return spent;
    }

    /// <summary>The spend stage of <see cref="CastStageTrace"/> for the caster owning <paramref name="spellComp"/>; inert outside the harness.</summary>
    private static void MarkCastSpend(SpellcastingComponent spellComp, SpellData spell, string detail)
    {
        if (ScenarioHooks.CastStage != null && spellComp != null)
            CastStageTrace.Mark(spellComp.GetComponent<CharacterController>(), spell, CastStageTrace.Spend, detail);
    }

    /// <summary>
    /// Arcane spell failure for one casting, the same for every caster and path (batch 11 step 13): rolled only when
    /// CastComponentRules.ArcaneFailureApplies (an arcane caster, a spell with a somatic component left after Still
    /// Spell, PHB p.122 and p.101; a scroll too, DMG p.238; not a wand, a held-charge delivery or the F12 panel's
    /// ArcaneFailure bypass; SPL-096, SPL-116), with the cast's own source (batch 11 step 4), against
    /// CharacterStats.ArcaneSpellFailure.
    /// </summary>
    private bool TryRollArcaneSpellFailure(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource castSource,
        out int roll, out int asfChance)
    {
        roll = 0;
        asfChance = 0;

        if (!CastComponentRules.ArcaneFailureApplies(caster, spell, metamagic, castSource))
            return false;

        asfChance = Mathf.Clamp(caster.Stats.ArcaneSpellFailure, 0, 100);
        if (asfChance <= 0)
            return false;

        roll = DiceService.Percentile("Arcane spell failure");

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"ASF Check ({caster.Stats.CharacterName}, {spell.Name}): roll {roll}% vs {asfChance}%"));
        CastStageTrace.Mark(caster, spell, CastStageTrace.Failure, roll <= asfChance ? "asf-failed" : "asf-passed");
        return roll <= asfChance;
    }

    private void LogArcaneSpellFailure(CharacterController caster, SpellData spell, int roll, int asfChance)
    {
        if (caster == null || caster.Stats == null || spell == null) return;

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "═══════════════════════════════"));
        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"{caster.Stats.CharacterName} attempts to cast {spell.Name}"));
        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"Arcane Spell Failure: {roll}% ≤ {asfChance}%"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "️ SPELL FAILS! Spell slot consumed, no effect."));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "═══════════════════════════════"));
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

        // The summoned creature fights for its caster's side and follows its caster's control (batch 11 step 27): the
        // player commands a player-controlled caster's creature, the AI runs an AI-run caster's (an enemy's, or an AI-run
        // party member's in a scenario), whose turn would otherwise wait for a player who is not there.
        bool alliedToPlayer = caster.Team == CharacterTeam.Player;
        summon.ConfigureTeamControl(alliedToPlayer ? CharacterTeam.Player : CharacterTeam.Enemy, controllable: alliedToPlayer && caster.IsControllable);

        if (summon.Stats != null)
        {
            // The summon list's alignment (Summon Monster tables, PHB p.287) wins; otherwise the definition's alignment, which the
            // celestial or fiendish template has made good or evil, stays (CRE-002). The fallbacks below cover only a
            // definition without an alignment.
            if (option.SummonedCreatureAlignment != Alignment.None)
            {
                summon.Stats.CharacterAlignment = option.SummonedCreatureAlignment;
            }
            else if (summon.Stats.CharacterAlignment != Alignment.None)
            {
                // Keep the spawned alignment.
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

            // Augment Summoning (PHB p.89): summoned creatures gain +4 STR, +4 CON
            if (caster != null && caster.Stats != null && FeatManager.HasAugmentSummoning(caster.Stats))
            {
                FeatManager.ApplyAugmentSummoningBonuses(summon.Stats);
                Debug.Log($"[Summon] Augment Summoning applied to {summon.Stats.CharacterName}: +4 STR, +4 CON");
            }
        }

        NPCs.Add(summon);
        _npcAIBehaviors.Add(summonDef.AIBehavior);
        summon.MarkAsSummoned(); // after InitializeNPCFromDefinition, whose slot reset clears it

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
                && ConcentrationService.IsConcentratingOnSpell(summon.Caster, summon.SourceSpellId))
            {
                ConcentrationManager casterConc = summon.Caster.Concentration;
                if (casterConc != null)
                {
                    string breakLog = casterConc.ForceBreakConcentration("incapacitated");
                    if (!string.IsNullOrEmpty(breakLog))
                        CombatUI?.ShowCombatLog(CombatLogHelper.InterruptedRaw(breakLog));
                }
            }

            bool holdByConcentration = summon.IsConcentrationSummon
                && !summon.HasEnteredPostConcentrationDuration
                && ConcentrationService.IsConcentratingOnSpell(summon.Caster, summon.SourceSpellId);

            if (!holdByConcentration)
            {
                if (summon.IsConcentrationSummon && !summon.HasEnteredPostConcentrationDuration)
                {
                    summon.HasEnteredPostConcentrationDuration = true;
                    summon.RemainingRounds = Mathf.Max(1, summon.TotalDurationRounds);
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("", $"{summon.Controller.Stats.CharacterName}: concentration ended, {summon.RemainingRounds} rounds until dismissal."));
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
                    CombatUI?.ShowCombatLog(CombatLogHelper.SummonRaw($"{summon.Controller.Stats.CharacterName}: 2 rounds remaining."));
                else if (summon.RemainingRounds == 1)
                    CombatUI?.ShowCombatLog(CombatLogHelper.Buff("", $"{summon.Controller.Stats.CharacterName}: 1 round remaining!"));
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

    /// <summary>
    /// The number of creatures a summon monster or summon nature's ally brings from a lower list (PHB p.285): 1d3 from the
    /// next lower list, 1d4+1 from a list two or more lower. A variable effect the cast's Empower and Maximize change
    /// (PHB p.93: "affects half again as many targets"; SpellDiceRules.RollFor, batch 11 step 19, SPL-038).
    /// </summary>
    private int RollSummonCreatureCount(int spellLevel, int selectedListLevel, out string rollLog, SpellData castSpell = null)
    {
        SummonCreatureCountInfo info = SummonMonsterLists.GetCreatureCountInfo(spellLevel, selectedListLevel);

        if (info == null || !info.RequiresRoll)
        {
            rollLog = "Creature count: 1 (same-level list).";
            return 1;
        }

        if (info.LevelDifference == 1)
        {
            int d3Roll = SpellDiceRules.RollFor(castSpell, SpellDice.Of(1, 3), "Summon creature count 1d3");
            rollLog = $"Rolling for creature count: 1d3 = {d3Roll}";
            return d3Roll;
        }

        MetamagicData countMetamagic = SpellDiceRules.MetamagicOf(castSpell);
        if (countMetamagic != null && (countMetamagic.Has(MetamagicFeatId.EmpowerSpell) || countMetamagic.Has(MetamagicFeatId.MaximizeSpell)))
        {
            int changed = SpellDiceRules.Roll(SpellDice.Of(1, 4, 1), countMetamagic, "Summon creature count 1d4");
            rollLog = $"Rolling for creature count: 1d4+1 with {countMetamagic.GetDisplayName()} = {changed}";
            return changed;
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

            // In the caster's line of effect (PHB p.176), as the cast core's summon test (SummonPlacementRefusal).
            if (caster != null && !AoESystem.HasLineOfEffectToSpace(caster.GridPosition, caster.GetCurrentSpaceSquares(), cell.Coords, summonSizeSquares))
                continue;

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

        // 1 round per caster level by default (CasterLevelRules, SPL-017), not per character level.
        int defaultDuration = CasterLevelRules.ForCaster(caster, string.IsNullOrEmpty(sourceSpellId) ? null : SpellDatabase.GetSpell(sourceSpellId));
        int durationRounds = Mathf.Max(1, durationRoundsOverride ?? defaultDuration);

        var activeSummon = new ActiveSummonInstance
        {
            Controller = summon,
            Caster = caster,
            RemainingRounds = durationRounds,
            TotalDurationRounds = durationRounds,
            SourceSpellId = sourceSpellId,
            IsAlliedToPCs = summon.Team == CharacterTeam.Player,
            CurrentCommand = SummonCommand.AttackNearest(),
            IsConcentrationSummon = concentrationSummon,
            HasEnteredPostConcentrationDuration = startsInPostConcentrationMode
        };
        _activeSummons.Add(activeSummon);

        var visual = summon.GetComponent<SummonedCreatureVisual>();
        if (visual != null)
            visual.SetDuration(durationRounds, durationRounds);
    }

    /// <summary>
    /// The open Ui cast a summon or Grease builder hands to the cast core, or null (with a warning and a return to the
    /// action menu) when there is none, it is an AI cast, or it waits for its AoO prompt's answer (a re-entry would wipe
    /// the waiting run's state).
    /// </summary>
    private CastContext OpenUiCastForBuilder(string from)
    {
        CastContext ctx = CurrentCast;
        if (RejectCastReentry(ctx, from))
            return null;
        if (ctx == null || ctx.Request.Spell == null || ctx.Request.Decider.IsSynchronous)
        {
            Debug.LogWarning($"[SpellCasting] {from} without an open Ui cast; back to the action menu.");
            ShowActionChoices();
            return null;
        }
        return ctx;
    }

    /// <summary>
    /// The Summon Swarm placement click (PHB p.289): the request builder of a Summon Swarm cast (batch 11 step 10). The
    /// swarm type is the open cast's choice (CastChoices.SummonSwarmNpcId); the cast places it on
    /// <paramref name="targetCell"/> through the cast core (SpellCastRequest.ForUiSummon, the Summon resolve,
    /// <see cref="ResolveSummonSwarmCast"/>).
    /// </summary>
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"Cannot summon swarm: missing creature definition '{swarmNpcId}'."));
            ShowActionChoices();
            return;
        }

        CastContext ctx = OpenUiCastForBuilder(nameof(PerformSummonSwarmCast));
        if (ctx == null)
            return;

        CastStageTrace.Mark(caster, _pendingSpell, CastStageTrace.Begin, nameof(PerformSummonSwarmCast));
        CurrentSubPhase = PlayerSubPhase.Animating;
        ctx.Request.Choices.SummonSwarmNpcId = swarmNpcId;
        SpellCastRequest.ForUiSummon(ctx.Request, targetCell.Coords);
        ExecuteCast(ctx, null);
    }

    /// <summary>
    /// Resolve (Summon Swarm, PHB p.289): the chosen swarm appears on the destination, uncontrolled (it attacks the nearest
    /// creature, friend or foe), joins the initiative after the caster and lasts while the caster concentrates, then
    /// 2 rounds. The cast ends at its Resolve stage.
    /// </summary>
    private void ResolveSummonSwarmCast(CastContext ctx)
    {
        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;
        string swarmNpcId = ctx.Request.Choices.SummonSwarmNpcId;
        SquareCell targetCell = Grid.GetCell(ctx.Request.Aim.Destination.Value);
        NPCDefinition baseDef = NPCDatabase.Get(swarmNpcId);
        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(PerformSummonSwarmCast));

        SummonMonsterOption option = new SummonMonsterOption
        {
            DisplayName = baseDef.Name,
            NpcDefinitionId = swarmNpcId,
            TemplateId = null
        };

        CharacterController summonCC = SpawnSummonedCreature(caster, targetCell.Coords, option);
        if (summonCC == null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Summon Swarm failed: creature could not be spawned."));
            FinishFailedSummon(ctx, "not-spawned");
            return;
        }

        summonCC.aiProfile = ScriptableObject.CreateInstance<IndiscriminateSwarmAI>();
        summonCC.EnemyUseCoupDeGraceOverride = false;

        // Swarm is completely uncontrollable - it attacks nearest creature regardless of team
        summonCC.SetControllable(false);

        InsertIntoInitiative(summonCC, caster);

        int persistedRounds = Mathf.Max(1, ConcentrationRules.TailRounds(spell, CasterLevelRules.ForCaster(caster, spell))); // PHB p.289: concentration + 2 rounds
        RegisterActiveSummon(
            summonCC,
            caster,
            spell.SpellId,
            durationRoundsOverride: persistedRounds,
            concentrationSummon: true,
            startsInPostConcentrationMode: false);

        BeginSummonSwarmConcentration(caster, spell, summonCC);

        // The cast's caster level (CasterLevelRules, SPL-017), not the character level.
        int swarmCasterLevel = CasterLevelRules.ForCaster(caster, spell);
        int rangeFeet = CalculateSummonSwarmRangeFeet(swarmCasterLevel);
        CombatUI?.ShowCombatLog(CombatLogHelper.SummonRaw($"{caster.Stats.CharacterName} casts {spell.Name}!"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  Range: {rangeFeet} ft (level {swarmCasterLevel} caster)"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  Target location: ({targetCell.Coords.x}, {targetCell.Coords.y})"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  Swarm type: {baseDef.Name}"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("⚠", $"WARNING: The swarm is uncontrolled and will attack the nearest creature (NO friend/foe distinction - caster and allies ARE valid targets)!"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("", $"{caster.Stats.CharacterName} is concentrating on {spell.Name}."));

        FinishEffectCast(ctx, "summoned");
    }

    /// <summary>
    /// The Summon Monster (or Summon Nature's Ally) placement click (PHB p.285-288): the request builder of a summon cast
    /// (batch 11 step 10). The creature, list level and count rule are the open cast's choices, written by the creature
    /// prompt; a square without room for the creature asks for another square. The cast places it on
    /// <paramref name="targetCell"/> through the cast core (SpellCastRequest.ForUiSummon, the Summon resolve,
    /// <see cref="ResolveSummonMonsterCast"/>).
    /// </summary>
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"Cannot summon {option.DisplayName}: missing creature definition."));
            ShowActionChoices();
            return;
        }

        int summonSizeSquares = baseDef.SizeCategory.GetSpaceWidthSquares();
        if (!Grid.CanPlaceCreature(targetCell.Coords, summonSizeSquares))
        {
            CombatUI.ShowCombatLog(CombatLogHelper.Failure("", "Cannot summon there: not enough open space for that creature size."));
            ShowSummonPlacementTargets(caster, _pendingSpell);
            return;
        }

        CastContext ctx = OpenUiCastForBuilder(nameof(PerformSummonMonsterCast));
        if (ctx == null)
            return;

        CastStageTrace.Mark(caster, _pendingSpell, CastStageTrace.Begin, nameof(PerformSummonMonsterCast));
        CurrentSubPhase = PlayerSubPhase.Animating;
        ctx.Request.Choices.SummonSelection = option;
        SpellCastRequest.ForUiSummon(ctx.Request, targetCell.Coords);
        ExecuteCast(ctx, null);
    }

    /// <summary>
    /// Resolve (Summon Monster, Summon Nature's Ally; PHB p.285-288): rolls the creature count of the chosen list level,
    /// places the first creature on the destination and the others on the best free squares near it (stacked on the
    /// destination when none is free), inserts each into the initiative after the caster (RAW it acts at once, on the
    /// caster's turn when the 1-round casting completes, PHB p.285, p.288; SPL-164) and registers it for the spell's
    /// duration (1 round per caster level, SPL-002). The cast ends at its Resolve stage.
    /// </summary>
    private void ResolveSummonMonsterCast(CastContext ctx)
    {
        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;
        CastChoices choices = ctx.Request.Choices;
        SummonMonsterOption option = choices.SummonSelection;
        SquareCell targetCell = Grid.GetCell(ctx.Request.Aim.Destination.Value);
        NPCDefinition baseDef = NPCDatabase.Get(option.NpcDefinitionId);
        bool isSwarmSummonOption = SummonMonsterLists.IsSwarmOption(option);
        int summonSizeSquares = baseDef.SizeCategory.GetSpaceWidthSquares();
        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(PerformSummonMonsterCast));

        int spellLevel = SummonMonsterLists.GetSummonMonsterSpellLevel(spell.SpellId);
        int selectedListLevel = choices.SummonListLevel > 0 ? choices.SummonListLevel : spellLevel;
        SummonCreatureCountInfo countInfo = choices.SummonCountInfo ?? SummonMonsterLists.GetCreatureCountInfo(spellLevel, selectedListLevel);
        int creatureCount = RollSummonCreatureCount(spellLevel, selectedListLevel, out string rollLog, spell);
        creatureCount = Mathf.Max(1, creatureCount);

        string selectedListRoman = SummonMonsterLists.ToRomanLevel(Mathf.Max(1, selectedListLevel));
        CombatUI?.ShowCombatLog(CombatLogHelper.SummonRaw($"{caster.Stats.CharacterName} casts {spell.Name} (using Level {selectedListRoman} list)."));
        if (countInfo != null && countInfo.RequiresRoll)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{rollLog}"));

        string summonLabel = option.BuildUiLabel();
        CombatUI?.ShowCombatLog(CombatLogHelper.SummonRaw($"Summoning {creatureCount} {summonLabel}{(creatureCount == 1 ? string.Empty : "s")}..."));
        if (isSwarmSummonOption)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "Swarm summons are AI-controlled allies and cannot be directly commanded."));

        List<CharacterController> spawnedCreatures = new List<CharacterController>(creatureCount);
        Vector2Int primaryCell = targetCell.Coords;
        // PHB p.285: summon monster lasts 1 round/level, from the cast's caster level (Extend doubles it) (SPL-002).
        int? summonDurationRounds = SpellDurationRules.IsTimed(spell)
            ? SpellCastingHelper.CalculateDuration(spell, SpellDurationRules.CasterLevelFor(caster, spell))
            : (int?)null;
        int summonRangeSquares = Mathf.Max(1, CasterLevelRules.RangeSquares(caster, spell));

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
                CombatUI?.ShowCombatLog(CombatLogHelper.Buff("", "Not enough open slots; stacking extra summons on the primary tile."));

            InsertIntoInitiative(summonCC, caster);
            RegisterActiveSummon(summonCC, caster, spell.SpellId, durationRoundsOverride: summonDurationRounds);
            spawnedCreatures.Add(summonCC);

            string summonIndexLabel = creatureCount > 1 ? $" {i + 1}" : string.Empty;
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{summonLabel}{summonIndexLabel} appears!"));
        }

        if (spawnedCreatures.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Summoning failed: no valid creatures could be spawned."));
            FinishFailedSummon(ctx, "not-spawned");
            return;
        }

        FinishEffectCast(ctx, "summoned");
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
            CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("🔮", $"{caster.Stats.CharacterName}'s spell cannot pass through the Resilient Sphere!"));
            EndPendingCast();
            ShowActionChoices();
            return;
        }

        if (!CanBeginSpellcastWhileGrappledOrPinned(caster, _pendingSpell))
        {
            EndPendingCast();
            ShowActionChoices();
            return;
        }

        // Spell routing golden suite (Tests.Magic.SpellRoutingGoldenTests.PcRoutingBranch) mirrors the decision order from
        // here down (Grease, Disguise Self, summon, swarm, area, Wish, self or single): keep them in step.
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

        // Summon Monster spells: choose creature first, then select destination tile.
        if (IsSummonMonsterSpell(_pendingSpell))
        {
            ShowSummonCreatureSelectionMenu(caster, _pendingSpell);
            return;
        }

        // Summon Swarm: choose swarm type first, then place in range (occupied squares allowed). An Area spell that places a
        // swarm rather than affecting an area, so it is routed before the area spells.
        if (IsSummonSwarmSpell(_pendingSpell))
        {
            ShowSummonSwarmSelectionMenu(caster, _pendingSpell);
            return;
        }

        // ===== AREA SPELLS: Enter AoE targeting mode =====
        // By geometry (batch 11 step 23, SPL-042, SPL-046): an Area target type or an area shape (AoESystem.RoutesToArea),
        // so an Area spell is never resolved against one clicked creature.
        if (AoESystem.RoutesToArea(_pendingSpell))
        {
            EnterAoETargetingMode(caster, _pendingSpell);
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
            Debug.Log($"[SpellCasting] Non-self spell → SelectingAttackTarget mode, calling ShowSpellTargets  range={CasterLevelRules.RangeSquares(caster, _pendingSpell)}");
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"No races available for size {casterSize}. Disguise Self cancelled."));
            EndPendingCast();
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
                UiCastChoicesForAnswer.DisguiseRace = selectedRace;
                PerformSpellCast(caster, caster);
            },
            onCancel: () =>
            {
                EndPendingCast();
                ShowActionChoices();
            });
    }

    /// <summary>
    /// Goes on with the open cast once a choice dialog of the UI decider (the Resist Energy or Protection from Energy
    /// type) is answered: a hold of the charge (Discharge Later, batch 11 step 15) runs the cast core again from Legality,
    /// which now has the choice; any other cast goes back to its targeting, as before.
    /// </summary>
    private void ResumeCastAfterChoiceDialog(CharacterController caster)
    {
        CastContext open = CurrentCast;
        if (open != null && open.Outcome == null && open.Request.ResolveKind == CastResolveKind.HoldCharge)
        {
            CurrentSubPhase = PlayerSubPhase.Animating;
            ExecuteCast(open, null);
            return;
        }

        BeginPendingSpellTargeting(caster);
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
                    EndPendingCast();
                    CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Resist Energy cancelled: no energy type selected."));
                    ShowActionChoices();
                    return;
                }

                UiCastChoicesForAnswer.Energy = (ResistEnergyType)selectedIndex;
                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("✨", $"Resist Energy prepared for {options[selectedIndex].ToLowerInvariant()}."));
                ResumeCastAfterChoiceDialog(caster);
            },
            onCancel: () =>
            {
                EndPendingCast();
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", "Resist Energy cancelled (energy type not selected)."));
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
                    EndPendingCast();
                    CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Protection from Energy cancelled: no energy type selected."));
                    ShowActionChoices();
                    return;
                }

                UiCastChoicesForAnswer.Energy = (ResistEnergyType)selectedIndex;
                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("✨", $"Protection from Energy prepared for {options[selectedIndex].ToLowerInvariant()}."));
                ResumeCastAfterChoiceDialog(caster);
            },
            onCancel: () =>
            {
                EndPendingCast();
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", "Protection from Energy cancelled (energy type not selected)."));
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
                    EndPendingCast();
                    CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Fire Shield cancelled: no shield type selected."));
                    ShowActionChoices();
                    return;
                }

                // Index 0 = Chill Shield (not warm), Index 1 = Warm Shield
                UiCastChoicesForAnswer.FireShieldWarm = (selectedIndex == 1);
                string chosen = selectedIndex == 1 ? "Warm Shield" : "Chill Shield";
                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("✨", $"Fire Shield prepared: {chosen}."));
                BeginPendingSpellTargeting(caster);
            },
            onCancel: () =>
            {
                EndPendingCast();
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", "Fire Shield cancelled (shield type not selected)."));
                ShowActionChoices();
            },
            titleOverride: "Fire Shield — Choose Shield Type",
            bodyOverride: "Choose a shield type:\n• Chill Shield: deals cold retribution to melee attackers; reduces fire damage by half.\n• Warm Shield: deals fire retribution to melee attackers; reduces cold damage by half.",
            optionButtonColorOverride: new Color(0.8f, 0.3f, 0.1f, 1f));
    }

    /// <summary>The Cast Now / Discharge Later prompt only for a spell whose charge can be held (SpellData.CanHoldCharge).</summary>
    private bool ShouldShowTouchSpellPrompt(SpellData spell)
        => spell != null && spell.CanHoldCharge();

    /// <summary>Legacy callback for backward compat (no metamagic).</summary>
    private void OnSpellSelected(SpellData spell)
    {
        OnSpellSelectedWithMetamagic(spell, null);
    }

    /// <summary>Called when spell selection is cancelled.</summary>
    private void OnSpellSelectionCancelled()
    {
        ResetPendingWallOfFireMode();
        ResetPendingWallOfIceMode();
        ShowActionChoices();
    }

    private bool IsFriendlyTarget(CharacterController caster, CharacterController target)
    {
        return TeamUtility.IsAlly(caster, target);
    }

        /// <summary>Delegates to <see cref="TeamUtility.IsHumanoid"/>.</summary>
    private bool IsHumanoid(CharacterController target)
        => TeamUtility.IsHumanoid(target);

    /// <summary>Delegates to <see cref="TeamUtility.GetHitDice"/>.</summary>
    private int GetTargetHitDice(CharacterController target)
        => TeamUtility.GetHitDice(target);

    /// <summary>Delegates to <see cref="SpellUtilities.IsImmuneToMindAffecting"/>.</summary>
    private bool IsImmuneToMindAffecting(CharacterController target)
    {
        return SpellUtilities.IsImmuneToMindAffecting(target);
    }

    /// <summary>Delegates to <see cref="SpellUtilities.IsFearSpell"/> (Cause Fear only).</summary>
    private static bool IsCauseFearSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.CAUSE_FEAR, System.StringComparison.Ordinal);
    }

    /// <summary>Delegates to <see cref="SpellUtilities.IsLivingCreatureForFear"/>.</summary>
    private bool IsLivingCreatureForFearSpell(CharacterController target)
    {
        return SpellUtilities.IsLivingCreatureForFear(target);
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
            return SpellTargetingService.IsHumanoid(target);
        }

        if (spell.SpellId == SpellNames.DAZE)
        {
            // D&D 3.5e Daze: one humanoid creature of 4 HD or less (PHB p.217).
            if (!_isProtectionFromEvilTestEncounter && !SpellTargetingService.IsValidHumanoidMindAffectingTarget(target, 4))
                return false;
            if (_isProtectionFromEvilTestEncounter && !SpellTargetingService.IsValidHumanoidMindAffectingTarget(target, 0))
                return false;
            return true;
        }

        if (spell.SpellId == SpellNames.CHARM_PERSON)
        {
            // D&D 3.5e Charm Person: one humanoid creature (PHB p.209).
            return SpellTargetingService.IsValidHumanoidMindAffectingTarget(target, 4);
        }

        // Charm Monster — any living creature, no HD limit, mind-affecting (PHB p.209)
        if (spell.SpellId == SpellNames.CHARM_MONSTER)
        {
            return SpellTargetingService.IsValidMindAffectingTarget(target);
        }

        // Dominate Person — one humanoid, no HD limit, mind-affecting (PHB p.224)
        if (spell.SpellId == SpellNames.DOMINATE_PERSON)
        {
            return SpellTargetingService.IsValidHumanoidMindAffectingTarget(target, 0);
        }

        // Hold Monster — any living creature, mind-affecting (PHB p.241)
        if (spell.SpellId == SpellNames.HOLD_MONSTER)
        {
            return SpellTargetingService.IsValidMindAffectingTarget(target);
        }

        if (spell.SpellId == SpellNames.TOUCH_OF_IDIOCY)
        {
            // TARGETING OVERRIDE: Removed enemy-only restriction. Still requires living creature.
            return SpellTargetingService.IsValidLivingTarget(target);
        }

        // Direct targeting requires line of sight for enemies.
        // See Invisible allows direct targeting of invisible enemies, but does not bypass
        // true visibility blockers or total concealment sources.
        // TARGETING OVERRIDE: Line-of-sight check still applies for enemies only.
        if (TeamUtility.IsEnemy(caster, target))
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

    private bool HasActiveShieldSpell(CharacterController target)
    {
        if (target == null)
            return false;

        StatusEffectManager statusMgr = target.StatusEffectManager;
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

        SpellcastingComponent spellComp = target.Spellcasting;
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

        // The cast's range at its caster level, the rule the NPC range test uses too (CasterLevelRules, SPL-017; batch 11
        // step 14): the open cast's own (a scroll's or wand's caster level), the casting class's, not the character level.
        int range = CasterLevelRules.RangeSquares(caster, spell);
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
    /// Discharge Later on the touch prompt (PHB p.141, p.176): the request builder of a hold (batch 11 step 10). The open
    /// cast of the melee touch spell becomes a hold (SpellCastRequest.ForUiHoldCharge) and runs through the cast core: the
    /// action, the slot, the distraction and failure checks and the provocation as for any cast, then the HoldCharge
    /// resolve stores the charge (SpellcastingComponent.SetHeldTouchCharge); its delivery is a cast of its own
    /// (OnDischargeHeldTouchButtonPressed).
    /// </summary>
    private void BeginHoldTouchCharge(CharacterController caster)
    {
        if (caster == null || _pendingSpell == null) { ShowActionChoices(); return; }

        CastContext ctx = OpenUiCastForBuilder(nameof(BeginHoldTouchCharge));
        if (ctx == null)
            return;

        CastStageTrace.Mark(caster, _pendingSpell, CastStageTrace.Begin, nameof(BeginHoldTouchCharge));
        CurrentSubPhase = PlayerSubPhase.Animating;
        SpellCastRequest.ForUiHoldCharge(ctx.Request);
        ExecuteCast(ctx, null);
    }
    /// <summary>
    /// Casts the open single-target cast from <paramref name="caster"/> at <paramref name="target"/> (the spell menu's
    /// self cast, the target click, a choice dialog's re-entry, the touch prompt's Cast Now, the Discharge button's
    /// delivery, a scroll, wand or F12 cast). Since batch 11 step 7 the body is the staged cast core
    /// (GameManager.CastCore.cs, ExecuteCast) with the UI decider: this method only does the UI's part before the cast
    /// and hands the open cast's request, with its caster and target, to the core. The UI follow-up comes from
    /// UiCastDecider.OnCastFinished (GameManager.CastDeciders.cs).
    /// </summary>
    private void PerformSpellCast(CharacterController caster, CharacterController target)
    {
        Debug.Log($"[SpellCasting] PerformSpellCast  caster={caster?.Stats?.CharacterName}  target={target?.Stats?.CharacterName}  spell={_pendingSpell?.Name}  testPanel={_testPanelCastActive}  scrollCast={_pendingScrollCastActive}");

        // A cast whose run waits for the AoO prompt's answer is not re-entered: its caster, target and run state stay.
        if (RejectCastReentry(CurrentCast, nameof(PerformSpellCast)))
            return;

        CastStageTrace.Mark(caster, _pendingSpell, CastStageTrace.Begin, nameof(PerformSpellCast));

        // Test-panel cleanup: none. The F12 panel's bypass flags are the cast's own (its CastSource) and end with it:
        // they are no longer cleared here, before the cast resolved (SPL-036).

        CombatUI?.HideDisguiseSelfRaceSelector();
        CurrentSubPhase = PlayerSubPhase.Animating;
        // The spell menu's spontaneous-conversion state was copied into the cast's source and cleared when the request
        // was built (batch 11 step 4); clearing it again is the old belt and braces.
        CombatUI?.ClearSpontaneousCastState();

        CastContext ctx = CurrentCast;
        if (ctx == null || ctx.Request.Spell == null || ctx.Request.Decider.IsSynchronous)
        {
            Debug.LogWarning("[SpellCasting] PerformSpellCast without an open Ui cast; back to the action menu.");
            ShowActionChoices();
            return;
        }

        SpellCastRequest req = ctx.Request;
        if (req.Caster != caster)
        {
            if (req.Caster != null)
                Debug.LogWarning($"[SpellCasting] PerformSpellCast: the cast's caster {req.Caster.Stats?.CharacterName} is replaced by {caster?.Stats?.CharacterName}.");
            req.Caster = caster;
        }
        req.Aim.Target = target;

        ExecuteCast(ctx, null);
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
        // Every spell routed here has a shape (AoESystem.RoutesToArea; Tests.Magic.AreaRoutingTests checks the data): a
        // spell without one has no area to aim, so the cast is cancelled rather than resolved against a guess.
        if (spell.AoEShapeType == AoEShape.None)
        {
            Debug.LogError($"[AoE] {spell.Name} is routed to an area but has no area shape; the cast is cancelled.");
            CancelAoETargeting();
            return;
        }

        // The placement range at the cast's caster level (CasterLevelRules, SPL-017: a scroll's or wand's own).
        int placementRange = spell.AoERangeSquares > 0
            ? spell.AoERangeSquares
            : CasterLevelRules.RangeSquares(caster, spell);

        // ===== SELF-CENTERED BURST: Show preview with confirmation =====
        // Some burst spells (e.g., Bless) are centered on the caster. Others (e.g.,
        // Flaming Sphere, Sleep, Web) use AoERangeSquares=0 as "use range profile"
        // and must still prompt for target selection.
        if (spell.AoEShapeType == AoEShape.Burst && placementRange <= 0)
        {
            // The area is centred on the caster: its point of origin is the intersection at, or nearest, the centre of the
            // caster's space (decision D5 (3), delegated RAW reading 2026-10-14), one of the four corners of a Small or
            // Medium caster's square, which the cursor picks (UpdateSelfAoEPreview); the north-east one is shown first.
            GridIntersection origin = AoESystem.GetCreatureCentredOrigins(caster)[0];
            Debug.Log($"[AoE] Self-centered burst: {spell.Name} — showing preview from {origin}");

            ShowSelfAoEPreview(caster, spell, origin);

            Debug.Log($"[AoE] Self-centered {spell.Name}: {_pendingSelfAoECells.Count} cells, {_pendingSelfAoETargets.Count} targets — awaiting confirmation");

            // Store state for confirmation
            _isConfirmingSelfAoE = true;
            CurrentSubPhase = PlayerSubPhase.ConfirmingSelfAoE;
            CombatUI.SetActionButtonsVisible(false);

            // Show turn indicator with confirm/cancel instructions
            CombatUI.SetTurnIndicator($"Casting {spell.Name} — Move the mouse to pick the corner it is centred on, Left-click to confirm, Right-click to cancel");
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

        // For burst and cube spells with range > 0, show the valid placement range
        if (AoESystem.IsPlacedShape(spell.AoEShapeType))
        {
            int range = placementRange;
            if (range <= 0) range = 1;

            // The squares with a corner the area may originate at (IsAreaPlacementSquareInRange, the aim rule's range test).
            List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, range + caster.GetCurrentSpaceSquares() + 1);
            foreach (var cell in rangeCells)
            {
                if (cell != null && IsAreaPlacementSquareInRange(caster, spell, cell.Coords, range))
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
                string sizeStr = spell.AoEShapeType == AoEShape.Cube
                    ? $"{spell.AoESizeSquares * 5}-ft cube"
                    : $"{spell.AoESizeSquares * 5}-ft radius burst";
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
    /// Shows the self-centred area of <paramref name="spell"/> from the point of origin <paramref name="origin"/> (one of the
    /// caster's creature-centred origins, AoESystem.GetCreatureCentredOrigins): its squares after line of effect from the
    /// origin, highlighted, and records them, their creatures and the origin for the confirmation (batch 11 step 24).
    /// </summary>
    private void ShowSelfAoEPreview(CharacterController caster, SpellData spell, GridIntersection origin)
    {
        HashSet<Vector2Int> aoeCells = AoESystem.GetCreatureCentredBurstCells(caster.GridPosition, caster.GetCurrentSpaceSquares(),
            origin, spell.AoESizeSquares, Grid);
        AoESystem.FilterCellsByLineOfEffect(aoeCells, origin);

        Grid.ClearAllHighlights();
        foreach (Vector2Int cellPos in aoeCells)
        {
            SquareCell cell = Grid.GetCell(cellPos);
            if (cell == null) continue;

            if (cell.IsOccupied && cell.Occupant != null && !cell.Occupant.Stats.IsDead)
            {
                bool isAlly = TeamUtility.IsAlly(caster, cell.Occupant);
                cell.SetHighlight(isAlly ? HighlightType.AoEAlly : HighlightType.AoETarget);
            }
            else
            {
                cell.SetHighlight(HighlightType.AoEPreview);
            }
        }

        _pendingSelfAoEOrigin = origin;
        _pendingSelfAoECells = aoeCells;
        // The creatures in it, relative to the caster's side (SPL-119).
        _pendingSelfAoETargets = AoESystem.GetTargetsInArea(aoeCells, caster, spell.AoEFilter, Grid);
    }

    /// <summary>
    /// Every frame of a self-centred area's confirmation: the creature-centred origin nearest the cursor becomes the area's
    /// point of origin (the caster's choice among the four corners of a Small or Medium caster's square; a Large caster
    /// has one), and the preview follows it (batch 11 step 24).
    /// </summary>
    private void UpdateSelfAoEPreview()
    {
        if (!_isConfirmingSelfAoE || _pendingSpell == null)
            return;
        CharacterController caster = ActivePC;
        if (caster == null || Grid == null)
            return;
        List<GridIntersection> origins = AoESystem.GetCreatureCentredOrigins(caster);
        if (origins.Count < 2)
            return;
        GridIntersection origin = AoESystem.NearestOf(origins, GetMouseGridPoint());
        if (origin == _pendingSelfAoEOrigin)
            return;
        ShowSelfAoEPreview(caster, _pendingSpell, origin);
    }

    /// <summary>
    /// True when the placement range highlight of a placed area (a burst or cube) covers <paramref name="square"/>: one of
    /// its four corners is a point of origin within <paramref name="range"/> of the caster's space, the test the aim rule
    /// makes (AoESystem.IsOriginWithinRange, TryBuildUiAreaAim); for a one-square effect the square itself is within range
    /// (batch 11 step 24 review).
    /// </summary>
    private static bool IsAreaPlacementSquareInRange(CharacterController caster, SpellData spell, Vector2Int square, int range)
    {
        if (caster == null)
            return false;
        if (AoESystem.IsSingleSquarePlacement(spell))
            return AoESystem.IsWithinCastingRange(caster.GridPosition, square, range);
        int size = caster.GetCurrentSpaceSquares();
        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                if (AoESystem.IsOriginWithinRange(caster.GridPosition, size, new GridIntersection(square.x + dx, square.y + dy), range))
                    return true;
            }
        }
        return false;
    }

    /// <summary>The cursor's position in grid units (a square's centre is its coordinates).</summary>
    private Vector2 GetMouseGridPoint() => GetMouseWorldPosition() / SquareGridUtils.CellSize;

    /// <summary>
    /// The point a click on <paramref name="square"/> aims at, in grid units: the cursor when it lies in that square, else
    /// the square's centre (a click with no cursor over the square, as the scenario harness makes; its centre gives the
    /// square's north-east corner as the nearest intersection).
    /// </summary>
    private Vector2 GetAimGridPointForClick(Vector2Int square)
    {
        Vector2 p = GetMouseGridPoint();
        if (SquareGridUtils.WorldToGrid(p * SquareGridUtils.CellSize) == square)
            return p;
        return new Vector2(square.x, square.y);
    }

    /// <summary>
    /// The area the PC aims at <paramref name="gridPoint"/> (grid units) with <paramref name="spell"/>, the one rule the
    /// cursor's preview and the click share (batch 11 step 24, decision D5, PHB p.175):
    /// a burst or cube has its point of origin at the intersection nearest the point, within the spell's range of the
    /// caster's space (AoESystem.IsOriginWithinRange) and in its line of effect (AoESystem.HasLineOfEffectToOrigin, PHB
    /// p.176); a one-square effect (Flaming Sphere, Call Lightning) takes the square
    /// under the point, within range, and has no intersection origin; a cone starts at the intersection on the edge of the
    /// caster's space nearest the point and points the way of the point (AoESystem.TryAimCone); a line starts there and runs
    /// through the point (AoESystem.TryAimLine). False when the point gives no legal aim.
    /// </summary>
    private bool TryBuildUiAreaAim(CharacterController caster, SpellData spell, Vector2 gridPoint,
        out HashSet<Vector2Int> cells, out GridIntersection? origin)
    {
        cells = null;
        origin = null;
        if (caster == null || spell == null || Grid == null)
            return false;
        int size = caster.GetCurrentSpaceSquares();
        int range = spell.AoERangeSquares > 0 ? spell.AoERangeSquares : CasterLevelRules.RangeSquares(caster, spell);

        if (AoESystem.IsSingleSquarePlacement(spell))
        {
            Vector2Int square = SquareGridUtils.WorldToGrid(gridPoint * SquareGridUtils.CellSize);
            if (Grid.GetCell(square) == null || !AoESystem.IsWithinCastingRange(caster.GridPosition, square, range))
                return false;
            cells = new HashSet<Vector2Int> { square };
            return true;
        }

        if (AoESystem.IsPlacedShape(spell.AoEShapeType))
        {
            GridIntersection point = GridIntersection.NearestToGridPoint(gridPoint);
            if (!AoESystem.IsOriginWithinRange(caster.GridPosition, size, point, range)
                || !AoESystem.HasLineOfEffectToOrigin(caster.GridPosition, size, point))
                return false;
            cells = AoESystem.GetPlacedAreaCells(spell, point, Grid);
            origin = point;
            return cells.Count > 0;
        }

        if (spell.AoEShapeType == AoEShape.Cone)
        {
            bool ok = AoESystem.TryAimCone(caster.GridPosition, size, gridPoint, spell.AoESizeSquares, Grid,
                out GridIntersection start, out _, out cells);
            origin = start;
            return ok;
        }

        if (spell.AoEShapeType == AoEShape.Line)
        {
            bool ok = AoESystem.TryAimLine(caster.GridPosition, size, gridPoint, spell.AoESizeSquares, Grid,
                out GridIntersection start, out cells);
            origin = start;
            return ok;
        }

        return false;
    }

    /// <summary>
    /// Get the current mouse position in world coordinates.
    /// Used by AoE targeting previews (including line spell endpoint selection).
    /// </summary>
    private Vector2 GetMouseWorldPosition()
    {
#if UNITY_EDITOR
        // A scenario's click aims at the point it names, not wherever the editor's cursor is (batch 11 step 24).
        if (Harness_AimPoint.HasValue)
            return Harness_AimPoint.Value * SquareGridUtils.CellSize;
#endif
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
        GridIntersection? aimOrigin = null;

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
            // From the intersection on the edge of the caster's space nearest the hovered square, through its centre
            // (TryBuildUiAreaAim; batch 11 step 24, PHB p.175).
            else
            {
                if (!AoESystem.IsWithinCastingRange(pc.GridPosition, gridPos, _pendingSpell.AoESizeSquares))
                    return;

                if (!TryBuildUiAreaAim(pc, _pendingSpell, new Vector2(gridPos.x, gridPos.y), out aoeCells, out aimOrigin))
                    return;
            }
        }
        else
        {
            Vector2 gridPoint = worldPoint / SquareGridUtils.CellSize;

            if (AoESystem.IsPlacedShape(_pendingSpell.AoEShapeType))
            {
                // A burst or cube snaps to the intersection nearest the cursor, its point of origin (a one-square
                // effect to the hovered square); the preview depends only on that.
                Vector2Int hoverKey;
                if (AoESystem.IsSingleSquarePlacement(_pendingSpell))
                {
                    hoverKey = SquareGridUtils.WorldToGrid(worldPoint);
                }
                else
                {
                    GridIntersection snapped = GridIntersection.NearestToGridPoint(gridPoint);
                    hoverKey = new Vector2Int(snapped.X, snapped.Y);
                }
                if (hoverKey == _lastAoEHoverPos) return;
                _lastAoEHoverPos = hoverKey;

                ClearAoEPreviewHighlights();

                if (!TryBuildUiAreaAim(pc, _pendingSpell, gridPoint, out aoeCells, out aimOrigin))
                    return;
            }
            else if (_pendingSpell.AoEShapeType == AoEShape.Cone)
            {
                // A cone starts at the intersection on the edge of the caster's space nearest the cursor and points the
                // way of the cursor (AoESystem.TryAimCone): the preview depends on that start and direction.
                bool aimed = AoESystem.TryAimCone(pc.GridPosition, pc.GetCurrentSpaceSquares(), gridPoint,
                    _pendingSpell.AoESizeSquares, Grid, out GridIntersection coneStart, out int coneDir, out HashSet<Vector2Int> coneCells);
                Vector2Int coneHoverKey = aimed
                    ? new Vector2Int(coneStart.X * 8 + coneDir, coneStart.Y)
                    : new Vector2Int(int.MinValue + 1, int.MinValue + 1);

                if (coneHoverKey == _lastConeHoverKey) return;
                _lastConeHoverKey = coneHoverKey;

                ClearAoEPreviewHighlights();
                if (!aimed)
                    return;
                aoeCells = coneCells;
                aimOrigin = coneStart;
            }
        }

        if (aoeCells == null || aoeCells.Count == 0) return;

        // ── Line-of-effect filtering for the PREVIEW ──
        // The squares the area's point of origin (a burst's or cube's intersection, a cone's or line's starting
        // intersection) has no line of effect to are dropped, so the player sees the squares that will be affected; the
        // cast core's Legality stage filters the cast from the same origin (batch 11 step 24, PHB p.176). Walls and
        // one-square effects keep their square-based origin.
        if (aimOrigin.HasValue)
        {
            AoESystem.FilterCellsByLineOfEffect(aoeCells, aimOrigin.Value);
        }
        else
        {
            Vector2Int loeOrigin = AoESystem.IsSingleSquarePlacement(_pendingSpell)
                ? SquareGridUtils.WorldToGrid(worldPoint)
                : pc.GridPosition;
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
                bool isAlly = TeamUtility.IsAlly(pc, occupant);
                bool isEnemy = TeamUtility.IsEnemy(pc, occupant);

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

            // Restore base range highlights (burst, cube and line), otherwise clear.
            if (_pendingSpell != null && AoESystem.IsPlacedShape(_pendingSpell.AoEShapeType))
            {
                int range = _pendingSpell.AoERangeSquares > 0
                    ? _pendingSpell.AoERangeSquares
                    : CasterLevelRules.RangeSquares(pc, _pendingSpell);
                if (pc != null && IsAreaPlacementSquareInRange(pc, _pendingSpell, cellPos, Mathf.Max(1, range)))
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

        // Validate range for line spells (a burst's or cube's point of origin is checked by TryBuildUiAreaAim below)
        if (_pendingSpell.AoEShapeType == AoEShape.Line)
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
                            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Click on one side of the wall, not on the wall itself."));
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
                        CombatUI?.ShowCombatLog(CombatLogHelper.Damage("🔥", $"Wall start set at ({targetPos.x}, {targetPos.y}). Click END point."));

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
                            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"End point too far from start (max {maxLen * 5} ft). Click closer."));
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
                        List<CharacterController> targets2 = AoESystem.GetTargetsInArea(wallCells, caster, _pendingSpell.AoEFilter, Grid);

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
                            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Click inside or outside the ring, not on the ring itself."));
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
                        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"❄ Wall start set at ({targetPos.x}, {targetPos.y}). Click END point."));

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
                            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"End point too far from start (max {maxLen * 5} ft). Click closer."));
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
                        List<CharacterController> targets3 = AoESystem.GetTargetsInArea(wallCells, caster, _pendingSpell.AoEFilter, Grid);

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

        // Calculate the final AoE cells and the area's point of origin, by the rule the preview uses (TryBuildUiAreaAim;
        // batch 11 step 24): a burst or cube at the intersection nearest the click, a cone or line from the intersection on
        // the edge of the caster's space nearest it. Wall of Fire and Wall of Ice are handled above and return early.
        // A line aims through the centre of the clicked square, as its preview does.
        Vector2 aimPoint = _pendingSpell.AoEShapeType == AoEShape.Line
            ? new Vector2(targetPos.x, targetPos.y)
            : GetAimGridPointForClick(targetPos);
        if (!TryBuildUiAreaAim(caster, _pendingSpell, aimPoint, out HashSet<Vector2Int> aoeCells, out GridIntersection? aimOrigin)
            || aoeCells == null || aoeCells.Count == 0)
        {
            Debug.Log($"[AoE] No legal area at ({targetPos.x},{targetPos.y}) (out of range, or a cone or line that would cover the caster)");
            return; // Don't cancel, just ignore the click
        }
        Debug.Log($"[AoE] {_pendingSpell.Name} aimed from {(aimOrigin.HasValue ? aimOrigin.Value.ToString() : "its square")} → {aoeCells.Count} cells");

        // Get all valid targets in the AoE (relative to the caster's side, SPL-119)
        List<CharacterController> targets = AoESystem.GetTargetsInArea(aoeCells, caster, _pendingSpell.AoEFilter, Grid);

        Debug.Log($"[AoE] {_pendingSpell.Name}: {aoeCells.Count} cells, {targets.Count} targets");

        // Exit AoE targeting mode
        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        // Execute the AoE spell
        PerformAoESpellCast(caster, targets, aoeCells, aimOrigin);
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
        // The cast ends with its spell, metamagic, choices and item or F12 source (batch 11 step 4); the wall sub-phase
        // fields are UI sequencing and are reset below.
        EndPendingCast();
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
        // The cast ends with its spell, metamagic, choices, summon picks and scroll, wand or F12 source (batch 11 step 4:
        // a cancelled wand cast used to leave its wand flag set, SPL-116); the wall sub-phase fields are UI sequencing.
        EndPendingCast();
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", "Remaining full-attack swings/shots cancelled."));
            return;
        }

        CharacterController pc = ActivePC;
        if (pc != null && _pendingDefensiveAttackSelection)
        {
            pc.SetFightingDefensively(false);
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{pc.Stats.CharacterName} cancels defensive attack declaration."));
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
        GridIntersection origin = _pendingSelfAoEOrigin;

        ClearSelfAoEState();

        // Now execute the spell (the cast core's Legality stage filters the area by line of effect)
        PerformAoESpellCast(caster, targets, cells, origin);
    }

    /// <summary>
    /// Called when the player cancels a self-centered AoE spell via right-click/Escape.
    /// Returns to action choices without consuming the spell slot.
    /// </summary>
    private void OnSelfAoECancelled()
    {
        Debug.Log($"[AoE] Self-centered AoE spell CANCELLED — no spell slot consumed");

        ClearSelfAoEState();

        // The cast ends with its own state (batch 11 step 4).
        EndPendingCast();

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
    /// Casts the open area cast from <paramref name="caster"/> over <paramref name="aoeCells"/> at the creatures in it
    /// (<paramref name="targets"/>): the aimed burst, cone or line, a self-centred area once confirmed, the walls once
    /// placed. Since batch 11 step 9 the body is the staged cast core (GameManager.CastCore.cs, ExecuteCast) with the UI
    /// decider and the area resolve variant: this method only does the UI's part before the cast and turns the open cast's
    /// request into an area request (SpellCastRequest.ForUiArea, which sets the area path's legacy quirks); the core then
    /// filters the area by line of effect, spends the cast, runs the area handler registry (GameManager.SpellHandlerRegistry.cs)
    /// and finishes in FinishAreaCast. The UI follow-up comes from UiCastDecider.OnCastFinished (GameManager.CastDeciders.cs).
    /// <paramref name="origin"/> is the area's point of origin, a grid intersection (a burst's or cube's, a cone's or line's
    /// starting one; batch 11 step 24), recorded on the aim so line of effect is traced from it; the walls pass none and
    /// keep the caster's square.
    /// </summary>
    private void PerformAoESpellCast(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells,
        GridIntersection? origin = null)
    {
        // A cast whose run waits for the AoO prompt's answer is not re-entered: its aim and run state stay.
        if (RejectCastReentry(CurrentCast, nameof(PerformAoESpellCast)))
            return;

        CastStageTrace.Mark(caster, _pendingSpell, CastStageTrace.Begin, nameof(PerformAoESpellCast));

        CurrentSubPhase = PlayerSubPhase.Animating;
        // The spell menu's spontaneous-conversion state was copied into the cast's source and cleared when the request
        // was built (batch 11 step 4); clearing it again is the old belt and braces.
        CombatUI?.ClearSpontaneousCastState();

        CastContext ctx = CurrentCast;
        if (ctx == null || ctx.Request.Spell == null || ctx.Request.Decider.IsSynchronous)
        {
            Debug.LogWarning("[AoE] PerformAoESpellCast without an open Ui cast; back to the action menu.");
            ShowActionChoices();
            return;
        }

        SpellCastRequest req = ctx.Request;
        if (req.Caster != caster)
        {
            if (req.Caster != null)
                Debug.LogWarning($"[AoE] PerformAoESpellCast: the cast's caster {req.Caster.Stats?.CharacterName} is replaced by {caster?.Stats?.CharacterName}.");
            req.Caster = caster;
        }
        SpellCastRequest.ForUiArea(req, targets, aoeCells, origin);

        ExecuteCast(ctx, null);
    }

    /// <summary>
    /// The area handler registry's fallback (GameManager.SpellHandlerRegistry.cs, batch 11 step 9): the generic
    /// per-target loop for an area spell no custom handler claims. Each creature in the area (after line of effect and the
    /// cast core's Globe stage, which drops the creatures a globe protects) gets the tracked effect (Buff, Debuff, Control, Illusion, Wall), or SpellCaster.Cast's save and
    /// damage, or its healing. Returns the cast's log (without the quickened and spontaneous prefixes, which
    /// FinishAreaCast adds); the core shows it and runs the combat-end check ("ResolveAOESpell").
    /// </summary>
    private string ResolveGenericAreaSpell(CastContext ctx)
    {
        CharacterController caster = ctx.Request.Caster;
        List<CharacterController> targets = ctx.Request.Aim.AreaTargets;
        HashSet<Vector2Int> aoeCells = ctx.Request.Aim.Cells;
        SpellcastingComponent spellComp = ctx.SpellComp;
        // The request's own spell and metamagic, not the _pendingSpell and _pendingMetamagic views, which
        // read whichever cast is on top of the cast stack (batch 11 step 25: an NPC area cast runs nested over a Ui cast
        // the player left open).
        SpellData spell = ctx.Request.Spell;
        MetamagicData metamagic = ctx.Request.Metamagic;

        if (aoeCells != null
            && string.Equals(spell.DamageType, "fire", StringComparison.OrdinalIgnoreCase))
        {
            foreach (Vector2Int cell in aoeCells)
                NotifyFireDamageAtPosition(cell, spell.Name);
        }

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, "generic-area-loop");

        // Build the combat log header
        var logBuilder = new System.Text.StringBuilder();
        string shapeStr = spell.AoEShapeType == AoEShape.Cone ? "cone" :
                          spell.AoEShapeType == AoEShape.Burst ? "burst" :
                          spell.AoEShapeType == AoEShape.Cube ? "cube" : "line";
        logBuilder.AppendLine($"═══════════════════════════════════");
        logBuilder.AppendLine($"✨ {caster.Stats.CharacterName} casts {spell.Name}! ({spell.AoESizeSquares * 5}-ft {shapeStr})");
        logBuilder.AppendLine($"  [{(spell.SpellLevel == 0 ? "Cantrip" : $"Level {spell.SpellLevel}")}] {spell.School}");
        if (metamagic != null && metamagic.HasAnyMetamagic)
            logBuilder.AppendLine($"  Metamagic: {metamagic.GetDisplayName()}");
        logBuilder.AppendLine($"  Targets: {targets.Count} creature(s) in {aoeCells.Count} squares");
        logBuilder.AppendLine();

        if (targets.Count == 0)
        {
            logBuilder.AppendLine($"  No valid targets in area!");
            logBuilder.Append($"═══════════════════════════════════");
        }
        else
        {
            // Resolve spell for each target. A damage spell's dice are rolled once for the whole area, at the first creature
            // its spell resistance does not stop, and every creature takes that one total before its own save (AreaDamageRoll,
            // decision D6; delegated RAW reading 2026-10-14, batch 11 step 20, SPL-180).
            var areaDamageRoll = new AreaDamageRoll();
            int targetIndex = 0;
            foreach (CharacterController target in targets)
            {
                targetIndex++;
                logBuilder.AppendLine($"  --- Target {targetIndex}: {target.Stats.CharacterName} ---");

                // A creature inside a globe of invulnerability that excludes this spell was dropped from the targets by
                // the cast core's Globe stage before any area handler ran (CastStageAreaGlobe, SPL-141).

                // For buff/debuff/control/illusion/wall spells, apply tracked effects. The area pipeline routes only
                // the tracked effect types (SpellEffectRouting.IsTrackedEffectType), not the single-target extras.
                if (SpellEffectRouting.IsTrackedEffectType(spell.EffectType))
                {
                    var appliedEffect = ApplySpellBuff(caster, target, spell, spellComp);
                    if (ScenarioHooks.CastStage != null)
                        CastStageTrace.Mark(caster, spell, CastStageTrace.Buff, (appliedEffect != null ? "applied " : "no-effect ") + target.Stats.CharacterName);

                    // Track concentration for the first target of a concentration AoE effect
                    if (appliedEffect != null && spell.DurationType == DurationType.Concentration && targetIndex == 1)
                    {
                        BeginConcentrationTracking(caster, appliedEffect, spell);
                    }

                    string effectLabel = (spell.EffectType == SpellEffectType.Debuff || spell.EffectType == SpellEffectType.Control)
                        ? "DEBUFF APPLIED" : "BUFF APPLIED";
                    logBuilder.AppendLine($"  {effectLabel}! {spell.Description}");
                    Debug.Log($"[AoE] {spell.EffectType} applied to {target.Stats.CharacterName}");
                }
                // For damage spells, resolve with save and damage
                else if (spell.EffectType == SpellEffectType.Damage)
                {
                    // The target's own decider says whether it gives up its save (PHB p.177; batch 11 step 18, SPL-149):
                    // never against a damage spell, so an ally in its own side's Burning Hands rolls its Reflex save.
                    bool aoeForceFailSave = ctx.TargetForgoesSave(target);
                    SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, metamagic, false, aoeForceFailSave, caster, target,
                        areaDamageRoll: areaDamageRoll);
                    if (ScenarioHooks.CastStage != null)
                        CastStageTrace.Mark(caster, spell, CastStageTrace.SpellCasterCast, (result.Success ? "success " : "failed ") + target.Stats.CharacterName);

                    if (result.RequiredSave)
                    {
                        string saveResult = result.SaveSucceeded ? "SAVED" : "FAILED";
                        logBuilder.AppendLine($"  {result.SaveType} save DC {result.SaveDC}: d20={result.SaveRoll}+{result.SaveMod}={result.SaveTotal} - {saveResult}!");
                    }
                    if (!string.IsNullOrEmpty(result.EvasionNote))
                        logBuilder.AppendLine($"  {result.EvasionNote}");

                    if (result.DamageDealt > 0)
                    {
                        if (result.DamageRolls != null && result.DamageRolls.Length > 0 && result.Spell != null)
                            logBuilder.AppendLine($"  Damage: {result.DiceCount}d{result.DiceSides}{(result.DiceBonus > 0 ? "+" + result.DiceBonus : "")} {SpellResult.FormatDiceRolls(result.DamageRolls)} (CL {result.CasterLevel}) = {result.DamageDealt} {result.DamageType}");
                        else
                            logBuilder.AppendLine($"  Damage: {result.DamageDealt} {result.DamageType}");
                        if (result.EmpowerBonus > 0)
                            logBuilder.AppendLine($"    Empower: +{result.EmpowerBonus} (×1.5)");
                        if (result.Metamagic != null && result.Metamagic.Has(MetamagicFeatId.MaximizeSpell))
                            logBuilder.AppendLine($"    Maximized (all dice set to max)");
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
                else if (spell.EffectType == SpellEffectType.Healing)
                {
                    // A living creature of the caster's side gives up its save against a healing spell (PHB p.177); an
                    // undead one does not (SpellSaveRules.IsHarmlessOrBeneficialTo; batch 11 step 18).
                    bool aoeHealForceFailSave = ctx.TargetForgoesSave(target);
                    SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, metamagic, false, aoeHealForceFailSave, caster, target);
                    if (ScenarioHooks.CastStage != null)
                        CastStageTrace.Mark(caster, spell, CastStageTrace.SpellCasterCast, (result.Success ? "success " : "failed ") + target.Stats.CharacterName);

                    logBuilder.AppendLine($"  Healed: {result.HealingDone} HP");
                    if (result.EmpowerBonus > 0)
                        logBuilder.AppendLine($"    Empower: +{result.EmpowerBonus} (×1.5)");
                    if (result.Metamagic != null && result.Metamagic.Has(MetamagicFeatId.MaximizeSpell))
                        logBuilder.AppendLine($"    Maximized (all dice set to max)");
                    logBuilder.AppendLine($"  {target.Stats.CharacterName}: {result.TargetHPBefore} → {result.TargetHPAfter} HP");
                }

                logBuilder.AppendLine();
            }

            logBuilder.Append($"═══════════════════════════════════");
        }

        return logBuilder.ToString();
    }

    /// <summary>
    /// Bane over its area (PHB p.203; batch 11 step 23, SPL-046): every enemy of the caster within 50 ft. (the aim's
    /// creatures after the EnemiesOnly filter, line of effect and the cast core's Globe stage) gets the single-target cast's
    /// own tests through SpellCaster.Cast, its spell resistance, its Will save (negates) and the mind-affecting immunity,
    /// and an affected creature gets the tracked effect (ApplySpellBuff: -1 on attack rolls and on saves against fear),
    /// exactly as a single-target Bane gave it. Returns the cast's log. The same for every caster. (The generic loop
    /// would apply the effect with no save or spell resistance, SPL-027.)
    /// </summary>
    private string ResolveBaneAoE(CastContext ctx)
    {
        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;
        List<CharacterController> targets = ctx.Request.Aim.AreaTargets;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts {spell.Name}! (enemies within {spell.AoESizeSquares * 5} ft.)");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        int targetIndex = 0;
        if (targets != null)
        {
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead)
                    continue;

                targetIndex++;
                string targetName = target.Stats.CharacterName;
                sb.AppendLine($"  --- Target {targetIndex}: {targetName} ---");

                SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, ctx.Request.Metamagic, false, ctx.TargetForgoesSave(target), caster, target);
                if (ScenarioHooks.CastStage != null)
                    CastStageTrace.Mark(caster, spell, CastStageTrace.SpellCasterCast, (result.Success ? "success " : "failed ") + targetName);

                if (result.MindAffectingImmunityBlocked)
                {
                    sb.AppendLine($"  {targetName} is immune to mind-affecting effects.");
                }
                else if (!result.Success)
                {
                    string why = !result.SpellResistancePassed
                        ? $"spell resistance (d20 {result.SpellResistanceRoll}, total {result.SpellResistanceTotal} vs SR {result.SpellResistanceValue})"
                        : (result.NoEffectReason ?? "no effect");
                    sb.AppendLine($"  {targetName} is unaffected: {why}.");
                }
                else if (SpellUtilities.IsEffectNegatedBySave(spell, result, !target.IsIntelligentUndead()))
                {
                    sb.AppendLine($"  {result.SaveType} save DC {result.SaveDC}: d20={result.SaveRoll}+{result.SaveMod}={result.SaveTotal} - SAVED (negates).");
                }
                else
                {
                    if (result.RequiredSave)
                        sb.AppendLine($"  {result.SaveType} save DC {result.SaveDC}: d20={result.SaveRoll}+{result.SaveMod}={result.SaveTotal} - FAILED.");
                    ActiveSpellEffect applied = ApplySpellBuff(caster, target, spell, ctx.SpellComp, result);
                    if (ScenarioHooks.CastStage != null)
                        CastStageTrace.Mark(caster, spell, CastStageTrace.Buff, (applied != null ? "applied " : "no-effect ") + targetName);
                    sb.AppendLine(applied != null
                        ? $"  DEBUFF APPLIED! {targetName}: -1 on attack rolls and on saves against fear."
                        : $"  {targetName}: no effect.");
                }
                sb.AppendLine();
            }
        }

        if (targetIndex == 0)
            sb.AppendLine("  No valid targets in area!");
        sb.Append("═══════════════════════════════════");
        return sb.ToString();
    }

    /// <summary>
    /// An area handler's spell resistance line: "d20 R + CL C = T vs SR S", with " + P Spell Penetration" after the caster
    /// level when the caster has the feat (the total always included it).
    /// </summary>
    private static string FormatAreaSpellResistanceCheck(SRResult sr)
        => $"d20 {sr.Roll} + CL {sr.CasterLevel}{(sr.PenetrationBonus > 0 ? $" + {sr.PenetrationBonus} Spell Penetration" : "")} = {sr.Total} vs SR {sr.TargetSR}";

    /// <summary>
    /// Resolve Hypnotism (PHB p.242) over the area's creatures.
    /// Returns the cast's log (null for no caster or spell); since batch 11 step 9 the cast core shows it, refreshes the
    /// UI, runs the combat-end check and paces the turn (FinishAreaCast), and the area handler registry calls this.
    /// </summary>
    private string ResolveHypnotismSpell(CastContext ctx, CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (ctx == null || caster == null || caster.Stats == null || ctx.Request.Spell == null)
            return null;

        int casterLevel = SpellCastingHelper.GetBaseCasterLevel(caster);
        int saveDc = GetSpellSaveDC(caster, ctx.Request.Spell);
        // 2d4 HD and 2d4 rounds, variable effects the cast's Empower and Maximize change (PHB p.93, p.97); Extend doubles
        // the rounds (PHB p.94) (ctx.RollSpellDice, SpellDurationRules.Extended; batch 11 step 19, SPL-038)
        int hdPool = ctx.RollSpellDice(CustomSpellDice.HypnotismHitDice(), "Hypnotism HD pool 2d4"); // 2d4
        int fascinatedRounds = SpellDurationRules.Extended(ctx.Request.Spell, ctx.RollSpellDice(CustomSpellDice.HypnotismRounds(), "Fascinated rounds 2d4")); // 2d4

        List<CharacterController> candidates = new List<CharacterController>();
        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(caster, target))
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
            int aHd = TeamUtility.GetHitDice(a);
            int bHd = TeamUtility.GetHitDice(b);
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
            int targetHd = Mathf.Max(1, TeamUtility.GetHitDice(target));
            if (targetHd > remainingPool)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName} ({targetHd} HD) exceeds remaining pool ({remainingPool}) — skipped.");
                continue;
            }

            remainingPool -= targetHd;

            // The one spell resistance check and the cast's save (batch 11 step 18; SPL-122, SPL-018).
            if (ctx.Request.Spell.SpellResistanceApplies)
            {
                SRResult sr = ctx.RollSpellResistance(target, "Hypnotism SR check");
                if (!sr.Overcame)
                {
                    logBuilder.AppendLine($"  • {target.Stats.CharacterName}: SR blocks effect ({FormatAreaSpellResistanceCheck(sr)}).");
                    continue;
                }
            }

            SaveResult save = ctx.RollSave(target, SaveType.Will, saveDc, "Hypnotism Will save", saveContextModifier);
            int saveTotal = save.Total;
            bool saved = save.Saved;
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
                SourceSpellId = ctx.Request.Spell.SpellId,
                SourceEffectName = ctx.Request.Spell.Name
            };

            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Fascinated,
                    fascinatedRounds,
                    source: caster,
                    data: fascinatedData,
                    sourceNameOverride: ctx.Request.Spell.Name,
                    sourceCategory: "Spell",
                    sourceId: ctx.Request.Spell.SpellId);
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

        // The cast core shows the log, refreshes the UI, runs the combat-end check and paces the turn (FinishAreaCast).
        return logBuilder.ToString();
    }

    /// <summary>
    /// The caster's key spellcasting ability modifier for the class that casts <paramref name="spell"/>
    /// (<see cref="SpellSaveDCRules.GetKeyAbilityModifier(CharacterStats, SpellData)"/>).
    /// </summary>
    private int GetSpellSaveAbilityModifier(CharacterController caster, SpellData spell)
    {
        return SpellUtilities.GetCastingAbilityModifier(caster, spell);
    }

    /// <summary>
    /// Resolve Sleep (PHB p.280) over the area's creatures.
    /// Returns the cast's log (null for no caster or spell); since batch 11 step 9 the cast core shows it, refreshes the
    /// UI, runs the combat-end check and paces the turn (FinishAreaCast), and the area handler registry calls this.
    /// </summary>
    private string ResolveSleepSpell(CastContext ctx, CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (ctx == null || caster == null || caster.Stats == null || ctx.Request.Spell == null)
            return null;

        int casterLevel = SpellCastingHelper.GetBaseCasterLevel(caster);
        int saveDc = GetSpellSaveDC(caster, ctx.Request.Spell);
        int hdPool = DiceService.RollMultiple(4, 4, "Sleep HD pool 4d4"); // 4d4
        int sleepRounds = SpellCastingHelper.CalculateDuration(ctx.Request.Spell, casterLevel);

        List<CharacterController> candidates = new List<CharacterController>();
        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;
            if (IsImmuneToSleepEffects(target))
                continue;

            int targetHd = Mathf.Max(1, TeamUtility.GetHitDice(target));
            if (targetHd > 4)
                continue;

            candidates.Add(target);
        }

        candidates.Sort((a, b) =>
        {
            int aHd = TeamUtility.GetHitDice(a);
            int bHd = TeamUtility.GetHitDice(b);
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
            int targetHd = Mathf.Max(1, TeamUtility.GetHitDice(target));
            if (targetHd > remainingPool)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName} ({targetHd} HD) exceeds remaining pool ({remainingPool}) — skipped.");
                continue;
            }

            remainingPool -= targetHd;

            // The one spell resistance check and the cast's save (batch 11 step 18; SPL-122, SPL-018); Lullaby's -2 against
            // sleep effects (PHB p.249) is in the save's spell context.
            if (ctx.Request.Spell.SpellResistanceApplies)
            {
                SRResult sr = ctx.RollSpellResistance(target, "Sleep SR check");
                if (!sr.Overcame)
                {
                    logBuilder.AppendLine($"  • {target.Stats.CharacterName}: SR blocks effect ({FormatAreaSpellResistanceCheck(sr)}).");
                    continue;
                }
            }

            SaveResult save = ctx.RollSave(target, SaveType.Will, saveDc, "Sleep Will save");
            int saveTotal = save.Total;
            if (save.Saved)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName}: Will save succeeds ({saveTotal} vs DC {saveDc}).");
                continue;
            }

            ApplySleepState(caster, target, sleepRounds, saveDc, ctx.Request.Spell);
            affectedCount++;
            logBuilder.AppendLine($"  • {target.Stats.CharacterName}: falls asleep for {sleepRounds} rounds (Will {saveTotal} vs DC {saveDc}).");
        }

        logBuilder.AppendLine();
        logBuilder.AppendLine($"  Result: {affectedCount} target(s) asleep. Remaining HD pool: {remainingPool}.");
        logBuilder.Append("═══════════════════════════════════");

        // The cast core shows the log, refreshes the UI, runs the combat-end check and paces the turn (FinishAreaCast).
        return logBuilder.ToString();
    }

    /// <summary>
    /// Resolve Deep Slumber spell (PHB p.217).
    /// As Sleep, but affects up to 10 HD of creatures (no per-creature HD cap).
    /// Lowest HD creatures affected first; Will negates; SR applies.
    /// Returns the cast's log (null for no caster or spell); since batch 11 step 9 the cast core shows it, refreshes the
    /// UI, runs the combat-end check and paces the turn (FinishAreaCast), and the area handler registry calls this.
    /// </summary>
    private string ResolveDeepSlumberSpell(CastContext ctx, CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (ctx == null || caster == null || caster.Stats == null || ctx.Request.Spell == null)
            return null;

        int casterLevel = SpellCastingHelper.GetBaseCasterLevel(caster);
        int saveDc = GetSpellSaveDC(caster, ctx.Request.Spell);
        int hdPool = 10; // Deep Slumber: flat 10 HD (no dice roll, unlike Sleep's 4d4)
        int sleepRounds = SpellCastingHelper.CalculateDuration(ctx.Request.Spell, casterLevel);

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
            int aHd = TeamUtility.GetHitDice(a);
            int bHd = TeamUtility.GetHitDice(b);
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
            int targetHd = Mathf.Max(1, TeamUtility.GetHitDice(target));
            if (targetHd > remainingPool)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName} ({targetHd} HD) exceeds remaining pool ({remainingPool}) — skipped.");
                continue;
            }

            remainingPool -= targetHd;

            // The one spell resistance check and the cast's save (batch 11 step 18; SPL-122, SPL-018); Lullaby's -2 against
            // sleep effects (PHB p.249) is in the save's spell context. The dice keep their old labels.
            if (ctx.Request.Spell.SpellResistanceApplies)
            {
                SRResult sr = ctx.RollSpellResistance(target, "Color Spray SR check");
                if (!sr.Overcame)
                {
                    logBuilder.AppendLine($"  • {target.Stats.CharacterName}: SR blocks effect ({FormatAreaSpellResistanceCheck(sr)}).");
                    continue;
                }
            }

            SaveResult save = ctx.RollSave(target, SaveType.Will, saveDc, "Color Spray Will save");
            int saveTotal = save.Total;
            if (save.Saved)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName}: Will save succeeds ({saveTotal} vs DC {saveDc}).");
                continue;
            }

            ApplySleepState(caster, target, sleepRounds, saveDc, ctx.Request.Spell);
            affectedCount++;
            logBuilder.AppendLine($"  • {target.Stats.CharacterName}: falls into deep slumber for {sleepRounds} rounds (Will {saveTotal} vs DC {saveDc}).");
        }

        logBuilder.AppendLine();
        logBuilder.AppendLine($"  Result: {affectedCount} target(s) in deep slumber. Remaining HD pool: {remainingPool}.");
        logBuilder.Append("═══════════════════════════════════");

        // The cast core shows the log, refreshes the UI, runs the combat-end check and paces the turn (FinishAreaCast).
        return logBuilder.ToString();
    }

    /// <summary>
    /// Resolve Color Spray (PHB p.210) over the cone's creatures.
    /// Returns the cast's log (null for no caster or spell); since batch 11 step 9 the cast core shows it, refreshes the
    /// UI, runs the combat-end check and paces the turn (FinishAreaCast), and the area handler registry calls this.
    /// </summary>
    private string ResolveColorSpraySpell(CastContext ctx, CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (ctx == null || caster == null || caster.Stats == null || ctx.Request.Spell == null)
            return null;

        int casterLevel = SpellCastingHelper.GetBaseCasterLevel(caster);
        int saveDc = GetSpellSaveDC(caster, ctx.Request.Spell);

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

            int targetHd = Mathf.Max(1, TeamUtility.GetHitDice(target));

            // The one spell resistance check and the cast's save (batch 11 step 18; SPL-122, SPL-018). The dice keep their
            // old labels.
            if (ctx.Request.Spell.SpellResistanceApplies)
            {
                SRResult sr = ctx.RollSpellResistance(target, "Dazing Touch SR check");
                if (!sr.Overcame)
                {
                    logBuilder.AppendLine($"  • {target.Stats.CharacterName}: SR blocks Color Spray ({FormatAreaSpellResistanceCheck(sr)}).");
                    continue;
                }
            }

            SaveResult save = ctx.RollSave(target, SaveType.Will, saveDc, "Dazing Touch Will save");
            int saveTotal = save.Total;
            if (save.Saved)
            {
                logBuilder.AppendLine($"  • {target.Stats.CharacterName}: Will save succeeds ({saveTotal} vs DC {saveDc}).");
                continue;
            }

            ColorSprayEffectData effectData = BuildColorSprayEffectData(caster, targetHd, ctx.Request.Spell);
            ApplyColorSprayStageConditions(target, effectData, ctx.Request.Spell);
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

        // The cast core shows the log, refreshes the UI, runs the combat-end check and paces the turn (FinishAreaCast).
        return logBuilder.ToString();
    }

    /// <summary>
    /// Color Spray's staged durations by the target's Hit Dice (PHB p.210). The rolled ones (2d4, 1d4) are variable effects
    /// the cast's Empower and Maximize change (PHB p.93, p.97; SpellDiceRules.RollFor, batch 11 step 19, SPL-038); Extend
    /// does not, the spell being instantaneous (PHB p.94, p.210).
    /// </summary>
    private ColorSprayEffectData BuildColorSprayEffectData(CharacterController caster, int targetHd, SpellData castSpell = null)
    {
        int hd = Mathf.Max(1, targetHd);

        int stage1Duration;
        int stage2Duration;
        int stage3Duration;
        int hdTier;

        if (hd <= 2)
        {
            hdTier = 1;
            stage1Duration = SpellDiceRules.RollFor(castSpell, SpellDice.Of(2, 4), "Disease stage 1 duration 2d4"); // 2d4
            stage2Duration = SpellDiceRules.RollFor(castSpell, SpellDice.Of(1, 4), "Disease stage 2 duration 1d4"); // 1d4
            stage3Duration = 1;
        }
        else if (hd <= 4)
        {
            hdTier = 2;
            stage1Duration = SpellDiceRules.RollFor(castSpell, SpellDice.Of(1, 4), "Disease stage 1 duration 1d4"); // 1d4
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"{character.Stats.CharacterName} is no longer stunned."));
            return true;
        }

        if (data.CurrentStage == 1 && data.HdTier == 1)
            CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"{character.Stats.CharacterName} no longer unconscious, still blinded and stunned ({Mathf.Max(1, GetColorSprayStageDuration(data, nextStage))} rounds)."));
        else if (((data.CurrentStage == 2 && data.HdTier == 1) || (data.CurrentStage == 1 && data.HdTier == 2)))
            CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"{character.Stats.CharacterName} no longer blinded, still stunned ({Mathf.Max(1, GetColorSprayStageDuration(data, nextStage))} round{(Mathf.Max(1, GetColorSprayStageDuration(data, nextStage)) == 1 ? string.Empty : "s")})."));

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

    /// <summary>Delegates to <see cref="SpellUtilities.IsImmuneToSleepEffects"/>.</summary>
    private bool IsImmuneToSleepEffects(CharacterController target)
    {
        return SpellUtilities.IsImmuneToSleepEffects(target);
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("💤", $"{target.Stats.CharacterName} wakes{wakerText}{reasonText}."));
        }

        return true;
    }

    private bool TryResolveCauseFearSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsCauseFearSpell(spell) || target == null || target.Stats == null)
            return false;

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveCauseFearSpellEffect));

        if (result != null && result.MindAffectingImmunityBlocked)
            return true;

        string targetName = target.Stats.CharacterName;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("✨", $"{casterName} casts Cause Fear on {targetName}."));

        // Spell failure, spell resistance (PHB p.177; Cause Fear SR: Yes) or a Lesser Globe already
        // stopped the spell: claim it so no later handler applies it, but apply nothing.
        if (result != null && !result.Success)
            return true;

        if (!SpellTargetingService.IsLivingCreature(target))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is immune to fear (not a living creature).";
            }

            CombatUI?.ShowCombatLog(CombatLogHelper.Immune("", $"🧟 {targetName} is immune to fear effects."));
            return true;
        }

        int targetHd = Mathf.Max(1, TeamUtility.GetHitDice(target));
        if (targetHd > 5)
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is too powerful to be frightened.";
            }

            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{targetName} is too powerful to be frightened."));
            return true;
        }

        if (result != null && result.RequiredSave && result.SaveSucceeded)
        {
            // 1 round, doubled by Extend (PHB p.94; SpellDurationRules.Extended, batch 11 step 19, SPL-038)
            int shakenRounds = SpellDurationRules.Extended(spell, 1);
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
            result.BuffDescription = $"Debuff: Shaken for {SpellDurationRules.DescribeRounds(shakenRounds)} (successful Will save reduces Cause Fear).";
            CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("😰", $"{targetName} resists the worst of Cause Fear and is shaken for {SpellDurationRules.DescribeRounds(shakenRounds)}."));
            return true;
        }

        // 1d4 rounds (PHB p.208), with the cast's metamagic: Empower and Maximize change the roll (PHB p.93, p.97), Extend
        // doubles it (PHB p.94) (batch 11 step 19, SPL-038)
        int frightenedRounds = SpellDurationRules.Extended(spell, SpellDiceRules.RollFor(spell, SpellDice.Of(1, 4), "Frightened duration 1d4"));
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

        CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("😱", $"{targetName} fails Will save - Frightened for {frightenedRounds} rounds! ({casterName} is the source of fear)"));
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

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveGhoulTouchSpellEffect));

        string targetName = target.Stats.CharacterName;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("✨", $"{casterName} casts Ghoul Touch on {targetName}."));

        // Ghoul Touch only works on living humanoids (PHB p.235)
        if (!SpellTargetingService.IsLivingCreature(target))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is not a living creature.";
            }
            CombatUI?.ShowCombatLog(CombatLogHelper.Immune("", $"🧟 {targetName} is immune to Ghoul Touch (not a living creature)."));
            return true;
        }

        if (!SpellTargetingService.IsHumanoid(target))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is not a humanoid.";
            }
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{targetName} is immune to Ghoul Touch (not a humanoid)."));
            return true;
        }

        // Touch attack must hit
        if (result != null && result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", $"{casterName}'s Ghoul Touch misses {targetName}."));
            return true;
        }

        // A cast that failed after the touch landed has no effect: spell resistance won (PHB p.235, SR yes), a globe
        // blocked it (PHB p.236, p.246) or the caster's deafness spoiled the verbal component (PHB p.307). The registry
        // runs this handler with gate None, so it tests the result itself, as Cause Fear and Scare do.
        if (result != null && !result.Success)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", $"{casterName}'s Ghoul Touch fails to take hold on {targetName}."));
            return true;
        }

        // Fort save negates
        if (result != null && result.RequiredSave && result.SaveSucceeded)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{targetName} resists Ghoul Touch with a successful Fortitude save."));
            return true;
        }

        // Failed save: apply paralysis + stench
        GhoulTouchEffectData ghoulEffect = GhoulTouchEffectData.CreateGhoulTouch(caster, target, spell);

        // Apply paralysis via CharacterController
        target.ApplyGhoulTouchEffect(ghoulEffect);

        // Track via StatusEffectManager for dispel/duration
        StatusEffectManager targetStatusMgr = target.StatusEffectManager;
        if (targetStatusMgr == null)
        {
            targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);
        }

        // PHB p.235: 1d6+2 rounds; the tracked effect lasts as long as the paralysis just rolled.
        targetStatusMgr.AddEffect(spell, casterName, CasterLevelRules.ForCaster(caster, spell),
            Mathf.Max(1, ghoulEffect.ParalysisDurationRounds));

        if (result != null)
        {
            result.BuffApplied = true;
            result.BuffDescription = $"Debuff: Paralyzed for {ghoulEffect.ParalysisDurationRounds} rounds (stench aura active).";
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Failure("⛓", $"{targetName} is paralyzed by Ghoul Touch for {ghoulEffect.ParalysisDurationRounds} rounds!"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("☠", $"Carrion stench emanates from {targetName} (10-ft radius, sickens living creatures)."));

        // Apply stench to nearby creatures immediately
        // The stench uses this cast's save DC (PHB p.235): the paralysis DC SpellCaster.Cast rolled against, else the
        // handler DC (keeps a scroll's or wand's fixed DC and the pending cast's Heighten; SPL-001).
        int stenchDC = result != null && result.SaveDC > 0 ? result.SaveDC : GetSpellSaveDC(caster, spell);
        ApplyGhoulTouchStench(caster, target, ghoulEffect, stenchDC);

        Debug.Log($"[GameManager] Ghoul Touch: {targetName} paralyzed for {ghoulEffect.ParalysisDurationRounds} rounds, stench active");
        return true;
    }

    /// <summary>
    /// Apply Ghoul Touch stench aura to living creatures within 10 ft of the paralyzed target.
    /// Each creature makes a Fort save or becomes sickened. Caster is exempt. Poison effect.
    /// </summary>
    private void ApplyGhoulTouchStench(CharacterController caster, CharacterController paralyzedTarget, GhoulTouchEffectData ghoulEffect, int spellDC)
    {
        if (paralyzedTarget == null || !ghoulEffect.IsStenchActive)
            return;

        var allCharacters = GetAllCharacters();
        if (allCharacters == null) return;

        foreach (CharacterController creature in allCharacters)
        {
            if (creature == null || creature == paralyzedTarget) continue;

            if (!ghoulEffect.IsValidStenchTarget(creature)) continue;
            if (ghoulEffect.IsCreaturePoisonImmune(creature))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{creature.Stats.CharacterName} is immune to poison (stench has no effect)."));
                continue;
            }

            // Check distance (10 ft = 2 squares)
            int distance = Mathf.Max(
                Mathf.Abs(creature.GridPosition.x - paralyzedTarget.GridPosition.x),
                Mathf.Abs(creature.GridPosition.y - paralyzedTarget.GridPosition.y));

            if (distance > ghoulEffect.StenchRadiusSquares) continue;

            // Fort save vs sickened
            // The stench is a poison effect of the spell (PHB p.235): spell and poison bonuses apply (SaveRules, CHR-018),
            // and those against its caster (the protection ward, SPL-018).
            int fortSave = DiceService.D20("Ghoul Touch Fort save")
                + SaveRules.Modifier(creature.Stats, SavingThrowType.Fortitude,
                    new SaveContext(spell: true, poison: true, label: "Ghoul Touch stench", sourceCaster: caster));
            bool saved = fortSave >= spellDC;

            if (saved)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{creature.Stats.CharacterName} resists the carrion stench (Fort {fortSave} vs DC {spellDC})."));
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

                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"🤢 {creature.Stats.CharacterName} is sickened by the carrion stench! (-2 to attacks, damage, saves, checks) Fort {fortSave} vs DC {spellDC}"));
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

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveScareSpellEffect));

        string targetName = target.Stats.CharacterName;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        int casterLevel = CasterLevelRules.ForCaster(caster, spell);

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("✨", $"{casterName} casts Scare on {targetName}."));

        // Check mind-affecting immunity
        if (result != null && result.MindAffectingImmunityBlocked)
            return true;

        // Spell failure, spell resistance (PHB p.177; Scare SR: Yes) or a Lesser Globe already
        // stopped the spell: claim it so no later handler applies it, but apply nothing.
        if (result != null && !result.Success)
            return true;

        // Must be a living creature
        if (!SpellTargetingService.IsLivingCreature(target))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} is immune to fear (not a living creature).";
            }
            CombatUI?.ShowCombatLog(CombatLogHelper.Immune("", $"🧟 {targetName} is immune to Scare (not a living creature)."));
            return true;
        }

        // HD limit: 6+ HD are completely immune
        int targetHd = Mathf.Max(1, TeamUtility.GetHitDice(target));
        if (ScareEffectData.IsImmuneByHD(targetHd))
        {
            if (result != null)
            {
                result.Success = false;
                result.NoEffectReason = $"{targetName} has {targetHd} HD (6+ HD immune to Scare).";
            }
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{targetName} ({targetHd} HD) is too powerful to be affected by Scare."));
            return true;
        }

        // Will save partial
        if (result != null && result.RequiredSave && result.SaveSucceeded)
        {
            // Successful save: shaken for 1 round, doubled by Extend (PHB p.94; batch 11 step 19, SPL-038)
            int shakenRounds = SpellDurationRules.Extended(spell, 1);
            ScareEffectData shakenEffect = ScareEffectData.CreateShaken(caster, shakenRounds);
            target.ApplyScareEffect(shakenEffect);

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

            if (result != null)
            {
                result.BuffApplied = true;
                result.BuffDescription = $"Debuff: Shaken for {SpellDurationRules.DescribeRounds(shakenRounds)} (successful Will save reduces Scare).";
            }

            CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("😰", $"{targetName} resists the worst of Scare and is shaken for {SpellDurationRules.DescribeRounds(shakenRounds)} (-2 to attacks, saves, checks)."));
            return true;
        }

        // Failed save: frightened for 1 round/level, the cast's duration (SpellDurationRules.Rounds on the cast's clone, so
        // Extend doubles it; batch 11 step 19, SPL-038)
        int frightenedRounds = SpellDurationRules.Rounds(spell, casterLevel);
        ScareEffectData frightenedEffect = ScareEffectData.CreateFrightened(casterLevel, caster, frightenedRounds);
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

        CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("😱", $"{targetName} fails Will save - Frightened for {frightenedRounds} rounds! Must flee from {casterName}. (-2 to attacks, saves, checks)"));
        Debug.Log($"[GameManager] Scare: {targetName} frightened for {frightenedRounds} rounds by {casterName}");
        return true;
    }

    // ======================== FEAR SPELL HANDLER (Cone AoE) ========================

    /// <summary>Delegates to <see cref="SpellUtilities.IsFearSpell"/>.</summary>
    private static bool IsFearSpell(SpellData spell)
    {
        return SpellUtilities.IsFearSpell(spell);
    }

    /// <summary>
    /// Resolve the Fear spell (PHB p.229) as a cone AoE.
    /// All living creatures in the 30-ft cone: Will partial.
    /// Failed save → Panicked for 1 round/level.
    /// Successful save → Shaken for 1 round.
    /// Returns the cast's log (null for no caster or spell); since batch 11 step 9 the cast core shows it, refreshes the
    /// UI, runs the combat-end check and paces the turn (FinishAreaCast), and the area handler registry calls this.
    /// </summary>
    private string ResolveFearSpell(CastContext ctx, CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        if (ctx == null || caster == null || caster.Stats == null || ctx.Request.Spell == null)
            return null;

        int casterLevel = SpellCastingHelper.GetBaseCasterLevel(caster);
        int saveDc = GetSpellSaveDC(caster, ctx.Request.Spell);
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
            if (!SpellTargetingService.IsLivingCreature(target))
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

            // The one spell resistance check and the cast's save (batch 11 step 18; SPL-122, SPL-018).
            if (ctx.Request.Spell.SpellResistanceApplies)
            {
                SRResult sr = ctx.RollSpellResistance(target, "Fear SR check");
                if (!sr.Overcame)
                {
                    logBuilder.AppendLine($"  • {targetName}: SR blocks Fear ({FormatAreaSpellResistanceCheck(sr)}).");
                    immuneCount++;
                    continue;
                }
            }

            SaveResult save = ctx.RollSave(target, SaveType.Will, saveDc, "Fear Will save");
            int saveRoll = save.Roll;
            int fearWillMod = save.Modifier;
            int saveTotal = save.Total;
            bool saveSuccess = save.Saved;

            if (saveSuccess)
            {
                // Successful save: Shaken for 1 round, doubled by Extend (PHB p.94; batch 11 step 19, SPL-038)
                int fearShakenRounds = SpellDurationRules.Extended(ctx.Request.Spell, 1);
                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        target,
                        CombatConditionType.Shaken,
                        fearShakenRounds,
                        source: caster,
                        sourceNameOverride: ctx.Request.Spell.Name,
                        sourceCategory: "Spell",
                        sourceId: ctx.Request.Spell.SpellId);
                }
                else
                {
                    target.ApplyCondition(CombatConditionType.Shaken, fearShakenRounds, casterName);
                }

                shakenCount++;
                logBuilder.AppendLine($"  • {targetName}: Will d20({saveRoll}) + {fearWillMod} = {saveTotal} ≥ DC {saveDc} — Shaken for {SpellDurationRules.DescribeRounds(fearShakenRounds)}.");
            }
            else
            {
                // Failed save: Panicked for 1 round/level
                int panickedRounds = SpellDurationRules.Rounds(ctx.Request.Spell, casterLevel); // the cast's duration, Extend included (batch 11 step 19)

                var fearData = new FrightenedConditionData
                {
                    Caster = caster,
                    CasterName = casterName,
                    RemainingRounds = panickedRounds,
                    SourceSpellId = ctx.Request.Spell.SpellId,
                    SourceEffectName = ctx.Request.Spell.Name
                };

                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        target,
                        CombatConditionType.Panicked,
                        panickedRounds,
                        source: caster,
                        data: fearData,
                        sourceNameOverride: ctx.Request.Spell.Name,
                        sourceCategory: "Spell",
                        sourceId: ctx.Request.Spell.SpellId);
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
                logBuilder.AppendLine($"  • {targetName}: Will d20({saveRoll}) + {fearWillMod} = {saveTotal} < DC {saveDc} — PANICKED for {panickedRounds} rounds!");
            }
        }

        logBuilder.AppendLine();
        logBuilder.AppendLine($"Result: {panickedCount} panicked, {shakenCount} shaken, {immuneCount} immune/resisted");
        logBuilder.AppendLine("═══════════════════════════════════");

        // The cast core shows the log, refreshes the UI, runs the combat-end check and paces the turn (FinishAreaCast).
        return logBuilder.ToString();
    }

    private static bool IsRayOfEnfeeblementSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.RAY_OF_ENFEEBLEMENT, StringComparison.Ordinal);
    }

    private static int CalculateRayOfEnfeeblementPenalty(CharacterController caster)
        => RollRayOfEnfeeblementPenalty(caster, null);

    /// <summary>The Strength penalty, 1d6 + 1 per two caster levels (max +5), with the cast's metamagic (<paramref name="castSpell"/>'s stamp).</summary>
    private static int RollRayOfEnfeeblementPenalty(CharacterController caster, SpellData castSpell)
    {
        // The cast's caster level (CasterLevelRules: a scroll's or wand's own, the casting class's; SPL-016, SPL-017).
        int casterLevel = CasterLevelRules.ForCaster(caster, SpellDatabase.GetSpell(SpellNames.RAY_OF_ENFEEBLEMENT));
        int levelBonus = Mathf.Min(5, 1 + ((casterLevel - 1) / 2));
        // 1d6 + the level bonus, a variable effect the cast's Empower and Maximize change (PHB p.93, p.97; SpellDiceRules,
        // batch 11 step 19, SPL-038).
        return SpellDiceRules.RollFor(castSpell, SpellDice.Of(1, 6, levelBonus), "Enervation damage");
    }

    private bool TryResolveRayOfEnfeeblementSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsRayOfEnfeeblementSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null || !result.Success || (result.RequiredAttackRoll && !result.AttackHit))
            return false;

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveRayOfEnfeeblementSpellEffect));

        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, CasterLevelRules.ForCaster(caster, spell)));
        int penalty = RollRayOfEnfeeblementPenalty(caster, spell);

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
            CombatUI?.ShowCombatLog(CombatLogHelper.Success("♻", $"Previous enfeeblement replaced on {target.Stats.CharacterName}."));
            CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{target.Stats.CharacterName}'s enfeeblement is replaced by {article} {replacementDescriptor} effect (old STR -{previousPenalty} → new STR -{penalty})."));
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{target.Stats.CharacterName} is enfeebled by {casterName}: STR -{penalty} for {durationRounds} rounds (STR now {resultingStrength})."));
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

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveTouchOfIdiocySpellEffect));

        int casterLevel = CasterLevelRules.ForCaster(caster, spell);
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);

        // 1d6 to each score, each a variable effect the cast's Empower and Maximize change (PHB p.93, p.97; batch 11 step 19)
        int intDamage = SpellDiceRules.RollFor(spell, SpellDice.Of(1, 6), "Touch of Idiocy INT damage");
        int wisDamage = SpellDiceRules.RollFor(spell, SpellDice.Of(1, 6), "Touch of Idiocy WIS damage");
        int chaDamage = SpellDiceRules.RollFor(spell, SpellDice.Of(1, 6), "Touch of Idiocy CHA damage");

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

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🧠", $"{target.Stats.CharacterName} is touched by idiocy for {durationRounds} rounds."));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   Ability damage: INT 1d6={intDamage}, WIS 1d6={wisDamage}, CHA 1d6={chaDamage}"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   INT {intBefore}→{intAfter}, WIS {wisBefore}→{wisAfter}, CHA {chaBefore}→{chaAfter}"));

        return true;
    }

    private static bool IsMelfsAcidArrowSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.MELFS_ACID_ARROW, StringComparison.Ordinal);
    }

    private static int CalculateMelfsAcidArrowAdditionalRounds(CharacterController caster)
    {
        // The cast's caster level (CasterLevelRules: a scroll's or wand's own, the casting class's, or the Hit Dice of a
        // creature with no casting class, MM p.315; SPL-016, SPL-017).
        int casterLevel = CasterLevelRules.ForCaster(caster, SpellDatabase.GetSpell(SpellNames.MELFS_ACID_ARROW));

        // D&D 3.5e: total rounds = 1 + floor(CL / 3), max total 7 rounds at CL 18.
        return Mathf.Min(6, Mathf.Max(0, casterLevel / 3));
    }

    private bool TryResolveMelfsAcidArrowSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsMelfsAcidArrowSpell(spell) || target == null || target.Stats == null)
            return false;

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveMelfsAcidArrowSpellEffect));

        if (result == null)
            return true;

        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            target.ClearMelfsAcidArrowEffect();
            return true;
        }

        int initialDamage = Mathf.Max(0, result.DamageDealt);
        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🧪", $"Melf's Acid Arrow hits for {initialDamage} damage"));

        // The duration is 1 round + 1 round per three levels; Extend doubles the whole of it (PHB p.94, p.253), and the
        // later rounds' 2d4 take the cast's Empower and Maximize (batch 11 step 19, SPL-038).
        int lingeringRounds = SpellDurationRules.Extended(spell, CalculateMelfsAcidArrowAdditionalRounds(caster) + 1) - 1;
        if (lingeringRounds > 0)
            target.ApplyMelfsAcidArrowEffect(lingeringRounds, caster, SpellDiceRules.MetamagicOf(spell));
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
        // The arrow's 2d4 with its cast's metamagic (SpellDiceRules; batch 11 step 19, SPL-038). The dice keep their old label.
        int rolledDamage = SpellDiceRules.Roll(SpellDice.Of(diceCount, diceSides), effectData.Metamagic, "Spectral Hand damage die");

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

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🧪", $"Acid continues to burn for {damageDealt} damage ({roundsLeft} rounds left)"));

        if (character.Stats.IsDead)
        {
            character.OnDeath();
            HandleSummonDeathCleanup(character);
        }

        if (roundsLeft <= 0)
        {
            character.ClearMelfsAcidArrowEffect();
            CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", "Acid effect expires"));
        }
    }

    /// <summary>
    /// The shared effect step of a landed cast: the PC pipeline (PerformSpellCast), the NPC cast executor
    /// (TryNPCPerformSpellCast), the area pipeline for each target, and the scenario harness. Spell-specific branches
    /// come first, dispatched by SpellId; the generic StatusEffectManager branch at the end handles every other spell
    /// and returns, so a new special case goes above it (SPL-037). Which casts reach this method: SpellEffectRouting.
    /// <paramref name="castResult"/> is the SpellCaster.Cast result of a single-target cast (null for area casts and
    /// the harness): a handler whose attack roll, spell resistance or save the cast already resolved reads it instead
    /// of rolling again (Disintegrate, Heal on an undead, Plane Shift, Telekinesis).
    /// </summary>
    private ActiveSpellEffect ApplySpellBuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp, SpellResult castResult = null)
    {
        if (spell != null && (spell.SpellId == SpellNames.DAZE || spell.SpellId == SpellNames.DAZE_MONSTER))
        {
            int dazeRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, CasterLevelRules.ForCaster(caster, spell)));
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

            CombatUI?.ShowCombatLog(CombatLogHelper.Buff("💫", $"{target.Stats.CharacterName} is dazed for {dazeRounds} round(s)!"));
            Debug.Log($"[GameManager] {spell.Name} applied Dazed to {target.Stats.CharacterName} for {dazeRounds} round(s)");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.HIDEOUS_LAUGHTER)
        {
            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
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

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"🤣 HAHAHA! {target.Stats.CharacterName} collapses in hideous laughter and falls prone for {laughterRounds} round(s)!"));
            Debug.Log($"[GameManager] Tasha's Hideous Laughter applied to {target.Stats.CharacterName} for {laughterRounds} rounds");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.FLARE)
        {
            // PHB p.232: Flare is instantaneous; the dazzled condition it causes lasts 1 minute.
            int dazzledRounds = SpellDurationRules.RoundsPerMinute;
            string sourceName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
            target.ApplyCondition(CombatConditionType.Dazzled, dazzledRounds, sourceName);
            CombatUI?.ShowCombatLog(CombatLogHelper.Buff("✨", $"{target.Stats.CharacterName} is dazzled (-1 attack, Spot, and Search) for {dazzledRounds} round(s)!"));
            Debug.Log($"[GameManager] Flare applied Dazzled to {target.Stats.CharacterName} for {dazzledRounds} round(s)");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.HOLD_PERSON)
        {
            return ApplyHoldPersonBuff(caster, target, spell, spellComp);
        }

        // Hold Monster — as Hold Person but affects any living creature (Phase 1)
        if (spell != null && spell.SpellId == SpellNames.HOLD_MONSTER)
        {
            return ApplyHoldMonsterEffect(caster, target, spell, spellComp);
        }

        if (spell != null && spell.SpellId == SpellNames.RAGE)
        {
            return ApplyRageSpellBuff(caster, target, spell, spellComp);
        }

        // Prayer (PHB p.264): the area cast path calls this once per creature in the burst; each gets its share, the
        // caster and allies a +1 luck bonus, foes a -1 penalty (SPL-042).
        if (spell != null && spell.SpellId == SpellNames.PRAYER)
        {
            CharacterController prayerCaster = caster ?? target;
            int prayerCasterLevel = SpellCastingHelper.GetEffectiveCasterLevel(prayerCaster, spell);
            int prayerRounds = SpellCastingHelper.CalculateDuration(spell, prayerCasterLevel);
            ApplyPrayerToCreature(prayerCaster, target, spell, prayerCasterLevel, prayerRounds, out ActiveSpellEffect prayerEffect);
            return prayerEffect;
        }

        if (spell != null && spell.SpellId == SpellNames.BLINDNESS_DEAFNESS)
        {
            int casterLevel = CasterLevelRules.ForCaster(caster, spell); // the cast's caster level, not the character level (SPL-017)

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
                CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🔲", $"{target.Stats.CharacterName} is blinded by {spell.Name}!"));
                Debug.Log($"[GameManager] {spell.Name} applied Blindness to {target.Stats.CharacterName} (permanent)");
            }
            else
            {
                var deafEffect = BlindnessDeafnessEffectData.CreateSpellDeafness(spell.SpellId, caster, casterLevel);
                target.ApplyDeafnessEffect(deafEffect);
                CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🔔", $"{target.Stats.CharacterName} is deafened by {spell.Name}!"));
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{spell.Name} has no effect — {target.Stats.CharacterName} is not undead."));
                Debug.Log($"[GameManager] {spell.Name} failed: {target.Stats.CharacterName} is not undead (CreatureType={target.Stats.CreatureType})");
                return null;
            }

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
            bool isIntelligent = target.IsIntelligentUndead();
            // 1 day/level from the cast's clone, so an extended cast lasts 2 days/level (PHB p.94, p.211; batch 11 step 19).
            int commandRounds = SpellDurationRules.Rounds(spell, casterLevel);
            int commandDays = Mathf.Max(1, commandRounds / SpellDurationRules.RoundsPerDay);

            CommandUndeadEffectData effectData;
            if (isIntelligent)
            {
                effectData = CommandUndeadEffectData.CreateForIntelligent(caster, target, casterLevel, commandRounds);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("💀", $"{target.Stats.CharacterName} is now commanded by {caster.Stats.CharacterName}! (intelligent undead — Friendly attitude, {commandDays} day(s))"));
            }
            else
            {
                effectData = CommandUndeadEffectData.CreateForNonintelligent(caster, target, casterLevel, commandRounds);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("💀", $"{target.Stats.CharacterName} is now commanded by {caster.Stats.CharacterName}! (mindless undead — full obedience, {commandDays} day(s))"));
            }

            target.ApplyCommandUndeadEffect(effectData);

            Debug.Log($"[GameManager] Command Undead applied: {target.Stats.CharacterName} commanded by {caster.Stats.CharacterName} " +
                      $"(intelligent={isIntelligent}, duration={effectData.DurationRemainingRounds} rounds, casterLevel={casterLevel})");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.CONFUSION)
        {
            // PHB p.212: 1 round/level (SPL-002; the legacy field had fixed it at 4 rounds).
            int confusionRounds = Mathf.Max(1, SpellDurationRules.Rounds(spell, SpellDurationRules.CasterLevelFor(caster, spell)));
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

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🌀", $"{target.Stats.CharacterName} is confused for {confusionRounds} round(s)!"));
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.CHARM_PERSON)
        {
            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
            int charmRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);
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

            CombatUI?.ShowCombatLog(CombatLogHelper.Buff("💞", $"{target.Stats.CharacterName} is charmed by {charmData.CasterName} for {charmRounds} round(s)!"));
            return null;
        }

        // Charm Monster — as Charm Person but affects any living creature (Phase 1)
        if (spell != null && spell.SpellId == SpellNames.CHARM_MONSTER)
        {
            return ApplyCharmMonsterEffect(caster, target, spell, spellComp);
        }

        // ── Dominate Person (PHB p.224) ──
        // Enchantment (Compulsion) [Mind-Affecting]. Control one humanoid via telepathic link.
        // Uses CommandUndeadEffectData for side-switching (same pattern as Dominate Animal).
        if (spell != null && spell.SpellId == SpellNames.DOMINATE_PERSON)
        {
            if (target == null || target.Stats == null)
                return null;

            // Dominate Person only works on humanoids (PHB p.224)
            if (!SpellTargetingService.IsHumanoid(target))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🧠", $"{caster.Stats.CharacterName} casts Dominate Person on {target.Stats.CharacterName} — no effect (not a humanoid)!"));
                return null;
            }

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
            int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);

            // Apply domination using the command/dominate side-switching pattern
            var effectData = new CommandUndeadEffectData
            {
                IsActive = true,
                IsIntelligent = true, // Humanoids are intelligent — they get a new save if ordered to act against nature
                DurationRemainingRounds = durationRounds,
                CasterLevel = casterLevel,
                SourceSpellId = SpellNames.DOMINATE_PERSON,
                SourceName = "Dominate Person"
            };
            effectData.SetCaster(caster);
            effectData.SetControlledUndead(target); // Reuse field for "controlled creature"

            target.ApplyCommandUndeadEffect(effectData);

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🧠", $"{caster.Stats.CharacterName} dominates {target.Stats.CharacterName}!"));
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   The target is under {caster.Stats.CharacterName}'s telepathic control for {durationRounds} round(s)."));

            Debug.Log($"[GameManager] Dominate Person: {target.Stats.CharacterName} controlled by {caster.Stats.CharacterName} for {durationRounds} rounds");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.ENERVATION)
        {
            string sourceName = spell.Name;
            int negativeLevels = SpellDiceRules.RollFor(spell, SpellDice.Of(1, 4), "Enervation negative levels 1d4"); // the cast's metamagic (batch 11 step 19)
            int total = NegativeLevelSystem.ApplyNegativeLevels(target, negativeLevels, sourceName);
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("☠", $"{target.Stats.CharacterName} gains {negativeLevels} negative level(s) from Enervation (total {total})."));
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.FLESH_TO_STONE)
        {
            string sourceName = spell.Name;
            // PHB p.232: instantaneous; the petrification lasts until Stone to Flesh (a permanent condition).
            int rounds = SpellDurationRules.PermanentRounds;
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

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪨", $"{target.Stats.CharacterName} is petrified!"));
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.STONE_TO_FLESH)
        {
            bool removed = target.RemoveCondition(CombatConditionType.Petrified);
            if (removed)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("✨", $"{target.Stats.CharacterName} returns to flesh."));
            return null;
        }

        // Restoration (Clr 4) and Greater Restoration (Clr 7), PHB p.272: both function like lesser restoration, so they
        // dispel magical effects that reduce ability scores (restoration one, greater restoration all of them); negative
        // levels dispelled, all temporary ability damage cured, fatigue and exhaustion ended; greater restoration also ends
        // confusion. Restoring drained levels and drained ability points is not modelled (SPL-131).
        if (spell != null && (spell.SpellId == SpellNames.RESTORATION || spell.SpellId == SpellNames.GREATER_RESTORATION))
        {
            int removed = NegativeLevelSystem.RemoveNegativeLevels(target, int.MaxValue, spell.Name);
            // Before the ability damage cure: ending Touch of Idiocy heals the ability damage it is modelled as.
            List<string> penaltiesDispelled = DispelAbilityPenaltyEffects(target, spell.SpellId == SpellNames.GREATER_RESTORATION);
            int abilityHealed = target.Stats.HealAllAbilityDamage(999);
            var ended = new List<string>();
            foreach (string penalty in penaltiesDispelled)
                ended.Add(penalty);
            if (target.RemoveCondition(CombatConditionType.Exhausted)) ended.Add("exhaustion");
            if (target.RemoveCondition(CombatConditionType.Fatigued)) ended.Add("fatigue");
            if (spell.SpellId == SpellNames.GREATER_RESTORATION && target.RemoveCondition(CombatConditionType.Confused)) ended.Add("confusion");
            string extra = (abilityHealed > 0 ? $", {abilityHealed} point(s) of ability damage cured" : "")
                + (ended.Count > 0 ? $", {string.Join(" and ", ended)} ended" : "");
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("✨", $"{target.Stats.CharacterName} recovers {removed} negative level(s){extra}."));
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.SLEEP)
        {
            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
            int sleepRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
            int wakeDc = GetSpellSaveDC(caster, spell);

            ApplySleepState(caster, target, sleepRounds, wakeDc, spell);

            CombatUI?.ShowCombatLog(CombatLogHelper.StatusEnd($"💤 {target.Stats.CharacterName} falls asleep for {sleepRounds} round(s)!"));
            Debug.Log($"[GameManager] Sleep applied Asleep/Unconscious to {target.Stats.CharacterName} for {sleepRounds} rounds");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.DEEP_SLUMBER)
        {
            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
            int sleepRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
            int wakeDc = GetSpellSaveDC(caster, spell);

            ApplySleepState(caster, target, sleepRounds, wakeDc, spell);

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("💤", $"{target.Stats.CharacterName} falls into deep slumber for {sleepRounds} round(s)!"));
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

            CombatUI?.ShowCombatLog(CombatLogHelper.Buff("💫", $"{target.Stats.CharacterName} is stunned by {spell.Name} for {stunRounds} round(s)!"));
            Debug.Log($"[GameManager] {spell.Name} applied Stunned to {target.Stats.CharacterName} for {stunRounds} rounds");
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.TOUCH_OF_FATIGUE)
        {
            string sourceName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
            int casterLevel = CasterLevelRules.ForCaster(caster, spell);

            bool wasExhausted = target.HasCondition(CombatConditionType.Exhausted);
            if (wasExhausted)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Buff("💤", $"{target.Stats.CharacterName} is already exhausted. Touch of Fatigue has no effect."));
                Debug.Log($"[GameManager] Touch of Fatigue had no effect on {target.Stats.CharacterName} (already exhausted)");
                return null;
            }

            bool wasFatigued = target.HasCondition(CombatConditionType.Fatigued);
            target.ApplyCondition(CombatConditionType.Fatigued, casterLevel, sourceName);

            bool isNowExhausted = target.HasCondition(CombatConditionType.Exhausted);
            if (wasFatigued && isNowExhausted)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🥵", $"{target.Stats.CharacterName} becomes exhausted for {casterLevel} round(s)!"));
                Debug.Log($"[GameManager] Touch of Fatigue escalated {target.Stats.CharacterName} to Exhausted for {casterLevel} rounds");
            }
            else
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Buff("😫", $"{target.Stats.CharacterName} is fatigued for {casterLevel} round(s)!"));
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("⚠", $"True Strike is personal and can only target the caster."));
                Debug.Log("[GameManager] True Strike attempted on non-caster target; ignoring target and applying to caster only.");
            }

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            recipientStatusMgr?.RemoveEffectsBySpellId(SpellNames.TRUE_STRIKE);

            TrueStrikeEffect existing = recipient.GetComponent<TrueStrikeEffect>();
            if (existing != null)
                Destroy(existing);

            TrueStrikeEffect effect = recipient.gameObject.AddComponent<TrueStrikeEffect>();
            effect.Initialize(recipient, this, CurrentRound);

            string casterName = recipient.Stats != null ? recipient.Stats.CharacterName : "Unknown";
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("✨", $"{casterName} casts True Strike (+20 insight on next attack, ignores concealment)."));
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.DISGUISE_SELF)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
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

            int casterLevel = CasterLevelRules.ForCaster(caster, spell); // the cast's caster level, not the character level (SPL-017)
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                recipient.ApplyDisguiseSelfEffect(selectedRace, effect.RemainingRounds, caster);

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🎭", $"{recipient.Stats.CharacterName} now appears as {recipient.DisplayedRace} ({effect.GetDurationDisplayString()})."));
            }

            UpdateAllStatsUI();
            return effect;
        }

        if (spell != null && spell.SpellId == SpellNames.EXPEDITIOUS_RETREAT)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = CasterLevelRules.ForCaster(caster, spell); // the cast's caster level, not the character level (SPL-017)
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                recipient.ApplyExpeditiousRetreatEffect(effect.AppliedSpeedBonusFeet, effect.RemainingRounds, caster);

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("💨", $"{recipient.Stats.CharacterName}'s land speed increases by +{effect.AppliedSpeedBonusFeet} ft ({effect.GetDurationDisplayString()})."));
            }

            UpdateAllStatsUI();
            return effect;
        }

        if (spell != null && spell.SpellId == SpellNames.SEE_INVISIBLE)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            // The duration's caster level is the cast's caster level, not the character level (SPL-002, SPL-017).
            int casterLevel = SpellDurationRules.CasterLevelFor(caster, spell);
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                recipient.ApplySeeInvisibilityEffect(effect.RemainingRounds, recipient);

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("👁", $"{recipient.Stats.CharacterName} can now see invisible creatures ({effect.GetDurationDisplayString()})."));
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

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = CasterLevelRules.ForCaster(caster, spell); // the cast's caster level, not the character level (SPL-017)
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                recipient.ApplyInvisibilityEffect(effect.RemainingRounds, caster, isMoving: false);

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                UpdateEnemyLastKnownPositionForInvisibility(recipient);
                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("👁", $"{recipient.Stats.CharacterName} becomes invisible ({effect.GetDurationDisplayString()})."));
            }

            UpdateAllStatsUI();
            return effect;
        }

        if (spell != null && spell.SpellId == SpellNames.BLUR)
        {
            CharacterController recipient = target ?? caster;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = CasterLevelRules.ForCaster(caster, spell); // the cast's caster level, not the character level (SPL-017)
            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name, casterLevel);
            if (effect != null)
            {
                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
                bool selfCast = recipient == caster;
                string castLine = selfCast
                    ? $"<color=#88FFEE>🌫 {casterName} casts Blur on self!</color>"
                    : $"<color=#88FFEE>🌫 {casterName} casts Blur on {recipient.Stats.CharacterName}!</color>";

                CombatUI?.ShowCombatLog(castLine);
                CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("", $"   {recipient.Stats.CharacterName}'s outline becomes blurred and indistinct."));
                CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("", $"   Attacks against {recipient.Stats.CharacterName} have 20% miss chance ({effect.GetDurationDisplayString()}, {Mathf.Max(0, effect.RemainingRounds)} rounds)."));
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

            // The energy type is the cast's own choice: the PC prompt or the AI request build (AiCastDecider) filled it
            // (batch 11 step 4); this rule no longer asks who controls the caster.
            if (!_pendingResistEnergyType.HasValue)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Resist Energy failed: no energy type selected."));
                return null;
            }

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
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
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🛡", $"{recipient.Stats.CharacterName} gains Resist Energy ({energyLabel} {resistance}) for {Mathf.Max(0, durationRounds)} rounds."));
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

            // The energy type is the cast's own choice: the PC prompt or the AI request build (AiCastDecider) filled it
            // (batch 11 step 4); this rule no longer asks who controls the caster.
            if (!_pendingProtectionFromEnergyType.HasValue)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Protection from Energy failed: no energy type selected."));
                return null;
            }

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
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
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🛡", $"{recipient.Stats.CharacterName} gains Protection from Energy ({protEnergyLabel}, {absorptionPool} pts) for {Mathf.Max(0, durationRoundsProtEnergy)} rounds (CL {casterLevel})."));
            UpdateAllStatsUI();
            return null;
        }

        if (spell != null && spell.SpellId == SpellNames.PROTECTION_FROM_ARROWS)
        {
            CharacterController recipient = target ?? caster;
            if (recipient == null || recipient.Stats == null)
                return null;

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = CasterLevelRules.ForCaster(caster, spell); // the cast's caster level, not the character level (SPL-017)
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

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🛡", $"{recipient.Stats.CharacterName} gains Protection from Arrows (DR {drAmount}/magic vs ranged weapons, {maxPool} absorption pool)."));
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

            // The 250 gp of diamond dust (and the granite, in the pouch) are tested at the cast core's Legality stage
            // and the dust spent with the slot, for every caster (SpellComponentRules; batch 11 step 17, SPL-020).
            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
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

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🪨", $"{recipient.Stats.CharacterName} gains Stoneskin (DR 10/adamantine, {maxPool} absorption pool, CL {casterLevel})."));
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

            int casterLevel = CasterLevelRules.ForCaster(recipient, spell); // the cast's caster level (SPL-016, SPL-017)
            int intMod = recipient.Stats.INTMod;

            // Roll 1d4 for HP loss / hand HP. A price the caster pays, not a variable effect of the spell, so Empower and
            // Maximize leave it (delegated RAW reading 2026-10-14, batch 11 step 19; PHB p.93 raises what the spell does).
            int handHP = SpectralHandEffectData.RollHandHP();

            // Check if caster has enough HP (must have at least 1 HP after loss)
            if (recipient.Stats.CurrentHP <= handHP)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("👻", $"{recipient.Stats.CharacterName} does not have enough HP to create Spectral Hand (needs >{handHP} HP)."));
                return null;
            }

            // Create the effect data
            SpectralHandEffectData spectralHandEffect = SpectralHandEffectData.CreateWithHP(handHP, casterLevel, intMod, recipient,
                SpellDurationRules.Rounds(spell, casterLevel)); // the cast's duration, Extend included (batch 11 step 19)

            // Track via StatusEffectManager for duration and dispel
            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                // Apply the effect (handles HP loss)
                recipient.ApplySpectralHandEffect(spectralHandEffect);

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.Success("👻", $"{recipient.Stats.CharacterName} casts Spectral Hand — loses {handHP} HP. " +
                                        $"Hand HP: {handHP}, AC: {spectralHandEffect.HandAC} " +
                                        $"(22 + Int {intMod:+#;-#;+0}) [{effect.GetDurationDisplayString()}]"));
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

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
            int targetHD = target.Stats.GetHitDice();

            // Create the attribute enhancement effect data
            AttributeEnhancementEffectData enhancementEffect = AttributeEnhancementEffectData.Create(
                spell.SpellId, casterLevel, targetHD, caster,
                SpellDurationRules.Rounds(spell, casterLevel)); // the cast's duration, Extend included (batch 11 step 19)

            // Track via StatusEffectManager for duration and dispel (handles stat bonus apply/reverse)
            StatusEffectManager targetStatusMgr = target.StatusEffectManager;
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
                SpellcastingComponent targetSpellComp = target.Spellcasting;
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                // Build log message
                string abilityName = enhancementEffect.AbilityName;
                string spellName = enhancementEffect.SourceName;
                string hpNote = enhancementEffect.IsBearsEndurance && enhancementEffect.GrantedBonusHP > 0
                    ? $", +{enhancementEffect.GrantedBonusHP} HP ({targetHD} HD × 2)"
                    : "";
                string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

                CombatUI?.ShowCombatLog(CombatLogHelper.Success("💪", $"{casterName} casts {spellName} on {target.Stats.CharacterName} — " +
                    $"+{enhancementEffect.BonusAmount} enhancement bonus to {abilityName}{hpNote} " +
                    $"[{effect.GetDurationDisplayString()}]"));
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

            int casterLevel = CasterLevelRules.ForCaster(recipient, spell); // the cast's caster level (SPL-016, SPL-017)

            // Roll temp HP: 1d10 + min(CL, 10)
            FalseLifeEffectData falseLifeEffect = FalseLifeEffectData.CreateFalseLife(casterLevel, recipient, spell); // the cast's metamagic (batch 11 step 19)

            // Track via StatusEffectManager for duration and dispel
            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                // Apply the rolled temp HP via CharacterController (handles non-stacking)
                recipient.ApplyFalseLifeEffect(falseLifeEffect);

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.Success("💀", $"{recipient.Stats.CharacterName} casts False Life — gains {falseLifeEffect.CurrentTempHP} temporary HP " +
                                        $"(1d10+{Mathf.Min(casterLevel, 10)}) [{effect.GetDurationDisplayString()}]"));
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

            int casterLevel = CasterLevelRules.ForCaster(recipient, spell); // the cast's caster level (SPL-016, SPL-017)
            int saveDC = GetSpellSaveDC(caster ?? recipient, spell);

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                recipient.Stats.SanctuaryActive = true;
                recipient.Stats.SanctuaryDC = saveDC;
                recipient.Stats.SanctuaryCasterLevel = casterLevel;

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🛡", $"️ {recipient.Stats.CharacterName} casts Sanctuary — enemies must make Will save DC {saveDC} to attack [{effect.GetDurationDisplayString()}]"));
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

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);
            int saveDC = GetSpellSaveDC(caster, spell);

            StatusEffectManager targetStatusMgr = target.StatusEffectManager;
            if (targetStatusMgr == null)
                targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);

            ActiveSpellEffect effect = targetStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown", casterLevel);
            if (effect != null)
            {
                target.Stats.HideFromUndeadActive = true;
                target.Stats.HideFromUndeadDC = saveDC;
                target.Stats.HideFromUndeadCasterLevel = casterLevel;

                SpellcastingComponent targetSpellComp = target.Spellcasting;
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"👻 {target.Stats.CharacterName} is hidden from undead — mindless undead auto-fail, intelligent undead Will DC {saveDC} [{effect.GetDurationDisplayString()}]"));
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

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);

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
            StatusEffectManager targetStatusMgr = target.StatusEffectManager;
            if (targetStatusMgr == null)
                targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);

            ActiveSpellEffect effect = targetStatusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown", casterLevel);
            if (effect != null)
            {
                target.Stats.RemoveFearMoraleBonus = 4;

                SpellcastingComponent targetSpellComp = target.Spellcasting;
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                string fearStatus = removedFear ? "Fear removed! " : "";
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("✨", $"{fearStatus}{target.Stats.CharacterName} gains +4 morale bonus vs fear [{effect.GetDurationDisplayString()}]"));
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

            int casterLevel = CasterLevelRules.ForCaster(recipient, spell); // the cast's caster level (SPL-016, SPL-017)

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                recipient.Stats.EntropicShieldActive = true;
                recipient.Stats.EntropicShieldCasterLevel = casterLevel;

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🛡", $"️ {recipient.Stats.CharacterName} casts Entropic Shield — ranged attacks have 20% miss chance (CL {casterLevel}) [{effect.GetDurationDisplayString()}]"));
                Debug.Log($"[GameManager] Entropic Shield applied to {recipient.Stats.CharacterName}: CL {casterLevel}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== MAGIC STONE — D&D 3.5e PHB p.251 =====
        // Self buff: enchants up to 3 pebbles. +1 enhancement to attack, 1d6+1 damage, counts as magic.
        // Duration 30 minutes or until discharged.
        if (spell != null && spell.SpellId == SpellNames.MAGIC_STONE)
        {
            CharacterController recipient = caster ?? target;
            if (recipient == null || recipient.Stats == null)
                return null;

            int casterLevel = CasterLevelRules.ForCaster(recipient, spell); // the cast's caster level (SPL-016, SPL-017)

            StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
            if (recipientStatusMgr == null)
                recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            recipientStatusMgr.Init(recipient.Stats);

            ActiveSpellEffect effect = recipientStatusMgr.AddEffect(spell, recipient.Stats.CharacterName, casterLevel);
            if (effect != null)
            {
                recipient.Stats.MagicStoneActive = true;
                recipient.Stats.MagicStoneCharges = 3;
                recipient.Stats.MagicStoneCasterLevel = casterLevel;

                SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
                if (recipientSpellComp != null)
                    recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪨", $"{recipient.Stats.CharacterName} casts Magic Stone — 3 pebbles enchanted (+1 atk, 1d6+1 dmg, magic) (CL {casterLevel}) [{effect.GetDurationDisplayString()}]"));
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

            int casterLevel = CasterLevelRules.ForCaster(caster, spell);

            // D&D 3.5e: +2 at CL 1-5, +3 at CL 6-11, +4 at CL 12-17, +5 at CL 18+
            int deflectionBonus = 2;
            if (casterLevel >= 18) deflectionBonus = 5;
            else if (casterLevel >= 12) deflectionBonus = 4;
            else if (casterLevel >= 6) deflectionBonus = 3;

            StatusEffectManager targetStatusMgr = target.StatusEffectManager;
            if (targetStatusMgr == null)
                targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);

            // Override the spell's deflection bonus with scaled value
            SpellData scaledSpell = spell;
            scaledSpell.BuffDeflectionBonus = deflectionBonus;

            int deflectionBefore = target.Stats.EffectiveDeflectionBonus;
            ActiveSpellEffect effect = targetStatusMgr.AddEffect(scaledSpell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown", casterLevel);
            if (effect != null)
            {
                // AddEffect already applied the bonus to DeflectionBonus (AppliedDeflectionBonus) and reverses it
                // on removal; this field only feeds the status indicator. A Ring of Protection is kept apart and
                // combined at read time (CharacterStats.EffectiveDeflectionBonus, ITM-001).
                target.Stats.ShieldOfFaithDeflectionBonus = targetStatusMgr.BestAppliedDeflection(SpellNames.SHIELD_OF_FAITH);

                SpellcastingComponent targetSpellComp = target.Spellcasting;
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                // Deflection bonuses do not stack (PHB p.171): say so when a ring or another deflection effect
                // is already as high, so the log does not claim an AC gain that did not happen.
                int deflectionGain = target.Stats.EffectiveDeflectionBonus - deflectionBefore;
                string stackingNote = deflectionGain >= deflectionBonus ? ""
                    : deflectionGain <= 0 ? $" (no AC gain: existing deflection +{deflectionBefore} is as high or higher)"
                    : $" (AC rises by only +{deflectionGain}: deflection bonuses do not stack)";
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🛡", $"️ {target.Stats.CharacterName} gains +{deflectionBonus} deflection bonus to AC from Shield of Faith (CL {casterLevel}){stackingNote} [{effect.GetDurationDisplayString()}]"));
                Debug.Log($"[GameManager] Shield of Faith applied to {target.Stats.CharacterName}: +{deflectionBonus} deflection, CL {casterLevel}");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== DISPEL MAGIC — D&D 3.5e PHB p.223 =====
        // Targeted dispel: a dispel check (1d20 + the cast's CL, max +10) against each ongoing spell on the target,
        // DC 11 + that spell's CL; every success dispels its spell. Auto-succeeds against the caster's own spells.
        if (spell != null && spell.SpellId == SpellNames.DISPEL_MAGIC)
        {
            // A creature inside a globe of invulnerability that excludes Dispel Magic: the targeted dispel can bring the
            // globe down (PHB p.236) but not reach the spells on the creature inside it, so it checks the globe alone
            // (delegated RAW reading 2026-10-14, SPL-141; the cast core's Globe stage lets it through).
            if (LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(spell, target, out LesserGlobeOfInvulnerabilityAreaEffect dispelledGlobe))
                PerformGlobeDispel(caster, target, dispelledGlobe, SpellDurationRules.CasterLevelFor(caster, spell));
            else
                PerformTargetedDispel(caster, target, SpellDurationRules.CasterLevelFor(caster, spell));
            return null; // Dispel Magic is instantaneous — no ongoing effect to track
        }

        // ── Break Enchantment (PHB p.207) ──
        // Frees victims from enchantments, transmutations and curses: a caster level check (1d20 + CL, max +15) against
        // each such effect on the subject, DC 11 + its caster level (DispelMagicService.PerformBreakEnchantment).
        if (spell != null && spell.SpellId == SpellNames.BREAK_ENCHANTMENT)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("✨", $"{caster.Stats.CharacterName} attempts to break enchantments on {target.Stats.CharacterName}!"));
            PerformBreakEnchantment(caster, target, SpellDurationRules.CasterLevelFor(caster, spell));
            return null; // Break Enchantment is instantaneous
        }

        // ===== PHASE 1 STAFF SINGLE-TARGET SPELLS =====

        // Telekinesis (Violent Thrust mode)
        if (spell != null && spell.SpellId == SpellNames.TELEKINESIS)
        {
            return ApplyTelekinesisEffect(caster, target, spell, spellComp, castResult);
        }

        // Continual Flame — permanent light
        if (spell != null && spell.SpellId == SpellNames.CONTINUAL_FLAME)
        {
            return ApplyContinualFlameEffect(caster, target, spell, spellComp);
        }

        // Levitate — vertical movement buff
        if (spell != null && spell.SpellId == SpellNames.LEVITATE)
        {
            return ApplyLevitateEffect(caster, target, spell, spellComp);
        }

        // Shrink Item — utility
        if (spell != null && spell.SpellId == SpellNames.SHRINK_ITEM)
        {
            return ApplyShrinkItemEffect(caster, target, spell, spellComp);
        }

        // Barkskin (Druid/Ranger) — natural armor enhancement
        if (spell != null && (spell.SpellId == SpellNames.BARKSKIN))
        {
            return ApplyBarkskinEffect(caster, target, spell, spellComp);
        }

        // Passwall — creates passage through walls
        if (spell != null && spell.SpellId == SpellNames.PASSWALL)
        {
            return ApplyPasswallEffect(caster, target, spell, spellComp);
        }

        // Globe of Invulnerability — self-targeting abjuration sphere
        if (spell != null && spell.SpellId == SpellNames.GLOBE_OF_INVULNERABILITY)
        {
            if (caster == null || caster.Stats == null)
                return null;

            // The tracked effect on the caster comes first and owns the spell's duration: the emanation is created only
            // when it lands (a recast while a globe as long is up adds nothing), lasts exactly as long, and ends with it
            // when it expires or is dispelled (StatusEffectManager.RemoveEffect -> EndGlobesOf; PHB p.223, p.236).
            StatusEffectManager globeStatusMgr = caster.StatusEffectManager;
            if (globeStatusMgr == null)
                globeStatusMgr = caster.gameObject.AddComponent<StatusEffectManager>();
            globeStatusMgr.Init(caster.Stats);
            ActiveSpellEffect globeEffect = globeStatusMgr.AddEffect(spell, caster.Stats.CharacterName, SpellDurationRules.CasterLevelFor(caster, spell));
            if (globeEffect == null)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🔮", $"{caster.Stats.CharacterName}'s Globe of Invulnerability is already up; the new casting adds nothing."));
                return null;
            }

            if (!TryResolveGlobeOfInvulnerabilitySpell(caster, spell, globeEffect.RemainingRounds, out string globeLog))
            {
                globeStatusMgr.RemoveEffect(globeEffect);
                return null;
            }
            CombatUI?.ShowCombatLog(globeLog);
            return globeEffect;
        }

        // ===== PHASE 2 & 3 SINGLE-TARGET SPELLS =====

        // Disintegrate — ranged touch ray, massive damage
        if (spell != null && spell.SpellId == SpellNames.DISINTEGRATE)
        {
            return ApplyDisintegrateEffect(caster, target, spell, spellComp, castResult);
        }

        // Protection from Spells — +8 resistance saves
        if (spell != null && spell.SpellId == SpellNames.PROTECTION_FROM_SPELLS)
        {
            return ApplyProtectionFromSpellsEffect(caster, target, spell, spellComp);
        }

        // Spell Turning — reflect spells
        if (spell != null && spell.SpellId == SpellNames.SPELL_TURNING)
        {
            return ApplySpellTurningEffect(caster, target, spell, spellComp);
        }

        // Heal — major healing + condition cure
        if (spell != null && spell.SpellId == SpellNames.HEAL)
        {
            return ApplyHealSpellEffect(caster, target, spell, spellComp, castResult);
        }

        // Resurrection — restore dead to life
        if (spell != null && spell.SpellId == SpellNames.RESURRECTION)
        {
            return ApplyResurrectionEffect(caster, target, spell, spellComp);
        }

        // True Seeing — see through illusions/invisibility
        if (spell != null && spell.SpellId == SpellNames.TRUE_SEEING)
        {
            return ApplyTrueSeeingEffect(caster, target, spell, spellComp);
        }

        // Mislead — Greater Invisibility + illusory double
        if (spell != null && spell.SpellId == SpellNames.MISLEAD)
        {
            return ApplyMisleadEffect(caster, target, spell, spellComp);
        }

        // ── Magic Fang (Phase 3) ──
        // +1 enhancement bonus to natural weapon attack and damage
        if (spell != null && spell.SpellId == SpellNames.MAGIC_FANG)
        {
            return ApplyMagicFangEffect(caster, target, spell, spellComp);
        }

        // ── Utility Cantrips (Phase 2) ──
        // Simple cantrips that always succeed with a log message.
        if (spell != null && TryApplyUtilityCantrip(caster, target, spell, spellComp, out ActiveSpellEffect cantripEffect))
        {
            return cantripEffect;
        }

        // Dancing Lights — utility cantrip
        if (spell != null && spell.SpellId == SpellNames.DANCING_LIGHTS)
        {
            return ApplyDancingLightsEffect(caster, target, spell, spellComp);
        }

        // Plane Shift — touch attack, Will save or removed from combat
        if (spell != null && spell.SpellId == SpellNames.PLANE_SHIFT)
        {
            return ApplyPlaneShiftEffect(caster, target, spell, spellComp, castResult);
        }

        // Alter Self — shape change buff
        if (spell != null && spell.SpellId == SpellNames.ALTER_SELF)
        {
            return ApplyAlterSelfEffect(caster, target, spell, spellComp);
        }

        // ===== GENERIC TRACKED EFFECT (every spell without a branch above) =====
        // This branch returns for every spell, so a spell-specific branch must sit above it (SPL-037). A spell whose
        // effect type is not Buff/Debuff/Control/Illusion/Wall reaches this method only when SpellEffectRouting lists it.
        var genericStatusMgr = target.StatusEffectManager;
        if (genericStatusMgr == null)
            genericStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        // Defensive rebind: some encounter presets reinitialize CharacterStats objects on existing
        // character GameObjects, so the manager must always point at the current stats instance.
        genericStatusMgr.Init(target.Stats);

        // The duration's caster level is the cast's caster level, not the character level (SPL-002, SPL-017).
        int genericCasterLevel = SpellDurationRules.CasterLevelFor(caster, spell);
        string genericCasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
        var genericEffect = genericStatusMgr.AddEffect(spell, genericCasterName, genericCasterLevel);

        if (genericEffect != null)
        {
            // Also track in SpellcastingComponent's ActiveBuffs for backward compat
            var targetSpellComp = target.Spellcasting;
            if (targetSpellComp != null)
            {
                targetSpellComp.ActiveBuffs[spell.SpellId] = genericEffect.RemainingRounds;
            }

            // ── Magic Circle area emanation registration ──
            if (AlignmentProtectionRules.IsMagicCircleSpell(spell.SpellId))
            {
                AlignmentProtectionRules.TryGetProtectionTypeForSpell(spell.SpellId, out AlignmentProtectionType mcWardType);
                var mcData = new MagicCircleEffectData
                {
                    WardedAlignment = mcWardType,
                    CenterCreature = target,
                    CasterLevel = genericCasterLevel,
                    RemainingRounds = genericEffect.RemainingRounds,
                    SourceSpellId = spell.SpellId,
                    CasterName = genericCasterName
                };
                RegisterMagicCircle(mcData);
                CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🔵", $"{spell.Name} emanation (10-ft radius) centered on {target.Stats.CharacterName}."));
            }

            string durStr = genericEffect.GetDurationDisplayString();
            bool isDebuff = spell.EffectType == SpellEffectType.Debuff || spell.EffectType == SpellEffectType.Control;
            string color = isDebuff ? "#FF8888" : "#88FF88";
            string effectLabel = isDebuff ? "debuff" : "buff";
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"<color={color}>✨ {spell.Name} {effectLabel} applied to {target.Stats.CharacterName} [{durStr}]</color>"));
            Debug.Log($"[GameManager] {spell.Name} {effectLabel} applied to {target.Stats.CharacterName} via StatusEffectManager: {genericEffect.GetDetailedString()}");
        }
        else
        {
            Debug.Log($"[GameManager] {spell.Name} effect NOT applied to {target.Stats.CharacterName} (stacking rules prevented it)");
        }

        UpdateAllStatsUI();
        return genericEffect;
    }

    /// <summary>
    /// Dispels the magical effects that penalize the target's ability scores (lesser restoration, PHB p.272, which
    /// restoration and greater restoration function like): Ray of Enfeeblement, Touch of Idiocy and tracked spell
    /// effects with a negative ability modifier. <paramref name="all"/> false ends one (restoration), true ends every
    /// one (greater restoration). Returns the names of the effects ended.
    /// </summary>
    internal static List<string> DispelAbilityPenaltyEffects(CharacterController target, bool all)
    {
        var ended = new List<string>();
        if (target == null || target.Stats == null)
            return ended;

        if (target.ActiveEnfeeblementEffect != null)
        {
            target.RemoveEnfeeblementEffect();
            ended.Add("Ray of Enfeeblement");
            if (!all) return ended;
        }

        if (target.ActiveTouchOfIdiocyEffect != null)
        {
            target.RemoveTouchOfIdiocyEffect();
            ended.Add("Touch of Idiocy");
            if (!all) return ended;
        }

        StatusEffectManager effects = target.StatusEffectManager;
        if (effects != null)
        {
            foreach (ActiveSpellEffect effect in new List<ActiveSpellEffect>(effects.ActiveEffects))
            {
                if (effect == null || !effect.IsApplied || (effect.AppliedStatBonus >= 0 && effect.AppliedSecondaryStatBonus >= 0))
                    continue;
                effects.RemoveEffect(effect);
                ended.Add(effect.Spell != null ? effect.Spell.Name : "ability penalty");
                if (!all) return ended;
            }
        }

        return ended;
    }

    /// <summary>
    /// Apply a stat buff to a target character by changing the score directly (the Rage spell's +2 Str and Con,
    /// ApplyRageSpellBuff). CON adds 1 HP per Hit Die per +2 (CHR-071).
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
                int hpBonus = (bonus / 2) * target.Stats.GetHitDice(); // per Hit Die (CHR-071)
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
    /// Tick spell effect durations for all characters (PCs and NPCs): the effects whose duration anchor passes
    /// <paramref name="ticksForAnchor"/>. Called with each initiative count reached (the effects anchored to that
    /// creature) and at the round boundary (the effects with no anchor in the initiative order), so each effect loses
    /// one round per round at its caster's initiative count (PHB p.138, CMB-006, SPL-032).
    /// Removes expired effects and reverses their stat modifications. At the round boundary it also ticks the
    /// alignment/undead detection effects, which are tracked apart.
    /// </summary>
    private void TickAllSpellDurations(Func<CharacterController, bool> ticksForAnchor, bool roundBoundary)
    {
        if (roundBoundary)
            Debug.Log($"[SpellDuration] Ticking round-boundary spell durations for round {CurrentRound}...");

        bool anyExpired = false;

        // Tick active, living PCs
        foreach (var pc in PCs)
        {
            if (!IsActiveCombatant(pc) || pc.Stats.IsDead) continue;
            anyExpired |= TickCharacterSpellDurations(pc, ticksForAnchor, roundBoundary);
        }

        // Tick active, living NPCs
        foreach (var npc in NPCs)
        {
            if (!IsActiveCombatant(npc) || npc.Stats.IsDead) continue;
            anyExpired |= TickCharacterSpellDurations(npc, ticksForAnchor, roundBoundary);
        }

        // Tick alignment/undead detection effects (concentration-based, separate from StatusEffectManager)
        if (roundBoundary)
            TickAllAlignmentDetectionDurations();

        if (roundBoundary || anyExpired)
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
            string detSpellName = character.ActiveAlignmentDetectionEffect?.SpellDisplayName ?? "Detection";
            bool expired = character.TickAlignmentDetectionDuration();
            if (expired)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"{detSpellName} expires on {character.Stats.CharacterName}."));
                ClearDetectionHighlights(character);
            }
        }
    }

    /// <summary>
    /// Tick the spell durations of a single character whose duration anchor passes <paramref name="ticksForAnchor"/>.
    /// The spell trackers kept outside StatusEffectManager (EffectService energy and debuff effects, item spells, the
    /// cleric spell counters) have no anchor and tick only at the round boundary (<paramref name="roundBoundary"/>;
    /// SPL-032). Returns true when an effect expired.
    /// </summary>
    private bool TickCharacterSpellDurations(CharacterController character, Func<CharacterController, bool> ticksForAnchor, bool roundBoundary)
    {
        if (!IsActiveCombatant(character) || character.Stats.IsDead)
            return false;
        bool anyExpired = false;
        var statusMgr = character.StatusEffectManager;
        // Mirror Image images whose tracked effect was removed by anything but its expiry end with it (batch 11 step 26).
        if (MirrorImageRules.HasImages(character))
            SyncMirrorImageDurationForCaster(character, statusMgr);
        if (statusMgr != null && statusMgr.ActiveEffectCount > 0
            && (ticksForAnchor == null || statusMgr.ActiveEffects.Exists(e => e != null && ticksForAnchor(e.DurationAnchor))))
        {
            var expired = statusMgr.TickEffects(ticksForAnchor);
            anyExpired = expired.Count > 0;

            foreach (var effect in expired)
            {
                string msg = $"⏱ {effect.Spell?.Name ?? "Unknown"} has expired on {character.Stats.CharacterName}!";
                Debug.Log($"[SpellDuration] {msg}");
                CombatUI?.ShowCombatLog(CombatLogHelper.Color(msg, CombatLogHelper.ColorOrange));

                if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.DISGUISE_SELF, StringComparison.Ordinal))
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🎭", $"{character.Stats.CharacterName}'s disguise fades; visible race returns to {character.DisplayedRace}."));
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.EXPEDITIOUS_RETREAT, StringComparison.Ordinal))
                {
                    ExpeditiousRetreatEffectData expiredData = character.RemoveExpeditiousRetreatEffect();
                    int speedDelta = expiredData != null ? Mathf.Max(0, expiredData.SpeedBonusFeet) : Mathf.Max(0, effect.AppliedSpeedBonusFeet);
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Expeditious Retreat expires on {character.Stats.CharacterName}: speed -{speedDelta} ft."));
                }
                else if (effect.Spell != null && (string.Equals(effect.Spell.SpellId, SpellNames.INVISIBILITY, StringComparison.Ordinal)
                    || string.Equals(effect.Spell.SpellId, "greater_invisibility", StringComparison.Ordinal)
                    || string.Equals(effect.Spell.SpellId, "improved_invisibility", StringComparison.Ordinal)))
                {
                    character.ClearInvisibilityEffect();
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"{effect.Spell.Name} expires on {character.Stats.CharacterName}."));
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.SEE_INVISIBLE, StringComparison.Ordinal))
                {
                    character.ClearSeeInvisibilityEffect();
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"{character.Stats.CharacterName}'s See Invisible expires."));
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.GLITTERDUST, StringComparison.Ordinal))
                {
                    character.ClearGlitterdustEffect();
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Glitterdust fades from {character.Stats.CharacterName}."));
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.PROTECTION_FROM_ARROWS, StringComparison.Ordinal))
                {
                    character.Stats.ActiveProtectionFromArrowsEffect = null;
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Protection from Arrows expires on {character.Stats.CharacterName}."));
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.STONESKIN, StringComparison.Ordinal))
                {
                    character.Stats.ActiveStoneskinEffect = null;
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Stoneskin expires on {character.Stats.CharacterName}."));
                }
                else if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.DIMENSIONAL_ANCHOR, StringComparison.Ordinal))
                {
                    character.Stats.ActiveDimensionalAnchorEffect = null;
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("⏱", $"Dimensional Anchor fades from {character.Stats.CharacterName}. Extradimensional travel is no longer blocked."));
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
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Haste expires on {character.Stats.CharacterName}: attack, AC, Reflex, and speed bonuses removed."));
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
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Slow expires on {character.Stats.CharacterName}: penalties removed, full actions restored."));
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
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Fire Shield expires on {character.Stats.CharacterName}: retribution and elemental damage reduction removed."));
                }
                else if (effect.Spell != null && AlignmentDetectionEffectData.IsDetectionSpell(effect.Spell.SpellId))
                {
                    character.RemoveAlignmentDetectionEffect();
                    ClearDetectionHighlights(character);
                    CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"{effect.Spell.Name} expires on {character.Stats.CharacterName}."));
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

        if (!roundBoundary)
            return anyExpired;

        // Delegate energy resist, protection, and debuff ticking to EffectService
        Action<string> logCb = msg => CombatUI?.ShowCombatLog(msg);
        EffectService.TickResistEnergyEffects(character, logCb);
        EffectService.TickProtectionFromEnergyEffects(character, logCb);
        EffectService.TickDebuffEffects(character, logCb);

        TickCharacterItemSpellDurations(character);

        // Tick custom cleric spell duration counters (level 2, SPL-003; level 3 + level 4)
        TickClericSpell2Durations(character);
        TickClericSpell3Durations(character);
        TickClericSpell4Durations(character);
        return anyExpired;
    }

    private void TickCharacterItemSpellDurations(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        InventoryComponent inventoryComponent = character.InventoryComp;
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"{spellName} expires on {owner.Stats.CharacterName}'s {item.Name}."));
        }
    }


    private List<CharacterController> GetThreateningEnemiesForSpellcasting(CharacterController caster)
    {
        // An enemy that threatens any square of the caster's footprint threatens a Large or
        // bigger caster, not only one that threatens its base square.
        List<CharacterController> allCharacters = GetAllCharacters();
        var threatening = ThreatSystem.GetThreateningEnemies(caster.GridPosition, caster, allCharacters);
        List<Vector2Int> footprint = caster.GetOccupiedSquares();
        if (footprint != null)
        {
            for (int i = 0; i < footprint.Count; i++)
            {
                if (footprint[i] == caster.GridPosition)
                    continue;

                List<CharacterController> squareThreats = ThreatSystem.GetThreateningEnemies(footprint[i], caster, allCharacters);
                for (int j = 0; j < squareThreats.Count; j++)
                {
                    if (!threatening.Contains(squareThreats[j]))
                        threatening.Add(squareThreats[j]);
                }
            }
        }

        threatening.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoOAgainst(enemy, caster));
        return threatening;
    }

    /// <summary>The dice label of the entangled casting Concentration check's d20 (a forced-dice scenario matches it).</summary>
    internal const string EntangledConcentrationDiceContext = "Entangled casting Concentration check";

    /// <summary>
    /// An entangled caster's Concentration check to cast (batch 11 step 13, SPL-145), the same for every caster and path
    /// (the cast core's Distraction stage): any spell, somatic component or not, needs a Concentration check
    /// (ConcentrationManager.MakeSpellcastingConcentrationCheck: d20 + the casting Concentration bonus, caster level + Con
    /// in this game, CHR-085; no Combat Casting, PHB p.92) against DC 15 + the spell's level on the casting class's list
    /// (<see cref="GetEntangledCastingSpellLevel"/>), the heightened level for a heightened spell (PHB p.95) but not the
    /// metamagic slot's level (PHB p.88). PHB
    /// p.308 (entangled) and the p.70 table with its footnote give DC 15 + the spell's level where PHB p.170 gives a flat
    /// 15: delegated RAW reading 2026-10-14 (docs/systems/RULES_COVERAGE.md 4.1). A failed check loses the spell and
    /// spends its slot. True when the caster is not entangled or passes.
    /// </summary>
    /// <summary>
    /// The spell level of the entangled check's DC: the level on the casting class's list (PHB p.177, as the save DC
    /// resolves it: the class of the slot spent, else the class lists), raised to the Heighten level of the cast's
    /// metamagic or, when the cast carries none (an NPC cast, a prepared spell), of the spell's own prepared record
    /// (PHB p.95).
    /// </summary>
    internal static int GetEntangledCastingSpellLevel(CharacterController caster, SpellData spell, MetamagicData metamagic)
    {
        if (spell == null)
            return 0;
        string castingClass = null;
        if (caster != null)
        {
            SpellcastingComponent spellComp = caster.Spellcasting;
            string slotClass = spellComp != null ? spellComp.GetCastingClassForSpellDC(spell) : null;
            castingClass = !string.IsNullOrWhiteSpace(slotClass) ? slotClass : SpellSaveDCRules.ResolveCastingClass(caster.Stats, spell);
        }
        return SpellSaveDCRules.GetEffectiveSpellLevel(spell, castingClass, metamagic ?? spell.MetamagicDataRef);
    }

    /// <summary>
    /// The entangled casting Concentration check (DC 15 + spell level, PHB p.308, p.70), with its log and trace mark and
    /// no spending (the cast core's Distraction stage spends, GameManager.LoseCastAtDistraction): true when the caster is
    /// not entangled or passes. <paramref name="dc"/> is 0 when no check is made. Also used by a scroll read outside the cast core (ResolveDirectItemCastChecks).
    /// </summary>
    private bool RollEntangledCastingConcentration(CharacterController caster, SpellData spell, MetamagicData metamagic, out int dc)
    {
        dc = 0;
        if (caster == null || caster.Stats == null || spell == null || !caster.HasCondition(CombatConditionType.Entangled))
            return true;

        int spellLevel = Mathf.Max(0, GetEntangledCastingSpellLevel(caster, spell, metamagic));
        dc = ConcentrationService.GetEntangledCastingDC(spellLevel);
        ConcentrationCheckResult check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
            caster, dc, ConcentrationCheckType.Entangled, spell, diceContext: EntangledConcentrationDiceContext);

        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🪢", $"Entangled casting concentration ({caster.Stats.CharacterName}, {spell.Name}): d20 {check.D20Roll} + conc {check.TotalBonus} = {check.TotalRoll} vs DC {dc} (15 + spell level {spellLevel})"));

        CastStageTrace.Mark(caster, spell, CastStageTrace.Distraction, check.Success ? "entangled-passed" : "entangled-lost");
        return check.Success;
    }

    private bool AttemptCastDefensively(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null) return false;

        int dc = ConcentrationService.GetDefensiveCastingDC(spell.SpellLevel);
        var check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
            caster,
            dc,
            ConcentrationCheckType.CastingDefensively,
            spell);

        LogSpellcastingConcentrationCheck(caster, spell, check);

        if (!check.Success)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💥", $"{caster.Stats.CharacterName} fails to cast defensively. {spell.Name} is lost!"));
            return false;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{caster.Stats.CharacterName} casts defensively and avoids attacks of opportunity."));
        return true;
    }

    /// <param name="beforeRolls">Runs once the caster has chosen to cast (defensively or not), before the rolls: the end
    /// of its concentration (decision D1, batch 11 step 16; the timing is a delegated RAW reading 2026-10-15). Not run
    /// on a cancel.</param>
    private void ResolveSpellcastProvocation(CharacterController caster, SpellData spell, bool isDeliveringHeldCharge, System.Action beforeRolls, System.Action<bool> onResolved)
    {
        _spellcastProvocationCancelled = false;

        if (caster == null || spell == null || isDeliveringHeldCharge)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Provocation, "no-provocation");
            onResolved?.Invoke(true);
            return;
        }

        var threateningEnemies = GetThreateningEnemiesForSpellcasting(caster);
        if (threateningEnemies == null || threateningEnemies.Count == 0)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Provocation, "not-threatened");
            onResolved?.Invoke(true);
            return;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{caster.Stats.CharacterName} is casting {spell.Name} while threatened ({threateningEnemies.Count} adjacent)."));

        int defensiveDC = ConcentrationService.GetDefensiveCastingDC(spell.SpellLevel);
        int concentrationBonus = ConcentrationService.GetConcentrationBonus(caster);
        float successChance = ConcentrationService.CalculateSuccessChancePercent(concentrationBonus, defensiveDC);

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
            OnCastDefensively = () => { beforeRolls?.Invoke(); onResolved?.Invoke(ResolveThreatenedSpellcast(caster, spell, threateningEnemies, castDefensively: true)); },
            OnProceed = () => { beforeRolls?.Invoke(); onResolved?.Invoke(ResolveThreatenedSpellcast(caster, spell, threateningEnemies, castDefensively: false)); },
            OnCancel = () =>
            {
                _spellcastProvocationCancelled = true;
                CastStageTrace.Mark(caster, spell, CastStageTrace.Provocation, "cancelled");
                onResolved?.Invoke(false);
            }
        });
    }

    /// <summary>
    /// Spellcasting provocation for a caster that decides without the player prompt (NPC turns and
    /// AI-controlled characters such as charmed PCs). Same rule and rolls as the PC prompt (SPL-006):
    /// the choice to cast defensively comes from <see cref="AISpellcastingStrategist.ShouldCastDefensively"/>,
    /// then <see cref="ResolveThreatenedSpellcast"/> resolves it. Call after the slot is spent and before
    /// the spell resolves. Returns true when the spell goes on to resolve; false means it is lost.
    /// </summary>
    private bool ResolveNPCSpellcastProvocation(CharacterController caster, SpellData spell, System.Action beforeRolls = null)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        List<CharacterController> threateningEnemies = GetThreateningEnemiesForSpellcasting(caster);
        if (threateningEnemies == null || threateningEnemies.Count == 0)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Provocation, "not-threatened");
            return true;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{caster.Stats.CharacterName} is casting {spell.Name} while threatened ({threateningEnemies.Count} adjacent)."));

        bool castDefensively = AISpellcastingStrategist.ShouldCastDefensively(caster, spell);
        beforeRolls?.Invoke(); // the caster stopped concentrating to begin casting (decision D1)
        return ResolveThreatenedSpellcast(caster, spell, threateningEnemies, castDefensively);
    }

    /// <summary>
    /// Shared PC and NPC resolution of a spell cast while threatened (PHB p.140, Table 8-2 p.141).
    /// Cast defensively: Concentration DC 15 + spell level, no AoOs, the spell is lost on a failure.
    /// Otherwise every threatening enemy that can still make an AoO gets one before the spell
    /// resolves; each hit forces Concentration DC 10 + damage + spell level or the spell is lost
    /// (PHB p.69-70, Concentration). Resolves synchronously. Returns true when the spell proceeds.
    /// </summary>
    private bool ResolveThreatenedSpellcast(CharacterController caster, SpellData spell, List<CharacterController> threateningEnemies, bool castDefensively)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (threateningEnemies == null || threateningEnemies.Count == 0)
            return true;

        bool proceeds;
        if (castDefensively)
        {
            proceeds = AttemptCastDefensively(caster, spell);
        }
        else
        {
            bool canProceed = false;
            ResolveSpellcastAoOs(caster, spell, threateningEnemies, proceed => canProceed = proceed);
            proceeds = canProceed;
        }

        ScenarioHooks.ThreatenedCast?.Invoke(caster, spell, castDefensively, proceeds, threateningEnemies.Count);
        CastStageTrace.Mark(caster, spell, CastStageTrace.Provocation,
            castDefensively ? (proceeds ? "defensive-passed" : "defensive-lost") : (proceeds ? "aoo-proceeds" : "aoo-lost"));
        return proceeds;
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

        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{caster.Stats.CharacterName} casts normally and provokes {threateningEnemies.Count} attack(s) of opportunity."));

        foreach (var enemy in threateningEnemies)
        {
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoOAgainst(enemy, caster))
                continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, caster, trigger: "spellcast");
            if (aooResult == null)
                continue;

            CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"AoO vs spellcasting: {aooResult.GetDetailedSummary()}"));

            // A caster the AoO kills or knocks unconscious (dying, stable) cannot finish casting.
            // A disabled caster (0 HP) still rolls the damage Concentration check below.
            if (caster.IsDead || caster.IsUnconscious || caster.Stats.IsDead)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💀", $"{caster.Stats.CharacterName} is struck down while casting {spell.Name}!"));
                onResolved?.Invoke(false);
                return;
            }

            if (aooResult.Hit && aooResult.TotalDamage > 0)
            {
                // Existing concentration effects / held charges can also be disrupted by this damage.
                CheckConcentrationOnDamage(caster, aooResult.TotalDamage);

                int dc = ConcentrationService.GetDamageDC(aooResult.TotalDamage, spell.SpellLevel);
                var check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
                    caster,
                    dc,
                    ConcentrationCheckType.DamagedWhileCasting,
                    spell,
                    aooResult.TotalDamage);

                LogSpellcastingConcentrationCheck(caster, spell, check);

                if (!check.Success)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💥", $"{caster.Stats.CharacterName}'s casting is interrupted by damage. {spell.Name} is lost!"));
                    onResolved?.Invoke(false);
                    return;
                }
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

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"<color={color}>Concentration [{reason}] {caster.Stats.CharacterName}: d20 {check.Roll} + {check.Bonus} = {check.Total} vs DC {check.DC} — {status}</color>"));
    }

    /// <summary>
    /// Records what the cast of <paramref name="caster"/> is about to spend (the cast core's Cost stage): the actions, the
    /// quickened spell of the round, the prepared slots and the spells-per-day counters, and, for a scroll or wand
    /// <paramref name="source"/>, the item, its pack slot, the scroll's stack and both of a wand's charge stores (ITM-084,
    /// ITM-034), so <see cref="RestoreSpellcastResourceSnapshot"/> can give all of it back.
    /// </summary>
    private void CaptureSpellcastResourceSnapshot(CharacterController caster, CastSource source = null)
    {
        _pendingSpellcastSnapshot = null;

        if (caster == null)
            return;

        var snapshot = new CastResourceSnapshot
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

        var spellComp = caster.Spellcasting;
        if (spellComp != null)
        {
            snapshot.QuickenedSpellUsed = spellComp.HasCastQuickenedSpellThisRound;

            if (spellComp.SpellSlots != null && spellComp.SpellSlots.Count > 0)
            {
                snapshot.SlotUsedStates = new List<bool>(spellComp.SpellSlots.Count);
                foreach (var slot in spellComp.SpellSlots)
                    snapshot.SlotUsedStates.Add(slot != null && slot.IsUsed);
            }

            // Spontaneous and partial casters spend a per-level counter, not a slot's IsUsed flag.
            snapshot.SpontaneousSlotsRemaining = spellComp.SpontaneousData?.SlotsRemaining != null
                ? (int[])spellComp.SpontaneousData.SlotsRemaining.Clone()
                : null;
            snapshot.LegacySlotsRemaining = spellComp.SlotsRemaining != null
                ? (int[])spellComp.SlotsRemaining.Clone()
                : null;
            snapshot.PartialSlotsRemaining = spellComp.PartialData?.SlotsRemaining != null
                ? (int[])spellComp.PartialData.SlotsRemaining.Clone()
                : null;
        }

        if (source != null && source.IsItem && source.Item != null)
        {
            snapshot.ItemSource = source;
            snapshot.Item = source.Item;
            snapshot.ItemIndex = source.ItemIndex;
            snapshot.ItemStackCount = source.Item.StackCount;
            snapshot.ItemCharges = source.Item.CurrentCharges;
            snapshot.WandDataCharges = source.Item.Wand != null ? source.Item.Wand.CurrentCharges : -1;
        }

        _pendingSpellcastSnapshot = snapshot;
    }

    /// <summary>
    /// Gives back the scroll or wand a restored snapshot recorded (ITM-084): the scroll's stack count, and the scroll in
    /// its pack slot when the spend took the last one out (ConsumeOneFromStack empties the slot); both of a wand's charge
    /// stores (ITM-034); and the cast source's item and slot, which the spend cleared.
    /// </summary>
    private static void RestoreSnapshotItem(CastResourceSnapshot snapshot, CharacterController caster)
    {
        ItemData item = snapshot != null ? snapshot.Item : null;
        if (item == null)
            return;

        item.StackCount = snapshot.ItemStackCount;
        item.CurrentCharges = snapshot.ItemCharges;
        if (item.Wand != null && snapshot.WandDataCharges >= 0)
            item.Wand.CurrentCharges = snapshot.WandDataCharges;

        Inventory inv = caster != null && caster.InventoryComp != null ? caster.InventoryComp.CharacterInventory : null;
        if (inv != null && inv.GeneralSlots != null && !InventoryHolds(inv, item))
        {
            int index = snapshot.ItemIndex;
            if (index >= 0 && index < inv.GeneralSlots.Length && inv.GeneralSlots[index] == null)
            {
                inv.GeneralSlots[index] = item;
                inv.RecalculateStats();
            }
            else if (!inv.AddItem(item))
            {
                // The pack has no room for the refunded item (nothing normally fills its slot between the spend and
                // the refund): say so rather than lose it silently.
                string owner = caster.Stats != null ? caster.Stats.CharacterName : "The caster";
                Debug.LogWarning($"[ScrollCast] {owner}'s pack has no room for the refunded {item.Name}; the item is lost.");
                Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", $"{owner}'s pack has no room for the refunded {item.Name}; it is lost."));
            }
        }

        if (snapshot.ItemSource != null)
        {
            snapshot.ItemSource.Item = item;
            snapshot.ItemSource.ItemIndex = snapshot.ItemIndex;
        }
    }

    private static bool InventoryHolds(Inventory inv, ItemData item)
    {
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            if (ReferenceEquals(inv.GeneralSlots[i], item))
                return true;
        }
        return false;
    }

    private void ClearSpellcastResourceSnapshot()
    {
        _pendingSpellcastSnapshot = null;
    }

    /// <summary>The cancel snapshot as it stands (RunCastSync keeps it for an outer cast; batch 11 step 8).</summary>
    private CastResourceSnapshot PeekSpellcastResourceSnapshot() => _pendingSpellcastSnapshot;

    /// <summary>Puts back a snapshot <see cref="PeekSpellcastResourceSnapshot"/> returned (RunCastSync, after a nested AI cast).</summary>
    private void PutBackSpellcastResourceSnapshot(CastResourceSnapshot snapshot)
    {
        _pendingSpellcastSnapshot = snapshot;
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

        var spellComp = caster.Spellcasting;
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

            RestoreSlotCounters(_pendingSpellcastSnapshot.SpontaneousSlotsRemaining, spellComp.SpontaneousData?.SlotsRemaining);
            RestoreSlotCounters(_pendingSpellcastSnapshot.LegacySlotsRemaining, spellComp.SlotsRemaining);
            RestoreSlotCounters(_pendingSpellcastSnapshot.PartialSlotsRemaining, spellComp.PartialData?.SlotsRemaining);
        }

        RestoreSnapshotItem(_pendingSpellcastSnapshot, caster);
        SpellComponentRules.RefundCostlyComponents(caster, _pendingSpellcastSnapshot.ComponentItemsSpent, _pendingSpellcastSnapshot.ComponentGoldSpent);
        _pendingSpellcastSnapshot = null;
    }

    private static void RestoreSlotCounters(int[] saved, int[] live)
    {
        if (saved == null || live == null)
            return;
        Array.Copy(saved, live, Mathf.Min(saved.Length, live.Length));
    }

    // ========== CONCENTRATION MECHANICS (D&D 3.5e PHB) ==========
    // One rule for PCs and NPCs (ConcentrationRules; batch 11 step 16, decisions D1 and D2, SPL-153): casting ends the
    // maintained spell with no roll, maintaining it costs a standard action on each later turn, and a "+N rounds" spell
    // keeps going N rounds after the concentration ends, its effect and its summoned swarm alike.

    private void BeginSummonSwarmConcentration(CharacterController caster, SpellData spell, CharacterController summonedSwarm)
    {
        if (caster == null || spell == null)
            return;

        ConcentrationManager concMgr = caster.Concentration;
        if (concMgr == null)
            return;

        // A previous concentration (the cast core normally ended it already) ends here, its summons into their tail.
        bool wasConcentrating = concMgr.IsConcentrating;
        ActiveSpellEffect concentrationMarker = new ActiveSpellEffect(spell, caster.Stats != null ? caster.Stats.CharacterName : "Caster", CasterLevelRules.ForCaster(caster, spell), caster.Stats != null ? caster.Stats.CharacterName : "Caster");
        string log = concMgr.BeginConcentration(concentrationMarker);
        if (!string.IsNullOrEmpty(log))
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("", $"{log}"));
        if (wasConcentrating)
            TransitionConcentrationSummonsToTail(caster, "new concentration spell");

        if (summonedSwarm != null)
        {
            ActiveSummonInstance active = GetActiveSummon(summonedSwarm);
            if (active != null)
            {
                int tail = Mathf.Max(1, ConcentrationRules.TailRounds(spell, concentrationMarker.CasterLevel));
                active.IsConcentrationSummon = true;
                active.HasEnteredPostConcentrationDuration = false;
                active.RemainingRounds = tail;
                active.TotalDurationRounds = tail;
            }
        }
    }

    /// <summary>
    /// The summons a concentration kept (Summon Swarm) whose caster no longer concentrates on their spell start the
    /// spell's "+N rounds" tail now (PHB p.176, p.289; ConcentrationRules.TailRounds); TickSummonDurations counts it down.
    /// </summary>
    private void TransitionConcentrationSummonsToTail(CharacterController caster, string reason)
    {
        if (caster == null)
            return;

        for (int i = 0; i < _activeSummons.Count; i++)
        {
            ActiveSummonInstance summon = _activeSummons[i];
            if (summon == null || summon.Caster != caster || !summon.IsConcentrationSummon || summon.HasEnteredPostConcentrationDuration)
                continue;
            if (ConcentrationService.IsConcentratingOnSpell(caster, summon.SourceSpellId))
                continue;

            SpellData source = string.IsNullOrEmpty(summon.SourceSpellId) ? null : SpellDatabase.GetSpell(summon.SourceSpellId);
            // The tail BeginSummonSwarmConcentration set from the cast's clone (an extended cast's doubled tail, batch 11
            // step 19), else the template's.
            int tail = Mathf.Max(1, summon.TotalDurationRounds > 0 ? summon.TotalDurationRounds
                : source != null ? ConcentrationRules.TailRounds(source, CasterLevelRules.ForCaster(caster, source)) : 0);
            summon.HasEnteredPostConcentrationDuration = true;
            summon.RemainingRounds = tail;
            summon.TotalDurationRounds = tail;
            string spellName = source != null ? source.Name : "the summoning";
            CombatUI?.ShowCombatLog(CombatLogHelper.Expired("", $"Concentration on {spellName} ended ({reason}). {summon.Controller?.Stats?.CharacterName ?? "The summoned creature"} will last {tail} more round{(tail == 1 ? "" : "s")}."));
        }
    }

    /// <summary>
    /// Check if a character needs to make concentration checks after taking damage.
    /// Applies to both ongoing concentration spells and held touch charges.
    /// </summary>
    private void CheckConcentrationOnDamage(CharacterController character, int damageTaken)
    {
        if (character == null || damageTaken <= 0) return;

        var concMgr = character.Concentration;
        var spellComp = character.Spellcasting;

        bool hasConcentrationSpell = concMgr != null && concMgr.IsConcentrating;
        bool hasHeldTouchCharge = spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null;

        if (!hasConcentrationSpell && !hasHeldTouchCharge) return;

        // If the character is dead, concentration and held charge break automatically.
        if (character.Stats.IsDead)
        {
            if (hasConcentrationSpell)
            {
                string breakLog = concMgr.ForceBreakConcentration("killed");
                if (!string.IsNullOrEmpty(breakLog))
                    CombatUI?.ShowCombatLog(CombatLogHelper.InterruptedRaw(breakLog));
                TransitionConcentrationSummonsToTail(character, "caster incapacitated");
            }

            if (hasHeldTouchCharge)
            {
                string lostSpellName = spellComp.HeldTouchSpell.Name;
                spellComp.ClearHeldTouchCharge("killed");
                CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💥", $"{character.Stats.CharacterName}'s held {lostSpellName} charge is lost (killed)!"));
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"<color={color}>{heldResult.LogMessage}</color>"));
            }

            if (!heldResult.Success)
            {
                string lostSpellName = spellComp.HeldTouchSpell.Name;
                spellComp.ClearHeldTouchCharge("failed concentration after damage");
                CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💥", $"{character.Stats.CharacterName} loses concentration and the held {lostSpellName} charge dissipates!"));
            }
        }

        // 2) Ongoing concentration spell check
        if (hasConcentrationSpell)
        {
            var result = concMgr.CheckConcentrationOnDamage(damageTaken);
            if (!string.IsNullOrEmpty(result.LogMessage))
            {
                string color = result.Success ? "#88CCFF" : "#FF6644";
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"<color={color}>{result.LogMessage}</color>"));
            }

            if (!result.Success)
                TransitionConcentrationSummonsToTail(character, "failed concentration check");
        }

        UpdateAllStatsUI();
    }

    /// <summary>
    /// The casting-while-concentrating step (decision D1, batch 11 step 16; PHB p.176): a caster cannot cast a spell while
    /// concentrating on another, so casting any spell (any casting time, a quickened one included; a scroll, DMG p.213)
    /// first ends the maintained spell, a free action with no roll; a "+N rounds" spell then runs its tail
    /// (ConcentrationRules.TailRounds), its summons included. The delivery of a held charge is not a casting, and a wand
    /// use is an open delegated question that does not end it yet (ConcentrationRules.CastEndsConcentration, SPL-114).
    /// The same for every caster. Returns true (the cast always proceeds).
    /// </summary>
    private bool HandleConcentrationOnCasting(CharacterController caster, SpellData newSpell, CastSource source)
    {
        if (caster == null || newSpell == null) return true;

        var concMgr = caster.Concentration;
        if (concMgr == null || !concMgr.IsConcentrating)
        {
            CastStageTrace.Mark(caster, newSpell, CastStageTrace.ConcentrationOnCast, "not-concentrating");
            return true;
        }

        if (!ConcentrationRules.CastEndsConcentration(source))
        {
            CastStageTrace.Mark(caster, newSpell, CastStageTrace.ConcentrationOnCast, "not-a-casting");
            return true;
        }

        CastStageTrace.Mark(caster, newSpell, CastStageTrace.ConcentrationOnCast, "ended");
        string endLog = concMgr.CeaseConcentration("to cast " + newSpell.Name);
        if (!string.IsNullOrEmpty(endLog))
            CombatUI?.ShowCombatLog(CombatLogHelper.Color(endLog, CombatLogHelper.ColorOrange));
        TransitionConcentrationSummonsToTail(caster, "caster cast another spell");
        UpdateAllStatsUI();
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

        var concMgr = caster.Concentration;
        if (concMgr == null) return;

        bool wasConcentrating = concMgr.IsConcentrating;
        string log = concMgr.BeginConcentration(effect);
        if (!string.IsNullOrEmpty(log))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("", $"{log}"));
        }
        if (wasConcentrating)
            TransitionConcentrationSummonsToTail(caster, "started another concentration spell");
    }

    /// <summary>
    /// Voluntarily stop concentrating (a free action, PHB p.141): the spell ends or runs its "+N rounds" tail.
    /// </summary>
    public void EndConcentrationVoluntarily(CharacterController character)
    {
        if (character == null) return;

        var concMgr = character.Concentration;
        if (concMgr == null || !concMgr.IsConcentrating) return;

        string log = concMgr.CeaseConcentration("by choice");
        if (!string.IsNullOrEmpty(log))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Color(log, CombatLogHelper.ColorOrange));
        }

        TransitionConcentrationSummonsToTail(character, "voluntary end");
        UpdateAllStatsUI();
    }

    // ── Maintaining the concentration (decision D2, PHB p.176; PC and NPC alike) ──────────────────────────────────

    /// <summary>
    /// Spends <paramref name="caster"/>'s standard action keeping its concentration spell going this turn (no attack of
    /// opportunity; ConcentrationRules.Maintain). The PC's Maintain button and the AI both call it. False when it cannot.
    /// </summary>
    private bool ExecuteMaintainConcentration(CharacterController caster)
    {
        string log = ConcentrationRules.Maintain(caster, out string reason);
        if (log == null)
        {
            if (caster != null && caster.Stats != null)
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{caster.Stats.CharacterName} cannot concentrate on a spell: {reason}."));
            return false;
        }
        CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🔵", log));
        UpdateAllStatsUI();
        return true;
    }

    /// <summary>The PC's Maintain button: the standard action that keeps the maintained spell going this turn.</summary>
    public void OnMaintainConcentrationButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null)
            return;
        EndAttackSequence();
        ExecuteMaintainConcentration(pc);
        ShowActionChoices();
    }

    /// <summary>The scenario and AI entry: maintain the concentration through the shared executor.</summary>
    public bool TryMaintainConcentrationForAI(CharacterController npc) => ExecuteMaintainConcentration(npc);

    /// <summary>
    /// A turn the turn-skip gate skips (CMB-075), PC or NPC: a pinned or paralyzed caster still concentrates on its
    /// spell, the one thing that turn lets it do (ConcentrationRules.MaintainOnSkippedTurn; delegated RAW reading
    /// 2026-10-15, PHB p.156, p.311). Any other caster's spell ends at the end of the turn.
    /// </summary>
    private void MaintainConcentrationOnSkippedTurn(CharacterController character)
    {
        string log = ConcentrationRules.MaintainOnSkippedTurn(character);
        if (log != null)
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🔵", log));
    }

    /// <summary>
    /// For the AI's maintain choice: whether <paramref name="caster"/> keeps a creature summoned by the spell it
    /// concentrates on (Summon Swarm), which lapsing would start on its tail.
    /// </summary>
    public bool HasConcentrationSummonForAI(CharacterController caster)
    {
        if (caster == null || caster.Concentration == null || !caster.Concentration.IsConcentrating)
            return false;
        for (int i = 0; i < _activeSummons.Count; i++)
        {
            ActiveSummonInstance summon = _activeSummons[i];
            if (summon != null && summon.Caster == caster && summon.IsConcentrationSummon && !summon.HasEnteredPostConcentrationDuration
                && ConcentrationService.IsConcentratingOnSpell(caster, summon.SourceSpellId))
                return true;
        }
        return false;
    }

    /// <summary>At each initiative count: that caster's new turn owes a new concentration; a spell at its limit ends.</summary>
    private void TickSpellConcentrationAtCount(CharacterController count)
    {
        if (count == null || count.Concentration == null || !count.Concentration.IsConcentrating)
            return;
        string log = ConcentrationRules.OnCasterCountReached(count);
        if (string.IsNullOrEmpty(log))
            return;
        CombatUI?.ShowCombatLog(CombatLogHelper.Expired("", log));
        TransitionConcentrationSummonsToTail(count, "duration limit");
        UpdateAllStatsUI();
    }

    /// <summary>
    /// At the end of each turn: a caster that spent no standard action concentrating this turn loses its spell, or the
    /// spell runs its tail (PHB p.176; delegated RAW reading 2026-10-15, the CHR-026 timing). The PC gets the notice in
    /// the combat log.
    /// </summary>
    private void CheckSpellConcentrationAtTurnEnd(CharacterController ending)
    {
        if (ending == null || ending.Concentration == null || !ending.Concentration.IsConcentrating)
            return;
        string log = ConcentrationRules.OnCasterTurnEnded(ending);
        if (string.IsNullOrEmpty(log))
            return;
        CombatUI?.ShowCombatLog(CombatLogHelper.Color(log, CombatLogHelper.ColorOrange));
        TransitionConcentrationSummonsToTail(ending, "not maintained");
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"Previous {old.SpellDisplayName} ends as {caster.Stats.CharacterName} begins a new detection."));
            }
        }

        // Ensure StatusEffectManager exists
        StatusEffectManager statusMgr = caster.StatusEffectManager;
        if (statusMgr == null)
            statusMgr = caster.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(caster.Stats);

        // Add spell effect for duration tracking
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
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
        CombatUI?.ShowCombatLog(CombatLogHelper.Defensive(emoji, $"{caster.Stats.CharacterName} begins concentrating on {detectionData.SpellDisplayName}."));

        string summary = detectionData.GetDetectionSummary();
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  → {summary}"));

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
            CombatUI?.ShowCombatLog(CombatLogHelper.Expired(emoji, $"{character.Stats.CharacterName}'s {detection.SpellDisplayName} expires."));
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
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{emoji2} {detection.SpellDisplayName} (Round {detection.ConcentrationRounds}): {summary}"));

        // Update visual highlights
        RefreshDetectionHighlights(character);
    }

    /// <summary>
    /// Get all living characters on the battlefield.
    /// </summary>
    private List<CharacterController> GetAllLivingCharacters()
    {
        var result = new List<CharacterController>();
        if (PCs != null)
        {
            foreach (var pc in PCs)
            {
                if (pc != null && pc.Stats != null && pc.Stats.CurrentHP > 0)
                    result.Add(pc);
            }
        }
        if (NPCs != null)
        {
            foreach (var npc in NPCs)
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
            if (detected.Creature == null || Grid == null)
                continue;

            // Apply a subtle colored highlight on the creature's cell
            var cell = Grid.GetCell(detected.Creature.GridPosition);
            if (cell == null) continue;
            cell.SetHighlight(detection.HighlightColor);
        }
    }

    /// <summary>
    /// Clear visual highlights from all cells when detection ends.
    /// </summary>
    private void ClearDetectionHighlights(CharacterController detector)
    {
        // Clear all cell highlights (they'll be restored by normal game logic)
        if (Grid != null)
        {
            foreach (var cell in Grid.Cells.Values)
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
