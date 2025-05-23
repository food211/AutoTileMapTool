using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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
    // Canvas实例，用于显示UI元素
    private Canvas uiCanvas;
    [SerializeField] private Canvas mainUICanvas; // 在Inspector手动指定


    [Header("自动保存设置")]
    [Tooltip("是否启用自动保存")]
    [SerializeField] private bool enableAutoSave = true;

    [Tooltip("自动保存间隔（秒）")]
    [SerializeField] private float autoSaveInterval = 300f; // 5分钟

    private float lastAutoSaveTime = 0f;

    [Header("保存图标设置")]
    [Tooltip("保存图标预制体")]
    [SerializeField] private GameObject saveIconPrefab;

    [Tooltip("保存图标在屏幕上的位置")]
    [SerializeField] private Vector2 saveIconScreenPosition = new Vector2(50f, 50f); // 默认右下角，距离边缘50像素

    [Tooltip("保存图标大小")]
    [SerializeField] private Vector2 saveIconSize = new Vector2(16f, 16f);

    [Tooltip("保存图标显示时间")]
    [SerializeField] private float saveIconDuration = 1.5f;

    [Tooltip("像素完美渲染")]
    [SerializeField] private bool pixelPerfect = true;

    // 存档键名
    private const string LAST_SCENE_KEY = "LastScene";
    private const string LAST_STARTPOINT_KEY = "LastStartPoint";
    private const string LAST_CHECKPOINT_KEY = "LastCheckpointID";
    private const string ENDPOINT_PREFIX = "Endpoint_";
    private const string CHECKPOINT_PREFIX = "Checkpoint_";
    private const string ITEM_PREFIX = "Item_";
    private const string COIN_PREFIX = "Coin_";

    // 当前玩家数据
    private PlayerData currentPlayerData = new PlayerData();

    // 是否正在保存
    private bool isSaving = false;
    public bool debugCoinMode = false;

    // 保存图标实例（持久保留）
    private GameObject saveIconInstance;
    private CanvasGroup saveIconCanvasGroup;
    private Coroutine fadeCoroutine;

    private void Start()
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

    private void Update()
    {
        // 自动保存逻辑
        if (enableAutoSave && Time.time - lastAutoSaveTime > autoSaveInterval)
        {
            // 检查是否在游戏场景中（非菜单）
            if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().name) &&
                SceneManager.GetActiveScene().name != "StartMenu" &&
                SceneManager.GetActiveScene().name != "LoadingUI")
            {
                // 检查玩家是否在地面上
                PlayerController player = FindObjectOfType<PlayerController>();
                if (player != null && player.isPlayerGrounded())
                {
                    // 执行自动保存
                    SavePlayerData();
                    lastAutoSaveTime = Time.time;

                    // 显示保存图标
                    if (showSaveIcon)
                    {
                        ShowSaveIcon();
                    }

#if UNITY_EDITOR
                    Debug.LogFormat("已执行自动保存");
#endif
                }
            }
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

        // 创建UI Canvas和保存图标
        CreateUICanvas();
        CreateSaveIcon();
        // 立即加载玩家数据
        LoadPlayerData();
        // 添加一个事件，通知其他对象ProgressManager已初始化完成

        GameEvents.TriggerOnProgressManagerInitialized();


#if UNITY_EDITOR
        Debug.LogFormat("ProgressManager 初始化完成");
