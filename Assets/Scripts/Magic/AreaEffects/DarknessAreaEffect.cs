using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Darkness (PHB 3.5e): 20-ft radius magical darkness.
/// - Darkness does NOT block line of sight or targeting.
/// - Attacks involving darkness squares have concealment (20% miss chance).
/// - Darkvision/low-light do not negate this spell's concealment.
/// Visual uses black semi-transparent grid-cell shading.
/// </summary>
public class DarknessAreaEffect : PersistentAreaEffect
{
    private const string ConcealmentSpellId = "darkness_concealment";

    // Cache all currently darkened cells so concealment/path checks do not iterate every area every frame.
    private static readonly HashSet<Vector2Int> CachedDarknessCells = new HashSet<Vector2Int>();
    private static bool DarknessCellCacheDirty = true;

    protected override Color GridHighlightColor => new Color(0f, 0f, 0f, 0.72f);
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Darkness";
        SpellId = SpellNames.DARKNESS;
        Shape = AreaShape.Circle;
        Radius = 4f; // 20-ft radius

        // Use per-cell black overlay instead of a world mesh.
        ShowVisual = false;
    }

    protected override void OnAreaCreated()
    {
        MarkVisionCacheDirty();
        LogEffect("20-ft radius fills with magical darkness.");
        LogEffect("Darkness does not block vision or targeting.");
        LogEffect("Attacks involving darkness squares have concealment (20% miss chance).");
    }

    private void Update()
    {
        UpdateCharacterTracking();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    public override void OnRoundStart()
    {
        base.OnRoundStart();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        string timing = isInitial ? "is in" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} magical darkness (20% miss chance).");
        ApplyConcealment(character);
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        ApplyConcealment(character);
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        RemoveConcealment(character);
        LogEffect($"{character.Stats.CharacterName} leaves magical darkness.");
    }

    protected override void OnAreaExpires()
    {
        MarkVisionCacheDirty();

        foreach (CharacterController character in CharactersInArea)
        {
            if (character != null)
                RemoveConcealment(character);
        }

        RemoveGridHighlight();
        LogEffect("Darkness spell expires.");
        LogEffect("Magical darkness dissipates.");

        if (gameManager != null)
            gameManager.StartCoroutine(ReapplyDarknessHighlightsNextFrame());
    }

    protected override void OnDestroy()
    {
        MarkVisionCacheDirty();
        base.OnDestroy();
    }

    private IEnumerator ReapplyDarknessHighlightsNextFrame()
    {
        yield return null;

        if (!AreaEffectManager.HasInstance)
            yield break;

        List<DarknessAreaEffect> darknessAreas = AreaEffectManager.Instance.GetEffectsOfType<DarknessAreaEffect>();
        for (int i = 0; i < darknessAreas.Count; i++)
        {
            DarknessAreaEffect other = darknessAreas[i];
            if (other == null)
                continue;

            other.ApplyGridHighlight();
        }
    }

    private void ApplyConcealment(CharacterController character)
    {
        StatusEffectManager statusMgr = character.StatusEffectManager;
        if (statusMgr == null)
            statusMgr = character.gameObject.AddComponent<StatusEffectManager>();

        statusMgr.Init(character.Stats);

        for (int i = 0; i < statusMgr.ActiveEffects.Count; i++)
        {
            ActiveSpellEffect existing = statusMgr.ActiveEffects[i];
            if (existing != null && existing.Spell != null && existing.Spell.SpellId == ConcealmentSpellId)
            {
                existing.RemainingRounds = 1;
                existing.MissChance = 20;
                existing.IsTotalConcealment = false;
                existing.ConcealmentSource = "Magical Darkness";
                existing.SourceAreaEffect = this;
                return;
            }
        }

        var effect = new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = ConcealmentSpellId, Name = "Darkness" },
            CasterName = Caster != null && Caster.Stats != null ? Caster.Stats.CharacterName : "Unknown",
            CasterLevel = Mathf.Max(1, CasterLevel),
            RemainingRounds = 1,
            DurationType = DurationType.Rounds,
            AffectedCharacterName = character.Stats.CharacterName,
            BonusTypeLegacy = "Concealment",
            BonusTypeEnum = BonusType.Concealment,
            IsApplied = true,
            MissChance = 20,
            IsTotalConcealment = false,
            ConcealmentSource = "Magical Darkness",
            SourceAreaEffect = this
        };

        statusMgr.ActiveEffects.Add(effect);
    }

    public int GetConcealmentMissChance(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
            return 0;

        bool attackerInDarkness = IsCharacterInArea(attacker);
        bool targetInDarkness = IsCharacterInArea(target);
        bool pathCrossesDarkness = false;

        if (!attackerInDarkness && !targetInDarkness)
        {
            foreach (Vector2Int cell in EnumerateLineCells(attacker.GridPosition, target.GridPosition))
            {
                if (cell == attacker.GridPosition || cell == target.GridPosition)
                    continue;

                if (AffectedCells != null && AffectedCells.Contains(cell))
                {
                    pathCrossesDarkness = true;
                    break;
                }
            }
        }

        return (attackerInDarkness || targetInDarkness || pathCrossesDarkness) ? 20 : 0;
    }

    private void RemoveConcealment(CharacterController character)
    {
        if (character == null)
            return;

        var darknessAreas = AreaEffectManager.Instance.GetEffectsOfType<DarknessAreaEffect>();
        for (int i = 0; i < darknessAreas.Count; i++)
        {
            DarknessAreaEffect other = darknessAreas[i];
            if (other == null || other == this)
                continue;

            if (other.IsCharacterInArea(character))
                return;
        }

        StatusEffectManager statusMgr = character.StatusEffectManager;
        if (statusMgr == null || statusMgr.ActiveEffects == null || statusMgr.ActiveEffects.Count == 0)
            return;

        for (int i = statusMgr.ActiveEffects.Count - 1; i >= 0; i--)
        {
            ActiveSpellEffect effect = statusMgr.ActiveEffects[i];
            if (effect != null && effect.Spell != null && effect.Spell.SpellId == ConcealmentSpellId)
                statusMgr.RemoveEffect(effect);
        }
    }

    // Darkness provides concealment but never blocks line of sight.
    public static bool BlocksVision(CharacterController observer, CharacterController target)
    {
        return false;
    }

    public static int GetAttackConcealmentMissChance(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
            return 0;

        bool attackerInDarkness = IsPositionInMagicalDarkness(attacker.GridPosition);
        bool targetInDarkness = IsPositionInMagicalDarkness(target.GridPosition);
        bool pathCrossesDarkness = IsLineBlockedByMagicalDarkness(attacker.GridPosition, target.GridPosition, includeEndpoints: false);

        return (attackerInDarkness || targetInDarkness || pathCrossesDarkness) ? 20 : 0;
    }

    public static bool IsPositionInMagicalDarkness(Vector2Int position)
    {
        RebuildDarknessCellCacheIfNeeded();
        return CachedDarknessCells.Contains(position);
    }

    public static bool IsLineBlockedByMagicalDarkness(Vector2Int from, Vector2Int to, bool includeEndpoints = true)
    {
        RebuildDarknessCellCacheIfNeeded();
        if (CachedDarknessCells.Count == 0)
            return false;

        foreach (Vector2Int cell in EnumerateLineCells(from, to))
        {
            if (!includeEndpoints && (cell == from || cell == to))
                continue;

            if (CachedDarknessCells.Contains(cell))
                return true;
        }

        return false;
    }

    public static void InvalidateVisionCache()
    {
        DarknessCellCacheDirty = true;
    }

    private static void MarkVisionCacheDirty()
    {
        InvalidateVisionCache();
    }

    private static void RebuildDarknessCellCacheIfNeeded()
    {
        if (!DarknessCellCacheDirty)
            return;

        CachedDarknessCells.Clear();

        if (AreaEffectManager.HasInstance)
        {
            List<DarknessAreaEffect> darknessAreas = AreaEffectManager.Instance.GetEffectsOfType<DarknessAreaEffect>();
            if (darknessAreas != null)
            {
                for (int i = 0; i < darknessAreas.Count; i++)
                {
                    DarknessAreaEffect area = darknessAreas[i];
                    if (area == null || area.AffectedCells == null)
                        continue;

                    foreach (Vector2Int cell in area.AffectedCells)
                        CachedDarknessCells.Add(cell);
                }
            }
        }

        DarknessCellCacheDirty = false;
    }

    private static IEnumerable<Vector2Int> EnumerateLineCells(Vector2Int start, Vector2Int end)
    {
        int x0 = start.x;
        int y0 = start.y;
        int x1 = end.x;
        int y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            yield return new Vector2Int(x0, y0);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = err << 1;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
}
