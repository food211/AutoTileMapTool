using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡终点，用于处理玩家进入下一个关卡
/// </summary>
public class Endpoint : MonoBehaviour, ISaveable
{
    [Header("基本设置")]
    [Tooltip("终点唯一ID")]
    [SerializeField] private string endpointID;
    
    [Tooltip("是否可用")]
    [SerializeField] private bool isEnabled = true;
    
    [Header("目标设置")]
    [Tooltip("目标场景名称")]
    [SerializeField] private string targetSceneName;
    
    [Tooltip("目标起始点ID")]
    [SerializeField] private string targetStartPointID;
    
    [Tooltip("过渡延迟时间")]
    [SerializeField] private float transitionDelay = 1.5f;

    [Header("触发设置")]
    [Tooltip("进入终点需要的触发条件")]
    [SerializeField] private EndpointTriggerType triggerType = EndpointTriggerType.PlayerEnter;
    
    [Header("视觉效果")]
    [Tooltip("是否在编辑器中显示可视化")]
    [SerializeField] private bool showVisuals = true;
    
    [Tooltip("可视化颜色")]
    [SerializeField] private Color gizmoColor = new Color(0.8f, 0.2f, 0.2f, 0.7f);
    
    [Tooltip("可视化大小")]
    [SerializeField] private float gizmoSize = 1.0f;
    
    [Tooltip("玩家进入终点时是否播放特效")]
    [SerializeField] private bool playEffectsOnEnter = true;
    
    [Tooltip("终点特效预制体")]
    [SerializeField] private GameObject endpointEffectPrefab;
    
    [Header("音频")]
    [Tooltip("玩家进入终点时是否播放音效")]
    [SerializeField] private bool playSoundOnEnter = true;
    
    [Tooltip("终点音效")]
    [SerializeField] private AudioClip endpointSound;
    
    // 是否已经触发
    private bool isTriggered = false;
    
    // 是否已经使用过
    private bool isUsed = false;
    
    /// <summary>
    /// 获取终点ID
    /// </summary>
    public string EndpointID => string.IsNullOrEmpty(endpointID) ? name : endpointID;
    
    /// <summary>
    /// 获取目标场景名称
    /// </summary>
    public string TargetSceneName => targetSceneName;
    
    /// <summary>
    /// 获取目标起始点ID
    /// </summary>
    public string TargetStartPointID => targetStartPointID;
    
    /// <summary>
    /// 获取过渡延迟时间
    /// </summary>
    public float TransitionDelay => transitionDelay;
    
    /// <summary>
    /// 是否可用
    /// </summary>
    public bool IsEnabled 
    { 
        get => isEnabled; 
        set => isEnabled = value; 
    }
    
    /// <summary>
    /// 是否已经使用过
    /// </summary>
    public bool IsUsed
    {
        get => isUsed;
        set => isUsed = value;
    }
    
    private void Awake()
    {
        // 如果没有设置ID，使用对象名称作为ID
        if (string.IsNullOrEmpty(endpointID))
        {
            endpointID = name;
        }
    }
    
