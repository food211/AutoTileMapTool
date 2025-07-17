using UnityEngine;

public class VisualizeCircle : MonoBehaviour
{
    [Tooltip("圆形的半径")]
    public float radius = 0.25f;
    
    [Tooltip("圆形的颜色")]
    public Color circleColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
    
    [Tooltip("是否总是显示名字")]
    public bool alwaysShowText = true;
    
    [Tooltip("圆形的线段数量 - 越高越平滑")]
    [Range(8, 64)]
    public int segments = 32;
    
    [Tooltip("是否填充圆形")]
    public bool fillCircle = false;
    
    [Tooltip("填充的透明度")]
    [Range(0f, 1f)]
    public float fillAlpha = 0.2f;
    
    [Tooltip("文字显示的偏移位置")]
    public Vector3 textOffset = new Vector3(0, 0.5f, 0);
    
    [Tooltip("文字的颜色")]
    public Color textColor = Color.white;

    // 在Scene视图中绘制圆形和ID
    private void OnDrawGizmos()
    {
        DrawCircle();
        
        // 如果设置了总是显示文字，则显示物体名称
        if (alwaysShowText)
        {
            DrawText();
        }
    }
    
    // 当物体被选中时，可以使用不同颜色
    private void OnDrawGizmosSelected()
    {
        // 保存原始颜色
        Color originalColor = circleColor;
        
        // 选中时使用更亮的颜色
        circleColor = new Color(1f, 1f, 0f, 0.8f);
        
        DrawCircle();
        
        // 无论是否设置了总是显示文字，当被选中时都显示物体名称
        DrawText();
        
        // 恢复原始颜色
        circleColor = originalColor;
    }
    
    // 绘制圆形 - 修改为2D游戏适用的XY平面
    private void DrawCircle()
    {
        // 设置Gizmos的颜色
        Gizmos.color = circleColor;
        
        // 绘制圆形
        Vector3 position = transform.position;
        
        // 如果需要填充圆形
        if (fillCircle)
        {
            Color fillColor = circleColor;
            fillColor.a = fillAlpha;
            Gizmos.color = fillColor;
            
            // 绘制填充的圆形（通过绘制多边形来模拟）
            Vector3 prevPoint = position + new Vector3(radius, 0, 0);
            float angleStep = 2f * Mathf.PI / segments;
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep;
                // 修改为XY平面 (x,y,z=0)
                Vector3 point = position + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                
                // 绘制三角形
                Gizmos.DrawLine(position, prevPoint);
                Gizmos.DrawLine(position, point);
                Gizmos.DrawLine(prevPoint, point);
                
                prevPoint = point;
            }
            
            // 恢复原始颜色用于绘制轮廓
            Gizmos.color = circleColor;
        }
        
        // 绘制圆形轮廓
        float step = 2f * Mathf.PI / segments;
        
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * step;
            float angle2 = (i + 1) * step;
            
            // 修改为XY平面 (x,y,z=0)
            Vector3 point1 = position + new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0);
            Vector3 point2 = position + new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0);
            
            Gizmos.DrawLine(point1, point2);
        }
        
        // 绘制十字线
        Gizmos.DrawLine(position + new Vector3(-radius, 0, 0), position + new Vector3(radius, 0, 0));
        Gizmos.DrawLine(position + new Vector3(0, -radius, 0), position + new Vector3(0, radius, 0));
    }
    
    // 绘制物体名称
    private void DrawText()
    {
        // 使用Unity的Handles.Label来绘制文本
        // 但由于Handles命名空间在运行时不可用，所以我们需要使用Gizmos.DrawIcon或在编辑器脚本中实现
        // 这里我们使用GUIStyle和UnityEditor命名空间
        #if UNITY_EDITOR
        UnityEditor.Handles.color = textColor;
        
        GUIStyle style = new GUIStyle();
        style.normal.textColor = textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;
        
        // 在圆形上方显示物体名称
        UnityEditor.Handles.Label(transform.position + textOffset, gameObject.name, style);
        #endif
    }
}