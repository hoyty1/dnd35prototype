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
/// <see cref="RunSingleTargetHandlers"/> is the PC path's handler step. The NPC path (TryNPCPerformSpellCast) still
/// runs its own 19-handler chain (SPL-054) and only records, in the scenario harness's cast-stage side channel, what
/// the registry would have claimed (<see cref="PredictSingleTargetClaim"/>) and which continuing handler it would have
/// run (<see cref="PredictSingleTargetContinuingEntry"/>; the NPC chain has neither); step 6 switches it to the registry.
/// To add a single-target handler: write the method and add one line to <see cref="BuildSingleTargetHandlers"/>.
/// </summary>
public partial class GameManager
{
    private List<SpellHandlerEntry> _singleTargetHandlers;
    private Dictionary<string, List<SpellHandlerEntry>> _singleTargetHandlersBySpellId;

    private static List<SpellHandlerEntry> _singleTargetHandlerManifest;
    private static Dictionary<string, List<SpellHandlerEntry>> _singleTargetHandlerManifestBySpellId;

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
    /// The registry's table (names, spell ids, gates, ContinueToBuff) without an instance behind it, for
    /// <see cref="PredictSingleTargetClaim"/> and the static suite. Its <see cref="SpellHandlerEntry.Resolve"/>
    /// delegates must not be called.
    /// </summary>
    internal static IReadOnlyList<SpellHandlerEntry> DescribeSingleTargetHandlers()
    {
        EnsureSingleTargetHandlerManifest();
        return _singleTargetHandlerManifest;
    }

    private static void EnsureSingleTargetHandlerManifest()
    {
        if (_singleTargetHandlerManifest != null)
            return;
        _singleTargetHandlerManifest = BuildSingleTargetHandlers(null);
        _singleTargetHandlerManifestBySpellId = IndexBySpellId(_singleTargetHandlerManifest);
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

    /// <summary>
    /// Pure prediction of <see cref="RunSingleTargetHandlers"/>'s claim, running no handler: a non-ContinueToBuff entry
    /// for the spell's id whose gate passes. It cannot see a handler's own late refusals; those are null checks of the
    /// caster or target, and Ray of Enfeeblement's and Touch of Idiocy's attack-miss check, which the gate already
    /// covers (SpellCaster.Cast sets Success to false on a miss). Used for the NPC shadow ledger of batch 11 step 5.
    /// </summary>
    internal static bool PredictSingleTargetClaim(SpellData spell, SpellResult result, bool negated)
    {
        return PredictSingleTargetClaimEntry(spell, result, negated) != null;
    }

    /// <summary>The entry <see cref="PredictSingleTargetClaim"/> predicts would claim the cast, or null.</summary>
    internal static SpellHandlerEntry PredictSingleTargetClaimEntry(SpellData spell, SpellResult result, bool negated)
    {
        if (spell == null || string.IsNullOrEmpty(spell.SpellId))
            return null;

        EnsureSingleTargetHandlerManifest();
        if (!_singleTargetHandlerManifestBySpellId.TryGetValue(spell.SpellId, out List<SpellHandlerEntry> entries))
            return null;

        foreach (SpellHandlerEntry entry in entries)
        {
            if (!entry.ContinueToBuff && HandlerGatePasses(entry.Gate, result, negated))
                return entry;
        }
        return null;
    }

    /// <summary>
    /// The ContinueToBuff entry (Shield Other's empathic link, the Sound Burst stun) that <see cref="RunSingleTargetHandlers"/>
    /// would run for this cast without claiming it, or null; pure, runs no handler. The NPC shadow ledger reports it as
    /// <c>registryWouldRun</c>: the NPC chain runs neither of these handlers, so a non-null result is an NPC cast whose
    /// effects step 6 changes even though the claim agrees.
    /// </summary>
    internal static SpellHandlerEntry PredictSingleTargetContinuingEntry(SpellData spell, SpellResult result, bool negated)
    {
        if (spell == null || string.IsNullOrEmpty(spell.SpellId))
            return null;

        EnsureSingleTargetHandlerManifest();
        if (!_singleTargetHandlerManifestBySpellId.TryGetValue(spell.SpellId, out List<SpellHandlerEntry> entries))
            return null;

        foreach (SpellHandlerEntry entry in entries)
        {
            if (entry.ContinueToBuff && HandlerGatePasses(entry.Gate, result, negated))
                return entry;
        }
        return null;
    }
}
