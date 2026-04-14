using UnityEngine;

/// <summary>
/// Handles orthographic zoom controls for the battle camera.
/// Supports mouse wheel, keyboard +/- keys, and optional touch pinch.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    [Tooltip("Minimum zoom level (0.5 = zoomed out to see more).")]
    public float minZoom = 0.5f;

    [Tooltip("Maximum zoom level (2.0 = zoomed in for detail).")]
    public float maxZoom = 2.0f;

    [Tooltip("Base zoom speed per wheel tick / key step.")]
    public float zoomSpeed = 0.1f;

    [Tooltip("Smoothing factor for zoom transitions.")]
    public float zoomSmoothness = 5f;

    [Header("Touch")]
    [Tooltip("Enable two-finger pinch zoom on touch devices.")]
    public bool enablePinchZoom = true;

    private Camera _mainCamera;
    private float _targetOrthographicSize;
    private float _defaultOrthographicSize;

    private void Start()
    {
        _mainCamera = GetComponent<Camera>();

        if (_mainCamera == null)
        {
            Debug.LogError("[CameraController] Requires a Camera component.");
            enabled = false;
            return;
        }

        _defaultOrthographicSize = Mathf.Max(0.01f, _mainCamera.orthographicSize);
        _targetOrthographicSize = _defaultOrthographicSize;
    }

    private void Update()
    {
        HandleZoomInput();
        ApplyZoom();
    }

    private void HandleZoomInput()
    {
        float scrollDelta = 0f;

#if ENABLE_LEGACY_INPUT_MANAGER
        scrollDelta = Input.mouseScrollDelta.y;
#endif

#if ENABLE_INPUT_SYSTEM
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
                scrollDelta = mouse.scroll.ReadValue().y * 0.01f;
        }
#endif

        if (!Mathf.Approximately(scrollDelta, 0f))
        {
            float zoomChange = -scrollDelta * zoomSpeed;
            _targetOrthographicSize += zoomChange;
        }

        bool zoomInHeld = false;
        bool zoomOutHeld = false;
        bool resetPressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        zoomInHeld = Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus);
        zoomOutHeld = Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus);
        resetPressed = Input.GetKeyDown(KeyCode.R);
#endif

#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            zoomInHeld = zoomInHeld || keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed;
            zoomOutHeld = zoomOutHeld || keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed;
            resetPressed = resetPressed || keyboard.rKey.wasPressedThisFrame;
        }
#endif

        if (zoomInHeld)
            _targetOrthographicSize -= zoomSpeed * Time.deltaTime * 10f;

        if (zoomOutHeld)
            _targetOrthographicSize += zoomSpeed * Time.deltaTime * 10f;

        if (resetPressed)
            _targetOrthographicSize = _defaultOrthographicSize;

        if (enablePinchZoom)
            HandlePinchZoom();

        ClampTargetZoom();
    }

    private void HandlePinchZoom()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount != 2)
            return;

        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);

        Vector2 previous0 = touch0.position - touch0.deltaPosition;
        Vector2 previous1 = touch1.position - touch1.deltaPosition;

        float prevDistance = Vector2.Distance(previous0, previous1);
        float currentDistance = Vector2.Distance(touch0.position, touch1.position);
        float delta = currentDistance - prevDistance;

        _targetOrthographicSize -= delta * (zoomSpeed * 0.01f);
#endif
    }

    private void ClampTargetZoom()
    {
        float minSize = _defaultOrthographicSize / maxZoom;
        float maxSize = _defaultOrthographicSize / minZoom;
        _targetOrthographicSize = Mathf.Clamp(_targetOrthographicSize, minSize, maxSize);
    }

    private void ApplyZoom()
    {
        if (_mainCamera == null)
            return;

        _mainCamera.orthographicSize = Mathf.Lerp(
            _mainCamera.orthographicSize,
            _targetOrthographicSize,
            Time.deltaTime * zoomSmoothness
        );
    }

    public void ZoomIn()
    {
        _targetOrthographicSize -= zoomSpeed;
        ClampTargetZoom();
    }

    public void ZoomOut()
    {
        _targetOrthographicSize += zoomSpeed;
        ClampTargetZoom();
    }

    public void ResetZoom()
    {
        _targetOrthographicSize = _defaultOrthographicSize;
    }

    public float GetCurrentZoomLevel()
    {
        if (_mainCamera == null || Mathf.Approximately(_mainCamera.orthographicSize, 0f))
            return 1f;

        return _defaultOrthographicSize / _mainCamera.orthographicSize;
    }
}
