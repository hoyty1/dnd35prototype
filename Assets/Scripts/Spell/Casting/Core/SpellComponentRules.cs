using System;
using System.Collections.Generic;
using DND35e.Identifiers;

/// <summary>
/// The material, focus and divine focus rules of a cast (batch 11 step 17, raw-components-all-casters; decision D3 of
/// docs/designs/cast_core_batch11.md), one rule for every caster: a PC, a class-level NPC and a spellcasting monster
/// alike (docs/systems/PC_NPC_PARITY.md). The cast core's Legality stage refuses a cast that <see cref="Refusal"/>
/// names, before anything is spent; its Spend stage spends the priced material components with the slot
/// (<see cref="SpendCostlyComponents"/>); the AI's castable lists (SpellcastingComponent.GetCastablePreparedSpells) and
/// the PC's spell menu leave out what it refuses. Pure except for <see cref="SpendCostlyComponents"/>.
///
/// RAW applied:
/// - PHB p.140, p.174: a spell with a material (M), focus (F) or divine focus (DF) component cannot be cast without it.
///   A material component or focus with no listed price is in the spell component pouch (PHB p.130, p.174); the pouch
///   holds no priced component, no divine focus and no focus too big for it, which are separate items.
/// - PHB p.174: with M/DF or F/DF the arcane version of the spell has the material component or focus and the divine
///   version the divine focus. The version cast is the one of the class the spell is cast with (CasterLevelRules
///   .CastingClassFor; delegated RAW reading 2026-10-14); a monster that casts cleric or domain spells as arcane spells
///   (a dragon, MM p.69; the couatl, MM p.37) casts the arcane version and needs no divine focus (decision D3).
/// - PHB p.174: the divine focus of a cleric or paladin is a holy symbol, of an evil cleric an unholy symbol, of a druid
///   or ranger holly and mistletoe; PHB p.130: an unholy symbol is used by evil clerics or by neutral clerics who want to
///   cast evil spells; an adept's is a holy symbol of its tradition (DMG p.108). See <see cref="DivineFocusIdsFor(CharacterStats, string)"/>.
/// - PHB p.94 (Eschew Materials): a spell's material component costing 1 gp or less is not needed; a focus, a divine
///   focus and a costlier material component still are.
/// - PHB p.174: the casting process consumes a material component; a focus is not consumed.
/// - DMG p.238, p.213: a scroll or a wand supplies its spell's material component and focus, and a held charge's
///   delivery is no casting (PHB p.141), so neither is tested here; the F12 test panel's Components bypass skips it.
/// - MM p.315: a creature without hands still needs its components, touching or carrying them; Natural Spell (PHB p.98)
///   lets a wild-shaped druid use the components melded into its form, which stay in its inventory, so the test reads
///   the inventory whatever the form.
/// Delegated RAW readings 2026-10-14 (docs/systems/RULES_COVERAGE.md 4.1): the symbol follows the caster's alignment on
/// the good-evil axis (an evil cleric or adept an unholy symbol, a good one or a paladin a holy symbol, a neutral one
/// either; an adept as a cleric); a priced material component is spent with the slot, so a spell lost to a failed
/// Concentration check or spell failure still uses it up (PHB p.174: the casting process consumes it, and the XP Cost
/// paragraph treats XP like a material component, expended whether or not the casting succeeds), and a cast cancelled
/// at the attack of opportunity prompt gives it back with the slot; a priced material component that no item stands
/// for is paid from CharacterStats.ComponentGold (ITM-029).
/// </summary>
public static class SpellComponentRules
{
    public const string ReasonNoPouch = "no-component-pouch";
    public const string ReasonNoDivineFocus = "no-divine-focus";
    public const string ReasonNoCostlyComponent = "no-costly-component";
    public const string ReasonNoFocus = "no-focus";

    public const string EschewMaterials = "Eschew Materials";

