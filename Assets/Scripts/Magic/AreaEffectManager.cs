using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks and ticks all active persistent area effects.
/// </summary>
public class AreaEffectManager : MonoBehaviour
{
    private static AreaEffectManager instance;

    public static bool HasInstance => instance != null;

    public static AreaEffectManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("AreaEffectManager");
                instance = go.AddComponent<AreaEffectManager>();
                DontDestroyOnLoad(go);
            }

            return instance;
        }
    }

    private readonly List<PersistentAreaEffect> activeEffects = new List<PersistentAreaEffect>();

    public void RegisterAreaEffect(PersistentAreaEffect effect)
    {
        if (effect == null || activeEffects.Contains(effect))
            return;

        activeEffects.Add(effect);
        Debug.Log($"[AreaEffectManager] Registered: {effect.EffectName}");
    }

    public void UnregisterAreaEffect(PersistentAreaEffect effect)
    {
        if (effect == null)
            return;

        activeEffects.Remove(effect);
        Debug.Log($"[AreaEffectManager] Unregistered: {effect.EffectName}");
    }

    public void OnCombatRoundStart()
    {
        var copy = new List<PersistentAreaEffect>(activeEffects);
        for (int i = 0; i < copy.Count; i++)
        {
            PersistentAreaEffect effect = copy[i];
            if (effect == null)
                continue;

            effect.OnRoundStart();
        }

        activeEffects.RemoveAll(e => e == null);
    }

    public List<PersistentAreaEffect> GetEffectsAtPosition(Vector3 position)
    {
        var result = new List<PersistentAreaEffect>();

        for (int i = 0; i < activeEffects.Count; i++)
        {
            PersistentAreaEffect effect = activeEffects[i];
            if (effect != null && effect.IsPositionInArea(position))
                result.Add(effect);
        }

        return result;
    }

    public List<T> GetEffectsOfType<T>() where T : PersistentAreaEffect
    {
        var result = new List<T>();
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i] is T typed)
                result.Add(typed);
        }

        return result;
    }

    /// <summary>
    /// Returns a snapshot of all currently active persistent area effects.
    /// </summary>
    public List<PersistentAreaEffect> GetAllAreaEffects()
    {
        activeEffects.RemoveAll(e => e == null);
        return new List<PersistentAreaEffect>(activeEffects);
    }

    public void ClearAllEffects()
    {
        var copy = new List<PersistentAreaEffect>(activeEffects);
        for (int i = 0; i < copy.Count; i++)
        {
            if (copy[i] != null)
                Destroy(copy[i].gameObject);
        }

        activeEffects.Clear();
    }

    public int GetActiveEffectCount()
    {
        activeEffects.RemoveAll(e => e == null);
        return activeEffects.Count;
    }
}
