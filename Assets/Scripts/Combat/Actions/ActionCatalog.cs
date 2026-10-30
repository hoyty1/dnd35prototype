using System;
using System.Collections.Generic;

/// <summary>
/// The action catalog (batch 13 step 3, docs/designs/action_catalog_batch13.md; docs/architecture/combat-actions.md):
/// one <see cref="ActionDefinition"/> per combat action in scope, with its PHB p.141 Table 8-2 cost and provocation, its
/// rules page, its roles, its build status and issues, and its shared executor. Lazy <see cref="Init"/>; the entries are
/// shared and never mutated. The status column is the source of docs/systems/AI_ACTION_COVERAGE.md.
///
/// Table 8-2 as read from the book (the text extract's move-action column is offset by two rows and its full-round
/// column by one; realigned 2026-10-29): loading a hand or light crossbow, standing up, picking up an item, sheathing a
/// weapon and retrieving a stored item provoke; drawing a weapon, readying a shield, opening a door and mounting do not;
/// a coup de grace provokes and a full attack, a charge and a withdraw do not. Where the action itself does not provoke
/// but the movement it makes does (move, charge, withdraw, bull rush, overrun, crawl), the entry says ByMovement.
/// </summary>
public static class ActionCatalog
{
    private static Dictionary<string, ActionDefinition> _byId;
    private static List<ActionDefinition> _all;

    public static void Init()
    {
        if (_byId != null)
            return;
        var list = new List<ActionDefinition>();
        Build(list);
        var byId = new Dictionary<string, ActionDefinition>(StringComparer.Ordinal);
        foreach (ActionDefinition d in list)
        {
            if (byId.ContainsKey(d.Id))
                throw new InvalidOperationException("ActionCatalog: duplicate action id " + d.Id);
            byId[d.Id] = d;
        }
        _all = list;
        _byId = byId;
    }

    /// <summary>The entry with <paramref name="id"/> (an <see cref="ActionIds"/> constant), or null.</summary>
    public static ActionDefinition Get(string id)
    {
        Init();
        return id != null && _byId.TryGetValue(id, out ActionDefinition d) ? d : null;
    }

    /// <summary>Every entry, in catalog order.</summary>
    public static IReadOnlyList<ActionDefinition> All
    {
        get
        {
            Init();
            return _all;
        }
    }

    /// <summary>The entry of a maneuver (every <see cref="SpecialAttackType"/> has exactly one).</summary>
    public static ActionDefinition ForManeuver(SpecialAttackType type)
    {
        Init();
        foreach (ActionDefinition d in _all)
            if (d.Maneuver.HasValue && d.Maneuver.Value == type)
                return d;
        return null;
    }

    /// <summary>
    /// The entries <paramref name="actor"/> has at all (<see cref="ActionDefinition.IsAvailableTo"/>): every common
    /// action, plus its class abilities and its per-creature abilities (breath, spittle, ranged specials, engulf). Not
    /// whether it can take them now: that is the executor's CanStart (and from step 6 the legal set, ActorActionSet).
    /// </summary>
    public static List<ActionDefinition> AvailableTo(CharacterController actor)
    {
        Init();
        var result = new List<ActionDefinition>();
        foreach (ActionDefinition d in _all)
            if (d.IsAvailableTo(actor))
                result.Add(d);
        return result;
    }

    // ── The entries ────────────────────────────────────────────────────────────────────────────────────────────────

    private static ActionDefinition Def(string id, string name, ActionCategory category, ActionCostType cost, ProvokeRule provokes,
        ActionTargetKind target, ActionRoles roles, string rulesRef, ImplStatus status, params string[] issues)
    {
        return new ActionDefinition
        {
            Id = id,
            Name = name,
            Category = category,
            Cost = cost,
            Provokes = provokes,
            Target = target,
            Roles = roles,
            RulesRef = rulesRef,
            Status = status,
            IssueIds = issues ?? Array.Empty<string>(),
        };
    }

    private static ActionDefinition Run(this ActionDefinition d, IActionExecutor executor, ActionUiAfter after = ActionUiAfter.None, float seconds = 0f)
    {
        d.Executor = executor;
        d.UiAfter = after;
        d.UiAfterSeconds = seconds;
        return d;
    }

