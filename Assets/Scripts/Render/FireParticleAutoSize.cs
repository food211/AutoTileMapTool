using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class FireParticleAutoSize : MonoBehaviour
{
    [Header("缩放设置")]
    [Tooltip("粒子系统大小相对于目标对象的比例")]
    [SerializeField] private float sizeMultiplier = 0.3f;
    
    [Tooltip("粒子发射率基础值")]
    [SerializeField] private float baseEmissionRate = 10f;
    
    [Tooltip("粒子发射率与面积的比例系数")]
    [SerializeField] private float emissionAreaMultiplier = 5f;
    
    [Tooltip("是否在启动时自动调整大小")]
    [SerializeField] private bool adjustOnStart = true;
    
    [Tooltip("是否持续调整大小以适应目标变化")]
    [SerializeField] private bool continuousAdjustment = false;
    
    [Tooltip("连续调整的更新间隔(秒)")]
    [SerializeField] private float updateInterval = 0.5f;
    
    // 组件引用
    private ParticleSystem myParticleSystem;
    private Transform targetTransform;
    private SpriteRenderer targetRenderer;
    private float timeSinceLastUpdate = 0f;
    
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
                    AdjustToTarget();
                }
            }
        }
    }
    
    private void Update()
    {
        if (continuousAdjustment && targetRenderer != null)
        {
            timeSinceLastUpdate += Time.deltaTime;
            
            if (timeSinceLastUpdate >= updateInterval)
            {
                AdjustToTarget();
                timeSinceLastUpdate = 0f;
            }
        }
    }
    
    // 手动调用此方法来调整粒子系统
    public void AdjustToTarget(SpriteRenderer newTarget = null)
    {
        // 如果提供了新目标，更新引用
        if (newTarget != null)
        {
            targetRenderer = newTarget;
            transform.position = targetRenderer.transform.position;
        }
        
        // 确保有有效的目标
        if (targetRenderer == null) return;
        
        // 获取目标边界
        Bounds targetBounds = targetRenderer.bounds;
        
        // 调整粒子系统的形状
        var shape = myParticleSystem.shape;
        
        // 根据目标物体的大小调整粒子系统的形状
        if (shape.shapeType == ParticleSystemShapeType.Box || 
            shape.shapeType == ParticleSystemShapeType.Rectangle)
        {
            // 对于盒状或矩形形状，设置尺寸
            shape.scale = new Vector3(
                targetBounds.size.x * 0.8f, // 稍微小于目标物体，以便火焰不会超出太多
                targetBounds.size.y * 0.8f,
                0.1f // 保持较小的z尺寸
            );
        }
        else if (shape.shapeType == ParticleSystemShapeType.Circle || 
                 shape.shapeType == ParticleSystemShapeType.Sphere)
        {
            // 对于圆形或球形形状，设置半径
            float radius = Mathf.Max(targetBounds.size.x, targetBounds.size.y) * 0.4f;
            shape.radius = radius;
        }
        
        // 调整粒子系统的位置，使其中心与目标物体对齐
        transform.localPosition = Vector3.zero;
        
        // 调整主模块参数以匹配物体大小
        var main = myParticleSystem.main;
        float size = Mathf.Max(targetBounds.size.x, targetBounds.size.y) * sizeMultiplier;
        main.startSize = size; // 使用startSize而不是startSizeMultiplier
        
        // 调整发射率以匹配物体大小
        var emission = myParticleSystem.emission;
        float area = targetBounds.size.x * targetBounds.size.y;
        emission.rateOverTime = Mathf.Max(baseEmissionRate, area * emissionAreaMultiplier); // 使用rateOverTime而不是rateOverTimeMultiplier
    }
    
    // 设置目标对象
    public void SetTarget(GameObject target)
    {
        if (target != null)
        {
            targetRenderer = target.GetComponent<SpriteRenderer>();
            if (targetRenderer != null)
            {
                AdjustToTarget();
            }
        }
    }
    
    // 设置为绳索火焰模式
    public void SetAsRopeFire(float ropeWidth)
    {
        var main = myParticleSystem.main;
        main.startSize = ropeWidth * 2.0f; // 火焰大小是绳索宽度的2倍
        
        var shape = myParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = ropeWidth * 0.5f;
        
        var emission = myParticleSystem.emission;
        emission.rateOverTime = 10f; // 基础发射率
    }
    
    // 增加火焰强度 - 用于绳索燃烧进度
    public void IncreaseIntensity(float progress)
    {
        var emission = myParticleSystem.emission;
        emission.rateOverTime = baseEmissionRate + 20 * progress; // 随着燃烧进度增加发射率
        
        var main = myParticleSystem.main;
        float currentSize = main.startSize.constant; // 获取当前大小的常量值
        main.startSize = currentSize * (1 + progress * 0.5f); // 随着燃烧进度增加火焰大小
    }
    
    // 逐渐熄灭火焰
    public void FadeOut(float duration = 2.0f)
    {
        // 减少粒子寿命
        var main = myParticleSystem.main;
        main.startLifetime = main.startLifetime.constant * 0.5f; // 使用startLifetime.constant获取当前值
        
        // 停止发射新粒子
        myParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        
        // 销毁游戏对象
        Destroy(gameObject, duration);
    }
}