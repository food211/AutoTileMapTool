using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps;
using System.IO;

public class TilePaletteGenerator : EditorWindow
{
  private Texture2D sourceTexture;
  private string tilesOutputPath = "Assets/GeneratedTiles";
  private string paletteOutputPath = "Assets/GeneratedPalettes";
  private string paletteFileName = "MyTilePalette";
  private float transparencyThreshold = 0.1f;
private static bool? overwriteChoice = null;
  
  // 添加去重精度选项
    private enum DedupPrecision
    {
        Low,    // 低精度，使用简化的哈希算法
        Medium, // 中等精度，使用直方图方法
        High    // 高精度，精确到每个像素
    }
  
  private DedupPrecision dedupPrecision = DedupPrecision.Medium;
  
  private Dictionary<int, string> pixelHashCache = new Dictionary<int, string>();

  // EditorPrefs keys for saving settings
  private const string PREF_TILES_PATH = "TilePaletteGenerator_TilesPath";
  private const string PREF_PALETTE_PATH = "TilePaletteGenerator_PalettePath";
  private const string PREF_PALETTE_FILENAME = "TilePaletteGenerator_PaletteFileName";
  private const string PREF_TRANSPARENCY_THRESHOLD = "TilePaletteGenerator_TransparencyThreshold";
  private const string PREF_DEDUP_PRECISION = "TilePaletteGenerator_DedupPrecision";

  [MenuItem("Tools/Tilemap/TilePalatteGenerator")]
  public static void ShowWindow()
  {
      GetWindow<TilePaletteGenerator>("Tile Palette Generator");
  }

  private void OnEnable()
  {
      LoadSettings();
  }

  private void OnDisable()
  {
      SaveSettings();
  }

  private void LoadSettings()
  {
      tilesOutputPath = EditorPrefs.GetString(PREF_TILES_PATH, "Assets/GeneratedTiles");
      paletteOutputPath = EditorPrefs.GetString(PREF_PALETTE_PATH, "Assets/GeneratedPalettes");
      paletteFileName = EditorPrefs.GetString(PREF_PALETTE_FILENAME, "MyTilePalette");
      transparencyThreshold = EditorPrefs.GetFloat(PREF_TRANSPARENCY_THRESHOLD, 0.1f);
      dedupPrecision = (DedupPrecision)EditorPrefs.GetInt(PREF_DEDUP_PRECISION, (int)DedupPrecision.Medium);
  }

  private void SaveSettings()
  {
      EditorPrefs.SetString(PREF_TILES_PATH, tilesOutputPath);
      EditorPrefs.SetString(PREF_PALETTE_PATH, paletteOutputPath);
      EditorPrefs.SetString(PREF_PALETTE_FILENAME, paletteFileName);
      EditorPrefs.SetFloat(PREF_TRANSPARENCY_THRESHOLD, transparencyThreshold);
      EditorPrefs.SetInt(PREF_DEDUP_PRECISION, (int)dedupPrecision);
  }

