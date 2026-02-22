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
        // Size matches Live2D model bounds (~0.6 tall, ~0.4 wide at orthoSize=1)
        var col = pet.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(0.4f, 0.6f);
        col.offset = new Vector2(0f, 0.3f);

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
