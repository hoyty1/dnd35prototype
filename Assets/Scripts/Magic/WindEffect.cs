using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime description of an active wind zone/effect.
/// </summary>
public class WindEffect
{
    public string EffectName { get; set; }
    public CharacterController Caster { get; set; }
    public Vector3 OriginPosition { get; set; }
    public Vector3 Direction { get; set; }
    public float Length { get; set; }
    public float AffectedRadius { get; set; }
    public WindStrength Strength { get; set; }
    public int RoundsRemaining { get; set; }
    public int SaveDC { get; set; }

    /// <summary>
    /// Optional direct grid footprint; when present this is preferred for overlap checks.
    /// </summary>
    public HashSet<Vector2Int> AffectedCells { get; set; }

    public bool AffectsCell(Vector2Int cell)
    {
        if (AffectedCells != null && AffectedCells.Count > 0)
            return AffectedCells.Contains(cell);

        return AffectsPosition(SquareGridUtils.GridToWorld(cell));
    }

    public bool AffectsPosition(Vector3 position)
    {
        if (Length > 0f)
            return IsPositionInLine(position);

        if (AffectedRadius > 0f)
            return Vector3.Distance(OriginPosition, position) <= AffectedRadius;

        return false;
    }

    private bool IsPositionInLine(Vector3 position)
    {
        Vector3 normalizedDirection = Direction.sqrMagnitude > 0.001f ? Direction.normalized : Vector3.right;
        Vector3 toPosition = position - OriginPosition;

        float along = Vector3.Dot(toPosition, normalizedDirection);
        if (along < 0f || along > Length)
            return false;

        Vector3 closestPoint = OriginPosition + (normalizedDirection * along);
        float perpendicularDistance = Vector3.Distance(position, closestPoint);

        // 10-ft wide line (2 squares): 1 square to each side of centerline.
        return perpendicularDistance <= 1f;
    }
}

/// <summary>
/// Tracks active wind effects and applies wind-vs-fog dispersion rules.
/// </summary>
public class WindEffectManager : MonoBehaviour
{
    private static WindEffectManager instance;

    public static bool HasInstance => instance != null;

    public static WindEffectManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("WindEffectManager");
                instance = go.AddComponent<WindEffectManager>();
                DontDestroyOnLoad(go);
            }

            return instance;
        }
    }

    private readonly List<WindEffect> activeWindEffects = new List<WindEffect>();

    public void RegisterWindEffect(WindEffect wind)
    {
        if (wind == null)
            return;

        activeWindEffects.Add(wind);
        Debug.Log($"[WindEffectManager] Registered '{wind.EffectName}' (Strength: {wind.Strength}).");
        CheckForFogDispersion(wind);
    }

    public void RemoveWindEffect(WindEffect wind)
    {
        if (wind == null)
            return;

        activeWindEffects.Remove(wind);
    }

    public void OnRoundStart()
    {
        if (activeWindEffects.Count == 0)
            return;

        var copy = new List<WindEffect>(activeWindEffects);
        foreach (WindEffect wind in copy)
        {
            if (wind == null)
                continue;

            wind.RoundsRemaining--;
            if (wind.RoundsRemaining <= 0)
            {
                RemoveWindEffect(wind);
                continue;
            }

            CheckForFogDispersion(wind);
        }

        activeWindEffects.RemoveAll(w => w == null || w.RoundsRemaining <= 0);
    }

    public void ClearAllWindEffects()
    {
        activeWindEffects.Clear();
    }

    private void CheckForFogDispersion(WindEffect wind)
    {
        if (wind == null || !AreaEffectManager.HasInstance)
            return;

        List<PersistentAreaEffect> areaEffects = AreaEffectManager.Instance.GetAllAreaEffects();
        for (int i = 0; i < areaEffects.Count; i++)
        {
            PersistentAreaEffect area = areaEffects[i];
            if (area == null || !area.DispersibleByWind)
                continue;

            if (wind.Strength < area.RequiredWindStrength)
                continue;

            bool overlaps = false;
            foreach (Vector2Int cell in area.AffectedCells)
            {
                if (wind.AffectsCell(cell))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
                continue;

            if (wind.Strength >= WindStrength.Severe)
            {
                area.LogWindDispersion($"{wind.EffectName} instantly disperses {area.EffectName}!");
                area.ExpireEffect();
                continue;
            }

            int roundsToDisperse = wind.Strength >= WindStrength.Strong ? 1 : 4;
            if (!area.IsBeingDispersedByWind)
            {
                area.IsBeingDispersedByWind = true;
                area.WindDispersionCounter = roundsToDisperse;
                area.LogWindDispersion($"{wind.EffectName} begins dispersing {area.EffectName} ({roundsToDisperse} round(s)).");
            }
            else if (roundsToDisperse < area.WindDispersionCounter)
            {
                area.WindDispersionCounter = roundsToDisperse;
                area.LogWindDispersion($"{wind.EffectName} intensifies; {area.EffectName} now disperses in {roundsToDisperse} round(s).");
            }
        }
    }
}
