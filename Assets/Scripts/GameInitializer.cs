using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// 游戏初始化器：负责确保所有必要的管理器在游戏运行时存在
/// 集成了场景管理器检查功能，并提供更完善的初始化流程
/// 支持根据场景类型控制管理器的加载和卸载
/// </summary>
public class GameInitializer : MonoBehaviour
{
    // 单例模式
    private static GameInitializer _instance;
    public static GameInitializer Instance => _instance;

    // 事件
    public static event Action OnManagersInitialized;
    public static event Action<Scene, LoadSceneMode> OnManagersVerified;

    // 标记是否已完成初始化
    private static bool _managersInitialized = false;
    public static bool ManagersInitialized => _managersInitialized;

    // 场景类型枚举
    public enum SceneType
    {
        All,            // 所有场景
        GameScene,      // 游戏场景
        MenuScene,      // 菜单场景
        LoadingScene,   // 加载场景
        None            // 不在任何场景中加载
    }

    // 场景类型配置
    [System.Serializable]
    public class SceneTypeConfig
    {
        public string sceneName;
        public SceneType sceneType = SceneType.GameScene;
    }

    [System.Serializable]
    public class ManagerPrefab
    {
        public GameObject prefab;
        public bool initializeOnStart = true;
        [HideInInspector] public System.Type managerType;
        
        [Tooltip("指定此管理器应该在哪些类型的场景中存在")]
        public SceneType visibleInScenes = SceneType.All;
        
        // 获取管理器名称
        public string GetManagerName() => prefab != null ? prefab.name : string.Empty;

        public ManagerPrefab(GameObject prefab)
        {
            this.prefab = prefab;
            this.initializeOnStart = true;
            this.visibleInScenes = SceneType.All;
        }
    }

    [Header("Manager Prefabs")]
    [SerializeField] private List<ManagerPrefab> managerPrefabs = new List<ManagerPrefab>();
    
    [Header("Scene Configuration")]
    [SerializeField] private List<SceneTypeConfig> sceneConfigs = new List<SceneTypeConfig>();
    [SerializeField] private SceneType defaultSceneType = SceneType.GameScene;
    [SerializeField] private List<string> menuSceneNames = new List<string>() { "StartMenu", "MainMenu" };
    [SerializeField] private List<string> loadingSceneNames = new List<string>() { "LoadingUI" };
    
    [Header("Debug Settings")]
    [SerializeField] private bool logDebugInfo = true;
    
    // 已初始化的管理器列表
    private static List<string> initializedManagers = new List<string>();
    
    // 当前管理器实例字典
    private static Dictionary<string, GameObject> managerInstances = new Dictionary<string, GameObject>();
    
    // 当前场景类型
    private static SceneType currentSceneType = SceneType.GameScene;
    
    // 在游戏启动时自动执行一次
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeOnGameStart()
    {
        // 尝试从Resources加载预制体
        GameObject initializerPrefab = Resources.Load<GameObject>("Managers/GameInitializer");
        
        if (initializerPrefab != null)
        {
            // 实例化预制体
            GameObject initializerObj = Instantiate(initializerPrefab);
            initializerObj.name = "GameInitializer";
            _instance = initializerObj.GetComponent<GameInitializer>();
            
            if (_instance == null)
            {
                LogError("GameInitializer预制体不包含GameInitializer组件");
                return;
            }
        }
        else
        {
            // 如果找不到预制体，则创建一个新实例
            GameObject initializerObj = new GameObject("GameInitializer");
            _instance = initializerObj.AddComponent<GameInitializer>();
            LogWarning("未找到GameInitializer预制体，已创建新实例");
        }
        
        DontDestroyOnLoad(_instance.gameObject);
        
        // 初始化核心管理器
        InitializeManagers();
        
        // 订阅场景加载事件，确保每次场景加载都检查管理器
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        Log("GameInitializer 已创建并初始化完成");
        
        // 标记初始化完成并触发事件
        _managersInitialized = true;
        OnManagersInitialized?.Invoke();
    }
    
