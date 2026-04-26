using System.Collections.Generic;
using System.Text;
using UnityEngine;

public partial class GameManager
{
    private enum GreaseCastMode
    {
        None,
        Area,
        Object
    }

    private sealed class ActiveGreaseArea
    {
        public HashSet<Vector2Int> Cells;
        public int RemainingRounds;
        public int SaveDC;
        public string CasterName;
    }

    private sealed class ActiveGreasedObject
    {
        public ItemData Item;
        public int RemainingRounds;
        public int SaveDC;
        public string CasterName;
    }

    private GreaseCastMode _pendingGreaseCastMode;
    private readonly List<ActiveGreaseArea> _activeGreaseAreas = new List<ActiveGreaseArea>();
    private readonly List<ActiveGreasedObject> _activeGreasedObjects = new List<ActiveGreasedObject>();

    private static bool IsGreaseSpell(SpellData spell)
    {
        return spell != null && spell.SpellId == "grease";
    }

    private bool IsPendingGreaseAreaCast()
    {
        return IsGreaseSpell(_pendingSpell) && _pendingGreaseCastMode == GreaseCastMode.Area;
    }

    private bool IsPendingGreaseObjectCast()
    {
        return IsGreaseSpell(_pendingSpell) && _pendingGreaseCastMode == GreaseCastMode.Object;
    }

    private void ResetPendingGreaseCastMode()
    {
        _pendingGreaseCastMode = GreaseCastMode.None;
    }

    private bool TryShowGreaseCastModePrompt(CharacterController caster)
    {
        if (!IsGreaseSpell(_pendingSpell) || CombatUI == null || caster == null || caster.Stats == null)
            return false;

        var options = new List<string>
        {
            "Grease Area (10-ft square)",
            "Grease Held Object"
        };

        CombatUI.ShowPickUpItemSelection(
            actorName: caster.Stats.CharacterName,
            itemOptions: options,
            onSelect: selectedIndex =>
            {
                _pendingGreaseCastMode = selectedIndex == 0 ? GreaseCastMode.Area : GreaseCastMode.Object;
                BeginPendingSpellTargeting(caster);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                ResetPendingGreaseCastMode();
                ShowActionChoices();
            },
            titleOverride: "Grease: Choose Target Mode",
            bodyOverride: "Select whether you want to grease a 10-ft square area or a held object.",
            optionButtonColorOverride: new Color(0.46f, 0.36f, 0.12f, 0.95f));

        return true;
    }

    private void EnterGreaseAreaTargetingMode(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null)
        {
            ShowActionChoices();
            return;
        }

        _isAoETargeting = true;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _pendingAttackMode = PendingAttackMode.CastSpell;
        CurrentSubPhase = PlayerSubPhase.SelectingAoETarget;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int range = spell.GetRangeSquaresForCasterLevel(caster?.Stats?.GetCasterLevel() ?? 0);
        if (range <= 0) range = 1;