  private void OnGUI()
  {
      GUILayout.Label("Tile Palette Generator", EditorStyles.boldLabel);
      
      sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture (Sliced)", sourceTexture, typeof(Texture2D), false);
      EditorGUILayout.HelpBox("Please use Sprite Editor to slice the texture first!", MessageType.Info);
      
      EditorGUILayout.Space();
      
      transparencyThreshold = EditorGUILayout.Slider("Transparency Threshold", transparencyThreshold, 0f, 1f);
      EditorGUILayout.HelpBox("Sprites with average alpha below this threshold will be ignored", MessageType.Info);
      
      // 添加去重精度选项
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Deduplication Settings:", EditorStyles.boldLabel);
      dedupPrecision = (DedupPrecision)EditorGUILayout.EnumPopup("Deduplication Precision", dedupPrecision);
      
      string helpText = "";
      switch(dedupPrecision)
      {
          case DedupPrecision.Low:
              helpText = "Fast but less accurate. Good for simple tiles with distinct features.";
              break;
          case DedupPrecision.Medium:
              helpText = "Balanced performance and accuracy. Recommended for most cases.";
              break;
          case DedupPrecision.High:
              helpText = "Pixel-perfect comparison. Use for complex tiles with subtle differences.";
              break;
      }
      EditorGUILayout.HelpBox(helpText, MessageType.Info);
      
      EditorGUILayout.Space();
      
      // Palette文件名设置
      EditorGUILayout.LabelField("Palette Settings:", EditorStyles.boldLabel);
      paletteFileName = EditorGUILayout.TextField("Palette File Name", paletteFileName);
      EditorGUILayout.HelpBox("Enter the name for the generated palette (without .prefab extension)", MessageType.Info);
      
      EditorGUILayout.Space();
      
      // Output paths
      EditorGUILayout.LabelField("Output Paths:", EditorStyles.boldLabel);
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField("Tiles Path:", GUILayout.Width(80));
      tilesOutputPath = EditorGUILayout.TextField(tilesOutputPath);
      if (GUILayout.Button("Browse", GUILayout.Width(60)))
      {
          string selectedPath = EditorUtility.OpenFolderPanel("Select Tiles Output Folder", tilesOutputPath, "");
          if (!string.IsNullOrEmpty(selectedPath))
          {
              if (selectedPath.StartsWith(Application.dataPath))
              {
                  tilesOutputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
              }
              else
              {
                  Debug.LogWarning("Selected folder must be inside the Assets folder!");
              }
          }
      }
      EditorGUILayout.EndHorizontal();
      
      // Palette output path
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField("Palette Path:", GUILayout.Width(80));
      paletteOutputPath = EditorGUILayout.TextField(paletteOutputPath);
      if (GUILayout.Button("Browse", GUILayout.Width(60)))
      {
          string selectedPath = EditorUtility.OpenFolderPanel("Select Palette Output Folder", paletteOutputPath, "");
          if (!string.IsNullOrEmpty(selectedPath))
          {
              if (selectedPath.StartsWith(Application.dataPath))
              {
                  paletteOutputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
              }
              else
              {
                  Debug.LogWarning("Selected folder must be inside the Assets folder!");
              }
          }
      }
      EditorGUILayout.EndHorizontal();
      
      EditorGUILayout.Space();
      
      // 显示Sprite信息
      if (sourceTexture != null)
      {
          var sprites = GetSpritesFromTexture(sourceTexture);
          EditorGUILayout.HelpBox($"Found {sprites.Length} sprites in the texture", MessageType.Info);
      }
      
      // 验证输入
      bool canGenerate = sourceTexture != null && !string.IsNullOrEmpty(paletteFileName.Trim());
      
      if (!canGenerate && sourceTexture == null)
      {
          EditorGUILayout.HelpBox("Please select a sliced source texture", MessageType.Warning);
      }
      
      if (!canGenerate && string.IsNullOrEmpty(paletteFileName.Trim()))
      {
          EditorGUILayout.HelpBox("Please enter a palette file name", MessageType.Warning);
      }
      
      EditorGUI.BeginDisabledGroup(!canGenerate);
      if (GUILayout.Button("Generate Tile Palette", GUILayout.Height(30)))
      {
          SaveSettings();
          GenerateTilePalette();
      }
      EditorGUI.EndDisabledGroup();
  }

