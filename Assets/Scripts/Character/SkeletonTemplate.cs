using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// D&D 3.5e Skeleton Template (Monster Manual p.226).
/// Converts any corporeal creature with a skeletal structure into an animated skeleton.
///
/// Template rules summary:
/// - Type changes to Undead
/// - Drop class HD, keep racial HD (minimum 1), convert to d12
/// - Recalculate HP from d12 HD (no CON bonus)
/// - Remove flight from wings (keep magical flight)
/// - Set natural armor by size
/// - Keep natural weapons and manufactured weapons
/// - Gain claw attacks (1 per hand for bipeds) at full BAB
/// - BAB = 1/2 HD (undead poor BAB)
/// - Remove all special attacks (breath weapons, poison, etc.)
/// - Gain: cold immunity, DR 5/bludgeoning, undead immunities
/// - Saves: Fort = HD/3, Ref = HD/3, Will = HD/2 + 2
/// - Abilities: DEX +2, no CON, no INT, WIS 10, CHA 1
/// - No skills; gain Improved Initiative
/// - Alignment: Always Neutral Evil
/// - CR by HD table
/// </summary>
public static class SkeletonTemplate
{
    // ────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────

    /// <summary>
    /// Apply the skeleton template to a base creature definition.
    /// Returns a new NPCDefinition with skeleton modifications.
    /// The base creature is NOT modified.
    /// </summary>
    /// <param name="baseCreature">The base creature definition to skeletonize.</param>
    /// <param name="overrideId">Optional ID override. If null, generates "skeleton_[baseid]".</param>
    /// <param name="overrideName">Optional name override. If null, generates "[BaseName] Skeleton".</param>
    /// <param name="hasHands">Whether the creature has hands for claw attacks (true for humanoids/bipeds).</param>
    /// <param name="hasWings">Whether the creature had wings (loses flight).</param>
    /// <returns>A new skeleton NPCDefinition.</returns>
    public static NPCDefinition Apply(
        NPCDefinition baseCreature,
        string overrideId = null,
        string overrideName = null,
        bool hasHands = true,
        bool hasWings = false)
    {
        if (baseCreature == null)
        {
            Debug.LogError("[SkeletonTemplate] Cannot apply template to null base creature.");
            return null;
        }

        // Start from a deep clone so we never mutate the database entry.
        NPCDefinition skel = baseCreature.Clone();

        // ── Identity ──
        skel.Id = overrideId ?? $"skeleton_{baseCreature.Id}";
        skel.Name = overrideName ?? $"{baseCreature.Name} Skeleton";

        // ── Type → Undead ──
        skel.CreatureType = "Undead";
        skel.MaterialComposition = MaterialComposition.Bone;

        // ── Hit Dice ──
        // Drop class HD, keep racial HD. Minimum 1 HD.
        // For creatures with class levels, we use their racial HitDice field.
        int racialHD = Mathf.Max(1, baseCreature.HitDice);
        skel.HitDice = racialHD;
        skel.Level = 0;
        skel.CharacterClass = null;

        // ── Abilities (modify before HP calc) ──
        // DEX +2, CON = none, INT = none, WIS = 10, CHA = 1
        skel.DEX = baseCreature.DEX + 2;
        skel.CON = CharacterStats.NO_SCORE;
        skel.INT = CharacterStats.NO_SCORE;
        skel.WIS = 10;
        skel.CHA = 1;
        // STR stays the same

        // ── HP from d12 HD (no CON modifier for undead) ──
        // Average d12 = 6.5, round down. Use standard d12 average.
        skel.BaseHitDieHP = (int)(racialHD * 6.5f);

        // ── BAB = 1/2 HD (Undead poor BAB) ──
        int bab = racialHD / 2;
        skel.BAB = bab;
        skel.BABOverride = BABProgression.Poor;
        skel.BaseAttackBonusOverride = bab;

        // ── Saves: Fort = HD/3, Ref = HD/3, Will = HD/2 + 2 ──
        // These are set as overrides since the template replaces all saves.
        skel.FortitudeSaveOverride = SaveProgression.Poor;
        skel.ReflexSaveOverride = SaveProgression.Poor;
        skel.WillSaveOverride = SaveProgression.Good;

        // ── Natural Armor by size ──
        skel.NaturalArmorBonus = GetSkeletonNaturalArmor(skel.SizeCategory);

        // ── Speed: keep base speed, remove flight from wings ──
        if (hasWings)
        {
            // Remove flight tags - skeleton wings don't work
            skel.CreatureTags.RemoveAll(t =>
                t.StartsWith("Fly", StringComparison.OrdinalIgnoreCase));
        }

        // ── Natural Attacks ──
        // Keep existing natural weapons (bite, tail slap, etc.)
        // but strip all on-hit special effects (poison, paralysis, disease, etc.)
        if (skel.NaturalAttacks != null)
        {
            for (int i = 0; i < skel.NaturalAttacks.Count; i++)
            {
                UndeadTemplateUtils.StripSpecialEffects(skel.NaturalAttacks[i]);
            }
        }

        // Add claw attacks for creatures with hands
        if (hasHands)
        {
            int clawDice = GetSkeletonClawDamageDice(skel.SizeCategory);
            int clawCount = GetSkeletonClawDamageCount(skel.SizeCategory);
            bool hasBite = false;
            bool hasOtherNatural = false;

            if (skel.NaturalAttacks != null)
            {
                for (int i = 0; i < skel.NaturalAttacks.Count; i++)
                {
                    string name = skel.NaturalAttacks[i].Name;
                    if (name != null && name.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0)
                        hasBite = true;
                    else if (name != null && name.IndexOf("Claw", StringComparison.OrdinalIgnoreCase) < 0)
                        hasOtherNatural = true;
                }
            }
            else
            {
                skel.NaturalAttacks = new List<NaturalAttackDefinition>();
            }

            // Remove any existing claw attacks (we'll add ours)
            skel.NaturalAttacks.RemoveAll(a =>
                a.Name != null && a.Name.IndexOf("Claw", StringComparison.OrdinalIgnoreCase) >= 0);

            // Claw attacks: 2 claws for bipeds, primary attacks
            // If creature has other natural attacks, claws are still primary for skeletons
            skel.NaturalAttacks.Insert(0, new NaturalAttackDefinition
            {
                Name = "Claw",
                DamageDice = clawDice,
                DamageCount = clawCount,
                Count = 2,
                BonusDamageSource = DamageBonusSource.Strength,
                IsPrimary = true,
                Range = 1
            });

            // If creature also has a bite, make it secondary (1/2 STR damage)
            if (hasBite)
            {
                for (int i = 0; i < skel.NaturalAttacks.Count; i++)
                {
                    if (skel.NaturalAttacks[i].Name != null &&
                        skel.NaturalAttacks[i].Name.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        skel.NaturalAttacks[i].IsPrimary = false;
                        skel.NaturalAttacks[i].BonusDamageSource = DamageBonusSource.StrengthHalf;
                    }
                }
            }
        }

        // ── Remove ALL special attacks ──
        skel.BreathWeapon = null;
        skel.SecondaryBreathWeapon = null;
        skel.FrightfulPresence = null;
        skel.Engulf = null;
        skel.AuraAbility = null;
        skel.StenchAuraDC = 0;
        skel.StenchAuraRange = 0;
        skel.HasTripAttack = false;
        skel.HasImprovedGrab = false;
        skel.HasPounce = false;
        skel.HasRake = false;
        skel.RakeAttack = null;
        skel.GainsSmiteEvil = false;
        skel.GainsSmiteGood = false;

        // ── Remove rake attack ──
        skel.RakeAttack = null;

        // ── Immunities: Undead immunities + cold ──
        skel.Immunities = ImmunityPresets.UndeadImmunities();
        skel.Immunities.immuneToCold = true;
        skel.DamageImmunities = new List<DamageType> { DamageType.Cold };
        skel.IsMindless = true;

        // ── DR 5/bludgeoning ──
        skel.DamageReductionAmount = 5;
        skel.DamageReductionBypass = DamageBypassTag.Bludgeoning;
        skel.DamageReductionRangedOnly = false;

        // ── Clear resistances from base creature ──
        skel.DamageResistances = new List<DamageResistanceEntry>();

        // ── Clear regeneration / SR ──
        skel.RegenerationAmount = 0;
        skel.RegenerationSuppressedBy = DamageBypassTag.None;
        skel.SpellResistance = 0;

        // ── Not incorporeal ──
        skel.IsIncorporeal = false;
        skel.IsSwarm = false;
        skel.SwarmTraits = new SwarmTraits();

        // ── No scent ──
        skel.HasScent = false;

        // ── Skills: None (mindless) ──
        // Already handled by IsMindless

        // ── Feats: Replace with Improved Initiative ──
        skel.Feats = new List<string> { "Improved Initiative" };
        skel.WeaponFocusChoice = null;

        // ── Spells: None ──
        skel.KnownSpellIds = new List<string>();
        skel.PreparedSpellSlotIds = new List<string>();

        // ── CR ──
        skel.ChallengeRating = GetSkeletonCR(racialHD);

        // ── Tags ──
        skel.CreatureTags = new List<string> { "Undead", "Skeleton" };

        // ── Special Abilities display ──
        skel.SpecialAbilities = BuildSkeletonAbilities(skel.SizeCategory, racialHD);

        // ── Template tracking ──
        skel.AppliedTemplateIds = new List<string> { "skeleton" };

        // ── AI: mindless melee ──
        skel.AIBehavior = NPCAIBehavior.AggressiveMelee;
        skel.AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless;
        skel.UseCoupDeGrace = null;
        skel.AITargetPriority = null;

        // ── Visuals: bone white ──
        skel.SpriteColor = new Color(0.85f, 0.85f, 0.75f, 1f);
        skel.PanelColor = new Color(0.2f, 0.2f, 0.3f, 0.85f);
        skel.NameColor = new Color(0.7f, 0.85f, 1f);

        // ── Description ──
        skel.Description = $"An animated skeleton of a {baseCreature.Name.ToLowerInvariant()}. " +
                          "Its bones clatter as it moves with unnatural purpose, " +
                          "eye sockets glowing with faint necromantic energy.";

        return skel;
    }

