using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Otiluke's Resilient Sphere (PHB 3.5e p.263): Evocation [Force].
///
/// A globe of shimmering force that occupies grid squares as a STATIONARY area effect.
///   • Diameter = 1 foot × caster level
///   • Grid squares = diameter ÷ 5 (rounded down)
///     - CL 5:  5 ft → 1×1 square
///     - CL 8:  8 ft → 1×1 square
///     - CL 10: 10 ft → 2×2 squares
///     - CL 15: 15 ft → 3×3 squares
///   • Sphere is STATIONARY — anchored to the location where it was cast
///   • Creatures inside CAN move within the sphere's squares
///   • Creatures inside CAN act/attack/cast (but only on targets also inside same sphere)
///   • Nothing crosses the sphere boundary (in or out)
///   • Sphere is INDESTRUCTIBLE — no HP, no Hardness
///   • Can ONLY be removed by: Disintegrate, Rod of Cancellation, Rod of Negation, Dispel Magic
///   • Duration: 1 min/level (Dismissible by caster)
///   • Reflex save negates (if ANY creature in area saves, spell fails entirely — they dodge out)
/// </summary>
public class ResilientSphereAreaEffect : PersistentAreaEffect, ILineOfEffectBlocker
{
    /// <summary>Center cell where the sphere was cast.</summary>
    public Vector2Int CenterCell { get; set; }

    /// <summary>Computed side length of the sphere in squares.</summary>
    public int SphereSquareSize { get; set; } = 1;

    protected override Color GridHighlightColor => AreaEffectColors.ResilientSphere;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Resilient Sphere";
        SpellId = SpellNames.RESILIENT_SPHERE;
        Shape = AreaShape.Square;

        // No mesh visual — we use grid highlighting
        ShowVisual = false;