    // 修改GenerateTilePalette方法，确保正确处理瓦片位置
    private void GenerateTilePalette()
{
    // 重置覆盖选择
    overwriteChoice = null;

    // 验证文件名
    string sanitizedFileName = SanitizeFileName(paletteFileName.Trim());
    if (string.IsNullOrEmpty(sanitizedFileName))
    {
        Debug.LogError("Invalid palette file name!");
        return;
    }

    // 确保输出目录存在
    EnsureDirectoryExists(tilesOutputPath);
    EnsureDirectoryExists(paletteOutputPath);

    // 获取所有Sprites
    Sprite[] sprites = GetSpritesFromTexture(sourceTexture);
    if (sprites == null || sprites.Length == 0)
    {
        Debug.LogError("No sprites found in the texture! Please slice the texture first using Sprite Editor.");
        return;
    }

    Debug.Log($"Found {sprites.Length} sprites in texture");

    // 处理Sprites并去重
    var spriteData = ProcessSprites(sprites);
    if (spriteData.Count == 0)
    {
        Debug.LogError("No valid sprites found after processing");
        return;
    }

    // 获取原始纹理的尺寸
    int textureWidth = sourceTexture.width;
    int textureHeight = sourceTexture.height;

    // 假设所有sprite尺寸相同，取第一个sprite的尺寸
    int tileWidth = (int)sprites[0].rect.width;
    int tileHeight = (int)sprites[0].rect.height;

    // 计算网格尺寸
    int gridWidth = textureWidth / tileWidth;
    int gridHeight = textureHeight / tileHeight;

    Debug.Log($"Creating tile palette with grid size: {gridWidth}x{gridHeight}");
    Debug.Log($"Sprite pivot: {sprites[0].pivot}, normalized: ({sprites[0].pivot.x / tileWidth}, {sprites[0].pivot.y / tileHeight})");

    // 创建去重的Tiles
    Dictionary<string, Tile> uniqueTiles = new Dictionary<string, Tile>();
    Dictionary<Vector2Int, Tile> tilePositions = new Dictionary<Vector2Int, Tile>();

    int processedTiles = 0;
    int skippedTiles = 0;
    int createdTileAssets = 0; // 添加计数器跟踪实际创建的文件数量

    // 首先创建所有唯一的Tile
    foreach (var data in spriteData)
    {
        if (data.isTransparent)
        {
            skippedTiles++;
            continue;
        }

        string hash = data.hash;

        if (!uniqueTiles.ContainsKey(hash))
        {
            // 创建新的Tile
            Tile newTile = CreateTileFromSprite(data.sprite, hash, ref createdTileAssets);
            if (newTile != null)
            {
                uniqueTiles[hash] = newTile;
                Debug.Log($"Created unique tile for hash {hash.Substring(0, 8)}");
            }
            else
            {
                Debug.LogError($"Failed to create tile for hash {hash.Substring(0, 8)}");
                continue;
            }
        }
    }

    // 计算每个sprite的位置并填充tilePositions字典
    foreach (var data in spriteData)
    {
        if (data.isTransparent)
            continue;

        // 计算sprite在网格中的位置，考虑pivot
        // 使用rect.x和rect.y获取sprite在纹理中的位置
        int gridX = Mathf.FloorToInt(data.sprite.rect.x / tileWidth);
        int gridY = Mathf.FloorToInt(data.sprite.rect.y / tileHeight);

        // 将Y坐标从底部向上转换为从上到下
        gridY = gridHeight - 1 - gridY;

        // 使用Vector2Int作为字典键，存储位置和对应的瓦片
        Vector2Int position = new Vector2Int(gridX, gridY);

        // 确保不会覆盖已有的瓦片位置
        if (!tilePositions.ContainsKey(position))
        {
            tilePositions[position] = uniqueTiles[data.hash];
            processedTiles++;
            
            // 记录pivot信息用于调试
            Vector2 normalizedPivot = new Vector2(
                data.pivot.x / tileWidth,
                data.pivot.y / tileHeight
            );
            Debug.Log($"Tile at ({gridX}, {gridY}) has pivot: {data.pivot}, normalized: {normalizedPivot}");
        }
        else
        {
            Debug.LogWarning($"Position ({gridX}, {gridY}) already has a tile assigned. This might indicate a problem with sprite slicing.");
        }
    }

    Debug.Log($"Processed {processedTiles} tiles, skipped {skippedTiles} transparent tiles");

    // 创建Tile Palette
    CreateTilePalette(tilePositions, gridWidth, gridHeight, sanitizedFileName);

    Debug.Log($"Generated {uniqueTiles.Count} unique tiles in memory, created {createdTileAssets} tile asset files");
    // 验证实际创建的文件数量
    string[] tileFiles = Directory.GetFiles(tilesOutputPath, "tile_*.asset");
    Debug.Log($"Actual tile files found in directory: {tileFiles.Length}");
}

