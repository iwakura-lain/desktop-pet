using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// System tray icon using pure Win32 Shell_NotifyIcon — no WinForms dependency.
/// IL2CPP safe. Windows only; stubbed out on other platforms.
/// </summary>
public class TrayIconManager : MonoBehaviour
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

    // -------------------------------------------------------------------------
    // Win32 constants & structs
    // -------------------------------------------------------------------------
    private const uint NIM_ADD    = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON    = 0x00000002;
    private const uint NIF_TIP     = 0x00000004;
    private const uint WM_APP      = 0x8000;
    private const uint TRAY_MSG    = WM_APP + 1;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP     = 0x0205;

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

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIcon(
        IntPtr hInstance, int nWidth, int nHeight,
        byte cPlanes, byte cBitsPixel,
        byte[] lpbANDbits, byte[] lpbXORbits);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [SerializeField] private string tooltipText = "Desktop Pet";

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private NOTIFYICONDATA _nid;
    private IntPtr         _hwnd;
    private IntPtr         _hIcon;
    private bool           _trayAdded;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Start()
    {
        _hwnd = GetActiveWindow();
        _hIcon = MakeTinyIcon();
        AddTrayIcon();
    }

    private void OnDestroy() => RemoveTrayIcon();

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------
    public void HideToTray()
    {
        ShowWindow(_hwnd, SW_HIDE);
        ModifyTrayTip(tooltipText + " (hidden — double-click to restore)");
    }

    public void ShowFromTray()
    {
        ShowWindow(_hwnd, SW_SHOW);

        WindowManager wm = FindObjectOfType<WindowManager>();
        wm?.ApplyWindowStyle();  // re-apply topmost / transparent

        ModifyTrayTip(tooltipText);
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------
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
        _nid.szTip = tip;
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

    /// <summary>Programmatically build a tiny 16x16 solid-purple icon.</summary>
    private static IntPtr MakeTinyIcon()
    {
        // 16x16 monochrome mask (all transparent = AND=1 XOR=0, except our pixels)
        int stride = 4;  // DWORD-aligned rows for 16 pixels (16/8 = 2 bytes, padded to 4)
        byte[] andMask = new byte[stride * 16];
        byte[] xorMask = new byte[stride * 16];

        // Fill AND mask with 0xFF (transparent), XOR with 0 initially
        for (int i = 0; i < andMask.Length; i++) andMask[i] = 0xFF;

        // Draw a simple 8x8 square in the centre (rows 4-11, cols 4-11)
        for (int row = 4; row < 12; row++)
        {
            // Columns 4-11 → bits 4-11 of the row
            // In a monochrome bitmap, bit 7 = leftmost pixel
            // AND=0 + XOR=1 → white pixel shown
            andMask[row * stride] &= 0xF0; // clear bits for col 0-3 — keep transparent
            andMask[row * stride] &= ~(byte)0x0F; // cols 4-7: AND=0 → opaque
            xorMask[row * stride]  =  0x0F;       // cols 4-7: XOR=1 → white
            andMask[row * stride + 1] &= ~(byte)0xF0; // cols 8-11: AND=0
            xorMask[row * stride + 1]  =  0xF0;       // cols 8-11: XOR=1
        }

        return CreateIcon(IntPtr.Zero, 16, 16, 1, 1, andMask, xorMask);
    }

#else
    // Non-Windows stub
    public void HideToTray()   => gameObject.SetActive(false);
    public void ShowFromTray() => gameObject.SetActive(true);
#endif
}
