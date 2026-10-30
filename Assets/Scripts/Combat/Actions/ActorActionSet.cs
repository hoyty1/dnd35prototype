using System.Collections.Generic;

/// <summary>One action of an actor's legal set (<see cref="ActorActionSet"/>): the entry, whether it may be taken now and why not, and whether taking it now provokes.</summary>
public sealed class ActionOption
{
    public ActionDefinition Definition;

    /// <summary>Allowed, or the refusal with its bar (today's legality through the ActionLegality adapters, then the compulsion filter).</summary>
    public Legality Legality;

    /// <summary>
    /// Taking it now would itself provoke from a foe that threatens the actor (Table 8-2: a "Yes" action, or an attack
    /// with a ranged weapon, PHB p.137). Movement is not counted: a ByMovement action provokes by the squares it leaves.
    /// Partial until batch 22 (nothing reads it yet): of the Varies entries only an attack or full attack with a ranged
    /// weapon is answered; an unarmed attack without Improved Unarmed Strike, a trip without Improved Trip (PHB p.158),
    /// a spittle or ranged special and a spell-like bardic effect read as not provoking.
    /// </summary>
    public bool ProvokesNow;

    public override string ToString() => $"{Definition?.Id}: {Legality}{(ProvokesNow ? " (provokes)" : string.Empty)}";
}

/// <summary>
/// The actions an actor may take now (batch 13 step 6, docs/designs/action_catalog_batch13.md "End state"; design choice
/// C8, shadow mode): one <see cref="ActionOption"/> per catalog entry the actor has (ActionCatalog.AvailableTo), its
/// legality read from today's rules and nothing else:
/// - an entry with an executor: the executor's CanStart (its action, its conditions, its resources);
/// - a PC-only entry: the rule the PC panel reads today, through the ActionLegality adapters (pick up, drop, turn
///   undead, domain power, rage; crawl, drop prone and reload have executors since batch 13 step 12, aid another, waking
///   an ally and attacking a wall since step 13); one with no shared rule yet (use item) is refused with its status and
///   issue, as a Missing or OutOfScope entry is;
/// then the compulsion filter of the turn (CharacterController.CurrentTurnStart, decided once at its start): a frightened,
/// panicked or turned creature flees (PHB p.309-311, p.159; D15), so only Retreat and Escape entries, standing up,
/// dropping an item and total defense stay legal unless it is cornered in panic, plus (in <see cref="Check"/>) a cast of
/// an Escape spell (D15: spells may be used to flee); and the charm links (PHB p.209, D14), which bar an attack, a hostile
/// maneuver or a hostile spell on the charmer (<see cref="Check"/>, <see cref="LegalTargets"/>). A confused or suggested
/// creature's turn is its controller's whole choice (the compulsion picks the action), so the set does not filter it.
/// Known gaps, for step 20 (which builds the restricted menus on this filter): a frightened creature's class or monster
/// ability that would carry it away (no Escape tag on those entries yet) reads as barred; bardic music on the charmer is
/// never charm-barred (whether a song is hostile depends on the song, which the context does not carry); a cast or a held
/// charge whose spell the context does not name (Allows, LegalTargets) is not charm-barred.
/// Pure: no dice, no log, no state change. Nothing reads it yet but GameManager.RunAIAction's shadow log and
/// ActionPanelParityTests; batch 22 makes the PC panel and the evaluator read it.
/// </summary>
public sealed class ActorActionSet
{
    public CharacterController Actor { get; private set; }
    public ActorSource Source { get; private set; }

    private readonly List<ActionOption> _options = new List<ActionOption>();
    private readonly Dictionary<string, ActionOption> _byId = new Dictionary<string, ActionOption>(System.StringComparer.Ordinal);
    private GameManager _gm;

    public IReadOnlyList<ActionOption> Options => _options;

