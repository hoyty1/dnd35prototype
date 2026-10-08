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
/// and reach into one's own square (a square-mate is threatened and may be attacked, CMB-131).
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
        // turn start (the round boundary) clears it; a bull rush push (forcedMovement) ignores it (CMB-150).
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

            List<AoOThreatInfo> pushed = ThreatSystem.AnalyzePathForAoOs(mover, path, all, forcedMovement: true);
            Assert(pushed.Count == 2, $"{side} round record: forced movement (a bull rush push) ignores the record (got {pushed.Count})",
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
