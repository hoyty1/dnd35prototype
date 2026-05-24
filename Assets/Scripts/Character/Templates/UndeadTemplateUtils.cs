using System.Collections.Generic;

/// <summary>
/// Shared utility methods for undead creature templates (Skeleton, Zombie).
/// Eliminates duplication of common undead-template operations.
/// </summary>
public static class UndeadTemplateUtils
{
    /// <summary>
    /// Strips all special on-hit effects from a natural attack (poison, paralysis,
    /// energy drain, disease, petrification, elemental damage, blood drain).
    /// Used by both skeleton and zombie templates — undead lose the base creature's
    /// extraordinary special attacks.
    /// </summary>
    public static void StripSpecialEffects(NaturalAttackDefinition attack)
    {
        if (attack == null) return;

        attack.PoisonOnHitId = null;
        attack.ParalysisOnHitDC = 0;
        attack.ParalysisOnHitDurationRounds = 0;
        attack.EnergyDrainOnHit = 0;
        attack.EnergyDrainRemovalDC = 0;
        attack.AbilityDrainAmount = 0;
        attack.HasDiseaseOnHit = false;
        attack.PetrificationOnHitDC = 0;
        attack.BonusElementalDamageDice = 0;
        attack.BonusElementalDamageCount = 0;
        attack.HasBloodDrain = false;
        attack.BloodDrainConDamagePerRound = 0;
    }

    /// <summary>
    /// Strips special effects from all natural attacks in the provided list.
    /// </summary>
    public static void StripAllSpecialEffects(List<NaturalAttackDefinition> attacks)
    {
        if (attacks == null) return;
        for (int i = 0; i < attacks.Count; i++)
            StripSpecialEffects(attacks[i]);
    }
}
