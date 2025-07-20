#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEditor.U2D.Sprites;


namespace TilemapTools
{
    public class TilePaletteGenerator : EditorWindow
    {
        public Texture2D sourceTexture;
        public string tilesOutputPath = "Assets/GeneratedTiles";
        public string paletteOutputPath = "Assets/GeneratedPalettes";
        private string asepriteOutputPath = "Assets/Temp/AsepriteImport"; // 默认Aseprite输出路径
        private const string PREF_ASEPRITE_OUTPUT_PATH = "TilePaletteGenerator_AsepriteOutputPath"; // 保存设置的键

        public string paletteFileName = "MyTilePalette";
        public float transparencyThreshold = 0.1f;
        public static bool preserveGUIDs = false;
        public int sliceSize = 16; // 默认切片大小为16x16


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

        // Aseprite相关字段
        public string asepriteFilePath = "";
        private bool useAseprite = false;
        private string asepriteExePath = "";
        
        private bool includeHiddenLayers = false; // 默认不包含隐藏图层


        private const string PREF_ASEPRITE_EXE_PATH = "TilePaletteGenerator_AsepriteExePath";
        private const string PREF_ASEPRITE_FILE_PATH = "TilePaletteGenerator_AsepriteFilePath";

        // 初始化本地化系统
        private static void InitializeLocalization()
        {
            // 使用语言管理器获取当前语言
            currentLanguage = TilemapLanguageManager.GetCurrentLanguageCode();

            // 初始化本地化文本字典
            localizedTexts = new Dictionary<string, Dictionary<string, string>>();

            // 英语文本
            var enTexts = new Dictionary<string, string>
            {
                {"title", "Tile Palette Generator"},
                {"HowTo","Aseprite files will be automatically sliced upon import"},
                { "sourceTexture", "Source Texture"},
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
                {"startGeneratingPalette", "Starting to generate palette: {0}"},
                {"sourceTextureInfo", "Source texture: {0}"},
                {"tilesOutputPathInfo", "Tiles output path: {0}"},
                {"paletteOutputPathInfo", "Palette output path: {0}"},
                {"sourceTextureEmpty", "Source texture is null, cannot generate palette"},
                {"foundSpritesInTexture", "Found {0} sprites in the texture"},
                {"textureNotSliced", "Texture is not sliced or contains no sprites, cannot generate palette"},
                {"invalidPaletteFileName", "Invalid palette file name!"},
                {"noValidSprites", "No valid sprites after processing"},
                {"paletteUpdated", "Tile palette updated in-place: {0}"},
                {"paletteSaved", "Tile palette saved to: {0}"},
                {"paletteContainsTiles", "Palette {0} contains {1} tiles"},
                {"paletteNoTiles", "Generated palette contains no tiles: {0}"},
                {"paletteNoTilemap", "No Tilemap component found in generated palette: {0}"},
                {"cannotLoadPalette", "Cannot load generated palette: {0}"},
                {"paletteGenerationError", "Error occurred while generating palette: {0}\n{1}"},
                {"assetsExist", "Assets Exist"},
                {"updateExistingTiles", "Some tile assets already exist. Update existing tiles?"},
                {"updateAll", "Update All"},
                {"skipAll", "Skip All"},
                {"cannotSavePalette", "Cannot save palette to root folder. Please specify a subfolder."},
                {"automatedWorkflowNextStep", "Automated workflow: proceeding to next step without user interaction."},
                {"unslicedPixelsWarning", "Warning: Unsliced Pixels Detected"},
                {"unslicedPixelsMessage", "Some areas of the texture are not covered by any sprite. This may result in incomplete tile palette.\n\nDo you want to continue anyway?"},
                {"continueGeneration", "Continue"},
                {"cancelGeneration", "Cancel"},
                {"asepriteSupport", "Aseprite Support"},
                {"asepritePackage", "Unity Aseprite Package:"},
                {"asepriteInstalled", "Installed"},
                {"asepriteNotInstalled", "Not Installed"},
                {"installPackage", "Install Package"},
                {"useExternalCLI", "Use External Aseprite CLI"},
                {"asepriteExecutable", "Aseprite Executable:"},
                {"testCLI", "Test Aseprite CLI Connection"},
                {"asepriteFile", "Aseprite File:"},
                {"noFileSelected", "No file selected"},
                {"importAsepriteFile", "Import Aseprite File"},
                {"importFailed", "Failed to import Aseprite file. Please check the console for errors."},
                // 添加Aseprite相关的本地化文本
                {"error", "Error"},
                {"ok", "OK"},
                {"noAsepriteFile", "No Aseprite file selected."},
                {"executingAsepriteCommand", "Executing Aseprite CLI with arguments"},
                {"asepriteExportSuccess", "Aseprite CLI export successful"},
                {"foundLayerFiles", "Found {0} generated layer files"},
                {"processingLayerFile", "Processing layer file"},
                {"failedToLoadTexture", "Failed to load texture from {0}"},
                {"importSuccess", "Import Successful"},
                {"asepriteImportSuccessWithLayers", "Aseprite file imported successfully. Generated {0} layer files."},
                {"noLayerFilesGenerated", "No layer files were generated."},
                {"layerExportPossibleReasons", "This might mean:\n1. The Aseprite file has only one layer\n2. All layers are hidden and --all-layers is not enabled\n3. There was an issue with the export process"},
                {"asepriteExportFailed", "Aseprite CLI export failed"},
                {"failedToExportFromAseprite", "Failed to export from Aseprite. Error"},
                {"asepriteCliNotConfigured", "Aseprite CLI is not configured. Please set the Aseprite executable path."},
                {"attemptingFallbackExport", "Attempting fallback single export..."},
                {"fallbackExportSuccess", "Fallback single texture export successful"},
                {"asepriteImportSuccessSingleTexture", "Aseprite file imported successfully as a single merged texture (fallback mode)."},
                {"failedToLoadExportedTexture", "Failed to load exported texture."},
                {"fallbackExportFailed", "Fallback export also failed"},
                {"bothExportMethodsFailed", "Both layer export and fallback export failed. Error"},
                {"selectAsepriteExecutable", "Select Aseprite Executable"},
                {"asepriteOutputPath", "Aseprite Output Path"},
                {"selectAsepriteOutputFolder", "Select Aseprite Output Folder"},
                {"includeHiddenLayers", "Include Hidden Layers"},
                {"sliceSize", "Slice Size"},
                {"singleImageProcessing", "Single Image Processing"},
                {"asepriteImportRequired", "You've selected an Aseprite file but haven't imported it yet. Please click 'Import Aseprite File' first."},
                {"noSpritesFound", "No sprites found in the texture! Please slice the texture first using Sprite Editor."},
                {"workflowCancelled", "Sprite '{0}' ({1}) has unsliced pixels, workflow cancelled."},
                {"irregularSizesWarning", "Warning: Some sprites have irregular sizes:"},
                {"squareSizesRequired", "This tool only works with square sprites of the same size."},
                {"irregularSizes", "Irregular Sprite Sizes"},
                {"generationCancelledIrregular", "Tile palette generation cancelled due to irregular sprite sizes."},
                {"paletteCreatedSuccessfully", "Tile palette created successfully: {0}"},
                {"selectAsepriteExeFirst", "Please select the Aseprite executable path first."},
                {"success", "Success"},
                {"asepriteCliSuccess", "Aseprite CLI connection successful!\n\nVersion: {0}"},
                {"asepriteCliFailure", "Failed to connect to Aseprite CLI. Please check the executable path."},
                {"asepriteCliError", "Error executing Aseprite CLI: {0}"},
                {"selectImageFile", "Please select an image file (PNG/JPG)"},
                {"selectTextureFirst", "Please select a texture asset first"},
                {"unslicedPixelsMessageWithFileName", "Some areas of the texture '{0}' ({1}) are not covered by any sprite. This may result in incomplete tile palette.\n\nDo you want to continue anyway?"},

            };
            localizedTexts["en"] = enTexts;

            // 中文文本
            var zhTexts = new Dictionary<string, string>
            {
                {"title", "瓦片调色板生成器"},
                {"HowTo","Aseprite文件在导入后会自动切片"},
                { "sourceTexture", "源纹理"},
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
                {"textureNotSlicedMessage", "纹理 '{0}' 未切片或不包含精灵。您想自动切片吗？"},
                {"autoSlice", "自动切片"},
                {"cancel", "取消"},
                {"createNewCopy", "创建新副本"},
                {"paletteGenerated", "瓦片调色板 '{0}' 已成功创建！\n\n您想继续下一步（创建规则）吗？"},
                {"yesNextStep", "是的，下一步"},
                {"stayHere", "留在这里"},
                {"startGeneratingPalette", "开始生成调色板: {0}"},
                {"sourceTextureInfo", "源纹理: {0}"},
                {"tilesOutputPathInfo", "瓦片输出路径: {0}"},
                {"paletteOutputPathInfo", "调色板输出路径: {0}"},
                {"sourceTextureEmpty", "源纹理为空，无法生成调色板"},
                {"foundSpritesInTexture", "在纹理中找到 {0} 个精灵"},
                {"textureNotSliced", "纹理未切片或未包含精灵，无法生成调色板"},
                {"invalidPaletteFileName", "无效的调色板文件名!"},
                {"noValidSprites", "处理后没有有效的精灵"},
                {"paletteUpdated", "调色板已原地更新: {0}"},
                {"paletteSaved", "调色板已保存到: {0}"},
                {"paletteContainsTiles", "调色板 {0} 中包含 {1} 个瓦片"},
                {"paletteNoTiles", "生成的调色板不包含任何瓦片: {0}"},
                {"paletteNoTilemap", "生成的调色板中没有找到Tilemap组件: {0}"},
                {"cannotLoadPalette", "无法加载生成的调色板: {0}"},
                {"paletteGenerationError", "生成调色板时发生错误: {0}\n{1}"},
                {"assetsExist", "资产已存在"},
                {"updateExistingTiles", "一些瓦片资产已经存在。是否更新现有瓦片？"},
                {"updateAll", "全部更新"},
                {"skipAll", "全部跳过"},
                {"cannotSavePalette", "无法将调色板保存到根文件夹。请指定一个子文件夹。"},
                {"automatedWorkflowNextStep", "自动化工作流：无需用户交互，正在进入下一步。"},
                {"unslicedPixelsWarning", "警告：检测到未切片像素"},
                {"unslicedPixelsMessage", "纹理的某些区域没有被任何精灵覆盖。这可能导致生成的调色板不完整。\n\n是否仍要继续？"},
                {"continueGeneration", "继续"},
                {"cancelGeneration", "取消"},
                {"asepriteSupport", "Aseprite 支持"},
                {"asepritePackage", "Unity Aseprite 包:"},
                {"asepriteInstalled", "已安装"},
                {"asepriteNotInstalled", "未安装"},
                {"installPackage", "安装包"},
                {"useExternalCLI", "使用外部 Aseprite CLI"},
                {"asepriteExecutable", "Aseprite 可执行文件:"},
                {"testCLI", "测试 Aseprite CLI 连接"},
                {"asepriteFile", "Aseprite 文件:"},
                {"noFileSelected", "未选择文件"},
                {"importAsepriteFile", "导入 Aseprite 文件"},
                {"importFailed", "导入 Aseprite 文件失败。请查看控制台获取错误信息。"},
                // 添加Aseprite相关的本地化文本
                {"error", "错误"},
                {"ok", "确定"},
                {"noAsepriteFile", "未选择Aseprite文件。"},
                {"executingAsepriteCommand", "执行Aseprite CLI命令参数"},
                {"asepriteExportSuccess", "Aseprite CLI导出成功"},
                {"foundLayerFiles", "找到 {0} 个生成的图层文件"},
                {"processingLayerFile", "处理图层文件"},
                {"failedToLoadTexture", "无法从 {0} 加载纹理"},
                {"importSuccess", "导入成功"},
                {"asepriteImportSuccessWithLayers", "Aseprite文件导入成功。生成了 {0} 个图层文件。"},
                {"noLayerFilesGenerated", "未生成图层文件。"},
                {"layerExportPossibleReasons", "这可能意味着：\n1. Aseprite文件只有一个图层\n2. 所有图层都是隐藏的，并且未启用--all-layers选项\n3. 导出过程中出现问题"},
                {"asepriteExportFailed", "Aseprite CLI导出失败"},
                {"failedToExportFromAseprite", "从Aseprite导出失败。错误"},
                {"asepriteCliNotConfigured", "Aseprite CLI未配置。请设置Aseprite可执行文件路径。"},
                {"attemptingFallbackExport", "尝试备用单一导出..."},
                {"fallbackExportSuccess", "备用单一纹理导出成功"},
                {"asepriteImportSuccessSingleTexture", "Aseprite文件作为单一合并纹理导入成功（备用模式）。"},
                {"failedToLoadExportedTexture", "无法加载导出的纹理。"},
                {"fallbackExportFailed", "备用导出也失败了"},
                {"bothExportMethodsFailed", "图层导出和备用导出均失败。错误"},
                {"selectAsepriteExecutable", "选择Aseprite可执行文件"},
                {"asepriteOutputPath", "Aseprite输出路径"},
                {"selectAsepriteOutputFolder", "选择Aseprite输出文件夹"},
                {"includeHiddenLayers", "包含隐藏图层"},
                {"sliceSize", "切片大小"},
                {"useExternalAsepriteCLI", "使用外部 Aseprite CLI工具"},
                {"singleImageProcessing", "单图处理"},
                {"asepriteImportRequired", "您已选择Aseprite文件但尚未导入。请先点击\"导入Aseprite文件\"按钮。"},
                {"noSpritesFound", "纹理中未找到任何精灵！请先使用精灵编辑器对纹理进行切片。"},
                {"workflowCancelled", "精灵'{0}'（{1}）有未切片像素，工作流已取消。"},
                {"irregularSizesWarning", "警告：部分精灵尺寸不规则："},
                {"squareSizesRequired", "此工具只能处理相同尺寸的正方形精灵。"},
                {"irregularSizes", "不规则精灵尺寸"},
                {"generationCancelledIrregular", "由于精灵尺寸不规则，瓦片调色板生成已取消。"},
                {"paletteCreatedSuccessfully", "瓦片调色板创建成功：{0}"},
                {"selectAsepriteExeFirst", "请先选择Aseprite可执行文件路径。"},
                {"success", "成功"},
                {"asepriteCliSuccess", "Aseprite CLI连接成功！\n\n版本：{0}"},
                {"asepriteCliFailure", "连接Aseprite CLI失败。请检查可执行文件路径。"},
                {"asepriteCliError", "执行Aseprite CLI时出错：{0}"},
                {"selectImageFile", "请选择一个图片文件（PNG/JPG）"},
                {"selectTextureFirst", "请先选择一个纹理资源"},
                {"unslicedPixelsMessageWithFileName", "纹理 '{0}' ({1}) 的某些区域没有被任何精灵覆盖。这可能导致生成的调色板不完整。\n\n是否仍要继续？"},
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
            // 确保本地化系统已初始化
            if (localizedTexts == null)
            {
                InitializeLocalization();
            }

            // 获取本地化的窗口标题
            string title = GetLocalizedText("title");

            // 打开窗口并设置本地化标题
            var window = GetWindow<TilePaletteGenerator>();
            window.titleContent = new GUIContent(title);
        }

        public static TilePaletteGenerator CreateStaticInstance()
        {
            // 创建实例但不显示窗口
            TilePaletteGenerator instance = ScriptableObject.CreateInstance<TilePaletteGenerator>();

            // 确保本地化系统已初始化
            if (localizedTexts == null)
            {
                InitializeLocalization();
            }

            return instance;
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
            // 加载Aseprite设置
            asepriteExePath = EditorPrefs.GetString(PREF_ASEPRITE_EXE_PATH, "");
            asepriteOutputPath = EditorPrefs.GetString(PREF_ASEPRITE_OUTPUT_PATH, "Assets/Temp/AsepriteImport");
            asepriteFilePath = EditorPrefs.GetString(PREF_ASEPRITE_FILE_PATH, "");
        }

        private void SaveSettings()
        {
            EditorPrefs.SetString(PREF_TILES_PATH, tilesOutputPath);
            EditorPrefs.SetString(PREF_PALETTE_PATH, paletteOutputPath);
            EditorPrefs.SetString(PREF_PALETTE_FILENAME, paletteFileName);
            EditorPrefs.SetFloat(PREF_TRANSPARENCY_THRESHOLD, transparencyThreshold);
            EditorPrefs.SetInt(PREF_DEDUP_PRECISION, (int)dedupPrecision);
            // 保存Aseprite设置
            EditorPrefs.SetString(PREF_ASEPRITE_EXE_PATH, asepriteExePath);
            EditorPrefs.SetString(PREF_ASEPRITE_OUTPUT_PATH, asepriteOutputPath);
            EditorPrefs.SetString(PREF_ASEPRITE_FILE_PATH, asepriteFilePath);
        }
        
        // 获取Aseprite可执行文件的扩展名
        private string GetAsepriteExecutableExtension()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
                return "exe";
            else if (Application.platform == RuntimePlatform.OSXEditor)
                return "app";
            else
                return "";
        }
        private void TestAsepriteCLI()
        {
            if (string.IsNullOrEmpty(asepriteExePath))
            {
                EditorUtility.DisplayDialog(GetLocalizedText("error"), GetLocalizedText("selectAsepriteExeFirst"), GetLocalizedText("ok"));
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = asepriteExePath;
                startInfo.Arguments = "--version";
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.CreateNoWindow = true;

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && output.Contains("Aseprite"))
                    {
                        EditorUtility.DisplayDialog(GetLocalizedText("success"),
                            GetLocalizedText("asepriteCliSuccess", output.Trim()), GetLocalizedText("ok"));
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(GetLocalizedText("error"),
                            GetLocalizedText("asepriteCliFailure"), GetLocalizedText("ok"));
                    }
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog(GetLocalizedText("error"),
                    GetLocalizedText("asepriteCliError", e.Message), GetLocalizedText("ok"));
            }
        }

