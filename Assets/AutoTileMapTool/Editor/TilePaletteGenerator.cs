#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps;
using System.IO;

namespace TilemapTools
{
    public class TilePaletteGenerator : EditorWindow
    {
        private Texture2D sourceTexture;
        private string tilesOutputPath = "Assets/GeneratedTiles";
        private string paletteOutputPath = "Assets/GeneratedPalettes";
        private string paletteFileName = "MyTilePalette";
        private float transparencyThreshold = 0.1f;
        private static bool? overwriteChoice = null;

        private enum DedupPrecision { Low, Medium, High }
        private DedupPrecision dedupPrecision = DedupPrecision.Medium;
        private Dictionary<int, string> pixelHashCache = new Dictionary<int, string>();

        private const string PREF_TILES_PATH = "TilePaletteGenerator_TilesPath";
        private const string PREF_PALETTE_PATH = "TilePaletteGenerator_PalettePath";
        private const string PREF_PALETTE_FILENAME = "TilePaletteGenerator_PaletteFileName";
        private const string PREF_TRANSPARENCY_THRESHOLD = "TilePaletteGenerator_TransparencyThreshold";
        private const string PREF_DEDUP_PRECISION = "TilePaletteGenerator_DedupPrecision";

        // 添加本地化文本字典
        private static Dictionary<string, Dictionary<string, string>> localizedTexts;
        
        // 当前语言
        private static string currentLanguage;

        // 初始化本地化系统
        private static void InitializeLocalization()
        {
            // 获取Unity编辑器的当前语言
            currentLanguage = EditorPrefs.GetString("Locale", "en");

            // 如果语言不是我们支持的，默认使用英语
            if (currentLanguage != "en" && currentLanguage != "zh-CN" && currentLanguage != "ja")
            {
                currentLanguage = "en";
            }

            // 初始化本地化文本字典
            localizedTexts = new Dictionary<string, Dictionary<string, string>>();

            // 英语文本
            var enTexts = new Dictionary<string, string>
            {
                {"title", "Tile Palette Generator"},
                {"sourceTexture", "Source Texture (Sliced)"},
                {"sliceHint", "Please use Sprite Editor to slice the texture first!"},
                {"transparencyThreshold", "Transparency Threshold"},
                {"transparencyHint", "Sprites with average alpha below this threshold will be ignored"},
                {"dedupSettings", "Deduplication Settings:"},
                {"dedupPrecision", "Deduplication Precision"},
                {"dedupLow", "Fast but less accurate. Good for simple tiles with distinct features."},
                {"dedupMedium", "Balanced performance and accuracy. Recommended for most cases."},
                {"dedupHigh", "Pixel-perfect comparison. Use for complex tiles with subtle differences."},
                {"paletteSettings", "Palette Settings:"},
                {"paletteFileName", "Palette File Name"},
                {"paletteFileNameHint", "Enter the name for the generated palette (without .prefab extension)"},
                {"outputPaths", "Output Paths:"},
                {"tilesPath", "Tiles Path:"},
                {"palettePath", "Palette Path:"},
                {"browse", "Browse"},
                {"foundSprites", "Found {0} sprites in the texture"},
                {"selectTexture", "Please select a sliced source texture"},
                {"enterFileName", "Please enter a palette file name"},
                {"generateButton", "Generate Tile Palette"},
                {"paletteExists", "The palette '{0}' already exists."},
                {"updateInPlace", "Update In-Place"},
                {"cancel", "Cancel"},
                {"createNewCopy", "Create New Copy"},
                {"paletteGenerated", "Tile palette '{0}' has been created successfully!\n\nWould you like to proceed to the next step (Create Rules)?"},
                {"yesNextStep", "Yes, Next Step"},
                {"stayHere", "Stay Here"},
                // 添加更多需要的文本...
            };
            localizedTexts["en"] = enTexts;

            // 中文文本
            var zhTexts = new Dictionary<string, string>
            {
                {"title", "瓦片调色板生成器"},
                {"sourceTexture", "源纹理（已切片）"},
                {"sliceHint", "请先使用精灵编辑器切片纹理！"},
                {"transparencyThreshold", "透明度阈值"},
                {"transparencyHint", "平均透明度低于此阈值的精灵将被忽略"},
                {"dedupSettings", "去重设置:"},
                {"dedupPrecision", "去重精度"},
                {"dedupLow", "速度快但精度低。适用于特征明显的简单瓦片。"},
                {"dedupMedium", "性能和精度平衡。推荐用于大多数情况。"},
                {"dedupHigh", "像素级比较。用于具有细微差别的复杂瓦片。"},
                {"paletteSettings", "调色板设置:"},
                {"paletteFileName", "调色板文件名"},
                {"paletteFileNameHint", "输入生成的调色板的名称（不含.prefab扩展名）"},
                {"outputPaths", "输出路径:"},
                {"tilesPath", "瓦片路径:"},
                {"palettePath", "调色板路径:"},
                {"browse", "浏览"},
                {"foundSprites", "在纹理中找到 {0} 个精灵"},
                {"selectTexture", "请选择一个已切片的源纹理"},
                {"enterFileName", "请输入调色板文件名"},
                {"generateButton", "生成瓦片调色板"},
                {"paletteExists", "调色板 '{0}' 已存在。"},
                {"updateInPlace", "原地更新"},
                {"cancel", "取消"},
                {"createNewCopy", "创建新副本"},
                {"paletteGenerated", "瓦片调色板 '{0}' 已成功创建！\n\n您想继续下一步（创建规则）吗？"},
                {"yesNextStep", "是的，下一步"},
                {"stayHere", "留在这里"},
                // 添加更多需要的文本...
            };
            localizedTexts["zh-CN"] = zhTexts;
        }

