using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// What a creature was before it took a wild shape, kept on <see cref="CharacterStats.ActiveWildShape"/> so that
/// <see cref="WildShapeRules.Revert"/> restores it exactly (CHR-020). The ability score and hit point changes are kept
/// as deltas, so spell bonuses that begin or end while the druid is in animal form stay correct.
/// </summary>
public sealed class WildShapeState
{
    public string FormId;
    public string FormName;
    public int FormHitDice;
    public int DurationRounds;

    public int StrengthDelta;
    public int DexterityDelta;
    public int ConstitutionDelta;
    /// <summary>The change in maximum (and current) hit points from the new Constitution modifier.</summary>
    public int HitPointDelta;
    /// <summary>The hit points the change into the form restored (as a night's rest).</summary>
    public int HitPointsHealed;
    /// <summary>
    /// The maximum (and current) hit points the melded gear took with it (an item's Constitution bonus ends while it is
    /// melded, PHB p.197), given back when the gear returns.
    /// </summary>
    public int MeldHitPointLoss;
    /// <summary>The form's racial bonus feats the druid did not have, gained for the form (PHB p.197) and removed after.</summary>
    public List<string> GainedFeats = new List<string>();

    public int NaturalArmorBonus;
    public List<NaturalAttackDefinition> NaturalAttacks;
    public NaturalAttackDefinition RakeAttack;
    public SizeCategory BaseSize;
    public int BaseSpeed;
    public bool IsTallCreature;
    public bool IsQuadruped;
    public bool IsExceptionallyStable;
    public bool HasTripAttack;
    public int TripAttackCheckBonus;
    public string TripTriggerAttackName;
    public bool HasImprovedGrab;
    public string ImprovedGrabTriggerAttackName;
    public SizeCategory? ImprovedGrabMaxTargetSize;
    public bool HasPounce;
    public bool HasRake;
    public string CreatureType;
    public RaceData Race;
    /// <summary>The worn and held items that melded into the form (PHB p.197), by the slot each came from.</summary>
    public List<KeyValuePair<EquipSlot, ItemData>> MeldedEquipment = new List<KeyValuePair<EquipSlot, ItemData>>();
}

/// <summary>What one wild shape or revert did (<see cref="WildShapeRules.Assume"/>, <see cref="WildShapeRules.Revert"/>).</summary>
public struct WildShapeResult
{
    public bool Performed;
    public string Refusal;
    public string Log;
}

