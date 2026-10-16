using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The AI's metamagic dimension (batch 11 step 28, ai-metamagic-choice; AI-001): which metamagic a caster's spells could
/// carry and what each is worth. Tactics only; the rules are MetamagicSystem's and the cast core's, which every candidate
/// passes before it is scored (AIService.ScoreCastableSpells: GameManager.CheckCasterLegalityForAI and the target or aim
/// legality, the same CheckLegality the cast runs), so PCs and NPCs stay bound by one rule set (docs/systems/PC_NPC_PARITY.md).
/// <list type="bullet">
/// <item>A spontaneous caster (a sorcerer, a bard, a monster casting as a sorcerer) applies its metamagic when it casts
/// (PHB p.88): <see cref="SpontaneousCandidates"/> gives one candidate per known spell and known metamagic feat that the
/// rules let it cast now (MetamagicSystem.ValidateMetamagicApplication, SpellcastingComponent.CanCastWithMetamagic), each
/// a clone of the spell carrying the feat (SpellData.AiChosenMetamagic, read by SpellCastRequest.AiMetamagicFor).</item>
/// <item>A prepared caster's metamagic was chosen when it prepared: its prepared metamagic slots are listed as their own
/// spells (SpellcastingComponent.GetCastablePreparedSpells, batch 11 step 21) and valued by <see cref="Value"/> too.</item>
/// <item><see cref="Value"/> replaces the old AISpellcastingStrategist.GetMetamagicScore: Empower and Maximize by the
/// expected damage or healing they add (SpellDiceRules.ExpectedTotal) on every creature the effect lands on; Extend by the
/// rounds of a buff, debuff or control effect it adds within a fight's horizon; Heighten by the save DC it adds and a globe
/// it gets through (PHB p.95, p.236); Quicken by the action it leaves (PHB p.98). A spontaneous choice pays for the
/// higher spell per day it spends and, for a 1-action spell, the full-round action (PHB p.88), which costs the caster its
/// move. Silent, Still, Enlarge and Widen add nothing of their own: they are chosen when the plain spell cannot be cast or
/// reach (Silence, a grapple, range) or when the wider area's own aim scores more (the area scoring sees it).</item>
/// <item>One feat per spontaneous candidate (no combinations), and at most <see cref="MaxSpontaneousCandidates"/> per
/// decision; the cast walk still tries at most AiSpellCandidates.MaxTries casts, so the AI's turn stays short.</item>
/// </list>
/// </summary>
public static class AiMetamagicPlanner
{
    /// <summary>At most this many spontaneous metamagic candidates are scored in one decision (bounds the AI's turn time).</summary>
    public const int MaxSpontaneousCandidates = 24;

    /// <summary>Score per point of expected damage or healing added, per creature the effect lands on.</summary>
    public const float EffectPointValue = 0.35f;

    /// <summary>Score when the metamagic's expected damage drops the target and the plain spell's does not (the strategist's finishing blow).</summary>
    public const float FinishingBlowValue = 15f;

    /// <summary>Score per extra round of a buff, debuff or control effect within <see cref="FightHorizonRounds"/>, per creature.</summary>
    public const float ExtendRoundValue = 0.5f;

    /// <summary>The rounds a fight is expected to last: an extended duration counts only up to here.</summary>
    public const int FightHorizonRounds = 10;

    /// <summary>Score per point of save DC Heighten adds, per enemy the spell lands on.</summary>
    public const float HeightenDcValue = 1.5f;

    /// <summary>Score when Heighten gets the spell through a globe that stops the plain spell on the target (PHB p.236).</summary>
    public const float GlobePenetrationValue = 20f;

    /// <summary>Score of the action a quickened spell leaves (the old strategist's QUICKEN_BONUS).</summary>
    public const float QuickenValue = 8f;

    /// <summary>Cost per spell level a spontaneous metamagic cast adds to the spell per day it spends.</summary>
    public const float SlotLevelCost = 2f;

    /// <summary>Cost of a spontaneous metamagic cast's full-round action (PHB p.88) for a spell that takes less: the move it gives up.</summary>
    public const float FullRoundCost = 3f;

