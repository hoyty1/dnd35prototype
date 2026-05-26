using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// AlignmentDetectionEffectData.cs — Runtime data for Detect Alignment / Undead
// spells (Detect Chaos, Detect Evil, Detect Good, Detect Law, Detect Undead).
// D&D 3.5e PHB p.218-220.
// ============================================================================

/// <summary>
/// Enumerates the type of detection active (alignment axis or undead).
/// </summary>
public enum DetectionType
{
    Chaos,
    Evil,
    Good,
    Law,
    Undead
}

/// <summary>
/// Info about a single detected creature, per D&D 3.5e rules.
/// Aura strength is based on creature HD (or undead HD).
/// </summary>
[System.Serializable]
public class DetectedCreatureInfo
{
    public CharacterController Creature;
    public string CreatureName;
    public string AuraStrength; // "Faint", "Moderate", "Strong", "Overwhelming"
    public int HitDice;
    public Vector2Int GridPosition;

    /// <summary>
    /// Calculate aura strength from HD per PHB p.218 table.
    /// For alignment auras on creatures:
    ///   HD 1       = Faint
    ///   HD 2-4     = Faint
    ///   HD 5-10    = Moderate
    ///   HD 11-25   = Strong
    ///   HD 26+     = Overwhelming
    /// Undead uses same scale but is always detectable regardless of alignment.
    /// </summary>
    public static string CalculateAuraStrength(int hitDice)
    {
        if (hitDice <= 0) return "Dim";
        if (hitDice <= 4) return "Faint";
        if (hitDice <= 10) return "Moderate";
        if (hitDice <= 25) return "Strong";
        return "Overwhelming";
    }
}

/// <summary>
/// Runtime metadata for an active Detect Alignment/Undead effect.
/// Tracks the detection type, detected creatures, concentration rounds,
/// and caster attribution.
/// </summary>
[System.Serializable]
public class AlignmentDetectionEffectData
{
    /// <summary>What type of detection is active.</summary>
    public DetectionType Type;

    /// <summary>The spell ID that created this detection effect.</summary>
    public string SourceSpellId;

    /// <summary>Display name of the spell.</summary>
    public string SpellDisplayName;

    /// <summary>Duration remaining in rounds.</summary>
    public int DurationRemainingRounds;

    /// <summary>
    /// Number of consecutive rounds the caster has been concentrating.
    /// Per PHB: Round 1 = presence/absence, Round 2 = number + strongest,
    /// Round 3+ = location + strength of each.
    /// </summary>
    public int ConcentrationRounds;

    /// <summary>List of creatures currently detected.</summary>
    public List<DetectedCreatureInfo> DetectedCreatures = new List<DetectedCreatureInfo>();

    /// <summary>Caster reference.</summary>
    [System.NonSerialized] public CharacterController Caster;
    public string CasterName;

    /// <summary>Color used for visual highlighting of detected creatures.</summary>
    public Color HighlightColor;

