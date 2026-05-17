using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Wall of Ice (PHB 3.5e p.299): Evocation [Cold].
///
/// Creates a plane of ice or hemisphere.
/// Supports TWO shapes per PHB:
///   1) WALL (Line Mode): An anchored plane of ice, up to one 10-ft square
///      per caster level (2 squares per CL). 1 inch thick/CL, hardness 0, 3 HP/inch.
///   2) HEMISPHERE (Circle Mode): A hemisphere with radius up to (3 + CL) feet.
///      Convert to squares: floor((3 + CL) / 5).
///
/// Per-Cell HP Tracking (PHB p.299):
///   • Each 10-ft section (cell) has HP = 3 × caster level
///   • Sections can be individually attacked (auto-hit, no attack roll)
///   • When a section reaches 0 HP, it becomes "breached"
///   • Breached sections allow movement through
///   • Line mode: Moving through breached section deals 1d6+CL cold damage (no save)
///   • Circle mode: Moving through breached section deals NO damage
///   • A creature can attempt a Strength check (DC 15+CL) to breach a section
///
/// Movement Blocking:
///   • Intact cells block all movement
///   • Breached cells allow movement (with damage in Line mode)
///   • Wall blocks line of sight/effect through intact cells
///
/// Duration: 1 min/level
/// </summary>
public class WallOfIceAreaEffect : PersistentAreaEffect
{
    /// <summary>Anchor cell of the wall.</summary>
    public Vector2Int CenterCell { get; set; }

    /// <summary>Length of the wall in squares (Line mode only).</summary>
    public int LengthSquares { get; set; } = 8;

    /// <summary>Direction the wall faces (Line mode).</summary>
    public Vector2Int WallDirection { get; set; } = new Vector2Int(1, 0);

    /// <summary>True if this wall was created in Circle mode (hemisphere).</summary>
    public bool IsCircleMode { get; set; }

    /// <summary>Radius of the circle in squares (Circle mode only).</summary>
    public int CircleRadius { get; set; } = 1;

    /// <summary>Total HP of the wall (legacy - sum of all cells). Use per-cell HP instead.</summary>
    public int WallHP { get; set; }

    /// <summary>Maximum HP of the wall (legacy).</summary>
    public int WallMaxHP { get; set; }

    /// <summary>Thickness in inches (= caster level).</summary>
    public int ThicknessInches { get; set; }

    // ═══════════════════════════════════════════════════
    // PER-CELL HP TRACKING
    // ═══════════════════════════════════════════════════

    /// <summary>Per-cell HP. Each cell starts at 3 × CL HP.</summary>
    private Dictionary<Vector2Int, int> _cellHitPoints = new Dictionary<Vector2Int, int>();

    /// <summary>Maximum HP per cell (3 × CL).</summary>
    public int CellMaxHP { get; private set; }

    /// <summary>Set of breached (destroyed) cells that allow movement through.</summary>
    private HashSet<Vector2Int> _breachedCells = new HashSet<Vector2Int>();

    private HashSet<Vector2Int> _pendingExplicitCells;

    protected override Color GridHighlightColor => AreaEffectColors.WallOfIce;
    protected override bool UseGridHighlighting => true;

    /// <summary>Color for breached cells — uses standardized palette from AreaEffectColors.</summary>
    private static Color BreachedCellColor => AreaEffectColors.WallOfIceBreached;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Wall of Ice";
        SpellId = SpellNames.WALL_OF_ICE;
        // Shape is set in Start() based on IsCircleMode
        Shape = AreaShape.Line;
        SizeY = 1;

