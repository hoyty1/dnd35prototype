#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// Spell routing golden suite (batch 11 step 2, docs/designs/cast_core_batch11.md): pins, for every SpellDatabase spell,
/// the data that decides how a cast is routed, so a data change in the cast-core refactor of batch 11 (reclassification,
/// target type, shape, SpellEffectRouting) cannot reroute a spell unnoticed.
/// Per spell: the EffectType after SpellCategoryClassifier.ReclassifyAll (run by SpellDatabase.Init), TargetType,
/// AoEShapeType, SpellEffectRouting.ReachesApplySpellBuff, the branch GameManager.BeginPendingSpellTargeting takes for a
/// PC cast (aoe, summon, swarm, wish, self, single; grease for Grease, whose branch is the caster's mode choice) and
/// whether the NPC cast path (GameManager.TryNPCPerformSpellCast, the cast core's Legality stage with the NpcRefusesArea
/// quirk) refuses it on data alone (an Area target type). Everything is computed
/// from data, without casting. The result is compared with tools/tests/spell-routing-golden.json; every changed, added
/// or missing spell is one failure naming the spell. After a deliberate routing change, regenerate the file with
/// <see cref="WriteGolden"/> (through the Unity MCP) and list the changed spells in the commit body.
/// The PC branch and the NPC refusal are hand copies of the decision order of those two methods (both carry a keep-in-step
/// comment), not calls into them: a change to the routing code itself that leaves the data alone passes here and must be
/// checked by the rules scenarios.
/// Runs in edit or Play mode.
/// </summary>
public static class SpellRoutingGoldenTests
{
    public const string GoldenRelativePath = "tools/tests/spell-routing-golden.json";

    private static int _passed;
    private static int _failed;

