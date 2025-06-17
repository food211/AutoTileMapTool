using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LockMechanism : MonoBehaviour, IMechanicAction
{
    [System.Serializable]
    public enum LockState
    {
        Locked,
        Unlocking,
        Unlocked
    }

    [Header("锁定设置")]
    [SerializeField] private int requiredUnlockers = 3;
    [SerializeField] private bool resetOnDeactivate = false;
    [SerializeField] private float unlockDelay = 1.0f;
    [SerializeField] private bool allowUnlockerDeactivation = true; // 允许解锁器取消激活

    [Header("视觉反馈")]
    [SerializeField] private SpriteRenderer lockSpriteRenderer;
    [SerializeField] private Sprite[] progressSprites; // 不同进度的锁的图片
    [SerializeField] private Animator lockAnimator; // 锁的动画控制器
    [SerializeField] private string progressAnimParam = "Progress"; // 进度参数 (0-1)

    [Header("音效")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip unlockStepSound;
    [SerializeField] private AudioClip unlockCompleteSound;
    [SerializeField] private AudioClip lockSound;
    [SerializeField] private AudioClip lockStepSound; // 解锁器取消激活时的音效

    [Header("事件")]
    public UnityEvent onUnlockStart;
    public UnityEvent onUnlockStep;
    public UnityEvent onUnlockComplete;
    public UnityEvent onResetLock;
    public UnityEvent onResetLockStep; // 解锁器取消激活时触发

    // 当前已解锁的数量
    private int currentUnlockCount = 0;
    private LockState currentState = LockState.Locked;
    private List<UnlockerTrigger> unlockerTriggers = new List<UnlockerTrigger>();
    private bool isProcessingUnlock = false;

    public bool IsActive { get; private set; }

    private void Start()
    {
        // 初始化
        UpdateVisuals();
    }

    /// <summary>
    /// 注册一个解锁器触发器
    /// </summary>
    public void RegisterUnlocker(Trigger unlockerTrigger)
    {
        if (unlockerTrigger is UnlockerTrigger unlockTrigger && !unlockerTriggers.Contains(unlockTrigger))
        {
            unlockerTriggers.Add(unlockTrigger);
        }
    }

    /// <summary>
    /// 当一个解锁器被激活时调用
    /// </summary>
    public void OnUnlockerActivated(Trigger unlockerTrigger)
    {
        if (currentState == LockState.Unlocked || isProcessingUnlock)
            return;

        // 增加解锁计数
        currentUnlockCount++;
        
        // 播放解锁步骤音效
        if (audioSource != null && unlockStepSound != null)
        {
            audioSource.PlayOneShot(unlockStepSound);
        }
        
        // 触发解锁步骤事件
        onUnlockStep?.Invoke();
        
        // 更新视觉效果
        UpdateVisuals();
        
        // 检查是否已全部解锁
        if (currentUnlockCount >= requiredUnlockers)
        {
            StartCoroutine(ProcessUnlock());
        }
    }
    
    /// <summary>
    /// 当一个解锁器被取消激活时调用
    /// </summary>
    public void OnUnlockerDeactivated(Trigger unlockerTrigger)
    {
        // 如果不允许解锁器取消激活，或者锁已经完全解锁，则忽略
        if (!allowUnlockerDeactivation || currentState == LockState.Unlocked || isProcessingUnlock)
            return;
            
        // 减少解锁计数
        if (currentUnlockCount > 0)
        {
            currentUnlockCount--;
            
            // 播放锁定步骤音效
            if (audioSource != null && lockStepSound != null)
            {
                audioSource.PlayOneShot(lockStepSound);
            }
            
            // 触发锁定步骤事件
            onResetLockStep?.Invoke();
            
            // 更新视觉效果
            UpdateVisuals();
            
            // 如果之前是解锁状态，现在变为锁定状态
            if (currentState == LockState.Unlocking)
            {
                currentState = LockState.Locked;
            }
        }
    }

    /// <summary>
    /// 处理解锁过程
    /// </summary>
    private IEnumerator ProcessUnlock()
    {
        isProcessingUnlock = true;
        currentState = LockState.Unlocking;
        
        // 触发解锁开始事件
        onUnlockStart?.Invoke();
        
        // 等待解锁延迟
        if (unlockDelay > 0)
        {
            yield return new WaitForSeconds(unlockDelay);
            
            // 再次检查是否仍然满足解锁条件（可能在延迟期间有解锁器被取消激活）
            if (currentUnlockCount < requiredUnlockers && allowUnlockerDeactivation)
            {
                isProcessingUnlock = false;
                currentState = LockState.Locked;
                yield break;
            }
        }
        
        // 完成解锁
        currentState = LockState.Unlocked;
        
        
        // 播放解锁完成音效
        if (audioSource != null && unlockCompleteSound != null)
        {
            audioSource.PlayOneShot(unlockCompleteSound);
        }
        
        // 触发解锁完成事件
        onUnlockComplete?.Invoke();
        
        // 更新视觉效果
        UpdateVisuals();
        
        isProcessingUnlock = false;
    }

    /// <summary>
    /// 更新锁的视觉效果
    /// </summary>
    private void UpdateVisuals()
    {
        // 计算解锁进度 (0-1)
        float progress = Mathf.Clamp01((float)currentUnlockCount / requiredUnlockers);
        
        // 更新进度精灵
        if (lockSpriteRenderer != null && progressSprites != null && progressSprites.Length > 0)
        {
            int spriteIndex = Mathf.FloorToInt(progress * (progressSprites.Length - 1));
            spriteIndex = Mathf.Clamp(spriteIndex, 0, progressSprites.Length - 1);
            lockSpriteRenderer.sprite = progressSprites[spriteIndex];
        }
        
        // 更新动画参数
        if (lockAnimator != null)
        {
            lockAnimator.SetFloat(progressAnimParam, progress);
        }
    }

    /// <summary>
    /// 重置锁状态
    /// </summary>
    public void ResetLock()
    {
        currentUnlockCount = 0;
        currentState = LockState.Locked;
        isProcessingUnlock = false;
        
        // 播放锁定音效
        if (audioSource != null && lockSound != null)
        {
            audioSource.PlayOneShot(lockSound);
        }
        
        // 触发锁定事件
        onResetLock?.Invoke();
        
        // 更新视觉效果
        UpdateVisuals();
        
        // 重置所有解锁器
        foreach (var unlocker in unlockerTriggers)
        {
            if (unlocker != null)
            {
                unlocker.ResetTrigger();
            }
        }
    }

    /// <summary>
    /// 获取当前解锁进度 (0-1)
    /// </summary>
    public float GetUnlockProgress()
    {
        return Mathf.Clamp01((float)currentUnlockCount / requiredUnlockers);
    }

    /// <summary>
    /// 获取当前锁状态
    /// </summary>
    public LockState GetLockState()
    {
        return currentState;
    }

    /// <summary>
    /// 设置所需的解锁器数量
    /// </summary>
    public void SetRequiredUnlockers(int count)
    {
        requiredUnlockers = Mathf.Max(1, count);
        UpdateVisuals();
    }

    #region IMechanicAction Implementation
    
    /// <summary>
    /// 激活锁机制
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        
        // 如果锁已经解锁，可以在此处触发相关事件
        if (currentState == LockState.Unlocked)
        {
            onUnlockComplete?.Invoke();
        }
    }
    
    /// <summary>
    /// 停用锁机制
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        
        // 如果需要重置锁状态
        if (resetOnDeactivate)
        {
            ResetLock();
        }
    }
    
    #endregion
}