    // ────────────────────────────────────────────
    //  Lookup Tables
    // ────────────────────────────────────────────

    /// <summary>
    /// Natural armor bonus by size (MM p.226).
    /// </summary>
    public static int GetSkeletonNaturalArmor(SizeCategory size)
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
            case SizeCategory.Large:
                return 2;
            case SizeCategory.Huge:
                return 3;
            case SizeCategory.Gargantuan:
                return 6;
            case SizeCategory.Colossal:
                return 10;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Claw damage dice by size (MM p.226).
    /// Returns the die size (e.g., 4 for d4).
    /// For Fine/Diminutive (flat 1 damage), returns 1.
    /// </summary>
    public static int GetSkeletonClawDamageDice(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine:
            case SizeCategory.Diminutive:
                return 1;   // flat 1 damage
            case SizeCategory.Tiny:
                return 2;   // 1d2
            case SizeCategory.Small:
                return 3;   // 1d3
            case SizeCategory.Medium:
                return 4;   // 1d4
            case SizeCategory.Large:
                return 6;   // 1d6
            case SizeCategory.Huge:
                return 8;   // 1d8
            case SizeCategory.Gargantuan:
                return 6;   // 2d6
            case SizeCategory.Colossal:
                return 8;   // 2d8
            default:
                return 4;
        }
    }

    /// <summary>
    /// Claw damage count (number of dice) by size.
    /// Most sizes are 1dX; Gargantuan = 2d6, Colossal = 2d8.
    /// </summary>
    public static int GetSkeletonClawDamageCount(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Gargantuan:
            case SizeCategory.Colossal:
                return 2;
            default:
                return 1;
        }
    }

    /// <summary>
    /// CR by Hit Dice (MM p.226 table).
    /// Returns a string like "1/6", "1/3", "1", "2", etc.
    /// </summary>
    public static string GetSkeletonCR(int hitDice)
    {
        if (hitDice <= 0) return "1/6";
        if (hitDice == 1) return "1/3";
        if (hitDice <= 3) return "1";
        if (hitDice <= 5) return "2";
        if (hitDice <= 7) return "3";
        if (hitDice <= 9) return "4";
        if (hitDice <= 11) return "5";
        if (hitDice <= 14) return "6";
        if (hitDice <= 17) return "7";
        if (hitDice <= 20) return "8";
        return "9";
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
    /// Build the Special Abilities display list for a skeleton.
    /// </summary>
    private static List<string> BuildSkeletonAbilities(SizeCategory size, int hitDice)
    {
        var abilities = new List<string>();
        abilities.Add("Undead traits (immune to mind-affecting, poison, sleep, paralysis, stunning, disease, death effects)");
        abilities.Add("Immune to cold");
        abilities.Add("DR 5/bludgeoning");
        abilities.Add("Not subject to critical hits or sneak attack");
        abilities.Add("Darkvision 60 ft.");

        string clawDmg;
        int dice = GetSkeletonClawDamageDice(size);
        int count = GetSkeletonClawDamageCount(size);
        if (dice <= 1)
            clawDmg = "1";
        else
            clawDmg = $"{count}d{dice}";
        abilities.Add($"2 claws ({clawDmg} + STR)");

        abilities.Add($"CR {GetSkeletonCR(hitDice)} ({hitDice} HD)");

        return abilities;
    }
}

