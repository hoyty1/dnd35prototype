using UnityEngine;

/// <summary>
/// Persistent Grease area implementation using the reusable area-effect framework.
/// </summary>
public class GreaseAreaEffect : PersistentAreaEffect
{
    private const int BalanceDC = 10;

    protected override Color GridHighlightColor => AreaEffectColors.Grease;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Grease";
        SpellId = "grease";
        Shape = AreaShape.Square;
        SizeX = 2; // 10-ft square on 5-ft grid
        SizeY = 2;
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, true);
        LogEffect($"10-ft square becomes slippery (Balance DC {BalanceDC} to keep footing while moving). Reflex DC {SaveDC} on entry.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        int roll = Random.Range(1, 21);
        int reflex = character.Stats.ReflexSave;
        int total = roll + reflex;
        bool success = total >= SaveDC;

        string timing = isInitial ? "is in" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} grease: Reflex d20({roll}) + {reflex} = {total} vs DC {SaveDC} {(success ? "SUCCESS" : "FAIL")}");

        if (!success)
        {
            character.ApplyCondition(CombatConditionType.Prone, -1, "Grease");
            LogEffect($"💥 {character.Stats.CharacterName} falls prone.");
        }
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        int balanceTotal = character.Stats.RollSkillCheck("Balance");
        bool success = balanceTotal >= BalanceDC;
        LogEffect($"Round start check: {character.Stats.CharacterName} Balance {balanceTotal} vs DC {BalanceDC} {(success ? "SUCCESS" : "FAIL")}");

        if (!success)
        {
            character.ApplyCondition(CombatConditionType.Prone, -1, "Grease");
            LogEffect($"💥 {character.Stats.CharacterName} loses footing and falls prone.");
        }
    }

    protected override void OnAreaExpires()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, false);
        LogEffect("Grease dissipates.");
    }
}
