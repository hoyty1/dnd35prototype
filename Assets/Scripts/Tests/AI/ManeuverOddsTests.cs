#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using DND35.AI.Valuation;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 8 (docs/designs/personalities_batch14.md "Maneuver scorer" and D6): the maneuver odds
/// (DND35.AI.Valuation.ManeuverOdds) and the shared modifiers the rolls and the estimate both read.
/// <list type="bullet">
/// <item>The exact helpers: a touch attack keeps the natural 1 and 20 (PHB p.134), a sunder's "at least" comparison.</item>
/// <item>Trip (PHB p.158): the estimate against 3,000 seeded rolls of the real trip (CharacterController.ExecuteSpecialAttack,
/// the touch attack and the opposed check), within 0.03, for an even and a lopsided pair.</item>
/// <item>Disarm (PHB p.155), sunder (p.158) and grapple (p.155-156) by shared-modifier identity: the terms the estimate
/// reads equal the ones the roll added (the roll's total minus its d20), an off-hand disarm's held item and a grapple in
/// place of a secondary natural attack (its check without the -5, CMB-102) included.</item>
/// <item>An AoO that deals damage cancels a disarm (ThreatSystem.DoesManeuverAoODisruptAttempt; a hit DR absorbs does not),
/// and the odds price each provoker: a disarm's chance not to be foiled is 1 minus the AoO's hit chance, a trip's is 1.</item>
/// <item>The step BAB the odds use is the PC menu's formula with the step hook's intent (GameManager.GetManeuverStepBab,
/// ManeuverStepIntentForAI null outside the hook).</item>
/// </list>
/// Play mode (CharacterController.Init, TST-009). Random.state is saved and restored around the seeded rolls.
/// </summary>
public static class ManeuverOddsTests
{
    private static int _passed;
    private static int _failed;

    private const int TripTrials = 3000;
    private const float TripTolerance = 0.03f;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== MANEUVER ODDS TESTS (batch 14 step 8) ======");
        TestHelpers.EnsureCoreDatabasesInitialized();

        var made = new List<CharacterController>();
        Random.State saved = Random.state;
        try
        {
            TestExactHelpers();
            TestTripAgainstRealRolls(made, "even", tripperStr: 14, targetStr: 14, targetDex: 12, seed: 20261123);
            TestTripAgainstRealRolls(made, "lopsided", tripperStr: 18, targetStr: 8, targetDex: 10, seed: 20261124);
            TestDisarmIdentity(made);
            TestSunderIdentity(made);
            TestGrappleIdentity(made);
            TestNaturalSecondaryGrappleIdentity(made);
            TestAoOCancelsDisarm(made);
            TestStepBab(made);
        }
        finally
        {
            Random.state = saved;
            // A won grapple links the pair and places the grappler on the scene grid: release both before destroying,
            // so no later suite in the pass finds a destroyed occupant (as SaveRollRulesTests and NpcSlotReuseTests do).
            SquareGrid sceneGrid = GameManager.Instance != null ? GameManager.Instance.Grid : null;
            foreach (CharacterController c in made)
            {
                if (c == null)
                    continue;
                c.ReleaseGrappleState("test cleanup");
                if (sceneGrid != null)
                    sceneGrid.ClearCreatureOccupancy(c);
            }
            foreach (CharacterController c in made)
                if (c != null)
                    Object.DestroyImmediate(c.gameObject);
        }

        Debug.Log($"====== Maneuver Odds Results: {_passed} passed, {_failed} failed ======");
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
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    private static CharacterController Make(List<CharacterController> made, string name, int str = 14, int dex = 12, int bab = 3,
        CharacterTeam team = CharacterTeam.Enemy, int x = 0, int y = 0)
    {
        CharacterController c = TestHelpers.CreateCharacter(name: name, str: str, dex: dex, bab: bab);
        // Stats.BaseAttackBonus follows the class for a classed character (CHR-068): fix it.
        c.Stats.BaseAttackBonusOverride = bab;
        c.SetTeam(team);
        TestHelpers.SetGridPosition(c, x, y);
        ThreatSystem.ResetAoOForTurn(c);
        made.Add(c);
        return c;
    }

