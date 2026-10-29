using System;
using System.Collections.Generic;

/// <summary>Which attack flow an <see cref="AttackSequenceRequest"/> comes from (its builder).</summary>
public enum AttackSequenceKind
{
    /// <summary>One click of the PC's Attack button sequence (origin B): one step at the clicked target.</summary>
    UiAttack,
    /// <summary>The PC's Full Attack button (origin C): every step, the player picking a new target when one is needed.</summary>
    UiFullAttack,
    /// <summary>An NPC's attack or full attack (origins F, G, H): every step, the AI picking each step's target.</summary>
    AI,
    /// <summary>An attack of opportunity (origin I): one step, outside the attacker's attack sequence.</summary>
    AoO,
    /// <summary>
    /// The touch that delivers a held charge after its casting round (batch 12 step 18, SPL-156): one melee touch attack in
    /// place of a regular attack, PC and NPC alike (<see cref="AttackSequenceRequest.ForHeldChargeTouch"/>).
    /// </summary>
    HeldChargeTouch
}

/// <summary>
/// The stages of one step of <c>GameManager.RunAttackSequence</c>, in the order the executor runs them (batch 12 step 17).
/// A melee step skips <see cref="RangedWeaponChecks"/> and <see cref="RangedAoO"/>; an AoO skips
/// <see cref="CanCommitAttack"/> and <see cref="TryCommitAttack"/> (it is not a step of the attack sequence).
/// </summary>
public enum AttackSequenceStage
{
    /// <summary>1. The decider names the step's target, or stops (IAttackDecider.ChooseNextStep).</summary>
    ChooseNextStep,
    /// <summary>2. One more step of this kind may be committed (CharacterController.CanCommitAttack).</summary>
    CanCommitAttack,
    /// <summary>3. The attacker may attack the target: sphere, hold bar, known ward failure, reach or range (AttackLegality).</summary>
    CanDirectlyAttack,
    /// <summary>4. A ranged or thrown step: a weapon to throw, a loaded crossbow, ammunition; Rapid Shot declared before the first attack (D9).</summary>
    RangedWeaponChecks,
    /// <summary>5. The attempt rolls the attacker's one save against the target's Sanctuary or Hide from Undead (WardSaveLedger.Commit, D6).</summary>
    WardCommit,
    /// <summary>6. The step is paid and recorded (CharacterController.TryCommitAttack).</summary>
    TryCommitAttack,
    /// <summary>7. A ranged step provokes from each threatening foe (PHB p.141 Table 8-2; CombatFlowService.ResolveRangedAttackAoOIfProvoked).</summary>
    RangedAoO,
    /// <summary>8. The attack roll (CharacterController.ResolveAttackSequenceStep; an AoO: Attack).</summary>
    ResolveStep,
    /// <summary>9. The post-attack hook (GameManager.OnAttackResolved).</summary>
    OnAttackResolved
}

/// <summary>The decider's answer to <c>IAttackDecider.ChooseNextStep</c>: the next step's target, or stop.</summary>
public sealed class AttackStepChoice
{
    public CharacterController Target;
    public bool Stop;
    public string Reason;

    public static AttackStepChoice At(CharacterController target) => new AttackStepChoice { Target = target };

    public static AttackStepChoice StopWith(string reason) => new AttackStepChoice { Stop = true, Reason = reason };
}

/// <summary>One resolved step of an attack sequence, for the caller's log lines and the deciders.</summary>
public sealed class AttackSequenceStep
{
    public CharacterController Target;
    public AttackIntent Intent;
    public AttackStepKind Kind;
    /// <summary>The step's index in the attack sequence (CharacterController.TryCommitAttack); -1 for an AoO.</summary>
    public int StepIndex = -1;
    /// <summary>The natural attack made (a natural-sequence index), or -1.</summary>
    public int NaturalAttackIndex = -1;
    /// <summary>The step is Haste's extra natural attack (PHB p.239; CMB-106).</summary>
    public bool HasteNaturalStep;
    public bool IsRanged;
    public RangeInfo RangeInfo;
    public bool IsFlanking;
    public int FlankBonus;
    public string PartnerName;
    public CombatResult Result;
    public string Label;
}

