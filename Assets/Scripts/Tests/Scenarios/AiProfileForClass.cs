#if UNITY_EDITOR
using DND35.AI;
using DND35.AI.Profiles;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Picks an AI profile for a PC-slot actor the harness runs through the AI (the party has no profiles).
    /// Instances are created like GameManager.BuildRuntimeAIProfile does (ScriptableObject.CreateInstance);
    /// the runner destroys them when the job ends.
    /// Cleric and Druid: Healer. Wizard and Sorcerer: Evoker with a damage spell prepared or known, else Spellcaster.
    /// Ranger with a ranged weapon in hand: Ranged. Anything else: Humanoid.
    /// </summary>
    public static class AiProfileForClass
    {
        public static AIProfile Create(CharacterController pc)
        {
            string cls = pc != null && pc.Stats != null ? pc.Stats.CharacterClass : null;
            switch (cls)
            {
                case "Cleric":
                case "Druid":
                    return ScriptableObject.CreateInstance<HealerAIProfile>();
                case "Wizard":
                case "Sorcerer":
                    return HasDamageSpell(pc)
                        ? ScriptableObject.CreateInstance<EvokerAIProfile>()
                        : (AIProfile)ScriptableObject.CreateInstance<SpellcasterAIProfile>();
                case "Ranger":
                    ItemData weapon = pc.GetEquippedMainWeapon();
                    if (weapon != null && weapon.IsRangedWeapon)
                        return ScriptableObject.CreateInstance<RangedAIProfile>();
                    return ScriptableObject.CreateInstance<HumanoidAIProfile>();
                default:
                    return ScriptableObject.CreateInstance<HumanoidAIProfile>();
            }
        }

        private static bool HasDamageSpell(CharacterController pc)
        {
            SpellcastingComponent sc = pc.Spellcasting;
            if (sc == null)
                return false;
            if (sc.PreparedSpells != null)
                foreach (SpellData s in sc.PreparedSpells)
                    if (s != null && s.EffectType == SpellEffectType.Damage)
                        return true;
            if (pc.Stats.CharacterClass == "Sorcerer" && sc.KnownSpells != null)
                foreach (SpellData s in sc.KnownSpells)
                    if (s != null && s.EffectType == SpellEffectType.Damage && s.SpellLevel > 0)
                        return true;
            return false;
        }
    }
}
#endif
