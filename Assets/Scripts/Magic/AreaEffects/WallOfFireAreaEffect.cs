using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Wall of Fire (PHB 3.5e p.298): Evocation [Fire].
///
/// Creates an immobile, blazing curtain of shimmering violet fire.
/// Supports TWO shapes per PHB:
///   1) WALL (Line Mode): An opaque sheet of flame up to 20 ft/level long.
///      The caster picks start and end points within Medium range.
///   2) RING (Ring Mode): A ring of fire with radius up to 5 ft per 2 CL.
///      The caster picks a center point and radius.
///
/// Damage:
///   • Heat wave (hot side only, NO save):
///       Band 1: 2d4 fire damage to creatures within 10 ft (1-2 squares) of hot side
///       Band 2: 1d4 fire damage to creatures within 10-20 ft (3-4 squares) of hot side
///       Cool side: NO heat wave damage at all
///   • Pass-through: 2d6+CL (max +20) fire damage to creatures passing through (NO save)
///   • Undead take double damage from Wall of Fire (PHB 3.5e)
///   • Multi-square creatures only take damage once per entry/stay
///   • Wall is opaque: blocks line of sight, provides 20% concealment (miss chance) for attacks through
///   • Duration: Concentration + 1 round/level
///   • A wall section (cell) that takes 20+ cold damage in a single round is extinguished
///
/// Heat wave damage is dealt on the hot side only:
///   - At time of casting (wall creation)
///   - At the beginning of the caster's turn each round
/// </summary>
public class WallOfFireAreaEffect : PersistentAreaEffect
{
    /// <summary>Anchor cell of the wall.</summary>
    public Vector2Int CenterCell { get; set; }

    /// <summary>Length of the wall in squares (Line mode only).</summary>
    public int LengthSquares { get; set; } = 8;

    /// <summary>Direction the wall faces (Line mode).</summary>
    public Vector2Int WallDirection { get; set; } = new Vector2Int(1, 0);

    /// <summary>True if this wall was created in Ring mode (circle perimeter).</summary>
    public bool IsRingMode { get; set; }

    /// <summary>Radius of the ring in squares (Ring mode only).</summary>
    public int RingRadius { get; set; } = 1;

    /// <summary>
    /// Heat wave direction for Ring mode: "Inwards" or "Outwards" (PHB p.298).
    /// Inwards = heat radiates toward center (damages creatures inside the ring).
    /// Outwards = heat radiates away from center (damages creatures outside the ring).
    /// Null = not yet selected (defaults to "Inwards" for backward compat).
    /// </summary>
    public string HeatWaveDirectionRing { get; set; }

    /// <summary>
    /// Heat wave direction for Line mode: perpendicular normal vector pointing
    /// toward the "hot" side of the wall (PHB p.298).
    /// Null = not yet selected (defaults to no heat wave direction).
    /// </summary>
    public Vector2? HeatWaveDirectionLine { get; set; }

    private HashSet<Vector2Int> _pendingExplicitCells;

    // ── Per-creature damage tracking ──
    // Tracks which creatures have already been damaged THIS round (for standing-in-wall)
    // so multi-square creatures only take damage once per round/entry.
    private HashSet<CharacterController> _damagedThisRound = new HashSet<CharacterController>();
    // Tracks which creatures were damaged on entry (pass-through) during the current
    // Update() cycle, reset when they leave the wall so re-entry damages again.
    private HashSet<CharacterController> _damagedOnEntry = new HashSet<CharacterController>();

    // ── Heat wave damage tracking ──
    // Tracks which creatures have already taken heat wave damage this trigger
    // to prevent multi-square creatures from being hit multiple times.
    private HashSet<CharacterController> _heatWaveDamagedThisTrigger = new HashSet<CharacterController>();
    // Whether we've already subscribed to the TurnStartedEvent
    private bool _subscribedToTurnStart;

    // ── Cold damage extinguishing ──
    // Tracks accumulated cold damage per cell this round. If any cell reaches 20+, it is extinguished.
    private Dictionary<Vector2Int, int> _coldDamagePerCell = new Dictionary<Vector2Int, int>();
    // Cells that have been extinguished (no longer deal damage or block).
    private HashSet<Vector2Int> _extinguishedCells = new HashSet<Vector2Int>();

    /// <summary>Threshold of cold damage in a single round to extinguish a wall cell.</summary>
    private const int ColdDamageExtinguishThreshold = 20;

