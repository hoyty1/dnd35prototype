using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: G
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_G()
    {
        RegisterGoblin();
    }

    private static void RegisterGoblin()
    {
        Register(new NPCDefinition
        {
            Id = "goblin",
            Name = "Goblin",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 1,
            BaseAttackBonusOverride = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 11, DEX = 13, CON = 12, WIS = 9, INT = 10, CHA = 6,
            BaseSpeed = 6, // 30 ft.
            BaseHitDieHP = 5,
            CreatureTags = new List<string> { "Goblinoid", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string>
            {
                "Darkvision 60 ft",
                "Usually neutral evil",
                "Skills: Hide +5, Listen +2, Move Silently +5, Ride +4, Spot +2",
                "Attack: Morningstar +2 melee (1d6) or javelin +3 ranged (1d4, 30 ft.)"
            },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("leather_armor", EquipSlot.Armor),
                new EquipmentSlotPair("morningstar", EquipSlot.RightHand),
                new EquipmentSlotPair("shield_light_wooden", EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.56f, 0.78f, 0.28f, 1f),
            PanelColor = new Color(0.33f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.45f, 0.45f),
            Description = "Monster Manual goblin. Small goblinoid skirmisher with shield, morningstar, and javelin."
        });
    }
}
