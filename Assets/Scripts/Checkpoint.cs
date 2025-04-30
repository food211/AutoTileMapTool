using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 单个存档点的行为
/// </summary>
public class Checkpoint : MonoBehaviour
{
    // 激活区域形状枚举
    public enum ActivationAreaShape
    {
        Circle,
        Rectangle
    }

    [Header("存档点设置")]
    [SerializeField] private string checkpointID; // 存档点唯一ID
    [SerializeField] private bool isActive = false;
    [SerializeField] public bool HealOnActivate = true; // 是否在激活时恢复生命值
    [SerializeField] private Transform respawnPoint; // 重生点位置
    
    [Header("激活区域设置")]
    [SerializeField] private ActivationAreaShape areaShape = ActivationAreaShape.Circle; // 激活区域形状
    [SerializeField] private float activationRadius = 2.0f; // 圆形激活区域半径
    [SerializeField] private Vector2 activationSize = new Vector2(4.0f, 4.0f); // 矩形激活区域大小
    [SerializeField] public Vector2 activationOffset = Vector2.zero; // 激活区域偏移量
    
    [Header("视觉效果")]
    [SerializeField] private GameObject activeVisual; // 激活时的视觉效果
    [SerializeField] private GameObject inactiveVisual; // 未激活时的视觉效果
    [SerializeField] private ParticleSystem activationParticle; // 激活时的粒子效果
    [SerializeField] private Light pointLight; // 可选的点光源
    [SerializeField] private Color activationAreaColor = new Color(0.2f, 0.8f, 0.2f, 0.3f); // 激活区域颜色
    [SerializeField] private Color respawnPointColor = new Color(0.2f, 0.2f, 0.8f, 0.7f); // 重生点颜色
    
    // 激活区域游戏对象
    private GameObject activationArea;
    
    // 公开属性
    public string CheckpointID => string.IsNullOrEmpty(checkpointID) ? name : checkpointID;
    public Transform RespawnPoint => respawnPoint != null ? respawnPoint : transform;
    
    private void Awake()
    {
        // 确保存档点有唯一ID
        if (string.IsNullOrEmpty(checkpointID))
        {
            // 使用游戏对象名称作为ID
            checkpointID = name;
        }
        
        // 如果没有设置重生点，使用自身位置
        if (respawnPoint == null)
        {
            respawnPoint = transform;
        }
        
        // 创建激活区域
        CreateActivationArea();
        
        // 初始化视觉效果
        UpdateVisuals();
    }
    
    private void CreateActivationArea()
    {
        // 创建激活区域游戏对象
        activationArea = new GameObject(name + "_ActivationArea");
        activationArea.transform.SetParent(transform);
        activationArea.transform.localPosition = new Vector3(activationOffset.x, activationOffset.y, 0);
        
        // 根据选择的形状添加不同的碰撞器
        if (areaShape == ActivationAreaShape.Circle)
        {
            CircleCollider2D collider = activationArea.AddComponent<CircleCollider2D>();
            collider.radius = activationRadius;
            collider.isTrigger = true;
        }
        else // Rectangle
        {
            BoxCollider2D collider = activationArea.AddComponent<BoxCollider2D>();
            collider.size = activationSize;
            collider.isTrigger = true;
        }
        
        // 添加激活区域脚本
        CheckpointActivationArea activationAreaScript = activationArea.AddComponent<CheckpointActivationArea>();
        activationAreaScript.SetCheckpoint(this);
    }
    
    /// <summary>
    /// 激活存档点
    /// </summary>
    public void Activate()
    {
        if (isActive) return; // 已经激活，不重复处理
        
        isActive = true;
        
        // 更新视觉效果
        UpdateVisuals();
        
        // 播放激活粒子效果
        if (activationParticle != null)
        {
            activationParticle.Play();
        }
        
        // 可以在这里添加更多激活效果，如屏幕闪烁、相机震动等
        GameEvents.TriggerCameraShake(0.3f);
    }
    
    /// <summary>
    /// 停用存档点
    /// </summary>
    public void Deactivate()
    {
        if (!isActive) return; // 已经停用，不重复处理
        
        isActive = false;
        
        // 更新视觉效果
        UpdateVisuals();
    }
    
    /// <summary>
    /// 更新视觉效果
    /// </summary>
    private void UpdateVisuals()
    {
        // 激活/停用相应的视觉效果
        if (activeVisual != null)
            activeVisual.SetActive(isActive);
            
        if (inactiveVisual != null)
            inactiveVisual.SetActive(!isActive);
            
        // 更新点光源
        if (pointLight != null)
        {
            pointLight.enabled = isActive;
            
            // 如果激活，可以设置更亮的光
            if (isActive)
            {
                pointLight.intensity = 1.5f;
                pointLight.color = Color.cyan; // 或其他醒目颜色
            }
            else
            {
                pointLight.intensity = 0.5f;
                pointLight.color = Color.gray;
            }
        }
    }
    
