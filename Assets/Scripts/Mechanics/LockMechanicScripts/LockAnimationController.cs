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
    
    [Header("引用")]
    [SerializeField] private LockMechanism lockMechanism;
    [SerializeField] private SpriteRenderer lockSpriteRenderer;
    
    private Animator animator;
    private float lastProgress = -1f;
    private int lastLightCount = -1;
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
            // 如果灯光数量减少（解锁器取消激活），取消正在进行的解锁动作
            if (currentLightCount < lastLightCount && unlockActionCoroutine != null)
            {
                StopCoroutine(unlockActionCoroutine);
                unlockActionCoroutine = null;
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
            }
        }
    }
}