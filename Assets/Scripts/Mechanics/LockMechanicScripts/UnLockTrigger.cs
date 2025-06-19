using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnlockerTrigger : Trigger
{
    [Header("调试设置")]
    [SerializeField] private bool debugMode = false; // 是否启用调试模式
    
    [Header("解锁器设置")]
    [SerializeField] private LockMechanism targetLock;
    [SerializeField] private bool registerOnStart = true;
    [SerializeField] private bool deactivateOnReset = true; // 重置时是否取消激活
    [SerializeField] private bool disableWhenLockUnlocked = true; // 锁解开后是否禁用交互
    
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
    
    // 预定义调试字符串，避免运行时字符串连接导致的GC
    private const string DEBUG_HANDLE_INTERACT = "UnlockerTrigger.HandlePlayerInteract - 交互类型: {0}, isActive: {1}, playerInTriggerArea: {2}, 匹配交互类型: {3}, GameObject: {4}";
    private const string DEBUG_LOCK_UNLOCKED = "UnlockerTrigger.HandlePlayerInteract - 锁已解开，忽略交互";
    private const string DEBUG_CONDITION_MET = "UnlockerTrigger.HandlePlayerInteract - 条件满足，调用ToggleActivation(), 当前激活状态: {0}";
    private const string DEBUG_ACTIVATE_TRIGGER = "UnlockerTrigger.ActivateTrigger - 锁已解开，忽略触发";
    private const string DEBUG_TOGGLE_ACTIVATION = "UnlockerTrigger.ToggleActivation - 锁已解开，忽略激活切换";
    private const string DEBUG_ACTIVATE_UNLOCKER = "UnlockerTrigger.ActivateUnlocker - 锁已解开，忽略激活";
    private const string DEBUG_NOTIFY_LOCK = "UnlockerTrigger.ActivateUnlocker - 通知锁机制解锁器已激活";
    private const string DEBUG_TRIGGER_ENTER = "UnlockerTrigger.OnTriggerEnter2D - 玩家进入解锁器 {0} 的交互区域, playerInTriggerArea: {1}";
    private const string DEBUG_TRIGGER_EXIT = "UnlockerTrigger.OnTriggerExit2D - 玩家离开解锁器 {0} 的交互区域, playerInTriggerArea: {1}";
    
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
    
    protected override void OnEnable()
    {
        base.OnEnable();
        // 订阅玩家交互事件
        GameEvents.OnPlayerInteract += HandlePlayerInteract;
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        // 取消订阅事件
        onPlayerEnterInteractionZone.RemoveListener(ShowInteractionPrompt);
        onPlayerExitInteractionZone.RemoveListener(HideInteractionPrompt);
        GameEvents.OnPlayerInteract -= HandlePlayerInteract;
    }

    // 处理玩家交互事件，返回是否处理了交互
    protected override bool HandlePlayerInteract(GameEvents.InteractionType interactionType)
    {
        // 添加详细调试信息
        if (debugMode)
        {
            Debug.LogFormat(DEBUG_HANDLE_INTERACT,
                interactionType,
                isActive,
                playerInTriggerArea,
                interactionType == GameEvents.InteractionType.Environmental,
                gameObject.name);
        }

        // 检查锁是否已经解开
        bool lockIsUnlocked = false;
        if (targetLock != null && disableWhenLockUnlocked)
        {
            lockIsUnlocked = targetLock.GetLockState() == LockMechanism.LockState.Unlocked;
            
            if (debugMode && lockIsUnlocked)
            {
                Debug.Log(DEBUG_LOCK_UNLOCKED);
            }
        }

        // 解锁器只响应Environmental类型的交互，并检查优先级
        // 增加条件：锁未解开或不禁用已解锁的解锁器
        if (isActive && playerInTriggerArea && 
            interactionType == GameEvents.InteractionType.Environmental && 
            !lockIsUnlocked)
        {
            if (debugMode)
            {
                Debug.LogFormat(DEBUG_CONDITION_MET, isActivated);
            }

            ToggleActivation();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 显示交互提示
    /// </summary>
    private void ShowInteractionPrompt()
    {
        // 检查锁是否已经解开，如果已解开且设置为禁用，则不显示提示
        if (disableWhenLockUnlocked && targetLock != null && 
            targetLock.GetLockState() == LockMechanism.LockState.Unlocked)
        {
            return;
        }
        
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
        // 检查锁是否已经解开
        if (disableWhenLockUnlocked && targetLock != null && 
            targetLock.GetLockState() == LockMechanism.LockState.Unlocked)
        {
            if (debugMode)
            {
                Debug.Log(DEBUG_ACTIVATE_TRIGGER);
            }
            return;
        }
        
        base.ActivateTrigger();
        
        // 切换激活状态
        ToggleActivation();
    }
    
    /// <summary>
    /// 切换解锁器的激活状态
    /// </summary>
    public void ToggleActivation()
    {
        // 检查锁是否已经解开
        if (disableWhenLockUnlocked && targetLock != null && 
            targetLock.GetLockState() == LockMechanism.LockState.Unlocked)
        {
            if (debugMode)
            {
                Debug.Log(DEBUG_TOGGLE_ACTIVATION);
            }
            return;
        }
        
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
        
        // 检查锁是否已经解开
        if (disableWhenLockUnlocked && targetLock != null && 
            targetLock.GetLockState() == LockMechanism.LockState.Unlocked)
        {
            if (debugMode)
            {
                Debug.Log(DEBUG_ACTIVATE_UNLOCKER);
            }
            return;
        }
        
        isActivated = true;
        
        // 通知锁机制
        if (targetLock != null)
        {
            if (debugMode)
            {
                Debug.Log(DEBUG_NOTIFY_LOCK);
            }
            
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
    /// 检查玩家是否在触发区域内并更新触发区域状态
    /// </summary>
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        base.OnTriggerEnter2D(other);
        
        if (debugMode && other.CompareTag(playerTag))
        {
            Debug.LogFormat(DEBUG_TRIGGER_ENTER, gameObject.name, playerInTriggerArea);
        }
    }

    protected override void OnTriggerExit2D(Collider2D other)
    {
        base.OnTriggerExit2D(other);
        
        if (debugMode && other.CompareTag(playerTag))
        {
            Debug.LogFormat(DEBUG_TRIGGER_EXIT, gameObject.name, playerInTriggerArea);
        }
    }
}