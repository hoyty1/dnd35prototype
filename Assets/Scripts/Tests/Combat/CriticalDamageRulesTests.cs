using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// CMB-004 (owner ruling 2026-10-08: follow RAW): a confirmed critical hit rolls the weapon damage, every static
/// modifier included, the multiplier number of times and adds the rolls (PHB p.134 "Multiplying Damage", p.140
/// "Critical Hits"); extra dice such as sneak attack are added once (same pages); a creature not subject to critical
/// hits takes normal damage (MM p.307-317 type traits; CMB-064) and no sneak attack (PHB p.50), though a confirmed threat
/// still applies the weapon's critical-hit effects (DMG p.222); a coup de grace is an automatic critical hit under the same
/// rule (PHB p.153), with the same extra dice, bane bonus and Fortification as any critical. Checks the shared roll
/// (WeaponDamageRoll) with injected dice, then the attack core and the coup de grace with the scenario dice filter forcing
/// the d20s and the weapon dice. Needs Play mode (CharacterController.Init).
/// </summary>
public static class CriticalDamageRulesTests
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

        Debug.Log("========== CRITICAL DAMAGE RULES TESTS (CMB-004) ==========");

        TestRollerMultipliesDiceAndModifier();
        TestRollerNormalHitRollsOnce();
        TestRollerMinimumOneOnTheTotal();
        TestLongswordCriticalMultipliesModifier();
        TestGreataxeCriticalWithPowerAttack();
        TestSneakAttackAddedOnce();
        TestCritImmuneTargetTakesNormalDamage();
        TestBurstDiceApplyToCritImmuneTarget();
        TestElementalAndPlantImmuneToCritsAndSneakAttack();
        TestCoupDeGraceMultipliesWeaponNotSneak();
        TestCoupDeGraceAddsRidersOnceLikeAnAttack();
        TestCoupDeGraceAgainstFortification();

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
        var go = new GameObject($"{name}_CritDamageTest");
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

    /// <summary>A defender with plenty of hit points, so a critical never kills it.</summary>
    private static CharacterController CreateDefender(Vector2Int pos, string creatureType = null)
    {
        CharacterController d = Create("Defender", 10, pos);
        d.Stats.AdjustMaxHP(1000);
        d.Stats.CurrentHP += 1000;
        if (!string.IsNullOrEmpty(creatureType))
            d.Stats.CreatureType = creatureType;
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

    /// <summary>
    /// Every d20 rolls <paramref name="d20"/>, every weapon damage die <paramref name="weaponDie"/>, and every other d6
    /// (sneak attack) <paramref name="d6"/>; counts the d20s in <see cref="_d20Count"/>.
    /// </summary>
    private static System.Func<int, string, int, int> Forced(int d20, int weaponDie, int d6)
    {
        return (sides, ctx, natural) =>
        {
            if (sides == 20)
            {
                _d20Count++;
                return d20;
            }
            if (ctx == WeaponDamageRoll.DiceContext)
                return Mathf.Min(weaponDie, sides);
            if (sides == 6)
                return d6;
            return natural;
        };
    }

    private static int _d20Count;

    // PHB p.134's own example: a x3 greataxe with +4 rolls 1d12+4 three times, the same as 3d12+12.
    private static void TestRollerMultipliesDiceAndModifier()
    {
        int[] dice = { 7, 3, 10 };
        int next = 0;
        WeaponDamageRoll r = WeaponDamageRoll.Roll(1, 12, 4, 3, (count, sides) => dice[next++]);
        Assert(r.Multiplier == 3 && r.DicePerRoll.Length == 3 && r.DiceTotal == 20, "A x3 critical rolls the weapon dice three times", $"rolls {r.DicePerRoll.Length}, dice {r.DiceTotal}");
        Assert(r.StaticTotal == 12 && r.Total == 32, "Each roll adds the +4: 7+4 + 3+4 + 10+4 = 32 (PHB p.134)", $"static {r.StaticTotal}, total {r.Total}");
        Assert(r.DiceLabel == "3d12" && r.IsCritical, "The roll is labelled 3d12 in all", r.DiceLabel);
        Assert(r.Describe().EndsWith("= 32") && r.Describe().Contains("[1d12(7) + 4]"), "The logged roll lists each roll with its modifier and adds up", r.Describe());
    }

    private static void TestRollerNormalHitRollsOnce()
    {
        int calls = 0;
        WeaponDamageRoll r = WeaponDamageRoll.Roll(2, 6, 3, 1, (count, sides) => { calls++; return 9; });
        Assert(calls == 1 && r.Total == 12 && !r.IsCritical && r.DiceLabel == "2d6", "A normal hit rolls 2d6 + 3 once", $"calls {calls}, total {r.Total}, {r.Describe()}");
    }

    // A hit deals at least 1 damage (PHB p.134); on a critical the minimum applies to the total of the rolls.
    private static void TestRollerMinimumOneOnTheTotal()
    {
        WeaponDamageRoll r = WeaponDamageRoll.Roll(1, 4, -5, 2, (count, sides) => 1);
        Assert(r.Unclamped == -8 && r.Total == 1, "1d4-5 twice rolling 1s totals -8, raised to the minimum 1", $"unclamped {r.Unclamped}, total {r.Total}");
        Assert(r.Describe().Contains("minimum 1"), "The log notes the minimum", r.Describe());
    }

    // Longsword (19-20/x2), STR 16 +3, Inspire Courage +2 (PHB p.29): (5 + 5) x 2 rolls = 20, on the attack path.
    private static void TestLongswordCriticalMultipliesModifier()
    {
        CharacterController a = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            a = Create("Critter", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            a.Stats.ApplyInspireCourage(2);
            ScenarioHooks.RollFilter = Forced(20, 5, 1);
            CombatResult r = a.Attack(t, false, 0, null);
            Assert(r != null && r.Hit && r.CritConfirmed && r.DamageRollCount == 2, "A natural 20 confirmed by a 20 is a x2 critical",
                r != null ? $"hit {r.Hit}, confirmed {r.CritConfirmed}, rolls {r.DamageRollCount}" : "no result");
            Assert(r != null && r.WeaponDamageBonus.Total == 5, "The modifier per roll is STR +3 and Inspire Courage +2", r != null ? r.WeaponDamageBonus.Describe() : "no result");
            Assert(r != null && r.BaseDamageRoll == 10 && r.Damage == 20,
                "The longsword critical deals (1d8 + 5) twice = 5+5 + 5+5 = 20, not 2d8 + 5 = 15 (PHB p.140; CMB-004)",
                r != null ? $"dice {r.BaseDamageRoll}, damage {r.Damage}" : "no result");
            Assert(r != null && r.FinalDamageDealt == 20, "The defender takes the 20", r != null ? r.FinalDamageDealt.ToString() : "no result");
            string log = r != null ? r.GetDetailedSummary() : string.Empty;
            Assert(log.Contains("Critical ×2") && log.Contains("2d8") && log.Contains("STR +3 ×2"), "The detailed log shows both rolls and each term times the multiplier", log);
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(a, t);
        }
    }

    // Greataxe (x3) two-handed, STR 16: 1-1/2 x 3 = +4, Power Attack 2 two-handed +4 (PHB p.98): (6 + 8) x 3 = 42.
    private static void TestGreataxeCriticalWithPowerAttack()
    {
        CharacterController a = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            a = Create("Axer", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0));
            a.Stats.Feats.Add("Power Attack");
            Equip(a, ItemID.WeaponGreataxe, EquipSlot.RightHand);
            a.SetPowerAttack(2);
            ScenarioHooks.RollFilter = Forced(20, 6, 1);
            CombatResult r = a.Attack(t, false, 0, null);
            Assert(r != null && r.CritConfirmed && r.DamageRollCount == 3 && r.CritDamageDice == "3d12", "The greataxe critical is x3 with 3d12 in all",
                r != null ? $"confirmed {r.CritConfirmed}, rolls {r.DamageRollCount}, dice {r.CritDamageDice}" : "no result");
            Assert(r != null && r.WeaponDamageBonus.Total == 8 && r.WeaponDamageBonus.PowerAttackBonus == 4,
                "The modifier per roll is 1-1/2 x STR +4 and Power Attack +4", r != null ? r.WeaponDamageBonus.Describe() : "no result");
            Assert(r != null && r.Damage == 42, "Power Attack and Strength are added on every roll: (6 + 8) x 3 = 42 (PHB p.134)",
                r != null ? $"damage {r.Damage}" : "no result");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(a, t);
        }
    }

    // A rogue 3 (2d6 sneak attack) with a rapier (18-20/x2), STR 14 +2, against a stunned defender (denied DEX):
    // weapon (4 + 2) x 2 = 12, sneak attack 3 + 3 = 6 added once, 18 in all (PHB p.140: sneak attack is not multiplied).
    private static void TestSneakAttackAddedOnce()
    {
        CharacterController a = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            a = Create("Stabber", 14, new Vector2Int(0, 0), "Rogue");
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponRapier, EquipSlot.RightHand);
            t.ApplyCondition(CombatConditionType.Stunned, 5, "Test");
            ScenarioHooks.RollFilter = Forced(20, 4, 3);
            CombatResult r = a.Attack(t, false, 0, null);
            Assert(r != null && r.CritConfirmed && r.SneakAttackApplied, "The rogue's critical against a stunned defender adds sneak attack",
                r != null ? $"confirmed {r.CritConfirmed}, sneak {r.SneakAttackApplied} ({r.SneakAttackTriggerReason})" : "no result");
            Assert(r != null && r.Damage == 12, "The rapier critical deals (1d6 + 2) twice = 12", r != null ? $"weapon damage {r.Damage} [{r.WeaponDamageBonus.Describe()}]" : "no result");
            Assert(r != null && r.SneakAttackDamage == 6 && r.RawTotalDamage == 18,
                "Sneak attack 2d6 = 6 is added once, not doubled: 12 + 6 = 18 (PHB p.134, p.140)",
                r != null ? $"sneak {r.SneakAttackDamage}, raw {r.RawTotalDamage}" : "no result");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(a, t);
        }
    }

    // MM p.317: undead are not subject to critical hits. A natural 20 still hits and threatens, and the confirmation is
    // rolled as usual (DMG p.222: a confirmed critical still applies the weapon's critical-hit effects), but the damage
    // is rolled once: 5 + 5 = 10.
    private static void TestCritImmuneTargetTakesNormalDamage()
    {
        CharacterController a = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            a = Create("Bonebreaker", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0), "Undead");
            Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            a.Stats.ApplyInspireCourage(2);
            _d20Count = 0;
            ScenarioHooks.RollFilter = Forced(20, 5, 1);
            CombatResult r = a.Attack(t, false, 0, null);
            Assert(r != null && r.Hit && r.IsCritThreat && !r.CritConfirmed && r.CritImmunityPrevented,
                "A threat against an undead target is not a critical (CMB-064)",
                r != null ? $"threat {r.IsCritThreat}, confirmed {r.CritConfirmed}, immune {r.CritImmunityPrevented}" : "no result");
            Assert(_d20Count == 2 && r != null && r.CritEffectsOnly,
                "The threat is still confirmed, for the weapon's critical-hit effects only (DMG p.222)",
                $"{_d20Count} d20s, effects only {(r != null && r.CritEffectsOnly)}");
            Assert(r != null && r.DamageRollCount == 1 && r.Damage == 10, "The undead target takes normal damage: 1d8 + 5 = 10",
                r != null ? $"rolls {r.DamageRollCount}, damage {r.Damage}" : "no result");
            string log = r != null ? r.GetDetailedSummary() : string.Empty;
            Assert(log.Contains("immune to critical hits"), "The detailed log says the target is immune to critical hits", log);
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(a, t);
        }
    }

    // DMG p.222 ("Magic Weapons and Critical Hits"): against a creature not subject to critical hits a threat is still
    // confirmed, and a confirmed critical applies the weapon's critical-hit effect without multiplying the weapon damage.
    // A flaming burst longsword, STR 16 +3, against an undead: 1d8(5) + 3 = 8 once, flaming 1d6(1), burst 1d10(7): 16.
    private static void TestBurstDiceApplyToCritImmuneTarget()
    {
        CharacterController a = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            a = Create("Burster", 16, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0), "Undead");
            ItemData sword = Equip(a, ItemID.WeaponLongsword, EquipSlot.RightHand);
            sword.Enchantment = new ItemEnchantmentData();
            sword.Enchantment.Abilities.Add(EnchantmentType.FlamingBurst);
            System.Func<int, string, int, int> forced = Forced(20, 5, 1);
            ScenarioHooks.RollFilter = (sides, ctx, natural) => sides == 10 ? 7 : forced(sides, ctx, natural);
            CombatResult r = a.Attack(t, false, 0, null);
            Assert(r != null && r.Hit && r.CritEffectsOnly && !r.CritConfirmed && r.DamageRollCount == 1 && r.Damage == 8,
                "A confirmed threat on an undead deals the weapon damage once: 1d8 + 3 = 8",
                r != null ? $"effects only {r.CritEffectsOnly}, confirmed {r.CritConfirmed}, rolls {r.DamageRollCount}, damage {r.Damage}" : "no result");
            Assert(r != null && r.RawTotalDamage == 16,
                "The flaming burst's critical 1d10 still applies, with the flaming 1d6: 8 + 1 + 7 = 16 (DMG p.222, p.224)",
                r != null ? $"raw {r.RawTotalDamage}" : "no result");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(a, t);
        }
    }

    // MM p.308 (elemental), p.313 (plant) and p.316 (swarm subtype): not subject to critical hits; PHB p.50: a creature
    // immune to critical hits is not vulnerable to sneak attacks. "Swarm" is a humanoid-typed defender flagged IsSwarm.
    private static void TestElementalAndPlantImmuneToCritsAndSneakAttack()
    {
        foreach (string type in new[] { "Elemental", "Plant", "Swarm" })
        {
            CharacterController a = null, t = null;
            System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
            try
            {
                a = Create("Stabber" + type, 14, new Vector2Int(0, 0), "Rogue");
                t = CreateDefender(new Vector2Int(1, 0), type == "Swarm" ? null : type);
                if (type == "Swarm")
                    t.Stats.IsSwarm = true;
                Equip(a, ItemID.WeaponRapier, EquipSlot.RightHand);
                ScenarioHooks.RollFilter = Forced(20, 4, 3);
                // Flanking (passed in) would allow sneak attack against a vulnerable target (PHB p.50, p.153).
                CombatResult r = a.Attack(t, true, 2, "Partner");
                Assert(r != null && r.Hit && !r.CritConfirmed && r.CritImmunityPrevented && r.Damage == 6,
                    type + ": a threat is a normal hit, (1d6 + 2) once = 6",
                    r != null ? $"confirmed {r.CritConfirmed}, immune {r.CritImmunityPrevented}, damage {r.Damage}" : "no result");
                Assert(r != null && !r.SneakAttackApplied && r.SneakAttackDamage == 0,
                    type + ": no sneak attack against a creature immune to critical hits (PHB p.50)",
                    r != null ? $"sneak {r.SneakAttackApplied} {r.SneakAttackDamage} ({r.SneakAttackTriggerReason})" : "no result");
            }
            finally
            {
                ScenarioHooks.RollFilter = saved;
                Cleanup(a, t);
            }
        }
    }

    // A coup de grace is an automatic critical hit (PHB p.153): the rapier x2 rolls (4 + 2) twice = 12; the rogue 3's
    // sneak attack 2d6 = 6 is added once (PHB p.140): 18. Before the fix it was (4 + 2 + 6) x 2 = 24.
    private static void TestCoupDeGraceMultipliesWeaponNotSneak()
    {
        CharacterController a = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            a = Create("Assassin", 14, new Vector2Int(0, 0), "Rogue");
            t = CreateDefender(new Vector2Int(1, 0));
            Equip(a, ItemID.WeaponRapier, EquipSlot.RightHand);
            t.ApplyCondition(CombatConditionType.Paralyzed, 5, "Test");
            ScenarioHooks.RollFilter = Forced(20, 4, 3);
            SpecialAttackResult r = a.ExecuteSpecialAttack(SpecialAttackType.CoupDeGrace, t);
            Assert(r != null && r.Success, "The coup de grace against a paralyzed defender is delivered", r != null ? r.Log : "no result");
            Assert(r != null && r.WeaponDamageRoll.Multiplier == 2 && r.WeaponDamageRoll.StaticPerRoll == 2 && r.WeaponDamageRoll.Total == 12,
                "The coup de grace rolls the rapier's damage, STR +2 included, twice: 12 (PHB p.134, p.153)",
                r != null ? r.WeaponDamageRoll.Describe() : "no result");
            Assert(r != null && r.SneakAttackDamage == 6 && r.RawDamage == 18,
                "Sneak attack is added once to the coup de grace: 12 + 6 = 18, not (6 + 6) x 2 = 24 (CMB-004)",
                r != null ? $"sneak {r.SneakAttackDamage}, raw {r.RawDamage}" : "no result");
            Assert(r != null && r.Log != null && r.Log.Contains("not multiplied") && r.Log.Contains("= 18;"),
                "The coup de grace log shows the rolls and the sneak attack added once, adding up to 18", r != null ? r.Log : "no result");

            // PHB p.153: a creature immune to critical hits cannot be coup de graced. Each target is made helpless
            // (unconscious) first, so the refusal is for the immunity, not for a missing helpless state.
            foreach (string immuneType in new[] { "Undead", "Elemental", "Plant" })
            {
                CharacterController immune = CreateDefender(new Vector2Int(0, 1), immuneType);
                try
                {
                    immune.ApplyCondition(CombatConditionType.Unconscious, 5, "Test");
                    bool helpless = immune.IsHelplessForCoupDeGrace();
                    SpecialAttackResult refused = a.ExecuteSpecialAttack(SpecialAttackType.CoupDeGrace, immune);
                    Assert(helpless && refused != null && !refused.Success && refused.Log != null && refused.Log.Contains("immune to critical hits"),
                        $"No coup de grace against a helpless {immuneType.ToLowerInvariant()}: it is immune to critical hits (PHB p.153)",
                        $"helpless {helpless}, log '{(refused != null ? refused.Log : "no result")}'");
                }
                finally { Cleanup(immune); }
            }
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(a, t);
        }
    }

    /// <summary>A rapier with flaming (1d6 fire) and bane against <paramref name="baneType"/> (+2 enhancement, +2d6), DMG p.224.</summary>
    private static ItemData EquipFlamingBaneRapier(CharacterController c, string baneType)
    {
        ItemData rapier = Equip(c, ItemID.WeaponRapier, EquipSlot.RightHand);
        rapier.Enchantment = new ItemEnchantmentData();
        rapier.Enchantment.Abilities.Add(EnchantmentType.Flaming);
        rapier.Enchantment.Abilities.Add(EnchantmentType.Bane);
        rapier.Enchantment.BaneCreatureType = baneType;
        c.GetComponent<InventoryComponent>().CharacterInventory.RecalculateStats();
        return rapier;
    }

    // A coup de grace adds the same extra damage as a confirmed critical on the attack path (CMB-004): a flaming bane
    // rapier, STR 14, against a humanoid rolls (4 + 2 STR + 2 bane) twice = 16, then adds flaming 1d6 = 3 and bane
    // 2d6 = 6 once: 25, the same as its confirmed critical.
    private static void TestCoupDeGraceAddsRidersOnceLikeAnAttack()
    {
        CharacterController a = null, t = null, t2 = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            a = Create("Slayer", 14, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0), "Humanoid");
            t2 = CreateDefender(new Vector2Int(0, 1), "Humanoid");
            EquipFlamingBaneRapier(a, "Humanoid");
            t.ApplyCondition(CombatConditionType.Unconscious, 5, "Test");
            ScenarioHooks.RollFilter = Forced(20, 4, 3);

            SpecialAttackResult cdg = a.ExecuteSpecialAttack(SpecialAttackType.CoupDeGrace, t);
            Assert(cdg != null && cdg.Success && cdg.WeaponDamageRoll.StaticPerRoll == 4 && cdg.WeaponDamageRoll.Total == 16,
                "The coup de grace adds bane's +2 to each roll: (4 + 2 + 2) x 2 = 16 (DMG p.224)",
                cdg != null ? cdg.Log : "no result");
            Assert(cdg != null && cdg.ExtraDamage == 9 && cdg.RawDamage == 25,
                "Flaming 3 and bane 6 are added once to the coup de grace: 16 + 9 = 25",
                cdg != null ? $"extra {cdg.ExtraDamage}, raw {cdg.RawDamage}; {cdg.Log}" : "no result");

            CombatResult hit = a.Attack(t2, false, 0, null);
            Assert(hit != null && hit.CritConfirmed && hit.Damage == 16 && hit.RawTotalDamage == 25,
                "A confirmed critical with the same rapier deals the same 16 + 9 = 25 (one rule for both)",
                hit != null ? $"confirmed {hit.CritConfirmed}, weapon {hit.Damage}, raw {hit.RawTotalDamage}" : "no result");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(a, t, t2);
        }
    }

    // Fortification (DMG p.219) can negate a coup de grace's critical like any other: the percentile roll is forced to 1
    // against heavy fortification, so the rapier's damage is rolled once (4 + 2 + 2 = 8) and the riders added: 17.
    private static void TestCoupDeGraceAgainstFortification()
    {
        CharacterController a = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            a = Create("Slayer", 14, new Vector2Int(0, 0));
            t = CreateDefender(new Vector2Int(1, 0), "Humanoid");
            EquipFlamingBaneRapier(a, "Humanoid");
            ItemData armor = Equip(t, ItemID.ArmorChainShirt, EquipSlot.Armor);
            armor.Enchantment = new ItemEnchantmentData();
            armor.Enchantment.Abilities.Add(EnchantmentType.FortificationHeavy);
            t.ApplyCondition(CombatConditionType.Unconscious, 5, "Test");
            System.Func<int, string, int, int> forced = Forced(20, 4, 3);
            ScenarioHooks.RollFilter = (sides, ctx, natural) => sides == 100 ? 1 : forced(sides, ctx, natural);

            SpecialAttackResult cdg = a.ExecuteSpecialAttack(SpecialAttackType.CoupDeGrace, t);
            Assert(cdg != null && cdg.Success && cdg.CritNegated && cdg.WeaponDamageRoll.Multiplier == 1 && cdg.WeaponDamageRoll.Total == 8,
                "Fortification negates the coup de grace's critical: the damage is rolled once, 8",
                cdg != null ? cdg.Log : "no result");
            Assert(cdg != null && cdg.RawDamage == 17 && cdg.Log != null && cdg.Log.Contains("Fortification"),
                "The riders still add 9 (17 in all) and the log names Fortification",
                cdg != null ? $"raw {cdg.RawDamage}; {cdg.Log}" : "no result");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(a, t);
        }
    }
}
}
