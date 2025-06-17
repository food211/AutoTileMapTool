using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Animator))]
public class LockAnimationController : MonoBehaviour
{
    [Header("动画设置")]
    [SerializeField] private string progressParameterName = "Progress"; // Animator中的进度参数名称
    [SerializeField] private string lightCountParameterName = "LightCount"; // 亮起的灯数量参数
    
    [Header("灯光设置")]
    [SerializeField] private int totalLights = 3; // 锁上的灯总数
    [SerializeField] private bool useDiscreteSteps = true; // 是否使用离散步骤（而不是连续过渡）
    
    [Header("闪烁效果设置")]
    [SerializeField] private float blinkSpeed = 2.0f;
    [SerializeField] private float blinkIntensity = 0.3f;
    [SerializeField] private bool useBlinkEffect = true;
    
    [Header("解锁行为")]
    [SerializeField] private MonoBehaviour[] actionComponents; // 解锁后要激活的组件
    [SerializeField] private float unlockActionDelay = 0.5f; // 解锁后执行动作的延迟时间
    
    [Header("引用")]
    [SerializeField] private LockMechanism lockMechanism;
    [SerializeField] private SpriteRenderer lockSpriteRenderer;
    
    [Header("事件")]
    public UnityEvent onAllLightsOn; // 当所有灯亮起时触发
    public UnityEvent onLightChange; // 当灯光状态变化时触发
    
    private Animator animator;
    private float lastProgress = -1f;
    private int lastLightCount = -1;
    private bool allLightsTriggered = false;
    private List<IMechanicAction> mechanicActions = new List<IMechanicAction>();
    private Coroutine unlockActionCoroutine;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        
        // 如果没有指定锁机制，尝试在同一对象上查找
        if (lockMechanism == null)
        {
            lockMechanism = GetComponent<LockMechanism>();
        }
        
        // 如果没有指定Sprite渲染器，尝试在同一对象上查找
        if (lockSpriteRenderer == null)
        {
            lockSpriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        // 收集所有实现了IMechanicAction接口的组件
        foreach (var component in actionComponents)
        {
            if (component is IMechanicAction action)
            {
                mechanicActions.Add(action);
            }
        }
    }
    
    private void Update()
    {
        if (lockMechanism == null)
            return;
        
        // 获取当前解锁进度
        float progress = lockMechanism.GetUnlockProgress();
        
        // 计算当前亮起的灯数量
        int currentLightCount = Mathf.FloorToInt(progress * totalLights);
        currentLightCount = Mathf.Clamp(currentLightCount, 0, totalLights);
        
        // 如果进度变化，更新动画参数
        if (Mathf.Abs(progress - lastProgress) > 0.01f || currentLightCount != lastLightCount)
        {
            // 触发灯光变化事件
            onLightChange?.Invoke();
            
            // 如果灯光数量减少（解锁器取消激活），取消正在进行的解锁动作
            if (currentLightCount < lastLightCount && unlockActionCoroutine != null)
            {
                StopCoroutine(unlockActionCoroutine);
                unlockActionCoroutine = null;
                allLightsTriggered = false;
            }
            
            lastProgress = progress;
            lastLightCount = currentLightCount;
            
            if (animator != null)
            {
                // 更新连续进度参数
                animator.SetFloat(progressParameterName, progress);
                
                // 更新离散灯光数量参数
                if (useDiscreteSteps)
                {
                    animator.SetInteger(lightCountParameterName, currentLightCount);
                }
                
                // 检查是否所有灯都亮了
                if (currentLightCount >= totalLights && !allLightsTriggered)
                {
                    allLightsTriggered = true;
                    
                    // 延迟执行解锁动作
                    if (unlockActionDelay > 0)
                    {
                        unlockActionCoroutine = StartCoroutine(DelayedUnlockAction());
                    }
                    else
                    {
                        ExecuteUnlockActions();
                    }
                }
            }
        }
        
        // 应用闪烁效果（如果启用）
        if (useBlinkEffect && lockSpriteRenderer != null && progress > 0 && progress < 1.0f)
        {
            float blinkValue = Mathf.Sin(Time.time * blinkSpeed) * blinkIntensity + (1 - blinkIntensity);
            lockSpriteRenderer.color = new Color(1f, 1f, 1f, blinkValue);
        }
        else if (lockSpriteRenderer != null)
        {
            lockSpriteRenderer.color = Color.white;
        }
    }
    
    /// <summary>
    /// 延迟执行解锁动作的协程
    /// </summary>
    private IEnumerator DelayedUnlockAction()
    {
        yield return new WaitForSeconds(unlockActionDelay);
        ExecuteUnlockActions();
        unlockActionCoroutine = null;
    }
    
    /// <summary>
    /// 执行解锁后的动作
    /// </summary>
    private void ExecuteUnlockActions()
    {
        // 激活所有机关动作
        foreach (var action in mechanicActions)
        {
            action.Activate();
        }
        
        // 触发事件
        onAllLightsOn?.Invoke();
    }
    
    /// <summary>
    /// 重置锁状态
    /// </summary>
    public void ResetLock()
    {
        allLightsTriggered = false;
        
        if (unlockActionCoroutine != null)
        {
            StopCoroutine(unlockActionCoroutine);
            unlockActionCoroutine = null;
        }
        
        if (animator != null)
        {
            animator.SetFloat(progressParameterName, 0);
            animator.SetInteger(lightCountParameterName, 0);
        }
        
        if (lockSpriteRenderer != null)
        {
            lockSpriteRenderer.color = Color.white;
        }
    }
    
    /// <summary>
    /// 添加解锁后要激活的动作组件
    /// </summary>
    public void AddActionComponent(MonoBehaviour component)
    {
        if (component is IMechanicAction action)
        {
            if (!mechanicActions.Contains(action))
            {
                // 添加到数组
                List<MonoBehaviour> componentsList = new List<MonoBehaviour>(actionComponents);
                componentsList.Add(component);
                actionComponents = componentsList.ToArray();
                
                // 添加到动作列表
                mechanicActions.Add(action);
            }
        }
    }
}