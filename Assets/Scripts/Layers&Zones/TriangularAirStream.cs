using UnityEngine;

public class TriangularAirStream : MonoBehaviour
{
    [Header("力场设置")]
    [Tooltip("气流最大力度（中心位置）")]
    public float maxForce = 15f;
    
    [Tooltip("气流影响的水平范围")]
    public float horizontalRange = 2f;
    
    [Tooltip("三角形形状的锐利程度（值越大，两侧衰减越快）")]
    [Range(1f, 5f)]
    public float triangleSharpness = 2f;
    
    [Tooltip("垂直方向的力度变化（值越大，顶部衰减越快）")]
    [Range(0f, 1f)]
    public float verticalFalloff = 0.2f;
    
    [Header("视觉反馈")]
    [Tooltip("是否在编辑器中显示力场可视化")]
    public bool showDebugGizmos = true;
    
    [Tooltip("可视化箭头的数量")]
    [Range(5, 30)]
    public int debugArrowCount = 15;
    
    [Header("高级设置")]
    [Tooltip("力的应用模式")]
    public ForceMode2D forceMode = ForceMode2D.Force;
    
    [Tooltip("是否使用连续检测而非触发器")]
    public bool useContinuousDetection = false;
    
    // 内部变量
    private Collider2D airStreamCollider;
    private Transform cachedTransform;
    
    private void Awake()
    {
        // 缓存组件引用以提高性能
        cachedTransform = transform;
        airStreamCollider = GetComponent<Collider2D>();
        
        // 确保有碰撞器
        if (airStreamCollider == null)
        {
            Debug.LogWarning("气流对象缺少碰撞器！添加一个BoxCollider2D...");
            airStreamCollider = gameObject.AddComponent<BoxCollider2D>();
            ((BoxCollider2D)airStreamCollider).size = new Vector2(horizontalRange * 2, 5f);
            airStreamCollider.isTrigger = true;
        }
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!useContinuousDetection)
        {
            ApplyForceToObject(other);
        }
    }
    
    private void FixedUpdate()
    {
        if (useContinuousDetection)
        {
            // 获取范围内的所有碰撞体
            Collider2D[] colliders = Physics2D.OverlapBoxAll(
                cachedTransform.position, 
                new Vector2(horizontalRange * 2, airStreamCollider.bounds.size.y),
                cachedTransform.eulerAngles.z
            );
            
            foreach (Collider2D collider in colliders)
            {
                ApplyForceToObject(collider);
            }
        }
    }
    
    private void ApplyForceToObject(Collider2D other)
    {
        // 检查是否有刚体
        Rigidbody2D rb = other.attachedRigidbody;
        if (rb == null) return;
        
        // 忽略静态刚体
        if (rb.bodyType == RigidbodyType2D.Static) return;
        
        // 计算相对位置
        Vector2 localPos = cachedTransform.InverseTransformPoint(other.transform.position);
        
        // 计算水平距离比例（0=中心，1=边缘）
        float horizontalDistanceRatio = Mathf.Abs(localPos.x) / horizontalRange;
        if (horizontalDistanceRatio > 1f) return; // 超出范围
        
        // 计算垂直位置比例（0=底部，1=顶部）
        float verticalRatio = Mathf.InverseLerp(
            airStreamCollider.bounds.min.y,
            airStreamCollider.bounds.max.y,
            other.transform.position.y
        );
        
        // 三角形形状力度计算
        float triangleForce = Mathf.Pow(1f - horizontalDistanceRatio, triangleSharpness);
        
        // 垂直衰减（可选）
        float verticalFactor = 1f - (verticalRatio * verticalFalloff);
        
        // 计算最终力度
        float finalForce = maxForce * triangleForce * verticalFactor;
        
        // 应用力
        rb.AddForce(Vector2.up * finalForce, forceMode);
        
        // 可选：添加轻微的水平力，将物体推向中心
        if (localPos.x != 0)
        {
            float centeringForce = maxForce * 0.1f * triangleForce * Mathf.Sign(-localPos.x);
            rb.AddForce(Vector2.right * centeringForce, forceMode);
        }
    }
    
    // 可视化调试
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // 缓存变换，以防在编辑器中修改
        Transform t = transform;
        Vector3 position = t.position;
        
        // 绘制气流区域边界
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireCube(position, new Vector3(horizontalRange * 2, 5f, 0.1f));
        
        // 绘制力场箭头
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.6f);
        
        for (int i = 0; i < debugArrowCount; i++)
        {
            // 计算水平位置
            float xPos = Mathf.Lerp(-horizontalRange, horizontalRange, i / (float)(debugArrowCount - 1));
            Vector3 startPos = position + new Vector3(xPos, -2f, 0);
            
            // 计算力度
            float distanceRatio = Mathf.Abs(xPos) / horizontalRange;
            float arrowForce = Mathf.Pow(1f - distanceRatio, triangleSharpness);
            float arrowLength = arrowForce * 0.5f;
            
            // 绘制箭头
            Vector3 endPos = startPos + Vector3.up * arrowLength;
            Gizmos.DrawLine(startPos, endPos);
            
            // 箭头尖
            Vector3 left = endPos + new Vector3(-0.1f, -0.1f, 0);
            Vector3 right = endPos + new Vector3(0.1f, -0.1f, 0);
            Gizmos.DrawLine(endPos, left);
            Gizmos.DrawLine(endPos, right);
        }
    }
}