    // ── Candidates ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The AI's candidate clone of <paramref name="spell"/> cast with <paramref name="metamagic"/>: a clone (never the
    /// database template) carrying the choice in SpellData.AiChosenMetamagic and MetamagicDataRef (the record the
    /// strategist's readers take, as on a prepared metamagic slot's spell), with the adjusted level in
    /// EffectiveSpellLevel. Its metamagic's changes are not applied: the cast's request applies them to its own clone.
    /// </summary>
    public static SpellData CandidateFor(SpellData spell, MetamagicData metamagic)
    {
        if (spell == null || metamagic == null || !metamagic.HasAnyMetamagic)
            return spell;
        SpellData candidate = spell.Clone();
        MetamagicData chosen = metamagic.Clone();
        candidate.AiChosenMetamagic = chosen;
        candidate.MetamagicDataRef = chosen;
        candidate.BaseSpellLevel = spell.SpellLevel;
        candidate.EffectiveSpellLevel = chosen.GetEffectiveSpellLevel(spell.SpellLevel);
        candidate.AppliedMetamagics = new List<MetamagicFeatId>(chosen.AppliedMetamagic);
        return candidate;
    }

    /// <summary>
    /// The spontaneous metamagic candidates of <paramref name="caster"/> (see the class summary): for each spell it knows
    /// and each metamagic feat it has (SpellcastingComponent.GetKnownMetamagicFeats), every option of that feat
    /// (Heighten to each higher level it has a spell per day of) that the rules allow
    /// (MetamagicSystem.ValidateMetamagicApplication: Quicken never on a spontaneous cast, PHB p.98; a feat that cannot
    /// modify the spell; the adjusted level at most 9th) and that the caster can pay for
    /// (SpellcastingComponent.CanCastWithMetamagic: a spell per day of the adjusted level), and that could matter now
    /// (<see cref="CouldMatter"/>). Empty for a prepared caster (its metamagic is prepared) or one with no feat.
    /// </summary>
    public static List<SpellData> SpontaneousCandidates(CharacterController caster, IList<CharacterController> all)
    {
        var result = new List<SpellData>();
        SpellcastingComponent sc = caster != null ? caster.Spellcasting : null;
        if (sc == null || caster.Stats == null || !sc.IsSpontaneousCaster || sc.SpontaneousData == null)
            return result;
        List<MetamagicFeatId> feats = sc.GetKnownMetamagicFeats();
        if (feats == null || feats.Count == 0)
            return result;

        // Known spells from the highest level down, so the cap keeps the strongest spells' options (the review of step 28:
        // a long list of cantrips and 1st-level spells must not crowd out an Empowered Fireball).
        SpontaneousCastingData data = sc.SpontaneousData;
        for (int knownLevel = data.SpellsKnownByLevel.Length - 1; knownLevel >= 0; knownLevel--)
        {
            List<string> known = data.GetKnownSpellsAtLevel(knownLevel);
            if (known == null)
                continue;
            foreach (string id in known)
            {
                SpellData spell = SpellDatabase.GetSpell(id);
                if (spell == null)
                    continue;
                CastSource source = SpellCastRequest.AiSourceFor(caster, spell);
                foreach (MetamagicFeatId feat in feats)
                {
                    foreach (MetamagicData option in OptionsFor(feat, spell, sc))
                    {
                        if (MetamagicSystem.ValidateMetamagicApplication(spell, option, caster.Stats, source) != null)
                            continue;
                        if (!sc.CanCastWithMetamagic(spell, option))
                            continue;
                        if (!CouldMatter(feat, caster, spell, option, all))
                            continue;
                        result.Add(CandidateFor(spell, option));
                        if (result.Count >= MaxSpontaneousCandidates)
                            return result;
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// The metamagic options of one feat, the feat alone except for Heighten, which gives at most two: the highest level
    /// with a spell per day left (the best save DC), and, with a globe on the field, the lowest level with a spell per day
    /// left that gets through every globe (PHB p.236, p.95). The levels between add only a smaller DC for the same costs.
    /// </summary>
    private static IEnumerable<MetamagicData> OptionsFor(MetamagicFeatId feat, SpellData spell, SpellcastingComponent sc)
    {
        if (feat == MetamagicFeatId.HeightenSpell)
        {
            int top = Mathf.Min(MetamagicSystem.MaxSpellLevel, sc.SpontaneousData.GetHighestCastableLevel());
            int highest = -1;
            for (int level = top; level > spell.SpellLevel; level--)
            {
                if (sc.SpontaneousData.GetSlotsRemaining(level) > 0)
                {
                    highest = level;
                    break;
                }
            }
            if (highest < 0)
                yield break;
            int throughGlobes = -1;
            int globeBlocks = HighestGlobeBlockedLevel();
            if (globeBlocks >= spell.SpellLevel)
            {
                for (int level = Mathf.Max(spell.SpellLevel + 1, globeBlocks + 1); level < highest; level++)
                {
                    if (sc.SpontaneousData.GetSlotsRemaining(level) > 0)
                    {
                        throughGlobes = level;
                        break;
                    }
                }
            }
            if (throughGlobes > 0)
                yield return Heighten(throughGlobes);
            yield return Heighten(highest);
            yield break;
        }
        var single = new MetamagicData();
        single.Toggle(feat);
        yield return single;
    }

    /// <summary>
    /// Whether <paramref name="feat"/> could change anything on this cast now, so the candidate is worth scoring: Empower
    /// and Maximize on a spell with dice; Extend on a buff, debuff or control spell whose plain duration ends within the
    /// fight's horizon; Heighten on a spell that allows a save against foes, or with a globe on the field; Enlarge when a
    /// creature the spell is for stands beyond its plain range and within the doubled one; Widen on an area spell; Silent
    /// when a Silence stops the plain spell (PHB p.279, p.100); Still when the caster grapples or has arcane spell failure
    /// and the spell has a somatic component (PHB p.156, p.101). A tactical filter; the rules are tested after it.
    /// </summary>
    public static bool CouldMatter(MetamagicFeatId feat, CharacterController caster, SpellData spell, MetamagicData option, IList<CharacterController> all)
    {
        if (caster == null || spell == null)
            return false;
        int casterLevel = SpellDiceRules.CasterLevelFor(caster.Stats, spell);
        switch (feat)
        {
            case MetamagicFeatId.EmpowerSpell:
            case MetamagicFeatId.MaximizeSpell:
                return PlainEffectDice(caster, spell, casterLevel).HasDice;
            case MetamagicFeatId.ExtendSpell:
            {
                if (spell.EffectType != SpellEffectType.Buff && spell.EffectType != SpellEffectType.Debuff
                    && spell.EffectType != SpellEffectType.Control)
                    return false;
                int rounds = SpellDurationRules.Rounds(spell, casterLevel);
                return rounds > 0 && rounds < FightHorizonRounds;
            }
            case MetamagicFeatId.HeightenSpell:
                return (spell.AllowsSavingThrow && AISpellcastingStrategist.IsEnemyTargetedSpell(spell)) || AnyGlobeOnField();
            case MetamagicFeatId.EnlargeSpell:
                return HasCreatureOnlyEnlargedReaches(caster, spell, option, all);
            case MetamagicFeatId.WidenSpell:
                return AreaAimSearch.IsAreaCastForAI(spell);
            case MetamagicFeatId.SilentSpell:
                return spell.HasVerbalComponent
                       && CastComponentRules.SilenceBlocks(caster, spell, null, SpellCastRequest.AiSourceFor(caster, spell));
            case MetamagicFeatId.StillSpell:
                return spell.HasSomaticComponent && (caster.IsGrappling() || caster.Stats.ArcaneSpellFailure > 0);
            default:
                return false;
        }
    }

    // ── Value ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// What the metamagic <paramref name="spell"/> carries (SpellCastRequest.AiMetamagicFor: a prepared slot's or the AI's
    /// spontaneous choice) is worth on this cast, 0 without metamagic (see the class summary). <paramref name="target"/>
    /// is the creature the AI is planning against (the scoring's primary target); <paramref name="placement"/> the area's
    /// aim for an area spell (its creatures count), else one creature. Positive: the metamagic version outranks the plain
    /// one, whose own score is the same before this term.
    /// </summary>
    public static float Value(CharacterController caster, SpellData spell, CharacterController target,
        IList<CharacterController> all, AreaPlacement placement)
    {
        MetamagicData metamagic = SpellCastRequest.AiMetamagicFor(spell);
        if (caster == null || caster.Stats == null || spell == null || metamagic == null || !metamagic.HasAnyMetamagic)
            return 0f;

        CastSource source = SpellCastRequest.AiSourceFor(caster, spell);
        int casterLevel = SpellDiceRules.CasterLevelFor(caster.Stats, spell);
        float value = 0f;

        if (metamagic.Has(MetamagicFeatId.EmpowerSpell) || metamagic.Has(MetamagicFeatId.MaximizeSpell))
            value += DiceValue(caster, spell, metamagic, casterLevel, target, placement);

        if (metamagic.Has(MetamagicFeatId.ExtendSpell) && SpellDurationRules.IsExtendable(spell)
            && (spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Debuff || spell.EffectType == SpellEffectType.Control))
        {
            int rounds = SpellDurationRules.Rounds(spell, casterLevel);
            if (rounds > 0)
            {
                int gained = Mathf.Min(rounds * 2, FightHorizonRounds) - Mathf.Min(rounds, FightHorizonRounds);
                value += ExtendRoundValue * gained * Mathf.Max(1, Reach(spell, placement));
            }
        }

        if (metamagic.Has(MetamagicFeatId.HeightenSpell) && metamagic.HeightenToLevel > spell.SpellLevel)
        {
            int dcGain = metamagic.HeightenToLevel - spell.SpellLevel;
            if (spell.AllowsSavingThrow && AISpellcastingStrategist.IsEnemyTargetedSpell(spell))
                value += HeightenDcValue * dcGain * Mathf.Max(1, Reach(spell, placement));
            if (placement == null && target != null && TeamUtility.IsEnemy(caster, target)
                && LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(PlainOf(spell), target)
                && !LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(spell, target))
                value += GlobePenetrationValue;
        }

        if (MetamagicSystem.IsQuickened(metamagic, source))
            value += QuickenValue;

        // What a spontaneous choice spends (PHB p.88): a higher spell per day, and the full-round action for a spell
        // that takes less. A prepared metamagic slot was paid for when it was prepared.
        if (source != null && source.IsSpontaneous)
        {
            value -= SlotLevelCost * metamagic.GetFeatOnlyLevelAdjustment(spell.SpellLevel);
            if (CastActionRules.CastingAction(spell, metamagic, source) == CastActionCost.FullRound
                && CastActionRules.CastingAction(spell, null, source) != CastActionCost.FullRound)
                value -= FullRoundCost;
        }
        return value;
    }

    /// <summary>
    /// The expected damage (or healing, for a Healing spell) of one cast of <paramref name="spell"/> by
    /// <paramref name="caster"/> on one creature with <paramref name="metamagic"/> (null: plain): SpellDiceRules.ExpectedTotal
    /// of the spell's dice at the cast's caster level, Magic Missile's dice once per missile (SpellCaster.MissileCountFor).
    /// </summary>
    public static float ExpectedEffect(CharacterController caster, SpellData spell, MetamagicData metamagic)
    {
        if (caster == null || spell == null)
            return 0f;
        int casterLevel = SpellDiceRules.CasterLevelFor(caster.Stats, spell);
        SpellDice dice = PlainEffectDice(caster, spell, casterLevel);
        float perRoll = SpellDiceRules.ExpectedTotal(dice, metamagic);
        return IsMissileSpell(spell) ? perRoll * SpellCaster.MissileCountFor(caster, caster.Stats, spell) : perRoll;
    }

    /// <summary>"Empowered Fireball", or the spell's name without metamagic (log lines).</summary>
    public static string Describe(SpellData spell)
    {
        if (spell == null)
            return "none";
        MetamagicData metamagic = SpellCastRequest.AiMetamagicFor(spell);
        return metamagic != null && metamagic.HasAnyMetamagic ? metamagic.GetDisplayName() + " " + spell.Name : spell.Name;
    }

    private static float DiceValue(CharacterController caster, SpellData spell, MetamagicData metamagic, int casterLevel,
        CharacterController target, AreaPlacement placement)
    {
        if (!PlainEffectDice(caster, spell, casterLevel).HasDice)
            return 0f;
        float plain = ExpectedEffect(caster, spell, null);
        float boosted = ExpectedEffect(caster, spell, metamagic);
        float delta = boosted - plain;
        if (delta <= 0f)
            return 0f;

        if (spell.EffectType == SpellEffectType.Healing)
        {
            // Healing past the recipient's missing hit points is worth nothing.
            CharacterController recipient = target != null && (target == caster || TeamUtility.IsAlly(caster, target)) ? target : caster;
            int missing = recipient != null && recipient.Stats != null ? Mathf.Max(0, recipient.Stats.TotalMaxHP - recipient.Stats.CurrentHP) : 0;
            float gained = Mathf.Min(boosted, missing) - Mathf.Min(plain, missing);
            return EffectPointValue * Mathf.Max(0f, gained) * Mathf.Max(1, Reach(spell, placement));
        }

        float value = EffectPointValue * delta * Reach(spell, placement);
        // The strategist's finishing blow, for the metamagic alone: the boosted average drops the target, the plain one does not.
        if (placement == null && target != null && target.Stats != null && TeamUtility.IsEnemy(caster, target)
            && target.Stats.CurrentHP > 0 && plain < target.Stats.CurrentHP && boosted >= target.Stats.CurrentHP)
            value += FinishingBlowValue;
        return value;
    }

    /// <summary>
    /// The creatures the effect lands on, for the per-creature terms: an area's foes less the allies it harms (a harmful
    /// spell), or the allies it helps (a beneficial one); one for a single target.
    /// </summary>
    private static int Reach(SpellData spell, AreaPlacement placement)
    {
        if (placement == null)
            return 1;
        AreaAimPolarity polarity = AreaAimSearch.PolarityOf(spell);
        if (polarity == AreaAimPolarity.Beneficial)
            return placement.AlliesBenefited;
        if (polarity == AreaAimPolarity.Mixed)
            return placement.AlliesBenefited + placement.EnemiesHit;
        return placement.EnemiesHit - placement.AlliesHarmed;
    }

    /// <summary>
    /// The spell's own dice at <paramref name="casterLevel"/>: a custom resolver's damage dice where the spell's data dice
    /// are a placeholder (CustomSpellDice.TryDamageEstimate: Fireball's 1d6 per level), else SpellDiceRules.Effect.
    /// </summary>
    public static SpellDice PlainEffectDice(CharacterController caster, SpellData spell, int casterLevel)
    {
        if (spell != null && spell.EffectType == SpellEffectType.Damage && CustomSpellDice.TryDamageEstimate(spell, casterLevel, out SpellDice custom))
            return custom;
        return SpellDiceRules.Effect(spell, casterLevel);
    }

    private static bool IsMissileSpell(SpellData spell)
        => spell != null && spell.AutoHit && spell.MissileCount > 0;

    /// <summary>The spell without the candidate's metamagic (a clone: Clone drops the choice and the record).</summary>
    private static SpellData PlainOf(SpellData spell) => spell.Clone();

    private static bool AnyGlobeOnField() => HighestGlobeBlockedLevel() >= 0;

    /// <summary>The highest spell level a globe on the field blocks (LesserGlobeOfInvulnerabilityAreaEffect.MaxBlockedSpellLevel), -1 with none.</summary>
    private static int HighestGlobeBlockedLevel()
    {
        if (!AreaEffectManager.HasInstance)
            return -1;
        List<LesserGlobeOfInvulnerabilityAreaEffect> globes =
            AreaEffectManager.Instance.GetEffectsOfType<LesserGlobeOfInvulnerabilityAreaEffect>();
        int highest = -1;
        if (globes != null)
            foreach (LesserGlobeOfInvulnerabilityAreaEffect g in globes)
                if (g != null && g.MaxBlockedSpellLevel > highest)
                    highest = g.MaxBlockedSpellLevel;
        return highest;
    }

    private static MetamagicData Heighten(int level)
    {
        var heighten = new MetamagicData { HeightenToLevel = level };
        heighten.Toggle(MetamagicFeatId.HeightenSpell);
        return heighten;
    }

    /// <summary>
    /// The range in squares of <paramref name="spell"/> as the AI's cast would carry it: for an Enlarged candidate (or an
    /// Enlarged prepared slot) the range of a clone with the metamagic applied (SpellCaster.ApplyMetamagicToSpellData), the
    /// range the cast core measures (PHB p.94: doubled; only a custom range is doubled today, SPL-010), else
    /// CasterLevelRules.RangeSquares. The AI's target prefilters and approach sizing read this, so a candidate chosen for
    /// its enlarged reach is not dropped by the plain spell's range (batch 11 step 28 review).
    /// </summary>
    public static int RangeSquaresFor(CharacterController caster, SpellData spell)
    {
        MetamagicData metamagic = SpellCastRequest.AiMetamagicFor(spell);
        if (spell == null || metamagic == null || !metamagic.Has(MetamagicFeatId.EnlargeSpell) || spell.RangeSquares <= 0)
            return CasterLevelRules.RangeSquares(caster, spell);
        SpellData enlarged = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(enlarged, metamagic);
        return CasterLevelRules.RangeSquares(caster, enlarged);
    }

    private static bool HasCreatureOnlyEnlargedReaches(CharacterController caster, SpellData spell, MetamagicData option, IList<CharacterController> all)
    {
        if (all == null || spell.RangeSquares <= 0)
            return false;
        SpellData enlarged = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(enlarged, option);
        int plainRange = CasterLevelRules.RangeSquares(caster, spell);
        int enlargedRange = CasterLevelRules.RangeSquares(caster, enlarged);
        if (enlargedRange <= plainRange)
            return false;
        bool forAllies = AISpellcastingStrategist.IsAllyTargetedSpell(spell);
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (c == null || c == caster || c.Stats == null || c.Stats.IsDead)
                continue;
            if (forAllies ? !TeamUtility.IsAlly(caster, c) : !TeamUtility.IsEnemy(caster, c))
                continue;
            int distance = CasterLevelRules.DistanceToTarget(caster, c);
            if (distance > plainRange && distance <= enlargedRange)
                return true;
        }
        return false;
    }
}
