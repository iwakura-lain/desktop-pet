using UnityEngine;

/// <summary>
/// Creates all Pet components at runtime via code.
/// This bypasses the Unity Scene GUID binding issue entirely —
/// no fake GUIDs needed, components are added directly.
/// Attach ONLY this script to any GameObject in the Scene.
/// </summary>
public class PetBootstrap : MonoBehaviour
{
    private void Awake()
    {
        // ---- Pet GameObject ----
        var pet = new GameObject("Pet");
        pet.layer = 0;

        // BoxCollider2D (for click detection)
        var col = pet.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);

        // Live2DController — loads Natori model and drives motions
        pet.AddComponent<Live2DController>();

        // ContextMenuHandler
        pet.AddComponent<ContextMenuHandler>();

        // TrayIconManager
        pet.AddComponent<TrayIconManager>();

        // PetController (auto-finds WindowManager)
        pet.AddComponent<PetController>();

        // ---- WindowManager on this GameObject ----
        gameObject.AddComponent<WindowManager>();
    }
}
