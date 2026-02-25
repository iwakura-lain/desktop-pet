using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Cross-platform window manager: transparent, borderless, always-on-top, draggable.
/// Windows: DWM sheet-of-glass + WS_EX_TRANSPARENT click-through toggle.
/// macOS:   NSWindow clearColor + ignoresMouseEvents toggle via ObjC bridge.
/// </summary>
public class WindowManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Windows P/Invoke
    // -------------------------------------------------------------------------
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
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
    private const int  SM_CXSCREEN       = 0;
    private const int  SM_CYSCREEN       = 1;
    private const int  VK_LBUTTON        = 0x01;
    private const int  VK_RBUTTON        = 0x02;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT  { public int left, top, right, bottom; }

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] private static extern int    SetWindowLong(IntPtr h, int i, uint v);
    [DllImport("user32.dll")] private static extern uint   GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern bool   SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] private static extern bool   GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool   GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool   MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);
    [DllImport("user32.dll")] private static extern int    ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern short  GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern int    GetSystemMetrics(int n);
    [DllImport("Dwmapi.dll")] private static extern uint   DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);

    private IntPtr _hwnd;
    private bool   _isClickThrough = true;
    private int    _diagFrames     = 0;
    private bool   _prevLeftDown   = false;
    private bool   _prevRightDown  = false;
    private POINT  _dragStartCursor;
    private RECT   _dragStartWinRect;
#endif

    // -------------------------------------------------------------------------
    // macOS P/Invoke (Objective-C bridge in Assets/Plugins/macOS/DesktopPetBridge.mm)
    // -------------------------------------------------------------------------
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    [DllImport("DesktopPetBridge")] private static extern void MacOS_ApplyWindowStyle();
    [DllImport("DesktopPetBridge")] private static extern void MacOS_SetIgnoreMouse(bool ignore);
    [DllImport("DesktopPetBridge")] private static extern void MacOS_GetCursorPos(out float x, out float y);
    [DllImport("DesktopPetBridge")] private static extern bool MacOS_IsMouseButtonDown(int button);
    [DllImport("DesktopPetBridge")] private static extern void MacOS_MoveWindow(float x, float y, float w, float h);
    [DllImport("DesktopPetBridge")] private static extern void MacOS_GetWindowRect(out float x, out float y, out float w, out float h);
    [DllImport("DesktopPetBridge")] private static extern int  MacOS_GetScreenWidth();
    [DllImport("DesktopPetBridge")] private static extern int  MacOS_GetScreenHeight();

    private bool  _isClickThrough  = true;
    private int   _diagFrames      = 0;
    private bool  _prevLeftDown    = false;
    private bool  _prevRightDown   = false;
    private float _dragStartCursorX, _dragStartCursorY;
    private float _dragStartWinX,    _dragStartWinY;
    private float _dragStartWinW,    _dragStartWinH;
