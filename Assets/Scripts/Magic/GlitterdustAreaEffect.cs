using UnityEngine;

/// <summary>
/// Persistent visual cloud for Glitterdust.
/// Mechanical effects are applied only at cast time; new entrants are unaffected.
/// </summary>
public class GlitterdustAreaEffect : PersistentAreaEffect
{
    protected override Color GridHighlightColor => AreaEffectColors.Glitterdust;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Glitterdust";
        SpellId = "glitterdust";
        Shape = AreaShape.Circle;
        Radius = 2f; // 10-ft radius spread
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        LogEffect("Golden dust sparkles in a 10-ft radius spread.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        // Glitterdust applies only at cast time.
    }

    protected override void OnAreaExpires()
    {
        RemoveGridHighlight();
        LogEffect("The glittering cloud fades.");
    }
}
