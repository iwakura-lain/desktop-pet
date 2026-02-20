using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Window manager — borderless, always-on-top, transparent background, draggable.
/// Uses colorkey transparency: camera background is set to a magic color (magenta),
/// and SetLayeredWindowAttributes makes that color transparent.
/// </summary>
public class WindowManager : MonoBehaviour
{
    // The colorkey color — must match camera background exactly
    // Using RGB(1, 0, 1) — nearly black magenta, won't appear in normal sprites
    public static readonly Color32 ColorKey = new Color32(1, 0, 1, 255);

    private const int GWL_STYLE   = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_POPUP    = unchecked((int)0x80000000);
    private const int WS_VISIBLE  = 0x10000000;
    private const int WS_EX_TOPMOST  = 0x00000008;
    private const int WS_EX_LAYERED  = 0x00080000;
    private const int LWA_COLORKEY   = 0x00000001;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT  { public int left, top, right, bottom; }

    [DllImport("user32.dll")] private static extern int  SetWindowLong(IntPtr h, int n, int v);
    [DllImport("user32.dll")] private static extern int  GetWindowLong(IntPtr h, int n);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern int  ShowWindow(IntPtr h, int cmd);

    private delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);

    private IntPtr _hwnd;
    private bool   _isDragging;
    private POINT  _dragStart;
    private RECT   _winRectAtDragStart;

    private void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Set camera background to colorkey color
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = new Color(ColorKey.r / 255f, ColorKey.g / 255f, ColorKey.b / 255f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
        StartCoroutine(InitWindow());
#endif
    }

    private IEnumerator InitWindow()
    {
        uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        for (int i = 0; i < 50 && _hwnd == IntPtr.Zero; i++)
        {
            yield return new WaitForSeconds(0.1f);
            EnumWindows((h, _) =>
            {
                if (!IsWindowVisible(h)) return true;
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == myPid) { _hwnd = h; return false; }
                return true;
            }, IntPtr.Zero);
        }
        if (_hwnd == IntPtr.Zero) yield break;

        ApplyWindowStyle();
    }

    public void ApplyWindowStyle()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Borderless popup
        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        // Layered + topmost
        SetWindowLong(_hwnd, GWL_EXSTYLE, WS_EX_TOPMOST | WS_EX_LAYERED);
        // Colorkey transparency: RGB(1, 0, 1)
        uint colorRef = (uint)ColorKey.r | ((uint)ColorKey.g << 8) | ((uint)ColorKey.b << 16);
        SetLayeredWindowAttributes(_hwnd, colorRef, 0, LWA_COLORKEY);
        // Apply
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
