#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

namespace TilemapTools
{
    public class AutoTerrainTileEditor : EditorWindow
    {
        public Tilemap sourceTilemap;    // 源Tilemap
        public Tilemap outputTilemap;    // 输出Tilemap
        // 本地化相关变量
        private Dictionary<string, Dictionary<string, string>> localizedTexts;
        private TilemapLanguageManager.Language selectedLanguage;
        private bool showLanguageSettings = false;


        [System.Serializable]
        private class IterationStep
        {
            public string name = "迭代步骤";
            public AutoTerrainTileRuleConfiger terrainSystem;
            public bool enabled = true;
            public int inputSourceIndex = -1; // -1表示使用原始sourceTilemap，0~n表示使用第n个步骤的结果
        }

        [SerializeField] private List<IterationStep> iterationSteps = new List<IterationStep>();
        private const string IterationStepInputSourceKeyPrefix = "AutoTerrainTileEditor_StepInputSource_";
        private Vector2 scrollPosition;
        private bool clearOutputBeforeApply = true;
        private bool showAddStepUI = false;
        private AutoTerrainTileRuleConfiger newStepTerrainSystem;

        // 持久化数据的键
        private const string SourceTilemapGuidKey = "AutoTerrainTileEditor_SourceTilemapGuid";
        private const string OutputTilemapGuidKey = "AutoTerrainTileEditor_OutputTilemapGuid";
        private const string ClearBeforeApplyKey = "AutoTerrainTileEditor_ClearBeforeApply";
        private const string IterationStepsCountKey = "AutoTerrainTileEditor_IterationStepsCount";
        private const string IterationStepNameKeyPrefix = "AutoTerrainTileEditor_StepName_";
        private const string IterationStepSystemGuidKeyPrefix = "AutoTerrainTileEditor_StepSystemGuid_";
        private const string IterationStepEnabledKeyPrefix = "AutoTerrainTileEditor_StepEnabled_";

        public static AutoTerrainTileEditor ShowWindow()
        {
            var window = GetWindow<AutoTerrainTileEditor>();
            window.titleContent = new GUIContent(window.GetLocalizedText("windowTitle"));
            return window;
        }

        // 初始化本地化文本
        private void InitializeLocalization()
        {
            localizedTexts = new Dictionary<string, Dictionary<string, string>>();
            selectedLanguage = TilemapLanguageManager.GetCurrentLanguageSetting();

            // 英语文本
            var enTexts = new Dictionary<string, string>
            {
                {"windowTitle", "Auto Terrain Tile Editor"},
                {"sourceTilemap", "Source Tilemap"},
                {"outputTilemap", "Output Tilemap"},
                {"iterationSteps", "Iteration Steps"},
                {"addStep", "Add Step"},
                {"applyAll", "Apply All Iterations"},
                {"clearBeforeApply", "Clear Output Before Apply"},
                {"selectTerrainSystem", "Select Terrain System"},
                {"stepName", "Step Name"},
                {"stepEnabled", "Enabled"},
                {"inputSource", "Input Source"},
                {"originalSource", "Original Source"},
                {"previousStep", "Previous Step {0}"},
                {"delete", "Delete"},
                {"moveUp", "↑"},
                {"moveDown", "↓"},
                {"apply", "Apply"},
                {"languageSettings", "Language"},
                {"language", "Language:"},
                {"applyLanguage", "Apply"},
                {"restartRequired", "Changes will take full effect after restarting the editor"}
            };
            localizedTexts["en"] = enTexts;

            // 中文文本
            var zhTexts = new Dictionary<string, string>
            {
                {"windowTitle", "自动地形瓦片编辑器"},
                {"sourceTilemap", "源瓦片地图"},
                {"outputTilemap", "输出瓦片地图"},
                {"iterationSteps", "迭代步骤"},
                {"addStep", "添加步骤"},
                {"applyAll", "应用所有迭代"},
                {"clearBeforeApply", "应用前清除输出"},
                {"selectTerrainSystem", "选择地形系统"},
                {"stepName", "步骤名称"},
                {"stepEnabled", "启用"},
                {"inputSource", "输入源"},
                {"originalSource", "原始源"},
                {"previousStep", "前一步骤 {0}"},
                {"delete", "删除"},
                {"moveUp", "↑"},
                {"moveDown", "↓"},
                {"apply", "应用"},
                {"languageSettings", "语言设置"},
                {"language", "语言:"},
                {"applyLanguage", "应用"},
                {"restartRequired", "更改将在重新启动编辑器后完全生效"}
            };
            localizedTexts["zh-CN"] = zhTexts;
        }

