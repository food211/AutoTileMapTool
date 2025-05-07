using UnityEngine;

public class RepulsionSurface : MonoBehaviour
{
    [Header("排斥力设置")]
    [SerializeField] private float repulsionForce = 15f; // 排斥力大小
    [SerializeField] private bool useConstantForce = false; // 是否使用恒定力度
    [SerializeField] private float maxRepulsionDistance = 10f; // 最大排斥距离
    [SerializeField] private AnimationCurve forceFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // 力度衰减曲线
    
    [Header("视觉效果")]
    [SerializeField] private bool showRepulsionArea = true; // 是否显示排斥区域
    [SerializeField] private Color areaColor = new Color(1f, 0.3f, 0.3f, 0.2f); // 区域颜色
    
    private void OnTriggerStay2D(Collider2D collision)
    {
        // 检查是否是玩家
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player == null) return;
        
        // 获取玩家刚体
        Rigidbody2D rb = player.GetRigidbody();
        if (rb == null) return;
        
        // 计算方向（远离中心）
        Vector2 direction = (Vector2)player.transform.position - (Vector2)transform.position;
        float distance = direction.magnitude;
        
        // 如果超出最大距离，不施加力
        if (distance > maxRepulsionDistance) return;
        
        // 如果距离太小，防止除零错误
        if (distance < 0.1f)
        {
            // 使用随机方向
            direction = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
        }
        
        // 标准化方向
        direction.Normalize();
        
        // 计算力度
        float forceMagnitude = repulsionForce;
        
        // 如果不使用恒定力度，根据距离和曲线计算力度
        if (!useConstantForce)
        {
            float normalizedDistance = distance / maxRepulsionDistance;
            forceMagnitude *= forceFalloff.Evaluate(1f - normalizedDistance);
        }
        
        // 应用力
        rb.AddForce(direction * forceMagnitude);
    }
    
    // 在编辑器中可视化排斥区域
    private void OnDrawGizmos()
    {
        if (!showRepulsionArea) return;
        
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
            Gizmos.DrawSphere(transform.position, maxRepulsionDistance);
        }
        
        // 绘制最大排斥距离
        Gizmos.color = new Color(areaColor.r, areaColor.g, areaColor.b, 0.1f);
        Gizmos.DrawWireSphere(transform.position, maxRepulsionDistance);
        
        // 绘制中心点
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.2f);
        
        // 绘制排斥方向指示
        Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.5f);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 1.5f;
            Vector3 start = transform.position + direction * 0.5f;
            Vector3 end = transform.position + direction * 1.5f;
            Gizmos.DrawLine(start, end);
            // 绘制箭头
            Vector3 arrowDir1 = Quaternion.Euler(0, 0, 20) * -direction.normalized * 0.3f;
            Vector3 arrowDir2 = Quaternion.Euler(0, 0, -20) * -direction.normalized * 0.3f;
            Gizmos.DrawLine(end, end + arrowDir1);
            Gizmos.DrawLine(end, end + arrowDir2);
        }
    }
}