    private static ItemData Equip(CharacterController c, ItemID id, EquipSlot slot = EquipSlot.RightHand)
    {
        ItemData item = ItemDatabase.CloneItem(id);
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        inv.CharacterInventory.DirectEquip(item, slot);
        inv.CharacterInventory.RecalculateStats();
        return item;
    }

    private static readonly List<CharacterController> NoOne = new List<CharacterController>();

    private static void TestExactHelpers()
    {
        Assert(ManeuverOdds.TouchHitChance(0, 11) == 0.5f && ManeuverOdds.TouchHitChance(-30, 5) == 0.05f && ManeuverOdds.TouchHitChance(30, 5) == 0.95f,
            "a maneuver touch attack hits on 11+ at +0 against touch AC 11, a natural 20 always hits and a natural 1 always misses (PHB p.134)");
        Assert(ManeuverOdds.ChanceAtLeast(0, 0) == 210f / 400f && ManeuverOdds.ChanceAtLeast(20, 0) == 1f && ManeuverOdds.ChanceAtLeast(-19, 0) == 1f / 400f && ManeuverOdds.ChanceAtLeast(-20, 0) == 0f,
            "the sunder comparison (attacker total at least the defender's) is exact over the 400 roll pairs, with no natural 20 rule (PHB p.158)");
        Assert(Mathf.Approximately(CharacterController.EstimateOpposedCheckWinChance(0, 0), 0.5f),
            "an even opposed check is 0.5 (ties to the higher modifier, else a reroll; PHB p.64)");
    }

    private static void TestTripAgainstRealRolls(List<CharacterController> made, string label, int tripperStr, int targetStr, int targetDex, int seed)
    {
        CharacterController tripper = Make(made, "MO Tripper " + label, str: tripperStr, bab: 3, team: CharacterTeam.Enemy, x: 0, y: 0);
        CharacterController target = Make(made, "MO Trip Target " + label, str: targetStr, dex: targetDex, bab: 3, team: CharacterTeam.Player, x: 1, y: 0);
        const int bab = 3;

        ManeuverOddsEstimate odds = ManeuverOdds.Estimate(tripper, target, SpecialAttackType.Trip, bab, null, NoOne);
        float expectedTouch = ManeuverOdds.TouchHitChance(tripper.GetManeuverTouchAttackModifierAgainst(target, bab), target.GetManeuverTouchArmorClassAgainst(tripper));
        float expectedCheck = CharacterController.EstimateOpposedCheckWinChance(tripper.GetTripAttackerCheckModifier(), target.GetTripOrOverrunDefenderCheckModifier());
        Assert(odds.Supported && odds.CanSucceed && odds.HasTouchAttack && odds.TouchChance == expectedTouch && odds.CheckChance == expectedCheck
                && odds.NotFoiledChance == 1f && odds.Provokers == 0,
            "the " + label + " trip estimate is the touch chance times the opposed Strength check from the rolls' own getters (PHB p.158)",
            "touch " + odds.TouchChance + "/" + expectedTouch + " check " + odds.CheckChance + "/" + expectedCheck);

        Random.InitState(seed);
        int successes = 0;
        int touchHits = 0;
        for (int i = 0; i < TripTrials; i++)
        {
            SpecialAttackResult r = tripper.ExecuteSpecialAttack(SpecialAttackType.Trip, target, tripAttackBonusOverride: bab);
            if (r != null && r.Success)
                successes++;
            if (r != null && r.Log != null && r.Log.Contains("→ hit."))
                touchHits++;
            target.RemoveCondition(CombatConditionType.Prone);
        }
        float rate = successes / (float)TripTrials;
        float touchRate = touchHits / (float)TripTrials;
        Assert(Mathf.Abs(rate - odds.SuccessChance) <= TripTolerance,
            "the " + label + " trip estimate is within " + TripTolerance + " of " + TripTrials + " seeded rolls of the real trip",
            "estimate " + odds.SuccessChance.ToString("0.000") + " observed " + rate.ToString("0.000"));
        Assert(Mathf.Abs(touchRate - odds.TouchChance) <= TripTolerance,
            "the " + label + " trip's touch chance is within " + TripTolerance + " of the real touch attacks",
            "estimate " + odds.TouchChance.ToString("0.000") + " observed " + touchRate.ToString("0.000"));
        Assert(odds.CounterPossible == target.CanCounterTrip(tripper, out _)
                && (!odds.CounterPossible || odds.CounterChance == CharacterController.EstimateOpposedCheckWinChance(target.GetTripAttackerCheckModifier(), tripper.GetTripOrOverrunDefenderCheckModifier())),
            "the " + label + " trip's counter-trip odds are the defender's own trip check against the tripper (PHB p.158)");
    }

