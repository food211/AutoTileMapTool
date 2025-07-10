using UnityEditor;
using UnityEngine;

/// <summary>
/// 增强植物摆动控制器 - 重构版
/// 专注于应用个性设置和处理交互效果，完全匹配BushSwayInteractive着色器
/// </summary>
public class EnhancedPlantSwayController : MonoBehaviour, IPlantController
{
  [Header("=== 组件引用 ===")]
  [SerializeField] private Renderer plantRenderer;
  [SerializeField] private string expectedShaderName = "Custom/BushSwayInteractive";

  [Header("=== 植物类型 ===")]
  [SerializeField] private PlantType plantType = PlantType.Bush;
  
  [Header("=== 植物个性设置 ===")]
  [SerializeField] private PlantPersonalitySettings personalitySettings;
  [SerializeField] private bool applySettingsOnStart = true;

  [Header("=== 风向设置 ===")]
  [SerializeField] private Vector4 windDirection = new Vector4(1, 0, 0, 0);
  
  [Header("=== 随机化设置 ===")]
  [SerializeField] private RandomizationSettings randomization = new RandomizationSettings();
  
  [Header("=== 交互设置 ===")]
  [SerializeField] private bool enableInteraction = true;
  [SerializeField] private float interactionUpdateRate = 0.1f;
  [SerializeField] private float defaultImpactRadius = 3.0f;
[SerializeField] private Transform interactionPivot; // 新增：自定义交互计算的pivot点
  [SerializeField] private bool useCustomPivot = false; // 新增：是否使用自定义pivot
  
  [Header("=== 调试设置 ===")]
  [SerializeField] private bool showDebugInfo = false;
  [SerializeField] private bool previewInEditor = false;
  
  // 运行时状态
  private MaterialPropertyBlock propertyBlock;
  private bool isInitialized = false;
  private bool settingsChanged = false;
  private float lastUpdateTime = 0f;
  private float lastInteractionTime = -100f; // 初始化为很久以前
  private bool hasClearedInteractionEffect = false;
  
  public Transform GetTransform() => transform;
  
  // 缓存所有 Shader 属性 ID
  private static readonly int TimeOffsetID = Shader.PropertyToID("_TimeOffset");
  private static readonly int RandomSeedID = Shader.PropertyToID("_RandomSeed");
  private static readonly int PhaseVariationID = Shader.PropertyToID("_PhaseVariation");
  private static readonly int WindStrengthID = Shader.PropertyToID("_WindStrength");
  private static readonly int WindFrequencyID = Shader.PropertyToID("_WindFrequency");
  private static readonly int WindDirectionID = Shader.PropertyToID("_WindDirection");
  private static readonly int ElasticAmountID = Shader.PropertyToID("_ElasticAmount");
  private static readonly int ElasticFrequencyID = Shader.PropertyToID("_ElasticFrequency");
  private static readonly int CenterDeadZoneID = Shader.PropertyToID("_CenterDeadZone");
  private static readonly int EdgeInfluenceID = Shader.PropertyToID("_EdgeInfluence");
  private static readonly int ResponseIntensityID = Shader.PropertyToID("_ResponseIntensity");
  private static readonly int LerpSpeedID = Shader.PropertyToID("_LerpSpeed");
  private static readonly int DecaySpeedID = Shader.PropertyToID("_DecaySpeed");
  private static readonly int SmoothingFactorID = Shader.PropertyToID("_SmoothingFactor");
  private static readonly int ResponseCurveStrengthID = Shader.PropertyToID("_ResponseCurveStrength");
  private static readonly int WaveFrequencyID = Shader.PropertyToID("_WaveFrequency");
  private static readonly int WaveDecayID = Shader.PropertyToID("_WaveDecay");
  
  // 交互相关的 Shader 属性 ID
  private static readonly int PlayerPositionID = Shader.PropertyToID("_PlayerPosition");
  private static readonly int ImpactStrengthID = Shader.PropertyToID("_ImpactStrength");
  private static readonly int ImpactRadiusID = Shader.PropertyToID("_ImpactRadius");
  private static readonly int ImpactStartTimeID = Shader.PropertyToID("_ImpactStartTime");
  
  #region Unity 生命周期
  
  void Start()
  {
      InitializeController();
      
      if (applySettingsOnStart)
      {
          ApplyPersonalitySettings();
      }
      
      RegisterToManager();
  }
  
