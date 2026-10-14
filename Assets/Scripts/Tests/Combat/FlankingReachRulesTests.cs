using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;
using Tests.Utilities;

namespace Tests.Combat
{
/// <summary>
/// Lightweight runtime checks for reach-aware flanking geometry and threat-distance semantics,
/// plus the PHB p.153 flanking bonus rule (flankers get +2 on melee attacks; the defender
/// takes no AC penalty, CMB-001) and flanking sneak attack eligibility, and the shared
/// attack-of-opportunity rules for movement and maneuvers (one movement AoO per opponent per round, CMB-128,
/// what stops a mover, who a grapple, sunder, trip, disarm, bull rush or coup de grace
/// provokes, CMB-014) for PC and NPC sides,
/// and the prone rules (standing up provokes; no ordinary movement while prone, CMB-074),
/// and reach into one's own square (a square-mate is threatened and may be attacked, CMB-131),
/// and Withdraw (start square exempt from enemies the mover can see, CanVisuallyPerceive, PHB p.143) through the
/// threat-aware PC click path, which keeps the caller's range, withdraw rule and swarm flags and falls back to the
/// direct route when a detour would stop short (GRID-001), a multi-square withdrawer's whole starting space exempt for
/// the whole withdraw, square by square (GRID-021), and a step provoking only for the squares it vacates (GRID-023).
/// Attach to any GameObject or call FlankingReachRulesTests.RunAllTests().
/// </summary>
public class FlankingReachRulesTests : MonoBehaviour
{
    private void Start()
    {
        RunAllTests();
    }

    public static void RunAllTests()
    {
        int passed = 0;
        int failed = 0;

        // Geometric opposite-side checks (independent from adjacency/reach).
        Assert(CombatUtils.IsFlanking(new Vector2Int(1, 1), new Vector2Int(3, 3), new Vector2Int(2, 2)),
            "Adjacent opposite diagonal flanks", ref passed, ref failed);

        Assert(CombatUtils.IsFlanking(new Vector2Int(0, 0), new Vector2Int(4, 4), new Vector2Int(2, 2)),
            "Reach-distance opposite diagonal still counts as opposite sides", ref passed, ref failed);

        Assert(!CombatUtils.IsFlanking(new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2)),
            "Same-side positions do not flank", ref passed, ref failed);

        // Weapon reach semantics for threat ring ranges.
        ItemDatabase.Init();
        AssertThreatBand(ItemIDs.LONGSPEAR, expectedMin: 2, expectedMax: 2, ref passed, ref failed);
        AssertThreatBand(ItemIDs.SPIKED_CHAIN, expectedMin: 1, expectedMax: 2, ref passed, ref failed);
        AssertThreatBand(ItemIDs.WHIP, expectedMin: 2, expectedMax: 3, ref passed, ref failed);
        AssertThreatBand(ItemIDs.LONGSWORD, expectedMin: 1, expectedMax: 1, ref passed, ref failed);

        // PHB p.153 flanking: attacker bonus only, no defender AC penalty (CMB-001).
        TestFlankedConditionHasNoAcPenalty(ref passed, ref failed);
        TestFlankingAttackGetsBonusAndSneakAttack(ref passed, ref failed);
        TestFlankedTagAloneGivesNonFlankerNothing(ref passed, ref failed);

        // Shared AoO rules: movement and maneuvers provoke the same way for PCs and NPCs.
        TestPathAoOsOncePerOpponent(CharacterTeam.Enemy, ref passed, ref failed);
        TestPathAoOsOncePerOpponent(CharacterTeam.Player, ref passed, ref failed);
        TestPathAoOsOncePerRound(CharacterTeam.Enemy, ref passed, ref failed);
        TestPathAoOsOncePerRound(CharacterTeam.Player, ref passed, ref failed);
        TestWithdrawStartSquareUnseenEnemy(CharacterTeam.Enemy, ref passed, ref failed);
        TestWithdrawStartSquareUnseenEnemy(CharacterTeam.Player, ref passed, ref failed);
        TestWithdrawWholeStartingSpace(CharacterTeam.Enemy, ref passed, ref failed);
        TestWithdrawWholeStartingSpace(CharacterTeam.Player, ref passed, ref failed);
        TestMultiSquareStepProvokesOnlyForVacatedSquares(CharacterTeam.Enemy, ref passed, ref failed);
        TestMultiSquareStepProvokesOnlyForVacatedSquares(CharacterTeam.Player, ref passed, ref failed);
        TestThreatAwarePathKeepsCallerRules(CharacterTeam.Enemy, ref passed, ref failed);
        TestThreatAwarePathKeepsCallerRules(CharacterTeam.Player, ref passed, ref failed);
        TestThreatAwarePathFallsBackToDirectRoute(CharacterTeam.Enemy, ref passed, ref failed);
        TestThreatAwarePathFallsBackToDirectRoute(CharacterTeam.Player, ref passed, ref failed);
        TestMovementStopsOnlyOnAoOChanges(ref passed, ref failed);
        TestSharedSquareThreat(CharacterTeam.Enemy, ref passed, ref failed);
        TestSharedSquareThreat(CharacterTeam.Player, ref passed, ref failed);
        TestManeuverAoOProvokers(CharacterTeam.Enemy, ref passed, ref failed);
        TestManeuverAoOProvokers(CharacterTeam.Player, ref passed, ref failed);
        TestManeuverAoODisruption(ref passed, ref failed);

        // Prone: standing up provokes, and prone creatures take no ordinary movement (CMB-074).
        TestStandUpAoOProvokers(CharacterTeam.Enemy, ref passed, ref failed);
        TestStandUpAoOProvokers(CharacterTeam.Player, ref passed, ref failed);
        TestProneMovementAndStandUpRules(CharacterTeam.Enemy, ref passed, ref failed);
        TestProneMovementAndStandUpRules(CharacterTeam.Player, ref passed, ref failed);

        Debug.Log($"[FlankReachTest] === RESULTS: {passed} passed, {failed} failed ===");
    }

