// ============================================================================
// GameManager_ClericSpells4.cs — Resolution logic for 4th-level Cleric spells:
//   Chaos Hammer, Holy Smite, Order's Wrath, Unholy Blight,
//   Death Ward, Divine Power, Freedom of Movement, Spell Immunity,
//   Imbue with Spell Ability, Neutralize Poison, Poison, Dismissal,
//   Repel Vermin, Giant Vermin.
//
// Cure Critical Wounds and Inflict Critical Wounds are handled
// by the generic healing/damage pipeline (SpellCaster.Cast).
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
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

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
    //  DEATH WARD  (PHB p.217)
    // ================================================================
    // Touch. 1 min/level. Immunity to death spells, death effects,
    // energy drain, and negative energy effects.

    private bool TryResolveDeathWardSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DEATH_WARD) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 10; // 1 min/level = 10 rounds/level

        target.Stats.DeathWardActive = true;
        target.Stats.DeathWardRoundsRemaining = durationRounds;

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#CCFFCC>🛡✨ Death Ward! {casterName} wards {targetName} against death effects for {durationRounds} rounds.</color>");
        Debug.Log($"[DeathWard] {casterName} -> {targetName}: duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Death Ward ({durationRounds} rounds)";
        return true;
    }

    // ================================================================
    //  DIVINE POWER  (PHB p.224)
    // ================================================================
    // Personal. 1 round/level. +6 enhancement STR, +1 temp HP/level,
    // BAB equal to character level.

    private bool TryResolveDivinePowerSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DIVINE_POWER) return false;
        if (caster == null || caster.Stats == null) return false;
        if (!result.Success) return true;

        // Personal spell — target is always caster
        target = caster;
        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel; // 1 round/level

        // +6 enhancement to STR
        int strBonus = 6;
        target.Stats.DivinePowerActive = true;
        target.Stats.DivinePowerRoundsRemaining = durationRounds;
        target.Stats.DivinePowerStrBonus = strBonus;
        target.Stats.STR += strBonus; // Enhancement bonus applied directly

        // +1 temp HP per caster level
        int tempHP = casterLevel;
        target.Stats.DivinePowerTempHP = tempHP;
        target.Stats.TempHP += tempHP;

        // BAB = character level (boost the difference)
        int currentBAB = target.Stats.BaseAttackBonus;
        int characterLevel = target.Stats.CharacterLevel;
        int babBonus = Mathf.Max(0, characterLevel - currentBAB);
        target.Stats.DivinePowerBABBonus = babBonus;
        target.Stats.BaseAttackBonus += babBonus;

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null)
            {
                effect.RemainingRounds = durationRounds;
                effect.AppliedAttackBonus = babBonus; // Track for reversal
            }
        }

        CombatUI?.ShowCombatLog($"<color=#FFD700>⚔✨ Divine Power! {casterName} gains +{strBonus} STR, +{tempHP} temp HP, BAB +{babBonus} for {durationRounds} rounds.</color>");
        Debug.Log($"[DivinePower] {casterName}: STR+{strBonus}, tempHP+{tempHP}, BAB+{babBonus}, duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Divine Power ({durationRounds} rounds)";
        return true;
    }

    // ================================================================
    //  FREEDOM OF MOVEMENT  (PHB p.233)
    // ================================================================
    // Touch. 10 min/level. Immune to paralysis, entanglement,
    // grapple penalties, and movement restrictions.

    private bool TryResolveFreedomOfMovementSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.FREEDOM_OF_MOVEMENT) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 100; // 10 min/level = 100 rounds/level

        target.Stats.FreedomOfMovementActive = true;
        target.Stats.FreedomOfMovementRoundsRemaining = durationRounds;

        // Remove existing paralysis/entanglement
        if (target.HasCondition(CombatConditionType.Paralyzed))
            target.RemoveCondition(CombatConditionType.Paralyzed);
        if (target.HasCondition(CombatConditionType.Entangled))
            target.RemoveCondition(CombatConditionType.Entangled);

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#88CCFF>🦅✨ Freedom of Movement! {casterName} grants {targetName} freedom from movement restrictions for {durationRounds} rounds.</color>");
        Debug.Log($"[FreedomOfMovement] {casterName} -> {targetName}: duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Freedom of Movement ({durationRounds} rounds)";
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
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 100; // 10 min/level = 100 rounds/level

        // Default immune spell — in full implementation, player would choose
        string immuneSpellId = SpellNames.MAGIC_MISSILE;
        string immuneSpellName = "Magic Missile";

        target.Stats.SpellImmunityActive = true;
        target.Stats.SpellImmunityRoundsRemaining = durationRounds;
        target.Stats.SpellImmunitySpellId = immuneSpellId;

        var statusMgr = target.GetComponent<StatusEffectManager>();
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
    //  NEUTRALIZE POISON  (PHB p.257)
    // ================================================================
    // Touch. Instantaneous cure + 10 min/level immunity.
    // Cures existing poison and grants temporary immunity.

    private bool TryResolveNeutralizePoisonSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.NEUTRALIZE_POISON) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 100; // 10 min/level

        // Cure existing poison
        bool wasPoisoned = target.HasCondition(CombatConditionType.Poisoned);
        if (wasPoisoned)
            target.RemoveCondition(CombatConditionType.Poisoned);

        // Grant immunity
        target.Stats.NeutralizePoisonImmunityActive = true;
        target.Stats.NeutralizePoisonImmunityRoundsRemaining = durationRounds;

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        string curedMsg = wasPoisoned ? " Poison cured!" : "";
        CombatUI?.ShowCombatLog($"<color=#88FF88>🌿✨ Neutralize Poison! {casterName} neutralizes poison on {targetName}.{curedMsg} Poison immunity for {durationRounds} rounds.</color>");
        Debug.Log($"[NeutralizePoison] {casterName} -> {targetName}: cured={wasPoisoned}, immunity {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Neutralize Poison{(wasPoisoned ? " (cured)" : "")} + immunity ({durationRounds} rounds)";
        return true;
    }

    // ================================================================
    //  POISON  (PHB p.262)
    // ================================================================
    // Touch attack. Fortitude DC 14 negates.
    // Initial: 1d10 CON. Secondary: 1d10 CON (1 minute later).
    // We apply the initial damage and the Poisoned condition.

    private bool TryResolvePoisonSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.POISON) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // Check if target has Neutralize Poison immunity
        if (target.Stats.NeutralizePoisonImmunityActive)
        {
            CombatUI?.ShowCombatLog($"<color=#88FF88>🛡 {targetName} is immune to poison (Neutralize Poison)!</color>");
            return true;
        }

        // Fort save: DC = 10 + spell level (4) + WIS mod
        int saveDC = 10 + 4 + caster.Stats.WISMod;
        int saveRoll = Random.Range(1, 21) + target.Stats.FortitudeSave;
        bool saveSuccess = saveRoll >= saveDC;

        if (saveSuccess)
        {
            CombatUI?.ShowCombatLog($"<color=#88FF88>☠ Poison: {targetName} resists! (Fort {saveRoll} vs DC {saveDC})</color>");
            Debug.Log($"[Poison] {casterName} -> {targetName}: Fort save {saveRoll} vs DC {saveDC} — resisted");
            return true;
        }

        // Initial CON damage: 1d10
        int conDamage = Random.Range(1, 11);
        target.Stats.AbilityScoreDamage.ApplyDamage(AbilityType.CON, conDamage);

        // Apply Poisoned condition (for secondary damage tracking)
        if (!target.HasCondition(CombatConditionType.Poisoned))
            target.AddCondition(CombatConditionType.Poisoned);

        CombatUI?.ShowCombatLog($"<color=#CC44CC>☠ Poison! {casterName} poisons {targetName}! {conDamage} CON damage (Fort {saveRoll} vs DC {saveDC}). Secondary: 1d10 CON in 1 minute.</color>");
        Debug.Log($"[Poison] {casterName} -> {targetName}: {conDamage} CON damage, Fort save {saveRoll} vs DC {saveDC}");

        result.BuffApplied = true;
        result.BuffDescription = $"Poisoned ({conDamage} CON damage)";
        return true;
    }

    // ================================================================
    //  DISMISSAL  (PHB p.222)
    // ================================================================
    // Close range. Will save negates (–5 penalty for extraplanar creatures).
    // Sends an extraplanar creature back to its home plane.
    // In this prototype, we check for the "Extraplanar" or "Outsider"
    // creature type and kill/remove the creature on failed save.

    private bool TryResolveDismissalSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DISMISSAL) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        string creatureType = (target.Stats.CreatureType ?? "").Trim().ToLowerInvariant();
        bool isExtraplanar = creatureType == "outsider" || creatureType == "extraplanar";

        // Check if target is a summon (summoned creatures are always extraplanar)
        bool isSummon = IsSummonedCreature(target);

        if (!isExtraplanar && !isSummon)
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>  ✦ Dismissal: {targetName} is not an extraplanar creature — spell has no effect.</color>");
            return true;
        }

        // Will save with -5 penalty (D&D 3.5e)
        int saveDC = 10 + 4 + caster.Stats.WISMod;
        int saveRoll = Random.Range(1, 21) + target.Stats.WillSave - 5;
        bool saveSuccess = saveRoll >= saveDC;

        if (saveSuccess)
        {
            CombatUI?.ShowCombatLog($"<color=#88FF88>🌀 Dismissal: {targetName} resists being sent home! (Will {saveRoll} vs DC {saveDC})</color>");
            Debug.Log($"[Dismissal] {casterName} -> {targetName}: Will save {saveRoll} vs DC {saveDC} — resisted");
            return true;
        }

        // Dismissed! Kill/remove the creature
        target.Stats.TakeDamage(target.Stats.CurrentHP + 100); // Ensure death
        CombatUI?.ShowCombatLog($"<color=#FF8800>🌀✨ Dismissal! {casterName} sends {targetName} back to its home plane!</color>");
        Debug.Log($"[Dismissal] {casterName} -> {targetName}: dismissed (Will {saveRoll} vs DC {saveDC})");

        result.TargetKilled = true;
        return true;
    }

    // ================================================================
    //  REPEL VERMIN  (PHB p.271)
    // ================================================================
    // Personal emanation. 10 min/level. 10 ft/level radius.
    // Keeps vermin out. Sets flag for AI/movement checks.

    private bool TryResolveRepelVerminSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.REPEL_VERMIN) return false;
        if (caster == null || caster.Stats == null) return false;
        if (!result.Success) return true;

        // Personal spell
        target = caster;
        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 100; // 10 min/level
        int radiusFeet = casterLevel * 10; // 10 ft/level

        target.Stats.RepelVerminActive = true;
        target.Stats.RepelVerminRoundsRemaining = durationRounds;

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#88CC88>🐛🚫 Repel Vermin! {casterName} creates a {radiusFeet}-ft anti-vermin emanation for {durationRounds} rounds.</color>");
        Debug.Log($"[RepelVermin] {casterName}: radius {radiusFeet} ft, duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Repel Vermin ({radiusFeet} ft, {durationRounds} rounds)";
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
}