  void Update()
  {
      // 只在设置改变时更新
      if (settingsChanged && Time.time - lastUpdateTime > interactionUpdateRate)
      {
          ApplyPersonalitySettings();
          settingsChanged = false;
          lastUpdateTime = Time.time;
      }
      
      // 自动衰减交互效果
      if (enableInteraction && Time.time - lastInteractionTime > 5.0f)
      {
          // 如果上次交互已经过去5秒，确保交互效果已经完全衰减
          ClearInteractionEffect();
      }
  }
  
  void OnDestroy()
  {
      UnregisterFromManager();
  }
  
  #endregion
  
  #region 初始化
  
  /// <summary>
  /// 初始化控制器
  /// </summary>
  private void InitializeController()
  {
      // 自动获取 Renderer
      if (plantRenderer == null)
      {
          plantRenderer = GetComponent<Renderer>();
          if (plantRenderer == null)
          {
              plantRenderer = GetComponentInChildren<Renderer>();
          }
      }
      
      // 如果没有设置交互pivot，默认使用自身transform
      if (interactionPivot == null)
      {
          interactionPivot = transform;
      }
      
      // 验证组件
      if (!ValidateComponents())
      {
          return;
      }
      
      // 初始化 MaterialPropertyBlock
      if (propertyBlock == null)
      {
          propertyBlock = new MaterialPropertyBlock();
      }
      
      // 应用随机化
      if (randomization.autoRandomizeOnStart)
      {
          ApplyRandomization();
      }
      
      // 初始化风向
      UpdateWindDirection();
      
      isInitialized = true;
      
      if (showDebugInfo)
      {
          Debug.Log($"[{gameObject.name}] 植物摆动控制器初始化完成");
      }
  }
  
  /// <summary>
  /// 验证必要组件
  /// </summary>
  private bool ValidateComponents()
  {
      if (plantRenderer == null)
      {
          Debug.LogError($"[{gameObject.name}] 未找到 Renderer 组件");
          return false;
      }
      
      if (plantRenderer.material == null)
      {
          Debug.LogError($"[{gameObject.name}] Renderer 没有材质");
          return false;
      }
      
      // 检查 Shader
      if (plantRenderer.material.shader.name != expectedShaderName)
      {
          if (showDebugInfo)
          {
              Debug.LogWarning($"[{gameObject.name}] 材质 Shader 不是 {expectedShaderName}，当前是 {plantRenderer.material.shader.name}");
          }
      }
      
      return true;
  }
  
  #endregion
  
  #region 个性设置管理
  
  /// <summary>
  /// 应用个性设置到材质
  /// </summary>
  public void ApplyPersonalitySettings()
  {
      if (!isInitialized || personalitySettings == null)
      {
          if (showDebugInfo)
          {
              Debug.LogWarning($"[{gameObject.name}] 无法应用个性设置：未初始化或设置为空");
          }
          return;
      }
      
      ApplySettingsToMaterial();
      
      if (showDebugInfo)
      {
          Debug.Log($"[{gameObject.name}] 已应用个性设置");
      }
  }
  
  /// <summary>
  /// 将设置应用到材质
  /// </summary>
  private void ApplySettingsToMaterial()
  {
      // 使用 MaterialPropertyBlock 而不是直接修改材质
      // 这样可以避免创建材质实例，更高效
      plantRenderer.GetPropertyBlock(propertyBlock);
      
      // 时间和相位设置
      propertyBlock.SetFloat(TimeOffsetID, personalitySettings.timeOffset);
      propertyBlock.SetFloat(RandomSeedID, personalitySettings.randomSeed);
      propertyBlock.SetFloat(PhaseVariationID, personalitySettings.phaseVariation);
      
      // 风力响应设置
      propertyBlock.SetFloat(WindStrengthID, personalitySettings.windStrength);
      propertyBlock.SetFloat(WindFrequencyID, personalitySettings.windFrequency);
      propertyBlock.SetVector(WindDirectionID, windDirection);
      
      // 弹性特征设置
      propertyBlock.SetFloat(ElasticAmountID, personalitySettings.elasticAmount);
      propertyBlock.SetFloat(ElasticFrequencyID, personalitySettings.elasticFrequency);
      
      // 结构特征设置
      propertyBlock.SetFloat(CenterDeadZoneID, personalitySettings.centerDeadZone);
      propertyBlock.SetFloat(EdgeInfluenceID, personalitySettings.edgeInfluence);
      
      // 交互响应设置
      propertyBlock.SetFloat(ResponseIntensityID, personalitySettings.responseIntensity);
      propertyBlock.SetFloat(LerpSpeedID, personalitySettings.lerpSpeed);
      propertyBlock.SetFloat(DecaySpeedID, personalitySettings.decaySpeed);
      propertyBlock.SetFloat(SmoothingFactorID, personalitySettings.smoothingFactor);
      
      // 波动特征设置
      propertyBlock.SetFloat(ResponseCurveStrengthID, personalitySettings.responseCurveStrength);
      propertyBlock.SetFloat(WaveFrequencyID, personalitySettings.waveFrequency);
      propertyBlock.SetFloat(WaveDecayID, personalitySettings.waveDecay);
      
      // 应用 PropertyBlock
      plantRenderer.SetPropertyBlock(propertyBlock);
  }
  
