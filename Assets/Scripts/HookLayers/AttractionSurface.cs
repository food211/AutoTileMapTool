using UnityEngine;

public class AttractionSurface : MonoBehaviour
{
    [Header("吸引力设置")]
    [SerializeField] private float attractionForce = 10f; // 吸引力大小
    [SerializeField] private bool useConstantForce = true; // 是否使用恒定力度
    [SerializeField] private float maxAttractionDistance = 10f; // 最大吸引距离
    [SerializeField] private AnimationCurve forceFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // 力度衰减曲线
    
    [Header("视觉效果")]
    [SerializeField] private bool showAttractionArea = true; // 是否显示吸引区域
    [SerializeField] private Color areaColor = new Color(0.2f, 0.6f, 1f, 0.2f); // 区域颜色
    
    private void OnTriggerStay2D(Collider2D collision)
    {
        // 检查是否是玩家
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player == null) return;
        
        // 获取玩家刚体
        Rigidbody2D rb = player.GetRigidbody();
        if (rb == null) return;
        
        // 计算方向（指向中心）
        Vector2 direction = (Vector2)transform.position - (Vector2)player.transform.position;
        float distance = direction.magnitude;
        
        // 如果超出最大距离，不施加力
        if (distance > maxAttractionDistance) return;
        
        // 标准化方向
        direction.Normalize();
        
        // 计算力度
        float forceMagnitude = attractionForce;
        
        // 如果不使用恒定力度，根据距离和曲线计算力度
        if (!useConstantForce)
        {
            float normalizedDistance = distance / maxAttractionDistance;
            forceMagnitude *= forceFalloff.Evaluate(1f - normalizedDistance);
        }
        
        // 应用力
        rb.AddForce(direction * forceMagnitude);
    }
    
    // 在编辑器中可视化吸引区域
    private void OnDrawGizmos()
    {
        if (!showAttractionArea) return;
        
        Gizmos.color = areaColor;
        
        // 获取碰撞体
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            // 根据碰撞体类型绘制不同形状
            if (collider is CircleCollider2D)
            {
                CircleCollider2D circleCollider = (CircleCollider2D)collider;
                Gizmos.DrawSphere(transform.position, circleCollider.radius);
            }
            else if (collider is BoxCollider2D)
            {
                BoxCollider2D boxCollider = (BoxCollider2D)collider;
                // 考虑旋转
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                    transform.position, 
                    transform.rotation, 
                    transform.lossyScale
                );
                Gizmos.matrix = rotationMatrix;
                Gizmos.DrawCube(boxCollider.offset, boxCollider.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
        else
        {
            // 如果没有碰撞体，绘制默认区域
            Gizmos.DrawSphere(transform.position, maxAttractionDistance);
        }
        
        // 绘制最大吸引距离
        Gizmos.color = new Color(areaColor.r, areaColor.g, areaColor.b, 0.1f);
        Gizmos.DrawWireSphere(transform.position, maxAttractionDistance);
        
        // 绘制中心点
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position, 0.2f);
    }
}