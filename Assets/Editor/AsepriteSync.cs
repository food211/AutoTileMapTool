using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

// 用于序列化的配置类
[System.Serializable]
public class AsepriteSyncConfig
{
    public string externalFilePath = "";
    public string unityTargetPath = "Assets/AsepriteAssets";
    public string asepritePath = "";
    public bool autoSync = false;
    public float checkInterval = 2f;
    // 添加一个可序列化的文件列表
    public List<TrackedFileEntry> trackedFilesList = new List<TrackedFileEntry>();

    // 非序列化字段，用于运行时
    [System.NonSerialized]
    public Dictionary<string, AsepriteFileInfo> trackedFiles = new Dictionary<string, AsepriteFileInfo>();
}

// 用于序列化的文件条目类
[System.Serializable]
public class TrackedFileEntry
{
    public string filePath;
    public long lastModified;
    public List<string> exportedLayers = new List<string>();
    
    public TrackedFileEntry(string path, long timestamp)
    {
        filePath = path;
        lastModified = timestamp;
    }
    
    public TrackedFileEntry(AsepriteFileInfo info)
    {
        filePath = info.filePath;
        lastModified = info.lastModified;
        exportedLayers = new List<string>(info.exportedLayers);
    }
}

// 用于跟踪单个Aseprite文件的信息
[System.Serializable]
public class AsepriteFileInfo
{
    public string filePath;
    public long lastModified;
    public List<string> exportedLayers = new List<string>();
    
    public AsepriteFileInfo(string path, long timestamp)
    {
        filePath = path;
        lastModified = timestamp;
    }
}

public class AsepriteAutoSync : EditorWindow
{
  private AsepriteSyncConfig config = new AsepriteSyncConfig();
  private double lastCheckTime = 0;
  private const string CONFIG_PATH = "Assets/Editor/AsepriteSync.json";
  
  // 静态属性，让AsepriteImportProcessor可以访问
  public static string TargetPath { get; private set; } = "Assets/AsepriteAssets";

  [MenuItem("Tools/Aseprite Auto Sync")]
  public static void ShowWindow()
  {
      GetWindow<AsepriteAutoSync>("Aseprite Auto Sync");
  }
  
  void OnEnable()
  {
      LoadConfig();
      // 更新静态属性
      TargetPath = config.unityTargetPath;
  }

  void OnDisable()
  {
      SaveConfig();
  }

  void LoadConfig()
{
    // 尝试从JSON文件加载配置
    if (File.Exists(CONFIG_PATH))
    {
        try
        {
            string json = File.ReadAllText(CONFIG_PATH);
            JsonUtility.FromJsonOverwrite(json, config);
            
            // 将序列化列表转换为字典
            config.trackedFiles.Clear();
            foreach (var entry in config.trackedFilesList)
            {
                if (!string.IsNullOrEmpty(entry.filePath))
                {
                    config.trackedFiles[entry.filePath] = new AsepriteFileInfo(entry.filePath, entry.lastModified)
                    {
                        exportedLayers = new List<string>(entry.exportedLayers)
                    };
                }
            }
            
            UnityEngine.Debug.Log("已加载Aseprite同步配置");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"加载配置失败: {e.Message}");
            // 使用默认配置
            config = new AsepriteSyncConfig();
        }
    }
    else
    {
        // 如果配置文件不存在，尝试从EditorPrefs加载旧的配置
        config.externalFilePath = EditorPrefs.GetString("AsepriteSync_ExternalPath", "");
        config.unityTargetPath = EditorPrefs.GetString("AsepriteSync_TargetPath", "Assets/AsepriteAssets");
        config.asepritePath = EditorPrefs.GetString("AsepriteSync_AsepritePath", "");
        config.autoSync = EditorPrefs.GetBool("AsepriteSync_AutoSync", false);
        config.checkInterval = EditorPrefs.GetFloat("AsepriteSync_CheckInterval", 2f);
    }
}

    void SaveConfig()
    {
        try
        {
            // 将字典转换为序列化列表
            config.trackedFilesList.Clear();
            foreach (var pair in config.trackedFiles)
            {
                config.trackedFilesList.Add(new TrackedFileEntry(pair.Value));
            }

            // 确保目录存在
            string directory = Path.GetDirectoryName(CONFIG_PATH);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 将配置保存到JSON文件
            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(CONFIG_PATH, json);

            UnityEngine.Debug.Log("已保存Aseprite同步配置");

            // 同时保存到EditorPrefs作为备份
            EditorPrefs.SetString("AsepriteSync_ExternalPath", config.externalFilePath);
            EditorPrefs.SetString("AsepriteSync_TargetPath", config.unityTargetPath);
            EditorPrefs.SetString("AsepriteSync_AsepritePath", config.asepritePath);
            EditorPrefs.SetBool("AsepriteSync_AutoSync", config.autoSync);
            EditorPrefs.SetFloat("AsepriteSync_CheckInterval", config.checkInterval);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"保存配置失败: {e.Message}");
        }
    }

