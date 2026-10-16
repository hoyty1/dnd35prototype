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
/// During the migration GameManager's old per-cast fields keep their names as private forwarding properties onto the
/// context on top of the stack (below). A getter with no cast open returns the old default; a setter that writes a
/// non-default value with no cast open opens a scratch Ui context, so the UI's scratch flow is unchanged. Assigning
/// null to <c>_pendingSpell</c> ends the open Ui cast: every legacy exit (cast resolved, cancelled, refused) already
/// does that, so the context closes exactly where the old flow let go of the spell. Exits that never cleared the spell
/// call <see cref="EndPendingCast"/>. A new Ui cast ends any Ui cast the UI left open (the old reset lists), and the
/// combat reset ends them all.
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

    // ── Forwarding properties: the old per-cast field names, onto the cast on top of the stack ──────────────

    /// <summary>The open cast's spell. Assigning null ends the open Ui cast (every legacy exit does so).</summary>
    private SpellData _pendingSpell
    {
        get => CurrentCast?.Request.Spell;
        set
        {
            if (value == null)
            {
                EndPendingCast();
                return;
            }
            CastForWrite(false).Request.Spell = value;
        }
    }

    private MetamagicData _pendingMetamagic
    {
        get => CurrentCast?.Request.Metamagic;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Metamagic = value; }
    }

    /// <summary>True when the open cast delivers a held touch charge (its source kind).</summary>
    private bool _pendingSpellFromHeldCharge
    {
        get => CurrentCastSource?.Kind == CastSourceKind.HeldChargeDelivery;
        set
        {
            CastContext c = CastForWrite(!value);
            if (c == null)
                return;
            if (value)
                c.Request.Source.Kind = CastSourceKind.HeldChargeDelivery;
            else if (c.Request.Source.Kind == CastSourceKind.HeldChargeDelivery)
                c.Request.Source.Kind = CastSourceKind.PreparedSlot;
        }
    }

    private ItemData _pendingAnimateRopeItem
    {
        get => CurrentCast?.Request.Choices.AnimateRopeItem;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Choices.AnimateRopeItem = value; }
    }

    private ItemData _pendingMagicWeaponItem
    {
        get => CurrentCast?.Request.Choices.MagicWeaponItem;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Choices.MagicWeaponItem = value; }
    }

    private ItemData _pendingKeenEdgeItem
    {
        get => CurrentCast?.Request.Choices.KeenEdgeItem;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Choices.KeenEdgeItem = value; }
    }

    private bool _pendingKeenEdgeIsAmmo
    {
        get => CurrentCast != null && CurrentCast.Request.Choices.KeenEdgeIsAmmo;
        set { CastContext c = CastForWrite(!value); if (c != null) c.Request.Choices.KeenEdgeIsAmmo = value; }
    }

    private ItemData _pendingGreaterMagicWeaponItem
    {
        get => CurrentCast?.Request.Choices.GreaterMagicWeaponItem;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Choices.GreaterMagicWeaponItem = value; }
    }

    /// <summary>The energy type of the open Resist Energy cast (one choice field with Protection from Energy).</summary>
    private ResistEnergyType? _pendingResistEnergyType
    {
        get => CurrentCast?.Request.Choices.Energy;
        set { CastContext c = CastForWrite(!value.HasValue); if (c != null) c.Request.Choices.Energy = value; }
    }

    /// <summary>The energy type of the open Protection from Energy cast (one choice field with Resist Energy).</summary>
    private ResistEnergyType? _pendingProtectionFromEnergyType
    {
        get => CurrentCast?.Request.Choices.Energy;
        set { CastContext c = CastForWrite(!value.HasValue); if (c != null) c.Request.Choices.Energy = value; }
    }

    private bool? _pendingFireShieldIsWarm
    {
        get => CurrentCast?.Request.Choices.FireShieldWarm;
        set { CastContext c = CastForWrite(!value.HasValue); if (c != null) c.Request.Choices.FireShieldWarm = value; }
    }

    private string _pendingDisguiseSelfRace
    {
        get => CurrentCast?.Request.Choices.DisguiseRace;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Choices.DisguiseRace = value; }
    }

    private SummonMonsterOption _pendingSummonSelection
    {
        get => CurrentCast?.Request.Choices.SummonSelection;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Choices.SummonSelection = value; }
    }

    private int _pendingSummonListLevel
    {
        get => CurrentCast != null ? CurrentCast.Request.Choices.SummonListLevel : 0;
        set { CastContext c = CastForWrite(value == 0); if (c != null) c.Request.Choices.SummonListLevel = value; }
    }

    private SummonCreatureCountInfo _pendingSummonCountInfo
    {
        get => CurrentCast?.Request.Choices.SummonCountInfo;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Choices.SummonCountInfo = value; }
    }

    private string _pendingSummonSwarmNpcId
    {
        get => CurrentCast?.Request.Choices.SummonSwarmNpcId;
        set { CastContext c = CastForWrite(value == null); if (c != null) c.Request.Choices.SummonSwarmNpcId = value; }
    }

    private GreaseCastMode _pendingGreaseCastMode
    {
        get => CurrentCast != null ? CurrentCast.Request.Choices.GreaseMode : GreaseCastMode.None;
        set { CastContext c = CastForWrite(value == GreaseCastMode.None); if (c != null) c.Request.Choices.GreaseMode = value; }
    }

    private WallOfFireMode? _pendingWallOfFireMode
    {
        get => CurrentCast?.Request.Choices.WallOfFireMode;
        set { CastContext c = CastForWrite(!value.HasValue); if (c != null) c.Request.Choices.WallOfFireMode = value; }
    }

    private WallOfIceMode? _pendingWallOfIceMode
    {
        get => CurrentCast?.Request.Choices.WallOfIceMode;
        set { CastContext c = CastForWrite(!value.HasValue); if (c != null) c.Request.Choices.WallOfIceMode = value; }
    }

    /// <summary>True when the open cast reads a scroll (its source kind).</summary>
    private bool _pendingScrollCastActive => CurrentCastSource?.Kind == CastSourceKind.Scroll;

    /// <summary>The open scroll cast's scroll; null once spent (or for any other source).</summary>
    private ItemData _pendingScrollCastItem
    {
        get => _pendingScrollCastActive ? CurrentCastSource.Item : null;
        set { if (_pendingScrollCastActive) CurrentCastSource.Item = value; }
    }

    private int _pendingScrollCastInventoryIndex
    {
        get => _pendingScrollCastActive ? CurrentCastSource.ItemIndex : -1;
        set { if (_pendingScrollCastActive) CurrentCastSource.ItemIndex = value; }
    }

    /// <summary>True when the open cast uses a wand (its source kind).</summary>
    private bool _pendingWandCastActive => CurrentCastSource?.Kind == CastSourceKind.Wand;

    /// <summary>The open wand cast's wand; null once its charge is spent (or for any other source).</summary>
    private ItemData _pendingWandCastItem
    {
        get => _pendingWandCastActive ? CurrentCastSource.Item : null;
        set { if (_pendingWandCastActive) CurrentCastSource.Item = value; }
    }

    private int _pendingWandCastInventoryIndex
    {
        get => _pendingWandCastActive ? CurrentCastSource.ItemIndex : -1;
        set { if (_pendingWandCastActive) CurrentCastSource.ItemIndex = value; }
    }

    /// <summary>True when the open cast comes from the F12 Spell Testing Panel (its source kind).</summary>
    private bool _testPanelCastActive => CurrentCastSource?.Kind == CastSourceKind.TestPanel;
}