    // 修改CreateTilePalette方法，修复Y坐标翻转问题
    private void CreateTilePalette(Dictionary<Vector2Int, Tile> tilePositions, int gridWidth, int gridHeight, string fileName)
    {
        // 创建Palette GameObject
        GameObject paletteGO = new GameObject(fileName);
        Grid grid = paletteGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(1, 1, 0);

        // 创建Tilemap
        GameObject tilemapGO = new GameObject("Layer1");
        tilemapGO.transform.SetParent(paletteGO.transform);

        Tilemap tilemap = tilemapGO.AddComponent<Tilemap>();
        TilemapRenderer tilemapRenderer = tilemapGO.AddComponent<TilemapRenderer>();

        // 设置材质为默认Sprite材质
        tilemapRenderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        // 填充tilemap - 使用字典中的位置信息
        int tilesPlaced = 0;

        // 不再基于非透明瓦片的分布计算偏移量
        // 而是直接使用原始网格尺寸，确保所有图像使用相同的映射逻辑

        Debug.Log($"Creating palette with original grid dimensions: {gridWidth}x{gridHeight}");

        // 放置瓦片，直接使用原始坐标
        foreach (var kvp in tilePositions)
        {
            Vector2Int originalPos = kvp.Key;
            Tile tile = kvp.Value;

            if (tile != null)
            {
                // 直接使用原始位置，不再应用额外的偏移
                // 只需要处理Y坐标的翻转，因为Unity的Tilemap坐标系是从左下角开始的
                Vector3Int position = new Vector3Int(
                    originalPos.x,
                    (gridHeight - 1 - originalPos.y),
                    0);

                tilemap.SetTile(position, tile);
                tilesPlaced++;

                // 记录放置位置用于调试
                Debug.Log($"Placed tile at position: {position} (original grid position: {originalPos})");
            }
        }

        Debug.Log($"Placed {tilesPlaced} tiles in palette");

        // 构建完整的文件路径
        string prefabPath = $"{paletteOutputPath}/{fileName}.prefab";

        // 检查文件是否已存在
        if (File.Exists(prefabPath))
        {
            // 询问用户是否覆盖
            bool overwrite = EditorUtility.DisplayDialog(
                "Palette Already Exists",
                $"The palette at path {prefabPath} already exists. Do you want to overwrite it?",
                "Overwrite",
                "Create New");

            if (overwrite)
            {
                // 用户选择覆盖，删除现有文件
                AssetDatabase.DeleteAsset(prefabPath);
            }
            else
            {
                // 用户选择不覆盖，创建新文件名
                int counter = 1;
                while (File.Exists(prefabPath))
                {
                    prefabPath = $"{paletteOutputPath}/{fileName}_{counter}.prefab";
                    counter++;
                }
            }
        }

        // 保存为Prefab
        PrefabUtility.SaveAsPrefabAsset(paletteGO, prefabPath);
        DestroyImmediate(paletteGO);

        AssetDatabase.SaveAssets();
        Debug.Log($"Tile palette saved to: {prefabPath}");
    }

