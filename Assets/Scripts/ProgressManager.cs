using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏进度管理器 - 负责保存和加载游戏进度
/// </summary>
public class ProgressManager : MonoBehaviour
{
    // 单例实例
    public static ProgressManager Instance { get; private set; }
    
    [Header("存档设置")]
    [Tooltip("是否在存档时显示保存图标")]
    [SerializeField] private bool showSaveIcon = true;
    
    [Tooltip("保存图标预制体")]
    [SerializeField] private GameObject saveIconPrefab;
    
    [Tooltip("保存图标显示时间")]
    [SerializeField] private float saveIconDuration = 1.5f;
    
    // 存档键名
    private const string LAST_SCENE_KEY = "LastScene";
    private const string LAST_STARTPOINT_KEY = "LastStartPoint";
    private const string LAST_CHECKPOINT_KEY = "LastCheckpointID";
    private const string ENDPOINT_PREFIX = "Endpoint_";
    private const string CHECKPOINT_PREFIX = "Checkpoint_";
    private const string ITEM_PREFIX = "Item_";
    private const string VARIABLE_PREFIX = "GameVar_";
    
    // 是否正在保存
    private bool isSaving = false;
    
    // 保存图标实例
    private GameObject saveIconInstance;
    
    private void Awake()
    {
        // 实现单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 初始化
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 初始化
    /// </summary>
    private void Initialize()
    {
        // 订阅场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // 订阅存档点激活事件
        GameEvents.OnCheckpointActivated += HandleCheckpointActivated;
        
        // 订阅终点到达事件
        GameEvents.OnEndpointReached += HandleEndpointReached;
        
        #if UNITY_EDITOR
        Debug.Log("ProgressManager 初始化完成");
        #endif
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameEvents.OnCheckpointActivated -= HandleCheckpointActivated;
        GameEvents.OnEndpointReached -= HandleEndpointReached;
    }
    
    /// <summary>
    /// 场景加载完成回调
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 场景加载完成后的处理
        string sceneName = scene.name;
        
        #if UNITY_EDITOR
        Debug.Log($"ProgressManager: 场景 {sceneName} 已加载");
        #endif
    }
    
    /// <summary>
    /// 处理存档点激活事件
    /// </summary>
    private void HandleCheckpointActivated(Transform checkpointTransform)
    {
        Checkpoint checkpoint = checkpointTransform.GetComponent<Checkpoint>();
        if (checkpoint != null)
        {
            // 保存存档点信息
            SaveCheckpointActivated(checkpoint.CheckpointID, SceneManager.GetActiveScene().name);
            
            // 显示保存图标
            if (showSaveIcon)
            {
                ShowSaveIcon(checkpointTransform.position);
            }
        }
    }
    
    /// <summary>
    /// 处理终点到达事件
    /// </summary>
    private void HandleEndpointReached(Transform endpointTransform)
    {
        Endpoint endpoint = endpointTransform.GetComponent<Endpoint>();
        if (endpoint != null)
        {
            // 这里我们不直接保存，因为终点信息应该由Endpoint组件调用SaveEndpointUsed方法保存
            #if UNITY_EDITOR
            Debug.Log($"ProgressManager: 终点 {endpoint.name} 已到达");
            #endif
        }
    }
    
    /// <summary>
    /// 保存使用过的终点
    /// </summary>
    public void SaveEndpointUsed(string endpointID, string targetScene, string targetStartPoint)
    {
        // 标记正在保存
        isSaving = true;
        
        // 保存最后一个场景和起始点
        PlayerPrefs.SetString(LAST_SCENE_KEY, targetScene);
        PlayerPrefs.SetString(LAST_STARTPOINT_KEY, targetStartPoint);
        
        // 记录这个终点已被使用
        PlayerPrefs.SetInt(ENDPOINT_PREFIX + endpointID, 1);
        
        // 保存更改
        PlayerPrefs.Save();
        
        // 保存完成
        isSaving = false;
        
        #if UNITY_EDITOR
        Debug.Log($"已保存终点使用记录: {endpointID} -> {targetScene}:{targetStartPoint}");
        #endif
    }
    
    /// <summary>
    /// 保存激活的存档点
    /// </summary>
    public void SaveCheckpointActivated(string checkpointID, string sceneName)
    {
        // 标记正在保存
        isSaving = true;
        
        // 保存最后激活的存档点
        PlayerPrefs.SetString(LAST_CHECKPOINT_KEY, checkpointID);
        
        // 保存场景名称
        PlayerPrefs.SetString(LAST_SCENE_KEY, sceneName);
        
        // 记录这个存档点已被激活
        PlayerPrefs.SetInt(CHECKPOINT_PREFIX + checkpointID, 1);
        
        // 保存玩家状态信息（如果有PlayerHealthManager）
        SavePlayerState();
        
        // 保存更改
        PlayerPrefs.Save();
        
        // 保存完成
        isSaving = false;
        
        #if UNITY_EDITOR
        Debug.Log($"已保存存档点激活记录: {checkpointID} 在场景 {sceneName}");
        #endif
    }
    