/// <summary>What a decider sees when it chooses the next step: the request, the current target and the steps so far.</summary>
public sealed class AttackSequenceState
{
    public AttackSequenceRequest Request;
    public AttackSequenceOutcome Outcome;

    /// <summary>The target of the last step (the request's target before the first); a decider may change it.</summary>
    public CharacterController CurrentTarget;

    /// <summary>Steps attempted so far: attacks made and steps lost to a ward.</summary>
    public int StepsTaken;

    /// <summary>The step resolved since the decider last asked (null after a maneuver, a lost step or the first ask).</summary>
    public AttackSequenceStep PreviousStep;

    /// <summary>The first ranged step threw its weapon: a later step needs a weapon to throw in hand (PHB p.113).</summary>
    public bool ThrownSequence;
    public bool RangedChecked;

    /// <summary>The step resolved since the last ask, once (the next ask sees null).</summary>
    public AttackSequenceStep TakePreviousStep()
    {
        AttackSequenceStep step = PreviousStep;
        PreviousStep = null;
        return step;
    }
}

/// <summary>
/// One attack or full attack for <c>GameManager.RunAttackSequence</c> (batch 12 step 17, docs/designs/pc_npc_parity_batch12.md;
/// the batch 11 cast-core pattern): who attacks, the first target, the intent, the decider that picks each step's target,
/// and the caller's own pieces. Built by <see cref="ForUiAttack"/>, <see cref="ForUiFullAttack"/>, <see cref="ForUiOffHand"/>,
/// <see cref="ForUiTwoWeaponFullAttack"/>, <see cref="ForAI"/>, <see cref="ForAoO"/> and <see cref="ForHeldChargeTouch"/> (the
/// charge's builder comes with the charge core). The executor commits costs only through the
/// attack sequence (CharacterController.TryCommitAttack); the caller does the UI continuation.
/// </summary>
public sealed class AttackSequenceRequest
{
    public AttackSequenceKind Kind;
    public AttackOrigin Origin;
    public CharacterController Attacker;
    public CharacterController InitialTarget;

    /// <summary>The intent of every step (each step works on a copy with its own weapon and hand).</summary>
    public AttackIntent Intent;

    /// <summary>Who picks each step's target, the Haste natural attack and the post-attack choices.</summary>
    public IAttackDecider Decider;

    /// <summary>The AI profile that runs the turn (AI requests; null: no adaptive retargeting, as a creature without one).</summary>
    public DND35.AI.AIProfile Profile;

    /// <summary>Steps at most (the Attack button's click and an AoO: one).</summary>
    public int MaxSteps = int.MaxValue;

    /// <summary>The AI's maneuver in place of a melee attack (trip, disarm, sunder, grapple; CMB-102), tried before each melee step; null for none.</summary>
    public Func<CharacterController, CharacterController, bool> TryStepManeuver;

    /// <summary>False when the caller drives the executor synchronously (the AoO, the static suites): no target reaction may wait on a prompt.</summary>
    public bool CallerCanWait = true;

    /// <summary>The combat-end context of the steps' post-attack hook.</summary>
    public string CombatEndContext;

    /// <summary>
    /// Added to a main-hand step's BAB: the two-weapon main-hand penalty (PHB p.160) on the PC Attack button's sequence, the
    /// Dual Wield full attack and the AI's two-weapon attack.
    /// </summary>
    public int MainHandBabAdjustment;

    /// <summary>
    /// The executor picks off-hand steps itself (batch 12 step 18, PHB p.160): each step is the hand whose next attack has
    /// the higher BAB, the main hand on a tie, while that hand has one left (GameManager.TwoWeaponStepKind). Set by the
    /// Dual Wield full attack and the AI's two-weapon intent; the PC Attack button's clicks are main-hand steps and the
    /// Off-Hand buttons are one off-hand step each (<see cref="ForUiOffHand"/>).
    /// </summary>
    public bool InterleaveOffHand;

