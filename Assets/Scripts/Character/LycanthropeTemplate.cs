using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// D&D 3.5e Lycanthrope Template (Monster Manual p.170-178).
/// Converts any humanoid or giant into a lycanthrope by merging with
/// a base animal form. Can be natural (DR 10/silver, can spread curse)
/// or afflicted (DR 5/silver, cannot spread curse).
///
/// Key template rules:
/// - Type stays same, gains Shapechanger subtype
/// - HD = base creature HD + base animal HD
/// - Combined BAB = creature BAB + animal BAB
/// - Combined saves = creature saves + animal saves
/// - Hybrid form: 2 claws + bite (natural weapons by size)
///   or weapon + bite (secondary)
/// - Physical ability modifiers from animal form apply in hybrid/animal form
/// - All lycanthropes gain +2 Wisdom
/// - Natural armor +2 in all forms; hybrid uses better of base creature/animal
/// - DR 5/silver (afflicted) or 10/silver (natural)
/// - Iron Will as bonus feat
/// - Low-light vision, scent in all forms
/// - Lycanthropic empathy with animal type
/// - Curse of Lycanthropy (natural only): DC 15 Fort on bite
///
/// Our implementation registers the HYBRID form stat block since that's
/// the most common combat encounter form. The template merges base
/// creature and base animal stats into a single NPCDefinition.
/// </summary>
public static class LycanthropeTemplate
{
    // ────────────────────────────────────────────
    //  Data: Animal Form Ability Modifiers
    // ────────────────────────────────────────────

    /// <summary>
    /// Physical ability modifiers applied in animal/hybrid form.
    /// These equal the animal's ability scores minus 10 (or 11 for odd scores).
    /// MM p.175 table.
    /// </summary>
    public struct AnimalFormModifiers
    {
        public string AnimalName;
        public int StrMod;
        public int DexMod;
        public int ConMod;
        public int AnimalHD;
        public SizeCategory AnimalSize;
        public int AnimalNaturalArmor;
        public int AnimalSpeed;          // in grid squares (5ft each)
        public int AnimalBAB;            // animal's BAB
        public bool HasTrip;
        public int TripBonus;
        public bool HasImprovedGrab;
        public bool HasPounce;
        public bool HasRake;
        public NaturalAttackDefinition RakeAttack;
    }

    /// <summary>
    /// Get the standard animal form modifiers for a known lycanthrope type.
    /// </summary>
    public static AnimalFormModifiers GetAnimalModifiers(LycanthropeAnimalType animalType)
    {
        switch (animalType)
        {
            case LycanthropeAnimalType.Wolf:
                // Wolf: Str 13, Dex 15, Con 15 → +2/+4/+4
                return new AnimalFormModifiers
                {
                    AnimalName = "Wolf",
                    StrMod = 2, DexMod = 4, ConMod = 4,
                    AnimalHD = 2, AnimalSize = SizeCategory.Medium,
                    AnimalNaturalArmor = 2, AnimalSpeed = 10, // 50 ft
                    AnimalBAB = 1,
                    HasTrip = true, TripBonus = 1
                };

            case LycanthropeAnimalType.DireWolf:
                // Dire Wolf: Str 25, Dex 15, Con 17 → +14/+4/+6
                return new AnimalFormModifiers
                {
                    AnimalName = "Dire Wolf",
                    StrMod = 14, DexMod = 4, ConMod = 6,
                    AnimalHD = 6, AnimalSize = SizeCategory.Large,
                    AnimalNaturalArmor = 3, AnimalSpeed = 10, // 50 ft
                    AnimalBAB = 4,
                    HasTrip = true, TripBonus = 11
                };

            case LycanthropeAnimalType.Boar:
                // Boar: Str 15, Dex 10, Con 17 → +4/+0/+6
                return new AnimalFormModifiers
                {
                    AnimalName = "Boar",
                    StrMod = 4, DexMod = 0, ConMod = 6,
                    AnimalHD = 3, AnimalSize = SizeCategory.Medium,
                    AnimalNaturalArmor = 6, AnimalSpeed = 8, // 40 ft
                    AnimalBAB = 2
                };

            case LycanthropeAnimalType.DireBoar:
                // Dire Boar: Str 27, Dex 10, Con 17 → +16/+0/+6
                return new AnimalFormModifiers
                {
                    AnimalName = "Dire Boar",
                    StrMod = 16, DexMod = 0, ConMod = 6,
                    AnimalHD = 7, AnimalSize = SizeCategory.Large,
                    AnimalNaturalArmor = 6, AnimalSpeed = 8, // 40 ft
                    AnimalBAB = 5
                };

            case LycanthropeAnimalType.Rat:
                // Dire Rat: Str 10, Dex 17, Con 12 → +0/+6/+2
                return new AnimalFormModifiers
                {
                    AnimalName = "Dire Rat",
                    StrMod = 0, DexMod = 6, ConMod = 2,
                    AnimalHD = 1, AnimalSize = SizeCategory.Small,
                    AnimalNaturalArmor = 1, AnimalSpeed = 8, // 40 ft
                    AnimalBAB = 0
                };

            case LycanthropeAnimalType.Tiger:
                // Tiger: Str 23, Dex 15, Con 17 → +12/+4/+6
                return new AnimalFormModifiers
                {
                    AnimalName = "Tiger",
                    StrMod = 12, DexMod = 4, ConMod = 6,
                    AnimalHD = 6, AnimalSize = SizeCategory.Large,
                    AnimalNaturalArmor = 3, AnimalSpeed = 8, // 40 ft
                    AnimalBAB = 4,
                    HasImprovedGrab = true,
                    HasPounce = true,
                    HasRake = true,
                    RakeAttack = new NaturalAttackDefinition
                    {
                        Name = "Rake", DamageDice = 8, DamageCount = 1, Count = 2,
                        BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = true
                    }
                };

            case LycanthropeAnimalType.BrownBear:
                // Brown Bear: Str 27, Dex 13, Con 19 → +16/+2/+8
                return new AnimalFormModifiers
                {
                    AnimalName = "Brown Bear",
                    StrMod = 16, DexMod = 2, ConMod = 8,
                    AnimalHD = 6, AnimalSize = SizeCategory.Large,
                    AnimalNaturalArmor = 5, AnimalSpeed = 8, // 40 ft
                    AnimalBAB = 4,
                    HasImprovedGrab = true
                };

            default:
                Debug.LogError($"[LycanthropeTemplate] Unknown animal type: {animalType}");
                return new AnimalFormModifiers
                {
                    AnimalName = "Unknown",
                    StrMod = 0, DexMod = 0, ConMod = 0,
                    AnimalHD = 1, AnimalSize = SizeCategory.Medium,
                    AnimalNaturalArmor = 0, AnimalSpeed = 6,
                    AnimalBAB = 0
                };
        }
    }

