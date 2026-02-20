using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windows API wrapper for transparent, always-on-top, draggable window.
/// Requires Unity 6 LTS, Windows Standalone x86_64, URP with transparency.
/// </summary>
public class WindowManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Win32 constants
    // -------------------------------------------------------------------------
    private const int GWL_STYLE      = -16;
    private const int GWL_EXSTYLE    = -20;
    private const int WS_POPUP       = unchecked((int)0x80000000);
    private const int WS_VISIBLE     = 0x10000000;
    private const int WS_EX_LAYERED  = 0x00080000;
    private const int WS_EX_TOPMOST  = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int SWP_NOSIZE     = 0x0001;
    private const int SWP_NOMOVE     = 0x0002;
    private const int SWP_FRAMECHANGED = 0x0020;
    private const int LWA_COLORKEY   = 0x00000001;
    private const int LWA_ALPHA      = 0x00000002;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    // -------------------------------------------------------------------------
    // Win32 imports
    // -------------------------------------------------------------------------
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private IntPtr _hwnd;
    private bool   _isDragging;
    private POINT  _dragStart;
    private RECT   _winRectAtDragStart;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [Header("Window")]
    [Range(0, 255)]
    [SerializeField] private byte windowAlpha = 255;
    [SerializeField] private bool clickThrough = false;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        _hwnd = GetActiveWindow();
        ApplyWindowStyle();
#endif
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Remove title bar / border, enable layered (transparent) window, stay on top.</summary>
    public void ApplyWindowStyle()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Strip border, keep popup + visible
        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        // Layered + topmost; optionally transparent to mouse
        int exStyle = WS_EX_LAYERED | WS_EX_TOPMOST;
        if (clickThrough) exStyle |= WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);

        // Black = transparent colour key (matches Camera background)
        SetLayeredWindowAttributes(_hwnd, 0x000000, windowAlpha, LWA_COLORKEY | LWA_ALPHA);

        // Flush the frame change
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                     SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
    }

    /// <summary>Set window opacity 0-255.</summary>
    public void SetAlpha(byte alpha)
    {
        windowAlpha = alpha;
        if (_hwnd != IntPtr.Zero)
            SetLayeredWindowAttributes(_hwnd, 0x000000, alpha, LWA_COLORKEY | LWA_ALPHA);
    }

    // -------------------------------------------------------------------------
    // Drag support (called from PetController)
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
        int newX = _winRectAtDragStart.left + dx;
        int newY = _winRectAtDragStart.top  + dy;
        int w = _winRectAtDragStart.right  - _winRectAtDragStart.left;
        int h = _winRectAtDragStart.bottom - _winRectAtDragStart.top;
        MoveWindow(_hwnd, newX, newY, w, h, false);
    }

    public void EndDrag() => _isDragging = false;

    /// <summary>Show or hide the window (used by TrayIconManager).</summary>
    public void SetVisible(bool visible)
    {
        if (_hwnd == IntPtr.Zero) return;
        int style = GetWindowLong(_hwnd, GWL_STYLE);
        if (visible)
            SetWindowLong(_hwnd, GWL_STYLE, style | WS_VISIBLE);
        else
            SetWindowLong(_hwnd, GWL_STYLE, style & ~WS_VISIBLE);
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                     SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
    }
}
