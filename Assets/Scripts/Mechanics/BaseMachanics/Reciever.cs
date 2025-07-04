using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Reciever : MonoBehaviour, ISaveable
{
    // 添加一个唯一ID字段
    [SerializeField] private string uniqueID;

    [System.Serializable]
    public enum ActivationType
    {
        Toggle,     // 切换行为的激活状态
        Activate,   // 只激活行为
        Deactivate  // 只停止行为
    }

    [Header("接收器设置")]
    [SerializeField] protected ActivationType activationType = ActivationType.Toggle;
    [SerializeField] private bool startActive = false;

    [Header("行为组件")]
    [SerializeField] private MonoBehaviour[] actionComponents;

    // 事件系统，允许在编辑器中连接其他行为
    public UnityEvent onActivate;
    public UnityEvent onDeactivate;

    private bool isActive = false;
    private List<IMechanicAction> mechanicActions = new List<IMechanicAction>();

    public void Start()
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
    public virtual void ToggleActions()
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
    public virtual void ActivateActions()
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
    public virtual void DeactivateActions()
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
    #region ISaveable Implementation
    
    public string GetUniqueID()
    {
        // 如果没有设置ID，自动生成一个
        if (string.IsNullOrEmpty(uniqueID))
        {
            uniqueID = $"Reciever_{gameObject.name}_{GetInstanceID()}";
        }
        return uniqueID;
    }
    
    public SaveData Save()
    {
        SaveData data = new SaveData();
        data.objectType = "Reciever";
        
        // 保存当前激活状态
        data.boolValue = isActive;
        
        return data;
    }
    
    public void Load(SaveData data)
    {
        if (data == null || data.objectType != "Reciever") return;
        
        // 加载激活状态
        bool wasActive = isActive;
        isActive = data.boolValue;
        
        // 只有当状态与当前状态不同时才触发相应的操作
        if (isActive && !wasActive)
        {
            ActivateActions();
        }
        else if (!isActive && wasActive)
        {
            DeactivateActions();
        }
    }
    
    #endregion
}