using System.Collections.Generic;

/// <summary>
/// The flight a fear or a turning compels (batch 13 step 20, decision D15; explicit RAW: a frightened creature flees from
/// the source of its fear as best it can and may use special abilities, spells included, to flee, and may fight when it
/// cannot flee, PHB glossary p.309; turned undead flee by the best and fastest means available, PHB p.159; a panicked
/// creature flees at top speed and takes no other action, p.311). Pure rules, the same for every creature; the turn start
/// (GameManager.EvaluateTurnCompulsion) records their answers on <see cref="TurnCompulsion"/>, and the IsControllable fork
/// (GameManager.TryRunCompulsionTurn) only chooses between the forced flight (the shared flee controller) and the
/// restricted menu (delegated RAW reading 2026-10-29, D15: "as best it can" means a frightened or turned PC keeps choosing
/// flight while flight is possible). No dice, no log.
/// </summary>
public static class FlightRules
{
    /// <summary>
    /// The creatures <paramref name="actor"/> must flee from: its fear's source (the frightened or panicked condition's
    /// creature, FrightenedBehaviorController) and, when it is turned, its turner (the turn undead tracker, else the
    /// creature named as the Turned condition's source, else its closest living foe, as AIService.ExecuteTurnedTurn reads
    /// it). Living creatures only.
    /// </summary>
    public static List<CharacterController> Sources(GameManager gm, CharacterController actor, TurnCompulsion compulsion)
    {
        var result = new List<CharacterController>();
        if (gm == null || actor == null || compulsion == null)
            return result;
        if (compulsion.Fear != null)
            AddLiving(result, compulsion.Fear.CasterSource);
        if (compulsion.Turned)
            AddLiving(result, Turner(gm, actor));
        return result;
    }

