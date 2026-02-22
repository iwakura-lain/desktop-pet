using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Window manager — borderless, always-on-top, transparent background, draggable.
/// Uses DWM sheet-of-glass transparency. Click-through is toggled per-frame based
/// on whether the mouse is over an opaque pixel (detected via Camera ray + Collider).
/// </summary>
public class WindowManager : MonoBehaviour
{
    private const int    GWL_STYLE    = -16;
    private const int    GWL_EXSTYLE  = -20;
    private const uint   WS_POPUP     = 0x80000000;
    private const uint   WS_VISIBLE   = 0x10000000;
    private const uint   WS_EX_LAYERED    = 0x00080000;
    private const uint   WS_EX_TRANSPARENT = 0x00000020;
    private const uint   SWP_NOSIZE   = 0x0001;
    private const uint   SWP_NOMOVE   = 0x0002;
    private const uint   SWP_FRAMECHANGED = 0x0020;
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
    [DllImport("Dwmapi.dll")] private static extern uint   DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS m);

    private IntPtr _hwnd;
    private bool   _isDragging;
    private POINT  _dragStart;
    private RECT   _winRectAtDragStart;
    private bool   _isClickThrough = false;

    private void Start()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        Camera cam = Camera.main;
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
        if (_hwnd == IntPtr.Zero || _isDragging) return;

        // Determine if mouse is over the pet collider
        var ray    = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool overPet = Physics2D.GetRayIntersection(ray).collider != null;

        // Toggle click-through: transparent when not over pet
        if (overPet == _isClickThrough)
        {
            _isClickThrough = !overPet;
            uint exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            if (_isClickThrough)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
        }
#endif
    }

    public void ApplyWindowStyle()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Borderless popup + layered (required for WS_EX_TRANSPARENT to work)
        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, WS_EX_LAYERED);

        // DWM: extend frame into entire client area = transparent
        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        // Always on top
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                     SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
    }

    public void SetVisible(bool visible)
    {
        if (_hwnd != IntPtr.Zero)
            ShowWindow(_hwnd, visible ? 5 : 0);
    }

    public void BeginDrag()
    {
        if (_hwnd == IntPtr.Zero) return;
        _isDragging = true;
        // Disable click-through while dragging
        uint exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        exStyle &= ~WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
        _isClickThrough = false;

        GetCursorPos(out _dragStart);
        GetWindowRect(_hwnd, out _winRectAtDragStart);
    }

    public void UpdateDrag()
    {
        if (!_isDragging || _hwnd == IntPtr.Zero) return;
        GetCursorPos(out POINT cur);
        int w = _winRectAtDragStart.right  - _winRectAtDragStart.left;
        int h = _winRectAtDragStart.bottom - _winRectAtDragStart.top;
        MoveWindow(_hwnd,
            _winRectAtDragStart.left + (cur.x - _dragStart.x),
            _winRectAtDragStart.top  + (cur.y - _dragStart.y),
            w, h, false);
    }

    public void EndDrag() => _isDragging = false;
}
