using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 简化版植物摆动批量设置工具
/// 专注于核心功能，代码简洁易懂
/// </summary>
public class EnhancedPlantSwayBatchSetup : EditorWindow
{
  [MenuItem("Tools/Plant Sway/增强版批量设置")]
  public static void ShowWindow()
  {
      GetWindow<EnhancedPlantSwayBatchSetup>("植物个性批量设置", true);
  }
  
  private Vector2 scrollPosition;
  
  #region 设置选项
  
  // 批量设置选项
  private bool setTimeOffset = true;
  private bool setRandomSeed = true;
  private bool setWindParams = false;
  private bool setElasticParams = false;
  private bool setStructureParams = false;
  private bool setInteractionParams = false;
  private bool setWaveParams = false;
  
  // 随机化强度
  private float timeRandomness = 1f;
  private float windRandomness = 0.2f;
  private float elasticRandomness = 0.2f;
  private float structureRandomness = 0.1f;
  private float interactionRandomness = 0.15f;
  private float waveRandomness = 0.2f;
  
  // 预设选择
  private PlantPersonalityPreset selectedPreset = PlantPersonalityPreset.Gentle;
  private bool applyPreset = false;
  private bool randomizePresets = false;
  
  // 植物类型设置
  private bool setPlantType = false;
  private PlantType selectedPlantType = PlantType.Bush;
  
  // 组件管理
  private bool autoAddComponent = true;
  private bool autoRegisterWithManager = true;
  
  // 强制刷新选项
  private bool forceSceneSave = true;
  
  #endregion
  
  void OnGUI()
  {
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
      
      GUILayout.Label("🌿 植物个性批量设置工具", EditorStyles.boldLabel);
      EditorGUILayout.Space();
      
      // 组件管理设置
      DrawComponentSettings();
      EditorGUILayout.Space();
      
      // 植物类型设置
      DrawPlantTypeSettings();
      EditorGUILayout.Space();
      
      // 选择要设置的参数
      DrawParameterSelection();
      EditorGUILayout.Space();
      
      // 随机化强度设置
      DrawRandomnessSettings();
      EditorGUILayout.Space();
      
      // 预设设置
      DrawPresetSettings();
      EditorGUILayout.Space();
      
      // 高级选项
      DrawAdvancedSettings();
      EditorGUILayout.Space();
      
      // 操作按钮
      DrawActionButtons();
      
      EditorGUILayout.EndScrollView();
  }
  
  private void DrawComponentSettings()
  {
      EditorGUILayout.LabelField("🔧 组件管理", EditorStyles.boldLabel);
      
      autoAddComponent = EditorGUILayout.Toggle("自动添加 EnhancedPlantSwayController", autoAddComponent);
      autoRegisterWithManager = EditorGUILayout.Toggle("自动注册到管理器", autoRegisterWithManager);
      
      EditorGUILayout.HelpBox(
          "• 自动添加：为没有组件的对象添加 EnhancedPlantSwayController\n" +
          "• 自动注册：自动将植物注册到 PlantInteractionManager", 
          MessageType.Info);
  }
  
  private void DrawPlantTypeSettings()
  {
      EditorGUILayout.LabelField("🌱 植物类型设置", EditorStyles.boldLabel);
      
      setPlantType = EditorGUILayout.Toggle("设置植物类型", setPlantType);
      
      if (setPlantType)
      {
          selectedPlantType = (PlantType)EditorGUILayout.EnumPopup("植物类型", selectedPlantType);
          EditorGUILayout.HelpBox(GetPlantTypeDescription(selectedPlantType), MessageType.Info);
      }
  }
  
