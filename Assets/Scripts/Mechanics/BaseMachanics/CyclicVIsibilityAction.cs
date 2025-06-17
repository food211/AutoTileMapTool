using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 使物体定期循环出现和消失的行为组件
/// </summary>
public class CyclicVisibilityAction : MonoBehaviour, IMechanicAction
{
    [Header("循环设置")]
    [SerializeField] private float visibleDuration = 2f;    // 可见持续时间
    [SerializeField] private float invisibleDuration = 3f;  // 不可见持续时间
    [SerializeField] private float initialDelay = 0f;       // 初始延迟
    [SerializeField] private bool startVisible = true;      // 初始状态是否可见
    
    [Header("过渡效果")]
    [SerializeField] private bool useFade = false;          // 是否使用淡入淡出效果
    [SerializeField] private float fadeTime = 0.5f;         // 淡入淡出时间
    
    [Header("目标组件")]
    [SerializeField] private GameObject[] targetObjects;    // 要控制的目标物体
    [SerializeField] private bool affectColliders = true;   // 是否影响碰撞体
    
    // 内部变量
    private bool isActive = false;
    private Coroutine cycleRoutine = null;
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();
    private List<Collider2D> targetColliders = new List<Collider2D>();
    
    private void Awake()
    {
        // 收集所有渲染器的原始颜色
        if (useFade)
        {
            foreach (var obj in targetObjects)
            {
                if (obj == null) continue;
                
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer is SpriteRenderer spriteRenderer)
                    {
                        originalColors[renderer] = new Color[] { spriteRenderer.color };
                    }
                    else if (renderer.materials.Length > 0)
                    {
                        Color[] colors = new Color[renderer.materials.Length];
                        for (int i = 0; i < renderer.materials.Length; i++)
                        {
                            colors[i] = renderer.materials[i].color;
                        }
                        originalColors[renderer] = colors;
                    }
                }
            }
        }
        
        // 收集所有碰撞体
        if (affectColliders)
        {
            foreach (var obj in targetObjects)
            {
                if (obj == null) continue;
                
                Collider2D[] colliders = obj.GetComponentsInChildren<Collider2D>();
                targetColliders.AddRange(colliders);
            }
        }
        
        // 设置初始状态
        SetObjectsVisibility(startVisible);
    }
    
    public void Activate()
    {
        if (isActive) return;
        
        isActive = true;
        
        // 停止之前的协程（如果有）
        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
        }
        
        // 启动新的循环协程
        cycleRoutine = StartCoroutine(CycleVisibilityRoutine());
    }
    
    public void Deactivate()
    {
        if (!isActive) return;
        
        isActive = false;
        
        // 停止循环协程
        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }
        
        // 重置所有物体为可见
        SetObjectsVisibility(true);
    }
    
    public bool IsActive => isActive;
    
    private IEnumerator CycleVisibilityRoutine()
    {
        // 初始延迟
        if (initialDelay > 0)
        {
            yield return new WaitForSeconds(initialDelay);
        }
        
        bool isVisible = startVisible;
        
        while (isActive)
        {
            // 设置物体可见性
            if (useFade)
            {
                yield return StartCoroutine(FadeObjects(isVisible));
            }
            else
            {
                SetObjectsVisibility(isVisible);
            }
            
            // 等待相应的持续时间
            float waitTime = isVisible ? visibleDuration : invisibleDuration;
            yield return new WaitForSeconds(waitTime);
            
            // 切换状态
            isVisible = !isVisible;
        }
    }
    
    private void SetObjectsVisibility(bool visible)
    {
        foreach (var obj in targetObjects)
        {
            if (obj == null) continue;
            
            // 设置游戏对象活动状态
            obj.SetActive(visible);
        }
        
        // 如果只影响碰撞体而不是整个游戏对象
        if (affectColliders && !visible)
        {
            foreach (var collider in targetColliders)
            {
                if (collider != null)
                {
                    collider.enabled = visible;
                }
            }
        }
    }
    
    private IEnumerator FadeObjects(bool fadeIn)
    {
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        float elapsedTime = 0f;
        
        // 如果是淡入，先确保物体是激活的
        if (fadeIn)
        {
            foreach (var obj in targetObjects)
            {
                if (obj != null && !obj.activeSelf)
                {
                    obj.SetActive(true);
                }
            }
        }
        
        // 执行淡入淡出
        while (elapsedTime < fadeTime)
        {
            float normalizedTime = elapsedTime / fadeTime;
            float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, normalizedTime);
            
            // 应用透明度到所有渲染器
            foreach (var rendererEntry in originalColors)
            {
                Renderer renderer = rendererEntry.Key;
                Color[] originalColorArray = rendererEntry.Value;
                
                if (renderer is SpriteRenderer spriteRenderer)
                {
                    Color color = spriteRenderer.color;
                    color.a = currentAlpha;
                    spriteRenderer.color = color;
                }
                else
                {
                    for (int i = 0; i < renderer.materials.Length && i < originalColorArray.Length; i++)
                    {
                        Color color = renderer.materials[i].color;
                        color.a = currentAlpha;
                        renderer.materials[i].color = color;
                    }
                }
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 确保最终值是精确的
        float finalAlpha = endAlpha;
        foreach (var rendererEntry in originalColors)
        {
            Renderer renderer = rendererEntry.Key;
            Color[] originalColorArray = rendererEntry.Value;
            
            if (renderer is SpriteRenderer spriteRenderer)
            {
                Color color = spriteRenderer.color;
                color.a = finalAlpha;
                spriteRenderer.color = color;
            }
            else
            {
                for (int i = 0; i < renderer.materials.Length && i < originalColorArray.Length; i++)
                {
                    Color color = renderer.materials[i].color;
                    color.a = finalAlpha;
                    renderer.materials[i].color = color;
                }
            }
        }
        
        // 如果是淡出，最后禁用物体
        if (!fadeIn)
        {
            foreach (var obj in targetObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }
    
    // 在编辑器中可视化循环时间
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        
        // 绘制一个表示循环时间的圆
        float totalCycleTime = visibleDuration + invisibleDuration;
        float radius = 1f;
        Vector3 center = transform.position + Vector3.up * 2f;
        
        // 绘制完整循环
        DrawGizmosCircle(center, radius, 32);
        
        // 绘制可见部分
        float visiblePortion = visibleDuration / totalCycleTime;
        DrawGizmosArc(center, radius, 0, visiblePortion * 360f, 32, Color.green);
        
        // 绘制不可见部分
        DrawGizmosArc(center, radius, visiblePortion * 360f, 360f, 32, Color.red);
        
        // 添加标签
        UnityEditor.Handles.Label(center + Vector3.up * radius * 1.2f, 
            $"循环: {totalCycleTime}s (可见: {visibleDuration}s, 不可见: {invisibleDuration}s)");
    }
    
    private void DrawGizmosCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
    
    private void DrawGizmosArc(Vector3 center, float radius, float startAngle, float endAngle, int segments, Color color)
    {
        Color originalColor = Gizmos.color;
        Gizmos.color = color;
        
        float angleRange = endAngle - startAngle;
        float angleStep = angleRange / segments;
        Vector3 prevPoint = center + new Vector3(
            Mathf.Cos(startAngle * Mathf.Deg2Rad) * radius, 
            Mathf.Sin(startAngle * Mathf.Deg2Rad) * radius, 
            0
        );
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = startAngle + i * angleStep;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius, 
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius, 
                0
            );
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
        
        Gizmos.color = originalColor;
    }
}