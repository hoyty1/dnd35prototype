using System.Collections.Generic;

namespace DND35.AI
{
    /// <summary>
    /// Who hurt this creature since the start of its previous turn, and by how much (batch 14 step 2,
    /// docs/designs/personalities_batch14.md: the DamagedObserver fact the HurtMe consideration reads, "it damaged me since
    /// my last turn"). It is AI knowledge, not a rule: no rule reads it, and writing it rolls no dice and logs nothing.
    /// <list type="bullet">
    /// <item>Two windows: <c>previous</c> holds what the creature took between its previous turn start and its latest
    /// one, <c>current</c> what it took since its latest turn start. The reads sum both, so on its own turn the creature
    /// remembers everything since its previous turn started: the hits taken between its turns and the ongoing damage of
    /// this turn's start (Melf's Acid Arrow, a Flaming Sphere: GameManager.BeginCombatantTurn deals it before
    /// CharacterController.StartNewTurn rolls the windows over).</item>
    /// <item>Writers: GameManager.OnAttackResolved (each hit of a resolved attack step that dealt damage, after
    /// mitigation), GameManager.DealDamage (non-weapon damage taken from the packet's source creature, else the caster of
    /// the resolving cast) and SpellCaster.Cast, the per-target spell damage step of single-target casts, the generic area
    /// loop, Sound Burst and Flaming Sphere impacts (its caster), which does not go through DealDamage. Damage applied by
    /// other direct ApplyIncomingDamage or ApplyDamagePacket callers is not recorded yet (AI-085).</item>
    /// <item><see cref="RollOver"/> at the creature's turn start (CharacterController.StartNewTurn); <see cref="Clear"/>
    /// when its slot gets a new creature (CharacterController.ResetForNewCreature, CRE-046) and when the encounter resets
    /// (GameManager.ResetCombatStateForNextEncounter).</item>
    /// </list>
    /// Owned by each creature (CharacterController.DamageMemory), PC and NPC alike.
    /// </summary>
    public sealed class AiDamageMemory
    {
        private readonly Window _previous = new Window();
        private readonly Window _current = new Window();

        /// <summary>How many creatures hurt this one since its previous turn started.</summary>
        public int Count
        {
            get
            {
                int n = _current.Sources.Count;
                for (int i = 0; i < _previous.Sources.Count; i++)
                    if (!_current.Sources.Contains(_previous.Sources[i]))
                        n++;
                return n;
            }
        }

        /// <summary>Forgets every entry of both windows (a new creature in the slot, a new encounter).</summary>
        public void Clear()
        {
            _previous.Clear();
            _current.Clear();
        }

        /// <summary>
        /// The creature's turn starts: what it took since its latest turn start becomes the previous window, and the
        /// window before that is forgotten.
        /// </summary>
        public void RollOver()
        {
            _previous.Clear();
            _previous.Sources.AddRange(_current.Sources);
            _previous.Damage.AddRange(_current.Damage);
            _current.Clear();
        }

        /// <summary>
        /// Records <paramref name="amount"/> points of damage taken from <paramref name="source"/>. Nothing is recorded
        /// for no source, no damage, or damage the creature dealt itself.
        /// </summary>
        public void Record(CharacterController owner, CharacterController source, int amount)
        {
            if (source == null || amount <= 0 || source == owner)
                return;
            _current.Add(source, amount);
        }

        /// <summary>True when <paramref name="source"/> hurt this creature since its previous turn started.</summary>
        public bool WasDamagedBy(CharacterController source)
            => source != null && (_current.Sources.Contains(source) || _previous.Sources.Contains(source));

        /// <summary>The damage <paramref name="source"/> dealt this creature since its previous turn started (0 when none).</summary>
        public int DamageFrom(CharacterController source)
            => source != null ? _current.DamageFrom(source) + _previous.DamageFrom(source) : 0;

        private sealed class Window
        {
            public readonly List<CharacterController> Sources = new List<CharacterController>();
            public readonly List<int> Damage = new List<int>();

            public void Clear()
            {
                Sources.Clear();
                Damage.Clear();
            }

            public void Add(CharacterController source, int amount)
            {
                int i = Sources.IndexOf(source);
                if (i >= 0)
                    Damage[i] += amount;
                else
                {
                    Sources.Add(source);
                    Damage.Add(amount);
                }
            }

            public int DamageFrom(CharacterController source)
            {
                int i = Sources.IndexOf(source);
                return i >= 0 ? Damage[i] : 0;
            }
        }
    }
}
