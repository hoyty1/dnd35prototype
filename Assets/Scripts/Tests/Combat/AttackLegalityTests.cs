using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The one "may this creature directly attack that one" gate, AttackLegality.CanDirectlyAttack, and the ward save ledger,
/// WardSaveLedger (batch 12 step 12; CMB-092, CMB-093, CMB-104, AI-005; decision D6), as a pure table, for any attacker:
/// - Otiluke's Resilient Sphere blocks every kind of direct attack in both directions, not inside one sphere (PHB p.258);
/// - the Improved Grab hold bar refuses every attack on the held creature but not a spell (MM p.310, CMB-163);
/// - maneuver reach (PHB p.155-158): trip and grapple are unarmed melee touch attacks at natural reach, disarm and sunder
///   use the weapon's reach, so nothing at 10 ft without reach, and a longspear trips an adjacent foe but disarms only at
///   10 ft;
/// - the ledger: Predict never rolls; Commit rolls the Will save once per (attacker, casting) and keeps the result (a
///   failed save refuses every later attack on that creature, AoOs included; a new casting rolls again); a spell meets a
///   ward only when it is hostile, whatever the teams; Hide from Undead binds undead only, a mindless undead never sees
///   through it, an intelligent undead's one save is rolled at its perception step or first attack, a failure leaves the
///   creature unseen (total concealment) without losing an attack, and a success does not end the spell (PHB p.274, p.241).
/// Dice are forced through ScenarioHooks.RollFilter. Play mode only: CharacterController.Init needs Awake (TST-009).
/// </summary>
public static class AttackLegalityTests
{
    private static int _passed;
    private static int _failed;
    private static readonly List<Object> _created = new List<Object>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== ATTACK LEGALITY TESTS (CMB-092, CMB-093, CMB-104, AI-005, D6) ==========");

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();
        ItemDatabase.Init();

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            Run(TestSphereBothWays);
            Run(TestHoldBar);
            Run(TestManeuverReachUnarmed);
            Run(TestManeuverReachLongspear);
            Run(TestManeuverReachTripWeaponAndChosenHand);
            Run(TestDispelReachesSphere);
            Run(TestSanctuaryPredictAndCommit);
            Run(TestSanctuarySpellOnlyAgainstOpponents);
            Run(TestHideFromUndead);
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            WardSaveLedger.Clear();
            foreach (Object o in _created)
                if (o != null)
                    Object.DestroyImmediate(o is Component comp ? comp.gameObject : o);
            _created.Clear();
        }

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Run(System.Action test)
    {
        try
        {
            WardSaveLedger.Clear();
            test();
        }
        catch (System.Exception ex)
        {
            Assert(false, $"{test.Method.Name} threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = null;
        }
    }

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}");
        }
    }

    private static CharacterController Create(string name, Vector2Int pos, CharacterTeam team)
    {
        var go = new GameObject($"{name}_GO");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        var stats = new CharacterStats(name, 3, "Fighter", 14, 12, 12, 10, 10, 10, 3, 0, 0, 8, 1, 0, 8, 1, 18);
        controller.Init(stats, pos, null, null);
        inventory.Init(stats);
        controller.SetTeam(team);
        controller.GridPosition = pos;
        controller.StartNewTurn();
        _created.Add(controller);
        return controller;
    }

    private static void Equip(CharacterController c, ItemData item)
    {
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        inv.CharacterInventory.DirectEquip(item, EquipSlot.RightHand);
        inv.CharacterInventory.RecalculateStats();
    }

    private static ResilientSphereAreaEffect Sphere(params Vector2Int[] cells)
    {
        var go = new GameObject("AttackLegalityTests_Sphere");
        ResilientSphereAreaEffect sphere = go.AddComponent<ResilientSphereAreaEffect>();
        foreach (Vector2Int cell in cells)
            sphere.AffectedCells.Add(cell);
        AreaEffectManager.Instance.RegisterAreaEffect(sphere);
        _created.Add(sphere);
        return sphere;
    }

    private static void RemoveSphere(ResilientSphereAreaEffect sphere)
    {
        if (sphere == null)
            return;
        AreaEffectManager.Instance.UnregisterAreaEffect(sphere);
        Object.DestroyImmediate(sphere.gameObject);
    }

    private static System.Func<int, string, int, int> ForceSave(string effect, int value)
        => (sides, ctx, natural) => sides == 20 && ctx != null && ctx.Contains("save vs " + effect) ? value : natural;

    private static ActiveSpellEffect Ward(CharacterController target, string spellId, int dc)
    {
        SpellData spell = SpellDatabase.GetSpell(spellId).Clone();
        var effect = new ActiveSpellEffect(spell, "Test Cleric", 5, target.Stats.CharacterName);
        if (spellId == SpellNames.SANCTUARY)
        {
            target.Stats.SanctuaryActive = true;
            target.Stats.SanctuaryDC = dc;
            target.Stats.ActiveSanctuaryEffect = effect;
        }
        else
        {
            target.Stats.HideFromUndeadActive = true;
            target.Stats.HideFromUndeadDC = dc;
            target.Stats.ActiveHideFromUndeadEffect = effect;
        }
        return effect;
    }

    // ── The sphere ─────────────────────────────────────────────────────

    private static void TestSphereBothWays()
    {
        var a = Create("AL_SphereA", new Vector2Int(2, 2), CharacterTeam.Player);
        var b = Create("AL_SphereB", new Vector2Int(3, 2), CharacterTeam.Enemy);

        Assert(AttackLegality.CanDirectlyAttack(a, b, DirectAttackKind.Weapon).Ok && AttackLegality.CanDirectlyAttack(b, a, DirectAttackKind.Weapon).Ok,
            "Without a sphere two adjacent foes may attack each other");

        ResilientSphereAreaEffect sphere = Sphere(new Vector2Int(2, 2));
        try
        {
            bool allOut = true, allIn = true;
            foreach (DirectAttackKind kind in new[] { DirectAttackKind.Weapon, DirectAttackKind.Natural, DirectAttackKind.Touch,
                         DirectAttackKind.AoO, DirectAttackKind.Charge, DirectAttackKind.Spell })
            {
                allOut &= !AttackLegality.CanDirectlyAttack(a, b, kind).Ok;
                allIn &= !AttackLegality.CanDirectlyAttack(b, a, kind).Ok;
            }
            allOut &= !AttackLegality.CanDirectlyAttack(a, b, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(SpecialAttackType.Trip)).Ok;
            allIn &= !AttackLegality.CanDirectlyAttack(b, a, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(SpecialAttackType.Grapple)).Ok;
            Assert(allOut, "Nothing the sphered creature does passes the boundary outward: weapon, natural, touch, maneuver, AoO, charge, spell (PHB p.258)");
            Assert(allIn, "Nothing passes the boundary inward either: the creature outside cannot attack or target the sphered one (CMB-092)");
            Assert(AttackLegality.CanDirectlyAttack(a, a, DirectAttackKind.Spell).Ok, "A self-targeted spell inside the sphere stays legal");

            sphere.AffectedCells.Add(new Vector2Int(3, 2));
            Assert(AttackLegality.CanDirectlyAttack(a, b, DirectAttackKind.Weapon).Ok && AttackLegality.CanDirectlyAttack(b, a, DirectAttackKind.Spell).Ok,
                "Two creatures inside the same sphere attack and target each other normally");
        }
        finally
        {
            RemoveSphere(sphere);
        }
    }

    // ── The hold bar ───────────────────────────────────────────────────

    private static void TestHoldBar()
    {
        var holder = Create("AL_Holder", new Vector2Int(0, 0), CharacterTeam.Player);
        var held = Create("AL_Held", new Vector2Int(1, 0), CharacterTeam.Enemy);
        var other = Create("AL_Other", new Vector2Int(0, 1), CharacterTeam.Enemy);
        holder.Stats.BaseAttackBonusOverride = 11;
        holder.Stats.NaturalAttacks.Clear();
        holder.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
        holder.Stats.HasImprovedGrab = true;
        holder.Stats.ImprovedGrabTriggerAttackName = "Bite";
        holder.Stats.CurrentSizeCategory = SizeCategory.Large;
        held.Stats.AdjustMaxHP(200);
        held.Stats.CurrentHP += 200;

        int n = 0;
        ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == "Grapple check" ? (n++ % 2 == 0 ? 20 : 1) : natural;
        SpecialAttackResult hold = holder.ResolveImprovedGrabFreeAttempt(held, holder.GetNaturalAttackByWeaponName("Bite"), holdWithBodyPart: true);
        ScenarioHooks.RollFilter = null;
        Assert(hold != null && hold.Success && holder.IsHoldingWithImprovedGrab(), "Setup: the holder holds its foe with the bite (MM p.310)");

        Assert(!AttackLegality.CanDirectlyAttack(holder, held, DirectAttackKind.Weapon).Ok
               && !AttackLegality.CanDirectlyAttack(holder, held, DirectAttackKind.AoO).Ok
               && !AttackLegality.CanDirectlyAttack(holder, held, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(SpecialAttackType.Trip)).Ok,
            "The holder may not attack the creature it holds with one body part, by weapon, AoO or trip (MM p.310, CMB-163)");
        Assert(AttackLegality.CanDirectlyAttack(holder, held, DirectAttackKind.Spell).Ok,
            "A spell is not an attack, so the hold bar does not refuse it");
        Assert(AttackLegality.Check(holder, other, DirectAttackKind.Weapon, null, AttackLegalityChecks.HoldBar).Ok,
            "The holder's other attacks go on against other opponents");
    }

    // ── Maneuver reach ─────────────────────────────────────────────────

    private static bool Can(CharacterController a, CharacterController t, SpecialAttackType type)
        => AttackLegality.CanDirectlyAttack(a, t, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(type)).Ok;

    private static void TestManeuverReachUnarmed()
    {
        var a = Create("AL_ReachA", new Vector2Int(5, 5), CharacterTeam.Player);
        var far = Create("AL_ReachFar", new Vector2Int(7, 5), CharacterTeam.Enemy);
        var near = Create("AL_ReachNear", new Vector2Int(6, 6), CharacterTeam.Enemy);
#pragma warning disable CS0618 // the longsword's string id
        Equip(a, ItemDatabase.CloneItem(ItemIDs.LONGSWORD));
#pragma warning restore CS0618

        Assert(!Can(a, far, SpecialAttackType.Trip) && !Can(a, far, SpecialAttackType.Disarm)
               && !Can(a, far, SpecialAttackType.Sunder) && !Can(a, far, SpecialAttackType.Grapple),
            "From 10 ft without reach, no trip, disarm, sunder or grapple (PHB p.155-158; CMB-104)");
        Assert(Can(a, near, SpecialAttackType.Trip) && Can(a, near, SpecialAttackType.Disarm)
               && Can(a, near, SpecialAttackType.Sunder) && Can(a, near, SpecialAttackType.Grapple),
            "An adjacent foe (diagonal) can be tripped, disarmed, sundered and grappled");
    }

    private static void TestManeuverReachLongspear()
    {
        var a = Create("AL_SpearA", new Vector2Int(5, 5), CharacterTeam.Player);
        var far = Create("AL_SpearFar", new Vector2Int(7, 5), CharacterTeam.Enemy);
        var near = Create("AL_SpearNear", new Vector2Int(6, 5), CharacterTeam.Enemy);
#pragma warning disable CS0618 // the longspear has no ItemID value
        Equip(a, ItemDatabase.CloneItem(ItemIDs.LONGSPEAR));
#pragma warning restore CS0618

        Assert(Can(a, near, SpecialAttackType.Trip) && Can(a, near, SpecialAttackType.Grapple),
            "A longspear wielder trips and grapples an adjacent foe: both are unarmed melee touch attacks (PHB p.155-158; CMB-104)");
        Assert(!Can(a, near, SpecialAttackType.Disarm) && Can(a, far, SpecialAttackType.Disarm),
            "The longspear disarms at its reach of 10 ft, not adjacent (PHB p.113, p.155)");
        Assert(!Can(a, far, SpecialAttackType.Trip) && !Can(a, far, SpecialAttackType.Grapple),
            "Trip and grapple at 10 ft need natural reach, which the longspear does not give");
        Assert(AttackLegality.CanDirectlyAttack(a, far, DirectAttackKind.Weapon).Ok && !AttackLegality.CanDirectlyAttack(a, near, DirectAttackKind.Weapon).Ok,
            "The longspear's own attack reaches 10 ft and not an adjacent foe");
    }

    private static void TestManeuverReachTripWeaponAndChosenHand()
    {
        var a = Create("AL_HalberdA", new Vector2Int(5, 5), CharacterTeam.Player);
        var far = Create("AL_HalberdFar", new Vector2Int(7, 5), CharacterTeam.Enemy);
        var near = Create("AL_HalberdNear", new Vector2Int(6, 5), CharacterTeam.Enemy);
#pragma warning disable CS0618 // string item ids
        ItemData halberd = ItemDatabase.CloneItem(ItemIDs.HALBERD);
        ItemData longspear = ItemDatabase.CloneItem(ItemIDs.LONGSPEAR);
        ItemData longsword = ItemDatabase.CloneItem(ItemIDs.LONGSWORD);
        bool flags = halberd.CanMakeTripAttacks && ItemDatabase.CloneItem(ItemIDs.WHIP).CanMakeTripAttacks
                     && ItemDatabase.CloneItem(ItemIDs.SICKLE).CanMakeTripAttacks && !longspear.CanMakeTripAttacks && !longsword.CanMakeTripAttacks;
#pragma warning restore CS0618
        Assert(flags, "The PHB trip weapons carry the trip-weapon flag through CloneItem; a longspear and a longsword do not (PHB p.115-122)");

        Equip(a, halberd);
        Assert(Can(a, far, SpecialAttackType.Trip) && Can(a, near, SpecialAttackType.Trip),
            "A halberd trips at its 10-ft reach (a trip weapon), and an adjacent foe by the unarmed trip (PHB p.119, p.158; CMB-104)");
        Assert(!Can(a, far, SpecialAttackType.Grapple), "A trip weapon's reach does not help a grapple");

        // Disarm and sunder test the hand chosen: the weapon used, or natural reach for an unarmed attempt.
        Equip(a, longspear);
        Assert(!AttackLegality.CanDirectlyAttack(a, far, DirectAttackKind.Maneuver, AttackIntent.ForManeuverWith(SpecialAttackType.Disarm, longsword)).Ok
               && AttackLegality.CanDirectlyAttack(a, far, DirectAttackKind.Maneuver, AttackIntent.ForManeuverWith(SpecialAttackType.Disarm, longspear)).Ok,
            "A disarm with a 5-ft weapon in the chosen hand does not reach 10 ft, whatever the other hand holds");
        Assert(AttackLegality.CanDirectlyAttack(a, near, DirectAttackKind.Maneuver, AttackIntent.ForManeuverWith(SpecialAttackType.Sunder, null)).Ok
               && !AttackLegality.CanDirectlyAttack(a, far, DirectAttackKind.Maneuver, AttackIntent.ForManeuverWith(SpecialAttackType.Sunder, null)).Ok,
            "An unarmed sunder reaches as far as natural reach (PHB p.158)");
    }

    // ── Dispel Magic and the sphere ────────────────────────────────────

    private static void TestDispelReachesSphere()
    {
        var cleric = Create("AL_DispelCleric", new Vector2Int(2, 4), CharacterTeam.Player);
        var friend = Create("AL_DispelFriend", new Vector2Int(3, 4), CharacterTeam.Player);
        SpellData dispel = SpellDatabase.GetSpell(SpellNames.DISPEL_MAGIC);
        SpellData cure = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS);
        ResilientSphereAreaEffect sphere = Sphere(new Vector2Int(3, 4));
        try
        {
            Assert(GameManager.IsTargetedDispelAtSphere(cleric, friend, dispel)
                   && !GameManager.IsTargetedDispelAtSphere(cleric, friend, cure)
                   && !GameManager.IsTargetedDispelAtSphere(friend, cleric, dispel),
                "A targeted Dispel Magic from outside at a sphered creature reaches the sphere (PHB p.258); nothing else does, and not outward");
            Assert(!AttackLegality.CanDirectlyAttack(cleric, friend, DirectAttackKind.Spell, AttackIntent.ForSpell(cure)).Ok
                   && AttackLegality.Check(cleric, friend, DirectAttackKind.Spell, AttackIntent.ForSpell(dispel), AttackLegalityChecks.All & ~AttackLegalityChecks.Sphere).Ok,
                "The cast core's gate refuses a cure into the sphere and lets the dispel through (it drops only the sphere test)");
        }
        finally
        {
            RemoveSphere(sphere);
        }
    }

    // ── The ward ledger ────────────────────────────────────────────────

    private static void TestSanctuaryPredictAndCommit()
    {
        var foe = Create("AL_SancFoe", new Vector2Int(0, 0), CharacterTeam.Enemy);
        var cleric = Create("AL_SancCleric", new Vector2Int(1, 0), CharacterTeam.Player);
        Ward(cleric, SpellNames.SANCTUARY, 15);

        int rolled = WardSaveLedger.SavesRolled;
        Legality before = AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Weapon);
        bool knownBefore = WardSaveLedger.HasKnownFailure(foe, cleric);
        float score = AttackLegality.WardTargetScoreAdjustment(foe, cleric);
        Assert(before.Ok && before.Risk == LegalityRisk.WardSaveUnrolled && !knownBefore && score < 0f && WardSaveLedger.SavesRolled == rolled,
            $"Before any attempt the ward is legal but unrolled, scores lower, and predicting rolls nothing (risk {before.Risk}, score {score})");

        ScenarioHooks.RollFilter = ForceSave("Sanctuary", 2);
        WardVerdict first = AttackLegality.CommitWard(foe, cleric, DirectAttackKind.Weapon);
        Assert(first == WardVerdict.Failed && WardSaveLedger.SavesRolled == rolled + 1,
            $"The first attempt rolls the Will save once and a failure loses the attack (PHB p.274; got {first})");

        ScenarioHooks.RollFilter = ForceSave("Sanctuary", 20);
        WardVerdict second = AttackLegality.CommitWard(foe, cleric, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(SpecialAttackType.Trip));
        Legality after = AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Weapon);
        Assert(second == WardVerdict.Failed && WardSaveLedger.SavesRolled == rolled + 1 && !after.Ok,
            "One save per casting: a later attack (a trip) rolls nothing and fails again; the gate refuses the creature for the duration");
        Assert(WardSaveLedger.HasKnownFailure(foe, cleric) && !AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.AoO).Ok,
            "Nor may the foe take an AoO against it (the known failure, which ThreatSystem.CanMakeAoOAgainst reads)");

        Ward(cleric, SpellNames.SANCTUARY, 15); // a new casting
        Legality recast = AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Weapon);
        WardVerdict third = AttackLegality.CommitWard(foe, cleric, DirectAttackKind.Weapon);
        WardVerdict fourth = AttackLegality.CommitWard(foe, cleric, DirectAttackKind.AoO);
        Assert(recast.Ok && recast.Risk == LegalityRisk.WardSaveUnrolled && third == WardVerdict.Passed && fourth == WardVerdict.Passed
               && WardSaveLedger.SavesRolled == rolled + 2,
            "A new casting is a new save: it rolls once more, and a success leaves the foe unaffected by that casting (no further rolls)");

        WardSaveLedger.ForgetAttacker(foe);
        Assert(AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Weapon).Risk == LegalityRisk.WardSaveUnrolled,
            "A reused slot forgets its old creature's saves (ForgetAttacker)");

        // A cast cancelled at the AoO prompt attempted nothing: the cast core forgets the save its Ward stage rolled.
        ScenarioHooks.RollFilter = ForceSave("Sanctuary", 20);
        WardVerdict cancelled = AttackLegality.CommitWard(foe, cleric, DirectAttackKind.Spell);
        WardSaveLedger.ForgetSanctuarySave(foe, cleric);
        Assert(cancelled == WardVerdict.Passed && AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Weapon).Risk == LegalityRisk.WardSaveUnrolled,
            "A cancelled cast's Sanctuary save is forgotten, so the next attempt rolls again (ForgetSanctuarySave, SPL-152)");
    }

    private static void TestSanctuarySpellOnlyAgainstOpponents()
    {
        var friend = Create("AL_SancFriend", new Vector2Int(0, 0), CharacterTeam.Player);
        var foe = Create("AL_SancSpellFoe", new Vector2Int(2, 0), CharacterTeam.Enemy);
        var cleric = Create("AL_SancSpellCleric", new Vector2Int(1, 0), CharacterTeam.Player);
        Ward(cleric, SpellNames.SANCTUARY, 15);

        int rolled = WardSaveLedger.SavesRolled;
        WardVerdict ally = AttackLegality.CommitWard(friend, cleric, DirectAttackKind.Spell);
        Assert(ally == WardVerdict.NoWard && WardSaveLedger.SavesRolled == rolled
               && AttackLegality.CanDirectlyAttack(friend, cleric, DirectAttackKind.Spell).Risk == LegalityRisk.None,
            "An ally's spell on the warded creature is no attack: no save");
        Assert(AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Spell).Risk == LegalityRisk.WardSaveUnrolled,
            "An opponent's targeted spell meets the ward (PHB p.274 \"even with a targeted spell\")");
        Assert(AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(SpecialAttackType.Feint)).Risk == LegalityRisk.None,
            "A feint is not an attack, so it meets no ward");

        // The spell's hostility decides, not the teams (PHB p.274 binds only a creature that attempts to attack).
        SpellData cure = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS);
        SpellData hold = SpellDatabase.GetSpell(SpellNames.HOLD_PERSON);
        Assert(cure != null && hold != null
               && AttackLegality.CommitWard(foe, cleric, DirectAttackKind.Spell, AttackIntent.ForSpell(cure)) == WardVerdict.NoWard
               && AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Spell, AttackIntent.ForSpell(cure)).Risk == LegalityRisk.None
               && WardSaveLedger.SavesRolled == rolled,
            "A cure cast by an opponent on the warded creature is no attack: no save");
        Assert(AttackLegality.CanDirectlyAttack(friend, cleric, DirectAttackKind.Spell, AttackIntent.ForSpell(hold)).Risk == LegalityRisk.WardSaveUnrolled,
            "A hostile spell from the warded creature's own side (a confused ally) meets the ward");
        Assert(AttackLegality.CanDirectlyAttack(foe, cleric, DirectAttackKind.Spell, AttackIntent.ForSpell(hold)).Risk == LegalityRisk.WardSaveUnrolled,
            "An opponent's Hold Person meets the ward");
    }

    private static void TestHideFromUndead()
    {
        var zombie = Create("AL_Zombie", new Vector2Int(0, 0), CharacterTeam.Enemy);
        var wight = Create("AL_Wight", new Vector2Int(0, 1), CharacterTeam.Enemy);
        var orc = Create("AL_Orc", new Vector2Int(2, 1), CharacterTeam.Enemy);
        var hidden = Create("AL_Hidden", new Vector2Int(1, 0), CharacterTeam.Player);
        zombie.Stats.CreatureType = "Undead";
        zombie.Stats.IsMindless = true;
        wight.Stats.CreatureType = "Undead";
        wight.Stats.IsMindless = false;
        Ward(hidden, SpellNames.HIDE_FROM_UNDEAD, 14);

        int rolled = WardSaveLedger.SavesRolled;
        Assert(!AttackLegality.CanDirectlyAttack(zombie, hidden, DirectAttackKind.Weapon).Ok
               && AttackLegality.CommitWard(zombie, hidden, DirectAttackKind.Weapon) == WardVerdict.Failed
               && WardSaveLedger.SavesRolled == rolled,
            "A mindless undead never perceives the warded creature: no save (PHB p.241)");
        Assert(AttackLegality.CommitWard(orc, hidden, DirectAttackKind.Weapon) == WardVerdict.NoWard,
            "Hide from Undead does not bind a living attacker");

        // Before its save an intelligent undead cannot perceive the warded creature: total concealment, but no refusal
        // and no attack lost (PHB p.241: it may still try to strike an unseen foe it suspects; delegated RAW reading D6).
        Legality wightBefore = AttackLegality.CanDirectlyAttack(wight, hidden, DirectAttackKind.Weapon);
        Assert(wightBefore.Ok && wightBefore.Risk == LegalityRisk.None
               && WardSaveLedger.IsHiddenFromUndead(wight, hidden) && hidden.HasTotalConcealment(wight)
               && !wight.CanVisuallyPerceive(hidden) && !hidden.HasTotalConcealment(orc),
            "Before its save an intelligent undead does not perceive the warded creature (total concealment), and is not barred from attacking");

        var ghast = Create("AL_Ghast", new Vector2Int(2, 0), CharacterTeam.Enemy);
        ghast.Stats.CreatureType = "Undead";
        ghast.Stats.IsMindless = false;
        var all = new List<CharacterController> { zombie, wight, orc, hidden, ghast };

        // A failed save at the turn-start perception step: still unseen, the attack still goes on (no attack lost).
        ScenarioHooks.RollFilter = ForceSave("Hide from Undead", 1);
        WardSaveLedger.RollHideFromUndeadSaves(ghast, all);
        WardSaveLedger.RollHideFromUndeadSaves(ghast, all);
        Assert(WardSaveLedger.SavesRolled == rolled + 1 && WardSaveLedger.IsHiddenFromUndead(ghast, hidden)
               && hidden.GetMissChance(ghast) >= 50
               && AttackLegality.CanDirectlyAttack(ghast, hidden, DirectAttackKind.Weapon).Ok
               && AttackLegality.CommitWard(ghast, hidden, DirectAttackKind.Weapon) == WardVerdict.Passed
               && WardSaveLedger.SavesRolled == rolled + 1,
            "A failed Hide from Undead save is rolled once, leaves the creature unseen (50% miss chance) and loses no attack");

        // The mindless zombie rolls nothing at its perception step.
        WardSaveLedger.RollHideFromUndeadSaves(zombie, all);
        Assert(WardSaveLedger.SavesRolled == rolled + 1, "A mindless undead makes no Hide from Undead save at its perception step");

        // An attack attempted before the perception step rolls the one save; a success lets it perceive the creature.
        ScenarioHooks.RollFilter = ForceSave("Hide from Undead", 20);
        WardVerdict wightCommit = AttackLegality.CommitWard(wight, hidden, DirectAttackKind.Weapon);
        Assert(wightCommit == WardVerdict.Passed && WardSaveLedger.SavesRolled == rolled + 2 && hidden.Stats.HideFromUndeadActive
               && !WardSaveLedger.IsHiddenFromUndead(wight, hidden) && !hidden.HasTotalConcealment(wight),
            "An intelligent undead saves once and, on a success, perceives the creature; the spell is not ended (AI-005)");
        Assert(AttackLegality.CanDirectlyAttack(zombie, hidden, DirectAttackKind.Weapon).Ok == false
               && WardSaveLedger.IsHiddenFromUndead(ghast, hidden),
            "The spell goes on for the undead that did not see through it");
    }
}
}
