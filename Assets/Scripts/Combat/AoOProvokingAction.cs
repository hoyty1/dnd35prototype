using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enumerates actions that can provoke attacks of opportunity.
/// </summary>
public enum AoOProvokingAction
{
    Movement,
    CastSpell,
    StandFromProne,
    DrinkPotion,
    UseScroll,
    RetrieveItem,
    LoadCrossbow,
    RangedAttackInMelee,
    UseWand
}

/// <summary>
/// Payload used by the unified AoO confirmation prompt.
/// </summary>
[Serializable]
public class AoOProvokingActionInfo
{
    public AoOProvokingAction ActionType;
    public string ActionName;
    public string ActionDescription;
    public CharacterController Actor;
    public List<CharacterController> ThreateningEnemies = new List<CharacterController>();

    // Optional spellcasting context
    public SpellData Spell;
    public int CastDefensivelyDC;
    public int ConcentrationBonus;
    public float SuccessChance;

    // Callbacks
    public Action OnProceed;
    public Action OnCastDefensively;
    public Action OnCancel;
}
