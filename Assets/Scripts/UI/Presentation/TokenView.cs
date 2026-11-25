using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rendered mirror of a battlefield token (grouped AI turns step 3, docs/designs/grouped_ai_turns.md, "Run-ahead
/// state kept hidden"). The creature's own GameObject (the model) stays rules state: area tracking finds creatures with
/// FindObjectsOfType&lt;CharacterController&gt;, IsActiveCombatant reads activeInHierarchy, and the Lesser Globe reads the
/// model's transform. The model's SpriteRenderer keeps every value the game writes but is not drawn
/// (<c>forceRenderingOff</c>); this view, on its own root GameObject and never a CharacterController, draws it.
///
/// Live mode: every LateUpdate, after every other script (DefaultExecutionOrder 10000, so after CharacterController's
/// blur pulse and StatusEffectIndicator), the view copies the model's world position, rotation, lossy scale, active
/// state and sprite renderer (sprite, colour, enabled, flipX, flipY, sorting layer and order, material, draw mode and
/// size). It follows the model's destruction (<see cref="Release"/>, called from the model's OnDestroy, with a null
/// check in LateUpdate as a backstop). The view's root always stays active so that its LateUpdate keeps running; the
/// model's active state is mirrored on <see cref="Body"/>, which holds the renderer, the status icon container
/// (StatusEffectIndicator) and the summon visual (SummonedCreatureVisual).
///
/// Held mode (grouped AI turns step 9): while a group of AI turns is recorded and played back, the view stops copying
/// its model and shows what the playback applied to it: each Keyframe beat's picture (<see cref="ApplyPicture"/>, taken
/// from the model at a rules boundary) and, between keyframes, each Step beat's tween (<see cref="ShowStep"/>). The
/// model's StatusEffectIndicator is held with it, so its icons and tooltip follow the keyframes. A view created while
/// the presentation queues (a summon) is held and draws nothing until its Spawned beat; a model destroyed while it queues
/// leaves its view showing its last keyframe until its Removed beat (a despawn plays its fade there). When the queue
/// drains with no group open, every held view goes back to Live mode and copies its model at once
/// (<see cref="ReleaseAllHolds"/>, from CombatPresentation's drain and Clear). The blur pulse and the grapple blink do
/// not play while held (a held grappling token stays drawn; cosmetic).
///
/// Presentation only: the view never draws from UnityEngine.Random, never writes rules state, and never calls SetActive
/// or Destroy on its model. A reused pool slot (CRE-046) needs no reset: the view copies whatever its model shows.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class TokenView : MonoBehaviour
{
    private static readonly List<TokenView> _live = new List<TokenView>();

    private GameObject _model;
    private Transform _modelTransform;
    private SpriteRenderer _modelRenderer;
    private SpriteRenderer _renderer;
    private Transform _body;
    private SummonedCreatureVisual _summonVisual;
    private bool _released;
    private bool _held;
    private bool _hiddenUntilSpawn;
    private bool _pendingRemoval;
    private StatusEffectIndicator _heldIndicator;

    /// <summary>
    /// The picture last recorded for this view while holding (CombatPresentation records a Keyframe beat only when the
    /// model's picture differs from it); null after a hold starts, so the next boundary records one.
    /// </summary>
    internal TokenPicture LastRecorded;

    /// <summary>
    /// Where the last Step beat recorded for this view while holding ends (step 11): a keyframe that only puts the token
    /// there is a lane beat with the steps (CombatPresentation.EnqueueKeyframe). Null after a hold starts.
    /// </summary>
    internal Vector3? LastStepEnd;

    /// <summary>The object this view mirrors (null once it was destroyed).</summary>
    public GameObject Model => _model;

    /// <summary>The child that carries the drawn renderer and everything shown on the token; active while the model is.</summary>
    public Transform Body => _body;

    /// <summary>The drawn renderer (null for a view of a model without a SpriteRenderer, such as a Flaming Sphere).</summary>
    public SpriteRenderer Renderer => _renderer;

    /// <summary>The model's renderer the view copies (null for a model without one).</summary>
    public SpriteRenderer ModelRenderer => _modelRenderer;

    /// <summary>True once the view follows its model's destruction; its GameObject is gone at the end of the frame.</summary>
    public bool IsReleased => _released;

    /// <summary>True while the view shows the playback's keyframes instead of copying its model (Held mode, step 9).</summary>
    public bool IsHeld => _held;

    /// <summary>True while a view created during a recorded group waits for its Spawned beat (it draws nothing until then).</summary>
    public bool IsHiddenUntilSpawn => _hiddenUntilSpawn;

    /// <summary>True while the view of a model destroyed during a recorded group waits for its Removed beat.</summary>
    public bool IsPendingRemoval => _pendingRemoval;

    /// <summary>The summon aura, duration bar and badge hosted on this view, or null.</summary>
    public SummonedCreatureVisual SummonVisual => _summonVisual;

    /// <summary>Every view not yet released (for CleanCheck and the tests); destroyed entries are dropped first.</summary>
    public static IReadOnlyList<TokenView> Live
    {
        get
        {
            _live.RemoveAll(v => v == null);
            return _live;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _live.Clear();

    /// <summary>
    /// Creates the view of <paramref name="model"/> on a new root GameObject named after it, with a drawn
    /// SpriteRenderer when <paramref name="modelRenderer"/> is given, and copies the model once. The caller turns the
    /// model renderer's drawing off (<c>forceRenderingOff</c>). No UnityEngine.Random draw. Live mode is Play mode only:
    /// outside Play mode it returns null (no root is added to the open edit scene, where no OnDestroy would release it),
    /// and every caller then draws on the model's own transform as before. A view created while the presentation queues
    /// (a summon brought in during a recorded group, step 9) is held and hidden until its Spawned beat.
    /// </summary>
    public static TokenView Create(GameObject model, SpriteRenderer modelRenderer)
    {
        if (model == null || !Application.isPlaying)
            return null;

        GameObject root = new GameObject(model.name + " (view)");
        TokenView view = root.AddComponent<TokenView>();

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);

        view._body = body.transform;
        view._model = model;
        view._modelTransform = model.transform;
        view._modelRenderer = modelRenderer;
        if (modelRenderer != null)
            view._renderer = body.AddComponent<SpriteRenderer>();

        _live.Add(view);
        view.SyncFromModel();
        if (CombatPresentation.Queueing)
        {
            view.Hold();
            view.SetHidden(true);
            CombatPresentation.RecordSpawned(view);
        }
        return view;
    }

    private void LateUpdate()
    {
        if (_held)
        {
            // A model destroyed without its OnDestroy release (a backstop): follow it, through the presentation.
            if (_model == null && !_released)
                Release();
            return;
        }
        SyncFromModel();
    }

    /// <summary>
    /// The Live copy LateUpdate runs: the model's active state, world position, rotation, lossy scale and sprite
    /// renderer. A destroyed model releases the view. A held view (step 9) does nothing here: only
    /// <see cref="ReleaseHold"/> returns it to the model's copy.
    /// </summary>
    public void SyncFromModel()
    {
        if (_released || _held)
            return;

        if (_model == null)
        {
            Release();
            return;
        }

        bool active = _model.activeInHierarchy;
        if (_body.gameObject.activeSelf != active)
            _body.gameObject.SetActive(active);

        _body.SetPositionAndRotation(_modelTransform.position, _modelTransform.rotation);
        Vector3 scale = _modelTransform.lossyScale;
        if (_body.localScale != scale)
            _body.localScale = scale;

        if (_renderer == null)
            return;

        if (_modelRenderer == null)
        {
            if (_renderer.enabled)
                _renderer.enabled = false;
            return;
        }

        CopyRenderer(_modelRenderer, _renderer);
    }

    private static void CopyRenderer(SpriteRenderer from, SpriteRenderer to)
    {
        if (to.sprite != from.sprite) to.sprite = from.sprite;
        if (to.color != from.color) to.color = from.color;
        if (to.enabled != from.enabled) to.enabled = from.enabled;
        if (to.flipX != from.flipX) to.flipX = from.flipX;
        if (to.flipY != from.flipY) to.flipY = from.flipY;
        if (to.sortingLayerID != from.sortingLayerID) to.sortingLayerID = from.sortingLayerID;
        if (to.sortingOrder != from.sortingOrder) to.sortingOrder = from.sortingOrder;
        if (to.sharedMaterial != from.sharedMaterial) to.sharedMaterial = from.sharedMaterial;
        if (to.drawMode != from.drawMode) to.drawMode = from.drawMode;
        if (from.drawMode != SpriteDrawMode.Simple && to.size != from.size) to.size = from.size;
    }

    // ── Held mode (grouped AI turns step 9) ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts Held mode: the view keeps what it draws now and from here shows only what the playback applies. The model's
    /// status icons are held too. No-op on a released view.
    /// </summary>
    public void Hold()
    {
        if (_released || _held)
            return;
        _held = true;
        _maybeHeld = true;
        LastRecorded = null;
        LastStepEnd = null;
        HoldIndicator();
    }

    private void HoldIndicator()
    {
        StatusEffectIndicator ind = _model != null ? _model.GetComponent<StatusEffectIndicator>() : null;
        if (ind != null && !ind.IsHeld)
            ind.Hold();
        _heldIndicator = ind;
    }

    /// <summary>
    /// Ends Held mode: the view copies its model again, at once, and the status icons show the live state; a view still
    /// hidden for its spawn shows. A view waiting for its Removed beat stays as it is.
    /// </summary>
    public void ReleaseHold()
    {
        if (_hiddenUntilSpawn)
            SetHidden(false);
        if (!_held)
            return;
        _held = false;
        LastRecorded = null;
        LastStepEnd = null;
        if (_heldIndicator != null)
            _heldIndicator.ReleaseHold();
        _heldIndicator = null;
        SyncFromModel();
    }

    /// <summary>Holds every live view (a group opens).</summary>
    public static void HoldAll()
    {
        IReadOnlyList<TokenView> views = Live;
        for (int i = 0; i < views.Count; i++)
            if (views[i] != null)
                views[i].Hold();
    }

    /// <summary>Returns every live view to Live mode (the queue drained with no group open, or Clear).</summary>
    public static void ReleaseAllHolds()
    {
        if (!_maybeHeld)
            return;
        var views = new List<TokenView>(Live);
        for (int i = 0; i < views.Count; i++)
            if (views[i] != null)
                views[i].ReleaseHold();
        _maybeHeld = AnyHeld;
    }

    private static bool _maybeHeld;

    /// <summary>
    /// False when no view has been held or hidden since the last <see cref="ReleaseAllHolds"/> (a flag, no loop): the
    /// playback's idle frame reads it instead of <see cref="AnyHeld"/>.
    /// </summary>
    public static bool MaybeAnyHeld => _maybeHeld;

    /// <summary>True when any live view is held or hidden for its spawn.</summary>
    public static bool AnyHeld
    {
        get
        {
            IReadOnlyList<TokenView> views = Live;
            for (int i = 0; i < views.Count; i++)
                if (views[i] != null && (views[i]._held || views[i]._hiddenUntilSpawn))
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Hides (or shows again) everything the view draws without touching an active state: the Body stays active, so a
    /// summon visual added at rules time can still start its effect coroutine. CombatPresentation re-applies it at each
    /// boundary while the view waits, so a renderer built later (the summon aura, the status icons) is hidden too.
    /// </summary>
    internal void SetHidden(bool hidden)
    {
        _hiddenUntilSpawn = hidden;
        if (hidden)
            _maybeHeld = true;
        if (_body == null)
            return;
        Renderer[] renderers = _body.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].forceRenderingOff = hidden;
    }

    /// <summary>The Spawned beat: a view created during the group draws from here (its keyframes follow).</summary>
    internal void ApplySpawned()
    {
        if (this == null)
            return;
        if (_hiddenUntilSpawn)
            SetHidden(false);
    }

    /// <summary>
    /// A Keyframe beat: the held view draws <paramref name="p"/> (active state, position, rotation, scale, the sprite
    /// renderer's values and the held status icons).
    /// </summary>
    internal void ApplyPicture(TokenPicture p)
    {
        if (this == null || p == null || !_held || _body == null)
            return;
        if (_body.gameObject.activeSelf != p.Active)
            _body.gameObject.SetActive(p.Active);
        _body.SetPositionAndRotation(p.Position, p.Rotation);
        if (_body.localScale != p.Scale)
            _body.localScale = p.Scale;
        if (_renderer != null)
        {
            if (!p.HasRenderer)
            {
                if (_renderer.enabled)
                    _renderer.enabled = false;
            }
            else
            {
                if (_renderer.sprite != p.Sprite) _renderer.sprite = p.Sprite;
                if (_renderer.color != p.Color) _renderer.color = p.Color;
                if (_renderer.enabled != p.Enabled) _renderer.enabled = p.Enabled;
                if (_renderer.flipX != p.FlipX) _renderer.flipX = p.FlipX;
                if (_renderer.flipY != p.FlipY) _renderer.flipY = p.FlipY;
                if (_renderer.sortingLayerID != p.SortingLayerId) _renderer.sortingLayerID = p.SortingLayerId;
                if (_renderer.sortingOrder != p.SortingOrder) _renderer.sortingOrder = p.SortingOrder;
                if (_renderer.sharedMaterial != p.Material) _renderer.sharedMaterial = p.Material;
                if (_renderer.drawMode != p.DrawMode) _renderer.drawMode = p.DrawMode;
                if (p.DrawMode != SpriteDrawMode.Simple && _renderer.size != p.Size) _renderer.size = p.Size;
            }
        }
        if (_heldIndicator == null)
            HoldIndicator();
        if (_heldIndicator != null && p.Icons != null)
            _heldIndicator.ApplyHeld(p.Icons);
    }

    /// <summary>A Step beat in progress: the held view stands at <paramref name="position"/> (the playback's tween).</summary>
    internal void ShowStep(Vector3 position)
    {
        if (this == null || !_held || _body == null)
            return;
        _body.position = position;
    }

    /// <summary>
    /// The Removed beat: the view of a model destroyed during the group goes now; a despawn plays its fade first
    /// (<paramref name="despawn"/>, <see cref="PlayDespawnAndRelease"/>).
    /// </summary>
    internal void ApplyRemoved(bool despawn)
    {
        if (this == null)
            return;
        _pendingRemoval = false;
        if (despawn && Application.isPlaying && gameObject.activeInHierarchy)
        {
            StartCoroutine(DespawnThenDestroy());
            return;
        }
        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }

    /// <summary>
    /// True when the drawn state equals the model's (active state, position, scale and, with a renderer, sprite,
    /// colour, enabled and flips); <paramref name="problem"/> names the first difference. The tests read it after a
    /// LateUpdate; CleanCheck after its own <see cref="SyncFromModel"/>, since a coroutine resumes before LateUpdate and
    /// a model changed earlier in that frame (a size animation, the grapple blink) is one copy ahead of its view. A held
    /// view is never synced (step 9: the playback has not drained).
    /// </summary>
    public bool IsSyncedWithModel(out string problem)
    {
        problem = null;
        if (_released || _model == null)
        {
            problem = "released or model gone";
            return false;
        }
        if (_held || _hiddenUntilSpawn)
        {
            problem = "held by the presentation (the playback has not drained)";
            return false;
        }
        if (_body.gameObject.activeSelf != _model.activeInHierarchy)
            problem = "active " + _body.gameObject.activeSelf + " vs model " + _model.activeInHierarchy;
        else if ((_body.position - _modelTransform.position).sqrMagnitude > 1e-8f)
            problem = "position " + _body.position + " vs model " + _modelTransform.position;
        else if ((_body.lossyScale - _modelTransform.lossyScale).sqrMagnitude > 1e-8f)
            problem = "scale " + _body.lossyScale + " vs model " + _modelTransform.lossyScale;
        else if (_renderer != null && _modelRenderer != null)
        {
            if (_renderer.sprite != _modelRenderer.sprite)
                problem = "sprite " + (_renderer.sprite != null ? _renderer.sprite.name : "none") + " vs model "
                    + (_modelRenderer.sprite != null ? _modelRenderer.sprite.name : "none");
            else if (_renderer.color != _modelRenderer.color)
                problem = "colour " + _renderer.color + " vs model " + _modelRenderer.color;
            else if (_renderer.enabled != _modelRenderer.enabled)
                problem = "enabled " + _renderer.enabled + " vs model " + _modelRenderer.enabled;
            else if (_renderer.flipX != _modelRenderer.flipX || _renderer.flipY != _modelRenderer.flipY)
                problem = "flip differs from the model";
            else if (!_modelRenderer.forceRenderingOff)
                problem = "the model renderer is drawn too (forceRenderingOff is off)";
        }
        return problem == null;
    }

    /// <summary>
    /// The summon visual on this view, added on first use (the summon spawn). It copies the model first, so a model
    /// reactivated this frame (a reused slot) has an active Body before the visual starts its summon effect coroutine.
    /// Only the field is trusted: the view is the visual's only host, so a fresh one is added whenever it is null.
    /// </summary>
    public SummonedCreatureVisual EnsureSummonVisual()
    {
        SyncFromModel();
        if (_summonVisual == null && _body != null)
            _summonVisual = _body.gameObject.AddComponent<SummonedCreatureVisual>();
        return _summonVisual;
    }

    /// <summary>
    /// Removes the summon visual and the objects it built (a slot that gets a new creature, CRE-046; summons are never
    /// pooled today, so this is a guard). Immediate, like ResetForNewCreature's removal of the other per-creature
    /// components, so a summon placed on the slot in the same frame gets a fresh visual, never the dying one.
    /// </summary>
    public void ClearSummonVisual()
    {
        if (_summonVisual == null)
            return;
        SummonedCreatureVisual visual = _summonVisual;
        _summonVisual = null;
        DestroyImmediate(visual);
    }

    /// <summary>
    /// The view's name for diagnostics: its root's name (frozen at creation, so possibly an earlier creature of a
    /// reused slot), the model's current name and its creature's name.
    /// </summary>
    public string Describe()
    {
        string root = this != null ? name : "(view gone)";
        if (_model == null)
            return root + " [model gone]";
        CharacterController cc = _model.GetComponent<CharacterController>();
        string creature = cc != null && cc.Stats != null ? cc.Stats.CharacterName : null;
        return root + " [model " + _model.name + (string.IsNullOrEmpty(creature) ? "" : ", " + creature) + "]";
    }

    /// <summary>
    /// Follows the model's destruction: the view leaves <see cref="Live"/> at once and its GameObject (with the status
    /// icons and the summon visual on it) is destroyed at the end of the frame, unless it is playing a despawn
    /// (<see cref="PlayDespawnAndRelease"/>), which destroys it when the effect ends, or the presentation queues (step 9):
    /// then the view keeps its last keyframe and goes at its Removed beat.
    /// </summary>
    public void Release()
    {
        if (_released)
            return;
        if (this != null && !_despawning && Application.isPlaying && CombatPresentation.Queueing)
        {
            // Hold and record the last picture while the model is still readable (and before _released, which Hold
            // refuses), then wait for the Removed beat.
            if (!_held)
                Hold();
            CombatPresentation.RecordFinalKeyframe(this);
            _released = true;
            _live.Remove(this);
            _pendingRemoval = true;
            CombatPresentation.RecordRemoved(this, false);
            return;
        }
        _released = true;
        _live.Remove(this);
        if (this == null || _despawning)
            return;
        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }

    private bool _despawning;

    /// <summary>True while the view plays a despawn after its model left play (<see cref="PlayDespawnAndRelease"/>).</summary>
    public bool IsDespawning => _despawning;

    /// <summary>
    /// A summon leaves play (grouped AI turns step 4, CRE-312): GameManager.DespawnSummonWithEffect does all its rules
    /// work and destroys the model at once, then the view, detached from the model, keeps the token's last drawn state,
    /// plays the summon visual's despawn fade (<see cref="SummonedCreatureVisual.PlayDespawnEffect"/>, 0.45 s of game
    /// time) and destroys itself. Presentation only: no rule waits on it, it is never in <see cref="Live"/>, and area
    /// tracking (FindObjectsOfType&lt;CharacterController&gt;) and IsActiveCombatant never see it, since the view is not a
    /// controller. Call it before the model's Destroy. Outside Play mode it releases the view at once. While the
    /// presentation queues (step 9) the model's last state becomes the view's last keyframe and the fade plays at the
    /// view's Removed beat.
    /// </summary>
    public void PlayDespawnAndRelease()
    {
        if (_released || _despawning)
            return;
        bool queued = Application.isPlaying && CombatPresentation.Queueing;
        if (queued)
        {
            if (!_held)
                Hold();
            CombatPresentation.RecordFinalKeyframe(this);
        }
        else
        {
            SyncFromModel(); // the last state the token showed, the death sprite or tint included
        }
        _released = true;
        _live.Remove(this);
        _model = null;
        _modelTransform = null;
        _modelRenderer = null;
        if (!Application.isPlaying)
        {
            DestroyImmediate(gameObject);
            return;
        }
        _despawning = true;
        if (queued)
        {
            _pendingRemoval = true;
            CombatPresentation.RecordRemoved(this, true);
            return;
        }
        StartCoroutine(DespawnThenDestroy());
    }

    private System.Collections.IEnumerator DespawnThenDestroy()
    {
        SummonedCreatureVisual visual = _summonVisual;
        if (visual != null && _body != null && _body.gameObject.activeInHierarchy)
            yield return visual.PlayDespawnEffect(); // nested on this view, so the view's own destruction stops it
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        _released = true;
        _live.Remove(this);
    }
}