    private static void TestFlankedConditionHasNoAcPenalty(ref int passed, ref int failed)
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Flanked);
        Assert(def != null && def.ArmorClassModifier == 0,
            "Flanked condition definition has no AC modifier", ref passed, ref failed);

        CharacterController defender = null;
        try
        {
            defender = TestHelpers.CreateCharacter(name: "FlankedDefender", dex: 14);
            int baseAc = defender.Stats.ArmorClass;
            int baseTouch = defender.Stats.TouchArmorClass;

            defender.ApplyCondition(CombatConditionType.Flanked, -1, "Flanking");

            Assert(defender.HasCondition(CombatConditionType.Flanked) && defender.Stats.IsFlanked,
                "Flanked condition still applies as a display tag", ref passed, ref failed);
            Assert(defender.Stats.ArmorClass == baseAc,
                $"Flanked does not lower AC (base={baseAc}, flanked={defender.Stats.ArmorClass})", ref passed, ref failed);
            Assert(defender.Stats.TouchArmorClass == baseTouch,
                $"Flanked does not lower touch AC (base={baseTouch}, flanked={defender.Stats.TouchArmorClass})", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(defender != null ? defender.gameObject : null);
        }
    }

    private static void TestFlankingAttackGetsBonusAndSneakAttack(ref int passed, ref int failed)
    {
        CharacterController rogue = null;
        CharacterController defender = null;
        try
        {
            rogue = TestHelpers.CreateRogue("FlankingRogue", level: 5);
            TestHelpers.SetGridPosition(rogue, 0, 0);

            bool sawHit = false;
            bool bonusAlwaysRecorded = true;
            bool sneakOnEveryHit = true;
            Random.InitState(1530);
            for (int i = 0; i < 40; i++)
            {
                // Fresh defender each swing so prior damage (and any unconscious/helpless
                // state, which would itself allow sneak attack) cannot leak into the check.
                TestHelpers.Cleanup(defender != null ? defender.gameObject : null);
                defender = TestHelpers.CreateCharacter(name: "FlankedTarget", dex: 10);
                TestHelpers.SetGridPosition(defender, 1, 0);
                defender.ApplyCondition(CombatConditionType.Flanked, -1, "Flanking");
                CombatResult r = rogue.Attack(defender, true, CombatUtils.FlankingAttackBonus, "Partner");
                if (r == null) { bonusAlwaysRecorded = false; break; }
                if (!r.IsFlanking || r.FlankingBonus != 2) bonusAlwaysRecorded = false;
                if (r.Hit)
                {
                    sawHit = true;
                    if (!r.SneakAttackApplied || !r.SneakAttackByFlanking) sneakOnEveryHit = false;
                }
            }

            Assert(bonusAlwaysRecorded, "Flanking melee attacker gets +2 flanking bonus", ref passed, ref failed);
            Assert(sawHit, "Flanking rogue landed at least one hit in 40 attacks", ref passed, ref failed);
            Assert(sawHit && sneakOnEveryHit, "Flanking rogue hit applies sneak attack (by flanking)", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(rogue != null ? rogue.gameObject : null, defender != null ? defender.gameObject : null);
        }
    }

    private static void TestFlankedTagAloneGivesNonFlankerNothing(ref int passed, ref int failed)
    {
        // Regression: an attacker who is not itself flanking (e.g. a third ally or an archer)
        // gets no flanking bonus and no flanking sneak attack against a Flanked target.
        CharacterController rogue = null;
        CharacterController defender = null;
        try
        {
            rogue = TestHelpers.CreateRogue("NonFlankingRogue", level: 5);
            TestHelpers.SetGridPosition(rogue, 0, 0);

            bool sawHit = false;
            bool noBonus = true;
            bool noSneak = true;
            Random.InitState(1531);
            for (int i = 0; i < 40; i++)
            {
                // Fresh defender each swing so prior damage (and any unconscious/helpless
                // state, which would itself allow sneak attack) cannot leak into the check.
                TestHelpers.Cleanup(defender != null ? defender.gameObject : null);
                defender = TestHelpers.CreateCharacter(name: "TaggedTarget", dex: 10);
                TestHelpers.SetGridPosition(defender, 1, 0);
                defender.ApplyCondition(CombatConditionType.Flanked, -1, "Flanking");
                CombatResult r = rogue.Attack(defender, false, 0, null);
                if (r == null) { noBonus = false; break; }
                if (r.IsFlanking || r.FlankingBonus != 0) noBonus = false;
                if (r.Hit)
                {
                    sawHit = true;
                    if (r.SneakAttackApplied) noSneak = false;
                }
            }

            Assert(noBonus, "Non-flanking attacker gets no flanking bonus vs Flanked target", ref passed, ref failed);
            Assert(sawHit && noSneak, "Non-flanking rogue gets no sneak attack from the Flanked tag", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(rogue != null ? rogue.gameObject : null, defender != null ? defender.gameObject : null);
        }
    }

    // ------------------------------------------------------------------------
    // Shared AoO rules for movement and maneuvers (CMB-005, CMB-073, CMB-076).
    // The GameManager coroutines that apply them need Play mode; these check the
    // ThreatSystem rules they all call, for an NPC mover and a PC mover alike.
    // ------------------------------------------------------------------------

    private static CharacterController CreateTeamCharacter(string name, CharacterTeam team, int x, int y)
    {
        CharacterController c = TestHelpers.CreateCharacter(name: name);
        c.SetTeam(team);
        TestHelpers.SetGridPosition(c, x, y);
        ThreatSystem.ResetAoOForTurn(c);
        return c;
    }

    private static void TestPathAoOsOncePerOpponent(CharacterTeam moverTeam, ref int passed, ref int failed)
    {
        CharacterTeam threatTeam = moverTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = moverTeam == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController mover = null;
        CharacterController threatener = null;
        try
        {
            mover = CreateTeamCharacter("PathMover", moverTeam, 0, 0);
            threatener = CreateTeamCharacter("PathThreatener", threatTeam, 1, 0);
            var all = new List<CharacterController> { mover, threatener };

            // Leaves (0,0) and (0,1), both threatened from (1,0), then (0,2), which is not.
            var path = new List<Vector2Int> { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3) };

            // Combat Reflexes-sized pool: still one opportunity for this movement (PHB p.138).
            threatener.Stats.MaxAttacksOfOpportunity = 3;
            threatener.Stats.AttacksOfOpportunityUsed = 0;
            List<AoOThreatInfo> aoos = ThreatSystem.AnalyzePathForAoOs(mover, path, all);
            Assert(aoos.Count == 1 && aoos[0].PathIndex == 0 && aoos[0].Threatener == threatener,
                $"{side} mover leaving two squares threatened by one Combat Reflexes enemy provokes once, at the first square (got {aoos.Count})",
                ref passed, ref failed);

            // Withdraw: the first square is exempt, the next threatened square still provokes.
            List<AoOThreatInfo> withdraw = ThreatSystem.AnalyzePathForAoOs(mover, path, all, suppressFirstSquareAoO: true);
            Assert(withdraw.Count == 1 && withdraw[0].PathIndex == 1,
                $"{side} withdraw skips the first square but provokes on leaving the second threatened square",
                ref passed, ref failed);

            // Regression: an enemy that has spent its AoOs this round gets none.
            threatener.Stats.MaxAttacksOfOpportunity = 1;
            threatener.Stats.AttacksOfOpportunityUsed = 1;
            Assert(ThreatSystem.AnalyzePathForAoOs(mover, path, all).Count == 0,
                $"{side} mover provokes nothing from an enemy with no AoOs left", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null, threatener != null ? threatener.gameObject : null);
        }
    }

    private static void TestPathAoOsOncePerRound(CharacterTeam moverTeam, ref int passed, ref int failed)
    {
        // PHB p.138 (owner ruling 2026-10-08, CMB-128): the single opportunity covers the whole round, so a
        // second move this round (a double move, a bull rush follow after a move) lists no opponent whose
        // movement opportunity against this mover already came; other opponents are unaffected; the mover's
        // turn start (the round boundary) clears it. A bull rush push reads the same record (owner interpretation
        // 2026-10-11, CMB-150); BullRushRulesTests checks the push itself.
        CharacterTeam threatTeam = moverTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = moverTeam == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController mover = null;
        CharacterController threatener = null;
        CharacterController second = null;
        try
        {
            mover = CreateTeamCharacter("RoundMover", moverTeam, 0, 0);
            threatener = CreateTeamCharacter("RoundThreatener", threatTeam, 1, 0);
            second = CreateTeamCharacter("RoundSecond", threatTeam, 1, 1);
            var all = new List<CharacterController> { mover, threatener, second };
            var path = new List<Vector2Int> { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3) };
            threatener.Stats.MaxAttacksOfOpportunity = 3;
            threatener.Stats.AttacksOfOpportunityUsed = 0;
            second.Stats.MaxAttacksOfOpportunity = 3;
            second.Stats.AttacksOfOpportunityUsed = 0;

            List<AoOThreatInfo> fresh = ThreatSystem.AnalyzePathForAoOs(mover, path, all);
            Assert(fresh.Count == 2, $"{side} round record: both opponents provoke on a fresh round (got {fresh.Count})", ref passed, ref failed);
            Assert(!ThreatSystem.HasHadMovementOpportunity(threatener, mover),
                $"{side} round record: the analysis alone records nothing (previews and AI scoring are read-only)", ref passed, ref failed);

            ThreatSystem.RecordMovementOpportunity(threatener, mover);
            List<AoOThreatInfo> later = ThreatSystem.AnalyzePathForAoOs(mover, path, all);
            Assert(later.Count == 1 && later[0].Threatener == second,
                $"{side} round record: a later move this round provokes only the opponent that has not had its opportunity (got {later.Count})",
                ref passed, ref failed);

            ThreatSystem.ClearMovementOpportunities(mover);
            Assert(ThreatSystem.AnalyzePathForAoOs(mover, path, all).Count == 2,
                $"{side} round record: after the record is cleared both opponents provoke again", ref passed, ref failed);

            ThreatSystem.RecordMovementOpportunity(threatener, mover);
            ThreatSystem.RecordMovementOpportunity(second, mover);
            string startError = null;
            try { mover.StartNewTurn(); }
            catch (System.Exception ex) { startError = ex.GetType().Name + ": " + ex.Message; }
            Assert(startError == null && !ThreatSystem.HasHadMovementOpportunity(threatener, mover) && !ThreatSystem.HasHadMovementOpportunity(second, mover),
                $"{side} round record: the mover's turn start clears it" + (startError != null ? " (StartNewTurn threw " + startError + ")" : string.Empty),
                ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null, threatener != null ? threatener.gameObject : null,
                second != null ? second.gameObject : null);
        }
    }

    /// <summary>Gives <paramref name="target"/> Invisibility's total concealment (no See Invisibility on the observer).</summary>
    private static void MakeInvisible(CharacterController target)
        => AddConcealmentEffect(target, SpellNames.INVISIBILITY, "Invisibility", 50, isTotal: true);

    /// <summary>A concealment-style spell effect as StatusEffectManager records it (MissChance, IsTotalConcealment).</summary>
    private static void AddConcealmentEffect(CharacterController target, string spellId, string name, int missChance, bool isTotal)
    {
        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
        {
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(target.Stats);
        }

        statusMgr.ActiveEffects.Add(new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = spellId, Name = name },
            CasterName = "UnitTest",
            CasterLevel = 5,
            RemainingRounds = 5,
            DurationType = DurationType.Rounds,
            AffectedCharacterName = target.Stats.CharacterName,
            BonusTypeLegacy = "Concealment",
            BonusTypeEnum = BonusType.Concealment,
            IsApplied = true,
            MissChance = missChance,
            IsTotalConcealment = isTotal,
            ConcealmentSource = name
        });
    }

    private static void TestWithdrawStartSquareUnseenEnemy(CharacterTeam moverTeam, ref int passed, ref int failed)
    {
        // PHB p.143 (GRID-001): a withdrawer's start square is not threatened by opponents it can see; invisible
        // opponents still get their AoO for it. Later squares provoke as normal.
        CharacterTeam threatTeam = moverTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = moverTeam == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController mover = null;
        CharacterController enemy = null;
        try
        {
            mover = CreateTeamCharacter("WithdrawMover", moverTeam, 0, 0);
            enemy = CreateTeamCharacter("WithdrawEnemy", threatTeam, 1, 0);
            enemy.Stats.MaxAttacksOfOpportunity = 1;
            enemy.Stats.AttacksOfOpportunityUsed = 0;
            var all = new List<CharacterController> { mover, enemy };
            // Straight away from the enemy: only the start square is threatened.
            var path = new List<Vector2Int> { new Vector2Int(-1, 0), new Vector2Int(-2, 0), new Vector2Int(-3, 0) };

            Assert(mover.CanVisuallyPerceive(enemy) && ThreatSystem.AnalyzePathForAoOs(mover, path, all, suppressFirstSquareAoO: true).Count == 0,
                $"{side} withdraw: leaving the start square provokes no AoO from a visible enemy", ref passed, ref failed);

            // Seen despite a 50% miss chance: incorporeal (MM p.310), Displacement (PHB p.223: not real total
            // concealment) and Blink (ethereal, PHB p.206). The miss-chance test CanSee calls these unseen (CMB-078);
            // the withdraw rule asks whether the mover sees the enemy.
            enemy.ConfigureIncorporeal(true);
            Assert(mover.CanVisuallyPerceive(enemy) && ThreatSystem.AnalyzePathForAoOs(mover, path, all, suppressFirstSquareAoO: true).Count == 0,
                $"{side} withdraw: an incorporeal enemy is seen, so the start square provokes nothing", ref passed, ref failed);
            Assert(ThreatSystem.AnalyzePathForAoOs(mover, path, all).Count == 1,
                $"{side} ordinary move: the incorporeal enemy provokes on leaving the start square", ref passed, ref failed);
            enemy.ConfigureIncorporeal(false);

            AddConcealmentEffect(enemy, SpellNames.DISPLACEMENT, "Displacement", 50, isTotal: false);
            AddConcealmentEffect(enemy, SpellNames.BLINK, "Blink", 50, isTotal: false);
            Assert(mover.CanVisuallyPerceive(enemy) && ThreatSystem.AnalyzePathForAoOs(mover, path, all, suppressFirstSquareAoO: true).Count == 0,
                $"{side} withdraw: a displaced and blinking enemy is seen, so the start square provokes nothing", ref passed, ref failed);
            enemy.GetComponent<StatusEffectManager>().ActiveEffects.Clear();

            // A blinded mover sees no one (DMG p.300), so an adjacent enemy keeps its AoO for the start square.
            mover.ApplyCondition(CombatConditionType.Blinded, 3, "Unit Test");
            List<AoOThreatInfo> blindAoOs = ThreatSystem.AnalyzePathForAoOs(mover, path, all, suppressFirstSquareAoO: true);
            Assert(!mover.CanVisuallyPerceive(enemy) && blindAoOs.Count == 1 && blindAoOs[0].PathIndex == 0,
                $"{side} withdraw: a blinded mover provokes from the enemy it cannot see (got {blindAoOs.Count})", ref passed, ref failed);
            mover.RemoveCondition(CombatConditionType.Blinded);

            MakeInvisible(enemy);
            Assert(!mover.CanVisuallyPerceive(enemy),
                $"{side} withdraw: precondition, the mover cannot see the invisible enemy", ref passed, ref failed);
            List<AoOThreatInfo> aoos = ThreatSystem.AnalyzePathForAoOs(mover, path, all, suppressFirstSquareAoO: true);
            Assert(aoos.Count == 1 && aoos[0].Threatener == enemy && aoos[0].PathIndex == 0,
                $"{side} withdraw: an invisible enemy still gets its AoO for the start square (PHB p.143; got {aoos.Count})",
                ref passed, ref failed);
            Assert(ThreatSystem.AnalyzePathForAoOs(mover, path, all).Count == 1,
                $"{side} ordinary move: the invisible enemy provokes on leaving the start square", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null, enemy != null ? enemy.gameObject : null);
        }
    }

    private static void TestWithdrawWholeStartingSpace(CharacterTeam moverTeam, ref int passed, ref int failed)
    {
        // GRID-021 (delegated RAW reading 2026-10-14; PHB p.143, DMG p.29): every square of a withdrawer's starting
        // space is "the square you start out in", not threatened by an enemy it can see for the whole withdraw, square by
        // square: vacating a starting-space square at any step provokes only from an unseen enemy, and squares outside
        // the starting space provoke as normal.
        CharacterTeam threatTeam = moverTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = moverTeam == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController mover = null;
        CharacterController enemy = null;
        CharacterController far = null;
        CharacterController inside = null;
        try
        {
            // A Large mover based at (1,0) covers (1,0), (2,0), (1,1), (2,1). Withdrawing east to base (2,0) vacates
            // (1,0) and (1,1); on to (3,0) vacates (2,0) and (2,1). An enemy at (1,-1) threatens (1,0) and (2,0).
            mover = CreateTeamCharacter("LargeWithdrawer", moverTeam, 1, 0);
            mover.Stats.CurrentSizeCategory = SizeCategory.Large;
            enemy = CreateTeamCharacter("LargeWithdrawEnemy", threatTeam, 1, -1);
            enemy.Stats.MaxAttacksOfOpportunity = 1;
            enemy.Stats.AttacksOfOpportunityUsed = 0;
            var all = new List<CharacterController> { mover, enemy };
            var east = new List<Vector2Int> { new Vector2Int(2, 0), new Vector2Int(3, 0) };

            Assert(mover.GetOccupiedSquaresAt(mover.GridPosition).Count == 4,
                $"{side} Large withdraw: precondition, the mover covers 2x2 squares", ref passed, ref failed);
            List<AoOThreatInfo> withdraw = ThreatSystem.AnalyzePathForAoOs(mover, east, all, suppressFirstSquareAoO: true);
            Assert(withdraw.Count == 0,
                $"{side} Large withdraw: leaving any square of the starting space, at any step, provokes nothing from a visible enemy (got {withdraw.Count})",
                ref passed, ref failed);
            List<AoOThreatInfo> ordinary = ThreatSystem.AnalyzePathForAoOs(mover, east, all);
            Assert(ordinary.Count == 1 && ordinary[0].PathIndex == 0 && ordinary[0].ProvokedAtSquare == new Vector2Int(1, 0),
                $"{side} Large ordinary move: the same path provokes at the first step, from the square it vacates (got {ordinary.Count})",
                ref passed, ref failed);

            // An enemy the mover cannot see still threatens the starting space (PHB p.143).
            MakeInvisible(enemy);
            List<AoOThreatInfo> unseen = ThreatSystem.AnalyzePathForAoOs(mover, east, all, suppressFirstSquareAoO: true);
            Assert(!mover.CanVisuallyPerceive(enemy) && unseen.Count == 1 && unseen[0].PathIndex == 0,
                $"{side} Large withdraw: an invisible enemy still gets its AoO, at the first step (got {unseen.Count})", ref passed, ref failed);
            enemy.GetComponent<StatusEffectManager>().ActiveEffects.Clear();

            // Square by square: an enemy at (4,-1) threatens (3,0), outside the starting space. One more step to base
            // (4,0) vacates (3,0), so it provokes at index 2 even during the withdraw.
            far = CreateTeamCharacter("LargeWithdrawFar", threatTeam, 4, -1);
            far.Stats.MaxAttacksOfOpportunity = 1;
            far.Stats.AttacksOfOpportunityUsed = 0;
            var withFar = new List<CharacterController> { mover, enemy, far };
            var further = new List<Vector2Int> { new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0) };
            List<AoOThreatInfo> squareBySquare = ThreatSystem.AnalyzePathForAoOs(mover, further, withFar, suppressFirstSquareAoO: true);
            Assert(squareBySquare.Count == 1 && squareBySquare[0].Threatener == far && squareBySquare[0].PathIndex == 2
                   && squareBySquare[0].ProvokedAtSquare == new Vector2Int(3, 0),
                $"{side} Large withdraw: a square outside the starting space provokes as normal (got {squareBySquare.Count})", ref passed, ref failed);

            // DMG p.29: a smaller creature inside a larger creature's space gets no AoO when the larger one withdraws
            // out of it, though the squares are left at two different steps.
            TestHelpers.Cleanup(far.gameObject);
            far = null;
            inside = CreateTeamCharacter("LargeWithdrawInside", threatTeam, 2, 1);
            inside.Stats.MaxAttacksOfOpportunity = 1;
            inside.Stats.AttacksOfOpportunityUsed = 0;
            var withInside = new List<CharacterController> { mover, inside };
            var west = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(-1, 0) };
            Assert(ThreatSystem.AnalyzePathForAoOs(mover, west, withInside, suppressFirstSquareAoO: true).Count == 0,
                $"{side} Large withdraw: a creature inside the starting space gets no AoO (DMG p.29)", ref passed, ref failed);
            List<AoOThreatInfo> insideOrdinary = ThreatSystem.AnalyzePathForAoOs(mover, west, withInside);
            Assert(insideOrdinary.Count == 1 && insideOrdinary[0].Threatener == inside,
                $"{side} Large ordinary move: the creature inside the space gets its AoO (got {insideOrdinary.Count})", ref passed, ref failed);

            // A Medium withdrawer that comes back through its start square and leaves it again: still exempt.
            TestHelpers.Cleanup(inside.gameObject);
            inside = null;
            mover.Stats.CurrentSizeCategory = SizeCategory.Medium;
            TestHelpers.SetGridPosition(mover, 0, 0);
            TestHelpers.SetGridPosition(enemy, 1, 0);
            var loop = new List<Vector2Int> { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(-1, 1) };
            Assert(mover.GetOccupiedSquaresAt(mover.GridPosition).Count == 1
                   && ThreatSystem.AnalyzePathForAoOs(mover, loop, all, suppressFirstSquareAoO: true).Count == 0,
                $"{side} Medium withdraw: leaving the start square a second time provokes nothing from a visible enemy", ref passed, ref failed);
            List<AoOThreatInfo> loopOrdinary = ThreatSystem.AnalyzePathForAoOs(mover, loop, all);
            Assert(loopOrdinary.Count == 1 && loopOrdinary[0].PathIndex == 0,
                $"{side} Medium ordinary move: the same loop provokes on first leaving the start square (got {loopOrdinary.Count})", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null, enemy != null ? enemy.gameObject : null,
                far != null ? far.gameObject : null, inside != null ? inside.gameObject : null);
        }
    }

    private static void TestMultiSquareStepProvokesOnlyForVacatedSquares(CharacterTeam moverTeam, ref int passed, ref int failed)
    {
        // GRID-023 (delegated RAW reading 2026-10-14; PHB p.137-138, DMG p.29): a step moves out of only the squares it
        // vacates (the previous footprint minus the new one). A Large mover based at (0,0) shifting east to (1,0)
        // vacates (0,0) and (0,1) and still covers (1,0); an enemy at (2,-1) threatens (1,0) but neither vacated square.
        CharacterTeam threatTeam = moverTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = moverTeam == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController mover = null;
        CharacterController enemy = null;
        try
        {
            mover = CreateTeamCharacter("LargeShifter", moverTeam, 0, 0);
            mover.Stats.CurrentSizeCategory = SizeCategory.Large;
            enemy = CreateTeamCharacter("ShiftEnemy", threatTeam, 2, -1);
            enemy.Stats.MaxAttacksOfOpportunity = 1;
            enemy.Stats.AttacksOfOpportunityUsed = 0;
            var all = new List<CharacterController> { mover, enemy };

            Assert(ThreatSystem.AnalyzePathForAoOs(mover, new List<Vector2Int> { new Vector2Int(1, 0) }, all).Count == 0,
                $"{side} Large one-square shift: no AoO from an enemy that threatens only a square the mover still covers", ref passed, ref failed);

            // The next step east vacates (1,0) and (1,1): now it provokes.
            List<AoOThreatInfo> twoSteps = ThreatSystem.AnalyzePathForAoOs(mover,
                new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(2, 0) }, all);
            Assert(twoSteps.Count == 1 && twoSteps[0].PathIndex == 1 && twoSteps[0].ProvokedAtSquare == new Vector2Int(1, 0),
                $"{side} Large move: the AoO comes when the threatened square is vacated, at the second step (got {twoSteps.Count})",
                ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null, enemy != null ? enemy.gameObject : null);
        }
    }

    private static void TestThreatAwarePathKeepsCallerRules(CharacterTeam moverTeam, ref int passed, ref int failed)
    {
        // GRID-001: the PC click path (MovementService.FindPath with avoidThreats: true) keeps the caller's range
        // (a withdraw's double move), the withdraw start-square rule and the swarm flags, exactly like the AI's
        // avoidThreats: false calls; only the route preference differs.
        CharacterTeam threatTeam = moverTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = moverTeam == CharacterTeam.Player ? "PC" : "NPC";
        SquareGrid previousGrid = SquareGrid.Instance;
        GameObject gridGo = null;
        GameObject serviceGo = null;
        CharacterController mover = null;
        CharacterController enemy = null;
        try
        {
            gridGo = new GameObject("GRID001_Grid");
            SquareGrid grid = gridGo.AddComponent<SquareGrid>();
            grid.Width = 16;
            grid.Height = 5;
            grid.GenerateGrid();

            mover = CreateTeamCharacter("ThreatPathMover", moverTeam, 12, 2);
            enemy = CreateTeamCharacter("ThreatPathEnemy", threatTeam, 13, 2);
            grid.SetCreatureOccupancy(mover, mover.GridPosition, 1);
            grid.SetCreatureOccupancy(enemy, enemy.GridPosition, 1);
            enemy.Stats.MaxAttacksOfOpportunity = 1;
            enemy.Stats.AttacksOfOpportunityUsed = 0;
            var all = new List<CharacterController> { mover, enemy };

            serviceGo = new GameObject("GRID001_MovementService");
            MovementService service = serviceGo.AddComponent<MovementService>();
            service.Initialize(grid, () => all);

            int single = mover.Stats.MoveRange;
            var farSquare = new Vector2Int(2, 2); // 10 squares west: beyond a single move, within a withdraw
            Assert(single > 0 && single < 10 && single * 2 >= 10,
                $"{side} threat-aware path: precondition, 50 ft is beyond one move ({single} sq) but within a withdraw", ref passed, ref failed);

            foreach (bool avoidThreats in new[] { true, false })
            {
                string mode = avoidThreats ? "PC click (avoidThreats)" : "AI (no threat routing)";
                AoOPathResult withdraw = service.FindPath(mover, farSquare, avoidThreats: avoidThreats,
                    maxRangeOverride: single * 2, suppressFirstSquareAoO: true);
                bool reaches = withdraw != null && withdraw.Path != null && withdraw.Path.Count > 0
                    && withdraw.Path[withdraw.Path.Count - 1] == farSquare;
                Assert(reaches, $"{side} {mode}: a withdraw reaches a square 10 squares away with its double-move range", ref passed, ref failed);
                Assert(reaches && withdraw.ProvokedAoOs.Count == 0,
                    $"{side} {mode}: a withdraw's start square provokes no AoO from the visible adjacent enemy (got {withdraw?.ProvokedAoOs?.Count})",
                    ref passed, ref failed);

                AoOPathResult move = service.FindPath(mover, new Vector2Int(9, 2), avoidThreats: avoidThreats, maxRangeOverride: single);
                Assert(move != null && move.Path != null && move.Path.Count > 0 && move.ProvokedAoOs.Count == 1 && move.ProvokedAoOs[0].PathIndex == 0,
                    $"{side} {mode}: an ordinary move out of the threatened start square provokes once", ref passed, ref failed);

                AoOPathResult tooFar = service.FindPath(mover, farSquare, avoidThreats: avoidThreats, maxRangeOverride: single);
                Assert(tooFar == null || tooFar.Path == null || tooFar.Path.Count == 0 || tooFar.Path[tooFar.Path.Count - 1] != farSquare,
                    $"{side} {mode}: a single move does not reach 10 squares", ref passed, ref failed);
            }

            // Swarms move through and end in enemy squares (MM p.316), on the PC click path and its destination check too.
            Assert(!service.IsValidMoveDestination(mover, enemy.GridPosition),
                $"{side} move destination: a non-swarm cannot end in an enemy's square (PHB p.148)", ref passed, ref failed);
            mover.Stats.IsSwarm = true;
            Assert(service.IsValidMoveDestination(mover, enemy.GridPosition),
                $"{side} move destination: a swarm may end in an enemy's square (MM p.316)", ref passed, ref failed);
            AoOPathResult swarmPath = service.FindPath(mover, enemy.GridPosition, avoidThreats: true, maxRangeOverride: single);
            Assert(swarmPath != null && swarmPath.Path != null && swarmPath.Path.Count > 0 && swarmPath.Path[swarmPath.Path.Count - 1] == enemy.GridPosition,
                $"{side} PC click path: a swarm's threat-aware path ends in the enemy's square", ref passed, ref failed);
            mover.Stats.IsSwarm = false;
        }
        finally
        {
            if (serviceGo != null) Object.DestroyImmediate(serviceGo);
            if (gridGo != null) Object.DestroyImmediate(gridGo);
            // SquareGrid.Awake replaced the static Instance; put the previous grid back (GRID-010).
            typeof(SquareGrid).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, previousGrid);
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null, enemy != null ? enemy.gameObject : null);
        }
    }

    private static void TestThreatAwarePathFallsBackToDirectRoute(CharacterTeam moverTeam, ref int passed, ref int failed)
    {
        // GRID-001 review: A* prunes by straight-line distance, so a threat-avoiding detour can cost more than the
        // budget and come back trimmed. The PC click path (avoidThreats: true) then takes the direct route, so it
        // reaches as far as the AI's path. Mover (2,2), enemy (6,2) threatening x 5-7, y 1-3, destination (9,2), budget
        // 8 squares: the direct route past the enemy costs 8 (one AoO), the detour along y = 0 costs 9.
        CharacterTeam threatTeam = moverTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = moverTeam == CharacterTeam.Player ? "PC" : "NPC";
        SquareGrid previousGrid = SquareGrid.Instance;
        GameObject gridGo = null;
        GameObject serviceGo = null;
        CharacterController mover = null;
        CharacterController enemy = null;
        try
        {
            gridGo = new GameObject("GRID001_FallbackGrid");
            SquareGrid grid = gridGo.AddComponent<SquareGrid>();
            grid.Width = 16;
            grid.Height = 5;
            grid.GenerateGrid();

            mover = CreateTeamCharacter("FallbackMover", moverTeam, 2, 2);
            enemy = CreateTeamCharacter("FallbackEnemy", threatTeam, 6, 2);
            grid.SetCreatureOccupancy(mover, mover.GridPosition, 1);
            grid.SetCreatureOccupancy(enemy, enemy.GridPosition, 1);
            enemy.Stats.MaxAttacksOfOpportunity = 1;
            enemy.Stats.AttacksOfOpportunityUsed = 0;
            var all = new List<CharacterController> { mover, enemy };

            serviceGo = new GameObject("GRID001_FallbackService");
            MovementService service = serviceGo.AddComponent<MovementService>();
            service.Initialize(grid, () => all);

            var destination = new Vector2Int(9, 2);
            foreach (bool avoidThreats in new[] { true, false })
            {
                string mode = avoidThreats ? "PC click (avoidThreats)" : "AI (no threat routing)";
                AoOPathResult r = service.FindPath(mover, destination, avoidThreats: avoidThreats, maxRangeOverride: 8);
                bool reaches = r != null && r.Path != null && r.Path.Count > 0 && r.Path[r.Path.Count - 1] == destination;
                Assert(reaches && SquareGridUtils.CalculatePathCost(mover.GridPosition, r.Path) <= 8,
                    $"{side} {mode}: reaches (9,2) within 8 squares when only the direct route fits", ref passed, ref failed);
                Assert(reaches && r.ProvokedAoOs.Count == 1,
                    $"{side} {mode}: the direct route past the enemy provokes once (got {r?.ProvokedAoOs?.Count})", ref passed, ref failed);
            }

            AoOPathResult roomy = service.FindPath(mover, destination, avoidThreats: true, maxRangeOverride: 9);
            Assert(roomy != null && roomy.Path != null && roomy.Path.Count > 0 && roomy.Path[roomy.Path.Count - 1] == destination && roomy.ProvokedAoOs.Count == 0,
                $"{side} PC click (avoidThreats): with 9 squares the detour fits and provokes nothing", ref passed, ref failed);
        }
        finally
        {
            if (serviceGo != null) Object.DestroyImmediate(serviceGo);
            if (gridGo != null) Object.DestroyImmediate(gridGo);
            // SquareGrid.Awake replaced the static Instance; put the previous grid back (GRID-010).
            typeof(SquareGrid).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, previousGrid);
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null, enemy != null ? enemy.gameObject : null);
        }
    }

    private static void TestSharedSquareThreat(CharacterTeam moverTeam, ref int passed, ref int failed)
    {
        // Owner ruling 2026-10-08 (CMB-131): a creature reaches into its own square (PHB p.149), so it threatens a
        // creature sharing it (PHB p.137) and may melee it; leaving the shared square provokes, but not as a
        // withdraw's first square; a reach weapon still cannot attack there (PHB p.113); a swarm does not threaten
        // creatures in its square (MM p.316); a creature inside the target's space does not flank it (PHB p.153).
        CharacterTeam threatTeam = moverTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = moverTeam == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController mover = null;
        CharacterController stayer = null;
        CharacterController partner = null;
        GameObject cellObject = null;
        try
        {
            mover = CreateTeamCharacter("SharedMover", moverTeam, 0, 0);
            stayer = CreateTeamCharacter("SharedStayer", threatTeam, 0, 0);
            var all = new List<CharacterController> { mover, stayer };
            stayer.Stats.MaxAttacksOfOpportunity = 1;
            stayer.Stats.AttacksOfOpportunityUsed = 0;

            Assert(stayer.CanMeleeAttackDistance(0) && mover.CanMeleeAttackDistance(0),
                $"{side} shared square: distance 0 is in melee reach for both creatures", ref passed, ref failed);
            Assert(stayer.IsTargetInCurrentWeaponRange(mover) && mover.IsTargetInCurrentWeaponRange(stayer),
                $"{side} shared square: each creature's square-mate is in weapon range", ref passed, ref failed);
            // A longspear's reach profile (the database entry is asserted by AssertThreatBand above).
            var longspear = new ItemData { Name = "Longspear", WeaponCat = WeaponCategory.Melee, ReachSquares = 2, AttackRange = 2, CanAttackAdjacent = false, IsReachWeapon = true };
            Assert(!stayer.CanMeleeAttackDistance(0, longspear) && stayer.CanMeleeAttackDistance(2, longspear),
                $"{side} shared square: a longspear cannot attack into its wielder's own square (PHB p.113)", ref passed, ref failed);

            Assert(ThreatSystem.GetThreatenedSquares(stayer).Contains(Vector2Int.zero),
                $"{side} shared square: the stayer threatens its own square", ref passed, ref failed);
            Assert(CombatUtils.IsThreatening(stayer, mover) && ThreatSystem.GetThreateningEnemies(mover.GridPosition, mover, all).Contains(stayer),
                $"{side} shared square: the stayer threatens its square-mate (spellcasting, ranged attacks and standing up provoke)", ref passed, ref failed);

            // One step out of the shared square into a square the stayer also threatens, and no further.
            var oneStep = new List<Vector2Int> { new Vector2Int(1, 0) };
            List<AoOThreatInfo> aoos = ThreatSystem.AnalyzePathForAoOs(mover, oneStep, all);
            Assert(aoos.Count == 1 && aoos[0].Threatener == stayer && aoos[0].PathIndex == 0,
                $"{side} shared square: leaving it with a one-square move provokes from the stayer (got {aoos.Count})", ref passed, ref failed);
            Assert(ThreatSystem.AnalyzePathForAoOs(mover, oneStep, all, suppressFirstSquareAoO: true).Count == 0,
                $"{side} shared square: a withdraw's first square out of it does not provoke", ref passed, ref failed);

            // A creature inside the target's space threatens it but is not on an opposite side of it.
            partner = CreateTeamCharacter("SharedPartner", moverTeam, 1, 0);
            var trio = new List<CharacterController> { mover, stayer, partner };
            Assert(!CombatUtils.IsAttackerFlanking(mover, stayer, trio, out _) && !CombatUtils.IsAttackerFlanking(partner, stayer, trio, out _),
                $"{side} shared square: a creature in the target's square neither flanks nor gives a flank", ref passed, ref failed);

            stayer.Stats.IsSwarm = true;
            Assert(!ThreatSystem.GetThreatenedSquares(stayer).Contains(Vector2Int.zero),
                $"{side} shared square: a swarm does not threaten creatures in its square (MM p.316)", ref passed, ref failed);
            stayer.Stats.IsSwarm = false;

            // Coup de grace: one distance test for the PC button and highlight, the AI and the resolver (adjacent or
            // own square; not two squares away).
            Assert(mover.IsInCoupDeGraceReach(stayer) && partner.IsInCoupDeGraceReach(stayer),
                $"{side} shared square: coup de grace reaches a square-mate and an adjacent foe", ref passed, ref failed);
            TestHelpers.SetGridPosition(partner, 2, 0);
            Assert(!partner.IsInCoupDeGraceReach(stayer),
                $"{side} shared square: coup de grace does not reach two squares away", ref passed, ref failed);
            TestHelpers.SetGridPosition(partner, 1, 0);

            // A click on a square shared by an ally and an enemy picks the enemy, whichever entered first; a click on
            // one's own square picks the square-mate.
            cellObject = new GameObject("SharedSquareCell");
            SquareCell cell = cellObject.AddComponent<SquareCell>();
            cell.AddOccupant(mover);
            cell.AddOccupant(stayer);
            Assert(cell.GetOccupantOtherThan(partner) == stayer && cell.GetOccupantOtherThan(mover) == stayer
                && cell.GetOccupantOtherThan(stayer) == mover,
                $"{side} shared square: a click picks the enemy in a square shared with an ally, and the square-mate in one's own square", ref passed, ref failed);
            Assert(cell.GetOccupantOtherThan(partner, o => o != stayer) == mover,
                $"{side} shared square: a filtered click falls back to the first valid occupant", ref passed, ref failed);

            // A Large target (2x2, base at the origin) with a Medium creature in its (1,1) square: the creature
            // inside neither flanks nor gives a flank to an ally on the far corner, but two creatures outside on
            // opposite borders still flank (PHB p.153). Without the inside-the-space exclusion the first pair flanks.
            stayer.Stats.CurrentSizeCategory = SizeCategory.Large;
            TestHelpers.SetGridPosition(mover, 1, 1);
            TestHelpers.SetGridPosition(partner, -1, -1);
            Assert(!CombatUtils.IsAttackerFlanking(mover, stayer, trio, out _) && !CombatUtils.IsAttackerFlanking(partner, stayer, trio, out _),
                $"{side} shared square: a creature inside a Large target's space neither flanks nor gives a flank", ref passed, ref failed);
            TestHelpers.SetGridPosition(mover, 2, 0);
            TestHelpers.SetGridPosition(partner, -1, 0);
            Assert(CombatUtils.IsAttackerFlanking(mover, stayer, trio, out _) && CombatUtils.IsAttackerFlanking(partner, stayer, trio, out _),
                $"{side} shared square: two creatures on opposite borders of a Large target flank it", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null, stayer != null ? stayer.gameObject : null,
                partner != null ? partner.gameObject : null, cellObject);
        }
    }

    private static void TestMovementStopsOnlyOnAoOChanges(ref int passed, ref int failed)
    {
        CharacterController mover = null;
        try
        {
            mover = CreateTeamCharacter("StopMover", CharacterTeam.Enemy, 0, 0);

            ThreatSystem.MoverAoOSnapshot before = ThreatSystem.CaptureMoverState(mover);
            Assert(!ThreatSystem.ShouldStopMovementAfterAoO(mover, before, out _),
                "Mover that is unharmed by an AoO keeps moving", ref passed, ref failed);

            mover.ApplyCondition(CombatConditionType.Prone, -1, "AoO trip");
            Assert(ThreatSystem.ShouldStopMovementAfterAoO(mover, before, out string tripReason) && tripReason == "knocked prone",
                "Mover tripped by an AoO stops moving", ref passed, ref failed);

            // Regression: a creature already prone (crawling) is not stopped by being prone.
            ThreatSystem.MoverAoOSnapshot pronebefore = ThreatSystem.CaptureMoverState(mover);
            Assert(!ThreatSystem.ShouldStopMovementAfterAoO(mover, pronebefore, out _),
                "Mover already prone before the AoO is not stopped by Prone", ref passed, ref failed);

            mover.Stats.CurrentHP = 0;
            Assert(ThreatSystem.IsMoverIncapacitated(mover) && ThreatSystem.ShouldStopMovementAfterAoO(mover, pronebefore, out _),
                "Mover dropped to 0 HP by an AoO stops moving", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(mover != null ? mover.gameObject : null);
        }
    }

    private static void TestManeuverAoOProvokers(CharacterTeam attackerTeam, ref int passed, ref int failed)
    {
        CharacterTeam foeTeam = attackerTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = attackerTeam == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController attacker = null;
        CharacterController target = null;
        CharacterController sideFoe = null;
        CharacterController farFoe = null;
        try
        {
            attacker = CreateTeamCharacter("ManeuverAttacker", attackerTeam, 0, 0);
            target = CreateTeamCharacter("ManeuverTarget", foeTeam, 1, 0);
            sideFoe = CreateTeamCharacter("ManeuverSideFoe", foeTeam, 0, 1);
            farFoe = CreateTeamCharacter("ManeuverFarFoe", foeTeam, 6, 6);
            var all = new List<CharacterController> { attacker, target, sideFoe, farFoe };

            List<CharacterController> grapple = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Grapple, all);
            Assert(grapple.Count == 1 && grapple[0] == target,
                $"{side} grapple provokes from the target only", ref passed, ref failed);

            List<CharacterController> sunder = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Sunder, all);
            Assert(sunder.Count == 1 && sunder[0] == target,
                $"{side} sunder provokes from the target only", ref passed, ref failed);

            List<CharacterController> coup = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.CoupDeGrace, all);
            Assert(coup.Count == 2 && coup.Contains(target) && coup.Contains(sideFoe) && !coup.Contains(farFoe),
                $"{side} coup de grace provokes from every threatening enemy (got {coup.Count})", ref passed, ref failed);

            // CMB-014: trip and disarm provoke from the target (PHB p.155, p.158).
            List<CharacterController> trip = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Trip, all);
            Assert(trip.Count == 1 && trip[0] == target,
                $"{side} trip provokes from the target only", ref passed, ref failed);

            List<CharacterController> disarm = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Disarm, all);
            Assert(disarm.Count == 1 && disarm[0] == target,
                $"{side} disarm provokes from the target only", ref passed, ref failed);

            // Regression: a target that does not threaten the attacker (e.g. out of reach of a
            // reach-weapon disarm) gets no initiation AoO (PHB p.137).
            Assert(ThreatSystem.GetManeuverAoOProvokers(attacker, farFoe, SpecialAttackType.Disarm, all).Count == 0
                && ThreatSystem.GetManeuverAoOProvokers(attacker, farFoe, SpecialAttackType.Trip, all).Count == 0
                && ThreatSystem.GetManeuverAoOProvokers(attacker, farFoe, SpecialAttackType.Sunder, all).Count == 0,
                $"{side} a target that does not threaten the attacker gets no maneuver AoO", ref passed, ref failed);

            // Bull rush provokes from every enemy that threatens the attacker (PHB p.154).
            List<CharacterController> bullRush = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.BullRushAttack, all);
            Assert(bullRush.Count == 2 && bullRush.Contains(target) && bullRush.Contains(sideFoe) && !bullRush.Contains(farFoe),
                $"{side} bull rush provokes from every threatening enemy (got {bullRush.Count})", ref passed, ref failed);

            // A charge bull rush: entering the defender's space is the bull rush's own provocation
            // (PHB p.154, owner decision 2026-10-07), so an enemy that already made a movement AoO
            // during the charge takes another if it has one left this round (Combat Reflexes); with
            // its one AoO spent it takes none (PHB p.137).
            sideFoe.Stats.AttacksOfOpportunityUsed = 1;
            sideFoe.Stats.MaxAttacksOfOpportunity = 1;
            List<CharacterController> chargeSpent = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.BullRushCharge, all);
            Assert(chargeSpent.Count == 1 && chargeSpent[0] == target,
                $"{side} charge bull rush: an enemy whose one AoO went on the charge move gets none at the entry", ref passed, ref failed);
            sideFoe.Stats.MaxAttacksOfOpportunity = 2;
            List<CharacterController> chargeReflexes = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.BullRushCharge, all);
            Assert(chargeReflexes.Count == 2 && chargeReflexes.Contains(target) && chargeReflexes.Contains(sideFoe),
                $"{side} charge bull rush: an enemy with an AoO left after its charge-move AoO takes one at the entry (PHB p.154)", ref passed, ref failed);
            sideFoe.Stats.AttacksOfOpportunityUsed = 0;
            sideFoe.Stats.MaxAttacksOfOpportunity = 1;

            attacker.Stats.Feats.Add("Improved Grapple");
            attacker.Stats.Feats.Add("Improved Sunder");
            attacker.Stats.Feats.Add("Improved Trip");
            attacker.Stats.Feats.Add("Improved Disarm");
            attacker.Stats.Feats.Add("Improved Bull Rush");
            Assert(ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Grapple, all).Count == 0
                && ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Sunder, all).Count == 0,
                $"{side} Improved Grapple and Improved Sunder remove the initiation AoO", ref passed, ref failed);
            Assert(ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Trip, all).Count == 0
                && ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Disarm, all).Count == 0,
                $"{side} Improved Trip and Improved Disarm remove the initiation AoO", ref passed, ref failed);

            // Improved Bull Rush spares only the defender's AoO; other threatening enemies still get one.
            List<CharacterController> improvedBullRush = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.BullRushAttack, all);
            Assert(improvedBullRush.Count == 1 && improvedBullRush[0] == sideFoe,
                $"{side} Improved Bull Rush removes only the defender's AoO", ref passed, ref failed);

            // Feint and overrun (own AoO path) are not initiation provokers here.
            Assert(ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Feint, all).Count == 0,
                $"{side} feint does not provoke", ref passed, ref failed);

            // Regression: an enemy with no AoO left this round gets none.
            sideFoe.Stats.AttacksOfOpportunityUsed = sideFoe.Stats.MaxAttacksOfOpportunity;
            List<CharacterController> coupSpent = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.CoupDeGrace, all);
            Assert(coupSpent.Count == 1 && coupSpent[0] == target,
                $"{side} coup de grace skips an enemy whose AoOs are spent", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(
                attacker != null ? attacker.gameObject : null,
                target != null ? target.gameObject : null,
                sideFoe != null ? sideFoe.gameObject : null,
                farFoe != null ? farFoe.gameObject : null);
        }
    }

    private static void TestManeuverAoODisruption(ref int passed, ref int failed)
    {
        var hit = new CombatResult { Hit = true };
        var miss = new CombatResult { Hit = false };
        Assert(ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Grapple, hit)
            && !ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Grapple, miss),
            "A hitting initiation AoO foils a grapple; a miss does not", ref passed, ref failed);
        Assert(!ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.CoupDeGrace, hit),
            "A hitting AoO does not by itself foil a coup de grace", ref passed, ref failed);

        // CMB-014: a disarm fails only if the AoO deals damage (PHB p.155); trip and bull rush
        // are not foiled by the AoO.
        var damagingHit = new CombatResult { Hit = true, Damage = 4 };
        var harmlessHit = new CombatResult { Hit = true, Damage = 6, FinalDamageDealt = 0, DRPrevented = 6 };
        Assert(ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Disarm, damagingHit)
            && !ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Disarm, harmlessHit),
            "A disarm is foiled only by an AoO that deals damage", ref passed, ref failed);
        Assert(!ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Trip, damagingHit)
            && !ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.BullRushAttack, damagingHit),
            "A damaging AoO does not foil a trip or bull rush", ref passed, ref failed);
    }

    // ------------------------------------------------------------------------
    // Prone (CMB-074): standing up is a move action that provokes from every
    // threatening enemy (PHB p.143, Table 8-2), and a prone creature takes no
    // ordinary movement; it stands first or crawls 5 ft (PHB p.142). The
    // ResolveStandUpFromProne coroutine needs Play mode; these check the rules
    // it and the AI stand-up step rely on, for an NPC and a PC alike.
    // ------------------------------------------------------------------------

    private static void TestStandUpAoOProvokers(CharacterTeam actorTeam, ref int passed, ref int failed)
    {
        CharacterTeam foeTeam = actorTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = actorTeam == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController actor = null;
        CharacterController adjacentFoe = null;
        CharacterController diagonalFoe = null;
        CharacterController farFoe = null;
        CharacterController ally = null;
        CharacterController sideFoe = null;
        try
        {
            actor = CreateTeamCharacter("StandUpActor", actorTeam, 0, 0);
            adjacentFoe = CreateTeamCharacter("StandUpAdjacentFoe", foeTeam, 1, 0);
            diagonalFoe = CreateTeamCharacter("StandUpDiagonalFoe", foeTeam, 1, 1);
            farFoe = CreateTeamCharacter("StandUpFarFoe", foeTeam, 6, 6);
            ally = CreateTeamCharacter("StandUpAlly", actorTeam, 0, 1);
            actor.ApplyCondition(CombatConditionType.Prone, -1, "Trip");
            var all = new List<CharacterController> { actor, adjacentFoe, diagonalFoe, farFoe, ally };

            List<CharacterController> provokers = ThreatSystem.GetStandUpAoOProvokers(actor, all);
            Assert(provokers.Count == 2 && provokers.Contains(adjacentFoe) && provokers.Contains(diagonalFoe)
                && !provokers.Contains(farFoe) && !provokers.Contains(ally),
                $"{side} standing up provokes from every threatening enemy and no one else (got {provokers.Count})",
                ref passed, ref failed);

            // Regression: an enemy with no AoO left this round gets none.
            diagonalFoe.Stats.AttacksOfOpportunityUsed = diagonalFoe.Stats.MaxAttacksOfOpportunity;
            List<CharacterController> spent = ThreatSystem.GetStandUpAoOProvokers(actor, all);
            Assert(spent.Count == 1 && spent[0] == adjacentFoe,
                $"{side} standing up skips an enemy whose AoOs are spent", ref passed, ref failed);

            // A Large creature provokes from an enemy that threatens only its far squares.
            sideFoe = CreateTeamCharacter("StandUpSideFoe", foeTeam, 2, 1);
            var sideOnly = new List<CharacterController> { actor, sideFoe };
            Assert(ThreatSystem.GetStandUpAoOProvokers(actor, sideOnly).Count == 0,
                $"{side} Medium creature standing up is not threatened from two squares away", ref passed, ref failed);
            actor.Stats.CurrentSizeCategory = SizeCategory.Large;
            List<CharacterController> large = ThreatSystem.GetStandUpAoOProvokers(actor, sideOnly);
            Assert(large.Count == 1 && large[0] == sideFoe,
                $"{side} Large creature standing up provokes from an enemy threatening any square it occupies (got {large.Count})",
                ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(
                actor != null ? actor.gameObject : null,
                adjacentFoe != null ? adjacentFoe.gameObject : null,
                diagonalFoe != null ? diagonalFoe.gameObject : null,
                farFoe != null ? farFoe.gameObject : null,
                ally != null ? ally.gameObject : null,
                sideFoe != null ? sideFoe.gameObject : null);
        }
    }

    private static void TestProneMovementAndStandUpRules(CharacterTeam actorTeam, ref int passed, ref int failed)
    {
        string side = actorTeam == CharacterTeam.Player ? "PC" : "NPC";
        GameObject gmObject = null;
        CharacterController actor = null;
        try
        {
            gmObject = new GameObject("ProneRulesTest_GameManager");
            GameManager gm = gmObject.AddComponent<GameManager>();
            actor = CreateTeamCharacter("ProneActor", actorTeam, 2, 2);

            int standingRange = gm.GetCurrentMoveRangeSquares(actor);
            Assert(standingRange > 0 && standingRange == actor.Stats.MoveRange,
                $"{side} standing creature moves its full speed (got {standingRange})", ref passed, ref failed);
            Assert(gm.GetStandUpDisabledReason(actor) == "Not prone",
                $"{side} standing creature has nothing to stand up from", ref passed, ref failed);

            actor.ApplyCondition(CombatConditionType.Prone, -1, "Trip");
            Assert(gm.GetCurrentMoveRangeSquares(actor) == 0,
                $"{side} prone creature has no ordinary movement (stand up or crawl instead)", ref passed, ref failed);
            Assert(actor.Stats.MoveRange == standingRange,
                $"{side} prone creature's speed itself is unchanged", ref passed, ref failed);
            Assert(string.IsNullOrEmpty(gm.GetStandUpDisabledReason(actor)),
                $"{side} prone creature with its move action can stand up", ref passed, ref failed);

            // Standing up after a move needs the standard action converted to a move.
            actor.Actions.UseMoveAction();
            Assert(string.IsNullOrEmpty(gm.GetStandUpDisabledReason(actor)),
                $"{side} prone creature can still stand up with its standard action as a move", ref passed, ref failed);
            actor.Actions.UseStandardAction();
            Assert(!string.IsNullOrEmpty(gm.GetStandUpDisabledReason(actor)),
                $"{side} prone creature with no move or standard action left cannot stand up", ref passed, ref failed);

            // Regression: once it stands, its full movement is back.
            actor.RemoveCondition(CombatConditionType.Prone);
            Assert(gm.GetCurrentMoveRangeSquares(actor) == standingRange,
                $"{side} creature that stood up moves its full speed again", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(actor != null ? actor.gameObject : null, gmObject);
        }
    }

    private static void AssertThreatBand(string itemId, int expectedMin, int expectedMax, ref int passed, ref int failed)
    {
        ItemData item = ItemDatabase.Get(itemId);
        if (item == null)
        {
            failed++;
            Debug.LogError($"[FlankReachTest] FAIL: missing item {itemId}");
            return;
        }

        int maxReach = Mathf.Max(1, item.ReachSquares > 0 ? item.ReachSquares : item.AttackRange);
        int minReach = item.CanAttackAdjacent ? 1 : (maxReach >= 2 ? 2 : 1);

        bool ok = minReach == expectedMin && maxReach == expectedMax;
        Assert(ok, $"{item.Name} threat band {minReach}-{maxReach} (expected {expectedMin}-{expectedMax})", ref passed, ref failed);
    }

    private static void Assert(bool condition, string label, ref int passed, ref int failed)
    {
        if (condition)
        {
            passed++;
            Debug.Log($"[FlankReachTest] PASS: {label}");
        }
        else
        {
            failed++;
            Debug.LogError($"[FlankReachTest] FAIL: {label}");
        }
    }
}

}
