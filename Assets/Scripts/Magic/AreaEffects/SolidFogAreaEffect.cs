using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Solid Fog (PHB 3.5e p.281): Conjuration (Creation).
/// Level: Sor/Wiz 4. Components: V, S, M (dried peas + powdered animal hoof).
/// Range: Medium (100 ft. + 10 ft./level).
/// Effect: Fog spreads in 20-ft. radius, 20 ft. high.
/// Duration: 1 min./level.
/// Saving Throw: None. Spell Resistance: No.
///
/// This spell functions like Fog Cloud, but in addition to obscuring sight,
/// the solid fog is so thick that any creature attempting to move through it
/// progresses at a speed of 5 feet, regardless of its normal speed, and it
/// takes a -2 penalty on all melee attack and damage rolls. The vapors
/// prevent effective ranged weapon attacks (except for magic rays and the
/// like). A creature or object that falls into solid fog is slowed, so that
/// each 10 feet of vapor that it passes through reduces falling damage by
/// 1d6. A creature cannot take a 5-foot step while in solid fog.
/// A moderate wind (11+ mph) disperses the fog in 4 rounds; a strong wind
/// (21+ mph) disperses it in 1 round.
///
/// Implementation notes:
///   - Same concealment as Fog Cloud (20% at 5 ft, 50% beyond)
///   - Half movement speed for creatures inside
///   - -2 melee attack, -2 melee damage penalties
///   - Blocks normal ranged weapon attacks (not magic rays)
///   - Dispersible by strong wind (WindStrength.Strong)
/// </summary>
public class SolidFogAreaEffect : PersistentAreaEffect
{
    // ═══════════════════════════════════════════════════════════════
    // CONSTANTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Penalty to melee attack rolls for creatures inside solid fog.</summary>
    public const int MeleeAttackPenalty = -2;

    /// <summary>Penalty to melee damage rolls for creatures inside solid fog.</summary>
    public const int MeleeDamagePenalty = -2;

    /// <summary>Speed multiplier for creatures moving through solid fog (half speed).</summary>
    public const float SpeedMultiplier = 0.5f;

    private const string ConcealmentSpellId = "solid_fog_concealment";

    // ═══════════════════════════════════════════════════════════════
    // GRID HIGHLIGHT
    // ═══════════════════════════════════════════════════════════════

    protected override Color GridHighlightColor => AreaEffectColors.SolidFog;
    protected override bool UseGridHighlighting => true;

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Solid Fog";
        SpellId = SpellNames.SOLID_FOG;
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius (4 squares × 5 ft)

        // Replace opaque area mesh with per-cell shading.
        ShowVisual = false;

