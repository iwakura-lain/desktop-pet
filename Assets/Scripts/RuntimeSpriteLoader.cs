using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads sprite-sheet PNGs from Resources/Sprites/ at runtime and populates
/// AnimationController clips. Runs in Awake so clips are ready before Start.
/// </summary>
[RequireComponent(typeof(AnimationController))]
public class RuntimeSpriteLoader : MonoBehaviour
{
    private const int FrameW  = 128;
    private const int FrameH  = 128;
    private const int FrameN  = 4;
    private const float PPU   = 100f;

    private void Awake()
    {
        var anim = GetComponent<AnimationController>();

        var clips = new List<AnimationController.AnimClip>
        {
            MakeClip("Sprites/catgirl_idle",    "Idle",    6f,  true),
            MakeClip("Sprites/catgirl_clicked", "Clicked", 8f,  true),
            MakeClip("Sprites/catgirl_drag",    "Drag",    8f,  true),
        };

        // Filter out any clips that failed to load
        clips.RemoveAll(c => c.frames == null || c.frames.Length == 0);

        if (clips.Count == 0)
        {
            Debug.LogError("[RuntimeSpriteLoader] No sprite clips loaded! " +
                           "Check that Assets/Resources/Sprites/ PNGs exist with correct .meta files.");
            return;
        }

        anim.SetClipsAtRuntime(clips, clips[0].name);
    }

    private static AnimationController.AnimClip MakeClip(
        string path, string name, float fps, bool loop)
    {
        // Load as Texture2D (meta sets isReadable=1 and textureType=8/Sprite)
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null)
        {
            Debug.LogError($"[RuntimeSpriteLoader] Failed to load: Resources/{path}");
            return new AnimationController.AnimClip { name = name, frames = new Sprite[0], fps = fps, loop = loop };
        }

        var frames = new Sprite[FrameN];
        for (int i = 0; i < FrameN; i++)
        {
            // Unity UV origin is bottom-left; sprite sheet frames go left→right
            // so frame i starts at x = i*FrameW, y = 0 (bottom of texture)
            var rect = new Rect(i * FrameW, 0, FrameW, FrameH);
            frames[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), PPU);
        }

        return new AnimationController.AnimClip { name = name, frames = frames, fps = fps, loop = loop };
    }
}
