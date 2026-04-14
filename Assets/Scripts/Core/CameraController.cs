using UnityEngine;

/// <summary>
/// Handles orthographic camera zoom and panning controls for the battle camera.
/// Supports mouse wheel/keyboard zoom, optional touch pinch zoom,
/// keyboard panning (arrow keys + WASD), and optional mouse-drag panning.
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

    [Header("Pan Settings")]
    [Tooltip("Pan speed in world units per second.")]
    public float panSpeed = 10f;

    [Tooltip("Smoothing factor for pan transitions.")]
    public float panSmoothness = 5f;

    [Tooltip("If enabled, pan target is clamped to map bounds.")]
    public bool enablePanLimits = false;

    [Tooltip("Minimum pan X/Y world bounds.")]
    public Vector2 minPanBounds = new Vector2(-50f, -50f);

    [Tooltip("Maximum pan X/Y world bounds.")]
    public Vector2 maxPanBounds = new Vector2(50f, 50f);

    [Header("Mouse Pan Settings")]
    [Tooltip("Enable mouse drag panning.")]
    public bool enableMousePan = true;

    [Tooltip("Mouse button to pan with: 0 = Left, 1 = Right, 2 = Middle.")]
    public int mousePanButton = 0;

    [Header("Touch")]
    [Tooltip("Enable two-finger pinch zoom on touch devices.")]
    public bool enablePinchZoom = true;

    private Camera _mainCamera;
    private float _targetOrthographicSize;
    private float _defaultOrthographicSize;

    private Vector3 _targetPosition;
    private Vector3 _defaultPosition;
    private Vector3 _lastMousePosition;
    private bool _isMousePanning;

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

        _defaultPosition = transform.position;
        _targetPosition = _defaultPosition;
    }

    private void Update()
    {
        HandleZoomInput();
        HandlePanInput();
        HandleMousePanInput();
        ApplyZoom();
        ApplyPan();
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

    private void HandlePanInput()
    {
        Vector3 panDirection = Vector3.zero;

        bool up = false;
        bool down = false;
        bool left = false;
        bool right = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        up = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
        down = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        left = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        right = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
#endif

#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            up = up || keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed;
            down = down || keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
            left = left || keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
            right = right || keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;
        }
#endif

        if (up)
            panDirection += Vector3.up;

        if (down)
            panDirection += Vector3.down;

        if (left)
            panDirection += Vector3.left;

        if (right)
            panDirection += Vector3.right;

        if (panDirection.sqrMagnitude > 0f)
        {
            panDirection.Normalize();
            _targetPosition += panDirection * panSpeed * Time.deltaTime;
            ClampTargetPosition();
        }
    }

    private void HandleMousePanInput()
    {
        if (!enableMousePan)
            return;

        if (IsMouseButtonDownThisFrame(mousePanButton))
        {
            if (CanStartMousePan())
            {
                _isMousePanning = true;
                _lastMousePosition = GetMousePosition();
            }
            else
            {
                _isMousePanning = false;
            }
        }

        if (IsMouseButtonUpThisFrame(mousePanButton))
        {
            _isMousePanning = false;
            return;
        }

        if (!_isMousePanning || !IsMouseButtonHeld(mousePanButton) || _mainCamera == null)
            return;

        if (IsPointerOverUI())
        {
            _lastMousePosition = GetMousePosition();
            return;
        }

        Vector3 currentMousePosition = GetMousePosition();
        Vector3 mouseDelta = currentMousePosition - _lastMousePosition;

        float unitsPerPixel = (_mainCamera.orthographicSize * 2f) / Mathf.Max(1f, Screen.height);
        Vector3 panDelta = new Vector3(-mouseDelta.x * unitsPerPixel, -mouseDelta.y * unitsPerPixel, 0f);

        _targetPosition += panDelta;
        ClampTargetPosition();

        _lastMousePosition = currentMousePosition;
    }

    private bool CanStartMousePan()
    {
        if (IsPointerOverUI())
            return false;

        if (mousePanButton != 0)
            return true;

        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.IsPlayerTurn)
            return true;

        switch (gm.CurrentSubPhase)
        {
            case GameManager.PlayerSubPhase.Moving:
            case GameManager.PlayerSubPhase.SelectingAttackTarget:
            case GameManager.PlayerSubPhase.SelectingSpecialTarget:
            case GameManager.PlayerSubPhase.SelectingChargeTarget:
            case GameManager.PlayerSubPhase.ConfirmingChargePath:
            case GameManager.PlayerSubPhase.SelectingAoETarget:
            case GameManager.PlayerSubPhase.ConfirmingSelfAoE:
                return false;
            default:
                return true;
        }
    }

    private bool IsPointerOverUI()
    {
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsMouseButtonDownThisFrame(int button)
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed = Input.GetMouseButtonDown(button);
#endif

#if ENABLE_INPUT_SYSTEM
        if (!pressed)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                switch (button)
                {
                    case 0:
                        pressed = mouse.leftButton.wasPressedThisFrame;
                        break;
                    case 1:
                        pressed = mouse.rightButton.wasPressedThisFrame;
                        break;
                    case 2:
                        pressed = mouse.middleButton.wasPressedThisFrame;
                        break;
                    default:
                        pressed = false;
                        break;
                }
            }
        }
