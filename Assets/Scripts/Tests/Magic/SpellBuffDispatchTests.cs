#if UNITY_EDITOR
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// SPL-037: the spell-specific branches of <c>GameManager.ApplySpellBuff</c> that sat below its generic
/// StatusEffectManager branch now run, and the spells whose effect type kept them out of the method reach it through
/// <see cref="SpellEffectRouting"/> on the PC and NPC pipelines. Each spell is applied through
/// <c>GameManager.Harness_ApplySpellBuff</c> (the method itself, with an optional stand-in for the cast's
/// SpellResult) and checked against the PHB (owner's copy, printed pages): Barkskin p.203, Magic Fang p.250,
/// Levitate p.248, Continual Flame p.213, Dancing Lights p.216, Shrink Item p.279, Ghost Sound p.235, Message p.253,
/// Prestidigitation p.264, Lullaby p.249, True Seeing p.296, Mislead p.254, Protection from Spells p.266, Spell Turning
/// p.282, Globe of Invulnerability p.236, Heal p.239 (harm p.239), Disintegrate p.222, Plane Shift p.262, Resurrection
/// p.272. Alter Self (p.197) keeps its +2 Str pending the owner (SPL-037). Telekinesis's push and the globe's blocking need the
/// grid and a frame: rules scenario rules/spell-buff-dispatch.
/// Needs Play mode and the scene GameManager (editor only: the harness entry point is editor-only).
/// </summary>
public static class SpellBuffDispatchTests
{
    private static int _passed;
    private static int _failed;
    private static readonly List<Object> _created = new List<Object>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        Debug.Log("====== SPELL BUFF DISPATCH TESTS (SPL-037) ======");

        if (GameManager.Instance == null)
        {
            Assert(false, "A scene GameManager exists (run in Play mode)");
            Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
            return;
        }

        Run(TestRoutingTable);
        Run(TestGenericBranchWithoutStatusEffectManager);
        Run(TestBarkskin);
        Run(TestMagicFang);
        Run(TestTrackedUtilityAndBuffSpells);
        Run(TestCantrips);
        Run(TestLullabySleepPenalty);
        Run(TestTrueSeeingAndMislead);
        Run(TestProtectionFromSpellsAndSpellTurning);
        Run(TestGlobeOfInvulnerability);
        Run(TestHealLiving);
        Run(TestHealUndeadAndConstruct);
        Run(TestHealCastHealsNothingItself);
        Run(TestDisintegrate);
        Run(TestPlaneShift);
        Run(TestResurrection);
        Run(TestReclassifiedBranches);
        Run(TestDispelMagicChecksEverySpell);
        Run(TestAreaDispelStopsAtFirstSuccess);
        Run(TestBreakEnchantment);
        Run(TestBarkskinThroughAddEffect);
        Run(TestDisintegrateMetamagic);
        Run(TestGlobeEndsWithItsEffect);
        Run(TestRestorationDispelsAbilityPenalties);

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Run(System.Action test)
    {
        try
        {
            test();
        }
        finally
        {
            ScenarioHooks.RollFilter = null;
            foreach (Object o in _created)
                if (o != null)
                    Object.DestroyImmediate(o);
            _created.Clear();
        }
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

    // ── helpers ──

    private static CharacterController Actor(string name, string cls, int level, int x, int intelligence = 10, int wis = 10)
    {
        CharacterController c = TestHelpers.CreateCharacter(name: name, characterClass: cls, level: level,
            intelligence: intelligence, wis: wis, gridPosition: new Vector2Int(x, 0));
        _created.Add(c.gameObject);
        return c;
    }

    private static ActiveSpellEffect Apply(CharacterController caster, CharacterController target, string spellId, SpellResult castResult = null)
    {
        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null)
            return null;
        return GameManager.Instance.Harness_ApplySpellBuff(caster, target, spell.Clone(), castResult);
    }

    /// <summary>The actor's StatusEffectManager, added if the test character has none, bound to its stats.</summary>
    private static StatusEffectManager Effects(CharacterController c)
    {
        StatusEffectManager effects = c.StatusEffectManager;
        if (effects == null)
            effects = c.gameObject.AddComponent<StatusEffectManager>();
        effects.Init(c.Stats);
        return effects;
    }

    private static bool HasEffect(CharacterController c, string spellId)
        => c.StatusEffectManager != null && c.StatusEffectManager.HasEffect(spellId);

    private static SpellResult LandedSave(bool saved)
        => new SpellResult { Success = true, AttackHit = true, RequiredSave = true, SaveSucceeded = saved, SaveDC = 20, SaveTotal = saved ? 25 : 2 };

    private static SpellResult LandedNoSave()
        => new SpellResult { Success = true, AttackHit = true };

    // ── tests ──

