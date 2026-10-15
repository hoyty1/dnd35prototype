#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 5 (docs/designs/cast_core_batch11.md, handler-registry-pc): GameManager's single-target spell handler
/// registry (GameManager.SpellHandlerRegistry.cs), which PerformSpellCast runs in place of its old cascade of handled
/// flags. Checks, on the registry's table (GameManager.DescribeSingleTargetHandlers, no instance needed):
/// every private GameManager method named TryResolve*SpellEffect, plus TryResolveSoundBurstStunEffect, found by
/// reflection, is registered exactly once with the handler signature, and the table names no other method; no spell id
/// is claimed by two entries (the condition under which the registry equals the old flag groups); every spell id exists
/// in SpellDatabase; and the order, gates, ContinueToBuff flags and spell ids equal the manifest below, re-derived on 2026-10-15
/// from the old chain's guard expressions. Also the gate evaluation, and (batch 11 step 6, which runs the registry on the
/// NPC cast path too) that the AI keeps out of its candidates exactly the registered spells whose handler needs a caster
/// choice it cannot give (SPL-091). Runs in edit or Play mode.
/// </summary>
public static class SpellHandlerRegistryTests
{
    private static int _passed;
    private static int _failed;

    private const string None = "None";
    private const string Ok = "Success";
    private const string OkKept = "SuccessNotNegated";

    /// <summary>
    /// The old PC chain (GameManager.SpellCasting.cs before batch 11 step 5), in order: handler, the guard it had
    /// besides the handled flags (None: no result check; Success: result.Success; SuccessNotNegated: result.Success
    /// and !effectNegatedBySave), and whether returning true still let ApplySpellBuff run (Shield Other, left out of
    /// anyClericHandled; the Sound Burst stun, which always returns false), and the one spell id its own guard (an id check
    /// or an Is*Spell predicate) accepts, read from each method on 2026-10-15.
    /// </summary>
    private static readonly (string name, string gate, bool continueToBuff, string id)[] Manifest =
    {
        ("TryResolveCauseFearSpellEffect", None, false, SpellNames.CAUSE_FEAR),
        ("TryResolveGhoulTouchSpellEffect", None, false, SpellNames.GHOUL_TOUCH),
        ("TryResolveScareSpellEffect", None, false, SpellNames.SCARE),
        ("TryResolveRayOfEnfeeblementSpellEffect", OkKept, false, SpellNames.RAY_OF_ENFEEBLEMENT),
        ("TryResolveTouchOfIdiocySpellEffect", OkKept, false, SpellNames.TOUCH_OF_IDIOCY),
        ("TryResolveMelfsAcidArrowSpellEffect", OkKept, false, SpellNames.MELFS_ACID_ARROW),
        ("TryResolveRayOfExhaustionSpellEffect", Ok, false, SpellNames.RAY_OF_EXHAUSTION),
        ("TryResolveVampiricTouchSpellEffect", Ok, false, SpellNames.VAMPIRIC_TOUCH),
        ("TryResolveEnervationSpellEffect", Ok, false, SpellNames.ENERVATION),
        ("TryResolveContagionSpellEffect", Ok, false, SpellNames.CONTAGION),
        ("TryResolveBestowCurseSpellEffect", Ok, false, SpellNames.BESTOW_CURSE),
        ("TryResolveGreaterInvisibilitySpellEffect", Ok, false, SpellNames.GREATER_INVISIBILITY),
        ("TryResolvePhantasmalKillerSpellEffect", Ok, false, SpellNames.PHANTASMAL_KILLER),
        ("TryResolveFireShieldSpellEffect", Ok, false, SpellNames.FIRE_SHIELD),
        ("TryResolveResilientSphereSpellEffect", OkKept, false, SpellNames.RESILIENT_SPHERE),
        ("TryResolveAnimateRopeSpellEffect", None, false, SpellNames.ANIMATE_ROPE),
        ("TryResolveMirrorImageSpellEffect", OkKept, false, SpellNames.MIRROR_IMAGE),
        ("TryResolveDimensionalAnchorSpellEffect", OkKept, false, SpellNames.DIMENSIONAL_ANCHOR),
        ("TryResolveRemoveCurseSpellEffect", Ok, false, SpellNames.REMOVE_CURSE),
        ("TryResolveDimensionDoorSpellEffect", Ok, false, SpellNames.DIMENSION_DOOR),
        ("TryResolveLesserGlobeSpellEffect", Ok, false, SpellNames.LESSER_GLOBE_OF_INVULNERABILITY),
        ("TryResolveDeathKnellSpellEffect", OkKept, false, SpellNames.DEATH_KNELL),
        ("TryResolveShieldOtherSpellEffect", OkKept, true, SpellNames.SHIELD_OTHER),
        ("TryResolveSilenceSpellEffect", OkKept, false, SpellNames.SILENCE),
        ("TryResolveSoundBurstStunEffect", None, true, SpellNames.SOUND_BURST),
        ("TryResolveSpiritualWeaponSpellEffect", Ok, false, SpellNames.SPIRITUAL_WEAPON),
        ("TryResolveAlignWeaponSpellEffect", OkKept, false, SpellNames.ALIGN_WEAPON),
        ("TryResolveSearingLightSpellEffect", Ok, false, SpellNames.SEARING_LIGHT),
        ("TryResolveInvisibilityPurgeSpellEffect", Ok, false, SpellNames.INVISIBILITY_PURGE),
        ("TryResolveRemoveDiseaseSpellEffect", Ok, false, SpellNames.REMOVE_DISEASE),
        ("TryResolvePrayerSpellEffect", Ok, false, SpellNames.PRAYER),
        ("TryResolveRemoveBlindnessDeafnessSpellEffect", Ok, false, SpellNames.REMOVE_BLINDNESS_DEAFNESS),
        ("TryResolveChaosHammerSpellEffect", Ok, false, SpellNames.CHAOS_HAMMER),
        ("TryResolveHolySmiteSpellEffect", Ok, false, SpellNames.HOLY_SMITE),
        ("TryResolveOrdersWrathSpellEffect", Ok, false, SpellNames.ORDERS_WRATH),
        ("TryResolveUnholyBlightSpellEffect", Ok, false, SpellNames.UNHOLY_BLIGHT),
        ("TryResolveDeathWardSpellEffect", Ok, false, SpellNames.DEATH_WARD),
        ("TryResolveDivinePowerSpellEffect", Ok, false, SpellNames.DIVINE_POWER),
        ("TryResolveFreedomOfMovementSpellEffect", Ok, false, SpellNames.FREEDOM_OF_MOVEMENT),
        ("TryResolveSpellImmunitySpellEffect", Ok, false, SpellNames.SPELL_IMMUNITY),
        ("TryResolveNeutralizePoisonSpellEffect", Ok, false, SpellNames.NEUTRALIZE_POISON),
        ("TryResolvePoisonSpellEffect", Ok, false, SpellNames.POISON),
        ("TryResolveDismissalSpellEffect", Ok, false, SpellNames.DISMISSAL),
        ("TryResolveRepelVerminSpellEffect", Ok, false, SpellNames.REPEL_VERMIN),
        ("TryResolveImbueWithSpellAbilitySpellEffect", Ok, false, SpellNames.IMBUE_WITH_SPELL_ABILITY),
        ("TryResolveHoldAnimalSpellEffect", OkKept, false, SpellNames.HOLD_ANIMAL),
        ("TryResolveProduceFlameSpellEffect", Ok, false, SpellNames.PRODUCE_FLAME),
        ("TryResolveHeatMetalSpellEffect", OkKept, false, SpellNames.HEAT_METAL),
        ("TryResolveMagicVestmentSpellEffect", OkKept, false, SpellNames.MAGIC_VESTMENT),
        ("TryResolveDominateAnimalSpellEffect", OkKept, false, SpellNames.DOMINATE_ANIMAL),
        ("TryResolveCommandPlantsSpellEffect", OkKept, false, SpellNames.COMMAND_PLANTS),
    };

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== SPELL HANDLER REGISTRY TESTS (batch 11 steps 5-6) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        IReadOnlyList<SpellHandlerEntry> entries = GameManager.DescribeSingleTargetHandlers();
        Assert(entries != null && entries.Count > 0, "registry has entries", entries != null ? "count " + entries.Count : "null");
        if (entries == null)
        {
            Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
            return;
        }

