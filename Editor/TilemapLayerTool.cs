#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TilemapTools
{
    public class TilemapLayerTool : EditorWindow
    {
        private Tilemap sourceTilemap;
        private Tilemap targetTilemap;
        private Vector2Int selectionStart;
        private Vector2Int selectionEnd;
        private bool isSelecting = false;
        private bool clearTargetBeforeMove = true; // 是否在移动前清空目标区域
        private SelectionMode currentSelectionMode = SelectionMode.New;
        private bool isShiftPressed = false;
        private bool isAltPressed = false;
        private HashSet<Vector2Int> selectedCells = new HashSet<Vector2Int>();
        private bool useModifierKeysForMode = true; // 新增：是否使用修饰键覆盖模式
        private const string PREF_SOURCE_TILEMAP_PATH = "TilemapLayerTool_SourceTilemap";
        private const string PREF_TARGET_TILEMAP_PATH = "TilemapLayerTool_TargetTilemap";

        private TileBase selectedTile;
        private TileBase replacementTile;
        private bool showTileReplaceOptions = true;
        private Vector2 tileInfoScrollPosition;
        private Material highlightMaterial;
        private Dictionary<TileBase, Sprite> tileToSpriteMap = new Dictionary<TileBase, Sprite>();
        // 添加录制相关字段
        private bool isRecording = false;
        private List<TilemapOperation> recordedOperations = new List<TilemapOperation>();
        private string newRecordingName = "New Recording";
        private List<SavedRecording> savedRecordings = new List<SavedRecording>();
        private int selectedRecordingIndex = -1;
        private bool isPlayingRecording = false;
        private int currentOperationIndex = 0;
        private double lastOperationTime = 0;

        // 语言相关
        private string languageCode;
        private Vector2 scrollPositionRecordings;
        private Dictionary<string, Dictionary<string, string>> localizedTexts;

        // 添加选择模式枚举
        private enum SelectionMode
        {
            New,        // 新建选区
            Add,        // 增选
            Subtract,   // 反选
            Intersect   // 交集
        }

        // 保存的选区
        [System.Serializable]

        private class SavedSelection
        {
            public string name;
            public List<SerializableVector2Int> cells = new List<SerializableVector2Int>();

            // 添加无参构造函数，用于反序列化
            public SavedSelection()
            {
                name = "";
                cells = new List<SerializableVector2Int>();
            }

            public SavedSelection(string name, HashSet<Vector2Int> selectedCells)
            {
                this.name = name;

                // 将HashSet<Vector2Int>转换为可序列化的List
                foreach (Vector2Int cell in selectedCells)
                {
                    cells.Add(new SerializableVector2Int(cell));
                }
            }

            // 获取此选区包含的单元格
            public HashSet<Vector2Int> GetCells()
            {
                HashSet<Vector2Int> result = new HashSet<Vector2Int>();
                foreach (SerializableVector2Int cell in cells)
                {
                    result.Add(new Vector2Int(cell.x, cell.y));
                }
                return result;
            }

            // 获取此选区的边界（用于显示和可视化）
            public void GetBounds(out Vector2Int min, out Vector2Int max)
            {
                if (cells.Count == 0)
                {
                    min = Vector2Int.zero;
                    max = Vector2Int.zero;
                    return;
                }

                int minX = int.MaxValue;
                int minY = int.MaxValue;
                int maxX = int.MinValue;
                int maxY = int.MinValue;

                foreach (SerializableVector2Int cell in cells)
                {
                    minX = Mathf.Min(minX, cell.x);
                    maxX = Mathf.Max(maxX, cell.x);
                    minY = Mathf.Min(minY, cell.y);
                    maxY = Mathf.Max(maxY, cell.y);
                }

                min = new Vector2Int(minX, minY);
                max = new Vector2Int(maxX, maxY);
            }
        }

        // 添加可序列化的Vector2Int结构
        [System.Serializable]
        public class SerializableVector2Int
        {
            public int x;
            public int y;

            public SerializableVector2Int(Vector2Int vector)
            {
                this.x = vector.x;
                this.y = vector.y;
            }
        }

        private List<SavedSelection> savedSelections = new List<SavedSelection>();
        private string newSelectionName = "New Selection";
        private int selectedSavedSelectionIndex = -1;

        // 保存选区的文件路径
        private string SaveFilePath => Path.Combine(Application.dataPath, "Editor", "TilemapSelections.json");


        // 保存录制的文件路径
        private string RecordingsFilePath => Path.Combine(Application.dataPath, "Editor", "TilemapRecordings.json");

        // 操作类型枚举
        private enum OperationType
        {
            SelectTile,
            MoveTile,
            ReplaceTile,
            SelectArea
        }

        // 可序列化的操作基类
        [System.Serializable]
        private abstract class TilemapOperation
        {
            public OperationType operationType;
            public float timeStamp; // 相对于录制开始的时间戳

            public TilemapOperation(OperationType type)
            {
                operationType = type;
                timeStamp = Time.realtimeSinceStartup;
            }

            public abstract void Execute(TilemapLayerTool tool);
        }

        // 选择瓦片操作
        [System.Serializable]
        private class SelectTileOperation : TilemapOperation
        {
            public List<SerializableVector2Int> selectedCells = new List<SerializableVector2Int>();

            public SelectTileOperation() : base(OperationType.SelectTile) { }

            public SelectTileOperation(HashSet<Vector2Int> cells) : base(OperationType.SelectTile)
            {
                foreach (Vector2Int cell in cells)
                {
                    selectedCells.Add(new SerializableVector2Int(cell));
                }
            }

            public override void Execute(TilemapLayerTool tool)
            {
                tool.selectedCells.Clear();
                foreach (SerializableVector2Int cell in selectedCells)
                {
                    tool.selectedCells.Add(new Vector2Int(cell.x, cell.y));
                }
                SceneView.RepaintAll();
            }
        }

        // 移动瓦片操作
        [System.Serializable]
        private class MoveTileOperation : TilemapOperation
        {
            public List<SerializableVector2Int> movedCells = new List<SerializableVector2Int>();
            public bool clearBeforeMove;

            public MoveTileOperation() : base(OperationType.MoveTile) { }

            public MoveTileOperation(HashSet<Vector2Int> cells, bool clear) : base(OperationType.MoveTile)
            {
                foreach (Vector2Int cell in cells)
                {
                    movedCells.Add(new SerializableVector2Int(cell));
                }
                clearBeforeMove = clear;
            }

            public override void Execute(TilemapLayerTool tool)
            {
                if (tool.sourceTilemap == null || tool.targetTilemap == null)
                    return;

                // 转换回HashSet<Vector2Int>
                HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
                foreach (SerializableVector2Int cell in movedCells)
                {
                    cells.Add(new Vector2Int(cell.x, cell.y));
                }

                // 设置选区
                tool.selectedCells = cells;

                // 执行移动
                tool.clearTargetBeforeMove = clearBeforeMove;
                tool.MoveTiles();
            }
        }

        // 替换瓦片操作
        [System.Serializable]
        private class ReplaceTileOperation : TilemapOperation
        {
            public List<SerializableVector2Int> replacedCells = new List<SerializableVector2Int>();
            public string originalTileName;
            public string replacementTileName;
            public bool replaceAll;

            public ReplaceTileOperation() : base(OperationType.ReplaceTile) { }

            public ReplaceTileOperation(HashSet<Vector2Int> cells, TileBase original, TileBase replacement, bool all)
                : base(OperationType.ReplaceTile)
            {
                foreach (Vector2Int cell in cells)
                {
                    replacedCells.Add(new SerializableVector2Int(cell));
                }
                originalTileName = original != null ? original.name : "";
                replacementTileName = replacement != null ? replacement.name : "";
                replaceAll = all;
            }

            public override void Execute(TilemapLayerTool tool)
            {
                if (tool.sourceTilemap == null)
                    return;

                // 查找原始瓦片和替换瓦片
                TileBase originalTile = FindTileByName(originalTileName);
                TileBase replacementTile = FindTileByName(replacementTileName);

                if (originalTile == null || replacementTile == null)
                {
                    Debug.LogError($"Cannot find tiles for replacement: {originalTileName} -> {replacementTileName}");
                    return;
                }

                // 设置瓦片
                tool.selectedTile = originalTile;
                tool.replacementTile = replacementTile;

                // 转换回HashSet<Vector2Int>
                HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
                foreach (SerializableVector2Int cell in replacedCells)
                {
                    cells.Add(new Vector2Int(cell.x, cell.y));
                }

                // 设置选区
                tool.selectedCells = cells;

                // 执行替换
                if (replaceAll)
                {
                    tool.ReplaceAllSimilarTiles();
                }
                else
                {
                    tool.ReplaceTiles();
                }
            }

            // 辅助方法：通过名称查找瓦片
            private TileBase FindTileByName(string tileName)
            {
                if (string.IsNullOrEmpty(tileName))
                    return null;

                // 查找项目中所有瓦片资源
                string[] guids = AssetDatabase.FindAssets("t:TileBase");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile != null && tile.name == tileName)
                    {
                        return tile;
                    }
                }

                return null;
            }
        }

        // 选择区域操作
        [System.Serializable]
        private class SelectAreaOperation : TilemapOperation
        {
            public SerializableVector2Int start;
            public SerializableVector2Int end;
            public SelectionMode selectionMode;

            public SelectAreaOperation() : base(OperationType.SelectArea) { }

            public SelectAreaOperation(Vector2Int start, Vector2Int end, SelectionMode mode) : base(OperationType.SelectArea)
            {
                this.start = new SerializableVector2Int(start);
                this.end = new SerializableVector2Int(end);
                this.selectionMode = mode;
            }

            public override void Execute(TilemapLayerTool tool)
            {
                if (tool.sourceTilemap == null)
                    return;

                // 设置选择模式
                tool.currentSelectionMode = selectionMode;

                // 设置选择起点和终点
                tool.selectionStart = new Vector2Int(start.x, start.y);
                tool.selectionEnd = new Vector2Int(end.x, end.y);

                // 应用选择
                tool.ApplyCurrentSelection();

                // 重置选择框
                tool.selectionStart = Vector2Int.zero;
                tool.selectionEnd = Vector2Int.zero;

                SceneView.RepaintAll();
            }
        }

        // 保存的录制
        [System.Serializable]
        private class SavedRecording
        {
            public string name;
            public string sourceMapPath;
            public string targetMapPath;
            public List<TilemapOperation> operations = new List<TilemapOperation>();

            // 无参构造函数，用于反序列化
            public SavedRecording() { }

            public SavedRecording(string name, string sourcePath, string targetPath, List<TilemapOperation> ops)
            {
                this.name = name;
                this.sourceMapPath = sourcePath;
                this.targetMapPath = targetPath;
                this.operations = ops;
            }
        }

        // 用于序列化的包装类
        [System.Serializable]
        private class SavedRecordingList
        {
            public List<SavedRecording> recordings;
        }

        private string GetLocalizedText(string key, params object[] args)
        {
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

        public static void ShowWindow()
        {
            var window = GetWindow<TilemapLayerTool>();
            window.titleContent = new GUIContent(window.GetLocalizedText("windowTitle"));
        }

        private void InitializeLocalization()
        {
            localizedTexts = new Dictionary<string, Dictionary<string, string>>();

            // 英语文本
            var enTexts = new Dictionary<string, string>
            {
                {"windowTitle", "Tilemap Layer Editor"},
                {"tilemapLayerTool", "Tilemap Layer Tool"},
                {"tilemapSelection", "Tilemap Selection"},
                {"sourceTilemap", "Source Tilemap"},
                {"targetTilemap", "Target Tilemap"},
                {"clearTargetBeforeMove", "Clear Target Before Move"},
                {"selectionTools", "Selection Tools"},
                {"useModifierKeys", "Use Modifier Keys (Shift/Alt)"},
                {"new", "New"},
                {"add", "Add"},
                {"subtract", "Subtract"},
                {"intersect", "Intersect"},
                {"startSelectionMode", "Start Selection Mode"},
                {"exitSelectionMode", "Exit Selection Mode"},
                {"clearSelection", "Clear Selection"},
                {"selectSimilarTiles", "Select Similar Tiles"},
                {"selectionHelp", "Click and drag in Scene View to select tiles\nShift+Drag: Add to selection\nAlt+Drag: Subtract from selection"},
                {"currentSelection", "Current selection: {0} cells"},
                {"finishSelection", "Finish Selection"},
                {"moveSelectedTiles", "Move Selected Tiles"},
                {"saveLoadSelections", "Save/Load Selections"},
                {"saveSelection", "Save Selection"},
                {"savedSelections", "Saved Selections"},
                {"noSavedSelections", "No saved selections"},
                {"loadSelectedArea", "Load Selected Area"},
                {"tileReplacement", "Tile Replacement"},
                {"tileReplacementOptions", "Tile Replacement Options"},
                {"selectedTileInfo", "Selected Tile Info:"},
                {"noTileSelected", "No tile selected. Use the selection tools above to select tiles first."},
                {"tileName", "Tile Name:"},
                {"spriteName", "Sprite Name:"},
                {"sprite", "Sprite: None"},
                {"replaceWith", "Replace With:"},
                {"replacementTile", "Replacement Tile"},
                {"replaceSelectedTiles", "Replace Selected Tiles"},
                {"replaceAllSimilarTiles", "Replace All Similar Tiles"},
                {"recordingTitle", "Action Recorder"},
                {"recordingName", "Recording Name"},
                {"startRecording", "Start Recording"},
                {"stopRecording", "Stop Recording"},
                {"recordingInProgress", "Recording in progress... {0} operations recorded"},
                {"savedRecordings", "Saved Recordings"},
                {"noSavedRecordings", "No saved recordings"},
                {"operations", "operations"},
                {"playRecording", "Play Recording"},
                {"stopPlaying", "Stop Playing"},
                {"operationsCompleted", "operations completed"},
                {"newRecording", "New Recording"},
                {"Recording started with source tilemap: {0}", "Recording started with source tilemap: {0}"},
                {"Target tilemap: {0}", "Target tilemap: {0}"}

            };
            localizedTexts["en"] = enTexts;

            // 中文文本
            var zhTexts = new Dictionary<string, string>
            {
                {"windowTitle", "瓦片地图层编辑器"},
                {"tilemapLayerTool", "瓦片地图层工具"},
                {"tilemapSelection", "瓦片地图选择"},
                {"sourceTilemap", "源瓦片地图"},
                {"targetTilemap", "目标瓦片地图"},
                {"clearTargetBeforeMove", "移动前清空目标区域"},
                {"selectionTools", "选择工具"},
                {"useModifierKeys", "使用修饰键 (Shift/Alt)"},
                {"new", "新建"},
                {"add", "增选"},
                {"subtract", "减选"},
                {"intersect", "交集"},
                {"startSelectionMode", "开始选择模式"},
                {"exitSelectionMode", "退出选择模式"},
                {"clearSelection", "清除选择"},
                {"selectSimilarTiles", "选择相似瓦片"},
                {"selectionHelp", "在场景视图中点击并拖动以选择瓦片\nShift+拖动: 添加到选区\nAlt+拖动: 从选区中减去"},
                {"currentSelection", "当前选择: {0} 个单元格"},
                {"finishSelection", "完成选择"},
                {"moveSelectedTiles", "移动选中的瓦片"},
                {"saveLoadSelections", "保存/加载选区"},
                {"saveSelection", "保存选区"},
                {"savedSelections", "已保存的选区"},
                {"noSavedSelections", "没有已保存的选区"},
                {"loadSelectedArea", "加载选中区域"},
                {"tileReplacement", "瓦片替换"},
                {"tileReplacementOptions", "瓦片替换选项"},
                {"selectedTileInfo", "已选瓦片信息:"},
                {"noTileSelected", "未选择瓦片。请先使用上方的选择工具选择瓦片。"},
                {"tileName", "瓦片名称:"},
                {"spriteName", "精灵名称:"},
                {"sprite", "精灵: 无"},
                {"replaceWith", "替换为:"},
                {"replacementTile", "替换用瓦片"},
                {"replaceSelectedTiles", "替换选中的瓦片"},
                {"replaceAllSimilarTiles", "替换所有相似瓦片"},
                {"recordingTitle", "动作录制"},
                {"recordingName", "录制名称"},
                {"startRecording", "开始录制"},
                {"stopRecording", "停止录制"},
                {"recordingInProgress", "正在录制... 已记录 {0} 个操作"},
                {"savedRecordings", "已保存的录制"},
                {"noSavedRecordings", "没有已保存的录制"},
                {"operations", "个操作"},
                {"playRecording", "播放录制"},
                {"stopPlaying", "停止播放"},
                {"operationsCompleted", "操作已完成"},
                {"newRecording", "新录制"},
                {"Recording started with source tilemap: {0}", "录制已开始，源瓦片地图: {0}"},
                {"Target tilemap: {0}", "目标瓦片地图: {0}"}
            };
            localizedTexts["zh-CN"] = zhTexts;
        }

        private void OnEnable()
        {
            // 加载保存的选区
            LoadSavedSelections();

            // 加载上次使用的Tilemap设置
            LoadTilemapPreferences();

            // 初始化本地化文本
            InitializeLocalization();

            // 加载保存的录制
            LoadSavedRecordings();

            // 获取当前语言
            languageCode = TilemapLanguageManager.GetCurrentLanguageCode();

            // 确保在启用窗口时不会自动进入选择模式
            isSelecting = false;
            isRecording = false;
            isPlayingRecording = false;
            SceneView.duringSceneGui -= OnSceneGUI;

            // 添加更新回调，用于播放录制
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            // 确保在禁用窗口时清除场景GUI回调
            isSelecting = false;
            SceneView.duringSceneGui -= OnSceneGUI;
            // 移除更新回调
            EditorApplication.update -= OnEditorUpdate;

            // 确保在禁用窗口时清除录制状态
            isRecording = false;
            isPlayingRecording = false;
        }
#region ONGUI
        private void OnGUI()
        {
            // 使用本地化文本
            GUILayout.Label(GetLocalizedText("tilemapLayerTool"), EditorStyles.boldLabel);

            // Tilemap 选择
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedText("tilemapSelection"), EditorStyles.boldLabel);
            Tilemap newSourceTilemap = EditorGUILayout.ObjectField(GetLocalizedText("sourceTilemap"), sourceTilemap, typeof(Tilemap), true) as Tilemap;
            if (newSourceTilemap != sourceTilemap)
            {
                sourceTilemap = newSourceTilemap;
                SaveTilemapPreferences();
            }

            Tilemap newTargetTilemap = EditorGUILayout.ObjectField(GetLocalizedText("targetTilemap"), targetTilemap, typeof(Tilemap), true) as Tilemap;
            if (newTargetTilemap != targetTilemap)
            {
                targetTilemap = newTargetTilemap;
                SaveTilemapPreferences();
            }

            // 清空目标选项
            clearTargetBeforeMove = EditorGUILayout.Toggle(GetLocalizedText("clearTargetBeforeMove"), clearTargetBeforeMove);

            // 选区操作
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedText("selectionTools"), EditorStyles.boldLabel);

            // 新增：是否使用修饰键覆盖模式
            useModifierKeysForMode = EditorGUILayout.Toggle(GetLocalizedText("useModifierKeys"), useModifierKeysForMode);

            // 选择模式工具栏
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = currentSelectionMode == SelectionMode.New ? Color.cyan : Color.white;
            if (GUILayout.Button(new GUIContent(GetLocalizedText("new"), "Create a new selection (N)")))
            {
                currentSelectionMode = SelectionMode.New;
            }

            GUI.backgroundColor = currentSelectionMode == SelectionMode.Add ? Color.cyan : Color.white;
            if (GUILayout.Button(new GUIContent(GetLocalizedText("add"), "Add to selection (Shift+Drag)")))
            {
                currentSelectionMode = SelectionMode.Add;
            }

            GUI.backgroundColor = currentSelectionMode == SelectionMode.Subtract ? Color.cyan : Color.white;
            if (GUILayout.Button(new GUIContent(GetLocalizedText("subtract"), "Subtract from selection (Alt+Drag)")))
            {
                currentSelectionMode = SelectionMode.Subtract;
            }

            GUI.backgroundColor = currentSelectionMode == SelectionMode.Intersect ? Color.cyan : Color.white;
            if (GUILayout.Button(new GUIContent(GetLocalizedText("intersect"), "Intersect with selection")))
            {
                currentSelectionMode = SelectionMode.Intersect;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (!isSelecting)
            {
                if (GUILayout.Button(GetLocalizedText("startSelectionMode")))
                {
                    isSelecting = true;
                    SceneView.duringSceneGui -= OnSceneGUI; // 移除以防重复添加
                    SceneView.duringSceneGui += OnSceneGUI;
                    SceneView.RepaintAll(); // 立即重绘场景视图
                }
            }
            else
            {
                // 使用红色按钮表示可以退出选择模式
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button(GetLocalizedText("exitSelectionMode")))
                {
                    isSelecting = false;
                    SceneView.duringSceneGui -= OnSceneGUI;
                    SceneView.RepaintAll(); // 立即重绘场景视图
                }
                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button(GetLocalizedText("clearSelection")))
            {
                selectedCells.Clear();
                isSelecting = false;
                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.RepaintAll();
            }

            EditorGUI.BeginDisabledGroup(sourceTilemap == null || selectedCells.Count == 0);
            if (GUILayout.Button(new GUIContent(GetLocalizedText("selectSimilarTiles"), "Select all tiles of the same type as currently selected")))
            {
                SelectSimilarTiles();
            }
            EditorGUI.EndDisabledGroup();

            if (isSelecting)
            {
                EditorGUILayout.HelpBox(GetLocalizedText("selectionHelp"), MessageType.Info);
                EditorGUILayout.LabelField(GetLocalizedText("currentSelection", selectedCells.Count));

                if (GUILayout.Button(GetLocalizedText("finishSelection")))
                {
                    isSelecting = false;
                    SceneView.duringSceneGui -= OnSceneGUI;
                }
            }

            // 移动按钮
            EditorGUI.BeginDisabledGroup(sourceTilemap == null || targetTilemap == null || (!isSelecting && selectedSavedSelectionIndex < 0));
            if (GUILayout.Button(GetLocalizedText("moveSelectedTiles")))
            {
                // 如果有保存的选区被选中且当前没有活动选择
                if (selectedCells.Count == 0 && selectedSavedSelectionIndex >= 0 && selectedSavedSelectionIndex < savedSelections.Count)
                {
                    // 使用保存的选区
                    SavedSelection selection = savedSelections[selectedSavedSelectionIndex];
                    selectedCells = selection.GetCells();

                    // 获取边界用于显示
                    selection.GetBounds(out selectionStart, out selectionEnd);
                }

                MoveTiles();

                // 如果在选择模式，完成后退出
                if (isSelecting)
                {
                    isSelecting = false;
                    SceneView.duringSceneGui -= OnSceneGUI;
                }
            }
            EditorGUI.EndDisabledGroup();

            // 保存/加载选区
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedText("saveLoadSelections"), EditorStyles.boldLabel);

            // 保存当前选区
            EditorGUI.BeginDisabledGroup(selectedCells.Count == 0);
            EditorGUILayout.BeginHorizontal();
            newSelectionName = EditorGUILayout.TextField(newSelectionName);
            if (GUILayout.Button(GetLocalizedText("saveSelection"), GUILayout.Width(120)))
            {
                SaveCurrentSelection();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            // 显示保存的选区列表
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedText("savedSelections"), EditorStyles.boldLabel);

            if (savedSelections.Count == 0)
            {
                EditorGUILayout.HelpBox(GetLocalizedText("noSavedSelections"), MessageType.Info);
            }
            else
            {
                // 显示保存的选区
                for (int i = 0; i < savedSelections.Count; i++)
                {
                    SavedSelection selection = savedSelections[i];
                    EditorGUILayout.BeginHorizontal();

                    // 选择按钮
                    bool isSelected = (i == selectedSavedSelectionIndex);
                    bool newIsSelected = GUILayout.Toggle(isSelected, "", GUILayout.Width(20));
                    if (newIsSelected != isSelected)
                    {
                        selectedSavedSelectionIndex = newIsSelected ? i : -1;
                    }

                    // 选区信息 - 显示名称和单元格数量
                    selection.GetBounds(out Vector2Int min, out Vector2Int max);
                    EditorGUILayout.LabelField($"{selection.name} ({selection.cells.Count} cells, bounds: {min}-{max})");

                    // 删除按钮
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        savedSelections.RemoveAt(i);
                        SaveSavedSelections();
                        i--; // 调整索引
                        if (selectedSavedSelectionIndex >= savedSelections.Count)
                        {
                            selectedSavedSelectionIndex = savedSelections.Count - 1;
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            // 加载选区按钮
            EditorGUI.BeginDisabledGroup(selectedSavedSelectionIndex < 0);
            if (GUILayout.Button(GetLocalizedText("loadSelectedArea")))
            {
                LoadSelectedArea();
            }
            EditorGUI.EndDisabledGroup();

            //Tile Replacement
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedText("tileReplacement"), EditorStyles.boldLabel);

            // 显示/隐藏Tile替换选项
            showTileReplaceOptions = EditorGUILayout.Foldout(showTileReplaceOptions, GetLocalizedText("tileReplacementOptions"));
            if (showTileReplaceOptions)
            {
                EditorGUI.BeginDisabledGroup(sourceTilemap == null);

                // 显示选中的Tile信息
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(GetLocalizedText("selectedTileInfo"), EditorStyles.boldLabel);

                // 从当前选择中获取Tile
                if (selectedCells.Count > 0 && sourceTilemap != null)
                {
                    // 获取第一个选中单元格的Tile
                    Vector2Int firstCell = selectedCells.First();
                    Vector3Int tilePos = new Vector3Int(firstCell.x, firstCell.y, 0);
                    TileBase currentTile = sourceTilemap.GetTile(tilePos);

                    // 如果选中了Tile，则更新selectedTile
                    if (currentTile != null && selectedTile != currentTile)
                    {
                        selectedTile = currentTile;
                    }
                }

                if (selectedTile != null)
                {
                    tileInfoScrollPosition = EditorGUILayout.BeginScrollView(tileInfoScrollPosition, GUILayout.Height(100));
                    EditorGUILayout.LabelField(GetLocalizedText("tileName"), selectedTile.name);

                    // 获取并显示Tile对应的Sprite
                    Sprite tileSprite = GetSpriteFromTile(selectedTile);
                    if (tileSprite != null)
                    {
                        EditorGUILayout.LabelField(GetLocalizedText("spriteName"), tileSprite.name);

                        // 显示Sprite预览
                        Rect previewRect = EditorGUILayout.GetControlRect(false, 64);
                        EditorGUI.DrawPreviewTexture(previewRect, tileSprite.texture, null, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(GetLocalizedText("sprite"));
                    }

                    EditorGUILayout.EndScrollView();
                }
                else
                {
                    EditorGUILayout.HelpBox(GetLocalizedText("noTileSelected"), MessageType.Info);
                }
                EditorGUILayout.EndVertical();

                // 替换Tile选项
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(GetLocalizedText("replaceWith"), EditorStyles.boldLabel);

                // 选择替换用的Tile
                replacementTile = (TileBase)EditorGUILayout.ObjectField(GetLocalizedText("replacementTile"), replacementTile, typeof(TileBase), false);

                // 显示替换Tile的预览
                if (replacementTile != null)
                {
                    Sprite replacementSprite = GetSpriteFromTile(replacementTile);
                    if (replacementSprite != null)
                    {
                        Rect previewRect = EditorGUILayout.GetControlRect(false, 64);
                        EditorGUI.DrawPreviewTexture(previewRect, replacementSprite.texture, null, ScaleMode.ScaleToFit);
                    }
                }

                // 替换按钮
                EditorGUI.BeginDisabledGroup(selectedTile == null || replacementTile == null || selectedCells.Count == 0);
                if (GUILayout.Button(GetLocalizedText("replaceSelectedTiles")))
                {
                    ReplaceTiles();
                }
                EditorGUI.EndDisabledGroup();

                // 替换所有相同类型的Tile按钮
                EditorGUI.BeginDisabledGroup(selectedTile == null || replacementTile == null);
                if (GUILayout.Button(GetLocalizedText("replaceAllSimilarTiles")))
                {
                    ReplaceAllSimilarTiles();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();

                EditorGUI.EndDisabledGroup();
            }

            // 添加录制UI
            AddRecordingUI();
        }

        private void AddRecordingUI()
        {
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedText("recordingTitle"), EditorStyles.boldLabel);

            // 修复这里的错误 - 删除多余的EndHorizontal调用
            // EditorGUILayout.EndHorizontal(); // 这行是多余的，应该删除

            // 录制控制
            EditorGUILayout.BeginHorizontal();
            if (!isRecording)
            {
                // 录制名称输入
                newRecordingName = EditorGUILayout.TextField(GetLocalizedText("recordingName"), newRecordingName);

                // 开始录制按钮
                if (GUILayout.Button(GetLocalizedText("startRecording"), GUILayout.Width(120)))
                {
                    StartRecording();
                }
            }
            else
            {
                // 停止录制按钮
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button(GetLocalizedText("stopRecording")))
                {
                    StopRecording();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // 如果正在录制，显示当前录制状态
            if (isRecording)
            {
                EditorGUILayout.HelpBox(GetLocalizedText("recordingInProgress", recordedOperations.Count), MessageType.Info);
            }

            // 显示保存的录制列表
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedText("savedRecordings"), EditorStyles.boldLabel);

            if (savedRecordings.Count == 0)
            {
                EditorGUILayout.HelpBox(GetLocalizedText("noSavedRecordings"), MessageType.Info);
            }
            else
            {
                // 显示保存的录制
                scrollPositionRecordings = EditorGUILayout.BeginScrollView(scrollPositionRecordings, GUILayout.Height(150));
                for (int i = 0; i < savedRecordings.Count; i++)
                {
                    SavedRecording recording = savedRecordings[i];
                    EditorGUILayout.BeginHorizontal();

                    // 选择按钮
                    bool isSelected = (i == selectedRecordingIndex);
                    bool newIsSelected = GUILayout.Toggle(isSelected, "", GUILayout.Width(20));
                    if (newIsSelected != isSelected)
                    {
                        selectedRecordingIndex = newIsSelected ? i : -1;
                    }

                    // 录制信息
                    EditorGUILayout.LabelField($"{recording.name} ({recording.operations.Count} {GetLocalizedText("operations")})");

                    // 删除按钮
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        if (isPlayingRecording && selectedRecordingIndex == i)
                        {
                            isPlayingRecording = false;
                        }

                        savedRecordings.RemoveAt(i);
                        SaveSavedRecordings();
                        i--; // 调整索引
                        if (selectedRecordingIndex >= savedRecordings.Count)
                        {
                            selectedRecordingIndex = savedRecordings.Count - 1;
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                // 播放按钮
                EditorGUI.BeginDisabledGroup(selectedRecordingIndex < 0 || isRecording || isPlayingRecording);
                if (GUILayout.Button(GetLocalizedText("playRecording")))
                {
                    PlayRecording(selectedRecordingIndex);
                }
                EditorGUI.EndDisabledGroup();

                // 如果正在播放，显示停止按钮
                if (isPlayingRecording)
                {
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button(GetLocalizedText("stopPlaying")))
                    {
                        isPlayingRecording = false;
                    }
                    GUI.backgroundColor = Color.white;

                    // 显示播放进度
                    if (selectedRecordingIndex >= 0 && selectedRecordingIndex < savedRecordings.Count)
                    {
                        SavedRecording recording = savedRecordings[selectedRecordingIndex];
                        float progress = (float)currentOperationIndex / recording.operations.Count;
                        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), progress,
                            $"{currentOperationIndex}/{recording.operations.Count} {GetLocalizedText("operationsCompleted")}");
                    }
                }
            }
        }

#endregion
        private void SelectSimilarTiles()
        {
            if (sourceTilemap == null || selectedCells.Count == 0)
                return;

            // 收集当前选中单元格中的所有瓦片类型
            HashSet<TileBase> selectedTileTypes = new HashSet<TileBase>();
            foreach (Vector2Int cell in selectedCells)
            {
                Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
                TileBase tile = sourceTilemap.GetTile(pos);
                if (tile != null)
                {
                    selectedTileTypes.Add(tile);
                }
            }

            // 如果没有找到有效的瓦片类型，则退出
            if (selectedTileTypes.Count == 0)
                return;

            // 获取tilemap的边界
            sourceTilemap.CompressBounds();
            BoundsInt bounds = sourceTilemap.cellBounds;

            // 创建一个新的集合来存储所有匹配的单元格
            HashSet<Vector2Int> newSelection = new HashSet<Vector2Int>();

            // 遍历整个tilemap
            for (int x = bounds.min.x; x < bounds.max.x; x++)
            {
                for (int y = bounds.min.y; y < bounds.max.y; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    TileBase tile = sourceTilemap.GetTile(pos);

                    // 如果该位置的瓦片类型与选中的任一类型匹配，则添加到新选区
                    if (tile != null && selectedTileTypes.Contains(tile))
                    {
                        newSelection.Add(new Vector2Int(x, y));
                    }
                }
            }

            // 更新选区
            selectedCells = newSelection;

            // 刷新场景视图
            SceneView.RepaintAll();
        }


        // 添加新方法用于保存Tilemap引用
        private void SaveTilemapPreferences()
        {
            // 保存源Tilemap路径
            if (sourceTilemap != null)
            {
                string sourcePath = GetGameObjectPath(sourceTilemap.gameObject);
                EditorPrefs.SetString(PREF_SOURCE_TILEMAP_PATH, sourcePath);
            }

            // 保存目标Tilemap路径
            if (targetTilemap != null)
            {
                string targetPath = GetGameObjectPath(targetTilemap.gameObject);
                EditorPrefs.SetString(PREF_TARGET_TILEMAP_PATH, targetPath);
            }
        }

        // 添加新方法用于加载Tilemap引用
        private void LoadTilemapPreferences()
        {
            // 尝试加载源Tilemap
            string sourcePath = EditorPrefs.GetString(PREF_SOURCE_TILEMAP_PATH, "");
            if (!string.IsNullOrEmpty(sourcePath))
            {
                GameObject sourceObj = FindGameObjectByPath(sourcePath);
                if (sourceObj != null)
                {
                    sourceTilemap = sourceObj.GetComponent<Tilemap>();
                }
            }

            // 尝试加载目标Tilemap
            string targetPath = EditorPrefs.GetString(PREF_TARGET_TILEMAP_PATH, "");
            if (!string.IsNullOrEmpty(targetPath))
            {
                GameObject targetObj = FindGameObjectByPath(targetPath);
                if (targetObj != null)
                {
                    targetTilemap = targetObj.GetComponent<Tilemap>();
                }
            }
        }

        // 添加获取GameObject路径的辅助方法
        private string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return "";

            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        // 添加通过路径查找GameObject的辅助方法
        private GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            return GameObject.Find(path);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // 如果不在选择模式，则不处理场景GUI事件
            if (!isSelecting)
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                return;
            }
            // 处理鼠标事件和绘制选择框
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Event e = Event.current;

            // 检测修饰键
            isShiftPressed = e.shift;
            isAltPressed = e.alt;

            // 根据修饰键更新选择模式，但仅在启用了修饰键覆盖时
            SelectionMode previousMode = currentSelectionMode;

            if (useModifierKeysForMode)
            {
                if (isShiftPressed)
                {
                    currentSelectionMode = SelectionMode.Add;
                }
                else if (isAltPressed)
                {
                    currentSelectionMode = SelectionMode.Subtract;
                }
                else if (!isShiftPressed && !isAltPressed &&
                        (previousMode == SelectionMode.Add || previousMode == SelectionMode.Subtract))
                {
                    // 如果之前是由修饰键设置的模式，且现在没有按下修饰键，则恢复为New模式
                    currentSelectionMode = SelectionMode.New;
                }
                // 如果之前是手动选择的Intersect或其他模式，则保持不变
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector3 mouseWorldPos = ray.origin;

                if (sourceTilemap != null)
                {
                    Vector3Int cellPos = sourceTilemap.WorldToCell(mouseWorldPos);
                    selectionStart = new Vector2Int(cellPos.x, cellPos.y);
                    selectionEnd = selectionStart;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector3 mouseWorldPos = ray.origin;

                if (sourceTilemap != null)
                {
                    Vector3Int cellPos = sourceTilemap.WorldToCell(mouseWorldPos);
                    Vector2Int newSelectionEnd = new Vector2Int(cellPos.x, cellPos.y);

                    // 只有当选择结束位置改变时才重绘
                    if (selectionEnd != newSelectionEnd)
                    {
                        selectionEnd = newSelectionEnd;
                        sceneView.Repaint();
                    }
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                // 鼠标释放时应用选区
                ApplyCurrentSelection();

                // 重置选择框起点和终点，确保黄色选框消失
                selectionStart = Vector2Int.zero;
                selectionEnd = Vector2Int.zero;

                e.Use();
                sceneView.Repaint(); // 强制重绘，确保黄色选框消失
            }

            // 绘制当前拖拽的选择框
            if (sourceTilemap != null)
            {
                // 只有在鼠标按下并拖拽时才绘制临时选区（黄色框）
                if (isSelecting && e.type != EventType.MouseUp &&
                    (selectionStart != Vector2Int.zero || selectionEnd != Vector2Int.zero))
                {
                    DrawSelectionArea(selectionStart, selectionEnd, new Color(1, 1, 0, 0.3f), Color.yellow);
                }

                // 无论是否在选择模式，都绘制已选中的单元格（绿色高亮）
                DrawSelectedCells();
            }

            // 如果有保存的选区被选中，也绘制它（使用不同颜色）
            if (!isSelecting && selectedSavedSelectionIndex >= 0 && selectedSavedSelectionIndex < savedSelections.Count && sourceTilemap != null)
            {
                SavedSelection selection = savedSelections[selectedSavedSelectionIndex];
                HashSet<Vector2Int> cells = selection.GetCells();

                // 绘制选中的单元格
                foreach (Vector2Int cell in cells)
                {
                    Vector3 worldPos = sourceTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                    Vector3 cellSize = sourceTilemap.cellSize;

                    Vector3[] vertices = new Vector3[]
                    {
                        worldPos + new Vector3(-cellSize.x/2, -cellSize.y/2, 0),
                        worldPos + new Vector3(cellSize.x/2, -cellSize.y/2, 0),
                        worldPos + new Vector3(cellSize.x/2, cellSize.y/2, 0),
                        worldPos + new Vector3(-cellSize.x/2, cellSize.y/2, 0)
                    };

                    Handles.DrawSolidRectangleWithOutline(vertices, new Color(0, 0.5f, 1f, 0.3f), Color.blue);
                }

                // 绘制边界框
                selection.GetBounds(out Vector2Int min, out Vector2Int max);
            }

            // 处理键盘快捷键
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.N) // 新建选区
                {
                    currentSelectionMode = SelectionMode.New;
                    e.Use();
                    Repaint();
                }
                else if (e.keyCode == KeyCode.Escape) // 取消选择
                {
                    if (isSelecting)
                    {
                        isSelecting = false;
                        // 重置选择框，确保黄色选框消失
                        selectionStart = Vector2Int.zero;
                        selectionEnd = Vector2Int.zero;
                        SceneView.duringSceneGui -= OnSceneGUI;
                        e.Use();
                        sceneView.Repaint(); // 强制重绘
                        Repaint();
                    }
                }
            }
        }

        // 添加应用当前选区的方法
        private void ApplyCurrentSelection()
        {
            // 计算当前拖拽框覆盖的单元格
            int minX = Mathf.Min(selectionStart.x, selectionEnd.x);
            int maxX = Mathf.Max(selectionStart.x, selectionEnd.x);
            int minY = Mathf.Min(selectionStart.y, selectionEnd.y);
            int maxY = Mathf.Max(selectionStart.y, selectionEnd.y);

            // 创建临时集合存储当前拖拽选择的单元格
            HashSet<Vector2Int> currentDragSelection = new HashSet<Vector2Int>();

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int cellPos = new Vector2Int(x, y);

                    // 只添加有瓦片的单元格
                    Vector3Int tilePos = new Vector3Int(x, y, 0);
                    if (sourceTilemap != null && sourceTilemap.GetTile(tilePos) != null)
                    {
                        currentDragSelection.Add(cellPos);
                    }
                }
            }

            // 根据选择模式应用选区
            switch (currentSelectionMode)
            {
                case SelectionMode.New:
                    selectedCells = new HashSet<Vector2Int>(currentDragSelection);
                    break;

                case SelectionMode.Add:
                    selectedCells.UnionWith(currentDragSelection);
                    break;

                case SelectionMode.Subtract:
                    selectedCells.ExceptWith(currentDragSelection);
                    break;

                case SelectionMode.Intersect:
                    selectedCells.IntersectWith(currentDragSelection);
                    break;
            }

            // 如果使用修饰键覆盖模式，并且修饰键已释放，则重置为默认模式
            if (useModifierKeysForMode && !isShiftPressed && !isAltPressed)
            {
                currentSelectionMode = SelectionMode.New;
            }
            // 如果不使用修饰键覆盖，则保持当前选择模式不变
            // 如果正在录制，记录此选择操作
            if (isRecording)
            {
                recordedOperations.Add(new SelectAreaOperation(selectionStart, selectionEnd, currentSelectionMode));
            }
        }

        // 替换DrawSelectedCells方法，使用GPU渲染高亮区域
        private void DrawSelectedCells()
        {
            if (sourceTilemap == null || selectedCells.Count == 0) return;

            // 初始化高亮材质（如果尚未创建）
            if (highlightMaterial == null)
            {
                // 使用Unity内置的半透明材质
                highlightMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                highlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                highlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                highlightMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                highlightMaterial.SetInt("_ZWrite", 0);
            }

            // 计算所有选中单元格的边界
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (Vector2Int cell in selectedCells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            // 准备GPU渲染
            highlightMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Handles.matrix);

            // 设置颜色
            Color fillColor = new Color(0, 0.8f, 0, 0.4f);

            // 开始绘制四边形
            GL.Begin(GL.QUADS);
            GL.Color(fillColor);

            Vector3 cellSize = sourceTilemap.cellSize;

            // 使用GPU批处理绘制所有选中的单元格
            foreach (Vector2Int cell in selectedCells)
            {
                Vector3 worldPos = sourceTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));

                // 绘制四边形
                GL.Vertex3(worldPos.x - cellSize.x / 2, worldPos.y - cellSize.y / 2, 0);
                GL.Vertex3(worldPos.x + cellSize.x / 2, worldPos.y - cellSize.y / 2, 0);
                GL.Vertex3(worldPos.x + cellSize.x / 2, worldPos.y + cellSize.y / 2, 0);
                GL.Vertex3(worldPos.x - cellSize.x / 2, worldPos.y + cellSize.y / 2, 0);
            }

            GL.End();
            GL.PopMatrix();

            // 如果选中的单元格数量很多，显示数量信息
            if (selectedCells.Count > 1000)
            {
                Vector3 labelPos = sourceTilemap.GetCellCenterWorld(new Vector3Int((minX + maxX) / 2, (minY + maxY) / 2, 0));
                Handles.Label(labelPos, $"{selectedCells.Count} tiles selected", EditorStyles.boldLabel);
            }
        }

        // 绘制选择区域
        private void DrawSelectionArea(Vector2Int start, Vector2Int end, Color fillColor = default, Color outlineColor = default)
        {
            // 如果起点和终点都是零，则不绘制（避免绘制无效选区）
            if (start == Vector2Int.zero && end == Vector2Int.zero)
                return;

            if (fillColor == default) fillColor = new Color(0, 1, 0, 0.3f);
            if (outlineColor == default) outlineColor = Color.green;

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            Handles.color = fillColor;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3 worldPos = sourceTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
                    Vector3 cellSize = sourceTilemap.cellSize;

                    Vector3[] vertices = new Vector3[]
                    {
                        worldPos + new Vector3(-cellSize.x/2, -cellSize.y/2, 0),
                        worldPos + new Vector3(cellSize.x/2, -cellSize.y/2, 0),
                        worldPos + new Vector3(cellSize.x/2, cellSize.y/2, 0),
                        worldPos + new Vector3(-cellSize.x/2, cellSize.y/2, 0)
                    };

                    Handles.DrawSolidRectangleWithOutline(vertices, fillColor, outlineColor);
                }
            }
        }

        // 移动瓦片
        private void MoveTiles()
        {
            if (sourceTilemap == null || targetTilemap == null)
                return;

            // 如果正在录制，记录此操作
            if (isRecording && selectedCells.Count > 0)
            {
                recordedOperations.Add(new MoveTileOperation(selectedCells, clearTargetBeforeMove));
            }

            // 如果有保存的选区被选中且没有活动选择
            if (selectedCells.Count == 0 && selectedSavedSelectionIndex >= 0 && selectedSavedSelectionIndex < savedSelections.Count)
            {
                // 使用保存的选区
                SavedSelection selection = savedSelections[selectedSavedSelectionIndex];
                HashSet<Vector2Int> cells = selection.GetCells();

                // 注册撤销
                Undo.RegisterCompleteObjectUndo(new Object[] { sourceTilemap, targetTilemap }, "Move Tiles Between Tilemaps");

                // 如果选择了清空目标，先清空目标区域
                if (clearTargetBeforeMove)
                {
                    targetTilemap.ClearAllTiles();
                }

                // 移动瓦片
                foreach (Vector2Int cell in cells)
                {
                    Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
                    TileBase tile = sourceTilemap.GetTile(pos);

                    if (tile != null)
                    {
                        targetTilemap.SetTile(pos, tile);
                        sourceTilemap.SetTile(pos, null);
                    }
                }
            }
            else if (selectedCells.Count > 0)
            {
                // 使用已选中的单元格

                // 注册撤销
                Undo.RegisterCompleteObjectUndo(new Object[] { sourceTilemap, targetTilemap }, "Move Tiles Between Tilemaps");

                // 如果选择了清空目标，先清空目标区域
                if (clearTargetBeforeMove)
                {
                    foreach (Vector2Int cell in selectedCells)
                    {
                        Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
                        targetTilemap.SetTile(pos, null);
                    }
                }

                // 移动瓦片
                foreach (Vector2Int cell in selectedCells)
                {
                    Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
                    TileBase tile = sourceTilemap.GetTile(pos);

                    if (tile != null)
                    {
                        targetTilemap.SetTile(pos, tile);
                        sourceTilemap.SetTile(pos, null);
                    }
                }

                // 移动完成后清空选区
                selectedCells.Clear();
            }
        }

        private void SaveCurrentSelection()
        {
            if (string.IsNullOrEmpty(newSelectionName))
            {
                newSelectionName = "Selection " + (savedSelections.Count + 1);
            }

            // 检查是否已存在同名选区
            for (int i = 0; i < savedSelections.Count; i++)
            {
                if (savedSelections[i].name == newSelectionName)
                {
                    // 更新已有选区
                    savedSelections[i] = new SavedSelection(newSelectionName, selectedCells);
                    SaveSavedSelections();
                    return;
                }
            }

            // 添加新选区
            savedSelections.Add(new SavedSelection(newSelectionName, selectedCells));
            SaveSavedSelections();

            // 重置名称
            newSelectionName = "New Selection";
        }

        private void LoadSelectedArea()
        {
            if (selectedSavedSelectionIndex >= 0 && selectedSavedSelectionIndex < savedSelections.Count)
            {
                SavedSelection selection = savedSelections[selectedSavedSelectionIndex];

                // 清空当前选区
                selectedCells.Clear();

                // 加载保存的单元格
                selectedCells = selection.GetCells();

                // 确保在场景视图中可以看到选区
                isSelecting = true;
                SceneView.duringSceneGui -= OnSceneGUI; // 先移除以防重复添加
                SceneView.duringSceneGui += OnSceneGUI;

                // 刷新场景视图
                SceneView.RepaintAll();
            }
        }

        // 保存所有选区到文件
        private void SaveSavedSelections()
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(SaveFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 序列化选区列表
                string json = JsonUtility.ToJson(new SavedSelectionList { selections = savedSelections });
                File.WriteAllText(SaveFilePath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error saving selections: {e.Message}");
            }
        }

        // 从文件加载选区
        private void LoadSavedSelections()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    SavedSelectionList loadedList = JsonUtility.FromJson<SavedSelectionList>(json);
                    if (loadedList != null && loadedList.selections != null)
                    {
                        savedSelections = loadedList.selections;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading selections: {e.Message}");
                savedSelections = new List<SavedSelection>();
            }
        }

        // 用于序列化的包装类
        [System.Serializable]
        private class SavedSelectionList
        {
            public List<SavedSelection> selections;
        }

        private Sprite GetSpriteFromTile(TileBase tile)
        {
            if (tile == null) return null;

            // 检查缓存中是否已有此Tile的Sprite
            if (tileToSpriteMap.TryGetValue(tile, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            // 尝试获取Sprite
            Sprite sprite = null;

            // 从不同类型的Tile中获取Sprite
            if (tile is Tile standardTile)
            {
                sprite = standardTile.sprite;
            }
            else
            {
                // 使用反射尝试获取其他类型Tile的sprite属性
                System.Reflection.PropertyInfo spriteProperty = tile.GetType().GetProperty("sprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (spriteProperty != null)
                {
                    sprite = spriteProperty.GetValue(tile) as Sprite;
                }
            }

            // 缓存结果
            if (sprite != null)
            {
                tileToSpriteMap[tile] = sprite;
            }

            return sprite;
        }

        private void ReplaceTiles()
        {
            if (sourceTilemap == null || selectedTile == null || replacementTile == null || selectedCells.Count == 0)
                return;

            // 如果正在录制，记录此操作
            if (isRecording)
            {
                recordedOperations.Add(new ReplaceTileOperation(selectedCells, selectedTile, replacementTile, false));
            }

            // 注册撤销
            Undo.RegisterCompleteObjectUndo(sourceTilemap, "Replace Tiles");

            int replacedCount = 0;

            // 替换选中区域内的所有匹配Tile
            foreach (Vector2Int cell in selectedCells)
            {
                Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
                TileBase currentTile = sourceTilemap.GetTile(pos);

                if (currentTile == selectedTile)
                {
                    sourceTilemap.SetTile(pos, replacementTile);
                    replacedCount++;
                }
            }

            Debug.Log($"Replaced {replacedCount} tiles of type '{selectedTile.name}' with '{replacementTile.name}'.");
            SceneView.RepaintAll();
        }

        private void ReplaceAllSimilarTiles()
        {
            if (sourceTilemap == null || selectedTile == null || replacementTile == null)
                return;

            // 如果正在录制，记录此操作
            if (isRecording)
            {
                recordedOperations.Add(new ReplaceTileOperation(selectedCells, selectedTile, replacementTile, true));
            }

            // 注册撤销
            Undo.RegisterCompleteObjectUndo(sourceTilemap, "Replace All Similar Tiles");

            // 获取tilemap的边界
            sourceTilemap.CompressBounds();
            BoundsInt bounds = sourceTilemap.cellBounds;

            int replacedCount = 0;

            // 遍历整个tilemap
            for (int x = bounds.min.x; x < bounds.max.x; x++)
            {
                for (int y = bounds.min.y; y < bounds.max.y; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    TileBase tile = sourceTilemap.GetTile(pos);

                    // 如果找到匹配的Tile，则替换
                    if (tile == selectedTile)
                    {
                        sourceTilemap.SetTile(pos, replacementTile);
                        replacedCount++;
                    }
                }
            }

            Debug.Log($"Replaced {replacedCount} tiles.");
            SceneView.RepaintAll();
        }


        #region Recorder
        private void StartRecording()
        {
            if (isRecording || isPlayingRecording)
                return;

            if (string.IsNullOrEmpty(newRecordingName))
            {
                newRecordingName = GetLocalizedText("newRecording");
            }

            isRecording = true;
            recordedOperations.Clear();

            // 记录初始状态
            if (sourceTilemap != null)
            {
                Debug.LogFormat(GetLocalizedText("Recording started with source tilemap: {0}"), GetGameObjectPath(sourceTilemap.gameObject));
            }

            if (targetTilemap != null)
            {
                Debug.LogFormat(GetLocalizedText("Target tilemap: {0}"), GetGameObjectPath(targetTilemap.gameObject));
            }
        }

        // 停止录制
        private void StopRecording()
        {
            if (!isRecording)
                return;

            isRecording = false;

            // 保存录制
            if (recordedOperations.Count > 0)
            {
                string sourcePath = sourceTilemap != null ? GetGameObjectPath(sourceTilemap.gameObject) : "";
                string targetPath = targetTilemap != null ? GetGameObjectPath(targetTilemap.gameObject) : "";

                SavedRecording recording = new SavedRecording(
                    newRecordingName,
                    sourcePath,
                    targetPath,
                    recordedOperations
                );

                // 检查是否已存在同名录制
                bool exists = false;
                for (int i = 0; i < savedRecordings.Count; i++)
                {
                    if (savedRecordings[i].name == newRecordingName)
                    {
                        savedRecordings[i] = recording;
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    savedRecordings.Add(recording);
                }

                SaveSavedRecordings();

                // 重置名称
                newRecordingName = GetLocalizedText("newRecording");
            }
        }

        // 播放录制
        private void PlayRecording(int index)
        {
            if (isRecording || isPlayingRecording || index < 0 || index >= savedRecordings.Count)
                return;

            SavedRecording recording = savedRecordings[index];

            // 尝试查找源和目标Tilemap
            GameObject sourceObj = FindGameObjectByPath(recording.sourceMapPath);
            GameObject targetObj = FindGameObjectByPath(recording.targetMapPath);

            if (sourceObj != null)
            {
                sourceTilemap = sourceObj.GetComponent<Tilemap>();
            }

            if (targetObj != null)
            {
                targetTilemap = targetObj.GetComponent<Tilemap>();
            }

            if (sourceTilemap == null)
            {
                Debug.LogError("Cannot find source tilemap: " + recording.sourceMapPath);
                return;
            }

            // 开始播放
            isPlayingRecording = true;
            currentOperationIndex = 0;
            lastOperationTime = EditorApplication.timeSinceStartup;

            Debug.Log("Playing recording: " + recording.name);
        }

        // 编辑器更新回调，用于播放录制
        private void OnEditorUpdate()
        {
            if (!isPlayingRecording || selectedRecordingIndex < 0 || selectedRecordingIndex >= savedRecordings.Count)
                return;

            SavedRecording recording = savedRecordings[selectedRecordingIndex];

            if (currentOperationIndex >= recording.operations.Count)
            {
                // 播放完成
                isPlayingRecording = false;
                Repaint();
                return;
            }

            // 获取当前操作
            TilemapOperation operation = recording.operations[currentOperationIndex];

            // 计算操作间隔
            float delay = 0.2f; // 默认延迟
            if (currentOperationIndex > 0)
            {
                TilemapOperation prevOp = recording.operations[currentOperationIndex - 1];
                delay = operation.timeStamp - prevOp.timeStamp;
                delay = Mathf.Clamp(delay, 0.1f, 2.0f); // 限制延迟范围
            }

            // 检查是否应该执行下一个操作
            if (EditorApplication.timeSinceStartup - lastOperationTime >= delay)
            {
                // 执行操作
                operation.Execute(this);

                // 更新索引和时间
                currentOperationIndex++;
                lastOperationTime = EditorApplication.timeSinceStartup;

                // 刷新UI
                Repaint();
            }
        }

        // 保存录制到文件
        private void SaveSavedRecordings()
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(RecordingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 序列化录制列表
                string json = JsonUtility.ToJson(new SavedRecordingList { recordings = savedRecordings });
                File.WriteAllText(RecordingsFilePath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error saving recordings: {e.Message}");
            }
        }

        // 从文件加载录制
        private void LoadSavedRecordings()
        {
            try
            {
                if (File.Exists(RecordingsFilePath))
                {
                    string json = File.ReadAllText(RecordingsFilePath);
                    SavedRecordingList loadedList = JsonUtility.FromJson<SavedRecordingList>(json);
                    if (loadedList != null && loadedList.recordings != null)
                    {
                        savedRecordings = loadedList.recordings;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading recordings: {e.Message}");
                savedRecordings = new List<SavedRecording>();
            }
        }
        #endregion
    }
}
#endif