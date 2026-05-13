using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Stinking Cloud (PHB p.284): Conjuration (Creation).
/// 20-ft radius spread, 1 round/level.
///
/// Effects:
///   - Fort save each round or become nauseated
///   - Nauseated: can only take a single move action (no attacks, spells, concentration)
///   - Nausea persists 1d4+1 rounds after leaving cloud
///   - Vision blocked (like Fog Cloud)
///   - Immune: undead, constructs, creatures that don't breathe, poison immunity
///   - Dispersible by wind (moderate: 4 rounds, strong: 1 round)
/// </summary>
public class StinkingCloudAreaEffect : PersistentAreaEffect
{
    private const string ConcealmentSpellId = "stinking_cloud_concealment";

    /// <summary>Tracks creatures that are currently nauseated by this cloud.</summary>
    private readonly HashSet<CharacterController> _nauseatedCreatures = new HashSet<CharacterController>();

    /// <summary>
    /// Tracks creatures that left the cloud while nauseated and their remaining nausea rounds.
    /// Key: creature, Value: remaining nausea rounds after leaving.
    /// </summary>
    private readonly Dictionary<CharacterController, int> _lingeringNausea = new Dictionary<CharacterController, int>();

    protected override Color GridHighlightColor => AreaEffectColors.StinkingCloud;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Stinking Cloud";
        SpellId = SpellNames.STINKING_CLOUD;
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius (4 squares × 5 ft)

        ShowVisual = false;

