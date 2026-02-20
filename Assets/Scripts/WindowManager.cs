using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Transparent, borderless, always-on-top Unity window for Windows desktop pet.
///
/// Strategy: DWM "sheet of glass" — extend the DWM frame to cover the entire
/// client area. This makes the window truly transparent at the OS compositor
/// level without relying on a colorkey. The Camera clears to Color.clear
/// (0,0,0,0) and Unity renders sprites on top of the transparent glass.
///
/// Requirements:
///   - PlayerSettings → preserveFramebufferAlpha = true  (already set)
///   - Camera background = (0,0,0,0), ClearFlags = SolidColor
///   - This script on any active GameObject
/// </summary>
public class WindowManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Win32 / DWM constants
    // -------------------------------------------------------------------------
    private const int GWL_STYLE      = -16;
    private const int GWL_EXSTYLE    = -20;
    private const int WS_POPUP       = unchecked((int)0x80000000);
    private const int WS_VISIBLE     = 0x10000000;
    private const int WS_EX_LAYERED  = 0x00080000;
    private const int WS_EX_TOPMOST  = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const uint SWP_NOSIZE    = 0x0001;
    private const uint SWP_NOMOVE    = 0x0002;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int left, right, top, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    // -------------------------------------------------------------------------
    // Win32 imports
    // -------------------------------------------------------------------------
    [DllImport("user32.dll")] private static extern int  SetWindowLong(IntPtr h, int n, int v);
    [DllImport("user32.dll")] private static extern int  GetWindowLong(IntPtr h, int n);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("dwmapi.dll")] private static extern int  DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);

    private delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);

    [DllImport("user32.dll")] private static extern int ShowWindow(IntPtr h, int cmd);
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [SerializeField] private bool clickThrough = false;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private IntPtr _hwnd;
    private bool   _isDragging;
    private POINT  _dragStart;
    private RECT   _winRectAtDragStart;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        StartCoroutine(InitWindow());
#endif
    }

    private IEnumerator InitWindow()
    {
        uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        // Poll until our visible window appears (up to 5 s)
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

        if (_hwnd == IntPtr.Zero)
        {
            Debug.LogError("[WindowManager] Window handle not found.");
            yield break;
        }

        ApplyWindowStyle();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------
    public void ApplyWindowStyle()
    {
        if (_hwnd == IntPtr.Zero) return;

        // 1. Strip title bar / border — popup only
        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        // 2. Make layered + topmost (optionally click-through)
        int ex = WS_EX_LAYERED | WS_EX_TOPMOST;
        if (clickThrough) ex |= WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, ex);

        // 3. DWM "sheet of glass" — extend frame to cover entire client area.
        //    Margins of -1 on all sides signal "cover everything".
        var m = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref m);

        // 4. Flush style change
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                     SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
    }

    public void SetVisible(bool visible)
    {
        if (_hwnd == IntPtr.Zero) return;
        ShowWindow(_hwnd, visible ? SW_SHOW : SW_HIDE);
        if (visible) ApplyWindowStyle();
    }

    // -------------------------------------------------------------------------
    // Drag (called by PetController)
    // -------------------------------------------------------------------------
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
        int dx = cur.x - _dragStart.x;
        int dy = cur.y - _dragStart.y;
        int w  = _winRectAtDragStart.right  - _winRectAtDragStart.left;
        int h  = _winRectAtDragStart.bottom - _winRectAtDragStart.top;
        MoveWindow(_hwnd,
            _winRectAtDragStart.left + dx,
            _winRectAtDragStart.top  + dy,
            w, h, false);
    }

    public void EndDrag() => _isDragging = false;
}
