using System.Collections.Generic;
using UnityEngine;

/// <summary>How a square hinders one mover (<see cref="MovementTerrain.Classify"/>).</summary>
public enum SquareHindrance
{
    /// <summary>The square neither blocks nor slows this mover.</summary>
    None,
    /// <summary>The square slows this mover's movement (difficult terrain, Solid Fog).</summary>
    Slows,
    /// <summary>This mover cannot enter the square (off the grid, a wall, a Resilient Sphere's boundary).</summary>
    Blocks,
}

/// <summary>
/// The one per-mover square question of movement, PC and NPC alike (owner interpretation 2026-10-29: "Charges need a clear
/// path unless they have some ability to bypass an object in their way, and they cannot end with two creatures spaces
/// overlapping"; docs/systems/RULES_COVERAGE.md 4.1). Whether a square blocks or slows THIS mover, with every movement
/// ability that bypasses a hindrance in one place (the exemptions below, and <see cref="ZoneHindersMovement"/> for the
/// spell zones, which the zones' own entry, save and Balance effects ask too). Normal movement asks it through
/// <see cref="SquareGridUtils.IsStepBlockedByBarrier"/>, <see cref="SquareGridUtils.IsCellBlockedByWall"/> and
/// <see cref="SquareGridUtils.IsDifficultTerrainForMovement"/> (the A*, the path cost, the move highlight, the 5-foot
/// step, the step executor); the charge planner asks <see cref="Classify"/> for every square of its line and of its end
/// space (GameManager.DescribeChargeSquareBlocker, DescribeChargeEndSpaceBlocker). Creatures in a square are not terrain:
/// the charge's creature rule is <see cref="ChargeRules.LetsChargePass"/>.
/// <list type="bullet">
/// <item>Blocks: off the grid; an intact Wall of Force square (PHB p.298, force: nothing passes); an intact Wall of Ice
/// square (PHB p.299) unless the mover passes solid objects; a Resilient Sphere's squares for a mover outside that sphere,
/// and every square outside it for a mover inside (PHB p.258, force).</item>
/// <item>Slows: difficult terrain (the squares the spell zones mark: Grease, Web, Entangle, Spike Stones, Plant Growth,
/// Soften Earth; PHB p.148) when one of the zones there hinders the mover; Solid Fog squares (PHB p.281, "any creature
/// attempting to move through it") and Sleet Storm squares (PHB p.280, icy ground: half speed with a Balance check) when
/// the zone hinders the mover. Plain moves pay the difficult-terrain cost only; Solid Fog keeps its own speed rule for
/// them (CharacterStats.EffectiveSpeedFeet, through <see cref="SolidFogAreaEffect.GetSpeedMultiplierFor"/>) and Sleet
/// Storm has none yet (SPL-206), so those two bar a charge line only.</item>
/// </list>
/// </summary>
public static class MovementTerrain
{
    // ── Exemptions: the movement abilities that bypass a hindrance ──────────────────────────────────────────────────

    /// <summary>
    /// Whether difficult terrain does not hamper <paramref name="mover"/>: an incorporeal creature (PHB p.148: flying and
    /// incorporeal creatures are not hampered by difficult terrain). Flight is not modelled (no fly movement mode; GRID-027):
    /// a fly mode joins here (a Web still holds a flyer: <see cref="ZoneHindersMovement"/> asks <see cref="PassesSolidObjects"/>
    /// for it, and Solid Fog any creature). A null mover (an object such as the Flaming Sphere, or a test with no creature) has no
    /// exemption.
    /// </summary>
    public static bool IgnoresDifficultTerrain(CharacterController mover)
        => mover != null && mover.IsIncorporeal;

    /// <summary>
    /// Whether <paramref name="mover"/> moves through solid objects: an incorporeal creature, which passes through solid
    /// objects at will but not force effects (MM p.310-311), so a Wall of Ice does not stop it and a Wall of Force or a
    /// Resilient Sphere does.
    /// </summary>
    public static bool PassesSolidObjects(CharacterController mover)
        => mover != null && mover.IsIncorporeal;

    /// <summary>
    /// Whether <paramref name="mover"/> moves normally despite magic that impedes movement (PHB p.233, Freedom of Movement,
    /// naming Solid Fog and Web among it): the spell or the ring (<see cref="CharacterStats.FreedomOfMovementActive"/>) or the
    /// Travel domain's granted power (<see cref="CharacterStats.TravelDomainFreedomRounds"/>, PHB p.188).
    /// </summary>
    public static bool IgnoresMagicalImpediment(CharacterController mover)
    {
        CharacterStats stats = mover != null ? mover.Stats : null;
        return stats != null && (stats.FreedomOfMovementActive || stats.TravelDomainFreedomRounds > 0);
    }

