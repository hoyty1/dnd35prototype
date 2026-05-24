using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D&D 3.5e Zombie Template (Monster Manual p.265).
/// Converts any corporeal creature into a shambling zombie.
///
/// Key differences from Skeleton template:
/// - DOUBLES racial HD (skeleton keeps HD as-is)
/// - Gains slam attack (skeleton gains claws)
/// - DR 5/slashing (skeleton: 5/bludgeoning)
/// - STR +2, DEX -2 (skeleton: DEX +2)
/// - Natural armor INCREASES by size (skeleton: replaces)
/// - Single Actions Only (can only move OR standard, except charge)
/// - Toughness feat (skeleton: Improved Initiative)
/// - Different CR table
/// - Max 10 base HD for animate dead (20 as zombie)
/// </summary>
public static class ZombieTemplate
{
    // ────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────

    /// <summary>
    /// Apply the zombie template to a base creature definition.
    /// Returns a new NPCDefinition with zombie modifications.
    /// The base creature is NOT modified.
    /// </summary>
    /// <param name="baseCreature">The base creature definition to zombify.</param>
    /// <param name="overrideId">Optional ID override. If null, generates "zombie_[baseid]".</param>
    /// <param name="overrideName">Optional name override. If null, generates "[BaseName] Zombie".</param>
    /// <returns>A new zombie NPCDefinition.</returns>
    public static NPCDefinition Apply(
        NPCDefinition baseCreature,
        string overrideId = null,
        string overrideName = null)
    {
        if (baseCreature == null)
        {
            Debug.LogError("[ZombieTemplate] Cannot apply template to null base creature.");
            return null;
        }

        // Start from a deep clone so we never mutate the database entry.
        NPCDefinition zombie = baseCreature.Clone();

        // ── Identity ──
        zombie.Id = overrideId ?? $"zombie_{baseCreature.Id}";
        zombie.Name = overrideName ?? $"{baseCreature.Name} Zombie";

        // ── Type → Undead ──
        zombie.CreatureType = "Undead";
        zombie.MaterialComposition = MaterialComposition.Organic; // still has flesh, unlike skeleton

        // ── Hit Dice: DOUBLE racial HD, convert to d12 ──
        // Drop class HD, keep racial HD. Minimum 1 HD.
        int racialHD = Mathf.Max(1, baseCreature.HitDice);
        int doubledHD = racialHD * 2;
        zombie.HitDice = doubledHD;
        zombie.Level = 0;
        zombie.CharacterClass = null;

        // ── Abilities (modify before HP calc) ──
        // STR +2, DEX -2, CON = none, INT = none, WIS = 10, CHA = 1
        zombie.STR = baseCreature.STR + 2;
        zombie.DEX = Mathf.Max(1, baseCreature.DEX - 2);
        zombie.CON = CharacterStats.NO_SCORE;
        zombie.INT = CharacterStats.NO_SCORE;
        zombie.WIS = 10;
        zombie.CHA = 1;

        // ── HP from doubled d12 HD (no CON modifier for undead) ──
        // Average d12 = 6.5. Toughness adds +3 HP for the first 3 HD, then +1/HD after.
        // For simplicity, add Toughness as flat +3 HP.
        int baseHP = (int)(doubledHD * 6.5f);
        int toughnessHP = doubledHD <= 3 ? 3 : doubledHD; // Toughness: +3 if 3 HD or less, +1/HD otherwise
        zombie.BaseHitDieHP = baseHP + toughnessHP;

        // ── BAB = 1/2 HD (Undead poor BAB, uses doubled HD) ──
        int bab = doubledHD / 2;
        zombie.BAB = bab;
        zombie.BABOverride = BABProgression.Poor;
        zombie.BaseAttackBonusOverride = bab;

        // ── Saves: Fort = HD/3, Ref = HD/3, Will = HD/2 + 2 ──
        zombie.FortitudeSaveOverride = SaveProgression.Poor;
        zombie.ReflexSaveOverride = SaveProgression.Poor;
        zombie.WillSaveOverride = SaveProgression.Good;

        // ── Natural Armor: INCREASE by size (adds to existing, unlike skeleton which replaces) ──
        zombie.NaturalArmorBonus = baseCreature.NaturalArmorBonus + GetZombieNaturalArmorIncrease(zombie.SizeCategory);

        // ── Speed: reduce by 10 ft (minimum 20 ft / 4 squares) ──
        // Zombies are slow. If base speed was in squares (5ft each), subtract 2 squares.
        zombie.BaseSpeed = Mathf.Max(4, baseCreature.BaseSpeed - 2);

        // ── Flight: becomes clumsy ──
        // If creature had flight, it's reduced to clumsy maneuverability.
        // For the prototype, we'll note this in tags but keep flight capability.
        // (The prototype doesn't track flight maneuverability in detail.)

        // ── Natural Attacks ──
        // Keep existing natural weapons but strip all on-hit special effects
        if (zombie.NaturalAttacks != null)
        {
            for (int i = 0; i < zombie.NaturalAttacks.Count; i++)
            {
                UndeadTemplateUtils.StripSpecialEffects(zombie.NaturalAttacks[i]);
            }
        }
        else
        {
            zombie.NaturalAttacks = new List<NaturalAttackDefinition>();
        }

        // Remove any existing slam attacks (we'll add ours)
        zombie.NaturalAttacks.RemoveAll(a =>
            a.Name != null && a.Name.IndexOf("Slam", StringComparison.OrdinalIgnoreCase) >= 0);

        // Add slam attack — primary natural attack
        int slamDice = GetZombieSlamDamageDice(zombie.SizeCategory);
        int slamCount = GetZombieSlamDamageCount(zombie.SizeCategory);

        // Determine if slam is primary or secondary (secondary if creature has other attacks)
        bool hasOtherNaturalAttacks = zombie.NaturalAttacks.Count > 0;

        zombie.NaturalAttacks.Add(new NaturalAttackDefinition
        {
            Name = "Slam",
            DamageDice = slamDice,
            DamageCount = slamCount,
            Count = 1,
            BonusDamageSource = hasOtherNaturalAttacks
                ? DamageBonusSource.StrengthHalf
                : DamageBonusSource.StrengthOneAndHalf,
            IsPrimary = !hasOtherNaturalAttacks,
            Range = 1
        });

        // If creature only has the slam (no other natural attacks), it's primary with 1.5x STR
        // If creature has other attacks, slam becomes secondary with 0.5x STR

        // ── Remove ALL special attacks ──
        zombie.BreathWeapon = null;
        zombie.SecondaryBreathWeapon = null;
        zombie.FrightfulPresence = null;
        zombie.Engulf = null;
        zombie.AuraAbility = null;
        zombie.StenchAuraDC = 0;
        zombie.StenchAuraRange = 0;
        zombie.HasTripAttack = false;
        zombie.HasImprovedGrab = false;
        zombie.HasPounce = false;
        zombie.HasRake = false;
        zombie.RakeAttack = null;
        zombie.GainsSmiteEvil = false;
        zombie.GainsSmiteGood = false;

        // ── Immunities: Undead immunities (cold immunity is NOT automatic for zombies) ──
        zombie.Immunities = ImmunityPresets.UndeadImmunities();
        zombie.DamageImmunities = new List<DamageType>();
        zombie.IsMindless = true;

        // ── Single Actions Only ──
        zombie.IsSingleActionsOnly = true;

        // ── DR 5/slashing ──
        zombie.DamageReductionAmount = 5;
        zombie.DamageReductionBypass = DamageBypassTag.Slashing;
        zombie.DamageReductionRangedOnly = false;

        // ── Clear resistances from base creature ──
        zombie.DamageResistances = new List<DamageResistanceEntry>();

        // ── Clear regeneration / SR ──
        zombie.RegenerationAmount = 0;
        zombie.RegenerationSuppressedBy = DamageBypassTag.None;
        zombie.SpellResistance = 0;

        // ── Not incorporeal ──
        zombie.IsIncorporeal = false;
        zombie.IsSwarm = false;
        zombie.SwarmTraits = new SwarmTraits();

        // ── No scent ──
        zombie.HasScent = false;

        // ── Skills: None (mindless) ──
        // Already handled by IsMindless

        // ── Feats: Replace with Toughness ──
        zombie.Feats = new List<string> { "Toughness" };
        zombie.WeaponFocusChoice = null;

        // ── Spells: None ──
        zombie.KnownSpellIds = new List<string>();
        zombie.PreparedSpellSlotIds = new List<string>();

        // ── CR ──
        zombie.ChallengeRating = GetZombieCR(doubledHD);

        // ── Tags ──
        zombie.CreatureTags = new List<string> { "Undead", "Zombie" };

        // ── Special Abilities display ──
        zombie.SpecialAbilities = BuildZombieAbilities(zombie.SizeCategory, doubledHD, racialHD);

        // ── Template tracking ──
        zombie.AppliedTemplateIds = new List<string> { "zombie" };

        // ── AI: mindless melee ──
        zombie.AIBehavior = NPCAIBehavior.AggressiveMelee;
        zombie.AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless;
        zombie.UseCoupDeGrace = null;
        zombie.AITargetPriority = null;

        // ── Equipment: keep manufactured weapons but strip armor for visual ──
        // Zombies typically keep whatever equipment the base creature had.
        // Don't clear equipment — let the factory override if needed.

        // ── Visuals: sickly grey-green ──
        zombie.SpriteColor = new Color(0.5f, 0.6f, 0.45f, 1f);
        zombie.PanelColor = new Color(0.15f, 0.2f, 0.15f, 0.85f);
        zombie.NameColor = new Color(0.6f, 0.8f, 0.5f);

        // ── Description ──
        zombie.Description = $"A shambling corpse that was once a {baseCreature.Name.ToLowerInvariant()}. " +
                            "Its rotting flesh hangs loosely, and it moves with jerky, unnatural motions. " +
                            "It can only perform a single action each round.";

        return zombie;
    }

