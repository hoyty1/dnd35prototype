using UnityEngine;

/// <summary>The smite a creature has declared or can declare (<see cref="SmiteRules"/>).</summary>
public enum SmiteSource
{
    None,
    /// <summary>Paladin Smite Evil (Su), PHB p.44: +Charisma bonus on the attack roll, +1 damage per paladin level.</summary>
    PaladinSmiteEvil,
    /// <summary>Celestial template Smite Evil (Su), MM p.31: extra damage equal to Hit Dice (maximum +20), no attack bonus.</summary>
    TemplateSmiteEvil,
    /// <summary>Fiendish template Smite Good (Su), MM p.107: the same against a good foe.</summary>
    TemplateSmiteGood
}

/// <summary>What a declared smite did to one melee attack (<see cref="SmiteRules.ConsumeForMeleeAttack"/>).</summary>
public struct SmiteApplication
{
    /// <summary>A declared smite was used up by this attack (hit or miss, a smite foe or not).</summary>
    public bool Used;
    public SmiteSource Source;
    /// <summary>The target is of the smitten alignment, so the bonuses apply.</summary>
    public bool TargetIsSmiteFoe;
    public int AttackBonus;
    public int DamageBonus;
    /// <summary>"Smite Evil" or "Smite Good" (combat log and damage-term label).</summary>
    public string Label;
}

/// <summary>
/// The one smite rule for PCs and NPCs (CHR-020, CMB-021, AI-056): the paladin's Smite Evil (PHB p.44) and the celestial
/// and fiendish templates' Smite Evil and Smite Good (MM p.31, p.107). A smite is declared before the attack roll (the
/// PC Smite button, the AI before its melee sequence or charge: <see cref="TryDeclare"/>), and the next melee weapon
/// attack the creature makes (a single attack, any attack of a full attack, a charge's attack) is the smiting attack:
/// CharacterController.PerformSingleAttackWithCritCore calls <see cref="ConsumeForMeleeAttack"/> before rolling, as it
/// does for the Destruction domain smite. Declaring costs no action; the attack it rides on costs what it always costs.
/// Delegated RAW readings 2026-10-14 (docs/systems/RULES_COVERAGE.md 4.1):
/// - The daily use is spent by the declared attack itself, hit or miss: the paladin "may attempt to smite evil with one
///   normal melee attack", and the template "can make a normal melee attack to deal extra damage", once per day.
/// - Against a creature that is not of the smitten alignment the smite has no effect (no attack or damage bonus) and is
///   still used up (PHB p.44); the template smite, the same Su ability under the same name, is read the same way.
/// - A declaration lapses unused at the end of the creature's turn (GameManager.NextInitiativeTurn): it belongs to an
///   attack the creature makes, so it is not held over into other creatures' turns.
/// - A ranged attack neither gains nor spends the smite (both texts say a melee attack).
/// </summary>
public static class SmiteRules
{
    /// <summary>The template smite's damage cap (MM p.31, p.107: maximum of +20).</summary>
    public const int TemplateSmiteDamageCap = 20;

    /// <summary>True when the creature has any smite ability, used or not (the PC Smite button's visibility).</summary>
    public static bool HasAnySmite(CharacterStats stats)
        => stats != null && (stats.PaladinLevel >= 1 || stats.HasTemplateSmiteEvil || stats.HasTemplateSmiteGood);

    /// <summary>
    /// The smite the creature would declare now: its paladin Smite Evil while it has a use left, else its template smite
    /// while unused. None when it has no unused smite. A creature with both (a celestial paladin) smites with one per
    /// attack, never both on the same attack (delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1: two separate
    /// abilities, each making "one normal melee attack" a smite).
    /// </summary>
    public static SmiteSource AvailableSource(CharacterStats stats)
    {
        if (stats == null)
            return SmiteSource.None;
        SmiteEvilData paladin = stats.GetPaladinSmiteEvil();
        if (paladin != null && paladin.CanSmite)
            return SmiteSource.PaladinSmiteEvil;
        if (!stats.TemplateSmiteUsed)
        {
            if (stats.HasTemplateSmiteEvil)
                return SmiteSource.TemplateSmiteEvil;
            if (stats.HasTemplateSmiteGood)
                return SmiteSource.TemplateSmiteGood;
        }
        return SmiteSource.None;
    }

