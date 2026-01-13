using UnityEngine;

/// <summary>
/// 处理玩家与车辆的交互
/// 当玩家与车辆交互时，改变玩家和车辆的控制状态
/// 不显示TMP文本框，仅播放动画
/// </summary>
public class InteractVehicle : ObjectInteractable
{
    [Tooltip("关联的车辆状态脚本")]
    [SerializeField] private VehicleCondition vehicleCondition;

    [Tooltip("车辆的主刚体")]
    [SerializeField] private Rigidbody vehicleRigidbody;

    [Tooltip("车辆头部方向的空物体")]
    [SerializeField] private Transform vehicleHeadEmpty;

    [Tooltip("车辆立方体")]
    [SerializeField] private Transform vehicleCube;

    private PlayerCondition playerCondition;

    protected override void Start()
    {
        base.Start();

        // 如果没有指定VehicleCondition，尝试获取
        if (vehicleCondition == null)
        {
            vehicleCondition = GetComponent<VehicleCondition>();
        }

        // 如果没有指定刚体，尝试获取
        if (vehicleRigidbody == null)
        {
            vehicleRigidbody = GetComponent<Rigidbody>();
        }

        // 如果没有指定，尝试在子物体中寻找
        if (vehicleHeadEmpty == null)
        {
            vehicleHeadEmpty = transform.Find("VehicleHeadEmpty");
        }

        if (vehicleCube == null)
        {
            vehicleCube = transform.Find("VehicleCube");
        }
    }

    public override void Interact(GameObject interactor)
    {
        if (!IsInteractable) return;

        // 获取玩家的 PlayerCondition
        playerCondition = interactor.GetComponent<PlayerCondition>();
        if (playerCondition == null)
        {
            Debug.LogWarning("InteractVehicle: Interactor does not have PlayerCondition component");
            return;
        }

        // 获取或创建 VehicleCondition
        if (vehicleCondition == null)
        {
            vehicleCondition = GetComponent<VehicleCondition>();
            if (vehicleCondition == null)
            {
                vehicleCondition = gameObject.AddComponent<VehicleCondition>();
            }
        }

        // 设置控制状态
        playerCondition.SetControlling(true);
        vehicleCondition.SetControlling(true);

        // 仅播放动画，不显示TMP
        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(PopAnimation());

        // 执行车辆交互逻辑
        OnVehicleInteracted(interactor);
    }

    /// <summary>
    /// 车辆交互时的自定义逻辑
    /// </summary>
    protected virtual void OnVehicleInteracted(GameObject interactor)
    {
        // 这里可以添加特定的车辆交互效果
        // 例如：改变车辆的物理属性、启动特效等

        if (vehicleRigidbody != null)
        {
            // 可以在这里修改车辆的物理参数
            // 例如改变drag等参数
        }
    }

    /// <summary>
    /// 玩家下车时调用
    /// </summary>
    public void OnPlayerExit()
    {
        if (playerCondition != null)
        {
            playerCondition.SetControlling(false);
        }

        if (vehicleCondition != null)
        {
            vehicleCondition.SetControlling(false);
        }
    }

    /// <summary>
    /// 获取车辆是否被控制
    /// </summary>
    public bool IsVehicleControlled()
    {
        return vehicleCondition != null && vehicleCondition.GetIsControlling();
    }

    /// <summary>
    /// 获取车辆头部方向
    /// </summary>
    public Transform GetVehicleHeadDirection()
    {
        return vehicleHeadEmpty;
    }

    /// <summary>
    /// 获取车辆立方体
    /// </summary>
    public Transform GetVehicleCube()
    {
        return vehicleCube;
    }
}
