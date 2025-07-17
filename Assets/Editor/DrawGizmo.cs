using UnityEngine;

public class DrawGizmo: MonoBehaviour
{
    /// <summary>
    /// 在场景视图中为物体绘制一个圆形并显示ID
    /// </summary>
    public class VisualizeCircle : MonoBehaviour
    {
        [Tooltip("圆形的半径")]
        public float radius = 1.0f;
        
        [Tooltip("圆形的颜色")]
        public Color circleColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
        
        [Tooltip("物体的唯一ID")]
        public string objectID = "ID_001";
        
        [Tooltip("ID文本的颜色")]
        public Color textColor = Color.white;
        
        [Tooltip("ID文本的大小")]
        public float textSize = 12f;
        
        [Tooltip("是否总是显示ID文本")]
        public bool alwaysShowText = true;
        
        [Tooltip("圆形的线段数量 - 越高越平滑")]
        [Range(8, 64)]
        public int segments = 32;
        
        [Tooltip("是否填充圆形")]
        public bool fillCircle = false;
        
        [Tooltip("填充的透明度")]
        [Range(0f, 1f)]
        public float fillAlpha = 0.2f;

        // 在Scene视图中绘制圆形和ID
        private void OnDrawGizmos()
        {
            DrawCircle();
            DrawID();
        }
        
        // 当物体被选中时，可以使用不同颜色
        private void OnDrawGizmosSelected()
        {
            // 保存原始颜色
            Color originalColor = circleColor;
            
            // 选中时使用更亮的颜色
            circleColor = new Color(1f, 1f, 0f, 0.8f);
            
            DrawCircle();
            DrawID();
            
            // 恢复原始颜色
            circleColor = originalColor;
        }
        
        // 绘制圆形
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
                    Vector3 point = position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    
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
                
                Vector3 point1 = position + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 point2 = position + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);
                
                Gizmos.DrawLine(point1, point2);
            }
        }
        
        // 绘制ID文本
        private void DrawID()
        {
            if (string.IsNullOrEmpty(objectID) || (!alwaysShowText && !UnityEditor.Selection.Contains(gameObject.GetInstanceID())))
                return;
                
            // 使用GUIStyle来设置文本样式
            GUIStyle style = new GUIStyle();
            style.normal.textColor = textColor;
            style.fontSize = (int)textSize;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            
            // 在场景视图中绘制文本
            UnityEditor.Handles.BeginGUI();
            
            // 将3D位置转换为屏幕位置
            Vector3? screenPosNullable = UnityEditor.SceneView.currentDrawingSceneView?.camera.WorldToScreenPoint(transform.position + Vector3.up * radius * 0.5f);

            if (screenPosNullable.HasValue && screenPosNullable.Value.z > 0) // 确保物体在相机前方
            {
                Vector3 screenPos = screenPosNullable.Value;
                Vector2 guiPosition = new Vector2(screenPos.x, UnityEditor.SceneView.currentDrawingSceneView.camera.pixelHeight - screenPos.y);
                UnityEditor.Handles.Label(guiPosition, objectID, style);
            }
            
            UnityEditor.Handles.EndGUI();
        }
    }
}