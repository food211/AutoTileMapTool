using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 增强植物摆动控制器 - 批处理优化版
/// 使用固定参数组来提高GPU Instancing效率
/// </summary>
public class EnhancedPlantSwayController : MonoBehaviour, IPlantController
{
    [Header("=== 组件引用 ===")]
    [SerializeField] private Renderer plantRenderer;
    [SerializeField] private string expectedShaderName = "Custom/BushSwayInteractive";

    [Header("=== 植物类型 ===")]
    [SerializeField] private PlantType plantType = PlantType.Bush;

    [Header("=== 颜色设置 ===")]
    [SerializeField] private Color plantColor = Color.white;
    [SerializeField] private bool useSharedColorMaterial = true;
    [SerializeField] private string colorVariantName = "Default"; // 颜色变体名称，如"Green", "Red"等

    [Header("=== 植物个性设置 ===")]
    [SerializeField] private PlantPersonalitySettings personalitySettings;
    [SerializeField] private bool applySettingsOnStart = true;
    [SerializeField] private PlantPersonalityPreset personalityPreset = PlantPersonalityPreset.Gentle; // 使用预设枚举

    [Header("=== 风向设置 ===")]
    [SerializeField] private Vector4 windDirection = new Vector4(1, 0, 0, 0);

    [Header("=== 交互设置 ===")]
    [SerializeField] private bool enableInteraction = true;
    [SerializeField] private float interactionUpdateRate = 0.1f;
    [SerializeField] private float defaultImpactRadius = 3.0f;
    [SerializeField] private Transform interactionPivot; // 自定义交互计算的pivot点
    [SerializeField] private bool useCustomPivot = false; // 是否使用自定义pivot

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
    private Material sharedColorMaterial;
    private ParameterGroup currentParameterGroup;

    public Transform GetTransform()
    {
        try
        {
            return transform;
        }
        catch (System.Exception)
        {
            // 如果访问transform属性时出现异常，返回null
            return null;
        }
    }

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
    private static readonly int PlayerPositionID = Shader.PropertyToID("_PlayerPosition");
    private static readonly int ImpactStrengthID = Shader.PropertyToID("_ImpactStrength");
    private static readonly int ImpactRadiusID = Shader.PropertyToID("_ImpactRadius");
    private static readonly int ImpactStartTimeID = Shader.PropertyToID("_ImpactStartTime");
    private static readonly int PivotPositionID = Shader.PropertyToID("_PivotPosition");
    private static readonly int UseCustomPivotID = Shader.PropertyToID("_UseCustomPivot");
    private static readonly int WholeObjectMovementID = Shader.PropertyToID("_WholeObjectMovement");
    private static readonly int WholeObjectRotationID = Shader.PropertyToID("_WholeObjectRotation");
    private static readonly int VertexDeformStrengthID = Shader.PropertyToID("_VertexDeformStrength");
    private static readonly int VertexDeformSpreadID = Shader.PropertyToID("_VertexDeformSpread");
    private static readonly int ColorID = Shader.PropertyToID("_Color"); 

    // 颜色材质缓存
    private static Dictionary<string, Material> colorMaterialCache = new Dictionary<string, Material>();

    #region Unity 生命周期

    void Start()
    {
        InitializeController();

        if (applySettingsOnStart)
        {
            ApplyPersonalitySettings();
        }

        RegisterToManager();

        #if UNITY_EDITOR
        if (showDebugInfo)
        {
            DebugGPUInstancingStatus();
        }
        #endif
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
        
        // 处理共享材质
        if (useSharedColorMaterial)
        {
            ApplySharedColorMaterial();
        }
        else
        {
            // 确保启用GPU Instancing
            EnsureGPUInstancingEnabled();
        }

        // 使用预设参数
        ApplyPresetParameters();

        // 初始化风向
        UpdateWindDirection();

        InitializePivotSettings();

        isInitialized = true;

        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] 植物摆动控制器初始化完成");
        }
    }

    /// <summary>
    /// 应用共享颜色材质
    /// </summary>
    private void ApplySharedColorMaterial()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // 编辑器模式下使用PropertyBlock预览颜色
            plantRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorID, plantColor);
            plantRenderer.SetPropertyBlock(propertyBlock);
            return;
        }
