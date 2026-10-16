using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Combat
{
    /// <summary>
    /// Mirror Image by RAW (PHB p.254; decision D7, batch 11 step 26; <see cref="MirrorImageRules"/>): the image count,
    /// the 1d(N+1) figure pick and its distribution, a weapon attack on an image (image AC, destroyed on a hit, no damage)
    /// and on the caster (normal, and the attacker learns the real figure), the caster's movement confounding it, the
    /// exemptions (sightless attacker, True Seeing, invisible caster), with a PC and an NPC attacker alike; and, with a
    /// scene GameManager (Play pass), spells through the cast core's MirrorImage stage on the AI and the UI path (a no-roll
    /// spell spent on an image, a ray rolled against the image AC, Magic Missile's per-missile picks, an ally's spell).
    /// </summary>
    public static class MirrorImageRulesTests
    {
        private static int _passed;
        private static int _failed;

        public static void mirror_image_rules_test() => RunAll();

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== MIRROR IMAGE RULES TESTS ======");
            Tests.Utilities.TestHelpers.EnsureCoreDatabasesInitialized();
            SpellDatabase.Init();

            TestSpellDefinition();
            TestImageCount();
            TestFigurePick();
            TestPickDistribution();
            TestSwapRemoved();

            Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
            try
            {
                // "PC and AI attackers alike": the attacker on either side.
                TestWeaponAttacks(CharacterTeam.Enemy, CharacterTeam.Player, "NPC attacker vs PC caster");
                TestWeaponAttacks(CharacterTeam.Player, CharacterTeam.Enemy, "PC attacker vs NPC caster");
                TestExemptions();
                TestMovementAndReveal();
                TestImageAttackBookkeeping();
                TestGrappleExemption();

                if (GameManager.Instance != null)
                {
                    TestSpellPicks(GameManager.Instance);
                    TestEffectAttacks(GameManager.Instance);
                    TestDispelAndConsumables(GameManager.Instance);
                    TestForcedMovement(GameManager.Instance);
                }
                else
                    Debug.Log("  SKIP: the spell picks, effect attacks, dispels, consumables and forced moves (no scene GameManager; they run in the Play pass)");
            }
            finally
            {
                ScenarioHooks.RollFilter = saved;
            }

            Debug.Log($"====== Mirror Image Rules Results: {_passed} passed, {_failed} failed ======");
        }

        private static void Assert(bool condition, string testName, string detail = "")
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"  PASS: {testName}");
            }
            else
            {
                _failed++;
                Debug.LogError($"  FAIL: {testName} {detail}");
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────────────

        private static CharacterController Create(string name, CharacterTeam team, Vector2Int pos, int dex = 14)
        {
            var go = new GameObject($"MirrorImageTest_{name}");
            var controller = go.AddComponent<CharacterController>();
            var inventory = go.AddComponent<InventoryComponent>();
            var stats = new CharacterStats(
                name: name, level: 5, characterClass: "Wizard",
                str: 14, dex: dex, con: 12, wis: 10, intelligence: 16, cha: 10,
                bab: 3, armorBonus: 4, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 30, raceName: "Human");
            stats.BaseAttackBonusOverride = 3;
            controller.Init(stats, pos, null, null);
            inventory.Init(stats);
            controller.GridPosition = pos;
            controller.SetTeam(team);
            stats.AdjustMaxHP(500);
            stats.CurrentHP += 500;
            return controller;
        }

        private static void Destroy(params CharacterController[] controllers)
        {
            foreach (CharacterController c in controllers)
                if (c != null)
                    UnityEngine.Object.DestroyImmediate(c.gameObject);
        }

        /// <summary>A roll filter that forces the figure pick, the d20 and the d4 of the count; records the figure dice asked for.</summary>
        private static Func<int, string, int, int> Forced(int figure, int d20, int d4, List<int> figureSides = null)
        {
            return (sides, ctx, natural) =>
            {
                if (ctx == MirrorImageRules.FigureRollContext)
                {
                    figureSides?.Add(sides);
                    return Mathf.Min(figure, sides);
                }
                if (ctx == MirrorImageRules.CountRollContext)
                    return Mathf.Min(d4, sides);
                if (sides == 20)
                    return d20;
                return natural;
            };
        }

        // ── Pure rules ──────────────────────────────────────────────────────────────────────────────────────────

        private static void TestSpellDefinition()
        {
            SpellData spell = SpellDatabase.GetSpell(SpellNames.MIRROR_IMAGE);
            Assert(spell != null, "Mirror Image spell definition exists");
            if (spell == null)
                return;

            Assert(spell.SpellLevel == 2, "Mirror Image is level 2");
            Assert(spell.TargetType == SpellTargetType.Self, "Mirror Image target is self/personal");
            Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
                "Mirror Image duration is 1 minute/level");
            Assert(spell.IsAvailableFor("Wizard", 2), "Mirror Image available to Wizard 2");
            Assert(spell.IsAvailableFor("Sorcerer", 2), "Mirror Image available to Sorcerer 2");
            Assert(spell.IsAvailableFor("Bard", 2), "Mirror Image available to Bard 2");
        }

        private static void TestImageCount()
        {
            Assert(MirrorImageRules.ImageCountDice(3).Bonus == 1 && MirrorImageRules.ImageCountDice(5).Bonus == 1
                   && MirrorImageRules.ImageCountDice(6).Bonus == 2 && MirrorImageRules.ImageCountDice(2).Bonus == 0,
                "The count is 1d4 + 1 per three caster levels (PHB p.254)");
            Assert(MirrorImageRules.CapImageCount(12) == 8 && MirrorImageRules.CapImageCount(8) == 8 && MirrorImageRules.CapImageCount(5) == 5,
                "At most eight images (PHB p.254)");

            Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
            try
            {
                SpellData spell = SpellDatabase.GetSpell(SpellNames.MIRROR_IMAGE).Clone();
                ScenarioHooks.RollFilter = Forced(1, 10, 3);
                int cl3 = MirrorImageRules.RollImageCount(spell, 3, out int rolled3);
                Assert(cl3 == 4 && rolled3 == 4, "Caster level 3, d4 = 3: 3 + 1 = 4 images", cl3 + " / " + rolled3);
                ScenarioHooks.RollFilter = Forced(1, 10, 4);
                int cl15 = MirrorImageRules.RollImageCount(spell, 15, out int rolled15);
                Assert(cl15 == 8 && rolled15 == 9, "Caster level 15, d4 = 4: 4 + 5 = 9, capped at 8", cl15 + " / " + rolled15);
                ScenarioHooks.RollFilter = Forced(1, 10, 1);
                int cl1 = MirrorImageRules.RollImageCount(spell, 1, out _);
                Assert(cl1 == 1, "Caster level 1, d4 = 1: one image", "" + cl1);
            }
            finally
            {
                ScenarioHooks.RollFilter = saved;
            }
        }

        private static void TestFigurePick()
        {
            Assert(MirrorImageRules.FigureFromRoll(1) == MirrorImageFigurePick.Caster, "A pick roll of 1 is the real caster");
            Assert(MirrorImageRules.FigureFromRoll(2) == MirrorImageFigurePick.Image && MirrorImageRules.FigureFromRoll(9) == MirrorImageFigurePick.Image,
                "Any higher pick roll is an image");
            Assert(MirrorImageRules.PickFigure(0) == MirrorImageFigurePick.NoPick, "No images, no pick");

            Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
            try
            {
                var sides = new List<int>();
                for (int n = 1; n <= MirrorImageRules.MaxImages; n++)
                {
                    ScenarioHooks.RollFilter = Forced(1, 10, 1, sides);
                    MirrorImageRules.PickFigure(n);
                }
                Assert(string.Join(",", sides) == "2,3,4,5,6,7,8,9", "The pick is 1d(N+1) over N images and the caster", string.Join(",", sides));
            }
            finally
            {
                ScenarioHooks.RollFilter = saved;
            }
        }

        private static void TestPickDistribution()
        {
            Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
            ScenarioHooks.RollFilter = null;
            try
            {
                foreach (int images in new[] { 1, 3, 7 })
                {
                    const int trials = 6000;
                    int caster = 0;
                    for (int i = 0; i < trials; i++)
                        if (MirrorImageRules.PickFigure(images) == MirrorImageFigurePick.Caster)
                            caster++;
                    float share = caster / (float)trials;
                    float expected = 1f / (images + 1);
                    Assert(Mathf.Abs(share - expected) < 0.03f,
                        $"With {images} image(s) the caster is picked about 1/{images + 1} of the time", $"{share:F3} vs {expected:F3}");
                }
            }
            finally
            {
                ScenarioHooks.RollFilter = saved;
            }
        }

        private static void TestSwapRemoved()
        {
            Type gm = typeof(GameManager);
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                                                      | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            Assert(gm.GetMethod("TryBeginMirrorImageSwapSelection", all) == null && gm.GetField("_isSelectingMirrorImageSwap", all) == null,
                "The end-of-turn swap is gone (SPL-117)");
            Assert(gm.GetMethod("GetMirrorImagePriorityTargetForAI", all) == null, "The AI's map-wide Mirror Image priority target is gone (AI-036)");
            Assert(Type.GetType("MirrorImageClone") == null && typeof(GameManager).Assembly.GetType("MirrorImageClone") == null,
                "No image tokens: the images are a count on the caster");
        }

        // ── Weapon attacks ──────────────────────────────────────────────────────────────────────────────────────

        private static void TestWeaponAttacks(CharacterTeam attackerTeam, CharacterTeam casterTeam, string label)
        {
            CharacterController attacker = null, caster = null;
            try
            {
                attacker = Create("Attacker", attackerTeam, new Vector2Int(0, 0));
                caster = Create("Caster", casterTeam, new Vector2Int(1, 0), dex: 14);
                MirrorImageRules.Grant(caster, 3);
                int imageAc = MirrorImageRules.ImageArmorClass(caster.Stats);
                Assert(imageAc == 10 + caster.Stats.SizeModifier + caster.Stats.DEXMod && imageAc == 12,
                    label + ": image AC is 10 + size + Dex (12), not the caster's AC " + caster.Stats.ArmorClass, "" + imageAc);

                // An image picked, the d20 hits the image AC: the image vanishes, no damage, no hit on the caster.
                int hp = caster.Stats.CurrentHP;
                ScenarioHooks.RollFilter = Forced(3, 10, 1);
                CombatResult r = attacker.Attack(caster, false, 0, null);
                Assert(r != null && r.MirrorImageFigure && r.MirrorImageFigureHit && !r.Hit && r.TargetAC == imageAc && r.FinalDamageDealt == 0,
                    label + ": an attack that picks an image rolls against the image AC and destroys it on a hit (no damage)",
                    r != null ? $"figure {r.MirrorImageFigure}, figureHit {r.MirrorImageFigureHit}, hit {r.Hit}, AC {r.TargetAC}, dmg {r.FinalDamageDealt}" : "no result");
                Assert(MirrorImageRules.ImagesOf(caster) == 2 && caster.Stats.CurrentHP == hp, label + ": one image fewer, the caster unharmed",
                    MirrorImageRules.ImagesOf(caster) + " images, HP " + caster.Stats.CurrentHP + "/" + hp);
                Assert(r != null && r.GetDetailedSummary().Contains("image AC"), label + ": the log names the image AC",
                    r != null ? r.GetDetailedSummary() : "");

                // An image picked, the d20 misses: nothing changes.
                ScenarioHooks.RollFilter = Forced(2, 5, 1);
                r = attacker.Attack(caster, false, 0, null);
                Assert(r != null && r.MirrorImageFigure && !r.MirrorImageFigureHit && MirrorImageRules.ImagesOf(caster) == 2,
                    label + ": a miss against an image changes nothing", "" + MirrorImageRules.ImagesOf(caster));

                // The caster picked on a 1: a normal attack against the caster's AC.
                ScenarioHooks.RollFilter = Forced(1, 19, 1);
                r = attacker.Attack(caster, false, 0, null);
                // The test attacker is unarmed (nonlethal damage, PHB p.139), so the hit shows as damage, not lost hit points.
                Assert(r != null && !r.MirrorImageFigure && r.Hit && r.TargetAC != imageAc && r.Damage > 0,
                    label + ": a pick of 1 strikes the real caster normally",
                    r != null ? $"figure {r.MirrorImageFigure}, hit {r.Hit}, AC {r.TargetAC}, damage {r.Damage}" : "no result");
                Assert(MirrorImageRules.ImagesOf(caster) == 2 && MirrorImageRules.KnowsRealFigure(attacker, caster),
                    label + ": the strike that did not vanish shows the attacker the real figure");

                // A knowing attacker rolls no pick.
                var sides = new List<int>();
                ScenarioHooks.RollFilter = Forced(3, 19, 1, sides);
                r = attacker.Attack(caster, false, 0, null);
                Assert(sides.Count == 0 && r != null && !r.MirrorImageFigure, label + ": an attacker that knows the real figure attacks it directly",
                    "picks " + sides.Count);

                // The last images: destroying them ends the spell.
                MirrorImageRules.ConfoundOnMove(caster, ownMovement: true);
                ScenarioHooks.RollFilter = Forced(2, 15, 1);
                attacker.Attack(caster, false, 0, null);
                attacker.Attack(caster, false, 0, null);
                Assert(caster.MirrorImageFigures == null, label + ": destroying the last image ends Mirror Image");
            }
            finally
            {
                ScenarioHooks.RollFilter = null;
                Destroy(attacker, caster);
            }
        }

        private static void TestExemptions()
        {
            CharacterController attacker = null, caster = null;
            try
            {
                attacker = Create("Grimlock", CharacterTeam.Enemy, new Vector2Int(0, 0));
                caster = Create("Caster", CharacterTeam.Player, new Vector2Int(1, 0));
                MirrorImageRules.Grant(caster, 4);

                Assert(MirrorImageRules.WhyNoPick(attacker, caster) == null, "A sighted enemy is fooled");
                Assert(MirrorImageRules.WhyNoPick(caster, caster) != null, "The caster's own figures never fool it");

                attacker.Stats.CreatureTags.Add(MirrorImageRules.SightlessTag);
                attacker.Stats.CreatureTags.Add("Blindsight40");
                Assert(MirrorImageRules.WhyNoPick(attacker, caster) != null, "A sightless attacker (blindsight, no eyes) is not fooled (D7 (7))");
                attacker.Stats.CreatureTags.Remove(MirrorImageRules.SightlessTag);
                Assert(MirrorImageRules.WhyNoPick(attacker, caster) == null, "Blindsight alone does not defeat the images (a creature that sees is fooled)");

                string oldType = attacker.Stats.CreatureType;
                attacker.Stats.CreatureType = "Ooze";
                Assert(MirrorImageRules.WhyNoPick(attacker, caster) != null, "An ooze (blind, MM p.313) is not fooled");
                attacker.Stats.CreatureType = oldType;

                SpellData trueSeeing = SpellDatabase.GetSpell(SpellNames.TRUE_SEEING);
                if (trueSeeing != null)
                {
                    StatusEffectManager sem = attacker.gameObject.GetComponent<StatusEffectManager>() ?? attacker.gameObject.AddComponent<StatusEffectManager>();
                    sem.Init(attacker.Stats);
                    sem.AddEffect(trueSeeing.Clone(), "Test", 9);
                    Assert(MirrorImageRules.WhyNoPick(attacker, caster) != null, "True Seeing sees through the images (PHB p.296)");
                    sem.RemoveEffectsBySpellId(SpellNames.TRUE_SEEING);
                }

                caster.ApplyInvisibilityEffectData(InvisibilityEffectData.CreateGreaterInvisibility(5, caster));
                Assert(MirrorImageRules.WhyNoPick(attacker, caster) != null, "An invisible caster's images have no effect (PHB p.254)");

                // A sightless attacker's attack goes straight to the caster: no figure roll.
                caster.ClearInvisibilityEffect();
                attacker.Stats.CreatureTags.Add(MirrorImageRules.SightlessTag);
                var sides = new List<int>();
                ScenarioHooks.RollFilter = Forced(3, 19, 1, sides);
                CombatResult r = attacker.Attack(caster, false, 0, null);
                Assert(sides.Count == 0 && r != null && !r.MirrorImageFigure, "A sightless attacker's attack rolls no figure pick", "picks " + sides.Count);
            }
            finally
            {
                ScenarioHooks.RollFilter = null;
                Destroy(attacker, caster);
            }
        }

        private static void TestMovementAndReveal()
        {
            CharacterController attacker = null, caster = null, ally = null;
            try
            {
                attacker = Create("Orc", CharacterTeam.Enemy, new Vector2Int(0, 0));
                caster = Create("Caster", CharacterTeam.Player, new Vector2Int(1, 0));
                ally = Create("Ally", CharacterTeam.Player, new Vector2Int(2, 0));
                MirrorImageRules.Grant(caster, 3);

                MirrorImageRules.RevealRealFigure(caster, attacker);
                Assert(MirrorImageRules.KnowsRealFigure(attacker, caster), "A strike on the real figure that does not vanish teaches the striker");
                Assert(!MirrorImageRules.ConfoundOnMove(caster, ownMovement: false) && MirrorImageRules.KnowsRealFigure(attacker, caster),
                    "Being moved (pushed, dragged) does not confound");
                Assert(MirrorImageRules.ConfoundOnMove(caster, ownMovement: true) && !MirrorImageRules.KnowsRealFigure(attacker, caster),
                    "The caster's own movement confounds every enemy that had learned the real figure (PHB p.254)");

                MirrorImageRules.RevealRealFigure(caster, ally);
                Assert(MirrorImageRules.KnowsRealFigure(ally, caster),
                    "Knowing is what a creature saw, not its side: an ally that sees the strike learns too (reading (d))");

                MirrorImageRules.RevealRealFigure(caster, attacker);
                caster.ForgetCreature(attacker);
                Assert(!MirrorImageRules.KnowsRealFigure(attacker, caster), "A reused slot forgets its old creature's knowledge (CRE-046)");

                caster.ResetForNewCreature("test");
                Assert(caster.MirrorImageFigures == null, "ResetForNewCreature clears the images (CRE-046)");
            }
            finally
            {
                Destroy(attacker, caster, ally);
            }
        }

        private static void TestImageAttackBookkeeping()
        {
            CharacterController attacker = null, caster = null;
            try
            {
                attacker = Create("Smiter", CharacterTeam.Enemy, new Vector2Int(0, 0));
                caster = Create("Caster", CharacterTeam.Player, new Vector2Int(1, 0));
                MirrorImageRules.Grant(caster, 3);
                attacker.Stats.DestructionSmiteActive = true;
                ScenarioHooks.RollFilter = Forced(2, 3, 1);
                CombatResult r = attacker.Attack(caster, false, 0, null);
                Assert(r != null && r.MirrorImageFigure && !attacker.Stats.DestructionSmiteActive,
                    "A declared Destruction smite (PHB p.186) is used up by an attack that picks an image",
                    r != null ? "figure " + r.MirrorImageFigure + ", smite still active " + attacker.Stats.DestructionSmiteActive : "no result");
            }
            finally
            {
                ScenarioHooks.RollFilter = null;
                Destroy(attacker, caster);
            }
        }

        private static void TestGrappleExemption()
        {
            CharacterController grappler = null, caster = null;
            try
            {
                grappler = Create("Grappler", CharacterTeam.Enemy, new Vector2Int(0, 0));
                caster = Create("Caster", CharacterTeam.Player, new Vector2Int(1, 0));
                MirrorImageRules.Grant(caster, 3);
                System.Reflection.MethodInfo establish = typeof(CharacterController).GetMethod("EstablishGrappleWith",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert(establish != null, "CharacterController.EstablishGrappleWith exists (TST-006)");
                if (establish == null)
                    return;
                establish.Invoke(grappler, new object[] { caster });
                Assert(MirrorImageRules.WhyNoPick(grappler, caster) == "the attacker is grappling the caster",
                    "A creature grappling the caster feels the real figure: no pick (delegated RAW reading 2026-10-14)",
                    MirrorImageRules.WhyNoPick(grappler, caster));
                var sides = new List<int>();
                ScenarioHooks.RollFilter = Forced(3, 19, 1, sides);
                CombatResult r = grappler.Attack(caster, false, 0, null);
                Assert(sides.Count == 0 && r != null && !r.MirrorImageFigure, "The grappler's attack on the held caster rolls no figure pick", "picks " + sides.Count);
                grappler.ReleaseGrappleState("test");
                MirrorImageRules.ConfoundOnMove(caster, ownMovement: true); // the hit on the held caster taught it the real figure
                Assert(MirrorImageRules.WhyNoPick(grappler, caster) == null, "Once the grapple ends the images fool it again",
                    MirrorImageRules.WhyNoPick(grappler, caster));
            }
            finally
            {
                ScenarioHooks.RollFilter = null;
                if (grappler != null)
                    grappler.ReleaseGrappleState("test cleanup");
                Destroy(grappler, caster);
            }
        }

        // ── Effect attacks, dispels, consumables, forced moves (Play pass) ──────────────────────────────────────

        private static void TestEffectAttacks(GameManager gm)
        {
            if (gm.Harness_OpenCastCount != 0)
            {
                Debug.Log("  SKIP: the effect attacks (a cast is open in this Play session)");
                return;
            }

            CharacterController cleric = null, target = null;
            try
            {
                cleric = Create("EnemyCleric", CharacterTeam.Enemy, new Vector2Int(0, 0));
                target = Create("ImageCaster", CharacterTeam.Player, new Vector2Int(3, 0));
                MirrorImageRules.Grant(target, 3);

                Assert(MirrorImageRules.MakesOwnAttackRolls(SpellDatabase.GetSpell(SpellNames.SPIRITUAL_WEAPON))
                       && MirrorImageRules.MakesOwnAttackRolls(SpellDatabase.GetSpell(SpellNames.PRODUCE_FLAME))
                       && !MirrorImageRules.MakesOwnAttackRolls(SpellDatabase.GetSpell(SpellNames.HOLD_PERSON)),
                    "Spiritual Weapon and Produce Flame make their own attacks: the cast picks no figure");

                System.Reflection.MethodInfo swAttack = typeof(GameManager).GetMethod("ResolveSpiritualWeaponAttack",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert(swAttack != null, "GameManager.ResolveSpiritualWeaponAttack exists (TST-006)");
                if (swAttack == null)
                    return;

                int hp = target.Stats.CurrentHP;
                ScenarioHooks.RollFilter = Forced(2, 2, 1);
                string log = (string)swAttack.Invoke(gm, new object[] { cleric, target, 3, null });
                Assert(MirrorImageRules.ImagesOf(target) == 3 && target.Stats.CurrentHP == hp && log != null && log.Contains("misses the image"),
                    "Spiritual Weapon: an attack that picks an image and misses its AC changes nothing", log);

                ScenarioHooks.RollFilter = Forced(2, 19, 1);
                log = (string)swAttack.Invoke(gm, new object[] { cleric, target, 3, null });
                Assert(MirrorImageRules.ImagesOf(target) == 2 && target.Stats.CurrentHP == hp && log != null && log.Contains("IMAGE STRUCK"),
                    "Spiritual Weapon: an attack that hits an image destroys it, no damage", log);

                ScenarioHooks.RollFilter = Forced(1, 19, 1);
                log = (string)swAttack.Invoke(gm, new object[] { cleric, target, 3, null });
                Assert(MirrorImageRules.ImagesOf(target) == 2 && target.Stats.CurrentHP < hp && MirrorImageRules.KnowsRealFigure(cleric, target),
                    "Spiritual Weapon: a pick of 1 hits the real caster, and the cleric learns the real figure", log);

                // The cast through the core: no figure pick at cast time, the weapon is created, its first attack picks.
                SpellcastingComponent sc = cleric.gameObject.AddComponent<SpellcastingComponent>();
                sc.Init(cleric.Stats);
                sc.SpellSlots.Clear();
                MirrorImageRules.Grant(target, 3);
                ScenarioHooks.RollFilter = Forced(2, 2, 1);
                CastOutcome o = CastAi(gm, sc, cleric, target, SpellNames.SPIRITUAL_WEAPON);
                Assert(o != null && o.At != CastStage.MirrorImage && cleric.Stats.SpiritualWeaponActive && MirrorImageRules.ImagesOf(target) == 3
                       && !MirrorImageRules.KnowsRealFigure(cleric, target),
                    "Spiritual Weapon cast at a caster with images: the weapon is created and its first attack, on an image, misses (no reveal)",
                    o + " weapon " + cleric.Stats.SpiritualWeaponActive + " images " + MirrorImageRules.ImagesOf(target));
            }
            catch (Exception ex)
            {
                Exception inner = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                Assert(false, "The effect attack tests ran without exceptions", inner.Message);
            }
            finally
            {
                ScenarioHooks.RollFilter = null;
                if (cleric != null && cleric.Stats != null)
                {
                    cleric.Stats.SpiritualWeaponActive = false;
                    cleric.Stats.SpiritualWeaponTarget = null;
                }
                if (gm.Harness_OpenCastCount > 0)
                    gm.Harness_EndAllCasts("MirrorImageRulesTests cleanup");
                Destroy(cleric, target);
            }
        }

        private static void TestDispelAndConsumables(GameManager gm)
        {
            if (gm.Harness_OpenCastCount != 0)
            {
                Debug.Log("  SKIP: the dispel and consumable checks (a cast is open in this Play session)");
                return;
            }

            CharacterController dispeller = null, target = null;
            try
            {
                dispeller = Create("EnemyDispeller", CharacterTeam.Enemy, new Vector2Int(0, 0));
                target = Create("ImageCaster", CharacterTeam.Player, new Vector2Int(3, 0));
                StatusEffectManager sem = target.gameObject.GetComponent<StatusEffectManager>() ?? target.gameObject.AddComponent<StatusEffectManager>();
                sem.Init(target.Stats);

                // A dispelled Mirror Image ends its images.
                sem.AddEffect(SpellDatabase.GetSpell(SpellNames.MIRROR_IMAGE).Clone(), "Someone Else", 5);
                MirrorImageRules.Grant(target, 3);
                ScenarioHooks.RollFilter = Forced(1, 20, 1);
                gm.PerformTargetedDispel(dispeller, target, 10);
                Assert(!sem.HasEffect(SpellNames.MIRROR_IMAGE) && MirrorImageRules.ImagesOf(target) == 0 && MirrorImageRules.WhyNoPick(dispeller, target) == "no images",
                    "A dispelled Mirror Image ends every image (PHB p.223)", "images " + MirrorImageRules.ImagesOf(target));

                // A targeted Dispel Magic whose pick names an image checks the Mirror Image alone (delegated RAW reading).
                SpellcastingComponent sc = dispeller.gameObject.AddComponent<SpellcastingComponent>();
                sc.Init(dispeller.Stats);
                sc.SpellSlots.Clear();
                sem.AddEffect(SpellDatabase.GetSpell(SpellNames.MIRROR_IMAGE).Clone(), "Someone Else", 5);
                sem.AddEffect(SpellDatabase.GetSpell(SpellNames.BULLS_STRENGTH).Clone(), "Someone Else", 5);
                MirrorImageRules.Grant(target, 3);
                ScenarioHooks.RollFilter = Forced(2, 20, 1);
                // The test wizard 5 has no 3rd-level slot (SPL-041): a clone at 2nd level, the stage under test ignores the level.
                SpellData dispel = SpellDatabase.GetSpell(SpellNames.DISPEL_MAGIC).Clone();
                dispel.SpellLevel = 2;
                CastOutcome o = CastAi(gm, sc, dispeller, target, dispel);
                Assert(o != null && o.At == CastStage.MirrorImage && o.Reason == "dispel-at-image"
                       && MirrorImageRules.ImagesOf(target) == 0 && !sem.HasEffect(SpellNames.MIRROR_IMAGE) && sem.HasEffect(SpellNames.BULLS_STRENGTH),
                    "Dispel Magic aimed at an image dispels the Mirror Image alone; the other spells on the caster stay",
                    o + " images " + MirrorImageRules.ImagesOf(target) + " bull " + sem.HasEffect(SpellNames.BULLS_STRENGTH));
                sem.RemoveEffectsBySpellId(SpellNames.BULLS_STRENGTH);

                // A scroll works at its own caster level (DMG p.238): CL 6 read by a caster level 5 wizard, d4 = 1: 1 + 2 images.
#pragma warning disable CS0618
                ItemData scroll = ItemDatabase.CloneItem(ScrollFactory.GenerateScrollId(SpellNames.MIRROR_IMAGE, "Arcane"));
#pragma warning restore CS0618
                Assert(scroll != null && scroll.Scroll != null, "The ItemDatabase has a scroll of Mirror Image");
                System.Reflection.MethodInfo apply = typeof(GameManager).GetMethod("TryApplySpellConsumableEffect",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert(apply != null, "GameManager.TryApplySpellConsumableEffect exists (TST-006)");
                if (scroll != null && scroll.Scroll != null && apply != null)
                {
                    scroll.Scroll.CasterLevel = 6;
                    ScenarioHooks.RollFilter = Forced(1, 10, 1);
                    object[] args = { target, scroll, null };
                    bool ok = (bool)apply.Invoke(gm, args);
                    Assert(ok && MirrorImageRules.ImagesOf(target) == 3,
                        "A caster level 6 scroll of Mirror Image gives 1d4 + 2 images, whatever the reader's level (DMG p.238)",
                        "ok " + ok + " images " + MirrorImageRules.ImagesOf(target) + " " + args[2]);
                }
            }
            catch (Exception ex)
            {
                Exception inner = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                Assert(false, "The dispel and consumable tests ran without exceptions", inner.Message);
            }
            finally
            {
                ScenarioHooks.RollFilter = null;
                if (gm.Harness_OpenCastCount > 0)
                    gm.Harness_EndAllCasts("MirrorImageRulesTests cleanup");
                Destroy(dispeller, target);
            }
        }

        private static void TestForcedMovement(GameManager gm)
        {
            CharacterController foe = null, caster = null;
            try
            {
                foe = Create("Foe", CharacterTeam.Enemy, new Vector2Int(0, 0));
                caster = Create("Caster", CharacterTeam.Player, new Vector2Int(1, 0));
                MirrorImageRules.Grant(caster, 3);
                MirrorImageRules.RevealRealFigure(caster, foe);
                gm.NotifyCharacterMovement(caster, new Vector2Int(1, 0), new Vector2Int(2, 0), "reposition", forced: true);
                Assert(MirrorImageRules.KnowsRealFigure(foe, caster), "Being pushed or dragged (a forced move) does not confound, on any turn (D7 (6))");
                gm.NotifyCharacterMovement(caster, new Vector2Int(2, 0), new Vector2Int(3, 0), "move");
                Assert(!MirrorImageRules.KnowsRealFigure(foe, caster), "The caster's own move confounds the foe again (PHB p.254)");
            }
            finally
            {
                Destroy(foe, caster);
            }
        }

        // ── Spells through the cast core (Play pass) ────────────────────────────────────────────────────────────

        private static void TestSpellPicks(GameManager gm)
        {
            if (gm.Harness_OpenCastCount != 0)
            {
                Debug.Log("  SKIP: the spell picks (a cast is open in this Play session)");
                return;
            }

            CharacterController wizard = null, target = null, friend = null;
            try
            {
                wizard = Create("EnemyWizard", CharacterTeam.Enemy, new Vector2Int(0, 0));
                target = Create("ImageCaster", CharacterTeam.Player, new Vector2Int(3, 0));
                friend = Create("TargetsFriend", CharacterTeam.Player, new Vector2Int(4, 0));
                SpellcastingComponent sc = wizard.gameObject.AddComponent<SpellcastingComponent>();
                sc.Init(wizard.Stats);
                sc.SpellSlots.Clear();

                // A spell with no attack roll that picks an image is spent on it and destroys it (D7 (a)).
                MirrorImageRules.Grant(target, 3);
                int hp = target.Stats.CurrentHP;
                ScenarioHooks.RollFilter = Forced(2, 10, 1);
                CastOutcome o = CastAi(gm, sc, wizard, target, SpellNames.HOLD_PERSON);
                Assert(o != null && o.Kind == CastOutcomeKind.Resolved && o.At == CastStage.MirrorImage && o.Reason == "struck-image"
                       && MirrorImageRules.ImagesOf(target) == 2 && !target.HasCondition(CombatConditionType.Paralyzed),
                    "AI: Hold Person that picks an image is spent on it, which vanishes", o + " images " + MirrorImageRules.ImagesOf(target));

                // A ray that picks an image rolls its ranged touch attack against the image AC: a miss leaves it.
                ScenarioHooks.RollFilter = Forced(2, 2, 1);
                o = CastAi(gm, sc, wizard, target, SpellNames.RAY_OF_FROST);
                Assert(o != null && o.At == CastStage.MirrorImage && o.Reason == "missed-image" && MirrorImageRules.ImagesOf(target) == 2 && target.Stats.CurrentHP == hp,
                    "AI: a ray that picks an image and misses its AC leaves it", o + " images " + MirrorImageRules.ImagesOf(target));
                ScenarioHooks.RollFilter = Forced(2, 19, 1);
                o = CastAi(gm, sc, wizard, target, SpellNames.RAY_OF_FROST);
                Assert(o != null && o.Reason == "struck-image" && MirrorImageRules.ImagesOf(target) == 1 && target.Stats.CurrentHP == hp,
                    "AI: a ray that hits the image AC destroys the image, no damage", o + " images " + MirrorImageRules.ImagesOf(target));

                // The caster picked: the spell goes on and resolves on the real target, which the wizard now knows.
                ScenarioHooks.RollFilter = Forced(1, 19, 1);
                o = CastAi(gm, sc, wizard, target, SpellNames.RAY_OF_FROST);
                Assert(o != null && o.At == CastStage.CombatEnd && target.Stats.CurrentHP < hp && MirrorImageRules.KnowsRealFigure(wizard, target),
                    "AI: a pick of 1 lets the ray reach the real caster, and the wizard learns the real figure",
                    o + " HP " + target.Stats.CurrentHP + "/" + hp);

                // Magic Missile: one pick per missile (CL 5: three missiles; picks image, image, caster with 3 images).
                MirrorImageRules.Grant(target, 3);
                int mmHp = target.Stats.CurrentHP;
                var picks = new Queue<int>(new[] { 2, 2, 1 });
                ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                    ctx == MirrorImageRules.FigureRollContext ? (picks.Count > 0 ? picks.Dequeue() : 1) : natural;
                o = CastAi(gm, sc, wizard, target, SpellNames.MAGIC_MISSILE);
                Assert(o != null && o.At == CastStage.CombatEnd && MirrorImageRules.ImagesOf(target) == 1 && target.Stats.CurrentHP < mmHp
                       && mmHp - target.Stats.CurrentHP <= 5,
                    "AI: Magic Missile picks per missile: two images vanish, one missile (at most 5 damage) reaches the caster",
                    o + " images " + MirrorImageRules.ImagesOf(target) + " damage " + (mmHp - target.Stats.CurrentHP));

                // UI path: the same pick for a PC's spell.
                CharacterController pcWizard = Create("PcWizard", CharacterTeam.Player, new Vector2Int(0, 2));
                CharacterController npcTarget = Create("NpcImageCaster", CharacterTeam.Enemy, new Vector2Int(3, 2));
                try
                {
                    SpellcastingComponent pcSc = pcWizard.gameObject.AddComponent<SpellcastingComponent>();
                    pcSc.Init(pcWizard.Stats);
                    pcSc.SpellSlots.Clear();
                    MirrorImageRules.Grant(npcTarget, 2);
                    ScenarioHooks.RollFilter = Forced(3, 10, 1);
                    o = CastUi(gm, pcSc, pcWizard, npcTarget, SpellNames.HOLD_PERSON);
                    Assert(o != null && o.At == CastStage.MirrorImage && o.Reason == "struck-image" && MirrorImageRules.ImagesOf(npcTarget) == 1,
                        "UI: a PC's Hold Person that picks an image is spent on it, the same rule", "" + o);
                }
                finally
                {
                    Destroy(pcWizard, npcTarget);
                }

                // An ally's hostile spell picks a figure like a foe's (reading (d)): the act, not the team.
                MirrorImageRules.Grant(target, 3);
                SpellcastingComponent hostileFriendSc = friend.gameObject.AddComponent<SpellcastingComponent>();
                hostileFriendSc.Init(friend.Stats);
                hostileFriendSc.SpellSlots.Clear();
                ScenarioHooks.RollFilter = Forced(2, 10, 1);
                o = CastAi(gm, hostileFriendSc, friend, target, SpellNames.HOLD_PERSON);
                Assert(o != null && o.At == CastStage.MirrorImage && o.Reason == "struck-image" && MirrorImageRules.ImagesOf(target) == 2,
                    "An ally's hostile spell (Hold Person) picks a figure like a foe's", "" + o);

                // An ally's spell that is not hostile reaches the caster directly: no pick.
                MirrorImageRules.Grant(target, 3);
                SpellcastingComponent friendSc = hostileFriendSc;
                friendSc.SpellSlots.Clear();
                var sides = new List<int>();
                ScenarioHooks.RollFilter = Forced(2, 10, 1, sides);
                o = CastAi(gm, friendSc, friend, target, SpellNames.BULLS_STRENGTH);
                Assert(sides.Count == 0 && o != null && o.At == CastStage.CombatEnd && MirrorImageRules.ImagesOf(target) == 3,
                    "An ally's spell reaches the caster directly (no figure pick)", o + " picks " + sides.Count);
            }
            finally
            {
                ScenarioHooks.RollFilter = null;
                if (gm.Harness_OpenCastCount > 0)
                    gm.Harness_EndAllCasts("MirrorImageRulesTests cleanup");
                Destroy(wizard, target, friend);
            }
        }

        private static SpellSlot AddSlot(SpellcastingComponent sc, CharacterController caster, SpellData template)
        {
            var slot = new SpellSlot(template.SpellLevel, template.Clone(), false, "Wizard");
            sc.SpellSlots.Add(slot);
            caster.Actions.Reset();
            return slot;
        }

        private static CastOutcome CastAi(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController target, string spellId)
            => CastAi(gm, sc, caster, target, SpellDatabase.GetSpell(spellId));

        private static CastOutcome CastAi(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController target, SpellData template)
        {
            SpellSlot slot = AddSlot(sc, caster, template);
            SpellCastRequest req = SpellCastRequest.ForAI(caster, target, template);
            req.Spell.HasMaterialComponent = false;
            req.Spell.HasFocusComponent = false;
            gm.RunCastSync(req, out CastOutcome outcome);
            sc.SpellSlots.Remove(slot);
            return outcome;
        }

        private static CastOutcome CastUi(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController target, string spellId)
        {
            SpellData template = SpellDatabase.GetSpell(spellId);
            SpellSlot slot = AddSlot(sc, caster, template);
            SpellCastRequest req = SpellCastRequest.ForUi(caster, template, null, new CastSource());
            req.Spell.HasMaterialComponent = false;
            req.Spell.HasFocusComponent = false;
            req.Aim.Target = target;
            req.Decider = new SilentUiDecider();
            CastContext ctx = gm.BeginCast(req);
            CastOutcome done = null;
            gm.ExecuteCast(ctx, x => done = x);
            sc.SpellSlots.Remove(slot);
            return done;
        }

        /// <summary>A synchronous decider that answers like the UI with nothing to ask: the choices filled, the cast proceeds.</summary>
        private sealed class SilentUiDecider : ICastDecider
        {
            public bool IsSynchronous => true;
            public CastChoiceFill FillChoices(GameManager gm, CastContext ctx) => CastChoiceFill.Filled;
            public void ChooseProvocation(GameManager gm, CastContext ctx, Action<CastProvocationAnswer> answer) => answer(CastProvocationAnswer.Proceeds);
            public void ChooseTouchDelivery(GameManager gm, CastContext ctx, Action<CastTouchDelivery> answer) => answer(CastTouchDelivery.CastNow);
            public bool CanFill(SpellCastRequest req) => true;
            public bool ForgoSave(CharacterController self, CharacterController caster, SpellData spell) => false;
            public void BeforeCombatEndCheck(GameManager gm, CastContext ctx) { }
            public void OnCastFinished(GameManager gm, CastContext ctx, CastOutcome outcome) { }
        }
    }
}