    /// <summary>The creature a turned <paramref name="actor"/> flees from (see <see cref="Sources"/>); null when none resolves.</summary>
    public static CharacterController Turner(GameManager gm, CharacterController actor)
    {
        if (gm == null || actor == null)
            return null;
        CharacterController turner = gm.GetTurnUndeadTurnerForAI(actor);
        if (IsLiving(turner))
            return turner;

        List<StatusEffect> conditions = actor.GetActiveConditions();
        if (conditions != null)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                StatusEffect condition = conditions[i];
                if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Turned)
                    continue;
                if (string.IsNullOrWhiteSpace(condition.SourceName))
                    break;
                foreach (CharacterController candidate in gm.GetAllCharactersForAI())
                    if (IsLiving(candidate) && string.Equals(candidate.Stats.CharacterName, condition.SourceName, System.StringComparison.Ordinal))
                        return candidate;
                break;
            }
        }

        CharacterController closest = gm.GetClosestAliveEnemyToForAI(actor);
        return IsLiving(closest) ? closest : null;
    }

    /// <summary>
    /// Whether <paramref name="actor"/> has a way to flee from every creature in <paramref name="sources"/> now (D15), the
    /// flight options of the restricted menu: a grapple to escape when <paramref name="countGrapple"/> (PHB p.156: escaping
    /// is a grappled creature's only way to flee; the turn start counts it, the restricted menu has no grapple escape yet,
    /// CMB-210); an Escape spell the cast core would let it cast (<see cref="HasEscapeSpell"/>; PHB p.309: spells may be
    /// used to flee); a move that ends farther from every source (<see cref="HasMoveFlight"/>). Pure.
    /// </summary>
    public static bool HasFlightOption(GameManager gm, CharacterController actor, IList<CharacterController> sources, bool countGrapple = true)
    {
        if (gm == null || actor == null || actor.Stats == null || actor.Actions == null || !AnyLiving(sources))
            return false;
        if (actor.IsGrappling())
            return countGrapple;
        if (HasEscapeSpell(gm, actor, sources))
            return true;
        return HasMoveFlight(gm, actor, sources, includeShortMoves: true);
    }

    /// <summary>
    /// Whether a move flight is open that ends farther from every living source (<see cref="EndsFartherFromAll"/>): a move,
    /// double move or withdraw (the cell search of the shared flight, FrightenedBehaviorController.FindBestFleeCell, within
    /// the movement the shared legality allows now, MovementService.CanStartMovement); for a prone creature, standing up
    /// with its move action and then moving with its standard action (PHB p.141-143). With
    /// <paramref name="includeShortMoves"/>, also a prone creature's crawl when it cannot stand and move (PHB p.143). A
    /// 5-foot step is never a flight option of its own, and a run is not counted until the Run action exists (CMB-202): a
    /// PC that can get away only by running is cornered for the menu and keeps the forced flight, which runs. Cached per
    /// creature and board state (the action panel asks on every refresh).
    /// </summary>
    public static bool HasMoveFlight(GameManager gm, CharacterController actor, IList<CharacterController> sources, bool includeShortMoves)
    {
        if (gm == null || actor == null || actor.Stats == null || actor.Actions == null || !AnyLiving(sources))
            return false;
        if (actor.Stats.MovementBlockedByCondition)
            return false;

        int key = BoardKey(gm, actor, sources, includeShortMoves);
        if (_cacheActor == actor && _cacheKey == key)
            return _cacheResult;
        bool result = ComputeMoveFlight(gm, actor, sources, includeShortMoves);
        _cacheActor = actor;
        _cacheKey = key;
        _cacheResult = result;
        return result;
    }

    private static bool ComputeMoveFlight(GameManager gm, CharacterController actor, IList<CharacterController> sources, bool includeShortMoves)
    {
        CharacterController first = FirstLiving(sources);
        int current = SquareGridUtils.GetDistance(actor.GridPosition, first.GridPosition);
        UnityEngine.Vector2Int from = actor.GridPosition;
        System.Func<UnityEngine.Vector2Int, bool> accept = cell => EndsFartherFromAll(from, cell, sources);

        if (actor.Stats.IsProne)
        {
            // Stand up (a move action), then move with the standard action converted (PHB p.141-143).
            bool canStandAndMove = actor.Actions.HasMoveAction && actor.Actions.HasStandardAction
                && !actor.Actions.SingleActionOnly && !actor.Actions.MoveActionOnly
                && string.IsNullOrEmpty(gm.GetStandUpDisabledReason(actor))
                && actor.Stats.MoveRange > 0
                && FrightenedBehaviorController.FindBestFleeCell(gm, actor, first.GridPosition, withdraw: false, running: false, current, actor.Stats.MoveRange, accept) != null;
            if (canStandAndMove || !includeShortMoves)
                return canStandAndMove;
            return HasCrawlFlight(gm, actor, sources);
        }

        if (MovementService.CanStartMovement(actor, MoveKind.Move).Ok
            && FrightenedBehaviorController.FindBestFleeCell(gm, actor, first.GridPosition, withdraw: false, running: false, current, -1, accept) != null)
            return true;
        return MovementService.CanStartMovement(actor, MovementService.WithdrawKindFor(actor)).Ok
            && FrightenedBehaviorController.FindBestFleeCell(gm, actor, first.GridPosition, withdraw: true, running: false, current, -1, accept) != null;
    }

    /// <summary>A prone creature's crawl (the crawl entry's shared legality) into an adjacent square farther from every source (PHB p.143).</summary>
    private static bool HasCrawlFlight(GameManager gm, CharacterController actor, IList<CharacterController> sources)
    {
        ActionDefinition crawl = ActionCatalog.Get(ActionIds.Crawl);
        if (crawl == null || crawl.Executor == null || !crawl.Executor.CanStart(actor).Ok)
            return false;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                var cell = new UnityEngine.Vector2Int(actor.GridPosition.x + dx, actor.GridPosition.y + dy);
                if (EndsFartherFromAll(actor.GridPosition, cell, sources) && gm.IsValidCrawlDestinationForActions(actor, cell))
                    return true;
            }
        return false;
    }

    /// <summary>
    /// An Escape spell (SpellEffectType.Escape) the creature can cast now: a slot and the standard action left, the cast
    /// core's caster tests (GameManager.CheckCasterLegalityForAI: the metamagic, the casting action, Silence, the
    /// components), and for Dimension Door a destination farther from every source (GameManager.ChooseDimensionDoorDestination).
    /// </summary>
    public static bool HasEscapeSpell(GameManager gm, CharacterController actor, IList<CharacterController> sources = null)
    {
        if (gm == null || actor == null || actor.Actions == null || !actor.Actions.HasStandardAction)
            return false;
        SpellcastingComponent spells = actor.Spellcasting;
        if (spells == null)
            return false;
        foreach (SpellData spell in spells.GetCastableSpells())
        {
            if (spell == null || spell.EffectType != SpellEffectType.Escape)
                continue;
            if (gm.CheckCasterLegalityForAI(actor, spell) != null)
                continue;
            if (string.Equals(spell.SpellId, DND35e.Identifiers.SpellNames.DIMENSION_DOOR, System.StringComparison.Ordinal)
                && !gm.ChooseDimensionDoorDestination(actor, spell, sources ?? CurrentSources(actor)).HasValue)
                continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// The cast refusal of a chosen flight (D15; PHB p.309: spells may be used to flee): any spell but an Escape spell. The
    /// cast core's Legality stage reads it for every PC cast path (the spell menu, an imbued spell, a scroll, a wand, a held
    /// charge's delivery). Null when the cast may go on or the turn is no chosen flight.
    /// </summary>
    public static string CastRefusal(CharacterController actor, SpellData spell)
    {
        if (spell == null || spell.EffectType == SpellEffectType.Escape || !IsOnChosenFlight(actor, out TurnCompulsion compulsion))
            return null;
        return $"{actor.Stats.CharacterName} cannot cast {spell.Name}: it must flee ({Why(compulsion)}); only a spell that carries it away";
    }

    /// <summary>
    /// True while <paramref name="actor"/>'s turn is a chosen flight (<see cref="TurnCompulsion.ChoosesFlight"/>, set by the
    /// turn start for a frightened or turned creature that is not cornered), and it still must flee: a living source.
    /// </summary>
    public static bool IsOnChosenFlight(CharacterController actor, out TurnCompulsion compulsion)
    {
        compulsion = actor != null && actor.CurrentTurnStart != null ? actor.CurrentTurnStart.Compulsion : null;
        if (compulsion == null || !compulsion.ChoosesFlight || !compulsion.CompelsFlight)
            return false;
        return AnyLiving(compulsion.FlightSources);
    }

    /// <summary>True when <paramref name="to"/> is farther than <paramref name="from"/> from every living creature in <paramref name="sources"/>.</summary>
    public static bool EndsFartherFromAll(UnityEngine.Vector2Int from, UnityEngine.Vector2Int to, IList<CharacterController> sources)
    {
        if (sources == null)
            return true;
        for (int i = 0; i < sources.Count; i++)
        {
            CharacterController source = sources[i];
            if (!IsLiving(source))
                continue;
            if (SquareGridUtils.GetDistance(to, source.GridPosition) <= SquareGridUtils.GetDistance(from, source.GridPosition))
                return false;
        }
        return true;
    }

    /// <summary>
    /// The refusal of a move, withdraw, 5-foot step or crawl to <paramref name="destination"/> on a chosen flight (D15): the
    /// move must end farther from every living flight source than the creature stands now (the shared flight's own distance
    /// test, SquareGridUtils.GetDistance). A 5-foot step or a crawl (<paramref name="shortMove"/>) is refused too while a
    /// fuller move flight is open (<see cref="HasMoveFlight"/> without short moves; "as best it can", PHB p.309, p.159): it
    /// would cut the flight short. Null when the move may go on, or when the turn is no chosen flight.
    /// </summary>
    public static string DestinationRefusal(CharacterController actor, UnityEngine.Vector2Int destination, bool shortMove = false)
    {
        if (!IsOnChosenFlight(actor, out TurnCompulsion compulsion))
            return null;
        for (int i = 0; i < compulsion.FlightSources.Count; i++)
        {
            CharacterController source = compulsion.FlightSources[i];
            if (!IsLiving(source))
                continue;
            int now = SquareGridUtils.GetDistance(actor.GridPosition, source.GridPosition);
            int after = SquareGridUtils.GetDistance(destination, source.GridPosition);
            if (after <= now)
                return $"{actor.Stats.CharacterName} must flee from {source.Stats.CharacterName}: every move ends farther away ({Why(compulsion)})";
        }
        if (shortMove && HasMoveFlight(GameManager.Instance, actor, compulsion.FlightSources, includeShortMoves: false))
            return $"{actor.Stats.CharacterName} must flee as best it can: a move, double move or withdraw carries it farther ({Why(compulsion)})";
        return null;
    }

    /// <summary>
    /// The refusal of End Turn on a chosen flight while a flight option the restricted menu offers is left (D15: "as best
    /// it can"; <see cref="HasFlightOption"/> against every source at once, the grapple escape not counted since the menu
    /// has none yet, CMB-210): null when the turn is no chosen flight or no option is left (the creature has fled as far as
    /// it can this turn).
    /// </summary>
    public static string EndTurnRefusal(GameManager gm, CharacterController actor)
    {
        if (!IsOnChosenFlight(actor, out TurnCompulsion compulsion))
            return null;
        if (!HasFlightOption(gm, actor, compulsion.FlightSources, countGrapple: false))
            return null;
        CharacterController source = FirstLiving(compulsion.FlightSources);
        return $"{actor.Stats.CharacterName} must flee from {source.Stats.CharacterName} while it can ({Why(compulsion)})";
    }

    /// <summary>The rule a flight refusal cites.</summary>
    public static string Why(TurnCompulsion compulsion)
        => compulsion != null && compulsion.Fear == null && compulsion.Turned ? "turned, PHB p.159" : "frightened, PHB p.309";

    private static void AddLiving(List<CharacterController> list, CharacterController c)
    {
        if (IsLiving(c) && !list.Contains(c))
            list.Add(c);
    }

    private static bool IsLiving(CharacterController c) => c != null && c.Stats != null && !c.Stats.IsDead;

    private static bool AnyLiving(IList<CharacterController> list) => FirstLiving(list) != null;

    private static CharacterController FirstLiving(IList<CharacterController> list)
    {
        if (list == null)
            return null;
        for (int i = 0; i < list.Count; i++)
            if (IsLiving(list[i]))
                return list[i];
        return null;
    }

    /// <summary>The flight sources of the actor's turn, or null when nothing compels it to flee.</summary>
    private static IList<CharacterController> CurrentSources(CharacterController actor)
    {
        TurnCompulsion compulsion = actor != null && actor.CurrentTurnStart != null ? actor.CurrentTurnStart.Compulsion : null;
        return compulsion != null && compulsion.CompelsFlight ? compulsion.FlightSources : null;
    }

    // The move-flight cache: the action panel asks HasMoveFlight on every refresh, and its cell search runs a path search
    // per cell. The key covers what the answer reads: the actor's square, action economy and posture, and every living
    // creature's square (the sources' and the blockers').
    private static CharacterController _cacheActor;
    private static int _cacheKey;
    private static bool _cacheResult;

    private static int BoardKey(GameManager gm, CharacterController actor, IList<CharacterController> sources, bool includeShortMoves)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + actor.GridPosition.GetHashCode();
            h = h * 31 + (actor.Actions.HasMoveAction ? 1 : 0);
            h = h * 31 + (actor.Actions.HasStandardAction ? 2 : 0);
            h = h * 31 + (actor.Actions.SingleActionOnly ? 4 : 0);
            h = h * 31 + (actor.Actions.MoveActionOnly ? 8 : 0);
            h = h * 31 + (actor.HasTakenFiveFootStep ? 16 : 0);
            h = h * 31 + (actor.Stats.IsProne ? 32 : 0);
            h = h * 31 + (includeShortMoves ? 64 : 0);
            h = h * 31 + actor.Stats.MoveRange;
            for (int i = 0; sources != null && i < sources.Count; i++)
                h = h * 31 + (IsLiving(sources[i]) ? sources[i].GridPosition.GetHashCode() + i : -1);
            List<CharacterController> all = gm.GetAllCharactersForAI();
            for (int i = 0; all != null && i < all.Count; i++)
                if (IsLiving(all[i]))
                    h = h * 31 + all[i].GridPosition.GetHashCode();
            return h;
        }
    }
}
