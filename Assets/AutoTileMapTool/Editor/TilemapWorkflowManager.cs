#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TilemapTools
{
    public class TilemapWorkflowManager : EditorWindow
    {
        public enum WorkflowStep { GeneratePalette, CreateRules, ApplyTerrain, LayerEditing }
        private WorkflowStep currentStep = WorkflowStep.GeneratePalette;

        // 添加静态变量来跟踪生成的资源
        private static string lastGeneratedPalettePath = "";
        private static TilemapWorkflowManager instance;
        
        // 本地化文本字典
        private Dictionary<string, Dictionary<string, string>> localizedTexts;

        // 添加语言设置相关变量
        private bool showLanguageSettings = false;
        private TilemapLanguageManager.Language selectedLanguage;

        public static void ShowWindow()
        {
            instance = GetWindow<TilemapWorkflowManager>("Tilemap Workflow");
        }

        // 添加静态方法供其他工具调用
        public static void SetLastGeneratedPalette(string palettePath)
        {
            lastGeneratedPalettePath = palettePath;
            if (instance != null)
            {
                instance.Repaint();
            }
        }

        public static void SetCurrentStep(WorkflowStep step)
        {
            if (instance == null)
            {
                instance = GetWindow<TilemapWorkflowManager>("Tilemap Workflow");
            }
            instance.currentStep = step;
            instance.Repaint();
        }

        private void OnEnable()
        {
            instance = this;
            selectedLanguage = TilemapLanguageManager.GetCurrentLanguageSetting();
            InitializeLocalization();
        }

        // 初始化本地化文本
        private void InitializeLocalization()
        {
            localizedTexts = new Dictionary<string, Dictionary<string, string>>();
            
            // 英语文本
            var enTexts = new Dictionary<string, string>
            {
                {"title", "Tilemap Creation Workflow"},
                {"step1", "1. Generate Palette"},
                {"step2", "2. Create Rules"},
                {"step3", "3. Apply Terrain"},
                {"step4", "4. Layer Tools"},
                {"step1Help", "Step 1: Generate a Tile Palette from a sliced Sprite Sheet."},
                {"openGenerator", "Open Tile Palette Generator"},
                {"lastGenerated", "Last Generated Palette:"},
                {"selectInProject", "Select in Project"},
                {"instructions", "Instructions:"},
                {"step1Instr1", "1. Select a sliced sprite sheet texture"},
                {"step1Instr2", "2. Configure output paths and settings"},
                {"step1Instr3", "3. Click 'Generate Tile Palette'"},
                {"step1Instr4", "4. Return here and go to next step"},
                {"next", "Next: {0} →"},
                {"previous", "← Previous"},
                {"step2Help", "Step 2: Create an AutoTerrainTileRuleConfiger asset to define terrain rules."},
                {"createNewRule", "Create New Auto Terrain Tile Rule"},
                {"selectExisting", "Select Existing Asset"},
                {"step2Instr1", "1. Create or select an AutoTerrainTileRuleConfiger asset"},
                {"step2Instr2", "2. Configure terrain rules in the Inspector"},
                {"step2Instr3", "3. Assign sprites from your generated palette"},
                {"step2Instr4", "4. Return here and go to next step"},
                {"step3Help", "Step 3: Apply the rules to a base tilemap to generate the final terrain."},
                {"openTerrainEditor", "Open Auto Terrain Editor"},
                {"step3Instr1", "1. Select source and output tilemaps"},
                {"step3Instr2", "2. Add your AutoTerrainTileRuleConfiger as iteration step"},
                {"step3Instr3", "3. Apply terrain rules to generate final result"},
                {"step3Instr4", "4. Return here for layer editing tools"},
                {"step4Help", "Step 4: Use advanced tools to edit and manage tilemap layers."},
                {"openLayerTool", "Open Tilemap Layer Tool"},
                {"step4Instr1", "1. Select source and target tilemaps"},
                {"step4Instr2", "2. Use selection tools to choose areas"},
                {"step4Instr3", "3. Move, copy, or manipulate tile layers"},
                {"step4Instr4", "4. Save selections for future use"},
                {"workflowComplete", "Workflow Complete! You can cycle through steps as needed."},
                {"languageSettings", "Language Settings"},
                {"language", "Language:"},
                {"apply", "Apply"},
                {"restartRequired", "Changes will take full effect after restarting windows"}
            };
            localizedTexts["en"] = enTexts;
            
            // 中文文本
            var zhTexts = new Dictionary<string, string>
            {
                {"title", "瓦片地图创建工作流"},
                {"step1", "1. 生成调色板"},
                {"step2", "2. 创建规则"},
                {"step3", "3. 应用地形"},
                {"step4", "4. 图层工具"},
                {"step1Help", "步骤1：从切片的精灵表生成瓦片调色板。"},
                {"openGenerator", "打开瓦片调色板生成器"},
                {"lastGenerated", "最近生成的调色板："},
                {"selectInProject", "在项目中选择"},
                {"instructions", "操作指南："},
                {"step1Instr1", "1. 选择已切片的精灵表纹理"},
                {"step1Instr2", "2. 配置输出路径和设置"},
                {"step1Instr3", "3. 点击<生成瓦片调色板>"},
                {"step1Instr4", "4. 返回此处并进入下一步"},
                {"next", "下一步：{0} →"},
                {"previous", "← 上一步"},
                {"step2Help", "步骤2：创建自动地形瓦片规则配置器资产以定义地形规则。"},
                {"createNewRule", "创建新的自动地形瓦片规则"},
                {"selectExisting", "选择现有资产"},
                {"step2Instr1", "1. 创建或选择自动地形瓦片规则配置器资产"},
                {"step2Instr2", "2. 在检查器中配置地形规则"},
                {"step2Instr3", "3. 从生成的调色板中分配精灵"},
                {"step2Instr4", "4. 返回此处并进入下一步"},
                {"step3Help", "步骤3：将规则应用于基础瓦片地图以生成最终地形。"},
                {"openTerrainEditor", "打开自动地形编辑器"},
                {"step3Instr1", "1. 选择源和输出瓦片地图"},
                {"step3Instr2", "2. 添加您的自动地形瓦片规则配置器作为迭代步骤"},
                {"step3Instr3", "3. 应用地形规则生成最终结果"},
                {"step3Instr4", "4. 返回此处使用图层编辑工具"},
                {"step4Help", "步骤4：使用高级工具编辑和管理瓦片地图图层。"},
                {"openLayerTool", "打开瓦片地图图层工具"},
                {"step4Instr1", "1. 选择源和目标瓦片地图"},
                {"step4Instr2", "2. 使用选择工具选择区域"},
                {"step4Instr3", "3. 移动、复制或操作瓦片图层"},
                {"step4Instr4", "4. 保存选择以供将来使用"},
                {"workflowComplete", "工作流程完成！您可以根据需要循环使用各个步骤。"},
                {"languageSettings", "语言设置"},
                {"language", "语言："},
                {"apply", "应用"},
                {"restartRequired", "更改将在重新启动窗口后完全生效"}
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

        private void OnGUI()
        {
            // 标题和语言设置按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(GetLocalizedText("title"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(GetLocalizedText("languageSettings"), EditorStyles.miniButton))
            {
                showLanguageSettings = !showLanguageSettings;
            }
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
                
                if (GUILayout.Button(GetLocalizedText("apply")))
                {
                    TilemapLanguageManager.SetCurrentLanguageSetting(selectedLanguage);
                    // 应用后自动隐藏语言设置菜单
                    showLanguageSettings = false;
                    Repaint();
                }
                
                EditorGUILayout.HelpBox(GetLocalizedText("restartRequired"), MessageType.Info);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space();
            }
            
            // 工作流步骤标签页
            currentStep = (WorkflowStep)GUILayout.Toolbar((int)currentStep, new string[]
            {
                GetLocalizedText("step1"), 
                GetLocalizedText("step2"), 
                GetLocalizedText("step3"), 
                GetLocalizedText("step4")
            });

            EditorGUILayout.Space();

            switch(currentStep)
            {
                case WorkflowStep.GeneratePalette: 
                    DrawPaletteGenerationStep(); 
                    break;
                case WorkflowStep.CreateRules: 
                    DrawRuleCreationStep(); 
                    break;
                case WorkflowStep.ApplyTerrain: 
                    DrawTerrainApplicationStep(); 
                    break;
                case WorkflowStep.LayerEditing: 
                    DrawLayerEditingStep(); 
                    break;
            }
        }

        private void DrawPaletteGenerationStep()
        {
            EditorGUILayout.HelpBox(GetLocalizedText("step1Help"), MessageType.Info);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button(GetLocalizedText("openGenerator"), GUILayout.Height(40)))
            {
                TilePaletteGenerator.ShowWindow();
            }
            
            // 显示最后生成的调色板
            if (!string.IsNullOrEmpty(lastGeneratedPalettePath))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(GetLocalizedText("lastGenerated"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(System.IO.Path.GetFileName(lastGeneratedPalettePath));
                if (GUILayout.Button(GetLocalizedText("selectInProject")))
                {
                    var palette = AssetDatabase.LoadAssetAtPath<GameObject>(lastGeneratedPalettePath);
                    if (palette != null)
                    {
                        Selection.activeObject = palette;
                        EditorGUIUtility.PingObject(palette);
                    }
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("instructions"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetLocalizedText("step1Instr1"));
            EditorGUILayout.LabelField(GetLocalizedText("step1Instr2"));
            EditorGUILayout.LabelField(GetLocalizedText("step1Instr3"));
            EditorGUILayout.LabelField(GetLocalizedText("step1Instr4"));
            
            EditorGUILayout.Space();
            if (GUILayout.Button(GetLocalizedText("next", GetLocalizedText("step2")), GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.CreateRules;
            }
        }

        private void DrawRuleCreationStep()
        {
            EditorGUILayout.HelpBox(GetLocalizedText("step2Help"), MessageType.Info);
            
            EditorGUILayout.Space();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetLocalizedText("createNewRule"), GUILayout.Height(40)))
            {
                CreateAutoTerrainTileRuleConfigerAsset();
            }
            if (GUILayout.Button(GetLocalizedText("selectExisting"), GUILayout.Height(40)))
            {
                SelectExistingAutoTerrainTileRuleConfiger();
            }
            GUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("instructions"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetLocalizedText("step2Instr1"));
            EditorGUILayout.LabelField(GetLocalizedText("step2Instr2"));
            EditorGUILayout.LabelField(GetLocalizedText("step2Instr3"));
            EditorGUILayout.LabelField(GetLocalizedText("step2Instr4"));
            
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetLocalizedText("previous"), GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.GeneratePalette;
            }
            if (GUILayout.Button(GetLocalizedText("next", GetLocalizedText("step3")), GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.ApplyTerrain;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawTerrainApplicationStep()
        {
            EditorGUILayout.HelpBox(GetLocalizedText("step3Help"), MessageType.Info);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button(GetLocalizedText("openTerrainEditor"), GUILayout.Height(40)))
            {
                AutoTerrainTileEditor.ShowWindow();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("instructions"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetLocalizedText("step3Instr1"));
            EditorGUILayout.LabelField(GetLocalizedText("step3Instr2"));
            EditorGUILayout.LabelField(GetLocalizedText("step3Instr3"));
            EditorGUILayout.LabelField(GetLocalizedText("step3Instr4"));
            
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GetLocalizedText("previous"), GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.CreateRules;
            }
            if (GUILayout.Button(GetLocalizedText("next", GetLocalizedText("step4")), GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.LayerEditing;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawLayerEditingStep()
        {
            EditorGUILayout.HelpBox(GetLocalizedText("step4Help"), MessageType.Info);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button(GetLocalizedText("openLayerTool"), GUILayout.Height(40)))
            {
                TilemapLayerTool.ShowWindow();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(GetLocalizedText("instructions"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetLocalizedText("step4Instr1"));
            EditorGUILayout.LabelField(GetLocalizedText("step4Instr2"));
            EditorGUILayout.LabelField(GetLocalizedText("step4Instr3"));
            EditorGUILayout.LabelField(GetLocalizedText("step4Instr4"));
            
            EditorGUILayout.Space();
            if (GUILayout.Button(GetLocalizedText("previous"), GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.ApplyTerrain;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(GetLocalizedText("workflowComplete"), MessageType.Info);
        }

        private void CreateAutoTerrainTileRuleConfigerAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Auto Terrain Tile", "NewAutoTerrainTileRuleConfiger", "asset", "Choose location");
            if (!string.IsNullOrEmpty(path))
            {
                AutoTerrainTileRuleConfiger newTile = CreateInstance<AutoTerrainTileRuleConfiger>();
                AssetDatabase.CreateAsset(newTile, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = newTile;
                EditorGUIUtility.PingObject(newTile);
            }
        }

        private void SelectExistingAutoTerrainTileRuleConfiger()
        {
            string path = EditorUtility.OpenFilePanel("Select Auto Terrain Tile", "Assets", "asset");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                AutoTerrainTileRuleConfiger existingTile = AssetDatabase.LoadAssetAtPath<AutoTerrainTileRuleConfiger>(relativePath);
                if (existingTile != null)
                {
                    Selection.activeObject = existingTile;
                    EditorGUIUtility.PingObject(existingTile);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Selected file is not a valid AutoTerrainTileRuleConfiger asset.", "OK");
                }
            }
        }
    }
    // 语言管理器类，用于集中管理插件的语言设置
    public static class TilemapLanguageManager
    {
        // 语言设置常量
        private const string PREF_LANGUAGE = "TilemapTools_Language";
        
        // 支持的语言枚举
        public enum Language
        {
            Auto,   // 自动跟随Unity编辑器
            English,
            Chinese
        }
        
        // 获取当前语言设置
        public static Language GetCurrentLanguageSetting()
        {
            return (Language)EditorPrefs.GetInt(PREF_LANGUAGE, (int)Language.Auto);
        }
        
        // 设置当前语言
        public static void SetCurrentLanguageSetting(Language language)
        {
            EditorPrefs.SetInt(PREF_LANGUAGE, (int)language);
        }
        
        // 获取实际使用的语言代码
        public static string GetCurrentLanguageCode()
        {
            Language setting = GetCurrentLanguageSetting();
            
            if (setting == Language.Auto)
            {
                // 获取Unity编辑器的当前语言
                string editorLanguage = EditorPrefs.GetString("Locale", "en");
                
                // 如果语言不是我们支持的，默认使用英语
                if (editorLanguage != "en" && editorLanguage != "zh-CN" && editorLanguage != "ja")
                {
                    return "en";
                }
                return editorLanguage;
            }
            else if (setting == Language.Chinese)
            {
                return "zh-CN";
            }
            else // English
            {
                return "en";
            }
        }
        
        // 获取语言名称的本地化文本
        public static string GetLanguageName(Language language)
        {
            string currentCode = GetCurrentLanguageCode();
            
            if (currentCode == "zh-CN")
            {
                switch (language)
                {
                    case Language.Auto: return "自动（跟随Unity）";
                    case Language.English: return "英语";
                    case Language.Chinese: return "中文";
                    default: return "未知";
                }
            }
            else // 默认英语
            {
                switch (language)
                {
                    case Language.Auto: return "Auto (Follow Unity)";
                    case Language.English: return "English";
                    case Language.Chinese: return "Chinese";
                    default: return "Unknown";
                }
            }
        }
    }
}
#endif
