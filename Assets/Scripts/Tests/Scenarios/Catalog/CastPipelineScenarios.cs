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
            yield return S("area-damage-one-roll", AreaDamageOneRoll);
            yield return S("cast-ui-sleep", UiSleep);
            yield return S("cast-ui-sleep-quickened", UiSleepQuickened);
            yield return S("cast-scroll-ui", () => ItemCast(scroll: true));
            yield return S("cast-wand-ui", () => ItemCast(scroll: false));
            yield return S("cast-scroll-aoo-cancel", () => ItemAoOCancel(scroll: true));
            yield return S("cast-wand-aoo-cancel", () => ItemAoOCancel(scroll: false));
            yield return S("cast-held-touch-charge", HeldTouchCharge);
            yield return S("cast-held-aoo-cancel", HeldAoOCancel);
            yield return S("cast-held-grappled", HeldGrappled);
            yield return S("npc-concentration-tracking", NpcConcentrationTracking);
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
            yield return S("npc-resist-energy-choice", () => NpcResistEnergyChoice("rules/npc-resist-energy-choice", "dragon_red_wyrmling", "red dragon", "Fire"));
            yield return S("npc-resist-energy-choice-cold", () => NpcResistEnergyChoice("rules/npc-resist-energy-choice-cold", "dragon_white_wyrmling", "white dragon", "Cold"));
            yield return S("npc-magic-weapon", NpcMagicWeapon);
            yield return S("ai-spell-retry", AiSpellRetry);
            yield return S("ai-spell-retry-refused", AiSpellRetryRefused);
            yield return S("cast-globe-damage", GlobeDamage);
            yield return S("area-globe", AreaGlobe);
            yield return S("cast-globe-held-touch", GlobeHeldTouch);
            yield return S("cast-globe-dispel", GlobeDispel);
            yield return S("cast-globe-grease-armor", GlobeGreaseArmor);
            yield return S("prayer-globe", PrayerGlobe);
            yield return S("area-globe-invisible", AreaGlobeInvisible);
            yield return S("cast-quickened-threatened", QuickenedThreatened);
            yield return S("quicken-once-per-round", QuickenOncePerRound);
            yield return S("cast-free-action-threatened", FreeActionThreatened);
            yield return S("cast-full-round-after-move", FullRoundAfterMove);
            yield return S("cast-aoo-cancel-keeps-concentration", AoOCancelKeepsConcentration);
            yield return S("concentration-cast-ends-image", () => CastEndsImage(ui: true));
            yield return S("concentration-cast-ends-image-npc", () => CastEndsImage(ui: false));
            yield return S("concentration-lapse-unmaintained", () => LapseUnmaintained(ui: true));
            yield return S("concentration-lapse-unmaintained-npc", () => LapseUnmaintained(ui: false));
            yield return S("concentration-ai-maintains", AiMaintains);
            yield return S("cast-empowered-fireball", EmpoweredFireball);
            yield return S("cast-globe-heightened", GlobeHeightened);
            yield return S("cast-ally-in-burning-hands", AllyInBurningHands);
            yield return S("sleep-spell-penetration", SleepSpellPenetration);
            yield return S("spell-save-bonus-custom-handler", SpellSaveBonusCustomHandler);
            yield return S("cast-entangled", Entangled);
            yield return S("cast-silenced", Silenced);
            yield return S("npc-cast-without-divine-focus", NpcCastWithoutDivineFocus);
            yield return S("cast-deafened-area", DeafenedArea);
            yield return S("cast-slot-failure-refund", SlotFailureRefund);
            yield return S("metamagic-cast-paths", MetamagicCastPaths);
            yield return S("npc-cast-prepared-metamagic", NpcCastPreparedMetamagic);
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
        /// 4th- or 5th-level slot (PHB p.55) adds the one it casts from; the slot spend matches the spell, not the class
        /// table. With <paramref name="metamagic"/> the slot holds the spell prepared with it (PHB p.88: a prepared caster
        /// applies metamagic when preparing, and a metamagic cast spends only such a slot since batch 11 step 21, SPL-012).
        /// </summary>
        private static void AddWizardSlot(CharacterController c, int level, string spellId, MetamagicData metamagic = null)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting to add a slot to");
            var slot = new SpellSlot(level, false, "Wizard");
            slot.PrepareWithMetamagic(SpellDatabase.GetSpell(spellId).Clone(), metamagic);
            sc.SpellSlots.Add(slot);
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
        /// (PHB p.280), a full-round action (PHB p.174): since batch 11 step 12 the data says so and the cast core's Cost
        /// stage charges it, so no action is left (SPL-162; the effect still comes in the casting turn, SPL-164).
        /// </summary>
        private static ScenarioDef UiSleep()
        {
            return Rules("rules/cast-ui-sleep", "A Ui Sleep: an area handler resolves it and the cast core finishes it (PHB p.280, p.174)")
                .Covers("PHB p.280", "PHB p.174", "SPL-162", "batch 11 step 9", "batch 11 step 12")
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
                .Expect("Sleep's 1-round casting time is a full-round action: no action is left (PHB p.280, p.174)",
                    FactIs("actions", "---"))
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
                .Tweak("wizard", c => AddWizardSlot(c, 5, SpellNames.SLEEP, quicken))
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

        // ── rules/area-damage-one-roll (batch 11 step 20, D6) ───────────

        /// <summary>
        /// Decision D6 of the batch 11 design (delegated RAW reading 2026-10-14, batch 11 step 20, SPL-180): an area damage
        /// effect rolls its damage once and every creature in the area takes that one total before its own save (PHB p.231:
        /// unattended objects "also take this damage"; p.208, Chain Lightning's half "as much"; MM p.137, a fireball's
        /// damage total). Round 1: a Ui wizard 5 casts Fireball (PHB p.231; TryResolveScaledAoEDamageSpell) centred on d1;
        /// d1 fails its Reflex save, d2 makes it, and a human monk 3 makes it. The first five d6 are 1 and every later d6 is
        /// 6, so the one roll is 5: d1 loses 5, d2 half of it (2), the monk none (Evasion, PHB p.41). Rolled per creature,
        /// d2 would have lost 15 and the monk 15. Round 2: Burning Hands (PHB p.207; the generic area loop and
        /// SpellCaster.Cast) at b1 (fails) and b2 (saves); the first five d4 are 1, later ones 4: b1 loses 5, b2 2 (per
        /// creature: 5 and 10). Five damage dice are drawn for each cast.
        /// </summary>
        private static ScenarioDef AreaDamageOneRoll()
        {
            return Rules("rules/area-damage-one-roll", "An area spell rolls its damage once for every creature in the area; saves and Evasion stay per creature (D6; PHB p.231, p.207, p.41)")
                .Covers("SPL-180", "SPL-090", "PHB p.231", "PHB p.207", "PHB p.41", "PHB p.177", "batch 11 step 20")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 2, 10, Control.Ui)
                .Npc("d1", "target_dummy", 10, 10, Control.Idle)
                .Npc("d2", "target_dummy", 11, 10, Control.Idle)
                .Npc("monk", "human_monk_3", 10, 12, Control.Idle)
                .Npc("b1", "target_dummy", 4, 10, Control.Idle)
                .Npc("b2", "target_dummy", 5, 11, Control.Idle)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.BURNING_HANDS); AddWizardSlot(c, 3, SpellNames.FIREBALL); })
                .Tweak("d1", Fails).Tweak("d2", Saves).Tweak("monk", Saves).Tweak("b1", Fails).Tweak("b2", Saves)
                .Initiative("wizard", "d1", "d2", "monk", "b1", "b2")
                .Force(6, 1, null, 5)
                .Force(6, 6, null, -1)
                .Force(4, 1, null, 5)
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx =>
                    {
                        RecordHp(ctx, "d1", "d2", "monk", "b1", "b2");
                        Fact(ctx, "monk.evasion", ctx.Get("monk").Stats.HasEvasion);
                        return true;
                    }),
                    Step.Cast(SpellNames.FIREBALL, "d1"),
                    Step.Assert("after fireball", ctx => { FactHpLost(ctx, "fireball", "d1", "d2", "monk", "b1", "b2"); RecordHp(ctx, "b1", "b2"); return true; }))
                .Turn("wizard", 2,
                    Step.Cast(SpellNames.BURNING_HANDS, "b1"),
                    Step.Assert("after cone", ctx => { FactHpLost(ctx, "cone", "b1", "b2"); return true; }))
                .Expect("Both Ui casts ran", Expect.All(Expect.StepStatus("wizard", 1, "Cast", 0, "done"), Expect.StepStatus("wizard", 2, "Cast", 0, "done")))
                .Expect("The monk 3 has Evasion (PHB p.41)", FactIs("monk.evasion", true))
                .Expect("Fireball: one 5d6 roll of 5; failed 5, saved 2, the monk's Evasion 0, nothing outside the burst (D6; PHB p.231, p.41)",
                    FactsAre(("fireball.lost.d1", 5), ("fireball.lost.d2", 2), ("fireball.lost.monk", 0), ("fireball.lost.b1", 0), ("fireball.lost.b2", 0)))
                .Expect("Burning Hands: one 5d4 roll of 5; failed 5, saved 2 (D6; PHB p.207)",
                    FactsAre(("cone.lost.b1", 5), ("cone.lost.b2", 2)))
                .Expect("Five d6 and five d4 are drawn: one damage roll per cast",
                    Expect.All(Expect.Count("dice", e => e.Int("sides") == 6, 5, 5), Expect.Count("dice", e => e.Int("sides") == 4, 5, 5)))
                .Build();
        }

        // ── rules/cast-scroll-ui, rules/cast-wand-ui ────────────────────

        /// <summary>
        /// A Ui wizard 5 with no enemy adjacent uses a scroll (or a wand) of magic missile (caster level 1: one missile,
        /// 1d4+1, PHB p.251; DMG p.237 and p.245: the item's caster level) on a dummy through the Use Item call
        /// (TryUseConsumableFromInventory), which enters the cast pipeline (InitiateScrollCastThroughPipeline,
        /// InitiateWandCastThroughPipeline) and PerformSpellCast. Every d4 is 4. The scroll is used up (or the wand loses one
        /// charge, both charge fields, ITM-034), no prepared slot is spent, and no action is left: RAW retrieving the stored
        /// item is a move action (PHB p.141, Table 8-2) and activating it a standard action (DMG p.213). The missiles count
        /// at the item's caster level 1, not the user's 5 (one missile, 5; CasterLevelRules, batch 11 step 14, SPL-016).
        /// Pinned: today the item use is charged as one full-round action (GetItemUseActionType, ITM-005; the fact
        /// "fullround" tells the two apart, since both leave no action).
        /// </summary>
        private static ScenarioDef ItemCast(bool scroll)
        {
            string id = scroll ? ScrollId : WandId;
            string name = scroll ? "scroll" : "wand";
            ScenarioBuilder b = Rules("rules/cast-" + name + "-ui", "A Ui " + name + " of magic missile through the cast pipeline: the " + name + " is spent, no slot, the item action (DMG p.237, p.245)")
                .Covers("DMG p.237", "DMG p.245", "PHB p.251", "ITM-005", "ITM-034", "SPL-016")
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
                .Expect("The missiles count at the item's caster level 1: one missile of 1d4+1 = 5 (DMG p.237, p.245; PHB p.251)", FactIs("missile.lost.dummy", 5))
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
        /// first goes to the item use's own confirmation, answered Proceed) and -30 on attacks. The pipeline spends the
        /// scroll or the charge (ConsumePendingSpellSlot) before the confirmation; since batch 11 step 14 the cancel restores
        /// the snapshot, which records the scroll's stack and slot and both of the wand's charge stores (ITM-034), so the
        /// scroll is kept (or no charge lost), no slot is spent and the standard action of activation is left. Every other
        /// action is left too, but only because the item use is one deferred full-round action (ITM-005): RAW the
        /// retrieval is a move action that provoked (PHB p.141, Table 8-2) and stays spent once its confirmation was
        /// answered Proceed, so that part is pinned as today's behaviour.
        /// A scroll is a spell completion item and provokes as casting does (DMG p.213), so its cast confirmation is the
        /// rule. A wand is a spell trigger item whose activation does not provoke (DMG p.213, p.245); only retrieving it
        /// provokes (PHB p.141). Today the wand skips the item-use confirmation and its pipeline cast opens the spellcasting
        /// one (ITM-085), so the wand scenario pins that confirmation as today's behaviour, not as the rule.
        /// </summary>
        private static ScenarioDef ItemAoOCancel(bool scroll)
        {
            string id = scroll ? ScrollId : WandId;
            string name = scroll ? "scroll" : "wand";
            ScenarioBuilder b = Rules("rules/cast-" + name + "-aoo-cancel", "A " + name + " cast cancelled at the AoO prompt keeps the " + name + (scroll ? " (ITM-084)" : " (ITM-084; ITM-085 pinned)"))
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
            steps.Add(Step.Assert("after", ctx => { RecordItem(ctx, id, "after"); FactHpLost(ctx, "missile", "dummy"); Fact(ctx, "actions", Actions(ctx.Get("wizard"))); Fact(ctx, "standard.left", ctx.Get("wizard").Actions.HasStandardAction); return true; }));
            b = b.Turn("wizard", 1, steps.ToArray());
            b = scroll
                ? b.Expect("The scroll's cast confirmation (it provokes as casting does, DMG p.213) was answered Cancel", Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"))
                : b.Covers("ITM-085").ExpectKnown("ITM-085", "Today the wand's activation opens the spellcasting AoO confirmation, answered Cancel, where a spell trigger activation does not provoke and the retrieval that does gets no confirmation (ITM-085)",
                    Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"));
            b = b.Expect("The cancelled spell does nothing", FactIs("missile.lost.dummy", 0))
                .Expect("No prepared slot is spent", FactsEqual("slots.before", "slots.after"))
                .Expect("The standard action is left: the cancelled spell cost no activation (ITM-084; DMG p.237, p.245)", FactIs("standard.left", true))
                .Covers("ITM-005").ExpectKnown("ITM-005", "Today every action is left, the move and full-round ones too: the cancel refunds the one deferred full-round item action, where RAW the retrieval's move action stays spent once it provoked (PHB p.141, Table 8-2; ITM-005)",
                    FactIs("actions", "smf"));
            b = scroll
                ? b.Expect("The scroll is kept: the cancel restores it (ITM-084)", Expect.All(FactIs("count.before", 1), FactIs("count.after", 1)))
                : b.Expect("The wand keeps its charge in both charge fields: the cancel restores them (ITM-084, ITM-034)", Expect.All(FactIs("charges.before", "50/50"), FactIs("charges.after", "50/50")));
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
        /// the initiative after the caster. Summon Monster's 1-round casting time is a full-round action (PHB p.174; since
        /// batch 11 step 12), so no action is left; the creature still appears in the casting turn (SPL-164).
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
                .Expect("Summon Monster's 1-round casting time is a full-round action: no action is left (PHB p.285, p.174)", FactIs("cast.actions", "---"))
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
        /// An entangled Ui wizard 3 loses Summon Monster I to its entangled check (PHB p.308, p.70: Concentration against DC
        /// 15 + 1, the d20 forced to 1 under GameManager.EntangledConcentrationDiceContext; batch 11 step 13, SPL-145): the
        /// slot is spent and no creature appears. Since step 13 the lost summon pauses like any lost cast (SPL-006).
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
                .Force(20, 1, GameManager.EntangledConcentrationDiceContext, 1)
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

        // ── rules/cast-held-grappled, rules/npc-concentration-tracking (batch 11 step 15) ──

        /// <summary>
        /// Step-1 issue (f), SPL-146, fixed in batch 11 step 15: holding the charge is casting the spell (PHB p.141), so a
        /// grappled caster meets the grapple casting rule at the hold (PHB p.156: no somatic component while grappling). A
        /// Ui wizard 5 grappled at the start of its turn picks Shocking Grasp (V, S) and answers the touch prompt Discharge
        /// Later: the hold is refused at Legality with one log line, nothing is spent and no charge is held.
        /// </summary>
        private static ScenarioDef HeldGrappled()
        {
            return Rules("rules/cast-held-grappled", "A grappled Ui caster cannot hold the charge of a somatic touch spell (PHB p.141, p.156; SPL-146)")
                .Covers("PHB p.141", "PHB p.156", "SPL-146", "batch 11 step 15")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Grappled Wizard", 5)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 15, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SHOCKING_GRASP))
                .Initiative("wizard", "gob")
                .Turn("wizard", 1,
                    Step.Assert("grappled", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        w.ApplyCondition(CombatConditionType.Grappled, 10, "scenario");
                        Fact(ctx, "grappled", w.HasCondition(CombatConditionType.Grappled));
                        return true;
                    }),
                    RecordCaster("wizard"),
                    Step.CastHeld(SpellNames.SHOCKING_GRASP),
                    FactCaster("hold", "wizard"))
                .Expect("The wizard is grappled when it casts", FactIs("grappled", true))
                .Expect("The hold is refused: nothing is spent and no charge is held (PHB p.141, p.156)",
                    FactsAre(("hold.spent", "none"), ("hold.actions", "smf"), ("hold.held", false)))
                .Expect("The refusal is logged once", Expect.Count("log", e => (e.Str("text") ?? "").Contains("spells with somatic components are blocked"), 1, 1))
                .Expect("No Shocking Grasp is cast", CastEvents("wizard", SpellNames.SHOCKING_GRASP, 0))
                .Build();
        }

        /// <summary>
        /// SPL-114, fixed in batch 11 step 15: an NPC's Concentration-duration spell starts its concentration as a PC's
        /// does (PHB p.176). The Scripted neutral mage (an enemy wizard 3, given a 1st-level slot holding Silent Image, PHB
        /// p.279) casts Silent Image through the NPC cast path and is then dealt 30 damage (GameManager.DealDamage): the
        /// Concentration check against damage is DC 10 + 30 + the spell's level 1 = 41 (PHB p.69-70), which no d20 roll of
        /// the mage reaches, so its concentration breaks and the image ends.
        /// </summary>
        private static ScenarioDef NpcConcentrationTracking()
        {
            return Rules("rules/npc-concentration-tracking", "An NPC's Silent Image ends when the caster is damaged and fails Concentration (PHB p.176; SPL-114)")
                .Covers("SPL-114", "PHB p.176", "PHB p.279", "PHB p.69-70", "PC_NPC_PARITY", "batch 11 step 15")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Wizard("Conc Hero", 3)), 4, 10, Control.Idle)
                .Npc("mage", "neutral_mage_test", 10, 10, Control.Scripted)
                .Tweak("mage", c => { AddWizardSlot(c, 1, SpellNames.SILENT_IMAGE); Sturdy(c); })
                .Initiative("mage", "hero")
                .Turn("mage", 1,
                    Step.Cast(SpellNames.SILENT_IMAGE, "mage"),
                    Step.Assert("after the cast", ctx =>
                    {
                        CharacterController m = ctx.Get("mage");
                        Fact(ctx, "cast.concentrating", m.Concentration != null && m.Concentration.IsConcentrating);
                        Fact(ctx, "cast.spell", m.Concentration != null ? m.Concentration.ConcentrationSpellName : "none");
                        Fact(ctx, "cast.effect", m.StatusEffectManager != null && m.StatusEffectManager.HasEffect(SpellNames.SILENT_IMAGE));
                        return true;
                    }),
                    Step.Assert("damaged", ctx =>
                    {
                        CharacterController m = ctx.Get("mage");
                        GameManager.Instance.DealDamage(m, 30, DamagePackets.Spell("Scenario damage", DamageType.Untyped));
                        Fact(ctx, "hit.concentrating", m.Concentration != null && m.Concentration.IsConcentrating);
                        Fact(ctx, "hit.effect", m.StatusEffectManager != null && m.StatusEffectManager.HasEffect(SpellNames.SILENT_IMAGE));
                        Fact(ctx, "hit.alive", !m.IsDead);
                        return true;
                    }))
                .Expect("The NPC cast of Silent Image is done", Expect.StepStatus("mage", 1, "Cast", 0, "done"))
                .Expect("The NPC's Silent Image starts its concentration (PHB p.176; SPL-114)",
                    FactsAre(("cast.concentrating", true), ("cast.spell", "Silent Image"), ("cast.effect", true)))
                .Expect("30 damage breaks the concentration (DC 41, PHB p.69-70) and the image ends; the mage lives",
                    FactsAre(("hit.concentrating", false), ("hit.effect", false), ("hit.alive", true)))
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
            /// <summary>A setup of the far anchor dummy, an enemy both casters see (an energy attack the AI's choice reads).</summary>
            public Action<CharacterController> AnchorTweak;
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
                // The caster picks the energy (PHB p.272): the Ui caster picks cold, and the AI picks the energy of the
                // visible enemy's breath, the anchor's cold (batch 11 step 22, SPL-112).
                AnchorTweak = c => c.ConfigureBreathWeapon(new BreathWeaponDefinition { DamageType = global::DamageType.Cold, DamageCount = 2, DamageDice = 6 }),
                SharedPins = b => b.Expect("Both casters resist cold: the Ui pick and the AI's pick against the visible cold breath (SPL-112)",
                    v =>
                    {
                        string ui = FactOf(v, "ui"), ai = FactOf(v, "scripted");
                        if (ui == null || ai == null)
                            return ExpectResult.Inconclusive("missing fact " + (ui == null ? "ui" : "scripted"));
                        return ui.Contains("resist=Cold:") && ai.Contains("resist=Cold:")
                            ? ExpectResult.Pass("both resist cold")
                            : ExpectResult.Fail("ui " + ui + "; scripted " + ai + ", expected cold resistance on both");
                    }) };
            yield return new PairSpec { Family = "magic-weapon", SpellId = SpellNames.MAGIC_WEAPON, Caster = wizard, SlotLevel = 1, Choices = new[] { "*" },
                Title = "Magic Weapon on the caster's weapon (PHB p.251)", Covers = new[] { "PHB p.251", "SPL-113" },
                // The PC prompt picks the only weapon; the AI picks the recipient's wielded weapon (batch 11 step 22, SPL-113).
                SharedPins = b => b.Expect("Both quarterstaffs carry Magic Weapon's +1 for 50 rounds (PHB p.251; SPL-113)",
                    PairTargets(t => t.Contains("items=Quarterstaff:magic_weapon:+1:50") ? null : "expected the quarterstaff enchanted", "L1:1/")) };
        }

        /// <summary>
        /// Paired casts of one spell (one per handler guard group of the PC cast chain, SPL-054) on identical setups: a Ui
        /// caster (the PC pipeline) and a Scripted one (the NPC cast executor), Stats-built with the same class, level and
        /// scores, each with the same target 15 ft away (5 ft for a touch spell; the caster itself for a personal-range
        /// buff), a far dummy keeping the fight going. Every d20 is 19 (touch attacks hit), every d4, d6 and d8 its maximum,
        /// and the target fails its saves (-30). After both casts the Scripted caster's Assert step records each side's end
        /// state: the caster's slots, and the target's hit points, conditions, spell effects with rounds left, Silence flag,
        /// Resist Energy entries and weapon spell effects. RAW the two must match (PC_NPC_PARITY, owner 2026-10-07); a
        /// family where they differ today is pinned as Known with the issue that explains it (the caster-choice families,
        /// Resist Energy and Magic Weapon, pass since batch 11 step 22 gives the AI its own choices, SPL-112 and SPL-113).
        /// The handler families (Silence, Sound Burst, Hold Animal, and Ghoul Touch,
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
            if (p.AnchorTweak != null)
                b = b.Tweak("anchor", p.AnchorTweak);
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

        /// <summary>Spends every prepared slot of <paramref name="c"/> except one holding each of <paramref name="keepSpellIds"/>.</summary>
        private static void SpendAllBut(CharacterController c, params string[] keepSpellIds)
        {
            if (keepSpellIds.Length == 1)
            {
                SpendAllBut(c, keepSpellIds[0]);
                return;
            }
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spell slots");
            var toKeep = new List<string>(keepSpellIds);
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (slot == null || slot.IsUsed)
                    continue;
                if (slot.PreparedSpell != null && toKeep.Remove(slot.PreparedSpell.SpellId))
                    continue;
                slot.IsUsed = true;
            }
            if (toKeep.Count > 0)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no unspent slot holding " + string.Join(", ", toKeep));
            sc.SyncPreparedSpellsFromSlots();
        }

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
        /// it out of the AI's spell candidates (AIService.ScoreCastableSpells): the wizard never casts it.
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

        // ── Batch 11 step 22: the AI's caster choices and ordered retries ──

        /// <summary>
        /// SPL-112 (PHB p.272: the caster picks the energy type). A Scripted wizard 5 (the NPC cast path and its AI decider)
        /// casts Resist Energy on itself with a dragon wyrmling in sight, 35 ft away and idle: the AI guards against the
        /// energy of the visible enemy's breath (AiCastChoiceHeuristics), fire against a red dragon, cold against a white
        /// one. CL 5 gives resistance 10.
        /// </summary>
        private static ScenarioDef NpcResistEnergyChoice(string id, string dragonId, string dragonName, string expected)
        {
            return Rules(id, "An NPC caster's Resist Energy guards against the " + dragonName + "'s visible breath: " + expected + " (SPL-112)")
                .Covers("SPL-112", "PHB p.272", "batch 11 step 22")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Scripted Wizard", 5)), 3, 10, Control.Scripted)
                .Npc("dragon", dragonId, 10, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 2, SpellNames.RESIST_ENERGY))
                .Initiative("wizard", "dragon")
                .Turn("wizard", 1,
                    Step.Cast(SpellNames.RESIST_ENERGY, "wizard"),
                    Step.Assert("record", ctx => { Fact(ctx, "resist", ResistEntries(ctx.Get("wizard"))); return true; }))
                .Expect("The Scripted cast ran", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("The AI resists " + expected + ", the " + dragonName + "'s breath (SPL-112)", FactIs("resist", expected + ":10"))
                .Build();
        }

        /// <summary>
        /// SPL-113 (PHB p.251: Magic Weapon enchants the weapon touched). An AI-run cleric 5 (the Healer profile) whose only
        /// unspent slot holds Magic Weapon stands beside a fighter ally; a dummy 60 ft away keeps the fight going. Nobody is
        /// hurt, so the cleric buffs: the cleric holds no weapon itself, so its only target is the fighter, and the AI's
        /// choice enchants the fighter's wielded weapon (+1 enhancement, 50 rounds at CL 5) where the NPC cast used to fizzle.
        /// </summary>
        private static ScenarioDef NpcMagicWeapon()
        {
            return Rules("rules/npc-magic-weapon", "An AI-run cleric casts Magic Weapon on its ally's wielded weapon (SPL-113)")
                .Covers("SPL-113", "PHB p.251", "batch 11 step 22")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => Cleric("AI Cleric", 5)), 3, 10, Control.Ai)
                .Pc("fighter", ActorSource.Stats(() => BardScenarios.Fighter("Fighter", 3)), 4, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 15, 10, Control.Idle)
                .Tweak("cleric", c =>
                {
                    Prepare(c, 1, SpellNames.MAGIC_WEAPON);
                    SpendAllBut(c, SpellNames.MAGIC_WEAPON);
                    global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
                    if (inv != null)
                    {
                        if (inv.RightHandSlot != null && inv.RightHandSlot.IsWeapon) inv.RightHandSlot = null;
                        if (inv.LeftHandSlot != null && inv.LeftHandSlot.IsWeapon) inv.LeftHandSlot = null;
                        if (inv.HandsSlot != null && inv.HandsSlot.IsWeapon) inv.HandsSlot = null;
                    }
                })
                .Initiative("cleric", "fighter", "dummy")
                .Turn("fighter", 1,
                    Step.Assert("record the fighter's weapon", ctx =>
                    {
                        ItemData w = ctx.Get("fighter").GetEquippedMainWeapon();
                        string effects = w != null && w.ActiveSpellEffects != null
                            ? string.Join(",", w.ActiveSpellEffects.Where(e => e != null).Select(e => e.SpellId + ":+" + e.EnhancementBonusAttack + ":" + e.RemainingRounds))
                            : "";
                        Fact(ctx, "fighter.weapon", w != null ? w.Name + "[" + effects + "]" : "none");
                        return true;
                    }))
                .Expect("The AI cleric casts Magic Weapon once", CastEvents("cleric", SpellNames.MAGIC_WEAPON, 1))
                .Expect("The fighter's wielded weapon carries Magic Weapon's +1 (PHB p.251; SPL-113)", v =>
                {
                    string fact = FactOf(v, "fighter.weapon");
                    if (fact == null)
                        return ExpectResult.Inconclusive("missing fact fighter.weapon");
                    return fact.Contains("[" + SpellNames.MAGIC_WEAPON + ":+1:") ? ExpectResult.Pass(fact) : ExpectResult.Fail(fact + ", expected the weapon enchanted");
                })
                .Build();
        }

        /// <summary>
        /// AI-063 (single-target part; batch 11 step 22). An AI-run wizard 5 (the Evoker profile) holds only Shocking Grasp
        /// (touch, PHB p.279) and Ray of Frost (close range, 35 ft at CL 5, PHB p.269); the only enemy is a wolf 20 ft away.
        /// The AI's best-scored spell is Shocking Grasp (recorded through AIService.SelectSpell, which tests no target or
        /// range), which has no legal target: the AI's legality filter (GameManager.CheckLegalityForAI: out of range) drops
        /// it before any cast is tried, so it costs no try, and the wizard casts its next candidate, Ray of Frost, where the
        /// old single pick found no target and gave up its spellcasting for the turn. A cast refused after it was tried is
        /// rules/ai-spell-retry-refused.
        /// </summary>
        private static ScenarioDef AiSpellRetry()
        {
            return Rules("rules/ai-spell-retry", "When its first spell pick is illegal the AI casts its second (AI-063)")
                .Covers("AI-063", "PHB p.279", "PHB p.269", "batch 11 step 22")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("AI Wizard", 5)), 3, 10, Control.Ai)
                .Npc("wolf", "wolf", 7, 10, Control.Scripted)
                .Tweak("wizard", c =>
                {
                    AddWizardSlot(c, 1, SpellNames.SHOCKING_GRASP);
                    AddWizardSlot(c, 0, SpellNames.RAY_OF_FROST);
                    SpendAllBut(c, SpellNames.SHOCKING_GRASP, SpellNames.RAY_OF_FROST);
                })
                .Tweak("wolf", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("wolf", "wizard")
                .Turn("wolf", 1,
                    Step.Assert("record the AI's first pick and its legality", ctx =>
                    {
                        CharacterController wizard = ctx.Get("wizard"), wolf = ctx.Get("wolf");
                        AIService ai = ctx.Gm.GetComponent<AIService>();
                        SpellData top = ai != null ? ai.SelectSpell(wizard, wolf) : null;
                        Fact(ctx, "top", top != null ? top.SpellId : "none");
                        Fact(ctx, "top.legal", top != null ? ctx.Gm.CheckLegalityForAI(wizard, wolf, top).Reason : "none");
                        return true;
                    }))
                .Expect("The AI's first pick is Shocking Grasp, refused against the wolf 20 ft away (out-of-range)", FactsAre(("top", SpellNames.SHOCKING_GRASP), ("top.legal", "out-of-range")))
                .Expect("The wizard casts Ray of Frost, its second candidate", CastEvents("wizard", SpellNames.RAY_OF_FROST, 1))
                .Expect("The wizard never casts Shocking Grasp", Expect.Count("cast", e => e.Str("by") == "wizard" && e.Str("spell") == SpellNames.SHOCKING_GRASP, 0, 0))
                .Build();
        }

        // The hook state of rules/ai-spell-retry-refused: the handler counts only (a hook handler emits no trace event).
        private static Action<CharacterController, SpellData, string, string> _retryRefusedPreviousHook;
        private static int _retryRefusedBegins;

        /// <summary>
        /// AI-063 (single-target part; batch 11 step 22 review): a cast the cast core refuses after the AI picked it passes
        /// the turn's spell to the next candidate, and the refused cast spends nothing. An AI-run cleric 5 (the Healer
        /// profile) holds only Magic Weapon (1st) and Light (0, which the AI scores below Magic Weapon); a fighter ally beside it wields a weapon, a dummy 55 ft
        /// away keeps the fight going, nobody is hurt. The AI's first pick is Magic Weapon on the fighter (recorded through
        /// AIService.SelectSpell on the fighter's turn, which also installs the test's hook). When that cast begins
        /// (CastStageTrace.Begin, raised before the cast core runs) the hook takes the weapon out of the fighter's hands, so
        /// the AI decider has no weapon to choose and refuses the cast (choice-refused, before the Cost stage): no 'cast'
        /// event, the Magic Weapon slot unspent, and the walk casts Light, its next candidate.
        /// </summary>
        private static ScenarioDef AiSpellRetryRefused()
        {
            return Rules("rules/ai-spell-retry-refused", "A cast refused after the AI picked it spends nothing and the AI casts its next candidate (AI-063)")
                .Covers("AI-063", "SPL-113", "batch 11 step 22")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => Cleric("AI Cleric", 5)), 3, 10, Control.Ai)
                .Pc("fighter", ActorSource.Stats(() => BardScenarios.Fighter("Fighter", 3)), 4, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 14, 10, Control.Scripted)
                .Tweak("cleric", c =>
                {
                    Prepare(c, 1, SpellNames.MAGIC_WEAPON);
                    Prepare(c, 0, SpellNames.LIGHT);
                    SpendAllBut(c, SpellNames.MAGIC_WEAPON, SpellNames.LIGHT);
                    global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
                    if (inv != null)
                    {
                        if (inv.RightHandSlot != null && inv.RightHandSlot.IsWeapon) inv.RightHandSlot = null;
                        if (inv.LeftHandSlot != null && inv.LeftHandSlot.IsWeapon) inv.LeftHandSlot = null;
                        if (inv.HandsSlot != null && inv.HandsSlot.IsWeapon) inv.HandsSlot = null;
                    }
                })
                .Initiative("fighter", "cleric", "dummy")
                .Turn("fighter", 1,
                    Step.Assert("record the first pick and disarm the fighter when its cast begins", ctx =>
                    {
                        CharacterController cleric = ctx.Get("cleric"), fighter = ctx.Get("fighter");
                        AIService ai = ctx.Gm.GetComponent<AIService>();
                        SpellData top = ai != null ? ai.SelectSpell(cleric, fighter) : null;
                        Fact(ctx, "top", top != null ? top.SpellId : "none");
                        Fact(ctx, "fighter.armed", fighter.GetEquippedMainWeapon() != null);
                        _retryRefusedBegins = 0;
                        Action<CharacterController, SpellData, string, string> previous = ScenarioHooks.CastStage;
                        _retryRefusedPreviousHook = previous;
                        ScenarioHooks.CastStage = ScenarioHooks.Safe<CharacterController, SpellData, string, string>("ai-spell-retry-refused", (c, s, stage, detail) =>
                        {
                            previous?.Invoke(c, s, stage, detail);
                            if (c != cleric || s == null || s.SpellId != SpellNames.MAGIC_WEAPON || stage != CastStageTrace.Begin)
                                return;
                            _retryRefusedBegins++;
                            global::Inventory inv = fighter.InventoryComp != null ? fighter.InventoryComp.CharacterInventory : null;
                            if (inv != null)
                            {
                                if (inv.RightHandSlot != null && inv.RightHandSlot.IsWeapon) inv.RightHandSlot = null;
                                if (inv.LeftHandSlot != null && inv.LeftHandSlot.IsWeapon) inv.LeftHandSlot = null;
                                if (inv.HandsSlot != null && inv.HandsSlot.IsWeapon) inv.HandsSlot = null;
                            }
                        });
                        Fact(ctx, "hook.previous", previous != null);
                        return true;
                    }))
                .Turn("dummy", 1,
                    Step.Assert("record the cleric's slots and restore the hook", ctx =>
                    {
                        ScenarioHooks.CastStage = _retryRefusedPreviousHook;
                        _retryRefusedPreviousHook = null;
                        Fact(ctx, "mw.begins", _retryRefusedBegins);
                        SpellcastingComponent sc = ctx.Get("cleric").Spellcasting;
                        int mwSlots = sc != null && sc.SpellSlots != null
                            ? sc.SpellSlots.Count(s => s != null && s.CanCast && s.PreparedSpell != null && s.PreparedSpell.SpellId == SpellNames.MAGIC_WEAPON)
                            : -1;
                        Fact(ctx, "mw.slots", mwSlots);
                        return true;
                    }))
                .Expect("The AI's first pick is Magic Weapon on the armed fighter", FactsAre(("top", SpellNames.MAGIC_WEAPON), ("fighter.armed", true), ("hook.previous", true)))
                .Expect("Magic Weapon's cast began once and was refused: no 'cast' event, its slot unspent (nothing spent)",
                    Expect.All(FactIs("mw.begins", 1), FactIs("mw.slots", 1), CastEvents("cleric", SpellNames.MAGIC_WEAPON, 0)))
                .Expect("The walk casts the next candidate, Light", CastEvents("cleric", SpellNames.LIGHT, 1))
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

        // ── rules/cast-quickened-threatened (SPL-143, closed) ──────────

        /// <summary>
        /// PHB p.98 (Quicken Spell: casting a quickened spell does not provoke attacks of opportunity). A Ui wizard 9 with
        /// Quicken Spell, a goblin beside it (-30 on attacks, 100 more hit points; first in initiative, so it is no longer
        /// flat-footed and threatens, PHB p.137), casts a quickened Magic Missile (an added 5th-level slot, SPL-041; every
        /// d4 is 4) at the goblin. Magic Missile makes no attack roll, so the only possible provocation is the casting
        /// itself (a ray's ranged touch attack would mix in a second question). The quickened cast is a free action and
        /// leaves the turn's other actions (PHB p.98). Since batch 11 step 12 the cast core's Provocation stage lets a
        /// quickened cast through: no AoO confirmation, no threatened cast, no attack of opportunity (SPL-143, which this
        /// scenario pinned from batch 11 step 3).
        /// </summary>
        private static ScenarioDef QuickenedThreatened()
        {
            var quicken = new MetamagicData();
            quicken.Toggle(MetamagicFeatId.QuickenSpell);
            return Rules("rules/cast-quickened-threatened", "A quickened spell cast while threatened does not provoke (PHB p.98)")
                .Covers("SPL-143", "PHB p.98", "batch 11 step 12")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 9, "Quicken Spell")), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 5, SpellNames.MAGIC_MISSILE, quicken))
                .Tweak("gob", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("gob", "wizard")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob", quicken),
                    Step.Assert("after", ctx => { Fact(ctx, "actions", Actions(ctx.Get("wizard"))); FactSlotsSpent(ctx, "spent", "wizard"); return true; }))
                .Expect("The quickened cast spends a 5th-level slot and keeps the turn's other actions (PHB p.98)",
                    FactsAre(("spent", "L5+1"), ("actions", "smf")))
                .Expect("The quickened cast opens no AoO confirmation, is no threatened cast and draws no attack of opportunity (PHB p.98)", Expect.All(
                    Expect.None("threatened_cast", e => e.Str("by") == "wizard"),
                    Expect.None("aoo", e => e.Str("by") == "gob" && e.Str("target") == "wizard")))
                .Build();
        }

        // ── rules/quicken-once-per-round (batch 11 step 12) ─────────────

        /// <summary>
        /// PHB p.98 (Quicken Spell: only one quickened spell per round) and PHB p.229 (Feather Fall: a free action, like a
        /// quickened spell, that counts toward that limit), one rule for a Ui and a Scripted caster (CastActionRules, the
        /// cast core's Legality stage; batch 11 step 12). The Ui wizard 9 (Quicken Spell; two added 5th-level slots with
        /// Magic Missile prepared quickened, SPL-041; a Feather Fall) casts a quickened Magic Missile at a distant goblin in round 1: a free
        /// action, so every other action is left. A second quickened Magic Missile and a Feather Fall the same round are
        /// refused at the spell menu with a log line and spend nothing. The Scripted wizard 3 casts Feather Fall (two
        /// prepared) on the Ui wizard: a free action through the NPC cast path; the second the same round is refused (the
        /// Scripted wizard has no Quicken Spell, so Feather Fall is how it meets the limit). In round 2 the
        /// limit is reset and each casts again.
        /// </summary>
        private static ScenarioDef QuickenOncePerRound()
        {
            var quicken = new MetamagicData();
            quicken.Toggle(MetamagicFeatId.QuickenSpell);
            const string refusedLog = "already cast a quickened spell this round";
            return Rules("rules/quicken-once-per-round", "One quickened spell per round, Feather Fall included, for a Ui and a Scripted caster (PHB p.98, p.229)")
                .Covers("PHB p.98", "PHB p.229", "SPL-143", "batch 11 step 12")
                .MaxRounds(2)
                .Pc("ui", ActorSource.Stats(() => Wizard("Quicken Ui Wizard", 9, "Quicken Spell")), 3, 10, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Wizard("Quicken Scripted Wizard", 3)), 5, 10, Control.Scripted)
                .Npc("gob", "goblin", 14, 10, Control.Idle)
                .Tweak("ui", c => { AddWizardSlot(c, 5, SpellNames.MAGIC_MISSILE, quicken); AddWizardSlot(c, 5, SpellNames.MAGIC_MISSILE, quicken); Prepare(c, 1, SpellNames.FEATHER_FALL); })
                .Tweak("scripted", c => Prepare(c, 1, SpellNames.FEATHER_FALL, 2))
                .Tweak("gob", Sturdy)
                .Initiative("ui", "scripted", "gob")
                .Force(4, 4, null, -1)
                .Turn("ui", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "ui"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob", quicken),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob", quicken),
                    Step.Cast(SpellNames.FEATHER_FALL, "scripted"),
                    Step.Assert("after", ctx =>
                    {
                        FactSlotsSpent(ctx, "ui1.spent", "ui");
                        Fact(ctx, "ui1.actions", Actions(ctx.Get("ui")));
                        return true;
                    }))
                .Turn("scripted", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "scripted"); return true; }),
                    Step.Cast(SpellNames.FEATHER_FALL, "ui"),
                    Step.Cast(SpellNames.FEATHER_FALL, "ui"),
                    Step.Assert("after", ctx =>
                    {
                        FactSlotsSpent(ctx, "sc1.spent", "scripted");
                        Fact(ctx, "sc1.actions", Actions(ctx.Get("scripted")));
                        return true;
                    }))
                .Turn("ui", 2,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "ui"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob", quicken),
                    Step.Assert("after", ctx => { FactSlotsSpent(ctx, "ui2.spent", "ui"); return true; }))
                .Turn("scripted", 2,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "scripted"); return true; }),
                    Step.Cast(SpellNames.FEATHER_FALL, "ui"),
                    Step.Assert("after", ctx => { FactSlotsSpent(ctx, "sc2.spent", "scripted"); return true; }))
                .Expect("Round 1, Ui: one quickened Magic Missile spends one 5th-level slot and no action; the second quickened spell and Feather Fall spend nothing (PHB p.98, p.229)",
                    FactsAre(("ui1.spent", "L5+1"), ("ui1.actions", "smf")))
                .Expect("Round 1, Ui: the second quickened spell and the Feather Fall are refused with a log line", Expect.Count("log", e => (e.Str("text") ?? "").Contains(refusedLog), 2, 2))
                .Expect("Round 1, Scripted: the first Feather Fall is cast as a free action, the second is refused (PHB p.229)", Expect.All(
                    Expect.StepStatus("scripted", 1, "Cast", 0, "done"),
                    Expect.StepStatus("scripted", 1, "Cast", 1, "refused"),
                    FactsAre(("sc1.spent", "L1+1"), ("sc1.actions", "smf"))))
                .Expect("Round 2: the limit is reset, each casts its quickened or free-action spell again", Expect.All(
                    FactIs("ui2.spent", "L5+1"),
                    Expect.StepStatus("scripted", 2, "Cast", 0, "done"),
                    FactIs("sc2.spent", "L1+1")))
                .Expect("Two quickened Magic Missiles and two Feather Falls are cast in all (ScenarioHooks.SpellCast)", Expect.All(
                    CastEvents("ui", SpellNames.MAGIC_MISSILE, 2), CastEvents("ui", SpellNames.FEATHER_FALL, 0),
                    CastEvents("scripted", SpellNames.FEATHER_FALL, 2)))
                .Expect("No cast is a threatened cast (the goblin is out of reach here; a threatened free-action cast is rules/cast-free-action-threatened)",
                    Expect.None("threatened_cast", e => true))
                .Build();
        }

        // ── rules/cast-free-action-threatened (batch 11 step 12) ────────

        /// <summary>
        /// PHB p.174 (Casting Time: casting a spell whose casting time is 1 free action does not provoke attacks of
        /// opportunity) for a Ui and a Scripted caster (CastActionRules.CountsAsQuickenedSpell, the cast core's Provocation
        /// stage; batch 11 step 12). A Ui wizard 3 and a Scripted wizard 3 stand on either side of a goblin that is first in
        /// initiative (so it threatens, PHB p.137; -30 on attacks, 100 more hit points); each casts Feather Fall (PHB p.229)
        /// on the other. Neither cast opens the AoO prompt, is a threatened cast or draws an attack of opportunity; each
        /// spends its 1st-level slot and no action.
        /// </summary>
        private static ScenarioDef FreeActionThreatened()
        {
            return Rules("rules/cast-free-action-threatened", "A free-action spell cast while threatened does not provoke, for a Ui and a Scripted caster (PHB p.174)")
                .Covers("PHB p.174", "PHB p.229", "batch 11 step 12")
                .MaxRounds(1)
                .Pc("ui", ActorSource.Stats(() => Wizard("Free Ui Wizard", 3)), 5, 10, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Wizard("Free Scripted Wizard", 3)), 7, 10, Control.Scripted)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("ui", c => { Prepare(c, 1, SpellNames.FEATHER_FALL); Sturdy(c); })
                .Tweak("scripted", c => { Prepare(c, 1, SpellNames.FEATHER_FALL); Sturdy(c); })
                .Tweak("gob", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("gob", "ui", "scripted")
                .Turn("ui", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "ui"); return true; }),
                    Step.Cast(SpellNames.FEATHER_FALL, "scripted"),
                    Step.Assert("after", ctx => { FactSlotsSpent(ctx, "ui.spent", "ui"); Fact(ctx, "ui.actions", Actions(ctx.Get("ui"))); return true; }))
                .Turn("scripted", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "scripted"); return true; }),
                    Step.Cast(SpellNames.FEATHER_FALL, "ui"),
                    Step.Assert("after", ctx => { FactSlotsSpent(ctx, "sc.spent", "scripted"); Fact(ctx, "sc.actions", Actions(ctx.Get("scripted"))); return true; }))
                .Expect("Both Feather Falls are cast, each a free action from a 1st-level slot (PHB p.174, p.229)", Expect.All(
                    CastEvents("ui", SpellNames.FEATHER_FALL, 1), CastEvents("scripted", SpellNames.FEATHER_FALL, 1),
                    Expect.StepStatus("scripted", 1, "Cast", 0, "done"),
                    FactsAre(("ui.spent", "L1+1"), ("ui.actions", "smf"), ("sc.spent", "L1+1"), ("sc.actions", "smf"))))
                .Expect("Neither cast is a threatened cast or draws an attack of opportunity (PHB p.174)", Expect.All(
                    Expect.None("threatened_cast", e => true),
                    Expect.None("aoo", e => e.Str("by") == "gob")))
                .Build();
        }

        // ── rules/cast-full-round-after-move (batch 11 step 12) ─────────

        /// <summary>
        /// PHB p.174 (a 1-round casting time is a full-round action) and PHB p.138 (a full-round action takes the whole
        /// round, so not after a move action), for a Ui and a Scripted caster (CastActionRules.Refusal, read by the PC spell
        /// menu, the cast core's Legality stage and the AI's spell choice; batch 11 step 12). A Ui wizard 1 and a Scripted
        /// wizard 1, Sleep prepared (casting time 1 round, PHB p.280), each move one square and then try Sleep at a distant
        /// goblin: the Ui menu refuses it with a log line, the Scripted cast is refused by the Legality stage (silently, as
        /// the NPC path's refusals are); neither spends the slot and each keeps its standard action.
        /// </summary>
        private static ScenarioDef FullRoundAfterMove()
        {
            const string refusedLog = "casting it takes a full-round action";
            return Rules("rules/cast-full-round-after-move", "A 1-round casting time is refused after a move, for a Ui and a Scripted caster (PHB p.174, p.138)")
                .Covers("PHB p.174", "PHB p.138", "PHB p.280", "SPL-162", "batch 11 step 12")
                .MaxRounds(1)
                .Pc("ui", ActorSource.Stats(() => Wizard("Move Ui Wizard", 1)), 2, 10, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Wizard("Move Scripted Wizard", 1)), 2, 14, Control.Scripted)
                .Npc("g1", "goblin", 12, 10, Control.Idle)
                .Npc("g2", "goblin", 12, 14, Control.Idle)
                .Tweak("ui", c => Prepare(c, 1, SpellNames.SLEEP))
                .Tweak("scripted", c => Prepare(c, 1, SpellNames.SLEEP))
                .Initiative("ui", "scripted", "g1", "g2")
                .Turn("ui", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "ui"); return true; }),
                    Step.Move(3, 10),
                    Step.Cast(SpellNames.SLEEP, "g1"),
                    Step.Assert("after", ctx => { FactSlotsSpent(ctx, "ui.spent", "ui"); Fact(ctx, "ui.actions", Actions(ctx.Get("ui"))); return true; }))
                .Turn("scripted", 1,
                    Step.Assert("record", ctx =>
                    {
                        RecordSlots(ctx, "scripted");
                        CharacterController c = ctx.Get("scripted");
                        SpellData sleep = SpellDatabase.GetSpell(SpellNames.SLEEP);
                        Fact(ctx, "sc.rule.before", CastActionRules.Refusal(c, sleep, null, SpellCastRequest.AiSourceFor(c, sleep)) ?? "none");
                        return true;
                    }),
                    Step.Move(3, 14),
                    Step.Assert("rule", ctx =>
                    {
                        CharacterController c = ctx.Get("scripted");
                        SpellData sleep = SpellDatabase.GetSpell(SpellNames.SLEEP);
                        Fact(ctx, "sc.rule.after", CastActionRules.Refusal(c, sleep, null, SpellCastRequest.AiSourceFor(c, sleep)) ?? "none");
                        return true;
                    }),
                    Step.Cast(SpellNames.SLEEP, "g2"),
                    Step.Assert("after", ctx => { FactSlotsSpent(ctx, "sc.spent", "scripted"); Fact(ctx, "sc.actions", Actions(ctx.Get("scripted"))); return true; }))
                .Expect("Both casters moved", Expect.All(Expect.StepStatus("ui", 1, "Move", 0, "done"), Expect.StepStatus("scripted", 1, "Move", 0, "done")))
                .Expect("Ui: the spell menu refuses Sleep after the move with a log line; nothing is spent and the standard action is left (PHB p.174)", Expect.All(
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains(refusedLog), 1, 1),
                    FactsAre(("ui.spent", "none"), ("ui.actions", "s--"))))
                .Expect("Scripted: the rule allows Sleep before the move and refuses it after; the cast is refused, nothing is spent and the standard action is left (PHB p.174)", Expect.All(
                    FactsAre(("sc.rule.before", "none"), ("sc.rule.after", CastActionRules.RefusalNoFullRoundAction)),
                    Expect.StepStatus("scripted", 1, "Cast", 0, "refused"),
                    FactsAre(("sc.spent", "none"), ("sc.actions", "s--"))))
                .Expect("No Sleep is cast", Expect.All(CastEvents("ui", SpellNames.SLEEP, 0), CastEvents("scripted", SpellNames.SLEEP, 0)))
                .Build();
        }

        // ── rules/cast-aoo-cancel-keeps-concentration (SPL-152, batch 11 step 12) ──

        /// <summary>
        /// SPL-152, fixed in batch 11 step 12: the casting-while-concentrating step runs only once the AoO prompt lets the
        /// cast proceed. A Ui wizard 3 concentrating on Silent Image (a concentration spell, PHB p.279; set up directly) casts
        /// Magic Missile while threatened by a goblin first in initiative (-30 on attacks; both 100 more hit points) and
        /// answers Cancel: the cast spends nothing, the wizard still concentrates on Silent Image, and nothing is rolled. It
        /// then casts Magic Missile again and answers Proceed: since batch 11 step 16 (decision D1, PHB p.176) that cast ends
        /// the concentration before its attack of opportunity, with no roll, and the end is logged once.
        /// </summary>
        private static ScenarioDef AoOCancelKeepsConcentration()
        {
            const string castingCheck = "(casting a spell)";
            const string ended = "stops concentrating on Silent Image (to cast Magic Missile)";
            return Rules("rules/cast-aoo-cancel-keeps-concentration", "A cast cancelled at the AoO prompt rolls no concentration check and keeps the maintained spell (SPL-152)")
                .Covers("SPL-152", "PHB p.140", "batch 11 step 12")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Conc Ui Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.MAGIC_MISSILE, 2); Sturdy(c); })
                .Tweak("gob", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("gob", "wizard")
                .Turn("wizard", 1,
                    Step.Assert("concentrate", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        SpellData image = SpellDatabase.GetSpell(SpellNames.SILENT_IMAGE).Clone();
                        w.Concentration.BeginConcentration(new ActiveSpellEffect(image, w.Stats.CharacterName, 3, w.Stats.CharacterName));
                        RecordSlots(ctx, "wizard");
                        return w.Concentration.IsConcentrating;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob"),
                    Step.AnswerAoO(AoOAnswer.Cancel),
                    Step.Assert("after the cancel", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        FactSlotsSpent(ctx, "cancel.spent", "wizard");
                        Fact(ctx, "cancel.actions", Actions(w));
                        Fact(ctx, "cancel.concentrating", w.Concentration.ConcentrationSpellName);
                        return true;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob"),
                    Step.AnswerAoO(AoOAnswer.Proceed))
                .Expect("The first cast reached the AoO prompt and was cancelled", Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"))
                .Expect("The cancelled cast spends nothing and the wizard still concentrates on Silent Image (SPL-152)",
                    FactsAre(("cancel.spent", "none"), ("cancel.actions", "smf"), ("cancel.concentrating", "Silent Image")))
                .Expect("The cast that went on ended the concentration once, with no casting-while-concentrating check (SPL-152; decision D1)", Expect.All(
                    Expect.StepStatus("wizard", 1, "AnswerAoO(Proceed)", 0, "done"),
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains(castingCheck), 0, 0),
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains(ended), 1, 1)))
                .Build();
        }

        // ── rules/concentration-* (batch 11 step 16: decisions D1 and D2, SPL-153) ──

        /// <summary>The concentration facts of <paramref name="key"/>: tag.concentrating, tag.spell and tag.effect (the image on it).</summary>
        private static Step FactConcentration(string tag, string key, string spellId)
            => Step.Assert(tag, ctx =>
            {
                CharacterController c = ctx.Get(key);
                ConcentrationManager conc = c.Concentration;
                Fact(ctx, tag + ".concentrating", conc != null && conc.IsConcentrating);
                Fact(ctx, tag + ".maintained", conc != null && conc.MaintainedThisTurn);
                Fact(ctx, tag + ".effect", c.StatusEffectManager != null && c.StatusEffectManager.HasEffect(spellId));
                Fact(ctx, tag + ".actions", Actions(c));
                return true;
            });

        /// <summary>A log line that rolls a Concentration check on the maintained Silent Image (none may appear: D1 has no roll).</summary>
        private static bool SilentImageCheckLine(TraceEvent e) => (e.Str("text") ?? "").Contains("Concentration check (Silent Image)");

        /// <summary>
        /// Decision D1 and D2 (PHB p.176), for a Ui wizard 3 and a Scripted enemy wizard 3: round 1 the caster casts Silent
        /// Image (Concentration, PHB p.279); round 2 it spends its standard action concentrating (the Maintain step: the
        /// PC's Maintain button, the NPC's TryMaintainConcentrationForAI), so the image is still there at its round-3 turn;
        /// round 3 it casts Magic Missile at a foe, which first ends the concentration as a free action with no roll: the
        /// image is gone and no Concentration check on it was rolled.
        /// </summary>
        private static ScenarioDef CastEndsImage(bool ui)
        {
            string id = ui ? "rules/concentration-cast-ends-image" : "rules/concentration-cast-ends-image-npc";
            string caster = ui ? "wizard" : "mage";
            string foe = ui ? "gob" : "hero";
            ScenarioBuilder b = Rules(id, (ui ? "A Ui" : "A Scripted NPC") + " caster maintains Silent Image, then casts Magic Missile: the image ends with no roll (PHB p.176)")
                .Covers("SPL-153", "PHB p.176", "PHB p.279", "decision D1", "decision D2", "batch 11 step 16")
                .MaxRounds(3);
            if (ui)
                b = b.Pc("wizard", ActorSource.Stats(() => Wizard("Image Wizard", 3)), 5, 10, Control.Ui)
                    .Npc("gob", "goblin", 12, 10, Control.Idle)
                    .Tweak("wizard", c => { AddWizardSlot(c, 1, SpellNames.SILENT_IMAGE); AddWizardSlot(c, 1, SpellNames.MAGIC_MISSILE); Sturdy(c); })
                    .Tweak("gob", Sturdy)
                    .Initiative("wizard", "gob");
            else
                b = b.Pc("hero", ActorSource.Stats(() => Wizard("Image Target", 3)), 5, 10, Control.Idle)
                    .Npc("mage", "neutral_mage_test", 12, 10, Control.Scripted)
                    .Tweak("mage", c => { AddWizardSlot(c, 1, SpellNames.SILENT_IMAGE); AddWizardSlot(c, 1, SpellNames.MAGIC_MISSILE); Sturdy(c); })
                    .Tweak("hero", Sturdy)
                    .Initiative("mage", "hero");
            return b
                .Turn(caster, 1,
                    Step.Cast(SpellNames.SILENT_IMAGE, caster),
                    FactConcentration("r1", caster, SpellNames.SILENT_IMAGE))
                .Turn(caster, 2,
                    FactConcentration("r2.start", caster, SpellNames.SILENT_IMAGE),
                    Step.MaintainConcentration(),
                    FactConcentration("r2", caster, SpellNames.SILENT_IMAGE))
                .Turn(caster, 3,
                    FactConcentration("r3.start", caster, SpellNames.SILENT_IMAGE),
                    Step.Cast(SpellNames.MAGIC_MISSILE, foe),
                    FactConcentration("r3", caster, SpellNames.SILENT_IMAGE))
                .Expect("Round 1: the cast starts the concentration; the casting turn counts as kept (PHB p.176)",
                    FactsAre(("r1.concentrating", true), ("r1.maintained", true), ("r1.effect", true)))
                .Expect("Round 2: the new turn owes the concentration, and the Maintain step spends the standard action (PHB p.176, Table 8-2)",
                    FactsAre(("r2.start.concentrating", true), ("r2.start.maintained", false), ("r2.maintained", true), ("r2.actions", "-m-")))
                .Expect("The maintain step is done", Expect.StepStatus(caster, 2, "MaintainConcentration", 0, "done"))
                .Expect("Round 3: the image kept up in round 2 is still there when the turn starts", FactsAre(("r3.start.concentrating", true), ("r3.start.effect", true)))
                .Expect("Casting Magic Missile ends the concentration and the image (decision D1, PHB p.176)",
                    FactsAre(("r3.concentrating", false), ("r3.effect", false)))
                .Expect("The cast is in the trace once", CastEvents(caster, SpellNames.MAGIC_MISSILE, 1))
                .Expect("No Concentration check is rolled for the image (D1: no roll)", Expect.Count("log", SilentImageCheckLine, 0, 0))
                .Expect("The end is logged once: the caster stops concentrating to cast Magic Missile",
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("stops concentrating on Silent Image (to cast Magic Missile)"), 1, 1))
                .Build();
        }

        /// <summary>
        /// Decision D2 (PHB p.176): a caster who spends its standard action on something else loses the spell at the end
        /// of that turn (delegated RAW reading 2026-10-15, the CHR-026 timing). A Ui wizard 3 and a Scripted enemy wizard 3
        /// cast Silent Image in round 1; in round 2 each moves next to a foe and attacks it (its standard action): the image
        /// is still there after the attack, the caster can no longer maintain it, and at its round-3 turn it is gone, with
        /// the lapse logged once.
        /// </summary>
        private static ScenarioDef LapseUnmaintained(bool ui)
        {
            string id = ui ? "rules/concentration-lapse-unmaintained" : "rules/concentration-lapse-unmaintained-npc";
            string caster = ui ? "wizard" : "mage";
            string foe = ui ? "gob" : "hero";
            ScenarioBuilder b = Rules(id, (ui ? "A Ui" : "A Scripted NPC") + " caster who attacks instead of concentrating loses Silent Image at the end of its turn (PHB p.176)")
                .Covers("SPL-153", "PHB p.176", "PHB p.279", "decision D2", "batch 11 step 16")
                .MaxRounds(3);
            if (ui)
                b = b.Pc("wizard", ActorSource.Stats(() => Wizard("Lapse Wizard", 3)), 5, 10, Control.Ui)
                    .Npc("gob", "goblin", 8, 10, Control.Idle)
                    .Tweak("wizard", c => { AddWizardSlot(c, 1, SpellNames.SILENT_IMAGE); Sturdy(c); })
                    .Tweak("gob", Sturdy)
                    .Initiative("wizard", "gob");
            else
                b = b.Pc("hero", ActorSource.Stats(() => Wizard("Lapse Target", 3)), 8, 10, Control.Idle)
                    .Npc("mage", "neutral_mage_test", 5, 10, Control.Scripted)
                    .Tweak("mage", c => { AddWizardSlot(c, 1, SpellNames.SILENT_IMAGE); Sturdy(c); })
                    .Tweak("hero", Sturdy)
                    .Initiative("mage", "hero");
            return b
                .Turn(caster, 1,
                    Step.Cast(SpellNames.SILENT_IMAGE, caster),
                    FactConcentration("r1", caster, SpellNames.SILENT_IMAGE))
                .Turn(caster, 2,
                    Step.Move(7, 10),
                    Step.Attack(foe),
                    FactConcentration("r2", caster, SpellNames.SILENT_IMAGE),
                    Step.Assert("can maintain", ctx => { Fact(ctx, "r2.canMaintain", ConcentrationRules.CanMaintain(ctx.Get(caster), out _)); return true; }))
                .Turn(caster, 3,
                    FactConcentration("r3.start", caster, SpellNames.SILENT_IMAGE))
                .Expect("Round 1: the cast starts the concentration", FactsAre(("r1.concentrating", true), ("r1.effect", true)))
                .Expect("Round 2: after the attack the image is still up, but the standard action is spent, so it cannot be kept",
                    FactsAre(("r2.concentrating", true), ("r2.effect", true), ("r2.maintained", false), ("r2.canMaintain", false)))
                .Expect("The attack step is done", Expect.StepStatus(caster, 2, "Attack(" + foe + ")", 0, "done"))
                .Expect("Round 3: the image ended at the end of round 2's turn (PHB p.176; delegated end-of-turn timing)",
                    FactsAre(("r3.start.concentrating", false), ("r3.start.effect", false)))
                .Expect("The lapse is logged once",
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("stops concentrating on Silent Image (spent no standard action concentrating on it this turn)"), 1, 1))
                .Expect("No Concentration check is rolled for the image", Expect.Count("log", SilentImageCheckLine, 0, 0))
                .Build();
        }

        /// <summary>Marks every prepared slot of <paramref name="c"/> used (a caster with nothing left to cast).</summary>
        private static void SpendAll(CharacterController c)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spell slots");
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null)
                    slot.IsUsed = true;
            sc.SyncPreparedSpellsFromSlots();
        }

        /// <summary>Starts <paramref name="c"/>'s concentration on a Silent Image effect on itself (set up before the fight).</summary>
        private static void ConcentrateOnImage(CharacterController c)
        {
            ActiveSpellEffect image = c.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.SILENT_IMAGE).Clone(), c.Stats.CharacterName, 3);
            c.Concentration.BeginConcentration(image);
        }

        /// <summary>
        /// Decision D2 for the AI (AIProfile.DefaultShouldMaintainConcentration, an AI choice): two AI-run enemy wizards 3
        /// start the fight concentrating on Silent Image (set up directly; the AI does not cast the placeholder spell). The
        /// first has no spell left, no foe beside it and no ranged weapon, so keeping the image is worth more than its other
        /// standard action and it maintains it in rounds 1 and 2; the second has Magic Missile, which is worth more, so it
        /// casts it, which ends its image (decision D1). A Scripted hero, last in initiative, records both each round.
        /// </summary>
        private static ScenarioDef AiMaintains()
        {
            return Rules("rules/concentration-ai-maintains", "An AI caster keeps its Silent Image when it has nothing better and drops it for Magic Missile (PHB p.176)")
                .Covers("SPL-153", "PHB p.176", "decision D1", "decision D2", "batch 11 step 16")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => Wizard("AI Watcher", 3)), 3, 10, Control.Scripted)
                .Npc("keeper", "neutral_mage_test", 14, 4, Control.Ai)
                .Npc("caster", "neutral_mage_test", 14, 16, Control.Ai)
                .Tweak("hero", Sturdy)
                .Tweak("keeper", c => { SpendAll(c); Sturdy(c); ConcentrateOnImage(c); })
                .Tweak("caster", c => { SpendAll(c); AddWizardSlot(c, 1, SpellNames.MAGIC_MISSILE); Sturdy(c); ConcentrateOnImage(c); })
                .Initiative("keeper", "caster", "hero")
                .Turn("hero", 1,
                    Step.Assert("ranged", ctx => { Fact(ctx, "keeper.ranged", ctx.Get("keeper").HasRangedWeaponEquipped()); return true; }),
                    FactConcentration("r1.keeper", "keeper", SpellNames.SILENT_IMAGE),
                    FactConcentration("r1.caster", "caster", SpellNames.SILENT_IMAGE))
                .Turn("hero", 2,
                    FactConcentration("r2.keeper", "keeper", SpellNames.SILENT_IMAGE))
                .Expect("The keeper has no ranged weapon (fixture)", FactIs("keeper.ranged", false))
                .Expect("The keeper spent its round-1 standard action concentrating, and its image is up (AI choice; PHB p.176)",
                    FactsAre(("r1.keeper.concentrating", true), ("r1.keeper.maintained", true), ("r1.keeper.effect", true)))
                .Expect("The keeper kept it up again in round 2", FactsAre(("r2.keeper.concentrating", true), ("r2.keeper.maintained", true), ("r2.keeper.effect", true)))
                .Expect("The caster with Magic Missile cast it, and its image ended (decision D1)",
                    Expect.All(CastEvents("caster", SpellNames.MAGIC_MISSILE, 1), FactsAre(("r1.caster.concentrating", false), ("r1.caster.effect", false))))
                .Expect("No Concentration check is rolled for an image", Expect.Count("log", SilentImageCheckLine, 0, 0))
                .Build();
        }

        // ── rules/cast-empowered-fireball (SPL-038) ─────────────────────

        /// <summary>
        /// SPL-038 (PHB p.93, Empower Spell: all variable numeric effects are increased by one-half). A Ui wizard 9 with
        /// Empower Spell casts an empowered Fireball (from an added 5th-level slot, SPL-041) at a dummy that fails its save; every d6 is 3, so
        /// 9d6 = 27 and empowered 27 + 13 = 40 (half rounded down, PHB p.304). Fixed in batch 11 step 19: the cast's clone carries
        /// its metamagic (SpellCaster.ApplyMetamagicToSpellData) and Fireball's custom handler (TryResolveScaledAoEDamageSpell)
        /// rolls through SpellDiceRules.Roll with it; before, it rolled plain dice and the dummy lost 27.
        /// </summary>
        private static ScenarioDef EmpoweredFireball()
        {
            var empower = new MetamagicData();
            empower.Toggle(MetamagicFeatId.EmpowerSpell);
            return Rules("rules/cast-empowered-fireball", "An empowered Fireball deals one and a half times its damage (PHB p.93; SPL-038)")
                .Covers("SPL-038", "PHB p.93", "PHB p.231", "batch 11 step 19")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 9, "Empower Spell")), 2, 10, Control.Ui)
                .Npc("d1", "target_dummy", 10, 10, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 5, SpellNames.FIREBALL, empower))
                .Tweak("d1", Fails)
                .Initiative("wizard", "d1")
                .Force(6, 3, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d1"); RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.FIREBALL, "d1", empower),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "fireball", "d1"); FactSlotsSpent(ctx, "spent", "wizard"); return true; }))
                .Expect("The empowered cast ran and spent a 5th-level slot (PHB p.93: +2 levels)", Expect.All(
                    Expect.StepStatus("wizard", 1, "Cast", 0, "done"), FactIs("spent", "L5+1")))
                .Expect("The empowered Fireball deals 9d6 = 27 plus half, 40 (PHB p.93)", FactIs("fireball.lost.d1", 40))
                .Build();
        }

        // ── rules/cast-globe-heightened (SPL-141) ───────────────────────

        /// <summary>
        /// SPL-141's Heighten part, fixed in batch 11 step 19 (PHB p.95: a heightened spell's ability to penetrate a lesser
        /// globe of invulnerability is calculated at its heightened level; p.236: the lesser globe stops spells of 3rd level
        /// and lower). The warded dummy raises a Lesser Globe of Invulnerability on itself in round 1. The Ui wizard 7 with
        /// Heighten Spell casts a plain Magic Missile at it in round 1 (stopped: 1st level) and in round 2 one heightened to
        /// 4th from an added 4th-level slot, which the globe no longer stops: every d4 is 4, so four missiles of 1d4+1 deal 20.
        /// Before, LesserGlobeOfInvulnerabilityAreaEffect compared SpellData.SpellLevel, which Heighten never changes.
        /// </summary>
        private static ScenarioDef GlobeHeightened()
        {
            var heighten = new MetamagicData();
            heighten.Toggle(MetamagicFeatId.HeightenSpell);
            heighten.HeightenToLevel = 4;
            return Rules("rules/cast-globe-heightened", "A Magic Missile heightened to 4th passes a lesser globe of invulnerability (PHB p.95, p.236; SPL-141)")
                .Covers("SPL-141", "PHB p.95", "PHB p.236", "PHB p.251", "batch 11 step 19")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Heighten Wizard", 7, "Heighten Spell")), 2, 10, Control.Ui)
                .Npc("warded", "target_dummy", 8, 10, Control.Scripted)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.MAGIC_MISSILE); AddWizardSlot(c, 4, SpellNames.MAGIC_MISSILE, heighten); })
                .Tweak("warded", Sturdy)
                .Initiative("warded", "wizard")
                .Force(4, 4, null, -1)
                .Turn("warded", 1, RaiseLesserGlobe("warded"))
                .Turn("warded", 2, Step.Pass())
                .Turn("wizard", 1,
                    Step.Assert("record", ctx =>
                    {
                        RecordHp(ctx, "warded");
                        SpellData plain = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
                        SpellData heightened = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
                        SpellCaster.ApplyMetamagicToSpellData(heightened, heighten);
                        Fact(ctx, "blocks.plain", LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(plain, ctx.Get("warded")));
                        Fact(ctx, "blocks.heightened", LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(heightened, ctx.Get("warded")));
                        return true;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "warded"),
                    Step.Assert("after plain", ctx => { FactHpLost(ctx, "plain", "warded"); RecordHp(ctx, "warded"); return true; }))
                .Turn("wizard", 2,
                    Step.Cast(SpellNames.MAGIC_MISSILE, "warded", heighten),
                    Step.Assert("after heightened", ctx => { FactHpLost(ctx, "heightened", "warded"); return true; }))
                .Expect("Both Ui casts ran", Expect.All(
                    Expect.StepStatus("wizard", 1, "Cast", 0, "done"), Expect.StepStatus("wizard", 2, "Cast", 0, "done")))
                .Expect("The lesser globe stops the 1st-level missile and not the one heightened to 4th (PHB p.95, p.236)",
                    FactsAre(("globe.raised", true), ("blocks.plain", true), ("blocks.heightened", false)))
                .Expect("The plain missile does nothing; the heightened one deals four missiles of 1d4+1 at 4 = 20 (PHB p.251)",
                    FactsAre(("plain.lost.warded", 0), ("heightened.lost.warded", 20)))
                .Build();
        }

        // ── rules/cast-ally-in-burning-hands (SPL-149) ──────────────────

        /// <summary>
        /// SPL-149, fixed in batch 11 step 18 (PHB p.177: a creature may give up a save voluntarily; nothing makes it fail).
        /// A Ui wizard 5 casts Burning Hands (5d4, every d4 at 4 = 20, Reflex half) through an ally fighter 10 ft away and a
        /// dummy 15 ft away, both with +30 on saves. Both make their Reflex saves and take 10: the ally's own decider gives
        /// up its save only against a harmless or beneficial spell (SpellSaveRules.IsHarmlessOrBeneficialTo), and Burning
        /// Hands is a damage spell. Before step 18 ShouldForceTargetToAcceptSave made the ally forgo it and take the full 20.
        /// </summary>
        private static ScenarioDef AllyInBurningHands()
        {
            return Rules("rules/cast-ally-in-burning-hands", "An ally in its own side's Burning Hands rolls its save (PHB p.177; SPL-149)")
                .Covers("SPL-149", "PHB p.177", "PHB p.207", "batch 11 step 18")
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
                .Expect("The ally rolls its Reflex save too, makes it and takes half (PHB p.177: no one is made to fail)",
                    FactIs("cone.lost.ally", 10))
                .Build();
        }

        // ── rules/sleep-spell-penetration (SPL-122) ─────────────────────

        /// <summary>
        /// Batch 11 step 18 (SPL-122): the Sleep handler's spell resistance check is the one rule, d20 + the cast's caster
        /// level + Spell Penetration (PHB p.177, p.100), through CastContext.RollSpellResistance. A Ui wizard 1 with Spell
        /// Penetration casts Sleep (PHB p.280; spell resistance yes) on two goblins of 1 HD that fail every save; every d4
        /// is 4 (a 16 HD pool) and each SR d20 (label "Sleep SR check") is 10, so the check is 10 + 1 + 2 = 13. g1 has SR 13
        /// and is overcome (13 meets it), g2 has SR 14 and is not; without Spell Penetration (11) both would resist. The
        /// blocked line names the feat's +2.
        /// </summary>
        private static ScenarioDef SleepSpellPenetration()
        {
            return Rules("rules/sleep-spell-penetration", "Sleep's spell resistance check adds Spell Penetration (PHB p.177, p.100; SPL-122)")
                .Covers("SPL-122", "PHB p.177", "PHB p.100", "PHB p.280", "batch 11 step 18")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 1, "Spell Penetration")), 2, 10, Control.Ui)
                .Npc("g1", "goblin", 8, 10, Control.Idle)
                .Npc("g2", "goblin", 9, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SLEEP))
                .Tweak("g1", c => { Fails(c); c.Stats.SpellResistance = 13; })
                .Tweak("g2", c => { Fails(c); c.Stats.SpellResistance = 14; })
                .Initiative("wizard", "g1", "g2")
                .Force(4, 4, null, -1)
                .Force(20, 10, "Sleep SR check", -1)
                .Turn("wizard", 1,
                    Step.Cast(SpellNames.SLEEP, "g1"),
                    Step.Assert("after", ctx =>
                    {
                        Fact(ctx, "asleep.g1", ctx.Get("g1").HasCondition(CombatConditionType.Asleep));
                        Fact(ctx, "asleep.g2", ctx.Get("g2").HasCondition(CombatConditionType.Asleep));
                        return true;
                    }))
                .Expect("The Ui cast ran", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("10 + CL 1 + 2 = 13 overcomes SR 13 (g1 sleeps) and not SR 14 (g2 stays awake)",
                    FactsAre(("asleep.g1", true), ("asleep.g2", false)))
                .Expect("The blocked check is logged with the Spell Penetration term", Expect.Count("log",
                    e => (e.Str("text") ?? "").Contains("d20 10 + CL 1 + 2 Spell Penetration = 13 vs SR 14"), 1, 1))
                .Build();
        }

        // ── rules/spell-save-bonus-custom-handler (SPL-018) ─────────────

        /// <summary>
        /// Batch 11 step 18 (SPL-018): a save rolled by a spell's own handler carries the bonuses that depend on the spell and
        /// its caster, as SpellCaster.Cast's does. The example is the protection ward (PHB p.266: Protection from Evil gives
        /// +2 resistance on saves against effects of evil creatures), on Fireball's handler (TryResolveScaledAoEDamageSpell,
        /// PHB p.231). A chaotic evil Ui wizard 5 casts Fireball (every d6 6: 5d6 = 30, Reflex half) on two dummies; d1 is
        /// under Protection from Evil, d2 is not. Before the cast each dummy's save tweak is set so that the forced d20 of 10
        /// plus its Reflex modifier against the spell (without the ward) is the DC less 1. d2 fails and takes 30; d1's ward
        /// makes it the DC plus 1, so it saves and takes 15. Before step 18 the handler's save had no ward and d1 took 30.
        /// (No charm spell resolves in a custom handler, so the plan's "charm" example is this ward; Charm Person's +5,
        /// Hideous Laughter's +4 and Lullaby's -2 are checked in Tests.Magic.SpellSaveAndResistanceRulesTests.)
        /// </summary>
        private static ScenarioDef SpellSaveBonusCustomHandler()
        {
            return Rules("rules/spell-save-bonus-custom-handler", "A spell handler's own save carries the protection ward against an evil caster (PHB p.266; SPL-018)")
                .Covers("SPL-018", "PHB p.266", "PHB p.231", "PHB p.177", "batch 11 step 18")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 2, 10, Control.Ui)
                .Npc("d1", "target_dummy", 10, 10, Control.Idle)
                .Npc("d2", "target_dummy", 11, 10, Control.Idle)
                .Tweak("wizard", c => { AddWizardSlot(c, 3, SpellNames.FIREBALL); c.Stats.CharacterAlignment = Alignment.ChaoticEvil; })
                .Tweak("d1", Sturdy).Tweak("d2", Sturdy)
                .Initiative("wizard", "d1", "d2")
                .Force(6, 6, null, -1)
                .Force(20, 10, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("setup", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        SpellData fireball = SpellDatabase.GetSpell(SpellNames.FIREBALL);
                        int dc = SpellSaveDCRules.Explain(w, fireball).Total;
                        foreach (string key in new[] { "d1", "d2" })
                        {
                            CharacterController d = ctx.Get(key);
                            int mod = SaveRules.Modifier(d.Stats, SavingThrowType.Reflex, SaveContext.ForSpell(fireball));
                            SaveTweak(d, dc - 1 - 10 - mod);
                        }
                        CharacterController d1 = ctx.Get("d1");
                        ctx.Gm.Harness_ApplySpellBuff(d1, d1, SpellDatabase.GetSpell(SpellNames.PROTECTION_FROM_EVIL).Clone());
                        Fact(ctx, "ward", d1.StatusEffectManager != null && d1.StatusEffectManager.HasEffect(SpellNames.PROTECTION_FROM_EVIL));
                        RecordHp(ctx, "d1", "d2");
                        return true;
                    }),
                    Step.Cast(SpellNames.FIREBALL, "d1"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "fireball", "d1", "d2"); return true; }))
                .Expect("d1 is under Protection from Evil", FactIs("ward", true))
                .Expect("The Ui cast ran", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("d2 misses the DC by 1 and takes 30; d1's +2 ward makes the DC + 1, so it takes half (PHB p.266, p.231)",
                    FactsAre(("fireball.lost.d2", 30), ("fireball.lost.d1", 15)))
                .Build();
        }

        // ── rules/cast-entangled (SPL-145) ──────────────────────────────

        /// <summary>
        /// SPL-145, fixed in batch 11 step 13 (PHB p.308 entangled, p.70 Concentration table and its footnote: DC 15 + the
        /// spell's level, delegated RAW reading 2026-10-14). An entangled Ui wizard 5 casts Magic Missile; the entangled
        /// check's d20 is forced to 10 under its own label (GameManager.EntangledConcentrationDiceContext). The check is
        /// d20 + the wizard's Concentration bonus (CharacterStats.GetSpellcastingConcentrationBonus, recorded as the fact
        /// "conc") against DC 16, logged as "d20 10 + conc X = 10+X vs DC 16"; the three missiles (every d4 at 4: 15) land
        /// only when 10 + X reaches 16. Before step 13 the check added caster level + INT (5 + 3 = 8) under a "Dispel
        /// Magic check" label and only for somatic spells.
        /// </summary>
        private static ScenarioDef Entangled()
        {
            return Rules("rules/cast-entangled", "The entangled casting check is Concentration against DC 15 + the spell's level (SPL-145)")
                .Covers("SPL-145", "PHB p.70", "PHB p.308")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .StartCondition("wizard", CombatConditionType.Entangled, 10)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.MAGIC_MISSILE))
                .Initiative("wizard", "dummy")
                .Force(20, 10, GameManager.EntangledConcentrationDiceContext, 1)
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx =>
                    {
                        RecordHp(ctx, "dummy");
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "entangled", w.HasCondition(CombatConditionType.Entangled));
                        Fact(ctx, "conc", w.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: false));
                        return true;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "dummy"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "missile", "dummy"); return true; }))
                .Expect("The wizard is entangled when it casts", FactIs("entangled", true))
                .Expect("The check is d20 + Concentration against DC 15 + spell level 1 (PHB p.308, p.70), logged once", v =>
                {
                    string conc = FactOf(v, "conc");
                    if (conc == null)
                        return ExpectResult.Inconclusive("no fact conc");
                    int bonus = int.Parse(conc, System.Globalization.CultureInfo.InvariantCulture);
                    string line = @"Entangled casting concentration \(Pin Wizard, Magic Missile\): d20 10 \+ conc " + bonus + " = " + (10 + bonus) + @" vs DC 16 \(15 \+ spell level 1\)";
                    return v.Log(line).Count == 1
                        ? ExpectResult.Pass("logged d20 10 + conc " + bonus + " vs DC 16")
                        : ExpectResult.Fail("no entangled Concentration check logged as d20 10 + conc " + bonus + " vs DC 16");
                })
                .Expect("The spell goes off only when the check reaches DC 16, else it is lost with its slot", v =>
                {
                    string conc = FactOf(v, "conc");
                    string lost = FactOf(v, "missile.lost.dummy");
                    if (conc == null || lost == null)
                        return ExpectResult.Inconclusive("missing fact conc or missile.lost.dummy");
                    int want = 10 + int.Parse(conc, System.Globalization.CultureInfo.InvariantCulture) >= 16 ? 15 : 0;
                    return lost == want.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        ? ExpectResult.Pass("missile.lost.dummy=" + lost)
                        : ExpectResult.Fail("missile.lost.dummy=" + lost + ", expected " + want);
                })
                .Build();
        }

        // ── rules/cast-silenced, rules/cast-deafened-area (SPL-092, SPL-150; batch 11 step 13) ──

        /// <summary>
        /// SPL-092 (PHB p.279: no spell with a verbal component can be cast in a Silence; PHB p.100: Silent Spell). Three
        /// casters stand under a Silence (CharacterStats.SilenceActive, the creature a Silence is centred on). Round 1: the Ui
        /// wizard 5 casts Magic Missile and is refused at the cast core's Legality stage (nothing spent, one log line), then
        /// casts it with Silent Spell from a 2nd-level slot: the three missiles hit (every d4 at 4: 15). The Scripted wizard
        /// is refused its plain Magic Missile (the AI path, no log line, as its other refusals), and the second Scripted
        /// wizard casts its prepared Silent Spell Magic Missile (since batch 11 step 21 the NPC cast carries the slot's
        /// metamagic and spends that slot). A fourth, Ui wizard is not silenced at first: it holds a Shocking Grasp charge (Discharge Later),
        /// is then put under the Silence and picks Magic Missile: the spell menu refuses it before the cast begins, so the
        /// held charge, which a new spell would end, is kept and nothing is spent (batch 11 step 13 review).
        /// </summary>
        private static ScenarioDef Silenced()
        {
            MetamagicData silent = new MetamagicData();
            silent.Toggle(MetamagicFeatId.SilentSpell);
            return Scenario.Define("rules/cast-silenced", "A silenced caster cannot cast a verbal spell; Silent Spell can (SPL-092)")
                .Tags("rules", "cast-pipeline").Covers("PC_NPC_PARITY", "batch 11 step 13", "SPL-092", "PHB p.279", "PHB p.100")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5, "Silent Spell")), 5, 10, Control.Ui)
                .Pc("npc", ActorSource.Stats(() => Wizard("Pin Scripted", 5)), 5, 16, Control.Scripted)
                .Pc("npcsilent", ActorSource.Stats(() => Wizard("Pin Silent Scripted", 5, "Silent Spell")), 5, 13, Control.Scripted)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Npc("dummy2", "target_dummy", 9, 16, Control.Idle)
                .Npc("dummy3", "target_dummy", 9, 13, Control.Idle)
                .Pc("holder", ActorSource.Stats(() => Wizard("Pin Holder", 5)), 5, 7, Control.Ui)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.MAGIC_MISSILE); AddSilentSlot(c, SpellNames.MAGIC_MISSILE); Silence(c); })
                .Tweak("holder", c => { Prepare(c, 1, SpellNames.SHOCKING_GRASP); AddWizardSlot(c, 1, SpellNames.MAGIC_MISSILE); })
                .Tweak("npc", c => { Prepare(c, 1, SpellNames.MAGIC_MISSILE); Silence(c); })
                .Tweak("npcsilent", c => { AddSilentSlot(c, SpellNames.MAGIC_MISSILE); Silence(c); })
                .Tweak("dummy", Sturdy).Tweak("dummy2", Sturdy).Tweak("dummy3", Sturdy)
                .Initiative("wizard", "npc", "npcsilent", "holder", "dummy", "dummy2", "dummy3")
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "dummy"); RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "dummy"),
                    Step.Assert("refused", ctx => { FactHpLost(ctx, "plain", "dummy"); FactSlotsSpent(ctx, "plain.spent", "wizard"); Fact(ctx, "plain.actions", Actions(ctx.Get("wizard"))); RecordHp(ctx, "dummy"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "dummy", silent),
                    Step.Assert("silent", ctx => { FactHpLost(ctx, "silent", "dummy"); FactSlotsSpent(ctx, "silent.spent", "wizard"); return true; }))
                .Turn("npc", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "dummy2"); RecordSlots(ctx, "npc"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "dummy2"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "npc", "dummy2"); FactSlotsSpent(ctx, "npc.spent", "npc"); return true; }))
                .Turn("npcsilent", 1,
                    Step.Assert("cast the prepared Silent Spell Magic Missile", ctx =>
                    {
                        RecordHp(ctx, "dummy3");
                        SpellData spell = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
                        MetamagicData record = new MetamagicData();
                        record.Toggle(MetamagicFeatId.SilentSpell);
                        spell.MetamagicDataRef = record;
                        // A prepared metamagic slot's spell, as SpellcastingComponent.GetUniqueAvailableSpells builds it: only
                        // such a spell counts its metamagic slot (batch 11 step 19; a cast's stamped clone counts as plain).
                        spell.IsPreparedMetamagicSlotSpell = true;
                        Fact(ctx, "npcsilent.cast", ctx.Gm.TryNPCPerformSpellCastForAI(ctx.Get("npcsilent"), ctx.Get("dummy3"), spell));
                        FactHpLost(ctx, "npcsilent", "dummy3");
                        return true;
                    }))
                .Turn("holder", 1,
                    Step.CastHeld(SpellNames.SHOCKING_GRASP),
                    Step.Assert("silence the holder", ctx =>
                    {
                        CharacterController h = ctx.Get("holder");
                        Fact(ctx, "holder.held.before", h.Spellcasting.HasHeldTouchCharge);
                        Silence(h);
                        RecordHp(ctx, "dummy");
                        RecordSlots(ctx, "holder");
                        return true;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "dummy"),
                    Step.Assert("after", ctx =>
                    {
                        CharacterController h = ctx.Get("holder");
                        Fact(ctx, "holder.held.after", h.Spellcasting.HasHeldTouchCharge);
                        FactHpLost(ctx, "holder", "dummy");
                        FactSlotsSpent(ctx, "holder.spent", "holder");
                        return true;
                    }))
                .Expect("The Ui wizard's plain Magic Missile is refused: nothing lands, nothing is spent (PHB p.279)",
                    FactsAre(("plain.lost.dummy", 0), ("plain.spent", "none"), ("plain.actions", "smf")))
                .Expect("The refusal is logged once, for the Ui caster only", v => v.Log(@"Pin Wizard cannot cast Magic Missile inside a Silence").Count == 1
                    ? ExpectResult.Pass("one refusal line") : ExpectResult.Fail(v.Log(@"Pin Wizard cannot cast Magic Missile inside a Silence").Count + " refusal lines"))
                .Expect("The Silent Spell Magic Missile is cast from a 2nd-level slot: 3 missiles of 1d4+1 = 15 (PHB p.100, p.251)",
                    FactsAre(("silent.lost.dummy", 15), ("silent.spent", "L2+1")))
                .Expect("The Scripted wizard's plain Magic Missile is refused the same way (the AI path)", Expect.All(
                    Expect.StepStatus("npc", 1, "Cast", 0, "refused"), FactsAre(("npc.lost.dummy2", 0), ("npc.spent", "none"))))
                .Expect("The Scripted wizard's prepared Silent Spell Magic Missile is cast", FactsAre(("npcsilent.cast", true), ("npcsilent.lost.dummy3", 15)))
                .Expect("A silenced Ui caster holding a touch charge who picks a verbal spell is refused before the cast begins: the charge is kept, nothing lands or is spent",
                    FactsAre(("holder.held.before", true), ("holder.held.after", true), ("holder.lost.dummy", 0), ("holder.spent", "none")))
                .Expect("The holder's refusal is logged once", v => v.Log(@"Pin Holder cannot cast Magic Missile inside a Silence").Count == 1
                    ? ExpectResult.Pass("one refusal line") : ExpectResult.Fail(v.Log(@"Pin Holder cannot cast Magic Missile inside a Silence").Count + " refusal lines"))
                .Build();
        }

        // ── rules/npc-cast-without-divine-focus (batch 11 step 17) ──

        /// <summary>
        /// Decision D3 (batch 11 step 17, SPL-114; PHB p.174, p.130): an NPC caster needs its components as a PC does, and
        /// carries the kit its class calls for. Two human_cleric NPCs (Cleric 5, the DMG p.114 sample cleric's spells)
        /// through the NPC cast path: one has lost its holy symbol (taken from its pack at setup, as confiscation or theft
        /// would), the other keeps the kit it spawned with. The first casts Bull's Strength on itself (V, S, M/DF: a
        /// cleric casts the divine version, which needs the divine focus): refused at Legality with one log line and
        /// nothing spent (not Bless: the NPC path still refuses an area burst, AI-001); it then casts Shield of Faith
        /// (V, S, M), whose material component is in the pouch it still carries. The second casts Bull's Strength.
        /// </summary>
        private static ScenarioDef NpcCastWithoutDivineFocus()
        {
            return Rules("rules/npc-cast-without-divine-focus", "An NPC cleric that lost its holy symbol cannot cast the divine version of an M/DF spell; its pouch still serves an M spell (PHB p.174; SPL-114)")
                .Covers("SPL-114", "PHB p.174", "PHB p.130", "batch 11 step 17")
                .MaxRounds(1)
                .Npc("priest", "human_cleric", 5, 10, Control.Scripted, CharacterTeam.Enemy)
                .Npc("priest2", "human_cleric", 5, 14, Control.Scripted, CharacterTeam.Enemy)
                .Npc("dummy", "target_dummy", 12, 10, Control.Idle, CharacterTeam.Player)
                .Tweak("priest", c => TakeDivineFocus(c))
                .Tweak("dummy", Sturdy)
                .Initiative("priest", "priest2", "dummy")
                .Turn("priest", 1,
                    Step.Assert("record", ctx =>
                    {
                        RecordSlots(ctx, "priest");
                        Fact(ctx, "priest.symbol", SpellComponentRules.InventoryHoldsAny(ctx.Get("priest").GetInventoryData(), SpellComponentRules.DivineFocusIdsFor("Cleric")));
                        Fact(ctx, "priest.pouch", SpellComponentRules.InventoryHoldsAny(ctx.Get("priest").GetInventoryData(), DND35e.Identifiers.ItemIDs.COMPONENT_SPELL_POUCH));
                        return true;
                    }),
                    Step.Cast(SpellNames.BULLS_STRENGTH, "priest"),
                    Step.Assert("after Bull's Strength", ctx =>
                    {
                        FactSlotsSpent(ctx, "bull.spent", "priest");
                        Fact(ctx, "bull.active", ctx.Get("priest").StatusEffectManager != null && ctx.Get("priest").StatusEffectManager.HasEffect(SpellNames.BULLS_STRENGTH));
                        return true;
                    }),
                    Step.Cast(SpellNames.SHIELD_OF_FAITH, "priest"),
                    Step.Assert("after Shield of Faith", ctx =>
                    {
                        Fact(ctx, "sof.active", ctx.Get("priest").StatusEffectManager != null && ctx.Get("priest").StatusEffectManager.HasEffect(SpellNames.SHIELD_OF_FAITH));
                        return true;
                    }))
                .Turn("priest2", 1,
                    Step.Assert("record", ctx =>
                    {
                        Fact(ctx, "priest2.symbol", SpellComponentRules.InventoryHoldsAny(ctx.Get("priest2").GetInventoryData(), SpellComponentRules.DivineFocusIdsFor("Cleric")));
                        return true;
                    }),
                    Step.Cast(SpellNames.BULLS_STRENGTH, "priest2"),
                    Step.Assert("after", ctx =>
                    {
                        Fact(ctx, "priest2.bull", ctx.Get("priest2").StatusEffectManager != null && ctx.Get("priest2").StatusEffectManager.HasEffect(SpellNames.BULLS_STRENGTH));
                        return true;
                    }))
                .Expect("The NPC clerics spawn with the kit; the first has lost only its symbol",
                    FactsAre(("priest.symbol", false), ("priest.pouch", true), ("priest2.symbol", true)))
                .Expect("Without its holy symbol the NPC cleric is refused Bull's Strength (M/DF: the divine version needs the divine focus) at Legality: nothing is spent (PHB p.174)", Expect.All(
                    Expect.StepStatus("priest", 1, "Cast", 0, "refused"), FactsAre(("bull.spent", "none"), ("bull.active", false))))
                .Expect("The refusal is logged once", v => v.Log(@"cannot cast Bull's Strength: it needs a divine focus").Count == 1
                    ? ExpectResult.Pass("one refusal line") : ExpectResult.Fail(v.Log(@"cannot cast Bull's Strength: it needs a divine focus").Count + " refusal lines"))
                .Expect("Its pouch still serves Shield of Faith's material component", Expect.All(
                    Expect.StepStatus("priest", 1, "Cast", 1, "done"), FactsAre(("sof.active", true))))
                .Expect("The cleric with its kit casts Bull's Strength", Expect.All(
                    Expect.StepStatus("priest2", 1, "Cast", 0, "done"), FactsAre(("priest2.bull", true))))
                .Build();
        }

        /// <summary>Takes every holy or unholy symbol out of <paramref name="c"/>'s pack.</summary>
        private static void TakeDivineFocus(CharacterController c)
        {
            global::Inventory inv = c.GetInventoryData();
            string[] ids = SpellComponentRules.DivineFocusIdsFor("Cleric");
            for (int i = 0; i < inv.GeneralSlots.Length; i++)
            {
                ItemData item = inv.GeneralSlots[i];
                if (item != null && Array.IndexOf(ids, item.Id) >= 0)
                    inv.GeneralSlots[i] = null;
            }
        }

        /// <summary>Puts <paramref name="c"/> under a Silence centred on it for 100 rounds (CharacterStats.SilenceActive).</summary>
        private static void Silence(CharacterController c)
        {
            c.Stats.SilenceActive = true;
            c.Stats.SilenceRoundsRemaining = 100;
        }

        /// <summary>Adds a 2nd-level wizard slot holding <paramref name="spellId"/> prepared with Silent Spell (PHB p.100).</summary>
        private static void AddSilentSlot(CharacterController c, string spellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting to add a slot to");
            var slot = new SpellSlot(2, false, "Wizard");
            MetamagicData silent = new MetamagicData();
            silent.Toggle(MetamagicFeatId.SilentSpell);
            slot.PrepareWithMetamagic(SpellDatabase.GetSpell(spellId).Clone(), silent);
            sc.SpellSlots.Add(slot);
            sc.SyncPreparedSpellsFromSlots();
        }

        /// <summary>
        /// SPL-150 (PHB p.307: a deafened caster has a 20% chance of spell failure with a verbal component, once per
        /// casting). A deafened Ui wizard 5 casts Burning Hands (PHB p.207: every d4 at 4, 5d4 = 20) at two dummies that
        /// fail their saves; the deafened d% is forced to 50. Since batch 11 step 13 the roll is made once, in the cast core's
        /// Failure stage (one "Deafened spell check" line), not once per target inside SpellCaster.Cast; both dummies take
        /// 20. A deafened Scripted wizard 5 then casts Magic Missile (15): one roll for its cast too (the AI path).
        /// </summary>
        private static ScenarioDef DeafenedArea()
        {
            return Scenario.Define("rules/cast-deafened-area", "A deafened caster rolls the verbal failure once per casting, also for an area spell (SPL-150)")
                .Tags("rules", "cast-pipeline").Covers("PC_NPC_PARITY", "batch 11 step 13", "SPL-150", "PHB p.307", "PHB p.207")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Pc("npc", ActorSource.Stats(() => Wizard("Pin Scripted", 5)), 5, 16, Control.Scripted)
                .Npc("d1", "target_dummy", 7, 10, Control.Idle)
                .Npc("d2", "target_dummy", 8, 11, Control.Idle)
                .Npc("d3", "target_dummy", 9, 16, Control.Idle)
                .StartCondition("wizard", CombatConditionType.Deafened, 10)
                .StartCondition("npc", CombatConditionType.Deafened, 10)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.BURNING_HANDS))
                .Tweak("npc", c => Prepare(c, 1, SpellNames.MAGIC_MISSILE))
                .Tweak("d1", Fails).Tweak("d2", Fails).Tweak("d3", Sturdy)
                .Initiative("wizard", "npc", "d1", "d2", "d3")
                .Force(100, 50, CastComponentRules.DeafenedFailureDiceContext, -1)
                .Force(4, 4, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d1", "d2"); return true; }),
                    Step.Cast(SpellNames.BURNING_HANDS, "d1"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "cone", "d1", "d2"); return true; }))
                .Turn("npc", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d3"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d3"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "missile", "d3"); return true; }))
                .Expect("Both creatures in the cone take the full 20 (failed saves; PHB p.207)", FactsAre(("cone.lost.d1", 20), ("cone.lost.d2", 20)))
                .Expect("The Ui wizard's Burning Hands rolls the deafened failure once (PHB p.307; SPL-150)",
                    v => v.Log(@"Deafened spell check: Pin Wizard rolled 50 > 20%").Count == 1
                        ? ExpectResult.Pass("one roll") : ExpectResult.Fail(v.Log(@"Deafened spell check: Pin Wizard rolled").Count + " rolls logged"))
                .Expect("The Scripted wizard's Magic Missile rolls it once too and lands: 15",
                    Expect.All(v => v.Log(@"Deafened spell check: Pin Scripted rolled 50 > 20%").Count == 1
                        ? ExpectResult.Pass("one roll") : ExpectResult.Fail(v.Log(@"Deafened spell check: Pin Scripted rolled").Count + " rolls logged"),
                        FactIs("missile.lost.d3", 15)))
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
        /// cast's request gets the AI's own choice, fire (AiCastDecider: the goblin has no energy attack; batch 11 step 22). The cancel
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
        /// Scripted wizard's cast that follows has its own request, warm (AiCastDecider: no fire attack in sight).
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
        /// harness selects it past the spell menu's slot filter, as a slot mismatch such as SPL-011 does). The cast core
        /// commits the standard action, fails to spend the slot, logs an error and, since batch 11 step 14, restores the
        /// snapshot before returning to the action menu: no spell, the slot kept, every action left.
        /// </summary>
        private static ScenarioDef SlotFailureRefund()
        {
            var still = new MetamagicData();
            still.Toggle(MetamagicFeatId.StillSpell);
            return Rules("rules/cast-slot-failure-refund", "A failed slot spend gives the action back (SPL-147)")
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
                .Expect("Every action is left: a cast that never happens costs nothing (PHB p.141, p.143)", FactIs("actions", "smf"))
                .Build();
        }

        // ── rules/metamagic-cast-paths, rules/npc-cast-prepared-metamagic (batch 11 step 21) ──

        /// <summary>A Human sorcerer (CHA 16) knowing Magic Missile at 1st level, with <paramref name="feats"/>.</summary>
        private static CharacterStats Sorcerer(string name, int level, params string[] feats)
        {
            CharacterStats s = Caster(name, "Sorcerer", level, wis: 10, intelligence: 10, feats);
            s.CHA = 16;
            return s;
        }

        /// <summary>Makes Magic Missile one of the sorcerer's known 1st-level spells (PHB p.54).</summary>
        private static void KnowsMagicMissile(CharacterController c)
        {
            SpontaneousCastingData known = c.Spellcasting != null ? c.Spellcasting.SpontaneousData : null;
            if (known == null || !c.Spellcasting.IsSpontaneousCaster)
                throw new InvalidOperationException(c.Stats.CharacterName + " is not a spontaneous caster");
            if (known.IsSpellKnown(SpellNames.MAGIC_MISSILE, 1))
                return;
            if (known.SpellsKnownByLevel[1] != null && known.SpellsKnownByLevel[1].Count > 0)
                known.SpellsKnownByLevel[1].RemoveAt(0);
            if (!known.LearnSpell(SpellNames.MAGIC_MISSILE, 1))
                throw new InvalidOperationException(c.Stats.CharacterName + " could not learn Magic Missile");
        }

        /// <summary>A spontaneous caster's spells per day left at 1st and 3rd level, such as "L1:6 L3:4".</summary>
        private static string SpellsPerDay(CharacterController c)
        {
            SpontaneousCastingData d = c.Spellcasting != null ? c.Spellcasting.SpontaneousData : null;
            return d == null ? "none" : "L1:" + d.GetSlotsRemaining(1) + " L3:" + d.GetSlotsRemaining(3);
        }

        /// <summary>
        /// Batch 11 step 21 (metamagic-legality-and-slots; MetamagicSystem is the live rule, SPL-055, SPL-040, SPL-148,
        /// SPL-012). Four casters, every d4 at 4 and every d8 at 4:
        /// - the Ui sorcerer 6 (Empower Spell, Quicken Spell) first picks a quickened Magic Missile: refused at the spell menu
        ///   with a log line, nothing spent (PHB p.98 Special: no Quicken on a spontaneous cast); then an empowered one at d1:
        ///   a full-round action (PHB p.88) that spends a 3rd-level spell per day, not a 1st (SPL-040), and deals three
        ///   missiles of (4 + 1) x 1.5 = 7, 21 (PHB p.93);
        /// - the Ui wizard 5 (Silent Spell) with a plain 1st-level and a Silent 2nd-level Magic Missile casts the plain one at
        ///   d2: it spends the 1st-level slot, not the Silent one (SPL-012), and a standard action;
        /// - the Scripted sorcerer 6 (Empower Spell) casts an empowered Magic Missile at d3 through the NPC cast path (the
        ///   step's metamagic goes with the cast): full round, a 3rd-level spell per day, 21;
        /// - the Ui cleric 5 (Empower Spell, Extend Spell, Quicken Spell; spontaneous cure; 30 hit points down) converts its
        ///   prepared Bless on itself. Round 1: a quickened Cure Light Wounds is refused, nothing spent (PHB p.98 Special: a
        ///   conversion is a spontaneous cast); an extended one is refused too, nothing spent: Extend cannot modify an
        ///   instantaneous spell (PHB p.88: a feat cannot be used with the spells its entry says it cannot modify; p.94;
        ///   delegated RAW reading 2026-10-14). Round 2: an empowered one gives up a prepared 3rd-level spell, not the Bless
        ///   (PHB p.88: a prepared spell of the adjusted level; SPL-148), as a full-round action and heals
        ///   (4 + 5) x 1.5 = 13 (PHB p.93).
        /// </summary>
        private static ScenarioDef MetamagicCastPaths()
        {
            var empower = new MetamagicData();
            empower.Toggle(MetamagicFeatId.EmpowerSpell);
            var quicken = new MetamagicData();
            quicken.Toggle(MetamagicFeatId.QuickenSpell);
            var extend = new MetamagicData();
            extend.Toggle(MetamagicFeatId.ExtendSpell);
            return Rules("rules/metamagic-cast-paths", "Metamagic on every cast path: spontaneous, prepared, Scripted and a cleric's conversion (PHB p.88)")
                .Covers("SPL-055", "SPL-040", "SPL-148", "SPL-012", "PHB p.88", "PHB p.93", "PHB p.98", "batch 11 step 21")
                .MaxRounds(2)
                .Pc("sorc", ActorSource.Stats(() => Sorcerer("Empower Sorcerer", 6, "Empower Spell", "Quicken Spell")), 3, 4, Control.Ui)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Silent Wizard", 5, "Silent Spell")), 3, 8, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Sorcerer("Scripted Sorcerer", 6, "Empower Spell")), 3, 12, Control.Scripted)
                .Pc("cleric", ActorSource.Stats(() => Caster("Convert Cleric", "Cleric", 5, wis: 16, intelligence: 10, "Empower Spell", "Extend Spell", "Quicken Spell")), 3, 16, Control.Ui)
                .Npc("d1", "target_dummy", 8, 4, Control.Idle)
                .Npc("d2", "target_dummy", 8, 8, Control.Idle)
                .Npc("d3", "target_dummy", 8, 12, Control.Idle)
                .Tweak("sorc", KnowsMagicMissile)
                .Tweak("scripted", KnowsMagicMissile)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.MAGIC_MISSILE); AddSilentSlot(c, SpellNames.MAGIC_MISSILE); })
                .Tweak("cleric", c =>
                {
                    c.Stats.SpontaneousCasting = SpontaneousCastingType.Cure;
                    Prepare(c, 1, SpellNames.BLESS);
                    Prepare(c, 2, SpellNames.BULLS_STRENGTH);
                    Prepare(c, 3, SpellNames.PRAYER);
                    c.Stats.CurrentHP = Math.Max(1, c.Stats.TotalMaxHP - 30);
                })
                .Tweak("d1", Sturdy).Tweak("d2", Sturdy).Tweak("d3", Sturdy)
                .Initiative("sorc", "wizard", "scripted", "cleric", "d1", "d2", "d3")
                .Force(4, 4, null, -1)
                .Force(8, 4, null, -1)
                .Turn("sorc", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d1"); Fact(ctx, "sorc.before", SpellsPerDay(ctx.Get("sorc"))); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d1", quicken),
                    Step.Assert("after quicken", ctx =>
                    {
                        Fact(ctx, "sorc.quicken", SpellsPerDay(ctx.Get("sorc")));
                        Fact(ctx, "sorc.quicken.actions", Actions(ctx.Get("sorc")));
                        return true;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d1", empower),
                    Step.Assert("after empower", ctx =>
                    {
                        FactHpLost(ctx, "sorc", "d1");
                        Fact(ctx, "sorc.after", SpellsPerDay(ctx.Get("sorc")));
                        Fact(ctx, "sorc.actions", Actions(ctx.Get("sorc")));
                        return true;
                    }))
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d2"); RecordSlots(ctx, "wizard"); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d2"),
                    Step.Assert("after", ctx =>
                    {
                        FactHpLost(ctx, "wizard", "d2");
                        FactSlotsSpent(ctx, "wizard.spent", "wizard");
                        Fact(ctx, "wizard.actions", Actions(ctx.Get("wizard")));
                        return true;
                    }))
                .Turn("scripted", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d3"); Fact(ctx, "scripted.before", SpellsPerDay(ctx.Get("scripted"))); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d3", empower),
                    Step.Assert("after", ctx =>
                    {
                        FactHpLost(ctx, "scripted", "d3");
                        Fact(ctx, "scripted.after", SpellsPerDay(ctx.Get("scripted")));
                        Fact(ctx, "scripted.actions", Actions(ctx.Get("scripted")));
                        return true;
                    }))
                .Turn("cleric", 1,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "cleric"); Numbers(ctx)["hp|cleric.self"] = ctx.Get("cleric").Stats.CurrentHP; return true; }),
                    Step.Convert(SpellNames.BLESS, 1, "cleric", quicken),
                    Step.Assert("after quicken", ctx =>
                    {
                        FactSlotsSpent(ctx, "cleric.quicken.spent", "cleric");
                        Fact(ctx, "cleric.quicken.actions", Actions(ctx.Get("cleric")));
                        return true;
                    }),
                    Step.Convert(SpellNames.BLESS, 1, "cleric", extend),
                    Step.Assert("after extend", ctx =>
                    {
                        FactSlotsSpent(ctx, "cleric.extend.spent", "cleric");
                        Fact(ctx, "cleric.extend.actions", Actions(ctx.Get("cleric")));
                        Fact(ctx, "cleric.extend.healed", ctx.Get("cleric").Stats.CurrentHP - Numbers(ctx)["hp|cleric.self"]);
                        return true;
                    }))
                .Turn("cleric", 2,
                    Step.Assert("record", ctx => { RecordSlots(ctx, "cleric"); Numbers(ctx)["hp|cleric.self"] = ctx.Get("cleric").Stats.CurrentHP; return true; }),
                    Step.Convert(SpellNames.BLESS, 1, "cleric", empower),
                    Step.Assert("after empower", ctx =>
                    {
                        FactSlotsSpent(ctx, "cleric.spent", "cleric");
                        Fact(ctx, "cleric.actions", Actions(ctx.Get("cleric")));
                        Fact(ctx, "cleric.healed", ctx.Get("cleric").Stats.CurrentHP - Numbers(ctx)["hp|cleric.self"]);
                        Fact(ctx, "cleric.bless.kept", ctx.Get("cleric").Spellcasting.CanConvertSpellToSpontaneous(SpellDatabase.GetSpell(SpellNames.BLESS)));
                        return true;
                    }))
                .Expect("Sorcerer: Quicken Spell is refused on a spontaneous cast with a log line and nothing spent (PHB p.98 Special)", Expect.All(
                    FactsEqual("sorc.before", "sorc.quicken"), FactIs("sorc.quicken.actions", "smf"),
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("cannot quicken Magic Missile"), 1, 1)))
                .Expect("Sorcerer: the empowered Magic Missile is a full-round action that spends a 3rd-level spell per day (PHB p.88; SPL-040)", v =>
                {
                    string before = FactValue(v, "sorc.before"), after = FactValue(v, "sorc.after"), actions = FactValue(v, "sorc.actions");
                    if (before == null || after == null || actions == null)
                        return ExpectResult.Inconclusive("missing facts");
                    return OneLess(before, after, "L3") && Same(before, after, "L1") && actions == "---"
                        ? ExpectResult.Pass(before + " -> " + after + ", actions " + actions)
                        : ExpectResult.Fail(before + " -> " + after + ", actions " + actions);
                })
                .Expect("Sorcerer: three empowered missiles of 7 deal 21 (PHB p.93, p.251)", FactIs("sorc.lost.d1", 21))
                .Expect("Wizard: the plain Magic Missile spends the 1st-level slot, not the Silent one, as a standard action (SPL-012)",
                    FactsAre(("wizard.spent", "L1+1"), ("wizard.actions", "-m-"), ("wizard.lost.d2", 15)))
                .Expect("Scripted sorcerer: the step's Empower goes with the NPC cast: full round, a 3rd-level spell per day, 21", v =>
                {
                    string before = FactValue(v, "scripted.before"), after = FactValue(v, "scripted.after"), actions = FactValue(v, "scripted.actions");
                    string lost = FactValue(v, "scripted.lost.d3");
                    if (before == null || after == null || actions == null || lost == null)
                        return ExpectResult.Inconclusive("missing facts");
                    return OneLess(before, after, "L3") && Same(before, after, "L1") && actions == "---" && lost == "21"
                        ? ExpectResult.Pass(before + " -> " + after + ", actions " + actions + ", lost " + lost)
                        : ExpectResult.Fail(before + " -> " + after + ", actions " + actions + ", lost " + lost);
                })
                .Expect("Scripted sorcerer: the cast ran", Expect.StepStatus("scripted", 1, "Cast", 0, "done"))
                .Expect("Cleric: a quickened conversion is refused with a log line, nothing spent (PHB p.98 Special)", Expect.All(
                    FactsAre(("cleric.quicken.spent", "none"), ("cleric.quicken.actions", "smf")),
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("cannot quicken Cure Light Wounds"), 1, 1)))
                .Expect("Cleric: an extended conversion is refused with a log line, nothing spent: Extend cannot modify an instantaneous cure (PHB p.88, p.94; delegated RAW reading 2026-10-14)", Expect.All(
                    FactsAre(("cleric.extend.spent", "none"), ("cleric.extend.actions", "smf"), ("cleric.extend.healed", 0)),
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("cannot cast Cure Light Wounds with Extend Spell"), 1, 1)))
                .Expect("Cleric: the empowered conversion gives up a 3rd-level prepared spell (not the Bless) as a full-round action and heals 13 (PHB p.88, p.93; SPL-148)",
                    FactsAre(("cleric.spent", "L3+1"), ("cleric.actions", "---"), ("cleric.healed", 13), ("cleric.bless.kept", true)))
                .Build();
        }

        /// <summary>The value of the fact <paramref name="key"/> (the last one written), or null.</summary>
        private static string FactValue(TraceView v, string key)
        {
            string prefix = FactPrefix + key + "=";
            string value = null;
            foreach (TraceEvent e in v.Of("note"))
            {
                string text = e.Str("text");
                if (text != null && text.StartsWith(prefix, StringComparison.Ordinal))
                    value = text.Substring(prefix.Length);
            }
            return value;
        }

        /// <summary>The count of <paramref name="level"/> ("L3") in a <see cref="SpellsPerDay"/> string.</summary>
        private static int PerDay(string perDay, string level)
        {
            foreach (string part in perDay.Split(' '))
                if (part.StartsWith(level + ":", StringComparison.Ordinal) && int.TryParse(part.Substring(level.Length + 1), out int n))
                    return n;
            return int.MinValue;
        }

        private static bool OneLess(string before, string after, string level) => PerDay(after, level) == PerDay(before, level) - 1;
        private static bool Same(string before, string after, string level) => PerDay(after, level) == PerDay(before, level);

        /// <summary>
        /// Batch 11 step 21: an NPC casts a prepared metamagic slot's spell with its metamagic (SpellCastRequest.ForAI takes the
        /// slot's metamagic; LegacyCastQuirk.NpcNullMetamagic removed), and a plain NPC cast never spends a metamagic slot
        /// (SPL-012). Every d4 at 4.
        /// - The AI-run wizard 5 (Empower Spell) whose only unspent slot is an added 3rd-level Empowered Magic Missile (PHB
        ///   p.88; SPL-041 for the slot) casts it on its own choice at d1: three missiles of 7, 21 (PHB p.93), the 3rd-level
        ///   slot spent, a standard action.
        /// - The Scripted wizard 5 (Empower Spell) whose only Magic Missile is the same Empowered slot is refused a plain
        ///   Magic Missile (no plain slot holds it: nothing spent), then casts it with the step's Empower at d2: 21, the slot
        ///   spent.
        /// </summary>
        private static ScenarioDef NpcCastPreparedMetamagic()
        {
            var empower = new MetamagicData();
            empower.Toggle(MetamagicFeatId.EmpowerSpell);
            return Rules("rules/npc-cast-prepared-metamagic", "An NPC casts its prepared metamagic spell with its metamagic; a plain cast spends no metamagic slot (batch 11 step 21)")
                .Covers("AI-001", "SPL-012", "PHB p.88", "PHB p.93", "PC_NPC_PARITY", "batch 11 step 21")
                .MaxRounds(1)
                .Pc("ai", ActorSource.Stats(() => Wizard("AI Empower Wizard", 5, "Empower Spell")), 3, 6, Control.Ai)
                .Pc("scripted", ActorSource.Stats(() => Wizard("Scripted Empower Wizard", 5, "Empower Spell")), 3, 12, Control.Scripted)
                .Npc("d1", "target_dummy", 9, 6, Control.Idle)
                .Npc("d2", "target_dummy", 9, 12, Control.Idle)
                .Tweak("ai", c => OnlyEmpoweredMissile(c, empower))
                .Tweak("scripted", c => OnlyEmpoweredMissile(c, empower))
                .Tweak("d1", Sturdy).Tweak("d2", Sturdy)
                .Initiative("ai", "scripted", "d1", "d2")
                .Force(4, 4, null, -1)
                .Turn("scripted", 1,
                    Step.Assert("record", ctx =>
                    {
                        // The AI wizard's turn has ended: its Empowered slot and its actions.
                        CharacterController ai = ctx.Get("ai");
                        Fact(ctx, "ai.empowered.used", ai.Spellcasting.SpellSlots.Any(s => s != null && s.Level == 3 && s.HasMetamagic && s.IsUsed));
                        Fact(ctx, "ai.actions", Actions(ai));
                        RecordHp(ctx, "d2");
                        RecordSlots(ctx, "scripted");
                        return true;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d2"),
                    Step.Assert("after plain", ctx =>
                    {
                        FactHpLost(ctx, "plain", "d2");
                        FactSlotsSpent(ctx, "plain.spent", "scripted");
                        Fact(ctx, "plain.actions", Actions(ctx.Get("scripted")));
                        RecordHp(ctx, "d2");
                        RecordSlots(ctx, "scripted");
                        return true;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d2", empower),
                    Step.Assert("after empower", ctx =>
                    {
                        FactHpLost(ctx, "empowered", "d2");
                        FactSlotsSpent(ctx, "empowered.spent", "scripted");
                        return true;
                    }))
                .Expect("The AI wizard casts its Empowered Magic Missile once", CastEvents("ai", SpellNames.MAGIC_MISSILE, 1))
                .Expect("It deals three empowered missiles of 7: 150 -> 129 (PHB p.93)", v =>
                {
                    int hp = FinalHp(v, "d1");
                    if (hp == int.MinValue)
                        return ExpectResult.Inconclusive("no final hit points for d1");
                    return hp == 129 ? ExpectResult.Pass("d1 hp 129") : ExpectResult.Fail("d1 hp " + hp + ", expected 129");
                })
                .Expect("The AI wizard's 3rd-level Empowered slot is spent (PHB p.88)",
                    FactIs("ai.empowered.used", true))
                .Expect("The Scripted wizard's plain Magic Missile is refused and spends nothing: no plain slot holds it (SPL-012)",
                    FactsAre(("plain.lost.d2", 0), ("plain.spent", "none"), ("plain.actions", "smf")))
                .Expect("The Scripted wizard's step Empower casts the Empowered Magic Missile from its slot: 21 (PHB p.88, p.93)",
                    FactsAre(("empowered.lost.d2", 21), ("empowered.spent", "L3+1")))
                .Build();
        }

        /// <summary>Adds a 3rd-level slot with an Empowered Magic Missile (PHB p.88) and spends every other slot.</summary>
        private static void OnlyEmpoweredMissile(CharacterController c, MetamagicData empower)
        {
            AddWizardSlot(c, 3, SpellNames.MAGIC_MISSILE, empower);
            SpellcastingComponent sc = c.Spellcasting;
            SpellSlot kept = sc.SpellSlots[sc.SpellSlots.Count - 1];
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null && slot != kept)
                    slot.IsUsed = true;
            sc.SyncPreparedSpellsFromSlots();
        }
    }
}
#endif