    public static ActorActionSet Build(GameManager gm, CharacterController actor, ActorSource source)
    {
        var set = new ActorActionSet { Actor = actor, Source = source, _gm = gm };
        if (actor == null || actor.Stats == null)
            return set;

        List<CharacterController> all = gm != null ? gm.GetAllCharactersForAI() : null;
        bool threatened = all != null && ThreatSystem.GetActionAoOProvokers(actor, all).Count > 0;

        foreach (ActionDefinition d in ActionCatalog.AvailableTo(actor))
        {
            Legality legality = TodayLegality(gm, d, actor);
            if (legality.Ok)
                legality = CompulsionFilter(d, actor);
            var option = new ActionOption
            {
                Definition = d,
                Legality = legality,
                ProvokesNow = threatened && ProvokesItself(d, actor),
            };
            set._options.Add(option);
            set._byId[d.Id] = option;
        }
        return set;
    }

    /// <summary>The option of <paramref name="id"/>, or null when the actor does not have that action.</summary>
    public ActionOption Get(string id) => id != null && _byId.TryGetValue(id, out ActionOption o) ? o : null;

    /// <summary>Whether the actor has the action and may take it now (with no target yet).</summary>
    public bool Allows(string id)
    {
        ActionOption o = Get(id);
        return o != null && o.Legality.Ok;
    }

    /// <summary>
    /// Whether the action may be taken as <paramref name="ctx"/> asks: the option's legality, then the charm link on its
    /// target. A Delegating executor's core may still refuse (the set does not ask the cores' request checks).
    /// </summary>
    public Legality Check(string id, ActionContext ctx = null)
    {
        ActionOption o = Get(id);
        if (o == null)
        {
            ActionDefinition d = ActionCatalog.Get(id);
            return Legality.Refused(LegalityBar.Other, d == null
                ? "no catalog action " + id
                : (Actor != null && Actor.Stats != null ? Actor.Stats.CharacterName : "the actor") + " does not have " + d.Name);
        }
        if (!o.Legality.Ok)
        {
            // D15 (PHB p.309, p.159): a frightened or turned creature may use a spell to flee; an Escape spell is its flight.
            if (o.Legality.Bar == LegalityBar.Compulsion && o.Definition.Id == ActionIds.CastSpell
                && ctx != null && ctx.Spell != null && ctx.Spell.EffectType == SpellEffectType.Escape
                && TodayLegality(_gm, o.Definition, Actor).Ok)
                return Legality.Allowed();
            return o.Legality;
        }
        CharacterController target = ctx != null ? ctx.Target : null;
        if (target != null && IsCharmBarred(o.Definition, target, ctx))
            return Legality.Refused(LegalityBar.Charm, Actor.Stats.CharacterName + " is charmed by " + target.Stats.CharacterName + " and makes no attack on its charmer (PHB p.209)");
        return Legality.Allowed();
    }

    /// <summary>
    /// The creatures the action may be aimed at now (an entry that targets a creature): every living creature on the
    /// field but the actor itself, less the charmer for an attack (PHB p.209). Empty when the action is not legal now.
    /// Range, reach and line of sight are the executors' (the set does not ask them).
    /// </summary>
    public List<CharacterController> LegalTargets(string id)
    {
        var result = new List<CharacterController>();
        ActionOption o = Get(id);
        if (o == null || !o.Legality.Ok || o.Definition.Target != ActionTargetKind.Creature || _gm == null)
            return result;
        foreach (CharacterController c in _gm.GetAllCharactersForAI())
        {
            if (c == null || c == Actor || c.Stats == null || c.Stats.IsDead)
                continue;
            if (IsCharmBarred(o.Definition, c, null))
                continue;
            result.Add(c);
        }
        return result;
    }

