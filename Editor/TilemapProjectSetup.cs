#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace TilemapTools
{
    public static class TilemapProjectSetup
    {
        public static void SetupNewProject()
        {
            CreateFolderIfNotExists("Assets/Tilemaps");
            CreateFolderIfNotExists("Assets/Tilemaps/Palettes");
            CreateFolderIfNotExists("Assets/Tilemaps/Rules");
            CreateFolderIfNotExists("Assets/Tilemaps/Sprites");
            
            EditorUtility.DisplayDialog("Setup Complete", 
                "Recommended folder structure created under Assets/Tilemaps.", "OK");
        }
        
        private static void CreateFolderIfNotExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parentFolder = Path.GetDirectoryName(path).Replace('\\', '/');
                string folderName = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }
    }
}
#endif
