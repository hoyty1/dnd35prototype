using UnityEngine;

/// <summary>
/// Fireball spell area effect example.
/// Uses a circular radius and deals damage to all creatures in the burst.
/// </summary>
public class FireballAreaEffect : PersistentAreaEffect
{
    protected override void Awake()
    {
        base.Awake();

        EffectName = "Fireball";
        SpellId = "fireball";
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius (4 squares)

        VisualColor = new Color(1f, 0.4f, 0f, 0.6f);
        VisualHeight = 0.03f;
        ShowVisual = true;
        RoundsRemaining = 1;
    }

    protected override void Start()
    {
        base.Start();

        // Fireball is instantaneous; remove the area object after applying initial effect.
        if (this != null && gameObject != null)
            ExpireEffect();
    }

    protected override void OnAreaCreated()
    {
        LogEffect($"Fireball explodes in a {Radius * 5f:0.#}-ft radius! Reflex DC {SaveDC} for half damage.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        // Fireball damage is applied on initial burst only.
        if (!isInitial)
            return;

        DealFireballDamage(character);
    }

    private void DealFireballDamage(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        int diceCount = Mathf.Clamp(CasterLevel, 1, 10);
        int damage = 0;

        for (int i = 0; i < diceCount; i++)
            damage += Random.Range(1, 7); // 1d6

        int roll = Random.Range(1, 21);
        int reflexSave = character.Stats.ReflexSave;
        int total = roll + reflexSave;

        bool saveSucceeded = total >= SaveDC;
        if (saveSucceeded)
            damage /= 2;

        damage = Mathf.Max(0, damage);
        character.Stats.TakeDamage(damage);

        LogEffect($"{character.Stats.CharacterName} Reflex: d20({roll}) + {reflexSave} = {total} vs DC {SaveDC} -> {(saveSucceeded ? "half" : "full")} {damage} fire damage.");
    }
}
