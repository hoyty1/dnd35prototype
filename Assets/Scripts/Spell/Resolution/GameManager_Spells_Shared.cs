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
    //  METAMAGIC OF A LASTING EFFECT (batch 11 step 19, SPL-038)
    // ================================================================

    /// <summary>
    /// The metamagic of <paramref name="holder"/>'s active <paramref name="spellId"/> effect: the one stamped on the cast's
    /// spell clone the effect keeps (ActiveSpellEffect.Spell.MetamagicDataRef), so the later dice of a lasting effect
    /// (Spiritual Weapon's attacks, Heat Metal's rounds) use the metamagic it was cast with. Null when there is none.
    /// </summary>
    internal static MetamagicData ActiveSpellMetamagic(CharacterController holder, string spellId)
    {
        StatusEffectManager effects = holder != null ? holder.StatusEffectManager : null;
        if (effects == null || effects.ActiveEffects == null || string.IsNullOrEmpty(spellId))
            return null;
        for (int i = 0; i < effects.ActiveEffects.Count; i++)
        {
            ActiveSpellEffect effect = effects.ActiveEffects[i];
            if (effect != null && effect.Spell != null && effect.Spell.SpellId == spellId)
                return effect.Spell.MetamagicDataRef;
        }
        return null;
    }

    // ================================================================
    //  ALIGNMENT BURST SPELLS — area handler (batch 11 step 23, SPL-046)
    // ================================================================
    // Chaos Hammer (PHB p.208), Holy Smite (p.241), Order's Wrath (p.258) and Unholy Blight (p.297):
    //   • an area at a point in Medium range (a 20-ft.-radius burst or spread; Order's Wrath a burst that fills a 30-ft.
    //     cube), every creature in it, the alignment deciding the effect
    //   • 1d8 per two caster levels (max 5d8) to a creature of the opposed alignment, 1d6 per caster level (max 10d6) to an
    //     outsider of that alignment; half to a creature neutral on the axis; the caster's own side of the axis unharmed
    //   • a successful Will save halves the damage (a neutral creature's half again, down to one quarter of the roll) and
    //     negates the secondary condition, which only a creature of the opposed alignment suffers
    //   • spell resistance per creature; the damage rolled once for the area (one roll for the creatures, one for the
    //     outsiders: AreaDamageRoll, decision D6, batch 11 step 20)

    /// <summary>What one of the four alignment bursts does: its harmed alignments and its secondary condition.</summary>
    private sealed class AlignmentBurstSpec
    {
        public string Descriptor;
        public string Emoji;
        public Func<Alignment, bool> IsFullDamage;
        public Func<Alignment, bool> IsHalfDamage;
        public CombatConditionType Condition;
        public string ConditionName;
        /// <summary>The condition's rounds for one creature (a rolled duration is a variable effect Empower and Maximize change, PHB p.93).</summary>
        public Func<SpellData, int> ConditionRounds;
    }

    /// <summary>The alignment burst <paramref name="spellId"/> names, or null for any other spell.</summary>
    private static AlignmentBurstSpec AlignmentBurstSpecFor(string spellId)
    {
        switch (spellId)
        {
            case SpellNames.CHAOS_HAMMER:
                // Slowed (PHB p.208) is applied as Staggered, a labelled simplification (RULES_COVERAGE 4.1).
                return new AlignmentBurstSpec
                {
                    Descriptor = "chaotic", Emoji = "⚡🌀",
                    IsFullDamage = AlignmentHelper.IsLawful, IsHalfDamage = AlignmentHelper.IsNeutralLC,
                    Condition = CombatConditionType.Staggered, ConditionName = "slowed (staggered)",
                    ConditionRounds = s => SpellDiceRules.RollFor(s, SpellDice.Of(1, 6)) // 1d6 rounds
                };
            case SpellNames.HOLY_SMITE:
                return new AlignmentBurstSpec
                {
                    Descriptor = "good", Emoji = "⚡✨",
                    IsFullDamage = AlignmentHelper.IsEvil, IsHalfDamage = AlignmentHelper.IsNeutralGE,
                    Condition = CombatConditionType.Blinded, ConditionName = "blinded",
                    ConditionRounds = s => 1
                };
            case SpellNames.ORDERS_WRATH:
                return new AlignmentBurstSpec
                {
                    Descriptor = "lawful", Emoji = "⚡⚖",
                    IsFullDamage = AlignmentHelper.IsChaotic, IsHalfDamage = AlignmentHelper.IsNeutralLC,
                    Condition = CombatConditionType.Dazed, ConditionName = "dazed",
                    ConditionRounds = s => 1
                };
            case SpellNames.UNHOLY_BLIGHT:
                return new AlignmentBurstSpec
                {
                    Descriptor = "evil", Emoji = "⚡💀",
                    IsFullDamage = AlignmentHelper.IsGood, IsHalfDamage = AlignmentHelper.IsNeutralGE,
                    Condition = CombatConditionType.Sickened, ConditionName = "sickened",
                    ConditionRounds = s => SpellDiceRules.RollFor(s, SpellDice.Of(1, 4)) // 1d4 rounds
                };
            default:
                return null;
        }
    }

    /// <summary>
    /// The four alignment bursts over their area (the area handler registry, GameManager.SpellHandlerRegistry.cs): each
    /// creature in the aim (after line of effect and the cast core's Globe stage) is judged by its alignment, then rolls
    /// its own spell resistance and Will save against the area's one damage roll (outsiders of the opposed alignment
    /// against the area's one 1d6-per-level roll). Damage goes through DealDamage (mitigation, concentration, death); the
    /// core runs the combat-end check. Halves round down (PHB p.177, p.304; SPL-182). The same for every caster.
    /// </summary>
    private bool TryResolveAlignmentBurstAoE(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        AlignmentBurstSpec spec = AlignmentBurstSpecFor(spell.SpellId);
        if (spec == null)
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int saveDc = GetSpellSaveDC(caster, spell);
        SpellDice dice = CustomSpellDice.AlignmentBurst(casterLevel);
        SpellDice outsiderDice = CustomSpellDice.AlignmentBurstOutsider(casterLevel);
        string shapeStr = spell.AoEShapeType == AoEShape.Cube ? $"{spell.AoESizeSquares * 5}-ft. cube" : $"{spell.AoESizeSquares * 5}-ft.-radius burst";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"{spec.Emoji} {caster.Stats.CharacterName} casts {spell.Name}! ({shapeStr})");
        sb.AppendLine($"  Damage: {dice} ({outsiderDice} to outsiders) to the opposed alignment, half to neutral | Will DC {saveDc} partial");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        var areaRoll = new AreaDamageRoll();
        var outsiderRoll = new AreaDamageRoll();
        int targetIndex = 0;
        if (targets != null)
        {
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead)
                    continue;

                targetIndex++;
                string targetName = target.Stats.CharacterName;
                sb.AppendLine($"  --- Target {targetIndex}: {targetName} ---");

                Alignment alignment = target.Stats.CharacterAlignment;
                bool full = spec.IsFullDamage(alignment);
                bool half = !full && spec.IsHalfDamage(alignment);
                if (!full && !half)
                {
                    sb.AppendLine($"  Unharmed: {spell.Name} harms only the opposed alignment and the neutral (alignment {alignment}).");
                    sb.AppendLine();
                    continue;
                }

                SRResult sr = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
                sr.AppendToLog(sb);
                if (!sr.Overcame)
                {
                    sb.AppendLine($"  {targetName} resists {spell.Name} via Spell Resistance!");
                    sb.AppendLine();
                    continue;
                }

                bool outsider = full && SpellTargetingService.IsOutsider(target);
                int damage = outsider
                    ? outsiderRoll.RollAndLog(spell, outsiderDice, sb, "outsiders")
                    : areaRoll.RollAndLog(spell, dice, sb);
                if (half)
                    damage /= 2; // only half damage to a creature neutral on the axis

                SaveResult save = RollSpellSave(caster, target, spell, SaveType.Will, saveDc);
                save.AppendHalfDamageLog(sb);
                if (save.Saved)
                    damage /= 2; // half again: one quarter of the roll for a neutral creature
                // A blinking creature takes half damage from area attacks (PHB p.206), as in the other area handlers.
                damage = SpellSaveResolver.ApplyBlinkHalving(damage, target, sb);

                int hpBefore = target.Stats.CurrentHP;
                DamageResolutionResult dealt = DealDamage(target, damage, DamagePackets.Spell(spell.Name, DamageType.Untyped, save.Saved));
                sb.AppendLine($"  Damage: {dealt.FinalDamage} {spec.Descriptor} ({(outsider ? "outsider, " : "")}{(half ? "half, neutral" : "full")}){DescribeMitigation(dealt)}");
                sb.AppendLine($"  {targetName}: {hpBefore} → {target.Stats.CurrentHP} HP");

                if (target.Stats.IsDead)
                {
                    sb.AppendLine($"  💀 {targetName} has been slain!");
                }
                else if (full && !save.Saved)
                {
                    int rounds = spec.ConditionRounds(spell);
                    if (_conditionService != null)
                        _conditionService.ApplyCondition(target, spec.Condition, rounds, source: caster, sourceNameOverride: spell.Name,
                            sourceCategory: "Spell", sourceId: spell.SpellId);
                    else
                        target.ApplyCondition(spec.Condition, rounds, spell.Name);
                    sb.AppendLine($"  {targetName} is {spec.ConditionName} for {rounds} round(s).");
                }

                Debug.Log($"[{spell.Name}] {caster.Stats.CharacterName} -> {targetName}: {dealt.FinalDamage} damage, save {save.Total} vs DC {saveDc}, alignment={alignment}");
                sb.AppendLine();
            }
        }

        if (targetIndex == 0)
            sb.AppendLine("  No valid targets in area!");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
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
    /// 1d6/CL damage (max 10d6), Reflex half, and SR; Call Lightning's bolt (3d6) the same way.
    /// The damage is rolled once for the whole effect and every creature and Wall of Ice section in the area takes that
    /// one total (AreaDamageRoll, decision D6; delegated RAW reading 2026-10-14, batch 11 step 20, SPL-180); each creature
    /// then rolls its own spell resistance and Reflex save (half), and Evasion or Improved Evasion applies (PHB p.50, p.42).
    /// Registered in the area handler registry (GameManager.SpellHandlerRegistry.cs, BuildAreaHandlers).
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
        SpellDice scaledDice = isCallLightning ? CustomSpellDice.CallLightningBolt() : CustomSpellDice.FireballOrLightningBolt(casterLevel);
        int diceCount = scaledDice.Count;
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

        // One damage roll for the whole effect, with the cast's metamagic (SpellDiceRules; batch 11 step 19, SPL-038),
        // made when it first reaches a creature its SR does not stop or a Wall of Ice section (D6, batch 11 step 20, SPL-180)
        var areaRoll = new AreaDamageRoll();

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

                // The effect's one damage total (rolled, and logged once, at the first creature it reaches)
                int damage = areaRoll.RollAndLog(spell, scaledDice, sb);

                // Reflex save (half, rounded down) + Blink + Evasion and Improved Evasion (PHB p.177, p.50, p.42), logged
                // in that order
                var saveResult = RollSpellSave(caster, target, spell, SaveType.Reflex, saveDc);
                bool savePassed = saveResult.Saved;
                saveResult.AppendHalfDamageLog(sb);
                if (savePassed)
                    damage = Mathf.Max(0, damage / 2);
                damage = SpellSaveResolver.ApplyBlinkHalving(damage, target, sb);
                damage = SpellSaveResolver.ApplyEvasion(damage, target, savePassed, sb);

                int hpBefore = target.Stats.CurrentHP;
                DamageResolutionResult dealt = DealDamage(target, damage,
                    DamagePackets.Spell(spellName, isFireball ? DamageType.Fire : DamageType.Electricity, savePassed, !isCallLightning));
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  Damage: {dealt.FinalDamage} {damageType}{DescribeMitigation(dealt)}");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                if (target.Stats.IsDead)
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");

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

                // An unattended object takes the effect's same damage total (PHB p.231: objects "also take this damage";
                // D6, batch 11 step 20, SPL-180)
                int wallDamage = areaRoll.RollAndLog(spell, scaledDice, sb);

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
