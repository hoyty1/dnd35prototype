// ============================================================================
// GameManager_Spells_L.cs — Spell resolution methods starting with "L".
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
    // ═══════════════════════════════════════════════════════════════
    // LESSER GLOBE OF INVULNERABILITY (PHB p.246)
    // ═══════════════════════════════════════════════════════════════

    private static bool IsLesserGlobeOfInvulnerabilitySpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.LESSER_GLOBE_OF_INVULNERABILITY, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Lesser Globe of Invulnerability: creates a 10-ft radius emanation
    /// centered on the caster that blocks spell effects of 3rd level or lower.
    /// The emanation moves with the caster.
    /// PHB p.246
    /// </summary>
    private bool TryResolveLesserGlobeSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (!IsLesserGlobeOfInvulnerabilitySpell(spell))
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = Mathf.Max(1, casterLevel); // 1 round/level

        // Create the area effect centered on the caster
        Vector3 centerWorldPos = caster.transform.position;

        GameObject globeObj = new GameObject("LesserGlobeOfInvulnerability_Area");
        globeObj.transform.position = centerWorldPos;

        LesserGlobeOfInvulnerabilityAreaEffect globeEffect = globeObj.AddComponent<LesserGlobeOfInvulnerabilityAreaEffect>();
        globeEffect.CenterPosition = centerWorldPos;
        globeEffect.RoundsRemaining = durationRounds;
        globeEffect.CasterLevel = casterLevel;
        globeEffect.Caster = caster;
        globeEffect.MaxBlockedSpellLevel = 3; // Lesser Globe blocks ≤ 3rd level

        string casterName = caster.Stats.CharacterName ?? "Unknown";

        CombatUI?.ShowCombatLog($"<color=#44AAFF>🛡 {casterName} casts Lesser Globe of Invulnerability!</color>");
        CombatUI?.ShowCombatLog($"  A 10-ft radius emanation of shimmering protection forms around {casterName}.");
        CombatUI?.ShowCombatLog($"  Blocks all spell effects of 3rd level or lower.");
        CombatUI?.ShowCombatLog($"  Spells of 4th level and higher pass through normally.");
        CombatUI?.ShowCombatLog($"  Globe moves with the caster. Duration: {durationRounds} round(s).");

        Debug.Log($"[LesserGlobe] Created by {casterName}: CL {casterLevel}, blocks ≤ level 3, {durationRounds} rounds");

        return true;
    }

}