        private void ImportAsepriteFile()
        {
            if (!useAseprite || string.IsNullOrEmpty(asepriteFilePath))
            {
                EditorUtility.DisplayDialog(GetLocalizedText("error"), GetLocalizedText("noAsepriteFile"), GetLocalizedText("ok"));
                return;
            }

            // 确保输出目录存在
            EnsureDirectoryExists(asepriteOutputPath);

            string outputBaseName = Path.GetFileNameWithoutExtension(asepriteFilePath);

            // 使用CLI导出精灵图
            if (!string.IsNullOrEmpty(asepriteExePath))
            {
                // 将输出路径转换为绝对路径
                string absoluteOutputDir = Path.Combine(Application.dataPath, asepriteOutputPath.Substring(7));
                string outputPattern = Path.Combine(absoluteOutputDir, outputBaseName + "_{layer}.png");

                // 构建完整的命令行参数 - 按图层分别导出
                string arguments = string.Format(
                    "-b \"{0}\" --split-layers --save-as \"{1}\"",
                    asepriteFilePath,
                    outputPattern
                );

                // 如果需要包含隐藏图层，添加 --all-layers 参数
                if (includeHiddenLayers)
                {
                    arguments = string.Format(
                        "-b \"{0}\" --all-layers --split-layers --save-as \"{1}\"",
                        asepriteFilePath,
                        outputPattern
                    );
                }

                Debug.Log(GetLocalizedText("executingAsepriteCommand") + ": " + arguments);

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = asepriteExePath;
                startInfo.Arguments = arguments;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.CreateNoWindow = true;

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Debug.Log(GetLocalizedText("asepriteExportSuccess") + ": " + output);
                        AssetDatabase.Refresh();

                        // 查找所有生成的图层文件
                        string[] generatedFiles = Directory.GetFiles(
                            absoluteOutputDir,
                            outputBaseName + "_*.png")
                            .Select(p => "Assets" + p.Substring(Application.dataPath.Length).Replace('\\', '/'))
                            .ToArray();

                        Debug.Log(GetLocalizedText("foundLayerFiles", generatedFiles.Length) + ": " + string.Join(", ", generatedFiles));

                        if (generatedFiles.Length > 0)
                        {
                            // 处理所有生成的图层文件
                            foreach (string filePath in generatedFiles)
                            {
                                Debug.Log(GetLocalizedText("processingLayerFile") + ": " + filePath);

                                // 为每个导出的图层文件应用切片
                                SliceExportedTexture(filePath);

                                // 加载处理后的纹理
                                Texture2D layerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
                                if (layerTexture != null)
                                {
                                    // 设置最后一个处理的纹理为当前纹理
                                    sourceTexture = layerTexture;
                                }
                                else
                                {
                                    Debug.LogError(GetLocalizedText("failedToLoadTexture", filePath));
                                }
                            }

                            EditorUtility.DisplayDialog(GetLocalizedText("importSuccess"),
                                GetLocalizedText("asepriteImportSuccessWithLayers", generatedFiles.Length), GetLocalizedText("ok"));
                        }
                        else
                        {
                            Debug.LogWarning(GetLocalizedText("noLayerFilesGenerated"));
                            Debug.LogWarning(GetLocalizedText("layerExportPossibleReasons"));

                            // 尝试导出单一文件作为备选方案
                            TryFallbackSingleExport(absoluteOutputDir, outputBaseName);
                        }
                    }
                    else
                    {
                        Debug.LogError(GetLocalizedText("asepriteExportFailed") + ": " + error);
                        EditorUtility.DisplayDialog(GetLocalizedText("importFailed"),
                            GetLocalizedText("failedToExportFromAseprite") + ": " + error, GetLocalizedText("ok"));
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog(GetLocalizedText("importFailed"),
                    GetLocalizedText("asepriteCliNotConfigured"), GetLocalizedText("ok"));
            }
        }
        private void TryFallbackSingleExport(string absoluteOutputDir, string outputBaseName)
        {
            Debug.Log(GetLocalizedText("attemptingFallbackExport"));

            string singleOutputPath = Path.Combine(absoluteOutputDir, outputBaseName + ".png");
            string singleArguments = string.Format(
                "-b \"{0}\" --save-as \"{1}\"",
                asepriteFilePath,
                singleOutputPath
            );

            if (includeHiddenLayers)
            {
                singleArguments = string.Format(
                    "-b \"{0}\" --all-layers --save-as \"{1}\"",
                    asepriteFilePath,
                    singleOutputPath
                );
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = asepriteExePath;
            startInfo.Arguments = singleArguments;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            using (Process singleProcess = Process.Start(startInfo))
            {
                string singleOutput = singleProcess.StandardOutput.ReadToEnd();
                string singleError = singleProcess.StandardError.ReadToEnd();
                singleProcess.WaitForExit();

                if (singleProcess.ExitCode == 0)
                {
                    AssetDatabase.Refresh();

                    string assetPath = "Assets" + singleOutputPath.Substring(Application.dataPath.Length).Replace('\\', '/');
                    Texture2D exportedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

                    if (exportedTexture != null)
                    {
                        Debug.Log(GetLocalizedText("fallbackExportSuccess") + ": " + assetPath);

                        // 设置为当前纹理
                        sourceTexture = exportedTexture;

                        // 自动切片纹理
                        SliceExportedTexture(assetPath);

                        EditorUtility.DisplayDialog(GetLocalizedText("importSuccess"),
                            GetLocalizedText("asepriteImportSuccessSingleTexture"), GetLocalizedText("ok"));
                    }
                    else
                    {
                        Debug.LogError(GetLocalizedText("failedToLoadTexture", assetPath));
                        EditorUtility.DisplayDialog(GetLocalizedText("importFailed"),
                            GetLocalizedText("failedToLoadExportedTexture"), GetLocalizedText("ok"));
                    }
                }
                else
                {
                    Debug.LogError(GetLocalizedText("fallbackExportFailed") + ": " + singleError);
                    EditorUtility.DisplayDialog(GetLocalizedText("importFailed"),
                        GetLocalizedText("bothExportMethodsFailed") + ": " + singleError, GetLocalizedText("ok"));
                }
            }
        }

        private void SliceExportedTexture(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"无法获取 TextureImporter: {texturePath}");
                return;
            }

