#if UNITY_EDITOR
using System.Collections.Generic;
using DND35.AI;
using DND35.AI.Personality;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 9 (docs/designs/personalities_batch14.md "Maneuver scorer", C9; AI-035, AI-060, AI-058, AI-025): the AI's
/// maneuver-versus-attack choice, DND35.AI.AiManeuverChooser.
/// <list type="bullet">
/// <item>The golden table: <see cref="AiManeuverChooser.Score"/> on hand-filled terms, each row's value, cost, net and
/// threshold pinned to the starter constants (a retune is a visible diff here): a Strength gap trips, an even match
/// attacks, Improved Trip and many allies make a trip win, a cowardly creature (rho 1.6) weighs the AoO more, a sadist's
/// Attack desire keeps the attack, the disarm and grapple formulas, an unmodelled sunder keeps the attack, and the plan's
/// goblin tripping a fighter swings.</item>
/// <item>Live rows (Play mode): Decide on real creatures. An even match attacks; Improved Trip against an armored weakling
/// trips where the same pair without the feat attacks; a helpless target gets the attack, never a maneuver (AI-058); no
/// profile proposes through GameManager.ChooseNPCFallbackManeuver (AI-025); Gather reads the traits' rho and Attack desire.</item>
/// </list>
/// The pure rows run in either pass; the live rows need Play mode (CharacterController.Init, TST-009) and skip otherwise.
/// </summary>
public static class ManeuverChooserTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== MANEUVER CHOOSER TESTS (batch 14 step 9) ======");

        TestGoldenTable();
        if (Application.isPlaying && GameManager.Instance != null)
            TestLiveRows();
        else
            Debug.Log("  [SKIP] live rows need Play mode (GameManager)");

        Debug.Log($"====== Maneuver Chooser Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    // ── The golden table ──

    private static ManeuverScoreInputs Trip(float p, float s, float d, float dH, float th, int r, int a,
        float aoo = 0f, bool improvedTrip = false, float rho = 1f, float desire = 0f, float lost = 0f, float counter = 0f)
        => new ManeuverScoreInputs
        {
            Type = SpecialAttackType.Trip,
            SuccessChance = p,
            LostCheckChance = lost,
            CounterPossible = counter > 0f,
            CounterChance = counter,
            ExpectedAoODamage = aoo,
            AttackValue = s,
            HitDamage = d,
            ProneHitGain = dH,
            TargetThreat = th,
            StepsLeft = r,
            AlliesThreatening = a,
            ImprovedTrip = improvedTrip,
            Rho = rho,
            AttackDesire = desire,
        };

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;

    private static void Row(string name, ManeuverScoreInputs inputs, float value, float cost, float net, float threshold, bool wins)
    {
        ManeuverScore s = AiManeuverChooser.Score(inputs);
        Assert(Near(s.Value, value) && Near(s.Cost, cost) && Near(s.Net, net) && Near(s.Threshold, threshold) && s.Wins == wins,
            "golden: " + name,
            $"V {s.Value:0.###}/{value} cost {s.Cost:0.###}/{cost} net {s.Net:0.###}/{net} threshold {s.Threshold:0.###}/{threshold} wins {s.Wins}/{wins}");
    }

    private static void TestGoldenTable()
    {
        // Trip: V = dH D (r + a) + 0.4 Th (+ S + dH D with Improved Trip: its attack is on the prone target); threshold S (1 + 0.5 desire) + max(0.5, 0.1 S).
        Row("a Strength gap trips (P .9: V .2x12.5x3 + .4x4 = 9.1, net 8.19 >= 5.5)",
            Trip(0.9f, 5f, 12.5f, 0.2f, 4f, 2, 1), 9.1f, 0f, 8.19f, 5.5f, true);
        Row("an even match attacks (P .3, a lost check .3 lets a .5 counter-trip cost .3x.5x.4x4 = .24; net 2.49 < 5.5)",
            Trip(0.3f, 5f, 12.5f, 0.2f, 4f, 2, 1, lost: 0.3f, counter: 0.5f), 9.1f, 0.24f, 2.49f, 5.5f, false);
        Row("Improved Trip adds the attack after a trip that lands, on the prone target (P .5: V 9.1 + S 5 + .2x12.5 = 16.6, net 8.3 >= 5.5)",
            Trip(0.5f, 5f, 12.5f, 0.2f, 4f, 2, 1, improvedTrip: true), 16.6f, 0f, 8.3f, 5.5f, true);
        Row("one step left and no ally: the trip loses (V 4.1, net 3.28 < 5.5)",
            Trip(0.8f, 5f, 12.5f, 0.2f, 4f, 1, 0), 4.1f, 0f, 3.28f, 5.5f, false);
        Row("the same trip with three allies threatening the target wins (V 11.6, net 9.28)",
            Trip(0.8f, 5f, 12.5f, 0.2f, 4f, 1, 3), 11.6f, 0f, 9.28f, 5.5f, true);
        Row("a provoked AoO of 2 expected damage is paid at rho 1 (net 6.19 >= 5.5)",
            Trip(0.9f, 5f, 12.5f, 0.2f, 4f, 2, 1, aoo: 2f), 9.1f, 2f, 6.19f, 5.5f, true);
        Row("a cowardly creature (rho 1.6) weighs the same AoO at 3.2 and attacks (net 4.99 < 5.5)",
            Trip(0.9f, 5f, 12.5f, 0.2f, 4f, 2, 1, aoo: 2f, rho: 1.6f), 9.1f, 3.2f, 4.99f, 5.5f, false);
        Row("a sadist's Attack desire .4 raises the threshold to 6.5 and keeps the attack (net 6.19)",
            Trip(0.9f, 5f, 12.5f, 0.2f, 4f, 2, 1, aoo: 2f, desire: 0.4f), 9.1f, 2f, 6.19f, 6.5f, false);
        Row("the plan's goblin (P .16, S 2.03, an AoO of 3) tripping a fighter swings",
            Trip(0.16f, 2.025f, 4.5f, 0.2f, 6f, 0, 0, aoo: 3f, lost: 0.6f, counter: 0.4f), 2.4f, 3.576f, -3.192f, 2.525f, false);
        Row("the margin's floor: S 0 still needs a net of .5 (V .4x1 = .4, net .4 < .5)",
            Trip(1f, 0f, 1f, 0f, 1f, 0, 0), 0.4f, 0f, 0.4f, 0.5f, false);

        // Disarm: V = 0.75 Th x the weapon's share; a counter-disarm after a lost check costs the creature's own weapon value.
        Row("disarm: V .75x10x1 = 7.5, counter .3x.4x4 = .48, net 4.02 >= 3.5",
            new ManeuverScoreInputs
            {
                Type = SpecialAttackType.Disarm, SuccessChance = 0.6f, LostCheckChance = 0.3f, CounterPossible = true, CounterChance = 0.4f,
                AttackValue = 3f, HitDamage = 6f, TargetThreat = 10f, TargetWeaponShare = 1f, OwnWeaponValue = 4f, Rho = 1f,
            }, 7.5f, 0.48f, 4.02f, 3.5f, true);
        Row("disarm of a creature that fights with no held weapon is worth nothing",
            new ManeuverScoreInputs
            {
                Type = SpecialAttackType.Disarm, SuccessChance = 0.9f, AttackValue = 3f, HitDamage = 6f, TargetThreat = 10f,
                TargetWeaponShare = 0f, Rho = 1f,
            }, 0f, 0f, 0f, 3.5f, false);
        // Grapple: V = 0.5 Th + D.
        Row("grapple: V .5x6 + 6 = 9, net 4.5 >= 3.5",
            new ManeuverScoreInputs { Type = SpecialAttackType.Grapple, SuccessChance = 0.5f, AttackValue = 3f, HitDamage = 6f, TargetThreat = 6f, Rho = 1f },
            9f, 0f, 4.5f, 3.5f, true);
        // Sunder and bull rush have no value model: the attack is kept.
        Row("sunder has no value model: the attack is kept",
            new ManeuverScoreInputs { Type = SpecialAttackType.Sunder, SuccessChance = 1f, AttackValue = 1f, HitDamage = 6f, TargetThreat = 6f, Rho = 1f },
            0f, 0f, 0f, 1.5f, false);
    }

    // ── Live rows ──

    private static CharacterController Make(List<CharacterController> made, string name, int str, int dex, int bab, CharacterTeam team, int x, int y)
    {
        CharacterController c = TestHelpers.CreateCharacter(name: name, str: str, dex: dex, bab: bab);
        c.Stats.BaseAttackBonusOverride = bab; // classed characters ignore Stats.BaseAttackBonus (CHR-068)
        c.SetTeam(team);
        TestHelpers.SetGridPosition(c, x, y);
        c.StartNewTurn();
        made.Add(c);
        return c;
    }

    private static void Equip(CharacterController c, ItemID id)
    {
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(id), EquipSlot.RightHand);
        inv.CharacterInventory.RecalculateStats();
    }

    private static void TestLiveRows()
    {
        TestHelpers.EnsureCoreDatabasesInitialized();
        var made = new List<CharacterController>();
        var profiles = new List<AIProfile>();
        try
        {
            AIProfile humanoid = AIProfile.CreateRuntime<DND35.AI.Profiles.HumanoidAIProfile>();
            profiles.Add(humanoid);

            // An even match: Str 14 BAB +3 longswords, side by side.
            CharacterController a = Make(made, "MC Even A", 14, 12, 3, CharacterTeam.Enemy, 40, 40);
            CharacterController b = Make(made, "MC Even B", 14, 12, 3, CharacterTeam.Player, 41, 40);
            Equip(a, ItemID.WeaponLongsword);
            Equip(b, ItemID.WeaponLongsword);
            ManeuverDecision even = AiManeuverChooser.Decide(a, b, humanoid);
            Assert(even.Proposed == SpecialAttackType.Trip && even.Kind == ManeuverChoiceKind.Attack,
                "live: an even match attacks; the Humanoid Role's trip proposal loses (AI-035)", even.Reason);

            // Improved Trip against an armored weakling (Str 8, AC 23 from natural armor, touch AC 11).
            CharacterController tripper = Make(made, "MC Tripper", 18, 12, 6, CharacterTeam.Enemy, 40, 43);
            CharacterController armored = Make(made, "MC Armored", 8, 12, 3, CharacterTeam.Player, 41, 43);
            Equip(tripper, ItemID.WeaponLongsword);
            Equip(armored, ItemID.WeaponLongsword);
            armored.Stats.NaturalArmorBonus = 12;
            ManeuverDecision plain = AiManeuverChooser.Decide(tripper, armored, humanoid);
            tripper.Stats.Feats.Add("Improved Trip");
            ManeuverDecision improved = AiManeuverChooser.Decide(tripper, armored, humanoid);
            Assert(plain.Proposed == SpecialAttackType.Trip && plain.Kind == ManeuverChoiceKind.Attack,
                "live: without Improved Trip the armored weakling still draws the attack", plain.Reason);
            Assert(improved.Chosen == SpecialAttackType.Trip && improved.Inputs.ImprovedTrip && improved.Inputs.ExpectedAoODamage == 0f
                    && improved.Inputs.ProneHitGain > 0f,
                "live: with Improved Trip (+4, the attack after it, PHB p.96) the trip beats the swing", improved.Reason);

            // A helpless target gets the attack, never a maneuver (AI-058).
            armored.ApplyCondition(CombatConditionType.Helpless, 3, "ManeuverChooserTests");
            ManeuverDecision helpless = AiManeuverChooser.Decide(tripper, armored, humanoid);
            Assert(helpless.Kind == ManeuverChoiceKind.Attack && helpless.Proposed == SpecialAttackType.Trip
                    && helpless.Reason != null && helpless.Reason.Contains("AI-058"),
                "live: a helpless target gets an attack, not the trip the Role proposes (AI-058)", helpless.Reason);
            armored.RemoveCondition(CombatConditionType.Helpless);

            // No profile: the one no-profile proposer (GameManager.ChooseNPCFallbackManeuver, AI-025).
            ManeuverDecision noProfile = AiManeuverChooser.Decide(tripper, armored, null);
            Assert(noProfile.Proposed == GameManager.Instance.ChooseNPCFallbackManeuverForAI(tripper, armored) && noProfile.Proposed == SpecialAttackType.Trip,
                "live: with no profile the proposal is ChooseNPCFallbackManeuver's (trip a standing target, AI-025)", noProfile.Reason);

            // The traits' rho and Attack desire reach the scorer's terms.
            tripper.Personality = PersonalityFactory.Create("Cowardly:1.0", DND35.AI.Knowledge.CognitionTier.Average, Alignment.None, "mc_test", _ => { });
            try
            {
                ManeuverScoreInputs coward = AiManeuverChooser.Gather(tripper, armored, SpecialAttackType.Trip);
                Assert(tripper.Personality != null && Near(coward.Rho, tripper.Personality.Rho) && coward.Rho > 1f
                        && Near(coward.AttackDesire, tripper.Personality.RoleShift(ActionRoles.Attack)),
                    "live: a cowardly tripper's terms carry its rho (above 1) and its Attack desire",
                    "rho " + coward.Rho + " desire " + coward.AttackDesire);
            }
            finally
            {
                tripper.Personality = null;
            }
            ManeuverScoreInputs neutral = AiManeuverChooser.Gather(tripper, armored, SpecialAttackType.Trip);
            Assert(neutral.Rho == 1f && neutral.AttackDesire == 0f, "live: with no traits rho is 1 and the Attack desire 0");
        }
        catch (System.Exception ex)
        {
            Assert(false, "live rows threw " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            foreach (CharacterController c in made)
                if (c != null)
                    Object.DestroyImmediate(c.gameObject);
            foreach (AIProfile p in profiles)
                if (p != null)
                    Object.DestroyImmediate(p);
        }
    }
}
}
#endif
