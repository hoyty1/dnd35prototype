// ============================================================================
// GameManager_DomainAreaSpells.cs — Domain AoE spell resolution & area creation.
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
//
// AoE spells implemented here:
//   • Entangle (Plant 1)          — 40-ft radius entangling area
//   • Soften Earth and Stone (Earth 2) — Difficult terrain creation
//   • Spike Stones (Earth 4)      — Movement-damage area
//   • Plant Growth (Plant 3)      — Movement quartered area
// ============================================================================
using DND35e.Identifiers;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  ENTANGLE AoE — PHB p.227
    //  Creates a persistent EntangleAreaEffect at the target location.
    // ================================================================

    /// <summary>
    /// Resolves Entangle as an AoE spell: creates an EntangleAreaEffect
    /// at the targeted area.
    /// </summary>
    private bool TryResolveEntangleSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.DOMAIN_ENTANGLE, StringComparison.Ordinal))
            return false;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int saveDc = GetSpellSaveDC(caster, spell);
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Calculate center position from AoE cells
        Vector3 centerPos = CalculateAreaCenter(aoeCells);

        // Create area effect
        CreateEntangleArea(centerPos, durationRounds, saveDc, caster);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🌿 {caster?.Stats?.CharacterName ?? "Caster"} casts Entangle!");
        sb.AppendLine($"  Area: 40-ft radius spread | Reflex DC {saveDc} | Duration: {durationRounds} rounds");
        sb.AppendLine($"  Entangled: -2 attack, -4 Dex, can't move. Break free: DC 20 Str/Escape Artist.");
        sb.AppendLine($"  Difficult terrain throughout area.");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    /// <summary>Creates an EntangleAreaEffect at the specified position.</summary>
    public void CreateEntangleArea(Vector3 centerPosition, int duration, int saveDC, CharacterController caster)
    {
        var go = new GameObject("EntangleArea");
        go.transform.position = centerPosition;
        var effect = go.AddComponent<EntangleAreaEffect>();
        effect.CenterPosition = centerPosition;
        effect.RoundsRemaining = duration;
        effect.SaveDC = saveDC;
        effect.CasterLevel = caster != null && caster.Stats != null ? caster.Stats.GetDomainBoostedCasterLevel(spell) : 1;

        Debug.Log($"[GameManager] Entangle area created at {centerPosition}, duration={duration}, DC={saveDC}");
    }

    // ================================================================
    //  SOFTEN EARTH AND STONE AoE — PHB p.280
    //  Instantaneous: creates permanent difficult terrain.
    // ================================================================

    /// <summary>
    /// Resolves Soften Earth and Stone: creates a SoftenEarthAreaEffect.
    /// </summary>
    private bool TryResolveSoftenEarthSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.DOMAIN_SOFTEN_EARTH, StringComparison.Ordinal))
            return false;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;

        // Instantaneous but lasts rest of combat (999 rounds)
        Vector3 centerPos = CalculateAreaCenter(aoeCells);
        CreateSoftenEarthArea(centerPos, 999, caster, casterLevel);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🪨 {caster?.Stats?.CharacterName ?? "Caster"} casts Soften Earth and Stone!");
        sb.AppendLine($"  Area: {casterLevel * 2} squares of earth/stone softened into mud/sand.");
        sb.AppendLine($"  Difficult terrain (double movement cost). No save, no SR.");
        sb.AppendLine($"  Effect is instantaneous (permanent change).");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    /// <summary>Creates a SoftenEarthAreaEffect at the specified position.</summary>
    public void CreateSoftenEarthArea(Vector3 centerPosition, int duration, CharacterController caster, int casterLevel)
    {
        var go = new GameObject("SoftenEarthArea");
        go.transform.position = centerPosition;
        var effect = go.AddComponent<SoftenEarthAreaEffect>();
        effect.CenterPosition = centerPosition;
        effect.RoundsRemaining = duration;
        effect.CasterLevel = casterLevel;

        Debug.Log($"[GameManager] Soften Earth area created at {centerPosition}, CL={casterLevel}");
    }

    // ================================================================
    //  SPIKE STONES AoE — PHB p.283
    //  Movement-damage area, 1d8 per 5 ft, Reflex DC 15 half.
    // ================================================================

    /// <summary>
    /// Resolves Spike Stones: creates a SpikeStoneAreaEffect.
    /// </summary>
    private bool TryResolveSpikeStoneSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.SPIKE_STONES, StringComparison.Ordinal))
            return false;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        Vector3 centerPos = CalculateAreaCenter(aoeCells);
        CreateSpikeStoneArea(centerPos, durationRounds, caster, casterLevel);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💎 {caster?.Stats?.CharacterName ?? "Caster"} casts Spike Stones!");
        sb.AppendLine($"  Area: 20-ft radius | Duration: {durationRounds} rounds");
        sb.AppendLine($"  1d8 piercing per 5 ft moved. Reflex DC 15 half. Half speed.");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    /// <summary>Creates a SpikeStoneAreaEffect at the specified position.</summary>
    public void CreateSpikeStoneArea(Vector3 centerPosition, int duration, CharacterController caster, int casterLevel)
    {
        var go = new GameObject("SpikeStoneArea");
        go.transform.position = centerPosition;
        var effect = go.AddComponent<SpikeStoneAreaEffect>();
        effect.CenterPosition = centerPosition;
        effect.RoundsRemaining = duration;
        effect.CasterLevel = casterLevel;

        Debug.Log($"[GameManager] Spike Stones area created at {centerPosition}, duration={duration}, CL={casterLevel}");
    }

    // ================================================================
    //  PLANT GROWTH AoE — PHB p.262
    //  Overgrowth: 100-ft radius, movement quartered, instantaneous.
    // ================================================================

    /// <summary>
    /// Resolves Plant Growth: creates a PlantGrowthAreaEffect.
    /// </summary>
    private bool TryResolvePlantGrowthSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.PLANT_GROWTH, StringComparison.Ordinal))
            return false;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;

        // Instantaneous but persists for rest of combat
        Vector3 centerPos = CalculateAreaCenter(aoeCells);
        CreatePlantGrowthArea(centerPos, 999, caster, casterLevel);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🌿 {caster?.Stats?.CharacterName ?? "Caster"} casts Plant Growth!");
        sb.AppendLine($"  Area: 100-ft radius overgrowth.");
        sb.AppendLine($"  Movement quartered (×4 cost). No save, no SR.");
        sb.AppendLine($"  Instantaneous effect (permanent overgrowth).");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    /// <summary>Creates a PlantGrowthAreaEffect at the specified position.</summary>
    public void CreatePlantGrowthArea(Vector3 centerPosition, int duration, CharacterController caster, int casterLevel)
    {
        var go = new GameObject("PlantGrowthArea");
        go.transform.position = centerPosition;
        var effect = go.AddComponent<PlantGrowthAreaEffect>();
        effect.CenterPosition = centerPosition;
        effect.RoundsRemaining = duration;
        effect.CasterLevel = casterLevel;

        Debug.Log($"[GameManager] Plant Growth area created at {centerPosition}, CL={casterLevel}");
    }

    // ================================================================
    //  HELPER: Calculate center position from AoE cells
    // ================================================================
    private Vector3 CalculateAreaCenter(HashSet<Vector2Int> aoeCells)
    {
        if (aoeCells == null || aoeCells.Count == 0)
            return Vector3.zero;

        float sumX = 0, sumY = 0;
        int count = 0;
        foreach (var cell in aoeCells)
        {
            sumX += cell.x;
            sumY += cell.y;
            count++;
        }

        if (count == 0) return Vector3.zero;

        float avgX = sumX / count;
        float avgY = sumY / count;

        // Convert grid position to world position
        if (Grid != null)
        {
            Vector2Int centerCell = new Vector2Int(Mathf.RoundToInt(avgX), Mathf.RoundToInt(avgY));
            SquareCell cell = Grid.GetCell(centerCell);
            if (cell != null)
                return cell.transform.position;
        }

        return new Vector3(avgX, 0, avgY);
    }
}
