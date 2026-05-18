using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Evard's Black Tentacles (PHB p.228): Conjuration (Creation).
/// 20-ft radius spread, fixed location on ground, 1 round/level.
///
/// Effects:
///   - Tentacles make grapple checks against all creatures in the area each round
///   - Treat tentacles as Large creature: BAB = caster level, Str 19
///   - Grapple modifier = CL + 4 (Str mod) + 4 (Large size) = CL + 8
///   - Successful grapple deals 1d6+4 bludgeoning damage
///   - Creatures entering the area are immediately attacked
///   - Already-grappled creatures take damage automatically each round
///   - Creatures can break free with opposed grapple or Escape Artist check
///   - Tentacles are immune to all damage
/// </summary>
public class BlackTentaclesAreaEffect : PersistentAreaEffect
{
    /// <summary>Tentacle STR modifier (+4 from Str 19).</summary>
    private const int TentacleStrMod = 4;

    /// <summary>Tentacle size bonus to grapple (Large = +4).</summary>
    private const int TentacleSizeGrappleMod = 4;

    /// <summary>Damage die for tentacle constriction: 1d6.</summary>
    private const int DamageDie = 6;

    /// <summary>Damage bonus (Str mod of 19 = +4).</summary>
    private const int DamageBonus = 4;

    /// <summary>Tracks creatures currently grappled by tentacles.</summary>
    private readonly HashSet<CharacterController> _grappledCreatures = new HashSet<CharacterController>();

    protected override Color GridHighlightColor => AreaEffectColors.BlackTentacles;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Black Tentacles";
        SpellId = SpellNames.EVARDS_BLACK_TENTACLES;
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius = 4 squares

        ShowVisual = false;

