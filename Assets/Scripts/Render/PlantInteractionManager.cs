using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 植物交互管理器 - 重构版
/// 管理所有植物的交互和预设切换
/// </summary>
public class PlantInteractionManager : MonoBehaviour
{
  public static PlantInteractionManager Instance { get; private set; }
  
  [Header("=== 全局设置 ===")]
  [SerializeField] private float globalResponseIntensity = 1f;
  [SerializeField] private float maxUpdateDistance = 20f;
  [SerializeField] private Transform playerTransform;
  
  [Header("=== 性能优化 ===")]
  [SerializeField] private int maxPlantsPerFrame = 100;
  [SerializeField] private float updateInterval = 0.016f;   // 60fps
  [SerializeField] private bool enableDistanceCulling = true;
  
  [Header("=== 运行时预设切换 ===")]
  [SerializeField] private float presetTransitionDuration = 2f;
  [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
  [SerializeField] private bool enableGlobalPresetControl = true;
  
  [Header("=== 交互设置 ===")]
  [SerializeField] private float impactDuration = 1f;
  [SerializeField] private float impactFadeTime = 0.5f;
  
  [Header("=== 调试设置 ===")]
  [SerializeField] private bool showDebugInfo = true;
  [SerializeField] private bool drawGizmos = true;
  [SerializeField] private Color impactGizmoColor = Color.red;
  
  // 植物管理 - 使用接口
  private List<IPlantController> registeredPlants = new List<IPlantController>();
  private Queue<IPlantController> updateQueue = new Queue<IPlantController>();
  
  // 当前影响状态
  private Vector3 currentImpactPosition;
  private float currentImpactStrength;
  private float currentImpactRadius;
  private float impactStartTime;
  private bool hasActiveImpact = false;
  
  // 预设切换状态
  private PlantPersonalityPreset currentGlobalPreset = PlantPersonalityPreset.Gentle;
  private bool isTransitioning = false;
  private Coroutine transitionCoroutine;
  
  // 性能计数
  private int plantsUpdatedThisFrame = 0;
  private float lastUpdateTime;
  private int totalRegisteredPlants = 0;
  
  // 事件系统
  public System.Action<Vector3, float, float> OnPlayerImpact;  // 位置，强度，半径
  public System.Action<PlantPersonalityPreset> OnGlobalPresetChanged; // 全局预设改变

    #region Unity 生命周期

    void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();

            // 添加场景加载事件监听
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }
  
  void Start()
  {
      // 自动查找玩家
      if (playerTransform == null)
      {
          FindPlayerTransform();
      }
      
      // 自动注册场景中的所有植物
      RegisterAllPlantsInScene();
      
      // 设置初始预设
      if (enableGlobalPresetControl)
      {
          ApplyGlobalPreset(currentGlobalPreset, false);
      }
  }
  
  void Update()
  {
      // 分帧更新植物
      if (Time.time - lastUpdateTime >= updateInterval)
      {
          UpdatePlants();
          lastUpdateTime = Time.time;
      }
      
      // 更新交互效果
      if (hasActiveImpact)
      {
          UpdateInteractionEffects();
      }
      
      // 调试信息
      if (showDebugInfo)
      {
          UpdateDebugInfo();
      }
  }

    void OnDestroy()
    {
        // 移除场景加载事件监听
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        // 停止所有正在运行的协程
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        // 清理事件订阅
        OnPlayerImpact = null;
        OnGlobalPresetChanged = null;

        // 清理所有植物的引用
        foreach (var plant in registeredPlants)
        {
            if (plant != null)
            {
                try
                {
                    plant.ClearInteractionEffect();
                }
                catch (System.Exception)
                {
                    // 忽略错误
                }
            }
        }

        // 清空植物列表，避免在销毁过程中引用已销毁的对象
        registeredPlants.Clear();
        updateQueue.Clear();

        // 如果是单例实例，重置静态引用
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 清理所有已销毁的植物引用
        CleanupDestroyedPlants();

        // 重建更新队列
        RebuildUpdateQueue();

        // 延迟一帧后重新注册场景中的植物
        StartCoroutine(DelayedRegisterAllPlantsInScene());

        if (showDebugInfo)
        {
            Debug.Log($"[PlantInteractionManager] 场景已加载: {scene.name}，已清理植物引用");
        }
    }

