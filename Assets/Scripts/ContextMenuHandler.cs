using UnityEngine;

/// <summary>
/// Right-click context menu for the desktop pet.
/// Uses Unity IMGUI (safe under IL2CPP, no WinForms dependency).
/// Attach to the Pet GameObject alongside PetController.
/// </summary>
public class ContextMenuHandler : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [Header("Menu Style")]
    [SerializeField] private int   menuWidth  = 140;
    [SerializeField] private int   menuHeight = 28;  // height per item
    [SerializeField] private Color bgColor    = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    [SerializeField] private Color hoverColor = new Color(0.28f, 0.28f, 0.48f, 1.00f);
    [SerializeField] private Color textColor  = new Color(0.92f, 0.92f, 0.92f, 1.00f);

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private bool    _visible;
    private Vector2 _menuPos;   // screen coords (top-left of menu)
    private int     _hovered = -1;

    private static readonly string[] MenuItems = { "Hide", "Quit" };

    // -------------------------------------------------------------------------
    // Public API — called from PetController
    // -------------------------------------------------------------------------
    public void ShowAt(Vector2 screenPos)
    {
        _menuPos = screenPos;
        _visible = true;
        _hovered = -1;
    }

    public void Hide() => _visible = false;

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------
    private void Update()
    {
        if (!_visible) return;

        // Close on left-click outside menu
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mp = Input.mousePosition;
            // Unity mouse Y is bottom-up; GUI Y is top-down
            float guiY = Screen.height - mp.y;
            Rect  area = new Rect(_menuPos.x, _menuPos.y, menuWidth, menuHeight * MenuItems.Length);
            if (!area.Contains(new Vector2(mp.x, guiY)))
                _visible = false;
        }
    }

    // -------------------------------------------------------------------------
    // GUI
    // -------------------------------------------------------------------------
    private void OnGUI()
    {
        if (!_visible) return;

        // Track hover
        Vector2 mp     = Input.mousePosition;
        float   guiMy  = Screen.height - mp.y;
        _hovered = -1;

        for (int i = 0; i < MenuItems.Length; i++)
        {
            Rect r = new Rect(_menuPos.x, _menuPos.y + i * menuHeight, menuWidth, menuHeight);
            if (r.Contains(new Vector2(mp.x, guiMy)))
                _hovered = i;
        }

        // Draw background
        GUI.color = bgColor;
        GUI.DrawTexture(
            new Rect(_menuPos.x, _menuPos.y, menuWidth, menuHeight * MenuItems.Length),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Draw items
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment  = TextAnchor.MiddleLeft,
            fontSize   = 13,
            padding    = new RectOffset(10, 4, 0, 0),
            normal     = { textColor = textColor },
            hover      = { textColor = textColor }
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

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------
    private void OnMenuItemSelected(int index)
    {
        switch (index)
        {
            case 0: // Hide — minimise to tray via TrayIconManager
                TrayIconManager tray = GetComponent<TrayIconManager>();
                if (tray != null) tray.HideToTray();
                else              gameObject.SetActive(false);
                break;

            case 1: // Quit
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }
}
