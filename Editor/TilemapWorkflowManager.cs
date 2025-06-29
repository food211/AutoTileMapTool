#if UNITY_EDITOR
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
        }

        private void OnGUI()
        {
            GUILayout.Label("Tilemap Creation Workflow", EditorStyles.boldLabel);
            
            // 工作流步骤标签页
            currentStep = (WorkflowStep)GUILayout.Toolbar((int)currentStep, new string[]
            {
                "1. Generate Palette", "2. Create Rules", "3. Apply Terrain", "4. Layer Tools"
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
            EditorGUILayout.HelpBox("Step 1: Generate a Tile Palette from a sliced Sprite Sheet.", MessageType.Info);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Open Tile Palette Generator", GUILayout.Height(40)))
            {
                TilePaletteGenerator.ShowWindow();
            }
            
            // 显示最后生成的调色板
            if (!string.IsNullOrEmpty(lastGeneratedPalettePath))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Last Generated Palette:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(System.IO.Path.GetFileName(lastGeneratedPalettePath));
                if (GUILayout.Button("Select in Project"))
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
            EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Select a sliced sprite sheet texture");
            EditorGUILayout.LabelField("2. Configure output paths and settings");
            EditorGUILayout.LabelField("3. Click 'Generate Tile Palette'");
            EditorGUILayout.LabelField("4. Return here and go to next step");
            
            EditorGUILayout.Space();
            if (GUILayout.Button("Next: Create Rules →", GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.CreateRules;
            }
        }

        private void DrawRuleCreationStep()
        {
            EditorGUILayout.HelpBox("Step 2: Create an AutoTerrainTileRuleConfiger asset to define terrain rules.", MessageType.Info);
            
            EditorGUILayout.Space();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Auto Terrain Tile Rule", GUILayout.Height(40)))
            {
                CreateAutoTerrainTileRuleConfigerAsset();
            }
            if (GUILayout.Button("Select Existing Asset", GUILayout.Height(40)))
            {
                SelectExistingAutoTerrainTileRuleConfiger();
            }
            GUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Create or select an AutoTerrainTileRuleConfiger asset");
            EditorGUILayout.LabelField("2. Configure terrain rules in the Inspector");
            EditorGUILayout.LabelField("3. Assign sprites from your generated palette");
            EditorGUILayout.LabelField("4. Return here and go to next step");
            
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("← Previous", GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.GeneratePalette;
            }
            if (GUILayout.Button("Next: Apply Terrain →", GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.ApplyTerrain;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawTerrainApplicationStep()
        {
            EditorGUILayout.HelpBox("Step 3: Apply the rules to a base tilemap to generate the final terrain.", MessageType.Info);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Open Auto Terrain Editor", GUILayout.Height(40)))
            {
                AutoTerrainTileEditor.ShowWindow();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Select source and output tilemaps");
            EditorGUILayout.LabelField("2. Add your AutoTerrainTileRuleConfiger as iteration step");
            EditorGUILayout.LabelField("3. Apply terrain rules to generate final result");
            EditorGUILayout.LabelField("4. Return here for layer editing tools");
            
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("← Previous", GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.CreateRules;
            }
            if (GUILayout.Button("Next: Layer Tools →", GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.LayerEditing;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawLayerEditingStep()
        {
            EditorGUILayout.HelpBox("Step 4: Use advanced tools to edit and manage tilemap layers.", MessageType.Info);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Open Tilemap Layer Tool", GUILayout.Height(40)))
            {
                TilemapLayerTool.ShowWindow();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Select source and target tilemaps");
            EditorGUILayout.LabelField("2. Use selection tools to choose areas");
            EditorGUILayout.LabelField("3. Move, copy, or manipulate tile layers");
            EditorGUILayout.LabelField("4. Save selections for future use");
            
            EditorGUILayout.Space();
            if (GUILayout.Button("← Previous", GUILayout.Height(25)))
            {
                currentStep = WorkflowStep.ApplyTerrain;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Workflow Complete! You can cycle through steps as needed.", MessageType.Info);
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
}
#endif
