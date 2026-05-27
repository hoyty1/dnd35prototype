using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Invisibility Sphere — D&D 3.5e PHB p.245 (Bard 3, Sorcerer/Wizard 3).
///
/// Illusion (Glamer). Range Personal or Touch. Area: 10-ft.-radius emanation
/// centered on the recipient. Duration: 1 min/level (D). Will negates
/// (harmless). SR: Yes (harmless).
///
/// MECHANICS:
///   • At cast time, every creature within the 10-ft emanation (including the
///     recipient) becomes invisible — granted total concealment (50% miss
///     chance), +2 to attack rolls, opponents lose Dex bonus to AC.
///   • The emanation moves with the recipient (mobile emanation).
///   • A creature that LEAVES the emanation immediately becomes visible
///     (the spell ends for that creature).
///   • Creatures that ENTER the emanation after the spell was cast do NOT
///     become invisible.
///   • If any AFFECTED creature OTHER THAN the recipient attacks, only that
///     creature becomes visible.
///   • If the RECIPIENT attacks, the entire spell ends and ALL affected
///     creatures become visible at once.
///
/// IMPLEMENTATION:
///   • Inherits from EmanationEffectData for mobile-emanation tracking.
///     Center = recipient's grid position; radius = 2 squares (10 ft).
///   • Each tick (round) the emanation re-checks affected creatures:
///       - Initially-affected creatures still in the area keep invisibility.
///       - Initially-affected creatures who left the area lose invisibility
///         (their per-creature invisibility is cleared).
///   • Per-creature invisibility uses
///     <see cref="CharacterController.ApplyInvisibilityEffectData"/> with a
///     bespoke <see cref="InvisibilityEffectData"/> instance that has
///     <c>BreaksOnAttack = false</c> for non-recipient affected creatures —
///     because attack handling for non-recipient affected creatures is
///     resolved by the sphere itself (we strip just that creature's
///     invisibility), not by the standard invisibility-on-attack flow.
///   • The recipient's per-creature invisibility uses
///     <c>BreaksOnAttack = true</c> with a sphere-aware end-for-all hook in
///     GameManager (see ApplyInvisibilitySphere wiring).
/// </summary>
[System.Serializable]
public class InvisibilitySphereEffect : EmanationEffectData
{
    // ═══════════════════════════════════════════════════════════════════
    //  CONSTANTS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>10-ft radius emanation (10 ft / 5 ft per square = 2 squares).</summary>
    public const int SphereRadiusSquares = 2;

    /// <summary>Display radius in feet (for UI / logs).</summary>
    public const float SphereRadiusFeet = 10f;

    // ═══════════════════════════════════════════════════════════════════
    //  STATE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Initially-affected creatures (those made invisible at cast time).</summary>
    [System.NonSerialized] public List<CharacterController> InitiallyAffectedCreatures = new List<CharacterController>();

    /// <summary>True once <see cref="EndForAll"/> has run, to make further calls idempotent.</summary>
    [System.NonSerialized] public bool HasEnded;

    // ═══════════════════════════════════════════════════════════════════
    //  CONSTRUCTION
    // ═══════════════════════════════════════════════════════════════════

    public InvisibilitySphereEffect()
    {
        RadiusSquares = SphereRadiusSquares;
        RadiusFeet = SphereRadiusFeet;
        SourceSpellId = SpellNames.INVISIBILITY_SPHERE;
    }

