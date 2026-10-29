using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Mirror Image's GameManager side (PHB p.254; decision D7, batch 11 step 26): the cast handler that grants the images,
/// the cast core's figure pick for a spell, the caster's own movement confounding its foes, and the cleanup. The rule
/// itself is <see cref="MirrorImageRules"/>, which the weapon attack path calls too, so PC and NPC casters and attackers
/// follow one model. There are no image tokens and no end-of-turn swap (SPL-117 closed).
/// </summary>
public partial class GameManager
{
    /// <summary>The creatures with Mirror Image images, for the cleanup between encounters (each one's state is on it).</summary>
    private readonly HashSet<CharacterController> _mirrorImageCasters = new HashSet<CharacterController>();

    private bool TryResolveMirrorImageSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
        => ApplyMirrorImageSpell(caster, target, spell, result, 0);

    /// <summary>
    /// Mirror Image's effect: grants the images and tracks the effect. <paramref name="casterLevelOverride"/> (above 0)
    /// is a scroll's or wand's own caster level when no cast is open (the consumable's direct path,
    /// GameManager.TryApplySpellConsumableEffect; DMG p.238, p.245); otherwise the cast's caster level is used.
    /// </summary>
    private bool ApplyMirrorImageSpell(CharacterController caster, CharacterController target, SpellData spell, SpellResult result,
        int casterLevelOverride)
    {
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.MIRROR_IMAGE, StringComparison.Ordinal))
            return false;

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveMirrorImageSpellEffect));

        if (caster == null || caster.Stats == null)
            return true;

        if (result == null || !result.Success)
            return true;

        CharacterController recipient = caster;
        if (target != null && target != caster)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Mirror Image is a personal spell and affects only the caster."));
        }

        ClearMirrorImageForCaster(recipient, "recast", removeStatusEffect: true, log: false);

        StatusEffectManager statusMgr = recipient.StatusEffectManager;
        if (statusMgr == null)
            statusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(recipient.Stats);

        int casterLevel = casterLevelOverride > 0 ? casterLevelOverride : SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        ActiveSpellEffect effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);
        if (effect == null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{recipient.Stats.CharacterName} could not gain Mirror Image due to stacking rules."));
            return true;
        }

        // 1d4 + 1 per three caster levels, at most eight (PHB p.254); the cast's Empower or Maximize changes the roll
        // (PHB p.93, p.97; SpellDiceRules, batch 11 step 19).
        int images = MirrorImageRules.RollImageCount(spell, casterLevel, out int rolled);
        MirrorImageRules.Grant(recipient, images);
        _mirrorImageCasters.Add(recipient);

        SpellcastingComponent spellComp = recipient.Spellcasting;
        if (spellComp != null)
            spellComp.ActiveBuffs[SpellNames.MIRROR_IMAGE] = effect.RemainingRounds;

        string cap = rolled > images ? $", rolled {rolled}, at most {MirrorImageRules.MaxImages}" : "";
        CombatUI?.ShowCombatLog(CombatLogHelper.Color(
            $"🪞 {recipient.Stats.CharacterName} casts Mirror Image: {images} image(s) surround it (1d4 + {Mathf.Max(0, casterLevel) / 3}{cap}). Each attack or spell against it strikes a random figure.",
            "B7A8FF"));
        return true;
    }

    /// <summary>
    /// The figure pick for a single-target spell cast at a creature with Mirror Image images (PHB p.254; decision D7 (3);
    /// the cast core's MirrorImage stage). An ally's spell that is not hostile (<see cref="MirrorImageRules.SpellReachesDirectly"/>),
    /// casts the spell's exemptions cover (<see cref="MirrorImageRules.WhyNoPick"/>), and spells whose effect makes its
    /// own attacks (<see cref="MirrorImageRules.MakesOwnAttackRolls"/>: each of those attacks picks in its handler) reach
    /// the creature directly. A missile spell picks once per missile and fires the missiles that pick the creature
    /// (<see cref="CastContext.MissilesSpentOnImages"/>). A spell with an attack roll that picks an image rolls its touch
    /// attack against the image's AC: a hit destroys the image and spends the spell; a missed ray is spent, and a missed
    /// melee touch keeps its charge, a delivered one as a cast one (PHB p.141; batch 11 step 27, SPL-115). A targeted Dispel Magic that picks an image makes its dispel check against
    /// the Mirror Image alone (PHB p.223; delegated RAW reading 2026-10-14). Any other spell with no attack roll that picks
    /// an image is spent on it and destroys it (delegated RAW reading 2026-10-14, decision D7 (a)). True when the spell
    /// was spent on images (<paramref name="logLine"/> and <paramref name="traceDetail"/> say how).
    /// </summary>
    private bool TryResolveMirrorImageSpellPick(CastContext ctx, out string logLine, out string traceDetail)
    {
        logLine = null;
        traceDetail = null;
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;
        if (caster == null || target == null || target == caster || spell == null || !MirrorImageRules.HasImages(target))
            return false;
        if (MirrorImageRules.SpellReachesDirectly(caster, target, spell))
            return false;
        if (MirrorImageRules.MakesOwnAttackRolls(spell))
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.MirrorImage, "own-attacks");
            return false;
        }
        string why = MirrorImageRules.WhyNoPick(caster, target);
        if (why != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.MirrorImage, "no-pick");
            Debug.Log($"[MirrorImage] {spell.Name} reaches {target.Stats.CharacterName} directly: {why}.");
            return false;
        }

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : caster.name;
        string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;

        // Missiles: one pick per missile (decision D7 (3)).
        if (spell.AutoHit && spell.MissileCount > 0)
        {
            int missiles = SpellCaster.MissileCountFor(caster, caster.Stats, spell);
            int spent = 0;
            for (int i = 0; i < missiles; i++)
            {
                if (MirrorImageRules.HasImages(target) && MirrorImageRules.PickFigure(MirrorImageRules.ImagesOf(target)) == MirrorImageFigurePick.Image)
                {
                    spent++;
                    MirrorImageRules.DestroyImage(target, $"struck by {casterName}'s {spell.Name}");
                }
            }

            if (spent >= missiles)
            {
                traceDetail = "struck-image";
                logLine = CombatLogHelper.Color($"🪞 {casterName}'s {spell.Name} ({missiles} missile(s)) strikes only images of {targetName}.", "B7A8FF");
                return true;
            }

            ctx.MissilesSpentOnImages = spent;
            CastStageTrace.Mark(caster, spell, CastStageTrace.MirrorImage, spent > 0 ? $"missiles-on-images {spent}/{missiles}" : "caster-picked");
            if (spent > 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Color($"🪞 {spent} of {casterName}'s {missiles} missiles strike images of {targetName}; {missiles - spent} find the real one.", "B7A8FF"));
            return false;
        }

        if (MirrorImageRules.PickFigure(MirrorImageRules.ImagesOf(target)) != MirrorImageFigurePick.Image)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.MirrorImage, "caster-picked");
            return false;
        }

        bool needsAttackRoll = (spell.IsMeleeTouchSpell() || spell.IsRangedTouchSpell()) && !spell.AutoHit && !ctx.SkipFriendlyTouchAttackRoll;
        if (!needsAttackRoll && string.Equals(spell.SpellId, SpellNames.DISPEL_MAGIC, StringComparison.Ordinal))
        {
            // A targeted dispel aimed at a figment targets the Mirror Image spell itself (PHB p.223, p.254): one dispel
            // check against it alone, which ends every image on a success (delegated RAW reading 2026-10-14).
            CombatUI?.ShowCombatLog(CombatLogHelper.Color($"🪞 {casterName}'s {spell.Name} is aimed at an image of {targetName}: the dispel check is made against the Mirror Image alone.", "B7A8FF"));
            PerformMirrorImageDispel(caster, target, SpellDurationRules.CasterLevelFor(caster, spell));
            traceDetail = "dispel-at-image";
            logLine = CombatLogHelper.Color($"🪞 {casterName}'s {spell.Name} reached only the Mirror Image of {targetName}.", "B7A8FF");
            return true;
        }

        if (!needsAttackRoll)
        {
            MirrorImageRules.DestroyImage(target, $"struck by {casterName}'s {spell.Name}");
            traceDetail = "struck-image";
            logLine = CombatLogHelper.Color($"🪞 {casterName}'s {spell.Name} strikes an image of {targetName}; the image vanishes and the spell is spent.", "B7A8FF");
            return true;
        }

        bool isRanged = spell.IsRangedTouchSpell();
        int imageAc = MirrorImageRules.ImageArmorClass(target.Stats);
        SpellCaster.SpellTouchAttackTerms terms = SpellCaster.SpellTouchAttack(spell, caster, caster.Stats, target, target.Stats, isRanged, againstImage: true,
            stepBabAdjustment: ctx.Request.TouchStepBabAdjustment);
        if (terms.BeyondMaximumRange)
            return false; // SpellCaster.Cast fails the attack that cannot be made
        int modifier = terms.Total;
        int roll = DiceService.D20(SpellCaster.TouchAttackContext);
        int total = roll + modifier;
        bool hit = CombatCalculationService.IsHit(roll, total, imageAc);
        string touchKind = isRanged ? "ranged touch" : "touch";

        if (hit)
        {
            MirrorImageRules.DestroyImage(target, $"struck by {casterName}'s {spell.Name}");
            if (ctx.IsHeldChargeDelivery)
                ctx.SpellComp?.ClearHeldTouchCharge("touch struck a Mirror Image");
            traceDetail = "struck-image";
            logLine = CombatLogHelper.Color($"🪞 {casterName}'s {spell.Name}: {touchKind} attack {roll}{FormatModifier(modifier)} = {total} vs image AC {imageAc} - strikes an image of {targetName}, which vanishes; the spell is spent.", "B7A8FF");
            return true;
        }

        traceDetail = "missed-image";
        string charge = "";
        if (ctx.IsHeldChargeDelivery)
        {
            ctx.RetainedHeldChargeOnMiss = true;
            charge = $" {casterName} retains the {spell.Name} charge.";
        }
        else if (!isRanged && spell.CanHoldCharge())
        {
            // A melee touch that missed in its casting round touched nothing, so the spell is not discharged: its charge
            // is held (PHB p.141, p.176; delegated RAW reading 2026-10-14, batch 11 step 27, SPL-115). A ray is spent, and
            // so is a spell that touches several targets (Chill Touch; SpellData.CanHoldCharge).
            HoldChargeAfterMissedTouch(ctx);
            charge = $" {casterName} holds the {spell.Name} charge.";
        }
        logLine = CombatLogHelper.Color($"🪞 {casterName}'s {spell.Name}: {touchKind} attack {roll}{FormatModifier(modifier)} = {total} vs image AC {imageAc} - misses an image of {targetName}.{charge}", "B7A8FF");
        return true;
    }

    private static string FormatModifier(int modifier) => modifier >= 0 ? $" + {modifier}" : $" - {-modifier}";

    /// <summary>Ends <paramref name="caster"/>'s images (recast, death, reused slot); with <paramref name="removeStatusEffect"/> also its tracked effect.</summary>
    private void ClearMirrorImageForCaster(CharacterController caster, string reason, bool removeStatusEffect, bool log)
    {
        if (caster == null)
            return;
        _mirrorImageCasters.Remove(caster);
        MirrorImageRules.End(caster, reason, removeStatusEffect, log);
    }

    private void OnMirrorImageEffectExpired(CharacterController caster)
    {
        ClearMirrorImageForCaster(caster, "duration expired", removeStatusEffect: false, log: true);
    }

    /// <summary>
    /// Keeps the legacy ActiveBuffs mirror in step with the tracked effect and ends the images when the effect is gone
    /// (removed by anything but its expiry, which <see cref="OnMirrorImageEffectExpired"/> handles). Called by the duration
    /// tick for every creature with images (GameManager.TickCharacterSpellDurations), whether or not it holds the effect;
    /// a dispel ends them at once (DispelMagicService.HandleDispelSpecialCleanup).
    /// </summary>
    private void SyncMirrorImageDurationForCaster(CharacterController caster, StatusEffectManager statusMgr)
    {
        if (caster == null || caster.MirrorImageFigures == null)
            return;

        ActiveSpellEffect mirrorEffect = null;
        if (statusMgr != null && statusMgr.ActiveEffects != null)
        {
            for (int i = 0; i < statusMgr.ActiveEffects.Count; i++)
            {
                ActiveSpellEffect effect = statusMgr.ActiveEffects[i];
                if (effect?.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.MIRROR_IMAGE, StringComparison.Ordinal))
                {
                    mirrorEffect = effect;
                    break;
                }
            }
        }

        if (mirrorEffect == null)
        {
            caster.Spellcasting?.ActiveBuffs?.Remove(SpellNames.MIRROR_IMAGE);
            ClearMirrorImageForCaster(caster, "effect removed", removeStatusEffect: false, log: true);
            return;
        }

        SpellcastingComponent spellComp = caster.Spellcasting;
        if (spellComp != null)
            spellComp.ActiveBuffs[SpellNames.MIRROR_IMAGE] = mirrorEffect.RemainingRounds;
    }

    /// <summary>
    /// Every change of a creature's square lands here (MoveToCell, each MoveAlongPath segment, a grapple reposition).
    /// <paramref name="forced"/>: the creature was pushed or dragged by something else, not its own movement.
    /// </summary>
    public void NotifyCharacterMovement(CharacterController mover, Vector2Int previousPosition, Vector2Int currentPosition, string movementType = null, bool forced = false)
    {
        if (mover == null || previousPosition == currentPosition)
            return;

        // Inert test hook: every MoveToCell, MoveAlongPath (segment) and grapple reposition lands here.
        ScenarioHooks.Moved?.Invoke(mover, previousPosition, currentPosition, movementType);

        // An undead creature entering or leaving a Consecrate or Desecrate area gains or loses its modifiers (PHB p.212,
        // p.218; batch 11 step 23, SPL-042).
        UpdateHolyAreaEffectsForCharacter(mover);

        // An Improved Grab hold with one body part: a moving holder carries the held creature along while it can drag its
        // weight, else the hold ends (MM p.310, PHB p.162; CMB-163).
        string holdEnded = CharacterController.CheckImprovedGrabHoldReach(mover, previousPosition, currentPosition);
        if (!string.IsNullOrEmpty(holdEnded))
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", holdEnded));

        // A Mirror Image caster moving among its figments confounds the foes that had learned the real one (PHB p.254).
        if (MirrorImageRules.HasImages(mover))
            MirrorImageRules.ConfoundOnMove(mover, !forced && IsOwnMovement(mover, movementType));
    }

    /// <summary>
    /// True when <paramref name="mover"/>'s move is its own movement (a move, 5-foot step, withdraw or charge): a move on
    /// its own turn other than a grapple reposition, or any move outside combat turns. A creature pushed or dragged
    /// (bull rush, overrun, Gust of Wind, a grappler's drag: MoveToCell's forced, which NotifyCharacterMovement checks
    /// first, on any creature's turn, a readied bull rush on its own included) does not move by itself (decision D7 (6)).
    /// </summary>
    private bool IsOwnMovement(CharacterController mover, string movementType)
    {
        if (string.Equals(movementType, "grapple-reposition", StringComparison.Ordinal))
            return false;
        CharacterController acting = CurrentCharacter;
        return acting == null || acting == mover;
    }

    private void ClearAllMirrorImageEffects(string reason)
    {
        var casters = new List<CharacterController>(_mirrorImageCasters);
        if (PCs != null)
            casters.AddRange(PCs);
        if (NPCs != null)
            casters.AddRange(NPCs);
        for (int i = 0; i < casters.Count; i++)
        {
            CharacterController caster = casters[i];
            if (caster == null)
                continue;

            MirrorImageRules.End(caster, reason, removeStatusEffect: false, log: false);
        }

        _mirrorImageCasters.Clear();
    }
}
