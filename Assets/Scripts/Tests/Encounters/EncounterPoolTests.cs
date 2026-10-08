using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Tests.Encounters
{
/// <summary>
/// ENC-001: the encounter enemy pool and the grid state spawn placement reads. GameManager.EnsureNPCPoolSize must
/// leave only pool slots in GameManager.NPCs, so a creature that is not a pool slot but was left in the list (the
/// Greater Lion's Shield lion, CRE-005) is taken out of play instead of being taken over as an enemy; a hidden party
/// slot (GameManager.SetPCActiveState) holds no square, so spawn placement, which uses the grid's own
/// SquareGrid.CanPlaceCreature rule, may use it, and a shown one holds its square again.
/// Needs Play mode (a scene GameManager and grid). The test controllers sit in the grid's bottom-left corner and are
/// destroyed (immediately) afterwards; pool slots the test adds are removed again. Placement itself is covered by
/// EncounterSpawnPlacementTests and the scenarios rules/large-encounter-* (docs/TESTING.md 3.4).
/// </summary>
public static class EncounterPoolTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int StraySquare = new Vector2Int(0, 19);
    private static readonly Vector2Int HolderSquare = new Vector2Int(2, 17);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== ENCOUNTER POOL TESTS (ENC-001) ==========");

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.Grid == null)
        {
            Assert(false, "Encounter pool tests need Play mode with a scene GameManager and grid");
        }
        else
        {
            TestStrayNonPoolControllerIsEvicted(gm);
            TestHiddenPartySlotHoldsNoSquare(gm);
        }

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
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

    // ── Helpers ─────────────────────────────────────────────────────────

    private static CharacterController NewActor(string name, Vector2Int square)
    {
        var go = new GameObject(name);
        go.AddComponent<SpriteRenderer>();
        CharacterController cc = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(
            name: name, level: 1, characterClass: "Warrior",
            str: 10, dex: 10, con: 10, wis: 10, intelligence: 10, cha: 10,
            bab: 1, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 8);
        cc.Init(stats, square, null, null);
        return cc;
    }

    private static void Discard(GameManager gm, CharacterController cc)
    {
        if (cc == null)
            return;
        gm.Grid.ClearCreatureOccupancy(cc);
        // Immediate, so nothing is left at the scene root when the suite returns (the evicted stray is only marked
        // for destruction by the game's Destroy until the frame ends).
        Object.DestroyImmediate(cc.gameObject);
    }

    private static bool Holds(GameManager gm, CharacterController cc, Vector2Int square)
    {
        SquareCell cell = gm.Grid.GetCell(square);
        return cell != null && cell.Occupants.Contains(cc);
    }

    // ── Tests ───────────────────────────────────────────────────────────

    /// <summary>
    /// A controller that is not a pool slot (as the Lion's Shield lion is) sits in NPCs when a 16-creature-size request
    /// comes: it leaves the list and the grid, and the list grows to exactly the requested number of pool slots.
    /// </summary>
    private static void TestStrayNonPoolControllerIsEvicted(GameManager gm)
    {
        var before = new List<CharacterController>(gm.NPCs);
        int pool = before.Count(n => n != null && n.IsEncounterPoolSlot);
        CharacterController stray = NewActor("ENC001_StrayLion", StraySquare);
        gm.NPCs.Add(stray);
        try
        {
            Assert(Holds(gm, stray, StraySquare), "Control: the stray controller holds its grid square");
            Assert(!stray.IsEncounterPoolSlot, "Control: the stray controller is not a pool slot");

            gm.EnsureNPCPoolSize(pool + 1);

            Assert(!gm.NPCs.Contains(stray), "A non-pool controller left in NPCs is removed before the encounter (not taken over as an enemy)");
            Assert(gm.NPCs.All(n => n != null && n.IsEncounterPoolSlot), "After EnsureNPCPoolSize every NPCs entry is a pool slot");
            Assert(gm.NPCs.Count == pool + 1, "The pool grows to exactly the requested size",
                $"expected {pool + 1}, got {gm.NPCs.Count}");
            Assert(!Holds(gm, stray, StraySquare), "The removed controller no longer holds its grid square");
        }
        finally
        {
            for (int i = gm.NPCs.Count - 1; i >= 0; i--)
            {
                CharacterController n = gm.NPCs[i];
                if (before.Contains(n))
                    continue;
                gm.NPCs.RemoveAt(i);
                Discard(gm, n);
            }
            if (stray != null)
                Discard(gm, stray);
        }
    }

    /// <summary>
    /// A hidden party slot gives up its square (spawn placement may use it) and takes it back when shown again; while
    /// it is shown, a spawn asking for that square gives way to an adjacent one.
    /// </summary>
    private static void TestHiddenPartySlotHoldsNoSquare(GameManager gm)
    {
        MethodInfo setActive = typeof(GameManager).GetMethod("SetPCActiveState", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo resolve = typeof(GameManager).GetMethod("TryResolveSpawnSquare", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(setActive != null, "GameManager.SetPCActiveState exists");
        Assert(resolve != null, "GameManager.TryResolveSpawnSquare exists");
        if (setActive == null || resolve == null)
            return;

        CharacterController holder = NewActor("ENC001_PartyHolder", HolderSquare);
        CharacterController spawner = NewActor("ENC001_Spawner", new Vector2Int(0, 0));
        gm.Grid.ClearCreatureOccupancy(spawner);
        try
        {
            Vector2Int Resolve()
            {
                object[] args = { spawner, (Vector2Int?)HolderSquare, -1, 1, null };
                bool ok = (bool)resolve.Invoke(gm, args);
                return ok ? (Vector2Int)args[4] : new Vector2Int(-1, -1);
            }

            Vector2Int shownSpawn = Resolve();
            Assert(shownSpawn != HolderSquare && SquareGridUtils.GetChebyshevDistance(shownSpawn, HolderSquare) == 1,
                "A spawn asking for a shown party member's square gives way to an adjacent one", "got " + shownSpawn);

            setActive.Invoke(gm, new object[] { holder, false, null });
            Assert(!Holds(gm, holder, HolderSquare), "A hidden party slot holds no grid square");
            Assert(gm.Grid.CanPlaceCreature(HolderSquare, 1), "The grid's own rule sees the hidden slot's square as free");
            Assert(Resolve() == HolderSquare, "A spawn may take a hidden party member's square");

            setActive.Invoke(gm, new object[] { holder, true, null });
            Assert(Holds(gm, holder, HolderSquare), "A party slot shown again holds its square");
        }
        finally
        {
            Discard(gm, holder);
            Discard(gm, spawner);
        }
    }
}
}
