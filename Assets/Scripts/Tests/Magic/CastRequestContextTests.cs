#if UNITY_EDITOR
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 4 (docs/designs/cast_core_batch11.md, per-cast-request-context): every cast carries its own
/// <see cref="SpellCastRequest"/> in a <see cref="CastContext"/> on GameManager's cast stack.
/// Request builds: the spell is always the cast's own clone (a template is never touched), the UI clone reads as the
/// menu's instance did (the MetamagicDataRef record kept without metamagic; with metamagic a plain clone carrying the
/// pre-cast changes), and the AI request's choices come from AiCastDecider (batch 11 step 22: with no enemy energy
/// attack seen, fire for Resist Energy and Protection from Energy and the warm Fire Shield; no weapon to enchant
/// refuses Magic Weapon; AIProfileFrameworkTests checks the real choices). The source flags
/// (TestPanelBypass, IsItem). With a scene GameManager (Play mode): no cast open reads the defaults; setting the pending
/// spell opens a scratch Ui cast and clearing it ends that cast; an AI cast nests over a Ui cast and shows its own state
/// (spell, choices, source) until it ends; a new Ui cast replaces a Ui cast left open; and ending a context that is not on
/// top closes the ones above it and raises ScenarioHooks.CastStackViolation. The stale-state rules (a cancelled PC choice,
/// wand flag or F12 bypass) are checked through real casts by the rules/stale-cast-state-* scenarios.
/// Pure parts run in edit or Play mode; the stack checks need Play mode and are skipped (not failed) without a GameManager.
/// </summary>
public static class CastRequestContextTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== CAST REQUEST CONTEXT TESTS (batch 11 step 4) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        TestAiRequestDefaults();
        TestUiRequestClone();
        TestSourceFlags();

        if (GameManager.Instance != null)
            TestCastStack(GameManager.Instance);
        else
            Debug.Log("  SKIP: cast stack checks (no scene GameManager; they run in the Play pass)");

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    private static void TestAiRequestDefaults()
    {
        SpellData resist = SpellDatabase.GetSpell(SpellNames.RESIST_ENERGY);
        SpellCastRequest r = SpellCastRequest.ForAI(null, null, resist);
        Assert(r.Spell != null && !ReferenceEquals(r.Spell, resist) && r.Spell.SpellId == SpellNames.RESIST_ENERGY,
            "ForAI: the request's spell is its own clone of the template");
        Assert(!r.Choices.Energy.HasValue, "ForAI: the choices are left to the decider (the cast core's Legality stage asks it)");
        Assert(FillAi(r) == CastChoiceFill.Filled && r.Choices.Energy == ResistEnergyType.Fire,
            "AiCastDecider.FillChoices: Resist Energy with no energy attack seen gets fire", "" + r.Choices.Energy);
        Assert(r.Decider is AiCastDecider && r.Decider.IsSynchronous, "ForAI: the AI decider answers inline");
        Assert(r.Metamagic == null && r.Source.Kind == CastSourceKind.PreparedSlot, "ForAI: no metamagic; a caster without spellcasting casts from a prepared slot");
        Assert(r.Quirks == LegacyCastQuirkSets.AiPath, "ForAI: the NPC path's legacy quirks (batch 11 step 8)", "" + r.Quirks);

        SpellCastRequest p = SpellCastRequest.ForAI(null, null, SpellDatabase.GetSpell(SpellNames.PROTECTION_FROM_ENERGY));
        Assert(FillAi(p) == CastChoiceFill.Filled && p.Choices.Energy == ResistEnergyType.Fire, "AiCastDecider.FillChoices: Protection from Energy gets fire", "" + p.Choices.Energy);

        SpellCastRequest f = SpellCastRequest.ForAI(null, null, SpellDatabase.GetSpell(SpellNames.FIRE_SHIELD));
        Assert(FillAi(f) == CastChoiceFill.Filled && f.Choices.FireShieldWarm == true, "AiCastDecider.FillChoices: Fire Shield is warm", "" + f.Choices.FireShieldWarm);

        SpellCastRequest m = SpellCastRequest.ForAI(null, null, SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE));
        Assert(FillAi(m) == CastChoiceFill.Filled && !m.Choices.Energy.HasValue && !m.Choices.FireShieldWarm.HasValue,
            "AiCastDecider.FillChoices: another spell gets no energy or shield choice");

        SpellCastRequest w = SpellCastRequest.ForAI(null, null, SpellDatabase.GetSpell(SpellNames.MAGIC_WEAPON));
        Assert(FillAi(w) == CastChoiceFill.Refused && w.Choices.MagicWeaponItem == null && w.Choices.DisguiseRace == null,
            "AiCastDecider.FillChoices: Magic Weapon with no recipient weapon is refused, nothing spent (batch 11 step 22, SPL-113)");

        SpellCastRequest chosen = SpellCastRequest.ForAI(null, null, resist);
        chosen.Choices.Energy = ResistEnergyType.Cold;
        Assert(FillAi(chosen) == CastChoiceFill.Filled && chosen.Choices.Energy == ResistEnergyType.Cold,
            "AiCastDecider.FillChoices keeps a choice the request already carries");
    }

    private static CastChoiceFill FillAi(SpellCastRequest request)
        => AiCastDecider.Instance.FillChoices(null, new CastContext(request));

    private static void TestUiRequestClone()
    {
        SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        int templateRange = missile.RangeSquares;

        var record = new MetamagicData();
        SpellData menuSpell = missile.Clone();
        menuSpell.MetamagicDataRef = record;
        SpellCastRequest plain = SpellCastRequest.ForUi(null, menuSpell, null, null);
        Assert(plain.Spell != null && !ReferenceEquals(plain.Spell, menuSpell), "ForUi: the request's spell is a clone of the menu's spell");
        Assert(ReferenceEquals(plain.Spell.MetamagicDataRef, record), "ForUi without metamagic: the clone keeps the menu spell's MetamagicDataRef record");
        Assert(plain.Decider is UiCastDecider && !plain.Decider.IsSynchronous, "ForUi: the UI decider may wait for the player");
        Assert(plain.Source != null && plain.Source.Kind == CastSourceKind.PreparedSlot && plain.Choices != null, "ForUi: a default source and fresh choices");

        var enlarge = new MetamagicData();
        enlarge.Toggle(MetamagicFeatId.EnlargeSpell);
        SpellCastRequest enlarged = SpellCastRequest.ForUi(null, missile, enlarge, null);
        Assert(enlarged.Spell.RangeSquares == templateRange * 2 && missile.RangeSquares == templateRange,
            "ForUi with Enlarge Spell: the clone's range doubles and the template's does not (PHB p.94)",
            "clone " + enlarged.Spell.RangeSquares + ", template " + missile.RangeSquares + ", was " + templateRange);
        Assert(ReferenceEquals(enlarged.Spell.MetamagicDataRef, enlarge) && !enlarged.Spell.IsPreparedMetamagicSlotSpell,
            "ForUi with metamagic: a plain clone stamped with the cast's metamagic, not a prepared-slot spell (batch 11 step 19, SPL-038)");

        SpellData prebuilt = missile.Clone();
        SpellCastRequest item = SpellCastRequest.ForUiPrebuilt(null, prebuilt, null, new CastSource { Kind = CastSourceKind.Wand });
        Assert(ReferenceEquals(item.Spell, prebuilt) && item.Source.Kind == CastSourceKind.Wand, "ForUiPrebuilt: the item's own clone is used as built");
    }

    private static void TestSourceFlags()
    {
        var f12 = new CastSource { Kind = CastSourceKind.TestPanel, Bypass = TestPanelBypass.Components | TestPanelBypass.ArcaneFailure };
        Assert(f12.HasBypass(TestPanelBypass.Components) && f12.HasBypass(TestPanelBypass.ArcaneFailure) && !f12.HasBypass(TestPanelBypass.Slots),
            "TestPanelBypass: each F12 privilege is its own flag");
        Assert(!f12.HasBypass(TestPanelBypass.None), "TestPanelBypass.None is no privilege");
        Assert(new CastSource { Kind = CastSourceKind.Scroll }.IsItem && new CastSource { Kind = CastSourceKind.Wand }.IsItem
            && !new CastSource().IsItem && !f12.IsItem, "IsItem: scrolls and wands only");
    }

    private static void TestCastStack(GameManager gm)
    {
        int baseCount = gm.Harness_OpenCastCount;
        if (baseCount != 0)
        {
            Debug.Log("  SKIP: cast stack checks (a cast is open in this Play session: " + baseCount + ")");
            return;
        }

        var violations = new List<string>();
        System.Action<string> oldHook = ScenarioHooks.CastStackViolation;
        ScenarioHooks.CastStackViolation = d => violations.Add(d);
        try
        {
            Assert(gm.Harness_GetPendingSpell() == null && gm.Harness_GetPendingMetamagic() == null && gm.Harness_CurrentCastRequest == null,
                "No cast open: the pending spell and metamagic read their defaults");

            SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
            var mm = new MetamagicData();
            gm.Harness_SetPendingCast(missile, mm);
            Assert(gm.Harness_OpenCastCount == 1 && ReferenceEquals(gm.Harness_GetPendingSpell(), missile) && ReferenceEquals(gm.Harness_GetPendingMetamagic(), mm),
                "Setting the pending spell with no cast open opens one scratch Ui cast holding it");
            SpellCastRequest ui = gm.Harness_CurrentCastRequest;
            Assert(ui != null && !ui.Decider.IsSynchronous, "The scratch cast is a Ui cast");

            CastContext ai = gm.BeginCast(SpellCastRequest.ForAI(null, null, SpellDatabase.GetSpell(SpellNames.RESIST_ENERGY)));
            ai.Request.Decider.FillChoices(gm, ai);
            Assert(gm.Harness_OpenCastCount == 2 && gm.Harness_GetPendingSpell() != null && gm.Harness_GetPendingSpell().SpellId == SpellNames.RESIST_ENERGY
                && gm.Harness_GetPendingMetamagic() == null && gm.Harness_CurrentCastRequest.Choices.Energy == ResistEnergyType.Fire,
                "An AI cast nests over the Ui cast and shows only its own spell, metamagic and choices");
            gm.EndCast(ai);
            Assert(ai.Ended && gm.Harness_OpenCastCount == 1 && ReferenceEquals(gm.Harness_GetPendingSpell(), missile),
                "Ending the AI cast uncovers the Ui cast unchanged");

            gm.Harness_SetPendingCast(null, null);
            Assert(gm.Harness_OpenCastCount == 0 && gm.Harness_GetPendingSpell() == null, "Clearing the pending spell ends the Ui cast");

            CastContext first = gm.BeginCast(SpellCastRequest.ForUi(null, missile, null, null));
            CastContext second = gm.BeginCast(SpellCastRequest.ForUi(null, missile, null, null));
            Assert(first.Ended && !second.Ended && gm.Harness_OpenCastCount == 1, "A new Ui cast replaces a Ui cast left open");
            Assert(violations.Count == 0, "No violation so far", string.Join("; ", violations));

            CastContext nested = gm.BeginCast(SpellCastRequest.ForAI(null, null, missile));
            gm.EndCast(second);
            Assert(second.Ended && nested.Ended && gm.Harness_OpenCastCount == 0, "Ending a cast that is not on top closes the casts above it too");
            Assert(violations.Count == 1, "...and raises one cast-stack violation", string.Join("; ", violations));
            gm.EndCast(nested);
            Assert(violations.Count == 1 && gm.Harness_OpenCastCount == 0, "Ending an already-ended cast does nothing");
        }
        finally
        {
            ScenarioHooks.CastStackViolation = oldHook;
            if (gm.Harness_OpenCastCount > 0)
                gm.Harness_EndAllCasts("CastRequestContextTests cleanup");
        }
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }
}
}
#endif
