#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
#endif