    private Tile CreateTileFromSprite(Sprite sprite, string hash, ref int createdTileAssets)
  {
      Tile tile = ScriptableObject.CreateInstance<Tile>();
      tile.sprite = sprite;

      // 使用哈希的前16个字符作为文件名，这应该足够唯一
      string shortHash = hash.Substring(0, Mathf.Min(16, hash.Length));
      string path = $"{tilesOutputPath}/tile_{shortHash}.asset";
      bool willCreateNewFile = false;

      // 处理文件名冲突
      if (File.Exists(path))
      {
          // 如果用户已经做出了选择，则应用该选择
          if (overwriteChoice.HasValue)
          {
              if (overwriteChoice.Value)
              {
                  // 用户选择覆盖，删除现有文件
                  AssetDatabase.DeleteAsset(path);
                  willCreateNewFile = true;
              }
              else
              {
                  // 用户选择不覆盖，创建新文件名
                  int counter = 1;
                  while (File.Exists(path))
                  {
                      path = $"{tilesOutputPath}/tile_{shortHash}_{counter}.asset";
                      counter++;
                  }
                  willCreateNewFile = true;
              }
          }
          else
          {
              // 用户尚未做出选择，询问一次
              bool overwrite = EditorUtility.DisplayDialog(
                  "File Already Exists",
                  $"Some tile assets already exist. Do you want to overwrite all existing files?",
                  "Overwrite All",
                  "Create New For All");
              
              // 保存用户的选择
              overwriteChoice = overwrite;
              
              if (overwrite)
              {
                  // 用户选择覆盖，删除现有文件
                  AssetDatabase.DeleteAsset(path);
                  willCreateNewFile = true;
              }
              else
              {
                  // 用户选择不覆盖，创建新文件名
                  int counter = 1;
                  while (File.Exists(path))
                  {
                      path = $"{tilesOutputPath}/tile_{shortHash}_{counter}.asset";
                      counter++;
                  }
                  willCreateNewFile = true;
              }
          }
      }
      else
      {
          willCreateNewFile = true;
      }

      // 创建资产文件
      AssetDatabase.CreateAsset(tile, path);
      
      // 确认文件是否真的创建了
      if (willCreateNewFile && File.Exists(path))
      {
          createdTileAssets++;
          Debug.Log($"Created tile asset: {path} for hash {shortHash}");
      }
      
      return tile;
  }

private class SpriteData
{
    public Sprite sprite;
    public bool isTransparent;
    public string hash;
    public int gridX, gridY;
    public Vector2 pivot;  // 添加pivot信息
}

  private class GridInfo
  {
      public int width;
      public int height;
      public float cellWidth;
      public float cellHeight;
  }

  private Sprite[] GetSpritesFromTexture(Texture2D texture)
  {
      string path = AssetDatabase.GetAssetPath(texture);
      return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
  }

private List<SpriteData> ProcessSprites(Sprite[] sprites)
{
    List<SpriteData> spriteDataList = new List<SpriteData>();
    
    // 清除缓存，为新的处理做准备
    pixelHashCache.Clear();

    foreach (var sprite in sprites)
    {
        // 获取sprite的像素数据
        var pixels = GetSpritePixels(sprite);
        
        // 检查透明度
        bool isTransparent = IsTransparent(pixels);
        
        SpriteData spriteData = new SpriteData
        {
            sprite = sprite,
            gridX = 0,  // 这些值在GenerateTilePalette方法中会重新计算
            gridY = 0,
            isTransparent = isTransparent,
            pivot = sprite.pivot  // 保存pivot信息
        };

        if (!isTransparent)
        {
            // 根据选择的精度计算像素哈希用于去重
            spriteData.hash = CalculatePixelHash(pixels);
        }
        else
        {
            spriteData.hash = "transparent";
        }

        spriteDataList.Add(spriteData);
    }

    return spriteDataList;
}

  private Color[] GetSpritePixels(Sprite sprite)
  {
      // 确保纹理可读
      MakeTextureReadable(sprite.texture);

      Rect rect = sprite.rect;
      return sprite.texture.GetPixels(
          (int)rect.x, (int)rect.y,
          (int)rect.width, (int)rect.height);
  }

  private bool IsTransparent(Color[] pixels)
  {
      float totalAlpha = 0f;
      foreach (Color pixel in pixels)
          totalAlpha += pixel.a;
      return (totalAlpha / pixels.Length) < transparencyThreshold;
  }

