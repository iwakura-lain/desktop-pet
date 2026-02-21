using System;
using System.IO;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MotionFade;
using Live2D.Cubism.Rendering;
using UnityEngine;

/// <summary>
/// Replaces AnimationController + RuntimeSpriteLoader for Live2D models.
/// Loads the Natori model from Resources/Live2D/Natori at runtime and maps
/// PetController states (Idle / Clicked / Drag) to Live2D motions.
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

    private const string ModelResourcePath  = "Live2D/Natori/Natori";
    private const string MotionResourceBase = "Live2D/Natori/motions/";

    // Motion file names (without extension) per state
    private const string MotionIdle    = "mtn_00";
    private const string MotionClicked = "mtn_01";
    private const string MotionDrag    = "mtn_02";

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private CubismModel          _model;
    private CubismMotionController _motionCtrl;
    private string               _currentState;

    // Loaded AnimationClips keyed by state name
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

    /// <summary>Plays the Live2D motion corresponding to the given state name.</summary>
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
        // --- 1. Load model3.json from Resources ---
        var model3JsonAsset = Resources.Load<TextAsset>(ModelResourcePath + ".model3");
        if (model3JsonAsset == null)
        {
            Debug.LogError($"[Live2DController] model3.json not found at Resources/{ModelResourcePath}.model3");
            return;
        }

        // --- 2. Deserialize + instantiate CubismModel via runtime loader ---
        var model3Json = CubismModel3Json.LoadAtPath(
            "Assets/Resources/" + ModelResourcePath + ".model3.json",
            RuntimeLoadAssetAtPath
        );

        if (model3Json == null)
        {
            Debug.LogError("[Live2DController] Failed to parse model3.json");
            return;
        }

        _model = model3Json.ToModel();
        if (_model == null)
        {
            Debug.LogError("[Live2DController] ToModel() returned null");
            return;
        }

        // Parent model under this GameObject
        _model.transform.SetParent(transform, false);
        _model.transform.localPosition = Vector3.zero;
        _model.transform.localScale    = Vector3.one * 0.01f; // Live2D units → Unity units

        // --- 3. Add required rendering components if missing ---
        if (_model.GetComponent<CubismRenderController>() == null)
            _model.gameObject.AddComponent<CubismRenderController>();

        // --- 4. Add motion playback components ---
        if (_model.GetComponent<CubismFadeController>() == null)
            _model.gameObject.AddComponent<CubismFadeController>();

        _motionCtrl = _model.GetComponent<CubismMotionController>();
        if (_motionCtrl == null)
            _motionCtrl = _model.gameObject.AddComponent<CubismMotionController>();

        // --- 5. Pre-load AnimationClips ---
        _clipIdle    = LoadMotionClip(MotionIdle,    loop: true);
        _clipClicked = LoadMotionClip(MotionClicked,  loop: false);
        _clipDrag    = LoadMotionClip(MotionDrag,     loop: true);

        // --- 6. Start idle ---
        PlayState("Idle");

        Debug.Log("[Live2DController] Natori model loaded successfully.");
    }

    private AnimationClip LoadMotionClip(string motionName, bool loop)
    {
        var asset = Resources.Load<TextAsset>(MotionResourceBase + motionName);
        if (asset == null)
        {
            Debug.LogWarning($"[Live2DController] Motion not found: {MotionResourceBase}{motionName}");
            return null;
        }

        var motion3Json = CubismMotion3Json.LoadFrom(asset);
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

    // -------------------------------------------------------------------------
    // Runtime asset loader for CubismModel3Json.LoadAtPath
    // -------------------------------------------------------------------------

    private static object RuntimeLoadAssetAtPath(Type type, string path)
    {
        // Convert file path to Resources-relative path
        // e.g. "Assets/Resources/Live2D/Natori/Natori.model3.json"
        //   → "Live2D/Natori/Natori.model3"
        const string resourcesPrefix = "Assets/Resources/";
        string resourcePath = path;

        if (resourcePath.StartsWith(resourcesPrefix))
            resourcePath = resourcePath.Substring(resourcesPrefix.Length);

        // Strip extension for Resources.Load
        if (resourcePath.EndsWith(".json"))
            resourcePath = resourcePath.Substring(0, resourcePath.Length - 5);
        else if (Path.HasExtension(resourcePath))
            resourcePath = Path.ChangeExtension(resourcePath, null);

        if (type == typeof(string))
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            return textAsset != null ? textAsset.text : null;
        }
        if (type == typeof(byte[]))
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            return textAsset != null ? textAsset.bytes : null;
        }
        if (type == typeof(Texture2D))
        {
            // Texture path may still have extension - strip it
            return Resources.Load<Texture2D>(resourcePath);
        }

        return Resources.Load(resourcePath);
    }
}