    /// <summary>The axis the creature's smites strike: "Evil" for a paladin or a celestial, "Good" for a fiendish creature.</summary>
    public static string AxisLabel(CharacterStats stats)
    {
        if (stats == null)
            return "Evil";
        if (stats.PaladinLevel <= 0 && stats.HasTemplateSmiteGood && !stats.HasTemplateSmiteEvil)
            return "Good";
        return "Evil";
    }

    /// <summary>"Smite Evil" or "Smite Good".</summary>
    public static string Label(SmiteSource source)
        => source == SmiteSource.TemplateSmiteGood ? "Smite Good" : "Smite Evil";

    /// <summary>Uses left today across the creature's smite abilities (paladin uses plus an unused template smite).</summary>
    public static int UsesLeft(CharacterStats stats)
    {
        if (stats == null)
            return 0;
        int uses = 0;
        SmiteEvilData paladin = stats.GetPaladinSmiteEvil();
        if (paladin != null)
            uses += paladin.RemainingUses;
        if ((stats.HasTemplateSmiteEvil || stats.HasTemplateSmiteGood) && !stats.TemplateSmiteUsed)
            uses += 1;
        return uses;
    }

    /// <summary>Uses per day across the creature's smite abilities.</summary>
    public static int UsesPerDay(CharacterStats stats)
    {
        if (stats == null)
            return 0;
        int uses = 0;
        SmiteEvilData paladin = stats.GetPaladinSmiteEvil();
        if (paladin != null)
            uses += paladin.MaxUsesPerDay;
        if (stats.HasTemplateSmiteEvil || stats.HasTemplateSmiteGood)
            uses += 1;
        return uses;
    }

