#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DND35.AI;
using SpellNames = DND35e.Identifiers.SpellNames;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 14 (docs/designs/personalities_batch14.md): the weighted-personality scenarios. A "-plain" twin of a later
    /// scenario drops the personality.
    /// <para>Step 1 (owner request 2026-10-30, the decision-time budget): rules/ai-decision-records, the plumbing of the
    /// decision trace and the decision timer. Every AI turn, an NPC's and an AI-run party member's alike, opens and closes
    /// one decision-time record and one decision-trace record, and every recorded decision is the Role's legacy choice (no
    /// personality exists yet). The expectations read only counts, never a time, so the verdict and the hash stay
    /// deterministic; the times themselves are in summary.json "aiTiming" and ai-timing.tsv, outside the hash.</para>
    /// <para>Step 2 (the facts for the considerations): rules/ai-damage-memory, the writers of the AI damage memory
    /// (CharacterController.DamageMemory, the DamagedObserver fact) in a real fight: a weapon hit (GameManager.OnAttackResolved)
    /// and a Magic Missile (SpellCaster.Cast, the per-target spell damage step), each remembered by its victim on its own
    /// next turn (the turn start rolls the memory over) and forgotten at the turn start after that. No AI reads the memory
    /// yet, so the step changes no other scenario's hash.</para>
    /// <para>Step 4 (the personality data and the difficulty): rules/ai-personality-spawn, the three trait creatures
    /// (goblin_adept, orc_sadist, hobgoblin_captain) spawn through the real spawn path with the personalities of their
    /// definitions, a zombie given Smart:0.5 by the harness spawns with none and warns once (C13), and the AI-run party
    /// member has none (C12), pinned to Normal; rules/ai-personality-spawn-easy pins Easy and checks that
    /// the blend reads it. Nobody acts on a personality yet (step 5), so the step changes no other scenario's hash.</para>
    /// <para>Step 5 (the blend at the target sites): rules/ai-captain-focuses-softest, the Smart hobgoblin captain takes the
    /// soft, wounded wizard where its plain twin takes the near, healthy, armored fighter (the layout is pinned by
    /// PersonalityBlendTests.TestCaptainLayout); rules/ai-sadist-coup-de-grace, the sadistic orc's coup de grace on a dying
    /// PC at Normal, none on Easy (owner, 2026-10-30) and none from the plain orc; rules/ai-sadist-keeps-downed-target, a
    /// sadistic dretch (an Animal Role, which skips downed foes) keeps hitting the PC it drops mid full attack where the
    /// plain dretch shifts to the standing one. Every older scenario's creature has no traits, so its hash is unchanged.</para>
    /// </summary>
    public static class PersonalityScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("ai-decision-records", AiDecisionRecords);
            yield return S("ai-damage-memory", AiDamageMemory);
            yield return S("ai-personality-spawn", () => AiPersonalitySpawn(easy: false));
            yield return S("ai-personality-spawn-easy", () => AiPersonalitySpawn(easy: true));
            yield return S("ai-captain-focuses-softest", () => AiCaptainFocusesSoftest(plain: false));
            yield return S("ai-captain-focuses-softest-plain", () => AiCaptainFocusesSoftest(plain: true));
            yield return S("ai-sadist-coup-de-grace", () => AiSadistCoupDeGrace(SadistTwin.Normal));
            yield return S("ai-sadist-coup-de-grace-easy", () => AiSadistCoupDeGrace(SadistTwin.Easy));
            yield return S("ai-sadist-coup-de-grace-plain", () => AiSadistCoupDeGrace(SadistTwin.Plain));
            yield return S("ai-sadist-keeps-downed-target", () => AiSadistKeepsDownedTarget(plain: false));
            yield return S("ai-sadist-keeps-downed-target-plain", () => AiSadistKeepsDownedTarget(plain: true));
            yield return S("ai-adept-never-charges", () => AiAdeptNeverCharges(plain: false));
            yield return S("ai-adept-never-charges-plain", () => AiAdeptNeverCharges(plain: true));
            yield return S("ai-adept-keeps-distance", () => AiAdeptKeepsDistance(plain: false));
            yield return S("ai-adept-keeps-distance-plain", () => AiAdeptKeepsDistance(plain: true));
            yield return S("ai-cowardly-avoids-aoo", () => AiAoOPath(AoOTwin.Cowardly));
            yield return S("ai-cowardly-avoids-aoo-plain", () => AiAoOPath(AoOTwin.CowardlyPlain));
            yield return S("ai-berserker-takes-aoo-path", () => AiAoOPath(AoOTwin.Berserker));
            yield return S("ai-berserker-takes-aoo-path-plain", () => AiAoOPath(AoOTwin.BerserkerPlain));
            yield return S("ai-cowardly-caster-defends-before-approach", () => AiOutOfReachOrder(berserker: false, plain: false));
            yield return S("ai-cowardly-caster-defends-before-approach-plain", () => AiOutOfReachOrder(berserker: false, plain: true));
            yield return S("ai-berserker-approaches-first", () => AiOutOfReachOrder(berserker: true, plain: false));
            yield return S("ai-berserker-approaches-first-plain", () => AiOutOfReachOrder(berserker: true, plain: true));
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("PersonalityScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        /// <summary>
        /// An AI-run fighter and two goblins fight for two rounds, everyone sturdy so nobody drops. Each of the six AI turns
        /// has its decision-time record (AiDecisionTimer) and its decision-trace record (AiDecisionTrace), and every
        /// decision is "legacy".
        /// </summary>
        private static ScenarioDef AiDecisionRecords()
        {
            return Rules("rules/ai-decision-records", "Every AI turn, NPC or AI-run party member, has one decision-time record and one decision-trace record (batch 14 step 1)")
                .Covers("owner request 2026-10-30")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 9, 10, Control.Ai)
                .Npc("gob1", "goblin", 11, 10, Control.Ai)
                .Npc("gob2", "goblin", 11, 12, Control.Ai)
                .Tweak("hero", Sturdy)
                .Tweak("gob1", Sturdy)
                .Tweak("gob2", Sturdy)
                .Initiative("gob1", "hero", "gob2")
                .Expect("Every turn is an AI turn", Expect.All(Expect.Controller("hero", "ai"), Expect.Controller("gob1", "ai"), Expect.Controller("gob2", "ai")))
                .Expect("Each AI turn has one decision-time record and one decision-trace record", v =>
                {
                    // Rounds 1-2 only: the halt at round 3 can come after the first AI turn of round 3 opened its records but
                    // before the trace's turn_start (that open turn is closed later as unclosed and is not counted here).
                    int aiTurns = v.Events.Count(e => e.Ev == "turn_start" && e.Str("controller") == "ai");
                    int timed = global::AiDecisionTimer.Completed.Concat(global::AiDecisionTimer.OpenTurnSamples).Count(t => t.Round <= 2);
                    int traced = global::AiDecisionTrace.Ring.Concat(global::AiDecisionTrace.OpenRecords).Count(t => t.Round <= 2);
                    string detail = aiTurns + " AI turns, " + timed + " timed, " + traced + " traced";
                    return aiTurns == 6 && timed == aiTurns && traced == aiTurns ? ExpectResult.Pass(detail) : ExpectResult.Fail(detail);
                })
                .Expect("Target choices are recorded and every recorded decision is the Role's legacy choice", v =>
                {
                    IReadOnlyDictionary<string, int> counts = global::AiDecisionTrace.Counts;
                    counts.TryGetValue("Target: legacy", out int targets);
                    int changed = counts.Where(kv => kv.Key.EndsWith(": changed", StringComparison.Ordinal)).Sum(kv => kv.Value);
                    string detail = (targets > 0 ? "target choices recorded" : "no target choice recorded") + ", " + changed + " changed";
                    return targets > 0 && changed == 0 ? ExpectResult.Pass(detail) : ExpectResult.Fail(detail);
                })
                .Build();
        }

        // ── rules/ai-damage-memory (step 2) ─────────────────────────

        private static CharacterStats Wizard(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Wizard",
                str: 10, dex: 12, con: 12, wis: 10, intelligence: 16, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 6 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.TrueNeutral;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static void PrepareMissiles(CharacterController c)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting");
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (slot == null || slot.Level != 1 || slot.IsDomainSlot || slot.IsUsed)
                    continue;
                slot.PreparedSpell = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
                sc.SyncPreparedSpellsFromSlots();
                return;
            }
            throw new InvalidOperationException(c.Stats.CharacterName + " has no level-1 slot");
        }

        /// <summary>The damage <paramref name="victim"/> remembers from <paramref name="source"/>, noted in the trace.</summary>
        private static int Remembered(ScenarioContext ctx, string victim, string source)
        {
            CharacterController v = ctx.Get(victim);
            int damage = v.DamageMemory.DamageFrom(ctx.Get(source));
            ctx.Note("memory " + victim + "<-" + source + "=" + damage + " (sources " + v.DamageMemory.Count + ")");
            return damage;
        }

        /// <summary>
        /// A Scripted fighter with a longsword hits a goblin (every d20 rolls 19), a Scripted wizard 3 casts Magic Missile at a second goblin,
        /// then the first goblin hits the fighter; everyone is sturdy, so nobody drops. Each victim remembers its attacker
        /// with the damage it took (the weapon hit through GameManager.OnAttackResolved, the missiles through
        /// SpellCaster.Cast), remembers nobody else, still remembers it when its own next turn starts (the turn start rolls
        /// the memory over: "it damaged me since my last turn"), and forgets it at the turn start after that. Round 2 has
        /// no attacks: the hero still remembers gob1 on its round 2 turn, and gob1 has forgotten the hero by its round 2 turn.
        /// </summary>
        private static ScenarioDef AiDamageMemory()
        {
            return Rules("rules/ai-damage-memory", "A creature remembers who hurt it since its previous turn: a weapon hit and a Magic Missile, still known on its next turn, forgotten at the one after (batch 14 step 2)")
                .Covers("batch 14 step 2")
                .MaxRounds(2)
                .RecordDice()
                .Force(20, 19, null, -1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 9, 10, Control.Scripted)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Missile Wizard", 3)), 5, 10, Control.Scripted)
                .Npc("gob1", "goblin", 10, 10, Control.Scripted)
                .Npc("gob2", "goblin", 10, 13, Control.Scripted)
                .Tweak("hero", c =>
                {
                    Sturdy(c);
                    global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
                    if (inv == null)
                        throw new InvalidOperationException("the hero has no inventory");
                    inv.RightHandSlot = null;
                    inv.LeftHandSlot = null;
                    inv.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponLongsword), EquipSlot.RightHand);
                    inv.RecalculateStats();
                })
                .Tweak("wiz", c => { PrepareMissiles(c); Sturdy(c); })
                .Tweak("gob1", Sturdy)
                .Tweak("gob2", Sturdy)
                .Initiative("hero", "wiz", "gob1", "gob2")
                .Turn("hero", 1,
                    Step.Attack("gob1"),
                    Step.Assert("gob1 remembers the hero's hit, and only the hero", ctx =>
                    {
                        int hp = ctx.Get("gob1").Stats.TotalMaxHP - ctx.Get("gob1").Stats.CurrentHP;
                        return Remembered(ctx, "gob1", "hero") == hp && hp > 0 && ctx.Get("gob1").DamageMemory.Count == 1
                            && ctx.Get("gob2").DamageMemory.Count == 0;
                    }),
                    Step.EndTurn())
                .Turn("wiz", 1,
                    Step.Cast(SpellNames.MAGIC_MISSILE, "gob2"),
                    Step.Assert("gob2 remembers the wizard's missiles", ctx =>
                    {
                        int hp = ctx.Get("gob2").Stats.TotalMaxHP - ctx.Get("gob2").Stats.CurrentHP;
                        return Remembered(ctx, "gob2", "wiz") == hp && hp > 0 && Remembered(ctx, "gob2", "hero") == 0;
                    }),
                    Step.EndTurn())
                .Turn("gob1", 1,
                    Step.Assert("gob1 still remembers the hero at its own turn start", ctx =>
                    {
                        int hp = ctx.Get("gob1").Stats.TotalMaxHP - ctx.Get("gob1").Stats.CurrentHP;
                        return Remembered(ctx, "gob1", "hero") == hp && hp > 0 && ctx.Get("gob1").DamageMemory.Count == 1;
                    }),
                    Step.Attack("hero"),
                    Step.Assert("the hero remembers gob1's hit", ctx =>
                    {
                        int hp = ctx.Get("hero").Stats.TotalMaxHP - ctx.Get("hero").Stats.CurrentHP;
                        return Remembered(ctx, "hero", "gob1") == hp && hp > 0;
                    }),
                    Step.EndTurn())
                .Turn("gob2", 1,
                    Step.Assert("gob2 still remembers the wizard at its own turn start; the hero still remembers gob1", ctx =>
                        Remembered(ctx, "gob2", "wiz") > 0 && ctx.Get("gob2").DamageMemory.Count == 1
                        && ctx.Get("hero").DamageMemory.WasDamagedBy(ctx.Get("gob1"))),
                    Step.Pass())
                .Turn("hero", 2,
                    Step.Assert("the hero still remembers gob1 at its own next turn start", ctx =>
                    {
                        int hp = ctx.Get("hero").Stats.TotalMaxHP - ctx.Get("hero").Stats.CurrentHP;
                        return Remembered(ctx, "hero", "gob1") == hp && hp > 0 && ctx.Get("hero").DamageMemory.Count == 1;
                    }),
                    Step.Pass())
                .Turn("gob1", 2,
                    Step.Assert("gob1 forgot the hero at its second turn start", ctx =>
                        Remembered(ctx, "gob1", "hero") == 0 && ctx.Get("gob1").DamageMemory.Count == 0),
                    Step.Pass())
                .Expect("Every memory check holds", Expect.AssertsPass())
                .Expect("All seven checks ran", Expect.Count("assert", e => true, 7, 7))
                .Build();
        }

        // ── rules/ai-personality-spawn (step 4) ─────────────────────

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

        /// <summary>Notes and returns <paramref name="key"/>'s personality ("none" without one).</summary>
        private static string NotePersonality(ScenarioContext ctx, string key)
        {
            DND35.AI.Personality.PersonalityProfile p = ctx.Get(key).Personality;
            string text = p == null
                ? "none"
                : string.Join(",", p.Traits.Select(t => t.Trait.Id + ":" + t.Weight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)))
                  + " tier " + p.Tier;
            ctx.Note("personality " + key + "=" + text);
            return text;
        }

        private static AIService.AIDifficultyLevel? ServiceDifficulty(ScenarioContext ctx)
        {
            AIService ai = ctx.Gm != null ? ctx.Gm.GetComponent<AIService>() : null;
            return ai != null ? ai.Difficulty : (AIService.AIDifficultyLevel?)null;
        }

        /// <summary>
        /// The three batch 14 trait creatures and a zombie spawn far from an AI-run fighter; the fighter's only turn checks
        /// them and passes, everyone else idles. Each trait creature has its definition's personality (goblin_adept
        /// Cowardly .6 and Healer .4, an Adept 1 with no armor; orc_sadist Sadistic .7 and Berserker .3; hobgoblin_captain
        /// Smart .5 at the Keen tier, a Fighter 3); the zombie, given Smart:0.5 by the harness, keeps none (mindless, C13)
        /// and its one warning is a trace note; the party fighter has none (C12). The non-easy twin pins Normal; the
        /// "-easy" twin pins Easy, and the blend reads it through AiBlend.SettingsDifficulty.
        /// </summary>
        private static ScenarioDef AiPersonalitySpawn(bool easy)
        {
            string id = easy ? "rules/ai-personality-spawn-easy" : "rules/ai-personality-spawn";
            AIService.AIDifficultyLevel want = easy ? AIService.AIDifficultyLevel.Easy : AIService.AIDifficultyLevel.Normal;
            ScenarioBuilder b = Rules(id, easy
                    ? "The AI difficulty a scenario pins (Easy) reaches AIService.Difficulty and the personality blend (batch 14 step 4, AI-012)"
                    : "The batch 14 trait creatures spawn with their personalities; a mindless zombie given one keeps none and warns (batch 14 step 4)")
                .Covers("AI-012", "CRE-023", "CRE-046")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 2, 2, Control.Scripted)
                .Npc("adept", "goblin_adept", 15, 15, Control.Idle)
                .Npc("sadist", "orc_sadist", 17, 15, Control.Idle)
                .Npc("captain", "hobgoblin_captain", 15, 17, Control.Idle)
                .Npc("zombie", "zombie_shambler", 17, 17, Control.Idle)
                .Personality("zombie", "Smart:0.5")
                .Initiative("hero", "adept", "sadist", "captain", "zombie");
            // Both twins pin their difficulty, so a run with difficulty=easy or hard cannot fail the Normal twin's checks;
            // a Normal pin adds no note and the meta line does not carry it, so the hash is unchanged.
            b.AiDifficulty(want);
            b.Turn("hero", 1,
                    Step.Assert("the goblin adept: Cowardly .6, Healer .4; Adept 1, no armor or shield", ctx =>
                    {
                        NotePersonality(ctx, "adept");
                        CharacterController c = ctx.Get("adept");
                        DND35.AI.Personality.PersonalityProfile p = c.Personality;
                        return p != null && p.Traits.Count == 2 && Near(p.WeightOf("Cowardly"), 0.6f) && Near(p.WeightOf("Healer"), 0.4f)
                               && c.Stats.GetClassLevel("Adept") == 1 && c.Stats.ArmorBonus == 0 && c.Stats.ShieldBonus == 0;
                    }),
                    Step.Assert("the orc sadist: Sadistic .7, Berserker .3", ctx =>
                    {
                        NotePersonality(ctx, "sadist");
                        DND35.AI.Personality.PersonalityProfile p = ctx.Get("sadist").Personality;
                        return p != null && p.Traits.Count == 2 && Near(p.WeightOf("Sadistic"), 0.7f) && Near(p.WeightOf("Berserker"), 0.3f);
                    }),
                    Step.Assert("the hobgoblin captain: Smart .5 at the Keen tier; Fighter 3", ctx =>
                    {
                        NotePersonality(ctx, "captain");
                        CharacterController c = ctx.Get("captain");
                        DND35.AI.Personality.PersonalityProfile p = c.Personality;
                        return p != null && p.Traits.Count == 1 && Near(p.WeightOf("Smart"), 0.5f)
                               && p.Tier == DND35.AI.Knowledge.CognitionTier.Keen && c.Stats.GetClassLevel("Fighter") == 3;
                    }),
                    Step.Assert("the zombie given Smart:0.5 keeps none and warned once; the party fighter has none", ctx =>
                    {
                        string zombie = NotePersonality(ctx, "zombie");
                        string hero = NotePersonality(ctx, "hero");
                        return zombie == "none" && hero == "none" && ctx.PersonalityWarnings.Count == 1
                               && ctx.PersonalityWarnings[0].Contains("zombie_shambler") && ctx.PersonalityWarnings[0].Contains("Smart dropped");
                    }),
                    Step.Assert("the difficulty is " + want + " in AIService and in the personality blend", ctx =>
                    {
                        AIService.AIDifficultyLevel? service = ServiceDifficulty(ctx);
                        Func<AIService.AIDifficultyLevel> source = DND35.AI.Personality.AiBlend.SettingsDifficulty;
                        ctx.Note("difficulty service=" + service + " blend=" + (source != null ? source().ToString() : "unset"));
                        return service == want && source != null && source() == want;
                    }),
                    Step.Pass())
                .Expect("Every spawn check holds", Expect.AssertsPass())
                .Expect("All five checks ran", Expect.Count("assert", e => true, 5, 5))
                .Expect("The zombie's personality warning is one trace note", Expect.Count("note",
                    e => (e.Str("text") ?? "").StartsWith("personality warning:", StringComparison.Ordinal), 1, 1))
                .Expect(easy ? "The Easy pin is a trace note" : "The default difficulty adds no note", Expect.Count("note",
                    e => (e.Str("text") ?? "").StartsWith("ai difficulty", StringComparison.Ordinal), easy ? 1 : 0, easy ? 1 : 0));
            return b.Build();
        }

        // ── Step 5: the blend at the target sites ──────────────────

        /// <summary>Sets <paramref name="c"/>'s full and current hit points and its HP state (dying below 0).</summary>
        private static void SetHp(CharacterController c, int max, int current)
        {
            c.Stats.AdjustMaxHP(max - c.Stats.TotalMaxHP);
            c.Stats.CurrentHP = current;
            c.SyncHPStateFromCurrentHP(emitLog: false);
        }

        /// <summary>The round-1 attacks (not AoOs) of <paramref name="by"/>, in order.</summary>
        private static List<TraceEvent> FirstRoundAttacks(TraceView v, string by) => v.Attacks(by: by, aoo: false, round: 1);

        /// <summary>
        /// The decision-trace rows of <paramref name="site"/> kept by the ring and the open turns, only those whose legacy
        /// value is <paramref name="legacy"/> when one is given.
        /// </summary>
        private static List<global::AiDecisionTrace.Decision> TraceRows(string site, string legacy = null)
            => global::AiDecisionTrace.Ring.Concat(global::AiDecisionTrace.OpenRecords)
                .SelectMany(t => t.Decisions)
                .Where(d => d.Site == site && (legacy == null || d.Legacy == legacy))
                .ToList();

        /// <summary>
        /// The failure for a trace row of <paramref name="site"/> that is not there (<paramref name="kept"/> rows of the
        /// site were kept, none <paramref name="want"/>). A turn keeps at most AiDecisionTrace.MaxDecisionsPerTurn rows and
        /// only counts the rest, so when rows were dropped the message says so, with the site's "changed" count, and a
        /// truncated trace does not read as a behaviour fault.
        /// </summary>
        private static ExpectResult MissingRow(string site, int kept, string want)
        {
            int dropped = global::AiDecisionTrace.Ring.Concat(global::AiDecisionTrace.OpenRecords).Sum(t => t.Overflow);
            global::AiDecisionTrace.Counts.TryGetValue(site + ": changed", out int changed);
            string why = dropped > 0
                ? "; " + dropped + " row(s) dropped by the " + global::AiDecisionTrace.MaxDecisionsPerTurn + "-row cap, "
                  + changed + " '" + site + ": changed' decision(s) counted"
                : "";
            return ExpectResult.Fail(kept + " " + site + " row(s), none " + want + why);
        }

        /// <summary>
        /// The layout of PersonalityBlendTests.TestCaptainLayout, in a real fight: the hobgoblin captain (Humanoid Role,
        /// Smart .5) stands between a healthy fighter in medium armor (Fighter 3, nobody beside it) and a wizard 2 at 10 of
        /// 14 hit points with two fighters beside it, both in its reach. The Role's score (HumanoidAIProfile: unarmored,
        /// wounded, isolated, near) favours the fighter by about 1.2; Smart's term (KillChance and KnownCaster, design 4.4)
        /// adds about 3.8 to the wizard and 0.3 to the fighter. The Smart captain's first attack or maneuver (the Role
        /// opens with a trip) goes to the wizard and the decision trace records the fighter as the legacy choice; the
        /// "-plain" twin (personality dropped) goes for the fighter. Everyone but the captain idles; one round.
        /// </summary>
        private static ScenarioDef AiCaptainFocusesSoftest(bool plain)
        {
            string id = plain ? "rules/ai-captain-focuses-softest-plain" : "rules/ai-captain-focuses-softest";
            string want = plain ? "fighter" : "wizard";
            ScenarioBuilder b = Rules(id, plain
                    ? "The plain hobgoblin captain (no traits) attacks the near, healthy, armored fighter: the Role's own target score (batch 14 step 5)"
                    : "The Smart hobgoblin captain attacks the soft, wounded wizard the plain captain passes over: the trait term at the target score (batch 14 step 5, C4)")
                .Covers("batch 14 step 5", "AI-058")
                .MaxRounds(1)
                .Npc("captain", "hobgoblin_captain", 10, 10, Control.Ai)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Hero Fighter", 3)), 9, 10, Control.Idle)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Soft Wizard", 2)), 11, 10, Control.Idle)
                .Pc("ally1", ActorSource.Stats(() => RulesScenarios.Fighter("Ally North", 3)), 12, 9, Control.Idle)
                .Pc("ally2", ActorSource.Stats(() => RulesScenarios.Fighter("Ally South", 3)), 12, 11, Control.Idle)
                .Tweak("fighter", Sturdy)
                .Tweak("wizard", c => SetHp(c, 14, 10))
                .Tweak("ally1", Sturdy)
                .Tweak("ally2", Sturdy)
                .Initiative("captain", "fighter", "wizard", "ally1", "ally2");
            if (plain)
                b.Personality("captain", "");
            // The Humanoid Role may open with a trip on its target (AttemptTrip; scored by odds from step 9), so the check reads
            // the first attack or maneuver of the captain's turn.
            b.Expect("The captain's first attack or maneuver goes to the " + want, v =>
            {
                TraceEvent first = v.Events.FirstOrDefault(e => e.Round == 1
                    && ((e.Ev == "attack" && e.Str("attacker") == "captain" && !e.Bool("aoo"))
                        || (e.Ev == "maneuver" && e.Str("by") == "captain" && !e.Bool("counter"))));
                string at = first != null ? first.Ev + " at " + first.Str("target") : "no attack or maneuver";
                return first != null && first.Str("target") == want ? ExpectResult.Pass(at, first.Seq) : ExpectResult.Fail(at);
            });
            if (!plain)
                b.Expect("The decision trace records the fighter as the Role's choice and the wizard as the blend's", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("Personality.Target", "Hero Fighter");
                    bool ok = rows.Any(d => d.Chosen == "Soft Wizard" && d.Bonus > 0f);
                    return ok ? ExpectResult.Pass(rows.Count + " Personality.Target row(s): legacy Hero Fighter, chosen Soft Wizard")
                              : ExpectResult.Fail(rows.Count + " Personality.Target row(s) with legacy Hero Fighter; none chose Soft Wizard");
                });
            return b.Build();
        }

        private enum SadistTwin { Normal, Easy, Plain }

        /// <summary>
        /// A sadistic orc (orc_sadist: Sadistic .7, Berserker .3; the Humanoid Role, which takes no coup de grace) stands
        /// beside a dying wizard (-3 hit points, helpless, PHB p.145) with its full-round action; a sturdy fighter stands far
        /// off, so the fight goes on. At Normal difficulty the trait's Insist takes the coup de grace (PHB p.153); on Easy the
        /// row is skipped (owner, 2026-10-30) and the Role's no stands; the "-plain" twin (personality dropped) takes none.
        /// The orc acts first; one round.
        /// </summary>
        private static ScenarioDef AiSadistCoupDeGrace(SadistTwin twin)
        {
            string id = "rules/ai-sadist-coup-de-grace" + (twin == SadistTwin.Easy ? "-easy" : twin == SadistTwin.Plain ? "-plain" : "");
            bool wantsCoup = twin == SadistTwin.Normal;
            ScenarioBuilder b = Rules(id, twin == SadistTwin.Normal
                    ? "A sadistic orc takes a coup de grace on the dying PC beside it at Normal difficulty (batch 14 step 5; owner ruling 2026-10-30; PHB p.153)"
                    : twin == SadistTwin.Easy
                        ? "On Easy the sadistic orc takes no coup de grace: the trait's row is skipped and the Role's no stands (batch 14 step 5; owner ruling 2026-10-30)"
                        : "The plain orc (no traits) takes no coup de grace on the dying PC beside it: the Humanoid Role's no (batch 14 step 5)")
                .Covers("batch 14 step 5", "AI-012")
                .MaxRounds(1)
                .Npc("sadist", "orc_sadist", 10, 10, Control.Ai)
                .Pc("fallen", ActorSource.Stats(() => Wizard("Fallen Wizard", 2)), 11, 10, Control.Idle)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Far Fighter", 3)), 2, 2, Control.Idle)
                .Tweak("fallen", c => SetHp(c, 14, -3))
                .Tweak("hero", Sturdy)
                .Initiative("sadist", "fallen", "hero");
            b.AiDifficulty(twin == SadistTwin.Easy ? AIService.AIDifficultyLevel.Easy : AIService.AIDifficultyLevel.Normal);
            if (twin == SadistTwin.Plain)
                b.Personality("sadist", "");
            b.Expect("The wizard is dying (helpless) when the orc's turn starts", v =>
            {
                JsonObj snap = v.Snapshot("fallen", 1, "sadist");
                object hp = snap != null ? snap.Get("hp") : null;
                return hp is int h && h < 0 ? ExpectResult.Pass("hp " + h) : ExpectResult.Fail("hp " + (hp ?? "?"));
            });
            b.Expect(wantsCoup ? "The orc takes one coup de grace, on the wizard" : "The orc takes no coup de grace", v =>
            {
                List<TraceEvent> coups = v.Maneuvers("sadist", SpecialAttackType.CoupDeGrace);
                if (!wantsCoup)
                    return coups.Count == 0 ? ExpectResult.Pass("no coup de grace") : ExpectResult.Fail(coups.Count + " coup(s) de grace", coups[0].Seq);
                return coups.Count == 1 && coups[0].Str("target") == "fallen"
                    ? ExpectResult.Pass("coup de grace on fallen", coups[0].Seq)
                    : ExpectResult.Fail(coups.Count + " coup(s) de grace" + (coups.Count > 0 ? ", first on " + coups[0].Str("target") : ""));
            });
            return b.Build();
        }

        /// <summary>
        /// A dretch given the Animal Role by the harness (it skips downed foes and retargets mid full attack, AnimalAIProfile,
        /// and makes no trip; the spawn's Humanoid Role keeps downed foes, so it could not show the difference) full-attacks a
        /// wizard at 1 hit point, every d20 a 19, so its first claw drops the wizard to dying; a sturdy fighter stands on its
        /// other side. Its target is pinned to the wizard for the turn start (PriorityTarget), and its coup de grace is off
        /// (the data override), so only the ignore-downed choice is tested. Given Sadistic .7 and Berserker .3, the
        /// Sadistic Forbid keeps the downed wizard its target, so its second attack hits the wizard again; the "-plain"
        /// twin (no traits) skips the downed wizard and shifts focus to the fighter. One round.
        /// </summary>
        private static ScenarioDef AiSadistKeepsDownedTarget(bool plain)
        {
            string id = plain ? "rules/ai-sadist-keeps-downed-target-plain" : "rules/ai-sadist-keeps-downed-target";
            string want = plain ? "guard" : "victim";
            ScenarioBuilder b = Rules(id, plain
                    ? "The plain dretch (Animal Role) skips the PC it drops mid full attack and shifts focus to the standing one (batch 14 step 5)"
                    : "A sadistic dretch keeps attacking the PC it drops mid full attack, where its Animal Role would skip it (batch 14 step 5, IgnoreUnconscious Forbid)")
                .Covers("batch 14 step 5")
                .MaxRounds(1)
                .RecordDice()
                .Force(20, 19, null, -1)
                .Npc("dretch", "dretch", 10, 10, Control.Ai)
                .Pc("victim", ActorSource.Stats(() => Wizard("Weak Wizard", 2)), 11, 10, Control.Idle)
                .Pc("guard", ActorSource.Stats(() => RulesScenarios.Fighter("Guard Fighter", 3)), 9, 10, Control.Idle)
                // The spawn gives the dretch the Humanoid Role (the Humanoid entry wins the dretch id collision, CRE-022), which
                // keeps downed foes anyway; the Animal Role skips them.
                .Profile("dretch", () => DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.AnimalAIProfile>())
                .PriorityTarget("dretch", "victim")
                .Tweak("dretch", c => c.EnemyUseCoupDeGraceOverride = false)
                .Tweak("victim", c => SetHp(c, 14, 1))
                .Tweak("guard", Sturdy)
                .Initiative("dretch", "victim", "guard");
            if (!plain)
                b.Personality("dretch", "Sadistic:0.7,Berserker:0.3");
            b.Expect("The dretch full-attacks, its first attack at the wizard, who is dying after it", v =>
            {
                List<TraceEvent> attacks = FirstRoundAttacks(v, "dretch");
                string first = attacks.Count > 0 ? attacks[0].Str("target") : "none";
                bool dropped = v.Of("hp").Any(e => e.Str("actor") == "victim" && e.Get("new") is int n && n < 0);
                return first == "victim" && attacks.Count >= 2 && dropped
                    ? ExpectResult.Pass(attacks.Count + " attacks, first at victim", attacks[0].Seq)
                    : ExpectResult.Fail(attacks.Count + " attacks, first at " + first);
            });
            b.Expect("The dretch's second attack goes to the " + want, v =>
            {
                List<TraceEvent> attacks = FirstRoundAttacks(v, "dretch");
                string second = attacks.Count > 1 ? attacks[1].Str("target") : "none";
                return second == want ? ExpectResult.Pass("second attack at " + second, attacks[1].Seq) : ExpectResult.Fail("second attack at " + second);
            });
            return b.Build();
        }

        // ── Step 6: the blend at the movement sites and the out-of-reach order ──

        private static UnityEngine.Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is UnityEngine.Vector2Int p ? p : (UnityEngine.Vector2Int?)null;

        /// <summary>
        /// The goblin adept has no castable spell until batch 15 gives Adepts their lists (CHR-024, batch 15 step 10), and
        /// the adept scenarios of step 6 rely on it (the kiter shoots its sling; out of reach it has no ranged means). A
        /// precondition, so a future spell list fails here, naming its cause, instead of in a blend check.
        /// </summary>
        private static void ExpectNoCast(ScenarioBuilder b, string key) =>
            b.Expect("Precondition: the " + key + " casts no spell (no Adept lists until batch 15, CHR-024)",
                Expect.None("cast", e => e.Str("by") == key));

        /// <summary>The fixture's fatigue (PHB p.308: a fatigued creature can neither run nor charge), so a scenario sees the move, not a charge.</summary>
        private static void Fatigue(CharacterController c) => c.Stats.IsFatigued = true;

        /// <summary>
        /// Leaves <paramref name="c"/> a morningstar in its right hand and nothing else to fight with: no sling, no bullets,
        /// no javelin, no shield (the fixture of the out-of-reach scenarios: no ranged means, AIService.HasRangedMeans).
        /// </summary>
        private static void MorningstarOnly(CharacterController c)
        {
            global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
            if (inv == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no inventory");
            inv.RightHandSlot = null;
            inv.LeftHandSlot = null;
            if (inv.GeneralSlots != null)
                for (int i = 0; i < inv.GeneralSlots.Length; i++)
                    inv.GeneralSlots[i] = null;
            inv.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponMorningstar), EquipSlot.RightHand);
            inv.RecalculateStats();
        }

        /// <summary>
        /// A melee goblin (goblin_warrior, Humanoid Role, which charges whenever the rules allow it) six squares from an idle
        /// fighter on a clear line, and the goblin adept farther off. Given Cowardly .6, the goblin's Charge Forbid
        /// (a quorum) refuses the legal charge (C5): it moves up and attacks instead, and the decision trace records the
        /// Role's yes and the blend's no. The adept never charges at all: its kiter routine has no charge site (it shoots
        /// with its sling). The "-plain" twin (both personalities dropped) charges. One round.
        /// </summary>
        private static ScenarioDef AiAdeptNeverCharges(bool plain)
        {
            string id = plain ? "rules/ai-adept-never-charges-plain" : "rules/ai-adept-never-charges";
            ScenarioBuilder b = Rules(id, plain
                    ? "A plain goblin (no traits) charges the fighter six squares off: the Humanoid Role takes every legal charge (batch 14 step 6)"
                    : "A cowardly goblin refuses a legal charge and the goblin adept never charges: the traits' Charge Forbid (batch 14 step 6, C5; PHB p.154)")
                .Covers("batch 14 step 6", "PHB p.154")
                .MaxRounds(1)
                .Npc("gob", "goblin_warrior", 8, 10, Control.Ai)
                .Npc("adept", "goblin_adept", 8, 16, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 14, 10, Control.Idle)
                .Tweak("hero", Sturdy)
                .Tweak("gob", Sturdy)
                .Tweak("adept", Sturdy)
                .Initiative("gob", "adept", "hero");
            // The goblin warrior is plain only because its definition carries no traits today (Q7): pinned, so a later
            // definition change cannot turn the plain charger into a trait twin.
            b.Personality("gob", plain ? "" : "Cowardly:0.6");
            if (plain)
                b.Personality("adept", "");
            ExpectNoCast(b, "adept");
            b.Expect("The adept takes no charge", v =>
                v.Log("Goblin Adept is charging").Count == 0 ? ExpectResult.Pass("no charge") : ExpectResult.Fail("the adept charged"));
            if (plain)
                b.Expect("The plain goblin charges the fighter", v =>
                {
                    List<TraceEvent> line = v.Log("Goblin Warrior is charging");
                    return line.Count == 1 ? ExpectResult.Pass("charged", line[0].Seq) : ExpectResult.Fail(line.Count + " charge line(s)");
                });
            else
            {
                b.Expect("The cowardly goblin takes no charge, and moves up instead", v =>
                {
                    int charges = v.Log("Goblin Warrior is charging").Count;
                    int moved = v.Moves("gob", 1).Sum(e => e.Int("sq"));
                    return charges == 0 && moved > 0 ? ExpectResult.Pass("no charge, moved " + moved) : ExpectResult.Fail(charges + " charge(s), moved " + moved);
                });
                b.Expect("The decision trace records the Role's yes and the blend's no", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("Personality.Charge");
                    return rows.Any(d => d.Legacy == "yes" && d.Chosen == "no")
                        ? ExpectResult.Pass(rows.Count + " Personality.Charge row(s): legacy yes, chosen no")
                        : MissingRow("Personality.Charge", rows.Count, "legacy yes and chosen no");
                });
            }
            return b.Build();
        }

        /// <summary>
        /// The goblin adept (Healer Role on the kiter routine, a sling in hand; Cowardly .6, Healer .4) starts three squares
        /// from an idle fighter; a plain goblin warrior closes on the fighter. The kiter backs off from a foe within its
        /// keep-away distance: 2 squares for the Role, 4 with Cowardly's range adjust (+2 for a Ranged, Mixed or casting
        /// Role, C3). So the cowardly adept backs off on its first turn and then shoots, and stays at least 3 squares from
        /// the fighter for 4 rounds; the "-plain" twin (the adept's personality dropped) stays where it is, three squares
        /// off, and shoots. Everyone is sturdy.
        /// </summary>
        private static ScenarioDef AiAdeptKeepsDistance(bool plain)
        {
            string id = plain ? "rules/ai-adept-keeps-distance-plain" : "rules/ai-adept-keeps-distance";
            ScenarioBuilder b = Rules(id, plain
                    ? "The plain goblin adept keeps the kiter's 2-square keep-away distance: three squares off, it stays and shoots (batch 14 step 6)"
                    : "The cowardly goblin adept backs off to keep its distance (keep-away 2 + Cowardly's 2) and stays at least 3 squares from the PC for 4 rounds while a plain goblin closes (batch 14 step 6, C3)")
                .Covers("batch 14 step 6")
                .MaxRounds(4)
                .Npc("adept", "goblin_adept", 10, 10, Control.Ai)
                .Npc("gob", "goblin_warrior", 10, 15, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 13, 10, Control.Idle)
                .Tweak("hero", Sturdy)
                .Tweak("gob", Sturdy)
                .Tweak("adept", Sturdy)
                .Initiative("adept", "gob", "hero");
            // The plain closer stays plain whatever goblin_warrior's definition says.
            b.Personality("gob", "");
            if (plain)
                b.Personality("adept", "");
            ExpectNoCast(b, "adept");
            b.Expect("The adept is never within 2 squares of the fighter, at any turn start or at the end", v =>
            {
                int worst = int.MaxValue;
                foreach (TraceEvent ts in v.Of("turn_start").Where(e => e.Round >= 1 && e.Round <= 4))
                {
                    UnityEngine.Vector2Int? a = Pos(v.Snapshot("adept", ts.Round, ts.Str("actor")));
                    UnityEngine.Vector2Int? h = Pos(v.Snapshot("hero", ts.Round, ts.Str("actor")));
                    if (a.HasValue && h.HasValue)
                        worst = Math.Min(worst, SquareGridUtils.GetDistance(a.Value, h.Value));
                }
                UnityEngine.Vector2Int? fa = Pos(v.Final("adept"));
                UnityEngine.Vector2Int? fh = Pos(v.Final("hero"));
                if (fa.HasValue && fh.HasValue)
                    worst = Math.Min(worst, SquareGridUtils.GetDistance(fa.Value, fh.Value));
                return worst != int.MaxValue && worst >= 3 ? ExpectResult.Pass("closest " + worst) : ExpectResult.Fail("closest " + worst);
            });
            b.Expect("The plain goblin closes on the fighter", v =>
            {
                UnityEngine.Vector2Int? g = Pos(v.Final("gob"));
                UnityEngine.Vector2Int? h = Pos(v.Final("hero"));
                int d = g.HasValue && h.HasValue ? SquareGridUtils.GetDistance(g.Value, h.Value) : -1;
                return d == 1 ? ExpectResult.Pass("adjacent") : ExpectResult.Fail("distance " + d);
            });
            b.Expect("The adept shoots the fighter in round 1", v =>
                v.Attacks("adept", "hero", false, 1).Any(e => e.Bool("ranged")) ? ExpectResult.Pass("shot") : ExpectResult.Fail("no ranged attack"));
            if (plain)
                b.Expect("The plain adept does not move in round 1 (three squares is beyond its keep-away distance of 2)", v =>
                {
                    int moved = v.Moves("adept", 1).Sum(e => e.Int("sq"));
                    return moved == 0 ? ExpectResult.Pass("stayed") : ExpectResult.Fail("moved " + moved);
                });
            else
            {
                b.Expect("The cowardly adept backs off in round 1, farther from the fighter", v =>
                {
                    int moved = v.Moves("adept", 1).Sum(e => e.Int("sq"));
                    UnityEngine.Vector2Int? a = Pos(v.Snapshot("adept", 1, "gob"));
                    UnityEngine.Vector2Int? h = Pos(v.Snapshot("hero", 1, "gob"));
                    int d = a.HasValue && h.HasValue ? SquareGridUtils.GetDistance(a.Value, h.Value) : -1;
                    return moved > 0 && d > 3 ? ExpectResult.Pass("moved " + moved + ", now " + d + " squares off") : ExpectResult.Fail("moved " + moved + ", " + d + " squares off");
                });
                b.Expect("The decision trace records the keep-away distance 4 against the Role's 2", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("Personality.KeepAway");
                    return rows.Any(d => d.Legacy == "2" && d.Chosen == "4")
                        ? ExpectResult.Pass(rows.Count + " Personality.KeepAway row(s): legacy 2, chosen 4")
                        : MissingRow("Personality.KeepAway", rows.Count, "legacy 2 and chosen 4");
                });
            }
            return b.Build();
        }

        private enum AoOTwin { Cowardly, CowardlyPlain, Berserker, BerserkerPlain }

        /// <summary>
        /// An orc walks up to a fighter (its priority target) that stands behind a guard fighter: every square beside the
        /// target it can reach lies past the guard's threatened squares, so the shortest path there provokes the guard's AoO
        /// (PHB p.137), while a square two squares from the target does not. The movement score (AIService
        /// EvaluateMovementOptions) prices each provoker by AiBlend.AoOCost (C6). The orc is fatigued (PHB p.308), so it
        /// cannot charge and the move is the choice under test; both fighters act first (a flat-footed creature makes no AoO, PHB p.137) and idle, and everyone is sturdy; one round.
        /// - Cowardly: an orc berserker (Berserk Role, price 2 per provoker, so it takes the provoking path) given Cowardly
        ///   .6: its ProvokeAoO Against raises the price to 6, and it stops short without provoking; "-plain" provokes.
        /// - Berserker: an orc warrior (Humanoid Role, which avoids AoOs: 1000) given Berserker .6: its ProvokeAoO For lowers
        ///   the price to 2, and it takes the provoking path to its target; "-plain" stops short without provoking.
        /// </summary>
        private static ScenarioDef AiAoOPath(AoOTwin twin)
        {
            bool cowardly = twin == AoOTwin.Cowardly || twin == AoOTwin.CowardlyPlain;
            bool plain = twin == AoOTwin.CowardlyPlain || twin == AoOTwin.BerserkerPlain;
            string id = (cowardly ? "rules/ai-cowardly-avoids-aoo" : "rules/ai-berserker-takes-aoo-path") + (plain ? "-plain" : "");
            bool provokes = cowardly ? plain : !plain;
            string title = cowardly
                ? plain
                    ? "A plain orc berserker (Berserk Role, AoO price 2) takes the path past the guard that provokes, to reach its target (batch 14 step 6)"
                    : "A cowardly orc berserker (price 6 per provoker, ProvokeAoO Against) stops short rather than provoke the guard (batch 14 step 6, C6; PHB p.137)"
                : plain
                    ? "A plain orc warrior (Humanoid Role, avoids AoOs) stops short rather than provoke the guard (batch 14 step 6)"
                    : "A berserker orc warrior (price 2 per provoker, ProvokeAoO For) takes the path past the guard that provokes, to reach its target (batch 14 step 6, C6; PHB p.137)";
            ScenarioBuilder b = Rules(id, title)
                .Covers("batch 14 step 6", "PHB p.137", "PHB p.308")
                .MaxRounds(1)
                .Npc("orc", cowardly ? "orc_berserker" : "orc_warrior", 8, 10, Control.Ai)
                .Pc("guard", ActorSource.Stats(() => RulesScenarios.Fighter("Guard Fighter", 3)), 12, 10, Control.Idle)
                .Pc("target", ActorSource.Stats(() => RulesScenarios.Fighter("Target Fighter", 3)), 13, 10, Control.Idle)
                .PriorityTarget("orc", "target")
                .Tweak("orc", c => { Sturdy(c); Fatigue(c); })
                .Tweak("guard", Sturdy)
                .Tweak("target", Sturdy)
                // The fighters act first: a flat-footed creature makes no attacks of opportunity (PHB p.137).
                .Initiative("guard", "target", "orc");
            // Each twin pins the orc's traits, the plain ones to none, so a later definition change cannot make a plain twin a trait twin.
            b.Personality("orc", plain ? "" : cowardly ? "Cowardly:0.6" : "Berserker:0.6");
            b.Expect("The fatigued orc takes no charge (PHB p.308) and moves", v =>
            {
                int moved = v.Moves("orc", 1).Sum(e => e.Int("sq"));
                int charges = v.Log("is charging").Count;
                return charges == 0 && moved > 0 ? ExpectResult.Pass("moved " + moved) : ExpectResult.Fail(charges + " charge(s), moved " + moved);
            });
            if (provokes)
            {
                b.Expect("Its move provokes the guard's AoO (PHB p.137)",
                    Expect.Count("aoo", e => e.Str("by") == "guard" && e.Str("target") == "orc" && e.Str("trigger") == "movement", 1));
                b.Expect("It ends beside its target", v =>
                {
                    UnityEngine.Vector2Int? o = Pos(v.Final("orc"));
                    UnityEngine.Vector2Int? t = Pos(v.Final("target"));
                    int d = o.HasValue && t.HasValue ? SquareGridUtils.GetDistance(o.Value, t.Value) : -1;
                    return d == 1 ? ExpectResult.Pass("adjacent") : ExpectResult.Fail("distance " + d);
                });
            }
            else
            {
                // Only the movement counts: having stopped short beside the guard, the orc may use its standard action on it
                // (a trip provokes a maneuver AoO, PHB p.158), which is not the path choice under test.
                b.Expect("Its move provokes no AoO", Expect.None("aoo", e => e.Str("target") == "orc" && e.Str("trigger") == "movement"));
                b.Expect("It stops short of its target", v =>
                {
                    UnityEngine.Vector2Int? o = Pos(v.Final("orc"));
                    UnityEngine.Vector2Int? t = Pos(v.Final("target"));
                    int d = o.HasValue && t.HasValue ? SquareGridUtils.GetDistance(o.Value, t.Value) : -1;
                    return d >= 2 ? ExpectResult.Pass("distance " + d) : ExpectResult.Fail("distance " + d);
                });
            }
            if (!plain)
            {
                string want = cowardly ? "6" : "2";
                string legacy = cowardly ? "2" : "1000";
                b.Expect("The decision trace records the AoO price " + want + " against the Role's " + legacy, v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("Personality.AoOCost");
                    return rows.Any(d => d.Legacy == legacy && d.Chosen == want)
                        ? ExpectResult.Pass(rows.Count + " Personality.AoOCost row(s): legacy " + legacy + ", chosen " + want)
                        : MissingRow("Personality.AoOCost", rows.Count, "legacy " + legacy + " and chosen " + want);
                });
            }
            return b.Build();
        }

        /// <summary>
        /// A test Role for the out-of-reach order (batch 14 step 6, C8): a casting Role on the kiter routine (Ranged style;
        /// a casting profile draws no weapon, AIProfile.TryEnsureWeaponFallback) whose out-of-reach list is fixed, so a
        /// scenario holds a list with both an approach and total defense (no shipped Role's C1 list has both).
        /// </summary>
        private sealed class FixedOutOfReachRole : DND35.AI.Profiles.SpellcasterAIProfile
        {
            public OutOfReachAlternative[] Alternatives = new OutOfReachAlternative[0];

            public override void ApplyRuntimeDefaults()
            {
                base.ApplyRuntimeDefaults();
                ProfileName = "FixedOutOfReach";
                CombatStyle = DND35.AI.CombatStyle.Ranged;
            }

            public override IReadOnlyList<OutOfReachAlternative> GetOutOfReachAlternatives(CharacterController self) => Alternatives;
        }

        private static DND35.AI.AIProfile OutOfReachRole(params OutOfReachAlternative[] list)
        {
            FixedOutOfReachRole role = DND35.AI.AIProfile.CreateRuntime<FixedOutOfReachRole>();
            role.Alternatives = list;
            return role;
        }

        /// <summary>
        /// A creature with only a morningstar (no ranged means) five squares from an idle fighter, on a test Role whose
        /// out-of-reach list holds another target, an approach and total defense. No target is in reach, so the list runs
        /// (AiOutOfReach.Run): another target fails, then the first of the approach and total defense that it can do.
        /// AiBlend.OrderOutOfReach re-weights the Role's list by RoleDesire (each entry: minus its place plus 2 times the
        /// desire of its roles, C8):
        /// - the cowardly caster (the goblin adept, Cowardly .6 and Healer .4; Role list attack other, approach, total
        ///   defense): approach -1.6, total defense -1.32, so it takes total defense; "-plain" approaches and attacks;
        /// - the berserker (a goblin warrior given Berserker .6; Role list attack other, total defense, approach): approach
        ///   -1.28, total defense -1.72, so it approaches and attacks; "-plain" takes total defense.
        /// One round; the fighter idles and everyone is sturdy.
        /// </summary>
        private static ScenarioDef AiOutOfReachOrder(bool berserker, bool plain)
        {
            string id = (berserker ? "rules/ai-berserker-approaches-first" : "rules/ai-cowardly-caster-defends-before-approach") + (plain ? "-plain" : "");
            bool approaches = berserker ? !plain : plain;
            OutOfReachAlternative[] list = berserker
                ? new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.TotalDefense, OutOfReachAlternative.Approach }
                : new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.Approach, OutOfReachAlternative.TotalDefense };
            string name = berserker ? "Goblin Warrior" : "Goblin Adept";
            string title = berserker
                ? plain
                    ? "Out of reach, a plain goblin takes total defense, its Role's next choice (batch 14 step 6)"
                    : "Out of reach, a berserker goblin approaches and attacks before its Role's total defense: RoleDesire re-weights the list (batch 14 step 6, C8)"
                : plain
                    ? "Out of reach, the plain goblin adept approaches and attacks, its Role's next choice (batch 14 step 6)"
                    : "Out of reach, the cowardly goblin adept takes total defense before its Role's approach: RoleDesire re-weights the list (batch 14 step 6, C8)";
            ScenarioBuilder b = Rules(id, title)
                .Covers("batch 14 step 6", "PHB p.142")
                .MaxRounds(1)
                .Npc("npc", berserker ? "goblin_warrior" : "goblin_adept", 8, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 13, 10, Control.Idle)
                .Profile("npc", () => OutOfReachRole(list))
                .Tweak("npc", c => { Sturdy(c); MorningstarOnly(c); })
                .Tweak("hero", Sturdy)
                .Initiative("npc", "hero");
            // Every twin pins its traits: the cowardly one the adept's Cowardly .6 and Healer .4 explicitly (the order margin,
            // -1.32 against -1.6, depends on those values), so a retune of goblin_adept cannot move it.
            b.Personality("npc", plain ? "" : berserker ? "Berserker:0.6" : "Cowardly:0.6,Healer:0.4");
            if (!berserker)
                ExpectNoCast(b, "npc");
            if (approaches)
                b.Expect("It approaches the fighter and attacks it; no total defense", v =>
                {
                    int moved = v.Moves("npc", 1).Sum(e => e.Int("sq"));
                    int attacks = v.Attacks("npc", "hero", false, 1).Count;
                    int defense = v.Log(name + " takes total defense").Count;
                    return moved > 0 && attacks > 0 && defense == 0
                        ? ExpectResult.Pass("moved " + moved + ", " + attacks + " attack(s)")
                        : ExpectResult.Fail("moved " + moved + ", " + attacks + " attack(s), " + defense + " total defense");
                });
            else
                b.Expect("It takes total defense where it stands (PHB p.142)", v =>
                {
                    int moved = v.Moves("npc", 1).Sum(e => e.Int("sq"));
                    int defense = v.Log(name + " takes total defense").Count;
                    return moved == 0 && defense == 1 ? ExpectResult.Pass("total defense, no move") : ExpectResult.Fail("moved " + moved + ", " + defense + " total defense");
                });
            if (!plain)
                b.Expect("The decision trace records the re-weighted list against the Role's", v =>
                {
                    string roleOrder = string.Join(">", list.Select(a => a.ToString()));
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("Personality.OutOfReach");
                    return rows.Any(d => d.Legacy == roleOrder && d.Chosen != roleOrder)
                        ? ExpectResult.Pass("Role " + roleOrder + ", blend " + rows.First(d => d.Legacy == roleOrder).Chosen)
                        : MissingRow("Personality.OutOfReach", rows.Count, "changing " + roleOrder);
                });
            return b.Build();
        }
    }
}
#endif
