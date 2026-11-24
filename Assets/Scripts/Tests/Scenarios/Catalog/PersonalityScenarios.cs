#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DND35.AI;
using DND35.AI.Valuation;
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
    /// <para>Step 8 (the maneuver odds, a pure refactor): rules/maneuver-odds-disarm and rules/maneuver-odds-sunder, a
    /// Scripted fighter's disarm and sunder of an orc (forced 20 against 1): right before the maneuver a step notes the AI's
    /// odds (DND35.AI.Valuation.ManeuverOdds, the item the AI decider picks, the step's BAB), and the roll then adds exactly
    /// the terms the odds read (the shared GetDisarmCheckModifiers and sunder terms). No AI reads the odds yet (step 9), so
    /// the step changes no other scenario's hash.</para>
    /// <para>Step 9 (maneuver versus attack by odds and value; AI-035, AI-060, AI-058): rules/ai-maneuver-trip-when-odds-good,
    /// an AI fighter with Improved Trip trips an armored orc it rarely hits, then attacks it prone (the Role's disarm loses);
    /// rules/ai-maneuver-trip-fail-redecided, its failed trip is decided again at the next step and tried again (no turn
    /// memory); rules/ai-maneuver-attacks-when-odds-poor, a goblin swings at a stronger fighter instead of tripping it;
    /// rules/ai-maneuver-skips-helpless, a goblin attacks a dying PC instead of tripping it. The expectations read the
    /// scorer's "Maneuver" rows of the decision trace (outside the hash) besides the hashed maneuvers and attacks. The first
    /// two replace rules/ai-maneuver-stopgap-trip and -trip-fail.</para>
    /// <para>Step 11 (the move left after a standard-action ability; AI-077, D1, C8): rules/ai-kiter-casts-then-retreats, an
    /// adept kiter casts at a fighter two squares off and then backs away; rules/ai-melee-caster-casts-then-closes, a cleric casts
    /// first and then closes on a far hero without attacking (the move action is all it has left); rules/ai-quickened-cast-
    /// continues-turn, an AI-run wizard's quickened Magic Missile (a free action, PHB p.98) leaves its standard action for a
    /// second cast in the same round; rules/ai-fullround-cast-no-move, a Summon Monster I (a 1-round, full-round cast) leaves
    /// no move; rules/ai-lay-on-hands-then-moves, a wounded paladin heals itself and then closes on the fighter;
    /// rules/ai-cowardly-casts-then-retreats, a Cowardly cleric backs away after its cast where its -plain twin closes in;
    /// rules/ai-cleric-move-keeps-turning, a cleric's move after its turning never ends within 10 feet of an undead it
    /// turned (PHB p.159); rules/ai-threatened-caster-steps-away, a threatened adept 5-foot steps out of the threat after
    /// its cast instead of walking off through the AoO (PHB p.144, AI-088). The expectations read the "AfterStandard" and "Personality.AfterStandard" rows of the decision trace (outside the
    /// hash) besides the hashed casts and moves.</para>
    /// <para>Step 12 (the healer reaches its patient; AI-047, AI-063, Q8): rules/ai-healer-moves-to-touch-and-cures, a Healer-Role
    /// cleric moves beside its wounded kobold 3 squares off and cures it in the same turn; rules/ai-healer-approaches-far-ally,
    /// it moves toward a kobold beyond one move; rules/ai-healer-critical-provokes, it takes a fighter's attack of opportunity
    /// to reach a kobold at 20% (a provoking path costs 6 per provoker for a critical patient); rules/ai-healer-hurt-ally-no-aoo,
    /// it does not provoke for a kobold at 50%; rules/ai-healer-cowardly-skips-aoo-approach, a hurt Cowardly cleric never
    /// provokes, even for the critical kobold; rules/ai-healer-no-spell-no-approach, the goblin adept, with no spells before
    /// batch 15, makes no Healing turn and never approaches. The expectations read the "HealerApproach" rows of the decision
    /// trace (outside the hash) besides the hashed moves, casts and AoOs.</para>
    /// <para>Step 13 (aid another, total defense and posture; AI-063, D3-D5): rules/ai-weak-attacker-aids-offense, a goblin that
    /// hits the armoured fighter only on a 20 aids the ogre's attack instead; rules/ai-strong-attacker-attacks, the ogre
    /// attacks; rules/ai-aids-defense-badly-hurt-ally, a goblin with a longspear aids the defense of its badly hurt ally the
    /// fighter is engaging; rules/ai-total-defense-only-when-reachable (and its "-reachable" and "-charge" twins), total
    /// defense out of reach only when a foe can reach the creature, by a move or a charge; rules/ai-prone-caster-stays-prone
    /// (and its "-archer-close" twin), a prone caster facing an archer stays down and casts, and is not stood up again by
    /// the move after its cast; rules/ai-prone-melee-stands, a prone melee goblin stands as before;
    /// rules/ai-prone-healer-stands-to-cure, a prone Healer whose cure needs a step stands and cures. The expectations read the
    /// "MeleeTurnOption" and "ProneStart" rows of the decision trace besides the hashed aid, attacks, casts and conditions.</para>
    /// <para>Step 14 (domain powers; batch 13's C5 leftover, D7, C14): the DMG sample cleric given domains by Tweak, its slots
    /// spent so it fights in melee. rules/ai-cleric-strength-before-full-attack, a Strength cleric beside the fighter performs
    /// its feat of strength before its melee attack and attacks with it (the "-charge" twin uses it before its charge's
    /// attack, the "-far" twin, nobody in reach this turn, keeps it);
    /// rules/ai-cleric-death-touch-low-hp-foe, a Death cleric touches the fighter at 10 hit points instead of attacking (the
    /// "-healthy-foe-attacks" twin attacks a sturdy fighter, the "-ends-invisibility" twin, an invisible cleric under
    /// Sanctuary, becomes visible and loses its Sanctuary by the touch, the "-sanctuary-save" twin loses the touch and its
    /// standard action, not the day's use, to the fighter's Sanctuary); rules/ai-cleric-destruction-smite-first-swing, a Destruction
    /// cleric smites with its first swing, once. The expectations read the "DomainPower" rows of the decision trace besides
    /// the hashed log lines and attacks.</para>
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
            yield return S("maneuver-odds-disarm", () => ManeuverOddsMatchRoll(SpecialAttackType.Disarm));
            yield return S("maneuver-odds-sunder", () => ManeuverOddsMatchRoll(SpecialAttackType.Sunder));
            yield return S("ai-maneuver-trip-when-odds-good", () => AiManeuverTripOddsGood(tripLands: true));
            yield return S("ai-maneuver-trip-fail-redecided", () => AiManeuverTripOddsGood(tripLands: false));
            yield return S("ai-maneuver-attacks-when-odds-poor", AiManeuverAttacksWhenOddsPoor);
            yield return S("ai-maneuver-skips-helpless", AiManeuverSkipsHelpless);
            yield return S("ai-kiter-casts-then-retreats", AiKiterCastsThenRetreats);
            yield return S("ai-melee-caster-casts-then-closes", AiMeleeCasterCastsThenCloses);
            yield return S("ai-quickened-cast-continues-turn", AiQuickenedCastContinuesTurn);
            yield return S("ai-fullround-cast-no-move", AiFullRoundCastNoMove);
            yield return S("ai-lay-on-hands-then-moves", AiLayOnHandsThenMoves);
            yield return S("ai-cowardly-casts-then-retreats", () => AiCowardlyCastsThenRetreats(plain: false));
            yield return S("ai-cowardly-casts-then-retreats-plain", () => AiCowardlyCastsThenRetreats(plain: true));
            yield return S("ai-cleric-move-keeps-turning", AiClericMoveKeepsTurning);
            yield return S("ai-threatened-caster-steps-away", AiThreatenedCasterStepsAway);
            yield return S("ai-healer-moves-to-touch-and-cures", AiHealerMovesToTouchAndCures);
            yield return S("ai-healer-approaches-far-ally", AiHealerApproachesFarAlly);
            yield return S("ai-healer-critical-provokes", AiHealerCriticalProvokes);
            yield return S("ai-healer-hurt-ally-no-aoo", AiHealerHurtAllyNoAoO);
            yield return S("ai-healer-cowardly-skips-aoo-approach", AiHealerCowardlySkipsAoOApproach);
            yield return S("ai-healer-no-spell-no-approach", AiHealerNoSpellNoApproach);
            yield return S("ai-weak-attacker-aids-offense", AiWeakAttackerAidsOffense);
            yield return S("ai-strong-attacker-attacks", AiStrongAttackerAttacks);
            yield return S("ai-aids-defense-badly-hurt-ally", AiAidsDefenseBadlyHurtAlly);
            yield return S("ai-total-defense-only-when-reachable", () => AiTotalDefenseOnlyWhenReachable(19));
            yield return S("ai-total-defense-only-when-reachable-reachable", () => AiTotalDefenseOnlyWhenReachable(7));
            yield return S("ai-total-defense-only-when-reachable-charge", () => AiTotalDefenseOnlyWhenReachable(11));
            yield return S("ai-total-defense-only-when-reachable-charge-prone", () => AiTotalDefenseOnlyWhenReachable(11, heroProne: true));
            yield return S("ai-prone-caster-stays-prone", () => AiProneCasterStaysProne(18));
            yield return S("ai-prone-caster-stays-prone-archer-close", () => AiProneCasterStaysProne(12));
            yield return S("ai-prone-melee-stands", AiProneMeleeStands);
            yield return S("ai-prone-healer-stands-to-cure", AiProneHealerStandsToCure);
            yield return S("ai-cleric-strength-before-full-attack", () => AiClericStrengthBeforeFullAttack(StrengthTwin.Adjacent));
            yield return S("ai-cleric-strength-before-full-attack-charge", () => AiClericStrengthBeforeFullAttack(StrengthTwin.Charge));
            yield return S("ai-cleric-strength-before-full-attack-far", () => AiClericStrengthBeforeFullAttack(StrengthTwin.Far));
            yield return S("ai-cleric-death-touch-low-hp-foe", () => AiClericDeathTouch(lowHp: true));
            yield return S("ai-cleric-death-touch-healthy-foe-attacks", () => AiClericDeathTouch(lowHp: false));
            yield return S("ai-cleric-death-touch-ends-invisibility", () => AiClericDeathTouchAttackRules(sanctuaryOnFoe: false));
            yield return S("ai-cleric-death-touch-sanctuary-save", () => AiClericDeathTouchAttackRules(sanctuaryOnFoe: true));
            yield return S("ai-cleric-destruction-smite-first-swing", AiClericDestructionSmiteFirstSwing);
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
        /// decision but the maneuver scorer's is "legacy": since batch 14 step 9 the "Maneuver" rows compare the scorer's
        /// choice with the Role's proposal, which it is meant to overrule (here every goblin and the hero keep the swing
        /// over the Role's trip, AI-035).
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
                .Expect("Target choices are recorded and every recorded decision but the maneuver scorer's is the Role's legacy choice", v =>
                {
                    IReadOnlyDictionary<string, int> counts = global::AiDecisionTrace.Counts;
                    counts.TryGetValue("Target: legacy", out int targets);
                    int changed = counts.Where(kv => kv.Key.EndsWith(": changed", StringComparison.Ordinal)
                                                     && !kv.Key.StartsWith("Maneuver:", StringComparison.Ordinal)).Sum(kv => kv.Value);
                    string detail = (targets > 0 ? "target choices recorded" : "no target choice recorded") + ", " + changed + " changed";
                    return targets > 0 && changed == 0 ? ExpectResult.Pass(detail) : ExpectResult.Fail(detail);
                })
                .Expect("Every maneuver scorer row keeps the swing over the Role's trip (AI-035)", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = global::AiDecisionTrace.Ring.Concat(global::AiDecisionTrace.OpenRecords)
                        .Where(t => t.Round <= 2).SelectMany(t => t.Decisions).Where(d => d.Site == "Maneuver").ToList();
                    bool ok = rows.Count > 0 && rows.All(d => d.Chosen == "Attack" && d.Legacy == "Trip");
                    string detail = rows.Count + " Maneuver row(s): " + RowsText(rows);
                    return ok ? ExpectResult.Pass(detail) : ExpectResult.Fail(detail);
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

        // ── Step 8: the maneuver odds read the roll's terms ─────────────

        /// <summary>
        /// Batch 14 step 8 (PHB p.155, p.158; D6): a Scripted fighter 6 disarms or sunders an orc that makes no AoO, with the
        /// opposed rolls forced to 20 against 1. Right before the maneuver a step computes the AI's odds for the same attempt
        /// (the item the AI decider picks, AiManeuverDecider.ChooseItem; the step's BAB, ManeuverOdds.StepBab) and notes the
        /// terms they read (TargetNumbers.TryDisarm or TrySunder). The maneuver event then shows the roll added exactly those
        /// terms to each d20, and the forced dice win.
        /// </summary>
        private static ScenarioDef ManeuverOddsMatchRoll(SpecialAttackType type)
        {
            bool sunder = type == SpecialAttackType.Sunder;
            string id = sunder ? "rules/maneuver-odds-sunder" : "rules/maneuver-odds-disarm";
            string title = sunder
                ? "A sunder rolls the terms the AI's maneuver odds read (PHB p.158; batch 14 step 8)"
                : "A disarm rolls the terms the AI's maneuver odds read (PHB p.155; batch 14 step 8)";
            string notePrefix = "maneuver-odds " + type + " ";
            return Rules(id, title)
                .Covers("AI-035", "batch 14 step 8", sunder ? "PHB p.158" : "PHB p.155")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("fighter", RulesScenarios.StripOffHand)
                .Tweak("orc", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Initiative("fighter", "orc")
                .Force(20, 20, sunder ? "Sunder attack roll" : "Disarm attack roll")
                .Force(20, 1, sunder ? "Sunder defense roll" : "Disarm defense roll")
                .Turn("fighter", 1, Step.Assert("the AI's odds for the attempt are computed", ctx => NoteManeuverOdds(ctx, type, notePrefix)),
                    Step.Maneuver(type, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Expect("The odds step holds and the maneuver step is done", Expect.All(
                    Expect.AssertsPass(),
                    Expect.StepStatus("fighter", 1, "Maneuver", 0, "done")))
                .Expect("The roll adds exactly the terms the odds read to each d20, and the forced 20 against 1 wins", v =>
                {
                    TraceEvent note = v.Of("note").FirstOrDefault(e => (e.Str("text") ?? "").StartsWith(notePrefix, StringComparison.Ordinal));
                    TraceEvent m = v.Maneuvers("fighter", type).FirstOrDefault();
                    if (note == null) return ExpectResult.Fail("no odds note");
                    if (m == null) return ExpectResult.Fail("no " + type, note.Seq);
                    int atk = NoteInt(note.Str("text"), "atk"), def = NoteInt(note.Str("text"), "def");
                    int rolledAtk = m.Int("check") - m.Int("checkRoll"), rolledDef = m.Int("opposed") - m.Int("opposedRoll");
                    bool same = rolledAtk == atk && rolledDef == def;
                    return same && m.Bool("success") && m.Int("checkRoll") == 20 && m.Int("opposedRoll") == 1
                        ? ExpectResult.Pass("odds +" + atk + " against +" + def + ", rolled the same", note.Seq, m.Seq)
                        : ExpectResult.Fail("odds " + atk + "/" + def + ", rolled " + rolledAtk + "/" + rolledDef + ", success " + m.Get("success"), note.Seq, m.Seq);
                })
                .Build();
        }

        private static bool NoteManeuverOdds(ScenarioContext ctx, SpecialAttackType type, string notePrefix)
        {
            CharacterController fighter = ctx.Get("fighter");
            CharacterController orc = ctx.Get("orc");
            if (fighter == null || orc == null)
                return false;
            EquipSlot? slot = null;
            AiManeuverDecider.Instance.ChooseItem(ctx.Gm, fighter, orc, type, s => slot = s);
            int bab = ManeuverOdds.StepBab(fighter);
            bool got;
            int atk, def;
            if (type == SpecialAttackType.Sunder)
            {
                got = TargetNumbers.TrySunder(fighter, orc, slot, bab, out CharacterController.SunderCheckModifiers m);
                atk = m.AttackerModifier;
                def = m.DefenderModifier;
            }
            else
            {
                got = TargetNumbers.TryDisarm(fighter, orc, slot, bab, out CharacterController.DisarmCheckModifiers m);
                atk = m.AttackerModifier;
                def = m.DefenderModifier;
            }
            ManeuverOddsEstimate odds = ManeuverOdds.Estimate(fighter, orc, type, bab, slot);
            ctx.Note(notePrefix + "bab=" + bab + " atk=" + atk + " def=" + def + " provokers=" + odds.Provokers
                + " success=" + odds.SuccessChance.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
            return got && odds.Supported && odds.CanSucceed && odds.Provokers == 0;
        }

        private static int NoteInt(string text, string key)
        {
            if (string.IsNullOrEmpty(text))
                return int.MinValue;
            foreach (string part in text.Split(' '))
                if (part.StartsWith(key + "=", StringComparison.Ordinal) && int.TryParse(part.Substring(key.Length + 1), out int value))
                    return value;
            return int.MinValue;
        }

        // ── Step 9: maneuver versus attack by odds and value (AI-035, AI-060, AI-058) ──

        /// <summary>
        /// The decision-trace rows of the "Maneuver" site (DND35.AI.AiManeuverChooser.Decide, one per decided step that had a
        /// proposal or a coup de grace) of <paramref name="actorName"/>'s round-<paramref name="round"/> turn, in order.
        /// </summary>
        private static List<global::AiDecisionTrace.Decision> ManeuverRows(string actorName, int round)
            => global::AiDecisionTrace.Ring.Concat(global::AiDecisionTrace.OpenRecords)
                .Where(t => t.Round == round && t.Actor == actorName)
                .SelectMany(t => t.Decisions)
                .Where(d => d.Site == "Maneuver")
                .ToList();

        private static string RowsText(List<global::AiDecisionTrace.Decision> rows)
            => rows.Count == 0 ? "no Maneuver row" : string.Join("; ", rows.Select(r => r.Chosen + " (proposal " + r.Legacy + ": " + r.Reason + ")"));

        /// <summary>
        /// An AI-run fighter 11 (Str 20: +16/+11/+6 with a longsword; Improved Trip: no AoO, +4 on the check and an attack
        /// after a trip that lands, PHB p.96; the Humanoid Role, which proposes a trip, then a disarm of an armed target) beside
        /// a sturdy, scripted orc holding a greataxe, given natural armor +17 (AC 30, touch AC 10) and no attacks of
        /// opportunity; the fighter's own AC is raised by natural armor +10 so the orc's threat is small. A swing hits on a
        /// 14 or better, a prone orc on a 10, while the trip is nearly sure, so the scorer trips with the first step
        /// (batch 14 step 9, C9). With the trip forced to land, the remaining steps are attacks on the prone orc: no trip is
        /// proposed against a prone target, and the disarm the Role proposes instead is worth less than the swing (the orc's
        /// threat is small). With every trip touch attack a natural 1 (<paramref name="tripLands"/> false), each step is
        /// decided again with no turn memory (AI-060's stopgap is gone): the failed trip still beats the +11 swing, so the
        /// second step trips again. Rewritten from rules/ai-maneuver-stopgap-trip and -trip-fail (2026-10-07).
        /// </summary>
        private static ScenarioDef AiManeuverTripOddsGood(bool tripLands)
        {
            string id = tripLands ? "rules/ai-maneuver-trip-when-odds-good" : "rules/ai-maneuver-trip-fail-redecided";
            ScenarioBuilder b = Rules(id, tripLands
                    ? "An AI fighter with Improved Trip trips an armored orc it rarely hits, then attacks the prone orc (batch 14 step 9, AI-035)"
                    : "After its trip fails, an AI fighter decides the next step again and trips again: no turn memory (batch 14 step 9, AI-060)")
                .Covers("AI-035", "AI-060", "CMB-102", "batch 14 step 9", "PHB p.96", "PHB p.158")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Fighter", 11, "Improved Trip")), 10, 10, Control.Ai)
                .Profile("fighter", () => DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.HumanoidAIProfile>())
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("fighter", c =>
                {
                    RulesScenarios.StripOffHand(c);
                    c.Stats.STR = 20;
                    c.Stats.NaturalArmorBonus += 10;
                })
                .Tweak("orc", c =>
                {
                    Sturdy(c);
                    c.Stats.CanMakeAttacksOfOpportunity = false;
                    c.Stats.NaturalArmorBonus += 17;
                })
                .Initiative("fighter", "orc")
                .Turn("orc", 0, Step.Pass());
            if (tripLands)
                b.Force(20, 20, "Trip touch attack", -1)
                 .Force(20, 18, "Trip Strength check", -1)
                 .Force(20, 2, "Trip defense check", -1);
            else
                b.Force(20, 1, "Trip touch attack", -1);

            b.Expect("The fighter's turn is an AI turn and the orc makes no AoO (fixture checks)", Expect.All(
                Expect.Controller("fighter", "ai"),
                Expect.None("aoo", e => e.Str("by") == "orc")));
            if (tripLands)
            {
                b.Expect("One maneuver in the turn: a trip that lands with the first step; no disarm or grapple follows", v =>
                {
                    List<TraceEvent> ms = v.Maneuvers("fighter").Where(e => e.Round == 1).ToList();
                    string kinds = string.Join(",", ms.Select(e => Convert.ToString(e.Get("type"))));
                    if (ms.Count != 1) return ExpectResult.Fail(ms.Count + " maneuvers (" + kinds + ")", ms.Select(e => e.Seq).ToArray());
                    TraceEvent trip = ms[0];
                    if (!(trip.Get("type") is SpecialAttackType t) || t != SpecialAttackType.Trip || !trip.Bool("success"))
                        return ExpectResult.Fail("first maneuver " + kinds + ", success " + trip.Get("success"), trip.Seq);
                    if (v.Attacks("fighter", "orc", false, 1).Any(e => e.Seq < trip.Seq))
                        return ExpectResult.Fail("an attack came before the trip", trip.Seq);
                    return HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("trip lands, orc prone", trip.Seq) : ExpectResult.Fail("orc not prone", trip.Seq);
                });
                b.Expect("The last two attacks are the remaining iteratives, 5 apart (+11, +6; PHB p.143)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("fighter", "orc", false, 1);
                    if (attacks.Count < 2) return ExpectResult.Fail(attacks.Count + " attacks", attacks.Select(e => e.Seq).ToArray());
                    TraceEvent a = attacks[attacks.Count - 2], z = attacks[attacks.Count - 1];
                    return a.Int("mod") - z.Int("mod") == 5
                        ? ExpectResult.Pass(attacks.Count + " attacks, last mods " + a.Int("mod") + ", " + z.Int("mod"), a.Seq, z.Seq)
                        : ExpectResult.Fail("last mods " + a.Int("mod") + ", " + z.Int("mod"), a.Seq, z.Seq);
                });
                b.Expect("The scorer's rows: a trip first, then the attack over the Role's disarm on the prone orc", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = ManeuverRows("Fighter", 1);
                    bool ok = rows.Count == 3 && rows[0].Chosen == "Trip" && rows[1].Chosen == "Attack" && rows[1].Legacy == "Disarm"
                              && rows[2].Chosen == "Attack" && rows[2].Legacy == "Disarm";
                    return ok ? ExpectResult.Pass(RowsText(rows)) : ExpectResult.Fail(RowsText(rows));
                });
            }
            else
            {
                b.Expect("The first two steps are trips that fail: the second is decided again, not barred (no turn memory)", v =>
                {
                    List<TraceEvent> ms = v.Maneuvers("fighter", SpecialAttackType.Trip).Where(e => e.Round == 1).ToList();
                    if (ms.Count < 2) return ExpectResult.Fail(ms.Count + " trip(s)", ms.Select(e => e.Seq).ToArray());
                    if (ms.Any(e => e.Bool("success"))) return ExpectResult.Fail("a trip succeeded", ms.Select(e => e.Seq).ToArray());
                    List<TraceEvent> attacks = v.Attacks("fighter", "orc", false, 1);
                    if (attacks.Any(e => e.Seq < ms[1].Seq)) return ExpectResult.Fail("an attack came between the trips", ms[1].Seq);
                    return HasCond(v.Final("orc"), "Prone")
                        ? ExpectResult.Fail("orc prone", ms[0].Seq)
                        : ExpectResult.Pass(ms.Count + " failed trips, " + attacks.Count + " attack(s)", ms[0].Seq, ms[1].Seq);
                });
                b.Expect("The scorer's rows: each step decided, the first two trips", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = ManeuverRows("Fighter", 1);
                    bool ok = rows.Count == 3 && rows[0].Chosen == "Trip" && rows[1].Chosen == "Trip" && rows.All(r => r.Legacy == "Trip");
                    return ok ? ExpectResult.Pass(RowsText(rows)) : ExpectResult.Fail(RowsText(rows));
                });
            }
            return b.Build();
        }

        /// <summary>
        /// An AI-run goblin (Small, Str 11, BAB +1; its spawn's Humanoid Role proposes a trip) beside a sturdy fighter 3
        /// (Str 16). Its trip would hit the fighter's touch AC about half the time and win the opposed check (+0, -4 for its
        /// size, against the fighter's +3, PHB p.158) about one time in six, and the fighter's AoO would be paid first: the
        /// scorer keeps the swing (the plan's example; batch 14 step 9, AI-035). Before step 9 the goblin tripped. One round.
        /// </summary>
        private static ScenarioDef AiManeuverAttacksWhenOddsPoor()
        {
            return Rules("rules/ai-maneuver-attacks-when-odds-poor",
                    "An AI goblin swings at a stronger fighter instead of trying a trip it would rarely win (batch 14 step 9, AI-035)")
                .Covers("AI-035", "batch 14 step 9", "PHB p.158")
                .MaxRounds(1)
                .Npc("gob", "goblin", 10, 10, Control.Ai)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Fighter", 3)), 11, 10, Control.Idle)
                .Tweak("fighter", Sturdy)
                .Initiative("gob", "fighter")
                .Expect("The goblin runs the Humanoid Role (fixture check)", v =>
                {
                    TraceEvent actor = v.Of("actor").FirstOrDefault(e => e.Str("key") == "gob");
                    string profile = actor != null ? actor.Str("profile") : null;
                    return profile == "HumanoidAIProfile" ? ExpectResult.Pass(profile) : ExpectResult.Fail("profile " + (profile ?? "none"));
                })
                .Expect("The goblin makes no maneuver and attacks the fighter", v =>
                {
                    List<TraceEvent> ms = v.Maneuvers("gob").Where(e => e.Round == 1).ToList();
                    if (ms.Count > 0) return ExpectResult.Fail(ms.Count + " maneuver(s), first " + ms[0].Get("type"), ms[0].Seq);
                    List<TraceEvent> attacks = v.Attacks("gob", "fighter", false, 1);
                    return attacks.Count > 0 ? ExpectResult.Pass(attacks.Count + " attack(s)", attacks[0].Seq) : ExpectResult.Fail("no attack");
                })
                .Expect("The scorer's row: the Role's trip proposal loses to the attack", v =>
                {
                    TraceEvent actor = v.Of("actor").FirstOrDefault(e => e.Str("key") == "gob");
                    List<global::AiDecisionTrace.Decision> rows = ManeuverRows(actor != null ? actor.Str("name") : "Goblin", 1);
                    return rows.Count > 0 && rows[0].Chosen == "Attack" && rows[0].Legacy == "Trip"
                        ? ExpectResult.Pass(RowsText(rows)) : ExpectResult.Fail(RowsText(rows));
                })
                .Build();
        }

        /// <summary>
        /// An AI-run goblin (the Humanoid Role, which keeps downed foes and proposes a trip; its coup de grace turned off by
        /// the data override, so only the maneuver choice is tested) beside a dying wizard (-3 hit points: helpless, PHB p.145),
        /// its target pinned to the wizard; a sturdy fighter stands far off. A helpless target gets the attack, never a trip,
        /// disarm or grapple (AI-058; batch 14 step 9): before step 9 the plain orc of rules/ai-sadist-coup-de-grace-plain
        /// tripped such a wizard. One round.
        /// </summary>
        private static ScenarioDef AiManeuverSkipsHelpless()
        {
            return Rules("rules/ai-maneuver-skips-helpless",
                    "An AI goblin attacks the dying PC beside it instead of tripping it: no maneuver on a helpless target (batch 14 step 9, AI-058)")
                .Covers("AI-058", "batch 14 step 9", "PHB p.145", "PHB p.153")
                .MaxRounds(1)
                .Npc("gob", "goblin", 10, 10, Control.Ai)
                .Pc("fallen", ActorSource.Stats(() => Wizard("Fallen Wizard", 2)), 11, 10, Control.Idle)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Far Fighter", 3)), 2, 2, Control.Idle)
                .PriorityTarget("gob", "fallen")
                .Tweak("gob", c => c.EnemyUseCoupDeGraceOverride = false)
                .Tweak("fallen", c => SetHp(c, 14, -3))
                .Tweak("hero", Sturdy)
                .Initiative("gob", "fallen", "hero")
                .Expect("The wizard is dying (helpless) when the goblin's turn starts", v =>
                {
                    JsonObj snap = v.Snapshot("fallen", 1, "gob");
                    object hp = snap != null ? snap.Get("hp") : null;
                    return hp is int h && h < 0 ? ExpectResult.Pass("hp " + h) : ExpectResult.Fail("hp " + (hp ?? "?"));
                })
                .Expect("The goblin makes no maneuver and attacks the wizard", v =>
                {
                    List<TraceEvent> ms = v.Maneuvers("gob").Where(e => e.Round == 1).ToList();
                    if (ms.Count > 0) return ExpectResult.Fail(ms.Count + " maneuver(s), first " + ms[0].Get("type") + " on " + ms[0].Str("target"), ms[0].Seq);
                    List<TraceEvent> attacks = v.Attacks("gob", "fallen", false, 1);
                    return attacks.Count > 0 ? ExpectResult.Pass(attacks.Count + " attack(s) on the wizard", attacks[0].Seq) : ExpectResult.Fail("no attack on the wizard");
                })
                .Expect("The scorer's row: the Role's proposal is set aside because the target is helpless", v =>
                {
                    TraceEvent actor = v.Of("actor").FirstOrDefault(e => e.Str("key") == "gob");
                    List<global::AiDecisionTrace.Decision> rows = ManeuverRows(actor != null ? actor.Str("name") : "Goblin", 1);
                    return rows.Count > 0 && rows[0].Chosen == "Attack" && (rows[0].Reason ?? "").Contains("AI-058")
                        ? ExpectResult.Pass(RowsText(rows)) : ExpectResult.Fail(RowsText(rows));
                })
                .Build();
        }

        // ── Batch 14 step 11: the move left after a standard-action ability (AI-077; D1, C8) ──

        /// <summary>The distance in squares between two actors at the end of the job, or -1.</summary>
        private static int FinalDistance(TraceView v, string a, string b)
        {
            UnityEngine.Vector2Int? pa = Pos(v.Final(a));
            UnityEngine.Vector2Int? pb = Pos(v.Final(b));
            return pa.HasValue && pb.HasValue ? SquareGridUtils.GetDistance(pa.Value, pb.Value) : -1;
        }

        /// <summary>
        /// In round 1, <paramref name="by"/> takes its ability (the first event <paramref name="ability"/> picks) and then
        /// moves: a move event of its own after the ability's seq (PHB p.138: a standard and a move action in either order).
        /// </summary>
        private static Func<TraceView, ExpectResult> MovesAfter(string by, string what, Func<TraceView, TraceEvent> ability)
            => v =>
            {
                TraceEvent a = ability(v);
                if (a == null)
                    return ExpectResult.Fail("no " + what + " in round 1");
                TraceEvent move = v.Moves(by, 1).FirstOrDefault(e => e.Seq > a.Seq);
                return move != null
                    ? ExpectResult.Pass(what + " #" + a.Seq + ", then a move of " + move.Int("sq") + " square(s) #" + move.Seq, a.Seq, move.Seq)
                    : ExpectResult.Fail(what + " #" + a.Seq + ", no move after it", a.Seq);
            };

        private static TraceEvent FirstCast(TraceView v, string by) => v.Of("cast").FirstOrDefault(e => e.Str("by") == by && e.Round == 1);

        /// <summary>One "AfterStandard" decision-trace row (AIService.UseMoveAfterStandardAction) chose <paramref name="chosen"/>.</summary>
        private static Func<TraceView, ExpectResult> AfterStandardRow(string chosen, string legacy = null)
            => v =>
            {
                List<global::AiDecisionTrace.Decision> rows = TraceRows("AfterStandard");
                return rows.Any(d => d.Chosen == chosen && (legacy == null || d.Legacy == legacy))
                    ? ExpectResult.Pass(rows.Count + " AfterStandard row(s), one " + chosen)
                    : MissingRow("AfterStandard", rows.Count, "chose " + chosen + (legacy != null ? " over " + legacy : ""));
            };

        /// <summary>
        /// AI-077 (batch 14 step 11): the arcane missile adept (Evoker Role, the kiter) casts Magic Missile at an idle
        /// fighter two squares off, then backs away with its move action (a casting Role keeps its distance from a foe
        /// within the keep-away distance, AIProfile.DefaultChooseAfterStandardAction). Before the step its turn ended at
        /// the cast, two squares from the fighter.
        /// </summary>
        private static ScenarioDef AiKiterCastsThenRetreats()
        {
            return Rules("rules/ai-kiter-casts-then-retreats", "An AI kiter casts, then backs away from the close foe with its move action (AI-077, PHB p.138)")
                .Covers("AI-077", "PHB p.138", "batch 14 step 11")
                .MaxRounds(1)
                .Npc("adept", "arcane_missile_adept", 10, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 12, 10, Control.Idle)
                .Tweak("hero", Sturdy)
                .Tweak("adept", Sturdy)
                .Initiative("adept", "hero")
                .Expect("Round 1: it casts, then moves", MovesAfter("adept", "cast", v => FirstCast(v, "adept")))
                .Expect("It ends farther than 2 squares from the fighter", v =>
                {
                    int d = FinalDistance(v, "adept", "hero");
                    return d > 2 ? ExpectResult.Pass("distance " + d) : ExpectResult.Fail("distance " + d);
                })
                .Expect("The decision trace records KeepDistance", AfterStandardRow("KeepDistance", "KeepDistance"))
                .Build();
        }

        /// <summary>
        /// AI-077 (batch 14 step 11): the human cleric (Humanoid Role, the defensive melee routine) casts first (AI-002),
        /// then closes on the idle fighter five squares off with its move action (a melee Role approaches a focus out of
        /// reach). Before the step its turn ended at the cast.
        /// </summary>
        private static ScenarioDef AiMeleeCasterCastsThenCloses()
        {
            return Rules("rules/ai-melee-caster-casts-then-closes", "An AI melee caster casts, then closes on its target with its move action (AI-077, PHB p.138)")
                .Covers("AI-077", "AI-002", "PHB p.138", "batch 14 step 11")
                .MaxRounds(1)
                .Npc("cleric", "human_cleric", 10, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 15, 10, Control.Idle)
                .Tweak("cleric", Sturdy)
                .Tweak("hero", Sturdy)
                .Initiative("cleric", "hero")
                .Expect("Round 1: it casts, then moves", MovesAfter("cleric", "cast", v => FirstCast(v, "cleric")))
                .Expect("It ends closer to the fighter than it started (5 squares)", v =>
                {
                    int d = FinalDistance(v, "cleric", "hero");
                    return d >= 0 && d < 5 ? ExpectResult.Pass("distance " + d) : ExpectResult.Fail("distance " + d);
                })
                .Expect("It makes no attack in round 1 (the cast took the standard action)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("cleric", null, false, 1);
                    return attacks.Count == 0 ? ExpectResult.Pass("no attack") : ExpectResult.Fail(attacks.Count + " attack(s)", attacks[0].Seq);
                })
                .Expect("The decision trace records Approach", AfterStandardRow("Approach", "Approach"))
                .Build();
        }

        /// <summary>A Human neutral wizard: STR 10, DEX 12, CON 12, INT 16; Wizard kit (CastPipelineScenarios' fixture).</summary>
        private static CharacterStats StepWizard(string name, int level, params string[] feats)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Wizard",
                str: 10, dex: 12, con: 12, wis: 10, intelligence: 16, cha: 10,
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
        /// Leaves <paramref name="c"/> only the given slots: each (level, spell, metamagic) is added prepared, every other
        /// slot is marked used.
        /// </summary>
        private static void OnlySlots(CharacterController c, params (int level, string spellId, MetamagicData metamagic)[] slots)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting");
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null)
                    slot.IsUsed = true;
            foreach (var (level, spellId, metamagic) in slots)
            {
                var slot = new SpellSlot(level, false, "Wizard");
                slot.PrepareWithMetamagic(SpellDatabase.GetSpell(spellId).Clone(), metamagic);
                sc.SpellSlots.Add(slot);
            }
            sc.SyncPreparedSpellsFromSlots();
        }

        /// <summary>
        /// D1 (PHB p.98: a quickened spell is a free action, one per round): an AI-run wizard 9 with Quicken Spell, only a
        /// quickened Magic Missile (a 5th-level slot) and a plain one (1st level), and a sturdy dummy nine squares off.
        /// The quickened cast leaves the standard action, so the routine goes on (AIService.KeepsRoutineAfterAbility) and
        /// the plain missile follows in the same turn. Before the step the turn ended at the first cast.
        /// </summary>
        private static ScenarioDef AiQuickenedCastContinuesTurn()
        {
            var quicken = new MetamagicData();
            quicken.Toggle(MetamagicFeatId.QuickenSpell);
            return Rules("rules/ai-quickened-cast-continues-turn", "An AI caster's quickened spell leaves its standard action, and the routine casts again (AI-077, PHB p.98)")
                .Covers("AI-077", "PHB p.98", "batch 14 step 11")
                .MaxRounds(1)
                .Pc("wiz", ActorSource.Stats(() => StepWizard("Quick Wizard", 9, "Quicken Spell")), 3, 10, Control.Ai)
                .Npc("dummy", "target_dummy", 12, 10, Control.Scripted)
                .Tweak("wiz", c => OnlySlots(c, (5, SpellNames.MAGIC_MISSILE, quicken), (1, SpellNames.MAGIC_MISSILE, null)))
                .Tweak("dummy", Sturdy)
                .Initiative("wiz", "dummy")
                .Turn("dummy", 1, Step.Assert("record", ctx =>
                {
                    SpellcastingComponent sc = ctx.Get("wiz").Spellcasting;
                    ctx.Note("b14s11 quickened.used=" + sc.SpellSlots.Any(x => x != null && x.Level == 5 && x.HasMetamagic && x.IsUsed));
                    ctx.Note("b14s11 plain.used=" + sc.SpellSlots.Any(x => x != null && x.Level == 1 && !x.HasMetamagic && x.IsUsed
                        && x.PreparedSpell != null && x.PreparedSpell.SpellId == SpellNames.MAGIC_MISSILE));
                    return true;
                }))
                .Expect("Round 1: two Magic Missiles", Expect.Count("cast", e => e.Str("by") == "wiz" && e.Round == 1 && e.Str("spell") == SpellNames.MAGIC_MISSILE, 2, 2))
                .Expect("The quickened slot and the plain slot are both spent", v =>
                {
                    List<string> notes = v.Of("note").Select(e => e.Str("text") ?? "").Where(t => t.StartsWith("b14s11 ", StringComparison.Ordinal)).ToList();
                    bool q = notes.Contains("b14s11 quickened.used=True");
                    bool p = notes.Contains("b14s11 plain.used=True");
                    return q && p ? ExpectResult.Pass("both spent") : ExpectResult.Fail(string.Join("; ", notes));
                })
                .Build();
        }

        /// <summary>
        /// D1 (a 1-round casting time is a full-round action, PHB p.139, and leaves no move): an AI-run wizard 3 with only
        /// Summon Monster I (two slots) and an idle fighter two squares off. It summons, and does not move afterwards,
        /// though a standard cast would have backed it away from the close foe (rules/ai-kiter-casts-then-retreats).
        /// </summary>
        private static ScenarioDef AiFullRoundCastNoMove()
        {
            return Rules("rules/ai-fullround-cast-no-move", "An AI caster's full-round summon leaves no move action, so it stays (AI-077, PHB p.139)")
                .Covers("AI-077", "PHB p.139", "batch 14 step 11")
                .MaxRounds(1)
                .Pc("wiz", ActorSource.Stats(() => StepWizard("Summoning Wizard", 3)), 10, 10, Control.Ai)
                .Npc("orc", "orc_warrior", 12, 10, Control.Idle)
                .Tweak("wiz", c => { OnlySlots(c, (1, SpellNames.SUMMON_MONSTER_1, null), (1, SpellNames.SUMMON_MONSTER_1, null)); Sturdy(c); })
                .Tweak("orc", Sturdy)
                .Initiative("wiz", "orc")
                .Expect("Round 1: it casts Summon Monster I", v =>
                {
                    TraceEvent c = FirstCast(v, "wiz");
                    return c != null && c.Str("spell") == SpellNames.SUMMON_MONSTER_1
                        ? ExpectResult.Pass("cast #" + c.Seq, c.Seq) : ExpectResult.Fail(c == null ? "no cast" : "cast " + c.Str("spell"));
                })
                .Expect("It does not move in round 1", v =>
                {
                    int moved = v.Moves("wiz", 1).Sum(e => e.Int("sq"));
                    return moved == 0 ? ExpectResult.Pass("stayed") : ExpectResult.Fail("moved " + moved);
                })
                .Expect("The decision trace records Stay with no move left", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("AfterStandard");
                    return rows.Any(d => d.Chosen == "Stay" && d.Reason == "no move")
                        ? ExpectResult.Pass(rows.Count + " AfterStandard row(s), one Stay with no move")
                        : MissingRow("AfterStandard", rows.Count, "Stay with no move");
                })
                .Build();
        }

        /// <summary>
        /// AI-077 (batch 14 step 11): the paladin 3 at a third of its hit points lays on hands on itself for 6
        /// (rules/paladin-lay-on-hands-ai's layout), then closes on the idle fighter six squares off with its move action.
        /// </summary>
        private static ScenarioDef AiLayOnHandsThenMoves()
        {
            return Rules("rules/ai-lay-on-hands-then-moves", "An AI paladin lays on hands, then closes on its foe with its move action (AI-077, PHB p.44, p.138)")
                .Covers("AI-077", "PHB p.44", "PHB p.138", "batch 14 step 11")
                .MaxRounds(1)
                .Npc("pal", "human_paladin_3", 10, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Foe", 4)), 16, 10, Control.Idle)
                .Tweak("pal", c => c.Stats.CurrentHP = c.Stats.TotalMaxHP / 3)
                .Tweak("foe", Sturdy)
                .Initiative("pal", "foe")
                .Expect("Round 1: it heals itself 6, then moves", MovesAfter("pal", "the +6 heal",
                    v => v.Of("hp").FirstOrDefault(e => e.Round == 1 && e.Str("actor") == "pal" && e.Int("new") - e.Int("old") == 6)))
                .Expect("It ends closer to the fighter than it started (6 squares)", v =>
                {
                    int d = FinalDistance(v, "pal", "foe");
                    return d >= 0 && d < 6 ? ExpectResult.Pass("distance " + d) : ExpectResult.Fail("distance " + d);
                })
                .Build();
        }

        /// <summary>
        /// C8 with the traits (batch 14 step 11): the human cleric with Cowardly:1 casts first, then backs away from the
        /// idle fighter two squares off (Cowardly's Retreat .8 beats the Role's approach, AiBlend.AfterStandardAction);
        /// the -plain twin closes in instead.
        /// </summary>
        private static ScenarioDef AiCowardlyCastsThenRetreats(bool plain)
        {
            string id = plain ? "rules/ai-cowardly-casts-then-retreats-plain" : "rules/ai-cowardly-casts-then-retreats";
            ScenarioBuilder b = Rules(id, plain
                    ? "The plain melee caster casts, then closes on the fighter two squares off (batch 14 step 11)"
                    : "The cowardly melee caster casts, then backs away from the fighter two squares off (batch 14 step 11, C8)")
                .Covers("AI-077", "PHB p.138", "batch 14 step 11")
                .MaxRounds(1)
                .Npc("cleric", "human_cleric", 10, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 12, 10, Control.Idle)
                .Tweak("cleric", Sturdy)
                .Tweak("hero", Sturdy)
                .Initiative("cleric", "hero");
            b.Personality("cleric", plain ? "" : "Cowardly:1");
            b.Expect("Round 1: it casts, then moves", MovesAfter("cleric", "cast", v => FirstCast(v, "cleric")));
            if (plain)
            {
                b.Expect("It ends next to the fighter", v =>
                {
                    int d = FinalDistance(v, "cleric", "hero");
                    return d == 1 ? ExpectResult.Pass("adjacent") : ExpectResult.Fail("distance " + d);
                });
                b.Expect("The decision trace records the Role's Approach", AfterStandardRow("Approach", "Approach"));
            }
            else
            {
                b.Expect("It ends farther than 2 squares from the fighter", v =>
                {
                    int d = FinalDistance(v, "cleric", "hero");
                    return d > 2 ? ExpectResult.Pass("distance " + d) : ExpectResult.Fail("distance " + d);
                });
                b.Expect("The decision trace records KeepDistance over the Role's Approach", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("Personality.AfterStandard");
                    return rows.Any(d => d.Chosen == "KeepDistance" && d.Legacy == "Approach")
                        ? ExpectResult.Pass(rows.Count + " Personality.AfterStandard row(s): Approach -> KeepDistance")
                        : MissingRow("Personality.AfterStandard", rows.Count, "KeepDistance over Approach");
                });
            }
            return b.Build();
        }

        /// <summary>
        /// The review of batch 14 step 11 (AI-077, PHB p.159): an AI-run human cleric on the party's side, a goblin 3
        /// squares off (its likely focus, out of reach) and two skeletons 4 squares off flanking the goblin. The forced
        /// dice turn both skeletons; every square next to the goblin is within 10 feet of a skeleton, so the move after the
        /// turning (AIService.UseMoveAfterStandardAction) refuses those squares and the turning holds through round 1
        /// (GameManager.WouldBreakTurningAtForAI). Before the fix an approach toward another foe could end next to a turned
        /// undead and break its turning in the cleric's own turn.
        /// </summary>
        private static ScenarioDef AiClericMoveKeepsTurning()
        {
            return Rules("rules/ai-cleric-move-keeps-turning", "An AI cleric's move after Turn Undead never ends within 10 feet of an undead it turned (AI-077, PHB p.159)")
                .Covers("AI-077", "PHB p.159", "PHB p.138", "batch 14 step 11")
                .MaxRounds(1)
                .Npc("cleric", "human_cleric", 5, 10, Control.Ai, CharacterTeam.Player)
                .Tweak("cleric", Sturdy)
                .Npc("gob", "goblin", 8, 10, Control.Idle)
                .Tweak("gob", Sturdy)
                .Npc("skel1", "skeleton_warrior", 9, 9, Control.Ai)
                .Npc("skel2", "skeleton_warrior", 9, 11, Control.Ai)
                .Initiative("cleric", "gob", "skel1", "skel2")
                .Force(20, 20, "Turning check").Force(6, 1, "Turning damage", 2)
                .Expect("The cleric invokes Turn Undead in round 1", v =>
                    v.Log("invokes Turn Undead").Any(e => e.Round == 1) ? ExpectResult.Pass("invoked") : ExpectResult.Fail("no turning"))
                .Expect("Both skeletons are turned", v =>
                {
                    int n = new[] { "skel1", "skel2" }.Count(k => v.Conds(k, CombatConditionType.Turned).Any(e => e.Bool("added") && e.Round == 1));
                    return n == 2 ? ExpectResult.Pass("2 turned") : ExpectResult.Fail(n + " turned");
                })
                .Expect("No turning ends in round 1 (the cleric's move does not come within 10 feet)", v =>
                {
                    TraceEvent ended = v.Conds(null, CombatConditionType.Turned).FirstOrDefault(e => !e.Bool("added") && e.Round == 1);
                    return ended == null ? ExpectResult.Pass("the turning holds") : ExpectResult.Fail("a turning ended", ended.Seq);
                })
                .Expect("The move after the turning was decided (an AfterStandard row; Approach when the goblin is the focus)", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("AfterStandard");
                    if (rows.Count == 0)
                        return MissingRow("AfterStandard", 0, "a decision after the turning");
                    return rows.Any(d => d.Chosen == "Approach")
                        ? ExpectResult.Pass(rows.Count + " AfterStandard row(s), one Approach")
                        : ExpectResult.Inconclusive("no Approach (the focus was not the goblin): " + string.Join(",", rows.Select(d => d.Chosen)));
                })
                .Build();
        }

        /// <summary>
        /// AI-088 (the review of batch 14 step 11): the arcane missile adept next to an idle fighter (no reach) casts, then
        /// keeps its distance with a 5-foot step out of the fighter's threat (PHB p.144: it has not moved this round, and the
        /// step provokes nothing) instead of walking off through a movement AoO (PHB p.137).
        /// </summary>
        private static ScenarioDef AiThreatenedCasterStepsAway()
        {
            return Rules("rules/ai-threatened-caster-steps-away", "A threatened AI caster 5-foot steps out of the threat after its cast, provoking no movement AoO (AI-088, PHB p.144)")
                .Covers("AI-088", "AI-077", "PHB p.144", "PHB p.137", "batch 14 step 11")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Fighter", 4)), 10, 10, Control.Idle)
                .Tweak("fighter", Sturdy)
                .Npc("adept", "arcane_missile_adept", 11, 10, Control.Ai)
                .Tweak("adept", Sturdy)
                .Initiative("adept", "fighter")
                .Expect("Round 1: it casts", v => FirstCast(v, "adept") != null ? ExpectResult.Pass("cast") : ExpectResult.Fail("no cast"))
                .Expect("The decision trace records KeepDistance", AfterStandardRow("KeepDistance"))
                .Expect("No movement AoO against the adept", Expect.None("aoo", e => e.Str("target") == "adept" && e.Str("trigger") != "spellcast"))
                .Expect("It ends out of the fighter's reach, one 5-foot step away", v =>
                {
                    int d = FinalDistance(v, "adept", "fighter");
                    return d == 2 ? ExpectResult.Pass("distance 2") : ExpectResult.Fail("distance " + d);
                })
                .Build();
        }

        // ── Batch 14 step 12: the healer reaches its patient (AI-047, AI-063; D1, C8, Q8) ──

        /// <summary>
        /// Leaves the cleric <paramref name="c"/> only <paramref name="count"/> prepared Cure Light Wounds (PHB p.215): every
        /// slot is spent, then that many regular 1st-level slots hold the cure again. Throws (the job ends SetupFailed) when
        /// there are not enough such slots.
        /// </summary>
        private static void OnlyCures(CharacterController c, int count = 1)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting");
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null)
                    slot.IsUsed = true;
            int placed = 0;
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (placed >= count)
                    break;
                if (slot == null || slot.Level != 1 || slot.IsDomainSlot)
                    continue;
                slot.PreparedSpell = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS).Clone();
                slot.IsUsed = false;
                placed++;
            }
            if (placed < count)
                throw new InvalidOperationException(c.Stats.CharacterName + " has " + placed + " regular 1st-level slots, needs " + count);
            sc.SyncPreparedSpellsFromSlots();
        }

        /// <summary>One "HealerApproach" decision-trace row (AIService.RunHealerApproach) chose <paramref name="chosen"/>, its reason containing <paramref name="reasonPart"/> when given.</summary>
        private static Func<TraceView, ExpectResult> HealerApproachRow(string chosen, string reasonPart = null)
            => v =>
            {
                List<global::AiDecisionTrace.Decision> rows = TraceRows("HealerApproach");
                return rows.Any(d => d.Chosen == chosen && (reasonPart == null || (d.Reason ?? "").Contains(reasonPart)))
                    ? ExpectResult.Pass(rows.Count + " HealerApproach row(s), one " + chosen)
                    : MissingRow("HealerApproach", rows.Count, "chose " + chosen + (reasonPart != null ? " with " + reasonPart : ""));
            };

        /// <summary>The round-1 Cure Light Wounds casts of <paramref name="by"/> on <paramref name="target"/>.</summary>
        private static Func<TraceEvent, bool> CureOn(string by, string target)
            => e => e.Str("by") == by && e.Str("target") == target && e.Round == 1 && e.Str("spell") == SpellNames.CURE_LIGHT_WOUNDS;

        /// <summary>
        /// The healer layout: an AI-run human cleric (Cleric 5, speed 20 ft., heavy mace) given the Healer Role and only one
        /// Cure Light Wounds, its wounded kobold ally <paramref name="allyAt"/> (an idle 20-hp kobold at
        /// <paramref name="allyHp"/> hp: 50% is a Healing turn, 20% a critical one, HealerAIProfile thresholds 70% and
        /// 25%), and a sturdy idle fighter at <paramref name="heroAt"/> who acts first (an Idle actor still makes its
        /// attacks of opportunity once it has acted). One round.
        /// </summary>
        private static ScenarioBuilder HealerLayout(string id, string title, (int x, int y) clericAt, (int x, int y) allyAt, int allyHp, (int x, int y) heroAt,
            Action<CharacterController> clericTweak = null)
        {
            return Rules(id, title)
                .Covers("AI-047", "AI-063", "PHB p.138", "PHB p.215", "batch 14 step 12")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), heroAt.x, heroAt.y, Control.Idle)
                .Npc("cleric", "human_cleric", clericAt.x, clericAt.y, Control.Ai)
                .Npc("kobold", "kobold_warrior", allyAt.x, allyAt.y, Control.Idle)
                .Profile("cleric", () => DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.HealerAIProfile>())
                .Tweak("cleric", c => { OnlyCures(c); Sturdy(c); clericTweak?.Invoke(c); })
                .Tweak("kobold", c => SetHp(c, 20, allyHp))
                .Tweak("hero", Sturdy)
                .Initiative("hero", "cleric", "kobold");
        }

        /// <summary>
        /// AI-047 (batch 14 step 12): the healer's kobold ally at half its hit points stands 3 squares off, out of the
        /// cure's touch (PHB p.215) but within one move (20 ft.). The cleric moves beside it and cures it in the same turn
        /// (PHB p.138: a move and a standard action). Before the step the cure found no target and the turn fell to the
        /// kiter, which ended it in total defense where it stood (AI-063).
        /// </summary>
        private static ScenarioDef AiHealerMovesToTouchAndCures()
        {
            return HealerLayout("rules/ai-healer-moves-to-touch-and-cures",
                    "An AI healer moves beside its wounded ally and cures it in the same turn (AI-047, PHB p.138)",
                    (10, 10), (13, 10), 10, (10, 17))
                .Expect("Round 1: it moves, then casts Cure Light Wounds on the kobold", v =>
                {
                    TraceEvent cast = v.Of("cast").FirstOrDefault(CureOn("cleric", "kobold"));
                    if (cast == null)
                        return ExpectResult.Fail("no cure on the kobold in round 1");
                    TraceEvent move = v.Moves("cleric", 1).FirstOrDefault(e => e.Seq < cast.Seq);
                    return move != null
                        ? ExpectResult.Pass("move #" + move.Seq + ", then the cure #" + cast.Seq, move.Seq, cast.Seq)
                        : ExpectResult.Fail("the cure #" + cast.Seq + " came without a move before it", cast.Seq);
                })
                .Expect("It ends beside the kobold", v =>
                {
                    int d = FinalDistance(v, "cleric", "kobold");
                    return d == 1 ? ExpectResult.Pass("adjacent") : ExpectResult.Fail("distance " + d);
                })
                .Expect("The kobold is healed", v =>
                    v.Of("hp").Any(e => e.Round == 1 && e.Str("actor") == "kobold" && e.Int("new") > e.Int("old"))
                        ? ExpectResult.Pass("healed") : ExpectResult.Fail("no heal"))
                .Expect("The decision trace records MoveAndCast", HealerApproachRow("MoveAndCast", "touch=move"))
                .Build();
        }

        /// <summary>
        /// AI-047 (batch 14 step 12): the wounded kobold stands 12 squares off, beyond one move (4 squares). The cleric
        /// moves toward it (a partial approach) and casts nothing; before the step it stood in total defense (AI-063).
        /// </summary>
        private static ScenarioDef AiHealerApproachesFarAlly()
        {
            return HealerLayout("rules/ai-healer-approaches-far-ally",
                    "An AI healer moves toward a wounded ally beyond one move (AI-047)",
                    (4, 10), (16, 10), 10, (4, 17))
                .Expect("It casts no cure in round 1", Expect.Count("cast", e => e.Str("by") == "cleric" && e.Round == 1, 0, 0))
                .Expect("It ends closer to the kobold than it started (12 squares)", v =>
                {
                    int d = FinalDistance(v, "cleric", "kobold");
                    return d >= 0 && d < 12 ? ExpectResult.Pass("distance " + d) : ExpectResult.Fail("distance " + d);
                })
                .Expect("The decision trace records PartialApproach", HealerApproachRow("PartialApproach", "partial=True"))
                .Build();
        }

        /// <summary>
        /// Q8 (owner default, batch 14 step 12): the fighter stands beside the cleric, and the kobold, 3 squares off, is at
        /// 4 of 20 hit points (20%, at or below the critical 25%). Every move out of the fighter's reach provokes (PHB
        /// p.137), and for a critical patient a provoking path costs 6 per provoker instead of the Healer's 1000: the cleric
        /// takes the fighter's attack of opportunity, moves beside the kobold and cures it. The plain wound twin
        /// (rules/ai-healer-hurt-ally-no-aoo) stays.
        /// </summary>
        private static ScenarioDef AiHealerCriticalProvokes()
        {
            return HealerLayout("rules/ai-healer-critical-provokes",
                    "An AI healer provokes to reach a critically wounded ally and cures it (Q8, AI-047, PHB p.137)",
                    (10, 10), (13, 10), 4, (9, 10))
                .Covers("Q8")
                .Expect("The fighter makes a movement attack of opportunity on the cleric", Expect.Count("aoo",
                    e => e.Str("by") == "hero" && e.Str("target") == "cleric" && e.Str("trigger") != "spellcast" && e.Round == 1, 1, 9))
                .Expect("The cleric casts Cure Light Wounds on the kobold", Expect.Count("cast", CureOn("cleric", "kobold"), 1, 1))
                .Expect("The decision trace records MoveAndCast for a critical patient at 6 per provoker",
                    HealerApproachRow("MoveAndCast", "critical=True aooPrice=6"))
                .Build();
        }

        /// <summary>
        /// Q8 (batch 14 step 12): the layout of rules/ai-healer-critical-provokes with the kobold at half its hit points.
        /// The Healer avoids provoking (Movement.AvoidAoOs) except for a critical patient, so it does not walk out of the
        /// fighter's reach: no movement attack of opportunity, no cure; it keeps today's turn (the kiter).
        /// </summary>
        private static ScenarioDef AiHealerHurtAllyNoAoO()
        {
            return HealerLayout("rules/ai-healer-hurt-ally-no-aoo",
                    "An AI healer does not provoke to reach an ally that is hurt but not critical (Q8)",
                    (10, 10), (13, 10), 10, (9, 10))
                .Covers("Q8")
                .Expect("No movement attack of opportunity on the cleric", Expect.None("aoo", e => e.Str("target") == "cleric" && e.Str("trigger") != "spellcast"))
                .Expect("It casts no cure on the kobold", Expect.Count("cast", CureOn("cleric", "kobold"), 0, 0))
                .Expect("The decision trace records Kiter at the Healer's price", HealerApproachRow("Kiter", "aooPrice=1000"))
                .Build();
        }

        /// <summary>
        /// Q8 with the traits (batch 14 step 12): the layout of rules/ai-healer-critical-provokes, the cleric given
        /// Cowardly:1 and hurt (20 hit points below its full). Cowardly forbids a provoking path once hurt (AiBlend.AoOCost),
        /// which overrides the critical patient's price: the cleric never provokes, casts no cure and keeps today's turn.
        /// A hurt living healer cures itself first (its own square is in reach), so this cleric is made an undead, which a
        /// cure would harm (PHB p.215; AISpellcastingStrategist.ScoreAllyTarget skips it): an intelligent undead priest.
        /// </summary>
        private static ScenarioDef AiHealerCowardlySkipsAoOApproach()
        {
            ScenarioBuilder b = HealerLayout("rules/ai-healer-cowardly-skips-aoo-approach",
                    "A hurt Cowardly AI healer never provokes, even for a critically wounded ally (Q8, C6)",
                    (10, 10), (13, 10), 4, (9, 10), c => { c.Stats.CreatureType = "Undead"; c.Stats.CurrentHP -= 20; })
                .Covers("Q8", "C6");
            b.Personality("cleric", "Cowardly:1");
            return b
                .Expect("No movement attack of opportunity on the cleric", Expect.None("aoo", e => e.Str("target") == "cleric" && e.Str("trigger") != "spellcast"))
                .Expect("It casts no cure on the kobold", Expect.Count("cast", CureOn("cleric", "kobold"), 0, 0))
                .Expect("The decision trace records Kiter at the Forbid price", HealerApproachRow("Kiter", "critical=True aooPrice=1000"))
                .Build();
        }

        /// <summary>
        /// AI-047 (batch 14 step 12): the goblin adept (Healer Role, Cowardly:0.6,Healer:0.4) has no spells before batch 15
        /// (CHR-024, ENC-021), so its wounded goblin ally 3 squares off makes no Healing turn (AI-063): no approach is
        /// decided, it casts nothing and does not close on the ally.
        /// </summary>
        private static ScenarioDef AiHealerNoSpellNoApproach()
        {
            return Rules("rules/ai-healer-no-spell-no-approach", "A Healer-Role goblin adept without spells never approaches its wounded ally (AI-047, AI-063)")
                .Covers("AI-047", "AI-063", "CHR-024", "batch 14 step 12")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 16, Control.Idle)
                .Npc("adept", "goblin_adept", 10, 10, Control.Ai)
                .Npc("gob", "goblin_warrior", 10, 7, Control.Idle)
                .Tweak("adept", Sturdy)
                .Tweak("gob", c => SetHp(c, 20, 8))
                .Tweak("hero", Sturdy)
                .Initiative("hero", "adept", "gob")
                .Expect("Its priority is PhysicalAttack, not the legacy Healing (AI-063)", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("HealerPriority");
                    if (rows.Count == 0)
                        return MissingRow("HealerPriority", 0, "chose PhysicalAttack");
                    global::AiDecisionTrace.Decision d = rows[0];
                    return d.Chosen == "PhysicalAttack" && d.Legacy == "Healing"
                        ? ExpectResult.Pass("PhysicalAttack (legacy Healing) [" + d.Reason + "]")
                        : ExpectResult.Fail("chose " + d.Chosen + " (legacy " + d.Legacy + ") [" + d.Reason + "]");
                })
                .Expect("It decides no healer approach", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("HealerApproach");
                    return rows.Count == 0 ? ExpectResult.Pass("no HealerApproach row") : ExpectResult.Fail(rows.Count + " HealerApproach row(s): " + rows[0].Chosen);
                })
                .Expect("It casts nothing", Expect.Count("cast", e => e.Str("by") == "adept", 0, 0))
                .Expect("It does not close on the wounded goblin (3 squares)", v =>
                {
                    int d = FinalDistance(v, "adept", "gob");
                    return d >= 3 ? ExpectResult.Pass("distance " + d) : ExpectResult.Fail("distance " + d);
                })
                .Build();
        }

        // ── Batch 14 step 13: aid another, total defense and posture (AI-063, batch 13's C5; D3-D5, C8) ──

        /// <summary>Puts <paramref name="weapon"/> in <paramref name="c"/>'s right hand and empties the left (with <paramref name="ammo"/> pieces of ammunition when given).</summary>
        private static void Hold(CharacterController c, ItemData weapon, DND35e.Identifiers.ItemID? ammo = null, int ammoCount = 20)
        {
            global::Inventory inv = c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
            if (inv == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no inventory");
            inv.RightHandSlot = null;
            inv.LeftHandSlot = null;
            inv.DirectEquip(weapon, EquipSlot.RightHand);
            if (ammo.HasValue)
            {
                ItemData pieces = ItemDatabase.CloneItem(ammo.Value);
                pieces.Quantity = ammoCount;
                inv.AddItem(pieces);
            }
            inv.RecalculateStats();
        }

        private static ItemData Longspear()
        {
#pragma warning disable CS0618 // the longspear has no ItemID value; the string id is the only key
            return ItemDatabase.CloneItem(DND35e.Identifiers.ItemIDs.LONGSPEAR);
#pragma warning restore CS0618
        }

        /// <summary>An archer's longbow and 20 arrows in <paramref name="c"/>'s hands (a foe that attacks at range, PHB p.311).</summary>
        private static void Longbow(CharacterController c)
            => Hold(c, ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponLongbow), DND35e.Identifiers.ItemID.AmmoArrow, 20);

        /// <summary>One "MeleeTurnOption" decision-trace row (AIService.ChooseMeleeTurnOptionForAI) chose <paramref name="chosen"/>.</summary>
        private static Func<TraceView, ExpectResult> MeleeTurnRow(string chosen)
            => v =>
            {
                List<global::AiDecisionTrace.Decision> rows = TraceRows("MeleeTurnOption");
                global::AiDecisionTrace.Decision hit = rows.FirstOrDefault(d => d.Chosen == chosen);
                return hit != null
                    ? ExpectResult.Pass(rows.Count + " MeleeTurnOption row(s), one " + chosen + " [" + hit.Reason + "]")
                    : rows.Count > 0
                        ? ExpectResult.Fail("chose " + rows[0].Chosen + " [" + rows[0].Reason + "]")
                        : MissingRow("MeleeTurnOption", 0, "chose " + chosen);
            };

        /// <summary>One "ProneStart" decision-trace row (AIService.ChooseProneStartForAI) chose <paramref name="chosen"/>.</summary>
        private static Func<TraceView, ExpectResult> ProneStartRow(string chosen, string reasonPart = null)
            => v =>
            {
                List<global::AiDecisionTrace.Decision> rows = TraceRows("ProneStart");
                global::AiDecisionTrace.Decision hit = rows.FirstOrDefault(d => d.Chosen == chosen && (reasonPart == null || (d.Reason ?? "").Contains(reasonPart)));
                return hit != null
                    ? ExpectResult.Pass("ProneStart " + chosen + " [" + hit.Reason + "]")
                    : rows.Count > 0
                        ? ExpectResult.Fail("chose " + rows[0].Chosen + " [" + rows[0].Reason + "]")
                        : MissingRow("ProneStart", 0, "chose " + chosen);
            };

        /// <summary>
        /// The melee layout of the aid scenarios: an idle fighter 3 at (10,10), an AI-run or idle goblin warrior with only a
        /// morningstar beside it at (11,10), and an ogre (Large, 10-foot reach) beside it on the other side at (8,10); one
        /// round, everyone sturdy. <paramref name="heroArmor"/> is natural armor added to the fighter.
        /// </summary>
        private static ScenarioBuilder AidLayout(string id, string title, bool goblinActs, int heroArmor)
        {
            return Rules(id, title)
                .Covers("AI-063", "PHB p.154", "PHB p.142", "batch 14 step 13")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Idle)
                .Npc("gob", "goblin_warrior", 11, 10, goblinActs ? Control.Ai : Control.Idle)
                .Npc("ogre", "ogre", 8, 10, goblinActs ? Control.Idle : Control.Ai)
                .Tweak("hero", c => { Sturdy(c); c.Stats.NaturalArmorBonus += heroArmor; })
                .Tweak("gob", c => { Sturdy(c); MorningstarOnly(c); })
                .Tweak("ogre", Sturdy)
                .Initiative(goblinActs ? "gob" : "ogre", "hero", goblinActs ? "ogre" : "gob");
        }

        /// <summary>
        /// D3 (PHB p.154; batch 14 step 13, C8): the goblin hits the fighter (given +6 natural armor, AC 23) only on a natural 20,
        /// so its attack is worth little, while its aid roll against AC 10 lands two times in three and +2 lifts the ogre's
        /// big hits by a tenth: aid offense beats the attack by the margin max(0.5, 0.15 S), and beats total defense (the
        /// fighter threatens only the goblin, but the goblin's hit points are worth what little it deals with them, the
        /// exchange rate of TurnOptionValue.TotalDefense). The goblin aids the ogre and does not attack.
        /// </summary>
        private static ScenarioDef AiWeakAttackerAidsOffense()
        {
            return AidLayout("rules/ai-weak-attacker-aids-offense", "A weak AI attacker aids its strong ally's attack instead of swinging (PHB p.154, D3)",
                    goblinActs: true, heroArmor: 6)
                .Expect("The decision trace records AidOffense", MeleeTurnRow("AidOffense"))
                .Expect("The goblin aids the ogre's attack on the fighter", v =>
                    v.Log("'s offense vs Hero").Count == 1 ? ExpectResult.Pass("aided") : ExpectResult.Fail(v.Log("aids").Count + " aid line(s)"))
                .Expect("The goblin makes no attack in round 1", v =>
                {
                    List<TraceEvent> attacks = FirstRoundAttacks(v, "gob");
                    return attacks.Count == 0 ? ExpectResult.Pass("no attack") : ExpectResult.Fail(attacks.Count + " attack(s)", attacks[0].Seq);
                })
                .Build();
        }

        /// <summary>
        /// D3 (batch 14 step 13): the same melee without the fighter's extra armor, the ogre acting: its own full attack is
        /// worth far more than +2 on the goblin's next swing, so it attacks, as before the step.
        /// </summary>
        private static ScenarioDef AiStrongAttackerAttacks()
        {
            return AidLayout("rules/ai-strong-attacker-attacks", "A strong AI attacker attacks rather than aid a weak ally (PHB p.154, D3)",
                    goblinActs: false, heroArmor: 0)
                .Expect("The decision trace records Attack", MeleeTurnRow("Attack"))
                .Expect("The ogre attacks the fighter in round 1", v =>
                    FirstRoundAttacks(v, "ogre").Count >= 1 ? ExpectResult.Pass("attacked") : ExpectResult.Fail("no attack"))
                .Expect("Nobody aids another", v => v.Log("'s offense vs").Count + v.Log("'s defense vs").Count == 0
                    ? ExpectResult.Pass("no aid") : ExpectResult.Fail("an aid line"))
                .Build();
        }

        /// <summary>
        /// D3 (PHB p.154; batch 14 step 13): a goblin with a longspear (10-foot reach, PHB p.113) stands two squares from the
        /// fighter (+12 natural armor), behind its badly hurt ally (2 of 6 hit points) that fights the fighter. The fighter
        /// threatens only the hurt goblin, so its next attack is the ally's, and one average hit drops it (the drop weight):
        /// aid defense (+2 AC against the fighter's next attack) beats the spearman's natural-20 attack and its total
        /// defense (worth nothing: nobody threatens it while the fighter is engaged). It aids its ally's defense.
        /// </summary>
        private static ScenarioDef AiAidsDefenseBadlyHurtAlly()
        {
            return Rules("rules/ai-aids-defense-badly-hurt-ally", "An AI creature aids the defense of a badly hurt ally the foe is attacking (PHB p.154, D3)")
                .Covers("AI-063", "PHB p.154", "PHB p.113", "batch 14 step 13")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Idle)
                .Npc("gob", "goblin_warrior", 11, 10, Control.Idle)
                .Npc("spear", "goblin_warrior", 12, 10, Control.Ai)
                .Tweak("hero", c => { Sturdy(c); c.Stats.NaturalArmorBonus += 12; })
                .Tweak("gob", c => { MorningstarOnly(c); SetHp(c, 6, 2); })
                .Tweak("spear", c => { Sturdy(c); Hold(c, Longspear()); })
                .Initiative("spear", "hero", "gob")
                .Expect("The decision trace records AidDefense", MeleeTurnRow("AidDefense"))
                .Expect("The spearman aids the hurt goblin's defense against the fighter", v =>
                    v.Log("'s defense vs Hero").Count == 1 ? ExpectResult.Pass("aided") : ExpectResult.Fail(v.Log("aids").Count + " aid line(s)"))
                .Expect("The spearman makes no attack in round 1", v =>
                {
                    List<TraceEvent> attacks = FirstRoundAttacks(v, "spear");
                    return attacks.Count == 0 ? ExpectResult.Pass("no attack") : ExpectResult.Fail(attacks.Count + " attack(s)", attacks[0].Seq);
                })
                .Build();
        }

        /// <summary>
        /// AI-063 (batch 14 step 13): a goblin adept with only a morningstar on a test Role whose out-of-reach list is total
        /// defense alone. With the idle fighter 17 squares off (<paramref name="heroX"/> 19) nobody can reach it next round,
        /// even by a charge (twice its speed, PHB p.154), so it takes no total defense (before the step it stood in total
        /// defense against nobody); in the "-reachable" twin the fighter stands 5 squares off, within a move and its reach,
        /// and in the "-charge" twin 9 squares off, beyond one move but within a charge (batch 14 step 13 review): it takes
        /// total defense in both (PHB p.142). In the "-charge-prone" twin (<paramref name="heroProne"/>, AI-089) the fighter 9
        /// squares off starts prone: standing up is a move action and a charge a full-round action (PHB p.141, p.154), so it
        /// cannot charge next round (TargetNumbers.ChargeConditionsMet, read by TurnOptionValue.FoeReach) and the adept takes
        /// no total defense.
        /// </summary>
        private static ScenarioDef AiTotalDefenseOnlyWhenReachable(int heroX, bool heroProne = false)
        {
            bool reachable = heroX != 19 && !heroProne;
            string id = "rules/ai-total-defense-only-when-reachable" + (heroX == 7 ? "-reachable" : heroX == 11 ? "-charge" : "") + (heroProne ? "-prone" : "");
            ScenarioBuilder b = Rules(id, heroProne
                    ? "Out of reach, an AI creature whose only foe in charge range is prone takes no total defense (PHB p.142, p.154, AI-089)"
                    : heroX == 11
                    ? "Out of reach, an AI creature a foe can charge takes total defense (PHB p.142, p.154, AI-063)"
                    : reachable
                    ? "Out of reach, an AI creature a foe can reach takes total defense (PHB p.142, AI-063)"
                    : "Out of reach, an AI creature no foe can reach takes no total defense (PHB p.142, AI-063)")
                .Covers("AI-063", "PHB p.142", "batch 14 step 13")
                .MaxRounds(1)
                .Npc("npc", "goblin_adept", 2, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), heroX, 10, Control.Idle)
                .Profile("npc", () => OutOfReachRole(OutOfReachAlternative.TotalDefense))
                .Tweak("npc", c => { Sturdy(c); MorningstarOnly(c); })
                .Tweak("hero", Sturdy)
                .Initiative("npc", "hero");
            if (heroProne)
                b.StartCondition("hero", CombatConditionType.Prone, -1).Covers("AI-089", "PHB p.141");
            b.Personality("npc", "");
            ExpectNoCast(b, "npc");
            return b
                .Expect(reachable ? "It takes total defense" : "It takes no total defense", v =>
                {
                    int defense = v.Log("Goblin Adept takes total defense").Count;
                    return defense == (reachable ? 1 : 0) ? ExpectResult.Pass(defense + " total defense") : ExpectResult.Fail(defense + " total defense");
                })
                .Expect("It does not move", v =>
                {
                    int moved = v.Moves("npc", 1).Sum(e => e.Int("sq"));
                    return moved == 0 ? ExpectResult.Pass("no move") : ExpectResult.Fail("moved " + moved);
                })
                .Build();
        }

        /// <summary>
        /// D5 (PHB p.311, p.141; batch 14 step 13, C8): the arcane missile adept (Evoker Role) starts prone, eight squares
        /// from an idle fighter with a longbow (<paramref name="heroX"/> 18), who threatens it neither now nor after a move.
        /// Its cast works prone and prone gives +4 AC against the arrows, so it stays down and casts (before the step it stood
        /// up first). In the "-archer-close" twin the archer stands 2 squares off, within the kiter's keep-away distance: the
        /// move after the cast wishes to keep its distance, but a creature that chose to stay prone is not stood up again
        /// (AfterStandardActionContext.StayingProne, batch 14 step 13 review).
        /// </summary>
        private static ScenarioDef AiProneCasterStaysProne(int heroX)
        {
            string id = "rules/ai-prone-caster-stays-prone" + (heroX == 18 ? "" : "-archer-close");
            return Rules(id, heroX == 18
                    ? "A prone AI caster facing an archer stays prone and casts (PHB p.311, D5)"
                    : "A prone AI caster with an archer close by stays prone after its cast (PHB p.311, D5)")
                .Covers("PHB p.311", "PHB p.141", "batch 14 step 13")
                .MaxRounds(1)
                .Npc("adept", "arcane_missile_adept", 10, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), heroX, 10, Control.Idle)
                .StartCondition("adept", CombatConditionType.Prone, -1)
                .Tweak("adept", Sturdy)
                .Tweak("hero", c => { Sturdy(c); Longbow(c); })
                .Initiative("adept", "hero")
                .Expect("The decision trace records StayProne", ProneStartRow("StayProne"))
                .Expect("It casts in round 1", v => FirstCast(v, "adept") != null ? ExpectResult.Pass("cast") : ExpectResult.Fail("no cast"))
                .Expect("It does not stand up", Expect.None("cond", e => e.Str("actor") == "adept"
                    && e.Get("type") is CombatConditionType t && t == CombatConditionType.Prone && !e.Bool("added")))
                .Build();
        }

        /// <summary>
        /// D5 (PHB p.141, Table 8-2; batch 14 step 13): a prone goblin warrior (a melee Role) facing the same archer six
        /// squares off stands up as before: its attack does not work as well prone (-4, PHB p.311) and it must move to reach
        /// the fighter.
        /// </summary>
        private static ScenarioDef AiProneMeleeStands()
        {
            return Rules("rules/ai-prone-melee-stands", "A prone AI melee creature stands up as before (PHB p.141, D5)")
                .Covers("PHB p.141", "PHB p.311", "batch 14 step 13")
                .MaxRounds(1)
                .Npc("gob", "goblin_warrior", 10, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 16, 10, Control.Idle)
                .StartCondition("gob", CombatConditionType.Prone, -1)
                .Tweak("gob", c => { Sturdy(c); MorningstarOnly(c); })
                .Tweak("hero", c => { Sturdy(c); Longbow(c); })
                .Initiative("gob", "hero")
                .Expect("The decision trace records StandUp (its planned action does not work prone)", ProneStartRow("StandUp", "worksProne=False"))
                .Expect("It stands up in round 1", v =>
                    v.Conds("gob", CombatConditionType.Prone).Any(e => !e.Bool("added") && e.Round == 1)
                        ? ExpectResult.Pass("stood up") : ExpectResult.Fail("still prone"))
                .Build();
        }

        /// <summary>
        /// D5 (batch 14 step 13 review; PHB p.141, p.215): the Healer cleric of the healer layout starts prone, unthreatened,
        /// an idle archer (the fighter with a longbow) 8 squares off and its wounded kobold ally 2 squares off. Its Healing
        /// turn's cure is a touch that needs a step (PHB p.175), which a prone creature cannot take, so its plan does not
        /// work prone (AIService.PlannedActionWorksProne): it stands up, as before the step, and cures the kobold.
        /// </summary>
        private static ScenarioDef AiProneHealerStandsToCure()
        {
            return HealerLayout("rules/ai-prone-healer-stands-to-cure",
                    "A prone AI healer with a wounded ally close by stands up and cures it (PHB p.141, D5)",
                    (10, 10), (12, 10), 10, (10, 18))
                .Covers("PHB p.311", "batch 14 step 13")
                .StartCondition("cleric", CombatConditionType.Prone, -1)
                .Tweak("hero", c => { Sturdy(c); Longbow(c); })
                .Expect("The decision trace records StandUp (its plan does not work prone)", ProneStartRow("StandUp", "worksProne=False"))
                .Expect("It stands up in round 1", v =>
                    v.Conds("cleric", CombatConditionType.Prone).Any(e => !e.Bool("added") && e.Round == 1)
                        ? ExpectResult.Pass("stood up") : ExpectResult.Fail("still prone"))
                .Expect("It casts Cure Light Wounds on the kobold", Expect.Count("cast", CureOn("cleric", "kobold"), 1, 1))
                .Build();
        }

        private static bool HasCond(JsonObj snap, string cond) => snap != null && snap.Get("conds") is List<string> l && l.Contains(cond);

        // ── Batch 14 step 14: domain powers (batch 13's C5 leftover; D7, C14) ──

        /// <summary>One "DomainPower" decision-trace row (AIService.ChooseDomainPowerForAI) chose <paramref name="chosen"/>, its reason holding <paramref name="reasonPart"/> when given.</summary>
        private static Func<TraceView, ExpectResult> DomainPowerRow(string chosen, string reasonPart = null)
            => v =>
            {
                List<global::AiDecisionTrace.Decision> rows = TraceRows("DomainPower");
                global::AiDecisionTrace.Decision hit = rows.FirstOrDefault(d => d.Chosen == chosen && (reasonPart == null || (d.Reason ?? "").Contains(reasonPart)));
                return hit != null
                    ? ExpectResult.Pass(rows.Count + " DomainPower row(s), one " + chosen + " [" + hit.Reason + "]")
                    : rows.Count > 0
                        ? ExpectResult.Fail("chose " + string.Join(", ", rows.Select(r => r.Chosen + " [" + r.Reason + "]")))
                        : MissingRow("DomainPower", 0, "chose " + chosen);
            };

        /// <summary>The "b14s14 " notes of the trace.</summary>
        private static List<string> DomainNotes(TraceView v)
            => v.Of("note").Select(e => e.Str("text") ?? "").Where(t => t.StartsWith("b14s14 ", StringComparison.Ordinal)).ToList();

        /// <summary>
        /// The domain-power layout: the DMG p.114 sample cleric (human_cleric, cleric 5, Humanoid Role on the defensive melee
        /// routine, heavy mace) with <paramref name="domains"/> in place of Healing and Protection and every spell slot spent,
        /// so it fights in melee instead of casting first; a fighter 3 that only idles (or, Scripted, only notes) at (<paramref name="heroX"/>, 10). The cleric acts
        /// first. A creature given domains by Tweak (C14: no shipped NPC has an activated domain power).
        /// </summary>
        private static ScenarioBuilder DomainLayout(string id, string title, int heroX, int rounds, Control heroControl, params string[] domains)
        {
            return Rules(id, title)
                .Covers("batch 13 C5", "D7", "C14", "batch 14 step 14", "PC_NPC_PARITY")
                .MaxRounds(rounds)
                .Npc("cleric", "human_cleric", 10, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), heroX, 10, heroControl)
                .Tweak("cleric", c =>
                {
                    Sturdy(c);
                    OnlySlots(c);
                    c.Stats.ChosenDomains = new List<string>(domains);
                })
                .Initiative("cleric", "hero");
        }

        /// <summary>Which twin of the feat-of-strength scenario: the fighter beside the cleric, 7 squares off (a charge), or out of its reach.</summary>
        private enum StrengthTwin { Adjacent, Charge, Far }

        /// <summary>
        /// D7 (PHB p.188; batch 14 step 14, C14): a Strength-domain cleric 5 beside the idle fighter will make melee attacks
        /// this turn, so it performs its feat of strength where its melee attack is certain, before the attack (a free action:
        /// +5 enhancement to Strength, 12 to 17, for 1 round; the turn start asks only a grappling cleric, batch 14 step 14
        /// review) and then makes its attack with the bonus. The fighter's turn notes the day's use and the cleric's
        /// Strength modifier (+3). The "-charge" twin puts the fighter 8 squares off: the cleric threatens nobody at its turn
        /// start, then charges (PHB p.154), and the feat of strength rides on the charge's attack (the ChargeAttack site). The
        /// "-far" twin slows the cleric to 5 ft: it can reach nobody this turn, makes no melee attack and keeps the power.
        /// </summary>
        private static ScenarioDef AiClericStrengthBeforeFullAttack(StrengthTwin twin)
        {
            bool adjacent = twin == StrengthTwin.Adjacent;
            string id = "rules/ai-cleric-strength-before-full-attack" + (twin == StrengthTwin.Charge ? "-charge" : twin == StrengthTwin.Far ? "-far" : "");
            string title = twin == StrengthTwin.Far
                ? "An AI Strength cleric that makes no melee attack this turn keeps its feat of strength (PHB p.188, D7)"
                : twin == StrengthTwin.Charge
                    ? "An AI Strength cleric performs its feat of strength before its charge's attack (PHB p.188, p.154, D7)"
                    : "An AI Strength cleric performs its feat of strength before it attacks in melee (PHB p.188, D7)";
            ScenarioBuilder b = DomainLayout(id, title, adjacent ? 11 : 18, 1, Control.Scripted, "Strength", "Good")
                .Tweak("hero", Sturdy)
                .Turn("hero", 1, Step.Assert("the cleric's feat of strength at the fighter's turn", ctx =>
                {
                    CharacterStats st = ctx.Get("cleric").Stats;
                    ctx.Note("b14s14 str.uses=" + st.StrengthDomainUsesToday);
                    ctx.Note("b14s14 str.mod=" + st.STRMod);
                    return true;
                }), Step.Pass());
            if (twin == StrengthTwin.Far)
                return b
                    .Tweak("cleric", c =>
                    {
                        Sturdy(c);
                        OnlySlots(c);
                        c.Stats.ChosenDomains = new List<string> { "Strength", "Good" };
                        c.Stats.BaseSpeed = 1;
                    })
                    .Expect("No feat of strength in round 1", v => v.Log("activates Feat of Strength").Count == 0
                        ? ExpectResult.Pass("kept") : ExpectResult.Fail("used"))
                    .Expect("The day's use is kept and Strength is 12 (+1)", v =>
                    {
                        List<string> notes = DomainNotes(v);
                        return notes.Contains("b14s14 str.uses=0") && notes.Contains("b14s14 str.mod=1")
                            ? ExpectResult.Pass(string.Join("; ", notes)) : ExpectResult.Fail(string.Join("; ", notes));
                    })
                    .Expect("The cleric makes no attack in round 1", v =>
                    {
                        List<TraceEvent> attacks = FirstRoundAttacks(v, "cleric");
                        return attacks.Count == 0 ? ExpectResult.Pass("no attack") : ExpectResult.Fail(attacks.Count + " attack(s)", attacks[0].Seq);
                    })
                    .Build();
            if (!adjacent)
                b = b.Expect("The cleric charges the fighter", v => v.Log("charges Hero").Count == 1 ? ExpectResult.Pass("charged") : ExpectResult.Fail("no charge"))
                    .Expect("The decision trace records Strength at the charge's attack", DomainPowerRow("Strength", "site=ChargeAttack"));
            else
                b = b.Expect("The decision trace records Strength where the melee attack is certain (the melee attack or the first swing, not the turn start)", v =>
                {
                    List<global::AiDecisionTrace.Decision> rows = TraceRows("DomainPower").Where(d => d.Chosen == "Strength").ToList();
                    if (rows.Count != 1)
                        return ExpectResult.Fail(rows.Count + " Strength row(s)");
                    string reason = rows[0].Reason ?? "";
                    return reason.Contains("site=MeleeAttack") || reason.Contains("site=Smite")
                        ? ExpectResult.Pass(reason) : ExpectResult.Fail(reason);
                });
            return b
                .Expect("Round 1: the feat of strength comes before the cleric's first attack", v =>
                {
                    List<TraceEvent> feat = v.Log("activates Feat of Strength");
                    List<TraceEvent> attacks = FirstRoundAttacks(v, "cleric");
                    if (feat.Count != 1)
                        return ExpectResult.Fail(feat.Count + " feat of strength line(s)");
                    if (attacks.Count == 0)
                        return ExpectResult.Fail("no attack");
                    return v.Before(feat[0].Seq, attacks[0].Seq) ? ExpectResult.Pass("feat then attack") : ExpectResult.Fail("attack first", attacks[0].Seq);
                })
                .Expect("One use spent; Strength 17 (+3) through the fighter's turn", v =>
                {
                    List<string> notes = DomainNotes(v);
                    return notes.Contains("b14s14 str.uses=1") && notes.Contains("b14s14 str.mod=3")
                        ? ExpectResult.Pass(string.Join("; ", notes)) : ExpectResult.Fail(string.Join("; ", notes));
                })
                .Build();
        }

        /// <summary>
        /// D7 (PHB p.186; batch 14 step 14, C14): a Death-domain cleric 5 beside the idle fighter at 10 hit points. Its touch
        /// (+4 against touch AC 11, 0.7) slays when 5d6 reach 10 (0.98), worth about 6.9 hit points against its mace's 2 or
        /// so, so the touch replaces the attack: one Death Touch line, no weapon attack, and on a hit the fighter is slain (or
        /// the 5d6 fall short), on a miss nothing happens. The "-healthy-foe-attacks" twin makes the fighter sturdy (over 300
        /// hit points): the touch is worth next to nothing and the cleric attacks with its mace instead.
        /// </summary>
        private static ScenarioDef AiClericDeathTouch(bool lowHp)
        {
            string id = lowHp ? "rules/ai-cleric-death-touch-low-hp-foe" : "rules/ai-cleric-death-touch-healthy-foe-attacks";
            ScenarioBuilder b = DomainLayout(id, lowHp
                    ? "An AI Death cleric touches a foe its death touch can slay instead of attacking (PHB p.186, D7)"
                    : "An AI Death cleric attacks a healthy foe instead of wasting its death touch (PHB p.186, D7)",
                    11, 1, Control.Idle, "Death", "Evil");
            b = lowHp ? b.Hp("hero", 10) : b.Tweak("hero", Sturdy);
            if (!lowHp)
                return b
                    .Expect("The decision trace records None with the touch open", DomainPowerRow("None", "death=True"))
                    .Expect("No death touch", v => v.Log("with Death Touch").Count == 0 ? ExpectResult.Pass("none") : ExpectResult.Fail("touched"))
                    .Expect("The cleric attacks the fighter in round 1", v =>
                        FirstRoundAttacks(v, "cleric").Count >= 1 ? ExpectResult.Pass("attacked") : ExpectResult.Fail("no attack"))
                    .Build();
            return b
                .Expect("The decision trace records Death at the melee attack", DomainPowerRow("Death", "site=MeleeAttack"))
                .Expect("Round 1: one death touch on the fighter", v =>
                    v.Log("reaches out with Death Touch against Hero").Count(e => e.Round == 1) == 1
                        ? ExpectResult.Pass("touched") : ExpectResult.Fail(v.Log("Death Touch").Count + " death touch line(s)"))
                .Expect("The cleric makes no weapon attack in round 1 (the touch took the standard action)", v =>
                {
                    List<TraceEvent> attacks = FirstRoundAttacks(v, "cleric");
                    return attacks.Count == 0 ? ExpectResult.Pass("no attack") : ExpectResult.Fail(attacks.Count + " attack(s)", attacks[0].Seq);
                })
                .Expect("A hit slays the fighter unless the dice fall short; a miss does nothing", v =>
                {
                    bool missed = v.Log("Melee Touch: .* MISS").Count == 1;
                    bool slain = v.Log("Hero is slain!").Count == 1;
                    bool shortFall = v.Log("Death Touch fails").Count == 1;
                    return missed || slain || shortFall
                        ? ExpectResult.Pass(missed ? "missed" : slain ? "slain" : "the dice fell short")
                        : ExpectResult.Fail("no touch outcome line");
                })
                .Build();
        }

        /// <summary>Sanctuary on <paramref name="c"/> (DC <paramref name="dc"/>) with the effect and fields ApplySpellBuff sets (as AttackLegalityScenarios' fixture).</summary>
        private static bool SanctuaryOn(CharacterController c, int dc)
        {
            StatusEffectManager sem = c.StatusEffectManager;
            if (sem == null)
            {
                sem = c.gameObject.AddComponent<StatusEffectManager>();
                sem.Init(c.Stats);
            }
            ActiveSpellEffect effect = sem.AddEffect(SpellDatabase.GetSpell(SpellNames.SANCTUARY).Clone(), c.Stats.CharacterName, 5);
            if (effect == null)
                return false;
            c.Stats.SanctuaryActive = true;
            c.Stats.SanctuaryDC = dc;
            c.Stats.SanctuaryCasterLevel = 5;
            c.Stats.ActiveSanctuaryEffect = effect;
            return true;
        }

        /// <summary>Makes <paramref name="c"/> invisible with the tracked effect a cast would add (as PerceptionScenarios' fixture).</summary>
        private static bool InvisibleOn(CharacterController c)
        {
            ActiveSpellEffect effect = c.StatusEffectManager != null
                ? c.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.INVISIBILITY).Clone(), c.Stats.CharacterName, 10)
                : null;
            if (effect != null)
                c.ApplyInvisibilityEffect(effect.RemainingRounds, c, isMoving: false);
            return c.HasActiveInvisibilityEffect;
        }

        /// <summary>
        /// The death touch is a melee touch attack (PHB p.186) and runs the stages every attack runs (batch 14 step 14
        /// review): the fighter (10 hit points) acts first and, in its Scripted turn, sets the fixture. The
        /// "-ends-invisibility" twin makes the cleric invisible and puts Sanctuary on it: its touch ends both (PHB p.245,
        /// p.274), logged before the touch's roll. The "-sanctuary-save" twin puts Sanctuary (DC 15) on the fighter with every
        /// Sanctuary save d20 forced to 2: the cleric's one Will save fails, so the touch is lost with its standard action, no
        /// touch is rolled, no weapon attack follows, and the day's use is kept (D6's delegated reading); in round 2 the cleric
        /// knows it cannot attack the fighter.
        /// </summary>
        private static ScenarioDef AiClericDeathTouchAttackRules(bool sanctuaryOnFoe)
        {
            string id = "rules/ai-cleric-death-touch-" + (sanctuaryOnFoe ? "sanctuary-save" : "ends-invisibility");
            ScenarioBuilder b = DomainLayout(id, sanctuaryOnFoe
                    ? "An AI Death cleric's touch on a sanctuaried foe rolls the Will save; a failure loses the touch, not the use (PHB p.186, p.274, D6)"
                    : "An AI Death cleric's touch ends its invisibility and its Sanctuary, as any attack does (PHB p.186, p.245, p.274)",
                    11, sanctuaryOnFoe ? 2 : 1, Control.Scripted, "Death", "Evil")
                .Hp("hero", 10)
                .Initiative("hero", "cleric");
            if (!sanctuaryOnFoe)
                return b
                    .Turn("hero", 1, Step.Assert("the cleric invisible and under Sanctuary", ctx =>
                    {
                        CharacterController cleric = ctx.Get("cleric");
                        ctx.Note("b14s14 fixture=" + (InvisibleOn(cleric) && SanctuaryOn(cleric, 15)));
                        return true;
                    }), Step.Pass())
                    .Expect("The fixture is set", v => DomainNotes(v).Contains("b14s14 fixture=True")
                        ? ExpectResult.Pass("invisible and warded") : ExpectResult.Fail(string.Join("; ", DomainNotes(v))))
                    .Expect("The decision trace records Death at the melee attack", DomainPowerRow("Death", "site=MeleeAttack"))
                    .Expect("Round 1: the touch ends the cleric's invisibility and Sanctuary before its roll", v =>
                    {
                        List<TraceEvent> touch = v.Log("Melee Touch: ");
                        List<TraceEvent> invisible = v.Log("ends \\(attack action against Hero\\)");
                        List<TraceEvent> sanctuary = v.Log("makes an attack .* Sanctuary ends");
                        if (touch.Count != 1 || invisible.Count != 1 || sanctuary.Count != 1)
                            return ExpectResult.Fail(touch.Count + " touch, " + invisible.Count + " invisibility and " + sanctuary.Count + " sanctuary line(s)");
                        return v.Before(invisible[0].Seq, touch[0].Seq) && v.Before(sanctuary[0].Seq, touch[0].Seq)
                            ? ExpectResult.Pass("visible and unwarded, then the touch") : ExpectResult.Fail("the touch came first", touch[0].Seq);
                    })
                    .Build();
            return b
                .Force(20, 2, "save vs Sanctuary", -1)
                .Turn("hero", 1, Step.Assert("the fighter under Sanctuary", ctx =>
                {
                    ctx.Note("b14s14 fixture=" + SanctuaryOn(ctx.Get("hero"), 15));
                    return true;
                }), Step.Pass())
                .Turn("hero", 2, Step.Assert("after the lost touch", ctx =>
                {
                    CharacterController cleric = ctx.Get("cleric");
                    ctx.Note("b14s14 death.uses=" + cleric.Stats.DeathDomainUsesToday);
                    ctx.Note("b14s14 known-failure=" + WardSaveLedger.HasKnownFailure(cleric, ctx.Get("hero")));
                    return true;
                }), Step.Pass())
                .Expect("The fixture is set", v => DomainNotes(v).Contains("b14s14 fixture=True")
                    ? ExpectResult.Pass("warded") : ExpectResult.Fail(string.Join("; ", DomainNotes(v))))
                .Expect("The decision trace records Death at the melee attack", DomainPowerRow("Death", "site=MeleeAttack"))
                .Expect("One Sanctuary save, failed: the touch is lost", v =>
                    v.Log("cannot follow through against Hero").Count == 1
                        ? ExpectResult.Pass("lost") : ExpectResult.Fail(v.Log("Sanctuary").Count + " Sanctuary line(s)"))
                .Expect("No touch is rolled and no weapon attack follows in round 1", v =>
                {
                    int touches = v.Log("Melee Touch: ").Count;
                    List<TraceEvent> attacks = v.Attacks(by: "cleric", aoo: false, round: 1);
                    return touches == 0 && attacks.Count == 0
                        ? ExpectResult.Pass("none") : ExpectResult.Fail(touches + " touch line(s), " + attacks.Count + " attack(s)");
                })
                .Expect("The day's use is kept and the cleric knows it cannot attack the fighter (D6)", v =>
                {
                    List<string> notes = DomainNotes(v);
                    return notes.Contains("b14s14 death.uses=0") && notes.Contains("b14s14 known-failure=True")
                        ? ExpectResult.Pass(string.Join("; ", notes)) : ExpectResult.Fail(string.Join("; ", notes));
                })
                .Build();
        }

        /// <summary>
        /// D7 (PHB p.186; batch 14 step 14, C14): a Destruction-domain cleric 5 beside the sturdy idle fighter declares its
        /// smite before the first swing of round 1 (its gain, +4 and +5 damage on that swing, beats 0), once a day: round 2
        /// has none. The round 1 attack's modifier is 4 above round 2's, the smite's damage term rides on it, and the fighter's
        /// turn notes the day's use spent and the smite used up.
        /// </summary>
        private static ScenarioDef AiClericDestructionSmiteFirstSwing()
        {
            return DomainLayout("rules/ai-cleric-destruction-smite-first-swing",
                    "An AI Destruction cleric smites with its first melee swing, once a day (PHB p.186, D7)", 11, 2, Control.Scripted, "Destruction", "Evil")
                .Tweak("hero", Sturdy)
                .Turn("hero", 1, Step.Assert("the smite at the fighter's turn", ctx =>
                {
                    CharacterStats st = ctx.Get("cleric").Stats;
                    ctx.Note("b14s14 smite.uses=" + st.DestructionDomainUsesToday);
                    ctx.Note("b14s14 smite.active=" + st.DestructionSmiteActive);
                    return true;
                }), Step.Pass())
                .Expect("The decision trace records Destruction at the smite", DomainPowerRow("Destruction", "site=Smite"))
                .Expect("One smite, in round 1, before the cleric's first attack", v =>
                {
                    List<TraceEvent> smites = v.Log("invokes Destruction Smite");
                    List<TraceEvent> attacks = FirstRoundAttacks(v, "cleric");
                    if (smites.Count != 1 || smites[0].Round != 1)
                        return ExpectResult.Fail(smites.Count + " smite line(s)");
                    return attacks.Count > 0 && v.Before(smites[0].Seq, attacks[0].Seq)
                        ? ExpectResult.Pass("smite then attack") : ExpectResult.Fail("no attack after the smite");
                })
                .Expect("The smiting attack's modifier is 4 above the next round's", v =>
                {
                    List<TraceEvent> r1 = FirstRoundAttacks(v, "cleric");
                    List<TraceEvent> r2 = v.Attacks(by: "cleric", aoo: false, round: 2);
                    if (r1.Count == 0 || r2.Count == 0)
                        return ExpectResult.Fail(r1.Count + " round 1 and " + r2.Count + " round 2 attack(s)");
                    int diff = r1[0].Int("mod") - r2[0].Int("mod");
                    return diff == 4 ? ExpectResult.Pass("+4") : ExpectResult.Fail("difference " + diff, r1[0].Seq);
                })
                .Expect("The day's use is spent and the smite used up by the attack", v =>
                {
                    List<string> notes = DomainNotes(v);
                    return notes.Contains("b14s14 smite.uses=1") && notes.Contains("b14s14 smite.active=False")
                        ? ExpectResult.Pass(string.Join("; ", notes)) : ExpectResult.Fail(string.Join("; ", notes));
                })
                .Build();
        }
    }
}
#endif
