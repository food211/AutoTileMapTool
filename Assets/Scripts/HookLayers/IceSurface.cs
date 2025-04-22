using UnityEngine;

// 定义可冰冻接口
[UnityEngine.Scripting.Preserve]
public interface IFreezable
{
    void OnIceContact(IceSurface iceSurface);
}

public class IceSurface : MonoBehaviour
{
    [Header("视觉效果")]
    [SerializeField] private bool applyIceVisuals = true; // 是否应用冰面视觉效果
    [SerializeField] private float reflectivity = 0.5f; // 反光度
    [SerializeField] private Color tintColor = new Color(0.8f, 0.95f, 1f, 0.8f); // 冰面色调
    
    [Header("冰面属性")]
    [SerializeField] private float freezePower = 5f; // 冰冻能力
    public bool isTrigger = true; // 是否为触发器
    [SerializeField] private float friction = 0.05f; // 冰面摩擦力（越小越滑）
    
    private Collider2D iceCollider;
    private SpriteRenderer spriteRenderer;
    private PhysicsMaterial2D iceMaterial;
    
    private void Awake()
    {
        // 获取碰撞体
        iceCollider = GetComponent<Collider2D>();
        if (iceCollider == null)
        {
            // 添加组件
            iceCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // 设置是否为触发器
        iceCollider.isTrigger = isTrigger;
        
        // 如果不是触发器，创建并应用物理材质（让表面变滑）
        if (!isTrigger)
        {
            CreateAndApplyIceMaterial();
        }
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 确保该对象被标记为"Ice"标签
        if (gameObject.tag != "Ice")
        {
            gameObject.tag = "Ice";
        }
    }
    
    private void Start()
    {
        // 应用冰面视觉效果
        if (applyIceVisuals && spriteRenderer != null)
        {
            ApplyIceVisuals();
        }
    }

    private void ApplyIceVisuals()
    {
        // 设置材质属性以模拟冰面效果
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
    
    private void CreateAndApplyIceMaterial()
    {
        // 创建冰面物理材质
        iceMaterial = new PhysicsMaterial2D("Ice");
        iceMaterial.friction = friction;
        iceMaterial.bounciness = 0.1f; // 略微的弹性
        
        // 应用到碰撞体
        iceCollider.sharedMaterial = iceMaterial;
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
        // 可以处理持续接触冰面的效果，例如随时间增加冰冻效果
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
        // 检查对象是否实现了IFreezable接口
        IFreezable freezable = obj.GetComponent<IFreezable>();
        
        // 如果对象实现了IFreezable接口，调用其OnIceContact方法
        if (freezable != null)
        {
            freezable.OnIceContact(this);
        }
    }
    
    // 获取冰冻能力值的公共方法
    public float GetFreezePower()
    {
        return freezePower;
    }
    
    // 获取冰面摩擦力的公共方法
    public float GetFriction()
    {
        return friction;
    }
    
    // 冰面融化方法（可以被FireSurface调用）
    public void Melt()
    {
        // 实现冰面融化效果
        StartCoroutine(MeltRoutine());
    }
    
    private System.Collections.IEnumerator MeltRoutine()
    {
        // 逐渐降低冰面的透明度
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            float elapsedTime = 0f;
            float meltDuration = 2f; // 融化持续时间
            
            while (elapsedTime < meltDuration)
            {
                float alpha = Mathf.Lerp(originalColor.a, 0f, elapsedTime / meltDuration);
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        
        // 融化完成后销毁冰面
        Destroy(gameObject);
    }
}