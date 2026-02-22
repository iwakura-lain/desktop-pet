using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Window manager — borderless, always-on-top, transparent background, draggable.
/// Uses DWM sheet-of-glass transparency. Click-through is toggled per-frame based
/// on whether the mouse is over the pet collider (Physics2D.GetRayIntersection).
/// Also drives PetController input since OnMouseDown cannot work without a Raycaster.
/// </summary>
public class WindowManager : MonoBehaviour
{
    private const int  GWL_STYLE    = -16;
    private const int  GWL_EXSTYLE  = -20;
    private const uint WS_POPUP     = 0x80000000;
    private const uint WS_VISIBLE   = 0x10000000;
    private const uint WS_EX_LAYERED     = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_TOPMOST     = 0x00000008;
    private const uint SWP_NOSIZE        = 0x0001;
    private const uint SWP_NOMOVE        = 0x0002;
    private const uint SWP_FRAMECHANGED  = 0x0020;
    private const uint LWA_ALPHA         = 0x00000002;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT  { public int left, top, right, bottom; }

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern int    SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    [DllImport("user32.dll")] private static extern uint   GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool   SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] private static extern bool   GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool   GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool   MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);
    [DllImport("user32.dll")] private static extern int    ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool   SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("Dwmapi.dll")] private static extern uint   DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS m);

    private IntPtr        _hwnd;
    private bool          _isDragging;
    private POINT         _dragStart;
    private RECT          _winRectAtDragStart;
    private PetController _petController;

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    private bool _isClickThrough = true;
    private bool _wasOverPet     = false;
#endif

    private void Start()
    {
        _petController = FindFirstObjectByType<PetController>();

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
        _hwnd = GetActiveWindow();
        ApplyWindowStyle();
#endif
    }

    private void Update()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (_hwnd == IntPtr.Zero) return;

        var  ray     = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool overPet = Physics2D.GetRayIntersection(ray, Mathf.Infinity).collider != null;

        // Toggle click-through: enabled when mouse is not over pet and not dragging
        bool wantThrough = !overPet && !_isDragging;
        if (wantThrough != _isClickThrough)
        {
            _isClickThrough = wantThrough;
            uint ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
            if (_isClickThrough) ex |=  WS_EX_TRANSPARENT;
            else                 ex &= ~WS_EX_TRANSPARENT;
            SetWindowLong(_hwnd, GWL_EXSTYLE, ex);
        }
        _wasOverPet = overPet;

        // Manual mouse input (replaces OnMouseDown/OnMouseUp which need Physics2D Raycaster)
        if (overPet && Input.GetMouseButtonDown(1))
        {
            _petController?.OnRightClick(Input.mousePosition);
        }
        else if (overPet && Input.GetMouseButtonDown(0) && !_isDragging)
        {
            _isDragging = true;
            GetCursorPos(out _dragStart);
            GetWindowRect(_hwnd, out _winRectAtDragStart);
            _petController?.OnDragBegin();
        }

        if (_isDragging)
        {
            GetCursorPos(out POINT cur);
            int w = _winRectAtDragStart.right  - _winRectAtDragStart.left;
            int h = _winRectAtDragStart.bottom - _winRectAtDragStart.top;
            MoveWindow(_hwnd,
                _winRectAtDragStart.left + (cur.x - _dragStart.x),
                _winRectAtDragStart.top  + (cur.y - _dragStart.y),
                w, h, false);

            if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
                _petController?.OnDragEnd();
            }
        }
#endif
    }

    public void ApplyWindowStyle()
    {
        if (_hwnd == IntPtr.Zero) return;

        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        // Start with click-through; Update() will clear it when mouse enters pet
        SetWindowLong(_hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TRANSPARENT);

        // Required to initialize the layered window before DwmExtendFrameIntoClientArea
        SetLayeredWindowAttributes(_hwnd, 0, 255, LWA_ALPHA);

        // DWM sheet-of-glass: per-pixel alpha transparency over entire client area
        MARGINS m = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref m);

        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
    }

    public void SetVisible(bool visible)
    {
        if (_hwnd != IntPtr.Zero)
            ShowWindow(_hwnd, visible ? 5 : 0);
    }
}
