using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Resolves Gust of Wind (PHB 3.5e) creature interactions and registers temporary severe wind.
/// </summary>
public class GustOfWindEffect
{
    private CharacterController caster;
    private SpellData spell;
    private HashSet<Vector2Int> affectedCells;
    private List<CharacterController> targets;
    private int saveDC;
    private int casterLevel;
    private Vector2Int windDirection = Vector2Int.right;

    public void Initialize(
        CharacterController caster,
        SpellData spell,
        HashSet<Vector2Int> affectedCells,
        List<CharacterController> targets,
        int saveDC,
        int casterLevel)
    {
        this.caster = caster;
        this.spell = spell;
        this.affectedCells = affectedCells != null ? new HashSet<Vector2Int>(affectedCells) : new HashSet<Vector2Int>();
        this.targets = targets ?? new List<CharacterController>();
        this.saveDC = saveDC;
        this.casterLevel = Mathf.Max(1, casterLevel);
        windDirection = DetermineWindDirection();
    }

    public string ResolveEffect()
    {
        var log = new StringBuilder();
        log.AppendLine("  A powerful wind blasts in a line!");

        RegisterWindZone();
        SpawnWindLineVisual();

        if (targets.Count == 0)
        {
            log.AppendLine("  No creatures are caught in the gust.");
            log.AppendLine("  Unprotected flames in the line are extinguished; protected flames have a 50% chance to go out.");
            log.AppendLine("  Fog/cloud/gas effects in the path are dispersed by severe wind.");
            return log.ToString();
        }

        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController target = targets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;

            string targetName = target.Stats.CharacterName;
            log.AppendLine($"  → {targetName}");

            if (spell != null && spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                int srRoll = UnityEngine.Random.Range(1, 21);
                int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                bool overcameSR = srTotal >= target.Stats.SpellResistance;
                log.AppendLine($"    SR check: d20({srRoll}) + CL {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} {(overcameSR ? "PASS" : "BLOCKED")}");
                if (!overcameSR)
                    continue;
            }

            int saveRoll = UnityEngine.Random.Range(1, 21);
            int saveTotal = saveRoll + target.Stats.FortitudeSave;
            bool saveSucceeded = saveTotal >= saveDC;
            log.AppendLine($"    Fortitude save: d20({saveRoll}) + {target.Stats.FortitudeSave} = {saveTotal} vs DC {saveDC} {(saveSucceeded ? "SUCCESS" : "FAIL")}");

            if (saveSucceeded)
            {
                log.AppendLine("    Holds position against the gust.");
                continue;
            }

            ApplyFailedSaveOutcome(target, log);
        }

