using System.Collections.Generic;
using System.Text;
using UnityEngine;

public partial class GameManager
{
    private bool TryHandleConcealmentAreaSpellCast(
        CharacterController caster,
        SpellData spell,
        HashSet<Vector2Int> aoeCells,
        List<CharacterController> targets,
        out string log)
    {
        log = string.Empty;
        if (caster == null || spell == null || aoeCells == null)
            return false;

        bool isObscuringMist = spell.SpellId == "obscuring_mist";
        bool isFogCloud = spell.SpellId == "fog_cloud";
        if (!isObscuringMist && !isFogCloud)
            return false;

        int casterLevel = caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int durationRounds = ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel);
        if (durationRounds <= 0)
            durationRounds = Mathf.Max(1, spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : 10);

        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);

        if (isObscuringMist)
            CreateObscuringMistArea(centerPosition, durationRounds, casterLevel, caster);
        else
            CreateFogCloudArea(centerPosition, durationRounds, casterLevel, caster);

        var sb = new StringBuilder();
        string spellName = isObscuringMist ? "Obscuring Mist" : "Fog Cloud";
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts {spellName}!");
        sb.AppendLine($"  Area: 20-ft radius spread ({aoeCells.Count} squares)");
        sb.AppendLine($"  Duration: {durationRounds} rounds");
        sb.AppendLine("  Effect: Creatures inside have concealment (20% miss chance)");

        if (targets != null && targets.Count > 0)
        {
            sb.Append("  Currently affected: ");
            for (int i = 0; i < targets.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(targets[i] != null && targets[i].Stats != null ? targets[i].Stats.CharacterName : "Unknown");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("  No creatures currently inside the fog.");
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    public void CreateObscuringMistArea(Vector3 centerPosition, int durationRounds, int casterLevel, CharacterController caster)
    {
        GameObject mistObject = new GameObject("ObscuringMist_Area");
        mistObject.transform.position = centerPosition;

        ObscuringMistAreaEffect mist = mistObject.AddComponent<ObscuringMistAreaEffect>();
        mist.CenterPosition = centerPosition;
        mist.RoundsRemaining = Mathf.Max(1, durationRounds);
        mist.CasterLevel = Mathf.Max(1, casterLevel);
        mist.Caster = caster;
    }

    public void CreateFogCloudArea(Vector3 centerPosition, int durationRounds, int casterLevel, CharacterController caster)
    {
        GameObject fogObject = new GameObject("FogCloud_Area");
        fogObject.transform.position = centerPosition;

        FogCloudAreaEffect fog = fogObject.AddComponent<FogCloudAreaEffect>();
        fog.CenterPosition = centerPosition;
        fog.RoundsRemaining = Mathf.Max(1, durationRounds);
        fog.CasterLevel = Mathf.Max(1, casterLevel);
        fog.Caster = caster;
    }

    private Vector3 GetAreaCenterWorldPosition(HashSet<Vector2Int> cells, Vector2Int fallbackCell)
    {
        if (cells == null || cells.Count == 0)
            return SquareGridUtils.GridToWorld(fallbackCell);

        float sumX = 0f;
        float sumY = 0f;
        int count = 0;

        foreach (Vector2Int cell in cells)
        {
            sumX += cell.x;
            sumY += cell.y;
            count++;
        }

        if (count <= 0)
            return SquareGridUtils.GridToWorld(fallbackCell);

        int centerX = Mathf.RoundToInt(sumX / count);
        int centerY = Mathf.RoundToInt(sumY / count);
        return SquareGridUtils.GridToWorld(centerX, centerY);
    }
}
