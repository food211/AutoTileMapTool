using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Trigger : MonoBehaviour, ISaveable
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
    [SerializeField] public string playerTag = "Player";
    [SerializeField] private float triggerDelay = 0f;
    [SerializeField] private bool oneTimeOnly = false;
    [SerializeField] private bool startActive = false;
    
    [Header("碰撞器设置")]
    [SerializeField] private bool useInteractionCollider = true; // 是否使用交互碰撞器
    [SerializeField] private Collider2D interactionCollider; // 交互碰撞器引用
    [SerializeField] private Collider2D physicsCollider; // 物理碰撞器引用
    [SerializeField] private bool showInteractionRange = true; // 是否在编辑器中显示交互范围
    
    [Header("定时触发设置")]
    [SerializeField] private float initialDelay = 0f;     // 初始延迟时间
    [SerializeField] private float repeatInterval = 5f;   // 重复触发间隔
    [SerializeField] private bool repeatTrigger = true;   // 是否重复触发
    [SerializeField] private int maxRepeatCount = 0;      // 最大重复次数 (0 = 无限)
    
    [Header("目标接收器")]
    [SerializeField] private Reciever[] targetRecievers;

    [Header("交互设置")]
    [SerializeField] protected GameEvents.InteractionType interactionType = GameEvents.InteractionType.Environmental; // 触发器的交互类型
    
    [Header("保存设置")]
    [SerializeField] protected string uniqueID; // 触发器的唯一ID，用于保存/加载
    
    // 事件系统，允许在编辑器中连接其他行为
    public UnityEvent onTriggerActivated;
    public UnityEvent onPlayerEnterInteractionZone; // 当玩家进入交互区域时
    public UnityEvent onPlayerExitInteractionZone; // 当玩家离开交互区域时
    
    protected bool hasTriggered = false;
    protected bool isActive = false;
    protected bool playerInTriggerArea = false;  // 跟踪玩家是否在触发区域内
    protected int currentRepeatCount = 0;        // 当前重复次数
    protected Coroutine timerCoroutine;          // 定时器协程引用
    
    protected virtual void Awake()
    {
        // 初始化碰撞器
        SetupColliders();
        
        // 如果没有设置唯一ID，自动生成一个
        if (string.IsNullOrEmpty(uniqueID))
        {
            uniqueID = $"Trigger_{gameObject.scene.name}_{transform.position.x}_{transform.position.y}_{GetInstanceID()}";
        }
    }
    
    protected virtual void Start()
    {
        isActive = startActive;
        
        // 如果是定时触发类型，启动定时器
        if (isActive && triggerType == TriggerType.TimerBased)
        {
            StartTimerTrigger();
        }
        
        // 加载保存的状态
        LoadSavedState();
    }

    /// <summary>
    /// 加载保存的状态
    /// </summary>
    protected virtual void LoadSavedState()
    {
        // 只有在对象是动态创建的，或者需要立即加载状态的情况下才手动调用
        // 通常情况下，ProgressManager 会在场景加载时自动处理所有 ISaveable 对象
        if (ProgressManager.Instance != null && gameObject.scene.name == "")
        {
            ProgressManager.Instance.LoadObject(this);
        }
    }
    
    /// <summary>
    /// 设置碰撞器
    /// </summary>
    protected virtual void SetupColliders()
    {
        // 如果没有指定交互碰撞器，尝试查找
        if (useInteractionCollider && interactionCollider == null)
        {
            // 首先尝试查找圆形碰撞器
            interactionCollider = GetComponent<CircleCollider2D>();
            
            // 如果没有找到圆形碰撞器，查找任何触发器碰撞器
            if (interactionCollider == null)
            {
                Collider2D[] colliders = GetComponents<Collider2D>();
                foreach (var collider in colliders)
                {
                    if (collider.isTrigger)
                    {
                        interactionCollider = collider;
                        break;
                    }
                }
            }
            
            // 如果仍然没有找到，创建一个新的圆形碰撞器
            if (interactionCollider == null && useInteractionCollider)
            {
                CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
                circleCollider.isTrigger = true;
                circleCollider.radius = 1.5f; // 默认交互半径
                interactionCollider = circleCollider;
            }
        }
        
        // 确保交互碰撞器是触发器
        if (interactionCollider != null)
        {
            interactionCollider.isTrigger = true;
        }
        
        // 如果没有指定物理碰撞器，尝试查找
        if (physicsCollider == null)
        {
            Collider2D[] colliders = GetComponents<Collider2D>();
            foreach (var collider in colliders)
            {
                if (!collider.isTrigger && collider != interactionCollider)
                {
                    physicsCollider = collider;
                    break;
                }
            }
        }
    }
    
    protected virtual void Update()
    {
        if (!isActive) return;
    }
    
    protected virtual void OnEnable()
    {
        // 订阅交互事件
        if (triggerType == TriggerType.ButtonPress)
        {
            GameEvents.OnPlayerInteract += HandlePlayerInteract;
        }
    }
    
    protected virtual void OnDisable()
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
        
        // 取消事件订阅（如果有必要）
        if (onPlayerEnterInteractionZone != null)
            onPlayerEnterInteractionZone.RemoveAllListeners();
            
        if (onPlayerExitInteractionZone != null)
            onPlayerExitInteractionZone.RemoveAllListeners();
    }

    // 处理玩家交互事件
    protected virtual bool HandlePlayerInteract(GameEvents.InteractionType interactionType)
    {
        // 检查传入的交互类型是否与此触发器的交互类型匹配
        if (isActive && playerInTriggerArea && interactionType == this.interactionType)
        {
            ActivateTrigger();
            return true;
        }
        return false;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        // 确保是交互碰撞器触发的事件
        if (interactionCollider != null && other.IsTouching(interactionCollider) && other.CompareTag(playerTag))
        {
            // 更新玩家在触发区域内的状态
            playerInTriggerArea = true;

            // 触发玩家进入交互区域事件
            onPlayerEnterInteractionZone?.Invoke();

            // 如果是按钮类型的触发器，通知 GameEvents 玩家进入了交互区域，并传递交互类型
            if (triggerType == TriggerType.ButtonPress)
            {
                GameEvents.TriggerPlayerInInteractiveZoneChanged(true, interactionType);
            }

            if (triggerType == TriggerType.PlayerEnter)
            {
                ActivateTrigger();
            }
        }
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (!isActive) return;

        // 确保是交互碰撞器触发的事件
        if (interactionCollider != null && other.CompareTag(playerTag))
        {
            // 更新玩家离开触发区域的状态
            playerInTriggerArea = false;

            // 触发玩家离开交互区域事件
            onPlayerExitInteractionZone?.Invoke();

            // 如果是按钮类型的触发器，通知 GameEvents 玩家离开了交互区域
            if (triggerType == TriggerType.ButtonPress)
            {
                // 离开时使用默认的Item类型，表示恢复到默认交互状态
                GameEvents.TriggerPlayerInInteractiveZoneChanged(false, GameEvents.InteractionType.Item);
            }

            if (triggerType == TriggerType.PlayerExit)
            {
                ActivateTrigger();
            }
        }
    }
    
    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive) return;
        
        // 确保是交互碰撞器触发的事件
        if (interactionCollider != null && other.IsTouching(interactionCollider) && 
            triggerType == TriggerType.PlayerStay && other.CompareTag(playerTag))
        {
            ActivateTrigger();
        }
    }
    
    /// <summary>
    /// 激活触发器，通知所有关联的接收器
    /// </summary>
    public virtual void ActivateTrigger()
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
    
    protected virtual IEnumerator DelayedTrigger()
    {
        yield return new WaitForSeconds(triggerDelay);
        ExecuteTrigger();
    }
    
    protected virtual void ExecuteTrigger()
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
        
        // 保存状态
        SaveState();
    }
    
    /// <summary>
    /// 保存触发器状态
    /// </summary>
    protected virtual void SaveState()
    {
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.SaveObject(this);
        }
    }
    
    /// <summary>
    /// 设置触发器的激活状态
    /// </summary>
    public virtual void SetActive(bool active)
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
        
        // 保存状态
        SaveState();
    }
    
    /// <summary>
    /// 重置触发器状态，允许再次触发
    /// </summary>
    public virtual void ResetTrigger()
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
        
        // 保存状态
        SaveState();
    }
    
    /// <summary>
    /// 启动定时触发器
    /// </summary>
    protected virtual void StartTimerTrigger()
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
    protected virtual IEnumerator TimerTriggerCoroutine()
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
    public virtual void ExternalActivate(string callerName = "")
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
    /// 获取触发器是否已经触发过
    /// </summary>
    public bool HasTriggered()
    {
        return hasTriggered;
    }
    
    /// <summary>
    /// 获取目标接收器数组
    /// </summary>
    public Reciever[] GetTargetRecievers()
    {
        return targetRecievers;
    }
    
    /// <summary>
    /// 添加目标接收器
    /// </summary>
    public void AddTargetReciever(Reciever reciever)
    {
        if (reciever != null)
        {
            // 创建新数组并添加接收器
            List<Reciever> recievers = new List<Reciever>(targetRecievers);
            if (!recievers.Contains(reciever))
            {
                recievers.Add(reciever);
                targetRecievers = recievers.ToArray();
            }
        }
    }
    
    /// <summary>
    /// 获取玩家是否在交互区域内
    /// </summary>
    public bool IsPlayerInInteractionZone()
    {
        return playerInTriggerArea;
    }
    
    #region ISaveable Implementation
    
    /// <summary>
    /// 获取对象的唯一ID
    /// </summary>
    public virtual string GetUniqueID()
    {
        return uniqueID;
    }
    
    /// <summary>
    /// 保存对象状态
    /// </summary>
    public virtual SaveData Save()
    {
        SaveData data = new SaveData();
        data.objectType = "Trigger";
        data.boolValue = hasTriggered;
        data.boolValue2 = isActive;
        data.intValue = currentRepeatCount;
        
        return data;
    }
    
    /// <summary>
    /// 加载对象状态
    /// </summary>
    public virtual void Load(SaveData data)
    {
        if (data == null || data.objectType != "Trigger") return;
        
        hasTriggered = data.boolValue;
        isActive = data.boolValue2;
        currentRepeatCount = data.intValue;
        
        // 如果是定时触发类型且处于激活状态，重新启动定时器
        if (isActive && triggerType == TriggerType.TimerBased && timerCoroutine == null)
        {
            StartTimerTrigger();
        }
    }
    
    #endregion
    
    #if UNITY_EDITOR
    // 在编辑器中可视化交互范围
    protected virtual void OnDrawGizmos()
    {
        if (showInteractionRange && useInteractionCollider)
        {
            // 绘制交互范围
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            
            // 如果有交互碰撞器，使用它的实际尺寸
            if (interactionCollider != null)
            {
                if (interactionCollider is CircleCollider2D circleCollider)
                {
                    // 绘制圆形交互区域
                    Gizmos.DrawSphere(transform.position + (Vector3)circleCollider.offset, circleCollider.radius);
                }
                else if (interactionCollider is BoxCollider2D boxCollider)
                {
                    // 绘制方形交互区域
                    Gizmos.matrix = Matrix4x4.TRS(
                        transform.position + (Vector3)boxCollider.offset,
                        transform.rotation,
                        transform.lossyScale
                    );
                    Gizmos.DrawCube(Vector3.zero, boxCollider.size);
                    Gizmos.matrix = Matrix4x4.identity;
                }
            }
            else
            {
                // 如果没有碰撞器，使用默认半径
                Gizmos.DrawSphere(transform.position, 1.5f);
            }
        }
    }
    #endif
}