using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Text;

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
    [SerializeField] private bool startActive = false; // 接收器的初始激活状态，如果为true，则在Start时会自动激活所有行为
    [SerializeField] private bool debugMode = false; // 是否启用调试模式

    [Header("行为组件")]
    [SerializeField] private MonoBehaviour[] actionComponents;

    // 事件系统，允许在编辑器中连接其他行为
    public UnityEvent onActivate;
    public UnityEvent onDeactivate;

    private bool isActive = false;
    private List<IMechanicAction> mechanicActions = new List<IMechanicAction>();
    
    #if UNITY_EDITOR
    // 用于非分配字符串构建的StringBuilder
    private StringBuilder debugBuilder = new StringBuilder(128);
    #endif

    public void Start()
    {
        // 收集所有实现了IMechanicAction接口的组件
        foreach (var component in actionComponents)
        {
            if (component is IMechanicAction action)
            {
                mechanicActions.Add(action);
                
                #if UNITY_EDITOR
                if (debugMode)
                {
                    debugBuilder.Length = 0;
                    debugBuilder.Append("接收器 [");
                    debugBuilder.Append(gameObject.name);
                    debugBuilder.Append("] 添加了行为组件: ");
                    debugBuilder.Append(component.GetType().Name);
                    Debug.Log(debugBuilder.ToString());
                }
                #endif
            }
            #if UNITY_EDITOR
            else if (debugMode)
            {
                debugBuilder.Length = 0;
                debugBuilder.Append("警告: 接收器 [");
                debugBuilder.Append(gameObject.name);
                debugBuilder.Append("] 的组件 ");
                debugBuilder.Append(component.GetType().Name);
                debugBuilder.Append(" 未实现 IMechanicAction 接口");
                Debug.LogWarning(debugBuilder.ToString());
            }
            #endif
        }

        isActive = startActive;

        // 如果初始状态为激活，则激活所有行为
        if (isActive)
        {
            ActivateActions();
        }
        
        #if UNITY_EDITOR
        if (debugMode)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 初始化完成, 状态: ");
            debugBuilder.Append(isActive ? "激活" : "未激活");
            debugBuilder.Append(", 行为数量: ");
            debugBuilder.Append(mechanicActions.Count);
            Debug.Log(debugBuilder.ToString());
        }
        #endif
    }

    /// <summary>
    /// 当触发器激活时调用此方法
    /// </summary>
    public void OnTriggerActivated(Trigger trigger)
    {
        #if UNITY_EDITOR
        if (debugMode)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 被触发器 [");
            debugBuilder.Append(trigger != null ? trigger.gameObject.name : "未知");
            debugBuilder.Append("] 激活, 当前激活类型: ");
            debugBuilder.Append(activationType.ToString());
            Debug.Log(debugBuilder.ToString());
        }
        #endif

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

        #if UNITY_EDITOR
        if (debugMode)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 切换状态为: ");
            debugBuilder.Append(isActive ? "激活" : "未激活");
            Debug.Log(debugBuilder.ToString());
        }
        #endif

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

        #if UNITY_EDITOR
        if (debugMode)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 正在激活 ");
            debugBuilder.Append(mechanicActions.Count);
            debugBuilder.Append(" 个行为组件");
            Debug.Log(debugBuilder.ToString());
        }
        #endif

        foreach (var action in mechanicActions)
        {
            #if UNITY_EDITOR
            if (debugMode)
            {
                debugBuilder.Length = 0;
                debugBuilder.Append("接收器 [");
                debugBuilder.Append(gameObject.name);
                debugBuilder.Append("] 激活行为: ");
                debugBuilder.Append(action.GetType().Name);
                Debug.Log(debugBuilder.ToString());
            }
            #endif

            action.Activate();
        }

        #if UNITY_EDITOR
        if (debugMode && onActivate != null && onActivate.GetPersistentEventCount() > 0)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 调用 onActivate 事件, 事件数量: ");
            debugBuilder.Append(onActivate.GetPersistentEventCount());
            Debug.Log(debugBuilder.ToString());
        }
        #endif

        onActivate?.Invoke();
    }

    /// <summary>
    /// 停止所有行为
    /// </summary>
    public virtual void DeactivateActions()
    {
        isActive = false;

        #if UNITY_EDITOR
        if (debugMode)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 正在停用 ");
            debugBuilder.Append(mechanicActions.Count);
            debugBuilder.Append(" 个行为组件");
            Debug.Log(debugBuilder.ToString());
        }
        #endif

        foreach (var action in mechanicActions)
        {
            #if UNITY_EDITOR
            if (debugMode)
            {
                debugBuilder.Length = 0;
                debugBuilder.Append("接收器 [");
                debugBuilder.Append(gameObject.name);
                debugBuilder.Append("] 停用行为: ");
                debugBuilder.Append(action.GetType().Name);
                Debug.Log(debugBuilder.ToString());
            }
            #endif

            action.Deactivate();
        }

        #if UNITY_EDITOR
        if (debugMode && onDeactivate != null && onDeactivate.GetPersistentEventCount() > 0)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 调用 onDeactivate 事件, 事件数量: ");
            debugBuilder.Append(onDeactivate.GetPersistentEventCount());
            Debug.Log(debugBuilder.ToString());
        }
        #endif

        onDeactivate?.Invoke();
    }

    /// <summary>
    /// 获取接收器的激活状态
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }
    
    public ActivationType GetActivationType()
    {
        return activationType;
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
        
        #if UNITY_EDITOR
        if (debugMode)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 保存状态: ");
            debugBuilder.Append(isActive ? "激活" : "未激活");
            Debug.Log(debugBuilder.ToString());
        }
        #endif
        
        return data;
    }
    
    public void Load(SaveData data)
    {
        if (data == null || data.objectType != "Reciever") return;
        
        // 加载激活状态
        bool wasActive = isActive;
        isActive = data.boolValue;
        
        #if UNITY_EDITOR
        if (debugMode)
        {
            debugBuilder.Length = 0;
            debugBuilder.Append("接收器 [");
            debugBuilder.Append(gameObject.name);
            debugBuilder.Append("] 加载状态: ");
            debugBuilder.Append(isActive ? "激活" : "未激活");
            debugBuilder.Append(", 之前状态: ");
            debugBuilder.Append(wasActive ? "激活" : "未激活");
            Debug.Log(debugBuilder.ToString());
        }
        #endif
        
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