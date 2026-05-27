using UnityEngine;
using DND35e.Identifiers;
using System.Collections.Generic;

/// <summary>
/// Persistent Spike Stones area effect (PHB p.283).
/// Rocky terrain sprouts sharp spikes. Creatures moving through the area
/// take 1d8 piercing damage per 5 ft of movement. Reflex DC 15 halves.
/// Reduces movement to half speed in the area.
/// Duration: 1 hour/level.
/// </summary>
public class SpikeStoneAreaEffect : PersistentAreaEffect
{
    private const int MovementDamageDie = 8;
    private const int ReflexDC = 15;

    // Gray-brown rocky color
    protected override Color GridHighlightColor => new Color(0.55f, 0.50f, 0.45f, 0.55f);
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Spike Stones";
        SpellId = SpellNames.SPIKE_STONES;
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius = 4 squares
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, true);
        LogEffect($"Sharp stone spikes erupt from the ground! 1d8 piercing per 5 ft moved, Reflex DC {ReflexDC} half. Half speed.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // Deal damage on entry (1d8 for moving into a square)
        if (!isInitial) // Only on movement, not initial placement
        {
            int damage = Random.Range(1, MovementDamageDie + 1);

            // Reflex save for half
            int reflexRoll = DiceRoller.D20();
            int reflexTotal = reflexRoll + character.Stats.ReflexSave;
            bool saved = reflexTotal >= ReflexDC;
            int actualDamage = saved ? Mathf.Max(1, damage / 2) : damage;

            int hpBefore = character.Stats.CurrentHP;
            character.Stats.CurrentHP -= actualDamage;
            int hpAfter = character.Stats.CurrentHP;

            string saveStr = saved ? $"Reflex {reflexTotal} vs DC {ReflexDC} SAVED (half)" : $"Reflex {reflexTotal} vs DC {ReflexDC} FAILED";
            LogEffect($"💎 {character.Stats.CharacterName} steps on spike stones: 1d8={damage} → {actualDamage} piercing ({saveStr}) [{hpBefore}→{hpAfter} HP]");

            if (character.Stats.IsDead)
            {
                character.OnDeath();
                LogEffect($"💀 {character.Stats.CharacterName} has been slain by spike stones!");
            }
        }
        else
        {
            LogEffect($"{character.Stats.CharacterName} is standing on spike stones (half speed, 1d8 per 5 ft moved).");
        }
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        // No per-round damage — damage is on movement only
    }

    protected override void OnAreaExpires()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, false);
        LogEffect("The spike stones recede back into the ground.");
    }
}
