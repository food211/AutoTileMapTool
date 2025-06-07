using System.Collections;
using UnityEngine;
using ObjectPoolClass; // 确保引用正确的命名空间

[RequireComponent(typeof(ParticleSystem))]
public class FireParticleAutoSize : MonoBehaviour
{
    [Header("缩放设置")]
    [Tooltip("粒子系统大小相对于目标对象的比例")]
    [SerializeField] private float sizeMultiplier = 0.3f;

    [Tooltip("是否在启动时自动调整大小")]
    public bool adjustOnStart = true;

    [Tooltip("是否持续调整大小以适应目标变化")]
    [SerializeField] private bool continuousAdjustment = false;

    [Tooltip("连续调整的更新间隔(秒)")]
    [SerializeField] private float updateInterval = 0.5f;

    // 组件引用
    private ParticleSystem myParticleSystem;
    private Transform targetTransform;
    private SpriteRenderer targetRenderer;
    private float timeSinceLastUpdate = 0f;
    private bool isFadingOut = false;
    private Coroutine fadeCoroutine = null;

    private void Awake()
    {
        myParticleSystem = GetComponent<ParticleSystem>();
    }

    private void Start()
    {
        if (adjustOnStart)
        {
            // 获取父对象
            targetTransform = transform.parent;
            if (targetTransform != null)
            {
                // 尝试获取SpriteRenderer
                targetRenderer = targetTransform.GetComponent<SpriteRenderer>();
                if (targetRenderer != null)
                {
                    AdjustToTarget(targetRenderer);
                }
            }
        }
    }
        private void OnEnable()
    {
        // 重置状态
        isFadingOut = false;
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        
        // 确保粒子系统处于播放状态
        if (myParticleSystem != null && !myParticleSystem.isPlaying)
        {
            myParticleSystem.Play();
        }
    }

        // 在对象被禁用时确保清理资源
    private void OnDisable()
    {
        // 停止所有协程
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        
        // 重置状态
        isFadingOut = false;
        
        // 停止粒子系统
        if (myParticleSystem != null)
        {
            myParticleSystem.Stop(true);
        }
    }


    private void Update()
    {
        if (continuousAdjustment && targetRenderer != null)
        {
            timeSinceLastUpdate += Time.deltaTime;

            if (timeSinceLastUpdate >= updateInterval)
            {
                AdjustToTarget(targetRenderer);
                timeSinceLastUpdate = 0f;
            }
        }
    }

    // 调整粒子系统大小以适应目标
    public void AdjustToTarget(Renderer targetRenderer)
    {
        if (myParticleSystem == null || targetRenderer == null) return;
        
        // 获取目标的边界
        Bounds bounds = targetRenderer.bounds;
        
        // 计算合适的大小
        float targetSize = Mathf.Max(bounds.size.x, bounds.size.y) * sizeMultiplier;
        
        // 应用大小
        var main = myParticleSystem.main;
        main.startSize = new ParticleSystem.MinMaxCurve(targetSize * 0.8f, targetSize);
        
        // 将粒子系统位置设置为目标中心
        transform.position = bounds.center;
    }

    // 增加火焰强度 - 用于绳索燃烧进度
    public void IncreaseIntensity(float progress)
    {
        // 限制progress在0-1范围内，防止异常值
        progress = Mathf.Clamp01(progress);

        if (myParticleSystem == null) return;

        // 获取粒子系统的主模块
        var main = myParticleSystem.main;
        var emission = myParticleSystem.emission;

        // 根据进度从0逐渐增加到预设大小
        if (progress <= 0.01f) // 几乎没有进度时
        {
            // 设置为最小值
            emission.rateOverTime = 0.1f; // 几乎不发射粒子
        }
        else // 有进度时
        {
            // 发射率从几乎为0逐渐增加到预设的最大值
            float maxEmissionRate = 30f; // 最大发射率
            float currentEmissionRate = Mathf.Lerp(0.1f, maxEmissionRate, progress);
            emission.rateOverTime = currentEmissionRate;
        }
    }

    // 淡出火焰效果，然后回收到对象池
    public void FadeOutThenReturn(float duration)
    {
        if (isFadingOut) return;

        // 停止之前的淡出协程
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // 开始新的淡出协程
        fadeCoroutine = StartCoroutine(FadeOutAndReturnToPoolCoroutine(duration));
    }
    // 淡出协程
    private IEnumerator FadeOutAndReturnToPoolCoroutine(float duration)
    {
        isFadingOut = true;

        if (myParticleSystem == null)
        {
            // 如果没有粒子系统，直接回收
            ObjectPool.Instance.ReturnObject(gameObject);
            yield break;
        }

        // 停止发射新粒子
        myParticleSystem.Stop(true); // true表示停止发射但允许现有粒子完成生命周期

        // 获取粒子系统的主模块
        var main = myParticleSystem.main;

        // 等待所有粒子消失
        float fadeTime = Mathf.Max(main.duration + main.startLifetime.constantMax, duration);
        yield return new WaitForSeconds(fadeTime);

        // 回收到对象池
        ObjectPool.Instance.ReturnObject(gameObject);

        isFadingOut = false;
        fadeCoroutine = null;
    }
}