    /// <summary>
    /// 保存玩家状态
    /// </summary>
    private void SavePlayerState()
    {
        // 查找玩家健康管理器
        PlayerHealthManager healthManager = FindObjectOfType<PlayerHealthManager>();
        if (healthManager != null)
        {
            // 保存玩家生命值
            PlayerPrefs.SetFloat("Player_Health", healthManager.currentHealth);
            PlayerPrefs.SetFloat("Player_MaxHealth", healthManager.maxHealth);
            
            #if UNITY_EDITOR
            Debug.Log($"已保存玩家状态: 生命值 {healthManager.currentHealth}/{healthManager.maxHealth}");
            #endif
        }
        
        // 保存玩家位置（可选）
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            Vector3 position = player.transform.position;
            PlayerPrefs.SetFloat("Player_PosX", position.x);
            PlayerPrefs.SetFloat("Player_PosY", position.y);
            PlayerPrefs.SetFloat("Player_PosZ", position.z);
            
            #if UNITY_EDITOR
            Debug.Log($"已保存玩家位置: ({position.x}, {position.y}, {position.z})");
            #endif
        }
    }
    
    /// <summary>
    /// 显示保存图标
    /// </summary>
    private void ShowSaveIcon(Vector3 position)
    {
        // 如果已经有保存图标，先销毁
        if (saveIconInstance != null)
        {
            Destroy(saveIconInstance);
        }
        
        // 如果有预制体，实例化
        if (saveIconPrefab != null)
        {
            saveIconInstance = Instantiate(saveIconPrefab, position + Vector3.up * 2f, Quaternion.identity);
            
            // 延迟销毁
            Destroy(saveIconInstance, saveIconDuration);
        }
    }
    
    /// <summary>
    /// 获取最后一个场景
    /// </summary>
    public string GetLastScene()
    {
        return PlayerPrefs.GetString(LAST_SCENE_KEY, "");
    }
    
    /// <summary>
    /// 获取最后一个起始点
    /// </summary>
    public string GetLastStartPoint()
    {
        return PlayerPrefs.GetString(LAST_STARTPOINT_KEY, "");
    }
    
    /// <summary>
    /// 获取最后激活的存档点
    /// </summary>
    public string GetLastCheckpoint()
    {
        return PlayerPrefs.GetString(LAST_CHECKPOINT_KEY, "");
    }
    
    /// <summary>
    /// 检查终点是否已使用
    /// </summary>
    public bool IsEndpointUsed(string endpointID)
    {
        return PlayerPrefs.GetInt(ENDPOINT_PREFIX + endpointID, 0) == 1;
    }
    
    /// <summary>
    /// 检查存档点是否已激活
    /// </summary>
    public bool IsCheckpointActivated(string checkpointID)
    {
        return PlayerPrefs.GetInt(CHECKPOINT_PREFIX + checkpointID, 0) == 1;
    }
    
    /// <summary>
    /// 保存游戏变量（整数）
    /// </summary>
    public void SaveGameVariable(string key, int value)
    {
        PlayerPrefs.SetInt(VARIABLE_PREFIX + key, value);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 保存游戏变量（浮点数）
    /// </summary>
    public void SaveGameVariable(string key, float value)
    {
        PlayerPrefs.SetFloat(VARIABLE_PREFIX + key, value);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 保存游戏变量（字符串）
    /// </summary>
    public void SaveGameVariable(string key, string value)
    {
        PlayerPrefs.SetString(VARIABLE_PREFIX + key, value);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 保存游戏变量（布尔值）
    /// </summary>
    public void SaveGameVariable(string key, bool value)
    {
        PlayerPrefs.SetInt(VARIABLE_PREFIX + key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 获取游戏变量（整数）
    /// </summary>
    public int GetGameVariableInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(VARIABLE_PREFIX + key, defaultValue);
    }
    
    /// <summary>
    /// 获取游戏变量（浮点数）
    /// </summary>
    public float GetGameVariableFloat(string key, float defaultValue = 0f)
    {
        return PlayerPrefs.GetFloat(VARIABLE_PREFIX + key, defaultValue);
    }
    
    /// <summary>
    /// 获取游戏变量（字符串）
    /// </summary>
    public string GetGameVariableString(string key, string defaultValue = "")
    {
        return PlayerPrefs.GetString(VARIABLE_PREFIX + key, defaultValue);
    }
    
    /// <summary>
    /// 获取游戏变量（布尔值）
    /// </summary>
    public bool GetGameVariableBool(string key, bool defaultValue = false)
    {
        return PlayerPrefs.GetInt(VARIABLE_PREFIX + key, defaultValue ? 1 : 0) == 1;
    }
    
    /// <summary>
    /// 记录物品获取
    /// </summary>
    public void SaveItemCollected(string itemID)
    {
        PlayerPrefs.SetInt(ITEM_PREFIX + itemID, 1);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 检查物品是否已获取
    /// </summary>
    public bool IsItemCollected(string itemID)
    {
        return PlayerPrefs.GetInt(ITEM_PREFIX + itemID, 0) == 1;
    }
    
    /// <summary>
    /// 手动保存游戏
    /// </summary>
    public void SaveGame()
    {
        // 保存当前场景
        string currentScene = SceneManager.GetActiveScene().name;
        PlayerPrefs.SetString(LAST_SCENE_KEY, currentScene);
        
        // 保存玩家状态
        SavePlayerState();
        
        // 保存更改
        PlayerPrefs.Save();
        
        // 显示保存图标
        if (showSaveIcon)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                ShowSaveIcon(player.transform.position);
            }
        }
        
        #if UNITY_EDITOR
        Debug.Log("已手动保存游戏");
        #endif
    }
    
    /// <summary>
    /// 重置所有游戏进度
    /// </summary>
    public void ResetAllProgress()
    {
        // 清除所有PlayerPrefs数据
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        
        #if UNITY_EDITOR
        Debug.Log("已重置所有游戏进度");
        #endif
    }
    
    /// <summary>
    /// 检查是否有保存的游戏进度
    /// </summary>
    public bool HasSavedGame()
    {
        return !string.IsNullOrEmpty(GetLastScene()) || !string.IsNullOrEmpty(GetLastCheckpoint());
    }
    
    /// <summary>
    /// 是否正在保存
    /// </summary>
    public bool IsSaving => isSaving;
}