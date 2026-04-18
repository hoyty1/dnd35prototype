using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Enables dragging a window RectTransform via a dedicated title bar (or any UI handle this script is attached to).
/// </summary>
public class DraggableWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Window RectTransform that should move while dragging. Defaults to this object's RectTransform.")]
    public RectTransform WindowRect;

    [Tooltip("If enabled, clamps the draggable window so it stays inside the canvas bounds.")]
    public bool ClampToCanvasBounds = true;

    [Tooltip("Optional key used to save/load window anchored position in PlayerPrefs.")]
    public string PersistenceKey = "";

    [Tooltip("Persist dragged position in PlayerPrefs when drag ends.")]
    public bool SavePositionToPlayerPrefs = true;

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private RectTransform _windowParentRect;
    private Vector2 _dragOffset;

    private void Awake()
    {
        if (WindowRect == null)
            WindowRect = transform as RectTransform;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
            _canvasRect = _canvas.transform as RectTransform;

        _windowParentRect = WindowRect != null ? WindowRect.parent as RectTransform : null;

        TryLoadPosition();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag())
            return;

        Vector2 localPointer;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_windowParentRect, eventData.position, eventData.pressEventCamera, out localPointer))
            _dragOffset = localPointer - WindowRect.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!CanDrag())
            return;

        Vector2 localPointer;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_windowParentRect, eventData.position, eventData.pressEventCamera, out localPointer))
            return;

        Vector2 target = localPointer - _dragOffset;
        if (ClampToCanvasBounds)
            target = ClampAnchoredPositionToParent(target, WindowRect, _windowParentRect);

        WindowRect.anchoredPosition = target;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        SavePosition();
    }

    private bool CanDrag()
    {
        return WindowRect != null && _windowParentRect != null;
    }

    private void TryLoadPosition()
    {
        if (WindowRect == null || string.IsNullOrEmpty(PersistenceKey))
            return;

        string xKey = PersistenceKey + "_x";
        string yKey = PersistenceKey + "_y";

        if (PlayerPrefs.HasKey(xKey) && PlayerPrefs.HasKey(yKey))
        {
            Vector2 loaded = new Vector2(PlayerPrefs.GetFloat(xKey), PlayerPrefs.GetFloat(yKey));
            if (ClampToCanvasBounds && _windowParentRect != null)
                loaded = ClampAnchoredPositionToParent(loaded, WindowRect, _windowParentRect);
            WindowRect.anchoredPosition = loaded;
        }
    }

    private void SavePosition()
    {
        if (!SavePositionToPlayerPrefs || WindowRect == null || string.IsNullOrEmpty(PersistenceKey))
            return;

        PlayerPrefs.SetFloat(PersistenceKey + "_x", WindowRect.anchoredPosition.x);
        PlayerPrefs.SetFloat(PersistenceKey + "_y", WindowRect.anchoredPosition.y);
    }

    public static Vector2 ClampAnchoredPositionToParent(Vector2 desiredAnchoredPosition, RectTransform rect, RectTransform parentRect)
    {
        if (rect == null || parentRect == null)
            return desiredAnchoredPosition;

        Vector2 parentSize = parentRect.rect.size;
        Vector2 parentPivot = parentRect.pivot;
        Vector2 rectSize = rect.rect.size;
        Vector2 rectPivot = rect.pivot;

        float minX = -parentSize.x * parentPivot.x + rectSize.x * rectPivot.x;
        float maxX = parentSize.x * (1f - parentPivot.x) - rectSize.x * (1f - rectPivot.x);

        float minY = -parentSize.y * parentPivot.y + rectSize.y * rectPivot.y;
        float maxY = parentSize.y * (1f - parentPivot.y) - rectSize.y * (1f - rectPivot.y);

        return new Vector2(
            Mathf.Clamp(desiredAnchoredPosition.x, minX, maxX),
            Mathf.Clamp(desiredAnchoredPosition.y, minY, maxY));
    }
}
