using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds resize handles on all edges/corners for a UI window RectTransform.
/// Supports min/max constraints, canvas-bound clamping, cursor feedback, and PlayerPrefs persistence.
/// </summary>
public class ResizableWindow : MonoBehaviour
{
    [Flags]
    public enum ResizeDirection
    {
        None = 0,
        Left = 1,
        Right = 2,
        Bottom = 4,
        Top = 8
    }

    [Header("Target")]
    public RectTransform WindowRect;

    [Header("Constraints")]
    public Vector2 MinSize = new Vector2(260f, 140f);
    public Vector2 MaxSize = new Vector2(1200f, 900f);
    public bool ClampToCanvasBounds = true;

    [Header("Handles")]
    public bool CreateHandlesAtRuntime = true;
    public float EdgeHandleThickness = 8f;
    public float CornerHandleSize = 16f;
    public Color HandleColor = new Color(0.9f, 0.9f, 1f, 0.12f);
    public Color HandleHoverColor = new Color(0.9f, 0.9f, 1f, 0.32f);

    [Header("Persistence")]
    public bool SaveSizeToPlayerPrefs = true;
    public bool SavePositionToPlayerPrefs = true;
    public string PersistenceKey = "";

    public event Action<Vector2> OnResized;

    private Canvas _canvas;
    private RectTransform _parentRect;
    private Vector2 _resizeStartPointerParentLocal;
    private Vector2 _resizeStartSize;
    private Vector2 _resizeStartPos;
    private ResizeDirection _activeResizeDirection;

    private bool _isInitialized;

    private static Texture2D _horizontalCursor;
    private static Texture2D _verticalCursor;
    private static Texture2D _diagonalCursor;

    private readonly List<Image> _runtimeHandleImages = new List<Image>();

    private void Awake()
    {
        if (WindowRect == null)
            WindowRect = transform as RectTransform;

        _canvas = GetComponentInParent<Canvas>();
        _parentRect = WindowRect != null ? WindowRect.parent as RectTransform : null;

        EnsureRuntimeCursorTextures();

        if (CreateHandlesAtRuntime)
            CreateHandles();
    }

    private void Start()
    {
        if (WindowRect == null)
            return;

        LoadWindowState();
        ClampWindowToParentBounds();
        NotifyResized();
        _isInitialized = true;
    }

    private void OnDisable()
    {
        if (_isInitialized)
            SaveWindowState();

        ResetCursor();
    }

    private void OnDestroy()
    {
        ResetCursor();
    }

    public void BeginResize(ResizeDirection direction, PointerEventData eventData)
    {
        if (WindowRect == null || _parentRect == null)
            return;

        _activeResizeDirection = direction;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out _resizeStartPointerParentLocal);

