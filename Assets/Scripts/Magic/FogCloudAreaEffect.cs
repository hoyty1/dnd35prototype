using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fog Cloud (PHB 3.5e): creatures inside gain concealment (20% miss chance).
/// Visual uses shaded grid squares instead of opaque 3D volume.
/// </summary>
public class FogCloudAreaEffect : PersistentAreaEffect
{
    private const string ConcealmentSpellId = "fog_cloud_concealment";

    private readonly List<SquareCell> _highlightedCells = new List<SquareCell>();
    private static readonly Color FogColor = new Color(0.60f, 0.60f, 0.60f, 0.40f);
    private bool _gridHighlightApplied;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Fog Cloud";
        SpellId = "fog_cloud";
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius

        // Replace opaque area mesh with per-cell shading.
        ShowVisual = false;

        DispersibleByWind = true;
        RequiredWindStrength = WindStrength.Moderate;
    }

    protected override void OnAreaCreated()
    {
        LogEffect("A dense fog forms in a 20-ft radius spread.");
        LogEffect("Creatures in the fog gain concealment (20% miss chance).");
        ApplyGridHighlight();
    }

    private void Update()
    {
        // Keep occupancy/concealment state synchronized while creatures move mid-round.
        UpdateCharacterTracking();

        // Defensive re-apply in case grid initializes slightly after the area object.
        if (!_gridHighlightApplied)
            ApplyGridHighlight();
    }

    public override void OnRoundStart()
    {
        base.OnRoundStart();
        if (!_gridHighlightApplied)
            ApplyGridHighlight();
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        string timing = isInitial ? "is within" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} the fog cloud.");
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
        LogEffect($"{character.Stats.CharacterName} leaves the fog cloud and loses concealment from it.");
    }

    protected override void OnAreaExpires()
    {
        foreach (CharacterController character in CharactersInArea)
        {
            if (character != null)
                RemoveConcealment(character);
        }

        RemoveGridHighlight();
        LogEffect("Fog Cloud dissipates.");
    }

    protected override void OnDestroy()
    {
        RemoveGridHighlight();
        base.OnDestroy();
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
                existing.ConcealmentSource = "Fog Cloud";
                return;
            }
        }

        var effect = new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = ConcealmentSpellId, Name = "Fog Cloud" },
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
            ConcealmentSource = "Fog Cloud"
        };

        statusMgr.ActiveEffects.Add(effect);
        LogEffect($"{character.Stats.CharacterName} gains concealment (20% miss chance).");
    }

    private void RemoveConcealment(CharacterController character)
    {
        if (character == null)
            return;

        // Do not remove concealment if the character still stands inside another active fog cloud area.
        var fogAreas = AreaEffectManager.Instance.GetEffectsOfType<FogCloudAreaEffect>();
        for (int i = 0; i < fogAreas.Count; i++)
        {
            FogCloudAreaEffect other = fogAreas[i];
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

    private void ApplyGridHighlight()
    {
        if (gameManager == null || gameManager.Grid == null)
        {
            _gridHighlightApplied = false;
            Debug.LogWarning("[FogCloudAreaEffect] Grid not found - cannot apply fog highlight.");
            return;
        }

        _highlightedCells.Clear();
        foreach (Vector2Int cellCoord in AffectedCells)
        {
            SquareCell cell = gameManager.Grid.GetCell(cellCoord);
            if (cell == null)
                continue;

            cell.SetHighlight(FogColor);
            _highlightedCells.Add(cell);
        }

        _gridHighlightApplied = _highlightedCells.Count > 0;
    }

    private void RemoveGridHighlight()
    {
        for (int i = 0; i < _highlightedCells.Count; i++)
        {
            SquareCell cell = _highlightedCells[i];
            if (cell != null)
                cell.ClearHighlight();
        }

        _highlightedCells.Clear();
        _gridHighlightApplied = false;
    }
}
