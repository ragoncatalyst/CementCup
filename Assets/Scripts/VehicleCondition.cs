using UnityEngine;

/// <summary>
/// 记录车辆状态信息
/// </summary>
public class VehicleCondition : MonoBehaviour
{
    [Tooltip("车辆是否正在被玩家控制")]
    private bool isControlling = false;

    [Tooltip("车辆头部的空物体")]
    [SerializeField] private Transform vehicleHeadEmpty;

    [Tooltip("车辆立方体")]
    [SerializeField] private Transform vehicleCube;

    public bool IsControlling
    {
        get => isControlling;
        set => isControlling = value;
    }

    public Transform VehicleHeadEmpty => vehicleHeadEmpty;
    public Transform VehicleCube => vehicleCube;

    private void Start()
    {
        isControlling = false;

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

    /// <summary>
    /// 设置车辆为被控制状态
    /// </summary>
    public void SetControlling(bool controlling)
    {
        isControlling = controlling;
    }

    /// <summary>
    /// 检查车辆是否被控制
    /// </summary>
    public bool GetIsControlling()
    {
        return isControlling;
    }

    /// <summary>
    /// 设置车辆头部空物体
    /// </summary>
    public void SetVehicleHeadEmpty(Transform head)
    {
        vehicleHeadEmpty = head;
    }

    /// <summary>
    /// 设置车辆立方体
    /// </summary>
    public void SetVehicleCube(Transform cube)
    {
        vehicleCube = cube;
    }
}