    /// <summary>The index <see cref="SpendCostlyComponents"/> records for a component taken from the right hand.</summary>
    public const int RightHandIndex = -1;
    /// <summary>The index <see cref="SpendCostlyComponents"/> records for a component taken from the left hand.</summary>
    public const int LeftHandIndex = -2;

    private static readonly string[] HolySymbolIds = { ItemIDs.HOLY_SYMBOL_WOODEN, ItemIDs.HOLY_SYMBOL_SILVER };
    private static readonly string[] UnholySymbolIds = { ItemIDs.UNHOLY_SYMBOL_WOODEN, ItemIDs.UNHOLY_SYMBOL_SILVER };
    private static readonly string[] AnySymbolIds =
    {
        ItemIDs.HOLY_SYMBOL_WOODEN, ItemIDs.HOLY_SYMBOL_SILVER, ItemIDs.UNHOLY_SYMBOL_WOODEN, ItemIDs.UNHOLY_SYMBOL_SILVER
    };

    private static readonly string[] HollyIds = { ItemIDs.HOLLY_AND_MISTLETOE };

    /// <summary>True for a cast that makes no component test: a held-charge delivery, a scroll or wand, the F12 bypass.</summary>
    public static bool IsExempt(CastSource source)
    {
        if (source == null)
            return false;
        return CastComponentRules.IsHeldChargeDelivery(source) || source.IsItem || source.HasBypass(TestPanelBypass.Components);
    }

    /// <summary>
    /// True when <paramref name="caster"/> casts <paramref name="spell"/> as a divine spell: the class it is cast with
    /// (CasterLevelRules.CastingClassFor) is a divine casting class. A creature casting as a sorcerer casts arcane.
    /// </summary>
    public static bool CastsAsDivine(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        return IsDivineClass(caster.Stats, CasterLevelRules.CastingClassFor(caster, spell));
    }

    /// <summary>True when <paramref name="castingClass"/> casts divine spells (cleric, druid, paladin, ranger, adept).</summary>
    public static bool IsDivineClass(CharacterStats stats, string castingClass)
    {
        if (stats == null || string.IsNullOrWhiteSpace(castingClass))
            return false;
        return ClassAssociationRules.IsDivineCasterClass(castingClass)
               || stats.GetSpellcastingKind(castingClass) == SpellcastingType.Divine;
    }

    /// <summary>
    /// The reason <paramref name="caster"/> cannot cast <paramref name="spell"/> from <paramref name="source"/> for want
    /// of a component (<see cref="ReasonNoPouch"/>, <see cref="ReasonNoDivineFocus"/>, <see cref="ReasonNoFocus"/>,
    /// <see cref="ReasonNoCostlyComponent"/>), with the missing component in <paramref name="missing"/>; null when it has
    /// them or the source is exempt (<see cref="IsExempt"/>).
    /// </summary>
    public static string Refusal(CharacterController caster, SpellData spell, CastSource source, out string missing)
    {
        missing = null;
        if (caster == null || caster.Stats == null || spell == null || IsExempt(source))
            return null;
        string castingClass = CasterLevelRules.CastingClassFor(caster, spell);
        return Refusal(caster.Stats, caster.GetInventoryData(), spell, castingClass, out missing);
    }

