using System.Collections.Generic;
using System.Text;
using UnityEngine;

public partial class GameManager
{
    private enum GreaseCastMode
    {
        None,
        Area,
        Object,
        Armor
    }

    private sealed class ActiveGreasedObject
    {
        public ItemData Item;
        public int RemainingRounds;
        public int SaveDC;
        public string CasterName;
    }

    private GreaseCastMode _pendingGreaseCastMode;
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

    private bool IsPendingGreaseArmorCast()
    {
        return IsGreaseSpell(_pendingSpell) && _pendingGreaseCastMode == GreaseCastMode.Armor;
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
            "Grease Held Object",
            "Grease Worn Armor"
        };

        CombatUI.ShowPickUpItemSelection(
            actorName: caster.Stats.CharacterName,
            itemOptions: options,
            onSelect: selectedIndex =>
            {
                if (selectedIndex == 0)
                    _pendingGreaseCastMode = GreaseCastMode.Area;
                else if (selectedIndex == 1)
                    _pendingGreaseCastMode = GreaseCastMode.Object;
                else
                    _pendingGreaseCastMode = GreaseCastMode.Armor;

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
            bodyOverride: "Select whether you want to grease an area, held object, or worn armor.",
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

    private void EnterGreaseArmorTargetingMode(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null || Grid == null)
        {
            ShowActionChoices();
            return;
        }

        _pendingAttackMode = PendingAttackMode.CastSpell;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        int range = spell.GetRangeSquaresForCasterLevel(caster?.Stats?.GetCasterLevel() ?? 0);
        if (range <= 0) range = 1;

        List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, range);
        for (int i = 0; i < rangeCells.Count; i++)
        {
            SquareCell cell = rangeCells[i];
            if (cell == null || !cell.IsOccupied || cell.Occupant == null || cell.Occupant.Stats == null || cell.Occupant.Stats.IsDead)
                continue;

            _highlightedCells.Add(cell);
            bool isAlly = IsAllyTeam(caster, cell.Occupant);
            cell.SetHighlight(isAlly ? HighlightType.AoEAlly : HighlightType.AttackRange);
        }

        HighlightCharacterFootprint(caster, HighlightType.Selected);
        CombatUI?.SetTurnIndicator($"✦ {spell.Name}: Select a creature's worn armor | Range: {range * 5} ft");
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
            if (_isGreaseTestEncounter)
                log.AppendLine("  [TEST] Verifying low-Reflex grappler saves and prone results for clustered targets.");

            if (targets != null && targets.Count > 0)
            {
                var seenTargets = new HashSet<CharacterController>();
                int uniqueTargets = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    CharacterController target = targets[i];
                    if (target == null || target.Stats == null || target.Stats.IsDead || !seenTargets.Add(target))
                        continue;

                    uniqueTargets++;
                }

                log.AppendLine($"  Creatures currently in area: {uniqueTargets}");
            }

            CreateGreaseArea(GetGreaseCenterWorldPosition(greaseCells), durationRounds, saveDC, caster);

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

    private Vector3 GetGreaseCenterWorldPosition(HashSet<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return Vector3.zero;

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (Vector2Int cell in cells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }

        return new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
    }

    /// <summary>
    /// Creates a grease area through the reusable persistent area-effect system.
    /// </summary>
    public void CreateGreaseArea(Vector3 centerPosition, int duration, int saveDC, CharacterController caster)
    {
        GameObject greaseObj = new GameObject("Grease_Area");
        greaseObj.transform.position = centerPosition;

        GreaseAreaEffect grease = greaseObj.AddComponent<GreaseAreaEffect>();
        grease.CenterPosition = centerPosition;
        grease.RoundsRemaining = Mathf.Max(1, duration);
        grease.SaveDC = saveDC;
        grease.Caster = caster;
        grease.CasterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
    }

    /// <summary>
    /// Shared hook for area effects that need to mark difficult terrain on the movement grid.
    /// </summary>
    public void SetAreaDifficultTerrain(IEnumerable<Vector2Int> cells, bool isDifficult)
    {
        if (_movementService == null || cells == null)
            return;

        foreach (Vector2Int cell in cells)
            _movementService.SetDifficultTerrain(cell, isDifficult);
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

    private void PerformGreaseArmorCast(CharacterController caster, CharacterController target)
    {
        if (caster == null || target == null || target.Stats == null || _pendingSpell == null)
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

            int durationRounds = GetGreaseDurationRounds(caster);
            StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
            if (statusMgr == null)
                statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(target.Stats);

            for (int i = statusMgr.ActiveEffects.Count - 1; i >= 0; i--)
            {
                ActiveSpellEffect existing = statusMgr.ActiveEffects[i];
                if (existing == null)
                    continue;

                bool isGreaseArmor = (existing.Spell != null && existing.Spell.SpellId == "grease_armor")
                    || existing.GreasedArmorGrappleResistBonus != 0
                    || existing.GreasedArmorGrappleEscapeBonus != 0
                    || existing.GreasedArmorBreakPinBonus != 0
                    || existing.GreasedArmorResistPinBonus != 0;

                if (isGreaseArmor)
                    statusMgr.RemoveEffect(existing);
            }

            var effectSpell = new SpellData
            {
                SpellId = "grease_armor",
                Name = "Grease (Armor)",
                BuffType = "circumstance"
            };

            var greasedArmorEffect = new ActiveSpellEffect
            {
                Spell = effectSpell,
                CasterName = caster.Stats.CharacterName,
                CasterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel()),
                AffectedCharacterName = target.Stats.CharacterName,
                DurationType = DurationType.Rounds,
                RemainingRounds = durationRounds,
                BonusTypeLegacy = "Circumstance",
                BonusTypeEnum = BonusType.Circumstance,
                IsApplied = true,
                GreasedArmorGrappleResistBonus = 10,
                GreasedArmorGrappleEscapeBonus = 10,
                GreasedArmorBreakPinBonus = 10,
                GreasedArmorResistPinBonus = 10
            };
            statusMgr.ActiveEffects.Add(greasedArmorEffect);

            var log = new StringBuilder();
            log.AppendLine("═══════════════════════════════");
            log.AppendLine($"✨ {caster.Stats.CharacterName} casts Grease on {target.Stats.CharacterName}'s armor!");
            log.AppendLine($"Duration: {durationRounds} rounds | Save: none (worn armor)");
            log.AppendLine($"{target.Stats.CharacterName} gains +10 circumstance bonus to resist/escape grapple and pin checks.");
            log.Append("═══════════════════════════════");
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

    private bool IsCellInActiveGreaseArea(Vector2Int pos)
    {
        List<GreaseAreaEffect> greaseEffects = AreaEffectManager.Instance.GetEffectsOfType<GreaseAreaEffect>();
        for (int i = 0; i < greaseEffects.Count; i++)
        {
            GreaseAreaEffect effect = greaseEffects[i];
            if (effect != null && effect.IsCellInArea(pos))
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
        AreaEffectManager.Instance.OnCombatRoundStart();

        if (_activeGreasedObjects.Count > 0)
            TickGreasedObjectsAtRoundStart();
    }

    private void ClearAllActiveGreaseEffects()
    {
        AreaEffectManager.Instance.ClearAllEffects();
        _activeGreasedObjects.Clear();
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
        if (_isGreaseTestEncounter)
            sb.AppendLine("[TEST] Greased-object validation: failed save should force immediate weapon drop.");

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
