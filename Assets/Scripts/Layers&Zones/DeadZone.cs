using UnityEngine;
using System.Collections.Generic;

public class DeadZone : MonoBehaviour
{
    [Header("死亡区域设置")]
    [SerializeField] private bool instantKill = true; // 是否立即杀死玩家
    [SerializeField] private float damageAmount = 100f; // 如果不是立即杀死，造成的伤害
    [SerializeField] private string deathMessage = "你被杀死了"; // 死亡消息
    [SerializeField] private bool respawnPlayer = true; // 是否重生玩家
    [SerializeField] private bool ForceDeath = true; // 是否无视无敌状态强制杀死

    [Header("区域设置")]
    [SerializeField] private bool useChildrenAsZones = true; // 是否使用子对象作为检测区域
    [SerializeField] private bool showDeadZone = true; // 是否显示死亡区域
    [SerializeField] private Color zoneColor = new Color(1f, 0f, 0f, 0.3f); // 区域颜色
    
    // 存储所有检测区域的碰撞体
    private List<Collider2D> zoneColliders = new List<Collider2D>();
    
    private void Awake()
    {
        InitializeZones();
    }
    
    private void InitializeZones()
    {
        zoneColliders.Clear();
        
        // 如果使用子对象作为检测区域
        if (useChildrenAsZones)
        {
            // 获取所有子对象的碰撞体
            Collider2D[] childColliders = GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D collider in childColliders)
            {
                // 确保碰撞体设置为触发器
                collider.isTrigger = true;
                zoneColliders.Add(collider);
                
                // 如果子对象有自己的DeadZone组件，不要添加它的碰撞体
                if (collider.gameObject != gameObject && collider.GetComponent<DeadZone>() == null)
                {
                    // 为子对象添加TriggerForwarder组件，将触发事件转发给父对象
                    TriggerForwarder forwarder = collider.gameObject.GetComponent<TriggerForwarder>();
                    if (forwarder == null)
                    {
                        forwarder = collider.gameObject.AddComponent<TriggerForwarder>();
                    }
                    forwarder.SetTarget(this);
                }
            }
            #if UNITY_EDITOR
            Debug.Log($"DeadZone '{gameObject.name}' 使用 {zoneColliders.Count} 个碰撞体作为检测区域");
            #endif
        }
        else
        {
            // 只使用自身的碰撞体
            Collider2D ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null)
            {
                ownCollider.isTrigger = true;
                zoneColliders.Add(ownCollider);
            }
            else
            {
                Debug.LogWarning($"DeadZone '{gameObject.name}' 没有碰撞体组件，无法检测碰撞");
            }
        }
    }
    
    // 这个方法可以被TriggerForwarder调用
    public void OnForwardedTriggerEnter2D(Collider2D collision)
    {
        HandleCollision(collision);
    }
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleCollision(collision);
    }
    
    private void HandleCollision(Collider2D collision)
    {
        // 检查是否是玩家
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player == null) return;
        
        // 如果玩家处于无敌状态且不强制死亡，不触发死亡
        if (player.IsInvincible() && !ForceDeath) return;
        
        // 获取玩家的健康管理器
        PlayerHealthManager healthManager = player.GetComponent<PlayerHealthManager>();
        if (healthManager == null) return;
        
        // 触发玩家死亡事件
        HandlePlayerDeath(player, healthManager);
    }
    
    private void HandlePlayerDeath(PlayerController player, PlayerHealthManager healthManager)
    {
        // 这里可以添加死亡特效、声音等
        #if UNITY_EDITOR
        Debug.Log($"玩家触发死亡区域: {deathMessage}");
        #endif
        
        if (instantKill)
        {
            // 如果设置为不重生玩家，需要在调用KillPlayer前处理
            if (!respawnPlayer)
            {
                // 监听死亡事件，在死亡后阻止重生
                GameEvents.OnPlayerDied += PreventRespawn;
            }
            
            // 直接杀死玩家
            healthManager.KillPlayer();
            
            #if UNITY_EDITOR
            Debug.Log($"玩家死亡: {deathMessage}");
            #endif
        }
        else
        {
            // 对玩家造成伤害
            GameEvents.TriggerPlayerDamaged((int)damageAmount);
            
            #if UNITY_EDITOR
            Debug.Log($"玩家受到伤害: {damageAmount}");
            #endif
        }
    }
    
    // 用于阻止玩家重生的方法
    private void PreventRespawn()
    {
        // 取消订阅事件，防止多次调用
        GameEvents.OnPlayerDied -= PreventRespawn;
        
        // 阻止重生事件的默认处理
        GameEvents.OnPlayerRespawn -= FindObjectOfType<PlayerHealthManager>().RespawnPlayer;
        
        #if UNITY_EDITOR
        Debug.Log("已阻止玩家重生");
        #endif
        
        // 这里可以添加游戏结束逻辑
        // 例如：显示游戏结束界面、重新加载场景等
    }
    
    // 在编辑器中可视化死亡区域
    private void OnDrawGizmos()
    {
        if (!showDeadZone) return;
        
        // 如果这是子对象上的DeadZone，且父对象也有DeadZone组件，则跳过绘制
        // 这可以防止重复绘制
        if (transform.parent != null && transform.parent.GetComponent<DeadZone>() != null && 
            transform.parent.GetComponent<DeadZone>().useChildrenAsZones)
        {
            return;
        }
        
        // 设置颜色，确保透明度一致
        Gizmos.color = zoneColor;
        
        // 如果使用子对象作为检测区域
        if (useChildrenAsZones)
        {
            // 获取所有子对象的碰撞体
            Collider2D[] childColliders = GetComponentsInChildren<Collider2D>(true);
            
            // 过滤掉同时有DeadZone组件的子对象的碰撞体，防止重复绘制
            List<Collider2D> filteredColliders = new List<Collider2D>();
            foreach (Collider2D collider in childColliders)
            {
                // 如果碰撞体所在的游戏对象有DeadZone组件，且不是当前对象，则跳过
                if (collider.gameObject != gameObject && collider.GetComponent<DeadZone>() != null)
                {
                    continue;
                }
                filteredColliders.Add(collider);
            }
            
            // 绘制过滤后的碰撞体
            foreach (Collider2D collider in filteredColliders)
            {
                DrawColliderGizmo(collider);
                
                // 只为主要区域或独立的子区域绘制死亡标志
                if (collider.gameObject == gameObject || collider.GetComponent<DeadZone>() != null)
                {
                    DrawDeathSymbol(collider.transform.position);
                }
            }
        }
        else
        {
            // 只绘制自身的碰撞体
            Collider2D ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null)
            {
                DrawColliderGizmo(ownCollider);
            }
            else
            {
                // 如果没有碰撞体，绘制默认区域
                Gizmos.DrawSphere(transform.position, 1f);
            }
            
            // 绘制死亡标志
            DrawDeathSymbol(transform.position);
        }
    }
    
    // 绘制碰撞体的Gizmo
    private void DrawColliderGizmo(Collider2D collider)
    {
        // 确保使用一致的颜色
        Gizmos.color = zoneColor;
        
        // 根据碰撞体类型绘制不同形状
        if (collider is CircleCollider2D)
        {
            CircleCollider2D circleCollider = (CircleCollider2D)collider;
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                collider.transform.position,
                collider.transform.rotation,
                collider.transform.lossyScale
            );
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawSphere(circleCollider.offset, circleCollider.radius);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (collider is BoxCollider2D)
        {
            BoxCollider2D boxCollider = (BoxCollider2D)collider;
            // 考虑旋转
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                collider.transform.position, 
                collider.transform.rotation, 
                collider.transform.lossyScale
            );
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(boxCollider.offset, boxCollider.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (collider is PolygonCollider2D)
        {
            PolygonCollider2D polygonCollider = (PolygonCollider2D)collider;
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                collider.transform.position,
                collider.transform.rotation,
                collider.transform.lossyScale
            );
            Gizmos.matrix = rotationMatrix;
            
            // 绘制多边形
            for (int i = 0; i < polygonCollider.pathCount; i++)
            {
                Vector2[] points = polygonCollider.GetPath(i);
                for (int j = 0; j < points.Length; j++)
                {
                    Vector2 current = points[j] + polygonCollider.offset;
                    Vector2 next = points[(j + 1) % points.Length] + polygonCollider.offset;
                    Gizmos.DrawLine(current, next);
                }
            }
            
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (collider is EdgeCollider2D)
        {
            EdgeCollider2D edgeCollider = (EdgeCollider2D)collider;
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                collider.transform.position,
                collider.transform.rotation,
                collider.transform.lossyScale
            );
            Gizmos.matrix = rotationMatrix;
            
            // 绘制边缘
            Vector2[] points = edgeCollider.points;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 current = points[i] + edgeCollider.offset;
                Vector2 next = points[i + 1] + edgeCollider.offset;
                Gizmos.DrawLine(current, next);
            }
            
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
    
    // 绘制死亡标志
    private void DrawDeathSymbol(Vector3 position)
    {
        Gizmos.color = Color.red;
        float size = 0.3f;
        
        // 绘制十字
        Gizmos.DrawLine(position + new Vector3(-size, -size, 0), position + new Vector3(size, size, 0));
        Gizmos.DrawLine(position + new Vector3(-size, size, 0), position + new Vector3(size, -size, 0));
    }
    
    // 在运行时验证设置
    private void OnValidate()
    {
        // 确保所有碰撞体都设置为触发器
        if (Application.isPlaying)
        {
            foreach (Collider2D collider in zoneColliders)
            {
                if (collider != null && !collider.isTrigger)
                {
                    collider.isTrigger = true;
                }
            }
        }
    }
    
    // 在销毁时确保取消订阅事件
    private void OnDestroy()
    {
        if (!respawnPlayer)
        {
            GameEvents.OnPlayerDied -= PreventRespawn;
        }
    }
}

// 触发器转发器组件，用于将子对象的触发事件转发给父对象
[RequireComponent(typeof(Collider2D))]
public class TriggerForwarder : MonoBehaviour
{
    private DeadZone targetDeadZone;
    
    public void SetTarget(DeadZone target)
    {
        targetDeadZone = target;
    }
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (targetDeadZone != null)
        {
            targetDeadZone.OnForwardedTriggerEnter2D(collision);
        }
    }
}