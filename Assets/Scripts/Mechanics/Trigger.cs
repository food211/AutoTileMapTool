using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Trigger : MonoBehaviour
{
    [System.Serializable]
    public enum TriggerType
    {
        PlayerEnter,      // 玩家进入触发区域
        PlayerExit,       // 玩家离开触发区域
        PlayerStay,       // 玩家停留在触发区域
        ButtonPress,      // 按钮按下（可以是键盘按键或游戏内按钮）
        TimerBased,       // 基于时间的触发
        ExternalCall      // 由其他脚本调用触发
    }

    [Header("触发设置")]
    [SerializeField] private TriggerType triggerType = TriggerType.PlayerEnter;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode triggerKey = KeyCode.Z;
    [SerializeField] private float triggerDelay = 0f;
    [SerializeField] private bool oneTimeOnly = false;
    [SerializeField] private bool startActive = true;
    
    [Header("定时触发设置")]
    [SerializeField] private float initialDelay = 0f;     // 初始延迟时间
    [SerializeField] private float repeatInterval = 5f;   // 重复触发间隔
    [SerializeField] private bool repeatTrigger = true;   // 是否重复触发
    [SerializeField] private int maxRepeatCount = 0;      // 最大重复次数 (0 = 无限)
    
    [Header("目标接收器")]
    [SerializeField] private Reciever[] targetRecievers;
    
    // 事件系统，允许在编辑器中连接其他行为
    public UnityEvent onTriggerActivated;
    
    private bool hasTriggered = false;
    private bool isActive = false;
    private bool playerInTriggerArea = false;  // 跟踪玩家是否在触发区域内
    private int currentRepeatCount = 0;        // 当前重复次数
    private Coroutine timerCoroutine;          // 定时器协程引用
    
    private void Start()
    {
        isActive = startActive;
        
        // 如果是定时触发类型，启动定时器
        if (isActive && triggerType == TriggerType.TimerBased)
        {
            StartTimerTrigger();
        }
    }
    
    private void Update()
    {
        if (!isActive) return;
        
        // 检查按键触发 - 修改为需要玩家在触发区域内
        if (triggerType == TriggerType.ButtonPress && Input.GetKeyDown(triggerKey) && playerInTriggerArea)
        {
            ActivateTrigger();
        }

        // 这里可以添加其他类型的触发检测
    }
    
    private void OnEnable()
    {
        // 订阅交互事件
        if (triggerType == TriggerType.ButtonPress)
        {
            GameEvents.OnPlayerInteract += HandlePlayerInteract;
        }
    }
    
    private void OnDisable()
    {
        // 取消订阅交互事件
        if (triggerType == TriggerType.ButtonPress)
        {
            GameEvents.OnPlayerInteract -= HandlePlayerInteract;
        }
        
        // 停止定时器协程
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
    }
    
    // 处理玩家交互事件
    private void HandlePlayerInteract()
    {
        if (isActive && playerInTriggerArea && triggerType == TriggerType.ButtonPress)
        {
            ActivateTrigger();
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        
        if (other.CompareTag(playerTag))
        {
            // 更新玩家在触发区域内的状态
            playerInTriggerArea = true;
            
            // 如果是按钮类型的触发器，通知 GameEvents 玩家进入了交互区域
            if (triggerType == TriggerType.ButtonPress)
            {
                GameEvents.TriggerPlayerInInteractiveZoneChanged(true);
            }
            
            if (triggerType == TriggerType.PlayerEnter)
            {
                ActivateTrigger();
            }
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isActive) return;
        
        if (other.CompareTag(playerTag))
        {
            // 更新玩家离开触发区域的状态
            playerInTriggerArea = false;
            
            // 如果是按钮类型的触发器，通知 GameEvents 玩家离开了交互区域
            if (triggerType == TriggerType.ButtonPress)
            {
                GameEvents.TriggerPlayerInInteractiveZoneChanged(false);
            }
            
            if (triggerType == TriggerType.PlayerExit)
            {
                ActivateTrigger();
            }
        }
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive) return;
        
        if (triggerType == TriggerType.PlayerStay && other.CompareTag(playerTag))
        {
            ActivateTrigger();
        }
    }
    
    /// <summary>
    /// 激活触发器，通知所有关联的接收器
    /// </summary>
    public void ActivateTrigger()
    {
        if (oneTimeOnly && hasTriggered) return;
        
        if (triggerDelay > 0)
        {
            StartCoroutine(DelayedTrigger());
        }
        else
        {
            ExecuteTrigger();
        }
    }
    
    private IEnumerator DelayedTrigger()
    {
        yield return new WaitForSeconds(triggerDelay);
        ExecuteTrigger();
    }
    
    private void ExecuteTrigger()
    {
        hasTriggered = true;
        
        // 通知所有接收器
        foreach (var reciever in targetRecievers)
        {
            if (reciever != null)
            {
                reciever.OnTriggerActivated(this);
            }
        }
        
        // 调用事件
        onTriggerActivated?.Invoke();
    }
    
    /// <summary>
    /// 设置触发器的激活状态
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
        
        // 如果是定时触发类型，根据激活状态启动或停止定时器
        if (triggerType == TriggerType.TimerBased)
        {
            if (active)
            {
                StartTimerTrigger();
            }
            else if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }
        }
    }
    
    /// <summary>
    /// 重置触发器状态，允许再次触发
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
        currentRepeatCount = 0;
        
        // 如果是定时触发类型且当前处于激活状态，重新启动定时器
        if (isActive && triggerType == TriggerType.TimerBased)
        {
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
            }
            StartTimerTrigger();
        }
    }
    
    /// <summary>
    /// 启动定时触发器
    /// </summary>
    private void StartTimerTrigger()
    {
        if (triggerType == TriggerType.TimerBased)
        {
            // 停止已有的定时器协程
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
            }
            
            // 启动新的定时器协程
            timerCoroutine = StartCoroutine(TimerTriggerCoroutine());
        }
    }
    
    /// <summary>
    /// 定时触发器协程
    /// </summary>
    private IEnumerator TimerTriggerCoroutine()
    {
        // 初始延迟
        if (initialDelay > 0)
        {
            yield return new WaitForSeconds(initialDelay);
        }
        
        while (isActive)
        {
            // 执行触发
            ActivateTrigger();
            
            // 增加重复计数
            currentRepeatCount++;
            
            // 检查是否达到最大重复次数
            if (maxRepeatCount > 0 && currentRepeatCount >= maxRepeatCount)
            {
                break;
            }
            
            // 如果不需要重复触发，退出循环
            if (!repeatTrigger)
            {
                break;
            }
            
            // 等待重复间隔
            yield return new WaitForSeconds(repeatInterval);
        }
        
        timerCoroutine = null;
    }
    
    /// <summary>
    /// 外部调用触发 - 供其他脚本调用
    /// </summary>
    /// <param name="callerName">调用者名称，用于调试</param>
    public void ExternalActivate(string callerName = "")
    {
        if (triggerType == TriggerType.ExternalCall)
        {
            #if UNITY_EDITOR
            if (!string.IsNullOrEmpty(callerName))
            {
                Debug.LogFormat("触发器 {0} 被 {1} 外部调用", gameObject.name, callerName);
            }
            #endif
            
            ActivateTrigger();
        }
        #if UNITY_EDITOR
        else
        {
            Debug.LogWarningFormat("触发器 {0} 不是外部调用类型，无法通过外部调用激活", gameObject.name);
        }
        #endif
    }
    
    /// <summary>
    /// 获取触发器类型
    /// </summary>
    public TriggerType GetTriggerType()
    {
        return triggerType;
    }
    
    /// <summary>
    /// 获取触发器是否已经触发过
    /// </summary>
    public bool HasTriggered()
    {
        return hasTriggered;
    }
}