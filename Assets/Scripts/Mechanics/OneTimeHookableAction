using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 可被钩中一次后自动计时禁用的物体，冷却期间不可见，冷却结束后自动恢复
/// </summary>
public class OneTimeHookableAction : MonoBehaviour, IMechanicAction
{
    [Header("钩中设置")]
    [SerializeField] private float disableDuration = 3f;       // 禁用持续时间（变色后隐藏）
    [SerializeField] private float cooldownTime = 5f;          // 冷却时间（隐藏后多久才能再次出现）
    
    [Header("视觉效果")]
    [SerializeField] private bool useVisualFeedback = true;    // 是否使用视觉反馈
    [SerializeField] private Color disablingColor = new Color(1f, 0.5f, 0.5f, 0.7f); // 禁用过程中的颜色
    [SerializeField] private float fadeTime = 0.5f;            // 颜色过渡时间
    
    [Header("事件")]
    public UnityEvent onHooked;                               // 被钩中时触发
    public UnityEvent onDisabled;                             // 被禁用时触发
    public UnityEvent onEnabled;                              // 被启用时触发
    
    // 内部变量
    private bool isActive = false;
    private bool isDisabled = false;
    private bool isInCooldown = false;
    private Coroutine disableRoutine = null;
    private Coroutine cooldownRoutine = null;
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();
    private List<Collider2D> objectColliders = new List<Collider2D>();
    
    private void Awake()
    {
        // 收集所有渲染器的原始颜色
        if (useVisualFeedback)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
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
        
        // 收集所有碰撞体
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        objectColliders.AddRange(colliders);
    }
    
    private void OnEnable()
    {
        // 订阅绳索钩中事件
        GameEvents.OnRopeHooked += CheckIfHooked;
    }
    
    private void OnDisable()
    {
        // 取消订阅绳索钩中事件
        GameEvents.OnRopeHooked -= CheckIfHooked;
    }
    