    private static readonly MethodInfo RollDisarm = typeof(CharacterController).GetMethod("RollDisarmCheck", BindingFlags.Static | BindingFlags.NonPublic);

    private static int RolledModifier(object check, string side)
    {
        System.Type type = check.GetType();
        return (int)type.GetField(side + "Total").GetValue(check) - (int)type.GetField(side + "Roll").GetValue(check);
    }

    private static void TestDisarmIdentity(List<CharacterController> made)
    {
        CharacterController attacker = Make(made, "MO Disarmer", str: 16, bab: 4, team: CharacterTeam.Enemy, x: 0, y: 0);
        CharacterController defender = Make(made, "MO Disarm Target", str: 12, bab: 2, team: CharacterTeam.Player, x: 1, y: 0);
        CharacterController emptyHanded = Make(made, "MO Disarm Empty", team: CharacterTeam.Player, x: 0, y: 1);
        ItemData greatsword = Equip(attacker, ItemID.WeaponGreatsword);
        ItemData dagger = Equip(defender, ItemID.WeaponDagger);

        CharacterController.DisarmCheckModifiers shared = CharacterController.GetDisarmCheckModifiers(attacker, defender, greatsword, dagger, 0, 2);
        Assert(RollDisarm != null, "RollDisarmCheck exists (reflection, TST-006)");
        if (RollDisarm != null)
        {
            object rolled = RollDisarm.Invoke(null, new object[] { attacker, defender, greatsword, dagger, EquipSlot.RightHand, 0, string.Empty, 2, 0 });
            Assert(RolledModifier(rolled, "Attacker") == shared.AttackerModifier && RolledModifier(rolled, "Defender") == shared.DefenderModifier,
                "RollDisarmCheck adds exactly GetDisarmCheckModifiers' terms to each d20 (PHB p.155)",
                RolledModifier(rolled, "Attacker") + "/" + shared.AttackerModifier + ", " + RolledModifier(rolled, "Defender") + "/" + shared.DefenderModifier);
        }
        Assert(shared.AttackerHeldItem == 4 && shared.DefenderHeldItem == -4 && shared.AttackerBab == 2,
            "a greatsword disarms at +4 and a dagger resists at -4 (PHB p.155), at the step's BAB");

        bool got = attacker.TryGetDisarmCheckModifiersAgainst(defender, EquipSlot.RightHand, 2, out CharacterController.DisarmCheckModifiers viaTarget);
        Assert(got && viaTarget.AttackerModifier == shared.AttackerModifier && viaTarget.DefenderModifier == shared.DefenderModifier,
            "the target lookup the estimate uses gives the same terms (its main-hand weapon, the held item, no locked gauntlet)");

        // An off-hand disarm rolls with the hand the step paid (ResolveDisarm's attackerWeaponOverride): a light dagger at -4.
        ItemData offHandDagger = ItemDatabase.CloneItem(ItemID.WeaponDagger);
        CharacterController.DisarmCheckModifiers offShared = CharacterController.GetDisarmCheckModifiers(attacker, defender, offHandDagger, dagger, 0, 2);
        bool offGot = attacker.TryGetDisarmCheckModifiersAgainst(defender, EquipSlot.RightHand, 2, out CharacterController.DisarmCheckModifiers offVia, offHandDagger);
        bool offRolledMatch = false;
        if (RollDisarm != null)
        {
            object offRolled = RollDisarm.Invoke(null, new object[] { attacker, defender, offHandDagger, dagger, EquipSlot.RightHand, 0, string.Empty, 2, 0 });
            offRolledMatch = RolledModifier(offRolled, "Attacker") == offVia.AttackerModifier && RolledModifier(offRolled, "Defender") == offVia.DefenderModifier;
        }
        ManeuverOddsEstimate offOdds = ManeuverOdds.Estimate(attacker, defender, SpecialAttackType.Disarm, 2, EquipSlot.RightHand, NoOne, offHandDagger);
        Assert(offGot && offRolledMatch && offVia.AttackerHeldItem == -4 && offVia.AttackerModifier == offShared.AttackerModifier
                && offOdds.CheckChance == CharacterController.EstimateOpposedCheckWinChance(offVia.AttackerModifier, offVia.DefenderModifier),
            "an off-hand disarm is priced with the off-hand dagger (-4, PHB p.155), as RollDisarmCheck rolls it",
            "got " + offGot + " rolled match " + offRolledMatch + " held item " + offVia.AttackerHeldItem);

        ManeuverOddsEstimate odds = ManeuverOdds.Estimate(attacker, defender, SpecialAttackType.Disarm, 2, EquipSlot.RightHand, NoOne);
        Assert(odds.Supported && odds.CanSucceed && !odds.HasTouchAttack && odds.TouchChance == 1f
                && odds.CheckChance == CharacterController.EstimateOpposedCheckWinChance(shared.AttackerModifier, shared.DefenderModifier),
            "the disarm estimate is the opposed attack rolls from the shared terms");
        defender.TryGetDisarmCheckModifiersAgainst(attacker, null, null, out CharacterController.DisarmCheckModifiers counter);
        Assert(odds.CounterPossible && odds.CounterChance == CharacterController.EstimateOpposedCheckWinChance(counter.AttackerModifier, counter.DefenderModifier),
            "a lost disarm lets the defender disarm back, priced from its own terms (PHB p.155)");
        attacker.Stats.Feats.Add("Improved Disarm");
        ManeuverOddsEstimate improved = ManeuverOdds.Estimate(attacker, defender, SpecialAttackType.Disarm, 2, EquipSlot.RightHand, NoOne);
        Assert(!improved.CounterPossible && improved.CheckChance > odds.CheckChance,
            "Improved Disarm adds +4 and denies the counter-disarm (PHB p.95)");
        attacker.Stats.Feats.Remove("Improved Disarm");

        Assert(!ManeuverOdds.Estimate(attacker, emptyHanded, SpecialAttackType.Disarm, 2, null, NoOne).CanSucceed,
            "a target with nothing in hand cannot be disarmed (the attempt fails at once)");

        // The real disarm (ResolveDisarm through RollDisarmCheck) adds the same terms.
        Random.InitState(4242);
        SpecialAttackResult real = attacker.ExecuteSpecialAttack(SpecialAttackType.Disarm, defender, EquipSlot.RightHand, disarmAttackBonusOverride: 2,
            disarmAttackerWeaponOverride: greatsword);
        Assert(real != null && real.CheckTotal - real.CheckRoll == shared.AttackerModifier && real.OpposedTotal - real.OpposedRoll == shared.DefenderModifier,
            "the real disarm rolls the estimate's terms", real != null ? real.Log : "no result");

        // A won disarm (or counter-disarm) drops a weapon in a square: take it off the grid again.
        SquareGrid grid = GameManager.Instance != null ? GameManager.Instance.Grid : SquareGrid.Instance;
        foreach (CharacterController c in new[] { attacker, defender })
        {
            SquareCell cell = grid != null ? grid.GetCell(c.GridPosition) : null;
            if (cell == null)
                continue;
            cell.RemoveGroundItem(dagger);
            cell.RemoveGroundItem(greatsword);
        }
    }

