using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a quad spritesheet animation by adjusting texture tiling/offset on a target renderer.
/// Supports single-row sheets with configurable per-frame durations exposed in the Inspector.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Rendering/Player Animation")]
public class PlayerAnimation : MonoBehaviour
{
    [Header("Renderer Target")]
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField, Tooltip("Renderer material slot that should display the spritesheet.")]
    private int materialIndex = 0;
    [SerializeField, Tooltip("Texture property used by the material (default _MainTex).")]
    private string textureProperty = "_MainTex";

    [Header("Spritesheet")]
    [SerializeField, Tooltip("Single-row spritesheet texture.")]
    private Texture2D spriteSheet;
    [Min(1)] [SerializeField, Tooltip("How many columns are present in the spritesheet.")]
    private int columns = 4;
    [Min(1)] [SerializeField, Tooltip("How many frames to play from the sheet.")]
    private int frameCount = 4;

    [Header("Timing")]
    [Min(0.001f)] [SerializeField, Tooltip("Default duration (seconds) for frames without an override.")]
    private float defaultFrameDuration = 0.08f;
    [SerializeField, Tooltip("Optional per-frame overrides (seconds). Leave empty to use the default for all frames.")]
    private List<float> frameDurations = new List<float>();
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool loop = true;

    private MaterialPropertyBlock propertyBlock;
    private bool isPlaying;
    private int currentFrame;
    private float frameTimer;
    private float currentFrameDuration;
    private int textureId;
    private int textureStId;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<MeshRenderer>();
        UpdatePropertyIds();
        InitializeFrameData();
        if (Application.isPlaying && playOnAwake)
        {
            Play(true);
        }
        else
        {
            ApplyFrame(currentFrame, true);
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying && playOnAwake)
        {
            Play(false);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || !isPlaying || !IsConfigValid()) return;

        frameTimer += Time.deltaTime;
        if (frameTimer >= currentFrameDuration)
        {
            frameTimer -= currentFrameDuration;
            AdvanceFrame();
        }
    }

    private void OnValidate()
    {
        if (columns < 1) columns = 1;
        if (frameCount < 1) frameCount = 1;
        if (defaultFrameDuration < 0.001f) defaultFrameDuration = 0.001f;
        if (materialIndex < 0) materialIndex = 0;
        if (string.IsNullOrWhiteSpace(textureProperty)) textureProperty = "_MainTex";

        UpdatePropertyIds();
        InitializeFrameData();
        ApplyFrame(currentFrame, true);
    }

    public void Play(bool restart)
    {
        if (restart)
        {
            currentFrame = 0;
            frameTimer = 0f;
        }

        isPlaying = true;
        currentFrameDuration = GetFrameDuration(currentFrame);
        ApplyFrame(currentFrame, true);
    }

    public void Stop()
    {
        isPlaying = false;
        frameTimer = 0f;
    }

    public void SetFrame(int frameIndex)
    {
        ApplyFrame(frameIndex, true);
    }

    private void AdvanceFrame()
    {
        if (frameCount <= 1)
        {
            frameTimer = 0f;
            return;
        }

        int next = currentFrame + 1;
        if (next >= frameCount)
        {
            if (loop)
            {
                next = 0;
            }
            else
            {
                Stop();
                return;
            }
        }

        ApplyFrame(next, true);
    }

    private void InitializeFrameData()
    {
        if (frameCount < 1) frameCount = 1;
        if (currentFrame >= frameCount) currentFrame = frameCount - 1;
        if (currentFrame < 0) currentFrame = 0;
        currentFrameDuration = GetFrameDuration(currentFrame);
    }

    private bool IsConfigValid()
    {
        return targetRenderer != null && spriteSheet != null && columns > 0 && frameCount > 0;
    }

    private float GetFrameDuration(int frameIndex)
    {
        if (frameDurations != null && frameIndex >= 0 && frameIndex < frameDurations.Count)
        {
            float custom = frameDurations[frameIndex];
            if (custom > 0f) return custom;
        }
        return defaultFrameDuration;
    }

    private void ApplyFrame(int frameIndex, bool force)
    {
        if (frameCount < 1) frameCount = 1;
        int clamped = Mathf.Clamp(frameIndex, 0, frameCount - 1);
        if (!force && clamped == currentFrame) return;

        currentFrame = clamped;
        currentFrameDuration = GetFrameDuration(currentFrame);
        frameTimer = 0f;
        UpdateMaterialUV();
    }

    private void UpdateMaterialUV()
    {
        if (!IsConfigValid()) return;

        propertyBlock ??= new MaterialPropertyBlock();
        float tileX = 1f / Mathf.Max(1, columns);
        int column = currentFrame % columns;
        Vector2 scale = new Vector2(tileX, 1f);
        Vector2 offset = new Vector2(column * tileX, 0f);

        targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
        propertyBlock.SetTexture(textureId, spriteSheet);
        propertyBlock.SetVector(textureStId, new Vector4(scale.x, scale.y, offset.x, offset.y));
        targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
    }

    private void UpdatePropertyIds()
    {
        textureId = Shader.PropertyToID(textureProperty);
        textureStId = Shader.PropertyToID(textureProperty + "_ST");
    }
}