#endif

    // -------------------------------------------------------------------------
    // Shared
    // -------------------------------------------------------------------------
    private bool          _isDragging;
    private PetController _petController;

    // Scale multiplier applied to the Pet GameObject on macOS.
    // Increase to make the pet larger. Default 3.0 = 3x the scene default scale.
    [SerializeField] private float macOSPetScale = 3.0f;

    private void Start()
    {
        _petController = FindFirstObjectByType<PetController>();

        var cam = Camera.main;
        if (cam != null)
        {
            // Both platforms: transparent black background.
            // On macOS, MTKView layer is set transparent in MacOS_ApplyWindowStyle
            // so this SolidColor (alpha=0) punches through to the desktop.
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            Debug.Log($"[WM] Start: cam.clearFlags={cam.clearFlags} backgroundColor={cam.backgroundColor}");

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            // Add AlphaBackground to force alpha=0 in background pixels after
            // each frame, overriding Unity's built-in pipeline alpha overwrite.
            if (cam.GetComponent<AlphaBackground>() == null)
                cam.gameObject.AddComponent<AlphaBackground>();
            Debug.Log($"[WM] AlphaBackground component: {cam.GetComponent<AlphaBackground>() != null}");
#endif
        }
        else
        {
            Debug.LogError("[WM] Camera.main is NULL in Start!");
        }

#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        StartCoroutine(InitWindowDelayed());
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private IEnumerator InitWindowDelayed()
    {
        for (int i = 0; i < 5; i++) yield return null;

        _hwnd = GetActiveWindow();
        if (_hwnd == IntPtr.Zero)
            _hwnd = FindWindow(null, Application.productName);

        if (_hwnd == IntPtr.Zero) { Debug.LogError("[WM] Failed to get HWND"); yield break; }

        int sw = GetSystemMetrics(SM_CXSCREEN);
        int sh = GetSystemMetrics(SM_CYSCREEN);
        Screen.SetResolution(sw, sh, false);
        yield return null;
        MoveWindow(_hwnd, 0, 0, sw, sh, false);

        ApplyWindowStyle_Win();
        Debug.Log($"[WM] Win hwnd={_hwnd} {sw}x{sh} exStyle=0x{GetWindowLong(_hwnd, GWL_EXSTYLE):X}");
    }

    private void ApplyWindowStyle_Win()
    {
        SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TRANSPARENT);
        MARGINS m = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref m);
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
    }
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private IEnumerator InitWindowDelayed()
    {
        // Wait 1 frame for Unity to finish initialization, then apply immediately.
        yield return null;

        int sw = MacOS_GetScreenWidth();
        int sh = MacOS_GetScreenHeight();
        Debug.Log($"[WM] macOS screen={sw}x{sh}");

        // Apply transparency BEFORE SetResolution to catch the initial window
        MacOS_ApplyWindowStyle();
        Debug.Log("[WM] macOS MacOS_ApplyWindowStyle() called (pre-resize)");

        Screen.SetResolution(sw, sh, false);
        yield return null;
        MacOS_MoveWindow(0, 0, sw, sh);
        Debug.Log($"[WM] macOS MoveWindow(0,0,{sw},{sh}) done");

        // Wait several frames for Metal to finish RecreateSurface after SetResolution,
        // then re-apply transparency so the new CAMetalLayer is also transparent.
        for (int i = 0; i < 5; i++) yield return null;
        MacOS_ApplyWindowStyle();
        Debug.Log("[WM] macOS MacOS_ApplyWindowStyle() called (post-RecreateSurface)");

        MacOS_SetIgnoreMouse(true);  // start click-through

        // Log camera state after init
        var cam = Camera.main;
        if (cam != null)
            Debug.Log($"[WM] post-init cam.clearFlags={cam.clearFlags} bg={cam.backgroundColor} allowHDR={cam.allowHDR} targetTexture={cam.targetTexture}");

        // Scale the Pet GameObject to make it larger on macOS.
        var pet = GameObject.Find("Pet");
        if (pet != null)
            pet.transform.localScale = Vector3.one * macOSPetScale;

        Debug.Log($"[WM] macOS {sw}x{sh} petScale={macOSPetScale}");
    }
