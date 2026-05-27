using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Daylight (PHB 3.5e p.216): 60-ft radius bright illumination.
/// - Object touched sheds bright light in a 60-ft radius.
/// - Counters and dispels any darkness spell of 3rd level or lower.
/// - If Daylight overlaps with a Darkness effect, both are temporarily negated
///   in the overlapping area.
/// - Duration 10 min/level (D).
/// - No save, no SR.
/// Visual uses warm golden semi-transparent grid-cell shading.
/// </summary>
public class DaylightAreaEffect : PersistentAreaEffect
{
    protected override Color GridHighlightColor => new Color(1f, 0.95f, 0.6f, 0.25f);
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Daylight";
        SpellId = SpellNames.DAYLIGHT;
        Shape = AreaShape.Circle;
        Radius = 12f; // 60-ft radius = 12 squares

        // Use per-cell golden overlay instead of a world mesh.
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        LogEffect("60-ft radius fills with bright daylight.");

        // Counter/dispel overlapping Darkness effects of 3rd level or lower
        DispelOverlappingDarkness();
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

        string timing = isInitial ? "is in" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} bright daylight.");
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        // Daylight has no per-round effect on creatures;
        // it just provides illumination.
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        LogEffect($"{character.Stats.CharacterName} leaves the area of daylight.");
    }

    protected override void OnAreaExpires()
    {
        RemoveGridHighlight();
        LogEffect("Daylight spell expires.");
        LogEffect("The bright illumination fades away.");
    }

    /// <summary>
    /// Counters and dispels any Darkness area effects of 3rd level or lower
    /// that overlap with this Daylight effect.
    /// Per PHB p.216: "Daylight counters or dispels any darkness spell of
    /// equal or lower level, such as darkness."
    /// </summary>
    private void DispelOverlappingDarkness()
    {
        if (!AreaEffectManager.HasInstance)
            return;

        List<DarknessAreaEffect> darknessAreas = AreaEffectManager.Instance.GetEffectsOfType<DarknessAreaEffect>();
        if (darknessAreas == null || darknessAreas.Count == 0)
            return;

        // Daylight is a 3rd-level spell, so it counters/dispels darkness spells of 3rd level or lower.
        // The Darkness spell is 2nd level, so it's always dispelled.
        for (int i = darknessAreas.Count - 1; i >= 0; i--)
        {
            DarknessAreaEffect darkness = darknessAreas[i];
            if (darkness == null)
                continue;

            // Check if the Darkness spell level is <= 3 (Daylight's level)
            SpellData darknessSpell = SpellDatabase.GetSpell(darkness.SpellId);
            int darknessSpellLevel = darknessSpell != null ? darknessSpell.SpellLevel : 2; // Darkness is 2nd level

            if (darknessSpellLevel > 3)
                continue; // Higher-level darkness spells are not dispelled

            // Check if areas overlap
            if (AffectedCells == null || darkness.AffectedCells == null)
                continue;

            bool overlaps = false;
            foreach (Vector2Int cell in AffectedCells)
            {
                if (darkness.AffectedCells.Contains(cell))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
            {
                string darknessName = darkness.EffectName ?? "Darkness";
                LogEffect($"☀ Daylight dispels {darknessName}!");
                darkness.ExpireEffect();
            }
        }
    }

    /// <summary>
    /// Static utility: check if a position is within any active Daylight area.
    /// </summary>
    public static bool IsPositionInDaylight(Vector2Int position)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<DaylightAreaEffect> daylightAreas = AreaEffectManager.Instance.GetEffectsOfType<DaylightAreaEffect>();
        if (daylightAreas == null)
            return false;

        for (int i = 0; i < daylightAreas.Count; i++)
        {
            DaylightAreaEffect area = daylightAreas[i];
            if (area == null || area.AffectedCells == null)
                continue;

            if (area.AffectedCells.Contains(position))
                return true;
        }

        return false;
    }
}
