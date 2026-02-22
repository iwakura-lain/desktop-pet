using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Right-click context menu for the desktop pet.
/// Uses Unity IMGUI (safe under IL2CPP, no WinForms dependency).
/// Windows: Win32 GetCursorPos/GetAsyncKeyState.
/// macOS: DesktopPetBridge MacOS_GetCursorPos/MacOS_IsMouseButtonDown.
/// </summary>
public class ContextMenuHandler : MonoBehaviour
{
    [Header("Menu Style")]
    [SerializeField] private int   menuWidth  = 140;
    [SerializeField] private int   menuHeight = 28;
    [SerializeField] private Color bgColor    = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    [SerializeField] private Color hoverColor = new Color(0.28f, 0.28f, 0.48f, 1.00f);
    [SerializeField] private Color textColor  = new Color(0.92f, 0.92f, 0.92f, 1.00f);

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [DllImport("user32.dll")] private static extern bool  GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    private const int VK_LBUTTON = 0x01;
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void MacOS_GetCursorPos(out float x, out float y);
    [DllImport("__Internal")] private static extern bool MacOS_IsMouseButtonDown(int button);
#endif

    private bool    _visible;
    private Vector2 _menuPos;
    private int     _hovered = -1;
    private bool    _prevLeftDown = false;

    private static readonly string[] MenuItems = { "Hide", "Quit" };

    public void ShowAt(Vector2 screenPos)
    {
        _menuPos = screenPos;
        _visible = true;
        _hovered = -1;
    }

    public void Hide() => _visible = false;

    private void Update()
    {
        if (!_visible) return;

        bool leftDown = GetLeftMouseDown();
        bool leftPressed = leftDown && !_prevLeftDown;
        _prevLeftDown = leftDown;

        if (leftPressed)
        {
            Vector2 mp = GetMouseGuiPos();
            Rect area  = new Rect(_menuPos.x, _menuPos.y, menuWidth, menuHeight * MenuItems.Length);
            if (!area.Contains(mp))
                _visible = false;
        }
    }

    private void OnGUI()
    {
        if (!_visible) return;

        Vector2 mp = GetMouseGuiPos();
        _hovered = -1;

        for (int i = 0; i < MenuItems.Length; i++)
        {
            Rect r = new Rect(_menuPos.x, _menuPos.y + i * menuHeight, menuWidth, menuHeight);
            if (r.Contains(mp))
                _hovered = i;
        }

        GUI.color = bgColor;
        GUI.DrawTexture(
            new Rect(_menuPos.x, _menuPos.y, menuWidth, menuHeight * MenuItems.Length),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize  = 13,
            padding   = new RectOffset(10, 4, 0, 0),
            normal    = { textColor = textColor },
            hover     = { textColor = textColor }
        };

        for (int i = 0; i < MenuItems.Length; i++)
        {
            Rect r = new Rect(_menuPos.x, _menuPos.y + i * menuHeight, menuWidth, menuHeight);

            if (_hovered == i)
            {
                GUI.color = hoverColor;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (GUI.Button(r, MenuItems[i], style))
            {
                OnMenuItemSelected(i);
                _visible = false;
            }
        }
    }

    private bool GetLeftMouseDown()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        return (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        return MacOS_IsMouseButtonDown(0);
#else
        return false;
#endif
    }

    // Convert native screen coords to Unity GUI coords (Y flipped)
    private Vector2 GetMouseGuiPos()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        GetCursorPos(out POINT p);
        return new Vector2(p.x, Screen.height - p.y);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacOS_GetCursorPos(out float cx, out float cy);
        // macOS reports bottom-up; Unity GUI is top-down
        return new Vector2(cx, Screen.height - cy);
#else
        return Vector2.zero;
#endif
    }

    private void OnMenuItemSelected(int index)
    {
        switch (index)
        {
            case 0:
                TrayIconManager tray = GetComponent<TrayIconManager>();
                if (tray != null) tray.HideToTray();
                else              gameObject.SetActive(false);
                break;
            case 1:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }
}