        // Sphere is NOT dispersible by wind (it's a force effect)
        DispersibleByWind = false;
    }

    protected override void Start()
    {
        // Calculate sphere size from caster level
        // Diameter = CL × 1 foot, squareSize = diameter / 5 (rounded down, min 1)
        int diameterFeet = Mathf.Max(1, CasterLevel);
        SphereSquareSize = Mathf.Max(1, diameterFeet / 5);

        SizeX = SphereSquareSize;
        SizeY = SphereSquareSize;

        base.Start();
    }

    protected override void OnAreaCreated()
    {
        // Register with the centralized LoE blocking service
        LineOfEffectService.Register(this);

        int diameterFeet = Mathf.Max(1, CasterLevel);
        LogEffect($"🔮 A shimmering sphere of force appears ({diameterFeet} ft diameter, {SphereSquareSize}×{SphereSquareSize} squares)!");
        LogEffect("  • Nothing can pass through the sphere boundary");
        LogEffect("  • Creatures inside can move and act within the sphere");
        LogEffect("  • Indestructible except by Disintegrate, Rod of Cancellation/Negation, or Dispel Magic");
        LogEffect($"  • Duration: {RoundsRemaining} round(s)");
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
            LogEffect($"  🔮 {character.Stats.CharacterName} is enclosed within the Resilient Sphere!");
        }
        else
        {
            LogEffect($"  {character.Stats.CharacterName} enters the Resilient Sphere area.");
        }
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        LogEffect($"  {character.Stats.CharacterName} is no longer inside the Resilient Sphere.");
    }

    protected override void OnAreaExpires()
    {
        // Unregister from centralized LoE blocking service
        LineOfEffectService.Unregister(this);

        RemoveGridHighlight();
        LogEffect("The Resilient Sphere dissipates.");
    }

    // ═══════════════════════════════════════════════════════════════
    // STATIC LOOKUP METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the ResilientSphereAreaEffect that contains the given cell, or null.
    /// </summary>
    public static ResilientSphereAreaEffect GetSphereContainingCell(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return null;

        List<ResilientSphereAreaEffect> spheres = AreaEffectManager.Instance.GetEffectsOfType<ResilientSphereAreaEffect>();
        if (spheres == null)
            return null;

        for (int i = 0; i < spheres.Count; i++)
        {
            ResilientSphereAreaEffect sphere = spheres[i];
            if (sphere != null && sphere.AffectedCells != null && sphere.AffectedCells.Contains(cell))
                return sphere;
        }
        return null;
    }

    /// <summary>
    /// Returns the ResilientSphereAreaEffect that contains the given character, or null.
    /// </summary>
    public static ResilientSphereAreaEffect GetSphereContainingCharacter(CharacterController character)
    {
        if (character == null)
            return null;

        return GetSphereContainingCell(character.GridPosition);
    }

    /// <summary>
    /// Returns true if the given character is inside any Resilient Sphere.
    /// </summary>
    public static bool IsCharacterInAnySphere(CharacterController character)
    {
        return GetSphereContainingCharacter(character) != null;
    }

    /// <summary>
    /// Returns true if both characters are inside the SAME Resilient Sphere.
    /// Returns false if either is not in a sphere, or they are in different spheres.
    /// </summary>
    public static bool AreCharactersInSameSphere(CharacterController char1, CharacterController char2)
    {
        if (char1 == null || char2 == null)
            return false;

        ResilientSphereAreaEffect sphere1 = GetSphereContainingCharacter(char1);
        if (sphere1 == null)
            return false;

        return sphere1.IsCharacterInArea(char2);
    }

    /// <summary>
    /// Checks whether an attack/spell between attacker and target should be blocked by a sphere.
    /// Returns true if blocked (they are separated by a sphere boundary).
    /// Returns false if both are outside all spheres, or both inside the SAME sphere.
    /// </summary>
    public static bool DoesSphereBlockInteraction(CharacterController source, CharacterController target)
    {
        if (source == null || target == null)
            return false;

        ResilientSphereAreaEffect sourceSphere = GetSphereContainingCharacter(source);
        ResilientSphereAreaEffect targetSphere = GetSphereContainingCharacter(target);

        // Both outside any sphere — no block
        if (sourceSphere == null && targetSphere == null)
            return false;

        // Both inside the same sphere — no block (can interact normally within)
        if (sourceSphere != null && targetSphere != null && sourceSphere == targetSphere)
            return false;

        // One inside, one outside, OR in different spheres — blocked
        return true;
    }

    /// <summary>
    /// Returns true if the cell is inside any active Resilient Sphere.
    /// </summary>
    public static bool IsCellInAnySphere(Vector2Int cell)
    {
        return GetSphereContainingCell(cell) != null;
    }

    /// <summary>
    /// Checks if moving from sourceCell to destCell would cross a sphere boundary.
    /// Returns true if movement should be blocked.
    /// A character inside a sphere can move to cells also inside that sphere.
    /// A character outside all spheres cannot move into a sphere cell.
    /// </summary>
    public static bool DoesMovementCrossSphereBoundary(Vector2Int sourceCell, Vector2Int destCell)
    {
        ResilientSphereAreaEffect sourceSphere = GetSphereContainingCell(sourceCell);
        ResilientSphereAreaEffect destSphere = GetSphereContainingCell(destCell);

        // Both outside — fine
        if (sourceSphere == null && destSphere == null)
            return false;

        // Both in same sphere — fine (moving within)
        if (sourceSphere != null && destSphere != null && sourceSphere == destSphere)
            return false;

        // Crossing boundary (in→out, out→in, or between different spheres) — blocked
        return true;
    }

    /// <summary>
    /// Returns a short description of the sphere for UI display.
    /// </summary>
    public string GetSphereInfoString()
    {
        int diameterFeet = Mathf.Max(1, CasterLevel);
        return $"Resilient Sphere — {diameterFeet} ft diameter, {SphereSquareSize}×{SphereSquareSize} squares, {RoundsRemaining} round(s)";
    }

    // ═══════════════════════════════════════════════════════════════
    // ILineOfEffectBlocker IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// The Resilient Sphere blocks line of effect across its boundary.
    /// If one cell is inside the sphere and the other is outside (or in a
    /// different sphere), LoE is blocked. If both are inside the same sphere
    /// or both are outside, LoE is NOT blocked by this sphere.
    /// </summary>
    bool ILineOfEffectBlocker.BlocksLineOfEffect(Vector2Int from, Vector2Int to)
    {
        if (AffectedCells == null || AffectedCells.Count == 0)
            return false;

        bool fromInside = AffectedCells.Contains(from);
        bool toInside = AffectedCells.Contains(to);

        // Block only when the line crosses the sphere boundary
        // (one inside, one outside)
        return fromInside != toInside;
    }

    /// <summary>
    /// Returns the sphere's cells. AoE filtering uses this to understand
    /// which cells "belong" to the blocker.
    /// </summary>
    HashSet<Vector2Int> ILineOfEffectBlocker.GetBlockerCells()
    {
        return AffectedCells;
    }
}
