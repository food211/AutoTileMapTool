using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RegionManager : MonoBehaviour
{
    [Header("References")]
    public Tilemap tilemap;
    public Material regionShaderMaterial;
    
    [Header("Edge Highlight Settings")]
    public Color edgeColor = new Color(1f, 0.8f, 0.2f, 1f);
    [Range(0.01f, 0.5f)]
    public float edgeThickness = 0.1f;
    [Range(0f, 2f)]
    public float edgeGlow = 1.2f;
    
    [Header("Shadow Settings")]
    public Color shadowColor = new Color(0.1f, 0.1f, 0.3f, 0.5f);
    [Range(0f, 1f)]
    public float shadowIntensity = 0.6f;
    [Range(0.01f, 1f)]
    public float shadowSoftness = 0.15f;
    
    [Header("Debug")]
    public bool showRegionBounds = false;
    public bool regenerateOnStart = true;
    
    private Dictionary<int, RegionData> regions = new Dictionary<int, RegionData>();
    private TilemapRenderer tilemapRenderer;
    private Material instancedMaterial;
    
    private void Start()
    {
        tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        
        if (regenerateOnStart)
        {
            RegenerateAllRegions();
        }
    }
    
    [ContextMenu("Regenerate All Regions")]
    public void RegenerateAllRegions()
    {
        // 清除旧数据
        foreach (var region in regions.Values)
        {
            if (region.distanceField != null)
            {
                Destroy(region.distanceField);
            }
        }
        regions.Clear();
        
        // 识别所有连通区域
        Dictionary<int, List<Vector3Int>> connectedRegions = GetAllConnectedRegions();
        
        // 为每个区域生成距离场并应用效果
        foreach (var kvp in connectedRegions)
        {
            int regionId = kvp.Key;
            List<Vector3Int> tiles = kvp.Value;
            
            RegionData regionData = new RegionData
            {
                tiles = tiles,
                bounds = CalculateRegionBounds(tiles)
            };
            
            // 生成距离场
            regionData.distanceField = GenerateDistanceField(tiles);
            regions.Add(regionId, regionData);
        }
        
        // 应用shader效果
        ApplyShaderEffects();
    }
    
    private Dictionary<int, List<Vector3Int>> GetAllConnectedRegions()
    {
        Dictionary<int, List<Vector3Int>> regions = new Dictionary<int, List<Vector3Int>>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        int regionId = 0;
        
        // 获取Tilemap的边界
        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;
        
        // 遍历所有瓦片
        for (int y = bounds.min.y; y < bounds.max.y; y++)
        {
            for (int x = bounds.min.x; x < bounds.max.x; x++)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                
                // 如果有瓦片且未访问过
                if (tilemap.GetTile(tilePos) != null && !visited.Contains(tilePos))
                {
                    // 找出这个瓦片所在的连通区域
                    List<Vector3Int> region = GetConnectedRegion(tilePos);
                    regions.Add(regionId, region);
                    regionId++;
                    
                    // 标记为已访问
                    foreach (var pos in region)
                    {
                        visited.Add(pos);
                    }
                }
            }
        }
        
        return regions;
    }
    
    private List<Vector3Int> GetConnectedRegion(Vector3Int startPos)
    {
        List<Vector3Int> connectedTiles = new List<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        
        // 确保起始位置有瓦片
        if (tilemap.GetTile(startPos) == null)
            return connectedTiles;
            
        queue.Enqueue(startPos);
        visited.Add(startPos);
        
        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            connectedTiles.Add(current);
            
            // 检查四个方向的相邻瓦片
            Vector3Int[] neighbors = new Vector3Int[]
            {
                current + new Vector3Int(0, 1, 0),  // 上
                current + new Vector3Int(1, 0, 0),  // 右
                current + new Vector3Int(0, -1, 0), // 下
                current + new Vector3Int(-1, 0, 0)  // 左
            };
            
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor) && tilemap.GetTile(neighbor) != null)
                {
                    queue.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }
        }
        
        return connectedTiles;
    }
    
    private List<Vector3Int> GetRegionEdges(List<Vector3Int> region)
    {
        List<Vector3Int> edges = new List<Vector3Int>();
        HashSet<Vector3Int> regionSet = new HashSet<Vector3Int>(region);
        
        foreach (var tilePos in region)
        {
            // 检查四个方向
            Vector3Int[] neighbors = new Vector3Int[]
            {
                tilePos + new Vector3Int(0, 1, 0),  // 上
                tilePos + new Vector3Int(1, 0, 0),  // 右
                tilePos + new Vector3Int(0, -1, 0), // 下
                tilePos + new Vector3Int(-1, 0, 0)  // 左
            };
            
            bool isEdge = false;
            foreach (var neighbor in neighbors)
            {
                // 如果邻居位置没有瓦片，则当前瓦片是边缘
                if (!regionSet.Contains(neighbor))
                {
                    isEdge = true;
                    break;
                }
            }
            
            if (isEdge)
            {
                edges.Add(tilePos);
            }
        }
        
        return edges;
    }
    
    private BoundsInt CalculateRegionBounds(List<Vector3Int> region)
    {
        if (region.Count == 0)
            return new BoundsInt();
            
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (var pos in region)
        {
            minX = Mathf.Min(minX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxX = Mathf.Max(maxX, pos.x);
            maxY = Mathf.Max(maxY, pos.y);
        }
        
        return new BoundsInt(
            minX, minY, 0,
            maxX - minX + 1, maxY - minY + 1, 1
        );
    }
    
    private Texture2D GenerateDistanceField(List<Vector3Int> region)
    {
        // 获取区域边界
        BoundsInt bounds = CalculateRegionBounds(region);
        int width = bounds.size.x;
        int height = bounds.size.y;
        
        // 创建纹理
        Texture2D distanceField = new Texture2D(width, height, TextureFormat.RFloat, false);
        distanceField.filterMode = FilterMode.Bilinear;
        distanceField.wrapMode = TextureWrapMode.Clamp;
        
        // 转换区域瓦片为局部坐标
        HashSet<Vector2Int> localTiles = new HashSet<Vector2Int>();
        foreach (var pos in region)
        {
            int x = pos.x - bounds.x;
            int y = pos.y - bounds.y;
            localTiles.Add(new Vector2Int(x, y));
        }
        
        // 找出边缘瓦片
        List<Vector3Int> edges = GetRegionEdges(region);
        HashSet<Vector2Int> localEdges = new HashSet<Vector2Int>();
        foreach (var pos in edges)
        {
            int x = pos.x - bounds.x;
            int y = pos.y - bounds.y;
            localEdges.Add(new Vector2Int(x, y));
        }
        
        // 计算距离场
        float[,] distances = new float[width, height];
        
        // 初始化距离为无限大
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                distances[x, y] = float.MaxValue;
            }
        }
        
        // 为每个瓦片计算到最近边缘的距离
        foreach (var localPos in localTiles)
        {
            float minDistance = float.MaxValue;
            
            foreach (var edgePos in localEdges)
            {
                float dx = localPos.x - edgePos.x;
                float dy = localPos.y - edgePos.y;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                minDistance = Mathf.Min(minDistance, distance);
            }
            
            distances[localPos.x, localPos.y] = minDistance;
        }
        
        // 找出最大距离用于归一化
        float maxDistance = 0;
        foreach (var localPos in localTiles)
        {
            maxDistance = Mathf.Max(maxDistance, distances[localPos.x, localPos.y]);
        }
        
        // 归一化并设置纹理像素
        Color[] colors = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                
                if (localTiles.Contains(new Vector2Int(x, y)))
                {
                    float normalizedDistance = distances[x, y] / maxDistance;
                    colors[index] = new Color(normalizedDistance, 0, 0, 1);
                }
                else
                {
                    colors[index] = new Color(0, 0, 0, 0);
                }
            }
        }
        
        distanceField.SetPixels(colors);
        distanceField.Apply();
        
        return distanceField;
    }
    
