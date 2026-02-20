using UnityEngine;

/// <summary>
/// Main desktop-pet controller.
/// States: Idle → Clicked → (returns to Idle after delay)
/// Handles mouse drag via WindowManager.
/// </summary>
[RequireComponent(typeof(AnimationController))]
public class PetController : MonoBehaviour
{
    public enum PetState { Idle, Clicked, Dragging }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [Header("References")]
    [SerializeField] private WindowManager windowManager;
    [SerializeField] private ContextMenuHandler contextMenu;

    [Header("Behaviour")]
    [SerializeField] private float clickResetDelay = 1.5f;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private PetState         _state = PetState.Idle;
    private AnimationController _anim;
    private float            _clickTimer;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        _anim = GetComponent<AnimationController>();

        // Auto-find WindowManager if not assigned
        if (windowManager == null)
            windowManager = FindFirstObjectByType<WindowManager>();

        if (contextMenu == null)
            contextMenu = GetComponent<ContextMenuHandler>();
    }

    private void Start()
    {
        TransitionTo(PetState.Idle);
    }

    private void Update()
    {
        switch (_state)
        {
            case PetState.Idle:
                // Nothing special; AnimationController loops idle frames
                break;

            case PetState.Clicked:
                _clickTimer -= Time.deltaTime;
                if (_clickTimer <= 0f)
                    TransitionTo(PetState.Idle);
                break;

            case PetState.Dragging:
                windowManager?.UpdateDrag();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Input — uses OnMouseDown/OnMouseUp (works with 2D Collider automatically)
    // -------------------------------------------------------------------------

    private void OnMouseDown()
    {
        // Right-click → context menu
        if (Input.GetMouseButton(1))
        {
            Vector2 mp = Input.mousePosition;
            contextMenu?.ShowAt(new Vector2(mp.x, Screen.height - mp.y));
            return;
        }
        contextMenu?.Hide();
        TransitionTo(PetState.Dragging);
        windowManager?.BeginDrag();
    }

    private void OnMouseUp()
    {
        if (_state == PetState.Dragging)
        {
            windowManager?.EndDrag();
            TransitionTo(PetState.Clicked);
            _clickTimer = clickResetDelay;
        }
    }

    // -------------------------------------------------------------------------
    // State machine
    // -------------------------------------------------------------------------
    private void TransitionTo(PetState next)
    {
        _state = next;
        switch (next)
        {
            case PetState.Idle:
                _anim.PlayState("Idle");
                break;
            case PetState.Clicked:
                _anim.PlayState("Clicked");
                _clickTimer = clickResetDelay;
                break;
            case PetState.Dragging:
                _anim.PlayState("Drag");
                break;
        }
    }
}
