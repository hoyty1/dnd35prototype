> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Enemy AI: action coverage matrix

Part of [AI.md](AI.md) (section 9). It lists which 3.5e combat options the enemy AI uses today compared with what player characters can do. Statuses were established by reading code, not in Play mode. Issue IDs link to `docs/issues/`.


Status: **Used** = the AI chooses it; **Partial** = some routines or profiles, or degraded; **Auto** = happens in shared resolution with no AI choice; **Never**. "PC" = available to player characters. The rows cover every PC combat control wired in the UI (the `GameManager.On*ButtonPressed` handlers, the Power Attack slider, the Rapid Shot and damage-mode toggles, the special-attack menu and the grapple menu) and the PHB ch.8 actions that no one can take yet. When a PC control is added, add its row here.

| Option | AI | Where / conditions | PC | Notes |
|---|---|---|---|---|
| Move | Used | every routine, `EvaluateMovementOptions`, one move action, only while `HasMoveAction` | Yes | provokes AoOs like PC movement; AI-052, AI-019 |
| Double move | Never | AggressiveMelee wastes the standard action when still out of reach | Yes | gap |
| Run (×4) | Never | Frightened "run" is 1× (it provokes like any move) | No | game-wide gap (PHB ch.8) |
| 5-foot step | Partial | kiter AoO avoidance; mid full attack (Animal, Dragon, UndeadTactical, UndeadIncorporeal) | Yes | never "step then full attack" |
| Withdraw | Partial | DefensiveMelee <30% HP; Frightened | Yes | AI-010 |
| Charge | Used | Aggressive, Defensive, Dragon via `ShouldNPCCharge` + `ShouldPreferCharge`; pounce | Yes | Morale-typed +2; CMB-018 |
| Seek flanking | Used | +3 movement term if `SeekFlanking` | Manual | flankers get +2 on melee attacks (RAW); no AC penalty on the target |
| Cover, terrain, hazards | Never | `UseCover` unread; no cover system | No | paths through walls of fire, clouds, webs (inferred) |
| Stand up / drop prone / crawl | Partial | stands up at turn start when prone (`TryStandUpFromProneForAI`, move action, provokes; charmed NPCs too, not confused ones); never drops prone or crawls. Prone creatures get no ordinary movement (`GetCurrentMoveRangeSquares` is 0) | Yes | CMB-074 |
| Escape entangle | Auto | `TryExecuteAnimateRopeEscapeForNpc`, standard action | Yes | CMB-055 |
| Break Wall of Ice | Used | only when blocked and out of reach | Yes | |
| Single attack | Used | after moving | Yes | |
| Full attack | Used | only if the NPC has not moved; adaptive retarget for some profiles; same attack modifier as a single attack (shared builder, CMB-043 fixed) | Yes | CMB-044 |
| Two-weapon, off-hand, off-hand thrown | Never | no NPC dual-wield | Yes | AI-055 (off-hand math: CMB-008) |
| Natural attacks | Auto | used only when no manufactured weapon is equipped; no choice between natural attacks | Yes (picks each natural attack) | CMB-077 |
| Natural + weapon combination | Never | naturals only with no weapon | n/a | CMB-077 |
| Ranged attack | Used | any routine with a ranged weapon equipped; kiter manages AoO risk | Yes | ITM-004 strips many bows |
| Thrown weapons | Partial | only `WeaponCat == Ranged` items | Yes | CMB-019 |
| Switch weapons, draw, pick up, drop held item | Never | only the UndeadMindless free re-equip; disarmed NPCs never re-arm | Partial (pick up, drop) | ITM-005 |
| Reload | Partial | forced when unloaded; ends the turn | Yes | CMB-032 |
| Power Attack, Rapid Shot | Never | `UsePowerAttack` unread; no NPC caller of `SetRapidShot` | Yes | AI-026 (Power Attack), AI-055 (Rapid Shot) |
| Combat Expertise | Never | never written | No | CHR-021, AI-031 |
| Fight defensively | Partial | cornered Frightened only | Yes | UI-007 |
| Total defense | Never | no code | No | gap |
| Cleave | Never | PC path only | Yes | CMB-017 |
| Sneak attack | Auto | damage path; AI seeks flanks, not sneak attack | Auto | CMB-028 |
| Nonlethal | Never | | Yes | |
| Take AoOs | Auto | `ThreatSystem.ExecuteAoO` when something provokes, before the mover leaves the square | Auto | CMB-084 |
| Avoid provoking: movement | Partial | path scoring | | |
| Avoid provoking: ranged | Used | kiter risk model | Prompt | |
| Avoid provoking: casting | Used | adjacency estimate picks or drops the spell; at cast time `ShouldCastDefensively` picks defensive (rolled) or normal (AoOs rolled) | Prompt + roll | |
| Trip | Used | Humanoid, Berserk, Grappler, null profile, `HasTripAttack` summons; free trip on hit | Yes | AI-035, CMB-079, CMB-085; 11.8.4 |
| Disarm | Used | Humanoid, Grappler, null profile (STR ≥3) | Yes | never picks weapons up; 11.8.4 |
| Trip or disarm within a full attack | Used | the NPC melee attack runs step by step and the maneuver evaluation may replace any weapon step at that step's BAB; the evaluation re-runs before each step, so profiles chain trip, disarm and grapple and attack only when none applies; after a grapple start the remaining steps become grapple actions (CMB-102, 2026-10-07). Natural-attack sequences substitute only at the first step and then make their remaining natural attacks; no charge or AoO substitutes | Yes (per-creature attack sequence: standard action first, full attack from the second step) | CMB-102, AI-035 |
| Sunder, bull rush (attack or charge), overrun, feint, Improved Feint | Never | flags never set; the NPC executor supports the standard and the charge bull rush (`TryNPCSpecialAttackIfBeneficial`, `NPCExecuteCharge(..., bullRush)` through `ShouldNPCChargeBullRush`) but nothing chooses them; feint has no AI path | Yes | AI-014, CMB-015 |
| Aid another (+2 attack or AC, or wake a sleeping ally) | Never | `UseAidAnother` unread; executor is PC-bound | Yes | AI-026, AI-054; 11.8.1 |
| Coup de grace | Used | profiles or data override, helpless adjacent | Yes | provokes from all threatening enemies, as for PCs; CMB-004 |
| Grapple start / in-grapple | Used | 8.6 | Yes | CMB-033, CMB-083 |
| Grapple escape | Partial | animals <25% HP only in practice | Yes | CMB-075, AI-029 |
| Improved Grab | Auto | free on hit | Prompt | |
| Constrict, blood drain, swallow whole, trample | Never | text or unread data | n/a | CRE-031, CRE-018 |
| Breath weapon | Partial | Dragon profile, once per spawn | n/a | AI-032, AI-033, AI-040, AI-008 |
| Gaze / aura | Used | `ProcessAuraAbility` | n/a | AI-013, CRE-012, CORE-002 |
| Frightful presence | Auto | first attack or breath | n/a | AI-041 |
| Engulf, spittle, bombardier spray | Used | 8.4 | n/a | AI-006, CMB-024 |
| Spell-like abilities | Never | no system | n/a | CRE-015, CRE-017 |
| Turn undead | Never | PC menu flow; NPC undead that are turned lose their turns | Yes (turn only; no rebuke, command or bolster) | CMB-016, CMB-026, CMB-075, AI-054; 11.8.6, 11.8.7 |
| Rage, Flurry of Blows | Never | PC-only buttons; the rules cores work for any actor | Yes | CHR-003; 11.8.7 |
| Bardic music | Never | no database bard; DMG Bard spawns get the Spellcaster profile | Yes (Inspire Courage only) | CHR-026; 11.8.7 |
| Paladin Smite Evil | Never | text in `SpecialAbilities` only | **No**: `SmiteEvilData` is never assigned, and the Smite button runs template smite only and is visible only to characters with template smite, so a PC paladin has no smite control (UI/Combat/ActionButtonPanel.cs:692-698) | CHR-020, CRE-002; 11.8.7 |
| Template smite (celestial or fiendish template) | Partial | enemy-side summons only, once (5.7) | Yes (Smite button; only controllable templated creatures, such as the template-test allies) | CMB-021, CMB-003 (damage bonus lost), CRE-002 |
| Destruction domain smite | Never | NPCs have no domains | Yes (Domain Power button; the +4 attack applies, the damage bonus never does) | CHR-005; 11.8.7 |
| Domain powers | Never | NPCs have no domains | Yes (Strength, Destruction, Death, Sun, Travel, Plant, Luck, elemental turning) | CHR-023, CHR-062 |
| Lay on Hands, Wild Shape, Favored Enemy and other data-only class features | Never | | No | CHR-020, CHR-053 |
| Single-target damage/debuff/control spell | Used | kiter, dragon, healer | Yes | SPL-005, AI-046 |
| Area spells, metamagic | Never | refused at cast | Yes | AI-001; 11.8.2 |
| Heal, buff | Partial | adjacent or self only | Yes | AI-047; 11.8.6 |
| Spontaneous cure/inflict conversion | Never | PC cast path only (GameManager.SpellCasting.cs:193) | Yes | 11.8.6 |
| Hold the charge, discharge a held touch spell | Never | NPC touch spells resolve at once against an adjacent target | Yes | |
| Dismiss a spell | Never | | Yes (Invisibility, See Invisibility, Expeditious Retreat, Disguise Self, Jump) | |
| Direct a Flaming Sphere | Never | `TryControlFlamingSphereForAI` has no callers | Yes | AI-053 |
| Cast an imbued spell (Imbue with Spell Ability) | Never | | Yes | |
| Summon, dispel, escape spells | Never effective | slot spent, no effect | Yes | SPL-091 |
| Full-round casting time | Ignored | always a standard action | Yes | |
| Counterspell, ready, delay | Never | | No | SPL-047, CMB-029 |
| Potions, wands, scrolls | Never | charmed-heals-charmer exception | Yes | AI-009, ITM-005, AI-054; 11.8.3 |
| Rods, staves, activated rings and wondrous items | Never | | No (unreachable for PCs too) | ITM-018, ITM-019, ITM-024 |
| Whirlwind Attack, Manyshot, Stunning Fist, Spring Attack, Shot on the Run, Tumble, mounted combat | Never | | No (unusable for PCs too) | CMB-022, CMB-023, CMB-027, CMB-031 |
| Invisible/concealed targets | Used | tiers, Listen, last-known attacks | Manual | AI-007, AI-021, GRID-009 |
| Retreat, morale, surrender | Partial | 8.9 | n/a | AI-010; 11.8.5 |
| Protect allies, focus fire | Never | each NPC scores alone | n/a | gap |

**PC can, NPC cannot:** double move; free 5-foot steps (including step then full attack); drop prone, crawl; Power Attack; Rapid Shot; fighting defensively by choice; two-weapon and off-hand attacks; throw melee throwables; Cleave; Flurry, Rage, Bardic Music, Turn Undead, domain powers (including the Destruction smite); template smite outside summons; sunder, bull rush, overrun, feint; aid another and waking allies; trip or disarm as part of a full attack; spontaneous cure/inflict conversion; escape a grapple while not pinned; decline Improved Grab; area spells and metamagic; summoning; holding a touch charge; controlling a Flaming Sphere (`TryControlFlamingSphereForAI` has no callers); dismissing spells; imbued spells; potions, wands, scrolls; picking up or dropping items; weapon swaps; nonlethal damage.

**NPC-only advantages (all bugs or gaps):** breath and specials cost no action (AI-040, AI-053); melee routines path to an invisible target's true square (AI-051); monster ranged specials skip mitigation (AI-006); silenced NPCs can cast (SPL-092).
