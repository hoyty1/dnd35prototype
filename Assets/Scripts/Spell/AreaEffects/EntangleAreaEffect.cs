using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Persistent Entangle area effect (PHB p.227).
/// 40-ft radius spread. Creatures entering or starting in the area must make
/// a Reflex save or become entangled (-2 attack, -4 Dex, can't move).
/// Break free: DC 20 Strength or Escape Artist check as move action.
/// Duration: 1 min/level.
/// </summary>
public class EntangleAreaEffect : PersistentAreaEffect
{
    private const int BreakFreeDC = 20;

    protected override Color GridHighlightColor => AreaEffectColors.Entangle;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Entangle";
        SpellId = SpellNames.ENTANGLE;
        Shape = AreaShape.Circle;
        Radius = 8f; // 40-ft radius = 8 squares
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, true);
        LogEffect($"40-ft radius becomes entangling undergrowth. Reflex DC {SaveDC} or entangled. Break free: DC {BreakFreeDC} Str/Escape Artist.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // The plants do not grasp a creature the zone does not hinder: an incorporeal creature (not subject to physical
        // actions that move or manipulate it, MM p.311; difficult terrain does not hamper it, PHB p.148) or one under
        // Freedom of Movement (PHB p.233). Delegated RAW reading 2026-10-29, docs/systems/RULES_COVERAGE.md 4.1;
        // MovementTerrain.ZoneHindersMovement, the exemption the charge planner asks too.
        if (!MovementTerrain.ZoneHindersMovement(character, this))
            return;

        SaveRoll save = RollSpellSave(character, SavingThrowType.Reflex, SaveDC); // SaveRollRules, D13
        bool saved = save.Succeeded;

        string timing = isInitial ? "is in" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} Entangle: Reflex d20({save.Roll})+{save.Modifier}={save.Total} vs DC {SaveDC} → {(saved ? "SAVED (half speed)" : "ENTANGLED")}{save.NaturalSuffix}");

        if (!saved)
        {
            // Apply Entangled condition: -2 attack, -4 Dex, can't move. The area renews it at the round boundary
            // (OnCreatureInAreaAtRoundStart), so it ticks there, not at the caster's or the mover's count (CMB-006).
            using (TurnDurations.AtRoundBoundary())
                character.ApplyCondition(CombatConditionType.Entangled, 1, "Entangle");
            LogEffect($"🌿 {character.Stats.CharacterName} is entangled! (-2 attack, -4 Dex, can't move — DC {BreakFreeDC} Str/Escape Artist to break free)");
        }
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        // Check if already entangled — allow break-free attempt
        if (character.HasCondition(CombatConditionType.Entangled))
        {
            // Break free attempt (can use Str or Escape Artist)
            int strCheck = DiceRoller.D20() + character.Stats.STRMod;
            bool breakFree = strCheck >= BreakFreeDC;

            LogEffect($"{character.Stats.CharacterName} tries to break free: Str d20+{character.Stats.STRMod}={strCheck} vs DC {BreakFreeDC} → {(breakFree ? "FREED" : "STILL ENTANGLED")}");

            if (breakFree)
            {
                character.RemoveCondition(CombatConditionType.Entangled);
                LogEffect($"🌿 {character.Stats.CharacterName} breaks free from the entangle!");
            }
        }
        else if (MovementTerrain.ZoneHindersMovement(character, this))
        {
            // Re-check entanglement for creatures still in area but not entangled (none for a creature the zone does not
            // hinder, as on entry)
            SaveRoll save = RollSpellSave(character, SavingThrowType.Reflex, SaveDC); // SaveRollRules, D13

            if (!save.Succeeded)
            {
                character.ApplyCondition(CombatConditionType.Entangled, 1, "Entangle");
                LogEffect($"🌿 {character.Stats.CharacterName} becomes entangled again! Reflex {save.Total} vs DC {SaveDC}{save.NaturalSuffix}");
            }
        }
    }

    protected override void OnAreaExpires()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, false);

        // Remove entangled conditions from remaining creatures
        foreach (var character in CharactersInArea)
        {
            if (character != null && character.HasCondition(CombatConditionType.Entangled))
            {
                character.RemoveCondition(CombatConditionType.Entangled);
            }
        }

        LogEffect("The entangling plants wither and release their grip.");
    }
}
