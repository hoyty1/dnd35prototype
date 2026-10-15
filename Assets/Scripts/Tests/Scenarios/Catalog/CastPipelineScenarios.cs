#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SpellNames = DND35e.Identifiers.SpellNames;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 11 step 3 (docs/designs/cast_core_batch11.md, cast-pipeline-pin-scenarios): pins today's spell cast behaviour
    /// before the cast pipelines are merged into one cast core. Ui actors cast through the PC callbacks (the spell menu,
    /// the touch-spell prompt, the Use Item call, the Discharge button and the target click); Scripted actors through the
    /// NPC cast executor. Every scenario records what happened as facts (trace notes "cpp key=value", written by Assert
    /// steps) and judges them in its expectations: a plain expectation states a rule (with its page) that holds today; a
    /// known-behaviour pin (ScenarioBuilder.ExpectKnown) states today's behaviour that a filed issue says is not the rule
    /// or not yet shown to be, so the job is Known until a later batch 11 step changes it. A pin is not a ruling.
    /// Casters are Stats-built Humans (Wizard INT 16, Cleric and Druid WIS 16) with the class kit; the spells a scenario
    /// casts are put in their prepared slots (clones, never the templates). Targets are target_dummy (Commoner 1, 50 base
    /// HP, no weapon, so it threatens no square) unless a rule needs another creature; saves are decided with a +-30 save
    /// tweak and damage dice are forced, so every seed gives the same numbers.
    /// </summary>
    public static class CastPipelineScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("cast-ui-fireball", UiFireball);
            yield return S("cast-ui-cone-line", UiConeLine);
            yield return S("cast-ui-sleep", UiSleep);
            yield return S("cast-ui-sleep-quickened", UiSleepQuickened);
            yield return S("cast-scroll-ui", () => ItemCast(scroll: true));
            yield return S("cast-wand-ui", () => ItemCast(scroll: false));
            yield return S("cast-scroll-aoo-cancel", () => ItemAoOCancel(scroll: true));
            yield return S("cast-wand-aoo-cancel", () => ItemAoOCancel(scroll: false));
            yield return S("cast-held-touch-charge", HeldTouchCharge);
            yield return S("cast-held-aoo-cancel", HeldAoOCancel);
            yield return S("cast-ui-summon-monster", UiSummonMonster);
            yield return S("cast-ui-summon-aoo-cancel", UiSummonAoOCancel);
            yield return S("cast-ui-summon-entangled", UiSummonEntangled);
            yield return S("cast-ui-summon-swarm", UiSummonSwarm);
            yield return S("cast-ui-grease", UiGrease);
            foreach (PairSpec pair in Pairs())
            {
                PairSpec p = pair;
                yield return S(p.Id != null ? p.Id.Substring("rules/".Length) : "cast-pair-" + p.Family, () => PairScenario(p));
            }
            yield return S("cast-ai-spiritual-weapon", AiSpiritualWeapon);
            yield return S("cast-ai-skips-dimension-door", AiSkipsDimensionDoor);
            yield return S("cast-globe-damage", GlobeDamage);
            yield return S("area-globe", AreaGlobe);
            yield return S("cast-globe-held-touch", GlobeHeldTouch);
            yield return S("cast-globe-dispel", GlobeDispel);
            yield return S("cast-globe-grease-armor", GlobeGreaseArmor);
            yield return S("prayer-globe", PrayerGlobe);
            yield return S("area-globe-invisible", AreaGlobeInvisible);
            yield return S("cast-quickened-threatened", QuickenedThreatened);
            yield return S("cast-empowered-fireball", EmpoweredFireball);
            yield return S("cast-ally-in-burning-hands", AllyInBurningHands);
            yield return S("cast-entangled", Entangled);
            yield return S("cast-slot-failure-refund", SlotFailureRefund);
            yield return S("stale-cast-state-energy", StaleEnergyChoice);
            yield return S("stale-cast-state-fire-shield", FireShieldChoicePerCast);
            yield return S("stale-cast-state-wand-asf", StaleWandFlag);
            yield return S("stale-cast-state-f12-bypass", TestPanelBypassThroughResolution);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("CastPipelineScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "cast-pipeline").Covers("PC_NPC_PARITY", "batch 11 step 3");

        private static ScenarioBuilder StaleRules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "cast-pipeline", "cast-context").Covers("PC_NPC_PARITY", "batch 11 step 4");

        // ── Builders ────────────────────────────────────────────────────

        /// <summary>A Human wizard: STR 10, DEX 12, CON 12, INT 16 (+3); BAB from the class table; Wizard kit.</summary>
        private static CharacterStats Wizard(string name, int level, params string[] feats)
            => Caster(name, "Wizard", level, wis: 10, intelligence: 16, feats);

        /// <summary>A Human neutral cleric: STR 10, DEX 12, CON 12, WIS 16 (+3); Cleric kit.</summary>
        private static CharacterStats Cleric(string name, int level) => Caster(name, "Cleric", level, wis: 16, intelligence: 10);

        private static CharacterStats Caster(string name, string cls, int level, int wis, int intelligence, params string[] feats)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: cls,
                str: 10, dex: 12, con: 12, wis: wis, intelligence: intelligence, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 6 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.TrueNeutral;
            s.EnsureMulticlassDataInitialized();
            foreach (string f in feats)
                s.Feats.Add(f);
            return s;
        }

        /// <summary>
        /// Puts clones of <paramref name="spellId"/> into the first <paramref name="count"/> unused non-domain slots of
        /// <paramref name="level"/>. Throws (the job ends SetupFailed) when there are not enough such slots.
        /// </summary>
        private static void Prepare(CharacterController c, int level, string spellId, int count = 1)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting to prepare " + spellId + " in");
            int placed = 0;
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (placed >= count)
                    break;
                if (slot == null || slot.Level != level || slot.IsDomainSlot || slot.IsUsed)
                    continue;
                if (slot.PreparedSpell != null && slot.PreparedSpell.SpellId == spellId)
                {
                    placed++;
                    continue;
                }
                slot.PreparedSpell = SpellDatabase.GetSpell(spellId).Clone();
                placed++;
            }
            if (placed < count)
                throw new InvalidOperationException(c.Stats.CharacterName + " has " + placed + " regular level-" + level + " slots for " + spellId + ", needs " + count);
            sc.SyncPreparedSpellsFromSlots();
        }

        /// <summary>
        /// Adds a wizard slot of <paramref name="level"/> holding a clone of <paramref name="spellId"/>. The prototype's
        /// wizard table stops at 2nd-level slots from wizard 4 on (SPL-041), so a scenario that needs a wizard's 3rd-,
        /// 4th- or 5th-level slot (PHB p.55) adds the one it casts from; the slot spend matches the spell (or, with
        /// metamagic, the effective level), not the class table.
        /// </summary>
        private static void AddWizardSlot(CharacterController c, int level, string spellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting to add a slot to");
            sc.SpellSlots.Add(new SpellSlot(level, SpellDatabase.GetSpell(spellId).Clone(), false, "Wizard"));
            sc.SyncPreparedSpellsFromSlots();
        }

        private static void SaveTweak(CharacterController c, int value)
            => c.Stats.Bonuses.Set("scenario:save-tweak", BonusTarget.AllSaves, BonusType.Untyped, value, "scenario save tweak");

        private static void AttackTweak(CharacterController c, int value)
            => c.Stats.Bonuses.Set("scenario:attack-tweak", BonusTarget.AttackRoll, BonusType.Untyped, value, "scenario attack tweak");

        /// <summary>100 more hit points, so no forced damage in these scenarios drops the creature.</summary>
        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(100);
            c.Stats.CurrentHP += 100;
        }

        private static Action<CharacterController> Fails => c => { SaveTweak(c, -30); Sturdy(c); };
        private static Action<CharacterController> Saves => c => { SaveTweak(c, 30); Sturdy(c); };

#pragma warning disable CS0618 // scroll and wand ids are generated at runtime (ScrollFactory, WandFactory): no ItemID value
        private static void GiveItem(CharacterController c, string itemId)
        {
            ItemData item = ItemDatabase.CloneItem(itemId);
            if (item == null)
                throw new InvalidOperationException("no item " + itemId + " in the ItemDatabase");
            global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
            if (inv == null || !inv.AddItem(item))
                throw new InvalidOperationException(c.Stats.CharacterName + " cannot carry " + itemId);
        }
