// ============================================================================
//  GameManager_Spells_B.cs  —  Spell resolution: 'B' spells
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
    //  BLINK — Ethereal/Material Plane Shifting (PHB p.206)
    // ================================================================

    /// <summary>
    /// Applies the Blink spell effect to the caster (Personal range).
    /// Per PHB p.206: Subject blinks between Material and Ethereal Planes randomly.
    ///
    /// Defensive benefits:
    ///   - 50% miss chance vs physical attacks (ethereal, not invisible)
    ///   - 20% miss chance if attacker can see invisible OR strike ethereal
    ///   - 0% miss chance if attacker can do BOTH
    ///   - Blind-Fight feat doesn't help
    ///   - 50% miss chance vs targeted spells
    ///   - Half damage from area attacks
    ///   - Half damage from falling
    ///
    /// Offensive penalties/bonuses:
    ///   - 20% miss chance on own attacks
    ///   - 20% failure chance on own spells
    ///   - +2 attack bonus (strikes as invisible)
    ///   - Denies target Dex bonus to AC
    ///
    /// Duration: 1 round/level (D).
    /// </summary>
    private ActiveSpellEffect ApplyBlinkBuff(CharacterController caster, SpellData spell, SpellcastingComponent spellComp)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return null;

        StatusEffectManager statusMgr = caster.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = caster.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(caster.Stats);

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        ActiveSpellEffect effect = statusMgr.AddEffect(
            spell,
            caster.Stats.CharacterName,
            casterLevel);

        if (effect != null)
        {
            SpellcastingComponent casterSpellComp = caster.GetComponent<SpellcastingComponent>();
            if (casterSpellComp != null)
                casterSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

            // Apply the Blinking condition
            caster.ApplyCondition(CombatConditionType.Blinking, effect.RemainingRounds, spell.Name);

            string casterName = caster.Stats.CharacterName;
            CombatUI?.ShowCombatLog($"<color=#88CCFF>✨ {casterName} casts Blink!</color>");
            CombatUI?.ShowCombatLog($"<color=#A6D4FF>   {casterName} begins blinking between the Material and Ethereal Planes.</color>");
            CombatUI?.ShowCombatLog($"<color=#A6D4FF>   Defensive: 50% miss chance vs attacks ({effect.GetDurationDisplayString()}).</color>");
            CombatUI?.ShowCombatLog($"<color=#A6D4FF>   Offensive: 20% miss chance, but +2 attack & deny Dex to AC.</color>");
        }

        UpdateAllStatsUI();
        return effect;
    }

    // ================================================================
    //  BESTOW CURSE — PHB p.203
    //  Necromancy. Clr 3, Sor/Wiz 4.
    //  Melee touch attack. Will negates. SR: Yes.
    //  Duration: Permanent.
    //  Effects (choose one):
    //    • -6 penalty to one ability score (minimum 1)
    //    • -4 penalty on attack rolls, saves, ability checks, skill checks
    //    • 50% chance each turn the creature can't act normally
    // ================================================================

    private static bool IsBestowCurseSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.BESTOW_CURSE, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// The type of curse effect applied by Bestow Curse.
    /// </summary>
    public enum BestowCurseType
    {
        /// <summary>-6 penalty to one ability score (minimum 1).</summary>
        AbilityPenalty,
        /// <summary>-4 penalty on attack rolls, saves, ability checks, and skill checks.</summary>
        GeneralPenalty,
        /// <summary>50% chance each turn the creature can't act normally.</summary>
        ActionLoss
    }

    /// <summary>
    /// Resolves Bestow Curse: melee touch attack, Will negates, applies permanent curse.
    /// AI/NPC casters pick a random curse type. PC casters also get random for now
    /// (curse selection UI can be added later).
    /// </summary>
    private bool TryResolveBestowCurseSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsBestowCurseSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Melee touch missed → no effect (charge held)
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Bestow Curse touch misses {target.Stats.CharacterName}.");
            return true;
        }

        // Will save negates
        if (result.RequiredSave && result.SaveSucceeded)
        {
            CombatUI?.ShowCombatLog($"<color=#66CC66>🛡 {target.Stats.CharacterName} resists Bestow Curse with a Will save!</color>");
            return true;
        }

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        // Choose a random curse type
        BestowCurseType curseType = (BestowCurseType)UnityEngine.Random.Range(0, 3);
        string curseDescription = "";

        switch (curseType)
        {
            case BestowCurseType.AbilityPenalty:
            {
                // Pick a random ability score
                AbilityType[] abilities = { AbilityType.STR, AbilityType.DEX, AbilityType.CON, AbilityType.INT, AbilityType.WIS, AbilityType.CHA };
                AbilityType chosenAbility = abilities[UnityEngine.Random.Range(0, abilities.Length)];
                int penalty = 6;

                // Apply as ability damage (tracked, permanent until Remove Curse)
                target.ApplyAbilityDamage(chosenAbility, penalty, "Bestow Curse");
                curseDescription = $"-{penalty} {chosenAbility} penalty";

                // Register with CurseTracker for Remove Curse
                CurseTracker.AddCurse(target, new CurseEffectData
                {
                    SourceSpellId = SpellNames.BESTOW_CURSE,
                    Description = curseDescription,
                    CasterName = casterName,
                    CasterLevel = casterLevel,
                    AffectedAbility = chosenAbility.ToString(),
                    PenaltyAmount = penalty,
                    Type = CurseType.BestowCurseAbilityPenalty
                });

                CombatUI?.ShowCombatLog($"<color=#8B0000>🔮 {target.Stats.CharacterName} is cursed! {curseDescription} (permanent).</color>");
                CombatUI?.ShowCombatLog($"<color=#AA5555>   Ability reduced by {penalty} (minimum effective score of 1).</color>");
                break;
            }

            case BestowCurseType.GeneralPenalty:
            {
                // Apply -4 penalty on attacks, saves, ability checks, skill checks
                // Use the condition system to track this
                target.ApplyCondition(CombatConditionType.BestowCurseGeneralPenalty, -1, "Bestow Curse");
                curseDescription = "-4 on attacks, saves, ability checks, and skill checks";

                // Register with CurseTracker for Remove Curse
                CurseTracker.AddCurse(target, new CurseEffectData
                {
                    SourceSpellId = SpellNames.BESTOW_CURSE,
                    Description = curseDescription,
                    CasterName = casterName,
                    CasterLevel = casterLevel,
                    PenaltyAmount = 4,
                    Type = CurseType.BestowCurseGeneralPenalty
                });

                CombatUI?.ShowCombatLog($"<color=#8B0000>🔮 {target.Stats.CharacterName} is cursed! {curseDescription} (permanent).</color>");
                break;
            }

            case BestowCurseType.ActionLoss:
            {
                // 50% chance each turn the creature can't act
                target.ApplyCondition(CombatConditionType.BestowCurseActionLoss, -1, "Bestow Curse");
                curseDescription = "50% chance each turn to lose all actions";

                // Register with CurseTracker for Remove Curse
                CurseTracker.AddCurse(target, new CurseEffectData
                {
                    SourceSpellId = SpellNames.BESTOW_CURSE,
                    Description = curseDescription,
                    CasterName = casterName,
                    CasterLevel = casterLevel,
                    Type = CurseType.BestowCurseActionLoss
                });

                CombatUI?.ShowCombatLog($"<color=#8B0000>🔮 {target.Stats.CharacterName} is cursed! {curseDescription} (permanent).</color>");
                break;
            }
        }

        // Track via StatusEffectManager for UI/dispel
        if (target.StatusEffectManager != null)
        {
            target.StatusEffectManager.AddEffect(spell, casterName, casterLevel);
        }

        result.BuffApplied = true;
        result.BuffDescription = $"Debuff: Bestow Curse — {curseDescription}.";

        // Check if ability damage killed the target
        target.CheckAbilityScoreZeroEffects();

        Debug.Log($"[BestowCurse] {casterName} -> {target.Stats.CharacterName}: {curseDescription} (permanent)");
        return true;
    }


}