        TestEveryHandlerMethodRegisteredOnce(entries);
        TestSpellIdsDisjointAndKnown(entries);
        TestManifest(entries);
        TestGates();
        TestAiCandidateExclusions(entries);

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    private static readonly Regex HandlerName = new Regex(@"^TryResolve\w+SpellEffect$", RegexOptions.CultureInvariant);

    private static void TestEveryHandlerMethodRegisteredOnce(IReadOnlyList<SpellHandlerEntry> entries)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (SpellHandlerEntry e in entries)
        {
            Assert(e != null && !string.IsNullOrEmpty(e.Name) && e.Resolve != null && e.SpellIds != null && e.SpellIds.Count > 0,
                "entry is complete (name, resolve, spell ids)", e != null ? e.Name : "null entry");
            if (e == null || e.Name == null)
                continue;
            counts.TryGetValue(e.Name, out int n);
            counts[e.Name] = n + 1;
        }

        var methods = new List<MethodInfo>();
        foreach (MethodInfo m in typeof(GameManager).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (HandlerName.IsMatch(m.Name) || m.Name == "TryResolveSoundBurstStunEffect")
                methods.Add(m);
        }
        Assert(methods.Count > 0, "reflection finds the handler methods", "found " + methods.Count);

        var methodNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (MethodInfo m in methods)
        {
            methodNames.Add(m.Name);
            ParameterInfo[] p = m.GetParameters();
            bool signature = m.IsPrivate && !m.IsStatic && m.ReturnType == typeof(bool) && p.Length == 4
                && p[0].ParameterType == typeof(CharacterController) && p[1].ParameterType == typeof(CharacterController)
                && p[2].ParameterType == typeof(SpellData) && p[3].ParameterType == typeof(SpellResult);
            Assert(signature, "handler has the private (caster, target, spell, result) -> bool signature: " + m.Name);
            counts.TryGetValue(m.Name, out int n);
            Assert(n == 1, "handler registered exactly once: " + m.Name, "registered " + n + " times");
        }

