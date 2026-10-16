using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

public partial class GameManager
{
    private sealed class ActiveGreasedObject
    {
        public ItemData Item;
        public int RemainingRounds;
        public int SaveDC;
        public string CasterName;
        /// <summary>The caster, for the saves' spell-and-caster context (SaveRules, SPL-018); cleared with the list.</summary>
        public CharacterController Caster;
    }

    // _pendingGreaseCastMode (GreaseCastMode, Spell/Casting/Core/CastChoices.cs): the open cast's choice (GameManager.CastContext.cs, batch 11 step 4).
    private readonly List<ActiveGreasedObject> _activeGreasedObjects = new List<ActiveGreasedObject>();

    private static bool IsGreaseSpell(SpellData spell)
    {
        return spell != null && spell.SpellId == SpellNames.GREASE;
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

        int range = CasterLevelRules.RangeSquares(caster, spell);
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

        int range = CasterLevelRules.RangeSquares(caster, spell);
        if (range <= 0) range = 1;

        List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, range);
        for (int i = 0; i < rangeCells.Count; i++)
        {
            SquareCell cell = rangeCells[i];
            if (cell == null || !cell.IsOccupied || cell.Occupant == null || cell.Occupant.Stats == null || cell.Occupant.Stats.IsDead)
                continue;

            _highlightedCells.Add(cell);
            bool isAlly = TeamUtility.IsAlly(caster, cell.Occupant);
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

        int range = CasterLevelRules.RangeSquares(caster, _pendingSpell);
        if (range <= 0) range = 1;

        if (!AoESystem.IsWithinCastingRange(caster.GridPosition, anchor, range))
            return true;

        // The preview shows what the cast takes: an origin (the 10-ft. square's centre) in the caster's line of effect, and
        // the squares that origin has line of effect to, as the Legality stage filters them (batch 11 step 24, SPL-006).
        GridIntersection origin = GridIntersection.NorthEastCornerOf(anchor);
        if (!AoESystem.HasLineOfEffectToOrigin(caster.GridPosition, caster.GetCurrentSpaceSquares(), origin))
            return true;
        HashSet<Vector2Int> greaseCells = GetGreaseAreaCells(anchor);
        greaseCells = FilterToExistingCells(greaseCells);
        AoESystem.FilterCellsByLineOfEffect(greaseCells, origin);
        if (greaseCells.Count == 0)
            return true;

        _currentAoECells = greaseCells;

        foreach (Vector2Int cellPos in greaseCells)
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

        return true;
    }