    /// <summary>
    /// 检查存档点是否激活
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }
    
    /// <summary>
    /// 更新激活区域形状和大小
    /// </summary>
    public void UpdateActivationArea()
    {
        if (activationArea == null)
            return;
            
        // 更新位置
        activationArea.transform.localPosition = new Vector3(activationOffset.x, activationOffset.y, 0);
        
        // 移除现有的碰撞器
        Collider2D existingCollider = activationArea.GetComponent<Collider2D>();
        if (existingCollider != null)
        {
            Destroy(existingCollider);
        }
        
        // 添加新的碰撞器
        if (areaShape == ActivationAreaShape.Circle)
        {
            CircleCollider2D collider = activationArea.AddComponent<CircleCollider2D>();
            collider.radius = activationRadius;
            collider.isTrigger = true;
        }
        else // Rectangle
        {
            BoxCollider2D collider = activationArea.AddComponent<BoxCollider2D>();
            collider.size = activationSize;
            collider.isTrigger = true;
        }
    }
    
#if UNITY_EDITOR
    // 在编辑器中绘制可视化辅助
    private void OnDrawGizmos()
    {
        // 绘制激活区域
        Gizmos.color = activationAreaColor;
        Vector3 areaPosition = transform.position + new Vector3(activationOffset.x, activationOffset.y, 0);
        
        if (areaShape == ActivationAreaShape.Circle)
        {
            Gizmos.DrawSphere(areaPosition, activationRadius);
        }
        else // Rectangle
        {
            Gizmos.DrawCube(areaPosition, new Vector3(activationSize.x, activationSize.y, 0.1f));
        }
        
        // 绘制重生点
        if (respawnPoint != null && respawnPoint != transform)
        {
            Gizmos.color = respawnPointColor;
            Gizmos.DrawSphere(respawnPoint.position, 0.5f);
            
            // 绘制从存档点到重生点的连线
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, respawnPoint.position);
        }
        else
        {
            // 如果重生点就是存档点自身，绘制一个小图标
            Gizmos.color = respawnPointColor;
            Gizmos.DrawSphere(transform.position, 0.3f);
        }
    }
    
    // 在选中时绘制更详细的信息
    private void OnDrawGizmosSelected()
    {
        // 绘制激活区域
        Vector3 areaPosition = transform.position + new Vector3(activationOffset.x, activationOffset.y, 0);
        Gizmos.color = new Color(activationAreaColor.r, activationAreaColor.g, activationAreaColor.b, activationAreaColor.a + 0.2f);
        
        if (areaShape == ActivationAreaShape.Circle)
        {
            Gizmos.DrawSphere(areaPosition, activationRadius);
            
            // 绘制圆形轮廓
            Handles.color = new Color(1f, 1f, 1f, 0.8f);
            Handles.DrawWireDisc(areaPosition, Vector3.forward, activationRadius);
        }
        else // Rectangle
        {
            Gizmos.DrawCube(areaPosition, new Vector3(activationSize.x, activationSize.y, 0.1f));
            
            // 绘制矩形轮廓
            Handles.color = new Color(1f, 1f, 1f, 0.8f);
            Vector3 halfSize = new Vector3(activationSize.x / 2, activationSize.y / 2, 0);
            Vector3[] corners = new Vector3[4]
            {
                areaPosition + new Vector3(-halfSize.x, -halfSize.y, 0),
                areaPosition + new Vector3(halfSize.x, -halfSize.y, 0),
                areaPosition + new Vector3(halfSize.x, halfSize.y, 0),
                areaPosition + new Vector3(-halfSize.x, halfSize.y, 0)
            };
            
            Handles.DrawLine(corners[0], corners[1]);
            Handles.DrawLine(corners[1], corners[2]);
            Handles.DrawLine(corners[2], corners[3]);
            Handles.DrawLine(corners[3], corners[0]);
        }
        
        // 绘制重生点
        if (respawnPoint != null && respawnPoint != transform)
        {
            Gizmos.color = new Color(respawnPointColor.r, respawnPointColor.g, respawnPointColor.b, respawnPointColor.a + 0.2f);
            Gizmos.DrawSphere(respawnPoint.position, 0.6f);
            
            // 绘制从存档点到重生点的连线
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, respawnPoint.position);
            
            // 在重生点位置绘制坐标轴
            Handles.color = Color.red;
            Handles.ArrowHandleCap(0, respawnPoint.position, Quaternion.LookRotation(Vector3.right), 1f, EventType.Repaint);
            Handles.color = Color.green;
            Handles.ArrowHandleCap(0, respawnPoint.position, Quaternion.LookRotation(Vector3.up), 1f, EventType.Repaint);
        }
        
        // 绘制存档点ID
        Handles.Label(transform.position + Vector3.up * 1.5f, "ID: " + CheckpointID);
    }
    
    // 当Inspector中的值发生变化时调用
    private void OnValidate()
    {
        // 确保激活半径为正数
        activationRadius = Mathf.Max(0.1f, activationRadius);
        
        // 确保激活区域大小为正数
        activationSize = new Vector2(
            Mathf.Max(0.1f, activationSize.x),
            Mathf.Max(0.1f, activationSize.y)
        );
        
        // 如果在运行时修改，更新激活区域
        if (Application.isPlaying && activationArea != null)
        {
            UpdateActivationArea();
        }
    }
#endif
}

/// <summary>
/// 存档点激活区域脚本 - 处理与玩家的碰撞检测
/// </summary>
public class CheckpointActivationArea : MonoBehaviour
{
    private Checkpoint checkpoint;
    
    public void SetCheckpoint(Checkpoint cp)
    {
        checkpoint = cp;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 检查是否是玩家
        if (other.CompareTag("Player") && checkpoint != null && !checkpoint.IsActive())
        {
            // 触发存档点激活事件
            GameEvents.TriggerCheckpointActivated(checkpoint.transform);
        }
    }
}