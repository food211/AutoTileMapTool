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

        public static void ShowWindow()
        {
            GetWindow<TilePaletteGenerator>("Tile Palette Generator");
        }

        private void OnEnable() { LoadSettings(); }
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
            // ... (All your existing OnGUI code remains the same)
            GUILayout.Label("Tile Palette Generator", EditorStyles.boldLabel);
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture (Sliced)", sourceTexture, typeof(Texture2D), false);
            EditorGUILayout.HelpBox("Please use Sprite Editor to slice the texture first!", MessageType.Info);
            EditorGUILayout.Space();
            transparencyThreshold = EditorGUILayout.Slider("Transparency Threshold", transparencyThreshold, 0f, 1f);
            EditorGUILayout.HelpBox("Sprites with average alpha below this threshold will be ignored", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Deduplication Settings:", EditorStyles.boldLabel);
            dedupPrecision = (DedupPrecision)EditorGUILayout.EnumPopup("Deduplication Precision", dedupPrecision);
            string helpText = "";
            switch (dedupPrecision)
            {
                case DedupPrecision.Low: helpText = "Fast but less accurate. Good for simple tiles with distinct features."; break;
                case DedupPrecision.Medium: helpText = "Balanced performance and accuracy. Recommended for most cases."; break;
                case DedupPrecision.High: helpText = "Pixel-perfect comparison. Use for complex tiles with subtle differences."; break;
            }
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Palette Settings:", EditorStyles.boldLabel);
            paletteFileName = EditorGUILayout.TextField("Palette File Name", paletteFileName);
            EditorGUILayout.HelpBox("Enter the name for the generated palette (without .prefab extension)", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output Paths:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tiles Path:", GUILayout.Width(80));
            tilesOutputPath = EditorGUILayout.TextField(tilesOutputPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Tiles Output Folder", tilesOutputPath, "");
                if (!string.IsNullOrEmpty(selectedPath) && selectedPath.StartsWith(Application.dataPath))
                    tilesOutputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Palette Path:", GUILayout.Width(80));
            paletteOutputPath = EditorGUILayout.TextField(paletteOutputPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Palette Output Folder", paletteOutputPath, "");
                if (!string.IsNullOrEmpty(selectedPath) && selectedPath.StartsWith(Application.dataPath))
                    paletteOutputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            if (sourceTexture != null)
            {
                var sprites = GetSpritesFromTexture(sourceTexture);
                EditorGUILayout.HelpBox($"Found {sprites.Length} sprites in the texture", MessageType.Info);
            }
            bool canGenerate = sourceTexture != null && !string.IsNullOrEmpty(paletteFileName.Trim());
            if (!canGenerate && sourceTexture == null)
                EditorGUILayout.HelpBox("Please select a sliced source texture", MessageType.Warning);
            if (!canGenerate && string.IsNullOrEmpty(paletteFileName.Trim()))
                EditorGUILayout.HelpBox("Please enter a palette file name", MessageType.Warning);

            EditorGUI.BeginDisabledGroup(!canGenerate);
            if (GUILayout.Button("Generate Tile Palette", GUILayout.Height(30)))
            {
                SaveSettings();
                GenerateTilePalette(); // This method now handles the workflow logic
            }
            EditorGUI.EndDisabledGroup();
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
                if (EditorUtility.DisplayDialog("Palette Generated",
                    $"Tile palette '{sanitizedFileName}' has been created successfully!\n\nWould you like to proceed to the next step (Create Rules)?",
                    "Yes, Next Step", "Stay Here"))
                {
                    TilemapWorkflowManager.SetCurrentStep(TilemapWorkflowManager.WorkflowStep.CreateRules);
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
            tilemap.tileAnchor = new Vector3(0, 1, 0); // Anchor at top-left for consistency
            tilemapGO.AddComponent<TilemapRenderer>();

            foreach (var kvp in tilePositions)
            {
                Vector3Int position = new Vector3Int(kvp.Key.x, kvp.Key.y, 0);
                tilemap.SetTile(position, kvp.Value);
            }

            string prefabPath = Path.Combine(paletteOutputPath, $"{fileName}.prefab").Replace("\\", "/");

            if (File.Exists(prefabPath))
            {
                int choice = EditorUtility.DisplayDialogComplex("Palette Exists",
                    $"The palette '{prefabPath}' already exists.",
                    "Overwrite", "Cancel", "Create New with Suffix");

                switch (choice)
                {
                    case 0: // Overwrite
                        AssetDatabase.DeleteAsset(prefabPath);
                        break;
                    case 1: // Cancel
                        DestroyImmediate(paletteGO);
                        return null; // MODIFICATION: Return null on cancellation
                    case 2: // Create New
                        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                        break;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(paletteGO, prefabPath);
            DestroyImmediate(paletteGO);
            AssetDatabase.SaveAssets();
            Debug.Log($"Tile palette saved to: {prefabPath}");

            // MODIFICATION: Return the final path on success
            return prefabPath;
        }

        // ... (The rest of your script remains unchanged)
        private Tile CreateTileFromSprite(Sprite sprite, string hash, ref int createdTileAssets)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            string shortHash = hash.Substring(0, Mathf.Min(16, hash.Length));
            string path = $"{tilesOutputPath}/tile_{shortHash}.asset";
            bool fileExists = File.Exists(path);

            if (fileExists)
            {
                if (!overwriteChoice.HasValue)
                {
                    overwriteChoice = EditorUtility.DisplayDialog("Assets Exist", "Some tile assets already exist. Overwrite all existing files?", "Overwrite All", "Skip All");
                }
                if (!overwriteChoice.Value)
                {
                    // User chose to skip, so we load the existing asset instead of creating a new one.
                    return AssetDatabase.LoadAssetAtPath<Tile>(path);
                }
                AssetDatabase.DeleteAsset(path);
            }

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