// ============================================================================
//  GameManager_Spells_S.cs  —  Spell resolution: 'S' spells
//  (partial class GameManager)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;
using Random = UnityEngine.Random;

public partial class GameManager
{

    // ================================================================
    //  SEARING LIGHT  (PHB p.275)
    // ================================================================
    // Ranged touch attack dealing divine/light damage that varies by creature type:
    //   Undead:     1d8 per 2 caster levels (max 5d8)
    //   Constructs: 1d6 per 2 caster levels (max 5d6)
    //   Others:     1d8 per 2 caster levels (max 5d8), but only half total damage
    // No saving throw. Spell Resistance applies.

    private bool TryResolveSearingLightSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SEARING_LIGHT)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true; // Spell was cast but missed ranged touch

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        // Determine creature type via SpellTargetingService
        bool isUndead = SpellTargetingService.IsUndead(target);
        bool isConstruct = SpellTargetingService.IsConstruct(target);

        // Calculate number of dice: 1 die per 2 caster levels, max 5 dice
        int numDice = Mathf.Clamp(casterLevel / 2, 1, 5);

        int damage = 0;
        string damageDesc;

        if (isUndead)
        {
            // Undead: 1d8 per 2 CL, max 5d8
            for (int i = 0; i < numDice; i++)
                damage += DiceRoller.D8(); // 1d8
            damageDesc = $"{numDice}d8 = {damage} (undead — full divine damage)";
        }
        else if (isConstruct)
        {
            // Constructs: 1d6 per 2 CL, max 5d6
            for (int i = 0; i < numDice; i++)
                damage += DiceRoller.D6(); // 1d6
            damageDesc = $"{numDice}d6 = {damage} (construct)";
        }
        else
        {
            // Others: 1d8 per 2 CL, max 5d8, then half damage
            int fullDamage = 0;
            for (int i = 0; i < numDice; i++)
                fullDamage += DiceRoller.D8(); // 1d8
            damage = Mathf.Max(1, fullDamage / 2);
            damageDesc = $"{numDice}d8 = {fullDamage}, halved to {damage} (living creature)";
        }

        // Apply damage
        target.Stats.TakeDamage(damage);
        result.DamageDealt = damage;

        // Log
        string typeLabel = isUndead ? "☀💀" : isConstruct ? "☀🔧" : "☀";
        CombatUI?.ShowCombatLog(CombatLogHelper.Special(typeLabel, $"Searing Light! {casterName} blasts {targetName} for {damageDesc} divine damage!"));
        Debug.Log($"[SearingLight] {casterName} -> {targetName}: {damage} damage ({creatureType})");

        return true;
    }

    // ================================================================
    //  SPELL IMMUNITY  (PHB p.282)
    // ================================================================
    // Touch. 10 min/level. Subject is immune to one spell of 4th level or lower.
    // For now, we set a generic flag. The immune spell ID defaults to
    // the most common threat (e.g., "magic_missile").

    private bool TryResolveSpellImmunitySpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SPELL_IMMUNITY) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = casterLevel * 100; // 10 min/level = 100 rounds/level

        // Default immune spell — in full implementation, player would choose
        string immuneSpellId = SpellNames.MAGIC_MISSILE;
        string immuneSpellName = "Magic Missile";

        target.Stats.SpellImmunityActive = true;
        target.Stats.SpellImmunityRoundsRemaining = durationRounds;
        target.Stats.SpellImmunitySpellId = immuneSpellId;

        var statusMgr = target.StatusEffectManager;
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#AADDFF>🛡🔮 Spell Immunity! {casterName} grants {targetName} immunity to {immuneSpellName} for {durationRounds} rounds.</color>");
        Debug.Log($"[SpellImmunity] {casterName} -> {targetName}: immune to {immuneSpellId}, duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Spell Immunity: {immuneSpellName} ({durationRounds} rounds)";
        return true;
    }

    // ================================================================
    //  SLOW — Transmutation Speed Debuff (PHB p.280)
    // ================================================================

    /// <summary>
    /// Applies the Slow spell effect to a target.
    /// Per PHB p.280:
    ///   • -1 penalty on attack rolls, AC, and Reflex saves
    ///   • Movement speed halved (round down to nearest 5 ft)
    ///   • Can only take single move or standard action (no full-round actions)
    ///   • Slow counters and dispels Haste
    /// Duration: 1 round/level. Will negates. SR: Yes.
    /// </summary>
    private ActiveSpellEffect ApplySlowDebuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        if (target == null || target.Stats == null || spell == null)
            return null;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";

        CombatUI?.ShowCombatLog($"<color=#CC88FF>🐌 {casterName} casts Slow on {target.Stats.CharacterName}!</color>");

        // Spell Resistance check
        var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
        if (!srResult.Skipped)
        {
            string srLog = $"  SR Check: d20({srResult.Roll}) + {srResult.CasterLevel}{(srResult.PenetrationBonus > 0 ? $"+{srResult.PenetrationBonus}" : "")} = {srResult.Total} vs SR {srResult.TargetSR} → {(srResult.Overcame ? "OVERCAME SR" : "BLOCKED by SR")}";
            CombatUI?.ShowCombatLog(srLog);
        }
        if (!srResult.Overcame)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellResistance("", $" {target.Stats.CharacterName} resists Slow via Spell Resistance!"));
            return null;
        }

        // Will save
        if (spell.AllowsSavingThrow)
        {
            var saveResult = SpellSaveResolver.RollSave(target, SaveType.Will, saveDc);
            CombatUI?.ShowCombatLog($"  Will Save: d20({saveResult.Roll}) + {saveResult.Modifier} = {saveResult.Total} vs DC {saveDc} → {(saveResult.Saved ? "SAVED" : "FAILED")}");

            if (saveResult.Saved)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.SpellResisted(target.Stats.CharacterName, "Slow"));
                return null;
            }
        }

        // If target has Haste, Slow dispels it
        StatusEffectManager targetStatusMgr = target.StatusEffectManager;
        if (targetStatusMgr == null)
            targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        targetStatusMgr.Init(target.Stats);

        if (target.HasActiveHasteEffect)
        {
            target.ClearHasteEffect();
            target.Stats.HasteAttackBonus = 0;
            target.Stats.HasteACBonus = 0;
            target.Stats.HasteReflexBonus = 0;

            targetStatusMgr.RemoveEffectsBySpellId(SpellNames.HASTE);

            CombatUI?.ShowCombatLog($"<color=#CC88FF>  🐌 Slow dispels Haste on {target.Stats.CharacterName}!</color>");
        }

        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);

        ActiveSpellEffect effect = targetStatusMgr.AddEffect(
            spell,
            casterName,
            casterLevel);

        if (effect != null)
        {
            // Apply Slow penalties to stats
            target.Stats.SlowAttackPenalty = -1;
            target.Stats.SlowACPenalty = -1;
            target.Stats.SlowReflexPenalty = -1;
            target.Stats.SlowSpeedMultiplier = 0.5f;

            // Apply custom effect data
            target.ApplySlowEffect(durationRounds, caster);

            SpellcastingComponent targetSpellComp = target.Spellcasting;
            if (targetSpellComp != null)
                targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

            CombatUI?.ShowCombatLog($"<color=#CC88FF>  {target.Stats.CharacterName} is Slowed!</color>");
            CombatUI?.ShowCombatLog($"<color=#DDAAFF>   -1 attack, -1 AC, -1 Reflex, half speed, no full-round actions</color>");
            CombatUI?.ShowCombatLog($"<color=#DDAAFF>   Duration: {durationRounds} rounds (CL {casterLevel})</color>");
        }

        UpdateAllStatsUI();
        return effect;
    }

    // ================================================================
    //  SHOUT — Cone AoE Sonic Damage + Deafen (PHB p.275)
    // ================================================================

    private static bool IsShoutSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.SHOUT, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Shout: 5d6 sonic in 30-ft cone. Fort half.
    /// Failed save = deafened for 2d6 rounds. SR: Yes. PHB p.275
    /// </summary>
    private bool TryResolveShoutAoE(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!IsShoutSpell(spell))
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"📣 {caster.Stats.CharacterName} casts Shout! (30-ft cone)");
        sb.AppendLine($"  [Level {spell.SpellLevel}] {spell.School}");
        sb.AppendLine($"  Damage: 5d6 sonic | Fort DC {saveDc} for half");
        sb.AppendLine($"  Failed save: deafened for 2d6 rounds | SR: Yes");
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
                    sb.AppendLine($"  {target.Stats.CharacterName} resists Shout via Spell Resistance!");
                    sb.AppendLine();
                    continue;
                }

                // Roll 5d6 sonic damage
                int damage = 0;
                for (int i = 0; i < 5; i++)
                    damage += DiceRoller.D6();

                // Fortitude save
                var saveResult = SpellSaveResolver.RollSave(target, SaveType.Fortitude, saveDc);
                bool savePassed = saveResult.Saved;

                bool deafened = false;
                int deafRounds = 0;

                if (savePassed)
                {
                    damage = Mathf.Max(1, damage / 2);
                }
                else
                {
                    // Failed save: deafened for 2d6 rounds
                    deafRounds = DiceRoller.D6() + DiceRoller.D6();
                    deafened = true;
                }

                // D&D 3.5e: Blinking creatures take half damage from area attacks
                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    damage = Mathf.Max(1, damage / 2);

                sb.AppendLine($"  Fort save: d20({fortRoll}) + {fortMod} = {fortTotal} vs DC {saveDc} → {(savePassed ? "SAVED (half)" : "FAILED (full + deafened)")}");
                if (targetIsBlinking)
                    sb.AppendLine($"  Blink: area damage halved");

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(damage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  Damage: {damage} sonic");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                if (deafened && !target.Stats.IsDead)
                {
                    target.ApplyCondition(CombatConditionType.Deafened, deafRounds, "Shout");
                    sb.AppendLine($"  🔇 {target.Stats.CharacterName} is DEAFENED for {deafRounds} rounds!");
                }

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

        // ── WALL OF ICE DAMAGE FROM SHOUT ──
        // Sonic damage is especially effective against crystalline/brittle objects.
        // Only damage the specific wall sections that overlap the Shout AoE.
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

                // 5d6 sonic (no save for objects)
                int wallDamage = 0;
                for (int i = 0; i < 5; i++) wallDamage += DiceRoller.D6();

                sb.AppendLine($"  --- Wall of Ice ({overlapCells.Count} section(s) hit) ---");
                sb.AppendLine($"  Sonic damage to overlapping sections: {wallDamage}");

                bool destroyed = wall.DealDamageToOverlappingCells(wallDamage, overlapCells, false);

                if (destroyed)
                    sb.AppendLine($"  💥 The Wall of Ice is shattered by Shout!");
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
    //  SILENCE — PHB p.279
    //  Illusion (Glamer). Cleric 2 / Bard 2. V, S.
    //  Range: Long. Area: 20-ft-radius emanation centered on a creature/object/point.
    //  Duration: 1 round/level (D). Saving Throw: Will negates (creature-targeted) or None (area).
    //  Spell Resistance: Yes.
    //  Negates all sound in the area. Creatures in the area cannot cast
    //  spells with verbal components. Counters/dispels sound-based effects.
    // ================================================================

    private bool TryResolveSilenceSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SILENCE)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🔇", $"{target.Stats.CharacterName} resists {caster.Stats.CharacterName}'s Silence!"));
            return true;
        }

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = Mathf.Max(1, casterLevel); // 1 round/level

        target.Stats.SilenceActive = true;
        target.Stats.SilenceRoundsRemaining = durationRounds;

        // Track via StatusEffectManager
        var statusMgr = target.StatusEffectManager;
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);
            if (effect != null)
                effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#99CCFF>🔇 Silence! {target.Stats.CharacterName} is silenced for {durationRounds} round(s)! Cannot cast spells with verbal components.</color>");
        Debug.Log($"[Silence] Applied to {target.Stats.CharacterName} for {durationRounds} rounds (CL {casterLevel})");

        return true;
    }

    // ================================================================
    //  SOUND BURST — PHB p.281
    //  Evocation [Sonic]. Cleric 2. V, S, DF.
    //  Range: Close. Area: 10-ft-radius spread.
    //  Duration: Instantaneous.
    //  Saving Throw: Fortitude partial. Spell Resistance: Yes.
    //  1d8 sonic damage to all creatures in area. Creatures that fail
    //  Fort save are stunned for 1 round.
    //  NOTE: The damage is already handled by the existing AoE/damage system.
    //  This handler adds the stun effect on failed Fort save.
    // ================================================================

    private bool TryResolveSoundBurstStunEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SOUND_BURST)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        // Sound Burst damage is handled by normal spell damage resolution.
        // Here we check for the stun: Fort save or stunned for 1 round.
        int saveDC = CombatCalculationService.SpellSaveDC(spell.SpellLevel, GetSpellSaveAbilityModifier(caster, spell));
        var fortResult = SpellSaveResolver.RollSave(target, SaveType.Fortitude, saveDC);

        Debug.Log($"[SoundBurst] Fort save: {target.Stats.CharacterName} rolled {fortResult.Total} vs DC {saveDC}");

        if (!fortResult.Saved)
        {
            // Failed — stunned for 1 round
            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Stunned,
                    1,
                    source: caster,
                    sourceNameOverride: spell.Name,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                string sourceName = caster.Stats.CharacterName;
                target.ApplyCondition(CombatConditionType.Stunned, 1, sourceName);
            }

            CombatUI?.ShowCombatLog($"<color=#FF9933>💥🔔 {target.Stats.CharacterName} is stunned by Sound Burst! (Fort {fortSave} vs DC {saveDC})</color>");
            Debug.Log($"[SoundBurst] {target.Stats.CharacterName} STUNNED for 1 round (Fort {fortSave} < DC {saveDC})");
        }
        else
        {
            CombatUI?.ShowCombatLog($"<color=#CCCCCC>🔔 {target.Stats.CharacterName} resists Sound Burst's stun (Fort {fortSave} vs DC {saveDC}).</color>");
            Debug.Log($"[SoundBurst] {target.Stats.CharacterName} resisted stun (Fort {fortSave} >= DC {saveDC})");
        }

        return false; // Let normal damage resolution continue
    }

    // ================================================================
    //  SPIRITUAL WEAPON — PHB p.283
    //  Evocation [Force]. Cleric 2. V, S, DF.
    //  Range: Medium (100 ft + 10 ft/level). Effect: Magic weapon of force.
    //  Duration: 1 round/level (D, max 10 rounds).
    //  Saving Throw: None. Spell Resistance: Yes.
    //  Weapon attacks designated foe each round using caster's BAB + WIS mod.
    //  Deals 1d8 + 1/3 caster levels (max +5) force damage.
    //  Cannot be attacked or harmed. No flanking. No AoO.
    //  Moves up to target automatically each round.
    //  On initial cast, makes an attack immediately. Subsequent attacks
    //  happen at start of caster's turn (free action, no AoO).
    // ================================================================

    private bool TryResolveSpiritualWeaponSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SPIRITUAL_WEAPON)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = Mathf.Min(casterLevel, 10); // Max 10 rounds at CL 10+

        // Set up the spiritual weapon tracking on the caster
        caster.Stats.SpiritualWeaponActive = true;
        caster.Stats.SpiritualWeaponTarget = target;
        caster.Stats.SpiritualWeaponCasterLevel = casterLevel;
        caster.Stats.SpiritualWeaponRoundsRemaining = durationRounds;

        // Track via StatusEffectManager
        var statusMgr = caster.StatusEffectManager;
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);
            if (effect != null)
                effect.RemainingRounds = durationRounds;
        }

        // Make the initial attack on casting
        string attackLog = ResolveSpiritualWeaponAttack(caster, target, casterLevel);

        CombatUI?.ShowCombatLog($"<color=#CCAAFF>⚔🌟 Spiritual Weapon! {caster.Stats.CharacterName} conjures a force weapon for {durationRounds} round(s)!\n{attackLog}</color>");
        Debug.Log($"[SpiritualWeapon] Created for {caster.Stats.CharacterName}, target={target.Stats.CharacterName}, CL={casterLevel}, duration={durationRounds}");

        return true;
    }

    /// <summary>
    /// Resolves a single Spiritual Weapon attack roll and damage.
    /// Uses caster's BAB + WIS mod for attack, 1d8 + floor(CL/3) force damage (max +5).
    /// </summary>
    private string ResolveSpiritualWeaponAttack(
        CharacterController caster, CharacterController target, int casterLevel)
    {
        if (caster?.Stats == null || target?.Stats == null)
            return "";

        // Attack roll: caster's BAB + WIS modifier
        int bab = caster.Stats.BaseAttackBonus;
        int wisMod = caster.Stats.WISMod;
        int attackRoll = DiceService.D20("Spiritual Weapon attack");
        int totalAttack = attackRoll + bab + wisMod;
        int targetAC = target.Stats.ArmorClass;

        StringBuilder sb = new StringBuilder();

        bool isHit = CombatCalculationService.IsHit(attackRoll, totalAttack, targetAC);

        if (isHit)
        {
            // Damage: 1d8 + floor(CL/3), max bonus of +5
            int damageBonus = Mathf.Min(5, casterLevel / 3);
            int damage = DiceService.D8("Spiritual Weapon damage 1d8") + damageBonus;
            damage = Mathf.Max(1, damage);

            target.Stats.TakeDamage(damage);

            sb.Append($"  ⚔ Spiritual Weapon attacks {target.Stats.CharacterName}: {attackRoll}+{bab}+{wisMod}={totalAttack} vs AC {targetAC} — HIT for {damage} force damage!");
            Debug.Log($"[SpiritualWeapon] Hit {target.Stats.CharacterName}: roll={attackRoll}+BAB{bab}+WIS{wisMod}={totalAttack} vs AC{targetAC}, damage={damage}");
        }
        else
        {
            sb.Append($"  ⚔ Spiritual Weapon attacks {target.Stats.CharacterName}: {attackRoll}+{bab}+{wisMod}={totalAttack} vs AC {targetAC} — MISS!");
            Debug.Log($"[SpiritualWeapon] Miss {target.Stats.CharacterName}: roll={attackRoll}+BAB{bab}+WIS{wisMod}={totalAttack} vs AC{targetAC}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Called at the start of a character's turn to process their Spiritual Weapon.
    /// Should be called from the turn processing loop.
    /// </summary>
    public void ProcessSpiritualWeaponTurnStart(CharacterController character)
    {
        if (character?.Stats == null || !character.Stats.SpiritualWeaponActive)
            return;

        // Decrement duration
        if (character.Stats.SpiritualWeaponRoundsRemaining > 0)
        {
            character.Stats.SpiritualWeaponRoundsRemaining--;

            if (character.Stats.SpiritualWeaponRoundsRemaining <= 0)
            {
                // Spiritual weapon expires
                character.Stats.SpiritualWeaponActive = false;
                character.Stats.SpiritualWeaponTarget = null;
                character.Stats.SpiritualWeaponCasterLevel = 0;
                CombatUI?.ShowCombatLog(CombatLogHelper.ConditionFaded("🌟", character.Stats.CharacterName, "Spiritual Weapon"));
                Debug.Log($"[SpiritualWeapon] Expired for {character.Stats.CharacterName}");
                return;
            }
        }

        // Make the automatic attack
        var target = character.Stats.SpiritualWeaponTarget;
        if (target == null || target.Stats == null || target.Stats.CurrentHP <= -10)
        {
            // Target is dead — weapon remains but doesn't attack
            // Per PHB: caster can redirect as a move action (simplified: auto-retarget nearest enemy)
            var newTarget = FindNearestLivingEnemy(character);
            if (newTarget != null)
            {
                character.Stats.SpiritualWeaponTarget = newTarget;
                target = newTarget;
                Debug.Log($"[SpiritualWeapon] Retargeted to {target.Stats.CharacterName}");
            }
            else
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🌟", $"{character.Stats.CharacterName}'s Spiritual Weapon has no valid target."));
                return;
            }
        }

        string attackLog = ResolveSpiritualWeaponAttack(character, target, character.Stats.SpiritualWeaponCasterLevel);
        CombatUI?.ShowCombatLog($"<color=#CCAAFF>{attackLog}</color>");
        UpdateAllStatsUI();
    }

    /// <summary>Finds the nearest living enemy to a character for auto-retargeting.</summary>
    private CharacterController FindNearestLivingEnemy(CharacterController self)
    {
        if (self == null) return null;
        var allChars = GetAllCharacters();
        if (allChars == null) return null;

        CharacterController nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var ch in allChars)
        {
            if (ch == null || ch == self || ch.Stats == null) continue;
            if (ch.Stats.CurrentHP <= -10) continue; // Dead
            if (ch.IsPlayerControlled == self.IsPlayerControlled) continue; // Same side

            float dist = Vector2Int.Distance(self.GridPosition, ch.GridPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ch;
            }
        }

        return nearest;
    }

    // ================================================================
    //  SHIELD OTHER — PHB p.278
    //  Abjuration. Cleric 2 / Paladin 2. V, S, F (pair of platinum rings).
    //  Range: Close. Target: One creature.
    //  Duration: 1 hr/level (D). Saving Throw: Will negates (harmless).
    //  +1 deflection AC, +1 resistance bonus on saves.
    //  Caster takes half of the subject's damage (transferred via empathic link).
    //  NOTE: The +1 deflection/resistance is handled by SpellData buffs.
    //  The damage sharing is handled in CharacterStats.TakeDamage.
    //  This handler sets up the Shield Other link between caster and target.
    // ================================================================

    private bool TryResolveShieldOtherSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SHIELD_OTHER)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true; // Spell was cast but failed (shouldn't happen for harmless)

        // Clear any existing Shield Other on the target
        ClearShieldOtherLink(target);
        // Clear any existing protection the caster is providing to someone else
        ClearShieldOtherProtectorLink(caster);

        // Establish the empathic link
        target.Stats.ShieldOtherProtectedActive = true;
        target.Stats.ShieldOtherProtector = caster;
        caster.Stats.ShieldOtherProtectorActive = true;
        caster.Stats.ShieldOtherProtected = target;

        CombatUI?.ShowCombatLog($"<color=#66CCFF>🛡 Shield Other! {caster.Stats.CharacterName} protects {target.Stats.CharacterName} (+1 deflection AC, +1 resistance saves, damage shared).</color>");
        Debug.Log($"[ShieldOther] Empathic link: {caster.Stats.CharacterName} (protector) ↔ {target.Stats.CharacterName} (protected)");

        return false; // Return false so the normal buff application (deflection/resistance) still runs
    }

    /// <summary>Clears Shield Other protection FROM a target (the protected character).</summary>
    private void ClearShieldOtherLink(CharacterController protectedChar)
    {
        if (protectedChar?.Stats == null) return;

        if (protectedChar.Stats.ShieldOtherProtectedActive && protectedChar.Stats.ShieldOtherProtector != null)
        {
            var oldProtector = protectedChar.Stats.ShieldOtherProtector;
            if (oldProtector.Stats != null)
            {
                oldProtector.Stats.ShieldOtherProtectorActive = false;
                oldProtector.Stats.ShieldOtherProtected = null;
            }
        }

        protectedChar.Stats.ShieldOtherProtectedActive = false;
        protectedChar.Stats.ShieldOtherProtector = null;
    }

    /// <summary>Clears Shield Other protection FROM a caster (the protector).</summary>
    private void ClearShieldOtherProtectorLink(CharacterController protector)
    {
        if (protector?.Stats == null) return;

        if (protector.Stats.ShieldOtherProtectorActive && protector.Stats.ShieldOtherProtected != null)
        {
            var oldProtected = protector.Stats.ShieldOtherProtected;
            if (oldProtected.Stats != null)
            {
                oldProtected.Stats.ShieldOtherProtectedActive = false;
                oldProtected.Stats.ShieldOtherProtector = null;
            }
        }

        protector.Stats.ShieldOtherProtectorActive = false;
        protector.Stats.ShieldOtherProtected = null;
    }


}