    /// <summary>
    /// <see cref="Refusal(CharacterController, SpellData, CastSource, out string)"/> for a caster known by its stats,
    /// inventory and the class it casts the spell with (no source exemption).
    /// </summary>
    public static string Refusal(CharacterStats stats, Inventory inv, SpellData spell, string castingClass, out string missing)
    {
        missing = null;
        if (stats == null || spell == null)
            return null;

        bool divine = IsDivineClass(stats, castingClass);
        bool needsMaterial = spell.HasMaterialComponent && !(spell.MaterialOrDivineFocus && divine);
        bool needsFocus = spell.HasFocusComponent && !(spell.FocusOrDivineFocus && divine);
        bool needsDivineFocus = spell.HasDivineFocus && divine;
        SpellComponentRequirements req = SpellComponentRegistry.GetRequirements(spell.SpellId);

        // A material component with no listed price is in the pouch (PHB p.174); Eschew Materials waives it (PHB p.94).
        bool cheapMaterial = needsMaterial && (req == null || req.HasCommonMaterialComponents || !req.HasCostlyComponents);
        // A focus with no listed price is in the pouch; Eschew Materials does not waive a focus.
        bool cheapFocus = needsFocus && !HasOnlyPricedFoci(req);
        bool eschews = stats.HasFeat(EschewMaterials);
        if ((cheapFocus || (cheapMaterial && !eschews)) && !InventoryHoldsAny(inv, ItemIDs.COMPONENT_SPELL_POUCH))
        {
            missing = "spell component pouch";
            return ReasonNoPouch;
        }

        if (needsDivineFocus && !InventoryHoldsAny(inv, DivineFocusIdsFor(stats, castingClass)))
        {
            missing = DivineFocusNameFor(stats, castingClass);
            return ReasonNoDivineFocus;
        }

        if (needsFocus && req != null && req.FocusComponents != null)
        {
            foreach (SpellMaterialComponent focus in req.FocusComponents)
            {
                if (focus == null || string.IsNullOrEmpty(focus.InventoryItemId))
                    continue;
                if (!InventoryHoldsAny(inv, focus.InventoryItemId))
                {
                    missing = focus.GetDisplayString();
                    return ReasonNoFocus;
                }
            }
        }

        if (needsMaterial && req != null && req.HasCostlyComponents)
        {
            int goldNeeded = 0;
            foreach (SpellMaterialComponent comp in req.MaterialComponents)
            {
                if (comp == null || comp.GpCost <= 0)
                    continue;
                if (!string.IsNullOrEmpty(comp.InventoryItemId))
                {
                    if (!InventoryHoldsAny(inv, comp.InventoryItemId))
                    {
                        missing = comp.GetDisplayString();
                        return ReasonNoCostlyComponent;
                    }
                }
                else if (comp.IsConsumed)
                {
                    goldNeeded += comp.GpCost;
                }
            }
            if (goldNeeded > 0 && stats.ComponentGold < goldNeeded)
            {
                missing = $"{goldNeeded:N0} gp of material components (has {stats.ComponentGold:N0} gp)";
                return ReasonNoCostlyComponent;
            }
        }

        return null;
    }

    /// <summary>
    /// Spends the priced material components of <paramref name="spell"/> once its slot is spent (PHB p.174): each
    /// component item is taken from the pack or, failing that, from a hand, as <see cref="Refusal(CharacterController, SpellData, CastSource, out string)"/>
    /// finds it (recorded in <paramref name="removed"/> with its pack index, or <see cref="RightHandIndex"/> or
    /// <see cref="LeftHandIndex"/>, for a refund) and the rest is paid from CharacterStats.ComponentGold
    /// (<paramref name="goldSpent"/>). Nothing for an exempt
    /// source or the divine version of an M/DF spell. Returns the log line, or null when nothing was spent.
    /// </summary>
    public static string SpendCostlyComponents(CharacterController caster, SpellData spell, CastSource source,
        List<KeyValuePair<int, ItemData>> removed, out int goldSpent)
    {
        goldSpent = 0;
        if (caster == null || caster.Stats == null || spell == null || IsExempt(source) || !spell.HasMaterialComponent)
            return null;
        if (spell.MaterialOrDivineFocus && CastsAsDivine(caster, spell))
            return null;
        SpellComponentRequirements req = SpellComponentRegistry.GetRequirements(spell.SpellId);
        if (req == null || !req.HasCostlyComponents)
            return null;

        Inventory inv = caster.GetInventoryData();
        var spent = new List<string>();
        foreach (SpellMaterialComponent comp in req.MaterialComponents)
        {
            if (comp == null || comp.GpCost <= 0 || !comp.IsConsumed)
                continue;
            if (!string.IsNullOrEmpty(comp.InventoryItemId))
            {
                int index = FindItemIndex(inv, comp.InventoryItemId);
                ItemData item;
                if (index >= 0)
                {
                    item = inv.GeneralSlots[index];
                    inv.GeneralSlots[index] = null;
                }
                else if (Is(inv?.RightHandSlot, comp.InventoryItemId))
                {
                    index = RightHandIndex;
                    item = inv.RightHandSlot;
                    inv.RightHandSlot = null;
                }
                else if (Is(inv?.LeftHandSlot, comp.InventoryItemId))
                {
                    index = LeftHandIndex;
                    item = inv.LeftHandSlot;
                    inv.LeftHandSlot = null;
                }
                else
                {
                    continue;
                }
                removed?.Add(new KeyValuePair<int, ItemData>(index, item));
                spent.Add(comp.GetDisplayString());
            }
            else
            {
                int cost = Math.Min(comp.GpCost, Math.Max(0, caster.Stats.ComponentGold));
                caster.Stats.ComponentGold -= cost;
                goldSpent += cost;
                spent.Add(comp.GetDisplayString());
            }
        }
        if (inv != null && spent.Count > 0)
            inv.RecalculateStats();
        if (spent.Count == 0)
            return null;
        string gold = goldSpent > 0 ? $" ({caster.Stats.ComponentGold:N0} gp of components left)" : "";
        return $"{caster.Stats.CharacterName} uses up {string.Join(" and ", spent)} casting {spell.Name}{gold}.";
    }

