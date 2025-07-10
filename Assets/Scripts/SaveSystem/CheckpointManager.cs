using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理场景中所有存档点的脚本
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    [Header("存档点设置")]
    [SerializeField] private Transform initialSpawnPoint; // 初始出生点
    [SerializeField] private List<Checkpoint> checkpoints = new List<Checkpoint>(); // 场景中的所有存档点
    [SerializeField] private bool autoFindCheckpoints = true; // 是否自动查找场景中的所有存档点
    
    [Header("玩家引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerHealthManager healthManager;
    [SerializeField] private ProgressManager progressManager;
    
    // 当前激活的存档点
    private Checkpoint activeCheckpoint;
    
    private void Start()
    {
        // 获取组件引用
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
            
        if (healthManager == null && playerController != null)
            healthManager = playerController.GetComponent<PlayerHealthManager>();
            
        // 如果设置为自动查找，则查找场景中的所有存档点
        if (autoFindCheckpoints)
        {
            FindAllCheckpoints();
        }
        
        // 初始化存档点状态
        InitializeCheckpoints();
        
        // 设置健康管理器的初始出生点引用
        if (healthManager != null)
        {
            healthManager.SetCheckpointManager(this);
        }
    }
    
    private void OnEnable()
    {
        // 订阅事件
        GameEvents.OnCheckpointActivated += HandleCheckpointActivated;
    }
    
    private void OnDisable()
    {
        // 取消订阅事件
        GameEvents.OnCheckpointActivated -= HandleCheckpointActivated;
    }
    
    // 事件处理适配器方法 - 将事件转发到带两个参数的ActivateCheckpoint方法
    private void HandleCheckpointActivated(Transform checkpointTransform)
    {
        // 当通过事件触发时，我们希望触发治疗效果
        ActivateCheckpoint(checkpointTransform, true);
        progressManager.SavePlayerData();
    }
    
    /// <summary>
    /// 查找场景中所有存档点
    /// </summary>
    private void FindAllCheckpoints()
    {
        // 清空列表
        checkpoints.Clear();
        
        // 查找所有存档点
        Checkpoint[] foundCheckpoints = FindObjectsOfType<Checkpoint>();
        
        // 添加到列表
        foreach (Checkpoint checkpoint in foundCheckpoints)
        {
            checkpoints.Add(checkpoint);
        }
        #if UNITY_EDITOR
        Debug.Log($"找到 {checkpoints.Count} 个存档点");
        #endif
    }
    
    /// <summary>
    /// 初始化存档点状态
    /// </summary>
    private void InitializeCheckpoints()
    {
        // 如果有存档数据，加载上次激活的存档点
        string lastCheckpointID = PlayerPrefs.GetString("LastCheckpointID", "");
        
        if (!string.IsNullOrEmpty(lastCheckpointID))
        {
            // 查找对应ID的存档点
            foreach (Checkpoint checkpoint in checkpoints)
            {
                if (checkpoint.CheckpointID == lastCheckpointID)
                {
                    // 激活该存档点，但不触发恢复生命值效果，因为这只是游戏启动
                    ActivateCheckpoint(checkpoint.transform, false);
                    return;
                }
            }
        }
        
        // 如果没有找到上次激活的存档点，使用初始出生点
        if (initialSpawnPoint != null)
        {
            // 查找是否有与初始出生点关联的存档点
            Checkpoint initialCheckpoint = initialSpawnPoint.GetComponent<Checkpoint>();
            if (initialCheckpoint != null)
            {
                // 激活该存档点，但不触发恢复生命值效果，因为这只是游戏启动
                ActivateCheckpoint(initialCheckpoint.transform, false);
            }
            else
            {
                // 如果初始出生点没有关联存档点，则不激活任何存档点
                activeCheckpoint = null;
            }
        }
    }
    
    /// <summary>
    /// 激活存档点
    /// </summary>
    /// <param name="checkpointTransform">要激活的存档点Transform</param>
    public void ActivateCheckpoint(Transform checkpointTransform, bool triggerHeal = true)
    {
        Checkpoint checkpoint = checkpointTransform.GetComponent<Checkpoint>();
        if (checkpoint == null)
        {
            Debug.LogError($"Transform {checkpointTransform.name} 没有Checkpoint组件");
            return;
        }
        
        // 如果是同一个存档点，不做任何处理
        if (activeCheckpoint == checkpoint)
            return;
            
        // 停用当前激活的存档点
        if (activeCheckpoint != null)
        {
            activeCheckpoint.Deactivate();
        }
        
        // 激活新存档点
        checkpoint.Activate();
        activeCheckpoint = checkpoint;
        
        // 保存存档点ID
        SaveCheckpointID(checkpoint.CheckpointID);
        
        // 如果存档点设置为恢复生命值，且triggerHeal为true，则恢复玩家生命值
        if (triggerHeal && checkpoint.HealOnActivate && healthManager != null)
        {
            healthManager.FullHeal();
        }
        #if UNITY_EDITOR
        Debug.Log($"激活存档点: {checkpoint.name} (ID: {checkpoint.CheckpointID})");
        #endif
    }
    
    /// <summary>
    /// 保存存档点ID
    /// </summary>
    private void SaveCheckpointID(string checkpointID)
    {
        PlayerPrefs.SetString("LastCheckpointID", checkpointID);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 获取当前激活的存档点
    /// </summary>
    public Checkpoint GetActiveCheckpoint()
    {
        return activeCheckpoint;
    }
    
    /// <summary>
    /// 获取初始出生点
    /// </summary>
    public Transform GetInitialSpawnPoint()
    {
        return initialSpawnPoint;
    }
    
    /// <summary>
    /// 获取所有存档点
    /// </summary>
    public List<Checkpoint> GetAllCheckpoints()
    {
        return checkpoints;
    }
    
    /// <summary>
    /// 设置初始出生点
    /// </summary>
    public void SetInitialSpawnPoint(Transform spawnPoint)
    {
        initialSpawnPoint = spawnPoint;
    }
    
    /// <summary>
    /// 获取适合的重生位置
    /// </summary>
    public Vector3 GetRespawnPosition()
    {
        // 优先使用激活的存档点
        if (activeCheckpoint != null && activeCheckpoint.RespawnPoint != null)
        {
            return activeCheckpoint.RespawnPoint.position;
        }
        // 其次使用初始出生点
        else if (initialSpawnPoint != null)
        {
            return initialSpawnPoint.position;
        }
        // 如果都没有，返回原点
        else
        {
            Debug.LogWarning("没有设置任何重生点，使用原点(0,0,0)");
            return Vector3.zero;
        }
    }
}