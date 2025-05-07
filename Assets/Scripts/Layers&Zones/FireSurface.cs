using UnityEngine;

// 定义可燃接口
[UnityEngine.Scripting.Preserve]
public interface IFlammable
{
    void OnFireContact(FireSurface fireSurface);
}

public class FireSurface : MonoBehaviour
{
    [Header("视觉效果")]
    [SerializeField] private bool applyFireVisuals = true; // 是否应用视觉效果
    [SerializeField] private float reflectivity = 0.1f; // 反光度
    [SerializeField] private Color tintColor = new Color(0.5f, 0.1f, 0.2f, 1f); // 火色调
    
    [Header("火焰属性")]
    [SerializeField] private float fireDamage = 10f; // 火焰伤害
    [SerializeField] private bool isTrigger = false; // 是否为触发器
    
    private Collider2D fireCollider;
    private SpriteRenderer spriteRenderer;
    
    private void Awake()
    {
        // 获取碰撞体
        fireCollider = GetComponent<Collider2D>();
        if (fireCollider == null)
        {
            // 添加组件
            fireCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // 设置是否为触发器
        fireCollider.isTrigger = isTrigger;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 确保该对象被标记为"Fire"标签
        if (gameObject.tag != "Fire")
        {
            gameObject.tag = "Fire";
        }
    }
    
    private void Start()
    {
        // 应用火焰视觉效果
        if (applyFireVisuals && spriteRenderer != null)
        {
            ApplyFireVisuals();
        }
    }

    private void ApplyFireVisuals()
    {
        // 设置材质属性以模拟火焰效果
        Material material = spriteRenderer.material;
        
        // 检查材质是否支持这些属性
        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", reflectivity);
        }
        
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", tintColor);
        }
        
        // 如果使用URP或HDRP，可以设置额外的属性
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", reflectivity);
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
        // 可以处理持续接触火焰的效果，例如随时间增加伤害
        HandleInteraction(collision.gameObject);
    }
    
    // 处理持续接触的情况（非触发器）
    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleInteraction(collision.gameObject);
    }
    
    // 处理交互逻辑
    private void HandleInteraction(GameObject obj)
    {
        // 检查对象是否实现了IFlammable接口
        IFlammable flammable = obj.GetComponent<IFlammable>();
        
        // 如果对象实现了IFlammable接口，调用其OnFireContact方法
        if (flammable != null)
        {
            flammable.OnFireContact(this);
        }
    }
    
    // 获取火焰伤害值的公共方法
    public float GetFireDamage()
    {
        return fireDamage;
    }
}