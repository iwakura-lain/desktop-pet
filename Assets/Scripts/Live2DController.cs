using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MotionFade;
using UnityEngine;

/// <summary>
/// Loads the Mao Live2D model from its pre-built prefab in Resources/Live2D/Mao/
/// and maps PetController states (Idle / Clicked / Drag) to Live2D motions.
///
/// Motion mapping:
///   Idle    → mtn_01  (looping idle)
///   Clicked → mtn_02  (tap reaction, one-shot)
///   Drag    → mtn_03  (alternate, looping while dragged)
/// </summary>
public class Live2DController : MonoBehaviour
{
    private const string PrefabResourcePath = "Live2D/Mao/Mao";
    private const string MotionResourceBase = "Live2D/Mao/motions/";
    private const string MotionIdle         = "mtn_01";
    private const string MotionClicked      = "mtn_02";
    private const string MotionDrag         = "mtn_03";

    private GameObject             _modelRoot;
    private CubismMotionController _motionCtrl;
    private string                 _currentState;

    private AnimationClip _clipIdle;
    private AnimationClip _clipClicked;
    private AnimationClip _clipDrag;

    private void Start()
    {
        LoadModel();
    }

    public void PlayState(string state)
    {
        if (_motionCtrl == null || state == _currentState) return;
        _currentState = state;

        switch (state)
        {
            case "Idle":    PlayClip(_clipIdle,    isLoop: true);  break;
            case "Clicked": PlayClip(_clipClicked, isLoop: false); break;
            case "Drag":    PlayClip(_clipDrag,    isLoop: true);  break;
            default:
                Debug.LogWarning($"[Live2DController] Unknown state: {state}");
                break;
        }
    }

    private void LoadModel()
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[Live2DController] Prefab not found at Resources/{PrefabResourcePath}");
            return;
        }

        _modelRoot = Instantiate(prefab, transform);
        _modelRoot.transform.localPosition = Vector3.zero;
        _modelRoot.transform.localScale    = Vector3.one;

        if (_modelRoot.GetComponent<CubismFadeController>() == null)
            _modelRoot.AddComponent<CubismFadeController>();

        _motionCtrl = _modelRoot.GetComponent<CubismMotionController>();
        if (_motionCtrl == null)
            _motionCtrl = _modelRoot.AddComponent<CubismMotionController>();

        _clipIdle    = LoadMotionClip(MotionIdle,    loop: true);
        _clipClicked = LoadMotionClip(MotionClicked, loop: false);
        _clipDrag    = LoadMotionClip(MotionDrag,    loop: true);

        PlayState("Idle");

        Debug.Log("[Live2DController] Mao model loaded.");
    }

    private AnimationClip LoadMotionClip(string motionName, bool loop)
    {
        var asset = Resources.Load<TextAsset>(MotionResourceBase + motionName + ".motion3");
        if (asset == null) return null;

        var motion3Json = Live2D.Cubism.Framework.Json.CubismMotion3Json.LoadFrom(asset);
        if (motion3Json == null) return null;

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