#endif

        string materialKey = colorVariantName + "_" + plantColor.ToString();

        // 尝试从缓存获取材质
        if (!colorMaterialCache.TryGetValue(materialKey, out sharedColorMaterial))
        {
            // 缓存中没有，尝试加载
            string materialPath = $"Assets/Resources/Materials/BushSway_{colorVariantName}.mat";
            Material baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (baseMaterial == null)
            {
                // 找不到特定颜色变体，使用默认材质
                baseMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/BushSway.mat");

                if (baseMaterial == null)
                {
                    Debug.LogWarning($"找不到植物材质，将使用当前材质并应用颜色");
                    // 使用PropertyBlock应用颜色
                    plantRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(ColorID, plantColor);
                    plantRenderer.SetPropertyBlock(propertyBlock);
                    return;
                }
            }

            // 创建材质实例并设置颜色
            sharedColorMaterial = new Material(baseMaterial);
            sharedColorMaterial.name = $"BushSway_{colorVariantName}_{plantColor.ToString()}";
            sharedColorMaterial.color = plantColor;

            // 添加到缓存
            colorMaterialCache[materialKey] = sharedColorMaterial;
        }

        // 应用共享材质
        plantRenderer.sharedMaterial = sharedColorMaterial;

        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] 应用共享颜色材质: {materialKey}, 颜色: {plantColor}");
        }
        
    }

    private void EnsureGPUInstancingEnabled()
    {
        if (plantRenderer != null && plantRenderer.material != null)
        {
            plantRenderer.material.enableInstancing = true;

            if (showDebugInfo)
            {
                Debug.Log($"[{gameObject.name}] GPU Instancing 已启用: {plantRenderer.material.enableInstancing}");
            }
        }
    }

    private void InitializePivotSettings()
    {
        if (plantRenderer == null) return;

        Vector3 pivotPosition = useCustomPivot && interactionPivot != null ?
            interactionPivot.position : transform.position;

        plantRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(PivotPositionID, new Vector4(pivotPosition.x, pivotPosition.y, pivotPosition.z, 0));
        propertyBlock.SetFloat(UseCustomPivotID, useCustomPivot ? 1.0f : 0.0f);
        plantRenderer.SetPropertyBlock(propertyBlock);
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

    #region 预设参数管理

    /// <summary>
    /// 应用预设参数
    /// </summary>
    private void ApplyPresetParameters()
    {
        // 获取预设设置
        PlantPersonalitySettings presetSettings = PlantPersonalityPresets.GetPresetSettings(personalityPreset);
        
        // 如果没有个性设置，创建一个
        if (personalitySettings == null)
        {
            personalitySettings = presetSettings;
        }
        else
        {
            // 复制预设设置到当前设置
            personalitySettings = presetSettings.Clone();
        }
        
        // 获取参数组（用于批处理优化）
        currentParameterGroup = PlantPersonalityPresets.GetPresetParameterGroup(personalityPreset);
        
        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] 应用预设参数: {PlantPersonalityPresets.GetPresetDisplayName(personalityPreset)}");
        }
    }

    #endregion

    #region 个性设置管理

    /// <summary>
    /// 应用个性设置到材质
    /// </summary>
    public void ApplyPersonalitySettings()
    {
        if (personalitySettings == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[{gameObject.name}] 无法应用个性设置：设置为空");
            }
            return;
        }

        ApplyPresetParameters();       

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
        if (plantRenderer == null) return;

        // 使用 MaterialPropertyBlock 而不是直接修改材质
        plantRenderer.GetPropertyBlock(propertyBlock);

        // 只在不使用预设参数时设置这些参数，或者在编辑器预览模式
        if (previewInEditor && !Application.isPlaying)
        {
            // 时间和相位设置
            propertyBlock.SetFloat(TimeOffsetID, personalitySettings.timeOffset);
            propertyBlock.SetFloat(RandomSeedID, personalitySettings.randomSeed);
            propertyBlock.SetFloat(PhaseVariationID, personalitySettings.phaseVariation);

            // 风力响应设置
            propertyBlock.SetFloat(WindStrengthID, personalitySettings.windStrength);
            propertyBlock.SetFloat(WindFrequencyID, personalitySettings.windFrequency);
            
            // 弹性特征设置
            propertyBlock.SetFloat(ElasticAmountID, personalitySettings.elasticAmount);
            propertyBlock.SetFloat(ElasticFrequencyID, personalitySettings.elasticFrequency);

            // 结构特征设置
            propertyBlock.SetFloat(CenterDeadZoneID, personalitySettings.centerDeadZone);
            propertyBlock.SetFloat(EdgeInfluenceID, personalitySettings.edgeInfluence);
        }
        else if (currentParameterGroup != null)
        {
            // 使用参数组中的批处理关键参数
            propertyBlock.SetFloat(TimeOffsetID, currentParameterGroup.timeOffset);
            propertyBlock.SetFloat(RandomSeedID, currentParameterGroup.randomSeed);
            propertyBlock.SetFloat(PhaseVariationID, currentParameterGroup.phaseVariation);
            propertyBlock.SetFloat(WindStrengthID, currentParameterGroup.windStrength);
            propertyBlock.SetFloat(WindFrequencyID, currentParameterGroup.windFrequency);
            propertyBlock.SetFloat(ElasticAmountID, currentParameterGroup.elasticAmount);
            propertyBlock.SetFloat(ElasticFrequencyID, currentParameterGroup.elasticFrequency);
            propertyBlock.SetFloat(CenterDeadZoneID, currentParameterGroup.centerDeadZone);
            propertyBlock.SetFloat(EdgeInfluenceID, currentParameterGroup.edgeInfluence);
        }

        // 风向设置 - 这个通常是全局的，不会影响批处理
        propertyBlock.SetVector(WindDirectionID, windDirection);

        // 这些参数对批处理影响较小，可以保持个性化
        // 交互响应设置
        propertyBlock.SetFloat(ResponseIntensityID, personalitySettings.responseIntensity);
        propertyBlock.SetFloat(LerpSpeedID, personalitySettings.lerpSpeed);
        propertyBlock.SetFloat(DecaySpeedID, personalitySettings.decaySpeed);
        propertyBlock.SetFloat(SmoothingFactorID, personalitySettings.smoothingFactor);

        // 波动特征设置
        propertyBlock.SetFloat(ResponseCurveStrengthID, personalitySettings.responseCurveStrength);
        propertyBlock.SetFloat(WaveFrequencyID, personalitySettings.waveFrequency);
        propertyBlock.SetFloat(WaveDecayID, personalitySettings.waveDecay);

        propertyBlock.SetFloat(WholeObjectMovementID, personalitySettings.wholeObjectMovement);
        propertyBlock.SetFloat(WholeObjectRotationID, personalitySettings.wholeObjectRotation);
        propertyBlock.SetFloat(VertexDeformStrengthID, personalitySettings.vertexDeformStrength);
        propertyBlock.SetFloat(VertexDeformSpreadID, personalitySettings.vertexDeformSpread);

        // 只在不使用共享颜色材质时设置颜色
        if (!useSharedColorMaterial && plantColor != Color.white)
        {
            propertyBlock.SetColor(ColorID, plantColor);
        }

        // 应用 PropertyBlock
        plantRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>
    /// 更新风向
    /// </summary>
    private void UpdateWindDirection()
    {
        if (plantRenderer == null) return;

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
        personalityPreset = preset;
        ApplyPresetParameters();
        settingsChanged = true;
    }

    /// <summary>
    /// 根据植物类型应用默认设置
    /// </summary>
    public void ApplyDefaultSettingsForType()
    {
        personalityPreset = PlantPersonalityPresets.GetDefaultPresetForPlantType(plantType);
        ApplyPresetParameters();
        settingsChanged = true;
    }

    #endregion

    #region 交互效果

    /// <summary>
    /// 应用交互效果
    /// </summary>
    public void ApplyInteractionEffect(Vector3 playerPosition, float strength, float radius = -1)
    {
        if (!enableInteraction || plantRenderer == null)
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
        propertyBlock.SetVector(PivotPositionID, new Vector4(pivotPosition.x, pivotPosition.y, pivotPosition.z, 0));
        propertyBlock.SetFloat(UseCustomPivotID, useCustomPivot ? 1.0f : 0.0f);
        propertyBlock.SetFloat(ImpactStrengthID, strength);
        propertyBlock.SetFloat(ImpactRadiusID, radius);
        propertyBlock.SetFloat(ImpactStartTimeID, Time.time);
        plantRenderer.SetPropertyBlock(propertyBlock);

        // 记录最后交互时间
        lastInteractionTime = Time.time;

        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] 应用交互效果: 位置={playerPosition}, 强度={strength}, 半径={radius}");
        }
    }

    /// <summary>
    /// 清除交互效果
    /// </summary>
    public void ClearInteractionEffect()
    {
        if (plantRenderer == null || hasClearedInteractionEffect)
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

        // 根据新类型应用默认设置
        ApplyDefaultSettingsForType();

        // 标记设置已更改，触发更新
        settingsChanged = true;
    }

    #endregion

    #region 编辑器支持

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 确保有渲染器
        if (plantRenderer == null)
        {
            plantRenderer = GetComponent<Renderer>();
            if (plantRenderer == null)
            {
                plantRenderer = GetComponentInChildren<Renderer>();
                if (plantRenderer == null)
                    return; // 如果没有找到渲染器，直接返回
            }
        }

        // 初始化 PropertyBlock（如果尚未初始化）
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        // 应用颜色设置以便在编辑器中预览
        if (!useSharedColorMaterial || !Application.isPlaying)
        {
            plantRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorID, plantColor);
            plantRenderer.SetPropertyBlock(propertyBlock);
        }

        // 在编辑器中预览预设参数
        if (Application.isPlaying && isInitialized)
        {
            ApplyPresetParameters();
            ApplySettingsToMaterial();
        }

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

    [ContextMenu("刷新颜色设置")]
    public void RefreshColorInEditor()
    {
        if (plantRenderer == null)
        {
            plantRenderer = GetComponent<Renderer>();
            if (plantRenderer == null)
            {
                plantRenderer = GetComponentInChildren<Renderer>();
                if (plantRenderer == null)
                {
                    Debug.LogError("找不到渲染器组件");
                    return;
                }
            }
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (useSharedColorMaterial && Application.isPlaying)
        {
            ApplySharedColorMaterial();
        }
        else
        {
            plantRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorID, plantColor);
            plantRenderer.SetPropertyBlock(propertyBlock);
        }
        
        Debug.Log($"已刷新 {gameObject.name} 的颜色设置为 {plantColor}");
    }

    [ContextMenu("切换预设参数模式")]
    public void TogglePresetParametersMode()
    {

        ApplyPresetParameters();
        ApplySettingsToMaterial();
        Debug.Log($"已启用预设参数模式，使用预设 {PlantPersonalityPresets.GetPresetDisplayName(personalityPreset)}");
        Debug.Log("已禁用预设参数模式，使用自定义参数");
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
            // 绘制风向
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.8f); // 蓝色
            Vector3 windDir = new Vector3(windDirection.x, windDirection.y, windDirection.z).normalized;
            Gizmos.DrawRay(transform.position, windDir * 2f);

            // 绘制植物信息
            if (personalitySettings != null)
            {
                // 现在总是使用预设参数，所以直接显示预设名称
                string presetInfo = $"预设: {PlantPersonalityPresets.GetPresetDisplayName(personalityPreset)}";

                string colorInfo = useSharedColorMaterial ?
                    $"共享颜色: {colorVariantName}" :
                    $"自定义颜色: {plantColor}";

                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                    $"类型: {plantType}\n" +
                    $"{presetInfo}\n" +
                    $"{colorInfo}\n" +
                    $"风力: {personalitySettings.windStrength:F3}\n" +
                    $"弹性: {personalitySettings.elasticAmount:F3}");
            }
        }
    }

    private void DebugGPUInstancingStatus()
    {
        if (plantRenderer != null && plantRenderer.material != null)
        {
            bool materialInstancingEnabled = plantRenderer.material.enableInstancing;

            Debug.Log($"[{gameObject.name}] GPU Instancing 状态:\n" +
                     $"材质启用Instancing: {materialInstancingEnabled}\n" +
                     $"使用的Shader: {plantRenderer.material.shader.name}\n" +
                     $"使用预设参数: {personalityPreset})\n" +
                     $"使用共享颜色材质: {useSharedColorMaterial} ({colorVariantName})");

            // 检查是否使用共享材质
            if (plantRenderer.sharedMaterial != plantRenderer.material)
            {
                Debug.LogWarning($"[{gameObject.name}] 使用了材质实例而不是共享材质，这可能会阻止GPU Instancing");
            }
        }
    }