    public void Activate()
    {
        if (isActive) return;
        
        isActive = true;
        
        // 如果当前处于禁用或冷却状态，不立即恢复
        // 让计时器自然结束
        if (!isDisabled && !isInCooldown)
        {
            // 确保物体可见且碰撞体启用
            gameObject.SetActive(true);
            foreach (var collider in objectColliders)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }
            
            // 恢复原始颜色
            if (useVisualFeedback)
            {
                RestoreOriginalColors();
            }
        }
    }
    
    public void Deactivate()
    {
        if (!isActive) return;
        
        isActive = false;
        
        // 停止所有协程
        if (disableRoutine != null)
        {
            StopCoroutine(disableRoutine);
            disableRoutine = null;
        }
        
        if (cooldownRoutine != null)
        {
            StopCoroutine(cooldownRoutine);
            cooldownRoutine = null;
        }
        
        // 重置状态
        isDisabled = false;
        isInCooldown = false;
        
        // 恢复原始颜色
        if (useVisualFeedback)
        {
            RestoreOriginalColors();
        }
        
        // 确保物体可见
        gameObject.SetActive(true);
        
        // 启用所有碰撞体
        foreach (var collider in objectColliders)
        {
            if (collider != null)
            {
                collider.enabled = true;
            }
        }
    }
    
    public bool IsActive => isActive;
    
    /// <summary>
    /// 检查是否被绳索钩中
    /// </summary>
    private void CheckIfHooked(Vector2 hookPosition)
    {
        if (!isActive || isDisabled || isInCooldown) return;
        
        // 检查钩点是否在碰撞体内
        foreach (var collider in objectColliders)
        {
            if (collider != null && collider.enabled)
            {
                if (collider.OverlapPoint(hookPosition))
                {
                    // 被钩中了
                    HandleHooked();
                    break;
                }
            }
        }
    }
    
    /// <summary>
    /// 处理被钩中的逻辑
    /// </summary>
    private void HandleHooked()
    {
        // 触发钩中事件
        onHooked?.Invoke();
        
        // 开始禁用过程
        StartDisablingProcess();
    }
    
    /// <summary>
    /// 开始禁用过程
    /// </summary>
    private void StartDisablingProcess()
    {
        // 防止重复启动
        if (isDisabled || disableRoutine != null) return;
        
        isDisabled = true;
        
        // 应用禁用颜色
        if (useVisualFeedback)
        {
            StartCoroutine(FadeToColor(disablingColor));
        }
        
        // 启动禁用计时器
        disableRoutine = StartCoroutine(DisableTimer());
    }
    
    /// <summary>
    /// 禁用计时器
    /// </summary>
    private IEnumerator DisableTimer()
    {
        // 等待颜色过渡完成
        yield return new WaitForSeconds(fadeTime);
        
        // 等待禁用持续时间
        yield return new WaitForSeconds(disableDuration - fadeTime);
        
        // 禁用物体
        DisableObject();
        
        disableRoutine = null;
    }
    
    /// <summary>
    /// 禁用物体
    /// </summary>
    private void DisableObject()
    {
        // 禁用所有碰撞体
        foreach (var collider in objectColliders)
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
        
        // 隐藏物体
        gameObject.SetActive(false);
        
        // 触发禁用事件
        onDisabled?.Invoke();
        
        // 进入冷却状态
        isDisabled = false;
        isInCooldown = true;
        
        // 启动冷却计时器
        cooldownRoutine = StartCoroutine(CooldownTimer());
    }
    
    /// <summary>
    /// 冷却计时器
    /// </summary>
    private IEnumerator CooldownTimer()
    {
        // 冷却期间物体保持隐藏状态
        yield return new WaitForSeconds(cooldownTime);
        
        // 冷却结束，恢复物体
        EnableObject();
        
        cooldownRoutine = null;
    }
    
    /// <summary>
    /// 启用物体
    /// </summary>
    private void EnableObject()
    {
        isInCooldown = false;
        
        // 只有在组件仍处于活动状态时才恢复物体
        if (isActive)
        {
            // 显示物体
            gameObject.SetActive(true);
            
            // 启用所有碰撞体
            foreach (var collider in objectColliders)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }
            
            // 恢复原始颜色
            if (useVisualFeedback)
            {
                RestoreOriginalColors();
            }
            
            // 触发启用事件
            onEnabled?.Invoke();
        }
    }
    
    /// <summary>
    /// 颜色渐变到指定颜色
    /// </summary>
    private IEnumerator FadeToColor(Color targetColor)
    {
        float elapsedTime = 0f;
        Dictionary<Renderer, Color[]> startColors = new Dictionary<Renderer, Color[]>();
        
        // 保存当前颜色
        foreach (var rendererEntry in originalColors)
        {
            Renderer renderer = rendererEntry.Key;
            
            if (renderer is SpriteRenderer spriteRenderer)
            {
                startColors[renderer] = new Color[] { spriteRenderer.color };
            }
            else
            {
                Color[] colors = new Color[renderer.materials.Length];
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    colors[i] = renderer.materials[i].color;
                }
                startColors[renderer] = colors;
            }
        }
        
        // 执行渐变
        while (elapsedTime < fadeTime)
        {
            float normalizedTime = elapsedTime / fadeTime;
            
            foreach (var rendererEntry in originalColors)
            {
                Renderer renderer = rendererEntry.Key;
                Color[] startColorArray = startColors[renderer];
                
                if (renderer is SpriteRenderer spriteRenderer)
                {
                    Color startColor = startColorArray[0];
                    spriteRenderer.color = Color.Lerp(startColor, targetColor, normalizedTime);
                }
                else
                {
                    for (int i = 0; i < renderer.materials.Length && i < startColorArray.Length; i++)
                    {
                        Color startColor = startColorArray[i];
                        renderer.materials[i].color = Color.Lerp(startColor, targetColor, normalizedTime);
                    }
                }
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 确保最终颜色是精确的
        foreach (var rendererEntry in originalColors)
        {
            Renderer renderer = rendererEntry.Key;
            
            if (renderer is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.color = targetColor;
            }
            else
            {
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    renderer.materials[i].color = targetColor;
                }
            }
        }
    }
    
    /// <summary>
    /// 立即恢复原始颜色
    /// </summary>
    private void RestoreOriginalColors()
    {
        foreach (var rendererEntry in originalColors)
        {
            Renderer renderer = rendererEntry.Key;
            Color[] originalColorArray = rendererEntry.Value;
            
            if (renderer is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.color = originalColorArray[0];
            }
            else
            {
                for (int i = 0; i < renderer.materials.Length && i < originalColorArray.Length; i++)
                {
                    renderer.materials[i].color = originalColorArray[i];
                }
            }
        }
    }
    
    // 在编辑器中可视化禁用和冷却时间
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = disablingColor;
        
        // 绘制一个表示禁用时间的圆
        float radius = 1f;
        Vector3 center = transform.position + Vector3.up * 2f;
        
        // 绘制完整循环
        DrawGizmosCircle(center, radius, 32);
        
        // 绘制禁用部分
        float totalTime = disableDuration + cooldownTime;
        float disablePortion = disableDuration / totalTime;
        DrawGizmosArc(center, radius, 0, disablePortion * 360f, 32, disablingColor);
        
        // 绘制冷却部分
        DrawGizmosArc(center, radius, disablePortion * 360f, 360f, 32, Color.gray);
        
        // 添加标签
        UnityEditor.Handles.Label(center + Vector3.up * radius * 1.2f, 
            $"禁用: {disableDuration}s (变色后隐藏), 冷却: {cooldownTime}s (隐藏)");
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