#endif
    }

    private void OnDestroy()
    {
        // 取消订阅事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameEvents.OnCheckpointActivated -= HandleCheckpointActivated;
        GameEvents.OnEndpointReached -= HandleEndpointReached;

        // 停止所有协程
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    /// <summary>
    /// 场景加载完成回调
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 场景加载完成后的处理
        string sceneName = scene.name;

        // 确保UI Canvas和保存图标在新场景中有效
        if (uiCanvas == null)
        {
            CreateUICanvas();
        }

        if (saveIconInstance == null)
        {
            CreateSaveIcon();
        }
        // 重新加载玩家数据，确保数据是最新的
        LoadPlayerData();
        // 通知其他对象场景已加载完成
        GameEvents.TriggerOnSceneFullyLoaded(sceneName);

#if UNITY_EDITOR
        Debug.LogFormat($"ProgressManager: 场景 {sceneName} 已加载");
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
                ShowSaveIcon();
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
            Debug.LogFormat($"ProgressManager: 终点 {endpoint.name} 已到达");
#endif
        }
    }

    /// <summary>
    /// 创建UI Canvas
    /// </summary>
    private void CreateUICanvas()
{
    if (mainUICanvas != null)
    {
        uiCanvas = mainUICanvas;
        return;
    }

    // 获取当前激活场景
    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    // 查找当前场景下的主 Canvas
    Canvas[] canvases = FindObjectsOfType<Canvas>();
    foreach (Canvas canvas in canvases)
    {
        if (canvas.gameObject.scene == activeScene &&
            canvas.name == "Canvas" &&
            canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
            canvas.gameObject.activeInHierarchy)
        {
            uiCanvas = canvas;
            break;
        }
    }

    // 如果没有找到合适的 Canvas，创建一个新的
    if (uiCanvas == null)
    {
        GameObject canvasObj = new GameObject("SaveIconCanvas");
        uiCanvas = canvasObj.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        if (pixelPerfect)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320, 180);
            scaler.referencePixelsPerUnit = 32;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        // 注意：不要 DontDestroyOnLoad，这样它只属于当前场景
    }
}

    /// <summary>
    /// 创建保存图标（只创建一次，然后隐藏）
    /// </summary>
    private void CreateSaveIcon()
    {
        // 确保有UI Canvas
        if (uiCanvas == null)
        {
            CreateUICanvas();
        }
#if UNITY_EDITOR
Debug.LogFormat("创建保存图标，prefab={0}", saveIconPrefab);
#endif

        // 如果已经有保存图标，不需要重新创建
        if (saveIconInstance != null)
        {
            return;
        }

        // 如果有预制体，实例化
        if (saveIconPrefab != null)
        {
            // 在Canvas下实例化保存图标
            saveIconInstance = Instantiate(saveIconPrefab, uiCanvas.transform);

            // 设置为RectTransform
            RectTransform rectTransform = saveIconInstance.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // 设置位置 - 右下角
                rectTransform.anchorMin = new Vector2(1, 0);
                rectTransform.anchorMax = new Vector2(1, 0);
                rectTransform.pivot = new Vector2(1, 0);
                rectTransform.anchoredPosition = new Vector2(-saveIconScreenPosition.x, saveIconScreenPosition.y);

                // 设置大小
                rectTransform.sizeDelta = saveIconSize;
            }

            // 添加或获取CanvasGroup组件
            saveIconCanvasGroup = saveIconInstance.GetComponent<CanvasGroup>();
            if (saveIconCanvasGroup == null)
            {
                saveIconCanvasGroup = saveIconInstance.AddComponent<CanvasGroup>();
            }

            // 初始状态为隐藏
            saveIconCanvasGroup.alpha = 0f;
            saveIconInstance.SetActive(false);
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
        Debug.LogFormat($"已保存终点使用记录: {endpointID} -> {targetScene}:{targetStartPoint}");
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

        // 查找对应的检查点组件以获取重生点坐标
        Checkpoint checkpoint = FindCheckpointById(checkpointID);
        if (checkpoint != null && checkpoint.RespawnPoint != null)
        {
            // 保存重生点坐标，而不是玩家当前位置
            Vector3 respawnPosition = checkpoint.RespawnPoint.position;
            PlayerPrefs.SetFloat("Player_PosX", respawnPosition.x);
            PlayerPrefs.SetFloat("Player_PosY", respawnPosition.y);
            PlayerPrefs.SetFloat("Player_PosZ", respawnPosition.z);

#if UNITY_EDITOR
            Debug.LogFormat($"已保存重生点位置: ({respawnPosition.x}, {respawnPosition.y}, {respawnPosition.z})");
#endif
        }

        // 保存玩家状态信息（如果有PlayerHealthManager）
        SavePlayerHealthState();

        // 保存更改
        PlayerPrefs.Save();

        // 保存完成
        isSaving = false;

#if UNITY_EDITOR
        Debug.LogFormat($"已保存存档点激活记录: {checkpointID} 在场景 {sceneName}");
#endif
    }

    /// <summary>
    /// 根据ID查找检查点组件
    /// </summary>
    private Checkpoint FindCheckpointById(string checkpointID)
    {
        Checkpoint[] checkpoints = FindObjectsOfType<Checkpoint>();
        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (checkpoint.CheckpointID == checkpointID)
            {
                return checkpoint;
            }
        }
        return null;
    }

    /// <summary>
    /// 保存玩家健康状态
    /// </summary>
    private void SavePlayerHealthState()
    {
        // 查找玩家健康管理器
        PlayerHealthManager healthManager = FindObjectOfType<PlayerHealthManager>();
        if (healthManager != null)
        {
            // 保存玩家生命值
            PlayerPrefs.SetFloat("Player_Health", healthManager.currentHealth);
            PlayerPrefs.SetFloat("Player_MaxHealth", healthManager.maxHealth);

#if UNITY_EDITOR
            Debug.LogFormat($"已保存玩家状态: 生命值 {healthManager.currentHealth}/{healthManager.maxHealth}");
#endif
        }
    }

    /// <summary>
    /// 保存完整的玩家数据
    /// </summary>
    public void SavePlayerData()
    {
        // 获取玩家和健康管理器引用
        PlayerController player = FindObjectOfType<PlayerController>();
        PlayerHealthManager healthManager = FindObjectOfType<PlayerHealthManager>();

        // 更新玩家数据
        currentPlayerData.PopulateFromPlayer(player, healthManager);

        // 更新游戏时间
        currentPlayerData.playTime += Time.deltaTime;

        // 序列化为JSON
        string jsonData = JsonUtility.ToJson(currentPlayerData, true);

        // 保存到PlayerPrefs
        PlayerPrefs.SetString("PlayerData_JSON", jsonData);
        PlayerPrefs.Save();

#if UNITY_EDITOR
        Debug.LogFormat("已保存完整玩家数据");
#endif
    }

    /// <summary>
    /// 加载完整的玩家数据
    /// </summary>
    public PlayerData LoadPlayerData()
    {
        if (PlayerPrefs.HasKey("PlayerData_JSON"))
        {
            string jsonData = PlayerPrefs.GetString("PlayerData_JSON");
            currentPlayerData = JsonUtility.FromJson<PlayerData>(jsonData);
            return currentPlayerData;
        }

#if UNITY_EDITOR
        Debug.LogFormat("未找到玩家数据，返回新的数据实例");
#endif

        // 如果没有保存的数据，返回新实例
        currentPlayerData = new PlayerData();
        return currentPlayerData;
    }

    /// <summary>
    /// 应用已加载的玩家数据到当前玩家
    /// </summary>
    public void ApplyPlayerData()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        PlayerHealthManager healthManager = FindObjectOfType<PlayerHealthManager>();

        // 确保已加载数据
        if (currentPlayerData == null)
        {
            LoadPlayerData();
        }

        // 应用健康值
        if (healthManager != null)
        {
            healthManager.maxHealth = currentPlayerData.maxHealth;
            healthManager.currentHealth = currentPlayerData.currentHealth;
        }

        // 应用位置（如果在同一场景）
        if (player != null && currentPlayerData.currentScene == SceneManager.GetActiveScene().name)
        {
            player.transform.position = new Vector3(
                currentPlayerData.positionX,
                currentPlayerData.positionY,
                currentPlayerData.positionZ
            );
        }

        // 可以添加更多数据应用逻辑

