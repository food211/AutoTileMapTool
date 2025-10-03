#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace TilemapTools
{
    [CustomEditor(typeof(AutoTerrainTileRuleConfiger))]
    public class AutoTerrainTileRuleConfigerEditor : Editor
    {
        private AutoTerrainTileRuleConfiger terrainSystem;
        private int selectedRuleIndex = -1;

        private Dictionary<string, Dictionary<string, string>> localizedTexts;

        private void OnEnable()
        {
            // 添加空检查，确保target不为空
            if (target != null)
            {
                terrainSystem = (AutoTerrainTileRuleConfiger)target;
            }

            // 初始化本地化
            InitializeLocalization();
        }
        
        private void InitializeLocalization()
        {
            localizedTexts = new Dictionary<string, Dictionary<string, string>>();

            // 英语文本
            var enTexts = new Dictionary<string, string>
            {
                {"title", "Auto Terrain Tile Rule Configer"},
                {"emptyMarkerTitle", "Empty Marker Tile (Optional)"},
                {"emptyMarkerTile", "Empty Marker Tile"},
                {"emptyTileAlpha", "Empty Tile Alpha"},
                {"emptyTileAlphaTooltip", "Controls the transparency of empty tiles"},
                {"anyNonEmptyTile", "Any Non-Empty Tile"},
                {"anyNonEmptyTileTooltip", "Used to match any non-empty tile"},
                {"anyNonEmptyTileHelp", "Use 'Any Non-Empty Tile' to match any non-empty tile, useful for creating generic rules."},
                {"ruleTilemapTitle", "Rule Tilemap Prefabs"},
                {"inputRulesPrefab", "Input Rules Prefab"},
                {"outputRulesPrefab", "Output Rules Prefab"},
                {"terrainRulesTitle", "Terrain Rules"},
                {"ruleName", "Rule Name"},
                {"priority", "Priority"},
                {"ruleBounds", "Rule Bounds: x={0}, y={1}, width={2}, height={3}"},
                {"extractRules", "Extract Rules from Prefabs"},
                {"addEmptyRule", "Add Empty Rule"},
                {"error", "Error"},
                {"noTilemapError", "Selected prefab does not contain a Tilemap component"},
                {"extractError", "Cannot extract rules: Target object is null."},
                {"tilemapError", "Cannot get Tilemap component from prefab"},
                {"inputBounds", "Input rules Tilemap bounds: min=({0}, {1}), max=({2}, {3})"},
                {"foundRuleBlock", "Found rule block: min=({0}, {1}), size={2}x{3}"},
                {"addedRule", "Added rule '{0}': bounds=({1}, {2}), size={3}x{4}, priority={5}"},
                {"extractedRules", "Extracted {0} rules, higher rules have higher priority"}
            };
            localizedTexts["en"] = enTexts;

            // 中文文本
            var zhTexts = new Dictionary<string, string>
            {
                {"title", "自动地形瓦片规则配置器"},
                {"emptyMarkerTitle", "空白标记瓦片（可选）"},
                {"emptyMarkerTile", "空白标记瓦片"},
                {"emptyTileAlpha", "空白瓦片透明度"},
                {"emptyTileAlphaTooltip", "控制空白瓦片的透明度"},
                {"anyNonEmptyTile", "任意非空白瓦片"},
                {"anyNonEmptyTileTooltip", "用于匹配任何非空白的瓦片"},
                {"anyNonEmptyTileHelp", "使用「任意非空白瓦片」可以匹配任何非空的瓦片，用于创建通用规则。"},
                {"ruleTilemapTitle", "规则瓦片地图预制体"},
                {"inputRulesPrefab", "输入规则预制体"},
                {"outputRulesPrefab", "输出规则预制体"},
                {"terrainRulesTitle", "地形规则"},
                {"ruleName", "规则名称"},
                {"priority", "优先级"},
                {"ruleBounds", "规则边界: x={0}, y={1}, 宽度={2}, 高度={3}"},
                {"extractRules", "从预制体提取规则"},
                {"addEmptyRule", "添加空规则"},
                {"error", "错误"},
                {"noTilemapError", "所选预制体不包含瓦片地图组件"},
                {"extractError", "无法提取规则：目标对象为空。"},
                {"tilemapError", "无法从预制体中获取瓦片地图组件"},
                {"inputBounds", "输入规则瓦片地图边界: 最小=({0}, {1}), 最大=({2}, {3})"},
                {"foundRuleBlock", "找到规则区块: 最小=({0}, {1}), 大小={2}x{3}"},
                {"addedRule", "添加规则 '{0}': 边界=({1}, {2}), 大小={3}x{4}, 优先级={5}"},
                {"extractedRules", "提取了 {0} 条规则，上方规则优先级更高"}
            };
            localizedTexts["zh-CN"] = zhTexts;
        }


        // 获取本地化文本
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
        public override void OnInspectorGUI()
        {
            // 确保terrainSystem不为空
            if (terrainSystem == null && target != null)
            {
                terrainSystem = (AutoTerrainTileRuleConfiger)target;
            }

            if (terrainSystem == null)
            {
                EditorGUILayout.HelpBox(GetLocalizedText("extractError"), MessageType.Error);
                return;
            }

            serializedObject.Update();

            EditorGUILayout.LabelField(GetLocalizedText("title"), EditorStyles.boldLabel);

            // 空白标记瓦片（可选）
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("emptyMarkerTitle"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            TileBase emptyMarkerTile = (TileBase)EditorGUILayout.ObjectField(GetLocalizedText("emptyMarkerTile"), terrainSystem.emptyMarkerTile, typeof(TileBase), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(terrainSystem, "Change Empty Marker Tile");
                terrainSystem.emptyMarkerTile = emptyMarkerTile;
                EditorUtility.SetDirty(terrainSystem);
            }

            // 添加空白瓦片透明度滑动条
            EditorGUI.BeginChangeCheck();
            float newAlpha = EditorGUILayout.Slider(new GUIContent(GetLocalizedText("emptyTileAlpha"), GetLocalizedText("emptyTileAlphaTooltip")), terrainSystem.emptyTileAlpha, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(terrainSystem, "Change Empty Tile Alpha");
                terrainSystem.SetEmptyTileAlpha(newAlpha);
                EditorUtility.SetDirty(terrainSystem);

                // 如果emptyMarkerTile是Tile类型，确保它也被标记为已修改
                if (terrainSystem.emptyMarkerTile is Tile)
                {
                    EditorUtility.SetDirty(terrainSystem.emptyMarkerTile);
                }
            }

            // 任意非空白瓦片
            EditorGUI.BeginChangeCheck();
            TileBase anyNonEmptyTile = (TileBase)EditorGUILayout.ObjectField(
                new GUIContent(GetLocalizedText("anyNonEmptyTile"), GetLocalizedText("anyNonEmptyTileTooltip")),
                terrainSystem.anyNonEmptyTile, typeof(TileBase), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(terrainSystem, "Change Any Non-Empty Tile");
                terrainSystem.anyNonEmptyTile = anyNonEmptyTile;
                EditorUtility.SetDirty(terrainSystem);
            }

            EditorGUILayout.HelpBox(GetLocalizedText("anyNonEmptyTileHelp"), MessageType.Info);

            // 规则Tilemap Prefabs
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("ruleTilemapTitle"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            GameObject inputRulesPrefab = (GameObject)EditorGUILayout.ObjectField(GetLocalizedText("inputRulesPrefab"), terrainSystem.inputRulesPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                // 验证所选的prefab是否包含Tilemap组件
                if (inputRulesPrefab != null)
                {
                    var tilemap = inputRulesPrefab.GetComponentInChildren<Tilemap>();
                    if (tilemap == null)
                    {
                        EditorUtility.DisplayDialog(GetLocalizedText("error"), GetLocalizedText("noTilemapError"), "OK");
                    }
                    else
                    {
                        Undo.RecordObject(terrainSystem, "Change Input Rules Prefab");
                        terrainSystem.inputRulesPrefab = inputRulesPrefab;
                        terrainSystem.ClearCache();
                        EditorUtility.SetDirty(terrainSystem);
                    }
                }
                else
                {
                    Undo.RecordObject(terrainSystem, "Clear Input Rules Prefab");
                    terrainSystem.inputRulesPrefab = null;
                    terrainSystem.ClearCache();
                    EditorUtility.SetDirty(terrainSystem);
                }
            }

            EditorGUI.BeginChangeCheck();
            GameObject outputRulesPrefab = (GameObject)EditorGUILayout.ObjectField(GetLocalizedText("outputRulesPrefab"), terrainSystem.outputRulesPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                // 验证所选的prefab是否包含Tilemap组件
                if (outputRulesPrefab != null)
                {
                    var tilemap = outputRulesPrefab.GetComponentInChildren<Tilemap>();
                    if (tilemap == null)
                    {
                        EditorUtility.DisplayDialog(GetLocalizedText("error"), GetLocalizedText("noTilemapError"), "OK");
                    }
                    else
                    {
                        Undo.RecordObject(terrainSystem, "Change Output Rules Prefab");
                        terrainSystem.outputRulesPrefab = outputRulesPrefab;
                        terrainSystem.ClearCache();
                        EditorUtility.SetDirty(terrainSystem);
                    }
                }
                else
                {
                    Undo.RecordObject(terrainSystem, "Clear Output Rules Prefab");
                    terrainSystem.outputRulesPrefab = null;
                    terrainSystem.ClearCache();
                    EditorUtility.SetDirty(terrainSystem);
                }
            }

            // 规则列表
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("terrainRulesTitle"), EditorStyles.boldLabel);

            for (int i = 0; i < terrainSystem.rules.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                string ruleName = string.IsNullOrEmpty(terrainSystem.rules[i].ruleName) ? $"Rule {i}" : terrainSystem.rules[i].ruleName;
                if (GUILayout.Button(ruleName, GUILayout.Width(150)))
                {
                    selectedRuleIndex = (selectedRuleIndex == i) ? -1 : i;
                }

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    Undo.RecordObject(terrainSystem, "Remove Terrain Rule");
                    terrainSystem.rules.RemoveAt(i);
                    EditorUtility.SetDirty(terrainSystem);
                    i--;
                    continue;
                }

                EditorGUILayout.EndHorizontal();

                // 显示选中规则的详细信息
                if (selectedRuleIndex == i)
                {
                    EditorGUI.indentLevel++;

                    EditorGUI.BeginChangeCheck();
                    string newRuleName = EditorGUILayout.TextField(GetLocalizedText("ruleName"), terrainSystem.rules[i].ruleName);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(terrainSystem, "Change Rule Name");
                        terrainSystem.rules[i].ruleName = newRuleName;
                        EditorUtility.SetDirty(terrainSystem);
                    }

                    EditorGUI.BeginChangeCheck();
                    int newPriority = EditorGUILayout.IntField(GetLocalizedText("priority"), terrainSystem.rules[i].priority);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(terrainSystem, "Change Rule Priority");
                        terrainSystem.rules[i].priority = newPriority;
                        EditorUtility.SetDirty(terrainSystem);
                    }

                    // 显示规则边界
                    EditorGUILayout.LabelField(GetLocalizedText("ruleBounds",
                        terrainSystem.rules[i].bounds.xMin,
                        terrainSystem.rules[i].bounds.yMin,
                        terrainSystem.rules[i].bounds.size.x,
                        terrainSystem.rules[i].bounds.size.y));

                    EditorGUI.indentLevel--;
                }
            }

            // 从Tilemap中提取规则按钮
            EditorGUI.BeginDisabledGroup(terrainSystem.inputRulesPrefab == null || terrainSystem.outputRulesPrefab == null);
            if (GUILayout.Button(GetLocalizedText("extractRules")))
            {
                ExtractRulesFromPrefabs();
            }
            EditorGUI.EndDisabledGroup();

            // 添加新规则按钮
            if (GUILayout.Button(GetLocalizedText("addEmptyRule")))
            {
                AddNewRule();
            }

            serializedObject.ApplyModifiedProperties();
        }

        public void AddNewRule()
        {
            Undo.RecordObject(terrainSystem, "Add Terrain Rule");
            AutoTerrainTileRuleConfiger.TerrainRule newRule = new AutoTerrainTileRuleConfiger.TerrainRule();
            newRule.ruleName = $"Rule {terrainSystem.rules.Count}";
            newRule.bounds = new BoundsInt(0, 0, 0, 3, 3, 1);
            terrainSystem.rules.Add(newRule);
            EditorUtility.SetDirty(terrainSystem);
            selectedRuleIndex = terrainSystem.rules.Count - 1;
        }

        public void ExtractRulesFromPrefabs()
        {
            // 确保terrainSystem不为空
            if (terrainSystem == null)
            {
                Debug.LogError(GetLocalizedText("extractError"));
                return;
            }

            Tilemap inputTilemap = terrainSystem.GetInputRulesTilemap();
            Tilemap outputTilemap = terrainSystem.GetOutputRulesTilemap();

            if (inputTilemap == null || outputTilemap == null)
            {
                EditorUtility.DisplayDialog(GetLocalizedText("error"), GetLocalizedText("tilemapError"), "OK");
                return;
            }

            // 压缩边界以获取实际使用的区域
            inputTilemap.CompressBounds();
            BoundsInt inputBounds = inputTilemap.cellBounds;

            // 记录撤销
            Undo.RecordObject(terrainSystem, "Extract Terrain Rules");

            // 清除现有规则
            terrainSystem.ClearRules();

            // 扫描规则Tilemap，寻找规则区块
            bool[,] visited = new bool[inputBounds.size.x, inputBounds.size.y];

            // 临时存储提取的规则
            List<AutoTerrainTileRuleConfiger.TerrainRule> extractedRules = new List<AutoTerrainTileRuleConfiger.TerrainRule>();

            // 打印边界信息
            Debug.Log(GetLocalizedText("inputBounds", inputBounds.min.x, inputBounds.min.y, inputBounds.max.x, inputBounds.max.y));

            for (int y = inputBounds.min.y; y < inputBounds.max.y; y++)
            {
                for (int x = inputBounds.min.x; x < inputBounds.max.x; x++)
                {
                    int localX = x - inputBounds.min.x;
                    int localY = y - inputBounds.min.y;

                    if (localX < 0 || localX >= visited.GetLength(0) ||
                        localY < 0 || localY >= visited.GetLength(1) ||
                        visited[localX, localY])
                        continue;

                    Vector3Int pos = new Vector3Int(x, y, 0);
                    TileBase tile = inputTilemap.GetTile(pos);

                    if (tile != null)
                    {
                        // 找到规则的起始点，执行洪水填充算法找到整个规则区块
                        BoundsInt ruleBounds = FindRuleBounds(x, y, inputBounds, inputTilemap);

                        // 打印找到的规则边界
                        Debug.Log(GetLocalizedText("foundRuleBlock", ruleBounds.min.x, ruleBounds.min.y, ruleBounds.size.x, ruleBounds.size.y));

                        // 标记该区域为已访问
                        for (int ry = ruleBounds.min.y; ry < ruleBounds.max.y; ry++)
                        {
                            for (int rx = ruleBounds.min.x; rx < ruleBounds.max.x; rx++)
                            {
                                int lx = rx - inputBounds.min.x;
                                int ly = ry - inputBounds.min.y;

                                if (lx >= 0 && lx < visited.GetLength(0) && ly >= 0 && ly < visited.GetLength(1))
                                {
                                    visited[lx, ly] = true;
                                }
                            }
                        }

                        // 创建新规则
                        AutoTerrainTileRuleConfiger.TerrainRule newRule = new AutoTerrainTileRuleConfiger.TerrainRule();
                        newRule.ruleName = $"Rule {extractedRules.Count}";
                        newRule.bounds = ruleBounds;

                        // 设置优先级：使用规则的y坐标作为优先级，这样上方的规则优先级更高
                        newRule.priority = ruleBounds.min.y;

                        extractedRules.Add(newRule);
                    }
                }
            }

            // 对提取的规则按优先级排序（y坐标越高，优先级越高）
            extractedRules.Sort((a, b) => b.priority.CompareTo(a.priority));

            // 重新设置优先级，确保上方的规则有更高的数值优先级
            for (int i = 0; i < extractedRules.Count; i++)
            {
                extractedRules[i].priority = extractedRules.Count - i;
                // 使用AddRule方法而不是直接操作规则列表
                terrainSystem.AddRule(extractedRules[i]);
            }

            Debug.Log(GetLocalizedText("extractedRules", terrainSystem.rules.Count));
        }

        // 为自动化工作流添加的静态方法，不依赖于编辑器的target属性
        public static void ExtractRulesFromPrefabsStatic(AutoTerrainTileRuleConfiger ruleConfiger)
        {
            if (ruleConfiger == null)
            {
                Debug.LogError("无法提取规则：规则配置器为空。");
                return;
            }

            Tilemap inputTilemap = ruleConfiger.GetInputRulesTilemap();
            Tilemap outputTilemap = ruleConfiger.GetOutputRulesTilemap();

            if (inputTilemap == null || outputTilemap == null)
            {
                Debug.LogError("无法从Prefab中获取Tilemap组件");
                return;
            }

            // 压缩边界以获取实际使用的区域
            inputTilemap.CompressBounds();
            BoundsInt inputBounds = inputTilemap.cellBounds;

            // 使用ClearRules方法清除现有规则，而不是直接操作规则列表
            ruleConfiger.ClearRules();

            // 扫描规则Tilemap，寻找规则区块
            bool[,] visited = new bool[inputBounds.size.x, inputBounds.size.y];

            // 临时存储提取的规则
            List<AutoTerrainTileRuleConfiger.TerrainRule> extractedRules = new List<AutoTerrainTileRuleConfiger.TerrainRule>();

            // 打印边界信息
            Debug.Log($"输入规则Tilemap边界: min=({inputBounds.min.x}, {inputBounds.min.y}), max=({inputBounds.max.x}, {inputBounds.max.y})");

            for (int y = inputBounds.min.y; y < inputBounds.max.y; y++)
            {
                for (int x = inputBounds.min.x; x < inputBounds.max.x; x++)
                {
                    int localX = x - inputBounds.min.x;
                    int localY = y - inputBounds.min.y;

                    if (localX < 0 || localX >= visited.GetLength(0) ||
                        localY < 0 || localY >= visited.GetLength(1) ||
                        visited[localX, localY])
                        continue;

                    Vector3Int pos = new Vector3Int(x, y, 0);
                    TileBase tile = inputTilemap.GetTile(pos);

                    if (tile != null)
                    {
                        // 找到规则的起始点，执行洪水填充算法找到整个规则区块
                        BoundsInt ruleBounds = FindRuleBoundsStatic(x, y, inputBounds, inputTilemap);

                        // 标记该区域为已访问
                        for (int ry = ruleBounds.min.y; ry < ruleBounds.max.y; ry++)
                        {
                            for (int rx = ruleBounds.min.x; rx < ruleBounds.max.x; rx++)
                            {
                                int lx = rx - inputBounds.min.x;
                                int ly = ry - inputBounds.min.y;

                                if (lx >= 0 && lx < visited.GetLength(0) && ly >= 0 && ly < visited.GetLength(1))
                                {
                                    visited[lx, ly] = true;
                                }
                            }
                        }

                        // 创建新规则
                        AutoTerrainTileRuleConfiger.TerrainRule newRule = new AutoTerrainTileRuleConfiger.TerrainRule();
                        newRule.ruleName = $"Rule {extractedRules.Count}";
                        newRule.bounds = ruleBounds;

                        // 设置优先级：使用规则的y坐标作为优先级，这样上方的规则优先级更高
                        newRule.priority = ruleBounds.min.y;

                        extractedRules.Add(newRule);
                    }
                }
            }

            // 对提取的规则按优先级排序（y坐标越高，优先级越高）
            extractedRules.Sort((a, b) => b.priority.CompareTo(a.priority));

            // 重新设置优先级，确保上方的规则有更高的数值优先级
            for (int i = 0; i < extractedRules.Count; i++)
            {
                extractedRules[i].priority = extractedRules.Count - i;
                // 使用AddRule方法而不是直接操作规则列表
                ruleConfiger.AddRule(extractedRules[i]);
            }

            Debug.Log($"提取了 {ruleConfiger.rules.Count} 条规则，上方规则优先级更高");
        }

        private BoundsInt FindRuleBounds(int startX, int startY, BoundsInt tileBounds, Tilemap tilemap)
        {
            // 使用洪水填充算法找到规则的边界
            int minX = startX, maxX = startX;
            int minY = startY, maxY = startY;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            queue.Enqueue(new Vector2Int(startX, startY));
            visited.Add(new Vector2Int(startX, startY));

            // 定义8个方向的偏移
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                // 更新边界
                minX = Mathf.Min(minX, current.x);
                maxX = Mathf.Max(maxX, current.x);
                minY = Mathf.Min(minY, current.y);
                maxY = Mathf.Max(maxY, current.y);

                // 检查8个方向
                for (int i = 0; i < 8; i++)
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];
                    Vector2Int next = new Vector2Int(nx, ny);

                    // 检查是否在边界内且未访问过
                    if (nx >= tileBounds.min.x && nx < tileBounds.max.x &&
                        ny >= tileBounds.min.y && ny < tileBounds.max.y &&
                        !visited.Contains(next))
                    {
                        Vector3Int pos = new Vector3Int(nx, ny, 0);
                        TileBase tile = tilemap.GetTile(pos);

                        if (tile != null)
                        {
                            queue.Enqueue(next);
                            visited.Add(next);
                        }
                    }
                }
            }

            // 返回规则边界
            return new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        }

        // 静态版本的FindRuleBounds方法
        private static BoundsInt FindRuleBoundsStatic(int startX, int startY, BoundsInt tileBounds, Tilemap tilemap)
        {
            // 使用洪水填充算法找到规则的边界
            int minX = startX, maxX = startX;
            int minY = startY, maxY = startY;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            queue.Enqueue(new Vector2Int(startX, startY));
            visited.Add(new Vector2Int(startX, startY));

            // 定义8个方向的偏移
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                // 更新边界
                minX = Mathf.Min(minX, current.x);
                maxX = Mathf.Max(maxX, current.x);
                minY = Mathf.Min(minY, current.y);
                maxY = Mathf.Max(maxY, current.y);

                // 检查8个方向
                for (int i = 0; i < 8; i++)
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];
                    Vector2Int next = new Vector2Int(nx, ny);

                    // 检查是否在边界内且未访问过
                    if (nx >= tileBounds.min.x && nx < tileBounds.max.x &&
                        ny >= tileBounds.min.y && ny < tileBounds.max.y &&
                        !visited.Contains(next))
                    {
                        Vector3Int pos = new Vector3Int(nx, ny, 0);
                        TileBase tile = tilemap.GetTile(pos);

                        if (tile != null)
                        {
                            queue.Enqueue(next);
                            visited.Add(next);
                        }
                    }
                }
            }

            // 返回规则边界
            return new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        }
    }
}
#endif