    // 场景加载时检查
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 确定新场景的类型
        SceneType newSceneType = DetermineSceneType(scene.name);
        
        // 如果场景类型变化，处理管理器的显示/隐藏
        if (newSceneType != currentSceneType)
        {
            HandleSceneTypeChange(newSceneType);
            currentSceneType = newSceneType;
        }
        
        // 检查管理器是否存在
        VerifyManagersExist();
        
        Log($"场景 '{scene.name}' (类型: {newSceneType}) 已加载，管理器检查完成");
        
        // 触发管理器验证完成事件
        OnManagersVerified?.Invoke(scene, mode);
    }
    
    // 确定场景类型
    private static SceneType DetermineSceneType(string sceneName)
    {
        // 如果没有实例，返回默认类型
        if (_instance == null)
            return SceneType.GameScene;
            
        // 首先检查明确配置的场景
        foreach (var config in _instance.sceneConfigs)
        {
            if (config.sceneName == sceneName)
                return config.sceneType;
        }
        
        // 检查是否是菜单场景
        if (_instance.menuSceneNames.Contains(sceneName))
            return SceneType.MenuScene;
        
        // 检查是否是加载场景
        if (_instance.loadingSceneNames.Contains(sceneName))
            return SceneType.LoadingScene;
        
        // 返回默认场景类型
        return _instance.defaultSceneType;
    }
    
    // 处理场景类型变化
    private static void HandleSceneTypeChange(SceneType newSceneType)
    {
        if (_instance == null || _instance.managerPrefabs == null)
            return;
            
        foreach (var managerPrefab in _instance.managerPrefabs)
        {
            string managerName = managerPrefab.GetManagerName();
            
            // 跳过空预制体
            if (string.IsNullOrEmpty(managerName))
                continue;
                
            bool shouldBeVisible = ShouldManagerBeVisible(managerPrefab.visibleInScenes, newSceneType);
            bool isCurrentlyActive = managerInstances.ContainsKey(managerName) && 
                                    managerInstances[managerName] != null;
            
            // 如果应该可见但当前不活跃，激活它
            if (shouldBeVisible && !isCurrentlyActive)
            {
                Log($"激活管理器 {managerName} (场景类型: {newSceneType})");
                
                // 尝试初始化管理器
                if (managerPrefab.managerType != null)
                {
                    typeof(GameInitializer)
                        .GetMethod("EnsureManagerExistsWithPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .MakeGenericMethod(managerPrefab.managerType)
                        .Invoke(null, new object[] { managerName, managerPrefab.prefab });
                }
            }
            // 如果不应该可见但当前活跃，停用它
            else if (!shouldBeVisible && isCurrentlyActive)
            {
                Log($"停用管理器 {managerName} (场景类型: {newSceneType})");
                
                // 停用管理器
                GameObject managerObj = managerInstances[managerName];
                if (managerObj != null)
                    managerObj.SetActive(false);
            }
        }
    }
    
    // 判断管理器是否应该在指定场景类型中可见
    private static bool ShouldManagerBeVisible(SceneType managerSceneType, SceneType currentSceneType)
    {
        // 如果管理器配置为在所有场景中可见
        if (managerSceneType == SceneType.All)
            return true;
            
        // 如果管理器配置为在任何场景中都不可见
        if (managerSceneType == SceneType.None)
            return false;
            
        // 否则，检查场景类型是否匹配
        return managerSceneType == currentSceneType;
    }
    
    // 初始化所有必要的管理器
    private static void InitializeManagers()
    {
        if (_instance == null || _instance.managerPrefabs == null || _instance.managerPrefabs.Count == 0)
        {
            Log("未找到ManagerPrefab配置");
            return;
        }

        // 确定当前场景类型
        currentSceneType = DetermineSceneType(SceneManager.GetActiveScene().name);

        // 使用配置的管理器预制体进行初始化
        foreach (var managerPrefab in _instance.managerPrefabs)
        {
            if (managerPrefab.prefab != null && managerPrefab.initializeOnStart)
            {
                // 检查此管理器是否应该在当前场景类型中可见
                if (!ShouldManagerBeVisible(managerPrefab.visibleInScenes, currentSceneType))
                {
                    Log($"跳过管理器 {managerPrefab.GetManagerName()} 初始化 (当前场景类型: {currentSceneType})");
                    continue;
                }
                
                string managerName = managerPrefab.GetManagerName();
                
                // 获取预制体上的所有MonoBehaviour组件
                MonoBehaviour[] components = managerPrefab.prefab.GetComponents<MonoBehaviour>();
                if (components.Length > 0)
                {
                    // 使用第一个组件类型作为管理器类型
                    Type managerType = components[0].GetType();
                    managerPrefab.managerType = managerType;
                    
                    // 使用反射调用EnsureManagerExists方法
                    typeof(GameInitializer)
                        .GetMethod("EnsureManagerExistsWithPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .MakeGenericMethod(managerType)
                        .Invoke(null, new object[] { managerName, managerPrefab.prefab });
                }
                else
                {
                    LogWarning($"预制体 {managerName} 没有包含任何MonoBehaviour组件");
                }
            }
            else if (managerPrefab.initializeOnStart && managerPrefab.prefab == null)
            {
                LogWarning("有一个管理器预制体为空");
            }
        }
    }
    
    // 验证所有管理器是否存在
    private static void VerifyManagersExist()
    {
        if (_instance == null || _instance.managerPrefabs == null || _instance.managerPrefabs.Count == 0)
        {
            // 使用默认验证
            VerifyDefaultManagers();
            return;
        }

        // 确定当前场景类型
        SceneType sceneType = DetermineSceneType(SceneManager.GetActiveScene().name);

        // 验证配置的管理器
        foreach (var managerPrefab in _instance.managerPrefabs)
        {
            // 检查此管理器是否应该在当前场景类型中可见
            if (!ShouldManagerBeVisible(managerPrefab.visibleInScenes, sceneType))
                continue;
                
            if (managerPrefab.managerType != null && managerPrefab.prefab != null)
            {
                string managerName = managerPrefab.GetManagerName();
                
                // 使用反射查找对象
                var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { });
                var genericMethod = findMethod.MakeGenericMethod(managerPrefab.managerType);
                var manager = genericMethod.Invoke(null, null);

                if (manager == null && managerPrefab.initializeOnStart)
                {
                    LogWarning($"场景加载后未找到 {managerName}，尝试重新初始化...");
                    
                    // 使用反射调用EnsureManagerExists方法
                    typeof(GameInitializer)
                        .GetMethod("EnsureManagerExistsWithPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .MakeGenericMethod(managerPrefab.managerType)
                        .Invoke(null, new object[] { managerName, managerPrefab.prefab });
                }
                else if (manager != null)
                {
                    // 确保管理器处于激活状态
                    var managerObj = ((MonoBehaviour)manager).gameObject;
                    if (!managerObj.activeSelf)
                        managerObj.SetActive(true);
                    
                    // 更新管理器实例字典
                    managerInstances[managerName] = managerObj;
                }
            }
        }
    }
    
    // 验证默认管理器
    private static void VerifyDefaultManagers()
    {
        // 确定当前场景类型
        SceneType sceneType = DetermineSceneType(SceneManager.GetActiveScene().name);
        
        // 检查LoadManager（在所有场景中都需要）
        if (FindObjectOfType<LoadManager>() == null)
        {
            LogWarning("场景加载后未找到 LoadManager，尝试重新初始化...");
            EnsureManagerExists<LoadManager>("LoadManager");
        }
        
        // 检查LevelManager（在游戏场景和菜单场景中需要）
        if ((sceneType == SceneType.GameScene || sceneType == SceneType.MenuScene) && 
            FindObjectOfType<LevelManager>() == null)
        {
            LogWarning("场景加载后未找到 LevelManager，尝试重新初始化...");
            EnsureManagerExists<LevelManager>("LevelManager");
        }
    }
    
    // 确保指定类型的管理器存在（使用预制体）
    private static void EnsureManagerExistsWithPrefab<T>(string prefabName, GameObject prefab) where T : MonoBehaviour
    {
        // 检查管理器是否已存在
        T manager = UnityEngine.Object.FindObjectOfType<T>();
        
        if (manager == null)
        {
            // 如果已经尝试过初始化但失败，不要重复尝试
            if (initializedManagers.Contains(prefabName))
            {
                LogWarning($"{prefabName} 已尝试初始化但未成功，跳过重复尝试");
                return;
            }
            
            GameObject instance = null;
            
            if (prefab != null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = prefabName;
                DontDestroyOnLoad(instance);
                
                // 检查是否成功创建
                if (instance.GetComponent<T>() != null)
                {
                    initializedManagers.Add(prefabName);
                    managerInstances[prefabName] = instance;
                    Log($"{prefabName} 已成功初始化");
                }
                else
                {
                    LogError($"{prefabName} 预制体不包含 {typeof(T).Name} 组件");
                }
            }
            else
            {
                // 尝试从Resources加载预制体
                GameObject resourcePrefab = Resources.Load<GameObject>("Managers/" + prefabName);
                
                if (resourcePrefab != null)
                {
                    instance = UnityEngine.Object.Instantiate(resourcePrefab);
                    instance.name = prefabName;
                    DontDestroyOnLoad(instance);
                    
                    // 检查是否成功创建
                    if (instance.GetComponent<T>() != null)
                    {
                        initializedManagers.Add(prefabName);
                        managerInstances[prefabName] = instance;
                        Log($"{prefabName} 已从Resources/Managers成功初始化");
                    }
                    else
                    {
                        LogError($"{prefabName} 预制体不包含 {typeof(T).Name} 组件");
                    }
                }
                else
                {
                    // 如果没有找到预制体，尝试创建一个空对象并添加组件
                    instance = new GameObject(prefabName);
                    T component = instance.AddComponent<T>();
                    
                    if (component != null)
                    {
                        DontDestroyOnLoad(instance);
                        initializedManagers.Add(prefabName);
                        managerInstances[prefabName] = instance;
                        Log($"{prefabName} 已自动创建（无预制体）");
                    }
                    else
                    {
                        LogError($"无法创建 {prefabName}，请确保预制体位于 Resources/Managers 文件夹中");
                    }
                }
            }
        }
        else if (!initializedManagers.Contains(prefabName))
        {
            // 管理器已存在但未记录
            initializedManagers.Add(prefabName);
            managerInstances[prefabName] = manager.gameObject;
            Log($"{prefabName} 已存在，无需初始化");
        }
    }
    
    // 确保指定类型的管理器存在（从Resources/Managers加载）
    private static void EnsureManagerExists<T>(string prefabName) where T : MonoBehaviour
    {
        EnsureManagerExistsWithPrefab<T>(prefabName, null);
    }
    
    // 提供公共方法，允许手动初始化或重新初始化特定管理器
    public static void InitializeManager<T>(string prefabName) where T : MonoBehaviour
    {
        GameObject prefab = null;
        
        // 查找预制体
        if (_instance != null && _instance.managerPrefabs != null)
        {
            foreach (var managerPrefab in _instance.managerPrefabs)
            {
                if (managerPrefab.prefab != null && managerPrefab.GetManagerName() == prefabName)
                {
                    prefab = managerPrefab.prefab;
                    break;
                }
            }
        }
        
        EnsureManagerExistsWithPrefab<T>(prefabName, prefab);
    }
    
    // 条件日志方法
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private static void Log(string message)
    {
        if (_instance != null && _instance.logDebugInfo)
            Debug.Log($"GameInitializer: {message}");
    }
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private static void LogWarning(string message)
    {
        Debug.LogWarning($"GameInitializer: {message}");
    }
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private static void LogError(string message)
    {
        Debug.LogError($"GameInitializer: {message}");
    }
    
    // 清理方法
    private void OnDestroy()
    {
        // 取消订阅场景加载事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        if (_instance == this)
            _instance = null;
    }
    
    // 在Unity编辑器中添加菜单项，方便手动初始化
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Game/Initialize Managers")]
    private static void EditorInitializeManagers()
    {
        InitializeManagers();
        UnityEditor.EditorUtility.DisplayDialog("初始化完成", "所有管理器已初始化", "确定");
    }
    
    // 在编辑器中添加按钮，用于添加默认管理器
    [UnityEditor.CustomEditor(typeof(GameInitializer))]
    public class GameInitializerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            GameInitializer initializer = (GameInitializer)target;
            
            if (GUILayout.Button("添加默认管理器预制体"))
            {
                // 添加LoadManager
                AddDefaultManager(initializer, "LoadManager", SceneType.All);
                
                // 添加LevelManager
                AddDefaultManager(initializer, "LevelManager", SceneType.All);
                
                // 标记对象已修改
                UnityEditor.EditorUtility.SetDirty(initializer);
            }
            
            if (GUILayout.Button("添加默认场景配置"))
            {
                // 添加StartMenu
                AddDefaultSceneConfig(initializer, "StartMenu", SceneType.MenuScene);
                
                // 添加LoadingUI
                AddDefaultSceneConfig(initializer, "LoadingUI", SceneType.LoadingScene);
                
                // 标记对象已修改
                UnityEditor.EditorUtility.SetDirty(initializer);
            }
        }
        
        private void AddDefaultManager(GameInitializer initializer, string managerName, SceneType sceneType)
        {
            // 检查是否已存在
            bool exists = false;
            foreach (var manager in initializer.managerPrefabs)
            {
                if (manager.prefab != null && manager.GetManagerName() == managerName)
                {
                    exists = true;
                    break;
                }
            }
            
            if (!exists)
            {
                // 尝试从Resources/Managers加载预制体
                GameObject prefab = Resources.Load<GameObject>("Managers/" + managerName);
                
                if (prefab != null)
                {
                    ManagerPrefab newManager = new GameInitializer.ManagerPrefab(prefab);
                    newManager.visibleInScenes = sceneType;
                    initializer.managerPrefabs.Add(newManager);
                    Debug.Log($"GameInitializer: 已添加 {managerName} 预制体，场景类型: {sceneType}");
                }
                else
                {
                    // 创建一个空的预制体项
                    GameObject emptyPrefab = new GameObject(managerName);
                    ManagerPrefab newManager = new GameInitializer.ManagerPrefab(null);
                    newManager.visibleInScenes = sceneType;
                    initializer.managerPrefabs.Add(newManager);
                    UnityEngine.Object.DestroyImmediate(emptyPrefab);
                    Debug.LogWarning($"GameInitializer: 未找到 {managerName} 预制体，已添加空项，场景类型: {sceneType}");
                }
            }
        }
        
        private void AddDefaultSceneConfig(GameInitializer initializer, string sceneName, SceneType sceneType)
        {
            // 检查是否已存在
            bool exists = false;
            foreach (var config in initializer.sceneConfigs)
            {
                if (config.sceneName == sceneName)
                {
                    exists = true;
                    break;
                }
            }
            
            if (!exists)
            {
                SceneTypeConfig newConfig = new SceneTypeConfig();
                newConfig.sceneName = sceneName;
                newConfig.sceneType = sceneType;
                initializer.sceneConfigs.Add(newConfig);
                Debug.Log($"GameInitializer: 已添加场景配置: {sceneName}, 类型: {sceneType}");
            }
        }
    }
#endif
}