    /// <summary>Gives back what <see cref="SpendCostlyComponents"/> spent (a cast cancelled at its AoO prompt).</summary>
    public static void RefundCostlyComponents(CharacterController caster, List<KeyValuePair<int, ItemData>> removed, int goldSpent)
    {
        if (caster == null || caster.Stats == null)
            return;
        caster.Stats.ComponentGold += Math.Max(0, goldSpent);
        if (removed == null || removed.Count == 0)
            return;
        Inventory inv = caster.GetInventoryData();
        if (inv == null || inv.GeneralSlots == null)
            return;
        foreach (KeyValuePair<int, ItemData> entry in removed)
        {
            if (entry.Value == null)
                continue;
            if (entry.Key == RightHandIndex && inv.RightHandSlot == null)
                inv.RightHandSlot = entry.Value;
            else if (entry.Key == LeftHandIndex && inv.LeftHandSlot == null)
                inv.LeftHandSlot = entry.Value;
            else if (entry.Key >= 0 && entry.Key < inv.GeneralSlots.Length && inv.GeneralSlots[entry.Key] == null)
                inv.GeneralSlots[entry.Key] = entry.Value;
            else
                inv.AddItem(entry.Value);
        }
        inv.RecalculateStats();
    }

    /// <summary>The log line of a component refusal, the same on every path.</summary>
    public static string DescribeRefusal(string casterName, string spellName, string reason, string missing)
    {
        string what = string.IsNullOrEmpty(missing) ? "a component" : missing;
        switch (reason)
        {
            case ReasonNoDivineFocus:
                return $"{casterName} cannot cast {spellName}: it needs a divine focus ({what}) (PHB p.174).";
            case ReasonNoFocus:
                return $"{casterName} cannot cast {spellName}: it needs its focus, {what} (PHB p.174).";
            case ReasonNoCostlyComponent:
                return $"{casterName} cannot cast {spellName}: it needs its material component, {what} (PHB p.174).";
            default:
                return $"{casterName} cannot cast {spellName}: it needs a {what} for its components (PHB p.130, p.174). One costs 5 gp at the store.";
        }
    }

    /// <summary>
    /// Every item that may be the divine focus of <paramref name="castingClass"/> for some caster, whatever its alignment:
    /// any holy or unholy symbol, or holly and mistletoe for a druid or ranger (PHB p.174). For the items that serve a
    /// given caster use <see cref="DivineFocusIdsFor(CharacterStats, string)"/>.
    /// </summary>
    public static string[] DivineFocusIdsFor(string castingClass)
    {
        if (IsNatureClass(castingClass))
            return HollyIds;
        if (string.IsNullOrWhiteSpace(castingClass))
        {
            var any = new List<string>(AnySymbolIds);
            any.AddRange(HollyIds);
            return any.ToArray();
        }
        return AnySymbolIds;
    }

