using System.Collections.Generic;
using UnityEngine;

public enum PlantPersonalityPreset
{
  Gentle,     // 温和
  Energetic,  // 活跃
  Sturdy,     // 坚固
  Delicate,   // 精致
  Wild        // 狂野
}

public enum PlantType
{
  Bush,       // 灌木
  Grass,      // 草
  Flower,     // 花
  Tree,       // 小树
  Fern        // 蕨类
}

[System.Serializable]
public class PlantPersonalitySettings
{
  [Header("=== 时间和相位个性化 ===")]
  [Range(0f, 10f)]
  [Tooltip("时间偏移 - 让植物摆动不同步")]
  public float timeOffset = 0f;
  
  [Range(0f, 100f)]
  [Tooltip("随机种子 - 影响植物的整体随机性")]
  public float randomSeed = 0f;
  
  [Range(0f, 6.28f)]
  [Tooltip("相位变化 - 影响摆动的相位差")]
  public float phaseVariation = 1f;
  
  [Header("=== 风力响应个性化 ===")]
  [Range(0f, 0.1f)]
  [Tooltip("风力强度 - 植物对风的敏感度")]
  public float windStrength = 0.02f;
  
  [Range(0.5f, 5f)]
  [Tooltip("风力频率 - 植物响应风的频率")]
  public float windFrequency = 2f;
  
  [Header("=== 弹性特征个性化 ===")]
  [Range(0f, 0.15f)]
  [Tooltip("弹性量 - 植物的柔韧程度")]
  public float elasticAmount = 0.06f;
  
  [Range(2f, 8f)]
  [Tooltip("弹性频率 - 植物弹性变形的频率")]
  public float elasticFrequency = 4f;
  
  [Header("=== 结构特征个性化 ===")]
  [Range(0f, 0.5f)]
  [Tooltip("中心死区 - 植物根部不动的范围")]
  public float centerDeadZone = 0.2f;
  
  [Range(0.3f, 0.8f)]
  [Tooltip("边缘影响 - 植物边缘受影响的程度")]
  public float edgeInfluence = 0.5f;
  
  [Header("=== 交互响应个性化 ===")]
  [Range(0f, 1f)]
  [Tooltip("响应强度 - 对玩家交互的敏感度")]
  public float responseIntensity = 1f;
  
  [Range(1f, 20f)]
  [Tooltip("响应速度 - 响应玩家交互的速度")]
  public float lerpSpeed = 8f;
  
  [Range(0.5f, 8f)]
  [Tooltip("衰减速度 - 交互效果消失的速度")]
  public float decaySpeed = 4f;
  
  [Range(0.05f, 0.5f)]
  [Tooltip("平滑因子 - 交互效果的平滑程度")]
  public float smoothingFactor = 0.15f;
  
  [Header("=== 波动特征个性化 ===")]
  [Range(0.5f, 3f)]
  [Tooltip("响应曲线强度 - 响应曲线的陡峭程度")]
  public float responseCurveStrength = 1.5f;
  
  [Range(2f, 12f)]
  [Tooltip("波动频率 - 交互波动的频率")]
  public float waveFrequency = 6f;
  
  [Range(1f, 6f)]
  [Tooltip("波动衰减 - 波动效果的衰减速度")]
  public float waveDecay = 3f;
  
  /// <summary>
  /// 复制设置
  /// </summary>
  public PlantPersonalitySettings Clone()
  {
      return new PlantPersonalitySettings
      {
          timeOffset = this.timeOffset,
          randomSeed = this.randomSeed,
          phaseVariation = this.phaseVariation,
          windStrength = this.windStrength,
          windFrequency = this.windFrequency,
          elasticAmount = this.elasticAmount,
          elasticFrequency = this.elasticFrequency,
          centerDeadZone = this.centerDeadZone,
          edgeInfluence = this.edgeInfluence,
          responseIntensity = this.responseIntensity,
          lerpSpeed = this.lerpSpeed,
          decaySpeed = this.decaySpeed,
          smoothingFactor = this.smoothingFactor,
          responseCurveStrength = this.responseCurveStrength,
          waveFrequency = this.waveFrequency,
          waveDecay = this.waveDecay
      };
  }
  