        log.AppendLine("  Unprotected flames in the line are extinguished; protected flames have a 50% chance to go out.");
        log.AppendLine("  Fog/cloud/gas effects in the path are dispersed by severe wind.");
        return log.ToString();
    }

    private void RegisterWindZone()
    {
        if (affectedCells == null || affectedCells.Count == 0)
            return;

        Vector3 direction = new Vector3(windDirection.x, windDirection.y, 0f).normalized;
        var wind = new WindEffect
        {
            EffectName = "Gust of Wind",
            Caster = caster,
            OriginPosition = caster != null ? SquareGridUtils.GridToWorld(caster.GridPosition) : Vector3.zero,
            Direction = direction,
            Length = 12f,
            AffectedRadius = 0f,
            Strength = WindStrength.Severe,
            RoundsRemaining = 1,
            SaveDC = saveDC,
            AffectedCells = new HashSet<Vector2Int>(affectedCells)
        };

        WindEffectManager.Instance.RegisterWindEffect(wind);
    }

    private static Sprite _gustSprite;

    private void SpawnWindLineVisual()
    {
        if (caster == null || affectedCells == null || affectedCells.Count == 0)
            return;

        Vector3 dir = new Vector3(windDirection.x, windDirection.y, 0f);
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector3.right;
        dir.Normalize();

        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float zRotation = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        var orderedCells = new List<Vector2Int>(affectedCells);
        Vector2 origin = caster.GridPosition;
        orderedCells.Sort((a, b) => Vector2.Dot(((Vector2)a - origin), (Vector2)windDirection)
            .CompareTo(Vector2.Dot(((Vector2)b - origin), (Vector2)windDirection)));

        GameObject vfxRoot = new GameObject("GustOfWind_VFX");
        Sprite sprite = GetGustSprite();

        for (int i = 0; i < orderedCells.Count; i++)
        {
            Vector3 world = SquareGridUtils.GridToWorld(orderedCells[i]);
            float t = orderedCells.Count <= 1 ? 1f : (i / (float)(orderedCells.Count - 1));
            float alpha = Mathf.Lerp(0.78f, 0.22f, t);

            CreateWindStreak(vfxRoot.transform, world, zRotation, sprite, new Color(0.68f, 0.92f, 1f, alpha), Vector3.one);
            CreateWindStreak(vfxRoot.transform, world + (perp * 0.16f), zRotation, sprite, new Color(0.55f, 0.85f, 1f, alpha * 0.7f), new Vector3(0.78f, 0.78f, 1f));
            CreateWindStreak(vfxRoot.transform, world - (perp * 0.16f), zRotation, sprite, new Color(0.55f, 0.85f, 1f, alpha * 0.7f), new Vector3(0.78f, 0.78f, 1f));
        }

        UnityEngine.Object.Destroy(vfxRoot, 0.5f);
    }

    private static void CreateWindStreak(Transform parent, Vector3 position, float zRotation, Sprite sprite, Color color, Vector3 scale)
    {
        GameObject streak = new GameObject("GustStreak");
        streak.transform.SetParent(parent, worldPositionStays: false);
        streak.transform.position = position;
        streak.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        streak.transform.localScale = new Vector3(0.92f * scale.x, 0.34f * scale.y, 1f);

        SpriteRenderer renderer = streak.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 12;
    }

    private static Sprite GetGustSprite()
    {
        if (_gustSprite != null)
            return _gustSprite;

        Texture2D tex = new Texture2D(24, 24, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2(11.5f, 11.5f);
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                float nx = (x - center.x) / center.x;
                float ny = (y - center.y) / center.y;
                float radial = Mathf.Clamp01(1f - Mathf.Sqrt((nx * nx) + (ny * ny)));
                float horizontalBias = Mathf.Clamp01(1f - Mathf.Abs(ny));
                float alpha = Mathf.Pow(radial * horizontalBias, 1.25f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        _gustSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 24f);
        return _gustSprite;
    }

    private Vector2Int DetermineWindDirection()
    {
        if (caster == null || affectedCells == null || affectedCells.Count == 0)
            return Vector2Int.right;

        Vector2Int origin = caster.GridPosition;
        Vector2Int bestDelta = Vector2Int.zero;
        int bestDistance = -1;

        foreach (Vector2Int cell in affectedCells)
        {
            Vector2Int delta = cell - origin;
            int distance = SquareGridUtils.GetDistance(origin, cell);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestDelta = delta;
            }
        }

        if (bestDelta == Vector2Int.zero)
            return Vector2Int.right;

        bestDelta.x = Math.Sign(bestDelta.x);
        bestDelta.y = Math.Sign(bestDelta.y);
        if (bestDelta == Vector2Int.zero)
            bestDelta = Vector2Int.right;

        return bestDelta;
    }

    private void ApplyFailedSaveOutcome(CharacterController target, StringBuilder log)
    {
        if (target == null || target.Stats == null)
            return;

        bool flying = IsFlyingTarget(target);
        if (flying)
        {
            int pushDistanceFeet = (UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7)) * 10; // 2d6 x 10
            int requestedSquares = Mathf.Max(0, pushDistanceFeet / 5);
            int movedSquares = PushTargetAlongWind(target, requestedSquares);
            int movedFeet = movedSquares * 5;

            int nonlethalDamage = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7); // 2d6
            int appliedDamage = ApplySpellNonlethalDamage(target, nonlethalDamage, "Gust of Wind (flying)");

            log.AppendLine($"    Flying target is blown back {Mathf.Max(0, movedFeet)} ft (rolled {pushDistanceFeet} ft). Takes {appliedDamage} nonlethal damage.");
            return;
        }

        SizeCategory size = target.Stats.CurrentSizeCategory;

        if (size <= SizeCategory.Tiny)
        {
            target.ApplyCondition(CombatConditionType.Prone, 1, "Gust of Wind");

            int tenFootIncrements = UnityEngine.Random.Range(1, 5); // 1d4 x 10 ft
            int requestedSquares = tenFootIncrements * 2;
            int movedSquares = PushTargetAlongWind(target, requestedSquares);
            int movedTenFootIncrements = movedSquares / 2;

            int totalNonlethal = 0;
            for (int i = 0; i < movedTenFootIncrements; i++)
                totalNonlethal += UnityEngine.Random.Range(1, 5); // 1d4 per 10 ft actually moved

            int appliedDamage = ApplySpellNonlethalDamage(target, totalNonlethal, "Gust of Wind (tiny)");
            log.AppendLine($"    Tiny or smaller: knocked prone and rolled {movedSquares * 5} ft (rolled {tenFootIncrements * 10} ft). Takes {appliedDamage} nonlethal damage.");
            return;
        }

        if (size == SizeCategory.Small)
        {
            target.ApplyCondition(CombatConditionType.Prone, 1, "Gust of Wind");
            log.AppendLine("    Small target is knocked prone.");
            return;
        }

        if (size == SizeCategory.Medium)
        {
            target.ApplyCondition(CombatConditionType.Checked, 1, "Gust of Wind");
            log.AppendLine("    Medium target is checked by wind (movement blocked for 1 round).");
            return;
        }

        log.AppendLine("    Large or larger: unaffected by ground wind force.");
    }

    private bool IsFlyingTarget(CharacterController target)
    {
        if (target == null || target.Stats == null)
            return false;

        if (target.Tags != null)
        {
            if (target.Tags.HasTag("Fly") || target.Tags.HasTag("Flying") || target.Tags.HasTag("Airborne"))
                return true;
        }

        if (target.Stats.CreatureTags != null)
        {
            for (int i = 0; i < target.Stats.CreatureTags.Count; i++)
            {
                string tag = target.Stats.CreatureTags[i];
                if (string.Equals(tag, "Fly", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tag, "Flying", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tag, "Airborne", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private int PushTargetAlongWind(CharacterController target, int requestedSquares)
    {
        if (target == null || target.Stats == null)
            return 0;

        if (requestedSquares <= 0)
            return 0;

        GameManager gm = GameManager.Instance;
        SquareGrid grid = gm != null ? gm.Grid : null;
        if (grid == null)
            return 0;

        Vector2Int direction = windDirection;
        if (direction == Vector2Int.zero)
            direction = Vector2Int.right;

        Vector2Int destination = target.GridPosition;
        int movedSquares = 0;

        for (int i = 0; i < requestedSquares; i++)
        {
            Vector2Int next = destination + direction;
            SquareCell nextCell = grid.GetCell(next);
            if (nextCell == null || nextCell.IsOccupied)
                break;

            destination = next;
            movedSquares++;
        }

        if (movedSquares <= 0)
            return 0;

        SquareCell destinationCell = grid.GetCell(destination);
        if (destinationCell == null)
            return 0;

        target.MoveToCell(destinationCell, markAsMoved: false);
        return movedSquares;
    }

    private static int ApplySpellNonlethalDamage(CharacterController target, int amount, string sourceName)
    {
        if (target == null || target.Stats == null || amount <= 0)
            return 0;

        var packet = new DamagePacket
        {
            RawDamage = amount,
            Types = new HashSet<DamageType> { DamageType.Bludgeoning },
            AttackTags = DamageBypassTag.None,
            IsRanged = true,
            IsNonlethal = true,
            Source = AttackSource.Spell,
            SourceName = sourceName
        };

        DamageResolutionResult result = target.Stats.ApplyIncomingDamage(amount, packet);
        return result != null ? Mathf.Max(0, result.FinalDamage) : 0;
    }
}
