using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_J()
    {
        RegisterJanni();
    }

    private static void RegisterJanni()
    {
        Register(new NPCDefinition
        {
            Id = "janni",
            Name = "Janni",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 15, CON = 12, WIS = 15, INT = 14, CHA = 13,
            NaturalArmorBonus = 1,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            BaseSpeed = 4, // 20 ft (armor), fly 20 ft (perfect)
            BaseHitDieHP = 33,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Outsider", "Native", "Fly20", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Mobility" },
            SpecialAbilities = new List<string> { "Elemental Endurance (Ex): survive on Elemental Planes", "Change Size (Sp): 2/day, enlarge/reduce", "Invisibility (Sp): 3/day", "Speak with Animals (Sp): 3/day", "Create Food and Water (Sp): 1/day", "Ethereal Jaunt (Sp): 1/day", "Resist fire 10", "Telepathy 100 ft.", "Plane Shift (Sp): at will", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("longbow", EquipSlot.Ranged),
                new EquipmentSlotPair("chainmail", EquipSlot.Armor)
            },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.6f, 0.55f, 0.5f, 1f),
            PanelColor = new Color(0.22f, 0.2f, 0.18f, 0.85f),
            NameColor = new Color(0.85f, 0.8f, 0.72f),
            Description = "Janni (CR 4). Weakest genie, native to Material Plane. MM 3.5e p.116."
        });
    }
}