            // 设置基本导入参数
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;

            // 先应用基本设置
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

            // 加载纹理
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"无法加载纹理: {texturePath}");
                return;
            }

            // 自动检测精灵尺寸
            int spriteSize = sliceSize;
            Debug.Log($"检测到精灵尺寸: {spriteSize}x{spriteSize}");

            // 使用官方API进行切片
            AutoSliceSpritesSkipEmpty(importer, texture, spriteSize, spriteSize);
        }

        private void AutoSliceSpritesSkipEmpty(TextureImporter importer, Texture2D texture, int cellWidth, int cellHeight)
        {
            try
            {
                // 创建 SpriteDataProviderFactories 实例
                var factory = new SpriteDataProviderFactories();
                factory.Init();

                // 获取数据提供者
                var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                if (dataProvider == null)
                {
                    Debug.LogError("无法获取 ISpriteEditorDataProvider");
                    return;
                }

                // 初始化数据提供者
                dataProvider.InitSpriteEditorDataProvider();

                // 获取纹理像素数据
                Color[] pixels = texture.GetPixels();

                // 清除现有的精灵
                var spriteRects = new List<SpriteRect>();

                // 计算网格
                int cols = texture.width / cellWidth;
                int rows = texture.height / cellHeight;

                Debug.Log($"分析 {cols}x{rows} 网格，检测非空白区域...");

                int nonEmptyCount = 0;

                // 遍历每个网格单元
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        // 检查当前网格是否为空白
                        if (IsCellEmpty(pixels, texture.width, texture.height, col, row, cellWidth, cellHeight))
                        {
                            continue; // 跳过空白单元格
                        }

                        var spriteRect = new SpriteRect()
                        {
                            name = $"sprite_{col}_{row}",
                            spriteID = GUID.Generate(),
                            rect = new Rect(
                                col * cellWidth,
                                row * cellHeight, // Unity坐标系从左下角开始
                                cellWidth,
                                cellHeight
                            ),
                            alignment = SpriteAlignment.Center,
                            pivot = new Vector2(0.5f, 0.5f),
                            border = Vector4.zero
                        };

                        spriteRects.Add(spriteRect);
                        nonEmptyCount++;
                    }
                }

                Debug.Log($"找到 {nonEmptyCount} 个非空白精灵（跳过了 {cols * rows - nonEmptyCount} 个空白格）");

                if (spriteRects.Count == 0)
                {
                    Debug.LogWarning("没有找到任何非空白区域，请检查透明度阈值设置");
                    return;
                }

                // 设置精灵数据
                dataProvider.SetSpriteRects(spriteRects.ToArray());

                // Unity 2021.2+ 需要额外设置名称和ID对
