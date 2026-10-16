using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>What an area spell does to the creatures it takes in, for the AI's aim (<see cref="AreaAimSearch.PolarityOf"/>).</summary>
public enum AreaAimPolarity
{
    /// <summary>No value the AI can judge (light, obscurement, terrain, walls, figments, summons): no placement.</summary>
    None,

    /// <summary>A damage, debuff or control spell: worth the enemies it affects, less the allies it affects.</summary>
    Harmful,

    /// <summary>A buff for the caster's side (Bless): worth the allies it helps, less the enemies it helps.</summary>
    Beneficial,

    /// <summary>
    /// Prayer (PHB p.264): a bonus for allies and a penalty for foes, so every creature in it that is not already under it
    /// counts for the caster.
    /// </summary>
    Mixed
}

/// <summary>
/// One aim of an area spell the AI could cast (batch 11 step 25, <see cref="AreaAimSearch"/>): its point of origin (a
/// grid intersection; none for a one-square effect), a cone's direction, the squares it covers after line of effect from
/// its origin, and what it is worth to the caster.
/// </summary>
public sealed class AreaPlacement
{
    /// <summary>The resolve the cast takes: Area, or Grease for Grease on a 10-ft. square (PHB p.237).</summary>
    public CastResolveKind Kind = CastResolveKind.Area;

    /// <summary>The area's point of origin (CastAim.AreaOrigin); null for a one-square effect (Flaming Sphere, Call Lightning).</summary>
    public GridIntersection? Origin;

    /// <summary>A cone's direction (an index into SquareGridUtils.Directions); -1 otherwise.</summary>
    public int DirIndex = -1;

    /// <summary>The squares the area covers, after line of effect from its origin (the Legality stage's filter).</summary>
    public HashSet<Vector2Int> Cells = new HashSet<Vector2Int>();

    /// <summary>The enemies in the area that the spell would affect (standing, not immune, not behind a globe).</summary>
    public int EnemiesHit;

    /// <summary>The caster and its allies in the area that the spell's filter takes in.</summary>
    public int AlliesHit;

    /// <summary>The allies (the caster included) a harmful spell would affect: each costs the ally weight.</summary>
    public int AlliesHarmed;

    /// <summary>The allies a beneficial spell, or Prayer, would help (not already under it).</summary>
    public int AlliesBenefited;

    /// <summary>What the creatures in the area are worth to the caster, before the allies' cost.</summary>
    public float Value;

    /// <summary><see cref="Value"/> less the ally weight times <see cref="AlliesHarmed"/>.</summary>
    public float Score;

    /// <summary>The aim's place in the search order (the first of equals wins).</summary>
    public int Order;

    public override string ToString()
        => (Origin.HasValue ? Origin.Value.ToString() : "square") + (DirIndex >= 0 ? " dir " + DirIndex : "")
           + " cells=" + Cells.Count + " enemies=" + EnemiesHit + " allies=" + AlliesHit + " harmed=" + AlliesHarmed
           + " helped=" + AlliesBenefited + " score=" + Score.ToString("0.##");
}

/// <summary>How <see cref="AreaAimSearch.FindBest"/> weighs and filters the aims.</summary>
public sealed class AreaAimOptions
{
    /// <summary>The cost of each ally a harmful spell affects, in enemies.</summary>
    public float AllyWeight = AreaAimSearch.DefaultAllyWeight;

    /// <summary>An extra test an aim must pass (a profile's ally-casualty limit), or null.</summary>
    public Func<AreaPlacement, bool> Accept;
}

