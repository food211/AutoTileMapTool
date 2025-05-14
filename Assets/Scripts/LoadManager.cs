using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class LoadManager : MonoBehaviour
{
    private static LoadManager _instance;
    public static LoadManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<LoadManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("UI References")]
    public GameObject loadingScreen;
    public Slider progressBar;
    public TextMeshProUGUI progressText;
    public Camera loadingCamera;
    public CanvasGroup canvasGroup; // 可用于淡入淡出效果

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (loadingScreen != null && loadingScreen.transform.parent != transform)
            {
                loadingScreen.transform.SetParent(transform);
            }

            // 确保加载场景的相机深度最高
            if (loadingCamera != null)
            {
                loadingCamera.depth = 100; // 保持与场景设置一致
                // 移除 loading camera 上的 Audio Listener
                AudioListener audioListener = loadingCamera.GetComponent<AudioListener>();
                if (audioListener != null)
                {
                    Destroy(audioListener);
                }
            }
            
            // 初始化 CanvasGroup 如果没有指定
            if (canvasGroup == null && loadingScreen != null)
            {
                canvasGroup = loadingScreen.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = loadingScreen.AddComponent<CanvasGroup>();
                }
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        if (loadingScreen == null || progressBar == null || progressText == null)
        {
            Debug.LogError("Loading UI references are missing!");
            yield break;
        }

        // 显示加载界面
        loadingScreen.SetActive(true);
        progressBar.value = 0;
        progressText.text = "0%";

        // 开始异步加载场景
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float currentDisplayProgress = 0f;
        float targetProgress = 0f;
        float smoothSpeed = 5f; // 进度条动画速度，调整为更适合你的游戏节奏

        while (!operation.isDone)
        {
            // 将实际加载进度（0-0.9）映射到（0-1）
            targetProgress = operation.progress / 0.9f;

            // 平滑更新显示进度
            currentDisplayProgress = Mathf.Lerp(currentDisplayProgress, targetProgress, smoothSpeed * Time.deltaTime);
            
            // 更新UI
            progressBar.value = currentDisplayProgress;
            progressText.text = $"{Mathf.Round(currentDisplayProgress * 100)}%";

            // 检查是否加载完成
            if (operation.progress >= 0.9f && currentDisplayProgress >= 0.95f)
            {
                // 确保显示100%
                progressBar.value = 1f;
                progressText.text = "100%";
                
                // 短暂延迟以确保玩家看到100%
                yield return new WaitForSeconds(0.5f);
                
                // 允许场景激活
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        // 可以添加淡出效果
        if (canvasGroup != null)
        {
            float fadeTime = 0.5f;
            float elapsedTime = 0f;
            
            while (elapsedTime < fadeTime)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
        }

        // 隐藏加载界面
        loadingScreen.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}