    // ── The square question ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// How <paramref name="cell"/> hinders <paramref name="mover"/>, with <paramref name="reason"/> naming the hindrance as
    /// a noun phrase ("difficult terrain", "a wall"; null for <see cref="SquareHindrance.None"/>). The charge refuses a line
    /// or an end space where this is not None (PHB p.154); see the class summary for what blocks and what slows.
    /// </summary>
    public static SquareHindrance Classify(CharacterController mover, Vector2Int cell, out string reason)
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.Grid != null && gm.Grid.GetCell(cell) == null)
        {
            reason = "the edge of the battlefield";
            return SquareHindrance.Blocks;
        }
        if (IsCellBlockedByWall(mover, cell))
        {
            reason = "a wall";
            return SquareHindrance.Blocks;
        }
        if (IsSphereWallFor(mover, cell))
        {
            reason = "a Resilient Sphere";
            return SquareHindrance.Blocks;
        }
        if (IsDifficultTerrain(mover, cell))
        {
            reason = "difficult terrain";
            return SquareHindrance.Slows;
        }
        reason = SlowingZoneAt(mover, cell);
        if (reason != null)
            return SquareHindrance.Slows;
        return SquareHindrance.None;
    }

    /// <summary>
    /// Whether <paramref name="mover"/> cannot enter <paramref name="cell"/> for a wall: an intact Wall of Force square
    /// always, an intact Wall of Ice square unless the mover passes solid objects (<see cref="PassesSolidObjects"/>);
    /// breached ice lets anyone through (PHB p.299).
    /// </summary>
    public static bool IsCellBlockedByWall(CharacterController mover, Vector2Int cell)
        => WallOfForceAreaEffect.DoesCellBlockMovement(cell)
            || (!PassesSolidObjects(mover) && WallOfIceAreaEffect.DoesCellBlockMovement(cell));

    /// <summary>
    /// The one barrier rule of a single step for <paramref name="mover"/>: the step may not cross a Resilient Sphere's
    /// boundary (PHB p.258, force), enter a square <see cref="IsCellBlockedByWall"/> bars, or cut a diagonal past the
    /// corner of a wall square that bars this mover (PHB p.147-148). See <see cref="SquareGridUtils.IsStepBlockedByBarrier"/>.
    /// </summary>
    public static bool IsStepBlockedByBarrier(CharacterController mover, Vector2Int from, Vector2Int to)
    {
        return ResilientSphereAreaEffect.DoesMovementCrossSphereBoundary(from, to)
            || IsCellBlockedByWall(mover, to)
            || WallOfForceAreaEffect.DoesDiagonalMoveCrossWall(from, to)
            || (!PassesSolidObjects(mover) && WallOfIceAreaEffect.DoesDiagonalMoveCrossWall(from, to));
    }

    /// <summary>
    /// Whether <paramref name="cell"/> is difficult terrain for <paramref name="mover"/> (PHB p.148): a square in the
    /// movement grid's difficult-terrain set where one of the zones that mark it hinders this mover
    /// (<see cref="ZoneHindersMovement"/>), or where no zone marks it (terrain with no zone hinders every mover that
    /// difficult terrain hampers, <see cref="IgnoresDifficultTerrain"/>). False with no GameManager (an edit-mode test).
    /// </summary>
    public static bool IsDifficultTerrain(CharacterController mover, Vector2Int cell)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.IsDifficultTerrain(cell))
            return false;
        if (!AreaEffectManager.HasInstance)
            return !IgnoresDifficultTerrain(mover);

        bool anyZone = false;
        List<PersistentAreaEffect> areas = AreaEffectManager.Instance.GetAllAreaEffects();
        for (int i = 0; i < areas.Count; i++)
        {
            PersistentAreaEffect area = areas[i];
            if (area == null || !MarksDifficultTerrain(area) || !area.IsCellInArea(cell))
                continue;
            anyZone = true;
            if (ZoneHindersMovement(mover, area))
                return true;
        }
        return !anyZone && !IgnoresDifficultTerrain(mover);
    }

    /// <summary>Whether any square of <paramref name="mover"/>'s space at <paramref name="anchor"/> is difficult terrain for it.</summary>
    public static bool OccupiesDifficultTerrainAt(CharacterController mover, Vector2Int anchor)
    {
        if (mover == null)
            return IsDifficultTerrain(null, anchor);
        List<Vector2Int> occupied = mover.GetOccupiedSquaresAt(anchor);
        for (int i = 0; i < occupied.Count; i++)
            if (IsDifficultTerrain(mover, occupied[i]))
                return true;
        return false;
    }

    /// <summary>
    /// Whether <paramref name="cell"/> is a Resilient Sphere's barrier for <paramref name="mover"/>: the cell lies in a
    /// sphere that does not hold the mover, or outside the sphere that does (PHB p.258; a force effect, so nothing bypasses it).
    /// </summary>
    private static bool IsSphereWallFor(CharacterController mover, Vector2Int cell)
    {
        ResilientSphereAreaEffect cellSphere = ResilientSphereAreaEffect.GetSphereContainingCell(cell);
        ResilientSphereAreaEffect moverSphere = mover != null ? ResilientSphereAreaEffect.GetSphereContainingCharacter(mover) : null;
        return cellSphere != moverSphere;
    }

    // ── The spell zones that hinder movement ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the spell zone <paramref name="zone"/> hinders <paramref name="mover"/>'s movement: the one exemption rule
    /// of the zones, which the square question (<see cref="IsDifficultTerrain"/>, <see cref="Classify"/>) and the zones'
    /// own effects on a mover share (Grease's Balance check and fall save, Entangle's and Web's entanglement; Spike Stones'
    /// step damage asks <see cref="IgnoresDifficultTerrain"/>), so the charge planner and the step executor agree
    /// (delegated RAW readings 2026-10-29 under the owner interpretation of that date, docs/systems/RULES_COVERAGE.md 4.1):
    /// <list type="bullet">
    /// <item>Freedom of Movement (<see cref="IgnoresMagicalImpediment"/>) lifts every zone of ongoing magic that impedes
    /// movement (PHB p.233, "such as" Solid Fog and Web): Grease, Web, Entangle, Spike Stones, Solid Fog and Sleet Storm.
    /// Plant Growth's overgrowth and Soften Earth's mud are instantaneous spells whose result is ordinary terrain
    /// (PHB p.262, p.280), so they still hinder.</item>
    /// <item>A mover that difficult terrain does not hamper (<see cref="IgnoresDifficultTerrain"/>: incorporeal; PHB p.148)
    /// is not hindered by a zone on the ground: Grease, Entangle, Spike Stones, Plant Growth, Soften Earth and Sleet Storm's
    /// icy ground.</item>
    /// <item>A Web's strands hold every mover but one that passes solid objects (an incorporeal creature, which is not
    /// subject to physical actions that move or manipulate it, MM p.311).</item>
    /// <item>Solid Fog hinders every other mover, an incorporeal one too (PHB p.281, "any creature").</item>
    /// </list>
    /// A zone that is none of these (a Wall of Fire, a Fog Cloud) is not a movement hindrance and returns false.
    /// </summary>
    public static bool ZoneHindersMovement(CharacterController mover, PersistentAreaEffect zone)
    {
        if (zone == null)
            return false;
        bool slowing = MarksDifficultTerrain(zone) || zone is SolidFogAreaEffect || zone is SleetStormAreaEffect;
        if (!slowing)
            return false;
        if (IsImpedingMagic(zone) && IgnoresMagicalImpediment(mover))
            return false;
        if (zone is WebAreaEffect)
            return !PassesSolidObjects(mover);
        if (zone is SolidFogAreaEffect)
            return true;
        return !IgnoresDifficultTerrain(mover);
    }

    /// <summary>The zones that write the movement grid's difficult-terrain set (GameManager.SetAreaDifficultTerrain).</summary>
    private static bool MarksDifficultTerrain(PersistentAreaEffect zone)
        => zone is GreaseAreaEffect || zone is WebAreaEffect || zone is EntangleAreaEffect || zone is SpikeStoneAreaEffect
            || zone is PlantGrowthAreaEffect || zone is SoftenEarthAreaEffect;

    /// <summary>The zones of ongoing magic that impede movement, which Freedom of Movement lifts (PHB p.233).</summary>
    private static bool IsImpedingMagic(PersistentAreaEffect zone)
        => zone is GreaseAreaEffect || zone is WebAreaEffect || zone is EntangleAreaEffect || zone is SpikeStoneAreaEffect
            || zone is SolidFogAreaEffect || zone is SleetStormAreaEffect;

    /// <summary>
    /// The slowing zone at <paramref name="cell"/> outside the difficult-terrain set that hinders <paramref name="mover"/>,
    /// as a noun phrase ("solid fog", "a sleet storm's icy ground"), or null: Solid Fog (PHB p.281) and Sleet Storm
    /// (PHB p.280).
    /// </summary>
    private static string SlowingZoneAt(CharacterController mover, Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return null;
        List<PersistentAreaEffect> areas = AreaEffectManager.Instance.GetAllAreaEffects();
        for (int i = 0; i < areas.Count; i++)
        {
            PersistentAreaEffect area = areas[i];
            if (area == null || !(area is SolidFogAreaEffect || area is SleetStormAreaEffect) || !area.IsCellInArea(cell))
                continue;
            if (ZoneHindersMovement(mover, area))
                return area is SolidFogAreaEffect ? "solid fog" : "a sleet storm's icy ground";
        }
        return null;
    }
}
