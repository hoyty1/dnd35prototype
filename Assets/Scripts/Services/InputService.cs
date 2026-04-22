using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Centralized input service for mouse/keyboard polling, UI click filtering, mode tracking,
/// and routing click/cancel events to game callbacks.
/// </summary>
public class InputService : MonoBehaviour
{
    public enum InputMode
    {
        Normal,
        SelectingTarget,
        SelectingMovement,
        SelectingArea,
        PlacingSummon,
        MenuOpen
    }

    public enum MouseButtonType
    {
        Left,
        Right,
        Middle
    }

    public class InputClickContext
    {
        public MouseButtonType Button;
        public Vector3 ScreenPosition;
        public Vector2 WorldPosition;
        public RaycastHit2D Hit;
        public bool IsPointerOverUI;

        public SquareCell GetSquareCell() => Hit.collider != null ? Hit.collider.GetComponent<SquareCell>() : null;

        public CharacterController GetCharacter()
        {
            if (Hit.collider == null)
                return null;

            CharacterController clicked = Hit.collider.GetComponent<CharacterController>();
            if (clicked != null)
                return clicked;

            SquareCell cell = Hit.collider.GetComponent<SquareCell>();
            if (cell != null && cell.IsOccupied)
                return cell.Occupant;

            return null;
        }
    }

    [SerializeField] private Camera _mainCamera;

    private readonly Dictionary<InputMode, Func<InputClickContext, bool>> _leftClickHandlers =
        new Dictionary<InputMode, Func<InputClickContext, bool>>();

    private Func<bool> _canProcessInput;
    private Func<bool> _shouldAllowGridClickThroughUi;
    private Func<InputClickContext, bool> _secondaryClickHandler;
    private Func<InputClickContext, bool> _cancelActionHandler;

    public event Action OnInventoryToggleRequested;
    public event Action OnSkillsToggleRequested;
    public event Action OnCharacterSheetToggleRequested;

    public event Action<Vector3> OnWorldClicked;
    public event Action<CharacterController> OnCharacterClicked;
    public event Action<int, int> OnGridSquareClicked;
    public event Action OnCancelAction;

    public InputMode CurrentMode { get; private set; } = InputMode.Normal;

    public bool IsSelectingTarget => CurrentMode == InputMode.SelectingTarget;
    public bool IsSelectingMovement => CurrentMode == InputMode.SelectingMovement;
    public bool IsSelectingArea => CurrentMode == InputMode.SelectingArea;
    public bool IsPlacingSummon => CurrentMode == InputMode.PlacingSummon;

    public void Initialize(
        Camera mainCamera,
        Func<bool> canProcessInput,
        Func<bool> shouldAllowGridClickThroughUi,
        Func<InputClickContext, bool> secondaryClickHandler,
        Func<InputClickContext, bool> cancelActionHandler)
    {
        _mainCamera = mainCamera;
        _canProcessInput = canProcessInput;
        _shouldAllowGridClickThroughUi = shouldAllowGridClickThroughUi;
        _secondaryClickHandler = secondaryClickHandler;
        _cancelActionHandler = cancelActionHandler;
    }

    public void SetCamera(Camera mainCamera)
    {
        _mainCamera = mainCamera;
    }

    public void SetInputMode(InputMode mode)
    {
        CurrentMode = mode;
    }

    public void RegisterClickHandler(InputMode mode, Func<InputClickContext, bool> handler)
    {
        if (handler == null)
        {
            _leftClickHandlers.Remove(mode);
            return;
        }

        _leftClickHandlers[mode] = handler;
    }

    public void ProcessInput()
    {
        HandleKeyboardInput();

        if (_canProcessInput != null && !_canProcessInput())
            return;

        HandleCancelInput();
        HandleSecondaryClick();
        HandleMouseClick();
    }

    public bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public bool IsClickOnUI()
    {
        return IsPointerOverUI();
    }

    public bool TryGetMouseScreenPosition(out Vector3 screenPosition)
    {
        screenPosition = Vector3.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        screenPosition = Input.mousePosition;
        return true;
#endif

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }
#endif