        _resizeStartSize = WindowRect.sizeDelta;
        _resizeStartPos = WindowRect.anchoredPosition;
    }

    public void Resize(PointerEventData eventData)
    {
        if (WindowRect == null || _parentRect == null || _activeResizeDirection == ResizeDirection.None)
            return;

        Vector2 currentPointer;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out currentPointer))
            return;

        Vector2 delta = currentPointer - _resizeStartPointerParentLocal;

        Vector2 size = _resizeStartSize;
        Vector2 pos = _resizeStartPos;

        if (HasFlag(_activeResizeDirection, ResizeDirection.Right))
        {
            size.x = _resizeStartSize.x + delta.x;
            pos.x = _resizeStartPos.x + (size.x - _resizeStartSize.x) * 0.5f;
        }

        if (HasFlag(_activeResizeDirection, ResizeDirection.Left))
        {
            size.x = _resizeStartSize.x - delta.x;
            pos.x = _resizeStartPos.x - (size.x - _resizeStartSize.x) * 0.5f;
        }

        if (HasFlag(_activeResizeDirection, ResizeDirection.Top))
        {
            size.y = _resizeStartSize.y + delta.y;
            pos.y = _resizeStartPos.y + (size.y - _resizeStartSize.y) * 0.5f;
        }

        if (HasFlag(_activeResizeDirection, ResizeDirection.Bottom))
        {
            size.y = _resizeStartSize.y - delta.y;
            pos.y = _resizeStartPos.y - (size.y - _resizeStartSize.y) * 0.5f;
        }

        size.x = Mathf.Clamp(size.x, MinSize.x, MaxAllowedWidth());
        size.y = Mathf.Clamp(size.y, MinSize.y, MaxAllowedHeight());

        WindowRect.sizeDelta = size;

        if (ClampToCanvasBounds)
            pos = DraggableWindow.ClampAnchoredPositionToParent(pos, WindowRect, _parentRect);

        WindowRect.anchoredPosition = pos;

        NotifyResized();
    }

    public void EndResize()
    {
        _activeResizeDirection = ResizeDirection.None;
        SaveWindowState();
    }

    public void SetHoverState(Image handleImage, bool isHovering)
    {
        if (handleImage != null)
            handleImage.color = isHovering ? HandleHoverColor : HandleColor;
    }

    public void SetCursorForDirection(ResizeDirection direction)
    {
        Texture2D tex = null;
        Vector2 hotspot = new Vector2(8, 8);

        bool horizontalOnly = (direction == ResizeDirection.Left || direction == ResizeDirection.Right);
        bool verticalOnly = (direction == ResizeDirection.Top || direction == ResizeDirection.Bottom);

        if (horizontalOnly)
            tex = _horizontalCursor;
        else if (verticalOnly)
            tex = _verticalCursor;
        else
            tex = _diagonalCursor;

        Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
    }

    public void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void ClampWindowToParentBounds()
    {
        if (WindowRect == null || _parentRect == null)
            return;

        Vector2 size = WindowRect.sizeDelta;
        size.x = Mathf.Clamp(size.x, MinSize.x, MaxAllowedWidth());
        size.y = Mathf.Clamp(size.y, MinSize.y, MaxAllowedHeight());
        WindowRect.sizeDelta = size;

        if (ClampToCanvasBounds)
            WindowRect.anchoredPosition = DraggableWindow.ClampAnchoredPositionToParent(WindowRect.anchoredPosition, WindowRect, _parentRect);
    }

    private void NotifyResized()
    {
        OnResized?.Invoke(WindowRect != null ? WindowRect.sizeDelta : Vector2.zero);
    }

    private bool HasFlag(ResizeDirection value, ResizeDirection flag)
    {
        return (value & flag) == flag;
    }

    private float MaxAllowedWidth()
    {
        float parentCap = _parentRect != null ? _parentRect.rect.width : MaxSize.x;
        return Mathf.Max(MinSize.x, Mathf.Min(MaxSize.x, parentCap));
    }

    private float MaxAllowedHeight()
    {
        float parentCap = _parentRect != null ? _parentRect.rect.height : MaxSize.y;
        return Mathf.Max(MinSize.y, Mathf.Min(MaxSize.y, parentCap));
    }

    private void LoadWindowState()
    {
        if (WindowRect == null || string.IsNullOrEmpty(PersistenceKey))
            return;

        string xKey = PersistenceKey + "_x";
        string yKey = PersistenceKey + "_y";
        string wKey = PersistenceKey + "_w";
        string hKey = PersistenceKey + "_h";

        if (PlayerPrefs.HasKey(wKey) && PlayerPrefs.HasKey(hKey))
        {
            Vector2 loadedSize = new Vector2(PlayerPrefs.GetFloat(wKey), PlayerPrefs.GetFloat(hKey));
            loadedSize.x = Mathf.Clamp(loadedSize.x, MinSize.x, MaxAllowedWidth());
            loadedSize.y = Mathf.Clamp(loadedSize.y, MinSize.y, MaxAllowedHeight());
            WindowRect.sizeDelta = loadedSize;
        }

        if (PlayerPrefs.HasKey(xKey) && PlayerPrefs.HasKey(yKey))
        {
            WindowRect.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(xKey), PlayerPrefs.GetFloat(yKey));
        }
    }

    private void SaveWindowState()
    {
        if (WindowRect == null || string.IsNullOrEmpty(PersistenceKey))
            return;

        if (SavePositionToPlayerPrefs)
        {
            PlayerPrefs.SetFloat(PersistenceKey + "_x", WindowRect.anchoredPosition.x);
            PlayerPrefs.SetFloat(PersistenceKey + "_y", WindowRect.anchoredPosition.y);
        }

        if (SaveSizeToPlayerPrefs)
        {
            PlayerPrefs.SetFloat(PersistenceKey + "_w", WindowRect.sizeDelta.x);
            PlayerPrefs.SetFloat(PersistenceKey + "_h", WindowRect.sizeDelta.y);
        }
    }

    private void CreateHandles()
    {
        if (WindowRect == null)
            return;

        CreateHandle("ResizeHandle_Left", ResizeDirection.Left,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(EdgeHandleThickness, 0f));

        CreateHandle("ResizeHandle_Right", ResizeDirection.Right,
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(0f, 0f), new Vector2(EdgeHandleThickness, 0f));

        CreateHandle("ResizeHandle_Top", ResizeDirection.Top,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, EdgeHandleThickness));

        CreateHandle("ResizeHandle_Bottom", ResizeDirection.Bottom,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, EdgeHandleThickness));

        CreateHandle("ResizeHandle_TopLeft", ResizeDirection.Top | ResizeDirection.Left,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(CornerHandleSize, CornerHandleSize));

        CreateHandle("ResizeHandle_TopRight", ResizeDirection.Top | ResizeDirection.Right,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            Vector2.zero, new Vector2(CornerHandleSize, CornerHandleSize));

        CreateHandle("ResizeHandle_BottomLeft", ResizeDirection.Bottom | ResizeDirection.Left,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            Vector2.zero, new Vector2(CornerHandleSize, CornerHandleSize));

        CreateHandle("ResizeHandle_BottomRight", ResizeDirection.Bottom | ResizeDirection.Right,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            Vector2.zero, new Vector2(CornerHandleSize, CornerHandleSize));
    }

    private void CreateHandle(
        string handleName,
        ResizeDirection direction,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject handle = new GameObject(handleName);
        handle.transform.SetParent(WindowRect, false);

        RectTransform rt = handle.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Image img = handle.AddComponent<Image>();
        img.color = HandleColor;
        _runtimeHandleImages.Add(img);

        ResizeHandleInteraction interaction = handle.AddComponent<ResizeHandleInteraction>();
        interaction.Owner = this;
        interaction.Direction = direction;
        interaction.HandleImage = img;
    }

    private static void EnsureRuntimeCursorTextures()
    {
        if (_horizontalCursor != null && _verticalCursor != null && _diagonalCursor != null)
            return;

        _horizontalCursor = BuildCursorTexture(CursorKind.Horizontal);
        _verticalCursor = BuildCursorTexture(CursorKind.Vertical);
        _diagonalCursor = BuildCursorTexture(CursorKind.Diagonal);
    }

    private enum CursorKind
    {
        Horizontal,
        Vertical,
        Diagonal
    }

    private static Texture2D BuildCursorTexture(CursorKind kind)
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color line = new Color(1f, 1f, 1f, 1f);

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        for (int i = 2; i < size - 2; i++)
        {
            switch (kind)
            {
                case CursorKind.Horizontal:
                    SetPixel(pixels, size, i, size / 2, line);
                    SetPixel(pixels, size, i, size / 2 - 1, line * 0.9f);
                    break;
                case CursorKind.Vertical:
                    SetPixel(pixels, size, size / 2, i, line);
                    SetPixel(pixels, size, size / 2 - 1, i, line * 0.9f);
                    break;
                case CursorKind.Diagonal:
                    SetPixel(pixels, size, i, i, line);
                    SetPixel(pixels, size, i, i - 1, line * 0.9f);
                    break;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void SetPixel(Color[] buffer, int size, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= size || y >= size)
            return;
        buffer[y * size + x] = color;
    }

    private class ResizeHandleInteraction : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public ResizableWindow Owner;
        public ResizeDirection Direction;
        public Image HandleImage;

        public void OnBeginDrag(PointerEventData eventData)
        {
            Owner?.BeginResize(Direction, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Owner?.Resize(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Owner?.EndResize();
            Owner?.SetHoverState(HandleImage, false);
            Owner?.ResetCursor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Owner?.SetHoverState(HandleImage, true);
            Owner?.SetCursorForDirection(Direction);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Owner?.SetHoverState(HandleImage, false);
            Owner?.ResetCursor();
        }
    }
}