  /// <summary>
  /// 更新风向
  /// </summary>
  private void UpdateWindDirection()
  {
      if (!isInitialized) return;
      
      plantRenderer.GetPropertyBlock(propertyBlock);
      propertyBlock.SetVector(WindDirectionID, windDirection);
      plantRenderer.SetPropertyBlock(propertyBlock);
  }
  
  /// <summary>
  /// 更新个性设置（外部调用）
  /// </summary>
  public void UpdatePersonalitySettings(PlantPersonalitySettings newSettings)
  {
      if (newSettings == null)
      {
          Debug.LogWarning($"[{gameObject.name}] 尝试设置空的个性设置");
          return;
      }
      
      personalitySettings = newSettings.Clone();
      settingsChanged = true;
      
      if (showDebugInfo)
      {
          Debug.Log($"[{gameObject.name}] 个性设置已更新");
      }
  }
  
  /// <summary>
  /// 应用预设设置
  /// </summary>
  public void ApplyPreset(PlantPersonalityPreset preset)
  {
      var presetSettings = PlantPersonalityPresets.GetPresetSettings(preset);
      UpdatePersonalitySettings(presetSettings);
  }
  
  /// <summary>
  /// 根据植物类型应用默认设置
  /// </summary>
  public void ApplyDefaultSettingsForType()
  {
      switch (plantType)
      {
          case PlantType.Bush:
              ApplyPreset(PlantPersonalityPreset.Gentle);
              break;
          case PlantType.Grass:
              ApplyPreset(PlantPersonalityPreset.Energetic);
              break;
          case PlantType.Flower:
              ApplyPreset(PlantPersonalityPreset.Gentle);
              break;
          case PlantType.Tree:
              ApplyPreset(PlantPersonalityPreset.Sturdy);
              break;
          default:
              ApplyPreset(PlantPersonalityPreset.Gentle);
              break;
      }
  }
  
  #endregion
  
  #region 随机化
  
  /// <summary>
  /// 应用随机化
  /// </summary>
  public void ApplyRandomization()
  {
      if (personalitySettings == null)
      {
          personalitySettings = new PlantPersonalitySettings();
      }
      
      // 生成随机种子
      float seed = randomization.usePositionBasedSeed ? 
          GetPositionBasedSeed() : Random.Range(0f, 100f);
      
      Random.InitState((int)(seed * 1000));
      
      // 应用随机化
      personalitySettings.timeOffset = Random.Range(0f, 10f);
      personalitySettings.randomSeed = seed;
      personalitySettings.phaseVariation = Random.Range(0.5f, 6.28f);
      
      // 风力随机化
      personalitySettings.windStrength *= 1f + Random.Range(-randomization.windRandomness, randomization.windRandomness);
      personalitySettings.windFrequency *= 1f + Random.Range(-randomization.windRandomness, randomization.windRandomness);
      
      // 弹性随机化
      personalitySettings.elasticAmount *= 1f + Random.Range(-randomization.elasticRandomness, randomization.elasticRandomness);
      personalitySettings.elasticFrequency *= 1f + Random.Range(-randomization.elasticRandomness, randomization.elasticRandomness);
      
      // 结构随机化
      personalitySettings.centerDeadZone *= 1f + Random.Range(-randomization.structureRandomness, randomization.structureRandomness);
      personalitySettings.edgeInfluence *= 1f + Random.Range(-randomization.structureRandomness, randomization.structureRandomness);
      
      // 交互响应随机化
      personalitySettings.responseIntensity *= 1f + Random.Range(-randomization.interactionRandomness, randomization.interactionRandomness);
      personalitySettings.lerpSpeed *= 1f + Random.Range(-randomization.interactionRandomness, randomization.interactionRandomness);
      personalitySettings.decaySpeed *= 1f + Random.Range(-randomization.interactionRandomness, randomization.interactionRandomness);
      
      // 波动特征随机化
      personalitySettings.responseCurveStrength *= 1f + Random.Range(-randomization.waveRandomness, randomization.waveRandomness);
      personalitySettings.waveFrequency *= 1f + Random.Range(-randomization.waveRandomness, randomization.waveRandomness);
      personalitySettings.waveDecay *= 1f + Random.Range(-randomization.waveRandomness, randomization.waveRandomness);
      
      // 确保值在合理范围内
      ClampSettings();
      
      settingsChanged = true;
      
      if (showDebugInfo)
      {
          Debug.Log($"[{gameObject.name}] 已应用随机化，种子: {seed}");
      }
  }
  
