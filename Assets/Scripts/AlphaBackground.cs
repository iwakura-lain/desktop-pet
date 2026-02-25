using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AlphaBackground : MonoBehaviour
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        // Use Depth clear so Unity doesn't overwrite the GL.Clear we do in OnPreRender
        _cam.clearFlags = CameraClearFlags.Depth;
        Debug.Log("[AlphaBackground] Awake: set clearFlags=Depth");
    }

    private void OnPreRender()
    {
        // Manually clear color (RGBA=0,0,0,0) + depth each frame.
        // GL.Clear writes alpha=0 into the Metal framebuffer loadAction,
        // which is what preserveFramebufferAlpha=1 needs to show transparency.
        GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
    }

    private int _frameCount = 0;
    private void OnPostRender()
    {
        _frameCount++;
        if (_frameCount == 1 || _frameCount % 300 == 0)
            Debug.Log($"[AlphaBackground] frame={_frameCount} clearFlags={_cam.clearFlags}");
    }
#endif
}