    private static ActionDefinition AsManeuver(this ActionDefinition d, SpecialAttackType type)
    {
        d.Maneuver = type;
        d.SubstitutesForMeleeAttack = ManeuverActionCost.ReplacesMeleeAttack(type);
        return d;
    }

    private static ActionDefinition For(this ActionDefinition d, Func<CharacterController, bool> availability)
    {
        d.Availability = availability;
        return d;
    }

    private static ActionDefinition Costs(this ActionDefinition d, Func<CharacterController, ActionContext, ActionCostType> rule)
    {
        d.CostRule = rule;
        return d;
    }

    /// <summary>A charge or a withdraw by a creature limited to one action is a standard action (PHB p.141 Table 8-2 note 5, p.154, p.144).</summary>
    private static ActionCostType SingleActionStandard(CharacterController actor, ActionContext ctx)
        => actor != null && actor.Actions != null && actor.Actions.SingleActionOnly ? ActionCostType.Standard : ActionCostType.FullRound;

    private static ActionCostType ManeuverCostFor(SpecialAttackType type, CharacterController actor)
        => ManeuverActionCost.For(type, actor).Action;

    private static ActionCostType CastCostFor(CharacterController actor, ActionContext ctx)
    {
        if (ctx != null && ctx.CoreRequest is SpellCastRequest req && req.Spell != null)
            return ActionCosts.From(CastActionRules.CastingAction(req.Spell, req.Metamagic, req.Source));
        if (ctx != null && ctx.Spell != null)
            return ActionCosts.From(CastActionRules.CastingAction(ctx.Spell, null, new CastSource()));
        return ActionCostType.Standard;
    }

    private static ActionCostType HeldChargeCostFor(CharacterController actor, ActionContext ctx)
    {
        CharacterController target = ctx != null ? ctx.Target : null;
        if (actor == null || target == null)
            return ActionCostType.Standard;
        switch (HeldChargeDeliveryRules.CostFor(actor, target))
        {
            case HeldChargeDeliveryCost.None: return ActionCostType.NotAnAction;
            default: return ActionCostType.Standard; // a touch in place of an attack, or a friend's touch (PHB p.141)
        }
    }

