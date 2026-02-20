using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Window manager — borderless, always-on-top, transparent background, draggable.
/// Uses DWM sheet-of-glass + WS_EX_LAYERED colorkey for full transparency.
/// Camera clears to solid black with alpha=0; DWM composites the transparent region.
/// </summary>
public class WindowManager : MonoBehaviour
{
    private const int GWL_STYLE   = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_POPUP    = unchecked((int)0x80000000);
    private const int WS_VISIBLE  = 0x10000000;
    private const int WS_EX_TOPMOST    = 0x00000008;
    private const int WS_EX_LAYERED    = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int LWA_COLORKEY = 0x00000001;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int left, right, top, bottom; }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT  { public int left, top, right, bottom; }

    // Use SetWindowLongPtr for 64-bit compatibility
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr h, int n, IntPtr v);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr h, int n, int v);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr h, int n);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr h, int n);

    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern int  ShowWindow(IntPtr h, int cmd);

    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled(out bool enabled);

    private delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);

    private IntPtr _hwnd;
    private bool   _isDragging;
    private POINT  _dragStart;
    private RECT   _winRectAtDragStart;

    private static void SetWindowLongSafe(IntPtr hwnd, int index, long value)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, index, new IntPtr(value));
        else
            SetWindowLong32(hwnd, index, (int)value);
    }

    private static long GetWindowLongSafe(IntPtr hwnd, int index)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hwnd, index).ToInt64();
        else
            return GetWindowLong32(hwnd, index);
    }

    private void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Camera: clear to solid color (black) — DWM will handle transparency
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
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

        // 1) Borderless popup
        SetWindowLongSafe(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        // 2) Extended style: topmost + layered
        SetWindowLongSafe(_hwnd, GWL_EXSTYLE, WS_EX_TOPMOST | WS_EX_LAYERED);

        // 3) Set colorkey transparency — use black (0,0,0) as the key color
        //    Camera background is black with alpha=0, so all background pixels are (0,0,0)
        SetLayeredWindowAttributes(_hwnd, 0x00000000, 0, LWA_COLORKEY);

        // 4) Also try DWM extend frame for per-pixel alpha
        DwmIsCompositionEnabled(out bool dwmEnabled);
        if (dwmEnabled)
        {
            MARGINS margins = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        }

        // 5) Apply topmost + frame changes
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
