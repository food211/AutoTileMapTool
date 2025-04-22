using UnityEngine;

// 定义可电击接口
[UnityEngine.Scripting.Preserve]
public interface IElectrifiable
{
    void OnElectricContact(ElectricSurface electricSurface);
}

public class ElectricSurface : MonoBehaviour
{
    [Header("视觉效果")]
    [SerializeField] private bool applyElectricVisuals = true; // 是否应用电击视觉效果
    [SerializeField] private float glowIntensity = 1.5f; // 发光强度
    [SerializeField] private Color electricColor = new Color(0.3f, 0.7f, 1f, 0.9f); // 电击色调
    
    [Header("电击属性")]
    [SerializeField] private float electricPower = 10f; // 电击能力
    [SerializeField] private bool isTrigger = true; // 是否为触发器
    [SerializeField] private float shockInterval = 0.5f; // 电击间隔时间（秒）
    [SerializeField] private bool continuousShock = true; // 是否持续电击
    
    private Collider2D electricCollider;
    private SpriteRenderer spriteRenderer;
    private float lastShockTime = 0f;
    
    private void Awake()
    {
        // 获取碰撞体
        electricCollider = GetComponent<Collider2D>();
        if (electricCollider == null)
        {
            // 添加组件
            electricCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // 设置是否为触发器
        electricCollider.isTrigger = isTrigger;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 确保该对象被标记为"Electric"标签
        if (gameObject.tag != "Elect")
        {
            gameObject.tag = "Elect";
        }
    }
    
    private void Start()
    {
        // 应用电击视觉效果
        if (applyElectricVisuals && spriteRenderer != null)
        {
            ApplyElectricVisuals();
        }
        
        // 开始电流闪烁效果
        if (applyElectricVisuals)
        {
            StartCoroutine(ElectricFlickerRoutine());
        }
    }

    private void ApplyElectricVisuals()
    {
        // 设置材质属性以模拟电击效果
        Material material = spriteRenderer.material;
        
        // 检查材质是否支持这些属性
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", electricColor * glowIntensity);
            material.EnableKeyword("_EMISSION");
        }
        
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", electricColor);
        }
    }
    
    // 如果碰撞体是触发器(Trigger)，使用这个方法
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleInteraction(collision.gameObject);
    }
    
    // 如果碰撞体不是触发器，使用这个方法
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleInteraction(collision.gameObject);
    }
    
    // 处理持续接触的情况
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (continuousShock && Time.time >= lastShockTime + shockInterval)
        {
            HandleInteraction(collision.gameObject);
            lastShockTime = Time.time;
        }
    }
    
    // 处理持续接触的情况（非触发器）
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (continuousShock && Time.time >= lastShockTime + shockInterval)
        {
            HandleInteraction(collision.gameObject);
            lastShockTime = Time.time;
        }
    }
    
    // 处理交互逻辑
    private void HandleInteraction(GameObject obj)
    {
        // 检查对象是否实现了IElectrifiable接口
        IElectrifiable electrifiable = obj.GetComponent<IElectrifiable>();
        
        // 如果对象实现了IElectrifiable接口，调用其OnElectricContact方法
        if (electrifiable != null)
        {
            electrifiable.OnElectricContact(this);
        }
    }
    
    // 获取电击能力值的公共方法
    public float GetElectricPower()
    {
        return electricPower;
    }
    
    // 获取电击间隔的公共方法
    public float GetShockInterval()
    {
        return shockInterval;
    }
    
    // 电击闪烁效果
    private System.Collections.IEnumerator ElectricFlickerRoutine()
    {
        if (spriteRenderer == null) yield break;
        
        Material material = spriteRenderer.material;
        float originalIntensity = glowIntensity;
        
        while (true)
        {
            // 随机闪烁强度
            float randomIntensity = Random.Range(originalIntensity * 0.7f, originalIntensity * 1.3f);
            
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", electricColor * randomIntensity);
            }
            
            // 随机等待时间
            yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
        }
    }
    
    // 停用电击方法（可以被水或其他元素调用）
    public void Disable(float duration = 5f)
    {
        StartCoroutine(DisableRoutine(duration));
    }
    
    private System.Collections.IEnumerator DisableRoutine(float duration)
    {
        // 暂时禁用电击效果
        bool originalContinuousShock = continuousShock;
        float originalElectricPower = electricPower;
        
        continuousShock = false;
        electricPower = 0f;
        
        // 视觉效果变暗
        if (spriteRenderer != null && spriteRenderer.material.HasProperty("_EmissionColor"))
        {
            spriteRenderer.material.SetColor("_EmissionColor", Color.black);
        }
        
        // 等待指定时间
        yield return new WaitForSeconds(duration);
        
        // 恢复原始状态
        continuousShock = originalContinuousShock;
        electricPower = originalElectricPower;
        
        // 恢复视觉效果
        if (applyElectricVisuals && spriteRenderer != null)
        {
            ApplyElectricVisuals();
        }
    }
    
    // 可以添加一个方法来增强电击效果，例如被雨水淋湿后
    public void Enhance(float multiplier = 1.5f, float duration = 3f)
    {
        StartCoroutine(EnhanceRoutine(multiplier, duration));
    }
    
    private System.Collections.IEnumerator EnhanceRoutine(float multiplier, float duration)
    {
        float originalPower = electricPower;
        float originalGlow = glowIntensity;
        
        // 增强电击效果
        electricPower *= multiplier;
        glowIntensity *= multiplier;
        
        // 更新视觉效果
        if (spriteRenderer != null && spriteRenderer.material.HasProperty("_EmissionColor"))
        {
            spriteRenderer.material.SetColor("_EmissionColor", electricColor * glowIntensity);
        }
        
        // 等待指定时间
        yield return new WaitForSeconds(duration);
        
        // 恢复原始状态
        electricPower = originalPower;
        glowIntensity = originalGlow;
        
        // 恢复视觉效果
        if (applyElectricVisuals && spriteRenderer != null)
        {
            ApplyElectricVisuals();
        }
    }
}