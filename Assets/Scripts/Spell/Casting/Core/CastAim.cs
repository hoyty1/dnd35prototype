using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where one cast is aimed (batch 11, cast_core_batch11.md): its target creature, or its area's squares, the creatures
/// in it (after line-of-effect filtering) and its origin. Today only the NPC request records its target; the PC
/// pipelines still carry their aim in their own arguments until the cast core takes them over (step 7 on).
/// </summary>
public sealed class CastAim
{
    public CharacterController Target;
    public HashSet<Vector2Int> Cells;
    public List<CharacterController> AreaTargets;
    public Vector2Int? Origin;
}