  private void DrawParameterSelection()
  {
      EditorGUILayout.LabelField("📋 选择要设置的参数", EditorStyles.boldLabel);
      
      setTimeOffset = EditorGUILayout.Toggle("⏰ 时间偏移 (防同步)", setTimeOffset);
      setRandomSeed = EditorGUILayout.Toggle("🎲 随机种子", setRandomSeed);
      setWindParams = EditorGUILayout.Toggle("💨 风力参数", setWindParams);
      setElasticParams = EditorGUILayout.Toggle("🌊 弹性参数", setElasticParams);
      setStructureParams = EditorGUILayout.Toggle("🏗️ 结构参数", setStructureParams);
      setInteractionParams = EditorGUILayout.Toggle("👆 交互参数", setInteractionParams);
      setWaveParams = EditorGUILayout.Toggle("〰️ 波动参数", setWaveParams);
  }
  
  private void DrawRandomnessSettings()
  {
      EditorGUILayout.LabelField("🎯 随机化强度设置", EditorStyles.boldLabel);
      
      if (setTimeOffset || setRandomSeed)
      {
          timeRandomness = EditorGUILayout.Slider("时间随机化强度", timeRandomness, 0f, 1f);
      }
      
      if (setWindParams)
      {
          windRandomness = EditorGUILayout.Slider("风力随机化强度", windRandomness, 0f, 1f);
      }
      
      if (setElasticParams)
      {
          elasticRandomness = EditorGUILayout.Slider("弹性随机化强度", elasticRandomness, 0f, 1f);
      }
      
      if (setStructureParams)
      {
          structureRandomness = EditorGUILayout.Slider("结构随机化强度", structureRandomness, 0f, 1f);
      }
      
      if (setInteractionParams)
      {
          interactionRandomness = EditorGUILayout.Slider("交互随机化强度", interactionRandomness, 0f, 1f);
      }
      
      if (setWaveParams)
      {
          waveRandomness = EditorGUILayout.Slider("波动随机化强度", waveRandomness, 0f, 1f);
      }
  }
  
  private void DrawPresetSettings()
  {
      EditorGUILayout.LabelField("🎨 个性预设", EditorStyles.boldLabel);
      
      applyPreset = EditorGUILayout.Toggle("应用个性预设", applyPreset);
      
      if (applyPreset)
      {
          randomizePresets = EditorGUILayout.Toggle("随机化预设", randomizePresets);
          
          if (!randomizePresets)
          {
              selectedPreset = (PlantPersonalityPreset)EditorGUILayout.EnumPopup("选择预设", selectedPreset);
              EditorGUILayout.HelpBox(GetPresetDescription(selectedPreset), MessageType.Info);
          }
          else
          {
              EditorGUILayout.HelpBox("将为每个植物随机选择预设", MessageType.Info);
          }
      }
  }
  
  private void DrawAdvancedSettings()
  {
      EditorGUILayout.LabelField("⚙️ 高级选项", EditorStyles.boldLabel);
      
      forceSceneSave = EditorGUILayout.Toggle("强制保存场景", forceSceneSave);
      
      EditorGUILayout.HelpBox(
          "• 强制保存场景：确保修改立即生效并保存到场景中", 
          MessageType.Info);
  }
  
  private void DrawActionButtons()
  {
      EditorGUILayout.LabelField("🚀 执行操作", EditorStyles.boldLabel);
      
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("🎯 应用到选中植物"))
      {
          ApplyToSelectedPlants();
      }
      
      if (GUILayout.Button("🌍 应用到所有植物"))
      {
          ApplyToAllPlants();
      }
      EditorGUILayout.EndHorizontal();
      
