using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encapsulates combat-stat and attack-sequence math used by <see cref="CharacterController"/>.
/// </summary>
public class CharacterCombatStats : MonoBehaviour
{
    private CharacterController _character;

    public void Initialize(CharacterController character)
    {
        _character = character;
    }

    /// <summary>
    /// Number of iterative main-hand attacks granted by BAB.
    /// </summary>
    public int GetNumberOfAttacks()
    {
        return GetAttackBonuses().Count;
    }

    /// <summary>
    /// Iterative attack BAB values (BAB, BAB-5, BAB-10, BAB-15). While the creature flurries, the flurry's steps instead
    /// (CharacterStats.GetFlurryOfBlowsBaseAttackBonuses: the extra attack, two from 11th level, at the highest BAB;
    /// PHB p.40-41, CHR-003), so the shared attack sequence (budget, step BAB, maneuver substitutes) follows the flurry.
    /// Once the monk is grappling (a flurry step replaced by a grapple that succeeded), her remaining actions are grapple
    /// actions, one in place of each attack her base attack bonus gives (PHB p.156), so the plain ladder applies again
    /// and the flurry's extra attack ends; the flurry penalty still applies to her attack rolls that round
    /// (CharacterController.FlurryAttackRollPenalty, PHB p.40). Delegated RAW reading 2026-10-14, the same reading that
    /// bars declaring a flurry while grappling (CharacterController.CanUseFlurryOfBlows).
    /// </summary>
    public List<int> GetAttackBonuses()
    {
        if (_character != null && _character.Stats != null && _character.IsUsingFlurryOfBlows && !_character.IsGrappling())
        {
            int[] flurry = _character.Stats.GetFlurryOfBlowsBaseAttackBonuses();
            if (flurry.Length > 0)
                return new List<int>(flurry);
        }

        int bab = _character != null && _character.Stats != null ? _character.Stats.BaseAttackBonus : 0;
        return GetBaseAttackLadder(bab);
    }

    /// <summary>
    /// The one iterative-attack ladder (PHB p.22, Table 3-1, and p.143, Full Attack; CMB-044): the base attack bonus, then one more attack at
    /// -5 for each further +5, up to four attacks (BAB +6: +6/+1; +11: +11/+6/+1; +16: four). A base attack bonus of +5
    /// or less (zero or a negative one too) gives one attack at that bonus. Without Haste, Rapid Shot or a flurry: the
    /// flurry's steps are built on this ladder (CharacterStats.GetFlurryOfBlowsBaseAttackBonuses), Haste and Rapid
    /// Shot add their attack in the full-attack sequence (<see cref="GetIterativeAttackCount"/>,
    /// CharacterController.GetPlannedFullAttackCount and FullAttack).
    /// </summary>
    private bool IsRapidShotDeclared => _character != null && _character.ProgressiveAttackPool.IsRapidShot;

    public static List<int> GetBaseAttackLadder(int baseAttackBonus)
    {
        var bonuses = new List<int> { baseAttackBonus };

        if (baseAttackBonus >= 6) bonuses.Add(baseAttackBonus - 5);
        if (baseAttackBonus >= 11) bonuses.Add(baseAttackBonus - 10);
        if (baseAttackBonus >= 16) bonuses.Add(baseAttackBonus - 15);

        return bonuses;
    }

    /// <summary>
    /// Attacks of a weapon (or unarmed) full attack: the steps of <see cref="GetAttackBonuses"/> (the flurry's while the
    /// creature flurries) plus Haste's extra attack at the highest bonus (PHB p.239), plus Rapid Shot's extra attack once it
    /// is declared this turn (PHB p.99; AttackPool.IsRapidShot, D9: declared before the first attack, so the turn's
    /// budget includes it). Before the declaration, CharacterController.GetPlannedFullAttackCount adds it for the toggle.
    /// </summary>
    public int GetIterativeAttackCount()
    {
        int count = GetAttackBonuses().Count;
        if (IsRapidShotDeclared)
            count++;

        // Haste grants one extra attack at highest BAB (PHB p.239)
        if (_character != null && _character.HasActiveHasteEffect
            && _character.ActiveHasteEffect.GrantsExtraAttack)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// The BAB of step <paramref name="attackIndex"/> of the full attack, in FullAttack's order: Rapid Shot's extra attack
    /// first at the highest bonus once declared (PHB p.99), then the ladder, then Haste's extra attack at the highest bonus.
    /// </summary>
    public int GetIterativeAttackBAB(int attackIndex)
    {
        List<int> bonuses = GetAttackBonuses();

        if (IsRapidShotDeclared)
        {
            if (attackIndex == 0)
                return bonuses.Count > 0 ? bonuses[0] : 0;
            attackIndex--;
        }

        // Haste extra attack is appended at the end and uses highest BAB
        bool hasteExtraAttack = _character != null && _character.HasActiveHasteEffect
            && _character.ActiveHasteEffect.GrantsExtraAttack;

        if (hasteExtraAttack && attackIndex == bonuses.Count)
        {
            // The Haste extra attack is at the character's highest BAB
            return bonuses.Count > 0 ? bonuses[0] : 0;
        }

        if (attackIndex < 0 || attackIndex >= bonuses.Count)
            return 0;

        return bonuses[attackIndex];
    }

    /// <summary>
    /// Off-hand attacks of a two-weapon full attack (PHB p.160, p.102, p.96): anyone wielding a second weapon in the off hand
    /// gets one extra attack with it (the Two-Weapon Fighting feat only lowers the penalties), Improved Two-Weapon Fighting a
    /// second at -5 and Greater Two-Weapon Fighting a third at -10 (FeatManager.GetOffHandAttackCount). None without an
    /// off-hand weapon or while grappling (CanDualWield). Batch 12 step 18: before it the count was 0 without the feat, and
    /// nothing read it.
    /// </summary>
    public int GetOffHandAttackCount()
    {
        if (_character == null || _character.Stats == null || !_character.CanDualWield())
            return 0;

        return FeatManager.GetOffHandAttackCount(_character.Stats);
    }

    public int GetOffHandAttackBAB(int attackIndex)
    {
        int bab = _character != null && _character.Stats != null ? _character.Stats.BaseAttackBonus : 0;
        if (attackIndex < 0)
            attackIndex = 0;

        return bab - (attackIndex * 5);
    }

    public int GetGrappleSizeModifier()
    {
        return _character != null && _character.Stats != null
            ? _character.Stats.CurrentSizeCategory.GetGrappleModifier()
            : 0;
    }

    public int GetGrappleModifier(int? baseAttackBonusOverride = null)
    {
        if (_character == null || _character.Stats == null)
            return 0;

        // BAB + Str + the special size modifier + effects that name grapple checks (PHB p.155-156). Condition attack
        // modifiers do not apply: a grapple check is not an attack roll (owner interpretation 2026-10-11, CMB-130).
        int sizeMod = _character.Stats.CurrentSizeCategory.GetGrappleModifier();
        int bab = baseAttackBonusOverride ?? _character.Stats.BaseAttackBonus;
        return bab
            + _character.Stats.STRMod
            + sizeMod
            + (_character.Stats.HasFeat("Improved Grapple") ? 4 : 0)
            + _character.GetImprovedGrabHoldGrappleCheckPenalty();
    }
}