    // ────────────────────────────────────────────
    //  Lookup Tables
    // ────────────────────────────────────────────

    /// <summary>
    /// Natural armor INCREASE by size (MM p.265).
    /// Unlike skeleton which replaces, zombie ADDS to existing natural armor.
    /// </summary>
    public static int GetZombieNaturalArmorIncrease(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine:
            case SizeCategory.Diminutive:
            case SizeCategory.Tiny:
                return 0;
            case SizeCategory.Small:
                return 1;
            case SizeCategory.Medium:
                return 2;
            case SizeCategory.Large:
                return 3;
            case SizeCategory.Huge:
                return 4;
            case SizeCategory.Gargantuan:
                return 7;
            case SizeCategory.Colossal:
                return 11;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Slam damage dice by size (MM p.265).
    /// Returns the die size (e.g., 6 for d6).
    /// </summary>
    public static int GetZombieSlamDamageDice(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine:
                return 1;   // flat 1 damage
            case SizeCategory.Diminutive:
                return 2;   // 1d2
            case SizeCategory.Tiny:
                return 3;   // 1d3
            case SizeCategory.Small:
                return 4;   // 1d4
            case SizeCategory.Medium:
                return 6;   // 1d6
            case SizeCategory.Large:
                return 8;   // 1d8
            case SizeCategory.Huge:
                return 6;   // 2d6
            case SizeCategory.Gargantuan:
                return 8;   // 2d8
            case SizeCategory.Colossal:
                return 6;   // 4d6
            default:
                return 6;
        }
    }

