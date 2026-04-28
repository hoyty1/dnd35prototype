using UnityEngine;

/// <summary>
/// Obscuring Mist (PHB 3.5e): creatures inside gain concealment (20% miss chance).
/// Visual uses shaded grid squares instead of opaque 3D volume.
/// </summary>
public class ObscuringMistAreaEffect : PersistentAreaEffect
{
    private const string ConcealmentSpellId = "obscuring_mist_concealment";

    protected override Color GridHighlightColor => AreaEffectColors.ObscuringMist;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Obscuring Mist";
        SpellId = "obscuring_mist";
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius

        // Replace opaque area mesh with per-cell shading.
        ShowVisual = false;

        DispersibleByWind = true;
        RequiredWindStrength = WindStrength.Moderate;
    }

    protected override void OnAreaCreated()
    {
        LogEffect("Mist billows across a 20-ft radius spread.");
        LogEffect("Within the mist: adjacent attackers suffer 20% miss chance; attackers farther than 5 ft suffer 50% (total concealment).");
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

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        string timing = isInitial ? "is within" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} the obscuring mist.");
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
        LogEffect($"{character.Stats.CharacterName} leaves the obscuring mist and loses concealment from it.");
    }

    protected override void OnAreaExpires()
    {
        foreach (CharacterController character in CharactersInArea)
        {
            if (character != null)
                RemoveConcealment(character);
        }

        RemoveGridHighlight();
        LogEffect("Obscuring Mist dissipates.");
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
                existing.ConcealmentSource = "Obscuring Mist";
                existing.SourceAreaEffect = this;
                return;
            }
        }

        var effect = new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = ConcealmentSpellId, Name = "Obscuring Mist" },
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
            ConcealmentSource = "Obscuring Mist",
            SourceAreaEffect = this
        };

        statusMgr.ActiveEffects.Add(effect);
        LogEffect($"{character.Stats.CharacterName} is shrouded by mist (20% at 5 ft, 50% beyond 5 ft).");
    }

    public int GetConcealmentMissChance(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
            return 0;

        if (!IsCharacterInArea(attacker) || !IsCharacterInArea(target))
            return 0;

        int distanceSquares = attacker.GetMinimumDistanceToTarget(target, chebyshev: true);
        return distanceSquares <= 1 ? 20 : 50;
    }

    public bool GrantsTotalConcealmentAgainst(CharacterController attacker, CharacterController target)
    {
        return GetConcealmentMissChance(attacker, target) >= 50;
    }

    private void RemoveConcealment(CharacterController character)
    {
        if (character == null)
            return;

        // Do not remove concealment if the character still stands inside another active obscuring mist area.
        var mistAreas = AreaEffectManager.Instance.GetEffectsOfType<ObscuringMistAreaEffect>();
        for (int i = 0; i < mistAreas.Count; i++)
        {
            ObscuringMistAreaEffect other = mistAreas[i];
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
