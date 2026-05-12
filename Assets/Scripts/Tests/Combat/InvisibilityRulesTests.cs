using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Invisibility mechanics.
/// </summary>
public static class InvisibilityRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void invisibility_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== INVISIBILITY RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinition();
        TestConcealmentAndHideBonuses();
        TestBreaksOnAttackRoll();
        TestDirectVisibilityBlockedWhileInvisible();
        TestGlitterdustRevealsInvisibleCreatures();
        TestInvisibleAttackerBonus();
        TestDenyTargetDexToAC();
        TestBreaksOnAttackFlag();
        TestFactoryMethods();
        TestSourceTracking();

        Debug.Log($"====== Invisibility Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(string name, string className = "Wizard", int level = 5, int baseSpeedSquares = 6)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: 14,
            con: 12,
            wis: 10,
            intelligence: 16,
            cha: 10,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: baseSpeedSquares,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        stats.InitializeSkills(className, level);

        GameObject go = new GameObject($"Invisibility_{name}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Init(stats, Vector2Int.zero, null, null);

        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        inv.Init(stats);

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void ApplyInvisibility(CharacterController target, CharacterController caster, int casterLevel = 5)
    {
        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        SpellData spell = SpellDatabase.GetSpell(SpellNames.INVISIBILITY);
        ActiveSpellEffect effect = statusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster", casterLevel);
        if (effect != null)
            target.ApplyInvisibilityEffect(effect.RemainingRounds, caster, isMoving: false);
    }

    private static void TestSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.INVISIBILITY);
        Assert(spell != null, "Invisibility definition exists");
        if (spell == null)
            return;

        Assert(spell.SpellLevel == 2, "Spell level is 2");
        Assert(spell.School == "Illusion", "School is Illusion");
        Assert(spell.TargetType == SpellTargetType.SingleAlly, "Target type supports ally/self touch cast flow");
        Assert(spell.RangeCategory == SpellRangeCategory.Touch, "Range is touch");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
            "Duration is 1 minute/level");
        Assert(spell.IsDismissible, "Spell is dismissible");
        Assert(spell.ClassList != null
               && System.Array.Exists(spell.ClassList, c => c == "Wizard")
               && System.Array.Exists(spell.ClassList, c => c == "Sorcerer")
               && System.Array.Exists(spell.ClassList, c => c == "Bard"),
            "Class list includes Wizard/Sorcerer/Bard");
    }

    private static void TestConcealmentAndHideBonuses()
    {
        CharacterController caster = null;
        CharacterController target = null;
        CharacterController attacker = null;

        try
        {
            caster = CreateController("Caster", "Wizard", 5);
            target = CreateController("Target", "Rogue", 5);
            attacker = CreateController("Attacker", "Fighter", 5);

            int baseHide = target.Stats.GetSkillBonus("Hide");
            ApplyInvisibility(target, caster, 5);

            Assert(target.HasActiveInvisibilityEffect, "Target tracks active invisibility effect data");
            Assert(target.GetMissChance(attacker, incomingIsRangedAttack: false) == 50,
                "Invisibility grants 50% miss chance vs melee");
            Assert(target.GetMissChance(attacker, incomingIsRangedAttack: true) == 50,
                "Invisibility grants 50% miss chance vs ranged");

            int stationaryHide = target.Stats.GetSkillBonus("Hide");
            Assert(stationaryHide - baseHide == 40,
                "Hide bonus is +40 while stationary",
                $"base={baseHide}, actual={stationaryHide}");

            target.UpdateInvisibilityMovementState(true);
            int movingHide = target.Stats.GetSkillBonus("Hide");
            Assert(movingHide - baseHide == 20,
                "Hide bonus is +20 while moving",
                $"base={baseHide}, actual={movingHide}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            DestroyController(attacker);
        }
    }

    private static void TestBreaksOnAttackRoll()
    {
        CharacterController attacker = null;
        CharacterController defender = null;

        try
        {
            attacker = CreateController("InvisibleAttacker", "Rogue", 5);
            defender = CreateController("Defender", "Fighter", 5);

            ApplyInvisibility(attacker, attacker, 5);
            StatusEffectManager statusMgr = attacker.GetComponent<StatusEffectManager>();

            CombatResult result = attacker.Attack(defender, false, 0, null, null, null, null);
            Assert(result != null, "Attack resolves while invisible");
            Assert(!attacker.HasActiveInvisibilityEffect, "Invisibility effect data clears on attack action");
            Assert(statusMgr != null && !statusMgr.HasEffect(SpellNames.INVISIBILITY), "Invisibility spell effect removed on attack action");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    private static void TestDirectVisibilityBlockedWhileInvisible()
    {
        CharacterController caster = null;
        CharacterController invisibleTarget = null;

        try
        {
            caster = CreateController("Spellcaster", "Wizard", 5);
            invisibleTarget = CreateController("InvisibleTarget", "Wizard", 5);

            ApplyInvisibility(invisibleTarget, invisibleTarget, 5);
            Assert(!caster.CanSee(invisibleTarget, incomingIsRangedAttack: false),
                "Direct visibility check fails while target is invisible");

            StatusEffectManager statusMgr = invisibleTarget.GetComponent<StatusEffectManager>();
            statusMgr.RemoveEffectsBySpellId(SpellNames.INVISIBILITY);
            invisibleTarget.ClearInvisibilityEffect();

            Assert(caster.CanSee(invisibleTarget, incomingIsRangedAttack: false),
                "Visibility check succeeds after invisibility ends");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(invisibleTarget);
        }
    }

    /// <summary>
    /// PHB p.141: An invisible attacker gains +2 bonus on attack rolls.
    /// Verify the bonus is applied during the attack action.
    /// </summary>
    private static void TestInvisibleAttackerBonus()
    {
        CharacterController attacker = null;
        CharacterController defender = null;

        try
        {
            attacker = CreateController("InvisAttacker", "Rogue", 5);
            defender = CreateController("Defender", "Fighter", 5);

            // Verify no bonus when visible
            int visibleBonus = attacker.GetInvisibleAttackerBonus(defender);
            Assert(visibleBonus == 0, "No invisible attacker bonus when visible", $"got={visibleBonus}");

            // Apply invisibility and verify +2 bonus
            ApplyInvisibility(attacker, attacker, 5);
            int invisBonus = attacker.GetInvisibleAttackerBonus(defender);
            Assert(invisBonus == 2, "Invisible attacker gets +2 attack bonus (PHB p.141)", $"got={invisBonus}");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    /// <summary>
    /// PHB p.141: An invisible attacker denies the target's Dex bonus to AC.
    /// </summary>
    private static void TestDenyTargetDexToAC()
    {
        CharacterController attacker = null;
        CharacterController defender = null;

        try
        {
            attacker = CreateController("InvisAttacker2", "Rogue", 5);
            defender = CreateController("DexDefender", "Fighter", 5);

            // When visible, should NOT deny Dex to AC
            bool deniesWhenVisible = attacker.ShouldDenyTargetDexToAC(defender);
            Assert(!deniesWhenVisible, "Visible attacker does not deny target Dex to AC");

            // When invisible, SHOULD deny Dex to AC
            ApplyInvisibility(attacker, attacker, 5);
            bool deniesWhenInvis = attacker.ShouldDenyTargetDexToAC(defender);
            Assert(deniesWhenInvis, "Invisible attacker denies target Dex to AC (PHB p.141)");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    /// <summary>
    /// Verify that BreaksOnAttack flag works correctly:
    /// - Standard Invisibility breaks on attack (BreaksOnAttack = true)
    /// - Greater Invisibility does NOT break on attack (BreaksOnAttack = false)
    /// </summary>
    private static void TestBreaksOnAttackFlag()
    {
        // Test standard invisibility effect data
        var stdEffect = InvisibilityEffectData.CreateStandardInvisibility(10, null);
        Assert(stdEffect.BreaksOnAttack, "Standard invisibility BreaksOnAttack is true");
        Assert(stdEffect.IsStandardInvisibility, "Standard invisibility IsStandardInvisibility is true");

        // Test greater invisibility effect data
        var greaterEffect = InvisibilityEffectData.CreateGreaterInvisibility(10, null);
        Assert(!greaterEffect.BreaksOnAttack, "Greater invisibility BreaksOnAttack is false");
        Assert(!greaterEffect.IsStandardInvisibility, "Greater invisibility IsStandardInvisibility is false");

        // Both should have 50% miss chance
        Assert(stdEffect.ConcealmentMissChance == 50, "Standard invisibility has 50% miss chance");
        Assert(greaterEffect.ConcealmentMissChance == 50, "Greater invisibility has 50% miss chance");
    }

    /// <summary>
    /// Verify factory methods produce correct configurations for different sources.
    /// </summary>
    private static void TestFactoryMethods()
    {
        // Standard Invisibility (spell-based)
        var std = InvisibilityEffectData.CreateStandardInvisibility(10, null);
        Assert(std.SourceType == InvisibilitySourceType.Spell, "Standard factory: SourceType is Spell");
        Assert(std.SourceSpellId == SpellNames.INVISIBILITY, "Standard factory: SourceSpellId is 'invisibility'");
        Assert(std.IsDismissible, "Standard factory: IsDismissible is true");
        Assert(std.HideBonusStationary == 40, "Standard factory: +40 Hide stationary");
        Assert(std.HideBonusMoving == 20, "Standard factory: +20 Hide moving");

        // Greater Invisibility (spell-based)
        var greater = InvisibilityEffectData.CreateGreaterInvisibility(10, null);
        Assert(greater.SourceType == InvisibilitySourceType.Spell, "Greater factory: SourceType is Spell");
        Assert(greater.SourceSpellId == "greater_invisibility", "Greater factory: SourceSpellId is 'greater_invisibility'");
        Assert(!greater.BreaksOnAttack, "Greater factory: BreaksOnAttack is false");
        Assert(greater.IsDismissible, "Greater factory: IsDismissible is true");

        // Magic Item source
        var ring = InvisibilityEffectData.CreateFromMagicItem("Ring of Invisibility", breaksOnAttack: true, durationRounds: 100);
        Assert(ring.SourceType == InvisibilitySourceType.MagicItem, "MagicItem factory: SourceType is MagicItem");
        Assert(ring.SourceName == "Ring of Invisibility", "MagicItem factory: SourceName is correct");
        Assert(ring.BreaksOnAttack, "MagicItem factory: BreaksOnAttack respected");

        // Supernatural ability source
        var ability = InvisibilityEffectData.CreateFromAbility("Pixie Invisibility",
            InvisibilitySourceType.Supernatural, breaksOnAttack: false, durationRounds: 5);
        Assert(ability.SourceType == InvisibilitySourceType.Supernatural, "Ability factory: SourceType is Supernatural");
        Assert(!ability.BreaksOnAttack, "Ability factory: BreaksOnAttack=false respected");
        Assert(ability.SourceName == "Pixie Invisibility", "Ability factory: SourceName correct");
    }

    /// <summary>
    /// Verify source tracking fields: SourceSpellId, SourceName, SourceType, and helper methods.
    /// </summary>
    private static void TestSourceTracking()
    {
        // Spell-based source tracking
        var spellEffect = InvisibilityEffectData.CreateStandardInvisibility(10, null);
        Assert(spellEffect.IsSpellBased, "Spell effect IsSpellBased is true");
        Assert(spellEffect.MatchesSpellId(SpellNames.INVISIBILITY), "MatchesSpellId matches 'invisibility'");
        Assert(!spellEffect.MatchesSpellId("greater_invisibility"), "MatchesSpellId does not match 'greater_invisibility'");
        Assert(spellEffect.GetAttackBonus() == 2, "GetAttackBonus returns +2");

        // Magic item source tracking
        var itemEffect = InvisibilityEffectData.CreateFromMagicItem("Cloak of Invisibility", breaksOnAttack: false, durationRounds: 100);
        Assert(!itemEffect.IsSpellBased, "Magic item IsSpellBased is false");
        Assert(itemEffect.SourceType == InvisibilitySourceType.MagicItem, "Magic item SourceType correct");
        Assert(itemEffect.GetAttackBonus() == 2, "Magic item invisibility still gives +2 attack bonus");

        // Hide bonus based on movement state
        var effect = InvisibilityEffectData.CreateStandardInvisibility(10, null);
        effect.IsMoving = false;
        Assert(effect.GetCurrentHideBonus() == 40, "GetCurrentHideBonus stationary = 40");
        effect.IsMoving = true;
        Assert(effect.GetCurrentHideBonus() == 20, "GetCurrentHideBonus moving = 20");
    }

    private static void TestGlitterdustRevealsInvisibleCreatures()
    {
        CharacterController caster = null;
        CharacterController invisibleTarget = null;
        CharacterController observer = null;

        try
        {
            caster = CreateController("GlitterCaster", "Wizard", 5);
            invisibleTarget = CreateController("GlitterTarget", "Rogue", 5);
            observer = CreateController("Observer", "Fighter", 5);

            SpellData glitterdust = SpellDatabase.GetSpell(SpellNames.GLITTERDUST);
            Assert(glitterdust != null, "Glitterdust definition exists");
            if (glitterdust == null)
                return;

            int baseHide = invisibleTarget.Stats.GetSkillBonus("Hide");
            ApplyInvisibility(invisibleTarget, caster, 5);

            StatusEffectManager statusMgr = invisibleTarget.GetComponent<StatusEffectManager>();
            ActiveSpellEffect glitterEffect = statusMgr.AddEffect(glitterdust, caster.Stats.CharacterName, 5);
            int duration = glitterEffect != null ? glitterEffect.RemainingRounds : 5;
            invisibleTarget.ApplyGlitterdustEffect(duration, caster, blindedByFailedSave: false);

            Assert(invisibleTarget.HasActiveGlitterdustEffect, "Glitterdust outlined effect is active on target");
            Assert(invisibleTarget.GetMissChance(observer, incomingIsRangedAttack: false) == 0,
                "Glitterdust removes invisibility concealment for all observers");
            Assert(observer.CanSee(invisibleTarget, incomingIsRangedAttack: false),
                "Observer can directly see glitterdusted invisible target");

            int hideAfterGlitterdust = invisibleTarget.Stats.GetSkillBonus("Hide");
            Assert(hideAfterGlitterdust - baseHide == -40,
                "Glitterdust applies net Hide -40 and suppresses invisibility Hide bonus",
                $"base={baseHide}, actual={hideAfterGlitterdust}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(invisibleTarget);
            DestroyController(observer);
        }
    }
}
}