  /// <summary>
  /// 基于位置生成种子
  /// </summary>
  private float GetPositionBasedSeed()
  {
      Vector3 pos = transform.position;
      return Mathf.Abs(pos.x * 73f + pos.z * 37f + pos.y * 17f) % 100f;
  }
  
  /// <summary>
  /// 限制设置值在合理范围内
  /// </summary>
  private void ClampSettings()
  {
      personalitySettings.timeOffset = Mathf.Clamp(personalitySettings.timeOffset, 0f, 10f);
      personalitySettings.randomSeed = Mathf.Clamp(personalitySettings.randomSeed, 0f, 100f);
      personalitySettings.phaseVariation = Mathf.Clamp(personalitySettings.phaseVariation, 0f, 6.28f);
      
      personalitySettings.windStrength = Mathf.Clamp(personalitySettings.windStrength, 0f, 0.1f);
      personalitySettings.windFrequency = Mathf.Clamp(personalitySettings.windFrequency, 0.5f, 5f);
      
      personalitySettings.elasticAmount = Mathf.Clamp(personalitySettings.elasticAmount, 0f, 0.15f);
      personalitySettings.elasticFrequency = Mathf.Clamp(personalitySettings.elasticFrequency, 2f, 8f);
      
      personalitySettings.centerDeadZone = Mathf.Clamp(personalitySettings.centerDeadZone, 0f, 0.5f);
      personalitySettings.edgeInfluence = Mathf.Clamp(personalitySettings.edgeInfluence, 0.3f, 0.8f);
      
      personalitySettings.responseIntensity = Mathf.Clamp(personalitySettings.responseIntensity, 0f, 1f);
      personalitySettings.lerpSpeed = Mathf.Clamp(personalitySettings.lerpSpeed, 1f, 20f);
      personalitySettings.decaySpeed = Mathf.Clamp(personalitySettings.decaySpeed, 0.5f, 8f);
      personalitySettings.smoothingFactor = Mathf.Clamp(personalitySettings.smoothingFactor, 0.05f, 0.5f);
      
      personalitySettings.responseCurveStrength = Mathf.Clamp(personalitySettings.responseCurveStrength, 0.5f, 3f);
      personalitySettings.waveFrequency = Mathf.Clamp(personalitySettings.waveFrequency, 2f, 12f);
      personalitySettings.waveDecay = Mathf.Clamp(personalitySettings.waveDecay, 1f, 6f);
  }
  
  #endregion
  
  #region 交互效果
  
  /// <summary>
  /// 应用交互效果
  /// </summary>
  public void ApplyInteractionEffect(Vector3 playerPosition, float strength, float radius = -1)
  {
      if (!enableInteraction || !isInitialized)
      {
          return;
      }
      hasClearedInteractionEffect = false; // 重置清除状态
      
      // 使用默认半径如果未指定
      if (radius < 0) radius = defaultImpactRadius;
      
      // 获取用于计算的位置点（使用自定义pivot或默认transform）
      Vector3 pivotPosition = useCustomPivot && interactionPivot != null ? 
          interactionPivot.position : transform.position;
      
      // 使用 MaterialPropertyBlock 来设置交互参数
      plantRenderer.GetPropertyBlock(propertyBlock);
      propertyBlock.SetVector(PlayerPositionID, new Vector4(playerPosition.x, playerPosition.y, playerPosition.z, 0));
      // 添加pivot位置信息，shader中需要使用这个位置而不是物体中心
      propertyBlock.SetVector("_PivotPosition", new Vector4(pivotPosition.x, pivotPosition.y, pivotPosition.z, 0));
      // 设置是否使用自定义pivot的标志
      propertyBlock.SetFloat("_UseCustomPivot", useCustomPivot ? 1.0f : 0.0f);
      propertyBlock.SetFloat(ImpactStrengthID, strength);
      propertyBlock.SetFloat(ImpactRadiusID, radius);
      propertyBlock.SetFloat(ImpactStartTimeID, Time.time);
      plantRenderer.SetPropertyBlock(propertyBlock);
      
      // 记录最后交互时间
      lastInteractionTime = Time.time;
      
      if (showDebugInfo)
      {
          Debug.Log($"[{gameObject.name}] 应用交互效果: 位置={playerPosition}, 强度={strength}, 半径={radius}, 使用自定义Pivot={useCustomPivot}");
      }
  }
  
