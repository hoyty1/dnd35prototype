// ============================================================================
//  GameManager_Spells_C.cs  —  Spell resolution: 'C' spells
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
    //  CONTAGION — PHB p.213
    //  Necromancy [Evil]. Clr 3, Dru 3, Sor/Wiz 4.
    //  Melee touch attack. Target contracts a disease chosen by caster.
    //  Disease takes effect immediately (no incubation period).
    //  Fortitude negates. SR: Yes.
    // ================================================================

    private static bool IsContagionSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.CONTAGION, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// The list of diseases available for Contagion (PHB standard diseases).
    /// </summary>
    private static readonly DiseaseType[] ContagionDiseases = new[]
    {
        DiseaseType.BlindingSickness,
        DiseaseType.CackleFever,
        DiseaseType.FilthFever,
        DiseaseType.Mindfire,
        DiseaseType.RedAche,
        DiseaseType.Shakes,
        DiseaseType.SlimyDoom
    };

    /// <summary>
    /// Resolves Contagion: melee touch attack, Fort negates, applies disease immediately.
    /// For AI/NPC casters, a random disease is chosen. For PC casters, a random one is also
    /// selected (disease selection UI can be added later).
    /// </summary>
    private bool TryResolveContagionSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsContagionSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Melee touch missed → no effect (charge held)
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Contagion touch misses {target.Stats.CharacterName}.");
            return true;
        }

        // Fort save negates
        if (result.RequiredSave && result.SaveSucceeded)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Immune("🛡", $"{target.Stats.CharacterName} resists Contagion with a Fortitude save!"));
            return true;
        }

        // Check disease immunity
        if (target.Stats.IsImmuneToDisease())
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Immune("🛡", $"{target.Stats.CharacterName} is immune to disease!"));
            return true;
        }

        // Select a disease — random for now (both PC and NPC casters)
        DiseaseType selectedType = ContagionDiseases[UnityEngine.Random.Range(0, ContagionDiseases.Length)];
        DiseaseData diseaseData = DiseaseDatabase.GetDisease(selectedType);

        if (diseaseData == null)
        {
            Debug.LogWarning($"[Contagion] Failed to find disease data for {selectedType}");
            return true;
        }

        // Create the active disease with NO incubation (Contagion's special property)
        ActiveDisease activeDisease = new ActiveDisease(diseaseData);
        activeDisease.DaysUntilActive = 0;
        activeDisease.IsIncubating = false;

        // Add to target's active diseases
        target.ActiveDiseases.Add(activeDisease);

        // Apply the first round of disease damage immediately
        if (diseaseData.DamageEffects != null && diseaseData.DamageEffects.Count > 0)
        {
            string damageReport = "";
            foreach (AbilityDamageEffect dmgEffect in diseaseData.DamageEffects)
            {
                int damage = dmgEffect.RollDamage();
                if (damage > 0)
                {
                    target.ApplyAbilityDamage(dmgEffect.Ability, damage, diseaseData.Name);
                    if (damageReport.Length > 0) damageReport += ", ";
                    damageReport += $"{damage} {dmgEffect.Ability} damage";
                }
            }

            if (damageReport.Length > 0)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Color($"   Immediate effect: {damageReport}", "CC6633"));
            }
        }

        result.BuffApplied = true;
        result.BuffDescription = $"Disease: {diseaseData.Name} contracted (immediate onset).";

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        CombatUI?.ShowCombatLog(CombatLogHelper.Color($"🦠 {target.Stats.CharacterName} contracts {diseaseData.Name} from Contagion!", "CC6633"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Color($"   Fort DC {diseaseData.FortitudeDC} daily to resist. 2 consecutive saves = cured.", "CC9966"));

        // Check if ability damage killed the target
        target.CheckAbilityScoreZeroEffects();

        Debug.Log($"[Contagion] {casterName} -> {target.Stats.CharacterName}: contracted {diseaseData.Name} (immediate onset, DC {diseaseData.FortitudeDC})");
        return true;
    }

}