    private static void TestSunderIdentity(List<CharacterController> made)
    {
        CharacterController attacker = Make(made, "MO Sunderer", str: 16, bab: 4, team: CharacterTeam.Enemy, x: 0, y: 0);
        CharacterController defender = Make(made, "MO Sunder Target", str: 12, bab: 2, team: CharacterTeam.Player, x: 1, y: 0);
        CharacterController brawler = Make(made, "MO Sunder Brawler", str: 14, bab: 2, team: CharacterTeam.Enemy, x: 0, y: 1);
        ItemData axe = Equip(attacker, ItemID.WeaponGreataxe);
        Equip(defender, ItemID.WeaponLongsword);

        bool got = attacker.TryGetSunderCheckModifiersAgainst(defender, EquipSlot.RightHand, 3, out CharacterController.SunderCheckModifiers m);
        Assert(got && m.Handedness == 4 && m.AttackerBab == 3 && !m.Unarmed && !m.DealsNonlethalDamage,
            "a greataxe sunders at +4 (two-handed, PHB p.158) at the step's BAB", "got " + got + " handedness " + m.Handedness);
        ManeuverOddsEstimate odds = ManeuverOdds.Estimate(attacker, defender, SpecialAttackType.Sunder, 3, EquipSlot.RightHand, NoOne);
        Assert(odds.Supported && odds.CanSucceed && odds.CheckChance == ManeuverOdds.ChanceAtLeast(m.AttackerModifier, m.DefenderModifier) && !odds.CounterPossible,
            "the sunder estimate is the opposed attack rolls from the shared terms, ties to the attacker");

        bool unarmedGot = brawler.TryGetSunderCheckModifiersAgainst(defender, EquipSlot.RightHand, null, out CharacterController.SunderCheckModifiers fist);
        Assert(unarmedGot && fist.Unarmed && fist.Handedness == CharacterController.UnarmedStrikeSunderHandednessModifier
                && ManeuverOdds.Estimate(brawler, defender, SpecialAttackType.Sunder, 2, EquipSlot.RightHand, NoOne).DealsNoDamage == fist.DealsNonlethalDamage,
            "an unarmed sunder rolls as a light weapon (-4) and the estimate flags a nonlethal one (objects are immune, PHB p.165)");

        Random.InitState(5151);
        SpecialAttackResult real = attacker.ExecuteSpecialAttack(SpecialAttackType.Sunder, defender, sunderTargetSlot: EquipSlot.RightHand,
            sunderAttackBonusOverride: 3, sunderAttackerWeaponOverride: axe);
        Assert(real != null && real.CheckTotal - real.CheckRoll == m.AttackerModifier && real.OpposedTotal - real.OpposedRoll == m.DefenderModifier,
            "the real sunder (ResolveSunder) rolls the estimate's terms", real != null ? real.Log : "no result");
    }

