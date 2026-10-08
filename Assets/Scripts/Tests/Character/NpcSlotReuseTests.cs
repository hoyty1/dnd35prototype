using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Character
{
/// <summary>
/// CRE-046: a controller reused for a new creature (the enemy pool SceneBootstrap builds once, reused by every
/// encounter) must not keep anything from the creature it held before. Each test spawns a creature into a
/// controller through the real spawn path (GameManager.InitializeNPCFromDefinition, which starts with
/// GameManager.ResetCharacterSlotForSpawn), dirties its runtime state, spawns another creature into the same controller
/// and compares the result with the same creature spawned into a fresh controller. Party slots go through
/// GameManager.ResetPCSlotForNewCharacter, the same reset, before Init.
/// Needs Play mode (a scene GameManager). The test controllers sit off the grid and are destroyed afterwards; the
/// scene's own pool slots are not touched (the scenario rules/slot-reuse-* pair covers them, docs/TESTING.md 3.4).
/// </summary>
public static class NpcSlotReuseTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int SlotPos = new Vector2Int(-40, -40);
    private static readonly Vector2Int PartnerPos = new Vector2Int(-42, -40);
    private static readonly Vector2Int FreshPos = new Vector2Int(-44, -40);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        NPCDatabase.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();
        FeatDefinitions.Init();

        Debug.Log("========== NPC SLOT REUSE TESTS (CRE-046) ==========");

        if (GameManager.Instance == null)
        {
            Assert(false, "NPC slot reuse tests need Play mode with a scene GameManager");
        }
        else
        {
            TestTraitCreatureThenPlainCreature();
            TestCasterThenPlainCreature();
            TestPlainCreatureThenTraitCreature();
            TestRespawnClearsOthersMemoryOfSlot();
            TestPartySlotGetsNewCharacter();
            TestAuraSaveImmunityForgottenWithSource();
        }

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
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

    // ── Helpers ─────────────────────────────────────────────────────────

    private static CharacterController NewController(string name)
    {
        var go = new GameObject(name);
        return go.AddComponent<CharacterController>();
    }

    private static void Spawn(CharacterController cc, NPCDefinition def, Vector2Int pos)
    {
        GameManager.Instance.InitializeNPCFromDefinition(cc, def, pos, null, null);
    }

    private static NPCDefinition Def(string id)
    {
        NPCDefinition template = NPCDatabase.Get(id);
        return template != null ? template.Clone() : null;
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        GameManager gm = GameManager.Instance;
        foreach (CharacterController cc in controllers)
        {
            if (cc == null)
                continue;
            cc.ReleaseGrappleState("NpcSlotReuseTests cleanup");
            if (gm != null && gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(cc);
            AIService.ForgetAuraSaveImmunities(cc);
            Object.DestroyImmediate(cc.gameObject);
        }
    }

    /// <summary>
    /// The gibbering mouther (gibbering aura, engulf, spittle, ground manipulation, blood drain) with every other
    /// conditional trait copied in from database clones: the allip's incorporeality and Babble aura, the hell hound's
    /// breath weapon, a dragon's secondary breath weapon and frightful presence, the ghast's stench and the troll's
    /// regeneration. The base is a living creature because the allip spawns dead (CRE-044) and a grapple with a dead
    /// creature ends at once. Templates are never mutated: every source is a Clone.
    /// </summary>
    private static NPCDefinition KitchenSink(out string missing)
    {
        var gaps = new List<string>();
        NPCDefinition def = Def("gibbering_mouther");
        if (def == null || def.Engulf == null || def.RangedSpecialAttack == null || def.TerrainManipulation == null || def.BloodDrain == null)
        {
            missing = "gibbering_mouther";
            return null;
        }

        def.Id = "cre046_kitchen_sink";
        def.Name = "Kitchen Sink";

        NPCDefinition hound = Def("hell_hound");
        if (hound?.BreathWeapon != null) def.BreathWeapon = hound.BreathWeapon; else gaps.Add("hell_hound breath");

        NPCDefinition secondary = NPCDatabase.AllNPCs.FirstOrDefault(d => d != null && d.SecondaryBreathWeapon != null)?.Clone();
        if (secondary != null) def.SecondaryBreathWeapon = secondary.SecondaryBreathWeapon; else gaps.Add("secondary breath");

        NPCDefinition fearsome = NPCDatabase.AllNPCs.FirstOrDefault(d => d != null && d.FrightfulPresence != null)?.Clone();
        if (fearsome != null) def.FrightfulPresence = fearsome.FrightfulPresence; else gaps.Add("frightful presence");

        NPCDefinition allip = Def("allip");
        if (allip != null && allip.IsIncorporeal && allip.AuraAbility != null)
        {
            def.IsIncorporeal = true;
            def.AuraAbility = allip.AuraAbility;
        }
        else gaps.Add("allip incorporeality and aura");

        NPCDefinition ghast = Def("ghast");
        if (ghast != null && ghast.StenchAuraDC > 0)
        {
            def.StenchAuraDC = ghast.StenchAuraDC;
            def.StenchAuraRange = ghast.StenchAuraRange;
        }
        else gaps.Add("ghast stench");

        NPCDefinition troll = Def("troll");
        if (troll != null && troll.RegenerationAmount > 0)
        {
            def.RegenerationAmount = troll.RegenerationAmount;
            def.RegenerationSuppressedBy = troll.RegenerationSuppressedBy;
        }
        else gaps.Add("troll regeneration");

        missing = gaps.Count > 0 ? string.Join(", ", gaps) : null;
        return def;
    }

    private static List<string> TraitsOf(CharacterController cc)
    {
        var t = new List<string>();
        if (cc.IsIncorporeal) t.Add("incorporeal");
        if (cc.HasAuraAbility) t.Add("aura");
        if (cc.HasBreathWeapon) t.Add("breath");
        if (cc.HasSecondaryBreathWeapon) t.Add("secondary breath");
        if (cc.HasFrightfulPresence) t.Add("frightful presence");
        if (cc.HasEngulf) t.Add("engulf");
        if (cc.HasRangedSpecialAttack) t.Add("ranged special");
        if (cc.HasBloodDrain) t.Add("blood drain");
        if (cc.HasTerrainManipulation) t.Add("terrain manipulation");
        if (cc.HasStenchAura) t.Add("stench");
        if (cc.HasRegeneration) t.Add("regeneration");
        if (!cc.HasBombardierAcidSprayReady) t.Add("acid spray cooldown");
        return t;
    }

    private static string ComponentSet(CharacterController cc)
        => string.Join(",", cc.GetComponents<Component>().Select(c => c.GetType().Name).OrderBy(n => n, System.StringComparer.Ordinal));

    private static string TagSet(CharacterController cc)
        => string.Join(",", cc.Tags.GetAllTags().OrderBy(n => n, System.StringComparer.Ordinal));

    // ── Tests ───────────────────────────────────────────────────────────

    /// <summary>
    /// Kitchen sink creature, dirtied at runtime (grapple, spell effect, condition, haste, Power Attack, spent move,
    /// tag, AI tracker component, sprite tint, acid spray cooldown, target priority), then an orc warrior in the same
    /// controller: none of it may remain, and the controller must match an orc warrior spawned into a fresh one.
    /// </summary>
    private static void TestTraitCreatureThenPlainCreature()
    {
        NPCDefinition sink = KitchenSink(out string missing);
        Assert(sink != null && missing == null, "CRE-046: the kitchen-sink creature has every conditional trait source" + (missing != null ? " (missing: " + missing + ")" : ""));
        if (sink == null)
            return;

        CharacterController slot = NewController("CRE046_Slot");
        CharacterController partner = NewController("CRE046_Partner");
        CharacterController fresh = NewController("CRE046_Fresh");
        try
        {
            Spawn(slot, sink, SlotPos);
            Spawn(partner, Def("orc_warrior"), PartnerPos);

            List<string> before = TraitsOf(slot);
            Assert(before.Count >= 11, "CRE-046 control: the kitchen sink spawns with 11 traits (has " + string.Join(", ", before) + ")");

            // Dirty the runtime state the way a fight would.
            MethodInfo establish = typeof(CharacterController).GetMethod("EstablishGrappleWith", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(establish != null, "CRE-046: CharacterController.EstablishGrappleWith exists for the grapple-link check");
            if (establish != null)
                establish.Invoke(slot, new object[] { partner });
            Assert(slot.IsGrappling() && partner.IsGrappling(), "CRE-046 control: the slot and its partner grapple before the reuse");

            ActiveSpellEffect bulls = slot.StatusEffectManager != null
                ? slot.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.BULLS_STRENGTH)?.Clone(), "Test", 5)
                : null;
            Assert(bulls != null && slot.StatusEffectManager.ActiveEffects.Count > 0, "CRE-046 control: a spell effect sits on the slot before the reuse");
            slot.ApplyCondition(CombatConditionType.Shaken, 5, "CRE-046 test");
            slot.ApplyHasteEffect(5, slot);
            slot.Stats.BaseAttackBonusOverride = 4;
            slot.SetPowerAttack(2);
            slot.SetRapidShot(true);
            slot.HasMovedThisTurn = true;
            slot.Actions.MoveActionUsed = true;
            slot.Tags.AddTag("ScenarioOverride:CRE046");
            slot.gameObject.AddComponent<LastKnownPositionTracker>();
            slot.ConfigureBombardierAcidSprayCooldown(3);
            slot.MarkFrightfulPresenceTriggered();
            slot.PriorityTargetName = "Someone";
            SpriteRenderer sr = slot.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.red;

            Spawn(slot, Def("orc_warrior"), SlotPos);
            Spawn(fresh, Def("orc_warrior"), FreshPos);

            List<string> after = TraitsOf(slot);
            Assert(after.Count == 0, "CRE-046: an orc warrior in the reused slot has none of the kitchen sink's traits (left: " + string.Join(", ", after) + ")");
            Assert(!slot.IsGrappling() && !partner.IsGrappling() && !partner.HasCondition(CombatConditionType.Grappled),
                "CRE-046: the reuse ends the old creature's grapple and frees its partner");
            Assert(slot.StatusEffectManager != null && slot.StatusEffectManager.ActiveEffects.Count == 0, "CRE-046: no spell effect of the old creature remains");
            Assert(!slot.HasCondition(CombatConditionType.Shaken) && slot.GetActiveConditions().Count == fresh.GetActiveConditions().Count,
                "CRE-046: no condition of the old creature remains");
            Assert(slot.ActiveHasteEffect == null, "CRE-046: the old creature's Haste record is gone");
            Assert(slot.PowerAttackValue == 0 && !slot.RapidShotEnabled && slot.CurrentAttackDamageMode == fresh.CurrentAttackDamageMode,
                "CRE-046: Power Attack, Rapid Shot and the damage mode start from the defaults");
            Assert(!slot.HasMovedThisTurn && !slot.Actions.MoveActionUsed, "CRE-046: turn and action-economy state start fresh");
            Assert(!slot.Tags.HasTag("ScenarioOverride:CRE046") && TagSet(slot) == TagSet(fresh),
                "CRE-046: the tags equal a fresh orc warrior's (" + TagSet(slot) + " vs " + TagSet(fresh) + ")");
            Assert(slot.GetComponent<LastKnownPositionTracker>() == null, "CRE-046: the old creature's AI position tracker is gone");
            Assert(ComponentSet(slot) == ComponentSet(fresh),
                "CRE-046: the reused slot has the components of a fresh spawn (" + ComponentSet(slot) + " vs " + ComponentSet(fresh) + ")");
            Assert(slot.PriorityTargetName == null && slot.aiProfile != null && fresh.aiProfile != null && slot.aiProfile.GetType() == fresh.aiProfile.GetType(),
                "CRE-046: the AI profile and target priority are the orc warrior's");
            Assert(sr == null || sr.color == Color.white, "CRE-046: the old creature's sprite tint is gone");
            Assert(slot.Stats.STR == fresh.Stats.STR && slot.Stats.BaseAttackBonus == fresh.Stats.BaseAttackBonus,
                "CRE-046: STR and BAB equal a fresh orc warrior's (no Bull's Strength, no BAB override)");
            Assert(!slot.HasFrightfulPresenceTriggered, "CRE-046: the frightful-presence trigger flag is clear");
            Assert(!slot.IsDead && slot.CurrentHPState == fresh.CurrentHPState, "CRE-046: the HP state is the new creature's");
        }
        finally
        {
            Cleanup(slot, partner, fresh);
        }
    }

    /// <summary>A prepared caster, then an orc warrior: no spellcasting component and none of the caster's gear stays.</summary>
    private static void TestCasterThenPlainCreature()
    {
        CharacterController slot = NewController("CRE046_CasterSlot");
        try
        {
            NPCDefinition adept = Def("arcane_missile_adept");
            Assert(adept != null, "CRE-046: arcane_missile_adept exists");
            if (adept == null)
                return;

            Spawn(slot, adept, SlotPos);
            Assert(slot.Spellcasting != null, "CRE-046 control: the adept spawns with a SpellcastingComponent");

            Spawn(slot, Def("orc_warrior"), SlotPos);
            Assert(slot.Spellcasting == null && slot.GetComponent<SpellcastingComponent>() == null,
                "CRE-046: an orc warrior in the adept's slot has no SpellcastingComponent");

            global::Inventory inv = slot.InventoryComp != null ? slot.InventoryComp.CharacterInventory : null;
            ItemData main = inv != null ? inv.RightHandSlot : null;
            Assert(main != null && main.Id != null && !main.Id.Contains("quarterstaff"),
                "CRE-046: the orc warrior wields its own weapon, not the adept's quarterstaff (" + (main != null ? main.Id : "none") + ")");
        }
        finally
        {
            Cleanup(slot);
        }
    }

    /// <summary>The reset must not get in the way of the next creature's own traits: an orc, then an allip.</summary>
    private static void TestPlainCreatureThenTraitCreature()
    {
        CharacterController slot = NewController("CRE046_PlainSlot");
        try
        {
            Spawn(slot, Def("orc_warrior"), SlotPos);
            Spawn(slot, Def("allip"), SlotPos);
            Assert(slot.IsIncorporeal && slot.HasAuraAbility, "CRE-046: an allip spawned into an orc's slot is incorporeal and has its Babble aura");
        }
        finally
        {
            Cleanup(slot);
        }
    }

    /// <summary>
    /// Respawning a slot clears what other characters keep on it: the observer (registered in GameManager.NPCs so
    /// that ResetCharacterSlotForSpawn's loop over other characters reaches it) loses its remembered square, its feint
    /// window against the slot and a counterspell readied against it.
    /// </summary>
    private static void TestRespawnClearsOthersMemoryOfSlot()
    {
        GameManager gm = GameManager.Instance;
        CharacterController observer = NewController("CRE046_Observer");
        CharacterController slot = NewController("CRE046_Seen");
        bool listed = false;
        try
        {
            Spawn(observer, Def("orc_warrior"), PartnerPos);
            Spawn(slot, Def("orc_warrior"), SlotPos);
            gm.NPCs.Add(observer);
            listed = true;

            LastKnownPositionTracker tracker = observer.gameObject.AddComponent<LastKnownPositionTracker>();
            tracker.UpdateLastKnownPosition(slot);
            Assert(tracker.HasLastKnownPosition(slot), "CRE-046 control: the observer remembers the creature's square");

            MethodInfo feint = typeof(CharacterController).GetMethod("RegisterSuccessfulFeint", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(feint != null, "CRE-046: CharacterController.RegisterSuccessfulFeint exists for the feint-window check");
            if (feint != null)
                feint.Invoke(observer, new object[] { slot });
            Assert(FeintWindowCount(observer) == 1, "CRE-046 control: the observer holds a feint window against the creature");

            PropertyInfo readied = typeof(CharacterController).GetProperty("ReadiedCounterspell", BindingFlags.Instance | BindingFlags.Public);
            if (readied != null)
                readied.SetValue(observer, new DND35.Magic.CounterspellData { Counterspeller = observer, WatchedCaster = slot });
            Assert(observer.ReadiedCounterspell != null && observer.ReadiedCounterspell.WatchedCaster == slot,
                "CRE-046 control: the observer has a counterspell readied against the creature");

            Spawn(slot, Def("orc_warrior"), SlotPos);

            Assert(!tracker.HasLastKnownPosition(slot), "CRE-046: the respawn drops the observer's remembered square");
            Assert(FeintWindowCount(observer) == 0, "CRE-046: the respawn drops the observer's feint window against the old creature");
            Assert(observer.ReadiedCounterspell == null, "CRE-046: the respawn drops a counterspell readied against the old creature");
        }
        finally
        {
            if (listed)
                gm.NPCs.Remove(observer);
            Cleanup(observer, slot);
        }
    }

    /// <summary>Number of feint windows <paramref name="cc"/> holds (private list, read by name; TST-006).</summary>
    private static int FeintWindowCount(CharacterController cc)
    {
        FieldInfo f = typeof(CharacterController).GetField("_activeFeintWindows", BindingFlags.Instance | BindingFlags.NonPublic);
        return f != null && f.GetValue(cc) is System.Collections.ICollection list ? list.Count : -1;
    }

    private static CharacterStats PcStats(string name)
    {
        return new CharacterStats(name, 3, "Fighter",
            16, 12, 14, 10, 10, 10,
            3, 0, 0,
            8, 1, 0,
            6, 1, 24,
            "Human");
    }

    /// <summary>Builds a party character in <paramref name="cc"/> the way the presets and the harness do.</summary>
    private static void SetupParty(CharacterController cc, CharacterStats stats)
    {
        GameManager.Instance.ResetPCSlotForNewCharacter(cc);
        cc.Init(stats, SlotPos, null, null);
        InventoryComponent inv = cc.GetComponent<InventoryComponent>() ?? cc.gameObject.AddComponent<InventoryComponent>();
        inv.Init(stats);
        StatusEffectManager sem = cc.StatusEffectManager;
        if (sem == null)
            sem = cc.gameObject.AddComponent<StatusEffectManager>();
        sem.Init(stats);
    }

    /// <summary>
    /// A party slot that gets a different character (character creation, a test-party preset, the scenario harness)
    /// goes through GameManager.ResetPCSlotForNewCharacter: the old character's feat toggles, turn state, tags, AI
    /// tracker, feint window and spell effects must not carry over to the new one.
    /// </summary>
    private static void TestPartySlotGetsNewCharacter()
    {
        CharacterController pc = NewController("CRE046_PartySlot");
        CharacterController partner = NewController("CRE046_PartyPartner");
        try
        {
            SetupParty(pc, PcStats("Old Fighter"));
            Spawn(partner, Def("orc_warrior"), PartnerPos);

            pc.SetPowerAttack(2);
            pc.SetFightingDefensively(true);
            pc.SetRapidShot(true);
            pc.HasMovedThisTurn = true;
            pc.Tags.AddTag("ScenarioOverride:CRE046Party");
            pc.gameObject.AddComponent<LastKnownPositionTracker>();
            MethodInfo feint = typeof(CharacterController).GetMethod("RegisterSuccessfulFeint", BindingFlags.Instance | BindingFlags.NonPublic);
            if (feint != null)
                feint.Invoke(pc, new object[] { partner });
            ActiveSpellEffect bulls = pc.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.BULLS_STRENGTH)?.Clone(), "Test", 5);
            Assert(pc.PowerAttackValue == 2 && pc.IsFightingDefensively && FeintWindowCount(pc) == 1 && bulls != null,
                "CRE-046 control: the old party character has Power Attack 2, fights defensively, holds a feint window and has Bull's Strength");

            SetupParty(pc, PcStats("New Fighter"));

            Assert(pc.PowerAttackValue == 0 && !pc.IsFightingDefensively && !pc.RapidShotEnabled,
                "CRE-046: the new party character starts without the old one's Power Attack, Fighting Defensively and Rapid Shot");
            Assert(!pc.HasMovedThisTurn, "CRE-046: the new party character's turn state starts fresh");
            Assert(!pc.Tags.HasTag("ScenarioOverride:CRE046Party"), "CRE-046: the old party character's tag is gone");
            Assert(pc.GetComponent<LastKnownPositionTracker>() == null, "CRE-046: the old party character's AI position tracker is gone");
            Assert(FeintWindowCount(pc) == 0, "CRE-046: the old party character's feint window is gone");
            Assert(pc.StatusEffectManager != null && pc.StatusEffectManager.ActiveEffects.Count == 0 && pc.Stats.STR == 16,
                "CRE-046: no spell effect of the old party character remains (STR 16, no Bull's Strength)");
            Assert(pc.GetComponents<InventoryComponent>().Length == 1 && pc.InventoryComp != null
                    && pc.InventoryComp.CharacterInventory != null && pc.InventoryComp.CharacterInventory.OwnerStats == pc.Stats,
                "CRE-046: the slot keeps one InventoryComponent, re-initialised for the new character");
        }
        finally
        {
            Cleanup(pc, partner);
        }
    }

    /// <summary>
    /// A creature that saved against a slot's aura is immune to that creature's aura only (MM: 24 hours against the
    /// same creature); the immunity must not carry over to the next creature in the slot.
    /// </summary>
    private static void TestAuraSaveImmunityForgottenWithSource()
    {
        MethodInfo grant = typeof(AIService).GetMethod("GrantAuraSaveImmunity", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo has = typeof(AIService).GetMethod("HasAuraSaveImmunity", BindingFlags.Static | BindingFlags.NonPublic);
        Assert(grant != null && has != null, "CRE-046: AIService aura immunity helpers exist");
        if (grant == null || has == null)
            return;

        CharacterController target = NewController("CRE046_AuraTarget");
        CharacterController slot = NewController("CRE046_AuraSource");
        try
        {
            Spawn(target, Def("orc_warrior"), PartnerPos);
            Spawn(slot, Def("allip"), SlotPos);
            grant.Invoke(null, new object[] { target, slot, "Babble" });
            Assert((bool)has.Invoke(null, new object[] { target, slot, "Babble" }), "CRE-046 control: the target is immune to this allip's Babble");

            Spawn(slot, Def("allip"), SlotPos);
            Assert(!(bool)has.Invoke(null, new object[] { target, slot, "Babble" }),
                "CRE-046: a new allip in the same slot is a new creature, so the old immunity does not apply");
        }
        finally
        {
            Cleanup(target, slot);
        }
    }
}
}