#if UNITY_2021_2_OR_NEWER
                try
                {
                    var spriteNameFileIdDataProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                    if (spriteNameFileIdDataProvider != null)
                    {
                        var nameFileIdPairs = spriteRects.Select(rect =>
                            new SpriteNameFileIdPair(rect.name, rect.spriteID)).ToList();
                        spriteNameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"设置名称ID对时出错（可能是Unity版本问题）: {e.Message}");
                }
#endif

                // 应用更改
                dataProvider.Apply();

                // 重新导入资源
                var assetImporter = dataProvider.targetObject as AssetImporter;
                if (assetImporter != null)
                {
                    assetImporter.SaveAndReimport();
                }

                Debug.Log($"成功创建 {spriteRects.Count} 个非空白精灵切片");

                // 验证结果
                EditorApplication.delayCall += () => VerifySlicingResult(importer.assetPath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"使用官方API切片失败: {e.Message}");
                Debug.LogError($"堆栈跟踪: {e.StackTrace}");
            }
        }

// 空白检测的配置参数
[SerializeField] private float alphaThreshold = 0.01f; // 透明度阈值
[SerializeField] private float emptyPixelRatio = 0.95f; // 空白像素比例阈值

        private bool IsCellEmpty(Color[] pixels, int textureWidth, int textureHeight, int cellCol, int cellRow, int cellWidth, int cellHeight)
        {
            int startX = cellCol * cellWidth;
            int startY = cellRow * cellHeight;

            int totalPixels = cellWidth * cellHeight;
            int emptyPixels = 0;

            // 检查网格内的每个像素
            for (int y = startY; y < startY + cellHeight && y < textureHeight; y++)
            {
                for (int x = startX; x < startX + cellWidth && x < textureWidth; x++)
                {
                    int pixelIndex = y * textureWidth + x;

                    if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                    {
                        Color pixel = pixels[pixelIndex];

                        // 检查像素是否为"空白"（透明或接近透明）
                        if (IsPixelEmpty(pixel))
                        {
                            emptyPixels++;
                        }
                    }
                }
            }

            // 如果空白像素比例超过阈值，则认为整个网格为空白
            float emptyRatio = (float)emptyPixels / totalPixels;
            return emptyRatio >= emptyPixelRatio;
        }

        private bool IsPixelEmpty(Color pixel)
        {
            // 方法1: 仅检查透明度
            if (pixel.a <= alphaThreshold)
                return true;

            // 方法2: 检查是否为纯白色（常见的空白填充）
            if (pixel.a > 0.9f && pixel.r > 0.95f && pixel.g > 0.95f && pixel.b > 0.95f)
                return true;

            // 方法3: 检查是否为纯黑色且透明度很低（某些导出格式）
            if (pixel.a <= 0.1f && pixel.r <= 0.05f && pixel.g <= 0.05f && pixel.b <= 0.05f)
                return true;

            return false;
        }

        private void VerifySlicingResult(string assetPath)
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                int spriteCount = assets.Count(asset => asset is Sprite);
                Debug.Log($"验证结果: 在 {assetPath} 中找到 {spriteCount} 个精灵");

                if (spriteCount > 0)
                {
                    // 更新当前纹理引用
                    sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"验证切片结果时出错: {e.Message}");
            }
        }

        // 菜单项测试
        [MenuItem("Tools/Tilemap Tools/Auto Slice Selected Texture")]
        private static void SliceSelectedTexture()
        {
            var selectedObject = Selection.activeObject;
            if (selectedObject != null)
            {
                string path = AssetDatabase.GetAssetPath(selectedObject);
                if (!string.IsNullOrEmpty(path) && (path.ToLower().EndsWith(".png") || path.ToLower().EndsWith(".jpg")))
                {
                    var window = GetWindow<TilePaletteGenerator>();
                    window.SliceExportedTexture(path);
                }
                else
                {
                    Debug.LogWarning(GetLocalizedText("selectImageFile"));
                }
            }
            else
            {
                Debug.LogWarning(GetLocalizedText("selectTextureFirst"));
            }
        }

        private void OnGUI()
        {
            // 使用本地化文本
            GUILayout.Label(GetLocalizedText("title"), EditorStyles.boldLabel);

            // Aseprite文件支持
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("asepriteSupport"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(GetLocalizedText("HowTo"), MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetLocalizedText("sliceSize"), GUILayout.Width(150));
            sliceSize = EditorGUILayout.IntField(sliceSize);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(asepriteExePath))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(GetLocalizedText("asepriteExecutable"), GUILayout.Width(150));
                asepriteExePath = EditorGUILayout.TextField(asepriteExePath);
                if (GUILayout.Button(GetLocalizedText("browse"), GUILayout.Width(60)))
                {
                    string path = EditorUtility.OpenFilePanel(
                        GetLocalizedText("selectAsepriteExecutable"),
                        "",
                        GetAsepriteExecutableExtension());
                    if (!string.IsNullOrEmpty(path))
                    {
                        asepriteExePath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();

                // 添加Aseprite输出路径设置
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(GetLocalizedText("asepriteOutputPath"), GUILayout.Width(150));
                asepriteOutputPath = EditorGUILayout.TextField(asepriteOutputPath);
                if (GUILayout.Button(GetLocalizedText("browse"), GUILayout.Width(60)))
                {
                    string selectedPath = EditorUtility.OpenFolderPanel(
                        GetLocalizedText("selectAsepriteOutputFolder"),
                        asepriteOutputPath,
                        "");
                    if (!string.IsNullOrEmpty(selectedPath) && selectedPath.StartsWith(Application.dataPath))
                        asepriteOutputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                EditorGUILayout.EndHorizontal();

                // 测试Aseprite CLI按钮
                if (GUILayout.Button(GetLocalizedText("testCLI")))
                {
                    TestAsepriteCLI();
                }
                includeHiddenLayers = EditorGUILayout.Toggle(GetLocalizedText("includeHiddenLayers"), includeHiddenLayers);
            }

            // 选择Aseprite文件
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Aseprite File:", GUILayout.Width(150));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(asepriteFilePath) ? "No file selected" : Path.GetFileName(asepriteFilePath));
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel(
                    "Select Aseprite File",
                    "",
                    "aseprite,ase");
                if (!string.IsNullOrEmpty(path))
                {
                    asepriteFilePath = path;
                    useAseprite = true;

                    // 如果选择了Aseprite文件，自动设置调色板文件名
                    if (!string.IsNullOrEmpty(asepriteFilePath))
                    {
                        paletteFileName = Path.GetFileNameWithoutExtension(asepriteFilePath);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // 如果选择了Aseprite文件，显示导入按钮
            if (!string.IsNullOrEmpty(asepriteFilePath))
            {
                if (GUILayout.Button("Import Aseprite File"))
                {
                    if (!useAseprite)
                    {
                        useAseprite = true; // 确保标志被设置
                    }
                    ImportAsepriteFile();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("singleImageProcessing"), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 源纹理选择
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField(GetLocalizedText("sourceTexture"), sourceTexture, typeof(Texture2D), false);
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

        public void GenerateTilePalette()
        {
            // 如果设置了使用Aseprite但没有源纹理，提示用户先导入
            if (useAseprite && sourceTexture == null)
            {
                EditorUtility.DisplayDialog(
                    GetLocalizedText("error"),
                    GetLocalizedText("asepriteImportRequired"),
                    GetLocalizedText("ok"));
                return;
            }

            string sanitizedFileName = SanitizeFileName(paletteFileName.Trim());
            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                Debug.LogError(GetLocalizedText("invalidPaletteFileName"));
                return;
            }

            EnsureDirectoryExists(tilesOutputPath);
            EnsureDirectoryExists(paletteOutputPath);

            // 获取纹理路径
            string texturePath = AssetDatabase.GetAssetPath(sourceTexture);

            // 获取精灵
            Sprite[] sprites = GetSpritesFromTexture(sourceTexture);

            // 检查纹理是否已被切片
            if (sprites == null || sprites.Length == 0)
            {
                // 提示用户纹理未切片，询问是否自动切片
                bool shouldSlice = EditorUtility.DisplayDialog(
                    GetLocalizedText("textureNotSliced"),
                    GetLocalizedText("textureNotSlicedMessage", sourceTexture.name),
                    GetLocalizedText("autoSlice"),
                    GetLocalizedText("cancel")
                );

                if (shouldSlice)
                {
                    // 自动切片纹理
                    SliceExportedTexture(texturePath);

                    // 重新获取精灵
                    sprites = GetSpritesFromTexture(sourceTexture);

                    // 如果切片后仍然没有精灵，则退出
                    if (sprites == null || sprites.Length == 0)
                    {
                        Debug.LogError(GetLocalizedText("textureNotSliced"));
                        return;
                    }
                }
                else
                {
                    Debug.LogError(GetLocalizedText("noSpritesFound"));
                    return;
                }
            }

            // 检查是否有未被切片的区域
            bool hasUnslicedPixels = CheckForUnslicedPixels(sourceTexture, sprites);

            // 如果发现未切片区域，询问用户是否继续
            if (hasUnslicedPixels)
            {
                bool shouldContinue = EditorUtility.DisplayDialog(
                    GetLocalizedText("unslicedPixelsWarning"),
                    string.Format(GetLocalizedText("unslicedPixelsMessageWithFileName"), sourceTexture.name, texturePath),
                    GetLocalizedText("continueGeneration"),
                    GetLocalizedText("cancelGeneration")
                );

                if (!shouldContinue)
                {
                    Debug.LogWarning(GetLocalizedText("workflowCancelled", sourceTexture.name, texturePath));
                    return;
                }
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
                string message = GetLocalizedText("irregularSizesWarning") + "\n" + string.Join("\n", irregularSprites);
                message += "\n\n" + GetLocalizedText("squareSizesRequired");

                EditorUtility.DisplayDialog(
                    GetLocalizedText("irregularSizes"),
                    message,
                    GetLocalizedText("ok"));
                Debug.LogWarning(GetLocalizedText("generationCancelledIrregular"));
                return;
            }

            var spriteData = ProcessSprites(sprites);
            if (spriteData.Count == 0)
            {
                Debug.LogError(GetLocalizedText("noValidSprites"));
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
                Debug.LogFormat(GetLocalizedText("paletteCreatedSuccessfully"), finalPalettePath);

                // 通知工作流管理器
                TilemapWorkflowManager.SetLastGeneratedPalette(finalPalettePath);

                // 检查是否在自动化工作流中
                if (!TilemapWorkflowManager.IsAutomatedWorkflow)
                {
                    // 只有在非自动化工作流中才询问用户是否要进入下一步
                    if (EditorUtility.DisplayDialog(
                        GetLocalizedText("title"),
                        GetLocalizedText("paletteGenerated", sanitizedFileName),
                        GetLocalizedText("yesNextStep"),
                        GetLocalizedText("stayHere")))
                    {
                        // 设置当前步骤
                        TilemapWorkflowManager.SetCurrentStep(TilemapWorkflowManager.WorkflowStep.CreateRules);

                        // 显示并激活工作流管理器窗口
                        var workflowWindow = EditorWindow.GetWindow<TilemapWorkflowManager>(GetLocalizedText("windowTitle"));
                        workflowWindow.Show();
                        workflowWindow.Focus();
                    }
                }
                else
                {
                    // 在自动化工作流中，只设置当前步骤，不显示对话框和窗口
                    TilemapWorkflowManager.SetCurrentStep(TilemapWorkflowManager.WorkflowStep.CreateRules);

                    Debug.LogFormat(GetLocalizedText("automatedWorkflowNextStep"));
                }
            }
        }

        // MODIFICATION: Changed return type from void to string
        public string CreateTilePalette(Dictionary<Vector2Int, Tile> tilePositions, int gridWidth, int gridHeight, string fileName)
        {
            // 创建调色板GameObject
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
                Debug.LogError(GetLocalizedText("cannotSavePalette"));
                DestroyImmediate(paletteGO);
                return null;
            }

            // 在自动化工作流中，使用不同的逻辑处理现有文件
            if (File.Exists(prefabPath))
            {
                if (TilemapWorkflowManager.IsAutomatedWorkflow)
                {
                    // 在自动化工作流中，默认更新现有调色板
                    GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (existingPrefab != null)
                    {
                        // 创建预制体的实例
                        GameObject existingInstance = PrefabUtility.InstantiatePrefab(existingPrefab) as GameObject;

                        // 查找现有的Tilemap组件
                        Tilemap existingTilemap = existingInstance.GetComponentInChildren<Tilemap>();

                        if (existingTilemap != null)
                        {
                            // 清除现有的瓦片
                            existingTilemap.ClearAllTiles();

                            // 复制新的瓦片到现有的Tilemap
                            foreach (var kvp in tilePositions)
                            {
                                Vector3Int position = new Vector3Int(kvp.Key.x, gridHeight - 1 - kvp.Key.y, 0);
                                existingTilemap.SetTile(position, kvp.Value);
                            }

                            // 应用更改到预制体
                            PrefabUtility.SaveAsPrefabAsset(existingInstance, prefabPath);
                            DestroyImmediate(existingInstance);
                            DestroyImmediate(paletteGO);

                            Debug.Log(GetLocalizedText("paletteUpdated", prefabPath));
                            return prefabPath;
                        }
                        else
                        {
                            // 如果没有找到Tilemap组件，则删除所有子对象并添加新的
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

                            Debug.Log(GetLocalizedText("paletteUpdated", prefabPath));
                            return prefabPath;
                        }
                    }
                    else
                    {
                        // 如果无法加载预制体，则覆盖它
                        AssetDatabase.DeleteAsset(prefabPath);
                        PrefabUtility.SaveAsPrefabAsset(paletteGO, prefabPath);
                        DestroyImmediate(paletteGO);
                        Debug.Log(GetLocalizedText("paletteSaved", prefabPath));
                        return prefabPath;
                    }
                }
                else
                {
                    // 在交互式模式下，显示对话框
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

                                // 查找现有的Tilemap组件
                                Tilemap existingTilemap = existingInstance.GetComponentInChildren<Tilemap>();

                                if (existingTilemap != null)
                                {
                                    // 清除现有的瓦片
                                    existingTilemap.ClearAllTiles();

                                    // 复制新的瓦片到现有的Tilemap
                                    foreach (var kvp in tilePositions)
                                    {
                                        Vector3Int position = new Vector3Int(kvp.Key.x, gridHeight - 1 - kvp.Key.y, 0);
                                        existingTilemap.SetTile(position, kvp.Value);
                                    }

                                    // 应用更改到预制体
                                    PrefabUtility.SaveAsPrefabAsset(existingInstance, prefabPath);
                                    DestroyImmediate(existingInstance);
                                    DestroyImmediate(paletteGO);

                                    Debug.Log(GetLocalizedText("paletteUpdated", prefabPath));
                                    return prefabPath;
                                }
                                else
                                {
                                    // 如果没有找到Tilemap组件，则删除所有子对象并添加新的
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

                                    Debug.Log(GetLocalizedText("paletteUpdated", prefabPath));
                                    return prefabPath;
                                }
                            }
                            else
                            {
                                // 如果无法加载预制体，则覆盖它
                                AssetDatabase.DeleteAsset(prefabPath);
                                PrefabUtility.SaveAsPrefabAsset(paletteGO, prefabPath);
                                DestroyImmediate(paletteGO);
                                Debug.Log(GetLocalizedText("paletteSaved", prefabPath));
                                return prefabPath;
                            }
                        case 1: // Cancel
                            DestroyImmediate(paletteGO);
                            return null;
                        case 2: // Create New
                            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                            break;
                    }
                }
            }

            PrefabUtility.SaveAsPrefabAsset(paletteGO, prefabPath);
            DestroyImmediate(paletteGO);
            AssetDatabase.SaveAssets();
            Debug.Log(GetLocalizedText("paletteSaved", prefabPath));

            return prefabPath;
        }

        public static string GenerateTilePaletteStatic(
            Texture2D sourceTexture,
            string tilesOutputPath,
            string paletteOutputPath,
            string paletteFileName,
            bool preserveGuids = false)
        {
            // 添加调试日志
            Debug.Log(GetLocalizedText("startGeneratingPalette", paletteFileName));
            Debug.Log(GetLocalizedText("sourceTextureInfo", (sourceTexture != null ? sourceTexture.name : "null")));
            Debug.Log(GetLocalizedText("tilesOutputPathInfo", tilesOutputPath));
            Debug.Log(GetLocalizedText("paletteOutputPathInfo", paletteOutputPath));

            if (sourceTexture == null)
            {
                Debug.LogError(GetLocalizedText("sourceTextureEmpty"));
                return "";
            }

            // 检查纹理是否已切片
            string texturePath = AssetDatabase.GetAssetPath(sourceTexture);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToArray();

            Debug.Log(GetLocalizedText("foundSpritesInTexture", sprites.Length));

            if (sprites.Length == 0)
            {
                Debug.LogError(GetLocalizedText("textureNotSliced"));
                return "";
            }

            // 保存当前的静态变量状态
            bool originalPreserveGUIDs = preserveGUIDs;

            // 设置静态保留GUID选项
            preserveGUIDs = preserveGuids;

            // 创建实例
            TilePaletteGenerator generator = CreateStaticInstance();

            // 设置参数
            generator.sourceTexture = sourceTexture;
            generator.tilesOutputPath = tilesOutputPath;
            generator.paletteOutputPath = paletteOutputPath;
            generator.paletteFileName = paletteFileName;

            // 检查是否有未被切片的区域
            bool hasUnslicedPixels = generator.CheckForUnslicedPixels(sourceTexture, sprites);

            // 如果发现未切片区域，询问用户是否继续
            if (hasUnslicedPixels)
            {
                bool shouldContinue = EditorUtility.DisplayDialog(
                    GetLocalizedText("unslicedPixelsWarning"),
                    string.Format(GetLocalizedText("unslicedPixelsMessageWithFileName"), sourceTexture.name, texturePath),
                    GetLocalizedText("continueGeneration"),
                    GetLocalizedText("cancelGeneration")
                );

                if (!shouldContinue)
                {
                    Debug.LogWarning($"Sprite '{sourceTexture.name}' ({texturePath}) has unsliced pixel，workflow cancelled。");
                    ScriptableObject.DestroyImmediate(generator);
                    preserveGUIDs = originalPreserveGUIDs;
                    return "";
                }
            }

            // 处理精灵
            string sanitizedFileName = generator.SanitizeFileName(paletteFileName.Trim());
            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                Debug.LogError(GetLocalizedText("invalidPaletteFileName"));
                ScriptableObject.DestroyImmediate(generator);
                preserveGUIDs = originalPreserveGUIDs;
                return "";
            }

            generator.EnsureDirectoryExists(tilesOutputPath);
            generator.EnsureDirectoryExists(paletteOutputPath);

            // 直接处理精灵，而不是调用GenerateTilePalette
            List<SpriteData> spriteData = generator.ProcessSprites(sprites);
            if (spriteData.Count == 0)
            {
                Debug.LogError(GetLocalizedText("noValidSprites"));
                ScriptableObject.DestroyImmediate(generator);
                preserveGUIDs = originalPreserveGUIDs;
                return "";
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
                    Tile newTile = generator.CreateTileFromSprite(data.sprite, data.hash, ref createdTileAssets);
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

            // 直接创建调色板并获取路径
            string palettePath = "";
            try
            {
                palettePath = generator.CreateTilePalette(tilePositions, gridWidth, gridHeight, sanitizedFileName);

                // 验证文件是否存在
                if (!File.Exists(palettePath))
                {
                    Debug.LogError(GetLocalizedText("paletteNoTiles", palettePath));
                    palettePath = "";
                }
                else
                {
                    // 验证调色板是否包含瓦片
                    GameObject palette = AssetDatabase.LoadAssetAtPath<GameObject>(palettePath);
                    if (palette != null)
                    {
                        Tilemap tilemap = palette.GetComponentInChildren<Tilemap>();
                        if (tilemap != null)
                        {
                            BoundsInt bounds = tilemap.cellBounds;
                            TileBase[] allTiles = tilemap.GetTilesBlock(bounds);
                            int tileCount = allTiles.Count(t => t != null);

                            Debug.Log(GetLocalizedText("paletteContainsTiles", paletteFileName, tileCount));

                            if (tileCount == 0)
                            {
                                Debug.LogWarning(GetLocalizedText("paletteNoTiles", palettePath));
                            }
                        }
                        else
                        {
                            Debug.LogError(GetLocalizedText("paletteNoTilemap", palettePath));
                        }
                    }
                    else
                    {
                        Debug.LogError(GetLocalizedText("cannotLoadPalette", palettePath));
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(GetLocalizedText("paletteGenerationError", e.Message, e.StackTrace));
                palettePath = "";
            }
            finally
            {
                // 销毁实例
                ScriptableObject.DestroyImmediate(generator);

                // 恢复原始的静态变量状态
                preserveGUIDs = originalPreserveGUIDs;
            }

            return palettePath;
        }

        public Tile CreateTileFromSprite(Sprite sprite, string hash, ref int createdTileAssets)
        {
            string shortHash = hash.Substring(0, Mathf.Min(16, hash.Length));
            string path = $"{tilesOutputPath}/tile_{shortHash}.asset";
            bool fileExists = File.Exists(path);

            if (fileExists)
            {
                // 在自动化工作流中，直接使用静态变量
                if (TilemapWorkflowManager.IsAutomatedWorkflow)
                {
                    // 如果设置为不保留GUID，则加载现有资产
                    if (!preserveGUIDs)
                    {
                        return AssetDatabase.LoadAssetAtPath<Tile>(path);
                    }
                }
                else
                {
                    // 在交互式模式下，显示对话框
                    bool shouldUpdate = EditorUtility.DisplayDialog(
                        GetLocalizedText("assetsExist"),
                        GetLocalizedText("updateExistingTiles"),
                        GetLocalizedText("updateAll"),
                        GetLocalizedText("skipAll"));

                    // 更新静态变量以保持一致性
                    preserveGUIDs = shouldUpdate;

                    // 如果用户选择不更新，则加载现有资产
                    if (!shouldUpdate)
                    {
                        return AssetDatabase.LoadAssetAtPath<Tile>(path);
                    }
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

        private bool CheckForUnslicedPixels(Texture2D texture, Sprite[] sprites)
        {
            // 确保纹理可读
            MakeTextureReadable(texture);

            // 添加调试信息
            Debug.Log($"Checking for unsliced pixels in texture: {texture.name} ({texture.width}x{texture.height})");
            Debug.Log($"Found {sprites.Length} sprites in this texture");

            // 创建一个布尔数组来标记哪些像素被精灵覆盖
            bool[,] coveredPixels = new bool[texture.width, texture.height];

            // 标记所有被精灵覆盖的像素
            foreach (Sprite sprite in sprites)
            {
                Rect rect = sprite.rect;
                int startX = (int)rect.x;
                int startY = (int)rect.y;
                int endX = startX + (int)rect.width;
                int endY = startY + (int)rect.height;

                for (int x = startX; x < endX; x++)
                {
                    for (int y = startY; y < endY; y++)
                    {
                        if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                        {
                            coveredPixels[x, y] = true;
                        }
                    }
                }
            }

            // 检查纹理中是否有未被覆盖的非透明像素
            List<Vector2Int> unslicedPixels = new List<Vector2Int>();
            int maxReportedPixels = 10; // 限制报告的未切片像素数量

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Color pixel = texture.GetPixel(x, y);

                    // 如果像素不透明且未被任何精灵覆盖
                    if (pixel.a > transparencyThreshold && !coveredPixels[x, y])
                    {
                        unslicedPixels.Add(new Vector2Int(x, y));
                        if (unslicedPixels.Count <= maxReportedPixels)
                        {
                            Debug.Log($"Found unsliced pixel at ({x},{y}) with alpha={pixel.a}");
                        }
                        else if (unslicedPixels.Count == maxReportedPixels + 1)
                        {
                            Debug.Log("More unsliced pixels found (not listing all)...");
                        }
                    }
                }
            }

            if (unslicedPixels.Count > 0)
            {
                Debug.Log($"Total unsliced pixels found: {unslicedPixels.Count}");
                return true;
            }

            Debug.Log("No unsliced pixels found in the texture");
            return false;
        }

        private class SpriteData { public Sprite sprite; public bool isTransparent; public string hash; public Vector2 pivot; }
        private Sprite[] GetSpritesFromTexture(Texture2D texture) { string path = AssetDatabase.GetAssetPath(texture); return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray(); }
        private List<SpriteData> ProcessSprites(Sprite[] sprites)
        {
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
        private Color[] GetSpritePixels(Sprite sprite)
        {
            MakeTextureReadable(sprite.texture);
            Rect rect = sprite.rect;
            return sprite.texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        }
        private bool IsTransparent(Color[] pixels)
        {
            float totalAlpha = 0f;
            foreach (Color pixel in pixels) totalAlpha += pixel.a;
            return (totalAlpha / pixels.Length) < transparencyThreshold;
        }
        private string CalculatePixelHash(Color[] pixels)
        {
            switch (dedupPrecision)
            {
                case DedupPrecision.Low: return CalculateLowPrecisionHash(pixels);
                case DedupPrecision.High: return CalculateHighPrecisionHash(pixels);
                case DedupPrecision.Medium: default: return CalculateMediumPrecisionHash(pixels);
            }
        }
        private string CalculateLowPrecisionHash(Color[] pixels)
        {
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
        private string CalculateMediumPrecisionHash(Color[] pixels)
        {
            int pixelsHashCode = GetPixelsHashCode(pixels);
            if (pixelHashCache.ContainsKey(pixelsHashCode)) return pixelHashCache[pixelsHashCode];
            string hash = ComputeExactHash(pixels);
            pixelHashCache[pixelsHashCode] = hash;
            return hash;
        }
        private string CalculateHighPrecisionHash(Color[] pixels)
        {
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
        private int GetPixelsHashCode(Color[] pixels)
        {
            unchecked
            {
                int hash = 17;
                int step = Mathf.Max(1, pixels.Length / 10);
                for (int i = 0; i < pixels.Length; i += step) { Color32 c = pixels[i]; hash = hash * 23 + c.r; hash = hash * 23 + c.g; hash = hash * 23 + c.b; hash = hash * 23 + c.a; }
                return hash;
            }
        }
        private string ComputeExactHash(Color[] pixels) { return CalculateHighPrecisionHash(pixels); } // Simplified to use the most robust hash
        private void MakeTextureReadable(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        }
        private string SanitizeFileName(string fileName) { return string.Join("_", fileName.Split(Path.GetInvalidFileNameChars())); }
        private void EnsureDirectoryExists(string path) { if (!Directory.Exists(path)) Directory.CreateDirectory(path); AssetDatabase.Refresh(); }
    }
}
#endif