    public static string GoldenPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, GoldenRelativePath);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== SPELL ROUTING GOLDEN TESTS (batch 11) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SortedDictionary<string, string> current = BuildRows();
        Assert(current.Count > 0, "SpellDatabase has spells", "count " + current.Count);

        string path = GoldenPath;
        if (!File.Exists(path))
        {
            Assert(false, "golden file exists", GoldenRelativePath + " is missing; generate it with Tests.Magic.SpellRoutingGoldenTests.WriteGolden()");
        }
        else
        {
            SortedDictionary<string, string> golden = ReadGolden(path, out string error);
            Assert(error == null, "golden file parses", error ?? "");
            if (error == null)
                CompareRows(golden, current);
        }

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    /// <summary>
    /// Writes tools/tests/spell-routing-golden.json from the current data (edit or Play mode) and returns a one-line summary.
    /// Only for a deliberate routing change: list every changed spell in the commit body.
    /// </summary>
    public static string WriteGolden()
    {
        TestHelpers.EnsureCoreDatabasesInitialized();
        SortedDictionary<string, string> rows = BuildRows();
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"version\": 1,\n");
        sb.Append("  \"generator\": \"Tests.Magic.SpellRoutingGoldenTests.WriteGolden\",\n");
        sb.Append("  \"count\": ").Append(rows.Count).Append(",\n");
        sb.Append("  \"spells\": [\n");
        int i = 0;
        foreach (KeyValuePair<string, string> kv in rows)
        {
            sb.Append("    ").Append(kv.Value);
            sb.Append(++i < rows.Count ? ",\n" : "\n");
        }
        sb.Append("  ]\n}\n");
        string path = GoldenPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return "wrote " + rows.Count + " spells to " + GoldenRelativePath;
    }

    /// <summary>One JSON line per spell id, in ordinal id order.</summary>
    public static SortedDictionary<string, string> BuildRows()
    {
        var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
        List<SpellData> spells = SpellDatabase.GetAllSpells();
        if (spells == null)
            return rows;
        foreach (SpellData spell in spells)
        {
            if (spell == null || string.IsNullOrEmpty(spell.SpellId) || rows.ContainsKey(spell.SpellId))
                continue;
            rows[spell.SpellId] = Row(spell);
        }
        return rows;
    }

    private static string Row(SpellData spell)
    {
        var sb = new StringBuilder(160);
        sb.Append("{\"id\":\"").Append(Escape(spell.SpellId)).Append('"');
        sb.Append(",\"effectType\":\"").Append(spell.EffectType).Append('"');
        sb.Append(",\"targetType\":\"").Append(spell.TargetType).Append('"');
        sb.Append(",\"aoeShape\":\"").Append(spell.AoEShapeType).Append('"');
        sb.Append(",\"reachesApplySpellBuff\":").Append(SpellEffectRouting.ReachesApplySpellBuff(spell) ? "true" : "false");
        sb.Append(",\"pcBranch\":\"").Append(PcRoutingBranch(spell)).Append('"');
        sb.Append(",\"npcRefuses\":").Append(NpcRefusesOnData(spell) ? "true" : "false");
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// The branch of GameManager.BeginPendingSpellTargeting for this spell's data, in that method's order (the Resilient
    /// Sphere and grapple refusals depend on the caster, and the choice prompts for Disguise Self, Resist Energy,
    /// Protection from Energy, Fire Shield and the wall modes come back to the same method or cast on the caster). Since
    /// batch 11 step 23 the summons come before the area spells, which are routed by geometry (AoESystem.RoutesToArea:
    /// an Area target type or an area shape).
    /// </summary>
    public static string PcRoutingBranch(SpellData spell)
    {
        if (spell.SpellId == SpellNames.GREASE)
            return "grease";
        if (string.Equals(spell.SpellId, SpellNames.DISGUISE_SELF, StringComparison.Ordinal))
            return "self";
        if (SummoningService.IsSummonMonsterSpell(spell))
            return "summon";
        if (SummoningService.IsSummonSwarmSpell(spell))
            return "swarm";
        if (AoESystem.RoutesToArea(spell))
            return "aoe";
        if (string.Equals(spell.SpellId, SpellNames.WISH, StringComparison.Ordinal))
            return "wish";
        return spell.TargetType == SpellTargetType.Self ? "self" : "single";
    }

    /// <summary>
    /// The data refusal of the NPC cast path (an Area target type; AI-001): the cast core's Legality stage for a request
    /// with LegacyCastQuirk.NpcRefusesArea (GameManager.CheckCastRangeAndAreaLegality).
    /// </summary>
    public static bool NpcRefusesOnData(SpellData spell) => spell.TargetType == SpellTargetType.Area;

    private static void CompareRows(SortedDictionary<string, string> golden, SortedDictionary<string, string> current)
    {
        var changed = new List<string>();
        foreach (KeyValuePair<string, string> kv in current)
        {
            if (!golden.TryGetValue(kv.Key, out string was))
                Assert(false, "spell routing added: " + kv.Key, "now " + kv.Value);
            else if (was != kv.Value)
            {
                changed.Add(kv.Key);
                Assert(false, "spell routing changed: " + kv.Key, "golden " + was + ", now " + kv.Value);
            }
            else
                _passed++;
        }
        foreach (KeyValuePair<string, string> kv in golden)
            if (!current.ContainsKey(kv.Key))
                Assert(false, "spell routing removed: " + kv.Key, "golden " + kv.Value);

        Debug.Log($"  Spell routing: {current.Count} spells now, {golden.Count} in the golden file, {changed.Count} changed"
                  + (changed.Count > 0 ? ": " + string.Join(", ", changed) : ""));
    }

    /// <summary>Reads the spells array written by <see cref="WriteGolden"/>: one spell object per line, keyed by its id.</summary>
    private static SortedDictionary<string, string> ReadGolden(string path, out string error)
    {
        error = null;
        var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
        const string idPrefix = "{\"id\":\"";
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (!line.StartsWith(idPrefix, StringComparison.Ordinal))
                continue;
            if (line.EndsWith(",", StringComparison.Ordinal))
                line = line.Substring(0, line.Length - 1);
            int idEnd = line.IndexOf('"', idPrefix.Length);
            if (idEnd < 0)
            {
                error = "bad spell line: " + line;
                return rows;
            }
            string id = line.Substring(idPrefix.Length, idEnd - idPrefix.Length);
            if (rows.ContainsKey(id))
            {
                error = "duplicate spell id " + id;
                return rows;
            }
            rows[id] = line;
        }
        if (rows.Count == 0)
            error = "no spell lines";
        return rows;
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }
}
}
#endif