private void ApplyShaderEffects()
{
    if (regions.Count == 0)
        return;
            
    // 检查必要引用
    if (regionShaderMaterial == null)
    {
        Debug.LogError("Region shader material is not assigned!");
        return;
    }
    
    if (tilemap == null)
    {
        Debug.LogError("Tilemap reference is missing!");
        return;
    }
    
    if (tilemapRenderer == null)
    {
        tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null)
        {
            Debug.LogError("No TilemapRenderer found on the tilemap!");
            return;
        }
    }
    
    // 创建材质实例
    if (instancedMaterial == null)
    {
        instancedMaterial = new Material(regionShaderMaterial);
        tilemapRenderer.material = instancedMaterial;
    }
    
    // 设置边缘和阴影参数
    instancedMaterial.SetColor("_EdgeColor", edgeColor);
    instancedMaterial.SetFloat("_EdgeThickness", edgeThickness);
    instancedMaterial.SetFloat("_EdgeGlow", edgeGlow);
    instancedMaterial.SetColor("_ShadowColor", shadowColor);
    instancedMaterial.SetFloat("_ShadowIntensity", shadowIntensity);
    instancedMaterial.SetFloat("_ShadowSoftness", shadowSoftness);
    
    // 如果只有一个区域，直接使用它的距离场
    if (regions.Count == 1)
    {
        foreach (var region in regions.Values)
        {
            instancedMaterial.SetTexture("_DistanceField", region.distanceField);
            
            // 设置UV变换以匹配区域边界
            Vector4 distanceFieldST = new Vector4(
                1.0f / region.bounds.size.x,
                1.0f / region.bounds.size.y,
                (float)region.bounds.x / tilemap.cellBounds.size.x,
                (float)region.bounds.y / tilemap.cellBounds.size.y
            );
            instancedMaterial.SetVector("_DistanceField_ST", distanceFieldST);
            break;
        }
    }
    else
    {
        // 多区域支持
        Debug.Log($"应用 {regions.Count} 个区域的效果");
        
        // 获取tilemap的边界
        tilemap.CompressBounds();
        BoundsInt tilemapBounds = tilemap.cellBounds;
        
        // 创建合并的距离场纹理
        Texture2D combinedDistanceField = new Texture2D(
            tilemapBounds.size.x, 
            tilemapBounds.size.y, 
            TextureFormat.RFloat, 
            false
        );
        combinedDistanceField.filterMode = FilterMode.Bilinear;
        combinedDistanceField.wrapMode = TextureWrapMode.Clamp;
        
        // 初始化为0（无距离场数据）
        Color[] colors = new Color[tilemapBounds.size.x * tilemapBounds.size.y];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = new Color(0, 0, 0, 0);
        }
        
        // 将每个区域的距离场数据填充到合并纹理中
        foreach (var kvp in regions)
        {
            RegionData region = kvp.Value;
            
            // 获取区域在tilemap中的位置
            int regionOffsetX = region.bounds.x - tilemapBounds.x;
            int regionOffsetY = region.bounds.y - tilemapBounds.y;
            
            // 读取区域距离场数据
            Color[] regionColors = region.distanceField.GetPixels();
            
            // 填充到合并纹理中
            for (int y = 0; y < region.bounds.size.y; y++)
            {
                for (int x = 0; x < region.bounds.size.x; x++)
                {
                    int sourceIndex = y * region.bounds.size.x + x;
                    int targetX = regionOffsetX + x;
                    int targetY = regionOffsetY + y;
                    
                    // 确保在合并纹理边界内
                    if (targetX >= 0 && targetX < tilemapBounds.size.x && 
                        targetY >= 0 && targetY < tilemapBounds.size.y)
                    {
                        int targetIndex = targetY * tilemapBounds.size.x + targetX;
                        
                        // 只复制有效的距离场数据（非零）
                        if (regionColors[sourceIndex].r > 0)
                        {
                            colors[targetIndex] = regionColors[sourceIndex];
                        }
                    }
                }
            }
        }
        
        // 应用纹理数据
        combinedDistanceField.SetPixels(colors);
        combinedDistanceField.Apply();
        
        // 设置到材质
        instancedMaterial.SetTexture("_DistanceField", combinedDistanceField);
        
        // 设置UV变换以匹配tilemap边界
        Vector4 distanceFieldST = new Vector4(
            1.0f / tilemapBounds.size.x,
            1.0f / tilemapBounds.size.y,
            0, 0  // 不需要偏移，因为已经是全局坐标
        );
        instancedMaterial.SetVector("_DistanceField_ST", distanceFieldST);
        
        // 设置tilemap边界信息，用于shader中的坐标转换
        instancedMaterial.SetVector("_TilemapBounds", new Vector4(
            tilemapBounds.x,
            tilemapBounds.y,
            tilemapBounds.size.x,
            tilemapBounds.size.y
        ));
    }
}
    
    private void OnDrawGizmos()
    {
        if (!showRegionBounds || regions == null)
            return;
            
        Gizmos.color = Color.green;
        
        foreach (var region in regions.Values)
        {
            Vector3 min = tilemap.CellToWorld(new Vector3Int(
                region.bounds.x,
                region.bounds.y,
                0
            ));
            
            Vector3 max = tilemap.CellToWorld(new Vector3Int(
                region.bounds.x + region.bounds.size.x,
                region.bounds.y + region.bounds.size.y,
                0
            ));
            
            // 绘制边界框
            Gizmos.DrawWireCube(
                (min + max) * 0.5f,
                new Vector3(max.x - min.x, max.y - min.y, 0.1f)
            );
        }
    }
}

[System.Serializable]
public class RegionData
{
    public List<Vector3Int> tiles = new List<Vector3Int>();
    public BoundsInt bounds;
    public Texture2D distanceField;
}
