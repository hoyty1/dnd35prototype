using System;
using System.Collections.Generic;
using DND35e.Identifiers;

/// <summary>
/// When a single-target handler may run, judged on the cast's <see cref="SpellResult"/> and save outcome just before
/// the call (batch 11 step 5, docs/designs/cast_core_batch11.md).
/// </summary>
internal enum HandlerGate
{
    /// <summary>Runs on every cast of its spell, even a failed one (the handler reads the result itself).</summary>
    None,
    /// <summary>Runs only when <see cref="SpellResult.Success"/> is true.</summary>
    Success,
    /// <summary>Runs only when the cast succeeded and a successful save did not negate the effect.</summary>
    SuccessNotNegated,
}

/// <summary>
/// One single-target spell handler in GameManager's registry (batch 11 step 5). <see cref="Resolve"/> wraps the
/// existing private <c>TryResolve&lt;Name&gt;SpellEffect</c> method unchanged; <see cref="Name"/> is that method's name
/// (Tests.Magic.SpellHandlerRegistryTests finds every such method by reflection and checks it is registered once).
/// </summary>
internal sealed class SpellHandlerEntry
{
    /// <summary>The wrapped method's name (nameof).</summary>
    public string Name;
    /// <summary>The spell ids the method claims (what its own id check accepts).</summary>
    public HashSet<string> SpellIds;
    /// <summary>The guard the cast must pass before the method is called.</summary>
    public HandlerGate Gate;
    /// <summary>
    /// The method adds to the cast and lets it go on: whatever it returns, the cast is not claimed, so the
    /// generic <c>ApplySpellBuff</c> step still runs (Shield Other's link, then its deflection and resistance buff).
    /// </summary>
    public bool ContinueToBuff;
    /// <summary>(caster, target, spell, result) → true when the method owns the cast.</summary>
    public Func<CharacterController, CharacterController, SpellData, SpellResult, bool> Resolve;
}

/// <summary>
/// What an area handler did with the cast (batch 11 step 9): claimed it with its log, did not claim it (the registry
/// goes on, and the generic loop takes what nothing claims), or is still resolving it (an Async handler, which resumes
/// the cast from its callback through GameManager.ResumeAreaCast).
/// </summary>
internal sealed class AreaHandlerResult
{
    public static readonly AreaHandlerResult NotClaimed = new AreaHandlerResult(false, false, null);
    public static readonly AreaHandlerResult Pending = new AreaHandlerResult(false, true, null);

    public readonly bool IsClaimed;
    public readonly bool IsPending;

    /// <summary>The handler's log, without the cast's quickened and spontaneous prefixes (FinishAreaCast adds them).</summary>
    public readonly string Log;

    private AreaHandlerResult(bool claimed, bool pending, string log)
    {
        IsClaimed = claimed;
        IsPending = pending;
        Log = log;
    }

    public static AreaHandlerResult Claimed(string log) => new AreaHandlerResult(true, false, log);
}

/// <summary>Where an area handler's "handler" cast-stage mark goes (the side channel only; CastStageTrace).</summary>
internal enum AreaHandlerMark
{
    /// <summary>The registry marks the handler once it claimed the cast (the old blocks marked after the method returned true).</summary>
    AfterClaim,
    /// <summary>The registry marks the handler before it runs (the old blocks that tested the spell id themselves).</summary>
    BeforeResolve,
    /// <summary>The handler marks itself (the generic loop, after its fire notification).</summary>
    ByHandler,
}

/// <summary>
/// One area spell handler in GameManager's area registry (batch 11 step 9). <see cref="Resolve"/> wraps the existing
/// method (a <c>TryResolve*</c> or <c>TryHandle*</c> method, a Resolve*Spell method or a wall's callback resolver);
/// <see cref="Name"/> is that method's name, which the "handler" cast-stage mark carries.
/// </summary>
internal sealed class AreaHandlerEntry
{
    /// <summary>The wrapped method's name (nameof).</summary>
    public string Name;
    /// <summary>The spell ids the method claims (what its own id check accepts); null for the fallback, which takes any spell.</summary>
    public HashSet<string> SpellIds;
    /// <summary>
    /// The method answers through a callback (Wall of Ice's Reflex prompts, Wall of Force): <see cref="Resolve"/> returns
    /// <see cref="AreaHandlerResult.Pending"/> and the callback resumes the cast (GameManager.ResumeAreaCast).
    /// </summary>
    public bool Async;
    public AreaHandlerMark Mark;
    /// <summary>
    /// The source context of the handler's combat-end check (EvaluateCombatEnd), or null for a handler that has none
    /// today (<see cref="LegacyCastQuirk.AreaCombatEndOnlyInSomeHandlers"/>, CORE-039).
    /// </summary>
    public string CombatEndContext;
    /// <summary>The handler's log goes out without the quickened and spontaneous lines (<see cref="LegacyCastQuirk.AreaLogPrefixOnlyInSomeHandlers"/>, SPL-161).</summary>
    public bool OmitsLogPrefix;
    /// <summary>
    /// After its log and the UI refresh, a Concentration-duration cast calls the caster's concentration tracking with no
    /// tracked effect (Rainbow Pattern, as its old block did; BeginConcentrationTracking ignores a null effect, SPL-028).
    /// </summary>
    public bool BeginsConcentrationAfterLog;
    /// <summary>The label of the console copy of the log (GameManager.LogAttacksToConsole), or null for none.</summary>
    public string ConsoleLogLabel;
    public Func<CastContext, AreaHandlerResult> Resolve;

