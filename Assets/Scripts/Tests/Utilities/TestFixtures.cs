using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tests.Utilities
{
    /// <summary>
    /// Lightweight runtime fixture pattern for test scripts that do not use NUnit.
    /// </summary>
    public abstract class D35TestBase : IDisposable
    {
        protected readonly List<UnityEngine.Object> TrackedObjects = new List<UnityEngine.Object>();

        protected GameManager gameManager;

        public virtual void Setup()
        {
            TestHelpers.EnsureCoreDatabasesInitialized();

            var gmObject = new GameObject("D35Test_GameManager");
            gameManager = gmObject.AddComponent<GameManager>();
            Track(gmObject);
        }

        public virtual void Teardown()
        {
            for (int i = TrackedObjects.Count - 1; i >= 0; i--)
            {
                if (TrackedObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(TrackedObjects[i]);
            }

            TrackedObjects.Clear();
            gameManager = null;
        }

        public void Dispose()
        {
            Teardown();
        }

        protected T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null)
                TrackedObjects.Add(obj);

            return obj;
        }
    }

    /// <summary>
    /// Shared setup pattern for combat-oriented runtime tests.
    /// </summary>
    public abstract class CombatTestBase : D35TestBase
    {
        protected CharacterController attacker;
        protected CharacterController defender;

        public override void Setup()
        {
            base.Setup();

            attacker = TestHelpers.CreateWarrior("Attacker");
            defender = TestHelpers.CreateWarrior("Defender");

            Track(attacker.gameObject);
            Track(defender.gameObject);

            TestHelpers.SetGridPosition(attacker, 0, 0);
            TestHelpers.SetGridPosition(defender, 1, 0);
        }

        public override void Teardown()
        {
            attacker = null;
            defender = null;
            base.Teardown();
        }
    }
}
