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
        bool isGustOfWind = spell.SpellId == "gust_of_wind";
        if (!isObscuringMist && !isFogCloud && !isGustOfWind)
            return false;

        int casterLevel = caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;

        if (isGustOfWind)
        {
            int gustSaveDC = GetSpellSaveDC(caster, spell);
            var gustEffect = new GustOfWindEffect();
            gustEffect.Initialize(caster, spell, aoeCells, targets, gustSaveDC, casterLevel);
            string gustResult = gustEffect.ResolveEffect();

            var gustLog = new StringBuilder();
            gustLog.AppendLine("═══════════════════════════════════");
            gustLog.AppendLine($"✨ {caster.Stats.CharacterName} casts Gust of Wind!");
            gustLog.AppendLine($"  Area: 60-ft line ({aoeCells.Count} squares)");
            gustLog.AppendLine($"  Wind Strength: {WindStrength.Severe}");
            gustLog.AppendLine($"  Save DC: Fortitude {gustSaveDC}");
            if (!string.IsNullOrWhiteSpace(gustResult))
                gustLog.Append(gustResult);
            else
                gustLog.AppendLine("  No creatures are caught in the gust.");
            gustLog.Append("═══════════════════════════════════");
            log = gustLog.ToString();
            return true;
        }

        int durationRounds = ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel);
        bool usedFallbackDuration = false;
        if (durationRounds <= 0)
        {
            usedFallbackDuration = true;
            // Defensive fallback for malformed spell definitions; default to 10 rounds (1 minute).
            durationRounds = Mathf.Max(1, spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : 10);
            Debug.LogWarning($"[ConcealmentArea] {spell.SpellId} returned non-positive duration from CalculateDurationRounds. " +
                             $"Using fallback duration: {durationRounds} rounds (caster level {casterLevel}).");
        }

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
        if (usedFallbackDuration)
            sb.AppendLine("  ⚠ Duration fallback was used (definition returned non-positive duration).");
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

    private bool TryResolveGlitterdustSpell(CharacterController caster, SpellData spell, List<CharacterController> targets, HashSet<Vector2Int> aoeCells, out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null || !string.Equals(spell.SpellId, "glitterdust", System.StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDc = GetSpellSaveDC(caster, spell);

        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        CreateGlitterdustArea(centerPosition, durationRounds, casterLevel, caster);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts Glitterdust! (10-ft radius spread)");
        sb.AppendLine($"  Duration: {durationRounds} rounds | Will DC {saveDc} negates blindness only");
        sb.AppendLine($"  Outlined: all creatures in area | Invisibility concealment negated | Hide -40");
        sb.AppendLine();

        int affectedCount = 0;
        int blindedCount = 0;

        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                CharacterController target = targets[i];
                if (target == null || target.Stats == null || target.Stats.IsDead)
                    continue;

                StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
                if (statusMgr == null)
                    statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
                statusMgr.Init(target.Stats);

                ActiveSpellEffect effect = statusMgr.AddEffect(
                    spell,
                    caster.Stats.CharacterName,
                    casterLevel);

                int trackedDuration = effect != null ? effect.RemainingRounds : Mathf.Max(1, statusMgr.GetRemainingRounds("glitterdust"));

                bool blinded = false;
                int saveRoll = Random.Range(1, 21);
                int saveTotal = saveRoll + target.Stats.WillSave;
                if (saveTotal < saveDc)
                {
                    blinded = true;
                    blindedCount++;
                    if (_conditionService != null)
                    {
                        _conditionService.ApplyCondition(
                            target,
                            CombatConditionType.Blinded,
                            trackedDuration,
                            source: caster,
                            sourceNameOverride: spell.Name,
                            sourceCategory: "Spell",
                            sourceId: spell.SpellId);
                    }
                    else
                    {
                        target.ApplyCondition(CombatConditionType.Blinded, trackedDuration, spell.Name);
                    }
                }

                target.ApplyGlitterdustEffect(trackedDuration, caster, blindedByFailedSave: blinded);
                target.SetGlitterdustBlindedState(blinded);
                affectedCount++;

                string blindText = blinded
                    ? $"FAILED Will d20({saveRoll}) + {target.Stats.WillSave} = {saveTotal} vs DC {saveDc} → BLINDED"
                    : $"Will d20({saveRoll}) + {target.Stats.WillSave} = {saveTotal} vs DC {saveDc} → not blinded";

                sb.AppendLine($"  • {target.Stats.CharacterName}: outlined in golden dust; {blindText}.");

                if (target.HasActiveInvisibilityEffect)
                    sb.AppendLine($"    👁 Invisibility concealment suppressed for all observers.");
            }
        }

        if (affectedCount == 0)
            sb.AppendLine("  No creatures in area.");

        sb.AppendLine();
        sb.AppendLine($"  Result: {affectedCount} outlined, {blindedCount} blinded.");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    public void CreateGlitterdustArea(Vector3 centerPosition, int durationRounds, int casterLevel, CharacterController caster)
    {
        GameObject glitterObj = new GameObject("Glitterdust_Area");
        glitterObj.transform.position = centerPosition;

        GlitterdustAreaEffect glitter = glitterObj.AddComponent<GlitterdustAreaEffect>();
        glitter.CenterPosition = centerPosition;
        glitter.RoundsRemaining = Mathf.Max(1, durationRounds);
        glitter.CasterLevel = Mathf.Max(1, casterLevel);
        glitter.Caster = caster;
    }
}