    public bool IsFallback => SpellIds == null;
}

/// <summary>
/// GameManager partial class: the single-target spell handler registry (batch 11 step 5, handler-registry-pc,
/// docs/designs/cast_core_batch11.md).
///
/// <see cref="PerformSpellCast"/> used to call its 51 single-target handlers through a cascade of
/// <c>if (!handledA &amp;&amp; !handledB ...)</c> guards in four flag groups (anyPriorHandled, then the 2nd/3rd-level
/// cleric group whose aggregate anyClericHandled left Shield Other out, then the 4th-level cleric group and the domain
/// group, which did not check each other). Every handler returns false at once, with no side effect, for any spell id
/// but its own, and no spell id is claimed by two handlers (checked by Tests.Magic.SpellHandlerRegistryTests). So for a
/// given spell at most one handler can do anything, every flag of every other handler is false, and the cascade reduces
/// to: find the handler for the spell's id, check its gate, call it, and the cast is claimed when it returned true
/// (Shield Other and the Sound Burst stun excepted: they continue to the buff step). The table keeps the old chain
/// order so a later overlap would still resolve in that order.
///
/// <see cref="RunSingleTargetHandlers"/> is the handler step of single-target casts: PerformSpellCast (PC, since step 5)
/// and TryNPCPerformSpellCast (NPC, since step 6, which replaced the NPC path's own 19-handler chain; SPL-054), and since
/// steps 7 and 8 the cast core's Handlers stage runs it for both, with the same gates, so a handler resolves the same for
/// every caster. A handler must read the caster it is given,
/// never ActivePC, IsControllable or a <c>_pending*</c> choice (Dimension Door's destination search still forks on
/// IsPlayerControlled, a known divergence filed in SPL-091); a spell whose handler needs a choice the AI cannot give
/// yet (Dimension Door's destination, Imbue with Spell Ability's slot picker) is kept out of the AI's candidates by
/// <c>AISpellcastingStrategist.NeedsCasterChoiceAiCannotGive</c> (SPL-091), not forked here.
/// To add a single-target handler: write the method and add one line to <see cref="BuildSingleTargetHandlers"/>.
///
/// The area registry (batch 11 step 9, cast-core-area): <see cref="BuildAreaHandlers"/> holds the area handlers of the
/// old PerformAoESpellCast in its block order, each a lambda over the unchanged method (the five Resolve*Spell methods
/// that showed their own log and paced the turn now return their log), with the generic per-target loop
/// (ResolveGenericAreaSpell) as the fallback. The cast core's area Resolve stage (GameManager.CastCore.cs,
/// CastStageResolveArea) runs the entry registered for the spell's id, then the fallback, and FinishAreaCast shows the
/// log once, with the cast's prefixes, and runs the combat-end check. To add an area handler: write the method and add
/// one line to <see cref="BuildAreaHandlers"/>.
/// </summary>
public partial class GameManager
{
    private List<SpellHandlerEntry> _singleTargetHandlers;
    private Dictionary<string, List<SpellHandlerEntry>> _singleTargetHandlersBySpellId;

    private static List<SpellHandlerEntry> _singleTargetHandlerManifest;