  private string CalculatePixelHash(Color[] pixels)
  {
      // 根据选择的精度使用不同的哈希算法
      switch (dedupPrecision)
      {
          case DedupPrecision.Low:
              return CalculateLowPrecisionHash(pixels);
          case DedupPrecision.High:
              return CalculateHighPrecisionHash(pixels);
          case DedupPrecision.Medium:
          default:
              return CalculateMediumPrecisionHash(pixels);
      }
  }
  
  private string CalculateLowPrecisionHash(Color[] pixels)
  {
      // 使用像素数组的哈希码作为缓存键
      int pixelsHashCode = GetPixelsHashCode(pixels);
      
      // 检查缓存中是否已存在这个哈希
      if (pixelHashCache.ContainsKey(pixelsHashCode))
      {
          return pixelHashCache[pixelsHashCode];
      }
      
      // 低精度哈希 - 只计算颜色分布的简单统计
      int width = (int)Mathf.Sqrt(pixels.Length);
      int height = pixels.Length / width;
      
      // 将图像分成4x4的区域，计算每个区域的平均颜色
      int regionSize = Mathf.Max(1, width / 4);
      int regionsX = Mathf.Max(1, width / regionSize);
      int regionsY = Mathf.Max(1, height / regionSize);
      
      System.Text.StringBuilder hashBuilder = new System.Text.StringBuilder();
      
      // 添加一些基本信息到哈希中
      hashBuilder.Append(width.ToString("X4"));
      hashBuilder.Append(height.ToString("X4"));
      hashBuilder.Append(pixels.Length.ToString("X8"));
      
      for (int ry = 0; ry < regionsY; ry++)
      {
          for (int rx = 0; rx < regionsX; rx++)
          {
              float r = 0, g = 0, b = 0, a = 0;
              int count = 0;
              
              // 计算区域内所有像素的平均颜色
              int startX = rx * regionSize;
              int startY = ry * regionSize;
              int endX = Mathf.Min(startX + regionSize, width);
              int endY = Mathf.Min(startY + regionSize, height);
              
              for (int y = startY; y < endY; y++)
              {
                  for (int x = startX; x < endX; x++)
                  {
                      if (y * width + x < pixels.Length) // 防止越界
                      {
                          Color pixel = pixels[y * width + x];
                          r += pixel.r;
                          g += pixel.g;
                          b += pixel.b;
                          a += pixel.a;
                          count++;
                      }
                  }
              }
              
              if (count > 0)
              {
                  r /= count;
                  g /= count;
                  b /= count;
                  a /= count;
              }
              
              // 将平均颜色添加到哈希中
              Color32 avgColor = new Color(r, g, b, a);
              hashBuilder.Append(avgColor.r.ToString("X2"));
              hashBuilder.Append(avgColor.g.ToString("X2"));
              hashBuilder.Append(avgColor.b.ToString("X2"));
              hashBuilder.Append(avgColor.a.ToString("X2"));
          }
      }
      
      string hash = hashBuilder.ToString();
      pixelHashCache[pixelsHashCode] = hash;
      return hash;
  }
  
  private string CalculateMediumPrecisionHash(Color[] pixels)
  {
      // 使用像素数组的哈希码作为缓存键
      int pixelsHashCode = GetPixelsHashCode(pixels);
      
      // 检查缓存中是否已存在这个哈希
      if (pixelHashCache.ContainsKey(pixelsHashCode))
      {
          return pixelHashCache[pixelsHashCode];
      }
      
      // 中等精度哈希 - 使用直方图方法 (原来的ComputeExactHash方法)
      string hash = ComputeExactHash(pixels);
      
      // 将结果存入缓存
      pixelHashCache[pixelsHashCode] = hash;
      return hash;
  }
  
