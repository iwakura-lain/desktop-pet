using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads sprite-sheet PNGs from Resources/Sprites/ at runtime and populates
/// AnimationController clips. This avoids any Editor-Inspector binding so the
/// build works out-of-the-box without manual asset setup.
///
/// Expected files in Assets/Resources/Sprites/:
///   catgirl_idle.png    — 512x128, 4 frames (128x128 each)
///   catgirl_clicked.png — 512x128, 4 frames
///   catgirl_drag.png    — 512x128, 4 frames
/// </summary>
[RequireComponent(typeof(AnimationController))]
public class RuntimeSpriteLoader : MonoBehaviour
{
    private const int FrameWidth  = 128;
    private const int FrameHeight = 128;
    private const int FrameCount  = 4;

    private void Awake()
    {
        var anim = GetComponent<AnimationController>();

        var clips = new List<AnimationController.AnimClip>
        {
            LoadClip("Sprites/catgirl_idle",    "Idle",    fps: 6f,  loop: true),
            LoadClip("Sprites/catgirl_clicked", "Clicked", fps: 8f,  loop: true),
            LoadClip("Sprites/catgirl_drag",    "Drag",    fps: 8f,  loop: true),
        };

        anim.SetClipsAtRuntime(clips, "Idle");
    }

    private static AnimationController.AnimClip LoadClip(
        string resourcePath, string clipName, float fps, bool loop)
    {
        // Load the full sprite sheet texture
        Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
        if (sheet == null)
        {
            Debug.LogError($"[RuntimeSpriteLoader] Could not load: Resources/{resourcePath}");
            return new AnimationController.AnimClip
            {
                name   = clipName,
                frames = new Sprite[0],
                fps    = fps,
                loop   = loop
            };
        }

        // Slice into individual frames left-to-right
        var frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            Rect rect = new Rect(i * FrameWidth, 0, FrameWidth, FrameHeight);
            frames[i] = Sprite.Create(
                sheet, rect,
                pivot: new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f);
        }

        return new AnimationController.AnimClip
        {
            name   = clipName,
            frames = frames,
            fps    = fps,
            loop   = loop
        };
    }
}