    private static void TestRoutingTable()
    {
        foreach (string id in SpellEffectRouting.HandlerOwnedSpellIdsForTests)
        {
            SpellData spell = SpellDatabase.GetSpell(id);
            Assert(spell != null, $"Routing: {id} is a registered spell");
            if (spell == null) continue;
            Assert(!SpellEffectRouting.IsTrackedEffectType(spell.EffectType),
                $"Routing: {id} is listed only because its effect type ({spell.EffectType}) would keep it out");
            Assert(SpellEffectRouting.ReachesApplySpellBuff(spell), $"Routing: {id} reaches ApplySpellBuff");
        }

        Assert(SpellEffectRouting.ReachesApplySpellBuff(SpellDatabase.GetSpell(SpellNames.BARKSKIN)), "Routing: a Buff (Barkskin) reaches ApplySpellBuff");
        Assert(SpellEffectRouting.ReachesApplySpellBuff(SpellDatabase.GetSpell(SpellNames.TELEKINESIS)), "Routing: a Control spell (Telekinesis) reaches ApplySpellBuff");
        Assert(!SpellEffectRouting.ReachesApplySpellBuff(SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS)), "Routing: Cure Light Wounds (Healing, no branch) does not");
        Assert(!SpellEffectRouting.ReachesApplySpellBuff(SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE)), "Routing: Magic Missile (Damage, no branch) does not");

        SpellData heal = SpellDatabase.GetSpell(SpellNames.HEAL);
        Assert(heal.HealingResolvedByHandler && heal.DamageResolvedByHandler && heal.Energy == SpellEnergy.Positive,
            "Heal: its handler heals and harms; positive energy (PHB p.239)");
        Assert(SpellDatabase.GetSpell(SpellNames.RESURRECTION).HealingResolvedByHandler, "Resurrection: its handler restores the hit points");
        Assert(SpellDatabase.GetSpell(SpellNames.DISINTEGRATE).DamageResolvedByHandler, "Disintegrate: its handler deals the damage");
        Assert(!SpellDatabase.GetSpell(SpellNames.TELEKINESIS).AllowsSavingThrow, "Telekinesis (combat maneuver): no save, PHB p.292");
    }

    private static void TestGenericBranchWithoutStatusEffectManager()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 3, 1);
        CharacterController ally = Actor("Ally", "Fighter", 3, 2);
        if (ally.StatusEffectManager != null)
            Object.DestroyImmediate(ally.StatusEffectManager);

        ActiveSpellEffect effect = Apply(cleric, ally, SpellNames.BLESS);
        Assert(effect != null && HasEffect(ally, SpellNames.BLESS),
            "Generic branch: a target with no StatusEffectManager gets one and the tracked effect (the legacy fallback is gone)");
    }

    private static void TestBarkskin()
    {
        Assert(GameManager.BarkskinNaturalArmorBonus(1) == 2 && GameManager.BarkskinNaturalArmorBonus(5) == 2,
            "Barkskin: +2 at caster levels 1-5 (PHB p.203)");
        Assert(GameManager.BarkskinNaturalArmorBonus(6) == 3 && GameManager.BarkskinNaturalArmorBonus(9) == 4
            && GameManager.BarkskinNaturalArmorBonus(12) == 5 && GameManager.BarkskinNaturalArmorBonus(20) == 5,
            "Barkskin: +1 per three levels above 3rd, max +5 at 12th");

        CharacterController druid = Actor("Druid", "Druid", 3, 1, wis: 16);
        CharacterController fighter = Actor("Armored", "Fighter", 3, 2);
        fighter.Stats.ArmorBonus = 5; // breastplate
        int acBefore = fighter.Stats.ArmorClass;
        int spellArmorBefore = fighter.Stats.SpellACBonus;
        int naturalBefore = fighter.Stats.NaturalArmorBonus;
        int cl = druid.Stats.GetDomainBoostedCasterLevel(SpellDatabase.GetSpell(SpellNames.BARKSKIN));
        int expected = GameManager.BarkskinNaturalArmorBonus(cl);

        ActiveSpellEffect effect = Apply(druid, fighter, SpellNames.BARKSKIN);
        Assert(effect != null, "Barkskin: the branch adds a tracked effect");
        Assert(fighter.Stats.ArmorClass == acBefore + expected,
            $"Barkskin: an armored creature gains +{expected} AC (CL {cl}); natural armor stacks with armor", $"AC {acBefore} -> {fighter.Stats.ArmorClass}");
        Assert(fighter.Stats.SpellACBonus == spellArmorBefore, "Barkskin: not an armor bonus (SpellACBonus unchanged)");
        Assert(fighter.Stats.NaturalArmorBonus == naturalBefore, "Barkskin: the creature's own natural armor score is not changed");
        Assert(fighter.Stats.SpellNaturalArmorEnhancementBonus == expected, "Barkskin: the enhancement bonus to natural armor is tracked");

        fighter.Stats.WondrousNaturalArmorBonus = 3; // amulet of natural armor +3
        Assert(fighter.Stats.ArmorClass == acBefore + 3,
            "Barkskin: does not stack with an amulet's enhancement bonus to natural armor; the higher (+3) applies", $"AC {fighter.Stats.ArmorClass}");
        fighter.Stats.WondrousNaturalArmorBonus = 0;

        fighter.StatusEffectManager.RemoveEffectsBySpellId(SpellNames.BARKSKIN);
        Assert(fighter.Stats.ArmorClass == acBefore && fighter.Stats.SpellNaturalArmorEnhancementBonus == 0,
            "Barkskin: the bonus ends with the spell", $"AC {fighter.Stats.ArmorClass}");

        CharacterController druid12 = Actor("Druid12", "Druid", 12, 3, wis: 18);
        CharacterController ogre = Actor("Hide", "Fighter", 3, 4);
        ogre.Stats.NaturalArmorBonus = 5;
        int ogreBefore = ogre.Stats.ArmorClass;
        int cl12 = druid12.Stats.GetDomainBoostedCasterLevel(SpellDatabase.GetSpell(SpellNames.BARKSKIN));
        Apply(druid12, ogre, SpellNames.BARKSKIN);
        Assert(ogre.Stats.ArmorClass == ogreBefore + GameManager.BarkskinNaturalArmorBonus(cl12),
            $"Barkskin: stacks with the creature's natural armor (+{GameManager.BarkskinNaturalArmorBonus(cl12)} at CL {cl12})", $"AC {ogreBefore} -> {ogre.Stats.ArmorClass}");
    }

    private static void TestMagicFang()
    {
        CharacterController druid = Actor("Druid", "Druid", 3, 1, wis: 14);
        CharacterController wolf = Actor("Wolf", "Fighter", 2, 2);
        int attackBefore = wolf.Stats.EffectAttackBonus;
        int damageBefore = wolf.Stats.EffectWeaponDamageBonus;

        ActiveSpellEffect effect = Apply(druid, wolf, SpellNames.MAGIC_FANG);
        Assert(effect != null && HasEffect(wolf, SpellNames.MAGIC_FANG), "Magic Fang: tracked effect");
        Assert(wolf.Stats.EffectAttackBonus == attackBefore + 1 && wolf.Stats.EffectWeaponDamageBonus == damageBefore + 1,
            "Magic Fang: +1 on attack and damage once (PHB p.250; the branch used to add a second +1)",
            $"attack {attackBefore} -> {wolf.Stats.EffectAttackBonus}, damage {damageBefore} -> {wolf.Stats.EffectWeaponDamageBonus}");

        wolf.StatusEffectManager.RemoveEffectsBySpellId(SpellNames.MAGIC_FANG);
        Assert(wolf.Stats.EffectAttackBonus == attackBefore && wolf.Stats.EffectWeaponDamageBonus == damageBefore,
            "Magic Fang: the bonus ends with the spell");
    }

    private static void TestTrackedUtilityAndBuffSpells()
    {
        CharacterController wizard = Actor("Wizard", "Wizard", 5, 1, intelligence: 16);
        CharacterController ally = Actor("Ally", "Fighter", 3, 2);
        CharacterController cleric = Actor("Cleric", "Cleric", 5, 3, wis: 16);

        Assert(Apply(wizard, ally, SpellNames.LEVITATE) != null && HasEffect(ally, SpellNames.LEVITATE),
            "Levitate: tracked effect on the subject (1 min./level; no vertical movement on the grid)");

        ActiveSpellEffect flame = Apply(cleric, ally, SpellNames.CONTINUAL_FLAME);
        Assert(flame != null && HasEffect(ally, SpellNames.CONTINUAL_FLAME), "Continual Flame: tracked effect");

        Apply(wizard, wizard, SpellNames.DANCING_LIGHTS);
        Assert(HasEffect(wizard, SpellNames.DANCING_LIGHTS), "Dancing Lights: tracked effect on the caster (1 minute)");

        Assert(Apply(wizard, ally, SpellNames.SHRINK_ITEM) == null && !HasEffect(ally, SpellNames.SHRINK_ITEM),
            "Shrink Item: affects an item, not the creature (log only)");

        Assert(Apply(wizard, wizard, SpellNames.ALTER_SELF) != null && HasEffect(wizard, SpellNames.ALTER_SELF),
            "Alter Self: tracked effect (its +2 Str awaits the owner, SPL-037)");
    }

    private static void TestCantrips()
    {
        CharacterController bard = Actor("Bard", "Bard", 3, 1);
        CharacterController enemy = Actor("Enemy", "Fighter", 3, 2);

        foreach (string id in new[] { SpellNames.GHOST_SOUND, SpellNames.MESSAGE, SpellNames.PRESTIDIGITATION })
        {
            Apply(bard, bard, id);
            Assert(HasEffect(bard, id), $"{id}: the cantrip's duration is tracked on the caster");
        }

        foreach (string id in new[] { SpellNames.CREATE_WATER, SpellNames.MENDING, SpellNames.PURIFY_FOOD_DRINK, SpellNames.KNOW_DIRECTION })
        {
            Assert(Apply(bard, bard, id) == null && !HasEffect(bard, id), $"{id}: instantaneous, no tracked effect");
        }

        Assert(Apply(bard, enemy, SpellNames.LULLABY) != null && HasEffect(enemy, SpellNames.LULLABY),
            "Lullaby: tracked effect on the creature that failed its save (concentration)");
    }

    private static void TestLullabySleepPenalty()
    {
        CharacterController bard = Actor("Bard", "Bard", 3, 1);
        CharacterController enemy = Actor("Enemy", "Fighter", 3, 2);
        SpellData sleep = SpellDatabase.GetSpell(SpellNames.SLEEP);
        SpellData deepSlumber = SpellDatabase.GetSpell(SpellNames.DEEP_SLUMBER);
        SpellData hold = SpellDatabase.GetSpell(SpellNames.HOLD_PERSON);

        Assert(SpellCaster.LullabySleepSavePenalty(enemy, sleep) == 0, "Lullaby: no penalty without the spell");
        Apply(bard, enemy, SpellNames.LULLABY);
        Assert(SpellCaster.LullabySleepSavePenalty(enemy, sleep) == -2 && SpellCaster.LullabySleepSavePenalty(enemy, deepSlumber) == -2,
            "Lullaby: -2 on Will saves against sleep effects (PHB p.249)");
        Assert(SpellCaster.LullabySleepSavePenalty(enemy, hold) == 0, "Lullaby: no penalty against other enchantments");
    }

    private static void TestTrueSeeingAndMislead()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 10, 1, wis: 18);
        CharacterController ally = Actor("Ally", "Fighter", 5, 2);
        CharacterController illusionist = Actor("Illusionist", "Wizard", 12, 3, intelligence: 18);

        Apply(cleric, ally, SpellNames.TRUE_SEEING);
        Assert(HasEffect(ally, SpellNames.TRUE_SEEING) && ally.HasActiveSeeInvisibilityEffect,
            "True Seeing: tracked effect and the subject sees invisible creatures");

        Apply(illusionist, illusionist, SpellNames.MISLEAD);
        Assert(illusionist.HasActiveInvisibilityEffect && HasEffect(illusionist, SpellNames.MISLEAD),
            "Mislead: the caster becomes invisible (greater invisibility)");
    }

    private static void TestProtectionFromSpellsAndSpellTurning()
    {
        CharacterController wizard = Actor("Wizard", "Wizard", 16, 1, intelligence: 18);
        CharacterController ally = Actor("Ally", "Fighter", 5, 2);

        ActiveSpellEffect ward = Apply(wizard, ally, SpellNames.PROTECTION_FROM_SPELLS);
        Assert(ward != null && ward.AppliedSaveBonus == 8, "Protection from Spells: +8 save bonus tracked and returned");

        Apply(wizard, wizard, SpellNames.SPELL_TURNING);
        ActiveSpellEffect turning = null;
        foreach (ActiveSpellEffect e in wizard.StatusEffectManager.ActiveEffects)
            if (e.Spell != null && e.Spell.SpellId == SpellNames.SPELL_TURNING)
                turning = e;
        int levels = 0;
        bool parsed = turning != null && turning.CustomTag != null && turning.CustomTag.StartsWith("SpellTurning:")
            && int.TryParse(turning.CustomTag.Substring("SpellTurning:".Length), out levels);
        Assert(parsed && levels >= 7 && levels <= 10, "Spell Turning: 1d4+6 spell levels stored on the tracked effect (PHB p.282)", turning?.CustomTag);
    }

    private static void TestGlobeOfInvulnerability()
    {
        CharacterController wizard = Actor("Wizard", "Wizard", 12, 1, intelligence: 18);
        int before = AreaEffectManager.HasInstance
            ? AreaEffectManager.Instance.GetEffectsOfType<LesserGlobeOfInvulnerabilityAreaEffect>().Count : 0;

        Apply(wizard, wizard, SpellNames.GLOBE_OF_INVULNERABILITY);
        LesserGlobeOfInvulnerabilityAreaEffect globe = null;
        if (AreaEffectManager.HasInstance)
            foreach (var g in AreaEffectManager.Instance.GetEffectsOfType<LesserGlobeOfInvulnerabilityAreaEffect>())
                if (g != null && g.SpellId == SpellNames.GLOBE_OF_INVULNERABILITY && g.Caster == wizard)
                    globe = g;
        Assert(globe != null && globe.MaxBlockedSpellLevel == 4,
            "Globe of Invulnerability: an emanation that blocks spells of 4th level and lower (PHB p.236)", $"globes before {before}");

        Assert(HasEffect(wizard, SpellNames.GLOBE_OF_INVULNERABILITY), "Globe of Invulnerability: a tracked effect on the caster for its duration");
        int trackedRounds = wizard.StatusEffectManager.GetRemainingRounds(SpellNames.GLOBE_OF_INVULNERABILITY);
        Assert(globe != null && globe.RoundsRemaining == trackedRounds,
            "Globe of Invulnerability: the emanation lasts exactly as long as the tracked effect", $"area {globe?.RoundsRemaining}, effect {trackedRounds}");

        // The globe's squares are computed in its Start (next frame), so blocking a 4th-level spell is checked by the
        // rules scenario rules/spell-buff-dispatch.
        if (globe != null)
        {
            AreaEffectManager.Instance.UnregisterAreaEffect(globe);
            _created.Add(globe.gameObject);
        }
    }

    private static List<LesserGlobeOfInvulnerabilityAreaEffect> GlobesOf(CharacterController caster)
    {
        var list = new List<LesserGlobeOfInvulnerabilityAreaEffect>();
        if (AreaEffectManager.HasInstance)
            foreach (var g in AreaEffectManager.Instance.GetEffectsOfType<LesserGlobeOfInvulnerabilityAreaEffect>())
                if (g != null && g.SpellId == SpellNames.GLOBE_OF_INVULNERABILITY && g.Caster == caster)
                    list.Add(g);
        return list;
    }

    /// <summary>
    /// The emanation and the caster's tracked effect are one spell: a recast while it is up adds no second globe, and
    /// dispelling (or the expiry of) the tracked effect ends the emanation (PHB p.223, p.236).
    /// </summary>
    private static void TestGlobeEndsWithItsEffect()
    {
        CharacterController wizard = Actor("Wizard", "Wizard", 12, 1, intelligence: 18);
        Apply(wizard, wizard, SpellNames.GLOBE_OF_INVULNERABILITY);
        List<LesserGlobeOfInvulnerabilityAreaEffect> globes = GlobesOf(wizard);
        foreach (var g in globes)
            _created.Add(g.gameObject);
        Assert(globes.Count == 1, "Globe of Invulnerability setup: one emanation", $"{globes.Count}");

        Assert(Apply(wizard, wizard, SpellNames.GLOBE_OF_INVULNERABILITY) == null && GlobesOf(wizard).Count == 1,
            "Globe of Invulnerability: a recast while an equally long globe is up adds no second emanation", $"{GlobesOf(wizard).Count}");

        wizard.StatusEffectManager.RemoveEffectsBySpellId(SpellNames.GLOBE_OF_INVULNERABILITY);
        Assert(GlobesOf(wizard).Count == 0,
            "Globe of Invulnerability: dispelling its tracked effect ends the emanation, so 4th-level spells get through again", $"{GlobesOf(wizard).Count}");
    }

    /// <summary>
    /// Targeted dispel (PHB p.223): one dispel check per ongoing spell, each success dispels its spell. Cleric 9
    /// (+9) with the d20 forced to 10 (total 19): the two CL 5 spells (DC 16) go, the CL 15 one (DC 26) stays.
    /// </summary>
    private static void TestDispelMagicChecksEverySpell()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 9, 1, wis: 18);
        CharacterController enemy = Actor("Enemy", "Fighter", 5, 2);
        StatusEffectManager effects = Effects(enemy);
        bool setup = effects.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Other", 5) != null;
        setup &= effects.AddEffect(SpellDatabase.GetSpell(SpellNames.MAGE_ARMOR).Clone(), "Other", 5) != null;
        setup &= effects.AddEffect(SpellDatabase.GetSpell(SpellNames.SHIELD).Clone(), "Other", 15) != null;
        Assert(setup, "Targeted dispel setup: Bless and Mage Armor at CL 5, Shield at CL 15");

        ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == DispelMagicService.DispelCheckContext ? 10 : natural;
        Apply(cleric, enemy, SpellNames.DISPEL_MAGIC, LandedNoSave());
        ScenarioHooks.RollFilter = null;

        Assert(!HasEffect(enemy, SpellNames.BLESS) && !HasEffect(enemy, SpellNames.MAGE_ARMOR),
            "Dispel Magic (targeted): every spell whose check succeeds is dispelled, not just the first (PHB p.223)");
        Assert(HasEffect(enemy, SpellNames.SHIELD), "Dispel Magic (targeted): a spell whose own check fails stays (19 vs DC 26)");
    }

    /// <summary>Area dispel (PHB p.223): checks from the highest caster level down until one spell is dispelled.</summary>
    private static void TestAreaDispelStopsAtFirstSuccess()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 9, 1, wis: 18);
        CharacterController enemy = Actor("Enemy", "Fighter", 5, 2);
        StatusEffectManager effects = Effects(enemy);
        effects.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Other", 5);
        effects.AddEffect(SpellDatabase.GetSpell(SpellNames.MAGE_ARMOR).Clone(), "Other", 5);
        effects.AddEffect(SpellDatabase.GetSpell(SpellNames.SHIELD).Clone(), "Other", 15);
        int before = effects.ActiveEffects.Count;

        ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == DispelMagicService.DispelCheckContext ? 10 : natural;
        GameManager.Instance.PerformAreaDispel(cleric, new List<CharacterController> { enemy }, 9);
        ScenarioHooks.RollFilter = null;

        Assert(effects.ActiveEffects.Count == before - 1 && HasEffect(enemy, SpellNames.SHIELD),
            "Dispel Magic (area): fails against the CL 15 Shield, then dispels one CL 5 spell and stops", $"{before} -> {effects.ActiveEffects.Count}");
    }

    /// <summary>
    /// Break Enchantment (PHB p.207): a check (1d20 + CL, max +15) against each enchantment, transmutation or curse
    /// that victimizes the subject; its beneficial ones stay.
    /// </summary>
    private static void TestBreakEnchantment()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 9, 1, wis: 18);
        CharacterController ally = Actor("Victim", "Fighter", 5, 2);
        StatusEffectManager effects = Effects(ally);
        ActiveSpellEffect hold = effects.AddEffect(SpellDatabase.GetSpell(SpellNames.HOLD_PERSON).Clone(), "Enemy", 5);
        ActiveSpellEffect slow = effects.AddEffect(SpellDatabase.GetSpell(SpellNames.SLOW).Clone(), "Enemy", 5);
        ActiveSpellEffect bless = effects.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Friend", 5);
        ActiveSpellEffect haste = effects.AddEffect(SpellDatabase.GetSpell(SpellNames.HASTE).Clone(), "Friend", 5);
        Assert(DispelMagicService.IsBreakEnchantmentEffect(hold) && DispelMagicService.IsBreakEnchantmentEffect(slow)
            && !DispelMagicService.IsBreakEnchantmentEffect(bless) && !DispelMagicService.IsBreakEnchantmentEffect(haste),
            "Break Enchantment: Hold Person and Slow are enchantments/transmutations that victimize; Bless and Haste do not");

        ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == DispelMagicService.DispelCheckContext ? 10 : natural;
        Apply(cleric, ally, SpellNames.BREAK_ENCHANTMENT, LandedNoSave());
        ScenarioHooks.RollFilter = null;

        Assert(!HasEffect(ally, SpellNames.HOLD_PERSON) && !HasEffect(ally, SpellNames.SLOW),
            "Break Enchantment: frees the subject of each enchantment and transmutation it beats (19 vs DC 16)");
        Assert(HasEffect(ally, SpellNames.BLESS) && HasEffect(ally, SpellNames.HASTE),
            "Break Enchantment: the subject's beneficial enchantments and transmutations stay");
    }

    /// <summary>Barkskin scales by the effect's caster level in AddEffect, so a staff or potion gets the same bonus (PHB p.203).</summary>
    private static void TestBarkskinThroughAddEffect()
    {
        CharacterController fighter = Actor("Armored", "Fighter", 3, 1);
        StatusEffectManager effects = Effects(fighter);
        int acBefore = fighter.Stats.ArmorClass;
        ActiveSpellEffect effect = effects.AddEffect(SpellDatabase.GetSpell(SpellNames.BARKSKIN).Clone(), "Staff of Life", 13);
        Assert(effect != null && effect.AppliedNaturalArmorEnhancementBonus == 5 && fighter.Stats.SpellNaturalArmorEnhancementBonus == 5,
            "Barkskin through StatusEffectManager.AddEffect at CL 13 (a staff): +5 natural armor enhancement", $"{effect?.AppliedNaturalArmorEnhancementBonus}");
        Assert(fighter.Stats.ArmorClass == acBefore + 5 && fighter.Stats.NaturalArmorEnhancementBonus == 5,
            "Barkskin at CL 13: AC +5, and the AC breakdowns' natural armor enhancement term shows it", $"AC {acBefore} -> {fighter.Stats.ArmorClass}");
    }

    /// <summary>Disintegrate's handler applies the cast's metamagic (PHB p.93 Empower, p.97 Maximize).</summary>
    private static void TestDisintegrateMetamagic()
    {
        int maxed = GameManager.RollDisintegrateDamage(24, true, false, out int noBonus);
        int both = GameManager.RollDisintegrateDamage(24, true, true, out int empowerBonus);
        Assert(maxed == 144 && noBonus == 0 && both == 216 && empowerBonus == 72,
            "Disintegrate: Maximize makes 24d6 144; Empower adds half (as SpellCaster does)", $"{maxed}, {both} (+{empowerBonus})");

        CharacterController wizard = Actor("Wizard", "Wizard", 12, 1, intelligence: 18);
        CharacterController target = Actor("Target", "Fighter", 5, 2);
        target.Stats.BonusMaxHP += 300;
        target.Stats.CurrentHP = 300;
        var metamagic = new MetamagicData();
        metamagic.AppliedMetamagic.Add(MetamagicFeatId.MaximizeSpell);
        SpellResult cast = LandedSave(false);
        cast.Metamagic = metamagic;
        cast.CasterLevel = 12;
        Apply(wizard, target, SpellNames.DISINTEGRATE, cast);
        Assert(target.Stats.CurrentHP == 300 - 144, "Disintegrate: a Maximized cast at CL 12 deals 144 (24d6 at 6 each)", $"HP {target.Stats.CurrentHP}");
    }

    /// <summary>
    /// Restoration and greater restoration function like lesser restoration (PHB p.272): they dispel magical effects
    /// that reduce ability scores, restoration one of them and greater restoration all of them.
    /// </summary>
    private static void TestRestorationDispelsAbilityPenalties()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 13, 1, wis: 18);
        CharacterController ally = Actor("Ally", "Fighter", 5, 2);
        int str = ally.Stats.EffectiveStrengthScore;
        ally.ApplyEnfeeblementEffect(4, 10, cleric);
        Assert(ally.ActiveEnfeeblementEffect != null && ally.Stats.EffectiveStrengthScore == str - 4, "Restoration setup: Ray of Enfeeblement -4 Str");
        Apply(cleric, ally, SpellNames.RESTORATION, LandedNoSave());
        Assert(ally.ActiveEnfeeblementEffect == null && ally.Stats.EffectiveStrengthScore == str,
            "Restoration: dispels the magical Strength penalty (PHB p.272, as lesser restoration)", $"Str {ally.Stats.EffectiveStrengthScore}");

        ally.ApplyEnfeeblementEffect(4, 10, cleric);
        ally.ApplyTouchOfIdiocyEffect(2, 2, 2, 10, cleric);
        Apply(cleric, ally, SpellNames.GREATER_RESTORATION, LandedNoSave());
        Assert(ally.ActiveEnfeeblementEffect == null && ally.ActiveTouchOfIdiocyEffect == null,
            "Greater Restoration: dispels every magical effect penalizing the abilities");
    }

    private static void TestHealLiving()
    {
        Assert(GameManager.HealAmount(1) == 10 && GameManager.HealAmount(11) == 110 && GameManager.HealAmount(15) == 150
            && GameManager.HealAmount(20) == 150, "Heal: 10 points per caster level, max 150 at 15th (PHB p.239)");

        CharacterController cleric = Actor("Cleric", "Cleric", 11, 1, wis: 18);
        CharacterController ally = Actor("Wounded", "Fighter", 5, 2);
        ally.Stats.BonusMaxHP += 300;
        ally.Stats.CurrentHP = 10;
        foreach (CombatConditionType c in new[] { CombatConditionType.Dazzled, CombatConditionType.Sickened, CombatConditionType.Fatigued, CombatConditionType.Confused })
            ally.ApplyCondition(c, 10, "Test");

        int cl = SpellCastingHelper.GetEffectiveCasterLevel(cleric, SpellDatabase.GetSpell(SpellNames.HEAL));
        Apply(cleric, ally, SpellNames.HEAL, LandedNoSave());
        Assert(ally.Stats.CurrentHP == 10 + GameManager.HealAmount(cl), $"Heal: cures 10 per level (CL {cl})", $"HP {ally.Stats.CurrentHP}");
        Assert(!ally.HasCondition(CombatConditionType.Dazzled) && !ally.HasCondition(CombatConditionType.Sickened)
            && !ally.HasCondition(CombatConditionType.Fatigued) && !ally.HasCondition(CombatConditionType.Confused),
            "Heal: ends dazzled, sickened, fatigued and confused");
    }

    private static void TestHealUndeadAndConstruct()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 10, 1, wis: 18);
        int cl = SpellCastingHelper.GetEffectiveCasterLevel(cleric, SpellDatabase.GetSpell(SpellNames.HEAL));
        int amount = GameManager.HealAmount(cl);

        CharacterController wight = Actor("Wight", "Fighter", 5, 2);
        wight.Stats.CreatureType = "Undead";
        wight.Stats.BonusMaxHP += 300;
        wight.Stats.CurrentHP = 300;
        Apply(cleric, wight, SpellNames.HEAL, LandedSave(false));
        Assert(wight.Stats.CurrentHP == 300 - amount, $"Heal on an undead: {amount} damage on a failed Will save (acts like harm)", $"HP {wight.Stats.CurrentHP}");

        wight.Stats.CurrentHP = 300;
        Apply(cleric, wight, SpellNames.HEAL, LandedSave(true));
        Assert(wight.Stats.CurrentHP == 300 - amount / 2, "Heal on an undead: half damage on a successful Will save", $"HP {wight.Stats.CurrentHP}");

        wight.Stats.CurrentHP = 20;
        Apply(cleric, wight, SpellNames.HEAL, LandedSave(true));
        Assert(wight.Stats.CurrentHP == 1, "Heal on an undead: a successful save cannot reduce it below 1 hp (harm, PHB p.239)", $"HP {wight.Stats.CurrentHP}");

        CharacterController golem = Actor("Golem", "Fighter", 5, 3);
        golem.Stats.CreatureType = "Construct";
        golem.Stats.BonusMaxHP += 50;
        golem.Stats.CurrentHP = 5;
        Apply(cleric, golem, SpellNames.HEAL, LandedNoSave());
        Assert(golem.Stats.CurrentHP == 5, "Heal: no effect on a construct (positive energy cures the living)");
    }

    private static void TestHealCastHealsNothingItself()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 11, 1, wis: 18);
        CharacterController ally = Actor("Wounded", "Fighter", 5, 2);
        ally.Stats.BonusMaxHP += 100;
        ally.Stats.CurrentHP = 10;
        SpellData heal = SpellDatabase.GetSpell(SpellNames.HEAL).Clone();
        SpellResult result = SpellCaster.Cast(heal, cleric.Stats, ally.Stats, null, true, false, cleric, ally);
        Assert(result.HealingDone == 0 && ally.Stats.CurrentHP == 10,
            "Heal: SpellCaster.Cast heals nothing itself (the handler heals; before SPL-037 the cast healed 1 point and the handler never ran)",
            $"healed {result.HealingDone}, HP {ally.Stats.CurrentHP}");

        CharacterController golem = Actor("Golem", "Fighter", 5, 3);
        golem.Stats.CreatureType = "Construct";
        SpellResult onGolem = SpellCaster.Cast(heal, cleric.Stats, golem.Stats, null, true, false, cleric, golem);
        Assert(!onGolem.Success, "Heal: the cast has no effect on a construct (positive energy)");

        SpellData disintegrate = SpellDatabase.GetSpell(SpellNames.DISINTEGRATE).Clone();
        CharacterController wizard = Actor("Wizard", "Wizard", 12, 4, intelligence: 18);
        CharacterController target = Actor("Target", "Fighter", 5, 5);
        target.Stats.BonusMaxHP += 100;
        target.Stats.CurrentHP = 100;
        ScenarioHooks.RollFilter = (sides, ctx, natural) => sides == 20 ? 20 : natural; // the ray hits
        SpellResult ray = SpellCaster.Cast(disintegrate, wizard.Stats, target.Stats, null, false, false, wizard, target);
        ScenarioHooks.RollFilter = null;
        Assert(ray.AttackHit, "Disintegrate setup: the ranged touch attack hits (natural 20)");
        Assert(ray.AttackHit && ray.DamageDealt == 0 && target.Stats.CurrentHP == 100,
            "Disintegrate: SpellCaster.Cast deals no damage itself on a hit (the handler does)", $"hit {ray.AttackHit}, dealt {ray.DamageDealt}");
        string rayLog = ray.GetFormattedLog() ?? "";
        Assert(!rayLog.Contains("0 → 0 HP"), "Disintegrate: the cast's log has no damage block (the handler logs the damage)", rayLog);
    }

    private static void TestDisintegrate()
    {
        Assert(GameManager.DisintegrateDiceCount(1, false) == 2 && GameManager.DisintegrateDiceCount(12, false) == 24
            && GameManager.DisintegrateDiceCount(25, false) == 40 && GameManager.DisintegrateDiceCount(12, true) == 5,
            "Disintegrate: 2d6 per caster level, max 40d6; 5d6 on a successful Fortitude save (PHB p.222)");

        CharacterController wizard = Actor("Wizard", "Wizard", 12, 1, intelligence: 18);

        CharacterController weak = Actor("Weak", "Fighter", 3, 2);
        weak.Stats.CurrentHP = 5;
        Apply(wizard, weak, SpellNames.DISINTEGRATE, LandedSave(false));
        Assert(weak.Stats.IsDead, "Disintegrate: a failed save at CL 12 (24d6) disintegrates a 5-hp creature", $"HP {weak.Stats.CurrentHP}");

        CharacterController frail = Actor("Frail", "Fighter", 3, 3);
        frail.Stats.CurrentHP = 3;
        Apply(wizard, frail, SpellNames.DISINTEGRATE, LandedSave(true));
        Assert(frail.Stats.IsDead, "Disintegrate: 5d6 that takes a creature to 0 or fewer hp disintegrates it (dead, not dying)", $"HP {frail.Stats.CurrentHP}");

        CharacterController tough = Actor("Tough", "Fighter", 5, 4);
        tough.Stats.BonusMaxHP += 100;
        tough.Stats.CurrentHP = 100;
        Apply(wizard, tough, SpellNames.DISINTEGRATE, LandedSave(true));
        int lost = 100 - tough.Stats.CurrentHP;
        Assert(lost >= 5 && lost <= 30, "Disintegrate: a successful save takes 5d6 (5-30)", $"lost {lost}");
    }

    private static void TestPlaneShift()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 9, 1, wis: 18);
        CharacterController enemy = Actor("Enemy", "Fighter", 5, 2);
        enemy.Stats.Bonuses.Set("test:save", BonusTarget.AllSaves, BonusType.Untyped, 100); // would save if the handler rolled again
        Apply(cleric, enemy, SpellNames.PLANE_SHIFT, LandedSave(false));
        Assert(CombatEndRules.IsOutOfFight(enemy),
            "Plane Shift: after the cast's failed Will save the target leaves the battle; the handler does not roll a second save");
    }

    /// <summary>
    /// Branches above the generic one that SpellCategoryClassifier's reclassification kept out (Dispel, Divination,
    /// Healing effect types): Dispel Magic, Break Enchantment, Remove Fear, See Invisibility, Detect Evil, Restoration,
    /// Greater Restoration, Stone to Flesh.
    /// </summary>
    private static void TestReclassifiedBranches()
    {
        foreach (string id in new[] { SpellNames.DISPEL_MAGIC, SpellNames.BREAK_ENCHANTMENT, SpellNames.REMOVE_FEAR, SpellNames.SEE_INVISIBLE,
                     SpellNames.DETECT_EVIL, SpellNames.DETECT_UNDEAD, SpellNames.RESTORATION, SpellNames.GREATER_RESTORATION, SpellNames.STONE_TO_FLESH })
            Assert(SpellEffectRouting.ReachesApplySpellBuff(SpellDatabase.GetSpell(id)), $"Routing: {id} reaches its ApplySpellBuff branch");
        foreach (string id in new[] { SpellNames.RESTORATION, SpellNames.GREATER_RESTORATION, SpellNames.STONE_TO_FLESH })
            Assert(SpellDatabase.GetSpell(id).HealingResolvedByHandler, $"{id}: SpellCaster.Cast heals no hit points");

        CharacterController cleric = Actor("Cleric", "Cleric", 9, 1, wis: 18);
        CharacterController ally = Actor("Ally", "Fighter", 5, 2);

        Apply(cleric, ally, SpellNames.BLESS);
        Assert(HasEffect(ally, SpellNames.BLESS), "Dispel Magic setup: Bless on the ally");
        Apply(cleric, ally, SpellNames.DISPEL_MAGIC, LandedNoSave());
        Assert(!HasEffect(ally, SpellNames.BLESS), "Dispel Magic: the caster's own Bless is dispelled (PHB p.223)");

        ally.ApplyCondition(CombatConditionType.Shaken, 10, "Test");
        Apply(cleric, ally, SpellNames.REMOVE_FEAR, LandedNoSave());
        Assert(!ally.HasCondition(CombatConditionType.Shaken) && ally.Stats.RemoveFearMoraleBonus == 4,
            "Remove Fear: the fear ends and +4 morale against fear (PHB p.271)");

        Apply(cleric, cleric, SpellNames.SEE_INVISIBLE, LandedNoSave());
        Assert(cleric.HasActiveSeeInvisibilityEffect, "See Invisibility: the caster sees invisible creatures (PHB p.275)");

        ally.ApplyCondition(CombatConditionType.Fatigued, 10, "Test");
        ally.Stats.ApplyAbilityDamage(AbilityType.STR, 2);
        Apply(cleric, ally, SpellNames.RESTORATION, LandedNoSave());
        Assert(!ally.HasCondition(CombatConditionType.Fatigued) && ally.Stats.GetAbilityDamage(AbilityType.STR) == 0,
            "Restoration: ends fatigue and cures temporary ability damage (PHB p.272)");

        CharacterController statue = Actor("Statue", "Fighter", 5, 3);
        statue.ApplyCondition(CombatConditionType.Petrified, 9999, "Test");
        Apply(cleric, statue, SpellNames.STONE_TO_FLESH, LandedNoSave());
        Assert(!statue.HasCondition(CombatConditionType.Petrified), "Stone to Flesh: the petrification ends");
    }

    private static void TestResurrection()
    {
        CharacterController cleric = Actor("Cleric", "Cleric", 13, 1, wis: 18);

        CharacterController living = Actor("Living", "Fighter", 5, 2);
        int hp = living.Stats.CurrentHP;
        Apply(cleric, living, SpellNames.RESURRECTION, LandedNoSave());
        Assert(living.Stats.CurrentHP == hp, "Resurrection: no effect on a living creature");

        CharacterController outsider = Actor("Outsider", "Fighter", 5, 3);
        outsider.Stats.CreatureType = "Outsider";
        outsider.Stats.CurrentHP = -10;
        Apply(cleric, outsider, SpellNames.RESURRECTION, LandedNoSave());
        Assert(outsider.Stats.IsDead, "Resurrection: an outsider can't be resurrected (PHB p.272)");

        CharacterController dead = Actor("Dead", "Fighter", 5, 4);
        dead.Stats.CurrentHP = -10;
        Apply(cleric, dead, SpellNames.RESURRECTION, LandedNoSave());
        Assert(dead.Stats.CurrentHP == dead.Stats.TotalMaxHP, "Resurrection: a dead humanoid returns with full hit points");
    }
}
}
#endif
