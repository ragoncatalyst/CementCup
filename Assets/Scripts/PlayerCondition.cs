using UnityEngine;

/// <summary>
/// 记录玩家状态信息
/// </summary>
public class PlayerCondition : MonoBehaviour
{
    [Tooltip("玩家是否正在控制载具")]
    private bool isControlling = false;

    public bool IsControlling
    {
        get => isControlling;
        set => isControlling = value;
    }

    private void Start()
    {
        isControlling = false;
    }

    /// <summary>
    /// 设置玩家为控制状态
    /// </summary>
    public void SetControlling(bool controlling)
    {
        isControlling = controlling;
    }

    /// <summary>
    /// 检查玩家是否在控制载具
    /// </summary>
    public bool GetIsControlling()
    {
        return isControlling;
    }
}