    /// <summary>
    /// The registry in the old PC chain order. <paramref name="gm"/> is the instance whose methods the entries call;
    /// with null the entries describe the table (names, ids, gates) and must not be resolved.
    /// </summary>
    private static List<SpellHandlerEntry> BuildSingleTargetHandlers(GameManager gm)
    {
        const HandlerGate None = HandlerGate.None;
        const HandlerGate Ok = HandlerGate.Success;
        const HandlerGate OkKept = HandlerGate.SuccessNotNegated;

        var list = new List<SpellHandlerEntry>(51);
        void Add(string name, HandlerGate gate, Func<CharacterController, CharacterController, SpellData, SpellResult, bool> resolve, params string[] ids)
            => list.Add(new SpellHandlerEntry { Name = name, Gate = gate, Resolve = resolve, SpellIds = new HashSet<string>(ids, StringComparer.Ordinal) });
        void AddContinuing(string name, HandlerGate gate, Func<CharacterController, CharacterController, SpellData, SpellResult, bool> resolve, params string[] ids)
            => list.Add(new SpellHandlerEntry { Name = name, Gate = gate, Resolve = resolve, ContinueToBuff = true, SpellIds = new HashSet<string>(ids, StringComparer.Ordinal) });

        // Old group 1 (anyPriorHandled).
        Add(nameof(TryResolveCauseFearSpellEffect), None, (c, t, s, r) => gm.TryResolveCauseFearSpellEffect(c, t, s, r), SpellNames.CAUSE_FEAR);
        Add(nameof(TryResolveGhoulTouchSpellEffect), None, (c, t, s, r) => gm.TryResolveGhoulTouchSpellEffect(c, t, s, r), SpellNames.GHOUL_TOUCH);
        Add(nameof(TryResolveScareSpellEffect), None, (c, t, s, r) => gm.TryResolveScareSpellEffect(c, t, s, r), SpellNames.SCARE);
        Add(nameof(TryResolveRayOfEnfeeblementSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveRayOfEnfeeblementSpellEffect(c, t, s, r), SpellNames.RAY_OF_ENFEEBLEMENT);
        Add(nameof(TryResolveTouchOfIdiocySpellEffect), OkKept, (c, t, s, r) => gm.TryResolveTouchOfIdiocySpellEffect(c, t, s, r), SpellNames.TOUCH_OF_IDIOCY);
        Add(nameof(TryResolveMelfsAcidArrowSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveMelfsAcidArrowSpellEffect(c, t, s, r), SpellNames.MELFS_ACID_ARROW);
        Add(nameof(TryResolveRayOfExhaustionSpellEffect), Ok, (c, t, s, r) => gm.TryResolveRayOfExhaustionSpellEffect(c, t, s, r), SpellNames.RAY_OF_EXHAUSTION);
        Add(nameof(TryResolveVampiricTouchSpellEffect), Ok, (c, t, s, r) => gm.TryResolveVampiricTouchSpellEffect(c, t, s, r), SpellNames.VAMPIRIC_TOUCH);
        Add(nameof(TryResolveEnervationSpellEffect), Ok, (c, t, s, r) => gm.TryResolveEnervationSpellEffect(c, t, s, r), SpellNames.ENERVATION);
        Add(nameof(TryResolveContagionSpellEffect), Ok, (c, t, s, r) => gm.TryResolveContagionSpellEffect(c, t, s, r), SpellNames.CONTAGION);
        Add(nameof(TryResolveBestowCurseSpellEffect), Ok, (c, t, s, r) => gm.TryResolveBestowCurseSpellEffect(c, t, s, r), SpellNames.BESTOW_CURSE);
        Add(nameof(TryResolveGreaterInvisibilitySpellEffect), Ok, (c, t, s, r) => gm.TryResolveGreaterInvisibilitySpellEffect(c, t, s, r), SpellNames.GREATER_INVISIBILITY);
        Add(nameof(TryResolvePhantasmalKillerSpellEffect), Ok, (c, t, s, r) => gm.TryResolvePhantasmalKillerSpellEffect(c, t, s, r), SpellNames.PHANTASMAL_KILLER);
        Add(nameof(TryResolveFireShieldSpellEffect), Ok, (c, t, s, r) => gm.TryResolveFireShieldSpellEffect(c, t, s, r), SpellNames.FIRE_SHIELD);
        Add(nameof(TryResolveResilientSphereSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveResilientSphereSpellEffect(c, t, s, r), SpellNames.RESILIENT_SPHERE);
        Add(nameof(TryResolveAnimateRopeSpellEffect), None, (c, t, s, r) => gm.TryResolveAnimateRopeSpellEffect(c, t, s, r), SpellNames.ANIMATE_ROPE);
        Add(nameof(TryResolveMirrorImageSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveMirrorImageSpellEffect(c, t, s, r), SpellNames.MIRROR_IMAGE);
        Add(nameof(TryResolveDimensionalAnchorSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveDimensionalAnchorSpellEffect(c, t, s, r), SpellNames.DIMENSIONAL_ANCHOR);
        Add(nameof(TryResolveRemoveCurseSpellEffect), Ok, (c, t, s, r) => gm.TryResolveRemoveCurseSpellEffect(c, t, s, r), SpellNames.REMOVE_CURSE);
        Add(nameof(TryResolveDimensionDoorSpellEffect), Ok, (c, t, s, r) => gm.TryResolveDimensionDoorSpellEffect(c, t, s, r), SpellNames.DIMENSION_DOOR);
        Add(nameof(TryResolveLesserGlobeSpellEffect), Ok, (c, t, s, r) => gm.TryResolveLesserGlobeSpellEffect(c, t, s, r), SpellNames.LESSER_GLOBE_OF_INVULNERABILITY);

        // Old group 2: 2nd- and 3rd-level cleric spells (anyClericHandled, which left Shield Other out).
        Add(nameof(TryResolveDeathKnellSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveDeathKnellSpellEffect(c, t, s, r), SpellNames.DEATH_KNELL);
        AddContinuing(nameof(TryResolveShieldOtherSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveShieldOtherSpellEffect(c, t, s, r), SpellNames.SHIELD_OTHER);
        Add(nameof(TryResolveSilenceSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveSilenceSpellEffect(c, t, s, r), SpellNames.SILENCE);
        AddContinuing(nameof(TryResolveSoundBurstStunEffect), None, (c, t, s, r) => gm.TryResolveSoundBurstStunEffect(c, t, s, r), SpellNames.SOUND_BURST);
        Add(nameof(TryResolveSpiritualWeaponSpellEffect), Ok, (c, t, s, r) => gm.TryResolveSpiritualWeaponSpellEffect(c, t, s, r), SpellNames.SPIRITUAL_WEAPON);
        Add(nameof(TryResolveAlignWeaponSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveAlignWeaponSpellEffect(c, t, s, r), SpellNames.ALIGN_WEAPON);
        Add(nameof(TryResolveSearingLightSpellEffect), Ok, (c, t, s, r) => gm.TryResolveSearingLightSpellEffect(c, t, s, r), SpellNames.SEARING_LIGHT);
        Add(nameof(TryResolveInvisibilityPurgeSpellEffect), Ok, (c, t, s, r) => gm.TryResolveInvisibilityPurgeSpellEffect(c, t, s, r), SpellNames.INVISIBILITY_PURGE);
        Add(nameof(TryResolveRemoveDiseaseSpellEffect), Ok, (c, t, s, r) => gm.TryResolveRemoveDiseaseSpellEffect(c, t, s, r), SpellNames.REMOVE_DISEASE);
        Add(nameof(TryResolvePrayerSpellEffect), Ok, (c, t, s, r) => gm.TryResolvePrayerSpellEffect(c, t, s, r), SpellNames.PRAYER);
        Add(nameof(TryResolveRemoveBlindnessDeafnessSpellEffect), Ok, (c, t, s, r) => gm.TryResolveRemoveBlindnessDeafnessSpellEffect(c, t, s, r), SpellNames.REMOVE_BLINDNESS_DEAFNESS);

        // Old group 3: 4th-level cleric spells.
        Add(nameof(TryResolveChaosHammerSpellEffect), Ok, (c, t, s, r) => gm.TryResolveChaosHammerSpellEffect(c, t, s, r), SpellNames.CHAOS_HAMMER);
        Add(nameof(TryResolveHolySmiteSpellEffect), Ok, (c, t, s, r) => gm.TryResolveHolySmiteSpellEffect(c, t, s, r), SpellNames.HOLY_SMITE);
        Add(nameof(TryResolveOrdersWrathSpellEffect), Ok, (c, t, s, r) => gm.TryResolveOrdersWrathSpellEffect(c, t, s, r), SpellNames.ORDERS_WRATH);
        Add(nameof(TryResolveUnholyBlightSpellEffect), Ok, (c, t, s, r) => gm.TryResolveUnholyBlightSpellEffect(c, t, s, r), SpellNames.UNHOLY_BLIGHT);
        Add(nameof(TryResolveDeathWardSpellEffect), Ok, (c, t, s, r) => gm.TryResolveDeathWardSpellEffect(c, t, s, r), SpellNames.DEATH_WARD);
        Add(nameof(TryResolveDivinePowerSpellEffect), Ok, (c, t, s, r) => gm.TryResolveDivinePowerSpellEffect(c, t, s, r), SpellNames.DIVINE_POWER);
        Add(nameof(TryResolveFreedomOfMovementSpellEffect), Ok, (c, t, s, r) => gm.TryResolveFreedomOfMovementSpellEffect(c, t, s, r), SpellNames.FREEDOM_OF_MOVEMENT);
        Add(nameof(TryResolveSpellImmunitySpellEffect), Ok, (c, t, s, r) => gm.TryResolveSpellImmunitySpellEffect(c, t, s, r), SpellNames.SPELL_IMMUNITY);
        Add(nameof(TryResolveNeutralizePoisonSpellEffect), Ok, (c, t, s, r) => gm.TryResolveNeutralizePoisonSpellEffect(c, t, s, r), SpellNames.NEUTRALIZE_POISON);
        Add(nameof(TryResolvePoisonSpellEffect), Ok, (c, t, s, r) => gm.TryResolvePoisonSpellEffect(c, t, s, r), SpellNames.POISON);
        Add(nameof(TryResolveDismissalSpellEffect), Ok, (c, t, s, r) => gm.TryResolveDismissalSpellEffect(c, t, s, r), SpellNames.DISMISSAL);
        Add(nameof(TryResolveRepelVerminSpellEffect), Ok, (c, t, s, r) => gm.TryResolveRepelVerminSpellEffect(c, t, s, r), SpellNames.REPEL_VERMIN);
        Add(nameof(TryResolveImbueWithSpellAbilitySpellEffect), Ok, (c, t, s, r) => gm.TryResolveImbueWithSpellAbilitySpellEffect(c, t, s, r), SpellNames.IMBUE_WITH_SPELL_ABILITY);

        // Old group 4: domain spells.
        Add(nameof(TryResolveHoldAnimalSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveHoldAnimalSpellEffect(c, t, s, r), SpellNames.HOLD_ANIMAL);
        Add(nameof(TryResolveProduceFlameSpellEffect), Ok, (c, t, s, r) => gm.TryResolveProduceFlameSpellEffect(c, t, s, r), SpellNames.PRODUCE_FLAME);
        Add(nameof(TryResolveHeatMetalSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveHeatMetalSpellEffect(c, t, s, r), SpellNames.HEAT_METAL);
        Add(nameof(TryResolveMagicVestmentSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveMagicVestmentSpellEffect(c, t, s, r), SpellNames.MAGIC_VESTMENT);
        Add(nameof(TryResolveDominateAnimalSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveDominateAnimalSpellEffect(c, t, s, r), SpellNames.DOMINATE_ANIMAL);
        Add(nameof(TryResolveCommandPlantsSpellEffect), OkKept, (c, t, s, r) => gm.TryResolveCommandPlantsSpellEffect(c, t, s, r), SpellNames.COMMAND_PLANTS);

        return list;
    }

    private static Dictionary<string, List<SpellHandlerEntry>> IndexBySpellId(List<SpellHandlerEntry> entries)
    {
        var index = new Dictionary<string, List<SpellHandlerEntry>>(StringComparer.Ordinal);
        foreach (SpellHandlerEntry entry in entries)
        {
            foreach (string id in entry.SpellIds)
            {
                if (!index.TryGetValue(id, out List<SpellHandlerEntry> forId))
                {
                    forId = new List<SpellHandlerEntry>(1);
                    index[id] = forId;
                }
                forId.Add(entry); // table order, so an overlap would resolve in the old chain order
            }
        }
        return index;
    }

    /// <summary>
    /// The registry's table (names, spell ids, gates, ContinueToBuff) without an instance behind it, for the static
    /// suite. Its <see cref="SpellHandlerEntry.Resolve"/> delegates must not be called.
    /// </summary>
    internal static IReadOnlyList<SpellHandlerEntry> DescribeSingleTargetHandlers()
    {
        if (_singleTargetHandlerManifest == null)
            _singleTargetHandlerManifest = BuildSingleTargetHandlers(null);
        return _singleTargetHandlerManifest;
    }

    private void EnsureSingleTargetHandlers()
    {
        if (_singleTargetHandlers != null)
            return;
        _singleTargetHandlers = BuildSingleTargetHandlers(this);
        _singleTargetHandlersBySpellId = IndexBySpellId(_singleTargetHandlers);
    }

    /// <summary>Whether a cast with this result and save outcome passes <paramref name="gate"/>.</summary>
    internal static bool HandlerGatePasses(HandlerGate gate, SpellResult result, bool negated)
    {
        switch (gate)
        {
            case HandlerGate.None:
                return true;
            case HandlerGate.Success:
                return result != null && result.Success;
            case HandlerGate.SuccessNotNegated:
                return result != null && result.Success && !negated;
            default:
                return false;
        }
    }

    /// <summary>
    /// The handler step of a single-target cast: runs the registered handler for <paramref name="spell"/>'s id when its
    /// gate passes. Returns true when the cast is claimed, i.e. the handler returned true and is not a ContinueToBuff
    /// entry; a claimed cast skips the generic ApplySpellBuff step. <paramref name="continueToBuff"/> is true when a
    /// ContinueToBuff entry (Shield Other, the Sound Burst stun) ran.
    /// </summary>
    private bool RunSingleTargetHandlers(CharacterController caster, CharacterController target, SpellData spell,
        SpellResult result, bool negated, out bool continueToBuff)
    {
        continueToBuff = false;
        if (spell == null || string.IsNullOrEmpty(spell.SpellId))
            return false;

        EnsureSingleTargetHandlers();
        if (!_singleTargetHandlersBySpellId.TryGetValue(spell.SpellId, out List<SpellHandlerEntry> entries))
            return false;

        foreach (SpellHandlerEntry entry in entries)
        {
            if (!HandlerGatePasses(entry.Gate, result, negated))
                continue;

            bool returned = entry.Resolve(caster, target, spell, result);
            if (entry.ContinueToBuff)
            {
                continueToBuff = true;
                continue;
            }
            if (returned)
                return true;
        }
        return false;
    }

    // ── The area handler registry (batch 11 step 9) ─────────────────────────────────────────────────────────────

    private List<AreaHandlerEntry> _areaHandlers;
    private Dictionary<string, List<AreaHandlerEntry>> _areaHandlersBySpellId;
    private AreaHandlerEntry _areaFallbackHandler;

    private static List<AreaHandlerEntry> _areaHandlerManifest;

    /// <summary>
    /// The area registry in the old PerformAoESpellCast block order, the generic per-target loop last as the fallback.
    /// <paramref name="gm"/> is the instance whose methods the entries call; with null the entries describe the table and
    /// must not be resolved. Every handler's own guard is a plain spell id test (after null tests) with no side effect
    /// before it (read 2026-10-15), and no id is claimed twice (Tests.Magic.SpellHandlerRegistryTests), so dispatching by
    /// id runs exactly the handler the old chain of blocks reached.
    /// </summary>
    private static List<AreaHandlerEntry> BuildAreaHandlers(GameManager gm)
    {
        var list = new List<AreaHandlerEntry>(41);
        AreaHandlerEntry Add(string name, string combatEnd, Func<CastContext, AreaHandlerResult> resolve, params string[] ids)
        {
            var entry = new AreaHandlerEntry
            {
                Name = name,
                CombatEndContext = combatEnd,
                Resolve = resolve,
                Mark = AreaHandlerMark.AfterClaim,
                SpellIds = new HashSet<string>(ids, StringComparer.Ordinal)
            };
            list.Add(entry);
            return entry;
        }
        // A Resolve*Spell handler that used to show its own log and pace the turn: marked before it runs, its log without
        // the prefixes (SPL-161), its own combat-end check under its own name.
        void AddSelfPaced(string name, Func<CastContext, string> resolve, string id)
        {
            AreaHandlerEntry entry = Add(name, name, ctx => AreaHandlerResult.Claimed(resolve(ctx)), id);
            entry.Mark = AreaHandlerMark.BeforeResolve;
            entry.OmitsLogPrefix = true;
        }
        AreaHandlerResult Claim(bool claimed, string log) => claimed ? AreaHandlerResult.Claimed(log) : AreaHandlerResult.NotClaimed;

        Add(nameof(TryHandleConcealmentAreaSpellCast), null,
            ctx => Claim(gm.TryHandleConcealmentAreaSpellCast(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.Cells, ctx.Request.Aim.AreaTargets, out string log), log),
            SpellNames.OBSCURING_MIST, SpellNames.FOG_CLOUD, SpellNames.DARKNESS, SpellNames.GUST_OF_WIND, SpellNames.SLEET_STORM,
            SpellNames.STINKING_CLOUD, SpellNames.SOLID_FOG);
        Add(nameof(TryResolveConsecrateAreaEffect), null,
            ctx => Claim(gm.TryResolveConsecrateAreaEffect(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.Cells, ctx.Request.Aim.AreaTargets, out string log), log),
            SpellNames.CONSECRATE);
        Add(nameof(TryResolveDesecrateAreaEffect), null,
            ctx => Claim(gm.TryResolveDesecrateAreaEffect(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.Cells, ctx.Request.Aim.AreaTargets, out string log), log),
            SpellNames.DESECRATE);
        Add(nameof(TryResolveScaledAoEDamageSpell), "ResolveScaledAoEDamageSpell",
            ctx => Claim(gm.TryResolveScaledAoEDamageSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.FIREBALL, SpellNames.LIGHTNING_BOLT, SpellNames.CALL_LIGHTNING);
        Add(nameof(TryResolveDaylightSpell), null,
            ctx => Claim(gm.TryResolveDaylightSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.DAYLIGHT);
        Add(nameof(TryResolveWindWallSpell), null,
            ctx => Claim(gm.TryResolveWindWallSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.WIND_WALL);
        Add(nameof(TryResolveHaltUndeadSpell), "ResolveHaltUndeadSpell",
            ctx => Claim(gm.TryResolveHaltUndeadSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.HALT_UNDEAD);
        AddSelfPaced(nameof(ResolveHypnotismSpell), ctx => gm.ResolveHypnotismSpell(ctx.Request.Caster, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells), SpellNames.HYPNOTISM);
        AddSelfPaced(nameof(ResolveSleepSpell), ctx => gm.ResolveSleepSpell(ctx.Request.Caster, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells), SpellNames.SLEEP);
        AddSelfPaced(nameof(ResolveDeepSlumberSpell), ctx => gm.ResolveDeepSlumberSpell(ctx.Request.Caster, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells), SpellNames.DEEP_SLUMBER);
        AddSelfPaced(nameof(ResolveColorSpraySpell), ctx => gm.ResolveColorSpraySpell(ctx.Request.Caster, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells), SpellNames.COLOR_SPRAY);
        AddSelfPaced(nameof(ResolveFearSpell), ctx => gm.ResolveFearSpell(ctx.Request.Caster, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells), SpellNames.FEAR);
        Add(nameof(TryResolveGlitterdustSpell), null,
            ctx => Claim(gm.TryResolveGlitterdustSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.GLITTERDUST);
        Add(nameof(TryResolveWebSpell), null,
            ctx => Claim(gm.TryResolveWebSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.WEB);
        Add(nameof(TryResolveRainbowPatternAoE), null,
            ctx => Claim(gm.TryResolveRainbowPatternAoE(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.RAINBOW_PATTERN).BeginsConcentrationAfterLog = true;
        Add(nameof(TryResolveIceStormAoE), "IceStorm",
            ctx => Claim(gm.TryResolveIceStormAoE(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.ICE_STORM);
        Add(nameof(TryResolveShoutAoE), "Shout",
            ctx => Claim(gm.TryResolveShoutAoE(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.SHOUT);
        Add(nameof(TryResolveWallOfFireSpell), null,
            ctx => Claim(gm.TryResolveWallOfFireSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.WALL_OF_FIRE);
        AreaHandlerEntry wallOfIce = Add(nameof(ResolveWallOfIceWithReflexSaves), null, ctx =>
        {
            // The Reflex prompts of a player's creatures may answer later: the callback resumes the cast.
            gm.ResolveWallOfIceWithReflexSaves(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells,
                wallLog => gm.ResumeAreaCast(ctx, wallLog));
            return AreaHandlerResult.Pending;
        }, SpellNames.WALL_OF_ICE);
        wallOfIce.Async = true;
        wallOfIce.Mark = AreaHandlerMark.BeforeResolve;
        AreaHandlerEntry wallOfForce = Add(nameof(ResolveWallOfForce), null, ctx =>
        {
            gm.ResolveWallOfForce(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells,
                forceLog => gm.ResumeAreaCast(ctx, forceLog));
            return AreaHandlerResult.Pending;
        }, SpellNames.WALL_OF_FORCE);
        wallOfForce.Async = true;
        wallOfForce.Mark = AreaHandlerMark.BeforeResolve;
        Add(nameof(TryResolveFlamingSphereAoECast), null,
            ctx => Claim(gm.TryResolveFlamingSphereAoECast(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.FLAMING_SPHERE);
        Add(nameof(TryResolveEntangleSpell), null,
            ctx => Claim(gm.TryResolveEntangleSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.ENTANGLE);
        Add(nameof(TryResolveSoftenEarthSpell), null,
            ctx => Claim(gm.TryResolveSoftenEarthSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.SOFTEN_EARTH);
        Add(nameof(TryResolveSpikeStoneSpell), null,
            ctx => Claim(gm.TryResolveSpikeStoneSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.SPIKE_STONES);
        Add(nameof(TryResolvePlantGrowthSpell), null,
            ctx => Claim(gm.TryResolvePlantGrowthSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.PLANT_GROWTH);
        Add(nameof(TryResolveCalmAnimalsSpell), null,
            ctx => Claim(gm.TryResolveCalmAnimalsSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.CALM_ANIMALS);
        Add(nameof(TryResolveCalmEmotionsSpell), null,
            ctx => Claim(gm.TryResolveCalmEmotionsSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.CALM_EMOTIONS);
        Add(nameof(TryResolveConeOfColdSpell), "ConeOfCold",
            ctx => Claim(gm.TryResolveConeOfColdSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.CONE_OF_COLD);
        Add(nameof(TryResolveChainLightningSpell), "ChainLightning",
            ctx => Claim(gm.TryResolveChainLightningSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.CHAIN_LIGHTNING);
        Add(nameof(TryResolveCircleOfDeathSpell), "CircleOfDeath",
            ctx => Claim(gm.TryResolveCircleOfDeathSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.CIRCLE_OF_DEATH);
        Add(nameof(TryResolveCrushingDespairSpell), null,
            ctx => Claim(gm.TryResolveCrushingDespairSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.CRUSHING_DESPAIR);
        Add(nameof(TryResolveMindFogSpell), null,
            ctx => Claim(gm.TryResolveMindFogSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.MIND_FOG);
        Add(nameof(TryResolveMassSuggestionSpell), null,
            ctx => Claim(gm.TryResolveMassSuggestionSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.MASS_SUGGESTION);
        Add(nameof(TryResolveInsectPlagueSpell), "InsectPlague",
            ctx => Claim(gm.TryResolveInsectPlagueSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.INSECT_PLAGUE);
        Add(nameof(TryResolveWallOfThornsSpell), "WallOfThorns",
            ctx => Claim(gm.TryResolveWallOfThornsSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.WALL_OF_THORNS);
        Add(nameof(TryResolvePersistentImageSpell), null,
            ctx => Claim(gm.TryResolvePersistentImageSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.PERSISTENT_IMAGE);
        Add(nameof(TryResolveSunburstSpell), "Sunburst",
            ctx => Claim(gm.TryResolveSunburstSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.SUNBURST);
        Add(nameof(TryResolveEarthquakeSpell), "Earthquake",
            ctx => Claim(gm.TryResolveEarthquakeSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.EARTHQUAKE);
        Add(nameof(TryResolveShieldOfLawSpell), null,
            ctx => Claim(gm.TryResolveShieldOfLawSpell(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.AreaTargets, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.SHIELD_OF_LAW);
        Add(nameof(TryResolveBlackTentaclesAoECast), null,
            ctx => Claim(gm.TryResolveBlackTentaclesAoECast(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Aim.Cells, out string log), log),
            SpellNames.EVARDS_BLACK_TENTACLES);

        // The fallback: the generic per-target loop takes every area spell no handler claimed.
        list.Add(new AreaHandlerEntry
        {
            Name = nameof(ResolveGenericAreaSpell),
            SpellIds = null,
            Mark = AreaHandlerMark.ByHandler,
            CombatEndContext = "ResolveAOESpell",
            ConsoleLogLabel = "[AoE Spell] ",
            Resolve = ctx => AreaHandlerResult.Claimed(gm.ResolveGenericAreaSpell(ctx))
        });
        return list;
    }

    /// <summary>
    /// The area registry's table (names, spell ids, flags) without an instance behind it, for the static suite. Its
    /// <see cref="AreaHandlerEntry.Resolve"/> delegates must not be called.
    /// </summary>
    internal static IReadOnlyList<AreaHandlerEntry> DescribeAreaHandlers()
    {
        if (_areaHandlerManifest == null)
            _areaHandlerManifest = BuildAreaHandlers(null);
        return _areaHandlerManifest;
    }

    private void EnsureAreaHandlers()
    {
        if (_areaHandlers != null)
            return;
        _areaHandlers = BuildAreaHandlers(this);
        _areaHandlersBySpellId = new Dictionary<string, List<AreaHandlerEntry>>(StringComparer.Ordinal);
        foreach (AreaHandlerEntry entry in _areaHandlers)
        {
            if (entry.IsFallback)
            {
                _areaFallbackHandler = entry;
                continue;
            }
            foreach (string id in entry.SpellIds)
            {
                if (!_areaHandlersBySpellId.TryGetValue(id, out List<AreaHandlerEntry> forId))
                {
                    forId = new List<AreaHandlerEntry>(1);
                    _areaHandlersBySpellId[id] = forId;
                }
                forId.Add(entry); // table order, so an overlap would resolve in the old block order
            }
        }
    }

    /// <summary>
    /// The area handlers to try for <paramref name="spellId"/>, in the old block order: the ones registered for the id,
    /// then the generic loop (the fallback, which always claims).
    /// </summary>
    private List<AreaHandlerEntry> AreaHandlersFor(string spellId)
    {
        EnsureAreaHandlers();
        var entries = new List<AreaHandlerEntry>(2);
        if (!string.IsNullOrEmpty(spellId) && _areaHandlersBySpellId.TryGetValue(spellId, out List<AreaHandlerEntry> forId))
            entries.AddRange(forId);
        entries.Add(_areaFallbackHandler);
        return entries;
    }
}
