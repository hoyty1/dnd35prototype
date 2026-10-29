using System;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The post-attack hook's shared rules (batch 12 steps 13-15, docs/designs/pc_npc_parity_batch12.md; the
/// CastCoreStageOrderTests pattern): since step 15 every attack origin A-J runs GameManager.OnAttackResolved's stages alike
/// and the PostAttackQuirk type is gone (pinned here); Cleave's "drop" test (CleaveRules.DroppedTarget, PHB p.92, D7); the
/// thrown drop's intent test (AttackIntent.IsThrowOf, GameManager.IsThrowableWeapon), the decider factory
/// (AttackDeciders.For: the IsControllable read and its override) and its Cleave and reaction answers without a scene, and
/// the intent's mode mapping. Pure: no scene, no dice; runs in edit or Play mode. The suite keeps its step 13 name.
/// </summary>
public static class PostAttackQuirkSetsTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== POST-ATTACK QUIRK SETS TESTS (batch 12 steps 13-15) ==========");

        TestNoQuirksLeft();
        TestCleaveDrop();
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

    /// <summary>Step 15 removed the last origin differences (the Cleave bits, CMB-017): no quirk type or field is left.</summary>
    private static void TestNoQuirksLeft()
    {
        Assert(typeof(AttackResolution).Assembly.GetType("PostAttackQuirk") == null, "the PostAttackQuirk type is gone");
        Assert(typeof(AttackResolution).Assembly.GetType("PostAttackQuirkSets") == null, "the PostAttackQuirkSets table is gone");
        Assert(typeof(AttackResolution).GetField("Quirks") == null, "AttackResolution has no quirk field");
        Assert(typeof(AttackResolution).Assembly.GetType("AttackIntentQuirk") == null && typeof(AttackIntent).GetField("Quirks") == null,
            "the AttackIntentQuirk type and the intent's quirk field are gone (batch 12 step 17, CMB-096)");
        foreach (AttackOrigin origin in Enum.GetValues(typeof(AttackOrigin)))
        {
            AttackResolution r = AttackResolution.Create(origin, null, null, null);
            Assert(r.Origin == origin && !r.IsCleave && r.CallerCanWait && r.Decider is AiAttackDecider,
                $"{origin}: a plain resolution (no cleave, may wait, the AI decider without a creature)");
        }
    }

    private static CombatResult Melee(bool hit, int before, int after, bool killed = false, bool downBefore = false, bool? downAfter = null)
        => new CombatResult { Hit = hit, DefenderHPBefore = before, DefenderHPAfter = after, TargetKilled = killed, DefenderDownBefore = downBefore,
            DefenderDownAfter = downAfter ?? (downBefore || killed || after < 0) };

    /// <summary>Cleave's trigger (PHB p.92; decision D7): killed, below 0, or knocked unconscious; never disabled at 0.</summary>
    private static void TestCleaveDrop()
    {
        Assert(CleaveRules.DroppedTarget(Melee(true, 5, -12, killed: true), null), "a kill drops the foe");
        Assert(CleaveRules.DroppedTarget(Melee(true, 5, -1), null), "5 hp to -1 drops the foe (below 0)");
        Assert(CleaveRules.DroppedTarget(Melee(true, 0, -2), null), "a disabled foe taken below 0 drops");
        Assert(!CleaveRules.DroppedTarget(Melee(true, 5, 0), null), "5 hp to exactly 0 (disabled) does not drop the foe");
        Assert(!CleaveRules.DroppedTarget(Melee(true, -2, -5, downBefore: true), null), "a dying foe hit again but not killed did not drop now");
        Assert(CleaveRules.DroppedTarget(Melee(true, -2, -10, killed: true, downBefore: true), null), "killing a dying foe kills it, which drops it (D7)");
        Assert(!CleaveRules.DroppedTarget(Melee(false, 5, 5), null), "a miss drops nothing");
        var ranged = Melee(true, 5, -1);
        ranged.IsRangedAttack = true;
        Assert(!CleaveRules.DroppedTarget(ranged, null), "a ranged attack never cleaves");
        var notMade = Melee(true, 5, -1);
        notMade.NotMade = true;
        Assert(!CleaveRules.DroppedTarget(notMade, null), "an attack never made drops nothing");
        var figure = Melee(true, 5, -1);
        figure.MirrorImageFigure = true;
        Assert(!CleaveRules.DroppedTarget(figure, null), "a hit on a Mirror Image figure drops nothing");
        Assert(CleaveRules.DroppedTarget(Melee(true, 5, 5, downAfter: true), null), "nonlethal damage that knocks the foe unconscious drops it");
        // A two-weapon batch (path D): the main hand leaves the foe standing, the off hand drops it. The per-result
        // snapshot (DefenderDownAfter) picks the off-hand result, never the earlier one.
        CombatResult mainHand = Melee(true, 10, 4);
        CombatResult offHand = Melee(true, 4, -2);
        offHand.IsOffHandAttack = true;
        CombatResult picked = CleaveRules.FirstDropping(new System.Collections.Generic.List<CombatResult> { mainHand, offHand }, null);
        Assert(picked == offHand, "FirstDropping picks the result that dropped the foe (the off hand), not an earlier hit");
        CombatResult knockout = Melee(true, 6, 6, downAfter: true);
        Assert(CleaveRules.FirstDropping(new System.Collections.Generic.List<CombatResult> { Melee(true, 9, 6), knockout }, null) == knockout,
            "FirstDropping picks the later nonlethal knockout, not the earlier hit");
        Assert(!CleaveRules.MayCleave(null, out bool great) && !great, "no attacker, no cleave");
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

        var noMode = new AttackIntent();
        Assert(noMode.IsThrowOf(javelin) && noMode.IsThrowOf(dart), "an intent with no mode throws a javelin or a dart");
        Assert(!noMode.IsThrowOf(dagger), "an intent with no mode never throws a dagger (only a declared throw does)");
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

        bool sprays = true;
        Coroutine reactionWait = AiAttackDecider.Instance.ChooseAttackedReaction(null, null, null, "Acid Spray", b => sprays = b);
        Assert(!sprays && reactionWait == null, "the AI decider without a GameManager declines the reaction at once");
        CharacterController picked = null;
        bool answered = false;
        Coroutine wait = AiAttackDecider.Instance.ChooseCleaveTarget(null, null, new System.Collections.Generic.List<CharacterController>(), c => { picked = c; answered = true; });
        Assert(answered && picked == null && wait == null, "no Cleave candidate: the AI answers none at once");
        answered = false;
        Coroutine uiWait = UiAttackDecider.Instance.ChooseCleaveTarget(null, null, new System.Collections.Generic.List<CharacterController>(), c => { picked = c; answered = true; });
        Assert(answered && picked == null && uiWait == null, "no Cleave candidate: the UI asks nothing (D7)");
    }

    private static void TestIntentModeMapping()
    {
        foreach (GameManager.AttackType type in Enum.GetValues(typeof(GameManager.AttackType)))
            Assert(AttackIntent.ToAttackType(AttackIntent.ToMode(type)) == type, $"intent mode round-trips {type}");
        Assert(AttackIntent.ToAttackType(null) == GameManager.AttackType.Melee, "no mode reads as Melee");
        Assert(new AttackIntent { Mode = AttackMode.Thrown }.ThrownSelected && !new AttackIntent().ThrownSelected, "ThrownSelected reads the mode");

        // Batch 12 step 17 (CMB-096): the NPC intent's mode is its own (AttackIntent.ChooseAiMode), never a copy of the PC UI's.
        AttackIntent ai = AttackIntent.ForAI(null, null, null);
        Assert(ai.Mode == AttackMode.Melee && ai.UiType == null && !ai.ThrownSelected,
            "ForAI with no creature: Melee, no UI type, no throw");
        ItemData longbowItem = ItemDatabase.Get(DND35e.Identifiers.ItemID.WeaponLongbow);
        ItemData daggerItem = ItemDatabase.Get(DND35e.Identifiers.ItemID.WeaponDagger);
        Assert(AttackIntent.ChooseAiMode(null, longbowItem, null) == AttackMode.Ranged,
            "an NPC with a bow shoots (Ranged)");
        Assert(AttackIntent.ChooseAiMode(null, daggerItem, null) == AttackMode.Melee,
            "an NPC with a dagger and no target out of its reach fights in melee");
        AttackIntent ui = AttackIntent.ForUi(null, null);
        Assert(ui.Mode == AttackMode.Melee && ui.UiType == GameManager.AttackType.Melee && ui.Weapon == null,
            "ForUi without a GameManager: Melee, the UI type Melee, no weapon");
        Assert(new AttackIntent { Mode = AttackMode.Ranged, UiType = GameManager.AttackType.Melee }.UiAttackType == GameManager.AttackType.Melee,
            "UiAttackType keeps the UI's own selection (the Attack button's Melee with a bow)");
    }
}
}
