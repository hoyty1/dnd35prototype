using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;
using Random = UnityEngine.Random;

public partial class GameManager
{
    private class MirrorImageState
    {
        public CharacterController Caster;
        public ActiveSpellEffect Effect;
        public readonly List<CharacterController> Clones = new List<CharacterController>();

        public int RemainingRounds => Effect != null ? Mathf.Max(0, Effect.RemainingRounds) : 0;
    }

    private readonly Dictionary<CharacterController, MirrorImageState> _mirrorImageStates = new Dictionary<CharacterController, MirrorImageState>();
    private readonly Dictionary<CharacterController, CharacterController> _mirrorImageCloneToCaster = new Dictionary<CharacterController, CharacterController>();
    private readonly HashSet<CharacterController> _mirrorImageFollowSuppression = new HashSet<CharacterController>();

    private bool _isSelectingMirrorImageSwap;
    private CharacterController _mirrorImageSwapCaster;

    private static readonly Vector2Int[] MirrorImageOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1)
    };

    private bool TryResolveMirrorImageSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.MIRROR_IMAGE, StringComparison.Ordinal))
            return false;

        if (caster == null || caster.Stats == null)
            return true;

        if (result == null || !result.Success)
            return true;

        CharacterController recipient = caster;
        if (target != null && target != caster)
        {
            CombatUI?.ShowCombatLog("⚠ Mirror Image is a personal spell and affects only the caster.");
        }

        ClearMirrorImageForCaster(recipient, "recast", removeStatusEffect: true, log: false);

        StatusEffectManager statusMgr = recipient.StatusEffectManager;
        if (statusMgr == null)
            statusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(recipient.Stats);

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        ActiveSpellEffect effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);
        if (effect == null)
        {
            CombatUI?.ShowCombatLog($"⚠ {recipient.Stats.CharacterName} could not gain Mirror Image due to stacking rules.");
            return true;
        }

        var state = new MirrorImageState
        {
            Caster = recipient,
            Effect = effect
        };

        int intendedCloneCount = UnityEngine.Random.Range(2, 6); // 1d4+1
        List<SquareCell> spawnCells = GetMirrorImageSpawnCells(recipient, intendedCloneCount);

        for (int i = 0; i < spawnCells.Count; i++)
        {
            CharacterController clone = SpawnMirrorImageClone(recipient, spawnCells[i], i + 1);
            if (clone == null)
                continue;

            state.Clones.Add(clone);
            _mirrorImageCloneToCaster[clone] = recipient;
        }

        _mirrorImageStates[recipient] = state;

        SpellcastingComponent spellComp = recipient.Spellcasting;
        if (spellComp != null)
            spellComp.ActiveBuffs[SpellNames.MIRROR_IMAGE] = effect.RemainingRounds;

        CombatUI?.ShowCombatLog($"<color=#B7A8FF>🪞 {recipient.Stats.CharacterName} casts Mirror Image: {state.Clones.Count} clone(s) appear (rolled {intendedCloneCount}).</color>");

        if (state.Clones.Count < intendedCloneCount)
            CombatUI?.ShowCombatLog($"<color=#FFCC66>⚠ Only {state.Clones.Count}/{intendedCloneCount} images could be placed due to blocked adjacent cells.</color>");

        return true;
    }

    private List<SquareCell> GetMirrorImageSpawnCells(CharacterController caster, int maxCount)
    {
        var cells = new List<SquareCell>();
        if (caster == null || Grid == null || maxCount <= 0)
            return cells;

        int size = 1;
        for (int i = 0; i < MirrorImageOffsets.Length && cells.Count < maxCount; i++)
        {
            Vector2Int pos = caster.GridPosition + MirrorImageOffsets[i];
            SquareCell cell = Grid.GetCell(pos);
            if (cell == null)
                continue;

            if (!Grid.CanPlaceCreature(pos, size, null))
                continue;

            cells.Add(cell);
        }

        return cells;
    }

    private CharacterController SpawnMirrorImageClone(CharacterController caster, SquareCell cell, int cloneIndex)
    {
        if (caster == null || caster.Stats == null || cell == null)
            return null;

        CharacterStats cloneStats = BuildMirrorImageCloneStats(caster, cloneIndex);

        GameObject cloneObj = new GameObject($"MirrorImage_{caster.Stats.CharacterName}_{cloneIndex}_{UnityEngine.Random.Range(1000, 9999)}");
        if (cloneObj.GetComponent<SpriteRenderer>() == null)
            cloneObj.AddComponent<SpriteRenderer>();

        CharacterController clone = cloneObj.AddComponent<CharacterController>();

        Sprite aliveSprite = caster.AliveSprite;
        SpriteRenderer casterRenderer = caster.GetComponent<SpriteRenderer>();
        if (aliveSprite == null && casterRenderer != null)
            aliveSprite = casterRenderer.sprite;

        clone.Init(cloneStats, cell.Coords, aliveSprite, caster.DeadSprite);
        clone.ConfigureTeamControl(caster.Team, controllable: false);
        clone.PriorityTargetName = caster.PriorityTargetName;

        SpriteRenderer cloneRenderer = clone.GetComponent<SpriteRenderer>();
        if (cloneRenderer != null && casterRenderer != null)
        {
            cloneRenderer.sprite = casterRenderer.sprite;
            cloneRenderer.color = casterRenderer.color;
            cloneRenderer.sortingLayerID = casterRenderer.sortingLayerID;
            cloneRenderer.sortingOrder = casterRenderer.sortingOrder;
        }

        clone.transform.localScale = caster.transform.localScale;

        MirrorImageClone cloneMarker = cloneObj.AddComponent<MirrorImageClone>();
        cloneMarker.Initialize(caster, cloneIndex, caster.Stats.TouchArmorClass);

        return clone;
    }

    private CharacterStats BuildMirrorImageCloneStats(CharacterController caster, int cloneIndex)
    {
        CharacterStats source = caster.Stats;
        string raceName = !string.IsNullOrWhiteSpace(source.RaceName) ? source.RaceName : null;

        CharacterStats stats = new CharacterStats(
            name: source.CharacterName,
            level: Mathf.Max(1, source.Level),
            characterClass: string.IsNullOrWhiteSpace(source.CharacterClass) ? "Wizard" : source.CharacterClass,
            str: source.BaseSTR != 0 ? source.BaseSTR : Mathf.Max(1, source.STR),
            dex: source.BaseDEX != 0 ? source.BaseDEX : Mathf.Max(1, source.DEX),
            con: source.BaseCON != 0 ? source.BaseCON : Mathf.Max(1, source.CON),
            wis: source.BaseWIS != 0 ? source.BaseWIS : Mathf.Max(1, source.WIS),
            intelligence: source.BaseINT != 0 ? source.BaseINT : Mathf.Max(1, source.INT),
            cha: source.BaseCHA != 0 ? source.BaseCHA : Mathf.Max(1, source.CHA),
            bab: Mathf.Max(0, source.BaseAttackBonus),
            armorBonus: Mathf.Max(0, source.ArmorBonus),
            shieldBonus: Mathf.Max(0, source.ShieldBonus),
            damageDice: Mathf.Max(1, source.BaseDamageDice),
            damageCount: Mathf.Max(1, source.BaseDamageCount),
            bonusDamage: 0,
            baseSpeed: Mathf.Max(1, source.BaseSpeed),
            atkRange: Mathf.Max(1, source.AttackRange),
            baseHitDieHP: 1,
            raceName: raceName);

        stats.CharacterAlignment = source.CharacterAlignment;
        stats.MaterialComposition = source.MaterialComposition;
        stats.CreatureType = source.CreatureType;
        stats.ChallengeRating = source.ChallengeRating;
        stats.CurrentHP = Mathf.Max(1, stats.CurrentHP);
        stats.SourceNpcDefinitionId = $"mirror_image_clone_{cloneIndex}";

        return stats;
    }

    public bool TryHandleMirrorImageCloneAttacked(CharacterController attacker, CharacterController target, CombatResult baseResult, out CombatResult resolved)
    {
        return TryHandleMirrorImageCloneAttacked(attacker, target, baseResult, null, out resolved);
    }

    public bool TryHandleMirrorImageCloneAttacked(CharacterController attacker, CharacterController target, CombatResult baseResult, int? totalAttackModifier, out CombatResult resolved)
    {
        resolved = baseResult;

        if (!IsMirrorImageClone(target))
            return false;

        MirrorImageClone marker = target != null ? target.GetComponent<MirrorImageClone>() : null;
        CharacterController caster = GetMirrorImageCaster(target);

        string attackerName = attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "Unknown";
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "caster";

        int touchArmorClass = marker != null
            ? Mathf.Max(0, marker.TouchArmorClass)
            : Mathf.Max(0, caster != null && caster.Stats != null ? caster.Stats.TouchArmorClass : 10);

        bool hasAttackRollContext = totalAttackModifier.HasValue && attacker != null && attacker.Stats != null;

        if (hasAttackRollContext)
        {
            var (hit, roll, total) = attacker.Stats.RollToHitWithMod(totalAttackModifier.Value, touchArmorClass);
            resolved.DieRoll = roll;
            resolved.TotalRoll = total;
            resolved.Hit = hit;
            resolved.NaturalTwenty = roll == 20;
            resolved.NaturalOne = roll == 1;
        }
        else if (resolved.DieRoll > 0)
        {
            int total = resolved.TotalRoll;
            bool hit = resolved.DieRoll == 20 || (resolved.DieRoll != 1 && total >= touchArmorClass);
            resolved.Hit = hit;
            resolved.NaturalTwenty = resolved.DieRoll == 20;
            resolved.NaturalOne = resolved.DieRoll == 1;
        }
        else
        {
            resolved.Hit = true;
            resolved.NaturalTwenty = false;
            resolved.NaturalOne = false;
        }

        resolved.TargetAC = touchArmorClass;
        resolved.MissedDueToConcealment = false;
        resolved.Damage = 0;
        resolved.BaseDamageRoll = 0;
        resolved.RawTotalDamage = 0;
        resolved.FinalDamageDealt = 0;
        resolved.TargetKilled = false;

        if (resolved.Hit)
        {
            resolved.SpecialAttackNote = $"Mirror Image: Attack {resolved.TotalRoll} vs Touch AC {touchArmorClass} - HIT! {attackerName}'s attack strikes an illusion of {casterName}, which dissipates.";
            DissipateMirrorImageClone(target, $"attacked by {attackerName}");
        }
        else
        {
            resolved.SpecialAttackNote = $"Mirror Image: Attack {resolved.TotalRoll} vs Touch AC {touchArmorClass} - MISS! {attackerName} fails to strike the illusion of {casterName}.";
        }

        return true;
    }

    public bool TryHandleMirrorImageSpellTargetAttack(CharacterController attacker, CharacterController target, SpellData spell, out string logLine)
    {
        logLine = null;

        if (!IsMirrorImageClone(target))
            return false;

        string attackerName = attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "Unknown";
        CharacterController caster = GetMirrorImageCaster(target);
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "caster";
        string spellName = spell != null && !string.IsNullOrWhiteSpace(spell.Name) ? spell.Name : "attack";

        DissipateMirrorImageClone(target, $"targeted by {spellName}");
        logLine = $"🪞 {attackerName}'s {spellName} strikes a Mirror Image of {casterName}; the clone dissipates.";
        return true;
    }

    private bool IsMirrorImageClone(CharacterController candidate)
    {
        if (candidate == null)
            return false;

        MirrorImageClone clone = candidate.GetComponent<MirrorImageClone>();
        return clone != null && !clone.IsDissipated;
    }

    private CharacterController GetMirrorImageCaster(CharacterController clone)
    {
        if (clone == null)
            return null;

        if (_mirrorImageCloneToCaster.TryGetValue(clone, out CharacterController caster))
            return caster;

        MirrorImageClone marker = clone.GetComponent<MirrorImageClone>();
        return marker != null ? marker.RealCaster : null;
    }

    private void DissipateMirrorImageClone(CharacterController clone, string reason)
    {
        if (clone == null)
            return;

        MirrorImageClone marker = clone.GetComponent<MirrorImageClone>();
        if (marker != null && marker.IsDissipated)
            return;

        CharacterController caster = GetMirrorImageCaster(clone);

        if (marker != null)
            marker.MarkDissipated();

        _mirrorImageCloneToCaster.Remove(clone);

        if (caster != null && _mirrorImageStates.TryGetValue(caster, out MirrorImageState state))
        {
            state.Clones.Remove(clone);
            if (state.Clones.Count <= 0)
            {
                ClearMirrorImageForCaster(caster, "all clones dissipated", removeStatusEffect: true, log: true);
            }
        }

        if (Grid != null)
            Grid.ClearCreatureOccupancy(clone);

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "caster";
        CombatUI?.ShowCombatLog($"<color=#D5C8FF>🪞 A Mirror Image of {casterName} dissipates ({reason}).</color>");

        Destroy(clone.gameObject);
    }

    private void ClearMirrorImageForCaster(CharacterController caster, string reason, bool removeStatusEffect, bool log)
    {
        if (caster == null)
            return;

        if (!_mirrorImageStates.TryGetValue(caster, out MirrorImageState state))
            return;

        for (int i = state.Clones.Count - 1; i >= 0; i--)
        {
            CharacterController clone = state.Clones[i];
            if (clone == null)
                continue;

            MirrorImageClone marker = clone.GetComponent<MirrorImageClone>();
            if (marker != null)
                marker.MarkDissipated();

            _mirrorImageCloneToCaster.Remove(clone);
            if (Grid != null)
                Grid.ClearCreatureOccupancy(clone);
            Destroy(clone.gameObject);
        }

        state.Clones.Clear();
        _mirrorImageStates.Remove(caster);
        _mirrorImageFollowSuppression.Remove(caster);

        if (removeStatusEffect)
        {
            StatusEffectManager statusMgr = caster.StatusEffectManager;
            statusMgr?.RemoveEffectsBySpellId(SpellNames.MIRROR_IMAGE);

            SpellcastingComponent spellComp = caster.Spellcasting;
            spellComp?.ActiveBuffs?.Remove(SpellNames.MIRROR_IMAGE);
        }

        if (log)
        {
            string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            CombatUI?.ShowCombatLog($"<color=#FFAA44>⏱ Mirror Image ends on {casterName} ({reason}).</color>");
        }
    }

    private List<CharacterController> GetAdjacentMirrorImageClones(CharacterController caster)
    {
        var adjacent = new List<CharacterController>();
        if (caster == null)
            return adjacent;

        if (!_mirrorImageStates.TryGetValue(caster, out MirrorImageState state) || state.Clones.Count == 0)
            return adjacent;

        for (int i = 0; i < state.Clones.Count; i++)
        {
            CharacterController clone = state.Clones[i];
            if (clone == null || !IsMirrorImageClone(clone))
                continue;

            int distance = SquareGridUtils.GetDistance(caster.GridPosition, clone.GridPosition);
            if (distance == 1)
                adjacent.Add(clone);
        }

        return adjacent;
    }

    private bool TryBeginMirrorImageSwapSelection(CharacterController caster)
    {
        if (caster == null || caster.Stats == null)
            return false;

        if (!caster.IsControllable)
            return false;

        List<CharacterController> adjacent = GetAdjacentMirrorImageClones(caster);
        if (adjacent.Count == 0)
            return false;

        _isSelectingMirrorImageSwap = true;
        _mirrorImageSwapCaster = caster;
        CurrentSubPhase = PlayerSubPhase.SelectingSpecialTarget;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        for (int i = 0; i < adjacent.Count; i++)
        {
            CharacterController clone = adjacent[i];
            if (clone == null)
                continue;

            SquareCell cloneCell = Grid.GetCell(clone.GridPosition);
            if (cloneCell == null)
                continue;

            cloneCell.SetHighlight(HighlightType.SpellTarget);
            _highlightedCells.Add(cloneCell);
        }

        HighlightCharacterFootprint(caster, HighlightType.Selected);
        CombatUI?.SetActionButtonsVisible(false);
        CombatUI?.SetTurnIndicator($"🪞 Mirror Image: Select adjacent clone to swap with, or click Skip ({adjacent.Count} available)");

        CombatUI?.ShowConfirmationDialog(
            title: "Mirror Image",
            message: "Select an adjacent clone on the grid to swap positions, or Skip to end your turn.",
            confirmLabel: "Skip",
            cancelLabel: "Keep Selecting",
            onConfirm: () => CompleteMirrorImageSwapSelection(skip: true, selectedClone: null),
            onCancel: () => { });

        return true;
    }

    private void HandleMirrorImageSwapCellClick(CharacterController caster, SquareCell cell)
    {
        if (!_isSelectingMirrorImageSwap || caster == null || cell == null)
            return;

        if (cell.IsOccupied && cell.Occupant != null && IsMirrorImageClone(cell.Occupant))
        {
            CharacterController clone = cell.Occupant;
            CharacterController cloneCaster = GetMirrorImageCaster(clone);
            if (cloneCaster == caster && SquareGridUtils.GetDistance(caster.GridPosition, clone.GridPosition) == 1)
            {
                CompleteMirrorImageSwapSelection(skip: false, selectedClone: clone);
                return;
            }
        }

        CombatUI?.ShowCombatLog("⚠ Select an adjacent Mirror Image clone, or press Skip.");
    }

    private void CompleteMirrorImageSwapSelection(bool skip, CharacterController selectedClone)
    {
        CharacterController caster = _mirrorImageSwapCaster;

        if (!skip && caster != null && selectedClone != null)
        {
            Vector2Int casterPos = caster.GridPosition;
            Vector2Int clonePos = selectedClone.GridPosition;

            if (Grid != null)
            {
                Grid.ClearCreatureOccupancy(caster);
                Grid.ClearCreatureOccupancy(selectedClone);
            }

            SquareCell casterDest = Grid != null ? Grid.GetCell(clonePos) : null;
            SquareCell cloneDest = Grid != null ? Grid.GetCell(casterPos) : null;

            _mirrorImageFollowSuppression.Add(caster);
            caster.MoveToCell(casterDest, markAsMoved: false);
            _mirrorImageFollowSuppression.Remove(caster);
            selectedClone.MoveToCell(cloneDest, markAsMoved: false);

            CombatUI?.ShowCombatLog($"<color=#B7A8FF>🪞 {caster.Stats.CharacterName} swaps positions with a Mirror Image ({casterPos.x},{casterPos.y}) ↔ ({clonePos.x},{clonePos.y}).</color>");
        }
        else if (caster != null)
        {
            CombatUI?.ShowCombatLog($"<color=#B7A8FF>🪞 {caster.Stats.CharacterName} keeps current position (Mirror Image swap skipped).</color>");
        }

        _isSelectingMirrorImageSwap = false;
        _mirrorImageSwapCaster = null;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        EndAttackSequence();
        EndThrownAttackSequence();
        ResetOffHandTurnState();
        CombatUI.SetActionButtonsVisible(false);

        if (CurrentPhase != TurnPhase.CombatOver)
            NextInitiativeTurn();
    }

    private void CancelMirrorImageSwapSelectionAndSkip()
    {
        CompleteMirrorImageSwapSelection(skip: true, selectedClone: null);
    }

    private void OnMirrorImageEffectExpired(CharacterController caster)
    {
        ClearMirrorImageForCaster(caster, "duration expired", removeStatusEffect: false, log: true);
    }

    private void SyncMirrorImageDurationForCaster(CharacterController caster, StatusEffectManager statusMgr)
    {
        if (caster == null)
            return;

        if (!_mirrorImageStates.TryGetValue(caster, out MirrorImageState state))
            return;

        ActiveSpellEffect mirrorEffect = null;
        if (statusMgr != null && statusMgr.ActiveEffects != null)
        {
            for (int i = 0; i < statusMgr.ActiveEffects.Count; i++)
            {
                ActiveSpellEffect effect = statusMgr.ActiveEffects[i];
                if (effect?.Spell == null)
                    continue;

                if (string.Equals(effect.Spell.SpellId, SpellNames.MIRROR_IMAGE, StringComparison.Ordinal))
                {
                    mirrorEffect = effect;
                    break;
                }
            }
        }

        if (mirrorEffect == null)
        {
            ClearMirrorImageForCaster(caster, "effect removed", removeStatusEffect: false, log: false);
            return;
        }

        state.Effect = mirrorEffect;

        SpellcastingComponent spellComp = caster.Spellcasting;
        if (spellComp != null)
            spellComp.ActiveBuffs[SpellNames.MIRROR_IMAGE] = mirrorEffect.RemainingRounds;
    }

    private class MirrorImageMovePlan
    {
        public CharacterController Clone;
        public SquareCell Destination;
    }

    public void NotifyCharacterMovement(CharacterController mover, Vector2Int previousPosition, Vector2Int currentPosition, string movementType = null)
    {
        if (mover == null || previousPosition == currentPosition)
            return;

        if (_mirrorImageFollowSuppression.Contains(mover))
        {
            _mirrorImageFollowSuppression.Remove(mover);
            return;
        }

        if (!_mirrorImageStates.TryGetValue(mover, out MirrorImageState state) || state == null || state.Clones == null || state.Clones.Count == 0)
            return;

        if (IsMirrorImageClone(mover))
            return;

        Vector2Int offset = currentPosition - previousPosition;
        if (offset == Vector2Int.zero)
            return;

        FollowMirrorImageCasterMovement(mover, state, offset, movementType);
    }

    private void FollowMirrorImageCasterMovement(CharacterController caster, MirrorImageState state, Vector2Int offset, string movementType)
    {
        if (caster == null || state == null || Grid == null)
            return;

        var activeClones = new List<CharacterController>();
        for (int i = 0; i < state.Clones.Count; i++)
        {
            CharacterController clone = state.Clones[i];
            if (clone == null || !IsMirrorImageClone(clone))
                continue;

            activeClones.Add(clone);
        }

        if (activeClones.Count == 0)
            return;

        for (int i = 0; i < activeClones.Count; i++)
            Grid.ClearCreatureOccupancy(activeClones[i]);

        var reservedCells = new HashSet<Vector2Int>();
        List<Vector2Int> casterSquares = caster.GetOccupiedSquares();
        for (int i = 0; i < casterSquares.Count; i++)
            reservedCells.Add(casterSquares[i]);

        var movePlans = new List<MirrorImageMovePlan>();
        var dissipatedClones = new List<CharacterController>();
        int fallbackMoves = 0;

        for (int i = 0; i < activeClones.Count; i++)
        {
            CharacterController clone = activeClones[i];
            Vector2Int preferredPos = clone.GridPosition + offset;

            if (TryReserveMirrorImageDestination(clone, preferredPos, reservedCells, out SquareCell preferredCell))
            {
                movePlans.Add(new MirrorImageMovePlan
                {
                    Clone = clone,
                    Destination = preferredCell
                });

                continue;
            }

            if (TryFindMirrorImageFallbackCell(caster, clone, preferredPos, reservedCells, out SquareCell fallbackCell))
            {
                fallbackMoves++;
                movePlans.Add(new MirrorImageMovePlan
                {
                    Clone = clone,
                    Destination = fallbackCell
                });

                continue;
            }

            dissipatedClones.Add(clone);
        }

        for (int i = 0; i < movePlans.Count; i++)
        {
            MirrorImageMovePlan plan = movePlans[i];
            if (plan?.Clone == null || plan.Destination == null)
                continue;

            MoveMirrorImageCloneToDestination(plan.Clone, plan.Destination);
        }

        for (int i = 0; i < dissipatedClones.Count; i++)
        {
            CharacterController clone = dissipatedClones[i];
            if (clone == null)
                continue;

            DissipateMirrorImageClone(clone, "could not keep formation while following");
        }

        if (movePlans.Count > 0)
        {
            string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            CombatUI?.ShowCombatLog($"<color=#B7A8FF>🪞 Mirror images shift to follow {casterName}.</color>");
        }

        if (fallbackMoves > 0)
            CombatUI?.ShowCombatLog($"<color=#C7B9FF>🪞 {fallbackMoves} image(s) adjust position around obstacles.</color>");

        if (dissipatedClones.Count > 0)
            CombatUI?.ShowCombatLog($"<color=#D5C8FF>🪞 {dissipatedClones.Count} image(s) dissipate while trying to keep up.</color>");
    }

    private bool TryReserveMirrorImageDestination(CharacterController clone, Vector2Int destination, HashSet<Vector2Int> reservedCells, out SquareCell cell)
    {
        cell = null;
        if (clone == null || Grid == null)
            return false;

        if (reservedCells.Contains(destination))
            return false;

        if (!Grid.CanPlaceCreature(destination, clone.GetVisualSquaresOccupied(), clone))
            return false;

        SquareCell candidate = Grid.GetCell(destination);
        if (candidate == null)
            return false;

        reservedCells.Add(destination);
        cell = candidate;
        return true;
    }

    private bool TryFindMirrorImageFallbackCell(CharacterController caster, CharacterController clone, Vector2Int preferredPosition, HashSet<Vector2Int> reservedCells, out SquareCell fallbackCell)
    {
        fallbackCell = null;
        if (caster == null || clone == null || Grid == null)
            return false;

        Vector2Int casterPos = caster.GridPosition;
        int bestScore = int.MaxValue;

        for (int i = 0; i < MirrorImageOffsets.Length; i++)
        {
            Vector2Int candidatePos = casterPos + MirrorImageOffsets[i];

            if (reservedCells.Contains(candidatePos))
                continue;

            if (!Grid.CanPlaceCreature(candidatePos, clone.GetVisualSquaresOccupied(), clone))
                continue;

            SquareCell candidateCell = Grid.GetCell(candidatePos);
            if (candidateCell == null)
                continue;

            int score = SquareGridUtils.GetDistance(preferredPosition, candidatePos) * 10 + i;
            if (score < bestScore)
            {
                bestScore = score;
                fallbackCell = candidateCell;
            }
        }

        if (fallbackCell == null)
            return false;

        reservedCells.Add(fallbackCell.Coords);
        return true;
    }

    private void MoveMirrorImageCloneToDestination(CharacterController clone, SquareCell destination)
    {
        if (clone == null || destination == null)
            return;

        if (SquareGridUtils.IsAdjacent(clone.GridPosition, destination.Coords))
        {
            StartCoroutine(clone.MoveAlongPath(new List<Vector2Int> { destination.Coords }, 0.05f, markAsMoved: false));
            return;
        }

        clone.MoveToCell(destination, markAsMoved: false);
    }

    private void ClearAllMirrorImageEffects(string reason)
    {
        var casters = new List<CharacterController>(_mirrorImageStates.Keys);
        for (int i = 0; i < casters.Count; i++)
        {
            CharacterController caster = casters[i];
            if (caster == null)
                continue;

            ClearMirrorImageForCaster(caster, reason, removeStatusEffect: false, log: false);
        }

        _mirrorImageStates.Clear();
        _mirrorImageCloneToCaster.Clear();
        _mirrorImageFollowSuppression.Clear();
        _isSelectingMirrorImageSwap = false;
        _mirrorImageSwapCaster = null;
    }

    public CharacterController GetMirrorImagePriorityTargetForAI(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return null;

        CharacterController best = null;
        int bestDistance = int.MaxValue;

        foreach (KeyValuePair<CharacterController, MirrorImageState> kvp in _mirrorImageStates)
        {
            CharacterController caster = kvp.Key;
            MirrorImageState state = kvp.Value;

            if (caster == null || caster.Stats == null || caster.Stats.IsDead)
                continue;

            if (!IsEnemyTeam(attacker, caster))
                continue;

            int casterDistance = SquareGridUtils.GetDistance(attacker.GridPosition, caster.GridPosition);
            if (casterDistance < bestDistance)
            {
                best = caster;
                bestDistance = casterDistance;
            }

            if (state == null || state.Clones == null)
                continue;

            for (int i = 0; i < state.Clones.Count; i++)
            {
                CharacterController clone = state.Clones[i];
                if (!IsMirrorImageClone(clone) || clone.Stats == null || clone.Stats.IsDead)
                    continue;

                int dist = SquareGridUtils.GetDistance(attacker.GridPosition, clone.GridPosition);
                if (dist < bestDistance)
                {
                    best = clone;
                    bestDistance = dist;
                }
            }
        }

        return best;
    }
}