    /// <summary>Short label for the status indicator (e.g., "DC", "DE", "DG", "DL", "DU").</summary>
    public string StatusLabel;

    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }

    /// <summary>
    /// Check whether a creature matches this detection type.
    /// </summary>
    public static bool CreatureMatchesDetection(CharacterController creature, DetectionType type)
    {
        if (creature == null || creature.Stats == null) return false;

        switch (type)
        {
            case DetectionType.Chaos:
                return AlignmentHelper.IsChaotic(creature.Stats.CharacterAlignment);
            case DetectionType.Evil:
                return AlignmentHelper.IsEvil(creature.Stats.CharacterAlignment);
            case DetectionType.Good:
                return AlignmentHelper.IsGood(creature.Stats.CharacterAlignment);
            case DetectionType.Law:
                return AlignmentHelper.IsLawful(creature.Stats.CharacterAlignment);
            case DetectionType.Undead:
                string ct = creature.Stats.CreatureType;
                return !string.IsNullOrWhiteSpace(ct)
                    && ct.Trim().Equals("Undead", System.StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
    }

    /// <summary>
    /// Scan all characters within 60 ft (12 squares) of the caster and build DetectedCreatures list.
    /// Uses Chebyshev distance for the "cone" (simplified as range in tactical combat).
    /// </summary>
    public void ScanForCreatures(List<CharacterController> allCharacters)
    {
        DetectedCreatures.Clear();

        if (Caster == null || Caster.Stats == null || Caster == null)
            return;

        Vector2Int casterPos = Caster.GridPosition;
        const int detectionRangeSquares = 12; // 60 ft = 12 squares at 5ft/sq

        foreach (var creature in allCharacters)
        {
            if (creature == null || creature.Stats == null || creature == Caster)
                continue;

            if (creature.Stats.CurrentHP <= 0)
                continue;

            if (creature == null)
                continue;

            Vector2Int pos = creature.GridPosition;
            int dist = Mathf.Max(Mathf.Abs(pos.x - casterPos.x), Mathf.Abs(pos.y - casterPos.y));

            if (dist > detectionRangeSquares)
                continue;

            if (!CreatureMatchesDetection(creature, Type))
                continue;

            int hd = Mathf.Max(1, creature.Stats.Level);
            DetectedCreatures.Add(new DetectedCreatureInfo
            {
                Creature = creature,
                CreatureName = creature.Stats.CharacterName,
                AuraStrength = DetectedCreatureInfo.CalculateAuraStrength(hd),
                HitDice = hd,
                GridPosition = pos
            });
        }
    }

    /// <summary>
    /// Build a summary string based on how many concentration rounds have passed.
    /// Per D&D 3.5e PHB:
    ///   Round 1: Presence or absence
    ///   Round 2: Number of auras and strength of strongest
    ///   Round 3+: Location and strength of each aura
    /// </summary>
    public string GetDetectionSummary()
    {
        string typeLabel = Type == DetectionType.Undead ? "undead" : Type.ToString().ToLower();
        int count = DetectedCreatures.Count;

        if (count == 0)
            return $"No {typeLabel} auras detected within 60 ft.";

        if (ConcentrationRounds <= 1)
        {
            return $"You sense the presence of {typeLabel} auras nearby.";
        }
        else if (ConcentrationRounds == 2)
        {
            // Find strongest aura
            string strongest = "Faint";
            foreach (var dc in DetectedCreatures)
            {
                if (dc.AuraStrength == "Overwhelming") { strongest = "Overwhelming"; break; }
                if (dc.AuraStrength == "Strong" && strongest != "Overwhelming") strongest = "Strong";
                if (dc.AuraStrength == "Moderate" && strongest != "Strong" && strongest != "Overwhelming") strongest = "Moderate";
            }
            return $"{count} {typeLabel} aura(s) detected. Strongest: {strongest}.";
        }
        else
        {
            // Full detail
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{count} {typeLabel} aura(s) detected:");
            foreach (var dc in DetectedCreatures)
            {
                sb.AppendLine($"  • {dc.CreatureName}: {dc.AuraStrength} aura (HD {dc.HitDice})");
            }
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Create detection data from a spell ID.
    /// </summary>
    public static AlignmentDetectionEffectData CreateFromSpell(string spellId, CharacterController caster)
    {
        var data = new AlignmentDetectionEffectData();
        data.SetCaster(caster);
        data.SourceSpellId = spellId;

        switch (spellId)
        {
            case SpellNames.DETECT_CHAOS:
                data.Type = DetectionType.Chaos;
                data.SpellDisplayName = "Detect Chaos";
                data.StatusLabel = "DC";
                data.HighlightColor = new Color(0.6f, 0.2f, 0.85f, 0.85f); // Purple for chaos
                break;
            case SpellNames.DETECT_EVIL:
                data.Type = DetectionType.Evil;
                data.SpellDisplayName = "Detect Evil";
                data.StatusLabel = "DE";
                data.HighlightColor = new Color(0.85f, 0.15f, 0.15f, 0.85f); // Red for evil
                break;
            case SpellNames.DETECT_GOOD:
                data.Type = DetectionType.Good;
                data.SpellDisplayName = "Detect Good";
                data.StatusLabel = "DG";
                data.HighlightColor = new Color(1f, 0.85f, 0.2f, 0.85f); // Gold for good
                break;
            case SpellNames.DETECT_LAW:
                data.Type = DetectionType.Law;
                data.SpellDisplayName = "Detect Law";
                data.StatusLabel = "DL";
                data.HighlightColor = new Color(0.2f, 0.5f, 0.9f, 0.85f); // Blue for law
                break;
            case SpellNames.DETECT_UNDEAD:
                data.Type = DetectionType.Undead;
                data.SpellDisplayName = "Detect Undead";
                data.StatusLabel = "DU";
                data.HighlightColor = new Color(0.4f, 0.9f, 0.3f, 0.85f); // Sickly green for undead
                break;
            default:
                Debug.LogWarning($"[AlignmentDetection] Unknown detection spell: {spellId}");
                data.Type = DetectionType.Evil;
                data.SpellDisplayName = "Detect Evil";
                data.StatusLabel = "D?";
                data.HighlightColor = Color.white;
                break;
        }

        return data;
    }

    /// <summary>
    /// Helper to check if a spellId is one of the alignment/undead detection spells.
    /// </summary>
    public static bool IsDetectionSpell(string spellId)
    {
        return spellId == SpellNames.DETECT_CHAOS
            || spellId == SpellNames.DETECT_EVIL
            || spellId == SpellNames.DETECT_GOOD
            || spellId == SpellNames.DETECT_LAW
            || spellId == SpellNames.DETECT_UNDEAD;
    }
}