    /// <summary>
    /// The held-charge touch's resolution (<see cref="ForHeldChargeTouch"/>): runs the delivery through the cast core once
    /// the step is paid (SpellCastRequest.DeliveryStepCommitted set) at the paid step's BAB (the third argument: the full
    /// BAB for the attack action or a full attack's first step, the iterative BAB of a later step, PHB p.143), and returns
    /// whether the delivery passed the cast core's Legality stage.
    /// </summary>
    public Func<CharacterController, CharacterController, int, bool> DeliverHeldCharge;

    /// <summary>An AoO's weapon (null: unarmed or natural) and its attack modifier (Mobility's -4).</summary>
    public ItemData AoOWeapon;
    public int AoOAttackModifier;

    /// <summary>An AoO's caller work after the roll and before the post-attack hook (its flag, the scenario hook, its log).</summary>
    public Action<CombatResult> OnAoOResolved;

    /// <summary>The caller's log lines for a step (null: the attack breakdown line and a stats refresh).</summary>
    public Action<AttackSequenceStep> ReportStep;

    /// <summary>Test seam: called as each stage starts (AttackSequenceStepTests); null in play.</summary>
    public Action<AttackSequenceStage> StageObserver;

    /// <summary>
    /// One click of the PC's Attack button sequence (origin B): the clicked target, the PC UI's intent (AttackIntent.ForUi)
    /// and the attacker's decider; the two-weapon main-hand penalty when the PC fights with two weapons.
    /// </summary>
    public static AttackSequenceRequest ForUiAttack(GameManager gm, CharacterController attacker, CharacterController target)
    {
        AttackIntent intent = AttackIntent.ForUi(gm, attacker);
        GameManager.AttackType type = intent.UiAttackType;
        bool twoWeaponMainHand = gm != null && gm.Combat_IsDualWielding()
            && (type == GameManager.AttackType.Melee || type == GameManager.AttackType.Thrown);
        return new AttackSequenceRequest
        {
            Kind = AttackSequenceKind.UiAttack,
            Origin = AttackOrigin.PcSequence,
            Attacker = attacker,
            InitialTarget = target,
            Intent = intent,
            Decider = AttackDeciders.For(attacker),
            MaxSteps = 1,
            CombatEndContext = "PerformIterativeSequenceAttack",
            MainHandBabAdjustment = twoWeaponMainHand ? gm.Combat_GetMainHandPenalty() : 0,
        };
    }

    /// <summary>
    /// One click of a PC Off-Hand button (origin E, batch 12 step 18): one off-hand step (PHB p.160) at the clicked target
    /// with <paramref name="offHandWeapon"/>, melee or thrown (<paramref name="thrown"/>, PHB p.113), at the off-hand ladder's
    /// BAB and penalty (CharacterController.ResolveOffHandAttackStep), paid through the attack sequence like any attack.
    /// </summary>
    public static AttackSequenceRequest ForUiOffHand(GameManager gm, CharacterController attacker, CharacterController target,
        ItemData offHandWeapon, bool thrown)
    {
        AttackIntent intent = AttackIntent.ForUi(gm, attacker);
        intent.Mode = thrown ? AttackMode.Thrown : AttackMode.Melee;
        intent.UiType = thrown ? GameManager.AttackType.Thrown : GameManager.AttackType.Melee;
        intent.Hand = AttackStepKind.OffHand;
        intent.Weapon = offHandWeapon;
        intent.TwoWeapon = true;
        intent.RapidShot = false;
        return new AttackSequenceRequest
        {
            Kind = AttackSequenceKind.UiAttack,
            Origin = AttackOrigin.PcOffHand,
            Attacker = attacker,
            InitialTarget = target,
            Intent = intent,
            Decider = AttackDeciders.For(attacker),
            MaxSteps = 1,
            CombatEndContext = "ResolveOffHandAttack",
        };
    }

