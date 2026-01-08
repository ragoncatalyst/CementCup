using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PlayerCam : MonoBehaviour
{
    [Header("Sway")]
    [Tooltip("Horizontal sway amplitude in local units (meters)")]
    public float swayAmplitude = 0.1f;
    [Tooltip("Sway speed in cycles per second")]
    public float swaySpeed = 0.5f;
    [Tooltip("Sway frequency phase offset")]
    public float swayPhase = 0f;

    [Header("Camera Settings")]
    public float distance = 10f;
    public float crouchDistanceReduction = 2f;
    public float lerpSpeed = 5f;

    public PlayerMov playerMov;
    private Transform player; // For PlayerMov reference
    private Transform playerVisual; // The actual visual quad being rendered
    private float currentDistance;

    Camera _cam;
    Vector3 _initialLocalPos;
    float _swayTime = 0f;

    [Header("Gaussian Blur (edge)")]
    [Tooltip("Enable Gaussian blur with edge-only masking")] public bool enableGaussianBlur = true;
    [Range(1, 4)] [Tooltip("Downsample factor (1=no downsample, 2=half res)")] public int gaussianDownsample = 2;
    [Range(1, 6)] [Tooltip("Number of blur passes (separable)")] public int gaussianIterations = 2;
    [Range(0.5f, 5f)] [Tooltip("Gaussian sigma controlling spread")] public float gaussianSigma = 2f;
    [Range(0f, 1f)] [Tooltip("Blend intensity of blurred edges")] public float gaussianIntensity = 0.9f;
    [Range(0.1f, 1f)] [Tooltip("Radius from center where edge blur starts")] public float gaussianEdgeRadius = 0.7f;
    [Range(0.01f, 0.9f)] [Tooltip("Feather width of the edge mask")] public float gaussianEdgeFeather = 0.25f;

    Material _gaussMat;
    Shader _gaussShader;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _initialLocalPos = transform.localPosition;
        _swayTime = Random.value * 10f + swayPhase;

        // Gaussian shader setup
        _gaussShader = Shader.Find("Hidden/GaussianEdgeBlur");
        if (_gaussShader != null)
        {
            _gaussMat = new Material(_gaussShader);
            _gaussMat.hideFlags = HideFlags.DontSave;
        }
        else
        {
            if (enableGaussianBlur)
            {
                Debug.LogWarning("PlayerCam: GaussianEdgeBlur shader not found. Gaussian blur disabled.");
                enableGaussianBlur = false;
            }
        }

        // find the player transform if present (for PlayerMov reference)
        var playerRoot = GameObject.Find("PlayerEmpty");
        if (playerRoot != null)
        {
            player = playerRoot.transform;
            Debug.Log("PlayerCam: Found PlayerEmpty");
        }
        else
        {
            Debug.LogWarning("PlayerCam: Could not find PlayerEmpty in scene");
        }

        // find the visual quad (PlayerQuad)
        var visualQuad = GameObject.Find("PlayerQuad");
        if (visualQuad != null)
        {
            playerVisual = visualQuad.transform;
            Debug.Log("PlayerCam: Found PlayerQuad for visual tracking");
        }
        else
        {
            Debug.LogWarning("PlayerCam: Could not find PlayerQuad in scene");
        }
    }

    void Start()
    {
        currentDistance = distance;
        
        // Re-find if not already found
        if (player == null)
        {
            var go = GameObject.Find("PlayerEmpty");
            if (go != null) player = go.transform;
        }
        if (playerVisual == null)
        {
            var go = GameObject.Find("PlayerQuad");
            if (go != null) playerVisual = go.transform;
        }
        
        // Set initial camera position based on visual quad location
        transform.rotation = Quaternion.Euler(30f, -30f, 0f);
        Transform followTarget = playerVisual != null ? playerVisual : player;
        if (followTarget != null)
        {
            Vector3 offset = transform.rotation * new Vector3(0, 0, -currentDistance);
            transform.position = followTarget.position + offset;
            Debug.Log($"PlayerCam Start: Set initial position to {transform.position}");
        }
    }

    void OnDestroy()
    {
        if (_gaussMat != null)
        {
            DestroyImmediate(_gaussMat);
            _gaussMat = null;
        }
    }

    void LateUpdate()
    {
        // Ensure player references are valid
        if (player == null)
        {
            var go = GameObject.Find("PlayerEmpty");
            if (go != null) player = go.transform;
        }
        if (playerVisual == null)
        {
            var go = GameObject.Find("PlayerQuad");
            if (go != null) playerVisual = go.transform;
        }

        // Use PlayerQuad for visual tracking, PlayerEmpty for PlayerMov reference
        Transform followTarget = playerVisual != null ? playerVisual : player;
        if (followTarget == null)
        {
            Debug.LogWarning("PlayerCam: No follow target found!");
            return;
        }

        // Get PlayerMov component for crouch state (from PlayerEmpty)
        if (playerMov == null && player != null)
        {
            var pm = player.GetComponent<PlayerMov>();
            if (pm != null) playerMov = pm;
        }

        // Calculate target distance (crouch reduces distance)
        float targetDistance = distance;
        if (playerMov != null && playerMov.isCrouching)
        {
            targetDistance = distance - crouchDistanceReduction;
        }
        
        // Smoothly interpolate distance (if player crouches)
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * lerpSpeed);

        // Calculate sway offset
        _swayTime += Time.deltaTime * swaySpeed * 2f * Mathf.PI;
        float swayOffset = Mathf.Sin(_swayTime) * swayAmplitude;

        // Camera look direction (fixed)
        Vector3 cameraDir = new Vector3(0, 0, -currentDistance);
        
        // Calculate final position: follow target position + rotated offset + sway
        transform.rotation = Quaternion.Euler(30f, -30f, 0f);
        Vector3 baseOffset = transform.rotation * cameraDir;
        Vector3 swayOffset3D = transform.right * swayOffset;
        
        Vector3 newPos = followTarget.position + baseOffset + swayOffset3D;
        
        // Debug every 30 frames
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"PlayerCam LateUpdate: FollowTarget={followTarget.position}, Camera={newPos}, Distance={currentDistance}");
        }
        
        transform.position = newPos;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // Only Gaussian blur is supported
        if (enableGaussianBlur && _gaussMat != null)
        {
            int ds = Mathf.Clamp(gaussianDownsample, 1, 4);
            int w = src.width / ds;
            int h = src.height / ds;

            var rt1 = RenderTexture.GetTemporary(w, h, 0, src.format);
            var rt2 = RenderTexture.GetTemporary(w, h, 0, src.format);

            // Downsample first
            Graphics.Blit(src, rt1);

            _gaussMat.SetFloat("_Sigma", gaussianSigma);
            int iters = Mathf.Clamp(gaussianIterations, 1, 8);
            for (int i = 0; i < iters; i++)
            {
                // Horizontal pass
                _gaussMat.SetVector("_BlurDirection", new Vector2(1f, 0f));
                Graphics.Blit(rt1, rt2, _gaussMat, 0);
                // Vertical pass
                _gaussMat.SetVector("_BlurDirection", new Vector2(0f, 1f));
                Graphics.Blit(rt2, rt1, _gaussMat, 0);
            }

            // Composite blurred with original using radial edge mask
            _gaussMat.SetTexture("_BlurTex", rt1);
            _gaussMat.SetFloat("_EdgeRadius", gaussianEdgeRadius);
            _gaussMat.SetFloat("_EdgeFeather", gaussianEdgeFeather);
            _gaussMat.SetFloat("_Intensity", gaussianIntensity);
            Graphics.Blit(src, dest, _gaussMat, 1);

            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
            return;
        }

        Graphics.Blit(src, dest);
    }
}