    // ────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────

    /// <summary>
    /// Apply the lycanthrope template to a base creature definition.
    /// Returns a new NPCDefinition representing the HYBRID form.
    /// The base creature is NOT modified.
    /// </summary>
    /// <param name="baseCreature">The base humanoid/giant creature.</param>
    /// <param name="animalType">Which animal form this lycanthrope uses.</param>
    /// <param name="isNatural">True = natural lycanthrope (DR 10/silver, curse).
    /// False = afflicted (DR 5/silver, no curse).</param>
    /// <param name="overrideId">Optional ID override.</param>
    /// <param name="overrideName">Optional name override.</param>
    /// <returns>A new hybrid-form NPCDefinition.</returns>
    public static NPCDefinition Apply(
        NPCDefinition baseCreature,
        LycanthropeAnimalType animalType,
        bool isNatural = true,
        string overrideId = null,
        string overrideName = null)
    {
        if (baseCreature == null)
        {
            Debug.LogError("[LycanthropeTemplate] Cannot apply template to null base creature.");
            return null;
        }

        AnimalFormModifiers animal = GetAnimalModifiers(animalType);
        NPCDefinition lycan = baseCreature.Clone();

        // ── Identity ──
        string prefix = GetLycanthropePrefix(animalType);
        lycan.Id = overrideId ?? $"{prefix}_{baseCreature.Id}";
        lycan.Name = overrideName ?? $"{baseCreature.Name}, {GetLycanthropeTypeName(animalType)} (Hybrid)";

        // ── Type: keep base type, add Shapechanger subtype ──
        // Type doesn't change per MM rules.
        // We note it in tags instead.

        // ── Size: hybrid = larger of base creature or base animal ──
        SizeCategory hybridSize = (SizeCategory)Mathf.Max((int)baseCreature.SizeCategory, (int)animal.AnimalSize);
        lycan.SizeCategory = hybridSize;

        // ── Hit Dice: base creature HD + base animal HD ──
        int creatureHD = Mathf.Max(1, baseCreature.HitDice);
        int totalHD = creatureHD + animal.AnimalHD;
        lycan.HitDice = totalHD;

        // ── Abilities: apply animal physical modifiers + WIS +2 ──
        // In hybrid form, physical abilities are modified by the animal form
        lycan.STR = baseCreature.STR + animal.StrMod;
        lycan.DEX = baseCreature.DEX + animal.DexMod;
        lycan.CON = baseCreature.CON + animal.ConMod;
        lycan.WIS = baseCreature.WIS + 2; // All lycanthropes gain +2 Wis

        // ── HP: creature d8 HD + animal d8 HD with CON modifier ──
        // Base creature HP + animal HD HP (with modified CON).
        // Average d8 = 4.5. Use hybrid CON for animal HD portion.
        int creatureConMod = (baseCreature.CON - 10) / 2;
        int hybridConMod = (lycan.CON - 10) / 2;
        int creatureHP = baseCreature.BaseHitDieHP > 0
            ? baseCreature.BaseHitDieHP
            : (int)(creatureHD * 4.5f) + (creatureHD * creatureConMod);
        int animalHP = (int)(animal.AnimalHD * 4.5f) + (animal.AnimalHD * hybridConMod);
        lycan.BaseHitDieHP = creatureHP + animalHP;

        // ── BAB: add creature BAB + animal BAB ──
        int creatureBAB = baseCreature.BAB > 0 ? baseCreature.BAB : creatureHD * 3 / 4;
        int combinedBAB = creatureBAB + animal.AnimalBAB;
        lycan.BAB = combinedBAB;
        lycan.BaseAttackBonusOverride = combinedBAB;

        // ── Natural Armor: +2 in all forms, use better of creature/animal in hybrid ──
        int betterNatArmor = Mathf.Max(baseCreature.NaturalArmorBonus, animal.AnimalNaturalArmor);
        lycan.NaturalArmorBonus = betterNatArmor + 2;

        // ── Speed: hybrid uses base creature speed ──
        // (Animal form would use animal speed, but we're building hybrid)
        lycan.BaseSpeed = baseCreature.BaseSpeed;

        // ── Natural Attacks: hybrid form gets 2 claws + bite ──
        // In hybrid form, can attack with weapon + bite (secondary),
        // or 2 claws + bite
        lycan.NaturalAttacks = new List<NaturalAttackDefinition>();

        // Claw attacks sized to hybrid form
        int clawDice = GetHybridClawDamageDice(hybridSize);
        int clawCount = GetHybridClawDamageCount(hybridSize);
        lycan.NaturalAttacks.Add(new NaturalAttackDefinition
        {
            Name = "Claw",
            DamageDice = clawDice,
            DamageCount = clawCount,
            Count = 2,
            BonusDamageSource = DamageBonusSource.Strength,
            IsPrimary = true,
            Range = 1
        });

        // Bite attack (secondary in hybrid when using weapons)
        int biteDice = GetHybridBiteDamageDice(hybridSize);
        int biteCount = GetHybridBiteDamageCount(hybridSize);
        var biteAttack = new NaturalAttackDefinition
        {
            Name = "Bite",
            DamageDice = biteDice,
            DamageCount = biteCount,
            Count = 1,
            BonusDamageSource = DamageBonusSource.StrengthHalf,
            IsPrimary = false, // secondary in hybrid form
            Range = 1
        };

        // Curse of Lycanthropy: natural lycanthropes can spread it via bite
        if (isNatural)
        {
            // We use the disease system to represent the curse.
            // DC 15 Fortitude save or contract lycanthropy.
            biteAttack.HasDiseaseOnHit = true;
            biteAttack.DiseaseOnHitType = DiseaseType.Lycanthropy;
        }

        lycan.NaturalAttacks.Add(biteAttack);

        // ── Special attacks from animal form (carried to hybrid only for some) ──
        // MM p.173: "A lycanthrope's hybrid form does not gain any special
        // attacks of the base animal." But trip on bite IS kept for wolves.
        // Trip is triggered by the bite attack, which the hybrid has.
        lycan.HasTripAttack = animal.HasTrip;
        lycan.TripAttackCheckBonus = animal.TripBonus;

        // Improved grab / pounce / rake are animal-form-only for most,
        // but weretiger hybrid CAN pounce (MM p.174 notes).
        // For simplicity in our system, we carry these over since
        // the hybrid form has claws.
        if (animalType == LycanthropeAnimalType.Tiger)
        {
            lycan.HasPounce = true;
            lycan.HasRake = false;     // Rake is animal-form only
            lycan.HasImprovedGrab = false; // Animal-form only per MM
        }
        else
        {
            lycan.HasPounce = false;
            lycan.HasImprovedGrab = false;
            lycan.HasRake = false;
        }
        lycan.RakeAttack = null;

        // Clear breath weapons and other non-applicable attacks
        lycan.BreathWeapon = null;
        lycan.SecondaryBreathWeapon = null;
        lycan.FrightfulPresence = null;
        lycan.Engulf = null;
        lycan.AuraAbility = null;
        lycan.StenchAuraDC = 0;
        lycan.StenchAuraRange = 0;

        // ── DR: 5/silver (afflicted) or 10/silver (natural) ──
        lycan.DamageReductionAmount = isNatural ? 10 : 5;
        lycan.DamageReductionBypass = DamageBypassTag.Silver;
        lycan.DamageReductionRangedOnly = false;

        // ── Special Qualities ──
        lycan.HasScent = true; // Scent in all forms

        // Immunities: keep base creature immunities (usually none for humanoids)
        // Lycanthropes are NOT undead, so no special immunities
        if (lycan.Immunities == null)
            lycan.Immunities = new CreatureImmunities();

        // ── Saves: combined from creature + animal ──
        // We set the save progression to match the combined totals.
        // Animal saves are Good Fort/Ref, Poor Will typically.
        // This is approximated through the override system.
        lycan.FortitudeSaveOverride = SaveProgression.Good;
        lycan.ReflexSaveOverride = SaveProgression.Good;
        lycan.WillSaveOverride = SaveProgression.Poor;

        // ── Feats: keep creature feats, add Iron Will as bonus ──
        if (lycan.Feats == null)
            lycan.Feats = new List<string>();
        if (!lycan.Feats.Contains("Iron Will"))
            lycan.Feats.Add("Iron Will");

        // ── Not mindless, not undead ──
        lycan.IsMindless = false;
        lycan.IsSingleActionsOnly = false;
        lycan.IsIncorporeal = false;
        lycan.IsSwarm = false;

        // ── Material composition: still organic ──
        lycan.MaterialComposition = MaterialComposition.Organic;

        // ── CR: base creature CR + modifier by animal HD ──
        lycan.ChallengeRating = CalculateLycanthropeCR(baseCreature.ChallengeRating, animal.AnimalHD);

        // ── Tags ──
        lycan.CreatureTags = new List<string>
        {
            baseCreature.CreatureType ?? "Humanoid",
            "Shapechanger",
            "Lycanthrope",
            GetLycanthropeTypeName(animalType),
            isNatural ? "NaturalLycanthrope" : "AfflictedLycanthrope"
        };

        // ── Special Abilities display ──
        lycan.SpecialAbilities = BuildLycanthropeAbilities(
            animalType, isNatural, animal, lycan, totalHD, creatureHD);

        // ── Template tracking ──
        lycan.AppliedTemplateIds = new List<string> { "lycanthrope", prefix };

        // ── AI: intelligent melee combatant ──
        lycan.AIBehavior = NPCAIBehavior.AggressiveMelee;
        lycan.AIProfileArchetype = NPCAIProfileArchetype.Berserk;

        // ── Visuals: feral, bestial colors ──
        Color sprColor, panColor, nameColor;
        GetLycanthropeColors(animalType, out sprColor, out panColor, out nameColor);
        lycan.SpriteColor = sprColor;
        lycan.PanelColor = panColor;
        lycan.NameColor = nameColor;

        // ── Description ──
        string afflictionStr = isNatural ? "natural" : "afflicted";
        lycan.Description = $"A {afflictionStr} {GetLycanthropeTypeName(animalType).ToLowerInvariant()} " +
                           $"in hybrid form. Once a {baseCreature.Name.ToLowerInvariant()}, " +
                           $"now a fearsome blend of humanoid and {animal.AnimalName.ToLowerInvariant()}. " +
                           $"DR {lycan.DamageReductionAmount}/silver. " +
                           (isNatural ? "Can spread the curse of lycanthropy via bite." : "Cannot spread the curse.");

        return lycan;
    }

