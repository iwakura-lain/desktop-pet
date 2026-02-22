using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// System tray / menu-bar icon manager.
/// Windows: pure Win32 Shell_NotifyIcon (no WinForms).
/// macOS:   NSStatusItem via DesktopPetBridge ObjC plugin.
/// IL2CPP safe on both platforms.
/// </summary>
public class TrayIconManager : MonoBehaviour
{
    [SerializeField] private string tooltipText = "Desktop Pet";

    // =========================================================================
    // Windows P/Invoke
    // =========================================================================
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    private const uint NIM_ADD    = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON    = 0x00000002;
    private const uint NIF_TIP     = 0x00000004;
    private const uint WM_APP      = 0x8000;
    private const uint TRAY_MSG    = WM_APP + 1;

    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint  cbSize;
        public IntPtr hWnd;
        public uint  uID;
        public uint  uFlags;
        public uint  uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint  dwState;
        public uint  dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint  uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint  dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern IntPtr CreateIcon(
        IntPtr hInstance, int nWidth, int nHeight,
        byte cPlanes, byte cBitsPixel,
        byte[] lpbANDbits, byte[] lpbXORbits);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

    private NOTIFYICONDATA _nid;
    private IntPtr         _hwnd;
    private IntPtr         _hIcon;
    private bool           _trayAdded;

    private void Start()
    {
        _hwnd  = GetActiveWindow();
        _hIcon = MakeTinyIcon();
        AddTrayIcon_Win();
    }

    private void OnDestroy() => RemoveTrayIcon_Win();

    private void AddTrayIcon_Win()
    {
        _nid = new NOTIFYICONDATA
        {
            cbSize           = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd             = _hwnd,
            uID              = 1,
            uFlags           = NIF_ICON | NIF_TIP | NIF_MESSAGE,
            uCallbackMessage = TRAY_MSG,
            hIcon            = _hIcon,
            szTip            = tooltipText
        };
        Shell_NotifyIcon(NIM_ADD, ref _nid);
        _trayAdded = true;
    }

    private void ModifyTrayTip_Win(string tip)
    {
        if (!_trayAdded) return;
        _nid.szTip  = tip;
        _nid.uFlags = NIF_TIP;
        Shell_NotifyIcon(NIM_MODIFY, ref _nid);
    }

    private void RemoveTrayIcon_Win()
    {
        if (!_trayAdded) return;
        Shell_NotifyIcon(NIM_DELETE, ref _nid);
        _trayAdded = false;
        if (_hIcon != IntPtr.Zero) { DestroyIcon(_hIcon); _hIcon = IntPtr.Zero; }
    }

    private static IntPtr MakeTinyIcon()
    {
        int stride = 4;
        byte[] andMask = new byte[stride * 16];
        byte[] xorMask = new byte[stride * 16];
        for (int i = 0; i < andMask.Length; i++) andMask[i] = 0xFF;
        for (int row = 4; row < 12; row++)
        {
            andMask[row * stride]     = 0x00;
            xorMask[row * stride]     = 0xFF;
            andMask[row * stride + 1] = 0x00;
            xorMask[row * stride + 1] = 0xFF;
        }
        return CreateIcon(IntPtr.Zero, 16, 16, 1, 1, andMask, xorMask);
    }

#endif  // UNITY_STANDALONE_WIN

    // =========================================================================
    // macOS P/Invoke
    // =========================================================================
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR

    [DllImport("__Internal")] private static extern void MacOS_CreateStatusItem(string tooltip);
    [DllImport("__Internal")] private static extern void MacOS_RemoveStatusItem();
    [DllImport("__Internal")] private static extern void MacOS_SetWindowVisible(bool visible);

    private void Start()  => MacOS_CreateStatusItem(tooltipText);
    private void OnDestroy() => MacOS_RemoveStatusItem();

#endif  // UNITY_STANDALONE_OSX

    // =========================================================================
    // Public API — always visible regardless of platform
    // =========================================================================
    public void HideToTray()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        ShowWindow(_hwnd, SW_HIDE);
        ModifyTrayTip_Win(tooltipText + " (hidden)");
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacOS_SetWindowVisible(false);
#else
        gameObject.SetActive(false);
#endif
    }

    public void ShowFromTray()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        ShowWindow(_hwnd, SW_SHOW);
        FindFirstObjectByType<WindowManager>()?.ApplyWindowStyle();
        ModifyTrayTip_Win(tooltipText);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacOS_SetWindowVisible(true);
        FindFirstObjectByType<WindowManager>()?.ApplyWindowStyle();
#else
        gameObject.SetActive(true);
#endif
    }
}