        // Solid Fog is dispersed by strong wind (21+ mph), not moderate like regular fog
        DispersibleByWind = true;
        RequiredWindStrength = WindStrength.Strong;
    }

    protected override void OnAreaCreated()
    {
        LogEffect("Solid fog fills a 20-ft radius spread, 20 ft. high.");
        LogEffect("  • Concealment: 20% miss chance at 5 ft, 50% (total) beyond 5 ft.");
        LogEffect("  • Movement speed halved inside the fog.");
        LogEffect("  • -2 penalty to melee attack and damage rolls.");
        LogEffect("  • Normal ranged weapon attacks blocked (magic rays still work).");
        LogEffect("  • Dispersed by strong wind (21+ mph).");
    }

    private void Update()
    {
        // Keep occupancy/concealment state synchronized while creatures move mid-round.
        UpdateCharacterTracking();

        // Defensive re-apply in case grid initializes slightly after the area object.
        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    public override void OnRoundStart()
    {
        base.OnRoundStart();
        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    // ═══════════════════════════════════════════════════════════════
    // CREATURE ENTER / EXIT / ROUND START
    // ═══════════════════════════════════════════════════════════════

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        string timing = isInitial ? "is within" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} the solid fog.");
        ApplyConcealment(character);
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        ApplyConcealment(character);
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        RemoveConcealment(character);
        LogEffect($"{character.Stats.CharacterName} leaves the solid fog and loses concealment from it.");
    }

    protected override void OnAreaExpires()
    {
        foreach (CharacterController character in CharactersInArea)
        {
            if (character != null)
                RemoveConcealment(character);
        }

        RemoveGridHighlight();
        LogEffect("Solid Fog dissipates.");
    }

    // ═══════════════════════════════════════════════════════════════
    // CONCEALMENT — same rules as Fog Cloud
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Get concealment miss chance for attacks through the solid fog.
    /// Within 5 ft (1 square): 20%. Beyond 5 ft: 50% (total concealment).
    /// </summary>
    public int GetConcealmentMissChance(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
            return 0;

        // Concealment is granted by the target being inside the fog.
        if (!IsCharacterInArea(target))
            return 0;

        int distanceSquares = attacker.GetMinimumDistanceToTarget(target, chebyshev: true);
        return distanceSquares <= 1 ? 20 : 50;
    }

    public bool GrantsTotalConcealmentAgainst(CharacterController attacker, CharacterController target)
    {
        return GetConcealmentMissChance(attacker, target) >= 50;
    }

    // ═══════════════════════════════════════════════════════════════
    // STATIC HELPERS — for combat and movement systems
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a creature is in any active Solid Fog area effect.
    /// Used by combat/movement systems to apply penalties.
    /// </summary>
    public static bool IsCreatureInAnySolidFog(CharacterController creature)
    {
        if (creature == null || !AreaEffectManager.HasInstance)
            return false;

        var fogs = AreaEffectManager.Instance.GetEffectsOfType<SolidFogAreaEffect>();
        for (int i = 0; i < fogs.Count; i++)
        {
            if (fogs[i] != null && fogs[i].IsCharacterInArea(creature))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get the melee attack roll penalty for a creature inside solid fog.
    /// Returns -2 if in solid fog, 0 otherwise.
    /// </summary>
    public static int GetMeleeAttackPenaltyFor(CharacterController creature)
    {
        return IsCreatureInAnySolidFog(creature) ? MeleeAttackPenalty : 0;
    }

    /// <summary>
    /// Get the melee damage roll penalty for a creature inside solid fog.
    /// Returns -2 if in solid fog, 0 otherwise.
    /// </summary>
    public static int GetMeleeDamagePenaltyFor(CharacterController creature)
    {
        return IsCreatureInAnySolidFog(creature) ? MeleeDamagePenalty : 0;
    }

    /// <summary>
    /// Get the speed multiplier for a creature inside solid fog.
    /// Returns 0.5f if in solid fog, 1.0f otherwise.
    /// </summary>
    public static float GetSpeedMultiplierFor(CharacterController creature)
    {
        return IsCreatureInAnySolidFog(creature) ? SpeedMultiplier : 1.0f;
    }

    /// <summary>
    /// Check if a normal ranged weapon attack is blocked by solid fog.
    /// Solid fog blocks all normal ranged weapon attacks (not magic rays/spells).
    /// Returns true if either the attacker or target is inside solid fog.
    /// </summary>
    public static bool BlocksRangedAttack(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || !AreaEffectManager.HasInstance)
            return false;

        // If either combatant is in solid fog, normal ranged attacks are blocked
        return IsCreatureInAnySolidFog(attacker) || IsCreatureInAnySolidFog(target);
    }

    // ═══════════════════════════════════════════════════════════════
    // CONCEALMENT HELPER
    // ═══════════════════════════════════════════════════════════════

    private void ApplyConcealment(CharacterController character)
    {
        StatusEffectManager statusMgr = character.StatusEffectManager;
        if (statusMgr == null)
            statusMgr = character.gameObject.AddComponent<StatusEffectManager>();

        statusMgr.Init(character.Stats);

        for (int i = 0; i < statusMgr.ActiveEffects.Count; i++)
        {
            ActiveSpellEffect existing = statusMgr.ActiveEffects[i];
            if (existing != null && existing.Spell != null && existing.Spell.SpellId == ConcealmentSpellId)
            {
                existing.RemainingRounds = 1;
                existing.MissChance = 20;
                existing.IsTotalConcealment = false;
                existing.ConcealmentSource = "Solid Fog";
                existing.SourceAreaEffect = this;
                return;
            }
        }

        var effect = new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = ConcealmentSpellId, Name = "Solid Fog" },
            CasterName = Caster != null && Caster.Stats != null ? Caster.Stats.CharacterName : "Unknown",
            CasterLevel = Mathf.Max(1, CasterLevel),
            RemainingRounds = 1,
            DurationType = DurationType.Rounds,
            AffectedCharacterName = character.Stats.CharacterName,
            BonusTypeLegacy = "Concealment",
            BonusTypeEnum = BonusType.Concealment,
            IsApplied = true,
            MissChance = 20,
            IsTotalConcealment = false,
            ConcealmentSource = "Solid Fog",
            SourceAreaEffect = this
        };

        statusMgr.ActiveEffects.Add(effect);
        LogEffect($"{character.Stats.CharacterName} is shrouded by solid fog (20% at 5 ft, 50% beyond 5 ft).");
    }

    private void RemoveConcealment(CharacterController character)
    {
        if (character == null)
            return;

        // Do not remove concealment if the character still stands inside another active solid fog area.
        var fogAreas = AreaEffectManager.Instance.GetEffectsOfType<SolidFogAreaEffect>();
        for (int i = 0; i < fogAreas.Count; i++)
        {
            SolidFogAreaEffect other = fogAreas[i];
            if (other == null || other == this)
                continue;

            if (other.IsCharacterInArea(character))
                return;
        }

        // Also check if creature is inside a regular Fog Cloud (same concealment spell ID would differ,
        // so we only remove OUR concealment effect).
        StatusEffectManager statusMgr = character.StatusEffectManager;
        if (statusMgr == null || statusMgr.ActiveEffects == null || statusMgr.ActiveEffects.Count == 0)
            return;

        for (int i = statusMgr.ActiveEffects.Count - 1; i >= 0; i--)
        {
            ActiveSpellEffect effect = statusMgr.ActiveEffects[i];
            if (effect != null && effect.Spell != null && effect.Spell.SpellId == ConcealmentSpellId)
                statusMgr.RemoveEffect(effect);
        }
    }
}
