// ============================================================================
// GameManager_Spells_I.cs — Spell resolution methods starting with "I".
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
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
    //  INVISIBILITY PURGE  (PHB p.245)
    // ================================================================
    // 5-ft/level emanation centered on caster. 1 min/level.
    // Dispels invisibility on any creature entering or within the area.
    // This implementation: sets InvisibilityPurgeActive on caster and
    // strips Invisible condition from enemies in radius at cast time.

    private bool TryResolveInvisibilityPurgeSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.INVISIBILITY_PURGE)
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 10; // 1 min/level = 10 rounds/level
        int radiusSquares = Mathf.Max(1, casterLevel); // 5 ft/level = 1 square/level

        // Set caster state
        caster.Stats.InvisibilityPurgeActive = true;
        caster.Stats.InvisibilityPurgeRoundsRemaining = durationRounds;

        // Track via StatusEffectManager
        var statusMgr = caster.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null)
                effect.RemainingRounds = durationRounds;
        }

        // Purge invisibility from all characters in radius
        int purgeCount = 0;
        List<CharacterController> allChars = GetAllCharacters();
        foreach (var ch in allChars)
        {
            if (ch == null || ch.Stats == null || ch.Stats.IsDead) continue;
            if (ch == caster) continue;

            int dist = SquareGridUtils.GetDistance(caster.GridPosition, ch.GridPosition);
            if (dist > radiusSquares) continue;

            if (ch.HasCondition(CombatConditionType.Invisible))
            {
                ch.RemoveCondition(CombatConditionType.Invisible);
                purgeCount++;
                CombatUI?.ShowCombatLog($"<color=#CCDDFF>  👁 {ch.Stats.CharacterName}'s invisibility is dispelled!</color>");
            }
        }

        CombatUI?.ShowCombatLog($"<color=#AADDFF>👁✨ Invisibility Purge! {casterName} radiates a {radiusSquares * 5}-ft anti-invisibility field for {durationRounds} rounds. {purgeCount} creature(s) revealed.</color>");
        Debug.Log($"[InvisibilityPurge] {casterName}: radius {radiusSquares} sq, duration {durationRounds} rounds, purged {purgeCount}");

        return true;
    }

    // ================================================================
    //  INVISIBILITY SPHERE — Mobile Emanation (PHB p.245)
    // ================================================================

    /// <summary>
    /// Applies the Invisibility Sphere spell (Bard 3, Sorcerer/Wizard 3).
    /// Per PHB p.245: 10-ft-radius emanation centered on the recipient.
    ///   - All creatures within the emanation at cast time become invisible.
    ///   - The area moves with the recipient (mobile emanation).
    ///   - Creatures that LEAVE the emanation become visible immediately.
    ///   - Creatures that ENTER the emanation later do NOT become invisible.
    ///   - If a creature OTHER THAN the recipient attacks, only that creature
    ///     becomes visible.
    ///   - If the RECIPIENT attacks, the entire spell ends.
    ///
    /// Called from ApplySpellBuff when spell.SpellId == INVISIBILITY_SPHERE.
    /// </summary>
    private ActiveSpellEffect ApplyInvisibilitySphere(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        // Track the spell on the recipient so duration/dispel/dismiss
        // work via the standard StatusEffectManager path.
        StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect == null)
        {
            UpdateAllStatsUI();
            return null;
        }

        SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
        if (recipientSpellComp != null)
            recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

        int durationRounds = Mathf.Max(1, effect.RemainingRounds);

        // Build & register the emanation
        var sphere = InvisibilitySphereEffect.Create(recipient, caster, durationRounds, casterLevel);
        RegisterEmanation(sphere);

        // Capture initial affected creatures (everyone within 10 ft of recipient)
        List<CharacterController> all = GetAllCharacters();
        sphere.ApplyInitialAffectedCreatures(all);

        // Logging
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        bool selfCast = recipient == caster;
        string castLine = selfCast
            ? $"<color=#88CCFF>👻 {casterName} casts Invisibility Sphere on self!</color>"
            : $"<color=#88CCFF>👻 {casterName} casts Invisibility Sphere on {recipient.Stats.CharacterName}!</color>";
        CombatUI?.ShowCombatLog(castLine);

        int affectedCount = sphere.InitiallyAffectedCreatures != null ? sphere.InitiallyAffectedCreatures.Count : 0;
        CombatUI?.ShowCombatLog($"<color=#A6F3FF>   A 10-ft emanation forms around {recipient.Stats.CharacterName}; {affectedCount} creature(s) become invisible.</color>");

        if (affectedCount > 0)
        {
            var sb = new StringBuilder("<color=#A6F3FF>   Affected: ");
            for (int i = 0; i < sphere.InitiallyAffectedCreatures.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var c = sphere.InitiallyAffectedCreatures[i];
                sb.Append(c != null && c.Stats != null ? c.Stats.CharacterName : "?");
            }
            sb.Append("</color>");
            CombatUI?.ShowCombatLog(sb.ToString());
        }

        CombatUI?.ShowCombatLog($"<color=#A6F3FF>   Duration: {effect.GetDurationDisplayString()}. Leaving the sphere or attacking ends invisibility.</color>");

        UpdateEnemyLastKnownPositionForInvisibility(recipient);
        UpdateAllStatsUI();
        return effect;
    }

    /// <summary>
    /// Per-round refresh of all active Invisibility Sphere emanations.
    /// Removes invisibility from creatures who have stepped out of the sphere.
    /// Called from TickEmanations (and may be invoked after movement actions).
    /// </summary>
    public void RefreshInvisibilitySpheres()
    {
        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            spheres[i]?.RefreshMembership();
        }
    }

    /// <summary>
    /// Ends an Invisibility Sphere centered on the given recipient (e.g. when
    /// the recipient attacks, the spell expires/is dismissed, or is dispelled).
    /// All initially-affected creatures become visible at once.
    /// </summary>
    /// <param name="recipient">The creature on whom the sphere is centered.</param>
    /// <param name="reason">Free-text reason shown in the combat log.</param>
    public void EndInvisibilitySphereForRecipient(CharacterController recipient, string reason = "spell ended")
    {
        if (recipient == null) return;

        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            var s = spheres[i];
            if (s == null || s.HasEnded) continue;
            if (s.CenterCreature != recipient) continue;

            s.EndForAll(reason);
        }
    }

    /// <summary>
    /// Returns the active Invisibility Sphere this creature is currently
    /// invisible from, or null if none.
    /// </summary>
    public InvisibilitySphereEffect GetInvisibilitySphereAffecting(CharacterController creature)
    {
        if (creature == null) return null;

        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            var s = spheres[i];
            if (s == null || s.HasEnded) continue;
            if (s.IsCreatureAffected(creature))
                return s;
        }
        return null;
    }

    /// <summary>
    /// Handles an attack made by a creature that is invisible due to an
    /// Invisibility Sphere. Per PHB p.245:
    ///   - If the attacker is the recipient → ALL affected creatures become visible.
    ///   - Otherwise → only that one creature becomes visible.
    /// Returns true if a sphere matched and was processed (so the standard
    /// invisibility-on-attack flow can be skipped for this attacker).
    /// </summary>
    public bool TryHandleInvisibilitySphereAttack(CharacterController attacker, string reason = "attacked")
    {
        if (attacker == null) return false;

        var sphere = GetInvisibilitySphereAffecting(attacker);
        if (sphere == null) return false;

        if (sphere.CenterCreature == attacker)
        {
            sphere.EndForAll(reason);

            // Also clean the recipient's tracking ActiveSpellEffect so the duration
            // bar / dismiss UI clears on the same round as the sphere ending.
            StatusEffectManager mgr = attacker.GetComponent<StatusEffectManager>();
            mgr?.RemoveEffectsBySpellId(SpellNames.INVISIBILITY_SPHERE);
        }
        else
        {
            sphere.EndForCreature(attacker, reason);
        }
        return true;
    }

    // ================================================================
    //  ICE STORM — AoE Damage, No Save (PHB p.243)
    // ================================================================

    private static bool IsIceStormSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.ICE_STORM, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Ice Storm: 3d6 bludgeoning + 2d6 cold (no save), SR: Yes.
    /// Area becomes difficult terrain for 1 round (logged for awareness).
    /// PHB p.243
    /// </summary>
    private bool TryResolveIceStormAoE(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!IsIceStormSpell(spell))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"❄ {caster.Stats.CharacterName} casts Ice Storm! (20-ft radius cylinder)");
        sb.AppendLine($"  [Level {spell.SpellLevel}] {spell.School}");
        sb.AppendLine($"  Damage: 3d6 bludgeoning + 2d6 cold (NO SAVE)");
        sb.AppendLine($"  SR: Yes | Area becomes icy difficult terrain for 1 round");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No valid targets in area!");
        }
        else
        {
            int targetIndex = 0;
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead)
                    continue;

                targetIndex++;
                sb.AppendLine($"  --- Target {targetIndex}: {target.Stats.CharacterName} ---");

                // Check Spell Resistance
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srCheckRoll = UnityEngine.Random.Range(1, 21);
                    int srCheckTotal = srCheckRoll + casterLevel;
                    bool srOvercome = srCheckTotal >= target.Stats.SpellResistance;

                    sb.AppendLine($"  SR Check: d20({srCheckRoll}) + {casterLevel} = {srCheckTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");

                    if (!srOvercome)
                    {
                        sb.AppendLine($"  {target.Stats.CharacterName} resists Ice Storm via Spell Resistance!");
                        sb.AppendLine();
                        continue;
                    }
                }

                // Roll 3d6 bludgeoning
                int bludgeoningDamage = 0;
                for (int i = 0; i < 3; i++)
                    bludgeoningDamage += UnityEngine.Random.Range(1, 7);

                // Roll 2d6 cold
                int coldDamage = 0;
                for (int i = 0; i < 2; i++)
                    coldDamage += UnityEngine.Random.Range(1, 7);

                int totalDamage = bludgeoningDamage + coldDamage;

                // D&D 3.5e PHB p.206: Blinking creatures take half damage from area attacks
                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    totalDamage = Mathf.Max(1, totalDamage / 2);

                sb.AppendLine($"  Damage: {bludgeoningDamage} bludgeoning + {coldDamage} cold = {totalDamage} total (no save)");
                if (targetIsBlinking)
                    sb.AppendLine($"  Blink: area damage halved");

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(totalDamage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                CheckConcentrationOnDamage(target, totalDamage);

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                }

                sb.AppendLine();
            }
        }

        // ── WALL OF ICE DAMAGE FROM ICE STORM ──
        // Only damage the specific wall sections that overlap the Ice Storm AoE.
        if (aoeCells != null && AreaEffectManager.HasInstance)
        {
            var wallOverlap = new Dictionary<WallOfIceAreaEffect, HashSet<Vector2Int>>();
            foreach (Vector2Int cell in aoeCells)
            {
                WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell);
                if (wall != null)
                {
                    if (!wallOverlap.ContainsKey(wall))
                        wallOverlap[wall] = new HashSet<Vector2Int>();
                    wallOverlap[wall].Add(cell);
                }
            }

            foreach (var kvp in wallOverlap)
            {
                WallOfIceAreaEffect wall = kvp.Key;
                HashSet<Vector2Int> overlapCells = kvp.Value;
                if (wall == null || wall.WallHP <= 0)
                    continue;

                // 3d6 bludgeoning + 2d6 cold (no save for objects)
                int wallBludg = 0;
                for (int i = 0; i < 3; i++) wallBludg += UnityEngine.Random.Range(1, 7);
                int wallCold = 0;
                for (int i = 0; i < 2; i++) wallCold += UnityEngine.Random.Range(1, 7);
                int wallTotal = wallBludg + wallCold;

                sb.AppendLine($"  --- Wall of Ice ({overlapCells.Count} section(s) hit) ---");
                sb.AppendLine($"  Bludgeoning + cold damage to overlapping sections: {wallTotal}");

                bool destroyed = wall.DealDamageToOverlappingCells(wallTotal, overlapCells, false);

                if (destroyed)
                    sb.AppendLine($"  💥 The Wall of Ice is destroyed by Ice Storm!");
                else
                    sb.AppendLine($"  Wall HP: {wall.WallHP}/{wall.WallMaxHP}");

                sb.AppendLine();
            }
        }

        // Note about difficult terrain
        sb.AppendLine("  ❄ Area is covered in ice (difficult terrain for 1 round)");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

}
