using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// A Mirror Image caster's figments (PHB p.254; decision D7 of docs/designs/cast_core_batch11.md, batch 11 step 26): how
/// many images remain and which creatures have learned which figure is real. The images are unreal (PHB p.173): they
/// occupy no square, block nothing, threaten nothing and move with the caster, so they are a count on the caster, not
/// tokens on the grid. Lives on <see cref="CharacterController.MirrorImageFigures"/>; cleared by
/// <see cref="CharacterController.ResetForNewCreature"/> and GameManager.ClearMirrorImageForCaster (CRE-046).
/// </summary>
public sealed class MirrorImageFigures
{
    /// <summary>The images still standing (at most <see cref="MirrorImageRules.MaxImages"/>).</summary>
    public int Images;

    /// <summary>The count the spell created.</summary>
    public int Created;

    /// <summary>The creatures that know which figure is real (decision D7 (6)); cleared when the caster moves.</summary>
    public readonly HashSet<CharacterController> KnowsRealFigure = new HashSet<CharacterController>();
}

/// <summary>What an attack or spell against a Mirror Image caster struck (decision D7).</summary>
public enum MirrorImageFigurePick
{
    /// <summary>No pick: the caster has no image, or the spell's exemptions apply (see <see cref="MirrorImageRules.WhyNoPick"/>).</summary>
    NoPick,
    /// <summary>The random pick chose the real caster: the attack or spell resolves normally.</summary>
    Caster,
    /// <summary>The random pick chose an image.</summary>
    Image
}

/// <summary>
/// Mirror Image by RAW (PHB p.254; decision D7, batch 11 step 26), one actor-agnostic rule for PC and NPC casters and
/// attackers alike:
/// <list type="bullet">
/// <item>1d4 images plus one per three caster levels, at most eight (<see cref="RollImageCount"/>; Empower and Maximize
/// change the roll, PHB p.93, p.97).</item>
/// <item>Each attack (every attack of a full attack, each attack of opportunity, natural and off-hand attacks, each touch
/// or ray attack) and each targeted spell or missile against the caster picks a figure uniformly at random among the
/// remaining images and the caster, so the caster is struck with chance 1/(N+1) (<see cref="PickFigure"/>).</item>
/// <item>A weapon or touch attack on an image rolls against 10 + the caster's size modifier + its Dexterity modifier
/// (<see cref="ImageArmorClass"/>; the spell's own line beats touch AC and the general figment AC, so deflection, dodge
/// and insight do not count); a hit destroys the image and deals no damage, a miss changes nothing. A spell that needs
/// no attack roll is spent on the image and destroys it (delegated RAW reading 2026-10-14, reading (a)).</item>
/// <item>Area spells neither destroy nor reveal images (PHB p.254: figments seem to react normally); the caster is
/// affected where it stands.</item>
/// <item>No pick (<see cref="WhyNoPick"/>) when the caster is invisible, the attacker cannot see (blinded, sightless, or
/// unable to see the caster, such as in a fog), the attacker has True Seeing (PHB p.296), the attacker and the caster
/// are in one grapple (touch tells them apart; delegated RAW reading 2026-10-14), or the attacker already knows the real
/// figure. An ally's spell that is not hostile reaches the caster directly (<see cref="SpellReachesDirectly"/>). Blindsight, blindsense and tremorsense do not defeat the spell by themselves: a creature that can
/// see is fooled, one that has no sight is not (delegated RAW reading 2026-10-14, reading (c)).</item>
/// <item>A creature learns which figure is real when it sees an attack or a hostile spell strike a figure that does not
/// vanish; every such knowledge ends when the caster moves at least one square of its own movement (delegated RAW
/// reading 2026-10-14, reading (b)).</item>
/// </list>
/// The end-of-turn swap that the clone-token model offered only to controllable casters (SPL-117) is gone.
/// </summary>
public static class MirrorImageRules
{
    /// <summary>PHB p.254: "maximum eight images total".</summary>
    public const int MaxImages = 8;

    /// <summary>DiceService context of the 1d(N+1) figure pick; a 1 is the real caster.</summary>
    public const string FigureRollContext = "Mirror Image figure";

    /// <summary>DiceService context of the image count's d4.</summary>
    public const string CountRollContext = "Mirror Image count";

