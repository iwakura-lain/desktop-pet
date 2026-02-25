using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AlphaBackground : MonoBehaviour
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void Start()
    {
        // Set in Start (not Awake) so we run after WindowManager.Start()
        // which may set clearFlags=SolidColor — we must override it last.
        _cam.clearFlags = CameraClearFlags.Nothing;
        Debug.Log("[AlphaBackground] Start: set clearFlags=Nothing");
    }

    private void OnPreRender()
    {
        // With clearFlags=Nothing Unity skips its internal Metal clear entirely.
        // GL.Clear here is the only clear — writes RGBA=(0,0,0,0) + depth reset.
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
