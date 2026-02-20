using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple sprite-sheet frame animator.
/// Each "state" maps to a named clip (list of sprites + FPS).
/// Attach to the same GameObject as a SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AnimationController : MonoBehaviour
{
    [System.Serializable]
    public class AnimClip
    {
        public string     name;
        public Sprite[]   frames;
        [Min(1)]
        public float      fps = 8f;
        public bool       loop = true;
    }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [SerializeField] private List<AnimClip> clips = new();
    [SerializeField] private string         defaultClip = "Idle";

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private SpriteRenderer _renderer;
    private AnimClip        _current;
    private int             _frameIndex;
    private float           _timer;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        PlayState(defaultClip);
    }

    private void Update()
    {
        if (_current == null || _current.frames.Length == 0) return;

        _timer += Time.deltaTime;
        float frameDuration = 1f / _current.fps;

        if (_timer >= frameDuration)
        {
            _timer -= frameDuration;
            AdvanceFrame();
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Inject clips programmatically (called by RuntimeSpriteLoader).</summary>
    public void SetClipsAtRuntime(List<AnimClip> newClips, string startClip)
    {
        clips = newClips;
        defaultClip = startClip;
        PlayState(startClip);
    }

    /// <summary>Switch to named clip. No-op if already playing same clip.</summary>
    public void PlayState(string clipName)
    {
        if (_current != null && _current.name == clipName) return;

        AnimClip clip = clips.Find(c => c.name == clipName);
        if (clip == null)
        {
            Debug.LogWarning($"[AnimationController] Clip '{clipName}' not found.");
            return;
        }

        _current    = clip;
        _frameIndex = 0;
        _timer      = 0f;
        ApplyFrame();
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------
    private void AdvanceFrame()
    {
        if (_current.loop)
        {
            _frameIndex = (_frameIndex + 1) % _current.frames.Length;
        }
        else
        {
            if (_frameIndex < _current.frames.Length - 1)
                _frameIndex++;
        }
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (_current.frames.Length > 0)
            _renderer.sprite = _current.frames[_frameIndex];
    }
}
