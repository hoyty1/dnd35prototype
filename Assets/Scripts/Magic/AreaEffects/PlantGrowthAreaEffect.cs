using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Persistent Plant Growth area effect (PHB p.262).
/// Overgrowth version: all normal vegetation in 100-ft radius becomes
/// thick and overgrown. Movement is quartered (×4 movement cost).
/// Duration: Instantaneous (persists for rest of combat).
/// No save, no SR.
/// </summary>
public class PlantGrowthAreaEffect : PersistentAreaEffect
{
    // Deep green for overgrown vegetation
    protected override Color GridHighlightColor => new Color(0.2f, 0.5f, 0.15f, 0.50f);
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Plant Growth";
        SpellId = SpellNames.PLANT_GROWTH;
        Shape = AreaShape.Circle;
        Radius = 20f; // 100-ft radius = 20 squares
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, true);
        LogEffect($"Vegetation grows thick and overgrown in 100-ft radius! Movement quartered (×4 cost). No save, no SR.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character != null && character.Stats != null && !character.Stats.IsDead)
        {
            string timing = isInitial ? "is standing in" : "enters";
            LogEffect($"🌿 {character.Stats.CharacterName} {timing} overgrown vegetation (movement quartered).");
        }
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        // No per-round effect; movement penalty is passive via difficult terrain
    }

    protected override void OnAreaExpires()
    {
        // Instantaneous effect — vegetation stays. But clean up terrain markers if combat ends.
        gameManager?.SetAreaDifficultTerrain(AffectedCells, false);
        LogEffect("The overgrown vegetation thins back to normal.");
    }

    /// <summary>
    /// Returns the movement cost multiplier for characters in Plant Growth areas.
    /// Plant Growth quadruples movement cost (PHB p.262).
    /// </summary>
    public static float GetMovementMultiplierFor(CharacterController character)
    {
        if (character == null) return 1f;

        var manager = AreaEffectManager.Instance;
        if (manager == null) return 1f;

        var effects = manager.GetEffectsOfType<PlantGrowthAreaEffect>();
        if (effects == null) return 1f;

        foreach (var effect in effects)
        {
            if (effect != null && effect.AffectedCells != null && effect.AffectedCells.Contains(character.GridPosition))
            {
                return 0.25f; // Quarter speed = multiply cost by 4
            }
        }

        return 1f;
    }
}
