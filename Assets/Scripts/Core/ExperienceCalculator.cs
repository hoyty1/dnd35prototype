using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Calculates and awards D&D 3.5e combat XP (DMG Chapter 2 award table).
/// </summary>
public class ExperienceCalculator : MonoBehaviour
{
    private static ExperienceCalculator instance;

    public static ExperienceCalculator Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("ExperienceCalculator");
                instance = obj.AddComponent<ExperienceCalculator>();
                DontDestroyOnLoad(obj);
            }

            return instance;
        }
    }

    // DMG 3.5e XP award table: total XP for defeating one creature by (CR - APL) delta.
    private static readonly Dictionary<int, int> XPTable = new Dictionary<int, int>
    {
        { -8, 13 },
        { -7, 19 },
        { -6, 25 },
        { -5, 38 },
        { -4, 50 },
        { -3, 100 },
        { -2, 150 },
        { -1, 200 },
        { 0, 300 },
        { 1, 400 },
        { 2, 600 },
        { 3, 800 },
        { 4, 1200 },
        { 5, 1800 },
        { 6, 2400 },
        { 7, 3600 },
        { 8, 5400 },
        { 9, 8100 },
        { 10, 12150 },
        { 11, 18225 },
        { 12, 27337 }
    };

    [System.Serializable]
    public class XPAward
    {
        public CharacterController Enemy;
        public float ChallengeRating;
        public int XPTotal;
        public string EnemyName;

        public XPAward(CharacterController enemy, float challengeRating, int xpTotal)
        {
            Enemy = enemy;
            ChallengeRating = challengeRating;
            XPTotal = xpTotal;
            EnemyName = enemy != null && enemy.Stats != null && !string.IsNullOrWhiteSpace(enemy.Stats.CharacterName)
                ? enemy.Stats.CharacterName
                : "Unknown Enemy";
        }
    }

    public class CombatXPResult
    {
        public List<XPAward> Awards = new List<XPAward>();
        public int TotalXPPerCharacter;
        public float AveragePartyLevel;
        public Dictionary<CharacterController, int> CharacterXPGained = new Dictionary<CharacterController, int>();
        public Dictionary<CharacterController, bool> CharacterLeveledUp = new Dictionary<CharacterController, bool>();
    }

    public CombatXPResult CalculateXPForCombat(List<CharacterController> party, List<CharacterController> defeatedEnemies)
    {
        party = party ?? new List<CharacterController>();
        defeatedEnemies = defeatedEnemies ?? new List<CharacterController>();

        Debug.Log("[XP] Calculating experience for combat");
        Debug.Log($"[XP] Party size: {party.Count}");
        Debug.Log($"[XP] Enemies defeated: {defeatedEnemies.Count}");

        CombatXPResult result = new CombatXPResult();
        result.AveragePartyLevel = CalculateAPL(party);
        Debug.Log($"[XP] Average Party Level: {result.AveragePartyLevel:F1}");

        int totalEncounterXP = 0;

        for (int i = 0; i < defeatedEnemies.Count; i++)
        {
            CharacterController enemy = defeatedEnemies[i];
            if (enemy == null || enemy.Stats == null)
                continue;

            float cr = GetChallengeRating(enemy);
            int xpForThisEnemy = GetXPForCR(cr, result.AveragePartyLevel);
            totalEncounterXP += xpForThisEnemy;

            XPAward award = new XPAward(enemy, cr, xpForThisEnemy);
            result.Awards.Add(award);

            Debug.Log($"[XP] {award.EnemyName} (CR {ChallengeRatingUtils.Format(cr)}): {xpForThisEnemy} XP total");

            bool isXpTestEnemy = !string.IsNullOrWhiteSpace(award.EnemyName)
                && (award.EnemyName.Contains("Piñata")
                    || award.EnemyName.Contains("Pinata")
                    || award.EnemyName.Contains("Test"));
            if (isXpTestEnemy)
            {
                int partySizeForLog = Mathf.Max(1, party.Count);
                int xpPerCharacterForLog = xpForThisEnemy / partySizeForLog;
                int crDifferenceForLog = Mathf.RoundToInt(cr - result.AveragePartyLevel);

                Debug.Log("[XP TEST] =============================");
                Debug.Log($"[XP TEST] Enemy: {award.EnemyName}");
                Debug.Log($"[XP TEST] CR: {ChallengeRatingUtils.Format(cr)}");
                Debug.Log($"[XP TEST] APL: {result.AveragePartyLevel:F1}");
                Debug.Log($"[XP TEST] CR Difference: {(crDifferenceForLog >= 0 ? "+" : string.Empty)}{crDifferenceForLog}");
                Debug.Log($"[XP TEST] XP for this enemy: {xpForThisEnemy}");
                Debug.Log($"[XP TEST] Party size: {partySizeForLog}");
                Debug.Log($"[XP TEST] XP per character: {xpPerCharacterForLog}");
                Debug.Log("[XP TEST] =============================");
            }
        }

        Debug.Log($"[XP] Total encounter XP: {totalEncounterXP}");

        if (party.Count > 0)
        {
            result.TotalXPPerCharacter = totalEncounterXP / party.Count;
            Debug.Log($"[XP] XP per character: {totalEncounterXP} / {party.Count} = {result.TotalXPPerCharacter}");
        }
        else
        {
            result.TotalXPPerCharacter = 0;
            Debug.Log("[XP] XP per character: 0 (party size is 0)");
        }

        for (int i = 0; i < party.Count; i++)
        {
            CharacterController character = party[i];
            if (character == null || character.Stats == null)
                continue;

            int oldXp = character.Stats.ExperiencePoints;
            int oldLevel = Mathf.Max(1, character.Stats.Level);

            character.Stats.AddExperience(result.TotalXPPerCharacter);

            int newXp = character.Stats.ExperiencePoints;
            int newLevel = Mathf.Max(1, character.Stats.Level);
            bool leveledUp = newLevel > oldLevel;

            result.CharacterXPGained[character] = result.TotalXPPerCharacter;
            result.CharacterLeveledUp[character] = leveledUp;

            string charName = !string.IsNullOrWhiteSpace(character.Stats.CharacterName) ? character.Stats.CharacterName : "Unknown";
            Debug.Log($"[XP] {charName}: {oldXp} → {newXp} XP (Level {oldLevel} → {newLevel})");

            if (leveledUp)
                Debug.Log($"[XP] ⭐ {charName} leveled up to {newLevel}! ⭐");
        }

        return result;
    }

    private float CalculateAPL(List<CharacterController> party)
    {
        if (party == null || party.Count == 0)
            return 1f;

        int totalLevels = 0;
        int count = 0;

        for (int i = 0; i < party.Count; i++)
        {
            CharacterController character = party[i];
            if (character == null || character.Stats == null)
                continue;

            totalLevels += Mathf.Max(1, character.Stats.Level);
            count++;
        }

        if (count == 0)
            return 1f;

        float apl = totalLevels / (float)count;
        return Mathf.Round(apl * 2f) / 2f;
    }

    private float GetChallengeRating(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return 1f;

        if (!string.IsNullOrWhiteSpace(creature.Stats.ChallengeRating)
            && ChallengeRatingUtils.TryParse(creature.Stats.ChallengeRating, out float parsedCr)
            && parsedCr > 0f)
        {
            return parsedCr;
        }

        return Mathf.Max(1f, creature.Stats.Level);
    }

    private int GetXPForCR(float challengeRating, float averagePartyLevel)
    {
        float crDifference = challengeRating - averagePartyLevel;
        int crDiff = Mathf.RoundToInt(crDifference);
        crDiff = Mathf.Clamp(crDiff, -8, 12);

        if (XPTable.TryGetValue(crDiff, out int xp))
            return xp;

        Debug.LogWarning($"[XP] CR difference {crDiff} not in table; returning 0 XP.");
        return 0;
    }

    /// <summary>
    /// D&D 3.5e level threshold table formula.
    /// L1=0, L2=1000, L3=3000, L4=6000, ...
    /// XP(level n) = ((n-1) * n / 2) * 1000
    /// </summary>
    public static int GetXPForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        if (safeLevel <= 1)
            return 0;

        return ((safeLevel - 1) * safeLevel / 2) * 1000;
    }
}