    private static void TestGrappleIdentity(List<CharacterController> made)
    {
        const int bab = 12;
        bool sawHit = false;
        bool sawMiss = false;
        bool allMatch = true;
        string detail = null;
        for (int attempt = 0; attempt < 8 && !(sawHit && sawMiss); attempt++)
        {
            // Each attempt on its own squares, so a won grapple's placement never meets an earlier attempt's pair.
            int ay = 2 * attempt;
            CharacterController grappler = Make(made, "MO Grappler " + attempt, str: 16, bab: bab, team: CharacterTeam.Enemy, x: 16, y: ay);
            CharacterController target = Make(made, "MO Grapple Target " + attempt, str: 12, bab: 2, team: CharacterTeam.Player, x: 17, y: ay);
            // A lower BAB on odd attempts so a miss (a natural 1, or a low roll) is seen too.
            int stepBab = attempt % 2 == 0 ? bab : -10;
            int touchMod = grappler.GetManeuverTouchAttackModifierAgainst(target, stepBab);
            int checkMod = grappler.GetGrappleCheckModifierToStart(grappler.GetGrappleCheckBabAfterTouchAttack(stepBab, true));
            int resistMod = target.GetGrappleCheckModifierToResistStart();
            ManeuverOddsEstimate odds = ManeuverOdds.Estimate(grappler, target, SpecialAttackType.Grapple, stepBab, null, NoOne);
            if (odds.TouchChance != ManeuverOdds.TouchHitChance(touchMod, target.GetManeuverTouchArmorClassAgainst(grappler))
                || odds.CheckChance != CharacterController.EstimateOpposedCheckWinChance(checkMod, resistMod) || !odds.CanSucceed)
            {
                allMatch = false;
                detail = "estimate terms differ at attempt " + attempt;
            }

            Random.InitState(6000 + attempt);
            SpecialAttackResult r = grappler.ExecuteSpecialAttack(SpecialAttackType.Grapple, target, grappleAttackBonusOverride: stepBab);
            if (r == null)
            {
                allMatch = false;
                detail = "no grapple result at attempt " + attempt;
                continue;
            }
            if (r.Log != null && r.Log.Contains("Result: Hit!"))
            {
                sawHit = true;
                if (r.CheckTotal - r.CheckRoll != checkMod || r.OpposedTotal - r.OpposedRoll != resistMod)
                {
                    allMatch = false;
                    detail = "grapple checks " + (r.CheckTotal - r.CheckRoll) + "/" + checkMod + ", " + (r.OpposedTotal - r.OpposedRoll) + "/" + resistMod;
                }
            }
            else if (r.Log != null && r.Log.Contains("Result: Miss!"))
            {
                sawMiss = true;
                if (r.CheckTotal - r.CheckRoll != touchMod)
                {
                    allMatch = false;
                    detail = "touch " + (r.CheckTotal - r.CheckRoll) + "/" + touchMod;
                }
            }
            else
            {
                // Neither marker: the log wording changed, so the hit/miss split above no longer works.
                allMatch = false;
                string log = r.Log ?? "(null)";
                detail = "no Result: Hit!/Miss! marker in attempt " + attempt + " log: " + (log.Length > 120 ? log.Substring(0, 120) : log);
            }
        }
        Assert(sawHit && allMatch,
            "the real grapple (ResolveGrapple) rolls the estimate's touch attack and both grapple-check modifiers (PHB p.155-156)", detail);
        Assert(sawMiss,
            "the grapple identity saw a missed touch attack too, so the touch modifier was compared with a real roll",
            "attempts gave hit " + sawHit + ", miss " + sawMiss);

        CharacterController small = Make(made, "MO Small Grappler", team: CharacterTeam.Enemy, x: 3, y: 3);
        CharacterController huge = Make(made, "MO Huge Target", team: CharacterTeam.Player, x: 4, y: 3);
        huge.Stats.SetBaseSizeCategory(SizeCategory.Huge);
        Assert(small.GrappleHoldFailsBySize(huge) && !ManeuverOdds.Estimate(small, huge, SpecialAttackType.Grapple, 3, null, NoOne).CanSucceed,
            "a hold on a target two sizes larger fails automatically, so the estimate cannot succeed (PHB p.156)");
    }

