using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class SceneUIMapping
{
    public string sceneName;                    // 场景名称
    public List<GameObject> objectsToShow;      // 在此场景中显示的物体
    public List<GameObject> objectsToHide;      // 在此场景中隐藏的物体
}

public class UIManager : MonoBehaviour
{
    [Header("UI管理设置")]
    [Tooltip("是否在场景切换时保持此管理器存在")]
    public bool dontDestroyOnLoad = true;
    
    [Header("场景UI映射")]
    [Tooltip("为每个场景配置要显示/隐藏的UI元素")]
    public List<SceneUIMapping> sceneUIMappings = new List<SceneUIMapping>();
    
    [Header("默认设置")]
    [Tooltip("未指定场景的默认显示物体")]
    public List<GameObject> defaultObjectsToShow;
    
    [Tooltip("未指定场景的默认隐藏物体")]
    public List<GameObject> defaultObjectsToHide;
    
    private void Awake()
    {
        if (dontDestroyOnLoad)
        {
            // 确保不会被销毁，以便在场景切换时保持存在
            DontDestroyOnLoad(gameObject);
        }
        
        // 初始检查当前场景
        CheckCurrentScene(SceneManager.GetActiveScene().name);
        
        // 订阅场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        // 取消订阅场景加载事件，防止内存泄漏
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 当任何场景加载时调用
        CheckCurrentScene(scene.name);
    }
    
    private void CheckCurrentScene(string sceneName)
    {
        bool foundMapping = false;
        
        // 查找当前场景的映射
        foreach (SceneUIMapping mapping in sceneUIMappings)
        {
            if (mapping.sceneName == sceneName)
            {
                ApplyUIMapping(mapping);
                foundMapping = true;
                
                #if UNITY_EDITOR
                Debug.Log($"UIManager: 已应用场景 '{sceneName}' 的UI映射");
                #endif
                
                break;
            }
        }
        
        // 如果没有找到映射，应用默认设置
        if (!foundMapping)
        {
            ApplyDefaultSettings();
            
            #if UNITY_EDITOR
            Debug.Log($"UIManager: 场景 '{sceneName}' 没有特定映射，已应用默认设置");
            #endif
        }
    }
    
    private void ApplyUIMapping(SceneUIMapping mapping)
    {
        // 显示指定的物体
        if (mapping.objectsToShow != null)
        {
            foreach (GameObject obj in mapping.objectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        
        // 隐藏指定的物体
        if (mapping.objectsToHide != null)
        {
            foreach (GameObject obj in mapping.objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }
    
    private void ApplyDefaultSettings()
    {
        // 显示默认物体
        if (defaultObjectsToShow != null)
        {
            foreach (GameObject obj in defaultObjectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        
        // 隐藏默认物体
        if (defaultObjectsToHide != null)
        {
            foreach (GameObject obj in defaultObjectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }
    
    /// <summary>
    /// 手动更新UI状态（可从其他脚本调用）
    /// </summary>
    public void UpdateUIState()
    {
        CheckCurrentScene(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// 手动设置物体可见性
    /// </summary>
    public void SetObjectActive(GameObject obj, bool active)
    {
        if (obj != null)
        {
            obj.SetActive(active);
        }
    }
}