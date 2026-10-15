using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where one cast is aimed (batch 11, cast_core_batch11.md): its target creature, or its area's squares, the creatures
/// in it (after line-of-effect filtering) and its origin. A single-target cast records its target (PC since step 7, NPC
/// since step 8); an area cast (SpellCastRequest.ForUiArea, step 9) records the squares and the creatures, which the
/// cast core's Legality stage filters by line of effect from the origin it records.
/// </summary>
public sealed class CastAim
{
    public CharacterController Target;
    public HashSet<Vector2Int> Cells;
    public List<CharacterController> AreaTargets;
    public Vector2Int? Origin;
}
