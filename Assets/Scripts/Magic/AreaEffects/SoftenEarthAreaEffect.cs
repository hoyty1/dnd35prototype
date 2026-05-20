using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Persistent Soften Earth and Stone area effect (PHB p.280).
/// Turns earth/stone to mud/sand — creates difficult terrain permanently (instantaneous).
/// No save, no SR. 10-ft square per level.
/// Since the effect is instantaneous, this area effect persists for the rest of combat.
/// </summary>
public class SoftenEarthAreaEffect : PersistentAreaEffect
{
    // Muddy brown color for softened earth
    protected override Color GridHighlightColor => new Color(0.55f, 0.40f, 0.25f, 0.55f);
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Soften Earth and Stone";
        SpellId = SpellNames.DOMAIN_SOFTEN_EARTH;
        Shape = AreaShape.Circle;
        Radius = 3f; // 15-ft radius = 3 squares
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, true);
        LogEffect($"Earth and stone soften into mud and sand — difficult terrain created. No save, no SR.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        // No entry effects beyond difficult terrain (movement cost doubled)
        if (character != null && character.Stats != null && !character.Stats.IsDead)
        {
            string timing = isInitial ? "is standing in" : "enters";
            LogEffect($"{character.Stats.CharacterName} {timing} softened earth (difficult terrain — double movement cost).");
        }
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        // No per-round effect; difficult terrain is passive
    }

    protected override void OnAreaExpires()
    {
        // Instantaneous effect — if it does expire, clean up terrain
        gameManager?.SetAreaDifficultTerrain(AffectedCells, false);
        LogEffect("The softened earth hardens.");
    }
}
