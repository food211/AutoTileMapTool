using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Diagnostics;

/// <summary>
/// 加载UI控制器 - 负责管理加载界面的UI元素和动画
/// </summary>
public class LoadingUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Camera loadingCamera;
    
    // 当前场景名
    private string currentSceneName = "";
    
    // 调试模式
    public bool isDebugMode = true;
    
    private void Awake()
    {
        // 检查UI引用
        if (progressBar == null)
        {
            LogError("LoadingUIController: progressBar 引用为空! 请在Inspector中设置.");
        }
        
        if (progressText == null)
        {
            LogError("LoadingUIController: progressText 引用为空! 请在Inspector中设置.");
        }
        
        // 确保加载相机深度最高
        if (loadingCamera != null)
        {
            loadingCamera.depth = 100;
            
            // 移除音频监听器（避免多个AudioListener冲突）
            AudioListener audioListener = loadingCamera.GetComponent<AudioListener>();
            if (audioListener != null)
            {
                Destroy(audioListener);
            }
        }
        else
        {
            LogWarning("LoadingUIController: loadingCamera 引用为空! 可能会导致渲染问题.");
        }
        
        // 初始化UI
        if (progressBar != null)
        {
            progressBar.value = 0;
        }
        
        if (progressText != null)
        {
            progressText.text = "0%";
        }
        
        // 显示加载界面
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }
        
        Log("LoadingUIController: 初始化完成");
    }
    
    /// <summary>
    /// 设置当前加载的场景名
    /// </summary>
    public void SetLoadingScene(string sceneName)
    {
        currentSceneName = sceneName;
        Log($"LoadingUIController: 设置当前加载场景为 {sceneName}");
        
        // 重置进度条
        if (progressBar != null)
        {
            progressBar.value = 0;
            Log("LoadingUIController: 重置进度条为 0");
        }
        
        if (progressText != null)
        {
            progressText.text = "0%";
            Log("LoadingUIController: 重置进度文本为 0%");
        }
    }
    
    /// <summary>
    /// 更新加载进度
    /// </summary>
    public void UpdateProgress(float progress)
    {
        Log($"LoadingUIController: 更新进度 {currentSceneName}: {progress:P0}");
        
        // 更新进度条
        if (progressBar != null)
        {
            Log($"LoadingUIController: 设置进度条值为 {progress:F2}");
            progressBar.value = progress;
            
            // 强制刷新UI
            Canvas.ForceUpdateCanvases();
        }
        else
        {
            LogError("LoadingUIController: progressBar 引用为空，无法更新进度条!");
        }
        
        // 更新进度文本
        if (progressText != null)
        {
            string newText = $"{Mathf.Round(progress * 100)}%";
            progressText.text = newText;
            Log($"LoadingUIController: 设置进度文本为 {newText}");
        }
        else
        {
            LogError("LoadingUIController: progressText 引用为空，无法更新进度文本!");
        }
    }
    
    /// <summary>
    /// 加载完成
    /// </summary>
    public void LoadingCompleted()
    {
        Log($"LoadingUIController: 完成加载场景 {currentSceneName}");
        
        // 确保进度条显示100%
        if (progressBar != null)
        {
            progressBar.value = 1f;
            Log("LoadingUIController: 设置进度条为 100%");
        }
        
        if (progressText != null)
        {
            progressText.text = "100%";
            Log("LoadingUIController: 设置进度文本为 100%");
        }
    }
    
    // 以下是条件日志方法，仅在编辑器模式或开发构建中执行
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void Log(string message)
    {
        if (isDebugMode)
            UnityEngine.Debug.Log(message);
    }
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void LogWarning(string message)
    {
        if (isDebugMode)
            UnityEngine.Debug.LogWarning(message);
    }
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void LogError(string message)
    {
        UnityEngine.Debug.LogError(message);
    }
}