using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自定义暴风雪控制器：
/// 1. 手动发射粒子，只有在摄像机可见且无遮挡的点位才会生成，避免浪费。
/// 2. 根据风力/风向实时更新速度曲线，同时加入缓慢的阵风扰动提升沉浸感。
/// 3. 粒子与场景碰撞时播放可配置的淡出粒子，由对象池复用。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
[DisallowMultipleComponent]
public class BlizzardController : MonoBehaviour
{
    [Header("Snow Field")]
    [Tooltip("体积包围盒尺寸 (X/Z 面积, Y 高度)")]
    public Vector3 areaSize = new Vector3(30f, 15f, 30f);
    [Tooltip("中心位置，可选角色或摄像机")] public Transform anchor;
    [Tooltip("单位秒目标发射数量 (脚本驱动)")] public float emissionRate = 900f;

    [Header("Visibility Culling")]
    public bool requireCameraVisibility = true;
    public bool occlusionChecks = true;
    public Camera visibilityCamera;
    [Range(16, 4096)] public int maxSpawnAttemptsPerFrame = 1024;
    [Range(4, 1024)] public int maxRaycastsPerFrame = 128;
    [Tooltip("屏幕外扩比例。0 表示刚好屏幕范围；0.15 表示四周各扩 15% 的视口。避免移动时露馅。")]
    [Range(0f, 0.5f)] public float viewportPadding = 0.15f;

    [Header("Wind & Motion")]
    public Vector3 windDirection = new Vector3(1f, 0f, 0.2f);
    public float windStrength = 5f;
    [Tooltip("阵风振幅 (m/s)")] public float gustAmplitude = 2f;
    [Tooltip("阵风速度 (Hz)")] public float gustFrequency = 0.15f;
    [Tooltip("竖直下落速度范围")]
    public Vector2 fallSpeedRange = new Vector2(-2.2f, -0.8f);
    [Tooltip("粒子尺寸范围")]
    public Vector2 sizeRange = new Vector2(0.05f, 0.14f);

    [Header("Collision Fade")] public ParticleSystem fadePrefab;
    public float fadeDuration = 1.2f;
    public int fadePoolSize = 16;

    [Header("Debug")] public bool drawSpawnBounds = false;

    ParticleSystem mainPS;
    List<ParticleSystem> fadePool = new List<ParticleSystem>();
    List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();
    float emitAccumulator;
    int raycastsThisFrame;
    Texture2D cachedDot;

    Plane[] cachedFrustum = new Plane[6];

    void Awake()
    {
        mainPS = GetComponent<ParticleSystem>();
        ConfigureMainSystem();
        EnsureParticleRendererHasValidMaterial(mainPS);
        if (fadePrefab == null) fadePrefab = CreateFadePrefab();
        BuildFadePool();

        // Auto-find PlayerEmpty as anchor if not set
        if (anchor == null)
        {
            var playerEmpty = GameObject.Find("PlayerEmpty");
            if (playerEmpty != null)
            {
                anchor = playerEmpty.transform;
                Debug.Log("BlizzardController: Set anchor to PlayerEmpty");
            }
        }
    }

    void LateUpdate()
    {
        if (anchor != null) transform.position = anchor.position;
    }

