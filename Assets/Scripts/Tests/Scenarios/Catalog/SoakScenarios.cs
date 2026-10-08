#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Scenarios
{
    /// <summary>
    /// AI-vs-AI soak (docs/TESTING.md 3.5): the AI-run Quick Start party against random encounters from the DMG
    /// dungeon encounter tables (DMG p.78, Random Dungeon Encounters: pick the dungeon level, roll d% on that level's
    /// table, which can send the roll to the next lower or higher table, then roll the number appearing). DMG p.77
    /// says a party should adventure where the dungeon level matches the party level. The Quick Start characters
    /// are 3rd level, so dungeon level 3 is the matching table and levels 1 and 2 are easier ones. The seed picks the
    /// level (1 + (seed - 1) mod 3) and, through the seeded RNG, the roll, so a seed always gives the same encounter.
    /// Any outcome is fine; the job passes when no unwaived invariant broke and nothing ended as an error.
    /// </summary>
    public static class SoakScenarios
    {
        public const string DmgId = "soak/dmg-random-encounters";
        public const int MaxGenerationAttempts = 5;
        /// <summary>The Quick Start characters' level (only logged by the encounter generator).</summary>
        public const int QuickStartLevel = 3;

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return ScenarioCatalog.Safe("SoakScenarios " + DmgId, DmgRandomEncounters);
        }

        private static ScenarioDef DmgRandomEncounters()
        {
            return Scenario.Define(DmgId, "AI-vs-AI soak: the AI-run Quick Start party against a DMG dungeon random encounter (dungeon level 1-3 by seed)")
                .Tags(SoakStats.Tag)
                .Covers("DMG p.78 Random Dungeon Encounters", "CORE-011", "CORE-037", "CORE-003", "AI depth baseline")
                .MaxRounds(30)
                .WallCap(600f)
                .Pc("fighter", ActorSource.QuickStart("Fighter"), 6, 9)
                .Pc("rogue", ActorSource.QuickStart("Rogue"), 6, 11)
                .Pc("cleric", ActorSource.QuickStart("Cleric"), 4, 9)
                .Pc("wizard", ActorSource.QuickStart("Wizard"), 4, 11)
                .GenerateActors(GenerateDmgEncounter)
                .Expect("The fight runs to an end the game declares itself, or to the round cap (CORE-011, CORE-037)",
                    Expect.Outcome(Outcome.Victory, Outcome.Defeat, Outcome.Stalemate))
                .Expect("The generated encounter is on the field and somebody acts", v =>
                {
                    int enemies = v.Of("actor").Count(e => e.Str("source") != null && e.Str("source").StartsWith("npc:dmg:", StringComparison.Ordinal));
                    if (enemies == 0)
                        return ExpectResult.Fail("no generated enemy in the trace");
                    int turns = v.Of("turn_start").Count();
                    return turns > 0 ? ExpectResult.Pass(enemies + " enemies, " + turns + " turns") : ExpectResult.Fail("no turn started");
                })
                .Build();
        }

        /// <summary>The dungeon level a seed uses: 1, 2, 3, 1, ...</summary>
        public static int DungeonLevelForSeed(int seed) => 1 + (((seed - 1) % 3) + 3) % 3;

        /// <summary>
        /// Rolls a DMG dungeon encounter for the job's seed (the RNG is already seeded), prepares it through
        /// DungeonEncounterSpawner (class levels and templates; the per-job "spawn_..." ids are unregistered in the
        /// cleanup) and places the creatures on the right half of the grid.
        /// </summary>
        public static GeneratedActors GenerateDmgEncounter(ScenarioContext ctx)
        {
            if (!DungeonEncounterTableManager.IsLoaded)
                DungeonEncounterTableManager.LoadTables();

            int level = DungeonLevelForSeed(ctx.Seed);
            var gen = new GeneratedActors();
            var rejected = new List<string>();
            EncounterDefinition encounter = null;
            DungeonEncounterSpawner.SpawnResult spawn = null;

            for (int attempt = 1; attempt <= MaxGenerationAttempts && spawn == null; attempt++)
            {
                EncounterDefinition e = DungeonEncounterTableManager.GenerateRandomEncounter(level, QuickStartLevel);
                if (e == null)
                {
                    rejected.Add("attempt " + attempt + ": no encounter");
                    continue;
                }
                DungeonEncounterSpawner.SpawnResult r = DungeonEncounterSpawner.PrepareEncounter(e);
                if (r.Count == 0)
                {
                    rejected.Add("attempt " + attempt + ": " + e.Name + " resolved no creature (" + string.Join("; ", r.Warnings) + ")");
                    DungeonEncounterSpawner.CleanupSpawnEntries(r);
                    continue;
                }
                encounter = e;
                spawn = r;
            }
            if (spawn == null)
                throw new InvalidOperationException("no DMG encounter for dungeon level " + level + " in " + MaxGenerationAttempts + " attempts: " + string.Join(" | ", rejected));

            DungeonEncounterSpawner.SpawnResult registered = spawn;
            gen.Cleanup = () => DungeonEncounterSpawner.CleanupSpawnEntries(registered);
            try
            {
                return Place(gen, encounter, spawn, level, rejected);
            }
            catch
            {
                // The runner never sees gen when this throws, so unregister the spawn_ ids here.
                DungeonEncounterSpawner.CleanupSpawnEntries(spawn);
                throw;
            }
        }

        private static GeneratedActors Place(GeneratedActors gen, EncounterDefinition encounter,
            DungeonEncounterSpawner.SpawnResult spawn, int level, List<string> rejected)
        {
            int count = Math.Min(spawn.Count, ScenarioLimits.PoolSlots);
            var labels = new List<string>();
            var sizes = new List<int>();
            for (int i = 0; i < count; i++)
            {
                labels.Add(Label(spawn.EnemyIds[i]));
                NPCDefinition def = i < spawn.Definitions.Count ? spawn.Definitions[i] : null;
                sizes.Add(def != null ? Mathf.Max(1, def.SizeCategory.GetSpaceWidthSquares()) : 1);
            }

            Vector2Int[] positions = PlaceEnemies(sizes, PartySquares());
            for (int i = 0; i < count; i++)
            {
                if (positions[i].x < 0)
                    throw new InvalidOperationException("no room on the grid for " + labels[i] + " (" + sizes[i] + "x" + sizes[i] + ")");
                gen.Actors.Add(new ActorSpec
                {
                    Key = "e" + (i + 1),
                    Team = CharacterTeam.Enemy,
                    Source = ActorSource.Npc(spawn.EnemyIds[i], "dmg:" + labels[i]),
                    Pos = positions[i],
                    Control = Control.Ai
                });
            }

            gen.Info.Set("name", encounter.Name)
                    .Set("dungeonLevel", level)
                    .Set("targetEL", encounter.TargetEL)
                    .Set("count", count)
                    .Set("creatures", labels)
                    .Set("capped", spawn.Count > count ? spawn.Count : 0)
                    .Set("warnings", spawn.Warnings.Count > 0 ? spawn.Warnings : null)
                    .Set("rejected", rejected.Count > 0 ? rejected : null);
            return gen;
        }

        /// <summary>"spawn_kobold_sorcerer_2_17" becomes "kobold_sorcerer_2" (the session counter is dropped).</summary>
        private static string Label(string spawnId)
        {
            string id = spawnId ?? "unknown";
            if (id.StartsWith("spawn_", StringComparison.Ordinal))
                id = id.Substring("spawn_".Length);
            int last = id.LastIndexOf('_');
            if (last > 0 && int.TryParse(id.Substring(last + 1), out _))
                id = id.Substring(0, last);
            return id;
        }

        private static List<Vector2Int> PartySquares()
            => new List<Vector2Int> { new Vector2Int(6, 9), new Vector2Int(6, 11), new Vector2Int(4, 9), new Vector2Int(4, 11) };

        /// <summary>
        /// Footprint-aware placement on the right half of the 20x20 grid (x 11-18, y 2-17), largest creatures first,
        /// with one free square between creatures. CustomEncounterBuilderUI.CalculateSpawnPositions ignores
        /// creature size, so the soak does not use it. Unplaceable creatures get (-1, -1).
        /// </summary>
        public static Vector2Int[] PlaceEnemies(IList<int> widths, IList<Vector2Int> avoid)
        {
            var result = new Vector2Int[widths.Count];
            var blocked = new HashSet<Vector2Int>(avoid ?? new List<Vector2Int>());
            var candidates = new List<Vector2Int>();
            for (int x = ScenarioLimits.GridWidth / 2 + 1; x <= ScenarioLimits.GridWidth - 2; x++)
                for (int y = 2; y <= ScenarioLimits.GridHeight - 3; y++)
                    candidates.Add(new Vector2Int(x, y));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Vector2Int t = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = t;
            }

            IEnumerable<int> order = Enumerable.Range(0, widths.Count).OrderByDescending(i => widths[i]).ThenBy(i => i);
            foreach (int i in order)
            {
                int w = widths[i];
                result[i] = new Vector2Int(-1, -1);
                foreach (Vector2Int c in candidates)
                {
                    if (!Fits(c, w, blocked))
                        continue;
                    result[i] = c;
                    // Block the footprint plus a one-square ring.
                    for (int dx = -1; dx <= w; dx++)
                        for (int dy = -1; dy <= w; dy++)
                            blocked.Add(new Vector2Int(c.x + dx, c.y + dy));
                    break;
                }
            }
            return result;
        }

        private static bool Fits(Vector2Int origin, int w, HashSet<Vector2Int> blocked)
        {
            for (int dx = 0; dx < w; dx++)
            {
                for (int dy = 0; dy < w; dy++)
                {
                    var p = new Vector2Int(origin.x + dx, origin.y + dy);
                    if (p.x >= ScenarioLimits.GridWidth || p.y >= ScenarioLimits.GridHeight || blocked.Contains(p))
                        return false;
                }
            }
            return true;
        }
    }
}
#endif