/// <summary>
/// The one Wild Shape rule for PCs and NPCs (PHB p.37; CHR-020). The PC Wild Shape button (GameManager.OnWildShapeButtonPressed)
/// and the AI (GameManager.TryNPCWildShapeTurnForAI) both call <see cref="Assume"/> and <see cref="Revert"/>.
///
/// From 5th druid level the druid turns into an animal and back: 1/day at 5th, 2/day at 6th, 3/day at 7th (4 at 10th,
/// 5 at 14th, 6 at 18th); Small or Medium forms, Large from 8th, Tiny from 11th, Huge from 15th; the form's Hit Dice
/// may not exceed the druid level. Changing form either way is a standard action that does not provoke. The change
/// works like polymorph (PHB p.263, which works like alter self, PHB p.197): the druid takes the form's Strength,
/// Dexterity and Constitution (hit points follow the new Constitution modifier), size, natural armor, natural attacks,
/// land speed, body shape, racial bonus feats (alter self, PHB p.197: the viper's Weapon Finesse, the wolf's Track) and
/// its extraordinary special attacks (trip, improved grab, pounce, rake, the poison of its natural attacks), but not its
/// special qualities (scent, low-light vision) or its other feats; she keeps her Intelligence,
/// Wisdom, Charisma, class levels, base attack bonus, base saves, hit dice and spell slots. Her type becomes the form's
/// (animal), and her worn and held gear melds into the form and stops working (an item's Constitution bonus and its hit
/// points with it, until the gear returns). On changing into the form she regains hit points as for a night's rest (1 per
/// character level, PHB p.146); changing back heals nothing. A change of hit points either way moves her HP state
/// (disabled, dying, dead; PHB p.145) as damage or healing would. Slain, she reverts and stays dead. The effect lasts 1
/// hour per druid level; the post-combat rest ends it.
///
/// Delegated RAW readings 2026-10-14 (RULES_COVERAGE 4.1): the forms are the animals of the game's bestiary
/// (NPCDatabase entries of the animal type: the game keeps no record of where a druid has been, so every listed animal
/// counts as one she is familiar with), not swarms, templated or aliased entries; the Hit Dice limit is the druid level
/// alone (PHB p.37 replaces polymorph's caster-level limit); her own racial traits are extraordinary qualities not derived
/// from class levels and are lost in the form (PHB p.197), so the race is set aside while she is shaped; all worn and
/// held equipment melds (an animal cannot wear or hold it), slotless items (ioun stones) stay; the gear in her pack is
/// melded too, so she can neither use items nor carry weight; she cannot cast a spell with a verbal or somatic component
/// without Natural Spell (PHB p.37: she cannot speak; p.98); the healing is lethal damage only (nonlethal damage is not
/// lost hit points); a grappling druid cannot change shape (a supernatural standard action is not a grapple action,
/// PHB p.156, as for turning and lay on hands); she changes from her own form into an animal and back, never from one
/// animal into another; base hit points (the Hit Dice rolls) stay, as alter self says, while the Constitution part
/// follows the new score; the save DC of the form's poison or disease is figured for the druid, the creature that now
/// delivers it (MM p.313 and p.6: 10 + half its racial Hit Dice + its Constitution modifier), not copied from the form's
/// entry (<see cref="OnHitSaveDcModifier"/>); an Enlarge or Reduce Person size change stays on top of the form's size
/// (wild shape sets her natural size and is not itself a size-increasing effect).
/// </summary>
public static class WildShapeRules
{
    /// <summary>Druid class level (0 for a creature without druid levels).</summary>
    public static int DruidLevel(CharacterStats stats) => stats != null ? stats.GetClassLevel("Druid") : 0;

    /// <summary>True when the creature has wild shape at all (druid 5th level or higher), used or not.</summary>
    public static bool HasAbility(CharacterStats stats) => DruidLevel(stats) >= 5;

    /// <summary>Uses per day at the druid's level (PHB Table 3-8).</summary>
    public static int UsesPerDay(CharacterStats stats) => HasAbility(stats) ? WildShapeData.GetUsesPerDay(DruidLevel(stats)) : 0;

    /// <summary>Uses left today.</summary>
    public static int UsesLeft(CharacterStats stats) => stats == null ? 0 : Mathf.Max(0, UsesPerDay(stats) - stats.WildShapeUsesToday);

    /// <summary>True while the creature is in a wild shape form.</summary>
    public static bool IsWildShaped(CharacterStats stats) => stats != null && stats.ActiveWildShape != null;

    /// <summary>The duration in rounds: 1 hour per druid level (PHB p.37), 600 rounds an hour.</summary>
    public static int DurationRounds(CharacterStats stats) => WildShapeData.GetDurationRounds(DruidLevel(stats));

    /// <summary>The smallest size the druid may take at her level (Small; Tiny from 11th).</summary>
    public static SizeCategory SmallestSize(int druidLevel) => druidLevel >= 11 ? SizeCategory.Tiny : SizeCategory.Small;

    /// <summary>The largest size the druid may take at her level (Medium; Large from 8th; Huge from 15th).</summary>
    public static SizeCategory LargestSize(int druidLevel)
        => druidLevel >= 15 ? SizeCategory.Huge : druidLevel >= 8 ? SizeCategory.Large : SizeCategory.Medium;

    /// <summary>True for an entry of the animal type (case and spacing ignored).</summary>
    public static bool IsAnimalType(string creatureType)
        => !string.IsNullOrWhiteSpace(creatureType) && creatureType.Replace(" ", string.Empty).Trim().ToLowerInvariant() == "animal";

