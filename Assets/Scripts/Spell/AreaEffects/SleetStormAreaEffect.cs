using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Sleet Storm (PHB p.281): Conjuration (Creation) [Cold].
/// 40-ft radius cylinder, 1 round/level.
///
/// Effects:
///   - Blocks ALL sight including darkvision (total concealment beyond 5 ft)
///   - Icy ground: DC 10 Balance check to move (half speed); fail by 5+ = prone
///   - Ranged attacks suffer concealment miss chance
///   - Concentration DC 5 + spell level to cast inside
///   - Extinguishes small fires
///   - No save, no SR
/// </summary>
public class SleetStormAreaEffect : PersistentAreaEffect
{
    /// <summary>Balance check DC to move through sleet storm area.</summary>
    public const int BalanceCheckDC = 10;

    /// <summary>Concentration check DC modifier: DC = 5 + spell level being cast.</summary>
    public const int ConcentrationDCBase = 5;

    private const string ConcealmentSpellId = "sleet_storm_concealment";

    protected override Color GridHighlightColor => AreaEffectColors.SleetStorm;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Sleet Storm";
        SpellId = SpellNames.SLEET_STORM;
        Shape = AreaShape.Circle;
        Radius = 8f; // 40-ft radius (8 squares × 5 ft)

        ShowVisual = false;

        // Sleet Storm creates its own weather — not easily dispersed by wind
        DispersibleByWind = false;
    }

    protected override void OnAreaCreated()
    {
        LogEffect("Driving sleet fills a 40-ft radius cylinder!");
        LogEffect("  • All sight blocked (including darkvision) — total concealment beyond 5 ft.");
        LogEffect("  • Icy ground: DC 10 Balance to move at half speed; fail by 5+ = fall prone.");
        LogEffect("  • Concentration DC 5 + spell level to cast spells inside.");
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
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        string timing = isInitial ? "is within" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} the sleet storm.");
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
        LogEffect($"{character.Stats.CharacterName} leaves the sleet storm.");
    }

    protected override void OnAreaExpires()
    {
        foreach (CharacterController character in CharactersInArea)
        {
            if (character != null)
                RemoveConcealment(character);
        }

        RemoveGridHighlight();
        LogEffect("Sleet Storm dissipates.");
    }

    // ═══════════════════════════════════════════════════════════════
    // BALANCE CHECK — called by GameManager when creature tries to move
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Perform a Balance check for a creature trying to move through the sleet storm.
    /// Returns the result of the check.
    /// </summary>
    /// <param name="creature">The creature attempting to move.</param>
    /// <param name="fellProne">True if the creature fell prone (failed by 5+).</param>
    /// <param name="canMove">True if the creature can move (passed the check).</param>
    /// <returns>Log string describing the result.</returns>
    public string PerformBalanceCheck(CharacterController creature, out bool fellProne, out bool canMove)
    {
        fellProne = false;
        canMove = false;

        if (creature == null || creature.Stats == null)
            return string.Empty;

        int roll = DiceRoller.D20();
        int dexMod = creature.Stats.DEXMod;
        int balanceRanks = creature.Stats.GetSkillBonus("Balance");
        int total = roll + balanceRanks;

        int margin = total - BalanceCheckDC;

        if (margin >= 0)
        {
            // Success: move at half speed
            canMove = true;
            string result = $"⛸ {creature.Stats.CharacterName} Balance check: d20({roll}) + {balanceRanks} = {total} vs DC {BalanceCheckDC} — SUCCESS (half speed)";
            LogEffect(result);
            return result;
        }
        else if (margin <= -5)
        {
            // Failed by 5+: fall prone
            fellProne = true;
            creature.SetProne(true);
            string result = $"⛸ {creature.Stats.CharacterName} Balance check: d20({roll}) + {balanceRanks} = {total} vs DC {BalanceCheckDC} — FAILED by {-margin} → FALLS PRONE!";
            LogEffect(result);
            return result;
        }
        else
        {
            // Failed by less than 5: can't move this round
            string result = $"⛸ {creature.Stats.CharacterName} Balance check: d20({roll}) + {balanceRanks} = {total} vs DC {BalanceCheckDC} — FAILED (cannot move this round)";
            LogEffect(result);
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CONCEALMENT — uses Fog Cloud rules
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Get concealment miss chance for attacks through the sleet storm.
    /// Within 5 ft (1 square): 20%. Beyond 5 ft: 50% (total concealment).
    /// </summary>
    public int GetConcealmentMissChance(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
            return 0;

        if (!IsCharacterInArea(target))
            return 0;

        int distanceSquares = attacker.GetMinimumDistanceToTarget(target, chebyshev: true);
        return distanceSquares <= 1 ? 20 : 50;
    }

    public bool GrantsTotalConcealmentAgainst(CharacterController attacker, CharacterController target)
    {
        return GetConcealmentMissChance(attacker, target) >= 50;
    }

    /// <summary>
    /// Check if a creature is in any active sleet storm (for movement/attack modifiers).
    /// </summary>
    public static bool IsCreatureInAnySleetStorm(CharacterController creature)
    {
        if (creature == null || !AreaEffectManager.HasInstance)
            return false;

        var storms = AreaEffectManager.Instance.GetEffectsOfType<SleetStormAreaEffect>();
        for (int i = 0; i < storms.Count; i++)
        {
            if (storms[i] != null && storms[i].IsCharacterInArea(creature))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get the Concentration DC modifier for casting inside sleet storm.
    /// DC = 5 + level of spell being cast (weather distraction table).
    /// </summary>
    public static int GetConcentrationDCModifier(int spellLevelBeingCast)
    {
        return ConcentrationDCBase + spellLevelBeingCast;
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
                existing.ConcealmentSource = "Sleet Storm";
                existing.SourceAreaEffect = this;
                return;
            }
        }

        var effect = new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = ConcealmentSpellId, Name = "Sleet Storm" },
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
            ConcealmentSource = "Sleet Storm",
            SourceAreaEffect = this
        };

        statusMgr.ActiveEffects.Add(effect);
    }

    private void RemoveConcealment(CharacterController character)
    {
        if (character == null)
            return;

        // Don't remove if character is still in another sleet storm
        var storms = AreaEffectManager.Instance.GetEffectsOfType<SleetStormAreaEffect>();
        for (int i = 0; i < storms.Count; i++)
        {
            SleetStormAreaEffect other = storms[i];
            if (other == null || other == this)
                continue;
            if (other.IsCharacterInArea(character))
                return;
        }

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
