using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// D&D 3.5e Web: 20-ft radius spread that entangles creatures and creates difficult terrain.
/// Fire can ignite the web, dealing 2d4 fire damage and destroying the web in 1 round.
/// </summary>
public class WebAreaEffect : PersistentAreaEffect
{
    public const int EscapeDc = 20;
    public const int SectionHitPoints = 12;

    private readonly Dictionary<Vector2Int, int> _sectionHp = new Dictionary<Vector2Int, int>();
    private bool _isBurning;
    private int _burnRoundsRemaining;

    protected override Color GridHighlightColor => AreaEffectColors.Web;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Web";
        SpellId = SpellNames.WEB;
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius spread on 5-ft grid
        ShowVisual = false;
        SaveDC = Mathf.Max(1, SaveDC);
    }

    protected override void OnAreaCreated()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, true);

        _sectionHp.Clear();
        foreach (Vector2Int cell in AffectedCells)
            _sectionHp[cell] = SectionHitPoints;

        LogEffect($"Sticky webs fill a 20-ft radius spread ({AffectedCells.Count} sections, {SectionHitPoints} HP each section). Reflex DC {SaveDC} or become entangled.");
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        if (!isInitial)
            return;

        TryApplyInitialEntangle(character);
    }

    private void TryApplyInitialEntangle(CharacterController character)
    {
        if (character.HasCondition(CombatConditionType.Entangled) && gameManager != null && gameManager.IsEntangledByWeb(character))
            return;

        int roll = DiceRoller.D20();
        int reflex = character.Stats.ReflexSave;
        int total = roll + reflex;
        bool success = total >= SaveDC;

        LogEffect($"{character.Stats.CharacterName} Reflex d20({roll}) + {reflex} = {total} vs DC {SaveDC} {(success ? "SUCCESS" : "FAIL")}");

        if (success)
            return;

        if (gameManager != null)
        {
            gameManager.ApplyWebEntangledCondition(Caster, character, Mathf.Max(1, RoundsRemaining));
        }
        else
        {
            character.ApplyCondition(CombatConditionType.Entangled, Mathf.Max(1, RoundsRemaining), "Web");
        }

        LogEffect($"🕸 {character.Stats.CharacterName} is entangled by webbing and cannot move until escaping (Str/Escape Artist DC {EscapeDc}).");
    }

    public bool IsBurning => _isBurning;

    public void Ignite(string sourceName)
    {
        if (_isBurning)
            return;

        _isBurning = true;
        _burnRoundsRemaining = 1;
        LogEffect($"🔥 Web ignites from {sourceName}. It burns away in 1 round.");

        ApplyBurnDamageToOccupants();
    }

    private void ApplyBurnDamageToOccupants()
    {
        var packet = new DamagePacket
        {
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Web Flames",
            IsRanged = false,
            IsNonlethal = false
        };

        int affectedCount = 0;
        foreach (CharacterController character in new List<CharacterController>(CharactersInArea))
        {
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            int damage = DiceRoller.D4() + DiceRoller.D4(); // 2d4 fire
            DamageResolutionResult mitigation = character.Stats.ApplyIncomingDamage(damage, packet);
            int applied = mitigation != null ? mitigation.FinalDamage : 0;
            affectedCount++;

            LogEffect($"🔥 {character.Stats.CharacterName} takes {applied} fire damage from burning web (2d4 rolled {damage}).");
        }

        if (affectedCount == 0)
            LogEffect("No creatures are caught in the flames.");
    }

    public bool TryDamageSection(Vector2Int cell, int damage, string sourceLabel)
    {
        if (damage <= 0 || !_sectionHp.TryGetValue(cell, out int hp))
            return false;

        int nextHp = Mathf.Max(0, hp - damage);
        _sectionHp[cell] = nextHp;

        LogEffect($"{sourceLabel} damages web section ({cell.x},{cell.y}) for {damage}. HP: {hp} → {nextHp}.");
        return true;
    }

    public override void OnRoundStart()
    {
        if (_isBurning)
        {
            _burnRoundsRemaining--;
            if (_burnRoundsRemaining <= 0)
            {
                LogEffect("🔥 Burning web is consumed.");
                ExpireEffect();
                return;
            }
        }

        base.OnRoundStart();
    }

    protected override void OnAreaExpires()
    {
        gameManager?.SetAreaDifficultTerrain(AffectedCells, false);
        if (gameManager != null)
            gameManager.RemoveWebEntangledConditionsFromArea(this);

        LogEffect("Web dissipates.");
    }
}