    /// <summary>
    /// The divine focus items that serve <paramref name="stats"/> casting with <paramref name="castingClass"/>: holly and
    /// mistletoe for a druid or ranger (PHB p.174); for a cleric, paladin or adept the symbol its alignment calls for, an
    /// unholy symbol if it is evil, a holy symbol if it is good, either if it is neutral on the good-evil axis (PHB p.174:
    /// a cleric's or paladin's focus is a holy symbol, an evil cleric's an unholy one; PHB p.130: unholy symbols serve evil
    /// clerics and neutral clerics who want to cast evil spells; an adept follows the cleric's rule, DMG p.108). Delegated
    /// RAW reading 2026-10-14 (docs/systems/RULES_COVERAGE.md 4.1): a neutral cleric may use either symbol for any spell,
    /// and an unset alignment counts as neutral.
    /// </summary>
    public static string[] DivineFocusIdsFor(CharacterStats stats, string castingClass)
    {
        if (IsNatureClass(castingClass))
            return HollyIds;
        string[] symbols = SymbolIdsFor(stats);
        if (string.IsNullOrWhiteSpace(castingClass))
        {
            var any = new List<string>(symbols);
            any.AddRange(HollyIds);
            return any.ToArray();
        }
        return symbols;
    }

    /// <summary>The holy or unholy symbols that serve <paramref name="stats"/> by its alignment (see <see cref="DivineFocusIdsFor(CharacterStats, string)"/>).</summary>
    public static string[] SymbolIdsFor(CharacterStats stats)
    {
        Alignment alignment = stats != null ? stats.CharacterAlignment : Alignment.None;
        if (AlignmentHelper.IsEvil(alignment))
            return UnholySymbolIds;
        if (AlignmentHelper.IsGood(alignment))
            return HolySymbolIds;
        return AnySymbolIds;
    }

    private static string DivineFocusNameFor(CharacterStats stats, string castingClass)
    {
        if (IsNatureClass(castingClass))
            return "holly and mistletoe";
        Alignment alignment = stats != null ? stats.CharacterAlignment : Alignment.None;
        if (AlignmentHelper.IsEvil(alignment))
            return "unholy symbol";
        if (AlignmentHelper.IsGood(alignment))
            return "holy symbol";
        return "holy or unholy symbol";
    }

    private static bool IsNatureClass(string castingClass)
        => string.Equals(castingClass, "Druid", StringComparison.OrdinalIgnoreCase)
           || string.Equals(castingClass, "Ranger", StringComparison.OrdinalIgnoreCase);

    private static bool HasOnlyPricedFoci(SpellComponentRequirements req)
    {
        if (req == null || req.FocusComponents == null || req.FocusComponents.Count == 0)
            return false;
        foreach (SpellMaterialComponent focus in req.FocusComponents)
        {
            if (focus != null && focus.GpCost <= 0 && string.IsNullOrEmpty(focus.InventoryItemId))
                return false;
        }
        return true;
    }

    /// <summary>True when the pack or a hand holds an item with one of <paramref name="itemIds"/>.</summary>
    public static bool InventoryHoldsAny(Inventory inv, params string[] itemIds)
    {
        if (inv == null || itemIds == null)
            return false;
        foreach (string id in itemIds)
        {
            if (FindItemIndex(inv, id) >= 0 || Is(inv.RightHandSlot, id) || Is(inv.LeftHandSlot, id))
                return true;
        }
        return false;
    }

    private static int FindItemIndex(Inventory inv, string itemId)
    {
        if (inv == null || inv.GeneralSlots == null || string.IsNullOrEmpty(itemId))
            return -1;
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            if (Is(inv.GeneralSlots[i], itemId))
                return i;
        }
        return -1;
    }

    private static bool Is(ItemData item, string itemId)
        => item != null && string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase);
}
