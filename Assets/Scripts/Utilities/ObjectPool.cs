using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// ObjectPool<T> — Generic object pool for Unity Components.
//
// Eliminates repeated new GameObject() + AddComponent + Destroy cycles that
// cause GC pressure in hot paths (combat log messages, visual effects, etc.).
//
// Usage:
//   var pool = new ObjectPool<Text>(
//       createFunc: () => {
//           var go = new GameObject("PooledText");
//           return go.AddComponent<Text>();
//       },
//       onGet: t => t.gameObject.SetActive(true),
//       onReturn: t => t.gameObject.SetActive(false),
//       maxSize: 200
//   );
//
//   Text t = pool.Get();        // Reuse or create
//   pool.Return(t);             // Deactivate and return to pool
//   pool.Prewarm(50);           // Pre-allocate 50 instances
//   pool.Clear();               // Destroy all pooled objects
//   PoolStats stats = pool.Stats; // Check hit/miss/size stats
// ============================================================================

/// <summary>
/// Pool performance statistics for monitoring and tuning.
/// </summary>
public struct PoolStats
{
    /// <summary>Number of times an object was reused from the pool (cache hit).</summary>
    public int Hits;

    /// <summary>Number of times a new object had to be created (cache miss).</summary>
    public int Misses;

    /// <summary>Current number of inactive objects waiting in the pool.</summary>
    public int PooledCount;

    /// <summary>Total number of objects created over the pool's lifetime.</summary>
    public int TotalCreated;

    /// <summary>Number of objects that were trimmed (destroyed) to stay within limits.</summary>
    public int Trimmed;

    /// <summary>Hit rate as a percentage (0-100). Returns 0 if no requests yet.</summary>
    public float HitRate => (Hits + Misses) > 0 ? (Hits * 100f / (Hits + Misses)) : 0f;

    public override string ToString()
    {
        return $"Pool: {PooledCount} pooled, {TotalCreated} created, " +
               $"{Hits} hits / {Misses} misses ({HitRate:F1}% hit rate), {Trimmed} trimmed";
    }
}

/// <summary>
/// Generic object pool for Unity Components. Reuses deactivated GameObjects
/// instead of creating and destroying them, reducing GC allocations.
/// </summary>
/// <typeparam name="T">Component type to pool (e.g., Text, SpriteRenderer, custom MonoBehaviour).</typeparam>
public class ObjectPool<T> where T : Component
{
    private readonly Stack<T> _pool;
    private readonly Func<T> _createFunc;
    private readonly Action<T> _onGet;
    private readonly Action<T> _onReturn;
    private readonly int _maxSize;

    private int _hits;
    private int _misses;
    private int _totalCreated;
    private int _trimmed;

    /// <summary>
    /// Create a new object pool.
    /// </summary>
    /// <param name="createFunc">Factory function to create a new instance when pool is empty.</param>
    /// <param name="onGet">Called when an object is retrieved from the pool (e.g., SetActive(true)).</param>
    /// <param name="onReturn">Called when an object is returned to the pool (e.g., SetActive(false)).</param>
    /// <param name="maxSize">Maximum number of objects to keep in the pool. Excess objects are destroyed. Default: 128.</param>
    /// <param name="initialCapacity">Initial stack capacity. Default: 32.</param>
    public ObjectPool(
        Func<T> createFunc,
        Action<T> onGet = null,
        Action<T> onReturn = null,
        int maxSize = 128,
        int initialCapacity = 32)
    {
        _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
        _onGet = onGet;
        _onReturn = onReturn;
        _maxSize = Mathf.Max(1, maxSize);
        _pool = new Stack<T>(Mathf.Min(initialCapacity, _maxSize));
    }

    /// <summary>
    /// Get an object from the pool, or create a new one if the pool is empty.
    /// </summary>
    /// <returns>An active, ready-to-use component instance.</returns>
    public T Get()
    {
        T obj;

        // Try to reuse from pool, skipping any that were externally destroyed
        while (_pool.Count > 0)
        {
            obj = _pool.Pop();
            if (obj != null && obj.gameObject != null)
            {
                _hits++;
                _onGet?.Invoke(obj);
                return obj;
            }
            // Object was destroyed externally — discard and try next
        }

        // Pool empty — create new
        _misses++;
        _totalCreated++;
        obj = _createFunc();
        _onGet?.Invoke(obj);
        return obj;
    }

    /// <summary>
    /// Return an object to the pool for reuse. The object is deactivated via onReturn callback.
    /// If the pool is full, the object is destroyed instead.
    /// </summary>
    /// <param name="obj">The component to return.</param>
    public void Return(T obj)
    {
        if (obj == null || obj.gameObject == null)
            return;

        _onReturn?.Invoke(obj);

        if (_pool.Count < _maxSize)
        {
            _pool.Push(obj);
        }
        else
        {
            // Pool is full — destroy the excess object
            _trimmed++;
            UnityEngine.Object.Destroy(obj.gameObject);
        }
    }

    /// <summary>
    /// Pre-instantiate objects to avoid allocation spikes during gameplay.
    /// Objects are created via the factory function and immediately deactivated.
    /// </summary>
    /// <param name="count">Number of objects to pre-create.</param>
    public void Prewarm(int count)
    {
        count = Mathf.Min(count, _maxSize - _pool.Count);
        for (int i = 0; i < count; i++)
        {
            T obj = _createFunc();
            _totalCreated++;
            _onReturn?.Invoke(obj);
            _pool.Push(obj);
        }
    }