  private string CalculateHighPrecisionHash(Color[] pixels)
  {
      // 高精度哈希 - 精确到每个像素
      System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();
      
      // 创建一个内存流来存储像素数据
      using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
      {
          using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
          {
              // 写入像素数量
              writer.Write(pixels.Length);
              
              // 写入每个像素的颜色值
              foreach (Color pixel in pixels)
              {
                  Color32 c = pixel;
                  writer.Write(c.r);
                  writer.Write(c.g);
                  writer.Write(c.b);
                  writer.Write(c.a);
              }
              
              // 计算哈希
              byte[] hashBytes = sha256.ComputeHash(stream.ToArray());
              
              // 将哈希转换为十六进制字符串
              System.Text.StringBuilder hashBuilder = new System.Text.StringBuilder();
              foreach (byte b in hashBytes)
              {
                  hashBuilder.Append(b.ToString("X2"));
              }
              
              return hashBuilder.ToString();
          }
      }
  }
  
  private int GetPixelsHashCode(Color[] pixels)
  {
      // 快速计算像素数组的哈希码，用于缓存查找
      unchecked
      {
          int hash = 17;
          // 只使用少量采样点计算哈希码
          int step = Mathf.Max(1, pixels.Length / 10);
          for (int i = 0; i < pixels.Length; i += step)
          {
              Color32 c = pixels[i];
              hash = hash * 23 + c.r.GetHashCode();
              hash = hash * 23 + c.g.GetHashCode();
              hash = hash * 23 + c.b.GetHashCode();
              hash = hash * 23 + c.a.GetHashCode();
          }
          return hash;
      }
  }
  
  private string ComputeExactHash(Color[] pixels)
  {
      int width = (int)Mathf.Sqrt(pixels.Length);
      int height = pixels.Length / width;
      
      // 使用直方图方法 - 这是图像处理中常用的特征提取方法
      // 对于像素艺术，颜色直方图特别有效，因为像素艺术通常使用有限的颜色
      Dictionary<Color32, int> colorHistogram = new Dictionary<Color32, int>();
      
      // 添加一个随机种子，以确保即使相同的像素数据也能生成不同的哈希
      System.Random random = new System.Random(pixels.Length);
      int randomSeed = random.Next();
      
      foreach (Color pixel in pixels)
      {
          // 转换为Color32以避免浮点精度问题
          Color32 c32 = pixel;
          
          if (colorHistogram.ContainsKey(c32))
          {
              colorHistogram[c32]++;
          }
          else
          {
              colorHistogram[c32] = 1;
          }
      }
      
      // 构建哈希字符串
      System.Text.StringBuilder hashBuilder = new System.Text.StringBuilder();
      
      // 添加随机种子
      hashBuilder.Append(randomSeed.ToString("X8"));
      
      // 添加尺寸信息
      hashBuilder.Append(width.ToString("X4"));
      hashBuilder.Append(height.ToString("X4"));
      
      // 添加颜色数量
      hashBuilder.Append(colorHistogram.Count.ToString("X4"));
      
      // 添加主要颜色信息（按出现频率排序）
      foreach (var colorEntry in colorHistogram.OrderByDescending(kv => kv.Value))
      {
          Color32 c = colorEntry.Key;
          hashBuilder.Append(c.r.ToString("X2"));
          hashBuilder.Append(c.g.ToString("X2"));
          hashBuilder.Append(c.b.ToString("X2"));
          hashBuilder.Append(c.a.ToString("X2"));
          hashBuilder.Append(colorEntry.Value.ToString("X4"));
      }
      
      // 添加边缘检测信息 - 这对于区分形状相似但细节不同的瓦片很有用
      int[] edgePattern = DetectEdges(pixels, width, height);
      foreach (int edgeValue in edgePattern)
      {
          hashBuilder.Append(edgeValue.ToString("X2"));
      }
      
      // 使用SHA256对哈希字符串进行哈希处理，确保唯一性
      using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
      {
          byte[] hashBytes = System.Text.Encoding.UTF8.GetBytes(hashBuilder.ToString());
          byte[] hashResult = sha256.ComputeHash(hashBytes);
          
          // 将哈希结果转换为十六进制字符串
          System.Text.StringBuilder finalHashBuilder = new System.Text.StringBuilder();
          foreach (byte b in hashResult)
          {
              finalHashBuilder.Append(b.ToString("X2"));
          }
          
          return finalHashBuilder.ToString();
      }
  }

