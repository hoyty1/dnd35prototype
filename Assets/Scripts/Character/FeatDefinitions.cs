using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// D&D 3.5 Player's Handbook - All General Feats + Metamagic Feats + Item Creation Feats
// Includes: Combat, Ranged, Defensive, TWF, Mounted, Unarmed, Skill, General,
//           Metamagic, Item Creation (placeholder), and Spell Mastery
// ============================================================================

/// <summary>
/// Static database of all D&D 3.5 PHB general feats.
/// Call Init() once before accessing feats.
/// </summary>
public static class FeatDefinitions
{
    private static Dictionary<string, FeatDefinition> _feats;
    private static bool _initialized = false;

    /// <summary>All defined feats, keyed by feat name.</summary>
    public static Dictionary<string, FeatDefinition> AllFeats
    {
        get
        {
            if (!_initialized) Init();
            return _feats;
        }
    }

    /// <summary>Initialize all feat definitions. Safe to call multiple times.</summary>
    public static void Init()
    {
        if (_initialized) return;
        _feats = new Dictionary<string, FeatDefinition>();
        DefineAllFeats();
        _initialized = true;
        Debug.Log($"[Feats] Initialized {_feats.Count} feat definitions.");
    }

    /// <summary>Get a feat definition by name. Returns null if not found.</summary>
    public static FeatDefinition GetFeat(string name)
    {
        if (!_initialized) Init();
        return _feats.ContainsKey(name) ? _feats[name] : null;
    }

    /// <summary>Get all feats a character qualifies for (excluding already owned).</summary>
    public static List<FeatDefinition> GetAvailableFeats(CharacterStats stats, bool fighterBonusOnly = false)
    {
        if (!_initialized) Init();
        var available = new List<FeatDefinition>();
        foreach (var feat in _feats.Values)
        {
            if (fighterBonusOnly && !feat.IsFighterBonus) continue;
            if (!feat.CanTakeMultiple && stats.HasFeat(feat.FeatName)) continue;
            if (feat.MeetsPrerequisites(stats))
                available.Add(feat);
        }
        return available;
    }

    /// <summary>
    /// Get Monk bonus feats available at a specific monk level.
    /// Monks bypass prerequisites for these feats.
    /// Level 1: Improved Grapple, Stunning Fist
    /// Level 2: Combat Reflexes, Deflect Arrows
    /// Level 6: Improved Disarm, Improved Trip
    /// </summary>
    public static List<FeatDefinition> GetMonkBonusFeats(int monkLevel, CharacterStats stats)
    {
        if (!_initialized) Init();
        var available = new List<FeatDefinition>();
        foreach (var feat in _feats.Values)
        {
            if (!feat.IsMonkBonus) continue;
            if (feat.MonkBonusLevel > monkLevel) continue;
            if (!feat.CanTakeMultiple && stats != null && stats.HasFeat(feat.FeatName)) continue;
            available.Add(feat);
        }
        return available;
    }

    /// <summary>
    /// Get Monk bonus feats available at a specific monk bonus feat level (exact match).
    /// Used for sequential selection: level 1 choices, then level 2 choices, etc.
    /// </summary>
    public static List<FeatDefinition> GetMonkBonusFeatsForLevel(int monkBonusLevel, CharacterStats stats)
    {
        if (!_initialized) Init();
        var available = new List<FeatDefinition>();
        foreach (var feat in _feats.Values)
        {
            if (!feat.IsMonkBonus) continue;
            if (feat.MonkBonusLevel != monkBonusLevel) continue;
            if (!feat.CanTakeMultiple && stats != null && stats.HasFeat(feat.FeatName)) continue;
            available.Add(feat);
        }
        return available;
    }

    /// <summary>Get all feats sorted by type for display.</summary>
    public static List<FeatDefinition> GetAllFeatsSorted()
    {
        if (!_initialized) Init();
        var list = new List<FeatDefinition>(_feats.Values);
        list.Sort((a, b) =>
        {
            int typeCompare = a.Type.CompareTo(b.Type);
            return typeCompare != 0 ? typeCompare : string.Compare(a.FeatName, b.FeatName);
        });
        return list;
    }

    // ========================================================================
    // FEAT DEFINITIONS
    // ========================================================================

    private static void DefineAllFeats()
    {
        // ==================== COMBAT FEATS ====================
        DefineCombatFeats();
        // ==================== RANGED COMBAT FEATS ====================
        DefineRangedFeats();
        // ==================== DEFENSIVE FEATS ====================
        DefineDefensiveFeats();
        // ==================== TWO-WEAPON FIGHTING ====================
        DefineTWFFeats();
        // ==================== MOUNTED COMBAT ====================
        DefineMountedFeats();
        // ==================== UNARMED COMBAT ====================
        DefineUnarmedFeats();
        // ==================== SKILL FEATS ====================
        DefineSkillFeats();
        // ==================== GENERAL FEATS ====================
        DefineGeneralFeats();
        // ==================== METAMAGIC FEATS ====================
        DefineMetamagicFeats();
        // ==================== ITEM CREATION FEATS (PLACEHOLDERS) ====================
        DefineItemCreationFeats();
        // ==================== SPELL MASTERY (PLACEHOLDER) ====================
        DefineSpellMastery();
    }

