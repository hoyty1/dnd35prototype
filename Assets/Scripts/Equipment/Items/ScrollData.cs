using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unified scroll data container used by ALL scroll sources (store, crafted, debug).
/// Encapsulates the spell reference, caster level, save DC, metamagic, and pricing.
///
/// D&D 3.5e DMG pp. 237-239:
///   - Scrolls store a single spell at a fixed caster level.
///   - Save DC = 10 + spell level (+ ability modifier at creation, if baked).
///   - Metamagic can be applied at creation time (raising effective spell level).
///   - Price = Spell Level × Caster Level × 25 gp (0-level = 0.5 × CL × 25).
///
/// Usage:
///   var sd = ScrollData.Create(spell, casterLevel, isArcane, goldValue);
///   SpellData resolved = sd.GetSpell();
/// </summary>
[System.Serializable]
public class ScrollData
{
    // ======================== CORE PROPERTIES ========================

    /// <summary>Spell database ID (e.g., "burning_hands"). Always the SpellDatabase key.</summary>
    public string SpellId;

    /// <summary>Caster level of the scroll (determines numeric spell effects, CL checks).</summary>
    public int CasterLevel;

    /// <summary>Base spell level before metamagic (0-9).</summary>
    public int BaseSpellLevel;

    /// <summary>Effective spell level after metamagic. Equals BaseSpellLevel when no metamagic.</summary>
    public int EffectiveSpellLevel;

    /// <summary>
    /// Save DC baked into the scroll at creation.
    /// D&D 3.5e DMG p.238: DC = 10 + spell level + minimum ability modifier.
    /// For metamagic scrolls, uses effective spell level.
    /// </summary>
    public int SaveDC;

    /// <summary>Metamagic feats applied at creation. Null or empty if none.</summary>
    public List<MetamagicFeatId> MetamagicFeats;

    /// <summary>True for arcane scrolls, false for divine.</summary>
    public bool IsArcane;

    /// <summary>Market price in gold pieces (D&D 3.5e DMG p.238).</summary>
    public int GoldValue;

    // ======================== CONVENIENCE ========================

    /// <summary>True if this scroll has metamagic feats applied.</summary>
    public bool HasMetamagic => MetamagicFeats != null && MetamagicFeats.Count > 0;

    /// <summary>"Arcane" or "Divine" string for display and validation.</summary>
    public string TypeLabel => IsArcane ? "Arcane" : "Divine";

    // ======================== SPELL RESOLUTION ========================

    /// <summary>
    /// Resolves the spell from SpellDatabase using the stored SpellId.
    /// Always uses the canonical ID-based lookup — no name-vs-ID ambiguity.
    /// Returns null if the spell is not found.
    /// </summary>
    public SpellData GetSpell()
    {
        if (string.IsNullOrWhiteSpace(SpellId)) return null;
        SpellDatabase.Init();
        return SpellDatabase.GetSpell(SpellId);
    }

    // ======================== DEEP COPY ========================

    /// <summary>Returns an independent deep copy of this ScrollData.</summary>
    public ScrollData Clone()
    {
        return new ScrollData
        {
            SpellId = SpellId,
            CasterLevel = CasterLevel,
            BaseSpellLevel = BaseSpellLevel,
            EffectiveSpellLevel = EffectiveSpellLevel,
            SaveDC = SaveDC,
            MetamagicFeats = MetamagicFeats != null ? new List<MetamagicFeatId>(MetamagicFeats) : null,
            IsArcane = IsArcane,
            GoldValue = GoldValue
        };
    }

    // ======================== FACTORY ========================

    /// <summary>
    /// Creates a ScrollData from a SpellData and creation parameters.
    /// This is the SINGLE entry point for all scroll creation (store, crafted, debug).
    /// </summary>
    /// <param name="spell">The base spell this scroll contains.</param>
    /// <param name="casterLevel">Caster level of the scroll.</param>
    /// <param name="isArcane">True for arcane scrolls, false for divine.</param>
    /// <param name="goldValue">Market price in GP.</param>
    /// <param name="metamagicFeats">Optional metamagic feats applied at creation.</param>
    /// <param name="effectiveSpellLevel">Effective spell level after metamagic. -1 = use base level.</param>
    /// <param name="saveDC">Baked save DC. 0 = auto-calculate as 10 + effective level.</param>
    public static ScrollData Create(
        SpellData spell,
        int casterLevel,
        bool isArcane,
        int goldValue,
        List<MetamagicFeatId> metamagicFeats = null,
        int effectiveSpellLevel = -1,
        int saveDC = 0)
    {
        if (spell == null)
        {
            Debug.LogError("[ScrollData] Cannot create ScrollData with null spell.");
            return null;
        }

        bool hasMetamagic = metamagicFeats != null && metamagicFeats.Count > 0;
        int baseLevel = spell.SpellLevel;
        int effLevel = effectiveSpellLevel >= 0 ? effectiveSpellLevel : baseLevel;

        return new ScrollData
        {
            SpellId = spell.SpellId,
            CasterLevel = Mathf.Max(1, casterLevel),
            BaseSpellLevel = baseLevel,
            EffectiveSpellLevel = effLevel,
            SaveDC = saveDC > 0 ? saveDC : (10 + effLevel),
            MetamagicFeats = hasMetamagic ? new List<MetamagicFeatId>(metamagicFeats) : null,
            IsArcane = isArcane,
            GoldValue = goldValue
        };
    }

    public override string ToString()
    {
        string mm = HasMetamagic ? $" +{MetamagicFeats.Count}mm" : "";
        return $"ScrollData({SpellId} CL{CasterLevel} L{BaseSpellLevel}→{EffectiveSpellLevel} DC{SaveDC}{mm} {TypeLabel} {GoldValue}gp)";
    }
}
