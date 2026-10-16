using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where one cast is aimed (batch 11, cast_core_batch11.md): its target creature, or its area's squares, the creatures
/// in it (after line-of-effect filtering) and its origin. A single-target cast records its target (PC since step 7, NPC
/// since step 8); an area cast (SpellCastRequest.ForUiArea, step 9) records the squares and the creatures, which the
/// cast core's Legality stage filters by line of effect from its point of origin (<see cref="AreaOrigin"/>). Since step 10 a summon records the
/// square it places its creature on (<see cref="Destination"/>), and Grease its area's squares and creatures or the
/// creature holding or wearing the object it greases (<see cref="ObjectHolder"/>).
/// </summary>
public sealed class CastAim
{
    public CharacterController Target;
    public HashSet<Vector2Int> Cells;
    public List<CharacterController> AreaTargets;

    /// <summary>
    /// The area's point of origin, a grid intersection (PHB p.175: a burst's or cube's point, a cone's or line's starting
    /// one, a self-centred area's chosen corner; a Grease square's centre), recorded by the builder that aimed it; the
    /// Legality stage traces line of effect from it (PHB p.176; batch 11 step 24, decision D5). Null for an area with no
    /// intersection origin (the walls, a one-square effect), whose line of effect runs from the caster's square.
    /// </summary>
    public GridIntersection? AreaOrigin;

    /// <summary>The square a Summon Monster or Summon Swarm cast places its creature on (SpellCastRequest.ForUiSummon).</summary>
    public Vector2Int? Destination;

    /// <summary>
    /// The creature holding or wearing the object an object-targeted cast aims at (Grease on a held object or worn armor,
    /// PHB p.237). The spell targets the object, not this creature, so it is not <see cref="Target"/>.
    /// </summary>
    public CharacterController ObjectHolder;
}