        // Not dispersible by wind (solid tentacles)
        DispersibleByWind = false;
    }

    /// <summary>
    /// The tentacles' grapple check modifier: CL + Str mod (+4) + Large size (+4) = CL + 8.
    /// </summary>
    public int TentacleGrappleModifier => CasterLevel + TentacleStrMod + TentacleSizeGrappleMod;

    protected override void OnAreaCreated()
    {
        LogEffect($"🦑 A writhing mass of black tentacles erupts from the ground!");
        LogEffect($"  • 20-ft radius spread ({AffectedCells.Count} squares)");
        LogEffect($"  • Tentacle grapple modifier: +{TentacleGrappleModifier} (CL {CasterLevel} + 8)");
        LogEffect($"  • Grappled creatures take 1d{DamageDie}+{DamageBonus} bludgeoning damage/round");
        LogEffect($"  • Creatures can break free with opposed grapple or Escape Artist check");
        LogEffect($"  • Duration: {RoundsRemaining} round(s) (Dismissible)");
    }

    private void Update()
    {
        UpdateCharacterTracking();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    public override void OnRoundStart()
    {
        base.OnRoundStart();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();

        // Already-grappled creatures automatically take damage
        ProcessGrappledCreatures();
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        string timing = isInitial ? "is within" : "enters";
        LogEffect($"🦑 {character.Stats.CharacterName} {timing} the black tentacles!");

        // Immediately try to grapple the creature
        if (!_grappledCreatures.Contains(character))
        {
            PerformTentacleGrappleCheck(character);
        }
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // If already grappled, damage is handled in ProcessGrappledCreatures
        if (_grappledCreatures.Contains(character))
            return;

        // Not yet grappled — tentacles try to grapple again
        PerformTentacleGrappleCheck(character);
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        if (_grappledCreatures.Contains(character))
        {
            _grappledCreatures.Remove(character);
            RemoveTentacleGrappleCondition(character);
            LogEffect($"  {character.Stats.CharacterName} escapes the tentacle area and is freed!");
        }
        else
        {
            LogEffect($"  {character.Stats.CharacterName} leaves the black tentacles area.");
        }
    }

    protected override void OnAreaExpires()
    {
        // Free all grappled creatures
        foreach (CharacterController creature in _grappledCreatures)
        {
            if (creature != null && creature.Stats != null)
            {
                RemoveTentacleGrappleCondition(creature);
                LogEffect($"  {creature.Stats.CharacterName} is freed as the tentacles dissolve.");
            }
        }

        _grappledCreatures.Clear();
        RemoveGridHighlight();
        LogEffect("The black tentacles dissolve into nothingness.");
    }

    // ═══════════════════════════════════════════════════════════════
    // GRAPPLE CHECKS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tentacles attempt to grapple a creature.
    /// Tentacle grapple modifier = CL + 8 (Str 19 + Large size).
    /// Opposed by creature's grapple check.
    /// </summary>
    private void PerformTentacleGrappleCheck(CharacterController creature)
    {
        if (creature == null || creature.Stats == null || creature.Stats.IsDead)
            return;

        // Tentacle grapple roll
        int tentacleRoll = Random.Range(1, 21);
        int tentacleTotal = tentacleRoll + TentacleGrappleModifier;

        // Creature's opposed grapple check
        int creatureGrappleMod = creature.GetGrappleModifier();
        int creatureRoll = Random.Range(1, 21);
        int creatureTotal = creatureRoll + creatureGrappleMod;

        if (tentacleTotal >= creatureTotal)
        {
            LogEffect($"  🦑 Tentacle grapple vs {creature.Stats.CharacterName}: " +
                $"Tentacle d20({tentacleRoll}) + {TentacleGrappleModifier} = {tentacleTotal} vs " +
                $"d20({creatureRoll}) + {creatureGrappleMod} = {creatureTotal} → GRAPPLED!");

            _grappledCreatures.Add(creature);
            ApplyTentacleGrappleCondition(creature);

            // Deal initial constriction damage
            DealTentacleDamage(creature);
        }
        else
        {
            LogEffect($"  🦑 Tentacle grapple vs {creature.Stats.CharacterName}: " +
                $"Tentacle d20({tentacleRoll}) + {TentacleGrappleModifier} = {tentacleTotal} vs " +
                $"d20({creatureRoll}) + {creatureGrappleMod} = {creatureTotal} → creature resists!");
        }
    }

    /// <summary>
    /// Process all currently grappled creatures at the start of each round.
    /// Grappled creatures take automatic constriction damage.
    /// They also get a chance to break free.
    /// </summary>
    private void ProcessGrappledCreatures()
    {
        if (_grappledCreatures.Count == 0)
            return;

        var toRelease = new List<CharacterController>();

        foreach (CharacterController creature in _grappledCreatures)
        {
            if (creature == null || creature.Stats == null || creature.Stats.IsDead)
            {
                toRelease.Add(creature);
                continue;
            }

            // Creature can attempt to break free: opposed grapple check or Escape Artist
            if (TryBreakFree(creature))
            {
                toRelease.Add(creature);
                continue;
            }

            // Still grappled — take automatic constriction damage
            DealTentacleDamage(creature);
        }

        foreach (CharacterController released in toRelease)
        {
            _grappledCreatures.Remove(released);
            if (released != null && released.Stats != null && !released.Stats.IsDead)
            {
                RemoveTentacleGrappleCondition(released);
            }
        }
    }

    /// <summary>
    /// Creature attempts to break free from tentacle grapple.
    /// Options: opposed grapple check or Escape Artist check vs tentacle grapple total.
    /// We use the better option for the creature (grapple or Escape Artist).
    /// </summary>
    private bool TryBreakFree(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return true;

        // Grapple check attempt
        int grappleRoll = Random.Range(1, 21);
        int creatureGrappleMod = creature.GetGrappleModifier();
        int grappleTotal = grappleRoll + creatureGrappleMod;

        // Tentacle opposing roll
        int tentacleRoll = Random.Range(1, 21);
        int tentacleTotal = tentacleRoll + TentacleGrappleModifier;

        // Also check Escape Artist if creature has it
        // Escape Artist DC = tentacle's grapple check result
        int escapeArtistBonus = creature.Stats.GetSkillBonus("Escape Artist");
        int escapeRoll = Random.Range(1, 21);
        int escapeTotal = escapeRoll + escapeArtistBonus;

        // Use better result
        bool grappleSuccess = grappleTotal >= tentacleTotal;
        bool escapeSuccess = escapeTotal >= tentacleTotal;

        if (grappleSuccess || escapeSuccess)
        {
            if (grappleSuccess && (!escapeSuccess || grappleTotal >= escapeTotal))
            {
                LogEffect($"  💪 {creature.Stats.CharacterName} breaks free from tentacles! " +
                    $"Grapple d20({grappleRoll}) + {creatureGrappleMod} = {grappleTotal} vs " +
                    $"Tentacle d20({tentacleRoll}) + {TentacleGrappleModifier} = {tentacleTotal}");
            }
            else
            {
                LogEffect($"  🏃 {creature.Stats.CharacterName} wriggles free from tentacles! " +
                    $"Escape Artist d20({escapeRoll}) + {escapeArtistBonus} = {escapeTotal} vs " +
                    $"Tentacle d20({tentacleRoll}) + {TentacleGrappleModifier} = {tentacleTotal}");
            }
            return true;
        }

        LogEffect($"  🦑 {creature.Stats.CharacterName} fails to escape the tentacles! " +
            $"Grapple: d20({grappleRoll})+{creatureGrappleMod}={grappleTotal}, " +
            $"Escape Artist: d20({escapeRoll})+{escapeArtistBonus}={escapeTotal} " +
            $"vs Tentacle d20({tentacleRoll})+{TentacleGrappleModifier}={tentacleTotal}");

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // DAMAGE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Deal 1d6+4 bludgeoning damage to a grappled creature.
    /// </summary>
    private void DealTentacleDamage(CharacterController creature)
    {
        if (creature == null || creature.Stats == null || creature.Stats.IsDead)
            return;

        int damage = Random.Range(1, DamageDie + 1) + DamageBonus;
        int previousHp = creature.Stats.CurrentHP;
        creature.Stats.CurrentHP -= damage;

        LogEffect($"  🦑 Tentacles crush {creature.Stats.CharacterName} for {damage} bludgeoning damage! " +
            $"(HP: {previousHp} → {creature.Stats.CurrentHP})");

        // Check for death
        if (creature.Stats.CurrentHP <= 0 && !creature.Stats.IsDead)
        {
            LogEffect($"  💀 {creature.Stats.CharacterName} is crushed to death by the tentacles!");
            creature.OnDeath();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CONDITION MANAGEMENT
    // ═══════════════════════════════════════════════════════════════

    private void ApplyTentacleGrappleCondition(CharacterController creature)
    {
        if (creature == null)
            return;

        // Apply entangled condition (grappled by tentacles restricts movement)
        creature.ApplyCondition(CombatConditionType.Entangled, -1, "Black Tentacles");
    }

    private void RemoveTentacleGrappleCondition(CharacterController creature)
    {
        if (creature == null)
            return;

        // Only remove if not grappled by another tentacle field
        var otherTentacles = AreaEffectManager.Instance.GetEffectsOfType<BlackTentaclesAreaEffect>();
        for (int i = 0; i < otherTentacles.Count; i++)
        {
            BlackTentaclesAreaEffect other = otherTentacles[i];
            if (other != null && other != this && other.IsCreatureGrappled(creature))
                return; // Still grappled by another set of tentacles
        }

        creature.RemoveCondition(CombatConditionType.Entangled);
    }

    /// <summary>
    /// Returns true if the given creature is currently grappled by these tentacles.
    /// </summary>
    public bool IsCreatureGrappled(CharacterController creature)
    {
        return creature != null && _grappledCreatures.Contains(creature);
    }

    // ═══════════════════════════════════════════════════════════════
    // STATIC LOOKUP METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the BlackTentaclesAreaEffect that contains the given character, or null.
    /// </summary>
    public static BlackTentaclesAreaEffect GetTentaclesContainingCharacter(CharacterController character)
    {
        if (character == null || !AreaEffectManager.HasInstance)
            return null;

        List<BlackTentaclesAreaEffect> effects =
            AreaEffectManager.Instance.GetEffectsOfType<BlackTentaclesAreaEffect>();

        if (effects == null)
            return null;

        for (int i = 0; i < effects.Count; i++)
        {
            BlackTentaclesAreaEffect effect = effects[i];
            if (effect != null && effect.IsCharacterInArea(character))
                return effect;
        }

        return null;
    }

    /// <summary>
    /// Returns true if the given character is inside any active Black Tentacles area.
    /// </summary>
    public static bool IsCharacterInAnyTentacles(CharacterController character)
    {
        return GetTentaclesContainingCharacter(character) != null;
    }

    /// <summary>
    /// Returns true if the given character is grappled by any active Black Tentacles.
    /// </summary>
    public static bool IsCharacterGrappledByAnyTentacles(CharacterController character)
    {
        if (character == null || !AreaEffectManager.HasInstance)
            return false;

        List<BlackTentaclesAreaEffect> effects =
            AreaEffectManager.Instance.GetEffectsOfType<BlackTentaclesAreaEffect>();

        if (effects == null)
            return false;

        for (int i = 0; i < effects.Count; i++)
        {
            BlackTentaclesAreaEffect effect = effects[i];
            if (effect != null && effect.IsCreatureGrappled(character))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets info string for UI display.
    /// </summary>
    public string GetTentaclesInfoString()
    {
        string casterName = Caster != null && Caster.Stats != null ? Caster.Stats.CharacterName : "Unknown";
        return $"Black Tentacles — grapple +{TentacleGrappleModifier}, {_grappledCreatures.Count} grappled, " +
               $"{RoundsRemaining} round(s) remaining, cast by {casterName}";
    }
}