    /// <summary>Creature tag: the creature has no sight at all (grimlock, MM p.140; destrachan, MM p.49). Oozes are blind by type (MM p.313).</summary>
    public const string SightlessTag = "Sightless";

    // ── The count ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The dice of the image count: 1d4 + 1 per three caster levels (PHB p.254).</summary>
    public static SpellDice ImageCountDice(int casterLevel) => SpellDice.Of(1, 4, Mathf.Max(0, casterLevel) / 3);

    /// <summary>The count from a rolled total, capped at eight (PHB p.254).</summary>
    public static int CapImageCount(int rolled) => Mathf.Clamp(rolled, 0, MaxImages);

    /// <summary>Rolls the images for <paramref name="spell"/> cast at <paramref name="casterLevel"/>, with the cast's Empower or Maximize (stamped on the spell).</summary>
    public static int RollImageCount(SpellData spell, int casterLevel, out int rolled)
    {
        rolled = SpellDiceRules.RollFor(spell, ImageCountDice(casterLevel), CountRollContext);
        return CapImageCount(rolled);
    }

    // ── State ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The images <paramref name="caster"/> still has (0 when none).</summary>
    public static int ImagesOf(CharacterController caster)
        => caster != null && caster.MirrorImageFigures != null ? Mathf.Max(0, caster.MirrorImageFigures.Images) : 0;

    public static bool HasImages(CharacterController caster) => ImagesOf(caster) > 0;

    /// <summary>Gives <paramref name="caster"/> <paramref name="images"/> new images, replacing any it had (a recast).</summary>
    public static MirrorImageFigures Grant(CharacterController caster, int images)
    {
        if (caster == null)
            return null;
        var figures = new MirrorImageFigures { Images = CapImageCount(images), Created = CapImageCount(images) };
        caster.MirrorImageFigures = figures;
        return figures;
    }

    /// <summary>True when <paramref name="observer"/> knows which of <paramref name="caster"/>'s figures is real.</summary>
    public static bool KnowsRealFigure(CharacterController observer, CharacterController caster)
        => observer != null && caster?.MirrorImageFigures != null && caster.MirrorImageFigures.KnowsRealFigure.Contains(observer);

