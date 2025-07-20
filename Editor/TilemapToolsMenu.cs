#if UNITY_EDITOR
using UnityEditor;

// 添加命名空间
namespace TilemapTools
{
    public static class TilemapToolsMenu
    {
        [MenuItem("Tools/Tilemap Tools/Workflow Manager", priority = 1)]
        public static void OpenWorkflowManager()
        {
            TilemapWorkflowManager.ShowWindow();
        }

        [MenuItem("Tools/Tilemap Tools/Setup New Project", priority = 2)]
        public static void SetupProject()
        {
            TilemapProjectSetup.SetupNewProject();
        }
    }
}
#endif
