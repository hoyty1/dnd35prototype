using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rendered mirror of a battlefield token (grouped AI turns step 3, docs/designs/grouped_ai_turns.md, "Run-ahead
/// state kept hidden"). The creature's own GameObject (the model) stays rules state: area tracking finds creatures with
/// FindObjectsOfType&lt;CharacterController&gt;, IsActiveCombatant reads activeInHierarchy, and the Lesser Globe reads the
/// model's transform. The model's SpriteRenderer keeps every value the game writes but is not drawn
/// (<c>forceRenderingOff</c>); this view, on its own root GameObject and never a CharacterController, draws it.
///
/// Live mode (the only mode until step 9 adds Held mode): every LateUpdate, after every other script (DefaultExecutionOrder 10000, so
/// after CharacterController's blur pulse and StatusEffectIndicator), the view copies the model's world position,
/// rotation, lossy scale, active state and sprite renderer (sprite, colour, enabled, flipX, flipY, sorting layer and
/// order, material, draw mode and size). It follows the model's destruction (<see cref="Release"/>, called from the
/// model's OnDestroy, with a null check in LateUpdate as a backstop). The view's root always stays active so that its
/// LateUpdate keeps running; the model's active state is mirrored on <see cref="Body"/>, which holds the renderer, the
/// status icon container (StatusEffectIndicator) and the summon visual (SummonedCreatureVisual).
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
    /// and every caller then draws on the model's own transform as before.
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
        return view;
    }

    private void LateUpdate()
    {
        SyncFromModel();
    }

    /// <summary>
    /// The Live copy LateUpdate runs: the model's active state, world position, rotation, lossy scale and sprite
    /// renderer. A destroyed model releases the view.
    /// </summary>
    public void SyncFromModel()
    {
        if (_released)
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

    /// <summary>
    /// True when the drawn state equals the model's (active state, position, scale and, with a renderer, sprite,
    /// colour, enabled and flips); <paramref name="problem"/> names the first difference. The tests read it after a
    /// LateUpdate; CleanCheck after its own <see cref="SyncFromModel"/>, since a coroutine resumes before LateUpdate and
    /// a model changed earlier in that frame (a size animation, the grapple blink) is one copy ahead of its view.
    /// </summary>
    public bool IsSyncedWithModel(out string problem)
    {
        problem = null;
        if (_released || _model == null)
        {
            problem = "released or model gone";
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
    /// (<see cref="PlayDespawnAndRelease"/>), which destroys it when the effect ends.
    /// </summary>
    public void Release()
    {
        if (_released)
            return;
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
    /// controller. Call it before the model's Destroy. Outside Play mode it releases the view at once.
    /// </summary>
    public void PlayDespawnAndRelease()
    {
        if (_released || _despawning)
            return;
        SyncFromModel(); // the last state the token showed, the death sprite or tint included
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
