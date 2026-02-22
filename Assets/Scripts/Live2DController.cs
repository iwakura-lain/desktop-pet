using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MotionFade;
using UnityEngine;

/// <summary>
/// Loads the Natori Live2D model from its pre-built prefab in Resources/Live2D/Natori/
/// and maps PetController states (Idle / Clicked / Drag) to Live2D motions.
///
/// Motion mapping:
///   Idle    → mtn_00  (looping idle)
///   Clicked → mtn_01  (tap reaction, one-shot)
///   Drag    → mtn_02  (alternate tap, looping while dragged)
/// </summary>
public class Live2DController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const string PrefabResourcePath  = "Live2D/Natori/Natori";
    private const string MotionResourceBase  = "Live2D/Natori/motions/";

    // Motion file names (without extension) per state
    private const string MotionIdle    = "mtn_00";
    private const string MotionClicked = "mtn_01";
    private const string MotionDrag    = "mtn_02";

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private GameObject             _modelRoot;
    private CubismMotionController _motionCtrl;
    private string                 _currentState;

    private AnimationClip _clipIdle;
    private AnimationClip _clipClicked;
    private AnimationClip _clipDrag;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        LoadModel();
    }

    // -------------------------------------------------------------------------
    // Public API — mirrors AnimationController.PlayState()
    // -------------------------------------------------------------------------

    public void PlayState(string state)
    {
        if (_motionCtrl == null || state == _currentState) return;
        _currentState = state;

        switch (state)
        {
            case "Idle":
                PlayClip(_clipIdle, isLoop: true);
                break;
            case "Clicked":
                PlayClip(_clipClicked, isLoop: false);
                break;
            case "Drag":
                PlayClip(_clipDrag, isLoop: true);
                break;
            default:
                Debug.LogWarning($"[Live2DController] Unknown state: {state}");
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void LoadModel()
    {
        // --- 1. Load pre-built prefab from Resources ---
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[Live2DController] Prefab not found at Resources/{PrefabResourcePath}");
            return;
        }

        // --- 2. Instantiate and parent under this GameObject ---
        _modelRoot = Instantiate(prefab, transform);
        _modelRoot.transform.localPosition = Vector3.zero;
        _modelRoot.transform.localScale    = Vector3.one;

        Debug.Log("[Live2DController] Natori prefab instantiated.");

        // --- 4. Add motion playback components if not already present ---
        if (_modelRoot.GetComponent<CubismFadeController>() == null)
            _modelRoot.AddComponent<CubismFadeController>();

        _motionCtrl = _modelRoot.GetComponent<CubismMotionController>();
        if (_motionCtrl == null)
            _motionCtrl = _modelRoot.AddComponent<CubismMotionController>();

        // --- 5. Pre-load AnimationClips ---
        _clipIdle    = LoadMotionClip(MotionIdle,    loop: true);
        _clipClicked = LoadMotionClip(MotionClicked, loop: false);
        _clipDrag    = LoadMotionClip(MotionDrag,    loop: true);

        // --- 6. Start idle ---
        PlayState("Idle");

        Debug.Log("[Live2DController] Natori model ready.");
    }

    private AnimationClip LoadMotionClip(string motionName, bool loop)
    {
        // Files are named mtn_00.motion3.json — Unity stores them as TextAsset
        // with the path "motions/mtn_00.motion3" (strips only the last ".json").
        var asset = Resources.Load<TextAsset>(MotionResourceBase + motionName + ".motion3");
        if (asset == null)
        {
            Debug.LogWarning($"[Live2DController] Motion not found: {MotionResourceBase}{motionName}.motion3");
            return null;
        }

        var motion3Json = Live2D.Cubism.Framework.Json.CubismMotion3Json.LoadFrom(asset);
        if (motion3Json == null)
        {
            Debug.LogWarning($"[Live2DController] Failed to parse motion: {motionName}");
            return null;
        }

        var clip = motion3Json.ToAnimationClip();
        if (clip == null) return null;

        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        clip.legacy   = false;
        return clip;
    }

    private void PlayClip(AnimationClip clip, bool isLoop)
    {
        if (clip == null) return;
        _motionCtrl.PlayAnimation(clip,
            layerIndex: 0,
            priority:   CubismMotionPriority.PriorityNormal,
            isLoop:     isLoop);
    }
}
