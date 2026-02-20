using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Window manager — borderless, always-on-top, transparent background, draggable.
/// Uses DWM sheet-of-glass transparency (DwmExtendFrameIntoClientArea with margins=-1).
/// Camera must clear to transparent black (0,0,0,0) with preserveFramebufferAlpha=1.
/// Based on proven approach from XJINE/Unity_TransparentWindowManager (161 stars).
/// </summary>
public class WindowManager : MonoBehaviour
{
    private const int GWL_STYLE   = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_POPUP   = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT  { public int left, top, right, bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr h, out RECT r);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);

    [DllImport("user32.dll")]
    private static extern int ShowWindow(IntPtr h, int cmd);

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    private IntPtr _hwnd;
    private bool   _isDragging;
    private POINT  _dragStart;
    private RECT   _winRectAtDragStart;

    private void Start()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        // Camera: transparent black background
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        _hwnd = GetActiveWindow();
        ApplyWindowStyle();
#endif
    }

    public void ApplyWindowStyle()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Borderless popup window
        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        // DWM: extend frame into entire client area = transparent
        MARGINS margins = new MARGINS
        {
            cxLeftWidth = -1
        };
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
