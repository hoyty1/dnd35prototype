using System;
using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI.Knowledge
{
    /// <summary>
    /// The ground-truth fact computations the omniscient provider reads (batch 13 step 7). Each reads the subject's live
    /// state, the same state the display tags of StatusTagManager are refreshed from.
    /// </summary>
    public static class KnowledgeFacts
    {
        /// <summary>
        /// The wound band of <paramref name="currentHp"/> out of <paramref name="maxHp"/>: Down at 0 or less, Unhurt at full
        /// hit points (or more, with temporary hit points), Hurt at half or more, else BadlyHurt.
        /// </summary>
        public static WoundBand WoundBandOf(int currentHp, int maxHp)
        {
            if (currentHp <= 0)
                return WoundBand.Down;
            if (maxHp <= 0 || currentHp >= maxHp)
                return WoundBand.Unhurt;
            return currentHp * 2 >= maxHp ? WoundBand.Hurt : WoundBand.BadlyHurt;
        }

        /// <summary>
        /// The creature type of <paramref name="stats"/>, its CreatureType string normalized through the one creature-type
        /// parser (CreatureTypeProgressionDatabase.TryParseCreatureType: "Magical Beast" and "MagicalBeast" are one type;
        /// an empty string is the CharacterStats default, Humanoid). Read live, because wild shape changes the type.
        /// False for a string that names no MM type.
        /// </summary>
        public static bool TryCreatureTypeOf(CharacterStats stats, out CreatureTypeId type)
        {
            type = CreatureTypeId.Humanoid;
            return stats != null && CreatureTypeProgressionDatabase.TryParseCreatureType(stats.CreatureType, out type);
        }

        /// <summary>The MM spelling of a type ("Magical Beast", "Monstrous Humanoid").</summary>
        public static string CreatureTypeName(CreatureTypeId type)
        {
            switch (type)
            {
                case CreatureTypeId.MagicalBeast: return "Magical Beast";
                case CreatureTypeId.MonstrousHumanoid: return "Monstrous Humanoid";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// The worn armor's category, read from the armor slot item's typed data (ItemData.ArmorCat, PHB p.123 Table 7-6),
        /// never from its display tags: no item, or an item that is not armor (a robe), is Unarmored; body armor gives its
        /// Light, Medium or Heavy category; armor of any other category is Other. The base category is the armor's look:
        /// mithral full plate looks heavy though it counts as medium for its wearer (DMG p.284,
        /// MaterialProperties.GetEffectiveArmorCategory), so the apparent fact keeps the base category.
        /// </summary>
        public static WornArmor WornArmorOf(CharacterController c)
        {
            Inventory inv = InventoryOf(c);
            ItemData armor = inv != null ? inv.ArmorRobeSlot : null;
            if (armor == null || !armor.IsArmor)
                return WornArmor.Unarmored;
            switch (armor.ArmorCat)
            {
                case ArmorCategory.Light: return WornArmor.Light;
                case ArmorCategory.Medium: return WornArmor.Medium;
                case ArmorCategory.Heavy: return WornArmor.Heavy;
                default: return WornArmor.Other;
            }
        }

        /// <summary>True when an item in either hand of <paramref name="c"/> passes <paramref name="test"/>.</summary>
        public static bool HandsHold(CharacterController c, Func<ItemData, bool> test)
        {
            Inventory inv = InventoryOf(c);
            if (inv == null || test == null)
                return false;
            return (inv.RightHandSlot != null && test(inv.RightHandSlot)) || (inv.LeftHandSlot != null && test(inv.LeftHandSlot));
        }

        public static bool IsShield(ItemData item) => item != null && item.IsShield;

        /// <summary>A melee weapon; the virtual unarmed strike is not one (an empty hand is unarmed).</summary>
        public static bool IsMeleeWeapon(ItemData item)
            => item != null && item.IsWeapon && !item.IsRangedWeapon && !string.Equals(item.Id, DND35e.Identifiers.ItemIDs.UNARMED_STRIKE, StringComparison.Ordinal);

        public static bool IsRangedWeapon(ItemData item) => item != null && item.IsRangedWeapon;

        public static bool IsReachWeapon(ItemData item) => item != null && item.IsWeapon && (item.IsReachWeapon || item.HasReach);

        private static Inventory InventoryOf(CharacterController c)
            => c != null && c.Inventory != null ? c.Inventory.GetInventory() : null;

        // ── Batch 14 step 2: the facts the personality considerations read (docs/designs/personalities_batch14.md) ──

        /// <summary>Visibly fleeing: withdrawing this turn (PHB p.143), or frightened or panicked (DMG p.301).</summary>
        public static bool IsFleeing(CharacterController c)
            => c != null && (c.IsWithdrawing || c.HasCondition(CombatConditionType.Frightened) || c.HasCondition(CombatConditionType.Panicked));

        /// <summary>
        /// A healer: a cleric, a paladin of 2nd level or higher (Lay on Hands, PHB p.44), or a creature with a Healing-effect
        /// spell prepared in an unspent slot, in its prepared list, or among its spontaneously known spells. Reads only.
        /// </summary>
        public static bool IsHealer(CharacterController c)
        {
            if (c == null || c.Stats == null)
                return false;
            if (c.Stats.IsCleric || c.Stats.PaladinLevel >= 2)
                return true;
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null)
                return false;
            if (sc.SpellSlots != null)
                for (int i = 0; i < sc.SpellSlots.Count; i++)
                {
                    SpellSlot slot = sc.SpellSlots[i];
                    if (slot != null && !slot.IsUsed && IsHealingSpell(slot.PreparedSpell))
                        return true;
                }
            if (sc.PreparedSpells != null)
                for (int i = 0; i < sc.PreparedSpells.Count; i++)
                    if (IsHealingSpell(sc.PreparedSpells[i]))
                        return true;
            if (sc.IsSpontaneousCaster)
            {
                List<string> known = sc.SpontaneousData.GetAllKnownSpellIds();
                for (int i = 0; known != null && i < known.Count; i++)
                    if (IsHealingSpell(SpellDatabase.GetSpell(known[i])))
                        return true;
            }
            return false;
        }

        private static bool IsHealingSpell(SpellData spell) => spell != null && spell.EffectType == SpellEffectType.Healing;

        /// <summary>
        /// False when <paramref name="c"/> cannot attack this round: incapacitated (ThreatSystem.IsMoverIncapacitated:
        /// dead, dying, stable, unconscious), unable to act or to attack by its conditions (stunned, dazed, nauseated...),
        /// or helpless.
        /// </summary>
        public static bool CanAttackThisRound(CharacterController c)
            => c != null && c.Stats != null && !ThreatSystem.IsMoverIncapacitated(c) && c.CanTakeActions() && c.CanAttack()
               && !c.IsHelplessLikeConditionState();

        /// <summary>True when <paramref name="threatener"/> threatens a square of <paramref name="other"/>'s space (a Resilient Sphere between them blocks it).</summary>
        public static bool ThreatensSpaceOf(CharacterController threatener, CharacterController other)
        {
            if (threatener == null || other == null || threatener == other || threatener.Stats == null || threatener.Stats.IsDead)
                return false;
            HashSet<Vector2Int> threatened = ThreatSystem.GetThreatenedSquares(threatener, log: false);
            return ThreatensAnySquare(threatened, other) && !ResilientSphereAreaEffect.DoesSphereBlockInteraction(threatener, other);
        }

        private static bool ThreatensAnySquare(HashSet<Vector2Int> threatened, CharacterController other)
        {
            if (threatened == null || threatened.Count == 0 || other == null)
                return false;
            List<Vector2Int> squares = other.GetOccupiedSquares();
            for (int i = 0; i < squares.Count; i++)
                if (threatened.Contains(squares[i]))
                    return true;
            return false;
        }

        /// <summary>
        /// True when <paramref name="subject"/> threatens an ally of <paramref name="observer"/> (not the observer itself) in
        /// <paramref name="roster"/> that is not dead and is below 70% of its full hit points.
        /// </summary>
        public static bool ThreatensAllyInNeed(CharacterController observer, CharacterController subject, List<CharacterController> roster)
        {
            if (observer == null || subject == null || roster == null || subject.Stats == null || subject.Stats.IsDead)
                return false;
            HashSet<Vector2Int> threatened = null;
            for (int i = 0; i < roster.Count; i++)
            {
                CharacterController ally = roster[i];
                if (ally == null || ally == observer || ally == subject || ally.Stats == null || ally.Stats.IsDead)
                    continue;
                if (!TeamUtility.IsAlly(observer, ally) || ally.Stats.CurrentHP * 10 >= ally.Stats.TotalMaxHP * 7)
                    continue;
                if (threatened == null)
                    threatened = ThreatSystem.GetThreatenedSquares(subject, log: false);
                if (ThreatensAnySquare(threatened, ally) && !ResilientSphereAreaEffect.DoesSphereBlockInteraction(subject, ally))
                    return true;
            }
            return false;
        }

        /// <summary>True when an ally of <paramref name="observer"/> (not the observer), standing (not down), is adjacent to <paramref name="subject"/>.</summary>
        public static bool EngagedByAlly(CharacterController observer, CharacterController subject, List<CharacterController> roster)
        {
            if (observer == null || subject == null || roster == null)
                return false;
            for (int i = 0; i < roster.Count; i++)
            {
                CharacterController ally = roster[i];
                if (ally == null || ally == observer || ally == subject || ally.Stats == null || ally.Stats.IsDead || ally.IsUnconscious)
                    continue;
                if (TeamUtility.IsAlly(observer, ally) && ally.GetMinimumDistanceToTarget(subject, chebyshev: true) <= 1)
                    return true;
            }
            return false;
        }

        /// <summary>How many foes of <paramref name="observer"/> other than <paramref name="subject"/>, not dead, are within 2 squares of it (PHB p.147 diagonals).</summary>
        public static int FoesNear(CharacterController observer, CharacterController subject, List<CharacterController> roster)
        {
            if (observer == null || subject == null || roster == null)
                return 0;
            int count = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                CharacterController foe = roster[i];
                if (foe == null || foe == observer || foe == subject || foe.Stats == null || foe.Stats.IsDead)
                    continue;
                if (TeamUtility.IsEnemy(observer, foe) && subject.GetMinimumDistanceToTarget(foe, chebyshev: false) <= 2)
                    count++;
            }
            return count;
        }
    }
}
