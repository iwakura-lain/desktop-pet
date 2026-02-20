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

        // SpriteRenderer
        var sr = pet.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 0;

        // BoxCollider2D (for click detection)
        var col = pet.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.28f, 1.28f); // matches 128px at 100 PPU

        // AnimationController
        var anim = pet.AddComponent<AnimationController>();

        // RuntimeSpriteLoader — loads clips via EmbeddedSprites
        pet.AddComponent<RuntimeSpriteLoader>();

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