/// <summary>
/// The AI's aim of an area spell (batch 11 step 25, docs/designs/cast_core_batch11.md, AI-001 area half): the candidate
/// placements the caster has, each scored by what its area takes in, and the best one. Tactics only: the candidates use
/// the step 24 geometry and tests the PC cursor and the cast core use (AoESystem: points of origin at grid intersections
/// within the spell's range and in the caster's line of effect, PHB p.175-176; the footprint filtered by line of effect
/// from its origin; the creatures the spell's filter takes in, judged with TeamUtility), so the cast the AI builds
/// (SpellCastRequest.ForAIArea) passes the same Legality stage as a PC's. Candidates: a burst or cube at the
/// intersections from which its area reaches a square the spell is aimed at (the enemies' for a harmful spell, the
/// allies' for a beneficial one): for a burst every intersection whose count to the square's far corner is within the
/// radius, for a cube every intersection whose cube holds the square, so an origin on the far side of an enemy that
/// spares the caster's ally beside it is searched too; the origins nearest an aimed square first (the intersections of
/// its own corners lead), then nearest the caster; a self-centred burst (Bless, Prayer) from each of the caster's
/// creature-centred origins; a one-square effect on each such square in range; a cone from each intersection on the edge
/// of the caster's space in each of the 8 directions; a line from each of those intersections through the centre of
/// each such square (a line along a grid line crosses no square's interior, PHB p.175's line passes through squares);
/// Grease's 10-ft. square on each 2-by-2 block that holds such a square. At most <see cref="MaxCandidates"/> aims.
/// Score: the enemies the spell would affect (standing, not immune to it, not inside a globe that stops it) less
/// <see cref="DefaultAllyWeight"/> times the allies it would affect, for a harmful spell; the allies it would help less the
/// enemies it would help, for a beneficial one; the foes and allies not already under it, for Prayer. The spell's own value comes from the strategist and the profile, which
/// score the spell on top of the best placement (SpellcasterAIProfile.EvaluateAOECast,
/// AISpellcastingStrategist.GetAreaDenialScore). Deterministic: the same board gives the same aims in the same order.
/// </summary>
public static class AreaAimSearch
{
    /// <summary>The most aims one search scores (the plan's cap, so an AI turn stays short).</summary>
    public const int MaxCandidates = 200;

    /// <summary>The cost of an ally a harmful spell affects, in enemies.</summary>
    public const float DefaultAllyWeight = 2f;

    /// <summary>
    /// True when the AI casts <paramref name="spell"/> as an area (an area request aimed by this search rather than a
    /// cast at one creature): every spell the PC's targeting routes to the area cast (AoESystem.RoutesToArea: an Area
    /// target type, PHB p.175, or an area shape, as Prayer's burst centred on the caster with a Self target, PHB p.264),
    /// and Grease on its 10-ft. square (PHB p.237). It is the PC routing's own predicate, so a spell has one cast path for
    /// every caster (batch 11 step 25 review). Summon Swarm is one too; the AI decider cannot place its swarm
    /// (AiCastDecider.CanFill), so it is no candidate.
    /// </summary>
    public static bool IsAreaCastForAI(SpellData spell)
        => spell != null && (AoESystem.RoutesToArea(spell) || IsGrease(spell));

    public static bool IsGrease(SpellData spell) => spell != null && spell.SpellId == SpellNames.GREASE;

