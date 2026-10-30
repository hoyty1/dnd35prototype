using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enumerates actions that can provoke attacks of opportunity. Using a wand is not one: a spell trigger item's
/// activation does not provoke (DMG p.213, p.245); retrieving the wand does (RetrieveItem; batch 13 step 14, D7).
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
    SheatheWeapon,   // PHB p.142, Table 8-2: sheathing a weapon is a move action that provokes (batch 13 step 15, D8)
    PickUpItem       // PHB p.142, Table 8-2: picking up an item is a move action that provokes (batch 13 step 15, D8)
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
