// ============================================================================
// GameManager_ClericSpells3.cs — Resolution logic for 3rd-level Cleric spells:
//   Searing Light, Invisibility Purge, Remove Disease, Prayer,
//   Remove Blindness/Deafness.
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
// ============================================================================
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
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // Determine creature type
        string creatureType = target.Stats.CreatureType ?? "Humanoid";
        creatureType = creatureType.Trim().ToLowerInvariant();

        bool isUndead = creatureType == "undead";
        bool isConstruct = creatureType == "construct";

        // Calculate number of dice: 1 die per 2 caster levels, max 5 dice
        int numDice = Mathf.Clamp(casterLevel / 2, 1, 5);

        int damage = 0;
        string damageDesc;

        if (isUndead)
        {
            // Undead: 1d8 per 2 CL, max 5d8
            for (int i = 0; i < numDice; i++)
                damage += Random.Range(1, 9); // 1d8
            damageDesc = $"{numDice}d8 = {damage} (undead — full divine damage)";
        }
        else if (isConstruct)
        {
            // Constructs: 1d6 per 2 CL, max 5d6
            for (int i = 0; i < numDice; i++)
                damage += Random.Range(1, 7); // 1d6
            damageDesc = $"{numDice}d6 = {damage} (construct)";
        }
        else
        {
            // Others: 1d8 per 2 CL, max 5d8, then half damage
            int fullDamage = 0;
            for (int i = 0; i < numDice; i++)
                fullDamage += Random.Range(1, 9); // 1d8
            damage = Mathf.Max(1, fullDamage / 2);
            damageDesc = $"{numDice}d8 = {fullDamage}, halved to {damage} (living creature)";
        }

        // Apply damage
        target.Stats.TakeDamage(damage);
        result.DamageDealt = damage;

        // Log
        string typeLabel = isUndead ? "☀💀" : isConstruct ? "☀🔧" : "☀";
        CombatUI?.ShowCombatLog($"<color=#FFD700>{typeLabel} Searing Light! {casterName} blasts {targetName} for {damageDesc} divine damage!</color>");
        Debug.Log($"[SearingLight] {casterName} -> {targetName}: {damage} damage ({creatureType})");

        return true;
    }

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
    //  REMOVE DISEASE  (PHB p.271)
    // ================================================================
    // Cures all diseases on the subject. Requires caster level check
    // (1d20 + CL) vs each disease's DC. Instantaneous.
    // Since the disease subsystem uses CombatConditionType, we remove
    // any Diseased condition on the target.

    private bool TryResolveRemoveDiseaseSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.REMOVE_DISEASE)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // Diseases are tracked via target.ActiveDiseases (populated by Contagion, etc.)
        if (target.ActiveDiseases == null || target.ActiveDiseases.Count == 0)
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>✦ {casterName} casts Remove Disease on {targetName}, but no diseases are found.</color>");
            result.BuffApplied = true;
            result.BuffDescription = "No diseases to remove.";
            return true;
        }

        // Remove each disease with a caster level check (1d20 + CL vs disease Fort DC)
        int removedCount = 0;
        StringBuilder removedList = new StringBuilder();

        // Iterate backward so removals don't shift indices
        for (int i = target.ActiveDiseases.Count - 1; i >= 0; i--)
        {
            var disease = target.ActiveDiseases[i];
            int diseaseDC = disease.DiseaseData != null ? disease.DiseaseData.FortitudeDC : 14;
            int check = Random.Range(1, 21) + casterLevel;

            string diseaseName = disease.DiseaseData != null ? disease.DiseaseData.Name : "Unknown Disease";

            if (check >= diseaseDC)
            {
                target.ActiveDiseases.RemoveAt(i);
                removedCount++;
                if (removedList.Length > 0) removedList.Append(", ");
                removedList.Append(diseaseName);
                Debug.Log($"[RemoveDisease] Removed {diseaseName} from {targetName} (check {check} vs DC {diseaseDC})");
            }
            else
            {
                CombatUI?.ShowCombatLog($"<color=#FF8888>  Failed to remove {diseaseName} (check {check} vs DC {diseaseDC})</color>");
                Debug.Log($"[RemoveDisease] Failed to remove {diseaseName} from {targetName} (check {check} vs DC {diseaseDC})");
            }
        }

        if (removedCount > 0)
        {
            CombatUI?.ShowCombatLog($"<color=#88FF88>🌿 Remove Disease! {casterName} cures {targetName} of {removedCount} disease(s): {removedList}</color>");
        }
        else
        {
            CombatUI?.ShowCombatLog($"<color=#FF8888>🌿 Remove Disease: {casterName} attempts to cure {targetName} but all caster level checks fail!</color>");
        }

        result.BuffApplied = true;
        result.BuffDescription = removedCount > 0
            ? $"Removed {removedCount} disease(s)"
            : "All disease removal checks failed";

        return true;
    }

    // ================================================================
    //  PRAYER  (PHB p.264)
    // ================================================================
    // 40-ft burst centered on caster. 1 round/level.
    // Allies: +1 luck bonus to attack rolls, weapon damage, saves, skill checks.
    // Enemies: –1 luck penalty to attack rolls, weapon damage, saves, skill checks.
    // Note: Prayer is cast on "self" but affects all in area. The resolution
    // applies buffs to allies and debuffs to enemies, tracked via ActiveSpellEffect.

    private bool TryResolvePrayerSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.PRAYER)
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel; // 1 round/level
        int radiusSquares = 8; // 40 ft = 8 squares

        List<CharacterController> allChars = GetAllCharacters();
        int allyCount = 0;
        int enemyCount = 0;

        foreach (var ch in allChars)
        {
            if (ch == null || ch.Stats == null || ch.Stats.IsDead) continue;

            // Check distance from caster
            int dist = SquareGridUtils.GetDistance(caster.GridPosition, ch.GridPosition);
            if (dist > radiusSquares) continue;

            bool isAlly = ch == caster || IsAllyTeam(caster, ch);

            if (isAlly)
            {
                // +1 luck bonus to attacks, damage, saves
                ch.Stats.PrayerActive = true;
                ch.Stats.PrayerRoundsRemaining = durationRounds;

                var statusMgr = ch.GetComponent<StatusEffectManager>();
                if (statusMgr != null)
                {
                    var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
                    if (effect != null)
                    {
                        effect.RemainingRounds = durationRounds;
                        effect.AppliedAttackBonus = 1;
                        effect.AppliedDamageBonus = 1;
                        effect.AppliedSaveBonus = 1;
                    }
                }

                // Apply luck bonuses (piggyback on morale fields — stacks with morale
                // in the real game, but this is the closest existing stat path).
                ch.Stats.MoraleAttackBonus += 1;
                ch.Stats.MoraleDamageBonus += 1;
                ch.Stats.MoraleSaveBonus += 1;

                allyCount++;
            }
            else
            {
                // –1 luck penalty to attacks, damage, saves
                // Apply via debuff ActiveSpellEffect
                var statusMgr = ch.GetComponent<StatusEffectManager>();
                if (statusMgr != null)
                {
                    var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
                    if (effect != null)
                    {
                        effect.RemainingRounds = durationRounds;
                        effect.AppliedAttackBonus = -1;
                        effect.AppliedDamageBonus = -1;
                        effect.AppliedSaveBonus = -1;
                    }
                }

                // Apply luck penalties (same morale fields — reversed on expiration)
                ch.Stats.MoraleAttackBonus -= 1;
                ch.Stats.MoraleDamageBonus -= 1;
                ch.Stats.MoraleSaveBonus -= 1;

                enemyCount++;
            }
        }

        CombatUI?.ShowCombatLog($"<color=#FFD700>🙏 Prayer! {casterName} prays — {allyCount} allies gain +1 luck bonus, {enemyCount} enemies suffer –1 luck penalty. Duration: {durationRounds} rounds.</color>");
        Debug.Log($"[Prayer] {casterName}: allies={allyCount}, enemies={enemyCount}, duration={durationRounds} rounds");

        return true;
    }

    // ================================================================
    //  REMOVE BLINDNESS / DEAFNESS  (PHB p.270)
    // ================================================================
    // Touch. Instantaneous. Removes Blinded or Deafened condition.
    // No saving throw. SR: Yes (harmless).

    private bool TryResolveRemoveBlindnessDeafnessSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.REMOVE_BLINDNESS_DEAFNESS)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";

        bool wasBlinded = target.HasCondition(CombatConditionType.Blinded);
        bool wasDeafened = target.HasCondition(CombatConditionType.Deafened);

        if (!wasBlinded && !wasDeafened)
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>✦ {casterName} casts Remove Blindness/Deafness on {targetName}, but they are neither blind nor deaf.</color>");
            result.BuffApplied = true;
            result.BuffDescription = "No blindness or deafness to remove.";
            return true;
        }

        StringBuilder removedList = new StringBuilder();

        if (wasBlinded)
        {
            target.RemoveCondition(CombatConditionType.Blinded);
            removedList.Append("Blindness");
        }

        if (wasDeafened)
        {
            target.RemoveCondition(CombatConditionType.Deafened);
            if (removedList.Length > 0) removedList.Append(" and ");
            removedList.Append("Deafness");
        }

        CombatUI?.ShowCombatLog($"<color=#88FF88>👁✨ Remove Blindness/Deafness! {casterName} cures {targetName}'s {removedList}!</color>");
        Debug.Log($"[RemoveBlindnessDeafness] {casterName} -> {targetName}: removed {removedList}");

        result.BuffApplied = true;
        result.BuffDescription = $"Removed {removedList}";

        return true;
    }

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
}
