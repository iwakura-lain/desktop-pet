using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Window manager — borderless, always-on-top, transparent background, draggable.
/// Uses DWM sheet-of-glass (DwmExtendFrameIntoClientArea margins=-1) for transparency.
/// NOTE: Do NOT call SetLayeredWindowAttributes when using DWM glass — they are mutually
/// exclusive. WS_EX_LAYERED is required for WS_EX_TRANSPARENT to work, but
/// SetLayeredWindowAttributes must NOT be called alongside DwmExtendFrameIntoClientArea.
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
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT  { public int left, top, right, bottom; }

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")] private static extern int    SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    [DllImport("user32.dll")] private static extern uint   GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool   SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] private static extern bool   GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool   GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool   MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);
    [DllImport("user32.dll")] private static extern int    ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern short  GetAsyncKeyState(int vKey);
    [DllImport("Dwmapi.dll")] private static extern uint   DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS m);

    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;

    private IntPtr        _hwnd;
    private bool          _isDragging;
    private POINT         _dragStart;
    private RECT          _winRectAtDragStart;
    private PetController _petController;

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    private bool _isClickThrough  = true;
    private int  _diagFrames      = 0;
    private bool _prevLeftDown    = false;
    private bool _prevRightDown   = false;
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
        StartCoroutine(InitWindowDelayed());
#endif
    }

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    private System.Collections.IEnumerator InitWindowDelayed()
    {
        // Wait a few frames for Unity's window to become active
        for (int i = 0; i < 5; i++)
            yield return null;

        // Try GetActiveWindow first, fall back to FindWindow by title
        _hwnd = GetActiveWindow();
        if (_hwnd == IntPtr.Zero)
            _hwnd = FindWindow(null, Application.productName);

        Debug.Log($"[WM] hwnd={_hwnd} product={Application.productName}");
        if (_hwnd != IntPtr.Zero)
        {
            ApplyWindowStyle();
            Debug.Log($"[WM] after ApplyWindowStyle exStyle=0x{GetWindowLong(_hwnd, GWL_EXSTYLE):X}");
        }
        else
        {
            Debug.LogError("[WM] Failed to obtain window handle!");
        }
    }
#endif

    private void Update()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        if (_hwnd == IntPtr.Zero) return;

        // Win32-only mouse state — no dependency on Unity Input system
        GetCursorPos(out POINT cursorScreen);
        GetWindowRect(_hwnd, out RECT winRect);
        int winH = winRect.bottom - winRect.top;
        float localX = cursorScreen.x - winRect.left;
        float localY = cursorScreen.y - winRect.top;
        // Unity screen coords: Y=0 at bottom
        var unityPos = new Vector3(localX, winH - localY, 0f);

        var  ray     = Camera.main.ScreenPointToRay(unityPos);
        var  hit     = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
        bool overPet = hit.collider != null;

        // Diagnostic: log hit status every 120 frames
        _diagFrames++;
        if (_diagFrames % 120 == 0)
        {
            Debug.Log($"[WM] overPet={overPet} collider={hit.collider} unityPos={unityPos} isClickThrough={_isClickThrough}");
        }

        // Toggle click-through
        bool wantThrough = !overPet && !_isDragging;
        if (wantThrough != _isClickThrough)
        {
            _isClickThrough = wantThrough;
            uint ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
            if (_isClickThrough) ex |=  WS_EX_TRANSPARENT;
            else                 ex &= ~WS_EX_TRANSPARENT;
            SetWindowLong(_hwnd, GWL_EXSTYLE, ex);
            Debug.Log($"[WM] clickThrough={_isClickThrough} overPet={overPet} exStyle=0x{ex:X}");
        }

        // GetAsyncKeyState: bit 15 = currently down, bit 0 = pressed since last call
        bool leftDown  = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        bool rightDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
        bool leftPressed  = leftDown  && !_prevLeftDown;
        bool leftReleased = !leftDown && _prevLeftDown;
        bool rightPressed = rightDown && !_prevRightDown;
        _prevLeftDown  = leftDown;
        _prevRightDown = rightDown;

        // Manual mouse input
        if (overPet && rightPressed)
        {
            Debug.Log("[WM] Right click on pet");
            _petController?.OnRightClick(new Vector2(unityPos.x, unityPos.y));
        }
        else if (overPet && leftPressed && !_isDragging)
        {
            Debug.Log("[WM] Drag begin");
            _isDragging = true;
            _dragStart  = cursorScreen;
            GetWindowRect(_hwnd, out _winRectAtDragStart);
            _petController?.OnDragBegin();
        }

        if (_isDragging)
        {
            int w = _winRectAtDragStart.right  - _winRectAtDragStart.left;
            int h = _winRectAtDragStart.bottom - _winRectAtDragStart.top;
            MoveWindow(_hwnd,
                _winRectAtDragStart.left + (cursorScreen.x - _dragStart.x),
                _winRectAtDragStart.top  + (cursorScreen.y - _dragStart.y),
                w, h, false);

            if (leftReleased)
            {
                Debug.Log("[WM] Drag end");
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
        // WS_EX_LAYERED is required for click-through (WS_EX_TRANSPARENT) to work.
        // Start transparent; Update() removes WS_EX_TRANSPARENT when mouse is over pet.
        // Do NOT call SetLayeredWindowAttributes here — it conflicts with DWM glass.
        SetWindowLong(_hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TRANSPARENT);

        // DWM sheet-of-glass: per-pixel alpha over entire client area
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
