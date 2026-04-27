using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Resolves Gust of Wind (PHB 3.5e) creature interactions and registers temporary severe wind.
/// </summary>
public class GustOfWindEffect
{
    private CharacterController caster;
    private SpellData spell;
    private HashSet<Vector2Int> affectedCells;
    private List<CharacterController> targets;
    private int saveDC;
    private int casterLevel;

    public void Initialize(
        CharacterController caster,
        SpellData spell,
        HashSet<Vector2Int> affectedCells,
        List<CharacterController> targets,
        int saveDC,
        int casterLevel)
    {
        this.caster = caster;
        this.spell = spell;
        this.affectedCells = affectedCells != null ? new HashSet<Vector2Int>(affectedCells) : new HashSet<Vector2Int>();
        this.targets = targets ?? new List<CharacterController>();
        this.saveDC = saveDC;
        this.casterLevel = Mathf.Max(1, casterLevel);
    }

    public string ResolveEffect()
    {
        var log = new StringBuilder();

        RegisterWindZone();

        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;

            log.AppendLine($"  → {target.Stats.CharacterName}");

            if (spell != null && spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                int srRoll = Random.Range(1, 21);
                int srTotal = srRoll + casterLevel;
                bool overcameSR = srTotal >= target.Stats.SpellResistance;
                log.AppendLine($"    SR check: d20({srRoll}) + CL {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} {(overcameSR ? "PASS" : "BLOCKED")}");
                if (!overcameSR)
                    continue;
            }

            int saveRoll = Random.Range(1, 21);
            int saveTotal = saveRoll + target.Stats.FortitudeSave;
            bool saveSucceeded = saveTotal >= saveDC;

            log.AppendLine($"    Fortitude save: d20({saveRoll}) + {target.Stats.FortitudeSave} = {saveTotal} vs DC {saveDC} {(saveSucceeded ? "SUCCESS" : "FAIL")}");

            if (saveSucceeded)
            {
                log.AppendLine("    Holds position against the blast.");
                continue;
            }

            ApplyFailedSaveOutcome(target, log);
        }

        return log.ToString();
    }

    private void RegisterWindZone()
    {
        if (affectedCells == null || affectedCells.Count == 0)
            return;

        Vector3 direction = EstimateDirectionFromCells();
        var wind = new WindEffect
        {
            EffectName = "Gust of Wind",
            Caster = caster,
            OriginPosition = caster != null ? SquareGridUtils.GridToWorld(caster.GridPosition) : Vector3.zero,
            Direction = direction,
            Length = 12f,
            AffectedRadius = 0f,
            Strength = WindStrength.Severe,
            RoundsRemaining = 1,
            SaveDC = saveDC,
            AffectedCells = new HashSet<Vector2Int>(affectedCells)
        };

        WindEffectManager.Instance.RegisterWindEffect(wind);
    }

    private Vector3 EstimateDirectionFromCells()
    {
        if (caster == null)
            return Vector3.right;

        Vector2Int origin = caster.GridPosition;
        Vector2 aggregate = Vector2.zero;

        foreach (Vector2Int cell in affectedCells)
            aggregate += new Vector2(cell.x - origin.x, cell.y - origin.y);

        if (aggregate.sqrMagnitude < 0.001f)
            return Vector3.right;

        Vector2 normalized = aggregate.normalized;
        return new Vector3(normalized.x, normalized.y, 0f);
    }

    private void ApplyFailedSaveOutcome(CharacterController target, StringBuilder log)
    {
        SizeCategory size = target.Stats.CurrentSizeCategory;

        if (size <= SizeCategory.Small)
        {
            target.ApplyCondition(CombatConditionType.Prone, 1, "Gust of Wind");
            log.AppendLine("    Blown down and knocked prone (Small or smaller).");
            return;
        }

        if (size == SizeCategory.Medium)
        {
            log.AppendLine("    Checked by the wind (cannot advance against the gust until next turn). [Movement restriction log-only]");
            return;
        }

        if (size <= SizeCategory.Huge)
        {
            log.AppendLine("    Checked by the wind (movement speed halved until next turn). [Movement restriction log-only]");
            return;
        }

        log.AppendLine("    Too massive to be meaningfully affected.");
    }
}