    /// <summary>
    /// Destroy all pooled objects and reset the pool.
    /// Active (checked-out) objects are NOT affected.
    /// </summary>
    public void Clear()
    {
        while (_pool.Count > 0)
        {
            T obj = _pool.Pop();
            if (obj != null && obj.gameObject != null)
                UnityEngine.Object.Destroy(obj.gameObject);
        }
    }

    /// <summary>
    /// Trim the pool to a target size, destroying excess objects.
    /// Useful for periodic cleanup to free memory after combat peaks.
    /// </summary>
    /// <param name="targetSize">Desired maximum pool size after trimming.</param>
    public void Trim(int targetSize)
    {
        targetSize = Mathf.Max(0, targetSize);
        while (_pool.Count > targetSize)
        {
            T obj = _pool.Pop();
            if (obj != null && obj.gameObject != null)
                UnityEngine.Object.Destroy(obj.gameObject);
            _trimmed++;
        }
    }

    /// <summary>
    /// Current number of inactive objects waiting in the pool.
    /// </summary>
    public int Count => _pool.Count;

    /// <summary>
    /// Maximum pool size configured at creation.
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// Get pool performance statistics.
    /// </summary>
    public PoolStats Stats => new PoolStats
    {
        Hits = _hits,
        Misses = _misses,
        PooledCount = _pool.Count,
        TotalCreated = _totalCreated,
        Trimmed = _trimmed
    };

    /// <summary>
    /// Log pool statistics to the Unity console (debug builds only).
    /// </summary>
    /// <param name="poolName">Name to identify this pool in the log.</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void LogStats(string poolName = "ObjectPool")
    {
        Debug.Log($"[{poolName}] {Stats}");
    }
}

// ============================================================================
// GameObjectPool — Simplified pool for plain GameObjects (no specific Component).
//
// Usage:
//   var pool = new GameObjectPool("EffectMarker", parent, maxSize: 64);
//   GameObject go = pool.Get();
//   pool.Return(go);
// ============================================================================

/// <summary>
/// Simplified object pool for plain GameObjects without a specific Component requirement.
/// Useful for visual markers, UI containers, and effect holders.
/// </summary>
public class GameObjectPool
{
    private readonly Stack<GameObject> _pool;
    private readonly string _name;
    private readonly Transform _parent;
    private readonly int _maxSize;
    private readonly Action<GameObject> _onGet;
    private readonly Action<GameObject> _onReturn;

    private int _hits;
    private int _misses;
    private int _totalCreated;
    private int _trimmed;

    /// <summary>
    /// Create a new GameObject pool.
    /// </summary>
    /// <param name="name">Name prefix for created GameObjects.</param>
    /// <param name="parent">Optional parent transform for pooled objects.</param>
    /// <param name="maxSize">Maximum pool size. Default: 64.</param>
    /// <param name="onGet">Called when retrieving (e.g., SetActive(true)).</param>
    /// <param name="onReturn">Called when returning (e.g., SetActive(false)).</param>
    public GameObjectPool(
        string name = "PooledObject",
        Transform parent = null,
        int maxSize = 64,
        Action<GameObject> onGet = null,
        Action<GameObject> onReturn = null)
    {
        _name = name;
        _parent = parent;
        _maxSize = Mathf.Max(1, maxSize);
        _onGet = onGet ?? (go => go.SetActive(true));
        _onReturn = onReturn ?? (go => go.SetActive(false));
        _pool = new Stack<GameObject>(Mathf.Min(32, _maxSize));
    }

    /// <summary>
    /// Get a GameObject from the pool or create a new one.
    /// </summary>
    public GameObject Get()
    {
        while (_pool.Count > 0)
        {
            GameObject go = _pool.Pop();
            if (go != null)
            {
                _hits++;
                _onGet(go);
                return go;
            }
        }

        _misses++;
        _totalCreated++;
        GameObject newGo = new GameObject(_name);
        if (_parent != null)
            newGo.transform.SetParent(_parent, false);
        _onGet(newGo);
        return newGo;
    }

    /// <summary>
    /// Return a GameObject to the pool.
    /// </summary>
    public void Return(GameObject go)
    {
        if (go == null) return;

        _onReturn(go);

        if (_pool.Count < _maxSize)
        {
            _pool.Push(go);
        }
        else
        {
            _trimmed++;
            UnityEngine.Object.Destroy(go);
        }
    }

    /// <summary>
    /// Pre-create GameObjects for the pool.
    /// </summary>
    public void Prewarm(int count)
    {
        count = Mathf.Min(count, _maxSize - _pool.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject(_name);
            if (_parent != null)
                go.transform.SetParent(_parent, false);
            _totalCreated++;
            _onReturn(go);
            _pool.Push(go);
        }
    }

    /// <summary>Destroy all pooled objects.</summary>
    public void Clear()
    {
        while (_pool.Count > 0)
        {
            GameObject go = _pool.Pop();
            if (go != null)
                UnityEngine.Object.Destroy(go);
        }
    }

    /// <summary>Trim pool to target size.</summary>
    public void Trim(int targetSize)
    {
        targetSize = Mathf.Max(0, targetSize);
        while (_pool.Count > targetSize)
        {
            GameObject go = _pool.Pop();
            if (go != null)
                UnityEngine.Object.Destroy(go);
            _trimmed++;
        }
    }

    /// <summary>Current pooled count.</summary>
    public int Count => _pool.Count;

    /// <summary>Pool statistics.</summary>
    public PoolStats Stats => new PoolStats
    {
        Hits = _hits,
        Misses = _misses,
        PooledCount = _pool.Count,
        TotalCreated = _totalCreated,
        Trimmed = _trimmed
    };

    /// <summary>Log stats in debug builds.</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void LogStats(string poolName = "GameObjectPool")
    {
        Debug.Log($"[{poolName}] {Stats}");
    }
}
