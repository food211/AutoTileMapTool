#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RegionManager))]
public class RegionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        RegionManager regionManager = (RegionManager)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Regenerate All Regions"))
        {
            regionManager.RegenerateAllRegions();
        }
        
        EditorGUILayout.HelpBox("Use this button to update the regions after modifying the tilemap.", MessageType.Info);
    }
}
#endif