    /// <summary>
    /// Factory that prepares a fully wired Invisibility Sphere emanation.
    /// </summary>
    public static InvisibilitySphereEffect Create(
        CharacterController recipient,
        CharacterController caster,
        int durationRounds,
        int casterLevel)
    {
        var sphere = new InvisibilitySphereEffect
        {
            CenterCreature = recipient,
            RemainingRounds = Mathf.Max(1, durationRounds),
            CasterLevel = Mathf.Max(1, casterLevel),
            CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty,
        };
        return sphere;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EMANATION BASE OVERRIDES
    // ═══════════════════════════════════════════════════════════════════

    public override string GetEffectName() => "Invisibility Sphere";

    /// <summary>
    /// Called when a creature enters the area DURING refresh ticks.
    /// Per PHB: New creatures entering the area do NOT become invisible.
    /// </summary>
    public override void OnCreatureEntersArea(CharacterController creature)
    {
        // Intentionally no-op: only initially-affected creatures get invisibility.
    }

    /// <summary>
    /// Called when an initially-affected creature leaves the emanation.
    /// They become visible immediately and are removed from the affected list.
    /// </summary>
    public override void OnCreatureLeavesArea(CharacterController creature)
    {
        if (creature == null)
            return;

        // Only initially-affected creatures had invisibility from this sphere.
        if (!InitiallyAffectedCreatures.Contains(creature))
            return;

        InitiallyAffectedCreatures.Remove(creature);
        ClearSphereInvisibilityOn(creature, "stepped out of the sphere");
    }

    public override void ApplyEffectsToCreature(CharacterController creature)
    {
        // No-op: per-creature invisibility is set up at cast time by ApplyInitialAffectedCreatures.
    }

    public override void RemoveEffectsFromCreature(CharacterController creature)
    {
        if (creature == null)
            return;

        ClearSphereInvisibilityOn(creature, "spell ended");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// At cast time, capture every creature currently in the emanation
    /// (allies, enemies, the recipient itself) and apply per-creature
    /// invisibility tagged as belonging to this sphere.
    /// </summary>
    /// <param name="allCharacters">All characters to consider for area membership.</param>
    public void ApplyInitialAffectedCreatures(List<CharacterController> allCharacters)
    {
        InitiallyAffectedCreatures.Clear();

        if (allCharacters == null || allCharacters.Count == 0)
        {
            // Always include the recipient even if no other characters were supplied.
            if (CenterCreature != null && !CenterCreature.IsDead)
                ApplySphereInvisibilityTo(CenterCreature, isRecipient: true);
            return;
        }

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController creature = allCharacters[i];
            if (creature == null || creature.IsDead)
                continue;

            if (!IsCreatureInArea(creature))
                continue;

            ApplySphereInvisibilityTo(creature, isRecipient: creature == CenterCreature);
        }

        // Defensive: ensure the recipient is always tracked.
        if (CenterCreature != null && !CenterCreature.IsDead && !InitiallyAffectedCreatures.Contains(CenterCreature))
            ApplySphereInvisibilityTo(CenterCreature, isRecipient: true);
    }

    /// <summary>
    /// Refresh per-tick: prune creatures that have left the emanation and
    /// remove their invisibility. Does NOT add new creatures (per PHB).
    /// </summary>
    public void RefreshMembership()
    {
        if (HasEnded)
            return;

        // Iterate over a snapshot because OnCreatureLeavesArea mutates the list.
        var snapshot = new List<CharacterController>(InitiallyAffectedCreatures);
        for (int i = 0; i < snapshot.Count; i++)
        {
            CharacterController c = snapshot[i];
            if (c == null || c.IsDead)
            {
                InitiallyAffectedCreatures.Remove(c);
                continue;
            }

            if (!IsCreatureInArea(c))
                OnCreatureLeavesArea(c);
        }
    }

    /// <summary>
    /// Ends invisibility for one specific affected creature without ending
    /// the entire sphere. Used when an affected creature OTHER THAN the
    /// recipient attacks.
    /// </summary>
    public bool EndForCreature(CharacterController creature, string reason = "attacked")
    {
        if (creature == null || HasEnded)
            return false;

        if (!InitiallyAffectedCreatures.Contains(creature))
            return false;

        InitiallyAffectedCreatures.Remove(creature);
        ClearSphereInvisibilityOn(creature, reason);
        return true;
    }

    /// <summary>
    /// Ends the sphere for ALL initially-affected creatures and marks the
    /// emanation as expired (so GameManager's tick will purge it).
    /// Used when the recipient attacks, the duration expires, or it is
    /// dispelled/dismissed.
    /// </summary>
    public void EndForAll(string reason = "spell ended")
    {
        if (HasEnded)
            return;

        HasEnded = true;
        IsActive = false;

        // Snapshot to avoid mutation during iteration.
        var snapshot = new List<CharacterController>(InitiallyAffectedCreatures);
        for (int i = 0; i < snapshot.Count; i++)
        {
            CharacterController c = snapshot[i];
            if (c != null)
                ClearSphereInvisibilityOn(c, reason);
        }
        InitiallyAffectedCreatures.Clear();

        RemainingRounds = 0; // mark expired so emanation tick removes us
    }

    /// <summary>
    /// True if the supplied creature is currently invisible because of THIS
    /// sphere (i.e. is in the initially-affected list and still in the area).
    /// </summary>
    public bool IsCreatureAffected(CharacterController creature)
    {
        if (HasEnded || creature == null)
            return false;

        return InitiallyAffectedCreatures.Contains(creature) && IsCreatureInArea(creature);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  INTERNAL HELPERS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply per-creature invisibility tagged with this sphere's spell id.
    /// </summary>
    /// <param name="creature">Target creature.</param>
    /// <param name="isRecipient">Whether the creature is the sphere's center.</param>
    private void ApplySphereInvisibilityTo(CharacterController creature, bool isRecipient)
    {
        if (creature == null || creature.IsDead)
            return;

        // The sphere's own attack-handling logic is responsible for removing
        // invisibility when an affected creature attacks. We therefore mark
        // BreaksOnAttack = false on the per-creature invisibility data and let
        // GameManager intercept attacks made by sphere-affected creatures.
        var data = new InvisibilityEffectData
        {
            IsInvisible = true,
            DurationRemainingRounds = Mathf.Max(1, RemainingRounds),
            IsMoving = false,
            BreaksOnAttack = false,
            IsDismissible = true,
            ConcealmentMissChance = 50,
            HideBonusMoving = 20,
            HideBonusStationary = 40,
            SourceType = InvisibilitySourceType.Spell,
            SourceSpellId = SpellNames.INVISIBILITY_SPHERE,
            SourceName = "Invisibility Sphere",
        };

        CharacterController casterRef = CenterCreature; // best caster reference we have without a separate field
        data.SetCaster(casterRef);

        creature.ApplyInvisibilityEffectData(data);

        if (!InitiallyAffectedCreatures.Contains(creature))
            InitiallyAffectedCreatures.Add(creature);

        Debug.Log($"[InvisibilitySphere] {creature.Stats?.CharacterName} becomes invisible inside {GetEffectName()}{(isRecipient ? " (recipient/center)" : string.Empty)}.");
    }

    /// <summary>
    /// Clear sphere-granted invisibility on a creature.
    /// We only clear if the creature's current invisibility is actually from
    /// this sphere (don't accidentally strip Greater Invisibility, etc.).
    /// </summary>
    private void ClearSphereInvisibilityOn(CharacterController creature, string reason)
    {
        if (creature == null)
            return;

        var active = creature.ActiveInvisibilityEffect;
        if (active == null || !active.MatchesSpellId(SpellNames.INVISIBILITY_SPHERE))
            return;

        creature.ClearInvisibilityEffect();

        if (creature.HasCondition(CombatConditionType.Invisible))
            creature.RemoveCondition(CombatConditionType.Invisible);

        string actorName = creature.Stats != null ? creature.Stats.CharacterName : creature.name;
        GameManager.Instance?.CombatUI?.ShowCombatLog(
            $"<color=#88CCFF>👁 {actorName}'s Invisibility Sphere fades ({reason}).</color>");
    }
}
