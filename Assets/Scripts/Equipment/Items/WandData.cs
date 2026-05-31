using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unified wand data container used by ALL wand sources (store, crafted, debug).
/// Encapsulates the spell reference, caster level, save DC, charges, and pricing.
///
/// D&D 3.5e DMG pp. 245-247:
///   - Wands store a single spell at a fixed caster level.
///   - Only spells of 4th level or lower can be made into wands.
///   - Save DC = 10 + spell level + minimum ability modifier (floor(SL/2)).
///   - Price = Spell Level × Caster Level × 750 gp (0-level = 0.5 × CL × 750).
///   - Each wand starts with 50 charges, 1 charge per use, non-rechargeable.
///   - Spell trigger activation: standard action, does NOT provoke AoO.
///   - Requires spell on class list or Use Magic Device (DC 20).
///   - Depleted wand (0 charges) = useless nonmagical stick.
///
/// Usage:
///   var wd = WandData.Create(spell, casterLevel, isArcane, goldValue);
///   SpellData resolved = wd.GetSpell();
/// </summary>
[System.Serializable]
public class WandData
{
    // ======================== CORE PROPERTIES ========================

    /// <summary>Spell database ID (e.g., "magic_missile"). Always the SpellDatabase key.</summary>
    public string SpellId;

    /// <summary>Caster level of the wand (determines numeric spell effects).</summary>
    public int CasterLevel;

    /// <summary>Base spell level before metamagic (0-4 for standard wands).</summary>
    public int BaseSpellLevel;

    /// <summary>Effective spell level after metamagic. Equals BaseSpellLevel when no metamagic.</summary>
    public int EffectiveSpellLevel;

    /// <summary>
    /// Save DC baked into the wand at creation.
    /// D&D 3.5e DMG p.245: DC = 10 + spell level + minimum ability modifier.
    /// Min ability for spell level N = 10+N, modifier = floor(N/2).
    /// Only Heighten metamagic increases DC; other metamagic does NOT affect DC.
    /// </summary>
    public int SaveDC;

    /// <summary>
    /// For Heighten Spell: the target spell level to heighten to. -1 = not heightened.
    /// Only Heighten increases DC; stored separately from EffectiveSpellLevel.
    /// </summary>
    public int HeightenToLevel = -1;

    /// <summary>Metamagic feats applied at creation. Null or empty if none.</summary>
    public List<MetamagicFeatId> MetamagicFeats;

    /// <summary>True for arcane wands, false for divine.</summary>
    public bool IsArcane;

    /// <summary>Market price in gold pieces (D&D 3.5e DMG p.245).</summary>
    public int GoldValue;

    // ======================== CHARGE TRACKING ========================

    /// <summary>Current number of charges remaining (starts at 50 for new wands).</summary>
    public int CurrentCharges;

    /// <summary>Maximum charges this wand can hold (always 50 for standard wands).</summary>
    public int MaxCharges;

    // ======================== CONVENIENCE ========================

    /// <summary>True if this wand has metamagic feats applied.</summary>
    public bool HasMetamagic => MetamagicFeats != null && MetamagicFeats.Count > 0;

    /// <summary>"Arcane" or "Divine" string for display.</summary>
    public string TypeLabel => IsArcane ? "Arcane" : "Divine";

    /// <summary>True if the wand still has charges remaining.</summary>
    public bool HasCharges => CurrentCharges > 0;

    /// <summary>True if the wand is depleted (0 charges).</summary>
    public bool IsDepleted => CurrentCharges <= 0;

    /// <summary>Charge display string, e.g. "27/50 charges".</summary>
    public string ChargeDisplay => $"{CurrentCharges}/{MaxCharges} charges";

    // ======================== SPELL RESOLUTION ========================

    /// <summary>
    /// Resolves the spell from SpellDatabase using the stored SpellId.
    /// Always uses the canonical ID-based lookup.
    /// Returns null if the spell is not found.
    /// </summary>
    public SpellData GetSpell()
    {
        if (string.IsNullOrWhiteSpace(SpellId)) return null;
        SpellDatabase.Init();
        return SpellDatabase.GetSpell(SpellId);
    }

    // ======================== CHARGE MANAGEMENT ========================

