using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 11 step 28 (docs/designs/cast_core_batch11.md, ai-metamagic-choice; AI-001, AI-046): the AI's metamagic
/// dimension (AiMetamagicPlanner) and its spell-resistance estimate.
/// <list type="bullet">
/// <item>SpellDiceRules.ExpectedTotal: 10d6 is 35 plain, 52.5 empowered, 60 maximized, 77.5 both (PHB p.93, p.97); a
/// fixed result is unchanged.</item>
/// <item>A sorcerer 10 (CHA 16) with Empower Spell, Quicken Spell and Extend Spell, knowing Fireball and Magic Missile:
/// its candidates are the empowered spells, a clone each carrying the choice (SpellCastRequest.AiMetamagicFor), the
/// database template untouched; never a quickened one (PHB p.98: not on a spontaneous cast) nor an extended Fireball
/// (instantaneous, PHB p.94). With its 5th-level spells per day spent, no Empowered Fireball (PHB p.88: a spell per day
/// of the adjusted level), the Empowered Magic Missile still.</item>
/// <item>Ranking (the plan's check): Empowered Fireball outranks plain Fireball for the sorcerer when its burst takes three
/// foes (17.5 more expected damage on each against a 5th-level spell per day and the full-round action, PHB p.88), and
/// the plain one stays first against one foe; the strategist's own score is the same for both, so the metamagic value
/// decides.</item>
/// <item>A finishing blow: an Empowered Magic Missile whose average drops a 20-hit-point foe that the plain average does
/// not is worth casting; against a 100-hit-point foe it is not.</item>
/// <item>Extend adds the rounds within the fight's horizon (Hold Person at caster level 5: 5 more rounds), Heighten is
/// weighed only for a spell with a save against foes or with a globe on the field, Silent only inside a Silence.</item>
/// <item>A prepared Quickened slot (a wizard's) is worth the action it leaves and pays nothing at the cast.</item>
/// <item>AI-046: the chance to beat spell resistance is (21 + caster level - SR) / 20, clamped (PHB p.177).</item>
/// </list>
/// Builds controllers without CharacterController.Init (as SpontaneousCastAvailabilityTests does), so it runs in edit or
/// Play mode.
/// </summary>
public static class AiMetamagicPlannerTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Debug.Log("====== AI METAMAGIC PLANNER TESTS (batch 11 step 28, AI-001, AI-046) ======");

        TestExpectedTotal();
        TestSpontaneousCandidates();
        TestEmpoweredFireballRanking();
        TestFinishingBlow();
        TestExtendHeightenSilent();
        TestPreparedQuicken();
        TestSpellResistanceChance();
        TestCandidateCapKeepsHighLevels();
        TestEnlargedRange();
        TestBardSilentSpell();

        Debug.Log($"====== AI Metamagic Planner Results: {_passed} passed, {_failed} failed ======");
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

    // ========== HELPERS ==========

    private static CharacterController CreateCaster(string cls, int level, int intelligence, int cha, List<string> known,
        CharacterTeam team, params string[] feats)
    {
        var go = new GameObject($"AiMetamagicPlannerTest_{cls}{level}");
        var controller = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(
            name: cls + " " + level, level: level, characterClass: cls,
            str: 10, dex: 14, con: 12, wis: 10, intelligence: intelligence, cha: cha,
            bab: level / 2, armorBonus: 0, shieldBonus: 0,
            damageDice: 4, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 4 * level, raceName: "Human");
        controller.Stats = stats;
        foreach (string feat in feats)
            stats.Feats.Add(feat);

        var inventory = go.AddComponent<InventoryComponent>();
        inventory.Init(stats);
        SpellComponentKit.Give(stats, inventory.CharacterInventory);

        var spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.SelectedSpellIds = known;
        spellComp.Init(stats);
        controller.SetTeam(team);
        return controller;
    }

    private static CharacterController CreateFoe(string name, int hp)
    {
        var go = new GameObject("AiMetamagicPlannerTest_" + name);
        var controller = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(
            name: name, level: 1, characterClass: "Fighter",
            str: 10, dex: 10, con: 10, wis: 10, intelligence: 10, cha: 10,
            bab: 1, armorBonus: 0, shieldBonus: 0,
            damageDice: 4, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: hp, raceName: "Human");
        controller.Stats = stats;
        stats.CurrentHP = hp;
        controller.SetTeam(CharacterTeam.Player);
        return controller;
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        foreach (CharacterController c in controllers)
            if (c != null && c.gameObject != null)
                Object.DestroyImmediate(c.gameObject);
    }

    private static MetamagicData Feat(MetamagicFeatId feat)
    {
        var metamagic = new MetamagicData();
        metamagic.Toggle(feat);
        return metamagic;
    }

    private static SpellData FindCandidate(List<SpellData> candidates, string spellId, MetamagicFeatId feat)
    {
        foreach (SpellData c in candidates)
        {
            MetamagicData m = SpellCastRequest.AiMetamagicFor(c);
            if (c != null && c.SpellId == spellId && m != null && m.Has(feat))
                return c;
        }
        return null;
    }

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.01f;

    // ========== TESTS ==========

    private static void TestExpectedTotal()
    {
        SpellDice tenD6 = SpellDice.Of(10, 6);
        Assert(Near(SpellDiceRules.ExpectedTotal(tenD6, null), 35f), "10d6 averages 35");
        Assert(Near(SpellDiceRules.ExpectedTotal(tenD6, Feat(MetamagicFeatId.EmpowerSpell)), 52.5f), "Empowered 10d6 averages 52.5 (PHB p.93)");
        Assert(Near(SpellDiceRules.ExpectedTotal(tenD6, Feat(MetamagicFeatId.MaximizeSpell)), 60f), "Maximized 10d6 is 60 (PHB p.97)");
        var both = Feat(MetamagicFeatId.EmpowerSpell);
        both.Toggle(MetamagicFeatId.MaximizeSpell);
        Assert(Near(SpellDiceRules.ExpectedTotal(tenD6, both), 77.5f), "Empowered and maximized 10d6 is 60 + 17.5 (PHB p.97-98)");
        Assert(Near(SpellDiceRules.ExpectedTotal(SpellDice.Of(0, 0, 5), Feat(MetamagicFeatId.EmpowerSpell)), 5f), "A result with no die is unchanged");
    }

    private static void TestSpontaneousCandidates()
    {
        CharacterController sorc = null, plain = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 10, 10, 16, new List<string> { SpellNames.MAGIC_MISSILE, SpellNames.FIREBALL },
                CharacterTeam.Enemy, "Empower Spell", "Quicken Spell", "Extend Spell");
            SpellcastingComponent sc = sorc.Spellcasting;
            Assert(sc != null && sc.IsSpontaneousCaster && sc.SpontaneousData.IsSpellKnown(SpellNames.FIREBALL, 3),
                "Sorcerer 10 is a spontaneous caster knowing Fireball");
            List<SpellData> candidates = AiMetamagicPlanner.SpontaneousCandidates(sorc, new List<CharacterController> { sorc });
            SpellData empFireball = FindCandidate(candidates, SpellNames.FIREBALL, MetamagicFeatId.EmpowerSpell);
            SpellData empMissile = FindCandidate(candidates, SpellNames.MAGIC_MISSILE, MetamagicFeatId.EmpowerSpell);
            Assert(empFireball != null && empMissile != null, "The candidates hold Empowered Fireball and Empowered Magic Missile",
                $"count {candidates.Count}");
            Assert(FindCandidate(candidates, SpellNames.FIREBALL, MetamagicFeatId.QuickenSpell) == null
                   && FindCandidate(candidates, SpellNames.MAGIC_MISSILE, MetamagicFeatId.QuickenSpell) == null,
                "No quickened candidate for a spontaneous caster (PHB p.98 Special)");
            Assert(FindCandidate(candidates, SpellNames.FIREBALL, MetamagicFeatId.ExtendSpell) == null,
                "No Extended Fireball: an instantaneous spell (PHB p.94)");
            SpellData template = SpellDatabase.GetSpell(SpellNames.FIREBALL);
            Assert(empFireball != null && !ReferenceEquals(empFireball, template) && template.AiChosenMetamagic == null && template.MetamagicDataRef == null,
                "The candidate is a clone; the database template carries no metamagic");
            Assert(empFireball != null && empFireball.EffectiveSpellLevel == 5 && !empFireball.IsPreparedMetamagicSlotSpell,
                "Empowered Fireball stands for a 5th-level spell per day, not a prepared slot");
            SpellData castClone = empFireball != null ? empFireball.Clone() : null;
            Assert(castClone != null && castClone.AiChosenMetamagic == null, "A clone of the candidate drops the AI's choice (SpellData.Clone)");

            SpellCastRequest req = empFireball != null ? SpellCastRequest.ForAI(sorc, null, empFireball) : null;
            Assert(req != null && req.Metamagic != null && req.Metamagic.Has(MetamagicFeatId.EmpowerSpell) && req.Source.IsSpontaneous
                   && CastActionRules.CastingAction(req.Spell, req.Metamagic, req.Source) == CastActionCost.FullRound,
                "The AI's request of the candidate carries Empower, a spontaneous source and a full-round action (PHB p.88)");

            while (sc.SpontaneousData.GetSlotsRemaining(5) > 0)
                sc.SpontaneousData.SpendSlot(5);
            candidates = AiMetamagicPlanner.SpontaneousCandidates(sorc, new List<CharacterController> { sorc });
            Assert(FindCandidate(candidates, SpellNames.FIREBALL, MetamagicFeatId.EmpowerSpell) == null
                   && FindCandidate(candidates, SpellNames.MAGIC_MISSILE, MetamagicFeatId.EmpowerSpell) != null,
                "With no 5th-level spell per day left there is no Empowered Fireball; the Empowered Magic Missile (3rd) remains");

            plain = CreateCaster("Sorcerer", 10, 10, 16, new List<string> { SpellNames.FIREBALL }, CharacterTeam.Enemy);
            Assert(AiMetamagicPlanner.SpontaneousCandidates(plain, null).Count == 0, "A sorcerer with no metamagic feat has no candidate");
        }
        finally
        {
            Cleanup(sorc, plain);
        }
    }

    /// <summary>
    /// The cap keeps the strongest spells' options (batch 11 step 28 review): a sorcerer 10 with Empower Spell and Heighten
    /// Spell who knows many cantrips and 1st-level spells still has Empowered Fireball among its candidates, and Heighten
    /// gives one option per spell (the highest level with a spell per day left) with no globe on the field.
    /// </summary>
    private static void TestCandidateCapKeepsHighLevels()
    {
        CharacterController sorc = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 10, 10, 16, new List<string>
                {
                    SpellNames.DAZE, SpellNames.FLARE, SpellNames.ACID_SPLASH, SpellNames.RAY_OF_FROST,
                    SpellNames.MAGIC_MISSILE, SpellNames.SLEEP, SpellNames.COLOR_SPRAY, SpellNames.BURNING_HANDS,
                    SpellNames.FIREBALL
                }, CharacterTeam.Enemy, "Empower Spell", "Heighten Spell");
            SpontaneousCastingData data = sorc.Spellcasting.SpontaneousData;
            // Exactly the listed spells are known, whatever the class's starting picks.
            for (int l = 0; l < data.SpellsKnownByLevel.Length; l++)
                data.SpellsKnownByLevel[l]?.Clear();
            data.LearnSpell(SpellNames.DAZE, 0); data.LearnSpell(SpellNames.FLARE, 0); data.LearnSpell(SpellNames.ACID_SPLASH, 0);
            data.LearnSpell(SpellNames.RAY_OF_FROST, 0); data.LearnSpell(SpellNames.MAGIC_MISSILE, 1); data.LearnSpell(SpellNames.SLEEP, 1);
            data.LearnSpell(SpellNames.COLOR_SPRAY, 1); data.LearnSpell(SpellNames.BURNING_HANDS, 1); data.LearnSpell(SpellNames.FIREBALL, 3);
            List<SpellData> candidates = AiMetamagicPlanner.SpontaneousCandidates(sorc, new List<CharacterController> { sorc });
            Assert(FindCandidate(candidates, SpellNames.FIREBALL, MetamagicFeatId.EmpowerSpell) != null,
                "With many low-level spells known, Empowered Fireball is still a candidate (the walk starts at the highest level)",
                $"count {candidates.Count}");
            int heightenedSleeps = 0;
            foreach (SpellData c in candidates)
            {
                MetamagicData m = SpellCastRequest.AiMetamagicFor(c);
                if (c.SpellId == SpellNames.SLEEP && m != null && m.Has(MetamagicFeatId.HeightenSpell))
                    heightenedSleeps++;
            }
            Assert(heightenedSleeps <= 1, "Heighten gives at most one option per spell with no globe on the field", $"{heightenedSleeps} heightened Sleep");
        }
        finally
        {
            Cleanup(sorc);
        }
    }

    /// <summary>
    /// An Enlarged candidate's range is the range its cast has (AiMetamagicPlanner.RangeSquaresFor, read by the AI's
    /// target prefilters, batch 11 step 28 review): the cast's clone with Enlarge applied, as the cast core measures it.
    /// A custom range doubles (PHB p.94); a close, medium or long range does not yet, for the AI as for the cast (SPL-010),
    /// so the AI makes no Enlarged candidate of such a spell (CouldMatter).
    /// </summary>
    private static void TestEnlargedRange()
    {
        CharacterController sorc = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 10, 10, 16, new List<string> { SpellNames.MAGIC_MISSILE }, CharacterTeam.Enemy, "Enlarge Spell");
            MetamagicData enlarge = Feat(MetamagicFeatId.EnlargeSpell);
            // A clone with a custom 7-square range (not a database template).
            SpellData custom = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
            custom.RangeCategory = SpellRangeCategory.Custom;
            custom.RangeSquares = 7;
            custom.RangeIncreaseSquares = 0;
            custom.RangeIncreasePerLevels = 0;
            int plainRange = CasterLevelRules.RangeSquares(sorc, custom);
            Assert(plainRange == 7 && AiMetamagicPlanner.RangeSquaresFor(sorc, custom) == 7, "The plain spell's range is its own", $"{plainRange}");
            Assert(AiMetamagicPlanner.RangeSquaresFor(sorc, AiMetamagicPlanner.CandidateFor(custom, enlarge)) == 14,
                "An Enlarged spell of custom range reaches twice as far (PHB p.94)", $"{AiMetamagicPlanner.RangeSquaresFor(sorc, AiMetamagicPlanner.CandidateFor(custom, enlarge))}");
            SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
            int medium = CasterLevelRules.RangeSquares(sorc, missile);
            Assert(AiMetamagicPlanner.RangeSquaresFor(sorc, AiMetamagicPlanner.CandidateFor(missile, enlarge)) == medium,
                "An Enlarged Magic Missile (medium range) has the range its cast has: not doubled yet (SPL-010)");
            Assert(!AiMetamagicPlanner.CouldMatter(MetamagicFeatId.EnlargeSpell, sorc, missile, enlarge, new List<CharacterController> { sorc }),
                "So Enlarge is no candidate on Magic Missile");
        }
        finally
        {
            Cleanup(sorc);
        }
    }

    /// <summary>
    /// PHB p.100 (Silent Spell, Special): a bard spell cannot be enhanced by Silent Spell; a sorcerer's can
    /// (MetamagicSystem.ValidateMetamagicApplication, the shared rule; batch 11 step 28 review, SPL-184 item 1 in part).
    /// </summary>
    private static void TestBardSilentSpell()
    {
        CharacterController bard = null, sorc = null;
        try
        {
            bard = CreateCaster("Bard", 7, 10, 16, new List<string> { SpellNames.HOLD_PERSON }, CharacterTeam.Enemy, "Silent Spell");
            sorc = CreateCaster("Sorcerer", 7, 10, 16, new List<string> { SpellNames.HOLD_PERSON }, CharacterTeam.Enemy, "Silent Spell");
            SpellData hold = SpellDatabase.GetSpell(SpellNames.HOLD_PERSON);
            MetamagicData silent = Feat(MetamagicFeatId.SilentSpell);
            CastSource bardSource = SpellCastRequest.AiSourceFor(bard, hold);
            CastSource sorcSource = SpellCastRequest.AiSourceFor(sorc, hold);
            Assert(bardSource.IsSpontaneous && sorcSource.IsSpontaneous, "Both cast from spontaneous spells per day");
            Assert(MetamagicSystem.ValidateMetamagicApplication(hold, silent, bard.Stats, bardSource) == MetamagicSystem.RefusalNotApplicable,
                "A bard's Silent Hold Person is refused (PHB p.100 Special)");
            Assert(MetamagicSystem.ValidateMetamagicApplication(hold, silent, sorc.Stats, sorcSource) == null,
                "A sorcerer's Silent Hold Person is allowed");
            Assert(!AiMetamagicPlanner.SpontaneousCandidates(bard, new List<CharacterController> { bard })
                    .Exists(c => SpellCastRequest.AiMetamagicFor(c) != null && SpellCastRequest.AiMetamagicFor(c).Has(MetamagicFeatId.SilentSpell)),
                "The AI bard has no Silent candidate");
        }
        finally
        {
            Cleanup(bard, sorc);
        }
    }

    private static void TestEmpoweredFireballRanking()
    {
        CharacterController sorc = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 10, 10, 16, new List<string> { SpellNames.FIREBALL }, CharacterTeam.Enemy, "Empower Spell");
            SpellData plain = SpellDatabase.GetSpell(SpellNames.FIREBALL);
            SpellData empowered = FindCandidate(AiMetamagicPlanner.SpontaneousCandidates(sorc, null), SpellNames.FIREBALL, MetamagicFeatId.EmpowerSpell);
            if (empowered == null)
            {
                Assert(false, "Empowered Fireball is a candidate (ranking)");
                return;
            }
            var all = new List<CharacterController> { sorc };
            var threeFoes = new AreaPlacement { EnemiesHit = 3 };
            var oneFoe = new AreaPlacement { EnemiesHit = 1 };

            float basePlain = AISpellcastingStrategist.ScoreSpellComprehensive(plain, sorc, null, all, null);
            float baseEmpowered = AISpellcastingStrategist.ScoreSpellComprehensive(empowered, sorc, null, all, null);
            Assert(Near(basePlain, baseEmpowered), "The strategist scores both the same before the metamagic value",
                $"{basePlain} vs {baseEmpowered}");
            Assert(Near(AiMetamagicPlanner.Value(sorc, plain, null, all, threeFoes), 0f), "Plain Fireball carries no metamagic value");

            float three = AiMetamagicPlanner.Value(sorc, empowered, null, all, threeFoes);
            float one = AiMetamagicPlanner.Value(sorc, empowered, null, all, oneFoe);
            float costs = AiMetamagicPlanner.SlotLevelCost * 2 + AiMetamagicPlanner.FullRoundCost;
            Assert(Near(three, AiMetamagicPlanner.EffectPointValue * 17.5f * 3 - costs),
                "Empowered Fireball at caster level 10: 17.5 more on each of three foes, less a 5th-level spell per day and the full round", $"value {three}");
            Assert(baseEmpowered + three > basePlain, "Empowered Fireball outranks plain Fireball when its burst takes three foes",
                $"{baseEmpowered + three} vs {basePlain}");
            Assert(baseEmpowered + one < basePlain, "Plain Fireball stays first against one foe: 17.5 more does not pay for the full round and the higher spell",
                $"{baseEmpowered + one} vs {basePlain}");
            Assert(one + AiMetamagicPlanner.FullRoundCost > 0f, "Without the full-round cost the one-foe empower would pay: the full round decides it");
        }
        finally
        {
            Cleanup(sorc);
        }
    }

    private static void TestFinishingBlow()
    {
        CharacterController sorc = null, weak = null, tough = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 10, 10, 16, new List<string> { SpellNames.MAGIC_MISSILE }, CharacterTeam.Enemy, "Empower Spell");
            weak = CreateFoe("Weak", 20);
            tough = CreateFoe("Tough", 100);
            SpellData empowered = FindCandidate(AiMetamagicPlanner.SpontaneousCandidates(sorc, null), SpellNames.MAGIC_MISSILE, MetamagicFeatId.EmpowerSpell);
            if (empowered == null)
            {
                Assert(false, "Empowered Magic Missile is a candidate (finishing blow)");
                return;
            }
            float plainAvg = AiMetamagicPlanner.ExpectedEffect(sorc, SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE), null);
            float empAvg = AiMetamagicPlanner.ExpectedEffect(sorc, empowered, SpellCastRequest.AiMetamagicFor(empowered));
            Assert(Near(plainAvg, 17.5f) && Near(empAvg, 26.25f), "Magic Missile at caster level 10: five missiles of 1d4+1, 17.5 plain and 26.25 empowered",
                $"{plainAvg} / {empAvg}");
            var all = new List<CharacterController> { sorc, weak, tough };
            Assert(AiMetamagicPlanner.Value(sorc, empowered, weak, all, null) > 0f, "Empowered Magic Missile is worth it when only its average drops the foe (20 hit points)");
            Assert(AiMetamagicPlanner.Value(sorc, empowered, tough, all, null) < 0f, "Against a 100-hit-point foe the plain Magic Missile stays first");
        }
        finally
        {
            Cleanup(sorc, weak, tough);
        }
    }

    private static void TestExtendHeightenSilent()
    {
        CharacterController sorc = null;
        try
        {
            sorc = CreateCaster("Sorcerer", 5, 10, 16, new List<string> { SpellNames.HOLD_PERSON, SpellNames.MAGIC_MISSILE }, CharacterTeam.Enemy,
                "Extend Spell", "Heighten Spell", "Silent Spell");
            SpellData hold = SpellDatabase.GetSpell(SpellNames.HOLD_PERSON);
            SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
            Assert(AiMetamagicPlanner.CouldMatter(MetamagicFeatId.ExtendSpell, sorc, hold, Feat(MetamagicFeatId.ExtendSpell), null),
                "Extend could matter on Hold Person at caster level 5 (5 rounds, within the fight)");
            SpellData extended = AiMetamagicPlanner.CandidateFor(hold, Feat(MetamagicFeatId.ExtendSpell));
            float value = AiMetamagicPlanner.Value(sorc, extended, null, null, null);
            float costs = AiMetamagicPlanner.SlotLevelCost * 1 + AiMetamagicPlanner.FullRoundCost;
            Assert(Near(value + costs, AiMetamagicPlanner.ExtendRoundValue * 5), "Extend adds 5 rounds (10 against 5) within the 10-round horizon",
                $"value {value}");
            Assert(AiMetamagicPlanner.CouldMatter(MetamagicFeatId.HeightenSpell, sorc, hold, null, null),
                "Heighten could matter on Hold Person (a Will save against a foe)");
            Assert(!AiMetamagicPlanner.CouldMatter(MetamagicFeatId.HeightenSpell, sorc, missile, null, null),
                "Heighten does not matter on Magic Missile (no save) with no globe on the field");
            Assert(!AiMetamagicPlanner.CouldMatter(MetamagicFeatId.SilentSpell, sorc, missile, null, null),
                "Silent does not matter outside a Silence");
            var heighten = new MetamagicData { HeightenToLevel = 3 };
            heighten.Toggle(MetamagicFeatId.HeightenSpell);
            float heightened = AiMetamagicPlanner.Value(sorc, AiMetamagicPlanner.CandidateFor(hold, heighten), null, null, null);
            Assert(Near(heightened, AiMetamagicPlanner.HeightenDcValue * 1 - AiMetamagicPlanner.SlotLevelCost * 1 - AiMetamagicPlanner.FullRoundCost),
                "Hold Person heightened to 3rd: +1 save DC on its foe, less a 3rd-level spell per day and the full round", $"value {heightened}");
        }
        finally
        {
            Cleanup(sorc);
        }
    }

    private static void TestPreparedQuicken()
    {
        CharacterController wizard = null;
        try
        {
            wizard = CreateCaster("Wizard", 9, 16, 10, null, CharacterTeam.Enemy, "Quicken Spell");
            SpellData slotSpell = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
            slotSpell.MetamagicDataRef = Feat(MetamagicFeatId.QuickenSpell);
            slotSpell.IsPreparedMetamagicSlotSpell = true; // a prepared Quickened slot's spell, as GetCastablePreparedSpells lists it
            Assert(!SpellCastRequest.AiSourceFor(wizard, slotSpell).IsSpontaneous, "The wizard's cast is from a prepared slot");
            Assert(Near(AiMetamagicPlanner.Value(wizard, slotSpell, null, null, null), AiMetamagicPlanner.QuickenValue),
                "A prepared Quickened Magic Missile is worth the action it leaves and pays nothing at the cast (PHB p.88, p.98)");
            Assert(AiMetamagicPlanner.SpontaneousCandidates(wizard, null).Count == 0, "A prepared caster has no spontaneous metamagic candidate");
        }
        finally
        {
            Cleanup(wizard);
        }
    }

    private static void TestSpellResistanceChance()
    {
        Assert(Near(AISpellcastingStrategist.SpellResistanceSuccessChance(5, 15), 0.55f), "AI-046: caster level 5 beats SR 15 on 10 or more, 55% (PHB p.177)");
        Assert(Near(AISpellcastingStrategist.SpellResistanceSuccessChance(5, 26), 0f), "AI-046: SR 26 is out of reach of caster level 5");
        Assert(Near(AISpellcastingStrategist.SpellResistanceSuccessChance(5, 25), 0.05f), "AI-046: SR 25 needs a natural 20 for caster level 5");
        Assert(Near(AISpellcastingStrategist.SpellResistanceSuccessChance(10, 11), 1f), "AI-046: caster level 10 always beats SR 11");
        Assert(Near(AISpellcastingStrategist.SpellResistanceSuccessChance(5, 0), 1f), "No spell resistance: always through");
    }
}
}