    // 延迟注册场景中的植物
    private IEnumerator DelayedRegisterAllPlantsInScene()
    {
        // 等待一帧，确保所有对象都已实例化
        yield return null;

        // 重新查找玩家
        if (playerTransform == null)
        {
            FindPlayerTransform();
        }

        // 注册新场景中的植物
        RegisterAllPlantsInScene();
    }
  
  #endregion

    #region 初始化

    /// <summary>
    /// 初始化管理器
    /// </summary>
    private void InitializeManager()
    {
        // 初始化事件
        OnPlayerImpact += HandlePlayerImpact;

        if (showDebugInfo)
        {
            Debug.Log("[PlantInteractionManager] 管理器初始化完成");
        }
    }
  
  /// <summary>
  /// 查找玩家变换
  /// </summary>
  private void FindPlayerTransform()
  {
      // 尝试多种方式查找玩家
      GameObject player = GameObject.FindGameObjectWithTag("Player");
      if (player != null)
      {
          playerTransform = player.transform;
      }
      else
      {
          // 如果没有Player标签，尝试查找主摄像机
          if (Camera.main != null)
          {
              playerTransform = Camera.main.transform;
          }
      }
      
      if (showDebugInfo)
      {
          if (playerTransform != null)
          {
              Debug.Log($"[PlantInteractionManager] 找到玩家: {playerTransform.name}");
          }
          else
          {
              Debug.LogWarning("[PlantInteractionManager] 未找到玩家变换");
          }
      }
  }
  
  #endregion
  
  #region 植物注册管理
  
  /// <summary>
  /// 注册场景中所有植物
  /// </summary>
  public void RegisterAllPlantsInScene()
  {
      // 查找所有实现了 IPlantController 接口的组件
      MonoBehaviour[] allComponents = FindObjectsOfType<MonoBehaviour>();
      int registeredCount = 0;
      
      foreach (var component in allComponents)
      {
          if (component is IPlantController plantController)
          {
              RegisterPlant(plantController);
              registeredCount++;
          }
      }
      
      if (showDebugInfo)
      {
          Debug.Log($"[PlantInteractionManager] 自动注册了 {registeredCount} 个植物控制器");
      }
  }
  
  /// <summary>
  /// 注册植物控制器
  /// </summary>
  public void RegisterPlant(IPlantController plantController)
  {
      if (plantController == null || registeredPlants.Contains(plantController))
      {
          return;
      }
      
      // 检查是否已初始化
      if (!plantController.IsInitialized)
      {
          if (showDebugInfo)
          {
              Debug.LogWarning($"[PlantInteractionManager] 植物控制器未初始化，稍后重试: {plantController.GetTransform().name}");
          }
          // 可以考虑延迟注册
          StartCoroutine(DelayedRegister(plantController));
          return;
      }
      
      registeredPlants.Add(plantController);
      updateQueue.Enqueue(plantController);
      totalRegisteredPlants++;
      
      // 如果启用了全局预设控制，应用当前预设
      if (enableGlobalPresetControl && !isTransitioning)
      {
          plantController.ApplyPreset(currentGlobalPreset);
      }
      
      if (showDebugInfo)
      {
          Debug.Log($"[PlantInteractionManager] 已注册植物: {plantController.GetTransform().name}");
      }
  }
  
  /// <summary>
  /// 延迟注册植物
  /// </summary>
  private IEnumerator DelayedRegister(IPlantController plantController)
  {
      int attempts = 0;
      const int maxAttempts = 10;
      
      while (attempts < maxAttempts)
      {
          yield return new WaitForSeconds(0.1f);
          
          if (plantController != null && plantController.IsInitialized)
          {
              RegisterPlant(plantController);
              yield break;
          }
          
          attempts++;
      }
      
      if (showDebugInfo)
      {
          Debug.LogWarning($"[PlantInteractionManager] 植物控制器注册失败，超过最大尝试次数");
      }
  }
  