    /// <summary>
    /// Slam damage count (number of dice) by size.
    /// Most sizes are 1dX; Huge = 2d6, Gargantuan = 2d8, Colossal = 4d6.
    /// </summary>
    public static int GetZombieSlamDamageCount(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Huge:
            case SizeCategory.Gargantuan:
                return 2;
            case SizeCategory.Colossal:
                return 4;
            default:
                return 1;
        }
    }

    /// <summary>
    /// CR by Hit Dice for zombies (MM p.265 table).
    /// Note: uses the DOUBLED HD, not the original racial HD.
    /// Returns a string like "1/8", "1/4", "1/2", "1", etc.
    /// </summary>
    public static string GetZombieCR(int doubledHitDice)
    {
        // MM zombie CR table (by final/doubled HD):
        // 1 HD or less: 1/8
        // 2 HD: 1/2
        // 4 HD: 1
        // 6 HD: 2
        // 8-10 HD: 3
        // 12-14 HD: 4
        // 16 HD: 5
        // 18-20 HD: 6
        // 20+ HD: 7
        if (doubledHitDice <= 1) return "1/8";
        if (doubledHitDice <= 2) return "1/2";
        if (doubledHitDice <= 4) return "1";
        if (doubledHitDice <= 6) return "2";
        if (doubledHitDice <= 10) return "3";
        if (doubledHitDice <= 14) return "4";
        if (doubledHitDice <= 16) return "5";
        if (doubledHitDice <= 20) return "6";
        return "7";
    }

    // ────────────────────────────────────────────
    //  Internal helpers
    // ────────────────────────────────────────────

    /// <summary>
    /// Strip all on-hit special effects from a natural attack
    /// (poison, paralysis, disease, energy drain, etc.)
    /// </summary>
    // StripSpecialEffects moved to UndeadTemplateUtils.StripSpecialEffects()

    /// <summary>
    /// Build the Special Abilities display list for a zombie.
    /// </summary>
    private static List<string> BuildZombieAbilities(SizeCategory size, int doubledHD, int originalHD)
    {
        var abilities = new List<string>();
        abilities.Add("Undead traits (immune to mind-affecting, poison, sleep, paralysis, stunning, disease, death effects)");
        abilities.Add("DR 5/slashing");
        abilities.Add("Not subject to critical hits or sneak attack");
        abilities.Add("Single actions only (move OR standard per turn, except charge)");
        abilities.Add("Darkvision 60 ft.");

        string slamDmg;
        int dice = GetZombieSlamDamageDice(size);
        int count = GetZombieSlamDamageCount(size);
        if (dice <= 1)
            slamDmg = "1";
        else
            slamDmg = $"{count}d{dice}";
        abilities.Add($"Slam ({slamDmg} + STR)");

        abilities.Add($"Toughness (+{(doubledHD <= 3 ? 3 : doubledHD)} HP)");
        abilities.Add($"CR {GetZombieCR(doubledHD)} ({originalHD} base HD → {doubledHD} zombie HD)");

        return abilities;
    }
}

