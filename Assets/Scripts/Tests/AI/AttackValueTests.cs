#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using DND35.AI;
using DND35.AI.Valuation;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 2 (docs/designs/personalities_batch14.md, "AI/Valuation"): AttackValue, the AI's hit chance and damage
/// estimates extracted from AIProfile's private HitChance and AverageWeaponDamage and ThreatSystem's
/// EstimateAverageMeleeDamage.
/// <list type="bullet">
/// <item>Each AttackValue member equals the formula it replaced (copied here as it stood before the extraction) on a
/// grid of modifiers and ACs and on live creatures (a longsword, a longbow, a natural attack, an unarmed strike), and
/// the old members, which now delegate, give the same values (reflection, TST-006).</item>
/// <item>ExpectedRoundDamage sums the iterative steps (BAB +6: two steps), the natural attacks by count with the
/// secondary penalty, and gives 0 without a creature.</item>
/// </list>
/// Play mode (CharacterController.Init, TST-009).
/// </summary>
public static class AttackValueTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== ATTACK VALUE TESTS (batch 14 step 2) ======");
        TestHelpers.EnsureCoreDatabasesInitialized();

        var made = new List<CharacterController>();
        try
        {
            TestHitChance();
            TestDamageOnCreatures(made);
            TestExpectedRoundDamage(made);
        }
        finally
        {
            foreach (CharacterController c in made)
                if (c != null)
                    Object.DestroyImmediate(c.gameObject);
        }

        Debug.Log($"====== Attack Value Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    // ── The formulas as they stood before the extraction (AIProfile and ThreatSystem at de36b83) ──

    private static float OldProfileHitChance(int attackModifier, int targetAC)
        => Mathf.Clamp((21 + attackModifier - targetAC) / 20f, 0.05f, 0.95f);

    private static float OldAoOHitChance(int attackModifier, int targetAC)
        => Mathf.Clamp((21f + attackModifier - targetAC) / 20f, 0.05f, 0.95f);

    private static float OldAverageWeaponDamage(CharacterController self, ItemData weapon, bool offHand)
    {
        self.GetScaledWeaponDamageDice(weapon, out int count, out int dice);
        float average = Mathf.Max(1, count) * (dice + 1) / 2f + self.Stats.GetWeaponDamageModifier(weapon, offHand) + weapon.BonusDamage;
        return Mathf.Max(1f, average);
    }

    private static float OldEstimateAverageMeleeDamage(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return 0f;
        ItemData weapon = attacker.GetEquippedMainWeapon();
        if (weapon != null && weapon.IsWeapon)
        {
            attacker.GetScaledWeaponDamageDice(weapon, out int count, out int dice);
            float diceAverage = count > 0 && dice > 0 ? count * (dice + 1) * 0.5f : 2.5f;
            float damageBonus = weapon.BonusDamage;
            if (weapon.WeaponCat == WeaponCategory.Melee || weapon.IsThrown)
                damageBonus += attacker.Stats.STRMod;
            return Mathf.Max(1f, diceAverage + damageBonus);
        }
        NaturalAttackDefinition natural = attacker.Stats.GetPrimaryNaturalAttack();
        if (natural != null)
        {
            attacker.Stats.GetScaledNaturalAttackDamage(natural, out int naturalCount, out int naturalDice);
            return Mathf.Max(1f, naturalCount * (naturalDice + 1) * 0.5f + attacker.Stats.GetNaturalAttackDamageBonus(natural));
        }
        var unarmed = attacker.GetUnarmedDamage();
        return Mathf.Max(1f, unarmed.damageCount * (unarmed.damageDice + 1) * 0.5f + unarmed.bonusDamage + attacker.Stats.STRMod);
    }

    // ── The delegating members, by reflection (TST-006: grep the tests before renaming them) ──

    private static readonly MethodInfo ProfileHitChance = typeof(AIProfile).GetMethod("HitChance", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo ProfileAverageWeaponDamage = typeof(AIProfile).GetMethod("AverageWeaponDamage", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo ThreatAverageMeleeDamage = typeof(ThreatSystem).GetMethod("EstimateAverageMeleeDamage", BindingFlags.NonPublic | BindingFlags.Static);

    private static void TestHitChance()
    {
        Assert(ProfileHitChance != null, "AIProfile.HitChance exists (reflection)");
        int mismatches = 0;
        string first = null;
        for (int mod = -15; mod <= 35; mod++)
        {
            for (int ac = 0; ac <= 40; ac++)
            {
                float v = AttackValue.HitChance(mod, ac);
                bool same = v == OldProfileHitChance(mod, ac) && v == OldAoOHitChance(mod, ac)
                    && (ProfileHitChance == null || v == (float)ProfileHitChance.Invoke(null, new object[] { mod, ac }));
                if (!same)
                {
                    mismatches++;
                    if (first == null)
                        first = "mod " + mod + " AC " + ac;
                }
            }
        }
        Assert(mismatches == 0, "AttackValue.HitChance equals the old profile and AoO formulas and AIProfile.HitChance (mod -15..35, AC 0..40)", first);
        Assert(AttackValue.HitChance(5, 15) == 0.55f && AttackValue.HitChance(-20, 15) == 0.05f && AttackValue.HitChance(40, 15) == 0.95f,
            "a natural 1 misses and a natural 20 hits (PHB p.134): +5 against AC 15 is 0.55, clamped to 0.05 and 0.95");
    }

    private static void Equip(CharacterController c, ItemData item)
    {
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        inv.CharacterInventory.DirectEquip(item, EquipSlot.RightHand);
        inv.CharacterInventory.RecalculateStats();
    }

    private static void TestDamageOnCreatures(List<CharacterController> made)
    {
        Assert(ProfileAverageWeaponDamage != null && ThreatAverageMeleeDamage != null,
            "AIProfile.AverageWeaponDamage and ThreatSystem.EstimateAverageMeleeDamage exist (reflection)");

        CharacterController swordsman = TestHelpers.CreateCharacter(name: "AV Swordsman", level: 4, str: 16, bab: 4);
        CharacterController archer = TestHelpers.CreateCharacter(name: "AV Archer", level: 4, str: 14, dex: 16, bab: 4);
        CharacterController beast = TestHelpers.CreateCharacter(name: "AV Beast", level: 2, str: 15, bab: 1);
        CharacterController brawler = TestHelpers.CreateCharacter(name: "AV Brawler", level: 1, str: 13, bab: 1);
        made.Add(swordsman);
        made.Add(archer);
        made.Add(beast);
        made.Add(brawler);
#pragma warning disable CS0618 // the longsword's and longbow's string ids
        ItemData sword = ItemDatabase.CloneItem(ItemIDs.LONGSWORD);
        ItemData bow = ItemDatabase.CloneItem(ItemIDs.LONGBOW);
#pragma warning restore CS0618
        Equip(swordsman, sword);
        Equip(archer, bow);
        beast.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });

        foreach (bool offHand in new[] { false, true })
        {
            float v = AttackValue.AverageWeaponDamage(swordsman, sword, offHand);
            float old = OldAverageWeaponDamage(swordsman, sword, offHand);
            float viaProfile = ProfileAverageWeaponDamage != null
                ? (float)ProfileAverageWeaponDamage.Invoke(null, new object[] { swordsman, sword, offHand }) : float.NaN;
            Assert(v == old && v == viaProfile, "AverageWeaponDamage of a longsword (" + (offHand ? "off" : "main") + " hand) equals the old formula and AIProfile's",
                v + " old " + old + " profile " + viaProfile);
        }

        foreach (CharacterController c in new[] { swordsman, archer, beast, brawler })
        {
            float v = AttackValue.AverageMeleeDamage(c);
            float old = OldEstimateAverageMeleeDamage(c);
            float viaThreat = ThreatAverageMeleeDamage != null ? (float)ThreatAverageMeleeDamage.Invoke(null, new object[] { c }) : float.NaN;
            Assert(v == old && v == viaThreat && v >= 1f, "AverageMeleeDamage of " + c.Stats.CharacterName + " equals the old formula and ThreatSystem's",
                v + " old " + old + " threat " + viaThreat);
        }
        Assert(sword != null && AttackValue.AverageMeleeDamage(swordsman) == 4.5f + swordsman.Stats.STRMod + sword.BonusDamage,
            "a Medium longsword's hit averages 4.5 (1d8) plus Strength (PHB p.116)", AttackValue.AverageMeleeDamage(swordsman).ToString());
        Assert(AttackValue.AverageMeleeDamage(null) == 0f, "no creature has no damage");
    }

    private static void TestExpectedRoundDamage(List<CharacterController> made)
    {
        CharacterController fighter = TestHelpers.CreateCharacter(name: "AV Fighter6", level: 6, str: 16, bab: 6);
        CharacterController beast = TestHelpers.CreateCharacter(name: "AV Clawer", level: 2, str: 14, bab: 2);
        made.Add(fighter);
        made.Add(beast);
#pragma warning disable CS0618 // the longsword's string id
        Equip(fighter, ItemDatabase.CloneItem(ItemIDs.LONGSWORD));
#pragma warning restore CS0618
        beast.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = true });
        beast.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = false });

        const int ac = 16;
        int bab = fighter.Stats.BaseAttackBonus;
        int melee = fighter.Stats.GetMeleeAttackBonus();
        float perHit = AttackValue.AverageMeleeDamage(fighter);
        float expected = AttackValue.HitChance(melee, ac) * perHit + (bab >= 6 ? AttackValue.HitChance(melee - 5, ac) * perHit : 0f);
        float got = AttackValue.ExpectedRoundDamage(fighter, ac);
        Assert(bab >= 6 && Mathf.Approximately(got, expected), "a BAB +6 swordsman's round is two iterative steps (+6/+1, PHB p.143) times the hit's average",
            "bab " + bab + " got " + got + " expected " + expected);

        int beastMelee = beast.Stats.GetMeleeAttackBonus();
        List<NaturalAttackDefinition> naturals = beast.Stats.GetValidNaturalAttacks();
        float natExpected = 0f;
        foreach (NaturalAttackDefinition a in naturals)
            natExpected += Mathf.Max(1, a.Count) * AttackValue.HitChance(beastMelee + (a.IsPrimary ? 0 : -5), ac) * AttackValue.AverageNaturalDamage(beast.Stats, a);
        float natGot = AttackValue.ExpectedRoundDamage(beast, ac);
        Assert(naturals.Count == 2 && Mathf.Approximately(natGot, natExpected),
            "a claw/claw/bite round counts each claw and the secondary bite at -5 (MM p.312)", "got " + natGot + " expected " + natExpected);
        Assert(AttackValue.ExpectedRoundDamage(null, ac) == 0f, "no creature deals no damage");
        Assert(AttackValue.ExpectedRoundDamage(fighter, 40) < AttackValue.ExpectedRoundDamage(fighter, 10),
            "a higher AC lowers the expected damage");
    }
}
}
#endif