  /// <summary>
  /// 注销植物控制器
  /// </summary>
  public void UnregisterPlant(IPlantController plantController)
  {
      if (plantController != null && registeredPlants.Contains(plantController))
      {
          registeredPlants.Remove(plantController);
          
          if (showDebugInfo)
          {
              Debug.Log($"[PlantInteractionManager] 已注销植物: {plantController.GetTransform().name}");
          }
      }
  }
  
    /// <summary>
    /// 清理已销毁的植物
    /// </summary>
    private void CleanupDestroyedPlants()
    {
        // 清理注册列表中已销毁的植物
        int removedCount = registeredPlants.RemoveAll(plant => 
        {
            return plant == null || plant.GetTransform() == null;
        });
        
        // 如果有植物被移除，需要重建更新队列
        if (removedCount > 0)
        {
            RebuildUpdateQueue();
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlantInteractionManager] 清理了 {removedCount} 个已销毁的植物");
            }
        }
    }
    /// <summary>
    /// 重建更新队列
    /// </summary>
    private void RebuildUpdateQueue()
    {
        // 清空当前队列
        updateQueue.Clear();

        // 重新添加所有有效的植物
        foreach (var plant in registeredPlants)
        {
            if (plant != null && plant.GetTransform() != null)
            {
                updateQueue.Enqueue(plant);
            }
        }
    }
    #endregion

    #region 交互效果管理

    /// <summary>
    /// 处理玩家冲击事件
    /// </summary>
    public void HandlePlayerImpact(Vector3 position, float strength, float radius)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[PlantInteractionManager] 玩家冲击: 位置={position}, 强度={strength}, 半径={radius}");
        }

        CreateImpact(position, strength, radius);
    }
  
  /// <summary>
  /// 创建冲击效果
  /// </summary>