    /// <summary>
    /// Whether <paramref name="form"/> is a form a druid of <paramref name="druidLevel"/> may take: an animal of the
    /// bestiary (not a swarm, a templated entry or a summon alias), of an allowed size, with Hit Dice no higher than
    /// the druid level, not incorporeal (PHB p.37, p.263).
    /// </summary>
    public static bool IsLegalForm(int druidLevel, NPCDefinition form, out string reason)
    {
        reason = string.Empty;
        if (form == null)
        {
            reason = "No such form";
            return false;
        }
        if (druidLevel < 5)
        {
            reason = "Druid level 5 required";
            return false;
        }
        if (!IsAnimalType(form.CreatureType))
        {
            reason = $"{form.Name} is not an animal";
            return false;
        }
        if (form.IsSwarm || (form.SwarmTraits != null && form.SwarmTraits.IsSwarm) || form.IsIncorporeal)
        {
            reason = $"{form.Name} is not a single creature of flesh";
            return false;
        }
        if (IsTemplatedOrAlias(form))
        {
            reason = $"{form.Name} is not a plain animal";
            return false;
        }
        if (form.SizeCategory < SmallestSize(druidLevel) || form.SizeCategory > LargestSize(druidLevel))
        {
            reason = $"{form.Name} is {form.SizeCategory} (allowed {SmallestSize(druidLevel)} to {LargestSize(druidLevel)} at druid level {druidLevel})";
            return false;
        }
        int hd = form.ResolveTotalHitDice();
        if (hd > druidLevel)
        {
            reason = $"{form.Name} has {hd} HD (more than druid level {druidLevel})";
            return false;
        }
        if (form.NaturalAttacks == null || form.NaturalAttacks.Count == 0)
        {
            reason = $"{form.Name} has no natural attacks in its entry";
            return false;
        }
        return true;
    }