    private bool TryHandleGreaseAreaTargetClick(CharacterController caster, SquareCell clickedCell)
    {
        if (!IsPendingGreaseAreaCast() || !_isAoETargeting || caster == null || clickedCell == null)
            return false;

        Vector2Int anchor = clickedCell.Coords;
        int range = CasterLevelRules.RangeSquares(caster, _pendingSpell);
        if (range <= 0) range = 1;

        if (!AoESystem.IsWithinCastingRange(caster.GridPosition, anchor, range))
            return true;

        // The caster needs line of effect to the area's origin, the 10-ft. square's centre (PHB p.176; the Legality stage
        // refuses it too, batch 11 step 24).
        if (!AoESystem.HasLineOfEffectToOrigin(caster.GridPosition, caster.GetCurrentSpaceSquares(), GridIntersection.NorthEastCornerOf(anchor)))
            return true;
        HashSet<Vector2Int> greaseCells = FilterToExistingCells(GetGreaseAreaCells(anchor));
        if (greaseCells.Count == 0)
            return true;

        List<CharacterController> targets = AoESystem.GetTargetsInArea(greaseCells, caster, AoETargetFilter.All, Grid);

        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        // The 10-ft. square's point of origin is its centre, the north-east corner of the clicked square, from which the
        // Legality stage traces line of effect (PHB p.176; batch 11 step 24, SPL-006).
        PerformGreaseCast(caster, null, targets, greaseCells, GridIntersection.NorthEastCornerOf(anchor));
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

    /// <summary>
    /// The save DC of a spell resolved by a GameManager handler, through the one DC rule
    /// (<see cref="SpellSaveDCRules"/>, SPL-001). The metamagic is the one on the cast's own spell clone
    /// (<see cref="SpellData.MetamagicDataRef"/>, stamped by SpellCaster.ApplyMetamagicToSpellData; batch 11 step 19), which
    /// SpellSaveDCRules.Compute reads, so a heightened spell uses the heightened level (PHB p.95) for every caster and
    /// path, whether or not the spell is the open PC cast.
    /// </summary>
    private int GetSpellSaveDC(CharacterController caster, SpellData spell)
    {
        return SpellSaveDCRules.Compute(caster, spell, null);
    }

    /// <summary>Grease lasts 1 round per caster level (PHB p.237), at the caster level of the cast's own spell.</summary>
    private static int GetGreaseDurationRounds(CharacterController caster, SpellData spell)
    {
        return SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
    }

    /// <summary>
    /// The Grease target click, in any of its three forms (PHB p.237): the request builder of one Grease cast (batch 11
    /// step 10; formerly PerformGreaseAreaCast, PerformGreaseObjectCast and PerformGreaseArmorCast). The form is the open
    /// cast's choice (CastChoices.GreaseMode, from the mode prompt, TryShowGreaseCastModePrompt); an area cast passes its
    /// 10-ft square's <paramref name="greaseCells"/> and the creatures in it, <paramref name="areaTargets"/>; an object or
    /// armor cast the creature holding or wearing it, <paramref name="objectHolder"/>. The cast runs through the cast core
    /// (SpellCastRequest.ForUiGrease, the Grease resolve, <see cref="ResolveGreaseCast"/>).
    /// </summary>
    private void PerformGreaseCast(CharacterController caster, CharacterController objectHolder,
        List<CharacterController> areaTargets, HashSet<Vector2Int> greaseCells, GridIntersection? areaOrigin = null)
    {
        GreaseCastMode mode = _pendingGreaseCastMode;
        bool missingHolder = mode == GreaseCastMode.Object ? objectHolder == null
            : mode == GreaseCastMode.Armor && (objectHolder == null || objectHolder.Stats == null);
        if (caster == null || _pendingSpell == null || missingHolder)
        {
            ShowActionChoices();
            return;
        }

        CastContext ctx = OpenUiCastForBuilder(nameof(PerformGreaseCast));
        if (ctx == null)
            return;

        CastStageTrace.Mark(caster, _pendingSpell, CastStageTrace.Begin, nameof(PerformGreaseCast) + ":" + mode);
        CurrentSubPhase = PlayerSubPhase.Animating;
        SpellCastRequest.ForUiGrease(ctx.Request, objectHolder, areaTargets, greaseCells, areaOrigin);
        ExecuteCast(ctx, null);
    }

    /// <summary>
    /// Resolve (Grease, PHB p.237), by the cast's mode: the area's grease (CreateGreaseArea), the held object's
    /// (ResolveGreaseObjectCast) or the worn armor's (ResolveGreaseArmorCast). Returns the cast's result log.
    /// </summary>
    private string ResolveGreaseCast(CastContext ctx)
    {
        switch (ctx.Request.Choices.GreaseMode)
        {
            case GreaseCastMode.Object:
                CastStageTrace.Mark(ctx.Request.Caster, ctx.Request.Spell, CastStageTrace.Handler, nameof(ResolveGreaseObjectCast));
                return ResolveGreaseObjectCast(ctx.Request.Caster, ctx.Request.Aim.ObjectHolder, ctx.Request.Spell);
            case GreaseCastMode.Armor:
                return ResolveGreaseArmorCast(ctx);
            default:
                return ResolveGreaseAreaCast(ctx);
        }
    }

    /// <summary>Grease on a 10-ft square (PHB p.237): the persistent grease area and its log.</summary>
    private string ResolveGreaseAreaCast(CastContext ctx)
    {
        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;
        List<CharacterController> targets = ctx.Request.Aim.AreaTargets;
        HashSet<Vector2Int> greaseCells = ctx.Request.Aim.Cells;
        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(CreateGreaseArea));

        int saveDC = GetSpellSaveDC(caster, spell);
        int durationRounds = GetGreaseDurationRounds(caster, spell);

        var log = new StringBuilder();
        log.AppendLine("═══════════════════════════════════");
        log.AppendLine($"✨ {caster.Stats.CharacterName} casts {spell.Name}! (10-ft square)");
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

        CreateGreaseArea(GetGreaseCenterWorldPosition(greaseCells), durationRounds, saveDC, caster, spell, footprint: greaseCells);

        log.AppendLine("  Ground is now greased (difficult movement, Balance DC 10 to stay upright).");
        log.Append("═══════════════════════════════════");
        return log.ToString();
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
    public void CreateGreaseArea(Vector3 centerPosition, int duration, int saveDC, CharacterController caster, SpellData spell, HashSet<Vector2Int> footprint = null)
    {
        GameObject greaseObj = new GameObject("Grease_Area");
        greaseObj.transform.position = centerPosition;

        GreaseAreaEffect grease = greaseObj.AddComponent<GreaseAreaEffect>();
        grease.CenterPosition = centerPosition;
        grease.CastFootprint = footprint != null ? new HashSet<Vector2Int>(footprint) : null; // the cast's squares (batch 11 step 24)
        grease.RoundsRemaining = Mathf.Max(1, duration);
        grease.SaveDC = saveDC;
        grease.Caster = caster;
        grease.CasterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
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

    /// <summary>
    /// Grease on worn armor (PHB p.237): a +10 circumstance bonus on the wearer's checks to resist or escape a grapple
    /// or a pin, replacing an earlier greased-armor effect, and its log.
    /// </summary>
    private string ResolveGreaseArmorCast(CastContext ctx)
    {
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.ObjectHolder;
        SpellData spell = ctx.Request.Spell;
        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, "grease-armor");

        int durationRounds = GetGreaseDurationRounds(caster, spell);
        StatusEffectManager statusMgr = target.StatusEffectManager;
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
            CasterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell),
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
        return log.ToString();
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

    private int GetHighestGreaseSaveDCAtCell(Vector2Int pos) => GetHighestGreaseSaveDCAtCell(pos, out _);

    /// <summary>The highest save DC of the Grease areas on <paramref name="pos"/>, and the caster of that area.</summary>
    private int GetHighestGreaseSaveDCAtCell(Vector2Int pos, out CharacterController greaseCaster)
    {
        greaseCaster = null;
        int highestDC = 10;
        List<GreaseAreaEffect> greaseEffects = AreaEffectManager.Instance.GetEffectsOfType<GreaseAreaEffect>();
        for (int i = 0; i < greaseEffects.Count; i++)
        {
            GreaseAreaEffect effect = greaseEffects[i];
            if (effect == null || !effect.IsCellInArea(pos))
                continue;

            if (greaseCaster == null || effect.SaveDC > highestDC)
                greaseCaster = effect.Caster;
            highestDC = Mathf.Max(highestDC, effect.SaveDC);
        }

        return highestDC;
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
            // If an area hook has already applied prone on entry, stop movement immediately.
            if (mover.HasCondition(CombatConditionType.Prone))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("💥", $"{mover.Stats.CharacterName} enters grease, falls prone, and stops moving."));
                return false;
            }