    /// <summary>
    /// The use's own action for the context's item in hand (D7; GameManager.GetItemUseCost): a standard action, a scroll's
    /// or wand's casting action, a full round to administer a potion. A stored item first needs the retrieval's move
    /// action (the retrieve-item entry's cost), which is not included here: the use-item core takes it first, so a planner
    /// must add a move action for an item not in hand (CharacterController.IsHoldingRetrievedItem; AI-009, batch 15).
    /// </summary>
    private static ActionCostType UseItemCostFor(CharacterController actor, ActionContext ctx)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || ctx == null || ctx.Item == null)
            return ActionCostType.Standard;
        return gm.GetItemUseCost(actor, ctx.Item, ctx.Variant == ItemUseRules.VariantAdminister);
    }

    /// <summary>
    /// The draw's action for the context's item (D8; EquipmentActionRules.DrawCost): free with Quick Draw, with a regular
    /// move at base attack bonus +1, or as a Two-Weapon Fighting creature's second draw; else a move action.
    /// </summary>
    private static ActionCostType DrawCostFor(CharacterController actor, ActionContext ctx)
        => EquipmentActionRules.DrawCost(actor, ctx != null ? ctx.Item : null);

    /// <summary>The load's action by the weapon and Rapid Reload (D4; PHB p.115, p.99): ReloadRules.CostFor.</summary>
    private static ActionCostType ReloadCostFor(CharacterController actor, ActionContext ctx)
        => ReloadRules.CostFor(actor);

    /// <summary>
    /// The attack entries' AI mode chooser (design choice C2, inherited Q2): the AI stopgap's expected-hits Rapid Shot rule
    /// and expected-damage two-weapon rule, formula unchanged (AIProfile.DefaultShouldUseRapidShot and
    /// DefaultShouldUseTwoWeaponFighting); an interim rule, replaced by batch 22's evaluator. AttackIntent.ForAI reads it.
    /// </summary>
    private static readonly AiModeValuation AttackModeValuation = new AiModeValuation
    {
        RapidShot = DND35.AI.AIProfile.DefaultShouldUseRapidShot,
        TwoWeapon = DND35.AI.AIProfile.DefaultShouldUseTwoWeaponFighting,
    };

    private static ActionDefinition WithModeValuation(this ActionDefinition d)
    {
        d.AiModeValuation = AttackModeValuation;
        return d;
    }

    private static ActionCostType BardicCostFor(CharacterController actor, ActionContext ctx)
        => ctx != null && ctx.Variant == BardicMusicActionExecutor.StopVariant ? ActionCostType.Free : ActionCostType.Standard;

    private static void Build(List<ActionDefinition> l)
    {
        const ActionCategory A = ActionCategory.Attack, M = ActionCategory.Maneuver, Mv = ActionCategory.Movement,
            Mg = ActionCategory.Magic, C = ActionCategory.ClassAbility, D = ActionCategory.Defense, P = ActionCategory.Posture,
            E = ActionCategory.Equipment, I = ActionCategory.Item, Mo = ActionCategory.MonsterAbility, T = ActionCategory.TurnOrder;

        // ── Attacks ──
        l.Add(Def(ActionIds.Attack, "Attack", A, ActionCostType.Standard, ProvokeRule.Varies, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.139-141, Table 8-2 (melee No, ranged Yes, unarmed Yes)", ImplStatus.Delegating)
            .Run(new AttackSequenceActionExecutor(AttackSequenceActionExecutor.Kind.Attack), ActionUiAfter.AttackDelay, 1.0f)
            .WithModeValuation());
        l.Add(Def(ActionIds.FullAttack, "Full Attack", A, ActionCostType.FullRound, ProvokeRule.Varies, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.143, Table 8-2", ImplStatus.Delegating)
            .Run(new AttackSequenceActionExecutor(AttackSequenceActionExecutor.Kind.FullAttack), ActionUiAfter.AttackDelay, 1.0f)
            .WithModeValuation());
        l.Add(Def(ActionIds.FlurryOfBlows, "Flurry of Blows", A, ActionCostType.FullRound, ProvokeRule.Varies, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.40-41, p.143", ImplStatus.Delegating)
            .Run(new AttackSequenceActionExecutor(AttackSequenceActionExecutor.Kind.Flurry), ActionUiAfter.AttackDelay, 1.0f)
            .For(c => c.Stats.IsMonk));
        l.Add(Def(ActionIds.Charge, "Charge", A, ActionCostType.FullRound, ProvokeRule.ByMovement, ActionTargetKind.Creature, ActionRoles.Attack | ActionRoles.Approach,
                "PHB p.154-155, Table 8-2 (No) and note 5", ImplStatus.Delegating)
            .Run(new ChargeActionExecutor(false), ActionUiAfter.AttackDelay, 1.0f)
            .Costs(SingleActionStandard));
        l.Add(Def(ActionIds.DeliverHeldCharge, "Deliver Held Charge", A, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Creature,
                ActionRoles.Attack | ActionRoles.Heal | ActionRoles.Support, "PHB p.140-141 (touch spells, holding the charge)", ImplStatus.Delegating)
            .Run(new DeliverHeldChargeActionExecutor(), ActionUiAfter.AttackDelay, 1.0f)
            .For(c => c.Spellcasting != null)
            .Costs(HeldChargeCostFor));
        l.Add(Def(ActionIds.AttackWall, "Attack a Wall", A, ActionCostType.Standard, ProvokeRule.Maybe, ActionTargetKind.Cell, ActionRoles.Attack | ActionRoles.Escape,
                "PHB p.165-166 (attacking an object: ranged damage halved), Table 8-2 p.141 (Attack (melee): No; Attack (ranged): Yes), p.156 (not while grappling); p.299 (Wall of Ice: Strength check DC 15 + CL); CORE-040",
                ImplStatus.Extracted)
            .Run(new AttackWallActionExecutor(), ActionUiAfter.AttackDelay, 1.0f));

        // ── Maneuvers (one per SpecialAttackType; costs from ManeuverActionCost.For) ──
        l.Add(Def(ActionIds.Trip, "Trip", M, ActionCostType.Standard, ProvokeRule.Varies, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.158 (an unarmed melee touch attack: provokes without Improved Trip), Table 8-2 (No) and note 7", ImplStatus.Delegating)
            .Run(new ManeuverActionExecutor(SpecialAttackType.Trip), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.Trip));
        l.Add(Def(ActionIds.Disarm, "Disarm", M, ActionCostType.Standard, ProvokeRule.Yes, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.155, p.95 (Improved Disarm), Table 8-2 and note 7", ImplStatus.Delegating)
            .Run(new ManeuverActionExecutor(SpecialAttackType.Disarm), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.Disarm));
        l.Add(Def(ActionIds.Grapple, "Grapple", M, ActionCostType.Standard, ProvokeRule.Yes, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.155-157, p.95 (Improved Grapple), Table 8-2 and note 7", ImplStatus.Delegating)
            .Run(new ManeuverActionExecutor(SpecialAttackType.Grapple), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.Grapple));
        l.Add(Def(ActionIds.Sunder, "Sunder", M, ActionCostType.Standard, ProvokeRule.Yes, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.158-159, p.96 (Improved Sunder), Table 8-2 (sunder a weapon: Yes); one melee attack by owner decision 2026-10-07", ImplStatus.Delegating)
            .Run(new ManeuverActionExecutor(SpecialAttackType.Sunder), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.Sunder));
        l.Add(Def(ActionIds.BullRush, "Bull Rush", M, ActionCostType.Standard, ProvokeRule.ByMovement, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.154-155 (moving into the defender's space provokes), Table 8-2 (No)", ImplStatus.Delegating)
            .Run(new ManeuverActionExecutor(SpecialAttackType.BullRushAttack), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.BullRushAttack));
        l.Add(Def(ActionIds.BullRushCharge, "Bull Rush (Charge)", M, ActionCostType.FullRound, ProvokeRule.ByMovement, ActionTargetKind.Creature,
                ActionRoles.Attack | ActionRoles.Approach, "PHB p.154-155, Table 8-2 (charge: No) and note 5", ImplStatus.Delegating)
            .Run(new ChargeActionExecutor(true), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.BullRushCharge)
            .Costs(SingleActionStandard));
        l.Add(Def(ActionIds.Overrun, "Overrun", M, ActionCostType.Standard, ProvokeRule.ByMovement, ActionTargetKind.Cell, ActionRoles.Attack | ActionRoles.Approach,
                "PHB p.157-158 (in a move or a charge; entering the space provokes), Table 8-2 (No)", ImplStatus.Delegating)
            .Run(new OverrunActionExecutor(), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.Overrun));
        l.Add(Def(ActionIds.Feint, "Feint", M, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Creature, ActionRoles.Attack | ActionRoles.Support,
                "PHB p.155, p.95 (Improved Feint: a move action), Table 8-2", ImplStatus.Delegating)
            .Run(new ManeuverActionExecutor(SpecialAttackType.Feint), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.Feint)
            .Costs((c, ctx) => ManeuverCostFor(SpecialAttackType.Feint, c)));
        l.Add(Def(ActionIds.CoupDeGrace, "Coup de Grace", M, ActionCostType.FullRound, ProvokeRule.Yes, ActionTargetKind.Creature, ActionRoles.Attack,
                "PHB p.153, Table 8-2", ImplStatus.Delegating)
            .Run(new ManeuverActionExecutor(SpecialAttackType.CoupDeGrace), ActionUiAfter.AttackDelay, 1.0f).AsManeuver(SpecialAttackType.CoupDeGrace));
        l.Add(Def(ActionIds.AidAnother, "Aid Another", M, ActionCostType.Standard, ProvokeRule.Maybe, ActionTargetKind.Creature, ActionRoles.Support | ActionRoles.Defend,
                "PHB p.154 (a standard action, an attack roll against AC 10; the +2 lasts until the aider's next turn), Table 8-2 (Maybe) and note 2 (provokes only when the aided action would); D6",
                ImplStatus.Extracted)
            .Run(new AidAnotherActionExecutor(), ActionUiAfter.AttackDelay, 0.9f)
            .AsManeuver(SpecialAttackType.AidAnother));
        l.Add(Def(ActionIds.WakeAlly, "Wake an Ally", M, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Creature, ActionRoles.Support,
                "PHB p.280 (Sleep: shaking a sleeper awake is a standard action), p.154 (an application of aid another); D6", ImplStatus.Extracted)
            .Run(new WakeAllyActionExecutor(), ActionUiAfter.AttackDelay, 0.9f)
            .AsManeuver(SpecialAttackType.WakeSleepingAlly));
        l.Add(Def(ActionIds.TurnUndead, "Turn Undead", C, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.None, ActionRoles.Attack | ActionRoles.Defend,
                "PHB p.159-160 (a standard action, no AoO; 60 ft with line of effect, closest first, the pool of HD stops at the first misfit; destroyed at half the turning level), Table 8-2; D9",
                ImplStatus.Extracted)
            .Run(new TurnUndeadActionExecutor(), ActionUiAfter.AttackDelay, 0.9f)
            .AsManeuver(SpecialAttackType.TurnUndead)
            .For(c => c.Stats.CanEverTurnUndead));

        // ── Defense ──
        l.Add(Def(ActionIds.TotalDefense, "Total Defense", D, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Self, ActionRoles.Defend,
                "PHB p.142 (+4 dodge AC until the next turn, no AoOs), p.85 (Tumble 5 ranks: +6), Table 8-2; p.311 (a cornered panicked creature)",
                ImplStatus.New)
            .Run(new TotalDefenseActionExecutor(), ActionUiAfter.ShowChoices));

        // ── Magic and items ──
        l.Add(Def(ActionIds.CastSpell, "Cast a Spell", Mg, ActionCostType.Standard, ProvokeRule.Yes, ActionTargetKind.Creature,
                ActionRoles.Attack | ActionRoles.Heal | ActionRoles.Support | ActionRoles.Defend,
                "PHB p.140-141, p.174 (casting time), p.98 (Quicken Spell: free, no AoO), Table 8-2", ImplStatus.Delegating)
            .Run(new CastActionExecutor(), ActionUiAfter.None)
            .For(c => c.Spellcasting != null)
            .Costs(CastCostFor));
        l.Add(Def(ActionIds.MaintainConcentration, "Maintain Concentration", Mg, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Self, ActionRoles.Support,
                "PHB p.176 (concentration), Table 8-2", ImplStatus.Delegating)
            .Run(new MaintainConcentrationActionExecutor(), ActionUiAfter.ShowChoices)
            .For(c => c.Spellcasting != null));
        l.Add(Def(ActionIds.UseItem, "Use an Item", I, ActionCostType.Standard, ProvokeRule.Varies, ActionTargetKind.Item,
                ActionRoles.Item | ActionRoles.Heal | ActionRoles.Support,
                "PHB p.141-142, Table 8-2 (retrieve: move, Yes; potion, oil: standard, Yes); DMG p.213 (scroll: the casting time, Yes; wand: standard, No), p.229 (administer: full-round); D7",
                ImplStatus.Extracted)
            .Run(new UseItemActionExecutor(), ActionUiAfter.ShowChoices)
            .Costs(UseItemCostFor));
        l.Add(Def(ActionIds.RetrieveItem, "Retrieve a Stored Item", I, ActionCostType.Move, ProvokeRule.Yes, ActionTargetKind.Item,
                ActionRoles.Item, "PHB p.141-142, Table 8-2 (retrieve a stored item: move, Yes); D7 (the item stays in hand)", ImplStatus.New)
            .Run(new RetrieveItemActionExecutor(), ActionUiAfter.ShowChoices));

        // ── Movement and posture ──
        l.Add(Def(ActionIds.Move, "Move", Mv, ActionCostType.Move, ProvokeRule.ByMovement, ActionTargetKind.Cell, ActionRoles.Approach | ActionRoles.Retreat,
                "PHB p.143-144, p.137, p.138 (spent when the movement starts, D3), Table 8-2 and note 1", ImplStatus.Extracted)
            .Run(new MoveActionExecutor(false), ActionUiAfter.ShowChoices));
        l.Add(Def(ActionIds.Withdraw, "Withdraw", Mv, ActionCostType.FullRound, ProvokeRule.ByMovement, ActionTargetKind.Cell, ActionRoles.Retreat | ActionRoles.Escape,
                "PHB p.143-144 (the first square provokes from no seen foe), Table 8-2 (No) and note 5", ImplStatus.Extracted)
            .Run(new MoveActionExecutor(true), ActionUiAfter.ShowChoices)
            .Costs(SingleActionStandard));
        l.Add(Def(ActionIds.FiveFootStep, "5-Foot Step", Mv, ActionCostType.NotAnAction, ProvokeRule.No, ActionTargetKind.Cell, ActionRoles.Approach | ActionRoles.Retreat,
                "PHB p.144, Table 8-2 (no action)", ImplStatus.Delegating)
            .Run(new FiveFootStepActionExecutor(), ActionUiAfter.ShowChoices));
        l.Add(Def(ActionIds.Run, "Run", Mv, ActionCostType.FullRound, ProvokeRule.Yes, ActionTargetKind.Cell, ActionRoles.Retreat | ActionRoles.Approach,
                "PHB p.144, Table 8-2", ImplStatus.Missing, "CMB-202"));
        l.Add(Def(ActionIds.Crawl, "Crawl", P, ActionCostType.Move, ProvokeRule.Yes, ActionTargetKind.Cell, ActionRoles.Posture | ActionRoles.Retreat,
                "PHB p.142-143 (crawling: 5 ft as a move action, provokes from every attacker that threatens it at any point of the crawl, D5), p.311 (prone)",
                ImplStatus.Extracted)
            .Run(new CrawlActionExecutor(), ActionUiAfter.ShowChoices));
        l.Add(Def(ActionIds.StandUp, "Stand Up", P, ActionCostType.Move, ProvokeRule.Yes, ActionTargetKind.Self, ActionRoles.Posture,
                "PHB p.143-144, Table 8-2", ImplStatus.Delegating)
            .Run(new StandUpActionExecutor(), ActionUiAfter.ShowChoices));
        l.Add(Def(ActionIds.DropProne, "Drop Prone", P, ActionCostType.Free, ProvokeRule.No, ActionTargetKind.Self, ActionRoles.Posture | ActionRoles.Defend,
                "PHB p.141 Table 8-2 (drop to the floor: free, D5), p.311 (prone)", ImplStatus.Extracted)
            .Run(new DropProneActionExecutor(), ActionUiAfter.ShowChoices));

        // ── Class abilities ──
        l.Add(Def(ActionIds.Smite, "Smite", C, ActionCostType.Free, ProvokeRule.No, ActionTargetKind.Self, ActionRoles.Attack,
                "PHB p.44 (declared for one melee attack); MM p.31, p.107", ImplStatus.Delegating)
            .Run(new SmiteActionExecutor(), ActionUiAfter.ShowChoices)
            .For(c => SmiteRules.HasAnySmite(c.Stats)));
        l.Add(Def(ActionIds.LayOnHands, "Lay on Hands", C, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Creature, ActionRoles.Heal | ActionRoles.Attack,
                "PHB p.44 (a standard action), Table 8-2 (use supernatural ability: No)", ImplStatus.Delegating)
            .Run(new LayOnHandsActionExecutor(), ActionUiAfter.AttackDelay, 0.6f)
            .For(c => LayOnHandsRules.HasAbility(c.Stats)));
        l.Add(Def(ActionIds.WildShape, "Wild Shape", C, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Self, ActionRoles.Support | ActionRoles.Defend,
                "PHB p.37 (a standard action), Table 8-2 (use supernatural ability: No)", ImplStatus.Delegating)
            .Run(new WildShapeActionExecutor(), ActionUiAfter.AttackDelay, 0.6f)
            .For(c => WildShapeRules.HasAbility(c.Stats)));
        l.Add(Def(ActionIds.BardicMusic, "Bardic Music", C, ActionCostType.Standard, ProvokeRule.Varies, ActionTargetKind.Creature, ActionRoles.Support | ActionRoles.Attack,
                "PHB p.29-30 (starting is a standard action; Su and Sp effects), Table 8-2", ImplStatus.Delegating)
            .Run(new BardicMusicActionExecutor(), ActionUiAfter.AttackDelay, 0.6f)
            .For(c => BardicMusicRules.HasBardicMusic(c.Stats))
            .Costs(BardicCostFor));
        l.Add(Def(ActionIds.Rage, "Rage", C, ActionCostType.Free, ProvokeRule.No, ActionTargetKind.Self, ActionRoles.Attack | ActionRoles.Support,
                "PHB p.25-26 (entering a rage takes no time, only on the barbarian's own turn); D10", ImplStatus.Extracted)
            .Run(new RageActionExecutor(), ActionUiAfter.ShowChoices)
            .For(c => c.Stats.MaxRagesPerDay > 0));
        l.Add(Def(ActionIds.DomainPower, "Domain Power", C, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Creature, ActionRoles.Support | ActionRoles.Attack,
                "PHB p.32, p.185-189 (granted powers), Table 8-2 (use supernatural ability: No)", ImplStatus.Extracted)
            .Run(new DomainPowerActionExecutor(), ActionUiAfter.AttackDelay, 0.6f)
            .Costs((c, ctx) => ctx != null && !string.IsNullOrEmpty(ctx.Variant) ? DomainPowerActionExecutor.CostOf(ctx.Variant) : ActionCostType.Standard)
            .For(c => c.Stats.ChosenDomains != null && c.Stats.ChosenDomains.Count > 0));

        // ── Equipment ──
        l.Add(Def(ActionIds.Reload, "Reload", E, ActionCostType.Move, ProvokeRule.Yes, ActionTargetKind.Self, ActionRoles.Equip,
                "PHB p.115 (hand and light crossbows: a move action; heavy: full-round), p.99 (Rapid Reload, still provokes), Table 8-2 (Yes); D4",
                ImplStatus.Extracted)
            .Run(new ReloadActionExecutor(), ActionUiAfter.ShowChoices)
            .Costs(ReloadCostFor));
        l.Add(Def(ActionIds.DrawWeapon, "Draw a Weapon", E, ActionCostType.Move, ProvokeRule.No, ActionTargetKind.Item, ActionRoles.Equip,
                "PHB p.142 (a weapon, or a wand in easy reach), Table 8-2 (No) and note 4 (free with a regular move at BAB +1; Two-Weapon Fighting draws two light or one-handed weapons at once), p.98 (Quick Draw: free); D8",
                ImplStatus.New)
            .Run(new DrawWeaponActionExecutor(), ActionUiAfter.ShowChoices)
            .Costs(DrawCostFor));
        l.Add(Def(ActionIds.SheatheWeapon, "Sheathe a Weapon", E, ActionCostType.Move, ProvokeRule.Yes, ActionTargetKind.Item, ActionRoles.Equip,
                "PHB p.142, Table 8-2 (move, Yes); D8", ImplStatus.New)
            .Run(new SheatheWeaponActionExecutor(), ActionUiAfter.ShowChoices));
        l.Add(Def(ActionIds.ReadyShield, "Ready or Loose a Shield", E, ActionCostType.Move, ProvokeRule.No, ActionTargetKind.Item, ActionRoles.Equip | ActionRoles.Defend,
                "PHB p.142-143 (loosing unstraps and drops it), Table 8-2 (No) and note 4 (free with a regular move at BAB +1); D8", ImplStatus.New)
            .Run(new ReadyShieldActionExecutor(), ActionUiAfter.ShowChoices)
            .Costs((c, ctx) => EquipmentActionRules.ShieldCost(c)));
        l.Add(Def(ActionIds.DonArmor, "Don Armor", E, ActionCostType.FullRound, ProvokeRule.No, ActionTargetKind.Item, ActionRoles.Equip | ActionRoles.Defend,
                "PHB p.123 Table 7-7 (rounds to minutes; not a Table 8-2 row): no action spans rounds, so armor is locked in combat; D8", ImplStatus.OutOfScope, "ITM-095"));
        l.Add(Def(ActionIds.PickUpItem, "Pick Up an Item", E, ActionCostType.Move, ProvokeRule.Yes, ActionTargetKind.Item, ActionRoles.Equip | ActionRoles.Item,
                "PHB p.142, Table 8-2 (move, Yes); D8 (a weapon into a free hand)", ImplStatus.Extracted)
            .Run(new PickUpItemActionExecutor(), ActionUiAfter.ShowChoices));
        l.Add(Def(ActionIds.DropItem, "Drop an Item", E, ActionCostType.Free, ProvokeRule.No, ActionTargetKind.Item, ActionRoles.Equip,
                "PHB p.141 Table 8-2 (free action), p.143 (a readied shield is loosed), p.311 (a panicked creature drops what it holds); D8", ImplStatus.Extracted)
            .Run(new DropItemActionExecutor(), ActionUiAfter.ShowChoices));

        // ── Monster abilities (per creature) ──
        l.Add(Def(ActionIds.BreathWeapon, "Breath Weapon", Mo, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.Cell, ActionRoles.Attack,
                "MM p.69 (a dragon's breath is a standard action), p.306, p.315 (Su: standard action, no AoO)", ImplStatus.AiOnly, "CRE-039", "AI-040")
            .Run(new BreathWeaponActionExecutor())
            .For(c => c.HasBreathWeapon || c.HasSecondaryBreathWeapon));
        l.Add(Def(ActionIds.Spittle, "Spittle", Mo, ActionCostType.Free, ProvokeRule.Varies, ActionTargetKind.Creature, ActionRoles.Attack,
                "MM p.315 (special attacks; the creature's entry gives a free action)", ImplStatus.AiOnly, "CRE-039")
            .Run(new RangedSpecialActionExecutor(true))
            .For(c => c.HasRangedSpecialAttack && c.GetRangedSpecialAttackDefinition() != null && c.GetRangedSpecialAttackDefinition().IsFreeAction));
        l.Add(Def(ActionIds.Engulf, "Engulf", Mo, ActionCostType.Standard, ProvokeRule.Varies, ActionTargetKind.Cell, ActionRoles.Attack,
                "MM p.201 (gelatinous cube: AoO or a Reflex save), p.315", ImplStatus.AiOnly, "CRE-039")
            .Run(new EngulfActionExecutor())
            .For(c => c.HasEngulf));
        l.Add(Def(ActionIds.RangedSpecial, "Ranged Special Attack", Mo, ActionCostType.Standard, ProvokeRule.Varies, ActionTargetKind.Creature, ActionRoles.Attack,
                "MM p.315 (special attacks: a standard action unless noted)", ImplStatus.AiOnly, "CRE-039")
            .Run(new RangedSpecialActionExecutor(false))
            .For(c => c.HasRangedSpecialAttack && c.GetRangedSpecialAttackDefinition() != null && !c.GetRangedSpecialAttackDefinition().IsFreeAction));
        l.Add(Def(ActionIds.Gaze, "Gaze", Mo, ActionCostType.Free, ProvokeRule.No, ActionTargetKind.Creature, ActionRoles.Attack,
                "MM p.309 (Gaze: automatic each turn; an active gaze is a standard action)", ImplStatus.OutOfScope, "CRE-076"));

        // ── Turn order ──
        l.Add(Def(ActionIds.Ready, "Ready", T, ActionCostType.Standard, ProvokeRule.No, ActionTargetKind.None, ActionRoles.Attack | ActionRoles.Defend,
                "PHB p.160, Table 8-2", ImplStatus.Missing, "CMB-029"));
        l.Add(Def(ActionIds.Delay, "Delay", T, ActionCostType.NotAnAction, ProvokeRule.No, ActionTargetKind.None, ActionRoles.None,
                "PHB p.160, Table 8-2 (no action)", ImplStatus.Missing, "CMB-029"));
    }
}