    /// <summary>
    /// A grapple in place of a secondary natural attack (CMB-102, owner ruling 2026-10-08): its touch attack takes the -5
    /// (MM p.312) and its grapple check does not. The estimate runs before the step is committed, as the scorer will, with
    /// a stale record of the last substitute (the primary bite): it must price the claw the step gives up, and equal the
    /// real check once that step is committed.
    /// </summary>
    private static void TestNaturalSecondaryGrappleIdentity(List<CharacterController> made)
    {
        GameManager gm = GameManager.Instance;
        Assert(gm != null, "a GameManager runs for the natural-attack grapple estimate (Play mode)");
        if (gm == null)
            return;

        const int bab = 6;
        CharacterController beast = Make(made, "MO Natural Grappler", str: 16, bab: bab, team: CharacterTeam.Enemy, x: 18, y: 18);
        CharacterController target = Make(made, "MO Natural Grapple Target", str: 12, bab: 2, team: CharacterTeam.Player, x: 19, y: 18);
        beast.Stats.NaturalAttacks.Clear();
        beast.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
        beast.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = false, BonusDamageSource = DamageBonusSource.StrengthHalf });
        beast.StartNewTurn();

        // The bite is given up first (a maneuver in its place), so the next step is a secondary claw and the record is stale.
        bool biteCommitted = beast.TryCommitManeuverSubstituteStep(-1, out int biteBab, out _, out _);
        int stale = beast.ProgressiveAttackPool.LastSubstituteNaturalAttackIndex;
        int stepBab = ManeuverOdds.StepBab(beast);
        int clawIndex = ManeuverOdds.StepNaturalAttackIndex(beast, false);
        int estimateCheckBab = beast.GetGrappleCheckBabAfterTouchAttack(stepBab, true, clawIndex);
        int estimateCheckMod = beast.GetGrappleCheckModifierToStart(estimateCheckBab);
        int resistMod = target.GetGrappleCheckModifierToResistStart();
        ManeuverOddsEstimate odds = ManeuverOdds.Estimate(beast, target, SpecialAttackType.Grapple, stepBab, null, NoOne);
        Assert(beast.GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence && biteCommitted && biteBab == bab && stale == 0
                && clawIndex >= 1 && stepBab == bab - 5 && estimateCheckBab == bab
                && odds.CheckChance == CharacterController.EstimateOpposedCheckWinChance(estimateCheckMod, resistMod),
            "before the claw step is committed, the grapple estimate prices the claw it gives up: touch at BAB -5, check at the full BAB (MM p.312, CMB-102)",
            "stale " + stale + " claw " + clawIndex + " step BAB " + stepBab + " check BAB " + estimateCheckBab);

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == "Touch attack" ? 20 : natural;
            bool clawCommitted = beast.TryCommitManeuverSubstituteStep(clawIndex, out int clawBab, out _, out _);
            Random.InitState(7070);
            SpecialAttackResult r = clawCommitted ? beast.ExecuteSpecialAttack(SpecialAttackType.Grapple, target, grappleAttackBonusOverride: clawBab) : null;
            bool hit = r != null && r.Log != null && r.Log.Contains("Result: Hit!");
            Assert(clawCommitted && clawBab == stepBab && hit && r.CheckTotal - r.CheckRoll == estimateCheckMod && r.OpposedTotal - r.OpposedRoll == resistMod,
                "after the step is committed, the real grapple check adds exactly the estimate's modifier (PHB p.156)",
                r == null ? "no result (committed " + clawCommitted + ")"
                    : "check " + (r.CheckTotal - r.CheckRoll) + "/" + estimateCheckMod + " resist " + (r.OpposedTotal - r.OpposedRoll) + "/" + resistMod + " hit " + hit);
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
        }
    }

    private static void TestAoOCancelsDisarm(List<CharacterController> made)
    {
        var damaging = new CombatResult { Hit = true, FinalDamageDealt = 3 };
        var absorbed = new CombatResult { Hit = true, FinalDamageDealt = 0, DRPrevented = 5 };
        var miss = new CombatResult { Hit = false };
        Assert(ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Disarm, damaging)
                && !ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Disarm, absorbed)
                && !ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Disarm, miss)
                && !ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Trip, damaging),
            "an AoO that deals damage cancels a disarm (PHB p.155); a hit DR absorbs, a miss and any AoO on a trip do not");
        Assert(ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Grapple, absorbed) && ThreatSystem.DoesManeuverAoODisruptAttempt(SpecialAttackType.Sunder, absorbed),
            "a grapple or sunder AoO stops the attempt on any hit, as the core does (CMB-083, an owner question)");

        CharacterController attacker = Make(made, "MO AoO Disarmer", str: 14, bab: 3, team: CharacterTeam.Enemy, x: 10, y: 10);
        CharacterController target = Make(made, "MO AoO Target", str: 14, bab: 3, team: CharacterTeam.Player, x: 11, y: 10);
        Equip(attacker, ItemID.WeaponLongsword);
        Equip(target, ItemID.WeaponLongsword);
        var all = new List<CharacterController> { attacker, target };
        List<CharacterController> provokers = ThreatSystem.GetManeuverAoOProvokers(attacker, target, SpecialAttackType.Disarm, all);
        Assert(provokers.Count == 1 && provokers[0] == target, "a disarm without Improved Disarm provokes from the target (PHB p.155)", provokers.Count + " provokers");

        ManeuverOddsEstimate disarm = ManeuverOdds.Estimate(attacker, target, SpecialAttackType.Disarm, 3, null, all);
        float hit = ThreatSystem.EstimateAoOHitChance(target, attacker);
        Assert(disarm.Provokers == 1 && Mathf.Approximately(disarm.NotFoiledChance, 1f - hit)
                && Mathf.Approximately(disarm.ExpectedAoODamage, ThreatSystem.EstimateExpectedAoODamage(target, attacker))
                && Mathf.Approximately(disarm.SuccessChance, (1f - hit) * disarm.CheckChance),
            "the disarm odds lose the provoker's hit chance (a hit is taken to deal damage) and price its expected damage",
            "notFoiled " + disarm.NotFoiledChance + " hit " + hit);

        ManeuverOddsEstimate trip = ManeuverOdds.Estimate(attacker, target, SpecialAttackType.Trip, 3, null, all);
        Assert(trip.Provokers == 1 && trip.NotFoiledChance == 1f && trip.ExpectedAoODamage > 0f,
            "a trip's AoO costs damage but never stops the trip");

        attacker.Stats.Feats.Add("Improved Disarm");
        ManeuverOddsEstimate improved = ManeuverOdds.Estimate(attacker, target, SpecialAttackType.Disarm, 3, null, all);
        Assert(improved.Provokers == 0 && improved.NotFoiledChance == 1f && improved.ExpectedAoODamage == 0f,
            "Improved Disarm provokes nothing (PHB p.95)");
        attacker.Stats.Feats.Remove("Improved Disarm");
    }

    private static readonly MethodInfo UiStepBab = typeof(GameManager).GetMethod("GetCurrentMainHandManeuverAttackBonusForUI", BindingFlags.Instance | BindingFlags.NonPublic);

    private static void TestStepBab(List<CharacterController> made)
    {
        GameManager gm = GameManager.Instance;
        Assert(gm != null, "a GameManager runs (Play mode)");
        if (gm == null)
            return;
        CharacterController fighter = Make(made, "MO Step Fighter", str: 14, bab: 6, team: CharacterTeam.Enemy, x: 14, y: 14);
        Equip(fighter, ItemID.WeaponLongsword);
        Assert(gm.ManeuverStepIntentForAI == null, "the step intent is null outside the attack sequence's step hook");
        int ai = ManeuverOdds.StepBab(fighter);
        int menu = UiStepBab != null ? (int)UiStepBab.Invoke(gm, new object[] { fighter, false, false }) : int.MinValue;
        Assert(UiStepBab != null && ai == gm.GetManeuverStepBab(fighter, null) && ai == menu,
            "the odds' step BAB is the PC menu's formula (one GetManeuverStepBab) with the step's intent", "ai " + ai + " menu " + menu);
    }
}
}
#endif