        List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, range);
        foreach (var cell in rangeCells)
            cell.SetHighlight(HighlightType.SpellRange);

        HighlightCharacterFootprint(caster, HighlightType.Selected);
        CombatUI.SetTurnIndicator($"✦ {spell.Name}: Aim 10-ft square | Range: {range * 5} ft | Move mouse to preview, click to cast | Right-click to cancel");
    }

    private bool TryUpdateGreaseAreaPreview(CharacterController caster, Vector2 worldPoint)
    {
        if (!IsPendingGreaseAreaCast())
            return false;

        Vector2Int anchor = SquareGridUtils.WorldToGrid(worldPoint);
        if (anchor == _lastAoEHoverPos)
            return true;

        _lastAoEHoverPos = anchor;
        ClearAoEPreviewHighlights();

        int range = _pendingSpell.GetRangeSquaresForCasterLevel(caster?.Stats?.GetCasterLevel() ?? 0);
        if (range <= 0) range = 1;

        if (!AoESystem.IsWithinCastingRange(caster.GridPosition, anchor, range))
            return true;

        HashSet<Vector2Int> greaseCells = GetGreaseAreaCells(anchor);
        greaseCells = FilterToExistingCells(greaseCells);
        if (greaseCells.Count == 0)
            return true;

        _currentAoECells = greaseCells;

        foreach (Vector2Int cellPos in greaseCells)
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

        return true;
    }

    private bool TryHandleGreaseAreaTargetClick(CharacterController caster, SquareCell clickedCell)
    {
        if (!IsPendingGreaseAreaCast() || !_isAoETargeting || caster == null || clickedCell == null)
            return false;

        Vector2Int anchor = clickedCell.Coords;
        int range = _pendingSpell.GetRangeSquaresForCasterLevel(caster?.Stats?.GetCasterLevel() ?? 0);
        if (range <= 0) range = 1;

        if (!AoESystem.IsWithinCastingRange(caster.GridPosition, anchor, range))
            return true;

        HashSet<Vector2Int> greaseCells = FilterToExistingCells(GetGreaseAreaCells(anchor));
        if (greaseCells.Count == 0)
            return true;

        bool casterIsPC = caster.Team == CharacterTeam.Player;
        CharacterTeam enemyTeamType = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        List<CharacterController> allyTeam = GetTeamMembers(caster.Team);
        List<CharacterController> enemyTeam = GetTeamMembers(enemyTeamType);
        List<CharacterController> targets = AoESystem.GetTargetsInArea(
            greaseCells, caster, allyTeam, enemyTeam,
            AoETargetFilter.All, casterIsPC, Grid);

        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        PerformGreaseAreaCast(caster, targets, greaseCells);
        return true;
    }

    private HashSet<Vector2Int> GetGreaseAreaCells(Vector2Int anchor)
    {
        return new HashSet<Vector2Int>
        {
            anchor,
            new Vector2Int(anchor.x + 1, anchor.y),
            new Vector2Int(anchor.x, anchor.y + 1),
            new Vector2Int(anchor.x + 1, anchor.y + 1)
        };
    }

    private HashSet<Vector2Int> FilterToExistingCells(HashSet<Vector2Int> cells)
    {
        var filtered = new HashSet<Vector2Int>();
        if (cells == null || Grid == null)
            return filtered;

        foreach (Vector2Int cell in cells)
        {
            if (Grid.GetCell(cell) != null)
                filtered.Add(cell);
        }

        return filtered;
    }

    private int GetSpellSaveDC(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return 10;

        CharacterStats stats = caster.Stats;
        string className = (stats.CharacterClass ?? string.Empty).Trim().ToLowerInvariant();

        int castingMod;
        if (stats.IsWizard)
        {
            castingMod = stats.INTMod;
        }
        else if (stats.IsCleric || className == "druid" || className == "ranger" || className == "paladin")
        {
            castingMod = stats.WISMod;
        }
        else if (className == "sorcerer" || className == "bard")
        {
            castingMod = stats.CHAMod;
        }
        else
        {
            castingMod = Mathf.Max(stats.INTMod, Mathf.Max(stats.WISMod, stats.CHAMod));
        }

        return 10 + spell.SpellLevel + castingMod;
    }

    private int GetGreaseDurationRounds(CharacterController caster)
    {
        int casterLevel = caster != null && caster.Stats != null
            ? Mathf.Max(1, caster.Stats.GetCasterLevel())
            : 1;
        return casterLevel;
    }

    private void PerformGreaseAreaCast(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> greaseCells)
    {
        if (caster == null || _pendingSpell == null)
        {
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

            int saveDC = GetSpellSaveDC(caster, _pendingSpell);
            int durationRounds = GetGreaseDurationRounds(caster);

            var log = new StringBuilder();
            log.AppendLine("═══════════════════════════════════");
            log.AppendLine($"✨ {caster.Stats.CharacterName} casts {_pendingSpell.Name}! (10-ft square)");
            log.AppendLine($"  Duration: {durationRounds} rounds | Reflex DC: {saveDC}");

            var seenTargets = new HashSet<CharacterController>();
            for (int i = 0; i < targets.Count; i++)
            {
                CharacterController target = targets[i];
                if (target == null || target.Stats == null || target.Stats.IsDead || !seenTargets.Add(target))
                    continue;

                int roll = UnityEngine.Random.Range(1, 21);
                int total = roll + target.Stats.ReflexSave;
                bool saveSucceeded = total >= saveDC;

                log.AppendLine($"  {target.Stats.CharacterName}: Reflex d20({roll}) + {target.Stats.ReflexSave} = {total} vs DC {saveDC} {(saveSucceeded ? "SUCCESS" : "FAIL")}");
                if (!saveSucceeded)
                {
                    target.ApplyCondition(CombatConditionType.Prone, -1, _pendingSpell.Name);
                    log.AppendLine($"    💥 {target.Stats.CharacterName} falls prone!");
                }
            }

            RegisterActiveGreaseArea(caster, greaseCells, durationRounds, saveDC);

            log.AppendLine("  Ground is now greased (difficult movement, Balance DC 10 to stay upright).");
            log.Append("═══════════════════════════════════");

            _lastCombatLog = log.ToString();
            CombatUI?.ShowCombatLog(_lastCombatLog);

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            ResetPendingGreaseCastMode();

            Grid.ClearAllHighlights();
            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(caster, 1.5f));
        });
    }

    private void RegisterActiveGreaseArea(CharacterController caster, HashSet<Vector2Int> cells, int durationRounds, int saveDC)
    {
        if (cells == null || cells.Count == 0)
            return;

        var area = new ActiveGreaseArea
        {
            Cells = new HashSet<Vector2Int>(cells),
            RemainingRounds = Mathf.Max(1, durationRounds),
            SaveDC = saveDC,
            CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown"
        };

        _activeGreaseAreas.Add(area);

        if (_movementService != null)
        {
            foreach (Vector2Int cell in area.Cells)
                _movementService.SetDifficultTerrain(cell, true);
        }
    }

    private void PerformGreaseObjectCast(CharacterController caster, CharacterController target)
    {
        if (caster == null || target == null || _pendingSpell == null)
        {
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

            _lastCombatLog = ResolveGreaseObjectCast(caster, target);
            CombatUI?.ShowCombatLog(_lastCombatLog);

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            ResetPendingGreaseCastMode();

            Grid.ClearAllHighlights();
            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(caster, 1.5f));
        });
    }

    private bool IsCellInActiveGreaseArea(Vector2Int pos)
    {
        for (int i = 0; i < _activeGreaseAreas.Count; i++)
        {
            ActiveGreaseArea area = _activeGreaseAreas[i];
            if (area == null || area.RemainingRounds <= 0 || area.Cells == null)
                continue;

            if (area.Cells.Contains(pos))
                return true;
        }

        return false;
    }

    private int GetGreaseAreaExtraMovementCost(CharacterController mover, Vector2Int destination)
    {
        if (mover == null || mover.Stats == null || mover.Stats.IsDead)
            return 0;

        return IsCellInActiveGreaseArea(destination) ? 1 : 0;
    }

    public bool HandleGreaseStepAfterMovement(CharacterController mover, Vector2Int previousCell, Vector2Int currentCell)
    {
        if (mover == null || mover.Stats == null || mover.Stats.IsDead)
            return true;

        bool wasInGrease = IsCellInActiveGreaseArea(previousCell);
        bool nowInGrease = IsCellInActiveGreaseArea(currentCell);
        if (!nowInGrease)
            return true;

        if (!wasInGrease)
        {
            int balanceTotal = mover.Stats.RollSkillCheck("Balance");
            bool success = balanceTotal >= 10;
            CombatUI?.ShowCombatLog($"🛢 Grease check ({mover.Stats.CharacterName}): Balance {balanceTotal} vs DC 10 {(success ? "SUCCESS" : "FAIL")}.");

            if (!success)
            {
                mover.ApplyCondition(CombatConditionType.Prone, -1, "Grease");
                CombatUI?.ShowCombatLog($"💥 {mover.Stats.CharacterName} slips in grease and falls prone!");
                return false;
            }
        }

        return true;
    }

    private void TickActiveGreaseEffects()
    {
        if (_activeGreaseAreas.Count > 0)
            TickGreaseAreasAtRoundStart();

        if (_activeGreasedObjects.Count > 0)
            TickGreasedObjectsAtRoundStart();
    }

    private void ClearAllActiveGreaseEffects()
    {
        if (_movementService != null)
        {
            for (int i = 0; i < _activeGreaseAreas.Count; i++)
            {
                ActiveGreaseArea area = _activeGreaseAreas[i];
                if (area?.Cells == null)
                    continue;

                foreach (Vector2Int cell in area.Cells)
                    _movementService.SetDifficultTerrain(cell, false);
            }
        }

        _activeGreaseAreas.Clear();
        _activeGreasedObjects.Clear();
    }

    private void TickGreaseAreasAtRoundStart()
    {
        var expired = new List<ActiveGreaseArea>();

        for (int i = 0; i < _activeGreaseAreas.Count; i++)
        {
            ActiveGreaseArea area = _activeGreaseAreas[i];
            if (area == null)
            {
                expired.Add(area);
                continue;
            }

            var checkedCharacters = new HashSet<CharacterController>();
            foreach (Vector2Int cellPos in area.Cells)
            {
                SquareCell cell = Grid != null ? Grid.GetCell(cellPos) : null;
                if (cell == null || !cell.IsOccupied || cell.Occupant == null || cell.Occupant.Stats == null || cell.Occupant.Stats.IsDead)
                    continue;

                CharacterController occupant = cell.Occupant;
                if (!checkedCharacters.Add(occupant))
                    continue;

                int balanceTotal = occupant.Stats.RollSkillCheck("Balance");
                bool success = balanceTotal >= 10;
                CombatUI?.ShowCombatLog($"🛢 Grease (round start): {occupant.Stats.CharacterName} Balance {balanceTotal} vs DC 10 {(success ? "SUCCESS" : "FAIL")}." );
                if (!success)
                {
                    occupant.ApplyCondition(CombatConditionType.Prone, -1, "Grease");
                    CombatUI?.ShowCombatLog($"💥 {occupant.Stats.CharacterName} loses footing in grease and falls prone!");
                }
            }

            area.RemainingRounds--;
            if (area.RemainingRounds <= 0)
                expired.Add(area);
        }

        for (int i = 0; i < expired.Count; i++)
        {
            ActiveGreaseArea ex = expired[i];
            _activeGreaseAreas.Remove(ex);
            UnregisterGreaseAreaTerrain(ex);
            CombatUI?.ShowCombatLog("🛢 Grease effect dissipates.");
        }
    }

    private void UnregisterGreaseAreaTerrain(ActiveGreaseArea expiredArea)
    {
        if (_movementService == null || expiredArea == null || expiredArea.Cells == null)
            return;

        foreach (Vector2Int cell in expiredArea.Cells)
        {
            bool stillCovered = false;
            for (int i = 0; i < _activeGreaseAreas.Count; i++)
            {
                ActiveGreaseArea remaining = _activeGreaseAreas[i];
                if (remaining == null || remaining.RemainingRounds <= 0 || remaining.Cells == null)
                    continue;

                if (remaining.Cells.Contains(cell))
                {
                    stillCovered = true;
                    break;
                }
            }

            if (!stillCovered)
                _movementService.SetDifficultTerrain(cell, false);
        }
    }

    private string ResolveGreaseObjectCast(CharacterController caster, CharacterController target)
    {
        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return "Grease fizzles: invalid caster/target.";

        List<DisarmableHeldItemOption> heldItems = target.GetDisarmableHeldItemOptions();
        if (heldItems == null || heldItems.Count == 0)
            return $"Grease has no effect: {target.Stats.CharacterName} is not holding a valid object.";

        DisarmableHeldItemOption primaryHeld = heldItems[0];
        ItemData targetItem = primaryHeld.HeldItem;
        if (targetItem == null)
            return $"Grease has no effect: no held item found on {target.Stats.CharacterName}.";

        int saveDC = GetSpellSaveDC(caster, _pendingSpell);
        int roll = UnityEngine.Random.Range(1, 21);
        int total = roll + target.Stats.ReflexSave;
        bool saveSucceeded = total >= saveDC;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts Grease on {target.Stats.CharacterName}'s {targetItem.Name}!");
        sb.AppendLine($"Reflex save: d20({roll}) + {target.Stats.ReflexSave} = {total} vs DC {saveDC} {(saveSucceeded ? "SUCCESS" : "FAIL")}");

        if (!saveSucceeded)
        {
            ItemData dropped = target.Inventory != null ? target.Inventory.RemoveEquippedHeldItem(primaryHeld.HandSlot) : null;
            if (dropped != null)
            {
                SquareCell dropCell = Grid != null ? Grid.GetCell(target.GridPosition) : null;
                dropCell?.AddGroundItem(dropped);
                sb.AppendLine($"💨 {target.Stats.CharacterName} drops {dropped.Name}!");
            }
            else
            {
                sb.AppendLine($"💨 {target.Stats.CharacterName} fumbles but keeps hold of the item.");
            }
        }

        RegisterGreasedObject(targetItem, GetGreaseDurationRounds(caster), saveDC, caster.Stats.CharacterName);
        sb.AppendLine($"{targetItem.Name} remains slick for {GetGreaseDurationRounds(caster)} rounds.");
        sb.Append("═══════════════════════════════");

        return sb.ToString();
    }

    private void RegisterGreasedObject(ItemData item, int durationRounds, int saveDC, string casterName)
    {
        if (item == null)
            return;

        for (int i = 0; i < _activeGreasedObjects.Count; i++)
        {
            ActiveGreasedObject existing = _activeGreasedObjects[i];
            if (existing != null && ReferenceEquals(existing.Item, item))
            {
                existing.RemainingRounds = Mathf.Max(existing.RemainingRounds, Mathf.Max(1, durationRounds));
                existing.SaveDC = Mathf.Max(existing.SaveDC, saveDC);
                existing.CasterName = casterName;
                return;
            }
        }

        _activeGreasedObjects.Add(new ActiveGreasedObject
        {
            Item = item,
            RemainingRounds = Mathf.Max(1, durationRounds),
            SaveDC = saveDC,
            CasterName = casterName
        });
    }

    private bool TryFindGreasedItemHolder(ItemData item, out CharacterController holder, out EquipSlot slot)
    {
        holder = null;
        slot = EquipSlot.None;

        if (item == null)
            return false;

        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController character = all[i];
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            List<DisarmableHeldItemOption> options = character.GetDisarmableHeldItemOptions();
            for (int j = 0; j < options.Count; j++)
            {
                if (ReferenceEquals(options[j].HeldItem, item))
                {
                    holder = character;
                    slot = options[j].HandSlot;
                    return true;
                }
            }
        }

        return false;
    }

    private void TickGreasedObjectsAtRoundStart()
    {
        var expired = new List<ActiveGreasedObject>();

        for (int i = 0; i < _activeGreasedObjects.Count; i++)
        {
            ActiveGreasedObject effect = _activeGreasedObjects[i];
            if (effect == null || effect.Item == null)
            {
                expired.Add(effect);
                continue;
            }

            if (TryFindGreasedItemHolder(effect.Item, out CharacterController holder, out EquipSlot slot))
            {
                int roll = UnityEngine.Random.Range(1, 21);
                int reflex = holder.Stats != null ? holder.Stats.ReflexSave : 0;
                int total = roll + reflex;
                bool saveSucceeded = total >= effect.SaveDC;
                CombatUI?.ShowCombatLog($"🛢 Greased item check: {holder.Stats.CharacterName} d20({roll}) + {reflex} = {total} vs DC {effect.SaveDC} {(saveSucceeded ? "SUCCESS" : "FAIL")}." );

                if (!saveSucceeded)
                {
                    ItemData dropped = holder.Inventory != null ? holder.Inventory.RemoveEquippedHeldItem(slot) : null;
                    if (dropped != null)
                    {
                        SquareCell cell = Grid != null ? Grid.GetCell(holder.GridPosition) : null;
                        cell?.AddGroundItem(dropped);
                        CombatUI?.ShowCombatLog($"💨 {holder.Stats.CharacterName} drops {dropped.Name} (too slippery to hold)." );
                    }
                }
            }

            effect.RemainingRounds--;
            if (effect.RemainingRounds <= 0)
                expired.Add(effect);
        }

        for (int i = 0; i < expired.Count; i++)
            _activeGreasedObjects.Remove(expired[i]);
    }

    private ActiveGreasedObject GetActiveGreasedObject(ItemData item)
    {
        if (item == null)
            return null;

        for (int i = 0; i < _activeGreasedObjects.Count; i++)
        {
            ActiveGreasedObject effect = _activeGreasedObjects[i];
            if (effect != null && effect.Item != null && ReferenceEquals(effect.Item, item) && effect.RemainingRounds > 0)
                return effect;
        }

        return null;
    }

    private bool TryResolveGreasedItemPickup(CharacterController actor, ItemData item, out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || actor.Stats == null || item == null)
            return true;

        ActiveGreasedObject effect = GetActiveGreasedObject(item);
        if (effect == null)
            return true;

        int roll = UnityEngine.Random.Range(1, 21);
        int reflex = actor.Stats.ReflexSave;
        int total = roll + reflex;
        bool saveSucceeded = total >= effect.SaveDC;

        if (!saveSucceeded)
        {
            failureReason = $"{actor.Stats.CharacterName} can't keep hold of {item.Name}: Reflex d20({roll}) + {reflex} = {total} vs DC {effect.SaveDC}.";
            return false;
        }

        CombatUI?.ShowCombatLog($"🛢 {actor.Stats.CharacterName} steadies {item.Name}: Reflex d20({roll}) + {reflex} = {total} vs DC {effect.SaveDC}.");
        return true;
    }
}