    /// <summary>
    /// The PC's Dual Wield full attack (origin D, batch 12 step 18): the Full Attack button's flow and decider with both
    /// weapons, every main-hand step at the main-hand penalty and every off-hand step at the off-hand one (PHB p.160;
    /// CharacterController.GetDualWieldPenalties), the executor choosing the hand of each step (<see cref="InterleaveOffHand"/>).
    /// </summary>
    public static AttackSequenceRequest ForUiTwoWeaponFullAttack(GameManager gm, CharacterController attacker, CharacterController target)
    {
        AttackSequenceRequest request = ForUiFullAttack(gm, attacker, target);
        request.Origin = AttackOrigin.PcDualWield;
        request.Intent.Mode = AttackMode.Melee;
        request.Intent.UiType = GameManager.AttackType.Melee;
        request.Intent.TwoWeapon = true;
        request.Intent.RapidShot = false;
        request.InterleaveOffHand = true;
        request.MainHandBabAdjustment = attacker != null ? attacker.GetDualWieldPenalties().mainPenalty : 0;
        request.CombatEndContext = "PerformDualWieldAttack";
        return request;
    }

    /// <summary>
    /// The delivery of a held touch charge after its casting round (batch 12 step 18, SPL-156; PHB p.141: a touch attack made
    /// instead of a regular attack; delegated RAW reading 2026-10-14): one melee touch step of the attack sequence, the
    /// attack action or one step of a full attack, for the PC Discharge button and the AI's delivery alike.
    /// <paramref name="deliver"/> runs the delivery's cast once the step is paid, at that step's BAB. The step is checked by the shared gate
    /// (the Resilient Sphere, the hold bar, the touch's reach: AttackLegality, DirectAttackKind.Touch); the ward's save and
    /// the touch's roll and effect are the cast core's (a spell's own ward commit, SpellCaster), and the post-attack hook is
    /// not run (the delivery's PostCast and CombatEnd stages are its after-effects).
    /// </summary>
    public static AttackSequenceRequest ForHeldChargeTouch(CharacterController caster, CharacterController target, IAttackDecider decider,
        Func<CharacterController, CharacterController, int, bool> deliver)
    {
        AttackIntent intent = AttackIntent.ForMode(AttackMode.Melee);
        intent.HeldChargeTouch = true;
        return new AttackSequenceRequest
        {
            Kind = AttackSequenceKind.HeldChargeTouch,
            Origin = AttackOrigin.PcSequence,
            Attacker = caster,
            InitialTarget = target,
            Intent = intent,
            Decider = decider ?? AttackDeciders.For(caster),
            MaxSteps = 1,
            CallerCanWait = false,
            DeliverHeldCharge = deliver,
            CombatEndContext = "DeliverHeldCharge",
            ReportStep = _ => { },
        };
    }

    /// <summary>The PC's Full Attack button (origin C): the PC UI's intent, read once, for every step; the attacker's decider.</summary>
    public static AttackSequenceRequest ForUiFullAttack(GameManager gm, CharacterController attacker, CharacterController target)
    {
        return new AttackSequenceRequest
        {
            Kind = AttackSequenceKind.UiFullAttack,
            Origin = AttackOrigin.PcFullAttackButton,
            Attacker = attacker,
            InitialTarget = target,
            Intent = AttackIntent.ForUi(gm, attacker),
            Decider = AttackDeciders.For(attacker),
            CombatEndContext = "ExecuteFullAttackSequence",
        };
    }

