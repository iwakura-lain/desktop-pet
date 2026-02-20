using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// System tray icon for the desktop pet (Windows only).
/// Runs a WinForms NotifyIcon on a dedicated STA thread so it does not
/// block the Unity main thread.
/// Use HideToTray() / ShowFromTray() to toggle window visibility.
/// </summary>
public class TrayIconManager : MonoBehaviour
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [SerializeField] private string tooltipText = "Desktop Pet";

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private Thread _staThread;
    private System.Windows.Forms.NotifyIcon _trayIcon;
    private volatile bool _showRequested  = false;
    private volatile bool _quitRequested  = false;
    private volatile bool _trayReady      = false;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Start()
    {
        _staThread = new Thread(TrayThreadProc);
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.IsBackground = true;
        _staThread.Start();
    }

    private void Update()
    {
        if (_showRequested)
        {
            _showRequested = false;
            ShowWindow();
        }
        if (_quitRequested)
        {
            _quitRequested = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void OnDestroy()
    {
        DisposeTray();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------
    public void HideToTray()
    {
        // Hide Unity window
        WindowManager wm = FindObjectOfType<WindowManager>();
        if (wm != null) wm.SetVisible(false);

        if (_trayReady && _trayIcon != null)
            _trayIcon.Visible = true;
    }

    public void ShowFromTray()
    {
        _showRequested = true;
        if (_trayReady && _trayIcon != null)
            _trayIcon.Visible = false;
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------
    private void ShowWindow()
    {
        WindowManager wm = FindObjectOfType<WindowManager>();
        if (wm != null) wm.SetVisible(true);
    }

    private void TrayThreadProc()
    {
        try
        {
            // Build a minimal 16x16 icon programmatically (purple circle)
            using var bmp = new System.Drawing.Bitmap(16, 16,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.FillEllipse(
                    new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(180, 100, 160, 220)),
                    1, 1, 13, 13);
            }
            var icon = System.Drawing.Icon.FromHandle(bmp.GetHicon());

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon    = icon,
                Text    = tooltipText,
                Visible = false
            };

            // Context menu
            var menu = new System.Windows.Forms.ContextMenuStrip();
            var showItem = new System.Windows.Forms.ToolStripMenuItem("Show");
            var quitItem = new System.Windows.Forms.ToolStripMenuItem("Quit");

            showItem.Click += (s, e) => ShowFromTray();
            quitItem.Click += (s, e) => { _quitRequested = true; };
            _trayIcon.DoubleClick += (s, e) => ShowFromTray();

            menu.Items.Add(showItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(quitItem);
            _trayIcon.ContextMenuStrip = menu;

            _trayReady = true;
            System.Windows.Forms.Application.Run();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TrayIconManager] Tray thread error: {ex.Message}");
        }
    }

    private void DisposeTray()
    {
        try
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            System.Windows.Forms.Application.Exit();
        }
        catch { /* ignore on shutdown */ }
    }
#else
    // Non-Windows stub — no tray icon
    public void HideToTray()  => gameObject.SetActive(false);
    public void ShowFromTray()=> gameObject.SetActive(true);
#endif
}
