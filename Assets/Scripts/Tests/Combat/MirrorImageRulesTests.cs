using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Combat
{
    /// <summary>
    /// Runtime regression checks for the tactical Mirror Image implementation.
    /// Run manually via MirrorImageRulesTests.RunAll().
    /// </summary>
    public static class MirrorImageRulesTests
    {
        private static int _passed;
        private static int _failed;

        public static void mirror_image_rules_test() => RunAll();

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== MIRROR IMAGE RULES TESTS ======");
            RaceDatabase.Init();
            ClassRegistry.Init();
            SpellDatabase.Init();

            TestSpellDefinitionMatchesTacticalImplementation();
            TestAiPrioritySelectsNearestMirrorImageEntity();
            TestCloneAttackDissipatesWithoutDamage();

            Debug.Log($"====== Mirror Image Rules Results: {_passed} passed, {_failed} failed ======");
        }

        private static void Assert(bool condition, string testName, string detail = "")
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"  PASS: {testName}");
            }
            else
            {
                _failed++;
                Debug.LogError($"  FAIL: {testName} {detail}");
            }
        }

        private static CharacterController CreateController(string name, CharacterTeam team, Vector2Int pos)
        {
            CharacterStats stats = new CharacterStats(
                name: name,
                level: 5,
                characterClass: "Wizard",
                str: 10,
                dex: 14,
                con: 12,
                wis: 10,
                intelligence: 16,
                cha: 10,
                bab: 2,
                armorBonus: 0,
                shieldBonus: 0,
                damageDice: 6,
                damageCount: 1,
                bonusDamage: 0,
                baseSpeed: 6,
                atkRange: 1,
                baseHitDieHP: 20,
                raceName: "Human");

            GameObject go = new GameObject($"MirrorImageTest_{name}");
            CharacterController controller = go.AddComponent<CharacterController>();
            controller.Init(stats, pos, null, null);
            controller.ConfigureTeamControl(team, controllable: team == CharacterTeam.Player);
            return controller;
        }

        private static void DestroyController(CharacterController controller)
        {
            if (controller != null)
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        private static void TestSpellDefinitionMatchesTacticalImplementation()
        {
            SpellData spell = SpellDatabase.GetSpell(SpellNames.MIRROR_IMAGE);
            Assert(spell != null, "Mirror Image spell definition exists");
            if (spell == null)
                return;

            Assert(spell.SpellLevel == 2, "Mirror Image is level 2");
            Assert(spell.TargetType == SpellTargetType.Self, "Mirror Image target is self/personal");
            Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
                "Mirror Image duration is 1 minute/level");
            Assert(spell.IsAvailableFor("Wizard", 2), "Mirror Image available to Wizard 2");
            Assert(spell.IsAvailableFor("Sorcerer", 2), "Mirror Image available to Sorcerer 2");
            Assert(spell.IsAvailableFor("Bard", 2), "Mirror Image available to Bard 2");
        }

        private static void TestAiPrioritySelectsNearestMirrorImageEntity()
        {
            GameObject gmObj = new GameObject("MirrorImageTestGM_AI");
            GameManager gm = gmObj.AddComponent<GameManager>();

            CharacterController attacker = null;
            CharacterController caster = null;
            CharacterController cloneNear = null;
            CharacterController cloneFar = null;

            try
            {
                attacker = CreateController("Attacker", CharacterTeam.Enemy, new Vector2Int(0, 0));
                caster = CreateController("Caster", CharacterTeam.Player, new Vector2Int(6, 6));
                cloneNear = CreateController("CloneNear", CharacterTeam.Player, new Vector2Int(1, 0));
                cloneFar = CreateController("CloneFar", CharacterTeam.Player, new Vector2Int(5, 5));

                cloneNear.gameObject.AddComponent<MirrorImageClone>().Initialize(caster, 1);
                cloneFar.gameObject.AddComponent<MirrorImageClone>().Initialize(caster, 2);

                RegisterMirrorImageStateForTest(gm, caster, new List<CharacterController> { cloneNear, cloneFar });

                CharacterController selected = gm.GetMirrorImagePriorityTargetForAI(attacker);
                Assert(selected == cloneNear, "AI selects nearest mirror image entity", $"selected={selected?.name}");
            }
            finally
            {
                DestroyController(attacker);
                DestroyController(caster);
                DestroyController(cloneNear);
                DestroyController(cloneFar);
                UnityEngine.Object.DestroyImmediate(gmObj);
            }
        }

        private static void TestCloneAttackDissipatesWithoutDamage()
        {
            GameObject gmObj = new GameObject("MirrorImageTestGM_Dissipate");
            GameManager gm = gmObj.AddComponent<GameManager>();

            CharacterController attacker = null;
            CharacterController caster = null;
            CharacterController clone = null;

            try
            {
                attacker = CreateController("Attacker", CharacterTeam.Enemy, new Vector2Int(0, 0));
                caster = CreateController("Caster", CharacterTeam.Player, new Vector2Int(3, 3));
                clone = CreateController("Clone", CharacterTeam.Player, new Vector2Int(1, 0));
                clone.gameObject.AddComponent<MirrorImageClone>().Initialize(caster, 1);

                RegisterMirrorImageStateForTest(gm, caster, new List<CharacterController> { clone });

                CombatResult baseResult = new CombatResult
                {
                    Attacker = attacker,
                    Defender = clone,
                    Hit = false,
                    Damage = 7,
                    FinalDamageDealt = 7
                };

                bool intercepted = gm.TryHandleMirrorImageCloneAttacked(attacker, clone, baseResult, out CombatResult resolved);
                MirrorImageClone marker = clone.GetComponent<MirrorImageClone>();

                Assert(intercepted, "Mirror image clone attack is intercepted");
                Assert(resolved != null && resolved.Hit, "Resolved clone attack counts as a hit on image");
                Assert(resolved != null && resolved.FinalDamageDealt == 0, "Clone attack deals zero damage");
                Assert(marker != null && marker.IsDissipated, "Clone marked dissipated after attack");
            }
            finally
            {
                DestroyController(attacker);
                DestroyController(caster);
                DestroyController(clone);
                UnityEngine.Object.DestroyImmediate(gmObj);
            }
        }

        private static void RegisterMirrorImageStateForTest(GameManager gm, CharacterController caster, List<CharacterController> clones)
        {
            Type gmType = typeof(GameManager);
            Type stateType = gmType.GetNestedType("MirrorImageState", BindingFlags.NonPublic);
            object state = Activator.CreateInstance(stateType, nonPublic: true);

            FieldInfo casterField = stateType.GetField("Caster", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo clonesField = stateType.GetField("Clones", BindingFlags.Public | BindingFlags.Instance);
            casterField?.SetValue(state, caster);

            var stateClones = clonesField?.GetValue(state) as IList;
            if (stateClones != null && clones != null)
            {
                for (int i = 0; i < clones.Count; i++)
                    stateClones.Add(clones[i]);
            }

            FieldInfo statesField = gmType.GetField("_mirrorImageStates", BindingFlags.NonPublic | BindingFlags.Instance);
            var states = statesField?.GetValue(gm) as IDictionary;
            states?.Add(caster, state);

            FieldInfo mapField = gmType.GetField("_mirrorImageCloneToCaster", BindingFlags.NonPublic | BindingFlags.Instance);
            var map = mapField?.GetValue(gm) as IDictionary;
            if (map != null && clones != null)
            {
                for (int i = 0; i < clones.Count; i++)
                    map[clones[i]] = caster;
            }
        }
    }
}
