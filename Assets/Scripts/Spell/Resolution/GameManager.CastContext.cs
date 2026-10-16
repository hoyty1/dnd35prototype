using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager partial class: the per-cast context (batch 11 step 4, docs/designs/cast_core_batch11.md).
///
/// Every cast entry point builds a <see cref="SpellCastRequest"/> and opens a <see cref="CastContext"/> for it with
/// <see cref="BeginCast"/>: the spell menu (OnSpellSelectedWithMetamagic, with its touch prompt), the Discharge button,
/// scrolls and wands (Initiate*CastThroughPipeline), the F12 panel (TestCastSpellFromPanel), an imbued spell
/// (ResolveImbuedSpellCast, Imbue with Spell Ability) and the NPC executor
/// (TryNPCPerformSpellCast, which the charmed heal and the scenario harness reach through TryNPCPerformSpellCastForAI).
/// Open contexts sit on a stack: a synchronous AI cast nests on top of a Ui cast that waits for the player, and pops
/// in order; a pop that does not match the top raises a scenario-harness violation (ScenarioHooks.CastStackViolation).
///
/// GameManager's old per-cast field names (<c>_pendingSpell</c>, <c>_pendingMetamagic</c>, the choices, the scroll and
/// wand state) are the current cast's request: read-only views onto the context on top of the stack (below; batch 11
/// step 29 removed their setters). A view with no cast open returns the old default. The request builders
/// (SpellCastRequest.For*) fill a request; the UI's choice dialogs answer into the current cast through
/// <see cref="UiCastChoicesForAnswer"/> (with no cast open, a scratch Ui context, as before); every UI exit (cast
/// resolved, cancelled, refused) ends the open Ui cast with <see cref="EndPendingCast"/>; the cast core's stages read
/// <see cref="CastContext"/>. A new Ui cast ends any Ui cast the UI left open (the old reset lists), and the combat
/// reset ends them all.
/// </summary>
public partial class GameManager
{
    private readonly Stack<CastContext> _castStack = new Stack<CastContext>();

    /// <summary>The scroll or wand cast the last consumable use handed to the pipeline (see <see cref="ConsumableCastAwaitingSpend"/>).</summary>
    private CastContext _lastConsumableCast;

    /// <summary>The cast on top of the stack, or null.</summary>
    private CastContext CurrentCast => _castStack.Count > 0 ? _castStack.Peek() : null;

    /// <summary>The source of the cast on top of the stack, or null.</summary>
    private CastSource CurrentCastSource => CurrentCast?.Request.Source;

    /// <summary>
    /// The newest open cast of <paramref name="spell"/> (by SpellId) by <paramref name="caster"/>, or null.
    /// CasterLevelRules.ForCaster reads it, so the cast's own caster level (a scroll's or wand's, DMG p.237, p.245)
    /// reaches the readers that have no request in hand: durations, handlers, SpellCaster.Cast (batch 11 step 14;
    /// SPL-016, SPL-017).
    /// </summary>
    internal SpellCastRequest OpenCastRequestFor(CharacterController caster, SpellData spell)
        => OpenCastFor(caster, spell)?.Request;

