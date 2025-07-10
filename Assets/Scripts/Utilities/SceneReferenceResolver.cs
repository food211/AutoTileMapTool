using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine.SceneManagement;

public class SceneReferenceResolver : MonoBehaviour
{
    [System.Serializable]
    public class ManagerReference
    {
        public GameObject targetManagerObject; // 目标管理器对象（场景中或预制体实例）
        public string managerTypeName; // 管理器类型名称（自动填充）
        [HideInInspector] public Type managerType; // 管理器类型（运行时使用）
        
        [System.Serializable]
        public class FieldReference
        {
            public string fieldName; // 字段名称
            public GameObject sceneObject; // 场景对象引用
            public Component componentReference; // 组件引用 - 新增
            [HideInInspector] public Type fieldType; // 字段类型（运行时使用）
            public string fieldTypeName; // 字段类型名称（显示用）
            [HideInInspector] public bool isProperty; // 是否为属性而非字段
            [HideInInspector] public bool isComponentType; // 是否为组件类型 - 新增
        }
        
        public List<FieldReference> fieldReferences = new List<FieldReference>();
    }
    
    public List<ManagerReference> managerReferences = new List<ManagerReference>();
    
    [Header("Debug Settings")]
    [SerializeField] private bool logDebugInfo = true;
    
    [Header("Resolution Settings")]
    [SerializeField] private bool resolveOnStart = true;
    [SerializeField] private bool resolveOnSceneLoad = true;
    [SerializeField] private float resolveDelay = 0.1f; // 解析延迟时间
    
    private void Awake()
    {
        // 订阅GameInitializer事件
        if (GameInitializer.ManagersInitialized)
        {
            // 如果已经初始化完成，直接设置延迟解析
            if (resolveOnStart)
            {
                StartCoroutine(ResolveReferencesDelayed(resolveDelay));
            }
        }
        else
        {
            // 如果尚未初始化，订阅初始化完成事件
            GameInitializer.OnManagersInitialized += HandleManagersInitialized;
        }
        
        // 如果需要在场景加载时解析，订阅验证完成事件
        if (resolveOnSceneLoad)
        {
            GameInitializer.OnManagersVerified += HandleManagersVerified;
        }
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        GameInitializer.OnManagersInitialized -= HandleManagersInitialized;
        GameInitializer.OnManagersVerified -= HandleManagersVerified;
    }
    
    private void HandleManagersInitialized()
    {
        if (resolveOnStart)
        {
            StartCoroutine(ResolveReferencesDelayed(resolveDelay));
        }
    }
    
    private void HandleManagersVerified(Scene scene, LoadSceneMode mode)
    {
        // 当前场景加载且管理器验证完成后，解析引用
        if (scene == SceneManager.GetActiveScene() && resolveOnSceneLoad)
        {
            StartCoroutine(ResolveReferencesDelayed(resolveDelay));
        }
    }
    
    IEnumerator ResolveReferencesDelayed(float delay = 0.1f)
    {
        // 等待指定的延迟时间
        yield return new WaitForSeconds(delay);
        
        // 解析所有引用
        ResolveAllReferences();
    }
    
    /// <summary>
    /// 解析所有引用
    /// </summary>
    public void ResolveAllReferences()
    {
        foreach (var managerRef in managerReferences)
        {
            if (managerRef.targetManagerObject == null)
            {
                Debug.LogWarning("目标管理器对象为空");
                continue;
            }
            
            // 获取管理器组件
            Type managerType = null;
            if (!string.IsNullOrEmpty(managerRef.managerTypeName))
            {
                // 通过名称获取类型
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    managerType = assembly.GetType(managerRef.managerTypeName);
                    if (managerType != null) break;
                }
            }
            
            if (managerType == null)
            {
                Debug.LogWarning($"无法找到管理器类型: {managerRef.managerTypeName}");
                continue;
            }
            
            // 获取管理器组件实例
            Component managerComponent = managerRef.targetManagerObject.GetComponent(managerType);
            if (managerComponent == null)
            {
                Debug.LogWarning($"目标对象上未找到管理器组件: {managerRef.managerTypeName}");
                continue;
            }
            
            // 解析所有字段引用
            foreach (var fieldRef in managerRef.fieldReferences)
            {
                if (string.IsNullOrEmpty(fieldRef.fieldName))
                {
                    continue;
                }
                
                try
                {
                    // 设置引用值
                    if (fieldRef.isProperty)
                    {
                        // 处理属性
                        PropertyInfo propertyInfo = managerType.GetProperty(fieldRef.fieldName);
                        if (propertyInfo != null && propertyInfo.CanWrite)
                        {
                            if (fieldRef.isComponentType && fieldRef.componentReference != null)
                            {
                                // 设置组件引用
                                propertyInfo.SetValue(managerComponent, fieldRef.componentReference);
                                if (logDebugInfo)
                                {
                                    Debug.Log($"已设置属性 {fieldRef.fieldName} 为组件 {fieldRef.componentReference}");
                                }
                            }
                            else if (fieldRef.sceneObject != null)
                            {
                                // 设置GameObject引用
                                propertyInfo.SetValue(managerComponent, fieldRef.sceneObject);
                                if (logDebugInfo)
                                {
                                    Debug.Log($"已设置属性 {fieldRef.fieldName} 为场景对象 {fieldRef.sceneObject.name}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // 处理字段
                        FieldInfo fieldInfo = managerType.GetField(fieldRef.fieldName);
                        if (fieldInfo != null)
                        {
                            if (fieldRef.isComponentType && fieldRef.componentReference != null)
                            {
                                // 设置组件引用
                                fieldInfo.SetValue(managerComponent, fieldRef.componentReference);
                                if (logDebugInfo)
                                {
                                    Debug.Log($"已设置字段 {fieldRef.fieldName} 为组件 {fieldRef.componentReference}");
                                }
                            }
                            else if (fieldRef.sceneObject != null)
                            {
                                // 设置GameObject引用
                                fieldInfo.SetValue(managerComponent, fieldRef.sceneObject);
                                if (logDebugInfo)
                                {
                                    Debug.Log($"已设置字段 {fieldRef.fieldName} 为场景对象 {fieldRef.sceneObject.name}");
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"设置引用时出错: {e.Message}");
                }
            }
        }
        
        if (logDebugInfo)
        {
            Debug.Log("场景引用解析完成");
        }
    }
    
    /// <summary>
    /// 手动触发引用解析
    /// </summary>
    public void ManualResolve()
    {
        StartCoroutine(ResolveReferencesDelayed(0.1f));
    }
}