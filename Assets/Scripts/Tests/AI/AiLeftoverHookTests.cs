#if UNITY_EDITOR
using System.Collections.Generic;
using DND35.AI;
using DND35.AI.Knowledge;
using DND35.AI.Personality;
using DND35.AI.Profiles;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 11 (AI-077; decision D1, design choice C8; docs/designs/personalities_batch14.md "Leftover hooks"): the
/// move left after a standard-action ability. The default table of AIProfile.DefaultChooseAfterStandardAction (no move:
/// stay; a Melee or Mixed Role approaches a focus out of reach; a Ranged or casting Role keeps its distance from a close
/// or threatening foe when it can still fight from range; a prone
/// creature that wants to move stands up instead, but a threatened one that wants distance stays down; otherwise it stays), the shipped profiles' hook equal to it, and the
/// traits' tilt (AiBlend.AfterStandardAction): no personality keeps the Role's choice; Cowardly backs away where the Role
/// would close in (Retreat .8 against Approach -.5) and Berserker closes in where the Role would back away (Approach .6
/// against Retreat -.8), with the starter scores pinned. Batch 14 step 12 (AI-047, C8): the healer's approach
/// (AIProfile.DefaultChooseHealerApproach, pinned in AIProfileFrameworkTests) and its tilt (AiBlend.HealerApproach): no
/// personality and Cowardly keep the Role's choice, a Healer trait keeps a move-and-cure and a partial approach. Batch 14 step
/// 13 (AI-063, D3-D5, C8): the melee turn's option (TurnOptionValue's aid offense, aid defense and total defense formulas
/// and their starter constants; AIProfile.DefaultChooseMeleeTurnOption: the attack unless an open alternative beats it by
/// max(0.5, 0.15 S), total defense only when a foe can reach; AiBlend.MeleeTurn: Cowardly takes total defense, Berserker
/// keeps the attack, Healer aids) and the prone start (AIProfile.DefaultChooseProneStart). Pure:
/// contexts are filled by hand. Either mode.
/// </summary>
public static class AiLeftoverHookTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AI LEFTOVER HOOK TESTS (batch 14 step 11) ======");

        TestDefaultTable();
        TestShippedProfilesUseTheDefault();
        TestNoPersonalityKeepsRole();
        TestCowardlyAndBerserkerReorder();
        TestHealerApproachTilt();
        TestMeleeTurnOptionFormulas();
        TestMeleeTurnOptionDefault();
        TestMeleeTurnOptionTilt();
        TestProneStartDefault();

        Debug.Log($"====== AI Leftover Hook Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (detail != null ? " - " + detail : ""));
        }
    }

    private static AfterStandardActionContext Ctx(CombatStyle style, bool casting = false, bool canMove = true, bool prone = false,
        bool outOfReach = false, bool near = false, bool threatened = false, bool ranged = false)
        => new AfterStandardActionContext
        {
            Style = style,
            CastingRole = casting,
            CanMove = canMove,
            Prone = prone,
            FocusOutOfReach = outOfReach,
            FoeWithinKeepAway = near,
            Threatened = threatened,
            CanFightFromRange = ranged
        };

    private static void Expect(string name, AfterStandardActionContext ctx, AfterStandardActionChoice expected)
    {
        AfterStandardActionChoice got = AIProfile.DefaultChooseAfterStandardAction(ctx);
        Assert(got == expected, "default: " + name + " -> " + expected, "got " + got);
    }

    private static void TestDefaultTable()
    {
        Expect("melee, no move left, focus out of reach", Ctx(CombatStyle.Melee, canMove: false, outOfReach: true), AfterStandardActionChoice.Stay);
        Expect("melee, focus out of reach", Ctx(CombatStyle.Melee, outOfReach: true), AfterStandardActionChoice.Approach);
        Expect("melee, focus in reach, threatened", Ctx(CombatStyle.Melee, threatened: true, near: true), AfterStandardActionChoice.Stay);
        Expect("Mixed non-casting (dragon), focus out of reach", Ctx(CombatStyle.Mixed, outOfReach: true), AfterStandardActionChoice.Approach);
        Expect("melee-style casting Role (Spellcaster), nobody close, focus out of reach", Ctx(CombatStyle.Melee, casting: true, outOfReach: true), AfterStandardActionChoice.Stay);
        Expect("melee-style casting Role (Spellcaster), a foe close, can fight from range", Ctx(CombatStyle.Melee, casting: true, near: true, ranged: true), AfterStandardActionChoice.KeepDistance);
        Expect("Ranged, foe within keep-away, can fight from range", Ctx(CombatStyle.Ranged, near: true, ranged: true), AfterStandardActionChoice.KeepDistance);
        Expect("Ranged, threatened, no ranged means (AI-039)", Ctx(CombatStyle.Ranged, threatened: true, near: true), AfterStandardActionChoice.Stay);
        Expect("Ranged, nobody close, focus out of reach", Ctx(CombatStyle.Ranged, outOfReach: true, ranged: true), AfterStandardActionChoice.Stay);
        Expect("Mixed casting Role (abjurer), threatened, can fight from range", Ctx(CombatStyle.Mixed, casting: true, threatened: true, ranged: true), AfterStandardActionChoice.KeepDistance);
        Expect("prone melee, focus out of reach", Ctx(CombatStyle.Melee, prone: true, outOfReach: true), AfterStandardActionChoice.StandUp);
        Expect("prone Ranged, foe close, can fight from range", Ctx(CombatStyle.Ranged, prone: true, near: true, ranged: true), AfterStandardActionChoice.StandUp);
        Expect("prone melee, focus in reach", Ctx(CombatStyle.Melee, prone: true), AfterStandardActionChoice.Stay);
        Expect("prone caster, threatened, can fight from range: stays down (standing provokes, gains no distance)",
            Ctx(CombatStyle.Ranged, casting: true, prone: true, near: true, threatened: true, ranged: true), AfterStandardActionChoice.Stay);
        // Batch 14 step 13 review: a creature that chose at its turn start to stay prone is not stood up by a KeepDistance wish.
        AfterStandardActionContext stayed = Ctx(CombatStyle.Ranged, casting: true, prone: true, near: true, ranged: true);
        stayed.StayingProne = true;
        Expect("prone caster that chose to stay prone, foe close, can fight from range: stays down", stayed, AfterStandardActionChoice.Stay);
    }

    private static void TestShippedProfilesUseTheDefault()
    {
        var profiles = new List<AIProfile>();
        try
        {
            profiles.Add(AIProfile.CreateRuntime<HumanoidAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<HealerAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<EvokerAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<DragonAIProfile>());
            var contexts = new[]
            {
                Ctx(CombatStyle.Melee, outOfReach: true),
                Ctx(CombatStyle.Ranged, near: true, ranged: true),
                Ctx(CombatStyle.Melee, prone: true, outOfReach: true),
                Ctx(CombatStyle.Ranged, canMove: false, near: true, ranged: true)
            };
            foreach (AIProfile p in profiles)
            {
                bool same = true;
                foreach (AfterStandardActionContext c in contexts)
                    same &= p.ChooseAfterStandardAction(null, c) == AIProfile.DefaultChooseAfterStandardAction(c);
                Assert(same, p.GetType().Name + ".ChooseAfterStandardAction equals the default");
            }
        }
        finally
        {
            foreach (AIProfile p in profiles)
                if (p != null)
                    Object.DestroyImmediate(p);
        }
    }

    private static void TestHealerApproachTilt()
    {
        var touch = new HealerApproachContext { HasPatient = true, Distance = 3, AoOPrice = 1000f, TouchSquare = new Vector2Int(2, 0) };
        var partial = new HealerApproachContext { HasPatient = true, Distance = 12, AoOPrice = 1000f, PartialSquare = new Vector2Int(4, 0) };
        Assert(AIProfile.DefaultChooseHealerApproach(touch) == HealerApproachChoice.MoveAndCast
               && AIProfile.DefaultChooseHealerApproach(partial) == HealerApproachChoice.PartialApproach,
            "healer approach default: a touch square is MoveAndCast, a closer square PartialApproach");
        Assert(AiBlend.HealerApproach((PersonalityProfile)null, HealerApproachChoice.MoveAndCast, touch) == HealerApproachChoice.MoveAndCast
               && AiBlend.HealerApproach((CharacterController)null, HealerApproachChoice.PartialApproach, partial) == HealerApproachChoice.PartialApproach,
            "healer approach: no personality keeps the Role's choice");
        Assert(AiBlend.RolesOf(HealerApproachChoice.MoveAndCast) == ActionRoles.Heal
               && AiBlend.RolesOf(HealerApproachChoice.PartialApproach) == (ActionRoles.Heal | ActionRoles.Approach)
               && AiBlend.RolesOf(HealerApproachChoice.Kiter) == ActionRoles.None,
            "healer approach roles: MoveAndCast Heal, PartialApproach Heal and Approach, Kiter none");

        PersonalityProfile coward = P("Cowardly:1");
        PersonalityProfile healer = P("Healer:1");
        PersonalityProfile adept = P("Cowardly:0.6,Healer:0.4");
        Assert(coward != null && healer != null && adept != null, "the healer test personalities build");
        if (coward == null || healer == null || adept == null)
            return;
        // Cowardly names no Heal desire: MoveAndCast 1 against Kiter 0; PartialApproach 1 + 2 * -.5 = 0 ties Kiter 0.
        Assert(AiBlend.HealerApproach(coward, HealerApproachChoice.MoveAndCast, touch) == HealerApproachChoice.MoveAndCast,
            "Cowardly keeps the Role's move and cure");
        Assert(AiBlend.HealerApproach(coward, HealerApproachChoice.PartialApproach, partial) == HealerApproachChoice.PartialApproach,
            "Cowardly's partial approach ties the kiter, so the Role's choice is kept");
        Assert(AiBlend.HealerApproach(healer, HealerApproachChoice.MoveAndCast, touch) == HealerApproachChoice.MoveAndCast
               && AiBlend.HealerApproach(healer, HealerApproachChoice.PartialApproach, partial) == HealerApproachChoice.PartialApproach,
            "Healer keeps both approaches (Heal .8)");
        // The goblin adept's mix with a Role that kites (no approach open beyond it): the tilt opens no closed choice.
        var none = new HealerApproachContext { HasPatient = true, Distance = 3, AoOPrice = 1000f };
        Assert(AiBlend.HealerApproach(adept, HealerApproachChoice.Kiter, none) == HealerApproachChoice.Kiter,
            "no square open: the adept's mix keeps the kiter");
    }

    private static PersonalityProfile P(string spec)
        => PersonalityFactory.Create(spec, CognitionTier.Average, Alignment.None, "leftover_test", _ => { });

    private static void TestNoPersonalityKeepsRole()
    {
        var c = Ctx(CombatStyle.Melee, outOfReach: true, near: true);
        foreach (AfterStandardActionChoice role in new[] { AfterStandardActionChoice.Stay, AfterStandardActionChoice.Approach,
                     AfterStandardActionChoice.KeepDistance, AfterStandardActionChoice.StandUp })
            Assert(AiBlend.AfterStandardAction((PersonalityProfile)null, role, c) == role, "no personality keeps the Role's " + role);
        Assert(AiBlend.AfterStandardAction((CharacterController)null, AfterStandardActionChoice.Approach, c) == AfterStandardActionChoice.Approach,
            "no creature keeps the Role's choice");
        Assert(AiBlend.RolesOf(AfterStandardActionChoice.Approach) == ActionRoles.Approach
               && AiBlend.RolesOf(AfterStandardActionChoice.KeepDistance) == ActionRoles.Retreat
               && AiBlend.RolesOf(AfterStandardActionChoice.Stay) == ActionRoles.None,
            "roles: Approach is Approach, KeepDistance is Retreat, Stay is none");
    }

    private static void TestCowardlyAndBerserkerReorder()
    {
        PersonalityProfile coward = P("Cowardly:1");
        PersonalityProfile berserker = P("Berserker:1");
        Assert(coward != null && berserker != null, "the two test personalities build");
        if (coward == null || berserker == null)
            return;
        Assert(Mathf.Approximately(coward.RoleShift(ActionRoles.Retreat), 0.8f) && Mathf.Approximately(coward.RoleShift(ActionRoles.Approach), -0.5f)
               && Mathf.Approximately(berserker.RoleShift(ActionRoles.Approach), 0.6f) && Mathf.Approximately(berserker.RoleShift(ActionRoles.Retreat), -0.8f),
            "starter desires: Cowardly Retreat .8, Approach -.5; Berserker Approach .6, Retreat -.8");
        Assert(Mathf.Approximately(AiBlend.AfterStandardDesireGain, 2f), "the desire gain is 2 (starter constant)");

        // A melee Role closing on its focus, a foe close: Approach scores 1 - 1 = 0, KeepDistance 1.6, Stay 0.
        var meleeNear = Ctx(CombatStyle.Melee, outOfReach: true, near: true);
        AfterStandardActionChoice role = AIProfile.DefaultChooseAfterStandardAction(meleeNear);
        Assert(role == AfterStandardActionChoice.Approach, "melee Role, focus out of reach and a foe close: the Role approaches");
        Assert(AiBlend.AfterStandardAction(coward, role, meleeNear) == AfterStandardActionChoice.KeepDistance, "Cowardly backs away instead");
        Assert(AiBlend.AfterStandardAction(berserker, role, meleeNear) == AfterStandardActionChoice.Approach, "Berserker approaches as the Role does");

        // Nobody close: Cowardly's Approach (0) ties Stay (0), so the Role's choice is kept.
        var meleeFar = Ctx(CombatStyle.Melee, outOfReach: true);
        Assert(AiBlend.AfterStandardAction(coward, AfterStandardActionChoice.Approach, meleeFar) == AfterStandardActionChoice.Approach,
            "Cowardly with nobody close: a tie keeps the Role's approach");

        // A casting Role keeping its distance, its focus out of reach: KeepDistance 1 - 1.6 = -0.6, Approach 1.2, Stay 0.
        var casterNear = Ctx(CombatStyle.Ranged, casting: true, outOfReach: true, near: true, ranged: true);
        role = AIProfile.DefaultChooseAfterStandardAction(casterNear);
        Assert(role == AfterStandardActionChoice.KeepDistance, "casting Role, a foe close: the Role keeps its distance");
        Assert(AiBlend.AfterStandardAction(berserker, role, casterNear) == AfterStandardActionChoice.Approach, "Berserker closes in instead");
        Assert(AiBlend.AfterStandardAction(coward, role, casterNear) == AfterStandardActionChoice.KeepDistance, "Cowardly keeps its distance as the Role does");

        // The same caster with its focus in reach: no approach is open, so Berserker stays rather than backing away.
        var casterInReach = Ctx(CombatStyle.Ranged, casting: true, near: true, threatened: true, ranged: true);
        Assert(AiBlend.AfterStandardAction(berserker, AfterStandardActionChoice.KeepDistance, casterInReach) == AfterStandardActionChoice.Stay,
            "Berserker with the focus in reach stays instead of backing away");

        // A Role that stays (a Ranged Role with no ranged means, threatened): KeepDistance is closed (AI-039), so Cowardly
        // stays too; with the foe close but not threatening, Cowardly backs away anyway (1.6 > 1).
        var rangedNoMeans = Ctx(CombatStyle.Ranged, threatened: true, near: true);
        Assert(AiBlend.AfterStandardAction(coward, AIProfile.DefaultChooseAfterStandardAction(rangedNoMeans), rangedNoMeans) == AfterStandardActionChoice.Stay,
            "Cowardly, threatened with no ranged means, stays as the Role does (AI-039)");
        var rangedNoMeansNear = Ctx(CombatStyle.Ranged, near: true);
        Assert(AiBlend.AfterStandardAction(coward, AIProfile.DefaultChooseAfterStandardAction(rangedNoMeansNear), rangedNoMeansNear) == AfterStandardActionChoice.KeepDistance,
            "Cowardly backs away from a close foe that does not threaten, where the Role would stay");

        // Prone: the blended move stands the creature up first.
        var proneNear = Ctx(CombatStyle.Melee, prone: true, outOfReach: true, near: true);
        role = AIProfile.DefaultChooseAfterStandardAction(proneNear);
        Assert(role == AfterStandardActionChoice.StandUp && AiBlend.AfterStandardAction(coward, role, proneNear) == AfterStandardActionChoice.StandUp,
            "prone: the Role and Cowardly both stand up first");
        var proneCaster = Ctx(CombatStyle.Ranged, casting: true, prone: true, near: true, threatened: true, ranged: true);
        Assert(AiBlend.AfterStandardAction(berserker, AIProfile.DefaultChooseAfterStandardAction(proneCaster), proneCaster) == AfterStandardActionChoice.Stay,
            "prone caster, focus in reach: Berserker stays prone rather than standing to back away");

        // No move left: every personality stays.
        var noMove = Ctx(CombatStyle.Melee, canMove: false, outOfReach: true, near: true);
        Assert(AiBlend.AfterStandardAction(coward, AfterStandardActionChoice.Stay, noMove) == AfterStandardActionChoice.Stay
               && AiBlend.AfterStandardAction(berserker, AfterStandardActionChoice.Stay, noMove) == AfterStandardActionChoice.Stay,
            "no move left: Cowardly and Berserker stay");
    }
    // ── Batch 14 step 13: aid another, total defense and posture (AI-063, D3-D5, C8) ──

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;

    private static void TestMeleeTurnOptionFormulas()
    {
        Assert(Near(DND35.AI.Valuation.TurnOptionValue.DropWeight, 2f) && DND35.AI.Valuation.TurnOptionValue.AidBonus == 2
               && DND35.AI.Valuation.TurnOptionValue.TotalDefenseBonus == 4 && DND35.AI.Valuation.TurnOptionValue.AidRollAC == 10,
            "starter constants: drop weight 2; aid +2 against AC 10 (PHB p.154); total defense +4 (PHB p.142)");
        // Aid roll +2 against AC 10: 0.65; the ally +4 against AC 18: 0.35, at +6 0.45; 9 damage: 0.65 * 0.1 * 9 = 0.585.
        float offense = DND35.AI.Valuation.TurnOptionValue.AidOffense(2, 4, 18, 9f);
        Assert(Near(offense, 0.585f), "aid offense: P(aid) x (H(ally+2) - H(ally)) x D_ally", "got " + offense);
        // A clamped hit chance gains nothing: the ally needs a 20 at AC 40 with or without +2.
        Assert(Near(DND35.AI.Valuation.TurnOptionValue.AidOffense(2, 4, 40, 9f), 0f), "aid offense: a natural-20-only ally gains nothing");
        // The opponent +8 against the ally's AC 15: 0.7, at AC 17 0.6; 11 damage, share 1, drop weight 2: 0.65 * 0.1 * 11 * 2 = 1.43.
        float defense = DND35.AI.Valuation.TurnOptionValue.AidDefense(2, 8, 15, 11f, 1f, 2f);
        Assert(Near(defense, 1.43f), "aid defense: P(aid) x lost hits at +2 AC x D x share x drop weight", "got " + defense);
        Assert(Near(DND35.AI.Valuation.TurnOptionValue.AidDefense(2, 8, 15, 11f, 0.5f, 1f), 0.3575f), "aid defense: half share, no drop weight");
        Assert(Near(DND35.AI.Valuation.TurnOptionValue.TotalDefensePart(4.9f, 3.4f, 0.5f, 1f), 0.75f), "total defense: (damage at AC - at AC+4) x share x drop weight");
        // The exchange rate (Q9 default): 0.75 kept off of 2.45 incoming by a creature whose attack is worth 0.18: 0.75 x 0.18 / 2.45.
        float total = DND35.AI.Valuation.TurnOptionValue.TotalDefense(0.75f, 2.45f, 0.18f);
        Assert(Near(total, 0.0551f), "total defense: the damage kept off x the creature's attack value per hit point it takes", "got " + total);
        Assert(Near(DND35.AI.Valuation.TurnOptionValue.TotalDefense(1.5f, 1f, 2f), 1.5f) && Near(DND35.AI.Valuation.TurnOptionValue.TotalDefense(1.5f, 0f, 2f), 0f),
            "total defense: the exchange rate is at most 1; nothing incoming is worth 0");
        Assert(Near(DND35.AI.Valuation.TurnOptionValue.DropWeightFor(9f, 9), 2f) && Near(DND35.AI.Valuation.TurnOptionValue.DropWeightFor(9f, 10), 1f),
            "drop weight: 2 when one average hit drops the creature, else 1");
        var ctx = new MeleeTurnOptionContext { AttackValue = 2f };
        var big = new MeleeTurnOptionContext { AttackValue = 10f };
        Assert(Near(ctx.Margin, 0.5f) && Near(big.Margin, 1.5f), "margin: max(0.5, 0.15 S)");

        // The Q9 default's bound (batch 14 step 13 review): with every drop weight 1 the damage kept off is at most the incoming
        // damage, so total defense is worth at most S and never reaches the threshold S + max(0.5, 0.15 S); with the drop
        // weight it can pass only when +4 AC removes at least 57.5% of the incoming damage (2 x 0.575 = 1.15).
        bool bounded = true;
        bool belowThreshold = true;
        foreach (float s in new[] { 0.1f, 0.5f, 1f, 2f, 4f, 10f })
            foreach (float incoming in new[] { 0.2f, 1f, 3f, 8f, 20f })
                foreach (float removed in new[] { 0f, 0.25f, 0.5f, 0.575f, 0.75f, 1f })
                {
                    float plain = DND35.AI.Valuation.TurnOptionValue.TotalDefense(removed * incoming, incoming, s);
                    bounded &= plain <= s + 1e-4f;
                    var turn = new MeleeTurnOptionContext { AttackValue = s };
                    belowThreshold &= plain < s + turn.Margin;
                    if (removed < 0.575f)
                        belowThreshold &= DND35.AI.Valuation.TurnOptionValue.TotalDefense(2f * removed * incoming, incoming, s) < s + turn.Margin;
                }
        Assert(bounded, "total defense (Q9 default): at most S when no drop weight applies");
        Assert(belowThreshold, "total defense (Q9 default): below the attack's threshold without a drop weight, or with one when +4 AC removes under 57.5%");
        // A near-dropped creature (drop weight 2) whose foe hits on 15+ (0.3, at +4 AC 19+, 0.1: two thirds removed), S 4, 4.8 incoming:
        // 2 x 3.2 x 4 / 4.8 = 5.33 >= 4 + 0.6.
        float nearDropped = DND35.AI.Valuation.TurnOptionValue.TotalDefense(2f * 3.2f, 4.8f, 4f);
        Assert(nearDropped >= 4f + new MeleeTurnOptionContext { AttackValue = 4f }.Margin, "total defense (Q9 default): the near-dropped creature against high-roll hits passes", "got " + nearDropped);
    }

    private static MeleeTurnOptionContext Turn(float s, float offense = -1f, float defense = -1f, float total = -1f, bool reach = true)
        => new MeleeTurnOptionContext
        {
            AttackValue = s,
            CanAidOffense = offense >= 0f, AidOffenseValue = Mathf.Max(0f, offense),
            CanAidDefense = defense >= 0f, AidDefenseValue = Mathf.Max(0f, defense),
            CanTotalDefense = total >= 0f, TotalDefenseValue = Mathf.Max(0f, total), FoeCanReach = reach
        };

    private static void ExpectTurn(string name, MeleeTurnOptionContext ctx, MeleeTurnOption expected)
    {
        MeleeTurnOption got = AIProfile.DefaultChooseMeleeTurnOption(ctx);
        Assert(got == expected, "melee turn default: " + name + " -> " + expected, "got " + got);
    }

    private static void TestMeleeTurnOptionDefault()
    {
        ExpectTurn("no alternative", Turn(1f), MeleeTurnOption.Attack);
        ExpectTurn("aid offense below S + 0.5", Turn(1f, offense: 1.4f), MeleeTurnOption.Attack);
        ExpectTurn("aid offense at S + 0.5", Turn(1f, offense: 1.5f), MeleeTurnOption.AidOffense);
        ExpectTurn("a strong attack keeps the attack (margin 0.15 S)", Turn(10f, offense: 11.2f, defense: 11f, total: 11.4f), MeleeTurnOption.Attack);
        ExpectTurn("the best alternative wins", Turn(0.2f, offense: 0.8f, defense: 1.2f, total: 0.9f), MeleeTurnOption.AidDefense);
        ExpectTurn("total defense when a foe can reach", Turn(0.5f, total: 1.2f), MeleeTurnOption.TotalDefense);
        ExpectTurn("no total defense when no foe can reach (AI-063)", Turn(0.5f, total: 5f, reach: false), MeleeTurnOption.Attack);
        ExpectTurn("ties go to aid offense", Turn(0f, offense: 1f, defense: 1f, total: 1f), MeleeTurnOption.AidOffense);
        Assert(!Turn(0f, total: 5f, reach: false).IsOpen(MeleeTurnOption.TotalDefense) && Near(Turn(0f, total: 5f, reach: false).ValueOf(MeleeTurnOption.TotalDefense), 0f),
            "total defense is closed and worth 0 when no foe can reach");

        var profiles = new List<AIProfile>();
        try
        {
            profiles.Add(AIProfile.CreateRuntime<HumanoidAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<HealerAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<EvokerAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<DragonAIProfile>());
            var turns = new[] { Turn(1f), Turn(0.2f, offense: 0.8f, defense: 1.2f, total: 0.9f), Turn(0.5f, total: 5f, reach: false) };
            var prones = new[] { Prone(), Prone(threatened: true), Prone(reachable: true, crawl: true) };
            foreach (AIProfile p in profiles)
            {
                bool same = true;
                foreach (MeleeTurnOptionContext c in turns)
                    same &= p.ChooseMeleeTurnOption(null, c) == AIProfile.DefaultChooseMeleeTurnOption(c);
                foreach (ProneStartContext c in prones)
                    same &= p.ChooseProneStart(null, c) == AIProfile.DefaultChooseProneStart(c);
                Assert(same, p.GetType().Name + ".ChooseMeleeTurnOption and ChooseProneStart equal the defaults");
            }
        }
        finally
        {
            foreach (AIProfile p in profiles)
                if (p != null)
                    Object.DestroyImmediate(p);
        }
    }

    private static void TestMeleeTurnOptionTilt()
    {
        var weakAgainstStrong = Turn(1f, total: 1.3f);
        Assert(AiBlend.MeleeTurn((PersonalityProfile)null, MeleeTurnOption.Attack, weakAgainstStrong) == MeleeTurnOption.Attack
               && AiBlend.MeleeTurn((CharacterController)null, MeleeTurnOption.TotalDefense, Turn(0.2f, total: 1.3f)) == MeleeTurnOption.TotalDefense,
            "melee turn: no personality keeps the Role's choice");
        Assert(AiBlend.RolesOf(MeleeTurnOption.Attack) == ActionRoles.Attack && AiBlend.RolesOf(MeleeTurnOption.AidOffense) == ActionRoles.Support
               && AiBlend.RolesOf(MeleeTurnOption.AidDefense) == (ActionRoles.Support | ActionRoles.Defend)
               && AiBlend.RolesOf(MeleeTurnOption.TotalDefense) == ActionRoles.Defend && Near(AiBlend.MeleeTurnDesireGain, 0.5f),
            "melee turn roles: Attack, Support, Support and Defend, Defend; the desire gain is 0.5 (starter constant)");

        PersonalityProfile coward = P("Cowardly:1");
        PersonalityProfile berserker = P("Berserker:1");
        PersonalityProfile healer = P("Healer:1");
        PersonalityProfile smart = P("Smart:1");
        Assert(coward != null && berserker != null && healer != null && smart != null, "the melee turn test personalities build");
        if (coward == null || berserker == null || healer == null || smart == null)
            return;
        MeleeTurnOption role = AIProfile.DefaultChooseMeleeTurnOption(weakAgainstStrong);
        Assert(role == MeleeTurnOption.Attack, "S 1, total defense 1.3: the Role attacks (1.3 < 1.5)");
        // Cowardly: the attack 1 x (1 - 0.1) = 0.9, total defense 1.3 x 1.25 = 1.625 >= 0.9 + 0.5. (Hand-filled: under the Q9
        // default a total defense above S needs a drop weight, a creature one average hit drops; TestMeleeTurnOptionFormulas.)
        Assert(AiBlend.MeleeTurn(coward, role, weakAgainstStrong) == MeleeTurnOption.TotalDefense, "Cowardly takes total defense instead");
        Assert(AiBlend.MeleeTurn(berserker, role, weakAgainstStrong) == MeleeTurnOption.Attack, "Berserker attacks as the Role does");
        Assert(AiBlend.MeleeTurn(smart, role, weakAgainstStrong) == MeleeTurnOption.Attack, "Smart, naming no desire, keeps the Role's attack");
        // Berserker against the Role's total defense (S 0.2, total defense 1.3): 0.2 x 1.3 = 0.26 against 1.3 x 0.7 = 0.91: kept.
        var hopeless = Turn(0.2f, total: 1.3f);
        Assert(AIProfile.DefaultChooseMeleeTurnOption(hopeless) == MeleeTurnOption.TotalDefense
               && AiBlend.MeleeTurn(berserker, MeleeTurnOption.TotalDefense, hopeless) == MeleeTurnOption.TotalDefense,
            "a hopeless attack: the Role's total defense, which Berserker keeps (the attack does not beat it by the margin)");
        // Healer: aid offense 1.2 x 1.3 = 1.56 >= 1 + 0.5.
        var aid = Turn(1f, offense: 1.2f);
        Assert(AIProfile.DefaultChooseMeleeTurnOption(aid) == MeleeTurnOption.Attack && AiBlend.MeleeTurn(healer, MeleeTurnOption.Attack, aid) == MeleeTurnOption.AidOffense,
            "Healer aids where the Role would attack (Support .6)");
        // No foe can reach: total defense stays closed for every personality.
        Assert(AiBlend.MeleeTurn(coward, MeleeTurnOption.Attack, Turn(0.1f, total: 9f, reach: false)) == MeleeTurnOption.Attack,
            "Cowardly takes no total defense when no foe can reach it");
    }

    private static ProneStartContext Prone(bool threatened = false, bool works = true, bool ranged = true, bool reachable = false, bool crawl = false)
        => new ProneStartContext
        {
            Threatened = threatened,
            PlannedActionWorksProne = works,
            RangedFoeInView = ranged,
            SquareReachableNextTurn = reachable,
            CanCrawl = crawl,
            CrawlSquare = crawl ? new Vector2Int(1, 0) : (Vector2Int?)null
        };

    private static void ExpectProne(string name, ProneStartContext c, ProneStartChoice expected)
    {
        ProneStartChoice got = AIProfile.DefaultChooseProneStart(c);
        Assert(got == expected, "prone start default: " + name + " -> " + expected, "got " + got);
    }

    private static void TestProneStartDefault()
    {
        ExpectProne("unthreatened caster, a foe at range", Prone(), ProneStartChoice.StayProne);
        ExpectProne("threatened", Prone(threatened: true), ProneStartChoice.StandUp);
        ExpectProne("its planned action needs standing", Prone(works: false), ProneStartChoice.StandUp);
        ExpectProne("no foe attacks at range", Prone(ranged: false), ProneStartChoice.StandUp);
        ExpectProne("a foe could reach it and a safe square is a crawl away", Prone(reachable: true, crawl: true), ProneStartChoice.Crawl);
        ExpectProne("a foe could reach it and no safe square", Prone(reachable: true), ProneStartChoice.StayProne);
        ExpectProne("nobody could reach it: no crawl", Prone(crawl: true), ProneStartChoice.StayProne);
    }
}
}
#endif
