// ============================================================================
// GameManager_Spells_MagicFang.cs — Magic Fang Effect Implementation
//
// Part of the GameManager partial class.
// Implements Magic Fang (PHB p.250) — +1 enhancement bonus to one natural
// weapon's attack and damage rolls. Duration 1 min/level.
//
// D&D 3.5e PHB core rules ONLY.
// ============================================================================
using DND35e.Identifiers;
using System.Text;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  MAGIC FANG — PHB p.250
    //  Transmutation
    //  Level: Drd 1, Rgr 1
    //  Components: V, S, DF
    //  Casting Time: 1 standard action
    //  Range: Touch
    //  Target: Living creature touched
    //  Duration: 1 min./level
    //  Saving Throw: Will negates (harmless)
    //  Spell Resistance: Yes (harmless)
    //
    //  Magic fang gives one natural weapon of the subject a +1
    //  enhancement bonus on attack and damage rolls. The spell can
    //  affect a slam attack, fist, bite, or other natural weapon.
    //  The spell does not change an unarmed strike's damage from
    //  nonlethal damage to lethal damage.
    //
    //  Implementation: Since natural attacks in this prototype resolve
    //  through GetNaturalAttackBonus (BAB + STR + size) and damage
    //  bonus (STR-based), we track the +1 enhancement via the
    //  StatusEffectManager and apply it as AppliedAttackBonus /
    //  AppliedDamageBonus on the ActiveSpellEffect. The creature's
    //  natural attacks gain the bonus for the spell's duration.
    // ================================================================

    private ActiveSpellEffect ApplyMagicFangEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        string recipientName = recipient.Stats.CharacterName;

        StatusEffectManager statusMgr = recipient.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
        {
            statusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(recipient.Stats);
        }

        int casterLevel = caster != null && caster.Stats != null
            ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell))
            : 1;

        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // +1 enhancement bonus to attack and damage (Magic Fang is always +1)
        int enhancementBonus = 1;

        ActiveSpellEffect effect = statusMgr.AddEffect(
            spell,
            casterName,
            casterLevel);

        if (effect != null)
        {
            // Apply +1 enhancement to attack and damage
            // These bonuses apply to the creature's attacks (primarily natural weapons).
            recipient.Stats.MoraleAttackBonus += enhancementBonus;
            recipient.Stats.MoraleDamageBonus += enhancementBonus;
            effect.AppliedAttackBonus = enhancementBonus;
            effect.AppliedDamageBonus = enhancementBonus;

            SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
            if (recipientSpellComp != null)
                recipientSpellComp.ActiveBuffs[spell.SpellId] = durationRounds;
        }

        // Determine which natural weapon is described in the log
        string naturalWeaponName = "natural weapon";
        if (recipient.Stats.HasNaturalAttacks)
        {
            var attacks = recipient.Stats.GetValidNaturalAttacks();
            if (attacks != null && attacks.Count > 0)
                naturalWeaponName = attacks[0].Name ?? "natural attack";
        }

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"<color=#88CC66>🐾 {casterName} casts Magic Fang on {recipientName}!</color>");
        sb.AppendLine($"  School: Transmutation | Level: 1 (Druid/Ranger)");
        sb.AppendLine($"  {recipientName}'s {naturalWeaponName} gains +{enhancementBonus} enhancement bonus to attack and damage.");
        sb.AppendLine($"  Duration: {durationRounds} rounds (CL {casterLevel})");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[MagicFang] +{enhancementBonus} enhancement to {recipientName}'s {naturalWeaponName} for {durationRounds} rounds (CL {casterLevel})");

        UpdateAllStatsUI();
        return effect;
    }
}