/// <summary>
/// Factory for creating pre-defined zombie variants from base creature blueprints.
/// Since many "base" creatures don't exist in the database, this factory
/// defines inline base stats and applies the zombie template.
/// </summary>
public static class ZombieFactory
{
    // ────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────

    /// <summary>
    /// Create a zombie from an existing registered creature.
    /// </summary>
    public static NPCDefinition CreateFromRegistered(string baseCreatureId, string overrideId = null, string overrideName = null)
    {
        NPCDefinition baseDef = NPCDatabase.Get(baseCreatureId);
        if (baseDef == null)
        {
            Debug.LogWarning($"[ZombieFactory] Base creature '{baseCreatureId}' not found in database.");
            return null;
        }
        return ZombieTemplate.Apply(baseDef, overrideId, overrideName);
    }

    // ────────────────────────────────────────────
    //  Pre-defined Zombie Variants
    // ────────────────────────────────────────────

    /// <summary>
    /// Human Commoner Zombie — The classic MM zombie.
    /// Base: Human commoner 1 (Medium humanoid, 1 HD).
    /// MM 3.5e p.265-266.
    /// </summary>
    public static NPCDefinition HumanCommonerZombie()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_commoner",
            Name = "Human Commoner",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 11, CON = 12, WIS = 11, INT = 10, CHA = 8,
            BAB = 0,
            BaseSpeed = 6, // 30 ft
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
        };

        var zombie = ZombieTemplate.Apply(baseDef, "zombie_human_commoner", "Human Commoner Zombie");

        // MM zombie: no equipment, just slam
        zombie.EquipmentIds = new List<EquipmentSlotPair>();
        zombie.BackpackItemIds = new List<string>();

        // MM stats: 1 base HD → 2 zombie HD (2d12)
        // STR 15 (13+2), DEX 9 (11-2), no CON/INT, WIS 10, CHA 1
        // HP: 2d12 + Toughness = 13 + 3 = 16
        // BAB: +1 (half of 2 HD)
        // CR: 1/2

        zombie.Description = "A shambling human corpse with rotting flesh and empty, staring eyes. " +
                            "It lurches forward with mindless hunger, one jerky step at a time.";
        return zombie;
    }

    /// <summary>
    /// Human Warrior Zombie — Armed zombie variant.
    /// Base: Human warrior 1 (Medium humanoid, 1 HD).
    /// </summary>
    public static NPCDefinition HumanWarriorZombie()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_warrior",
            Name = "Human Warrior",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 11, CON = 12, WIS = 11, INT = 10, CHA = 8,
            BAB = 1,
            BaseSpeed = 6, // 30 ft
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
        };

        var zombie = ZombieTemplate.Apply(baseDef, "zombie_human_warrior", "Human Warrior Zombie");

        // Armed zombie with chainmail and longsword
        zombie.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.CHAINMAIL, EquipSlot.Armor),
            new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.RightHand)
        };
        zombie.BackpackItemIds = new List<string>();

        // 1 base HD → 2 zombie HD (2d12)
        // STR 15 (13+2), DEX 9 (11-2), no CON/INT, WIS 10, CHA 1
        // HP: 2d12 + Toughness = 13 + 3 = 16
        // Natural armor: 0 (base) + 2 (Medium zombie) = 2
        // BAB: +1 (half of 2 HD)
        // CR: 1/2

        zombie.Description = "A shambling warrior corpse in rusted chainmail, still clutching its longsword. " +
                            "It fights with crude, single-minded determination.";
        return zombie;
    }

    /// <summary>
    /// Troglodyte Zombie — Medium undead with stench-less slam.
    /// Base: Troglodyte (Medium humanoid, 2 HD).
    /// </summary>
    public static NPCDefinition TroglodyteZombie()
    {
        NPCDefinition baseTrog = NPCDatabase.Get("troglodyte");
        if (baseTrog == null)
        {
            // Fallback inline definition
            baseTrog = new NPCDefinition
            {
                Id = "base_troglodyte",
                Name = "Troglodyte",
                HitDice = 2,
                SizeCategory = SizeCategory.Medium,
                IsTallCreature = true,
                STR = 12, DEX = 9, CON = 14, WIS = 10, INT = 8, CHA = 10,
                BAB = 1,
                BaseSpeed = 6,
                NaturalArmorBonus = 6,
                CreatureType = "Humanoid",
                NaturalAttacks = new List<NaturalAttackDefinition>
                {
                    new NaturalAttackDefinition
                    {
                        Name = "Claw",
                        DamageDice = 4,
                        DamageCount = 1,
                        Count = 2,
                        BonusDamageSource = DamageBonusSource.Strength,
                        IsPrimary = true,
                        Range = 1
                    },
                    new NaturalAttackDefinition
                    {
                        Name = "Bite",
                        DamageDice = 4,
                        DamageCount = 1,
                        Count = 1,
                        BonusDamageSource = DamageBonusSource.StrengthHalf,
                        IsPrimary = false,
                        Range = 1
                    }
                }
            };
        }

        var zombie = ZombieTemplate.Apply(baseTrog, "zombie_troglodyte", "Troglodyte Zombie");
        zombie.EquipmentIds = new List<EquipmentSlotPair>();
        zombie.BackpackItemIds = new List<string>();

        // 2 base HD → 4 zombie HD
        // Keeps claws and bite (stripped of poison/stench), gains slam
        // CR: 1
        zombie.Description = "A troglodyte corpse, its scales dulled and flesh decaying. " +
                            "The stench of death has replaced its natural musk.";
        return zombie;
    }

    /// <summary>
    /// Ogre Zombie — Large giant zombie, classic encounter foe.
    /// Base: Ogre (Large giant, 4 HD).
    /// MM 3.5e p.266.
    /// </summary>
    public static NPCDefinition OgreZombie()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_ogre",
            Name = "Ogre",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 8, CON = 15, WIS = 10, INT = 6, CHA = 7,
            BAB = 3,
            BaseSpeed = 8, // 40 ft
            NaturalArmorBonus = 5,
            CreatureType = "Giant",
        };

        var zombie = ZombieTemplate.Apply(baseDef, "zombie_ogre", "Ogre Zombie");

        // Ogre zombie typically has a greatclub
        zombie.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.GREATCLUB, EquipSlot.Hands)
        };
        zombie.BackpackItemIds = new List<string>();

        // 4 base HD → 8 zombie HD
        // STR 23 (21+2), DEX 6 (8-2)
        // Natural armor: 5 (base) + 3 (Large zombie increase) = 8
        // HP: 8d12 + Toughness = 52 + 8 = 60
        // BAB: +4 (half of 8)
        // CR: 3
        zombie.Description = "A massive ogre corpse, towering and relentless. Its bloated form lurches forward " +
                            "with terrible strength, wielding a greatclub in its decomposing hands.";
        return zombie;
    }

    /// <summary>
    /// Minotaur Zombie — Large monstrous humanoid zombie.
    /// Base: Minotaur (Large monstrous humanoid, 6 HD).
    /// </summary>
    public static NPCDefinition MinotaurZombie()
    {
        var baseDef = BaseCreatureDefinitions.Minotaur();

        var zombie = ZombieTemplate.Apply(baseDef, "zombie_minotaur", "Minotaur Zombie");

        zombie.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.GREATAXE, EquipSlot.Hands)
        };
        zombie.BackpackItemIds = new List<string>();

        // 6 base HD → 12 zombie HD
        // STR 21 (19+2), DEX 8 (10-2)
        // Natural armor: 5 (base) + 3 (Large) = 8
        // HP: 12d12 + Toughness = 78 + 12 = 90
        // BAB: +6 (half of 12)
        // CR: 4
        zombie.Description = "A hulking minotaur zombie, its horned skull lowered menacingly. " +
                            "Rot has not diminished its terrible strength.";
        return zombie;
    }

    /// <summary>
    /// Owlbear Zombie — Large beast zombie.
    /// Base: Owlbear (Large magical beast, 5 HD).
    /// </summary>
    public static NPCDefinition OwlbearZombie()
    {
        var baseDef = BaseCreatureDefinitions.Owlbear();

        var zombie = ZombieTemplate.Apply(baseDef, "zombie_owlbear", "Owlbear Zombie");
        zombie.EquipmentIds = new List<EquipmentSlotPair>();
        zombie.BackpackItemIds = new List<string>();

        // 5 base HD → 10 zombie HD
        // STR 23 (21+2), DEX 10 (12-2)
        // Natural armor: 5 (base) + 3 (Large) = 8
        // HP: 10d12 + Toughness = 65 + 10 = 75
        // BAB: +5 (half of 10)
        // CR: 3
        // Note: loses improved grab!
        zombie.Description = "An owlbear zombie, its once-keen beak now hanging slack. " +
                            "Its massive claws still rake with devastating force.";
        return zombie;
    }

    /// <summary>
    /// Bugbear Zombie — Medium zombie variant.
    /// Base: Bugbear (Medium humanoid, 3 HD).
    /// </summary>
    public static NPCDefinition BugbearZombie()
    {
        NPCDefinition baseBugbear = NPCDatabase.Get("bugbear");
        if (baseBugbear == null)
        {
            baseBugbear = new NPCDefinition
            {
                Id = "base_bugbear",
                Name = "Bugbear",
                HitDice = 3,
                SizeCategory = SizeCategory.Medium,
                IsTallCreature = true,
                STR = 15, DEX = 12, CON = 13, WIS = 10, INT = 10, CHA = 9,
                BAB = 2,
                BaseSpeed = 6,
                NaturalArmorBonus = 3,
                CreatureType = "Humanoid",
            };
        }

        var zombie = ZombieTemplate.Apply(baseBugbear, "zombie_bugbear", "Bugbear Zombie");
        zombie.EquipmentIds = new List<EquipmentSlotPair>();
        zombie.BackpackItemIds = new List<string>();

        // 3 base HD → 6 zombie HD
        // CR: 2
        zombie.Description = "A dead bugbear, its fur matted with dried blood. " +
                            "Despite its rotting state, it still towers over most humanoids.";
        return zombie;
    }
}
