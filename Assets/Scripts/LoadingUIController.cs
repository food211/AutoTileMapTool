using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class LoadingUIController : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI sceneNameText;
    [SerializeField] private Image backgroundImage;
    
    [Header("加载提示")]
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private string[] loadingTips;
    [SerializeField] private float tipChangeInterval = 5f;
    
    [Header("动画设置")]
    [SerializeField] private bool useProgressAnimation = true;
    [SerializeField] private float progressAnimationSpeed = 2f;
    
    private string targetSceneName;
    private float currentProgress = 0f;
    private float targetProgress = 0f;
    private Coroutine tipChangeCoroutine;
    
    // 目标起始点ID
    private string targetStartPointID;
    
    private void Awake()
    {
        // 初始化UI
        if (progressBar != null)
            progressBar.value = 0;
            
        if (progressText != null)
            progressText.text = "0%";
            
        if (sceneNameText != null)
            sceneNameText.text = "加载中...";
            
        if (tipText != null && loadingTips.Length > 0)
        {
            // 随机选择一个提示
            tipText.text = loadingTips[Random.Range(0, loadingTips.Length)];
            
            // 启动提示切换协程
            if (loadingTips.Length > 1)
                tipChangeCoroutine = StartCoroutine(ChangeTipsPeriodically());
        }
    }
    
    private void Update()
    {
        // 平滑进度条动画
        if (useProgressAnimation && progressBar != null)
        {
            currentProgress = Mathf.Lerp(currentProgress, targetProgress, Time.deltaTime * progressAnimationSpeed);
            progressBar.value = currentProgress;
            
            if (progressText != null)
                progressText.text = $"{Mathf.RoundToInt(currentProgress * 100)}%";
        }
    }
    
    /// <summary>
    /// 设置正在加载的场景名称
    /// </summary>
    public void SetLoadingScene(string sceneName)
    {
        targetSceneName = sceneName;
        
        if (sceneNameText != null)
            sceneNameText.text = $"正在加载 {sceneName}...";
    }
    
    /// <summary>
    /// 设置目标起始点ID
    /// </summary>
    public void SetTargetStartPointID(string startPointID)
    {
        targetStartPointID = startPointID;
        Debug.Log($"LoadingUIController: 设置目标起始点ID为 {startPointID}");
    }
    
    /// <summary>
    /// 更新加载进度
    /// </summary>
    public void UpdateProgress(float progress)
    {
        targetProgress = Mathf.Clamp01(progress);
        
        // 如果不使用动画，直接设置值
        if (!useProgressAnimation)
        {
            currentProgress = targetProgress;
            
            if (progressBar != null)
                progressBar.value = currentProgress;
                
            if (progressText != null)
                progressText.text = $"{Mathf.RoundToInt(currentProgress * 100)}%";
        }
    }
    
    /// <summary>
    /// 加载完成时调用
    /// </summary>
    public void LoadingCompleted()
    {
        // 停止提示切换协程
        if (tipChangeCoroutine != null)
        {
            StopCoroutine(tipChangeCoroutine);
            tipChangeCoroutine = null;
        }
    }
    
    /// <summary>
    /// 场景即将激活时调用
    /// </summary>
    public void OnSceneActivating()
    {
        // 确保ProgressManager实例存在
        ProgressManager progressManager = ProgressManager.Instance;
        if (progressManager != null)
        {
            // 设置LevelManager的目标起始点ID
            LevelManager levelManager = FindObjectOfType<LevelManager>();
            if (levelManager != null && !string.IsNullOrEmpty(targetStartPointID))
            {
                Debug.Log($"LoadingUIController: 传递目标起始点ID {targetStartPointID} 给LevelManager");
                levelManager.SetTargetStartPointID(targetStartPointID);
            }
        }
    }
    
    /// <summary>
    /// 定期切换加载提示
    /// </summary>
    private IEnumerator ChangeTipsPeriodically()
    {
        int currentTipIndex = Random.Range(0, loadingTips.Length);
        
        while (true)
        {
            yield return new WaitForSeconds(tipChangeInterval);
            
            // 选择一个不同的提示
            int newIndex;
            do
            {
                newIndex = Random.Range(0, loadingTips.Length);
            } while (newIndex == currentTipIndex && loadingTips.Length > 1);
            
            currentTipIndex = newIndex;
            
            if (tipText != null)
                tipText.text = loadingTips[currentTipIndex];
        }
    }
    
    private void OnDestroy()
    {
        if (tipChangeCoroutine != null)
        {
            StopCoroutine(tipChangeCoroutine);
            tipChangeCoroutine = null;
        }
    }
}