    /// <summary>The newest open cast of <paramref name="spell"/> (by SpellId) by <paramref name="caster"/>, or null.</summary>
    internal CastContext OpenCastFor(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null || string.IsNullOrEmpty(spell.SpellId))
            return null;
        foreach (CastContext ctx in _castStack)
        {
            SpellCastRequest req = ctx != null ? ctx.Request : null;
            if (req != null && !ctx.Ended && req.Caster == caster && req.Spell != null
                && string.Equals(req.Spell.SpellId, spell.SpellId, System.StringComparison.Ordinal))
                return ctx;
        }
        return null;
    }

    /// <summary>
    /// <paramref name="target"/>'s save against <paramref name="spell"/> cast by <paramref name="caster"/>, for a spell
    /// handler with no cast context in hand (batch 11 step 18, SPL-018): the open cast's CastContext.RollSave, else
    /// SpellSaveRules.RollSave for the same caster and spell (a later effect of a cast that has ended). Either way the
    /// target may give up its save (PHB p.177) and the save carries the spell-and-caster bonuses (SaveRules).
    /// </summary>
    private SaveResult RollSpellSave(CharacterController caster, CharacterController target, SpellData spell, SaveType save, int dc,
        string diceContext = null)
    {
        CastContext open = OpenCastFor(caster, spell);
        return open != null
            ? open.RollSave(target, save, dc, diceContext)
            : SpellSaveRules.RollSave(caster, target, spell, save, dc, diceContext);
    }

    /// <summary>Opens <paramref name="request"/>'s context on top of the cast stack.</summary>
    internal CastContext BeginCast(SpellCastRequest request)
    {
        if (request == null)
            request = new SpellCastRequest();
        if (request.Source == null)
            request.Source = new CastSource();
        if (request.Choices == null)
            request.Choices = new CastChoices();
        if (request.Aim == null)
            request.Aim = new CastAim();
        if (request.Decider == null)
            request.Decider = UiCastDecider.Instance;

        if (!request.Decider.IsSynchronous)
        {
            // The UI runs one cast at a time: a Ui cast it left open (an exit that never cleared the spell, such as a
            // target list with no valid target) is replaced, as the old reset lists overwrote its fields.
            while (_castStack.Count > 0 && !_castStack.Peek().Request.Decider.IsSynchronous)
                CloseCastContext(_castStack.Pop(), "replaced by a new Ui cast");
            if (_castStack.Count > 0)
                ReportCastStackViolation("a Ui cast of " + DescribeCast(request) + " began inside the AI cast " + DescribeCast(_castStack.Peek().Request));
        }

        var context = new CastContext(request);
        _castStack.Push(context);
        return context;
    }

    /// <summary>
    /// Closes <paramref name="context"/>. Ending a context that already ended (a combat reset closed it) does nothing;
    /// ending one that is not on top closes the contexts above it too and raises a scenario-harness violation.
    /// </summary>
    internal void EndCast(CastContext context)
    {
        if (context == null || context.Ended)
            return;

        if (!_castStack.Contains(context))
        {
            context.Ended = true;
            return;
        }

        if (_castStack.Peek() != context)
        {
            ReportCastStackViolation("EndCast for " + DescribeCast(context.Request) + " while " + DescribeCast(_castStack.Peek().Request) + " is on top of the cast stack");
            while (_castStack.Count > 0 && _castStack.Peek() != context)
                CloseCastContext(_castStack.Pop(), "closed under a mismatched EndCast");
        }

        CloseCastContext(_castStack.Pop(), null);
    }

    /// <summary>Ends the open Ui cast, if any (a cast exit, cancel or refusal on the PC side).</summary>
    private void EndPendingCast()
    {
        CastContext top = CurrentCast;
        if (top == null)
            return;
        if (top.Request.Decider.IsSynchronous)
        {
            ReportCastStackViolation("a Ui cast exit tried to end the AI cast " + DescribeCast(top.Request));
            return;
        }
        EndCast(top);
    }

    /// <summary>Ends every open cast (the combat reset).</summary>
    private void EndAllCasts(string reason)
    {
        while (_castStack.Count > 0)
            CloseCastContext(_castStack.Pop(), reason);
        _lastConsumableCast = null;
    }

    private void CloseCastContext(CastContext context, string reason)
    {
        if (context == null)
            return;
        context.Ended = true;
        if (context.Request.Source != null && context.Request.Source.Kind == CastSourceKind.TestPanel)
            _testPanelCaster = null;
        if (reason != null)
            Debug.Log($"[CastStack] Ended {DescribeCast(context.Request)}: {reason}");
    }

    private static string DescribeCast(SpellCastRequest request)
    {
        if (request == null)
            return "(no cast)";
        string caster = request.Caster != null && request.Caster.Stats != null ? request.Caster.Stats.CharacterName : "?";
        string spell = request.Spell != null ? request.Spell.SpellId : "no spell";
        string decider = request.Decider != null && request.Decider.IsSynchronous ? "AI" : "Ui";
        return $"{decider} {caster}:{spell} ({request.Source?.Kind})";
    }

    private static void ReportCastStackViolation(string detail)
    {
        Debug.LogWarning("[CastStack] " + detail);
        ScenarioHooks.CastStackViolation?.Invoke(detail);
    }

    /// <summary>
    /// The open cast's context to write a value into: the top one, or, when no cast is open, a new scratch Ui context
    /// for a non-default value (the UI's scratch flow), or null for a default value (nothing to reset).
    /// </summary>
    private CastContext CastForWrite(bool isDefaultValue)
    {
        CastContext top = CurrentCast;
        if (top != null || isDefaultValue)
            return top;
        return BeginCast(SpellCastRequest.ForUiPrebuilt(ActivePC ?? _testPanelCaster, null, null, new CastSource()));
    }

    /// <summary>
    /// True while the scroll or wand just handed to the cast pipeline by this consumable use has not been spent: its
    /// cast waits for a target, or it ended without spending the item. The item-use code then charges no action of its
    /// own (the cast spends it with the item, ConsumeScrollAfterCast and ConsumeWandChargeAfterCast).
    /// </summary>
    private bool ConsumableCastAwaitingSpend
        => _lastConsumableCast != null && _lastConsumableCast.Request.Source.Item != null;

    /// <summary>
    /// The current cast's choices for the UI's choice dialogs to fill (the Disguise Self race, the energy types, the Fire
    /// Shield form, the weapon, rope and Grease pickers, the wall shapes, the summon picks): the request of the cast on top
    /// of the stack, or, with no cast open, a new scratch Ui cast's (CastForWrite). Only the UI writes the request this
    /// way; the AI decider fills its own request (AiCastDecider.FillChoices) and the rules read it.
    /// </summary>
    private CastChoices UiCastChoicesForAnswer => CastForWrite(false).Request.Choices;

    // ── The current cast's request: read-only views onto the cast on top of the stack ─────────────────────
    // The old per-cast field names (batch 11 step 4) are views of the current cast's request since batch 11 step 29: no
    // setters. A request builder (SpellCastRequest.For*) fills the request, the UI's dialogs answer through
    // UiCastChoicesForAnswer, a cast ends with EndPendingCast or EndCast, and the cast core's stages read CastContext.
    // With no cast open each view returns its default.

    /// <summary>The current cast's spell, or null with no cast open.</summary>
    private SpellData _pendingSpell => CurrentCast?.Request.Spell;

    /// <summary>The current cast's metamagic, or null.</summary>
    private MetamagicData _pendingMetamagic => CurrentCast?.Request.Metamagic;

    /// <summary>True when the current cast delivers a held touch charge (its source kind).</summary>
    private bool _pendingSpellFromHeldCharge => CurrentCastSource?.Kind == CastSourceKind.HeldChargeDelivery;

    private ItemData _pendingAnimateRopeItem => CurrentCast?.Request.Choices.AnimateRopeItem;

    private ItemData _pendingMagicWeaponItem => CurrentCast?.Request.Choices.MagicWeaponItem;

    private ItemData _pendingKeenEdgeItem => CurrentCast?.Request.Choices.KeenEdgeItem;

    private bool _pendingKeenEdgeIsAmmo => CurrentCast != null && CurrentCast.Request.Choices.KeenEdgeIsAmmo;

    private ItemData _pendingGreaterMagicWeaponItem => CurrentCast?.Request.Choices.GreaterMagicWeaponItem;

    /// <summary>The energy type of the current Resist Energy cast (one choice field with Protection from Energy).</summary>
    private ResistEnergyType? _pendingResistEnergyType => CurrentCast?.Request.Choices.Energy;

    /// <summary>The energy type of the current Protection from Energy cast (one choice field with Resist Energy).</summary>
    private ResistEnergyType? _pendingProtectionFromEnergyType => CurrentCast?.Request.Choices.Energy;

    private bool? _pendingFireShieldIsWarm => CurrentCast?.Request.Choices.FireShieldWarm;

    private string _pendingDisguiseSelfRace => CurrentCast?.Request.Choices.DisguiseRace;

    private SummonMonsterOption _pendingSummonSelection => CurrentCast?.Request.Choices.SummonSelection;

    private int _pendingSummonListLevel => CurrentCast != null ? CurrentCast.Request.Choices.SummonListLevel : 0;

    private SummonCreatureCountInfo _pendingSummonCountInfo => CurrentCast?.Request.Choices.SummonCountInfo;

    private string _pendingSummonSwarmNpcId => CurrentCast?.Request.Choices.SummonSwarmNpcId;

    private GreaseCastMode _pendingGreaseCastMode => CurrentCast != null ? CurrentCast.Request.Choices.GreaseMode : GreaseCastMode.None;

    private WallOfFireMode? _pendingWallOfFireMode => CurrentCast?.Request.Choices.WallOfFireMode;

    private WallOfIceMode? _pendingWallOfIceMode => CurrentCast?.Request.Choices.WallOfIceMode;

    /// <summary>True when the current cast reads a scroll (its source kind).</summary>
    private bool _pendingScrollCastActive => CurrentCastSource?.Kind == CastSourceKind.Scroll;

    /// <summary>The current scroll cast's scroll; null once spent (or for any other source).</summary>
    private ItemData _pendingScrollCastItem => _pendingScrollCastActive ? CurrentCastSource.Item : null;

    private int _pendingScrollCastInventoryIndex => _pendingScrollCastActive ? CurrentCastSource.ItemIndex : -1;

    /// <summary>True when the current cast uses a wand (its source kind).</summary>
    private bool _pendingWandCastActive => CurrentCastSource?.Kind == CastSourceKind.Wand;

    /// <summary>The current wand cast's wand; null once its charge is spent (or for any other source).</summary>
    private ItemData _pendingWandCastItem => _pendingWandCastActive ? CurrentCastSource.Item : null;

    private int _pendingWandCastInventoryIndex => _pendingWandCastActive ? CurrentCastSource.ItemIndex : -1;

    /// <summary>True when the open cast comes from the F12 Spell Testing Panel (its source kind).</summary>
    private bool _testPanelCastActive => CurrentCastSource?.Kind == CastSourceKind.TestPanel;
}