        private string GetLocalizedText(string key, params object[] args)
        {
            string languageCode = TilemapLanguageManager.GetCurrentLanguageCode();

            // 检查当前语言是否有该文本
            if (localizedTexts.ContainsKey(languageCode) && localizedTexts[languageCode].ContainsKey(key))
            {
                string text = localizedTexts[languageCode][key];
                if (args != null && args.Length > 0)
                {
                    return string.Format(text, args);
                }
                return text;
            }

            // 如果没有找到，使用英语
            if (localizedTexts.ContainsKey("en") && localizedTexts["en"].ContainsKey(key))
            {
                string text = localizedTexts["en"][key];
                if (args != null && args.Length > 0)
                {
                    return string.Format(text, args);
                }
                return text;
            }

            // 如果英语也没有，返回键名
            return key;
        }

        private void OnEnable()
        {
            // 初始化本地化
            InitializeLocalization();
            // 加载保存的数据
            LoadEditorData();
        }

        private void OnDisable()
        {
            // 保存当前数据
            SaveEditorData();
        }

        private void SaveEditorData()
        {
            // 保存Tilemap引用
            if (sourceTilemap != null)
            {
                // 获取GameObject的实例ID作为唯一标识符
                string sourceId = sourceTilemap.gameObject.GetInstanceID().ToString();
                EditorPrefs.SetString(SourceTilemapGuidKey, sourceId);
            }
            else
            {
                EditorPrefs.DeleteKey(SourceTilemapGuidKey);
            }

            if (outputTilemap != null)
            {
                // 获取GameObject的实例ID作为唯一标识符
                string outputId = outputTilemap.gameObject.GetInstanceID().ToString();
                EditorPrefs.SetString(OutputTilemapGuidKey, outputId);
            }
            else
            {
                EditorPrefs.DeleteKey(OutputTilemapGuidKey);
            }

            // 保存清除选项
            EditorPrefs.SetBool(ClearBeforeApplyKey, clearOutputBeforeApply);

            // 保存迭代步骤
            EditorPrefs.SetInt(IterationStepsCountKey, iterationSteps.Count);
            for (int i = 0; i < iterationSteps.Count; i++)
            {
                EditorPrefs.SetString(IterationStepNameKeyPrefix + i, iterationSteps[i].name);
                EditorPrefs.SetBool(IterationStepEnabledKeyPrefix + i, iterationSteps[i].enabled);
                EditorPrefs.SetInt(IterationStepInputSourceKeyPrefix + i, iterationSteps[i].inputSourceIndex);

                if (iterationSteps[i].terrainSystem != null)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(iterationSteps[i].terrainSystem));
                    EditorPrefs.SetString(IterationStepSystemGuidKeyPrefix + i, guid);
                }
                else
                {
                    EditorPrefs.DeleteKey(IterationStepSystemGuidKeyPrefix + i);
                }
            }
        }

        private void LoadEditorData()
        {
            // 加载Tilemap引用
            if (EditorPrefs.HasKey(SourceTilemapGuidKey))
            {
                string sourceId = EditorPrefs.GetString(SourceTilemapGuidKey);
                // 尝试通过场景中的所有Tilemap找到匹配的
                Tilemap[] allTilemaps = FindObjectsOfType<Tilemap>();
                foreach (var tilemap in allTilemaps)
                {
                    if (tilemap.gameObject.GetInstanceID().ToString() == sourceId)
                    {
                        sourceTilemap = tilemap;
                        break;
                    }
                }
            }

            if (EditorPrefs.HasKey(OutputTilemapGuidKey))
            {
                string outputId = EditorPrefs.GetString(OutputTilemapGuidKey);
                // 尝试通过场景中的所有Tilemap找到匹配的
                Tilemap[] allTilemaps = FindObjectsOfType<Tilemap>();
                foreach (var tilemap in allTilemaps)
                {
                    if (tilemap.gameObject.GetInstanceID().ToString() == outputId)
                    {
                        outputTilemap = tilemap;
                        break;
                    }
                }
            }

            // 加载清除选项
            if (EditorPrefs.HasKey(ClearBeforeApplyKey))
            {
                clearOutputBeforeApply = EditorPrefs.GetBool(ClearBeforeApplyKey);
            }

            // 加载迭代步骤
            iterationSteps.Clear();
            int stepsCount = EditorPrefs.GetInt(IterationStepsCountKey, 0);
            for (int i = 0; i < stepsCount; i++)
            {
                IterationStep step = new IterationStep();

                if (EditorPrefs.HasKey(IterationStepNameKeyPrefix + i))
                {
                    step.name = EditorPrefs.GetString(IterationStepNameKeyPrefix + i);
                }

                if (EditorPrefs.HasKey(IterationStepEnabledKeyPrefix + i))
                {
                    step.enabled = EditorPrefs.GetBool(IterationStepEnabledKeyPrefix + i);
                }

                if (EditorPrefs.HasKey(IterationStepInputSourceKeyPrefix + i))
                {
                    step.inputSourceIndex = EditorPrefs.GetInt(IterationStepInputSourceKeyPrefix + i);
                }

                if (EditorPrefs.HasKey(IterationStepSystemGuidKeyPrefix + i))
                {
                    string guid = EditorPrefs.GetString(IterationStepSystemGuidKeyPrefix + i);
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        step.terrainSystem = AssetDatabase.LoadAssetAtPath<AutoTerrainTileRuleConfiger>(path);
                    }
                }

                iterationSteps.Add(step);
            }
        }

        private void OnGUI()
        {
            // 标题和语言设置按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(GetLocalizedText("windowTitle"), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // 语言设置面板
            if (showLanguageSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(GetLocalizedText("language"), GUILayout.Width(80));
                TilemapLanguageManager.Language newLanguage = (TilemapLanguageManager.Language)EditorGUILayout.EnumPopup(
                    selectedLanguage
                );

                if (newLanguage != selectedLanguage)
                {
                    selectedLanguage = newLanguage;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                if (GUILayout.Button(GetLocalizedText("applyLanguage")))
                {
                    TilemapLanguageManager.SetCurrentLanguageSetting(selectedLanguage);
                    // 应用后自动隐藏语言设置菜单
                    showLanguageSettings = false;
                    // 刷新界面
                    Repaint();
                }

                EditorGUILayout.HelpBox(GetLocalizedText("restartRequired"), MessageType.Info);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }

            EditorGUI.BeginChangeCheck();
            sourceTilemap = (Tilemap)EditorGUILayout.ObjectField(GetLocalizedText("sourceTilemap"), sourceTilemap, typeof(Tilemap), true);
            outputTilemap = (Tilemap)EditorGUILayout.ObjectField(GetLocalizedText("outputTilemap"), outputTilemap, typeof(Tilemap), true);

            EditorGUILayout.Space();

            clearOutputBeforeApply = EditorGUILayout.Toggle(GetLocalizedText("clearBeforeApply"), clearOutputBeforeApply);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("iterationSteps"), EditorStyles.boldLabel);

            // 显示迭代步骤列表
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < iterationSteps.Count; i++)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);

                EditorGUILayout.BeginHorizontal();
                iterationSteps[i].enabled = EditorGUILayout.Toggle(iterationSteps[i].enabled, GUILayout.Width(20));
                iterationSteps[i].name = EditorGUILayout.TextField(iterationSteps[i].name);

                if (GUILayout.Button(GetLocalizedText("moveUp"), GUILayout.Width(25)) && i > 0)
                {
                    // 上移步骤
                    var temp = iterationSteps[i];
                    iterationSteps[i] = iterationSteps[i - 1];
                    iterationSteps[i - 1] = temp;
                }

                if (GUILayout.Button(GetLocalizedText("moveDown"), GUILayout.Width(25)) && i < iterationSteps.Count - 1)
                {
                    // 下移步骤
                    var temp = iterationSteps[i];
                    iterationSteps[i] = iterationSteps[i + 1];
                    iterationSteps[i + 1] = temp;
                }

                if (GUILayout.Button(GetLocalizedText("delete"), GUILayout.Width(25)))
                {
                    // 删除步骤
                    iterationSteps.RemoveAt(i);
                    i--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }

                EditorGUILayout.EndHorizontal();

                // 显示步骤详情
                EditorGUI.indentLevel++;
                iterationSteps[i].terrainSystem = (AutoTerrainTileRuleConfiger)EditorGUILayout.ObjectField(
                    GetLocalizedText("selectTerrainSystem"), iterationSteps[i].terrainSystem, typeof(AutoTerrainTileRuleConfiger), false);

                // 添加输入源选择下拉菜单
                string[] inputOptions = new string[i + 1];
                inputOptions[0] = GetLocalizedText("originalSource");
                for (int j = 0; j < i; j++)
                {
                    inputOptions[j + 1] = GetLocalizedText("previousStep", j + 1) + ": " + iterationSteps[j].name;
                }

                int selectedIndex = iterationSteps[i].inputSourceIndex + 1; // 转换为UI索引
                selectedIndex = EditorGUILayout.Popup(GetLocalizedText("inputSource"), selectedIndex, inputOptions);
                iterationSteps[i].inputSourceIndex = selectedIndex - 1; // 转换回存储索引

                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            // 添加新步骤按钮
            EditorGUILayout.Space();

            if (showAddStepUI)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(GetLocalizedText("addStep"), EditorStyles.boldLabel);

                newStepTerrainSystem = (AutoTerrainTileRuleConfiger)EditorGUILayout.ObjectField(
                    GetLocalizedText("selectTerrainSystem"), newStepTerrainSystem, typeof(AutoTerrainTileRuleConfiger), false);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(GetLocalizedText("apply")))
                {
                    if (newStepTerrainSystem != null)
                    {
                        IterationStep newStep = new IterationStep();
                        newStep.name = $"{GetLocalizedText("stepName")} {iterationSteps.Count + 1}";
                        newStep.terrainSystem = newStepTerrainSystem;
                        iterationSteps.Add(newStep);

                        // 重置UI状态
                        showAddStepUI = false;
                        newStepTerrainSystem = null;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(GetLocalizedText("error"), GetLocalizedText("selectTerrainSystemRequired"), "OK");
                    }
                }

                if (GUILayout.Button(GetLocalizedText("cancel")))
                {
                    showAddStepUI = false;
                    newStepTerrainSystem = null;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            else
            {
                if (GUILayout.Button(GetLocalizedText("addStep")))
                {
                    showAddStepUI = true;
                }
            }

            EditorGUILayout.Space();

            // 应用按钮
            using (new EditorGUI.DisabledScope(sourceTilemap == null || outputTilemap == null || iterationSteps.Count == 0))
            {
                if (GUILayout.Button(GetLocalizedText("applyAll")))
                {
                    ApplyAllIterations();
                }
            }

            // 如果有任何UI变化，保存数据
            if (EditorGUI.EndChangeCheck())
            {
                SaveEditorData();
            }
        }

        public void ApplyAllIterations()
        {
            if (sourceTilemap == null || outputTilemap == null || iterationSteps.Count == 0)
                return;

            // 如果选择了清除输出，则先清除输出Tilemap
            if (clearOutputBeforeApply)
            {
                outputTilemap.ClearAllTiles();
            }

            // 创建临时Tilemap数组，存储每个步骤的结果
            GameObject[] tempGOs = new GameObject[iterationSteps.Count];
            Tilemap[] tempTilemaps = new Tilemap[iterationSteps.Count];

            try
            {
                // 初始化所有临时Tilemap
                for (int i = 0; i < iterationSteps.Count; i++)
                {
                    tempGOs[i] = new GameObject($"TempTilemap_{i}");
                    tempTilemaps[i] = tempGOs[i].AddComponent<Tilemap>();
                    tempGOs[i].AddComponent<TilemapRenderer>();
                }

                // 对每个启用的迭代步骤应用规则
                for (int i = 0; i < iterationSteps.Count; i++)
                {
                    if (!iterationSteps[i].enabled || iterationSteps[i].terrainSystem == null)
                        continue;

                    Debug.Log($"应用迭代步骤 {i + 1}: {iterationSteps[i].name}");

                    // 确定输入源
                    Tilemap inputTilemap;
                    if (iterationSteps[i].inputSourceIndex < 0)
                    {
                        // 使用原始源Tilemap
                        inputTilemap = sourceTilemap;
                        Debug.Log($"步骤 {i + 1} 使用原始源Tilemap作为输入");
                    }
                    else
                    {
                        // 使用之前步骤的结果
                        int sourceIndex = iterationSteps[i].inputSourceIndex;
                        if (sourceIndex >= i)
                        {
                            Debug.LogWarning($"步骤 {i + 1} 尝试使用无效的输入源索引 {sourceIndex}，将使用原始源Tilemap代替");
                            inputTilemap = sourceTilemap;
                        }
                        else if (!iterationSteps[sourceIndex].enabled)
                        {
                            Debug.LogWarning($"步骤 {i + 1} 尝试使用已禁用的步骤 {sourceIndex + 1} 作为输入源，将使用原始源Tilemap代替");
                            inputTilemap = sourceTilemap;
                        }
                        else
                        {
                            inputTilemap = tempTilemaps[sourceIndex];
                            Debug.Log($"步骤 {i + 1} 使用步骤 {sourceIndex + 1} 的结果作为输入");
                        }
                    }

                    // 应用当前步骤的规则
                    ApplyIterationStep(inputTilemap, tempTilemaps[i], iterationSteps[i].terrainSystem);
                }

                // 按顺序合并所有结果到输出Tilemap
                for (int i = 0; i < iterationSteps.Count; i++)
                {
                    if (!iterationSteps[i].enabled || iterationSteps[i].terrainSystem == null)
                        continue;

                    // 将当前步骤的结果合并到输出Tilemap
                    MergeTilemapToOutput(tempTilemaps[i], outputTilemap);
                    Debug.Log($"合并步骤 {i + 1} 的结果到最终输出");
                }

                // 所有迭代完成后，处理所有的 emptyMarkerTile
                ProcessEmptyMarkerTiles();

                Debug.Log("所有迭代步骤已完成应用。");
            }
            finally
            {
                // 清理临时对象
                for (int i = 0; i < tempGOs.Length; i++)
                {
                    if (tempGOs[i] != null)
                    {
                        DestroyImmediate(tempGOs[i]);
                    }
                }
            }
        }

        private void MergeTilemapToOutput(Tilemap source, Tilemap destination)
        {
            if (source == null || destination == null)
                return;

            source.CompressBounds();
            BoundsInt bounds = source.cellBounds;

            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                for (int x = bounds.min.x; x < bounds.max.x; x++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    TileBase tile = source.GetTile(pos);

                    // 只有当源Tilemap中有瓦片时才覆盖目标Tilemap
                    if (tile != null)
                    {
                        destination.SetTile(pos, tile);
                    }
                }
            }
        }

        // 新增方法，处理所有的 emptyMarkerTile
        private void ProcessEmptyMarkerTiles()
        {
            // 获取输出 Tilemap 的边界
            outputTilemap.CompressBounds();
            BoundsInt bounds = outputTilemap.cellBounds;

            // 收集所有需要清除的位置
            List<Vector3Int> positionsToRemove = new List<Vector3Int>();

            // 检查每个位置
            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                for (int x = bounds.min.x; x < bounds.max.x; x++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    TileBase tile = outputTilemap.GetTile(pos);

                    // 检查所有迭代步骤中的 emptyMarkerTile
                    foreach (var step in iterationSteps)
                    {
                        if (step.enabled && step.terrainSystem != null && tile == step.terrainSystem.emptyMarkerTile)
                        {
                            positionsToRemove.Add(pos);
                            break;
                        }
                    }
                }
            }

            // 清除所有 emptyMarkerTile 位置
            Debug.Log($"清除了 {positionsToRemove.Count} 个 emptyMarkerTile 位置");
            foreach (var pos in positionsToRemove)
            {
                outputTilemap.SetTile(pos, null);
            }
        }

        // 修改 ApplyIterationStep 方法，添加更详细的日志
        private void ApplyIterationStep(Tilemap sourceTilemap, Tilemap destTilemap, AutoTerrainTileRuleConfiger terrainSystem)
        {
            // 获取source tilemap的边界
            sourceTilemap.CompressBounds();
            BoundsInt bounds = sourceTilemap.cellBounds;

            Debug.Log($"应用地形系统: {terrainSystem.name}, 源Tilemap边界: min=({bounds.min.x}, {bounds.min.y}), max=({bounds.max.x}, {bounds.max.y})");

            // 创建一个副本以存储原始瓦片
            TileBase[,] originalTiles = new TileBase[bounds.size.x, bounds.size.y];

            // 读取所有原始瓦片
            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                for (int x = bounds.min.x; x < bounds.max.x; x++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    originalTiles[x - bounds.min.x, y - bounds.min.y] = sourceTilemap.GetTile(pos);
                }
            }


            // 修改规则排序逻辑，使优先级高的规则排在前面，优先级低的规则排在后面
            List<AutoTerrainTileRuleConfiger.TerrainRule> sortedRules = new List<AutoTerrainTileRuleConfiger.TerrainRule>(terrainSystem.rules);
            sortedRules.Sort((a, b) => b.priority.CompareTo(a.priority)); // 修改为从高到低排序，这样高优先级的规则会先执行

            Debug.Log($"规则总数: {sortedRules.Count}");

            // 创建一个临时字典来存储每个规则匹配的位置和对应的输出瓦片
            Dictionary<AutoTerrainTileRuleConfiger.TerrainRule, Dictionary<Vector3Int, TileBase>> ruleMatchResults =
                new Dictionary<AutoTerrainTileRuleConfiger.TerrainRule, Dictionary<Vector3Int, TileBase>>();

            // 对每个规则，检查所有位置是否匹配
            foreach (var rule in sortedRules)
            {
                // 获取输入和输出规则Tilemap
                Tilemap inputTilemap = terrainSystem.GetInputRulesTilemap();
                Tilemap outputRulesTilemap = terrainSystem.GetOutputRulesTilemap();

                if (inputTilemap == null || outputRulesTilemap == null)
                    continue;

                // 创建当前规则的匹配结果字典
                Dictionary<Vector3Int, TileBase> matchPositions = new Dictionary<Vector3Int, TileBase>();
                ruleMatchResults[rule] = matchPositions;

                // 获取规则大小
                int ruleWidth = rule.bounds.size.x;
                int ruleHeight = rule.bounds.size.y;

                // 调试信息
                Debug.Log($"检查规则 '{rule.ruleName}': 大小={ruleWidth}x{ruleHeight}, 边界=({rule.bounds.min.x}, {rule.bounds.min.y})");

                // 遍历源Tilemap中所有可能的起始位置
                // 注意：我们需要确保规则不会超出边界，所以减去规则的宽度和高度
                for (int y = bounds.min.y; y <= bounds.max.y - ruleHeight; y++)
                {
                    for (int x = bounds.min.x; x <= bounds.max.x - ruleWidth; x++)
                    {
                        // 检查规则是否匹配 - 始终基于原始瓦片进行匹配
                        if (IsRuleMatching(originalTiles, bounds, x, y, rule, terrainSystem))
                        {
                            // 规则匹配成功，现在我们需要找到输出瓦片

                            // 根据规则的大小，确定应该放置输出瓦片的位置
                            // 对于2x2、3x3等规则，我们需要找到规则输出Tilemap中对应的瓦片

                            // 对于每个规则位置，获取对应的输出瓦片并设置到对应位置
                            for (int ry = 0; ry < ruleHeight; ry++)
                            {
                                for (int rx = 0; rx < ruleWidth; rx++)
                                {
                                    // 计算规则输出Tilemap中的位置
                                    Vector3Int ruleOutputPos = new Vector3Int(rule.bounds.min.x + rx, rule.bounds.min.y + ry, 0);
                                    TileBase outputTile = outputRulesTilemap.GetTile(ruleOutputPos);

                                    // 计算目标Tilemap中的位置
                                    Vector3Int destPos = new Vector3Int(x + rx, y + ry, 0);

                                    // 记录匹配位置和对应的输出瓦片
                                    if (outputTile != null)
                                    {
                                        matchPositions[destPos] = outputTile;
                                    }
                                }
                            }
                        }
                    }
                }

                Debug.Log($"规则 '{rule.ruleName}' (优先级: {rule.priority}) 匹配了 {matchPositions.Count} 个位置");
            }

            // 按优先级顺序应用规则的结果（高优先级的规则先应用）
            int totalApplied = 0;
            foreach (var rule in sortedRules) // 现在sortedRules已经按照优先级从低到高排序，我们从前到后遍历
            {
                if (!ruleMatchResults.ContainsKey(rule))
                    continue;

                var matchPositions = ruleMatchResults[rule];
                int ruleApplied = 0;

                // 应用当前规则的所有匹配结果
                foreach (var kvp in matchPositions)
                {
                    Vector3Int pos = kvp.Key;
                    TileBase outputTile = kvp.Value;

                    if (outputTile != null)
                    {
                        // 设置输出瓦片
                        destTilemap.SetTile(pos, outputTile);
                        ruleApplied++;
                    }
                }

                Debug.Log($"应用规则 '{rule.ruleName}' (优先级: {rule.priority}) 的结果到 {ruleApplied} 个位置");
                totalApplied += ruleApplied;
            }

            // 处理未匹配的位置 - 保持原样
            int keptOriginal = 0;
            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                for (int x = bounds.min.x; x < bounds.max.x; x++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    TileBase currentTile = originalTiles[x - bounds.min.x, y - bounds.min.y];

                    // 检查这个位置是否被任何规则匹配过
                    bool matchedByAnyRule = false;
                    foreach (var rule in sortedRules)
                    {
                        if (ruleMatchResults.ContainsKey(rule) && ruleMatchResults[rule].ContainsKey(pos))
                        {
                            matchedByAnyRule = true;
                            break;
                        }
                    }

                    // 如果没有被任何规则匹配，保持原样
                    if (currentTile != null && !matchedByAnyRule)
                    {
                        // 如果当前瓦片是emptyMarkerTile，则不放置任何瓦片（真正的空白）
                        if (currentTile == terrainSystem.emptyMarkerTile)
                        {
                            // 不设置任何瓦片，保持为空
                        }
                        else
                        {
                            // 否则保持原样
                            destTilemap.SetTile(pos, currentTile);
                            keptOriginal++;
                        }
                    }
                }
            }

            Debug.Log($"保持原样的位置: {keptOriginal} 个");
            Debug.Log($"总计应用规则的位置: {totalApplied} 个");

            // 刷新输出tilemap
            destTilemap.RefreshAllTiles();
        }

        public void SetDefaultTerrainRules(AutoTerrainTileRuleConfiger rules)
        {
            if (rules == null) return;

            // 检查是否已存在相同的规则
            bool alreadyExists = iterationSteps.Any(step => step.terrainSystem == rules);
            if (alreadyExists)
            {
                EditorUtility.DisplayDialog("Info", $"Rules '{rules.name}' are already in the list. Will excute it", "OK");
                return;
            }

            // 添加新规则
            var newStep = new IterationStep
            {
                name = rules.name,
                terrainSystem = rules,
                enabled = true
            };
            iterationSteps.Add(newStep);

            EditorUtility.DisplayDialog("Rules Loaded", $"Terrain rules '{rules.name}' have been added to the list.", "OK");
            Repaint(); // 刷新窗口以显示新条目
        }

        private bool IsRuleMatching(TileBase[,] tiles, BoundsInt bounds, int startX, int startY,
                                AutoTerrainTileRuleConfiger.TerrainRule rule, AutoTerrainTileRuleConfiger terrainSystem)
        {
            int ruleWidth = rule.bounds.size.x;
            int ruleHeight = rule.bounds.size.y;

            // 获取输入规则Tilemap
            Tilemap inputTilemap = terrainSystem.GetInputRulesTilemap();
            if (inputTilemap == null)
                return false;

            // 检查规则范围内的每个位置
            for (int y = 0; y < ruleHeight; y++)
            {
                for (int x = 0; x < ruleWidth; x++)
                {
                    // 计算规则中的位置
                    Vector3Int rulePos = new Vector3Int(rule.bounds.min.x + x, rule.bounds.min.y + y, 0);
                    TileBase ruleTile = inputTilemap.GetTile(rulePos);

                    // 计算Tilemap中对应的位置
                    int tileX = startX + x;
                    int tileY = startY + y;

                    // 检查是否在Tilemap边界内
                    bool outOfBounds = tileX < bounds.min.x || tileX >= bounds.max.x ||
                                    tileY < bounds.min.y || tileY >= bounds.max.y;

                    if (outOfBounds)
                    {
                        // 超出边界，规则中的瓦片必须是null或emptyMarkerTile
                        if (ruleTile != null && ruleTile != terrainSystem.emptyMarkerTile)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // 在边界内，获取Tilemap中的瓦片
                        TileBase tileTile = tiles[tileX - bounds.min.x, tileY - bounds.min.y];

                        if (ruleTile == terrainSystem.emptyMarkerTile)
                        {
                            // 规则要求该位置必须是emptyMarkerTile
                            if (tileTile != terrainSystem.emptyMarkerTile)
                            {
                                return false;
                            }
                        }
                        else if (ruleTile == terrainSystem.anyNonEmptyTile)
                        {
                            // 新增：规则要求该位置必须是任意非空白瓦片
                            if (tileTile == null || tileTile == terrainSystem.emptyMarkerTile)
                            {
                                return false;
                            }
                        }
                        else if (ruleTile == null)
                        {
                            // 规则中的空白是通配符，跳过检查
                            continue;
                        }
                        else
                        {
                            // 规则要求特定瓦片，检查是否匹配
                            if (tileTile != ruleTile)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            // 所有位置都匹配，规则匹配成功
            return true;
        }
    }
}
#endif