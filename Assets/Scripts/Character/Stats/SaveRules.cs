using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a saving throw is made against, for the save bonuses that apply only to some effects (CHR-018). Every save
/// roll site builds one and passes it to <see cref="SaveRules.Modifier(CharacterStats, SavingThrowType, SaveContext)"/>;
/// a site that knows nothing about the effect passes <see cref="None"/> and gets the bonuses that apply to every save.
/// Immutable, so the shared instances below are safe.
/// </summary>
public sealed class SaveContext
{
    /// <summary>A spell or a spell-like ability (the dwarf's +2, PHB p.15). Magic items that cast a spell count too.</summary>
    public readonly bool IsSpell;
    /// <summary>A spell or effect of the enchantment school (elf and half-elf +2, PHB p.16, p.18; Still Mind, PHB p.41; Indomitable Will, PHB p.26).</summary>
    public readonly bool IsEnchantment;
    /// <summary>An illusion (the gnome's +2, PHB p.17).</summary>
    public readonly bool IsIllusion;
    /// <summary>A fear effect (the halfling's +2 morale bonus, PHB p.20; Remove Fear's +4 morale bonus, PHB p.271; a
    /// paladin's Aura of Courage, +4 morale bonus, PHB p.44).</summary>
    public readonly bool IsFear;
    /// <summary>A poison (the dwarf's +2, PHB p.15).</summary>
    public readonly bool IsPoison;
    /// <summary>Poison from a spider (Cloak of Arachnida's +2 luck bonus on Fortitude saves, DMG p.253).</summary>
    public readonly bool IsSpiderPoison;
    /// <summary>A trap (trap sense on Reflex saves, PHB p.26 and p.51).</summary>
    public readonly bool IsTrap;
    /// <summary>A spell-like ability of a fey (Resist Nature's Lure, PHB p.36).</summary>
    public readonly bool IsFeySpellLike;
    /// <summary>What the save is against, for logs (may be null).</summary>
    public readonly string Label;

    public SaveContext(bool spell = false, bool enchantment = false, bool illusion = false, bool fear = false,
        bool poison = false, bool spiderPoison = false, bool trap = false, bool feySpellLike = false, string label = null)
    {
        IsSpell = spell;
        IsEnchantment = enchantment;
        IsIllusion = illusion;
        IsFear = fear;
        IsPoison = poison || spiderPoison;
        IsSpiderPoison = spiderPoison;
        IsTrap = trap;
        IsFeySpellLike = feySpellLike;
        Label = label;
    }

    /// <summary>A save the roll site knows nothing about: only the bonuses that apply to every save.</summary>
    public static readonly SaveContext None = new SaveContext();

    /// <summary>A fear effect that is not a spell (frightful presence, an aura of fear).</summary>
    public static readonly SaveContext Fear = new SaveContext(fear: true, label: "fear");

    /// <summary>A trap.</summary>
    public static readonly SaveContext Trap = new SaveContext(trap: true, label: "trap");

    /// <summary>A poison, by its PoisonDatabase id or name; a spider's poison (an id or name containing "spider") is marked as such.</summary>
    public static SaveContext Poison(string poisonIdOrName = null)
    {
        bool spider = !string.IsNullOrEmpty(poisonIdOrName)
            && poisonIdOrName.IndexOf("spider", System.StringComparison.OrdinalIgnoreCase) >= 0;
        return new SaveContext(poison: true, spiderPoison: spider, label: string.IsNullOrEmpty(poisonIdOrName) ? "poison" : poisonIdOrName);
    }

