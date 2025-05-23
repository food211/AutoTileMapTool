#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO;

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
private struct SerializableVector2Int
{
    public int x;
    public int y;
    
    public SerializableVector2Int(Vector2Int v)
    {
        x = v.x;
        y = v.y;
    }
}
    
    private List<SavedSelection> savedSelections = new List<SavedSelection>();
    private string newSelectionName = "New Selection";
    private int selectedSavedSelectionIndex = -1;
    
    // 保存选区的文件路径
    private string SaveFilePath => Path.Combine(Application.dataPath, "Editor", "TilemapSelections.json");
    
    [MenuItem("Tools/Tilemap/Layer Tool")]
    public static void ShowWindow()
    {
        GetWindow<TilemapLayerTool>("Tilemap Layer Tool");
    }
    
    private void OnEnable()
    {
        // 加载保存的选区
        LoadSavedSelections();
        
        // 加载上次使用的Tilemap设置
        LoadTilemapPreferences();
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Tilemap Layer Tool", EditorStyles.boldLabel);
        
        // Tilemap 选择
        EditorGUILayout.Space();
        GUILayout.Label("Tilemap Selection", EditorStyles.boldLabel);
        Tilemap newSourceTilemap = EditorGUILayout.ObjectField("Source Tilemap", sourceTilemap, typeof(Tilemap), true) as Tilemap;
        if (newSourceTilemap != sourceTilemap)
        {
            sourceTilemap = newSourceTilemap;
            SaveTilemapPreferences();
        }

        Tilemap newTargetTilemap = EditorGUILayout.ObjectField("Target Tilemap", targetTilemap, typeof(Tilemap), true) as Tilemap;
        if (newTargetTilemap != targetTilemap)
        {
            targetTilemap = newTargetTilemap;
            SaveTilemapPreferences();
        }
        
        // 清空目标选项
        clearTargetBeforeMove = EditorGUILayout.Toggle("Clear Target Before Move", clearTargetBeforeMove);
        
        // 选区操作
        EditorGUILayout.Space();
        GUILayout.Label("Selection Tools", EditorStyles.boldLabel);
        
        // 新增：是否使用修饰键覆盖模式
        useModifierKeysForMode = EditorGUILayout.Toggle("Use Modifier Keys (Shift/Alt)", useModifierKeysForMode);
        
        // 选择模式工具栏
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = currentSelectionMode == SelectionMode.New ? Color.cyan : Color.white;
        if (GUILayout.Button(new GUIContent("New", "Create a new selection (N)")))
        {
            currentSelectionMode = SelectionMode.New;
        }
        
        GUI.backgroundColor = currentSelectionMode == SelectionMode.Add ? Color.cyan : Color.white;
        if (GUILayout.Button(new GUIContent("Add", "Add to selection (Shift+Drag)")))
        {
            currentSelectionMode = SelectionMode.Add;
        }
        
        GUI.backgroundColor = currentSelectionMode == SelectionMode.Subtract ? Color.cyan : Color.white;
        if (GUILayout.Button(new GUIContent("Subtract", "Subtract from selection (Alt+Drag)")))
        {
            currentSelectionMode = SelectionMode.Subtract;
        }
        
        GUI.backgroundColor = currentSelectionMode == SelectionMode.Intersect ? Color.cyan : Color.white;
        if (GUILayout.Button(new GUIContent("Intersect", "Intersect with selection")))
        {
            currentSelectionMode = SelectionMode.Intersect;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        
        if (GUILayout.Button("Start Selection"))
        {
            isSelecting = true;
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        if (GUILayout.Button("Clear Selection"))
        {
            selectedCells.Clear();
            isSelecting = false;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }
        
        EditorGUI.BeginDisabledGroup(sourceTilemap == null || selectedCells.Count == 0);
        if (GUILayout.Button(new GUIContent("Select Similar Tiles", "Select all tiles of the same type as currently selected")))
        {
            SelectSimilarTiles();
        }
        EditorGUI.EndDisabledGroup();
        
        if (isSelecting)
        {
            EditorGUILayout.HelpBox("Click and drag in Scene View to select tiles\nShift+Drag: Add to selection\nAlt+Drag: Subtract from selection", MessageType.Info);
            EditorGUILayout.LabelField($"Current selection: {selectedCells.Count} cells");
            
            if (GUILayout.Button("Finish Selection"))
            {
                isSelecting = false;
                SceneView.duringSceneGui -= OnSceneGUI;
            }
        }
        
        // 移动按钮
        EditorGUI.BeginDisabledGroup(sourceTilemap == null || targetTilemap == null || (!isSelecting && selectedSavedSelectionIndex < 0));
        if (GUILayout.Button("Move Selected Tiles"))
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
        GUILayout.Label("Save/Load Selections", EditorStyles.boldLabel);
        
        // 保存当前选区
        EditorGUI.BeginDisabledGroup(selectedCells.Count == 0);
        EditorGUILayout.BeginHorizontal();
        newSelectionName = EditorGUILayout.TextField(newSelectionName);
        if (GUILayout.Button("Save Selection", GUILayout.Width(120)))
        {
            SaveCurrentSelection();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
        
        // 显示保存的选区列表
        EditorGUILayout.Space();
        GUILayout.Label("Saved Selections", EditorStyles.boldLabel);
        
        if (savedSelections.Count == 0)
        {
            EditorGUILayout.HelpBox("No saved selections", MessageType.Info);
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
        if (GUILayout.Button("Load Selected Area"))
        {
            LoadSelectedArea();
        }
        EditorGUI.EndDisabledGroup();
    }

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
            selectionEnd = new Vector2Int(cellPos.x, cellPos.y);
            sceneView.Repaint();
            e.Use();
        }
    }
    else if (e.type == EventType.MouseUp && e.button == 0)
    {
        // 鼠标释放时应用选区
        ApplyCurrentSelection();
        e.Use();
    }
    
    // 绘制当前拖拽的选择框
    if (sourceTilemap != null)
    {
        // 如果正在选择，绘制当前拖拽的临时选区
        if (isSelecting)
        {
            DrawSelectionArea(selectionStart, selectionEnd, new Color(1, 1, 0, 0.3f), Color.yellow);
        }
        
        // 无论是否在选择模式，都绘制已选中的单元格
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
        DrawSelectionBorder(min, max, Color.blue);
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
                SceneView.duringSceneGui -= OnSceneGUI;
                e.Use();
                Repaint();
            }
        }
    }
}

    private void DrawSelectionBorder(Vector2Int min, Vector2Int max, Color color)
{
    if (sourceTilemap == null) return;
    
    Vector3 minWorldPos = sourceTilemap.GetCellCenterWorld(new Vector3Int(min.x, min.y, 0));
    Vector3 maxWorldPos = sourceTilemap.GetCellCenterWorld(new Vector3Int(max.x, max.y, 0));
    Vector3 cellSize = sourceTilemap.cellSize;
    
    // 计算边界框的四个角
    Vector3 bottomLeft = minWorldPos + new Vector3(-cellSize.x/2, -cellSize.y/2, 0);
    Vector3 bottomRight = new Vector3(maxWorldPos.x + cellSize.x/2, minWorldPos.y - cellSize.y/2, 0);
    Vector3 topRight = maxWorldPos + new Vector3(cellSize.x/2, cellSize.y/2, 0);
    Vector3 topLeft = new Vector3(minWorldPos.x - cellSize.x/2, maxWorldPos.y + cellSize.y/2, 0);
    
    // 绘制边界线
    Handles.color = color;
    Handles.DrawLine(bottomLeft, bottomRight);
    Handles.DrawLine(bottomRight, topRight);
    Handles.DrawLine(topRight, topLeft);
    Handles.DrawLine(topLeft, bottomLeft);
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
}

// 添加绘制已选中单元格的方法
private void DrawSelectedCells()
{
    if (sourceTilemap == null) return;
    
    foreach (Vector2Int cell in selectedCells)
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
        
        Handles.DrawSolidRectangleWithOutline(vertices, new Color(0, 0.8f, 0, 0.4f), Color.green);
    }
}
    
    // 绘制选择区域
    private void DrawSelectionArea(Vector2Int start, Vector2Int end, Color fillColor = default, Color outlineColor = default)
    {
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
}
#endif