#endif

        return pressed;
    }

    private bool IsMouseButtonHeld(int button)
    {
        bool held = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        held = Input.GetMouseButton(button);
#endif

#if ENABLE_INPUT_SYSTEM
        if (!held)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                switch (button)
                {
                    case 0:
                        held = mouse.leftButton.isPressed;
                        break;
                    case 1:
                        held = mouse.rightButton.isPressed;
                        break;
                    case 2:
                        held = mouse.middleButton.isPressed;
                        break;
                    default:
                        held = false;
                        break;
                }
            }
        }
#endif

        return held;
    }

    private bool IsMouseButtonUpThisFrame(int button)
    {
        bool released = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        released = Input.GetMouseButtonUp(button);
#endif

#if ENABLE_INPUT_SYSTEM
        if (!released)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                switch (button)
                {
                    case 0:
                        released = mouse.leftButton.wasReleasedThisFrame;
                        break;
                    case 1:
                        released = mouse.rightButton.wasReleasedThisFrame;
                        break;
                    case 2:
                        released = mouse.middleButton.wasReleasedThisFrame;
                        break;
                    default:
                        released = false;
                        break;
                }
            }
        }
#endif

        return released;
    }

    private Vector3 GetMousePosition()
    {
        Vector3 position = Vector3.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        position = Input.mousePosition;
#endif

#if ENABLE_INPUT_SYSTEM
        if (position == Vector3.zero)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
                position = mouse.position.ReadValue();
        }
#endif

        return position;
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

    private void ClampTargetPosition()
    {
        if (enablePanLimits)
        {
            _targetPosition.x = Mathf.Clamp(_targetPosition.x, minPanBounds.x, maxPanBounds.x);
            _targetPosition.y = Mathf.Clamp(_targetPosition.y, minPanBounds.y, maxPanBounds.y);
        }

        _targetPosition.z = _defaultPosition.z;
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

    private void ApplyPan()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            _targetPosition,
            Time.deltaTime * panSmoothness
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

    public void PanTo(Vector3 position)
    {
        _targetPosition = position;
        ClampTargetPosition();
    }

    public void PanBy(Vector3 offset)
    {
        _targetPosition += offset;
        ClampTargetPosition();
    }

    public void ResetPosition()
    {
        _targetPosition = _defaultPosition;
        ClampTargetPosition();
    }

    public void CenterOnPosition(Vector3 worldPosition)
    {
        _targetPosition = new Vector3(worldPosition.x, worldPosition.y, _defaultPosition.z);
        ClampTargetPosition();
    }
}
