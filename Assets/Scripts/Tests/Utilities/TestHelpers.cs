using UnityEngine;

namespace Tests.Utilities
{
    /// <summary>
    /// Shared helper methods for runtime test scripts.
    /// </summary>
    public static class TestHelpers
    {
        public static void EnsureCoreDatabasesInitialized()
        {
            RaceDatabase.Init();
            ClassRegistry.Init();
            ItemDatabase.Init();
            FeatDefinitions.Init();
            SpellDatabase.Init();
        }

        public static CharacterStats CreateStats(
            string name = "Test Character",
            int level = 3,
            string characterClass = "Fighter",
            int str = 14,
            int dex = 12,
            int con = 12,
            int wis = 10,
            int intelligence = 10,
            int cha = 10,
            int bab = 3,
            string raceName = "Human")
        {
            int safeLevel = Mathf.Max(1, level);
            int derivedHitDieHp = Mathf.Max(1, (safeLevel * 4) + ((con - 10) / 2));

            return new CharacterStats(
                name,
                safeLevel,
                characterClass,
                str,
                dex,
                con,
                wis,
                intelligence,
                cha,
                bab,
                0,
                0,
                8,
                1,
                0,
                6,
                1,
                derivedHitDieHp,
                raceName);
        }

        public static CharacterController CreateCharacter(
            string name = "Test Character",
            string characterClass = "Fighter",
            int level = 3,
            int str = 14,
            int dex = 12,
            int con = 12,
            int wis = 10,
            int intelligence = 10,
            int cha = 10,
            int bab = 3,
            Vector2Int? gridPosition = null)
        {
            EnsureCoreDatabasesInitialized();

            var go = new GameObject($"{name}_TestGO");
            var controller = go.AddComponent<CharacterController>();
            var inventory = go.AddComponent<InventoryComponent>();

            CharacterStats stats = CreateStats(
                name: name,
                level: level,
                characterClass: characterClass,
                str: str,
                dex: dex,
                con: con,
                wis: wis,
                intelligence: intelligence,
                cha: cha,
                bab: bab);

            controller.Init(stats, gridPosition ?? Vector2Int.zero, null, null);
            inventory.Init(stats);
            return controller;
        }

        public static CharacterController CreateWarrior(string name = "Test Warrior", int level = 5)
        {
            int safeLevel = Mathf.Max(1, level);
            return CreateCharacter(
                name: name,
                characterClass: "Fighter",
                level: safeLevel,
                str: 18,
                dex: 14,
                con: 14,
                wis: 10,
                intelligence: 10,
                cha: 10,
                bab: safeLevel);
        }

        public static CharacterController CreateRogue(string name = "Test Rogue", int level = 5)
        {
            int safeLevel = Mathf.Max(1, level);
            return CreateCharacter(
                name: name,
                characterClass: "Rogue",
                level: safeLevel,
                str: 12,
                dex: 18,
                con: 12,
                wis: 10,
                intelligence: 14,
                cha: 12,
                bab: Mathf.Max(0, safeLevel - 1));
        }

        public static CharacterController CreateCleric(string name = "Test Cleric", int level = 5)
        {
            int safeLevel = Mathf.Max(1, level);
            return CreateCharacter(
                name: name,
                characterClass: "Cleric",
                level: safeLevel,
                str: 14,
                dex: 10,
                con: 14,
                wis: 18,
                intelligence: 10,
                cha: 14,
                bab: Mathf.Max(0, safeLevel - 1));
        }

        public static void SetGridPosition(CharacterController character, int x, int y)
        {
            if (character == null)
                return;

            character.GridPosition = new Vector2Int(x, y);
            character.transform.position = new Vector3(x * 5f, 0f, y * 5f);
        }

        public static float GetDistance(CharacterController a, CharacterController b)
        {
            if (a == null || b == null)
                return 0f;

            return Vector3.Distance(a.transform.position, b.transform.position);
        }

        public static void ResetActions(CharacterController character)
        {
            if (character == null)
                return;

            character.Actions.Reset();
        }

        public static void Cleanup(params Object[] objects)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
