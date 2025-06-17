using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnlockerTrigger : Trigger
{
    [Header("解锁器设置")]
    [SerializeField] private LockMechanism targetLock;
    [SerializeField] private bool registerOnStart = true;
    [SerializeField] private bool deactivateOnReset = true; // 重置时是否取消激活
    
    [Header("视觉反馈")]
    [SerializeField] private SpriteRenderer unlockerSpriteRenderer;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Animator unlockerAnimator;
    [SerializeField] private string activateAnimTrigger = "Activate";
    [SerializeField] private string deactivateAnimTrigger = "Deactivate";
    
    [Header("交互提示")]
    [SerializeField] private GameObject interactionPrompt; // 交互提示对象
    [SerializeField] private float promptOffset = 1.0f; // 提示在Y轴上的偏移
    
    [Header("音效")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip activateSound;
    [SerializeField] private AudioClip deactivateSound;
    
    private bool isActivated = false;
    
    protected override void Start()
    {
        base.Start();
        
        // 注册到目标锁
        if (registerOnStart && targetLock != null)
        {
            targetLock.RegisterUnlocker(this);
        }
        
        // 初始化视觉状态
        UpdateVisuals();
        
        // 初始化交互提示
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
        
        // 订阅玩家进入/离开交互区域事件
        onPlayerEnterInteractionZone.AddListener(ShowInteractionPrompt);
        onPlayerExitInteractionZone.AddListener(HideInteractionPrompt);
    }
    
    protected override void OnDisable()
    {
        // 取消订阅事件
        onPlayerEnterInteractionZone.RemoveListener(ShowInteractionPrompt);
        onPlayerExitInteractionZone.RemoveListener(HideInteractionPrompt);
    }
    
    /// <summary>
    /// 显示交互提示
    /// </summary>
    private void ShowInteractionPrompt()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(true);
            
            // 设置提示位置
            if (promptOffset != 0)
            {
                interactionPrompt.transform.position = transform.position + Vector3.up * promptOffset;
            }
        }
    }
    
    /// <summary>
    /// 隐藏交互提示
    /// </summary>
    private void HideInteractionPrompt()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }
    
    /// <summary>
    /// 设置目标锁
    /// </summary>
    public void SetTargetLock(LockMechanism lockMechanism)
    {
        targetLock = lockMechanism;
        if (targetLock != null)
        {
            targetLock.RegisterUnlocker(this);
        }
    }
    
    /// <summary>
    /// 重写触发器激活方法
    /// </summary>
    public override void ActivateTrigger()
    {
        base.ActivateTrigger();
        
        // 切换激活状态
        ToggleActivation();
    }
    
    /// <summary>
    /// 切换解锁器的激活状态
    /// </summary>
    public void ToggleActivation()
    {
        if (isActivated)
        {
            DeactivateUnlocker();
        }
        else
        {
            ActivateUnlocker();
        }
    }
    
    /// <summary>
    /// 激活解锁器
    /// </summary>
    public void ActivateUnlocker()
    {
        if (isActivated) return;
        
        isActivated = true;
        
        // 通知锁机制
        if (targetLock != null)
        {
            targetLock.OnUnlockerActivated(this);
        }
        
        // 更新视觉效果
        UpdateVisuals();
        
        // 播放激活动画
        if (unlockerAnimator != null)
        {
            unlockerAnimator.SetTrigger(activateAnimTrigger);
        }
        
        // 播放激活音效
        if (audioSource != null && activateSound != null)
        {
            audioSource.PlayOneShot(activateSound);
        }
    }
    
    /// <summary>
    /// 取消激活解锁器
    /// </summary>
    public void DeactivateUnlocker()
    {
        if (!isActivated) return;
        
        isActivated = false;
        
        // 通知锁机制
        if (targetLock != null)
        {
            targetLock.OnUnlockerDeactivated(this);
        }
        
        // 更新视觉效果
        UpdateVisuals();
        
        // 播放取消激活动画
        if (unlockerAnimator != null)
        {
            unlockerAnimator.SetTrigger(deactivateAnimTrigger);
        }
        
        // 播放取消激活音效
        if (audioSource != null && deactivateSound != null)
        {
            audioSource.PlayOneShot(deactivateSound);
        }
    }
    
    /// <summary>
    /// 重置解锁器状态
    /// </summary>
    public override void ResetTrigger()
    {
        base.ResetTrigger();
        
        // 如果配置为重置时取消激活
        if (deactivateOnReset && isActivated)
        {
            DeactivateUnlocker();
        }
    }
    
    /// <summary>
    /// 更新解锁器的视觉效果
    /// </summary>
    private void UpdateVisuals()
    {
        if (unlockerSpriteRenderer != null)
        {
            unlockerSpriteRenderer.sprite = isActivated ? activeSprite : inactiveSprite;
        }
    }
    
    /// <summary>
    /// 获取解锁器是否已激活
    /// </summary>
    public bool IsUnlockerActivated()
    {
        return isActivated;
    }
    
    /// <summary>
    /// 设置解锁器的激活状态
    /// </summary>
    public void SetUnlockerActivated(bool activated)
    {
        if (activated != isActivated)
        {
            if (activated)
            {
                ActivateUnlocker();
            }
            else
            {
                DeactivateUnlocker();
            }
        }
    }
}