            int saveDC = GetHighestGreaseSaveDCAtCell(currentCell, out CharacterController greaseCaster);
            int roll = DiceRoller.D20();
            // The save against the area's caster's spell (SaveRules with the caster: the protection ward, SPL-018).
            int reflex = SaveRules.Modifier(mover.Stats, SavingThrowType.Reflex,
                SaveContext.ForSpellId(SpellNames.GREASE, greaseCaster != null ? greaseCaster.Stats : null)); // CHR-018
            int total = roll + reflex;
            bool success = total >= saveDC;

            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🛢", $"Grease check ({mover.Stats.CharacterName}): Reflex d20({roll}) + {reflex} = {total} vs DC {saveDC} {(success ? "SUCCESS" : "FAIL")}."));

            if (!success)
            {
                mover.ApplyCondition(CombatConditionType.Prone, -1, "Grease");
                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("💥", $"{mover.Stats.CharacterName} slips in grease, falls prone, and movement ends immediately."));
                return false;
            }
        }

        return true;
    }

    private void TickActiveGreaseEffects()
    {
        // Wind can disperse persistent fog effects before area effects tick.
        WindEffectManager.Instance.OnRoundStart();
        AreaEffectManager.Instance.OnCombatRoundStart();

        if (_activeGreasedObjects.Count > 0)
            TickGreasedObjectsAtRoundStart();
    }

    private void ClearAllActiveGreaseEffects()
    {
        AreaEffectManager.Instance.ClearAllEffects();
        WindEffectManager.Instance.ClearAllWindEffects();
        _activeGreasedObjects.Clear();
    }

    private string ResolveGreaseObjectCast(CharacterController caster, CharacterController target, SpellData spell)
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

        int saveDC = GetSpellSaveDC(caster, spell);
        // The spell save (RollSpellSave: the voluntary save, PHB p.177, and the spell-and-caster bonuses, SPL-018).
        SaveResult greaseSave = RollSpellSave(caster, target, spell, SaveType.Reflex, saveDC);
        int roll = greaseSave.Roll;
        int greaseReflexMod = greaseSave.Modifier;
        int total = greaseSave.Total;
        bool saveSucceeded = greaseSave.Saved;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts Grease on {target.Stats.CharacterName}'s {targetItem.Name}!");
        sb.AppendLine(greaseSave.Forgone
            ? "Reflex save: given up (PHB p.177) FAIL"
            : $"Reflex save: d20({roll}) + {greaseReflexMod} = {total} vs DC {saveDC} {(saveSucceeded ? "SUCCESS" : "FAIL")}");
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

        int durationRounds = GetGreaseDurationRounds(caster, spell);
        RegisterGreasedObject(targetItem, durationRounds, saveDC, caster);
        sb.AppendLine($"{targetItem.Name} remains slick for {durationRounds} rounds.");
        sb.Append("═══════════════════════════════");

        return sb.ToString();
    }

    private void RegisterGreasedObject(ItemData item, int durationRounds, int saveDC, CharacterController caster)
    {
        if (item == null)
            return;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;

        for (int i = 0; i < _activeGreasedObjects.Count; i++)
        {
            ActiveGreasedObject existing = _activeGreasedObjects[i];
            if (existing != null && ReferenceEquals(existing.Item, item))
            {
                existing.RemainingRounds = Mathf.Max(existing.RemainingRounds, Mathf.Max(1, durationRounds));
                existing.SaveDC = Mathf.Max(existing.SaveDC, saveDC);
                existing.CasterName = casterName;
                existing.Caster = caster;
                return;
            }
        }

        _activeGreasedObjects.Add(new ActiveGreasedObject
        {
            Item = item,
            RemainingRounds = Mathf.Max(1, durationRounds),
            SaveDC = saveDC,
            CasterName = casterName,
            Caster = caster
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
                int roll = DiceRoller.D20();
                int reflex = SaveRules.Modifier(holder.Stats, SavingThrowType.Reflex,
                    SaveContext.ForSpellId(SpellNames.GREASE, effect.Caster != null ? effect.Caster.Stats : null)); // CHR-018, SPL-018
                int total = roll + reflex;
                bool saveSucceeded = total >= effect.SaveDC;
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🛢", $"Greased item check: {holder.Stats.CharacterName} d20({roll}) + {reflex} = {total} vs DC {effect.SaveDC} {(saveSucceeded ? "SUCCESS" : "FAIL")}."));

                if (!saveSucceeded)
                {
                    ItemData dropped = holder.Inventory != null ? holder.Inventory.RemoveEquippedHeldItem(slot) : null;
                    if (dropped != null)
                    {
                        SquareCell cell = Grid != null ? Grid.GetCell(holder.GridPosition) : null;
                        cell?.AddGroundItem(dropped);
                        CombatUI?.ShowCombatLog(CombatLogHelper.Info("💨", $"{holder.Stats.CharacterName} drops {dropped.Name} (too slippery to hold)."));
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

        int roll = DiceRoller.D20();
        int reflex = SaveRules.Modifier(actor.Stats, SavingThrowType.Reflex,
            SaveContext.ForSpellId(SpellNames.GREASE, effect.Caster != null ? effect.Caster.Stats : null)); // CHR-018, SPL-018
        int total = roll + reflex;
        bool saveSucceeded = total >= effect.SaveDC;

        if (!saveSucceeded)
        {
            failureReason = $"{actor.Stats.CharacterName} can't keep hold of {item.Name}: Reflex d20({roll}) + {reflex} = {total} vs DC {effect.SaveDC}.";
            return false;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🛢", $"{actor.Stats.CharacterName} steadies {item.Name}: Reflex d20({roll}) + {reflex} = {total} vs DC {effect.SaveDC}."));
        return true;
    }
}