    // ========================================================================
    // COMBAT FEATS
    // ========================================================================
    private static void DefineCombatFeats()
    {
        // --- Power Attack ---
        var powerAttack = new FeatDefinition("Power Attack",
            "On your action, before making attack rolls for a round, you may choose to subtract a number from all melee attack rolls and add the same number to all melee damage rolls. The penalty/bonus applies until your next turn. With a two-handed weapon, you add double the number.",
            FeatType.Combat)
        {
            IsActive = true,
            IsFighterBonus = true
        };
        powerAttack.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "STR", 13));
        powerAttack.Benefit.Description = "Trade melee attack bonus for damage (1:1, or 1:2 with two-handed weapons)";
        Add(powerAttack);

        // --- Cleave ---
        var cleave = new FeatDefinition("Cleave",
            "If you deal a creature enough damage to make it drop (typically by dropping it to below 0 hit points or killing it), you get an immediate extra melee attack against another creature within reach.",
            FeatType.Combat)
        {
            IsFighterBonus = true
        };
        cleave.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "STR", 13));
        cleave.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Power Attack", 0));
        cleave.Benefit.GrantsCleave = true;
        cleave.Benefit.Description = "Extra melee attack after dropping a foe";
        Add(cleave);

        // --- Great Cleave ---
        var greatCleave = new FeatDefinition("Great Cleave",
            "As Cleave, except there is no limit to the number of times you can use it per round.",
            FeatType.Combat)
        {
            IsFighterBonus = true
        };
        greatCleave.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "STR", 13));
        greatCleave.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Power Attack", 0));
        greatCleave.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Cleave", 0));
        greatCleave.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 4));
        greatCleave.Benefit.GrantsGreatCleave = true;
        greatCleave.Benefit.Description = "Unlimited cleave attacks per round";
        Add(greatCleave);

        // --- Weapon Focus ---
        var weaponFocus = new FeatDefinition("Weapon Focus",
            "You gain a +1 bonus on all attack rolls you make using the selected weapon.",
            FeatType.Combat)
        {
            IsFighterBonus = true,
            RequiresChoice = true,
            CanTakeMultiple = true
        };
        weaponFocus.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 1));
        weaponFocus.Benefit.AttackBonus = 1;
        weaponFocus.Benefit.RequiresWeaponChoice = true;
        weaponFocus.Benefit.Description = "+1 attack with chosen weapon";
        Add(weaponFocus);

        // --- Weapon Specialization ---
        var weaponSpec = new FeatDefinition("Weapon Specialization",
            "You gain a +2 bonus on all damage rolls you make using the selected weapon.",
            FeatType.Combat)
        {
            IsFighterBonus = true,
            RequiresChoice = true,
            CanTakeMultiple = true
        };
        weaponSpec.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.ClassLevel, "Fighter", 4));
        weaponSpec.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Weapon Focus", 0));
        weaponSpec.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 4));
        weaponSpec.Benefit.DamageBonus = 2;
        weaponSpec.Benefit.RequiresWeaponChoice = true;
        weaponSpec.Benefit.Description = "+2 damage with chosen weapon";
        Add(weaponSpec);

        // --- Greater Weapon Focus ---
        var gwf = new FeatDefinition("Greater Weapon Focus",
            "You gain an additional +1 bonus on attack rolls you make using the selected weapon.",
            FeatType.Combat)
        {
            IsFighterBonus = true,
            RequiresChoice = true,
            CanTakeMultiple = true
        };
        gwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.ClassLevel, "Fighter", 8));
        gwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Weapon Focus", 0));
        gwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 8));
        gwf.Benefit.AttackBonus = 1;
        gwf.Benefit.RequiresWeaponChoice = true;
        gwf.Benefit.Description = "Additional +1 attack with chosen weapon (stacks with Weapon Focus)";
        Add(gwf);

        // --- Greater Weapon Specialization ---
        var gws = new FeatDefinition("Greater Weapon Specialization",
            "You gain an additional +2 bonus on damage rolls you make using the selected weapon.",
            FeatType.Combat)
        {
            IsFighterBonus = true,
            RequiresChoice = true,
            CanTakeMultiple = true
        };
        gws.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.ClassLevel, "Fighter", 12));
        gws.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Weapon Focus", 0));
        gws.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Greater Weapon Focus", 0));
        gws.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Weapon Specialization", 0));
        gws.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 12));
        gws.Benefit.DamageBonus = 2;
        gws.Benefit.RequiresWeaponChoice = true;
        gws.Benefit.Description = "Additional +2 damage with chosen weapon (stacks with Weapon Specialization)";
        Add(gws);

        // --- Improved Critical ---
        var impCrit = new FeatDefinition("Improved Critical",
            "When using the weapon you selected, your threat range is doubled.",
            FeatType.Combat)
        {
            IsFighterBonus = true,
            RequiresChoice = true,
            CanTakeMultiple = true
        };
        impCrit.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 8));
        impCrit.Benefit.DoublesWeaponThreatRange = true;
        impCrit.Benefit.RequiresWeaponChoice = true;
        impCrit.Benefit.Description = "Double threat range with chosen weapon";
        Add(impCrit);

        // --- Combat Expertise ---
        var combatExpertise = new FeatDefinition("Combat Expertise",
            "When you use the attack action or the full attack action in melee, you can take a penalty of as much as -5 on your attack roll and add the same number (+5 or less) as a dodge bonus to your AC.",
            FeatType.Combat)
        {
            IsActive = true,
            IsFighterBonus = true
        };
        combatExpertise.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "INT", 13));
        combatExpertise.Benefit.AllowsCombatExpertise = true;
        combatExpertise.Benefit.Description = "Trade attack bonus for AC (up to -5 attack for +5 AC)";
        Add(combatExpertise);

        // --- Improved Feint ---
        var impFeint = new FeatDefinition("Improved Feint",
            "You can make a Bluff check to feint in combat as a move action instead of a standard action.",
            FeatType.Combat)
        {
            IsFighterBonus = true
        };
        impFeint.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "INT", 13));
        impFeint.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Combat Expertise", 0));
        impFeint.Benefit.AllowsImprovedFeint = true;
        impFeint.Benefit.Description = "Feint as a move action instead of standard action";
        Add(impFeint);

        // --- Improved Bull Rush ---
        var impBullRush = new FeatDefinition("Improved Bull Rush",
            "When you perform a bull rush, you do not provoke an attack of opportunity from the defender. You also gain a +4 bonus on the opposed Strength check.",
            FeatType.Combat)
        {
            IsFighterBonus = true
        };
        impBullRush.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "STR", 13));
        impBullRush.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Power Attack", 0));
        impBullRush.Benefit.AllowsImprovedBullRush = true;
        impBullRush.Benefit.Description = "No AoO on bull rush, +4 bonus on opposed Strength check";
        Add(impBullRush);

        // --- Improved Disarm ---
        var impDisarm = new FeatDefinition("Improved Disarm",
            "You do not provoke an attack of opportunity when you attempt to disarm an opponent, and you receive a +4 bonus on the opposed attack roll.",
            FeatType.Combat)
        {
            IsFighterBonus = true,
            IsMonkBonus = true,
            MonkBonusLevel = 6
        };
        impDisarm.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "INT", 13));
        impDisarm.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Combat Expertise", 0));
        impDisarm.Benefit.AllowsImprovedDisarm = true;
        impDisarm.Benefit.Description = "No AoO on disarm, +4 bonus on opposed attack roll";
        Add(impDisarm);

        // --- Improved Grapple ---
        var impGrapple = new FeatDefinition("Improved Grapple",
            "You do not provoke an attack of opportunity when you make a touch attack to start a grapple. You also gain a +4 bonus on all grapple checks.",
            FeatType.Combat)
        {
            IsFighterBonus = true,
            IsMonkBonus = true,
            MonkBonusLevel = 1
        };
        impGrapple.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        impGrapple.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Improved Unarmed Strike", 0));
        impGrapple.Benefit.AllowsImprovedGrapple = true;
        impGrapple.Benefit.Description = "No AoO on grapple, +4 bonus on grapple checks";
        Add(impGrapple);

        // --- Improved Overrun ---
        var impOverrun = new FeatDefinition("Improved Overrun",
            "When you attempt to overrun an opponent, the target may not choose to avoid you. You also gain a +4 bonus on your Strength check.",
            FeatType.Combat)
        {
            IsFighterBonus = true
        };
        impOverrun.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "STR", 13));
        impOverrun.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Power Attack", 0));
        impOverrun.Benefit.AllowsImprovedOverrun = true;
        impOverrun.Benefit.Description = "Target can't avoid overrun, +4 bonus on Strength check";
        Add(impOverrun);

        // --- Improved Sunder ---
        var impSunder = new FeatDefinition("Improved Sunder",
            "When you strike at an object held or carried by an opponent, you do not provoke an attack of opportunity. You also gain a +4 bonus on any attack roll.",
            FeatType.Combat)
        {
            IsFighterBonus = true
        };
        impSunder.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "STR", 13));
        impSunder.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Power Attack", 0));
        impSunder.Benefit.AllowsImprovedSunder = true;
        impSunder.Benefit.Description = "No AoO on sunder, +4 bonus on attack roll";
        Add(impSunder);

        // --- Improved Trip ---
        var impTrip = new FeatDefinition("Improved Trip",
            "You do not provoke an attack of opportunity when you attempt to trip an opponent. You also gain a +4 bonus on your Strength check. If you trip successfully, you immediately get a melee attack against that opponent as if you hadn't used your attack for the trip attempt.",
            FeatType.Combat)
        {
            IsFighterBonus = true,
            IsMonkBonus = true,
            MonkBonusLevel = 6
        };
        impTrip.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "INT", 13));
        impTrip.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Combat Expertise", 0));
        impTrip.Benefit.AllowsImprovedTrip = true;
        impTrip.Benefit.Description = "No AoO on trip, +4 bonus, free attack on success";
        Add(impTrip);

        // --- Whirlwind Attack ---
        var whirlwind = new FeatDefinition("Whirlwind Attack",
            "When you use the full attack action, you can give up your regular attacks and instead make one melee attack at your full base attack bonus against each opponent within reach.",
            FeatType.Combat)
        {
            IsFighterBonus = true
        };
        whirlwind.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        whirlwind.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "INT", 13));
        whirlwind.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Combat Expertise", 0));
        whirlwind.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Dodge", 0));
        whirlwind.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Mobility", 0));
        whirlwind.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Spring Attack", 0));
        whirlwind.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 4));
        whirlwind.Benefit.AllowsWhirlwindAttack = true;
        whirlwind.Benefit.Description = "Attack all adjacent foes as a full-round action";
        Add(whirlwind);

        // --- Weapon Finesse ---
        var weaponFinesse = new FeatDefinition("Weapon Finesse",
            "With a light weapon, rapier, whip, or spiked chain made for a creature of your size category, you may use your Dexterity modifier instead of your Strength modifier on attack rolls.",
            FeatType.Combat)
        {
            IsFighterBonus = true
        };
        weaponFinesse.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 1));
        weaponFinesse.Benefit.GrantsWeaponFinesse = true;
        weaponFinesse.Benefit.Description = "Use DEX instead of STR for attack rolls with light weapons";
        Add(weaponFinesse);
    }

    // ========================================================================
    // RANGED COMBAT FEATS
    // ========================================================================
    private static void DefineRangedFeats()
    {
        // --- Point Blank Shot ---
        var pbs = new FeatDefinition("Point Blank Shot",
            "You get a +1 bonus on attack and damage rolls with ranged weapons at ranges of up to 30 feet.",
            FeatType.Ranged)
        {
            IsFighterBonus = true
        };
        pbs.Benefit.Description = "+1 attack and +1 damage with ranged weapons within 30 feet";
        Add(pbs);

        // --- Precise Shot ---
        var preciseShot = new FeatDefinition("Precise Shot",
            "You can shoot or throw ranged weapons at an opponent engaged in melee without taking the standard -4 penalty on your attack roll.",
            FeatType.Ranged)
        {
            IsFighterBonus = true
        };
        preciseShot.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Point Blank Shot", 0));
        preciseShot.Benefit.NoPenaltyShootingIntoMelee = true;
        preciseShot.Benefit.Description = "No -4 penalty for shooting into melee";
        Add(preciseShot);

        // --- Improved Precise Shot ---
        var impPrecise = new FeatDefinition("Improved Precise Shot",
            "Your ranged attacks ignore the AC bonus granted to targets by anything less than total cover, and the miss chance granted to targets by anything less than total concealment.",
            FeatType.Ranged)
        {
            IsFighterBonus = true
        };
        impPrecise.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 19));
        impPrecise.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Point Blank Shot", 0));
        impPrecise.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Precise Shot", 0));
        impPrecise.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 11));
        impPrecise.Benefit.IgnoreCoverConcealment = true;
        impPrecise.Benefit.Description = "Ignore cover and concealment (less than total) for ranged attacks";
        Add(impPrecise);

        // --- Far Shot ---
        var farShot = new FeatDefinition("Far Shot",
            "When you use a projectile weapon, its range increment increases by one-half (multiply by 1.5). When you use a thrown weapon, its range increment is doubled.",
            FeatType.Ranged)
        {
            IsFighterBonus = true
        };
        farShot.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Point Blank Shot", 0));
        farShot.Benefit.IncreasesRangeIncrement = true;
        farShot.Benefit.Description = "Range increment ×1.5 (projectile) or ×2 (thrown)";
        Add(farShot);

        // --- Rapid Shot ---
        var rapidShot = new FeatDefinition("Rapid Shot",
            "You can get one extra attack per round with a ranged weapon. The attack is at your highest base attack bonus, but each attack you make in that round (the extra one and the normal ones) takes a -2 penalty.",
            FeatType.Ranged)
        {
            IsActive = true,
            IsFighterBonus = true
        };
        rapidShot.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        rapidShot.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Point Blank Shot", 0));
        rapidShot.Benefit.Description = "Extra ranged attack at highest BAB, -2 penalty to all attacks";
        Add(rapidShot);

        // --- Shot on the Run ---
        var shotOnRun = new FeatDefinition("Shot on the Run",
            "When using the attack action with a ranged weapon, you can move both before and after the attack, provided that your total distance moved is not greater than your speed.",
            FeatType.Ranged)
        {
            IsFighterBonus = true
        };
        shotOnRun.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        shotOnRun.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Dodge", 0));
        shotOnRun.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Mobility", 0));
        shotOnRun.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Point Blank Shot", 0));
        shotOnRun.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 4));
        shotOnRun.Benefit.AllowsShotOnTheRun = true;
        shotOnRun.Benefit.Description = "Move before and after ranged attack";
        Add(shotOnRun);

        // --- Manyshot ---
        var manyshot = new FeatDefinition("Manyshot",
            "As a standard action, you may fire two arrows at a single opponent within 30 feet. Each arrow uses your primary attack bonus (with a -4 penalty). For every five points of base attack bonus you have above +6, you may add one additional arrow to this maximum of four arrows at +16.",
            FeatType.Ranged)
        {
            IsFighterBonus = true
        };
        manyshot.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 17));
        manyshot.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Point Blank Shot", 0));
        manyshot.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Rapid Shot", 0));
        manyshot.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 6));
        manyshot.Benefit.AllowsManyshot = true;
        manyshot.Benefit.Description = "Fire 2+ arrows at once (standard action, -4 penalty, within 30 ft)";
        Add(manyshot);
    }

    // ========================================================================
    // DEFENSIVE FEATS
    // ========================================================================
    private static void DefineDefensiveFeats()
    {
        // --- Dodge ---
        var dodge = new FeatDefinition("Dodge",
            "During your action, you designate an opponent and receive a +1 dodge bonus to Armor Class against attacks from that opponent.",
            FeatType.Defensive)
        {
            IsFighterBonus = true
        };
        dodge.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        dodge.Benefit.ACBonus = 1;
        dodge.Benefit.Description = "+1 dodge bonus to AC vs one designated opponent";
        Add(dodge);

        // --- Mobility ---
        var mobility = new FeatDefinition("Mobility",
            "You get a +4 dodge bonus to Armor Class against attacks of opportunity caused when you move out of or within a threatened area.",
            FeatType.Defensive)
        {
            IsFighterBonus = true
        };
        mobility.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        mobility.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Dodge", 0));
        mobility.Benefit.Description = "+4 dodge bonus to AC against attacks of opportunity from movement";
        Add(mobility);

        // --- Spring Attack ---
        var springAttack = new FeatDefinition("Spring Attack",
            "When using the attack action with a melee weapon, you can move both before and after the attack, provided that your total distance moved is not greater than your speed.",
            FeatType.Defensive)
        {
            IsFighterBonus = true
        };
        springAttack.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        springAttack.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Dodge", 0));
        springAttack.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Mobility", 0));
        springAttack.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 4));
        springAttack.Benefit.AllowsSpringAttack = true;
        springAttack.Benefit.Description = "Move before and after melee attack";
        Add(springAttack);

        // --- Combat Reflexes ---
        var combatReflexes = new FeatDefinition("Combat Reflexes",
            "You may make a number of additional attacks of opportunity equal to your Dexterity bonus. You can also make attacks of opportunity while flat-footed.",
            FeatType.Defensive)
        {
            IsFighterBonus = true,
            IsMonkBonus = true,
            MonkBonusLevel = 2
        };
        combatReflexes.Benefit.GrantsExtraAoO = true;
        combatReflexes.Benefit.Description = "Extra attacks of opportunity equal to DEX modifier";
        Add(combatReflexes);

        // --- Lightning Reflexes ---
        var lightningRef = new FeatDefinition("Lightning Reflexes",
            "You get a +2 bonus on all Reflex saving throws.",
            FeatType.Defensive);
        lightningRef.Benefit.ReflexSaveBonus = 2;
        lightningRef.Benefit.Description = "+2 Reflex saves";
        Add(lightningRef);

        // --- Great Fortitude ---
        var greatFort = new FeatDefinition("Great Fortitude",
            "You get a +2 bonus on all Fortitude saving throws.",
            FeatType.Defensive);
        greatFort.Benefit.FortitudeSaveBonus = 2;
        greatFort.Benefit.Description = "+2 Fortitude saves";
        Add(greatFort);

        // --- Iron Will ---
        var ironWill = new FeatDefinition("Iron Will",
            "You get a +2 bonus on all Will saving throws.",
            FeatType.Defensive);
        ironWill.Benefit.WillSaveBonus = 2;
        ironWill.Benefit.Description = "+2 Will saves";
        Add(ironWill);

        // --- Improved Initiative ---
        var impInit = new FeatDefinition("Improved Initiative",
            "You get a +4 bonus on initiative checks.",
            FeatType.Defensive)
        {
            IsFighterBonus = true
        };
        impInit.Benefit.InitiativeBonus = 4;
        impInit.Benefit.Description = "+4 initiative";
        Add(impInit);
    }

    // ========================================================================
    // TWO-WEAPON FIGHTING FEATS
    // ========================================================================
    private static void DefineTWFFeats()
    {
        // --- Two-Weapon Fighting ---
        var twf = new FeatDefinition("Two-Weapon Fighting",
            "Your penalties on attack rolls for fighting with two weapons are reduced. The penalty for your primary hand lessens by 2 and the one for your off hand lessens by 6.",
            FeatType.TwoWeaponFighting)
        {
            IsFighterBonus = true
        };
        twf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 15));
        twf.Benefit.ReducesTWFPenalty = true;
        twf.Benefit.Description = "Reduce TWF penalties to -2/-2 (light off-hand) or -4/-4 (normal)";
        Add(twf);

        // --- Improved Two-Weapon Fighting ---
        var itwf = new FeatDefinition("Improved Two-Weapon Fighting",
            "In addition to the standard single extra attack you get with an off-hand weapon, you get a second attack with it, albeit at a -5 penalty.",
            FeatType.TwoWeaponFighting)
        {
            IsFighterBonus = true
        };
        itwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 17));
        itwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Two-Weapon Fighting", 0));
        itwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 6));
        itwf.Benefit.GrantsExtraOffHandAttack = true;
        itwf.Benefit.Description = "Extra off-hand attack at -5 penalty";
        Add(itwf);

        // --- Greater Two-Weapon Fighting ---
        var gtwf = new FeatDefinition("Greater Two-Weapon Fighting",
            "You get a third attack with your off-hand weapon, albeit at a -10 penalty.",
            FeatType.TwoWeaponFighting)
        {
            IsFighterBonus = true
        };
        gtwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 19));
        gtwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Two-Weapon Fighting", 0));
        gtwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Improved Two-Weapon Fighting", 0));
        gtwf.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 11));
        gtwf.Benefit.GrantsSecondOffHandAttack = true;
        gtwf.Benefit.Description = "Second extra off-hand attack at -10 penalty";
        Add(gtwf);

        // --- Two-Weapon Defense ---
        var twfDef = new FeatDefinition("Two-Weapon Defense",
            "When wielding a double weapon or two weapons (not including natural weapons or unarmed strikes), you gain a +1 shield bonus to your AC.",
            FeatType.TwoWeaponFighting)
        {
            IsFighterBonus = true
        };
        twfDef.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 15));
        twfDef.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Two-Weapon Fighting", 0));
        twfDef.Benefit.GrantsTWFACBonus = true;
        twfDef.Benefit.Description = "+1 shield bonus to AC when fighting with two weapons";
        Add(twfDef);
    }

    // ========================================================================
    // MOUNTED COMBAT FEATS
    // ========================================================================
    private static void DefineMountedFeats()
    {
        // --- Mounted Combat ---
        var mounted = new FeatDefinition("Mounted Combat",
            "Once per round when your mount is hit in combat, you may attempt a Ride check (as a reaction) to negate the hit. The hit is negated if your Ride check result is greater than the opponent's attack roll.",
            FeatType.MountedCombat)
        {
            IsFighterBonus = true
        };
        mounted.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.SkillRanks, "Ride", 1));
        mounted.Benefit.AllowsMountedCombat = true;
        mounted.Benefit.Description = "Ride check to negate hit on mount";
        Add(mounted);

        // --- Ride-By Attack ---
        var rideBy = new FeatDefinition("Ride-By Attack",
            "When you are mounted and use the charge action, you may move and attack as if with a standard charge and then move again (continuing the straight line of the charge).",
            FeatType.MountedCombat)
        {
            IsFighterBonus = true
        };
        rideBy.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.SkillRanks, "Ride", 1));
        rideBy.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Mounted Combat", 0));
        rideBy.Benefit.AllowsRideByAttack = true;
        rideBy.Benefit.Description = "Move before and after charge while mounted";
        Add(rideBy);

        // --- Spirited Charge ---
        var spirited = new FeatDefinition("Spirited Charge",
            "When mounted and using the charge action, you deal double damage with a melee weapon (or triple damage with a lance).",
            FeatType.MountedCombat)
        {
            IsFighterBonus = true
        };
        spirited.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.SkillRanks, "Ride", 1));
        spirited.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Mounted Combat", 0));
        spirited.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Ride-By Attack", 0));
        spirited.Benefit.AllowsSpiritedCharge = true;
        spirited.Benefit.Description = "Double damage on mounted charge (triple with lance)";
        Add(spirited);

        // --- Trample ---
        var trample = new FeatDefinition("Trample",
            "When you attempt to overrun an opponent while mounted, your target may not choose to avoid you. Your mount may make one hoof attack against any target you knock down.",
            FeatType.MountedCombat)
        {
            IsFighterBonus = true
        };
        trample.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.SkillRanks, "Ride", 1));
        trample.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Mounted Combat", 0));
        trample.Benefit.AllowsTrample = true;
        trample.Benefit.Description = "Target can't avoid mounted overrun, mount gets hoof attack";
        Add(trample);
    }

    // ========================================================================
    // UNARMED COMBAT FEATS
    // ========================================================================
    private static void DefineUnarmedFeats()
    {
        // --- Improved Unarmed Strike ---
        var impUnarmed = new FeatDefinition("Improved Unarmed Strike",
            "You are considered to be armed even when unarmed — you do not provoke attacks of opportunity from armed opponents when you attack them while unarmed.",
            FeatType.Unarmed)
        {
            IsFighterBonus = true
        };
        impUnarmed.Benefit.AllowsUnarmedWithoutAoO = true;
        impUnarmed.Benefit.Description = "Unarmed attacks don't provoke attacks of opportunity";
        Add(impUnarmed);

        // --- Deflect Arrows ---
        var deflect = new FeatDefinition("Deflect Arrows",
            "You must have at least one hand free to use this feat. Once per round when you would normally be hit with a ranged weapon, you may deflect it so that you take no damage from it.",
            FeatType.Unarmed)
        {
            IsFighterBonus = true,
            IsMonkBonus = true,
            MonkBonusLevel = 2
        };
        deflect.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        deflect.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Improved Unarmed Strike", 0));
        deflect.Benefit.GrantsDeflectArrows = true;
        deflect.Benefit.Description = "Deflect one ranged attack per round (requires free hand)";
        Add(deflect);

        // --- Snatch Arrows ---
        var snatch = new FeatDefinition("Snatch Arrows",
            "Instead of just deflecting a ranged attack, you can catch it. You must have at least one hand free.",
            FeatType.Unarmed)
        {
            IsFighterBonus = true
        };
        snatch.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 15));
        snatch.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Improved Unarmed Strike", 0));
        snatch.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Deflect Arrows", 0));
        snatch.Benefit.GrantsSnatchArrows = true;
        snatch.Benefit.Description = "Catch deflected ranged attacks";
        Add(snatch);

        // --- Stunning Fist ---
        var stunFist = new FeatDefinition("Stunning Fist",
            "You must declare that you are using this feat before you make your attack roll. You can attempt a stunning attack once per day for every four levels you have attained, and no more than once per round. A defender hit must succeed on a Fortitude save (DC 10 + 1/2 your character level + your Wis modifier) or be stunned for 1 round.",
            FeatType.Unarmed)
        {
            IsFighterBonus = true,
            IsMonkBonus = true,
            MonkBonusLevel = 1
        };
        stunFist.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "DEX", 13));
        stunFist.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.AbilityScore, "WIS", 13));
        stunFist.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Improved Unarmed Strike", 0));
        stunFist.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 8));
        stunFist.Benefit.Description = "Stun opponent with unarmed strike (Fort save DC 10 + level/2 + WIS mod)";
        Add(stunFist);
    }

    // ========================================================================
    // SKILL FEATS
    // ========================================================================
    private static void DefineSkillFeats()
    {
        // --- Skill Focus ---
        var skillFocus = new FeatDefinition("Skill Focus",
            "You get a +3 bonus on all checks involving that skill.",
            FeatType.Skill)
        {
            CanTakeMultiple = true,
            RequiresChoice = true
        };
        skillFocus.Benefit.RequiresSkillChoice = true;
        skillFocus.Benefit.Description = "+3 bonus to chosen skill";
        Add(skillFocus);

        // --- Alertness ---
        DefineSkillPairFeat("Alertness", "Listen", "Spot",
            "You get a +2 bonus on all Listen checks and Spot checks.");

        // --- Athletic ---
        DefineSkillPairFeat("Athletic", "Climb", "Swim",
            "You get a +2 bonus on all Climb checks and Swim checks.");

        // --- Acrobatic ---
        DefineSkillPairFeat("Acrobatic", "Jump", "Tumble",
            "You get a +2 bonus on all Jump checks and Tumble checks.");

        // --- Stealthy ---
        DefineSkillPairFeat("Stealthy", "Hide", "Move Silently",
            "You get a +2 bonus on all Hide checks and Move Silently checks.");

        // --- Deceitful ---
        DefineSkillPairFeat("Deceitful", "Disguise", "Forgery",
            "You get a +2 bonus on all Disguise checks and Forgery checks.");

        // --- Deft Hands ---
        DefineSkillPairFeat("Deft Hands", "Sleight of Hand", "Use Rope",
            "You get a +2 bonus on all Sleight of Hand checks and Use Rope checks.");

        // --- Diligent ---
        DefineSkillPairFeat("Diligent", "Appraise", "Decipher Script",
            "You get a +2 bonus on all Appraise checks and Decipher Script checks.");

        // --- Investigator ---
        DefineSkillPairFeat("Investigator", "Gather Information", "Search",
            "You get a +2 bonus on all Gather Information checks and Search checks.");

        // --- Magical Aptitude ---
        DefineSkillPairFeat("Magical Aptitude", "Spellcraft", "Use Magic Device",
            "You get a +2 bonus on all Spellcraft checks and Use Magic Device checks.");

        // --- Negotiator ---
        DefineSkillPairFeat("Negotiator", "Diplomacy", "Sense Motive",
            "You get a +2 bonus on all Diplomacy checks and Sense Motive checks.");

        // --- Nimble Fingers ---
        DefineSkillPairFeat("Nimble Fingers", "Disable Device", "Open Lock",
            "You get a +2 bonus on all Disable Device checks and Open Lock checks.");

        // --- Persuasive ---
        DefineSkillPairFeat("Persuasive", "Bluff", "Intimidate",
            "You get a +2 bonus on all Bluff checks and Intimidate checks.");

        // --- Self-Sufficient ---
        DefineSkillPairFeat("Self-Sufficient", "Heal", "Survival",
            "You get a +2 bonus on all Heal checks and Survival checks.");

        // --- Animal Affinity ---
        DefineSkillPairFeat("Animal Affinity", "Handle Animal", "Ride",
            "You get a +2 bonus on all Handle Animal checks and Ride checks.");

        // --- Agile ---
        DefineSkillPairFeat("Agile", "Balance", "Escape Artist",
            "You get a +2 bonus on all Balance checks and Escape Artist checks.");
    }

    /// <summary>Helper to create a skill pair feat (+2 to two skills).</summary>
    private static void DefineSkillPairFeat(string name, string skill1, string skill2, string desc)
    {
        var feat = new FeatDefinition(name, desc, FeatType.Skill);
        feat.Benefit.SkillBonuses[skill1] = 2;
        feat.Benefit.SkillBonuses[skill2] = 2;
        feat.Benefit.Description = $"+2 {skill1} and +2 {skill2}";
        Add(feat);
    }

    // ========================================================================
    // GENERAL FEATS
    // ========================================================================
    private static void DefineGeneralFeats()
    {
        // --- Toughness ---
        var toughness = new FeatDefinition("Toughness",
            "You gain +3 hit points.",
            FeatType.General)
        {
            CanTakeMultiple = true
        };
        toughness.Benefit.HPBonus = 3;
        toughness.Benefit.Description = "+3 hit points";
        Add(toughness);

        // --- Endurance ---
        var endurance = new FeatDefinition("Endurance",
            "You gain a +4 bonus on the following checks and saves: Swim checks made to resist nonlethal damage, Constitution checks to continue running, Constitution checks to avoid nonlethal damage from a forced march, Constitution checks to hold your breath, Constitution checks to avoid nonlethal damage from starvation or thirst, Fortitude saves to avoid nonlethal damage from hot or cold environments, Fortitude saves to resist damage from suffocation.",
            FeatType.General);
        endurance.Benefit.GrantsEndurance = true;
        endurance.Benefit.Description = "+4 on checks to resist fatigue, exhaustion, and environmental hazards";
        Add(endurance);

        // --- Diehard ---
        var diehard = new FeatDefinition("Diehard",
            "When reduced to between -1 and -9 hit points, you automatically become stable. You don't have to roll d% to see if you lose 1 hit point each round. When reduced to negative hit points, you may choose to act as if you were disabled, rather than dying.",
            FeatType.General);
        diehard.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Endurance", 0));
        diehard.Benefit.GrantsDiehard = true;
        diehard.Benefit.Description = "Remain conscious at negative HP (act as disabled)";
        Add(diehard);

        // --- Run ---
        var run = new FeatDefinition("Run",
            "When running, you move five times your normal speed (if wearing medium, light, or no armor and carrying no more than a medium load) or four times your speed (if wearing heavy armor or carrying a heavy load). You retain your Dexterity bonus to AC while running.",
            FeatType.General);
        run.Benefit.SpeedMultiplier = 5;
        run.Benefit.Description = "Run at 5× speed (instead of 4×), retain DEX bonus to AC while running";
        Add(run);

        // --- Blind-Fight ---
        var blindFight = new FeatDefinition("Blind-Fight",
            "In melee, every time you miss because of concealment, you can reroll your miss chance percentile roll one time to see if you actually hit. An invisible attacker gets no advantages related to hitting you in melee.",
            FeatType.General)
        {
            IsFighterBonus = true
        };
        blindFight.Benefit.GrantsBlindFight = true;
        blindFight.Benefit.Description = "Reroll miss chances from concealment; invisible attackers get no bonus in melee";
        Add(blindFight);

        // --- Track ---
        var track = new FeatDefinition("Track",
            "To find tracks or to follow them for 1 mile requires a successful Survival check. You must make another Survival check every time the tracks become difficult to follow.",
            FeatType.General);
        track.Benefit.GrantsTrack = true;
        track.Benefit.Description = "Use Survival skill to track creatures";
        Add(track);

        // --- Quick Draw ---
        var quickDraw = new FeatDefinition("Quick Draw",
            "You can draw a weapon as a free action instead of as a move action. You can draw a hidden weapon as a move action.",
            FeatType.General)
        {
            IsFighterBonus = true
        };
        quickDraw.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.BAB, "", 1));
        quickDraw.Benefit.GrantsQuickDraw = true;
        quickDraw.Benefit.Description = "Draw weapon as free action";
        Add(quickDraw);

        // --- Combat Casting ---
        var combatCasting = new FeatDefinition("Combat Casting",
            "You get a +4 bonus on Concentration checks made to cast a spell or use a spell-like ability while on the defensive or while you are grappling or pinned.",
            FeatType.General);
        combatCasting.Benefit.GrantsCombatCasting = true;
        combatCasting.Benefit.Description = "+4 on Concentration checks to cast defensively";
        Add(combatCasting);

        // --- Augment Summoning ---
        var augSummon = new FeatDefinition("Augment Summoning",
            "Each creature you conjure with any summon spell gains a +4 enhancement bonus to Strength and Constitution for the duration of the spell.",
            FeatType.General);
        augSummon.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.Feat, "Spell Focus", 0));
        augSummon.Benefit.GrantsAugmentSummoning = true;
        augSummon.Benefit.Description = "Summoned creatures gain +4 STR and +4 CON";
        Add(augSummon);

        // --- Natural Spell ---
        // (Not adding - druid specific and requires Wild Shape class feature)

        // --- Extra Turning ---
        // (Not adding - cleric specific)

        // --- Spell Focus / Greater Spell Focus ---
        // (Not adding metamagic-adjacent)

        // --- Spell Penetration / Greater Spell Penetration ---
        // (Not adding - caster specific)
    }

    // ========================================================================
    // HELPER
    // ========================================================================
    private static void Add(FeatDefinition feat)
    {
        if (_feats.ContainsKey(feat.FeatName))
        {
            Debug.LogWarning($"[Feats] Duplicate feat name: {feat.FeatName}");
            return;
        }
        _feats[feat.FeatName] = feat;
    }

    /// <summary>
    /// Get the number of general feats a character gets at a given level.
    /// Characters get a feat at levels 1, 3, 6, 9, 12, 15, 18.
    /// </summary>
    public static int GetGeneralFeatCountAtLevel(int level)
    {
        int count = 1; // Level 1 feat
        for (int i = 3; i <= level; i += 3)
            count++;
        return count;
    }

    /// <summary>
    /// Get the number of fighter bonus feats at a given level.
    /// Fighters get bonus feats at levels 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20.
    /// </summary>
    public static int GetFighterBonusFeatCount(int level)
    {
        if (level < 1) return 0;
        int count = 1; // Level 1
        for (int i = 2; i <= level; i += 2)
            count++;
        return count;
    }

    /// <summary>
    /// Check if a character should get a general feat at this level.
    /// </summary>
    public static bool GetsGeneralFeatAtLevel(int level)
    {
        return level == 1 || (level >= 3 && level % 3 == 0);
    }

    /// <summary>
    /// Check if a fighter gets a bonus feat at this level.
    /// </summary>
    public static bool GetsFighterBonusFeatAtLevel(int level)
    {
        return level == 1 || (level >= 2 && level % 2 == 0);
    }

    /// <summary>
    /// Humans get a bonus feat at level 1.
    /// </summary>
    public static bool GetsRacialBonusFeat(string raceName, int level)
    {
        return raceName == "Human" && level == 1;
    }

    // ========================================================================
    // WIZARD BONUS FEATS
    // ========================================================================
    // D&D 3.5e: Wizards gain bonus feats at levels 5, 10, 15, and 20.
    // They can choose from: Metamagic feats, Item Creation feats, or Spell Mastery.
    // Unlike Monk bonus feats, Wizards MUST meet all prerequisites (including caster level).
    // ========================================================================

    /// <summary>
    /// Check if a Wizard gets a bonus feat at this level.
    /// Wizards get bonus feats at levels 5, 10, 15, and 20.
    /// </summary>
    public static bool GetsWizardBonusFeatAtLevel(int level)
    {
        return level == 5 || level == 10 || level == 15 || level == 20;
    }

    /// <summary>
    /// Get the number of Wizard bonus feats at a given level.
    /// Wizards get bonus feats at levels 5, 10, 15, and 20.
    /// </summary>
    public static int GetWizardBonusFeatCount(int level)
    {
        int count = 0;
        if (level >= 5) count++;
        if (level >= 10) count++;
        if (level >= 15) count++;
        if (level >= 20) count++;
        return count;
    }

    /// <summary>
    /// Get all Wizard bonus feats the character qualifies for.
    /// Includes: all Metamagic feats, all Item Creation feats, and Spell Mastery.
    /// Wizard bonus feats require meeting all prerequisites (unlike Monk bonus feats).
    /// </summary>
    /// <param name="stats">Character stats to check prerequisites against</param>
    /// <returns>List of eligible Wizard bonus feat definitions</returns>
    public static List<FeatDefinition> GetWizardBonusFeats(CharacterStats stats)
    {
        if (!_initialized) Init();
        var available = new List<FeatDefinition>();
        foreach (var feat in _feats.Values)
        {
            if (!feat.IsWizardBonus) continue;
            if (!feat.CanTakeMultiple && stats != null && stats.HasFeat(feat.FeatName)) continue;
            // Wizard bonus feats still require meeting prerequisites
            if (stats != null && !feat.MeetsPrerequisites(stats)) continue;
            available.Add(feat);
        }
        return available;
    }

    /// <summary>
    /// Get all feats marked as Wizard bonus feats (regardless of prerequisites).
    /// Useful for displaying the full list with locked/unlocked indicators.
    /// </summary>
    public static List<FeatDefinition> GetAllWizardBonusFeats()
    {
        if (!_initialized) Init();
        var list = new List<FeatDefinition>();
        foreach (var feat in _feats.Values)
        {
            if (feat.IsWizardBonus)
                list.Add(feat);
        }
        return list;
    }

    // ========================================================================
    // METAMAGIC FEATS (D&D 3.5 PHB Chapter 5)
    // ========================================================================
    private static void DefineMetamagicFeats()
    {
        // --- Empower Spell ---
        var empowerSpell = new FeatDefinition("Empower Spell",
            "All variable, numeric effects of an empowered spell are increased by one-half. An empowered spell uses up a spell slot two levels higher than the spell's actual level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        empowerSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.EmpowerSpell,
            Description = "Increase all variable numeric effects by 50%. Uses a slot 2 levels higher."
        };
        Add(empowerSpell);

        // --- Enlarge Spell ---
        var enlargeSpell = new FeatDefinition("Enlarge Spell",
            "You can alter a spell with a range of close, medium, or long to increase its range by 100%. An enlarged spell uses up a spell slot one level higher than the spell's actual level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        enlargeSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.EnlargeSpell,
            Description = "Double the spell's range. Uses a slot 1 level higher."
        };
        Add(enlargeSpell);

        // --- Extend Spell ---
        var extendSpell = new FeatDefinition("Extend Spell",
            "An extended spell lasts twice as long as normal. Spells with a duration of concentration, instantaneous, or permanent are not affected. An extended spell uses up a spell slot one level higher than the spell's actual level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        extendSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.ExtendSpell,
            Description = "Double the spell's duration. Uses a slot 1 level higher."
        };
        Add(extendSpell);

        // --- Heighten Spell ---
        var heightenSpell = new FeatDefinition("Heighten Spell",
            "A heightened spell has a higher spell level than normal (up to 9th level). Unlike other metamagic feats, Heighten Spell actually increases the effective level of the spell that it modifies. All effects dependent on spell level (such as saving throw DCs) are calculated according to the heightened level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        heightenSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.HeightenSpell,
            Description = "Increase spell's effective level (and save DC). Variable slot cost."
        };
        Add(heightenSpell);

        // --- Maximize Spell ---
        var maximizeSpell = new FeatDefinition("Maximize Spell",
            "All variable, numeric effects of a maximized spell are maximized. A maximized spell uses up a spell slot three levels higher than the spell's actual level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        maximizeSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.MaximizeSpell,
            Description = "Maximize all variable numeric effects. Uses a slot 3 levels higher."
        };
        Add(maximizeSpell);

        // --- Quicken Spell ---
        var quickenSpell = new FeatDefinition("Quicken Spell",
            "Casting a quickened spell is a free action. You can perform another action, even casting another spell, in the same round as you cast a quickened spell. A quickened spell uses up a spell slot four levels higher than the spell's actual level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        quickenSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.QuickenSpell,
            Description = "Cast as a free action. Uses a slot 4 levels higher."
        };
        Add(quickenSpell);

        // --- Silent Spell ---
        var silentSpell = new FeatDefinition("Silent Spell",
            "A silent spell can be cast with no verbal components. A silent spell uses up a spell slot one level higher than the spell's actual level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        silentSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.SilentSpell,
            Description = "Cast without verbal components. Uses a slot 1 level higher."
        };
        Add(silentSpell);

        // --- Still Spell ---
        var stillSpell = new FeatDefinition("Still Spell",
            "A stilled spell can be cast with no somatic components. A stilled spell uses up a spell slot one level higher than the spell's actual level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        stillSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.StillSpell,
            Description = "Cast without somatic components. Uses a slot 1 level higher."
        };
        Add(stillSpell);

        // --- Widen Spell ---
        var widenSpell = new FeatDefinition("Widen Spell",
            "You can alter a burst, emanation, line, or spread shaped spell to increase its area. Any numeric measurements of the spell's area increase by 100%. A widened spell uses up a spell slot three levels higher than the spell's actual level.",
            FeatType.Metamagic)
        { IsWizardBonus = true };
        widenSpell.Benefit = new FeatBenefit
        {
            IsMetamagic = true,
            MetamagicId = MetamagicFeatId.WidenSpell,
            Description = "Double the spell's area of effect. Uses a slot 3 levels higher."
        };
        Add(widenSpell);

        Debug.Log("[Feats] 9 metamagic feat definitions registered (all marked as Wizard bonus feats).");
    }

    // ========================================================================
    // ITEM CREATION FEATS (D&D 3.5 PHB Chapter 5) - PLACEHOLDERS
    // ========================================================================
    // NOTE: These are placeholder definitions. Actual crafting mechanics
    // (gold costs, XP costs, time requirements, spell prerequisites for items)
    // are NOT yet implemented. The feat definitions exist so they can be
    // selected as Wizard bonus feats and show in the feat list.
    // ========================================================================
    private static void DefineItemCreationFeats()
    {
        // --- Scribe Scroll ---
        var scribeScroll = new FeatDefinition("Scribe Scroll",
            "You can create a scroll of any spell that you know. Scribing a scroll takes one day for each 1,000 gp in its base price. The base price of a scroll is its spell level × its caster level × 25 gp. To scribe a scroll, you must spend 1/25 of this base price in XP and use up raw materials costing one-half of this base price. [PLACEHOLDER - Crafting mechanics not yet implemented]",
            FeatType.ItemCreation)
        {
            IsWizardBonus = true,
            IsPlaceholder = true
        };
        scribeScroll.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 1));
        scribeScroll.Benefit.Description = "Create spell scrolls from your known spells. [PLACEHOLDER]";
        Add(scribeScroll);

        // --- Brew Potion ---
        var brewPotion = new FeatDefinition("Brew Potion",
            "You can create a potion of any 3rd-level or lower spell that you know and that targets one or more creatures. Brewing a potion takes one day. The base price of a potion is its spell level × its caster level × 50 gp (minimum caster level is always at least high enough to cast the spell). To brew a potion, you must spend 1/25 of this base price in XP and use up raw materials costing one-half this base price. [PLACEHOLDER - Crafting mechanics not yet implemented]",
            FeatType.ItemCreation)
        {
            IsWizardBonus = true,
            IsPlaceholder = true
        };
        brewPotion.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 3));
        brewPotion.Benefit.Description = "Brew magic potions from spells of 3rd level or lower. [PLACEHOLDER]";
        Add(brewPotion);

        // --- Craft Wondrous Item ---
        var craftWondrous = new FeatDefinition("Craft Wondrous Item",
            "You can create any wondrous item whose prerequisites you meet. Enchanting a wondrous item takes one day for each 1,000 gp in its price. To enchant a wondrous item, you must spend 1/25 of the item's price in XP and use up raw materials costing half of this price. [PLACEHOLDER - Crafting mechanics not yet implemented]",
            FeatType.ItemCreation)
        {
            IsWizardBonus = true,
            IsPlaceholder = true
        };
        craftWondrous.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 3));
        craftWondrous.Benefit.Description = "Create wondrous magic items (cloaks, boots, amulets, etc.). [PLACEHOLDER]";
        Add(craftWondrous);

        // --- Craft Magic Arms and Armor ---
        var craftArms = new FeatDefinition("Craft Magic Arms and Armor",
            "You can create any magic weapon, armor, or shield whose prerequisites you meet. Enhancing a weapon, suit of armor, or shield takes one day for each 1,000 gp in the price of its magical features. To enhance a weapon, suit of armor, or shield, you must spend 1/25 of its features' total price in XP and use up raw materials costing half of this total price. [PLACEHOLDER - Crafting mechanics not yet implemented]",
            FeatType.ItemCreation)
        {
            IsWizardBonus = true,
            IsPlaceholder = true
        };
        craftArms.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 5));
        craftArms.Benefit.Description = "Create magic weapons, armor, and shields. [PLACEHOLDER]";
        Add(craftArms);

        // --- Craft Wand ---
        var craftWand = new FeatDefinition("Craft Wand",
            "You can create a wand of any 4th-level or lower spell that you know. Crafting a wand takes one day for each 1,000 gp in its base price. The base price of a wand is its caster level × the spell level × 750 gp. To craft a wand, you must spend 1/25 of this base price in XP and use up raw materials costing half of this base price. A newly created wand has 50 charges. [PLACEHOLDER - Crafting mechanics not yet implemented]",
            FeatType.ItemCreation)
        {
            IsWizardBonus = true,
            IsPlaceholder = true
        };
        craftWand.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 5));
        craftWand.Benefit.Description = "Create magic wands with spells of 4th level or lower. [PLACEHOLDER]";
        Add(craftWand);

        // --- Craft Rod ---
        var craftRod = new FeatDefinition("Craft Rod",
            "You can create any rod whose prerequisites you meet. Crafting a rod takes one day for each 1,000 gp in its base price. To craft a rod, you must spend 1/25 of its base price in XP and use up raw materials costing half of its base price. [PLACEHOLDER - Crafting mechanics not yet implemented]",
            FeatType.ItemCreation)
        {
            IsWizardBonus = true,
            IsPlaceholder = true
        };
        craftRod.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 9));
        craftRod.Benefit.Description = "Create magic rods. [PLACEHOLDER]";
        Add(craftRod);

        // --- Craft Staff ---
        var craftStaff = new FeatDefinition("Craft Staff",
            "You can create any staff whose prerequisites you meet. Crafting a staff takes one day for each 1,000 gp in its base price. To craft a staff, you must spend 1/25 of its base price in XP and use up raw materials costing half of its base price. A newly created staff has 50 charges. [PLACEHOLDER - Crafting mechanics not yet implemented]",
            FeatType.ItemCreation)
        {
            IsWizardBonus = true,
            IsPlaceholder = true
        };
        craftStaff.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 12));
        craftStaff.Benefit.Description = "Create magic staves. [PLACEHOLDER]";
        Add(craftStaff);

        // --- Forge Ring ---
        var forgeRing = new FeatDefinition("Forge Ring",
            "You can create any ring whose prerequisites you meet. Crafting a ring takes one day for each 1,000 gp in its base price. To forge a ring, you must spend 1/25 of its base price in XP and use up raw materials costing half of its base price. [PLACEHOLDER - Crafting mechanics not yet implemented]",
            FeatType.ItemCreation)
        {
            IsWizardBonus = true,
            IsPlaceholder = true
        };
        forgeRing.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 12));
        forgeRing.Benefit.Description = "Create magic rings. [PLACEHOLDER]";
        Add(forgeRing);

        Debug.Log("[Feats] 8 item creation feat definitions registered (all PLACEHOLDERS, marked as Wizard bonus feats).");
    }

    // ========================================================================
    // SPELL MASTERY (D&D 3.5 PHB) - PLACEHOLDER
    // ========================================================================
    // NOTE: Spell Mastery allows a Wizard to prepare selected spells without
    // a spellbook. The actual mechanics (selecting which spells to master,
    // tracking mastered spells) are NOT yet implemented.
    // ========================================================================
    private static void DefineSpellMastery()
    {
        var spellMastery = new FeatDefinition("Spell Mastery",
            "Each time you take this feat, choose a number of spells equal to your Intelligence modifier that you already know. From that point on, you can prepare these spells without referring to a spellbook. [PLACEHOLDER - Spell Mastery mechanics not yet implemented]",
            FeatType.General)
        {
            IsWizardBonus = true,
            CanTakeMultiple = true,
            IsPlaceholder = true
        };
        // Prerequisite: Wizard level 1st (any Wizard can take this)
        spellMastery.Prerequisites.Add(new FeatPrerequisite(PrerequisiteType.ClassLevel, "Wizard", 1));
        spellMastery.Benefit.Description = "Prepare INT modifier spells without spellbook. Can be taken multiple times. [PLACEHOLDER]";
        Add(spellMastery);

        Debug.Log("[Feats] Spell Mastery feat definition registered (PLACEHOLDER, marked as Wizard bonus feat).");
    }
}
