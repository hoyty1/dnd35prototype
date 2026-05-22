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

    /// <summary>
    /// Called at the start of each combat round to tick 3rd-level cleric spell durations.
    /// Should be called from the main round-tick loop alongside TickClericSpell2Durations.
    /// </summary>
    public void TickClericSpell3Durations(CharacterController character)
    {
        if (character?.Stats == null) return;

        // ── Prayer tick ──
        if (character.Stats.PrayerActive)
        {
            if (character.Stats.PrayerRoundsRemaining > 0)
            {
                character.Stats.PrayerRoundsRemaining--;
                if (character.Stats.PrayerRoundsRemaining <= 0)
                {
                    // Bonuses are reversed by StatusEffectManager when the
                    // ActiveSpellEffect expires, but we clean up the flag here.
                    character.Stats.PrayerActive = false;

                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🙏 {character.Stats.CharacterName}'s Prayer effect fades.</color>");
                    Debug.Log($"[Prayer] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Invisibility Purge tick ──
        if (character.Stats.InvisibilityPurgeActive)
        {
            if (character.Stats.InvisibilityPurgeRoundsRemaining > 0)
            {
                character.Stats.InvisibilityPurgeRoundsRemaining--;
                if (character.Stats.InvisibilityPurgeRoundsRemaining <= 0)
                {
                    character.Stats.InvisibilityPurgeActive = false;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>👁 {character.Stats.CharacterName}'s Invisibility Purge fades.</color>");
                    Debug.Log($"[InvisibilityPurge] Expired on {character.Stats.CharacterName}");
                }
            }
        }
    }

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
            (ch, rounds) => { ch.AddCondition(CombatConditionType.Staggered); }, // Slowed = Staggered for simplicity
            () => Random.Range(1, 7) // 1d6 rounds
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
            (ch, rounds) => { ch.AddCondition(CombatConditionType.Blinded); },
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
            (ch, rounds) => { ch.AddCondition(CombatConditionType.Dazed); },
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
            (ch, rounds) => { ch.AddCondition(CombatConditionType.Sickened); },
            () => Random.Range(1, 5) // 1d4 rounds
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
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));

        Alignment targetAlignment = target.Stats.CharacterAlignment;
        bool fullDamage = isFullDamageAlignment(targetAlignment);
        bool halfDamage = isHalfDamageAlignment(targetAlignment);

        if (!fullDamage && !halfDamage)
        {
            // Target's alignment is same as or not affected by this spell
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>  {emoji} {spellName}: {targetName} is unaffected (alignment: {targetAlignment}).</color>");
            return true;
        }

        // Roll damage: 1d8 per 2 CL, max 5d8
        int numDice = Mathf.Clamp(casterLevel / 2, 1, 5);
        int damage = 0;
        for (int i = 0; i < numDice; i++)
            damage += Random.Range(1, 9); // 1d8

        // Half damage for neutral creatures on the relevant axis
        if (halfDamage)
            damage = Mathf.Max(1, damage / 2);

        // Will save: DC = 10 + spell level (4) + WIS mod
        int saveDC = 10 + 4 + caster.Stats.WISMod;
        int saveRoll = Random.Range(1, 21) + target.Stats.WillSave;
        bool saveSuccess = saveRoll >= saveDC;

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
        CombatUI?.ShowCombatLog($"<color=#FFD700>{emoji} {spellName}! {casterName} blasts {targetName} for {numDice}d8 = {damage} {alignmentDescriptor} damage ({alignStr}), {saveStr}{conditionMsg}!</color>");
        Debug.Log($"[{spellName}] {casterName} -> {targetName}: {damage} damage, save {saveRoll} vs DC {saveDC}, alignment={targetAlignment}");

        return true;
    }

    // ================================================================
    //  ROUND TICK / CLEANUP — 4th-level Cleric spell durations
    // ================================================================

    /// <summary>
    /// Called at the start of each combat round to tick 4th-level cleric spell durations.
    /// Should be called from TickCharacterSpellDurations alongside TickClericSpell3Durations.
    /// </summary>
    public void TickClericSpell4Durations(CharacterController character)
    {
        if (character?.Stats == null) return;

        // ── Death Ward ──
        if (character.Stats.DeathWardActive)
        {
            if (character.Stats.DeathWardRoundsRemaining > 0)
            {
                character.Stats.DeathWardRoundsRemaining--;
                if (character.Stats.DeathWardRoundsRemaining <= 0)
                {
                    character.Stats.DeathWardActive = false;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🛡 {character.Stats.CharacterName}'s Death Ward fades.</color>");
                    Debug.Log($"[DeathWard] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Divine Power ──
        if (character.Stats.DivinePowerActive)
        {
            if (character.Stats.DivinePowerRoundsRemaining > 0)
            {
                character.Stats.DivinePowerRoundsRemaining--;
                if (character.Stats.DivinePowerRoundsRemaining <= 0)
                {
                    // Reverse bonuses
                    character.Stats.STR -= character.Stats.DivinePowerStrBonus;
                    character.Stats.TempHP = Mathf.Max(0, character.Stats.TempHP - character.Stats.DivinePowerTempHP);
                    character.Stats.BaseAttackBonus -= character.Stats.DivinePowerBABBonus;

                    character.Stats.DivinePowerActive = false;
                    character.Stats.DivinePowerStrBonus = 0;
                    character.Stats.DivinePowerTempHP = 0;
                    character.Stats.DivinePowerBABBonus = 0;

                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>⚔ {character.Stats.CharacterName}'s Divine Power fades.</color>");
                    Debug.Log($"[DivinePower] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Freedom of Movement ──
        if (character.Stats.FreedomOfMovementActive)
        {
            if (character.Stats.FreedomOfMovementRoundsRemaining > 0)
            {
                character.Stats.FreedomOfMovementRoundsRemaining--;
                if (character.Stats.FreedomOfMovementRoundsRemaining <= 0)
                {
                    character.Stats.FreedomOfMovementActive = false;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🦅 {character.Stats.CharacterName}'s Freedom of Movement fades.</color>");
                    Debug.Log($"[FreedomOfMovement] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Spell Immunity ──
        if (character.Stats.SpellImmunityActive)
        {
            if (character.Stats.SpellImmunityRoundsRemaining > 0)
            {
                character.Stats.SpellImmunityRoundsRemaining--;
                if (character.Stats.SpellImmunityRoundsRemaining <= 0)
                {
                    character.Stats.SpellImmunityActive = false;
                    character.Stats.SpellImmunitySpellId = null;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🛡🔮 {character.Stats.CharacterName}'s Spell Immunity fades.</color>");
                    Debug.Log($"[SpellImmunity] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Repel Vermin ──
        if (character.Stats.RepelVerminActive)
        {
            if (character.Stats.RepelVerminRoundsRemaining > 0)
            {
                character.Stats.RepelVerminRoundsRemaining--;
                if (character.Stats.RepelVerminRoundsRemaining <= 0)
                {
                    character.Stats.RepelVerminActive = false;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🐛 {character.Stats.CharacterName}'s Repel Vermin fades.</color>");
                    Debug.Log($"[RepelVermin] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Neutralize Poison Immunity ──
        if (character.Stats.NeutralizePoisonImmunityActive)
        {
            if (character.Stats.NeutralizePoisonImmunityRoundsRemaining > 0)
            {
                character.Stats.NeutralizePoisonImmunityRoundsRemaining--;
                if (character.Stats.NeutralizePoisonImmunityRoundsRemaining <= 0)
                {
                    character.Stats.NeutralizePoisonImmunityActive = false;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🌿 {character.Stats.CharacterName}'s poison immunity fades.</color>");
                    Debug.Log($"[NeutralizePoison] Immunity expired on {character.Stats.CharacterName}");
                }
            }
        }
    }

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

        if (!isFireball && !isLightningBolt)
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int diceCount = Mathf.Clamp(casterLevel, 1, 10); // 1d6/CL, max 10d6
        int saveDc = GetSpellSaveDC(caster, spell);
        int castingAbilityMod = GetSpellSaveAbilityModifier(caster, spell);

        string damageType = isFireball ? "fire" : "electricity";
        string shapeStr = isFireball ? "20-ft radius burst" : "120-ft line";
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
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srCheckRoll = UnityEngine.Random.Range(1, 21);
                    int srCheckTotal = srCheckRoll + casterLevel;
                    bool srOvercome = srCheckTotal >= target.Stats.SpellResistance;

                    sb.AppendLine($"  SR Check: d20({srCheckRoll}) + {casterLevel} = {srCheckTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");

                    if (!srOvercome)
                    {
                        sb.AppendLine($"  {target.Stats.CharacterName} resists {spellName} via Spell Resistance!");
                        sb.AppendLine();
                        continue;
                    }
                }

                // Roll damage
                int damage = 0;
                for (int i = 0; i < diceCount; i++)
                    damage += UnityEngine.Random.Range(1, 7); // 1d6

                // Reflex save
                int reflexRoll = UnityEngine.Random.Range(1, 21);
                int reflexMod = target.Stats.ReflexSave;
                int reflexTotal = reflexRoll + reflexMod;
                bool savePassed = reflexTotal >= saveDc;

                if (savePassed)
                    damage = Mathf.Max(0, damage / 2);

                // D&D 3.5e PHB p.206: Blinking creatures take half damage from area attacks
                // (they are partially on the Ethereal Plane). This stacks with Reflex halving.
                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    damage = Mathf.Max(0, damage / 2);

                damage = Mathf.Max(damage > 0 ? 1 : 0, damage);

                sb.AppendLine($"  Reflex save: d20({reflexRoll}) + {reflexMod} = {reflexTotal} vs DC {saveDc} → {(savePassed ? "SAVED (half)" : "FAILED (full)")}");
                if (targetIsBlinking)
                    sb.AppendLine($"  Blink: area damage halved (target partially ethereal)");

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
                    wallDamage += UnityEngine.Random.Range(1, 7);

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

    // ================================================================
    //  ROUND TICK / CLEANUP — 2nd-level Cleric spell durations
    // ================================================================

    /// <summary>
    /// Called at the start of each combat round to tick 2nd-level cleric spell durations.
    /// </summary>
    public void TickClericSpell2Durations(CharacterController character)
    {
        if (character?.Stats == null) return;

        // ── Death Knell tick ──
        if (character.Stats.DeathKnellActive)
        {
            if (character.Stats.DeathKnellRoundsRemaining > 0)
            {
                character.Stats.DeathKnellRoundsRemaining--;
                if (character.Stats.DeathKnellRoundsRemaining <= 0)
                {
                    // Remove Death Knell buffs
                    character.Stats.STR -= character.Stats.DeathKnellStrBonus;
                    character.Stats.DeathKnellActive = false;
                    character.Stats.DeathKnellStrBonus = 0;
                    character.Stats.DeathKnellCLBonus = 0;

                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>☠ {character.Stats.CharacterName}'s Death Knell buff fades.</color>");
                    Debug.Log($"[DeathKnell] Buff expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Silence tick ──
        if (character.Stats.SilenceActive)
        {
            if (character.Stats.SilenceRoundsRemaining > 0)
            {
                character.Stats.SilenceRoundsRemaining--;
                if (character.Stats.SilenceRoundsRemaining <= 0)
                {
                    character.Stats.SilenceActive = false;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🔇 {character.Stats.CharacterName}'s Silence ends.</color>");
                    Debug.Log($"[Silence] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Spiritual Weapon tick — handled by ProcessSpiritualWeaponTurnStart ──

        // ── Align Weapon tick ──
        if (character.Stats.AlignWeaponActive)
        {
            if (character.Stats.AlignWeaponRoundsRemaining > 0)
            {
                character.Stats.AlignWeaponRoundsRemaining--;
                if (character.Stats.AlignWeaponRoundsRemaining <= 0)
                {
                    character.Stats.AlignWeaponActive = false;
                    character.Stats.AlignWeaponAlignment = null;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>⚔ {character.Stats.CharacterName}'s Align Weapon fades.</color>");
                    Debug.Log($"[AlignWeapon] Expired on {character.Stats.CharacterName}");
                }
            }
        }
    }

}