    // ── Today's legality ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The entry's legality from today's rules only (see the class summary).</summary>
    internal static Legality TodayLegality(GameManager gm, ActionDefinition d, CharacterController actor)
    {
        if (d.Executor != null)
            return d.Executor.CanStart(actor);
        if (d.Status == ImplStatus.Missing || d.Status == ImplStatus.OutOfScope || d.Status == ImplStatus.AiOnly)
            return NotBuilt(d);

        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        switch (d.Id)
        {
            case ActionIds.PickUpItem:
                return ActionLegality.FromDisabledReason(gm.GetPickUpItemDisabledReason(actor), LegalityBar.Equipment);
            case ActionIds.DropItem:
                return ActionLegality.FromDisabledReason(gm.GetDropEquippedItemDisabledReason(actor), LegalityBar.Equipment);
            case ActionIds.TurnUndead:
                return ActionLegality.FromCheck(gm.CanUseTurnUndead(actor, out string turnReason), turnReason, LegalityBar.Resource);
            case ActionIds.DomainPower:
                return ActionLegality.FromCheck(gm.GetAvailableDomainPowers(actor).Count > 0, "no domain power left", LegalityBar.Resource);
            case ActionIds.Rage:
            {
                // The rage gate of CharacterStats.ActivateRage (PHB p.25-26), which the PC Rage button reads; step 16 shares it.
                CharacterStats s = actor.Stats;
                bool canRage = s.IsBarbarian && !s.IsRaging && !s.IsFatiguedOrExhausted && s.RagesUsedToday < s.MaxRagesPerDay;
                return ActionLegality.FromCheck(canRage, s.IsRaging ? "already raging" : (s.IsFatiguedOrExhausted ? "fatigued" : "no rage left today"), LegalityBar.Resource);
            }
            default:
                return NotBuilt(d);
        }
    }

    private static Legality NotBuilt(ActionDefinition d)
    {
        string issues = d.IssueIds != null && d.IssueIds.Length > 0 ? " (" + string.Join(", ", d.IssueIds) + ")" : string.Empty;
        return Legality.Refused(LegalityBar.Other, d.Name + " has no shared legality yet: " + d.Status + issues);
    }

    /// <summary>
    /// The turn's compulsion (CurrentTurnStart.Compulsion, D15): a frightened, panicked or turned creature flees, so only
    /// the entries that flee or free it stay legal: Retreat or Escape roles, standing up, dropping an item, and total
    /// defense (its own Check allows it only to a cornered panicked creature, PHB p.311). A cornered panicked creature
    /// keeps what its cowering allows (total defense); a cornered frightened creature may fight (PHB p.309), which the
    /// turn start does not record yet, so its fight shows as a shadow disagreement until step 20 records it.
    /// </summary>
    internal static Legality CompulsionFilter(ActionDefinition d, CharacterController actor)
    {
        TurnStartState start = actor.CurrentTurnStart;
        TurnCompulsion compulsion = start != null ? start.Compulsion : null;
        if (compulsion == null || (compulsion.Fear == null && !compulsion.Turned))
            return Legality.Allowed();
        if ((d.Roles & (ActionRoles.Retreat | ActionRoles.Escape)) != 0)
            return Legality.Allowed();
        if (d.Id == ActionIds.StandUp || d.Id == ActionIds.DropItem || d.Id == ActionIds.TotalDefense)
            return Legality.Allowed();
        return Legality.Refused(LegalityBar.Compulsion, compulsion.Turned
            ? "turned: it flees from the turner (PHB p.159)"
            : "afraid: it flees from the source of its fear (PHB p.309-311)");
    }

    /// <summary>Table 8-2's provocation of the action itself: "Yes", or an attack entry with a ranged weapon in hand (PHB p.137).</summary>
    private static bool ProvokesItself(ActionDefinition d, CharacterController actor)
    {
        switch (d.Provokes)
        {
            case ProvokeRule.Yes:
                return true;
            case ProvokeRule.Varies:
                return (d.Id == ActionIds.Attack || d.Id == ActionIds.FullAttack) && actor.IsEquippedWeaponRanged();
            default:
                return false;
        }
    }

