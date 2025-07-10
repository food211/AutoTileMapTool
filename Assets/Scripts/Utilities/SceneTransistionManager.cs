using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 场景过渡管理器，处理场景之间的淡入淡出过渡
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Canvas transitionCanvas;
    [SerializeField] private Image fadeImage;
    
    [Header("过渡设置")]
    [SerializeField] private float defaultFadeDuration = 1.0f;
    [SerializeField] private Color fadeColor = Color.black;
    
    // 单例实例
    private static SceneTransitionManager instance;
    
    private void Awake()
    {
        // 单例模式设置
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 确保有Canvas和Image组件
        EnsureComponents();
        
        // 初始化淡出状态（透明）
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0);
        transitionCanvas.enabled = false;
    }
    
    /// <summary>
    /// 确保必要的组件存在
    /// </summary>
    private void EnsureComponents()
    {
        if (transitionCanvas == null)
        {
            transitionCanvas = GetComponentInChildren<Canvas>();
            if (transitionCanvas == null)
            {
                Debug.LogError("SceneTransitionManager需要一个Canvas组件");
                
                // 创建Canvas
                GameObject canvasObj = new GameObject("TransitionCanvas");
                canvasObj.transform.SetParent(transform);
                transitionCanvas = canvasObj.AddComponent<Canvas>();
                transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                transitionCanvas.sortingOrder = 999; // 确保在最上层
                
                // 添加CanvasScaler
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                // 添加GraphicRaycaster
                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }
        
        if (fadeImage == null)
        {
            fadeImage = GetComponentInChildren<Image>();
            if (fadeImage == null)
            {
                Debug.LogError("SceneTransitionManager需要一个Image组件");
                
                // 创建Image
                GameObject imageObj = new GameObject("FadeImage");
                imageObj.transform.SetParent(transitionCanvas.transform);
                fadeImage = imageObj.AddComponent<Image>();
                fadeImage.color = fadeColor;
                
                // 设置Image为全屏
                RectTransform rect = fadeImage.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
            }
        }
    }
    
    /// <summary>
    /// 淡入淡出并加载新场景
    /// </summary>
    public void FadeAndLoadScene(string sceneName, float duration = -1)
    {
        if (duration < 0) duration = defaultFadeDuration;
        StartCoroutine(FadeAndLoadRoutine(sceneName, duration));
    }
    
    /// <summary>
    /// 淡入淡出并加载新场景的协程
    /// </summary>
    private IEnumerator FadeAndLoadRoutine(string sceneName, float duration)
    {
        // 启用Canvas
        transitionCanvas.enabled = true;
        
        // 淡入（从透明到不透明）
        yield return FadeRoutine(0f, 1f, duration * 0.5f);
        
        // 加载场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        
        // 淡出（从不透明到透明）
        yield return FadeRoutine(1f, 0f, duration * 0.5f);
        
        // 禁用Canvas
        transitionCanvas.enabled = false;
    }
    
    /// <summary>
    /// 淡入淡出效果协程
    /// </summary>
    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration)
    {
        float elapsedTime = 0;
        Color currentColor = fadeImage.color;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            
            // 更新Alpha值
            float alpha = Mathf.Lerp(startAlpha, endAlpha, normalizedTime);
            fadeImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
            
            yield return null;
        }
        
        // 确保最终Alpha值正确
        fadeImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, endAlpha);
    }
    
    /// <summary>
    /// 仅执行淡入效果
    /// </summary>
    public void FadeIn(float duration = -1)
    {
        if (duration < 0) duration = defaultFadeDuration;
        transitionCanvas.enabled = true;
        StartCoroutine(FadeRoutine(0f, 1f, duration));
    }
    
    /// <summary>
    /// 仅执行淡出效果
    /// </summary>
    public void FadeOut(float duration = -1)
    {
        if (duration < 0) duration = defaultFadeDuration;
        transitionCanvas.enabled = true;
        StartCoroutine(FadeOutRoutine(duration));
    }
    
    /// <summary>
    /// 淡出效果并禁用Canvas的协程
    /// </summary>
    private IEnumerator FadeOutRoutine(float duration)
    {
        yield return FadeRoutine(1f, 0f, duration);
        transitionCanvas.enabled = false;
    }
}