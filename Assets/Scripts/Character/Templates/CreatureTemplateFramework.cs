using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime creature-template framework for spawn-time mutation of NPC definitions.
/// Keeps template logic decoupled from encounter setup.
/// </summary>
public interface ICreatureTemplate
{
    string TemplateId { get; }
    void ApplyToDefinition(NPCDefinition definition);
}

public static class CreatureTemplateRegistry
{
    private static readonly Dictionary<string, ICreatureTemplate> _templates = new Dictionary<string, ICreatureTemplate>(StringComparer.OrdinalIgnoreCase)
    {
        { "celestial", new CelestialTemplate() },
        { "fiendish", new FiendishTemplate() },
    };

    public static List<ICreatureTemplate> ResolveTemplates(NPCDefinition definition)
    {
        List<ICreatureTemplate> resolved = new List<ICreatureTemplate>();
        if (definition == null)
            return resolved;

        if (definition.AppliedTemplateIds != null)
        {
            for (int i = 0; i < definition.AppliedTemplateIds.Count; i++)
            {
                string id = definition.AppliedTemplateIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (_templates.TryGetValue(id.Trim(), out ICreatureTemplate template) && !resolved.Contains(template))
                    resolved.Add(template);
            }
        }

        return resolved;
    }

    public static NPCDefinition ApplyTemplatesClone(NPCDefinition source)
    {
        if (source == null)
            return null;

        NPCDefinition clone = source.Clone();
        List<ICreatureTemplate> templates = ResolveTemplates(clone);
        for (int i = 0; i < templates.Count; i++)
            templates[i].ApplyToDefinition(clone);

        return clone;
    }
}

public abstract class OutsiderTemplateBase : ICreatureTemplate
{
    public abstract string TemplateId { get; }

    protected abstract bool IsGoodTemplate { get; }

    public void ApplyToDefinition(NPCDefinition definition)
    {
        if (definition == null)
            return;

        AddTemplateId(definition, TemplateId);

        int hd = Mathf.Max(1, definition.HitDice > 0 ? definition.HitDice : definition.Level);
        ApplyStatAdjustments(definition, hd);
        ApplyMitigation(definition, hd);
        ApplyTemplateTags(definition);
        ApplySpecialAbilities(definition, hd);
    }

