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
    [SerializeField] private KeyCode triggerKey = KeyCode.E;
    [SerializeField] private float triggerDelay = 0f;
    [SerializeField] private bool oneTimeOnly = false;
    [SerializeField] private bool startActive = true;
    
    [Header("目标接收器")]
    [SerializeField] private Reciever[] targetRecievers;
    
    // 事件系统，允许在编辑器中连接其他行为
    public UnityEvent onTriggerActivated;
    
    private bool hasTriggered = false;
    private bool isActive = false;
    
    private void Start()
    {
        isActive = startActive;
    }
    
    private void Update()
    {
        if (!isActive) return;
        
        // 检查按键触发
        if (triggerType == TriggerType.ButtonPress && Input.GetKeyDown(triggerKey))
        {
            ActivateTrigger();
        }

        // 这里可以添加其他类型的触发检测
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        
        if (triggerType == TriggerType.PlayerEnter && other.CompareTag(playerTag))
        {
            ActivateTrigger();
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isActive) return;
        
        if (triggerType == TriggerType.PlayerExit && other.CompareTag(playerTag))
        {
            ActivateTrigger();
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
    }
    
    /// <summary>
    /// 重置触发器状态，允许再次触发
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}