using System;

/// <summary>The menu group of a combat action (the action catalog, batch 13 step 3).</summary>
public enum ActionCategory
{
    Attack,
    Maneuver,
    Movement,
    Magic,
    ClassAbility,
    Defense,
    Posture,
    Equipment,
    Item,
    MonsterAbility,
    TurnOrder
}

/// <summary>
/// Whether the action itself provokes attacks of opportunity (PHB p.141 Table 8-2, the "Attack of Opportunity" column;
/// footnote 1: moving out of a threatened square provokes whatever the action).
/// </summary>
public enum ProvokeRule
{
    /// <summary>The action never provokes ("No").</summary>
    No,
    /// <summary>The action provokes ("Yes").</summary>
    Yes,
    /// <summary>The table's "Maybe" or "Usually": it depends on what is aided, sundered or used (footnotes 2 and 3).</summary>
    Maybe,
    /// <summary>It depends on how the action is made: the weapon (melee, ranged, unarmed), a feat (Improved Trip), the spell.</summary>
    Varies,
    /// <summary>The action is movement: each square left that a foe threatens provokes (PHB p.137), not the action itself.</summary>
    ByMovement
}

/// <summary>What an action is aimed at.</summary>
public enum ActionTargetKind
{
    None,
    Self,
    Creature,
    Cell,
    Item
}

/// <summary>What an action can do for the actor, for the AI's routing (batch 14 scores them). A combination.</summary>
[Flags]
public enum ActionRoles
{
    None = 0,
    Attack = 1 << 0,
    Approach = 1 << 1,
    Retreat = 1 << 2,
    Escape = 1 << 3,
    Defend = 1 << 4,
    Heal = 1 << 5,
    Support = 1 << 6,
    Equip = 1 << 7,
    Posture = 1 << 8,
    Item = 1 << 9
}

/// <summary>
/// How far an action is built (the catalog's status column, the source of docs/systems/AI_ACTION_COVERAGE.md since batch
/// 13 step 3).
/// </summary>
public enum ImplStatus
{
    /// <summary>A shared executor wraps a finished actor-agnostic core (cast, attack sequence, maneuver, charge, overrun).</summary>
    Delegating,
    /// <summary>A shared executor extracted from a PC-only or AI-only path (its own CanExecute is the legality).</summary>
    Extracted,
    /// <summary>A shared executor built new by the catalog.</summary>
    New,
    /// <summary>Only the PC UI can take the action; no shared executor yet.</summary>
    PcOnly,
    /// <summary>
    /// Only the AI takes the action. Its executor (from batch 13 step 6) wraps the AI's own core, so the AI's calls run
    /// through the catalog; no PC path reaches it until it becomes shared (breath, spittle, engulf: step 18).
    /// </summary>
    AiOnly,
    /// <summary>No one can take the action yet.</summary>
    Missing,
    /// <summary>Not built in this scope (a later batch).</summary>
    OutOfScope
}

/// <summary>What the PC runner does after the executor of an action has finished (GameManager.RunPlayerAction).</summary>
public enum ActionUiAfter
{
    /// <summary>Nothing: the core continues the UI itself (the cast core's UI decider) or the caller does.</summary>
    None,
    /// <summary>Show the action panel again.</summary>
    ShowChoices,
    /// <summary>The attack follow-up: wait, then end the turn when nothing is left or show the panel (AfterAttackDelay).</summary>
    AttackDelay,
    /// <summary>End the turn, after the entry's seconds when given.</summary>
    EndTurn
}

/// <summary>
/// The AI's choice of mode for an attack entry (Rapid Shot, two-weapon fighting): an interim rule, replaced by batch 22's
/// evaluator (batch 13 design choice C2, inherited Q2). Since step 6 the attack and full attack entries carry the AI
/// stopgap's formula unchanged (AIProfile.DefaultShouldUseRapidShot and DefaultShouldUseTwoWeaponFighting), and
/// AttackIntent.ForAI reads it here (AiModeValuation.ForAttack).
/// </summary>
public sealed class AiModeValuation
{
    /// <summary>"interim rule, replaced by batch 22's evaluator".</summary>
    public string Label = "interim rule, replaced by batch 22's evaluator";