        foreach (KeyValuePair<string, int> kv in counts)
            Assert(methodNames.Contains(kv.Key), "registry entry names an existing handler method: " + kv.Key);

        Assert(entries.Count == methods.Count, "registry size equals the number of handler methods",
            "entries " + entries.Count + ", methods " + methods.Count);
    }

    private static void TestSpellIdsDisjointAndKnown(IReadOnlyList<SpellHandlerEntry> entries)
    {
        var owner = new Dictionary<string, string>(StringComparer.Ordinal);
        bool disjoint = true;
        foreach (SpellHandlerEntry e in entries)
        {
            if (e?.SpellIds == null)
                continue;
            foreach (string id in e.SpellIds)
            {
                if (owner.TryGetValue(id, out string other))
                {
                    disjoint = false;
                    Assert(false, "spell id claimed by one entry only: " + id, other + " and " + e.Name);
                }
                else
                {
                    owner[id] = e.Name;
                }

                SpellData spell = string.IsNullOrEmpty(id) ? null : SpellDatabase.GetSpell(id);
                Assert(spell != null && spell.SpellId == id, "registered spell id exists in SpellDatabase: " + id, e.Name);
            }
        }
        Assert(disjoint, "no spell id is claimed by two entries (the registry then equals the old flag groups)");
    }

    private static void TestManifest(IReadOnlyList<SpellHandlerEntry> entries)
    {
        Assert(entries.Count == Manifest.Length, "registry has the manifest's entries", "entries " + entries.Count + ", manifest " + Manifest.Length);
        int n = Math.Min(entries.Count, Manifest.Length);
        for (int i = 0; i < n; i++)
        {
            SpellHandlerEntry e = entries[i];
            (string name, string gate, bool continueToBuff, string id) row = Manifest[i];
            Assert(e != null && e.Name == row.name, "entry " + (i + 1) + " is in the old chain order: " + row.name, e != null ? e.Name : "null");
            if (e == null || e.Name != row.name)
                continue;
            Assert(e.Gate.ToString() == row.gate, "gate matches the old guard: " + row.name, "registry " + e.Gate + ", manifest " + row.gate);
            Assert(e.ContinueToBuff == row.continueToBuff, "ContinueToBuff matches: " + row.name, "registry " + e.ContinueToBuff);
            Assert(e.SpellIds != null && e.SpellIds.SetEquals(new[] { row.id }), "spell ids equal the id the method's guard accepts: " + row.name,
                "registry " + (e.SpellIds != null ? string.Join(",", e.SpellIds) : "null") + ", manifest " + row.id);
        }
    }

    private static void TestGates()
    {
        var success = new SpellResult { Success = true };
        var failure = new SpellResult { Success = false };
        Assert(GameManager.HandlerGatePasses(HandlerGate.None, failure, true), "gate None passes a failed, negated cast");
        Assert(GameManager.HandlerGatePasses(HandlerGate.Success, success, true), "gate Success passes a successful cast even when negated");
        Assert(!GameManager.HandlerGatePasses(HandlerGate.Success, failure, false), "gate Success fails a failed cast");
        Assert(GameManager.HandlerGatePasses(HandlerGate.SuccessNotNegated, success, false), "gate SuccessNotNegated passes a successful cast not negated");
        Assert(!GameManager.HandlerGatePasses(HandlerGate.SuccessNotNegated, success, true), "gate SuccessNotNegated fails a negated cast");
        Assert(!GameManager.HandlerGatePasses(HandlerGate.SuccessNotNegated, failure, false), "gate SuccessNotNegated fails a failed cast");
    }

    /// <summary>
    /// Batch 11 step 6: the NPC cast path runs the registry too, so the AI keeps out of its candidates exactly the spells
    /// whose handler needs a caster choice it cannot give yet (Dimension Door, Imbue with Spell Ability; SPL-091), and
    /// no other registered spell.
    /// </summary>
    private static void TestAiCandidateExclusions(IReadOnlyList<SpellHandlerEntry> entries)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal) { SpellNames.DIMENSION_DOOR, SpellNames.IMBUE_WITH_SPELL_ABILITY };
        foreach (SpellHandlerEntry e in entries)
        {
            if (e == null || e.SpellIds == null)
                continue;
            foreach (string id in e.SpellIds)
            {
                bool expected = excluded.Contains(id);
                bool actual = AISpellcastingStrategist.NeedsCasterChoiceAiCannotGive(SpellDatabase.GetSpell(id));
                Assert(actual == expected, (expected ? "the AI leaves out " : "the AI may pick ") + id + " (" + e.Name + ")",
                    "NeedsCasterChoiceAiCannotGive=" + actual);
            }
        }
        Assert(!AISpellcastingStrategist.NeedsCasterChoiceAiCannotGive(null), "a null spell is not excluded");
    }

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