      EditorGUILayout.Space();
      
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("🔍 查找所有植物"))
      {
          FindAndSelectAllPlants();
      }
      
      if (GUILayout.Button("📊 生成植物统计"))
      {
          GeneratePlantReport();
      }
      EditorGUILayout.EndHorizontal();
      
      EditorGUILayout.Space();
      
      if (GUILayout.Button("🔗 检查管理器"))
      {
          CheckManagerConnection();
      }
      
      EditorGUILayout.Space();
      
      EditorGUILayout.HelpBox(
          "💡 使用提示:\n" +
          "1. 选择要修改的参数类型\n" +
          "2. 调整随机化强度\n" +
          "3. 可选择应用个性预设\n" +
          "4. 选择植物对象后点击应用按钮", 
          MessageType.Info);
  }
  
  #region 核心功能实现
  
  /// <summary>
  /// 应用设置到选中的植物
  /// </summary>
  private void ApplyToSelectedPlants()
  {
      GameObject[] selectedObjects = Selection.gameObjects;
      if (selectedObjects.Length == 0)
      {
          EditorUtility.DisplayDialog("提示", "请先选择要设置的植物对象", "确定");
          return;
      }
      
      // 开始记录撤销操作
      Undo.RecordObjects(selectedObjects, "Apply Plant Settings");
      
      int processedCount = 0;
      
      foreach (GameObject obj in selectedObjects)
      {
          if (ProcessPlantObject(obj))
          {
              processedCount++;
          }
      }
      
      // 强制刷新场景视图
      SceneView.RepaintAll();
      
      EditorUtility.DisplayDialog("完成", 
          $"已处理 {processedCount} 个植物对象", "确定");
      
      RefreshPlantManager();
  }
  
  /// <summary>
  /// 应用设置到所有植物
  /// </summary>
  private void ApplyToAllPlants()
  {
      if (!EditorUtility.DisplayDialog("确认", 
          "这将修改场景中所有的植物对象，是否继续？", "是", "否"))
      {
          return;
      }
      
      EnhancedPlantSwayController[] allControllers = FindObjectsOfType<EnhancedPlantSwayController>();
      
      // 记录撤销操作
      GameObject[] allObjects = new GameObject[allControllers.Length];
      for (int i = 0; i < allControllers.Length; i++)
      {
          allObjects[i] = allControllers[i].gameObject;
      }
      Undo.RecordObjects(allObjects, "Apply Plant Settings To All");
      
      int processedCount = 0;
      
      foreach (var controller in allControllers)
      {
          if (ProcessPlantObject(controller.gameObject))
          {
              processedCount++;
          }
      }
      
      // 强制刷新场景视图
      SceneView.RepaintAll();
      
      EditorUtility.DisplayDialog("完成", 
          $"已处理 {processedCount} 个植物对象", "确定");
      
      RefreshPlantManager();
  }
  
  /// <summary>
  /// 处理单个植物对象
  /// </summary>
  private bool ProcessPlantObject(GameObject obj)
  {
      if (obj == null) return false;
      
      // 获取或添加控制器
      EnhancedPlantSwayController controller = obj.GetComponent<EnhancedPlantSwayController>();
      
      if (controller == null && autoAddComponent)
      {
          // 记录添加组件的撤销操作
          controller = Undo.AddComponent<EnhancedPlantSwayController>(obj);
          Debug.Log($"已添加 EnhancedPlantSwayController: {obj.name}");
      }
      
      if (controller == null) return false;
      
      // 记录修改控制器的撤销操作
      Undo.RecordObject(controller, "Modify Plant Controller");
      
      // 应用设置
      ApplySettingsToController(controller);
      
      // 标记为脏数据
      EditorUtility.SetDirty(controller);
      
      // 强制序列化对象
      PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
      
      return true;
  }
  
  /// <summary>
  /// 应用设置到控制器
  /// </summary>
  private void ApplySettingsToController(EnhancedPlantSwayController controller)
  {
      // 获取当前设置
      PlantPersonalitySettings settings = controller.GetPersonalitySettings();
      if (settings == null)
      {
          settings = new PlantPersonalitySettings();
      }
      
      // 应用预设
      if (applyPreset)
      {
          PlantPersonalityPreset presetToApply = randomizePresets ? 
              GetRandomPreset() : selectedPreset;
          
          var presetSettings = PlantPersonalityPresets.GetPresetSettings(presetToApply);
          if (presetSettings != null)
          {
              settings = presetSettings;
          }
      }
      
      // 应用随机化
      ApplyRandomization(settings, controller.transform.position);
      
      // 设置植物类型
      if (setPlantType)
      {
          controller.SetPlantType(selectedPlantType);
      }
      
      // 更新控制器设置
      controller.UpdatePersonalitySettings(settings);
      
      // 强制控制器重新初始化（如果有此方法）
      if (controller.GetType().GetMethod("ForceInitialize") != null)
      {
          controller.SendMessage("ForceInitialize", SendMessageOptions.DontRequireReceiver);
      }
      
      Debug.Log($"已应用设置到: {controller.name}");
  }
  
  /// <summary>
  /// 应用随机化
  /// </summary>
  private void ApplyRandomization(PlantPersonalitySettings settings, Vector3 position)
  {
      // 基于位置生成一致的随机种子
      Random.InitState(GetPositionBasedSeed(position));
      
      if (setTimeOffset)
      {
          settings.timeOffset = Random.Range(0f, 10f * timeRandomness);
      }
      
      if (setRandomSeed)
      {
          settings.randomSeed = Random.Range(0f, 100f * timeRandomness);
      }
      
      if (setWindParams)
      {
          settings.windStrength *= (1f + Random.Range(-windRandomness, windRandomness));
          settings.windFrequency *= (1f + Random.Range(-windRandomness, windRandomness));
      }
      
      if (setElasticParams)
      {
          settings.elasticAmount *= (1f + Random.Range(-elasticRandomness, elasticRandomness));
          settings.elasticFrequency *= (1f + Random.Range(-elasticRandomness, elasticRandomness));
      }
      
      if (setStructureParams)
      {
          settings.centerDeadZone *= (1f + Random.Range(-structureRandomness, structureRandomness));
          settings.edgeInfluence *= (1f + Random.Range(-structureRandomness, structureRandomness));
      }
      
      if (setInteractionParams)
      {
          settings.responseIntensity *= (1f + Random.Range(-interactionRandomness, interactionRandomness));
          settings.lerpSpeed *= (1f + Random.Range(-interactionRandomness, interactionRandomness));
      }
      
      if (setWaveParams)
      {
          settings.waveFrequency *= (1f + Random.Range(-waveRandomness, waveRandomness));
          settings.waveDecay *= (1f + Random.Range(-waveRandomness, waveRandomness));
      }
  }
  
  #endregion
  
  #region 辅助功能
  
  /// <summary>
  /// 查找并选中所有植物
  /// </summary>
  private void FindAndSelectAllPlants()
  {
      EnhancedPlantSwayController[] controllers = FindObjectsOfType<EnhancedPlantSwayController>();
      
      if (controllers.Length > 0)
      {
          GameObject[] plantObjects = new GameObject[controllers.Length];
          for (int i = 0; i < controllers.Length; i++)
          {
              plantObjects[i] = controllers[i].gameObject;
          }
          
          Selection.objects = plantObjects;
          EditorUtility.DisplayDialog("查找完成", 
              $"找到 {controllers.Length} 个植物对象，已自动选中", "确定");
      }
      else
      {
          EditorUtility.DisplayDialog("查找完成", "未找到植物对象", "确定");
      }
  }
  
  /// <summary>
  /// 生成植物统计报告
  /// </summary>
  private void GeneratePlantReport()
  {
      EnhancedPlantSwayController[] controllers = FindObjectsOfType<EnhancedPlantSwayController>();
      
      string report = "🌿 植物统计报告\n\n";
      report += $"总植物数量: {controllers.Length}\n\n";
      
      // 统计植物类型
      Dictionary<PlantType, int> typeCounts = new Dictionary<PlantType, int>();
      foreach (var controller in controllers)
      {
          PlantType type = controller.GetPlantType();
          if (typeCounts.ContainsKey(type))
              typeCounts[type]++;
          else
              typeCounts[type] = 1;
      }
      
      report += "植物类型分布:\n";
      foreach (var kvp in typeCounts)
      {
          report += $"• {kvp.Key}: {kvp.Value}\n";
      }
      
      Debug.Log(report);
      EditorUtility.DisplayDialog("植物统计报告", report, "确定");
  }
  
  /// <summary>
  /// 检查管理器连接
  /// </summary>
  private void CheckManagerConnection()
  {
      PlantInteractionManager manager = FindObjectOfType<PlantInteractionManager>();
      
      if (manager == null)
      {
          bool createManager = EditorUtility.DisplayDialog("管理器未找到", 
              "场景中没有 PlantInteractionManager，是否创建一个？", "创建", "取消");
          
          if (createManager)
          {
              GameObject managerObj = new GameObject("PlantInteractionManager");
              Undo.RegisterCreatedObjectUndo(managerObj, "Create Plant Manager");
              manager = Undo.AddComponent<PlantInteractionManager>(managerObj);
              Selection.activeGameObject = managerObj;
              
              EditorUtility.SetDirty(managerObj);
              
              EditorUtility.DisplayDialog("创建完成", "已创建 PlantInteractionManager", "确定");
          }
      }
      else
      {
          int registeredCount = manager.GetRegisteredPlantCount();
          EditorUtility.DisplayDialog("管理器状态", 
              $"管理器正常运行\n已注册植物数量: {registeredCount}", "确定");
      }
  }
  
  /// <summary>
  /// 刷新植物管理器
  /// </summary>
  private void RefreshPlantManager()
  {
      if (autoRegisterWithManager)
      {
          PlantInteractionManager manager = FindObjectOfType<PlantInteractionManager>();
          if (manager != null)
          {
              Undo.RecordObject(manager, "Refresh Plant Manager");
              manager.RegisterAllPlantsInScene();
              EditorUtility.SetDirty(manager);
              Debug.Log("已刷新植物管理器注册");
          }
      }
  }
  
  /// <summary>
  /// 获取随机预设
  /// </summary>
  private PlantPersonalityPreset GetRandomPreset()
  {
      var allPresets = PlantPersonalityPresets.GetAllPresets();
      return allPresets[Random.Range(0, allPresets.Length)];
  }
  
  /// <summary>
  /// 基于位置生成随机种子
  /// </summary>
  private int GetPositionBasedSeed(Vector3 position)
  {
      return (int)(Mathf.Abs(position.x * 73.856093f + position.y * 128.311f + position.z * 57.2957795f) % 10000);
  }
  
  /// <summary>
  /// 获取预设描述
  /// </summary>
  private string GetPresetDescription(PlantPersonalityPreset preset)
  {
      switch (preset)
      {
          case PlantPersonalityPreset.Gentle:
              return "温和: 轻柔摆动，适合小花小草";
          case PlantPersonalityPreset.Energetic:
              return "活跃: 明显摆动，适合年轻植物";
          case PlantPersonalityPreset.Sturdy:
              return "坚韧: 稳定少动，适合大树粗茎";
          case PlantPersonalityPreset.Delicate:
              return "精致: 敏感易动，适合细叶嫩枝";
          case PlantPersonalityPreset.Wild:
              return "狂野: 剧烈摆动，适合野生植物";
          default:
              return "";
      }
  }
  
  /// <summary>
  /// 获取植物类型描述
  /// </summary>
  private string GetPlantTypeDescription(PlantType plantType)
  {
      switch (plantType)
      {
          case PlantType.Grass:
              return "草类: 柔软细长，容易摆动";
          case PlantType.Bush:
              return "灌木: 中等硬度，适中摆动";
          case PlantType.Tree:
              return "树木: 坚硬稳定，轻微摆动";
          case PlantType.Flower:
              return "花朵: 精致敏感，优雅摆动";
          case PlantType.Fern:
              return "蕨类: 叶片丰富，层次摆动";
          default:
              return "";
      }
  }
  
  #endregion
}

#endif