    /// <summary>
    /// The charm link (PHB p.209, D14): a charmed creature makes no attack, hostile maneuver or hostile spell on its
    /// charmer. Barred: an Attack-role entry that neither heals nor supports; every maneuver but aid another and waking an
    /// ally; a cast or held charge whose spell (<paramref name="ctx"/>) is hostile to the charmer (damage, debuff, control,
    /// wall, or energy that harms it); lay on hands on an undead charmer (positive energy harms it, PHB p.44). A cast or a
    /// held charge with no spell named, and bardic music, are not barred (see the class summary's gaps).
    /// </summary>
    private bool IsCharmBarred(ActionDefinition d, CharacterController target, ActionContext ctx)
    {
        if (_gm == null || target == null || target.Stats == null || !_gm.ActionCore_IsCharmedBy(Actor, target))
            return false;
        switch (d.Id)
        {
            case ActionIds.AidAnother:
            case ActionIds.WakeAlly:
            case ActionIds.BardicMusic:
                return false;
            case ActionIds.CastSpell:
            case ActionIds.DeliverHeldCharge:
            {
                SpellData spell = ctx != null ? ctx.Spell : null;
                if (spell == null)
                    return false;
                switch (spell.EffectType)
                {
                    case SpellEffectType.Damage:
                    case SpellEffectType.Debuff:
                    case SpellEffectType.Control:
                    case SpellEffectType.Wall:
                        return true;
                    default:
                        return SpellDiceRules.Harms(spell, target.Stats);
                }
            }
            case ActionIds.LayOnHands:
                return SpellDiceRules.IsUndead(target.Stats);
        }
        if (d.Category == ActionCategory.Maneuver)
            return true;
        return (d.Roles & ActionRoles.Attack) != 0 && (d.Roles & (ActionRoles.Heal | ActionRoles.Support)) == 0;
    }
}

/// <summary>
/// The shadow mode's record (batch 13 step 6, C8): every AI pick that GameManager.RunAIAction runs outside its actor's
/// legal set, and every set that could not be built. The Debug.Log line "[ActionSet] ..." is for watch mode only: the
/// scenario harness's fast mode filters Log-level lines out (ScenarioFastMode), so the harness reads this sink instead.
/// ScenarioRunner clears it when a job starts and writes it into the job's result and summary.json ("actionSet"), outside
/// the trace hash. Off in a player build that is not a development build (<see cref="Enabled"/>), where nothing reads it.
/// </summary>
public static class ActionSetShadow
{
    /// <summary>The first lines kept verbatim; the counts keep every distinct one.</summary>
    public const int MaxLines = 20;

    private static readonly Dictionary<string, int> _counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
    private static readonly List<string> _lines = new List<string>();

    /// <summary>Whether RunAIAction builds the set at all: in the editor and in development builds only.</summary>
    public static bool Enabled => UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild;

    /// <summary>Disagreements recorded since the last <see cref="Clear"/>.</summary>
    public static int Total { get; private set; }

    /// <summary>The distinct disagreements ("actionId: bar: reason") and how often each came up.</summary>
    public static IReadOnlyDictionary<string, int> Counts => _counts;

    /// <summary>The first <see cref="MaxLines"/> disagreements, with their actors.</summary>
    public static IReadOnlyList<string> Lines => _lines;

    public static void Clear()
    {
        _counts.Clear();
        _lines.Clear();
        Total = 0;
    }

    /// <summary>Records one pick outside the legal set (or a set that threw, <paramref name="bar"/> "Error").</summary>
    public static void Record(string actorName, string actionId, string bar, string reason)
    {
        Total++;
        string key = (actionId ?? "?") + ": " + (bar ?? "?") + ": " + (reason ?? string.Empty);
        _counts.TryGetValue(key, out int n);
        _counts[key] = n + 1;
        if (_lines.Count < MaxLines)
            _lines.Add((actorName ?? "?") + " picked " + key);
    }
}
