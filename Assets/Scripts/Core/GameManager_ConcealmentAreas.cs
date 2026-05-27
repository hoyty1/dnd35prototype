using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

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

        bool isObscuringMist = spell.SpellId == SpellNames.OBSCURING_MIST;
        bool isFogCloud = spell.SpellId == SpellNames.FOG_CLOUD;
        bool isDarkness = spell.SpellId == SpellNames.DARKNESS;
        bool isGustOfWind = spell.SpellId == SpellNames.GUST_OF_WIND;
        bool isSleetStorm = spell.SpellId == SpellNames.SLEET_STORM;
        bool isStinkingCloud = spell.SpellId == SpellNames.STINKING_CLOUD;
        bool isSolidFog = spell.SpellId == SpellNames.SOLID_FOG;
        if (!isObscuringMist && !isFogCloud && !isDarkness && !isGustOfWind && !isSleetStorm && !isStinkingCloud && !isSolidFog)
            return false;

        int casterLevel = caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;

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

        // Handle Sleet Storm
        if (isSleetStorm)
        {
            int sleetDuration = Mathf.Max(1, casterLevel); // 1 round/level
            Vector3 sleetCenter = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
            CreateSleetStormArea(sleetCenter, sleetDuration, casterLevel, caster);

            var sleetLog = new StringBuilder();
            sleetLog.AppendLine("═══════════════════════════════════");
            sleetLog.AppendLine($"✨ {caster.Stats.CharacterName} casts Sleet Storm!");
            sleetLog.AppendLine($"  Area: 40-ft radius cylinder ({aoeCells.Count} squares)");
            sleetLog.AppendLine($"  Duration: {sleetDuration} rounds");
            sleetLog.AppendLine("  • Blocks all sight (including darkvision) — total concealment beyond 5 ft");
            sleetLog.AppendLine("  • Icy ground: DC 10 Balance to move at half speed; fail by 5+ = fall prone");
            sleetLog.AppendLine("  • Concentration DC 5 + spell level to cast inside");
            sleetLog.AppendLine("  • No save, no SR");

            if (targets != null && targets.Count > 0)
            {
                sleetLog.Append("  Currently affected: ");
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i > 0) sleetLog.Append(", ");
                    sleetLog.Append(targets[i] != null && targets[i].Stats != null ? targets[i].Stats.CharacterName : "Unknown");
                }
                sleetLog.AppendLine();
            }

            sleetLog.Append("═══════════════════════════════════");
            log = sleetLog.ToString();
            return true;
        }

        // Handle Stinking Cloud
        if (isStinkingCloud)
        {
            int cloudDuration = Mathf.Max(1, casterLevel); // 1 round/level
            int saveDc = GetSpellSaveDC(caster, spell);
            Vector3 cloudCenter = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
            CreateStinkingCloudArea(cloudCenter, cloudDuration, casterLevel, saveDc, caster);

            var cloudLog = new StringBuilder();
            cloudLog.AppendLine("═══════════════════════════════════");
            cloudLog.AppendLine($"✨ {caster.Stats.CharacterName} casts Stinking Cloud!");
            cloudLog.AppendLine($"  Area: 20-ft radius spread ({aoeCells.Count} squares)");
            cloudLog.AppendLine($"  Duration: {cloudDuration} rounds | Fort DC {saveDc}");
            cloudLog.AppendLine("  • Fort save each round or become nauseated (can only take move action)");
            cloudLog.AppendLine("  • Nausea persists 1d4+1 rounds after leaving");
            cloudLog.AppendLine("  • Vision blocked (like Fog Cloud)");
            cloudLog.AppendLine("  • Immune: undead, constructs, non-breathers, poison immune");

            if (targets != null && targets.Count > 0)
            {
                cloudLog.Append("  Currently affected: ");
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i > 0) cloudLog.Append(", ");
                    cloudLog.Append(targets[i] != null && targets[i].Stats != null ? targets[i].Stats.CharacterName : "Unknown");
                }
                cloudLog.AppendLine();
            }

            cloudLog.Append("═══════════════════════════════════");
            log = cloudLog.ToString();
            return true;
        }

        // Handle Solid Fog
        if (isSolidFog)
        {
            int solidFogDuration = ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel);
            if (solidFogDuration <= 0)
                solidFogDuration = Mathf.Max(1, casterLevel * 10); // 1 min/level = 10 rounds/level fallback
            Vector3 solidFogCenter = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
            CreateSolidFogArea(solidFogCenter, solidFogDuration, casterLevel, caster);

            var solidFogLog = new StringBuilder();
            solidFogLog.AppendLine("═══════════════════════════════════");
            solidFogLog.AppendLine($"✨ {caster.Stats.CharacterName} casts Solid Fog!");
            solidFogLog.AppendLine($"  Area: 20-ft radius spread, 20 ft. high ({aoeCells.Count} squares)");
            solidFogLog.AppendLine($"  Duration: {solidFogDuration} rounds ({solidFogDuration / 10} min)");
            solidFogLog.AppendLine("  • Concealment: 20% miss chance at 5 ft, 50% (total) beyond 5 ft");
            solidFogLog.AppendLine("  • Movement speed halved inside the fog");
            solidFogLog.AppendLine("  • -2 penalty to melee attack and damage rolls");
            solidFogLog.AppendLine("  • Normal ranged weapon attacks blocked (magic rays still work)");
            solidFogLog.AppendLine("  • No save, no SR");

            if (targets != null && targets.Count > 0)
            {
                solidFogLog.Append("  Currently affected: ");
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i > 0) solidFogLog.Append(", ");
                    solidFogLog.Append(targets[i] != null && targets[i].Stats != null ? targets[i].Stats.CharacterName : "Unknown");
                }
                solidFogLog.AppendLine();
            }

            solidFogLog.Append("═══════════════════════════════════");
            log = solidFogLog.ToString();
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
        else if (isFogCloud)
            CreateFogCloudArea(centerPosition, durationRounds, casterLevel, caster);
        else
            CreateDarknessArea(centerPosition, durationRounds, casterLevel, caster);

        var sb = new StringBuilder();
        string spellName = isObscuringMist ? "Obscuring Mist" : (isFogCloud ? "Fog Cloud" : "Darkness");
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts {spellName}!");
        sb.AppendLine($"  Area: 20-ft radius spread ({aoeCells.Count} squares)");
        sb.AppendLine($"  Duration: {durationRounds} rounds");
        if (usedFallbackDuration)
            sb.AppendLine("  ⚠ Duration fallback was used (definition returned non-positive duration).");

        if (isDarkness)
        {
            sb.AppendLine("  Effect: Darkness does not block vision or targeting");
            sb.AppendLine("  Effect: 20% miss chance if attacker/target is in darkness or attack crosses darkness");
        }
        else
        {
            sb.AppendLine("  Effect: Creatures inside have concealment (20% miss chance)");
        }

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

    public void CreateDarknessArea(Vector3 centerPosition, int durationRounds, int casterLevel, CharacterController caster)
    {
        GameObject darknessObject = new GameObject("Darkness_Area");
        darknessObject.transform.position = centerPosition;

        DarknessAreaEffect darkness = darknessObject.AddComponent<DarknessAreaEffect>();
        darkness.CenterPosition = centerPosition;
        darkness.RoundsRemaining = Mathf.Max(1, durationRounds);
        darkness.CasterLevel = Mathf.Max(1, casterLevel);
        darkness.Caster = caster;
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

    public void CreateSleetStormArea(Vector3 centerPosition, int durationRounds, int casterLevel, CharacterController caster)
    {
        GameObject sleetObject = new GameObject("SleetStorm_Area");
        sleetObject.transform.position = centerPosition;

        SleetStormAreaEffect sleet = sleetObject.AddComponent<SleetStormAreaEffect>();
        sleet.CenterPosition = centerPosition;
        sleet.RoundsRemaining = Mathf.Max(1, durationRounds);
        sleet.CasterLevel = Mathf.Max(1, casterLevel);
        sleet.Caster = caster;
    }

    public void CreateStinkingCloudArea(Vector3 centerPosition, int durationRounds, int casterLevel, int saveDc, CharacterController caster)
    {
        GameObject cloudObject = new GameObject("StinkingCloud_Area");
        cloudObject.transform.position = centerPosition;

        StinkingCloudAreaEffect cloud = cloudObject.AddComponent<StinkingCloudAreaEffect>();
        cloud.CenterPosition = centerPosition;
        cloud.RoundsRemaining = Mathf.Max(1, durationRounds);
        cloud.CasterLevel = Mathf.Max(1, casterLevel);
        cloud.SaveDC = Mathf.Max(1, saveDc);
        cloud.Caster = caster;
    }

    public void CreateSolidFogArea(Vector3 centerPosition, int durationRounds, int casterLevel, CharacterController caster)
    {
        GameObject fogObject = new GameObject("SolidFog_Area");
        fogObject.transform.position = centerPosition;

        SolidFogAreaEffect fog = fogObject.AddComponent<SolidFogAreaEffect>();
        fog.CenterPosition = centerPosition;
        fog.RoundsRemaining = Mathf.Max(1, durationRounds);
        fog.CasterLevel = Mathf.Max(1, casterLevel);
        fog.Caster = caster;
    }

    private bool TryResolveGlitterdustSpell(CharacterController caster, SpellData spell, List<CharacterController> targets, HashSet<Vector2Int> aoeCells, out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null || !string.Equals(spell.SpellId, SpellNames.GLITTERDUST, System.StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
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

                StatusEffectManager statusMgr = target.StatusEffectManager;
                if (statusMgr == null)
                    statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
                statusMgr.Init(target.Stats);

                ActiveSpellEffect effect = statusMgr.AddEffect(
                    spell,
                    caster.Stats.CharacterName,
                    casterLevel);

                int trackedDuration = effect != null ? effect.RemainingRounds : Mathf.Max(1, statusMgr.GetRemainingRounds(SpellNames.GLITTERDUST));

                bool blinded = false;
                int saveRoll = DiceRoller.D20();
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

    private bool TryResolveWebSpell(CharacterController caster, SpellData spell, List<CharacterController> targets, HashSet<Vector2Int> aoeCells, out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null || !string.Equals(spell.SpellId, SpellNames.WEB, System.StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDc = GetSpellSaveDC(caster, spell);

        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        CreateWebArea(centerPosition, durationRounds, casterLevel, saveDc, caster);

        int totalTargets = 0;
        int entangledTargets = 0;
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts Web! (20-ft radius spread)");
        sb.AppendLine($"  Duration: {durationRounds} rounds | Reflex DC {saveDc} negates entanglement");
        sb.AppendLine($"  Effect: difficult terrain; entangled targets cannot move and can escape with Str/Escape Artist DC 20");
        sb.AppendLine($"  Fire: ignites web (2d4 fire to occupants), web burns away in 1 round");
        sb.AppendLine($"  Web section HP: {WebAreaEffect.SectionHitPoints}");
        sb.AppendLine();

        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                CharacterController target = targets[i];
                if (target == null || target.Stats == null || target.Stats.IsDead)
                    continue;

                totalTargets++;
                bool entangled = target.HasCondition(CombatConditionType.Entangled) && IsEntangledByWeb(target);
                if (entangled)
                    entangledTargets++;

                sb.AppendLine($"  • {target.Stats.CharacterName}: {(entangled ? "entangled" : "avoided entanglement (still difficult terrain)")}");
            }
        }

        if (totalTargets == 0)
            sb.AppendLine("  No creatures in area at cast time.");

        sb.AppendLine();
        sb.AppendLine($"  Result: {entangledTargets}/{Mathf.Max(0, totalTargets)} creatures entangled.");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    public void CreateWebArea(Vector3 centerPosition, int durationRounds, int casterLevel, int saveDc, CharacterController caster)
    {
        GameObject webObject = new GameObject("Web_Area");
        webObject.transform.position = centerPosition;

        WebAreaEffect web = webObject.AddComponent<WebAreaEffect>();
        web.CenterPosition = centerPosition;
        web.RoundsRemaining = Mathf.Max(1, durationRounds);
        web.CasterLevel = Mathf.Max(1, casterLevel);
        web.SaveDC = Mathf.Max(1, saveDc);
        web.Caster = caster;
    }
}