    /// <summary>Whether to declare Rapid Shot against the target with the weapon (null: never).</summary>
    public Func<CharacterController, CharacterController, ItemData, bool> RapidShot;

    /// <summary>Whether to fight with two weapons against the target (null: never).</summary>
    public Func<CharacterController, CharacterController, bool> TwoWeapon;

    /// <summary>The attack entry's valuation (ActionCatalog), the one AttackIntent.ForAI reads.</summary>
    public static AiModeValuation ForAttack => ActionCatalog.Get(ActionIds.Attack)?.AiModeValuation;
}

/// <summary>
/// One combat action of the catalog (batch 13 step 3, docs/designs/action_catalog_batch13.md "End state"): its PHB Table
/// 8-2 cost and provocation, its rules page, what it is for, how far it is built and its executor. Shared by both sides:
/// the PC panel reaches the executor through GameManager.RunPlayerAction, the AI and the harness through RunAIAction.
/// Built only by <see cref="ActionCatalog"/>; never mutated after its Init.
/// </summary>
public sealed class ActionDefinition
{
    /// <summary>The <see cref="ActionIds"/> constant.</summary>
    public string Id;
    public string Name;
    public ActionCategory Category;

    /// <summary>The Table 8-2 action type (a maneuver that substitutes for a melee attack: Standard, the attack action's).</summary>
    public ActionCostType Cost;

    /// <summary>The cost for this actor and context when it differs from <see cref="Cost"/> (Improved Feint, casting time, a partial charge); null: <see cref="Cost"/>.</summary>
    public Func<CharacterController, ActionContext, ActionCostType> CostRule;

    /// <summary>Whether the action itself provokes (Table 8-2's column).</summary>
    public ProvokeRule Provokes;

    public ActionTargetKind Target;
    public ActionRoles Roles;

    /// <summary>The rules pages (PHB, DMG, MM), never empty.</summary>
    public string RulesRef;

    public ImplStatus Status;

    /// <summary>The issues that track the entry's gap or its next step (empty for a finished entry).</summary>
    public string[] IssueIds = Array.Empty<string>();

    /// <summary>The shared executor (null while the entry is PcOnly, Missing or OutOfScope; an AiOnly entry's executor serves the AI only, and RunPlayerAction refuses it).</summary>
    public IActionExecutor Executor;

    /// <summary>What GameManager.RunPlayerAction does after the executor.</summary>
    public ActionUiAfter UiAfter;

    /// <summary>The wait before <see cref="UiAfter"/> (AfterAttackDelay, DelayedEndActivePCTurn).</summary>
    public float UiAfterSeconds;

    /// <summary>The maneuver the entry is (every SpecialAttackType has one entry; ManeuverActionCost.For agrees with it).</summary>
    public SpecialAttackType? Maneuver;

    /// <summary>Takes the place of one melee attack of an attack or full attack (PHB p.141 Table 8-2 note 7; ManeuverCost.ReplacesMeleeAttack).</summary>
    public bool SubstitutesForMeleeAttack;

    /// <summary>The creatures that have the action at all (null: every creature); a per-creature ability (breath, spittle, engulf) tests its data.</summary>
    public Func<CharacterController, bool> Availability;

    /// <summary>The AI's interim mode chooser, on the attack and full attack entries only (C2, batch 13 step 6).</summary>
    public AiModeValuation AiModeValuation;

    /// <summary>The cost for <paramref name="actor"/> in <paramref name="ctx"/> (<see cref="CostRule"/>, else <see cref="Cost"/>).</summary>
    public ActionCostType CostFor(CharacterController actor, ActionContext ctx)
        => CostRule != null ? CostRule(actor, ctx) : Cost;

    /// <summary>Whether <paramref name="actor"/> has this action at all (not whether it can take it now: the executor's CanStart says that).</summary>
    public bool IsAvailableTo(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return false;
        if (Status == ImplStatus.OutOfScope)
            return false;
        return Availability == null || Availability(actor);
    }

    public override string ToString() => $"{Id} ({Cost}, provokes {Provokes}, {Status})";
}
