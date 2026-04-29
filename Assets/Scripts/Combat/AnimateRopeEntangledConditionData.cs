using System;
using UnityEngine;

/// <summary>
/// Runtime metadata attached to an Entangled condition created by Animate Rope.
/// Tracks rope ownership/state so cleanup and escape actions can resolve correctly.
/// </summary>
[Serializable]
public sealed class AnimateRopeEntangledConditionData
{
    public CharacterController Caster;
    public CharacterController Target;
    public ItemData RopeItem;
    public int RopeBreakDC;
    public Vector2Int LastKnownTargetPosition;
    public bool RopeDestroyed;
    public bool RopeDroppedToGround;
    public bool Anchored;
    public string SourceSpellId = "animate_rope";
    public string SourceSpellName = "Animate Rope";
}