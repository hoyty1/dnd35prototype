using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// CMB-003: the weapon damage modifier is built in one place (CharacterController.BuildWeaponDamageBonus) and every
/// weapon attack adds it. Checks the terms against the rules: Strength x1, x1-1/2 two-handed, x1/2 off hand (PHB p.134;
/// CMB-008), a natural attack's own share (MM p.312), Power Attack doubled two-handed (PHB p.98), Weapon Specialization
/// +2 (PHB p.102), morale from Inspire Courage (PHB p.29), Sickened -2 (DMG p.301), a template smite's per-attack
/// bonus (no longer written into the morale fields), the Destruction smite's cleric-level damage (PHB p.186; CHR-005),
/// and that an attack's weapon damage is its dice roll plus the listed terms. Needs Play mode (CharacterController.Init).
/// </summary>
public static class WeaponDamageModifierTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();

        Debug.Log("========== WEAPON DAMAGE MODIFIER TESTS (CMB-003) ==========");

        TestOneHandedStrengthAndMorale();
        TestTwoHandedStrengthAndPowerAttack();
        TestOffHandHalfStrength();
        TestWeaponSpecializationAndSickened();
        TestNaturalAttackStrengthShare();
        TestTermsAddUpToTotal();
        TestAttackDamageIsDicePlusModifier();
        TestSmiteIsPerAttack();
        TestDestructionSmiteDamage();
        TestDestructionSmiteIsMeleeOnly();
        TestBaneEnhancementOnDamage();
        TestFlurryFullStrength();
        TestDivineFavorScales();

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
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

    /// <summary>A Human of the class at level 3 (BAB 3) with STR <paramref name="str"/>, at <paramref name="pos"/>.</summary>
    private static CharacterController Create(string name, int str, Vector2Int pos, string className = "Fighter")
    {
        var go = new GameObject($"{name}_WeaponDamageTest");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        var stats = new CharacterStats(
            name: name, level: 3, characterClass: className,
            str: str, dex: 10, con: 12, wis: 10, intelligence: 10, cha: 10,
            bab: 3, armorBonus: 0, shieldBonus: 0,
            damageDice: 8, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 24, raceName: "Human");
        stats.BaseAttackBonusOverride = 3;
        controller.Init(stats, pos, null, null);
        inventory.Init(stats);
        controller.GridPosition = pos;
        return controller;
    }

    /// <summary>A defender with plenty of hit points, so repeated attacks never kill it.</summary>
    private static CharacterController CreateDefender(Vector2Int pos)
    {
        CharacterController d = Create("Defender", 10, pos);
        d.Stats.AdjustMaxHP(1000);
        d.Stats.CurrentHP += 1000;
        return d;
    }

    private static ItemData Equip(CharacterController c, ItemID id, EquipSlot slot)
    {
        ItemData item = ItemDatabase.CloneItem(id);
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        inv.CharacterInventory.DirectEquip(item, slot);
        inv.CharacterInventory.RecalculateStats();
        return item;
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        foreach (CharacterController c in controllers)
        {
            if (c != null)
                Object.DestroyImmediate(c.gameObject);
        }
    }

    private static bool HasTerm(WeaponDamageBreakdown d, string label, int value)
    {
        foreach (AttackModifierBreakdownEntry t in d.GetTerms())
        {
            if (t.Label == label && t.Value == value)
                return true;
        }
        return false;
    }

    // STR 16 (+3) with a longsword in one hand: +3; Inspire Courage +1 is a morale term (PHB p.29, p.134).
    private static void TestOneHandedStrengthAndMorale()
    {
        CharacterController a = null;
        try
        {
            a = Create("Inspired", 16, new Vector2Int(0, 0));
            ItemData sword = Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            a.Stats.ApplyInspireCourage(1);
            WeaponDamageBreakdown d = a.BuildWeaponDamageBonus(sword, false, false, default(AttackCalculator.FeatModifiers));
            Assert(d.StrengthBonus == 3 && d.StrengthLabel == "STR", "One-handed longsword adds STR x1 (PHB p.134)", $"got {d.StrengthBonus} '{d.StrengthLabel}'");
            Assert(d.MoraleBonus == 1 && HasTerm(d, "Inspire Courage", 1), "Inspire Courage reaches weapon damage, listed by its source (PHB p.29)", d.Describe());
            Assert(d.Total == 4, "Longsword STR 16 + Inspire Courage +1 = +4", d.Describe());
        }
        finally { Cleanup(a); }
    }

    // STR 16 with a greatsword: +4 (1-1/2 x 3, rounded down); Power Attack 2 adds 4 two-handed (PHB p.98).
    private static void TestTwoHandedStrengthAndPowerAttack()
    {
        CharacterController a = null, t = null;
        try
        {
            a = Create("TwoHander", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0));
            a.Stats.Feats.Add("Power Attack");
            ItemData greatsword = Equip(a, ItemID.WeaponGreatsword, EquipSlot.RightHand);
            a.SetPowerAttack(2);
            AttackBonusBreakdown atk = a.BuildAttackBonus(t, greatsword, false, null, false, 0, 3, CharacterController.IsWeaponTwoHanded(greatsword));
            WeaponDamageBreakdown d = a.BuildWeaponDamageBonus(greatsword, false, false, atk.Feats);
            Assert(d.StrengthBonus == 4, "Two-handed greatsword adds 1-1/2 x STR, rounded down (PHB p.134)", $"got {d.StrengthBonus}");
            Assert(d.PowerAttackBonus == 4 && HasTerm(d, "Power Attack", 4), "Power Attack 2 adds 4 damage with a two-handed weapon (PHB p.98)", d.Describe());
            Assert(d.Total == 8, "Greatsword STR 16 with Power Attack 2 = +8", d.Describe());
        }
        finally { Cleanup(a, t); }
    }

    // CMB-008: an off-hand attack through Attack(isOffHandAttack: true) adds half the Strength bonus (PHB p.134).
    private static void TestOffHandHalfStrength()
    {
        CharacterController a = null, t = null;
        try
        {
            a = Create("OffHand", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            ItemData shortsword = Equip(a, ItemID.WeaponShortsword, EquipSlot.LeftHand);
            CombatResult r = a.Attack(t, false, 0, null, null, null, shortsword, 0, isOffHandAttack: true);
            Assert(r != null && r.HasWeaponDamageBonus, "The off-hand attack records its damage terms");
            Assert(r != null && r.WeaponDamageBonus.StrengthBonus == 1 && r.WeaponDamageBonus.StrengthLabel == "0.5× STR",
                "An off-hand attack adds 1/2 STR: +1 of +3 (PHB p.134; CMB-008)",
                r != null ? r.WeaponDamageBonus.Describe() : "no result");
            // The same weapon as an override without the flag (an AoO with the off-hand weapon, a weapon thrown from the
            // off hand): still the off hand, so still 1/2 STR. The main weapon as an override keeps full STR.
            CombatResult aoo = a.Attack(t, false, 0, null, null, null, shortsword, 0);
            Assert(aoo != null && aoo.WeaponDamageBonus.StrengthBonus == 1,
                "An attack with the off-hand weapon as an override adds 1/2 STR without the off-hand flag (PHB p.134; CMB-008)",
                aoo != null ? aoo.WeaponDamageBonus.Describe() : "no result");
            CombatResult main = a.Attack(t, false, 0, null, null, null, a.GetEquippedMainWeapon(), 0);
            Assert(main != null && main.WeaponDamageBonus.StrengthBonus == 3, "The main weapon as an override adds full STR",
                main != null ? main.WeaponDamageBonus.Describe() : "no result");
        }
        finally { Cleanup(a, t); }
    }

    // Weapon Specialization +2 with the chosen weapon (PHB p.102); Sickened -2 on weapon damage (DMG p.301).
    private static void TestWeaponSpecializationAndSickened()
    {
        CharacterController a = null;
        try
        {
            a = Create("Specialist", 16, new Vector2Int(0, 0));
            a.Stats.Feats.Add("Weapon Focus");
            a.Stats.Feats.Add("Weapon Specialization");
            a.Stats.WeaponFocusWeapons.Add("Longsword");
            ItemData sword = Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            a.ApplyCondition(CombatConditionType.Sickened, 5, "Test");
            AttackBonusBreakdown atk = a.BuildAttackBonus(null, sword, false, null, false, 0, 3, false);
            WeaponDamageBreakdown d = a.BuildWeaponDamageBonus(sword, false, false, atk.Feats);
            Assert(d.WeaponSpecializationBonus == 2 && HasTerm(d, "Weapon Specialization", 2), "Weapon Specialization adds +2 with the chosen weapon (PHB p.102)", d.Describe());
            Assert(d.ConditionModifier == -2 && HasTerm(d, "Sickened", -2), "Sickened takes -2 on weapon damage rolls (DMG p.301)", d.Describe());
            Assert(d.Total == 3, "Longsword STR 16 + Weapon Specialization 2 - Sickened 2 = +3", d.Describe());
        }
        finally { Cleanup(a); }
    }

    // A natural attack's Strength share is its own (MM p.312): a lone bite 1-1/2 x STR, a secondary attack 1/2.
    private static void TestNaturalAttackStrengthShare()
    {
        CharacterController a = null;
        try
        {
            a = Create("Beast", 16, new Vector2Int(0, 0));
            var bite = new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, IsPrimary = true };
            var claw = new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, IsPrimary = false };
            a.Stats.SetNaturalAttacks(new List<NaturalAttackDefinition> { bite, claw });
            WeaponDamageBreakdown b = a.BuildWeaponDamageBonus(null, false, false, default(AttackCalculator.FeatModifiers), naturalAttack: bite);
            WeaponDamageBreakdown c = a.BuildWeaponDamageBonus(null, false, false, default(AttackCalculator.FeatModifiers), naturalAttack: claw);
            Assert(b.StrengthBonus == 4 && b.StrengthLabel == "1.5× STR", "A bite with 1-1/2 x STR adds +4 at STR 16 (MM p.312)", b.Describe());
            Assert(c.StrengthBonus == 1 && c.StrengthLabel == "0.5× STR", "A secondary claw adds 1/2 STR: +1 at STR 16 (MM p.312)", c.Describe());
        }
        finally { Cleanup(a); }
    }

    // The listed terms add up to the total the attack adds.
    private static void TestTermsAddUpToTotal()
    {
        var d = new WeaponDamageBreakdown
        {
            StrengthBonus = 3, StrengthLabel = "STR", WeaponBonus = 1, EnhancementBonus = 2, MaterialModifier = -1,
            PowerAttackBonus = 2, PointBlankShotBonus = 0, WeaponSpecializationBonus = 2, MoraleBonus = 1, MoraleLabel = "Prayer",
            ConditionModifier = -2, ConditionLabel = "Sickened", SolidFogPenalty = -2, BracersOfArcheryBonus = 0,
            DestructionSmiteBonus = 5, SituationalBonus = 4, SituationalLabel = "Smite"
        };
        int sum = 0;
        foreach (AttackModifierBreakdownEntry t in d.GetTerms())
            sum += t.Value;
        Assert(sum == d.Total && d.Total == 15, "The breakdown's terms add up to its total", $"terms {sum}, total {d.Total}: {d.Describe()}");
        Assert(d.Describe().EndsWith("= +15"), "Describe ends with the total", d.Describe());
    }

    // Every hit: weapon damage = dice roll + the recorded modifier (added once on a critical, CMB-004), and the
    // detailed log lists the morale term.
    private static void TestAttackDamageIsDicePlusModifier()
    {
        CharacterController a = null, t = null;
        try
        {
            a = Create("Hitter", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            a.Stats.ApplyInspireCourage(2);
            int hits = 0;
            bool allAddUp = true;
            string firstMismatch = string.Empty;
            string summary = string.Empty;
            for (int i = 0; i < 40 && hits < 5; i++)
            {
                CombatResult r = a.Attack(t, false, 0, null);
                if (r == null || !r.Hit)
                    continue;
                hits++;
                int want = Mathf.Max(1, r.BaseDamageRoll + r.WeaponDamageBonus.Total + r.TorchCritEnhancementExtra);
                if (!r.HasWeaponDamageBonus || r.Damage != want || r.WeaponDamageBonus.Total != 5)
                {
                    allAddUp = false;
                    if (firstMismatch.Length == 0)
                        firstMismatch = $"roll {r.BaseDamageRoll} + [{r.WeaponDamageBonus.Describe()}] -> damage {r.Damage}";
                }
                if (summary.Length == 0)
                    summary = r.GetDetailedSummary();
            }
            Assert(hits > 0, "The attacker hit at least once in 40 attacks");
            Assert(allAddUp, "Each hit's weapon damage is its dice roll + STR +3 + morale +2 (CMB-003)", firstMismatch);
            Assert(summary.Contains("Inspire Courage"), "The detailed combat log lists the morale damage term", summary);
        }
        finally { Cleanup(a, t); }
    }

    // A template smite's bonuses apply to the one attack as situational terms, and the morale fields stay untouched.
    private static void TestSmiteIsPerAttack()
    {
        CharacterController a = null, t = null;
        try
        {
            a = Create("Smiter", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            int moraleAttack = a.Stats.MoraleAttackBonus, moraleDamage = a.Stats.MoraleDamageBonus;
            CombatResult r = a.Attack(t, false, 0, null, situationalAttackBonus: 3, situationalDamageBonus: 4, situationalLabel: "Smite");
            bool listed = false;
            if (r != null)
            {
                foreach (AttackModifierBreakdownEntry e in r.AttackBuffDebuffModifiers)
                    listed |= e.Label == "Smite" && e.Value == 3;
            }
            Assert(r != null && r.WeaponDamageBonus.SituationalBonus == 4 && HasTerm(r.WeaponDamageBonus, "Smite", 4),
                "The smite's damage bonus is a term of that attack's damage", r != null ? r.WeaponDamageBonus.Describe() : "no result");
            Assert(listed, "The smite's attack bonus is listed on that attack roll");
            Assert(a.Stats.MoraleAttackBonus == moraleAttack && a.Stats.MoraleDamageBonus == moraleDamage,
                "The smite leaves the morale fields untouched");
        }
        finally { Cleanup(a, t); }
    }

    // CHR-005: the Destruction smite's damage bonus (cleric level, PHB p.186) is read before the attack roll consumes it.
    private static void TestDestructionSmiteDamage()
    {
        CharacterController a = null, t = null;
        try
        {
            a = Create("Destroyer", 14, new Vector2Int(0, 0), "Cleric");
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            int want = Mathf.Max(1, a.Stats.GetClassLevel("Cleric"));
            a.Stats.DestructionSmiteActive = true;
            CombatResult r = a.Attack(t, false, 0, null);
            Assert(r != null && r.WeaponDamageBonus.DestructionSmiteBonus == want && HasTerm(r.WeaponDamageBonus, "Destruction smite", want),
                "The smiting attack's damage carries +cleric level (PHB p.186; CHR-005)", r != null ? r.WeaponDamageBonus.Describe() : "no result");
            Assert(!a.Stats.DestructionSmiteActive, "The smite is used up by the attack");
            Assert(r == null || !r.Hit || r.Damage == Mathf.Max(1, r.BaseDamageRoll + r.WeaponDamageBonus.Total + r.TorchCritEnhancementExtra),
                "A smiting hit's damage includes the smite bonus");
            CombatResult next = a.Attack(t, false, 0, null);
            Assert(next != null && next.WeaponDamageBonus.DestructionSmiteBonus == 0, "The next attack has no smite bonus");
        }
        finally { Cleanup(a, t); }
    }

    // PHB p.186: the Destruction smite is a melee attack, so a thrown javelin neither gains it nor uses it up.
    private static void TestDestructionSmiteIsMeleeOnly()
    {
        CharacterController a = null, t = null;
        try
        {
            a = Create("Thrower", 14, new Vector2Int(0, 0), "Cleric");
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponJavelin, EquipSlot.RightHand);
            a.Stats.DestructionSmiteActive = true;
            var thrown = new RangeInfo { IsMelee = false, IsInRange = true, IsThrownWeapon = true, SquareDistance = 1, DistanceFeet = 5,
                RangeIncrementFeet = 30, MaxRangeFeet = 150, MaxRangeSquares = 30, IncrementNumber = 1 };
            CombatResult r = a.Attack(t, false, 0, null, thrown);
            Assert(r != null && r.HasWeaponDamageBonus && r.WeaponDamageBonus.IsRangedAttack && r.WeaponDamageBonus.DestructionSmiteBonus == 0,
                "A thrown attack gets no Destruction smite damage (PHB p.186: a melee attack)", r != null ? r.WeaponDamageBonus.Describe() : "no result");
            Assert(a.Stats.DestructionSmiteActive, "A thrown attack leaves the smite unused");
        }
        finally { Cleanup(a, t); }
    }

    // DMG p.224: against its designated foe a bane weapon's effective enhancement bonus is +2 better, damage included.
    private static void TestBaneEnhancementOnDamage()
    {
        CharacterController a = null, t = null;
        try
        {
            a = Create("Baner", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0));
            ItemData sword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
            EnchantmentFactory.ApplyEnchantment(sword, 1, new[] { EnchantmentType.Bane }, t.Stats.CreatureType);
            InventoryComponent inv = a.GetComponent<InventoryComponent>();
            inv.CharacterInventory.DirectEquip(sword, EquipSlot.RightHand);
            inv.CharacterInventory.RecalculateStats();
            CombatResult r = a.Attack(t, false, 0, null);
            Assert(!string.IsNullOrEmpty(t.Stats.CreatureType) && r != null && r.WeaponDamageBonus.EnhancementBonus == 1
                && r.WeaponDamageBonus.BaneBonus == 2 && HasTerm(r.WeaponDamageBonus, "bane", 2),
                "A +1 bane longsword against its foe adds enhancement +1 and bane +2 to damage (DMG p.224)",
                r != null ? r.WeaponDamageBonus.Describe() : "no result");
            t.Stats.CreatureType = t.Stats.CreatureType + "_other";
            CombatResult other = a.Attack(t, false, 0, null);
            Assert(other != null && other.WeaponDamageBonus.BaneBonus == 0, "No bane term against another creature type",
                other != null ? other.WeaponDamageBonus.Describe() : "no result");
        }
        finally { Cleanup(a, t); }
    }

    // PHB p.41: a flurry adds the full Strength bonus, not 1-1/2 times it, even with a quarterstaff in both hands.
    private static void TestFlurryFullStrength()
    {
        CharacterController a = null, t = null;
        try
        {
            a = Create("Flurrier", 16, new Vector2Int(0, 0), "Monk");
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponQuarterstaff, EquipSlot.RightHand);
            FullAttackResult f = a.FlurryOfBlows(t, false, 0, null);
            bool allFull = f != null && f.Attacks.Count > 0;
            string got = string.Empty;
            if (f != null)
            {
                foreach (CombatResult r in f.Attacks)
                {
                    if (r == null || !r.HasWeaponDamageBonus || r.WeaponDamageBonus.StrengthBonus != 3)
                    {
                        allFull = false;
                        got = r != null ? r.WeaponDamageBonus.Describe() : "null attack";
                    }
                }
            }
            Assert(a.Stats.IsMonk && allFull, "A quarterstaff flurry adds STR x1 (+3), not x1-1/2 (PHB p.41)", got);
        }
        finally { Cleanup(a, t); }
    }

    // PHB p.224: Divine Favor gives +1 per three caster levels (at least +1) on attack and weapon damage rolls.
    private static void TestDivineFavorScales()
    {
        CharacterController a = null;
        try
        {
            a = Create("Favored", 14, new Vector2Int(0, 0), "Cleric");
            // Create() builds no StatusEffectManager (the game adds one in GameManager's party and NPC setup).
            if (a.StatusEffectManager == null)
                a.gameObject.AddComponent<StatusEffectManager>().Init(a.Stats);
            int damageBefore = a.Stats.MoraleDamageBonus;
            ActiveSpellEffect low = a.StatusEffectManager != null
                ? a.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.DIVINE_FAVOR).Clone(), "Test", 2)
                : null;
            Assert(low != null && low.AppliedAttackBonus == 1 && low.AppliedDamageBonus == 1, "Divine Favor at caster level 2 gives +1 (minimum, PHB p.224)");
            if (low != null)
                a.StatusEffectManager.RemoveEffect(low);
            ActiveSpellEffect high = a.StatusEffectManager != null
                ? a.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.DIVINE_FAVOR).Clone(), "Test", 7)
                : null;
            Assert(high != null && high.AppliedAttackBonus == 2 && high.AppliedDamageBonus == 2
                && a.Stats.MoraleDamageBonus == damageBefore + 2,
                "Divine Favor at caster level 7 gives +2 on attack and damage (PHB p.224)", $"damage bonus {a.Stats.MoraleDamageBonus}");
        }
        finally { Cleanup(a); }
    }
}
}
