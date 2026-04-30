using System;
using System.Collections.Generic;
using UnityEngine;

public enum RandomEncounterStrategy
{
    SingleBoss,
    EliteSquad,
    Swarm,
    MixedGroup
}

public class RandomEncounterRequest
{
    public List<int> PartyLevels = new List<int>();
    public int PartySize;
    public RandomEncounterDifficulty Difficulty = RandomEncounterDifficulty.Average;

    public string EnvironmentFilter;
    public string CreatureTypeFilter;

    public int? MinCreatures;
    public int? MaxCreatures;
}

public class RandomEncounterCreatureGroup
{
    public string NpcId;
    public string DisplayName;
    public string ChallengeRating;
    public float ChallengeRatingValue;
    public int Count;
    public int XpEach;
}

public class GeneratedRandomEncounter
{
    public List<RandomEncounterCreatureGroup> Groups = new List<RandomEncounterCreatureGroup>();
    public List<string> NpcIds = new List<string>();

    public RandomEncounterStrategy Strategy;
    public RandomEncounterDifficulty Difficulty;
    public int APL;
    public int TargetEL;
    public float ActualEL;
    public int TotalXP;

    public int TotalCreatureCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Groups.Count; i++)
                total += Mathf.Max(0, Groups[i].Count);
            return total;
        }
    }

    public string BuildHeaderLine()
    {
        return $"{Difficulty} Encounter (EL {ChallengeRatingUtils.Format(ActualEL)} vs APL {APL})";
    }

    public string BuildPreviewText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine(BuildHeaderLine());

        for (int i = 0; i < Groups.Count; i++)
        {
            RandomEncounterCreatureGroup g = Groups[i];
            string qty = g.Count > 1 ? g.Count.ToString() : "1";
            sb.AppendLine($"- {qty} {g.DisplayName} (CR {ChallengeRatingUtils.Format(g.ChallengeRatingValue)})");
        }

        sb.AppendLine($"Total EL: {ChallengeRatingUtils.Format(ActualEL)}");
        sb.AppendLine($"Total XP: {TotalXP}");
        sb.AppendLine($"Strategy: {Strategy}");
        return sb.ToString().TrimEnd();
    }
}

public class RandomEncounterSystem
{
    private const int MaxGenerationAttempts = 220;

    public GeneratedRandomEncounter Generate(RandomEncounterRequest request)
    {
        if (request == null)
            return null;

        NPCDatabase.Init();

        int partySize = request.PartySize > 0 ? request.PartySize : (request.PartyLevels != null ? request.PartyLevels.Count : 0);
        int apl = ChallengeRatingUtils.CalculateAPL(request.PartyLevels, partySize);
        int targetEl = ChallengeRatingUtils.GetTargetELForDifficulty(apl, request.Difficulty);

        List<MonsterCandidate> candidates = BuildCandidates(request);
        if (candidates.Count == 0)
            return null;

        List<RandomEncounterStrategy> strategies = new List<RandomEncounterStrategy>
        {
            RandomEncounterStrategy.SingleBoss,
            RandomEncounterStrategy.EliteSquad,
            RandomEncounterStrategy.Swarm,
            RandomEncounterStrategy.MixedGroup
        };
        Shuffle(strategies);

        GeneratedRandomEncounter best = null;
        float bestDelta = float.MaxValue;

        for (int s = 0; s < strategies.Count; s++)
        {
            RandomEncounterStrategy strategy = strategies[s];
            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                GeneratedRandomEncounter encounter = GenerateByStrategy(strategy, candidates, request, apl, targetEl);
                if (encounter == null)
                    continue;

                float delta = Mathf.Abs(encounter.ActualEL - targetEl);
                if (delta < bestDelta)
                {
                    best = encounter;
                    bestDelta = delta;
                }

                if (delta <= 0.75f)
                    return encounter;
            }
        }