private void CreateImpact(Vector3 position, float strength, float radius)
{
    currentImpactPosition = position;
    currentImpactStrength = strength * globalResponseIntensity;
    currentImpactRadius = radius;
    impactStartTime = Time.time;
    hasActiveImpact = true;

        // 添加调试
    if (showDebugInfo)
    {
        Debug.LogFormat($"[PlantInteractionManager] CreateImpact: 设置冲击参数完成");
    }

    // 确保立即应用冲击
    ApplyImpactToAffectedPlants();
}
  
  /// <summary>
  /// 更新交互效果
  /// </summary>
  private void UpdateInteractionEffects()
  {
      if (!hasActiveImpact) return;
      
      float elapsedTime = Time.time - impactStartTime;
      
      // 检查冲击是否应该结束
      if (elapsedTime > impactDuration)
      {
          hasActiveImpact = false;
          ClearAllInteractionEffects();
          return;
      }
      
      // 计算衰减强度
      float fadeProgress = Mathf.Clamp01(elapsedTime / impactFadeTime);
      float currentStrength = currentImpactStrength * (1f - fadeProgress);
      
      // 更新受影响的植物
      ApplyImpactToAffectedPlants(currentStrength);
  }
  
 /// <summary>
  /// 对受影响的植物应用冲击
  /// </summary>
  private void ApplyImpactToAffectedPlants(float strengthMultiplier = 1f)
  {
      if (showDebugInfo)
      {
          Debug.LogFormat($"[PlantInteractionManager] ApplyImpactToAffectedPlants 开始执行，植物数量: {registeredPlants.Count}");
      }

      int affectedCount = 0;
      
      // 创建临时列表以避免在迭代过程中修改集合
      List<IPlantController> validPlants = new List<IPlantController>();
      
      // 先收集所有有效的植物
      foreach (var plant in registeredPlants)
      {
          if (plant != null)
          {
              try
              {
                  Transform plantTransform = plant.GetTransform();
                  if (plantTransform != null)
                  {
                      validPlants.Add(plant);
                  }
              }
              catch (System.Exception)
              {
                  // 忽略错误，这个植物将被下一次CleanupDestroyedPlants移除
              }
          }
      }
      
      // 对有效植物应用效果
      foreach (var plant in validPlants)
      {
          Transform plantTransform = plant.GetTransform();
          
          // 只考虑X和Y方向的距离，忽略Z方向
          float distance = Vector2.Distance(
              new Vector2(plantTransform.position.x, plantTransform.position.y),
              new Vector2(currentImpactPosition.x, currentImpactPosition.y)
          );
          
          if (distance <= currentImpactRadius)
          {
              // 计算影响强度
              float influence = 1f - (distance / currentImpactRadius);
              float finalStrength = currentImpactStrength * influence * strengthMultiplier;

              // 应用交互效果
              plant.ApplyInteractionEffect(currentImpactPosition, finalStrength, currentImpactRadius);
              affectedCount++;
          }
      }

      if (showDebugInfo)
      {
          Debug.LogFormat($"[PlantInteractionManager] ApplyImpactToAffectedPlants 完成，影响了 {affectedCount} 个植物");
      }
  }
  /// <summary>
  /// 清除所有交互效果
  /// </summary>
  private void ClearAllInteractionEffects()
  {
      List<IPlantController> validPlants = new List<IPlantController>();
      
      // 收集有效植物
      foreach (var plant in registeredPlants)
      {
          if (plant != null)
          {
              try
              {
                  if (plant.GetTransform() != null)
                  {
                      validPlants.Add(plant);
                  }
              }
              catch (System.Exception)
              {
                  // 忽略错误
              }
          }
      }
      
      // 清除效果
      foreach (var plant in validPlants)
      {
          plant.ClearInteractionEffect();
      }
      
      if (showDebugInfo)
      {
          Debug.Log("[PlantInteractionManager] 已清除所有交互效果");
      }
  }

    #endregion

    #region 植物更新管理

    /// <summary>
    /// 更新植物状态
    /// </summary>
    private void UpdatePlants()
    {
        plantsUpdatedThisFrame = 0;

        // 清理已销毁的植物
        CleanupDestroyedPlants();

        // 如果没有植物需要更新，直接返回
        if (updateQueue.Count == 0)
        {
            return;
        }

        // 分帧更新植物队列
        int plantsToUpdate = Mathf.Min(maxPlantsPerFrame, updateQueue.Count);
        List<IPlantController> plantsToRequeue = new List<IPlantController>(plantsToUpdate);

        for (int i = 0; i < plantsToUpdate && updateQueue.Count > 0; i++)
        {
            var plant = updateQueue.Dequeue();

            // 更严格的空值检查
            if (plant != null)
            {
                try
                {
                    // 使用 try-catch 防止访问已销毁对象
                    if (plant.GetTransform() != null)
                    {
                        UpdatePlantState(plant);
                        plantsToRequeue.Add(plant); // 收集有效的植物以重新加入队列
                        plantsUpdatedThisFrame++;
                    }
                }
                catch (System.Exception e)
                {
                    if (showDebugInfo)
                    {
                        Debug.LogWarning($"[PlantInteractionManager] 更新植物时出错: {e.Message}");
                    }
                    // 不添加到重新入队列表中
                }
            }
        }

        // 将有效的植物重新加入队列
        foreach (var plant in plantsToRequeue)
        {
            updateQueue.Enqueue(plant);
        }
    }
  