    private static bool IsTemplatedOrAlias(NPCDefinition form)
    {
        if (!string.IsNullOrEmpty(form.SourceTemplateId))
            return true;
        if (form.AppliedTemplateIds != null && form.AppliedTemplateIds.Count > 0)
            return true;
        if (form.BakedTemplateIds != null && form.BakedTemplateIds.Count > 0)
            return true;
        if (form.CreatureTags != null)
        {
            for (int i = 0; i < form.CreatureTags.Count; i++)
            {
                if (string.Equals(form.CreatureTags[i], "SummonAlias", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>The forms <paramref name="stats"/> may take now, by Hit Dice and then name (empty below 5th level).</summary>
    public static List<NPCDefinition> GetAvailableForms(CharacterStats stats)
    {
        var forms = new List<NPCDefinition>();
        int level = DruidLevel(stats);
        if (level < 5)
            return forms;
        NPCDatabase.Init();
        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (IsLegalForm(level, def, out _))
                forms.Add(def);
        }
        forms.Sort((a, b) =>
        {
            int byHd = a.ResolveTotalHitDice().CompareTo(b.ResolveTotalHitDice());
            return byHd != 0 ? byHd : string.CompareOrdinal(a.Name, b.Name);
        });
        return forms;
    }

    /// <summary>Whether <paramref name="actor"/> may take a wild shape now (ability, uses, own form, standard action, not grappling).</summary>
    public static bool CanAssume(CharacterController actor, out string reason)
    {
        reason = string.Empty;
        if (actor == null || actor.Stats == null)
        {
            reason = "No active character";
            return false;
        }
        if (!HasAbility(actor.Stats))
        {
            reason = "Druid level 5 required";
            return false;
        }
        if (IsWildShaped(actor.Stats))
        {
            reason = "Already in animal form";
            return false;
        }
        if (UsesLeft(actor.Stats) <= 0)
        {
            reason = "No uses left today";
            return false;
        }
        if (actor.Stats.IsDead)
        {
            reason = "Dead";
            return false;
        }
        if (actor.Actions == null || !actor.Actions.HasStandardAction)
        {
            reason = "Standard action used";
            return false;
        }
        if (actor.IsGrappling())
        {
            reason = "Grappling (not a grapple action, PHB p.156)";
            return false;
        }
        return true;
    }

    /// <summary>Whether <paramref name="actor"/> may change back now (in a form, standard action, not grappling).</summary>
    public static bool CanRevert(CharacterController actor, out string reason)
    {
        reason = string.Empty;
        if (actor == null || actor.Stats == null || !IsWildShaped(actor.Stats))
        {
            reason = "Not in animal form";
            return false;
        }
        if (actor.Actions == null || !actor.Actions.HasStandardAction)
        {
            reason = "Standard action used";
            return false;
        }
        if (actor.IsGrappling())
        {
            reason = "Grappling (not a grapple action, PHB p.156)";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Turns <paramref name="actor"/> into <paramref name="formId"/> (an NPCDatabase animal id): spends the standard
    /// action and a use, then applies the form. Refused, with nothing spent, when the form is not legal for the druid or
    /// does not fit where she stands.
    /// </summary>
    public static WildShapeResult Assume(CharacterController actor, string formId)
    {
        var result = new WildShapeResult();
        if (!CanAssume(actor, out string reason))
        {
            result.Refusal = reason;
            return result;
        }
        NPCDefinition form = NPCDatabase.Get(formId);
        if (!IsLegalForm(DruidLevel(actor.Stats), form, out reason))
        {
            result.Refusal = reason;
            return result;
        }
        if (!actor.CanTakeNaturalSize(form.SizeCategory))
        {
            result.Refusal = $"No room for a {form.SizeCategory} {form.Name} here";
            return result;
        }
        if (!actor.CommitStandardAction())
        {
            result.Refusal = "Standard action used";
            return result;
        }

        actor.Stats.WildShapeUsesToday++;
        WildShapeState state = ApplyForm(actor, form);
        result.Performed = true;
        result.Log = DescribeChange(actor, state, true);
        return result;
    }

    /// <summary>
    /// Changes <paramref name="actor"/> back to its own form as a standard action (PHB p.37). No healing (PHB p.263).
    /// </summary>
    public static WildShapeResult Revert(CharacterController actor)
    {
        var result = new WildShapeResult();
        if (!CanRevert(actor, out string reason))
        {
            result.Refusal = reason;
            return result;
        }
        if (!actor.CommitStandardAction())
        {
            result.Refusal = "Standard action used";
            return result;
        }
        WildShapeState state = actor.Stats.ActiveWildShape;
        RestoreOwnForm(actor, keepCurrentHitPoints: false);
        result.Performed = true;
        result.Log = DescribeChange(actor, state, false);
        return result;
    }

    /// <summary>
    /// Ends the wild shape with no action: the druid was slain (PHB p.263: she reverts and stays dead, so her current
    /// hit points do not change), or the form ends on its own (<paramref name="slain"/> false: the Constitution hit points
    /// go, as on a change back). Returns false when she was not shaped. The post-combat rest uses <see cref="RefreshForRest"/>.
    /// </summary>
    public static bool EndWithoutAction(CharacterController actor, bool slain)
    {
        if (actor == null || actor.Stats == null || !IsWildShaped(actor.Stats))
            return false;
        RestoreOwnForm(actor, keepCurrentHitPoints: slain);
        return true;
    }

    /// <summary>
    /// The full rest after combat (the CORE-015 stand-in for a night): it outlasts any wild shape (1 hour per level) and
    /// restores the day's uses.
    /// </summary>
    public static void RefreshForRest(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return;
        // The form ends without the Constitution hit point loss: the same rest restores every hit point right after
        // (GameManager.RestorePartyAfterCombat), so the change back cannot kill or knock out a resting druid.
        if (IsWildShaped(actor.Stats))
            RestoreOwnForm(actor, keepCurrentHitPoints: true);
        actor.Stats.WildShapeUsesToday = 0;
    }

    /// <summary>
    /// False while wild shaped without Natural Spell for a spell with a verbal or somatic component (PHB p.37: the
    /// druid cannot speak in animal form; PHB p.98, Natural Spell; PHB p.197: casting needs speech and hands). The
    /// components are those left after <paramref name="metamagic"/> (or the spell's own prepared metamagic): Silent Spell
    /// removes the verbal one and Still Spell the somatic one (PHB p.100-101), so a silent, stilled spell can be cast.
    /// </summary>
    public static bool CanCastInCurrentForm(CharacterStats stats, SpellData spell, MetamagicData metamagic = null)
    {
        if (!IsWildShaped(stats) || spell == null)
            return true;
        MetamagicData applied = metamagic ?? spell.MetamagicDataRef;
        bool verbal = spell.HasVerbalComponent && (applied == null || !applied.Has(MetamagicFeatId.SilentSpell));
        bool somatic = spell.HasSomaticComponent && (applied == null || !applied.Has(MetamagicFeatId.StillSpell));
        if (!verbal && !somatic)
            return true;
        return FeatManager.HasNaturalSpell(stats);
    }

    /// <summary>
    /// The change to the printed save DC of a poison or disease a natural attack delivers (<paramref name="printedDc"/>)
    /// when the attacker is in a wild shape: the DC is figured for the creature that delivers it, 10 + half its racial
    /// Hit Dice + its Constitution modifier (MM p.313, Poison; MM p.6, special ability DCs), so the druid's own racial Hit Dice and her
    /// current (the form's) Constitution count, not the animal's (delegated RAW reading 2026-10-14, CHR-020). 0 for any
    /// creature in its own form, whose entry's DC stands.
    /// </summary>
    public static int OnHitSaveDcModifier(CharacterStats attacker, int printedDc)
    {
        if (!IsWildShaped(attacker))
            return 0;
        int dc = 10 + Mathf.Max(0, attacker.RacialHitDice) / 2 + attacker.CONMod;
        return dc - printedDc;
    }

    /// <summary>False while wild shaped: the gear is melded into the form, so no item can be drawn or used (PHB p.197).</summary>
    public static bool CanManipulateItems(CharacterStats stats) => !IsWildShaped(stats);

    // ── Applying and restoring ──────────────────────────────────────

    private static WildShapeState ApplyForm(CharacterController actor, NPCDefinition form)
    {
        CharacterStats stats = actor.Stats;
        var state = new WildShapeState
        {
            FormId = form.Id,
            FormName = form.Name,
            FormHitDice = form.ResolveTotalHitDice(),
            DurationRounds = DurationRounds(stats),
            NaturalArmorBonus = stats.NaturalArmorBonus,
            NaturalAttacks = CloneAttacks(stats.NaturalAttacks),
            RakeAttack = stats.RakeAttack != null ? stats.RakeAttack.Clone() : null,
            BaseSize = stats.BaseSizeCategory,
            BaseSpeed = stats.BaseSpeed,
            IsTallCreature = stats.IsTallCreature,
            IsQuadruped = stats.IsQuadruped,
            IsExceptionallyStable = stats.IsExceptionallyStable,
            HasTripAttack = stats.HasTripAttack,
            TripAttackCheckBonus = stats.TripAttackCheckBonus,
            TripTriggerAttackName = stats.TripTriggerAttackName,
            HasImprovedGrab = stats.HasImprovedGrab,
            ImprovedGrabTriggerAttackName = stats.ImprovedGrabTriggerAttackName,
            ImprovedGrabMaxTargetSize = stats.ImprovedGrabMaxTargetSize,
            HasPounce = stats.HasPounce,
            HasRake = stats.HasRake,
            CreatureType = stats.CreatureType,
            Race = stats.Race
        };

        // Gear melds first, so the item bonuses to ability scores and AC end before the form's scores are measured. An
        // item's Constitution bonus ends with it, and its hit points go as when it is taken off (PHB p.10); they come
        // back with the gear.
        Inventory inventory = actor.GetInventoryData();
        int maxBeforeMeld = stats.TotalMaxHP;
        if (inventory != null)
            state.MeldedEquipment = inventory.MeldAllEquipment();
        state.MeldHitPointLoss = Mathf.Max(0, maxBeforeMeld - stats.TotalMaxHP);
        if (state.MeldHitPointLoss > 0)
            stats.CurrentHP -= state.MeldHitPointLoss;

        int conModBefore = CharacterStats.GetAbilityModifier(stats.EffectiveCONScore);

        // The form's physical scores replace the druid's own (racial adjustments, increases and inherent bonuses
        // included); spell bonuses stay on top (the field holds them, CharacterStats.SyncAbilityScoreWithBonuses).
        state.StrengthDelta = form.STR - stats.NaturalAbilityScore(AbilityType.STR);
        state.DexterityDelta = form.DEX - stats.NaturalAbilityScore(AbilityType.DEX);
        state.ConstitutionDelta = form.CON - stats.NaturalAbilityScore(AbilityType.CON);
        stats.STR += state.StrengthDelta;
        stats.DEX += state.DexterityDelta;
        stats.CON += state.ConstitutionDelta;

        // Physical qualities (PHB p.197): natural armor, natural weapons, mundane movement, body shape, racial bonus
        // feats; the extraordinary special attacks (PHB p.263). Special qualities (scent) and other feats are not gained.
        stats.NaturalArmorBonus = form.NaturalArmorBonus;
        stats.SetNaturalAttacks(form.NaturalAttacks);
        stats.SetRakeAttack(form.RakeAttack);
        stats.BaseSpeed = form.BaseSpeed;
        stats.IsTallCreature = form.IsTallCreature;
        stats.IsQuadruped = form.IsQuadruped;
        stats.IsExceptionallyStable = form.IsExceptionallyStable;
        stats.HasTripAttack = form.HasTripAttack;
        stats.TripAttackCheckBonus = form.TripAttackCheckBonus;
        stats.TripTriggerAttackName = form.TripTriggerAttackName;
        stats.HasImprovedGrab = form.HasImprovedGrab;
        stats.ImprovedGrabTriggerAttackName = form.ImprovedGrabTriggerAttackName;
        stats.ImprovedGrabMaxTargetSize = form.ImprovedGrabMaxTargetSize;
        stats.HasPounce = form.HasPounce;
        stats.HasRake = form.HasRake;
        if (form.BonusFeats != null)
        {
            for (int i = 0; i < form.BonusFeats.Count; i++)
            {
                string feat = form.BonusFeats[i];
                if (string.IsNullOrWhiteSpace(feat) || stats.Feats.Contains(feat))
                    continue;
                stats.Feats.Add(feat);
                state.GainedFeats.Add(feat);
            }
        }
        // Type and subtype become the form's (PHB p.263; p.37 keeps the type only for elemental forms).
        stats.CreatureType = form.CreatureType;
        stats.Race = null;
        stats.ActiveWildShape = state;
        actor.SetNaturalSize(form.SizeCategory);

        // Hit points follow the new Constitution modifier (1 per Hit Die per point of modifier), then the change heals
        // as a night's rest: 1 hit point per character level (PHB p.146, p.263).
        int conModAfter = CharacterStats.GetAbilityModifier(stats.EffectiveCONScore);
        state.HitPointDelta = (conModAfter - conModBefore) * stats.GetHitDice();
        if (state.HitPointDelta != 0)
        {
            stats.AdjustMaxHP(state.HitPointDelta);
            stats.CurrentHP += state.HitPointDelta;
        }
        if (stats.CurrentHP > stats.TotalMaxHP)
            stats.CurrentHP = stats.TotalMaxHP;
        int before = stats.CurrentHP;
        if (before < stats.TotalMaxHP)
            stats.CurrentHP = Mathf.Min(stats.TotalMaxHP, before + stats.GetHitDice());
        state.HitPointsHealed = stats.CurrentHP - before;

        inventory?.RecalculateStats();
        actor.RefreshAllTags();
        actor.SetDisplayedRace(form.Name);
        // The new total sets her HP state (a disabled druid healed above 0 is no longer disabled; PHB p.145).
        actor.SyncHPStateFromCurrentHP(emitLog: true);
        return state;
    }

    private static void RestoreOwnForm(CharacterController actor, bool keepCurrentHitPoints)
    {
        CharacterStats stats = actor.Stats;
        WildShapeState state = stats.ActiveWildShape;
        if (state == null)
            return;

        stats.ActiveWildShape = null;
        stats.STR -= state.StrengthDelta;
        stats.DEX -= state.DexterityDelta;
        stats.CON -= state.ConstitutionDelta;

        stats.NaturalArmorBonus = state.NaturalArmorBonus;
        stats.SetNaturalAttacks(state.NaturalAttacks);
        stats.SetRakeAttack(state.RakeAttack);
        stats.BaseSpeed = state.BaseSpeed;
        stats.IsTallCreature = state.IsTallCreature;
        stats.IsQuadruped = state.IsQuadruped;
        stats.IsExceptionallyStable = state.IsExceptionallyStable;
        stats.HasTripAttack = state.HasTripAttack;
        stats.TripAttackCheckBonus = state.TripAttackCheckBonus;
        stats.TripTriggerAttackName = state.TripTriggerAttackName;
        stats.HasImprovedGrab = state.HasImprovedGrab;
        stats.ImprovedGrabTriggerAttackName = state.ImprovedGrabTriggerAttackName;
        stats.ImprovedGrabMaxTargetSize = state.ImprovedGrabMaxTargetSize;
        stats.HasPounce = state.HasPounce;
        stats.HasRake = state.HasRake;
        if (state.GainedFeats != null)
        {
            for (int i = 0; i < state.GainedFeats.Count; i++)
                stats.Feats.Remove(state.GainedFeats[i]);
        }
        stats.CreatureType = state.CreatureType;
        stats.Race = state.Race;
        actor.SetNaturalSize(state.BaseSize);

        if (state.HitPointDelta != 0)
        {
            stats.AdjustMaxHP(-state.HitPointDelta);
            if (!keepCurrentHitPoints)
                stats.CurrentHP -= state.HitPointDelta;
        }

        // The gear comes back before the hit points are capped, so an item's Constitution hit points return with it.
        Inventory inventory = actor.GetInventoryData();
        inventory?.RestoreMeldedEquipment(state.MeldedEquipment);
        if (!keepCurrentHitPoints && state.MeldHitPointLoss > 0)
            stats.CurrentHP += state.MeldHitPointLoss;
        if (stats.CurrentHP > stats.TotalMaxHP)
            stats.CurrentHP = stats.TotalMaxHP;

        actor.RefreshAllTags();
        // A Disguise Self still running shows its race again; otherwise her own (CharacterController.ClearDisguiseSelfEffect).
        if (actor.ActiveDisguiseSelfEffect != null)
            actor.SetDisplayedRace(actor.ActiveDisguiseSelfEffect.DisguisedRace);
        else
            actor.ResetDisplayedRaceToActual();

        // Losing Constitution hit points can leave her disabled, dying or dead (PHB p.145): the HP state follows the
        // new total, and a Dead state runs OnDeath (CharacterController.OnHPStateChanged).
        if (!keepCurrentHitPoints)
            actor.SyncHPStateFromCurrentHP(emitLog: true);
    }

    private static List<NaturalAttackDefinition> CloneAttacks(List<NaturalAttackDefinition> attacks)
    {
        var list = new List<NaturalAttackDefinition>();
        if (attacks == null)
            return list;
        for (int i = 0; i < attacks.Count; i++)
        {
            if (attacks[i] != null)
                list.Add(attacks[i].Clone());
        }
        return list;
    }

    private static string DescribeChange(CharacterController actor, WildShapeState state, bool intoForm)
    {
        CharacterStats stats = actor.Stats;
        var sb = new StringBuilder();
        if (intoForm)
        {
            sb.Append($"{stats.CharacterName} takes the shape of a {state.FormName} (Wild Shape, standard action; {UsesLeft(stats)}/{UsesPerDay(stats)} uses left): ");
            sb.Append($"Str {stats.STR}, Dex {stats.DEX}, Con {stats.CON}, {stats.CurrentSizeCategory}, AC {stats.ArmorClass}, speed {stats.BaseSpeed * 5} ft");
            if (stats.NaturalAttacks != null && stats.NaturalAttacks.Count > 0)
            {
                var names = new List<string>();
                foreach (NaturalAttackDefinition a in stats.NaturalAttacks)
                    names.Add((a.Count > 1 ? a.Count + " " : string.Empty) + a.Name);
                sb.Append("; attacks: " + string.Join(", ", names));
            }
            if (state.HitPointDelta != 0)
                sb.Append($"; {CharacterStats.FormatMod(state.HitPointDelta)} max hp from Constitution");
            if (state.HitPointsHealed > 0)
                sb.Append($"; heals {state.HitPointsHealed} hp as from a night's rest");
            if (state.MeldedEquipment != null && state.MeldedEquipment.Count > 0)
                sb.Append($"; {state.MeldedEquipment.Count} worn or held item(s) meld into the form");
            sb.Append($" (HP {stats.CurrentHP}/{stats.TotalMaxHP}).");
        }
        else
        {
            sb.Append($"{stats.CharacterName} changes back from {state.FormName} form to its own (standard action): ");
            sb.Append($"Str {stats.STR}, Dex {stats.DEX}, Con {stats.CON}, AC {stats.ArmorClass} (HP {stats.CurrentHP}/{stats.TotalMaxHP}).");
        }
        return sb.ToString();
    }
}
