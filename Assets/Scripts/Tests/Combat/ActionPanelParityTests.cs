using System;
using System.Collections.Generic;
using System.Reflection;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Combat
{
/// <summary>
/// The PC action panel against the legal set (batch 13 step 6, design choice C8: the set runs in shadow mode, the panel
/// keeps its own states until batch 22 makes it read the set). For each fixture actor (a fighter, a wizard, prone,
/// grappled, pinned, nauseated, disabled, a wild-shaped druid, frightened, charmed) the panel's state of each button that
/// stands for one catalog entry (ActionButtonPanel.ComputeButtonStatesForParity: visible and enabled) is compared with
/// ActorActionSet.Allows for that entry. Every disagreement must be in <see cref="KnownMismatches"/>, tagged with the
/// batch 13 step that resolves it or the issue that tracks it; a known mismatch that no longer occurs fails too, so the
/// step that fixes it removes its row. The Monster Abilities button (batch 13 step 18, D11) stands for the four
/// monster entries at once, so it is compared with the set by <see cref="TestMonsterAbilityButton"/>: on exactly when
/// one of them is allowed. Play only: it needs the scene's CombatUI and its buttons, and
/// CharacterController.Init needs Awake for its conditions (TST-009).
/// </summary>
public static class ActionPanelParityTests
{
    private static int _passed;
    private static int _failed;

    /// <summary>Every controller a fixture created; <see cref="Run"/> destroys them after each test, so a throw leaks nothing.</summary>
    private static readonly List<CharacterController> Created = new List<CharacterController>();

    /// <summary>The fixtures whose comparison finished; a known row is checked only when its fixture ran.</summary>
    private static readonly HashSet<string> Completed = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The CombatUI button field of each catalog entry with a button of its own.</summary>
    private static readonly string[,] ButtonToAction =
    {
        { "MoveButton", ActionIds.Move },
        { "WithdrawButton", ActionIds.Withdraw },
        { "FiveFootStepButton", ActionIds.FiveFootStep },
        { "DropProneButton", ActionIds.DropProne },
        { "StandUpButton", ActionIds.StandUp },
        { "CrawlButton", ActionIds.Crawl },
        { "AttackButton", ActionIds.Attack },
        { "ChargeButton", ActionIds.Charge },
        { "TotalDefenseButton", ActionIds.TotalDefense },
        { "TurnUndeadButton", ActionIds.TurnUndead },
        { "DomainPowerButton", ActionIds.DomainPower },
        { "SmiteButton", ActionIds.Smite },
        { "LayOnHandsButton", ActionIds.LayOnHands },
        { "WildShapeButton", ActionIds.WildShape },
        { "FlurryOfBlowsButton", ActionIds.FlurryOfBlows },
        { "RageButton", ActionIds.Rage },
        { "BardicMusicButton", ActionIds.BardicMusic },
        { "CastSpellButton", ActionIds.CastSpell },
        { "MaintainConcentrationButton", ActionIds.MaintainConcentration },
        { "ReloadButton", ActionIds.Reload },
        { "PickUpItemButton", ActionIds.PickUpItem },
        { "DropEquippedItemButton", ActionIds.DropItem },
        { "UseItemButton", ActionIds.UseItem },
        { "BreakWallButton", ActionIds.AttackWall },
        { "DischargeTouchButton", ActionIds.DeliverHeldCharge },
    };

    /// <summary>
    /// The known disagreements, "fixture:actionId" -> the step or issue that resolves it. Filled from the first Play pass
    /// (2026-10-29); each row says why the two disagree.
    /// </summary>
    private static readonly Dictionary<string, string> KnownMismatches = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // A charge with no foe on the map: the panel's state includes the target, the set's does not.
        { "fighter:charge", "UI-055: the panel folds a valid charge target into the button (no foe stands in the fixture); the set's Allows is actor-level and a charge's targets are its LegalTargets (batch 22 makes the panel read the set)" },
        { "wizard:charge", "UI-055: the panel folds a valid charge target into the button (no foe stands in the fixture); the set's Allows is actor-level and a charge's targets are its LegalTargets (batch 22 makes the panel read the set)" },
        { "disabled:charge", "UI-055: the panel folds a valid charge target into the button (no foe stands in the fixture); the set's Allows is actor-level and a charge's targets are its LegalTargets (batch 22 makes the panel read the set)" },
        { "grappled:charge", "UI-055: the panel folds a valid charge target into the button (no foe stands in the fixture); the set's Allows is actor-level and a charge's targets are its LegalTargets (batch 22 makes the panel read the set)" },
        { "charmed:charge", "UI-055: the panel folds a valid charge target into the button (no foe stands in the fixture); the set's Allows is actor-level and a charge's targets are its LegalTargets (batch 22 makes the panel read the set)" },
        { "wildshaped-druid:charge", "UI-055: the panel folds a valid charge target into the button (no foe stands in the fixture); the set's Allows is actor-level and a charge's targets are its LegalTargets (batch 22 makes the panel read the set)" },
        // The set's start gates are coarser than the panel's (the cores refuse these at the request, so no AI pick runs them).
        { "prone:charge", "UI-055: the charge entry's start gate (ChargeRules.HasChargeAction) does not read prone; GameManager.CanCharge refuses it at the request" },
        { "pinned:charge", "UI-055: the charge entry's start gate does not read pinned; GameManager.CanCharge refuses it at the request" },
        { "pinned:attack", "UI-055: the attack entry's start gate does not read pinned (PHB p.156); the attack sequence refuses it" },
        { "grappled:attack", "UI-055: a longsword cannot attack in a grapple (PHB p.156: light weapons only), which the panel shows as 'Reload first' and the attack entry's start gate does not read" },
        { "wildshaped-druid:attack", "UI-055: the panel's natural attack option (GameManager.CanUseNaturalAttackOption) requires the actor to be ActivePC, which a fixture is not; the set does not read ActivePC" },
        { "wildshaped-druid:cast-spell", "UI-055: the cast entry's start gate only asks for a SpellcastingComponent; the panel also needs a slot left (the fixture druid prepared none), and the wild shape bar (WildShapeRules, PHB p.37, p.98) is the cast core's" },
        // Use an Item has no shared legality yet.
        // A frightened PC's panel is not restricted yet (CMB-100).
        { "frightened:attack", "batch 13 step 20 (the restricted PC menu for flight; the set's compulsion filter, D15, already bars it)" },
        { "frightened:drop-prone", "batch 13 step 20 (the restricted PC menu for flight; the set's compulsion filter, D15, already bars it)" },
    };

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== ACTION PANEL PARITY TESTS (batch 13 step 6, C8) ==========");
        if (!Application.isPlaying || GameManager.Instance == null || GameManager.Instance.CombatUI == null)
        {
            Debug.Log("  [SKIP] ActionPanelParityTests (needs Play mode, a scene GameManager and its CombatUI)");
            Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
            return;
        }
        TestHelpers.EnsureCoreDatabasesInitialized();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        Completed.Clear();
        Run(() => Compare("fighter", Fighter("APP_Fighter"), null, seen));
        Run(() => Compare("wizard", Wizard("APP_Wizard"), null, seen));
        Run(() => Compare("prone", With(Fighter("APP_Prone"), c => c.ApplyCondition(CombatConditionType.Prone, 10, "ActionPanelParityTests")), null, seen));
        Run(() => CompareGrappled(seen, pinned: false));
        Run(() => CompareGrappled(seen, pinned: true));
        Run(() => Compare("nauseated", With(Fighter("APP_Nauseated"), c => c.ApplyCondition(CombatConditionType.Nauseated, 10, "ActionPanelParityTests")), null, seen));
        Run(() => Compare("disabled", With(Fighter("APP_Disabled"), c => c.Stats.CurrentHP = 0), null, seen));
        Run(() => Compare("wildshaped-druid", WildShapedDruid("APP_Druid"), null, seen));
        Run(() => Compare("frightened", Frightened("APP_Frightened"), null, seen));
        Run(CompareCharmed(seen));
        Run(TestTurnedFilter);
        Run(TestMonsterAbilityButton);

        var notRun = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> known in KnownMismatches)
        {
            string fixture = known.Key.Substring(0, known.Key.IndexOf(':'));
            if (!Completed.Contains(fixture))
            {
                if (notRun.Add(fixture))
                    Assert(false, "fixture " + fixture + " did not run; its known rows were not checked");
                continue;
            }
            Assert(seen.Contains(known.Key), "Known mismatch no longer occurs (remove its row if the fix is intended): " + known.Key, known.Value);
        }

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Run(Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            Assert(false, $"{test.Method.Name} threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            CleanupCreated();
        }
    }

    /// <summary>Destroys every fixture controller, removing a charm first so ConditionService's per-character store keeps no link.</summary>
    private static void CleanupCreated()
    {
        foreach (CharacterController c in Created)
        {
            if (c == null)
                continue;
            try
            {
                if (c.Stats != null && c.HasCondition(CombatConditionType.Charmed) && GameManager.Instance != null)
                    GameManager.Instance.RemoveCondition(c, CombatConditionType.Charmed);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ActionPanelParity] charm cleanup of " + c.name + ": " + ex.Message);
            }
            TestHelpers.Cleanup(c.gameObject);
        }
        Created.Clear();
    }

    private static CharacterController Track(CharacterController c)
    {
        if (c != null)
            Created.Add(c);
        return c;
    }

    // ── The comparison ──────────────────────────────────────────────────────────────────────────────────────────────

    private static ActionButtonPanel Panel()
    {
        CombatUI ui = GameManager.Instance.CombatUI;
        ActionButtonPanel panel = ui.GetComponent<ActionButtonPanel>();
        if (panel == null)
        {
            MethodInfo ensure = typeof(CombatUI).GetMethod("EnsureActionButtonPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            ensure?.Invoke(ui, null);
            panel = ui.GetComponent<ActionButtonPanel>();
        }
        return panel;
    }

    private static void Compare(string fixture, CharacterController actor, CharacterController[] extra, HashSet<string> seen)
    {
        // The fixtures are destroyed by Run (CleanupCreated), so a throw anywhere in a fixture leaks nothing.
        {
            ActionButtonPanel panel = Panel();
            Assert(panel != null, fixture + ": the CombatUI has its ActionButtonPanel");
            if (panel == null)
                return;
            CombatUI ui = GameManager.Instance.CombatUI;
            var byButton = new Dictionary<Button, ActionButtonPanel.ButtonStateView>();
            foreach (ActionButtonPanel.ButtonStateView v in panel.ComputeButtonStatesForParity(actor))
                if (v.Button != null)
                    byButton[v.Button] = v;
            ActorActionSet set = ActorActionSet.Build(GameManager.Instance, actor, ActorSource.Ui);

            int compared = 0;
            var unexpected = new List<string>();
            for (int i = 0; i < ButtonToAction.GetLength(0); i++)
            {
                FieldInfo field = typeof(CombatUI).GetField(ButtonToAction[i, 0], BindingFlags.Instance | BindingFlags.Public);
                Button button = field != null ? field.GetValue(ui) as Button : null;
                if (button == null || !byButton.TryGetValue(button, out ActionButtonPanel.ButtonStateView view))
                    continue;
                string id = ButtonToAction[i, 1];
                bool panelAllows = view.Visible && view.Enabled;
                bool setAllows = set.Allows(id);
                compared++;
                if (panelAllows == setAllows)
                    continue;
                string key = fixture + ":" + id;
                seen.Add(key);
                ActionOption option = set.Get(id);
                string detail = $"panel={(panelAllows ? "on" : "off")} ('{view.Label}'), set={(setAllows ? "on" : "off")} ({(option != null ? option.Legality.ToString() : "not available to the actor")})";
                Debug.Log($"[ActionPanelParity] {key}: {detail}");
                if (!KnownMismatches.ContainsKey(key))
                    unexpected.Add(key + " " + detail);
            }
            Completed.Add(fixture);
            Assert(compared > 0, fixture + ": the panel set states for the mapped buttons", compared.ToString());
            Assert(unexpected.Count == 0, fixture + ": every panel/set disagreement is a known, tagged mismatch",
                string.Join(" | ", unexpected));
        }
    }

    private static void CompareGrappled(HashSet<string> seen, bool pinned)
    {
        CharacterController a = Fighter(pinned ? "APP_Pinned" : "APP_Grappled", new Vector2Int(-60, -200));
        CharacterController b = Fighter(pinned ? "APP_Pinner" : "APP_Grappler", new Vector2Int(-59, -200));
        MethodInfo establish = typeof(CharacterController).GetMethod("EstablishGrappleWith", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(establish != null, "CharacterController.EstablishGrappleWith exists (TST-006)");
        if (establish != null)
            establish.Invoke(b, new object[] { a });
        if (pinned)
            a.ApplyCondition(CombatConditionType.Pinned, 10, "ActionPanelParityTests");
        Assert(a.IsGrappling(), (pinned ? "pinned" : "grappled") + ": the fixture is grappling");
        Compare(pinned ? "pinned" : "grappled", a, new[] { b }, seen);
    }

    private static Action CompareCharmed(HashSet<string> seen)
    {
        return () =>
        {
            CharacterController charmer = Fighter("APP_Charmer", new Vector2Int(-40, -200));
            CharacterController actor = Fighter("APP_Charmed", new Vector2Int(-41, -200));
            var charm = new CharmedConditionData { Caster = charmer, CasterName = charmer.Stats.CharacterName, RemainingRounds = 10 };
            GameManager.Instance.ApplyCondition(actor, CombatConditionType.Charmed, 10, source: charmer, data: charm, sourceNameOverride: "Charm Person");
            ActorActionSet set = ActorActionSet.Build(GameManager.Instance, actor, ActorSource.AI);
            Legality onCharmer = set.Check(ActionIds.Attack, ActionContext.For(ActionIds.Attack, actor, ActorSource.AI, charmer));
            Assert(!onCharmer.Ok && onCharmer.Bar == LegalityBar.Charm, "PHB p.209: the set bars an attack on the charmer (charm link)", onCharmer.ToString());
            Assert(!set.LegalTargets(ActionIds.Attack).Contains(charmer), "PHB p.209: the charmer is not a legal attack target");
            Compare("charmed", actor, new[] { charmer }, seen);
        };
    }

    /// <summary>D15: a turned creature's set keeps only the entries that flee; a turn with no compulsion keeps everything.</summary>
    private static void TestTurnedFilter()
    {
        CharacterController c = Fighter("APP_Turned");
        {
            c.CurrentTurnStart.Compulsion = new TurnCompulsion { Turned = true };
            ActorActionSet set = ActorActionSet.Build(GameManager.Instance, c, ActorSource.AI);
            ActionOption attack = set.Get(ActionIds.Attack);
            Assert(attack != null && !attack.Legality.Ok && attack.Legality.Bar == LegalityBar.Compulsion,
                "PHB p.159, D15: a turned creature may not attack (Compulsion bar)", attack != null ? attack.ToString() : "no option");
            Assert(set.Get(ActionIds.Move) != null && set.Get(ActionIds.Move).Legality.Bar != LegalityBar.Compulsion,
                "PHB p.159, D15: a turned creature may move (the flight)");
            c.CurrentTurnStart.Clear();
            Assert(ActorActionSet.Build(GameManager.Instance, c, ActorSource.AI).Allows(ActionIds.Attack), "With no compulsion the attack is legal again");
        }
    }

    /// <summary>
    /// The Monster Abilities button against the set's monster entries (batch 13 step 18, D11): hidden for a creature with
    /// none; for a fighter given a breath weapon and spittle, on while either is allowed, and its label names the one
    /// usable; with the standard action spent and the spittle used this round, off, as the set refuses both.
    /// </summary>
    private static void TestMonsterAbilityButton()
    {
        ActionButtonPanel panel = Panel();
        CombatUI ui = GameManager.Instance.CombatUI;
        Assert(panel != null && ui.MonsterAbilityButton != null, "monster: the panel and the Monster Abilities button exist");
        if (panel == null || ui.MonsterAbilityButton == null)
            return;

        ActionButtonPanel.ButtonStateView View(CharacterController c)
        {
            foreach (ActionButtonPanel.ButtonStateView v in panel.ComputeButtonStatesForParity(c))
                if (v.Button == ui.MonsterAbilityButton)
                    return v;
            return default;
        }
        bool SetAllowsAny(CharacterController c)
        {
            ActorActionSet set = ActorActionSet.Build(GameManager.Instance, c, ActorSource.Ui);
            return set.Allows(ActionIds.BreathWeapon) || set.Allows(ActionIds.Spittle) || set.Allows(ActionIds.Engulf) || set.Allows(ActionIds.RangedSpecial);
        }

        CharacterController plain = Fighter("APP_PlainFighter");
        ActionButtonPanel.ButtonStateView plainView = View(plain);
        Assert(plainView.Button != null && !plainView.Visible && !SetAllowsAny(plain), "monster: a creature with no monster ability has the button hidden");

        CharacterController breather = Fighter("APP_Breather", new Vector2Int(-70, -200));
        breather.ConfigureBreathWeapon(new BreathWeaponDefinition { DamageCount = 2, DamageDice = 6, RechargeDice = "1d4", RangeFeet = 15 });
        breather.ConfigureRangedSpecialAttack(new RangedSpecialAttackDefinition { Name = "Spittle", RangeFeet = 30, IsFreeAction = true });
        breather.StartNewTurn();
        ActionButtonPanel.ButtonStateView on = View(breather);
        Assert(on.Visible && on.Enabled && SetAllowsAny(breather), "monster: with a ready breath and the spittle the button is on, as the set allows them",
            on.Label);

        breather.Actions.UseStandardAction();
        ActionButtonPanel.ButtonStateView spittleOnly = View(breather);
        Assert(spittleOnly.Visible && spittleOnly.Enabled && spittleOnly.Label == "Spittle" && SetAllowsAny(breather),
            "monster: with the standard action spent only the spittle is usable, and the button names it", spittleOnly.Label);

        breather.SetFreeRangedSpecialUsedThisRound(true);
        ActionButtonPanel.ButtonStateView off = View(breather);
        Assert(off.Visible && !off.Enabled && !SetAllowsAny(breather),
            "monster: with the standard action spent and the spittle used this round the button is off, as the set refuses both", off.Label);

        // A nauseated creature (a move action only, PHB p.311: unable to attack) gets no usable monster ability, the free
        // spittle included (batch 13 step 18 review).
        breather.StartNewTurn();
        breather.Actions.MoveActionOnly = true;
        ActionButtonPanel.ButtonStateView sick = View(breather);
        Assert(sick.Visible && !sick.Enabled && !SetAllowsAny(breather),
            "monster: a nauseated creature's button is off and the set allows no monster ability (PHB p.311)", sick.Label);

        // A once-per-round breath (the bombardier beetle's acid spray, a reaction: batch 12 D1) gives no button.
        CharacterController beetle = Fighter("APP_Beetle", new Vector2Int(-60, -200));
        beetle.ConfigureBreathWeapon(new BreathWeaponDefinition { DamageCount = 1, DamageDice = 4, RangeFeet = 10, OncePerRound = true });
        beetle.StartNewTurn();
        ActionButtonPanel.ButtonStateView beetleView = View(beetle);
        Assert(beetleView.Button != null && !beetleView.Visible && !SetAllowsAny(beetle),
            "monster: a creature whose only breath is a once-per-round reaction has the button hidden", beetleView.Label);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────────────────────────────

    private static CharacterController With(CharacterController c, Action<CharacterController> change)
    {
        change(c);
        return c;
    }

    private static CharacterController Fighter(string name, Vector2Int? pos = null)
    {
        CharacterController c = Track(TestHelpers.CreateCharacter(name: name, characterClass: "Fighter", level: 3, str: 14, dex: 14, bab: 3,
            gridPosition: pos ?? new Vector2Int(-80, -200)));
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        if (inv != null && inv.CharacterInventory != null)
        {
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            inv.CharacterInventory.RecalculateStats();
        }
        c.StartNewTurn();
        return c;
    }

    private static CharacterController Wizard(string name)
    {
        CharacterController c = Track(TestHelpers.CreateCharacter(name: name, characterClass: "Wizard", level: 3, intelligence: 16, bab: 1,
            gridPosition: new Vector2Int(-80, -200)));
        SpellcastingComponent spells = c.GetComponent<SpellcastingComponent>() ?? c.gameObject.AddComponent<SpellcastingComponent>();
        spells.Init(c.Stats);
        spells.AutoPrepareWizardSlots();
        c.StartNewTurn();
        return c;
    }

    private static CharacterController WildShapedDruid(string name)
    {
        CharacterController c = Track(TestHelpers.CreateCharacter(name: name, characterClass: "Druid", level: 5, wis: 16, bab: 3,
            gridPosition: new Vector2Int(-80, -200)));
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        if (inv != null)
            new DruidClass().SetupStartingEquipment(inv);
        SpellcastingComponent spells = c.GetComponent<SpellcastingComponent>() ?? c.gameObject.AddComponent<SpellcastingComponent>();
        spells.Init(c.Stats);
        spells.AutoPrepareDruidSlots();
        WildShapeResult shaped = WildShapeRules.Assume(c, "black_bear");
        Assert(shaped.Performed, "wildshaped-druid: a druid 5 takes the black bear form", shaped.Refusal);
        c.StartNewTurn();
        return c;
    }

    private static CharacterController Frightened(string name)
    {
        CharacterController c = Fighter(name);
        c.ApplyCondition(CombatConditionType.Frightened, 10, "ActionPanelParityTests");
        c.CurrentTurnStart.Compulsion = new TurnCompulsion { Fear = new FrightenedBehaviorController.FrightenedTurnDecision() };
        return c;
    }

    // ── Asserts ─────────────────────────────────────────────────────────────────────────────────────────────────────

    private static void Assert(bool ok, string label, string detail = null)
    {
        if (ok)
        {
            _passed++;
            Debug.Log("  [PASS] " + label);
        }
        else
        {
            _failed++;
            Debug.LogError("  [FAIL] " + label + (string.IsNullOrEmpty(detail) ? string.Empty : " (" + detail + ")"));
        }
    }
}
}
