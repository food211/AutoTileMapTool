using UnityEngine;

/// <summary>
/// 标记关卡起始点的脚本
/// </summary>
public class StartPoint : MonoBehaviour
{
    [Header("基本设置")]
    [Tooltip("起始点唯一ID")]
    [SerializeField] private string startPointID;
    
    [Tooltip("是否为默认起始点（玩家首次进入场景时使用）")]
    [SerializeField] private bool isDefaultStartPoint = false;

    [Header("可视化设置")]
    [Tooltip("是否在编辑器中显示可视化")]
    [SerializeField] private bool showVisuals = true;
    
    [Tooltip("可视化颜色")]
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 0.2f, 0.7f);
    
    [Tooltip("可视化大小")]
    [SerializeField] private float gizmoSize = 1.0f;

    /// <summary>
    /// 获取起始点ID
    /// </summary>
    public string StartPointID => string.IsNullOrEmpty(startPointID) ? name : startPointID;

    /// <summary>
    /// 是否为默认起始点
    /// </summary>
    public bool IsDefaultStartPoint => isDefaultStartPoint;
    
    private void Awake()
    {
        // 如果没有设置ID，使用对象名称作为ID
        if (string.IsNullOrEmpty(startPointID))
        {
            startPointID = name;
        }
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (showVisuals)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, gizmoSize);
            
            // 绘制坐标轴
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + transform.right * gizmoSize);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * gizmoSize);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * gizmoSize);
            
            // 如果是默认起始点，绘制特殊标记
            if (isDefaultStartPoint)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, gizmoSize * 1.2f);
            }
        }
    }
    #endif
}