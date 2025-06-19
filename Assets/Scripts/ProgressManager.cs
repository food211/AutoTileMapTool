using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;

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
    [SerializeField] private Vector2 saveIconScreenPosition = new Vector2(50f, 50f);

    [Tooltip("保存图标大小")]
    [SerializeField] private Vector2 saveIconSize = new Vector2(16f, 16f);

    [Tooltip("保存图标显示时间")]
    [SerializeField] private float saveIconDuration = 1.5f;

    [Tooltip("像素完美渲染")]
    [SerializeField] private bool pixelPerfect = true;

    [Header("调试设置")]
    [SerializeField] private bool debugMode = false;

    [Header("加密设置")]
    [Tooltip("是否加密存档")]
    [SerializeField] private bool encryptSaveData = true;
    
    [Tooltip("加密强度 (1-10)")]
    [Range(1, 10)]
    [SerializeField] private int encryptionStrength = 5;

    // 当前玩家数据
    private PlayerData currentPlayerData = new PlayerData();

    // 保存图标实例
    private GameObject saveIconInstance;
    private CanvasGroup saveIconCanvasGroup;
    private Coroutine fadeCoroutine;

    // 当前场景中的可保存对象缓存
    private Dictionary<string, ISaveable> currentSceneSaveables = new Dictionary<string, ISaveable>();

    private void Start()
    {
        // 实现单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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
                    SaveGame();
                    lastAutoSaveTime = Time.time;
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

        // 创建UI Canvas和保存图标
        CreateUICanvas();
        CreateSaveIcon();

        // 立即加载玩家数据
        LoadPlayerData();

        // 查找当前场景中的所有可保存对象
        StartCoroutine(FindSaveablesDelayed());

        // 通知其他对象ProgressManager已初始化完成
        GameEvents.TriggerOnProgressManagerInitialized();
        #if UNITY_EDITOR
        if (debugMode)
            Debug.LogFormat("ProgressManager 初始化完成");
        #endif
    }

    private void OnDestroy()
    {
        // 取消订阅事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameEvents.OnCheckpointActivated -= HandleCheckpointActivated;

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

        // 清除上一个场景的可保存对象缓存
        currentSceneSaveables.Clear();
        
        // 重新加载玩家数据，确保数据是最新的
        LoadPlayerData();
        
        // 延迟查找可保存对象，确保所有对象都已初始化
        StartCoroutine(FindSaveablesDelayed());
        
        if (debugMode)
            Debug.Log($"ProgressManager: 场景 {sceneName} 已加载");
    }

    /// <summary>
    /// 延迟查找可保存对象
    /// </summary>
    private IEnumerator FindSaveablesDelayed()
    {
        // 等待一帧，确保所有对象都已初始化
        yield return null;

        // 查找当前场景中的所有可保存对象
        FindAllSaveables();

        // 加载所有可保存对象的状态
        LoadAllSaveables();

        // 通知其他对象场景已完全加载
        GameEvents.TriggerOnSceneFullyLoaded(SceneManager.GetActiveScene().name);
        // 添加游戏进度加载完成的调试信息
        Debug.LogFormat($"游戏进度加载完成 - 场景: {SceneManager.GetActiveScene().name}");
    }

    /// <summary>
    /// 查找当前场景中的所有可保存对象
    /// </summary>
    private void FindAllSaveables()
    {
        // 查找实现了ISaveable接口的所有MonoBehaviour
        MonoBehaviour[] allMonoBehaviours = FindObjectsOfType<MonoBehaviour>();

        foreach (MonoBehaviour mb in allMonoBehaviours)
        {
            if (mb is ISaveable saveable)
            {
                string uniqueID = saveable.GetUniqueID();

                if (!string.IsNullOrEmpty(uniqueID) && !currentSceneSaveables.ContainsKey(uniqueID))
                {
                    currentSceneSaveables.Add(uniqueID, saveable);
                }
                else if (string.IsNullOrEmpty(uniqueID))
                {
                    Debug.LogWarning($"对象 {mb.name} 实现了ISaveable接口但返回了空的UniqueID");
                }
            }
        }

        if (debugMode)
            Debug.Log($"找到 {currentSceneSaveables.Count} 个可保存对象");
    }

    /// <summary>
    /// 加载所有可保存对象的状态
    /// </summary>
    private void LoadAllSaveables()
    {
        foreach (KeyValuePair<string, ISaveable> pair in currentSceneSaveables)
        {
            string uniqueID = pair.Key;
            ISaveable saveable = pair.Value;
            
            if (currentPlayerData.HasObjectData(uniqueID))
            {
                SaveData data = currentPlayerData.GetObjectData(uniqueID);
                saveable.Load(data);
                
                if (debugMode)
                    Debug.Log($"已加载对象 {uniqueID} 的状态");
            }
        }
    }

    /// <summary>
    /// 保存所有可保存对象的状态
    /// </summary>
    private void SaveAllSaveables()
    {
        foreach (KeyValuePair<string, ISaveable> pair in currentSceneSaveables)
        {
            string uniqueID = pair.Key;
            ISaveable saveable = pair.Value;
            
            SaveData data = saveable.Save();
            if (data != null)
            {
                currentPlayerData.SaveObject(uniqueID, saveable.GetType().Name, data);
                
                if (debugMode)
                    Debug.Log($"已保存对象 {uniqueID} 的状态");
            }
        }
    }

    /// <summary>
    /// 处理存档点激活事件
    /// </summary>
    private void HandleCheckpointActivated(Transform checkpointTransform)
    {
        // 保存游戏
        SaveGame();
        
        // 显示保存图标
        if (showSaveIcon)
        {
            ShowSaveIcon();
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
        var activeScene = SceneManager.GetActiveScene();

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
                scaler.referenceResolution = new Vector2(768, 420);
                scaler.referencePixelsPerUnit = 16;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            canvasObj.AddComponent<GraphicRaycaster>();

            DontDestroyOnLoad(canvasObj);
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

        // 保存所有可保存对象的状态
        SaveAllSaveables();

        // 序列化为JSON
        string jsonData = JsonUtility.ToJson(currentPlayerData, true);

        // 如果启用了加密，对数据进行加密
        if (encryptSaveData)
        {
            string encryptedData = ObfuscateData(jsonData);
            PlayerPrefs.SetString("PlayerData_JSON", encryptedData);
        }
        else
        {
            PlayerPrefs.SetString("PlayerData_JSON", jsonData);
        }

        PlayerPrefs.Save();
#if UNITY_EDITOR
        if (debugMode)
            Debug.LogFormat("已保存完整玩家数据" + (encryptSaveData ? " (已混淆)" : ""));
            #endif
    }

    /// <summary>
    /// 加载完整的玩家数据
    /// </summary>
    public PlayerData LoadPlayerData()
    {
        if (PlayerPrefs.HasKey("PlayerData_JSON"))
        {
            string data = PlayerPrefs.GetString("PlayerData_JSON");

            // 尝试解密数据
            if (encryptSaveData)
            {
                try
                {
                    data = DeobfuscateData(data);
#if UNITY_EDITOR
                    if (debugMode)
                        Debug.Log("存档数据解密成功");
                        #endif
                }
                catch (Exception ex)
                {
                    Debug.LogError($"解析存档数据失败: {ex.Message}");
                    // 如果解析失败，返回新的数据实例
                    currentPlayerData = new PlayerData();
                    return currentPlayerData;
                }
            }

            try
            {
                currentPlayerData = JsonUtility.FromJson<PlayerData>(data);
#if UNITY_EDITOR
                if (debugMode)
                {
                    Debug.LogFormat($"玩家数据加载成功 - 当前场景: {currentPlayerData.currentScene}");
                    Debug.LogFormat($"玩家数据 - 生命值: {currentPlayerData.currentHealth}/{currentPlayerData.maxHealth}, 游戏时间: {currentPlayerData.playTime}秒");
                    Debug.LogFormat("已加载 {0} 个收集的金币", currentPlayerData.collectedCoins.Count);
                }
                #endif

                return currentPlayerData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"解析存档数据失败: {ex.Message}");
                // 如果解析失败，返回新的数据实例
                currentPlayerData = new PlayerData();
            }

            return currentPlayerData;
        }
#if UNITY_EDITOR
        if (debugMode)
            Debug.Log("未找到玩家数据，返回新的数据实例");
            #endif

        // 如果没有保存的数据，返回新实例
        currentPlayerData = new PlayerData();
        return currentPlayerData;
    }

    /// <summary>
    /// 显示保存图标
    /// </summary>
    private void ShowSaveIcon()
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
    /// 手动保存游戏
    /// </summary>
    public void SaveGame()
    {
        // 检查玩家是否在地面上
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null && !player.isPlayerGrounded())
        {
#if UNITY_EDITOR
            if (debugMode)
                Debug.LogWarning("无法保存游戏：玩家不在地面上");
            return;
#endif
        }

        // 保存完整的玩家数据
        SavePlayerData();

        // 显示保存图标
        if (showSaveIcon)
        {
            ShowSaveIcon();
        }