    void Update()
    {
        if (mainPS == null || emissionRate <= 0f) return;
        Camera cam = visibilityCamera != null ? visibilityCamera : Camera.main;
        if (requireCameraVisibility && cam == null) return;

        UpdateWindCurve();
        emitAccumulator += Mathf.Max(0f, emissionRate) * Time.deltaTime;
        int emitCount = Mathf.FloorToInt(emitAccumulator);
        if (emitCount <= 0) return;
        emitAccumulator -= emitCount;

        raycastsThisFrame = 0;
        int emitted = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(maxSpawnAttemptsPerFrame, emitCount);

        if (requireCameraVisibility && cam != null)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, cachedFrustum);
        }

        while (emitted < emitCount && attempts < maxAttempts)
        {
            attempts++;
            Vector3 spawn = SampleSpawnPoint();
            if (requireCameraVisibility && cam != null)
            {
                bool visible = false;
                float pad = Mathf.Max(0f, viewportPadding);
                if (pad > 0f)
                {
                    // 使用视口坐标并在四周扩展一个 padding，允许屏幕外一圈也生成
                    Vector3 vp = cam.WorldToViewportPoint(spawn);
                    if (vp.z > 0f)
                    {
                        visible = (vp.x > -pad && vp.x < 1f + pad && vp.y > -pad && vp.y < 1f + pad);
                    }
                }
                else
                {
                    var bounds = new Bounds(spawn, Vector3.one * 0.25f);
                    visible = GeometryUtility.TestPlanesAABB(cachedFrustum, bounds);
                }
                if (!visible) continue;
                if (occlusionChecks && raycastsThisFrame < maxRaycastsPerFrame)
                {
                    raycastsThisFrame++;
                    if (Physics.Linecast(cam.transform.position, spawn, out _)) continue;
                }
            }

            EmitOne(spawn);
            emitted++;
        }
    }

    void OnParticleCollision(GameObject other)
    {
        if (mainPS == null) return;
        int count = mainPS.GetCollisionEvents(other, collisionEvents);
        for (int i = 0; i < count; i++) SpawnFade(collisionEvents[i].intersection);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawSpawnBounds) return;
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.25f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, areaSize.y * 0.5f, 0f), areaSize);
    }

    void ConfigureMainSystem()
    {
        var main = mainPS.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startLifetime = Mathf.Max(6f, areaSize.y / Mathf.Abs(fallSpeedRange.x));
        main.maxParticles = Mathf.Max(4000, Mathf.CeilToInt(emissionRate * main.startLifetime.constant));

        var emission = mainPS.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

        var shape = mainPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = areaSize;
        shape.position = new Vector3(0f, areaSize.y * 0.5f, 0f);

        var collision = mainPS.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.lifetimeLoss = 1f;
        collision.dampen = 1f;
        collision.bounce = 0f;
        collision.sendCollisionMessages = true;

        var renderer = mainPS.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sortMode = ParticleSystemSortMode.OldestInFront;
    }

    void UpdateWindCurve()
    {
        var vol = mainPS.velocityOverLifetime;
        vol.enabled = true;
        float gust = Mathf.Sin(Time.time * Mathf.PI * 2f * gustFrequency) * gustAmplitude;
        Vector3 dir = windDirection.sqrMagnitude > 0.001f ? windDirection.normalized : Vector3.right;
        float strength = windStrength + gust;
        var curveX = vol.x;
        curveX.mode = ParticleSystemCurveMode.TwoConstants;
        curveX.constantMin = dir.x * strength - 0.5f;
        curveX.constantMax = dir.x * strength + 0.5f;
        vol.x = curveX;
        var curveZ = vol.z;
        curveZ.mode = ParticleSystemCurveMode.TwoConstants;
        curveZ.constantMin = dir.z * strength - 0.5f;
        curveZ.constantMax = dir.z * strength + 0.5f;
        vol.z = curveZ;
        var curveY = vol.y;
        curveY.mode = ParticleSystemCurveMode.TwoConstants;
        curveY.constantMin = fallSpeedRange.x;
        curveY.constantMax = fallSpeedRange.y;
        vol.y = curveY;
    }

    Vector3 SampleSpawnPoint()
    {
        Vector3 center = transform.position + new Vector3(0f, areaSize.y * 0.5f, 0f);
        return center + new Vector3(
            Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
            Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f),
            Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f)
        );
    }

    void EmitOne(Vector3 position)
    {
        var emitParams = new ParticleSystem.EmitParams
        {
            position = position,
            startSize = Random.Range(sizeRange.x, sizeRange.y),
            startLifetime = mainPS.main.startLifetime.constant,
            startColor = Color.white,
            applyShapeToPosition = false
        };
        mainPS.Emit(emitParams, 1);
    }

    void BuildFadePool()
    {
        fadePool.Clear();
        if (fadePrefab == null || fadePoolSize <= 0) return;
        for (int i = 0; i < fadePoolSize; i++)
        {
            var instance = Instantiate(fadePrefab, transform);
            instance.gameObject.name = $"BlizzardFade_{i}";
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            EnsureParticleRendererHasValidMaterial(instance);
            fadePool.Add(instance);
        }
        fadePrefab.gameObject.SetActive(false);
    }

    ParticleSystem CreateFadePrefab()
    {
        var go = new GameObject("BlizzardFadePrefab");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.startLifetime = fadeDuration;
        main.startSpeed = 0f;
        main.startSize = 0.12f;
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        color.color = new ParticleSystem.MinMaxGradient(grad);

        EnsureParticleRendererHasValidMaterial(ps);
        return ps;
    }

    void SpawnFade(Vector3 pos)
    {
        if (fadePool.Count == 0) return;
        var ps = fadePool[Random.Range(0, fadePool.Count)];
        ps.transform.position = pos;
        ps.Play(true);
    }

    void EnsureParticleRendererHasValidMaterial(ParticleSystem ps)
    {
        if (ps == null) return;
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend == null) return;
        var mat = rend.sharedMaterial;
        if (mat == null || mat.shader == null || !mat.shader.isSupported)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader != null)
            {
                mat = new Material(shader) { color = Color.white };
                if (mat.mainTexture == null) mat.mainTexture = GetDotTexture();
                rend.sharedMaterial = mat;
            }
        }
        else if (mat.mainTexture == null)
        {
            mat.mainTexture = GetDotTexture();
        }
    }

    Texture2D GetDotTexture()
    {
        if (cachedDot != null) return cachedDot;
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size - 1, size - 1) * 0.5f;
        float radius = size * 0.45f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(center, new Vector2(x, y));
                byte alpha = (byte)(Mathf.Clamp01(1f - d / radius) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        tex.hideFlags = HideFlags.DontSave;
        cachedDot = tex;
        return cachedDot;
    }
}
