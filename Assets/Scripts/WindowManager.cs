using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windows API wrapper for transparent, always-on-top, draggable window.
/// Uses colorkey #010101 (near-black, not pure black) so Unity's black
/// background is punched through while dark sprite pixels remain visible.
/// Grabs the HWND via EnumWindows on a background coroutine to survive
/// the race between Unity startup and window creation.
/// </summary>
public class WindowManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Win32 constants
    // -------------------------------------------------------------------------
    private const int GWL_STYLE        = -16;
    private const int GWL_EXSTYLE      = -20;
    private const int WS_POPUP         = unchecked((int)0x80000000);
    private const int WS_VISIBLE       = 0x10000000;
    private const int WS_EX_LAYERED    = 0x00080000;
    private const int WS_EX_TOPMOST    = 0x00000008;
    private const int WS_EX_TRANSPARENT= 0x00000020;
    private const uint SWP_NOSIZE      = 0x0001;
    private const uint SWP_NOMOVE      = 0x0002;
    private const uint SWP_FRAMECHANGED= 0x0020;
    private const uint LWA_COLORKEY    = 0x00000001;
    private const uint LWA_ALPHA       = 0x00000002;

    // Use pure black #000000 as colorkey. With preserveFramebufferAlpha=1,
    // Unity renders the background as transparent; the colorkey punches
    // through any remaining black pixels. Sprite outlines use deep purple
    // so they are never eaten by the colorkey.
    private const uint COLORKEY = 0x00000000;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    // -------------------------------------------------------------------------
    // Win32 imports
    // -------------------------------------------------------------------------
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int n, int v);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int n);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr h, uint cr, byte a, uint f);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);

    private delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT  { public int left, top, right, bottom; }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [Header("Window")]
    [Range(0, 255)]
    [SerializeField] private byte windowAlpha = 255;
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
        // Wait up to 3 seconds for the Unity window to appear
        uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        for (int attempt = 0; attempt < 30 && _hwnd == IntPtr.Zero; attempt++)
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
            Debug.LogError("[WindowManager] Could not find Unity window handle.");
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

        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        int exStyle = WS_EX_LAYERED | WS_EX_TOPMOST;
        if (clickThrough) exStyle |= WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);

        SetLayeredWindowAttributes(_hwnd, COLORKEY, windowAlpha, LWA_COLORKEY | LWA_ALPHA);

        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                     SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
    }

    public void SetAlpha(byte alpha)
    {
        windowAlpha = alpha;
        if (_hwnd != IntPtr.Zero)
            SetLayeredWindowAttributes(_hwnd, COLORKEY, alpha, LWA_COLORKEY | LWA_ALPHA);
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
        int dx = cur.x - _dragStart.x;
        int dy = cur.y - _dragStart.y;
        int w = _winRectAtDragStart.right  - _winRectAtDragStart.left;
        int h = _winRectAtDragStart.bottom - _winRectAtDragStart.top;
        MoveWindow(_hwnd, _winRectAtDragStart.left + dx, _winRectAtDragStart.top + dy, w, h, false);
    }

    public void EndDrag() => _isDragging = false;

    public void SetVisible(bool visible)
    {
        if (_hwnd == IntPtr.Zero) return;
        int style = GetWindowLong(_hwnd, GWL_STYLE);
        SetWindowLong(_hwnd, GWL_STYLE, visible ? (style | WS_VISIBLE) : (style & ~WS_VISIBLE));
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
    }
}