    private void ApplyStatAdjustments(NPCDefinition definition, int hd)
    {
        int strBonus = GetSizeScaledStrengthBonus(definition.SizeCategory);
        int conBonus = string.Equals(definition.CreatureType, "Undead", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.CreatureType, "Construct", StringComparison.OrdinalIgnoreCase)
            ? 0
            : 2;
        int chaBonus = hd >= 5 ? 2 : 1;

        definition.STR += strBonus;
        definition.CHA += chaBonus;
        definition.CON += conBonus;

        bool animalLike = string.Equals(definition.CreatureType, "Animal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.CreatureType, "Magical Beast", StringComparison.OrdinalIgnoreCase);

        if (animalLike)
            definition.INT = Mathf.Max(definition.INT, 3);

        if (IsGoodTemplate)
            definition.WIS += 1;
        else
            definition.DEX += definition.SizeCategory <= SizeCategory.Small ? 2 : 1;
    }

    private void ApplyMitigation(NPCDefinition definition, int hd)
    {
        int resistanceValue = hd >= 8 ? 10 : 5;

        if (hd >= 4)
        {
            int drAmount = hd >= 12 ? 10 : 5;
            definition.DamageReductionAmount = Mathf.Max(definition.DamageReductionAmount, drAmount);
            definition.DamageReductionBypass |= DamageBypassTag.Magic;
        }

        if (hd >= 8)
        {
            int sr = hd + (hd >= 12 ? 10 : 5);
            definition.SpellResistance = Mathf.Max(definition.SpellResistance, sr);
        }

        if (IsGoodTemplate)
        {
            AddOrRaiseResistance(definition, DamageType.Acid, resistanceValue);
            AddOrRaiseResistance(definition, DamageType.Cold, resistanceValue);
            AddOrRaiseResistance(definition, DamageType.Electricity, resistanceValue);
            definition.GainsSmiteEvil = true;
            definition.GainsSmiteGood = false;
        }
        else
        {
            AddOrRaiseResistance(definition, DamageType.Cold, resistanceValue);
            AddOrRaiseResistance(definition, DamageType.Fire, resistanceValue);
            definition.GainsSmiteGood = true;
            definition.GainsSmiteEvil = false;
        }
    }

    private void ApplyTemplateTags(NPCDefinition definition)
    {
        AddTag(definition, "Templated");
        AddTag(definition, IsGoodTemplate ? "Celestial" : "Fiendish");
        AddTag(definition, IsGoodTemplate ? "Good" : "Evil");
    }

    private void ApplySpecialAbilities(NPCDefinition definition, int hd)
    {
        AddSpecialAbility(definition, "Darkvision 60 ft");
        AddSpecialAbility(definition, IsGoodTemplate ? "Smite Evil 1/day" : "Smite Good 1/day");

        if (definition.DamageReductionAmount > 0)
            AddSpecialAbility(definition, $"DR {definition.DamageReductionAmount}/magic");

        if (definition.SpellResistance > 0)
            AddSpecialAbility(definition, $"SR {definition.SpellResistance}");

        if (IsGoodTemplate)
            AddSpecialAbility(definition, BuildResistanceSummary(definition, new[] { DamageType.Acid, DamageType.Cold, DamageType.Electricity }, hd));
        else
            AddSpecialAbility(definition, BuildResistanceSummary(definition, new[] { DamageType.Cold, DamageType.Fire }, hd));
    }

    private static string BuildResistanceSummary(NPCDefinition definition, DamageType[] types, int hd)
    {
        int amount = hd >= 8 ? 10 : 5;
        string joined = string.Join(", ", Array.ConvertAll(types, DamageTextUtils.GetDamageTypeDisplay));
        return $"Resist {amount} ({joined})";
    }

    private static int GetSizeScaledStrengthBonus(SizeCategory size)
    {
        if (size >= SizeCategory.Large)
            return 4;
        if (size <= SizeCategory.Small)
            return 1;
        return 2;
    }

    private static void AddOrRaiseResistance(NPCDefinition definition, DamageType type, int amount)
    {
        if (definition.DamageResistances == null)
            definition.DamageResistances = new List<DamageResistanceEntry>();

        for (int i = 0; i < definition.DamageResistances.Count; i++)
        {
            DamageResistanceEntry entry = definition.DamageResistances[i];
            if (entry != null && entry.Type == type)
            {
                entry.Amount = Mathf.Max(entry.Amount, amount);
                return;
            }
        }

        definition.DamageResistances.Add(new DamageResistanceEntry { Type = type, Amount = amount });
    }

    private static void AddTag(NPCDefinition definition, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        if (definition.CreatureTags == null)
            definition.CreatureTags = new List<string>();

        for (int i = 0; i < definition.CreatureTags.Count; i++)
        {
            if (string.Equals(definition.CreatureTags[i], tag, StringComparison.OrdinalIgnoreCase))
                return;
        }

        definition.CreatureTags.Add(tag);
    }

    private static void AddSpecialAbility(NPCDefinition definition, string trait)
    {
        if (string.IsNullOrWhiteSpace(trait))
            return;

        if (definition.SpecialAbilities == null)
            definition.SpecialAbilities = new List<string>();

        for (int i = 0; i < definition.SpecialAbilities.Count; i++)
        {
            if (string.Equals(definition.SpecialAbilities[i], trait, StringComparison.OrdinalIgnoreCase))
                return;
        }

        definition.SpecialAbilities.Add(trait);
    }

    private static void AddTemplateId(NPCDefinition definition, string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return;

        if (definition.AppliedTemplateIds == null)
            definition.AppliedTemplateIds = new List<string>();

        for (int i = 0; i < definition.AppliedTemplateIds.Count; i++)
        {
            if (string.Equals(definition.AppliedTemplateIds[i], templateId, StringComparison.OrdinalIgnoreCase))
                return;
        }

        definition.AppliedTemplateIds.Add(templateId);
    }
}

public sealed class CelestialTemplate : OutsiderTemplateBase
{
    public override string TemplateId => "celestial";
    protected override bool IsGoodTemplate => true;
}

public sealed class FiendishTemplate : OutsiderTemplateBase
{
    public override string TemplateId => "fiendish";
    protected override bool IsGoodTemplate => false;
}
