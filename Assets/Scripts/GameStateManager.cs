using UnityEngine;

/// <summary>
/// 游戏状态管理器 - 协调玩家与车辆的交互状态
/// 确保系统的整体状态一致性
/// </summary>
public class GameStateManager : MonoBehaviour
{
    private static GameStateManager instance;

    [Tooltip("玩家物体")]
    [SerializeField] private GameObject player;

    [Tooltip("当前被控制的车辆（如果有）")]
    private InteractVehicle currentControlledVehicle;

    private PlayerCondition playerCondition;

    public static GameStateManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameStateManager>();
                if (instance == null)
                {
                    GameObject managerGO = new GameObject("GameStateManager");
                    instance = managerGO.AddComponent<GameStateManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
        }

        if (player != null)
        {
            playerCondition = player.GetComponent<PlayerCondition>();
            if (playerCondition == null)
            {
                playerCondition = player.AddComponent<PlayerCondition>();
            }
        }
    }

    /// <summary>
    /// 玩家开始控制车辆
    /// </summary>
    public void PlayerStartControlVehicle(InteractVehicle vehicle)
    {
        if (currentControlledVehicle != null && currentControlledVehicle != vehicle)
        {
            PlayerStopControlVehicle();
        }

        currentControlledVehicle = vehicle;
        if (playerCondition != null)
        {
            playerCondition.SetControlling(true);
        }
    }

    /// <summary>
    /// 玩家停止控制车辆
    /// </summary>
    public void PlayerStopControlVehicle()
    {
        if (currentControlledVehicle != null)
        {
            currentControlledVehicle.OnPlayerExit();
            currentControlledVehicle = null;
        }

        if (playerCondition != null)
        {
            playerCondition.SetControlling(false);
        }
    }

    /// <summary>
    /// 获取玩家是否正在控制车辆
    /// </summary>
    public bool IsPlayerControllingVehicle()
    {
        return currentControlledVehicle != null && playerCondition != null && playerCondition.GetIsControlling();
    }

    /// <summary>
    /// 获取当前被控制的车辆
    /// </summary>
    public InteractVehicle GetCurrentControlledVehicle()
    {
        return currentControlledVehicle;
    }

    /// <summary>
    /// 获取玩家状态
    /// </summary>
    public PlayerCondition GetPlayerCondition()
    {
        return playerCondition;
    }
}
