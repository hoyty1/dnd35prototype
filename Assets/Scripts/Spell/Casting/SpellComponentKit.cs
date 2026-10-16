using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// The component kit every caster carries (batch 11 step 17, decision D3 of docs/designs/cast_core_batch11.md), given
/// the same way to a PC at creation (GameManager.SetupStartingEquipment, after its class package) and to an NPC or
/// monster at spawn (GameManager.InitializeNPCFromDefinition, after its definition's gear), as real items it can lose:
/// - a spell component pouch to every creature that casts spells: a bard, cleric, druid, sorcerer, wizard or adept, a
///   paladin or ranger from 4th level (when it gains spells, PHB p.44, p.48), and a monster that casts as a sorcerer
///   (CharacterStats.InnateSpellcastingClass: a dragon, MM p.69; MM p.315: a creature without hands carries it on its
///   body);
/// - a wooden holy symbol to a cleric, adept or paladin, a wooden unholy symbol if it is evil (PHB p.33, p.46, p.130,
///   p.174; DMG p.108; a neutral one gets the holy symbol of the PHB cleric package and may also use an unholy one);
/// - holly and mistletoe to a druid, and to a ranger from 4th level (PHB p.37, p.174).
/// Sources: the PHB starting packages (bard p.30, cleric p.33, druid p.37, paladin p.46, sorcerer p.55, wizard p.58),
/// the DMG's NPC gear (DMG p.110-112: basic gear chosen by the DM; the sample NPCs list holy symbols, p.114-126) and
/// decision D3's reading that the abbreviated NPC gear lists still carry the cheap kit. Priced components are never
/// given: an NPC has them only when bought from its gear wealth or found (decision D3), so an NPC starts with no
/// CharacterStats.ComponentGold (InitializeNPCFromDefinition). Eschew Materials (PHB p.94) does not drop the pouch:
/// the feat waives no focus. Adds only what the inventory lacks, so a class package that already gave a pouch keeps one.
/// </summary>
public static class SpellComponentKit
{
    private static readonly string[] FullCasterClasses = { "Bard", "Cleric", "Druid", "Sorcerer", "Wizard", "Adept" };

    /// <summary>The paladin's and ranger's first spellcasting level (PHB p.44, p.48).</summary>
    public const int PartialCasterFirstSpellLevel = 4;

    /// <summary>The item ids of the kit <paramref name="stats"/> calls for (empty for a creature that casts nothing).</summary>
    public static List<string> KitFor(CharacterStats stats)
    {
        var kit = new List<string>();
        if (stats == null)
            return kit;

        bool casts = !string.IsNullOrWhiteSpace(stats.InnateSpellcastingClass);
        foreach (string cls in FullCasterClasses)
            casts |= stats.GetClassLevel(cls) > 0;
        int paladin = stats.GetClassLevel("Paladin");
        int ranger = stats.GetClassLevel("Ranger");
        casts |= paladin >= PartialCasterFirstSpellLevel || ranger >= PartialCasterFirstSpellLevel;
        if (casts)
            kit.Add(ItemIDs.COMPONENT_SPELL_POUCH);

        if (stats.GetClassLevel("Cleric") > 0 || stats.GetClassLevel("Adept") > 0 || paladin > 0)
            kit.Add(AlignmentHelper.IsEvil(stats.CharacterAlignment) ? ItemIDs.UNHOLY_SYMBOL_WOODEN : ItemIDs.HOLY_SYMBOL_WOODEN);

        if (stats.GetClassLevel("Druid") > 0 || ranger >= PartialCasterFirstSpellLevel)
            kit.Add(ItemIDs.HOLLY_AND_MISTLETOE);

        return kit;
    }

    /// <summary>
    /// Adds to <paramref name="inv"/> each kit item (<see cref="KitFor"/>) of its owner that it does not hold: a pouch,
    /// holly and mistletoe, or a symbol that serves the owner's alignment (SpellComponentRules.SymbolIdsFor: a holy symbol
    /// does not serve an evil cleric) already there counts. Returns the number of items added.
    /// </summary>
    public static int Give(CharacterStats stats, Inventory inv)
    {
        if (stats == null || inv == null)
            return 0;
        ItemDatabase.Init();
        int added = 0;
        foreach (string id in KitFor(stats))
        {
            if (Holds(stats, inv, id))
                continue;
#pragma warning disable CS0618 // the kit's item ids are ItemIDs constants
            ItemData item = ItemDatabase.CloneItem(id);
#pragma warning restore CS0618
            if (item == null)
            {
                Debug.LogWarning($"[SpellComponentKit] Item '{id}' is missing from ItemDatabase.");
                continue;
            }
            if (inv.AddItem(item))
                added++;
            else
                Debug.LogWarning($"[SpellComponentKit] {stats.CharacterName}'s pack has no room for {item.Name}; its spells that need it cannot be cast.");
        }
        return added;
    }

    private static bool Holds(CharacterStats stats, Inventory inv, string kitId)
    {
        if (kitId == ItemIDs.HOLY_SYMBOL_WOODEN || kitId == ItemIDs.UNHOLY_SYMBOL_WOODEN)
            return SpellComponentRules.InventoryHoldsAny(inv, SpellComponentRules.SymbolIdsFor(stats));
        return SpellComponentRules.InventoryHoldsAny(inv, kitId);
    }
}