        private static string GetLocalizedText(string key, params object[] args)
        {
            // 确保本地化系统已初始化
            if (localizedTexts == null)
            {
                InitializeLocalization();
            }
            
            // 检查当前语言是否有该文本
            if (localizedTexts.ContainsKey(currentLanguage) && localizedTexts[currentLanguage].ContainsKey(key))
            {
                string text = localizedTexts[currentLanguage][key];
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
            GetWindow<TilePaletteGenerator>("Tile Palette Generator");
        }

        private void OnEnable()
        {
            LoadSettings();
            // 确保本地化系统已初始化
            if (localizedTexts == null)
            {
                InitializeLocalization();
            }
        }
        private void OnDisable() { SaveSettings(); }

        private void LoadSettings()
        {
            tilesOutputPath = EditorPrefs.GetString(PREF_TILES_PATH, "Assets/Tilemaps/Tiles");
            paletteOutputPath = EditorPrefs.GetString(PREF_PALETTE_PATH, "Assets/Tilemaps/Palettes");
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
            // 使用本地化文本
            GUILayout.Label(GetLocalizedText("title"), EditorStyles.boldLabel);

            // 源纹理选择
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField(GetLocalizedText("sourceTexture"), sourceTexture, typeof(Texture2D), false);
            EditorGUILayout.HelpBox(GetLocalizedText("sliceHint"), MessageType.Info);
            EditorGUILayout.Space();

            // 透明度阈值设置
            transparencyThreshold = EditorGUILayout.Slider(GetLocalizedText("transparencyThreshold"), transparencyThreshold, 0f, 1f);
            EditorGUILayout.HelpBox(GetLocalizedText("transparencyHint"), MessageType.Info);
            EditorGUILayout.Space();

            // 去重设置
            EditorGUILayout.LabelField(GetLocalizedText("dedupSettings"), EditorStyles.boldLabel);
            dedupPrecision = (DedupPrecision)EditorGUILayout.EnumPopup(GetLocalizedText("dedupPrecision"), dedupPrecision);

            string helpText = "";
            switch (dedupPrecision)
            {
                case DedupPrecision.Low: helpText = GetLocalizedText("dedupLow"); break;
                case DedupPrecision.Medium: helpText = GetLocalizedText("dedupMedium"); break;
                case DedupPrecision.High: helpText = GetLocalizedText("dedupHigh"); break;
            }
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
            EditorGUILayout.Space();

            // 调色板设置 - 合并文件名和路径选择
            EditorGUILayout.LabelField(GetLocalizedText("paletteSettings"), EditorStyles.boldLabel);

            // Tiles路径选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetLocalizedText("tilesPath"), GUILayout.Width(80));
            tilesOutputPath = EditorGUILayout.TextField(tilesOutputPath);
            if (GUILayout.Button(GetLocalizedText("browse"), GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel(
                    "Select Tiles Output Folder",
                    tilesOutputPath,
                    "");
                if (!string.IsNullOrEmpty(selectedPath) && selectedPath.StartsWith(Application.dataPath))
                    tilesOutputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();

            // 调色板保存位置和文件名 - 合并界面
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetLocalizedText("palettePath"), GUILayout.Width(80));

            // 显示完整路径（包括文件名）
            string fullPalettePath = Path.Combine(paletteOutputPath, $"{paletteFileName}.prefab").Replace("\\", "/");

            // 创建一个布局组，左侧显示路径，右侧是文件名输入框
            EditorGUILayout.BeginVertical();

            // 路径显示（禁用状态）
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(paletteOutputPath);
            EditorGUI.EndDisabledGroup();

            // 文件名输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetLocalizedText("paletteFileName"), GUILayout.Width(150));
            paletteFileName = EditorGUILayout.TextField(paletteFileName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 浏览按钮 - 使用SaveFilePanel
            if (GUILayout.Button(GetLocalizedText("browse"), GUILayout.Width(60), GUILayout.Height(38)))
            {
                // 计算初始路径
                string initialDir = paletteOutputPath;
                if (initialDir.StartsWith("Assets/"))
                    initialDir = Path.Combine(Application.dataPath, initialDir.Substring(7));
                else if (initialDir == "Assets")
                    initialDir = Application.dataPath;

                // 显示保存对话框
                string selectedPath = EditorUtility.SaveFilePanel(
                    "Save Palette As",
                    initialDir,
                    paletteFileName,
                    "prefab");

                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // 确保路径是相对于Assets文件夹的
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        // 提取路径和文件名
                        string relativePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                        paletteFileName = Path.GetFileNameWithoutExtension(relativePath);
                        paletteOutputPath = Path.GetDirectoryName(relativePath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Path",
                            "Please select a location inside your project's Assets folder.", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(GetLocalizedText("paletteFileNameHint"), MessageType.Info);
            EditorGUILayout.Space();

            // 显示找到的精灵数量
            if (sourceTexture != null)
            {
                var sprites = GetSpritesFromTexture(sourceTexture);
                EditorGUILayout.HelpBox(GetLocalizedText("foundSprites", sprites.Length), MessageType.Info);
            }

            // 生成按钮
            bool canGenerate = sourceTexture != null && !string.IsNullOrEmpty(paletteFileName.Trim());
            if (!canGenerate && sourceTexture == null)
                EditorGUILayout.HelpBox(GetLocalizedText("selectTexture"), MessageType.Warning);
            if (!canGenerate && string.IsNullOrEmpty(paletteFileName.Trim()))
                EditorGUILayout.HelpBox(GetLocalizedText("enterFileName"), MessageType.Warning);

            EditorGUI.BeginDisabledGroup(!canGenerate);
            if (GUILayout.Button(GetLocalizedText("generateButton"), GUILayout.Height(30)))
            {
                SaveSettings();
                GenerateTilePalette();
            }
            EditorGUI.EndDisabledGroup();

            // 显示当前选择的完整路径（底部信息）
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output: " + fullPalettePath, EditorStyles.miniLabel);
        }

        private void GenerateTilePalette()
        {
            overwriteChoice = null;
            string sanitizedFileName = SanitizeFileName(paletteFileName.Trim());
            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                Debug.LogError("Invalid palette file name!");
                return;
            }

            EnsureDirectoryExists(tilesOutputPath);
            EnsureDirectoryExists(paletteOutputPath);

            Sprite[] sprites = GetSpritesFromTexture(sourceTexture);
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogError("No sprites found in the texture! Please slice the texture first using Sprite Editor.");
                return;
            }

            // 检查所有精灵是否都是正方形（长宽相等）
            bool allSquare = true;
            bool sameSizes = true;
            int firstWidth = (int)sprites[0].rect.width;
            int firstHeight = (int)sprites[0].rect.height;

            List<string> irregularSprites = new List<string>();

            for (int i = 0; i < sprites.Length; i++)
            {
                int width = (int)sprites[i].rect.width;
                int height = (int)sprites[i].rect.height;

                if (width != height)
                {
                    allSquare = false;
                    irregularSprites.Add($"Sprite {i}: {width}x{height}");
                }

                if (width != firstWidth || height != firstHeight)
                {
                    sameSizes = false;
                    if (!irregularSprites.Contains($"Sprite {i}: {width}x{height}"))
                    {
                        irregularSprites.Add($"Sprite {i}: {width}x{height}");
                    }
                }
            }

            if (!allSquare || !sameSizes)
            {
                string message = "Warning: Some sprites have irregular sizes:\n" + string.Join("\n", irregularSprites);
                message += "\n\nThis tool only works with square sprites of the same size.";

                EditorUtility.DisplayDialog("Irregular Sprite Sizes", message, "OK");
                Debug.LogWarning("Tile palette generation cancelled due to irregular sprite sizes.");
                return;
            }

            var spriteData = ProcessSprites(sprites);
            if (spriteData.Count == 0)
            {
                Debug.LogError("No valid sprites found after processing");
                return;
            }

            int tileWidth = (int)sprites[0].rect.width;
            int tileHeight = (int)sprites[0].rect.height;
            int gridWidth = sourceTexture.width / tileWidth;
            int gridHeight = sourceTexture.height / tileHeight;

            Dictionary<string, Tile> uniqueTiles = new Dictionary<string, Tile>();
            int createdTileAssets = 0;

            foreach (var data in spriteData)
            {
                if (data.isTransparent) continue;
                if (!uniqueTiles.ContainsKey(data.hash))
                {
                    Tile newTile = CreateTileFromSprite(data.sprite, data.hash, ref createdTileAssets);
                    if (newTile != null) uniqueTiles[data.hash] = newTile;
                }
            }

            Dictionary<Vector2Int, Tile> tilePositions = new Dictionary<Vector2Int, Tile>();
            foreach (var data in spriteData)
            {
                if (data.isTransparent) continue;
                int gridX = Mathf.FloorToInt(data.sprite.rect.x / tileWidth);
                int gridY = gridHeight - 1 - Mathf.FloorToInt(data.sprite.rect.y / tileHeight);
                Vector2Int position = new Vector2Int(gridX, gridY);
                if (!tilePositions.ContainsKey(position))
                {
                    tilePositions[position] = uniqueTiles[data.hash];
                }
            }

            // MODIFICATION: Capture the returned path and interact with the Workflow Manager
            string finalPalettePath = CreateTilePalette(tilePositions, gridWidth, gridHeight, sanitizedFileName);

            if (!string.IsNullOrEmpty(finalPalettePath))
            {
                Debug.Log($"Tile palette created successfully: {finalPalettePath}");

                // 通知工作流管理器
                TilemapWorkflowManager.SetLastGeneratedPalette(finalPalettePath);

                // 询问用户是否要进入下一步
                if (EditorUtility.DisplayDialog(
                    GetLocalizedText("title"),
                    GetLocalizedText("paletteGenerated", sanitizedFileName),
                    GetLocalizedText("yesNextStep"),
                    GetLocalizedText("stayHere")))
                {
                    // 设置当前步骤
                    TilemapWorkflowManager.SetCurrentStep(TilemapWorkflowManager.WorkflowStep.CreateRules);
                    
                    // 显示并激活工作流管理器窗口
                    var workflowWindow = EditorWindow.GetWindow<TilemapWorkflowManager>("Tilemap Workflow");
                    workflowWindow.Show();
                    workflowWindow.Focus();
                }
            }
        }

        // MODIFICATION: Changed return type from void to string
        private string CreateTilePalette(Dictionary<Vector2Int, Tile> tilePositions, int gridWidth, int gridHeight, string fileName)
        {
            GameObject paletteGO = new GameObject(fileName);
            Grid grid = paletteGO.AddComponent<Grid>();
            GameObject tilemapGO = new GameObject("Layer1");
            tilemapGO.transform.SetParent(paletteGO.transform);
            Tilemap tilemap = tilemapGO.AddComponent<Tilemap>();
            // 获取第一个非空瓦片以检查其精灵的枢轴点
            Tile firstTile = null;
            foreach (var kvp in tilePositions)
            {
                if (kvp.Value != null)
                {
                    firstTile = kvp.Value;
                    break;
                }
            }
            
            // 如果找到了瓦片，使用其精灵的枢轴点
            if (firstTile != null && firstTile.sprite != null)
            {
                Sprite sprite = firstTile.sprite;
                // 计算归一化的枢轴点（0-1范围）
                Vector2 normalizedPivot = new Vector2(
                    sprite.pivot.x / sprite.rect.width,
                    sprite.pivot.y / sprite.rect.height
                );
                
                // 设置瓦片地图的锚点
                tilemap.tileAnchor = new Vector3(normalizedPivot.x, normalizedPivot.y, 0);
            }
            else
            {
                // 如果没有找到瓦片，使用默认的中心锚点
                tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0);
            }

            tilemapGO.AddComponent<TilemapRenderer>();

            foreach (var kvp in tilePositions)
            {
                Vector3Int position = new Vector3Int(kvp.Key.x, gridHeight - 1 - kvp.Key.y, 0);
                tilemap.SetTile(position, kvp.Value);
            }

            string prefabPath = Path.Combine(paletteOutputPath, $"{fileName}.prefab").Replace("\\", "/");

            // 确保paletteOutputPath不是根文件夹
            if (paletteOutputPath == "Assets" || paletteOutputPath == "/")
            {
                Debug.LogError("Cannot save palette to root folder. Please specify a subfolder.");
                DestroyImmediate(paletteGO);
                return null;
            }

            if (File.Exists(prefabPath))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    GetLocalizedText("title"),
                    GetLocalizedText("paletteExists", prefabPath),
                    GetLocalizedText("updateInPlace"),
                    GetLocalizedText("cancel"),
                    GetLocalizedText("createNewCopy"));

                switch (choice)
                {
                    case 0: // Update In-Place
                            // 加载现有的预制体
                        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        if (existingPrefab != null)
                        {
                            // 创建预制体的实例
                            GameObject existingInstance = PrefabUtility.InstantiatePrefab(existingPrefab) as GameObject;

                            // 删除所有子对象
                            while (existingInstance.transform.childCount > 0)
                            {
                                DestroyImmediate(existingInstance.transform.GetChild(0).gameObject);
                            }

                            // 将新的Tilemap添加为子对象
                            tilemapGO.transform.SetParent(existingInstance.transform, false);

                            // 确保有Grid组件
                            if (!existingInstance.GetComponent<Grid>())
                            {
                                existingInstance.AddComponent<Grid>();
                            }

                            // 应用更改到预制体
                            PrefabUtility.SaveAsPrefabAsset(existingInstance, prefabPath);
                            DestroyImmediate(existingInstance);
                            DestroyImmediate(paletteGO);

                            Debug.Log($"Tile palette updated in-place: {prefabPath}");
                            return prefabPath;
                        }
                        else
                        {
                            // 如果无法加载预制体，则覆盖它
                            AssetDatabase.DeleteAsset(prefabPath);
                        }
                        break;
                    case 1: // Cancel
                        DestroyImmediate(paletteGO);
                        return null;
                    case 2: // Create New
                        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                        break;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(paletteGO, prefabPath);
            DestroyImmediate(paletteGO);
            AssetDatabase.SaveAssets();
            Debug.Log($"Tile palette saved to: {prefabPath}");

            return prefabPath;
        }

        private Tile CreateTileFromSprite(Sprite sprite, string hash, ref int createdTileAssets)
        {
            string shortHash = hash.Substring(0, Mathf.Min(16, hash.Length));
            string path = $"{tilesOutputPath}/tile_{shortHash}.asset";
            bool fileExists = File.Exists(path);

            if (fileExists)
            {
                if (!overwriteChoice.HasValue)
                {
                    overwriteChoice = EditorUtility.DisplayDialog("Assets Exist", "Some tile assets already exist. Update existing tiles?", "Update All", "Skip All");
                }

                if (!overwriteChoice.Value)
                {
                    // User chose to skip, so we load the existing asset instead of creating a new one.
                    return AssetDatabase.LoadAssetAtPath<Tile>(path);
                }

                // 加载现有的瓦片资产
                Tile existingTile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (existingTile != null)
                {
                    // 更新现有瓦片的精灵引用，而不是删除并重新创建
                    existingTile.sprite = sprite;
                    EditorUtility.SetDirty(existingTile);
                    AssetDatabase.SaveAssets();
                    return existingTile;
                }
                else
                {
                    // 如果无法加载现有瓦片，则删除并重新创建
                    AssetDatabase.DeleteAsset(path);
                }
            }

            // 创建新的瓦片资产
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            AssetDatabase.CreateAsset(tile, path);
            createdTileAssets++;
            return tile;
        }

        private class SpriteData { public Sprite sprite; public bool isTransparent; public string hash; public Vector2 pivot; }
        private Sprite[] GetSpritesFromTexture(Texture2D texture) { string path = AssetDatabase.GetAssetPath(texture); return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray(); }
        private List<SpriteData> ProcessSprites(Sprite[] sprites) {  
            List<SpriteData> spriteDataList = new List<SpriteData>();
            pixelHashCache.Clear();
            foreach (var sprite in sprites)
            {
                var pixels = GetSpritePixels(sprite);
                bool isTransparent = IsTransparent(pixels);
                SpriteData spriteData = new SpriteData { sprite = sprite, isTransparent = isTransparent, pivot = sprite.pivot };
                if (!isTransparent) { spriteData.hash = CalculatePixelHash(pixels); }
                else { spriteData.hash = "transparent"; }
                spriteDataList.Add(spriteData);
            }
            return spriteDataList;
        }
        private Color[] GetSpritePixels(Sprite sprite) { 
            MakeTextureReadable(sprite.texture);
            Rect rect = sprite.rect;
            return sprite.texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        }
        private bool IsTransparent(Color[] pixels) { 
            float totalAlpha = 0f;
            foreach (Color pixel in pixels) totalAlpha += pixel.a;
            return (totalAlpha / pixels.Length) < transparencyThreshold;
        }
        private string CalculatePixelHash(Color[] pixels) { 
            switch (dedupPrecision)
            {
                case DedupPrecision.Low: return CalculateLowPrecisionHash(pixels);
                case DedupPrecision.High: return CalculateHighPrecisionHash(pixels);
                case DedupPrecision.Medium: default: return CalculateMediumPrecisionHash(pixels);
            }
        }
        private string CalculateLowPrecisionHash(Color[] pixels) {  
            int pixelsHashCode = GetPixelsHashCode(pixels);
            if (pixelHashCache.ContainsKey(pixelsHashCode)) return pixelHashCache[pixelsHashCode];
            int width = (int)Mathf.Sqrt(pixels.Length);
            int height = pixels.Length / width;
            int regionSize = Mathf.Max(1, width / 4);
            int regionsX = Mathf.Max(1, width / regionSize);
            int regionsY = Mathf.Max(1, height / regionSize);
            System.Text.StringBuilder hashBuilder = new System.Text.StringBuilder();
            hashBuilder.Append(width.ToString("X4"));
            hashBuilder.Append(height.ToString("X4"));
            for (int ry = 0; ry < regionsY; ry++)
            {
                for (int rx = 0; rx < regionsX; rx++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    int count = 0;
                    int startX = rx * regionSize, startY = ry * regionSize;
                    int endX = Mathf.Min(startX + regionSize, width), endY = Mathf.Min(startY + regionSize, height);
                    for (int y = startY; y < endY; y++) for (int x = startX; x < endX; x++)
                    {
                        if (y * width + x < pixels.Length) { Color pixel = pixels[y * width + x]; r += pixel.r; g += pixel.g; b += pixel.b; a += pixel.a; count++; }
                    }
                    if (count > 0) { r /= count; g /= count; b /= count; a /= count; }
                    Color32 avgColor = new Color(r, g, b, a);
                    hashBuilder.Append(avgColor.r.ToString("X2")); hashBuilder.Append(avgColor.g.ToString("X2")); hashBuilder.Append(avgColor.b.ToString("X2")); hashBuilder.Append(avgColor.a.ToString("X2"));
                }
            }
            string hash = hashBuilder.ToString();
            pixelHashCache[pixelsHashCode] = hash;
            return hash;
        }
        private string CalculateMediumPrecisionHash(Color[] pixels) { 
            int pixelsHashCode = GetPixelsHashCode(pixels);
            if (pixelHashCache.ContainsKey(pixelsHashCode)) return pixelHashCache[pixelsHashCode];
            string hash = ComputeExactHash(pixels);
            pixelHashCache[pixelsHashCode] = hash;
            return hash;
        }
        private string CalculateHighPrecisionHash(Color[] pixels) { 
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(pixels.Length);
                foreach (Color pixel in pixels) { Color32 c = pixel; writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
                byte[] hashBytes = sha256.ComputeHash(stream.ToArray());
                return System.BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }
        private int GetPixelsHashCode(Color[] pixels) { 
            unchecked
            {
                int hash = 17;
                int step = Mathf.Max(1, pixels.Length / 10);
                for (int i = 0; i < pixels.Length; i += step) { Color32 c = pixels[i]; hash = hash * 23 + c.r; hash = hash * 23 + c.g; hash = hash * 23 + c.b; hash = hash * 23 + c.a; }
                return hash;
            }
        }
        private string ComputeExactHash(Color[] pixels) {  return CalculateHighPrecisionHash(pixels); } // Simplified to use the most robust hash
        private void MakeTextureReadable(Texture2D texture) { 
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        }
        private string SanitizeFileName(string fileName) {  return string.Join("_", fileName.Split(Path.GetInvalidFileNameChars())); }
        private void EnsureDirectoryExists(string path) {  if (!Directory.Exists(path)) Directory.CreateDirectory(path); AssetDatabase.Refresh(); }
    }
}
#endif