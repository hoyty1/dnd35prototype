using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What a recorded presentation beat shows (grouped AI turns, docs/designs/grouped_ai_turns.md "Recording (resolve now,
/// show later)"). Step 8 builds the facade in Passthrough only, so no beat is queued in play yet: every facade call
/// applies at once, as before. Step 9 (gt-run-ahead) adds Holding, which records the kinds only a group has (Pace, Step,
/// Keyframe, Spawned, Removed) besides the facade's, and the ground-item token's beat (UI-058).
/// </summary>
public enum BeatKind
{
    /// <summary>CombatUI.ShowCombatLog (its ScenarioHooks.CombatLog hook already ran at rules time).</summary>
    Log,
    /// <summary>CombatUI.AddTurnSeparator.</summary>
    TurnSeparator,
    /// <summary>CombatUI.SetTurnIndicator.</summary>
    TurnIndicator,
    /// <summary>CombatUI.UpdateInitiativeDisplay, with the string built at call time.</summary>
    Initiative,
    /// <summary>CombatUI.SetActivePC, with the index and the creature (the creature overload).</summary>
    ActivePC,
    /// <summary>CombatUI.SetActiveNPC, with the index and the creature (the creature overload).</summary>
    ActiveNPC,
    /// <summary>CombatUI.SetActionButtonsVisible.</summary>
    ActionButtonsVisible,
    /// <summary>A stat panel snapshot (CombatUI.UpdateAllStats4PC and the legacy variants; CharacterInfoPanel.Capture*).</summary>
    Panel,
    /// <summary>The control beat: CombatUI.ShowActionButtonsForCharacter (S1).</summary>
    Control,
    /// <summary>An interactive barrier: CombatUI.ShowConfirmationDialog, CombatUI.ShowDefeatPanel (S3, S4).</summary>
    Interactive,
    /// <summary>SquareCell.SetHighlight(HighlightType).</summary>
    CellHighlight,
    /// <summary>SquareCell.SetHighlight(Color) and SquareCell.ClearHighlight (the custom overlay).</summary>
    CellOverlay,
    /// <summary>PersistentAreaEffect.CreateVisualIndicator.</summary>
    AreaShown,
    /// <summary>The area indicator's destruction.</summary>
    AreaHidden,
    /// <summary>A one-shot visual or a rarely used facade call, as a closure over call-time values (CombatPresentation.Defer).</summary>
    Deferred,
    /// <summary>
    /// A rarely used Queued-class facade call (CombatUI.AddCombatMessage, LogAttack, LogDamage) as a closure over its call
    /// arguments; its apply writes its own trace line, so the beat adds no "defer" line.
    /// </summary>
    QueuedClosure,
    /// <summary>A pacing wait while holding (CombatPacing.Wait): plays for its seconds divided by the AI speed, shows nothing.</summary>
    Pace,
    /// <summary>A MoveAlongPath step while holding (CombatPresentation.RecordStep): the token's view tweens over the step.</summary>
    Step,
    /// <summary>A token's picture at a boundary while holding (CombatPresentation.Boundary): its view shows it.</summary>
    Keyframe,
    /// <summary>A token created while queueing (a summon): its view stays hidden until this beat.</summary>
    Spawned,
    /// <summary>A token destroyed (or despawned) while queueing: its view keeps its last keyframe until this beat, then goes.</summary>
    Removed,
    /// <summary>SquareCell's ground-item token shows or hides (AddGroundItem, RemoveGroundItem; UI-058).</summary>
    GroundItem,
    /// <summary>
    /// A window stop (CombatPresentation.MarkStop, step 10: S2, a player-run creature's automatic reaction; S7 reserved): it
    /// shows nothing; step 11's overlap scheduler ends the window at it. <see cref="PresentationBeat.Index"/> holds the StopKind.
    /// </summary>
    WindowStop,
}

/// <summary>
/// One presentation beat: its kind, its rules order (<see cref="Seq"/>) and the values snapshotted at call time. Nothing
/// held here is a reference into a live rules list or a reader of live state: creatures are kept only to resolve a
/// highlight or a token against, and UI targets (Text, Image, cells, area indicators) are presentation objects.
/// </summary>
public sealed class PresentationBeat
{
    public BeatKind Kind;
    /// <summary>Rules order: the order the calls were made in.</summary>
    public int Seq;
    public CombatUI Ui;
    public string Text;
    public int Index;
    public bool Flag;
    public CharacterController Creature;
    public PanelSnapshot Panel;
    public SquareCell Cell;
    public HighlightType Highlight;
    public Color Color;
    public PersistentAreaEffect Area;
    public GameObject Indicator;
    /// <summary>The show of a Deferred, Control or Interactive beat (a closure over call-time values).</summary>
    public Action Show;
    /// <summary>A Pace beat's seconds, or a Step beat's seconds per step (both divided by the AI speed when played).</summary>
    public float Seconds;
    /// <summary>The pacing kind in force when a Pace or Step beat was recorded.</summary>
    public PaceKind Pace;
    /// <summary>The token view of a Step, Keyframe, Spawned or Removed beat (a presentation object).</summary>
    public TokenView View;
    /// <summary>A Keyframe beat's picture (values and asset references taken at the boundary).</summary>
    public TokenPicture Picture;
    /// <summary>A Step beat's world positions.</summary>
    public Vector3 From, To;
    /// <summary>The renderers an AreaShown beat turns back on.</summary>
    public Renderer[] Renderers;

    public static PresentationBeat Of(BeatKind kind, CombatUI ui) => new PresentationBeat { Kind = kind, Ui = ui };
}

/// <summary>
/// One row write of the stat panels, captured at call time by CharacterInfoPanel (the Capture half of the former
/// UpdateCharacterStats and UpdateBuffDisplay) and written by its Apply half. A stats row holds the text of each field
/// whose target exists (null where the original wrote nothing); a buff row holds the buff line and whether it shows.
/// </summary>
public sealed class PanelRowSnapshot
{
    /// <summary>The slot, for the trace: "PC1".."PC4", "NPC" (the legacy main NPC row) or "NPC0", "NPC1"...</summary>
    public string Slot;
    public bool IsBuffRow;
    public CharacterController Creature;
    public bool Dead;

    // Stats row: the targets and the captured values (a value is null when its target is null).
    public Text NameText, HpText, AcText, AtkText, SpeedText, AbilityText;
    public Image HpBar;
    public string Name, Hp, Ac, Atk, Speed, Ability;
    public float BarFill;
    public Color BarColor;

    // Buff row.
    public Text BuffText;
    public string Buff;
    /// <summary>The buff row's GameObject state: null leaves it as it is (no creature), false hides it, true shows it.</summary>
    public bool? BuffShown;

    /// <summary>The row as one string for the presentation trace (unhashed).</summary>
    public string Encode()
    {
        var sb = new StringBuilder();
        if (IsBuffRow)
        {
            sb.Append(BuffShown.HasValue ? (BuffShown.Value ? "show" : "hide") : "keep").Append('|').Append(Buff ?? "");
            return sb.ToString();
        }
        sb.Append(Name ?? "~").Append('|').Append(Hp ?? "~").Append('|').Append(Ac ?? "~").Append('|').Append(Atk ?? "~")
          .Append('|').Append(Speed ?? "~").Append('|').Append(Ability ?? "~").Append('|');
        if (HpBar != null)
            sb.Append(BarFill.ToString("0.###", CultureInfo.InvariantCulture)).Append('@').Append(PresentationText.ColorText(BarColor));
        else
            sb.Append('~');
        sb.Append(Dead ? "|dead" : "");
        return sb.ToString();
    }
}

/// <summary>The rows of one stat panel update, in the order the original update wrote them.</summary>
public sealed class PanelSnapshot
{
    public readonly List<PanelRowSnapshot> Rows = new List<PanelRowSnapshot>();
}

/// <summary>
/// A token's picture at a rules boundary (CombatPresentation.Boundary): what its view shows at the next yield. Read from
/// the model (the view copies it in its LateUpdate). The blur pulse's tint and the grapple blink's momentary
/// <c>enabled</c> follow the wall clock, so a keyframe keeps the blur flag (and only the alpha) and the grapple partner
/// instead. Pure reads only: no die, no rules write, no lazy component.
/// </summary>
public struct TokenKeyframe
{
    public bool HasRenderer;
    public string Sprite;
    public Color Color;
    public bool Enabled;
    public bool FlipX;
    public Vector3 Scale;
    public Vector3 Position;
    public bool Active;
    public string Icons;
    public bool Blur;
    public string GrapplePartner;

    /// <summary>The keyframe of <paramref name="view"/>'s model (default when the model is gone).</summary>
    public static TokenKeyframe Capture(TokenView view)
    {
        var kf = new TokenKeyframe();
        GameObject model = view != null ? view.Model : null;
        if (model == null)
            return kf;

        Transform t = model.transform;
        kf.Active = model.activeInHierarchy;
        kf.Position = t.position;
        kf.Scale = t.lossyScale;
        SpriteRenderer sr = view.ModelRenderer;
        if (sr != null)
        {
            kf.HasRenderer = true;
            kf.Sprite = sr.sprite != null ? sr.sprite.name : "";
            kf.Color = sr.color;
            kf.Enabled = sr.enabled;
            kf.FlipX = sr.flipX;
        }

        CharacterController c = model.GetComponent<CharacterController>();
        if (c != null)
        {
            kf.Blur = c.Stats != null && c.HasActiveBlurEffect;
            CharacterController partner = c.PeekGrappleOpponent();
            if (partner != null)
                kf.GrapplePartner = partner.Stats != null ? partner.Stats.CharacterName : partner.name;
            StatusEffectIndicator icons = model.GetComponent<StatusEffectIndicator>();
            kf.Icons = icons != null ? icons.CaptureIconSignature() : "";
        }
        return kf;
    }

    /// <summary>The keyframe as one string for the presentation trace (unhashed): equal strings show the same picture.</summary>
    public string Encode()
    {
        var sb = new StringBuilder();
        sb.Append("act=").Append(Active ? '1' : '0');
        sb.Append("|p=").Append(PresentationText.Num(Position.x)).Append(',').Append(PresentationText.Num(Position.y));
        sb.Append("|s=").Append(PresentationText.Num(Scale.x)).Append(',').Append(PresentationText.Num(Scale.y));
        if (HasRenderer)
        {
            sb.Append("|spr=").Append(Sprite);
            sb.Append("|c=").Append(Blur ? "blur@" + PresentationText.Num(Color.a) : PresentationText.ColorText(Color));
            sb.Append("|en=").Append(GrapplePartner != null ? '~' : (Enabled ? '1' : '0'));
            sb.Append("|fx=").Append(FlipX ? '1' : '0');
        }
        if (GrapplePartner != null)
            sb.Append("|gr=").Append(GrapplePartner);
        if (!string.IsNullOrEmpty(Icons))
            sb.Append("|ic=").Append(Icons);
        return sb.ToString();
    }
}

/// <summary>
/// What a token's view draws, taken from its model at a boundary while holding (grouped AI turns step 9): the values and
/// asset references a Keyframe beat applies to the held view (TokenView.ApplyPicture). Pure reads, like TokenKeyframe.
/// The grapple blink's momentary <c>enabled</c> is not kept (a grappling token shows as drawn), and the blur pulse's tint
/// is the one at the boundary (the pulse does not play while held; cosmetic).
/// </summary>
public sealed class TokenPicture
{
    public bool Active;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
    public bool HasRenderer;
    public Sprite Sprite;
    public Color Color;
    public bool Enabled;
    public bool FlipX;
    public bool FlipY;
    public int SortingLayerId;
    public int SortingOrder;
    public Material Material;
    public SpriteDrawMode DrawMode;
    public Vector2 Size;
    public List<StatusEffectIndicator.IconData> Icons;
    public string IconSignature;

    /// <summary>The picture of <paramref name="view"/>'s model, or null when the model is gone.</summary>
    public static TokenPicture Capture(TokenView view)
    {
        GameObject model = view != null ? view.Model : null;
        if (model == null)
            return null;
        var p = new TokenPicture();
        Transform t = model.transform;
        p.Active = model.activeInHierarchy;
        p.Position = t.position;
        p.Rotation = t.rotation;
        p.Scale = t.lossyScale;
        SpriteRenderer sr = view.ModelRenderer;
        CharacterController c = model.GetComponent<CharacterController>();
        if (sr != null)
        {
            p.HasRenderer = true;
            p.Sprite = sr.sprite;
            p.Color = sr.color;
            // The grapple blink toggles the model's enabled on the wall clock: a held grappling token stays drawn.
            p.Enabled = sr.enabled || (c != null && c.PeekGrappleOpponent() != null);
            p.FlipX = sr.flipX;
            p.FlipY = sr.flipY;
            p.SortingLayerId = sr.sortingLayerID;
            p.SortingOrder = sr.sortingOrder;
            p.Material = sr.sharedMaterial;
            p.DrawMode = sr.drawMode;
            p.Size = sr.drawMode != SpriteDrawMode.Simple ? sr.size : Vector2.zero;
        }
        StatusEffectIndicator icons = c != null ? model.GetComponent<StatusEffectIndicator>() : null;
        if (icons != null)
        {
            p.Icons = icons.CaptureIcons();
            p.IconSignature = icons.SignatureOf(p.Icons);
        }
        return p;
    }

    /// <summary>True when <paramref name="other"/> draws the same picture.</summary>
    public bool SameAs(TokenPicture other)
    {
        if (other == null)
            return false;
        return Active == other.Active && Position == other.Position && Rotation == other.Rotation && Scale == other.Scale
               && HasRenderer == other.HasRenderer && Sprite == other.Sprite && Color == other.Color && Enabled == other.Enabled
               && FlipX == other.FlipX && FlipY == other.FlipY && SortingLayerId == other.SortingLayerId
               && SortingOrder == other.SortingOrder && Material == other.Material && DrawMode == other.DrawMode
               && Size == other.Size && string.Equals(IconSignature, other.IconSignature, StringComparison.Ordinal);
    }
}

/// <summary>Invariant number and colour text for the presentation trace.</summary>
public static class PresentationText
{
    public static string Num(float v) => (Mathf.Round(v * 100f) / 100f).ToString("0.##", CultureInfo.InvariantCulture);

    public static string ColorText(Color c) => Num(c.r) + "," + Num(c.g) + "," + Num(c.b) + "," + Num(c.a);
}
