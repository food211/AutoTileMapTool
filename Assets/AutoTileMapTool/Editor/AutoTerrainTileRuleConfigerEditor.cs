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

        private void OnEnable()
        {
            terrainSystem = (AutoTerrainTileRuleConfiger)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Auto Terrain Tile Rule Configer", EditorStyles.boldLabel);

            // 空白标记瓦片（可选）
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Empty Marker Tile (Optional)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            TileBase emptyMarkerTile = (TileBase)EditorGUILayout.ObjectField("Empty Marker Tile", terrainSystem.emptyMarkerTile, typeof(TileBase), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(terrainSystem, "Change Empty Marker Tile");
                terrainSystem.emptyMarkerTile = emptyMarkerTile;
                EditorUtility.SetDirty(terrainSystem);
            }
            
            // 添加空白瓦片透明度滑动条
            EditorGUI.BeginChangeCheck();
            float newAlpha = EditorGUILayout.Slider(new GUIContent("Empty Tile Alpha", "控制空白瓦片的透明度"), terrainSystem.emptyTileAlpha, 0f, 1f);
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

            // 规则Tilemap Prefabs
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rule Tilemap Prefabs", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            GameObject inputRulesPrefab = (GameObject)EditorGUILayout.ObjectField("Input Rules Prefab", terrainSystem.inputRulesPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                // 验证所选的prefab是否包含Tilemap组件
                if (inputRulesPrefab != null)
                {
                    var tilemap = inputRulesPrefab.GetComponentInChildren<Tilemap>();
                    if (tilemap == null)
                    {
                        EditorUtility.DisplayDialog("错误", "所选Prefab不包含Tilemap组件", "确定");
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
            GameObject outputRulesPrefab = (GameObject)EditorGUILayout.ObjectField("Output Rules Prefab", terrainSystem.outputRulesPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                // 验证所选的prefab是否包含Tilemap组件
                if (outputRulesPrefab != null)
                {
                    var tilemap = outputRulesPrefab.GetComponentInChildren<Tilemap>();
                    if (tilemap == null)
                    {
                        EditorUtility.DisplayDialog("错误", "所选Prefab不包含Tilemap组件", "确定");
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
            EditorGUILayout.LabelField("Terrain Rules", EditorStyles.boldLabel);

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
                    string newRuleName = EditorGUILayout.TextField("Rule Name", terrainSystem.rules[i].ruleName);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(terrainSystem, "Change Rule Name");
                        terrainSystem.rules[i].ruleName = newRuleName;
                        EditorUtility.SetDirty(terrainSystem);
                    }

                    EditorGUI.BeginChangeCheck();
                    int newPriority = EditorGUILayout.IntField("Priority", terrainSystem.rules[i].priority);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(terrainSystem, "Change Rule Priority");
                        terrainSystem.rules[i].priority = newPriority;
                        EditorUtility.SetDirty(terrainSystem);
                    }

                    // 显示规则边界
                    EditorGUILayout.LabelField($"Rule Bounds: x={terrainSystem.rules[i].bounds.xMin}, y={terrainSystem.rules[i].bounds.yMin}, " +
                                            $"width={terrainSystem.rules[i].bounds.size.x}, height={terrainSystem.rules[i].bounds.size.y}");

                    EditorGUI.indentLevel--;
                }
            }

            // 从Tilemap中提取规则按钮
            EditorGUI.BeginDisabledGroup(terrainSystem.inputRulesPrefab == null || terrainSystem.outputRulesPrefab == null);
            if (GUILayout.Button("Extract Rules from Prefabs"))
            {
                ExtractRulesFromPrefabs();
            }
            EditorGUI.EndDisabledGroup();

            // 添加新规则按钮
            if (GUILayout.Button("Add Empty Rule"))
            {
                AddNewRule();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AddNewRule()
        {
            Undo.RecordObject(terrainSystem, "Add Terrain Rule");
            AutoTerrainTileRuleConfiger.TerrainRule newRule = new AutoTerrainTileRuleConfiger.TerrainRule();
            newRule.ruleName = $"Rule {terrainSystem.rules.Count}";
            newRule.bounds = new BoundsInt(0, 0, 0, 3, 3, 1);
            terrainSystem.rules.Add(newRule);
            EditorUtility.SetDirty(terrainSystem);
            selectedRuleIndex = terrainSystem.rules.Count - 1;
        }

        private void ExtractRulesFromPrefabs()
        {
            Tilemap inputTilemap = terrainSystem.GetInputRulesTilemap();
            Tilemap outputTilemap = terrainSystem.GetOutputRulesTilemap();

            if (inputTilemap == null || outputTilemap == null)
            {
                EditorUtility.DisplayDialog("错误", "无法从Prefab中获取Tilemap组件", "确定");
                return;
            }

            // 压缩边界以获取实际使用的区域
            inputTilemap.CompressBounds();
            BoundsInt inputBounds = inputTilemap.cellBounds;

            // 记录撤销
            Undo.RecordObject(terrainSystem, "Extract Terrain Rules");

            // 清除现有规则
            terrainSystem.rules.Clear();

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
                        BoundsInt ruleBounds = FindRuleBounds(x, y, inputBounds, inputTilemap);

                        // 打印找到的规则边界
                        Debug.Log($"找到规则区块: min=({ruleBounds.min.x}, {ruleBounds.min.y}), 大小={ruleBounds.size.x}x{ruleBounds.size.y}");

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
                terrainSystem.rules.Add(extractedRules[i]);
                Debug.Log($"添加规则 '{extractedRules[i].ruleName}': 边界=({extractedRules[i].bounds.min.x}, {extractedRules[i].bounds.min.y}), " +
                        $"大小={extractedRules[i].bounds.size.x}x{extractedRules[i].bounds.size.y}, 优先级={extractedRules[i].priority}");
            }

            // 标记对象为已修改，确保保存
            EditorUtility.SetDirty(terrainSystem);

            Debug.Log($"提取了 {terrainSystem.rules.Count} 条规则，上方规则优先级更高");
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
    }
}
#endif
