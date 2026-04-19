using UnityEngine;
using System.Collections;

/// <summary>
/// Visual treatment and tooltip augmentation for summoned creatures.
/// Adds summoned badge, aura, duration bar, and summon/despawn effects.
/// </summary>
public class SummonedCreatureVisual : MonoBehaviour
{
    private CharacterController _character;
    private SpriteRenderer _auraRenderer;
    private SpriteRenderer _durationBgRenderer;
    private SpriteRenderer _durationFillRenderer;
    private TextMesh _badgeText;

    private Color _auraColor = new Color(0.2f, 0.85f, 1f, 0.45f);
    private int _remainingRounds;
    private int _totalRounds = 1;
    private bool _initialized;

    public void Init(CharacterController character, bool isCelestial, bool isFiendish)
    {
        _character = character;
        _auraColor = isCelestial
            ? new Color(0.95f, 0.85f, 0.35f, 0.45f)
            : isFiendish
                ? new Color(0.9f, 0.25f, 0.25f, 0.45f)
                : new Color(0.2f, 0.85f, 1f, 0.45f);

        BuildVisuals();
        _initialized = true;
        StartCoroutine(PlaySummonEffect());
    }

    public void SetDuration(int remainingRounds, int totalRounds)
    {
        _remainingRounds = Mathf.Max(0, remainingRounds);
        _totalRounds = Mathf.Max(1, totalRounds);
        UpdateDurationVisuals();
    }

    public IEnumerator PlayDespawnEffect()
    {
        if (!_initialized)
            yield break;

        float duration = 0.45f;
        float elapsed = 0f;
        Color baseAura = _auraRenderer != null ? _auraRenderer.color : _auraColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(baseAura.a, 0f, t);

            if (_auraRenderer != null)
            {
                Color c = baseAura;
                c.a = alpha;
                _auraRenderer.color = c;
                _auraRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, t);
            }

            if (_durationBgRenderer != null)
            {
                Color c = _durationBgRenderer.color;
                c.a = Mathf.Lerp(c.a, 0f, t);
                _durationBgRenderer.color = c;
            }

            if (_durationFillRenderer != null)
            {
                Color c = _durationFillRenderer.color;
                c.a = Mathf.Lerp(c.a, 0f, t);
                _durationFillRenderer.color = c;
            }

            if (_badgeText != null)
            {
                Color c = _badgeText.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                _badgeText.color = c;
            }