    /// <summary>True when <paramref name="target"/> is of the alignment <paramref name="source"/> smites.</summary>
    public static bool IsSmiteFoe(SmiteSource source, CharacterStats target)
    {
        if (target == null)
            return false;
        switch (source)
        {
            case SmiteSource.PaladinSmiteEvil:
            case SmiteSource.TemplateSmiteEvil:
                return AlignmentHelper.IsEvil(target.CharacterAlignment);
            case SmiteSource.TemplateSmiteGood:
                return AlignmentHelper.IsGood(target.CharacterAlignment);
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether a creature may declare a smite now: it has an unused smite and none is declared yet. Both the PC button
    /// and the AI call this; the attack that follows checks its own legality.
    /// </summary>
    public static bool CanDeclare(CharacterController actor, out SmiteSource source, out string reason)
    {
        source = SmiteSource.None;
        reason = string.Empty;
        if (actor == null || actor.Stats == null)
        {
            reason = "No active character";
            return false;
        }
        CharacterStats stats = actor.Stats;
        if (!HasAnySmite(stats))
        {
            reason = "No smite ability";
            return false;
        }
        if (stats.DeclaredSmite != SmiteSource.None)
        {
            reason = "Already declared";
            source = stats.DeclaredSmite;
            return false;
        }
        source = AvailableSource(stats);
        if (source == SmiteSource.None)
        {
            reason = "No uses left today";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Declares the creature's smite for its next melee attack this turn (no action). Returns the combat log line, or
    /// null with <paramref name="reason"/> when it cannot.
    /// </summary>
    public static string TryDeclare(CharacterController actor, out string reason)
    {
        if (!CanDeclare(actor, out SmiteSource source, out reason))
            return null;
        actor.Stats.DeclaredSmite = source;
        return $"{actor.Stats.CharacterName} declares {Label(source)}: {DescribeBonuses(actor.Stats, source)} on the next melee attack this turn (no effect on a creature that is not {(source == SmiteSource.TemplateSmiteGood ? "good" : "evil")}).";
    }

    /// <summary>Withdraws a declaration before any attack used it (the PC pressing Smite again); no use is spent.</summary>
    public static bool CancelDeclaration(CharacterStats stats)
    {
        if (stats == null || stats.DeclaredSmite == SmiteSource.None)
            return false;
        stats.DeclaredSmite = SmiteSource.None;
        return true;
    }

    /// <summary>
    /// The end of the creature's turn: an unused declaration lapses without spending a use (delegated RAW reading
    /// 2026-10-14). Returns true when one lapsed.
    /// </summary>
    public static bool LapseAtTurnEnd(CharacterStats stats) => CancelDeclaration(stats);

    /// <summary>"+2 attack, +5 damage" for the declared or available smite.</summary>
    public static string DescribeBonuses(CharacterStats stats, SmiteSource source)
    {
        int atk = AttackBonus(stats, source);
        int dmg = DamageBonus(stats, source);
        return $"{CharacterStats.FormatMod(atk)} attack, {CharacterStats.FormatMod(dmg)} damage";
    }

    /// <summary>The smite's attack bonus: the paladin's Charisma bonus, if any (PHB p.44); none for a template smite (MM p.31).</summary>
    public static int AttackBonus(CharacterStats stats, SmiteSource source)
    {
        if (stats == null || source != SmiteSource.PaladinSmiteEvil)
            return 0;
        return Mathf.Max(0, stats.CHAMod);
    }

    /// <summary>The smite's damage bonus: paladin level (PHB p.44), or Hit Dice up to +20 for a template smite (MM p.31, p.107).</summary>
    public static int DamageBonus(CharacterStats stats, SmiteSource source)
    {
        if (stats == null)
            return 0;
        switch (source)
        {
            case SmiteSource.PaladinSmiteEvil:
                return Mathf.Max(0, stats.PaladinLevel);
            case SmiteSource.TemplateSmiteEvil:
            case SmiteSource.TemplateSmiteGood:
                return Mathf.Clamp(stats.GetHitDice(), 0, TemplateSmiteDamageCap);
            default:
                return 0;
        }
    }

    /// <summary>
    /// Called once per weapon attack roll, before the roll (CharacterController.PerformSingleAttackWithCritCore). A
    /// declared smite is used up by a melee attack: the use is spent and the declaration cleared, and the bonuses apply
    /// only against a smite foe. A ranged attack leaves the declaration in place.
    /// </summary>
    public static SmiteApplication ConsumeForMeleeAttack(CharacterController attacker, CharacterController target, bool isRangedAttack)
    {
        var none = new SmiteApplication { Used = false, Source = SmiteSource.None };
        if (attacker == null || attacker.Stats == null || isRangedAttack)
            return none;
        CharacterStats stats = attacker.Stats;
        SmiteSource source = stats.DeclaredSmite;
        if (source == SmiteSource.None)
            return none;

        stats.DeclaredSmite = SmiteSource.None;
        if (source == SmiteSource.PaladinSmiteEvil)
        {
            SmiteEvilData paladin = stats.GetPaladinSmiteEvil();
            if (paladin == null || !paladin.ExpendSmite())
                return none;
        }
        else
        {
            if (stats.TemplateSmiteUsed)
                return none;
            stats.TemplateSmiteUsed = true;
        }

        bool foe = target != null && IsSmiteFoe(source, target.Stats);
        return new SmiteApplication
        {
            Used = true,
            Source = source,
            TargetIsSmiteFoe = foe,
            AttackBonus = foe ? AttackBonus(stats, source) : 0,
            DamageBonus = foe ? DamageBonus(stats, source) : 0,
            Label = Label(source)
        };
    }

    /// <summary>
    /// The AI's belief that <paramref name="target"/> is a smite foe. Until the AI knowledge model exists (AI roadmap
    /// step 2, tags v2) this is a placeholder that reads the target's true alignment, for paladins (whose detect evil is
    /// not implemented, CHR-083) and for template creatures, which have no detection at all (AI-065). The AI never
    /// declares a smite on a creature it does not believe is a smite foe.
    /// </summary>
    public static bool AiBelievesSmiteFoe(CharacterController actor, CharacterController target)
    {
        if (actor == null || actor.Stats == null || target == null || target.Stats == null)
            return false;
        return IsSmiteFoe(AvailableSource(actor.Stats), target.Stats);
    }
}