    // ── Who is fooled ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// True when <paramref name="creature"/> has no sight at all: blinded (DMG p.300), an ooze (MM p.313: blind), or a
    /// creature tagged <see cref="SightlessTag"/>. Blindsight alone does not count (decision D7 (7)).
    /// </summary>
    public static bool IsSightless(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return false;
        if (creature.HasCondition(CombatConditionType.Blinded))
            return true;
        if (creature.Stats.HasCreatureTag(SightlessTag))
            return true;
        string type = creature.Stats.CreatureType;
        return !string.IsNullOrEmpty(type) && type.IndexOf("Ooze", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>True when <paramref name="creature"/> sees through illusions (True Seeing, PHB p.296).</summary>
    public static bool HasTrueSeeing(CharacterController creature)
    {
        StatusEffectManager effects = creature != null ? creature.StatusEffectManager : null;
        return effects != null && effects.HasEffect(SpellNames.TRUE_SEEING);
    }

    /// <summary>
    /// Null when an attack or spell by <paramref name="attacker"/> against <paramref name="caster"/> picks a figure at
    /// random; otherwise why it goes straight to the caster: no images, the caster itself, an invisible caster, an
    /// attacker that cannot see (blinded, sightless, or unable to see the caster), True Seeing, an attacker in one grapple
    /// with the caster, or an attacker that already knows the real figure (PHB p.254; decision D7 (5) to (7)). Whether a
    /// spell is hostile at all is <see cref="SpellReachesDirectly"/>.
    /// </summary>
    public static string WhyNoPick(CharacterController attacker, CharacterController caster)
    {
        if (!HasImages(caster))
            return "no images";
        if (attacker == null || attacker == caster)
            return "the caster itself";
        if (caster.HasActiveInvisibilityEffect)
            return "the caster is invisible";
        if (AreInOneGrapple(attacker, caster))
            return "the attacker is grappling the caster";
        if (IsSightless(attacker))
            return "the attacker cannot see";
        if (!attacker.CanVisuallyPerceive(caster))
            return "the attacker cannot see the figures";
        if (HasTrueSeeing(attacker))
            return "true seeing";
        if (KnowsRealFigure(attacker, caster))
            return "the attacker knows the real figure";
        return null;
    }

    /// <summary>
    /// True when <paramref name="a"/> and <paramref name="b"/> are the two creatures of one grapple, either holding the
    /// other (an Improved Grab hold included). Observers cannot tell the figures apart "by vision or hearing" (PHB p.254);
    /// a creature that holds the caster, or is held by it, feels which figure is real, so it is not fooled while the
    /// grapple lasts (delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1).
    /// </summary>
    public static bool AreInOneGrapple(CharacterController a, CharacterController b)
        => a != null && b != null && a.IsInAnyGrappleLink(out CharacterController other) && other == b;

    /// <summary>
    /// True when <paramref name="spell"/>, cast by <paramref name="spellCaster"/> at <paramref name="target"/>, reaches it
    /// with no figure pick: a spell of the target's own side (the target itself or its ally) that is not hostile to it.
    /// The spell's "enemies attempting to attack you or cast spells at you" (PHB p.254) is read by the act, not the team:
    /// an ally's Damage, Debuff, Control or Wall spell (a confused or charmed ally's Magic Missile, say), and Healing on an
    /// undead target, are hostile acts and pick a figure like a foe's spell or any weapon attack (delegated RAW reading
    /// 2026-10-14, reading (d), RULES_COVERAGE 4.1); a foe's spell always picks.
    /// </summary>
    public static bool SpellReachesDirectly(CharacterController spellCaster, CharacterController target, SpellData spell)
    {
        if (spellCaster == null || target == null || spell == null)
            return true;
        bool ownSide = spellCaster == target || TeamUtility.IsAlly(spellCaster, target);
        if (!ownSide)
            return false;
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
            case SpellEffectType.Debuff:
            case SpellEffectType.Control:
            case SpellEffectType.Wall:
                return false;
            case SpellEffectType.Healing:
                return SpellCaster.IsLivingCreature(target.Stats);
            default:
                return true;
        }
    }

    /// <summary>
    /// The spells whose effect makes its own attack rolls after the cast (Spiritual Weapon, PHB p.283; Produce Flame,
    /// PHB p.265): the cast itself picks no figure; each of the effect's attacks does, in its handler
    /// (<see cref="TryStrikeImageWithEffectAttack"/>), and only a hit on the real caster reveals it.
    /// </summary>
    public static bool MakesOwnAttackRolls(SpellData spell)
        => spell != null && (string.Equals(spell.SpellId, SpellNames.SPIRITUAL_WEAPON, StringComparison.Ordinal)
                             || string.Equals(spell.SpellId, SpellNames.PRODUCE_FLAME, StringComparison.Ordinal));

    /// <summary>
    /// The random pick among <paramref name="images"/> images and the caster: 1d(N+1), where a 1 is the caster (chance
    /// 1/(N+1)) and any other result an image (PHB p.254).
    /// </summary>
    public static MirrorImageFigurePick PickFigure(int images)
    {
        if (images <= 0)
            return MirrorImageFigurePick.NoPick;
        int roll = DiceService.Roll(1, images + 1, FigureRollContext);
        return FigureFromRoll(roll);
    }

    /// <summary>The figure a pick roll names: 1 is the caster, any higher result an image.</summary>
    public static MirrorImageFigurePick FigureFromRoll(int roll) => roll <= 1 ? MirrorImageFigurePick.Caster : MirrorImageFigurePick.Image;

    /// <summary>
    /// An image's AC: 10 + the caster's size modifier + its Dexterity modifier (PHB p.254). Not touch AC: deflection,
    /// dodge and insight bonuses do not count.
    /// </summary>
    public static int ImageArmorClass(CharacterStats casterStats)
        => casterStats == null ? 10 : 10 + casterStats.SizeModifier + casterStats.DEXMod;

    // ── Weapon attacks ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The figure pick for one weapon attack (CharacterController.PerformSingleAttackWithCritCore, so every attack of a
    /// full attack, each attack of opportunity, natural, off-hand and grapple attacks): any creature but the caster that
    /// attacks it is fooled (PHB p.254: indistinguishable "by vision or hearing"). True when the attack struck an image:
    /// <paramref name="result"/> then holds the roll against <see cref="ImageArmorClass"/>, <c>Hit</c> false (the caster
    /// is not hit, so no on-hit effect reaches it), <see cref="CombatResult.MirrorImageFigure"/> set and, on a hit,
    /// <see cref="CombatResult.MirrorImageFigureHit"/> with the image destroyed and no damage dealt.
    /// </summary>
    public static bool TryStrikeImageWithAttack(CharacterController attacker, CharacterController caster, int totalAttackModifier, CombatResult result)
    {
        if (result == null || attacker == null || attacker.Stats == null || WhyNoPick(attacker, caster) != null)
            return false;

        int images = ImagesOf(caster);
        MirrorImageFigurePick pick = PickFigure(images);
        if (pick != MirrorImageFigurePick.Image)
            return false;

        int imageAc = ImageArmorClass(caster.Stats);
        var (hit, roll, total) = attacker.Stats.RollToHitWithMod(totalAttackModifier, imageAc);
        result.DieRoll = roll;
        result.TotalRoll = total;
        result.TargetAC = imageAc;
        result.Hit = false;
        result.NaturalTwenty = roll == 20;
        result.NaturalOne = roll == 1;
        result.IsCritThreat = false;
        result.CritConfirmed = false;
        result.MissedDueToConcealment = false;
        result.Damage = 0;
        result.BaseDamageRoll = 0;
        result.RawTotalDamage = 0;
        result.FinalDamageDealt = 0;
        result.TargetKilled = false;
        result.MirrorImageFigure = true;
        result.MirrorImageFigureHit = hit;

        string attackerName = attacker.Stats.CharacterName;
        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "the caster";
        string note = hit
            ? $"Mirror Image: {attackerName}'s attack ({total} vs image AC {imageAc}) strikes an image of {casterName}, which vanishes."
            : $"Mirror Image: {attackerName}'s attack ({total} vs image AC {imageAc}) misses an image of {casterName}.";
        result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote) ? note : $"{result.SpecialAttackNote} {note}";

        if (hit)
            DestroyImage(caster, $"struck by {attackerName}");
        return true;
    }