#endif

    [MenuItem("Tools/Plant Sway/Reset All Plant Materials")]
    public static void ResetAllPlantMaterials()
    {
        // 找到参考材质
        Material referenceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/BushSway.mat");
        if (referenceMaterial == null)
        {
            Debug.LogError("找不到参考材质!");
            return;
        }

        // 应用到所有植物
        var allPlants = FindObjectsOfType<EnhancedPlantSwayController>();
        foreach (var plant in allPlants)
        {
            plant.ForceSharedMaterial(referenceMaterial);
        }

        Debug.Log($"已重置 {allPlants.Length} 个植物的材质");
    }

    [MenuItem("Tools/Plant Sway/Enable Preset Parameters For All")]
    public static void EnablePresetParametersForAll()
    {
        var allPlants = FindObjectsOfType<EnhancedPlantSwayController>();
        int count = 0;
        
        foreach (var plant in allPlants)
        {
            plant.ApplyPresetParameters();
            plant.ApplySettingsToMaterial();
            count++;
        }
        
        Debug.Log($"已为 {count} 个植物启用预设参数模式");
    }

    public void ForceSharedMaterial(Material sharedMaterial)
    {
        if (plantRenderer != null && sharedMaterial != null)
        {
            plantRenderer.sharedMaterial = sharedMaterial;

            // 重新应用设置
            ApplyPersonalitySettings();

            if (showDebugInfo)
            {
                Debug.Log($"[{gameObject.name}] 强制使用共享材质: {sharedMaterial.name}");
            }
        }
    }

    #endregion
}