        ShowVisual = false;
    }

    protected override void Start()
    {
        // Configure shape based on circle vs line mode
        if (IsCircleMode)
        {
            Shape = AreaShape.Circle;
            EffectName = "Wall of Ice (Hemisphere)";
            SizeX = Mathf.Max(1, CircleRadius * 2);
            SizeY = SizeX;
        }
        else
        {
            Shape = AreaShape.Line;
            WallDirection = NormalizeWallDirection(WallDirection);
            DirectionAngle = DirectionFromVector(WallDirection);
            SizeX = Mathf.Max(2, LengthSquares);
        }

        // Calculate wall HP: 3 HP per inch of thickness (= 3 × CL per cell)
        ThicknessInches = Mathf.Max(1, CasterLevel);
        CellMaxHP = ThicknessInches * 3;
        WallMaxHP = CellMaxHP; // Legacy compatibility
        WallHP = WallMaxHP;

        // IMPORTANT: base.Start() calls CalculateAffectedCells() then ApplyInitialEffect().
        // Our override of CalculateAffectedCells() applies _pendingExplicitCells BEFORE
        // ApplyInitialEffect() runs.
        base.Start();

        // Initialize per-cell HP after AffectedCells are set
        InitializeCellHitPoints();
    }

    /// <summary>
    /// Initialize per-cell HP tracking for all affected cells.
    /// </summary>
    private void InitializeCellHitPoints()
    {
        _cellHitPoints.Clear();
        _breachedCells.Clear();

        if (AffectedCells == null) return;

        foreach (Vector2Int cell in AffectedCells)
        {
            _cellHitPoints[cell] = CellMaxHP;
        }

        Debug.Log($"[WallOfIce] Initialized per-cell HP: {_cellHitPoints.Count} cells × {CellMaxHP} HP each");
    }

    /// <summary>
    /// Override CalculateAffectedCells to use explicit cells (circle perimeter) when available.
    /// This ensures AffectedCells is correct BEFORE ApplyInitialEffect() runs in base.Start().
    /// </summary>
    protected override void CalculateAffectedCells()
    {
        if (_pendingExplicitCells != null && _pendingExplicitCells.Count > 0)
        {
            AffectedCells = new HashSet<Vector2Int>(_pendingExplicitCells);
            _pendingExplicitCells = null;
            Debug.Log($"[WallOfIce] CalculateAffectedCells: Used {AffectedCells.Count} explicit cells instead of default calculation");
            return;
        }

        base.CalculateAffectedCells();
        Debug.Log($"[WallOfIce] CalculateAffectedCells: Calculated {AffectedCells.Count} cells via base");
    }

    protected override void OnAreaCreated()
    {
        if (IsCircleMode)
        {
            LogEffect($"❄ A hemisphere of ice forms ({CircleRadius * 5}-ft radius, {ThicknessInches} inches thick)!");
        }
        else
        {
            LogEffect($"❄ A wall of ice forms ({SizeX * 5} ft long, {ThicknessInches} inches thick)!");
        }
        LogEffect($"  HP per section: {CellMaxHP} (Hardness 0, 3 HP per inch × {ThicknessInches} inches)");
        LogEffect("  • Blocks movement through intact wall cells");
        LogEffect("  • Creatures caught in wall take cold damage equal to caster level");
        LogEffect("  • Each section can be attacked individually (auto-hit)");
        LogEffect("  • Breached sections allow movement through");
        if (!IsCircleMode)
            LogEffect("  • Moving through breached sections (Line mode) deals 1d6+CL cold damage");
        LogEffect("  • Strength check DC " + (15 + CasterLevel) + " to breach a section");
    }

    private void Update()
    {
        UpdateCharacterTracking();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    public override void OnRoundStart()
    {
        base.OnRoundStart();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        if (isInitial)
        {
            // Creatures caught when wall forms take CL cold damage (1 HP per CL)
            int coldDamage = Mathf.Max(1, CasterLevel);
            character.Stats.TakeDamage(coldDamage);

            LogEffect($"  ❄ {character.Stats.CharacterName} is caught in the forming Wall of Ice: {coldDamage} cold damage!");

            if (character.Stats.IsDead)
            {
                character.OnDeath();
                LogEffect($"  💀 {character.Stats.CharacterName} is frozen to death!");
            }
        }
        else
        {
            LogEffect($"  {character.Stats.CharacterName} is adjacent to the Wall of Ice.");
        }
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        LogEffect($"{character.Stats.CharacterName} moves away from the Wall of Ice.");
    }

    protected override void OnAreaExpires()
    {
        RemoveGridHighlight();
        LogEffect("The Wall of Ice melts away.");
    }

    // ═══════════════════════════════════════════════════
    // PER-CELL HP AND BREACH METHODS
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Get the current HP of a specific cell. Returns 0 if breached or cell not found.
    /// </summary>
    public int GetCellHP(Vector2Int cell)
    {
        if (_breachedCells.Contains(cell)) return 0;
        return _cellHitPoints.TryGetValue(cell, out int hp) ? hp : 0;
    }

    /// <summary>
    /// Check if a specific cell is breached (HP reached 0).
    /// </summary>
    public bool IsBreached(Vector2Int cell)
    {
        return _breachedCells.Contains(cell);
    }

    /// <summary>
    /// Check if a cell is part of this wall and still intact (not breached).
    /// </summary>
    public bool IsCellIntact(Vector2Int cell)
    {
        return AffectedCells != null && AffectedCells.Contains(cell) && !_breachedCells.Contains(cell);
    }

    /// <summary>
    /// Deal damage to a specific cell. Returns true if the cell is breached.
    /// </summary>
    public bool DamageCellHP(Vector2Int cell, int damage, bool isFire = false)
    {
        if (!_cellHitPoints.ContainsKey(cell) || _breachedCells.Contains(cell))
        {
            Debug.Log($"[WallOfIce] DamageCellHP: Cell ({cell.x},{cell.y}) not found or already breached");
            return _breachedCells.Contains(cell);
        }

        int actualDamage = Mathf.Max(0, damage);
        _cellHitPoints[cell] -= actualDamage;

        string dmgType = isFire ? " fire" : "";
        int remaining = Mathf.Max(0, _cellHitPoints[cell]);
        Debug.Log($"[WallOfIce] Cell ({cell.x},{cell.y}) takes {actualDamage}{dmgType} damage: {remaining}/{CellMaxHP} HP remaining");

        if (_cellHitPoints[cell] <= 0)
        {
            BreachCell(cell);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Mark a cell as breached (destroyed). Updates visuals.
    /// </summary>
    private void BreachCell(Vector2Int cell)
    {
        _cellHitPoints[cell] = 0;
        _breachedCells.Add(cell);

        Debug.Log($"[WallOfIce] Cell ({cell.x},{cell.y}) BREACHED! Total breached: {_breachedCells.Count}/{(_cellHitPoints.Count)}");
        LogEffect($"  💥 Wall of Ice section at ({cell.x},{cell.y}) is breached!");

        // Update visual for this cell
        UpdateBreachedCellHighlight(cell);

        // Check if all cells are breached — if so, destroy the whole wall
        if (_breachedCells.Count >= _cellHitPoints.Count)
        {
            LogEffect("  💥 The entire Wall of Ice has been breached and shatters!");
            ExpireEffect();
        }
    }

    /// <summary>
    /// Update the highlight color for a breached cell.
    /// </summary>
    private void UpdateBreachedCellHighlight(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance) return;

        SquareGrid grid = GameManager.Instance.Grid;
        if (grid == null) return;

        SquareCell gridCell = grid.GetCell(cell);
        if (gridCell != null)
        {
            gridCell.SetHighlight(BreachedCellColor);
        }
    }

    /// <summary>
    /// Override grid highlighting to show breached cells differently.
    /// </summary>
    protected override void ApplyGridHighlight()
    {
        if (!UseGridHighlighting)
            return;

        if (gameManager == null)
            gameManager = GameManager.Instance != null ? GameManager.Instance : FindObjectOfType<GameManager>();

        if (gameManager == null || gameManager.Grid == null)
        {
            gridHighlightApplied = false;
            return;
        }

        RemoveGridHighlight();

        foreach (Vector2Int cell in AffectedCells)
        {
            SquareCell gridCell = gameManager.Grid.GetCell(cell);
            if (gridCell == null) continue;

            if (_breachedCells.Contains(cell))
            {
                gridCell.SetHighlight(BreachedCellColor);
            }
            else
            {
                gridCell.SetHighlight(GridHighlightColor);
            }

            highlightedCells.Add(gridCell);
        }

        gridHighlightApplied = highlightedCells.Count > 0;
    }

    // ═══════════════════════════════════════════════════
    // AOE / TARGETED CELL DAMAGE
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Deals damage ONLY to the wall cells that actually overlap the given AoE cells.
    /// Each overlapping intact cell takes the full damage (objects in an AoE each take damage).
    /// Fire damage is especially effective (per PHB).
    /// Returns true if all remaining wall sections are destroyed.
    /// </summary>
    public bool DealDamageToOverlappingCells(int damage, HashSet<Vector2Int> aoeCells, bool isFire = false)
    {
        if (aoeCells == null || aoeCells.Count == 0)
            return false;

        // Find only the intact cells that overlap with the AoE
        var cellsToHit = new List<Vector2Int>();
        foreach (Vector2Int cell in aoeCells)
        {
            if (_cellHitPoints.ContainsKey(cell) && !_breachedCells.Contains(cell) && _cellHitPoints[cell] > 0)
                cellsToHit.Add(cell);
        }

        if (cellsToHit.Count == 0)
        {
            // No intact cells overlapping — check if entire wall is gone
            if (_breachedCells.Count >= _cellHitPoints.Count)
            {
                LogEffect("  💥 The Wall of Ice shatters!");
                ExpireEffect();
                return true;
            }
            return false;
        }

        string dmgType = isFire ? "fire" : "";
        LogEffect($"  Wall of Ice takes {damage} {dmgType} damage to {cellsToHit.Count} overlapping section(s)!");

        bool anyBreached = false;
        foreach (Vector2Int cell in cellsToHit)
        {
            if (DamageCellHP(cell, damage, isFire))
                anyBreached = true;
        }

        // Update legacy WallHP
        UpdateLegacyWallHP();

        if (WallHP <= 0 || _breachedCells.Count >= _cellHitPoints.Count)
        {
            LogEffect("  💥 The Wall of Ice shatters!");
            ExpireEffect();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Legacy method — deals damage to ALL intact cells (use DealDamageToOverlappingCells for AoE).
    /// Kept for backward compatibility but should rarely be called directly.
    /// </summary>
    public bool DealDamageToWall(int damage, bool isFire = false)
    {
        // Collect all intact cells
        var intactCells = new List<Vector2Int>();
        foreach (var kvp in _cellHitPoints)
        {
            if (!_breachedCells.Contains(kvp.Key) && kvp.Value > 0)
                intactCells.Add(kvp.Key);
        }

        if (intactCells.Count == 0)
        {
            LogEffect("  💥 The Wall of Ice shatters!");
            ExpireEffect();
            return true;
        }

        // Apply full damage to each intact cell
        string dmgType = isFire ? "fire" : "";
        LogEffect($"  Wall of Ice takes {damage} {dmgType} damage across ALL {intactCells.Count} section(s)!");

        bool anyBreached = false;
        foreach (Vector2Int cell in intactCells)
        {
            if (DamageCellHP(cell, damage, isFire))
                anyBreached = true;
        }

        UpdateLegacyWallHP();

        if (WallHP <= 0 || _breachedCells.Count >= _cellHitPoints.Count)
        {
            LogEffect("  💥 The Wall of Ice shatters!");
            ExpireEffect();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Update the legacy WallHP field to reflect total remaining HP across all intact cells.
    /// </summary>
    private void UpdateLegacyWallHP()
    {
        int totalHP = 0;
        foreach (var kvp in _cellHitPoints)
        {
            if (!_breachedCells.Contains(kvp.Key))
                totalHP += Mathf.Max(0, kvp.Value);
        }
        WallHP = totalHP;
    }

    // ═══════════════════════════════════════════════════
    // ATTACK TARGETING (per-cell)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Called when a character attacks a specific wall cell. Auto-hit, apply damage.
    /// Returns true if the cell is breached by this attack.
    /// </summary>
    public bool OnCellAttacked(CharacterController attacker, Vector2Int cell, int damage, bool isFire = false)
    {
        if (attacker == null) return false;

        string attackerName = attacker.Stats != null ? attacker.Stats.CharacterName : "Unknown";

        if (_breachedCells.Contains(cell))
        {
            Debug.Log($"[WallOfIce] {attackerName} attacks already-breached cell ({cell.x},{cell.y})");
            LogEffect($"⚠ {attackerName} attacks an already-breached Wall of Ice section at ({cell.x},{cell.y}).");
            return true;
        }

        int hpBefore = GetCellHP(cell);
        bool breached = DamageCellHP(cell, damage, isFire);
        int hpAfter = GetCellHP(cell);

        if (breached)
        {
            LogEffect($"⚔️ {attackerName} attacks Wall of Ice section ({cell.x},{cell.y}) — {damage} damage dealt, section BREACHED!");
        }
        else
        {
            LogEffect($"⚔️ {attackerName} attacks Wall of Ice section ({cell.x},{cell.y}) — {damage} damage dealt, {hpAfter}/{CellMaxHP} HP remaining");
        }

        return breached;
    }

    // ═══════════════════════════════════════════════════
    // STRENGTH CHECK TO BREACH
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// DC for Strength check to breach a section: 15 + caster level.
    /// </summary>
    public int GetStrengthCheckDC()
    {
        return 15 + CasterLevel;
    }

    /// <summary>
    /// Attempt a Strength check to breach a wall section.
    /// Returns true if the check succeeds (section is breached).
    /// </summary>
    public bool AttemptStrengthCheck(CharacterController character, Vector2Int cell)
    {
        if (character == null || character.Stats == null) return false;

        string charName = character.Stats.CharacterName;

        if (!AffectedCells.Contains(cell))
        {
            Debug.Log($"[WallOfIce] {charName} tries Strength check on non-wall cell ({cell.x},{cell.y})");
            return false;
        }

        if (_breachedCells.Contains(cell))
        {
            LogEffect($"⚠ {charName} tries to breach an already-breached Wall of Ice section at ({cell.x},{cell.y}).");
            return true;
        }

        int dc = GetStrengthCheckDC();
        int strMod = CharacterStats.GetModifier(character.Stats.STR);
        int roll = UnityEngine.Random.Range(1, 21);
        int total = roll + strMod;
        bool success = total >= dc;

        if (success)
        {
            LogEffect($"💪 {charName} smashes through Wall of Ice at ({cell.x},{cell.y})! Strength check: d20({roll}) + {strMod} = {total} vs DC {dc} — SUCCESS!");
            BreachCell(cell);
        }
        else
        {
            LogEffect($"💪 {charName} fails to breach Wall of Ice at ({cell.x},{cell.y}). Strength check: d20({roll}) + {strMod} = {total} vs DC {dc} — FAILURE!");
        }

        return success;
    }

    // ═══════════════════════════════════════════════════
    // BREACH PASS-THROUGH DAMAGE
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Apply cold damage when a creature moves through a breached cell.
    /// Line mode: 1d6 + CL cold damage (no save).
    /// Circle mode: No damage.
    /// </summary>
    public void ApplyBreachPassThroughDamage(CharacterController character, Vector2Int cell)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        if (!_breachedCells.Contains(cell))
            return;

        // Circle mode: no damage for passing through breached sections
        if (IsCircleMode)
        {
            Debug.Log($"[WallOfIce] {character.Stats.CharacterName} passes through breached hemisphere section at ({cell.x},{cell.y}) — no damage (Circle mode)");
            return;
        }

        // Line mode: 1d6 + CL cold damage
        int coldDamage = UnityEngine.Random.Range(1, 7) + CasterLevel;
        character.Stats.TakeDamage(coldDamage);

        LogEffect($"❄️ {character.Stats.CharacterName} takes {coldDamage} cold damage passing through breached Wall of Ice at ({cell.x},{cell.y})");

        if (character.Stats.IsDead)
        {
            character.OnDeath();
            LogEffect($"  💀 {character.Stats.CharacterName} is frozen to death passing through the Wall of Ice!");
        }
    }

    // ═══════════════════════════════════════════════════
    // MOVEMENT BLOCKING
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Check if movement to a cell is blocked by any intact Wall of Ice.
    /// Returns true if the cell is blocked (intact wall section).
    /// Breached cells do NOT block movement.
    /// </summary>
    public static bool DoesCellBlockMovement(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.IsCellIntact(cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Check if a cell is a breached Wall of Ice section (triggers pass-through damage in Line mode).
    /// </summary>
    public static bool IsCellBreachedWallOfIce(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.IsBreached(cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Apply breach pass-through damage for a creature moving into a breached cell.
    /// Should be called from movement system when creature enters a breached wall cell.
    /// </summary>
    public static void ApplyBreachDamageAtCell(CharacterController character, Vector2Int cell)
    {
        if (character == null || !AreaEffectManager.HasInstance)
            return;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null) return;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.IsBreached(cell))
            {
                wall.ApplyBreachPassThroughDamage(character, cell);
                return; // Only apply damage once per cell
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // LINE OF SIGHT / SPELL BLOCKING
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Check if an intact Wall of Ice cell blocks line of sight/effect.
    /// Intact cells block LOS; breached cells do not.
    /// </summary>
    public static bool DoesCellBlockLineOfSight(Vector2Int cell)
    {
        return DoesCellBlockMovement(cell); // Same logic: intact blocks, breached allows
    }

    // ═══════════════════════════════════════════════════
    // CREATION VALIDATION
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Check if all cells in the proposed wall area are valid for wall creation.
    /// Wall cannot be created if any cell is occupied by a creature or blocking object.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    public static string ValidateWallCreation(HashSet<Vector2Int> proposedCells, SquareGrid grid)
    {
        if (proposedCells == null || proposedCells.Count == 0)
            return "No cells specified for wall creation.";

        if (grid == null)
            return "Grid not available.";

        foreach (Vector2Int cell in proposedCells)
        {
            SquareCell gridCell = grid.GetCell(cell);
            if (gridCell == null)
                continue; // Off-grid cells are fine (wall just doesn't extend there)

            if (gridCell.IsOccupied)
            {
                // Check who/what is occupying
                IReadOnlyList<CharacterController> occupants = gridCell.Occupants;
                for (int i = 0; i < occupants.Count; i++)
                {
                    CharacterController occupant = occupants[i];
                    if (occupant != null && !occupant.Stats.IsDead)
                    {
                        string name = occupant.Stats.CharacterName;
                        Debug.Log($"[WallOfIce] Creation blocked: cell ({cell.x},{cell.y}) occupied by {name}");
                        return $"⚠ Wall of Ice cannot be created — path blocked by {name} at ({cell.x},{cell.y})";
                    }
                }
            }
        }

        return null; // Valid — no blockers
    }

    // ═══════════════════════════════════════════════════
    // STATIC UTILITY METHODS
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Checks if any active Wall of Ice contains the supplied cell.
    /// </summary>
    public static bool IsCellInAnyWallOfIce(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null && wall.AffectedCells.Contains(cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the WallOfIceAreaEffect that occupies the given cell, or null if none.
    /// </summary>
    public static WallOfIceAreaEffect GetWallAtCell(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return null;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return null;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null && wall.AffectedCells.Contains(cell))
                return wall;
        }
        return null;
    }

    /// <summary>
    /// Returns all cells occupied by any active Wall of Ice (including breached).
    /// </summary>
    public static HashSet<Vector2Int> GetAllWallOfIceCells()
    {
        var result = new HashSet<Vector2Int>();
        if (!AreaEffectManager.HasInstance)
            return result;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return result;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null)
            {
                foreach (Vector2Int cell in wall.AffectedCells)
                    result.Add(cell);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns all INTACT cells of any active Wall of Ice (excludes breached).
    /// Used for attack targeting — only intact cells should be targetable.
    /// </summary>
    public static HashSet<Vector2Int> GetAllIntactWallOfIceCells()
    {
        var result = new HashSet<Vector2Int>();
        if (!AreaEffectManager.HasInstance)
            return result;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return result;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null)
            {
                foreach (Vector2Int cell in wall.AffectedCells)
                {
                    if (!wall._breachedCells.Contains(cell))
                        result.Add(cell);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Returns a short description of the wall for UI display.
    /// </summary>
    public string GetWallInfoString()
    {
        string mode = IsCircleMode ? "Hemisphere" : "Wall";
        int intactCount = _cellHitPoints.Count - _breachedCells.Count;
        return $"Wall of Ice ({mode}) — {intactCount}/{_cellHitPoints.Count} sections intact ({CellMaxHP} HP each)";
    }

    /// <summary>
    /// Get info string for a specific cell.
    /// </summary>
    public string GetCellInfoString(Vector2Int cell)
    {
        if (_breachedCells.Contains(cell))
            return $"Wall of Ice ({cell.x},{cell.y}) — BREACHED";
        if (_cellHitPoints.TryGetValue(cell, out int hp))
            return $"Wall of Ice ({cell.x},{cell.y}) — {hp}/{CellMaxHP} HP";
        return $"Wall of Ice ({cell.x},{cell.y}) — not part of wall";
    }

    public void SetExplicitCells(HashSet<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return;

        bool hasFinishedStart = AffectedCells != null && AffectedCells.Count > 0
            && AreaEffectManager.HasInstance
            && AreaEffectManager.Instance.GetAllAreaEffects().Contains(this);

        if (hasFinishedStart)
        {
            AffectedCells = new HashSet<Vector2Int>(cells);
            InitializeCellHitPoints();
            RemoveGridHighlight();
            ApplyGridHighlight();
        }
        else
        {
            _pendingExplicitCells = new HashSet<Vector2Int>(cells);
        }
    }

    private static Vector2Int NormalizeWallDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return new Vector2Int(1, 0);

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return new Vector2Int(direction.x >= 0 ? 1 : -1, 0);

        return new Vector2Int(0, direction.y >= 0 ? 1 : -1);
    }

    private static float DirectionFromVector(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return 0f;

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
}
