using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Reciever : MonoBehaviour
{
    [System.Serializable]
    public enum ActivationType
    {
        Toggle,     // 切换行为的激活状态
        Activate,   // 只激活行为
        Deactivate  // 只停止行为
    }
    
    [Header("接收器设置")]
    [SerializeField] private ActivationType activationType = ActivationType.Toggle;
    [SerializeField] private bool startActive = false;
    
    [Header("行为组件")]
    [SerializeField] private MonoBehaviour[] actionComponents;
    
    // 事件系统，允许在编辑器中连接其他行为
    public UnityEvent onActivate;
    public UnityEvent onDeactivate;
    
    private bool isActive = false;
    private List<IMechanicAction> mechanicActions = new List<IMechanicAction>();
    
    private void Start()
    {
        // 收集所有实现了IMechanicAction接口的组件
        foreach (var component in actionComponents)
        {
            if (component is IMechanicAction action)
            {
                mechanicActions.Add(action);
            }
        }
        
        isActive = startActive;
        
        // 如果初始状态为激活，则激活所有行为
        if (isActive)
        {
            ActivateActions();
        }
    }
    
    /// <summary>
    /// 当触发器激活时调用此方法
    /// </summary>
    public void OnTriggerActivated(Trigger trigger)
    {
        switch (activationType)
        {
            case ActivationType.Toggle:
                ToggleActions();
                break;
            case ActivationType.Activate:
                ActivateActions();
                break;
            case ActivationType.Deactivate:
                DeactivateActions();
                break;
        }
    }
    
    /// <summary>
    /// 切换行为的激活状态
    /// </summary>
    public void ToggleActions()
    {
        isActive = !isActive;
        
        if (isActive)
        {
            ActivateActions();
        }
        else
        {
            DeactivateActions();
        }
    }
    
    /// <summary>
    /// 激活所有行为
    /// </summary>
    public void ActivateActions()
    {
        isActive = true;
        
        foreach (var action in mechanicActions)
        {
            action.Activate();
        }
        
        onActivate?.Invoke();
    }
    
    /// <summary>
    /// 停止所有行为
    /// </summary>
    public void DeactivateActions()
    {
        isActive = false;
        
        foreach (var action in mechanicActions)
        {
            action.Deactivate();
        }
        
        onDeactivate?.Invoke();
    }
    
    /// <summary>
    /// 获取接收器的激活状态
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }
}