    /// <summary>
    /// A save against <paramref name="spell"/>: a spell, plus its school (enchantment, illusion) and whether it is a
    /// fear effect. When the caster is a fey without a spellcasting class, its spells are its spell-like abilities
    /// (MM fey such as the dryad, pixie and satyr), which Resist Nature's Lure covers. A null spell gives a plain
    /// spell context.
    /// </summary>
    public static SaveContext ForSpell(SpellData spell, CharacterStats caster = null)
    {
        bool feySpellLike = caster != null
            && string.Equals((caster.CreatureType ?? string.Empty).Trim(), "Fey", System.StringComparison.OrdinalIgnoreCase)
            && !caster.IsSpellcaster;
        if (spell == null)
            return new SaveContext(spell: true, feySpellLike: feySpellLike, label: "spell");

        string school = (spell.School ?? string.Empty).Trim();
        // The school may carry a subschool or descriptor ("Illusion (Glamer)", "Evocation [Fire]").
        bool enchantment = school.StartsWith("Enchantment", System.StringComparison.OrdinalIgnoreCase);
        bool illusion = school.StartsWith("Illusion", System.StringComparison.OrdinalIgnoreCase);
        bool fear = SpellCaster.IsFearSpell(spell);
        bool poison = string.Equals(spell.SpellId, DND35e.Identifiers.SpellNames.POISON, System.StringComparison.Ordinal);
        return new SaveContext(spell: true, enchantment: enchantment, illusion: illusion, fear: fear, poison: poison,
            feySpellLike: feySpellLike, label: spell.Name);
    }

    /// <summary>A save against the spell <paramref name="spellId"/> (looked up read-only in SpellDatabase).</summary>
    public static SaveContext ForSpellId(string spellId, CharacterStats caster = null)
    {
        SpellDatabase.Init();
        return ForSpell(string.IsNullOrEmpty(spellId) ? null : SpellDatabase.GetSpell(spellId), caster);
    }
}

/// <summary>
/// The one saving throw modifier (CHR-018, PHB p.177-178): <see cref="CharacterStats.GetSaveTotal"/>, which holds every
/// bonus that applies to all saves (ability, base save, feats, Divine Grace, racial, resistance, competence, luck,
/// morale, familiar, rage, conditions), plus the bonuses that apply only against some effects, chosen by a
/// <see cref="SaveContext"/>. Every save roll site uses it, on the PC and the NPC side alike (PC_NPC_PARITY); the
/// spell-specific situational bonuses of SpellCaster.GetSaveModifier (Charm Person, Hideous Laughter, the alignment
/// ward, Lullaby) are added there on top (SPL-018).
/// Stacking (PHB p.171): bonuses of one type do not stack except dodge and untyped ones; luck bonuses stack by the
/// owner's house rule (Assets/Scripts/Spell/BonusType.cs).
/// </summary>
public static class SaveRules
{
    /// <summary>The save modifier of <paramref name="stats"/> for <paramref name="save"/> against <paramref name="context"/>.</summary>
    public static int Modifier(CharacterStats stats, SavingThrowType save, SaveContext context = null)
    {
        if (stats == null)
            return 0;
        return stats.GetSaveTotal(save) + SituationalBonus(stats, save, context, out _);
    }

    /// <summary>The save modifier for a SpellSaveResolver save type.</summary>
    public static int Modifier(CharacterStats stats, SaveType save, SaveContext context = null)
        => Modifier(stats, ToSavingThrowType(save), context);

    /// <summary>The save modifier for a SavingThrowResolver save type.</summary>
    public static int Modifier(CharacterStats stats, SavingThrowResolver.SaveType save, SaveContext context = null)
        => Modifier(stats, ToSavingThrowType(save), context);

    /// <summary>
    /// The save modifier for a save named as text ("Fortitude"/"Fort", "Reflex"/"Ref", "Will", any case). An unknown
    /// or empty name gives 0, as before.
    /// </summary>
    public static int Modifier(CharacterStats stats, string saveName, SaveContext context = null)
    {
        return TryParse(saveName, out SavingThrowType save) ? Modifier(stats, save, context) : 0;
    }

    /// <summary>Parses "Fortitude"/"Fort", "Reflex"/"Ref" or "Will" (any case, surrounding spaces ignored).</summary>
    public static bool TryParse(string saveName, out SavingThrowType save)
    {
        save = SavingThrowType.Will;
        if (string.IsNullOrWhiteSpace(saveName))
            return false;
        string s = saveName.Trim().ToLowerInvariant();
        if (s.StartsWith("fort")) { save = SavingThrowType.Fortitude; return true; }
        if (s.StartsWith("ref")) { save = SavingThrowType.Reflex; return true; }
        if (s.StartsWith("will")) { save = SavingThrowType.Will; return true; }
        return false;
    }

