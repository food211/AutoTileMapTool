using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 关卡管理器 - 负责关卡加载和场景转换
/// </summary>
public class LevelManager : MonoBehaviour
{
    // 单例实例
    public static LevelManager Instance { get; private set; }

    [Header("关卡设置")]
    [Tooltip("初始场景名称（通常是主菜单）")]
    [SerializeField] private string initialScene = "StartMenu";
    
    [Tooltip("第一个游戏关卡")]
    [SerializeField] private string firstLevelName;
    
    [Tooltip("第一个关卡默认起始点ID")]
    [SerializeField] private string firstLevelStartPoint = "DefaultStart";
    
    [Header("玩家设置")]
    [Tooltip("玩家预制体")]
    [SerializeField] private GameObject playerPrefab;
    
    [Header("菜单设置")]
    [Tooltip("继续游戏按钮")]
    [SerializeField] private Button continueButton;
    
    [Tooltip("继续游戏按钮文本")]
    [SerializeField] private TextMeshProUGUI continueText;

    [Header("确认对话框")]
    [SerializeField] private GameObject confirmationDialog;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    
    // 当前场景名称
    private string currentSceneName;
    
    // 是否正在加载场景
    private bool isLoadingScene = false;
    
    // 目标起始点ID（场景加载时使用）
    private string targetStartPointID = "";
    
    // CheckpointManager引用
    private CheckpointManager checkpointManager;
    
    // ProgressManager引用
    private ProgressManager progressManager;
    
    private void Start()
    {
        // 实现单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 初始化管理器
            InitializeManager();
        }
        else
        {
            // 如果已经存在实例，销毁这个重复的
            Destroy(gameObject);
            return;
        }
    }
    
    /// <summary>
    /// 初始化管理器
    /// </summary>
    private void InitializeManager()
    {
        // 获取ProgressManager引用 - 依赖GameInitializer
        progressManager = FindObjectOfType<ProgressManager>();
        if (progressManager == null)
        {
            Debug.LogWarning("未找到ProgressManager，请确保GameInitializer已正确配置");
        }
        
        // 订阅场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // 订阅终点到达事件
        GameEvents.OnPlayerReachedEndpointCenter += HandlePlayerReachedEndpointCenter;
        
        // 订阅玩家重生事件
        GameEvents.OnPlayerRespawn += HandlePlayerRespawn;
        
        // 记录当前场景名称
        currentSceneName = SceneManager.GetActiveScene().name;

        #if UNITY_EDITOR
        Debug.Log($"LevelManager 初始化完成，当前场景：{currentSceneName}");
        #endif
    }
    
    private void OnDestroy()
    {
        // 只有当这个是实例时才取消订阅事件
        if (Instance == this)
        {
            // 取消订阅事件
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.OnPlayerReachedEndpointCenter -= HandlePlayerReachedEndpointCenter;
            GameEvents.OnPlayerRespawn -= HandlePlayerRespawn;
        }
    }
    
    /// <summary>
    /// 场景加载完成回调
    /// </summary>