#if UNITY_EDITOR
        if (debugMode)
            Debug.Log("已手动保存游戏");
            #endif
    }

    /// <summary>
    /// 保存单个对象的状态
    /// </summary>
    public void SaveObject(ISaveable saveable)
    {
        if (saveable == null) return;
        
        string uniqueID = saveable.GetUniqueID();
        if (string.IsNullOrEmpty(uniqueID)) return;
        
        SaveData data = saveable.Save();
        if (data != null)
        {
            currentPlayerData.SaveObject(uniqueID, saveable.GetType().Name, data);

            // 保存到PlayerPrefs
            string jsonData = JsonUtility.ToJson(currentPlayerData, true);

            // 如果启用了加密，对数据进行加密
            if (encryptSaveData)
            {
                string encryptedData = ObfuscateData(jsonData);
                PlayerPrefs.SetString("PlayerData_JSON", encryptedData);
            }
            else
            {
                PlayerPrefs.SetString("PlayerData_JSON", jsonData);
            }

            PlayerPrefs.Save();
            #if UNITY_EDITOR
            if (debugMode)
                Debug.Log($"已保存对象 {uniqueID} 的状态");
            #endif
        }
    }

    #region 数据加密和解密
    /// <summary>
    /// 混淆数据 - 简单的数据混淆方法
    /// </summary>
    private string ObfuscateData(string data)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        try
        {
            // 1. 转换为字节数组
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            
            // 2. 应用XOR操作和字节偏移
            for (int i = 0; i < bytes.Length; i++)
            {
                // 使用位置和加密强度进行XOR操作
                byte xorValue = (byte)((i % 256) ^ encryptionStrength);
                bytes[i] = (byte)(bytes[i] ^ xorValue);
                
                // 应用简单的字节偏移
                bytes[i] = (byte)((bytes[i] + encryptionStrength) % 256);
            }
            
            // 3. 反转数据块
            if (encryptionStrength > 3)
            {
                int blockSize = Math.Max(1, bytes.Length / 10);
                for (int i = 0; i < bytes.Length; i += blockSize * 2)
                {
                    int end = Math.Min(i + blockSize, bytes.Length);
                    if (end > i)
                    {
                        Array.Reverse(bytes, i, end - i);
                    }
                }
            }
            
            // 4. 添加简单的校验和
            byte checksum = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                checksum = (byte)((checksum + bytes[i]) % 256);
            }
            
            byte[] result = new byte[bytes.Length + 1];
            Array.Copy(bytes, result, bytes.Length);
            result[bytes.Length] = checksum;
            
            // 5. 转换为Base64字符串
            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            Debug.LogError($"混淆数据时出错: {ex.Message}");
            // 如果混淆失败，返回原始数据
            return data;
        }
    }

    /// <summary>
    /// 解混淆数据 - 简单的数据解混淆方法
    /// </summary>
    private string DeobfuscateData(string obfuscatedData)
    {
        if (string.IsNullOrEmpty(obfuscatedData))
            return obfuscatedData;

        try
        {
            // 1. 从Base64转换回字节数组
            byte[] bytes = Convert.FromBase64String(obfuscatedData);
            
            // 检查长度
            if (bytes.Length <= 1)
            {
                throw new Exception("数据长度无效");
            }
            
            // 2. 验证校验和
            byte storedChecksum = bytes[bytes.Length - 1];
            byte calculatedChecksum = 0;
            for (int i = 0; i < bytes.Length - 1; i++)
            {
                calculatedChecksum = (byte)((calculatedChecksum + bytes[i]) % 256);
            }
            
            if (storedChecksum != calculatedChecksum)
            {
                throw new Exception("数据校验失败，可能已被篡改");
            }
            
            // 创建不包含校验和的新数组
            byte[] dataBytes = new byte[bytes.Length - 1];
            Array.Copy(bytes, dataBytes, bytes.Length - 1);
            
            // 3. 反转之前反转的数据块
            if (encryptionStrength > 3)
            {
                int blockSize = Math.Max(1, dataBytes.Length / 10);
                for (int i = 0; i < dataBytes.Length; i += blockSize * 2)
                {
                    int end = Math.Min(i + blockSize, dataBytes.Length);
                    if (end > i)
                    {
                        Array.Reverse(dataBytes, i, end - i);
                    }
                }
            }
            
            // 4. 还原XOR操作和字节偏移
            for (int i = 0; i < dataBytes.Length; i++)
            {
                // 还原字节偏移
                dataBytes[i] = (byte)((dataBytes[i] + 256 - encryptionStrength) % 256);
                
                // 还原XOR操作
                byte xorValue = (byte)((i % 256) ^ encryptionStrength);
                dataBytes[i] = (byte)(dataBytes[i] ^ xorValue);
            }
            
            // 5. 转换回字符串
            return Encoding.UTF8.GetString(dataBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError($"解混淆数据时出错: {ex.Message}");
            throw; // 重新抛出异常，让调用者知道解混淆失败
        }
    }
    #endregion 数据加密和解密

    /// <summary>
    /// 预加载特定场景的保存数据，用于在加载界面中提前处理
    /// </summary>
    public void PreloadSaveData(string sceneName)
    {
#if UNITY_EDITOR
        if (debugMode)
            Debug.Log($"预加载场景 {sceneName} 的保存数据");
#endif

        // 确保玩家数据已加载
        LoadPlayerData();

        // 设置当前场景名称
        currentPlayerData.currentScene = sceneName;

        // 预处理对象数据
        // 这里我们只是加载数据，不进行实际应用
        // 实际应用将在场景加载后由LevelManager完成
#if UNITY_EDITOR
        if (debugMode)
            Debug.Log($"场景 {sceneName} 的保存数据预加载完成");
            #endif
    }

    /// <summary>
    /// 从玩家数据中移除特定对象的状态
    /// </summary>
    /// <param name="objectID">对象唯一ID</param>
    public void RemoveObjectState(string objectID)
    {
        if (string.IsNullOrEmpty(objectID) || currentPlayerData == null) return;

        // 从玩家数据中移除此对象的状态
        currentPlayerData.RemoveObjectData(objectID);
#if UNITY_EDITOR
        if (debugMode)
            Debug.Log($"已从玩家数据中移除对象 {objectID} 的状态");
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

        // 重置当前玩家数据
        currentPlayerData = new PlayerData();
#if UNITY_EDITOR
        if (debugMode)
            Debug.Log("已重置所有游戏进度");
            #endif
    }

    /// <summary>
    /// 检查是否有保存的游戏进度
    /// </summary>
    public bool HasSavedGame()
    {
        return PlayerPrefs.HasKey("PlayerData_JSON");
    }

#if UNITY_EDITOR
    [ContextMenu("测试显示保存图标")]
#endif
    private void TestShowSaveIcon()
    {
        ShowSaveIcon();
    }
}