/// <summary>
  /// 更新单个植物状态
  /// </summary>
  private void UpdatePlantState(IPlantController plant)
  {
      // 安全检查
      if (plant == null) return;
      
      Transform plantTransform = null;
      
      try
      {
          plantTransform = plant.GetTransform();
      }
      catch (System.Exception e)
      {
          if (showDebugInfo)
          {
              Debug.LogWarning($"[PlantInteractionManager] 获取植物Transform时出错: {e.Message}");
          }
          return;
      }
      
      if (plantTransform == null) return;
      
      // 距离剔除优化 - 只考虑XY平面距离
      if (enableDistanceCulling && playerTransform != null)
      {
          float distance = Vector2.Distance(
              new Vector2(plantTransform.position.x, plantTransform.position.y),
              new Vector2(playerTransform.position.x, playerTransform.position.y)
          );
          
          if (distance > maxUpdateDistance)
          {
              return; // 距离太远，跳过更新
          }
      }
      
      // 这里可以添加其他植物状态更新逻辑
      // 比如LOD切换、动画状态管理等
  }
  
  #endregion
  
  #region 全局预设管理
  
  /// <summary>
  /// 切换全局个性预设
  /// </summary>
  public void SwitchGlobalPreset(PlantPersonalityPreset preset, float transitionDuration = -1f)
  {
      if (!enableGlobalPresetControl) 
      {
          if (showDebugInfo)
          {
              Debug.LogWarning("[PlantInteractionManager] 全局预设控制已禁用");
          }
          return;
      }
      
      if (preset == currentGlobalPreset && !isTransitioning)
      {
          if (showDebugInfo)
          {
              Debug.Log($"[PlantInteractionManager] 已经是当前预设: {preset}");
          }
          return;
      }
      
      if (transitionDuration < 0)
      {
          transitionDuration = presetTransitionDuration;
      }
      
      // 停止当前过渡
      if (isTransitioning && transitionCoroutine != null)
      {
          StopCoroutine(transitionCoroutine);
      }
      
      // 开始新的过渡
      transitionCoroutine = StartCoroutine(TransitionToPreset(preset, transitionDuration));
      
      if (showDebugInfo)
      {
          Debug.Log($"[PlantInteractionManager] 开始切换到预设: {PlantPersonalityPresets.GetPresetDisplayName(preset)}，过渡时间: {transitionDuration:F1}s");
      }
  }
  
    /// <summary>
    /// 立即应用全局预设（无过渡）
    /// </summary>
    public void ApplyGlobalPreset(PlantPersonalityPreset preset, bool notifyChange = true)
    {
        if (!enableGlobalPresetControl) return;
        
        currentGlobalPreset = preset;
        
        // 清理已销毁的植物
        CleanupDestroyedPlants();
        
        // 创建临时列表以避免在迭代过程中出现问题
        List<IPlantController> validPlants = new List<IPlantController>();
        
        // 先收集所有有效的植物
        foreach (var plant in registeredPlants)
        {
            if (plant != null)
            {
                try
                {
                    if (plant.GetTransform() != null)
                    {
                        validPlants.Add(plant);
                    }
                }
                catch (System.Exception)
                {
                    // 忽略错误
                }
            }
        }
        
        // 应用到所有有效植物
        foreach (var plant in validPlants)
        {
            plant.ApplyPreset(preset);
        }
        
        if (notifyChange)
        {
            OnGlobalPresetChanged?.Invoke(preset);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[PlantInteractionManager] 已应用全局预设: {PlantPersonalityPresets.GetPresetDisplayName(preset)}");
        }
    }
  
  /// <summary>
  /// 预设过渡协程
  /// </summary>
  private IEnumerator TransitionToPreset(PlantPersonalityPreset targetPreset, float duration)
  {
      isTransitioning = true;
      
      // 获取当前和目标预设的设置
      var fromSettings = PlantPersonalityPresets.GetPresetSettings(currentGlobalPreset);
      var toSettings = PlantPersonalityPresets.GetPresetSettings(targetPreset);
      
      float elapsedTime = 0f;
      
      while (elapsedTime < duration)
      {
          float progress = elapsedTime / duration;
          float curveProgress = transitionCurve.Evaluate(progress);
          
          // 插值设置
          var lerpedSettings = PlantPersonalitySettings.Lerp(fromSettings, toSettings, curveProgress);
          
          // 创建临时列表以避免在迭代过程中出现问题
          List<IPlantController> validPlants = new List<IPlantController>();
          
          // 先收集所有有效的植物
          foreach (var plant in registeredPlants)
          {
              if (plant != null)
              {
                  try
                  {
                      if (plant.GetTransform() != null)
                      {
                          validPlants.Add(plant);
                      }
                  }
                  catch (System.Exception)
                  {
                      // 忽略错误，这个植物将在下一次清理中被移除
                  }
              }
          }
          
          // 应用到所有有效植物
          foreach (var plant in validPlants)
          {
              plant.UpdatePersonalitySettings(lerpedSettings);
          }
          
          elapsedTime += Time.deltaTime;
          yield return null;
      }
      
      // 确保最终状态正确，使用安全的方式应用预设
      currentGlobalPreset = targetPreset;
      
      // 清理已销毁的植物，确保最终应用预设时不会出错
      CleanupDestroyedPlants();
      
      // 安全地应用最终预设
      List<IPlantController> finalValidPlants = new List<IPlantController>();
      foreach (var plant in registeredPlants)
      {
          if (plant != null)
          {
              try
              {
                  if (plant.GetTransform() != null)
                  {
                      finalValidPlants.Add(plant);
                  }
              }
              catch (System.Exception)
              {
                  // 忽略错误
              }
          }
      }
      
      foreach (var plant in finalValidPlants)
      {
          plant.ApplyPreset(targetPreset);
      }
      
      if (OnGlobalPresetChanged != null)
      {
          OnGlobalPresetChanged.Invoke(targetPreset);
      }
      
      isTransitioning = false;
      transitionCoroutine = null;
      
      if (showDebugInfo)
      {
          Debug.Log($"[PlantInteractionManager] 预设过渡完成: {PlantPersonalityPresets.GetPresetDisplayName(targetPreset)}");
      }
  }
  
  #endregion
  
  #region 公共接口
  
  /// <summary>
  /// 获取当前全局预设
  /// </summary>
  public PlantPersonalityPreset GetCurrentGlobalPreset()
  {
      return currentGlobalPreset;
  }
  
  /// <summary>
  /// 获取注册的植物数量
  /// </summary>
  public int GetRegisteredPlantCount()
  {
      return registeredPlants.Count;
  }
  
  /// <summary>
  /// 是否正在过渡
  /// </summary>
  public bool IsTransitioning()
  {
      return isTransitioning;
  }
  
  #endregion
  
  #region 调试和可视化
  
  /// <summary>
  /// 更新调试信息
  /// </summary>
  private void UpdateDebugInfo()
  {
      // 这里可以添加调试UI更新逻辑
      // 或者输出性能统计信息
  }
  
  /// <summary>
  /// 绘制调试Gizmos
  /// </summary>
  private void OnDrawGizmos()
  {
      if (!drawGizmos) return;
      
      // 绘制当前冲击范围
      if (hasActiveImpact)
      {
          Gizmos.color = impactGizmoColor;
          Gizmos.DrawWireSphere(currentImpactPosition, currentImpactRadius);
          
          // 绘制冲击强度
          float alpha = currentImpactStrength;
          Gizmos.color = new Color(impactGizmoColor.r, impactGizmoColor.g, impactGizmoColor.b, alpha * 0.3f);
          Gizmos.DrawSphere(currentImpactPosition, currentImpactRadius);
      }
      
      // 绘制更新距离
      if (playerTransform != null && enableDistanceCulling)
      {
          Gizmos.color = Color.yellow;
          Gizmos.DrawWireSphere(playerTransform.position, maxUpdateDistance);
      }
  }
  
  /// <summary>
  /// 绘制选中时的Gizmos
  /// </summary>
  private void OnDrawGizmosSelected()
  {
      if (!showDebugInfo) return;
      
      // 绘制所有注册植物的连线
      Gizmos.color = Color.green;
      foreach (var plant in registeredPlants)
      {
          if (plant != null && plant.GetTransform() != null)
          {
              Gizmos.DrawLine(transform.position, plant.GetTransform().position);
          }
      }
  }
  
  #endregion
}