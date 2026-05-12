using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Blindness/Deafness spell mechanics (D&D 3.5e PHB p.206).
/// Tests blind condition: -2 AC, lose Dex to AC, 50% miss chance, half speed.
/// Tests deaf condition: -4 initiative, 20% verbal spell failure.
/// Tests condition removal.
/// </summary>
public static class BlindnessDeafnessRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void blindness_deafness_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== BLINDNESS/DEAFNESS RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinition();
        TestEffectDataFactoryMethods();
        TestBlindnessConditionACPenalty();
        TestBlindnessLosesDexToAC();
        TestBlindnessAttackerMissChance();
        TestBlindnessHalfSpeed();
        TestDeafnessInitiativePenalty();
        TestDeafnessSpellFailure();
        TestConditionRemoval();
        TestIsBlindIsDeafQueries();
        TestBlindnessEffectDataQueries();
        TestDeafnessEffectDataQueries();

        Debug.Log($"====== Blindness/Deafness Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(string name, string className = "Wizard", int level = 5, int dex = 14)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: dex,
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
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        stats.InitializeSkills(className, level);

        GameObject go = new GameObject($"BlindDeaf_{name}");
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

    // ======================== SPELL DEFINITION TESTS ========================

    private static void TestSpellDefinition()
    {
        Debug.Log("--- Spell Definition Tests ---");

        // Wizard/Sorcerer variant
        SpellData wizSpell = SpellDatabase.GetSpell(SpellNames.BLINDNESS_DEAFNESS_WIZ);
        Assert(wizSpell != null, "Wizard Blindness/Deafness spell exists in database");
        if (wizSpell != null)
        {
            Assert(wizSpell.SpellLevel == 2, "Wizard variant is level 2", $"got {wizSpell.SpellLevel}");
            Assert(wizSpell.School == "Necromancy", "School is Necromancy", $"got {wizSpell.School}");
            Assert(wizSpell.SavingThrowType == "Fortitude", "Save is Fortitude", $"got {wizSpell.SavingThrowType}");
            Assert(wizSpell.AllowsSavingThrow, "Allows saving throw");
            Assert(wizSpell.SpellResistanceApplies, "Spell resistance applies");
            Assert(wizSpell.HasVerbalComponent, "Has verbal component");
            Assert(!wizSpell.HasSomaticComponent, "No somatic component (V only)");
            Assert(wizSpell.BuffDurationRounds == -1, "Duration is permanent (-1)", $"got {wizSpell.BuffDurationRounds}");
            Assert(wizSpell.IsDismissible, "Is dismissible (D)");
            Assert(wizSpell.RangeCategory == SpellRangeCategory.Medium, "Range is Medium");
        }

        // Cleric variant
        SpellData clrSpell = SpellDatabase.GetSpell(SpellNames.BLINDNESS_DEAFNESS_CLR);
        Assert(clrSpell != null, "Cleric Blindness/Deafness spell exists in database");
        if (clrSpell != null)
        {
            Assert(clrSpell.SpellLevel == 3, "Cleric variant is level 3", $"got {clrSpell.SpellLevel}");
            Assert(clrSpell.School == "Necromancy", "Cleric school is Necromancy");
        }

        // Bard variant
        SpellData brdSpell = SpellDatabase.GetSpell(SpellNames.BLINDNESS_DEAFNESS_BRD);
        Assert(brdSpell != null, "Bard Blindness/Deafness spell exists in database");
        if (brdSpell != null)
        {
            Assert(brdSpell.SpellLevel == 2, "Bard variant is level 2", $"got {brdSpell.SpellLevel}");
        }
    }

    // ======================== EFFECT DATA TESTS ========================

    private static void TestEffectDataFactoryMethods()
    {
        Debug.Log("--- Effect Data Factory Tests ---");

        var caster = CreateController("Caster");

        // Blindness factory
        var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(SpellNames.BLINDNESS_DEAFNESS_WIZ, caster, 5);
        Assert(blindEffect != null, "CreateSpellBlindness returns non-null");
        Assert(blindEffect.AfflictionType == BlindDeafType.Blindness, "Blind effect type is Blindness");
        Assert(blindEffect.IsActive, "Blind effect is active");
        Assert(blindEffect.DurationRemainingRounds == -1, "Blind effect is permanent (-1)");
        Assert(blindEffect.IsDismissible, "Blind effect is dismissible");
        Assert(blindEffect.IsPermanent, "Blind effect marked permanent");
        Assert(blindEffect.SourceType == BlindDeafSourceType.Spell, "Source type is Spell");
        Assert(blindEffect.CasterLevel == 5, "Caster level is 5");

        // Deafness factory
        var deafEffect = BlindnessDeafnessEffectData.CreateSpellDeafness(SpellNames.BLINDNESS_DEAFNESS_WIZ, caster, 5);
        Assert(deafEffect != null, "CreateSpellDeafness returns non-null");
        Assert(deafEffect.AfflictionType == BlindDeafType.Deafness, "Deaf effect type is Deafness");
        Assert(deafEffect.IsActive, "Deaf effect is active");
        Assert(deafEffect.DurationRemainingRounds == -1, "Deaf effect is permanent (-1)");

        // From source factory
        var poisonEffect = BlindnessDeafnessEffectData.CreateFromSource(
            BlindDeafType.Blindness, BlindDeafSourceType.PoisonOrDisease, "Poison Dart", 10, false);
        Assert(poisonEffect != null, "CreateFromSource returns non-null");
        Assert(poisonEffect.DurationRemainingRounds == 10, "Poison source has 10 round duration");
        Assert(!poisonEffect.IsDismissible, "Poison source is not dismissible");
        Assert(!poisonEffect.IsPermanent, "Poison source is not permanent");

        DestroyController(caster);
    }

    // ======================== BLIND CONDITION COMBAT TESTS ========================

    private static void TestBlindnessConditionACPenalty()
    {
        Debug.Log("--- Blind AC Penalty Tests ---");

        var target = CreateController("BlindTarget", dex: 14);
        int baseAC = target.Stats.ArmorClass;

        // Apply blindness
        var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        target.ApplyBlindnessEffect(blindEffect);

        int blindAC = target.Stats.ArmorClass;

        // Blinded: -2 AC penalty AND lose Dex bonus to AC
        // Base AC = 10 + DEX(+2) = 12
        // Blind AC = 10 + 0 (no Dex) - 2 = 8
        // So blind AC should be 4 less than base (lost +2 Dex, gained -2 penalty)
        Assert(blindAC < baseAC, "Blind AC is less than base AC",
            $"base={baseAC}, blind={blindAC}");
        Assert(blindAC == baseAC - 4, "Blind AC reduced by 4 (Dex loss + -2 penalty)",
            $"expected {baseAC - 4}, got {blindAC}");

        DestroyController(target);
    }

    private static void TestBlindnessLosesDexToAC()
    {
        Debug.Log("--- Blind Loses Dex to AC ---");

        var target = CreateController("DexTarget", dex: 18); // DEX mod +4
        Assert(!target.Stats.DeniedDexToAcByCondition, "Not denied Dex before blindness");

        var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        target.ApplyBlindnessEffect(blindEffect);

        Assert(target.Stats.DeniedDexToAcByCondition, "Denied Dex to AC while blinded");
        Assert(target.IsBlind(), "IsBlind() returns true");

        DestroyController(target);
    }

    private static void TestBlindnessAttackerMissChance()
    {
        Debug.Log("--- Blind Attacker 50% Miss Chance ---");

        var attacker = CreateController("BlindAttacker");

        // Verify miss chance from effect data
        var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        Assert(blindEffect.GetAttackMissChance() == 50, "Blind effect data reports 50% miss chance",
            $"got {blindEffect.GetAttackMissChance()}");

        // Apply blindness and verify condition
        attacker.ApplyBlindnessEffect(blindEffect);
        Assert(attacker.HasCondition(CombatConditionType.Blinded), "Attacker has Blinded condition");

        DestroyController(attacker);
    }

    private static void TestBlindnessHalfSpeed()
    {
        Debug.Log("--- Blind Half Speed ---");

        // Verify effect data reports 0.5x movement
        var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        Assert(Mathf.Approximately(blindEffect.GetMovementMultiplier(), 0.5f),
            "Blind effect data reports 0.5x movement multiplier",
            $"got {blindEffect.GetMovementMultiplier()}");

        // Verify condition definition has 0.5x movement
        var condDef = ConditionRules.GetDefinition(CombatConditionType.Blinded);
        Assert(condDef != null, "Blinded condition definition exists");
        if (condDef != null)
        {
            Assert(Mathf.Approximately(condDef.MovementMultiplier, 0.5f),
                "Blinded condition has 0.5x movement multiplier",
                $"got {condDef.MovementMultiplier}");
        }
    }

    // ======================== DEAF CONDITION TESTS ========================

    private static void TestDeafnessInitiativePenalty()
    {
        Debug.Log("--- Deaf Initiative Penalty ---");

        var target = CreateController("DeafTarget", dex: 14);
        int baseInit = target.Stats.InitiativeModifier;

        var deafEffect = BlindnessDeafnessEffectData.CreateSpellDeafness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        target.ApplyDeafnessEffect(deafEffect);

        int deafInit = target.Stats.InitiativeModifier;
        // PHB p.307: -4 initiative penalty
        Assert(deafInit == baseInit - 4, "Deaf initiative is 4 less than base",
            $"base={baseInit}, deaf={deafInit}, expected {baseInit - 4}");

        // Verify via effect data
        Assert(deafEffect.GetInitiativePenalty() == -4, "Effect data reports -4 initiative penalty");

        DestroyController(target);
    }

    private static void TestDeafnessSpellFailure()
    {
        Debug.Log("--- Deaf 20% Verbal Spell Failure ---");

        var caster = CreateController("DeafCaster");

        // Before deafness
        Assert(caster.GetDeafnessSpellFailureChance() == 0, "No spell failure before deafness");

        var deafEffect = BlindnessDeafnessEffectData.CreateSpellDeafness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        caster.ApplyDeafnessEffect(deafEffect);

        // After deafness
        Assert(caster.GetDeafnessSpellFailureChance() == 20, "20% spell failure while deafened",
            $"got {caster.GetDeafnessSpellFailureChance()}");

        DestroyController(caster);
    }

    // ======================== CONDITION REMOVAL TESTS ========================

    private static void TestConditionRemoval()
    {
        Debug.Log("--- Condition Removal Tests ---");

        // Test blindness removal
        var blindTarget = CreateController("BlindRemoveTarget");
        var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        blindTarget.ApplyBlindnessEffect(blindEffect);

        Assert(blindTarget.IsBlind(), "Target is blind before removal");
        Assert(blindTarget.HasActiveBlindnessDeafnessEffect, "Has active effect before removal");

        blindTarget.RemoveBlindnessDeafnessEffect();

        Assert(!blindTarget.IsBlind(), "Target is not blind after removal");
        Assert(!blindTarget.HasActiveBlindnessDeafnessEffect, "No active effect after removal");
        Assert(!blindTarget.HasCondition(CombatConditionType.Blinded), "Blinded condition removed");

        // Test deafness removal
        var deafTarget = CreateController("DeafRemoveTarget");
        var deafEffect = BlindnessDeafnessEffectData.CreateSpellDeafness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        deafTarget.ApplyDeafnessEffect(deafEffect);

        Assert(deafTarget.IsDeaf(), "Target is deaf before removal");

        deafTarget.RemoveBlindnessDeafnessEffect();

        Assert(!deafTarget.IsDeaf(), "Target is not deaf after removal");
        Assert(!deafTarget.HasCondition(CombatConditionType.Deafened), "Deafened condition removed");

        DestroyController(blindTarget);
        DestroyController(deafTarget);
    }

    // ======================== QUERY METHOD TESTS ========================

    private static void TestIsBlindIsDeafQueries()
    {
        Debug.Log("--- IsBlind/IsDeaf Query Tests ---");

        var target = CreateController("QueryTarget");

        // No condition
        Assert(!target.IsBlind(), "Not blind initially");
        Assert(!target.IsDeaf(), "Not deaf initially");

        // Apply blindness via effect
        var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        target.ApplyBlindnessEffect(blindEffect);
        Assert(target.IsBlind(), "IsBlind after applying blindness effect");
        Assert(!target.IsDeaf(), "Not deaf when blinded");

        // Remove and apply deafness
        target.RemoveBlindnessDeafnessEffect();
        var deafEffect = BlindnessDeafnessEffectData.CreateSpellDeafness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);
        target.ApplyDeafnessEffect(deafEffect);
        Assert(!target.IsBlind(), "Not blind when deafened");
        Assert(target.IsDeaf(), "IsDeaf after applying deafness effect");

        DestroyController(target);
    }

    private static void TestBlindnessEffectDataQueries()
    {
        Debug.Log("--- Blindness Effect Data Query Tests ---");

        var blindEffect = BlindnessDeafnessEffectData.CreateSpellBlindness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);

        Assert(blindEffect.IsBlindness, "IsBlindness is true");
        Assert(!blindEffect.IsDeafness, "IsDeafness is false for blind effect");
        Assert(blindEffect.GetACPenalty() == -2, "AC penalty is -2", $"got {blindEffect.GetACPenalty()}");
        Assert(blindEffect.DeniesDexToAC(), "Denies Dex to AC");
        Assert(blindEffect.GetAttackMissChance() == 50, "50% miss chance");
        Assert(Mathf.Approximately(blindEffect.GetMovementMultiplier(), 0.5f), "Half speed");
        Assert(blindEffect.GetInitiativePenalty() == 0, "No initiative penalty for blindness");
        Assert(blindEffect.GetVerbalSpellFailureChance() == 0, "No spell failure for blindness");
        Assert(blindEffect.GetSkillCheckPenalty() == -4, "Skill check penalty is -4");
        Assert(blindEffect.IsSpellBased, "Is spell-based");
        Assert(blindEffect.MatchesSpellId(SpellNames.BLINDNESS_DEAFNESS_WIZ), "Matches spell ID");
    }

    private static void TestDeafnessEffectDataQueries()
    {
        Debug.Log("--- Deafness Effect Data Query Tests ---");

        var deafEffect = BlindnessDeafnessEffectData.CreateSpellDeafness(SpellNames.BLINDNESS_DEAFNESS_WIZ, null, 5);

        Assert(!deafEffect.IsBlindness, "IsBlindness is false for deaf effect");
        Assert(deafEffect.IsDeafness, "IsDeafness is true");
        Assert(deafEffect.GetACPenalty() == 0, "No AC penalty for deafness");
        Assert(!deafEffect.DeniesDexToAC(), "Does not deny Dex to AC");
        Assert(deafEffect.GetAttackMissChance() == 0, "No miss chance for deafness");
        Assert(Mathf.Approximately(deafEffect.GetMovementMultiplier(), 1.0f), "Normal speed");
        Assert(deafEffect.GetInitiativePenalty() == -4, "Initiative penalty is -4");
        Assert(deafEffect.GetVerbalSpellFailureChance() == 20, "20% verbal spell failure");
        Assert(deafEffect.GetSkillCheckPenalty() == 0, "No skill check penalty for deafness");
    }
}
}