    protected override Color GridHighlightColor => AreaEffectColors.WallOfFire;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Wall of Fire";
        SpellId = SpellNames.WALL_OF_FIRE;
        // Shape is set in Start() based on IsRingMode
        Shape = AreaShape.Line;
        SizeY = 1; // 5 ft wide

        ShowVisual = false;
    }

    protected override void Start()
    {
        // Configure shape based on ring vs line mode
        if (IsRingMode)
        {
            Shape = AreaShape.Circle;
            EffectName = "Wall of Fire (Ring)";
            SizeX = Mathf.Max(1, RingRadius * 2);
            SizeY = SizeX;
        }
        else
        {
            Shape = AreaShape.Line;
            WallDirection = NormalizeWallDirection(WallDirection);
            DirectionAngle = DirectionFromVector(WallDirection);
            SizeX = Mathf.Max(2, LengthSquares);
        }

        // IMPORTANT: base.Start() calls CalculateAffectedCells() then ApplyInitialEffect().
        // Our override of CalculateAffectedCells() applies _pendingExplicitCells BEFORE
        // ApplyInitialEffect() runs, so creatures inside a ring (but NOT on wall cells)
        // won't incorrectly receive pass-through damage on creation.
        base.Start();

        // Deal initial heat wave damage AFTER base.Start() (PHB p.298).
        // AffectedCells now contains only the ring perimeter (not a filled circle)
        // so IsCharacterInArea correctly identifies only creatures standing ON the ring.
        TriggerHeatWaveDamage("on creation");
    }

    /// <summary>
    /// Override CalculateAffectedCells to use explicit cells (ring perimeter) when available.
    /// This ensures AffectedCells is correct BEFORE ApplyInitialEffect() runs in base.Start(),
    /// preventing creatures inside the ring (but not on wall cells) from taking pass-through damage.
    /// </summary>
    protected override void CalculateAffectedCells()
    {
        if (_pendingExplicitCells != null && _pendingExplicitCells.Count > 0)
        {
            AffectedCells = new HashSet<Vector2Int>(_pendingExplicitCells);
            _pendingExplicitCells = null;
            Debug.Log($"[WallOfFire] CalculateAffectedCells: Used {AffectedCells.Count} explicit cells (ring perimeter) instead of filled circle");
            return;
        }

        base.CalculateAffectedCells();
        Debug.Log($"[WallOfFire] CalculateAffectedCells: Calculated {AffectedCells.Count} cells via base (line mode or no explicit cells)");
    }

    protected override void OnAreaCreated()
    {
        if (IsRingMode)
        {
            string ringDir = GetEffectiveRingDirection();
            LogEffect($"🔥 A blazing ring of fire appears ({RingRadius * 5}-ft radius, heat {ringDir.ToLower()})!");
        }
        else
        {
            string lineDir = HeatWaveDirectionLine.HasValue ? $", heat side selected" : "";
            LogEffect($"🔥 A blazing wall of fire appears ({SizeX * 5} ft long{lineDir})!");
        }
        LogEffect("  • Hot side: 2d4 fire within 10 ft, 1d4 fire within 10-20 ft [No save]");
        LogEffect("  • Cool side: no heat wave damage");
        LogEffect("  • Pass-through: 2d6+CL (max +20) fire damage [No save]");
        LogEffect("  • Undead take double damage");
        LogEffect("  • Wall is opaque — blocks line of sight, 20% concealment for attacks through");
        LogEffect("  • Sections can be extinguished by 20+ cold damage in one round");

        // Subscribe to TurnStartedEvent so we can trigger heat wave at caster's turn start
        SubscribeToTurnStartEvent();

        // NOTE: Initial heat wave damage is triggered from Start() AFTER explicit cells
        // are applied, not here. OnAreaCreated() runs inside base.Start() before
        // _pendingExplicitCells override AffectedCells, so triggering heat wave here
        // would use the wrong cell set (filled circle instead of ring perimeter).
    }

    protected override void OnDestroy()
    {
        UnsubscribeFromTurnStartEvent();
        base.OnDestroy();
    }

    private void Update()
    {
        UpdateCharacterTracking();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    public override void OnRoundStart()
    {
        // Reset per-round damage tracking so creatures can be damaged again this round
        _damagedThisRound.Clear();
        _damagedOnEntry.Clear();

        // Reset cold damage accumulation for this new round
        _coldDamagePerCell.Clear();

        base.OnRoundStart();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // SAFETY CHECK: Verify creature is actually in a wall cell (AffectedCells).
        // This guards against edge cases where the base class tracking might fire
        // before AffectedCells has been narrowed to ring-perimeter-only cells.
        if (!AffectedCells.Contains(character.GridPosition))
        {
            Debug.Log($"[WallOfFire][PassThrough] BLOCKED OnCreatureEntersArea for {character.Stats.CharacterName} "
                + $"at ({character.GridPosition.x},{character.GridPosition.y}) — NOT in AffectedCells (wall cells). "
                + $"Creature is inside ring area but not on a wall cell. No pass-through damage.");
            return;
        }

        // Check if all wall cells this creature occupies are extinguished
        if (AreAllCreatureCellsExtinguished(character))
            return;

        // Per-creature damage tracking: don't double-damage multi-square creatures
        if (_damagedOnEntry.Contains(character))
            return;

        _damagedOnEntry.Add(character);

        string timing = isInitial ? "is caught in" : "passes through";
        Debug.Log($"[WallOfFire][PassThrough] ✓ {character.Stats.CharacterName} at ({character.GridPosition.x},{character.GridPosition.y}) "
            + $"— IS in wall cell, applying PASS-THROUGH damage (context: {timing}, isInitial: {isInitial})");
        DealPassThroughDamage(character, timing);
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // SAFETY CHECK: Verify creature is actually in a wall cell
        if (!AffectedCells.Contains(character.GridPosition))
        {
            Debug.Log($"[WallOfFire][PassThrough] BLOCKED OnCreatureInAreaAtRoundStart for {character.Stats.CharacterName} "
                + $"at ({character.GridPosition.x},{character.GridPosition.y}) — NOT in AffectedCells (wall cells).");
            return;
        }

        // Check if all wall cells this creature occupies are extinguished
        if (AreAllCreatureCellsExtinguished(character))
            return;

        // Per-creature damage tracking: only damage once per round for standing in wall
        if (_damagedThisRound.Contains(character))
            return;

        _damagedThisRound.Add(character);

        Debug.Log($"[WallOfFire][PassThrough] ✓ {character.Stats.CharacterName} at ({character.GridPosition.x},{character.GridPosition.y}) "
            + $"— IS in wall cell at round start, applying PASS-THROUGH damage");
        DealPassThroughDamage(character, "remains in");
    }

    // ════════════════════════════════════════════════════════════════════
    //  PASS-THROUGH Damage: 2d6 + CL fire (NO save — PHB p.298)
    //  Applied when a creature enters or remains in wall cells.
    //  Per PHB 3.5e p.298: "Any creature passing through the wall takes
    //  2d6 points of damage +1 point of damage per caster level (maximum +20)."
    //  NO saving throw — the Reflex save mentioned for Wall of Fire only
    //  applies to the initial casting if it catches a creature.
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Deals 2d6 + CL (max +20) fire damage to a creature in the wall. NO save (PHB p.298).
    /// Undead creatures take double damage (PHB 3.5e).
    /// </summary>
    private void DealPassThroughDamage(CharacterController character, string context)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // Debug: verify creature is actually in a wall cell
        Debug.Log($"[WallOfFire][PassThrough] TRIGGER: {character.Stats.CharacterName} at ({character.GridPosition.x},{character.GridPosition.y}) "
            + $"— context='{context}', InAffectedCells={AffectedCells.Contains(character.GridPosition)}, "
            + $"AffectedCells.Count={AffectedCells.Count}");

        int clBonus = Mathf.Min(CasterLevel, 20);
        int d1 = Random.Range(1, 7);
        int d2 = Random.Range(1, 7);
        int baseDamage = d1 + d2 + clBonus; // 2d6 + CL

        // Undead take double damage from Wall of Fire (PHB 3.5e p.298)
        bool isUndead = IsCreatureUndead(character);
        if (isUndead)
            baseDamage *= 2;

        // NO saving throw for pass-through damage (PHB p.298)
        int finalDamage = Mathf.Max(0, baseDamage);

        if (finalDamage > 0)
            character.Stats.TakeDamage(finalDamage);

        string undeadNote = isUndead ? " [UNDEAD ×2]" : "";
        LogEffect($"🔥 {character.Stats.CharacterName} takes {finalDamage} fire damage from Wall of Fire PASS-THROUGH "
            + $"[2d6({d1}+{d2})+{clBonus}CL={d1 + d2 + clBonus}] [No save]{undeadNote}");

        if (character.Stats.IsDead)
        {
            character.OnDeath();
            LogEffect($"  💀 {character.Stats.CharacterName} is slain by the Wall of Fire!");
        }
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        // Clear entry tracking so the creature takes damage again if it re-enters
        _damagedOnEntry.Remove(character);

        LogEffect($"{character.Stats.CharacterName} moves away from the Wall of Fire.");
    }

    protected override void OnAreaExpires()
    {
        RemoveGridHighlight();
        LogEffect("The Wall of Fire flickers and fades.");
    }

    // ════════════════════════════════════════════════════════════════════
    //  OPACITY / CONCEALMENT (PHB p.298)
    //  Wall of Fire is opaque: blocks line of sight and provides 20%
    //  concealment (miss chance) for attacks that cross active wall cells.
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Checks if an attack line between attacker and target crosses any active
    /// (non-extinguished) Wall of Fire cell. Returns 20 (%) if so, 0 otherwise.
    /// This provides the concealment miss chance per PHB p.298 ("Wall of Fire is opaque").
    /// Integrate into CharacterController.GetMissChance for automatic application.
    /// </summary>
    public static int GetAttackConcealmentMissChance(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
            return 0;

        if (!AreaEffectManager.HasInstance)
            return 0;

        List<WallOfFireAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfFireAreaEffect>();
        if (walls == null || walls.Count == 0)
            return 0;

        Vector2Int from = attacker.GridPosition;
        Vector2Int to = target.GridPosition;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfFireAreaEffect wall = walls[i];
            if (wall == null || wall.AffectedCells == null)
                continue;

            // Build set of active (non-extinguished) cells
            HashSet<Vector2Int> activeCells = wall.GetActiveCells();
            if (activeCells.Count == 0)
                continue;

            // Don't count concealment if attacker or target is standing IN the wall
            if (activeCells.Contains(from) || activeCells.Contains(to))
                continue;

            if (WindWallAreaEffect.LineSegmentCrossesAnyCellPublic(from, to, activeCells))
                return 20; // 20% concealment / miss chance for attacks through the opaque wall
        }

        return 0;
    }

    /// <summary>
    /// Checks if a ranged attack line crosses any active (non-extinguished) Wall of Fire cell.
    /// </summary>
    public static bool BlocksLineOfSight(Vector2Int from, Vector2Int to)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfFireAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfFireAreaEffect>();
        if (walls == null || walls.Count == 0)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfFireAreaEffect wall = walls[i];
            if (wall == null || wall.AffectedCells == null)
                continue;

            HashSet<Vector2Int> activeCells = wall.GetActiveCells();
            if (activeCells.Count > 0 && WindWallAreaEffect.LineSegmentCrossesAnyCellPublic(from, to, activeCells))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the set of active (non-extinguished) wall cells.
    /// </summary>
    private HashSet<Vector2Int> GetActiveCells()
    {
        if (_extinguishedCells.Count == 0)
            return AffectedCells;

        var activeCells = new HashSet<Vector2Int>(AffectedCells);
        activeCells.ExceptWith(_extinguishedCells);
        return activeCells;
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

    /// <summary>Helper to get the effective ring direction string.</summary>
    private string GetEffectiveRingDirection()
    {
        return !string.IsNullOrEmpty(HeatWaveDirectionRing) ? HeatWaveDirectionRing : "Inwards";
    }

    // ════════════════════════════════════════════════════════════════════
    //  Undead detection
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if the creature is of type Undead (case-insensitive).
    /// </summary>
    private static bool IsCreatureUndead(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return false;

        string creatureType = character.Stats.CreatureType;
        if (string.IsNullOrWhiteSpace(creatureType))
            return false;

        return creatureType.Trim().ToLowerInvariant() == "undead";
    }

    // ════════════════════════════════════════════════════════════════════
    //  Multi-square creature helpers
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if every wall cell the creature occupies has been extinguished.
    /// For single-square creatures this checks just one cell.
    /// </summary>
    private bool AreAllCreatureCellsExtinguished(CharacterController character)
    {
        if (_extinguishedCells.Count == 0)
            return false;

        List<Vector2Int> occupied = character.GetOccupiedSquares();
        for (int i = 0; i < occupied.Count; i++)
        {
            // If any occupied cell is a non-extinguished wall cell, the wall still affects them
            if (AffectedCells.Contains(occupied[i]) && !_extinguishedCells.Contains(occupied[i]))
                return false;
        }
        return true;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Cold damage / extinguishing
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply cold damage to a specific wall cell. If the accumulated cold damage
    /// this round reaches 20+, that cell is extinguished and stops dealing damage.
    /// Call this from whatever system resolves cold-damage spells/effects hitting the wall.
    /// </summary>
    /// <param name="cell">The grid cell being hit by cold damage.</param>
    /// <param name="coldDamage">Amount of cold damage dealt to the cell.</param>
    public void ApplyColdDamageToCell(Vector2Int cell, int coldDamage)
    {
        if (coldDamage <= 0)
            return;

        // Only track damage to cells that are actually part of this wall and not already out
        if (!AffectedCells.Contains(cell) || _extinguishedCells.Contains(cell))
            return;

        if (!_coldDamagePerCell.ContainsKey(cell))
            _coldDamagePerCell[cell] = 0;

        _coldDamagePerCell[cell] += coldDamage;

        LogEffect($"  ❄ Wall of Fire cell ({cell.x},{cell.y}) takes {coldDamage} cold damage "
            + $"(total this round: {_coldDamagePerCell[cell]})");

        if (_coldDamagePerCell[cell] >= ColdDamageExtinguishThreshold)
        {
            _extinguishedCells.Add(cell);
            LogEffect($"  💨 Wall of Fire cell ({cell.x},{cell.y}) is EXTINGUISHED by cold damage!");

            // Update grid highlighting — remove highlight from extinguished cell
            RefreshGridHighlightForExtinguishedCells();
        }
    }

    /// <summary>
    /// Apply cold damage to ALL cells of this wall (e.g., from a large AoE cold spell).
    /// </summary>
    /// <param name="coldDamage">Amount of cold damage dealt to each cell.</param>
    public void ApplyColdDamageToAllCells(int coldDamage)
    {
        if (coldDamage <= 0)
            return;

        // Copy to avoid modification during iteration
        var cells = AffectedCells.ToList();
        for (int i = 0; i < cells.Count; i++)
            ApplyColdDamageToCell(cells[i], coldDamage);
    }

    /// <summary>
    /// Returns true if the given cell has been extinguished.
    /// </summary>
    public bool IsCellExtinguished(Vector2Int cell)
    {
        return _extinguishedCells.Contains(cell);
    }

    /// <summary>
    /// Returns the number of still-active (non-extinguished) wall cells.
    /// </summary>
    public int ActiveCellCount => AffectedCells.Count - _extinguishedCells.Count;

    /// <summary>
    /// Refreshes grid highlighting to hide extinguished cells.
    /// </summary>
    private void RefreshGridHighlightForExtinguishedCells()
    {
        // For each extinguished cell, clear its individual highlight
        foreach (Vector2Int cell in _extinguishedCells)
        {
            if (GameManager.Instance != null && GameManager.Instance.Grid != null)
            {
                SquareCell gridCell = GameManager.Instance.Grid.GetCell(cell.x, cell.y);
                if (gridCell != null)
                    gridCell.ClearHighlight();
            }
        }

        // If all cells are extinguished, expire the entire effect
        if (_extinguishedCells.Count >= AffectedCells.Count)
        {
            LogEffect("🔥➡💨 The entire Wall of Fire has been extinguished by cold!");
            ExpireEffect();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  Heat Wave Damage (PHB 3.5e p.298) — Two-Band System
    //
    //  HOT SIDE ONLY (cool side gets NO heat wave damage):
    //    Band 1 (Close):  1-2 squares from hot side → 2d4 fire (NO save)
    //    Band 2 (Medium): 3-4 squares from hot side → 1d4 fire (NO save)
    //  Undead take double damage in both bands.
    //  NO saving throw for heat wave damage (pass-through also has NO save per PHB p.298).
    //  Triggered:
    //    1) At time of casting (wall creation)
    //    2) At the beginning of the caster's turn each round
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Max distance in squares for Band 1 (close): 10 ft = 2 squares.</summary>
    private const int HeatWaveBand1MaxSquares = 2;
    /// <summary>Max distance in squares for Band 2 (medium): 20 ft = 4 squares.</summary>
    private const int HeatWaveBand2MaxSquares = 4;

    /// <summary>
    /// Subscribe to TurnStartedEvent to trigger heat wave at caster's turn start.
    /// </summary>
    private void SubscribeToTurnStartEvent()
    {
        if (_subscribedToTurnStart) return;
        GameEventSystem.Instance.Subscribe<TurnStartedEvent>(OnTurnStartedForHeatWave);
        _subscribedToTurnStart = true;
    }

    /// <summary>
    /// Unsubscribe from TurnStartedEvent.
    /// </summary>
    private void UnsubscribeFromTurnStartEvent()
    {
        if (!_subscribedToTurnStart) return;
        GameEventSystem.Instance.Unsubscribe<TurnStartedEvent>(OnTurnStartedForHeatWave);
        _subscribedToTurnStart = false;
    }

    /// <summary>
    /// Called when any character's turn starts. We only trigger heat wave
    /// when it is the caster's turn.
    /// </summary>
    private void OnTurnStartedForHeatWave(TurnStartedEvent evt)
    {
        if (evt.Character == null || Caster == null)
            return;

        // Only trigger on the caster's turn
        if (evt.Character != Caster)
            return;

        // Don't trigger if the wall has expired or all cells extinguished
        if (ActiveCellCount <= 0)
            return;

        TriggerHeatWaveDamage("at caster's turn start");
    }

    /// <summary>
    /// Main heat wave damage trigger. Finds all creatures on the hot side
    /// within 4 squares and deals band-appropriate damage:
    ///   Band 1 (1-2 sq): 2d4 fire — NO save
    ///   Band 2 (3-4 sq): 1d4 fire — NO save
    /// Undead take double. Cool side: no damage.
    /// </summary>
    /// <param name="context">Description for the combat log (e.g., "on creation", "at caster's turn start").</param>
    public void TriggerHeatWaveDamage(string context)
    {
        _heatWaveDamagedThisTrigger.Clear();

        string modeLabel = IsRingMode ? "Ring" : "Line";
        string dirLabel = IsRingMode
            ? GetEffectiveRingDirection()
            : (HeatWaveDirectionLine.HasValue ? $"Normal({HeatWaveDirectionLine.Value.x:F1},{HeatWaveDirectionLine.Value.y:F1})" : "NoDirection");

        Debug.Log($"[WallOfFire][HeatWave] === TRIGGER ({context}) === Mode={modeLabel}, Direction={dirLabel}, "
            + $"AffectedCells={AffectedCells?.Count ?? 0}, ActiveCells={ActiveCellCount}, "
            + $"RingRadius={RingRadius}, Center=({CenterCell.x},{CenterCell.y})");

        List<(CharacterController creature, int band)> hotSideCreatures = GetCreaturesOnHotSideWithBand();
        if (hotSideCreatures.Count == 0)
        {
            Debug.Log($"[WallOfFire][HeatWave] No creatures on hot side within {HeatWaveBand2MaxSquares} squares — no heat wave damage this trigger.");
            return;
        }

        LogEffect($"🌊🔥 Heat wave radiates {context}!");

        for (int i = 0; i < hotSideCreatures.Count; i++)
        {
            DealHeatWaveDamage(hotSideCreatures[i].creature, hotSideCreatures[i].band);
        }
    }

    /// <summary>
    /// Returns all living creatures on the hot side within 4 squares of a
    /// non-extinguished wall cell, along with which band (1 or 2) they fall in.
    /// Band 1 = 1-2 squares (closest), Band 2 = 3-4 squares.
    /// Each creature is returned at most once (multi-square creatures use closest band).
    /// Cool side creatures are NOT returned.
    /// </summary>
    private List<(CharacterController creature, int band)> GetCreaturesOnHotSideWithBand()
    {
        var result = new List<(CharacterController creature, int band)>();

        if (AffectedCells == null || AffectedCells.Count == 0)
            return result;

        CharacterController[] allCharacters = FindObjectsOfType<CharacterController>();

        for (int i = 0; i < allCharacters.Length; i++)
        {
            CharacterController character = allCharacters[i];
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            // Skip creatures already tracked (multi-square)
            if (_heatWaveDamagedThisTrigger.Contains(character))
                continue;

            // Skip creatures standing IN the wall (they take pass-through damage instead)
            if (IsCharacterInArea(character))
            {
                Debug.Log($"[WallOfFire][HeatWave] SKIP {character.Stats.CharacterName} at ({character.GridPosition.x},{character.GridPosition.y}) — standing IN wall cells (takes PASS-THROUGH damage, not heat wave)");
                continue;
            }

            // Determine which band (if any) this creature is in on the hot side
            int band = GetCreatureHeatWaveBand(character);
            if (band > 0)
            {
                string bandLabel = band == 1 ? "Band1 (1-2 sq, 2d4)" : "Band2 (3-4 sq, 1d4)";
                Debug.Log($"[WallOfFire][HeatWave] ✓ HIT {character.Stats.CharacterName} at ({character.GridPosition.x},{character.GridPosition.y}) — HOT SIDE, {bandLabel}");
                result.Add((character, band));
            }
        }

        return result;
    }

    /// <summary>
    /// Determines which heat wave band a creature falls in on the hot side.
    /// Returns 1 for Band 1 (1-2 squares), 2 for Band 2 (3-4 squares),
    /// or 0 if the creature is not on the hot side or is beyond 4 squares.
    /// For multi-square creatures, uses the closest qualifying band.
    /// </summary>
    private int GetCreatureHeatWaveBand(CharacterController character)
    {
        List<Vector2Int> occupied = character.GetOccupiedSquares();
        int bestBand = 0; // 0 = not in any band
        int closestDist = int.MaxValue;
        bool anyInRange = false;
        bool anyOnHotSide = false;

        for (int i = 0; i < occupied.Count; i++)
        {
            Vector2Int creatureCell = occupied[i];

            // Check against each non-extinguished wall cell
            foreach (Vector2Int wallCell in AffectedCells)
            {
                if (_extinguishedCells.Contains(wallCell))
                    continue;

                int dist = SquareGridUtils.GetDistance(creatureCell, wallCell);
                if (dist > HeatWaveBand2MaxSquares)
                    continue;

                anyInRange = true;

                // Check if this creature cell is on the hot side relative to this wall cell
                bool onHotSide = IsCellOnHotSide(creatureCell, wallCell);
                if (!onHotSide)
                    continue;

                anyOnHotSide = true;

                // Determine band based on distance
                int cellBand;
                if (dist <= HeatWaveBand1MaxSquares)
                    cellBand = 1; // Band 1: 1-2 squares → 2d4
                else
                    cellBand = 2; // Band 2: 3-4 squares → 1d4

                // Keep the closest (best) band — Band 1 is better than Band 2
                if (bestBand == 0 || cellBand < bestBand)
                {
                    bestBand = cellBand;
                    closestDist = dist;
                    if (bestBand == 1)
                        return 1; // Can't do better than Band 1, exit early
                }
            }
        }

        // Debug logging for creatures that were checked but not hit
        if (anyInRange && !anyOnHotSide)
        {
            if (IsRingMode)
            {
                int distToCenter = SquareGridUtils.GetDistance(CenterCell, character.GridPosition);
                string dir = GetEffectiveRingDirection();
                Debug.Log($"[WallOfFire][HeatWave] ✗ COOL SIDE: {character.Stats.CharacterName} at ({character.GridPosition.x},{character.GridPosition.y}) "
                    + $"— distToCenter={distToCenter}, RingRadius={RingRadius}, direction={dir} "
                    + $"(hot side is {(dir == "Inwards" ? "INSIDE ring (dist < " + RingRadius + ")" : "OUTSIDE ring (dist > " + RingRadius + ")")}) "
                    + $"— creature is on COOL side, NO damage");
            }
            else
            {
                Debug.Log($"[WallOfFire][HeatWave] ✗ COOL SIDE: {character.Stats.CharacterName} at ({character.GridPosition.x},{character.GridPosition.y}) "
                    + $"— on cool side of line wall, NO damage");
            }
        }
        else if (!anyInRange)
        {
            // Only log if creature is somewhat close (within 6 squares of center) to avoid spam
            int distToCenter = SquareGridUtils.GetDistance(CenterCell, character.GridPosition);
            if (distToCenter <= RingRadius + HeatWaveBand2MaxSquares + 2)
            {
                Debug.Log($"[WallOfFire][HeatWave] ✗ OUT OF RANGE: {character.Stats.CharacterName} at ({character.GridPosition.x},{character.GridPosition.y}) "
                    + $"— not within {HeatWaveBand2MaxSquares} squares of any active wall cell");
            }
        }

        return bestBand;
    }

    /// <summary>
    /// Determines if a given cell is on the "hot" side of the wall relative
    /// to a specific wall cell, based on ring or line mode configuration.
    /// </summary>
    private bool IsCellOnHotSide(Vector2Int testCell, Vector2Int wallCell)
    {
        if (IsRingMode)
            return IsCellOnHotSideRing(testCell);
        else
            return IsCellOnHotSideLine(testCell);
    }

    /// <summary>
    /// Ring mode: check if cell is on the hot side.
    /// "Inwards" = heat goes toward center → creatures INSIDE the ring (closer to center) take damage.
    /// "Outwards" = heat goes away from center → creatures OUTSIDE the ring (farther from center) take damage.
    /// Uses D&D 3.5e distance (SquareGridUtils.GetDistance) to be consistent
    /// with how ring cells are generated (cells at D&D dist == RingRadius).
    /// </summary>
    private bool IsCellOnHotSideRing(Vector2Int testCell)
    {
        string direction = GetEffectiveRingDirection();

        // Use D&D 3.5e distance (alternating diagonal) — same metric used to generate ring cells
        int distFromCenter = SquareGridUtils.GetDistance(CenterCell, testCell);

        bool isHotSide;
        if (direction == "Inwards")
        {
            // Hot side is inside the ring — cell must be strictly closer to center than ring radius
            // (cells AT RingRadius are ring cells themselves → pass-through damage, not heat wave)
            isHotSide = distFromCenter < RingRadius;
        }
        else // "Outwards"
        {
            // Hot side is outside the ring — cell must be strictly farther from center than ring radius
            isHotSide = distFromCenter > RingRadius;
        }

        return isHotSide;
    }

    /// <summary>
    /// Line mode: check if cell is on the hot side.
    /// Uses HeatWaveDirectionLine (perpendicular normal pointing to hot side).
    /// The dot product of (cell - wall midpoint) with the normal determines the side.
    /// </summary>
    private bool IsCellOnHotSideLine(Vector2Int testCell)
    {
        if (!HeatWaveDirectionLine.HasValue)
            return false; // No direction chosen — no heat wave

        Vector2 normal = HeatWaveDirectionLine.Value;
        if (normal.sqrMagnitude < 0.0001f)
            return false;

        // Compute midpoint of the wall for reference
        Vector2 wallMidpoint = GetWallMidpoint();

        // Vector from wall midpoint to the test cell
        Vector2 toCell = new Vector2(testCell.x - wallMidpoint.x, testCell.y - wallMidpoint.y);

        // Positive dot product = same side as the normal = hot side
        float dot = Vector2.Dot(toCell, normal);
        return dot > 0f;
    }

    /// <summary>
    /// Compute the midpoint of all wall cells (used for line mode hot side check).
    /// </summary>
    private Vector2 GetWallMidpoint()
    {
        if (AffectedCells == null || AffectedCells.Count == 0)
            return new Vector2(CenterCell.x, CenterCell.y);

        float sumX = 0f, sumY = 0f;
        int count = 0;
        foreach (Vector2Int cell in AffectedCells)
        {
            sumX += cell.x;
            sumY += cell.y;
            count++;
        }

        return count > 0
            ? new Vector2(sumX / count, sumY / count)
            : new Vector2(CenterCell.x, CenterCell.y);
    }

    // ════════════════════════════════════════════════════════════════════
    //  Heat Wave Damage Application — NO SAVING THROW
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Deals heat wave fire damage (NO save) to a creature based on their band.
    ///   Band 1 (1-2 sq from hot side): 2d4 fire damage [NO save]
    ///   Band 2 (3-4 sq from hot side): 1d4 fire damage [NO save]
    /// Undead take double damage (PHB 3.5e p.298).
    /// NOTE: Neither heat wave NOR pass-through damage has a saving throw (PHB p.298).
    /// </summary>
    /// <param name="character">The creature taking damage.</param>
    /// <param name="band">1 for close band (2d4), 2 for medium band (1d4).</param>
    private void DealHeatWaveDamage(CharacterController character, int band)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // Per-creature tracking: don't damage same creature twice in one heat wave trigger
        if (_heatWaveDamagedThisTrigger.Contains(character))
            return;
        _heatWaveDamagedThisTrigger.Add(character);

        int damage;
        string bandLabel;
        string diceLabel;
        string diceRolls;

        if (band == 1)
        {
            int d1 = Random.Range(1, 5);
            int d2 = Random.Range(1, 5);
            damage = d1 + d2; // 2d4
            bandLabel = "within 10 ft";
            diceLabel = "2d4";
            diceRolls = $"{d1}+{d2}";
        }
        else
        {
            int d1 = Random.Range(1, 5);
            damage = d1; // 1d4
            bandLabel = "10-20 ft";
            diceLabel = "1d4";
            diceRolls = $"{d1}";
        }

        bool isUndead = IsCreatureUndead(character);
        if (isUndead)
            damage *= 2;

        int finalDamage = Mathf.Max(1, damage);

        if (finalDamage > 0)
            character.Stats.TakeDamage(finalDamage);

        string undeadNote = isUndead ? " [UNDEAD ×2]" : "";
        string sideNote = IsRingMode ? $" [{GetEffectiveRingDirection()} heat]" : "";
        LogEffect($"🔥 HEAT WAVE: {character.Stats.CharacterName} takes {finalDamage} fire damage "
            + $"({bandLabel}) [{diceLabel}({diceRolls})] [No save]{undeadNote}{sideNote}");

        if (character.Stats.IsDead)
        {
            character.OnDeath();
            LogEffect($"  💀 {character.Stats.CharacterName} is slain by heat waves from the Wall of Fire!");
        }
    }
}