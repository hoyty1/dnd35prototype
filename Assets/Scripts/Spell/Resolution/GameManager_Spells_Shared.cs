// ============================================================================
// GameManager_Spells_Shared.cs — Shared spell resolution helpers, tick methods, multi-spell resolvers.
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
    //  ROUND TICK / CLEANUP — 3rd-level Cleric spell durations
    // ================================================================

    /// <summary>Delegates to <see cref="EffectService.TickClericSpell3Durations"/>.</summary>
    public void TickClericSpell3Durations(CharacterController character)
        => EffectService.TickClericSpell3Durations(character, msg => CombatUI?.ShowCombatLog(msg));

    // ================================================================
    //  ALIGNMENT BURST SPELLS — shared helper
    // ================================================================
    // Chaos Hammer, Holy Smite, Order's Wrath, Unholy Blight all share
    // the same basic structure:
    //   • 20-ft burst (4 squares), Medium range
    //   • 1d8 per 2 CL (max 5d8) vs creatures of the opposing alignment
    //   • Half damage to neutral creatures on the relevant axis
    //   • Will save: halves damage, negates secondary condition
    //   • Secondary condition varies per spell

    private bool TryResolveChaosHammerSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.CHAOS_HAMMER) return false;
        return ResolveAlignmentBurstSpell(caster, target, spell, result,
            "Chaos Hammer", "chaotic", "⚡🌀",
            a => AlignmentHelper.IsLawful(a),
            a => AlignmentHelper.IsNeutralLC(a),
            (ch, rounds) => { ch.ApplyCondition(CombatConditionType.Staggered, rounds, "Slow"); }, // Slowed = Staggered for simplicity
            () => DiceRoller.D6() // 1d6 rounds
        );
    }

    private bool TryResolveHolySmiteSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.HOLY_SMITE) return false;
        return ResolveAlignmentBurstSpell(caster, target, spell, result,
            "Holy Smite", "good", "⚡✨",
            a => AlignmentHelper.IsEvil(a),
            a => AlignmentHelper.IsNeutralGE(a),
            (ch, rounds) => { ch.ApplyCondition(CombatConditionType.Blinded, rounds, "Blindness"); },
            () => 1 // 1 round blindness
        );
    }

    private bool TryResolveOrdersWrathSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.ORDERS_WRATH) return false;
        return ResolveAlignmentBurstSpell(caster, target, spell, result,
            "Order's Wrath", "lawful", "⚡⚖",
            a => AlignmentHelper.IsChaotic(a),
            a => AlignmentHelper.IsNeutralLC(a),
            (ch, rounds) => { ch.ApplyCondition(CombatConditionType.Dazed, rounds, "Daze"); },
            () => 1 // 1 round daze
        );
    }

    private bool TryResolveUnholyBlightSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.UNHOLY_BLIGHT) return false;
        return ResolveAlignmentBurstSpell(caster, target, spell, result,
            "Unholy Blight", "evil", "⚡💀",
            a => AlignmentHelper.IsGood(a),
            a => AlignmentHelper.IsNeutralGE(a),
            (ch, rounds) => { ch.ApplyCondition(CombatConditionType.Sickened, rounds, "Sicken"); },
            () => DiceRoller.D4() // 1d4 rounds
        );
    }

    /// <summary>
    /// Shared resolution for the four alignment burst spells.
    /// These are AoE spells centered on the target (clicked cell).
    /// Since the system dispatches per-target, we resolve damage for the
    /// single target that was passed in.
    /// </summary>
    private bool ResolveAlignmentBurstSpell(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result,
        string spellName, string alignmentDescriptor, string emoji,
        System.Func<Alignment, bool> isFullDamageAlignment,
        System.Func<Alignment, bool> isHalfDamageAlignment,
        System.Action<CharacterController, int> applyCondition,
        System.Func<int> rollConditionDuration)
    {
        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        Alignment targetAlignment = target.Stats.CharacterAlignment;
        bool fullDamage = isFullDamageAlignment(targetAlignment);
        bool halfDamage = isHalfDamageAlignment(targetAlignment);

        if (!fullDamage && !halfDamage)
        {
            // Target's alignment is same as or not affected by this spell
            CombatUI?.ShowCombatLog(CombatLogHelper.NoEffect(emoji, spellName, targetName, $"alignment: {targetAlignment}"));
            return true;
        }

        // Roll damage: 1d8 per 2 CL, max 5d8
        int numDice = Mathf.Clamp(casterLevel / 2, 1, 5);
        int damage = 0;
        for (int i = 0; i < numDice; i++)
            damage += DiceRoller.D8(); // 1d8

        // Half damage for neutral creatures on the relevant axis
        if (halfDamage)
            damage = Mathf.Max(1, damage / 2);

        // Will save: DC = 10 + spell level (4) + WIS mod
        int saveDC = CombatCalculationService.SpellSaveDC(4, caster.Stats.WISMod);
        var saveResult = SpellSaveResolver.RollSave(target, SaveType.Will, saveDC);
        bool saveSuccess = saveResult.Saved;

        if (saveSuccess)
            damage = Mathf.Max(1, damage / 2);

        // Apply damage
        target.Stats.TakeDamage(damage);
        result.DamageDealt = damage;

        // Apply condition only if save failed and full damage alignment
        int conditionDuration = 0;
        string conditionMsg = "";
        if (!saveSuccess && fullDamage)
        {
            conditionDuration = rollConditionDuration();
            applyCondition(target, conditionDuration);
            conditionMsg = $" + condition {conditionDuration} rd";
        }

        string saveStr = saveSuccess ? $"<color=#88FF88>saved (DC {saveDC})</color>" : $"<color=#FF8888>failed save (DC {saveDC})</color>";
        string alignStr = halfDamage ? "half (neutral)" : "full";
        CombatUI?.ShowCombatLog(CombatLogHelper.Special(emoji, $"{spellName}! {casterName} blasts {targetName} for {numDice}d8 = {damage} {alignmentDescriptor} damage ({alignStr}), {saveStr}{conditionMsg}!"));
        Debug.Log($"[{spellName}] {casterName} -> {targetName}: {damage} damage, save {saveRoll} vs DC {saveDC}, alignment={targetAlignment}");

        return true;
    }

    /// <summary>Delegates to <see cref="EffectService.TickClericSpell4Durations"/>.</summary>
    public void TickClericSpell4Durations(CharacterController character)
        => EffectService.TickClericSpell4Durations(character, msg => CombatUI?.ShowCombatLog(msg));

    // ================================================================
    //  LIGHTNING BOLT & FIREBALL — Scalable AoE Damage Resolution
    // ================================================================

    /// <summary>
    /// Resolves Lightning Bolt or Fireball as AoE damage spells with
    /// 1d6/CL damage (max 10d6), Reflex half, and SR.
    /// Called from PerformAoESpellCast when the pending spell matches.
    /// </summary>
    private bool TryResolveScaledAoEDamageSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        bool isFireball = string.Equals(spell.SpellId, SpellNames.FIREBALL, System.StringComparison.Ordinal);
        bool isLightningBolt = string.Equals(spell.SpellId, SpellNames.LIGHTNING_BOLT, System.StringComparison.Ordinal);
        bool isCallLightning = string.Equals(spell.SpellId, SpellNames.CALL_LIGHTNING, System.StringComparison.Ordinal);

        if (!isFireball && !isLightningBolt && !isCallLightning)
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        // Call Lightning: fixed 3d6 (PHB p.207). Fireball/Lightning Bolt: 1d6/CL, max 10d6.
        int diceCount = isCallLightning ? 3 : Mathf.Clamp(casterLevel, 1, 10);
        int saveDc = GetSpellSaveDC(caster, spell);
        int castingAbilityMod = GetSpellSaveAbilityModifier(caster, spell);

        string damageType = isFireball ? "fire" : "electricity";
        string shapeStr = isFireball ? "20-ft radius burst" : isCallLightning ? "vertical bolt" : "120-ft line";
        string spellName = spell.Name;

        // Fire damage notifies for fire-based area effects (pyrotechnics, web ignition, etc.)
        if (isFireball && aoeCells != null)
        {
            foreach (Vector2Int cell in aoeCells)
                NotifyFireDamageAtPosition(cell, spellName);
        }

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts {spellName}! ({shapeStr})");
        sb.AppendLine($"  [{(spell.SpellLevel == 0 ? "Cantrip" : $"Level {spell.SpellLevel}")}] {spell.School}");
        sb.AppendLine($"  Damage: {diceCount}d6 {damageType} | Reflex DC {saveDc} for half");
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
                var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
                srResult.AppendToLog(sb);
                if (!srResult.Overcame)
                {
                    sb.AppendLine($"  {target.Stats.CharacterName} resists {spellName} via Spell Resistance!");
                    sb.AppendLine();
                    continue;
                }

                // Roll damage
                int damage = 0;
                for (int i = 0; i < diceCount; i++)
                    damage += DiceRoller.D6(); // 1d6

                // Reflex save + Blink
                var saveResult = SpellSaveResolver.RollSave(target, SaveType.Reflex, saveDc);
                bool savePassed = saveResult.Saved;
                if (savePassed)
                    damage = Mathf.Max(0, damage / 2);
                damage = SpellSaveResolver.ApplyBlinkHalving(damage, target, sb);
                damage = Mathf.Max(damage > 0 ? 1 : 0, damage);
                saveResult.AppendHalfDamageLog(sb);

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(damage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  Damage: {damage} {damageType}");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                // Check concentration for spell damage on the target
                CheckConcentrationOnDamage(target, damage);

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                }

                sb.AppendLine();
            }
        }

        // ── WALL OF ICE DAMAGE ──
        // AoE spells damage only the Wall of Ice cells that overlap the AoE area.
        // Each overlapping section takes the full AoE damage (per PHB object rules).
        if (aoeCells != null && AreaEffectManager.HasInstance)
        {
            // Collect per-wall overlap: which specific cells of each wall are hit
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

                // Roll separate damage for the wall (same dice as character damage)
                int wallDamage = 0;
                for (int i = 0; i < diceCount; i++)
                    wallDamage += DiceRoller.D6();

                // No save for objects; fire is especially effective
                sb.AppendLine($"  --- Wall of Ice ({overlapCells.Count} section(s) hit) ---");
                sb.AppendLine($"  {damageType} damage to overlapping sections: {wallDamage}");

                bool destroyed = wall.DealDamageToOverlappingCells(wallDamage, overlapCells, isFireball);

                if (destroyed)
                    sb.AppendLine($"  💥 The Wall of Ice is destroyed by the {spellName}!");
                else
                    sb.AppendLine($"  Wall HP: {wall.WallHP}/{wall.WallMaxHP}");

                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    /// <summary>Delegates to <see cref="EffectService.TickClericSpell2Durations"/>.</summary>
    public void TickClericSpell2Durations(CharacterController character)
        => EffectService.TickClericSpell2Durations(character, msg => CombatUI?.ShowCombatLog(msg));

}
