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
    // Windows implementation
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
        AddTrayIcon();
    }

    private void OnDestroy() => RemoveTrayIcon();

    public void HideToTray()
    {
        ShowWindow(_hwnd, SW_HIDE);
        ModifyTrayTip(tooltipText + " (hidden — double-click to restore)");
    }

    public void ShowFromTray()
    {
        ShowWindow(_hwnd, SW_SHOW);
        FindFirstObjectByType<WindowManager>()?.ApplyWindowStyle();
        ModifyTrayTip(tooltipText);
    }

    private void AddTrayIcon()
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

    private void ModifyTrayTip(string tip)
    {
        if (!_trayAdded) return;
        _nid.szTip  = tip;
        _nid.uFlags = NIF_TIP;
        Shell_NotifyIcon(NIM_MODIFY, ref _nid);
    }

    private void RemoveTrayIcon()
    {
        if (!_trayAdded) return;
        Shell_NotifyIcon(NIM_DELETE, ref _nid);
        _trayAdded = false;
        if (_hIcon != IntPtr.Zero) { DestroyIcon(_hIcon); _hIcon = IntPtr.Zero; }
    }

    /// <summary>Programmatically build a tiny 16x16 solid-white icon.</summary>
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
    // macOS implementation
    // =========================================================================
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR

    [DllImport("__Internal")] private static extern void MacOS_CreateStatusItem(string tooltip);
    [DllImport("__Internal")] private static extern void MacOS_RemoveStatusItem();
    [DllImport("__Internal")] private static extern void MacOS_SetWindowVisible(bool visible);

    private void Start()
    {
        MacOS_CreateStatusItem(tooltipText);
    }

    private void OnDestroy()
    {
        MacOS_RemoveStatusItem();
    }

    public void HideToTray()
    {
        MacOS_SetWindowVisible(false);
    }

    public void ShowFromTray()
    {
        MacOS_SetWindowVisible(true);
        FindFirstObjectByType<WindowManager>()?.ApplyWindowStyle();
    }

#endif  // UNITY_STANDALONE_OSX

    // =========================================================================
    // Editor / other platforms stub
    // =========================================================================
#if !UNITY_STANDALONE_WIN && !UNITY_STANDALONE_OSX
    private void Start() { }
    public void HideToTray()   => gameObject.SetActive(false);
    public void ShowFromTray() => gameObject.SetActive(true);
#endif
}