  /// <summary>
  /// 线性插值到另一个设置
  /// </summary>
  public static PlantPersonalitySettings Lerp(PlantPersonalitySettings from, PlantPersonalitySettings to, float t)
  {
      if (from == null || to == null) return null;
      
      return new PlantPersonalitySettings
      {
          timeOffset = Mathf.Lerp(from.timeOffset, to.timeOffset, t),
          randomSeed = Mathf.Lerp(from.randomSeed, to.randomSeed, t),
          phaseVariation = Mathf.Lerp(from.phaseVariation, to.phaseVariation, t),
          windStrength = Mathf.Lerp(from.windStrength, to.windStrength, t),
          windFrequency = Mathf.Lerp(from.windFrequency, to.windFrequency, t),
          elasticAmount = Mathf.Lerp(from.elasticAmount, to.elasticAmount, t),
          elasticFrequency = Mathf.Lerp(from.elasticFrequency, to.elasticFrequency, t),
          centerDeadZone = Mathf.Lerp(from.centerDeadZone, to.centerDeadZone, t),
          edgeInfluence = Mathf.Lerp(from.edgeInfluence, to.edgeInfluence, t),
          responseIntensity = Mathf.Lerp(from.responseIntensity, to.responseIntensity, t),
          lerpSpeed = Mathf.Lerp(from.lerpSpeed, to.lerpSpeed, t),
          decaySpeed = Mathf.Lerp(from.decaySpeed, to.decaySpeed, t),
          smoothingFactor = Mathf.Lerp(from.smoothingFactor, to.smoothingFactor, t),
          responseCurveStrength = Mathf.Lerp(from.responseCurveStrength, to.responseCurveStrength, t),
          waveFrequency = Mathf.Lerp(from.waveFrequency, to.waveFrequency, t),
          waveDecay = Mathf.Lerp(from.waveDecay, to.waveDecay, t)
      };
  }
}

[System.Serializable]
public class RandomizationSettings
{
  [Header("=== 随机化控制 ===")]
  public bool autoRandomizeOnStart = true;
  public bool usePositionBasedSeed = true;
  
  [Header("=== 随机化范围 ===")]
  [Range(0f, 1f)]
  public float timeOffsetRandomness = 1f;
  
  [Range(0f, 1f)]
  public float windRandomness = 0.2f;
  
  [Range(0f, 1f)]
  public float elasticRandomness = 0.2f;
  
  [Range(0f, 1f)]
  public float structureRandomness = 0.1f;
  
  [Range(0f, 1f)]
  public float interactionRandomness = 0.15f;
  
  [Range(0f, 1f)]
  public float waveRandomness = 0.2f;
}

// ===== IPlantController.cs (新增接口) =====
/// <summary>
/// 植物控制器接口
/// </summary>
public interface IPlantController
{
  /// <summary>
  /// 应用个性设置
  /// </summary>
  void UpdatePersonalitySettings(PlantPersonalitySettings settings);
  
  /// <summary>
  /// 应用预设
  /// </summary>
  void ApplyPreset(PlantPersonalityPreset preset);
  
  /// <summary>
  /// 应用交互效果
  /// </summary>
  void ApplyInteractionEffect(Vector3 playerPosition, float strength, float radius);
  
  /// <summary>
  /// 清除交互效果
  /// </summary>
  void ClearInteractionEffect();
  
  /// <summary>
  /// 获取当前设置
  /// </summary>
  PlantPersonalitySettings GetPersonalitySettings();
  
  /// <summary>
  /// 获取变换组件
  /// </summary>
  Transform GetTransform();
  
  /// <summary>
  /// 是否已初始化
  /// </summary>
  bool IsInitialized { get; }
}

/// <summary>
/// 植物个性预设静态类
/// 提供预定义的植物个性设置
/// </summary>
public static class PlantPersonalityPresets
{
  // 缓存预设设置，避免重复创建
  private static Dictionary<PlantPersonalityPreset, PlantPersonalitySettings> presetCache = 
      new Dictionary<PlantPersonalityPreset, PlantPersonalitySettings>();
  
  /// <summary>
  /// 获取指定预设的设置
  /// </summary>
  public static PlantPersonalitySettings GetPresetSettings(PlantPersonalityPreset preset)
  {
      // 先检查缓存
      if (presetCache.ContainsKey(preset))
      {
          return presetCache[preset].Clone();
      }
      
      // 创建新的预设设置
      PlantPersonalitySettings settings = CreatePresetSettings(preset);
      
      // 缓存设置
      if (settings != null)
      {
          presetCache[preset] = settings.Clone();
      }
      
      return settings;
  }
  
  /// <summary>
  /// 创建预设设置
  /// </summary>
  private static PlantPersonalitySettings CreatePresetSettings(PlantPersonalityPreset preset)
  {
      switch (preset)
      {
          case PlantPersonalityPreset.Gentle:
              return GetGentlePreset();
          
          case PlantPersonalityPreset.Energetic:
              return GetEnergeticPreset();
          
          case PlantPersonalityPreset.Sturdy:
              return GetSturdyPreset();
          
          case PlantPersonalityPreset.Delicate:
              return GetDelicatePreset();
          
          case PlantPersonalityPreset.Wild:
              return GetWildPreset();
          
          default:
              Debug.LogWarning($"未知的植物个性预设: {preset}，使用温和预设");
              return GetGentlePreset();
      }
  }
  
  /// <summary>
  /// 获取所有可用的预设
  /// </summary>
  public static PlantPersonalityPreset[] GetAllPresets()
  {
      return (PlantPersonalityPreset[])System.Enum.GetValues(typeof(PlantPersonalityPreset));
  }
  
  /// <summary>
  /// 获取预设的显示名称
  /// </summary>
  public static string GetPresetDisplayName(PlantPersonalityPreset preset)
  {
      switch (preset)
      {
          case PlantPersonalityPreset.Gentle: return "温和";
          case PlantPersonalityPreset.Energetic: return "活跃";
          case PlantPersonalityPreset.Sturdy: return "坚固";
          case PlantPersonalityPreset.Delicate: return "精致";
          case PlantPersonalityPreset.Wild: return "狂野";
          default: return preset.ToString();
      }
  }
  