    public static SavingThrowType ToSavingThrowType(SaveType save)
    {
        switch (save)
        {
            case SaveType.Fortitude: return SavingThrowType.Fortitude;
            case SaveType.Reflex: return SavingThrowType.Reflex;
            default: return SavingThrowType.Will;
        }
    }

    public static SavingThrowType ToSavingThrowType(SavingThrowResolver.SaveType save)
    {
        switch (save)
        {
            case SavingThrowResolver.SaveType.Fortitude: return SavingThrowType.Fortitude;
            case SavingThrowResolver.SaveType.Reflex: return SavingThrowType.Reflex;
            default: return SavingThrowType.Will;
        }
    }

    /// <summary>
    /// The bonuses that apply only against the effect described by <paramref name="context"/> (0 for a null or
    /// <see cref="SaveContext.None"/> context). <paramref name="sources"/> names each one for logs (empty when none).
    /// <list type="bullet">
    /// <item>Racial (PHB p.15-20): dwarf +2 against poison and against spells and spell-like effects, elf and half-elf
    /// +2 against enchantments, gnome +2 against illusions; drow +2 on Will saves against spells and spell-like abilities
    /// (MM p.103). NPCs of these races have them too (CRE-038). The ones that apply are added together: PHB p.171 names
    /// racial bonuses among the bonuses of one type that do stack, so a dwarf gets +4 against the Poison spell (spell
    /// and poison). DMG p.21 states only the general rule (same-type bonuses do not stack, except dodge and some
    /// circumstance bonuses); which book governs is an open owner question (CHR-019).</item>
    /// <item>Morale against fear: the halfling's +2 (PHB p.20), Remove Fear's +4 (PHB p.271), Bless's and Aid's +1
    /// (PHB p.205, p.196; <see cref="CharacterStats.FearMoraleSaveBonusFromSpells"/>) and a paladin's Aura of
    /// Courage, +4 to each ally within 10 ft while she is conscious (PHB p.44, <see cref="AuraOfCourageBonus"/>). Morale
    /// bonuses do not stack (PHB p.171), so the best one applies, and on a Will save only beyond the barbarian's rage
    /// morale bonus already in <see cref="CharacterStats.GetSaveTotal"/> (PHB p.25). Spell morale bonuses such as
    /// Heroism sit in the untyped pool <see cref="CharacterStats.MoraleSaveBonus"/> and still stack with it (SPL-026).</item>
    /// <item>Still Mind (monk 3, PHB p.41): +2 on every save against enchantments.</item>
    /// <item>Indomitable Will (barbarian 14, PHB p.26): +4 on Will saves against enchantment spells while raging.</item>
    /// <item>Resist Nature's Lure (druid 4, PHB p.36): +4 against the spell-like abilities of fey.</item>
    /// <item>Trap sense (barbarian 3 and rogue 3, PHB p.26, p.51): on Reflex saves against traps; the classes' bonuses stack.</item>
    /// <item>Cloak of Arachnida (DMG p.253): +2 luck bonus on Fortitude saves against spider poison (luck stacks, house rule).</item>
    /// </list>
    /// </summary>
    public static int SituationalBonus(CharacterStats stats, SavingThrowType save, SaveContext context, out string sources)
    {
        sources = string.Empty;
        if (stats == null || context == null || ReferenceEquals(context, SaveContext.None))
            return 0;

        int total = 0;
        var parts = new System.Text.StringBuilder();

        RaceData race = stats.Race;
        if (race != null)
        {
            // Racial bonuses stack with each other (PHB p.171); owner question in CHR-019 (DMG p.21 reads otherwise).
            int racial = 0;
            if (context.IsPoison) racial += Mathf.Max(0, race.SaveVsPoison);
            if (context.IsSpell) racial += Mathf.Max(0, race.SaveVsSpells);
            if (context.IsEnchantment) racial += Mathf.Max(0, race.SaveVsEnchantment);
            if (context.IsIllusion) racial += Mathf.Max(0, race.SaveVsIllusion);
            // Drow: +2 on Will saves only against spells and spell-like abilities (MM p.103).
            if (context.IsSpell && save == SavingThrowType.Will) racial += Mathf.Max(0, race.WillSaveVsSpells);
            if (racial > 0)
                Add(ref total, parts, racial, "racial (" + race.RaceName + ")");
        }

        if (context.IsFear)
        {
            int fearMorale = Mathf.Max(race != null ? race.SaveVsFear : 0, stats.RemoveFearMoraleBonus);
            fearMorale = Mathf.Max(fearMorale, stats.FearMoraleSaveBonusFromSpells);
            fearMorale = Mathf.Max(fearMorale, AuraOfCourageBonus(stats));
            // Morale bonuses do not stack (PHB p.171): only the part beyond the rage morale bonus on Will counts.
            if (save == SavingThrowType.Will)
                fearMorale -= Mathf.Max(0, stats.RageWillBonus);
            if (fearMorale > 0)
                Add(ref total, parts, fearMorale, "morale vs fear");
        }

        if (context.IsEnchantment)
        {
            Add(ref total, parts, stats.StillMindBonus, "Still Mind");
            if (save == SavingThrowType.Will && context.IsSpell)
                Add(ref total, parts, stats.IndomitableWillBonus, "Indomitable Will");
        }

        if (context.IsFeySpellLike)
            Add(ref total, parts, stats.ResistNaturesLureBonus, "Resist Nature's Lure");

        if (context.IsTrap && save == SavingThrowType.Reflex)
            Add(ref total, parts, stats.TrapSenseSaveBonus, "trap sense");

        if (context.IsSpiderPoison && save == SavingThrowType.Fortitude)
            Add(ref total, parts, stats.WondrousLuckFortSaveBonus, "luck vs spider poison");

        sources = parts.ToString();
        return total;
    }

