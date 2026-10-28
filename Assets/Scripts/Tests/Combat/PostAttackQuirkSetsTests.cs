using System;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The post-attack hook's quirk sets (batch 12 step 13, docs/designs/pc_npc_parity_batch12.md; the CastCoreStageOrderTests
/// pattern): every attack origin A-J keeps today's differences from GameManager.OnAttackResolved's order as named
/// <see cref="PostAttackQuirk"/> bits, pinned here so that step 14 (the after-attack effects for every step) and step 15
/// (Cleave everywhere) remove them on purpose. Also the decider factory (AttackDeciders.For: the IsControllable read and
/// its override) and the intent's mode mapping. Pure: no scene, no dice; runs in edit or Play mode.
/// </summary>
public static class PostAttackQuirkSetsTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== POST-ATTACK QUIRK SETS TESTS (batch 12 step 13) ==========");

        TestEveryOriginHasASet();
        TestOriginSets();
        TestCleaveOnlyOnPcSingle();
        TestPounceSet();
        TestDeciderFactory();
        TestIntentModeMapping();

        Debug.Log($"========== POST-ATTACK QUIRK SETS TESTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string name, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {name}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {name}{(string.IsNullOrEmpty(detail) ? "" : " - " + detail)}");
        }
    }

    private static void TestEveryOriginHasASet()
    {
        foreach (AttackOrigin origin in Enum.GetValues(typeof(AttackOrigin)))
            Assert(PostAttackQuirkSets.For(origin) != PostAttackQuirk.None,
                $"{origin} has a quirk set", PostAttackQuirkSets.For(origin).ToString());
    }

    private static void Pin(AttackOrigin origin, PostAttackQuirk expected)
    {
        PostAttackQuirk actual = PostAttackQuirkSets.For(origin);
        Assert(actual == expected, $"{origin}: {expected}", "actual " + actual);
    }

    private static void TestOriginSets()
    {
        // A: the PC single attack. Trip, thrown drop and ammunition before its log; Cleave and Improved Grab after the
        // combat-end check (the only origin with Cleave).
        Pin(AttackOrigin.PcSingle, PostAttackQuirk.FreeTripBeforeLog | PostAttackQuirk.ThrownAndAmmoBeforeLog
            | PostAttackQuirk.CleaveAfterCombatEnd | PostAttackQuirk.GrabAfterCombatEnd);

        // B: a PC sequence step. No grab (CMB-097).
        Pin(AttackOrigin.PcSequence, PostAttackQuirk.NoImprovedGrab | PostAttackQuirk.FreeTripBeforeLog
            | PostAttackQuirk.ThrownAndAmmoBeforeLog | PostAttackQuirk.NoCleave);

        // C: the Full Attack button. No grab, ranged AoO, ammunition or thrown drop (CMB-019); reactions by the sequence's mode.
        Pin(AttackOrigin.PcFullAttackButton, PostAttackQuirk.NoImprovedGrab | PostAttackQuirk.NoRangedAttackAoO | PostAttackQuirk.NoThrownDrop
            | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.ReactionsBySequenceMode | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave);

        // D: the legacy two-weapon full attack. No trip, thrown drop or grab; Concentration on the summed damage.
        Pin(AttackOrigin.PcDualWield, PostAttackQuirk.NoFreeTrip | PostAttackQuirk.NoThrownDrop | PostAttackQuirk.NoAmmoSpend
            | PostAttackQuirk.NoImprovedGrab | PostAttackQuirk.NoRangedAttackAoO | PostAttackQuirk.ConcentrationSummed | PostAttackQuirk.NoCleave);

        // E: the off-hand attack. No trip, reactions or grab; its thrown drop comes after the caller's combat-end check.
        Pin(AttackOrigin.PcOffHand, PostAttackQuirk.NoFreeTrip | PostAttackQuirk.NoReactions | PostAttackQuirk.NoImprovedGrab
            | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.ThrownDropByCaller | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave);

        // F: an NPC melee step. Its own frame ends the combat (rules/combat-end-* expects PerformNPCMeleeAttackSequence).
        Pin(AttackOrigin.NpcMelee, PostAttackQuirk.NoThrownDrop | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.SummonCleanupWhenDead
            | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave);

        // G: the NPC ranged full attack. No ammunition or thrown drop; Concentration summed; the grab after all attacks.
        Pin(AttackOrigin.NpcRangedFull, PostAttackQuirk.NoThrownDrop | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.ConcentrationSummed
            | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave);

        // H: the NPC ranged single attack. No ammunition (CMB-019).
        Pin(AttackOrigin.NpcRangedSingle, PostAttackQuirk.NoThrownDrop | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.FreeTripBeforeLog
            | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave);

        // I: the AoO. No grab (CMB-170), Cleave or fear break (CMB-090); the callers do Concentration, summon cleanup and combat end.
        Pin(AttackOrigin.AoO, PostAttackQuirk.NoImprovedGrab | PostAttackQuirk.NoCleave | PostAttackQuirk.NoFearBreak | PostAttackQuirk.NoThrownDrop
            | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.FreeTripBeforeLog | PostAttackQuirk.TripAftermathAfterReactions
            | PostAttackQuirk.ReactionsOnAnyHit | PostAttackQuirk.NoConcentration | PostAttackQuirk.NoSummonCleanup | PostAttackQuirk.CombatEndByCaller);

        // J: the charge's single attack. The charge's one grab attempt; no summon cleanup; the charge ends the combat.
        Pin(AttackOrigin.Charge, PostAttackQuirk.NoThrownDrop | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.FreeTripBeforeLog
            | PostAttackQuirk.ChargeGrab | PostAttackQuirk.NoSummonCleanup | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave);
    }

    private static void TestCleaveOnlyOnPcSingle()
    {
        foreach (AttackOrigin origin in Enum.GetValues(typeof(AttackOrigin)))
        {
            bool noCleave = PostAttackQuirkSets.Has(PostAttackQuirkSets.For(origin), PostAttackQuirk.NoCleave);
            Assert(noCleave == (origin != AttackOrigin.PcSingle), $"{origin}: Cleave {(noCleave ? "off" : "on")}");
        }
        Assert(PostAttackQuirkSets.Has(PostAttackQuirkSets.For(AttackOrigin.Charge, pounce: true), PostAttackQuirk.NoCleave), "Charge pounce: no Cleave");
    }

    private static void TestPounceSet()
    {
        // J (the pounce, CMB-169): the grab after all attacks, each hit's Concentration inside the grab loop.
        PostAttackQuirk pounce = PostAttackQuirkSets.For(AttackOrigin.Charge, pounce: true);
        Assert(pounce == (PostAttackQuirk.NoThrownDrop | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.ConcentrationWithGrab
                          | PostAttackQuirk.ChargeGrab | PostAttackQuirk.NoSummonCleanup | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave),
            "Charge pounce set", pounce.ToString());
        Assert(!PostAttackQuirkSets.Has(pounce, PostAttackQuirk.FreeTripBeforeLog), "Charge pounce: the free trip after the reactions");

        // A combat-end check stays its caller's wherever the caller's frame names the end in a trace.
        foreach (AttackOrigin origin in new[] { AttackOrigin.PcSingle, AttackOrigin.PcSequence, AttackOrigin.PcDualWield })
            Assert(!PostAttackQuirkSets.Has(PostAttackQuirkSets.For(origin), PostAttackQuirk.CombatEndByCaller), $"{origin}: the hook checks combat end");
    }

    private static void TestDeciderFactory()
    {
        Assert(AttackDeciders.For(null) is AiAttackDecider, "no creature: the AI decider");
        Assert(AttackDeciders.For(null, ActorSource.Ui) is UiAttackDecider, "override Ui: the UI decider");
        Assert(AttackDeciders.For(null, ActorSource.AI) is AiAttackDecider, "override AI: the AI decider");
        Assert(AiAttackDecider.Instance.IsSynchronous && !UiAttackDecider.Instance.IsSynchronous, "only the AI decider is synchronous");

        bool declined = true;
        AiAttackDecider.Instance.ChooseAttackedReaction(null, null, null, "acid spray", b => declined = !b);
        CharacterController picked = null;
        bool answered = false;
        Coroutine wait = AiAttackDecider.Instance.ChooseCleaveTarget(null, null, new System.Collections.Generic.List<CharacterController>(), c => { picked = c; answered = true; });
        Assert(declined && answered && picked == null && wait == null, "the unwired choices answer at once (step 15 wires them)");
    }

    private static void TestIntentModeMapping()
    {
        foreach (GameManager.AttackType type in Enum.GetValues(typeof(GameManager.AttackType)))
            Assert(AttackIntent.ToAttackType(AttackIntent.ToMode(type)) == type, $"intent mode round-trips {type}");
        Assert(AttackIntent.ToAttackType(null) == GameManager.AttackType.Melee, "no mode reads as Melee");
        Assert(new AttackIntent { Mode = AttackMode.Thrown }.ThrownSelected && !new AttackIntent().ThrownSelected, "ThrownSelected reads the mode");

        AttackIntent ai = AttackIntent.ForAI(null, null, null);
        Assert(ai.Quirks == AttackIntentQuirk.NpcIntentReadsUiThrownGlobal && ai.Mode == null && ai.LegacyUiMode == AttackMode.Melee,
            "ForAI carries NpcIntentReadsUiThrownGlobal, its PC-selection copy in LegacyUiMode (no GameManager: Melee) and no Mode");

        // The legacy copy reaches the thrown-mode test only, and only under its quirk bit; the gate's Mode stays null.
        var npcAfterPcThrow = new AttackIntent { LegacyUiMode = AttackMode.Thrown, Quirks = AttackIntentQuirk.NpcIntentReadsUiThrownGlobal };
        Assert(npcAfterPcThrow.ThrownSelected && npcAfterPcThrow.Mode == null,
            "an NPC intent after a PC throw: ThrownSelected through the quirk, Mode null for the legality gate");
        Assert(!new AttackIntent { LegacyUiMode = AttackMode.Thrown }.ThrownSelected,
            "LegacyUiMode without the quirk bit is not read");
        Assert(!new AttackIntent { Mode = AttackMode.Melee, LegacyUiMode = AttackMode.Thrown, Quirks = AttackIntentQuirk.NpcIntentReadsUiThrownGlobal }.ThrownSelected,
            "a set Mode wins over the legacy copy");
        AttackIntent ui = AttackIntent.ForUi(null, null);
        Assert(ui.Quirks == AttackIntentQuirk.None && ui.Mode == AttackMode.Melee && ui.Weapon == null, "ForUi without a GameManager: Melee, no weapon");
    }
}
}
