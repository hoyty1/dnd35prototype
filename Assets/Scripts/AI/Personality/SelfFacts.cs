using System.Collections.Generic;
using DND35.AI.Knowledge;

namespace DND35.AI.Personality
{
    /// <summary>
    /// The creature's own state a yes/no vote reads (batch 14 step 5; docs/designs/personalities_batch14.md C5): the
    /// conditions of the opinion rows, built into a <see cref="JudgeContext"/> from the creature itself and the space
    /// around it. It reads the creature's own hit points and reach; the foes it counts are read only for their positions
    /// and whether they are down, through the knowledge view where a fact exists. AiBlend calls it only for a creature
    /// with a personality, so a creature with no traits never pays for it. OmniscienceLintTests skips this file (the
    /// creature's own state). Reads only: no roll, no log.
    /// </summary>
    public static class SelfFacts
    {
        /// <summary>
        /// The vote context of <paramref name="self"/>: its own wound band (KnowledgeFacts.WoundBandOf); whether a foe
        /// threatens its space (as ThreatSystem.GetThreateningEnemies: KnowledgeFacts.ThreatensSpaceOf); whether a standing
        /// foe (not Down) is in its own reach (it threatens that foe's space). <paramref name="roster"/> is the fight's
        /// creatures (null: GameManager.GetAllCharactersForAI, none without a GameManager). The difficulty is left null, so
        /// AiBlend fills it from the setting.
        /// </summary>
        public static JudgeContext JudgeContextOf(CharacterController self, List<CharacterController> roster = null)
        {
            var ctx = new JudgeContext();
            if (self == null || self.Stats == null)
                return ctx;
            ctx.SelfWound = KnowledgeFacts.WoundBandOf(self.Stats.CurrentHP, self.Stats.TotalMaxHP);

            if (roster == null)
            {
                GameManager gm = GameManager.Instance;
                roster = gm != null ? gm.GetAllCharactersForAI() : null;
            }
            if (roster == null)
                return ctx;

            for (int i = 0; i < roster.Count; i++)
            {
                CharacterController foe = roster[i];
                if (foe == null || foe == self || foe.Stats == null || foe.Stats.IsDead || !TeamUtility.IsEnemy(self, foe))
                    continue;
                if (!ctx.EnemyThreatensSelf && KnowledgeFacts.ThreatensSpaceOf(foe, self))
                    ctx.EnemyThreatensSelf = true;
                if (!ctx.StandingThreatInReach && !IsDown(self, foe) && KnowledgeFacts.ThreatensSpaceOf(self, foe))
                    ctx.StandingThreatInReach = true;
                if (ctx.EnemyThreatensSelf && ctx.StandingThreatInReach)
                    break;
            }
            return ctx;
        }

        private static bool IsDown(CharacterController self, CharacterController foe)
        {
            FactValue down = KnowledgeService.ViewOf(self, foe).Get(FactKey.Down);
            return down.IsKnown && down.Flag;
        }
    }
}
