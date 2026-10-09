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
    //  Implementation: the spell data's BuffAttackBonus/BuffDamageBonus
    //  (+1/+1) become the tracked effect's AppliedAttackBonus /
    //  AppliedDamageBonus through StatusEffectManager.AddEffect, which
    //  adds them on cast and takes them back when the spell ends. Like
    //  every spell bonus they land in the Morale* fields, so they reach
    //  the attack and damage roll of every attack of the subject, not
    //  only one natural weapon (SPL-026).
    //  Before SPL-037 this handler added a second +1 that was never
    //  removed; it adds nothing of its own now.
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

        StatusEffectManager statusMgr = recipient.StatusEffectManager;
        if (statusMgr == null)
            statusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(recipient.Stats); // always rebind to the current stats, as the generic branch does

        int casterLevel = caster != null && caster.Stats != null
            ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell))
            : 1;

        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);

        // +1 enhancement bonus to attack and damage (Magic Fang is always +1): AddEffect applies the data's +1/+1.
        int enhancementBonus = 1;

        ActiveSpellEffect effect = statusMgr.AddEffect(
            spell,
            casterName,
            casterLevel);

        if (effect != null)
        {
            SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
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
