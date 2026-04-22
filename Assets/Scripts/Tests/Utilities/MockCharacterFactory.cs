using UnityEngine;

namespace Tests.Utilities
{
    /// <summary>
    /// Factory presets for common test actors.
    /// </summary>
    public static class MockCharacterFactory
    {
        public static CharacterController CreateCommoner(string name = "Commoner")
        {
            return TestHelpers.CreateCharacter(
                name: name,
                characterClass: "Commoner",
                level: 1,
                str: 10,
                dex: 10,
                con: 10,
                wis: 10,
                intelligence: 10,
                cha: 10,
                bab: 0);
        }

        public static CharacterController CreateFighter(string name = "Fighter", int level = 5)
        {
            return TestHelpers.CreateWarrior(name, level);
        }

        public static CharacterController CreateSkeleton(string name = "Skeleton")
        {
            var skeleton = TestHelpers.CreateCharacter(
                name: name,
                characterClass: "Fighter",
                level: 1,
                str: 13,
                dex: 13,
                con: 10,
                wis: 10,
                intelligence: 6,
                cha: 10,
                bab: 1);

            skeleton.IsPlayerControlled = false;
            return skeleton;
        }

        public static CharacterController CreateGoblin(string name = "Goblin")
        {
            var goblin = TestHelpers.CreateCharacter(
                name: name,
                characterClass: "Rogue",
                level: 1,
                str: 11,
                dex: 15,
                con: 12,
                wis: 9,
                intelligence: 10,
                cha: 6,
                bab: 0);

            goblin.IsPlayerControlled = false;
            return goblin;
        }
    }
}
