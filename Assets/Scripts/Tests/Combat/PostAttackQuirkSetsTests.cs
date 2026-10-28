using System;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The post-attack hook's quirk sets (batch 12 steps 13 and 14, docs/designs/pc_npc_parity_batch12.md; the
/// CastCoreStageOrderTests pattern): since step 14 every attack origin A-J runs GameManager.OnAttackResolved's stages alike,
/// and only the Cleave <see cref="PostAttackQuirk"/> bits are left, pinned here so that step 15 (Cleave everywhere) removes
/// them on purpose. Also the thrown drop's intent test (AttackIntent.IsThrowOf, GameManager.IsThrowableWeapon), the decider
/// factory (AttackDeciders.For: the IsControllable read and its override) and the intent's mode mapping. Pure: no scene,
/// no dice; runs in edit or Play mode.
/// </summary>
public static class PostAttackQuirkSetsTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== POST-ATTACK QUIRK SETS TESTS (batch 12 steps 13-14) ==========");

        TestEveryOriginHasASet();
        TestOriginSets();
        TestCleaveOnlyOnPcSingle();
        TestOnlyCleaveBitsLeft();
        TestThrowIntent();
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
        // Step 14: every origin runs the same stages (grab, breaks, ammunition, thrown drop, Concentration, reactions,
        // the free trip, the summon cleanup and the combat-end check); only the Cleave bits are left (step 15).
        Pin(AttackOrigin.PcSingle, PostAttackQuirk.CleaveAfterCombatEnd);
        foreach (AttackOrigin origin in Enum.GetValues(typeof(AttackOrigin)))
            if (origin != AttackOrigin.PcSingle)
                Pin(origin, PostAttackQuirk.NoCleave);
    }

    private static void TestCleaveOnlyOnPcSingle()
    {
        foreach (AttackOrigin origin in Enum.GetValues(typeof(AttackOrigin)))
        {
            bool noCleave = PostAttackQuirkSets.Has(PostAttackQuirkSets.For(origin), PostAttackQuirk.NoCleave);
            Assert(noCleave == (origin != AttackOrigin.PcSingle), $"{origin}: Cleave {(noCleave ? "off" : "on")}");
        }
    }

    private static void TestOnlyCleaveBitsLeft()
    {
        PostAttackQuirk all = PostAttackQuirk.None;
        foreach (PostAttackQuirk bit in Enum.GetValues(typeof(PostAttackQuirk)))
            all |= bit;
        Assert(all == (PostAttackQuirk.NoCleave | PostAttackQuirk.CleaveAfterCombatEnd), "only the Cleave bits are left", all.ToString());
    }

    /// <summary>The thrown drop's test (AttackIntent.IsThrowOf; CMB-174, CMB-096 drop; PHB p.113).</summary>
    private static void TestThrowIntent()
    {
        ItemData javelin = ItemDatabase.Get(DND35e.Identifiers.ItemID.WeaponJavelin);
#pragma warning disable CS0618 // the dart has no ItemID value; the string id is the only key
        ItemData dart = ItemDatabase.Get(DND35e.Identifiers.ItemIDs.DART);
#pragma warning restore CS0618
        ItemData dagger = ItemDatabase.Get(DND35e.Identifiers.ItemID.WeaponDagger);
        ItemData longsword = ItemDatabase.Get(DND35e.Identifiers.ItemID.WeaponLongsword);
        ItemData longbow = ItemDatabase.Get(DND35e.Identifiers.ItemID.WeaponLongbow);
        if (javelin == null || dart == null || dagger == null || longsword == null || longbow == null)
        {
            Assert(false, "the thrown-drop items exist in ItemDatabase");
            return;
        }

        Assert(GameManager.IsThrowableWeapon(javelin) && GameManager.IsThrowableWeapon(dart) && GameManager.IsThrowableWeapon(dagger),
            "javelin, dart and dagger are throwable weapons");
        Assert(!GameManager.IsThrowableWeapon(longsword) && !GameManager.IsThrowableWeapon(longbow), "a longsword and a longbow are not");
        Assert(GameManager.IsThrowableWeapon(dagger, meleeOnly: true) && !GameManager.IsThrowableWeapon(javelin, meleeOnly: true),
            "meleeOnly keeps the melee throwables (the off-hand draw)");

        var npc = new AttackIntent { Quirks = AttackIntentQuirk.NpcIntentReadsUiThrownGlobal, LegacyUiMode = AttackMode.Thrown };
        Assert(npc.IsThrowOf(javelin) && npc.IsThrowOf(dart), "an NPC intent (no mode) throws a javelin or a dart");
        Assert(!npc.IsThrowOf(dagger), "an NPC intent never throws a dagger from the PC's copied selection (CMB-096)");
        Assert(AttackIntent.ForMode(AttackMode.Thrown).IsThrowOf(dagger), "a declared throw throws a dagger");
        Assert(!AttackIntent.ForMode(AttackMode.Melee).IsThrowOf(dagger) && !AttackIntent.ForMode(AttackMode.Melee).IsThrowOf(javelin),
            "a melee intent (an AoO, a charge) throws nothing");
        Assert(AttackIntent.ForMode(AttackMode.Ranged).IsThrowOf(javelin) && !AttackIntent.ForMode(AttackMode.Ranged).IsThrowOf(longbow),
            "a ranged intent throws a javelin, never a bow");
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