        DispersibleByWind = true;
        RequiredWindStrength = WindStrength.Moderate;
    }

    protected override void OnAreaCreated()
    {
        LogEffect("A nauseating bank of fog fills a 20-ft radius spread!");
        LogEffect("  • Fort save each round or become nauseated.");
        LogEffect("  • Nauseated creatures can only take a single move action.");
        LogEffect("  • Nausea persists 1d4+1 rounds after leaving the cloud.");
        LogEffect("  • Vision blocked beyond 5 ft (like Fog Cloud).");
    }

    private void Update()
    {
        UpdateCharacterTracking();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();

        // Tick lingering nausea for creatures that left the cloud
        TickLingeringNausea();
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
        LogEffect($"{character.Stats.CharacterName} {timing} the stinking cloud.");
        ApplyConcealment(character);

        // If creature had lingering nausea from this cloud and re-entered, remove from lingering
        _lingeringNausea.Remove(character);

        // Force save on entry
        if (!IsImmuneToNausea(character))
        {
            PerformFortSave(character);
        }
        else
        {
            LogEffect($"  {character.Stats.CharacterName} is immune to the nauseating vapors.");
        }
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        ApplyConcealment(character);

        if (IsImmuneToNausea(character))
            return;

        // If already nauseated by this cloud, remains nauseated (no new save)
        if (_nauseatedCreatures.Contains(character))
        {
            LogEffect($"  {character.Stats.CharacterName} remains nauseated in the stinking cloud.");
            return;
        }

        // Otherwise, must save each round
        PerformFortSave(character);
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        RemoveConcealment(character);
        LogEffect($"{character.Stats.CharacterName} leaves the stinking cloud.");

        // If creature was nauseated, start lingering nausea timer
        if (_nauseatedCreatures.Contains(character))
        {
            int lingeringRounds = Random.Range(1, 5) + 1; // 1d4+1
            _lingeringNausea[character] = lingeringRounds;
            _nauseatedCreatures.Remove(character);
            LogEffect($"  {character.Stats.CharacterName} is still nauseated for {lingeringRounds} rounds after leaving.");
        }
    }

    protected override void OnAreaExpires()
    {
        foreach (CharacterController character in CharactersInArea)
        {
            if (character != null)
            {
                RemoveConcealment(character);

                // Creatures still nauseated when cloud expires get lingering nausea
                if (_nauseatedCreatures.Contains(character))
                {
                    int lingeringRounds = Random.Range(1, 5) + 1; // 1d4+1
                    _lingeringNausea[character] = lingeringRounds;
                    LogEffect($"  {character.Stats.CharacterName} remains nauseated for {lingeringRounds} rounds after cloud expires.");
                }
            }
        }

        _nauseatedCreatures.Clear();
        RemoveGridHighlight();
        LogEffect("Stinking Cloud dissipates.");
    }

    // ═══════════════════════════════════════════════════════════════
    // FORTITUDE SAVE
    // ═══════════════════════════════════════════════════════════════

    private void PerformFortSave(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return;

        int roll = Random.Range(1, 21);
        int fortBonus = creature.Stats.FortitudeSave;
        int total = roll + fortBonus;

        if (total >= SaveDC)
        {
            LogEffect($"  💨 {creature.Stats.CharacterName} Fort save: d20({roll}) + {fortBonus} = {total} vs DC {SaveDC} — SUCCESS (not nauseated this round).");
        }
        else
        {
            LogEffect($"  💨 {creature.Stats.CharacterName} Fort save: d20({roll}) + {fortBonus} = {total} vs DC {SaveDC} — FAILED → NAUSEATED!");
            ApplyNauseated(creature);
        }
    }

    private void ApplyNauseated(CharacterController creature)
    {
        if (creature == null)
            return;

        _nauseatedCreatures.Add(creature);
        creature.ApplyCondition(CombatConditionType.Nauseated, -1, "Stinking Cloud");
    }

    // ═══════════════════════════════════════════════════════════════
    // IMMUNITY CHECKS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a creature is immune to Stinking Cloud's nauseating effect.
    /// Immune: undead, constructs, creatures that don't breathe, poison immunity.
    /// </summary>
    public static bool IsImmuneToNausea(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return true;

        string creatureType = string.IsNullOrWhiteSpace(creature.Stats.CreatureType)
            ? string.Empty
            : creature.Stats.CreatureType.Trim().ToLowerInvariant();

        // Undead are immune (immune to Fort-save effects)
        if (creatureType.Contains("undead"))
            return true;

        // Constructs are immune (immune to Fort-save effects)
        if (creatureType.Contains("construct"))
            return true;

        // Elementals don't need to breathe
        if (creatureType.Contains("elemental"))
            return true;

        // Check tags for special immunities (Tags is on CharacterController)
        CharacterTags tags = creature.Tags;
        if (tags != null)
        {
            if (tags.HasTag("no_breathe") ||
                tags.HasTag("does_not_breathe") ||
                tags.HasTag("poison_immunity") ||
                tags.HasTag("immune_poison"))
                return true;
        }

        // Also check CreatureTags list on CharacterStats
        if (creature.Stats.CreatureTags != null)
        {
            for (int i = 0; i < creature.Stats.CreatureTags.Count; i++)
            {
                string tag = creature.Stats.CreatureTags[i];
                if (tag == "no_breathe" || tag == "does_not_breathe" ||
                    tag == "poison_immunity" || tag == "immune_poison")
                    return true;
            }
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // LINGERING NAUSEA
    // ═══════════════════════════════════════════════════════════════

    private void TickLingeringNausea()
    {
        // This is handled per-round; we use a simple countdown
        // Called from Update but actual tick happens in OnRoundStart context
    }

    /// <summary>
    /// Call once per round to tick down lingering nausea for creatures that left the cloud.
    /// Should be called by GameManager during round processing.
    /// </summary>
    public void TickLingeringNauseaRound()
    {
        if (_lingeringNausea.Count == 0)
            return;

        var toRemove = new List<CharacterController>();

        foreach (var kvp in _lingeringNausea)
        {
            CharacterController creature = kvp.Key;
            if (creature == null || creature.Stats == null || creature.Stats.IsDead)
            {
                toRemove.Add(creature);
                continue;
            }

            int remaining = kvp.Value - 1;
            if (remaining <= 0)
            {
                toRemove.Add(creature);
                creature.RemoveCondition(CombatConditionType.Nauseated);
                LogEffect($"  {creature.Stats.CharacterName}'s nausea from Stinking Cloud wears off.");
            }
            else
            {
                _lingeringNausea[creature] = remaining;
                LogEffect($"  {creature.Stats.CharacterName} still nauseated ({remaining} round(s) remaining).");
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
            _lingeringNausea.Remove(toRemove[i]);
    }

    /// <summary>
    /// Check if a creature has lingering nausea from this cloud.
    /// </summary>
    public bool HasLingeringNausea(CharacterController creature)
    {
        return creature != null && _lingeringNausea.ContainsKey(creature);
    }

    /// <summary>
    /// Get remaining lingering nausea rounds for a creature.
    /// </summary>
    public int GetLingeringNauseaRounds(CharacterController creature)
    {
        if (creature != null && _lingeringNausea.TryGetValue(creature, out int rounds))
            return rounds;
        return 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // CONCEALMENT — uses Fog Cloud rules
    // ═══════════════════════════════════════════════════════════════

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

    private void ApplyConcealment(CharacterController character)
    {
        StatusEffectManager statusMgr = character.GetComponent<StatusEffectManager>();
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
                existing.ConcealmentSource = "Stinking Cloud";
                existing.SourceAreaEffect = this;
                return;
            }
        }

        var effect = new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = ConcealmentSpellId, Name = "Stinking Cloud" },
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
            ConcealmentSource = "Stinking Cloud",
            SourceAreaEffect = this
        };

        statusMgr.ActiveEffects.Add(effect);
    }

    private void RemoveConcealment(CharacterController character)
    {
        if (character == null)
            return;

        // Don't remove if character is still in another stinking cloud
        var clouds = AreaEffectManager.Instance.GetEffectsOfType<StinkingCloudAreaEffect>();
        for (int i = 0; i < clouds.Count; i++)
        {
            StinkingCloudAreaEffect other = clouds[i];
            if (other == null || other == this)
                continue;
            if (other.IsCharacterInArea(character))
                return;
        }

        StatusEffectManager statusMgr = character.GetComponent<StatusEffectManager>();
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