    /// <summary>
    /// What the spell does to the creatures in its area, for the AI's aim: damage, debuff and control spells harm
    /// (Grease, a control spell); Bless and other buffs whose filter takes only the caster's side help; Prayer does both
    /// (PHB p.264); everything else (light, obscurement, terrain, walls, figments, summons, Wind Wall) has no value the AI
    /// can judge, so it finds no placement and the spell is dropped.
    /// </summary>
    public static AreaAimPolarity PolarityOf(SpellData spell)
    {
        if (spell == null)
            return AreaAimPolarity.None;
        if (spell.SpellId == SpellNames.PRAYER)
            return AreaAimPolarity.Mixed;
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
            case SpellEffectType.Debuff:
            case SpellEffectType.Control:
                return AreaAimPolarity.Harmful;
            case SpellEffectType.Buff:
            case SpellEffectType.Healing:
                return spell.AoEFilter == AoETargetFilter.AlliesOnly ? AreaAimPolarity.Beneficial : AreaAimPolarity.None;
            default:
                return AreaAimPolarity.None;
        }
    }

    /// <summary>The range of a placed area's point of origin, as the PC cursor and the Legality stage take it.</summary>
    public static int PlacementRange(CharacterController caster, SpellData spell)
        => spell == null ? 0 : spell.AoERangeSquares > 0 ? spell.AoERangeSquares : CasterLevelRules.RangeSquares(caster, spell);

    // ── Cache ─────────────────────────────────────────────────────────────────────────────────────────────

    // The scoring asks for the same search several times in one decision (the profile, the strategist, the walk); a
    // search depends only on the board, so its result is kept for the frame and the board it was made on.
    private static int _cacheFrame = -1;
    private static readonly Dictionary<long, List<AreaPlacement>> _cache = new Dictionary<long, List<AreaPlacement>>();

    /// <summary>Forgets every cached search (a test that moves creatures within one frame).</summary>
    public static void ClearCache() => _cache.Clear();

    private static long BoardKey(SpellData spell, CharacterController caster, IList<CharacterController> all, float allyWeight)
    {
        unchecked
        {
            long h = 1469598103934665603L;
            void Mix(long v) { h = (h ^ v) * 1099511628211L; }
            Mix(caster != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(caster) : 0);
            Mix(caster != null ? caster.GridPosition.x * 7919 + caster.GridPosition.y : 0);
            Mix(spell.SpellId != null ? spell.SpellId.GetHashCode() : 0);
            Mix(spell.AoESizeSquares * 131 + spell.AoERangeSquares);
            Mix((long)(allyWeight * 1000f));
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    CharacterController c = all[i];
                    if (c == null || c.Stats == null)
                        continue;
                    Mix(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c));
                    Mix(c.GridPosition.x * 7919 + c.GridPosition.y);
                    Mix(c.Stats.CurrentHP);
                    Mix((int)c.Team);
                }
            }
            return h;
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every aim <paramref name="caster"/> has for <paramref name="spell"/> (the cast's own clone, so a Widened area is
    /// searched at its widened size) among <paramref name="all"/>, scored, in search order; empty for a spell with no
    /// judged value (<see cref="AreaAimPolarity.None"/>) or no shape. <paramref name="grid"/> may be null (a static
    /// test: every square exists).
    /// </summary>
    public static List<AreaPlacement> Search(SpellData spell, CharacterController caster, IList<CharacterController> all,
        SquareGrid grid, float allyWeight = DefaultAllyWeight)
    {
        if (spell == null || caster == null || caster.Stats == null)
            return new List<AreaPlacement>();

        int frame = Time.frameCount;
        if (frame != _cacheFrame)
        {
            _cache.Clear();
            _cacheFrame = frame;
        }
        long key = BoardKey(spell, caster, all, allyWeight);
        if (_cache.TryGetValue(key, out List<AreaPlacement> cached))
            return cached;

        var result = new List<AreaPlacement>();
        AreaAimPolarity polarity = PolarityOf(spell);
        if (polarity != AreaAimPolarity.None)
        {
            foreach (AreaPlacement p in Candidates(spell, caster, all, grid, polarity))
            {
                p.Order = result.Count;
                ScorePlacement(p, spell, caster, all, polarity, allyWeight);
                result.Add(p);
                if (result.Count >= MaxCandidates)
                    break;
            }
        }
        _cache[key] = result;
        return result;
    }

    /// <summary>
    /// The best aim of <see cref="Search"/> that is worth casting (<see cref="IsWorthwhile"/>) and passes
    /// <paramref name="options"/>' test, or null: then the AI drops the spell (no fallback).
    /// </summary>
    public static AreaPlacement FindBest(SpellData spell, CharacterController caster, IList<CharacterController> all,
        SquareGrid grid, AreaAimOptions options = null)
    {
        float weight = options != null ? options.AllyWeight : DefaultAllyWeight;
        AreaAimPolarity polarity = PolarityOf(spell);
        AreaPlacement best = null;
        foreach (AreaPlacement p in Search(spell, caster, all, grid, weight))
        {
            if (!IsWorthwhile(p, polarity))
                continue;
            if (options != null && options.Accept != null && !options.Accept(p))
                continue;
            if (best == null || IsBetter(p, best))
                best = p;
        }
        return best;
    }

    /// <summary>
    /// Worth casting: a harmful spell affects at least one enemy and scores above zero; a beneficial one helps at least
    /// one ally and scores above zero; Prayer scores above zero (it reaches an ally or a foe not already under it).
    /// </summary>
    public static bool IsWorthwhile(AreaPlacement p, AreaAimPolarity polarity)
    {
        if (p == null)
            return false;
        switch (polarity)
        {
            case AreaAimPolarity.Harmful:
                return p.EnemiesHit > 0 && p.Score > 0f;
            case AreaAimPolarity.Beneficial:
                return p.AlliesBenefited > 0 && p.Score > 0f;
            case AreaAimPolarity.Mixed:
                return p.Score > 0f;
            default:
                return false;
        }
    }

    /// <summary>The higher score, then fewer allies harmed, then the earlier aim.</summary>
    public static bool IsBetter(AreaPlacement a, AreaPlacement b)
    {
        if (b == null)
            return a != null;
        if (a == null)
            return false;
        if (Mathf.Abs(a.Score - b.Score) > 1e-4f)
            return a.Score > b.Score;
        if (a.AlliesHarmed != b.AlliesHarmed)
            return a.AlliesHarmed < b.AlliesHarmed;
        return a.Order < b.Order;
    }

    // ── Candidates ────────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<AreaPlacement> Candidates(SpellData spell, CharacterController caster, IList<CharacterController> all,
        SquareGrid grid, AreaAimPolarity polarity)
    {
        Vector2Int casterBase = caster.GridPosition;
        int casterSize = caster.GetCurrentSpaceSquares();
        List<Vector2Int> aimSquares = AimSquares(caster, all, polarity);

        if (IsGrease(spell))
        {
            int range = Mathf.Max(1, CasterLevelRules.RangeSquares(caster, spell));
            var anchors = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            foreach (Vector2Int sq in aimSquares)
            {
                for (int dy = -1; dy <= 0; dy++)
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        var anchor = new Vector2Int(sq.x + dx, sq.y + dy);
                        if (seen.Add(anchor))
                            anchors.Add(anchor);
                    }
            }
            anchors.Sort((a, b) => CompareByDistance(casterBase, a, b));
            foreach (Vector2Int anchor in anchors)
            {
                if (!AoESystem.IsWithinCastingRange(casterBase, anchor, range))
                    continue;
                GridIntersection origin = GridIntersection.NorthEastCornerOf(anchor);
                if (!AoESystem.HasLineOfEffectToOrigin(casterBase, casterSize, origin))
                    continue;
                var cells = new HashSet<Vector2Int>();
                foreach (var c in new[] { anchor, new Vector2Int(anchor.x + 1, anchor.y), new Vector2Int(anchor.x, anchor.y + 1), new Vector2Int(anchor.x + 1, anchor.y + 1) })
                    if (grid == null || grid.GetCell(c) != null)
                        cells.Add(c);
                AoESystem.RemoveCellsWithoutLineOfEffect(cells, origin);
                if (cells.Count > 0)
                    yield return new AreaPlacement { Kind = CastResolveKind.Grease, Origin = origin, Cells = cells };
            }
            yield break;
        }

        AoEShape shape = spell.AoEShapeType;
        int size = spell.AoESizeSquares;
        int placeRange = PlacementRange(caster, spell);

        if (AoESystem.IsSingleSquarePlacement(spell))
        {
            var squares = new List<Vector2Int>(aimSquares);
            squares.Sort((a, b) => CompareByDistance(casterBase, a, b));
            foreach (Vector2Int sq in squares)
            {
                if ((grid != null && grid.GetCell(sq) == null) || !AoESystem.IsWithinCastingRange(casterBase, sq, placeRange))
                    continue;
                yield return new AreaPlacement { Cells = new HashSet<Vector2Int> { sq } };
            }
            yield break;
        }

        if (shape == AoEShape.Burst && placeRange <= 0)
        {
            // Centred on the caster: its creature-centred origins (decision D5 (3)), as the PC's confirmation offers them.
            foreach (GridIntersection origin in AoESystem.GetCreatureCentredOrigins(caster))
            {
                HashSet<Vector2Int> cells = AoESystem.GetCreatureCentredBurstCells(casterBase, casterSize, origin, size, grid);
                AoESystem.RemoveCellsWithoutLineOfEffect(cells, origin);
                if (cells.Count > 0)
                    yield return new AreaPlacement { Origin = origin, Cells = cells };
            }
            yield break;
        }

        if (AoESystem.IsPlacedShape(shape))
        {
            // Every intersection from which the area reaches an aimed square (batch 11 step 25 review): a burst whose
            // count to the square's far corner is within its radius (PHB p.175), a cube that holds the square. Ordered by
            // the count to the nearest aimed square's far corner (each square's own corners first, so the cap never drops
            // them), then by the count from the caster's space, then south to north and west to east.
            int reach = Mathf.Max(1, size);
            var origins = new List<GridIntersection>();
            var nearest = new Dictionary<GridIntersection, int>();
            foreach (Vector2Int sq in aimSquares)
            {
                for (int y = sq.y - reach; y <= sq.y + reach + 1; y++)
                    for (int x = sq.x - reach; x <= sq.x + reach + 1; x++)
                    {
                        var o = new GridIntersection(x, y);
                        if (!PlacedAreaReaches(shape, reach, o, sq))
                            continue;
                        int count = AoESystem.CountToFarCorner(o, sq);
                        if (nearest.TryGetValue(o, out int had))
                        {
                            if (count < had)
                                nearest[o] = count;
                            continue;
                        }
                        nearest[o] = count;
                        origins.Add(o);
                    }
            }
            origins.Sort((a, b) =>
            {
                int na = nearest[a], nb = nearest[b];
                if (na != nb) return na.CompareTo(nb);
                int da = AoESystem.CountFromSpace(casterBase, casterSize, a), db = AoESystem.CountFromSpace(casterBase, casterSize, b);
                if (da != db) return da.CompareTo(db);
                if (a.Y != b.Y) return a.Y.CompareTo(b.Y);
                return a.X.CompareTo(b.X);
            });
            foreach (GridIntersection origin in origins)
            {
                if (!AoESystem.IsOriginWithinRange(casterBase, casterSize, origin, placeRange)
                    || !AoESystem.HasLineOfEffectToOrigin(casterBase, casterSize, origin))
                    continue;
                HashSet<Vector2Int> cells = AoESystem.GetPlacedAreaCells(spell, origin, grid);
                AoESystem.RemoveCellsWithoutLineOfEffect(cells, origin);
                if (cells.Count > 0)
                    yield return new AreaPlacement { Origin = origin, Cells = cells };
            }
            yield break;
        }

        if (shape == AoEShape.Cone)
        {
            foreach (GridIntersection start in AoESystem.GetSpaceEdgeIntersections(casterBase, casterSize))
            {
                for (int dir = 0; dir < SquareGridUtils.Directions.Length; dir++)
                {
                    HashSet<Vector2Int> cells = AoESystem.GetConeCells(start, dir, Mathf.Max(1, size), grid);
                    if (cells.Count == 0 || !AoESystem.IsClearOfSpace(cells, casterBase, casterSize))
                        continue;
                    AoESystem.RemoveCellsWithoutLineOfEffect(cells, start);
                    if (cells.Count > 0)
                        yield return new AreaPlacement { Origin = start, DirIndex = dir, Cells = cells };
                }
            }
            yield break;
        }

        if (shape == AoEShape.Line)
        {
            int length = Mathf.Max(1, size);
            var squares = new List<Vector2Int>(aimSquares);
            squares.Sort((a, b) => CompareByDistance(casterBase, a, b));
            foreach (GridIntersection start in AoESystem.GetSpaceEdgeIntersections(casterBase, casterSize))
            {
                foreach (Vector2Int sq in squares)
                {
                    if (AoESystem.CountToFarCorner(start, sq) > length + 1)
                        continue;
                    HashSet<Vector2Int> cells = AoESystem.GetLineCells(start, new Vector2(sq.x, sq.y), length, grid);
                    if (cells.Count == 0 || !AoESystem.IsClearOfSpace(cells, casterBase, casterSize))
                        continue;
                    AoESystem.RemoveCellsWithoutLineOfEffect(cells, start);
                    if (cells.Count > 0)
                        yield return new AreaPlacement { Origin = start, Cells = cells };
                }
            }
        }
    }

    /// <summary>
    /// True when a burst of radius <paramref name="reach"/> (a square is in it when its far corner is within the radius,
    /// AoESystem.IsSquareWithinRadius) or a cube of side <paramref name="reach"/> centred on <paramref name="origin"/>
    /// (AoESystem.GetCubeCells) takes in <paramref name="square"/>.
    /// </summary>
    private static bool PlacedAreaReaches(AoEShape shape, int reach, GridIntersection origin, Vector2Int square)
    {
        if (shape == AoEShape.Cube)
        {
            int lowX = origin.X - reach / 2, lowY = origin.Y - reach / 2;
            return square.x >= lowX && square.x < lowX + reach && square.y >= lowY && square.y < lowY + reach;
        }
        return AoESystem.IsSquareWithinRadius(origin, reach, square);
    }

    /// <summary>The squares the spell is aimed at: the occupied squares of the living enemies (harmful, Prayer) or of the caster and its allies (beneficial).</summary>
    private static List<Vector2Int> AimSquares(CharacterController caster, IList<CharacterController> all, AreaAimPolarity polarity)
    {
        var squares = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        if (all == null)
            return squares;
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (c == null || c.Stats == null || c.Stats.IsDead)
                continue;
            bool aimed = polarity == AreaAimPolarity.Beneficial
                ? c == caster || TeamUtility.IsAlly(caster, c)
                : c != caster && TeamUtility.IsEnemy(caster, c);
            if (!aimed)
                continue;
            foreach (Vector2Int sq in c.GetOccupiedSquares())
                if (seen.Add(sq))
                    squares.Add(sq);
        }
        return squares;
    }

    private static int CompareByDistance(Vector2Int from, Vector2Int a, Vector2Int b)
    {
        int da = SquareGridUtils.GetDistance(from, a), db = SquareGridUtils.GetDistance(from, b);
        if (da != db) return da.CompareTo(db);
        if (a.y != b.y) return a.y.CompareTo(b.y);
        return a.x.CompareTo(b.x);
    }

    // ── Scoring ───────────────────────────────────────────────────────────────────────────────────────────

    private static void ScorePlacement(AreaPlacement p, SpellData spell, CharacterController caster, IList<CharacterController> all,
        AreaAimPolarity polarity, float allyWeight)
    {
        AoETargetFilter filter = IsGrease(spell) ? AoETargetFilter.All : spell.AoEFilter;
        float value = 0f;
        if (all != null)
        {
            for (int i = 0; i < all.Count; i++)
            {
                CharacterController c = all[i];
                if (c == null || c.Stats == null || c.Stats.IsDead)
                    continue;
                if (!AoESystem.IsCreatureInCells(p.Cells, c) || !AoESystem.IncludedByFilter(caster, c, filter))
                    continue;
                bool ally = c == caster || TeamUtility.IsAlly(caster, c);
                bool enemy = !ally && TeamUtility.IsEnemy(caster, c);
                bool affected = WouldAffect(spell, c);
                if (enemy)
                {
                    bool counts = affected && c.Stats.CurrentHP > 0; // a fallen foe is worth nothing
                    if (polarity == AreaAimPolarity.Beneficial)
                    {
                        if (affected) value -= 1f;
                    }
                    else if (counts && !(polarity == AreaAimPolarity.Mixed && AlreadyUnder(c, spell)))
                    {
                        p.EnemiesHit++;
                        value += 1f;
                    }
                }
                else if (ally)
                {
                    p.AlliesHit++;
                    if (polarity == AreaAimPolarity.Harmful && affected)
                        p.AlliesHarmed++;
                    else if ((polarity == AreaAimPolarity.Beneficial || polarity == AreaAimPolarity.Mixed) && affected && !AlreadyUnder(c, spell))
                    {
                        p.AlliesBenefited++;
                        value += 1f;
                    }
                }
            }
        }
        p.Value = value;
        p.Score = value - allyWeight * p.AlliesHarmed;
    }

    /// <summary>
    /// Whether the spell would affect <paramref name="c"/> at all, as far as the AI can see: not inside a globe that stops
    /// it (PHB p.236), not immune to a mind-affecting spell (an undead, construct, ooze or vermin; PHB p.311-312, MM), not
    /// immune to its damage, not cured by it (an inflict area on an undead), and within the spell's own creature limits
    /// (Sleep: 4 HD or fewer, PHB p.280; Halt Undead: undead, PHB p.239; Calm Animals: animals, PHB p.207). Saves and
    /// spell resistance are chances, not exclusions.
    /// </summary>
    public static bool WouldAffect(SpellData spell, CharacterController c)
    {
        if (spell == null || c == null || c.Stats == null)
            return false;
        if (AISpellcastingStrategist.IsWastedOnGlobe(spell, c))
            return false;
        if (spell.IsMindAffecting && SpellUtilities.IsImmuneToMindAffecting(c))
            return false;
        if (spell.EffectType == SpellEffectType.Damage)
        {
            if (spell.Energy != SpellEnergy.None && !SpellDiceRules.Harms(spell, c.Stats))
                return false;
            if (!string.IsNullOrEmpty(spell.DamageType) && c.Stats.DamageImmunities != null)
            {
                DamageType type = DamageTextUtils.ParseSingleDamageType(spell.DamageType);
                if (type != DamageType.Untyped && c.Stats.DamageImmunities.Contains(type))
                    return false;
            }
        }
        if (spell.SpellId == SpellNames.SLEEP && c.Stats.GetHitDice() >= 5)
            return false;
        if (spell.SpellId == SpellNames.HALT_UNDEAD && !string.Equals(c.Stats.CreatureType, "Undead", StringComparison.OrdinalIgnoreCase))
            return false;
        if (spell.SpellId == SpellNames.CALM_ANIMALS && !string.Equals(c.Stats.CreatureType, "Animal", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static bool AlreadyUnder(CharacterController c, SpellData spell)
    {
        StatusEffectManager sem = c.StatusEffectManager;
        if (sem == null || !sem.HasEffect(spell.SpellId))
            return false;
        int remaining = sem.GetRemainingRounds(spell.SpellId);
        return remaining > 1 || remaining == -1;
    }
}
