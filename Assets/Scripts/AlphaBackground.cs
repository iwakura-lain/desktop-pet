using UnityEngine;

/// <summary>
/// Forces background pixels' alpha to 0 after the scene renders on macOS,
/// so the desktop shows through the transparent areas.
///
/// Unity's Built-in pipeline overwrites the framebuffer alpha during its
/// final composite. This component runs OnPostRender to draw a fullscreen
/// quad that only writes ColorMask A = 0 into pixels where nothing was
/// rendered (alpha still 0 after scene render), preserving the pet's
/// visible pixels.
///
/// The shader uses ColorMask A + Blend to set alpha=0 everywhere, then
/// Live2D's own alpha (>0) in pet pixels is preserved because we run
/// BEFORE the engine's own final blit in the OnPostRender callback order.
///
/// Attach to the same GameObject as Camera.main.
/// </summary>
[RequireComponent(typeof(Camera))]
public class AlphaBackground : MonoBehaviour
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private Material _mat;
    private Mesh     _quad;

    private void Awake()
    {
        var shader = Resources.Load<Shader>("AlphaBackground");
        if (shader == null)
        {
            Debug.LogError("[AlphaBackground] Resources/AlphaBackground.shader not found.");
            enabled = false;
            return;
        }
        Debug.Log("[AlphaBackground] Shader loaded OK: " + shader.name);
        _mat = new Material(shader);
        _mat.hideFlags = HideFlags.HideAndDontSave;

        // Full-screen quad in clip space (z=0, w=1 for correct pass-through)
        _quad = new Mesh();
        _quad.vertices = new Vector3[]
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3(-1f,  1f, 0f),
            new Vector3( 1f,  1f, 0f),
            new Vector3( 1f, -1f, 0f),
        };
        _quad.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e9f); // prevent frustum culling
        _quad.hideFlags = HideFlags.HideAndDontSave;
        Debug.Log("[AlphaBackground] Awake complete, OnPostRender will run each frame.");
    }

    private bool _loggedOnce = false;
    private void OnPostRender()
    {
        if (_mat == null || _quad == null) return;
        if (!_loggedOnce)
        {
            Debug.Log("[AlphaBackground] OnPostRender executing, drawing alpha=0 quad.");
            _loggedOnce = true;
        }
        _mat.SetPass(0);
        Graphics.DrawMeshNow(_quad, Matrix4x4.identity);
    }

    private void OnDestroy()
    {
        if (_mat  != null) Destroy(_mat);
        if (_quad != null) Destroy(_quad);
    }
#endif
}