#if UNITY_EDITOR
        Debug.LogFormat("已应用玩家数据");
#endif
    }

    /// <summary>
    /// 显示保存图标
    /// </summary>
    private void ShowSaveIcon(Vector3 worldPosition = default)
    {
        // 确保保存图标存在
        if (saveIconInstance == null)
        {
            CreateSaveIcon();
        }

        // 如果不显示保存图标，直接返回
        if (!showSaveIcon || saveIconInstance == null)
        {
            return;
        }

        // 停止之前的淡出协程
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // 确保图标激活
        saveIconInstance.SetActive(true);

        // 重置透明度
        if (saveIconCanvasGroup != null)
        {
            saveIconCanvasGroup.alpha = 1f;
        }
        else
        {
            // 如果没有CanvasGroup，尝试重置所有Image组件的透明度
            Image[] images = saveIconInstance.GetComponentsInChildren<Image>();
            foreach (Image img in images)
            {
                Color color = img.color;
                color.a = 1f;
                img.color = color;
            }
        }

        // 启动淡出协程
        fadeCoroutine = StartCoroutine(FadeOutSaveIcon(saveIconDuration));
    }

    /// <summary>
    /// 淡出保存图标
    /// </summary>
    private IEnumerator FadeOutSaveIcon(float duration)
    {
        // 等待一小段时间，让图标完全显示
        yield return new WaitForSeconds(0.5f);

        // 计算淡出的时间
        float fadeTime = duration - 0.5f;
        float startTime = Time.time;

        // 如果有CanvasGroup，使用它来淡出
        if (saveIconCanvasGroup != null)
        {
            saveIconCanvasGroup.alpha = 1f;
            while (Time.time < startTime + fadeTime)
            {
                float t = (Time.time - startTime) / fadeTime;
                saveIconCanvasGroup.alpha = 1f - t;
                yield return null;
            }
            saveIconCanvasGroup.alpha = 0f;
        }
        // 否则，使用Image组件淡出
        else
        {
            Image[] images = saveIconInstance.GetComponentsInChildren<Image>();
            if (images.Length > 0)
            {
                // 保存原始颜色
                Color[] originalColors = new Color[images.Length];
                for (int i = 0; i < images.Length; i++)
                {
                    originalColors[i] = images[i].color;
                }

                // 淡出
                while (Time.time < startTime + fadeTime)
                {
                    float t = (Time.time - startTime) / fadeTime;
                    for (int i = 0; i < images.Length; i++)
                    {
                        Color color = originalColors[i];
                        color.a = 1f - t;
                        images[i].color = color;
                    }
                    yield return null;
                }

                // 设置完全透明
                for (int i = 0; i < images.Length; i++)
                {
                    Color color = originalColors[i];
                    color.a = 0f;
                    images[i].color = color;
                }
            }
        }

        // 隐藏图标而不是销毁它
        saveIconInstance.SetActive(false);
        fadeCoroutine = null;
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
    /// 记录物品获取
    /// </summary>
    public void SaveItemCollected(string itemID)
    {
        PlayerPrefs.SetInt(ITEM_PREFIX + itemID, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 获取金币存储键名
    /// </summary>
    public string GetCoinKey(string coinID)
    {
        return COIN_PREFIX + coinID;
    }

    /// <summary>
    /// 保存金币收集状态
    /// </summary>
    public void SaveCoinCollected(string coinID)
    {
        // 确保已加载数据
        if (currentPlayerData == null)
        {
            LoadPlayerData();
        }

        // 添加到金币收集列表
        currentPlayerData.AddCoin(coinID);
        
        // 保存更新后的数据
        SavePlayerData();

        #if UNITY_EDITOR
        if(debugCoinMode)
            Debug.LogFormat("已保存金币收集状态: {0}", coinID);
        #endif
    }

    /// <summary>
    /// 检查金币是否已被收集
    /// </summary>
    public bool IsCoinCollected(string coinID)
    {
        // 确保已加载数据
        if (currentPlayerData == null)
        {
            LoadPlayerData();
        }
        
        return currentPlayerData.collectedCoins.Contains(coinID);
    }

    /// <summary>
    /// 手动保存游戏
    /// </summary>
    public void SaveGame()
    {
        // 检查玩家是否在地面上
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null && !player.isPlayerGrounded())
        {
#if UNITY_EDITOR
            Debug.LogWarning("无法保存游戏：玩家不在地面上");
#endif
            return;
        }

        // 保存当前场景
        string currentScene = SceneManager.GetActiveScene().name;
        PlayerPrefs.SetString(LAST_SCENE_KEY, currentScene);

        // 保存完整的玩家数据
        SavePlayerData();

        // 显示保存图标
        if (showSaveIcon && player != null)
        {
            ShowSaveIcon();
        }

#if UNITY_EDITOR
        Debug.LogFormat("已手动保存游戏");
#endif
    }

    /// <summary>
    /// 收集物品并保存到玩家数据
    /// </summary>
    public void CollectItem(string itemID, string itemType)
    {
        // 确保已加载数据
        if (currentPlayerData == null)
        {
            LoadPlayerData();
        }

        // 添加到收集列表
        if (!currentPlayerData.collectedItems.Contains(itemID))
        {
            currentPlayerData.collectedItems.Add(itemID);

            // 同时保存到PlayerPrefs以保持兼容性
            PlayerPrefs.SetInt(ITEM_PREFIX + itemID, 1);

            // 保存更新后的数据
            SavePlayerData();

#if UNITY_EDITOR
            Debug.LogFormat($"已收集物品: {itemID} (类型: {itemType})");
#endif
        }
    }

    /// <summary>
    /// 检查物品是否已获取
    /// </summary>
    public bool IsItemCollected(string itemID)
    {
        // 确保已加载数据
        if (currentPlayerData == null)
        {
            LoadPlayerData();
        }
        return currentPlayerData.collectedItems.Contains(itemID);
    }

    /// <summary>
    /// 解锁能力并保存到玩家数据
    /// </summary>
    public void UnlockAbility(string abilityID)
    {
        // 确保已加载数据
        if (currentPlayerData == null)
        {
            LoadPlayerData();
        }

        // 添加到已解锁能力列表
        if (!currentPlayerData.unlockedAbilities.Contains(abilityID))
        {
            currentPlayerData.unlockedAbilities.Add(abilityID);

            // 保存更新后的数据
            SavePlayerData();

#if UNITY_EDITOR
            Debug.LogFormat($"已解锁能力: {abilityID}");
#endif
        }
    }

    /// <summary>
    /// 检查能力是否已解锁
    /// </summary>
    public bool IsAbilityUnlocked(string abilityID)
    {
        // 确保已加载数据
        if (currentPlayerData == null)
        {
            LoadPlayerData();
        }

        return currentPlayerData.unlockedAbilities.Contains(abilityID);
    }

    /// <summary>
    /// 重置所有游戏进度
    /// </summary>
    public void ResetAllProgress()
    {
        // 清除所有PlayerPrefs数据
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // 重置当前玩家数据
        currentPlayerData = new PlayerData();

#if UNITY_EDITOR
        Debug.LogFormat("已重置所有游戏进度");
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

    #if UNITY_EDITOR
    [ContextMenu("测试显示保存图标")]
    #endif
    private void TestShowSaveIcon()
    {
        ShowSaveIcon();
    }
    
}