    /// <summary>
    /// Aura of Courage (paladin 3, PHB p.44): +4 morale bonus on saves against fear for each ally within 10 ft of a
    /// paladin who is conscious (not dead, dying, stable or unconscious). The paladin herself is immune to fear instead
    /// (<see cref="CharacterStats.IsImmuneToFear"/>), so her own aura gives her nothing here. The saver is
    /// <see cref="CharacterStats.OwnerCharacter"/>; <paramref name="combatants"/> defaults to the active combatants of
    /// <see cref="GameManager"/>. 0 when the saver has no controller. PCs and NPCs alike (TeamUtility.IsAlly).
    /// </summary>
    public static int AuraOfCourageBonus(CharacterStats stats, IList<CharacterController> combatants = null)
    {
        CharacterController saver = stats != null ? stats.OwnerCharacter : null;
        if (saver == null)
            return 0;
        if (combatants == null)
            combatants = GameManager.Instance != null ? GameManager.Instance.GetAllCharactersForAI() : null;
        if (combatants == null)
            return 0;

        for (int i = 0; i < combatants.Count; i++)
        {
            CharacterController paladin = combatants[i];
            if (paladin == null || paladin == saver || paladin.Stats == null || !paladin.Stats.HasAuraOfCourage)
                continue;
            if (paladin.IsDead || paladin.IsUnconscious || paladin.Stats.CurrentHP < 0)
                continue;
            if (!TeamUtility.IsAlly(saver, paladin))
                continue;
            if (DistanceSquares(saver, paladin) <= 2) // 10 ft, diagonals counted 5-10-5 (PHB p.147)
                return 4;
        }
        return 0;
    }

    /// <summary>The shortest grid distance in squares between any square of <paramref name="a"/> and of <paramref name="b"/>.</summary>
    private static int DistanceSquares(CharacterController a, CharacterController b)
    {
        List<Vector2Int> aSquares = a.GetOccupiedSquares();
        List<Vector2Int> bSquares = b.GetOccupiedSquares();
        int best = int.MaxValue;
        for (int i = 0; i < aSquares.Count; i++)
            for (int j = 0; j < bSquares.Count; j++)
                best = Mathf.Min(best, SquareGridUtils.GetDistance(aSquares[i], bSquares[j]));
        return best;
    }

    private static void Add(ref int total, System.Text.StringBuilder parts, int bonus, string label)
    {
        if (bonus == 0)
            return;
        total += bonus;
        if (parts.Length > 0)
            parts.Append(", ");
        parts.Append(label).Append(' ').Append(bonus > 0 ? "+" : string.Empty).Append(bonus);
    }
}