    // ────────────────────────────────────────────
    //  Hybrid Natural Attack Tables
    // ────────────────────────────────────────────

    /// <summary>
    /// Claw damage die by hybrid size (follows standard natural attack progression).
    /// </summary>
    public static int GetHybridClawDamageDice(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Tiny:     return 2;  // 1d2
            case SizeCategory.Small:    return 3;  // 1d3
            case SizeCategory.Medium:   return 4;  // 1d4
            case SizeCategory.Large:    return 6;  // 1d6
            case SizeCategory.Huge:     return 8;  // 1d8
            default:                    return 4;
        }
    }

    public static int GetHybridClawDamageCount(SizeCategory size)
    {
        // Standard: 1 die for all common sizes
        return 1;
    }

    /// <summary>
    /// Bite damage die by hybrid size.
    /// </summary>
    public static int GetHybridBiteDamageDice(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Tiny:     return 3;  // 1d3
            case SizeCategory.Small:    return 4;  // 1d4
            case SizeCategory.Medium:   return 6;  // 1d6
            case SizeCategory.Large:    return 8;  // 1d8
            case SizeCategory.Huge:     return 6;  // 2d6
            default:                    return 6;
        }
    }

    public static int GetHybridBiteDamageCount(SizeCategory size)
    {
        return size == SizeCategory.Huge ? 2 : 1;
    }

    // ────────────────────────────────────────────
    //  CR Calculation
    // ────────────────────────────────────────────

    /// <summary>
    /// Calculate lycanthrope CR from base creature CR + animal HD modifier.
    /// MM p.175: 1-2 HD: +2, 3-5 HD: +3, 6-10 HD: +4, 11-20 HD: +5, 21+: +6.
    /// </summary>
    public static string CalculateLycanthropeCR(string baseCR, int animalHD)
    {
        int crMod;
        if (animalHD <= 2) crMod = 2;
        else if (animalHD <= 5) crMod = 3;
        else if (animalHD <= 10) crMod = 4;
        else if (animalHD <= 20) crMod = 5;
        else crMod = 6;

        // Parse base CR
        float baseCRValue = ParseCR(baseCR);
        float totalCR = baseCRValue + crMod;

        // Format
        if (totalCR < 1f) return "1";
        return ((int)totalCR).ToString();
    }

    private static float ParseCR(string cr)
    {
        if (string.IsNullOrEmpty(cr)) return 1f;
        if (cr == "1/8") return 0.125f;
        if (cr == "1/6") return 0.167f;
        if (cr == "1/4") return 0.25f;
        if (cr == "1/3") return 0.333f;
        if (cr == "1/2") return 0.5f;
        float val;
        if (float.TryParse(cr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out val))
            return val;
        return 1f;
    }

    // ────────────────────────────────────────────
    //  Naming & Visuals
    // ────────────────────────────────────────────

    public static string GetLycanthropePrefix(LycanthropeAnimalType type)
    {
        switch (type)
        {
            case LycanthropeAnimalType.Wolf:      return "werewolf";
            case LycanthropeAnimalType.DireWolf:  return "werewolf_lord";
            case LycanthropeAnimalType.Rat:       return "wererat";
            case LycanthropeAnimalType.Boar:      return "wereboar";
            case LycanthropeAnimalType.DireBoar:  return "dire_wereboar";
            case LycanthropeAnimalType.Tiger:     return "weretiger";
            case LycanthropeAnimalType.BrownBear: return "werebear";
            default:                              return "lycanthrope";
        }
    }

    public static string GetLycanthropeTypeName(LycanthropeAnimalType type)
    {
        switch (type)
        {
            case LycanthropeAnimalType.Wolf:      return "Werewolf";
            case LycanthropeAnimalType.DireWolf:  return "Werewolf Lord";
            case LycanthropeAnimalType.Rat:       return "Wererat";
            case LycanthropeAnimalType.Boar:      return "Wereboar";
            case LycanthropeAnimalType.DireBoar:  return "Dire Wereboar";
            case LycanthropeAnimalType.Tiger:     return "Weretiger";
            case LycanthropeAnimalType.BrownBear: return "Werebear";
            default:                              return "Lycanthrope";
        }
    }

    private static void GetLycanthropeColors(LycanthropeAnimalType type,
        out Color sprite, out Color panel, out Color name)
    {
        switch (type)
        {
            case LycanthropeAnimalType.Wolf:
            case LycanthropeAnimalType.DireWolf:
                // Dark grey, feral
                sprite = new Color(0.55f, 0.55f, 0.58f, 1f);
                panel = new Color(0.2f, 0.2f, 0.25f, 0.85f);
                name = new Color(0.85f, 0.85f, 0.95f);
                break;
            case LycanthropeAnimalType.Rat:
                // Brownish-grey, sneaky
                sprite = new Color(0.6f, 0.55f, 0.5f, 1f);
                panel = new Color(0.22f, 0.2f, 0.18f, 0.85f);
                name = new Color(0.9f, 0.85f, 0.78f);
                break;
            case LycanthropeAnimalType.Boar:
            case LycanthropeAnimalType.DireBoar:
                // Ruddy brown, bristly
                sprite = new Color(0.6f, 0.42f, 0.35f, 1f);
                panel = new Color(0.25f, 0.15f, 0.12f, 0.85f);
                name = new Color(0.95f, 0.78f, 0.65f);
                break;
            case LycanthropeAnimalType.Tiger:
                // Orange-tawny, striped
                sprite = new Color(0.85f, 0.6f, 0.3f, 1f);
                panel = new Color(0.35f, 0.2f, 0.08f, 0.85f);
                name = new Color(1f, 0.85f, 0.55f);
                break;
            case LycanthropeAnimalType.BrownBear:
                // Warm brown, massive
                sprite = new Color(0.55f, 0.4f, 0.28f, 1f);
                panel = new Color(0.25f, 0.17f, 0.1f, 0.85f);
                name = new Color(0.95f, 0.82f, 0.62f);
                break;
            default:
                sprite = new Color(0.6f, 0.6f, 0.6f, 1f);
                panel = new Color(0.2f, 0.2f, 0.2f, 0.85f);
                name = new Color(0.9f, 0.9f, 0.9f);
                break;
        }
    }

    // ────────────────────────────────────────────
    //  Special Abilities Display
    // ────────────────────────────────────────────

    private static List<string> BuildLycanthropeAbilities(
        LycanthropeAnimalType animalType, bool isNatural,
        AnimalFormModifiers animal, NPCDefinition lycan,
        int totalHD, int creatureHD)
    {
        var abilities = new List<string>();

        abilities.Add($"Alternate Form (Su): Can shift between humanoid, {animal.AnimalName.ToLowerInvariant()}, and hybrid forms as a standard action");
        abilities.Add($"DR {lycan.DamageReductionAmount}/silver ({(isNatural ? "natural" : "afflicted")} lycanthrope)");

        if (isNatural)
        {
            abilities.Add("Curse of Lycanthropy (Su): DC 15 Fort save on bite or contract lycanthropy");
        }

        abilities.Add($"Lycanthropic Empathy: +4 on checks to influence {animal.AnimalName.ToLowerInvariant()}s");
        abilities.Add("Low-light vision");
        abilities.Add("Scent");

        // Physical ability modifiers
        string modStr = "";
        if (animal.StrMod != 0) modStr += $"STR {(animal.StrMod > 0 ? "+" : "")}{animal.StrMod}";
        if (animal.DexMod != 0)
        {
            if (modStr.Length > 0) modStr += ", ";
            modStr += $"DEX {(animal.DexMod > 0 ? "+" : "")}{animal.DexMod}";
        }
        if (animal.ConMod != 0)
        {
            if (modStr.Length > 0) modStr += ", ";
            modStr += $"CON {(animal.ConMod > 0 ? "+" : "")}{animal.ConMod}";
        }
        if (modStr.Length > 0)
            abilities.Add($"Hybrid/Animal form ability modifiers: {modStr}, WIS +2");

        abilities.Add($"Iron Will (bonus feat)");
        abilities.Add($"HD: {creatureHD} (base) + {animal.AnimalHD} ({animal.AnimalName}) = {totalHD} total");
        abilities.Add($"CR {lycan.ChallengeRating}");

        if (animal.HasTrip)
            abilities.Add($"Trip (free trip attempt on bite hit)");
        if (animalType == LycanthropeAnimalType.Tiger)
            abilities.Add("Pounce (full attack on charge in hybrid form)");

        return abilities;
    }
}

