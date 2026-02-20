using UnityEngine;

/// <summary>
/// Populates AnimationController clips from embedded sprite data at Awake.
/// Uses EmbeddedSprites (Base64 PNG) — no Resources folder dependency.
/// </summary>
[RequireComponent(typeof(AnimationController))]
public class RuntimeSpriteLoader : MonoBehaviour
{
    private void Awake()
    {
        var anim  = GetComponent<AnimationController>();
        var clips = EmbeddedSprites.LoadAll();
        if (clips == null || clips.Count == 0)
        {
            Debug.LogError("[RuntimeSpriteLoader] EmbeddedSprites.LoadAll() returned nothing.");
            return;
        }
        anim.SetClipsAtRuntime(clips, "Idle");
    }
}
