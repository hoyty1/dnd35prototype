#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 15 (docs/designs/npc_spellcasting_batch15.md): NPC spellcasting scenarios.
    /// <para>Step 1 (the caster soak, NEUTRAL): rules/npc-caster-spawn-has-spells spawns the three DMG class-levelled caster
    /// rows (dungeon_encounters.csv 5/89-90 kobold sorcerer 5, 6/89-90 lizardfolk druid 5 with crocodile, 8/89-90 1d3
    /// troglodyte clerics 5) through DungeonEncounterSpawner.PrepareEncounter, the path of both soaks (the kobold row's
    /// missing base id "kobold" stood in by kobold_warrior, SoakScenarios.ApplyMissingBaseStandIns, ENC-002), and records for each
    /// class-levelled caster whether it has a SpellcastingComponent with a castable spell. Today none has
    /// (CreatureClassEngine.ApplyEncounterClassLevel fills no spell list, so the spawn adds no SpellcastingComponent; ENC-021):
    /// that is a known-behaviour pin, which step 8 (the DMG caster lists) turns into the expectation it states.</para>
    /// </summary>
    public static class NpcCasterScenarios
    {
        /// <summary>
        /// The DMG class-levelled caster rows (dungeon level, first d% roll of the row, label, a fragment the row's description
        /// must contain: SoakScenarios.RequireDmgRow fails setup when the table gives another row).
        /// </summary>
        private static readonly (int level, int roll, string label, string expect)[] DmgCasterRows =
        {
            (5, 89, "kobold sorcerer 5 (5/89-90)", "kobold sorcerer"),
            (6, 89, "lizardfolk druid 5 with crocodile (6/89-90)", "lizardfolk druid"),
            (8, 89, "1d3 troglodyte clerics 5 (8/89-90)", "troglodyte cleric"),
        };

        private static readonly string[] CasterClasses = { "Wizard", "Sorcerer", "Cleric", "Druid", "Bard", "Adept" };

        private const string FactPrefix = "ncs ";

        /// <summary>The spawn id of each generated actor key, per job context (the spawn ids are per job).</summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScenarioContext, Dictionary<string, string>> _spawnIds
            = new System.Runtime.CompilerServices.ConditionalWeakTable<ScenarioContext, Dictionary<string, string>>();

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return ScenarioCatalog.Safe("NpcCasterScenarios rules/npc-caster-spawn-has-spells", NpcCasterSpawnHasSpells);
        }

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static ScenarioDef NpcCasterSpawnHasSpells()
        {
            return Rules("rules/npc-caster-spawn-has-spells", "Every DMG class-levelled caster row spawns with a SpellcastingComponent and a castable spell (ENC-021)")
                .Covers("ENC-021", "DMG p.78 Random Dungeon Encounters", "batch 15 step 1")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 3, 10, Control.Scripted)
                .GenerateActors(GenerateDmgCasterRows)
                .Initiative("hero")
                .Turn("hero", 1, Step.Assert("record each class-levelled caster's spells", RecordCasterSpells))
                .Expect("Each DMG caster row spawned its class-levelled caster: a sorcerer, a druid and 1-3 clerics", v =>
                {
                    int sorcerers = Count(v, "class.Sorcerer"), druids = Count(v, "class.Druid"), clerics = Count(v, "class.Cleric");
                    string detail = sorcerers + " sorcerer, " + druids + " druid, " + clerics + " cleric(s)";
                    if (FactOf(v, "casters") == null)
                        return ExpectResult.Inconclusive("the record step did not run");
                    return sorcerers == 1 && druids == 1 && clerics >= 1 && clerics <= 3 ? ExpectResult.Pass(detail) : ExpectResult.Fail(detail);
                })
                .ExpectKnown("ENC-021", "Today no DMG class-levelled caster spawns with spells: no SpellcastingComponent with a castable spell (ENC-021)", v =>
                {
                    string casters = FactOf(v, "casters");
                    string withSpells = FactOf(v, "withSpells");
                    if (casters == null || withSpells == null)
                        return ExpectResult.Inconclusive("the record step did not run");
                    return withSpells == "0" && casters != "0"
                        ? ExpectResult.Pass(casters + " casters, none with spells")
                        : ExpectResult.Fail(withSpells + " of " + casters + " casters with spells: " + (FactOf(v, "detail") ?? ""));
                })
                .Build();
        }

        /// <summary>
        /// Prepares the three DMG caster rows (the seeded RNG rolls the clerics' 1d3) through DungeonEncounterSpawner and
        /// places every creature on the right half of the grid, as the soaks do (SoakScenarios.PlaceEnemies).
        /// </summary>
        private static GeneratedActors GenerateDmgCasterRows(ScenarioContext ctx)
        {
            if (!DungeonEncounterTableManager.IsLoaded)
                DungeonEncounterTableManager.LoadTables();

            var gen = new GeneratedActors();
            var spawns = new List<DungeonEncounterSpawner.SpawnResult>();
            gen.Cleanup = () =>
            {
                foreach (DungeonEncounterSpawner.SpawnResult s in spawns)
                    DungeonEncounterSpawner.CleanupSpawnEntries(s);
            };
            try
            {
                var ids = new List<string>();
                var widths = new List<int>();
                var rows = new List<string>();
                foreach ((int level, int roll, string label, string expect) in DmgCasterRows)
                {
                    EncounterDefinition e = SoakScenarios.RequireDmgRow(level, roll, expect, "row " + label);
                    List<string> standIns = SoakScenarios.ApplyMissingBaseStandIns(e);
                    DungeonEncounterSpawner.SpawnResult r = DungeonEncounterSpawner.PrepareEncounter(e);
                    spawns.Add(r);
                    if (r.Count == 0)
                        throw new InvalidOperationException("row " + label + " resolved no creature (" + string.Join("; ", r.Warnings) + ")");
                    for (int i = 0; i < r.Count; i++)
                    {
                        ids.Add(r.EnemyIds[i]);
                        NPCDefinition def = i < r.Definitions.Count ? r.Definitions[i] : null;
                        widths.Add(def != null ? Math.Max(1, def.SizeCategory.GetSpaceWidthSquares()) : 1);
                    }
                    rows.Add(e.Name + ": " + r.Count + (standIns.Count > 0 ? " [" + string.Join("; ", standIns) + "]" : "")
                             + (r.Warnings.Count > 0 ? " (" + string.Join("; ", r.Warnings) + ")" : ""));
                }

                var positions = SoakScenarios.PlaceEnemies(widths, new List<UnityEngine.Vector2Int> { new UnityEngine.Vector2Int(3, 10) });
                Dictionary<string, string> keyToSpawn = _spawnIds.GetOrCreateValue(ctx);
                keyToSpawn.Clear();
                for (int i = 0; i < ids.Count; i++)
                {
                    if (positions[i].x < 0)
                        throw new InvalidOperationException("no room on the grid for " + ids[i]);
                    string key = "c" + (i + 1);
                    keyToSpawn[key] = ids[i];
                    gen.Actors.Add(new ActorSpec
                    {
                        Key = key,
                        Team = CharacterTeam.Enemy,
                        Source = ActorSource.Npc(ids[i], "dmg:" + Label(ids[i])),
                        Pos = positions[i],
                        Control = Control.Ai
                    });
                }
                gen.Info.Set("rows", rows).Set("count", ids.Count);
                return gen;
            }
            catch
            {
                gen.Cleanup();
                throw;
            }
        }

        /// <summary>
        /// Notes, for every generated actor with a caster class level, its class and whether the spawn gave it spellcasting
        /// (GameManager.HasCastableSpellForPersonality, the spawn's own test) and its SpellcastingComponent holds a castable
        /// spell; and the totals. Always passes: the expectations read the notes.
        /// </summary>
        private static bool RecordCasterSpells(ScenarioContext ctx)
        {
            Dictionary<string, string> keyToSpawn = _spawnIds.GetOrCreateValue(ctx);
            int casters = 0, withSpells = 0;
            var detail = new List<string>();
            foreach (KeyValuePair<string, string> kv in keyToSpawn.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                CharacterController c = ctx.Get(kv.Key);
                if (c == null || c.Stats == null)
                    continue;
                string cls = CasterClasses.FirstOrDefault(k => c.Stats.GetClassLevel(k) > 0);
                if (cls == null)
                {
                    Fact(ctx, "other." + kv.Key, Label(kv.Value));
                    continue;
                }
                casters++;
                NPCDefinition def = NPCDatabase.Get(kv.Value);
                SpellcastingComponent sc = c.Spellcasting;
                bool given = def != null && GameManager.HasCastableSpellForPersonality(def, c);
                bool castable = given && sc != null && sc.GetCastablePreparedSpells().Count > 0;
                if (castable)
                    withSpells++;
                Fact(ctx, "class." + cls + "." + kv.Key, castable);
                detail.Add(Label(kv.Value) + " " + cls + " " + c.Stats.GetClassLevel(cls) + ": " + (castable ? "spells" : given ? "component without a castable spell" : "no spellcasting"));
            }
            Fact(ctx, "casters", casters);
            Fact(ctx, "withSpells", withSpells);
            Fact(ctx, "detail", string.Join("; ", detail));
            return true;
        }

        private static void Fact(ScenarioContext ctx, string key, object value)
            => ctx.Note(FactPrefix + key + "=" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));

        private static string FactOf(TraceView v, string key)
        {
            string prefix = FactPrefix + key + "=";
            TraceEvent note = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
            return note != null ? note.Str("text").Substring(prefix.Length) : null;
        }

        /// <summary>The number of facts whose key starts with <paramref name="keyPrefix"/>.</summary>
        private static int Count(TraceView v, string keyPrefix)
        {
            string prefix = FactPrefix + keyPrefix + ".";
            return v.Of("note").Count(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
        }

        /// <summary>"spawn_kobold_sorcerer_0_17" becomes "kobold_sorcerer_0" (the session counter is dropped).</summary>
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
    }
}
#endif
