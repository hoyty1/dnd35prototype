#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Scenarios
{
    /// <summary>What a scripted step does (docs/TESTING.md 3.4, step vocabulary).</summary>
    public enum StepKind
    {
        Move, Withdraw, FiveFootStep, StandUp, Crawl, DropProne,
        Attack, AttackAgain, FullAttack, Maneuver, GrappleAction, Cast, Charge,
        AnswerAoO, Pass, EndTurn, Assert, Wait, NaturalAttack, AnswerImprovedGrab, Flurry, Smite, LayOnHands,
        WildShape, RevertWildShape, BardicMusic, BardicConcentrate, BardicSuggestion, BardicStop,
        UseItem, CastHeld, DeliverHeld
    }

    /// <summary>An answer to the PC's AoO confirmation (the panel's three buttons).</summary>
    public enum AoOAnswer { Proceed = 0, Defensive = 1, Cancel = 2 }

    /// <summary>
    /// One typed step of a scripted turn. Scripted actors run it through the GameManager *ForAI entry points
    /// (the NPC executors); Ui actors run it through the PC button and cell-click callbacks. Build steps with the
    /// static factories; <see cref="Problem"/> rejects a step the actor's path cannot run.
    /// </summary>
    public sealed class Step
    {
        public StepKind Kind { get; private set; }
        public Vector2Int Cell { get; private set; }
        public string Target { get; private set; }
        public SpecialAttackType ManeuverType { get; private set; }
        public bool OffHand { get; private set; }
        public bool BullRush { get; private set; }
        public string GrappleKind { get; private set; }
        public string SpellId { get; private set; }
        public MetamagicData Metamagic { get; private set; }
        public AoOAnswer Answer { get; private set; }
        public string AssertName { get; private set; }
        public Func<ScenarioContext, bool> Check { get; private set; }
        public int Frames { get; private set; }
        public string NaturalAttackName { get; private set; }
        /// <summary>The square a grapple Move or Escape step clicks after the button (<see cref="GrappleMove"/>, <see cref="GrappleEscape"/>).</summary>
        public bool HasCell { get; private set; }
        /// <summary>The option label prefix an <see cref="AnswerImprovedGrab"/> or <see cref="GrappleDamage"/> step clicks.</summary>
        public string MenuChoice { get; private set; }
        /// <summary>The bestiary id of the animal a <see cref="WildShape"/> step takes (NPCDatabase).</summary>
        public string FormId { get; private set; }
        /// <summary>The song a <see cref="BardicMusic"/> step starts.</summary>
        public BardicAbility Ability { get; private set; }
        /// <summary>The actors a <see cref="BardicMusic"/> step fascinates (or the one ally it inspires with competence).</summary>
        public string[] Targets { get; private set; }
        /// <summary>The skill an Inspire Competence step helps.</summary>
        public string Skill { get; private set; }
        /// <summary>The ItemDatabase id of the scroll or wand a <see cref="UseItem"/> step uses.</summary>
        public string ItemId { get; private set; }
        /// <summary>
        /// The selection-list answers of a <see cref="CastChoosing"/> step, in order: an option label prefix, or "*" for
        /// the first option (the Resist Energy type, the Magic Weapon weapon). Null for every other step.
        /// </summary>
        public string[] Choices { get; private set; }

        /// <summary>The grapple actions a Ui step can press (GrappleSystem buttons).</summary>
        public static readonly string[] GrappleKinds = { "Pin", "ReleasePin", "Damage", "Escape", "Attack", "Move" };

        private Step(StepKind kind) { Kind = kind; }

        public static Step Move(int x, int y) => new Step(StepKind.Move) { Cell = new Vector2Int(x, y) };
        /// <summary>Withdraw (full-round double move; vacating the starting space provokes no AoO from the visible creatures threatening it, PHB p.143, GRID-021).</summary>
        public static Step Withdraw(int x, int y) => new Step(StepKind.Withdraw) { Cell = new Vector2Int(x, y) };
        public static Step FiveFootStep(int x, int y) => new Step(StepKind.FiveFootStep) { Cell = new Vector2Int(x, y) };
        public static Step StandUp() => new Step(StepKind.StandUp);
        /// <summary>Crawl 5 ft while prone (Ui only: the NPC path has no crawl).</summary>
        public static Step Crawl(int x, int y) => new Step(StepKind.Crawl) { Cell = new Vector2Int(x, y) };
        /// <summary>Drop prone (Ui only: the NPC path has no drop-prone executor).</summary>
        public static Step DropProne() => new Step(StepKind.DropProne);
        /// <summary>
        /// An attack on <paramref name="target"/>. Scripted: NPCPerformAttackForAI, which runs the creature's whole
        /// attack sequence (a full attack when the full round is still free; one attack after a move). Ui: one click of
        /// the Attack button (the first attack of the sequence).
        /// </summary>
        public static Step Attack(string target) => new Step(StepKind.Attack) { Target = target };
        /// <summary>Ui: the next Attack click of the iterative sequence. Scripted: as Attack (the sequence continues).</summary>
        public static Step AttackAgain(string target) => new Step(StepKind.AttackAgain) { Target = target };
        /// <summary>
        /// Ui only: one natural-attack button press for the natural attack named <paramref name="attackName"/>, then the
        /// target square. Like the button (ActionButtonPanel.BuildNaturalAttackButtonOptions), it presses the first
        /// unused attack of that name, or, when every one of that name is used and Haste's extra natural attack is
        /// unused, that attack again as the Haste attack (CMB-106).
        /// </summary>
        public static Step NaturalAttack(string target, string attackName)
            => new Step(StepKind.NaturalAttack) { Target = target, NaturalAttackName = attackName };
        /// <summary>
        /// A full attack; refused when the full round is no longer free. Ui: the Full Attack button, then the target; the
        /// coroutine's optional 5-foot step prompts are skipped (the actor's own square is clicked, as a player skips), and
        /// when Haste's natural-attack chooser opens (CMB-124) the option for <paramref name="hasteAttack"/> is clicked, or
        /// Cancel (the default natural attack) when it is null. The AI path picks with NaturalAttackChoice, so
        /// <paramref name="hasteAttack"/> is Ui only.
        /// </summary>
        public static Step FullAttack(string target, string hasteAttack = null)
            => new Step(StepKind.FullAttack) { Target = target, NaturalAttackName = hasteAttack };
        /// <summary>
        /// A monk's flurry of blows (PHB p.40-41; CHR-003); refused when the shared rules refuse it. Scripted:
        /// CharacterController.TryBeginFlurryOfBlows, then NPCPerformAttackForAI, which steps the flurry. Ui: the Flurry of
        /// Blows button (it declares the flurry and starts the progressive attack sequence), then the target: the first
        /// flurry step only; continue with AttackAgain or a Maneuver step.
        /// </summary>
        public static Step Flurry(string target) => new Step(StepKind.Flurry) { Target = target };
        /// <summary>
        /// A smite (a paladin's Smite Evil or a template smite; SmiteRules, CHR-020), then an attack on
        /// <paramref name="target"/>. Scripted: SmiteRules.TryDeclare (whatever the target's alignment, so a wasted smite
        /// can be scripted), then NPCPerformAttackForAI. Ui: the Smite button (it declares), then the Attack button and the
        /// target: the first attack of the sequence.
        /// </summary>
        public static Step Smite(string target) => new Step(StepKind.Smite) { Target = target };
        /// <summary>
        /// Lay on Hands on <paramref name="target"/> (the actor itself, a creature within touch, or an undead creature;
        /// LayOnHandsRules, CHR-020). Scripted: GameManager.TryLayOnHandsForAI. Ui: the Lay on Hands button, then the
        /// target's square.
        /// </summary>
        public static Step LayOnHands(string target) => new Step(StepKind.LayOnHands) { Target = target };
        /// <summary>
        /// Wild Shape into the bestiary animal <paramref name="formId"/> (WildShapeRules, CHR-020). Scripted:
        /// GameManager.TryWildShapeForAI. Ui: the Wild Shape button, then the form's option in the list.
        /// </summary>
        public static Step WildShape(string formId) => new Step(StepKind.WildShape) { FormId = formId };
        /// <summary>Changing back from a wild shape (a standard action). Scripted: TryWildShapeRevertForAI. Ui: the Wild Shape button.</summary>
        public static Step RevertWildShape() => new Step(StepKind.RevertWildShape);
        /// <summary>
        /// Starts a bardic music song (BardicMusicRules, CHR-026): Inspire Courage or Countersong with no targets, Fascinate
        /// with the actors to fascinate. Scripted: GameManager.TryBardicMusicForAI. Ui: the Bardic Music button, the song's
        /// option, then each target's option and, when fewer than the bard's count were named, Begin Fascinate.
        /// </summary>
        public static Step BardicMusic(BardicAbility ability, params string[] targets)
            => new Step(StepKind.BardicMusic) { Ability = ability, Targets = targets ?? new string[0] };
        /// <summary>Inspire Competence on <paramref name="ally"/> for <paramref name="skill"/> (Ui: the song, the ally, the skill).</summary>
        public static Step InspireCompetence(string ally, string skill)
            => new Step(StepKind.BardicMusic) { Ability = BardicAbility.InspireCompetence, Targets = new[] { ally }, Skill = skill };
        /// <summary>Concentrates on the bard's song (a standard action). Scripted: TryBardicConcentrateForAI. Ui: the list's Concentrate option.</summary>
        public static Step BardicConcentrate() => new Step(StepKind.BardicConcentrate);
        /// <summary>The bard's Suggestion to a creature he fascinates. Scripted: TryBardicSuggestionForAI. Ui: the list's Suggestion option.</summary>
        public static Step BardicSuggestion(string target) => new Step(StepKind.BardicSuggestion) { Target = target };
        /// <summary>Stops the bard's song (no action). Scripted: TryBardicStopForAI. Ui: the list's Stop option.</summary>
        public static Step BardicStop() => new Step(StepKind.BardicStop);
        /// <summary>A special attack. Scripted: TryNPCSpecialAttackByTypeForAI. Ui: the Special Attack menu, then the target square.</summary>
        public static Step Maneuver(SpecialAttackType type, string target, bool offHand = false)
            => new Step(StepKind.Maneuver) { ManeuverType = type, Target = target, OffHand = offHand };
        /// <summary>Ui only: a grapple action button: Pin, ReleasePin, Damage, Escape or Move (or Attack: use <see cref="GrappleAttack"/>).</summary>
        public static Step GrappleAction(string kind) => new Step(StepKind.GrappleAction) { GrappleKind = kind };
        /// <summary>
        /// Ui only: a grapple check button (Pin, Escape or Move) by an Improved Grab grabber whose won check also deals the
        /// grabbing attack's damage (MM p.310; CMB-148): the button opens the hold damage-mode menu, and the option whose
        /// label starts with <paramref name="holdDamageOptionPrefix"/> ("Lethal damage" or "Nonlethal damage") is clicked.
        /// A plain <see cref="GrappleAction"/> answers that menu "Lethal damage".
        /// </summary>
        public static Step GrappleAction(string kind, string holdDamageOptionPrefix)
            => new Step(StepKind.GrappleAction) { GrappleKind = kind, MenuChoice = holdDamageOptionPrefix };
        /// <summary>
        /// Ui only: the grapple attack button (Attack Unarmed, or Natural Attack for a creature fighting with its natural
        /// attacks). With <paramref name="naturalAttackName"/>, the natural-attack menu option that starts with that name
        /// is clicked: the first unused attack of that name, else its Haste option (CMB-127, CMB-106). With one option
        /// left the game makes it at once and no menu opens.
        /// </summary>
        public static Step GrappleAttack(string naturalAttackName = null)
            => new Step(StepKind.GrappleAction) { GrappleKind = "Attack", NaturalAttackName = naturalAttackName };
        /// <summary>
        /// Ui only: the Grapple: Damage button (also a holder's Damage check, CMB-163), then the damage-mode menu option
        /// whose label starts with <paramref name="optionPrefix"/> ("Lethal damage" or "Nonlethal damage").
        /// </summary>
        public static Step GrappleDamage(string optionPrefix = "Lethal damage")
            => new Step(StepKind.GrappleAction) { GrappleKind = "Damage", MenuChoice = optionPrefix };
        /// <summary>
        /// Ui only: the Grapple: Move button (an opposed check, PHB p.156), then, when the check is won, the destination
        /// square (x, y) of the drag (GrappleSystem.ExecuteGrappleMovement; CMB-152).
        /// </summary>
        public static Step GrappleMove(int x, int y)
            => new Step(StepKind.GrappleAction) { GrappleKind = "Move", Cell = new Vector2Int(x, y), HasCell = true };
        /// <summary>
        /// Ui only: the Grapple: Escape Check button, then, when the escape succeeds, the adjacent square (x, y) of the
        /// free escape move (PHB p.157; GrappleSystem.ExecuteFreeAdjacentGrappleMovement, CMB-152).
        /// </summary>
        public static Step GrappleEscape(int x, int y)
            => new Step(StepKind.GrappleAction) { GrappleKind = "Escape", Cell = new Vector2Int(x, y), HasCell = true };
        /// <summary>
        /// Ui: answers the PC's Improved Grab menu (MM p.310; CMB-148) that the step before it opens: "grapple" (conduct
        /// the grapple normally), "hold" (hold with the body part that hit, -20) or "skip". With no such step the menu
        /// is answered "skip". Skipped on the AI path (the AI chooses with AIProfile.ShouldHoldWithImprovedGrab).
        /// </summary>
        public static Step AnswerImprovedGrab(string choice) => new Step(StepKind.AnswerImprovedGrab) { MenuChoice = choice };

        /// <summary>The ImprovedGrabMenu label prefix for an <see cref="AnswerImprovedGrab"/> choice.</summary>
        internal static string ImprovedGrabOptionPrefix(string choice)
        {
            switch ((choice ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "grapple": return "Improved Grab: grapple";
                case "hold": return "Improved Grab: hold";
                default: return "Skip the grapple";
            }
        }
        /// <summary>
        /// Casts a prepared spell at <paramref name="target"/>. Scripted: TryNPCPerformSpellCastForAI with a clone of the
        /// database spell (never the template); no metamagic on that path. Ui: the cast menu with the first unused
        /// prepared slot holding that spell, then the target square (or, for a self-centered burst such as Prayer, the
        /// preview's confirmation).
        /// </summary>
        public static Step Cast(string spellId, string target, MetamagicData metamagic = null)
            => new Step(StepKind.Cast) { SpellId = spellId, Target = target, Metamagic = metamagic };
        /// <summary>
        /// Ui only: <see cref="Cast"/>, answering each selection list the cast opens (the Resist Energy type after the spell
        /// is picked, the Magic Weapon weapon after the target is clicked) with <paramref name="choices"/> in order: an
        /// option label prefix, or "*" for the first option.
        /// </summary>
        public static Step CastChoosing(string spellId, string target, params string[] choices)
            => new Step(StepKind.Cast) { SpellId = spellId, Target = target, Choices = choices ?? new string[0] };
        /// <summary>
        /// Ui only: <see cref="CastChoosing"/> aimed at the square (<paramref name="x"/>, <paramref name="y"/>) rather than at
        /// an actor: the destination of a Summon Monster or Summon Swarm cast, or the anchor of a Grease area. The choices
        /// answer the selection lists the cast opens (the Grease mode, the summoned creature, the swarm type) in order.
        /// </summary>
        public static Step CastAt(string spellId, int x, int y, params string[] choices)
            => new Step(StepKind.Cast) { SpellId = spellId, Cell = new Vector2Int(x, y), HasCell = true, Choices = choices ?? new string[0] };
        /// <summary>
        /// Ui only: casts a prepared melee touch spell and answers its touch-spell prompt Discharge Later
        /// (GameManager.BeginHoldTouchCharge, the cast core's HoldCharge resolve), so the caster holds the charge.
        /// </summary>
        public static Step CastHeld(string spellId) => new Step(StepKind.CastHeld) { SpellId = spellId };
        /// <summary>Ui only: the Discharge button (GameManager.OnDischargeHeldTouchButtonPressed), then the target's square.</summary>
        public static Step DeliverHeld(string target) => new Step(StepKind.DeliverHeld) { Target = target };
        /// <summary>
        /// Ui only: uses the first scroll or wand with ItemDatabase id <paramref name="itemId"/> in the actor's pack through
        /// GameManager.TryUseConsumableFromInventory (the Use Item panel's call), so a targeted spell enters the cast
        /// pipeline (InitiateScrollCastThroughPipeline, InitiateWandCastThroughPipeline). The target's square is clicked as
        /// soon as the target selection opens, after any AoO confirmation the item use raises (answer those with
        /// <see cref="AnswerAoO"/> steps).
        /// </summary>
        public static Step UseItem(string itemId, string target) => new Step(StepKind.UseItem) { ItemId = itemId, Target = target };
        /// <summary>
        /// A charge (with <paramref name="bullRush"/>, a bull rush on a charge). Ui: the Charge button (or the menu's Bull
        /// Rush (Charge) button), the target and the path confirmation; a pounce's Haste natural-attack chooser (CMB-124) is
        /// answered as in <see cref="FullAttack"/> with <paramref name="hasteAttack"/> (Ui only).
        /// </summary>
        public static Step Charge(string target, bool bullRush = false, string hasteAttack = null)
            => new Step(StepKind.Charge) { Target = target, BullRush = bullRush, NaturalAttackName = hasteAttack };
        /// <summary>Ui: answers the pending AoO confirmation; skipped when no confirmation is open. Skipped on the AI path.</summary>
        public static Step AnswerAoO(AoOAnswer answer) => new Step(StepKind.AnswerAoO) { Answer = answer };
        /// <summary>Does nothing (an Ui turn still ends with End Turn when the steps run out).</summary>
        public static Step Pass() => new Step(StepKind.Pass);
        /// <summary>Ends the turn at once; later steps are dropped. Ui: the End Turn button.</summary>
        public static Step EndTurn() => new Step(StepKind.EndTurn);
        /// <summary>Records an 'assert' trace event with the check's result (read it with Expect.AssertsPass).</summary>
        public static Step Assert(string name, Func<ScenarioContext, bool> check) => new Step(StepKind.Assert) { AssertName = name, Check = check };
        public static Step Wait(int frames) => new Step(StepKind.Wait) { Frames = frames };

        /// <summary>A short label for the trace, such as "Move(14,10)" or "Maneuver(Trip,orc)".</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case StepKind.Move:
                case StepKind.Withdraw:
                case StepKind.FiveFootStep:
                case StepKind.Crawl:
                    return Kind + "(" + Cell.x + "," + Cell.y + ")";
                case StepKind.Attack:
                case StepKind.AttackAgain:
                case StepKind.Flurry:
                case StepKind.Smite:
                case StepKind.LayOnHands:
                    return Kind + "(" + Target + ")";
                case StepKind.FullAttack:
                    return "FullAttack(" + Target + (string.IsNullOrEmpty(NaturalAttackName) ? "" : ",haste=" + NaturalAttackName) + ")";
                case StepKind.NaturalAttack:
                    return "NaturalAttack(" + NaturalAttackName + "," + Target + ")";
                case StepKind.Maneuver:
                    return "Maneuver(" + ManeuverType + "," + Target + (OffHand ? ",offhand" : "") + ")";
                case StepKind.GrappleAction:
                    return "GrappleAction(" + GrappleKind + (string.IsNullOrEmpty(NaturalAttackName) ? "" : "," + NaturalAttackName)
                        + (HasCell ? "," + Cell.x + "," + Cell.y : "") + ")";
                case StepKind.AnswerImprovedGrab:
                    return "AnswerImprovedGrab(" + MenuChoice + ")";
                case StepKind.Cast:
                    return "Cast(" + SpellId + "," + (HasCell ? Cell.x + "," + Cell.y : Target) + (Metamagic != null && Metamagic.HasAnyMetamagic ? ",metamagic" : "")
                        + (Choices != null ? ",choose=" + string.Join("|", Choices) : "") + ")";
                case StepKind.CastHeld:
                    return "CastHeld(" + SpellId + ")";
                case StepKind.DeliverHeld:
                    return "DeliverHeld(" + Target + ")";
                case StepKind.UseItem:
                    return "UseItem(" + ItemId + "," + Target + ")";
                case StepKind.Charge:
                    return "Charge(" + Target + (BullRush ? ",bullrush" : "") + (string.IsNullOrEmpty(NaturalAttackName) ? "" : ",haste=" + NaturalAttackName) + ")";
                case StepKind.AnswerAoO:
                    return "AnswerAoO(" + Answer + ")";
                case StepKind.Assert:
                    return "Assert(" + AssertName + ")";
                case StepKind.WildShape:
                    return "WildShape(" + FormId + ")";
                case StepKind.BardicMusic:
                    return "BardicMusic(" + Ability + (Targets != null && Targets.Length > 0 ? "," + string.Join(",", Targets) : "")
                        + (string.IsNullOrEmpty(Skill) ? "" : "," + Skill) + ")";
                case StepKind.BardicSuggestion:
                    return "BardicSuggestion(" + Target + ")";
                case StepKind.Wait:
                    return "Wait(" + Frames + ")";
                default:
                    return Kind.ToString();
            }
        }

        public override string ToString() => Describe();

        /// <summary>Null when an actor with <paramref name="control"/> can run this step, else the problem.</summary>
        internal string Problem(Control control, ScenarioDef def)
        {
            bool needsTarget = Kind == StepKind.Attack || Kind == StepKind.AttackAgain || Kind == StepKind.FullAttack || Kind == StepKind.Flurry
                || Kind == StepKind.Smite || Kind == StepKind.LayOnHands
                || Kind == StepKind.Maneuver || (Kind == StepKind.Cast && !HasCell) || Kind == StepKind.Charge || Kind == StepKind.NaturalAttack
                || Kind == StepKind.UseItem || Kind == StepKind.DeliverHeld;
            if (needsTarget && (string.IsNullOrEmpty(Target) || def.Find(Target) == null))
                return Describe() + ": target '" + Target + "' is not an actor";
            bool hasCell = Kind == StepKind.Move || Kind == StepKind.Withdraw || Kind == StepKind.FiveFootStep || Kind == StepKind.Crawl || HasCell;
            if (hasCell && (Cell.x < 0 || Cell.y < 0 || Cell.x >= ScenarioLimits.GridWidth || Cell.y >= ScenarioLimits.GridHeight))
                return Describe() + ": square off the grid";
            if (Kind == StepKind.GrappleAction && Array.IndexOf(GrappleKinds, GrappleKind) < 0)
                return Describe() + ": unknown grapple action (" + string.Join(", ", GrappleKinds) + ")";
            if ((Kind == StepKind.Cast || Kind == StepKind.CastHeld) && string.IsNullOrEmpty(SpellId))
                return Kind + " without a spell id";
            if (Kind == StepKind.UseItem && string.IsNullOrEmpty(ItemId))
                return "UseItem without an item id";
            if (Kind == StepKind.WildShape && (string.IsNullOrEmpty(FormId) || NPCDatabase.Get(FormId) == null))
                return Describe() + ": no bestiary entry '" + FormId + "'";
            if (Kind == StepKind.BardicSuggestion && (string.IsNullOrEmpty(Target) || def.Find(Target) == null))
                return Describe() + ": target '" + Target + "' is not an actor";
            if (Kind == StepKind.BardicMusic && Targets != null)
                foreach (string t in Targets)
                    if (def.Find(t) == null)
                        return Describe() + ": target '" + t + "' is not an actor";
            if (Kind == StepKind.Maneuver && ManeuverType == SpecialAttackType.BullRushCharge)
                return Describe() + ": a bull rush on a charge is Charge(target, bullRush: true)";
            if (Kind == StepKind.Assert && Check == null)
                return Describe() + ": no check";
            if (Kind == StepKind.Wait && Frames < 0)
                return Describe() + ": negative frames";
            if (Kind == StepKind.NaturalAttack && string.IsNullOrEmpty(NaturalAttackName))
                return Describe() + ": no natural attack name";
            if (control != Control.Ui)
            {
                if (Kind == StepKind.Crawl || Kind == StepKind.DropProne || Kind == StepKind.GrappleAction || Kind == StepKind.NaturalAttack
                    || Kind == StepKind.UseItem || Kind == StepKind.CastHeld || Kind == StepKind.DeliverHeld)
                    return Describe() + " is Ui only (the NPC path has no executor for it)";
                if (Kind == StepKind.Cast && HasCell)
                    return Describe() + ": a cast aimed at a square is Ui only (the NPC path casts at an actor)";
                if (Kind == StepKind.Cast && Choices != null)
                    return Describe() + ": selection-list choices are Ui only (the NPC path makes its own)";
                if (Kind == StepKind.Cast && Metamagic != null && Metamagic.HasAnyMetamagic)
                    return Describe() + ": the NPC cast path takes no metamagic";
                if ((Kind == StepKind.FullAttack || Kind == StepKind.Charge) && !string.IsNullOrEmpty(NaturalAttackName))
                    return Describe() + ": the Haste pick is Ui only (the AI path picks with NaturalAttackChoice)";
            }
            return null;
        }
    }

    /// <summary>The typed steps one actor takes in one round (round 0: every round without its own script).</summary>
    public sealed class TurnScript
    {
        public string Actor;
        public int Round;
        public List<Step> Steps = new List<Step>();
    }

    /// <summary>
    /// Runs typed steps. The AI path (<see cref="RunAi"/>) is the coroutine ScenarioHooks.ScriptedTurn hands to
    /// AIService.ExecuteNPCTurn; the UI path (<see cref="UiTurnDriver"/>) is ticked by the runner every frame.
    /// Every step ends as a 'step' trace event with status done, refused (no state change), skipped or dropped.
    /// </summary>
    internal static class ScriptedSteps
    {
        /// <summary>
        /// A Ui step (or End Turn, or the return of the action menu) that has not settled after this many frames AND
        /// <see cref="UiSettleGameSeconds"/> game-seconds ends the job as SoftLock. Both must pass, as in the core
        /// watchdog (ScenarioChecks.Frame): fast mode runs about one game-second per frame, while watch mode runs
        /// hundreds of frames per real second and a maneuver with its AoO and attack delays can take a few seconds.
        /// </summary>
        public const int UiSettleFrames = 600;
        public const float UiSettleGameSeconds = 30f;

        private static readonly HashSet<string> ActingEvents = new HashSet<string>
        {
            "attack", "aoo", "maneuver", "move", "cond", "threatened_cast", "hp", "nl"
        };

        /// <summary>
        /// A state fingerprint for refused-step detection: the count of game events in the trace plus the actor's
        /// position, action economy and conditions. Unchanged across a step means the step changed nothing.
        /// </summary>
        public static string Fingerprint(ScenarioJob job, CharacterController a)
        {
            int n = 0;
            IReadOnlyList<TraceEvent> events = job.Trace.Events;
            for (int i = 0; i < events.Count; i++)
                if (ActingEvents.Contains(events[i].Ev))
                    n++;
            var sb = new StringBuilder();
            sb.Append(n).Append('|');
            if (a != null && a.Stats != null)
            {
                sb.Append(a.GridPosition).Append('|');
                ActionEconomy act = a.Actions;
                if (act != null)
                    sb.Append(act.StandardActionUsed).Append(act.MoveActionUsed).Append(act.FullRoundActionUsed)
                      .Append(act.StandardConvertedToMove).Append(act.HasMoved5Ft).Append(act.SwiftActionUsed);
                sb.Append('|').Append(string.Join(",", ScenarioTrace.ConditionNames(a)));
                // A bardic music stop costs no action: the song under way and the uses left show the change.
                sb.Append('|').Append(BardicMusicRules.ActiveAbility(a.Stats)).Append(BardicMusicRules.UsesLeft(a.Stats));
            }
            return sb.ToString();
        }

        public static void EmitStep(ScenarioJob job, CharacterController actor, string path, int index, Step step, string status, string note)
        {
            if (job.Trace == null || job.Trace.Closed)
                return;
            TraceEvent ev = job.Trace.Emit("step").Set("actor", job.Trace.KeyOf(actor)).Set("path", path).Set("i", index)
                .Set("step", step != null ? step.Describe() : "EndTurn").Set("kind", step != null ? step.Kind.ToString() : "EndTurn")
                .Set("status", status);
            if (note != null)
                ev.Set("note", note);
        }

        public static void EmitAssert(ScenarioJob job, CharacterController actor, Step step, bool ok, string error)
        {
            if (job.Trace == null || job.Trace.Closed)
                return;
            TraceEvent ev = job.Trace.Emit("assert").Set("actor", job.Trace.KeyOf(actor)).Set("name", step.AssertName).Set("ok", ok);
            if (error != null)
                ev.Set("error", error);
        }

        /// <summary>Why the actor's remaining steps are dropped, or null to go on.</summary>
        public static string StopReason(ScenarioJob job, CharacterController a)
        {
            if (job.Decided)
                return "job decided";
            if (job.Gm == null || job.Gm.CurrentPhase == GameManager.TurnPhase.CombatOver)
                return "combat over";
            if (ScenarioChecks.IsDown(a))
                return "actor down";
            return null;
        }

        public static void DropRest(ScenarioJob job, CharacterController a, string path, List<Step> steps, int from, string why)
        {
            for (int j = from; j < steps.Count; j++)
                EmitStep(job, a, path, j, steps[j], "dropped", why);
        }

        private static void RunAssert(ScenarioJob job, CharacterController a, Step step, string path, int index)
        {
            bool ok;
            string error = null;
            try { ok = step.Check(job.Ctx); }
            catch (Exception ex) { ok = false; error = ex.GetType().Name + ": " + ex.Message; }
            EmitAssert(job, a, step, ok, error);
            EmitStep(job, a, path, index, step, "done", ok ? "assert ok" : "assert failed");
        }

        /// <summary>
        /// The AI-path executor for a Scripted actor's turn. Each step calls the same GameManager *ForAI entry point
        /// AIService uses, with the same bookkeeping at the call site (a move spends the move action afterwards, or
        /// the standard action converted to a move, as ConsumeMoveAction does).
        /// </summary>
        public static IEnumerator RunAi(ScenarioJob job, CharacterController a, List<Step> steps)
        {
            GameManager gm = job.Gm;
            const string path = "ai";
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                string stop = StopReason(job, a);
                if (stop != null)
                {
                    DropRest(job, a, path, steps, i, stop);
                    yield break;
                }

                if (s.Kind == StepKind.Pass)
                {
                    EmitStep(job, a, path, i, s, "done", null);
                    continue;
                }
                if (s.Kind == StepKind.EndTurn)
                {
                    EmitStep(job, a, path, i, s, "done", null);
                    DropRest(job, a, path, steps, i + 1, "turn ended by EndTurn");
                    yield break;
                }
                if (s.Kind == StepKind.Assert)
                {
                    RunAssert(job, a, s, path, i);
                    continue;
                }
                if (s.Kind == StepKind.AnswerAoO || s.Kind == StepKind.AnswerImprovedGrab)
                {
                    EmitStep(job, a, path, i, s, "skipped", "no prompts on the AI path");
                    continue;
                }
                if (s.Kind == StepKind.Wait)
                {
                    for (int f = 0; f < s.Frames; f++)
                        yield return null;
                    EmitStep(job, a, path, i, s, "done", null);
                    continue;
                }

                // A grappling creature's turn belongs to the grapple rules (AIService.RunGrappleTurnForAI), which the
                // AI executors below bypass: refuse movement and ordinary attacks instead of breaking those rules.
                if (a.IsGrappling() && (s.Kind == StepKind.Move || s.Kind == StepKind.Withdraw || s.Kind == StepKind.FiveFootStep
                    || s.Kind == StepKind.Attack || s.Kind == StepKind.AttackAgain || s.Kind == StepKind.FullAttack || s.Kind == StepKind.Flurry
                    || s.Kind == StepKind.Smite || s.Kind == StepKind.Charge))
                {
                    EmitStep(job, a, path, i, s, "refused", "grappling (no AI-path grapple executor; let the AI run this turn)");
                    continue;
                }

                string before = Fingerprint(job, a);
                int hookErrorsBefore = ScenarioHooks.HookErrors.Count;
                bool? ok = null;
                string note = null;
                CharacterController target = s.Target != null ? job.Ctx.Get(s.Target) : null;
                ActionEconomy act = a.Actions;

                switch (s.Kind)
                {
                    case StepKind.Move:
                        if (a.HasTakenFiveFootStep) { ok = false; note = "a 5-ft step was taken this turn"; break; }
                        if (!(act.HasMoveAction || act.CanConvertStandardToMove)) { ok = false; note = "no move action left"; break; }
                        yield return gm.MoveCharacterAlongComputedPathForAI(a, s.Cell, gm.GetPlayerMoveSecondsPerStepForAI());
                        // AIService spends the move after the path, unless the mover was dropped (CMB-073).
                        if (Fingerprint(job, a) != before && a.Stats != null && a.Stats.CurrentHP > 0)
                        {
                            if (act.HasMoveAction) act.UseMoveAction();
                            else if (act.CanConvertStandardToMove) act.ConvertStandardToMove();
                        }
                        break;

                    case StepKind.Withdraw:
                        if (!act.HasFullRoundAction) { ok = false; note = "no full-round action"; break; }
                        yield return gm.ExecuteWithdrawMovementForAI(a, s.Cell, gm.GetPlayerMoveSecondsPerStepForAI());
                        break;

                    case StepKind.FiveFootStep:
                        ok = gm.TryTakeFiveFootStepForAI(a, s.Cell);
                        yield return null;
                        break;

                    case StepKind.StandUp:
                        if (!a.HasCondition(CombatConditionType.Prone)) { ok = false; note = "not prone"; break; }
                        yield return gm.TryStandUpFromProneForAI(a);
                        break;

                    case StepKind.Attack:
                    case StepKind.AttackAgain:
                    case StepKind.FullAttack:
                        if (target == null || target.Stats == null || target.Stats.IsDead) { ok = false; note = "no live target"; break; }
                        if (s.Kind == StepKind.FullAttack && !act.HasFullRoundAction) { ok = false; note = "no full-round action"; break; }
                        yield return gm.NPCPerformAttackForAI(a, target);
                        break;

                    case StepKind.Flurry:
                        if (target == null || target.Stats == null || target.Stats.IsDead) { ok = false; note = "no live target"; break; }
                        if (!a.TryBeginFlurryOfBlows(out string flurryRefusal)) { ok = false; note = flurryRefusal; break; }
                        yield return gm.NPCPerformAttackForAI(a, target);
                        break;

                    case StepKind.Smite:
                        if (target == null || target.Stats == null || target.Stats.IsDead) { ok = false; note = "no live target"; break; }
                        if (SmiteRules.TryDeclare(a, out string smiteRefusal) == null) { ok = false; note = smiteRefusal; break; }
                        yield return gm.NPCPerformAttackForAI(a, target);
                        break;

                    case StepKind.LayOnHands:
                        if (target == null) { ok = false; note = "no target"; break; }
                        if (!LayOnHandsRules.CanUse(a, out string lohRefusal)) { ok = false; note = lohRefusal; break; }
                        ok = gm.TryLayOnHandsForAI(a, target);
                        yield return null;
                        break;

                    case StepKind.WildShape:
                        if (!WildShapeRules.CanAssume(a, out string wildShapeRefusal)) { ok = false; note = wildShapeRefusal; break; }
                        ok = gm.TryWildShapeForAI(a, s.FormId);
                        if (ok == false) note = "refused (see the combat log)";
                        yield return null;
                        break;

                    case StepKind.RevertWildShape:
                        if (!WildShapeRules.CanRevert(a, out string revertRefusal)) { ok = false; note = revertRefusal; break; }
                        ok = gm.TryWildShapeRevertForAI(a);
                        yield return null;
                        break;

                    case StepKind.BardicMusic:
                    {
                        if (!BardicMusicRules.CanStart(a, s.Ability, out string bardicRefusal)) { ok = false; note = bardicRefusal; break; }
                        var bardicTargets = new List<CharacterController>();
                        if (s.Targets != null)
                            foreach (string key in s.Targets)
                                bardicTargets.Add(job.Ctx.Get(key));
                        ok = gm.TryBardicMusicForAI(a, s.Ability, bardicTargets, s.Skill);
                        if (ok == false) note = "refused (see the combat log)";
                        yield return null;
                        break;
                    }

                    case StepKind.BardicConcentrate:
                        if (!BardicMusicRules.CanConcentrate(a, out string concentrateRefusal)) { ok = false; note = concentrateRefusal; break; }
                        ok = gm.TryBardicConcentrateForAI(a);
                        yield return null;
                        break;

                    case StepKind.BardicSuggestion:
                    {
                        string suggestionRefusal = BardicMusicRules.SuggestionTargetProblem(a, target);
                        if (suggestionRefusal != null) { ok = false; note = suggestionRefusal; break; }
                        ok = gm.TryBardicSuggestionForAI(a, target);
                        yield return null;
                        break;
                    }

                    case StepKind.BardicStop:
                        if (!BardicMusicRules.IsPerforming(a.Stats)) { ok = false; note = "not performing"; break; }
                        ok = gm.TryBardicStopForAI(a);
                        yield return null;
                        break;

                    case StepKind.Maneuver:
                        if (target == null) { ok = false; note = "no target"; break; }
                        // The game logs its refusal reason below Warning, which fast mode filters: note the preconditions.
                        string preM = Preconditions(a, s.ManeuverType);
                        ok = gm.TryNPCSpecialAttackByTypeForAI(a, target, s.ManeuverType);
                        if (ok == false) note = "executor returned false (" + preM + ")";
                        yield return null;
                        // A failed trip against a controllable defender opens its counter-trip prompt (CMB-079); the
                        // runner answers it, and the step settles after the answer, as the NPC coroutines wait for it.
                        for (int w = 0; w < UiSettleFrames && gm.IsAwaitingCounterTripChoice; w++)
                            yield return null;
                        break;

                    case StepKind.Cast:
                    {
                        SpellData template = SpellDatabase.GetSpell(s.SpellId);
                        if (template == null || target == null) { ok = false; note = template == null ? "unknown spell" : "no target"; break; }
                        string preC = Preconditions(a, null);
                        ok = gm.TryNPCPerformSpellCastForAI(a, target, template.Clone());
                        if (ok == false) note = "executor returned false (" + preC + ")";
                        yield return null;
                        break;
                    }

                    case StepKind.Charge:
                        if (target == null || !gm.CanChargeTargetForAI(a, target, s.BullRush)) { ok = false; note = "charge not legal"; break; }
                        yield return gm.NPCExecuteChargeForAI(a, target, s.BullRush);
                        break;

                    default:
                        ok = false;
                        note = s.Kind + " has no AI-path executor";
                        break;
                }

                bool changed = Fingerprint(job, a) != before;
                string status = ok.HasValue ? (ok.Value ? "done" : "refused") : (changed ? "done" : "refused");
                if (ok == true && !changed && note == null)
                    note = "reported success without a state change";
                // An executor that threw: SafeEnumerate recorded it in HookErrors (the job fails on it) and the script goes on.
                if (ScenarioHooks.HookErrors.Count > hookErrorsBefore)
                {
                    status = "error";
                    note = (note != null ? note + "; " : "") + ScenarioHooks.HookErrors[ScenarioHooks.HookErrors.Count - 1];
                }
                EmitStep(job, a, path, i, s, status, note);
            }
        }

        /// <summary>The action-economy facts an AI-path maneuver or cast depends on, for a refused step's note.</summary>
        private static string Preconditions(CharacterController a, SpecialAttackType? maneuver)
        {
            ActionEconomy act = a.Actions;
            var sb = new StringBuilder();
            sb.Append("std=").Append(act != null && act.HasStandardAction)
              .Append(", full=").Append(act != null && act.HasFullRoundAction);
            if (maneuver.HasValue)
            {
                bool next = a.CanCommitAttack(AttackStepKind.MainHand, out string why);
                sb.Append(", nextAttack=").Append(next);
                if (!next && !string.IsNullOrEmpty(why))
                    sb.Append(" (").Append(why).Append(')');
                sb.Append(", canPerform=").Append(a.CanPerformSpecialAttack(maneuver.Value));
            }
            sb.Append(", grappled=").Append(a.HasCondition(CombatConditionType.Grappled));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Drives one Ui actor's turn through the PC callbacks, one step per settled frame. A step acts only on a fresh
    /// frame when the actor is the active PC, the sub-phase is ChoosingAction and no prompt is open; AnswerAoO acts
    /// only while the AoO confirmation is open (an unscripted confirmation is answered Proceed and noted; a refused
    /// answer is followed by Cancel, never Proceed). A step is settled when the action menu returns, the turn passes
    /// or combat ends; one that does not settle within <see cref="ScriptedSteps.UiSettleFrames"/> frames and
    /// <see cref="ScriptedSteps.UiSettleGameSeconds"/> game-seconds ends the job as SoftLock with a diagnosis. When
    /// the steps run out and the actor still has the turn, End Turn is pressed.
    /// </summary>
    internal sealed class UiTurnDriver
    {
        private const string PathName = "ui";
        private readonly ScenarioJob _job;
        private readonly CharacterController _actor;
        private readonly List<Step> _steps;
        private int _index;
        private bool _awaiting;
        private Step _current;
        private int _currentIndex;
        private string _before;
        private string _awaitNote;
        private int _actionFrame;
        private float _actionTime;
        private int _readySince;
        private float _readySinceTime;
        private bool _endPressed;
        private readonly bool _scripted;
        /// <summary>The actor whose square a UseItem step clicks once the target selection opens (null: none pending).</summary>
        private CharacterController _pendingClick;

        public bool Done { get; private set; }

        public UiTurnDriver(ScenarioJob job, CharacterController actor, List<Step> steps)
        {
            _job = job;
            _actor = actor;
            _scripted = steps != null;
            _steps = steps ?? new List<Step>();
            _actionFrame = Time.frameCount;
            _actionTime = Time.time;
            _readySince = Time.frameCount;
            _readySinceTime = Time.time;
        }

        /// <summary>
        /// Gives up the turn without acting (the runner calls it when another Ui turn starts before this one settled,
        /// for example when the game ended the turn inside a step): the in-flight step is emitted and the rest dropped.
        /// </summary>
        public void Abandon(string why)
        {
            if (Done)
                return;
            FinishCurrent(why);
            if (!_endPressed)
                ScriptedSteps.DropRest(_job, _actor, PathName, _steps, _index, why);
            Done = true;
        }

        /// <summary>True when both the frame and the game-second budgets since (frame, time) are spent.</summary>
        private static bool Overdue(int frame, int sinceFrame, float sinceTime)
            => frame - sinceFrame > ScriptedSteps.UiSettleFrames && Time.time - sinceTime > ScriptedSteps.UiSettleGameSeconds;

        private static string Budget => ScriptedSteps.UiSettleFrames + " frames and " + ScriptedSteps.UiSettleGameSeconds.ToString("0") + " game-seconds";

        public void Tick(GameManager gm)
        {
            if (Done)
                return;
            if (_job.Decided)
            {
                Done = true;
                return;
            }

            int frame = Time.frameCount;
            bool ourTurn = gm.CurrentCharacter == _actor && gm.CurrentPhase != GameManager.TurnPhase.CombatOver;
            if (!ourTurn)
            {
                FinishCurrent(gm.CurrentPhase == GameManager.TurnPhase.CombatOver ? "combat ended" : "turn passed");
                if (!_endPressed)
                    ScriptedSteps.DropRest(_job, _actor, PathName, _steps, _index, gm.CurrentPhase == GameManager.TurnPhase.CombatOver ? "combat over" : "turn ended");
                Done = true;
                return;
            }
            if (frame <= _actionFrame)
                return;

            string prompt = gm.Harness_PendingPrompt();
            if (prompt == "aoo")
            {
                if (_index < _steps.Count && _steps[_index].Kind == StepKind.AnswerAoO)
                {
                    Step answer = _steps[_index];
                    bool ok = gm.Harness_AnswerAoO((int)answer.Answer);
                    if (!ok)
                    {
                        // Never fall through to Proceed: a scenario that asked for another answer must not run the provoking path.
                        bool cancelled = gm.Harness_AnswerAoO((int)AoOAnswer.Cancel);
                        ScriptedSteps.EmitStep(_job, _actor, PathName, _index, answer, "refused",
                            "that answer is not offered; " + (cancelled ? "answered Cancel" : "Cancel failed too"));
                    }
                    else
                    {
                        ScriptedSteps.EmitStep(_job, _actor, PathName, _index, answer, "done", null);
                    }
                    _index++;
                }
                else
                {
                    gm.Harness_AnswerAoO((int)AoOAnswer.Proceed);
                    const string auto = "AoO confirmation answered Proceed (no AnswerAoO step)";
                    if (_awaiting)
                        _awaitNote = _awaitNote == null ? auto : _awaitNote + "; " + auto;
                    else
                        ScriptedSteps.EmitStep(_job, _actor, PathName, _index, Step.AnswerAoO(AoOAnswer.Proceed), "done", "auto: " + auto);
                }
                _actionFrame = frame;
                _actionTime = Time.time;
                return;
            }

            // Prompts a Full Attack or a pounce opens while the step is in flight: the optional 5-foot steps are skipped,
            // and Haste's natural-attack chooser (CMB-124) is answered from the step.
            if (_awaiting && prompt == "full-attack-5ft")
            {
                SkipFullAttackFiveFootStep(gm);
                _actionFrame = frame;
                _actionTime = Time.time;
                return;
            }
            if (_awaiting && prompt == "submenu" && gm.CombatUI != null
                && gm.CombatUI.Harness_OpenSpecialStyleMenuName() == GameManager.HasteNaturalAttackMenuName)
            {
                AnswerHasteNaturalAttackChooser(gm);
                _actionFrame = frame;
                _actionTime = Time.time;
                return;
            }
            // The Improved Grab menu a hit opens (MM p.310; CMB-148): answered from the next AnswerImprovedGrab step, else Skip.
            if (prompt == "submenu" && gm.CombatUI != null && gm.CombatUI.Harness_OpenSpecialStyleMenuName() == "ImprovedGrabMenu")
            {
                AnswerImprovedGrabMenu(gm);
                _actionFrame = frame;
                _actionTime = Time.time;
                return;
            }

            // A UseItem step's target selection opens only after the item use (and any AoO it provoked) resolved.
            if (_awaiting && _pendingClick != null && prompt == null && ClickPendingTarget(gm))
            {
                _actionFrame = frame;
                _actionTime = Time.time;
                return;
            }

            bool ready = gm.IsPlayerTurn && gm.ActivePC == _actor && gm.CurrentSubPhase == GameManager.PlayerSubPhase.ChoosingAction && prompt == null;
            if (_awaiting)
            {
                if (ready)
                {
                    FinishCurrent(null);
                    _readySince = frame;
                    _readySinceTime = Time.time;
                }
                else if (Overdue(frame, _actionFrame, _actionTime))
                {
                    SoftLock(gm, "Ui step " + _current.Describe() + " did not settle within " + Budget + " (prompt " + (prompt ?? "none") + ", subPhase " + gm.CurrentSubPhase + ")");
                }
                return;
            }

            if (!ready || _endPressed)
            {
                bool actionLater = _actionFrame >= _readySince;
                if (Overdue(frame, actionLater ? _actionFrame : _readySince, actionLater ? _actionTime : _readySinceTime))
                    SoftLock(gm, (_endPressed ? "End Turn did not pass the turn" : "the action menu did not return") + " within " + Budget + " (prompt " + (prompt ?? "none") + ", subPhase " + gm.CurrentSubPhase + ")");
                return;
            }

            while (_index < _steps.Count && (_steps[_index].Kind == StepKind.AnswerAoO || _steps[_index].Kind == StepKind.AnswerImprovedGrab))
            {
                ScriptedSteps.EmitStep(_job, _actor, PathName, _index, _steps[_index], "skipped",
                    _steps[_index].Kind == StepKind.AnswerAoO ? "no AoO confirmation open" : "no Improved Grab menu open");
                _index++;
            }

            if (_index >= _steps.Count)
            {
                ScriptedSteps.EmitStep(_job, _actor, PathName, _index, null, "done", _scripted ? "steps done" : "no Ui steps");
                _endPressed = true;
                _actionFrame = frame;
                _actionTime = Time.time;
                gm.OnEndTurnButtonPressed();
                return;
            }

            Step step = _steps[_index];
            int stepIndex = _index;
            _index++;
            Execute(gm, step, stepIndex, frame);
        }

        private void FinishCurrent(string why)
        {
            if (!_awaiting)
                return;
            _awaiting = false;
            bool changed = ScriptedSteps.Fingerprint(_job, _actor) != _before;
            if (_pendingClick != null)
            {
                _awaitNote = (_awaitNote == null ? "" : _awaitNote + "; ") + "the target selection never opened";
                _pendingClick = null;
            }
            string note = _awaitNote;
            if (why != null)
                note = note == null ? why : note + "; " + why;
            ScriptedSteps.EmitStep(_job, _actor, PathName, _currentIndex, _current, changed ? "done" : "refused", note);
            _awaitNote = null;
        }

        private void SoftLock(GameManager gm, string reason)
        {
            string diag = _job.Checks != null ? _job.Checks.Diagnose() : gm.Harness_DumpState();
            _job.Decide(Outcome.SoftLock, reason, haltNow: false, diag: diag);
            Done = true;
        }

        private void Execute(GameManager gm, Step s, int index, int frame)
        {
            _actionFrame = frame;
            _actionTime = Time.time;
            CharacterController target = s.Target != null ? _job.Ctx.Get(s.Target) : null;

            switch (s.Kind)
            {
                case StepKind.Pass:
                    ScriptedSteps.EmitStep(_job, _actor, PathName, index, s, "done", null);
                    return;
                case StepKind.Assert:
                {
                    bool ok;
                    string error = null;
                    try { ok = s.Check(_job.Ctx); }
                    catch (Exception ex) { ok = false; error = ex.GetType().Name + ": " + ex.Message; }
                    ScriptedSteps.EmitAssert(_job, _actor, s, ok, error);
                    ScriptedSteps.EmitStep(_job, _actor, PathName, index, s, "done", ok ? "assert ok" : "assert failed");
                    return;
                }
                case StepKind.Wait:
                    _actionFrame = frame + s.Frames;
                    _actionTime = Time.time;
                    ScriptedSteps.EmitStep(_job, _actor, PathName, index, s, "done", null);
                    return;
                case StepKind.EndTurn:
                    ScriptedSteps.EmitStep(_job, _actor, PathName, index, s, "done", null);
                    ScriptedSteps.DropRest(_job, _actor, PathName, _steps, index + 1, "turn ended by EndTurn");
                    _index = _steps.Count;
                    _endPressed = true;
                    gm.OnEndTurnButtonPressed();
                    return;
            }

            _current = s;
            _currentIndex = index;
            _before = ScriptedSteps.Fingerprint(_job, _actor);
            _awaiting = true;
            _awaitNote = null;

            switch (s.Kind)
            {
                case StepKind.Move:
                    gm.OnMoveButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.Moving, s.Cell);
                    break;
                case StepKind.Withdraw:
                    gm.OnWithdrawButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.Moving, s.Cell);
                    break;
                case StepKind.FiveFootStep:
                    gm.OnFiveFootStepButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.TakingFiveFootStep, s.Cell);
                    break;
                case StepKind.Crawl:
                    gm.OnCrawlButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.Crawling, s.Cell);
                    break;
                case StepKind.StandUp:
                    gm.OnStandUpButtonPressed();
                    break;
                case StepKind.DropProne:
                    gm.OnDropProneButtonPressed();
                    break;
                case StepKind.Attack:
                case StepKind.AttackAgain:
                    if (target == null) { _awaitNote = "no target"; break; }
                    gm.OnAttackButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.SelectingAttackTarget, target.GridPosition);
                    break;
                case StepKind.FullAttack:
                    if (target == null) { _awaitNote = "no target"; break; }
                    gm.OnFullAttackButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.SelectingAttackTarget, target.GridPosition);
                    break;
                case StepKind.Flurry:
                    if (target == null) { _awaitNote = "no target"; break; }
                    gm.OnFlurryOfBlowsButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.SelectingAttackTarget, target.GridPosition);
                    break;
                case StepKind.Smite:
                    if (target == null) { _awaitNote = "no target"; break; }
                    gm.OnSmiteButtonPressed();
                    if (_actor.Stats.DeclaredSmite == SmiteSource.None) { _awaitNote = "the Smite button declared nothing"; break; }
                    gm.OnAttackButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.SelectingAttackTarget, target.GridPosition);
                    break;
                case StepKind.LayOnHands:
                    if (target == null) { _awaitNote = "no target"; break; }
                    gm.OnLayOnHandsButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.SelectingAttackTarget, target.GridPosition);
                    break;
                case StepKind.WildShape:
                    if (WildShapeRules.IsWildShaped(_actor.Stats)) { _awaitNote = "already in animal form"; break; }
                    gm.OnWildShapeButtonPressed();
                    if (!gm.Harness_ChooseWildShapeForm(s.FormId))
                    {
                        _awaitNote = "the Wild Shape list did not offer " + s.FormId;
                        gm.CombatUI?.HidePickUpItemSelection();
                    }
                    break;
                case StepKind.RevertWildShape:
                    if (!WildShapeRules.IsWildShaped(_actor.Stats)) { _awaitNote = "not in animal form"; break; }
                    gm.OnWildShapeButtonPressed();
                    break;
                case StepKind.BardicMusic:
                {
                    gm.OnBardicMusicButtonPressed();
                    string song = BardicAbilityInfo.GetDisplayName(s.Ability) + " (";
                    if (!gm.Harness_ChooseSelectionOption(song)) { _awaitNote = "the Bardic Music list did not offer " + song; gm.CombatUI?.HidePickUpItemSelection(); break; }
                    bool picked = true;
                    if (s.Targets != null)
                    {
                        foreach (string key in s.Targets)
                        {
                            CharacterController t = _job.Ctx.Get(key);
                            if (t == null || !gm.Harness_ChooseSelectionOption(t.Stats.CharacterName + " ("))
                            {
                                _awaitNote = "the list did not offer " + key;
                                picked = false;
                                break;
                            }
                        }
                    }
                    if (picked && s.Ability == BardicAbility.InspireCompetence && !gm.Harness_ChooseSelectionOption(s.Skill + " ("))
                    {
                        _awaitNote = "the skill list did not offer " + s.Skill;
                        picked = false;
                    }
                    if (picked && s.Ability == BardicAbility.Fascinate)
                        gm.Harness_ChooseSelectionOption("Begin Fascinate");
                    if (!picked)
                        gm.CombatUI?.HidePickUpItemSelection();
                    break;
                }
                case StepKind.BardicConcentrate:
                    gm.OnBardicMusicButtonPressed();
                    if (!gm.Harness_ChooseSelectionOption("Concentrate on")) { _awaitNote = "no Concentrate option"; gm.CombatUI?.HidePickUpItemSelection(); }
                    break;
                case StepKind.BardicSuggestion:
                    if (target == null) { _awaitNote = "no target"; break; }
                    gm.OnBardicMusicButtonPressed();
                    if (!gm.Harness_ChooseSelectionOption("Suggestion: " + target.Stats.CharacterName)) { _awaitNote = "no Suggestion option for " + s.Target; gm.CombatUI?.HidePickUpItemSelection(); }
                    break;
                case StepKind.BardicStop:
                    gm.OnBardicMusicButtonPressed();
                    if (!gm.Harness_ChooseSelectionOption("Stop ")) { _awaitNote = "no Stop option"; gm.CombatUI?.HidePickUpItemSelection(); }
                    break;
                case StepKind.NaturalAttack:
                {
                    if (target == null) { _awaitNote = "no target"; break; }
                    int sequenceIndex = FindNaturalAttackOption(gm, _actor, s.NaturalAttackName);
                    if (sequenceIndex < 0) { _awaitNote = "no natural-attack option named " + s.NaturalAttackName; break; }
                    gm.OnNaturalAttackButtonPressed(sequenceIndex, s.NaturalAttackName);
                    ClickIf(gm, GameManager.PlayerSubPhase.SelectingAttackTarget, target.GridPosition);
                    break;
                }
                case StepKind.Maneuver:
                    if (target == null) { _awaitNote = "no target"; break; }
                    if (!PressMenuButton(gm, s.ManeuverType, s.OffHand))
                        break;
                    ClickIf(gm, GameManager.PlayerSubPhase.SelectingSpecialTarget, target.GridPosition);
                    break;
                case StepKind.GrappleAction:
                    PressGrapple(gm, s.GrappleKind);
                    if (s.GrappleKind == "Attack")
                        PickGrappleNaturalAttack(gm, s.NaturalAttackName);
                    else if (s.GrappleKind == "Damage")
                        PickGrappleDamageMode(gm, s.MenuChoice);
                    else if (gm.CombatUI != null && gm.CombatUI.Harness_OpenSpecialStyleMenuName() == "GrappleHoldDamageModeMenu")
                        PickGrappleDamageMode(gm, s.MenuChoice, "GrappleHoldDamageModeMenu");
                    // A won grapple move check, or a successful escape, opens the destination selection (CMB-152).
                    if (s.HasCell && gm.CurrentSubPhase == GameManager.PlayerSubPhase.Moving)
                        ClickIf(gm, GameManager.PlayerSubPhase.Moving, s.Cell);
                    else if (s.HasCell)
                        _awaitNote = "no destination selection opened (the check failed or the move is not offered)";
                    break;
                case StepKind.Cast:
                {
                    if (target == null && !s.HasCell) { _awaitNote = "no target"; break; }
                    SpellData spell = FindPreparedSpell(_actor, s.SpellId);
                    if (spell == null) { _awaitNote = "no unused prepared slot holds " + s.SpellId; break; }
                    gm.OnCastSpellButtonPressed();
                    gm.Harness_SelectSpell(spell, s.Metamagic);
                    // A melee touch spell first asks Cast Now or Discharge Later; the step casts now, as a player would.
                    if (gm.Harness_PendingPrompt() == "touch-spell")
                        _awaitNote = gm.Harness_AnswerTouchSpellPromptCastNow()
                            ? "touch-spell prompt answered Cast Now"
                            : "the touch-spell prompt had no Cast Now button";
                    int choice = 0;
                    if (s.Choices != null)
                        AnswerChoices(gm, s, ref choice);
                    GameManager.PlayerSubPhase sub = gm.CurrentSubPhase;
                    if (sub == GameManager.PlayerSubPhase.SelectingAttackTarget || sub == GameManager.PlayerSubPhase.SelectingAoETarget)
                        ClickIf(gm, sub, s.HasCell ? s.Cell : target.GridPosition);
                    // A self-centered burst (Bless, Prayer) opens a preview that a left click confirms; the step confirms it.
                    else if (sub == GameManager.PlayerSubPhase.ConfirmingSelfAoE && !gm.Harness_ConfirmSelfAoE())
                        _awaitNote = "the self-centered area preview could not be confirmed";
                    if (s.Choices != null)
                    {
                        AnswerChoices(gm, s, ref choice);
                        if (choice < s.Choices.Length)
                            _awaitNote = (_awaitNote == null ? "" : _awaitNote + "; ") + "no selection list offered '" + s.Choices[choice] + "'";
                    }
                    break;
                }
                case StepKind.CastHeld:
                {
                    SpellData spell = FindPreparedSpell(_actor, s.SpellId);
                    if (spell == null) { _awaitNote = "no unused prepared slot holds " + s.SpellId; break; }
                    gm.OnCastSpellButtonPressed();
                    gm.Harness_SelectSpell(spell, s.Metamagic);
                    if (gm.Harness_PendingPrompt() != "touch-spell") { _awaitNote = "no touch-spell prompt opened"; break; }
                    if (!gm.Harness_AnswerTouchSpellPromptDischargeLater())
                        _awaitNote = "the touch-spell prompt had no Discharge Later button";
                    break;
                }
                case StepKind.DeliverHeld:
                    if (target == null) { _awaitNote = "no target"; break; }
                    gm.OnDischargeHeldTouchButtonPressed();
                    ClickIf(gm, GameManager.PlayerSubPhase.SelectingAttackTarget, target.GridPosition);
                    break;
                case StepKind.UseItem:
                {
                    if (target == null) { _awaitNote = "no target"; break; }
                    int packSlot = FindPackItem(_actor, s.ItemId);
                    if (packSlot < 0) { _awaitNote = "no " + s.ItemId + " in the pack"; break; }
                    if (!gm.TryUseConsumableFromInventory(_actor, packSlot, out string feedback))
                    {
                        _awaitNote = "the item use was refused: " + feedback;
                        break;
                    }
                    _pendingClick = target;
                    if (gm.Harness_PendingPrompt() == null)
                        ClickPendingTarget(gm);
                    break;
                }
                case StepKind.Charge:
                    if (target == null) { _awaitNote = "no target"; break; }
                    if (s.BullRush)
                    {
                        if (!PressMenuButton(gm, SpecialAttackType.BullRushCharge, false))
                            break;
                    }
                    else
                    {
                        gm.OnChargeButtonPressed();
                    }
                    if (gm.CurrentSubPhase == GameManager.PlayerSubPhase.SelectingChargeTarget)
                        gm.OnCellClicked(gm.Grid.GetCell(target.GridPosition));
                    if (gm.CurrentSubPhase == GameManager.PlayerSubPhase.ConfirmingChargePath)
                        gm.OnCellClicked(gm.Grid.GetCell(target.GridPosition));
                    CancelIfStillSelecting(gm, GameManager.PlayerSubPhase.SelectingChargeTarget, GameManager.PlayerSubPhase.ConfirmingChargePath);
                    break;
                default:
                    _awaitNote = s.Kind + " has no Ui executor";
                    break;
            }
        }

        /// <summary>
        /// Opens the Special Attack menu and clicks the button for <paramref name="type"/> as a player would; false
        /// (with the reason noted and the menu closed) when the menu does not open or that button is hidden or disabled.
        /// </summary>
        private bool PressMenuButton(GameManager gm, SpecialAttackType type, bool offHand)
        {
            if (!gm.Harness_OpenSpecialAttackMenu(out string stale))
            {
                _awaitNote = "the Special Attack menu did not open" + (stale != null ? "; " + stale : "");
                return false;
            }
            if (stale != null)
                _awaitNote = stale;
            if (!gm.Harness_PressSpecialAttackButton(type, offHand, out string why))
            {
                _awaitNote = (_awaitNote != null ? _awaitNote + "; " : "") + why;
                gm.Harness_CancelToActionChoices();
                return false;
            }
            return true;
        }

        /// <summary>
        /// The sequence index a natural-attack button for <paramref name="attackName"/> carries, as
        /// ActionButtonPanel.BuildNaturalAttackButtonOptions builds it: the first unused attack of that name, else the
        /// first used one while it is offered again for Haste's extra attack (CMB-106); -1 when there is none.
        /// </summary>
        private static int FindNaturalAttackOption(GameManager gm, CharacterController actor, string attackName)
        {
            if (actor == null || actor.Stats == null)
                return -1;
            int count = actor.Stats.GetTotalNaturalAttackCount();
            int hasteOption = -1;
            for (int i = 0; i < count; i++)
            {
                NaturalAttackDefinition natural = actor.Stats.GetNaturalAttackAtSequenceIndex(i);
                if (natural == null || !string.Equals(natural.Name != null ? natural.Name.Trim() : null, attackName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!gm.IsNaturalAttackSequenceIndexUsed(actor, i))
                    return i;
                if (hasteOption < 0 && gm.IsHasteExtraNaturalAttackOption(actor, i))
                    hasteOption = i;
            }
            return hasteOption;
        }

        /// <summary>Clicks <paramref name="cell"/> when the button opened <paramref name="expected"/>; cancels back to the menu when the click chose nothing.</summary>
        private void ClickIf(GameManager gm, GameManager.PlayerSubPhase expected, Vector2Int cell)
        {
            if (gm.CurrentSubPhase != expected)
            {
                _awaitNote = "the button did not open " + expected;
                return;
            }
            SquareCell c = gm.Grid != null ? gm.Grid.GetCell(cell) : null;
            if (c == null)
            {
                _awaitNote = "no square " + cell;
                gm.Harness_CancelToActionChoices();
                return;
            }
            gm.OnCellClicked(c);
            CancelIfStillSelecting(gm, expected);
        }

        private void CancelIfStillSelecting(GameManager gm, params GameManager.PlayerSubPhase[] selecting)
        {
            if (gm.Harness_PendingPrompt() == "aoo")
                return; // the confirmation answers next frame
            if (gm.CurrentCharacter != _actor)
                return;
            if (Array.IndexOf(selecting, gm.CurrentSubPhase) >= 0)
            {
                _awaitNote = (_awaitNote != null ? _awaitNote + "; " : "") + "the click chose nothing; selection cancelled";
                gm.Harness_CancelToActionChoices();
            }
        }

        private void PressGrapple(GameManager gm, string kind)
        {
            switch (kind)
            {
                case "Pin": gm.OnGrapplePinButtonPressed(); break;
                case "ReleasePin": gm.OnGrappleReleasePinButtonPressed(); break;
                case "Damage": gm.OnGrappleDamageButtonPressed(); break;
                case "Escape": gm.OnGrappleEscapeCheckButtonPressed(); break;
                case "Attack": gm.OnGrappleUnarmedAttackButtonPressed(); break;
                case "Move": gm.OnGrappleMoveButtonPressed(); break;
            }
        }

        /// <summary>
        /// After the Grapple: Damage button: clicks the damage-mode option whose label starts with
        /// <paramref name="optionPrefix"/> (default "Lethal damage"); no menu (the action was refused): noted.
        /// </summary>
        private void PickGrappleDamageMode(GameManager gm, string optionPrefix, string menuName = "GrappleDamageModeMenu")
        {
            string menu = gm.CombatUI != null ? gm.CombatUI.Harness_OpenSpecialStyleMenuName() : null;
            if (menu != menuName)
            {
                _awaitNote = "no damage-mode menu opened";
                return;
            }
            string prefix = string.IsNullOrEmpty(optionPrefix) ? "Lethal damage" : optionPrefix;
            UnityEngine.UI.Button option = gm.CombatUI.Harness_FindSpecialStyleOption(prefix);
            if (option == null || !option.interactable)
            {
                _awaitNote = "no damage-mode option starting with " + prefix + "; cancelled";
                gm.CombatUI.HideSpecialStyleSelectionMenu();
                gm.Harness_CancelToActionChoices();
                return;
            }
            option.onClick.Invoke();
            _awaitNote = "damage mode: " + prefix;
        }

        /// <summary>
        /// Answers the PC's Improved Grab menu (MM p.310; CMB-148): with the next step an AnswerImprovedGrab, clicks its
        /// option ("grapple", "hold" or "skip") and records that step; otherwise clicks Skip and notes it on the step in flight.
        /// </summary>
        private void AnswerImprovedGrabMenu(GameManager gm)
        {
            Step answer = _index < _steps.Count && _steps[_index].Kind == StepKind.AnswerImprovedGrab ? _steps[_index] : null;
            string prefix = Step.ImprovedGrabOptionPrefix(answer != null ? answer.MenuChoice : "skip");
            UnityEngine.UI.Button option = gm.CombatUI.Harness_FindSpecialStyleOption(prefix);
            bool clicked = option != null && option.interactable;
            if (clicked)
                option.onClick.Invoke();
            else
            {
                UnityEngine.UI.Button cancel = gm.CombatUI.Harness_FindSpecialStyleCancel();
                if (cancel != null)
                    cancel.onClick.Invoke();
                else
                    gm.CombatUI.HideSpecialStyleSelectionMenu();
            }

            if (answer != null)
            {
                ScriptedSteps.EmitStep(_job, _actor, PathName, _index, answer, clicked ? "done" : "refused",
                    clicked ? null : "no option starting with " + prefix + "; answered Skip");
                _index++;
            }
            else
            {
                string note = "Improved Grab menu answered Skip (no AnswerImprovedGrab step)";
                _awaitNote = _awaitNote == null ? note : _awaitNote + "; " + note;
            }
        }

        /// <summary>
        /// After the grapple attack button: clicks the natural-attack menu option for <paramref name="attackName"/> as a
        /// player would (CMB-127). The options read "Name (primary ...)" or "Name (Haste, ...)", unused ones first, so the
        /// first label starting with "Name (" is the unused attack of that name, else its Haste option. No menu: noted.
        /// </summary>
        private void PickGrappleNaturalAttack(GameManager gm, string attackName)
        {
            string menu = gm.CombatUI != null ? gm.CombatUI.Harness_OpenSpecialStyleMenuName() : null;
            if (menu != "GrappleNaturalAttackMenu")
            {
                if (!string.IsNullOrEmpty(attackName))
                    _awaitNote = "no natural-attack menu opened (one option is made at once)";
                return;
            }
            if (string.IsNullOrEmpty(attackName))
            {
                _awaitNote = "the natural-attack menu opened but the step names no attack; cancelled";
                gm.CombatUI.HideSpecialStyleSelectionMenu();
                gm.Harness_CancelToActionChoices();
                return;
            }
            UnityEngine.UI.Button option = gm.CombatUI.Harness_FindSpecialStyleOption(attackName.Trim() + " (");
            if (option == null || !option.interactable)
            {
                _awaitNote = "no natural-attack option named " + attackName + "; cancelled";
                gm.CombatUI.HideSpecialStyleSelectionMenu();
                gm.Harness_CancelToActionChoices();
                return;
            }
            option.onClick.Invoke();
        }

        /// <summary>
        /// Skips the Full Attack coroutine's optional 5-foot step prompt by clicking the actor's own square, as a player
        /// skips it (GameManager.HandleFiveFootStepClick -> CancelFiveFootStepSelection). Noted once per step.
        /// </summary>
        private void SkipFullAttackFiveFootStep(GameManager gm)
        {
            SquareCell own = gm.Grid != null ? gm.Grid.GetCell(_actor.GridPosition) : null;
            if (own == null)
                return;
            gm.OnCellClicked(own);
            const string note = "full-attack 5-foot step prompts skipped";
            if (_awaitNote == null || !_awaitNote.Contains(note))
                _awaitNote = _awaitNote == null ? note : _awaitNote + "; " + note;
        }

        /// <summary>
        /// Answers Haste's natural-attack chooser on a Full Attack or a pounce (CMB-124): clicks the option whose label
        /// starts with the step's natural attack name and " (", or Cancel (the default natural attack) when the step names
        /// none or no such option exists. The answer is noted on the step.
        /// </summary>
        private void AnswerHasteNaturalAttackChooser(GameManager gm)
        {
            string attackName = _current != null ? _current.NaturalAttackName : null;
            string note;
            UnityEngine.UI.Button option = !string.IsNullOrEmpty(attackName)
                ? gm.CombatUI.Harness_FindSpecialStyleOption(attackName.Trim() + " (")
                : null;
            if (option != null && option.interactable)
            {
                option.onClick.Invoke();
                note = "Haste chooser: " + attackName;
            }
            else
            {
                UnityEngine.UI.Button cancel = gm.CombatUI.Harness_FindSpecialStyleCancel();
                if (cancel != null)
                    cancel.onClick.Invoke();
                else
                    gm.CombatUI.HideSpecialStyleSelectionMenu();
                note = string.IsNullOrEmpty(attackName)
                    ? "Haste chooser cancelled (default)"
                    : "Haste chooser has no option named " + attackName + "; cancelled (default)";
            }
            _awaitNote = _awaitNote == null ? note : _awaitNote + "; " + note;
        }

        /// <summary>Answers open selection lists with the step's choices from <paramref name="next"/> on, while one is offered.</summary>
        private static void AnswerChoices(GameManager gm, Step s, ref int next)
        {
            while (next < s.Choices.Length)
            {
                string c = s.Choices[next];
                bool ok = c == "*" ? gm.Harness_ChooseFirstSelectionOption() : gm.Harness_ChooseSelectionOption(c);
                if (!ok)
                    return;
                next++;
            }
        }

        /// <summary>
        /// Clicks the pending UseItem target's square when a target selection is open; true when it clicked. A click that
        /// picks nothing leaves the selection, as <see cref="ClickIf"/> does.
        /// </summary>
        private bool ClickPendingTarget(GameManager gm)
        {
            GameManager.PlayerSubPhase sub = gm.CurrentSubPhase;
            if (sub != GameManager.PlayerSubPhase.SelectingAttackTarget && sub != GameManager.PlayerSubPhase.SelectingAoETarget)
                return false;
            CharacterController target = _pendingClick;
            _pendingClick = null;
            SquareCell c = gm.Grid != null && target != null ? gm.Grid.GetCell(target.GridPosition) : null;
            if (c == null)
            {
                _awaitNote = "no square for the item's target";
                gm.Harness_CancelToActionChoices();
                return true;
            }
            gm.OnCellClicked(c);
            CancelIfStillSelecting(gm, sub);
            return true;
        }

        /// <summary>The pack slot of the first item with ItemDatabase id <paramref name="itemId"/>, or -1.</summary>
        private static int FindPackItem(CharacterController a, string itemId)
        {
            global::Inventory inv = a.InventoryComp != null ? a.InventoryComp.CharacterInventory : null;
            if (inv == null || inv.GeneralSlots == null)
                return -1;
            for (int i = 0; i < inv.GeneralSlots.Length; i++)
                if (inv.GeneralSlots[i] != null && inv.GeneralSlots[i].Id == itemId)
                    return i;
            return -1;
        }

        private static SpellData FindPreparedSpell(CharacterController a, string spellId)
        {
            SpellcastingComponent sc = a.Spellcasting;
            if (sc == null)
                return null;
            // A spontaneous caster has no prepared slots: the PC menu offers its known spells with a slot left
            // (GetCastablePreparedSpells, SPL-123), so the step picks from the same list.
            if (sc.IsSpontaneousCaster)
            {
                foreach (SpellData known in sc.GetCastablePreparedSpells())
                    if (known != null && known.SpellId == spellId)
                        return known;
                return null;
            }
            if (sc.SpellSlots == null)
                return null;
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null && !slot.IsUsed && slot.PreparedSpell != null && slot.PreparedSpell.SpellId == spellId)
                    return slot.PreparedSpell;
            return null;
        }
    }
}
#endif