        return best;
    }

    private GeneratedRandomEncounter GenerateByStrategy(
        RandomEncounterStrategy strategy,
        List<MonsterCandidate> candidates,
        RandomEncounterRequest request,
        int apl,
        int targetEl)
    {
        List<MonsterCandidate> picks = new List<MonsterCandidate>();

        switch (strategy)
        {
            case RandomEncounterStrategy.SingleBoss:
                if (!TryBuildSingleBoss(candidates, targetEl, picks))
                    return null;
                break;

            case RandomEncounterStrategy.EliteSquad:
                if (!TryBuildEliteSquad(candidates, targetEl, request, picks))
                    return null;
                break;

            case RandomEncounterStrategy.Swarm:
                if (!TryBuildSwarm(candidates, targetEl, request, picks))
                    return null;
                break;

            case RandomEncounterStrategy.MixedGroup:
                if (!TryBuildMixedGroup(candidates, targetEl, request, picks))
                    return null;
                break;

            default:
                return null;
        }

        if (!ValidateCreatureCount(picks.Count, request.MinCreatures, request.MaxCreatures))
            return null;

        return BuildEncounterResult(strategy, request.Difficulty, apl, targetEl, picks);
    }

    private bool TryBuildSingleBoss(List<MonsterCandidate> candidates, int targetEl, List<MonsterCandidate> picks)
    {
        List<MonsterCandidate> pool = FilterByCrBand(candidates, targetEl - 2f, targetEl + 2f);
        if (pool.Count == 0)
            pool = candidates;

        MonsterCandidate pick = RandomPick(pool);
        if (pick == null)
            return false;

        picks.Add(pick);
        return true;
    }

    private bool TryBuildEliteSquad(List<MonsterCandidate> candidates, int targetEl, RandomEncounterRequest request, List<MonsterCandidate> picks)
    {
        int minCount = request.MinCreatures.HasValue ? Mathf.Max(2, request.MinCreatures.Value) : 2;
        int maxCount = request.MaxCreatures.HasValue ? Mathf.Max(minCount, request.MaxCreatures.Value) : 4;
        maxCount = Mathf.Min(maxCount, 8);
        int count = UnityEngine.Random.Range(minCount, maxCount + 1);

        float targetCr = targetEl - GroupElBonusForCount(count);
        List<MonsterCandidate> pool = FilterByCrBand(candidates, targetCr - 1f, targetCr + 1f);
        if (pool.Count == 0)
            pool = FilterByCrBand(candidates, targetCr - 2f, targetCr + 2f);
        if (pool.Count == 0)
            return false;

        MonsterCandidate core = RandomPick(pool);
        if (core == null)
            return false;

        for (int i = 0; i < count; i++)
            picks.Add(core);

        return true;
    }

    private bool TryBuildSwarm(List<MonsterCandidate> candidates, int targetEl, RandomEncounterRequest request, List<MonsterCandidate> picks)
    {
        int minCount = request.MinCreatures.HasValue ? Mathf.Max(5, request.MinCreatures.Value) : 5;
        int maxCount = request.MaxCreatures.HasValue ? Mathf.Max(minCount, request.MaxCreatures.Value) : 8;
        maxCount = Mathf.Min(maxCount, 15);

        int count = UnityEngine.Random.Range(minCount, maxCount + 1);
        float targetCr = targetEl - GroupElBonusForCount(count);

        List<MonsterCandidate> pool = FilterByCrBand(candidates, 0.1f, Mathf.Max(0.5f, targetCr + 0.5f));
        if (pool.Count == 0)
            pool = FilterByCrBand(candidates, 0.1f, Mathf.Max(1f, targetCr + 1.5f));
        if (pool.Count == 0)
            return false;

        MonsterCandidate core = RandomPick(pool);
        if (core == null)
            return false;

        for (int i = 0; i < count; i++)
            picks.Add(core);

        return true;
    }

    private bool TryBuildMixedGroup(List<MonsterCandidate> candidates, int targetEl, RandomEncounterRequest request, List<MonsterCandidate> picks)
    {
        int minCount = request.MinCreatures.HasValue ? Mathf.Max(3, request.MinCreatures.Value) : 3;
        int maxCount = request.MaxCreatures.HasValue ? Mathf.Max(minCount, request.MaxCreatures.Value) : 7;
        maxCount = Mathf.Min(maxCount, 12);

        int count = UnityEngine.Random.Range(minCount, maxCount + 1);
        int minionCount = Mathf.Max(2, count - 1);

        float desiredLeaderCr = Mathf.Max(1f, targetEl - 1f);
        List<MonsterCandidate> leaderPool = FilterByCrBand(candidates, desiredLeaderCr - 2f, desiredLeaderCr + 1f);
        if (leaderPool.Count == 0)
            leaderPool = candidates;

        MonsterCandidate leader = RandomPick(leaderPool);
        if (leader == null)
            return false;

        float desiredMinionCrMax = Mathf.Max(0.25f, leader.Cr - 2f);
        List<MonsterCandidate> minionPool = FilterByCrBand(candidates, 0.1f, desiredMinionCrMax);
        if (minionPool.Count == 0)
            minionPool = FilterByCrBand(candidates, 0.1f, Mathf.Max(0.5f, leader.Cr - 1f));
        if (minionPool.Count == 0)
            return false;

        picks.Add(leader);
        for (int i = 0; i < minionCount; i++)
            picks.Add(RandomPick(minionPool));

        return true;
    }

    private GeneratedRandomEncounter BuildEncounterResult(RandomEncounterStrategy strategy, RandomEncounterDifficulty difficulty, int apl, int targetEl, List<MonsterCandidate> picks)
    {
        if (picks == null || picks.Count == 0)
            return null;

        Dictionary<string, RandomEncounterCreatureGroup> grouped = new Dictionary<string, RandomEncounterCreatureGroup>(StringComparer.Ordinal);
        List<float> crValues = new List<float>();

        for (int i = 0; i < picks.Count; i++)
        {
            MonsterCandidate candidate = picks[i];
            if (candidate == null)
                continue;

            crValues.Add(candidate.Cr);

            if (!grouped.TryGetValue(candidate.Def.Id, out RandomEncounterCreatureGroup group))
            {
                group = new RandomEncounterCreatureGroup
                {
                    NpcId = candidate.Def.Id,
                    DisplayName = candidate.Def.Name,
                    ChallengeRating = candidate.Def.ChallengeRating,
                    ChallengeRatingValue = candidate.Cr,
                    Count = 0,
                    XpEach = ChallengeRatingUtils.GetXpForCr(candidate.Cr)
                };
                grouped[candidate.Def.Id] = group;
            }

            group.Count++;
        }

        if (crValues.Count == 0)
            return null;

        GeneratedRandomEncounter encounter = new GeneratedRandomEncounter
        {
            Strategy = strategy,
            Difficulty = difficulty,
            APL = apl,
            TargetEL = targetEl,
            ActualEL = ChallengeRatingUtils.CalculateEncounterEL(crValues)
        };

        foreach (var kv in grouped)
        {
            RandomEncounterCreatureGroup group = kv.Value;
            encounter.Groups.Add(group);
            for (int i = 0; i < group.Count; i++)
                encounter.NpcIds.Add(group.NpcId);
            encounter.TotalXP += group.XpEach * group.Count;
        }

        // Keep output stable and readable.
        encounter.Groups.Sort((a, b) => b.ChallengeRatingValue.CompareTo(a.ChallengeRatingValue));

        return encounter;
    }

    private List<MonsterCandidate> BuildCandidates(RandomEncounterRequest request)
    {
        List<MonsterCandidate> list = new List<MonsterCandidate>();
        string env = (request.EnvironmentFilter ?? string.Empty).Trim();
        string creatureType = (request.CreatureTypeFilter ?? string.Empty).Trim();

        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.Id))
                continue;
            if (!ChallengeRatingUtils.TryParse(def.ChallengeRating, out float cr))
                continue;
            if (IsSummonedDefinition(def))
                continue;
            if (!string.IsNullOrEmpty(creatureType) && !MatchesTypeFilter(def, creatureType))
                continue;
            if (!string.IsNullOrEmpty(env) && !MatchesEnvironmentFilter(def, env))
                continue;

            list.Add(new MonsterCandidate { Def = def, Cr = cr });
        }

        return list;
    }

    private static bool IsSummonedDefinition(NPCDefinition def)
    {
        if (def == null)
            return true;

        if (def.CreatureTags != null)
        {
            for (int i = 0; i < def.CreatureTags.Count; i++)
            {
                string tag = def.CreatureTags[i];
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                if (string.Equals(tag, "SummonBase", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tag, "SummonAlias", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tag, "Summoned", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        string id = def.Id ?? string.Empty;
        return id.IndexOf("summon", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MatchesTypeFilter(NPCDefinition def, string filter)
    {
        if (def == null || string.IsNullOrWhiteSpace(filter))
            return true;

        if (!string.IsNullOrWhiteSpace(def.CreatureType)
            && def.CreatureType.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (def.CreatureTags != null)
        {
            for (int i = 0; i < def.CreatureTags.Count; i++)
            {
                string tag = def.CreatureTags[i];
                if (!string.IsNullOrWhiteSpace(tag)
                    && tag.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        return !string.IsNullOrWhiteSpace(def.Name)
               && def.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MatchesEnvironmentFilter(NPCDefinition def, string filter)
    {
        if (def == null || string.IsNullOrWhiteSpace(filter))
            return true;

        if (!string.IsNullOrWhiteSpace(def.Description)
            && def.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (def.CreatureTags != null)
        {
            for (int i = 0; i < def.CreatureTags.Count; i++)
            {
                string tag = def.CreatureTags[i];
                if (!string.IsNullOrWhiteSpace(tag)
                    && tag.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(def.CreatureType)
            && def.CreatureType.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return !string.IsNullOrWhiteSpace(def.Name)
               && def.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ValidateCreatureCount(int count, int? min, int? max)
    {
        if (min.HasValue && count < Mathf.Max(1, min.Value))
            return false;

        if (max.HasValue && count > Mathf.Max(1, max.Value))
            return false;

        return count > 0;
    }

    private static float GroupElBonusForCount(int count)
    {
        if (count <= 1)
            return 0f;

        return 1f + Mathf.Ceil(Mathf.Log(Mathf.Max(2, count), 2f));
    }

    private static List<MonsterCandidate> FilterByCrBand(List<MonsterCandidate> candidates, float minCr, float maxCr)
    {
        List<MonsterCandidate> result = new List<MonsterCandidate>();
        float low = Mathf.Max(0f, minCr);
        float high = Mathf.Max(low, maxCr);

        for (int i = 0; i < candidates.Count; i++)
        {
            MonsterCandidate c = candidates[i];
            if (c.Cr >= low && c.Cr <= high)
                result.Add(c);
        }

        return result;
    }

    private static MonsterCandidate RandomPick(List<MonsterCandidate> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    private static void Shuffle<T>(List<T> list)
    {
        if (list == null || list.Count < 2)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private class MonsterCandidate
    {
        public NPCDefinition Def;
        public float Cr;
    }
}