  /// <summary>
  /// 清除交互效果
  /// </summary>
  public void ClearInteractionEffect()
  {
      if (!isInitialized || hasClearedInteractionEffect)
      {
          return;
      }
      
      plantRenderer.GetPropertyBlock(propertyBlock);
      propertyBlock.SetFloat(ImpactStrengthID, 0f);
      plantRenderer.SetPropertyBlock(propertyBlock);

      hasClearedInteractionEffect = true;
      
      if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] 清除交互效果");
        }
  }
  
  /// <summary>
  /// 设置风向
  /// </summary>
  public void SetWindDirection(Vector3 direction)
  {
      windDirection = new Vector4(direction.x, direction.y, direction.z, 0);
      UpdateWindDirection();
  }
  
  #endregion
  
  #region 管理器注册
  
  /// <summary>
  /// 注册到管理器
  /// </summary>
  private void RegisterToManager()
  {
      var manager = FindObjectOfType<PlantInteractionManager>();
      if (manager != null)
      {
          manager.RegisterPlant(this);
          if (showDebugInfo)
          {
              Debug.Log($"[{gameObject.name}] 已注册到植物交互管理器");
          }
      }
  }
  
  /// <summary>
  /// 从管理器注销
  /// </summary>
  private void UnregisterFromManager()
  {
      var manager = FindObjectOfType<PlantInteractionManager>();
      if (manager != null)
      {
          manager.UnregisterPlant(this);
          if (showDebugInfo)
          {
              Debug.Log($"[{gameObject.name}] 已从植物交互管理器注销");
          }
      }
  }
  
  #endregion
  
  #region 公共接口
  
  /// <summary>
  /// 获取当前个性设置
  /// </summary>
  public PlantPersonalitySettings GetPersonalitySettings()
  {
      return personalitySettings?.Clone();
  }
  
  /// <summary>
  /// 检查是否已初始化
  /// </summary>
  public bool IsInitialized => isInitialized;
  
  /// <summary>
  /// 获取植物渲染器
  /// </summary>
  public Renderer GetRenderer() => plantRenderer;

  /// <summary>
  /// 获取植物类型
  /// </summary>
  public PlantType GetPlantType()
  {
      return plantType;
  }

  /// <summary>
  /// 设置植物类型
  /// </summary>
  public void SetPlantType(PlantType newPlantType)
  {
      plantType = newPlantType;
      
      // 可选：根据新类型应用默认设置
      ApplyDefaultSettingsForType();
      
      // 标记设置已更改，触发更新
      settingsChanged = true;
  }
  
  /// <summary>
  /// 获取当前风向
  /// </summary>
  public Vector3 GetWindDirection()
  {
      return new Vector3(windDirection.x, windDirection.y, windDirection.z);
  }
  
  /// <summary>
  /// 获取交互半径
  /// </summary>
  public float GetInteractionRadius()
  {
      return defaultImpactRadius;
  }

  #endregion

  #region 编辑器支持

#if UNITY_EDITOR
  private void OnValidate()
  {
      if (previewInEditor && Application.isPlaying && isInitialized)
      {
          settingsChanged = true;
      }
      
      // 确保在编辑器中设置pivot时的逻辑正确
      if (interactionPivot == null)
      {
          useCustomPivot = false;
      }
  }

    void OnDrawGizmos()
    {
        // 从正确的pivot点绘制
        Vector3 pivotPos = useCustomPivot && interactionPivot != null ? 
        interactionPivot.position : transform.position;
        Gizmos.DrawSphere(pivotPos, 0.1f);

        // 如果使用自定义pivot，绘制连接线
        if (useCustomPivot && interactionPivot != null && interactionPivot != transform)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, interactionPivot.position);
        }
    }
    private void OnDrawGizmosSelected()
    {
        if (showDebugInfo)
        {
            // 绘制交互半径
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.3f); // 半透明黄色

            // 绘制风向
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.8f); // 蓝色
            Vector3 windDir = new Vector3(windDirection.x, windDirection.y, windDirection.z).normalized;
            Gizmos.DrawRay(transform.position, windDir * 2f);

            // 绘制植物信息
            if (personalitySettings != null)
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                    $"Type: {plantType}\n" +
                    $"Wind: {personalitySettings.windStrength:F3}\n" +
                    $"Elastic: {personalitySettings.elasticAmount:F3}\n" +
                    $"Response: {personalitySettings.responseIntensity:F2}\n" +
                    $"Custom Pivot: {(useCustomPivot ? "Yes" : "No")}");
            }
        }
    }
#endif
  
  #endregion
}