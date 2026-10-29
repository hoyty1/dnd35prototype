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
            yield return S("area-origin-fireball-ui", AreaOriginFireballUi);
            yield return S("area-origin-breath-ai", AreaOriginBreathAi);
            yield return S("area-damage-one-roll", AreaDamageOneRoll);
            yield return S("sound-burst-area", SoundBurstArea);
            yield return S("cast-pair-alignment-burst", AlignmentBurstUi);
            yield return S("consecrate-area", ConsecrateArea);
            yield return S("holy-area-overlap", HolyAreaOverlap);
            yield return S("bane-area", BaneArea);
            yield return S("cast-ui-sleep", UiSleep);
            yield return S("mirror-image-figure-pick", MirrorImageFigurePickUi);
            yield return S("mirror-image-pc-attacker", MirrorImagePcAttacker);
            yield return S("cast-ui-sleep-quickened", UiSleepQuickened);
            yield return S("cast-scroll-ui", () => ItemCast(scroll: true));
            yield return S("cast-wand-ui", () => ItemCast(scroll: false));
            yield return S("cast-scroll-aoo-cancel", () => ItemAoOCancel(scroll: true));
            yield return S("cast-wand-aoo-cancel", () => ItemAoOCancel(scroll: false));
            yield return S("cast-held-touch-charge", HeldTouchCharge);
            yield return S("cast-held-aoo-cancel", HeldAoOCancel);
            yield return S("cast-held-grappled", HeldGrappled);
            yield return S("cast-combat-end-retaliation-ui", CastCombatEndRetaliationUi);
            yield return S("grappled-caster-legality-ui-area", GrappledCasterLegalityUiArea);
            yield return S("grapple-spontaneous-still-cast", GrappleSpontaneousStillCast);
            yield return S("grappled-npc-casts-legal-only", GrappledNpcCastsLegalOnly);
            yield return S("npc-concentration-tracking", NpcConcentrationTracking);
            yield return S("cast-ui-summon-monster", UiSummonMonster);
            yield return S("cast-ui-summon-aoo-cancel", UiSummonAoOCancel);
            yield return S("cast-ui-summon-entangled", UiSummonEntangled);
            yield return S("cast-ui-summon-swarm", UiSummonSwarm);
            yield return S("cast-ui-grease", UiGrease);
            yield return S("npc-untrained-skills", NpcUntrainedSkills);
            foreach (PairSpec pair in Pairs())
            {
                PairSpec p = pair;
                yield return S(p.Id != null ? p.Id.Substring("rules/".Length) : "cast-pair-" + p.Family, () => PairScenario(p));
            }
            yield return S("cast-ai-spiritual-weapon", AiSpiritualWeapon);
            yield return S("npc-dimension-door", NpcDimensionDoor);
            yield return S("npc-summon-monster", NpcSummonMonster);
            yield return S("npc-held-touch", NpcHeldTouch);
            yield return S("npc-hold-charge-approach", NpcHoldChargeApproach);
            yield return S("ui-touch-miss-holds-charge", UiTouchMissHoldsCharge);
            yield return S("held-charge-costs-attack-ui", HeldChargeCostsAttackUi);
            yield return S("held-charge-costs-attack-ai", HeldChargeCostsAttackAi);
            yield return S("held-charge-touch-iterative-bab-ui", HeldChargeTouchIterativeBabUi);
            yield return S("held-charge-dissipates-ui", UiHeldChargeDissipates);
            yield return S("held-charge-dissipates-npc", NpcHeldChargeDissipates);
            yield return S("held-charge-wand-keeps", UiHeldChargeWandKeeps);
            yield return S("npc-resist-energy-choice", () => NpcResistEnergyChoice("rules/npc-resist-energy-choice", "dragon_red_wyrmling", "red dragon", "Fire"));
            yield return S("npc-resist-energy-choice-cold", () => NpcResistEnergyChoice("rules/npc-resist-energy-choice-cold", "dragon_white_wyrmling", "white dragon", "Cold"));
            yield return S("npc-magic-weapon", NpcMagicWeapon);
            yield return S("ai-spell-retry", AiSpellRetry);
            yield return S("ai-spell-retry-refused", AiSpellRetryRefused);
            yield return S("npc-cast-fireball", NpcCastFireball);
            yield return S("npc-cast-bless", NpcCastBless);
            yield return S("npc-cleric-converts-cure", NpcClericConvertsCure);
            yield return S("npc-cast-web", NpcCastWeb);
            yield return S("ai-area-aim", AiAreaAim);
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
            yield return S("ai-metamagic-choice", () => AiMetamagicChoice(threeFoes: true));
            yield return S("ai-metamagic-choice-one-foe", () => AiMetamagicChoice(threeFoes: false));
            yield return S("ai-metamagic-hold-plain", AiMetamagicHoldPlain);
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
        /// Mirror Image by RAW (PHB p.254; decision D7, batch 11 step 26): a Ui wizard 5 casts Mirror Image (d4 = 3, plus 1
        /// for caster level 5: four images); goblin g1 (Scripted) attacks and the 1d5 figure pick names an image (3): the
        /// d20 of 19 beats the image AC (10 + size + Dex), the image vanishes and the wizard takes nothing; goblin g2's 1d4
        /// pick (three images left) is a 1, the real wizard, who is hit, and g2 learns which figure is real. In round 2 the
        /// wizard's own 5-foot step confounds g2 again. The goblins act after the wizard, so they are flat-footed and make
        /// no AoO against the cast (PHB p.137). The NPC-attacker half of the rule; mirror-image-pc-attacker is the PC half.
        /// </summary>
        private static ScenarioDef MirrorImageFigurePickUi()
        {
            return Rules("rules/mirror-image-figure-pick", "Mirror Image: each attack picks a random figure; an image hit vanishes (PHB p.254)")
                .Covers("PHB p.254", "SPL-117", "AI-036", "batch 11 step 26")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Mirror Wizard", 5)), 5, 10, Control.Ui)
                .Npc("g1", "goblin", 6, 10, Control.Scripted)
                .Npc("g2", "goblin", 5, 11, Control.Scripted)
                .Tweak("wizard", c => { Prepare(c, 2, SpellNames.MIRROR_IMAGE); Sturdy(c); })
                .Initiative("wizard", "g1", "g2")
                .Force(4, 3, MirrorImageRules.CountRollContext, 1)
                .Force(5, 3, MirrorImageRules.FigureRollContext, 1)
                .Force(4, 1, MirrorImageRules.FigureRollContext, 1)
                .Force(20, 19, null, -1)
                .Turn("wizard", 1,
                    Step.Cast(SpellNames.MIRROR_IMAGE, "wizard"),
                    Step.Assert("cast", ctx =>
                    {
                        Fact(ctx, "images.cast", MirrorImageRules.ImagesOf(ctx.Get("wizard")));
                        Fact(ctx, "hp.cast", ctx.Get("wizard").Stats.CurrentHP);
                        return true;
                    }))
                .Turn("g1", 1,
                    Step.Attack("wizard"),
                    Step.Assert("g1", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "images.g1", MirrorImageRules.ImagesOf(w));
                        Fact(ctx, "hp.g1", w.Stats.CurrentHP);
                        Fact(ctx, "knows.g1", MirrorImageRules.KnowsRealFigure(ctx.Get("g1"), w));
                        return true;
                    }))
                .Turn("g2", 1,
                    Step.Attack("wizard"),
                    Step.Assert("g2", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "images.g2", MirrorImageRules.ImagesOf(w));
                        Fact(ctx, "hurt.g2", w.Stats.CurrentHP < w.Stats.MaxHP);
                        Fact(ctx, "knows.g2", MirrorImageRules.KnowsRealFigure(ctx.Get("g2"), w));
                        return true;
                    }))
                .Turn("wizard", 2,
                    Step.FiveFootStep(4, 10),
                    Step.Assert("moved", ctx =>
                    {
                        Fact(ctx, "knows.moved", MirrorImageRules.KnowsRealFigure(ctx.Get("g2"), ctx.Get("wizard")));
                        return true;
                    }),
                    Step.EndTurn())
                .Turn("g1", 2, Step.Pass())
                .Turn("g2", 2, Step.Pass())
                .Expect("Caster level 5 and a d4 of 3 make four images (1d4 + 1 per three levels, PHB p.254)", FactIs("images.cast", 4))
                .Expect("g1's attack picks an image, hits its AC, and the image vanishes",
                    FactsAre(("images.g1", 3), ("knows.g1", false)))
                .Expect("No damage from the image strike", FactsEqual("hp.cast", "hp.g1"))
                .Expect("g2's pick of 1 is the real wizard: it is hit, no image is lost, and g2 learns the real figure",
                    FactsAre(("images.g2", 3), ("hurt.g2", true), ("knows.g2", true)))
                .Expect("The wizard's own 5-foot step confounds g2 again (PHB p.254)", FactIs("knows.moved", false))
                .Expect("The image strike is in the attack trace as an image struck, not a hit on the wizard", v =>
                {
                    List<TraceEvent> g1 = v.Attacks("g1", "wizard", false);
                    return g1.Count > 0 && !g1[0].Bool("hit") && g1[0].Bool("mirrorImage") && g1[0].Bool("imageStruck")
                        ? ExpectResult.Pass("g1 attack recorded on an image")
                        : ExpectResult.Fail(g1.Count + " attacks" + (g1.Count > 0 ? ", hit " + g1[0].Bool("hit") + ", image " + g1[0].Bool("mirrorImage") : ""));
                })
                .Build();
        }

        /// <summary>
        /// Gives <paramref name="c"/> <paramref name="images"/> Mirror Image images with the tracked effect a cast would add
        /// (the duration tick ends images whose effect is gone, batch 11 step 26).
        /// </summary>
        private static void GrantMirrorImages(CharacterController c, int images)
        {
            StatusEffectManager sem = c.StatusEffectManager ?? c.gameObject.AddComponent<StatusEffectManager>();
            sem.Init(c.Stats);
            sem.AddEffect(SpellDatabase.GetSpell(SpellNames.MIRROR_IMAGE).Clone(), c.Stats.CharacterName, 3);
            MirrorImageRules.Grant(c, images);
        }

        /// <summary>
        /// The PC half of <see cref="MirrorImageFigurePickUi"/>: a Ui fighter attacks a goblin with two Mirror Image images
        /// (granted by the tweak with the tracked effect; a cast grants them through the same rule). Round 1: the 1d3 pick is an image (2) and the
        /// d20 of 19 beats its AC, so it vanishes and the goblin is unharmed. Round 2: the 1d2 pick is the goblin (1) and the
        /// d20 of 19 hits it (PHB p.254; decision D7, batch 11 step 26).
        /// </summary>
        private static ScenarioDef MirrorImagePcAttacker()
        {
            return Rules("rules/mirror-image-pc-attacker", "Mirror Image fools a PC attacker the same way (PHB p.254)")
                .Covers("PHB p.254", "SPL-117", "PC_NPC_PARITY", "batch 11 step 26")
                .MaxRounds(2)
                .Pc("fighter", ActorSource.Stats(() => BardScenarios.Fighter("Mirror Fighter", 6)), 8, 10, Control.Ui)
                .Npc("goblin", "goblin", 9, 10, Control.Idle)
                .Tweak("fighter", RulesScenarios.StripOffHand)
                .Tweak("goblin", c => { Sturdy(c); GrantMirrorImages(c, 2); })
                .Initiative("fighter", "goblin")
                .Force(3, 2, MirrorImageRules.FigureRollContext, 1)
                .Force(2, 1, MirrorImageRules.FigureRollContext, 1)
                .Force(20, 19, null, -1)
                .Turn("fighter", 1,
                    Step.Attack("goblin"),
                    Step.Assert("r1", ctx =>
                    {
                        CharacterController g = ctx.Get("goblin");
                        Fact(ctx, "images.r1", MirrorImageRules.ImagesOf(g));
                        Fact(ctx, "hurt.r1", g.Stats.CurrentHP < g.Stats.MaxHP);
                        return true;
                    }),
                    Step.EndTurn())
                .Turn("fighter", 2,
                    Step.Attack("goblin"),
                    Step.Assert("r2", ctx =>
                    {
                        CharacterController g = ctx.Get("goblin");
                        Fact(ctx, "images.r2", MirrorImageRules.ImagesOf(g));
                        Fact(ctx, "hurt.r2", g.Stats.CurrentHP < g.Stats.MaxHP);
                        Fact(ctx, "knows.r2", MirrorImageRules.KnowsRealFigure(ctx.Get("fighter"), g));
                        return true;
                    }),
                    Step.EndTurn())
                .Expect("Round 1: the pick is an image, which the hit destroys; the goblin is unharmed", FactsAre(("images.r1", 1), ("hurt.r1", false)))
                .Expect("Round 2: the pick is the goblin, which is hit; the fighter learns the real figure",
                    FactsAre(("images.r2", 1), ("hurt.r2", true), ("knows.r2", true)))
                .Build();
        }

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
        /// core's area exit (FinishAreaCast, Sleep's own combat-end check) declares the victory. Sleep's log starts with the
        /// quickened line, as every area log does since batch 12 step 9 (SPL-161; before it the line was missing, pinned).
        /// </summary>
        private static ScenarioDef UiSleepQuickened()
        {
            var quicken = new MetamagicData();
            quicken.Toggle(MetamagicFeatId.QuickenSpell);
            return Rules("rules/cast-ui-sleep-quickened", "A quickened Ui Sleep puts both foes to sleep, logs its quickened line, and the area exit ends the fight (SPL-161)")
                .Covers("PHB p.280", "PHB p.98", "SPL-161", "batch 11 step 9", "batch 12 step 9")
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
                .Expect("The quickened Sleep logs its quickened line, as every area cast does (SPL-161, batch 12 step 9)",
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("casts QUICKENED Sleep"), 1, 1))
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

        // ── rules/area-origin-fireball-ui, rules/area-origin-breath-ai (batch 11 step 24, decision D5) ──

        /// <summary>
        /// A Ui Fireball aimed by clicking d1's square: its point of origin is that square's north-east corner, the
        /// intersection +(11,11) (PHB p.175: the point of origin of a spell is always a grid intersection; the scenario click
        /// aims at the square's centre, which snaps to that corner). A square is in the 20-ft. burst when its far corner is
        /// within 4 squares of the origin, counted like movement: d1 (10,10), dWest (7,10) and dNorthEast (13,13) are in it;
        /// dSouth (10,6) and dSouthWest (7,7), both inside the old burst centred on d1's square, are not. Every d6 is 6
        /// (5d6 = 30) and every dummy fails its Reflex save. Batch 11 step 24.
        /// </summary>
        private static ScenarioDef AreaOriginFireballUi()
        {
            return Rules("rules/area-origin-fireball-ui", "A Ui Fireball originates at a grid intersection and covers the squares whose far corner is within 20 ft. (PHB p.175)")
                .Covers("PHB p.175", "PHB p.231", "batch 11 step 24")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Aiming Wizard", 5)), 2, 10, Control.Ui)
                .Npc("d1", "target_dummy", 10, 10, Control.Idle)
                .Npc("dWest", "target_dummy", 7, 10, Control.Idle)
                .Npc("dNorthEast", "target_dummy", 13, 13, Control.Idle)
                .Npc("dSouth", "target_dummy", 10, 6, Control.Idle)
                .Npc("dSouthWest", "target_dummy", 7, 7, Control.Idle)
                .Tweak("wizard", c => AddWizardSlot(c, 3, SpellNames.FIREBALL))
                .Tweak("d1", Fails).Tweak("dWest", Fails).Tweak("dNorthEast", Fails).Tweak("dSouth", Fails).Tweak("dSouthWest", Fails)
                .Initiative("wizard", "d1", "dWest", "dNorthEast", "dSouth", "dSouthWest")
                .Force(6, 6, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "d1", "dWest", "dNorthEast", "dSouth", "dSouthWest"); return true; }),
                    Step.Cast(SpellNames.FIREBALL, "d1"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "fireball", "d1", "dWest", "dNorthEast", "dSouth", "dSouthWest"); return true; }))
                .Expect("The Ui cast ran", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("The burst from +(11,11) takes d1, dWest (far corner 4 squares away) and dNorthEast (3 diagonals = 4) (PHB p.175)",
                    FactsAre(("fireball.lost.d1", 30), ("fireball.lost.dWest", 30), ("fireball.lost.dNorthEast", 30)))
                .Expect("dSouth (far corner 5 squares away) and dSouthWest (4 diagonals = 6) are outside it",
                    FactsAre(("fireball.lost.dSouth", 0), ("fireball.lost.dSouthWest", 0)))
                .Build();
        }

        /// <summary>
        /// The AI's breath aim (AoESystem.BestDirectedAimAt, the aim DragonAIProfile scores and NPCExecuteBreathWeaponForAI
        /// fires): a Scripted hell hound at (5,10) breathes its 10-ft. cone (MM p.151) at hero1 (7,10). The cone starts at a
        /// corner of the hound's square (PHB p.175; MM p.69); from the north-east corner it would take hero1 and the hound's
        /// kobold ally at (7,11), from the south-east corner hero1 and hero2 (6,9), so the AI takes the south-east corner:
        /// both heroes take the 2d6 (forced to 12) on failed saves and the kobold takes nothing. The old cone from the hound's
        /// square (one square, then three) took the kobold and missed hero2. Batch 11 step 24.
        /// </summary>
        private static ScenarioDef AreaOriginBreathAi()
        {
            return Rules("rules/area-origin-breath-ai", "An AI breath cone starts at the corner of the creature's square that takes the most foes and spares its ally (PHB p.175, MM p.69)")
                .Covers("PHB p.175", "MM p.69", "MM p.151", "batch 11 step 24")
                .MaxRounds(1)
                .Npc("hound", "hell_hound", 5, 10, Control.Scripted)
                .Pc("hero1", ActorSource.Stats(() => Wizard("First Hero", 1)), 7, 10, Control.Idle)
                .Pc("hero2", ActorSource.Stats(() => Wizard("Second Hero", 1)), 6, 9, Control.Idle)
                .Npc("kobold", "kobold_warrior", 7, 11, Control.Idle)
                .Tweak("hero1", Fails).Tweak("hero2", Fails).Tweak("kobold", Fails)
                .Initiative("hound", "hero1", "hero2", "kobold")
                .Force(6, 6, "Breath weapon damage", -1)
                .Script("hound", HoundBreathAtHero)
                .Expect("The hound breathed", FactIs("breath", "used"))
                .Expect("The cone from the south-east corner takes both heroes (2d6 = 12, failed saves)",
                    FactsAre(("breath.lost.hero1", 12), ("breath.lost.hero2", 12)))
                .Expect("The kobold, the hound's ally, is not in the cone", FactIs("breath.lost.kobold", 0))
                .Build();
        }

        /// <summary>The hound's script: one breath at hero1 through the AI's breath executor, with the hit points around it.</summary>
        private static System.Collections.IEnumerator HoundBreathAtHero(ScenarioContext ctx, CharacterController hound)
        {
            CharacterController hero = ctx.Get("hero1");
            if (hound == null || hero == null || !hound.HasBreathWeapon || !hound.IsBreathWeaponReady)
            {
                Fact(ctx, "breath", "not-ready");
                yield break;
            }
            RecordHp(ctx, "hero1", "hero2", "kobold");
            Fact(ctx, "breath", "used");
            yield return ctx.Gm.StartCoroutine(ctx.Gm.NPCExecuteBreathWeaponForAI(hound, hero));
            FactHpLost(ctx, "breath", "hero1", "hero2", "kobold");
        }

        // ── rules/area-damage-one-roll (batch 11 step 20, D6) ───────────

        // ── rules/sound-burst-area, rules/cast-pair-alignment-burst, rules/consecrate-area (batch 11 step 23) ──

        /// <summary>A domain slot of <paramref name="level"/> holding <paramref name="spellId"/> (a Ui cleric's domain spell).</summary>
        private static void AddDomainSlot(CharacterController c, int level, string spellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting to add a domain slot to");
            var slot = new SpellSlot(level, true, "Cleric");
            slot.PrepareWithMetamagic(SpellDatabase.GetSpell(spellId).Clone(), null);
            sc.SpellSlots.Add(slot);
            sc.SyncPreparedSpellsFromSlots();
        }

        private static readonly string[] SoundBurstKeys = { "fails", "saves", "deaf", "resists", "silenced", "outside" };

        /// <summary>
        /// SPL-046 (batch 11 step 23): Sound Burst is an area spell (PHB p.281: Area 10-ft.-radius spread, Close range). A Ui
        /// cleric 7 aims it at "fails"; the spread takes every creature within 10 ft. of that square: "fails" fails its
        /// Fortitude save (8 sonic, stunned), "saves" makes it (8, not stunned), "deaf" fails it but cannot hear (8, not
        /// stunned), "resists" has spell resistance 100 (d20 19 + CL 7 fails: nothing), "silenced" is under a Silence (PHB
        /// p.279: nothing); "outside", 15 ft. away, is not in the area. One d8 is drawn for the whole area (decision D6), and
        /// the slot is spent. Replaces the single-target pairs rules/cast-pair-sound-burst, -saved, -deaf, -silenced and
        /// rules/sound-burst-failed (SPL-142's facts, now on one area cast; an NPC casts it through the same area resolve since batch 11 step 25, rules/ai-area-aim).
        /// </summary>
        private static ScenarioDef SoundBurstArea()
        {
            return Rules("rules/sound-burst-area", "A Ui Sound Burst affects every creature in its 10-ft. spread: one 1d8, each creature's own spell resistance, Silence and stun save (PHB p.281)")
                .Covers("SPL-046", "SPL-142", "SPL-180", "PHB p.281", "PHB p.279", "PHB p.177", "batch 11 step 23")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => Cleric("Burst Cleric", 7)), 3, 10, Control.Ui)
                .Npc("fails", "target_dummy", 9, 10, Control.Idle)
                .Npc("saves", "target_dummy", 10, 10, Control.Idle)
                .Npc("deaf", "target_dummy", 8, 10, Control.Idle)
                .Npc("resists", "target_dummy", 9, 11, Control.Idle)
                .Npc("silenced", "target_dummy", 9, 9, Control.Idle)
                .Npc("outside", "target_dummy", 12, 10, Control.Idle)
                .Tweak("cleric", c => Prepare(c, 2, SpellNames.SOUND_BURST))
                .Tweak("fails", Fails).Tweak("saves", Saves)
                .Tweak("deaf", c => { Fails(c); c.ApplyCondition(CombatConditionType.Deafened, 100, "scenario"); })
                .Tweak("resists", c => { Fails(c); c.Stats.SpellResistance = 100; })
                .Tweak("silenced", c => { Fails(c); c.Stats.SilenceActive = true; c.Stats.SilenceRoundsRemaining = 100; })
                .Tweak("outside", Fails)
                .Initiative("cleric", "fails", "saves", "deaf", "resists", "silenced", "outside")
                .Force(20, 19, null, -1)
                .Force(8, 8, null, -1)
                .Turn("cleric", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, SoundBurstKeys); Fact(ctx, "slots.before", Slots(ctx.Get("cleric"))); return true; }),
                    Step.Cast(SpellNames.SOUND_BURST, "fails"),
                    Step.Assert("after the burst", ctx =>
                    {
                        FactHpLost(ctx, "burst", SoundBurstKeys);
                        foreach (string k in SoundBurstKeys)
                            Fact(ctx, k + ".stunned", ctx.Get(k).HasCondition(CombatConditionType.Stunned));
                        Fact(ctx, "slots.after", Slots(ctx.Get("cleric")));
                        return true;
                    }))
                .Expect("The Ui cast ran", Expect.StepStatus("cleric", 1, "Cast", 0, "done"))
                .Expect("8 sonic damage to each creature the spread affects; none through spell resistance, a Silence or outside the area (PHB p.281, p.279)",
                    FactsAre(("burst.lost.fails", 8), ("burst.lost.saves", 8), ("burst.lost.deaf", 8), ("burst.lost.resists", 0), ("burst.lost.silenced", 0), ("burst.lost.outside", 0)))
                .Expect("Only the creature that fails its Fortitude save and can hear is stunned (PHB p.281)",
                    FactsAre(("fails.stunned", true), ("saves.stunned", false), ("deaf.stunned", false), ("resists.stunned", false), ("silenced.stunned", false), ("outside.stunned", false)))
                .Expect("One d8 is drawn for the whole area (decision D6)", Expect.Count("dice", e => e.Int("sides") == 8, 1, 1))
                .Expect("The 2nd-level slot is spent", v =>
                {
                    string before = FactOf(v, "slots.before"), after = FactOf(v, "slots.after");
                    if (before == null || after == null)
                        return ExpectResult.Inconclusive("missing slot facts");
                    return before.Contains("L2:0/") && after.Contains("L2:1/") ? ExpectResult.Pass(before + " -> " + after) : ExpectResult.Fail(before + " -> " + after);
                })
                .Build();
        }

        private static readonly string[] AlignmentBurstKeys = { "evilFails", "evilSaves", "neutralFails", "neutralSaves", "good", "fiend", "blinker", "outside" };

        /// <summary>
        /// SPL-046 (batch 11 step 23): Holy Smite is an area spell (PHB p.241: Area 20-ft.-radius burst, Medium range),
        /// resolved per creature by TryResolveAlignmentBurstAoE. A Ui neutral good cleric 7 casts it from a domain slot at
        /// "evilFails": 3d8 (every d8 is 8: 24) to an evil creature, blinded 1 round on a failed Will save; 12 and no
        /// blindness on a save; half (12) to a neutral creature, a quarter (6) when it saves, never blinded; nothing to a good
        /// creature; 7d6 (every d6 is 6: 42) to an evil outsider ("fiend", a dummy made an outsider), blinded; nothing
        /// outside the area; an evil creature under Blink that fails its save takes half, 12 (PHB p.206: half damage from
        /// area attacks; the step 23 review), and is blinded. One roll of 3d8 and one of 7d6 for the whole area (decision
        /// D6). Ui only (NPC area casts since batch 11 step 25 run the same resolve: rules/npc-cast-fireball).
        /// </summary>
        private static ScenarioDef AlignmentBurstUi()
        {
            Action<CharacterController> Align(Alignment a, Action<CharacterController> saves)
                => c => { saves(c); c.Stats.CharacterAlignment = a; };
            return Rules("rules/cast-pair-alignment-burst", "A Ui Holy Smite judges every creature in its 20-ft. burst by alignment: full, half and none, one roll for the area (PHB p.241)")
                .Covers("SPL-046", "SPL-180", "SPL-182", "PHB p.241", "PHB p.177", "batch 11 step 23")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => { CharacterStats s = Cleric("Smite Cleric", 7); s.CharacterAlignment = Alignment.NeutralGood; return s; }), 3, 10, Control.Ui)
                .Npc("evilFails", "target_dummy", 10, 10, Control.Idle)
                .Npc("evilSaves", "target_dummy", 11, 10, Control.Idle)
                .Npc("neutralFails", "target_dummy", 9, 10, Control.Idle)
                .Npc("neutralSaves", "target_dummy", 10, 11, Control.Idle)
                .Npc("good", "target_dummy", 10, 9, Control.Idle)
                .Npc("fiend", "target_dummy", 11, 11, Control.Idle)
                .Npc("blinker", "target_dummy", 9, 11, Control.Idle)
                .Npc("outside", "target_dummy", 16, 10, Control.Idle)
                .Tweak("cleric", c => AddDomainSlot(c, 4, SpellNames.HOLY_SMITE))
                .Tweak("evilFails", Align(Alignment.NeutralEvil, Fails))
                .Tweak("evilSaves", Align(Alignment.NeutralEvil, Saves))
                .Tweak("neutralFails", Align(Alignment.TrueNeutral, Fails))
                .Tweak("neutralSaves", Align(Alignment.TrueNeutral, Saves))
                .Tweak("good", Align(Alignment.LawfulGood, Fails))
                .Tweak("fiend", c => { Align(Alignment.ChaoticEvil, Fails)(c); c.Stats.CreatureType = "Outsider"; })
                .Tweak("blinker", c =>
                {
                    Align(Alignment.NeutralEvil, Fails)(c);
                    c.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.BLINK).Clone(), "Blink Wizard", 5, 10);
                })
                .Tweak("outside", Align(Alignment.NeutralEvil, Fails))
                .Initiative("cleric", "evilFails", "evilSaves", "neutralFails", "neutralSaves", "good", "fiend", "blinker", "outside")
                .Force(20, 10, null, -1)
                .Force(8, 8, null, -1)
                .Force(6, 6, null, -1)
                .Turn("cleric", 1,
                    Step.Assert("record", ctx =>
                    {
                        RecordHp(ctx, AlignmentBurstKeys);
                        Fact(ctx, "blinker.blinking", ctx.Get("blinker").HasActiveBlinkEffect);
                        return true;
                    }),
                    Step.Cast(SpellNames.HOLY_SMITE, "evilFails"),
                    Step.Assert("after the smite", ctx =>
                    {
                        FactHpLost(ctx, "smite", AlignmentBurstKeys);
                        foreach (string k in AlignmentBurstKeys)
                            Fact(ctx, k + ".blinded", ctx.Get(k).HasCondition(CombatConditionType.Blinded));
                        return true;
                    }))
                .Expect("The Ui cast ran", Expect.StepStatus("cleric", 1, "Cast", 0, "done"))
                .Expect("Evil 24, saved 12; neutral 12, saved 6; good none; an evil outsider 42; outside none (PHB p.241)",
                    FactsAre(("smite.lost.evilFails", 24), ("smite.lost.evilSaves", 12), ("smite.lost.neutralFails", 12), ("smite.lost.neutralSaves", 6),
                        ("smite.lost.good", 0), ("smite.lost.fiend", 42), ("smite.lost.outside", 0)))
                .Expect("A blinking evil creature takes half of the 24 from the area spell (PHB p.206)",
                    FactsAre(("blinker.blinking", true), ("smite.lost.blinker", 12)))
                .Expect("Only the evil creatures that fail their Will save are blinded (PHB p.241)",
                    FactsAre(("evilFails.blinded", true), ("evilSaves.blinded", false), ("neutralFails.blinded", false), ("neutralSaves.blinded", false),
                        ("good.blinded", false), ("fiend.blinded", true), ("blinker.blinded", true), ("outside.blinded", false)))
                .Expect("One 3d8 and one 7d6 for the whole area (decision D6)",
                    Expect.All(Expect.Count("dice", e => e.Int("sides") == 8, 3, 3), Expect.Count("dice", e => e.Int("sides") == 6, 7, 7)))
                .Build();
        }

        private static readonly string[] BaneKeys = { "fails", "saves", "resists", "skeleton", "ally", "far" };

        /// <summary>
        /// SPL-046, SPL-119 (batch 11 step 23): Bane is an area spell (PHB p.203: all enemies within 50 ft.), a 50-ft. burst
        /// centred on the caster with the enemies filter (a delegated RAW reading 2026-10-14: Bless's burst, PHB p.205). A Ui
        /// cleric 7 casts it and confirms the self-centred preview; each enemy gets the single-target cast's tests through
        /// SpellCaster.Cast (`ResolveBaneAoE`): "fails" fails its Will save (-1 on attack rolls), "saves" makes it, "resists"
        /// has SR 100 (d20 10 + CL 7 fails), a skeleton is immune to mind-affecting effects; an ally in the burst ("ally",
        /// a Player-team creature) is not an enemy, and "far" is 55 ft. away. Every d20 is 10.
        /// </summary>
        private static ScenarioDef BaneArea()
        {
            return Rules("rules/bane-area", "A Ui Bane takes the caster's enemies within 50 ft.: each its own spell resistance, Will save and mind-affecting immunity (PHB p.203)")
                .Covers("SPL-046", "SPL-119", "SPL-027", "PHB p.203", "PHB p.205", "batch 11 step 23")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => { CharacterStats s = Cleric("Bane Cleric", 7); s.CharacterAlignment = Alignment.NeutralGood; return s; }), 5, 10, Control.Ui)
                .Pc("ally", ActorSource.Stats(() => Cleric("Bane Ally", 1)), 6, 11, Control.Idle)
                .Npc("fails", "target_dummy", 9, 10, Control.Idle)
                .Npc("saves", "target_dummy", 9, 11, Control.Idle)
                .Npc("resists", "target_dummy", 9, 9, Control.Idle)
                .Npc("skeleton", "skeleton_warrior", 10, 10, Control.Idle)
                .Npc("far", "target_dummy", 16, 10, Control.Idle)
                .Tweak("cleric", c => Prepare(c, 1, SpellNames.BANE))
                .Tweak("fails", Fails).Tweak("saves", Saves)
                .Tweak("resists", c => { Fails(c); c.Stats.SpellResistance = 100; })
                .Tweak("skeleton", c => SaveTweak(c, -30))
                .Tweak("ally", c => SaveTweak(c, -30))
                .Tweak("far", Fails)
                .Initiative("cleric", "ally", "fails", "saves", "resists", "skeleton", "far")
                .Force(20, 10, null, -1)
                .Turn("cleric", 1,
                    Step.Assert("record", ctx =>
                    {
                        foreach (string k in BaneKeys)
                            Numbers(ctx)["atk|" + k] = ctx.Get(k).Stats.Bonuses.Total(BonusTarget.AttackRoll);
                        return true;
                    }),
                    Step.Cast(SpellNames.BANE, "fails"),
                    Step.Assert("after bane", ctx =>
                    {
                        foreach (string k in BaneKeys)
                            Fact(ctx, "bane." + k, ctx.Get(k).Stats.Bonuses.Total(BonusTarget.AttackRoll) - Numbers(ctx)["atk|" + k]);
                        return true;
                    }))
                .Expect("The Ui cast ran", Expect.StepStatus("cleric", 1, "Cast", 0, "done"))
                .Expect("Only the enemy that fails its Will save takes -1 on attack rolls; saved, resisting, mind-affecting-immune, allied and distant creatures none (PHB p.203)",
                    FactsAre(("bane.fails", -1), ("bane.saves", 0), ("bane.resists", 0), ("bane.skeleton", 0), ("bane.ally", 0), ("bane.far", 0)))
                .Build();
        }
        /// <summary>
        /// SPL-042 (batch 11 step 23): Consecrate has its area (PHB p.212: 20-ft.-radius emanation, Close range), so its area
        /// handler runs. A Ui neutral good cleric 7 casts it at a skeleton: the undead in the area takes -1 on attack rolls,
        /// weapon damage rolls and saves (the bonus ledger, HolyAreaRules); a living dummy in the area does not. The
        /// skeleton (Scripted) then walks out of the area and loses the penalty (UpdateHolyAreaEffectsForCharacter, called
        /// for every move).
        /// </summary>
        private static ScenarioDef ConsecrateArea()
        {
            // The ledger totals' change since the cast's start (the dummy has its own -10 on saves), and the flag.
            void RecordLedger(ScenarioContext ctx, string tag)
            {
                foreach (string k in new[] { "skel", "living" })
                {
                    CharacterStats s = ctx.Get(k).Stats;
                    int attack = s.Bonuses.Total(BonusTarget.AttackRoll), damage = s.Bonuses.Total(BonusTarget.WeaponDamage), saves = s.Bonuses.Total(BonusTarget.AllSaves);
                    if (tag == "before")
                    {
                        Numbers(ctx)["atk|" + k] = attack;
                        Numbers(ctx)["dmg|" + k] = damage;
                        Numbers(ctx)["sav|" + k] = saves;
                    }
                    Fact(ctx, tag + "." + k, (attack - Numbers(ctx)["atk|" + k]) + "/" + (damage - Numbers(ctx)["dmg|" + k]) + "/"
                        + (saves - Numbers(ctx)["sav|" + k]) + "/" + (s.ConsecrateActive ? "in" : "out"));
                }
            }
            return Rules("rules/consecrate-area", "A Ui Consecrate gives the undead in its 20-ft. area -1 on attacks, damage and saves until it leaves (PHB p.212)")
                .Covers("SPL-042", "SPL-050", "PHB p.212", "batch 11 step 23")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => { CharacterStats s = Cleric("Consecrating Cleric", 7); s.CharacterAlignment = Alignment.NeutralGood; return s; }), 3, 10, Control.Ui)
                .Npc("skel", "skeleton_warrior", 8, 10, Control.Scripted)
                .Npc("living", "target_dummy", 8, 12, Control.Idle)
                .Tweak("cleric", c => Prepare(c, 2, SpellNames.CONSECRATE))
                // Its banded mail off, so its 30-ft move leaves the area (heavy armor would slow it to 20 ft, PHB p.123; ITM-010).
                .Tweak("skel", RulesScenarios.StripArmor)
                .Initiative("cleric", "skel", "living")
                .Turn("cleric", 1,
                    Step.Assert("record", ctx => { RecordLedger(ctx, "before"); return true; }),
                    Step.Cast(SpellNames.CONSECRATE, "skel"),
                    Step.Assert("after the cast", ctx => { RecordLedger(ctx, "cast"); return true; }))
                .Turn("skel", 1,
                    Step.Move(13, 10),
                    Step.Assert("after leaving", ctx =>
                    {
                        RecordLedger(ctx, "left");
                        Fact(ctx, "skel.pos", ctx.Get("skel").GridPosition.x + "," + ctx.Get("skel").GridPosition.y);
                        return true;
                    }))
                .Expect("The Ui cast ran", Expect.StepStatus("cleric", 1, "Cast", 0, "done"))
                .Expect("The skeleton walked out of the area", FactIs("skel.pos", "13,10"))
                .Expect("Before the cast neither creature has a modifier", FactsAre(("before.skel", "0/0/0/out"), ("before.living", "0/0/0/out")))
                .Expect("In the area the undead takes -1 on attacks, damage and saves; the living creature nothing (PHB p.212)",
                    FactsAre(("cast.skel", "-1/-1/-1/in"), ("cast.living", "0/0/0/out")))
                .Expect("Out of the area the undead loses the penalty (PHB p.212: undead entering the area)",
                    FactsAre(("left.skel", "0/0/0/out"), ("left.living", "0/0/0/out")))
                .Build();
        }
        /// <summary>
        /// SPL-042 (the batch 11 step 23 review): which undead are in a Desecrate area, and what happens when a Consecrate
        /// dispels one of two. Round 1: a Ui neutral evil cleric 7 desecrates around (7,10) (area A). Round 2: it desecrates
        /// around (13,10) (area B), so the skeleton at (10,10) stands in both; a Large skeleton owlbear (2x2) walks from
        /// (17,5), wholly outside B, to (14,6), where its own square is still outside but (14,7) is inside: any occupied
        /// square in the area puts it in, so it gains the +1 profane bonus (PHB p.218). Then a Ui neutral good cleric 7
        /// consecrates around (1,10), overlapping A but not B: A is dispelled (PHB p.212), and the skeleton keeps its +1
        /// because it is still in B, as does the owlbear. Since batch 11 step 24 each click aims at the clicked square's
        /// north-east corner (area A from +(8,11), B from +(14,11), the Consecrate from +(2,11); PHB p.175), so the owlbear's
        /// destination moved from (16,6) to (14,6) to keep the same case.
        /// </summary>
        private static ScenarioDef HolyAreaOverlap()
        {
            // The +1 on attack rolls, weapon damage rolls and saves since the start, and the flag, for each undead.
            void RecordLedger(ScenarioContext ctx, string tag)
            {
                foreach (string k in new[] { "skel", "owl" })
                {
                    CharacterStats s = ctx.Get(k).Stats;
                    int attack = s.Bonuses.Total(BonusTarget.AttackRoll), damage = s.Bonuses.Total(BonusTarget.WeaponDamage), saves = s.Bonuses.Total(BonusTarget.AllSaves);
                    if (tag == "before")
                    {
                        Numbers(ctx)["atk|" + k] = attack;
                        Numbers(ctx)["dmg|" + k] = damage;
                        Numbers(ctx)["sav|" + k] = saves;
                    }
                    Fact(ctx, tag + "." + k, (attack - Numbers(ctx)["atk|" + k]) + "/" + (damage - Numbers(ctx)["dmg|" + k]) + "/"
                        + (saves - Numbers(ctx)["sav|" + k]) + "/" + (s.DesecrateActive ? "in" : "out"));
                }
            }
            // Whether the owlbear's own square, and any of its squares, is in area B (the precondition of the multi-square case).
            void RecordOwlInB(ScenarioContext ctx, string tag)
            {
                HashSet<UnityEngine.Vector2Int> b = AoESystem.GetPlacedAreaCells(SpellDatabase.GetSpell(SpellNames.DESECRATE), GridIntersection.NorthEastCornerOf(new UnityEngine.Vector2Int(13, 10)), SquareGrid.Instance); // a click on (13,10) aims at its north-east corner (batch 11 step 24)
                CharacterController owl = ctx.Get("owl");
                bool any = false;
                foreach (UnityEngine.Vector2Int sq in owl.GetOccupiedSquares())
                    any |= b.Contains(sq);
                Fact(ctx, tag + ".owlsquares", owl.GetOccupiedSquares().Count + "/" + (b.Contains(owl.GridPosition) ? "base-in" : "base-out") + "/" + (any ? "part-in" : "all-out"));
            }
            return Rules("rules/holy-area-overlap", "A Desecrate area takes an undead creature in any of its squares, and a Consecrate dispelling one of two Desecrate areas leaves the other's bonus (PHB p.212, p.218)")
                .Covers("SPL-042", "SPL-050", "PHB p.212", "PHB p.218", "batch 11 step 23")
                .MaxRounds(2)
                .Pc("evil", ActorSource.Stats(() => { CharacterStats s = Cleric("Desecrating Cleric", 7); s.CharacterAlignment = Alignment.NeutralEvil; return s; }), 10, 14, Control.Ui)
                .Pc("good", ActorSource.Stats(() => { CharacterStats s = Cleric("Consecrating Cleric", 7); s.CharacterAlignment = Alignment.NeutralGood; return s; }), 1, 14, Control.Ui)
                .Npc("skel", "skeleton_warrior", 10, 10, Control.Idle)
                .Npc("owl", "skeleton_owlbear", 17, 5, Control.Scripted)
                .Tweak("evil", c => Prepare(c, 2, SpellNames.DESECRATE, 2))
                .Tweak("good", c => Prepare(c, 2, SpellNames.CONSECRATE))
                .Initiative("evil", "owl", "good", "skel")
                .Turn("evil", 1,
                    Step.Assert("record", ctx => { RecordLedger(ctx, "before"); return true; }),
                    Step.CastAt(SpellNames.DESECRATE, 7, 10),
                    Step.Assert("after area A", ctx => { RecordLedger(ctx, "a"); return true; }))
                .Turn("evil", 2,
                    Step.CastAt(SpellNames.DESECRATE, 13, 10),
                    Step.Assert("after area B", ctx => { RecordLedger(ctx, "b"); RecordOwlInB(ctx, "b"); return true; }))
                .Turn("owl", 2,
                    Step.Move(14, 6),
                    Step.Assert("after the owlbear's move", ctx => { RecordLedger(ctx, "moved"); RecordOwlInB(ctx, "moved"); return true; }))
                .Turn("good", 2,
                    Step.CastAt(SpellNames.CONSECRATE, 1, 10),
                    Step.Assert("after the Consecrate", ctx => { RecordLedger(ctx, "consecrated"); return true; }))
                .Expect("The Ui casts ran", Expect.StepStatus("evil", 1, "Cast", 0, "done"))
                .Expect("The second Desecrate ran", Expect.StepStatus("evil", 2, "Cast", 0, "done"))
                .Expect("The Consecrate ran", Expect.StepStatus("good", 2, "Cast", 0, "done"))
                .Expect("Area A takes the skeleton only; area B adds nothing for it (one source, PHB p.171) and the owlbear is still wholly outside",
                    FactsAre(("before.skel", "0/0/0/out"), ("a.skel", "1/1/1/in"), ("a.owl", "0/0/0/out"), ("b.skel", "1/1/1/in"), ("b.owl", "0/0/0/out"),
                        ("b.owlsquares", "4/base-out/all-out")))
                .Expect("The owlbear's own square is outside B but another of its squares is inside, so it gains the +1 (PHB p.218)",
                    FactsAre(("moved.owlsquares", "4/base-out/part-in"), ("moved.owl", "1/1/1/in")))
                .Expect("The Consecrate dispels A only: the skeleton and the owlbear, still in B, keep the +1 (PHB p.212)",
                    FactsAre(("consecrated.skel", "1/1/1/in"), ("consecrated.owl", "1/1/1/in")))
                .Build();
        }
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
        /// attack (PHB p.141), so it takes the attack action, a standard action (delegated RAW reading 2026-10-14): the
        /// round-1 delivery after Discharge Later is the casting's own touch and costs nothing, the round-2 delivery spends
        /// the standard action (SPL-156, fixed by batch 12 step 18: it runs as one step of the attack sequence).
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
                .Expect("The round-2 delivery, a touch after the casting round, takes the attack action: the standard action (PHB p.141; SPL-156)",
                    FactIs("actions.hit", "-m-"))
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
        /// Batch 12 step 4 (CHR-067 fallback, CHR-086): a spawned goblin has no skill ranks (NPCs get none yet), and its skill
        /// checks use the untrained skill (PHB p.63-64) through the same <c>CharacterStats.GetSkillBonus</c> and
        /// <c>RollSkillCheck</c> as a PC's. The Stats wizard is given a created PC's skill list (every skill at 0 ranks,
        /// <c>RulesScenarios.GiveClassSkills</c>), so the PC side runs through real 0-rank dictionary entries. On the
        /// wizard's turn an Assert step reads both: the goblin's Listen is its Wisdom modifier plus its Alertness feat (+2,
        /// MM p.133, PHB p.89), a rolled Balance is d20 plus its total (not the automatic failure -1 of before), Spellcraft
        /// (trained only) is still -1 and cannot be attempted, a lowercase name finds the same skill; the wizard's 0-rank
        /// Listen entry follows the same rule and its 0-rank Spellcraft entry gives 0 and -1; neither can attempt to identify
        /// a spell for a counterspell without Spellcraft ranks (PHB p.64, p.170). The checks hold for every d20, so every
        /// seed agrees.
        /// </summary>
        private static ScenarioDef NpcUntrainedSkills()
        {
            return Scenario.Define("rules/npc-untrained-skills", "An NPC without skill ranks makes untrained skill checks with its ability modifier (PHB p.63-64; CHR-067)")
                .Tags("rules", "skills")
                .Covers("PHB p.63", "PHB p.64", "PHB p.170", "CHR-067", "CHR-086", "PC_NPC_PARITY", "batch 12 step 4")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Skill Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 9, 10, Control.Idle)
                .Tweak("gob", Sturdy)
                .Tweak("wizard", RulesScenarios.GiveClassSkills)
                .Initiative("wizard", "gob")
                .Turn("wizard", 1,
                    Step.Assert("skills", ctx =>
                    {
                        CharacterStats g = ctx.Get("gob").Stats;
                        CharacterStats w = ctx.Get("wizard").Stats;
                        Fact(ctx, "gob.ranked", g.Skills.Values.Count(s => s != null && s.Ranks > 0));
                        Fact(ctx, "gob.listen.feat", g.GetFeatSkillBonus("Listen"));
                        Fact(ctx, "gob.listen", g.GetSkillBonus("Listen") == g.WISMod + g.GetFeatSkillBonus("Listen")
                            + g.GetRacialSkillModifier("Listen") + g.GetConditionSkillModifier("Listen")
                            + g.SpellAndEffectSkillModifier("Listen"));
                        int balance = g.RollSkillCheck("Balance");
                        int d20 = balance - g.GetSkillBonus("Balance");
                        Fact(ctx, "gob.balance.d20", d20 >= 1 && d20 <= 20);
                        Fact(ctx, "gob.spellcraft", g.RollSkillCheck("Spellcraft"));
                        Fact(ctx, "gob.spellcraft.bonus", g.GetSkillBonus("Spellcraft"));
                        Fact(ctx, "gob.lowercase", g.GetSkillBonus("escape artist") == g.GetSkillBonus("Escape Artist"));
                        Fact(ctx, "gob.attempt", g.CanAttemptSkill("Listen") && !g.CanAttemptSkill("Spellcraft"));
                        Fact(ctx, "wizard.entries", w.Skills.ContainsKey("Listen") && w.Skills.ContainsKey("Spellcraft")
                            && w.Skills["Spellcraft"].Ranks == 0);
                        Fact(ctx, "wizard.listen", w.GetSkillBonus("Listen") == w.WISMod + w.GetRacialSkillModifier("Listen")
                            + w.GetConditionSkillModifier("Listen") + w.SpellAndEffectSkillModifier("Listen"));
                        Fact(ctx, "wizard.spellcraft", w.RollSkillCheck("Spellcraft"));
                        Fact(ctx, "wizard.spellcraft.bonus", w.GetSkillBonus("Spellcraft"));
                        bool wizIdentified = ctx.Get("wizard").RollSpellcraftIdentification(1, out int wRoll, out _, out _);
                        bool gobIdentified = ctx.Get("gob").RollSpellcraftIdentification(1, out int gRoll, out _, out _);
                        Fact(ctx, "identify.untrained", !wizIdentified && !gobIdentified && wRoll == 0 && gRoll == 0);
                        return true;
                    }),
                    Step.EndTurn())
                .Expect("The goblin has no skill ranks (NPCs get none yet, CHR-067)", FactIs("gob.ranked", 0))
                .Expect("Untrained Listen is the goblin's Wisdom modifier plus its Alertness +2 (PHB p.64, p.89; MM p.133)",
                    FactsAre(("gob.listen.feat", 2), ("gob.listen", true)))
                .Expect("A rolled untrained Balance is d20 plus the total, not an automatic failure", FactIs("gob.balance.d20", true))
                .Expect("Spellcraft is trained only: -1 rolled, 0 bonus, cannot be attempted (PHB p.63-64)",
                    FactsAre(("gob.spellcraft", -1), ("gob.spellcraft.bonus", 0), ("gob.attempt", true)))
                .Expect("A lowercase skill name finds the same skill (CHR-086)", FactIs("gob.lowercase", true))
                .Expect("The PC wizard's 0-rank entries follow the same rule: Listen untrained, Spellcraft 0 and -1 (PC_NPC_PARITY)",
                    FactsAre(("wizard.entries", true), ("wizard.listen", true), ("wizard.spellcraft", -1), ("wizard.spellcraft.bonus", 0)))
                .Expect("Without Spellcraft ranks neither side can attempt to identify a spell for a counterspell (PHB p.64, p.170)",
                    FactIs("identify.untrained", true))
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

        // ── rules/cast-combat-end-retaliation-ui, rules/grappled-caster-legality-ui-area (batch 12 step 9) ──

        /// <summary>
        /// CORE-039, fixed in batch 12 step 9: every cast ends with the combat-end check, so a cast that puts a side out
        /// other than through its target ends the fight at once. The game has no Fire Shield retribution against a touch
        /// spell (melee reactions answer weapon and natural attacks only), so the retaliation here is the attack of
        /// opportunity the cast provokes (PHB p.140): the Ui wizard 1, the only hero, at 1 hp, casts Magic Missile at the
        /// adjacent goblin without casting defensively. The goblin (acting first, so not flat-footed, PHB p.137; +30 to hit,
        /// every d6 a 6, so its morningstar deals 6) hits and drops the wizard to -5, dying (PHB p.145), so the spell is lost
        /// and the party is out. The defeat is declared inside the cast, not at the action menu after the pause
        /// (ShowActionChoices) as before: since batch 12 step 14 (D15) by the AoO's post-attack hook
        /// (ThreatSystem.ExecuteAoO, OnAttackResolved < EvaluateCombatEnd), else by the cast core's exit (FinishCast,
        /// RunCastCombatEndCheck; TST-046). The trace closes at that decision, so a watcher (<see cref="AfterCombatEndWatch"/>)
        /// records what the game still does after it: the lost cast may wind down (its provocation outcome, the lost
        /// cast's concentration step) and must reach its exit's combat-end mark before the harness halt, but no later cast
        /// stage, spell effect, 'cast' event or action-menu flow may follow (CMB-192's cast path); the watch's log lines
        /// hold exactly one 'struck down while casting' line.
        /// </summary>
        private static ScenarioDef CastCombatEndRetaliationUi()
        {
            return Rules("rules/cast-combat-end-retaliation-ui", "A Ui cast whose provoked AoO drops the last hero ends the fight inside the cast (CORE-039)")
                .Covers("CORE-039", "PHB p.140", "PHB p.145", "batch 12 step 9")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Retaliation Wizard", 1)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.MAGIC_MISSILE))
                .Tweak("gob", c => { AttackTweak(c, 30); Sturdy(c); })
                .Hp("wizard", 1)
                .Initiative("gob", "wizard")
                .Force(6, 6, null, -1)
                .OnSetup(ctx => AfterCombatEndWatch.Stop())
                .Turn("wizard", 1,
                    Step.Assert("watch what follows the combat end", ctx => { AfterCombatEndWatch.Start(); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob"),
                    Step.AnswerAoO(AoOAnswer.Proceed))
                .Expect("The goblin's attack of opportunity against the casting wizard hits (PHB p.140)",
                    Expect.Count("aoo", e => e.Str("by") == "gob" && e.Str("target") == "wizard" && e.Bool("hit"), 1, 1))
                .Expect("The spell is lost: the goblin keeps every hit point", v =>
                {
                    TraceEvent actor = v.Of("actor").FirstOrDefault(e => e.Str("key") == "gob");
                    if (actor == null) return ExpectResult.Fail("no actor event for gob");
                    int start = actor.Int("hp"), end = FinalHp(v, "gob");
                    return end == start ? ExpectResult.Pass("gob hp " + start + " -> " + end) : ExpectResult.Fail("gob hp " + start + " -> " + end);
                })
                .Expect("The game declares the defeat itself", Expect.Outcome(Outcome.Defeat))
                .Expect("The defeat is declared inside the cast (the AoO's post-attack hook or the cast's own combat-end check), not at the action menu or a turn boundary (CORE-039, D15)", v =>
                {
                    TraceEvent over = v.Of("phase").FirstOrDefault(e => e.Get("to") is GameManager.TurnPhase t && t == GameManager.TurnPhase.CombatOver);
                    if (over == null) return ExpectResult.Fail("no CombatOver");
                    var by = over.Get("by") as List<string>;
                    string text = by != null ? string.Join(" < ", by) : "";
                    TraceEvent cast = v.Of("cast").FirstOrDefault(e => e.Str("by") == "wizard");
                    TraceEvent aoo = v.AoOs("gob", "wizard").FirstOrDefault();
                    bool inCast = text.IndexOf("RunCastCombatEndCheck", StringComparison.Ordinal) >= 0
                                  || text.IndexOf("OnAttackResolved", StringComparison.Ordinal) >= 0;
                    bool notMenuOrTurn = text.IndexOf("ShowActionChoices", StringComparison.Ordinal) < 0
                                         && text.IndexOf("NextInitiativeTurn", StringComparison.Ordinal) < 0;
                    bool ordered = cast != null && aoo != null && cast.Seq < over.Seq && aoo.Seq < over.Seq;
                    return inCast && notMenuOrTurn && ordered
                        ? ExpectResult.Pass(text + " (cast #" + cast.Seq + ", AoO #" + aoo.Seq + ")", over.Seq)
                        : ExpectResult.Fail("by " + text + ", cast " + (cast != null ? "#" + cast.Seq : "none") + ", AoO " + (aoo != null ? "#" + aoo.Seq : "none"), over.Seq);
                })
                .Expect("The wizard is struck down once, the cast reaches its own exit, and after the combat end nothing of it goes on: no later cast stage, spell effect, 'cast' event or action menu (CMB-192's cast path)", v =>
                {
                    AfterCombatEndWatch w = AfterCombatEndWatch.Stop();
                    if (w == null) return ExpectResult.Inconclusive("no watcher (the wizard's first step did not run)");
                    if (!w.ProbeSeen) return ExpectResult.Inconclusive("the watcher's console listener never saw its probe line, so it cannot see the action-menu flow");
                    if (!w.SawCombatOver) return ExpectResult.Fail("the watcher saw no CombatOver phase");
                    List<string> late = w.Stages.Where(s => !AfterCombatEndWatch.IsWindDown(s)).ToList();
                    int struck = w.AllLogs.Count(l => l != null && l.Contains("is struck down while casting Magic Missile"));
                    string seen = "after the end: stages [" + string.Join(", ", w.Stages) + "], " + w.Logs.Count + " log lines, " + struck + " struck-down line(s)";
                    if (late.Count > 0) return ExpectResult.Fail("cast stages after the combat end: " + string.Join(", ", late) + "; " + seen);
                    if (w.Casts.Count > 0) return ExpectResult.Fail("'cast' events after the combat end: " + string.Join(", ", w.Casts));
                    if (w.MenuFlow.Count > 0) return ExpectResult.Fail("action-menu flow after the combat end: " + string.Join(" | ", w.MenuFlow));
                    // The cast must reach its own exit (FinishCast's combat-end mark) before the harness halt, which stops
                    // every GameManager coroutine; a cast cut off by the halt would otherwise pass by default.
                    if (!w.Stages.Any(s => s.StartsWith(CastStageTrace.CombatEnd + ":", StringComparison.Ordinal)))
                        return ExpectResult.Fail("the cast never reached its exit before the harness halt; " + seen);
                    if (struck != 1) return ExpectResult.Fail("expected one 'struck down while casting Magic Missile' line; " + seen);
                    return ExpectResult.Pass(seen);
                })
                .Build();
        }

        /// <summary>
        /// What the game still does after the CombatOver phase change of rules/cast-combat-end-retaliation-ui (TST-046). The
        /// trace closes at the harness decision, so this watcher, started by the wizard's first step, chains the trace's
        /// PhaseChanged, CastStage and SpellCast hooks and the CombatLog hook and listens to the console for the action-menu
        /// flow markers (GameManager.LogMenuFlow: "[GameManager][MenuFlow] ...", Debug.Log lines the harness's fast mode
        /// filters out, so the watch lowers the console filter to Log and proves it with a probe line; no probe makes the
        /// expectation Inconclusive), recording everything that follows the CombatOver change. The scenario's last
        /// expectation stops it (Finalize, before the job's cleanup) and puts the previous hooks and the filter back; the
        /// scenario's OnSetup and a new start stop a watcher an aborted job left behind, and its console listener drops
        /// itself once another trace owns the hooks.
        /// </summary>
        private sealed class AfterCombatEndWatch
        {
            private const string MenuFlowTag = "[GameManager][MenuFlow]";
            private const string ProbeTag = "[Scenario][AfterCombatEndWatch] console probe";
            private static AfterCombatEndWatch _current;

            public readonly List<string> Stages = new List<string>();
            public readonly List<string> Casts = new List<string>();
            public readonly List<string> Logs = new List<string>();
            /// <summary>Every combat log line from the start of the watch, before and after the combat end.</summary>
            public readonly List<string> AllLogs = new List<string>();
            public readonly List<string> MenuFlow = new List<string>();
            public bool SawCombatOver;
            /// <summary>The console listener received the probe line Start logs, so Debug.Log lines reach it.</summary>
            public bool ProbeSeen;

            private Action<GameManager.TurnPhase, GameManager.TurnPhase> _prevPhase, _phase;
            private Action<CharacterController, SpellData, string, string> _prevStage, _stage;
            private Action<CharacterController, CharacterController, SpellData> _prevCast, _cast;
            private Action<string> _prevLog, _log;
            private UnityEngine.Application.LogCallback _console;
            private UnityEngine.LogType _prevFilter;

            /// <summary>
            /// The lost cast winding down after the AoO dropped its caster: the provocation's outcome (aoo-lost), the lost
            /// cast's concentration step and the exit's combat-end mark. Any other stage means the cast went on.
            /// </summary>
            public static bool IsWindDown(string stage)
                => stage == CastStageTrace.Provocation + ":aoo-lost"
                   || stage.StartsWith(CastStageTrace.ConcentrationOnCast + ":", StringComparison.Ordinal)
                   || stage.StartsWith(CastStageTrace.CombatEnd + ":", StringComparison.Ordinal);

            public static void Start()
            {
                Stop();
                var w = new AfterCombatEndWatch();
                w._prevPhase = ScenarioHooks.PhaseChanged;
                w._prevStage = ScenarioHooks.CastStage;
                w._prevCast = ScenarioHooks.SpellCast;
                w._prevLog = ScenarioHooks.CombatLog;
                w._phase = ScenarioHooks.Safe<GameManager.TurnPhase, GameManager.TurnPhase>("after-combat-end-watch.phase", (from, to) =>
                {
                    w._prevPhase?.Invoke(from, to);
                    if (to == GameManager.TurnPhase.CombatOver)
                        w.SawCombatOver = true;
                });
                w._stage = ScenarioHooks.Safe<CharacterController, SpellData, string, string>("after-combat-end-watch.stage", (c, s, stage, detail) =>
                {
                    w._prevStage?.Invoke(c, s, stage, detail);
                    if (w.SawCombatOver)
                        w.Stages.Add(stage + ":" + detail);
                });
                w._cast = ScenarioHooks.Safe<CharacterController, CharacterController, SpellData>("after-combat-end-watch.cast", (c, t, s) =>
                {
                    w._prevCast?.Invoke(c, t, s);
                    if (w.SawCombatOver)
                        w.Casts.Add(s != null ? s.SpellId : "?");
                });
                w._log = ScenarioHooks.Safe<string>("after-combat-end-watch.log", text =>
                {
                    w._prevLog?.Invoke(text);
                    w.AllLogs.Add(text);
                    if (w.SawCombatOver)
                        w.Logs.Add(text);
                });
                w._console = (condition, stackTrace, type) =>
                {
                    // A watcher a failed Finalize left behind: the next job's trace replaced the hooks, so drop the listener.
                    if (ScenarioHooks.PhaseChanged != w._phase)
                    {
                        UnityEngine.Application.logMessageReceived -= w._console;
                        return;
                    }
                    if (condition == null)
                        return;
                    if (condition.StartsWith(ProbeTag, StringComparison.Ordinal))
                        w.ProbeSeen = true;
                    else if (w.SawCombatOver && condition.StartsWith(MenuFlowTag, StringComparison.Ordinal))
                    {
                        int nl = condition.IndexOf('\n');
                        w.MenuFlow.Add(nl > 0 ? condition.Substring(0, nl) : condition);
                    }
                };
                ScenarioHooks.PhaseChanged = w._phase;
                ScenarioHooks.CastStage = w._stage;
                ScenarioHooks.SpellCast = w._cast;
                ScenarioHooks.CombatLog = w._log;
                // GameManager.LogMenuFlow writes with Debug.Log, which the harness's fast mode filters out
                // (filterLogType = Warning) before any listener sees it: let Log lines through while watching, and prove it.
                w._prevFilter = UnityEngine.Debug.unityLogger.filterLogType;
                UnityEngine.Debug.unityLogger.filterLogType = UnityEngine.LogType.Log;
                UnityEngine.Application.logMessageReceived += w._console;
                _current = w;
                UnityEngine.Debug.Log(ProbeTag);
            }

            /// <summary>
            /// Stops the running watcher, puts back each hook it still holds and the console filter it set, and returns it
            /// (null when none ran). The scenario's OnSetup calls it too, before the job's fast mode is applied, so a watcher
            /// a failed Finalize left behind never runs into the next seed.
            /// </summary>
            public static AfterCombatEndWatch Stop()
            {
                AfterCombatEndWatch w = _current;
                _current = null;
                if (w == null)
                    return null;
                UnityEngine.Application.logMessageReceived -= w._console;
                UnityEngine.Debug.unityLogger.filterLogType = w._prevFilter;
                if (ScenarioHooks.PhaseChanged == w._phase) ScenarioHooks.PhaseChanged = w._prevPhase;
                if (ScenarioHooks.CastStage == w._stage) ScenarioHooks.CastStage = w._prevStage;
                if (ScenarioHooks.SpellCast == w._cast) ScenarioHooks.SpellCast = w._prevCast;
                if (ScenarioHooks.CombatLog == w._log) ScenarioHooks.CombatLog = w._prevLog;
                return w;
            }
        }

        /// <summary>
        /// CMB-101 (the cast half), fixed in batch 12 step 9: the cast core's Legality stage meets the grapple casting rule for
        /// every resolve kind, an area cast included (before it, the PC targeting tested it and the core only for a hold;
        /// GrappleSpellcastRefusal, PHB p.156: no somatic component while grappling). A Ui wizard 5 grappled at the start
        /// of its turn picks Burning Hands (V, S; PHB p.207) at the goblin 10 ft away: the cast is refused with one log line,
        /// nothing is spent and the goblin is untouched. Since the step 9 review the PC targeting runs the same test
        /// (CheckCastGrappleLegality) before its prompts for early feedback, so the Ui refusal comes before the aim; the
        /// Legality stage's own refusal of an area request is pinned by CastCoreStageOrderTests and by the AI's area filter.
        /// </summary>
        private static ScenarioDef GrappledCasterLegalityUiArea()
        {
            return Rules("rules/grappled-caster-legality-ui-area", "A grappled Ui caster cannot cast a somatic area spell: refused, nothing spent (PHB p.156; CMB-101)")
                .Covers("CMB-101", "PHB p.156", "PHB p.207", "batch 12 step 9")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Grappled Wizard", 5)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 7, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.BURNING_HANDS))
                .Initiative("wizard", "gob")
                .Turn("wizard", 1,
                    Step.Assert("grappled", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        w.ApplyCondition(CombatConditionType.Grappled, 10, "scenario");
                        Fact(ctx, "grappled", w.HasCondition(CombatConditionType.Grappled));
                        RecordHp(ctx, "gob");
                        return true;
                    }),
                    RecordCaster("wizard"),
                    Step.Cast(SpellNames.BURNING_HANDS, "gob"),
                    FactCaster("cast", "wizard"),
                    Step.Assert("after", ctx => { FactHpLost(ctx, "cone", "gob"); return true; }))
                .Expect("The wizard is grappled when it casts", FactIs("grappled", true))
                .Expect("The cast is refused: nothing is spent (PHB p.156)", FactsAre(("cast.spent", "none"), ("cast.actions", "smf")))
                .Expect("The goblin is untouched", FactIs("cone.lost.gob", 0))
                .Expect("The refusal is logged once", Expect.Count("log", e => (e.Str("text") ?? "").Contains("spells with somatic components are blocked"), 1, 1))
                .Expect("No Burning Hands is cast", CastEvents("wizard", SpellNames.BURNING_HANDS, 0))
                .Build();
        }

        /// <summary>
        /// CMB-101, the AI half (batch 12 step 19; PHB p.156): an AI-run wizard 5 whose only prepared spells are Magic
        /// Missile (V, S) and Blindness/Deafness (V only) is grappled by a Scripted grapple-drill orc in round 1 (touch and
        /// grapple checks forced). On its turn the AI grapple turn's cast option (AIService.TryGrappledSpellcastForAI: the
        /// weaker grappler casts) ranks its casts through the shared cast legality (CheckLegalityForAI meets
        /// GrappleSpellcastRefusal): it casts Blindness/Deafness, the one spell with no somatic component, and never Magic
        /// Missile.
        /// </summary>
        private static ScenarioDef GrappledNpcCastsLegalOnly()
        {
            return Rules("rules/grappled-npc-casts-legal-only", "A grappled AI caster casts only what the grapple allows: no somatic component (PHB p.156; CMB-101)")
                .Covers("CMB-101", "PHB p.156", "PC_NPC_PARITY", "batch 12 step 19")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Grappled Wizard", 5)), 10, 10, Control.Ai)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("wizard", c => OnlyPrepared(c, (1, SpellNames.MAGIC_MISSILE), (2, SpellNames.BLINDNESS_DEAFNESS)))
                .Tweak("orc", Sturdy)
                .Initiative("orc", "wizard")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Turn("orc", 1, Step.Maneuver(SpecialAttackType.Grapple, "wizard"))
                .Expect("The wizard is grappling when its turn starts", v =>
                {
                    JsonObj w = v.Snapshot("wizard", 1, "wizard");
                    return w != null && w.Get("gr") is bool g && g ? ExpectResult.Pass("grappling") : ExpectResult.Fail("not grappling at its turn");
                })
                .Expect("It casts Blindness/Deafness (V only) and never Magic Missile (somatic)", Expect.All(
                    CastEvents("wizard", SpellNames.BLINDNESS_DEAFNESS, 1),
                    CastEvents("wizard", SpellNames.MAGIC_MISSILE, 0)))
                .Build();
        }

        /// <summary>Empties every prepared slot of <paramref name="c"/> (cantrips included), then prepares one slot of each (level, spell) given.</summary>
        private static void OnlyPrepared(CharacterController c, params (int level, string spellId)[] spells)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting");
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null && !slot.IsDomainSlot)
                    slot.PreparedSpell = null;
            sc.SyncPreparedSpellsFromSlots();
            foreach ((int level, string spellId) in spells)
                Prepare(c, level, spellId);
        }

        /// <summary>
        /// Batch 12 step 9 review (CMB-101): the grapple casting rule counts the cast's own casting time. A grappler may cast
        /// only a spell of 1 standard action or less with no somatic component (PHB p.156), and a spontaneous cast with a
        /// metamagic feat takes a full-round action (PHB p.88), so a sorcerer's stilled Magic Missile (Still Spell clears
        /// the somatic component, PHB p.101) is still refused in a grapple. Two sorcerers 6 with Still Spell, both grappled at
        /// the start of their turns, cast a stilled Magic Missile at a dummy: the Ui one (the PC targeting's early test,
        /// the same CheckCastGrappleLegality the Legality stage runs) and the Scripted one (the NPC cast path, refused at
        /// the cast core's Legality stage). Each refusal is logged once; nothing is spent and the dummies are untouched.
        /// </summary>
        private static ScenarioDef GrappleSpontaneousStillCast()
        {
            var still = new MetamagicData();
            still.Toggle(MetamagicFeatId.StillSpell);
            return Rules("rules/grapple-spontaneous-still-cast", "A grappled sorcerer cannot cast a spontaneous stilled spell: a full-round casting (PHB p.88, p.156; CMB-101)")
                .Covers("CMB-101", "PHB p.88", "PHB p.101", "PHB p.156", "PC_NPC_PARITY", "batch 12 step 9")
                .MaxRounds(1)
                .Pc("sorc", ActorSource.Stats(() => Sorcerer("Grappled Sorcerer", 6, "Still Spell")), 3, 4, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Sorcerer("Grappled Scripted Sorcerer", 6, "Still Spell")), 3, 12, Control.Scripted)
                .Npc("d1", "target_dummy", 8, 4, Control.Idle)
                .Npc("d2", "target_dummy", 8, 12, Control.Idle)
                .Tweak("sorc", KnowsMagicMissile)
                .Tweak("scripted", KnowsMagicMissile)
                .Tweak("d1", Sturdy).Tweak("d2", Sturdy)
                .Initiative("sorc", "scripted", "d1", "d2")
                .Turn("sorc", 1,
                    Step.Assert("grappled", ctx => GrappleAndRecord(ctx, "sorc", "d1")),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d1", still),
                    Step.Assert("after", ctx => FactAfterStillCast(ctx, "sorc", "d1")))
                .Turn("scripted", 1,
                    Step.Assert("grappled", ctx => GrappleAndRecord(ctx, "scripted", "d2")),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "d2", still),
                    Step.Assert("after", ctx => FactAfterStillCast(ctx, "scripted", "d2")))
                .Expect("Both sorcerers are grappled when they cast", FactsAre(("sorc.grappled", true), ("scripted.grappled", true)))
                .Expect("No missile: both dummies are untouched", FactsAre(("missile.lost.d1", 0), ("missile.lost.d2", 0)))
                .Expect("Nothing is spent: spells per day and every action kept (PHB p.141)", Expect.All(
                    FactsEqual("sorc.perday.before", "sorc.perday.after"), FactsEqual("scripted.perday.before", "scripted.perday.after"),
                    FactsAre(("sorc.actions", "smf"), ("scripted.actions", "smf"))))
                .Expect("Each refusal is logged once with the casting-time reason", Expect.All(
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("Grappled Sorcerer cannot cast Magic Missile") && (e.Str("text") ?? "").Contains("full-round action"), 1, 1),
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("Grappled Scripted Sorcerer cannot cast Magic Missile") && (e.Str("text") ?? "").Contains("full-round action"), 1, 1)))
                .Expect("No Magic Missile is cast", Expect.All(CastEvents("sorc", SpellNames.MAGIC_MISSILE, 0), CastEvents("scripted", SpellNames.MAGIC_MISSILE, 0)))
                .Build();
        }

        private static bool GrappleAndRecord(ScenarioContext ctx, string key, string dummy)
        {
            CharacterController c = ctx.Get(key);
            c.ApplyCondition(CombatConditionType.Grappled, 10, "scenario");
            Fact(ctx, key + ".grappled", c.HasCondition(CombatConditionType.Grappled));
            Fact(ctx, key + ".perday.before", SpellsPerDay(c));
            RecordHp(ctx, dummy);
            return true;
        }

        private static bool FactAfterStillCast(ScenarioContext ctx, string key, string dummy)
        {
            CharacterController c = ctx.Get(key);
            FactHpLost(ctx, "missile", dummy);
            Fact(ctx, key + ".perday.after", SpellsPerDay(c));
            Fact(ctx, key + ".actions", Actions(c));
            return true;
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
            /// <summary>
            /// A further caster state both sides record after the casts as the facts "ui.extra" and "scripted.extra" (only
            /// the families that set it, so the other pairs keep their traces): Mirror Image's figure count.
            /// </summary>
            public Func<CharacterController, string> CasterFact;
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
            // Sound Burst is an area spell since batch 11 step 23 (SPL-046): its five pairs (failed save, spell resistance,
            // saved, deafened, silenced) became the one Ui area scenario rules/sound-burst-area; the NPC side casts it as
            // an area since batch 11 step 25 (rules/ai-area-aim).
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
            // Batch 11 step 27 (step 26 made Mirror Image one RAW model for both sides, SPL-117): the NPC caster casts it like
            // any buff. Wizard 5: 1d4 (4) + 5/3 = 5 images (PHB p.254), 5 minutes (50 rounds) on both.
            yield return new PairSpec { Family = "mirror-image", SpellId = SpellNames.MIRROR_IMAGE, Caster = wizard, SlotLevel = 2,
                Title = "Mirror Image on the caster (PHB p.254)", Covers = new[] { "PHB p.254", "SPL-117" },
                CasterFact = c => "images=" + MirrorImageRules.ImagesOf(c),
                SharedPins = b => b.Expect("Both casters get 1d4 (4) + 5/3 = 5 images (PHB p.254; SPL-117)", FactsAre(("ui.extra", "images=5"), ("scripted.extra", "images=5"))) };
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
                        if (p.CasterFact != null)
                        {
                            Fact(ctx, "ui.extra", p.CasterFact(ctx.Get("ui")));
                            Fact(ctx, "scripted.extra", p.CasterFact(ctx.Get("scripted")));
                        }
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

        // ── Batch 11 step 27: NPC summons, Dimension Door and held charges (SPL-091, SPL-115) ──

        /// <summary>The living summoned creatures on the field (GameManager.IsSummonedCreature).</summary>
        private static List<CharacterController> LivingSummons()
        {
            GameManager gm = GameManager.Instance;
            return gm == null
                ? new List<CharacterController>()
                : gm.Combat_GetAllCharacters().Where(c => c != null && c.Stats != null && !c.Stats.IsDead && gm.IsSummonedCreature(c)).ToList();
        }

        /// <summary>The position of <paramref name="c"/> in the initiative order (TurnService.InitiativeOrder), or -1.</summary>
        private static int InitiativeIndex(CharacterController c)
        {
            TurnService turns = GameManager.Instance != null ? GameManager.Instance.GetComponent<TurnService>() : null;
            if (turns == null || c == null)
                return -1;
            IReadOnlyList<TurnService.InitiativeEntry> order = turns.InitiativeOrder;
            for (int i = 0; i < order.Count; i++)
                if (order[i].Character == c)
                    return i;
            return -1;
        }

        /// <summary>
        /// SPL-091 (PHB p.285-286; batch 11 step 27): a Scripted Enemy-team cleric (human_cleric, Cleric 5, neutral good)
        /// casts Summon Monster II through the NPC summon cast, the AI decider choosing the creature (the list II creature
        /// of the highest challenge rating it may call, AiCastChoiceHeuristics.ChooseSummonOption) and its square (beside
        /// the hero, 4 squares away, within close range). One 2nd-level slot is spent and one creature appears (a list of
        /// the spell's own level: one creature, PHB p.285), fights for the cleric's side under the AI, is inserted into the
        /// initiative right after the cleric (as a PC's summon, SPL-164) and takes its turn before the hero's.
        /// </summary>
        private static ScenarioDef NpcSummonMonster()
        {
            return Rules("rules/npc-summon-monster", "A Scripted NPC cleric's Summon Monster II brings a creature that joins the initiative on its side and acts (PHB p.285; SPL-091)")
                .Covers("SPL-091", "PHB p.285", "PHB p.286", "SPL-164", "AI-015", "batch 11 step 27")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Wizard("Summon Target", 1)), 10, 10, Control.Scripted)
                .Npc("cleric", "human_cleric", 6, 10, Control.Scripted)
                .Tweak("cleric", c => Prepare(c, 2, SpellNames.SUMMON_MONSTER_2))
                .Tweak("hero", Sturdy)
                .Initiative("cleric", "hero")
                .Turn("cleric", 1,
                    RecordCaster("cleric"),
                    Step.Cast(SpellNames.SUMMON_MONSTER_2, "hero"),
                    FactCaster("cast", "cleric"),
                    Step.Assert("the summoned creature", ctx =>
                    {
                        CharacterController cleric = ctx.Get("cleric");
                        List<CharacterController> summons = LivingSummons();
                        if (summons.Count == 0)
                            return true;
                        CharacterController m = summons[0];
                        GameManager gm = GameManager.Instance;
                        SummonMonsterOption expected = AiCastChoiceHeuristics.ChooseSummonOption(
                            SummonMonsterLists.GetFilteredOptionsForListLevel(2, cleric.Stats));
                        Fact(ctx, "summon.aiChoice", expected != null && m.Stats.CharacterName.StartsWith(expected.BuildUiLabel(), StringComparison.Ordinal));
                        Fact(ctx, "summon.team", m.Team);
                        Fact(ctx, "summon.controllable", m.IsControllable);
                        Fact(ctx, "summon.caster", gm.GetSummonCasterForAI(m) == cleric);
                        Fact(ctx, "summon.afterCaster", InitiativeIndex(m) == InitiativeIndex(cleric) + 1);
                        Fact(ctx, "summon.besideHero", CasterLevelRules.DistanceToTarget(m, ctx.Get("hero")) <= 1);
                        return true;
                    }))
                .Turn("hero", 1, Step.Assert("after the summon's turn", ctx =>
                {
                    List<CharacterController> summons = LivingSummons();
                    if (summons.Count > 0)
                    {
                        ActionEconomy a = summons[0].Actions;
                        Fact(ctx, "summon.acted", !(a.HasStandardAction && a.HasMoveAction && a.HasFullRoundAction));
                    }
                    return true;
                }))
                .Expect("The summon is in the trace once (ScenarioHooks.SpellCast)", CastEvents("cleric", SpellNames.SUMMON_MONSTER_2, 1))
                .Expect("It spends one 2nd-level slot and the full-round action and brings one creature (PHB p.285, p.174)",
                    FactsAre(("summons.before", 0), ("cast.spent", "L2+1"), ("cast.summons", 1), ("cast.actions", "---")))
                .Expect("The creature is the AI's pick, beside the hero, on the cleric's side and run by the AI",
                    FactsAre(("summon.aiChoice", true), ("summon.besideHero", true), ("summon.team", "Enemy"), ("summon.controllable", false), ("summon.caster", true)))
                .Expect("Pinned (SPL-164, SPL-162): today the cast resolves in the casting turn and the creature joins the initiative right after the cleric, acting before the hero; RAW it appears just before the cleric's next turn and acts at once",
                    FactsAre(("summon.afterCaster", true), ("summon.acted", true)))
                .Build();
        }

        /// <summary>
        /// SPL-091 (PHB p.221; batch 11 step 27): an AI-run wizard 7 at 5 hit points of 42 (12%, in danger:
        /// AISpellcastingStrategist.GetEscapeScore) beside a goblin (-30 on attacks, 100 more hit points), its only unspent
        /// slot holding Dimension Door. The AI casts it, the AI decider choosing the destination as the PC's escape square
        /// (GameManager.ChooseDimensionDoorDestination: the free square farthest from the nearest foe, at least 2 squares
        /// away), and the wizard can take no other action that turn (PHB p.221). Replaces rules/cast-ai-skips-dimension-door,
        /// which pinned step 6's selection exclusion. The slot test reads the class table, which stops at 2nd-level wizard
        /// slots (SPL-041), so the scenario widens it through the editor-only seam, as rules/npc-cast-fireball does.
        /// </summary>
        private static ScenarioDef NpcDimensionDoor()
        {
            return Rules("rules/npc-dimension-door", "An AI-run wizard in danger casts Dimension Door to the escape square it chose (PHB p.221; SPL-091)")
                .Covers("SPL-091", "PHB p.221", "batch 11 step 27")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("AI Wizard", 7)), 3, 10, Control.Ai)
                .Npc("goblin", "goblin", 4, 10, Control.Scripted)
                .Hp("wizard", 5)
                .Tweak("wizard", c => { AddWizardSlot(c, 4, SpellNames.DIMENSION_DOOR); SpendAllBut(c, SpellNames.DIMENSION_DOOR); WidenNpcSlotTable(c, 4); })
                .Tweak("goblin", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("wizard", "goblin")
                .Turn("goblin", 1, Step.Assert("after the wizard's turn", ctx =>
                {
                    CharacterController w = ctx.Get("wizard");
                    Fact(ctx, "away", CasterLevelRules.DistanceToTarget(w, ctx.Get("goblin")) >= 2);
                    Fact(ctx, "moved", w.GridPosition != new UnityEngine.Vector2Int(3, 10));
                    Fact(ctx, "actions", Actions(w));
                    return true;
                }))
                .Expect("The AI wizard casts Dimension Door once", CastEvents("wizard", SpellNames.DIMENSION_DOOR, 1))
                .Expect("It teleports away from the goblin, to a square at least 2 squares from it", FactsAre(("moved", true), ("away", true)))
                .Expect("After Dimension Door it can take no other action that turn (PHB p.221)", FactIs("actions", "---"))
                .Expect("The move is a teleport", Expect.Count("move", e => e.Str("actor") == "wizard" && e.Str("type") == "teleport", 1, 1))
                .Build();
        }

        /// <summary>
        /// SPL-115 (PHB p.141, p.176; batch 11 step 27): an AI-run wizard 5 whose only unspent slot holds Shocking Grasp,
        /// beside a target dummy (100 more hit points). Round 1: the AI casts it now (the dummy is within reach,
        /// AiCastDecider.TouchDeliveryFor), the touch misses (d20 1), and the spell is not discharged: the wizard holds the
        /// charge (delegated RAW reading 2026-10-14; before, a missed touch spent the spell). Round 2: the AI delivers the
        /// held charge at its turn's start through the PC's delivery request (the touch d20 19 hits; 5d6 = 30, PHB p.279),
        /// which clears it. The delivery is no cast (one cast event); since batch 12 step 18 it takes the wizard's attack
        /// action, as the PC's does (SPL-156; rules/held-charge-costs-attack-ai).
        /// </summary>
        private static ScenarioDef NpcHeldTouch()
        {
            return Rules("rules/npc-held-touch", "An AI wizard's missed Shocking Grasp keeps its charge and is delivered the next round (PHB p.141; SPL-115)")
                .Covers("SPL-115", "PHB p.141", "PHB p.176", "PHB p.279", "SPL-156", "batch 11 step 27")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("AI Wizard", 5)), 5, 10, Control.Ai)
                .Npc("dummy", "target_dummy", 6, 10, Control.Scripted)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.SHOCKING_GRASP); SpendAllBut(c, SpellNames.SHOCKING_GRASP); })
                .Tweak("dummy", Sturdy)
                .Initiative("wizard", "dummy")
                .Force(20, 1, SpellCaster.TouchAttackContext, 1)
                .Force(20, 19, SpellCaster.TouchAttackContext, 1)
                .Force(6, 6, null, -1)
                .Turn("dummy", 1, Step.Assert("after round 1", ctx =>
                {
                    CharacterController d = ctx.Get("dummy");
                    Fact(ctx, "held.r1", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge);
                    Fact(ctx, "lost.r1", d.Stats.TotalMaxHP - d.Stats.CurrentHP);
                    return true;
                }))
                .Turn("dummy", 2, Step.Assert("after round 2", ctx => { Fact(ctx, "held.r2", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge); return true; }))
                .Expect("The AI casts Shocking Grasp once; the delivery is no cast", CastEvents("wizard", SpellNames.SHOCKING_GRASP, 1))
                .Expect("The missed touch deals nothing and the wizard holds the charge (PHB p.141)", FactsAre(("held.r1", true), ("lost.r1", 0)))
                .Expect("Round 2: the held charge hits for 5d6 = 30 (PHB p.279)",
                    v => v.Of("hp").Any(e => e.Round == 2 && e.Str("actor") == "dummy" && e.Int("old") - e.Int("new") == 30)
                        ? ExpectResult.Pass("the dummy lost 30 in round 2")
                        : ExpectResult.Fail("no 30-point loss of the dummy in round 2"))
                .Expect("The delivered charge is gone", FactIs("held.r2", false))
                .Build();
        }

        /// <summary>
        /// SPL-115 (PHB p.141; batch 11 step 27): an AI-run wizard 5 whose only unspent slot holds Shocking Grasp, a target
        /// dummy 3 squares away (100 more hit points). The dummy is out of reach but a move reaches it, so the AI casts the
        /// spell and holds the charge (AiCastDecider.TouchDeliveryFor answers HoldCharge; the hold is the PC's Discharge
        /// Later through the cast core), moves beside the dummy and touches it in the same turn (PHB p.141: in the casting
        /// round the caster may move between casting and touching): d20 19 hits, 5d6 = 30.
        /// </summary>
        private static ScenarioDef NpcHoldChargeApproach()
        {
            return Rules("rules/npc-hold-charge-approach", "An AI wizard holds Shocking Grasp, moves into reach and touches in the same turn (PHB p.141; SPL-115)")
                .Covers("SPL-115", "PHB p.141", "PHB p.279", "batch 11 step 27")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("AI Wizard", 5)), 5, 10, Control.Ai)
                .Npc("dummy", "target_dummy", 8, 10, Control.Scripted)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.SHOCKING_GRASP); SpendAllBut(c, SpellNames.SHOCKING_GRASP); })
                .Tweak("dummy", Sturdy)
                .Initiative("wizard", "dummy")
                .Force(20, 19, SpellCaster.TouchAttackContext, 1)
                .Force(6, 6, null, -1)
                .Turn("dummy", 1, Step.Assert("after the wizard's turn", ctx =>
                {
                    CharacterController w = ctx.Get("wizard"), d = ctx.Get("dummy");
                    Fact(ctx, "held", w.Spellcasting.HasHeldTouchCharge);
                    Fact(ctx, "beside", CasterLevelRules.DistanceToTarget(w, d) <= 1);
                    Fact(ctx, "lost", d.Stats.TotalMaxHP - d.Stats.CurrentHP);
                    return true;
                }))
                .Expect("The AI casts Shocking Grasp once (the hold); the delivery is no cast", CastEvents("wizard", SpellNames.SHOCKING_GRASP, 1))
                .Expect("It moves beside the dummy and the touch hits for 5d6 = 30, spending the charge (PHB p.141, p.279)",
                    FactsAre(("beside", true), ("lost", 30), ("held", false)))
                .Build();
        }

        /// <summary>
        /// SPL-115 (PHB p.141, p.176; batch 11 step 27), the PC side of rules/npc-held-touch: a Ui wizard 5 casts Shocking
        /// Grasp with Cast Now (the Cast step's answer to the touch prompt) at an adjacent dummy; the touch misses (d20 1), so
        /// the spell is not discharged and the wizard holds the charge (the same cast-core rule as an NPC's,
        /// MissedTouchHoldsCharge; delegated RAW reading 2026-10-14). Round 2: the Discharge button delivers it (d20 19 hits,
        /// 5d6 = 30, PHB p.279), taking the attack action since batch 12 step 18 (SPL-156). One slot, one cast event.
        /// </summary>
        private static ScenarioDef UiTouchMissHoldsCharge()
        {
            return Rules("rules/ui-touch-miss-holds-charge", "A Ui wizard's missed Cast Now Shocking Grasp holds its charge; the next round's delivery hits (PHB p.141; SPL-115)")
                .Covers("SPL-115", "PHB p.141", "PHB p.176", "PHB p.279", "batch 11 step 27")
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
                    Step.Cast(SpellNames.SHOCKING_GRASP, "dummy"),
                    Step.Assert("after the miss", ctx =>
                    {
                        Fact(ctx, "held.miss", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge);
                        FactSlotsSpent(ctx, "spent.cast", "wizard");
                        FactHpLost(ctx, "miss", "dummy");
                        return true;
                    }))
                .Turn("wizard", 2,
                    Step.DeliverHeld("dummy"),
                    Step.Assert("after the hit", ctx =>
                    {
                        Fact(ctx, "held.hit", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge);
                        FactHpLost(ctx, "hit", "dummy");
                        return true;
                    }))
                .Expect("The Cast Now spends one 1st-level slot; the missed touch deals nothing and the charge is held (PHB p.141)",
                    FactsAre(("spent.cast", "L1+1"), ("miss.lost.dummy", 0), ("held.miss", true)))
                .Expect("Round 2: the held charge hits for 5d6 = 30 and is spent (PHB p.279)", FactsAre(("hit.lost.dummy", 30), ("held.hit", false)))
                .Expect("One cast; the delivery is no cast", CastEvents("wizard", SpellNames.SHOCKING_GRASP, 1))
                .Build();
        }

        /// <summary>
        /// SPL-156 (batch 12 step 18; PHB p.140-141, p.176; delegated RAW reading 2026-10-14), the PC side: a Ui wizard 5
        /// (BAB +2: one attack a round) casts Shocking Grasp with Cast Now at an adjacent dummy; the touch misses (d20 1) and
        /// the charge is held. That touch was the casting's own, and the cast spent the standard action, so a Discharge in
        /// the same round is refused: a later touch is a melee touch attack made instead of a regular attack (PHB p.141), and
        /// no attack is left. Round 2: the Discharge button delivers it as the turn's attack (the standard action), the touch
        /// misses again (d20 1) and the charge stays; a second Discharge is refused, since a BAB +2 caster has no second
        /// attack. A d20 19 waits in the forced queue, so a touch that should not happen would hit for 30. Before the fix
        /// every delivery was free and the PC could press Discharge again and again.
        /// </summary>
        private static ScenarioDef HeldChargeCostsAttackUi()
        {
            return Rules("rules/held-charge-costs-attack-ui", "A Ui held touch charge costs an attack after its casting: one touch a turn at BAB +2 (PHB p.141; SPL-156)")
                .Covers("SPL-156", "PHB p.141", "PHB p.176", "batch 12 step 18")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Pin Wizard", 5)), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 6, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.SHOCKING_GRASP))
                .Tweak("dummy", Sturdy)
                .Initiative("wizard", "dummy")
                .Force(20, 1, SpellCaster.TouchAttackContext, 1)
                .Force(20, 1, SpellCaster.TouchAttackContext, 1)
                .Force(20, 19, SpellCaster.TouchAttackContext, 1)
                .Force(6, 6, null, -1)
                .Turn("wizard", 1,
                    Step.Assert("record", ctx => { RecordHp(ctx, "dummy"); return true; }),
                    Step.Cast(SpellNames.SHOCKING_GRASP, "dummy"),
                    Step.DeliverHeld("dummy"),
                    Step.Assert("round 1", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "r1.held", w.Spellcasting.HasHeldTouchCharge);
                        Fact(ctx, "r1.actions", Actions(w));
                        FactHpLost(ctx, "r1", "dummy");
                        return true;
                    }))
                .Turn("wizard", 2,
                    Step.DeliverHeld("dummy"),
                    Step.Assert("round 2 first touch", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "r2a.held", w.Spellcasting.HasHeldTouchCharge);
                        Fact(ctx, "r2a.actions", Actions(w));
                        return true;
                    }),
                    Step.DeliverHeld("dummy"),
                    Step.Assert("round 2 second touch", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "r2b.held", w.Spellcasting.HasHeldTouchCharge);
                        Fact(ctx, "r2b.actions", Actions(w));
                        FactHpLost(ctx, "r2b", "dummy");
                        return true;
                    }))
                .Expect("The wizard's turns are Ui turns", Expect.Controller("wizard", "ui"))
                .Expect("Round 1: the missed Cast Now touch keeps the charge; a Discharge in the same round is refused (no attack left), nothing lands",
                    FactsAre(("r1.held", true), ("r1.actions", "-m-"), ("r1.lost.dummy", 0)))
                .Expect("Round 2: the delivery takes the attack action (the standard action) and the missed touch keeps the charge (PHB p.141)",
                    FactsAre(("r2a.held", true), ("r2a.actions", "-m-")))
                .Expect("Round 2: a second Discharge is refused (BAB +2: one attack), nothing more spent, the forced 19 never rolled",
                    FactsAre(("r2b.held", true), ("r2b.actions", "-m-"), ("r2b.lost.dummy", 0)))
                .Expect("One cast; the deliveries are no casts", CastEvents("wizard", SpellNames.SHOCKING_GRASP, 1))
                .Build();
        }

        /// <summary>
        /// SPL-156 (batch 12 step 18; PHB p.141, p.176), the NPC side of rules/held-charge-costs-attack-ui: an AI-run wizard 5
        /// whose only unspent slot holds Shocking Grasp, beside a target dummy. Round 1: it casts it now and the touch misses
        /// (d20 1): the charge is held. Round 2: it delivers the held charge at its turn's start through the attack-sequence
        /// executor (the same request and cost as the PC's Discharge), the touch misses again (d20 1) and its standard
        /// action is spent; it makes no second touch that turn (BAB +2), so the forced d20 19 is never rolled. Before the
        /// fix the delivery was free and the AI limited itself to one try a turn by an AI-only rule (_heldChargeTriedThisTurn).
        /// </summary>
        private static ScenarioDef HeldChargeCostsAttackAi()
        {
            return Rules("rules/held-charge-costs-attack-ai", "An AI wizard's held touch charge costs its attack after the casting: one touch a turn (PHB p.141; SPL-156)")
                .Covers("SPL-156", "SPL-115", "PHB p.141", "PHB p.176", "batch 12 step 18")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("AI Wizard", 5)), 5, 10, Control.Ai)
                .Npc("dummy", "target_dummy", 6, 10, Control.Scripted)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.SHOCKING_GRASP); SpendAllBut(c, SpellNames.SHOCKING_GRASP); })
                // Fixture: the dummy makes no AoOs, so the wizard's retreat after its touch cannot cost it the charge.
                .Tweak("dummy", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Initiative("wizard", "dummy")
                .Force(20, 1, SpellCaster.TouchAttackContext, 1)
                .Force(20, 1, SpellCaster.TouchAttackContext, 1)
                .Force(20, 19, SpellCaster.TouchAttackContext, 1)
                .Force(6, 6, null, -1)
                .Turn("dummy", 1, Step.Assert("after round 1", ctx =>
                {
                    CharacterController d = ctx.Get("dummy");
                    Fact(ctx, "r1.held", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge);
                    Fact(ctx, "r1.lost", d.Stats.TotalMaxHP - d.Stats.CurrentHP);
                    return true;
                }))
                .Turn("dummy", 2, Step.Assert("after round 2", ctx =>
                {
                    CharacterController w = ctx.Get("wizard"), d = ctx.Get("dummy");
                    Fact(ctx, "r2.held", w.Spellcasting.HasHeldTouchCharge);
                    Fact(ctx, "r2.standard", w.Actions.HasStandardAction);
                    Fact(ctx, "r2.lost", d.Stats.TotalMaxHP - d.Stats.CurrentHP);
                    return true;
                }))
                .Expect("The AI casts Shocking Grasp once; the delivery is no cast", CastEvents("wizard", SpellNames.SHOCKING_GRASP, 1))
                .Expect("Round 1: the missed touch keeps the charge (PHB p.141)", FactsAre(("r1.held", true), ("r1.lost", 0)))
                .Expect("Round 2: the delivery takes the standard action, misses and keeps the charge; no second touch (the 19 never lands)",
                    FactsAre(("r2.held", true), ("r2.standard", false), ("r2.lost", 0)))
                .Build();
        }

        /// <summary>
        /// SPL-156 (batch 12 step 18 review; PHB p.141, p.143): a held charge's touch made as a later attack of a full attack
        /// rolls at that attack's iterative bonus. A Ui wizard 12 (BAB +6/+1, STR 10, Medium: touch bonus +6) holding a
        /// Shocking Grasp charge from an earlier turn attacks an adjacent dummy with a dagger (the attack action, at +6),
        /// then presses Discharge on it: the touch is the second step of the attack sequence, which makes the turn a full
        /// attack (the move action spent), and its modifier is +1, not +6.
        /// </summary>
        private static ScenarioDef HeldChargeTouchIterativeBabUi()
        {
            return Rules("rules/held-charge-touch-iterative-bab-ui", "A held touch made as a full attack's second attack rolls at that attack's BAB, +1 for BAB +6 (PHB p.141, p.143; SPL-156)")
                .Covers("SPL-156", "PHB p.141", "PHB p.143", "batch 12 step 18")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Iterative Wizard", 12)), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 6, 10, Control.Idle)
                .Tweak("wizard", c =>
                {
                    global::Inventory inv = c.InventoryComp.CharacterInventory;
                    inv.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponDagger), EquipSlot.RightHand);
                    inv.RecalculateStats();
                    GiveHeldShockingGrasp(c);
                })
                .Tweak("dummy", Sturdy)
                .Initiative("wizard", "dummy")
                .Force(20, 10, SpellCaster.TouchAttackContext, 1)
                .Turn("wizard", 1,
                    Step.Attack("dummy"),
                    Step.Assert("after the weapon attack", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "bab", w.Stats.BaseAttackBonus);
                        Fact(ctx, "steps.before", w.ProgressiveAttackPool.MainHandStepsUsed);
                        return true;
                    }),
                    Step.DeliverHeld("dummy"),
                    Step.Assert("after the touch", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Fact(ctx, "steps.after", w.ProgressiveAttackPool.MainHandStepsUsed);
                        Fact(ctx, "move", w.Actions.HasMoveAction);
                        return true;
                    }))
                .Expect("The wizard's turn is a Ui turn", Expect.Controller("wizard", "ui"))
                .Expect("BAB +6; the dagger attack is step 1, the touch step 2, and the second step makes it a full attack (PHB p.143)",
                    FactsAre(("bab", 6), ("steps.before", 1), ("steps.after", 2), ("move", false)))
                .Expect("The touch's attack modifier is the second attack's +1 (PHB p.141, p.143)",
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("Melee Touch Attack") && (e.Str("text") ?? "").Contains("+ 1 (attack modifier)"), 1, 1))
                .Expect("No touch rolls at the full +6", Expect.None("log", e => (e.Str("text") ?? "").Contains("Melee Touch Attack") && (e.Str("text") ?? "").Contains("+ 6 (attack modifier)")))
                .Build();
        }

        /// <summary>Gives <paramref name="c"/> a held Shocking Grasp charge (a clone, never the template), as a hold leaves it.</summary>
        private static bool GiveHeldShockingGrasp(CharacterController c)
        {
            c.Spellcasting.SetHeldTouchCharge(SpellDatabase.GetSpell(SpellNames.SHOCKING_GRASP).Clone(), null);
            return c.Spellcasting.HasHeldTouchCharge;
        }

        private const string HeldChargeDissipatedLog = "held touch charge dissipates";

        /// <summary>
        /// Batch 11 step 27 (PHB p.141, p.176: casting another spell dissipates a held touch spell): a threatened Ui wizard 3
        /// holding a Shocking Grasp charge picks Magic Missile and cancels at the AoO prompt: nothing is cast, so the charge
        /// is kept (the spell menu only warns, ConcentrationRules.HeldChargeCastWarning); it casts Magic Missile again and
        /// goes on: the cast core's ConcentrationOnCast step (DissipateHeldChargeForCasting) ends the charge once, the same
        /// step as for an NPC (rules/held-charge-dissipates-npc).
        /// </summary>
        private static ScenarioDef UiHeldChargeDissipates()
        {
            return Rules("rules/held-charge-dissipates-ui", "A Ui caster's held charge survives a cast cancelled at the AoO prompt and dissipates when another spell is cast (PHB p.141, p.176)")
                .Covers("PHB p.141", "PHB p.176", "SPL-115", "batch 11 step 27")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Held Ui Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 6, 10, Control.Idle)
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.MAGIC_MISSILE, 2); Sturdy(c); })
                .Tweak("gob", c => { AttackTweak(c, -30); Sturdy(c); })
                .Initiative("gob", "wizard")
                .Turn("wizard", 1,
                    Step.Assert("hold", ctx => GiveHeldShockingGrasp(ctx.Get("wizard"))),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob"),
                    Step.AnswerAoO(AoOAnswer.Cancel),
                    Step.Assert("after the cancel", ctx => { Fact(ctx, "held.cancel", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge); return true; }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob"),
                    Step.AnswerAoO(AoOAnswer.Proceed),
                    Step.Assert("after the cast", ctx => { Fact(ctx, "held.cast", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge); return true; }))
                .Expect("The first cast reached the AoO prompt and was cancelled; the second went on", Expect.All(
                    Expect.StepStatus("wizard", 1, "AnswerAoO(Cancel)", 0, "done"),
                    Expect.StepStatus("wizard", 1, "AnswerAoO(Proceed)", 0, "done")))
                .Expect("A cancelled cast keeps the charge; a cast that goes on dissipates it (PHB p.141, p.176)",
                    FactsAre(("held.cancel", true), ("held.cast", false)))
                .Expect("The charge dissipates once", Expect.Count("log", e => (e.Str("text") ?? "").Contains(HeldChargeDissipatedLog), 1, 1))
                .Build();
        }

        /// <summary>
        /// The NPC side of rules/held-charge-dissipates-ui (batch 11 step 27): a Scripted wizard 3 holding a Shocking Grasp
        /// charge casts Magic Missile at a dummy; the same cast-core step ends the charge once.
        /// </summary>
        private static ScenarioDef NpcHeldChargeDissipates()
        {
            return Rules("rules/held-charge-dissipates-npc", "A Scripted caster's held charge dissipates when it casts another spell (PHB p.141, p.176)")
                .Covers("PHB p.141", "PHB p.176", "SPL-115", "batch 11 step 27")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Held Scripted Wizard", 3)), 5, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.MAGIC_MISSILE))
                .Tweak("dummy", Sturdy)
                .Initiative("wizard", "dummy")
                .Turn("wizard", 1,
                    Step.Assert("hold", ctx => GiveHeldShockingGrasp(ctx.Get("wizard"))),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "dummy"),
                    Step.Assert("after the cast", ctx => { Fact(ctx, "held.cast", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge); return true; }))
                .Expect("The Scripted cast ran", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("The cast dissipates the held charge (PHB p.141, p.176)", FactIs("held.cast", false))
                .Expect("The charge dissipates once", Expect.Count("log", e => (e.Str("text") ?? "").Contains(HeldChargeDissipatedLog), 1, 1))
                .Build();
        }

        /// <summary>
        /// Batch 11 step 27 (delegated RAW reading 2026-10-14, SPL-114): a wand use is not casting a spell (DMG p.213 sets
        /// spell trigger activation apart from spell completion), so a Ui wizard 5 holding a Shocking Grasp charge who uses a
        /// wand of magic missile keeps the charge.
        /// </summary>
        private static ScenarioDef UiHeldChargeWandKeeps()
        {
            return Rules("rules/held-charge-wand-keeps", "A wand use does not dissipate a held charge (DMG p.213; SPL-114)")
                .Covers("DMG p.213", "PHB p.141", "SPL-114", "batch 11 step 27")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Held Wand Wizard", 5)), 5, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 9, 10, Control.Idle)
                .Tweak("wizard", c => GiveItem(c, WandId))
                .Tweak("dummy", Sturdy)
                .Initiative("wizard", "dummy")
                .Turn("wizard", 1,
                    Step.Assert("hold", ctx => GiveHeldShockingGrasp(ctx.Get("wizard"))),
                    Step.UseItem(WandId, "dummy"),
                    Step.Assert("after the wand", ctx => { Fact(ctx, "held.wand", ctx.Get("wizard").Spellcasting.HasHeldTouchCharge); return true; }))
                .Expect("The wand use ran", Expect.StepStatus("wizard", 1, "UseItem", 0, "done"))
                .Expect("The wizard still holds the charge", FactIs("held.wand", true))
                .Expect("Nothing dissipates", Expect.Count("log", e => (e.Str("text") ?? "").Contains(HeldChargeDissipatedLog), 0, 0))
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
        /// (touch, PHB p.279) and Ray of Frost (close range, 35 ft at CL 5, PHB p.269); the only enemy is a wolf 35 ft away.
        /// The AI's best-scored spell is Shocking Grasp (recorded through AIService.SelectSpell, which tests no target or
        /// range), which has no legal target: the AI's legality filter (GameManager.CheckLegalityForAI: out of range) drops
        /// it before any cast is tried, so it costs no try, and the wizard casts its next candidate, Ray of Frost, where the
        /// old single pick found no target and gave up its spellcasting for the turn. Since batch 11 step 27 the AI also holds
        /// a touch charge when a move this turn reaches the foe (SPL-115, rules/npc-hold-charge-approach), so the wizard's
        /// speed is cut to 20 ft. (4 squares), short of the wolf, to keep Shocking Grasp without a candidate. A cast refused
        /// after it was tried is rules/ai-spell-retry-refused.
        /// </summary>
        private static ScenarioDef AiSpellRetry()
        {
            return Rules("rules/ai-spell-retry", "When its first spell pick is illegal the AI casts its second (AI-063)")
                .Covers("AI-063", "PHB p.279", "PHB p.269", "batch 11 step 22")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("AI Wizard", 5)), 3, 10, Control.Ai)
                .Npc("wolf", "wolf", 10, 10, Control.Scripted)
                .Tweak("wizard", c =>
                {
                    AddWizardSlot(c, 1, SpellNames.SHOCKING_GRASP);
                    AddWizardSlot(c, 0, SpellNames.RAY_OF_FROST);
                    SpendAllBut(c, SpellNames.SHOCKING_GRASP, SpellNames.RAY_OF_FROST);
                    c.Stats.BaseSpeed = 4; // no move this turn reaches the wolf, so no held-charge candidate (batch 11 step 27)
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
                .Expect("The AI's first pick is Shocking Grasp, refused against the wolf 35 ft away (out-of-range)", FactsAre(("top", SpellNames.SHOCKING_GRASP), ("top.legal", "out-of-range")))
                .Expect("The wizard casts Ray of Frost, its second candidate", CastEvents("wizard", SpellNames.RAY_OF_FROST, 1))
                .Expect("The wizard never casts Shocking Grasp", Expect.Count("cast", e => e.Str("by") == "wizard" && e.Str("spell") == SpellNames.SHOCKING_GRASP, 0, 0))
                .Build();
        }

        // ── rules/npc-cast-fireball, -bless, -web, rules/ai-area-aim (batch 11 step 25, AI-001 area half) ──

        /// <summary>
        /// After <see cref="AddWizardSlot"/> for an NPC wizard: the NPC path's slot test (SpellcastingComponent.CanCast) also
        /// reads the class table, which stops at 2nd level from wizard 4 on (SPL-041), so the table is widened to the added
        /// slot's level (SpellcastingComponent.Harness_WidenClassSlotTable; a scenario setup, as the slot itself).
        /// </summary>
        private static void WidenNpcSlotTable(CharacterController c, int level)
            => c.Spellcasting.Harness_WidenClassSlotTable("Wizard", level);

        /// <summary>
        /// AI-001 (area half; batch 11 step 25): an AI-run Enemy-team wizard (mist_wizard, Wizard 5, the Evoker profile)
        /// holds only Fireball (PHB p.231: 20-ft.-radius spread, Long range). Three heroes stand together at (10,10),
        /// (11,10) and (10,11); the wizard's kobold ally stands at (13,13), inside the burst of some origins that take the
        /// heroes (+(12,12) takes all four) and outside others (+(10,10)). The AI aims the burst through its search
        /// (AreaAimSearch) and casts it through the same area resolve as a PC (SpellCastRequest.ForAIArea): every hero
        /// takes the 5d6 (forced to 30, failed saves) and the kobold takes nothing. Before this step the NPC cast path
        /// refused every Area spell (NpcRefusesArea).
        /// </summary>
        private static ScenarioDef NpcCastFireball()
        {
            return Rules("rules/npc-cast-fireball", "An Enemy-team AI wizard aims Fireball at the party and spares its ally (AI-001)")
                .Covers("AI-001", "PHB p.231", "PHB p.175", "batch 11 step 25")
                .MaxRounds(1)
                .Pc("h1", ActorSource.Stats(() => Wizard("Hero One", 1)), 10, 10, Control.Scripted)
                .Pc("h2", ActorSource.Stats(() => Wizard("Hero Two", 1)), 11, 10, Control.Scripted)
                .Pc("h3", ActorSource.Stats(() => Wizard("Hero Three", 1)), 10, 11, Control.Scripted)
                .Npc("wizard", "mist_wizard", 2, 10, Control.Ai)
                .Npc("kobold", "kobold_warrior", 13, 13, Control.Idle)
                .Tweak("wizard", c => { AddWizardSlot(c, 3, SpellNames.FIREBALL); SpendAllBut(c, SpellNames.FIREBALL); WidenNpcSlotTable(c, 3); })
                .Tweak("h1", Fails).Tweak("h2", Fails).Tweak("h3", Fails).Tweak("kobold", Fails)
                .Initiative("h1", "wizard", "h2", "h3", "kobold")
                .Force(6, 6, null, -1)
                .Turn("h1", 1, Step.Assert("record", ctx => { RecordHp(ctx, "h1", "h2", "h3", "kobold"); return true; }))
                .Turn("h2", 1, Step.Assert("after", ctx => { FactHpLost(ctx, "fireball", "h1", "h2", "h3", "kobold"); return true; }))
                .Expect("The AI wizard casts Fireball once", CastEvents("wizard", SpellNames.FIREBALL, 1))
                .Expect("Every hero takes the burst's 5d6 (30, failed Reflex saves)",
                    FactsAre(("fireball.lost.h1", 30), ("fireball.lost.h2", 30), ("fireball.lost.h3", 30)))
                .Expect("The wizard's kobold ally is outside the burst it chose", FactIs("fireball.lost.kobold", 0))
                .Build();
        }

        /// <summary>
        /// AI-001 (area half; batch 11 step 25): an AI-run Enemy-team cleric (human_cleric, Cleric 5, given the Healer profile:
        /// the melee turn of its own profile casts no spell) holds only Bless (PHB
        /// p.205: the caster and all allies within a 50-ft. burst centred on the caster). Two kobold allies and a hero
        /// stand within 50 ft. The cleric casts it through the area resolve: it and both kobolds gain Bless, the hero
        /// does not (allies only, AoESystem.GetTargetsInArea with the AlliesOnly filter, relative to the caster's side).
        /// </summary>
        private static ScenarioDef NpcCastBless()
        {
            return Rules("rules/npc-cast-bless", "An Enemy-team AI cleric casts Bless on itself and its allies only (AI-001)")
                .Covers("AI-001", "PHB p.205", "SPL-119", "batch 11 step 25")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Wizard("Watching Hero", 1)), 10, 14, Control.Scripted)
                .Npc("cleric", "human_cleric", 10, 10, Control.Ai)
                .Npc("k1", "kobold_warrior", 11, 10, Control.Idle)
                .Npc("k2", "kobold_warrior", 9, 11, Control.Idle)
                .Profile("cleric", () => UnityEngine.ScriptableObject.CreateInstance<DND35.AI.Profiles.HealerAIProfile>())
                .Tweak("cleric", c => { Prepare(c, 1, SpellNames.BLESS); SpendAllBut(c, SpellNames.BLESS); })
                .Tweak("hero", Sturdy)
                .Initiative("cleric", "hero", "k1", "k2")
                .Turn("hero", 1, Step.Assert("after", ctx =>
                {
                    foreach (string k in new[] { "cleric", "k1", "k2", "hero" })
                    {
                        StatusEffectManager sem = ctx.Get(k).StatusEffectManager;
                        Fact(ctx, "bless." + k, sem != null && sem.HasEffect(SpellNames.BLESS));
                    }
                    return true;
                }))
                .Expect("The AI cleric casts Bless once", CastEvents("cleric", SpellNames.BLESS, 1))
                .Expect("The cleric and both kobolds are blessed", FactsAre(("bless.cleric", true), ("bless.k1", true), ("bless.k2", true)))
                .Expect("The hero, an enemy inside the burst, is not", FactIs("bless.hero", false))
                .Build();
        }

        /// <summary>
        /// CHR-070 (PHB p.32; batch 12 step 24): an AI-run Enemy-team cleric (human_cleric, Cleric 5, neutral good, so it
        /// converts into cure spells; given the Healer profile) has only one 1st-level Bless left and no cure spell prepared.
        /// Its kobold ally beside it is at 1 hit point. The AI converts: it gives up the Bless for Cure Light Wounds through
        /// the cast core's conversion source (SpellCastRequest.AiSourceFor, the source the PC's Convert button builds) and
        /// heals the kobold; the Bless slot is spent and Bless is not cast. Before CHR-070 the AI could not convert, so it cast
        /// Bless or nothing.
        /// </summary>
        private static ScenarioDef NpcClericConvertsCure()
        {
            return Rules("rules/npc-cleric-converts-cure", "An AI cleric converts a prepared spell into a cure spell to heal an ally (PHB p.32; CHR-070)")
                .Covers("CHR-070", "PHB p.32", "PHB p.215", "batch 12 step 24")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Wizard("Watching Hero", 1)), 10, 16, Control.Scripted)
                .Npc("cleric", "human_cleric", 10, 10, Control.Ai)
                .Npc("kobold", "kobold_warrior", 11, 10, Control.Idle)
                .Profile("cleric", () => UnityEngine.ScriptableObject.CreateInstance<DND35.AI.Profiles.HealerAIProfile>())
                .Tweak("cleric", c => { Prepare(c, 1, SpellNames.BLESS); SpendAllBut(c, SpellNames.BLESS); })
                .Tweak("kobold", c => c.Stats.CurrentHP = 1)
                .Tweak("hero", Sturdy)
                .Initiative("cleric", "hero", "kobold")
                .Turn("hero", 1, Step.Assert("after the cleric's turn", ctx =>
                {
                    CharacterController cleric = ctx.Get("cleric");
                    Fact(ctx, "spont", cleric.Stats.SpontaneousCasting);
                    Fact(ctx, "kobold.healed", ctx.Get("kobold").Stats.CurrentHP > 1);
                    List<SpellData> castable = cleric.Spellcasting.GetCastablePreparedSpells() ?? new List<SpellData>();
                    Fact(ctx, "bless.left", castable.Count(s => s != null && s.SpellId == SpellNames.BLESS));
                    StatusEffectManager sem = cleric.StatusEffectManager;
                    Fact(ctx, "bless.cast", sem != null && sem.HasEffect(SpellNames.BLESS));
                    return true;
                }))
                .Expect("The neutral good cleric converts into cure spells (PHB p.32)", FactIs("spont", SpontaneousCastingType.Cure))
                .Expect("The AI cleric casts Cure Light Wounds once", CastEvents("cleric", SpellNames.CURE_LIGHT_WOUNDS, 1))
                .Expect("The kobold is healed", FactIs("kobold.healed", true))
                .Expect("The Bless is given up for it, not cast", FactsAre(("bless.left", 0), ("bless.cast", false)))
                .Expect("The AI cleric casts no Bless", CastEvents("cleric", SpellNames.BLESS, 0))
                .Build();
        }

        /// <summary>
        /// AI-001 (area half; batch 11 step 25): an AI-run Enemy-team wizard (mist_wizard) holds only Web (PHB p.301:
        /// 20-ft.-radius spread, Medium range, Reflex negates). Three heroes stand together 40 ft. away and fail their
        /// saves. The wizard casts Web through the area resolve: the web is made and every hero is entangled.
        /// </summary>
        private static ScenarioDef NpcCastWeb()
        {
            return Rules("rules/npc-cast-web", "An Enemy-team AI wizard webs the party (AI-001)")
                .Covers("AI-001", "PHB p.301", "batch 11 step 25")
                .MaxRounds(1)
                .Pc("h1", ActorSource.Stats(() => Wizard("Webbed One", 1)), 10, 10, Control.Scripted)
                .Pc("h2", ActorSource.Stats(() => Wizard("Webbed Two", 1)), 11, 10, Control.Scripted)
                .Pc("h3", ActorSource.Stats(() => Wizard("Webbed Three", 1)), 10, 11, Control.Scripted)
                .Npc("wizard", "mist_wizard", 2, 10, Control.Ai)
                .Tweak("wizard", c => { Prepare(c, 2, SpellNames.WEB); SpendAllBut(c, SpellNames.WEB); })
                .Tweak("h1", Fails).Tweak("h2", Fails).Tweak("h3", Fails)
                .Initiative("wizard", "h1", "h2", "h3")
                .Turn("h1", 1, Step.Assert("after", ctx =>
                {
                    foreach (string k in new[] { "h1", "h2", "h3" })
                        Fact(ctx, "web." + k, ctx.Get(k).HasCondition(CombatConditionType.Entangled));
                    return true;
                }))
                .Expect("The AI wizard casts Web once", CastEvents("wizard", SpellNames.WEB, 1))
                .Expect("Every hero failed its Reflex save and is entangled", FactsAre(("web.h1", true), ("web.h2", true), ("web.h3", true)))
                .Build();
        }

        /// <summary>
        /// The AI's area aim (batch 11 step 25, AreaAimSearch): four heroes stand in a 2-by-2 block at (10,10) to
        /// (11,11), and the Enemy side's kobold stands beside them at (12,10). An AI-run cleric (human_cleric) holds only
        /// Sound Burst (PHB p.281: 10-ft.-radius spread, Close range): the burst at +(11,11), on the block's centre, would
        /// take all four heroes and the kobold; the one at +(10,11) takes the four heroes and leaves the kobold out, so the
        /// AI takes it. Then an AI-run wizard (mist_wizard) 10 ft. west of the block holds only Burning Hands (PHB p.207:
        /// 15-ft. cone): its cone is aimed at the cluster and takes at least two heroes, never the kobold. Saves fail;
        /// everyone has 100 more hit points. Three Player-team target dummies far to the west record the hit points between the turns (the
        /// burst stuns the heroes, who then take no turn).
        /// </summary>
        private static ScenarioDef AiAreaAim()
        {
            return Rules("rules/ai-area-aim", "The AI's burst spares the ally beside the party and its cone is aimed at the cluster (AI-001)")
                .Covers("AI-001", "PHB p.175", "PHB p.281", "PHB p.207", "batch 11 step 25")
                .MaxRounds(1)
                .Pc("h1", ActorSource.Stats(() => Wizard("Block One", 1)), 10, 10, Control.Scripted)
                .Pc("h2", ActorSource.Stats(() => Wizard("Block Two", 1)), 11, 10, Control.Scripted)
                .Pc("h3", ActorSource.Stats(() => Wizard("Block Three", 1)), 10, 11, Control.Scripted)
                .Pc("h4", ActorSource.Stats(() => Wizard("Block Four", 1)), 11, 11, Control.Scripted)
                .Npc("kobold", "kobold_warrior", 12, 10, Control.Idle)
                .Npc("cleric", "human_cleric", 8, 14, Control.Ai)
                .Npc("wizard", "mist_wizard", 7, 10, Control.Ai)
                .Npc("obs1", "target_dummy", 2, 2, Control.Scripted, CharacterTeam.Player)
                .Npc("obs2", "target_dummy", 2, 4, Control.Scripted, CharacterTeam.Player)
                .Npc("obs3", "target_dummy", 2, 6, Control.Scripted, CharacterTeam.Player)
                .Profile("cleric", () => UnityEngine.ScriptableObject.CreateInstance<DND35.AI.Profiles.HealerAIProfile>())
                .Tweak("cleric", c => { Prepare(c, 2, SpellNames.SOUND_BURST); SpendAllBut(c, SpellNames.SOUND_BURST); })
                .Tweak("wizard", c => { Prepare(c, 1, SpellNames.BURNING_HANDS); SpendAllBut(c, SpellNames.BURNING_HANDS); })
                .Tweak("h1", Fails).Tweak("h2", Fails).Tweak("h3", Fails).Tweak("h4", Fails).Tweak("kobold", Fails)
                .Initiative("obs1", "cleric", "obs2", "wizard", "obs3", "h1", "h2", "h3", "h4", "kobold")
                .Turn("obs1", 1, Step.Assert("record", ctx => { RecordHp(ctx, "h1", "h2", "h3", "h4", "kobold"); return true; }))
                .Turn("obs2", 1, Step.Assert("after the burst", ctx =>
                {
                    FactHpLost(ctx, "burst", "h1", "h2", "h3", "h4", "kobold");
                    RecordHp(ctx, "h1", "h2", "h3", "h4", "kobold");
                    return true;
                }))
                .Turn("obs3", 1, Step.Assert("after the cone", ctx =>
                {
                    FactHpLost(ctx, "cone", "h1", "h2", "h3", "h4", "kobold");
                    int hit = 0;
                    foreach (string k in new[] { "h1", "h2", "h3", "h4" })
                        if (Numbers(ctx).TryGetValue("hp|" + k, out int before) && before > ctx.Get(k).Stats.CurrentHP)
                            hit++;
                    Fact(ctx, "cone.heroes", hit >= 2 ? "2+" : hit.ToString());
                    return true;
                }))
                .Expect("The AI cleric casts Sound Burst once", CastEvents("cleric", SpellNames.SOUND_BURST, 1))
                .Expect("The burst takes all four heroes", v =>
                {
                    foreach (string k in new[] { "h1", "h2", "h3", "h4" })
                    {
                        string f = FactOf(v, "burst.lost." + k);
                        if (f == null)
                            return ExpectResult.Inconclusive("no fact burst.lost." + k);
                        if (!int.TryParse(f, out int lost) || lost <= 0)
                            return ExpectResult.Fail(k + " lost " + f);
                    }
                    return ExpectResult.Pass("all four heroes hit");
                })
                .Expect("The burst spares the kobold beside the party", FactIs("burst.lost.kobold", 0))
                .Expect("The AI wizard casts Burning Hands once", CastEvents("wizard", SpellNames.BURNING_HANDS, 1))
                .Expect("The cone is aimed at the cluster: two heroes or more", FactIs("cone.heroes", "2+"))
                .Expect("The cone spares the kobold", FactIs("cone.lost.kobold", 0))
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
            const string uiName = "Quicken Ui Wizard", scriptedName = "Quicken Scripted Wizard";
            // The refusal line names its caster: "<name> has already cast a quickened spell this round ...".
            bool RefusalOf(TraceEvent e, string casterName) => (e.Str("text") ?? "").Contains(casterName + " has " + refusedLog);
            return Rules("rules/quicken-once-per-round", "One quickened spell per round, Feather Fall included, for a Ui and a Scripted caster (PHB p.98, p.229)")
                .Covers("PHB p.98", "PHB p.229", "SPL-143", "batch 11 step 12")
                .MaxRounds(2)
                .Pc("ui", ActorSource.Stats(() => Wizard(uiName, 9, "Quicken Spell")), 3, 10, Control.Ui)
                .Pc("scripted", ActorSource.Stats(() => Wizard(scriptedName, 3)), 5, 10, Control.Scripted)
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
                .Expect("Round 1, Ui: the second quickened spell and the Feather Fall are refused with a log line",
                    Expect.Count("log", e => RefusalOf(e, uiName), 2, 2))
                // Since batch 12 step 9 (CMB-101) an NPC caster's refusal is logged too (TST-044).
                .Expect("Round 1, Scripted: the second Feather Fall's refusal is logged once",
                    Expect.Count("log", e => RefusalOf(e, scriptedName), 1, 1))
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
        /// goblin: the Ui menu refuses it with a log line, the Scripted cast is refused by the Legality stage with the same
        /// line (every caster's refusals are logged since batch 12 step 9, SPL-160); neither spends the slot and each keeps
        /// its standard action.
        /// </summary>
        private static ScenarioDef FullRoundAfterMove()
        {
            const string refusedLog = "casting it takes a full-round action";
            return Rules("rules/cast-full-round-after-move", "A 1-round casting time is refused after a move, for a Ui and a Scripted caster (PHB p.174, p.138)")
                .Covers("PHB p.174", "PHB p.138", "PHB p.280", "SPL-162", "SPL-160", "batch 11 step 12", "batch 12 step 9")
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
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("Move Ui Wizard cannot cast Sleep: " + refusedLog), 1, 1),
                    FactsAre(("ui.spent", "none"), ("ui.actions", "s--"))))
                .Expect("Scripted: the rule allows Sleep before the move and refuses it after; the cast is refused, nothing is spent and the standard action is left (PHB p.174)", Expect.All(
                    FactsAre(("sc.rule.before", "none"), ("sc.rule.after", CastActionRules.RefusalNoFullRoundAction)),
                    Expect.StepStatus("scripted", 1, "Cast", 0, "refused"),
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("Move Scripted Wizard cannot cast Sleep: " + refusedLog), 1, 1),
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
        /// nothing spent (Bull's Strength, a single-target spell, as when this was written; Bless is an area cast since batch 11 step 25); it then casts Shield of Faith
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
        /// harness selects it past the spell menu's slot filter, as a slot mismatch such as SPL-011 does). Since batch 12
        /// step 9 (SPL-160 a) the cast core's caster tests refuse it at Legality for want of a slot, with a log line, before
        /// anything is spent (before it, the core committed the action, failed to spend the slot and restored the snapshot,
        /// batch 11 step 14): no spell, the slot kept, every action left.
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

        // ── rules/ai-metamagic-choice, -one-foe (batch 11 step 28, AI-001) ──

        /// <summary>Clears every spell the sorcerer knows and makes <paramref name="spellId"/> its one known spell, of <paramref name="level"/>.</summary>
        private static void KnowsOnly(CharacterController c, string spellId, int level)
        {
            SpontaneousCastingData known = c.Spellcasting != null ? c.Spellcasting.SpontaneousData : null;
            if (known == null || !c.Spellcasting.IsSpontaneousCaster)
                throw new InvalidOperationException(c.Stats.CharacterName + " is not a spontaneous caster");
            for (int l = 0; l < known.SpellsKnownByLevel.Length; l++)
                known.SpellsKnownByLevel[l]?.Clear();
            if (!known.LearnSpell(spellId, level))
                throw new InvalidOperationException(c.Stats.CharacterName + " could not learn " + spellId);
        }

        /// <summary>A spontaneous caster's spells per day left at 3rd and 5th level, such as "L3:7 L5:3".</summary>
        private static string SpellsPerDay35(CharacterController c)
        {
            SpontaneousCastingData d = c.Spellcasting != null ? c.Spellcasting.SpontaneousData : null;
            return d == null ? "none" : "L3:" + d.GetSlotsRemaining(3) + " L5:" + d.GetSlotsRemaining(5);
        }

        /// <summary>
        /// AI-001 (batch 11 step 28, ai-metamagic-choice): the AI chooses metamagic for a spontaneous cast. An AI-run sorcerer
        /// 10 (CHA 16) with Empower Spell knows only Fireball (PHB p.231: 10d6 at caster level 10, Reflex half). Every d6 is
        /// 6 and the dummies fail their saves.
        /// - <paramref name="threeFoes"/>: three dummies stand together; the AI's planner (AiMetamagicPlanner.Value) weighs
        ///   17.5 more expected damage on each of three foes against a 5th-level spell per day and the full-round action
        ///   (PHB p.88), and casts an Empowered Fireball: each dummy loses 60 + 30 = 90 (PHB p.93), a 5th-level spell per day
        ///   is spent, not a 3rd, and no action is left.
        /// - one dummy: the plain Fireball stays first (one foe's 17.5 does not pay for the full round and the higher spell):
        ///   60, a 3rd-level spell per day, the move action left.
        /// </summary>
        private static ScenarioDef AiMetamagicChoice(bool threeFoes)
        {
            string id = threeFoes ? "rules/ai-metamagic-choice" : "rules/ai-metamagic-choice-one-foe";
            string title = threeFoes
                ? "An AI sorcerer empowers its Fireball when the burst takes three foes (AI-001; PHB p.88, p.93)"
                : "An AI sorcerer casts Fireball plain against one foe (AI-001; PHB p.88)";
            ScenarioBuilder b = Rules(id, title)
                .Covers("AI-001", "PHB p.88", "PHB p.93", "PHB p.231", "batch 11 step 28")
                .MaxRounds(1)
                .Pc("sorc", ActorSource.Stats(() => Sorcerer("AI Empower Sorcerer", 10, "Empower Spell")), 2, 10, Control.Ai)
                .Npc("obs", "target_dummy", 2, 2, Control.Scripted, CharacterTeam.Player)
                .Npc("d1", "target_dummy", 10, 10, Control.Scripted)
                .Tweak("sorc", c => KnowsOnly(c, SpellNames.FIREBALL, 3))
                .Profile("sorc", () => UnityEngine.ScriptableObject.CreateInstance<DND35.AI.Profiles.EvokerAIProfile>())
                .Tweak("d1", Fails)
                .Force(6, 6, null, -1);
            if (threeFoes)
            {
                b = b.Npc("d2", "target_dummy", 11, 10, Control.Scripted)
                     .Npc("d3", "target_dummy", 10, 11, Control.Scripted)
                     .Tweak("d2", Fails).Tweak("d3", Fails)
                     .Initiative("obs", "sorc", "d1", "d2", "d3");
            }
            else
            {
                b = b.Initiative("obs", "sorc", "d1");
            }
            string[] foes = threeFoes ? new[] { "d1", "d2", "d3" } : new[] { "d1" };
            b = b.Turn("obs", 1, Step.Assert("record", ctx =>
                {
                    RecordHp(ctx, foes);
                    Fact(ctx, "perday.before", SpellsPerDay35(ctx.Get("sorc")));
                    return true;
                }))
                .Turn("d1", 1, Step.Assert("after", ctx =>
                {
                    FactHpLost(ctx, "fireball", foes);
                    CharacterController sorc = ctx.Get("sorc");
                    Fact(ctx, "perday.after", SpellsPerDay35(sorc));
                    Fact(ctx, "sorc.actions", Actions(sorc));
                    return true;
                }))
                .Expect("The AI sorcerer casts Fireball once", CastEvents("sorc", SpellNames.FIREBALL, 1));
            if (threeFoes)
            {
                b = b.Expect("Each dummy takes the empowered 10d6: 60 + 30 = 90 (PHB p.93)",
                        FactsAre(("fireball.lost.d1", 90), ("fireball.lost.d2", 90), ("fireball.lost.d3", 90)))
                     .Expect("A 5th-level spell per day is spent, not a 3rd (PHB p.88)", FactsAre(("perday.before", "L3:7 L5:3"), ("perday.after", "L3:7 L5:2")))
                     .Expect("The spontaneous metamagic cast took the full round (PHB p.88)", FactIs("sorc.actions", "---"));
            }
            else
            {
                b = b.Expect("The dummy takes the plain 10d6: 60", FactIs("fireball.lost.d1", 60))
                     .Expect("A 3rd-level spell per day is spent, no 5th", FactsAre(("perday.before", "L3:7 L5:3"), ("perday.after", "L3:6 L5:3")))
                     .Expect("The plain Fireball is a standard action: the move is left", FactIs("sorc.actions", "-m-"));
            }
            return b.Build();
        }

        /// <summary>
        /// Batch 11 step 28 review (AI-001; PHB p.88, p.141): an AI-run sorcerer 6 (CHA 16, the Evoker profile) with Empower Spell knows only
        /// Shocking Grasp (5d6 at caster level 6, PHB p.279); a target dummy with 20 hit points stands 3 squares away. The
        /// empowered average (26.25) would drop it and the plain one (17.5) would not, so the planner values the Empowered
        /// spell first, but its spontaneous cast takes the full round (PHB p.88) and leaves no move to reach the dummy, so it
        /// is no hold candidate (AIService.SelectHoldChargeTarget). The AI casts the plain spell, holds the charge, moves
        /// beside the dummy and touches it (PHB p.141): d20 19 hits, every d6 is 1, so 5 damage; a 1st-level spell per day is
        /// spent, no 3rd.
        /// </summary>
        private static ScenarioDef AiMetamagicHoldPlain()
        {
            return Rules("rules/ai-metamagic-hold-plain", "An AI sorcerer holds a plain Shocking Grasp to move and touch, not a full-round Empowered one (AI-001; PHB p.88, p.141)")
                .Covers("AI-001", "PHB p.88", "PHB p.141", "PHB p.279", "batch 11 step 28")
                .MaxRounds(1)
                .Pc("sorc", ActorSource.Stats(() => Sorcerer("AI Grasp Sorcerer", 6, "Empower Spell")), 5, 10, Control.Ai)
                .Npc("obs", "target_dummy", 2, 2, Control.Scripted, CharacterTeam.Player)
                .Npc("dummy", "target_dummy", 8, 10, Control.Scripted)
                .Tweak("sorc", c => KnowsOnly(c, SpellNames.SHOCKING_GRASP, 1))
                // The caster's turn shell (a profile caster takes the ranged turn, which casts first; as rules/ai-metamagic-choice).
                .Profile("sorc", () => UnityEngine.ScriptableObject.CreateInstance<DND35.AI.Profiles.EvokerAIProfile>())
                .Tweak("dummy", c => c.Stats.CurrentHP = UnityEngine.Mathf.Min(c.Stats.CurrentHP, 20))
                .Initiative("obs", "sorc", "dummy")
                .Force(20, 19, SpellCaster.TouchAttackContext, 1)
                .Force(6, 1, null, -1)
                .Turn("obs", 1, Step.Assert("record", ctx =>
                {
                    CharacterController s = ctx.Get("sorc");
                    RecordHp(ctx, "dummy");
                    Numbers(ctx)["perday|1"] = s.Spellcasting.SpontaneousData.GetSlotsRemaining(1);
                    Numbers(ctx)["perday|3"] = s.Spellcasting.SpontaneousData.GetSlotsRemaining(3);
                    Fact(ctx, "dummy.hp", ctx.Get("dummy").Stats.CurrentHP);
                    return true;
                }))
                .Turn("dummy", 1, Step.Assert("after the sorcerer's turn", ctx =>
                {
                    CharacterController s = ctx.Get("sorc"), d = ctx.Get("dummy");
                    SpontaneousCastingData data = s.Spellcasting.SpontaneousData;
                    Fact(ctx, "held", s.Spellcasting.HasHeldTouchCharge);
                    Fact(ctx, "beside", CasterLevelRules.DistanceToTarget(s, d) <= 1);
                    FactHpLost(ctx, "grasp", "dummy");
                    Fact(ctx, "perday.spent", "L1:" + (Numbers(ctx)["perday|1"] - data.GetSlotsRemaining(1))
                                               + " L3:" + (Numbers(ctx)["perday|3"] - data.GetSlotsRemaining(3)));
                    return true;
                }))
                .Expect("The dummy starts at 20 hit points", FactIs("dummy.hp", 20))
                .Expect("The AI casts Shocking Grasp once (the hold); the delivery is no cast", CastEvents("sorc", SpellNames.SHOCKING_GRASP, 1))
                .Expect("It moves beside the dummy and the plain touch hits for 5d6 of 1s = 5, spending the charge (PHB p.141, p.279)",
                    FactsAre(("beside", true), ("grasp.lost.dummy", 5), ("held", false)))
                .Expect("A 1st-level spell per day is spent, no 3rd: the plain spell, not the Empowered one (PHB p.88)", FactIs("perday.spent", "L1:1 L3:0"))
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