#pragma warning restore CS0618

        private static readonly string ScrollId = ScrollFactory.GenerateScrollId(SpellNames.MAGIC_MISSILE, "Arcane");
        private static readonly string WandId = WandFactory.GenerateWandId(SpellNames.MAGIC_MISSILE);

        // ── Facts ───────────────────────────────────────────────────────

        private const string FactPrefix = "cpp ";

        /// <summary>
        /// Baselines (hit points, used slots) recorded by a job's "record" Assert steps, kept per job context so that no
        /// job, seed or repetition can read another's.
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScenarioContext, Dictionary<string, int>> _baselines
            = new System.Runtime.CompilerServices.ConditionalWeakTable<ScenarioContext, Dictionary<string, int>>();

        private static Dictionary<string, int> Numbers(ScenarioContext ctx) => _baselines.GetOrCreateValue(ctx);

        private static void Fact(ScenarioContext ctx, string key, object value) => ctx.Note(FactPrefix + key + "=" + value);

        private static string FactOf(TraceView v, string key)
        {
            string prefix = FactPrefix + key + "=";
            TraceEvent note = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
            return note != null ? note.Str("text").Substring(prefix.Length) : null;
        }

        /// <summary>The fact <paramref name="key"/> equals <paramref name="want"/> (inconclusive when it was never recorded).</summary>
        private static Func<TraceView, ExpectResult> FactIs(string key, object want)
            => v =>
            {
                string got = FactOf(v, key);
                string w = Convert.ToString(want, System.Globalization.CultureInfo.InvariantCulture);
                if (got == null)
                    return ExpectResult.Inconclusive("no fact " + key);
                return got == w ? ExpectResult.Pass(key + "=" + got) : ExpectResult.Fail(key + "=" + got + ", expected " + w);
            };

        private static Func<TraceView, ExpectResult> FactsAre(params (string key, object want)[] facts)
            => Expect.All(facts.Select(f => FactIs(f.key, f.want)).ToArray());

        private static Func<TraceView, ExpectResult> FactsEqual(string a, string b)
            => v =>
            {
                string x = FactOf(v, a), y = FactOf(v, b);
                if (x == null || y == null)
                    return ExpectResult.Inconclusive("missing fact " + (x == null ? a : b));
                return x == y ? ExpectResult.Pass(a + " = " + b + " = " + x) : ExpectResult.Fail(a + "=" + x + " but " + b + "=" + y);
            };

        /// <summary>One 'cast' trace event (ScenarioHooks.SpellCast) by <paramref name="by"/> for <paramref name="spellId"/>.</summary>
        private static Func<TraceView, ExpectResult> CastEvents(string by, string spellId, int count)
            => Expect.Count("cast", e => e.Str("by") == by && e.Str("spell") == spellId, count, count);

        private static void RecordHp(ScenarioContext ctx, params string[] keys)
        {
            foreach (string k in keys)
                Numbers(ctx)["hp|" + k] = ctx.Get(k).Stats.CurrentHP;
        }

        /// <summary>
        /// Records the hit points <paramref name="keys"/> lost since <see cref="RecordHp"/> as facts "lost.&lt;key&gt;". A key
        /// with no recorded baseline gets no fact, so the expectations that read it are inconclusive, never a vacuous 0.
        /// </summary>
        private static void FactHpLost(ScenarioContext ctx, string tag, params string[] keys)
        {
            foreach (string k in keys)
            {
                if (!Numbers(ctx).TryGetValue("hp|" + k, out int before))
                    continue;
                Fact(ctx, tag + ".lost." + k, before - ctx.Get(k).Stats.CurrentHP);
            }
        }

        /// <summary>Used and total prepared slots per spell level, such as "L0:0/4 L1:1/4".</summary>
        private static string Slots(CharacterController c)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                return "none";
            return string.Join(" ", sc.SpellSlots.Where(s => s != null).GroupBy(s => s.Level).OrderBy(g => g.Key)
                .Select(g => "L" + g.Key + ":" + g.Count(s => s.IsUsed) + "/" + g.Count()));
        }

        private static void RecordSlots(ScenarioContext ctx, string key)
        {
            SpellcastingComponent sc = ctx.Get(key).Spellcasting;
            if (sc == null)
                return;
            Numbers(ctx)["slots|" + key] = 1;
            foreach (IGrouping<int, SpellSlot> g in sc.SpellSlots.Where(s => s != null).GroupBy(s => s.Level))
                Numbers(ctx)["slots|" + key + "|" + g.Key] = g.Count(s => s.IsUsed);
        }

        /// <summary>
        /// Records the slots <paramref name="key"/> spent since <see cref="RecordSlots"/> as the fact &lt;tag&gt;, such as
        /// "L3+1" (one 3rd-level slot), or "none"; no fact when no baseline was recorded (the expectation is inconclusive).
        /// A slot level added after the baseline counts from 0 used.
        /// </summary>
        private static void FactSlotsSpent(ScenarioContext ctx, string tag, string key)
        {
            if (!Numbers(ctx).ContainsKey("slots|" + key))
                return;
            SpellcastingComponent sc = ctx.Get(key).Spellcasting;
            var spent = new List<string>();
            if (sc != null)
                foreach (IGrouping<int, SpellSlot> g in sc.SpellSlots.Where(s => s != null).GroupBy(s => s.Level).OrderBy(g => g.Key))
                {
                    int before = Numbers(ctx).TryGetValue("slots|" + key + "|" + g.Key, out int b) ? b : 0;
                    int now = g.Count(s => s.IsUsed);
                    if (now != before)
                        spent.Add("L" + g.Key + (now > before ? "+" : "") + (now - before));
                }
            Fact(ctx, tag, spent.Count > 0 ? string.Join(" ", spent) : "none");
        }

        /// <summary>The actor's actions left this turn: s(tandard) m(ove) f(ull-round), a dash for a spent one.</summary>
        private static string Actions(CharacterController c)
        {
            ActionEconomy a = c.Actions;
            return (a.HasStandardAction ? "s" : "-") + (a.HasMoveAction ? "m" : "-") + (a.HasFullRoundAction ? "f" : "-");
        }

        private static int CountItems(CharacterController c, string itemId)
        {
            global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
            if (inv == null || inv.GeneralSlots == null)
                return 0;
            int n = 0;
            foreach (ItemData item in inv.GeneralSlots)
                if (item != null && item.Id == itemId)
                    n += item.IsStackable ? item.StackCount : 1;
            return n;
        }

        private static ItemData FindItem(CharacterController c, string itemId)
        {
            global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
            return inv != null && inv.GeneralSlots != null ? inv.GeneralSlots.FirstOrDefault(i => i != null && i.Id == itemId) : null;
        }

        // ── rules/cast-ui-fireball ──────────────────────────────────────

        /// <summary>
        /// A Ui area cast through PerformAoESpellCast: a wizard 5 (Ui) casts Fireball (PHB p.231: 1d6 per caster level, at
        /// most 10d6, 20-ft-radius spread, Reflex half) centred on d1; d1, d2 and d3 are in the burst, the wizard 40 ft
        /// away is not (Fireball is in an added 3rd-level slot: the wizard table stops at 2nd-level slots, SPL-041). Every d6 is forced to 6 (5d6 = 30); d1 and d3 fail their Reflex saves (-30), d2 makes it (+30)
        /// and takes half (15). The cast spends one 3rd-level slot and the standard action (PHB p.174), leaves the move
        /// action, and the trace has one 'cast' event (ScenarioHooks.SpellCast, raised by the PC pipelines since step 3).
        /// </summary>
        private static ScenarioDef UiFireball()
        {
            return Rules("rules/cast-ui-fireball", "A Ui Fireball: Reflex saves, damage per target, one 3rd-level slot and the standard action (PHB p.231, p.174)")
                .Covers("PHB p.231", "PHB p.174", "PHB p.177")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 2, 10, Control.Ui)
                .Npc("d1", "target_dummy", 10, 10, Control.Idle)
                .Npc("d2", "target_dummy", 11, 10, Control.Idle)
                .Npc("d3", "target_dummy", 10, 12, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 3, SpellNames.FIREBALL))
                .Tweak("d1", Fails).Tweak("d2", Saves).Tweak("d3", Fails)
                .Initiative("wizard", "d1", "d2", "d3")
                .Force(6, 6, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d1", "d2", "d3"); RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.FIREBALL, "d1"),
                    Step.Assert("after", ctx =>
                    {
                        FactHpLost(ctx, "fireball", "d1", "d2", "d3");
                        FactSlotsSpent(ctx, "spent", "wizard");
                        Fact(ctx, "actions", Actions(ctx.Get("wizard")));
                        return true;
                    }))
                .Expect("The Ui cast ran", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("5d6 = 30 on a failed Reflex save, half (15) on a made one (PHB p.231, p.177)",
                    FactsAre(("fireball.lost.d1", 30), ("fireball.lost.d2", 15), ("fireball.lost.d3", 30)))
                .Expect("One 3rd-level slot is spent", FactIs("spent", "L3+1"))
                .Expect("Casting is a standard action: the move action is left (PHB p.174)", FactIs("actions", "-m-"))
                .Expect("The PC area cast is in the trace (ScenarioHooks.SpellCast)", CastEvents("wizard", SpellNames.FIREBALL, 1))
                .Build();
        }

        // ── rules/cast-ui-sleep, rules/cast-ui-sleep-quickened (batch 11 step 9) ──────

        /// <summary>A trace condition event: <paramref name="actor"/> gained (or lost) <paramref name="type"/>.</summary>
        private static bool IsCondition(TraceEvent e, string actor, string type, bool added)
            => e.Str("actor") == actor && Convert.ToString(e.Get("type")) == type && e.Bool("added") == added;

        /// <summary>
        /// A Ui area cast resolved by one of the area handlers that used to show its own log and pace the turn
        /// (ResolveSleepSpell; batch 11 step 9 moved that into the cast core's FinishAreaCast). A wizard 1 (Ui) casts Sleep
        /// (PHB p.280: 4d4 HD of creatures, those with 4 HD or less, lowest first, in a 10-ft-radius burst; Will negates)
        /// centred on g1; g1 and g2, goblins of 1 HD, are in the burst. Every d4 is 4 (a 16 HD pool); g1 fails its Will
        /// save and falls asleep, g2 makes it. The cast spends the 1st-level slot, logs one Sleep result, and the turn goes
        /// on (the step settles at the action menu); the fight is not over (g2 is awake). Sleep's casting time is 1 round
        /// (PHB p.280), a full-round action (PHB p.174), but the data stores it as a standard action and the cast core's
        /// Cost stage never reads SpellData.ActionType, so today the move action is left (SPL-162, pinned).
        /// </summary>
        private static ScenarioDef UiSleep()
        {
            return Rules("rules/cast-ui-sleep", "A Ui Sleep: an area handler resolves it and the cast core finishes it (PHB p.280; SPL-162 pinned)")
                .Covers("PHB p.280", "PHB p.174", "SPL-162", "batch 11 step 9")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 1)), 2, 10, Control.Ui)
                .Npc("g1", "goblin", 8, 10, Control.Idle)
                .Npc("g2", "goblin", 9, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SLEEP))
                .Tweak("g1", Fails).Tweak("g2", Saves)
                .Initiative("wizard", "g1", "g2")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.SLEEP, "g1"),
                    Step.Assert("after", ctx =>
                    {
                        Fact(ctx, "asleep.g1", ctx.Get("g1").HasCondition(CombatConditionType.Asleep));
                        Fact(ctx, "asleep.g2", ctx.Get("g2").HasCondition(CombatConditionType.Asleep));
                        FactSlotsSpent(ctx, "spent", "wizard");
                        Fact(ctx, "actions", Actions(ctx.Get("wizard")));
                        return true;
                    }))
                .Expect("The Ui cast ran and the turn went on", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("g1 failed its Will save and sleeps, g2 made it (PHB p.280)", FactsAre(("asleep.g1", true), ("asleep.g2", false)))
                .Expect("One 1st-level slot is spent", FactIs("spent", "L1+1"))
                .ExpectKnown("SPL-162", "Today Sleep costs only the standard action and the move action is left; RAW its 1-round casting time is a full-round action (PHB p.280, p.174; SPL-162)",
                    FactIs("actions", "-m-"))
                .Expect("One Sleep result is logged", Expect.Count("log", e => (e.Str("text") ?? "").Contains("casts Sleep!"), 1, 1))
                .Expect("The PC area cast is in the trace (ScenarioHooks.SpellCast)", CastEvents("wizard", SpellNames.SLEEP, 1))
                .Build();
        }

        /// <summary>
        /// The same handler with a quickened Sleep that ends the fight. A wizard 9 with Quicken Spell (Ui) casts a quickened
        /// Sleep from an added 5th-level slot (PHB p.98: free action, +4 levels; SPL-041 for the slot) at g1; both goblins
        /// fail their Will saves and fall asleep, so the hostile side is out (asleep counts as unconscious) and the cast
        /// core's area exit (FinishAreaCast, Sleep's own combat-end check) declares the victory. Today Sleep's log has no
        /// quickened line (SPL-161, pinned).
        /// </summary>
        private static ScenarioDef UiSleepQuickened()
        {
            var quicken = new MetamagicData();
            quicken.Toggle(MetamagicFeatId.QuickenSpell);
            return Rules("rules/cast-ui-sleep-quickened", "A quickened Ui Sleep puts both foes to sleep and the area exit ends the fight (SPL-161 pinned)")
                .Covers("PHB p.280", "PHB p.98", "SPL-161", "batch 11 step 9")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 9, "Quicken Spell")), 2, 10, Control.Ui)
                .Npc("g1", "goblin", 8, 10, Control.Idle)
                .Npc("g2", "goblin", 9, 10, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 5, SpellNames.SLEEP))
                .Tweak("g1", Fails).Tweak("g2", Fails)
                .Initiative("wizard", "g1", "g2")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Cast(SpellNames.SLEEP, "g1", quicken))
                .Expect("Both goblins fall asleep (PHB p.280)", Expect.All(
                    Expect.Count("cond", e => IsCondition(e, "g1", "Asleep", true), 1),
                    Expect.Count("cond", e => IsCondition(e, "g2", "Asleep", true), 1)))
                .Expect("The game declares the victory itself, from the area cast's combat-end check", Expect.Outcome(Outcome.Victory))
                .Expect("The PC area cast is in the trace (ScenarioHooks.SpellCast)", CastEvents("wizard", SpellNames.SLEEP, 1))
                .ExpectKnown("SPL-161", "Today the quickened Sleep logs no quickened line (SPL-161)",
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("casts QUICKENED Sleep"), 0, 0))
                .Build();
        }

        // ── rules/cast-ui-cone-line ─────────────────────────────────────

        /// <summary>
        /// The other two area shapes through the Ui pipeline. Round 1: a wizard 5 casts Burning Hands (PHB p.207: 1d4 per
        /// caster level, at most 5d4, 15-ft cone, Reflex half; the generic area loop and SpellCaster.Cast) east at b1 (10
        /// ft) and b2 (15 ft); every d4 is 4, b1 fails (20), b2 saves (10). Round 2: Lightning Bolt (PHB p.248: 1d6 per
        /// level, at most 10d6, 120-ft line, Reflex half; TryResolveScaledAoEDamageSpell) north through l1 and l2; every d6
        /// is 6, l1 fails (30), l2 saves (15). Each cast spends one slot of its level.
        /// </summary>
        private static ScenarioDef UiConeLine()
        {
            return Rules("rules/cast-ui-cone-line", "Ui Burning Hands (cone) and Lightning Bolt (line): per-target saves and damage (PHB p.207, p.248)")
                .Covers("PHB p.207", "PHB p.248", "PHB p.175")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Npc("b1", "target_dummy", 7, 10, Control.Idle)
                .Npc("b2", "target_dummy", 8, 11, Control.Idle)
                .Npc("l1", "target_dummy", 5, 13, Control.Idle)
                .Npc("l2", "target_dummy", 5, 15, Control.Idle)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.BURNING_HANDS); AddWizardSlot(c, 3, SpellNames.LIGHTNING_BOLT); })
                .Tweak("b1", Fails).Tweak("b2", Saves).Tweak("l1", Fails).Tweak("l2", Saves)
                .Initiative("wizard", "b1", "b2", "l1", "l2")
                .Force(4, 4, null, -1)
                .Force(6, 6, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "b1", "b2", "l1", "l2"); RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.BURNING_HANDS, "b1"),
                    Step.Assert("after cone", ctx =>
                    {
                        FactHpLost(ctx, "cone", "b1", "b2", "l1", "l2");
                        FactSlotsSpent(ctx, "spent.cone", "wizard");
                        RecordHp(ctx, "b1", "b2", "l1", "l2");
                        RecordSlots(ctx, "wizard");
                        return true;
                    }))
                .Turn("wizard", 2,
                    Step.Cast(SpellNames.LIGHTNING_BOLT, "l2"),
                    Step.Assert("after line", ctx => { FactHpLost(ctx, "line", "b1", "b2", "l1", "l2"); FactSlotsSpent(ctx, "spent.line", "wizard"); return true; }))
                .Expect("Both Ui casts ran", Expect.All(Expect.StepStatus("wizard", 1, "Cast", 0, "done"), Expect.StepStatus("wizard", 2, "Cast", 0, "done")))
                .Expect("Burning Hands: 5d4 = 20 failed, 10 saved, nothing outside the cone (PHB p.207)",
                    FactsAre(("cone.lost.b1", 20), ("cone.lost.b2", 10), ("cone.lost.l1", 0), ("cone.lost.l2", 0)))
                .Expect("Lightning Bolt: 5d6 = 30 failed, 15 saved, nothing outside the line (PHB p.248)",
                    FactsAre(("line.lost.b1", 0), ("line.lost.b2", 0), ("line.lost.l1", 30), ("line.lost.l2", 15)))
                .Expect("One slot of each spell's level is spent", FactsAre(("spent.cone", "L1+1"), ("spent.line", "L3+1")))
                .Build();
        }

        // ── rules/cast-scroll-ui, rules/cast-wand-ui ────────────────────

        /// <summary>
        /// A Ui wizard 5 with no enemy adjacent uses a scroll (or a wand) of magic missile (caster level 1: one missile,
        /// 1d4+1, PHB p.251; DMG p.237 and p.245: the item's caster level) on a dummy through the Use Item call
        /// (TryUseConsumableFromInventory), which enters the cast pipeline (InitiateScrollCastThroughPipeline,
        /// InitiateWandCastThroughPipeline) and PerformSpellCast. Every d4 is 4. The scroll is used up (or the wand loses one
        /// charge, both charge fields, ITM-034), no prepared slot is spent, and no action is left: RAW retrieving the stored
        /// item is a move action (PHB p.141, Table 8-2) and activating it a standard action (DMG p.213). Pinned: today the
        /// item use is charged as one full-round action (GetItemUseActionType, ITM-005; the fact "fullround" tells the two
        /// apart, since both leave no action), and the missile count follows the user's caster level 5 (three missiles, 15),
        /// not the item's (SPL-016).
        /// </summary>
        private static ScenarioDef ItemCast(bool scroll)
        {
            string id = scroll ? ScrollId : WandId;
            string name = scroll ? "scroll" : "wand";
            ScenarioBuilder b = Rules("rules/cast-" + name + "-ui", "A Ui " + name + " of magic missile through the cast pipeline: the " + name + " is spent, no slot, the item action (DMG p.237, p.245)")
                .Covers("DMG p.237", "DMG p.245", "PHB p.251", "ITM-005", "ITM-034")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Tweak("wizard", c => GiveItem(c, id))
                .Initiative("wizard", "dummy")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => RecordItem(ctx, id, "before")),
                    Step.UseItem(id, "dummy"),
                    Step.Assert("after", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        RecordItem(ctx, id, "after");
                        FactHpLost(ctx, "missile", "dummy");
                        Fact(ctx, "actions", Actions(w));
                        Fact(ctx, "fullround", w.Actions.FullRoundActionUsed);
                        return true;
                    }))
                .Expect("The item use ran", Expect.StepStatus("wizard", 1, "UseItem", 0, "done"))
                .ExpectKnown("SPL-016", "Today the missiles count at the user's caster level 5: three missiles of 1d4+1 = 15, where the item's caster level 1 gives one (SPL-016)", FactIs("missile.lost.dummy", 15))
                .Expect("No prepared slot is spent", FactsEqual("slots.before", "slots.after"))
                .Expect("The item cast is in the trace (ScenarioHooks.SpellCast)", CastEvents("wizard", SpellNames.MAGIC_MISSILE, 1))
                .Expect("No action is left: a move action to retrieve the stored item and a standard action to activate it (PHB p.141 Table 8-2, DMG p.213)", FactIs("actions", "---"))
                .ExpectKnown("ITM-005", "Today the item use is charged as one full-round action, not a move and a standard action (ITM-005)", FactIs("fullround", true));
            b = scroll
                ? b.Expect("The scroll is used up", Expect.All(FactIs("count.before", 1), FactIs("count.after", 0)))
                : b.Expect("The wand loses one charge in both charge fields (ITM-034)", Expect.All(FactIs("charges.before", "50/50"), FactIs("charges.after", "49/49")));
            return b.Build();
        }

        private static bool RecordItem(ScenarioContext ctx, string itemId, string when)
        {
            CharacterController w = ctx.Get("wizard");
            if (when == "before")
                RecordHp(ctx, "dummy");
            Fact(ctx, "slots." + when, Slots(w));
            Fact(ctx, "count." + when, CountItems(w, itemId));
            ItemData item = FindItem(w, itemId);
            if (item != null && item.IsWand)
                Fact(ctx, "charges." + when, item.CurrentCharges + "/" + (item.Wand != null ? item.Wand.CurrentCharges : -1));
            return true;
        }

        // ── rules/cast-scroll-aoo-cancel, rules/cast-wand-aoo-cancel ────

        /// <summary>
        /// ITM-084: a threatened Ui wizard 5 starts a scroll (or wand) cast of magic missile at a dummy and answers the
        /// cast's AoO confirmation Cancel. The goblin beside it has Combat Reflexes (two AoOs a round: with a scroll, its
        /// first goes to the item use's own confirmation, answered Proceed) and -30 on attacks. Today the pipeline spends
        /// the scroll or the charge (ConsumePendingSpellSlot) before the confirmation and the cancel gives back only the
        /// actions and slots, so the scroll is gone (or a charge lost) although no spell was cast. A scroll is a spell
        /// completion item and provokes as casting does (DMG p.213), so its cast confirmation is the rule. A wand is a spell
        /// trigger item whose activation does not provoke (DMG p.213, p.245); only retrieving it provokes (PHB p.141). Today
        /// the wand skips the item-use confirmation and its pipeline cast opens the spellcasting one (ITM-085), so the wand
        /// scenario pins that confirmation as today's behaviour, not as the rule.
        /// </summary>
        private static ScenarioDef ItemAoOCancel(bool scroll)
        {
            string id = scroll ? ScrollId : WandId;
            string name = scroll ? "scroll" : "wand";
            ScenarioBuilder b = Rules("rules/cast-" + name + "-aoo-cancel", "A " + name + " cast cancelled at the AoO prompt (" + (scroll ? "ITM-084" : "ITM-084, ITM-085") + ", pinned)")
                .Covers("ITM-084", "PHB p.141", "DMG p.213", scroll ? "DMG p.237" : "DMG p.245")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Tweak("wizard", c => GiveItem(c, id))
                .Tweak("gob", c => { c.Stats.Feats.Add("Combat Reflexes"); AttackTweak(c, -30); })
                .Initiative("wizard", "gob", "dummy")
                .Force(4, 4, null, -1);
            var steps = new List<Step> { Step.Assert("record", ctx => RecordItem(ctx, id, "before")), Step.UseItem(id, "dummy") };
            if (scroll)
                steps.Add(Step.AnswerAoO(AoOAnswer.Proceed)); // the item use's own confirmation (retrieving and reading)
            steps.Add(Step.AnswerAoO(AoOAnswer.Cancel));       // the cast's confirmation
            steps.Add(Step.Assert("after", ctx => { RecordItem(ctx, id, "after"); FactHpLost(ctx, "missile", "dummy"); return true; }));
            b = b.Turn("wizard", 1, steps.ToArray());
            b = scroll
                ? b.Expect("The scroll's cast confirmation (it provokes as casting does, DMG p.213) was answered Cancel", Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"))
                : b.Covers("ITM-085").ExpectKnown("ITM-085", "Today the wand's activation opens the spellcasting AoO confirmation, answered Cancel, where a spell trigger activation does not provoke and the retrieval that does gets no confirmation (ITM-085)",
                    Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"));
            b = b.Expect("The cancelled spell does nothing", FactIs("missile.lost.dummy", 0))
                .Expect("No prepared slot is spent", FactsEqual("slots.before", "slots.after"));
            b = scroll
                ? b.ExpectKnown("ITM-084", "Today the scroll is used up although the cast was cancelled (ITM-084)", Expect.All(FactIs("count.before", 1), FactIs("count.after", 0)))
                : b.ExpectKnown("ITM-084", "Today the wand loses a charge although the cast was cancelled (ITM-084; under RAW the only confirmation to cancel is the retrieval's, before any charge is spent, ITM-085)", Expect.All(FactIs("charges.before", "50/50"), FactIs("charges.after", "49/49")));
            return b.Build();
        }

        // ── rules/cast-held-touch-charge ────────────────────────────────

        /// <summary>
        /// A held touch charge through the Ui (PHB p.176, Holding the Charge). Round 1: a wizard 5 casts Shocking Grasp
        /// (PHB p.279: melee touch, 1d6 per caster level, at most 5d6) and answers the touch prompt Discharge Later
        /// (BeginHoldTouchCharge, the cast core's HoldCharge resolve since batch 11 step 10): one 1st-level slot and the
        /// standard action are spent and the charge is held. It
        /// then delivers the charge to the dummy beside it with the touch attack forced to a natural 1: the attack misses and
        /// the charge is still held (PHB p.176: a missed touch keeps the charge). Round 2: it delivers again with the attack
        /// forced to 19: 5d6 with every d6 at 6 = 30, and the charge is gone. Only the touch in the round of casting comes
        /// with the spell (PHB p.140-141, Touch Spells in Combat); a later one is a touch attack made instead of a regular
        /// attack (PHB p.141), so it takes the attack action, a standard action (delegated RAW reading 2026-10-14). Today
        /// PerformSpellCast treats every delivery as a free action, so the round-2 delivery leaves every action (SPL-156,
        /// pinned).
        /// </summary>
        private static ScenarioDef HeldTouchCharge()
        {
            return Rules("rules/cast-held-touch-charge", "Ui Shocking Grasp held with Discharge Later: a miss keeps the charge, a hit spends it (PHB p.176)")
                .Covers("PHB p.176", "PHB p.141", "PHB p.279", "SPL-146", "SPL-156")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 6, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SHOCKING_GRASP))
                .Tweak("dummy", Sturdy)
                .Initiative("wizard", "dummy")
                .Force(20, 1, SpellCaster.TouchAttackContext, 1)
                .Force(20, 19, SpellCaster.TouchAttackContext, 1)
                .Force(6, 6, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "dummy"); RecordSlots(ctx, "wizard"); return true; }),
                    Step.CastHeld(SpellNames.SHOCKING_GRASP),
                    Step.Assert("held", ctx => { CharacterController w = ctx.Get("wizard"); Fact(ctx, "held.cast", w.Spellcasting.HasHeldTouchCharge); FactSlotsSpent(ctx, "spent.cast", "wizard"); Fact(ctx, "actions.cast", Actions(w)); return true; }),
                    Step.DeliverHeld("dummy"),
                    Step.Assert("after miss", ctx => { Fact(ctx, "held.miss", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge); FactHpLost(ctx, "miss", "dummy"); return true; }))
                .Turn("wizard", 2,
                    Step.DeliverHeld("dummy"),
                    Step.Assert("after hit", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "held.hit", w.Spellcasting.HasHeldTouchCharge);
                        FactHpLost(ctx, "hit", "dummy");
                        Fact(ctx, "actions.hit", Actions(w));
                        return true;
                    }))
                .Expect("Discharge Later holds the charge and spends one 1st-level slot and the standard action",
                    FactsAre(("held.cast", true), ("spent.cast", "L1+1"), ("actions.cast", "-m-")))
                .Expect("A missed touch keeps the charge and deals nothing (PHB p.176)", FactsAre(("held.miss", true), ("miss.lost.dummy", 0)))
                .Expect("A hit discharges it: 5d6 = 30 (PHB p.279)", FactsAre(("held.hit", false), ("hit.lost.dummy", 30)))
                .Expect("The held charge's cast is in the trace once, its deliveries are not casts", CastEvents("wizard", SpellNames.SHOCKING_GRASP, 1))
                .ExpectKnown("SPL-156", "Today the round-2 delivery costs no action (every action left), where a touch after the casting round takes the attack action (SPL-156)",
                    FactIs("actions.hit", "smf"))
                .Build();
        }

        // ── rules/cast-ui-summon-*, cast-ui-grease, cast-held-aoo-cancel (batch 11 step 10) ──

        /// <summary>The living summoned creatures on the field (GameManager.IsSummonedCreature).</summary>
        private static int Summons()
        {
            GameManager gm = GameManager.Instance;
            return gm == null ? 0 : gm.Combat_GetAllCharacters().Count(c => c != null && c.Stats != null && !c.Stats.IsDead && gm.IsSummonedCreature(c));
        }

        private static Step RecordCaster(string key)
            => Step.Assert("record", ctx => { RecordSlots(ctx, key); Fact(ctx, "summons.before", Summons()); return true; });

        private static Step FactCaster(string tag, string key)
            => Step.Assert(tag, ctx =>
            {
                CharacterController c = ctx.Get(key);
                FactSlotsSpent(ctx, tag + ".spent", key);
                Fact(ctx, tag + ".actions", Actions(c));
                Fact(ctx, tag + ".summons", Summons());
                Fact(ctx, tag + ".held", c.Spellcasting != null && c.Spellcasting.HasHeldTouchCharge);
                return true;
            });

        /// <summary>
        /// A Ui Summon Monster I (PHB p.285-286): a wizard 3 casts it at an empty square 2 squares away and takes the first
        /// creature of list I (one creature, PHB p.286). The cast runs PerformSummonMonsterCast (since batch 11 step 10 a
        /// request builder for the cast core's Summon resolve): one 1st-level slot is spent and one creature appears and joins
        /// the initiative after the caster. Summon Monster's 1-round casting time costs only the standard action today
        /// (SPL-162, pinned).
        /// </summary>
        private static ScenarioDef UiSummonMonster()
        {
            return Rules("rules/cast-ui-summon-monster", "A Ui Summon Monster I: one slot, one creature from list I (PHB p.285-286)")
                .Covers("PHB p.285", "PHB p.286", "SPL-162", "SPL-006")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 15, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SUMMON_MONSTER_1))
                .Initiative("wizard", "gob")
                .Turn("wizard", 1,
                    RecordCaster("wizard"),
                    Step.CastAt(SpellNames.SUMMON_MONSTER_1, 7, 10, "*"),
                    FactCaster("cast", "wizard"))
                .Expect("The summon spends one 1st-level slot and brings one creature (PHB p.286)", FactsAre(("summons.before", 0), ("cast.spent", "L1+1"), ("cast.summons", 1)))
                .Expect("The summon cast is in the trace once (ScenarioHooks.SpellCast)", CastEvents("wizard", SpellNames.SUMMON_MONSTER_1, 1))
                .ExpectKnown("SPL-162", "Today Summon Monster's 1-round casting time costs only the standard action (SPL-162)", FactIs("cast.actions", "-m-"))
                .Build();
        }

        /// <summary>
        /// A threatened Ui Summon Monster I cancelled at the AoO prompt: casting provokes (PHB p.140; the adjacent goblin has
        /// Combat Reflexes, so it threatens while flat-footed, PHB p.92), and backing out before the spell is cast gives back
        /// the action and the slot; no creature appears.
        /// </summary>
        private static ScenarioDef UiSummonAoOCancel()
        {
            return Rules("rules/cast-ui-summon-aoo-cancel", "A threatened Ui Summon Monster I cancelled at the AoO prompt spends nothing (PHB p.140)")
                .Covers("PHB p.140", "SPL-006")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SUMMON_MONSTER_1))
                .Tweak("gob", c => { c.Stats.Feats.Add("Combat Reflexes"); AttackTweak(c, -30); })
                .Initiative("wizard", "gob")
                .Turn("wizard", 1,
                    RecordCaster("wizard"),
                    Step.CastAt(SpellNames.SUMMON_MONSTER_1, 5, 12, "*"),
                    Step.AnswerAoO(AoOAnswer.Cancel),
                    FactCaster("cancel", "wizard"))
                .Expect("The cast's AoO confirmation was answered Cancel", Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"))
                .Expect("Nothing is spent and nothing appears", FactsAre(("cancel.spent", "none"), ("cancel.actions", "smf"), ("cancel.summons", 0)))
                .Build();
        }

        /// <summary>
        /// An entangled Ui wizard 3 loses Summon Monster I to its entangled check (the d20 forced to 1 under SPL-145's
        /// "Dispel Magic check" label: 1 + caster level 3 + INT 3 against DC 16): the slot is spent and no creature appears
        /// (PHB p.308).
        /// </summary>
        private static ScenarioDef UiSummonEntangled()
        {
            return Rules("rules/cast-ui-summon-entangled", "An entangled Ui Summon Monster I lost to the entangled check spends its slot (PHB p.308)")
                .Covers("PHB p.308", "SPL-145", "SPL-006")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 15, 10, Control.Idle)
                .StartCondition("wizard", CombatConditionType.Entangled, 10)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SUMMON_MONSTER_1))
                .Initiative("wizard", "gob")
                .Force(20, 1, "Dispel Magic check", 1)
                .Turn("wizard", 1,
                    RecordCaster("wizard"),
                    Step.CastAt(SpellNames.SUMMON_MONSTER_1, 7, 10, "*"),
                    FactCaster("lost", "wizard"))
                .Expect("The lost spell spends its slot and summons nothing", FactsAre(("lost.spent", "L1+1"), ("lost.summons", 0)))
                .Build();
        }

        /// <summary>
        /// A Ui Summon Swarm (PHB p.289): a wizard 3 places the first swarm type on a square in range; the swarm is
        /// uncontrolled, the caster concentrates on it, and one 2nd-level slot is spent.
        /// </summary>
        private static ScenarioDef UiSummonSwarm()
        {
            return Rules("rules/cast-ui-summon-swarm", "A Ui Summon Swarm: one 2nd-level slot, one swarm, the caster concentrating (PHB p.289)")
                .Covers("PHB p.289", "SPL-006")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 15, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 2, SpellNames.SUMMON_SWARM))
                .Initiative("wizard", "gob")
                .Turn("wizard", 1,
                    RecordCaster("wizard"),
                    Step.CastAt(SpellNames.SUMMON_SWARM, 9, 10, "*"),
                    FactCaster("cast", "wizard"),
                    Step.Assert("concentration", ctx =>
                    {
                        ConcentrationManager conc = ctx.Get("wizard").Concentration;
                        Fact(ctx, "concentrating", conc != null && conc.IsConcentrating);
                        return true;
                    }))
                .Expect("The swarm spends one 2nd-level slot and appears", FactsAre(("cast.spent", "L2+1"), ("cast.summons", 1)))
                .Expect("The caster concentrates on the swarm (PHB p.289)", FactIs("concentrating", true))
                .Build();
        }

        /// <summary>
        /// Grease's three forms through the Ui (PHB p.237): round 1 a 10-ft square anchored on the goblin's square, round 2
        /// the goblin's held object, round 3 its worn armor. Each cast spends one 1st-level slot and the standard action.
        /// </summary>
        private static ScenarioDef UiGrease()
        {
            return Rules("rules/cast-ui-grease", "Ui Grease as an area, a held object and worn armor: one slot each (PHB p.237)")
                .Covers("PHB p.237", "SPL-006", "SPL-152")
                .MaxRounds(3)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 9, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.GREASE, 3))
                .Tweak("gob", Sturdy)
                .Initiative("wizard", "gob")
                .Turn("wizard", 1,
                    RecordCaster("wizard"),
                    Step.CastAt(SpellNames.GREASE, 9, 10, "Grease Area"),
                    FactCaster("area", "wizard"))
                .Turn("wizard", 2,
                    RecordCaster("wizard"),
                    Step.CastChoosing(SpellNames.GREASE, "gob", "Grease Held Object"),
                    FactCaster("object", "wizard"))
                .Turn("wizard", 3,
                    RecordCaster("wizard"),
                    Step.CastChoosing(SpellNames.GREASE, "gob", "Grease Worn Armor"),
                    FactCaster("armor", "wizard"),
                    Step.Assert("armor effect", ctx =>
                    {
                        StatusEffectManager m = ctx.Get("gob").StatusEffectManager;
                        Fact(ctx, "armor.effect", m != null && m.ActiveEffects.Any(e => e != null && e.GreasedArmorGrappleEscapeBonus == 10));
                        return true;
                    }))
                .Expect("Each Grease spends one 1st-level slot and the standard action (PHB p.237, p.174)",
                    FactsAre(("area.spent", "L1+1"), ("area.actions", "-m-"), ("object.spent", "L1+1"), ("object.actions", "-m-"), ("armor.spent", "L1+1"), ("armor.actions", "-m-")))
                .Expect("Greased armor gives +10 on grapple escapes (PHB p.237)", FactIs("armor.effect", true))
                .Expect("Three Grease casts are in the trace", CastEvents("wizard", SpellNames.GREASE, 3))
                .Build();
        }

        /// <summary>
        /// A threatened Ui Shocking Grasp held with Discharge Later and cancelled at the AoO prompt (PHB p.140, p.176; the
        /// adjacent goblin has Combat Reflexes, so it threatens while flat-footed): the hold spends nothing and no charge is
        /// held.
        /// </summary>
        private static ScenarioDef HeldAoOCancel()
        {
            return Rules("rules/cast-held-aoo-cancel", "A threatened Ui touch spell held with Discharge Later and cancelled at the AoO prompt spends nothing (PHB p.140, p.176)")
                .Covers("PHB p.140", "PHB p.176", "SPL-146")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SHOCKING_GRASP))
                .Tweak("gob", c => { c.Stats.Feats.Add("Combat Reflexes"); AttackTweak(c, -30); })
                .Initiative("wizard", "gob")
                .Turn("wizard", 1,
                    RecordCaster("wizard"),
                    Step.CastHeld(SpellNames.SHOCKING_GRASP),
                    Step.AnswerAoO(AoOAnswer.Cancel),
                    FactCaster("cancel", "wizard"))
                .Expect("The hold's AoO confirmation was answered Cancel", Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"))
                .Expect("Nothing is spent and no charge is held", FactsAre(("cancel.spent", "none"), ("cancel.actions", "smf"), ("cancel.held", false)))
                .Build();
        }

        // ── rules/cast-pair-<family> ────────────────────────────────────

        /// <summary>One paired family: the same spell cast by a Ui caster and by a Scripted one on identical setups.</summary>
        private sealed class PairSpec
        {
            public string Family;
            /// <summary>The scenario id when it is not "rules/cast-pair-" + <see cref="Family"/>.</summary>
            public string Id;
            public string SpellId;
            public string Title;
            public string[] Covers;
            public Func<string, CharacterStats> Caster;
            public int SlotLevel;
            /// <summary>The target's bestiary id, or null when the caster targets itself.</summary>
            public string TargetNpc;
            public bool Adjacent;
            public int? TargetHp;
            public string[] Choices;
            /// <summary>Set when today's end states differ: the issue, and the pinned facts "ui" and "scripted".</summary>
            public string KnownIssue;
            public string KnownUi;
            public string KnownScripted;
            /// <summary>With <see cref="KnownIssue"/>, in place of the two pinned strings: the pin's name and check.</summary>
            public string KnownName;
            public Func<TraceView, ExpectResult> KnownCheck;
            /// <summary>Further known-behaviour pins for a defect both sides share (the end states match, so the pair cannot see it).</summary>
            public Func<ScenarioBuilder, ScenarioBuilder> SharedPins;
            /// <summary>A setup both casters get (a condition the spell removes), checked present before each cast.</summary>
            public Action<CharacterController> CasterTweak;
            /// <summary>With <see cref="CasterTweak"/>: the condition each caster must have when its turn starts.</summary>
            public string CasterHasBefore;
            /// <summary>A setup both targets get after the save tweak (spell resistance, for example).</summary>
            public Action<CharacterController> TargetTweak;
        }

        private static IEnumerable<PairSpec> Pairs()
        {
            Func<string, CharacterStats> wizard = n => Wizard(n, 5);
            Func<string, CharacterStats> cleric = n => Cleric(n, 7);
            Func<string, CharacterStats> druid = n => DruidScenarios.Druid(n, 3);
            yield return new PairSpec { Family = "cause-fear", SpellId = SpellNames.CAUSE_FEAR, Caster = wizard, SlotLevel = 1, TargetNpc = "target_dummy",
                Title = "Cause Fear (PHB p.208)", Covers = new[] { "PHB p.208" } };
            yield return new PairSpec { Family = "acid-arrow", SpellId = SpellNames.MELFS_ACID_ARROW, Caster = wizard, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Melf's Acid Arrow (PHB p.253)", Covers = new[] { "PHB p.253" } };
            yield return new PairSpec { Family = "death-knell", SpellId = SpellNames.DEATH_KNELL, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy", Adjacent = true, TargetHp = -3,
                Title = "Death Knell on a dying creature (PHB p.217)", Covers = new[] { "PHB p.217", "SPL-155" },
                // Both sides share the defect, so the pair matches: the spell's generic 10 damage kills the dying target in
                // SpellCaster.Cast before the shared handler runs, which then fizzles on both paths (SPL-155).
                SharedPins = b => b.ExpectKnown("SPL-155",
                    "Today both casts kill the dying target and neither caster gains Death Knell's 1d8 temporary hit points, +2 Str or +1 caster level (SPL-155)",
                    FactsAre(("ui.target.dead", true), ("ui.caster.thp", 0), ("ui.caster.dk", "0"),
                        ("scripted.target.dead", true), ("scripted.caster.thp", 0), ("scripted.caster.dk", "0"))) };
            yield return new PairSpec { Family = "silence", SpellId = SpellNames.SILENCE, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Silence on a creature (PHB p.279)", Covers = new[] { "PHB p.279" } };
            // Sound Burst (PHB p.281, Fortitude partial; batch 11 step 11, SPL-142): the 1d8 (8) sonic damage always lands, the
            // cast's one Fortitude save decides the stun, and a creature that cannot hear is not stunned.
            yield return new PairSpec { Family = "sound-burst", SpellId = SpellNames.SOUND_BURST, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Sound Burst (PHB p.281)", Covers = new[] { "PHB p.281" },
                SharedPins = b => b.Expect("A failed Fortitude save: 8 sonic damage and stunned (PHB p.281)",
                    PairTargets(t => t.StartsWith("hp=142 ", StringComparison.Ordinal) && t.Contains("Stunned") ? null : "expected hp=142 and Stunned", "L2:1/")) };
            yield return new PairSpec { Family = "sound-burst-failed", Id = "rules/sound-burst-failed", SpellId = SpellNames.SOUND_BURST, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Sound Burst stopped by spell resistance (PHB p.281, p.177)", Covers = new[] { "PHB p.281", "PHB p.177", "SPL-142" },
                // SR 100: the caster level check (d20 19 + CL 7 = 26) fails, so the spell has no effect at all.
                TargetTweak = c => c.Stats.SpellResistance = 100,
                SharedPins = b => b.Expect("Spell resistance stops the spell: no damage and no stun on either target, the slot spent (SPL-142)",
                    PairTargets(t => t.StartsWith("hp=150 ", StringComparison.Ordinal) && !t.Contains("Stunned") ? null : "expected hp=150 and no Stunned", "L2:1/")) };
            yield return new PairSpec { Family = "sound-burst-saved", SpellId = SpellNames.SOUND_BURST, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Sound Burst on a creature that saves (PHB p.281)", Covers = new[] { "PHB p.281", "SPL-142" },
                TargetTweak = c => SaveTweak(c, 30),
                SharedPins = b => b.Expect("A successful Fortitude save avoids only the stun: 8 sonic damage, not stunned (PHB p.281; SPL-142)",
                    PairTargets(t => t.StartsWith("hp=142 ", StringComparison.Ordinal) && !t.Contains("Stunned") ? null : "expected hp=142 and no Stunned", "L2:1/")) };
            yield return new PairSpec { Family = "sound-burst-deaf", SpellId = SpellNames.SOUND_BURST, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Sound Burst on a deafened creature (PHB p.281)", Covers = new[] { "PHB p.281", "SPL-142" },
                TargetTweak = c => c.ApplyCondition(CombatConditionType.Deafened, 100, "scenario"),
                SharedPins = b => b.Expect("A creature that cannot hear is not stunned but still takes the 8 sonic damage (PHB p.281; SPL-142)",
                    PairTargets(t => t.StartsWith("hp=142 ", StringComparison.Ordinal) && !t.Contains("Stunned") ? null : "expected hp=142 and no Stunned", "L2:1/")) };
            // Silence (PHB p.279): a defense against sonic attacks, so a Sound Burst has no effect on a creature under it,
            // neither the damage nor the stun (delegated RAW reading 2026-10-14; SPL-142).
            yield return new PairSpec { Family = "sound-burst-silenced", SpellId = SpellNames.SOUND_BURST, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Sound Burst on a creature under a Silence (PHB p.281, p.279)", Covers = new[] { "PHB p.281", "PHB p.279", "SPL-142" },
                TargetTweak = c => { c.Stats.SilenceActive = true; c.Stats.SilenceRoundsRemaining = 100; },
                SharedPins = b => b.Expect("Silence keeps the sonic spell out: no damage and no stun on either target, the slot spent (PHB p.279)",
                    PairTargets(t => t.StartsWith("hp=150 ", StringComparison.Ordinal) && !t.Contains("Stunned") ? null : "expected hp=150 and no Stunned", "L2:1/")) };
            yield return new PairSpec { Family = "searing-light", SpellId = SpellNames.SEARING_LIGHT, Caster = cleric, SlotLevel = 3, TargetNpc = "target_dummy",
                Title = "Searing Light (PHB p.275)", Covers = new[] { "PHB p.275" } };
            yield return new PairSpec { Family = "death-ward", SpellId = SpellNames.DEATH_WARD, Caster = cleric, SlotLevel = 4,
                Title = "Death Ward on the caster (PHB p.217)", Covers = new[] { "PHB p.217" } };
            yield return new PairSpec { Family = "hold-animal", SpellId = SpellNames.HOLD_ANIMAL, Caster = druid, SlotLevel = 2, TargetNpc = "wolf",
                Title = "Hold Animal on a wolf (PHB p.241)", Covers = new[] { "PHB p.241" } };
            // Batch 11 step 6: handlers the NPC path reaches since it runs the shared registry (SPL-054, SPL-091).
            yield return new PairSpec { Family = "ghoul-touch", SpellId = SpellNames.GHOUL_TOUCH, Caster = wizard, SlotLevel = 2, TargetNpc = "goblin", Adjacent = true,
                Title = "Ghoul Touch on a goblin (PHB p.235)", Covers = new[] { "PHB p.235" } };
            yield return new PairSpec { Family = "remove-curse", SpellId = SpellNames.REMOVE_CURSE, Caster = cleric, SlotLevel = 3,
                Title = "Remove Curse on the cursed caster (PHB p.270)", Covers = new[] { "PHB p.270", "SPL-091" },
                CasterTweak = c => c.ApplyCondition(CombatConditionType.BestowCurseGeneralPenalty, 100, "Bestow Curse"),
                CasterHasBefore = nameof(CombatConditionType.BestowCurseGeneralPenalty) };
            yield return new PairSpec { Family = "remove-blindness", SpellId = SpellNames.REMOVE_BLINDNESS_DEAFNESS, Caster = cleric, SlotLevel = 3,
                Title = "Remove Blindness/Deafness on the blinded caster (PHB p.270)", Covers = new[] { "PHB p.270", "SPL-091" },
                CasterTweak = c => c.ApplyCondition(CombatConditionType.Blinded, 100, "Blindness"),
                CasterHasBefore = nameof(CombatConditionType.Blinded) };
            // Step 6 review: a Ghoul Touch that spell resistance stops has no effect (PHB p.235, SR yes). The handler runs
            // with gate None, so it must test the failed result itself: no paralysis, no stench, the slot spent.
            yield return new PairSpec { Family = "ghoul-touch-sr", SpellId = SpellNames.GHOUL_TOUCH, Caster = wizard, SlotLevel = 2, TargetNpc = "goblin", Adjacent = true,
                Title = "Ghoul Touch stopped by spell resistance (PHB p.235)", Covers = new[] { "PHB p.235", "PHB p.177" },
                // SR 100: the caster level check (d20 19 + CL 5 = 24) fails.
                TargetTweak = c => c.Stats.SpellResistance = 100,
                SharedPins = b => b.Expect("Spell resistance stops the spell: no paralysis and no Ghoul Touch effect on either target, the slot spent (PHB p.235, p.177)",
                    PairTargets(t => !t.Contains("Paralyzed") && !t.Contains(SpellNames.GHOUL_TOUCH) ? null : "the target is paralyzed or carries the ghoul touch effect", "L2:1/")) };
            // SPL-124: the handler rolls the damage; SpellCaster.Cast must not deal the generic dice too.
            yield return new PairSpec { Family = "spiritual-weapon", SpellId = SpellNames.SPIRITUAL_WEAPON, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Spiritual Weapon's first strike (PHB p.283)", Covers = new[] { "PHB p.283", "SPL-124" },
                // The weapon's attack (d20 19 + BAB 5 + Wis 3) hits; 1d8 (8) + CL 7 / 3 (2) = 10 force damage, once.
                SharedPins = b => b.Expect("The target takes only the weapon's strike, 10 force damage (PHB p.283; SPL-124)", PairTargets(t => t.StartsWith("hp=140 ", StringComparison.Ordinal) ? null : "expected hp=140", null)) };
            yield return new PairSpec { Family = "produce-flame", SpellId = SpellNames.PRODUCE_FLAME, Caster = cleric, SlotLevel = 2, TargetNpc = "target_dummy",
                Title = "Produce Flame thrown at a creature (PHB p.265)", Covers = new[] { "PHB p.265", "SPL-124" },
                // The ranged touch attack (d20 19) hits; 1d6 (6) + min(CL 7, 5) = 11 fire damage, once.
                SharedPins = b => b.Expect("The target takes only the flame's 11 fire damage (PHB p.265; SPL-124)", PairTargets(t => t.StartsWith("hp=139 ", StringComparison.Ordinal) ? null : "expected hp=139", null)) };
            yield return new PairSpec { Family = "resist-energy", SpellId = SpellNames.RESIST_ENERGY, Caster = wizard, SlotLevel = 2, Choices = new[] { "Cold" },
                Title = "Resist Energy on the caster (PHB p.272)", Covers = new[] { "PHB p.272", "SPL-112" },
                // The caster picks the energy (PHB p.272): the Ui caster picks cold, the NPC path forces fire.
                KnownIssue = "SPL-112",
                KnownName = "Today the NPC path forces fire where the Ui caster chose cold; the end states differ only in that energy (SPL-112)",
                KnownCheck = v =>
                {
                    string ui = FactOf(v, "ui"), ai = FactOf(v, "scripted");
                    if (ui == null || ai == null)
                        return ExpectResult.Inconclusive("missing fact " + (ui == null ? "ui" : "scripted"));
                    if (!ui.Contains("resist=Cold:") || !ai.Contains("resist=Fire:"))
                        return ExpectResult.Fail("ui " + ui + "; scripted " + ai + ", expected cold resistance on the Ui side and fire on the Scripted side");
                    return ui.Replace("resist=Cold:", "resist=Fire:") == ai
                        ? ExpectResult.Pass("ui cold, scripted fire, otherwise equal: " + ai)
                        : ExpectResult.Fail("ui " + ui + "; scripted " + ai + ": they differ beyond the energy");
                } };
            yield return new PairSpec { Family = "magic-weapon", SpellId = SpellNames.MAGIC_WEAPON, Caster = wizard, SlotLevel = 1, Choices = new[] { "*" },
                Title = "Magic Weapon on the caster's weapon (PHB p.251)", Covers = new[] { "PHB p.251", "SPL-113" },
                // The weapon is picked only by the PC prompt: the NPC cast spends its slot and fizzles.
                KnownIssue = "SPL-113", KnownUi = "caster[L0:0/4 L1:1/4 L2:0/3 hp=35 nl=0 thp=0 conds= effects= silence=0 dk=0 resist= items=Quarterstaff:magic_weapon:+1:50] target[hp=35 nl=0 thp=0 conds= effects= silence=0 dk=0 resist= items=Quarterstaff:magic_weapon:+1:50]", KnownScripted = "caster[L0:0/4 L1:1/4 L2:0/3 hp=35 nl=0 thp=0 conds= effects= silence=0 dk=0 resist= items=] target[hp=35 nl=0 thp=0 conds= effects= silence=0 dk=0 resist= items=]" };
        }

        /// <summary>
        /// Paired casts of one spell (one per handler guard group of the PC cast chain, SPL-054) on identical setups: a Ui
        /// caster (the PC pipeline) and a Scripted one (the NPC cast executor), Stats-built with the same class, level and
        /// scores, each with the same target 15 ft away (5 ft for a touch spell; the caster itself for a personal-range
        /// buff), a far dummy keeping the fight going. Every d20 is 19 (touch attacks hit), every d4, d6 and d8 its maximum,
        /// and the target fails its saves (-30). After both casts the Scripted caster's Assert step records each side's end
        /// state: the caster's slots, and the target's hit points, conditions, spell effects with rounds left, Silence flag,
        /// Resist Energy entries and weapon spell effects. RAW the two must match (PC_NPC_PARITY, owner 2026-10-07); a
        /// family where they differ today is pinned as Known with the issue that explains it (SPL-112: caster choices;
        /// SPL-113: the Magic Weapon pick). The handler families (Silence, Sound Burst, Hold Animal, and Ghoul Touch,
        /// Remove Curse and Remove Blindness/Deafness added with it) pass since batch 11 step 6 runs the shared handler
        /// registry on the NPC path (SPL-054).
        /// </summary>
        private static ScenarioDef PairScenario(PairSpec p)
        {
            bool self = p.TargetNpc == null;
            int dx = p.Adjacent ? 1 : 3;
            string uiTarget = self ? "ui" : "uiTarget";
            string aiTarget = self ? "scripted" : "scriptedTarget";
            ScenarioBuilder b = Rules(p.Id ?? "rules/cast-pair-" + p.Family, "Ui and Scripted casts of " + p.Title + " end in the same state (PC_NPC_PARITY)")
                .Covers(p.Covers).Covers("SPL-054")
                .MaxRounds(1)
                .Pc("ui", ActorSource.Stats(() => p.Caster("Pair Ui")), 3, 4, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => p.Caster("Pair Scripted")), 3, 14, Control.Scripted)
                .Tweak("ui", c => { Prepare(c, p.SlotLevel, p.SpellId); p.CasterTweak?.Invoke(c); })
                .Tweak("scripted", c => { Prepare(c, p.SlotLevel, p.SpellId); p.CasterTweak?.Invoke(c); });
            var order = new List<string> { "ui", "scripted" };
            if (!self)
            {
                Action<CharacterController> tweak = p.TargetHp.HasValue ? (Action<CharacterController>)(c => SaveTweak(c, -30)) : Fails;
                if (p.TargetTweak != null)
                {
                    Action<CharacterController> saveTweak = tweak;
                    tweak = c => { saveTweak(c); p.TargetTweak(c); };
                }
                b = b.Npc("uiTarget", p.TargetNpc, 3 + dx, 4, Control.Idle)
                     .Npc("scriptedTarget", p.TargetNpc, 3 + dx, 14, Control.Idle)
                     .Tweak("uiTarget", tweak).Tweak("scriptedTarget", tweak);
                if (p.TargetHp.HasValue)
                    b = b.Hp("uiTarget", p.TargetHp.Value).Hp("scriptedTarget", p.TargetHp.Value);
                order.Add("uiTarget");
                order.Add("scriptedTarget");
            }
            b = b.Npc("anchor", "target_dummy", 18, 9, Control.Idle);
            order.Add("anchor");
            // A family with a caster setup records each caster's conditions just before its cast (only those families,
            // so the other pairs keep their step 3 traces).
            Step BeforeCast(string side)
                => Step.Assert("record the " + side + " caster before its cast", ctx => { Fact(ctx, side + ".before", string.Join(",", ScenarioTrace.ConditionNames(ctx.Get(side)))); return true; });
            var uiSteps = new List<Step>();
            var aiSteps = new List<Step>();
            if (p.CasterHasBefore != null)
            {
                uiSteps.Add(BeforeCast("ui"));
                aiSteps.Add(BeforeCast("scripted"));
            }
            uiSteps.Add(p.Choices != null ? Step.CastChoosing(p.SpellId, uiTarget, p.Choices) : Step.Cast(p.SpellId, uiTarget));
            aiSteps.Add(Step.Cast(p.SpellId, aiTarget));
            aiSteps.Add(Step.Assert("record both end states", ctx =>
                    {
                        Fact(ctx, "ui", PairState(ctx.Get("ui"), ctx.Get(uiTarget)));
                        Fact(ctx, "scripted", PairState(ctx.Get("scripted"), ctx.Get(aiTarget)));
                        foreach (string side in new[] { "ui", "scripted" })
                        {
                            CharacterController caster = ctx.Get(side), target = ctx.Get(side == "ui" ? uiTarget : aiTarget);
                            Fact(ctx, side + ".caster.thp", caster.Stats.TempHP);
                            Fact(ctx, side + ".caster.dk", DeathKnellState(caster));
                            Fact(ctx, side + ".target.dead", target.IsDead);
                        }
                        return true;
                    }));
            b = b.Initiative(order.ToArray())
                .Force(20, 19, null, -1)
                .Force(4, 4, null, -1)
                .Force(6, 6, null, -1)
                .Force(8, 8, null, -1)
                .Turn("ui", 1, uiSteps.ToArray())
                .Turn("scripted", 1, aiSteps.ToArray())
                // Both casts must have run: equal end states after two refused casts would prove nothing.
                .Expect("The Ui cast ran", Expect.StepStatus("ui", 1, "Cast", 0, "done"))
                .Expect("The Scripted cast ran", Expect.StepStatus("scripted", 1, "Cast", 0, "done"))
                .Expect("One cast event per caster (ScenarioHooks.SpellCast)", Expect.All(CastEvents("ui", p.SpellId, 1), CastEvents("scripted", p.SpellId, 1)));
            if (p.CasterHasBefore != null)
                b = b.Expect("Each caster has " + p.CasterHasBefore + " before its cast and not after it", v =>
                {
                    foreach (string side in new[] { "ui", "scripted" })
                    {
                        string before = FactOf(v, side + ".before"), after = FactOf(v, side);
                        if (before == null || after == null)
                            return ExpectResult.Inconclusive("missing fact " + side + (before == null ? ".before" : ""));
                        if (!before.Split(',').Contains(p.CasterHasBefore))
                            return ExpectResult.Fail(side + " caster before its cast: " + before + ", expected " + p.CasterHasBefore);
                        if (after.Contains(p.CasterHasBefore))
                            return ExpectResult.Fail(side + " after its cast: " + after + ", expected " + p.CasterHasBefore + " removed");
                    }
                    return ExpectResult.Pass("both casters had " + p.CasterHasBefore + " and lost it");
                });
            if (p.KnownIssue == null)
                b = b.Expect("The Ui and Scripted casts end in the same state", FactsEqual("ui", "scripted"));
            else if (p.KnownCheck != null)
                b = b.ExpectKnown(p.KnownIssue, p.KnownName, p.KnownCheck);
            else
                b = b.ExpectKnown(p.KnownIssue, "Today the end states differ (" + p.KnownIssue + "): ui " + p.KnownUi + "; scripted " + p.KnownScripted,
                    FactsAre(("ui", p.KnownUi), ("scripted", p.KnownScripted)));
            if (p.SharedPins != null)
                b = p.SharedPins(b);
            return b.Build();
        }

        /// <summary>
        /// Checks the target part of both pair facts ("ui", "scripted") with <paramref name="targetProblem"/> (null when the
        /// target's end state is right, else what is wrong), and, when <paramref name="casterHas"/> is set, that the caster
        /// part contains it (a spent slot such as "L2:1/").
        /// </summary>
        private static Func<TraceView, ExpectResult> PairTargets(Func<string, string> targetProblem, string casterHas)
            => v =>
            {
                var seen = new List<string>();
                foreach (string side in new[] { "ui", "scripted" })
                {
                    string fact = FactOf(v, side);
                    if (fact == null)
                        return ExpectResult.Inconclusive("missing fact " + side);
                    int at = fact.IndexOf("] target[", StringComparison.Ordinal);
                    if (at < 0)
                        return ExpectResult.Fail(side + ": no target part in " + fact);
                    string caster = fact.Substring(0, at);
                    string target = fact.Substring(at + "] target[".Length).TrimEnd(']');
                    string problem = targetProblem(target);
                    if (problem != null)
                        return ExpectResult.Fail(side + " target " + target + ": " + problem);
                    if (casterHas != null && !caster.Contains(casterHas))
                        return ExpectResult.Fail(side + " caster " + caster + ": expected " + casterHas);
                    seen.Add(side + " target " + target);
                }
                return ExpectResult.Pass(string.Join("; ", seen));
            };

        /// <summary>The caster's slots and state and the target's state, as one comparable string (no names, no positions).</summary>
        private static string PairState(CharacterController caster, CharacterController target)
            => "caster[" + Slots(caster) + " " + EndState(caster) + "] target[" + EndState(target) + "]";

        /// <summary>Death Knell's caster benefit (PHB p.217): "0", or "+&lt;Str&gt;str:+&lt;CL&gt;cl:&lt;rounds&gt;".</summary>
        private static string DeathKnellState(CharacterController c)
            => c.Stats.DeathKnellActive
                ? "+" + c.Stats.DeathKnellStrBonus + "str:+" + c.Stats.DeathKnellCLBonus + "cl:" + c.Stats.DeathKnellRoundsRemaining
                : "0";

        private static string EndState(CharacterController c)
        {
            var sb = new StringBuilder();
            sb.Append("hp=").Append(c.Stats.CurrentHP).Append(" nl=").Append(c.Stats.NonlethalDamage).Append(" thp=").Append(c.Stats.TempHP);
            sb.Append(" conds=").Append(string.Join(",", ScenarioTrace.ConditionNames(c)));
            List<string> effects = c.StatusEffectManager != null
                ? c.StatusEffectManager.ActiveEffects.Where(e => e != null).Select(e => e.SpellId + ":" + e.RemainingRounds).OrderBy(x => x, StringComparer.Ordinal).ToList()
                : new List<string>();
            sb.Append(" effects=").Append(string.Join(",", effects));
            sb.Append(" silence=").Append(c.Stats.SilenceActive ? c.Stats.SilenceRoundsRemaining : 0);
            sb.Append(" dk=").Append(DeathKnellState(c));
            List<string> resist = c.Stats.ActiveResistEnergyEffects != null
                ? c.Stats.ActiveResistEnergyEffects.Where(r => r != null).Select(r => r.EnergyType + ":" + r.ResistanceAmount + ":" + r.DurationRemainingRounds).ToList()
                : new List<string>();
            sb.Append(" resist=").Append(string.Join(",", resist));
            var weaponEffects = new List<string>();
            global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
            if (inv != null)
            {
                var items = new List<ItemData> { inv.RightHandSlot, inv.LeftHandSlot, inv.HandsSlot };
                if (inv.GeneralSlots != null)
                    items.AddRange(inv.GeneralSlots);
                foreach (ItemData item in items)
                    if (item != null && item.ActiveSpellEffects != null)
                        foreach (ItemSpellEffect e in item.ActiveSpellEffects)
                            if (e != null)
                                weaponEffects.Add(item.Name + ":" + e.SpellId + ":+" + e.EnhancementBonusAttack + ":" + e.RemainingRounds);
            }
            sb.Append(" items=").Append(string.Join(",", weaponEffects));
            return sb.ToString();
        }

        // ── rules/cast-ai-* (batch 11 step 6) ───────────────────────────

        /// <summary>Spends every prepared slot of <paramref name="c"/> except one holding <paramref name="keepSpellId"/>.</summary>
        private static void SpendAllBut(CharacterController c, string keepSpellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spell slots");
            bool kept = false;
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (slot == null || slot.IsUsed)
                    continue;
                if (!kept && slot.PreparedSpell != null && slot.PreparedSpell.SpellId == keepSpellId)
                {
                    kept = true;
                    continue;
                }
                slot.IsUsed = true;
            }
            if (!kept)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no unspent slot holding " + keepSpellId);
            sc.SyncPreparedSpellsFromSlots();
        }

        private static int FinalHp(TraceView v, string key)
        {
            JsonObj snap = v.Final(key);
            object hp = snap != null ? snap.Get("hp") : null;
            return hp != null ? Convert.ToInt32(hp, System.Globalization.CultureInfo.InvariantCulture) : int.MinValue;
        }

        /// <summary>
        /// An AI-chosen cast of a handler the NPC path gained in step 6 (SPL-054): an AI-run cleric 7 (the NPC turn path)
        /// whose only unspent slot holds Spiritual Weapon, a target dummy 30 ft away (-30 on saves, 100 more hit points)
        /// and one round. The cleric casts it on its own choice, and the dummy takes only the weapon's first strike
        /// (d20 19 + BAB 5 + Wis 3 hits; 1d8 = 8 + CL 7 / 3 = 2: 10 force damage, PHB p.283), not the generic dice too
        /// (SPL-124).
        /// </summary>
        private static ScenarioDef AiSpiritualWeapon()
        {
            return Rules("rules/cast-ai-spiritual-weapon", "An AI-chosen Spiritual Weapon runs the shared handler and strikes once (PHB p.283)")
                .Covers("SPL-054", "SPL-124", "PHB p.283", "batch 11 step 6")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => Cleric("AI Cleric", 7)), 3, 10, Control.Ai)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Tweak("cleric", c => { Prepare(c, 2, SpellNames.SPIRITUAL_WEAPON); SpendAllBut(c, SpellNames.SPIRITUAL_WEAPON); })
                .Tweak("dummy", Fails)
                .Initiative("cleric", "dummy")
                .Force(20, 19, null, -1)
                .Force(8, 8, null, -1)
                .Expect("The AI cleric casts Spiritual Weapon once", CastEvents("cleric", SpellNames.SPIRITUAL_WEAPON, 1))
                .Expect("The dummy takes only the weapon's strike: 150 -> 140 (PHB p.283; SPL-124)", v =>
                {
                    int hp = FinalHp(v, "dummy");
                    if (hp == int.MinValue)
                        return ExpectResult.Inconclusive("no final hit points for the dummy");
                    return hp == 140 ? ExpectResult.Pass("dummy hp 140") : ExpectResult.Fail("dummy hp " + hp + ", expected 140");
                })
                .Build();
        }

        /// <summary>
        /// The selection exclusion of step 6 (SPL-091): an AI-run wizard 7 whose only unspent slot holds Dimension Door, a
        /// goblin beside it (-30 on attacks, 100 more hit points), two rounds. Dimension Door's destination is the caster's
        /// choice (PHB p.221), which the AI cannot give yet, so AISpellcastingStrategist.NeedsCasterChoiceAiCannotGive keeps
        /// it out of AIService.SelectSpell and SelectLowerLevelAlternative: the wizard never casts it.
        /// </summary>
        private static ScenarioDef AiSkipsDimensionDoor()
        {
            return Rules("rules/cast-ai-skips-dimension-door", "The AI does not select Dimension Door, whose destination it cannot choose yet (SPL-091)")
                .Covers("SPL-091", "PHB p.221", "batch 11 step 6")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("AI Wizard", 7)), 3, 10, Control.Ai)
                .Npc("goblin", "goblin", 4, 10, Control.Idle)
                .Tweak("wizard", c => { AddWizardSlot(c, 4, SpellNames.DIMENSION_DOOR); SpendAllBut(c, SpellNames.DIMENSION_DOOR); })
                .Tweak("goblin", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("goblin", "wizard")
                .Expect("The AI wizard never casts Dimension Door", Expect.Count("cast", e => e.Str("by") == "wizard" && e.Str("spell") == SpellNames.DIMENSION_DOOR, 0, 0))
                .Build();
        }

        // ── rules/cast-globe-damage (SPL-141) ───────────────────────────

        /// <summary>
        /// SPL-141 (PHB p.236: a globe of invulnerability keeps spells of 4th level and lower from affecting any creature
        /// inside it). The warded dummy raises a Globe of Invulnerability through the shared effect step on its round-1
        /// turn, made to last 10 rounds (the dummy's caster level would give 1; before batch 11 step 11 the globe was gone
        /// by round 2, so this scenario's Fireball never met it). The Ui wizard 5 then casts Magic Missile at it (round 1; every d4 is 4, three missiles would be 15) and
        /// Fireball centred on it (round 2; every d6 is 6, it fails its save: it would be 30). The cast core's Globe stage
        /// tests the globe before SpellCaster.Cast on the single-target path and before the area handlers (batch 11 step
        /// 11), so neither deals damage.
        /// </summary>
        private static ScenarioDef GlobeDamage()
        {
            return Rules("rules/cast-globe-damage", "A Globe of Invulnerability blocks a spell before its damage")
                .Covers("SPL-141", "PHB p.236")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 2, 10, Control.Ui)
                .Npc("warded", "target_dummy", 12, 10, Control.Scripted)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.MAGIC_MISSILE); AddWizardSlot(c, 3, SpellNames.FIREBALL); })
                .Tweak("warded", Fails)
                .Initiative("warded", "wizard")
                .Force(4, 4, null, -1)
                .Force(6, 6, null, -1)
                .Turn("warded", 1, Step.Assert("raise the globe", ctx =>
                {
                    CharacterController w = ctx.Get("warded");
                    ActiveSpellEffect globe = ctx.Gm.Harness_ApplySpellBuff(w, w, SpellDatabase.GetSpell(SpellNames.GLOBE_OF_INVULNERABILITY).Clone());
                    // The dummy's caster level gives a 1-round globe; it must still be up for round 2's Fireball (the
                    // emanation ends with the tracked effect).
                    if (globe != null)
                        globe.RemainingRounds = 10;
                    foreach (LesserGlobeOfInvulnerabilityAreaEffect g in AreaEffectManager.Instance.GetEffectsOfType<LesserGlobeOfInvulnerabilityAreaEffect>())
                        g.RoundsRemaining = 10;
                    Fact(ctx, "globe", w.StatusEffectManager.HasEffect(SpellNames.GLOBE_OF_INVULNERABILITY));
                    return true;
                }))
                .Turn("warded", 2, Step.Pass())
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "warded"); Fact(ctx, "blocks.missile", LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE), ctx.Get("warded"))); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "warded"),
                    Step.Assert("after missile", ctx => { FactHpLost(ctx, "missile", "warded"); RecordHp(ctx, "warded"); return true; }))
                .Turn("wizard", 2,
                    Step.Assert("globe still up", ctx => { Fact(ctx, "blocks.fireball", LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(SpellDatabase.GetSpell(SpellNames.FIREBALL), ctx.Get("warded"))); return true; }),
                    Step.Cast(SpellNames.FIREBALL, "warded"),
                    Step.Assert("after fireball", ctx => { FactHpLost(ctx, "fireball", "warded"); return true; }))
                .Expect("The globe is up and blocks a 1st-level and a 3rd-level spell against the warded dummy (PHB p.236)", FactsAre(("globe", true), ("blocks.missile", true), ("blocks.fireball", true)))
                .Expect("Both Ui casts ran", Expect.All(Expect.StepStatus("wizard", 1, "Cast", 0, "done"), Expect.StepStatus("wizard", 2, "Cast", 0, "done")))
                .Expect("The globe blocks the magic missiles and Fireball before any damage: the warded dummy loses nothing (PHB p.236; SPL-141)",
                    FactsAre(("missile.lost.warded", 0), ("fireball.lost.warded", 0)))
                .Build();
        }

        // ── rules/area-globe (SPL-141) ──────────────────────────────────

        /// <summary>
        /// SPL-141 (PHB p.236: the area of a spell of 3rd level or lower does not include a lesser globe of invulnerability,
        /// and the spell fails to affect any creature inside it; Fireball, PHB p.231, is 3rd level). A Scripted wizard 7
        /// raises Lesser Globe of Invulnerability on itself in round 1 through the spell's registered handler
        /// (Harness_RunSingleTargetHandler: the NPC path cannot spend an added 4th-level slot, SPL-041; a 10-ft-radius
        /// emanation, 2 squares, 7 rounds). In round 2 the Ui wizard 5 casts Fireball centred on it (every d6 is 6: 30; every target
        /// fails its save). Two dummies 15 ft from the warded wizard are in the burst (20-ft radius) and outside the globe.
        /// The cast core's area Globe stage drops the warded wizard from the targets before Fireball's handler runs (batch
        /// 11 step 11), so it loses nothing and the dummies lose 30 each.
        /// </summary>
        private static ScenarioDef AreaGlobe()
        {
            return Rules("rules/area-globe", "A Fireball spares a creature inside a lesser globe of invulnerability and burns the others")
                .Covers("SPL-141", "PHB p.236", "PHB p.231")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Globe Fireball Wizard", 5)), 2, 10, Control.Ui)
                .Pc("warded", ActorSource.Stats(() => Wizard("Globe Warded Wizard", 7)), 12, 10, Control.Scripted)
                .Npc("north", "target_dummy", 12, 13, Control.Idle)
                .Npc("east", "target_dummy", 15, 10, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 3, SpellNames.FIREBALL))
                .Tweak("warded", Fails)
                .Tweak("north", Fails)
                .Tweak("east", Fails)
                .Initiative("warded", "wizard", "north", "east")
                .Force(6, 6, null, -1)
                .Turn("warded", 1, Step.Assert("raise the lesser globe", ctx =>
                {
                    CharacterController w = ctx.Get("warded");
                    Fact(ctx, "globe.raised", ctx.Gm.Harness_RunSingleTargetHandler(w, w, SpellDatabase.GetSpell(SpellNames.LESSER_GLOBE_OF_INVULNERABILITY).Clone()));
                    return true;
                }))
                .Turn("warded", 2, Step.Pass())
                .Turn("wizard", 1, Step.Pass())
                .Turn("wizard", 2,
                    Step.Assert("record", ctx =>
                    {
                        RecordHp(ctx, "warded", "north", "east");
                        SpellData fireball = SpellDatabase.GetSpell(SpellNames.FIREBALL);
                        Fact(ctx, "blocks.warded", LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(fireball, ctx.Get("warded")));
                        Fact(ctx, "blocks.north", LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(fireball, ctx.Get("north")));
                        Fact(ctx, "blocks.east", LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(fireball, ctx.Get("east")));
                        return true;
                    }),
                    Step.Cast(SpellNames.FIREBALL, "warded"),
                    Step.Assert("after fireball", ctx => { FactHpLost(ctx, "fireball", "warded", "north", "east"); return true; }))
                .Expect("The Ui Fireball ran", Expect.StepStatus("wizard", 2, "Cast", 0, "done"))
                .Expect("The lesser globe covers the warded wizard and neither dummy (PHB p.236: 10-ft radius)",
                    FactsAre(("globe.raised", true), ("blocks.warded", true), ("blocks.north", false), ("blocks.east", false)))
                .Expect("Fireball does not affect the creature inside the globe; the others take 30 fire damage (PHB p.236, p.231; SPL-141)",
                    FactsAre(("fireball.lost.warded", 0), ("fireball.lost.north", 30), ("fireball.lost.east", 30)))
                .Build();
        }

        /// <summary>
        /// Raises a Lesser Globe of Invulnerability (PHB p.236: 10-ft-radius emanation, spells of 3rd level and lower) on
        /// <paramref name="key"/> through the spell's registered handler (Harness_RunSingleTargetHandler; an NPC or a low
        /// caster cannot spend a 4th-level slot, SPL-041) and makes every globe last 10 rounds (a 1-round globe from a
        /// low caster level would be gone before the next round's cast). The globe registers and sets its squares in its
        /// Start, so it is found as a scene object here, and the cast it should stop comes in a later turn.
        /// </summary>
        private static Step RaiseLesserGlobe(string key)
            => Step.Assert("raise the lesser globe", ctx =>
            {
                CharacterController w = ctx.Get(key);
                Fact(ctx, "globe.raised", ctx.Gm.Harness_RunSingleTargetHandler(w, w, SpellDatabase.GetSpell(SpellNames.LESSER_GLOBE_OF_INVULNERABILITY).Clone()));
                foreach (LesserGlobeOfInvulnerabilityAreaEffect g in UnityEngine.Object.FindObjectsByType<LesserGlobeOfInvulnerabilityAreaEffect>(UnityEngine.FindObjectsInactive.Exclude))
                    if (g != null && g.Caster == w)
                        g.RoundsRemaining = 10;
                return true;
            });

        private static bool GlobeBlocks(ScenarioContext ctx, string spellId, string key)
            => LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(SpellDatabase.GetSpell(spellId), ctx.Get(key));

        // ── rules/cast-globe-held-touch (SPL-141) ───────────────────────

        /// <summary>
        /// SPL-141, delegated RAW reading 2026-10-14 (PHB p.236: spells of the globe's levels fail to affect any target
        /// inside it; PHB p.141-142: touching anything while holding a charge discharges the spell, a missed touch keeps
        /// it). The warded dummy raises a lesser globe in round 1. The Ui wizard 5 holds Shocking Grasp (1st level) and
        /// delivers it at the dummy: the round-1 touch misses (d20 1), so the charge is kept; the round-2 touch hits (d20
        /// 19), so the charge is discharged with no effect (every d6 would be 6: 30). The dummy loses nothing.
        /// </summary>
        private static ScenarioDef GlobeHeldTouch()
        {
            return Rules("rules/cast-globe-held-touch", "A held touch at a creature inside a lesser globe: a miss keeps the charge, a hit discharges it with no effect")
                .Covers("SPL-141", "PHB p.236", "PHB p.141", "PHB p.142")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Globe Touch Wizard", 5)), 5, 10, Control.Ui)
                .Npc("warded", "target_dummy", 6, 10, Control.Scripted)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SHOCKING_GRASP))
                .Tweak("warded", Sturdy)
                .Initiative("warded", "wizard")
                .Force(20, 1, SpellCaster.TouchAttackContext, 1)
                .Force(20, 19, SpellCaster.TouchAttackContext, 1)
                .Force(6, 6, null, -1)
                .Turn("warded", 1, RaiseLesserGlobe("warded"))
                .Turn("warded", 2, Step.Pass())
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "warded"); Fact(ctx, "blocks.grasp", GlobeBlocks(ctx, SpellNames.SHOCKING_GRASP, "warded")); return true; }),
                    Step.CastHeld(SpellNames.SHOCKING_GRASP),
                    Step.DeliverHeld("warded"),
                    Step.Assert("after miss", ctx => { Fact(ctx, "held.miss", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge); FactHpLost(ctx, "miss", "warded"); return true; }))
                .Turn("wizard", 2,
                    Step.DeliverHeld("warded"),
                    Step.Assert("after hit", ctx => { Fact(ctx, "held.hit", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge); FactHpLost(ctx, "hit", "warded"); return true; }))
                .Expect("The lesser globe is up around the dummy and excludes Shocking Grasp (PHB p.236)", FactsAre(("globe.raised", true), ("blocks.grasp", true)))
                .Expect("A missed touch keeps the charge and deals nothing (PHB p.142)", FactsAre(("held.miss", true), ("miss.lost.warded", 0)))
                .Expect("A touch that lands discharges the spell, which has no effect inside the globe (PHB p.141, p.236)", FactsAre(("held.hit", false), ("hit.lost.warded", 0)))
                .Build();
        }

        // ── rules/cast-globe-dispel (SPL-141) ───────────────────────────

        /// <summary>
        /// SPL-141, delegated RAW reading 2026-10-14 (PHB p.236: a targeted dispel magic can bring the globe down; the globe
        /// still keeps the spell from the creature inside). The warded dummy has Shield on itself and then raises a lesser
        /// globe (round 1). In round 2 the Ui wizard 5 casts Dispel Magic (3rd level, PHB p.223) at the dummy; its one dispel
        /// check (d20 20 + 5 = 25 against DC 11 + the globe's caster level) is made against the globe alone. The globe ends;
        /// the Shield on the dummy is untouched.
        /// </summary>
        private static ScenarioDef GlobeDispel()
        {
            return Rules("rules/cast-globe-dispel", "A targeted Dispel Magic at a creature inside a lesser globe brings the globe down and nothing else")
                .Covers("SPL-141", "PHB p.236", "PHB p.223")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Globe Dispel Wizard", 5)), 2, 10, Control.Ui)
                .Npc("warded", "target_dummy", 8, 10, Control.Scripted)
                .Tweak("wizard", c => AddWizardSlot(c, 3, SpellNames.DISPEL_MAGIC))
                .Tweak("warded", Sturdy)
                .Initiative("warded", "wizard")
                .Force(20, 20, DispelMagicService.DispelCheckContext, -1)
                .Turn("warded", 1,
                    Step.Assert("shield", ctx =>
                    {
                        CharacterController w = ctx.Get("warded");
                        ActiveSpellEffect shield = ctx.Gm.Harness_ApplySpellBuff(w, w, SpellDatabase.GetSpell(SpellNames.SHIELD).Clone());
                        if (shield != null)
                            shield.RemainingRounds = 10;
                        return true;
                    }),
                    RaiseLesserGlobe("warded"))
                .Turn("warded", 2, Step.Pass())
                .Turn("wizard", 1, Step.Pass())
                .Turn("wizard", 2,
                    Step.Assert("before", ctx =>
                    {
                        Fact(ctx, "blocks.before", GlobeBlocks(ctx, SpellNames.DISPEL_MAGIC, "warded"));
                        Fact(ctx, "shield.before", ctx.Get("warded").StatusEffectManager.HasEffect(SpellNames.SHIELD));
                        return true;
                    }),
                    Step.Cast(SpellNames.DISPEL_MAGIC, "warded"),
                    Step.Assert("after", ctx =>
                    {
                        Fact(ctx, "blocks.after", GlobeBlocks(ctx, SpellNames.DISPEL_MAGIC, "warded"));
                        Fact(ctx, "shield.after", ctx.Get("warded").StatusEffectManager.HasEffect(SpellNames.SHIELD));
                        return true;
                    }))
                .Expect("The Ui Dispel Magic ran", Expect.StepStatus("wizard", 2, "Cast", 0, "done"))
                .Expect("Before the dispel the globe covers the dummy, which also has Shield", FactsAre(("globe.raised", true), ("blocks.before", true), ("shield.before", true)))
                .Expect("The targeted dispel ends the globe and leaves the Shield on the creature inside (PHB p.236, p.223)", FactsAre(("blocks.after", false), ("shield.after", true)))
                .Build();
        }

        // ── rules/cast-globe-grease-armor (SPL-141) ─────────────────────

        /// <summary>
        /// SPL-141 (PHB p.236: the spell fails to affect any target inside the globe; Grease, PHB p.237, is 1st level). The
        /// goblin raises a lesser globe in round 1; in round 2 the Ui wizard 3 casts Grease on the goblin's worn armor. The
        /// armor is inside the globe, so the cast ends at the Globe stage with no effect, its slot and action spent.
        /// </summary>
        private static ScenarioDef GlobeGreaseArmor()
        {
            return Rules("rules/cast-globe-grease-armor", "Grease on the armor of a creature inside a lesser globe has no effect")
                .Covers("SPL-141", "PHB p.236", "PHB p.237")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Globe Grease Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 9, 10, Control.Scripted)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.GREASE))
                .Tweak("gob", c => { Sturdy(c); AttackTweak(c, -30); })
                .Initiative("gob", "wizard")
                .Turn("gob", 1, RaiseLesserGlobe("gob"))
                .Turn("gob", 2, Step.Pass())
                .Turn("wizard", 1, Step.Pass())
                .Turn("wizard", 2,
                    RecordCaster("wizard"),
                    Step.Assert("covered", ctx => { Fact(ctx, "blocks.grease", GlobeBlocks(ctx, SpellNames.GREASE, "gob")); return true; }),
                    Step.CastChoosing(SpellNames.GREASE, "gob", "Grease Worn Armor"),
                    FactCaster("armor", "wizard"),
                    Step.Assert("armor effect", ctx =>
                    {
                        StatusEffectManager m = ctx.Get("gob").StatusEffectManager;
                        Fact(ctx, "armor.effect", m != null && m.ActiveEffects.Any(e => e != null && e.GreasedArmorGrappleEscapeBonus == 10));
                        return true;
                    }))
                .Expect("The lesser globe is up around the goblin and excludes Grease", FactsAre(("globe.raised", true), ("blocks.grease", true)))
                .Expect("The cast spends one 1st-level slot and the standard action (PHB p.174)", FactsAre(("armor.spent", "L1+1"), ("armor.actions", "-m-")))
                .Expect("The armor inside the globe is not greased (PHB p.236)", FactIs("armor.effect", false))
                .Build();
        }

        // ── rules/area-globe-invisible (SPL-141) ────────────────────────

        /// <summary>
        /// SPL-141 (PHB p.236: the area of a spell of the globe's levels does not include the globe; PHB p.245: an
        /// invisible caster's spell is an attack when its area or effect includes a foe). The warded dummy raises a lesser
        /// globe in round 1; the Ui wizard 5 is made invisible in round 1 and in round 2 casts Fireball centred on the dummy,
        /// the only creature in the burst. The area Globe stage drops the dummy before the invisibility break, so the burst
        /// includes no foe: the wizard stays invisible and the dummy loses nothing.
        /// </summary>
        private static ScenarioDef AreaGlobeInvisible()
        {
            return Rules("rules/area-globe-invisible", "An invisible caster whose Fireball reaches only a foe inside a lesser globe stays invisible")
                .Covers("SPL-141", "PHB p.236", "PHB p.245")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Globe Invisible Wizard", 5)), 2, 10, Control.Ui)
                .Npc("warded", "target_dummy", 12, 10, Control.Scripted)
                .Tweak("wizard", c => AddWizardSlot(c, 3, SpellNames.FIREBALL))
                .Tweak("warded", Fails)
                .Initiative("warded", "wizard")
                .Force(6, 6, null, -1)
                .Turn("warded", 1, RaiseLesserGlobe("warded"))
                .Turn("warded", 2, Step.Pass())
                .Turn("wizard", 1, Step.Assert("become invisible", ctx =>
                {
                    CharacterController w = ctx.Get("wizard");
                    ActiveSpellEffect effect = w.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.INVISIBILITY).Clone(), w.Stats.CharacterName, 5);
                    if (effect != null)
                        w.ApplyInvisibilityEffect(effect.RemainingRounds, w, isMoving: false);
                    Fact(ctx, "invisible.before", w.HasActiveInvisibilityEffect);
                    return true;
                }))
                .Turn("wizard", 2,
                    Step.Assert("record", ctx => { RecordHp(ctx, "warded"); Fact(ctx, "blocks.warded", GlobeBlocks(ctx, SpellNames.FIREBALL, "warded")); return true; }),
                    Step.Cast(SpellNames.FIREBALL, "warded"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "fireball", "warded"); Fact(ctx, "invisible.after", ctx.Get("wizard").HasActiveInvisibilityEffect); return true; }))
                .Expect("The Ui Fireball ran", Expect.StepStatus("wizard", 2, "Cast", 0, "done"))
                .Expect("The wizard is invisible and the globe covers the dummy", FactsAre(("globe.raised", true), ("invisible.before", true), ("blocks.warded", true)))
                .Expect("The burst includes no foe outside the globe: the dummy loses nothing and the wizard stays invisible (PHB p.236, p.245; SPL-141)",
                    FactsAre(("fireball.lost.warded", 0), ("invisible.after", true)))
                .Build();
        }

        // ── rules/prayer-globe (SPL-141) ────────────────────────────────

        /// <summary>
        /// SPL-141, delegated RAW reading 2026-10-14 (PHB p.236: the area of a spell of the globe's levels does not include
        /// the globe, and any type of spell can be cast out of it; Prayer, PHB p.264, is a 3rd-level 40-ft burst centred on
        /// the caster). The Scripted cleric 7 raises a lesser globe on itself in round 1 and casts Prayer from inside it in
        /// round 2, through the NPC cast path (the single-target route, whose Globe stage would otherwise fail the whole
        /// cast). Its ally and its foe inside the globe (5 ft away) get nothing, nor does the cleric; its ally and its foe
        /// outside the globe (25 and 30 ft away) get +1 luck and -1 on attacks.
        /// </summary>
        private static ScenarioDef PrayerGlobe()
        {
            return Rules("rules/prayer-globe", "Prayer cast from inside a lesser globe affects only the creatures outside the globe")
                .Covers("SPL-141", "PHB p.236", "PHB p.264")
                .MaxRounds(2)
                .Pc("priest", ActorSource.Stats(() => Cleric("Globe Priest", 7)), 3, 10, Control.Scripted)
                .Pc("allyIn", ActorSource.Stats(() => Wizard("Globe Ally Inside", 1)), 4, 10, Control.Idle)
                .Pc("allyOut", ActorSource.Stats(() => Wizard("Globe Ally Outside", 1)), 8, 10, Control.Idle)
                .Npc("foeIn", "target_dummy", 3, 11, Control.Idle)
                .Npc("foeOut", "target_dummy", 9, 10, Control.Idle)
                .Tweak("priest", c => Prepare(c, 3, SpellNames.PRAYER))
                .Initiative("priest", "allyIn", "allyOut", "foeIn", "foeOut")
                .Turn("priest", 1, RaiseLesserGlobe("priest"))
                .Turn("priest", 2,
                    Step.Assert("covered", ctx =>
                    {
                        foreach (string k in new[] { "priest", "allyIn", "allyOut", "foeIn", "foeOut" })
                            Fact(ctx, "blocks." + k, GlobeBlocks(ctx, SpellNames.PRAYER, k));
                        return true;
                    }),
                    Step.Cast(SpellNames.PRAYER, "priest"),
                    Step.Assert("after", ctx =>
                    {
                        foreach (string k in new[] { "priest", "allyIn", "allyOut", "foeIn", "foeOut" })
                            Fact(ctx, "attack." + k, ctx.Get(k).Stats.EffectAttackBonus);
                        return true;
                    }))
                .Expect("The Scripted Prayer ran", Expect.StepStatus("priest", 2, "Cast", 0, "done"))
                .Expect("The globe covers the cleric and the creatures 5 ft from it, not those 25 and 30 ft away (PHB p.236: 10-ft radius)",
                    FactsAre(("globe.raised", true), ("blocks.priest", true), ("blocks.allyIn", true), ("blocks.foeIn", true), ("blocks.allyOut", false), ("blocks.foeOut", false)))
                .Expect("Prayer gives +1 luck to the ally outside and -1 to the foe outside, and nothing inside the globe (PHB p.264, p.236; SPL-141)",
                    FactsAre(("attack.priest", 0), ("attack.allyIn", 0), ("attack.foeIn", 0), ("attack.allyOut", 1), ("attack.foeOut", -1)))
                .Build();
        }

        // ── rules/cast-quickened-threatened (SPL-143) ───────────────────

        /// <summary>
        /// SPL-143 (PHB p.98, Quicken Spell: casting a quickened spell does not provoke attacks of opportunity). A Ui wizard 9
        /// with Quicken Spell, a goblin beside it (-30 on attacks, 100 more hit points; first in initiative, so it is no
        /// longer flat-footed and threatens, PHB p.137), casts a quickened Magic Missile (an added 5th-level slot, SPL-041;
        /// every d4 is 4) at the goblin. Magic Missile makes no attack roll, so the only possible provocation is the casting
        /// itself (a ray's ranged touch attack would mix in a second question). The quickened cast is a free action and
        /// leaves the turn's other actions (PHB p.98). Today the pipeline still runs the
        /// provocation step: the AoO confirmation opens (answered Proceed), the cast is a threatened cast and the goblin
        /// makes its attack of opportunity.
        /// </summary>
        private static ScenarioDef QuickenedThreatened()
        {
            var quicken = new MetamagicData();
            quicken.Toggle(MetamagicFeatId.QuickenSpell);
            return Rules("rules/cast-quickened-threatened", "A quickened spell cast while threatened provokes (SPL-143, pinned)")
                .Covers("SPL-143", "PHB p.98")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 9, "Quicken Spell")), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 5, SpellNames.MAGIC_MISSILE))
                .Tweak("gob", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("gob", "wizard")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob", quicken),
                    Step.AnswerAoO(AoOAnswer.Proceed),
                    Step.Assert("after", ctx => { Fact(ctx, "actions", Actions(ctx.Get("wizard"))); FactSlotsSpent(ctx, "spent", "wizard"); return true; }))
                .Expect("The quickened cast spends a 5th-level slot and keeps the turn's other actions (PHB p.98)",
                    FactsAre(("spent", "L5+1"), ("actions", "smf")))
                .ExpectKnown("SPL-143", "Today the quickened cast opens the AoO confirmation and the goblin's attack of opportunity follows (SPL-143)", Expect.All(
                    Expect.StepStatus("wizard", 1, "AnswerAoO(Proceed)", 0, "done"),
                    Expect.Count("threatened_cast", e => e.Str("by") == "wizard", 1, 1),
                    Expect.Count("aoo", e => e.Str("by") == "gob" && e.Str("target") == "wizard", 1, 1)))
                .Build();
        }

        // ── rules/cast-empowered-fireball (SPL-038) ─────────────────────

        /// <summary>
        /// SPL-038 (PHB p.93, Empower Spell: all variable numeric effects are increased by one-half). A Ui wizard 9 with
        /// Empower Spell casts an empowered Fireball (from an added 5th-level slot, SPL-041) at a dummy that fails its save; every d6 is 3, so
        /// 9d6 = 27 and empowered 40. Today Fireball's custom handler (TryResolveScaledAoEDamageSpell) rolls plain dice.
        /// </summary>
        private static ScenarioDef EmpoweredFireball()
        {
            var empower = new MetamagicData();
            empower.Toggle(MetamagicFeatId.EmpowerSpell);
            return Rules("rules/cast-empowered-fireball", "An empowered Fireball deals unempowered damage (SPL-038, pinned)")
                .Covers("SPL-038", "PHB p.93", "PHB p.231")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 9, "Empower Spell")), 2, 10, Control.Ui)
                .Npc("d1", "target_dummy", 10, 10, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 5, SpellNames.FIREBALL))
                .Tweak("d1", Fails)
                .Initiative("wizard", "d1")
                .Force(6, 3, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d1"); RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.FIREBALL, "d1", empower),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "fireball", "d1"); FactSlotsSpent(ctx, "spent", "wizard"); return true; }))
                .Expect("The empowered cast ran and spent a 5th-level slot (PHB p.93: +2 levels)", Expect.All(
                    Expect.StepStatus("wizard", 1, "Cast", 0, "done"), FactIs("spent", "L5+1")))
                .ExpectKnown("SPL-038", "Today the empowered Fireball deals the plain 9d6 = 27, not 40 (SPL-038)", FactIs("fireball.lost.d1", 27))
                .Build();
        }

        // ── rules/cast-ally-in-burning-hands (SPL-149) ──────────────────

        /// <summary>
        /// SPL-149 (PHB p.177: a creature may give up a save voluntarily; nothing makes it fail). A Ui wizard 5 casts Burning
        /// Hands (5d4, every d4 at 4 = 20, Reflex half) through an ally fighter 10 ft away and a dummy 15 ft away, both with
        /// +30 on saves. The dummy saves and takes 10. Today ShouldForceTargetToAcceptSave makes the ally forgo its save in
        /// the generic area loop, so it takes the full 20.
        /// </summary>
        private static ScenarioDef AllyInBurningHands()
        {
            return Rules("rules/cast-ally-in-burning-hands", "An ally in its own side's Burning Hands gets no save (SPL-149, pinned)")
                .Covers("SPL-149", "PHB p.177", "PHB p.207")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Pin Ally", 3)), 7, 10, Control.Idle)
                .Npc("foe", "target_dummy", 8, 11, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.BURNING_HANDS))
                .Tweak("ally", Saves).Tweak("foe", Saves)
                .Initiative("wizard", "ally", "foe")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "ally", "foe"); return true; }),
                    Step.Cast(SpellNames.BURNING_HANDS, "foe"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "cone", "ally", "foe"); return true; }))
                .Expect("The foe makes its Reflex save and takes half (PHB p.207)", FactIs("cone.lost.foe", 10))
                .ExpectKnown("SPL-149", "Today the ally forgoes its save and takes the full 20 (SPL-149)", FactIs("cone.lost.ally", 20))
                .Build();
        }

        // ── rules/cast-entangled (SPL-145) ──────────────────────────────

        /// <summary>
        /// SPL-145 (PHB p.70 Concentration, p.308 entangled; DC 15 + the spell's level, delegated RAW reading 2026-10-14). An
        /// entangled Ui wizard 5 (INT 16) casts Magic Missile; the entangled check's d20 is forced to 10. Today the check
        /// (ResolveEntangledSomaticCastingConcentration) adds caster level + INT (5 + 3 = 8) instead of the Concentration
        /// modifier, so it logs "d20 10 + 8 = 18 vs DC 16" and the spell goes off.
        /// </summary>
        private static ScenarioDef Entangled()
        {
            return Rules("rules/cast-entangled", "The entangled casting check adds caster level and the casting ability, not Concentration (SPL-145, pinned)")
                .Covers("SPL-145", "PHB p.70", "PHB p.308")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .StartCondition("wizard", CombatConditionType.Entangled, 10)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.MAGIC_MISSILE))
                .Initiative("wizard", "dummy")
                // The entangled casting check rolls its d20 under the context "Dispel Magic check", a mislabel that is part of
                // SPL-145: the fix that relabels the roll must change this force with it.
                .Force(20, 10, "Dispel Magic check", 1)
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "dummy"); Fact(ctx, "entangled", ctx.Get("wizard").HasCondition(CombatConditionType.Entangled)); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "dummy"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "missile", "dummy"); return true; }))
                .Expect("The wizard is entangled when it casts", FactIs("entangled", true))
                .ExpectKnown("SPL-145", "Today the check is d20 + caster level 5 + INT 3 against DC 16, and the spell goes off (SPL-145)", Expect.All(
                    v => v.Log(@"Entangled somatic concentration \(Pin Wizard, Magic Missile\): d20 10 \+ 8 = 18 vs DC 16").Count == 1
                        ? ExpectResult.Pass("logged d20 10 + 8 = 18 vs DC 16") : ExpectResult.Fail("no entangled check logged as d20 10 + 8 = 18 vs DC 16"),
                    FactIs("missile.lost.dummy", 15)))
                .Build();
        }

        // ── rules/stale-cast-state-* (batch 11 step 4) ──────────────────

        /// <summary>The resist energy entries of <paramref name="c"/>, such as "Fire:10", or "none".</summary>
        private static string ResistEntries(CharacterController c)
        {
            List<string> entries = c.Stats.ActiveResistEnergyEffects != null
                ? c.Stats.ActiveResistEnergyEffects.Where(r => r != null).Select(r => r.EnergyType + ":" + r.ResistanceAmount).ToList()
                : new List<string>();
            return entries.Count > 0 ? string.Join(",", entries) : "none";
        }

        /// <summary>
        /// Lets the NPC executor's availability check (SpellcastingComponent.CanCast, which first bounds the spell level by
        /// the per-level SlotsRemaining view) see a slot <see cref="AddWizardSlot"/> added above the class table (SPL-041):
        /// the view is widened to <paramref name="level"/> with one slot there. The spend itself reads the slots.
        /// </summary>
        private static void WidenSlotsRemaining(CharacterController c, int level)
        {
            SpellcastingComponent sc = c.Spellcasting;
            int[] old = sc.SlotsRemaining ?? new int[0];
            if (old.Length > level)
                return;
            var widened = new int[level + 1];
            Array.Copy(old, widened, old.Length);
            widened[level] = 1;
            sc.SlotsRemaining = widened;
        }

        /// <summary>The pack slot of the first item with ItemDatabase id <paramref name="itemId"/>, or -1.</summary>
        private static int PackIndex(CharacterController c, string itemId)
        {
            global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
            if (inv == null || inv.GeneralSlots == null)
                return -1;
            for (int i = 0; i < inv.GeneralSlots.Length; i++)
                if (inv.GeneralSlots[i] != null && inv.GeneralSlots[i].Id == itemId)
                    return i;
            return -1;
        }

        /// <summary>
        /// Batch 11 step 4 (SPL-112, stale-choice half; PHB p.272: the caster chooses the energy type). A threatened Ui wizard
        /// 5 picks cold for Resist Energy on itself and cancels the cast at its AoO confirmation (the goblin beside it is
        /// first in initiative, so it threatens; -30 on attacks). The cancel used to leave the cold choice in GameManager's
        /// shared field, and the Scripted wizard 5 who casts Resist Energy on itself next (away from the goblin) took that
        /// stale cold instead of its own choice. The choice is now the cancelled cast's own and ends with it; the Scripted
        /// cast's request gets the NPC path's fire (AiCastDecider; the AI's real choice is SPL-112's open half). The cancel
        /// spends nothing (the AoO prompt's refund) and leaves no cast open.
        /// </summary>
        private static ScenarioDef StaleEnergyChoice()
        {
            return StaleRules("rules/stale-cast-state-energy", "A cancelled PC energy choice does not reach a later NPC Resist Energy (SPL-112)")
                .Covers("SPL-112", "PHB p.272", "PHB p.141")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Stale Ui Wizard", 5)), 5, 10, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Wizard("Stale Scripted Wizard", 5)), 5, 16, Control.Scripted)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 2, SpellNames.RESIST_ENERGY))
                .Tweak("scripted", c => Prepare(c, 2, SpellNames.RESIST_ENERGY))
                .Tweak("gob", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("gob", "wizard", "scripted")
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { Fact(ctx, "slots.before", Slots(ctx.Get("wizard"))); return true; }),
                    Step.CastChoosing(SpellNames.RESIST_ENERGY, "wizard", "Cold"),
                    Step.AnswerAoO(AoOAnswer.Cancel),
                    Step.Assert("after the cancel", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "slots.after", Slots(w));
                        Fact(ctx, "ui.resist", ResistEntries(w));
                        Fact(ctx, "open.casts", ctx.Gm.Harness_OpenCastCount);
                        return true;
                    }))
                .Turn("scripted", 1,
                    Step.Cast(SpellNames.RESIST_ENERGY, "scripted"),
                    Step.Assert("record", ctx => { Fact(ctx, "scripted.resist", ResistEntries(ctx.Get("scripted"))); return true; }))
                .Expect("The Ui cast reached its AoO confirmation and was cancelled", Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"))
                .Expect("The cancelled cast spends no slot, grants nothing and leaves no cast open", FactsAre(("ui.resist", "none"), ("open.casts", 0)))
                .Expect("The cancelled cast's slot is given back", FactsEqual("slots.before", "slots.after"))
                .Expect("The Scripted cast ran", Expect.StepStatus("scripted", 1, "Cast", 0, "done"))
                .Expect("The Scripted cast resists its own request's energy, fire, not the cancelled PC cast's cold (SPL-112 stale-choice half)", FactIs("scripted.resist", "Fire:10"))
                .Build();
        }

        /// <summary>
        /// Batch 11 step 4 (PHB p.230: the caster chooses warm or chill). The Fire Shield prompt re-enters the target
        /// selection (BeginPendingSpellTargeting) inside the same cast, so the Ui wizard's chill pick reaches its cast; the
        /// Scripted wizard's cast that follows has its own request, warm (AiCastDecider, the NPC path's old default).
        /// Fire Shield is in an added 4th-level slot (SPL-041; for the Scripted wizard the NPC executor's slot view is widened
        /// to it right before its cast, <see cref="WidenSlotsRemaining"/>, which is why that cast is made from an Assert step
        /// through TryNPCPerformSpellCastForAI, the call the Scripted Cast step makes).
        /// </summary>
        private static ScenarioDef FireShieldChoicePerCast()
        {
            return StaleRules("rules/stale-cast-state-fire-shield", "The Fire Shield choice survives the prompt's re-entry and stays with its cast (SPL-112)")
                .Covers("SPL-112", "PHB p.230")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Stale Ui Wizard", 7)), 5, 10, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Wizard("Stale Scripted Wizard", 7)), 5, 16, Control.Scripted)
                .Npc("dummy", "target_dummy", 14, 13, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 4, SpellNames.FIRE_SHIELD))
                .Tweak("scripted", c => AddWizardSlot(c, 4, SpellNames.FIRE_SHIELD))
                .Initiative("wizard", "scripted", "dummy")
                .Turn("wizard", 1,
                    Step.CastChoosing(SpellNames.FIRE_SHIELD, "wizard", "Chill"),
                    Step.Assert("record", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "ui.shield", w.Stats.FireShieldActive ? (w.Stats.FireShieldIsWarm ? "warm" : "chill") : "none");
                        Fact(ctx, "open.casts", ctx.Gm.Harness_OpenCastCount);
                        return true;
                    }))
                .Turn("scripted", 1,
                    Step.Assert("cast Fire Shield through the NPC executor", ctx =>
                    {
                        CharacterController s = ctx.Get("scripted");
                        WidenSlotsRemaining(s, 4);
                        Fact(ctx, "scripted.cast", ctx.Gm.TryNPCPerformSpellCastForAI(s, s, SpellDatabase.GetSpell(SpellNames.FIRE_SHIELD).Clone()));
                        Fact(ctx, "scripted.shield", s.Stats.FireShieldActive ? (s.Stats.FireShieldIsWarm ? "warm" : "chill") : "none");
                        return true;
                    }))
                .Expect("Both casts ran", Expect.All(Expect.StepStatus("wizard", 1, "Cast", 0, "done"), FactIs("scripted.cast", true)))
                .Expect("The Ui wizard's chill pick reaches its own cast through the prompt's re-entry, and the cast closes", FactsAre(("ui.shield", "chill"), ("open.casts", 0)))
                .Expect("The Scripted cast's own request is warm (the NPC path's choice)", FactIs("scripted.shield", "warm"))
                .Build();
        }

        /// <summary>
        /// Batch 11 step 4 (SPL-116; PHB p.122: an arcane caster in armor risks arcane spell failure on a spell with a
        /// somatic component). A Ui wizard 5 uses its wand of magic missile and cancels at the target selection (right-click,
        /// CancelSpellTargeting); the wand flag used to stay set after that cancel, and the shared spell-failure check skipped
        /// arcane spell failure for every later caster while it was set. The Scripted wizard 5 then casts Magic Missile with
        /// 100% arcane spell failure (set just before its cast): it must roll and lose the spell. The wand keeps its charges
        /// (the cast never got to spend one).
        /// </summary>
        private static ScenarioDef StaleWandFlag()
        {
            return StaleRules("rules/stale-cast-state-wand-asf", "A wand cast cancelled at targeting does not spare a later NPC cast its arcane spell failure (SPL-116)")
                .Covers("SPL-116", "PHB p.122", "DMG p.245")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Stale Ui Wizard", 5)), 5, 10, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Wizard("Stale Scripted Wizard", 5)), 5, 16, Control.Scripted)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Npc("dummy2", "target_dummy", 9, 16, Control.Idle)
                .Tweak("wizard", c => GiveItem(c, WandId))
                .Tweak("scripted", c => Prepare(c, 1, SpellNames.MAGIC_MISSILE))
                .Tweak("dummy2", Sturdy)
                .Initiative("wizard", "scripted", "dummy", "dummy2")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("use the wand, then cancel the target selection", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        RecordItem(ctx, WandId, "before");
                        int slot = PackIndex(w, WandId);
                        bool used = slot >= 0 && ctx.Gm.TryUseConsumableFromInventory(w, slot, out _);
                        Fact(ctx, "wand.selecting", used && ctx.Gm.CurrentSubPhase == GameManager.PlayerSubPhase.SelectingAttackTarget);
                        Fact(ctx, "wand.cancelled", ctx.Gm.Harness_CancelSelection());
                        Fact(ctx, "open.casts", ctx.Gm.Harness_OpenCastCount);
                        RecordItem(ctx, WandId, "after");
                        return true;
                    }))
                .Turn("scripted", 1,
                    Step.Assert("cast Magic Missile in armor with 100% arcane spell failure", ctx =>
                    {
                        CharacterController s = ctx.Get("scripted");
                        RecordHp(ctx, "dummy2");
                        s.Stats.ArcaneSpellFailure = 100;
                        Fact(ctx, "cast", ctx.Gm.TryNPCPerformSpellCastForAI(s, ctx.Get("dummy2"), SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone()));
                        FactHpLost(ctx, "missile", "dummy2");
                        return true;
                    }))
                .Expect("The wand's target selection opened and was cancelled, leaving no cast open", FactsAre(("wand.selecting", true), ("wand.cancelled", true), ("open.casts", 0)))
                .Expect("The cancelled wand cast spent no charge", FactsEqual("charges.before", "charges.after"))
                .Expect("The Scripted cast went through the executor", FactIs("cast", true))
                .Expect("It rolled arcane spell failure (PHB p.122)", v => v.Log(@"ASF Check \(Stale Scripted Wizard, Magic Missile\): roll \d+% vs 100%").Count == 1
                    ? ExpectResult.Pass("one ASF check logged at 100%") : ExpectResult.Fail("no ASF check logged for the Scripted cast"))
                .Expect("The spell is lost: the dummy takes nothing (SPL-116 stale-flag part)", FactIs("missile.lost.dummy2", 0))
                .Build();
        }

        /// <summary>
        /// Batch 11 step 4 (SPL-036). The F12 Spell Testing Panel's privileges (no arcane spell failure, no component pouch
        /// or costly components) are TestPanelBypass flags on its cast's source; they used to be cleared as soon as a
        /// single-target cast started resolving (PerformSpellCast), so that cast got none of them. A Ui wizard 5 with 100%
        /// arcane spell failure casts Magic Missile at the dummy through TestCastSpellFromPanel (every d4 4: three missiles of
        /// 1d4+1 = 15); the bypass holds through resolution, so no failure check is rolled and the missiles hit, and the
        /// panel's caster is forgotten when the cast ends (no leak into later casts). The panel is clicked twice first: the
        /// second F12 cast replaces the first, still at its target selection, and keeps the panel's caster.
        /// </summary>
        private static ScenarioDef TestPanelBypassThroughResolution()
        {
            return StaleRules("rules/stale-cast-state-f12-bypass", "An F12 single-target cast keeps its bypass through resolution (SPL-036)")
                .Covers("SPL-036", "PHB p.122", "PHB p.251")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Stale Ui Wizard", 5)), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.MAGIC_MISSILE))
                .Tweak("dummy", Sturdy)
                .Initiative("wizard", "dummy")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("F12 cast at the dummy with 100% arcane spell failure", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        RecordHp(ctx, "dummy");
                        w.Stats.ArcaneSpellFailure = 100;
                        // A first F12 click left at its target selection, then a second: the second cast replaces the first
                        // and keeps the panel's caster (it is stored after the new cast opens).
                        ctx.Gm.TestCastSpellFromPanel(w, SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE), false);
                        ctx.Gm.TestCastSpellFromPanel(w, SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE), false);
                        Fact(ctx, "f12.second.caster", ReferenceEquals(ctx.Gm.GetTestPanelCaster(), w) ? "kept" : "lost");
                        Fact(ctx, "f12.second.open", ctx.Gm.Harness_OpenCastCount);
                        Fact(ctx, "f12.selecting", ctx.Gm.CurrentSubPhase == GameManager.PlayerSubPhase.SelectingAttackTarget);
                        ctx.Gm.OnCellClicked(ctx.Gm.Grid.GetCell(ctx.Get("dummy").GridPosition));
                        FactHpLost(ctx, "missile", "dummy");
                        return true;
                    }),
                    Step.Assert("after", ctx =>
                    {
                        Fact(ctx, "f12.caster", ctx.Gm.GetTestPanelCaster() == null ? "none" : "kept");
                        Fact(ctx, "open.casts", ctx.Gm.Harness_OpenCastCount);
                        return true;
                    }))
                .Expect("A second F12 click replaces the open F12 cast and keeps the panel's caster", FactsAre(("f12.second.caster", "kept"), ("f12.second.open", 1)))
                .Expect("The F12 cast opened its target selection", FactIs("f12.selecting", true))
                .Expect("No arcane spell failure is rolled for the F12 cast (its ArcaneFailure bypass holds through resolution, SPL-036)",
                    v => v.Log(@"ASF Check \(Stale Ui Wizard, Magic Missile\)").Count == 0
                        ? ExpectResult.Pass("no ASF check logged") : ExpectResult.Fail("an ASF check was logged for the F12 cast"))
                .Expect("The missiles hit: 3 x (1d4+1) = 15 (PHB p.251)", FactIs("missile.lost.dummy", 15))
                .Expect("The cast ended: no cast open and the panel's caster forgotten", FactsAre(("f12.caster", "none"), ("open.casts", 0)))
                .Build();
        }

        // ── rules/cast-slot-failure-refund (SPL-147) ────────────────────

        /// <summary>
        /// SPL-147 (PHB p.141 and p.143: casting the spell is the action; a cast that never happens costs nothing). A Ui
        /// wizard 1 with Still Spell picks a stilled Magic Missile (a 2nd-level slot, which a wizard 1 does not have; the
        /// harness selects it past the spell menu's slot filter, as a slot mismatch such as SPL-011 or SPL-154 does). Today
        /// PerformSpellCast spends the standard action, then fails to spend the slot, logs an error and returns to the action
        /// menu without restoring the action: no spell, the slot kept, the standard action lost.
        /// </summary>
        private static ScenarioDef SlotFailureRefund()
        {
            var still = new MetamagicData();
            still.Toggle(MetamagicFeatId.StillSpell);
            return Rules("rules/cast-slot-failure-refund", "A failed slot spend loses the action (SPL-147, pinned)")
                .Covers("SPL-147", "PHB p.141", "PHB p.143")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 1, "Still Spell")), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.MAGIC_MISSILE))
                .Initiative("wizard", "dummy")
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "dummy"); Fact(ctx, "slots.before", Slots(ctx.Get("wizard"))); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "dummy", still),
                    Step.Assert("after", ctx => { CharacterController w = ctx.Get("wizard"); FactHpLost(ctx, "missile", "dummy"); Fact(ctx, "slots.after", Slots(w)); Fact(ctx, "actions", Actions(w)); return true; }))
                .Expect("No spell: the dummy is untouched and the slot kept", Expect.All(FactIs("missile.lost.dummy", 0), FactsEqual("slots.before", "slots.after")))
                .ExpectKnown("SPL-147", "Today the standard action is spent anyway (SPL-147)", FactIs("actions", "-m-"))
                .Build();
        }
    }
}
#endif
