using UnityEngine;

/// <summary>
/// Example persistent area effect implementation for future spells.
/// Not currently wired into casting flow.
/// </summary>
public class WallOfFireAreaEffect : PersistentAreaEffect
{
    protected override Color GridHighlightColor => AreaEffectColors.WallOfFire;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Wall of Fire";
        SpellId = "wall_of_fire";

        Shape = AreaShape.Rectangle;
        SizeX = 4; // 20 ft long
        SizeY = 1; // 5 ft wide
        DirectionAngle = 0f; // Horizontal by default

        ShowVisual = false;
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        DealFireDamage(character, isInitial ? "is caught in" : "enters");
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        DealFireDamage(character, "remains in");
    }

    private void DealFireDamage(CharacterController character, string context)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        int roll = Random.Range(1, 21);
        int reflex = character.Stats.ReflexSave;
        int total = roll + reflex;

        int damage = Random.Range(1, 7) + Random.Range(1, 7); // 2d6 sample
        bool saveSucceeded = total >= SaveDC;
        if (saveSucceeded)
            damage /= 2;

        character.Stats.TakeDamage(Mathf.Max(0, damage));
        LogEffect($"{character.Stats.CharacterName} {context} the flames: Reflex {total} vs DC {SaveDC} => {(saveSucceeded ? "half" : "full")} {damage} fire damage.");
    }
}
