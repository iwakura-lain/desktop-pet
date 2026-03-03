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
        // UniWindowController confirms: SolidColor + Color.clear is correct for Built-in pipeline.
        // Must set in Start (after WindowManager.Start) to avoid being overwritten.
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _cam.allowHDR = false;
        _cam.allowMSAA = false;
        Debug.Log($"[AlphaBackground] Start: clearFlags={_cam.clearFlags} bg={_cam.backgroundColor} HDR={_cam.allowHDR} MSAA={_cam.allowMSAA}");
    }

    private int _frameCount = 0;
    private void OnPostRender()
    {
        _frameCount++;
        if (_frameCount == 1 || _frameCount % 300 == 0)
            Debug.Log($"[AlphaBackground] frame={_frameCount} clearFlags={_cam.clearFlags} bg={_cam.backgroundColor}");
    }
#endif
}
