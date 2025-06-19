using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// 加载管理器 - 负责场景异步加载和加载进度通知
/// 支持叠加式加载界面、玩家操作禁用和场景淡入淡出效果
/// </summary>
public class LoadManager : MonoBehaviour
{
    // 单例实例
    private static LoadManager _instance;
    public static LoadManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<LoadManager>();
            return _instance;
        }
    }

    // 调试与状态
    public bool IsDebugMode = true;
    private bool isLoading = false;
    private string currentLoadingScene = "";
    public string CurrentLoadingScene => currentLoadingScene;
    
    // 加载UI控制器引用
    private LoadingUIController loadingUIController;
    
    [Header("加载设置")]
    [SerializeField] private bool useLoadingScene = true;
    [SerializeField] private string loadingSceneName = "LoadingUI";
    [SerializeField] private bool useAdditiveLoading = true;
    
    [Header("时间设置")]
    [SerializeField] private float loadCompletedDelay = 0.5f;
    [SerializeField] private float minLoadingTime = 1.5f;
    [SerializeField] private float progressSmoothSpeed = 5f;
    
    [Header("淡入淡出效果")]
    [SerializeField] private bool useFadeEffect = true;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private Color fadeColor = Color.black;
    
    [Header("输入控制")]
    [SerializeField] private bool disableInputWhileLoading = true;
    
    [Header("进度加载")]
    [SerializeField] private bool loadProgressInLoadingScene = true;
    [SerializeField] private float progressLoadingTime = 0.5f;
    
    // UI组件
    private Canvas fadeCanvas;
    private CanvasGroup fadeCanvasGroup;
    private GameObject inputBlocker;
    
    // 当前加载UI场景
    private Scene currentLoadingUIScene;
    
    // 事件
    public event Action OnFadeInStarted;
    public event Action OnFadeOutStarted;
    
    // 保存目标起始点ID，用于在加载场景中传递
    private string targetStartPointID;
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (useFadeEffect) InitializeFadeCanvas();
            if (disableInputWhileLoading) InitializeInputBlocker();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 初始化淡入淡出效果的Canvas
    /// </summary>
    private void InitializeFadeCanvas()
    {
        // 创建淡入淡出Canvas
        GameObject fadeCanvasObj = new GameObject("FadeCanvas");
        fadeCanvasObj.transform.SetParent(transform);
        
        // 添加Canvas组件
        fadeCanvas = fadeCanvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;
        
        // 添加CanvasScaler组件
        var scaler = fadeCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // 添加CanvasGroup组件
        fadeCanvasGroup = fadeCanvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
        
        // 创建背景图像
        GameObject fadeImageObj = new GameObject("FadeImage");
        fadeImageObj.transform.SetParent(fadeCanvasObj.transform, false);
        
        // 添加RectTransform和Image组件
        var rectTransform = fadeImageObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        
        var image = fadeImageObj.AddComponent<UnityEngine.UI.Image>();
        image.color = fadeColor;
        
        // 初始时隐藏
        fadeCanvas.enabled = false;
    }
    
    /// <summary>
    /// 初始化输入阻止器
    /// </summary>
    private void InitializeInputBlocker()
    {
        // 创建输入阻止器
        inputBlocker = new GameObject("InputBlocker");
        inputBlocker.transform.SetParent(transform);
        
        // 添加Canvas组件
        var canvas = inputBlocker.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998;
        
        // 添加CanvasScaler组件
        var scaler = inputBlocker.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // 创建阻止器图像
        GameObject blockerImageObj = new GameObject("BlockerImage");
        blockerImageObj.transform.SetParent(inputBlocker.transform, false);
        
        // 添加RectTransform和Image组件
        var rectTransform = blockerImageObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        
        var image = blockerImageObj.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0, 0, 0, 0.01f);
        
        // 初始时隐藏
        inputBlocker.SetActive(false);
    }

    /// <summary>
    /// 加载场景
    /// </summary>
    public void LoadScene(string sceneName, bool? useLoadingUI = null, string startPointID = null)
    {
        // 如果已经在加载，则忽略
        if (isLoading)
        {
            LogWarning($"已经在加载场景 {currentLoadingScene}，忽略加载请求: {sceneName}");
            return;
        }
        
        // 保存目标起始点ID
        targetStartPointID = startPointID;
        
        bool shouldUseLoadingUI = useLoadingUI ?? useLoadingScene;
        
        // 禁用玩家输入
        if (disableInputWhileLoading && inputBlocker != null)
            inputBlocker.SetActive(true);
        
        // 开始淡出效果或直接加载
        if (useFadeEffect)
            StartCoroutine(FadeOut(() => StartLoadingProcess(sceneName, shouldUseLoadingUI)));
        else
            StartLoadingProcess(sceneName, shouldUseLoadingUI);
    }
    
    /// <summary>
    /// 开始加载流程
    /// </summary>
    private void StartLoadingProcess(string sceneName, bool shouldUseLoadingUI)
    {
        currentLoadingScene = sceneName;
        isLoading = true;
        
        // 在开始加载前执行垃圾回收
        PerformGarbageCollection();
        
        if (shouldUseLoadingUI && !string.IsNullOrEmpty(loadingSceneName))
            StartCoroutine(LoadLoadingScene(sceneName));
        else
            StartCoroutine(LoadSceneDirectly(sceneName));
    }
    
    /// <summary>
    /// 加载加载UI场景
    /// </summary>
    private IEnumerator LoadLoadingScene(string targetSceneName)
    {
        LoadSceneMode loadMode = useAdditiveLoading ? LoadSceneMode.Additive : LoadSceneMode.Single;
        Scene initialScene = SceneManager.GetActiveScene();
        
        // 加载加载UI场景
        AsyncOperation loadingSceneOperation = SceneManager.LoadSceneAsync(loadingSceneName, loadMode);
        while (!loadingSceneOperation.isDone)
            yield return null;
        
        // 处理叠加加载模式
        if (useAdditiveLoading)
        {
            currentLoadingUIScene = SceneManager.GetSceneByName(loadingSceneName);
            SceneManager.SetActiveScene(currentLoadingUIScene);
            yield return null;
            
            // 卸载初始场景
            if (initialScene.IsValid() && initialScene != currentLoadingUIScene)
            {
                Log($"卸载初始场景 {initialScene.name}");
                yield return SceneManager.UnloadSceneAsync(initialScene);
            }
        }
        
        yield return null;
        
        // 查找并缓存LoadingUIController
        loadingUIController = FindObjectOfType<LoadingUIController>();
        if (loadingUIController != null)
        {
            Log("找到 LoadingUIController");
            loadingUIController.SetLoadingScene(targetSceneName);
            
            // 设置目标起始点ID
            if (!string.IsNullOrEmpty(targetStartPointID))
            {
                loadingUIController.SetTargetStartPointID(targetStartPointID);
            }
            
            // 在加载场景中预加载游戏进度
            if (loadProgressInLoadingScene)
            {
                // 更新进度条显示
                loadingUIController.UpdateProgress(0.1f);
                yield return new WaitForSeconds(0.1f);
                
                // 预加载游戏进度
                Log("在加载界面中预加载游戏进度");
                loadingUIController.UpdateProgress(0.2f);
                
                // 获取ProgressManager实例
                ProgressManager progressManager = ProgressManager.Instance;
                if (progressManager != null)
                {
                    // 加载玩家数据
                    loadingUIController.UpdateProgress(0.3f);
                    yield return new WaitForSeconds(0.1f);
                    
                    PlayerData playerData = progressManager.LoadPlayerData();
                    loadingUIController.UpdateProgress(0.4f);
                    yield return new WaitForSeconds(progressLoadingTime * 0.5f);
                    
                    // 预处理可保存对象数据
                    loadingUIController.UpdateProgress(0.5f);
                    progressManager.PreloadSaveData(targetSceneName);
                    yield return new WaitForSeconds(progressLoadingTime * 0.5f);
                    
                    loadingUIController.UpdateProgress(0.6f);
                    Log("游戏进度预加载完成");
                }
                else
                {
                    LogWarning("未找到ProgressManager实例，无法预加载游戏进度");
                }
            }
        }
        else
        {
            LogError("无法找到 LoadingUIController! 请确保LoadingUI场景中包含此组件.");
        }
        
        // 淡入效果
        if (useFadeEffect)
            yield return StartCoroutine(FadeIn());
        
        // 开始加载目标场景
        StartCoroutine(LoadTargetScene(targetSceneName));
    }
    
    /// <summary>
    /// 在加载UI场景中加载目标场景
    /// </summary>
    private IEnumerator LoadTargetScene(string sceneName)
    {
        float startTime = Time.time;

        // 执行垃圾回收
        PerformGarbageCollection();
        
        // 开始异步加载目标场景
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, useAdditiveLoading ? LoadSceneMode.Additive : LoadSceneMode.Single);
        operation.allowSceneActivation = false;
        
        float currentDisplayProgress = loadProgressInLoadingScene ? 0.6f : 0f;
        float targetProgress = 0f;
        
        // 加载循环
        while (!operation.isDone)
        {
            // 如果启用了进度预加载，则从0.6开始计算场景加载进度
            float progressOffset = loadProgressInLoadingScene ? 0.6f : 0f;
            float progressScale = loadProgressInLoadingScene ? 0.4f : 1f;
            
            targetProgress = progressOffset + (operation.progress / 0.9f) * progressScale;
            currentDisplayProgress = Mathf.Lerp(currentDisplayProgress, targetProgress, progressSmoothSpeed * Time.deltaTime);

            // 更新UI进度
            Log($"更新进度 {sceneName}: {currentDisplayProgress:F2}");
            if (loadingUIController != null)
                loadingUIController.UpdateProgress(currentDisplayProgress);
            
            // 检查是否加载完成
            if (operation.progress >= 0.9f && currentDisplayProgress >= (loadProgressInLoadingScene ? 0.98f : 0.95f))
            {
                // 确保显示100%
                Log($"更新100%进度 {sceneName}");
                if (loadingUIController != null)
                    loadingUIController.UpdateProgress(1f);
                
                // 等待最短加载时间
                float elapsedTime = Time.time - startTime;
                if (elapsedTime < minLoadingTime)
                    yield return new WaitForSeconds(minLoadingTime - elapsedTime);
                
                yield return new WaitForSeconds(loadCompletedDelay);
                
                // 淡出效果
                if (useFadeEffect)
                    yield return StartCoroutine(FadeOut());
                
                // 通知加载UI控制器场景即将激活
                if (loadingUIController != null)
                    loadingUIController.OnSceneActivating();
                
                operation.allowSceneActivation = true;
            }
            
            yield return null;
        }
        
        yield return null;
        
        // 设置目标场景为活动场景
        if (useAdditiveLoading)
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        
        // 通知UI加载完成
        if (loadingUIController != null)
            loadingUIController.LoadingCompleted();
        
        // 卸载加载UI场景
        if (useAdditiveLoading && currentLoadingUIScene.IsValid())
        {
            Log($"卸载加载UI场景 {currentLoadingUIScene.name}");
            yield return SceneManager.UnloadSceneAsync(currentLoadingUIScene);
        }
        
        // 淡入效果
        if (useFadeEffect)
            yield return StartCoroutine(FadeIn());
        
        // 启用玩家输入
        if (disableInputWhileLoading && inputBlocker != null)
            inputBlocker.SetActive(false);
        
        // 加载完成
        isLoading = false;
        currentLoadingScene = "";
    }
    
    /// <summary>
    /// 直接加载场景（不使用加载UI）
    /// </summary>
    private IEnumerator LoadSceneDirectly(string sceneName)
    {
        float startTime = Time.time;
        currentLoadingScene = sceneName;
        isLoading = true;

        // 执行垃圾回收
        PerformGarbageCollection();
        
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;
        
        float currentDisplayProgress = 0f;
        
        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            currentDisplayProgress = Mathf.Lerp(currentDisplayProgress, progress, progressSmoothSpeed * Time.deltaTime);
            
            if (operation.progress >= 0.9f && currentDisplayProgress >= 0.95f)
            {
                float elapsedTime = Time.time - startTime;
                if (elapsedTime < minLoadingTime)
                    yield return new WaitForSeconds(minLoadingTime - elapsedTime);
                
                yield return new WaitForSeconds(loadCompletedDelay);
                operation.allowSceneActivation = true;
            }
            
            yield return null;
        }
        
        if (useFadeEffect)
            yield return StartCoroutine(FadeIn());
        
        if (disableInputWhileLoading && inputBlocker != null)
            inputBlocker.SetActive(false);
        
        isLoading = false;
        currentLoadingScene = "";
    }
    
    /// <summary>
    /// 淡出效果（从透明到不透明）
    /// </summary>
    private IEnumerator FadeOut(Action onComplete = null)
    {
        if (!useFadeEffect || fadeCanvas == null || fadeCanvasGroup == null)
        {
            onComplete?.Invoke();
            yield break;
        }
        
        OnFadeOutStarted?.Invoke();
        fadeCanvas.enabled = true;
        fadeCanvasGroup.blocksRaycasts = true;
        
        float startTime = Time.time;
        float progress = 0;
        
        while (progress < 1)
        {
            progress = (Time.time - startTime) / fadeOutDuration;
            fadeCanvasGroup.alpha = Mathf.Lerp(0, 1, progress);
            yield return null;
        }
        
        fadeCanvasGroup.alpha = 1;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// 淡入效果（从不透明到透明）
    /// </summary>
    private IEnumerator FadeIn()
    {
        if (!useFadeEffect || fadeCanvas == null || fadeCanvasGroup == null)
            yield break;
        
        OnFadeInStarted?.Invoke();
        
        float startTime = Time.time;
        float progress = 0;
        
        while (progress < 1)
        {
            progress = (Time.time - startTime) / fadeInDuration;
            fadeCanvasGroup.alpha = Mathf.Lerp(1, 0, progress);
            yield return null;
        }
        
        fadeCanvasGroup.alpha = 0;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvas.enabled = false;
    }

    private void PerformGarbageCollection()
    {
        // 强制垃圾回收
        System.GC.Collect();
        
        // 等待所有终结器执行完毕
        System.GC.WaitForPendingFinalizers();
        
        // 再次收集，确保所有对象都被清理
        System.GC.Collect();
        
        // 减少堆内存占用
        Resources.UnloadUnusedAssets();
        
        Log("执行了垃圾回收");
    }

    // 条件日志方法
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void Log(string message)
    {
        if (IsDebugMode)
            Debug.LogFormat($"LoadManager: {message}");
    }
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void LogWarning(string message)
    {
        Debug.LogWarning($"LoadManager: {message}");
    }
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void LogError(string message)
    {
        Debug.LogError($"LoadManager: {message}");
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}