    /// <summary>
    /// The figure pick for one attack of a spell effect that rolls its own attacks (<see cref="MakesOwnAttackRolls"/>):
    /// <paramref name="director"/> is the creature whose senses aim it (the cleric directing its spiritual weapon, the
    /// caster hurling its flame). When the pick names an image, the attack is rolled against
    /// <see cref="ImageArmorClass"/> with <paramref name="attackModifier"/> (d20 context <paramref name="rollContext"/>,
    /// the effect's own): a hit destroys the image, a miss changes nothing, and either way the attack deals nothing to
    /// the caster. True when the attack went to an image; <paramref name="note"/> then describes it (decision D7 (2)).
    /// </summary>
    public static bool TryStrikeImageWithEffectAttack(CharacterController director, CharacterController caster, int attackModifier,
        string rollContext, string attackName, out string note)
    {
        note = null;
        if (director == null || WhyNoPick(director, caster) != null)
            return false;
        if (PickFigure(ImagesOf(caster)) != MirrorImageFigurePick.Image)
            return false;

        int imageAc = ImageArmorClass(caster.Stats);
        int roll = DiceService.D20(rollContext);
        int total = roll + attackModifier;
        bool hit = CombatCalculationService.IsHit(roll, total, imageAc);
        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "the caster";
        string sign = attackModifier >= 0 ? "+" : "";
        note = hit
            ? $"{attackName} attacks an image of {casterName}: {roll}{sign}{attackModifier}={total} vs image AC {imageAc} - IMAGE STRUCK, it vanishes."
            : $"{attackName} attacks an image of {casterName}: {roll}{sign}{attackModifier}={total} vs image AC {imageAc} - misses the image.";
        if (hit)
            DestroyImage(caster, $"struck by {attackName}");
        return true;
    }

    // ── Images destroyed, the real figure revealed, the caster moving ───────────────────────────────────────────

