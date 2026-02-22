using UnityEngine;

/// <summary>
/// Desktop-pet state machine: Idle / Clicked / Dragging.
/// Input is driven externally by WindowManager (OnMouseDown/OnMouseUp cannot work
/// with BoxCollider2D without a Physics2D Raycaster on the camera).
/// </summary>
[RequireComponent(typeof(Live2DController))]
public class PetController : MonoBehaviour
{
    public enum PetState { Idle, Clicked, Dragging }

    [SerializeField] private float clickResetDelay = 1.5f;

    private PetState         _state = PetState.Idle;
    private Live2DController _anim;
    private ContextMenuHandler _contextMenu;
    private float            _clickTimer;

    private void Awake()
    {
        _anim        = GetComponent<Live2DController>();
        _contextMenu = GetComponent<ContextMenuHandler>();
        Debug.Log($"[PC] Awake: _anim={_anim}");
    }

    private void Start()
    {
        TransitionTo(PetState.Idle);
    }

    private void Update()
    {
        if (_state == PetState.Clicked)
        {
            _clickTimer -= Time.deltaTime;
            if (_clickTimer <= 0f)
                TransitionTo(PetState.Idle);
        }
    }

    // Called by WindowManager when left mouse button pressed over pet
    public void OnDragBegin()
    {
        _contextMenu?.Hide();
        TransitionTo(PetState.Dragging);
    }

    // Called by WindowManager when left mouse button released
    public void OnDragEnd()
    {
        TransitionTo(PetState.Clicked);
        _clickTimer = clickResetDelay;
    }

    // Called by WindowManager when right mouse button pressed over pet
    public void OnRightClick(Vector2 screenPos)
    {
        _contextMenu?.ShowAt(new Vector2(screenPos.x, Screen.height - screenPos.y));
    }

    private void TransitionTo(PetState next)
    {
        _state = next;
        switch (next)
        {
            case PetState.Idle:    _anim.PlayState("Idle");    break;
            case PetState.Clicked: _anim.PlayState("Clicked"); break;
            case PetState.Dragging: _anim.PlayState("Drag");   break;
        }
    }
}
