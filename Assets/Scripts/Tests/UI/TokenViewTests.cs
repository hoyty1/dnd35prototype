using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.UI
{
/// <summary>
/// Grouped AI turns step 3 (docs/designs/grouped_ai_turns.md, "Run-ahead state kept hidden"): every token renders
/// through its mirror view (TokenView, UI/Presentation/TokenView.cs). The suites run synchronously, so "after
/// LateUpdate" calls the view's own LateUpdate (by reflection, TST-006) where a frame would. Checks:
/// the view is its own root GameObject, never a CharacterController, and the model's renderer is not drawn;
/// the copy of sprite, colour, enabled, flipX, flipY, sorting order, lossy scale and position after LateUpdate;
/// the model's inactive state and destruction follow;
/// a reused enemy pool slot (CRE-046, through the real spawn GameManager.InitializeNPCFromDefinition) keeps its view
/// and shows the new creature's sprite, and its old summon visual is gone;
/// the status icon container and a summon's aura and duration bar sit on the view, not on the creature; a lost icon
/// container is rebuilt on the view; a cleared summon visual is gone at once and the next one is fresh; a model
/// reactivated in the frame gets an active Body before its summon visual starts;
/// a Flaming Sphere draws its quads on its own view, which follows it.
/// Needs Play mode (Awake must run). The test objects sit off the grid and are destroyed afterwards.
/// </summary>
public static class TokenViewTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int SlotPos = new Vector2Int(-60, -60);

    // Textures and sprites the suite builds, destroyed at the end of the run.
    private static readonly List<Object> _builtAssets = new List<Object>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== TOKEN VIEW TESTS (grouped AI turns step 3) ==========");

        if (!Application.isPlaying)
        {
            Assert(false, "Token view tests need Play mode (CharacterController.Awake creates the view)");
        }
        else
        {
            TestViewIsItsOwnRoot();
            TestCopiesModelAfterLateUpdate();
            TestInactiveStateFollows();
            TestDestructionFollows();
            TestStatusIconsOnView();
            TestSummonVisualOnView();
            if (GameManager.Instance != null)
                TestReusedPoolSlotShowsNewSprite();
            else
                Assert(false, "The pool slot check needs a scene GameManager");
            TestFlamingSphereView();
        }

        foreach (Object asset in _builtAssets)
            if (asset != null)
                Object.DestroyImmediate(asset);
        _builtAssets.Clear();

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}");
        }
    }

    private static readonly MethodInfo ViewLateUpdate =
        typeof(TokenView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

    private static void RunLateUpdate(TokenView view)
    {
        if (view != null && ViewLateUpdate != null)
            ViewLateUpdate.Invoke(view, null);
    }

    private static CharacterController NewController(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<SpriteRenderer>();
        return go.AddComponent<CharacterController>();
    }

    private static Sprite NewSprite(string name, Color color)
    {
        var tex = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        sprite.name = name;
        _builtAssets.Add(sprite);
        _builtAssets.Add(tex);
        return sprite;
    }

    private static void Destroy(CharacterController cc)
    {
        if (cc == null)
            return;
        TokenView view = cc.View;
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.Grid != null)
            gm.Grid.ClearCreatureOccupancy(cc);
        cc.ReleaseGrappleState("TokenViewTests cleanup");
        Object.DestroyImmediate(cc.gameObject);
        if (view != null)
            Object.DestroyImmediate(view.gameObject);
    }

    private static void TestViewIsItsOwnRoot()
    {
        CharacterController cc = NewController("TokenView_Root");
        try
        {
            TokenView view = cc.View;
            Assert(view != null, "Awake creates the token's view");
            if (view == null)
                return;
            Assert(view.gameObject != cc.gameObject && view.transform.parent == null, "The view is its own root GameObject");
            Assert(view.GetComponentInChildren<CharacterController>(true) == null, "The view carries no CharacterController (area tracking finds only the model)");
            Assert(view.Model == cc.gameObject && view.ModelRenderer == cc.GetComponent<SpriteRenderer>(), "The view mirrors its controller's GameObject and SpriteRenderer");
            Assert(cc.GetComponent<SpriteRenderer>().forceRenderingOff, "The model's SpriteRenderer is not drawn (forceRenderingOff)");
            Assert(view.Renderer != null && !view.Renderer.forceRenderingOff, "The view's own SpriteRenderer is drawn");
            Assert(cc.transform.childCount == 0, "Nothing is parented to the model token (status icons and summon visuals are on the view)");
            bool listed = false;
            foreach (TokenView v in TokenView.Live)
                if (v == view) listed = true;
            Assert(listed, "The view is listed in TokenView.Live");
        }
        finally
        {
            Destroy(cc);
        }
    }

    private static void TestCopiesModelAfterLateUpdate()
    {
        CharacterController cc = NewController("TokenView_Copy");
        try
        {
            TokenView view = cc.View;
            SpriteRenderer model = cc.GetComponent<SpriteRenderer>();
            Sprite sprite = NewSprite("TokenView_CopySprite", Color.red);
            model.sprite = sprite;
            model.color = new Color(0.4f, 0.6f, 0.8f, 0.45f);
            model.flipX = true;
            model.flipY = true;
            model.sortingOrder = 17;
            model.enabled = false;
            cc.transform.position = new Vector3(-73.5f, -71.25f, 0.5f);
            cc.transform.localScale = new Vector3(2f, 2f, 1f);

            Assert(view.Renderer.sprite != sprite, "Control: before LateUpdate the view still shows the old sprite");
            RunLateUpdate(view);

            SpriteRenderer drawn = view.Renderer;
            Assert(drawn.sprite == sprite, "After LateUpdate the view shows the model's sprite");
            Assert(drawn.color == model.color, "After LateUpdate the view has the model's colour (invisibility alpha included)");
            Assert(!drawn.enabled, "After LateUpdate the view has the model's enabled state (the grapple blink)");
            Assert(drawn.flipX && drawn.flipY, "After LateUpdate the view has the model's flipX and flipY");
            Assert(drawn.sortingOrder == 17, "After LateUpdate the view has the model's sorting order");
            Assert((view.Body.position - cc.transform.position).sqrMagnitude < 1e-8f, "After LateUpdate the view is at the model's position");
            Assert((view.Body.lossyScale - cc.transform.lossyScale).sqrMagnitude < 1e-8f, "After LateUpdate the view has the model's lossy scale (Enlarge, Reduce)");
            Assert(view.IsSyncedWithModel(out string problem), "IsSyncedWithModel after LateUpdate" + (problem != null ? " (" + problem + ")" : ""));

            model.enabled = true;
            model.color = Color.white;
            Assert(!view.IsSyncedWithModel(out _), "Control: a change after the copy is unsynced until the next LateUpdate");
            RunLateUpdate(view);
            Assert(drawn.enabled && drawn.color == Color.white, "The next LateUpdate copies the change");
        }
        finally
        {
            Destroy(cc);
        }
    }

    private static void TestInactiveStateFollows()
    {
        CharacterController cc = NewController("TokenView_Inactive");
        try
        {
            TokenView view = cc.View;
            cc.gameObject.SetActive(false);
            RunLateUpdate(view);
            Assert(view.gameObject.activeSelf, "The view's root stays active so it keeps copying");
            Assert(!view.Body.gameObject.activeSelf, "An inactive model hides its view's body");
            cc.gameObject.SetActive(true);
            RunLateUpdate(view);
            Assert(view.Body.gameObject.activeSelf, "A reactivated model shows its view's body again");
        }
        finally
        {
            Destroy(cc);
        }
    }

    private static void TestDestructionFollows()
    {
        CharacterController cc = NewController("TokenView_Destroyed");
        TokenView view = cc.View;
        GameObject viewObject = view != null ? view.gameObject : null;
        GameObject model = null;
        GameObject orphanObject = null;
        try
        {
            Object.DestroyImmediate(cc.gameObject);
            cc = null;
            Assert(view != null && view.IsReleased, "A destroyed model releases its view (CharacterController.OnDestroy)");
            bool listed = false;
            foreach (TokenView v in TokenView.Live)
                if (v == view) listed = true;
            Assert(!listed, "A released view leaves TokenView.Live at once");

            // The backstop: a model gone without its OnDestroy release (a sphere-like object) is noticed in LateUpdate.
            model = new GameObject("TokenView_Backstop");
            TokenView orphan = TokenView.Create(model, null);
            orphanObject = orphan != null ? orphan.gameObject : null;
            Object.DestroyImmediate(model);
            model = null;
            RunLateUpdate(orphan);
            Assert(orphan != null && orphan.IsReleased, "LateUpdate releases a view whose model is gone");
        }
        finally
        {
            if (cc != null)
                Destroy(cc);
            if (model != null)
                Object.DestroyImmediate(model);
            if (viewObject != null)
                Object.DestroyImmediate(viewObject);
            if (orphanObject != null)
                Object.DestroyImmediate(orphanObject);
        }
    }

    private static void TestStatusIconsOnView()
    {
        CharacterController cc = NewController("TokenView_Icons");
        try
        {
            var indicator = cc.GetComponent<StatusEffectIndicator>();
            Assert(indicator != null, "Control: Awake adds the StatusEffectIndicator");
            MethodInfo start = typeof(StatusEffectIndicator).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(start != null, "StatusEffectIndicator.Start exists (TST-006)");
            if (indicator == null || start == null)
                return;
            start.Invoke(indicator, null);
            FieldInfo containerField = typeof(StatusEffectIndicator).GetField("_iconContainer", BindingFlags.Instance | BindingFlags.NonPublic);
            var container = containerField != null ? containerField.GetValue(indicator) as Transform : null;
            Assert(container != null && container.parent == cc.View.Body, "The status icon container sits on the view's body");
            Assert(cc.transform.childCount == 0, "The status icon container is not under the model token");

            // The container lives under the view's root: if it goes while the token stays, LateUpdate rebuilds it there.
            MethodInfo late = typeof(StatusEffectIndicator).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (container != null && late != null)
            {
                Object.DestroyImmediate(container.gameObject);
                bool threw = false;
                try { late.Invoke(indicator, null); }
                catch (TargetInvocationException) { threw = true; }
                var rebuilt = containerField.GetValue(indicator) as Transform;
                Assert(!threw && rebuilt != null && rebuilt.parent == cc.View.Body, "A lost icon container is rebuilt on the view's body, without an exception");
            }
        }
        finally
        {
            Destroy(cc);
        }
    }

    private static void TestSummonVisualOnView()
    {
        CharacterController cc = NewController("TokenView_Summon");
        try
        {
            SummonedCreatureVisual visual = cc.EnsureSummonVisual();
            Assert(visual != null && visual.gameObject == cc.View.Body.gameObject, "EnsureSummonVisual hosts the summon visual on the view's body");
            Assert(cc.GetComponent<SummonedCreatureVisual>() == null, "The model has no SummonedCreatureVisual of its own");
            Assert(cc.SummonVisual == visual && cc.EnsureSummonVisual() == visual, "SummonVisual and a second EnsureSummonVisual reach the same visual");
            if (visual == null)
                return;

            visual.Init(cc, isCelestial: false, isFiendish: false);
            visual.SetDuration(3, 6);
            Transform body = cc.View.Body;
            Assert(body.Find("SummonAura") != null, "The summon aura sits on the view");
            Transform bar = body.Find("SummonDurationBG");
            Assert(bar != null && bar.Find("SummonDurationFill") != null, "The summon duration bar sits on the view");
            Assert(body.Find("SummonBadge") != null, "The [S] badge sits on the view");
            Transform fill = bar != null ? bar.Find("SummonDurationFill") : null;
            Assert(fill != null && Mathf.Abs(fill.localScale.x - 0.48f) < 1e-4f, "SetDuration through the controller sizes the bar on the view (3 of 6 rounds)");
            Assert(cc.transform.childCount == 0, "No summon object is under the model token");

            cc.View.ClearSummonVisual();
            Assert(cc.SummonVisual == null, "ClearSummonVisual drops the view's summon visual (ResetForNewCreature, CRE-046)");
            Assert(visual == null, "The cleared summon visual is destroyed at once, not at the end of the frame");
            SummonedCreatureVisual fresh = cc.EnsureSummonVisual();
            Assert(fresh != null && fresh != visual && body.GetComponents<SummonedCreatureVisual>().Length == 1,
                "A summon placed in the same frame gets a fresh visual, never the cleared one");

            // A model reactivated after its last copy (a reused slot) gets an active Body before the visual starts.
            cc.View.ClearSummonVisual();
            cc.gameObject.SetActive(false);
            RunLateUpdate(cc.View);
            cc.gameObject.SetActive(true);
            SummonedCreatureVisual onReactivated = cc.EnsureSummonVisual();
            Assert(onReactivated != null && body.gameObject.activeInHierarchy, "EnsureSummonVisual copies a reactivation first, so its Body is active");
        }
        finally
        {
            Destroy(cc);
        }
    }

    private static void TestReusedPoolSlotShowsNewSprite()
    {
        GameManager gm = GameManager.Instance;
        CharacterController slot = GameManager.CreateNPCPoolSlot(900);
        try
        {
            NPCDatabase.Init();
            NPCDefinition first = NPCDatabase.Get("orc_warrior");
            NPCDefinition second = NPCDatabase.Get("goblin_warrior") ?? NPCDatabase.Get("kobold_warrior");
            Assert(first != null && second != null, "Control: two NPC entries for the reuse");
            if (first == null || second == null)
                return;

            TokenView view = slot.View;
            Assert(view != null, "A pool slot has a view");
            Sprite orc = NewSprite("TokenView_Orc", Color.green);
            Sprite other = NewSprite("TokenView_Other", Color.blue);

            gm.InitializeNPCFromDefinition(slot, first.Clone(), SlotPos, orc, null);
            slot.EnsureSummonVisual();
            RunLateUpdate(view);
            Assert(view.Renderer.sprite == orc, "The slot's view shows the first creature's sprite");

            slot.gameObject.SetActive(false); // a slot between fights
            RunLateUpdate(view);
            slot.gameObject.SetActive(true);
            gm.InitializeNPCFromDefinition(slot, second.Clone(), SlotPos, other, null);
            RunLateUpdate(view);
            Assert(slot.View == view, "The reused slot keeps its view");
            Assert(view.Renderer.sprite == other, "The reused slot's view shows the new creature's sprite (CRE-046)");
            Assert(view.Body.gameObject.activeSelf && view.IsSyncedWithModel(out _), "The reused slot's view is active and synced");
            Assert(slot.SummonVisual == null, "The reused slot's old summon visual is gone (CRE-046)");
        }
        finally
        {
            // The real per-spawn reset releases what the spawn registered (the runtime AI profile, AI-017; CRE-046).
            if (slot != null && slot.Stats != null)
                gm.ResetCharacterSlotForSpawn(slot);
            Destroy(slot);
        }
    }

    private static void TestFlamingSphereView()
    {
        var go = new GameObject("TokenView_Sphere");
        FlamingSphereEntity sphere = go.AddComponent<FlamingSphereEntity>();
        sphere.Initialize(null, null, new Vector2Int(-62, -60), 3, 6);
        TokenView view = sphere.View;
        GameObject viewObject = view != null ? view.gameObject : null;
        try
        {
            Assert(view != null && view.Renderer == null, "A Flaming Sphere gets a view without a sprite renderer");
            if (view == null)
                return;
            Assert(view.Body.Find("FlamingSphere_Core") != null && view.Body.Find("FlamingSphere_Glow") != null, "The sphere's quads sit on its view");
            Assert(go.transform.childCount == 0, "Nothing is drawn under the sphere's own object");
            sphere.SetGridPosition(new Vector2Int(-64, -60));
            RunLateUpdate(view);
            Assert((view.Body.position - go.transform.position).sqrMagnitude < 1e-8f, "The sphere's view follows its moves after LateUpdate");
            Object.DestroyImmediate(go);
            go = null;
            Assert(view.IsReleased, "A dissipated sphere releases its view");
        }
        finally
        {
            if (go != null)
                Object.DestroyImmediate(go);
            if (viewObject != null)
                Object.DestroyImmediate(viewObject);
        }
    }
}
}