    /// <summary>Destroys one of <paramref name="caster"/>'s images; with the last one the spell ends (PHB p.254).</summary>
    public static void DestroyImage(CharacterController caster, string reason)
    {
        MirrorImageFigures figures = caster != null ? caster.MirrorImageFigures : null;
        if (figures == null || figures.Images <= 0)
            return;

        figures.Images--;
        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "the caster";
        Log(CombatLogHelper.Color($"🪞 An image of {casterName} vanishes ({reason}); {figures.Images} remain.", "D5C8FF"));
        if (figures.Images <= 0)
            End(caster, "all images destroyed", removeStatusEffect: true, log: true);
    }

    /// <summary>
    /// A hostile attack or spell struck the real <paramref name="caster"/> and it did not vanish: every creature that sees
    /// the caster (and would be fooled by the figments) now knows which figure is real, <paramref name="striker"/>
    /// included (decision D7 (6), delegated RAW reading 2026-10-14). Knowing is a matter of what a creature saw, not of
    /// its side: the caster's allies learn too, which matters only if one of them turns on the caster (reading (d)).
    /// Returns how many learned.
    /// </summary>
    public static int RevealRealFigure(CharacterController caster, CharacterController striker)
    {
        MirrorImageFigures figures = caster != null ? caster.MirrorImageFigures : null;
        if (figures == null || figures.Images <= 0 || caster.HasActiveInvisibilityEffect)
            return 0;

        var observers = new List<CharacterController>();
        if (striker != null)
            observers.Add(striker);
        List<CharacterController> all = GameManager.Instance != null ? GameManager.Instance.GetAllCharactersForAI() : null;
        if (all != null)
            observers.AddRange(all);

        int learned = 0;
        for (int i = 0; i < observers.Count; i++)
        {
            CharacterController o = observers[i];
            if (o == null || o == caster || o.Stats == null || o.Stats.IsDead)
                continue;
            if (IsSightless(o) || !o.CanVisuallyPerceive(caster))
                continue;
            if (figures.KnowsRealFigure.Add(o))
                learned++;
        }

        if (learned > 0)
            Debug.Log($"[MirrorImage] {learned} creature(s) learned which figure of {caster.Stats?.CharacterName} is real.");
        return learned;
    }

    /// <summary>
    /// The caster moved: as it moves it merges with and splits off from its figments, so every creature that had learned
    /// the real figure is confounded again (PHB p.254). Only its own movement counts (a move, 5-foot step, withdraw or
    /// charge: <paramref name="ownMovement"/>), not being pushed or dragged (decision D7 (6)).
    /// </summary>
    public static bool ConfoundOnMove(CharacterController caster, bool ownMovement)
    {
        MirrorImageFigures figures = caster != null ? caster.MirrorImageFigures : null;
        if (!ownMovement || figures == null || figures.KnowsRealFigure.Count == 0)
            return false;
        figures.KnowsRealFigure.Clear();
        Debug.Log($"[MirrorImage] {caster.Stats?.CharacterName} moves among its images: no enemy knows the real figure any more.");
        return true;
    }

    /// <summary>Removes <paramref name="observer"/> from <paramref name="caster"/>'s knowers (a reused slot, CRE-046).</summary>
    public static void Forget(CharacterController caster, CharacterController observer)
    {
        if (caster?.MirrorImageFigures != null && observer != null)
            caster.MirrorImageFigures.KnowsRealFigure.Remove(observer);
    }

    /// <summary>Ends <paramref name="caster"/>'s images; with <paramref name="removeStatusEffect"/> also its tracked spell effect.</summary>
    public static void End(CharacterController caster, string reason, bool removeStatusEffect, bool log)
    {
        if (caster == null || caster.MirrorImageFigures == null)
            return;
        caster.MirrorImageFigures = null;

        if (removeStatusEffect)
        {
            caster.StatusEffectManager?.RemoveEffectsBySpellId(SpellNames.MIRROR_IMAGE);
            caster.Spellcasting?.ActiveBuffs?.Remove(SpellNames.MIRROR_IMAGE);
        }

        if (log)
        {
            string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            Log(CombatLogHelper.Expired("⏱", $"Mirror Image ends on {casterName} ({reason})."));
        }
    }

    private static void Log(string line)
    {
        CombatUI ui = GameManager.Instance != null ? GameManager.Instance.CombatUI : null;
        if (ui != null)
            ui.ShowCombatLog(line);
        else
            Debug.Log("[MirrorImage] " + line);
    }
}
