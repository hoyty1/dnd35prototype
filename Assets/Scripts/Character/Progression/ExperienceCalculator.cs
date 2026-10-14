using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Calculates and awards D&D 3.5e combat XP (DMG ch.2 "Experience Awards", p.36-39, Table 2-6 on p.38).
/// Every party member's award is looked up at that character's own level (CHR-004).
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

    // DMG Table 2-6 (p.38): base XP for one defeated monster, by one character's level (rows) and the monster's CR
    // (columns CR 1-20). Row 0 is the shared "1st-3rd" row, row n is level n + 3. A 0 marks a cell the table leaves
    // blank: "*" (the monster is 8 or more CRs below the level) or "**" (8 or more CRs above); see GetBaseXPAward.
    private static readonly int[][] AwardTable =
    {
        //              CR 1     2     3     4     5     6     7     8     9     10     11     12     13     14     15     16     17     18     19     20
        new[] { /*1-3*/ 300,  600,  900, 1350, 1800, 2700, 3600, 5400, 7200, 10800,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0 },
        new[] { /* 4*/  300,  600,  800, 1200, 1600, 2400, 3200, 4800, 6400,  9600, 12800,     0,     0,     0,     0,     0,     0,     0,     0,     0 },
        new[] { /* 5*/  300,  500,  750, 1000, 1500, 2250, 3000, 4500, 6000,  9000, 12000, 18000,     0,     0,     0,     0,     0,     0,     0,     0 },
        new[] { /* 6*/  300,  450,  600,  900, 1200, 1800, 2700, 3600, 5400,  7200, 10800, 14400, 21600,     0,     0,     0,     0,     0,     0,     0 },
        new[] { /* 7*/  263,  350,  525,  700, 1050, 1400, 2100, 3150, 4200,  6300,  8400, 12600, 16800, 25200,     0,     0,     0,     0,     0,     0 },
        new[] { /* 8*/  200,  300,  400,  600,  800, 1200, 1600, 2400, 3600,  4800,  7200,  9600, 14400, 19200, 28800,     0,     0,     0,     0,     0 },
        new[] { /* 9*/    0,  225,  338,  450,  675,  900, 1350, 1800, 2700,  4050,  5400,  8100, 10800, 16200, 21600, 32400,     0,     0,     0,     0 },
        new[] { /*10*/    0,    0,  250,  375,  500,  750, 1000, 1500, 2000,  3000,  4500,  6000,  9000, 12000, 18000, 24000, 36000,     0,     0,     0 },
        new[] { /*11*/    0,    0,    0,  275,  413,  550,  825, 1100, 1650,  2200,  3300,  4950,  6600,  9900, 13200, 19800, 26400, 39600,     0,     0 },
        new[] { /*12*/    0,    0,    0,    0,  300,  450,  600,  900, 1200,  1800,  2400,  3600,  5400,  7200, 10800, 14400, 21600, 28800, 43200,     0 },
        new[] { /*13*/    0,    0,    0,    0,    0,  325,  488,  650,  975,  1300,  1950,  2600,  3900,  5850,  7800, 11700, 15600, 23400, 31200, 46800 },
        new[] { /*14*/    0,    0,    0,    0,    0,    0,  350,  525,  700,  1050,  1400,  2100,  2800,  4200,  6300,  8400, 12600, 16800, 25200, 33600 },
        new[] { /*15*/    0,    0,    0,    0,    0,    0,    0,  375,  563,   750,  1125,  1500,  2250,  3000,  4500,  6750,  9000, 13500, 18000, 27000 },
        new[] { /*16*/    0,    0,    0,    0,    0,    0,    0,    0,  400,   600,   800,  1200,  1600,  2400,  3200,  4800,  7200,  9600, 14400, 19200 },
        new[] { /*17*/    0,    0,    0,    0,    0,    0,    0,    0,    0,   425,   638,   850,  1275,  1700,  2550,  3400,  5100,  7650, 10200, 15300 },
        new[] { /*18*/    0,    0,    0,    0,    0,    0,    0,    0,    0,     0,   450,   675,   900,  1350,  1800,  2700,  3600,  5400,  8100, 10800 },
        new[] { /*19*/    0,    0,    0,    0,    0,    0,    0,    0,    0,     0,     0,   475,   713,   950,  1425,  1900,  2850,  3800,  5700,  8550 },
        new[] { /*20*/    0,    0,    0,    0,    0,    0,    0,    0,    0,     0,     0,     0,   500,   750,  1000,  1500,  2000,  3000,  4000,  6000 },
    };

    [System.Serializable]
    public class XPAward
    {
        public CharacterController Enemy;
        public float ChallengeRating;
        public string EnemyName;

        public XPAward(CharacterController enemy, float challengeRating)
        {
            Enemy = enemy;
            ChallengeRating = challengeRating;
            EnemyName = enemy != null && enemy.Stats != null && !string.IsNullOrWhiteSpace(enemy.Stats.CharacterName)
                ? enemy.Stats.CharacterName
                : "Unknown Enemy";
        }
    }

    public class CombatXPResult
    {
        public List<XPAward> Awards = new List<XPAward>();
        /// <summary>The number of characters the base awards are divided among (DMG p.37 step 4).</summary>
        public int PartySize;
        public float AveragePartyLevel;
        /// <summary>The level each character's awards were looked up at (DMG p.37 step 1).</summary>
        public Dictionary<CharacterController, int> CharacterLevel = new Dictionary<CharacterController, int>();
        /// <summary>The sum of the Table 2-6 base awards at that character's level, before the split.</summary>
        public Dictionary<CharacterController, int> CharacterBaseXP = new Dictionary<CharacterController, int>();
        /// <summary>The character's share (base total / party size), before the multiclass XP penalty.</summary>
        public Dictionary<CharacterController, int> CharacterXPAward = new Dictionary<CharacterController, int>();
        /// <summary>The XP the character actually gained (after the multiclass XP penalty).</summary>
        public Dictionary<CharacterController, int> CharacterXPGained = new Dictionary<CharacterController, int>();
        public Dictionary<CharacterController, bool> CharacterLeveledUp = new Dictionary<CharacterController, bool>();
    }

    /// <summary>Computes the encounter's XP (PreviewXPForCombat) and awards each party member its share.</summary>
    public CombatXPResult CalculateXPForCombat(List<CharacterController> party, List<CharacterController> defeatedEnemies)
    {
        CombatXPResult result = PreviewXPForCombat(party, defeatedEnemies);

        foreach (KeyValuePair<CharacterController, int> kvp in result.CharacterXPAward)
        {
            CharacterController character = kvp.Key;
            int award = kvp.Value;
            string charName = !string.IsNullOrWhiteSpace(character.Stats.CharacterName) ? character.Stats.CharacterName : "Unknown";
            int oldLevel = Mathf.Max(1, character.Stats.Level);
            int oldXp = character.Stats.ExperiencePoints;
            int effectiveXpGain = character.Stats.GetEffectiveXpGain(award);

            bool addExperienceReportedLevelUp = character.Stats.AddExperience(award);

            int newXp = character.Stats.ExperiencePoints;
            int newLevel = Mathf.Max(1, character.Stats.Level);
            bool leveledUp = newLevel > oldLevel;

            if (addExperienceReportedLevelUp != leveledUp)
            {
                Debug.LogWarning($"[XP] Level-up mismatch for {charName}: AddExperience={addExperienceReportedLevelUp}, computed={leveledUp}");
            }

            result.CharacterXPGained[character] = effectiveXpGain;
            result.CharacterLeveledUp[character] = leveledUp;

            Debug.Log($"[XP] {charName}: Level {oldLevel} → {newLevel}, LeveledUp={leveledUp}, XP {oldXp} → {newXp}");

            if (leveledUp)
            {
                Debug.Log($"[XP] ⭐⭐⭐ {charName} LEVELED UP to {newLevel}! ⭐⭐⭐");
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the encounter's XP without awarding it (DMG p.37 steps 1-6): for each character, look up every
    /// defeated monster's base award at that character's own level on Table 2-6, add them up and divide by the
    /// number of characters in the party, rounding down once (PHB p.304), as the DMG p.37 example does. Fills
    /// everything in the result except CharacterXPGained and CharacterLeveledUp.
    /// </summary>
    public static CombatXPResult PreviewXPForCombat(List<CharacterController> party, List<CharacterController> defeatedEnemies)
    {
        party = party ?? new List<CharacterController>();
        defeatedEnemies = defeatedEnemies ?? new List<CharacterController>();

        CombatXPResult result = new CombatXPResult();
        List<CharacterController> members = party.Where(c => c != null && c.Stats != null).Distinct().ToList();
        result.PartySize = members.Count;
        result.AveragePartyLevel = members.Count > 0 ? (float)members.Average(c => GetAwardLevel(c.Stats)) : 0f;

        for (int i = 0; i < defeatedEnemies.Count; i++)
        {
            CharacterController enemy = defeatedEnemies[i];
            if (enemy == null || enemy.Stats == null)
                continue;

            XPAward award = new XPAward(enemy, GetChallengeRating(enemy));
            result.Awards.Add(award);
            Debug.Log($"[XP] Defeated: {award.EnemyName} (CR {ChallengeRatingUtils.Format(award.ChallengeRating)})");
        }

        Debug.Log($"[XP] Calculating experience | party={members.Count} | defeated={result.Awards.Count}");

        for (int i = 0; i < members.Count; i++)
        {
            CharacterController character = members[i];
            int level = GetAwardLevel(character.Stats);
            float baseTotal = 0f;
            for (int a = 0; a < result.Awards.Count; a++)
                baseTotal += GetBaseXPAward(level, result.Awards[a].ChallengeRating);

            // The small epsilon keeps exact fractional products (a CR 1/3 award times 3) from flooring one short.
            int share = Mathf.FloorToInt(baseTotal / members.Count + 0.001f);
            result.CharacterLevel[character] = level;
            result.CharacterBaseXP[character] = Mathf.FloorToInt(baseTotal + 0.001f);
            result.CharacterXPAward[character] = share;

            Debug.Log($"[XP] {character.Stats.CharacterName} (level {level}): base {baseTotal:0.#} / {members.Count} = {share} XP");
        }

        return result;
    }

    /// <summary>
    /// The level a character's awards are looked up at: its character level, counting ECL for a powerful race
    /// (DMG p.37 step 1). ECL is never populated (CHR-028), so this is the character level today.
    /// </summary>
    public static int GetAwardLevel(CharacterStats stats)
    {
        if (stats == null)
            return 1;
        return Mathf.Max(1, stats.EffectiveCharacterLevel);
    }

    private static float GetChallengeRating(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return 1f;

        if (!string.IsNullOrWhiteSpace(creature.Stats.ChallengeRating)
            && ChallengeRatingUtils.TryParse(creature.Stats.ChallengeRating, out float parsedCr)
            && parsedCr > 0f)
        {
            return parsedCr;
        }

        // Fallback for a creature with no CR (CRE-035): its level. That is RAW only for an NPC with PC classes (DMG p.37);
        // NPC-class levels count one less (DMG p.38, CRE-043) and a monster needs its MM CR.
        return Mathf.Max(1f, creature.Stats.Level);
    }

    /// <summary>
    /// DMG Table 2-6 (p.38): the base XP for defeating one monster of <paramref name="challengeRating"/>, for one
    /// character of <paramref name="characterLevel"/>, before it is divided among the party.
    /// A CR below 1 earns the CR 1 award times that fraction (DMG p.37, monsters below CR 1).
    /// A CR above 20 earns double the award for a CR two lower (DMG p.38, note under the table).
    /// The cells the table leaves blank (DMG p.38 notes, sent to the ad hoc awards of DMG p.39-40), by the delegated RAW
    /// reading of 2026-10-14 (CHR-004): a monster 8 or more CRs below the level ("*") earns 0, since an encounter that
    /// costs the party nothing earns no XP and the DMG p.39-40 example gives less than the row's minimum, so the minimum
    /// is no floor (a fractional CR whose CR 1 cell is "*" earns 0 too, DMG p.37); a monster 8 or more CRs above the
    /// row's level ("**") earns the award for a CR exactly 7 above it, the row's maximum being the most to give (DMG
    /// p.39): the row's largest cell for levels 1-13 and, for levels 14-20, the doubling note's value past CR 20, so the
    /// award never drops as the CR rises (DMG p.36). The DMG p.39 minimum and maximum govern only these blank cells, one
    /// monster at a time: printed cells are summed with no clamp on the encounter (DMG p.37 steps 2-5; the table is
    /// per single monster), so two CR 12 monsters give a 4th-level character 25,600 base XP. The gap is measured from
    /// the row, so the shared 1st-3rd row reads as 3rd level (its printed CR 9 and 10 cells apply to a 1st-level
    /// character). Levels above 20 use the 20th-level row (epic play is out of scope).
    /// </summary>
    public static float GetBaseXPAward(int characterLevel, float challengeRating)
    {
        int level = Mathf.Clamp(characterLevel, 1, 20);
        if (challengeRating <= 0f)
            return 0f;
        if (challengeRating < 1f)
            return GetBaseXPAward(level, 1f) * challengeRating;

        int cr = Mathf.FloorToInt(challengeRating + 0.5f);
        int rowLevel = Mathf.Max(3, level); // levels 1-3 share one row
        if (cr - rowLevel >= 8)
            return GetBaseXPAward(level, rowLevel + 7); // "**": the award for CR level + 7
        if (level - cr >= 8)
            return 0f; // "*": the table supports no award
        if (cr > 20)
            return 2f * GetBaseXPAward(level, cr - 2);

        return AwardTable[rowLevel - 3][cr - 1];
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