#endif

    private void Update()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_hwnd == IntPtr.Zero) return;

        GetCursorPos(out POINT cur);
        GetWindowRect(_hwnd, out RECT wr);
        int winH   = wr.bottom - wr.top;
        var uPos   = new Vector3(cur.x - wr.left, winH - (cur.y - wr.top), 0f);
        var hit    = Physics2D.GetRayIntersection(Camera.main.ScreenPointToRay(uPos), Mathf.Infinity);
        bool overPet = hit.collider != null;

        _diagFrames++;
        if (_diagFrames % 120 == 0)
            Debug.Log($"[WM] overPet={overPet} uPos={uPos} clickThrough={_isClickThrough}");

        bool wantThrough = !overPet && !_isDragging;
        if (wantThrough != _isClickThrough)
        {
            _isClickThrough = wantThrough;
            uint ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
            if (_isClickThrough) ex |=  WS_EX_TRANSPARENT;
            else                 ex &= ~WS_EX_TRANSPARENT;
            SetWindowLong(_hwnd, GWL_EXSTYLE, ex);
            Debug.Log($"[WM] clickThrough={_isClickThrough} exStyle=0x{ex:X}");
        }

        bool leftDown     = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        bool rightDown    = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
        bool leftPressed  = leftDown  && !_prevLeftDown;
        bool leftReleased = !leftDown && _prevLeftDown;
        bool rightPressed = rightDown && !_prevRightDown;
        _prevLeftDown  = leftDown;
        _prevRightDown = rightDown;

        if (overPet && rightPressed)
        {
            Debug.Log("[WM] Right click on pet");
            _petController?.OnRightClick(new Vector2(uPos.x, uPos.y));
        }
        else if (overPet && leftPressed && !_isDragging)
        {
            Debug.Log("[WM] Drag begin");
            _isDragging = true;
            _dragStartCursor = cur;
            GetWindowRect(_hwnd, out _dragStartWinRect);
            _petController?.OnDragBegin();
        }

        if (_isDragging)
        {
            int w = _dragStartWinRect.right  - _dragStartWinRect.left;
            int h = _dragStartWinRect.bottom - _dragStartWinRect.top;
            MoveWindow(_hwnd,
                _dragStartWinRect.left + (cur.x - _dragStartCursor.x),
                _dragStartWinRect.top  + (cur.y - _dragStartCursor.y),
                w, h, false);
            if (leftReleased)
            {
                Debug.Log("[WM] Drag end");
                _isDragging = false;
                _petController?.OnDragEnd();
            }
        }
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacOS_GetCursorPos(out float cx, out float cy);
        MacOS_GetWindowRect(out float wx, out float wy, out float ww, out float wh);
        // Convert screen cursor to window-local Unity coords (Y already bottom-up on macOS)
        float localX = cx - wx;
        float localY = cy - wy;
        var uPos   = new Vector3(localX, localY, 0f);
        var hit    = Physics2D.GetRayIntersection(Camera.main.ScreenPointToRay(uPos), Mathf.Infinity);
        bool overPet = hit.collider != null;

        _diagFrames++;
        if (_diagFrames % 120 == 0)
            Debug.Log($"[WM] macOS overPet={overPet} uPos={uPos} clickThrough={_isClickThrough}");

        bool wantThrough = !overPet && !_isDragging;
        if (wantThrough != _isClickThrough)
        {
            _isClickThrough = wantThrough;
            MacOS_SetIgnoreMouse(_isClickThrough);
            Debug.Log($"[WM] macOS clickThrough={_isClickThrough}");
        }

        bool leftDown     = MacOS_IsMouseButtonDown(0);
        bool rightDown    = MacOS_IsMouseButtonDown(1);
        bool leftPressed  = leftDown  && !_prevLeftDown;
        bool leftReleased = !leftDown && _prevLeftDown;
        bool rightPressed = rightDown && !_prevRightDown;
        _prevLeftDown  = leftDown;
        _prevRightDown = rightDown;

        if (overPet && rightPressed)
        {
            _petController?.OnRightClick(new Vector2(uPos.x, uPos.y));
        }
        else if (overPet && leftPressed && !_isDragging)
        {
            Debug.Log("[WM] macOS Drag begin");
            _isDragging = true;
            _dragStartCursorX = cx;
            _dragStartCursorY = cy;
            _dragStartWinX = wx; _dragStartWinY = wy;
            _dragStartWinW = ww; _dragStartWinH = wh;
            _petController?.OnDragBegin();
        }

        if (_isDragging)
        {
            MacOS_MoveWindow(
                _dragStartWinX + (cx - _dragStartCursorX),
                _dragStartWinY + (cy - _dragStartCursorY),
                _dragStartWinW, _dragStartWinH);
            if (leftReleased)
            {
                Debug.Log("[WM] macOS Drag end");
                _isDragging = false;
                _petController?.OnDragEnd();
            }
        }
#endif
    }

    public void SetVisible(bool visible)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_hwnd != IntPtr.Zero) ShowWindow(_hwnd, visible ? 5 : 0);
#endif
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        // On macOS visibility is handled by TrayIconManager via MacOS_SetWindowVisible
#endif
    }

    /// <summary>Re-apply window style after show-from-tray (public for TrayIconManager).</summary>
    public void ApplyWindowStyle()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_hwnd != IntPtr.Zero) ApplyWindowStyle_Win();
#endif
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacOS_ApplyWindowStyle();
#endif
    }
}