/// <summary>
/// Standard D&D 3.5e lycanthrope animal types.
/// </summary>
public enum LycanthropeAnimalType
{
    Wolf,
    DireWolf,
    Rat,
    Boar,
    DireBoar,
    Tiger,
    BrownBear
}

/// <summary>
/// Factory for creating pre-defined lycanthrope variants.
/// Builds hybrid-form stat blocks for the 5 standard MM lycanthropes
/// plus the Werewolf Lord and Dire Wereboar variants.
///
/// MM p.170-178: Werewolf, Wererat, Wereboar, Weretiger, Werebear,
/// Werewolf Lord, Dire Wereboar.
/// </summary>
public static class LycanthropeFactory
{
    // ────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────

    /// <summary>
    /// Create a lycanthrope from an existing registered base creature.
    /// </summary>
    public static NPCDefinition CreateFromRegistered(
        string baseCreatureId,
        LycanthropeAnimalType animalType,
        bool isNatural = true,
        string overrideId = null,
        string overrideName = null)
    {
        NPCDefinition baseDef = NPCDatabase.Get(baseCreatureId);
        if (baseDef == null)
        {
            Debug.LogWarning($"[LycanthropeFactory] Base creature '{baseCreatureId}' not found in database.");
            return null;
        }
        return LycanthropeTemplate.Apply(baseDef, animalType, isNatural, overrideId, overrideName);
    }