    /// <summary>
    /// Consume one charge from the wand. Returns true if a charge was consumed,
    /// false if already depleted.
    /// </summary>
    public bool ConsumeCharge()
    {
        if (CurrentCharges <= 0) return false;
        CurrentCharges--;
        return true;
    }

    // ======================== DEEP COPY ========================

    /// <summary>Returns an independent deep copy of this WandData.</summary>
    public WandData Clone()
    {
        return new WandData
        {
            SpellId = SpellId,
            CasterLevel = CasterLevel,
            BaseSpellLevel = BaseSpellLevel,
            EffectiveSpellLevel = EffectiveSpellLevel,
            SaveDC = SaveDC,
            HeightenToLevel = HeightenToLevel,
            MetamagicFeats = MetamagicFeats != null ? new List<MetamagicFeatId>(MetamagicFeats) : null,
            IsArcane = IsArcane,
            GoldValue = GoldValue,
            CurrentCharges = CurrentCharges,
            MaxCharges = MaxCharges
        };
    }

    // ======================== FACTORY ========================

    /// <summary>Standard maximum charges for a new wand (D&D 3.5e DMG).</summary>
    public const int StandardMaxCharges = 50;

    /// <summary>Maximum base spell level for wands (D&D 3.5e DMG).</summary>
    public const int MaxBaseSpellLevel = 4;

    /// <summary>
    /// Creates a WandData from a SpellData and creation parameters.
    /// This is the SINGLE entry point for all wand creation (store, crafted, debug).
    /// </summary>
    /// <param name="spell">The base spell this wand contains.</param>
    /// <param name="casterLevel">Caster level of the wand.</param>
    /// <param name="isArcane">True for arcane wands, false for divine.</param>
    /// <param name="goldValue">Market price in GP.</param>
    /// <param name="metamagicFeats">Optional metamagic feats applied at creation.</param>
    /// <param name="effectiveSpellLevel">Effective spell level after metamagic (for cost). -1 = use base level.</param>
    /// <param name="saveDC">Baked save DC. 0 = auto-calculate using DMG formula.</param>
    /// <param name="heightenToLevel">For Heighten Spell: the target level. -1 = not heightened.</param>
    /// <param name="charges">Initial charge count. -1 = use StandardMaxCharges (50).</param>
    public static WandData Create(
        SpellData spell,
        int casterLevel,
        bool isArcane,
        int goldValue,
        List<MetamagicFeatId> metamagicFeats = null,
        int effectiveSpellLevel = -1,
        int saveDC = 0,
        int heightenToLevel = -1,
        int charges = -1)
    {
        if (spell == null)
        {
            Debug.LogError("[WandData] Cannot create WandData with null spell.");
            return null;
        }

        bool hasMetamagic = metamagicFeats != null && metamagicFeats.Count > 0;
        int baseLevel = spell.SpellLevel;
        int effLevel = effectiveSpellLevel >= 0 ? effectiveSpellLevel : baseLevel;

        // Calculate DC using DMG formula: 10 + SL + floor(SL/2), where SL is base level
        // (only Heighten raises DC — if heightened, use heightened level for DC)
        int dcLevel = (heightenToLevel > baseLevel) ? heightenToLevel : baseLevel;
        int defaultDC = 10 + dcLevel + (dcLevel / 2);

        int actualCharges = charges >= 0 ? charges : StandardMaxCharges;

        return new WandData
        {
            SpellId = spell.SpellId,
            CasterLevel = Mathf.Max(1, casterLevel),
            BaseSpellLevel = baseLevel,
            EffectiveSpellLevel = effLevel,
            SaveDC = saveDC > 0 ? saveDC : defaultDC,
            HeightenToLevel = heightenToLevel,
            MetamagicFeats = hasMetamagic ? new List<MetamagicFeatId>(metamagicFeats) : null,
            IsArcane = isArcane,
            GoldValue = goldValue,
            CurrentCharges = actualCharges,
            MaxCharges = actualCharges
        };
    }

    public override string ToString()
    {
        string mm = HasMetamagic ? $" +{MetamagicFeats.Count}mm" : "";
        return $"WandData({SpellId} CL{CasterLevel} L{BaseSpellLevel}→{EffectiveSpellLevel} DC{SaveDC} {ChargeDisplay}{mm} {TypeLabel} {GoldValue}gp)";
    }
}