private Vector2 trackedFilesScrollPosition;
private bool showTrackedFiles = false;

    void OnGUI()
    {
        GUILayout.Label("Aseprite文件自动同步工具", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // Aseprite可执行文件路径
        EditorGUILayout.LabelField("Aseprite可执行文件路径:");
        string oldAsepritePath = config.asepritePath;
        config.asepritePath = EditorGUILayout.TextField(config.asepritePath);
        if (oldAsepritePath != config.asepritePath)
        {
            SaveConfig();
        }

        if (GUILayout.Button("选择Aseprite.exe"))
        {
            string path = EditorUtility.OpenFilePanel("选择Aseprite可执行文件", "", "exe");
            if (!string.IsNullOrEmpty(path))
            {
                config.asepritePath = path;
                SaveConfig();
            }
        }

        EditorGUILayout.Space();

        // 当前选择的Aseprite文件
        EditorGUILayout.LabelField("当前Aseprite文件:");
        string oldExternalPath = config.externalFilePath;
        config.externalFilePath = EditorGUILayout.TextField(config.externalFilePath);
        if (oldExternalPath != config.externalFilePath)
        {
            SaveConfig();
        }

        if (GUILayout.Button("选择Aseprite文件"))
        {
            string path = EditorUtility.OpenFilePanel("选择Aseprite文件", "", "ase,aseprite");
            if (!string.IsNullOrEmpty(path))
            {
                config.externalFilePath = path;
                SaveConfig();
            }
        }

        // 添加当前文件到跟踪列表
        if (!string.IsNullOrEmpty(config.externalFilePath) && File.Exists(config.externalFilePath))
        {
            if (GUILayout.Button("添加当前文件到跟踪列表"))
            {
                if (!config.trackedFiles.ContainsKey(config.externalFilePath))
                {
                    FileInfo fileInfo = new FileInfo(config.externalFilePath);
                    config.trackedFiles[config.externalFilePath] = new AsepriteFileInfo(config.externalFilePath, fileInfo.LastWriteTime.ToBinary());
                    SaveConfig();
                    UnityEngine.Debug.Log($"已添加文件到跟踪列表: {Path.GetFileName(config.externalFilePath)}");
                }
                else
                {
                    UnityEngine.Debug.Log("该文件已在跟踪列表中");
                }
            }
        }

        EditorGUILayout.Space();

        // Unity目标路径
        EditorGUILayout.LabelField("Unity目标路径:");
        string oldPath = config.unityTargetPath;
        config.unityTargetPath = EditorGUILayout.TextField(config.unityTargetPath);

        // 如果路径改变，更新静态属性和配置
        if (oldPath != config.unityTargetPath)
        {
            TargetPath = config.unityTargetPath;
            SaveConfig();
        }

        EditorGUILayout.Space();

        // 检查间隔
        float oldInterval = config.checkInterval;
        config.checkInterval = EditorGUILayout.FloatField("检查间隔(秒):", config.checkInterval);
        if (oldInterval != config.checkInterval)
        {
            SaveConfig();
        }

        EditorGUILayout.Space();

        // 自动同步开关
        bool oldAutoSync = config.autoSync;
        config.autoSync = EditorGUILayout.Toggle("自动同步", config.autoSync);
        if (oldAutoSync != config.autoSync)
        {
            SaveConfig();
        }

        EditorGUILayout.Space();

        // 手动同步按钮
        if (GUILayout.Button("同步当前文件"))
        {
            SyncCurrentAsepriteFile();
        }

        if (GUILayout.Button("同步所有跟踪文件"))
        {
            SyncAllTrackedFiles();
        }

        EditorGUILayout.Space();

        // 跟踪文件列表折叠栏
        showTrackedFiles = EditorGUILayout.Foldout(showTrackedFiles, $"跟踪文件列表 ({config.trackedFiles.Count})");
        if (showTrackedFiles)
        {
            EditorGUILayout.Space();

            // 文件列表
            trackedFilesScrollPosition = EditorGUILayout.BeginScrollView(trackedFilesScrollPosition, GUILayout.Height(200));

            List<string> filesToRemove = new List<string>();

            foreach (var pair in config.trackedFiles)
            {
                string filePath = pair.Key;
                AsepriteFileInfo fileInfo = pair.Value;

                EditorGUILayout.BeginHorizontal("box");

                // 文件名和路径
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(Path.GetFileName(filePath), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(filePath, EditorStyles.miniLabel);

                // 最后修改时间
                System.DateTime lastModified = System.DateTime.FromBinary(fileInfo.lastModified);
                EditorGUILayout.LabelField($"最后修改: {lastModified.ToString("yyyy-MM-dd HH:mm:ss")}", EditorStyles.miniLabel);

                // 导出的图层数量
                EditorGUILayout.LabelField($"导出图层: {fileInfo.exportedLayers.Count}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                // 操作按钮
                EditorGUILayout.BeginVertical(GUILayout.Width(100));
                if (GUILayout.Button("同步"))
                {
                    ProcessAsepriteFile(filePath);
                    AssetDatabase.Refresh();
                }

                if (GUILayout.Button("删除"))
                {
                    filesToRemove.Add(filePath);
                }

                if (GUILayout.Button("设为当前"))
                {
                    config.externalFilePath = filePath;
                    SaveConfig();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            // 删除标记的文件
            foreach (string fileToRemove in filesToRemove)
            {
                config.trackedFiles.Remove(fileToRemove);
                UnityEngine.Debug.Log($"已从跟踪列表中移除: {Path.GetFileName(fileToRemove)}");
            }

            if (filesToRemove.Count > 0)
            {
                SaveConfig();
            }

            EditorGUILayout.EndScrollView();
        }

        // 状态显示
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("状态:", config.autoSync ? "自动同步中..." : "已停止");
        if (lastCheckTime > 0)
        {
            EditorGUILayout.LabelField("上次检查:", System.DateTime.Now.AddSeconds(-(EditorApplication.timeSinceStartup - lastCheckTime)).ToString("HH:mm:ss"));
        }

        // 帮助信息
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("工具会监控.ase和.aseprite文件，自动调用Aseprite CLI按图层导出PNG文件到Unity", MessageType.Info);
    }

    void SyncCurrentAsepriteFile()
    {
        if (string.IsNullOrEmpty(config.externalFilePath) || !File.Exists(config.externalFilePath))
        {
            UnityEngine.Debug.LogWarning("当前Aseprite文件不存在: " + config.externalFilePath);
            return;
        }

        string filePath = config.externalFilePath;
        FileInfo fileInfo = new FileInfo(filePath);
        long currentTimestamp = fileInfo.LastWriteTime.ToBinary();

        // 更新或添加跟踪信息
        if (!config.trackedFiles.ContainsKey(filePath))
        {
            config.trackedFiles[filePath] = new AsepriteFileInfo(filePath, currentTimestamp);
        }
        else
        {
            config.trackedFiles[filePath].lastModified = currentTimestamp;
        }

        ProcessAsepriteFile(filePath);
        SaveConfig();
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("当前Aseprite文件已同步到Unity");
    }

    // 添加同步所有跟踪文件的方法
    void SyncAllTrackedFiles()
    {
        bool hasChanges = false;
        List<string> invalidFiles = new List<string>();

        foreach (var pair in config.trackedFiles)
        {
            string filePath = pair.Key;

            if (File.Exists(filePath))
            {
                FileInfo fileInfo = new FileInfo(filePath);
                long currentTimestamp = fileInfo.LastWriteTime.ToBinary();

                // 更新时间戳
                config.trackedFiles[filePath].lastModified = currentTimestamp;

                ProcessAsepriteFile(filePath);
                hasChanges = true;
            }
            else
            {
                invalidFiles.Add(filePath);
            }
        }

        // 删除无效文件
        foreach (string invalidFile in invalidFiles)
        {
            config.trackedFiles.Remove(invalidFile);
            UnityEngine.Debug.Log($"已从跟踪列表中移除不存在的文件: {invalidFile}");
        }

        if (hasChanges || invalidFiles.Count > 0)
        {
            SaveConfig();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("所有跟踪的Aseprite文件已同步到Unity");
        }
    }

    void Update()
    {
        if (config.autoSync && !string.IsNullOrEmpty(config.asepritePath))
        {
            if (EditorApplication.timeSinceStartup - lastCheckTime > config.checkInterval)
            {
                CheckAndSyncAllTrackedFiles();
                lastCheckTime = EditorApplication.timeSinceStartup;
            }
        }
    }

void CheckAndSyncAllTrackedFiles()
{
    if (!File.Exists(config.asepritePath))
    {
        UnityEngine.Debug.LogWarning("Aseprite可执行文件不存在: " + config.asepritePath);
        return;
    }

    bool hasChanges = false;
    List<string> invalidFiles = new List<string>();
    
    foreach (var pair in config.trackedFiles)
    {
        string filePath = pair.Key;
        
        if (File.Exists(filePath))
        {
            FileInfo fileInfo = new FileInfo(filePath);
            long currentTimestamp = fileInfo.LastWriteTime.ToBinary();
            
            // 检查文件是否已修改
            if (pair.Value.lastModified != currentTimestamp)
            {
                // 更新时间戳
                config.trackedFiles[filePath].lastModified = currentTimestamp;
                
                ProcessAsepriteFile(filePath);
                hasChanges = true;
            }
        }
        else
        {
            invalidFiles.Add(filePath);
        }
    }
    
    // 删除无效文件
    foreach (string invalidFile in invalidFiles)
    {
        config.trackedFiles.Remove(invalidFile);
        UnityEngine.Debug.Log($"已从跟踪列表中移除不存在的文件: {invalidFile}");
    }
    
    if (hasChanges || invalidFiles.Count > 0)
    {
        SaveConfig();
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("已同步修改的Aseprite文件到Unity");
    }
}

  void ProcessAsepriteFile(string sourcePath)
{
    try
    {
        // 确保目标文件夹存在
        if (!Directory.Exists(config.unityTargetPath))
        {
            Directory.CreateDirectory(config.unityTargetPath);
            AssetDatabase.Refresh();
        }

        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        
        // 创建临时输出文件夹
        string tempOutputDir = Path.Combine(Path.GetTempPath(), "AsepriteExport");
        if (!Directory.Exists(tempOutputDir))
        {
            Directory.CreateDirectory(tempOutputDir);
        }

        // 调用Aseprite CLI导出图层
        List<string> exportedLayers = ExportAsepriteLayersToSprites(sourcePath, tempOutputDir, fileName);
        
        // 更新跟踪信息
        if (config.trackedFiles.ContainsKey(sourcePath))
        {
            config.trackedFiles[sourcePath].exportedLayers = exportedLayers;
        }
        
        // 将导出的PNG文件复制到Unity项目
        CopyExportedSpritesToUnity(tempOutputDir, "", fileName);
        
        UnityEngine.Debug.Log($"已处理Aseprite文件: {fileName}");
    }
    catch (System.Exception e)
    {
        UnityEngine.Debug.LogError($"处理Aseprite文件失败: {sourcePath}, 错误: {e.Message}");
    }
}

  List<string> ExportAsepriteLayersToSprites(string aseFilePath, string outputDir, string baseName)
  {
      List<string> exportedLayers = new List<string>();
      
      try
      {
          // 使用Aseprite CLI命令导出所有可见图层
          // --split-layers 会将每个图层导出为独立文件
          // --filename-format 指定文件名格式，{layer} 会被替换为图层名
          string arguments = $"-b \"{aseFilePath}\" --split-layers --ignore-empty --trim --save-as \"{Path.Combine(outputDir, baseName + "_{layer}.png")}\"";
          
          ProcessStartInfo startInfo = new ProcessStartInfo
          {
              FileName = config.asepritePath,
              Arguments = arguments,
              UseShellExecute = false,
              RedirectStandardOutput = true,
              RedirectStandardError = true,
              CreateNoWindow = true
          };

          using (Process process = Process.Start(startInfo))
          {
              process.WaitForExit();
              
              if (process.ExitCode != 0)
              {
                  string error = process.StandardError.ReadToEnd();
                  UnityEngine.Debug.LogError($"Aseprite导出失败: {error}");
              }
              else
              {
                  string output = process.StandardOutput.ReadToEnd();
                  if (!string.IsNullOrEmpty(output))
                  {
                      UnityEngine.Debug.Log($"Aseprite导出成功: {output}");
                  }
                  
                  // 获取导出的所有图层文件
                  if (Directory.Exists(outputDir))
                  {
                      string[] pngFiles = Directory.GetFiles(outputDir, "*.png");
                      foreach (string file in pngFiles)
                      {
                          string layerName = Path.GetFileNameWithoutExtension(file).Replace(baseName + "_", "");
                          exportedLayers.Add(layerName);
                      }
                  }
              }
          }
      }
      catch (System.Exception e)
      {
          UnityEngine.Debug.LogError($"调用Aseprite CLI失败: {e.Message}");
      }
      
      return exportedLayers;
  }

  void CopyExportedSpritesToUnity(string tempOutputDir, string relativePath, string baseName)
  {
      try
      {
          // 获取临时文件夹中的所有PNG文件
          if (!Directory.Exists(tempOutputDir))
          {
              return;
          }

          string[] pngFiles = Directory.GetFiles(tempOutputDir, "*.png");
          
          foreach (string pngFile in pngFiles)
          {
              // 计算Unity项目中的目标路径
              string targetDir = string.IsNullOrEmpty(relativePath) ? 
                  config.unityTargetPath : 
                  Path.Combine(config.unityTargetPath, relativePath);
              
              if (!Directory.Exists(targetDir))
              {
                  Directory.CreateDirectory(targetDir);
              }

              string fileName = Path.GetFileName(pngFile);
              string targetPath = Path.Combine(targetDir, fileName);
              
              bool isNewFile = !File.Exists(targetPath);
              
              // 复制文件到Unity项目
              File.Copy(pngFile, targetPath, true);
              
              // 如果是更新现有文件，确保保留引用
              if (!isNewFile)
              {
                  // 导入资源但保留原有设置
                  AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
              }
              
              UnityEngine.Debug.Log($"已{(isNewFile ? "创建" : "更新")}图层精灵: {fileName}");
          }

          // 清理临时文件
          Directory.Delete(tempOutputDir, true);
      }
      catch (System.Exception e)
      {
          UnityEngine.Debug.LogError($"复制导出的精灵失败: {e.Message}");
      }
  }
}

// Aseprite导入后处理器 - 专门处理单个精灵
public class AsepriteImportProcessor : AssetPostprocessor
{
  void OnPreprocessTexture()
  {
      // 使用静态属性获取目标路径
      string targetPath = AsepriteAutoSync.TargetPath;
      
      // 只处理目标文件夹下的PNG文件
      if (assetPath.EndsWith(".png") && assetPath.StartsWith(targetPath))
      {
          TextureImporter textureImporter = (TextureImporter)assetImporter;

          // 保留原始设置以避免破坏引用
          SpriteImportMode originalMode = textureImporter.spriteImportMode;
          float originalPPU = textureImporter.spritePixelsPerUnit;
          Vector2 originalPivot = textureImporter.spritePivot;

          // 设置为单个Sprite模式（不是Multiple）
          textureImporter.textureType = TextureImporterType.Sprite;
          
          // 如果之前不是精灵或者是新资源，设置为Single模式
          if (textureImporter.spriteImportMode == SpriteImportMode.None)
          {
              textureImporter.spriteImportMode = SpriteImportMode.Single;
              textureImporter.spritePixelsPerUnit = 16;
              textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
          }
          else
          {
              // 保留原始的模式和设置
              textureImporter.spriteImportMode = originalMode;
              textureImporter.spritePixelsPerUnit = originalPPU;
              textureImporter.spritePivot = originalPivot;
          }

          // 像素艺术设置
          textureImporter.filterMode = FilterMode.Point;
          textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
          textureImporter.alphaIsTransparency = true;

          // 禁用mipmap
          textureImporter.mipmapEnabled = false;

          UnityEngine.Debug.Log($"已配置Aseprite图层精灵导入设置: {assetPath}");
      }
  }
  
  // 添加后处理方法，确保不会破坏现有引用
  void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
  {
      string targetPath = AsepriteAutoSync.TargetPath;
      
      if (assetPath.EndsWith(".png") && assetPath.StartsWith(targetPath))
      {
          // 这里我们可以进一步处理精灵，如果需要的话
          // 例如，可以保留或修改精灵的名称等
          
          UnityEngine.Debug.Log($"已完成精灵处理: {assetPath}, 精灵数量: {sprites.Length}");
      }
  }
}