    private void Start()
    {
        // 在Start中加载状态，确保所有组件都已初始化
        LoadEndpointState();
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 如果触发类型是PlayerEnter，且碰撞体是玩家，且终点可用
        if (triggerType == EndpointTriggerType.PlayerEnter && 
            other.CompareTag("Player") && 
            !isTriggered && 
            isEnabled)
        {
            TriggerEndpoint();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        isTriggered = false;
    }
    
    /// <summary>
    /// 手动触发终点
    /// </summary>
    public void ManualTrigger()
    {
        if (triggerType == EndpointTriggerType.Manual && !isTriggered && isEnabled)
        {
            TriggerEndpoint();
        }
    }
    
    /// <summary>
    /// 设置目标场景和起始点
    /// </summary>
    public void SetTarget(string sceneName, string startPointID)
    {
        targetSceneName = sceneName;
        targetStartPointID = startPointID;
    }
    
    /// <summary>
    /// 触发终点逻辑
    /// </summary>
    private void TriggerEndpoint()
    {
        if (isTriggered || !isEnabled) return;
        
        isTriggered = true;
        isUsed = true;
        
        // 播放特效
        if (playEffectsOnEnter && endpointEffectPrefab != null)
        {
            Instantiate(endpointEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // 播放音效
        if (playSoundOnEnter && endpointSound != null)
        {
            AudioSource.PlayClipAtPoint(endpointSound, transform.position);
        }
        
        // 保存状态
        SaveEndpointState();
        
        // 触发终点事件 - 通知LevelManager处理场景切换
        GameEvents.TriggerEndpointReached(this.transform);
        
        #if UNITY_EDITOR
        Debug.Log($"终点 {name} 已触发，目标场景: {targetSceneName}，目标起始点: {targetStartPointID}");
        #endif
    }
    
    /// <summary>
    /// 保存终点状态
    /// </summary>
    private void SaveEndpointState()
    {
        if (ProgressManager.Instance != null)
        {
            // 使用新的保存系统
            ProgressManager.Instance.SaveObject(this);
        }
    }
    
    /// <summary>
    /// 加载终点状态
    /// </summary>
    private void LoadEndpointState()
    {
        // 状态将通过ISaveable接口的Load方法自动加载
    }
    
    #region ISaveable Implementation
    
    /// <summary>
    /// 获取对象的唯一ID
    /// </summary>
    public string GetUniqueID()
    {
        return "Endpoint_" + EndpointID;
    }
    
    /// <summary>
    /// 保存对象状态
    /// </summary>
    public SaveData Save()
    {
        SaveData data = new SaveData();
        data.objectType = "Endpoint";
        data.boolValue = isUsed;
        data.stringValue = $"{targetSceneName}|{targetStartPointID}";
        return data;
    }
    
    /// <summary>
    /// 加载对象状态
    /// </summary>
    public void Load(SaveData data)
    {
        if (data != null && data.objectType == "Endpoint")
        {
            isUsed = data.boolValue;
            
            // 如果有目标信息，解析它
            if (!string.IsNullOrEmpty(data.stringValue))
            {
                string[] parts = data.stringValue.Split('|');
                if (parts.Length >= 2)
                {
                    // 只有在未设置目标的情况下才加载保存的目标
                    if (string.IsNullOrEmpty(targetSceneName))
                    {
                        targetSceneName = parts[0];
                    }
                    
                    if (string.IsNullOrEmpty(targetStartPointID))
                    {
                        targetStartPointID = parts[1];
                    }
                }
            }
        }
    }
    
    #endregion
    
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (showVisuals)
        {
            // 根据是否启用设置颜色
            Gizmos.color = isEnabled ? gizmoColor : Color.gray;
            Gizmos.DrawSphere(transform.position, gizmoSize);
            
            // 绘制坐标轴
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + transform.right * gizmoSize);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * gizmoSize);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * gizmoSize);
            
            // 如果设置了目标，绘制连接线
            if (!string.IsNullOrEmpty(targetSceneName) && !string.IsNullOrEmpty(targetStartPointID))
            {
                Gizmos.color = Color.yellow;
                Vector3 direction = transform.up * 2f;
                Gizmos.DrawLine(transform.position, transform.position + direction);
                
                // 绘制箭头
                Vector3 arrowPos = transform.position + direction;
                float arrowSize = gizmoSize * 0.5f;
                Gizmos.DrawLine(arrowPos, arrowPos + Quaternion.Euler(0, 0, 135) * -direction.normalized * arrowSize);
                Gizmos.DrawLine(arrowPos, arrowPos + Quaternion.Euler(0, 0, -135) * -direction.normalized * arrowSize);
                
                // 显示目标文本
                UnityEditor.Handles.Label(arrowPos + Vector3.up * 0.5f, $"→ {targetSceneName}:{targetStartPointID}");
            }
        }
    }
    #endif
}

/// <summary>
/// 终点触发类型
/// </summary>
public enum EndpointTriggerType
{
    /// <summary>
    /// 玩家进入触发
    /// </summary>
    PlayerEnter,

    /// <summary>
    /// 手动触发（通过脚本调用）
    /// </summary>
    Manual
}