  /// <summary>
  /// 清除预设缓存
  /// </summary>
  public static void ClearCache()
  {
      presetCache.Clear();
  }
  
  #region 预设定义
  
  private static PlantPersonalitySettings GetGentlePreset()
  {
      return new PlantPersonalitySettings
      {
          // 时间和相位 - 温和缓慢
          timeOffset = 0f,
          randomSeed = 10f,
          phaseVariation = 0.8f,
          
          // 风力响应 - 轻柔
          windStrength = 0.015f,
          windFrequency = 1.5f,
          
          // 弹性特征 - 柔和
          elasticAmount = 0.04f,
          elasticFrequency = 3f,
          
          // 结构特征 - 稳定
          centerDeadZone = 0.25f,
          edgeInfluence = 0.4f,
          
          // 交互响应 - 温和
          responseIntensity = 0.7f,
          lerpSpeed = 6f,
          decaySpeed = 3f,
          smoothingFactor = 0.2f,
          
          // 波动特征 - 平缓
          responseCurveStrength = 1.2f,
          waveFrequency = 4f,
          waveDecay = 2.5f
      };
  }
  
  private static PlantPersonalitySettings GetEnergeticPreset()
  {
      return new PlantPersonalitySettings
      {
          // 时间和相位 - 活跃多变
          timeOffset = 2f,
          randomSeed = 25f,
          phaseVariation = 1.8f,
          
          // 风力响应 - 敏感
          windStrength = 0.035f,
          windFrequency = 3f,
          
          // 弹性特征 - 活跃
          elasticAmount = 0.08f,
          elasticFrequency = 5f,
          
          // 结构特征 - 灵活
          centerDeadZone = 0.15f,
          edgeInfluence = 0.6f,
          
          // 交互响应 - 积极
          responseIntensity = 1f,
          lerpSpeed = 12f,
          decaySpeed = 5f,
          smoothingFactor = 0.1f,
          
          // 波动特征 - 活跃
          responseCurveStrength = 2f,
          waveFrequency = 8f,
          waveDecay = 4f
      };
  }
  
  private static PlantPersonalitySettings GetSturdyPreset()
  {
      return new PlantPersonalitySettings
      {
          // 时间和相位 - 稳重
          timeOffset = 0.5f,
          randomSeed = 5f,
          phaseVariation = 0.5f,
          
          // 风力响应 - 抗风
          windStrength = 0.008f,
          windFrequency = 1f,
          
          // 弹性特征 - 坚固
          elasticAmount = 0.02f,
          elasticFrequency = 2.5f,
          
          // 结构特征 - 稳固
          centerDeadZone = 0.4f,
          edgeInfluence = 0.3f,
          
          // 交互响应 - 迟缓但持久
          responseIntensity = 0.5f,
          lerpSpeed = 4f,
          decaySpeed = 2f,
          smoothingFactor = 0.25f,
          
          // 波动特征 - 稳重
          responseCurveStrength = 1f,
          waveFrequency = 3f,
          waveDecay = 1.5f
      };
  }
  
  private static PlantPersonalitySettings GetDelicatePreset()
  {
      return new PlantPersonalitySettings
      {
          // 时间和相位 - 精致细腻
          timeOffset = 1f,
          randomSeed = 15f,
          phaseVariation = 1.2f,
          
          // 风力响应 - 敏感
          windStrength = 0.025f,
          windFrequency = 2.5f,
          
          // 弹性特征 - 精致
          elasticAmount = 0.06f,
          elasticFrequency = 4.5f,
          
          // 结构特征 - 细腻
          centerDeadZone = 0.2f,
          edgeInfluence = 0.7f,
          
          // 交互响应 - 敏感
          responseIntensity = 0.9f,
          lerpSpeed = 10f,
          decaySpeed = 6f,
          smoothingFactor = 0.12f,
          
          // 波动特征 - 精致
          responseCurveStrength = 1.8f,
          waveFrequency = 7f,
          waveDecay = 3.5f
      };
  }
  
  private static PlantPersonalitySettings GetWildPreset()
  {
      return new PlantPersonalitySettings
      {
          // 时间和相位 - 狂野随机
          timeOffset = 3f,
          randomSeed = 50f,
          phaseVariation = 2.5f,
          
          // 风力响应 - 极度敏感
          windStrength = 0.05f,
          windFrequency = 4f,
          
          // 弹性特征 - 狂野
          elasticAmount = 0.12f,
          elasticFrequency = 6f,
          
          // 结构特征 - 不规则
          centerDeadZone = 0.1f,
          edgeInfluence = 0.8f,
          
          // 交互响应 - 极端
          responseIntensity = 1f,
          lerpSpeed = 15f,
          decaySpeed = 7f,
          smoothingFactor = 0.08f,
          
          // 波动特征 - 狂野
          responseCurveStrength = 2.5f,
          waveFrequency = 10f,
          waveDecay = 5f
      };
  }
  
  #endregion
}