  private int[] DetectEdges(Color[] pixels, int width, int height)
  {
      // 简化的边缘检测 - 检测水平和垂直边缘
      // 返回一个包含边缘特征的数组
      
      // 我们将图像分成4x4的网格，并在每个网格单元中检测边缘
      int gridSize = 4;
      int[] edgePattern = new int[gridSize * gridSize];
      
      int cellWidth = Mathf.Max(1, width / gridSize);
      int cellHeight = Mathf.Max(1, height / gridSize);
      
      for (int gy = 0; gy < gridSize; gy++)
      {
          for (int gx = 0; gx < gridSize; gx++)
          {
              int startX = gx * cellWidth;
              int startY = gy * cellHeight;
              int endX = Mathf.Min(startX + cellWidth, width);
              int endY = Mathf.Min(startY + cellHeight, height);
              
              // 计算这个网格单元中的边缘强度
              int edgeStrength = 0;
              
              for (int y = startY; y < endY; y++)
              {
                  for (int x = startX; x < endX; x++)
                  {
                      // 检查右侧像素
                      if (x + 1 < endX)
                      {
                          Color c1 = pixels[y * width + x];
                          Color c2 = pixels[y * width + x + 1];
                          edgeStrength += CalculateColorDifference(c1, c2);
                      }
                      
                      // 检查下方像素
                      if (y + 1 < endY)
                      {
                          Color c1 = pixels[y * width + x];
                          Color c2 = pixels[(y + 1) * width + x];
                          edgeStrength += CalculateColorDifference(c1, c2);
                      }
                  }
              }
              
              // 归一化边缘强度
              int cellArea = (endX - startX) * (endY - startY);
              if (cellArea > 0)
              {
                  edgeStrength = Mathf.Min(255, edgeStrength / cellArea);
              }
              
              edgePattern[gy * gridSize + gx] = edgeStrength;
          }
      }
      
      return edgePattern;
  }

  private int CalculateColorDifference(Color c1, Color c2)
  {
      // 计算两个颜色之间的差异
      Color32 color1 = c1;
      Color32 color2 = c2;
      
      int rDiff = Mathf.Abs(color1.r - color2.r);
      int gDiff = Mathf.Abs(color1.g - color2.g);
      int bDiff = Mathf.Abs(color1.b - color2.b);
      int aDiff = Mathf.Abs(color1.a - color2.a);
      
      // 如果alpha差异很大，则认为这是一个强边缘
      if (aDiff > 128) return 255;
      
      // 否则，使用RGB差异的总和
      return (rDiff + gDiff + bDiff) / 3;
  }


  private void MakeTextureReadable(Texture2D texture)
  {
      string path = AssetDatabase.GetAssetPath(texture);
      TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
      if (importer != null && !importer.isReadable)
      {
          importer.isReadable = true;
          importer.SaveAndReimport();
      }
  }

  private string SanitizeFileName(string fileName)
  {
      if (string.IsNullOrEmpty(fileName))
          return "";

      // 移除或替换不合法的文件名字符
      char[] invalidChars = Path.GetInvalidFileNameChars();
      foreach (char c in invalidChars)
      {
          fileName = fileName.Replace(c, '_');
      }
      
      // 移除多余的空格和点
      fileName = fileName.Trim().Trim('.');
      
      return fileName;
  }

  private void EnsureDirectoryExists(string path)
  {
      if (!AssetDatabase.IsValidFolder(path))
      {
          string[] parts = path.Split('/');
          string currentPath = parts[0];
          for (int i = 1; i < parts.Length; i++)
          {
              string newPath = currentPath + "/" + parts[i];
              if (!AssetDatabase.IsValidFolder(newPath))
                  AssetDatabase.CreateFolder(currentPath, parts[i]);
              currentPath = newPath;
          }
      }
  }
}