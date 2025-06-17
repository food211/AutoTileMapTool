using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class SortToolEditor : EditorWindow
{
    private enum SortType
    {
        ByName,
        ByNameReverse,
        ByType,
        ByTypeReverse,
        ByPosition,
        BySiblingIndex,
        ByActive
    }

    private SortType currentSortType = SortType.ByName;

    [MenuItem("Tools/Sort Selected Objects")]
    public static void ShowWindow()
    {
        GetWindow<SortToolEditor>("Sort Objects");
    }

    private void OnGUI()
    {
        GUILayout.Label("Sort Selected Objects", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        currentSortType = (SortType)EditorGUILayout.EnumPopup("Sort Type", currentSortType);
        
        if (GUILayout.Button("Sort"))
        {
            SortSelectedObjects();
        }
    }

    private void SortSelectedObjects()
    {
        // 获取当前选中的所有游戏对象
        GameObject[] selectedObjects = Selection.gameObjects;
        
        if (selectedObjects == null || selectedObjects.Length <= 1)
        {
            EditorUtility.DisplayDialog("Sort Objects", "Please select at least two objects to sort.", "OK");
            return;
        }

        // 按照父对象分组
        var objectsByParent = selectedObjects.GroupBy(obj => obj.transform.parent);
        
        foreach (var group in objectsByParent)
        {
            Transform parent = group.Key;
            List<GameObject> children = group.ToList();
            
            // 根据选择的排序类型进行排序
            switch (currentSortType)
            {
                case SortType.ByName:
                    children = children.OrderBy(obj => obj.name).ToList();
                    break;
                case SortType.ByNameReverse:
                    children = children.OrderByDescending(obj => obj.name).ToList();
                    break;
                case SortType.ByType:
                    children = children.OrderBy(obj => obj.GetType().Name).ToList();
                    break;
                case SortType.ByTypeReverse:
                    children = children.OrderByDescending(obj => obj.GetType().Name).ToList();
                    break;
                case SortType.ByPosition:
                    children = children.OrderBy(obj => obj.transform.position.z)
                                      .ThenBy(obj => obj.transform.position.y)
                                      .ThenBy(obj => obj.transform.position.x).ToList();
                    break;
                case SortType.BySiblingIndex:
                    children = children.OrderBy(obj => obj.transform.GetSiblingIndex()).ToList();
                    break;
                case SortType.ByActive:
                    children = children.OrderByDescending(obj => obj.activeSelf).ToList();
                    break;
            }
            
            // 应用新的排序
            for (int i = 0; i < children.Count; i++)
            {
                Undo.RecordObject(children[i].transform, "Sort Objects");
                children[i].transform.SetSiblingIndex(i);
            }
        }
        
        EditorUtility.DisplayDialog("Sort Objects", "Objects sorted successfully!", "OK");
    }
}