        return false;
    }

    public bool TryGetMouseWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = Vector2.zero;

        if (_mainCamera == null)
            return false;

        if (!TryGetMouseScreenPosition(out Vector3 mouseScreenPos))
            return false;

        worldPosition = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        return true;
    }

    public Vector2 GetMouseWorldPosition()
    {
        if (TryGetMouseWorldPosition(out Vector2 worldPos))
            return worldPos;

        return Vector2.zero;
    }

    private void HandleKeyboardInput()
    {
        if (WasInventoryTogglePressed())
            OnInventoryToggleRequested?.Invoke();

        if (WasSkillsTogglePressed())
            OnSkillsToggleRequested?.Invoke();

        if (WasCharacterSheetTogglePressed())
            OnCharacterSheetToggleRequested?.Invoke();
    }

    private void HandleCancelInput()
    {
        if (!IsCancelMode(CurrentMode))
            return;

        if (!WasCancelInputPressed(out InputClickContext cancelContext))
            return;

        bool handled = false;
        if (_cancelActionHandler != null)
            handled = _cancelActionHandler(cancelContext);

        if (!handled)
            OnCancelAction?.Invoke();
    }

    private void HandleSecondaryClick()
    {
        if (!WasMouseButtonPressed(MouseButtonType.Right, out Vector3 screenPos))
            return;

        InputClickContext context = BuildClickContext(MouseButtonType.Right, screenPos, allowClickThroughUi: false);
        if (context == null)
            return;

        _secondaryClickHandler?.Invoke(context);
    }

    private void HandleMouseClick()
    {
        if (!WasMouseButtonPressed(MouseButtonType.Left, out Vector3 screenPos))
            return;

        InputClickContext context = BuildClickContext(MouseButtonType.Left, screenPos, allowClickThroughUi: ShouldAllowGridClickThroughUi());
        if (context == null)
            return;

        if (_leftClickHandlers.TryGetValue(CurrentMode, out Func<InputClickContext, bool> modeHandler) && modeHandler != null)
        {
            if (modeHandler(context))
                return;
        }

        RouteDefaultClick(context);
    }

    private InputClickContext BuildClickContext(MouseButtonType button, Vector3 screenPos, bool allowClickThroughUi)
    {
        if (_mainCamera == null)
            return null;

        bool pointerOverUi = IsPointerOverUI();
        if (pointerOverUi && !allowClickThroughUi)
        {
            Debug.Log("[Grid] Click blocked by UI element (IsPointerOverGameObject)");
            return null;
        }

        Vector2 worldPoint = _mainCamera.ScreenToWorldPoint(screenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

        return new InputClickContext
        {
            Button = button,
            ScreenPosition = screenPos,
            WorldPosition = worldPoint,
            Hit = hit,
            IsPointerOverUI = pointerOverUi
        };
    }

    private void RouteDefaultClick(InputClickContext context)
    {
        OnWorldClicked?.Invoke(context.WorldPosition);

        SquareCell clickedCell = context.GetSquareCell();
        if (clickedCell != null)
            OnGridSquareClicked?.Invoke(clickedCell.X, clickedCell.Y);

        CharacterController clickedCharacter = context.GetCharacter();
        if (clickedCharacter != null)
            OnCharacterClicked?.Invoke(clickedCharacter);
    }

    private bool ShouldAllowGridClickThroughUi()
    {
        return _shouldAllowGridClickThroughUi != null && _shouldAllowGridClickThroughUi();
    }

    private static bool IsCancelMode(InputMode mode)
    {
        return mode == InputMode.SelectingTarget
            || mode == InputMode.SelectingMovement
            || mode == InputMode.SelectingArea
            || mode == InputMode.PlacingSummon;
    }

    private static bool WasInventoryTogglePressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.I))
            return true;
#endif

#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
            return true;
#endif

        return false;
    }

    private static bool WasSkillsTogglePressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.K))
            return true;
#endif

#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.kKey.wasPressedThisFrame)
            return true;
#endif

        return false;
    }

    private static bool WasCharacterSheetTogglePressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.C))
            return true;
#endif

#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            return true;
#endif

        return false;
    }

    private bool WasCancelInputPressed(out InputClickContext context)
    {
        context = null;

        if (WasMouseButtonPressed(MouseButtonType.Right, out Vector3 mouseScreenPos))
        {
            context = BuildClickContext(MouseButtonType.Right, mouseScreenPos, allowClickThroughUi: true);
            return true;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#endif

#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            return true;
#endif

        return false;
    }

    private static bool WasMouseButtonPressed(MouseButtonType button, out Vector3 screenPos)
    {
        screenPos = Vector3.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        int legacyButton = button == MouseButtonType.Left ? 0 : (button == MouseButtonType.Right ? 1 : 2);
        if (Input.GetMouseButtonDown(legacyButton))
        {
            screenPos = Input.mousePosition;
            return true;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            bool pressed = false;
            switch (button)
            {
                case MouseButtonType.Left:
                    pressed = mouse.leftButton.wasPressedThisFrame;
                    break;
                case MouseButtonType.Right:
                    pressed = mouse.rightButton.wasPressedThisFrame;
                    break;
                case MouseButtonType.Middle:
                    pressed = mouse.middleButton.wasPressedThisFrame;
                    break;
            }

            if (pressed)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }
        }
#endif

        return false;
    }
}