            yield return null;
        }
    }

    private void LateUpdate()
    {
        if (!_initialized || _character == null || _character.Stats == null)
            return;

        UpdateDurationVisuals();

        if (StatusEffectTooltipUI.Instance == null)
            return;

        if (!IsPointerOverThisCharacter())
            return;

        string tooltip = GameManager.Instance != null
            ? GameManager.Instance.GetSummonTooltip(_character)
            : null;

        if (!string.IsNullOrEmpty(tooltip))
            StatusEffectTooltipUI.Instance.ShowTooltip(tooltip, GetMouseScreenPosition());
    }

    private void BuildVisuals()
    {
        Sprite badgeSprite = CreateSquareSprite(24, 2);

        GameObject auraGo = new GameObject("SummonAura");
        auraGo.transform.SetParent(transform, false);
        auraGo.transform.localPosition = new Vector3(0f, -0.03f, 0f);
        _auraRenderer = auraGo.AddComponent<SpriteRenderer>();
        _auraRenderer.sprite = badgeSprite;
        _auraRenderer.color = _auraColor;
        _auraRenderer.sortingOrder = 6;
        auraGo.transform.localScale = new Vector3(1.25f, 1.25f, 1f);

        GameObject durationBgGo = new GameObject("SummonDurationBG");
        durationBgGo.transform.SetParent(transform, false);
        durationBgGo.transform.localPosition = new Vector3(0f, 0.93f, 0f);
        _durationBgRenderer = durationBgGo.AddComponent<SpriteRenderer>();
        _durationBgRenderer.sprite = badgeSprite;
        _durationBgRenderer.color = new Color(0f, 0f, 0f, 0.6f);
        _durationBgRenderer.sortingOrder = 58;
        durationBgGo.transform.localScale = new Vector3(0.78f, 0.08f, 1f);

        GameObject durationFillGo = new GameObject("SummonDurationFill");
        durationFillGo.transform.SetParent(durationBgGo.transform, false);
        durationFillGo.transform.localPosition = Vector3.zero;
        _durationFillRenderer = durationFillGo.AddComponent<SpriteRenderer>();
        _durationFillRenderer.sprite = badgeSprite;
        _durationFillRenderer.color = new Color(0.15f, 0.92f, 0.35f, 0.95f);
        _durationFillRenderer.sortingOrder = 59;
        durationFillGo.transform.localScale = new Vector3(0.96f, 0.78f, 1f);

        GameObject badgeGo = new GameObject("SummonBadge");
        badgeGo.transform.SetParent(transform, false);
        badgeGo.transform.localPosition = new Vector3(0.45f, 0.54f, 0f);
        _badgeText = badgeGo.AddComponent<TextMesh>();
        _badgeText.text = "[S]";
        _badgeText.anchor = TextAnchor.MiddleCenter;
        _badgeText.alignment = TextAlignment.Center;
        _badgeText.characterSize = 0.12f;
        _badgeText.fontSize = 48;
        _badgeText.color = new Color(0.45f, 1f, 1f, 0.98f);

        MeshRenderer badgeRenderer = badgeGo.GetComponent<MeshRenderer>();
        if (badgeRenderer != null)
            badgeRenderer.sortingOrder = 62;
    }

    private void UpdateDurationVisuals()
    {
        if (_durationFillRenderer == null)
            return;

        float ratio = Mathf.Clamp01((float)_remainingRounds / Mathf.Max(1, _totalRounds));
        _durationFillRenderer.transform.localScale = new Vector3(0.96f * ratio, 0.78f, 1f);
        _durationFillRenderer.transform.localPosition = new Vector3((-0.96f + (0.96f * ratio)) * 0.5f, 0f, 0f);

        Color barColor;
        if (ratio > 0.5f)
            barColor = new Color(0.15f, 0.92f, 0.35f, 0.95f);
        else if (ratio > 0.25f)
            barColor = new Color(0.95f, 0.82f, 0.22f, 0.95f);
        else
            barColor = new Color(0.95f, 0.32f, 0.26f, 0.95f);

        if (_remainingRounds <= 1)
        {
            float pulse = 0.65f + Mathf.PingPong(Time.time * 3.5f, 0.35f);
            barColor.a = pulse;
        }

        _durationFillRenderer.color = barColor;
    }

    private IEnumerator PlaySummonEffect()
    {
        Sprite pulseSprite = CreateCircleSprite(32);
        GameObject fx = new GameObject("SummonFlash");
        fx.transform.SetParent(transform, false);
        fx.transform.localPosition = new Vector3(0f, 0f, 0f);
        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = pulseSprite;
        sr.sortingOrder = 57;

        Color start = _auraColor;
        start.a = 0.95f;

        Color end = _auraColor;
        end.a = 0f;

        float duration = 0.75f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            sr.color = Color.Lerp(start, end, t);
            fx.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.8f, t);
            yield return null;
        }

        Destroy(fx);
    }

    private bool IsPointerOverThisCharacter()
    {
        Camera cam = Camera.main;
        if (cam == null || GameManager.Instance == null || GameManager.Instance.Grid == null)
            return false;

        Vector3 mouseScreen = GetMouseScreenPosition();
        Vector3 world = cam.ScreenToWorldPoint(mouseScreen);
        Vector2Int coord = SquareGridUtils.WorldToGrid(world);

        if (coord != _character.GridPosition)
            return false;

        SquareCell cell = GameManager.Instance.Grid.GetCell(coord);
        return cell != null && cell.ContainsOccupant(_character);
    }

    private static Vector3 GetMouseScreenPosition()
    {
        Vector3 mouseScreenPos = Vector3.zero;
#if ENABLE_LEGACY_INPUT_MANAGER
        mouseScreenPos = Input.mousePosition;
#endif
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
            mouseScreenPos = mouse.position.ReadValue();
#endif
        return mouseScreenPos;
    }

    private static Sprite CreateSquareSprite(int size, int border)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < border || y < border || x >= size - border || y >= size - border;
                pixels[y * size + x] = edge ? new Color(0f, 0f, 0f, 0.85f) : Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