private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentSceneName = scene.name;
        
        // 获取CheckpointManager引用（可能在新场景中）
        checkpointManager = FindObjectOfType<CheckpointManager>();
        
        // 如果是主菜单场景，初始化菜单
        if (IsMenuScene(currentSceneName))
        {
            InitializeMenu();
        }
        // 如果是游戏场景，设置玩家和出生点
        else if (currentSceneName != "LoadingUI") // 忽略加载UI场景
        {
            // 查找场景中的所有起始点
            StartPoint[] startPoints = FindObjectsOfType<StartPoint>();
            
            // 如果有目标起始点ID，使用它
            if (!string.IsNullOrEmpty(targetStartPointID))
            {
                // 查找匹配ID的起始点
                StartPoint targetStartPoint = null;
                foreach (StartPoint sp in startPoints)
                {
                    if (sp.StartPointID == targetStartPointID)
                    {
                        targetStartPoint = sp;
                        break;
                    }
                }
                
                // 如果找到目标起始点，设置初始出生点
                if (targetStartPoint != null && checkpointManager != null)
                {
                    checkpointManager.SetInitialSpawnPoint(targetStartPoint.transform);
                }
                // 清除目标起始点ID
                targetStartPointID = "";
            }
            // 否则使用默认起始点
            else if (startPoints.Length > 0 && checkpointManager != null)
            {
                // 查找默认起始点
                StartPoint defaultStartPoint = null;
                foreach (StartPoint sp in startPoints)
                {
                    if (sp.IsDefaultStartPoint)
                    {
                        defaultStartPoint = sp;
                        break;
                    }
                }
                
                // 如果找到默认起始点，使用它
                if (defaultStartPoint != null)
                {
                    checkpointManager.SetInitialSpawnPoint(defaultStartPoint.transform);
                }
                // 否则使用第一个起始点
                else if (startPoints.Length > 0)
                {
                    checkpointManager.SetInitialSpawnPoint(startPoints[0].transform);
                }
            }
            
            // 放置玩家到起始点
            PlacePlayerAtStartPoint();
        }
        
        // 更新场景状态
        isLoadingScene = false;
        
        // 触发场景加载完成事件
        GameEvents.TriggerLevelLoaded(currentSceneName);
    }
    
    /// <summary>
    /// 初始化菜单
    /// </summary>
    private void InitializeMenu()
    {
        // 确保确认对话框初始时是隐藏的
        if (confirmationDialog == null)
        {
            confirmationDialog = GameObject.Find("ConfirmationDialog");
        }
        if (confirmationDialog != null)
        {
            confirmationDialog.SetActive(false);
        }

        // 查找继续游戏按钮（如果没有在Inspector中设置）
        if (continueButton == null)
        {
            GameObject continueObj = GameObject.Find("Continue");
            if (continueObj != null)
            {
                continueButton = continueObj.GetComponent<Button>();
                
                // 查找按钮文本
                Transform textTransform = continueObj.transform.Find("Text (TMP)");
                if (textTransform != null)
                {
                    continueText = textTransform.GetComponent<TextMeshProUGUI>();
                }
            }
        }
        
        // 检查是否有保存的游戏进度
        bool hasSavedGame = progressManager != null ? progressManager.HasSavedGame() : false;
        
        // 如果有继续游戏按钮，根据是否有存档设置其状态
        if (continueButton != null)
        {
            continueButton.interactable = hasSavedGame;
            
            // 如果有文本组件，设置颜色
            if (continueText != null)
            {
                Color textColor = continueText.color;
                textColor.a = hasSavedGame ? 1f : 0.5f;
                continueText.color = textColor;
            }
        }
    }
    
    /// <summary>
    /// 检查是否为菜单场景
    /// </summary>
    private bool IsMenuScene(string sceneName)
    {
        return sceneName == initialScene;
    }
    
    /// <summary>
    /// 处理终点到达事件
    /// </summary>
    private void HandlePlayerReachedEndpointCenter(Transform endpointTransform)
    {
        Endpoint endpoint = endpointTransform.GetComponent<Endpoint>();
        
        if (endpoint != null && !string.IsNullOrEmpty(endpoint.TargetSceneName))
        {
            // 设置目标起始点ID
            targetStartPointID = endpoint.TargetStartPointID;
            
            // 加载目标场景
            LoadLevel(endpoint.TargetSceneName, endpoint.TransitionDelay);
        }
        else
        {
            Debug.LogWarning($"终点 {endpointTransform.name} 没有设置目标场景");
        }
    }

    private void HandlePlayerRespawn()
    {
        // 如果场景中有CheckpointManager，让它处理重生位置
        if (checkpointManager != null)
        {
            // CheckpointManager会处理重生位置
            #if UNITY_EDITOR
            Debug.Log("玩家重生由CheckpointManager处理");
            #endif
        }
        else
        {
            // 获取玩家
            PlayerController player = FindObjectOfType<PlayerController>();
            
            if (player != null)
            {
                // 查找默认起始点
                StartPoint[] startPoints = FindObjectsOfType<StartPoint>();
                StartPoint defaultStartPoint = null;
                
                foreach (StartPoint sp in startPoints)
                {
                    if (sp.IsDefaultStartPoint)
                    {
                        defaultStartPoint = sp;
                        break;
                    }
                }
                
                if (defaultStartPoint != null)
                {
                    // 设置玩家位置
                    player.transform.position = defaultStartPoint.transform.position;
                    #if UNITY_EDITOR
                    Debug.Log($"LevelManager：玩家重生于默认起始点 {defaultStartPoint.name}");
                    #endif
                }
                else if (startPoints.Length > 0)
                {
                    // 使用第一个起始点
                    player.transform.position = startPoints[0].transform.position;
                    #if UNITY_EDITOR
                    Debug.Log($"LevelManager：玩家重生于起始点 {startPoints[0].name}");
                    #endif
                }
            }
        }
    }
    
    /// <summary>
    /// 将玩家放置到起始点
    /// </summary>
    private void PlacePlayerAtStartPoint()
    {
        // 如果是菜单场景，不生成玩家
        if (IsMenuScene(currentSceneName))
            return;
            
        // 查找场景中的玩家
        PlayerController player = FindObjectOfType<PlayerController>();
        
        // 如果场景中没有玩家且有预制体，则实例化
        if (player == null && playerPrefab != null)
        {
            GameObject playerObj = Instantiate(playerPrefab);
            player = playerObj.GetComponent<PlayerController>();
        }
        
        // 如果有玩家，设置位置
        if (player != null)
        {
            // 如果有CheckpointManager，使用它的重生位置
            if (checkpointManager != null)
            {
                player.transform.position = checkpointManager.GetRespawnPosition();
                #if UNITY_EDITOR
                Debug.Log($"已将玩家放置到CheckpointManager指定的重生位置");
                #endif
            }
            else
            {
                // 查找起始点
                StartPoint[] startPoints = FindObjectsOfType<StartPoint>();
                
                // 如果有目标起始点ID，使用它
                if (!string.IsNullOrEmpty(targetStartPointID))
                {
                    foreach (StartPoint sp in startPoints)
                    {
                        if (sp.StartPointID == targetStartPointID)
                        {
                            player.transform.position = sp.transform.position;
                            #if UNITY_EDITOR
                            Debug.Log($"已将玩家放置到指定起始点: {sp.name}");
                            #endif
                            return;
                        }
                    }
                }
                
                // 查找默认起始点
                foreach (StartPoint sp in startPoints)
                {
                    if (sp.IsDefaultStartPoint)
                    {
                        player.transform.position = sp.transform.position;
                        #if UNITY_EDITOR
                        Debug.Log($"已将玩家放置到默认起始点: {sp.name}");
                        #endif
                        return;
                    }
                }
                
                // 如果没有找到默认起始点，使用第一个起始点
                if (startPoints.Length > 0)
                {
                    player.transform.position = startPoints[0].transform.position;
                    #if UNITY_EDITOR
                    Debug.Log($"已将玩家放置到第一个起始点: {startPoints[0].name}");
                    #endif
                }
                else
                {
                    #if UNITY_EDITOR
                    Debug.LogWarning("未找到任何起始点");
                    #endif
                }
            }
            
        // 如果有保存的玩家数据，应用它
        if (progressManager != null && player != null)
        {
            progressManager.ApplyPlayerData();
        }
        }
    }
    
    /// <summary>
    /// 加载关卡
    /// </summary>
    public void LoadLevel(string levelName, float delay = 0)
    {
        if (isLoadingScene) return;
        
        isLoadingScene = true;
        
        // 如果有延迟，等待后再加载
        if (delay > 0)
        {
            StartCoroutine(DelayedLoadLevel(levelName, delay));
        }
        else
        {
            // 使用LoadManager加载场景
            LoadManager loadManager = LoadManager.Instance;
            if (loadManager != null)
            {
                loadManager.LoadScene(levelName);
            }
            else
            {
                // 如果没有LoadManager，使用直接场景加载
                Debug.LogWarning("未找到LoadManager，使用直接场景加载");
                SceneManager.LoadScene(levelName);
            }
        }
    }
    
    /// <summary>
    /// 延迟加载关卡
    /// </summary>
    private IEnumerator DelayedLoadLevel(string levelName, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 使用LoadManager加载场景
        LoadManager loadManager = LoadManager.Instance;
        if (loadManager != null)
        {
            loadManager.LoadScene(levelName);
        }
        else
        {
            // 如果没有LoadManager，使用直接场景加载
            Debug.LogWarning("未找到LoadManager，使用直接场景加载");
            SceneManager.LoadScene(levelName);
        }
    }
    
    /// <summary>
    /// 开始新游戏
    /// </summary>
    public void StartNewGame()
    {
        // 检查是否有保存的游戏进度
        if (progressManager != null && progressManager.HasSavedGame())
        {
            // 显示确认对话框
            ShowConfirmationDialog("是否开始新游戏？这将覆盖当前保存的进度。", () => {
                // 确认后执行
                StartNewGameConfirmed();
            });
        }
        else
        {
            // 没有保存的进度，直接开始新游戏
            StartNewGameConfirmed();
        }
    }

    private void ShowConfirmationDialog(string message, System.Action onConfirm)
    {
        // 查找确认对话框
        if (confirmationDialog == null)
        {
            confirmationDialog = GameObject.Find("ConfirmationDialog");
            if (confirmationDialog == null)
            {
                Debug.LogWarning("未找到确认对话框，将直接执行操作");
                onConfirm?.Invoke();
                return;
            }
        }
        
        // 查找确认和取消按钮
        if (confirmButton == null)
        {
            Transform confirmTransform = confirmationDialog.transform.Find("ConfirmButton");
            if (confirmTransform != null)
            {
                confirmButton = confirmTransform.GetComponent<Button>();
            }
        }
        
        if (cancelButton == null)
        {
            Transform cancelTransform = confirmationDialog.transform.Find("CancelButton");
            if (cancelTransform != null)
            {
                cancelButton = cancelTransform.GetComponent<Button>();
            }
        }
        
        // 查找消息文本并设置
        TextMeshProUGUI messageText = confirmationDialog.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = message;
        }
        
        // 设置按钮事件
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => {
                // 隐藏对话框
                confirmationDialog.SetActive(false);
                // 执行确认操作
                onConfirm?.Invoke();
            });
        }
        
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => {
                // 隐藏对话框
                confirmationDialog.SetActive(false);
            });
        }
        
        // 显示对话框
        confirmationDialog.SetActive(true);
    }

    private void StartNewGameConfirmed()
    {
        // 重置所有进度
        if (progressManager != null)
        {
            progressManager.ResetAllProgress();
        }
        
        // 设置目标起始点为第一关的默认起始点
        targetStartPointID = firstLevelStartPoint;
        
        // 加载第一个关卡
        if (!string.IsNullOrEmpty(firstLevelName))
        {
            LoadLevel(firstLevelName);
        }
        else
        {
            Debug.LogError("未设置第一个关卡，无法开始新游戏");
        }
    }
    
    /// <summary>
    /// 继续游戏
    /// </summary>
    public void ContinueGame()
    {
        if (progressManager != null)
        {
            // 获取最后保存的场景
            string lastScene = progressManager.GetLastScene();
            
            // 获取最后保存的起始点
            string lastStartPoint = progressManager.GetLastStartPoint();
            
            if (!string.IsNullOrEmpty(lastScene))
            {
                // 设置目标起始点
                if (!string.IsNullOrEmpty(lastStartPoint))
                {
                    targetStartPointID = lastStartPoint;
                }
                
                // 加载最后保存的场景
                LoadLevel(lastScene);
            }
            else if (!string.IsNullOrEmpty(firstLevelName))
            {
                // 如果没有保存的场景，加载第一个关卡
                targetStartPointID = firstLevelStartPoint;
                LoadLevel(firstLevelName);
            }
            else
            {
                Debug.LogError("未找到保存的关卡，也未设置第一个关卡");
            }
        }
        else
        {
            Debug.LogError("未找到ProgressManager，无法继续游戏");
        }
    }
    
    /// <summary>
    /// 退出游戏
    /// </summary>
    public void ExitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

        public void SaveAndExitGame()
    {
        // 保存游戏状态
        if (progressManager != null && !IsMenuScene(currentSceneName))
        {
            progressManager.SaveGame();
        }
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    /// <summary>
    /// 重新加载当前关卡
    /// </summary>
    public void ReloadCurrentLevel()
    {
        LoadLevel(currentSceneName);
    }
    
    /// <summary>
    /// 加载主菜单
    /// </summary>
    public void LoadMainMenu()
    {
        LoadLevel(initialScene);
    }
}