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
    /// <para>
    /// The caster soak <see cref="CastersId"/> (batch 15 step 1, docs/designs/npc_spellcasting_batch15.md, finding F2):
    /// the baseline soak's levels 1-3 hold none of the DMG's spellcasters, so this soak forces, by seed, one of the
    /// caster rows of levels 5-8 (or batch 14's goblin_adept fixture) through the same DungeonEncounterSpawner path,
    /// against the same AI-run Quick Start party (design choice DC3). It measures what the casters do (the NPC columns of
    /// <see cref="SoakStats.NpcColumns"/>), not who wins.
    /// </para>
    /// </summary>
    public static class SoakScenarios
    {
        public const string DmgId = "soak/dmg-random-encounters";
        public const string CastersId = "soak/dmg-casters";
        public const int MaxGenerationAttempts = 5;
        /// <summary>The Quick Start characters' level (only logged by the encounter generator).</summary>
        public const int QuickStartLevel = 3;

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return ScenarioCatalog.Safe("SoakScenarios " + DmgId, DmgRandomEncounters);
            yield return ScenarioCatalog.Safe("SoakScenarios " + CastersId, DmgCasters);
        }

        /// <summary>
        /// One group of the caster soak: a DMG dungeon table row (level and the first d% roll of its range, as in
        /// Assets/StreamingAssets/dungeon_encounters.csv) or, for <see cref="Fixture"/>, a fixed list of NPC ids.
        /// </summary>
        public sealed class CasterGroup
        {
            public int Group;
            /// <summary>"level/roll range" of the DMG row, or "fixture".</summary>
            public string Row;
            public int DungeonLevel;
            public int Roll;
            public string Label;
            /// <summary>
            /// A fragment the DMG row's description must contain (case-insensitive), or null for a fixture: the table manager
            /// clamps the level and roll and returns whatever row is there, so a changed or short table fails setup here
            /// instead of silently becoming another encounter.
            /// </summary>
            public string ExpectName;
            /// <summary>The NPC ids of a fixture group (spawned through the same spawner path), or null for a DMG row.</summary>
            public string[] Fixture;
        }

        /// <summary>
        /// The seven caster soak groups (plan step 1): the DMG class-levelled casters (kobold sorcerer 5, lizardfolk druid
        /// 5 with crocodile, 1d3 troglodyte clerics 5), the MM casters of levels 7-8 (drider, young copper dragon, dark naga)
        /// and batch 14's goblin_adept with two goblin warriors.
        /// </summary>
        public static readonly CasterGroup[] CasterGroups =
        {
            new CasterGroup { Group = 1, Row = "5/89-90", DungeonLevel = 5, Roll = 89, Label = "kobold sorcerer 5", ExpectName = "kobold sorcerer" },
            new CasterGroup { Group = 2, Row = "6/89-90", DungeonLevel = 6, Roll = 89, Label = "lizardfolk druid 5 with crocodile", ExpectName = "lizardfolk druid" },
            new CasterGroup { Group = 3, Row = "8/89-90", DungeonLevel = 8, Roll = 89, Label = "1d3 troglodyte clerics 5", ExpectName = "troglodyte cleric" },
            new CasterGroup { Group = 4, Row = "7/29-31", DungeonLevel = 7, Roll = 29, Label = "drider", ExpectName = "drider" },
            new CasterGroup { Group = 5, Row = "7/26-28", DungeonLevel = 7, Roll = 26, Label = "young copper dragon", ExpectName = "young copper dragon" },
            new CasterGroup { Group = 6, Row = "8/59-60", DungeonLevel = 8, Roll = 59, Label = "dark naga", ExpectName = "dark naga" },
            new CasterGroup { Group = 7, Row = "fixture", DungeonLevel = 0, Roll = 0, Label = "goblin_adept with 2 goblin warriors",
                Fixture = new[] { "goblin_adept", "goblin_warrior", "goblin_warrior" } },
        };

        /// <summary>
        /// Harness-only stand-ins for a row's base creature id that NPCDatabase lacks (ENC-002): the kobold sorcerer row
        /// (dungeon_encounters.csv 5/89-90) names the race "kobold", which the table parser passes on as the id "kobold",
        /// but the database stores the MM kobold (a 1st-level warrior, MM p.161) as "kobold_warrior". The game's tables are
        /// not changed here (batch 15 step 1 is NEUTRAL; step 8 fixes the row); a stand-in is used only when the row's id
        /// is missing and the stand-in exists, and the job's generated info names it.
        /// </summary>
        private static readonly Dictionary<string, string> MissingBaseStandIns = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "kobold", "kobold_warrior" },
        };

        /// <summary>
        /// Replaces each entry's base creature id that NPCDatabase lacks by its <see cref="MissingBaseStandIns"/> stand-in,
        /// keeping the row's class, level, advancement, templates and count; returns the "old -> new (ENC-002)" notes.
        /// </summary>
        public static List<string> ApplyMissingBaseStandIns(EncounterDefinition encounter)
        {
            var notes = new List<string>();
            if (encounter == null)
                return notes;
            NPCDatabase.Init();
            foreach (EncounterCreatureEntry entry in encounter.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.BaseCreatureId) || NPCDatabase.Get(entry.BaseCreatureId) != null)
                    continue;
                if (MissingBaseStandIns.TryGetValue(entry.BaseCreatureId, out string standIn) && NPCDatabase.Get(standIn) != null)
                {
                    notes.Add(entry.BaseCreatureId + " -> " + standIn + " (ENC-002)");
                    entry.BaseCreatureId = standIn;
                }
            }
            return notes;
        }

        /// <summary>
        /// The DMG dungeon row at <paramref name="level"/> and <paramref name="roll"/>
        /// (DungeonEncounterTableManager.GenerateEncounterForRoll), checked to be the expected row: its description must
        /// contain <paramref name="expect"/> (case-insensitive). Throws InvalidOperationException otherwise, so the job ends
        /// as a setup error naming the row it got.
        /// </summary>
        public static EncounterDefinition RequireDmgRow(int level, int roll, string expect, string what)
        {
            EncounterDefinition encounter = DungeonEncounterTableManager.GenerateEncounterForRoll(level, roll);
            if (encounter == null)
                throw new InvalidOperationException(what + ": no encounter at " + level + "/" + roll);
            if (!string.IsNullOrEmpty(expect) && (encounter.Name ?? "").IndexOf(expect, StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException(what + ": the table row at " + level + "/" + roll + " is '" + encounter.Name
                    + "', not the expected '" + expect + "' (dungeon_encounters.csv changed or loaded short)");
            return encounter;
        }

        /// <summary>The caster soak group a seed uses: ((seed - 1) mod 7) + 1, so seeds 1-49 run every group seven times.</summary>
        public static int CasterGroupForSeed(int seed)
        {
            int n = CasterGroups.Length;
            return 1 + (((seed - 1) % n) + n) % n;
        }

        private static ScenarioDef DmgCasters()
        {
            return Scenario.Define(CastersId, "AI-vs-AI caster soak: the AI-run Quick Start party against a DMG spellcaster row of levels 5-8 or the goblin_adept fixture (group by seed)")
                .Tags(SoakStats.Tag)
                .Covers("DMG p.78 Random Dungeon Encounters", "ENC-021", "CRE-016", "CRE-090", "batch 15 caster baseline")
                .MaxRounds(30)
                .WallCap(600f)
                .Pc("fighter", ActorSource.QuickStart("Fighter"), 6, 9)
                .Pc("rogue", ActorSource.QuickStart("Rogue"), 6, 11)
                .Pc("cleric", ActorSource.QuickStart("Cleric"), 4, 9)
                .Pc("wizard", ActorSource.QuickStart("Wizard"), 4, 11)
                .GenerateActors(GenerateCasterEncounter)
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

        /// <summary>
        /// The caster soak's encounter for the job's seed (the RNG is already seeded, so a 1d3 count is the same every run):
        /// the group's DMG row by its level and roll (DungeonEncounterTableManager.GenerateEncounterForRoll), or the fixture's
        /// ids, prepared through DungeonEncounterSpawner.PrepareEncounter and placed as the baseline soak places them.
        /// </summary>
        public static GeneratedActors GenerateCasterEncounter(ScenarioContext ctx)
        {
            if (!DungeonEncounterTableManager.IsLoaded)
                DungeonEncounterTableManager.LoadTables();

            CasterGroup g = CasterGroups[CasterGroupForSeed(ctx.Seed) - 1];
            EncounterDefinition encounter;
            if (g.Fixture != null)
            {
                encounter = new EncounterDefinition(g.Label);
                foreach (IGrouping<string, string> id in g.Fixture.GroupBy(x => x))
                    encounter.AddCreature(id.Key, id.Count());
            }
            else
            {
                encounter = RequireDmgRow(g.DungeonLevel, g.Roll, g.ExpectName, "caster group " + g.Group + " (" + g.Row + ", " + g.Label + ")");
            }
            List<string> standIns = ApplyMissingBaseStandIns(encounter);

            DungeonEncounterSpawner.SpawnResult spawn = DungeonEncounterSpawner.PrepareEncounter(encounter);
            if (spawn.Count == 0)
            {
                DungeonEncounterSpawner.CleanupSpawnEntries(spawn);
                throw new InvalidOperationException("caster group " + g.Group + " (" + g.Row + ", " + encounter.Name + ") resolved no creature ("
                    + string.Join("; ", spawn.Warnings) + ")");
            }

            var gen = new GeneratedActors();
            gen.Cleanup = () => DungeonEncounterSpawner.CleanupSpawnEntries(spawn);
            try
            {
                Place(gen, encounter, spawn, g.DungeonLevel, new List<string>());
                gen.Info.Set("casterGroup", g.Group).Set("row", g.Row).Set("standIns", standIns.Count > 0 ? standIns : null);
                return gen;
            }
            catch
            {
                DungeonEncounterSpawner.CleanupSpawnEntries(spawn);
                throw;
            }
        }

        private static ScenarioDef DmgRandomEncounters()
        {
            return Scenario.Define(DmgId, "AI-vs-AI soak: the AI-run Quick Start party against a DMG dungeon random encounter (dungeon level 1-3 by seed)")
                .Tags(SoakStats.Tag)
                .Covers("DMG p.78 Random Dungeon Encounters", "CORE-011", "CORE-037", "AI depth baseline")
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
            int count = Math.Min(spawn.Count, ScenarioLimits.MaxNpcActors);
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