    /// <summary>
    /// An NPC's attack or full attack (origins F, G, H): the NPC's own intent (AttackIntent.ForAI: melee, ranged or a throw),
    /// the AI decider (the NPC turn path is AI-run for every creature on it), <paramref name="profile"/> for its retargeting
    /// and 5-foot steps, and its maneuver evaluation for the melee steps.
    /// </summary>
    public static AttackSequenceRequest ForAI(GameManager gm, CharacterController npc, CharacterController target,
        Func<CharacterController, CharacterController, bool> tryStepManeuver, DND35.AI.AIProfile profile)
    {
        AttackIntent intent = AttackIntent.ForAI(gm, npc, target);
        bool twoWeapon = intent.Mode == AttackMode.Melee && intent.TwoWeapon;
        return new AttackSequenceRequest
        {
            Kind = AttackSequenceKind.AI,
            Origin = intent.Mode == AttackMode.Melee ? AttackOrigin.NpcMelee : AttackOrigin.NpcRangedFull,
            Attacker = npc,
            InitialTarget = target,
            Intent = intent,
            Decider = AttackDeciders.For(npc, ActorSource.AI),
            Profile = profile,
            TryStepManeuver = intent.Mode == AttackMode.Melee ? tryStepManeuver : null,
            CombatEndContext = "NPCPerformAttack.Sequence",
            // Two-weapon fighting (AI-055 stopgap, AttackIntent.ForAI): both hands' steps, the main hand at its penalty.
            InterleaveOffHand = twoWeapon,
            MainHandBabAdjustment = twoWeapon && npc != null ? npc.GetDualWieldPenalties().mainPenalty : 0,
        };
    }

    /// <summary>
    /// An attack of opportunity (origin I): one melee attack at full BAB with <paramref name="weapon"/> (PHB p.137) and
    /// <paramref name="attackModifier"/>, outside the attacker's attack sequence; the AI decider, since an AoO happens inside
    /// another creature's action and cannot wait on a prompt (CMB-190). ThreatSystem.ExecuteAoO spends the AoO first.
    /// </summary>
    public static AttackSequenceRequest ForAoO(CharacterController threatener, CharacterController target, ItemData weapon,
        int attackModifier, Action<CombatResult> onResolved)
    {
        AttackIntent intent = AttackIntent.ForMode(AttackMode.Melee, weapon);
        intent.IsAoO = true;
        return new AttackSequenceRequest
        {
            Kind = AttackSequenceKind.AoO,
            Origin = AttackOrigin.AoO,
            Attacker = threatener,
            InitialTarget = target,
            Intent = intent,
            Decider = AttackDeciders.For(threatener, ActorSource.AI),
            MaxSteps = 1,
            CallerCanWait = false,
            AoOWeapon = weapon,
            AoOAttackModifier = attackModifier,
            OnAoOResolved = onResolved,
            CombatEndContext = "ExecuteAoO",
            // The AoO's callers log it themselves (the provoking flow's "AoO vs ..." line), so the step adds no line.
            ReportStep = _ => { },
        };
    }
}

/// <summary>What <c>GameManager.RunAttackSequence</c> did, for the caller's continuation.</summary>
public sealed class AttackSequenceOutcome
{
    /// <summary>Every attack made, with its label (Defender: the first target).</summary>
    public FullAttackResult Aggregate;

    public int AttacksMade;
    public int StepsLostToWard;
    public int ManeuversUsed;
    public int TargetSwitches;

    /// <summary>Why the sequence stopped before its steps ran out (null when they ran out).</summary>
    public string StopReason;

    public bool CombatEnded;
    public bool AttackerOutOfFight;

    /// <summary>The attacker started grappling in the sequence (a maneuver, an Improved Grab): its remaining attacks are grapple actions (PHB p.156).</summary>
    public bool StartedGrapple;

    /// <summary>Rapid Shot was declared for this round (PHB p.99, D9).</summary>
    public bool RapidShotDeclared;

    public CombatResult LastResult;
    public AttackSequenceStep LastStep;

    /// <summary>The sequence stopped to wait (a prompt, a counter-trip answer) and finishes as a coroutine.</summary>
    public bool Suspended;
    public bool Finished;

    /// <summary>The stages run, in order (AttackSequenceStepTests reads them).</summary>
    public readonly List<AttackSequenceStage> Stages = new List<AttackSequenceStage>();
}