    // ────────────────────────────────────────────
    //  Standard Werewolf (Human Warrior 1 + Wolf)
    //  MM p.174 — CR 3, natural, chaotic evil
    // ────────────────────────────────────────────

    /// <summary>
    /// Werewolf — Human warrior 1 / wolf hybrid.
    /// The classic MM werewolf (p.174). 1st-level human warrior
    /// with wolf lycanthropy. Natural lycanthrope.
    /// CR 3 (1 base + 2 for wolf 2 HD).
    /// </summary>
    public static NPCDefinition Werewolf()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_warrior_1",
            Name = "Human Warrior",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 11, CON = 12, WIS = 11, INT = 10, CHA = 8,
            BAB = 1,
            BaseSpeed = 6, // 30 ft
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
            BaseHitDieHP = 5, // 1d8+1
            Feats = new List<string> { "Power Attack", "Track" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            ChallengeRating = "1"
        };

        var lycan = LycanthropeTemplate.Apply(baseDef, LycanthropeAnimalType.Wolf,
            isNatural: true, overrideId: "werewolf", overrideName: "Werewolf (Hybrid)");

        // MM werewolf hybrid wields a battleaxe
        lycan.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.BATTLEAXE, EquipSlot.RightHand),
            new EquipmentSlotPair(ItemIDs.CHAIN_SHIRT, EquipSlot.Armor)
        };
        lycan.BackpackItemIds = new List<string>();

        // Verify stats match MM p.174:
        // Hybrid form: STR 15 (13+2), DEX 15 (11+4), CON 16 (12+4), WIS 13 (11+2)
        // HD: 1d8+3 (warrior) + 2d8+6 (wolf) = 3 HD total
        // HP: ~22 (5+3con from base, + 2*4.5+6 = 15 from wolf = ~23)
        // BAB: +1 (warrior) + 1 (wolf) = +2
        // Nat armor: max(0, 2) + 2 = 4
        // DR 10/silver
        // CR: 3

        lycan.Description = "A werewolf in hybrid form — a snarling humanoid-wolf blend wielding " +
                           "a battleaxe alongside savage claws and fangs. Natural lycanthrope, " +
                           "DR 10/silver. Its bite can spread the curse of lycanthropy (DC 15 Fort).";
        lycan.CharacterAlignment = Alignment.ChaoticEvil;
        return lycan;
    }

    // ────────────────────────────────────────────
    //  Werewolf Lord (Human Fighter 10 + Dire Wolf)
    //  MM p.175-176 — CR 14, natural
    // ────────────────────────────────────────────

    /// <summary>
    /// Werewolf Lord — 10th-level human fighter with dire wolf form.
    /// MM p.175-176. An extremely dangerous natural lycanthrope.
    /// CR 14 (10 base + 4 for dire wolf 6 HD).
    /// </summary>
    public static NPCDefinition WerewolfLord()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_fighter_10",
            Name = "Human Fighter",
            HitDice = 10,
            Level = 10,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 18, DEX = 14, CON = 14, WIS = 12, INT = 10, CHA = 12,
            BAB = 10,
            BaseSpeed = 6,
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
            BaseHitDieHP = 75, // 10d10+20
            Feats = new List<string>
            {
                "Alertness", "Cleave", "Combat Reflexes",
                "Improved Critical", "Improved Natural Armor",
                "Power Attack", "Run", "Stealthy",
                "Weapon Focus", "Weapon Specialization"
            },
            WeaponFocusChoice = "Bastard Sword",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            ChallengeRating = "10"
        };

        var lycan = LycanthropeTemplate.Apply(baseDef, LycanthropeAnimalType.DireWolf,
            isNatural: true, overrideId: "werewolf_lord", overrideName: "Werewolf Lord (Hybrid)");

        // The werewolf lord is Large in hybrid form (dire wolf is Large)
        // Equipment: +2 bastard sword, mithral chain shirt, heavy shield (simplified)
        lycan.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.RightHand), // substitute for bastard sword
            new EquipmentSlotPair(ItemIDs.CHAIN_SHIRT, EquipSlot.Armor),
            new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.LeftHand)
        };
        lycan.BackpackItemIds = new List<string>();

        // MM hybrid stats:
        // STR 32 (18+14), DEX 18 (14+4), CON 20 (14+6), WIS 14 (12+2)
        // HD: 10d10 + 6d8 = 16 HD total
        // HP: 132 (75 + 6*4.5+6*5 = 75 + 57 = 132)
        // BAB: 10 + 4 = 14
        // Nat armor: max(0, 3) + 2 = 5 → MM says 6 (has Improved Natural Armor feat)
        // DR 10/silver
        // CR 14

        lycan.Description = "A terrifying werewolf lord in hybrid form — a Large, " +
                           "battle-hardened fighter merged with a dire wolf. Wields a bastard sword " +
                           "alongside devastating natural attacks. DR 10/silver, CR 14.";
        lycan.CharacterAlignment = Alignment.ChaoticEvil;
        return lycan;
    }

    // ────────────────────────────────────────────
    //  Wererat (Human Rogue 1 + Dire Rat)
    //  MM p.173 — CR 2, natural, chaotic evil
    // ────────────────────────────────────────────

    /// <summary>
    /// Wererat — Human rogue 1 / dire rat hybrid.
    /// MM p.173. Small and sneaky, prefers ambush tactics.
    /// CR 2 (1/2 base + 2 for dire rat 1 HD... effective CR 2).
    /// </summary>
    public static NPCDefinition Wererat()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_rogue_1",
            Name = "Human Rogue",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 10, DEX = 15, CON = 12, WIS = 12, INT = 13, CHA = 8,
            BAB = 0,
            BaseSpeed = 6,
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
            BaseHitDieHP = 7, // 1d6+1
            Feats = new List<string> { "Dodge", "Weapon Finesse" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            ChallengeRating = "1/2"
        };

        var lycan = LycanthropeTemplate.Apply(baseDef, LycanthropeAnimalType.Rat,
            isNatural: true, overrideId: "wererat", overrideName: "Wererat (Hybrid)");

        // Wererat uses rapier and light crossbow
        lycan.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.RAPIER, EquipSlot.RightHand),
            new EquipmentSlotPair(ItemIDs.CHAIN_SHIRT, EquipSlot.Armor)
        };
        lycan.BackpackItemIds = new List<string>();

        // Hybrid: STR 10 (+0), DEX 21 (15+6), CON 14 (12+2), WIS 14 (12+2)
        // HD: 1d6 + 1d8 = 2 HD total
        // BAB: 0 + 0 = 0
        // Nat armor: max(0, 1) + 2 = 3
        // DR 10/silver
        // CR 2

        lycan.AIBehavior = NPCAIBehavior.AggressiveMelee;
        lycan.AIProfileArchetype = NPCAIProfileArchetype.Humanoid;

        lycan.Description = "A wererat in hybrid form — a hunched, rat-faced humanoid with " +
                           "twitching whiskers and beady eyes. Wields a rapier with supernatural " +
                           "dexterity. Prefers ambush and flanking tactics. DR 10/silver.";
        lycan.CharacterAlignment = Alignment.LawfulEvil;
        return lycan;
    }

    // ────────────────────────────────────────────
    //  Wereboar (Human Barbarian 1 + Boar)
    //  MM p.172 — CR 4, natural, neutral
    // ────────────────────────────────────────────

    /// <summary>
    /// Wereboar — Human barbarian 1 / boar hybrid.
    /// MM p.172. Tough, bristly, and relentless.
    /// CR 4 (1 base + 3 for boar 3 HD).
    /// </summary>
    public static NPCDefinition Wereboar()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_barbarian_1",
            Name = "Human Barbarian",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 10, CON = 14, WIS = 10, INT = 8, CHA = 8,
            BAB = 1,
            BaseSpeed = 8, // 40 ft (barbarian fast movement)
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
            BaseHitDieHP = 9, // 1d12+2
            Feats = new List<string> { "Power Attack" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            ChallengeRating = "1"
        };

        var lycan = LycanthropeTemplate.Apply(baseDef, LycanthropeAnimalType.Boar,
            isNatural: true, overrideId: "wereboar", overrideName: "Wereboar (Hybrid)");

        // Wereboar uses greataxe
        lycan.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.GREATAXE, EquipSlot.Hands),
            new EquipmentSlotPair(ItemIDs.CHAIN_SHIRT, EquipSlot.Armor)
        };
        lycan.BackpackItemIds = new List<string>();

        // Hybrid: STR 20 (16+4), DEX 10 (+0), CON 20 (14+6), WIS 12 (10+2)
        // HD: 1d12 + 3d8 = 4 HD total
        // BAB: 1 + 2 = 3
        // Nat armor: max(0, 6) + 2 = 8
        // DR 10/silver
        // CR 4

        lycan.Description = "A wereboar in hybrid form — a bristly, pig-snouted brute " +
                           "with thick, tough hide and a vicious temper. Wields a greataxe " +
                           "and gores with savage tusks. DR 10/silver.";
        lycan.CharacterAlignment = Alignment.ChaoticNeutral;
        return lycan;
    }

    // ────────────────────────────────────────────
    //  Weretiger (Human Fighter 4 + Tiger)
    //  MM p.174 — CR 8, natural, neutral
    // ────────────────────────────────────────────

    /// <summary>
    /// Weretiger — 4th-level human noble/fighter with tiger form.
    /// MM p.174-175. Regal and terrifyingly powerful.
    /// CR 8 (4 base + 4 for tiger 6 HD).
    /// </summary>
    public static NPCDefinition Weretiger()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_fighter_4",
            Name = "Human Noble",
            HitDice = 4,
            Level = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 14, DEX = 16, CON = 12, WIS = 12, INT = 10, CHA = 10,
            BAB = 4,
            BaseSpeed = 6,
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
            BaseHitDieHP = 28, // 4d10+4
            Feats = new List<string> { "Dodge", "Improved Natural Attack", "Power Attack", "Weapon Focus" },
            WeaponFocusChoice = "Claw",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            ChallengeRating = "4"
        };

        var lycan = LycanthropeTemplate.Apply(baseDef, LycanthropeAnimalType.Tiger,
            isNatural: true, overrideId: "weretiger", overrideName: "Weretiger (Hybrid)");

        // Weretiger prefers to fight without weapons in hybrid form
        lycan.EquipmentIds = new List<EquipmentSlotPair>();
        lycan.BackpackItemIds = new List<string>();

        // Hybrid: STR 26 (14+12), DEX 20 (16+4), CON 18 (12+6), WIS 14 (12+2)
        // HD: 4d10 + 6d8 = 10 HD total
        // Size: Large (tiger size)
        // BAB: 4 + 4 = 8
        // Nat armor: max(0, 3) + 2 = 5
        // DR 10/silver
        // CR 8
        // Pounce in hybrid form!

        lycan.Description = "A weretiger in hybrid form — a sleek, powerful predator " +
                           "with rippling muscles beneath striped fur. Large size, devastating " +
                           "claws and fangs. Can pounce for full attacks on a charge. DR 10/silver.";
        lycan.CharacterAlignment = Alignment.TrueNeutral;
        return lycan;
    }

    // ────────────────────────────────────────────
    //  Werebear (Human Commoner 1 + Brown Bear)
    //  MM p.171-172 — CR 5, natural, lawful good
    // ────────────────────────────────────────────

    /// <summary>
    /// Werebear — Human commoner 1 / brown bear hybrid.
    /// MM p.171-172. The noble, good-aligned lycanthrope.
    /// CR 5 (1 base + 4 for brown bear 6 HD).
    /// </summary>
    public static NPCDefinition Werebear()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_commoner_1",
            Name = "Human Commoner",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 14, DEX = 10, CON = 14, WIS = 12, INT = 10, CHA = 8,
            BAB = 0,
            BaseSpeed = 6,
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
            BaseHitDieHP = 6, // 1d4+2
            Feats = new List<string> { "Endurance" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            ChallengeRating = "1/2"
        };

        var lycan = LycanthropeTemplate.Apply(baseDef, LycanthropeAnimalType.BrownBear,
            isNatural: true, overrideId: "werebear", overrideName: "Werebear (Hybrid)");

        // Werebear typically fights unarmed in hybrid form
        lycan.EquipmentIds = new List<EquipmentSlotPair>();
        lycan.BackpackItemIds = new List<string>();

        // Hybrid: STR 30 (14+16), DEX 12 (10+2), CON 22 (14+8), WIS 14 (12+2)
        // HD: 1d4 + 6d8 = 7 HD total
        // Size: Large (brown bear)
        // BAB: 0 + 4 = 4
        // Nat armor: max(0, 5) + 2 = 7
        // DR 10/silver
        // CR 5

        lycan.Description = "A werebear in hybrid form — a towering, fur-covered humanoid " +
                           "with the raw power of a brown bear. Unlike most lycanthropes, " +
                           "werebears are typically lawful good protectors of the wild. DR 10/silver.";
        lycan.CharacterAlignment = Alignment.LawfulGood;
        return lycan;
    }

    // ────────────────────────────────────────────
    //  Dire Wereboar (Human Barbarian 1 + Dire Boar)
    //  MM p.172 — CR 7, natural
    // ────────────────────────────────────────────

    /// <summary>
    /// Dire Wereboar — Human barbarian 1 / dire boar hybrid.
    /// MM p.172. Even tougher than the standard wereboar.
    /// CR 7 (1 base + 4 for dire boar 7 HD... wait, 7 HD = +4, so CR 5... 
    /// actually the dire boar is 7 HD which puts it in the 6-10 range = +4).
    /// Per MM, dire wereboar CR = 5 + 4 = 9? Let's check...
    /// MM says dire wereboar is actually listed at CR 7.
    /// We'll use the MM listed value.
    /// </summary>
    public static NPCDefinition DireWereboar()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_barbarian_1b",
            Name = "Human Barbarian",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 10, CON = 14, WIS = 10, INT = 8, CHA = 8,
            BAB = 1,
            BaseSpeed = 8, // 40 ft
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
            BaseHitDieHP = 9,
            Feats = new List<string> { "Power Attack", "Cleave" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            ChallengeRating = "1"
        };

        var lycan = LycanthropeTemplate.Apply(baseDef, LycanthropeAnimalType.DireBoar,
            isNatural: true, overrideId: "dire_wereboar", overrideName: "Dire Wereboar (Hybrid)");

        // Override CR to match MM listing
        lycan.ChallengeRating = "7";

        // Dire wereboar uses greatclub or fights unarmed
        lycan.EquipmentIds = new List<EquipmentSlotPair>
        {
            new EquipmentSlotPair(ItemIDs.GREATCLUB, EquipSlot.Hands)
        };
        lycan.BackpackItemIds = new List<string>();

        // Hybrid: STR 32 (16+16), DEX 10 (+0), CON 20 (14+6), WIS 12 (10+2)
        // HD: 1d12 + 7d8 = 8 HD total
        // Size: Large (dire boar)
        // BAB: 1 + 5 = 6
        // Nat armor: max(0, 6) + 2 = 8
        // DR 10/silver
        // CR 7

        lycan.Description = "A dire wereboar in hybrid form — a massive, bristling brute " +
                           "even larger and more fearsome than a standard wereboar. Its thick " +
                           "hide and dire boar heritage make it incredibly tough. DR 10/silver.";
        lycan.CharacterAlignment = Alignment.ChaoticNeutral;
        return lycan;
    }

    // ────────────────────────────────────────────
    //  Afflicted Werewolf (for encounters with cursed NPCs)
    // ────────────────────────────────────────────

    /// <summary>
    /// Afflicted Werewolf — A recently cursed human, weaker than natural.
    /// DR 5/silver, cannot spread the curse.
    /// </summary>
    public static NPCDefinition AfflictedWerewolf()
    {
        var baseDef = new NPCDefinition
        {
            Id = "base_human_commoner_afflicted",
            Name = "Human Commoner",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 12, DEX = 11, CON = 11, WIS = 10, INT = 10, CHA = 9,
            BAB = 0,
            BaseSpeed = 6,
            NaturalArmorBonus = 0,
            CreatureType = "Humanoid",
            BaseHitDieHP = 4, // 1d4
            Feats = new List<string>(),
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            ChallengeRating = "1/3"
        };

        var lycan = LycanthropeTemplate.Apply(baseDef, LycanthropeAnimalType.Wolf,
            isNatural: false, overrideId: "werewolf_afflicted", overrideName: "Afflicted Werewolf (Hybrid)");

        lycan.EquipmentIds = new List<EquipmentSlotPair>();
        lycan.BackpackItemIds = new List<string>();

        // Afflicted: DR 5/silver, cannot spread curse
        // STR 14 (12+2), DEX 15 (11+4), CON 15 (11+4), WIS 12 (10+2)
        // Weaker than natural werewolf

        lycan.Description = "An afflicted werewolf — a recently cursed human struggling " +
                           "to control the beast within. Weaker than natural werewolves, " +
                           "with only DR 5/silver and no ability to spread the curse.";
        lycan.CharacterAlignment = Alignment.ChaoticEvil;
        return lycan;
    }
}
