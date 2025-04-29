using UnityEngine;

public class DeadZone : MonoBehaviour
{
    [Header("死亡区域设置")]
    [SerializeField] private bool instantKill = true; // 是否立即杀死玩家
    [SerializeField] private float damageAmount = 100f; // 如果不是立即杀死，造成的伤害
    [SerializeField] private string deathMessage = "你被杀死了"; // 死亡消息
    [SerializeField] private bool respawnPlayer = true; // 是否重生玩家
    
    [Header("视觉效果")]
    [SerializeField] private bool showDeadZone = true; // 是否显示死亡区域
    [SerializeField] private Color zoneColor = new Color(1f, 0f, 0f, 0.3f); // 区域颜色
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 检查是否是玩家
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player == null) return;
        
        // 如果玩家处于无敌状态，不触发死亡
        if (player.IsInvincible()) return;
        
        // 触发玩家死亡事件
        HandlePlayerDeath(player);
    }
    
    private void HandlePlayerDeath(PlayerController player)
    {
        // 这里可以添加死亡特效、声音等
        #if UNITY_EDITOR
        Debug.LogFormat($"玩家触发死亡区域: {deathMessage}");
        #endif
        
        // 触发玩家死亡事件
        // 注意：这里预留了GameEvents.TriggerPlayerDeath方法，需要在GameEvents类中实现
        if (instantKill)
        {
            // 预留的死亡事件调用
            // GameEvents.TriggerPlayerDeath(deathMessage, respawnPlayer);
            
            // 临时解决方案：直接在控制台输出死亡信息
            #if UNITY_EDITOR
            Debug.LogFormat($"玩家死亡: {deathMessage}");
            #endif
            // 如果需要重生，可以在这里添加重生逻辑
            if (respawnPlayer)
            {
                // 预留重生逻辑
                // 可以调用场景管理器或游戏管理器的重生方法
                #if UNITY_EDITOR
                Debug.LogFormat("玩家将被重生");
                #endif
            }
        }
        else
        {
            // 预留的伤害事件调用
            // GameEvents.TriggerPlayerDamage(damageAmount);
            #if UNITY_EDITOR
            Debug.LogFormat($"玩家受到伤害: {damageAmount}");
            #endif
        }
    }
    
    // 在编辑器中可视化死亡区域
    private void OnDrawGizmos()
    {
        if (!showDeadZone) return;
        
        Gizmos.color = zoneColor;
        
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
            Gizmos.DrawSphere(transform.position, 1f);
        }
        
        // 绘制死亡标志
        Gizmos.color = Color.red;
        float size = 0.5f;
        Vector3 center = transform.position;
        
        // 绘制十字
        Gizmos.DrawLine(center + new Vector3(-size, -size, 0), center + new Vector3(size, size, 0));
        Gizmos.DrawLine(center + new Vector3(-size, size, 0), center + new Vector3(size, -size, 0));
    }
}