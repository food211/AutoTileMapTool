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
    [SerializeField] private float respawnDelay = 1.0f; // 重生延迟
    
    [Header("玩家引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerHealthManager healthManager;
    
    // 当前激活的存档点
    private Checkpoint activeCheckpoint;
    
    private void Awake()
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
    }
    
    private void OnEnable()
    {
        // 订阅事件
        GameEvents.OnPlayerRespawn += RespawnPlayer;
        GameEvents.OnCheckpointActivated += ActivateCheckpoint;
    }
    
    private void OnDisable()
    {
        // 取消订阅事件
        GameEvents.OnPlayerRespawn -= RespawnPlayer;
        GameEvents.OnCheckpointActivated -= ActivateCheckpoint;
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
        
        Debug.Log($"找到 {checkpoints.Count} 个存档点");
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
                    // 激活该存档点
                    ActivateCheckpoint(checkpoint.transform);
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
                ActivateCheckpoint(initialCheckpoint.transform);
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
    public void ActivateCheckpoint(Transform checkpointTransform)
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
        
        // 如果存档点设置为恢复生命值，则恢复玩家生命值
        if (checkpoint.HealOnActivate && healthManager != null)
        {
            healthManager.FullHeal();
        }
        
        Debug.Log($"激活存档点: {checkpoint.name} (ID: {checkpoint.CheckpointID})");
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
    /// 重生玩家
    /// </summary>
    private void RespawnPlayer()
    {
        StartCoroutine(RespawnSequence());
    }
    
    /// <summary>
    /// 重生序列
    /// </summary>
    private IEnumerator RespawnSequence()
    {
        // 等待短暂延迟
        yield return new WaitForSeconds(respawnDelay);
        
        // 确定重生位置
        Vector3 respawnPosition;
        
        if (activeCheckpoint != null)
        {
            // 使用当前激活的存档点的重生点
            respawnPosition = activeCheckpoint.RespawnPoint.position;
        }
        else if (initialSpawnPoint != null)
        {
            // 使用初始出生点
            respawnPosition = initialSpawnPoint.position;
        }
        else
        {
            Debug.LogError("没有设置重生点！");
            yield break;
        }
        
        // 重置玩家位置
        if (playerController != null)
        {
            // 确保玩家处于正常状态
            playerController.SetPlayerInput(false); // 暂时禁用输入
            
            // 重置玩家位置
            playerController.transform.position = respawnPosition;
            
            // 如果玩家有刚体组件，重置速度
            Rigidbody2D rb = playerController.GetRigidbody();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            
            // 如果绳索系统处于活跃状态，释放绳索
            GameEvents.TriggerRopeReleased();
            
            // 如果存档点设置为重生时恢复生命值，则恢复玩家生命值
            if (activeCheckpoint != null && activeCheckpoint.HealOnActivate && healthManager != null)
            {
                healthManager.FullHeal();
            }
            
            // 给玩家短暂的无敌时间
            playerController.SetInvincible(true, 2.0f);
            
            // 延迟一下再启用输入，让玩家有时间适应
            yield return new WaitForSeconds(0.5f);
            
            // 重新启用玩家输入
            playerController.SetPlayerInput(true);
        }
        
        // 触发重生完成事件
        GameEvents.TriggerPlayerRespawnCompleted();
        
        Debug.Log($"玩家在位置 {respawnPosition} 重生");
    }
    
    /// <summary>
    /// 获取当前激活的存档点
    /// </summary>
    public Checkpoint GetActiveCheckpoint()
    {
        return activeCheckpoint;
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
}