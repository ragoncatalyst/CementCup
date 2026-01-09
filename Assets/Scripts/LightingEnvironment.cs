using UnityEngine;

/// <summary>
/// 环境光照控制器：支持手动调节亮度和自动昼夜循环系统
/// 昼夜周期：60秒
///   0-15秒：白昼（60%亮度）
///   15-30秒：逐渐变暗（60% -> 10%）
///   30-45秒：夜晚（10%亮度）
///   45-60秒：逐渐变亮（10% -> 60%）
/// </summary>
public class LightingEnvironment : MonoBehaviour
{
    [Header("环境光亮度")]
    [Range(0f, 1f)]
    [Tooltip("环境光亮度（0-1），直接调节即可实时生效")]
    [SerializeField] private float ambientBrightness = 0.6f;

    [Header("亮度过渡")]
    [SerializeField] private float brightnessTransitionSpeed = 2f;
    [Tooltip("编辑器中修改亮度时的平滑过渡速度")]

    private float currentBrightness = 0.6f;
    private float targetBrightness = 0.6f;

    [Header("昼夜系统")]
    [SerializeField] private bool enableDayNightCycle = false;
    [Tooltip("昼夜周期时长（秒）")]
    [SerializeField] private float cycleDuration = 60f;
    [Tooltip("白昼亮度")]
    [SerializeField] private float dayBrightness = 0.6f;
    [Tooltip("夜晚亮度")]
    [SerializeField] private float nightBrightness = 0.1f;
    [Tooltip("白昼持续时间占比（0.25 = 25%）")]
    [SerializeField] private float dayDuration = 0.25f;  // 15秒 / 60秒
    [Tooltip("夜晚持续时间占比（0.25 = 25%）")]
    [SerializeField] private float nightDuration = 0.25f; // 15秒 / 60秒

    [Header("环境光颜色")]
    [SerializeField] private Color ambientColor = Color.white;

    private float cycleTimer = 0f;

    private void Awake()
    {
        // 设置环境光模式为Flat（避免光圈扩散效果）
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        
        // 初始化环境光
        currentBrightness = ambientBrightness;
        targetBrightness = ambientBrightness;
        ApplyAmbientLight();
    }

    private void Update()
    {
        if (enableDayNightCycle)
        {
            UpdateDayNightCycle();
        }

        // 平滑过渡亮度
        if (Mathf.Abs(currentBrightness - targetBrightness) > 0.001f)
        {
            currentBrightness = Mathf.Lerp(currentBrightness, targetBrightness, Time.deltaTime * brightnessTransitionSpeed);
        }
        else
        {
            currentBrightness = targetBrightness;
        }

        // 应用环境光设置（实时生效）
        ApplyAmbientLight();
    }

    private void OnValidate()
    {
        // 在编辑器中修改参数时，更新目标值（会平滑过渡）
        targetBrightness = ambientBrightness;
    }

    /// <summary>
    /// 更新昼夜循环
    /// </summary>
    private void UpdateDayNightCycle()
    {
        cycleTimer += Time.deltaTime;
        if (cycleTimer >= cycleDuration) cycleTimer -= cycleDuration;

        float normalizedTime = cycleTimer / cycleDuration; // 0-1
        float dayStart = 0f;
        float transitionStart = dayStart + dayDuration;
        float nightStart = transitionStart + (1f - dayDuration - nightDuration) / 2f;
        float transitionEnd = nightStart + nightDuration;

        // 重新计算各阶段的时间节点
        float phaseLength = cycleDuration / 4f;  // 每个阶段15秒

        if (cycleTimer < phaseLength)
        {
            // 阶段1（0-15秒）：白昼 60%
            targetBrightness = dayBrightness;
        }
        else if (cycleTimer < phaseLength * 2f)
        {
            // 阶段2（15-30秒）：逐渐变暗 60% -> 10%
            float t = (cycleTimer - phaseLength) / phaseLength;
            targetBrightness = Mathf.Lerp(dayBrightness, nightBrightness, t);
        }
        else if (cycleTimer < phaseLength * 3f)
        {
            // 阶段3（30-45秒）：夜晚 10%
            targetBrightness = nightBrightness;
        }
        else
        {
            // 阶段4（45-60秒）：逐渐变亮 10% -> 60%
            float t = (cycleTimer - phaseLength * 3f) / phaseLength;
            targetBrightness = Mathf.Lerp(nightBrightness, dayBrightness, t);
        }
    }

    /// <summary>
    /// 应用环境光设置到场景
    /// </summary>
    private void ApplyAmbientLight()
    {
        // 使用 Flat 模式的单一颜色，避免任何渐变或扩散效果
        Color finalColor = ambientColor * currentBrightness;
        RenderSettings.ambientLight = finalColor;
        RenderSettings.ambientSkyColor = finalColor;
        RenderSettings.ambientEquatorColor = finalColor;
        RenderSettings.ambientGroundColor = finalColor;
    }

    /// <summary>
    /// 手动设置环境光亮度
    /// </summary>
    public void SetAmbientBrightness(float brightness)
    {
        ambientBrightness = Mathf.Clamp01(brightness);
        targetBrightness = ambientBrightness;
    }

    /// <summary>
    /// 获取当前环境光亮度
    /// </summary>
    public float GetAmbientBrightness()
    {
        return currentBrightness;
    }

    /// <summary>
    /// 启用/禁用昼夜循环
    /// </summary>
    public void SetDayNightCycleEnabled(bool enabled)
    {
        enableDayNightCycle = enabled;
        if (enabled)
        {
            cycleTimer = 0f;
        }
    }

    /// <summary>
    /// 重置昼夜循环计时器
    /// </summary>
    public void ResetDayNightCycle()
    {
        cycleTimer = 0f;
    }

    /// <summary>
    /// 快进/快退昼夜循环（参数：秒数）
    /// </summary>
    public void AdvanceCycleTime(float seconds)
    {
        cycleTimer += seconds;
        if (cycleTimer >= cycleDuration) cycleTimer -= cycleDuration;
        if (cycleTimer < 0f) cycleTimer += cycleDuration;
    }
}