/// <summary>
/// Factory for creating pre-defined skeleton variants from base creature blueprints.
/// Since many "base" creatures don't exist in the database (owlbear, minotaur, etc.),
/// this factory defines inline base stats and applies the skeleton template.
/// </summary>
public static class SkeletonFactory
{
    // ────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────

    /// <summary>
    /// Create a skeleton from an existing registered creature.
    /// </summary>
    public static NPCDefinition CreateFromRegistered(string baseCreatureId, string overrideId = null, string overrideName = null, bool hasHands = true, bool hasWings = false)
    {
        NPCDefinition baseDef = NPCDatabase.Get(baseCreatureId);
        if (baseDef == null)
        {
            Debug.LogWarning($"[SkeletonFactory] Base creature '{baseCreatureId}' not found in database.");
            return null;
        }
        return SkeletonTemplate.Apply(baseDef, overrideId, overrideName, hasHands, hasWings);
    }

    // ────────────────────────────────────────────
    //  Pre-defined Skeleton Variants
    // ────────────────────────────────────────────

    /// <summary>
    /// Human Warrior Skeleton — The classic MM skeleton.
    /// Base: Human warrior 1 (Medium humanoid, 1 HD).
    /// MM 3.5e p.226.
    /// </summary>
    public static NPCDefinition HumanWarriorSkeleton()
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
            BaseSpeed = 6,
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
        };

        var skel = SkeletonTemplate.Apply(baseDef, "skeleton_human_warrior", "Human Warrior Skeleton", hasHands: true);

        // MM gives human warrior skeleton specific equipment
        skel.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.CHAIN_SHIRT, EquipSlot.Armor),
            new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.RightHand),
            new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.LeftHand)
        };
        skel.BackpackItemIds = new List<string>();

        // MM stats: STR 13, DEX 13 (11 base +2), no CON/INT, WIS 10, CHA 1
        // HP: 1d12 = 6.5 → 6
        // BAB: +0 (1/2 of 1 HD)
        // CR: 1/3

        skel.Description = "A human skeleton in rusted chainmail, gripping a longsword with bony fingers. The classic animated dead.";
        return skel;
    }

    /// <summary>
    /// Wolf Skeleton — Skeletal beast variant.
    /// Base: Wolf (Medium animal, 2 HD).
    /// </summary>
    public static NPCDefinition WolfSkeleton()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_wolf",
            Name = "Wolf",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 15, CON = 15, WIS = 12, INT = 2, CHA = 6,
            BAB = 1,
            BaseSpeed = 10, // 50 ft
            NaturalArmorBonus = 2,
            CreatureType = "Animal",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite",
                    DamageDice = 6,
                    DamageCount = 1,
                    Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                    IsPrimary = true,
                    Range = 1
                }
            }
        };

        var skel = SkeletonTemplate.Apply(baseDef, "skeleton_wolf", "Wolf Skeleton", hasHands: false);

        // Wolf skeleton has no claws (quadruped), only bite
        skel.EquipmentIds = new List<EquipmentSlotPair>();
        skel.BackpackItemIds = new List<string>();

        // 2 HD → CR 1
        skel.Description = "A wolf skeleton, jaws clacking as it stalks forward. Strips of dried sinew cling to its bones.";
        return skel;
    }

    /// <summary>
    /// Owlbear Skeleton — Large beast skeleton.
    /// Base: Owlbear (Large magical beast, 5 HD).
    /// MM 3.5e p.226 skeleton examples.
    /// </summary>
    public static NPCDefinition OwlbearSkeleton()
    {
        var baseDef = BaseCreatureDefinitions.Owlbear();

        var skel = SkeletonTemplate.Apply(baseDef, "skeleton_owlbear", "Owlbear Skeleton", hasHands: true);

        skel.EquipmentIds = new List<EquipmentSlotPair>();
        skel.BackpackItemIds = new List<string>();

        // 5 HD → CR 2
        // Large skeleton: nat armor +2, claw 1d6
        skel.Description = "The towering skeleton of an owlbear, its massive beak-skull and raking claws still terrifying even stripped of flesh.";
        return skel;
    }

    /// <summary>
    /// Minotaur Skeleton — Large humanoid skeleton.
    /// Base: Minotaur (Large monstrous humanoid, 6 HD).
    /// </summary>
    public static NPCDefinition MinotaurSkeleton()
    {
        var baseDef = BaseCreatureDefinitions.Minotaur();

        var skel = SkeletonTemplate.Apply(baseDef, "skeleton_minotaur", "Minotaur Skeleton", hasHands: true);

        // Minotaur skeleton typically wields a greataxe
        skel.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.GREATAXE, EquipSlot.Hands)
        };
        skel.BackpackItemIds = new List<string>();

        // 6 HD → CR 3
        skel.Description = "A massive skeletal minotaur, horned skull lowered menacingly. Its bony hands grip a rusted greataxe.";
        return skel;
    }

    /// <summary>
    /// Megaraptor Skeleton — Huge dinosaur skeleton.
    /// Base: Megaraptor (Huge animal, 8 HD).
    /// MM 3.5e p.226 skeleton examples.
    /// </summary>
    public static NPCDefinition MegaraptorSkeleton()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_megaraptor",
            Name = "Megaraptor",
            HitDice = 8,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = true,
            STR = 21, DEX = 15, CON = 21, WIS = 15, INT = 2, CHA = 10,
            BAB = 6,
            BaseSpeed = 12, // 60 ft
            NaturalArmorBonus = 4,
            CreatureType = "Animal",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Talons",
                    DamageDice = 8,
                    DamageCount = 2,
                    Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength,
                    IsPrimary = true,
                    Range = 1
                },
                new NaturalAttackDefinition
                {
                    Name = "Bite",
                    DamageDice = 8,
                    DamageCount = 1,
                    Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf,
                    IsPrimary = false,
                    Range = 1
                },
                new NaturalAttackDefinition
                {
                    Name = "Foreclaw",
                    DamageDice = 4,
                    DamageCount = 1,
                    Count = 2,
                    BonusDamageSource = DamageBonusSource.StrengthHalf,
                    IsPrimary = false,
                    Range = 1
                }
            },
            HasPounce = true
        };

        var skel = SkeletonTemplate.Apply(baseDef, "skeleton_megaraptor", "Megaraptor Skeleton", hasHands: false);

        skel.EquipmentIds = new List<EquipmentSlotPair>();
        skel.BackpackItemIds = new List<string>();

        // 8 HD → CR 4
        skel.Description = "A nightmarish dinosaur skeleton towers above, its massive talons scraping the ground. A fearsome guardian for any crypt.";
        return skel;
    }

    /// <summary>
    /// War Horse Skeleton — Large animal skeleton, common undead mount.
    /// Base: Heavy warhorse (Large animal, 4 HD).
    /// </summary>
    public static NPCDefinition HorseSkeleton()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_warhorse",
            Name = "Heavy Warhorse",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 18, DEX = 13, CON = 17, WIS = 13, INT = 2, CHA = 6,
            BAB = 3,
            BaseSpeed = 10, // 50 ft
            NaturalArmorBonus = 4,
            CreatureType = "Animal",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Hoof",
                    DamageDice = 6,
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

        var skel = SkeletonTemplate.Apply(baseDef, "skeleton_warhorse", "Warhorse Skeleton", hasHands: false);

        skel.EquipmentIds = new List<EquipmentSlotPair>();
        skel.BackpackItemIds = new List<string>();

        // 4 HD → CR 2
        skel.Description = "A skeletal warhorse, barding of bone and empty eye sockets. It gallops silently, driven by dark magic.";
        return skel;
    }

    /// <summary>
    /// Troll Skeleton — Large regenerating creature made into a skeleton.
    /// Base: Troll (Large giant, 6 HD).
    /// Note: skeleton loses regeneration!
    /// </summary>
    public static NPCDefinition TrollSkeleton()
    {
        NPCDefinition baseTroll = NPCDatabase.Get("troll");
        if (baseTroll == null)
        {
            Debug.LogWarning("[SkeletonFactory] Troll not found in database, creating from scratch.");
            baseTroll = new NPCDefinition
            {
                Id = "base_troll",
                Name = "Troll",
                HitDice = 6,
                SizeCategory = SizeCategory.Large,
                IsTallCreature = true,
                STR = 23, DEX = 14, CON = 23, WIS = 9, INT = 6, CHA = 6,
                BAB = 4,
                BaseSpeed = 6,
                NaturalArmorBonus = 5,
                CreatureType = "Giant",
            };
        }

        var skel = SkeletonTemplate.Apply(baseTroll, "skeleton_troll", "Troll Skeleton", hasHands: true);
        skel.EquipmentIds = new List<EquipmentSlotPair>();
        skel.BackpackItemIds = new List<string>();

        // 6 HD → CR 3
        skel.Description = "A lanky troll skeleton, its elongated arms ending in razor-sharp bone claws. Unlike its living counterpart, it cannot regenerate.";
        return skel;
    }
}
